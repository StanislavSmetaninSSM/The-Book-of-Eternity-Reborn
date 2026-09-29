using System.Text;
using System.Text.Json;
using System.Text.Json.Nodes;
using BookOfEternityClient.Core;

namespace BookOfEternityClient.Services;

public partial class ValidationService
{
    /// <summary>
    /// Retains fresh original owners after reproducing every saved decision from checkpoint inputs.
    /// The result neither repairs pending nor authorizes a new decision or accepted publication.
    /// </summary>
    /// <param name="Capture">
    /// Fresh fully replayed owner, or <see langword="null"/> on rejection.
    /// </param>
    /// <param name="Pending">
    /// Owner-derived last decision packet, or <see langword="null"/> on rejection.
    /// </param>
    /// <param name="Issues">
    /// Validation failures, empty only after every allocation and packet boundary matched.
    /// </param>
    internal sealed record SpiritualC2SavedReplayResult(
        SpiritualOriginalTurnCapture? Capture,
        SpiritualWoundDecisionPendingState? Pending,
        IReadOnlyList<ValidationIssue> Issues);

    /// <summary>
    /// Reopens the physical saved checkpoint, then strictly executes its decisions from signed A.
    /// The possibly absent or stale physical pending file is never an input to reconstruction.
    /// </summary>
    /// <param name="lease">
    /// Active canonical lease for checkpoint, signed snapshot and immutable witness reads.
    /// </param>
    /// <param name="expectedCheckpointBytes">
    /// Optional exact checkpoint image already observed by a pair classifier. A different
    /// physical replay origin is rejected before any owner is reconstructed.
    /// </param>
    /// <returns>
    /// Fresh owner and last packet on exact replay, or issues without retained owners.
    /// </returns>
    internal Task<SpiritualC2SavedReplayResult> ReplaySavedSpiritualCheckpointAsync(
        FileSystemManager.CanonicalWriteLease lease, byte[]? expectedCheckpointBytes = null) =>
        ReplaySavedSpiritualCheckpointCoreAsync(lease, expectedCheckpointBytes, verifyDependentProgress: true);

    /// <summary>
    /// Replays saved ownership, proving dependent progress on a disposable owner before restoring an unadvanced owner.
    /// </summary>
    /// <param name="lease">
    /// Active lease covering original ownership and exact checkpoint inputs.
    /// </param>
    /// <param name="expectedCheckpointBytes">
    /// Exact observed checkpoint, or null for the first read.
    /// </param>
    /// <param name="verifyDependentProgress">
    /// True for all callers; the single private restoration after successful proof suppresses duplicate speculation.
    /// </param>
    /// <returns>
    /// Reconstructed original selected owner or a refusal without retained ownership.
    /// </returns>
    private async Task<SpiritualC2SavedReplayResult> ReplaySavedSpiritualCheckpointCoreAsync(
        FileSystemManager.CanonicalWriteLease lease, byte[]? expectedCheckpointBytes, bool verifyDependentProgress)
    {
        _fs.EnsureCanonicalWriteLeaseActive(lease);
        SpiritualOriginalTurnCapture? capture = null;
        var retained = false;
        try
        {
            var physical = await _fs.ReadFileBytesAsync(lease,
                SpiritualWoundCaptureCheckpointState.StatePath);
            if (physical is null)
                return SavedReplayFailure("spiritual_c2_saved_checkpoint_missing");
            if (expectedCheckpointBytes is not null &&
                !expectedCheckpointBytes.AsSpan().SequenceEqual(physical))
                return SavedReplayFailure("spiritual_c2_saved_checkpoint_changed");
            var root = SpiritualWoundStateJson.Parse(DecodePhysicalRoot(physical));
            var body = root["checkpoint"] as JsonObject;
            var originalRows = body?["originalDraftImages"] as JsonArray;
            if (originalRows is null)
                return SavedReplayFailure("spiritual_c2_saved_checkpoint_invalid");
            var paths = originalRows.Select(row => row?["path"]?.GetValue<string>() ?? string.Empty)
                .ToArray();
            var parsed = SpiritualWoundCaptureCheckpointState.Parse(root.ToJsonString(),
                SpiritualWoundCaptureCheckpointState.StatePath, paths);
            if (!parsed.IsValid || parsed.State is not { HasCheckpoint: true } checkpoint ||
                checkpoint.CommittedAdvance == 0 && !checkpoint.HasPendingSubmission)
                return SavedReplayFailure("spiritual_c2_saved_checkpoint_invalid");
            var first = await SpiritualOriginalTurnCapture.ReplaySavedFirstCheckpointAsync(
                this, lease, checkpoint);
            capture = first.Capture;
            if (capture is null || first.Pending is null || first.Issues.Count != 0)
                return first.Issues.Count != 0
                    ? new(null, null, first.Issues)
                    : SavedReplayFailure("spiritual_c2_saved_first_replay_failed");
            var original = checkpoint.ReadOriginalDraftInputs();
            var layer = original.PathInventory.ToDictionary(path => path,
                original.ReadImage, StringComparer.Ordinal);
            var priorCheckpoint = ParseSavedPrefix(root, paths, 0,
                checkpoint.InitialAllocationCount);
            if (priorCheckpoint is null)
                return SavedReplayFailure("spiritual_c2_saved_initial_prefix_invalid");
            var priorPending = first.Pending;
            var advances = body!["advances"]!.AsArray();
            for (var index = 0; index < advances.Count; index++)
            {
                var advance = advances[index]!.AsObject();
                var changes = new List<(string Path, CanonicalBeforeImage Image)>();
                foreach (var node in advance["inputChanges"]!.AsArray())
                {
                    var row = node!.AsObject();
                    var path = row["path"]!.GetValue<string>();
                    var existed = row["existed"]!.GetValue<bool>();
                    var bytes = existed
                        ? Convert.FromBase64String(row["contentBase64"]!.GetValue<string>()) : null;
                    var image = new CanonicalBeforeImage(existed, bytes);
                    if (!layer.TryGetValue(path, out var previous) ||
                        SameSavedImage(previous, image))
                        return SavedReplayFailure("spiritual_c2_saved_input_change_invalid");
                    layer[path] = image;
                    changes.Add((path, image));
                }
                var replayed = await capture.ReplayC2SavedDecisionLayerAsync(lease,
                    priorPending, priorCheckpoint, layer, changes);
                if (replayed.Checkpoint is null || replayed.Pending is null ||
                    replayed.Issues.Count != 0)
                    return replayed.Issues.Count != 0
                        ? new(null, null, replayed.Issues)
                        : SavedReplayFailure("spiritual_c2_saved_step_replay_failed");
                var count = advance["allocationCount"]!.GetValue<int>();
                var expected = ParseSavedPrefix(root, paths, index + 1, count);
                if (expected is null ||
                    SpiritualWoundCaptureCheckpointState.SerializeCanonical(expected) !=
                    SpiritualWoundCaptureCheckpointState.SerializeCanonical(replayed.Checkpoint) ||
                    replayed.Pending.PacketFingerprint !=
                    advance["resultPendingPacketFingerprint"]!.GetValue<string>() ||
                    capture.ReadAllocationCursor(lease) != count)
                    return SavedReplayFailure("spiritual_c2_saved_step_mismatch");
                priorCheckpoint = replayed.Checkpoint;
                priorPending = replayed.Pending;
            }
            if (capture.ReadAllocationCursor(lease) !=
                body["allocations"]!.AsArray().Count ||
                priorPending.PacketFingerprint !=
                body["expectedPendingPacketFingerprint"]!.GetValue<string>())
                return SavedReplayFailure("spiritual_c2_saved_journal_incomplete");
            if (checkpoint.HasPendingSubmission)
            {
                var submissionIssues = await capture.ReplayC2PendingSubmissionAsync(lease, checkpoint, priorPending);
                if (submissionIssues.Count != 0) return new(null, null, submissionIssues);
            }
            else if (capture.ReadAllocationJournal(lease).Count != body["allocations"]!.AsArray().Count)
                return SavedReplayFailure("spiritual_c2_saved_journal_incomplete");
            if (verifyDependentProgress && checkpoint.ReadPendingSubmission()?["dependentDraftProgress"] is JsonArray)
            {
                var proof = await capture.WalkC2DependentContextAsync(lease, null,
                    replayCheckpoint: checkpoint, replayPending: priorPending);
                if (proof is null) return SavedReplayFailure("spiritual_c2_dependent_progress_invalid");
                capture.Dispose();
                capture = null;
                return await ReplaySavedSpiritualCheckpointCoreAsync(lease, physical, verifyDependentProgress: false);
            }
            var inputIssues = await capture.CheckRetainedInputsAsync(lease);
            if (inputIssues.Count != 0)
                return new(null, null, inputIssues);
            var checkpointNow = await _fs.ReadFileBytesAsync(lease,
                SpiritualWoundCaptureCheckpointState.StatePath);
            if (!capture.IsCurrentOwner || checkpointNow is null ||
                !physical.AsSpan().SequenceEqual(checkpointNow))
                return SavedReplayFailure("spiritual_c2_saved_checkpoint_changed");
            retained = true;
            return new(capture, priorPending, []);
        }
        catch (Exception error) when (error is IOException or UnauthorizedAccessException or
            InvalidOperationException or FormatException or JsonException or OverflowException or
            DecoderFallbackException)
        {
            return SavedReplayFailure("spiritual_c2_saved_replay_failed");
        }
        finally
        {
            if (!retained)
                capture?.Dispose();
        }
    }

    /// <summary>
    /// Rebuilds one saved prefix for exact comparison with a newly executed owner step.
    /// </summary>
    /// <param name="saved">
    /// Strict complete checkpoint root.
    /// </param>
    /// <param name="paths">
    /// Exact registered original draft path inventory.
    /// </param>
    /// <param name="steps">
    /// Number of saved decision advances retained in the prefix.
    /// </param>
    /// <param name="allocationCount">
    /// Cumulative allocation cursor of that prefix.
    /// </param>
    /// <returns>
    /// Strict prefix state, or <see langword="null"/> for an invalid boundary.
    /// </returns>
    private static SpiritualWoundCaptureCheckpointState? ParseSavedPrefix(
        JsonObject saved, IReadOnlyList<string> paths, int steps, int allocationCount)
    {
        var root = saved.DeepClone().AsObject();
        var body = root["checkpoint"]!.AsObject();
        body.Remove("pendingSubmission");
        var savedAdvances = body["advances"]!.AsArray();
        var savedAllocations = body["allocations"]!.AsArray();
        if (steps < 0 || steps > savedAdvances.Count || allocationCount < 0 ||
            allocationCount > savedAllocations.Count)
            return null;
        body["advances"] = new JsonArray(savedAdvances.Take(steps)
            .Select(row => row?.DeepClone()).ToArray());
        body["allocations"] = new JsonArray(savedAllocations.Take(allocationCount)
            .Select(row => row?.DeepClone()).ToArray());
        body["committedAdvance"] = steps;
        body["expectedPendingPacketFingerprint"] = steps == 0
            ? body["initialPendingPacketFingerprint"]!.DeepClone()
            : savedAdvances[steps - 1]!["resultPendingPacketFingerprint"]!.DeepClone();
        body["checkpointFingerprint"] = SpiritualWoundStateJson.Hash(body,
            "spiritual_capture_checkpoint_v1", "checkpointFingerprint");
        var parsed = SpiritualWoundCaptureCheckpointState.Parse(root.ToJsonString(),
            SpiritualWoundCaptureCheckpointState.StatePath, paths);
        return parsed.IsValid ? parsed.State : null;
    }

    private static bool SameSavedImage(CanonicalBeforeImage left, CanonicalBeforeImage right) =>
        left.Existed == right.Existed && (left.Bytes is { } bytes
            ? right.Bytes is { } other && bytes.AsSpan().SequenceEqual(other)
            : right.Bytes is null);

    private static SpiritualC2SavedReplayResult SavedReplayFailure(string code) =>
        new(null, null,
        [
            new ValidationIssue(SpiritualWoundCaptureCheckpointState.StatePath,
                IssueSeverity.Error, "The saved spiritual checkpoint cannot be replayed by its owners.",
                code: code, section: "AcceptedTurnWoundMaterialization")
        ]);
}
