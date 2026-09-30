using System.Text.Json.Nodes;

namespace BookOfEternityClient.Services;

internal static partial class EffectAcceptedTurnPlanner
{
    internal sealed partial class EffectAcceptedDraft
    {
        /// <summary>
        /// Authenticates the complete chronological wound chain from this exact completed spiritual draft.
        /// </summary>
        /// <param name="completedPlan">
        /// Exact effect plan issued by this draft's successful completion.
        /// </param>
        /// <param name="routing">
        /// Exact installed resource routing owner used at completion.
        /// </param>
        /// <param name="transcript">
        /// Final resource boundary transcript consumed by this draft.
        /// </param>
        /// <param name="resources">
        /// Resource owner that registered every retained insertion.
        /// </param>
        /// <param name="selected">
        /// Capture-retained source selections paired with their actual insertions in causal order.
        /// </param>
        /// <returns>
        /// <see langword="true"/> only when ownership, registration, order and final state agree.
        /// </returns>
        internal bool AuthenticatesSpiritualLiveWoundCompletion(
            EffectAcceptedTurnPlan completedPlan, BaseResourceRouting routing,
            AcceptedEffectBoundaryTranscript transcript,
            AcceptedMechanicsPlanner.ResourceExecutionSession resources,
            IReadOnlyList<(ValidationService.SpiritualOriginalTurnCapture.WoundSelection Selection,
                EffectDraftWoundInsertion Insertion)> selected)
        {
            ArgumentNullException.ThrowIfNull(completedPlan);
            ArgumentNullException.ThrowIfNull(routing);
            ArgumentNullException.ThrowIfNull(transcript);
            ArgumentNullException.ThrowIfNull(resources);
            ArgumentNullException.ThrowIfNull(selected);
            if (!OwnsCompletion(completedPlan, routing, transcript) ||
                _currentWoundState is null || identityState.State is null ||
                selected.Count == 0 || selected.Count != _woundInsertions.Count)
                return false;
            var ordered = _woundInsertions.Values.OrderBy(value => value.VersionAfter).ToArray();
            if (!resources.OwnsCompletedWoundChain(ordered))
                return false;
            var carriers = EffectCarrierCatalog.Build(workspace.ToInput());
            if (carriers.Issues.Count != 0 ||
                ordered.Any(insertion => !insertion.ValidatesCompletionState(
                    identityState.State, carriers)))
                return false;
            WoundStateReduction? previous = null;
            var priorExchange = -1L;
            var sourceSides = new HashSet<(string Coordinate, string AffectedSide)>();
            for (var index = 0; index < ordered.Length; index++)
            {
                var insertion = ordered[index];
                var (selection, supplied) = selected[index];
                if (!ReferenceEquals(insertion, supplied) ||
                    !ReferenceEquals(insertion.Selection, EffectDraftWoundSelection.From(selection)) ||
                    insertion.Interval is null ||
                    !ReferenceEquals(insertion.Interval, selection.Admission.Interval) ||
                    insertion.VersionAfter != index + 1 ||
                    insertion.Interval.Ordinal < priorExchange ||
                    !sourceSides.Add((selection.Admission.Source.Coordinate,
                        selection.Admission.Source.AffectedSide)))
                    return false;
                var before = insertion.OperationBefore;
                var reduced = insertion.ReducedState;
                if (previous is not null &&
                    (!SameWoundCarriers(before.WoundCarriers, previous.Carriers) ||
                     !JsonNode.DeepEquals(before.WoundIdentity, previous.Identity) ||
                     !JsonNode.DeepEquals(before.WoundHistory, previous.History)))
                    return false;
                if (!ValidWoundState(reduced))
                    return false;
                previous = reduced;
                priorExchange = insertion.Interval.Ordinal;
            }
            if (previous is null ||
                !SameWoundCarriers(previous.Carriers, _currentWoundState.WoundCarriers) ||
                !JsonNode.DeepEquals(previous.Identity, _currentWoundState.WoundIdentity) ||
                !JsonNode.DeepEquals(previous.History, _currentWoundState.WoundHistory))
                return false;
            return true;
        }

        /// <summary>
        /// Checks carrier, identity and history agreement for one actual reducer result.
        /// </summary>
        /// <param name="reduced">
        /// Detached result produced by a registered insertion.
        /// </param>
        /// <returns>
        /// <see langword="true"/> when every wound root agrees; otherwise, <see langword="false"/>.
        /// </returns>
        private static bool ValidWoundState(WoundStateReduction reduced)
        {
            var carriers = WoundCarrierCatalog.Build(reduced.Carriers);
            var identity = WoundIdentityState.Parse(reduced.Identity.ToJsonString(),
                WoundIdentityState.StatePath);
            var history = WoundHistoryState.Parse(reduced.History.ToJsonString(),
                WoundHistoryState.HistoryPath);
            return carriers.Issues.Count == 0 && identity.State is not null &&
                identity.Issues.Count == 0 && history.State is not null &&
                history.Issues.Count == 0 &&
                history.State.ValidateAgreement(identity.State, carriers).Count == 0;
        }

        /// <summary>
        /// Compares every wound carrier root across adjacent insertion snapshots.
        /// </summary>
        /// <param name="left">
        /// Earlier reducer output or final draft image; <see langword="null"/> fails.
        /// </param>
        /// <param name="right">
        /// Later operation-before or reducer image; <see langword="null"/> fails.
        /// </param>
        /// <returns>
        /// <see langword="true"/> only for equal complete wound carrier images.
        /// </returns>
        private static bool SameWoundCarriers(WoundCarrierCatalogInput? left,
            WoundCarrierCatalogInput? right) =>
            left is not null && right is not null &&
            JsonNode.DeepEquals(left.PlayerWounds, right.PlayerWounds) &&
            JsonNode.DeepEquals(left.NpcWounds, right.NpcWounds) &&
            JsonNode.DeepEquals(left.EnemyCombatants, right.EnemyCombatants) &&
            JsonNode.DeepEquals(left.AllyCombatants, right.AllyCombatants) &&
            JsonNode.DeepEquals(left.AfterlifeProfiles, right.AfterlifeProfiles);
    }
}
