using System.Text.Json;
using System.Text.Json.Nodes;
using BookOfEternityClient.Core;
using BookOfEternityClient.Services;
using Xunit;

namespace BookOfEternityClient.Tests;

public sealed partial class AfterlifeResourceCutoverTests
{
    /// <summary>A warmed v5 comparison refuses raw declaration-bearing input drift without source work; byte restoration cannot revive ownership.</summary>
    [Fact]
    public Task SpiritualGuardianAttribution_WarmRawInputDriftRevokesWithoutRecomputation() =>
        WithGuardianAttributionJourneyAsync("guardian_attribution_reuse_drift", async setup =>
        {
            var context = setup.Copy.Context;
            FileSystemManager.CanonicalWriteLease? lease = null;
            ValidationService.SpiritualOriginalTurnCapture? capture = null;
            IReadOnlyDictionary<string, byte[]>? files = null;
            IReadOnlyDictionary<string, byte[]>? disposalFiles = null;
            byte[]? generation = null;
            var original = setup.Authored[AfterlifeSpiritualConflictState.StatePath]!;
            var drifted = false;
            await ExecutePressureGroupFixtureStagesAsync(new(
                Work: async () =>
                {
                    lease = await context.FileSystem.AcquireCanonicalWriteLeaseAsync();
                    var recorded = await context.Validator.CaptureSpiritualOriginalTurnWithIntakeAsync(lease);
                    capture = recorded.Capture;
                    RequireGuardianJourneyIssues(setup.Name, "reuse_intake", recorded.Issues); Assert.NotNull(capture);
                    RequireGuardianJourneyIssues(setup.Name, "reuse_resource_begin", await capture.BeginResourceExecutionAsync(lease));
                    var prepared = await capture.PrepareOriginalResourcePrefixAsync(lease);
                    RequireGuardianJourneyIssues(setup.Name, "reuse_original_prefix", prepared.Issues);
                    Assert.True(prepared.Ready); Assert.False(prepared.PrefixBlocked);
                    files = CaptureExactSpiritualFiles(context.FileSystem.GameSessionPath);
                    generation = File.ReadAllBytes(context.FileSystem.SessionGenerationPath);
                    var frontier = await ReadPressureOwnerSnapshotAsync(capture, lease, setup.Name + "_before_drift");
                    await AssertGuardianAttributionRepeatedQueriesAsync(setup, capture, lease, frontier, files, generation);
                    var before = capture.ReadExistingStrainReuseCounts();
                    var sourceBefore = capture.ReadExistingPressureGroupComputationCounts();
                    var changed = original.Concat(new byte[] { 13, 10, 32 }).ToArray();
                    Assert.True(JsonNode.DeepEquals(ReadGuardianAttributionJson(original), ReadGuardianAttributionJson(changed)));
                    Assert.NotEqual(new CanonicalBeforeImage(true, original).Fingerprint, new CanonicalBeforeImage(true, changed).Fingerprint);
                    drifted = true; // Cleanup restores even if a mutation/assertion fails.
                    await context.FileSystem.WriteFileAtomicBytesAsync(lease, AfterlifeSpiritualConflictState.StatePath, changed);
                    var changedFiles = CaptureExactSpiritualFiles(context.FileSystem.GameSessionPath);
                    var refused = await MeasurePressureOwnerPhaseAsync("guardian_reuse_stale_raw_refusal", setup.Name,
                        () => capture.ReadExistingPressureOrdinaryComparisonAsync(lease));
                    var after = capture.ReadExistingStrainReuseCounts();
                    var sourceAfter = capture.ReadExistingPressureGroupComputationCounts();
                    _storageOutput.WriteLine(JsonSerializer.Serialize(new
                    {
                        phase = "guardian_reuse_raw_drift", setup.Name,
                        originalFingerprint = new CanonicalBeforeImage(true, original).Fingerprint,
                        changedFingerprint = new CanonicalBeforeImage(true, changed).Fingerprint,
                        issues = refused.Issues.Select(diagnosticIssue => new
                        {
                            diagnosticIssue.FilePath, diagnosticIssue.Severity, diagnosticIssue.Message, diagnosticIssue.Category,
                            diagnosticIssue.Code, diagnosticIssue.Actor, diagnosticIssue.Section, diagnosticIssue.Expected,
                            diagnosticIssue.Actual, diagnosticIssue.RepairHint, diagnosticIssue.RepairTargetFiles
                        }),
                        refused.PrefixBlocked,
                        comparison = refused.Comparison is not { } refusedDiagnosticComparison ? null : new
                        {
                            refusedDiagnosticComparison.OrdinaryExchangeJson, refusedDiagnosticComparison.OrdinaryCalculation,
                            refusedDiagnosticComparison.ExpectedTransitions, refusedDiagnosticComparison.RawInputFingerprint,
                            refusedDiagnosticComparison.ExchangePointer
                        },
                        before = new { before.Queries, before.SignedParentReads, before.OrdinaryEvaluations,
                            before.ActualEvaluations, before.FreshInputChecks, before.FreshDependencyChecks },
                        after = new { after.Queries, after.SignedParentReads, after.OrdinaryEvaluations,
                            after.ActualEvaluations, after.FreshInputChecks, after.FreshDependencyChecks },
                        sourceBefore, sourceAfter, currentOwner = capture.IsCurrentOwner
                    }));
                    var issue = Assert.Single(refused.Issues);
                    Assert.Equal(("spiritual_original_input_changed", AfterlifeSpiritualConflictState.StatePath, IssueSeverity.Error),
                        (issue.Code, issue.FilePath, issue.Severity));
                    Assert.Null(refused.Comparison); Assert.False(refused.PrefixBlocked); Assert.False(capture.IsCurrentOwner);
                    Assert.Equal(before.Queries, after.Queries);
                    Assert.Equal(before.SignedParentReads, after.SignedParentReads);
                    Assert.Equal(before.OrdinaryEvaluations, after.OrdinaryEvaluations);
                    Assert.Equal(before.ActualEvaluations, after.ActualEvaluations);
                    Assert.Equal(sourceBefore, sourceAfter);
                    AssertExistingPressureInputInert(context, lease, changedFiles, generation);
                    Assert.False(AcceptedMechanicsPlanAuthority.HasValidated(context.FileSystem, lease));
                    Assert.False(AcceptedMechanicsPlanAuthority.TryPeekSpiritualPublication(context.FileSystem, lease, out _));
                    AssertPressureNoPrivatePair(context, lease);
                    await context.FileSystem.WriteFileAtomicBytesAsync(lease, AfterlifeSpiritualConflictState.StatePath, original);
                    drifted = false;
                    AssertExistingPressureInputInert(context, lease, files, generation);
                    Assert.False(capture.IsCurrentOwner);
                    await Assert.ThrowsAsync<ObjectDisposedException>(() => capture.ReadExistingPressureOrdinaryComparisonAsync(lease));
                    await Assert.ThrowsAsync<ObjectDisposedException>(() => capture.CheckRetainedInputsAsync(lease));
                    Assert.Equal(after, capture.ReadExistingStrainReuseCounts());
                    Assert.Equal(sourceAfter, capture.ReadExistingPressureGroupComputationCounts());
                },
                HasWindow: () => files is not null, HasCapture: () => capture is not null,
                HasLease: () => lease is not null, HasContext: () => false,
                AssertWindowInert: async () =>
                {
                    if (drifted)
                        await context.FileSystem.WriteFileAtomicBytesAsync(lease!, AfterlifeSpiritualConflictState.StatePath, original);
                    AssertExistingPressureInputInert(context, lease!, files!, generation!);
                },
                CaptureDisposalFiles: () => disposalFiles = CaptureExactSpiritualFiles(context.FileSystem.GameSessionPath),
                DisposeCaptureExplicitly: () => capture!.Dispose(),
                AssertDisposedCaptureInert: () =>
                {
                    if (disposalFiles is not null) AssertExistingPressureInputInert(context, lease!, disposalFiles, generation!);
                },
                DisposeCaptureImplicitly: () => capture!.Dispose(), AssertNoPrivatePair: () => AssertPressureNoPrivatePair(context, lease!),
                DisposeLease: async () => await lease!.DisposeAsync(), DisposeContext: () => Task.CompletedTask, AssertRootRemoved: () => { }));
        });

    /// <summary>Measures two real current-owner queries: only the first computes parents/catalogs/ordinary/resources; both keep fresh fences.</summary>
    private async Task<ValidationService.SpiritualOrdinaryPressureComparisonData> AssertGuardianAttributionRepeatedQueriesAsync(
        GuardianAttributionJourney setup, ValidationService.SpiritualOriginalTurnCapture capture,
        FileSystemManager.CanonicalWriteLease lease, ValidationService.SpiritualOrdinaryPressureProbeState frontier,
        IReadOnlyDictionary<string, byte[]> files, byte[] generation)
    {
        var before = capture.ReadExistingStrainReuseCounts();
        var sourceBefore = capture.ReadExistingPressureGroupComputationCounts();
        // Every attempt owns a genuinely new operation, including a same-byte retry after rollback.
        Assert.Equal(0, before.Queries); Assert.Equal(0, before.SignedParentReads);
        Assert.Equal(0, before.OrdinaryEvaluations); Assert.Equal(0, before.ActualEvaluations);
        Assert.Equal(new ValidationService.SpiritualPressureBatchReaderCounts(0, 0, 0, 0, 0), sourceBefore.CurrentBatch);
        Assert.Equal(new ValidationService.SpiritualPressureBatchReaderCounts(0, 0, 0, 0, 0), sourceBefore.SignedBatch);
        Assert.Equal(0, sourceBefore.ResourceBatchComputations);
        var first = await Query("cold");
        var cold = capture.ReadExistingStrainReuseCounts();
        var sourceCold = capture.ReadExistingPressureGroupComputationCounts();
        Assert.Equal(before.Queries + 1, cold.Queries);
        Assert.Equal(before.SignedParentReads + 1, cold.SignedParentReads);
        Assert.Equal(before.OrdinaryEvaluations + 1, cold.OrdinaryEvaluations);
        Assert.Equal(before.ActualEvaluations, cold.ActualEvaluations);
        Assert.True(cold.FreshInputChecks > before.FreshInputChecks);
        Assert.True(cold.FreshDependencyChecks > before.FreshDependencyChecks);
        Assert.Equal(NextBatch(sourceBefore.CurrentBatch), sourceCold.CurrentBatch);
        Assert.Equal(NextBatch(sourceBefore.SignedBatch), sourceCold.SignedBatch);
        Assert.Equal(sourceBefore.ResourceBatchComputations + 1, sourceCold.ResourceBatchComputations);
        // Mutating a returned detached view cannot poison the reused current-owner DATA.
        var detached = first.ReadOrdinaryExchange();
        if (setup.IsGuardian) detached["spiritualWoundFailedAction"]!["actorId"] = "unrelated_guardian";
        detached["after"]![setup.StrainField] = "broken";
        var repeated = await Query("warm");
        var warm = capture.ReadExistingStrainReuseCounts();
        var sourceWarm = capture.ReadExistingPressureGroupComputationCounts();
        _storageOutput.WriteLine(JsonSerializer.Serialize(new
        {
            phase = "guardian_real_owner_repeated_query", setup.Name,
            before = new { before.Queries, before.SignedParentReads, before.OrdinaryEvaluations,
                before.ActualEvaluations, before.FreshInputChecks, before.FreshDependencyChecks },
            cold = new { cold.Queries, cold.SignedParentReads, cold.OrdinaryEvaluations,
                cold.ActualEvaluations, cold.FreshInputChecks, cold.FreshDependencyChecks },
            warm = new { warm.Queries, warm.SignedParentReads, warm.OrdinaryEvaluations,
                warm.ActualEvaluations, warm.FreshInputChecks, warm.FreshDependencyChecks },
            sourceBefore, sourceCold, sourceWarm, currentOwner = capture.IsCurrentOwner,
            rawInputFingerprint = first.RawInputFingerprint
        }));
        Assert.Equal(cold.Queries + 1, warm.Queries);
        Assert.Equal(cold.SignedParentReads, warm.SignedParentReads);
        Assert.Equal(cold.OrdinaryEvaluations, warm.OrdinaryEvaluations);
        Assert.Equal(cold.ActualEvaluations, warm.ActualEvaluations);
        Assert.True(warm.FreshInputChecks > cold.FreshInputChecks);
        Assert.True(warm.FreshDependencyChecks > cold.FreshDependencyChecks);
        Assert.Equal(sourceCold, sourceWarm);
        Assert.Equal(first.OrdinaryExchangeJson, repeated.OrdinaryExchangeJson);
        Assert.Equal(first.OrdinaryCalculation, repeated.OrdinaryCalculation);
        Assert.Equal(first.ExpectedTransitions, repeated.ExpectedTransitions);
        Assert.Equal((first.RawInputFingerprint, first.ExchangePointer), (repeated.RawInputFingerprint, repeated.ExchangePointer));
        return repeated;

        async Task<ValidationService.SpiritualOrdinaryPressureComparisonData> Query(string phase)
        {
            var queried = await ExecutePressureGroupComparisonStagesAsync(
                () => MeasurePressureOwnerPhaseAsync("guardian_real_owner_" + phase + "_query", setup.Name,
                    () => capture.ReadExistingPressureOrdinaryComparisonAsync(lease)),
                () => capture.IsCurrentOwner,
                async () =>
                {
                    AssertPressureOrdinaryFrontiersEqual(frontier,
                        await ReadPressureOwnerSnapshotAsync(capture, lease, setup.Name + "_" + phase + "_close"));
                    AssertExistingPressureInputInert(setup.Copy.Context, lease, files, generation);
                    AssertPressureNoPrivatePair(setup.Copy.Context, lease);
                });
            RequireGuardianJourneyIssues(setup.Name, phase + "_ordinary_comparison", queried.Issues);
            Assert.False(queried.PrefixBlocked); Assert.True(capture.IsCurrentOwner);
            var comparison = Assert.IsType<ValidationService.SpiritualOrdinaryPressureComparisonData>(queried.Comparison);
            Assert.True(JsonNode.DeepEquals(setup.Ordinary, comparison.ReadOrdinaryExchange()));
            Assert.Equal(setup.OrdinaryCalculation, comparison.OrdinaryCalculation);
            Assert.Equal("/activeConflict/exchangeLog/0", comparison.ExchangePointer);
            Assert.Equal(new CanonicalBeforeImage(true, setup.Authored[AfterlifeSpiritualConflictState.StatePath]).Fingerprint,
                comparison.RawInputFingerprint);
            Assert.Equal(2, comparison.ExpectedTransitions.Count);
            foreach (var kind in new[] { ResourceOwnerKind.AfterlifeActor, ResourceOwnerKind.AfterlifeConflictSide })
            {
                var transition = Assert.Single(comparison.ExpectedTransitions, row => row.Coordinate.OwnerKind == kind);
                var side = kind == ResourceOwnerKind.AfterlifeActor ? "player" : "opposition";
                var ownerId = side == "player" ? "player_soul" : setup.Copy.Conflict["activeConflict"]!
                    [AfterlifeEntityProfileState.ResourceOwnerBindingsProperty]!["opposition"]!["resourceOwnerId"]!.GetValue<string>();
                Assert.Equal(new AfterlifeSpiritualConflictResourceOutcome.ExpectedTransition(
                    "turn_46:afterlife_conflict:" + setup.ExchangeId + ":" + side, "afterlife_cost", setup.ExchangeId,
                    new("chaos_sea", kind, ownerId, "spiritual_action_points"), ResourceTransitionOperation.Spend, 3m, 0m, 3m, 0m), transition);
            }
            return comparison;
        }
        static ValidationService.SpiritualPressureBatchReaderCounts NextBatch(ValidationService.SpiritualPressureBatchReaderCounts value) =>
            new(value.CatalogBuildAttempts + 1, value.IdentityParseAttempts + 1, value.HistoryParseAttempts + 1,
                value.HistoryAgreementAttempts + 1, value.ParentResolutionAttempts + 1);
    }
}
