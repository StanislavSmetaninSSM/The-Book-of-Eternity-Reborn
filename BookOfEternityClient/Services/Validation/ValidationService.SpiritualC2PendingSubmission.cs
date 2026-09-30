using System.Text;
using System.Text.Json;
using System.Text.Json.Nodes;
using BookOfEternityClient.Core;

namespace BookOfEternityClient.Services;

public partial class ValidationService
{
    internal sealed partial class SpiritualOriginalTurnCapture
    {
        /// <summary>
        /// Gets whether strict original replay retained a selected decision awaiting continuation.
        /// </summary>
        internal bool HasC2PendingSubmission => IsCurrentOwner && _c2PendingSubmission;

        /// <summary>
        /// Reads bounded physical changes and checks the exact saved command before continuation.
        /// </summary>
        /// <param name="lease">
        /// Active lease covering the checkpoint and all draft reads.
        /// </param>
        /// <param name="checkpoint">
        /// Owner-matched checkpoint containing the immutable committed layer.
        /// </param>
        /// <returns>
        /// Changed images, with no permission to change any other original input.
        /// </returns>
        private async Task<List<(string Path, CanonicalBeforeImage Image)>> ReadC2SubmissionChangesCoreAsync(
            FileSystemManager.CanonicalWriteLease lease, SpiritualWoundCaptureCheckpointState checkpoint)
        {
            var committed = ReadC2CommittedInputLayer(checkpoint);
            var changes = new List<(string Path, CanonicalBeforeImage Image)>();
            foreach (var path in _draftInputs.PathInventory)
            {
                var bytes = await _validator._fs.ReadFileBytesAsync(lease, path);
                var image = new CanonicalBeforeImage(bytes is not null, bytes);
                if (SameExactImage(committed[path], image)) continue;
                if (path != AcceptedMechanicsPlan.WoundCommandPath && path != FixedOriginalOutputPaths[0] &&
                    path != AfterlifeSpiritualConflictState.StatePath)
                    throw new InvalidOperationException("A dependent input changed outside the decision layer.");
                changes.Add((path, image));
            }
            if (checkpoint.ReadPendingSubmission() is { } submission)
            {
                var expected = submission["command"] is JsonObject command
                    ? new CanonicalBeforeImage(true, Convert.FromBase64String(command["contentBase64"]!.GetValue<string>()))
                    : committed[AcceptedMechanicsPlan.WoundCommandPath];
                var bytes = await _validator._fs.ReadFileBytesAsync(lease, AcceptedMechanicsPlan.WoundCommandPath);
                if (!SameExactImage(expected, new CanonicalBeforeImage(bytes is not null, bytes)))
                    throw new InvalidOperationException("The saved selected command changed.");
            }
            return changes;
        }

        /// <summary>
        /// Probes only the prospective next source and rolls back every probe allocation.
        /// It never commits source evidence or executes a resource exchange.
        /// </summary>
        /// <param name="lease">
        /// Active lease retained with the capture gate.
        /// </param>
        /// <param name="changes">
        /// Bounded dependent draft changes from the committed input layer.
        /// </param>
        /// <returns>
        /// Genuine next-source diagnostics, or an empty collection for a valid or absent next exchange.
        /// </returns>
        private async Task<IReadOnlyList<ValidationIssue>> ProbeC2SelectedContinuationCoreAsync(
            FileSystemManager.CanonicalWriteLease lease,
            IReadOnlyList<(string Path, CanonicalBeforeImage Image)> changes)
        {
            var packet = _c2SelectedDecision!.PacketRoot["pending"]!;
            var nextOrdinal = packet["cursor"]!["nextSourceOrdinal"]!.GetValue<int>();
            var correction = changes.FirstOrDefault(change => change.Path == AfterlifeSpiritualConflictState.StatePath);
            if (nextOrdinal != packet["sources"]!.AsArray().Count)
                return correction.Path is null ? [] :
                    DecisionDraftFailure("spiritual_c2_conflict_continuation_before_source_resolution").Issues;
            if (!_source.TryReadInitialActiveExchangeInventory(out _, out var exchangeIds))
                return DecisionDraftFailure("spiritual_c2_original_exchange_inventory_invalid").Issues;
            if (_closedExchangeEvidence.Count == exchangeIds.Length)
                return correction.Path is null ? [] :
                    DecisionDraftFailure("spiritual_c2_conflict_continuation_not_needed").Issues;
            using var speculation = _allocations.Clock.BeginSpeculation();
            try
            {
                if (correction.Path is not null)
                {
                    if (!correction.Image.Existed || _coldCurrentInputs is null || _coldProposedInputs is not null)
                        return DecisionDraftFailure("spiritual_cold_continuation_view_invalid").Issues;
                    var proposed = _coldCurrentInputs.WithImageChanges(
                        new Dictionary<string, CanonicalBeforeImage>(StringComparer.Ordinal)
                        { [correction.Path] = correction.Image });
                    if (!_source.MatchesColdOriginalExchangeInventory(_draftInputs, proposed))
                        return DecisionDraftFailure("spiritual_cold_continuation_inventory_changed").Issues;
                    _coldProposedInputs = proposed;
                }
                var prepared = await SpiritualWoundSourceSession.PreparedContinuation.PrepareAsync(
                    _source, lease, this);
                return prepared.Ticket is not null ? prepared.Issues : prepared.Issues.Count != 0
                    ? prepared.Issues : DecisionDraftFailure("spiritual_c2_conflict_continuation_incomplete").Issues;
            }
            finally { _coldProposedInputs = null; }
        }

        /// <summary>
        /// Saves a genuine selection before any later exchange execution, then reopens its exact owner.
        /// </summary>
        /// <param name="lease">
        /// Active lease held through selection, checkpoint replacement and recovery.
        /// </param>
        /// <param name="writer">
        /// Atomic checkpoint and pending writer, including an optional test transport.
        /// </param>
        /// <param name="expectedCommandBytes">
        /// Exact adapter-composed bytes, or <see langword="null"/> for internal direct transport.
        /// </param>
        /// <returns>
        /// Continued transport result or a durable dependent submission without a committed advance.
        /// </returns>
        private async Task<SpiritualC2SavedTransportResult> StageAndResumeC2SubmissionCoreAsync(
            FileSystemManager.CanonicalWriteLease lease,
            Func<FileSystemManager.CanonicalWriteLease, SpiritualOriginalTurnCapture, string, byte[], Task> writer,
            byte[]? expectedCommandBytes)
        {
            try
            {
                var selected = await AdvanceC2NextDecisionDraftCoreAsync(lease, expectedCommandBytes,
                    selectionOnly: true);
                if (selected.Issues.Count != 0 || !_c2SelectionVerified || _c2SelectedDecision is null)
                    return new("blocked", null, null, selected.Issues);
                var pair = _matchedC2Pair!;
                var parsed = SpiritualWoundCaptureCheckpointState.Parse(DecodePhysicalRoot(pair.CheckpointBytes),
                    SpiritualWoundCaptureCheckpointState.StatePath, _draftInputs.PathInventory);
                var checkpoint = parsed.State!;
                var root = SpiritualWoundStateJson.Parse(SpiritualWoundCaptureCheckpointState.SerializeCanonical(checkpoint));
                var body = root["checkpoint"]!.AsObject();
                var committedCount = body["allocations"]!.AsArray().Count;
                var selectionAllocations = _allocations.Journal.ExportConsumedPrefix();
                var commandBytes = _c2SelectedDecision.CommandBytes;
                body["pendingSubmission"] = new JsonObject
                {
                    ["priorCommittedAdvance"] = checkpoint.CommittedAdvance,
                    ["priorPendingPacketFingerprint"] = pair.Pending.PacketFingerprint,
                    ["stagedDecision"] = _c2SelectedDecision.Staged.DeepClone(),
                    ["command"] = commandBytes is null ? null : new JsonObject
                    {
                        ["contentBase64"] = Convert.ToBase64String(commandBytes),
                        ["contentFingerprint"] = RawFingerprint(commandBytes)
                    },
                    ["allocations"] = new JsonArray(selectionAllocations.Skip(committedCount)
                        .Select(row => row!.DeepClone()).ToArray())
                };
                body["checkpointFingerprint"] = SpiritualWoundStateJson.Hash(body,
                    "spiritual_capture_checkpoint_v1", "checkpointFingerprint");
                var candidate = SpiritualWoundCaptureCheckpointState.Parse(root.ToJsonString(),
                    SpiritualWoundCaptureCheckpointState.StatePath, _draftInputs.PathInventory);
                if (candidate.State is null || !candidate.IsValid ||
                    SpiritualWoundCaptureCheckpointState.PlanPendingSubmission(checkpoint, candidate.State).Disposition != "staged")
                    return SavedTransportFailure("blocked", "spiritual_c2_submission_invalid");
                var changes = await ReadC2SubmissionChangesCoreAsync(lease, candidate.State);
                var issues = await ProbeC2SelectedContinuationCoreAsync(lease, changes);
                if (issues.Count != 0 && !IsCorrectableDependentConflictFailure(issues))
                    return new("blocked", null, null, issues);
                var repeatedChanges = await ReadC2SubmissionChangesCoreAsync(lease, candidate.State);
                if (changes.Count != repeatedChanges.Count || changes.Where((change, index) =>
                        change.Path != repeatedChanges[index].Path ||
                        !SameExactImage(change.Image, repeatedChanges[index].Image)).Any())
                    return SavedTransportFailure("blocked", "spiritual_c2_proposed_input_changed");
                var freshness = await CheckRetainedInputsAsync(lease);
                if (freshness.Count != 0) return new("blocked", null, null, freshness);
                if (!ExactBytes(await _validator._fs.ReadFileBytesAsync(lease,
                        SpiritualWoundCaptureCheckpointState.StatePath), pair.CheckpointBytes) ||
                    !ExactBytes(await _validator._fs.ReadFileBytesAsync(lease,
                        SpiritualWoundDecisionPendingState.StatePath), pair.PendingBytes))
                    return SavedTransportFailure("blocked", "spiritual_c2_pair_changed");
                var bytes = Encoding.UTF8.GetBytes(SpiritualWoundCaptureCheckpointState.SerializeCanonical(candidate.State));
                try { await writer(lease, this, SpiritualWoundCaptureCheckpointState.StatePath, bytes); }
                catch (Exception) { /* Exact read-back resolves the replacement. */ }
                var confirmed = await _validator._fs.ReadFileBytesAsync(lease,
                    SpiritualWoundCaptureCheckpointState.StatePath);
                if (!ExactBytes(confirmed, bytes))
                    return ExactBytes(confirmed, pair.CheckpointBytes)
                        ? SavedTransportFailure("not_committed", "spiritual_c2_submission_not_committed")
                        : SavedTransportFailure("blocked", "spiritual_c2_submission_ambiguous");
                var pendingNow = await _validator._fs.ReadFileBytesAsync(lease,
                    SpiritualWoundDecisionPendingState.StatePath);
                if (!ExactBytes(await _validator._fs.ReadFileBytesAsync(lease,
                        SpiritualWoundCaptureCheckpointState.StatePath), bytes))
                    return SavedTransportFailure("blocked", "spiritual_c2_submission_postflight_changed");
                if (!IsCurrentOwner || !ExactBytes(pendingNow, pair.PendingBytes))
                    return SavedTransportFailure("repair_required", "spiritual_c2_submission_pending_repair_required",
                        candidate.State, pair.Pending);
                var originIssues = await CheckRetainedInputsAfterC2TransportAsync(lease, bytes, pair.PendingBytes);
                if (originIssues.Count != 0) return new("blocked", null, null, originIssues);
                await ReadC2SubmissionChangesCoreAsync(lease, candidate.State);
                Dispose();
                if (issues.Count != 0)
                    return new("blocked", candidate.State, pair.Pending, issues, RequiresDependentContinuation: true);
                var reopened = await _validator.RepairSpiritualPendingAsync(lease);
                using var resumed = reopened.Capture;
                if (resumed is null || reopened.Disposition is not ("match" or "repaired"))
                    return new("blocked", null, null, reopened.Issues);
                return await resumed.CommitC2SavedTransportAsync(lease, writer, expectedCommandBytes);
            }
            catch (Exception error) when (error is IOException or UnauthorizedAccessException or
                InvalidOperationException or FormatException or JsonException or OverflowException)
            {
                return SavedTransportFailure("blocked", "spiritual_c2_submission_failed");
            }
            finally
            {
                if (IsCurrentOwner) RevokeUnderLease(lease);
            }
        }

        /// <summary>
        /// Replays only the saved selection after all committed packet and allocation boundaries matched.
        /// </summary>
        /// <param name="lease">
        /// Active lease for genuine original-owner execution.
        /// </param>
        /// <param name="checkpoint">
        /// Strict checkpoint containing the selected decision suffix.
        /// </param>
        /// <param name="pending">
        /// Last committed packet rederived by this capture.
        /// </param>
        /// <returns>
        /// Empty issues only after the saved outcome and complete journal match real execution.
        /// </returns>
        internal async Task<IReadOnlyList<ValidationIssue>> ReplayC2PendingSubmissionAsync(
            FileSystemManager.CanonicalWriteLease lease, SpiritualWoundCaptureCheckpointState checkpoint,
            SpiritualWoundDecisionPendingState pending)
        {
            await _gate.WaitAsync();
            var succeeded = false;
            try
            {
                EnsureCurrent(lease);
                if (_matchedC2Pair is not null || !checkpoint.HasPendingSubmission || _c2PendingSubmission)
                    return DecisionDraftFailure("spiritual_c2_submission_replay_invalid").Issues;
                var submission = checkpoint.ReadPendingSubmission()!;
                var layer = ReadC2CommittedInputLayer(checkpoint);
                var changes = new List<(string Path, CanonicalBeforeImage Image)>();
                if (submission["command"] is JsonObject command)
                {
                    var image = new CanonicalBeforeImage(true,
                        Convert.FromBase64String(command["contentBase64"]!.GetValue<string>()));
                    if (SameExactImage(layer[AcceptedMechanicsPlan.WoundCommandPath], image))
                        return DecisionDraftFailure("spiritual_c2_submission_command_unchanged").Issues;
                    layer[AcceptedMechanicsPlan.WoundCommandPath] = image;
                    changes.Add((AcceptedMechanicsPlan.WoundCommandPath, image));
                }
                var result = await ComposeC2DecisionLayerCoreAsync(lease, pending, checkpoint,
                    layer, changes, append: false, selectionOnly: true);
                if (result.Issues.Count != 0) return result.Issues;
                if (_c2SelectedDecision is null ||
                    !JsonNode.DeepEquals(_c2SelectedDecision.Staged, submission["stagedDecision"]) ||
                    _allocations.Journal.ReadCursor() != JsonNode.Parse(checkpoint.ReadAllocationJournalJson())!.AsArray().Count)
                    return DecisionDraftFailure("spiritual_c2_submission_replay_mismatch").Issues;
                _allocations.Journal.Export();
                await ReadC2SubmissionChangesCoreAsync(lease, checkpoint);
                _c2SelectionVerified = true;
                _c2PendingSubmission = true;
                succeeded = true;
                return [];
            }
            catch (Exception error) when (error is IOException or UnauthorizedAccessException or
                InvalidOperationException or FormatException or JsonException or OverflowException)
            {
                return DecisionDraftFailure("spiritual_c2_submission_replay_failed").Issues;
            }
            finally
            {
                if (!succeeded && IsCurrentOwner) RevokeUnderLease(lease);
                _gate.Release();
            }
        }

        /// <summary>
        /// Reads genuine dependent diagnostics without changing the saved choice or committed source.
        /// </summary>
        /// <param name="lease">
        /// Active lease for the current matched pair and candidate draft.
        /// </param>
        /// <returns>
        /// Current prospective diagnostics; an empty result means continuation can be attempted.
        /// </returns>
        internal async Task<IReadOnlyList<ValidationIssue>> ReadC2DependentIssuesAsync(
            FileSystemManager.CanonicalWriteLease lease)
        {
            await _gate.WaitAsync();
            try
            {
                var checkpoint = await RequireC2PendingSubmissionCoreAsync(lease);
                var changes = await ReadC2SubmissionChangesCoreAsync(lease, checkpoint);
                if (!_allocations.Journal.IsAppendMode) _allocations.Journal.EnableAppendAfterReplay();
                return await ProbeC2SelectedContinuationCoreAsync(lease, changes);
            }
            catch (Exception error) when (error is IOException or UnauthorizedAccessException or
                InvalidOperationException or FormatException or JsonException or OverflowException)
            {
                if (IsCurrentOwner) RevokeUnderLease(lease);
                return DecisionDraftFailure("spiritual_c2_submission_context_changed").Issues;
            }
            finally { _gate.Release(); }
        }

        /// <summary>
        /// Requires the exact replayed selection and both unchanged physical control roots.
        /// </summary>
        /// <param name="lease">
        /// Active lease held with the capture gate.
        /// </param>
        /// <returns>
        /// Parsed current checkpoint; invalid or stale ownership throws before continuation.
        /// </returns>
        private async Task<SpiritualWoundCaptureCheckpointState> RequireC2PendingSubmissionCoreAsync(
            FileSystemManager.CanonicalWriteLease lease)
        {
            EnsureCurrent(lease);
            if (!_c2PendingSubmission || !_c2SelectionVerified || _c2SelectedDecision is null ||
                _matchedC2Pair is not { } pair || _c2DecisionDrafted)
                throw new InvalidOperationException("A current replayed selected decision is required.");
            if ((await CheckRetainedInputsAsync(lease)).Count != 0 ||
                !ExactBytes(await _validator._fs.ReadFileBytesAsync(lease,
                    SpiritualWoundCaptureCheckpointState.StatePath), pair.CheckpointBytes) ||
                !ExactBytes(await _validator._fs.ReadFileBytesAsync(lease,
                    SpiritualWoundDecisionPendingState.StatePath), pair.PendingBytes) ||
                !ExactBytes(await _validator._fs.ReadFileBytesAsync(lease,
                    SpiritualWoundCaptureCheckpointState.StatePath), pair.CheckpointBytes))
                throw new InvalidOperationException("The selected decision's physical pair changed.");
            return SpiritualWoundCaptureCheckpointState.Parse(DecodePhysicalRoot(pair.CheckpointBytes),
                SpiritualWoundCaptureCheckpointState.StatePath, _draftInputs.PathInventory).State!;
        }

        /// <summary>
        /// Derives a successor using only the previously saved decision and bounded physical corrections.
        /// </summary>
        /// <param name="lease">
        /// Active lease held with the capture gate through all reads and execution.
        /// </param>
        /// <param name="dependentPolicy">
        /// Optional additional comparison from the current private-session adapter; source validation remains mandatory.
        /// </param>
        /// <returns>
        /// A successor checkpoint clearing the saved selection, or diagnostics preserving its durable bytes.
        /// </returns>
        private async Task<SpiritualC2DecisionDraftResult> ResumeC2DecisionDraftCoreAsync(
            FileSystemManager.CanonicalWriteLease lease, SpiritualWoundDependentDraftPolicy? dependentPolicy = null)
        {
            try
            {
                var checkpoint = await RequireC2PendingSubmissionCoreAsync(lease);
                if (checkpoint.ReadPendingSubmission()?["dependentDraftProgress"] is JsonArray && dependentPolicy is null)
                    return DecisionDraftFailure("spiritual_c2_dependent_progress_policy_required");
                var changes = await ReadC2SubmissionChangesCoreAsync(lease, checkpoint);
                if (dependentPolicy is not null)
                {
                    var committed = ReadC2CommittedInputLayer(checkpoint);
                    var path = AfterlifeSpiritualConflictState.StatePath;
                    var image = changes.FirstOrDefault(change => change.Path == path).Image ?? committed[path];
                    if (image.Bytes is null || !dependentPolicy.Allows(SpiritualWoundDependentDraftPolicy.ReadStrictRoot(
                            SpiritualWoundStateJson.DecodeUtf8JsonText(image.Bytes))))
                        return DecisionDraftFailure("spiritual_c2_dependent_correction_invalid");
                }
                if (!_allocations.Journal.IsAppendMode) _allocations.Journal.EnableAppendAfterReplay();
                var result = await ContinueC2SelectedDecisionCoreAsync(lease, _matchedC2Pair!.Pending,
                    checkpoint, changes, _c2SelectedDecision!);
                if (result.Checkpoint is null || result.Pending is null || result.Issues.Count != 0)
                    return result with { RequiresDependentContinuation = IsCorrectableDependentConflictFailure(result.Issues) };
                var repeated = await ReadC2SubmissionChangesCoreAsync(lease, checkpoint);
                if (changes.Count != repeated.Count || changes.Where((change, index) =>
                        change.Path != repeated[index].Path || !SameExactImage(change.Image, repeated[index].Image)).Any())
                    return DecisionDraftFailure("spiritual_c2_proposed_input_changed");
                await RequireC2PendingSubmissionCoreAsync(lease);
                _c2DecisionDrafted = true;
                return result;
            }
            catch (Exception error) when (error is IOException or UnauthorizedAccessException or
                InvalidOperationException or FormatException or JsonException or OverflowException)
            {
                return DecisionDraftFailure("spiritual_c2_submission_resume_failed");
            }
            finally
            {
                if (!_c2DecisionDrafted && IsCurrentOwner) RevokeUnderLease(lease);
            }
        }
    }
}
