using BookOfEternityClient.Core;

namespace BookOfEternityClient.Services;

public partial class ValidationService
{
    internal sealed partial class SpiritualWoundSourceSession
    {
        private bool _awaitingOriginalPrefix;
        private SpiritualOriginalTurnCapture? _prefixCapture;
        private AcceptedMechanicsPlanner.OriginalResourcePrefix? _resourcePrefix;
        private int? _acceptedExchangeLimit;

        // The signed original is never rewritten. Only the resource-sequence
        // checks read the actual completed prefix in the explicit retained path.
        private string? SourceResourceBaselineJson =>
            _resourcePrefix?.State.ToCanonicalJson() ?? _before[ResourceMaterializationContract.StatePath];

        /// <summary>
        /// Rechecks signed input identity and prepares sources against the owning capture's completed resource prefix.
        /// </summary>
        /// <param name="lease">
        /// Active canonical write lease for retained input checks.
        /// </param>
        /// <param name="capture">
        /// Current capture owning this source session and its resource executor.
        /// </param>
        /// <param name="prefix">
        /// Exact prefix produced by that executor; foreign or previously bound prefixes are rejected.
        /// </param>
        /// <returns>
        /// Validation issues, or an empty collection after successful source preparation.
        /// </returns>
        internal async Task<IReadOnlyList<ValidationIssue>> BindOriginalPrefixAsync(
            FileSystemManager.CanonicalWriteLease lease, SpiritualOriginalTurnCapture capture,
            AcceptedMechanicsPlanner.OriginalResourcePrefix prefix)
        {
            _validator._fs.EnsureCanonicalWriteLeaseActive(lease);
            await _continuationGate.WaitAsync();
            try
            {
                if (!IsCurrentOwner || !_awaitingOriginalPrefix || !capture.OwnsOriginalPrefix(this, prefix))
                    return new[] { SourceIssue(AfterlifeSpiritualConflictState.StatePath,
                        "spiritual_source_prefix_mismatch", "exact current capture and its unconsumed completed original prefix") };
                var read = PendingTurnSnapshotReader.ReadCurrent(_validator._fs, lease,
                    PendingTurnSnapshotPathSelection.CreateWithObservedOptionalPaths(RequiredPaths, OptionalPaths));
                var request = await _validator._fs.ReadFileBytesAsync(lease, "input/turn_request.json");
                if (!read.Success || read.Snapshot == null || !SameOriginal(read.Snapshot) ||
                    !SameBytes(_originalRequestBytes, request))
                {
                    RevokeCurrent();
                    return new[] { SourceIssue(AfterlifeSpiritualConflictState.StatePath,
                        "spiritual_source_continuation_origin_changed", "same signed original and request required for prefix binding") };
                }
                _resourcePrefix = prefix;
                _prefixCapture = capture;
                _acceptedExchangeLimit = 0;
                _awaitingOriginalPrefix = false;
                var issues = new List<ValidationIssue>();
                try
                {
                    Prepare(issues);
                }
                catch (Exception exception) when (exception is System.Text.Json.JsonException or
                    InvalidOperationException or ArgumentException or OverflowException)
                {
                    issues.Add(SourceIssue(AfterlifeSpiritualConflictState.StatePath,
                        "spiritual_source_input_invalid", exception.Message));
                }
                if (issues.Any(issue => issue.Severity == IssueSeverity.Error))
                    RevokeCurrent();
                return issues.AsReadOnly();
            }
            finally { _continuationGate.Release(); }
        }
    }
}
