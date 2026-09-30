using System.Collections.ObjectModel;
using System.Text.Json.Nodes;

namespace BookOfEternityClient.Services;

internal static partial class AcceptedMechanicsPlanner
{
    /// <summary>
    /// Carries detached resource-owner candidate images and transitions at one closed spiritual prefix.
    /// </summary>
    /// <param name="DefinitionsJson">
    /// Effective definition catalog after any same-turn definition materialization.
    /// </param>
    /// <param name="StateJson">
    /// Current canonical resource state.
    /// </param>
    /// <param name="HistoryJson">
    /// Current canonical resource history.
    /// </param>
    /// <param name="OwnerAuthorityJson">
    /// Current authority projection from the exact owned state and history.
    /// </param>
    /// <param name="AppliedTransitions">
    /// Detached applied transitions through the latest closed exchange.
    /// </param>
    /// <param name="ReplayTransitions">
    /// Detached replay transitions through the latest closed exchange.
    /// </param>
    /// <param name="CompanionAfterImages">
    /// Detached complete ordinary companion outcome roots.
    /// </param>
    /// <param name="OwnerTransitions">
    /// Detached typed owner transitions from ordinary prefix outcomes.
    /// </param>
    internal sealed record SpiritualResourceCandidatePrefix(
        string DefinitionsJson, string StateJson, string HistoryJson, string OwnerAuthorityJson,
        IReadOnlyList<ResourceTransition> AppliedTransitions,
        IReadOnlyList<ResourceTransition> ReplayTransitions,
        IReadOnlyDictionary<string, JsonObject> CompanionAfterImages,
        IReadOnlyList<AcceptedMechanicsOwnerTransition> OwnerTransitions);

    internal sealed partial class ResourceExecutionSession
    {
        /// <summary>
        /// Projects current resource and ordinary companion owners without completing the live executor.
        /// </summary>
        /// <param name="interval">
        /// Exact latest closed interval issued by this executor.
        /// </param>
        /// <returns>
        /// Detached candidate resources and ordinary owner outcomes.
        /// </returns>
        internal SpiritualResourceCandidatePrefix ReadClosedSpiritualResourceCandidatePrefix(
            SpiritualExchangeInterval interval)
        {
            ArgumentNullException.ThrowIfNull(interval);
            Enter();
            try
            {
                EnsureUsable();
                if (!_live || !_state.IsLatestClosedInterval(interval) ||
                    _pendingExchange || _pendingResourceObservation is not null ||
                    _pendingPacket is not null || _active is not null || _staged is not null ||
                    _pendingInput?.PlanningContext is not { } context)
                    throw new InvalidOperationException("Only the latest closed spiritual resource prefix can be projected.");
                var partial = _state.ReadClosedSpiritualResourcePlanningPrefix(
                    _originalResourceInput.Definitions);
                var projected = ProjectRegisteredSystemOutcomes(context, partial,
                    originalSpiritualPrefix: true);
                if (!projected.IsValid)
                    throw new InvalidOperationException(
                        "An ordinary outcome disagrees with the closed spiritual resource prefix.");
                var state = partial.StateAfterImage!;
                var history = partial.HistoryAfterImage!;
                return new(
                    _originalResourceInput.Definitions.ToCanonicalJson(),
                    state.ToCanonicalJson(), history.ToCanonicalJson(),
                    CanonicalResourceOwnerAuthorityComposer.CreateCanonicalAuthorityJson(
                        context.Owners, state, history),
                    partial.AppliedTransitions, partial.ReplayTransitions,
                    new ReadOnlyDictionary<string, JsonObject>(projected.CompanionAfterImages
                        .ToDictionary(pair => pair.Key,
                            pair => pair.Value.DeepClone().AsObject(), StringComparer.Ordinal)),
                    Array.AsReadOnly(projected.OwnerTransitions.Select(transition =>
                        transition.Clone()).ToArray()));
            }
            finally { Exit(); }
        }

        /// <summary>
        /// Reads canonical resource images at the exact latest closed spiritual exchange.
        /// The live executor remains open for later exchanges and wound insertions.
        /// </summary>
        /// <param name="interval">
        /// Latest interval issued by this exact executor.
        /// </param>
        /// <returns>
        /// Detached state and history JSON from the current closed resource prefix.
        /// </returns>
        internal (string StateJson, string HistoryJson) ReadClosedSpiritualResourcePrefix(
            SpiritualExchangeInterval interval)
        {
            ArgumentNullException.ThrowIfNull(interval);
            Enter();
            try
            {
                EnsureUsable();
                if (!_live || !_state.IsLatestClosedInterval(interval) ||
                    _pendingExchange || _pendingResourceObservation is not null ||
                    _pendingPacket is not null || _active is not null || _staged is not null)
                    throw new InvalidOperationException("Only the latest closed spiritual resource prefix can be read.");
                return _state.ReadClosedSpiritualResourcePrefix(_originalResourceInput.Definitions);
            }
            finally { Exit(); }
        }
    }

    private sealed partial class ResourceExecutionState
    {
        /// <summary>
        /// Copies an unfinished but validated resource prefix into a private outcome projection adapter.
        /// The missing effect transcript deliberately prevents treating it as a completed result.
        /// </summary>
        /// <param name="definitions">
        /// Effective definition catalog used by the live executor.
        /// </param>
        /// <returns>
        /// Detached resource facts for read-only ordinary outcome projection.
        /// </returns>
        internal AcceptedMechanicsResourcePlanningResult ReadClosedSpiritualResourcePlanningPrefix(
            ResourceDefinitionCatalog definitions)
        {
            var state = workingLedger.Freeze();
            var history = workingHistory.ReadValidatedSnapshot(definitions);
            if (!history.IsValid || history.History!.ValidateStateAgreement(state).Count != 0)
                throw new InvalidOperationException("The closed resource prefix disagrees with its history.");
            return new(state, history.History, events, appliedTransitions,
                replayTransitions, Array.Empty<ValidationIssue>(),
                Statistics(workingHistory, metrics));
        }

        /// <summary>
        /// Projects validated current resource state and history without freezing the working history.
        /// </summary>
        /// <param name="definitions">
        /// Original definition catalog used by this executor.
        /// </param>
        /// <returns>
        /// Detached canonical state and history JSON for the already checked boundary.
        /// </returns>
        internal (string StateJson, string HistoryJson) ReadClosedSpiritualResourcePrefix(
            ResourceDefinitionCatalog definitions)
        {
            var state = workingLedger.Freeze();
            var history = workingHistory.ReadValidatedSnapshot(definitions);
            if (!history.IsValid || history.History!.ValidateStateAgreement(state).Count != 0)
                throw new InvalidOperationException("The closed resource prefix disagrees with its history.");
            return (state.ToCanonicalJson(), history.History.ToCanonicalJson());
        }
    }
}
