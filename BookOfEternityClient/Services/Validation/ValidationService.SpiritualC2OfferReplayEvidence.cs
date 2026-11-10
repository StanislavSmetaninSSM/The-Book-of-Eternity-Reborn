using System.Text.Json;
using BookOfEternityClient.Core;

namespace BookOfEternityClient.Services;

public partial class ValidationService
{
    /// <summary>Immutable observation of DATA already reconstructed by one genuine open offer; never an owner or permission.</summary>
    /// <param name="ReconstructedPendingJson">The classifier-derived retained pending root, not a physical file parse.</param>
    /// <param name="ActualSourceDataJson">All fourteen immutable facts from the exact retained source, including calculation and preimage.</param>
    /// <param name="AllocationCursor">The actual committed journal position.</param>
    /// <param name="AllocationJournalJson">The actual full retained allocation rows.</param>
    /// <param name="OriginalConflictBytesBase64">Exact original authored conflict bytes, including original formatting.</param>
    /// <param name="OriginalConflictFingerprint">The existing presence-aware fingerprint of those exact original bytes.</param>
    internal sealed record SpiritualC2OfferReplayEvidence(string ReconstructedPendingJson,
        string ActualSourceDataJson, int AllocationCursor, string AllocationJournalJson,
        string OriginalConflictBytesBase64, string OriginalConflictFingerprint);

    internal sealed partial class SpiritualC2PrivateSession
    {
        /// <summary>Observes only the already opened offer under submit then capture exclusion, without I/O or replay.</summary>
        /// <param name="lease">An active canonical lease belonging to this exact session filesystem.</param>
        /// <returns>Detached immutable comparison DATA; consumed/disposed sessions and nonoffers reject.</returns>
        internal async Task<SpiritualC2OfferReplayEvidence> ReadOfferReplayEvidenceAsync(
            FileSystemManager.CanonicalWriteLease lease)
        {
            await _submitGate.WaitAsync();
            try
            {
                ObjectDisposedException.ThrowIf(_disposed || _used, this);
                if (Offer == null || _interval == null || _source == null || _binding == null || _opportunity == null)
                    throw new InvalidOperationException("Only a live opened offer has replay evidence.");
                _validator._fs.EnsureCanonicalWriteLeaseActive(lease);
                var evidence = await _capture.ReadC2OfferReplayEvidenceAsync(lease,
                    _interval, _source, _binding, _opportunity, Offer);
                ObjectDisposedException.ThrowIf(_disposed || _used, this);
                return evidence;
            }
            finally { _submitGate.Release(); }
        }
    }

    internal sealed partial class SpiritualOriginalTurnCapture
    {
        /// <summary>Joins an opened session to its exact retained offer and reads detached DATA under one capture gate.</summary>
        /// <param name="lease">Actual active lease for the original capture filesystem.</param>
        /// <param name="interval">The opened session's exact last closed resource interval.</param>
        /// <param name="source">The opened session's exact retained source.</param>
        /// <param name="binding">The exact retained accepted event binding.</param>
        /// <param name="opportunity">The exact retained admitted opportunity.</param>
        /// <param name="offer">The session's readonly safe projection of that opportunity.</param>
        /// <returns>Only immutable strings and a committed cursor; no file is read and no owner is exported.</returns>
        internal async Task<SpiritualC2OfferReplayEvidence> ReadC2OfferReplayEvidenceAsync(
            FileSystemManager.CanonicalWriteLease lease, AcceptedMechanicsPlanner.SpiritualExchangeInterval interval,
            PreparedSpiritualSource source, WoundAcceptedTurnBinding binding,
            WoundOpportunityAuthority opportunity, SpiritualC2PrivateOffer offer)
        {
            await _gate.WaitAsync();
            try
            {
                var pair = _matchedC2Pair;
                void RequireCurrentOffer()
                {
                    EnsureCurrent(lease);
                    if (!_usesColdOriginalInputs || _c2PendingSubmission || pair == null ||
                        !ReferenceEquals(pair, _matchedC2Pair) || !pair.Pending.HasPending ||
                        _closedExchangeEvidence.Count == 0 || _closedExchangeEvidence.Count != _nextResourceOrdinal ||
                        _source.CheckedExchanges.Count != _closedExchangeEvidence.Count ||
                        !ReferenceEquals(_closedExchangeEvidence[^1].Interval, interval) ||
                        !ReferenceEquals(_lastResourceStep?.Interval, interval) ||
                        _resources?.Owns(interval) != true || !OwnsWoundFrontier(interval, source) ||
                        !_woundAdmissions.TryGetValue(source, out var admission) || !OwnsWoundAdmission(admission) ||
                        !ReferenceEquals(admission.Interval, interval) ||
                        !_woundOpportunities.TryGetValue(admission, out var retained) || retained.Issues.Count != 0 ||
                        retained.Satisfaction != null || !ReferenceEquals(retained.Binding, binding) ||
                        !ReferenceEquals(retained.Opportunity, opportunity) ||
                        binding.SessionId != _source.SessionId || binding.RequestId != _source.RequestId ||
                        binding.SnapshotToken != _source.SnapshotToken || binding.Realm != _source.Realm || binding.Turn != _source.TurnNumber ||
                        opportunity.SessionId != binding.SessionId || opportunity.RequestId != binding.RequestId ||
                        opportunity.SnapshotToken != binding.SnapshotToken || opportunity.AcceptedEventsFingerprint != binding.AcceptedEventsFingerprint ||
                        !MatchesReplayOffer(offer, opportunity))
                        throw new InvalidOperationException("Replay evidence requires this capture's exact current opened offer.");
                    var closed = _closedExchangeEvidence[^1];
                    if (closed.ExchangeJson != source.ExchangeJson ||
                        _source.CheckedExchanges[interval.Ordinal] != closed.ExchangeJson ||
                        closed.Sources.Count(value => ReferenceEquals(value, source)) != 1)
                        throw new InvalidOperationException("The opened source no longer matches its retained closed exchange.");
                    var sources = _closedExchangeEvidence.SelectMany(entry => entry.Sources).ToArray();
                    var selected = Array.FindIndex(sources, value => ReferenceEquals(value, source));
                    if (selected < 0 || sources.Count(value => ReferenceEquals(value, source)) != 1 ||
                        !pair.Pending.MatchesOfferReplaySource(binding, _closedExchangeEvidence.Count,
                            sources.Length, selected, interval.Ordinal, source))
                        throw new InvalidOperationException("The reconstructed pending cursor no longer matches the exact opened source.");
                }

                RequireCurrentOffer();
                var original = _draftInputs.ReadImage(AfterlifeSpiritualConflictState.StatePath);
                if (!original.Existed || original.Bytes == null)
                    throw new InvalidOperationException("Replay evidence requires the exact retained original conflict bytes.");
                var evidence = new SpiritualC2OfferReplayEvidence(
                    SpiritualWoundDecisionPendingState.SerializeCanonical(pair!.Pending),
                    JsonSerializer.Serialize(new { source.Coordinate, source.ConflictId, source.ExchangeId, source.AffectedSide,
                        source.ActingActor, source.AffectedActor, source.Operation, source.OriginalSpecialArtJson,
                        source.ExchangeJson, source.Calculation, source.GuaranteedSeverityRank, source.RetraumaWoundId,
                        source.RetraumaWoundJson, source.StrainPreimageJson }),
                    _allocations.Journal.ReadCursor(), _allocations.Journal.Export().ToJsonString(),
                    Convert.ToBase64String(original.Bytes), original.Fingerprint);
                RequireCurrentOffer();
                return evidence;
            }
            finally { _gate.Release(); }
        }

        /// <summary>Checks the retained safe offer against its exact opportunity without rebuilding either authority.</summary>
        private static bool MatchesReplayOffer(SpiritualC2PrivateOffer offer, WoundOpportunityAuthority opportunity)
        {
            var minimum = Math.Max(opportunity.MinimumSeverityRank ?? 1,
                (opportunity.WorseningTarget?.Wound.Severity.Rank ?? 0) + 1);
            var decisions = opportunity.RequiredSeverityRank != null ? new[] { "materialize" } :
                minimum <= opportunity.MaximumSeverityRank ? new[] { "none", "materialize" } : new[] { "none" };
            return offer.OpportunityRef == opportunity.PublicRef && offer.MinimumSeverityRank == minimum &&
                offer.RequiredSeverityRank == opportunity.RequiredSeverityRank && offer.MaximumSeverityRank == opportunity.MaximumSeverityRank &&
                offer.Target == opportunity.SafeContext.Target && offer.Cause == opportunity.SafeContext.Cause &&
                offer.AllowedLocationKinds.SequenceEqual(opportunity.SafeContext.AllowedLocationKinds, StringComparer.Ordinal) &&
                offer.AllowedDecisions.SequenceEqual(decisions, StringComparer.Ordinal);
        }
    }
}
