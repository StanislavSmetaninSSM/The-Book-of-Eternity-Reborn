namespace BookOfEternityClient.Services;

internal static partial class EffectAcceptedTurnPlanner
{
    internal sealed partial class EffectAcceptedDraft
    {
        /// <summary>
        /// Preflights the selected wound's local N, R and U edits without advancing unrelated work.
        /// </summary>
        /// <param name="selection">
        /// Exact registered selection already authenticated under this draft's exclusive gate.
        /// </param>
        /// <param name="input">
        /// Detached input from that same selection, including its authenticated worsening target.
        /// </param>
        /// <param name="before">
        /// Current draft-owned state before allocation or mutation.
        /// </param>
        /// <param name="failures">
        /// Receives carrier, identity and lineage validation failures.
        /// </param>
        /// <param name="pending">
        /// Receives related N then U activations in ordinal order within each phase, excluding authenticated completed entries.
        /// </param>
        /// <param name="reactions">
        /// Receives preflighted related replacement applications for R, excluding authenticated completed entries.
        /// </param>
        /// <returns>
        /// True for a supported local cut; false leaves all queued effect work and draft state unchanged.
        /// </returns>
        private bool TryPrepareWoundDependencyCut(
            EffectDraftWoundSelection selection,
            WoundAcceptedTurnInput input, WoundOperationBeforeData before, List<ValidationIssue> failures,
            out IReadOnlyList<AcceptedEffectBoundaryActivation> pending,
            out WoundReactionCutPreparation reactions)
        {
            pending = Array.Empty<AcceptedEffectBoundaryActivation>();
            reactions = WoundReactionCutPreparation.Empty;
            var prefix = selection.Prefix;
            if (prefix.PlanAuthority != AcceptedMechanicsPlanner.CreateEffectPlanAuthority(plan))
                return false;
            if (_woundTriggerJournal.Count == 0 && prefix.AcceptedActivations.Count == 0 && prefix.AppliedComponentEvidence.Count == 0 &&
                prefix.TerminalAvailabilityReservations.Count == 0 && prefix.ReleasedReactions.Count == 0)
                return true;
            var target = input.Opportunities.SingleOrDefault(value =>
                value.OpportunityId == input.Transitions[0].OpportunityId)?.WorseningTarget?.Wound;
            if (target == null || target.Classification.Domain is not ("spiritual" or "physical") || before.WoundCarriers == null ||
                before.EffectCarriers == null || before.EffectIdentity == null ||
                !TryReadPositiveInt(plan.EventInput["turn"], out var acceptedTurn))
                return false;
            var wounds = WoundCarrierCatalog.Build(before.WoundCarriers);
            failures.AddRange(wounds.Issues);
            if (!wounds.TryResolveOne(target.WoundId, out var current) ||
                WoundMaterializationContract.SerializeCanonical(current.Wound) !=
                WoundMaterializationContract.SerializeCanonical(target))
                return false;
            var parsed = ParseIdentity(before.EffectIdentity);
            failures.AddRange(parsed.Issues);
            var carriers = EffectCarrierCatalog.Build(before.EffectCarriers);
            failures.AddRange(carriers.Issues);
            if (parsed.State == null || failures.Count != 0)
                return false;
            ValidationService.ValidateEffectCarrierIndexAgreement(carriers, parsed.State, failures);
            var lineage = WoundEffectLineagePlanner.Plan(current.Wound, parsed.State,
                retirementHistory: _lastWoundInsertion);
            failures.AddRange(lineage.Issues);
            if (failures.Count != 0)
                return false;

            // Full stack coordinates include realm and source group. Conservatively
            // reserving this entire group covers both existing and proposed roots,
            // regardless of definition key, target or stack policy within the group.
            var group = new EffectIdentitySourceGroup(target.Owner.Realm, "wound", target.WoundId);
            var footprint = parsed.State.ResolveSourceGroup(group).Select(entry => entry.EffectId)
                .Concat(lineage.ClosureEffectIds).ToHashSet(StringComparer.Ordinal);
            var accepted = new Dictionary<(EffectEventBoundaryStamp, EffectActivationCandidateIdentity),
                AcceptedEffectBoundaryActivation>();
            var related = new List<AcceptedEffectBoundaryActivation>();
            var remainingUses = new Dictionary<EffectReplayIdentity, int>();
            var pendingEvents = new HashSet<string>(StringComparer.Ordinal);
            foreach (var activation in prefix.AcceptedActivations.OrderBy(value => value.Activation.Stamp.ActivationOrdinal))
            {
                var stamp = activation.Activation.Stamp;
                var key = (activation.Boundary, stamp.Identity);
                if (!accepted.TryAdd(key, activation))
                    return false;
                if (_woundTriggerJournal.TryGetValue(key, out var completed))
                {
                    if (!completed.Matches(activation, prefix.AppliedComponentEvidence, parsed.State,
                            _lastWoundInsertion, _woundReactionJournal))
                        return false;
                    continue;
                }
                if (!carriers.TryResolveOne(stamp.Identity.EffectId, out var occurrence) ||
                    ResolvePendingEffectAuthority(occurrence.Effect, acceptedTurn) != stamp.EffectAuthority)
                    return false;
                if (footprint.Contains(stamp.Identity.EffectId))
                {
                    if (processedEventRefs?.Contains(stamp.Identity.EventRef) == true ||
                        !pendingEvents.Add(stamp.Identity.EventRef))
                        return false;
                    var triggers = (occurrence.Effect["triggers"] as System.Text.Json.Nodes.JsonArray)?
                        .OfType<System.Text.Json.Nodes.JsonObject>().Where(value =>
                            value["triggerId"]?.GetValue<string>() == stamp.Identity.TriggerId).ToArray();
                    if (triggers?.Length != 1 || triggers[0]["eventType"]?.GetValue<string>() != stamp.Identity.EventKind ||
                        triggers[0]["consumeUses"]?.GetValue<bool>() != stamp.ConsumesUse)
                        return false;
                    if (stamp.ConsumesUse)
                    {
                        var subject = new EffectReplayIdentity(stamp.Identity.EffectId, stamp.EffectAuthority);
                        var lifetime = occurrence.Effect["lifetime"];
                        if (lifetime?["mode"]?.GetValue<string>() != "uses" ||
                            lifetime["consumingTriggerIds"] is not System.Text.Json.Nodes.JsonArray consuming ||
                            !consuming.Any(value => value?.GetValue<string>() == stamp.Identity.TriggerId))
                            return false;
                        if (!remainingUses.TryGetValue(subject, out var remaining) &&
                            !TryReadPositiveInt(lifetime["remainingUses"], out remaining))
                            return false;
                        if (remaining <= 0 || stamp.UsesBefore != remaining || activation.Activation.UsesAfter != remaining - 1 ||
                            activation.Activation.EffectTerminal != (remaining == 1))
                            return false;
                        remainingUses[subject] = remaining - 1;
                    }
                    else if (stamp.UsesBefore.HasValue || activation.Activation.UsesAfter.HasValue || activation.Activation.EffectTerminal)
                        return false;
                    related.Add(activation);
                }
            }
            if (_woundTriggerJournal.Keys.Any(key => !accepted.ContainsKey(key)))
                return false;
            foreach (var evidence in prefix.AppliedComponentEvidence)
                if (!accepted.ContainsKey((evidence.Boundary, evidence.Activation)))
                    return false;
            var terminalReservations = new HashSet<(EffectEventBoundaryStamp, EffectActivationCandidateIdentity)>();
            foreach (var reservation in prefix.TerminalAvailabilityReservations)
            {
                if (reservation.Kind != EffectTerminalAvailabilityReservationKind.LastUse ||
                    !accepted.TryGetValue((reservation.Boundary, reservation.Activation.Identity), out var activation) ||
                    reservation.Activation != activation.Activation.Stamp || !activation.Activation.EffectTerminal ||
                    reservation.Subject != new EffectReplayIdentity(activation.Activation.Stamp.Identity.EffectId,
                        activation.Activation.Stamp.EffectAuthority) ||
                    !terminalReservations.Add((reservation.Boundary, reservation.Activation.Identity)))
                    return false;
            }
            if (prefix.AcceptedActivations.Any(value => value.Activation.EffectTerminal &&
                    footprint.Contains(value.Activation.Stamp.Identity.EffectId) &&
                    !terminalReservations.Contains((value.Boundary, value.Activation.Stamp.Identity))))
                return false;
            var pendingReactions = new List<ReleasedEffectReaction>();
            foreach (var released in prefix.ReleasedReactions.OrderBy(value => value.MechanicsOrdinal))
            {
                if (!accepted.TryGetValue((released.Boundary, released.Activation.Identity), out var activation) ||
                    activation.Activation.Stamp != released.Activation)
                    return false;
                var reaction = released.Reaction;
                var downstream = reaction.DownstreamSourceKey ?? reaction.DownstreamSource?.Key;
                var producerIsRelated = footprint.Contains(reaction.EffectId);
                var touchesRelatedGeneration = producerIsRelated ||
                    reaction.ReplacementTarget is { } replacement && footprint.Contains(replacement.EffectId) ||
                    downstream is { Kind: "wound" } && downstream.SourceId == target.WoundId;
                if (!producerIsRelated)
                {
                    if (touchesRelatedGeneration)
                        return false;
                    continue;
                }
                if (_woundReactionJournal.TryGetValue(reaction.EventRef, out var completedReaction))
                {
                    if (!completedReaction.Matches(released, parsed.State))
                        return false;
                    continue;
                }
                if (IsTerminalAvailabilityReaction(reaction) ||
                    !IsReactionBehavior(reaction, EffectReactionResultBehavior.ApplyDefinition))
                    return false;
                pendingReactions.Add(released);
            }
            if (_woundReactionJournal.Keys.Any(eventRef => !prefix.ReleasedReactions.Any(value =>
                    value.Reaction.EventRef == eventRef)))
                return false;
            if (pendingReactions.Count != 0)
            {
                var sources = plan.SourceAuthority;
                WoundReactionLineageAuthority reactionLineage;
                if (selection.Routing?.TryReadCurrentWoundReactionView(out var currentSources,
                        out _, out var currentLineage) == true)
                {
                    sources = currentSources;
                    reactionLineage = currentLineage;
                }
                else
                    reactionLineage = WoundReactionLineageAuthority.Build(sources, parsed.State,
                        carriers, plan.WoundApplicationRootEffectBindings);
                failures.AddRange(reactionLineage.Issues);
                if (failures.Count != 0)
                    return false;
                var plans = PrepareReleasedReactionApplicationPlans(pendingReactions, carriers,
                    sources, reactionLineage, plan.SkillScopeAuthority, failures);
                if (failures.Count != 0 || plans.Count != pendingReactions.Count || plans.Values.Any(value => !value.IsReplacement))
                    return false;
                reactions = new WoundReactionCutPreparation(pendingReactions, plans, sources);
            }
            pending = related.OrderBy(value => value.Activation.Stamp.ConsumesUse)
                .ThenBy(value => value.Activation.Stamp.ActivationOrdinal).ToArray();
            return true;
        }
    }
}
