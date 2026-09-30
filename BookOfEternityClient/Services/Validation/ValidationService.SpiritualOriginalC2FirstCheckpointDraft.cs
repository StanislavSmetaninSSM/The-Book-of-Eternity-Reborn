using System.Text.Json;
using System.Text.Json.Nodes;
using BookOfEternityClient.Core;

namespace BookOfEternityClient.Services;

public partial class ValidationService
{
    /// <summary>
    /// Carries a detached initial checkpoint and its exact unpublished first packet.
    /// This result grants neither checkpoint persistence nor accepted-turn publication.
    /// </summary>
    /// <param name="Checkpoint">
    /// Strict initial checkpoint, or <see langword="null"/> for no offer or failure.
    /// </param>
    /// <param name="Pending">
    /// First strict C1 packet derived in the same capture gate, or <see langword="null"/>.
    /// </param>
    /// <param name="ProposedInstanceRow">
    /// Detached unpublished instance start row, or <see langword="null"/> for a reused instance.
    /// </param>
    /// <param name="Issues">
    /// Validation failures; empty for a valid draft or no eligible offer.
    /// </param>
    internal sealed record SpiritualC2FirstCheckpointDraftResult(
        SpiritualWoundCaptureCheckpointState? Checkpoint,
        SpiritualWoundDecisionPendingState? Pending, JsonObject? ProposedInstanceRow,
        IReadOnlyList<ValidationIssue> Issues);

    internal sealed partial class SpiritualOriginalTurnCapture
    {
        /// <summary>
        /// Builds an initial checkpoint from actual first-offer, draft and allocation owners.
        /// </summary>
        /// <param name="lease">
        /// Active canonical lease authenticating the current capture and physical witnesses.
        /// </param>
        /// <param name="interval">
        /// Exact latest closed interval that may produce the first offer.
        /// </param>
        /// <returns>
        /// Detached strict checkpoint and packet, no offer, or issues; no files are written.
        /// </returns>
        internal async Task<SpiritualC2FirstCheckpointDraftResult> ComposeC2FirstCheckpointDraftAsync(
            FileSystemManager.CanonicalWriteLease lease,
            AcceptedMechanicsPlanner.SpiritualExchangeInterval interval)
        {
            ArgumentNullException.ThrowIfNull(interval);
            await _gate.WaitAsync();
            try { return await ComposeC2FirstCheckpointDraftCoreAsync(lease, interval); }
            finally { _gate.Release(); }
        }

        /// <summary>
        /// Derives the first checkpoint while its caller already holds the capture gate.
        /// </summary>
        /// <param name="lease">
        /// Active canonical lease authenticating the original owner and witnesses.
        /// </param>
        /// <param name="interval">
        /// Exact latest closed interval of the retained capture.
        /// </param>
        /// <returns>
        /// Strict detached checkpoint and first packet, no offer, or issues.
        /// </returns>
        private async Task<SpiritualC2FirstCheckpointDraftResult> ComposeC2FirstCheckpointDraftCoreAsync(
            FileSystemManager.CanonicalWriteLease lease,
            AcceptedMechanicsPlanner.SpiritualExchangeInterval interval)
        {
                EnsureCurrent(lease);
                if (_usesColdOriginalInputs || _coldViewRevision != 0 ||
                    _originalPrefixReceipts.Count != 0 || _consumedContinuations.Count != 0)
                    return FirstCheckpointFailure("spiritual_first_checkpoint_prior_continuation");
                var originalConflict = _draftInputs.ReadImage(
                    AfterlifeSpiritualConflictState.StatePath);
                var currentConflict = await _validator._fs.ReadFileBytesAsync(lease,
                    AfterlifeSpiritualConflictState.StatePath);
                if (originalConflict.Fingerprint !=
                    new CanonicalBeforeImage(currentConflict is not null, currentConflict).Fingerprint)
                    return FirstCheckpointFailure("spiritual_first_checkpoint_conflict_changed");
                var first = await ComposeC1FirstOfferCoreAsync(lease, interval);
                if (first.Pending is null || first.Issues.Count != 0)
                    return new(null, null, null, first.Issues);
                try
                {
                    var pendingRoot = SpiritualWoundStateJson.Parse(
                        SpiritualWoundDecisionPendingState.SerializeCanonical(first.Pending));
                    var packetFingerprint = pendingRoot["pending"]!["packetFingerprint"]!
                        .GetValue<string>();
                    var draftRows = new JsonArray();
                    foreach (var path in _draftInputs.PathInventory.OrderBy(path => path, StringComparer.Ordinal))
                    {
                        var image = _draftInputs.ReadImage(path);
                        draftRows.Add(new JsonObject
                        {
                            ["path"] = path,
                            ["existed"] = image.Existed,
                            ["contentBase64"] = image.Bytes is null ? null : Convert.ToBase64String(image.Bytes),
                            ["contentFingerprint"] = image.Fingerprint
                        });
                    }
                    var witnesses = new JsonArray();
                    foreach (var path in new[]
                        {
                            PendingTurnSnapshotAuthority.AuthorityPath,
                            LiveTurnPreparationService.PendingTurnSnapshotManifestPath,
                            LiveTurnPreparationService.TurnRequestPath
                        })
                    {
                        if (!_physicalWitnesses.TryGetValue(path, out var image) || !image.Existed)
                            return FirstCheckpointFailure("spiritual_first_checkpoint_witness_missing");
                        witnesses.Add(new JsonObject
                        {
                            ["path"] = path,
                            ["existed"] = true,
                            ["contentFingerprint"] = image.Fingerprint
                        });
                    }
                    var allocations = _allocations.Journal.Export();
                    var body = new JsonObject
                    {
                        ["sessionId"] = _source.SessionId,
                        ["requestId"] = _source.RequestId,
                        ["snapshotToken"] = _source.SnapshotToken,
                        ["turn"] = _source.TurnNumber,
                        ["realm"] = _source.Realm,
                        ["originalSnapshotFingerprint"] =
                            pendingRoot["pending"]!["originalSnapshotFingerprint"]!.DeepClone(),
                        ["originalDraftImages"] = draftRows,
                        ["physicalWitnesses"] = witnesses,
                        ["initialAllocationCount"] = allocations.Count,
                        ["initialPendingPacketFingerprint"] = packetFingerprint,
                        ["advances"] = new JsonArray(),
                        ["committedAdvance"] = 0,
                        ["allocations"] = allocations,
                        ["expectedPendingPacketFingerprint"] = packetFingerprint,
                        ["checkpointFingerprint"] = ""
                    };
                    body["checkpointFingerprint"] = SpiritualWoundStateJson.Hash(body,
                        "spiritual_capture_checkpoint_v1", "checkpointFingerprint");
                    var root = new JsonObject { ["schemaVersion"] = 1, ["checkpoint"] = body };
                    var parsed = SpiritualWoundCaptureCheckpointState.Parse(root.ToJsonString(),
                        SpiritualWoundCaptureCheckpointState.StatePath, _draftInputs.PathInventory);
                    if (!parsed.IsValid || parsed.State is null)
                        return new(null, null, null, parsed.Issues);
                    return new(parsed.State, first.Pending,
                        first.ProposedInstanceRow?.DeepClone().AsObject(), []);
                }
                catch (Exception error) when (error is ArgumentException or FormatException or
                    InvalidOperationException or OverflowException or JsonException)
                {
                    return FirstCheckpointFailure("spiritual_first_checkpoint_composition_invalid");
                }
        }

        private static SpiritualC2FirstCheckpointDraftResult FirstCheckpointFailure(string code) =>
            new(null, null, null,
            [
                new ValidationIssue(SpiritualWoundCaptureCheckpointState.StatePath,
                    IssueSeverity.Error, "The current original capture cannot derive its first checkpoint.",
                    code: code, section: "AcceptedTurnWoundMaterialization")
            ]);
    }
}
