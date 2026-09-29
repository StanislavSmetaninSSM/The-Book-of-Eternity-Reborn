using System.Text.Json;
using System.Text.Json.Nodes;
using BookOfEternityClient.Core;

namespace BookOfEternityClient.Services;

public partial class ValidationService
{
    /// <summary>
    /// Retains validated spiritual publication authority or prepares it from an authenticated completed checkpoint.
    /// </summary>
    /// <param name="lease">
    /// Active lease for this validator's canonical filesystem. Preparing an unregistered checkpoint requires
    /// physical snapshot reads without a prevalidated snapshot override; validating retained authority does not replay intake.
    /// </param>
    /// <returns>
    /// <see langword="null"/> when no spiritual checkpoint exists; otherwise the handoff diagnostics.
    /// Callers requiring publication authority must also verify its registration before admitting the turn.
    /// </returns>
    internal async Task<IReadOnlyList<ValidationIssue>?> TryPrepareSpiritualC4PublicationAsync(
        FileSystemManager.CanonicalWriteLease lease)
    {
        if (AcceptedMechanicsPlanAuthority.TryPeekSpiritualPublication(_fs, lease, out var retained))
        {
            var retainedIssues = await retained.ValidateCurrentInputsAsync(_fs, lease);
            if (retainedIssues.Count != 0)
                AcceptedMechanicsPlanAuthority.InvalidateValidated(_fs, lease);
            return retainedIssues;
        }
        var checkpoint = await _fs.ReadFileBytesAsync(lease, SpiritualWoundCaptureCheckpointState.StatePath);
        if (checkpoint is null &&
            await _fs.ReadFileBytesAsync(lease, SpiritualWoundDecisionPendingState.StatePath) is null)
            return null;
        var classified = await ClassifySpiritualPendingAsync(lease);
        using var capture = classified.Capture;
        if (classified.Disposition == "no_checkpoint")
            return null;
        if (classified.Disposition != "match" || capture is null)
            return classified.Issues.Count != 0 ? classified.Issues :
                [SpiritualOriginalTurnCapture.C4Issue("spiritual_c4_checkpoint_incomplete")];
        var prepared = await capture.PrepareC4PublicationAsync(lease);
        if (prepared.Authority is null)
            return prepared.Issues;
        var authority = prepared.Authority;
        var issues = await authority.ValidateCurrentInputsAsync(_fs, lease);
        if (issues.Count != 0)
            return issues;
        if (!AcceptedMechanicsPlanAuthority.RegisterSpiritualPublication(_fs, lease, authority))
            return [SpiritualOriginalTurnCapture.C4Issue("spiritual_c4_registration_rejected")];
        return Array.Empty<ValidationIssue>();
    }

    internal sealed partial class SpiritualOriginalTurnCapture
    {
        private static readonly object C4IssuanceKey = new();
        private bool _c4OwnershipTransferred;

        /// <summary>
        /// Transfers one completed C3 result into a detached, filesystem-bound publication capability.
        /// </summary>
        /// <param name="lease">
        /// Active lease reauthenticating the matched pair or source-only origin, current inputs and signed rollback bytes.
        /// </param>
        /// <returns>
        /// The sole transferred authority, or diagnostics without a publishable capability.
        /// </returns>
        internal async Task<(SpiritualC4PublicationAuthority? Authority, IReadOnlyList<ValidationIssue> Issues)>
            PrepareC4PublicationAsync(FileSystemManager.CanonicalWriteLease lease)
        {
            var reduced = _matchedC2Pair is null
                ? await ReduceCompletedSourceOnlyAsync(lease)
                : await ReduceCompletedDecisionsCoreAsync(lease, requireDeclines: false);
            if (!reduced.Success || reduced.Reduction is null)
                return (null, reduced.Issues);
            await _gate.WaitAsync();
            try
            {
                EnsureCurrent(lease);
                if (_c4OwnershipTransferred ||
                    _completedOrdinaryReduction?.Reduction is null)
                    return (null, [C4Issue("spiritual_c4_completion_required")]);
                var publication = _observed.ToDictionary(pair => pair.Key, pair => pair.Value,
                    StringComparer.Ordinal);
                foreach (var pair in _physicalWitnesses)
                    publication[pair.Key] = pair.Value;
                if (_matchedC2Pair is { } matched)
                {
                    var parsed = SpiritualWoundCaptureCheckpointState.Parse(
                        DecodePhysicalRoot(matched.CheckpointBytes),
                        SpiritualWoundCaptureCheckpointState.StatePath, _draftInputs.PathInventory);
                    if (!parsed.IsValid || parsed.State is null)
                        return (null, parsed.Issues);
                    foreach (var pair in ReadC2CommittedInputLayer(parsed.State))
                        publication[pair.Key] = pair.Value;
                    publication[SpiritualWoundCaptureCheckpointState.StatePath] =
                        new CanonicalBeforeImage(true, matched.CheckpointBytes);
                    publication[SpiritualWoundDecisionPendingState.StatePath] =
                        new CanonicalBeforeImage(true, matched.PendingBytes);
                }
                else
                    foreach (var path in _draftInputs.PathInventory)
                        publication[path] = _draftInputs.ReadImage(path);
                var planned = AcceptedMechanicsPlanner.CompleteAcceptedReduction(
                    AcceptedMechanicsPlanner.AcceptedMechanicsReduction.CompletedOrdinary(
                        reduced.Reduction, reduced.Reduction.Resources));
                if (!planned.Success || planned.Plan is null)
                    return (null, planned.Issues);
                var lookup = await _validator.LoadValidatedPendingTurnSnapshotLookupAsync();
                if (lookup.Status != ValidatedPendingTurnSnapshotStatus.Usable ||
                    lookup.Manifest is not { } manifest || manifest.SessionId != SessionId ||
                    manifest.RequestId != RequestId || manifest.ManifestPayloadHash != _input.SnapshotToken)
                    return (null, [C4Issue("spiritual_c4_original_snapshot_changed")]);
                var footprint = CanonicalStateNormalizer.NormalizerRollbackTrackedFiles
                    .Concat(planned.Plan.TouchedPaths).Concat(planned.Plan.ConsumedPaths)
                    .Concat(FixedOriginalOutputPaths).Distinct(StringComparer.Ordinal).ToArray();
                if (!PendingTurnSnapshotAuthority.HasValidatedRollbackSnapshotCoverage(manifest,
                        static value => value.Files, static value => value.SnapshotFileHashes,
                        static value => value.RollbackBaselineFiles, footprint, out var missing))
                    return (null, [C4Issue("spiritual_c4_rollback_coverage_missing", missing)]);
                var rollback = footprint.ToDictionary(path => path,
                    _ => new CanonicalBeforeImage(false, null), StringComparer.Ordinal);
                var present = footprint.Where(path => manifest.Files.ContainsKey(path)).ToArray();
                foreach (var batch in present.Chunk(48))
                {
                    var read = PendingTurnSnapshotReader.ReadCurrent(_validator._fs, lease, batch);
                    if (!read.Success || read.Snapshot is not { } signed ||
                        signed.SnapshotToken != _input.SnapshotToken)
                        return (null, read.Issues.Count != 0 ? read.Issues :
                            [C4Issue("spiritual_c4_original_snapshot_changed")]);
                    foreach (var path in batch)
                    {
                        var image = new CanonicalBeforeImage(true, signed.ReadRequiredBytes(path));
                        rollback[path] = image;
                        publication[manifest.Files[path]] = image;
                    }
                }
                foreach (var path in footprint.Where(path => !publication.ContainsKey(path)))
                {
                    publication[path] = rollback[path];
                }
                if (_allocations.Items is not { } itemFactory ||
                    !AcceptedTurnAuthorityRegistry.TryCaptureSpiritualMortalItemNormalizationSnapshot(
                        _validator._fs, lease, this, _input.SessionId, _input.SnapshotToken, _input.Turn,
                        itemFactory, out var itemSnapshot) ||
                    !itemSnapshot.MatchesAcceptedOwnerAuthority(planned.Plan.OwnerAuthority))
                    return (null, [C4Issue("spiritual_c4_item_snapshot_unavailable")]);
                MortalLocationAcceptedTurnPlan? locationPlan = null;
                if (HasC4OriginalLocationPlan() &&
                    (_allocations.Locations is not { } locationFactory ||
                     !MortalLocationAcceptedTurnPlanAuthority.TryCaptureSpiritualCompleted(
                         _validator._fs, lease, this, locationFactory, out locationPlan)))
                    return (null, [C4Issue("spiritual_c4_location_plan_unavailable")]);
                var authority = new SpiritualC4PublicationAuthority(C4IssuanceKey, _validator._fs,
                    reduced.Reduction.Input, planned.Plan, publication, rollback, itemSnapshot, locationPlan, reduced.Reduction.LiveWoundCompletion,
                    new SpiritualCompletedConflictValidation(C4IssuanceKey, this, planned.Plan));
                var currentIssues = await authority.ValidateCurrentInputsAsync(_validator._fs, lease);
                if (currentIssues.Count != 0)
                    return (null, currentIssues);
                _c4OwnershipTransferred = true;
                // Only the immutable completed result transfers. Disposal still revokes every
                // execution owner and its allocation journal; publication cannot execute them.
                return (authority, Array.Empty<ValidationIssue>());
            }
            catch (Exception error) when (error is IOException or UnauthorizedAccessException or
                InvalidOperationException or FormatException or JsonException)
            {
                return (null, [C4Issue("spiritual_c4_publication_unavailable")]);
            }
            finally { _gate.Release(); }
        }

        /// <summary>
        /// Checks that a registry snapshot request belongs to this live original capture and factory.
        /// </summary>
        /// <param name="fileSystem">
        /// Exact filesystem retained by this capture.
        /// </param>
        /// <param name="lease">
        /// Current canonical lease proving the capture remains usable.
        /// </param>
        /// <param name="sessionId">
        /// Exact original session identity.
        /// </param>
        /// <param name="snapshotToken">
        /// Exact authenticated original snapshot token.
        /// </param>
        /// <param name="turn">
        /// Original accepted turn number.
        /// </param>
        /// <param name="factory">
        /// Exact factory owned by this capture's allocation journal.
        /// </param>
        /// <returns>
        /// True only before ownership transfer for this live original owner and its exact tuple.
        /// </returns>
        internal bool AuthorizesC4ItemSnapshot(FileSystemManager fileSystem,
            FileSystemManager.CanonicalWriteLease lease, string sessionId, string snapshotToken,
            int turn, MortalItemIdentityFactory factory)
        {
            EnsureCurrent(lease);
            return !_c4OwnershipTransferred && ReferenceEquals(fileSystem, _validator._fs) &&
                ReferenceEquals(factory, _allocations.Items) && sessionId == _input.SessionId &&
                snapshotToken == _input.SnapshotToken && turn == _input.Turn;
        }

        /// <summary>
        /// Checks whether the original admitted draft required a location plan.
        /// </summary>
        /// <returns>
        /// True for original location commands or a bootstrap materialization request.
        /// </returns>
        private bool HasC4OriginalLocationPlan()
        {
            foreach (var (path, field) in new[]
            {
                (MortalLocationMaterializationContract.WorldMapPath, "worldMapUpdates"),
                (MortalLocationMaterializationContract.CurrentLocationPath, "currentLocationData"),
                (MortalBootstrapLocationScaffold.StatePath, "locationMaterializationRequest")
            })
            {
                var image = _draftInputs.ReadImage(path);
                if (image.Bytes is not null && JsonNode.Parse(image.Bytes)?[field] is JsonObject)
                    return true;
            }
            return false;
        }

        /// <summary>
        /// Checks that completed location extraction belongs to the live original allocation owner.
        /// </summary>
        /// <param name="fileSystem">
        /// Exact filesystem retained by the capture.
        /// </param>
        /// <param name="lease">
        /// Active canonical lease for this original capture.
        /// </param>
        /// <param name="factory">
        /// Exact private location factory owned by this capture's allocation journal.
        /// </param>
        /// <returns>
        /// True only before transfer for the exact live original owner and its location factory.
        /// </returns>
        internal bool AuthorizesC4LocationSnapshot(FileSystemManager fileSystem,
            FileSystemManager.CanonicalWriteLease lease, MortalLocationIdentityFactory factory)
        {
            EnsureCurrent(lease);
            return !_c4OwnershipTransferred && ReferenceEquals(fileSystem, _validator._fs) &&
                ReferenceEquals(factory, _allocations.Locations);
        }

        /// <summary>
        /// Creates one bounded private-publication diagnostic.
        /// </summary>
        /// <param name="code">
        /// Stable failure code.
        /// </param>
        /// <param name="path">
        /// Changed authority path, or null for the private checkpoint.
        /// </param>
        /// <returns>
        /// An error that grants no publication authority.
        /// </returns>
        internal static ValidationIssue C4Issue(string code, string? path = null) =>
            new(path ?? SpiritualWoundCaptureCheckpointState.StatePath, IssueSeverity.Error,
                "The completed spiritual publication is unavailable.", code: code,
                section: "AcceptedTurnWoundMaterialization");
    }
}
