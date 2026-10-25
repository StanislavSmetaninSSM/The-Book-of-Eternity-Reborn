using System.Text.Json;
using BookOfEternityClient.Core;
using BookOfEternityClient.Services;
using Xunit;

namespace BookOfEternityClient.Tests;

public sealed partial class JointBundleComponentTests
{
    /// <summary>
    /// Requires the real publication take to reject raw source drift after reusable genuine proofs are warm.
    /// The drift preserves inventory agreement and both actual registry slots must remain available.
    /// </summary>
    [Fact]
    public async Task RecoveryTakeFence_RejectsRawSelectedOriginalDriftBeforeBothRealSlotTakes()
    {
        await using var fixture = new Fixture(_output, nameof(RecoveryTakeFence_RejectsRawSelectedOriginalDriftBeforeBothRealSlotTakes));
        await fixture.InitializeAsync();
        var bound = await fixture.PrepareBoundJointAsync(fixture.AssertWarmTakeRefusesRawSelectedOriginalDriftAsync, reuseStableData: true);
        Assert.True(bound.Success, JsonSerializer.Serialize(bound.Issues));
    }

    /// <summary>
    /// Exercises the actual engine dispatcher with fresh nested checks across an await and persistent raw drift.
    /// </summary>
    /// <param name="callbackThrows">
    /// Whether the callback throws its original failure after changing the source instead of returning normally.
    /// </param>
    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task RecoveryFinalizationDispatcher_KeepsFreshBoundaryAcrossAwaitAndFailure(bool callbackThrows)
    {
        await using var fixture = new Fixture(_output, nameof(RecoveryFinalizationDispatcher_KeepsFreshBoundaryAcrossAwaitAndFailure));
        await fixture.InitializeAsync();
        await fixture.RunInOriginalRecoveryLeaseFlowAsync(async () =>
        {
            const string path = "game_state/meta/soul_state.json";
            var physical = Path.Combine(fixture.Files.GameSessionPath, path.Replace('/', Path.DirectorySeparatorChar));
            var original = await File.ReadAllBytesAsync(physical);
            var changed = original.Concat(new[] { (byte)' ' }).ToArray();
            var rule = new OperationValidationRule("FinalizationDispatcher", 1);
            var identity = new object();
            var bodies = 0;
            using var scope = fixture.Files.BeginRecoveryValidation(fixture.Lease);
            Task Validate() => fixture.Files.ValidateRecoveryDataAsync(rule, identity, fixture.Lease, async () =>
            {
                bodies++;
                var bytes = await fixture.Files.ReadFileBytesAsync(fixture.Lease, path);
                if (bytes is null || !original.AsSpan().SequenceEqual(bytes))
                    throw new InvalidDataException("Finalization dispatcher source changed: " + path);
            });
            await Validate();
            await Validate();
            Assert.Equal(1, bodies);
            Assert.Equal(1, scope.CaptureState().Entries);
            var callbackFailure = new InvalidOperationException("The actual finalization callback failed.");
            var calls = 0;
            var checkpoint = SessionFinalizationCheckpoint.MortalJointCustodyCaptureCompletedBeforePreparation;
            fixture.Runtime.Engine.ConfigureSessionFinalizationHooksForTesting(new GameEngineSessionFinalizationHooks
            {
                AtCheckpointAsync = async actual =>
                {
                    calls++;
                    Assert.Equal(checkpoint, actual);
                    await Validate();
                    Assert.Equal(2, bodies);
                    await Task.Yield();
                    await Validate();
                    Assert.Equal(3, bodies);
                    await File.WriteAllBytesAsync(physical, changed);
                    if (callbackThrows) throw callbackFailure;
                }
            });
            var start = fixture.ResetCandidateCapture("RecoveryActualFinalizationDispatcher");
            try
            {
                var failure = await Record.ExceptionAsync(() =>
                    fixture.Runtime.Engine.InvokeSessionFinalizationCheckpointForTestingAsync(checkpoint));
                if (callbackThrows) Assert.Same(callbackFailure, failure);
                else Assert.Null(failure);
                Assert.Equal(1, calls);
                Assert.Equal(3, bodies);
                Assert.Equal(changed, await File.ReadAllBytesAsync(physical));
                var refusal = await Assert.ThrowsAsync<InvalidDataException>(Validate);
                Assert.Contains(path, refusal.Message, StringComparison.Ordinal);
                Assert.Equal(4, bodies);
                var counts = fixture.CompleteCandidateCapture("RecoveryActualFinalizationDispatcher", start);
                Assert.Equal(3, counts.GetValueOrDefault("RecoveryValidation.Source.FinalizationDispatcher"));
                Assert.Equal(0, counts.GetValueOrDefault("RecoveryValidation.Reuse.FinalizationDispatcher"));
                fixture.Output.WriteLine(JsonSerializer.Serialize(new { Kind = "RecoveryFinalizationFence", CallbackThrows = callbackThrows,
                    CallbackCalls = calls, Bodies = bodies, RawDriftRefusedAfterDispatcher = true, Counts = counts }));
            }
            finally
            {
                fixture.Runtime.Engine.ConfigureSessionFinalizationHooksForTesting(null);
                await File.WriteAllBytesAsync(physical, original);
            }
            Assert.Equal(original, await File.ReadAllBytesAsync(physical));
        });
    }

    private sealed partial class Fixture
    {
        /// <summary>
        /// Builds a genuinely sealed common candidate and proves fresh take checks precede both real slot removals.
        /// </summary>
        /// <returns>
        /// Completion after isolated selected-original refusal, exact byte restoration and unchanged slot checks.
        /// </returns>
        internal async Task AssertWarmTakeRefusesRawSelectedOriginalDriftAsync()
        {
            using var scope = Files.BeginRecoveryValidation(Lease);
            var bound = _jointBound ?? throw new InvalidOperationException("A genuine bound common input is required.");
            MortalJointRepairPublicationPreparation preparation;
            var preparationStart = ResetCandidateCapture("RecoveryTakeReadOnlyPreparation");
            // This independent read-only suffix uses the existing operation cache. Close its
            // fresh source fence before warming recovery facts or introducing raw drift below.
            using (var source = Files.BeginSourceDataOperationIfNeeded())
            {
                Assert.NotNull(source);
                AcceptedMechanicsPlanningResult common;
                using (Files.MeasureOperation("RecoveryTake.PrepareCommonPlan"))
                    common = await WoundAcceptedTurnPlanner.CreateJointRepairCommonPlanFromBoundForComponentAsync(Files, Lease, bound);
                Assert.True(common.Success, JsonSerializer.Serialize(common.Issues));
                var plan = Assert.IsType<AcceptedMechanicsPlan>(common.Plan);
                Assert.Same(bound.Authority, plan.JointRepairCommonAuthority);
                Assert.True(AcceptedMechanicsPlanAuthority.TryPeekValidated(Files, Lease, out var binding, out var selected));
                Assert.Equal(plan.PreparedPlanFingerprint, selected.Plan!.PreparedPlanFingerprint);
                using (Files.MeasureOperation("RecoveryTake.PreparePublication"))
                    preparation = await MortalJointRepairPublicationPreparation.CreateAsync(Files, Lease, plan, binding);
                await source.CompleteAsync();
            }
            var preparationCounts = CompleteCandidateCapture("RecoveryTakeReadOnlyPreparation", preparationStart);
            Assert.True(preparationCounts.GetValueOrDefault("OperationFileData.SourceSync") +
                preparationCounts.GetValueOrDefault("OperationFileData.SourceAsync") > 0);
            Assert.True(preparationCounts.GetValueOrDefault("OperationFileData.ReuseSync") +
                preparationCounts.GetValueOrDefault("OperationFileData.ReuseAsync") > 0);
            Assert.True(preparationCounts.GetValueOrDefault("OperationFileData.FreshSource") > 0);
            const string logicalPath = MortalLocationMaterializationContract.WorldMapPath;
            var manifest = JsonSerializer.Deserialize<LiveTurnPendingSnapshotManifest>(
                (await Files.ReadFileAsync(Lease, LiveTurnPreparationService.PendingTurnSnapshotManifestPath))!,
                LiveTurnPreparationService.ManifestJsonOptions);
            Assert.NotNull(manifest);
            Assert.True(manifest.Files.TryGetValue(logicalPath, out var path));
            Assert.False(string.IsNullOrWhiteSpace(path));
            Assert.Contains(logicalPath, AcceptedMechanicsWoundCommonInputComposer.TreatmentOrdinaryGuardPaths);
            Assert.DoesNotContain(logicalPath, MortalQteWoundOpportunityProjection.ReadSnapshot(Files, Lease).CoveredLogicalPaths);
            Assert.True(preparation.BeforeImages.ContainsKey(logicalPath));
            Assert.False(preparation.BeforeImages.ContainsKey(path));
            Assert.DoesNotContain(path, preparation.MemberPaths);
            Assert.DoesNotContain(path, manifest.RollbackBackups.Values);
            var physical = Files.ResolvePath(path);
            var original = await File.ReadAllBytesAsync(physical);
            var changed = original.Concat(new[] { (byte)' ' }).ToArray();
            AssertJointSlotsAvailable(preparation);
            // Candidate preparation is part of this same operation. Start the measured
            // two-request comparison with an empty fact set, preserving all earlier diagnostics.
            using (Files.ForceFreshRecoveryValidation()) { }
            var warmStart = ResetCandidateCapture("RecoveryActualTakeWarmFacts");
            await ValidateRetainedRecoveryBaselineAsync();
            await ValidateRetainedRecoveryBaselineAsync();
            var warm = CompleteCandidateCapture("RecoveryActualTakeWarmFacts", warmStart);
            foreach (var name in new[] { "Mechanical", "Continuation" })
            {
                Assert.Equal(1, warm.GetValueOrDefault("RecoveryValidation.Source." + name));
                Assert.Equal(1, warm.GetValueOrDefault("RecoveryValidation.Reuse." + name));
            }
            Assert.Equal(2, scope.CaptureState().Entries);
            AcceptedTurnAuthorityRegistry.JointRepairPublicationTakeResult? taken = null;
            Exception? failure = null;
            Dictionary<string, int>? counts = null;
            try
            {
                // Raw IO deliberately bypasses mutation epochs. Change only the separate selected snapshot,
                // not canonical/inventory or rollback bytes. Mechanical evidence retained these exact bytes.
                await File.WriteAllBytesAsync(physical, changed);
                Assert.Equal(changed, await File.ReadAllBytesAsync(physical));
                await ValidateRetainedRecoveryBaselineAsync();
                await preparation.ValidateInventoryCurrentAsync(Files, Lease);
                AssertJointSlotsAvailable(preparation);
                var takeStart = ResetCandidateCapture("RecoveryActualTakeFreshSourceFence");
                failure = await Record.ExceptionAsync(async () =>
                {
                    taken = await AcceptedTurnAuthorityRegistry.TakeJointRepairPublicationAsync(Files, Lease, preparation);
                });
                counts = CompleteCandidateCapture("RecoveryActualTakeFreshSourceFence", takeStart);
            }
            finally
            {
                await File.WriteAllBytesAsync(physical, original);
                if (taken?.Receipt is { } receipt)
                {
                    var rearmed = await AcceptedTurnAuthorityRegistry.RearmTakenJointRepairPublicationAsync(Files, Lease, receipt);
                    Assert.Equal(MortalJointRepairPublicationDisposition.RetryableHeld, rearmed.Disposition);
                    Assert.Empty(rearmed.Issues);
                }
            }
            var refusal = Assert.IsType<InvalidDataException>(failure);
            Assert.Contains("pending_turn_snapshot_reader_source_changed", refusal.Message, StringComparison.Ordinal);
            Assert.Contains(logicalPath, refusal.Message, StringComparison.Ordinal);
            Assert.Contains("SelectedOriginal", refusal.Message, StringComparison.Ordinal);
            Assert.Null(taken);
            Assert.NotNull(counts);
            foreach (var name in new[] { "Mechanical", "Continuation" })
            {
                Assert.True(counts.GetValueOrDefault("RecoveryValidation.Source." + name) >= 1, name);
                Assert.Equal(0, counts.GetValueOrDefault("RecoveryValidation.Reuse." + name));
            }
            Assert.Equal(original, await File.ReadAllBytesAsync(physical));
            AssertJointSlotsAvailable(preparation);
            await ValidateRetainedRecoveryBaselineAsync();
            Output.WriteLine(JsonSerializer.Serialize(new { Kind = "RecoveryActualTakeFence", LogicalPath = logicalPath, SnapshotPath = path,
                InventoryAcceptedRawDrift = true, BothOriginalSlotsPreserved = true, Refusal = refusal.Message, Counts = counts }));
        }

        /// <summary>
        /// Observes the existing validated common and item slots without taking, rebuilding or granting authority.
        /// </summary>
        /// <param name="preparation">
        /// Genuine complete preparation whose exact common fingerprint and item baseline must still be present.
        /// </param>
        private void AssertJointSlotsAvailable(MortalJointRepairPublicationPreparation preparation)
        {
            Assert.True(AcceptedMechanicsPlanAuthority.TryPeekValidated(Files, Lease, out var binding, out var selected));
            Assert.True(selected.Success, JsonSerializer.Serialize(selected.Issues));
            Assert.Equal(preparation.Plan.PreparedPlanFingerprint, selected.Plan!.PreparedPlanFingerprint);
            Assert.Equal(AcceptedMechanicsPlanFingerprints.ComputeInput(preparation.Binding),
                AcceptedMechanicsPlanFingerprints.ComputeInput(binding));
            Assert.True(AcceptedTurnAuthorityRegistry.ConfirmsMortalJointRepairItemBaseline(Files, Lease,
                preparation.Authority.Continuation, preparation.Authority.Reservation, preparation.Authority.ItemsForRegistry));
        }

        /// <summary>
        /// Runs a bounded probe inside the asynchronous flow that installs and owns its original physical lease.
        /// </summary>
        /// <param name="probe">
        /// Probe invoked after ordinary lease acquisition and before fixture-owned cleanup.
        /// </param>
        /// <returns>
        /// Completion after the probe settles; the fixture retains responsibility for its acquired lease.
        /// </returns>
        internal async Task RunInOriginalRecoveryLeaseFlowAsync(Func<Task> probe)
        {
            await Lease.DisposeAsync();
            _lease = null;
            _lease = await Files.AcquireCanonicalWriteLeaseAsync();
            await probe();
        }
    }
}
