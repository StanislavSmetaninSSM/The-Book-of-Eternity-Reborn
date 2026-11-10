using System.Text;
using System.Text.Json;
using System.Text.Json.Nodes;
using BookOfEternityClient.Core;
using BookOfEternityClient.Services;
using Xunit;

namespace BookOfEternityClient.Tests;

public sealed partial class AfterlifeResourceCutoverTests
{
    /// <summary>Refuses actual after a warm valid IV cap cannot hide changed current parent membership or History.</summary>
    /// <param name="history">Change valid History instead of current profile membership.</param>
    [Theory]
    [InlineData(false)] [InlineData(true)]
    public Task SpiritualGuardianCapped_WarmParentOrHistoryDriftRefusesActual(bool history) =>
        WithCappedControlAsync("capped_warm_drift_" + history, async w =>
        {
            var path = history ? WoundHistoryState.HistoryPath : AfterlifeEntityProfileState.StatePath;
            var bytes = Assert.IsType<byte[]>(await w.Context.FileSystem.ReadFileBytesAsync(w.Lease, path));
            var changed = ReadGuardianAttributionJson(bytes);
            if (history)
            {
                changed["transitions"]![0]!["afterFingerprint"] = "sha256:" + new string('f', 64);
                Assert.True(WoundHistoryState.Parse(changed.ToJsonString(), path).IsValid);
            }
            else ReadGuardianAttributionProfile(changed)["activeWounds"]!.AsArray().Clear();
            var before = ReadCappedControlCounts(w.Capture);
            await WithCappedEditedInputAsync(w, path, Encoding.UTF8.GetBytes(changed.ToJsonString()), async edited =>
            {
                var refused = await w.Capture.AdvanceNextResourceExchangeAsync(w.Lease);
                Assert.Null(refused.Step); AssertPressurePairInputDriftIssue(refused.Issues, path);
                AssertCappedNoComputations(w, before); await AssertCappedRevokedAsync(w, edited);
                LogCappedControl(w, "guardian_capped_warm_drift_refusal", before, refused.Issues, edited);
            });
        });

    /// <summary>A genuine admitted positive source still checks exact physical raw/request before completion.</summary>
    /// <param name="path">Exact ordinary conflict or original request path.</param>
    [Theory]
    [InlineData(AfterlifeSpiritualConflictState.StatePath)] [InlineData(LiveTurnPreparationService.TurnRequestPath)]
    public Task SpiritualGuardianCapped_AdmittedRawOrRequestDriftRevokes(string path) =>
        WithCappedControlAsync("capped_admitted_drift_" + Path.GetFileNameWithoutExtension(path), async w =>
        {
            await AdmitCappedControlAndC1Async(w);
            var before = ReadCappedControlCounts(w.Capture);
            var original = Assert.IsType<byte[]>(await w.Context.FileSystem.ReadFileBytesAsync(w.Lease, path));
            await WithCappedEditedInputAsync(w, path, [.. original, (byte)' '], async edited =>
            {
                var refused = await w.Capture.CompleteOrdinaryReductionAsync(w.Lease);
                Assert.False(refused.Success); Assert.Null(refused.Reduction); AssertPressurePairInputDriftIssue(refused.Issues, path);
                AssertCappedNoComputations(w, before); await AssertCappedRevokedAsync(w, edited);
                LogCappedControl(w, "guardian_capped_admitted_drift_refusal", before, refused.Issues, edited);
            });
        });

    /// <summary>Two independent warm valid IV owners reject each other's leases; disposing A cannot poison or displace B.</summary>
    [Fact]
    public Task SpiritualGuardianCapped_ForeignLeaseAndDisposedOwnerRefuse() =>
        WithCappedControlAsync("capped_foreign_a", async a =>
            await WithCappedControlAsync("capped_foreign_b", async b =>
            {
                Assert.NotEqual(a.Generation, b.Generation);
                var beforeA = ReadCappedControlCounts(a.Capture); var beforeB = ReadCappedControlCounts(b.Capture);
                var foreignA = await Assert.ThrowsAsync<InvalidOperationException>(() => a.Capture.ReadExistingPressureOrdinaryComparisonAsync(b.Lease));
                var foreignB = await Assert.ThrowsAsync<InvalidOperationException>(() => b.Capture.ReadExistingPressureOrdinaryComparisonAsync(a.Lease));
                Assert.Equal(beforeA, ReadCappedControlCounts(a.Capture)); Assert.Equal(beforeB, ReadCappedControlCounts(b.Capture));
                a.Capture.Dispose(); Assert.False(a.Capture.IsCurrentOwner);
                var disposed = await Assert.ThrowsAsync<ObjectDisposedException>(() => a.Capture.ReadExistingPressureOrdinaryComparisonAsync(a.Lease));
                AssertExistingPressureInputInert(a.Context, a.Lease, a.Files, a.Generation);
                var good = await b.Capture.ReadExistingPressureOrdinaryComparisonAsync(b.Lease);
                Assert.Empty(good.Issues); Assert.NotNull(good.Comparison); Assert.True(b.Capture.IsCurrentOwner);
                Assert.True(JsonNode.DeepEquals(b.Setup.Ordinary, good.Comparison.ReadOrdinaryExchange()));
                AssertCappedNoComputations(b, beforeB, queryDelta: 1);
                var afterB = ReadCappedControlCounts(b.Capture);
                Assert.True(afterB.FreshInputChecks > beforeB.FreshInputChecks); Assert.True(afterB.FreshDependencyChecks > beforeB.FreshDependencyChecks);
                AssertPressureOrdinaryFrontiersEqual(b.Frontier, await ReadPressureOwnerSnapshotAsync(b.Capture, b.Lease, b.Setup.Name + "_surviving_owner"));
                AssertExistingPressureInputInert(b.Context, b.Lease, b.Files, b.Generation);
                _storageOutput.WriteLine(JsonSerializer.Serialize(new { phase = "guardian_capped_foreign_disposed_boundary", beforeA, beforeB,
                    afterA = ReadCappedControlCounts(a.Capture), afterB, foreignAError = foreignA.ToString(), foreignBError = foreignB.ToString(),
                    disposedError = disposed.ToString(), ownerACurrent = a.Capture.IsCurrentOwner, ownerBCurrent = b.Capture.IsCurrentOwner,
                    filesA = CaptureExactSpiritualFiles(a.Context.FileSystem.GameSessionPath), filesB = CaptureExactSpiritualFiles(b.Context.FileSystem.GameSessionPath) }));
            }));

    /// <summary>An inactive once-valid same-session lease refuses before warm DATA or computation can escape.</summary>
    [Fact]
    public Task SpiritualGuardianCapped_DisposedLeaseRefusesWithoutComputation() =>
        WithCappedControlAsync("capped_disposed_lease", async w =>
        {
            Assert.True(w.Capture.IsCurrentOwner); Assert.True(w.Lease.IsActive);
            var before = ReadCappedControlCounts(w.Capture); await w.Lease.DisposeAsync(); Assert.False(w.Lease.IsActive);
            var error = await Assert.ThrowsAsync<InvalidOperationException>(() => w.Capture.ReadExistingPressureOrdinaryComparisonAsync(w.Lease));
            Assert.Equal("Canonical write lease is not active for this game session.", error.Message);
            Assert.Equal(before, ReadCappedControlCounts(w.Capture));
            await ExecuteGuardianLeaseStagesAsync(w.Context, lease =>
            {
                AssertExistingPressureInputInert(w.Context, lease, w.Files, w.Generation); AssertPressureNoPrivatePair(w.Context, lease);
                return Task.CompletedTask;
            });
            _storageOutput.WriteLine(JsonSerializer.Serialize(new { phase = "guardian_capped_disposed_lease_refusal", before,
                after = ReadCappedControlCounts(w.Capture), leaseActive = w.Lease.IsActive, error = error.ToString(),
                files = CaptureExactSpiritualFiles(w.Context.FileSystem.GameSessionPath), w.Generation }));
        });

    /// <summary>One independently signed IV cap and owned capture/lease per native row; no state crosses test boundaries.</summary>
    private Task WithCappedControlAsync(string name, Func<CappedControlWindow, Task> work) =>
        WithSingletonIvJourneyFailureAsync(name, () => WithGuardianAttributionJourneyAsync(name, async setup =>
        {
            _storageOutput.WriteLine(JsonSerializer.Serialize(new { phase = "guardian_capped_control_signed_setup_inputs", setup.Name,
                setup.ExpectedRank, setup.Retained, signedOriginalTracked = setup.Origin.Tracked, setup.Authored,
                ordinaryRoot = setup.Copy.Conflict, setup.OrdinaryCalculation, setup.ActualCalculation }));
            var context = setup.Copy.Context; FileSystemManager.CanonicalWriteLease? lease = null;
            ValidationService.SpiritualOriginalTurnCapture? capture = null; CappedControlWindow? window = null;
            IReadOnlyDictionary<string, byte[]>? disposalFiles = null; byte[]? disposalGeneration = null;
            await ExecutePressureGroupFixtureStagesAsync(new(
                Work: async () =>
                {
                    lease = await context.FileSystem.AcquireCanonicalWriteLeaseAsync();
                    var intake = await context.Validator.CaptureSpiritualOriginalTurnWithIntakeAsync(lease); capture = intake.Capture;
                    RequireGuardianJourneyIssues(name, "capped_control_intake", intake.Issues); Assert.NotNull(capture);
                    RequireGuardianJourneyIssues(name, "capped_control_resource_begin", await capture.BeginResourceExecutionAsync(lease));
                    var prefix = await capture.PrepareOriginalResourcePrefixAsync(lease);
                    RequireGuardianJourneyIssues(name, "capped_control_prefix", prefix.Issues); Assert.True(prefix.Ready); Assert.False(prefix.PrefixBlocked);
                    var files = CaptureExactSpiritualFiles(context.FileSystem.GameSessionPath); var generation = File.ReadAllBytes(context.FileSystem.SessionGenerationPath);
                    var frontier = await ReadPressureOwnerSnapshotAsync(capture, lease, name + "_capped_control_before");
                    Assert.True(capture.IsCurrentOwner); Assert.Equal(0, frontier.NextOrdinal); Assert.Empty(capture.ReadClosedExchangeEvidence(lease));
                    window = new(setup, context, lease, capture, files, generation, frontier);
                    _storageOutput.WriteLine(JsonSerializer.Serialize(new { phase = "guardian_capped_control_current_before_warm", name,
                        frontier, files, generation, counters = ReadCappedControlCounts(capture), currentOwner = capture.IsCurrentOwner }));
                    await WarmCappedControlAsync(window); await work(window);
                },
                HasWindow: () => window is not null, HasCapture: () => capture is not null, HasLease: () => lease is not null, HasContext: () => false,
                AssertWindowInert: async () =>
                {
                    if (lease!.IsActive) { AssertExistingPressureInputInert(context, lease, window!.Files, window.Generation); AssertPressureNoPrivatePair(context, lease); }
                    else await ExecuteGuardianLeaseStagesAsync(context, current =>
                    { AssertExistingPressureInputInert(context, current, window!.Files, window.Generation); AssertPressureNoPrivatePair(context, current); return Task.CompletedTask; });
                },
                CaptureDisposalFiles: () => { disposalFiles = CaptureExactSpiritualFiles(context.FileSystem.GameSessionPath); disposalGeneration = File.ReadAllBytes(context.FileSystem.SessionGenerationPath); },
                DisposeCaptureExplicitly: () => capture!.Dispose(),
                AssertDisposedCaptureInert: () =>
                {
                    Assert.False(capture!.IsCurrentOwner);
                    if (disposalFiles is not null && disposalGeneration is not null) AssertCappedPhysicalSnapshot(context, disposalFiles, disposalGeneration);
                },
                DisposeCaptureImplicitly: () => capture!.Dispose(),
                AssertNoPrivatePair: () => { if (lease!.IsActive) AssertPressureNoPrivatePair(context, lease); },
                DisposeLease: async () => await lease!.DisposeAsync(), DisposeContext: () => Task.CompletedTask, AssertRootRemoved: () => { }));
        }, retainedRank: 4, contour: GuardianSingletonPressureContour.CappedBroken));

    /// <summary>Cold then warm valid ordinary; the returned detached strain is genuinely changed to clear, then fresh DATA remains broken.</summary>
    private async Task WarmCappedControlAsync(CappedControlWindow w)
    {
        var before = ReadCappedControlCounts(w.Capture); Assert.Equal((0, 0, 0, 0), (before.Queries, before.SignedParentReads, before.OrdinaryEvaluations, before.ActualEvaluations));
        var cold = await w.Capture.ReadExistingPressureOrdinaryComparisonAsync(w.Lease);
        RequireGuardianJourneyIssues(w.Setup.Name, "capped_control_cold_ordinary", cold.Issues);
        var data = Assert.IsType<ValidationService.SpiritualOrdinaryPressureComparisonData>(cold.Comparison);
        Assert.Equal(CappedControlCalculation(), data.OrdinaryCalculation); Assert.True(JsonNode.DeepEquals(w.Setup.Ordinary, data.ReadOrdinaryExchange()));
        Assert.Equal(new CanonicalBeforeImage(true, w.Setup.Authored[AfterlifeSpiritualConflictState.StatePath]).Fingerprint, data.RawInputFingerprint);
        Assert.Equal("/activeConflict/exchangeLog/0", data.ExchangePointer);
        var first = ReadCappedControlCounts(w.Capture); Assert.Equal((1, 1, 1, 0), (first.Queries, first.SignedParentReads, first.OrdinaryEvaluations, first.ActualEvaluations));
        Assert.Equal(new ValidationService.SpiritualPressureBatchReaderCounts(1, 1, 1, 1, 1), first.Sources.CurrentBatch);
        Assert.Equal(first.Sources.CurrentBatch, first.Sources.SignedBatch); Assert.Equal(1, first.Sources.ResourceBatchComputations);
        var detached = data.ReadOrdinaryExchange(); detached["after"]!["oppositionSideStrain"] = "clear";
        Assert.Equal("clear", detached["after"]!["oppositionSideStrain"]!.GetValue<string>());
        var warm = await w.Capture.ReadExistingPressureOrdinaryComparisonAsync(w.Lease);
        RequireGuardianJourneyIssues(w.Setup.Name, "capped_control_warm_ordinary", warm.Issues);
        Assert.Equal(CappedControlCalculation(), warm.Comparison!.OrdinaryCalculation);
        Assert.True(JsonNode.DeepEquals(w.Setup.Ordinary, warm.Comparison.ReadOrdinaryExchange()));
        Assert.Equal("broken", warm.Comparison.ReadOrdinaryExchange()["after"]!["oppositionSideStrain"]!.GetValue<string>());
        AssertCappedNoComputations(w, first, queryDelta: 1); var after = ReadCappedControlCounts(w.Capture);
        Assert.True(after.FreshInputChecks > first.FreshInputChecks); Assert.True(after.FreshDependencyChecks > first.FreshDependencyChecks);
        AssertPressureOrdinaryFrontiersEqual(w.Frontier, await ReadPressureOwnerSnapshotAsync(w.Capture, w.Lease, w.Setup.Name + "_warm_close"));
        AssertExistingPressureInputInert(w.Context, w.Lease, w.Files, w.Generation); AssertPressureNoPrivatePair(w.Context, w.Lease);
        _storageOutput.WriteLine(JsonSerializer.Serialize(new { phase = "guardian_capped_control_warm_baseline", w.Setup.Name,
            before, cold = first, after, w.Frontier, w.Files, w.Generation, ordinary = w.Setup.Ordinary,
            fullOrdinaryRoot = w.Setup.Copy.Conflict, rawOrdinaryBytes = w.Setup.Authored[AfterlifeSpiritualConflictState.StatePath], detached }));
    }

    /// <summary>Real actual admission/C1, still no C2, before an admitted-input cut.</summary>
    private async Task AdmitCappedControlAndC1Async(CappedControlWindow w)
    {
        var before = ReadCappedControlCounts(w.Capture);
        var advanced = await w.Capture.AdvanceNextResourceExchangeAsync(w.Lease);
        RequireGuardianJourneyIssues(w.Setup.Name, "capped_control_actual", advanced.Issues);
        var interval = Assert.IsType<AcceptedMechanicsPlanner.SpiritualExchangeInterval>(advanced.Step?.Interval);
        Assert.Equal(0, interval.Ordinal); Assert.Equal(2, interval.AppliedTransitions.Count); Assert.Empty(interval.ReplayTransitions);
        foreach (var spend in interval.AppliedTransitions)
        {
            Assert.Equal(ResourceTransitionOperation.Spend, spend.Operation); Assert.Equal((3m, 3m, 3m, 0m),
                (spend.RequestedAmount, spend.AppliedAmount, spend.BeforeState!.Current, spend.AfterState!.Current));
        }
        var afterActual = ReadCappedControlCounts(w.Capture);
        _storageOutput.WriteLine(JsonSerializer.Serialize(new { phase = "guardian_capped_control_actual_counter_operands", before, afterActual }));
        Assert.Equal((before.Queries + 1, before.SignedParentReads, before.OrdinaryEvaluations, before.ActualEvaluations + 1),
            (afterActual.Queries, afterActual.SignedParentReads, afterActual.OrdinaryEvaluations, afterActual.ActualEvaluations));
        Assert.Equal(before.Sources with { ResourceBatchComputations = before.Sources.ResourceBatchComputations + 1 }, afterActual.Sources);
        var prefix = w.Capture.ReadClosedResourceCandidatePrefix(w.Lease, interval);
        AssertGuardianAttributionOnlyProjections(w.Setup, prefix.CompanionAfterImages[AfterlifeSpiritualConflictState.StatePath]);
        var ap = AfterlifeConflictActionPointProjectionService.Resolve(prefix.DefinitionsJson, prefix.StateJson, w.Setup.ActualRoot["activeConflict"]!.AsObject());
        RequireGuardianJourneyIssues(w.Setup.Name, "capped_control_ap", ap.Issues); Assert.Equal((0m, 0m), (ap.Projection!.Player.Current, ap.Projection.Opposition.Current));
        var source = Assert.Single(Assert.Single(w.Capture.ReadClosedExchangeEvidence(w.Lease)).Sources);
        Assert.Equal(CappedControlCalculation(), source.Calculation); Assert.NotNull(source.StrainPreimageJson);
        var admitted = await w.Capture.AdmitLatestClosedWoundSourceAsync(w.Lease, "opposition");
        RequireGuardianJourneyIssues(w.Setup.Name, "capped_control_admission", admitted.Issues);
        var admission = Assert.IsType<ValidationService.SpiritualOriginalTurnCapture.WoundSourceAdmission>(admitted.Admission);
        Assert.True(w.Capture.OwnsWoundAdmission(admission)); Assert.Same(interval, admission.Interval);
        Assert.Equal(source, admission.Source);
        var opportunity = await w.Capture.ReadWoundOpportunityAsync(w.Lease, admission);
        RequireGuardianJourneyIssues(w.Setup.Name, "capped_control_opportunity", opportunity.Issues); Assert.Null(opportunity.Satisfaction);
        var currentOpportunity = Assert.IsType<WoundOpportunityAuthority>(opportunity.Opportunity);
        Assert.Null(currentOpportunity.RequiredSeverityRank); Assert.Null(currentOpportunity.WorseningTarget);
        var offered = await w.Capture.ComposeC1FirstOfferAsync(w.Lease, interval);
        RequireGuardianJourneyIssues(w.Setup.Name, "capped_control_c1", offered.Issues);
        var pending = JsonNode.Parse(SpiritualWoundDecisionPendingState.SerializeCanonical(Assert.IsType<SpiritualWoundDecisionPendingState>(offered.Pending)))!["pending"]!.AsObject();
        var witness = Assert.Single(pending["sources"]!.AsArray())!.AsObject(); AssertGuardianAttributionWitness(w.Setup, witness);
        Assert.Equal(3, witness["maximumSeverityRank"]!.GetValue<int>()); Assert.Empty(pending["stagedDecisions"]!.AsArray());
        Assert.True(JsonNode.DeepEquals(w.Setup.Ordinary, w.Setup.Actual)); Assert.True(JsonNode.DeepEquals(w.Setup.Copy.Conflict, w.Setup.ActualRoot));
        AssertExistingPressureInputInert(w.Context, w.Lease, w.Files, w.Generation); AssertPressureNoPrivatePair(w.Context, w.Lease);
        _storageOutput.WriteLine(JsonSerializer.Serialize(new { phase = "guardian_capped_control_admitted_c1_baseline", before,
            after = ReadCappedControlCounts(w.Capture), interval = new { interval.Ordinal, interval.ConflictId, interval.ExchangeId, interval.AppliedTransitions, interval.ReplayTransitions, interval.Events }, pending,
            beforeFrontier = w.Frontier,
            afterFrontierUnavailable = "After actual/C1, the ordinal-zero probe rejects advancement and the positive-closed source probe excludes admitted pressure projections.",
            w.Files, w.Generation }));
    }

    /// <summary>Restores an owned deliberate edit only after refusal assertions, while preserving every primary/cleanup error.</summary>
    private async Task WithCappedEditedInputAsync(CappedControlWindow w, string path, byte[] changed,
        Func<IReadOnlyDictionary<string, byte[]>, Task> work)
    {
        var original = Assert.IsType<byte[]>(await w.Context.FileSystem.ReadFileBytesAsync(w.Lease, path));
        await ExecutePressureGroupFixtureStagesAsync(new(
            Work: async () => { Assert.False(original.AsSpan().SequenceEqual(changed)); await w.Context.FileSystem.WriteFileAtomicBytesAsync(w.Lease, path, changed);
                await work(CaptureExactSpiritualFiles(w.Context.FileSystem.GameSessionPath)); },
            HasWindow: () => true, HasCapture: () => false, HasLease: () => false, HasContext: () => false,
            AssertWindowInert: async () => { await w.Context.FileSystem.WriteFileAtomicBytesAsync(w.Lease, path, original); AssertExistingPressureInputInert(w.Context, w.Lease, w.Files, w.Generation); },
            CaptureDisposalFiles: () => { }, DisposeCaptureExplicitly: () => { }, AssertDisposedCaptureInert: () => { }, DisposeCaptureImplicitly: () => { },
            AssertNoPrivatePair: () => AssertPressureNoPrivatePair(w.Context, w.Lease), DisposeLease: () => ValueTask.CompletedTask,
            DisposeContext: () => Task.CompletedTask, AssertRootRemoved: () => { }));
    }

    /// <summary>Checks revoked owner and intentional physical edit without asking a retired capture for a new frontier.</summary>
    private static async Task AssertCappedRevokedAsync(CappedControlWindow w, IReadOnlyDictionary<string, byte[]> edited)
    {
        Assert.False(w.Capture.IsCurrentOwner); AssertExistingPressureInputInert(w.Context, w.Lease, edited, w.Generation); AssertPressureNoPrivatePair(w.Context, w.Lease);
        await Assert.ThrowsAsync<ObjectDisposedException>(() => w.Capture.CompleteOrdinaryReductionAsync(w.Lease));
        AssertExistingPressureInputInert(w.Context, w.Lease, edited, w.Generation);
    }

    /// <summary>Checks disposal bytes/generation even when the original lease is intentionally inactive.</summary>
    private static void AssertCappedPhysicalSnapshot(ResourceMaterializationTestContext context,
        IReadOnlyDictionary<string, byte[]> before, byte[] generation)
    {
        var after = CaptureExactSpiritualFiles(context.FileSystem.GameSessionPath);
        Assert.Equal(before.Keys.OrderBy(p => p, StringComparer.Ordinal), after.Keys.OrderBy(p => p, StringComparer.Ordinal));
        foreach (var (path, bytes) in before) Assert.Equal(bytes, after[path]);
        Assert.Equal(generation, File.ReadAllBytes(context.FileSystem.SessionGenerationPath));
    }

    /// <summary>Logs actual named counters and full physical snapshot; revoked owners expose no invented after-frontier.</summary>
    private void LogCappedControl(CappedControlWindow w, string phase, CappedControlCounts before,
        IReadOnlyList<ValidationIssue> issues, IReadOnlyDictionary<string, byte[]> edited) =>
        _storageOutput.WriteLine(JsonSerializer.Serialize(new { phase, w.Setup.Name, before, after = ReadCappedControlCounts(w.Capture), issues,
            beforeFrontier = w.Frontier, afterFrontierUnavailable = "revoked owner, deliberately not queried", editedFiles = edited,
            actualFiles = CaptureExactSpiritualFiles(w.Context.FileSystem.GameSessionPath), w.Generation, currentOwner = w.Capture.IsCurrentOwner }));

    /// <summary>Refusal cannot add source work; only an explicitly expected successful query changes its query count.</summary>
    private static void AssertCappedNoComputations(CappedControlWindow w, CappedControlCounts before, int queryDelta = 0)
    {
        var after = ReadCappedControlCounts(w.Capture);
        Assert.Equal((before.Queries + queryDelta, before.SignedParentReads, before.OrdinaryEvaluations, before.ActualEvaluations),
            (after.Queries, after.SignedParentReads, after.OrdinaryEvaluations, after.ActualEvaluations)); Assert.Equal(before.Sources, after.Sources);
    }

    /// <summary>Names every tuple operand so durable diagnostics never silently serialize counters as {}.</summary>
    private static CappedControlCounts ReadCappedControlCounts(ValidationService.SpiritualOriginalTurnCapture capture)
    {
        var c = capture.ReadExistingStrainReuseCounts(); return new(c.Queries, c.SignedParentReads, c.OrdinaryEvaluations,
            c.ActualEvaluations, c.FreshInputChecks, c.FreshDependencyChecks, capture.ReadExistingPressureGroupComputationCounts());
    }
    /// <summary>Independent literal positive capped arithmetic.</summary>
    private static SpiritualWoundCalculation CappedControlCalculation() => new(new(8, 0, 0, "strained", "broken", "hostile", 4), 1, 4, 2, 20, 3, 4, 4, true, 3);
    /// <summary>Per-case current owner and immutable prepared baseline; never shared across tests.</summary>
    private sealed record CappedControlWindow(GuardianAttributionJourney Setup, ResourceMaterializationTestContext Context,
        FileSystemManager.CanonicalWriteLease Lease, ValidationService.SpiritualOriginalTurnCapture Capture,
        IReadOnlyDictionary<string, byte[]> Files, byte[] Generation, ValidationService.SpiritualOrdinaryPressureProbeState Frontier);
    /// <summary>Serializable named diagnostic DATA, not a source of ownership.</summary>
    private sealed record CappedControlCounts(int Queries, int SignedParentReads, int OrdinaryEvaluations, int ActualEvaluations,
        int FreshInputChecks, int FreshDependencyChecks, ValidationService.SpiritualPressureGroupComputationCounts Sources);
}
