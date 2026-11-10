using System.Diagnostics;
using System.Text.Json;
using System.Text.Json.Nodes;
using BookOfEternityClient.Core;
using BookOfEternityClient.Services;
using Microsoft.Extensions.Logging.Abstractions;
using Xunit;

namespace BookOfEternityClient.Tests;

public sealed partial class AfterlifeResourceCutoverTests
{
    /// <summary>Requires raw ordinary freshness before completion's missing-decision check, even after actual admission.</summary>
    [Fact]
    public async Task SpiritualExistingPressure_AdmittedOrdinaryBytesDriftRevokesBeforeCompletion()
        => await WithPressureNegativeCopyAsync("admitted_input_drift", async copy =>
        {
            var context = copy.Context;
            await using var lease = await context.FileSystem.AcquireCanonicalWriteLeaseAsync();
            using var capture = await BeginPressureOrdinaryOwnerAsync(copy, lease, "admitted_input_drift");
            var before = CaptureExactSpiritualFiles(context.FileSystem.GameSessionPath);
            var generation = File.ReadAllBytes(context.FileSystem.SessionGenerationPath);
            var admissionStarted = Stopwatch.GetTimestamp();
            var advanced = await capture.AdvanceNextResourceExchangeAsync(lease);
            AssertNoConflictFrameErrors(advanced.Issues);
            Assert.IsType<AcceptedMechanicsPlanner.SpiritualExchangeInterval>(advanced.Step?.Interval);
            AssertPressureNegativeActualSource(copy, capture, lease);
            AssertExistingPressureInputInert(context, lease, before, generation);
            var counts = capture.ReadExistingStrainReuseCounts();
            Assert.Equal(1, counts.OrdinaryEvaluations);
            Assert.Equal(1, counts.ActualEvaluations);
            WritePressureOwnerTiming("actual_admission_before_input_drift", "negative", admissionStarted);
            var changed = await AppendPressureRawWhitespaceAsync(context, lease);
            var edited = CaptureExactSpiritualFiles(context.FileSystem.GameSessionPath);

            var refusalStarted = Stopwatch.GetTimestamp();
            var refused = await capture.CompleteOrdinaryReductionAsync(lease);

            Assert.False(refused.Success);
            Assert.Null(refused.Reduction);
            AssertPressureRawInputDriftIssue(refused.Issues);
            Assert.False(capture.IsCurrentOwner);
            var failedCounts = capture.ReadExistingStrainReuseCounts();
            Assert.Equal(counts.OrdinaryEvaluations, failedCounts.OrdinaryEvaluations);
            Assert.Equal(counts.ActualEvaluations, failedCounts.ActualEvaluations);
            Assert.Equal(changed, await context.FileSystem.ReadFileBytesAsync(lease, AfterlifeSpiritualConflictState.StatePath));
            AssertPressureNoPrivatePair(context, lease);
            AssertExistingPressureInputInert(context, lease, edited, generation);
            WritePressureOwnerTiming("admitted_input_drift_refusal", "negative", refusalStarted);
            Assert.Throws<ObjectDisposedException>(() => capture.ReadClosedExchangeEvidence(lease));
            capture.Dispose();
            await Assert.ThrowsAsync<ObjectDisposedException>(async () =>
                await capture.ReadExistingPressureOrdinaryComparisonAsync(lease));
            AssertExistingPressureInputInert(context, lease, edited, generation);
        });

    /// <summary>Requires fresh ordinary bytes before a completed cached C3 result can be returned again.</summary>
    [Fact]
    public async Task SpiritualExistingPressure_CachedCompletedReductionRejectsOrdinaryBytesDrift()
        => await WithPressureNegativeCopyAsync("cached_completion_drift", async copy =>
        {
            var context = copy.Context;
            await using var lease = await context.FileSystem.AcquireCanonicalWriteLeaseAsync();
            using var warm = await BeginPressureOrdinaryOwnerAsync(copy, lease, "cached_completion_drift");
            var coldStarted = Stopwatch.GetTimestamp();
            var advanced = await warm.AdvanceNextResourceExchangeAsync(lease);
            AssertNoConflictFrameErrors(advanced.Issues);
            var interval = Assert.IsType<AcceptedMechanicsPlanner.SpiritualExchangeInterval>(advanced.Step?.Interval);
            var actual = AssertPressureNegativeActualSource(copy, warm, lease);
            var committed = await warm.CommitC2FirstTransportAsync(lease, interval);
            var expectedCold = ReadExpectedSpiritualColdOfferData(warm, lease, committed, actual);
            warm.Dispose();
            var opened = await new ValidationService(context.FileSystem, NullLogger<ValidationService>.Instance)
                .OpenC2PrivateSessionAsync(lease);
            AssertNoConflictFrameErrors(opened.Issues);
            Assert.Equal("offer", opened.Disposition);
            using var choice = Assert.IsType<ValidationService.SpiritualC2PrivateSession>(opened.Session);
            AssertSpiritualColdOfferEvidence(expectedCold, await choice.ReadOfferReplayEvidenceAsync(lease));
            var declined = await choice.SubmitDecisionAsync(lease, JsonSerializer.SerializeToElement(new
                { opportunityRef = choice.Offer!.OpportunityRef, decision = "none" }), null);
            AssertNoConflictFrameErrors(declined.Issues);
            Assert.Equal("completed_unpublished", declined.Disposition);
            using var completed = Assert.IsType<ValidationService.SpiritualC2PrivateSession>(declined.Session);
            WritePressureOwnerTiming("cached_negative_actual_cold_none", "negative", coldStarted);
            var files = CaptureExactSpiritualFiles(context.FileSystem.GameSessionPath);
            var generation = File.ReadAllBytes(context.FileSystem.SessionGenerationPath);
            var reductionStarted = Stopwatch.GetTimestamp();
            var first = await completed.ReduceCompletedDeclinesAsync(lease);
            AssertNoConflictFrameErrors(first.Issues);
            Assert.True(first.Success);
            var firstData = ReadPressureCompletedReductionData(first);
            var second = await completed.ReduceCompletedDeclinesAsync(lease);
            AssertNoConflictFrameErrors(second.Issues);
            Assert.True(second.Success);
            Assert.Equal(firstData, ReadPressureCompletedReductionData(second));
            AssertExistingPressureInputInert(context, lease, files, generation);
            WritePressureOwnerTiming("cached_negative_two_unchanged_c3_reads", "negative", reductionStarted);
            var changed = await AppendPressureRawWhitespaceAsync(context, lease);
            var edited = CaptureExactSpiritualFiles(context.FileSystem.GameSessionPath);

            var refusalStarted = Stopwatch.GetTimestamp();
            var refused = await completed.ReduceCompletedDeclinesAsync(lease);

            Assert.False(refused.Success);
            Assert.Null(refused.Reduction);
            Assert.Null(refused.ReceiptAfterImage);
            Assert.Null(refused.ReceiptProof);
            AssertPressureRawInputDriftIssue(refused.Issues);
            Assert.Equal(changed, await context.FileSystem.ReadFileBytesAsync(lease, AfterlifeSpiritualConflictState.StatePath));
            AssertExistingPressureInputInert(context, lease, edited, generation);
            WritePressureOwnerTiming("cached_completion_input_drift_refusal", "negative", refusalStarted);
            var unusable = await completed.ReduceCompletedDeclinesAsync(lease);
            Assert.False(unusable.Success);
            Assert.Null(unusable.Reduction);
            Assert.Null(unusable.ReceiptAfterImage);
            var unavailable = Assert.Single(unusable.Issues);
            Assert.Equal("spiritual_c2_next_source_read_failed", unavailable.Code);
            Assert.Equal(SpiritualWoundCaptureCheckpointState.StatePath, unavailable.FilePath);
            Assert.Equal(IssueSeverity.Error, unavailable.Severity);
            AssertExistingPressureInputInert(context, lease, edited, generation);
            completed.Dispose();
            await Assert.ThrowsAsync<ObjectDisposedException>(async () =>
                await completed.ReadOfferReplayEvidenceAsync(lease));
            AssertExistingPressureInputInert(context, lease, edited, generation);
        });

    /// <summary>Allows ordinary proof but refuses an authentic original conflict-carrier occurrence before actual evaluation.</summary>
    [Fact]
    public async Task SpiritualExistingPressure_ConflictEffectCarrierRefusesBeforeActualAdmission()
        => await WithSignedPressureParentBelowCapAsync([4, 12], "request_pressure_collision_44", async context =>
        {
            var mechanics = await EffectMechanicsSnapshot.LoadAsync(context.FileSystem);
            Assert.True(mechanics.IsAccepted, string.Join("\n", mechanics.Issues));
            Assert.Contains(mechanics.Components, row => row.EffectId == "c4_retained_guard_ward" &&
                row.Profile == "afterlife_combat_condition");
            var conflict = (await context.ReadJsonAsync(AfterlifeSpiritualConflictState.StatePath))!.AsObject();
            var ordinary = await AppendExistingPressureOrdinaryDraftAsync(context, conflict);
            var definitions = (await context.ReadJsonAsync(ResourceMaterializationContract.DefinitionsPath))!.ToJsonString();
            await using var lease = await context.FileSystem.AcquireCanonicalWriteLeaseAsync();
            var copy = new PressureOrdinaryOwnerCopy(context,
                Assert.IsType<string>(context.FileSystem.ReadExistingSessionGeneration(lease)), ordinary, conflict, definitions);
            using var capture = await BeginPressureOrdinaryOwnerAsync(copy, lease, "effect_carrier_collision");
            var files = CaptureExactSpiritualFiles(context.FileSystem.GameSessionPath);
            var generation = File.ReadAllBytes(context.FileSystem.SessionGenerationPath);
            var frontier = await ReadPressureOwnerSnapshotAsync(capture, lease, "effect_carrier_before_comparison");
            AssertPressureOrdinaryFrontier(frontier, copy, capture, lease);
            var ordinaryResult = await MeasurePressureOwnerPhaseAsync("ordinary_before_collision", "negative",
                () => capture.ReadExistingPressureOrdinaryComparisonAsync(lease));
            AssertNoConflictFrameErrors(ordinaryResult.Issues);
            Assert.False(ordinaryResult.PrefixBlocked);
            AssertPressureOrdinaryComparison(Assert.IsType<ValidationService.SpiritualOrdinaryPressureComparisonData>(
                ordinaryResult.Comparison), copy, files);
            var counts = capture.ReadExistingStrainReuseCounts();
            Assert.Equal(1, counts.OrdinaryEvaluations);
            Assert.Equal(0, counts.ActualEvaluations);
            AssertPressureOrdinaryFrontiersEqual(frontier,
                await ReadPressureOwnerSnapshotAsync(capture, lease, "effect_carrier_after_comparison"));
            AssertExistingPressureInputInert(context, lease, files, generation);

            var refused = await MeasurePressureOwnerPhaseAsync("actual_collision_refusal", "negative",
                () => capture.AdvanceNextResourceExchangeAsync(lease));

            Assert.Null(refused.Step);
            var issue = Assert.Single(refused.Issues);
            Assert.Equal("spiritual_pressure_effect_carrier_collision", issue.Code);
            Assert.Equal(AfterlifeSpiritualConflictState.StatePath, issue.FilePath);
            Assert.Equal(IssueSeverity.Error, issue.Severity);
            Assert.False(capture.IsCurrentOwner);
            var failedCounts = capture.ReadExistingStrainReuseCounts();
            Assert.Equal(counts.OrdinaryEvaluations, failedCounts.OrdinaryEvaluations);
            Assert.Equal(0, failedCounts.ActualEvaluations);
            AssertExistingPressureInputInert(context, lease, files, generation);
            AssertPressureNoPrivatePair(context, lease);
            capture.Dispose();
            await Assert.ThrowsAsync<ObjectDisposedException>(async () =>
                await capture.ReadExistingPressureOrdinaryComparisonAsync(lease));
            AssertExistingPressureInputInert(context, lease, files, generation);
        }, beforeFinalSignatureAsync: SeedPressureCollisionBeforeSignatureAsync);

    /// <summary>Uses one immutable genuine signed template per Fact, never shared mutable owners or another Fact.</summary>
    private async Task WithPressureNegativeCopyAsync(string label, Func<PressureOrdinaryOwnerCopy, Task> work,
        FileSystemManagerHooks? hooks = null,
        Func<ResourceMaterializationTestContext, Task>? beforeOrdinaryAuthorAsync = null)
    {
        var prepared = await PreparePressureOrdinaryOwnerTemplateAsync();
        var copy = await MaterializePressureOrdinaryOwnerCopyAsync(prepared, label, hooks, beforeOrdinaryAuthorAsync);
        try { await work(copy); }
        finally
        {
            var cleanupStarted = Stopwatch.GetTimestamp();
            await copy.Context.DisposeAsync();
            WritePressureOwnerTiming("negative_owned_cleanup", label, cleanupStarted);
            Assert.False(Directory.Exists(copy.Context.RootPath));
        }
    }

    /// <summary>Checks actual III evidence from the owned interval while physical ordinary II remains untouched.</summary>
    private static ValidationService.PreparedSpiritualSource AssertPressureNegativeActualSource(
        PressureOrdinaryOwnerCopy copy, ValidationService.SpiritualOriginalTurnCapture capture,
        FileSystemManager.CanonicalWriteLease lease)
    {
        var closed = Assert.Single(capture.ReadClosedExchangeEvidence(lease));
        Assert.Equal((0, "exchange_existing_pressure_44"), (closed.Ordinal, closed.ExchangeId));
        Assert.Equal(new[] { 0, 1 }, closed.DiceClaims.Select(row => row.SourceIndex));
        Assert.Equal(new[] { 4, 12 }, closed.DiceClaims.Select(row => row.Value));
        var expected = copy.Ordinary.DeepClone().AsObject();
        expected["after"]!["playerSideStrain"] = "overwhelmed";
        Assert.True(JsonNode.DeepEquals(expected, JsonNode.Parse(closed.ExchangeJson)));
        var source = Assert.Single(closed.Sources);
        Assert.True(JsonNode.DeepEquals(expected, JsonNode.Parse(source.ExchangeJson)));
        Assert.Equal(("player", "pressure", "guardian:guardian_frame", "player_soul:player_soul"),
            (source.AffectedSide, source.Operation, source.ActingActor, source.AffectedActor));
        Assert.Equal((15L, 2), (source.Calculation.TraumaPressure, source.Calculation.MaximumSeverityRank));
        Assert.Equal(("strained", "overwhelmed"), (source.Calculation.Input.PreviousStrain, source.Calculation.Input.NewStrain));
        return source;
    }

    /// <summary>Authors only trailing raw whitespace in the ordinary input under its actual canonical lease.</summary>
    private static async Task<byte[]> AppendPressureRawWhitespaceAsync(ResourceMaterializationTestContext context,
        FileSystemManager.CanonicalWriteLease lease)
    {
        var bytes = (await context.FileSystem.ReadFileBytesAsync(lease, AfterlifeSpiritualConflictState.StatePath))!;
        byte[] changed = [.. bytes, (byte)' '];
        await context.FileSystem.WriteFileAtomicBytesAsync(lease, AfterlifeSpiritualConflictState.StatePath, changed);
        return changed;
    }

    /// <summary>Requires the dedicated full-path refusal, not the later missing-decision or generic stale-owner diagnosis.</summary>
    private static void AssertPressureRawInputDriftIssue(IReadOnlyList<ValidationIssue> issues)
    {
        var issue = Assert.Single(issues);
        Assert.Equal("spiritual_original_input_changed", issue.Code);
        Assert.Equal(AfterlifeSpiritualConflictState.StatePath, issue.FilePath);
        Assert.Equal(IssueSeverity.Error, issue.Severity);
    }

    /// <summary>Checks absence without changing owner state or providing a synthetic transport.</summary>
    private static void AssertPressureNoPrivatePair(ResourceMaterializationTestContext context,
        FileSystemManager.CanonicalWriteLease lease)
    {
        foreach (var path in new[] { SpiritualWoundCaptureCheckpointState.StatePath,
                     SpiritualWoundDecisionPendingState.StatePath, AcceptedMechanicsPlan.WoundCommandPath })
            Assert.False(context.FileSystem.FileExists(lease, path));
        Assert.False(AcceptedMechanicsPlanAuthority.HasValidated(context.FileSystem, lease));
    }

    /// <summary>Captures only immutable completed DATA so repeated C3 reads can be compared without extracting owners.</summary>
    private static string ReadPressureCompletedReductionData(ValidationService.SpiritualC3DecisionReductionResult result)
    {
        Assert.True(result.Success);
        var reduction = result.Reduction!;
        return JsonSerializer.Serialize(new { reduction.InputFingerprint,
            resourceState = reduction.Resources.StateAfterImage!.ToCanonicalJson(),
            resourceHistory = reduction.Resources.HistoryAfterImage!.ToCanonicalJson(),
            companions = reduction.OwnerCompanionAfterImages.OrderBy(pair => pair.Key, StringComparer.Ordinal)
                .Select(pair => new { path = pair.Key, json = pair.Value.ToJsonString() }).ToArray(),
            receipt = result.ReceiptAfterImage!.ToJsonString() });
    }

    /// <summary>Seeds the independent legal ward only after genuine guard publication and before the first signed44.</summary>
    private static async Task SeedPressureCollisionBeforeSignatureAsync(ResourceMaterializationTestContext context)
    {
        var before = (await context.ReadJsonAsync(AfterlifeSpiritualConflictState.StatePath))!.AsObject();
        var active = before["activeConflict"]!;
        Assert.Empty(active["combatConditions"]!.AsArray());
        Assert.Equal("strained", active["playerSideStrain"]!.GetValue<string>());
        var parent = WoundMaterializationContract.SerializeCanonical(await ReadCombatHealingWoundAsync(context));
        var preserved = new Dictionary<string, byte[]?>(StringComparer.Ordinal);
        foreach (var path in new[] { WoundIdentityState.StatePath, WoundHistoryState.HistoryPath,
                     SpiritualWoundOpportunityReceiptState.StatePath, ResourceMaterializationContract.StatePath,
                     ResourceMaterializationContract.HistoryPath, ValidationService.SpiritualWoundSourceSession.SoulPath })
            preserved[path] = await context.FileSystem.ReadFileBytesAsync(path);
        var identity = (await context.ReadJsonAsync(EffectIdentityState.StatePath))!.DeepClone();
        await SeedSpiritualC4ConditionAsync(context);
        Assert.Equal(parent, WoundMaterializationContract.SerializeCanonical(await ReadCombatHealingWoundAsync(context)));
        foreach (var (path, bytes) in preserved)
            Assert.Equal(bytes, await context.FileSystem.ReadFileBytesAsync(path));
        var after = (await context.ReadJsonAsync(AfterlifeSpiritualConflictState.StatePath))!.AsObject();
        Assert.Single(after["activeConflict"]!["combatConditions"]!.AsArray());
        var restored = after.DeepClone();
        restored["activeConflict"]!["combatConditions"] = before["activeConflict"]!["combatConditions"]!.DeepClone();
        Assert.True(JsonNode.DeepEquals(before, restored));
        var nextIdentity = (await context.ReadJsonAsync(EffectIdentityState.StatePath))!;
        Assert.Equal(identity["entries"]!.AsArray().Count + 1, nextIdentity["entries"]!.AsArray().Count);
        foreach (var entry in identity["entries"]!.AsArray())
            Assert.True(JsonNode.DeepEquals(entry, Assert.Single(nextIdentity["entries"]!.AsArray(), row =>
                row!["effectId"]!.GetValue<string>() == entry!["effectId"]!.GetValue<string>())));
        var mechanics = await EffectMechanicsSnapshot.LoadAsync(context.FileSystem);
        Assert.True(mechanics.IsAccepted, string.Join("\n", mechanics.Issues));
    }
}
