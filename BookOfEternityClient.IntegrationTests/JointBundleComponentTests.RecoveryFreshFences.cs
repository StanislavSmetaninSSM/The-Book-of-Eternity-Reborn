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
    public async Task RecoveryTakeFence_RejectsRawContextDriftBeforeBothRealSlotTakes()
    {
        await using var fixture = new Fixture(_output, nameof(RecoveryTakeFence_RejectsRawContextDriftBeforeBothRealSlotTakes));
        await fixture.InitializeAsync();
        var bound = await fixture.PrepareBoundJointAsync(fixture.AssertWarmTakeRefusesRawContextDriftAsync);
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
        /// Completion after isolated raw-context refusal, exact byte restoration and unchanged slot checks.
        /// </returns>
        internal async Task AssertWarmTakeRefusesRawContextDriftAsync()
        {
            using var scope = Files.BeginRecoveryValidation(Lease);
            var bound = _jointBound ?? throw new InvalidOperationException("A genuine bound common input is required.");
            AcceptedMechanicsPlanningResult common;
            using (Files.MeasureOperation("RecoveryTake.PrepareCommonPlan"))
                common = await WoundAcceptedTurnPlanner.CreateJointRepairCommonPlanFromBoundForComponentAsync(Files, Lease, bound);
            Assert.True(common.Success, JsonSerializer.Serialize(common.Issues));
            var plan = Assert.IsType<AcceptedMechanicsPlan>(common.Plan);
            Assert.Same(bound.Authority, plan.JointRepairCommonAuthority);
            Assert.True(AcceptedMechanicsPlanAuthority.TryPeekValidated(Files, Lease, out var binding, out var selected));
            Assert.Equal(plan.PreparedPlanFingerprint, selected.Plan!.PreparedPlanFingerprint);
            MortalJointRepairPublicationPreparation preparation;
            using (Files.MeasureOperation("RecoveryTake.PreparePublication"))
                preparation = await MortalJointRepairPublicationPreparation.CreateAsync(Files, Lease, plan, binding);
            const string path = "ready/turn_complete.json";
            Assert.False(preparation.BeforeImages.ContainsKey(path));
            Assert.DoesNotContain(path, preparation.MemberPaths);
            var physical = Path.Combine(Files.GameSessionPath, path.Replace('/', Path.DirectorySeparatorChar));
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
                // Raw IO deliberately bypasses mutation epochs. JSON whitespace preserves the parsed frame,
                // but the genuine mechanical evidence retained the exact ContextText bytes before this edit.
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
            Assert.Contains(path, refusal.Message, StringComparison.Ordinal);
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
            Output.WriteLine(JsonSerializer.Serialize(new { Kind = "RecoveryActualTakeFence", Path = path,
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
