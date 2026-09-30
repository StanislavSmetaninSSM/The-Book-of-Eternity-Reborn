using BookOfEternityClient.Core;

namespace BookOfEternityClient.Services;

public partial class ValidationService
{
    /// <summary>
    /// Carries detached canonical state and history at one accepted closed resource boundary.
    /// </summary>
    /// <param name="StateJson">
    /// Canonical current resource state JSON.
    /// </param>
    /// <param name="HistoryJson">
    /// Canonical current resource history JSON.
    /// </param>
    internal sealed record SpiritualClosedResourcePrefix(string StateJson, string HistoryJson);

    internal sealed partial class SpiritualOriginalTurnCapture
    {
        /// <summary>
        /// Reads complete resource and ordinary companion candidate facts at the latest closed exchange.
        /// </summary>
        /// <param name="lease">
        /// Active canonical lease authenticating this capture.
        /// </param>
        /// <param name="interval">
        /// Exact latest interval owned by this capture's resource executor.
        /// </param>
        /// <returns>
        /// Detached prefix data without resource or effect completion.
        /// </returns>
        internal AcceptedMechanicsPlanner.SpiritualResourceCandidatePrefix ReadClosedResourceCandidatePrefix(
            FileSystemManager.CanonicalWriteLease lease,
            AcceptedMechanicsPlanner.SpiritualExchangeInterval interval)
        {
            ArgumentNullException.ThrowIfNull(interval);
            if (!_gate.Wait(0))
                throw new InvalidOperationException("The original capture is continuing.");
            try
            {
                EnsureCurrent(lease);
                if (_closedExchangeEvidence.Count != _nextResourceOrdinal ||
                    _closedExchangeEvidence.Count == 0 ||
                    !ReferenceEquals(_closedExchangeEvidence[^1].Interval, interval) ||
                    _resources is null || !_resources.Owns(interval))
                    throw new InvalidOperationException("The requested resource prefix is not the current closed exchange.");
                return _resources.ReadClosedSpiritualResourceCandidatePrefix(interval);
            }
            finally { _gate.Release(); }
        }

        /// <summary>
        /// Reads the current closed resource prefix without finalizing its executor.
        /// </summary>
        /// <param name="lease">
        /// Active canonical lease authenticating this capture.
        /// </param>
        /// <param name="interval">
        /// Exact latest interval owned by this capture's resource executor.
        /// </param>
        /// <returns>
        /// Detached canonical resource state and history.
        /// </returns>
        internal SpiritualClosedResourcePrefix ReadClosedResourcePrefix(
            FileSystemManager.CanonicalWriteLease lease,
            AcceptedMechanicsPlanner.SpiritualExchangeInterval interval)
        {
            ArgumentNullException.ThrowIfNull(interval);
            if (!_gate.Wait(0))
                throw new InvalidOperationException("The original capture is continuing.");
            try
            {
                EnsureCurrent(lease);
                if (_closedExchangeEvidence.Count != _nextResourceOrdinal ||
                    _closedExchangeEvidence.Count == 0 ||
                    !ReferenceEquals(_closedExchangeEvidence[^1].Interval, interval) ||
                    _resources is null || !_resources.Owns(interval))
                    throw new InvalidOperationException("The requested resource prefix is not the current closed exchange.");
                var view = _resources.ReadClosedSpiritualResourcePrefix(interval);
                return new(view.StateJson, view.HistoryJson);
            }
            finally { _gate.Release(); }
        }
    }
}
