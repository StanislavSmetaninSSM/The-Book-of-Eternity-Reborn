using System.Text;
using System.Text.Json;
using System.Text.Json.Nodes;
using BookOfEternityClient.Core;
using BookOfEternityClient.Services;
using Xunit;

namespace BookOfEternityClient.Tests;

public sealed partial class AfterlifeResourceCutoverTests
{
    /// <summary>A warm ordinary DATA hit cannot hide current guardian membership or its original creation History drift.</summary>
    /// <param name="history">Change the immutable History anchor instead of removing the current parent.</param>
    [Theory]
    [InlineData(false)] [InlineData(true)]
    public Task SpiritualGuardianZero_WarmParentOrHistoryDriftRefusesActual(bool history) =>
        WithGuardianZeroPressureOwnerAsync("guardian_zero_warm_drift_" + history, SpiritualGuardianZeroPressureTestData.Create(3), async window =>
        {
            await AssertGuardianZeroWarmOrdinaryAsync(window);
            var counts = window.Capture.ReadExistingStrainReuseCounts(); var batches = window.Capture.ReadExistingPressureGroupComputationCounts();
            var path = history ? WoundHistoryState.HistoryPath : AfterlifeEntityProfileState.StatePath;
            var original = (await window.Context.FileSystem.ReadFileBytesAsync(window.Lease, path))!; var changed = JsonNode.Parse(original)!;
            if (history)
            {
                changed["transitions"]![0]!["afterFingerprint"] = "sha256:" + new string('f', 64);
                Assert.True(WoundHistoryState.Parse(changed.ToJsonString(), path).IsValid);
            }
            else ReadGuardianAttributionProfile(changed.AsObject())["activeWounds"]!.AsArray().Clear();
            await WithGuardianZeroEditedInputAsync(window, path, Encoding.UTF8.GetBytes(changed.ToJsonString()), async edited =>
            {
                var refused = await window.Capture.AdvanceNextResourceExchangeAsync(window.Lease);
                Assert.Null(refused.Step); AssertPressurePairInputDriftIssue(refused.Issues, path);
                AssertGuardianZeroNoNewComputation(window, counts, batches);
                await AssertGuardianZeroRevokedAndEditedAsync(window, edited);
            });
        });

    /// <summary>Actual maximum-zero admission still reauthenticates exact raw ordinary conflict and original request at completion.</summary>
    /// <param name="path">The original conflict or request physical bytes, not a detached comparator.</param>
    [Theory]
    [InlineData(AfterlifeSpiritualConflictState.StatePath)] [InlineData(LiveTurnPreparationService.TurnRequestPath)]
    public Task SpiritualGuardianZero_AdmittedRawOrRequestDriftRevokes(string path) =>
        WithGuardianZeroPressureOwnerAsync("guardian_zero_admitted_drift_" + Path.GetFileNameWithoutExtension(path),
            SpiritualGuardianZeroPressureTestData.Create(3), async window =>
            {
                await AssertGuardianZeroPressureActualAndC1Async(window);
                var counts = window.Capture.ReadExistingStrainReuseCounts(); var batches = window.Capture.ReadExistingPressureGroupComputationCounts();
                var original = (await window.Context.FileSystem.ReadFileBytesAsync(window.Lease, path))!;
                await WithGuardianZeroEditedInputAsync(window, path, [.. original, (byte)' '], async edited =>
                {
                    var refused = await window.Capture.CompleteOrdinaryReductionAsync(window.Lease);
                    Assert.False(refused.Success); Assert.Null(refused.Reduction); AssertPressurePairInputDriftIssue(refused.Issues, path);
                    AssertGuardianZeroNoNewComputation(window, counts, batches);
                    await AssertGuardianZeroRevokedAndEditedAsync(window, edited);
                });
            });

    /// <summary>Two independent valid owners refuse foreign leases; disposing one cannot revoke or poison the other owner's warm DATA.</summary>
    [Fact]
    public Task SpiritualGuardianZero_ForeignLeaseAndDisposedOwnerRefuse() =>
        WithGuardianZeroPressureOwnerAsync("guardian_zero_foreign_a", SpiritualGuardianZeroPressureTestData.Create(3), async a =>
            await WithGuardianZeroPressureOwnerAsync("guardian_zero_foreign_b", SpiritualGuardianZeroPressureTestData.Create(3), async b =>
            {
                Assert.NotEqual(a.Generation, b.Generation);
                await AssertGuardianZeroWarmOrdinaryAsync(a); await AssertGuardianZeroWarmOrdinaryAsync(b);
                var beforeA = a.Capture.ReadExistingStrainReuseCounts(); var beforeB = b.Capture.ReadExistingStrainReuseCounts();
                await Assert.ThrowsAsync<InvalidOperationException>(() => a.Capture.ReadExistingPressureOrdinaryComparisonAsync(b.Lease));
                await Assert.ThrowsAsync<InvalidOperationException>(() => b.Capture.ReadExistingPressureOrdinaryComparisonAsync(a.Lease));
                Assert.Equal(beforeA, a.Capture.ReadExistingStrainReuseCounts()); Assert.Equal(beforeB, b.Capture.ReadExistingStrainReuseCounts());
                a.Capture.Dispose(); Assert.False(a.Capture.IsCurrentOwner);
                await Assert.ThrowsAsync<ObjectDisposedException>(() => a.Capture.ReadExistingPressureOrdinaryComparisonAsync(a.Lease));
                AssertExistingPressureInputInert(a.Context, a.Lease, a.Files, a.Generation);
                var sourceBefore = b.Capture.ReadExistingPressureGroupComputationCounts();
                var warm = await b.Capture.ReadExistingPressureOrdinaryComparisonAsync(b.Lease);
                Assert.Empty(warm.Issues); Assert.NotNull(warm.Comparison);
                AssertGuardianZeroNoNewComputation(b, beforeB, sourceBefore);
                Assert.True(b.Capture.ReadExistingStrainReuseCounts().FreshInputChecks > beforeB.FreshInputChecks);
                Assert.True(b.Capture.ReadExistingStrainReuseCounts().FreshDependencyChecks > beforeB.FreshDependencyChecks);
                AssertExistingPressureInputInert(b.Context, b.Lease, b.Files, b.Generation);
            }));

    /// <summary>A once-valid same-filesystem lease cannot return warm DATA after disposal, before the capture itself is disposed.</summary>
    [Fact]
    public Task SpiritualGuardianZero_DisposedLeaseRefusesWithoutComputation() =>
        WithGuardianZeroPressureSetupAsync("guardian_zero_disposed_lease", SpiritualGuardianZeroPressureTestData.Create(3), async setup =>
        {
            var context = setup.Context; FileSystemManager.CanonicalWriteLease? lease = null;
            ValidationService.SpiritualOriginalTurnCapture? capture = null; GuardianZeroPressureWindow? window = null;
            await ExecuteGuardianZeroFixtureStagesAsync(setup.Name, "guardian_zero_disposed_lease_capture_cleanup", new(
                Work: async () =>
                {
                    lease = await context.FileSystem.AcquireCanonicalWriteLeaseAsync();
                    var intake = await context.Validator.CaptureSpiritualOriginalTurnWithIntakeAsync(lease); capture = intake.Capture;
                    RequireGuardianJourneyIssues(setup.Name, "disposed_lease_intake", intake.Issues); Assert.NotNull(capture);
                    RequireGuardianJourneyIssues(setup.Name, "disposed_lease_resource_begin", await capture!.BeginResourceExecutionAsync(lease));
                    var prefix = await capture.PrepareOriginalResourcePrefixAsync(lease);
                    RequireGuardianJourneyIssues(setup.Name, "disposed_lease_prefix", prefix.Issues); Assert.True(prefix.Ready); Assert.False(prefix.PrefixBlocked);
                    var frontier = await ReadPressureOwnerSnapshotAsync(capture, lease, setup.Name + "_before");
                    window = new(setup.Name, setup.Data, context, lease, capture, frontier, setup.OrdinaryRoot, setup.RawOrdinaryBytes,
                        CaptureExactSpiritualFiles(context.FileSystem.GameSessionPath), File.ReadAllBytes(context.FileSystem.SessionGenerationPath));
                    await AssertGuardianZeroWarmOrdinaryAsync(window); Assert.True(capture.IsCurrentOwner); Assert.True(lease.IsActive);
                    var before = capture.ReadExistingStrainReuseCounts(); var batches = capture.ReadExistingPressureGroupComputationCounts();
                    await lease.DisposeAsync(); Assert.False(lease.IsActive);
                    var refused = await Assert.ThrowsAsync<InvalidOperationException>(() => capture.ReadExistingPressureOrdinaryComparisonAsync(lease));
                    Assert.Equal("Canonical write lease is not active for this game session.", refused.Message);
                    AssertGuardianZeroNoNewComputation(window, before, batches);
                    _storageOutput.WriteLine(JsonSerializer.Serialize(new { phase = "guardian_zero_disposed_lease_refusal", setup.Name,
                        leaseActive = lease.IsActive, refused.Message, counters = ReadGuardianZeroPressureCounts(capture) }));
                },
                HasWindow: () => false, HasCapture: () => capture is not null, HasLease: () => lease is not null, HasContext: () => false,
                AssertWindowInert: () => Task.CompletedTask, CaptureDisposalFiles: () => { }, DisposeCaptureExplicitly: () => capture!.Dispose(),
                AssertDisposedCaptureInert: () => Assert.False(capture!.IsCurrentOwner), DisposeCaptureImplicitly: () => capture!.Dispose(),
                AssertNoPrivatePair: () => { }, DisposeLease: async () => await lease!.DisposeAsync(),
                DisposeContext: () => Task.CompletedTask, AssertRootRemoved: () => { }));
            await WithGuardianZeroControlLeaseAsync(context, setup.Name, "disposed_lease_physical_inertness", current =>
            {
                AssertExistingPressureInputInert(context, current, window!.Files, window.Generation);
                AssertPressureNoPrivatePair(context, current); return Task.CompletedTask;
            });
        });

    /// <summary>A genuinely registered completion remains current after capture/lease disposal and reacquisition, then raw drift alone blocks pre-take before writes.</summary>
    [Fact]
    public Task SpiritualGuardianZero_PreTakeDriftPreservesAuthoredInput()
    {
        var armed = false; var writes = new List<string>();
        var hooks = new FileSystemManagerHooks
        { BeforeCanonicalMutationAsync = path => { if (armed) writes.Add(path); return Task.CompletedTask; } };
        return WithGuardianZeroPressureSetupAsync("guardian_zero_pre_take", SpiritualGuardianZeroPressureTestData.Create(3), async setup =>
        {
            var context = setup.Context;
            var authority = await PrepareGuardianZeroPreTakeAuthorityAsync(setup);
            RequireGuardianJourneyIssues(setup.Name, "guardian_zero_pre_take_raw_resources",
                await context.Validator.ValidateAcceptedTurnRawResourceMaterializationAsync());
            var backups = await ReadSpiritualC4BackupsAsync(context);
            IReadOnlyDictionary<string, byte[]>? edited = null; byte[]? generation = null;
            await WithGuardianZeroControlLeaseAsync(context, setup.Name, "pre_take_current_before_drift", async lease =>
            {
                Assert.True(AcceptedMechanicsPlanAuthority.TryPeekSpiritualPublication(context.FileSystem, lease, out var current));
                Assert.Same(authority, current); Assert.True(authority.HasValidSeal());
                Assert.True(authority.CurrentInputsAgree(context.FileSystem, lease));
                Assert.Empty(await authority.ValidateCurrentInputsAsync(context.FileSystem, lease));
                Assert.True(AcceptedMechanicsPlanAuthority.HasValidated(context.FileSystem, lease));
                AssertGuardianZeroSourceOnlyReceipt(setup,
                    authority.Plan.OwnerCompanionAfterImages[SpiritualWoundOpportunityReceiptState.StatePath],
                    authority.Plan.OwnerCompanionAfterImages[AfterlifeSpiritualConflictState.StatePath]);
                _storageOutput.WriteLine(JsonSerializer.Serialize(new { phase = "guardian_zero_pre_take_current_after_reacquire_before_drift",
                    setup.Name, validSeal = true, currentInputsAgree = true, registeredExactAuthority = true }));
                var before = CaptureExactSpiritualFiles(context.FileSystem.GameSessionPath);
                generation = File.ReadAllBytes(context.FileSystem.SessionGenerationPath);
                await AppendPressureRawWhitespaceAsync(context, lease);
                edited = CaptureExactSpiritualFiles(context.FileSystem.GameSessionPath);
                Assert.Equal(before.Keys.OrderBy(p => p, StringComparer.Ordinal), edited.Keys.OrderBy(p => p, StringComparer.Ordinal));
                foreach (var (path, bytes) in before.Where(row => row.Key.Replace('\\', '/') != AfterlifeSpiritualConflictState.StatePath))
                    Assert.Equal(bytes, edited[path]);
                Assert.NotEqual(before[AfterlifeSpiritualConflictState.StatePath], edited[AfterlifeSpiritualConflictState.StatePath]);
            }, expectedPublication: authority);
            armed = true;
            try
            {
                var error = await Assert.ThrowsAsync<InvalidDataException>(() => AcceptedTurnCanonicalStateRefresh.NormalizeAndValidateWithPlanAsync(
                    context.FileSystem, context.Normalizer, context.Validator, backups));
                Assert.Contains("spiritual_c4_publication_input_changed", error.Message);
                Assert.Contains(AfterlifeSpiritualConflictState.StatePath, error.Message);
            }
            finally { armed = false; }
            Assert.Empty(writes);
            await WithGuardianZeroControlLeaseAsync(context, setup.Name, "pre_take_edited_state_preserved", lease =>
            {
                AssertExistingPressureInputInert(context, lease, edited!, generation!);
                Assert.False(AcceptedMechanicsPlanAuthority.TryPeekSpiritualPublication(context.FileSystem, lease, out _));
                Assert.False(AcceptedMechanicsPlanAuthority.TryTakeSpiritualPublication(context.FileSystem, lease, authority, out _));
                AssertPressureNoPrivatePair(context, lease);
                AssertExistingPressureInputInert(context, lease, edited!, generation!);
                _storageOutput.WriteLine(JsonSerializer.Serialize(new { phase = "guardian_zero_pre_take_refusal_preserves_edit",
                    setup.Name, committedMutationCount = writes.Count, editedBytesAndGenerationPreserved = true, privatePairAbsent = true }));
                return Task.CompletedTask;
            });
        }, hooks);
    }

    /// <summary>Queries a real cold/warm ordinary owner without advancing it, measuring reuse while all input/dependency checks remain fresh.</summary>
    /// <param name="window">Current capture, lease and independently signed guardian-zero originals.</param>
    private async Task AssertGuardianZeroWarmOrdinaryAsync(GuardianZeroPressureWindow window)
    {
        var before = window.Capture.ReadExistingStrainReuseCounts();
        var cold = await window.Capture.ReadExistingPressureOrdinaryComparisonAsync(window.Lease);
        Assert.Empty(cold.Issues); var comparison = Assert.IsType<ValidationService.SpiritualOrdinaryPressureComparisonData>(cold.Comparison);
        Assert.Equal(window.Data.OrdinaryCalculation, comparison.OrdinaryCalculation);
        Assert.True(JsonNode.DeepEquals(window.OrdinaryRoot["activeConflict"]!["exchangeLog"]![0], comparison.ReadOrdinaryExchange()));
        var first = window.Capture.ReadExistingStrainReuseCounts(); var batches = window.Capture.ReadExistingPressureGroupComputationCounts();
        Assert.Equal(before.OrdinaryEvaluations + 1, first.OrdinaryEvaluations); Assert.Equal(before.SignedParentReads + 1, first.SignedParentReads);
        Assert.Equal(before.ActualEvaluations, first.ActualEvaluations);
        var repeated = await window.Capture.ReadExistingPressureOrdinaryComparisonAsync(window.Lease);
        Assert.Empty(repeated.Issues); Assert.Equal(comparison.OrdinaryExchangeJson, repeated.Comparison!.OrdinaryExchangeJson);
        AssertGuardianZeroNoNewComputation(window, first, batches);
        Assert.True(window.Capture.ReadExistingStrainReuseCounts().FreshInputChecks > first.FreshInputChecks);
        Assert.True(window.Capture.ReadExistingStrainReuseCounts().FreshDependencyChecks > first.FreshDependencyChecks);
        _storageOutput.WriteLine(JsonSerializer.Serialize(new { phase = "guardian_zero_control_warm_ordinary", window.Name,
            counters = ReadGuardianZeroPressureCounts(window.Capture) }));
    }

    /// <summary>Separates computation counters from fresh checks; refusal must not recompute signed parents, ordinary DATA or actual work.</summary>
    /// <param name="window">Exact capture whose diagnostic counters remain readable after refusal.</param>
    /// <param name="before">Independent counter snapshot immediately before the affected call.</param>
    /// <param name="batches">Independent current/signed/resource computation snapshot before the affected call.</param>
    private static void AssertGuardianZeroNoNewComputation(GuardianZeroPressureWindow window,
        (int Queries, int SignedParentReads, int OrdinaryEvaluations, int ActualEvaluations, int FreshInputChecks, int FreshDependencyChecks) before,
        ValidationService.SpiritualPressureGroupComputationCounts batches)
    {
        var after = window.Capture.ReadExistingStrainReuseCounts();
        Assert.Equal((before.SignedParentReads, before.OrdinaryEvaluations, before.ActualEvaluations),
            (after.SignedParentReads, after.OrdinaryEvaluations, after.ActualEvaluations));
        Assert.Equal(batches, window.Capture.ReadExistingPressureGroupComputationCounts());
    }

    /// <summary>Verifies the actual revoked call leaves the intentional edit/generation intact and cannot continue to execute.</summary>
    /// <param name="window">Revoked capture with its still-active exact canonical lease.</param>
    /// <param name="edited">Complete physical file inventory after the deliberate external edit.</param>
    private static async Task AssertGuardianZeroRevokedAndEditedAsync(GuardianZeroPressureWindow window, IReadOnlyDictionary<string, byte[]> edited)
    {
        Assert.False(window.Capture.IsCurrentOwner); AssertExistingPressureInputInert(window.Context, window.Lease, edited, window.Generation);
        AssertPressureNoPrivatePair(window.Context, window.Lease);
        Assert.Throws<ObjectDisposedException>(() => window.Capture.ReadClosedExchangeEvidence(window.Lease));
        await Assert.ThrowsAsync<ObjectDisposedException>(() => window.Capture.CompleteOrdinaryReductionAsync(window.Lease));
        AssertExistingPressureInputInert(window.Context, window.Lease, edited, window.Generation);
    }

    /// <summary>Restores fixture-owned bytes only after the refusal checks; the original failure and every cleanup error remain observable.</summary>
    /// <param name="window">Independent current fixture and active lease for the deliberate edit.</param>
    /// <param name="path">Exact original input or retained-state path to drift.</param>
    /// <param name="changed">New physical bytes whose preservation the affected call must prove.</param>
    /// <param name="work">The actual refusal and edited-state assertions, completed before fixture restoration.</param>
    private async Task WithGuardianZeroEditedInputAsync(GuardianZeroPressureWindow window, string path, byte[] changed,
        Func<IReadOnlyDictionary<string, byte[]>, Task> work)
    {
        var original = (await window.Context.FileSystem.ReadFileBytesAsync(window.Lease, path))!;
        await ExecuteGuardianZeroFixtureStagesAsync(window.Name, "guardian_zero_edited_input_cleanup", new(
            Work: async () =>
            {
                Assert.False(original.AsSpan().SequenceEqual(changed));
                await window.Context.FileSystem.WriteFileAtomicBytesAsync(window.Lease, path, changed);
                var edited = CaptureExactSpiritualFiles(window.Context.FileSystem.GameSessionPath); await work(edited);
                _storageOutput.WriteLine(JsonSerializer.Serialize(new { phase = "guardian_zero_drift_refusal", window.Name, path,
                    editedStatePreserved = true, counters = ReadGuardianZeroPressureCounts(window.Capture) }));
            },
            HasWindow: () => true, HasCapture: () => false, HasLease: () => false, HasContext: () => false,
            AssertWindowInert: async () =>
            {
                await window.Context.FileSystem.WriteFileAtomicBytesAsync(window.Lease, path, original);
                AssertExistingPressureInputInert(window.Context, window.Lease, window.Files, window.Generation);
            },
            CaptureDisposalFiles: () => { }, DisposeCaptureExplicitly: () => { }, AssertDisposedCaptureInert: () => { },
            DisposeCaptureImplicitly: () => { }, AssertNoPrivatePair: () => AssertPressureNoPrivatePair(window.Context, window.Lease),
            DisposeLease: () => ValueTask.CompletedTask, DisposeContext: () => Task.CompletedTask, AssertRootRemoved: () => { }));
        Assert.False(window.Capture.IsCurrentOwner);
        await Assert.ThrowsAsync<ObjectDisposedException>(() => window.Capture.ReadExistingPressureOrdinaryComparisonAsync(window.Lease));
    }

    /// <summary>Owns a dedicated registered-completion lifetime; existing core/source-only wrappers keep their unregistered default assertions unchanged.</summary>
    /// <param name="setup">Independent signed originals and authored ordinary bytes for this pre-take control only.</param>
    private async Task<ValidationService.SpiritualOriginalTurnCapture.SpiritualC4PublicationAuthority> PrepareGuardianZeroPreTakeAuthorityAsync(
        GuardianZeroPressureSetup setup)
    {
        var context = setup.Context; FileSystemManager.CanonicalWriteLease? lease = null;
        ValidationService.SpiritualOriginalTurnCapture? capture = null; GuardianZeroPressureWindow? window = null;
        ValidationService.SpiritualOriginalTurnCapture.SpiritualC4PublicationAuthority? authority = null;
        ValidationService.SpiritualOriginalTurnCapture.SpiritualC4PublicationAuthority? registeredAuthority = null;
        await ExecuteGuardianZeroFixtureStagesAsync(setup.Name, "guardian_zero_pre_take_completion_cleanup", new(
            Work: async () =>
            {
                lease = await context.FileSystem.AcquireCanonicalWriteLeaseAsync();
                var intake = await context.Validator.CaptureSpiritualOriginalTurnWithIntakeAsync(lease); capture = intake.Capture;
                RequireGuardianJourneyIssues(setup.Name, "pre_take_intake", intake.Issues); Assert.NotNull(capture);
                RequireGuardianJourneyIssues(setup.Name, "pre_take_resource_begin", await capture!.BeginResourceExecutionAsync(lease));
                var prefix = await capture.PrepareOriginalResourcePrefixAsync(lease);
                RequireGuardianJourneyIssues(setup.Name, "pre_take_original_prefix", prefix.Issues); Assert.True(prefix.Ready); Assert.False(prefix.PrefixBlocked);
                var files = CaptureExactSpiritualFiles(context.FileSystem.GameSessionPath); var generation = File.ReadAllBytes(context.FileSystem.SessionGenerationPath);
                var frontier = await ReadPressureOwnerSnapshotAsync(capture, lease, setup.Name + "_before");
                window = new(setup.Name, setup.Data, context, lease, capture, frontier, setup.OrdinaryRoot, setup.RawOrdinaryBytes, files, generation);
                await AssertGuardianZeroPressureActualAndC1Async(window);
                var completed = await capture.CompleteOrdinaryReductionAsync(lease);
                RequireGuardianJourneyIssues(setup.Name, "pre_take_completion", completed.Issues); Assert.True(completed.Success);
                var reduction = Assert.IsType<AcceptedMechanicsPlanner.CompletedOrdinaryMechanicsReduction>(completed.Reduction);
                Assert.True(reduction.RequiresSatisfactionReceipt); Assert.False(reduction.HasLiveWoundWork); Assert.False(reduction.HasLiveDecisionReceipt);
                Assert.Empty(reduction.Resources.ReplayTransitions); Assert.Equal(2, reduction.Resources.AppliedTransitions.Count);
                Assert.True(JsonNode.DeepEquals(window.ActualRoot, reduction.OwnerCompanionAfterImages[AfterlifeSpiritualConflictState.StatePath]));
                var journal = capture.ReadAllocationJournal(lease).ToJsonString(); var cursor = capture.ReadAllocationCursor(lease);
                _storageOutput.WriteLine(JsonSerializer.Serialize(new { phase = "guardian_zero_pre_take_before_preparation", setup.Name,
                    counters = ReadGuardianZeroPressureCounts(capture), journal, cursor }));
                var prepared = await capture.PrepareC4PublicationAsync(lease);
                RequireGuardianJourneyIssues(setup.Name, "pre_take_preparation", prepared.Issues);
                authority = Assert.IsType<ValidationService.SpiritualOriginalTurnCapture.SpiritualC4PublicationAuthority>(prepared.Authority);
                AssertGuardianZeroSourceOnlyReceipt(setup, authority.Plan.OwnerCompanionAfterImages[SpiritualWoundOpportunityReceiptState.StatePath], window.ActualRoot);
                Assert.True(JsonNode.DeepEquals(window.ActualRoot, authority.Plan.OwnerCompanionAfterImages[AfterlifeSpiritualConflictState.StatePath]));
                Assert.Equal(journal, capture.ReadAllocationJournal(lease).ToJsonString()); Assert.Equal(cursor, capture.ReadAllocationCursor(lease));
                Assert.Empty(await authority.ValidateCurrentInputsAsync(context.FileSystem, lease));
                Assert.True(AcceptedMechanicsPlanAuthority.RegisterSpiritualPublication(context.FileSystem, lease, authority));
                registeredAuthority = authority;
                _storageOutput.WriteLine(JsonSerializer.Serialize(new { phase = "guardian_zero_pre_take_real_registered_completion", setup.Name,
                    journal, cursor, counters = ReadGuardianZeroPressureCounts(capture), registered = true }));
            },
            HasWindow: () => window is not null, HasCapture: () => capture is not null, HasLease: () => lease is not null, HasContext: () => false,
            AssertWindowInert: () =>
            {
                AssertPressureZeroCaptureInert(context, lease!, window!.Files, window.Generation, registeredAuthority);
                return Task.CompletedTask;
            },
            CaptureDisposalFiles: () => { }, DisposeCaptureExplicitly: () => capture!.Dispose(),
            AssertDisposedCaptureInert: () =>
            {
                Assert.False(capture!.IsCurrentOwner);
                if (window is not null) AssertPressureZeroCaptureInert(context, lease!, window.Files, window.Generation, registeredAuthority);
            },
            DisposeCaptureImplicitly: () => capture!.Dispose(),
            AssertNoPrivatePair: () => AssertPressureZeroCapturePublicationState(context, lease!, registeredAuthority),
            DisposeLease: async () => await lease!.DisposeAsync(), DisposeContext: () => Task.CompletedTask, AssertRootRemoved: () => { }));
        Assert.False(capture!.IsCurrentOwner); Assert.False(lease!.IsActive);
        return Assert.IsType<ValidationService.SpiritualOriginalTurnCapture.SpiritualC4PublicationAuthority>(authority);
    }

    /// <summary>Owns each pre-take reacquisition independently and preserves full primary/secondary cleanup diagnostics.</summary>
    /// <param name="context">The exact filesystem that owns the signed inputs and publication capability.</param>
    /// <param name="name">Independent fixture label for complete diagnostics.</param>
    /// <param name="phase">Named lease lifetime used only for diagnostics.</param>
    /// <param name="work">Current-input or post-refusal checks under the newly acquired lease.</param>
    /// <param name="expectedPublication">Only the exact successfully registered authority for the current-before-drift phase; null preserves the no-authority default.</param>
    private async Task WithGuardianZeroControlLeaseAsync(ResourceMaterializationTestContext context, string name, string phase,
        Func<FileSystemManager.CanonicalWriteLease, Task> work,
        ValidationService.SpiritualOriginalTurnCapture.SpiritualC4PublicationAuthority? expectedPublication = null)
    {
        FileSystemManager.CanonicalWriteLease? lease = null;
        await ExecuteGuardianZeroFixtureStagesAsync(name, "guardian_zero_control_lease_" + phase, new(
            Work: async () => { lease = await context.FileSystem.AcquireCanonicalWriteLeaseAsync(); await work(lease); },
            HasWindow: () => false, HasCapture: () => false, HasLease: () => lease is not null, HasContext: () => false,
            AssertWindowInert: () => Task.CompletedTask, CaptureDisposalFiles: () => { }, DisposeCaptureExplicitly: () => { },
            AssertDisposedCaptureInert: () => { }, DisposeCaptureImplicitly: () => { },
            AssertNoPrivatePair: () =>
            {
                if (expectedPublication is null) AssertPressureNoPrivatePair(context, lease!);
                else AssertPressureZeroCapturePublicationState(context, lease!, expectedPublication);
            },
            DisposeLease: async () => await lease!.DisposeAsync(), DisposeContext: () => Task.CompletedTask, AssertRootRemoved: () => { }));
        Assert.False(lease!.IsActive);
    }
}
