using System.Text.Json.Nodes;
using BookOfEternityClient.Core;

namespace BookOfEternityClient.Services;

public partial class ValidationService
{
    /// <summary>
    /// Carries the actual next positive source and closed interval from a verified C2 owner.
    /// The returned objects still require ordinary source admission before a decision.
    /// </summary>
    /// <param name="Interval">
    /// Exact current resource interval, or <see langword="null"/> on rejection.
    /// </param>
    /// <param name="Source">
    /// Retained source object, or <see langword="null"/> on rejection.
    /// </param>
    /// <param name="Issues">
    /// Validation failures, empty only for an owned next source.
    /// </param>
    internal sealed record SpiritualC2NextSourceResult(
        AcceptedMechanicsPlanner.SpiritualExchangeInterval? Interval,
        PreparedSpiritualSource? Source, IReadOnlyList<ValidationIssue> Issues);

    internal sealed partial class SpiritualOriginalTurnCapture
    {
        private sealed record MatchedC2Pair(SpiritualWoundDecisionPendingState Pending,
            byte[] CheckpointBytes, byte[] PendingBytes);

        private MatchedC2Pair? _matchedC2Pair;

        /// <summary>
        /// Retains the exact physical pair after the classifier has replayed and rechecked it.
        /// A repair-required classification cannot bind a decision frontier.
        /// </summary>
        /// <param name="pending">
        /// Owner-derived pending packet that matched the physical projection.
        /// </param>
        /// <param name="checkpointBytes">
        /// Exact checkpoint bytes read and rechecked by classification.
        /// </param>
        /// <param name="pendingBytes">
        /// Exact matching pending bytes read and rechecked by classification.
        /// </param>
        internal void RetainC2MatchedPair(SpiritualWoundDecisionPendingState pending,
            byte[] checkpointBytes, byte[] pendingBytes)
        {
            ArgumentNullException.ThrowIfNull(pending);
            ArgumentNullException.ThrowIfNull(checkpointBytes);
            ArgumentNullException.ThrowIfNull(pendingBytes);
            if (!IsCurrentOwner || _matchedC2Pair is not null || !pending.HasPending)
                throw new InvalidOperationException("Only one current matching C2 pair may bind a frontier.");
            _matchedC2Pair = new(pending, (byte[])checkpointBytes.Clone(), (byte[])pendingBytes.Clone());
        }

        /// <summary>
        /// Resolves the next positive source from the current matched packet cursor and retained source owner.
        /// It rechecks both physical roots before exposing the interval and source for ordinary admission.
        /// </summary>
        /// <param name="lease">
        /// Active canonical lease for owner, signed-input and physical-pair freshness.
        /// </param>
        /// <returns>
        /// Exact retained source and interval, or issues without a source.
        /// </returns>
        internal async Task<SpiritualC2NextSourceResult> ReadC2NextSourceAsync(
            FileSystemManager.CanonicalWriteLease lease)
        {
            await _gate.WaitAsync();
            try { return await ReadC2NextSourceCoreAsync(lease); }
            finally { _gate.Release(); }
        }

        /// <summary>
        /// Resolves the saved cursor while a larger decision operation already holds the capture gate.
        /// </summary>
        /// <param name="lease">
        /// Active canonical lease for the exact physical pair and source checks.
        /// </param>
        /// <returns>
        /// Current source and interval, or validation issues.
        /// </returns>
        private async Task<SpiritualC2NextSourceResult> ReadC2NextSourceCoreAsync(
            FileSystemManager.CanonicalWriteLease lease)
        {
            try
            {
                EnsureCurrent(lease);
                if (_c2PendingSubmission)
                    return NextSourceFailure("spiritual_c2_selected_decision_pending");
                var pair = _matchedC2Pair;
                if (pair is null || !_usesColdOriginalInputs)
                    return NextSourceFailure("spiritual_c2_pair_required");
                var freshness = await CheckRetainedInputsAsync(lease);
                if (freshness.Count != 0)
                    return new(null, null, freshness);
                var sourceIssues = await _source.CheckContinuationInputsAsync(lease);
                if (sourceIssues.Count != 0)
                    return new(null, null, sourceIssues);
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
                {
                    RevokeUnderLease(lease);
                    return NextSourceFailure("spiritual_c2_pair_changed");
                }
                return ResolveC2NextSourceFromPending(pair.Pending);
            }
            catch (Exception error) when (error is System.IO.IOException or UnauthorizedAccessException or
                InvalidOperationException or FormatException or System.Text.Json.JsonException)
            {
                return NextSourceFailure("spiritual_c2_next_source_read_failed");
            }
        }

        /// <summary>
        /// Maps an owner-derived packet cursor to the exact current retained source without file intake.
        /// The caller must first authenticate the packet and validate its current input layer.
        /// </summary>
        /// <param name="pending">
        /// Packet rederived by the original owner, never a caller-supplied replacement.
        /// </param>
        /// <returns>
        /// Current retained source and interval, or issues for an exhausted or mismatched cursor.
        /// </returns>
        private SpiritualC2NextSourceResult ResolveC2NextSourceFromPending(
            SpiritualWoundDecisionPendingState pending)
        {
            try
            {
                var root = SpiritualWoundStateJson.Parse(
                    SpiritualWoundDecisionPendingState.SerializeCanonical(pending));
                var packet = root["pending"]!.AsObject();
                var nextOrdinal = packet["cursor"]!["nextSourceOrdinal"]!.GetValue<int>();
                var sources = _closedExchangeEvidence.SelectMany(entry => entry.Sources
                    .Select(source => (Entry: entry, Source: source))).ToArray();
                if (nextOrdinal >= sources.Length ||
                    packet["sources"] is not JsonArray packetSources ||
                    nextOrdinal >= packetSources.Count)
                    return NextSourceFailure("spiritual_c2_next_source_missing");
                var (entry, source) = sources[nextOrdinal];
                var sourceRow = packetSources[nextOrdinal]!.AsObject();
                if (source.Calculation.MaximumSeverityRank <= 0 &&
                    source.GuaranteedSeverityRank is null ||
                    sourceRow["sourceOrdinal"]!.GetValue<int>() != nextOrdinal ||
                    sourceRow["maximumSeverityRank"]!.GetValue<int>() != source.Calculation.MaximumSeverityRank ||
                    sourceRow["exchangeId"]!.GetValue<string>() != source.ExchangeId ||
                    sourceRow["affectedSide"]!.GetValue<string>() != source.AffectedSide ||
                    !ReferenceEquals(entry.Interval, _lastResourceStep?.Interval) ||
                    _resources?.Owns(entry.Interval) != true)
                    return NextSourceFailure("spiritual_c2_next_source_mismatch");
                return new(entry.Interval, source, []);
            }
            catch (Exception error) when (error is InvalidOperationException or FormatException or
                System.Text.Json.JsonException)
            {
                return NextSourceFailure("spiritual_c2_next_source_invalid");
            }
        }

        private static SpiritualC2NextSourceResult NextSourceFailure(string code) =>
            new(null, null,
            [
                new ValidationIssue(SpiritualWoundCaptureCheckpointState.StatePath,
                    IssueSeverity.Error, "The next spiritual decision source is not owned by the current pair.",
                    code: code, section: "AcceptedTurnWoundMaterialization")
            ]);
    }
}
