using System.Text.Json.Nodes;
using BookOfEternityClient.Services;
using Xunit;

namespace BookOfEternityClient.Tests;

public sealed partial class EffectAcceptedTurnPlannerTests
{
    [Theory]
    [InlineData("periodic_spend", "energy", true, false)]
    [InlineData("periodic_gain", "energy", true, false)]
    [InlineData("periodic_spend", "health", false, false)]
    [InlineData("periodic_gain", "health", false, false)]
    [InlineData("periodic_spend", "energy", true, true)]
    [InlineData("periodic_gain", "energy", true, true)]
    public void SpendGain_ActualPlannerPreservesDefinitionOperations(string profile, string resource, bool allowed, bool reaction)
    {
        var definition = EffectMaterializationTestFixture.CreateDefinition("periodic_restore");
        var component = definition["components"]![0]!;
        component["profile"] = profile;
        component["payload"]!["resource"] = resource;
        component["payload"]!["amount"] = 1;
        if (profile == "periodic_spend")
        {
            component["payload"]!.AsObject().Remove("capPolicy");
            component["payload"]!["floorPolicy"] = "registered_resource_floor";
        }
        if (reaction)
        {
            var routed = component.DeepClone();
            routed["componentId"] = "routed_resource";
            definition["components"]!.AsArray().Add(routed);
            definition["components"]!.AsArray().Add(new JsonObject
            {
                ["componentId"] = "route_resource", ["profile"] = "event_reaction", ["priority"] = 101,
                ["payload"] = new JsonObject
                {
                    ["eventType"] = "owner_turn_end", ["resultKind"] = "trigger_component",
                    ["componentId"] = "routed_resource", ["dependency"] = "after_component",
                    ["afterComponentId"] = "component_001", ["maxExpansion"] = 1
                }
            });
            definition["triggers"]![0]!["componentIds"] = new JsonArray("component_001", "route_resource");
        }
        var effect = EffectMaterializationTestFixture.CreateCanonicalEffect(profile: "periodic_restore");
        effect["components"] = definition["components"]!.DeepClone();
        effect["triggers"] = definition["triggers"]!.DeepClone();
        var input = CreateReactionInput(effect, definition);
        var lifecycle = input.EventInput["lifecycleEvents"]![0]!;
        lifecycle["eventRef"] = "turn_42:effect:on_owner_turn_end:1";
        lifecycle["causalEventRef"] = "turn_42:owner_turn_end:1";
        lifecycle["phase"] = "owner_turn_end";
        lifecycle["triggerId"] = "on_owner_turn_end";
        var planned = new EffectAcceptedTurnPlanCache().GetOrBuild(input);
        Assert.True(planned.Success, string.Join(Environment.NewLine, planned.Issues));
        var bootstrap = ResourceBootstrapStateBuilder.BuildMortalPlayer(1, 41, 10, 10, 10, 10, 10);
        Assert.True(bootstrap.IsValid);
        var due = EffectAcceptedTurnPlanner.ResolveDuePeriodicResourceMutations(planned.Plan!,
            ResourceOwnerAuthority.CreateCurrentPlayerAuthority(bootstrap.Definitions!), bootstrap.Definitions!);
        if (!allowed)
        {
            Assert.False(due.IsValid);
            Assert.Contains(due.Issues, issue => issue.Code == "effect_resource_operation_forbidden");
            return;
        }
        Assert.True(due.IsValid, string.Join(Environment.NewLine, due.Issues));
        var sources = ResourceMutationSourceCatalog.Create(due.SourceExports);
        Assert.True(sources.IsValid);
        var resources = AcceptedMechanicsPlanner.BuildResources(new AcceptedMechanicsResourceInput(
            Turn: 42, Definitions: bootstrap.Definitions!, State: bootstrap.State!, History: bootstrap.History!,
            Sources: sources.Catalog!, Mutations: due.Mutations, InitialTriggerCandidates: due.TriggerCandidates,
            InitialEffectResolutionWork: due.Work,
            EffectPlanAuthority: AcceptedMechanicsPlanner.CreateEffectPlanAuthority(planned.Plan!)), new AcceptedMechanicsIdentityFactory());
        Assert.True(resources.IsValid, string.Join(Environment.NewLine, resources.Issues));
        Assert.Equal(reaction ? 2 : 1, resources.AppliedTransitions.Count);
        var transition = resources.AppliedTransitions[0];
        Assert.Equal(resource, transition.Coordinate.ResourceKey);
        Assert.Equal(profile == "periodic_spend" ? ResourceTransitionOperation.Spend : ResourceTransitionOperation.Gain,
            transition.Operation);
        Assert.Equal(profile == "periodic_spend" ? 1m : 0m,
            transition.BeforeState!.Current - transition.AfterState!.Current);
    }
}
