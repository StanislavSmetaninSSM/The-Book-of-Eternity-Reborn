using System.Text.Json.Nodes;

namespace BookOfEternityClient.Services;

internal static partial class EffectAcceptedTurnPlanner
{
    internal sealed partial class EffectAcceptedDraft
    {
        // Runs inside ApplyPreparedWound's SAME busy guard, workspace, identity
        // writer and version. It does not invoke a second BuildCore/executor.
        private EffectDraftWoundInsertion? ApplyPreparedCombatHealing(EffectDraftWoundBeforeAuthority before,
            WoundPreparedAcceptedTurnPlan prepared, List<ValidationIssue> failures)
        {
            if (_busy != 1 || !before.IsCurrent || !before.MatchesPrepared(prepared) ||
                prepared.CombatHealingAuthority is not { } selection || !ReferenceEquals(before.Selection.Combat, selection) ||
                !selection.IsCurrentFor(this) || !selection.PreparedAgrees(prepared))
                throw new InvalidOperationException("Actual current combat preparation and draft required.");
            failures.AddRange(WoundAcceptedTurnPlannerCore.ValidatePreparedAuthority(prepared));
            if (failures.Count != 0) return null;
            var events = plan.EventInput;
            events["events"] = new JsonArray(prepared.Binding.AcceptedEvents.Select(e => (JsonNode)new JsonObject
                { ["eventRef"] = e.EventRef, ["kind"] = e.Kind, ["authorityId"] = e.AuthorityId }).ToArray());
            var sources = plan.SourceAuthority;
            if (selection.Routing?.TryReadCurrentWoundReactionView(out var currentSources, out _, out _) == true)
                sources = currentSources;
            var input = new EffectAcceptedTurnInput(prepared.Binding.SessionId, prepared.Binding.SnapshotToken,
                new JsonObject(), sources, plan.TargetAuthority, events, Realm: prepared.Binding.Realm, SkillScopeAuthority: plan.SkillScopeAuthority);
            var applicationStart = workspace.ApplicationCount; var writeStart = identityRoot.WriteCount;
            var allocationStart = identityRoot.AllocationCount; var carrierStart = workspace.EditCount;
            try
            {
                var executed = new List<WoundExecutedBoundary>(); var current = selection.Target;
                var batches = new List<WoundEffectOperationBatch>();
                var roots = new List<WoundApplicationRootEffectBinding>();
                var stages = selection.Stages;
                for (var index = 0; index < stages.Count; index++)
                {
                    var stage = stages[index];
                    var result = ExecuteSpiritualWoundStage(input, current, selection.ResolveGraph(current, stage), stage, index,
                        new(selection.EventRef, selection.SourceFingerprint, "Исцеление в духовном бою", "spiritual_combat_healing",
                            prepared.Binding.AcceptedEvents, null, batches.Add), workspace, identityRoot, identityFactory, turn, effectIds,
                        transitionIds, activeEffects, processedEventRefs, usedSources, usedTargets, roots, failures, before.RetirementHistory);
                    if (result is null || failures.Count != 0) { _faulted = true; return null; }
                    executed.Add(result); current = result.After;
                }
                identityState = ParseIdentity(identityRoot.ReadSnapshot()); failures.AddRange(identityState.Issues);
                ValidateAfterImages(workspace, identityRoot, activeEffects, failures);
                if (failures.Count != 0) { _faulted = true; return null; }
                var reduced = WoundAcceptedTurnPlannerCore.ReduceCombatHealingState(prepared, before, executed);
                failures.AddRange(reduced.Issues);
                if (reduced.State is null || failures.Count != 0) { _faulted = true; return null; }
                var catalog = WoundCarrierCatalog.Build(reduced.State.Carriers); failures.AddRange(catalog.Issues);
                var identity = WoundIdentityState.Parse(reduced.State.Identity.ToJsonString(), WoundIdentityState.StatePath);
                var history = WoundHistoryState.Parse(reduced.State.History.ToJsonString(), WoundHistoryState.HistoryPath);
                failures.AddRange(identity.Issues); failures.AddRange(history.Issues);
                if (identity.State is null || history.State is null || failures.Count != 0) { _faulted = true; return null; }
                failures.AddRange(history.State.ValidateAgreement(identity.State, catalog));
                if (current.Lifecycle == "healed")
                {
                    if (catalog.Occurrences.Any(w => w.WoundId == current.WoundId) ||
                        !identity.State.TryGetEntry(current.WoundId, out var entry) || entry.Status != "healed" ||
                        entry.TerminalTransitionId != current.LastTransition.TransitionId)
                        throw new InvalidDataException("Terminal combat wound must be absent with exact terminal identity/History.");
                }
                else if (!catalog.TryResolveOne(current.WoundId, out var actual) ||
                    WoundMaterializationContract.SerializeCanonical(actual.Wound) != WoundMaterializationContract.SerializeCanonical(current))
                    throw new InvalidDataException("Combat wound differs from its actual executed boundary.");
                if (failures.Count != 0) { _faulted = true; return null; }
                _pendingWoundInsertion = new(before, prepared, reduced.State, current, executed.SelectMany(s => s.Applications).ToArray(),
                    applicationStart, writeStart, allocationStart, carrierStart, executed, batches);
                return EffectDraftWoundInsertion.Retain(this);
            }
            catch { _faulted = true; throw; }
            finally { _pendingWoundInsertion = null; }
        }
    }
}
