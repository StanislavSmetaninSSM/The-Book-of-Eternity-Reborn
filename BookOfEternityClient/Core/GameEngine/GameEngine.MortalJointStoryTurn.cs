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
        if (frame is null) return null;
        var claimed = MatchesMortalJointRepairOutcomeFrame(frame, frame.TurnNumber) &&
            _mortalJointRepairCanonicalRefresh?.Disposition is (MortalJointRepairPublicationDisposition.Committed or
                MortalJointRepairPublicationDisposition.PublicationUncertain);
        try
        {
            await using (var lease = await _fs.AcquireCanonicalWriteLeaseAsync())
            {
                var bytes = await _fs.ReadFileBytesAsync(lease, MortalQteDecisionPublication.StatePath);
                if (bytes is null) return claimed ? HoldMortalJointStoryTurn("The joint completion marker is missing.") : null;
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
                var completion = ParseReadySignalMetadata(await _fs.ReadFileAsync(lease, "ready/turn_complete.json"), "ready/turn_complete.json");
                if (completion is null || completion.SessionId != frame.SessionId || completion.RequestId != frame.RequestId ||
                    completion.TurnNumber != frame.TurnNumber || !HasValidTerminalSignalContract("turn_complete", completion) ||
                    await _fs.ReadFileBytesAsync(lease, "ready/turn_error.json") is not null)
                    return HoldMortalJointStoryTurn("The original correlated terminal evidence is missing, foreign or contradictory.");
                var phase = await MortalQteDecisionPublication.ReadJointProgressionPhaseAsync(_fs, lease,
                    frame.SessionId, frame.RequestId, frame.Payload.ManifestPayloadHash, frame.TurnNumber);
                if (_gameLoop.TurnNumber == frame.TurnNumber)
                {
                    if (phase.Phase != "applied") return HoldMortalJointStoryTurn("An advanced runtime has no applied joint phase.");
                    var existing = await _storyService.ReadMortalJointStoryAsync(lease, phase);
                    if (existing.Disposition != StoryService.MortalJointStoryDisposition.AlreadyAppended || existing.Proof is null)
                        return HoldMortalJointStoryTurn("An advanced runtime has no exact authenticated joint story row.");
                }
            }

            var validated = await ValidateAcceptedTurnOutcomeWithRepairLoopAsync(source, frame,
                BuildValidatedRollbackSnapshot(frame), frame.TurnNumber, frame.ProgressionControl);
            if (validated != AcceptedTurnValidationDisposition.MortalJointRepairCompletionReady)
                return HoldMortalJointStoryTurn("The complete joint canonical/narrative/phase/projection pass did not authorize its completion owner.");
            await InvokeSessionFinalizationCheckpointAsync(SessionFinalizationCheckpoint.AcceptedOutcomeValidatedBeforeMaterialization);

            MortalJointCleanupResult cleanup;
            await using (var lease = await _fs.AcquireCanonicalWriteLeaseAsync())
            {
                var applied = await MortalQteDecisionPublication.ReadJointProgressionPhaseAsync(_fs, lease,
                    frame.SessionId, frame.RequestId, frame.Payload.ManifestPayloadHash, frame.TurnNumber);
                if (applied.Phase != "applied") throw new InvalidDataException("joint_story_turn_phase_not_applied");
                var projection = await _charService.VerifyMortalJointProjectionAsync(lease, applied);
                if (projection.Disposition != CharacteristicsService.JointDerivedProjectionDisposition.ExistingVerified)
                    return HoldMortalJointStoryTurn("The actual pre-story derived projection is not verified: " + projection.Failure);
                if (_gameLoop.SessionId != frame.SessionId ||
                    (_gameLoop.TurnNumber != frame.TurnNumber - 1 && _gameLoop.TurnNumber != frame.TurnNumber))
                    return HoldMortalJointStoryTurn("The runtime changed before story publication.");
                var story = await _storyService.AppendMortalJointStoryAsync(lease, applied);
                if (story.Disposition is not (StoryService.MortalJointStoryDisposition.AppendedAndVerified or
                    StoryService.MortalJointStoryDisposition.AlreadyAppended) || story.Proof is null)
                    return HoldMortalJointStoryTurn("The exact joint story publication remains unproved: " + story.Failure);
                await story.Proof.ValidateCurrentAsync(_fs, lease);
                await applied.ValidateCurrentAsync(_fs, lease);
                projection = await _charService.VerifyMortalJointProjectionAsync(lease, applied);
                if (projection.Disposition != CharacteristicsService.JointDerivedProjectionDisposition.ExistingVerified)
                    return HoldMortalJointStoryTurn("The derived projection changed during story publication: " + projection.Failure);
                if (_gameLoop.SessionId != frame.SessionId) throw new InvalidDataException("joint_story_turn_runtime_session_drift");
                if (_gameLoop.TurnNumber == frame.TurnNumber - 1) _gameLoop.IncrementTurn();
                else if (_gameLoop.TurnNumber != frame.TurnNumber) throw new InvalidDataException("joint_story_turn_runtime_drift");
                cleanup = await CleanupEligibleMortalJointTerminalAsync(lease, frame);
                if (cleanup.Disposition != MortalJointCleanupDisposition.CommittedAndVerified || cleanup.Response is null)
                    return HoldMortalJointStoryTurn("The owned terminal cleanup is not verified: " + cleanup.Failure);
            }
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

    private MortalJointStoryTurnDisposition HoldMortalJointStoryTurn(string reason)
    {
        _inGame = false;
        _logger.LogWarning("Joint story/turn completion held: {Reason}; original, ready, outputs, backups and committed union retained.", reason);
        return MortalJointStoryTurnDisposition.HeldBlocked;
    }
}
