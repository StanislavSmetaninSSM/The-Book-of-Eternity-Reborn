using System.Text;
using System.Text.Json;
using System.Text.Json.Nodes;
using BookOfEternityClient.Core;

namespace BookOfEternityClient.Services;

public partial class ValidationService
{
    /// <summary>
    /// Reports a read-only comparison of a physical checkpoint and its pending projection.
    /// The result cannot authorize repair, a decision, or accepted publication.
    /// </summary>
    /// <param name="Disposition">
    /// Match, repair_required, no_checkpoint, or blocked.
    /// </param>
    /// <param name="Capture">
    /// Fresh original owner retained for match or repair_required, or <see langword="null"/> otherwise.
    /// The caller must dispose the retained capture.
    /// </param>
    /// <param name="Pending">
    /// Strictly rederived packet for match or repair_required, or <see langword="null"/> otherwise.
    /// </param>
    /// <param name="Issues">
    /// Validation failures for a blocked classification; empty for the other dispositions.
    /// </param>
    /// <param name="CheckpointImage">
    /// Exact physical checkpoint read before replay, retained only with a fresh owner.
    /// </param>
    /// <param name="PendingImage">
    /// Exact physical pending read before replay, including distinct absence and present-empty bytes.
    /// </param>
    internal sealed record SpiritualC2PendingClassificationResult(
        string Disposition, SpiritualOriginalTurnCapture? Capture,
        SpiritualWoundDecisionPendingState? Pending, IReadOnlyList<ValidationIssue> Issues,
        CanonicalBeforeImage? CheckpointImage = null, CanonicalBeforeImage? PendingImage = null);

    /// <summary>
    /// Reopens the physical first checkpoint through signed original owners and compares pending.
    /// </summary>
    /// <param name="lease">
    /// Active canonical lease held for physical reads and original-owner replay.
    /// </param>
    /// <returns>
    /// A retained owner and derived packet on match or repair_required, or a blocked diagnosis.
    /// </returns>
    internal Task<SpiritualC2PendingClassificationResult> ClassifyInitialSpiritualPendingAsync(
        FileSystemManager.CanonicalWriteLease lease) =>
        ClassifySpiritualPendingCoreAsync(lease, includeSavedAdvances: false);

    /// <summary>
    /// Reopens an initial or advanced physical checkpoint through its complete saved owner replay.
    /// </summary>
    /// <param name="lease">
    /// Active canonical lease for the physical pair and original owner transaction.
    /// </param>
    /// <returns>
    /// A retained matching owner, repair-required owner, or blocked diagnosis.
    /// </returns>
    internal Task<SpiritualC2PendingClassificationResult> ClassifySpiritualPendingAsync(
        FileSystemManager.CanonicalWriteLease lease) =>
        ClassifySpiritualPendingCoreAsync(lease, includeSavedAdvances: true);

    /// <summary>
    /// Classifies the exact physical pair using initial-only or complete saved replay policy.
    /// </summary>
    /// <param name="lease">
    /// Active canonical lease retained through read-back.
    /// </param>
    /// <param name="includeSavedAdvances">
    /// Replays committed decision rows when <see langword="true"/>.
    /// </param>
    /// <returns>
    /// A matching or repair-required owner only after physical freshness is rechecked.
    /// </returns>
    private async Task<SpiritualC2PendingClassificationResult> ClassifySpiritualPendingCoreAsync(
        FileSystemManager.CanonicalWriteLease lease, bool includeSavedAdvances)
    {
        _fs.EnsureCanonicalWriteLeaseActive(lease);
        byte[]? checkpointBytes;
        byte[]? pendingBytes;
        try
        {
            checkpointBytes = await _fs.ReadFileBytesAsync(lease,
                SpiritualWoundCaptureCheckpointState.StatePath);
            pendingBytes = await _fs.ReadFileBytesAsync(lease,
                SpiritualWoundDecisionPendingState.StatePath);
        }
        catch (Exception error) when (error is IOException or UnauthorizedAccessException)
        {
            return PendingClassificationFailure("spiritual_checkpoint_physical_read_failed");
        }

        if (checkpointBytes is null)
            return IsEmptyPhysicalPending(pendingBytes)
                ? new("no_checkpoint", null, null, [])
                : PendingClassificationFailure("spiritual_pending_without_checkpoint");

        SpiritualWoundCaptureCheckpointState? checkpoint;
        try
        {
            var checkpointJson = DecodePhysicalRoot(checkpointBytes);
            var root = SpiritualWoundStateJson.Parse(checkpointJson);
            var images = root["checkpoint"] is JsonObject body &&
                body["originalDraftImages"] is JsonArray rows
                    ? rows.Select(row => SpiritualWoundStateJson.Text(
                        (row as JsonObject)?["path"])).ToArray()
                    : Array.Empty<string>();
            var parsed = SpiritualWoundCaptureCheckpointState.Parse(checkpointJson,
                SpiritualWoundCaptureCheckpointState.StatePath, images);
            if (!parsed.IsValid || parsed.State is null)
                return PendingClassificationFailure("spiritual_checkpoint_physical_invalid");
            checkpoint = parsed.State;
        }
        catch (Exception error) when (error is JsonException or FormatException or
            InvalidOperationException or OverflowException or DecoderFallbackException)
        {
            return PendingClassificationFailure("spiritual_checkpoint_physical_invalid");
        }

        if (!checkpoint.HasCheckpoint)
            return IsEmptyPhysicalPending(pendingBytes)
                ? new("no_checkpoint", null, null, [])
                : PendingClassificationFailure("spiritual_pending_without_checkpoint");
        if (checkpoint.CommittedAdvance != 0 && !includeSavedAdvances)
            return PendingClassificationFailure("spiritual_checkpoint_not_initial");

        SpiritualOriginalTurnCapture? capture;
        SpiritualWoundDecisionPendingState? derived;
        IReadOnlyList<ValidationIssue> replayIssues;
        try
        {
            if (checkpoint.CommittedAdvance == 0 && !checkpoint.HasPendingSubmission)
            {
                var first = await ReplayInitialSpiritualCheckpointAsync(lease, checkpoint);
                capture = first.Capture;
                derived = first.Pending;
                replayIssues = first.Issues;
            }
            else
            {
                var saved = await ReplaySavedSpiritualCheckpointAsync(lease, checkpointBytes);
                capture = saved.Capture;
                derived = saved.Pending;
                replayIssues = saved.Issues;
            }
        }
        catch (Exception error) when (error is IOException or UnauthorizedAccessException)
        {
            return PendingClassificationFailure("spiritual_checkpoint_replay_read_failed");
        }
        if (capture is null || derived is null || replayIssues.Count != 0)
        {
            capture?.Dispose();
            return new("blocked", null, null, replayIssues.Count == 0
                ? PendingClassificationFailure("spiritual_checkpoint_replay_missing").Issues
                : replayIssues);
        }

        var retained = false;
        try
        {
            var inventory = capture.ReadC1ImageInventory(lease);
            var physicalPendingMatches = false;
            if (pendingBytes is not null)
            {
                try
                {
                    var parsed = SpiritualWoundDecisionPendingState.Parse(
                        DecodePhysicalRoot(pendingBytes), SpiritualWoundDecisionPendingState.StatePath,
                        inventory.RegisteredPaths);
                    physicalPendingMatches = parsed.IsValid && parsed.State is { HasPending: true } actual &&
                        SpiritualWoundDecisionPendingState.SerializeCanonical(actual) ==
                        SpiritualWoundDecisionPendingState.SerializeCanonical(derived);
                }
                catch (Exception error) when (error is JsonException or FormatException or
                    InvalidOperationException or OverflowException or DecoderFallbackException)
                {
                    physicalPendingMatches = false;
                }
            }

            var checkpointNow = await _fs.ReadFileBytesAsync(lease,
                SpiritualWoundCaptureCheckpointState.StatePath);
            var pendingNow = await _fs.ReadFileBytesAsync(lease,
                SpiritualWoundDecisionPendingState.StatePath);
            var checkpointAfterPending = await _fs.ReadFileBytesAsync(lease,
                SpiritualWoundCaptureCheckpointState.StatePath);
            if (!capture.IsCurrentOwner || !checkpointBytes.AsSpan().SequenceEqual(checkpointNow) ||
                !SamePhysicalBytes(pendingBytes, pendingNow) ||
                !checkpointBytes.AsSpan().SequenceEqual(checkpointAfterPending))
                return PendingClassificationFailure("spiritual_checkpoint_physical_changed");
            var disposition = physicalPendingMatches ? "match" : "repair_required";
            if (physicalPendingMatches)
                capture.RetainC2MatchedPair(derived, checkpointBytes, pendingBytes!);
            retained = true;
            return new(disposition, capture, derived, [],
                new CanonicalBeforeImage(true, checkpointBytes),
                new CanonicalBeforeImage(pendingBytes is not null, pendingBytes));
        }
        catch (Exception error) when (error is IOException or UnauthorizedAccessException or
            InvalidOperationException)
        {
            return PendingClassificationFailure("spiritual_checkpoint_classification_failed");
        }
        finally
        {
            if (!retained)
                capture.Dispose();
        }
    }

    private static bool SamePhysicalBytes(byte[]? left, byte[]? right) =>
        left is null ? right is null : right is not null && left.AsSpan().SequenceEqual(right);

    private static bool IsEmptyPhysicalPending(byte[]? bytes)
    {
        if (bytes is null)
            return true;
        try
        {
            var parsed = SpiritualWoundDecisionPendingState.Parse(DecodePhysicalRoot(bytes),
                SpiritualWoundDecisionPendingState.StatePath, []);
            return parsed.IsValid && parsed.State is { HasPending: false };
        }
        catch (Exception error) when (error is JsonException or FormatException or
            InvalidOperationException or OverflowException or DecoderFallbackException)
        {
            return false;
        }
    }

    private static string DecodePhysicalRoot(byte[] bytes)
    {
        using var stream = new MemoryStream(bytes, writable: false);
        using var reader = new StreamReader(stream, new UTF8Encoding(false, true),
            detectEncodingFromByteOrderMarks: true);
        return reader.ReadToEnd();
    }

    private static SpiritualC2PendingClassificationResult PendingClassificationFailure(string code) =>
        new("blocked", null, null,
        [
            new ValidationIssue(SpiritualWoundCaptureCheckpointState.StatePath,
                IssueSeverity.Error, "The spiritual checkpoint cannot establish a current pending packet.",
                code: code, section: "AcceptedTurnWoundMaterialization")
        ]);
}
