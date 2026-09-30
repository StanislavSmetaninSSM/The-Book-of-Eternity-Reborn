using System.Reflection;
using System.Text.Json.Nodes;
using BookOfEternityClient.Services;
using Xunit;

namespace BookOfEternityClient.Tests;

public sealed partial class EffectAcceptedTurnPlannerTests
{
    [Fact]
    public void OrdinaryReduction_ApiIsProductionUsableBeforeItCreatesAPlan()
    {
        var productionFixture = CreateOrdinaryReductionFixture(effects: false, ordinaryCount: 1);
        var production = AcceptedMechanicsPlanner.BuildAcceptedPlan(productionFixture.Input,
            AcceptedMechanicsPlanFingerprints.ComputePlanningInput(productionFixture.Input));
        Assert.True(production.Success, DescribeOrdinaryReductionIssues(production.Issues));
        var productionPlan = Assert.IsType<AcceptedMechanicsPlan>(production.Plan);
        Assert.Single(productionPlan.ResourceEvents);

        // Fresh equivalent input: the production control must not consume this evaluation.
        var fixture = CreateOrdinaryReductionFixture(effects: false, ordinaryCount: 1,
            state: productionFixture.Input.PlanningContext!.State,
            history: productionFixture.Input.PlanningContext.History);
        var method = typeof(AcceptedMechanicsPlanner).GetMethod(
            "ReduceAcceptedPlan", BindingFlags.Static | BindingFlags.NonPublic);
        Assert.NotNull(method); // Isolated semantic RED before the new API exists.
        var reduction = method!.Invoke(null,
            new object[] { fixture.Input, AcceptedMechanicsPlanFingerprints.ComputePlanningInput(fixture.Input) });
        Assert.NotNull(reduction);
        var completed = reduction!.GetType().GetProperty("Completed",
            BindingFlags.Instance | BindingFlags.NonPublic)!.GetValue(reduction);
        Assert.NotNull(completed);
        var complete = typeof(AcceptedMechanicsPlanner).GetMethod(
            "CompleteAcceptedReduction", BindingFlags.Static | BindingFlags.NonPublic);
        Assert.NotNull(complete);
        var result = Assert.IsType<AcceptedMechanicsPlanningResult>(
            complete!.Invoke(null, new[] { reduction }));
        Assert.True(result.Success, DescribeOrdinaryReductionIssues(result.Issues));
        Assert.NotNull(result.Plan);
        Assert.Single(result.Plan!.ResourceEvents);
        Assert.Same(result, complete.Invoke(null, new[] { reduction }));
    }

    [Fact]
    public void OrdinaryReduction_RetainsDefinitionsResourcesEffectsAndCompletesOnlyOnce()
    {
        var fixture = CreateOrdinaryReductionFixture(effects: true, createDefinition: true);
        var reduction = AcceptedMechanicsPlanner.ReduceAcceptedPlan(fixture.Input,
            AcceptedMechanicsPlanFingerprints.ComputePlanningInput(fixture.Input));
        Assert.Equal(AcceptedMechanicsPlanner.AcceptedMechanicsReductionKind.CompletedOrdinaryReduction,
            reduction.Kind);
        var completed = Assert.IsType<AcceptedMechanicsPlanner.CompletedOrdinaryMechanicsReduction>(
            reduction.Completed);
        Assert.True(completed.Resources.IsValid);
        Assert.NotEmpty(completed.Resources.AppliedTransitions);
        Assert.Empty(completed.Resources.ReplayTransitions);
        Assert.True(completed.Resources.EffectBoundaryTranscript!.IsComplete);
        Assert.NotNull(completed.Effects);
        Assert.True(completed.Effects!.IsAcceptedBoundaryComplete);
        Assert.True(fixture.ResourceAllocations > 0);
        Assert.True(fixture.Effects.TransitionCalls > 0);
        Assert.Equal(1, fixture.Outcome.ProjectCalls);
        Assert.Same(completed.Resources, fixture.Outcome.ProjectedResources);
        fixture.Outcome.RejectFurtherProjection = true;
        var definitions = completed.Definitions.ToCanonicalJson();
        Assert.Contains("\"resourceKey\":\"mana\"", definitions, StringComparison.Ordinal);
        var history = completed.Resources.HistoryAfterImage!.ToCanonicalJson();
        var effects = completed.Effects.IdentityIndexAfterImage.ToJsonString();
        var before = (fixture.ResourceAllocations, fixture.Effects.EffectCalls, fixture.Effects.TransitionCalls);

        // Existing input and plan getters expose detached roots, not mutable aliases.
        fixture.Input.InternalInputs["changed"] = true;
        fixture.Input.PlanningContext!.DefinitionRoot["definitions"] = new JsonArray();
        completed.Input.InternalInputs["changed"] = true;
        completed.Input.PlanningContext!.EffectIdentityRoot["entries"] = new JsonArray();
        completed.Effects.IdentityIndexAfterImage["entries"] = new JsonArray();

        var first = AcceptedMechanicsPlanner.CompleteAcceptedReduction(reduction);
        Assert.True(first.Success, DescribeOrdinaryReductionIssues(first.Issues));
        var plan = Assert.IsType<AcceptedMechanicsPlan>(first.Plan);
        Assert.Equal(definitions, plan.DefinitionAfterImage.ToJsonString());
        Assert.Equal(history, plan.HistoryAfterImage.ToJsonString());
        Assert.Equal(effects, plan.EffectIdentityAfterImage.ToJsonString());
        Assert.Equal(completed.Resources.Events, plan.ResourceEvents);
        Assert.Equal(completed.Input.PlanningContext!.Owners.Fingerprint, plan.OwnerAuthority.Fingerprint);
        Assert.Same(completed.Effects, plan.EffectPlan);
        for (var index = 0; index < 3; index++)
        {
            var repeated = AcceptedMechanicsPlanner.CompleteAcceptedReduction(reduction);
            Assert.Same(first, repeated);
            Assert.Same(plan, repeated.Plan);
        }
        Assert.Equal(before,
            (fixture.ResourceAllocations, fixture.Effects.EffectCalls, fixture.Effects.TransitionCalls));
    }

    [Fact]
    public void OrdinaryReduction_PendingWaveHasNoCompletedSourceAndNoMechanicalPublication()
    {
        var fixture = CreateOrdinaryReductionFixture(effects: true, bounded: true);
        var reduction = AcceptedMechanicsPlanner.ReduceAcceptedPlan(fixture.Input,
            AcceptedMechanicsPlanFingerprints.ComputePlanningInput(fixture.Input));
        Assert.Equal(AcceptedMechanicsPlanner.AcceptedMechanicsReductionKind.AwaitingResourceReceipt,
            reduction.Kind);
        Assert.Null(reduction.Completed);
        Assert.NotNull(reduction.SelectedResources);
        Assert.NotEmpty(reduction.SelectedResources!.AcceptedPendingResolutions);
        Assert.False(reduction.SelectedResources.EffectBoundaryTranscript!.IsComplete);
        Assert.False(fixture.Input.PlanningContext!.EffectPlan!.IsAcceptedBoundaryComplete);
        Assert.Equal(0, fixture.Outcome.ProjectCalls);
        fixture.Outcome.RejectFurtherProjection = true;
        var counts = (fixture.ResourceAllocations, fixture.Effects.EffectCalls, fixture.Effects.TransitionCalls);
        var first = AcceptedMechanicsPlanner.CompleteAcceptedReduction(reduction);
        Assert.True(first.Success, DescribeOrdinaryReductionIssues(first.Issues));
        var plan = Assert.IsType<AcceptedMechanicsPlan>(first.Plan);
        Assert.True(plan.AwaitsPendingResolution);
        Assert.NotNull(plan.PendingPublicationAuthority);
        Assert.Empty(plan.ResourceEvents);
        Assert.Empty(plan.EffectCarrierAfterImages);
        Assert.Empty(plan.OwnerCompanionAfterImages);
        Assert.Equal(fixture.Input.PlanningContext.State.ToCanonicalJson(), plan.StateAfterImage.ToJsonString());
        Assert.Equal(fixture.Input.PlanningContext.History.ToCanonicalJson(), plan.HistoryAfterImage.ToJsonString());
        Assert.Equal(fixture.Input.PlanningContext.EffectIdentityRoot.ToJsonString(),
            plan.EffectIdentityAfterImage.ToJsonString());
        Assert.Same(first, AcceptedMechanicsPlanner.CompleteAcceptedReduction(reduction));
        Assert.Equal(counts,
            (fixture.ResourceAllocations, fixture.Effects.EffectCalls, fixture.Effects.TransitionCalls));
    }

    [Fact]
    public void OrdinaryReduction_ExposesActualAppliedAndReplayedTransitionsSeparately()
    {
        var fixture = CreateOrdinaryReductionFixture(effects: false, ordinaryCount: 1);
        var first = AcceptedMechanicsPlanner.ReduceAcceptedPlan(fixture.Input,
            AcceptedMechanicsPlanFingerprints.ComputePlanningInput(fixture.Input));
        var accepted = Assert.IsType<AcceptedMechanicsPlanner.CompletedOrdinaryMechanicsReduction>(first.Completed);
        var originalTransition = Assert.Single(accepted.Resources.AppliedTransitions);
        var next = CreateOrdinaryReductionFixture(effects: false, ordinaryCount: 2,
            state: accepted.Resources.StateAfterImage, history: accepted.Resources.HistoryAfterImage);
        // Characterize canonical history replay, not an in-memory prefix continuation.
        var replay = AcceptedMechanicsPlanner.ReduceAcceptedPlan(next.Input,
            AcceptedMechanicsPlanFingerprints.ComputePlanningInput(next.Input));
        var replayed = Assert.IsType<AcceptedMechanicsPlanner.CompletedOrdinaryMechanicsReduction>(replay.Completed);
        Assert.Equal(originalTransition, Assert.Single(replayed.Resources.ReplayTransitions));
        Assert.Single(replayed.Resources.AppliedTransitions);
        Assert.Single(replayed.Resources.Events);
        var count = next.ResourceAllocations;
        var final = AcceptedMechanicsPlanner.CompleteAcceptedReduction(replay);
        Assert.True(final.Success, DescribeOrdinaryReductionIssues(final.Issues));
        Assert.Equal(replayed.Resources.HistoryAfterImage!.ToCanonicalJson(),
            final.Plan!.HistoryAfterImage.ToJsonString());
        Assert.Same(final, AcceptedMechanicsPlanner.CompleteAcceptedReduction(replay));
        Assert.Equal(count, next.ResourceAllocations);
    }

    [Fact]
    public void OrdinaryReduction_ExistingWrapperUsesSameSemanticReductionAndNeverReallocatesOnCompletion()
    {
        var direct = CreateOrdinaryReductionFixture(effects: false, ordinaryCount: 1);
        var wrapped = CreateOrdinaryReductionFixture(effects: false, ordinaryCount: 1,
            state: direct.Input.PlanningContext!.State,
            history: direct.Input.PlanningContext.History);
        var reduced = AcceptedMechanicsPlanner.ReduceAcceptedPlan(direct.Input,
            AcceptedMechanicsPlanFingerprints.ComputePlanningInput(direct.Input));
        var result = AcceptedMechanicsPlanner.CompleteAcceptedReduction(reduced);
        var existing = AcceptedMechanicsPlanner.BuildAcceptedPlan(wrapped.Input,
            AcceptedMechanicsPlanFingerprints.ComputePlanningInput(wrapped.Input));
        Assert.True(result.Success, DescribeOrdinaryReductionIssues(result.Issues));
        Assert.True(existing.Success, DescribeOrdinaryReductionIssues(existing.Issues));
        // Shared captured baseline plus identical scripted new IDs: bootstrap IDs are random.
        Assert.Equal(result.Plan!.PreparedPlanFingerprint, existing.Plan!.PreparedPlanFingerprint);
        Assert.Equal(direct.ResourceAllocations, wrapped.ResourceAllocations);
        Assert.Same(result, AcceptedMechanicsPlanner.CompleteAcceptedReduction(reduced));
        Assert.Equal(direct.ResourceAllocations, wrapped.ResourceAllocations);
    }

    [Fact]
    public void OrdinaryReduction_RejectedResourcesNeverExportCompletedOrProjectAnOutcome()
    {
        var fixture = CreateOrdinaryReductionFixture(effects: false, ordinaryCount: 1, rejectSource: true);
        var reduction = AcceptedMechanicsPlanner.ReduceAcceptedPlan(fixture.Input,
            AcceptedMechanicsPlanFingerprints.ComputePlanningInput(fixture.Input));
        Assert.Equal(AcceptedMechanicsPlanner.AcceptedMechanicsReductionKind.Rejected, reduction.Kind);
        Assert.Null(reduction.Completed);
        Assert.Contains(reduction.Issues, issue => issue.Code == "resource_source_unknown");
        Assert.Equal(0, fixture.Outcome.ProjectCalls);
        fixture.Outcome.RejectFurtherProjection = true;
        var count = fixture.ResourceAllocations;
        var result = AcceptedMechanicsPlanner.CompleteAcceptedReduction(reduction);
        Assert.False(result.Success);
        Assert.Null(result.Plan);
        Assert.Equal(reduction.Issues, result.Issues);
        Assert.Same(result, AcceptedMechanicsPlanner.CompleteAcceptedReduction(reduction));
        Assert.Equal(count, fixture.ResourceAllocations);
    }

    private static OrdinaryReductionFixture CreateOrdinaryReductionFixture(
        bool effects, bool bounded = false, bool createDefinition = false, int ordinaryCount = 0,
        ResourceStateLedger? state = null, ResourceHistoryState? history = null, bool rejectSource = false)
    {
        var bootstrap = ResourceBootstrapStateBuilder.BuildMortalPlayer(1, 41, 10, 10, 10, 10, 10);
        Assert.True(bootstrap.IsValid, DescribeOrdinaryReductionIssues(bootstrap.Issues));
        var definitions = bootstrap.Definitions!;
        state ??= bootstrap.State!;
        history ??= bootstrap.History!;
        var owners = ResourceOwnerAuthority.CreateCurrentPlayerAuthority(definitions);
        var fixture = new OrdinaryReductionFixture();
        EffectAcceptedTurnPlan? effectPlan = null;
        var effectCommands = new JsonObject();
        var events = new JsonObject { ["turn"] = 42, ["events"] = new JsonArray() };
        var identity = new JsonObject { ["schemaVersion"] = 1, ["entries"] = new JsonArray() };
        if (effects)
        {
            var definition = EffectMaterializationTestFixture.CreateDefinition("periodic_damage");
            definition["triggers"]![0]!["resolutionMode"] = bounded ? "bounded_receipt" : "deterministic";
            var effect = EffectMaterializationTestFixture.CreateCanonicalEffect(profile: "periodic_damage");
            effect["triggers"] = definition["triggers"]!.DeepClone();
            var effectInput = CreateReactionInput(effect, definition);
            var lifecycle = effectInput.EventInput["lifecycleEvents"]![0]!;
            lifecycle["eventRef"] = "turn_42:effect:on_owner_turn_end:1";
            lifecycle["causalEventRef"] = "turn_42:owner_turn_end:1";
            lifecycle["phase"] = "owner_turn_end";
            lifecycle["triggerId"] = "on_owner_turn_end";
            var result = new EffectAcceptedTurnPlanCache(fixture.Effects).GetOrBuild(effectInput);
            Assert.True(result.Success, DescribeOrdinaryReductionIssues(result.Issues));
            effectPlan = result.Plan!;
            effectCommands = effectInput.RawCommands.DeepClone().AsObject();
            events = effectInput.EventInput.DeepClone().AsObject();
            identity = effectInput.PreTurnIdentityIndex!.DeepClone().AsObject();
        }
        var changes = new JsonArray();
        var exports = new List<ResourceMutationSourceExport>();
        for (var index = 0; index < ordinaryCount; index++)
        {
            var eventRef = $"turn_42:resource:{index + 1}";
            changes.Add(new JsonObject
            {
                ["operation"] = "damage",
                ["target"] = new JsonObject { ["kind"] = "player", ["targetId"] = "player_current" },
                ["resourceKey"] = "health", ["amount"] = 1,
                ["source"] = new JsonObject { ["kind"] = "combat_outcome" },
                ["eventRef"] = eventRef, ["reason"] = "Accepted ordinary reduction characterization"
            });
            exports.Add(new ResourceMutationSourceExport("combat_outcome", eventRef, ReductionHash,
                ResourceMutationSourceState.Active, false)
            {
                BoundOwner = new ResourceOwnerKey("mortal_world", ResourceOwnerKind.Player, "player_current")
            });
        }
        var commandsRoot = new JsonObject { ["resourceChanges"] = changes };
        if (createDefinition)
            commandsRoot["resourceDefinitionCreations"] = JsonNode.Parse("""
            [{
              "definitionRef":"mana_v1","eventRef":"turn_42:resource:1","reason":"Setting materialization",
              "definition":{
                "resourceKey":"mana","definitionVersion":1,"displayName":"Mana",
                "numericKind":"integer","unit":"point","quantum":1,
                "minimumPolicy":{"kind":"definition_fixed","value":0},
                "capacityPolicy":{"kind":"instance_fixed"},
                "initializationPolicy":{"kind":"maximum"},
                "allowedOwnerKinds":["player"],"allowedOperations":["spend","gain"],
                "defaultFloorPolicy":"reject_below_minimum","defaultCapPolicy":"clamp_to_maximum",
                "visibility":"player_visible"
              }
            }]
            """);
        var commands = ResourceAcceptedTurnInputComposer.Parse(commandsRoot.ToJsonString());
        Assert.True(commands.IsValid, DescribeOrdinaryReductionIssues(commands.Issues));
        var sources = ResourceMutationSourceCatalog.Create(
            rejectSource ? Array.Empty<ResourceMutationSourceExport>() : exports.ToArray());
        Assert.NotNull(sources.Catalog);
        Assert.Empty(sources.Issues);
        var context = new AcceptedMechanicsPlanningContext(
            definitions.ToCanonicalRoot(), definitions, state, history, owners, sources.Catalog!,
            commands, identity, effectPlan,
            registeredSystemOutcomes: new[] { fixture.Outcome },
            resourceIdentityFactory: new AcceptedMechanicsIdentityFactory(() =>
                new Guid(++fixture.ResourceAllocations, 0, 0, new byte[8])),
            effectIdentityFactory: fixture.Effects);
        var beforeImages = new Dictionary<string, CanonicalBeforeImage>(StringComparer.Ordinal);
        foreach (var path in new[]
        {
            ResourceMaterializationContract.DefinitionsPath, ResourceMaterializationContract.StatePath,
            ResourceMaterializationContract.HistoryPath, ResourceMaterializationContract.CommandPath,
            CanonicalResourceOwnerAuthorityComposer.AuthorityPath, EffectAcceptedTurnPlan.IdentityIndexPath,
            EffectAcceptedTurnPlan.CommandPath, EffectCarrierCatalog.PlayerPath,
            ResourcePendingResolutionState.PendingPath
        })
            beforeImages[path] = new CanonicalBeforeImage(false, null);
        fixture.Input = new AcceptedMechanicsInput(
            "session_effect_reaction", "request_reduction", "snapshot_effect_reaction", "mortal_world", 42,
            events, commands.Root, effectCommands, new JsonObject(), new JsonObject(),
            new AcceptedMechanicsAuthorityFingerprints(
                ReductionHash, owners.Fingerprint, state.Fingerprint, history.Fingerprint,
                ReductionHash, ReductionHash, ReductionHash, ReductionHash, ReductionHash,
                ReductionHash, ReductionHash, ReductionHash, ReductionHash, ReductionHash, ReductionHash),
            beforeImages, Array.Empty<ValidationIssue>(), context);
        return fixture;
    }

    private const string ReductionHash =
        "sha256:aaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaa";
    private static string DescribeOrdinaryReductionIssues(IEnumerable<ValidationIssue> issues) =>
        string.Join(Environment.NewLine, issues.Select(issue => $"{issue.Code}: {issue.Expected}; {issue.Actual}"));

    private sealed class OrdinaryReductionFixture
    {
        internal AcceptedMechanicsInput Input { get; set; } = null!;
        internal int ResourceAllocations;
        internal CountingFactory Effects { get; } = new();
        internal ReductionProjectionObserver Outcome { get; } = new();
    }

    // Observation only: no invented resource/effect behavior. Every mutation and
    // trigger above still runs through the real production planner and reducer.
    private sealed class ReductionProjectionObserver : IResourceRegisteredSystemOutcomeDraft
    {
        internal int ProjectCalls { get; private set; }
        internal bool RejectFurtherProjection { get; set; }
        internal AcceptedMechanicsResourcePlanningResult? ProjectedResources { get; private set; }
        public string Fingerprint => ReductionHash;
        public IReadOnlyList<ResourceMutationSourceExport> SourceExports =>
            Array.Empty<ResourceMutationSourceExport>();
        public IReadOnlyList<ResourceMutationIntent> Mutations =>
            Array.Empty<ResourceMutationIntent>();
        public IReadOnlyDictionary<string, CanonicalBeforeImage> ExpectedBeforeImages =>
            new Dictionary<string, CanonicalBeforeImage>(StringComparer.Ordinal);

        public ResourceRegisteredSystemOutcomeProjectionResult Project(
            AcceptedMechanicsResourcePlanningResult resources)
        {
            Assert.False(RejectFurtherProjection, "Assembly must not project registered outcomes again.");
            ProjectCalls++;
            ProjectedResources = resources;
            return new ResourceRegisteredSystemOutcomeProjectionResult(
                new Dictionary<string, JsonObject>(StringComparer.Ordinal),
                Array.Empty<AcceptedMechanicsOwnerTransition>(),
                Array.Empty<ValidationIssue>());
        }
    }
}
