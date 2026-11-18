using System.Text;
using BookOfEternityClient.Configuration;
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
            await ReadLivingDiagnosisBackupsAsync(files));
        Assert.DoesNotContain(first.Issues, issue => issue.Severity == IssueSeverity.Error);
        Assert.True(first.TreatmentResourcePublicationTransaction is not null, JsonSerializer.Serialize(first.Issues));
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
            await ReadLivingDiagnosisBackupsAsync(files));
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

    [Theory]
    [InlineData("none")]
    [InlineData("clock")]
    [InlineData("history")]
    [InlineData("control")]
    [InlineData("requestAction")]
    [InlineData("readyFiles")]
    [InlineData("repair")]
    public async Task LivingWorldDiagnosis_DirectCompletionChecksFinalAndOriginalAgreement(string drift)
    {
        await using var fixture = await CreateLivingDiagnosisFixtureAsync();
        await WriteLivingDiagnosisReportAsync(fixture);
        var before = fixture.CaptureJson();
        var composed = await ComposeLivingDiagnosisAsync(fixture);
        Assert.True(composed.IsValid, JsonSerializer.Serialize(composed.Issues));
        await fixture.ReleaseLeaseAsync();
        var files = fixture.Files;
        var result = await AcceptedTurnCanonicalStateRefresh.NormalizeAndValidateWithPlanAsync(files,
            new CanonicalStateNormalizer(files, NullLogger<CanonicalStateNormalizer>.Instance),
            new ValidationService(files, NullLogger<ValidationService>.Instance),
            await ReadLivingDiagnosisBackupsAsync(files));
        Assert.DoesNotContain(result.Issues, issue => issue.Severity == IssueSeverity.Error);
        Assert.True(result.TreatmentResourcePublicationTransaction is not null, JsonSerializer.Serialize(result.Issues));
        var transaction = Assert.IsType<MortalWoundTreatmentResourcePublicationTransaction>(result.TreatmentResourcePublicationTransaction);
        try
        {
            if (drift == "none")
            {
                var complete = await transaction.CompleteAsync(files);
                Assert.True(complete.IsValid, JsonSerializer.Serialize(complete.Issues));
                Assert.Null(await files.ReadFileBytesAsync(MortalLivingWorldSource.ReportPath));
                var history = JsonNode.Parse(Assert.IsType<string>(await files.ReadFileAsync(MortalLivingWorldSource.HistoryPath)))!;
                Assert.Single(history["entries"]!.AsArray());
                return;
            }
            var path = drift == "clock" ? MortalLivingWorldSource.WorldTimePath :
                drift == "history" ? MortalLivingWorldSource.HistoryPath : drift == "readyFiles" ? "ready/turn_complete.json" :
                drift == "repair" ? "game_state/control/validation_repair_request.json" : "input/turn_request.json";
            var physical = files.ResolvePath(path);
            var published = File.Exists(physical) ? File.ReadAllBytes(physical) : null;
            var changed = published is null ? new JsonObject() : JsonNode.Parse(FileSystemManager.DecodeFileBytes(published))!.AsObject();
            if (drift == "clock") changed["currentTimeInMinutes"] = changed["currentTimeInMinutes"]!.GetValue<long>() + 1;
            else if (drift == "history") changed["entries"]!.AsArray().Clear();
            else if (drift == "control") changed["progressionControl"]!["worldCycleMinutes"] = 1;
            else if (drift == "requestAction") changed["playerAction"] = "A different action with the same original tuple and control";
            else if (drift == "readyFiles") changed["filesModified"] = new JsonArray("game_state/world/world_flags.json");
            else changed["issues"] = new JsonArray("foreign repair under the same original");
            Directory.CreateDirectory(Path.GetDirectoryName(physical)!);
            File.WriteAllText(physical, changed.ToJsonString());
            try
            {
                var complete = await transaction.CompleteAsync(files);
                Assert.False(complete.IsValid);
            }
            finally { if (published is null) File.Delete(physical); else File.WriteAllBytes(physical, published); }
            // Restore only the external corruption; the transaction itself must compensate every owned output.
            var compensation = await transaction.CompensateAsync(files);
            Assert.True(compensation.IsValid, JsonSerializer.Serialize(compensation.Issues));
            fixture.AssertJson(before);
        }
        finally { await ((IAsyncDisposable)transaction).DisposeAsync(); }
    }

    [Fact]
    public async Task LivingWorldDiagnosis_BrowserCompletionReconstructsActualSpendAndRejectsReceiptTamper()
    {
        await using var fixture = await CreateLivingDiagnosisFixtureAsync(resourceCost: true);
        await WriteLivingDiagnosisReportAsync(fixture);
        var files = fixture.Files;
        var input = fixture.Authority.ReadDiagnosisOriginalInput();
        var selected = MortalWoundDiagnosisInputCodec.Read(input);
        var original = JsonSerializer.Deserialize<TurnRequest>(Assert.IsType<string>(
            await files.ReadFileAsync(fixture.Lease, LiveTurnPreparationService.TurnRequestPath)), SharedJsonOptions.PrettyCamelCaseUnsafeRelaxed)!;
        var token = fixture.Authority.Binding.SnapshotToken;
        var generation = fixture.Authority.SessionGeneration;
        var composed = await ComposeLivingDiagnosisAsync(fixture);
        Assert.True(composed.IsValid, JsonSerializer.Serialize(composed.Issues));
        var publication = Assert.IsType<MortalWoundTreatmentResourcePublicationAuthority>(composed.Plan!.TreatmentResourcePublicationAuthority);
        var semantic = publication.SemanticFingerprint;
        var identity = publication.IdentitySeed;
        await fixture.ReleaseLeaseAsync();
        var result = await AcceptedTurnCanonicalStateRefresh.NormalizeAndValidateWithPlanAsync(files,
            new CanonicalStateNormalizer(files, NullLogger<CanonicalStateNormalizer>.Instance),
            new ValidationService(files, NullLogger<ValidationService>.Instance), await ReadLivingDiagnosisBackupsAsync(files));
        Assert.DoesNotContain(result.Issues, issue => issue.Severity == IssueSeverity.Error);
        Assert.True(result.TreatmentResourcePublicationTransaction is not null, JsonSerializer.Serialize(result.Issues));
        var transaction = Assert.IsType<MortalWoundTreatmentResourcePublicationTransaction>(result.TreatmentResourcePublicationTransaction);
        try
        {
            var complete = await transaction.CompleteAsync(files);
            Assert.True(complete.IsValid, JsonSerializer.Serialize(complete.Issues));
        }
        finally { await ((IAsyncDisposable)transaction).DisposeAsync(); }
        await fixture.AcquireLeaseAsync();
        var current = new Dictionary<string, byte[]?>(StringComparer.Ordinal);
        async Task<byte[]?> Read(string path)
        {
            var bytes = await files.ReadFileBytesAsync(fixture.Lease, path);
            current.Add(path, bytes?.ToArray());
            return bytes;
        }
        var settled = await MortalWoundDiagnosisCompletion.RequirePublishedAsync(original, token, generation,
            selected.Selection, input, Read);
        Assert.Equal(semantic, settled.ReconstructPublicationSemantic());
        var finalization = MortalWoundTreatmentResourceComposer.Finalize(settled.Resolution).Finalization!;
        Assert.Equal(identity, MortalWoundTreatmentResourcePublicationAuthority.ComputeIdentitySeed(settled.Source.AcceptedStateFingerprint,
            settled.Request.RequestFingerprint, settled.Resolution.ResultFingerprint, finalization.FinalizationFingerprint,
            settled.ReconstructPublicationSemantic()));
        Assert.NotNull(settled.LivingWorldComparison);
        foreach (var fault in new[] { "history", "report", "comparison" })
        {
            var changed = current.ToDictionary(pair => pair.Key, pair => pair.Value?.ToArray(), StringComparer.Ordinal);
            if (fault == "history") changed[MortalLivingWorldSource.HistoryPath] = null;
            else if (fault == "report") changed[MortalLivingWorldSource.ReportPath] = Encoding.UTF8.GetBytes("{}");
            else
            {
                var history = JsonNode.Parse(CanonicalJsonUtf8.DecodeOneOptionalBom(changed[WoundHistoryState.HistoryPath]!))!.AsObject();
                var row = history["transitions"]!.AsArray().Single(value => value!["kind"]!.GetValue<string>() == "diagnose");
                row!["transitionResult"]!["publicationInputs"]!["livingWorld"]!["context"]!["ResultMinute"] = 1;
                changed[WoundHistoryState.HistoryPath] = Encoding.UTF8.GetBytes(history.ToJsonString());
            }
            await Assert.ThrowsAsync<InvalidDataException>(() => MortalWoundDiagnosisCompletion.RequirePublishedAsync(
                original, token, generation, selected.Selection, input, path => Task.FromResult(changed[path])));
        }
    }

    private static async Task<Dictionary<string, string>> ReadLivingDiagnosisBackupsAsync(FileSystemManager files)
    {
        var manifest = JsonNode.Parse(Assert.IsType<string>(await files.ReadFileAsync(
            LiveTurnPreparationService.PendingTurnSnapshotManifestPath)))!.AsObject();
        var backups = manifest["files"]!.AsObject().ToDictionary(pair => pair.Key,
            pair => pair.Value!.GetValue<string>(), StringComparer.Ordinal);
        Assert.Contains(MortalStorageAccessProjection.StatePath, backups.Keys);
        return backups;
    }

    private static async Task<DiagnosisOwnershipFixture> CreateLivingDiagnosisFixtureAsync(bool resourceCost = false)
    {
        var fixture = await DiagnosisOwnershipFixture.CreateAsync(20, signedInput: true, duration: 30, resourceCost: resourceCost,
            beforeResourceBootstrap: files =>
            {
                // Complete the canonical scenario before LivePrepare signs its originals.
                // Effective access is client-derived from the seeded location metadata.
                var map = JsonNode.Parse(File.ReadAllText(files.ResolvePath("game_state/world/world_map.json")))!.AsObject();
                var access = MortalStorageAccessProjection.Build(map, null);
                Assert.True(access.IsValid, JsonSerializer.Serialize(access.Issues));
                File.WriteAllText(files.ResolvePath(MortalStorageAccessProjection.StatePath), access.Root!.ToJsonString());

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
