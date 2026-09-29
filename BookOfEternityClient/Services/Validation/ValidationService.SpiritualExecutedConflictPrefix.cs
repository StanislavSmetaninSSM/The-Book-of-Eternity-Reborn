using System.Text.Json.Nodes;
using BookOfEternityClient.Core;

namespace BookOfEternityClient.Services;

public partial class ValidationService
{
    internal sealed partial class SpiritualWoundSourceSession
    {
        /// <summary>
        /// Copies the source owner's complete original conflict projection, including its unexecuted suffix.
        /// </summary>
        /// <returns>
        /// Detached projected original conflict for companion identity checks only.
        /// </returns>
        internal JsonObject ReadInitialFullCandidateConflict()
        {
            if (!IsCurrentOwner)
                throw new InvalidOperationException("The spiritual source owner is stale.");
            return _initialFullCandidateConflict.DeepClone().AsObject();
        }

        /// <summary>
        /// Copies the current executed conflict projection owned by this source session.
        /// </summary>
        /// <returns>
        /// Detached current conflict state, excluding any unexecuted original draft suffix.
        /// </returns>
        internal JsonObject ReadExecutedConflictPrefix()
        {
            if (!IsCurrentOwner)
                throw new InvalidOperationException("The spiritual source owner is stale.");
            return _candidateConflict.DeepClone().AsObject();
        }
    }

    internal sealed partial class SpiritualOriginalTurnCapture
    {
        /// <summary>
        /// Reads the source owner's conflict projection at the latest accepted closed exchange.
        /// </summary>
        /// <param name="lease">
        /// Active canonical lease authenticating the current capture.
        /// </param>
        /// <param name="interval">
        /// Exact latest interval owned by this capture's resource executor.
        /// </param>
        /// <returns>
        /// Detached executed-prefix conflict state without the unexecuted original suffix.
        /// </returns>
        internal JsonObject ReadExecutedConflictPrefix(
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
                    throw new InvalidOperationException("The requested conflict prefix is not the current closed exchange.");
                return _source.ReadExecutedConflictPrefix();
            }
            finally { _gate.Release(); }
        }
    }
}
