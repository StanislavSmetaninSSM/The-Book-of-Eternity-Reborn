using System.Text.Json;
using System.Text.Json.Nodes;
using BookOfEternityClient.Configuration;
using BookOfEternityClient.Services;
using Microsoft.Extensions.Logging;

namespace BookOfEternityClient.Core;

public partial class GameEngine
{
    private enum MortalJointStoryTurnDisposition { Completed, HeldBlocked }

    // This is an actual-caller owner, not a parsed-marker admission route. The
    // schema/tuple look is applicability only; every operation requires fresh
    // original/G/whole-union/phase proof and the normal canonical/narrative pass.
    private async Task<MortalJointStoryTurnDisposition?> TryReconcileMortalJointStoryTurnAsync(
        string source, ValidatedPendingTurnSnapshotContext? frame)
    {
        using var namedJointStoryTimer1 = _fs.MeasureOperation("JointStory.TryReconcileMortalJointStoryTurnAsync");
        if (frame is null) return null;
        var claimed = MatchesMortalJointRepairOutcomeFrame(frame, frame.TurnNumber) &&
            _mortalJointRepairCanonicalRefresh?.Disposition is (MortalJointRepairPublicationDisposition.Committed or
                MortalJointRepairPublicationDisposition.PublicationUncertain);
        try
        {
            using var namedStoryInitialLease = _fs.MeasureOperation("JointStory.InitialInspectionLease");
            await using (var lease = await _fs.AcquireCanonicalWriteLeaseAsync())
            {
                // Nonauthorizing protection precedes marker applicability. Erasing
                // the marker cannot hide a changed signed occurrence/receipt/history
                // partition and send already-published mechanics to ordinary rollback.
                using var namedJointStoryTimer2 = _fs.MeasureOperation("JointStory.InspectCurrentAsync");
                var inspection = await MortalQteDecisionPublication.InspectCurrentAsync(_fs, lease,
                    frame.SessionId, frame.RequestId, frame.Payload.ManifestPayloadHash, frame.TurnNumber);
                namedJointStoryTimer2?.Dispose();
                if (inspection.State == MortalQteDecisionPublication.CurrentPublicationState.IntegrityConflict)
                    return HoldMortalJointStoryTurn("The original-bound publication evidence is inconsistent: " + inspection.Reason);
                using var namedJointStoryTimer3 = _fs.MeasureOperation("JointStory.ReadCompletionState");
                var bytes = await _fs.ReadFileBytesAsync(lease, MortalQteDecisionPublication.StatePath);
                namedJointStoryTimer3?.Dispose();
                // A retained uncertainty is not proof that common publication
                // happened. This read-only classification routes only a current
                // selected Published I into the existing validator below; it
                // grants no completion authority and does not clear diagnostics.
                if (bytes is null)
                {
                    if (!claimed || _mortalJointRepairCanonicalRefresh?.Disposition != MortalJointRepairPublicationDisposition.PublicationUncertain ||
                        inspection.State != MortalQteDecisionPublication.CurrentPublicationState.NotCommitted ||
                        inspection.Reason != "original_baseline" ||
                        !await IsCurrentSelectedPublishedInterfaceResumeCandidateAsync(lease, frame))
                        return claimed ? HoldMortalJointStoryTurn("The joint completion marker is missing.") : null;
                    if (_gameLoop.SessionId != frame.SessionId || _gameLoop.TurnNumber != frame.TurnNumber - 1)
                        return HoldMortalJointStoryTurn("The selected pending original has no prior runtime session/turn.");
                }
                else
                {
                    var marker = StrictJsonAuthority.Deserialize<JsonObject>(CanonicalJsonUtf8.DecodeOneOptionalBom(bytes),
                        SharedJsonOptions.PrettyCamelCaseUnsafeRelaxed, "joint completion applicability");
                    if (marker?["schemaVersion"] is not JsonValue version || !version.TryGetValue<int>(out var schema) || schema != 2)
                        return claimed ? HoldMortalJointStoryTurn("The joint completion family changed.") : null;
                    if (marker["sessionId"]?.GetValue<string>() != frame.SessionId || marker["requestId"]?.GetValue<string>() != frame.RequestId ||
                        marker["snapshotToken"]?.GetValue<string>() != frame.Payload.ManifestPayloadHash || marker["turn"]?.GetValue<int>() != frame.TurnNumber)
                        return claimed ? HoldMortalJointStoryTurn("The joint completion tuple changed.") : null;
                    claimed = true;
                    if (_gameLoop.SessionId != frame.SessionId ||
                        (_gameLoop.TurnNumber != frame.TurnNumber - 1 && _gameLoop.TurnNumber != frame.TurnNumber))
                        return HoldMortalJointStoryTurn("The runtime session/turn is unrelated to the authenticated joint original.");
                    if (!await MortalQteDecisionPublication.IsCurrentJointCompletedFamilyAsync(_fs, lease,
                        frame.SessionId, frame.RequestId, frame.Payload.ManifestPayloadHash, frame.TurnNumber))
                        throw new InvalidDataException("joint_story_turn_family_not_proved");
                    _mortalJointRepairCanonicalRefresh = new(MortalJointRepairPublicationDisposition.Committed,
                        null, Array.Empty<ValidationIssue>(), Frame: frame);
                }
                var completion = ParseReadySignalMetadata(await _fs.ReadFileAsync(lease, "ready/turn_complete.json"), "ready/turn_complete.json");
                if (completion is null || completion.SessionId != frame.SessionId || completion.RequestId != frame.RequestId ||
                    completion.TurnNumber != frame.TurnNumber || !HasValidTerminalSignalContract("turn_complete", completion) ||
                    await _fs.ReadFileBytesAsync(lease, "ready/turn_error.json") is not null)
                    return HoldMortalJointStoryTurn("The original correlated terminal evidence is missing, foreign or contradictory.");
                if (bytes is not null)
                {
                    using var namedJointStoryTimer4 = _fs.MeasureOperation("JointStory.ReadOriginalProgressionPhaseAsync");
                    var phase = await MortalQteDecisionPublication.ReadJointProgressionPhaseAsync(_fs, lease,
                        frame.SessionId, frame.RequestId, frame.Payload.ManifestPayloadHash, frame.TurnNumber);
                    namedJointStoryTimer4?.Dispose();
                    if (_gameLoop.TurnNumber == frame.TurnNumber)
                    {
                        if (phase.Phase != "applied") return HoldMortalJointStoryTurn("An advanced runtime has no applied joint phase.");
                        using var namedJointStoryTimer5 = _fs.MeasureOperation("JointStory.ReadExistingStoryAsync");
                        var existing = await _storyService.ReadMortalJointStoryAsync(lease, phase);
                        namedJointStoryTimer5?.Dispose();
                        if (existing.Disposition != StoryService.MortalJointStoryDisposition.AlreadyAppended || existing.Proof is null)
                            return HoldMortalJointStoryTurn("An advanced runtime has no exact authenticated joint story row.");
                    }
                }
            }

            namedStoryInitialLease?.Dispose();
            using var namedJointStoryTimer6 = _fs.MeasureOperation("JointStory.ValidateAcceptedTurnOutcomeWithRepairLoopAsync");
            var validated = await ValidateAcceptedTurnOutcomeWithRepairLoopAsync(source, frame,
                BuildValidatedRollbackSnapshot(frame), frame.TurnNumber, frame.ProgressionControl);
            namedJointStoryTimer6?.Dispose();
            if (validated != AcceptedTurnValidationDisposition.MortalJointRepairCompletionReady)
                return HoldMortalJointStoryTurn("The complete joint canonical/narrative/phase/projection pass did not authorize its completion owner.");
            using var namedJointStoryTimer7 = _fs.MeasureOperation("JointStory.AcceptedOutcomeCheckpoint");
            await InvokeSessionFinalizationCheckpointAsync(SessionFinalizationCheckpoint.AcceptedOutcomeValidatedBeforeMaterialization);
            namedJointStoryTimer7?.Dispose();

            MortalJointCleanupResult cleanup;
            using var namedStoryFinalLease = _fs.MeasureOperation("JointStory.FinalMaterializationLeaseAndCleanup");
            await using (var lease = await _fs.AcquireCanonicalWriteLeaseAsync())
            {
                using var namedJointStoryTimer8 = _fs.MeasureOperation("JointStory.ReadAppliedProgressionPhaseAsync");
                var applied = await MortalQteDecisionPublication.ReadJointProgressionPhaseAsync(_fs, lease,
                    frame.SessionId, frame.RequestId, frame.Payload.ManifestPayloadHash, frame.TurnNumber);
                namedJointStoryTimer8?.Dispose();
                if (applied.Phase != "applied") throw new InvalidDataException("joint_story_turn_phase_not_applied");
                using var namedJointStoryTimer9 = _fs.MeasureOperation("JointStory.VerifyProjectionBeforeStoryAsync");
                var projection = await _charService.VerifyMortalJointProjectionAsync(lease, applied);
                namedJointStoryTimer9?.Dispose();
                if (projection.Disposition != CharacteristicsService.JointDerivedProjectionDisposition.ExistingVerified)
                    return HoldMortalJointStoryTurn("The actual pre-story derived projection is not verified: " + projection.Failure);
                if (_gameLoop.SessionId != frame.SessionId ||
                    (_gameLoop.TurnNumber != frame.TurnNumber - 1 && _gameLoop.TurnNumber != frame.TurnNumber))
                    return HoldMortalJointStoryTurn("The runtime changed before story publication.");
                using var namedJointStoryTimer10 = _fs.MeasureOperation("JointStory.AppendStoryAsync");
                var story = await _storyService.AppendMortalJointStoryAsync(lease, applied);
                namedJointStoryTimer10?.Dispose();
                if (story.Disposition is not (StoryService.MortalJointStoryDisposition.AppendedAndVerified or
                    StoryService.MortalJointStoryDisposition.AlreadyAppended) || story.Proof is null)
                    return HoldMortalJointStoryTurn("The exact joint story publication remains unproved: " + story.Failure);
                using var namedJointStoryTimer11 = _fs.MeasureOperation("JointStory.ValidateStoryProofAsync");
                await story.Proof.ValidateCurrentAsync(_fs, lease);
                namedJointStoryTimer11?.Dispose();
                using var namedJointStoryTimer12 = _fs.MeasureOperation("JointStory.ValidateAppliedProgressionAsync");
                await applied.ValidateCurrentAsync(_fs, lease);
                namedJointStoryTimer12?.Dispose();
                using var namedJointStoryTimer13 = _fs.MeasureOperation("JointStory.VerifyProjectionAfterStoryAsync");
                projection = await _charService.VerifyMortalJointProjectionAsync(lease, applied);
                namedJointStoryTimer13?.Dispose();
                if (projection.Disposition != CharacteristicsService.JointDerivedProjectionDisposition.ExistingVerified)
                    return HoldMortalJointStoryTurn("The derived projection changed during story publication: " + projection.Failure);
                if (_gameLoop.SessionId != frame.SessionId) throw new InvalidDataException("joint_story_turn_runtime_session_drift");
                if (_gameLoop.TurnNumber == frame.TurnNumber - 1) _gameLoop.IncrementTurn();
                else if (_gameLoop.TurnNumber != frame.TurnNumber) throw new InvalidDataException("joint_story_turn_runtime_drift");
                using var namedJointStoryTimer14 = _fs.MeasureOperation("JointStory.CleanupEligibleTerminalAsync");
                cleanup = await CleanupEligibleMortalJointTerminalAsync(lease, frame);
                namedJointStoryTimer14?.Dispose();
                if (cleanup.Disposition != MortalJointCleanupDisposition.CommittedAndVerified || cleanup.Response is null)
                    return HoldMortalJointStoryTurn("The owned terminal cleanup is not verified: " + cleanup.Failure);
            }
            namedStoryFinalLease?.Dispose();
            // Only the known owned deletion batch and complete readback settle
            // completion. Do not reissue authority from the now-deleted original.
            _inGame = true;
            _lastResponse = cleanup.Response!;
            _pendingImagePrompt = _lastResponse.ImagePrompt;
            TakeAcceptedTurnWoundNotifications();
            _mortalJointRepairCanonicalRefresh = null;
            _mortalJointRepairCheckpoint = null;
            _mortalJointRepairCorrectionReady = false;
            _mortalJointRepairTerminalReason = "validation_failed";
            EndHotOrdinaryOriginal("VerifiedJointTerminalCleanup");
            ClearMortalQteRepairState();
            _logger.LogInformation("Joint story and runtime turn {Turn} completed once after {Source}; owned terminal cleanup verified.",
                frame.TurnNumber, source);
            return MortalJointStoryTurnDisposition.Completed;
        }
        catch (SessionReplacedException) { throw; }
        catch (Exception error) when (claimed && error is (CoordinatedStatePublicationUncertainException or InvalidDataException or IOException or
            UnauthorizedAccessException or InvalidOperationException or ArgumentException or JsonException or FormatException or OperationCanceledException))
        {
            _logger.LogError(error, "Joint story/turn owner blocked after {Source}; no destructive ordinary fallback or cleanup.", source);
            return HoldMortalJointStoryTurn(error.Message);
        }
    }

    // Applicability only, borrowed lease only. The direct validator must still
    // reconstruct its own current owner and complete the full physical G proof.
    private async Task<bool> IsCurrentSelectedPublishedInterfaceResumeCandidateAsync(
        FileSystemManager.CanonicalWriteLease lease, ValidatedPendingTurnSnapshotContext frame)
    {
        using var namedJointStoryTimer15 = _fs.MeasureOperation("JointStory.IsSelectedPublishedInterfaceResumeCandidateAsync");
        _fs.EnsureCanonicalWriteLeaseActive(lease);
        RequireOrdinaryJointRepairLease(lease);
        var bytes = await _fs.ReadFileBytesAsync(lease, WoundAcceptedTurnSnapshotContract.PendingResolutionPath);
        if (bytes is null) return false;
        var pending = ReadOrdinaryTreatmentRoot(bytes, "selected pending before joint story dispatch");
        if (pending["jointRepairCheckpoint"] is not { } node) return false;
        var parsed = MortalJointRepairCheckpoint.Parse(JsonSerializer.SerializeToElement(node));
        if (!parsed.IsValid || parsed.Checkpoint is not { UsesInterfacePublication: true } checkpoint) return false;
        var binding = checkpoint.ToJsonObject();
        if (binding["sessionId"]!.GetValue<string>() != frame.SessionId ||
            binding["requestId"]!.GetValue<string>() != frame.RequestId ||
            binding["snapshotToken"]!.GetValue<string>() != frame.Payload.ManifestPayloadHash ||
            binding["turnNumber"]!.GetValue<int>() != frame.TurnNumber) return false;
        ThrowIfRepairSessionReplaced(lease, binding["sessionGeneration"]!.GetValue<string>());
        if (pending[MortalJointInterfacePublication.PendingField] is not { } data) return false;
        var record = MortalJointInterfacePublication.Read(JsonSerializer.SerializeToElement(data));
        record.RequireCheckpoint(binding);
        record.RequireLegacyPending(pending);
        if (record.State != "Published" || record.HasErrors) return false;
        await MortalJointInterfacePublication.RequireImagesAsync(_fs, lease, bytes, record.AfterBytes);
        return true;
    }

    private MortalJointStoryTurnDisposition HoldMortalJointStoryTurn(string reason)
    {
        _inGame = false;
        _logger.LogWarning("Joint story/turn completion held: {Reason}; original, ready, outputs, backups and committed union retained.", reason);
        return MortalJointStoryTurnDisposition.HeldBlocked;
    }
}
