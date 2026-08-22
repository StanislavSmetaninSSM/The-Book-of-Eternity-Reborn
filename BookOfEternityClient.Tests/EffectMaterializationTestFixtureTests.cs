using System.Text.Json.Nodes;
using Xunit;

namespace BookOfEternityClient.Tests;

public sealed class EffectMaterializationTestFixtureTests
{
    private static readonly string[] CanonicalRootFields =
    {
        "schemaVersion",
        "entityKind",
        "effectId",
        "state",
        "realm",
        "target",
        "display",
        "source",
        "components",
        "lifetime",
        "stacking",
        "triggers",
        "removal",
        "links",
        "chronology"
    };

    [Fact]
    public void CreateDefinition_IsCompleteStaticSourceWithoutRuntimeIdentity()
    {
        var definition = EffectMaterializationTestFixture.CreateDefinition();

        Assert.Equal(1, definition["schemaVersion"]!.GetValue<int>());
        Assert.Equal(
            EffectMaterializationTestFixture.DefinitionKey,
            definition["definitionKey"]!.GetValue<string>());
        Assert.Single(definition["allowedRealms"]!.AsArray());
        Assert.Equal(3, definition["allowedTargetKinds"]!.AsArray().Count);
        Assert.Single(definition["components"]!.AsArray());
        Assert.NotNull(definition["stacking"]);
        Assert.NotNull(definition["lifetime"]);
        Assert.NotNull(definition["triggers"]);
        Assert.NotNull(definition["removal"]);
        Assert.NotNull(definition["links"]);

        Assert.False(definition.ContainsKey("effectId"));
        Assert.False(definition.ContainsKey("currentStacks"));
        Assert.False(definition.ContainsKey("chronology"));
        Assert.False(definition.ContainsKey("materializationReceipt"));
    }

    [Fact]
    public void CreateApplyCommand_ContainsOnlyGmOwnedIntent()
    {
        var command = EffectMaterializationTestFixture.CreateApplyCommand();

        Assert.Equal("apply", command["operation"]!.GetValue<string>());
        Assert.Equal("player", command["target"]!["kind"]!.GetValue<string>());
        Assert.Equal("player_current", command["target"]!["targetId"]!.GetValue<string>());
        Assert.Equal("wound", command["source"]!["kind"]!.GetValue<string>());
        Assert.Equal(
            EffectMaterializationTestFixture.DefinitionKey,
            command["source"]!["definitionKey"]!.GetValue<string>());
        Assert.Equal("accepted_turn", command["eventRef"]!["kind"]!.GetValue<string>());
        Assert.False(command.ContainsKey("effectId"));
        Assert.False(command.ContainsKey("currentStacks"));
        Assert.False(command.ContainsKey("lifetime"));
        Assert.False(command.ContainsKey("transitionId"));
    }

    [Fact]
    public void CreateCanonicalEffect_HasCompleteCurrentRootAndRegisteredComponent()
    {
        var effect = EffectMaterializationTestFixture.CreateCanonicalEffect();

        Assert.Equal(CanonicalRootFields, effect.Select(pair => pair.Key));
        Assert.Equal("active_effect", effect["entityKind"]!.GetValue<string>());
        Assert.Equal(EffectMaterializationTestFixture.EffectId, effect["effectId"]!.GetValue<string>());
        Assert.Equal("active", effect["state"]!.GetValue<string>());
        Assert.Equal("mortal_world", effect["realm"]!.GetValue<string>());
        Assert.Equal("player_current", effect["target"]!["targetId"]!.GetValue<string>());

        var component = Assert.Single(effect["components"]!.AsArray().OfType<JsonObject>());
        Assert.Equal("periodic_damage", component["profile"]!.GetValue<string>());
        Assert.Equal("health", component["payload"]!["resource"]!.GetValue<string>());
        Assert.Equal(EffectMaterializationTestFixture.TransitionId,
            effect["chronology"]!["lastTransitionId"]!.GetValue<string>());
    }

    [Fact]
    public void CreateIdentityIndex_MatchesCanonicalEffectAndPlayerCarrier()
    {
        var effect = EffectMaterializationTestFixture.CreateCanonicalEffect();
        var index = EffectMaterializationTestFixture.CreateIdentityIndex(effect);

        Assert.Equal(1, index["schemaVersion"]!.GetValue<int>());
        var entry = Assert.Single(index["entries"]!.AsArray().OfType<JsonObject>());
        Assert.Equal(EffectMaterializationTestFixture.EffectId, entry["effectId"]!.GetValue<string>());
        Assert.Equal("active", entry["state"]!.GetValue<string>());
        Assert.Equal("player", entry["owner"]!["kind"]!.GetValue<string>());
        Assert.Equal("game_state/player/effects.json",
            entry["owner"]!["carrierPath"]!.GetValue<string>());
        Assert.Equal("activeEffects", entry["owner"]!["collection"]!.GetValue<string>());
        Assert.Single(entry["transitions"]!.AsArray());
    }

    [Fact]
    public void CreateCommandAndPendingRoots_UseSeparateTransientAndClientOwnedShapes()
    {
        var commandRoot = EffectMaterializationTestFixture.CreateCommandRoot(
            EffectMaterializationTestFixture.CreateApplyCommand());
        var pendingRoot = EffectMaterializationTestFixture.CreatePendingResolutionRoot();

        Assert.Single(commandRoot["effectChanges"]!.AsArray());
        Assert.Empty(commandRoot["effectResolutionReceipts"]!.AsArray());
        Assert.Empty(commandRoot["effectEventReports"]!.AsArray());
        Assert.False(commandRoot.ContainsKey("sessionId"));

        Assert.Equal(1, pendingRoot["schemaVersion"]!.GetValue<int>());
        Assert.Equal("session_effect_materialization_test",
            pendingRoot["sessionId"]!.GetValue<string>());
        var request = Assert.Single(pendingRoot["requests"]!.AsArray().OfType<JsonObject>());
        Assert.Equal(EffectMaterializationTestFixture.EffectId, request["effectId"]!.GetValue<string>());
        Assert.True(request["fullTurnResubmissionRequired"]!.GetValue<bool>());
    }

    [Fact]
    public void FixtureFactories_ReturnIndependentDeepGraphs()
    {
        var first = EffectMaterializationTestFixture.CreateCanonicalEffect();
        var second = EffectMaterializationTestFixture.CreateCanonicalEffect();

        first["display"]!["name"] = "Изменённое имя";
        first["components"]![0]!["payload"]!["amount"] = 99;

        Assert.Equal("Кровотечение", second["display"]!["name"]!.GetValue<string>());
        Assert.Equal(3, second["components"]![0]!["payload"]!["amount"]!.GetValue<int>());
    }
}
