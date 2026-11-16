using System.Text.Json;
using System.Text.Json.Nodes;
using BookOfEternityClient.Configuration;
using BookOfEternityClient.Core;
using BookOfEternityClient.Services;
using Microsoft.Extensions.Logging.Abstractions;
using Xunit;

namespace BookOfEternityClient.Tests;

public sealed partial class JointBundleComponentTests
{
    /// <summary>A due cycle without its private assessment refuses before any joint take or marker publication.</summary>
    [Fact]
    public async Task LivingWorldJoint_DueMissingReportRefusesBeforePublicationTake()
    {
        await using var fixture = new Fixture(_output, nameof(LivingWorldJoint_DueMissingReportRefusesBeforePublicationTake));
        await fixture.InitializeAsync();
        fixture.ConfigureLivingWorldJoint("missing");
        var bound = await fixture.PrepareBoundJointAsync();
        if (!bound.Success)
            Assert.Contains(bound.Issues, issue => issue.Code == "mortal_living_world_report_required");
        else
        {
            var plan = await WoundAcceptedTurnPlanner.CreateJointRepairCommonPlanFromBoundForComponentAsync(
                fixture.Files, fixture.Lease, bound);
            Assert.False(plan.Success);
            Assert.Contains(plan.Issues, issue => issue.Code == "mortal_living_world_report_required");
        }
        Assert.Null(await fixture.Files.ReadFileBytesAsync(fixture.Lease, MortalLivingWorldSource.HistoryPath));
        Assert.Null(await fixture.Files.ReadFileBytesAsync(fixture.Lease, MortalQteDecisionPublication.StatePath));
        Assert.Equal(0, fixture.LivingWorldCommonPublicationCount);
    }

    /// <summary>The genuine joint common owner seals one receipt in its cohort and both real progression phases recover it.</summary>
    [Fact]
    public async Task LivingWorldJoint_ValidNoChangeReceiptBelongsToCommonCohortAndBothPhases()
    {
        await using var fixture = new Fixture(_output, nameof(LivingWorldJoint_ValidNoChangeReceiptBelongsToCommonCohortAndBothPhases));
        await fixture.InitializeAsync();
        fixture.ConfigureLivingWorldJoint("valid");
        var bound = await fixture.PrepareBoundJointAsync();
        Assert.True(bound.Success, JsonSerializer.Serialize(bound.Issues));
        var common = await WoundAcceptedTurnPlanner.CreateJointRepairCommonPlanFromBoundForComponentAsync(
            fixture.Files, fixture.Lease, bound);
        Assert.True(common.Success, JsonSerializer.Serialize(common.Issues));
        var plan = Assert.IsType<AcceptedMechanicsPlan>(common.Plan);
        Assert.Contains(MortalLivingWorldSource.HistoryPath, plan.TouchedPaths);
        Assert.True(plan.BeforeImages.TryGetValue(MortalLivingWorldSource.HistoryPath, out var before));
        Assert.False(before.Existed);
        var intended = plan.OwnerCompanionAfterImages[MortalLivingWorldSource.HistoryPath]!.AsObject();
        var receipt = Assert.Single(intended["entries"]!.AsArray())!.AsObject();
        Assert.Equal(fixture.JointOriginal.SessionId, receipt["sessionId"]!.GetValue<string>());
        Assert.Equal(fixture.JointOriginal.RequestId, receipt["requestId"]!.GetValue<string>());
        Assert.Equal(fixture.JointOriginal.SnapshotToken, receipt["snapshotToken"]!.GetValue<string>());
        Assert.Equal(fixture.JointOriginal.Turn, receipt["turn"]!.GetValue<int>());

        var published = await fixture.PublishLivingWorldJointCommonAsync();
        Assert.Equal(MortalJointRepairPublicationDisposition.Committed, published.Disposition);
        Assert.Empty(published.Issues);
        Assert.Contains(fixture.LivingWorldCommittedCommonMembers,
            paths => paths.Contains(MortalLivingWorldSource.HistoryPath, StringComparer.Ordinal) &&
                     paths.Contains(MortalQteDecisionPublication.StatePath, StringComparer.Ordinal));
        var accepted = await fixture.Files.ReadFileBytesAsync(fixture.Lease, MortalLivingWorldSource.HistoryPath);
        Assert.NotNull(accepted);
        Assert.Single(JsonNode.Parse(CanonicalJsonUtf8.DecodeOneOptionalBom(accepted))!["entries"]!.AsArray());
        var original = fixture.JointOriginal;
        var unapplied = await MortalQteDecisionPublication.ReadJointProgressionPhaseAsync(fixture.Files, fixture.Lease,
            original.SessionId, original.RequestId, original.SnapshotToken, original.Turn);
        Assert.Equal("unapplied", unapplied.Phase);
        var applied = await MortalQteDecisionPublication.CommitJointProgressionPhaseAsync(fixture.Files, fixture.Lease, unapplied);
        Assert.Equal(MortalQteDecisionPublication.JointProgressionPublicationDisposition.Applied, applied.Disposition);
        var fresh = await MortalQteDecisionPublication.ReadJointProgressionPhaseAsync(fixture.Files, fixture.Lease,
            original.SessionId, original.RequestId, original.SnapshotToken, original.Turn);
        Assert.Equal("applied", fresh.Phase);
        Assert.Equal(accepted, await fixture.Files.ReadFileBytesAsync(fixture.Lease, MortalLivingWorldSource.HistoryPath));
    }

    /// <summary>A wrong assessment window is refused before taking or publishing the common candidate.</summary>
    [Fact]
    public async Task LivingWorldJoint_WrongWindowRefusesBeforeCommonPublication()
    {
        await using var fixture = new Fixture(_output, nameof(LivingWorldJoint_WrongWindowRefusesBeforeCommonPublication));
        await fixture.InitializeAsync();
        fixture.ConfigureLivingWorldJoint("wrongWindow");
        var bound = await fixture.PrepareBoundJointAsync();
        if (!bound.Success) Assert.Contains(bound.Issues, issue => issue.Code == "mortal_living_world_window_invalid");
        else
        {
            var common = await WoundAcceptedTurnPlanner.CreateJointRepairCommonPlanFromBoundForComponentAsync(
                fixture.Files, fixture.Lease, bound);
            Assert.False(common.Success);
            Assert.Contains(common.Issues, issue => issue.Code == "mortal_living_world_window_invalid");
        }
        Assert.Null(await fixture.Files.ReadFileBytesAsync(fixture.Lease, MortalLivingWorldSource.HistoryPath));
        Assert.Null(await fixture.Files.ReadFileBytesAsync(fixture.Lease, MortalQteDecisionPublication.StatePath));
        Assert.Equal(0, fixture.LivingWorldCommonPublicationCount);
    }

    /// <summary>An event with no real canonical effect is refused either by preseal projection or final owner validation.</summary>
    [Fact]
    public async Task LivingWorldJoint_WrongEffectCannotCompleteOrRetainHistory()
    {
        await using var fixture = new Fixture(_output, nameof(LivingWorldJoint_WrongEffectCannotCompleteOrRetainHistory));
        await fixture.InitializeAsync();
        fixture.ConfigureLivingWorldJoint("wrongEffect");
        var bound = await fixture.PrepareBoundJointAsync();
        if (!bound.Success)
            Assert.Contains(bound.Issues, issue => issue.Code?.StartsWith("mortal_living_world_", StringComparison.Ordinal) == true);
        else
        {
            var common = await WoundAcceptedTurnPlanner.CreateJointRepairCommonPlanFromBoundForComponentAsync(
                fixture.Files, fixture.Lease, bound);
            if (!common.Success)
                Assert.Contains(common.Issues, issue => issue.Code?.StartsWith("mortal_living_world_", StringComparison.Ordinal) == true);
            else
            {
                var before = common.Plan!.BeforeImages.ToDictionary(row => row.Key, row => row.Value.Bytes,
                    StringComparer.Ordinal);
                var rejected = await fixture.PublishLivingWorldJointCommonAsync();
                Assert.Equal(MortalJointRepairPublicationDisposition.RetryableHeld, rejected.Disposition);
                Assert.Contains(rejected.Issues, issue => issue.Code is "mortal_living_world_lifecycle_invalid" or
                    "mortal_living_world_event_invalid" or "mortal_joint_repair_publication_refused");
                foreach (var row in before)
                    Assert.Equal(row.Value, await fixture.Files.ReadFileBytesAsync(fixture.Lease, row.Key));
            }
        }
        Assert.Null(await fixture.Files.ReadFileBytesAsync(fixture.Lease, MortalLivingWorldSource.HistoryPath));
        Assert.Null(await fixture.Files.ReadFileBytesAsync(fixture.Lease, MortalQteDecisionPublication.StatePath));
    }

    /// <summary>Post-commit disappearance of the actual receipt member is stopped by exact readback before proof minting.</summary>
    [Fact]
    public async Task LivingWorldJoint_PostCommitReceiptDriftFailsExactFenceAndRestoresWholeCohort()
    {
        await using var fixture = new Fixture(_output, nameof(LivingWorldJoint_PostCommitReceiptDriftFailsExactFenceAndRestoresWholeCohort));
        await fixture.InitializeAsync();
        fixture.ConfigureLivingWorldJoint("valid");
        var bound = await fixture.PrepareBoundJointAsync();
        Assert.True(bound.Success, JsonSerializer.Serialize(bound.Issues));
        var common = await WoundAcceptedTurnPlanner.CreateJointRepairCommonPlanFromBoundForComponentAsync(
            fixture.Files, fixture.Lease, bound);
        Assert.True(common.Success, JsonSerializer.Serialize(common.Issues));
        var before = common.Plan!.BeforeImages.ToDictionary(row => row.Key, row => row.Value.Bytes,
            StringComparer.Ordinal);
        fixture.ArmLivingWorldPostCommitReceiptDrift();
        var rejected = await fixture.PublishLivingWorldJointCommonAsync();
        Assert.Equal(1, fixture.LivingWorldPostCommitDrifts);
        Assert.Equal(1, fixture.LivingWorldCommonPublicationCount);
        Assert.Equal(MortalJointRepairPublicationDisposition.RetryableHeld, rejected.Disposition);
        foreach (var row in before)
            Assert.Equal(row.Value, await fixture.Files.ReadFileBytesAsync(fixture.Lease, row.Key));
        Assert.Null(await fixture.Files.ReadFileBytesAsync(fixture.Lease, MortalLivingWorldSource.HistoryPath));
        Assert.Null(await fixture.Files.ReadFileBytesAsync(fixture.Lease, MortalQteDecisionPublication.StatePath));
        // This proves the exact member fence, not the semantic effect/clock gate.
    }

    /// <summary>A reached fault in the real common member batch restores absent history and permits one exact owning retry.</summary>
    [Fact]
    public async Task LivingWorldJoint_ReachedCommonMemberFailureRestoresReceiptAndRetriesOnce()
    {
        await using var fixture = new Fixture(_output, nameof(LivingWorldJoint_ReachedCommonMemberFailureRestoresReceiptAndRetriesOnce));
        await fixture.InitializeAsync();
        fixture.ConfigureLivingWorldJoint("valid");
        var bound = await fixture.PrepareBoundJointAsync();
        Assert.True(bound.Success, JsonSerializer.Serialize(bound.Issues));
        var common = await WoundAcceptedTurnPlanner.CreateJointRepairCommonPlanFromBoundForComponentAsync(
            fixture.Files, fixture.Lease, bound);
        Assert.True(common.Success, JsonSerializer.Serialize(common.Issues));
        var before = common.Plan!.BeforeImages.ToDictionary(row => row.Key, row => row.Value.Bytes,
            StringComparer.Ordinal);
        Assert.False(common.Plan.BeforeImages[MortalLivingWorldSource.HistoryPath].Existed);
        fixture.ArmLivingWorldCommonMemberFault();
        var failed = await fixture.PublishLivingWorldJointCommonAsync();
        Assert.Equal(1, fixture.LivingWorldFaults);
        Assert.Equal(MortalJointRepairPublicationDisposition.RetryableHeld, failed.Disposition);
        foreach (var row in before)
            Assert.Equal(row.Value, await fixture.Files.ReadFileBytesAsync(fixture.Lease, row.Key));
        Assert.Null(await fixture.Files.ReadFileBytesAsync(fixture.Lease, MortalLivingWorldSource.HistoryPath));
        Assert.Null(await fixture.Files.ReadFileBytesAsync(fixture.Lease, MortalQteDecisionPublication.StatePath));

        var retried = await fixture.PublishLivingWorldJointCommonAsync();
        Assert.Equal(MortalJointRepairPublicationDisposition.Committed, retried.Disposition);
        Assert.Equal(1, fixture.LivingWorldFaults);
        var history = JsonNode.Parse(CanonicalJsonUtf8.DecodeOneOptionalBom(
            (await fixture.Files.ReadFileBytesAsync(fixture.Lease, MortalLivingWorldSource.HistoryPath))!))!.AsObject();
        Assert.Single(history["entries"]!.AsArray());
    }

    /// <summary>A fresh phase read rejects internally rehashed history that differs from the accepted assessment.</summary>
    [Fact]
    public async Task LivingWorldJoint_FreshPhaseRecoveryRejectsIndividuallyRehashedDifferentAssessment()
    {
        await using var fixture = new Fixture(_output, nameof(LivingWorldJoint_FreshPhaseRecoveryRejectsIndividuallyRehashedDifferentAssessment));
        await fixture.InitializeAsync();
        fixture.ConfigureLivingWorldJoint("valid");
        var bound = await fixture.PrepareBoundJointAsync();
        Assert.True(bound.Success, JsonSerializer.Serialize(bound.Issues));
        var sourceRead = await MortalLivingWorldSource.PrepareAsync(fixture.Files, fixture.Lease);
        Assert.Empty(sourceRead.Issues);
        var source = Assert.IsType<MortalLivingWorldSource>(sourceRead.Source);
        var report = JsonNode.Parse(CanonicalJsonUtf8.DecodeOneOptionalBom(
            source.ReadOpeningBytes(MortalLivingWorldSource.ReportPath)!))!.AsObject();
        var common = await WoundAcceptedTurnPlanner.CreateJointRepairCommonPlanFromBoundForComponentAsync(
            fixture.Files, fixture.Lease, bound);
        Assert.True(common.Success, JsonSerializer.Serialize(common.Issues));
        var result = await fixture.PublishLivingWorldJointCommonAsync();
        Assert.Equal(MortalJointRepairPublicationDisposition.Committed, result.Disposition);
        var accepted = (await fixture.Files.ReadFileBytesAsync(fixture.Lease, MortalLivingWorldSource.HistoryPath))!;
        report["audit"]![0]!["reason"] = "Different internally valid assessment after acceptance.";
        var forged = MortalLivingWorldReceipt.Build(source.SnapshotToken, source.Context, report, null);
        Assert.NotEqual(accepted, forged.HistoryBytes);
        MortalLivingWorldReceipt.ValidateHistory(JsonNode.Parse(forged.HistoryJson)!.AsObject());
        var original = fixture.JointOriginal;
        try
        {
            await fixture.Files.WriteFileAtomicBytesAsync(fixture.Lease, MortalLivingWorldSource.HistoryPath, forged.HistoryBytes);
            using var fresh = fixture.Files.ForceFreshRecoveryValidation();
            await Assert.ThrowsAsync<InvalidDataException>(() => MortalQteDecisionPublication.ReadJointProgressionPhaseAsync(
                fixture.Files, fixture.Lease, original.SessionId, original.RequestId, original.SnapshotToken, original.Turn));
        }
        finally
        {
            await fixture.Files.WriteFileAtomicBytesAsync(fixture.Lease, MortalLivingWorldSource.HistoryPath, accepted);
        }
        var phase = await MortalQteDecisionPublication.ReadJointProgressionPhaseAsync(fixture.Files, fixture.Lease,
            original.SessionId, original.RequestId, original.SnapshotToken, original.Turn);
        Assert.Equal("unapplied", phase.Phase);
    }

    /// <summary>A short joint turn without an autonomous report follows the original owner and writes no history.</summary>
    [Fact]
    public async Task LivingWorldJoint_ShortNoReportStillPublishesWithoutAssessmentHistory()
    {
        await using var fixture = new Fixture(_output, nameof(LivingWorldJoint_ShortNoReportStillPublishesWithoutAssessmentHistory));
        await fixture.InitializeAsync();
        var bound = await fixture.PrepareBoundJointAsync();
        Assert.True(bound.Success, JsonSerializer.Serialize(bound.Issues));
        var common = await WoundAcceptedTurnPlanner.CreateJointRepairCommonPlanFromBoundForComponentAsync(
            fixture.Files, fixture.Lease, bound);
        Assert.True(common.Success, JsonSerializer.Serialize(common.Issues));
        Assert.False(common.Plan!.OwnerCompanionAfterImages.ContainsKey(MortalLivingWorldSource.HistoryPath));
        var published = await fixture.PublishLivingWorldJointCommonAsync();
        Assert.Equal(MortalJointRepairPublicationDisposition.Committed, published.Disposition);
        Assert.Null(await fixture.Files.ReadFileBytesAsync(fixture.Lease, MortalLivingWorldSource.HistoryPath));
        var original = fixture.JointOriginal;
        var phase = await MortalQteDecisionPublication.ReadJointProgressionPhaseAsync(fixture.Files, fixture.Lease,
            original.SessionId, original.RequestId, original.SnapshotToken, original.Turn);
        Assert.Equal("unapplied", phase.Phase);
    }

    private sealed partial class Fixture
    {
        private string? _livingWorldJointMode;
        private readonly List<string[]> _livingWorldCommittedCommonMembers = [];
        private bool _livingWorldMemberFaultArmed;
        private int _livingWorldMemberFaults;
        private bool _livingWorldPostCommitReceiptDriftArmed;
        private int _livingWorldPostCommitReceiptDrifts;
        internal int LivingWorldFaults => _livingWorldMemberFaults;
        internal int LivingWorldPostCommitDrifts => _livingWorldPostCommitReceiptDrifts;
        internal void ArmLivingWorldCommonMemberFault() => _livingWorldMemberFaultArmed = true;
        internal void ArmLivingWorldPostCommitReceiptDrift() => _livingWorldPostCommitReceiptDriftArmed = true;
        internal IReadOnlyList<string[]> LivingWorldCommittedCommonMembers => _livingWorldCommittedCommonMembers;
        internal int LivingWorldCommonPublicationCount => _livingWorldCommittedCommonMembers.Count;
        internal GameEngine.JointComponentOriginal JointOriginal => _jointOriginal ??
            throw new InvalidOperationException("Genuine joint original was not issued.");
        internal void ConfigureLivingWorldJoint(string mode)
        {
            if (mode is not ("missing" or "valid" or "wrongWindow" or "wrongEffect"))
                throw new ArgumentException("Unknown living-world test scenario.", nameof(mode));
            _livingWorldJointMode = mode;
        }

        private async Task AdvanceLivingWorldCycleBeforeOriginalAsync()
        {
            var clock = JsonNode.Parse((await Files.ReadFileAsync(Lease, MortalLivingWorldSource.WorldTimePath))!)!.AsObject();
            var schedule = JsonSerializer.Deserialize<ProgressionScheduleState>(
                (await Files.ReadFileAsync(Lease, ProgressionScheduleService.SchedulePath))!,
                SharedJsonOptions.PrettyCamelCaseUnsafeRelaxed)!;
            Assert.True(schedule.WorldCycleMinutes > 0);
            clock["currentTimeInMinutes"] = checked(clock["currentTimeInMinutes"]!.GetValue<int>() + schedule.WorldCycleMinutes);
            await Files.WriteFileAtomicAsync(Lease, MortalLivingWorldSource.WorldTimePath, clock.ToJsonString());
            _progression = await new ProgressionScheduleService(Files, NullLogger<ProgressionScheduleService>.Instance)
                .BuildControlForNextTurnAsync(Lease, "Mortal World");
            Assert.True(_progression.WorldCyclesAlreadyPendingBeforeTurn > 0);
        }

        private async Task AuthorLivingWorldRejectedOutputsAsync(GameEngine.JointComponentOriginal original)
        {
            var control = _progression ?? throw new InvalidOperationException("Progression control is required.");
            Assert.True(control.WorldCyclesAlreadyPendingBeforeTurn > 0);
            await Files.WriteFileAtomicAsync(Lease, ProgressionScheduleService.ReportPath, JsonSerializer.Serialize(new
            {
                progressionProcessingReport = new
                {
                    sessionId = original.SessionId, requestId = original.RequestId, turnNumber = original.Turn,
                    worldCyclesProcessed = control.WorldCyclesAlreadyPendingBeforeTurn,
                    factionCyclesProcessed = control.FactionCyclesAlreadyPendingBeforeTurn,
                    newLastWorldSimulationTimeInMinutes = checked(control.LastWorldSimulationTimeInMinutes +
                        control.WorldCyclesAlreadyPendingBeforeTurn * control.WorldCycleMinutes)
                }
            }, SharedJsonOptions.PrettyCamelCaseUnsafeRelaxed));
            var ready = JsonNode.Parse((await Files.ReadFileAsync(Lease, "ready/turn_complete.json"))!)!.AsObject();
            var modified = ready["filesModified"]!.AsArray();
            modified.Add(ProgressionScheduleService.ReportPath);
            if (_livingWorldJointMode != "missing")
            {
                var from = (long)control.CurrentWorldTimeInMinutes;
                if (control.WorldCyclesAlreadyPendingBeforeTurn > 0) from = Math.Min(from, control.LastWorldSimulationTimeInMinutes);
                if (control.FactionCyclesAlreadyPendingBeforeTurn > 0) from = Math.Min(from, control.LastFactionSimulationTimeInMinutes);
                var report = new JsonObject
                {
                    ["schemaVersion"] = 1, ["sessionId"] = original.SessionId, ["requestId"] = original.RequestId,
                    ["turn"] = original.Turn, ["fromMinute"] = from,
                    ["toMinute"] = control.CurrentWorldTimeInMinutes + (_livingWorldJointMode == "wrongWindow" ? 1 : 0),
                    ["resolution"] = _livingWorldJointMode == "wrongEffect" ? "changes" : "noSignificantChange",
                    ["audit"] = new JsonArray(new[] { "npcs", "factions", "locations", "projects", "world" }.Select(scope =>
                        (JsonNode)new JsonObject { ["scope"] = scope, ["reason"] = "Checked actual supported scope; no significant autonomous change." }).ToArray()),
                    ["events"] = _livingWorldJointMode == "wrongEffect" ? WrongEffectEvent(from, control.CurrentWorldTimeInMinutes) : new JsonArray()
                };
                await Files.WriteFileAtomicAsync(Lease, MortalLivingWorldSource.ReportPath, report.ToJsonString());
                modified.Add(MortalLivingWorldSource.ReportPath);
            }
            await Files.WriteFileAtomicAsync(Lease, "ready/turn_complete.json", ready.ToJsonString());
        }

        private static JsonArray WrongEffectEvent(long from, long to) => new(JsonNode.Parse($$"""
            {"eventId":"invented_flag","fromMinute":{{from}},"toMinute":{{to}},
             "actors":[{"kind":"system","id":"worldCycle"}],
             "targets":[{"kind":"worldFlag","id":"not_materialized"}],
             "cause":{"summary":"A flag was allegedly created","references":[{"kind":"system","id":"worldCycle"}]},
             "effects":[{"effectId":"create_flag","target":{"kind":"worldFlag","id":"not_materialized"},
               "operation":"create","field":"entity","before":{"present":false},
               "after":{"present":true,"value":{"flagId":"not_materialized","value":true}}}],
             "consequences":[{"summary":"An invented consequence","effectIds":["create_flag"]}],
             "knowledge":{"player":"hidden","newsReferences":[]}}
            """)!.AsObject());

        private void ObserveLivingWorldPublicationForFault(TrustedLocalPublicationPhase phase, int index)
        {
            if (_livingWorldJointMode is null || phase is not (TrustedLocalPublicationPhase.Committed or TrustedLocalPublicationPhase.MemberPublished)) return;
            var journal = Path.Combine(Files.RuntimeRootPath, "trusted-local-publication-v1", "active.json");
            using var document = JsonDocument.Parse(File.ReadAllBytes(journal));
            var members = document.RootElement.GetProperty("Members").EnumerateArray().Select(row =>
                Path.GetRelativePath(Files.GameSessionPath, row.GetProperty("Path").GetString()!).Replace('\\', '/')).ToArray();
            var common = members.Contains(MortalQteDecisionPublication.StatePath, StringComparer.Ordinal) &&
                members.Contains(MortalLivingWorldSource.HistoryPath, StringComparer.Ordinal);
            if (common && phase == TrustedLocalPublicationPhase.MemberPublished &&
                index >= 0 && index < members.Length && members[index] == MortalLivingWorldSource.HistoryPath &&
                _livingWorldMemberFaultArmed && _livingWorldMemberFaults == 0)
            {
                Assert.True(File.Exists(Files.ResolvePath(MortalLivingWorldSource.HistoryPath)));
                _livingWorldMemberFaults++;
                throw new InvalidDataException("Injected after actual joint living-world member publication.");
            }
            if (common && phase == TrustedLocalPublicationPhase.Committed)
            {
                _livingWorldCommittedCommonMembers.Add(members);
                if (_livingWorldPostCommitReceiptDriftArmed && _livingWorldPostCommitReceiptDrifts == 0)
                {
                    var history = Files.ResolvePath(MortalLivingWorldSource.HistoryPath);
                    Assert.True(File.Exists(history));
                    File.Delete(history);
                    Assert.False(File.Exists(history));
                    _livingWorldPostCommitReceiptDrifts++;
                }
            }
        }

        internal async Task<CanonicalStateNormalizer.JointRepairPublicationResult> PublishLivingWorldJointCommonAsync()
        {
            using var source = Files.BeginSourceDataOperationIfNeeded();
            using var recovery = Files.BeginRecoveryValidation(Lease);
            var mutationInFlight = false;
            try
            {
                var normalizer = new CanonicalStateNormalizer(Files, NullLogger<CanonicalStateNormalizer>.Instance).BindTo(Lease);
                var preflight = await normalizer.PrevalidateJointRepairPublicationTransactionAsync();
                Assert.NotNull(preflight);
                var manifest = JsonSerializer.Deserialize<LiveTurnPendingSnapshotManifest>(
                    (await Files.ReadFileAsync(Lease, LiveTurnPreparationService.PendingTurnSnapshotManifestPath))!,
                    LiveTurnPreparationService.ManifestJsonOptions)!;
                mutationInFlight = true;
                var outcome = await normalizer.NormalizeAndValidateJointRepairAsync(preflight, Runtime.ValidationService,
                    manifest.RollbackBackups);
                if (outcome.Disposition == MortalJointRepairPublicationDisposition.PublicationUncertain)
                {
                    _jointPublicationOutcomeUncertain = true;
                    _retained = true;
                    return outcome;
                }
                if (source is not null) await source.CompleteAsync();
                mutationInFlight = false;
                return outcome;
            }
            catch (CoordinatedStatePublicationUncertainException)
            {
                _jointPublicationOutcomeUncertain = true;
                _retained = true;
                throw;
            }
            catch (Exception failure) when (mutationInFlight)
            {
                _jointPublicationOutcomeUncertain = true;
                _retained = true;
                throw new CoordinatedStatePublicationUncertainException(failure);
            }
        }
    }
}
