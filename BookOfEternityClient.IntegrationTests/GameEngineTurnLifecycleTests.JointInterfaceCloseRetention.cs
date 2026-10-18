using System.Text.Json;
using BookOfEternityClient.Core;
using BookOfEternityClient.Services;
using BookOfEternityClient.Services.GmRuntime;
using Xunit;

namespace BookOfEternityClient.Tests;

public sealed partial class GameEngineTurnLifecycleTests
{
    [Fact]
    public async Task GuaranteedTreatment_InterfaceOwningCloseRetainsPublishedButCannotDeliverFreshResult()
    {
        using var probe = new JointInterfaceLifecycleProbe(this, "InterfaceClose");
        var operation = RunJointCompletionControlAsync(false, useLiveCaller: true, interfaceLifecycleProbe: probe);
        try
        {
            await probe.AttachInitialOwnerCloseAsync(operation);
            await operation;
        }
        finally
        {
            probe.ReleaseInitialRead();
            if (!operation.IsCompleted) await Record.ExceptionAsync(() => operation);
        }
    }

    [Fact]
    public async Task GuaranteedTreatment_LaterOuterFinalizationDoesNotUnemitCommittedInterfaceOrReplay()
    {
        using var probe = new JointInterfaceLifecycleProbe(this, "OuterClose");
        await RunJointCompletionControlAsync(false, useLiveCaller: true, interfaceLifecycleProbe: probe);
        // Shared full-union/turn43/inert-repeat proof still runs. Only the
        // original outer await is expected not to return successfully.
        probe.AssertEarlyInterfaceAndSingleConsumption();
    }

    private sealed partial class JointInterfaceLifecycleProbe
    {
        internal bool ExpectsOuterClose => scenario == "OuterClose";
        private Func<Task>? _priorOuterClosing;
        private readonly IOException _outerCloseFailure = new("L01 actual outer session finalization after committed I/common.");
        private int _outerCloses;

        private void AttachOuterCloseObservation()
        {
            if (!ExpectsOuterClose) return;
            _priorOuterClosing = fixture._consoleSessionClosingObserver;
            fixture._consoleSessionClosingObserver = async () =>
            {
                if (_priorOuterClosing is { } prior) await prior();
                if (_firstMarker is null || _outerCloses != 0) return;
                Assert.Equal(43, GetPrivateField<GameLoop>(_engine!, "_gameLoop").TurnNumber);
                Assert.Equal("Published", _resultCommits.Last()["state"]!.GetValue<string>());
                _outerCloses++;
                fixture.WritePhysicalInjuryDiagnostic("iobs_actual_outer_finalization_cut", new
                { OriginalCloseReturned = false, InterfaceOwningReleaseAlreadyDelivered = GetPrivateFieldValue<object?>(_engine!, "_mortalJointInterfaceData") is not null,
                    Record = _resultCommits.Last(), Marker = _firstMarker, Interface = Raw(ReadPhysical(InterfacePath)),
                    Exception = _outerCloseFailure.Message, Turn = GetPrivateField<GameLoop>(_engine!, "_gameLoop").TurnNumber });
                throw _outerCloseFailure;
            };
        }

        private void DisposeOuterCloseObservation()
        { if (ExpectsOuterClose) fixture._consoleSessionClosingObserver = _priorOuterClosing; }

        internal void AssertOuterCloseRetained(GameEngine engine, Exception? failure)
        {
            Assert.Equal(1, _outerCloses);
            var typed = Assert.IsType<MainOperationContinuationException<object?>>(failure);
            Assert.Null(typed.EstablishedResult); Assert.Equal(MainOperationOutcome.Completed, typed.EstablishedOutcome);
            Assert.Same(_outerCloseFailure, typed.InnerException); Assert.Same(_outerCloseFailure, typed.Data["SessionFinalizationFailure"]);
            Assert.Equal(true, _outerCloseFailure.Data["SessionFinalizationFailure"]);
            Assert.NotNull(GetPrivateFieldValue<object?>(engine, "_mortalJointInterfaceData"));
            Assert.Equal(43, GetPrivateField<GameLoop>(engine, "_gameLoop").TurnNumber);
            Assert.Equal(_atCapture, ReadPhysical(InterfacePath));
            fixture.WritePhysicalInjuryDiagnostic("iobs_actual_outer_finalization_retained", new
            { Failure = failure.ToString(), EstablishedResult = typed.EstablishedResult, Outcome = typed.EstablishedOutcome.ToString(),
                OriginalClose = typed.OriginalClose, Interface = Raw(ReadPhysical(InterfacePath)), Record = _resultCommits.Last(), Marker = _firstMarker,
                Meaning = "Public I owning return preceded same-common commit; later outer close cannot unemit DATA, fake overall return or authorize re-projection" });
        }

        private void AssertInterfaceCloseHeld(GameEngine engine, string diagnostics)
        {
            Assert.True(_initialInstalled); Assert.True(_initialPaused); Assert.Equal(1, _initialCloser!.Calls);
            Assert.NotNull(_initialOwner); Assert.False(_initialOwner.IsActive); Assert.Null(_initialOwner.MainAdmission);
            Assert.Null(_initialOwner.AmbientRegistration); Assert.Null(_initialOwner.ExternalPublicationContext);
            var pendingBytes = ReadPhysical(WoundAcceptedTurnSnapshotContract.PendingResolutionPath);
            var pending = ParseJsonObjectBytes(Assert.IsType<byte[]>(pendingBytes));
            var result = MortalJointInterfacePublication.Read(JsonSerializer.SerializeToElement(pending[MortalJointInterfacePublication.PendingField]));
            fixture.WritePhysicalInjuryDiagnostic("iobs_actual_interface_owning_close_retained", new
            { BeforeClose = Raw(_initialPending), ActualPending = Raw(pendingBytes), Published = result.ToJson(),
                Interface = Raw(ReadPhysical(InterfacePath)), Calls = _initialCloser.Calls, Diagnostics = diagnostics,
                DataDelivered = GetPrivateFieldValue<object?>(engine, "_mortalJointInterfaceData") is not null,
                OwningReleaseConfirmed = false, OuterFinalizationIsDistinct = true });
            Assert.Equal(_initialPending, pendingBytes); Assert.Equal("Published", result.State); Assert.False(result.HasErrors);
            Assert.Equal(result.AfterBytes, ReadPhysical(InterfacePath));
            Assert.Null(GetPrivateFieldValue<object?>(engine, "_mortalJointInterfaceData"));
            Assert.True(GetPrivateFieldValue<bool>(engine, "_mortalJointInterfaceFreshConsumed"));
            Assert.True(GetPrivateFieldValue<bool>(engine, "_mortalJointInterfacePreparationHeld"));
            Assert.Equal(42, GetPrivateField<GameLoop>(engine, "_gameLoop").TurnNumber);
            Assert.Equal(0, _captureCallbacks); Assert.Empty(_publications.Where(row => row.MarkerCommitted));
            Assert.Single(_publications.Where(row => row.InterfaceChanged));
            foreach (var (path, before) in _mechanicalBefore) Assert.Equal(before, ReadPhysical(path));
            Assert.Contains(_initialCloseFailure.Message, diagnostics);
        }

        private async Task AssertActualRetainingOwnersAsync(GameEngine engine, bool invokeWrapper = true)
        {
            var stage = GetPrivateFieldValue<object?>(engine, "_hotOrdinaryOriginal"); Assert.NotNull(stage);
            var preparation = GetPrivateFieldValue<object?>(stage, "Preparation"); Assert.NotNull(preparation);
            var backup = preparation.GetType().GetProperty("Backup")!.GetValue(preparation); Assert.NotNull(backup);
            var frame = preparation.GetType().GetProperty("SnapshotContext")!.GetValue(preparation); Assert.NotNull(frame);
            var progression = frame.GetType().GetProperty("ProgressionControl")!.GetValue(frame);
            var before = CaptureCanonicalFiles();
            Exception? direct = null; object? wrapper = null;
            await SessionOperationContext.RunParticipatingCurrentSessionAsync(fixture._fs, async () =>
            {
                // Use the ACTUAL Stage backup/frame, never a manufactured DTO.
                // The direct restore's durable guard must act even without entry dispatch.
                direct = await Record.ExceptionAsync(() => InvokePrivateTaskAsync(engine, "RestorePreTurnBackup", backup));
                Assert.NotNull(direct); Assert.Equal("MortalJointRepairPublicationHoldException", direct.GetType().Name);
                if (invokeWrapper)
                {
                    wrapper = await InvokePrivateTaskResultAsync(engine, "ValidateAcceptedTurnOutcomeWithRepairLoopAsync",
                        "L01 selected graph retention control", frame, backup, 43, progression);
                    Assert.Equal("MortalJointRepairPublicationHeldBlocked", wrapper.ToString());
                }
                return true;
            });
            var after = CaptureCanonicalFiles();
            fixture.WritePhysicalInjuryDiagnostic("iobs_actual_direct_and_outer_restore_retention", new
            { scenario, DirectFailure = direct!.ToString(), ActualOuterDisposition = wrapper?.ToString(),
                Before = before.ToDictionary(row => row.Key, row => Raw(row.Value)), After = after.ToDictionary(row => row.Key, row => Raw(row.Value)) });
            Assert.Equal(before.Keys.Order(StringComparer.Ordinal), after.Keys.Order(StringComparer.Ordinal));
            foreach (var (path, bytes) in before) Assert.Equal(bytes, after[path]);
        }
    }
}
