using System.Text.Json.Nodes;
using BookOfEternityClient.Services;
using Xunit;

namespace BookOfEternityClient.Tests;

public sealed partial class WoundEffectBatchPlannerTests
{
    [Fact]
    public void EffectStage_UsesSealedSameTurnRootMapForWoundReactionPlanning()
    {
        var prepared = AssertPrepared(WoundAcceptedTurnPlanner.Prepare(
            CreateInput(1, CandidateShape.ReactionWithMarkerLeaf)));
        var effectInput = CreateEffectInput(
            prepared,
            mutateEventInput: eventInput =>
                eventInput["lifecycleEvents"] = new JsonArray(new JsonObject
                {
                    ["eventRef"] = "turn_42:wound:reaction_trigger",
                    ["turn"] = prepared.Binding.Turn,
                    ["phase"] = "owner_damaged",
                    ["realm"] = "mortal_world",
                    ["target"] = new JsonObject
                    {
                        ["kind"] = "player",
                        ["targetId"] = "player_current"
                    }
                }));

        var stage = WoundEffectBatchPlanner.Build(
            prepared,
            effectInput,
            new ScriptedEffectIdentityFactory("same_turn_reaction"));
        var accepted = AssertEffectPlan(stage);

        var rootBinding = Assert.Single(
            accepted.EffectPlan.WoundApplicationRootEffectBindings);
        var reaction = Assert.Single(accepted.EffectPlan.DeferredReactions);
        Assert.Equal(rootBinding.EffectId, reaction.EffectId);
        Assert.Equal("apply_definition", reaction.ResultKind);
        Assert.Equal("wound", reaction.DownstreamSourceKey?.Kind);
    }

    [Fact]
    public void EffectPlan_ResourceRoutingPassesSealedWoundLineageAuthority()
    {
        const string eventType = "resource_depleted";
        var prepared = AssertPrepared(WoundAcceptedTurnPlanner.Prepare(
            CreateInput(
                1,
                CandidateShape.ReactionWithMarkerLeaf,
                reactionEventType: eventType)));
        var stage = BuildEffectStage(
            prepared,
            new ScriptedEffectIdentityFactory("resource_reaction"));
        var plan = AssertEffectPlan(stage.Result).EffectPlan;
        var coordinate = new ResourceCoordinate(
            "mortal_world",
            ResourceOwnerKind.Player,
            "player_current",
            "health");
        var candidate = Assert.Single(
            plan.ResourceTriggerIndex.ResolveResourceEvent(coordinate, eventType));
        var routing = candidate.OpenRoutingHandle(
            new EffectAcceptedTurnPlanner.EffectResourceRoutingWorkMeter());
        var carriers = EffectCarrierCatalog.Build(plan.ResourceTriggerCarriers);
        var lineageAuthority = WoundReactionLineageAuthority.Build(
            plan.SourceAuthority,
            ParseEffectIdentityState(plan.IdentityIndexAfterImage),
            carriers,
            plan.WoundApplicationRootEffectBindings);
        var producer = new ResourceAppliedEvent(
            eventType,
            "resource_operation_same_turn_wound",
            "turn_42:resource:same_turn_wound",
            coordinate,
            Before: 1m,
            After: 0m,
            AppliedAmount: 1m,
            Turn: prepared.Binding.Turn,
            ExecutionSequence: 1,
            SourceFingerprint: "source_fingerprint_same_turn_wound");

        var reactions = routing.PlanResourceEvent(
            candidate.TriggerId,
            producer,
            plan.SourceAuthority,
            lineageAuthority);

        Assert.True(
            reactions.Success,
            string.Join(Environment.NewLine, reactions.Issues.Select(static issue =>
                $"{issue.Code}: expected={issue.Expected}; actual={issue.Actual}")));
        Assert.Equal(
            "apply_definition",
            Assert.Single(reactions.Executions).ResultKind);
    }

    [Fact]
    public void FinalizeReaction_RevalidatesTypedWoundEdgeBeforeCreatingDescendant()
    {
        var prepared = AssertPrepared(WoundAcceptedTurnPlanner.Prepare(
            CreateInput(1, CandidateShape.ReactionWithMarkerLeaf)));
        var effectInput = CreateEffectInput(
            prepared,
            mutateEventInput: eventInput =>
                eventInput["lifecycleEvents"] = new JsonArray(new JsonObject
                {
                    ["eventRef"] = "turn_42:wound:reaction_finalize",
                    ["causalEventRef"] = "turn_42:wound:reaction_cause",
                    ["turn"] = prepared.Binding.Turn,
                    ["phase"] = "owner_damaged",
                    ["realm"] = "mortal_world",
                    ["target"] = new JsonObject
                    {
                        ["kind"] = "player",
                        ["targetId"] = "player_current"
                    }
                }));
        var stage = WoundEffectBatchPlanner.Build(
            prepared,
            effectInput,
            new ScriptedEffectIdentityFactory("finalize_reaction_root"));
        var plan = AssertEffectPlan(stage).EffectPlan;
        var definitions = ResourceDefinitionCatalog.CreateBuiltIn();
        var due = EffectAcceptedTurnPlanner.ResolveDuePeriodicResourceMutations(
            plan,
            ResourceOwnerAuthority.CreateCurrentPlayerAuthority(definitions),
            definitions);
        Assert.True(
            due.IsValid,
            string.Join(Environment.NewLine, due.Issues.Select(static issue =>
                $"{issue.Code}: expected={issue.Expected}; actual={issue.Actual}")));
        var bootstrap = ResourceBootstrapStateBuilder.BuildPristine();
        Assert.True(bootstrap.IsValid);
        var sources = ResourceMutationSourceCatalog.Create(due.SourceExports);
        Assert.True(sources.IsValid);
        var resources = AcceptedMechanicsPlanner.BuildResources(
            new AcceptedMechanicsResourceInput(
                Turn: prepared.Binding.Turn,
                Definitions: bootstrap.Definitions!,
                State: bootstrap.State!,
                History: bootstrap.History!,
                Sources: sources.Catalog!,
                Mutations: due.Mutations,
                InitialTriggerCandidates: due.TriggerCandidates,
                InitialEffectResolutionWork: due.Work,
                EffectPlanAuthority:
                    AcceptedMechanicsPlanner.CreateEffectPlanAuthority(plan)),
            new AcceptedMechanicsIdentityFactory());
        Assert.True(
            resources.IsValid,
            string.Join(Environment.NewLine, resources.Issues.Select(static issue =>
                $"{issue.Code}: expected={issue.Expected}; actual={issue.Actual}")));

        var finalized = EffectAcceptedTurnPlanner.CompleteAcceptedBoundaryTranscript(
            plan,
            resources.EffectBoundaryTranscript,
            new ScriptedEffectIdentityFactory("finalize_reaction_child"));

        Assert.True(
            finalized.Success,
            string.Join(Environment.NewLine, finalized.Issues.Select(static issue =>
                $"{issue.Code}: expected={issue.Expected}; actual={issue.Actual}")));
        var finalPlan = Assert.IsType<EffectAcceptedTurnPlan>(finalized.Plan);
        var rootEffectId = Assert.Single(
            plan.WoundApplicationRootEffectBindings).EffectId;
        var child = Assert.Single(
            finalPlan.ResourceTriggerCarriers.PlayerEffects!["activeEffects"]!
                .AsArray()
                .OfType<JsonObject>(),
            effect => !string.Equals(
                effect["effectId"]!.GetValue<string>(),
                rootEffectId,
                StringComparison.Ordinal));
        var childId = child["effectId"]!.GetValue<string>();
        var childIdentity = Assert.Single(
            finalPlan.IdentityIndexAfterImage["entries"]!
                .AsArray()
                .OfType<JsonObject>(),
            entry => string.Equals(
                entry["effectId"]!.GetValue<string>(),
                childId,
                StringComparison.Ordinal));
        Assert.Equal(
            rootEffectId,
            Assert.Single(childIdentity["transitions"]![0]!["sourceEffectIds"]!
                .AsArray())!.GetValue<string>());
    }

    [Fact]
    public void FinalizeReaction_RejectsWoundProducerComponentDriftBeforeMutation()
    {
        var prepared = AssertPrepared(WoundAcceptedTurnPlanner.Prepare(
            CreateInput(1, CandidateShape.ReactionWithMarkerLeaf)));
        var effectInput = CreateEffectInput(
            prepared,
            mutateEventInput: eventInput =>
                eventInput["lifecycleEvents"] = new JsonArray(new JsonObject
                {
                    ["eventRef"] = "turn_42:wound:reaction_drift",
                    ["turn"] = prepared.Binding.Turn,
                    ["phase"] = "owner_damaged",
                    ["realm"] = "mortal_world",
                    ["target"] = new JsonObject
                    {
                        ["kind"] = "player",
                        ["targetId"] = "player_current"
                    }
                }));
        var original = AssertEffectPlan(WoundEffectBatchPlanner.Build(
            prepared,
            effectInput,
            new ScriptedEffectIdentityFactory("reaction_drift_root"))).EffectPlan;
        var rootEffectId = Assert.Single(
            original.WoundApplicationRootEffectBindings).EffectId;
        var rootDefinitionKey = Assert.Single(original.ActiveEffects)
            ["source"]!["definitionKey"]!.GetValue<string>();
        var forged = CloneEffectPlanWithResourceCarrierMutation(
            original,
            rootEffectId,
            effect => effect["components"]![0]!["payload"]!["definitionKey"] =
                rootDefinitionKey);
        var definitions = ResourceDefinitionCatalog.CreateBuiltIn();
        var due = EffectAcceptedTurnPlanner.ResolveDuePeriodicResourceMutations(
            forged,
            ResourceOwnerAuthority.CreateCurrentPlayerAuthority(definitions),
            definitions);
        Assert.True(
            due.IsValid,
            string.Join(Environment.NewLine, due.Issues.Select(static issue =>
                $"{issue.Code}: expected={issue.Expected}; actual={issue.Actual}")));
        var bootstrap = ResourceBootstrapStateBuilder.BuildPristine();
        var sources = ResourceMutationSourceCatalog.Create(due.SourceExports);
        var resources = AcceptedMechanicsPlanner.BuildResources(
            new AcceptedMechanicsResourceInput(
                Turn: prepared.Binding.Turn,
                Definitions: bootstrap.Definitions!,
                State: bootstrap.State!,
                History: bootstrap.History!,
                Sources: sources.Catalog!,
                Mutations: due.Mutations,
                InitialTriggerCandidates: due.TriggerCandidates,
                InitialEffectResolutionWork: due.Work,
                EffectPlanAuthority:
                    AcceptedMechanicsPlanner.CreateEffectPlanAuthority(forged)),
            new AcceptedMechanicsIdentityFactory());
        Assert.True(resources.IsValid);

        var finalized = EffectAcceptedTurnPlanner.CompleteAcceptedBoundaryTranscript(
            forged,
            resources.EffectBoundaryTranscript,
            new ScriptedEffectIdentityFactory("reaction_drift_child"));

        Assert.False(finalized.Success);
        Assert.Contains(finalized.Issues, static issue =>
            issue.Code is "effect_reaction_wound_lineage_edge_invalid" or
                "accepted_mechanics_wound_lineage_identity_authority_mismatch" or
                "accepted_mechanics_wound_terminal_occurrence_mismatch");
    }

    [Fact]
    public void EffectPlan_SealsExactApplicationRefToAllocatedRootIdentityMap()
    {
        var prepared = AssertPrepared(WoundAcceptedTurnPlanner.Prepare(
            CreateInput(1, CandidateShape.ReactionWithMarkerLeaf)));
        var stage = BuildEffectStage(
            prepared,
            new ScriptedEffectIdentityFactory("reaction_root_map"));
        var accepted = AssertEffectPlan(stage.Result);
        var root = Assert.Single(Assert.Single(prepared.EffectOperationBatches)
            .RootApplications);
        var applicationResult = Assert.Single(accepted.ApplicationResults);

        var binding = Assert.Single(
            accepted.EffectPlan.WoundApplicationRootEffectBindings);

        Assert.Equal(root.ApplicationRef, binding.ApplicationRef);
        Assert.Equal(applicationResult.EffectId, binding.EffectId);
        var beforeSeal = WoundAcceptedTurnFingerprints.ComputeEffectPlan(
            prepared,
            accepted.EffectInput,
            accepted.EffectPlan,
            accepted.ApplicationResults,
            accepted.TerminationResults);
        var changedPlan = CloneEffectPlanWithWoundRootBindings(
            accepted.EffectPlan,
            new[]
            {
                binding with { EffectId = "effect_forged_reaction_root" }
            });
        var afterSeal = WoundAcceptedTurnFingerprints.ComputeEffectPlan(
            prepared,
            accepted.EffectInput,
            changedPlan,
            accepted.ApplicationResults,
            accepted.TerminationResults);

        Assert.NotEqual(beforeSeal, afterSeal);
        Assert.Equal(
            binding,
            Assert.Single(accepted.EffectPlan.WoundApplicationRootEffectBindings));
    }

    private static EffectAcceptedTurnPlan CloneEffectPlanWithWoundRootBindings(
        EffectAcceptedTurnPlan source,
        IReadOnlyList<WoundApplicationRootEffectBinding> bindings) =>
        new(
            source.InputFingerprint,
            source.CarrierAuthorityFingerprint,
            source.SourceAuthorityFingerprint,
            source.TargetAuthorityFingerprint,
            source.AllocatedCombatantIds,
            source.AllocatedEffectIds,
            source.AllocatedTransitionIds,
            source.Sources,
            source.Targets,
            source.SourceBindings,
            source.DeferredReactions,
            source.ReactionExpansionCount,
            source.ReactionExpansionUsage,
            source.ActiveEffects,
            source.ResourceTriggerCarriers,
            source.SourceAuthority,
            source.TargetAuthority,
            source.EventInput,
            source.CarrierBeforeImages,
            source.CarrierAfterImages,
            source.IdentityIndexBeforeImage,
            source.IdentityIndexAfterImage,
            source.TouchedPaths,
            source.DeletedPaths,
            source.AcceptedCarrierBaselines,
            acceptedBoundaryCompletionProof:
                ReadAcceptedBoundaryCompletionProof(source),
            acceptedBoundaryBasePlanFingerprint:
                source.AcceptedBoundaryBasePlanFingerprint,
            woundApplicationRootEffectBindings: bindings);

    private static EffectAcceptedTurnPlan CloneEffectPlanWithResourceCarrierMutation(
        EffectAcceptedTurnPlan source,
        string effectId,
        Action<JsonObject> mutate)
    {
        var activeEffects = source.ActiveEffects
            .Select(static effect => effect.DeepClone().AsObject())
            .ToArray();
        MutateActiveEffect(activeEffects, effectId, mutate);
        var resourcePlayer = source.ResourceTriggerCarriers.PlayerEffects!
            .DeepClone().AsObject();
        MutateActiveEffect(
            resourcePlayer["activeEffects"]!.AsArray().OfType<JsonObject>(),
            effectId,
            mutate);
        var resourceCarriers = source.ResourceTriggerCarriers with
        {
            PlayerEffects = resourcePlayer
        };
        var carrierAfterImages = source.CarrierAfterImages.ToDictionary(
            static pair => pair.Key,
            static pair => pair.Value.DeepClone().AsObject(),
            StringComparer.Ordinal);
        MutateActiveEffect(
            carrierAfterImages[EffectCarrierCatalog.PlayerPath]["activeEffects"]!
                .AsArray().OfType<JsonObject>(),
            effectId,
            mutate);
        return new EffectAcceptedTurnPlan(
            source.InputFingerprint,
            source.CarrierAuthorityFingerprint,
            source.SourceAuthorityFingerprint,
            source.TargetAuthorityFingerprint,
            source.AllocatedCombatantIds,
            source.AllocatedEffectIds,
            source.AllocatedTransitionIds,
            source.Sources,
            source.Targets,
            source.SourceBindings,
            source.DeferredReactions,
            source.ReactionExpansionCount,
            source.ReactionExpansionUsage,
            activeEffects,
            resourceCarriers,
            source.SourceAuthority,
            source.TargetAuthority,
            source.EventInput,
            source.CarrierBeforeImages,
            carrierAfterImages,
            source.IdentityIndexBeforeImage,
            source.IdentityIndexAfterImage,
            source.TouchedPaths,
            source.DeletedPaths,
            source.AcceptedCarrierBaselines,
            acceptedBoundaryCompletionProof:
                ReadAcceptedBoundaryCompletionProof(source),
            acceptedBoundaryBasePlanFingerprint:
                source.AcceptedBoundaryBasePlanFingerprint,
            woundApplicationRootEffectBindings:
                source.WoundApplicationRootEffectBindings);
    }
}
