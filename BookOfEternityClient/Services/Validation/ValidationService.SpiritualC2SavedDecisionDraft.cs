using System.Text;
using System.Text.Json;
using System.Text.Json.Nodes;
using BookOfEternityClient.Core;

namespace BookOfEternityClient.Services;

public partial class ValidationService
{
    /// <summary>
    /// Carries one owner-derived unpublished saved decision and its checkpoint advance.
    /// Neither detached state grants transport or accepted publication authority.
    /// </summary>
    /// <param name="Checkpoint">
    /// Strict appended checkpoint, or <see langword="null"/> on rejection.
    /// </param>
    /// <param name="Pending">
    /// Strict successor decision packet, or <see langword="null"/> on rejection.
    /// </param>
    /// <param name="Issues">
    /// Validation failures; empty after a private selection boundary or a complete successor was rederived.
    /// </param>
    /// <param name="RequiresDependentContinuation">
    /// Whether a later original exchange needs a corrected physical conflict input before retry.
    /// </param>
    internal sealed record SpiritualC2DecisionDraftResult(
        SpiritualWoundCaptureCheckpointState? Checkpoint,
        SpiritualWoundDecisionPendingState? Pending,
        IReadOnlyList<ValidationIssue> Issues,
        bool RequiresDependentContinuation = false);

    internal sealed partial class SpiritualOriginalTurnCapture
    {
        private bool _c2DecisionDrafted;

        /// <summary>
        /// Retains the executed selection inside its original capture until continuation completes.
        /// </summary>
        /// <param name="PacketRoot">
        /// Private packet with exactly one newly staged decision and no later exchange execution.
        /// </param>
        /// <param name="Staged">
        /// Exact owner-derived staged decision row.
        /// </param>
        /// <param name="Interval">
        /// Closed resource interval admitting this selection.
        /// </param>
        /// <param name="CommandBytes">
        /// Exact command bytes used for selection, or <see langword="null"/> for automatic satisfaction.
        /// </param>
        /// <param name="BindingOriginal">
        /// Privately rederived pre-selection legality comparison, or <see langword="null"/> when no last-binding refinement applies.
        /// </param>
        private sealed record C2SelectedDecision(JsonObject PacketRoot, JsonObject Staged,
            AcceptedMechanicsPlanner.SpiritualExchangeInterval Interval, byte[]? CommandBytes,
            OriginalBindingComparison? BindingOriginal);

        private C2SelectedDecision? _c2SelectedDecision;
        private bool _c2SelectionVerified;
        private bool _c2PendingSubmission;

        /// <summary>
        /// Validates one physical GM command at the verified next-source frontier and drafts its successor.
        /// This write-free stage keeps the initial pair unchanged until a separate transport commits it.
        /// </summary>
        /// <param name="lease">
        /// Active canonical lease for the physical pair, registered draft and owner transaction.
        /// </param>
        /// <returns>
        /// Strict detached successor checkpoint and packet, or issues without publication authority.
        /// </returns>
        internal async Task<SpiritualC2DecisionDraftResult> AdvanceC2NextDecisionDraftAsync(
            FileSystemManager.CanonicalWriteLease lease)
        {
            await _gate.WaitAsync();
            try { return await AdvanceC2NextDecisionDraftCoreAsync(lease); }
            finally { _gate.Release(); }
        }

        /// <summary>
        /// Derives one saved decision while the caller retains the capture gate.
        /// </summary>
        /// <param name="lease">
        /// Active canonical lease covering the physical pair and proposed input layer.
        /// </param>
        /// <param name="expectedCommandBytes">
        /// Optional exact private-adapter command bytes to bind to the proposed input layer.
        /// </param>
        /// <param name="selectionOnly">
        /// Stops after verified selection when transport must first save its durable submission.
        /// </param>
        /// <returns>
        /// Strict detached successor or validation issues.
        /// </returns>
        private async Task<SpiritualC2DecisionDraftResult> AdvanceC2NextDecisionDraftCoreAsync(
            FileSystemManager.CanonicalWriteLease lease, byte[]? expectedCommandBytes = null,
            bool selectionOnly = false)
        {
            var attempted = false;
            try
            {
                EnsureCurrent(lease);
                if (_c2DecisionDrafted || _c2SelectionVerified || _matchedC2Pair is null)
                    return DecisionDraftFailure("spiritual_c2_decision_pair_required");
                var next = await ReadC2NextSourceCoreAsync(lease);
                if (next.Source is null || next.Interval is null || next.Issues.Count != 0)
                    return new(null, null, next.Issues);
                var pair = _matchedC2Pair;
                var baseline = SpiritualWoundCaptureCheckpointState.Parse(
                    DecodePhysicalRoot(pair.CheckpointBytes),
                    SpiritualWoundCaptureCheckpointState.StatePath, _draftInputs.PathInventory);
                if (!baseline.IsValid || baseline.State is null)
                    return DecisionDraftFailure("spiritual_c2_checkpoint_baseline_invalid");
                var committed = ReadC2CommittedInputLayer(baseline.State);
                var proposed = new Dictionary<string, CanonicalBeforeImage>(StringComparer.Ordinal);
                var changes = new List<(string Path, CanonicalBeforeImage Image)>();
                foreach (var path in _draftInputs.PathInventory)
                {
                    var bytes = await _validator._fs.ReadFileBytesAsync(lease, path);
                    var image = new CanonicalBeforeImage(bytes is not null, bytes);
                    proposed.Add(path, image);
                    if (SameExactImage(committed[path], image))
                        continue;
                    if (path != AcceptedMechanicsPlan.WoundCommandPath &&
                        path != FixedOriginalOutputPaths[0] &&
                        path != AfterlifeSpiritualConflictState.StatePath)
                        return DecisionDraftFailure("spiritual_c2_input_change_outside_decision_layer");
                    changes.Add((path, image));
                }
                if (expectedCommandBytes is not null &&
                    !ExactBytes(proposed[AcceptedMechanicsPlan.WoundCommandPath].Bytes,
                        expectedCommandBytes))
                    return DecisionDraftFailure("spiritual_c2_submitted_command_changed");
                attempted = true;
                var stagedResult = await ComposeC2DecisionLayerCoreAsync(lease,
                    pair.Pending, baseline.State, proposed, changes, append: true, selectionOnly);
                if (stagedResult.Issues.Count != 0 ||
                    (selectionOnly ? _c2SelectedDecision is null :
                        stagedResult.Checkpoint is null || stagedResult.Pending is null))
                    return stagedResult;
                foreach (var path in _draftInputs.PathInventory)
                {
                    var bytes = await _validator._fs.ReadFileBytesAsync(lease, path);
                    if (!SameExactImage(proposed[path], new CanonicalBeforeImage(bytes is not null, bytes)))
                        return DecisionDraftFailure("spiritual_c2_proposed_input_changed");
                }
                var checkpointNow = await _validator._fs.ReadFileBytesAsync(lease,
                    SpiritualWoundCaptureCheckpointState.StatePath);
                var pendingNow = await _validator._fs.ReadFileBytesAsync(lease,
                    SpiritualWoundDecisionPendingState.StatePath);
                var checkpointAfterPending = await _validator._fs.ReadFileBytesAsync(lease,
                    SpiritualWoundCaptureCheckpointState.StatePath);
                if (!IsCurrentOwner || checkpointNow is null || pendingNow is null ||
                    !pair.CheckpointBytes.AsSpan().SequenceEqual(checkpointNow) ||
                    !pair.PendingBytes.AsSpan().SequenceEqual(pendingNow) ||
                    !pair.CheckpointBytes.AsSpan().SequenceEqual(checkpointAfterPending))
                    return DecisionDraftFailure("spiritual_c2_pair_changed");
                _c2SelectionVerified = selectionOnly;
                _c2DecisionDrafted = !selectionOnly;
                return stagedResult;
            }
            catch (Exception error) when (error is IOException or UnauthorizedAccessException or
                InvalidOperationException or FormatException or JsonException or OverflowException or
                DecoderFallbackException)
            {
                return DecisionDraftFailure("spiritual_c2_decision_draft_failed");
            }
            finally
            {
                if (attempted && !_c2DecisionDrafted && !_c2SelectionVerified && IsCurrentOwner)
                    RevokeUnderLease(lease);
            }
        }

        /// <summary>
        /// Reexecutes one checkpoint input layer under strict saved-allocation replay.
        /// The caller supplies only previously parsed checkpoint bytes and owner-derived prior states.
        /// </summary>
        /// <param name="lease">
        /// Active canonical lease for original owner checks.
        /// </param>
        /// <param name="priorPending">
        /// First or previously replayed owner-derived decision packet.
        /// </param>
        /// <param name="priorCheckpoint">
        /// Owner-matched checkpoint prefix through the preceding step.
        /// </param>
        /// <param name="proposed">
        /// Complete cumulative registered input layer from the saved exact bytes.
        /// </param>
        /// <param name="changes">
        /// Saved exact changed inputs for this step.
        /// </param>
        /// <returns>
        /// Strictly rederived next checkpoint prefix and packet, or validation issues.
        /// </returns>
        internal async Task<SpiritualC2DecisionDraftResult> ReplayC2SavedDecisionLayerAsync(
            FileSystemManager.CanonicalWriteLease lease,
            SpiritualWoundDecisionPendingState priorPending,
            SpiritualWoundCaptureCheckpointState priorCheckpoint,
            IReadOnlyDictionary<string, CanonicalBeforeImage> proposed,
            IReadOnlyList<(string Path, CanonicalBeforeImage Image)> changes)
        {
            await _gate.WaitAsync();
            var attempted = false;
            var succeeded = false;
            try
            {
                EnsureCurrent(lease);
                if (!_usesColdOriginalInputs || _matchedC2Pair is not null ||
                    changes.Any(change => change.Path != AcceptedMechanicsPlan.WoundCommandPath &&
                                          change.Path != FixedOriginalOutputPaths[0] &&
                                          change.Path != AfterlifeSpiritualConflictState.StatePath))
                    return DecisionDraftFailure("spiritual_c2_saved_layer_invalid");
                var inputIssues = await CheckRetainedInputsAsync(lease);
                if (inputIssues.Count != 0)
                    return new(null, null, inputIssues);
                attempted = true;
                var result = await ComposeC2DecisionLayerCoreAsync(lease,
                    priorPending, priorCheckpoint, proposed, changes, append: false);
                succeeded = result.Checkpoint is not null && result.Pending is not null &&
                    result.Issues.Count == 0;
                return result;
            }
            catch (Exception error) when (error is IOException or UnauthorizedAccessException or
                InvalidOperationException or FormatException or JsonException or OverflowException or
                DecoderFallbackException)
            {
                return DecisionDraftFailure("spiritual_c2_saved_layer_replay_failed");
            }
            finally
            {
                if (attempted && !succeeded && IsCurrentOwner)
                    RevokeUnderLease(lease);
                _gate.Release();
            }
        }

        private static bool SameExactImage(CanonicalBeforeImage left, CanonicalBeforeImage right) =>
            left.Existed == right.Existed && (left.Bytes is { } bytes
                ? right.Bytes is { } other && bytes.AsSpan().SequenceEqual(other)
                : right.Bytes is null);

        /// <summary>
        /// Applies only saved exact input layers to the checkpoint's immutable original draft.
        /// </summary>
        /// <param name="checkpoint">
        /// Structurally validated checkpoint carrying the committed continuation rows.
        /// </param>
        /// <returns>
        /// Complete current registered input images, separate from later physical GM edits.
        /// </returns>
        private static Dictionary<string, CanonicalBeforeImage> ReadC2CommittedInputLayer(
            SpiritualWoundCaptureCheckpointState checkpoint)
        {
            var original = checkpoint.ReadOriginalDraftInputs();
            var layer = original.PathInventory.ToDictionary(path => path,
                original.ReadImage, StringComparer.Ordinal);
            var root = SpiritualWoundStateJson.Parse(
                SpiritualWoundCaptureCheckpointState.SerializeCanonical(checkpoint));
            foreach (var advance in root["checkpoint"]!["advances"]!.AsArray())
            foreach (var node in advance!["inputChanges"]!.AsArray())
            {
                var row = node!.AsObject();
                var path = row["path"]!.GetValue<string>();
                var existed = row["existed"]!.GetValue<bool>();
                layer[path] = new CanonicalBeforeImage(existed, existed
                    ? Convert.FromBase64String(row["contentBase64"]!.GetValue<string>()) : null);
            }
            return layer;
        }

        private static void ReplaceCandidateImage(JsonObject packet, string path,
            CanonicalBeforeImage image)
        {
            var row = packet["candidateAfterImages"]!.AsArray()
                .Select(node => node!.AsObject())
                .Single(entry => entry["path"]!.GetValue<string>() == path);
            row["existed"] = image.Existed;
            row["contentBase64"] = image.Bytes is { } bytes ? Convert.ToBase64String(bytes) : null;
            row["contentFingerprint"] = image.Fingerprint;
        }

        private static SpiritualC2DecisionDraftResult DecisionDraftFailure(string code) =>
            new(null, null,
            [
                new ValidationIssue(SpiritualWoundCaptureCheckpointState.StatePath,
                    IssueSeverity.Error, "The saved spiritual decision cannot be derived from its owners.",
                    code: code, section: "AcceptedTurnWoundMaterialization")
            ]);
        /// <summary>
        /// Reexecutes one registered decision layer through source, wound, resource and effect owners.
        /// The caller holds the capture gate and authenticates the prior packet and input layer.
        /// </summary>
        /// <param name="lease">
        /// Active canonical lease for original owner checks.
        /// </param>
        /// <param name="priorPending">
        /// Previously owner-derived packet at the saved decision cursor.
        /// </param>
        /// <param name="priorCheckpoint">
        /// Strict prior checkpoint whose prefix must remain unchanged.
        /// </param>
        /// <param name="proposed">
        /// Complete registered input layer, frozen before any mutation.
        /// </param>
        /// <param name="changes">
        /// Exact changed input images for this single step.
        /// </param>
        /// <param name="append">
        /// Enables fresh values only after a verified live replay; saved replay keeps strict mode.
        /// </param>
        /// <param name="selectionOnly">
        /// Retains the real selected decision before any later exchange when <see langword="true"/>.
        /// </param>
        /// <returns>
        /// Owner-derived successor packet and checkpoint or validation issues.
        /// </returns>
        private async Task<SpiritualC2DecisionDraftResult> ComposeC2DecisionLayerCoreAsync(
            FileSystemManager.CanonicalWriteLease lease,
            SpiritualWoundDecisionPendingState priorPending,
            SpiritualWoundCaptureCheckpointState priorCheckpoint,
            IReadOnlyDictionary<string, CanonicalBeforeImage> proposed,
            IReadOnlyList<(string Path, CanonicalBeforeImage Image)> changes,
            bool append, bool selectionOnly = false)
        {
            if (changes.Any(change => change.Path != AcceptedMechanicsPlan.WoundCommandPath &&
                                      change.Path != FixedOriginalOutputPaths[0] &&
                                      change.Path != AfterlifeSpiritualConflictState.StatePath))
                return DecisionDraftFailure("spiritual_c2_input_change_outside_decision_layer");
            var next = ResolveC2NextSourceFromPending(priorPending);
            if (next.Source is null || next.Interval is null || next.Issues.Count != 0)
                return new(null, null, next.Issues);
            var admission = await WoundSourceAdmission.AdmitCoreAsync(this, lease,
                next.Interval, next.Source);
            if (admission.Admission is null || admission.Issues.Count != 0)
                return new(null, null, admission.Issues);
            var offer = await ReadWoundOpportunityCoreAsync(lease, admission.Admission);
            if (offer.Issues.Count != 0)
                return new(null, null, offer.Issues);
            string decisionKind;
            string opportunityRef;
            byte[]? selectedCommandBytes = null;
            OriginalBindingComparison? bindingOriginal = null;
            WoundMaterializationEnvelope? wound = null;
            byte[]? woundDraft = null;
            string? projectedNarrative = null;
            var narrativeChange = changes.FirstOrDefault(change =>
                change.Path == FixedOriginalOutputPaths[0]);
            if (offer.Satisfaction is { } satisfaction)
            {
                if (changes.Any(change => change.Path == AcceptedMechanicsPlan.WoundCommandPath))
                    return DecisionDraftFailure("spiritual_c2_satisfied_command_changed");
                if (narrativeChange.Path is not null)
                {
                    if (!narrativeChange.Image.Existed || narrativeChange.Image.Bytes is null)
                        return DecisionDraftFailure("spiritual_c2_response_required");
                    var projected = AcceptedTurnOutputProjector.ProjectNarrative(
                        DecodePhysicalRoot(narrativeChange.Image.Bytes));
                    projectedNarrative = projected.Json;
                    if (!ValidC2DependentNarrative(priorPending, projectedNarrative))
                        return DecisionDraftFailure("spiritual_c2_dependent_narrative_invalid");
                }
                decisionKind = "guarantee_satisfied";
                opportunityRef = satisfaction.OpportunityRef;
                if (append)
                    _allocations.Journal.EnableAppendAfterReplay();
            }
            else
            {
                if (offer.Opportunity is null || offer.Binding is null ||
                    proposed[AcceptedMechanicsPlan.WoundCommandPath].Bytes is not { } commandBytes ||
                    !changes.Any(change => change.Path == AcceptedMechanicsPlan.WoundCommandPath))
                    return DecisionDraftFailure("spiritual_c2_decision_command_required");
                using var commandJson = JsonDocument.Parse(DecodePhysicalRoot(commandBytes));
                selectedCommandBytes = (byte[])commandBytes.Clone();
                var parsedCommand = WoundResponseInputComposer.ParseCommandRoot(
                    commandJson.RootElement, [offer.Opportunity]);
                if (!parsedCommand.Success || parsedCommand.Commands.Count != 1)
                    return parsedCommand.Issues.Count != 0
                        ? new(null, null, parsedCommand.Issues)
                        : DecisionDraftFailure("spiritual_c2_single_decision_required");
                var command = parsedCommand.Commands[0];
                var recomposed = WoundResponseInputComposer.Compose(offer.Binding,
                    [offer.Opportunity], [command.Decision], command.FinalSceneText, []);
                if (!recomposed.Success || recomposed.CommandRoot is null ||
                    !JsonNode.DeepEquals(recomposed.CommandRoot, parsedCommand.CommandRoot))
                    return recomposed.Issues.Count != 0
                        ? new(null, null, recomposed.Issues)
                        : DecisionDraftFailure("spiritual_c2_decision_command_mismatch");
                var decisionRoot = SpiritualWoundStateJson.Parse(command.Decision.GetRawText());
                decisionKind = decisionRoot["decision"]?.GetValue<string>() ?? "";
                if (decisionKind is not ("none" or "materialize"))
                    return DecisionDraftFailure("spiritual_c2_decision_kind_invalid");
                if (narrativeChange.Path is not null)
                {
                    if (!narrativeChange.Image.Existed || narrativeChange.Image.Bytes is null)
                        return DecisionDraftFailure("spiritual_c2_response_required");
                    var projected = AcceptedTurnOutputProjector.ProjectNarrative(
                        DecodePhysicalRoot(narrativeChange.Image.Bytes));
                    projectedNarrative = projected.Json;
                    if (!ValidC2DependentNarrative(priorPending, projectedNarrative))
                        return DecisionDraftFailure("spiritual_c2_dependent_narrative_invalid");
                }
                if (decisionKind == "materialize")
                    bindingOriginal = await ReadOriginalBindingComparisonAsync(lease, priorCheckpoint);
                // All old allocations were replayed before a new decision can append.
                if (append)
                    _allocations.Journal.EnableAppendAfterReplay();
                var applied = await WoundSelection.MaterializeCoreAsync(this, lease,
                    admission.Admission, command.Decision, command.FinalSceneText);
                if (applied.Issues.Count != 0)
                    return new(null, null, applied.Issues);
                wound = applied.Wound;
                if (decisionKind == "none" && wound is not null ||
                    decisionKind == "materialize" && wound is null)
                    return DecisionDraftFailure("spiritual_c2_decision_result_mismatch");
                woundDraft = decisionKind == "materialize"
                    ? Encoding.UTF8.GetBytes(command.Decision.GetProperty("proposal").GetRawText())
                    : null;
                opportunityRef = offer.Opportunity.PublicRef;
            }

            var packetRoot = SpiritualWoundStateJson.Parse(
                SpiritualWoundDecisionPendingState.SerializeCanonical(priorPending));
            var packet = packetRoot["pending"]!.AsObject();
            var sourceOrdinal = packet["cursor"]!["nextSourceOrdinal"]!.GetValue<int>();
            var waveOrdinal = packet["cursor"]!["waveOrdinal"]!.GetValue<int>();
            var sourceRow = packet["sources"]![sourceOrdinal]!.AsObject();
            var staged = new JsonObject
            {
                ["opportunityRef"] = opportunityRef,
                ["sourceId"] = sourceRow["sourceId"]!.GetValue<string>(),
                ["decisionFingerprint"] = "",
                ["sourceOrdinal"] = sourceOrdinal,
                ["waveOrdinal"] = waveOrdinal,
                ["decision"] = decisionKind,
                ["selectedSeverityRank"] = wound?.Severity.Rank,
                ["woundDraftBase64"] = woundDraft is null
                    ? null : Convert.ToBase64String(woundDraft),
                ["woundDraftFingerprint"] = woundDraft is null
                    ? null : RawFingerprint(woundDraft)
            };
            if (offer.Satisfaction is { } fulfilled)
            {
                if (fulfilled.InstanceId != packet["conflictInstanceRef"]!.GetValue<string>())
                    return DecisionDraftFailure("spiritual_c2_satisfied_instance_changed");
                staged["satisfiedWoundId"] = fulfilled.WoundId;
                staged["satisfiedSeverityRank"] = fulfilled.SeverityRank;
            }
            staged["decisionFingerprint"] = SpiritualWoundStateJson.Hash(
                staged, "staged_decision", "decisionFingerprint");
            packet["stagedDecisions"]!.AsArray().Add(staged);
            var sourceRows = packet["sources"]!.AsArray();
            var nextOrdinal = sourceOrdinal + 1;
            while (nextOrdinal < sourceRows.Count &&
                   sourceRows[nextOrdinal]!["maximumSeverityRank"]!.GetValue<int>() == 0 &&
                   sourceRows[nextOrdinal]!["guaranteedSeverityRank"] is null)
                nextOrdinal++;
            packet["cursor"]!["nextSourceOrdinal"] = nextOrdinal;

            _c2SelectedDecision = new(packetRoot, staged, next.Interval, selectedCommandBytes, bindingOriginal);
            if (selectionOnly)
                return new(null, null, []);
            return await ContinueC2SelectedDecisionCoreAsync(lease, priorPending,
                priorCheckpoint, changes, _c2SelectedDecision);
        }

        /// <summary>
        /// Continues the exact already executed selection through later source and completion owners.
        /// </summary>
        /// <param name="lease">
        /// Active lease held with the capture gate.
        /// </param>
        /// <param name="priorPending">
        /// Last committed owner-derived packet.
        /// </param>
        /// <param name="priorCheckpoint">
        /// Exact committed checkpoint, optionally carrying this retained selection.
        /// </param>
        /// <param name="changes">
        /// Validated physical or saved input differences from the committed input layer.
        /// </param>
        /// <param name="selection">
        /// Private selection executed by this capture, never a caller-supplied authority.
        /// </param>
        /// <returns>
        /// A complete successor or diagnostics without a committed advance.
        /// </returns>
        private async Task<SpiritualC2DecisionDraftResult> ContinueC2SelectedDecisionCoreAsync(
            FileSystemManager.CanonicalWriteLease lease,
            SpiritualWoundDecisionPendingState priorPending,
            SpiritualWoundCaptureCheckpointState priorCheckpoint,
            IReadOnlyList<(string Path, CanonicalBeforeImage Image)> changes,
            C2SelectedDecision selection)
        {
            if (!ReferenceEquals(selection, _c2SelectedDecision))
                return DecisionDraftFailure("spiritual_c2_selected_owner_mismatch");
            var packetRoot = selection.PacketRoot.DeepClone().AsObject();
            var packet = packetRoot["pending"]!.AsObject();
            var staged = selection.Staged;
            var sourceRows = packet["sources"]!.AsArray();
            var nextOrdinal = packet["cursor"]!["nextSourceOrdinal"]!.GetValue<int>();
            var narrativeChange = changes.FirstOrDefault(change => change.Path == FixedOriginalOutputPaths[0]);
            string? projectedNarrative = null;
            if (narrativeChange.Path is not null)
            {
                if (!narrativeChange.Image.Existed || narrativeChange.Image.Bytes is null)
                    return DecisionDraftFailure("spiritual_c2_response_required");
                projectedNarrative = AcceptedTurnOutputProjector.ProjectNarrative(
                    DecodePhysicalRoot(narrativeChange.Image.Bytes)).Json;
                if (!ValidC2DependentNarrative(priorPending, projectedNarrative))
                    return DecisionDraftFailure("spiritual_c2_dependent_narrative_invalid");
            }

            var conflictChange = changes.FirstOrDefault(change =>
                change.Path == AfterlifeSpiritualConflictState.StatePath);
            AcceptedMechanicsPlanner.SpiritualExchangeInterval? continuedInterval = null;
            if (conflictChange.Path is not null &&
                (nextOrdinal != sourceRows.Count || !conflictChange.Image.Existed))
                return DecisionDraftFailure("spiritual_c2_conflict_continuation_before_source_resolution");
            if (nextOrdinal == sourceRows.Count)
            {
                if (!_source.TryReadInitialActiveExchangeInventory(out _,
                        out var originalExchangeIds) ||
                    _closedExchangeEvidence.Count > originalExchangeIds.Length)
                    return DecisionDraftFailure("spiritual_c2_original_exchange_inventory_invalid");
                var correction = conflictChange.Path is null
                    ? null
                    : new Dictionary<string, CanonicalBeforeImage>(StringComparer.Ordinal)
                    {
                        [AfterlifeSpiritualConflictState.StatePath] = conflictChange.Image
                    };
                if (_closedExchangeEvidence.Count == originalExchangeIds.Length &&
                    correction is not null)
                    return DecisionDraftFailure("spiritual_c2_conflict_continuation_not_needed");
                while (_closedExchangeEvidence.Count < originalExchangeIds.Length)
                {
                    var continued = await AdvanceNextResourceExchangeUnderGateAsync(lease,
                        correction);
                    correction = null;
                    if (continued.Step?.Interval is not { } interval ||
                        continued.Issues.Count != 0)
                        return continued.Issues.Count != 0
                            ? new(null, null, continued.Issues,
                                RequiresDependentContinuation: conflictChange.Path is null &&
                                    IsCorrectableDependentConflictFailure(continued.Issues))
                            : DecisionDraftFailure("spiritual_c2_conflict_continuation_incomplete");
                    continuedInterval = interval;
                    if (!AppendC2NextExchangeEvidence(packet))
                        return DecisionDraftFailure("spiritual_c2_conflict_evidence_mismatch");
                    sourceRows = packet["sources"]!.AsArray();
                    if (packet["cursor"]!["nextSourceOrdinal"]!.GetValue<int>() <
                        sourceRows.Count)
                        break;
                }
                if (packet["cursor"]!["nextSourceOrdinal"]!.GetValue<int>() ==
                    sourceRows.Count)
                {
                    var completionIssues = await _source.CheckCompletionInputsAsync(
                        lease, _nextResourceOrdinal, _resources!, _effects!);
                    if (completionIssues.Count != 0)
                        return new(null, null, completionIssues);
                }
            }

            var composedImages = await ReadC1CandidateOwnerImagesCoreAsync(lease,
                continuedInterval ?? selection.Interval);
            if (composedImages.Images is not { } ownerImages)
                return new(null, null, composedImages.Issues);
            foreach (var pair in ownerImages)
            {
                if (pair.Key == FixedOriginalOutputPaths[0] &&
                    narrativeChange.Path is null)
                    continue;
                ReplaceCandidateImage(packet, pair.Key, pair.Value);
            }
            foreach (var change in changes)
            {
                // The raw correction is replay input, not the executed conflict candidate.
                if (change.Path == AfterlifeSpiritualConflictState.StatePath)
                    continue;
                if (change.Path == FixedOriginalOutputPaths[0])
                {
                    ReplaceCandidateImage(packet, change.Path,
                        new CanonicalBeforeImage(true, Encoding.UTF8.GetBytes(projectedNarrative!)));
                    packet["preservedDraft"] = new JsonObject
                    {
                        ["contentBase64"] = Convert.ToBase64String(change.Image.Bytes!),
                        ["contentFingerprint"] = RawFingerprint(change.Image.Bytes!)
                    };
                }
                else
                    ReplaceCandidateImage(packet, change.Path, change.Image);
            }
            packet["retainedPrefixFingerprint"] =
                SpiritualWoundDecisionPendingState.ComputePrefixFingerprint(packet);
            packet["packetFingerprint"] = SpiritualWoundStateJson.Hash(
                packet, "pending_packet", "packetFingerprint");
            var pendingParsed = SpiritualWoundDecisionPendingState.Parse(packetRoot.ToJsonString(),
                SpiritualWoundDecisionPendingState.StatePath, ReadC1ImageInventoryCore().RegisteredPaths);
            if (!pendingParsed.IsValid || pendingParsed.State is null ||
                SpiritualWoundDecisionPendingState.PlanAdvance(priorPending,
                    pendingParsed.State).Disposition != "advanced")
                return pendingParsed.Issues.Count != 0
                    ? new(null, null, pendingParsed.Issues)
                    : DecisionDraftFailure("spiritual_c2_decision_packet_invalid");
            if (packet["cursor"]!["nextSourceOrdinal"]!.GetValue<int>() == sourceRows.Count)
            {
                var completed = await CompleteOrdinaryReductionUnderGateAsync(lease);
                if (!completed.Success || completed.Reduction is null)
                    return new(null, null, completed.Issues);
            }
            var checkpointRoot = SpiritualWoundStateJson.Parse(
                SpiritualWoundCaptureCheckpointState.SerializeCanonical(priorCheckpoint));
            var checkpoint = checkpointRoot["checkpoint"]!.AsObject();
            checkpoint.Remove("pendingSubmission");
            var inputRows = new JsonArray();
            foreach (var change in changes.OrderBy(change => change.Path, StringComparer.Ordinal))
                inputRows.Add(new JsonObject
                {
                    ["path"] = change.Path,
                    ["existed"] = change.Image.Existed,
                    ["contentBase64"] = change.Image.Bytes is { } bytes
                        ? Convert.ToBase64String(bytes) : null,
                    ["contentFingerprint"] = change.Image.Fingerprint
                });
            checkpoint["advances"]!.AsArray().Add(new JsonObject
            {
                ["ordinal"] = priorCheckpoint.CommittedAdvance + 1,
                ["priorPendingPacketFingerprint"] = priorPending.PacketFingerprint,
                ["inputChanges"] = inputRows,
                ["newDecisionFingerprints"] = new JsonArray(
                    JsonValue.Create(staged["decisionFingerprint"]!.GetValue<string>())),
                ["allocationCount"] = _allocations.Journal.ReadCursor(),
                ["resultPendingPacketFingerprint"] = pendingParsed.State.PacketFingerprint
            });
            checkpoint["committedAdvance"] = priorCheckpoint.CommittedAdvance + 1;
            checkpoint["allocations"] = _allocations.Journal.ExportConsumedPrefix();
            checkpoint["expectedPendingPacketFingerprint"] = pendingParsed.State.PacketFingerprint;
            checkpoint["checkpointFingerprint"] = SpiritualWoundStateJson.Hash(
                checkpoint, "spiritual_capture_checkpoint_v1", "checkpointFingerprint");
            var checkpointParsed = SpiritualWoundCaptureCheckpointState.Parse(
                checkpointRoot.ToJsonString(), SpiritualWoundCaptureCheckpointState.StatePath,
                _draftInputs.PathInventory);
            if (!checkpointParsed.IsValid || checkpointParsed.State is null ||
                SpiritualWoundCaptureCheckpointState.PlanAdvance(priorCheckpoint,
                    checkpointParsed.State).Disposition != "advanced")
                return checkpointParsed.Issues.Count != 0
                    ? new(null, null, checkpointParsed.Issues)
                    : DecisionDraftFailure("spiritual_c2_checkpoint_advance_invalid");
            return new(checkpointParsed.State, pendingParsed.State, []);
        }

        /// <summary>
        /// Recognizes only validator diagnostics for the editable later conflict exchange.
        /// Immutable witness, resource-owner and freshness failures remain blocked.
        /// </summary>
        /// <param name="issues">
        /// Owner-returned validation issues from the attempted next exchange.
        /// </param>
        /// <returns>
        /// <see langword="true"/> only when every issue targets a correctable conflict exchange field.
        /// </returns>
        internal static bool IsCorrectableDependentConflictFailure(
            IReadOnlyList<ValidationIssue> issues) =>
            issues.Count != 0 && issues.All(issue =>
                issue.FilePath.StartsWith("activeConflict.exchangeLog[", StringComparison.Ordinal) &&
                (PositionDependencyIndex(issue) is not null || BindingDependencyIndex(issue) is not null || issue.Code switch
                {
                    "afterlife_conflict_wound_force_cost_audit_missing" or
                    "afterlife_conflict_wound_force_cost_audit_obsolete" =>
                        issue.FilePath.EndsWith(".actionCostAudit.player", StringComparison.Ordinal) ||
                        issue.FilePath.EndsWith(".actionCostAudit.opposition", StringComparison.Ordinal),
                    "afterlife_conflict_action_recovery_delta_mismatch" =>
                        issue.FilePath.EndsWith(".actionCostAudit.player.after", StringComparison.Ordinal) ||
                        issue.FilePath.EndsWith(".actionCostAudit.opposition.after", StringComparison.Ordinal),
                    "afterlife_conflict_action_cost_mismatch" or
                    "afterlife_conflict_special_art_cost_mismatch" =>
                        issue.FilePath.EndsWith(".actionCostAudit.player.effectiveCost",
                            StringComparison.Ordinal) && SameFrozenCostEvidence(issue),
                    "afterlife_conflict_opposition_action_cost_mismatch" or
                    "afterlife_conflict_opposition_special_art_cost_mismatch" =>
                        issue.FilePath.EndsWith(".actionCostAudit.opposition.effectiveCost",
                            StringComparison.Ordinal) && SameFrozenCostEvidence(issue),
                    "afterlife_conflict_action_cost_delta_mismatch" =>
                        issue.FilePath.EndsWith(".actionCostAudit.player.after",
                            StringComparison.Ordinal),
                    "afterlife_conflict_opposition_action_cost_delta_mismatch" =>
                        issue.FilePath.EndsWith(".actionCostAudit.opposition.after",
                            StringComparison.Ordinal),
                    "afterlife_conflict_action_cost_sequence_mismatch" =>
                        issue.FilePath.EndsWith(".actionCostAudit.player.before",
                            StringComparison.Ordinal) ||
                        issue.FilePath.EndsWith(".actionCostAudit.opposition.before",
                            StringComparison.Ordinal),
                    "spiritual_wound_tempo_gain_forbidden" =>
                        issue.FilePath.EndsWith(".after.tempoAdvantage",
                            StringComparison.Ordinal),
                    "spiritual_wound_tempo_frontier_mismatch" =>
                        issue.FilePath.EndsWith(".before.tempoAdvantage",
                            StringComparison.Ordinal),
                    "spiritual_wound_roll_hindrance_missing" =>
                        issue.FilePath.Contains(".diceAudit.rollMode.", StringComparison.Ordinal),
                    _ => false
                }));

        /// <summary>
        /// Confirms an effective-cost diagnostic leaves the immutable base and minimum costs intact.
        /// </summary>
        /// <param name="issue">
        /// Ordinary conflict cost diagnostic carrying expected and submitted formula values.
        /// </param>
        /// <returns>
        /// <see langword="true"/> only when the submitted base and minimum equal owner expectations.
        /// </returns>
        private static bool SameFrozenCostEvidence(ValidationIssue issue)
        {
            const string suffix = ", effectiveCost=";
            if (issue.Expected is not { } expected || issue.Actual is not { } actual)
                return false;
            var expectedEnd = expected.IndexOf(suffix, StringComparison.Ordinal);
            var actualEnd = actual.IndexOf(suffix, StringComparison.Ordinal);
            return expectedEnd > 0 && actualEnd == expectedEnd &&
                   string.Equals(expected[..expectedEnd], actual[..actualEnd],
                       StringComparison.Ordinal);
        }

        /// <summary>
        /// Checks a corrected response against the ordinary narrative shape and preserved siblings.
        /// </summary>
        /// <param name="priorPending">
        /// Owner-derived packet retaining the preceding exact response draft.
        /// </param>
        /// <param name="projectedNarrative">
        /// Corrected narrative after the ordinary line-break projection.
        /// </param>
        /// <returns>
        /// <see langword="true"/> only when the response and timestamp are valid and independent fields remain exact.
        /// </returns>
        private bool ValidC2DependentNarrative(SpiritualWoundDecisionPendingState priorPending,
            string projectedNarrative)
        {
            var priorPacket = SpiritualWoundStateJson.Parse(
                SpiritualWoundDecisionPendingState.SerializeCanonical(priorPending))["pending"]!.AsObject();
            var originalBytes = Convert.FromBase64String(
                priorPacket["preservedDraft"]!["contentBase64"]!.GetValue<string>());
            var original = SpiritualWoundStateJson.Parse(DecodePhysicalRoot(originalBytes));
            var proposed = SpiritualWoundStateJson.Parse(projectedNarrative);
            if (proposed["response"] is not JsonValue responseValue ||
                !responseValue.TryGetValue<string>(out var response) ||
                string.IsNullOrWhiteSpace(response) ||
                proposed.Any(pair => pair.Key is not ("response" or "timestamp") &&
                    !pair.Key.StartsWith("_", StringComparison.OrdinalIgnoreCase)) ||
                original.Any(pair => pair.Key is not ("response" or "timestamp") &&
                    (!proposed.ContainsKey(pair.Key) ||
                     !JsonNode.DeepEquals(pair.Value, proposed[pair.Key]))) ||
                proposed.Any(pair => pair.Key is not ("response" or "timestamp") &&
                    !original.ContainsKey(pair.Key)))
                return false;
            using var document = JsonDocument.Parse(projectedNarrative);
            var issues = new List<ValidationIssue>();
            _validator.ValidateRequiredIsoTimestampField(document.RootElement,
                FixedOriginalOutputPaths[0], issues, "timestamp", "Narrative",
                "narrative_response_missing_timestamp", "narrative_response_invalid_timestamp",
                "Add a valid ISO 8601 timestamp with the corrected response.");
            return issues.Count == 0 &&
                !TryFindTechnicalRepairLeakInNarrative(response, out _);
        }

        /// <summary>
        /// Extends a saved decision packet with the newly closed owner exchange evidence.
        /// </summary>
        /// <param name="packet">
        /// Mutable successor packet whose earlier rows must match the reconstructed owner prefix.
        /// </param>
        /// <returns>
        /// <see langword="true"/> if the old source and die rows remain an exact prefix.
        /// </returns>
        private bool AppendC2NextExchangeEvidence(JsonObject packet)
        {
            var inventory = ReadC1ImageInventoryCore();
            var originalDice = _source.ReadOriginalAcceptedD20Values();
            var responseBytes = Convert.FromBase64String(
                packet["preservedDraft"]!["contentBase64"]!.GetValue<string>());
            var rebuilt = ComposeFirstOfferRoot(
                packet["conflictInstanceRef"]!.GetValue<string>(),
                packet["originalSnapshotFingerprint"]!.GetValue<string>(),
                packet["bounds"]!["exchangeCount"]!.GetValue<int>(), originalDice,
                inventory.RegisteredPaths, inventory.BeforeImages, inventory.BeforeImages,
                responseBytes)["pending"]!.AsObject();
            foreach (var field in new[] { "sources", "diceClaims" })
            {
                var oldRows = packet[field]!.AsArray();
                var rebuiltRows = rebuilt[field]!.AsArray();
                if (oldRows.Count > rebuiltRows.Count || oldRows.Where((row, index) =>
                    !JsonNode.DeepEquals(row, rebuiltRows[index])).Any())
                    return false;
                packet[field] = rebuiltRows.DeepClone();
            }
            packet["cursor"]!["exchangeOrdinal"] = _closedExchangeEvidence.Count;
            var oldCount = packet["cursor"]!["nextSourceOrdinal"]!.GetValue<int>();
            var sources = packet["sources"]!.AsArray();
            var nextSource = oldCount;
            while (nextSource < sources.Count &&
                   sources[nextSource]!["maximumSeverityRank"]!.GetValue<int>() == 0 &&
                   sources[nextSource]!["guaranteedSeverityRank"] is null)
                nextSource++;
            packet["cursor"]!["nextSourceOrdinal"] = nextSource;
            if (nextSource < sources.Count)
            {
                packet["continuationGeneration"] =
                    packet["continuationGeneration"]!.GetValue<int>() + 1;
                packet["cursor"]!["waveOrdinal"] =
                    packet["cursor"]!["waveOrdinal"]!.GetValue<int>() + 1;
            }
            return true;
        }
    }
}
