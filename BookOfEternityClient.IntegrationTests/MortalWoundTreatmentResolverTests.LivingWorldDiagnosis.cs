using System.Text.Json;
using System.Text.Json.Nodes;
using BookOfEternityClient.Core;
using BookOfEternityClient.Models;
using BookOfEternityClient.Services;
using Microsoft.Extensions.Logging.Abstractions;
using Xunit;

namespace BookOfEternityClient.Tests;

public sealed partial class MortalWoundTreatmentResolverTests
{
    [Theory]
    [InlineData("missing")]
    [InlineData("rawT0")]
    [InlineData("changes")]
    public async Task LivingWorldDiagnosis_RejectsMissingStaleOrUnsupportedAssessmentBeforeSeal(string fault)
    {
        await using var fixture = await CreateLivingDiagnosisFixtureAsync();
        if (fault != "missing") await WriteLivingDiagnosisReportAsync(fixture, fault);
        var before = fixture.CaptureJson();
        var result = await ComposeLivingDiagnosisAsync(fixture);
        Assert.False(result.IsValid);
        Assert.Null(result.Plan);
        Assert.Contains(result.Issues, issue => issue.Code?.StartsWith("mortal_living_world_", StringComparison.Ordinal) == true);
        fixture.AssertJson(before);
    }

    [Fact]
    public async Task LivingWorldDiagnosis_T1AssessmentIsInsideExactCommonPlanBeforePublication()
    {
        await using var fixture = await CreateLivingDiagnosisFixtureAsync();
        await WriteLivingDiagnosisReportAsync(fixture);
        var reportBefore = await fixture.Files.ReadFileBytesAsync(fixture.Lease, MortalLivingWorldSource.ReportPath);
        var before = fixture.CaptureJson();
        var result = await ComposeLivingDiagnosisAsync(fixture);
        Assert.True(result.IsValid, JsonSerializer.Serialize(result.Issues));
        var plan = Assert.IsType<AcceptedMechanicsPlan>(result.Plan);
        Assert.Contains(MortalLivingWorldSource.ReportPath, plan.ConsumedPaths);
        Assert.Contains(MortalLivingWorldSource.ReportPath, plan.TouchedPaths);
        Assert.Contains(MortalLivingWorldSource.HistoryPath, plan.TouchedPaths);
        Assert.Equal(reportBefore, plan.BeforeImages[MortalLivingWorldSource.ReportPath].Bytes);
        Assert.False(plan.BeforeImages[MortalLivingWorldSource.HistoryPath].Existed);
        var history = plan.OwnerCompanionAfterImages[MortalLivingWorldSource.HistoryPath];
        Assert.Single(history["entries"]!.AsArray());
        var input = fixture.Authority.ReadDiagnosisOriginalInput();
        var frame = MortalWoundDiagnosisInputCodec.Read(input).Frame;
        Assert.Equal(frame.Progression.CurrentWorldTimeInMinutes + 30,
            plan.OwnerCompanionAfterImages[EffectAcceptedTurnInputComposer.WorldTimePath]["currentTimeInMinutes"]!.GetValue<long>());
        fixture.AssertJson(before);
    }

    [Fact]
    public async Task LivingWorldDiagnosis_CommonPublicationCompensatesEntireReceiptThenRetryAppendsOnce()
    {
        await using var fixture = await CreateLivingDiagnosisFixtureAsync();
        await WriteLivingDiagnosisReportAsync(fixture);
        var files = fixture.Files;
        var frame = MortalWoundDiagnosisInputCodec.Read(fixture.Authority.ReadDiagnosisOriginalInput()).Frame;
        var before = fixture.CaptureJson();
        var composed = await ComposeLivingDiagnosisAsync(fixture);
        Assert.True(composed.IsValid, JsonSerializer.Serialize(composed.Issues));
        await fixture.ReleaseLeaseAsync();
        var normalizer = new CanonicalStateNormalizer(files, NullLogger<CanonicalStateNormalizer>.Instance);
        var validator = new ValidationService(files, NullLogger<ValidationService>.Instance);
        var first = await AcceptedTurnCanonicalStateRefresh.NormalizeAndValidateWithPlanAsync(files, normalizer, validator,
            new Dictionary<string, string>(StringComparer.Ordinal));
        var transaction = Assert.IsType<MortalWoundTreatmentResourcePublicationTransaction>(first.TreatmentResourcePublicationTransaction);
        try
        {
            Assert.DoesNotContain(first.Issues, issue => issue.Severity == IssueSeverity.Error);
            Assert.Null(await files.ReadFileBytesAsync(MortalLivingWorldSource.ReportPath));
            var history = JsonNode.Parse(Assert.IsType<string>(await files.ReadFileAsync(MortalLivingWorldSource.HistoryPath)))!;
            Assert.Single(history["entries"]!.AsArray());
            var compensation = await transaction.CompensateAsync(files);
            Assert.True(compensation.IsValid, JsonSerializer.Serialize(compensation.Issues));
            Assert.Equal(MortalWoundTreatmentPublicationTransactionOutcome.Rearmed, compensation.Outcome);
            fixture.AssertJson(before);
        }
        finally { await ((IAsyncDisposable)transaction).DisposeAsync(); }
        await fixture.AcquireLeaseAsync();
        var retried = await WoundApplicationService.ResolveMortalDiagnosisInputAsync(files, fixture.Lease, frame, requireRetained: true);
        var retry = await WoundAcceptedTurnPlanner.ComposeMortalDiagnosisPublicationAsync(files, fixture.Lease,
            new GameResponse(), retried.Owner, retried.Request);
        Assert.True(retry.IsValid, JsonSerializer.Serialize(retry.Issues));
        await fixture.ReleaseLeaseAsync();
        var second = await AcceptedTurnCanonicalStateRefresh.NormalizeAndValidateWithPlanAsync(files, normalizer, validator,
            new Dictionary<string, string>(StringComparer.Ordinal));
        var completed = Assert.IsType<MortalWoundTreatmentResourcePublicationTransaction>(second.TreatmentResourcePublicationTransaction);
        try
        {
            Assert.DoesNotContain(second.Issues, issue => issue.Severity == IssueSeverity.Error);
            var advance = await completed.AdvancePublishedAgreementWithProgressionAsync(files,
                new ProgressionScheduleService(files, NullLogger<ProgressionScheduleService>.Instance), frame.Progression);
            Assert.True(advance.IsValid, JsonSerializer.Serialize(advance.Issues));
            var close = await completed.CompleteAsync(files);
            Assert.True(close.IsValid, JsonSerializer.Serialize(close.Issues));
            Assert.Null(await files.ReadFileBytesAsync(MortalLivingWorldSource.ReportPath));
            var history = JsonNode.Parse(Assert.IsType<string>(await files.ReadFileAsync(MortalLivingWorldSource.HistoryPath)))!;
            Assert.Single(history["entries"]!.AsArray());
        }
        finally { await ((IAsyncDisposable)completed).DisposeAsync(); }
    }

    private static async Task<DiagnosisOwnershipFixture> CreateLivingDiagnosisFixtureAsync()
    {
        var fixture = await DiagnosisOwnershipFixture.CreateAsync(20, signedInput: true, duration: 30,
            beforeResourceBootstrap: files =>
            {
                // Initial scenario only: a real scheduler baseline 230 minutes before the signed T0.
                var path = files.ResolvePath(EffectAcceptedTurnInputComposer.WorldTimePath);
                var current = File.ReadAllBytes(path);
                var clock = JsonNode.Parse(FileSystemManager.DecodeFileBytes(current))!.AsObject();
                clock["currentTimeInMinutes"] = clock["currentTimeInMinutes"]!.GetValue<int>() - 230;
                File.WriteAllText(path, clock.ToJsonString());
                try
                {
                    _ = new ProgressionScheduleService(files, NullLogger<ProgressionScheduleService>.Instance)
                        .BuildControlForNextTurnAsync("Mortal World").GetAwaiter().GetResult();
                }
                finally { File.WriteAllBytes(path, current); }
            });
        try
        {
            var frame = MortalWoundDiagnosisInputCodec.Read(fixture.Authority.ReadDiagnosisOriginalInput()).Frame;
            _ = await WoundApplicationService.ResolveMortalDiagnosisInputAsync(fixture.Files, fixture.Lease, frame);
            return fixture;
        }
        catch { await fixture.DisposeAsync(); throw; }
    }

    private static async Task WriteLivingDiagnosisReportAsync(DiagnosisOwnershipFixture fixture, string? fault = null)
    {
        var frame = MortalWoundDiagnosisInputCodec.Read(fixture.Authority.ReadDiagnosisOriginalInput()).Frame;
        var control = frame.Progression;
        Assert.False(control.MustEvaluateWorldProgression);
        Assert.Equal(230, control.CurrentWorldTimeInMinutes - control.LastWorldSimulationTimeInMinutes);
        var audit = new JsonArray(new[] { "npcs", "factions", "locations", "projects", "world" }.Select(scope =>
            (JsonNode)new JsonObject { ["scope"] = scope, ["reason"] = "No significant autonomous change during this diagnosis." }).ToArray());
        var report = new JsonObject
        {
            ["schemaVersion"] = 1, ["sessionId"] = frame.SessionId, ["requestId"] = frame.RequestId,
            ["turn"] = frame.Turn, ["fromMinute"] = control.LastWorldSimulationTimeInMinutes,
            ["toMinute"] = control.CurrentWorldTimeInMinutes + (fault == "rawT0" ? 0 : 30),
            ["resolution"] = fault == "changes" ? "changes" : "noSignificantChange",
            ["audit"] = audit, ["events"] = fault == "changes" ? new JsonArray(new JsonObject { ["eventId"] = "unsupported" }) : new JsonArray()
        };
        await fixture.Files.WriteFileAtomicAsync(fixture.Lease, MortalLivingWorldSource.ReportPath, report.ToJsonString());
    }

    private static async Task<WoundAcceptedTurnPlanner.MortalWoundTreatmentPublicationResult> ComposeLivingDiagnosisAsync(DiagnosisOwnershipFixture fixture)
    {
        var frame = MortalWoundDiagnosisInputCodec.Read(fixture.Authority.ReadDiagnosisOriginalInput()).Frame;
        var admission = await WoundApplicationService.ResolveMortalDiagnosisInputAsync(fixture.Files, fixture.Lease, frame);
        var validator = new ValidationService(fixture.Files, NullLogger<ValidationService>.Instance);
        Assert.Empty(await validator.ValidateAcceptedTurnRawMortalLocationMaterializationAsync(fixture.Lease));
        Assert.Empty(await validator.ValidateAcceptedTurnRawMortalItemMaterializationAsync(fixture.Lease));
        return await WoundAcceptedTurnPlanner.ComposeMortalDiagnosisPublicationAsync(fixture.Files, fixture.Lease,
            new GameResponse(), admission.Owner, admission.Request);
    }
}
