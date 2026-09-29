using System.Text.Json.Nodes;
using BookOfEternityClient.Core;

namespace BookOfEternityClient.Services;

public partial class ValidationService
{
    /// <summary>
    /// Captures signed original inputs while deferring spiritual source admission until ordinary resource work closes.
    /// </summary>
    /// <param name="lease">
    /// Active canonical write lease used to read and retain the signed inputs.
    /// </param>
    /// <returns>
    /// A retained capture or validation issues; successful capture alone exports no spiritual source capabilities.
    /// </returns>
    internal Task<SpiritualOriginalTurnCaptureResult> CaptureSpiritualOriginalTurnWithPrefixAsync(
        FileSystemManager.CanonicalWriteLease lease) =>
        SpiritualOriginalTurnCapture.CaptureAsync(this, lease, awaitOriginalPrefix: true);

    internal sealed partial class SpiritualOriginalTurnCapture
    {
        private bool _usesOriginalPrefix;
        private AcceptedMechanicsPlanner.OriginalResourcePrefix? _originalPrefix;
        // These are accepted typed commands and their actual steps, retained for E.
        // They are not a substitute for signed source or publication authority.
        private readonly List<(AcceptedMechanicsPlanner.ResourceExecutionStep Step, JsonArray Receipts)>
            _originalPrefixReceipts = new();

        private ResourceStateLedger SourceResourceBaseline =>
            _originalPrefix?.State ?? _input.PlanningContext!.State;

        /// <summary>
        /// Checks the exact source, capture and executor identities for a completed original prefix.
        /// </summary>
        /// <param name="source">
        /// Source session whose identity must match this capture.
        /// </param>
        /// <param name="prefix">
        /// Prefix retained by this capture and its usable executor.
        /// </param>
        /// <returns>
        /// <see langword="true"/> when all identities match and ownership remains current; otherwise <see langword="false"/>.
        /// </returns>
        internal bool OwnsOriginalPrefix(SpiritualWoundSourceSession source,
            AcceptedMechanicsPlanner.OriginalResourcePrefix prefix) =>
            _usesOriginalPrefix && !_disposed && ReferenceEquals(_source, source) &&
            ReferenceEquals(_validator._spiritualOriginalTurnCapture, this) &&
            ReferenceEquals(_originalPrefix, prefix) && _resources?.Owns(prefix) == true;

        private async Task<AcceptedMechanicsPlanner.ResourceContinuationResult> AcceptOriginalPrefixStepAsync(
            FileSystemManager.CanonicalWriteLease lease,
            AcceptedMechanicsPlanner.ResourceExecutionStep step, JsonArray? receipts)
        {
            if (step.Result != null)
            {
                var issues = step.Result.Issues;
                RevokeUnderLease(lease);
                return issues.Count != 0 ? new(null, issues) :
                    ResourceFailure("spiritual_original_prefix_missing", "an actual closed prefix before source admission");
            }
            if (step.OriginalPrefix == null && step.PendingResource == null ||
                step.Interval != null || step.PendingExchange != null)
            {
                RevokeUnderLease(lease);
                return ResourceFailure("spiritual_original_prefix_step_invalid", "prefix closure or its owned receipt wait");
            }
            if (receipts != null)
                _originalPrefixReceipts.Add((step, receipts.DeepClone().AsArray()));
            _lastResourceStep = step;
            if (step.OriginalPrefix is { } prefix)
            {
                if (_resources?.Owns(prefix) != true)
                    throw new InvalidOperationException("The original prefix must belong to this retained executor.");
                _originalPrefix = prefix;
                var issues = await _source.BindOriginalPrefixAsync(lease, this, prefix);
                if (issues.Any(issue => issue.Severity == IssueSeverity.Error))
                {
                    RevokeUnderLease(lease);
                    return new(null, issues);
                }
            }
            return new(step, Array.Empty<ValidationIssue>());
        }
    }
}
