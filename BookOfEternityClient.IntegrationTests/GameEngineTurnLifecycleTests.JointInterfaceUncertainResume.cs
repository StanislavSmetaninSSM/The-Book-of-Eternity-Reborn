using System.Text.Json;
using System.Text.Json.Nodes;
using BookOfEternityClient.Core;
using BookOfEternityClient.Services;
using Xunit;

namespace BookOfEternityClient.Tests;

public sealed partial class GameEngineTurnLifecycleTests
{
    [Fact]
    public async Task GuaranteedTreatment_NewCurrentOwnerSupersedesOnlyObsoleteSelectedUncertainDiagnostic()
    {
        using var probe = new JointInterfaceLifecycleProbe(this, "CutResume");
        await RunJointCompletionControlAsync(false, useLiveCaller: true, interfaceLifecycleProbe: probe);
    }

    private sealed partial class JointInterfaceLifecycleProbe
    {
        private readonly IOException _uncertainCutFailure = new("L01 one-shot existing completed capture checkpoint; preparation not entered.");
        private bool _currentResumeRunning;
        private MortalJointInterfacePublication.Record? _currentResumedProof;

        private async Task ObserveUncertainCurrentResumeCheckpointAsync(GameEngine engine, SessionFinalizationCheckpoint checkpoint)
        {
            if (scenario != "CutResume" || !_currentResumeRunning ||
                checkpoint != SessionFinalizationCheckpoint.AcceptedOutcomeValidatedBeforeMaterialization) return;
            await using var lease = await fixture._fs.AcquireCanonicalWriteLeaseAsync();
            var original = MortalQteWoundOpportunityProjection.ReadSnapshot(fixture._fs, lease);
            _currentResumedProof = await MortalQteDecisionPublication.ReadCurrentJointInterfaceDataAsync(fixture._fs, lease,
                original.SessionId, original.RequestId, original.SnapshotToken, original.TurnNumber);
            Assert.NotNull(_currentResumedProof);
            AssertActualJointInterfaceRegistrySettled(fixture._fs);
            fixture.WritePhysicalInjuryDiagnostic("iobs_same_engine_new_current_fullproof_before_cleanup", new
            { Original = new { original.SessionId, original.RequestId, original.SnapshotToken, original.TurnNumber },
                Result = _currentResumedProof.ToJson(), Meaning = "Current full committed DATA proof before legitimate terminal original cleanup" });
        }

        private async Task AssertUncertainCurrentResumeAsync(GameEngine engine, string firstDiagnostics, Func<string> describeCurrentDiagnostics)
        {
            Assert.Equal(1, _captureCallbacks); Assert.Empty(_publications.Where(row => row.MarkerCommitted));
            Assert.Single(_publications.Where(row => row.InterfaceChanged));
            var prior = GetPrivateFieldValue<object?>(engine, "_mortalJointRepairCanonicalRefresh"); Assert.NotNull(prior);
            Assert.Equal(MortalJointRepairPublicationDisposition.PublicationUncertain, prior.GetType().GetProperty("Disposition")!.GetValue(prior));
            var oldAttempt = GetPrivateFieldValue<GameEngine.MortalJointPublicationAttempt?>(engine, "_mortalJointPublicationAttempt");
            Assert.NotNull(oldAttempt); Assert.False(oldAttempt.Live); Assert.False(oldAttempt.Delegated);
            Assert.Null(oldAttempt.Continuation); Assert.Null(oldAttempt.Reservation);
            var record = MortalJointInterfacePublication.Read(JsonSerializer.SerializeToElement(_resultCommits[1]));
            Assert.Equal("Published", record.State); Assert.False(record.HasErrors);
            Assert.Equal(record.AfterBytes, ReadPhysical(InterfacePath));
            Assert.Equal(42, GetPrivateField<GameLoop>(engine, "_gameLoop").TurnNumber);
            foreach (var (path, bytes) in _mechanicalBefore) Assert.Equal(bytes, ReadPhysical(path));
            await AssertActualRetainingOwnersAsync(engine, invokeWrapper: false);
            Dictionary<string, byte[]?> before;
            await using (var lease = await fixture._fs.AcquireCanonicalWriteLeaseAsync())
                before = await CaptureConfirmedClaimReadImagesAsync(fixture._fs, lease);
            var priorObserver = fixture._consolePublicationObserver;
            fixture._consolePublicationObserver = (phase, index) =>
            { priorObserver?.Invoke(phase, index); ObservePublication(phase, index); };
            try
            {
            _currentResumeRunning = true;
            // The existing real late caller binds a NEW participating Main.
            // The old attempt/Ready is not revived, and the one-shot cut is no
            // longer thrown. No private fields or issued permissions are seeded.
            var handled = await InvokePrivateAsync<bool>(engine, "ProcessLateTerminalAndIdleTransitionsForCurrentSessionAsync");
            // Observe this NEW returned call before its positive oracle. No lease,
            // recovery, authority validation or private capability serialization.
            var currentRefresh = GetPrivateFieldValue<object?>(engine, "_mortalJointRepairCanonicalRefresh");
            var currentDisposition = currentRefresh is null ? (MortalJointRepairPublicationDisposition?)null :
                (MortalJointRepairPublicationDisposition)currentRefresh.GetType().GetProperty("Disposition")!.GetValue(currentRefresh)!;
            var currentIssues = currentRefresh is null ? null :
                (IReadOnlyList<ValidationIssue>)currentRefresh.GetType().GetProperty("Issues")!.GetValue(currentRefresh)!;
            fixture.WritePhysicalInjuryDiagnostic("iobs_same_engine_uncertain_new_call_returned", new
            { Handled = handled, CurrentDiagnostics = describeCurrentDiagnostics(),
                Turn = GetPrivateField<GameLoop>(engine, "_gameLoop").TurnNumber,
                CurrentProof = _currentResumedProof?.ToJson(), CaptureCallbacks = _captureCallbacks,
                Disposition = currentDisposition?.ToString(), Issues = currentIssues,
                OldAttempt = new { oldAttempt.Live, oldAttempt.Delegated,
                    ContinuationAbsent = oldAttempt.Continuation is null, ReservationAbsent = oldAttempt.Reservation is null },
                Trace = _publications, Meaning = "New runtime observation only; original failed run had no post-call log" });
            object ObserveImage(string path)
            {
                try { return new { Bytes = File.ReadAllBytes(fixture._fs.ResolvePath(path)), Absent = false, ReadError = (string?)null }; }
                catch (Exception error) when (error is FileNotFoundException or DirectoryNotFoundException)
                { return new { Bytes = (byte[]?)null, Absent = true, ReadError = (string?)null }; }
                catch (Exception error) when (error is IOException or UnauthorizedAccessException)
                { return new { Bytes = (byte[]?)null, Absent = false, ReadError = error.ToString() }; }
            }
            fixture.WritePhysicalInjuryDiagnostic("iobs_same_engine_uncertain_new_call_raw_images", new
            { Pending = ObserveImage(WoundAcceptedTurnSnapshotContract.PendingResolutionPath),
                Interface = ObserveImage(InterfacePath), Mechanical = _mechanicalBefore.Keys.ToDictionary(path => path, ObserveImage),
                Meaning = "Raw read-only images after new call; ReadError distinguishes unreadable from absent" });
            Assert.NotNull(_currentResumedProof); Assert.Equal(43, GetPrivateField<GameLoop>(engine, "_gameLoop").TurnNumber);
            Assert.Equal("HistoricalNUnavailable", engine.CaptureMortalJointRuntimeObservation().NarrativeContribution);
            Assert.Equal(record.ResultFingerprint, _currentResumedProof.ResultFingerprint);
            Assert.True(JsonNode.DeepEquals(record.ToJson(), _currentResumedProof.ToJson()));
            Assert.Equal(record.AfterBytes, ReadPhysical(InterfacePath));
            Assert.Single(_publications.Where(row => row.InterfaceChanged)); // no projector/fallback publication replay
            Assert.Equal(2, _captureCallbacks); Assert.NotEmpty(_publications.Where(row => row.MarkerCommitted));
            Assert.False(oldAttempt.Live);
            Dictionary<string, byte[]?> after;
            await using (var lease = await fixture._fs.AcquireCanonicalWriteLeaseAsync())
            {
                await AssertJointInterfaceResumedMechanicalOutcomeAsync(fixture._fs, lease, before, record.Binding);
                foreach (var path in new[] { AcceptedMechanicsPlan.WoundCommandPath, WoundAcceptedTurnSnapshotContract.PendingResolutionPath,
                    MortalWoundOpportunityAdapter.DecisionPath, LiveTurnPreparationService.PendingTurnSnapshotManifestPath,
                    PendingTurnSnapshotAuthority.AuthorityPath, "input/turn_request.json", "ready/turn_complete.json" })
                    Assert.Null(await fixture._fs.ReadFileBytesAsync(lease, path));
                after = await CaptureConfirmedClaimReadImagesAsync(fixture._fs, lease);
            }
            var repeated = await InvokePrivateAsync<bool>(engine, "ProcessLateTerminalAndIdleTransitionsForCurrentSessionAsync");
            Assert.False(repeated);
            await using (var lease = await fixture._fs.AcquireCanonicalWriteLeaseAsync())
                await AssertConfirmedClaimReadImagesAsync(fixture._fs, lease, after);
            fixture.WritePhysicalInjuryDiagnostic("iobs_same_engine_uncertain_diagnostic_current_resume", new
            { First = "PublicationUncertain after genuine completed capture and no preparation", FirstDiagnostics = firstDiagnostics,
                Before = before, After = after, Handled = handled, InertRepeat = repeated,
                FrozenResult = record.ToJson(), CurrentProof = _currentResumedProof.ToJson(), Trace = _publications,
                Meaning = "Only independently current selected2/child3 proof supersedes obsolete diagnostics; old v1 uncertain policy remains" });
            }
            finally { _currentResumeRunning = false; fixture._consolePublicationObserver = priorObserver; }
        }
    }
}
