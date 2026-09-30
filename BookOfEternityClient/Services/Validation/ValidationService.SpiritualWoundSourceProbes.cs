using System.Text.Json.Nodes;
using BookOfEternityClient.Core;

namespace BookOfEternityClient.Services;

public partial class ValidationService
{
    internal sealed partial class SpiritualWoundSourceSession
    {
        /// <summary>
        /// Checks fresh continuation inputs without retaining any prospective source authority or time.
        /// Capture callers must hold their capture gate without an outer allocation scope.
        /// </summary>
        /// <param name="lease">
        /// Active canonical write lease authenticating the original and candidate inputs.
        /// </param>
        /// <returns>
        /// Rejection diagnostics, or an empty collection for valid inputs even when prospective work remains.
        /// </returns>
        internal Task<IReadOnlyList<ValidationIssue>> CheckContinuationInputsAsync(
            FileSystemManager.CanonicalWriteLease lease) => PreparedContinuation.CheckInputsAsync(this, lease);

        /// <summary>
        /// Checks completion under the source gate without issuing an accepted completion projection.
        /// Capture callers must hold their capture gate without an outer allocation scope.
        /// </summary>
        /// <param name="lease">
        /// Active canonical write lease used to authenticate fresh inputs.
        /// </param>
        /// <param name="closedExchangeCount">
        /// Number of exchanges closed by the owning capture.
        /// </param>
        /// <param name="resources">
        /// Exact resource execution owner bound to this source.
        /// </param>
        /// <param name="effects">
        /// Exact effect draft bound to <paramref name="resources"/>.
        /// </param>
        /// <returns>
        /// Completion rejection diagnostics, or an empty collection for a complete authenticated frontier.
        /// </returns>
        internal Task<IReadOnlyList<ValidationIssue>> CheckCompletionInputsAsync(
            FileSystemManager.CanonicalWriteLease lease, int closedExchangeCount,
            AcceptedMechanicsPlanner.ResourceExecutionSession resources,
            EffectAcceptedTurnPlanner.EffectAcceptedDraft effects) =>
            PreparedContinuation.CheckCompletionAsync(this, lease, closedExchangeCount, resources, effects);

        internal sealed partial class PreparedContinuation
        {
            /// <summary>
            /// Validates completion ownership and the fresh frontier while the source gate is already held.
            /// </summary>
            /// <param name="caller">
            /// Exact source owner that issued this ticket.
            /// </param>
            /// <param name="lease">
            /// Active canonical lease retained by the ticket.
            /// </param>
            /// <param name="closedExchangeCount">
            /// Capture-owned count of completed exchanges.
            /// </param>
            /// <param name="resources">
            /// Resource owner that must authenticate this source and effect draft.
            /// </param>
            /// <param name="effects">
            /// Exact effect draft bound to <paramref name="resources"/>.
            /// </param>
            /// <returns>
            /// Rejection diagnostics, or an empty collection when all completion checks pass.
            /// </returns>
            private IReadOnlyList<ValidationIssue> ValidateCompletionCore(
                SpiritualWoundSourceSession caller, FileSystemManager.CanonicalWriteLease lease,
                int closedExchangeCount, AcceptedMechanicsPlanner.ResourceExecutionSession resources,
                EffectAcceptedTurnPlanner.EffectAcceptedDraft effects) =>
                ValidateCommitContext(caller, lease) ??
                (!resources.OwnsOriginalCompletionOwners(effects, caller)
                    ? FailureIssues("spiritual_original_completion_owner_mismatch",
                        "the exact resource, effect and source owners bound by the original capture")
                    : _prospective.ValidateCompletionFrontier(caller, closedExchangeCount));

            /// <summary>
            /// Keeps the prospective ticket inside a discarded probe while holding the source gate.
            /// </summary>
            /// <param name="owner">
            /// Source owner providing actual retained comparison evidence.
            /// </param>
            /// <param name="lease">
            /// Active canonical write lease retained throughout validation.
            /// </param>
            /// <returns>
            /// Only rejection diagnostics; no source ticket or completion projection escapes.
            /// </returns>
            internal static async Task<IReadOnlyList<ValidationIssue>> CheckInputsAsync(
                SpiritualWoundSourceSession owner, FileSystemManager.CanonicalWriteLease lease)
            {
                owner._validator._fs.EnsureCanonicalWriteLeaseActive(lease);
                await owner._continuationGate.WaitAsync();
                try
                {
                    if (!owner.IsCurrentOwner || owner._awaitingOriginalPrefix)
                        return (await PrepareCoreAsync(owner, lease)).Issues;
                    using var probe = (owner._projectionClock as SpiritualWoundProjectionClock)?
                        .BeginConflictValidationProbe(RetainedEvidence(owner));
                    var prepared = await PrepareCoreAsync(owner, lease);
                    return prepared.Ticket is null ? prepared.Issues : Array.Empty<ValidationIssue>();
                }
                finally { owner._continuationGate.Release(); }
            }

            /// <summary>
            /// Checks completion using the same gate-held validation as accepted completion without minting a projection.
            /// </summary>
            /// <param name="owner">
            /// Current source owner supplying actual retained comparison evidence.
            /// </param>
            /// <param name="lease">
            /// Active canonical write lease held throughout validation.
            /// </param>
            /// <param name="closedExchangeCount">
            /// Number of exchanges closed by the owning capture.
            /// </param>
            /// <param name="resources">
            /// Exact resource execution owner bound to the source.
            /// </param>
            /// <param name="effects">
            /// Exact effect draft bound to <paramref name="resources"/>.
            /// </param>
            /// <returns>
            /// Only completion rejection diagnostics; no authority escapes the discarded probe.
            /// </returns>
            internal static async Task<IReadOnlyList<ValidationIssue>> CheckCompletionAsync(
                SpiritualWoundSourceSession owner, FileSystemManager.CanonicalWriteLease lease,
                int closedExchangeCount, AcceptedMechanicsPlanner.ResourceExecutionSession resources,
                EffectAcceptedTurnPlanner.EffectAcceptedDraft effects)
            {
                ArgumentNullException.ThrowIfNull(resources);
                ArgumentNullException.ThrowIfNull(effects);
                owner._validator._fs.EnsureCanonicalWriteLeaseActive(lease);
                await owner._continuationGate.WaitAsync();
                try
                {
                    if (!owner.IsCurrentOwner || owner._awaitingOriginalPrefix)
                        return (await PrepareCoreAsync(owner, lease)).Issues;
                    using var probe = (owner._projectionClock as SpiritualWoundProjectionClock)?
                        .BeginConflictValidationProbe(RetainedEvidence(owner));
                    var prepared = await PrepareCoreAsync(owner, lease);
                    return prepared.Ticket is null ? prepared.Issues :
                        prepared.Ticket.ValidateCompletionCore(owner, lease, closedExchangeCount, resources, effects);
                }
                finally { owner._continuationGate.Release(); }
            }

            /// <summary>
            /// Supplies only this source owner's retained roots and pending comparison values.
            /// </summary>
            /// <param name="owner">
            /// Prepared source owner whose accepted evidence remains unchanged during the probe.
            /// </param>
            /// <returns>
            /// Retained JSON roots; absent raw candidate evidence is represented by <see langword="null"/>.
            /// </returns>
            private static IEnumerable<JsonNode?> RetainedEvidence(SpiritualWoundSourceSession owner)
            {
                yield return owner._originalConflict;
                yield return owner._candidateConflict;
                if (owner._candidate.TryGetValue(AfterlifeSpiritualConflictState.StatePath, out var raw))
                    yield return raw is null ? null : JsonNode.Parse(raw);
                foreach (var pending in owner._pending)
                    yield return JsonNode.Parse(pending.CandidateJson);
            }
        }
    }
}
