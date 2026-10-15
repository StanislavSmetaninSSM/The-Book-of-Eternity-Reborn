using System.Text.Json.Nodes;
using BookOfEternityClient.Services;
using Xunit;

namespace BookOfEternityClient.Tests;

public sealed partial class WoundProjectionPrivacyTests
{
    [Fact]
    public void Build_OnlyVisibleExactWoundOwnedEffectsBecomeReadableConsequences()
    {
        var input = WoundPlayerProjectionTestData.Input(WoundContractTestData.CreateActiveWound());
        var owned = EffectMaterializationTestFixture.CreateCanonicalEffect();
        owned["effectId"] = "effect_wound_test_bleeding";
        owned["display"]!["name"] = "Явное истощение от раны";
        var unrelated = EffectMaterializationTestFixture.CreateCanonicalEffect();
        unrelated["effectId"] = "effect_independent";
        unrelated["display"]!["name"] = "Независимый секретный эффект";
        unrelated["source"]!["sourceId"] = "wound_unrelated";
        unrelated["chronology"]!["lastTransitionId"] = "transition_independent_effect";
        unrelated["chronology"]!["createdEventRef"] = "turn_42:independent_effect";
        var index = EffectMaterializationTestFixture.CreateIdentityIndex(owned, unrelated);
        index["entries"]![1]!["transitions"]![0]!["transitionId"] = "transition_independent_effect";
        index["entries"]![1]!["transitions"]![0]!["eventRef"] = "turn_42:independent_effect";
        var snapshot = EffectMechanicsSnapshot.Build(new(new EffectCarrierCatalogInput(
            new JsonObject { ["schemaVersion"] = 1, ["activeEffects"] = new JsonArray(owned.DeepClone(), unrelated.DeepClone()) },
            null, null, null, null, null), index));
        Assert.True(snapshot.IsAccepted, string.Join(";", snapshot.Issues));
        var result = WoundPlayerProjection.Build(input with { Effects = snapshot });
        Assert.Contains(Assert.Single(result.Active).Facts, f => f.Value == "Явное истощение от раны");
        Assert.DoesNotContain("Независимый секретный эффект", Text(result));
        Assert.DoesNotContain("effect_view_", Text(result));
        Assert.DoesNotContain("Selector", Text(result));
        owned["display"]!["visibility"] = "hidden";
        var hiddenSnapshot = EffectMechanicsSnapshot.Build(new(new EffectCarrierCatalogInput(
            new JsonObject { ["schemaVersion"] = 1, ["activeEffects"] = new JsonArray(owned) },
            null, null, null, null, null), EffectMaterializationTestFixture.CreateIdentityIndex(owned)));
        Assert.True(hiddenSnapshot.IsAccepted);
        Assert.DoesNotContain("Явное истощение от раны", Text(WoundPlayerProjection.Build(input with { Effects = hiddenSnapshot })));
    }

    [Fact]
    public void Build_ForeignEffectTargetCannotLeakThroughKnownOwnedId()
    {
        var input = WoundPlayerProjectionTestData.Input(WoundContractTestData.CreateActiveWound());
        var effect = EffectMaterializationTestFixture.CreateCanonicalEffect("npc");
        effect["effectId"] = "effect_wound_test_bleeding";
        effect["display"]!["name"] = "Секрет чужого владельца";
        var snapshot = EffectMechanicsSnapshot.Build(new(new EffectCarrierCatalogInput(null,
            new JsonObject { ["schemaVersion"] = 1, ["entries"] = new JsonArray(new JsonObject
            { ["NPCId"] = "npc_test_healer", ["activeEffects"] = new JsonArray(effect) }) },
            null, null, null, null), EffectMaterializationTestFixture.CreateIdentityIndex(effect)));
        Assert.True(snapshot.IsAccepted, string.Join(";", snapshot.Issues));
        Assert.DoesNotContain("Секрет чужого владельца", Text(WoundPlayerProjection.Build(input with { Effects = snapshot })));
    }
    [Fact]
    public void Build_HiddenComplicationRestrictsEvenVisibleOwnedEffect()
    {
        var node = WoundContractTestData.CreateActiveWound();
        node["complications"] = new JsonArray(new JsonObject
        {
            ["complicationId"] = "secret_pain", ["kind"] = "pain", ["state"] = "active",
            ["displayName"] = "Закрытое осложнение", ["treatmentDifficultyModifier"] = 1,
            ["ownedEffectIds"] = new JsonArray("effect_wound_test_pain"), ["visibility"] = "hidden"
        });
        var input = WoundPlayerProjectionTestData.Input(node);
        var effect = EffectMaterializationTestFixture.CreateCanonicalEffect(profile: "action_control");
        effect["effectId"] = "effect_wound_test_pain";
        effect["display"]!["name"] = "Следствие закрытого осложнения";
        var snapshot = EffectMechanicsSnapshot.Build(new(new EffectCarrierCatalogInput(
            new JsonObject { ["schemaVersion"] = 1, ["activeEffects"] = new JsonArray(effect) },
            null, null, null, null, null), EffectMaterializationTestFixture.CreateIdentityIndex(effect)));
        Assert.True(snapshot.IsAccepted, string.Join(";", snapshot.Issues));
        var text = Text(WoundPlayerProjection.Build(input with { Effects = snapshot }));
        Assert.DoesNotContain("Закрытое осложнение", text);
        Assert.DoesNotContain("Следствие закрытого осложнения", text);
    }

    [Fact]
    public void Build_MatchingOwnedIdDoesNotRevealAnEffectFromAnotherSource()
    {
        var input = WoundPlayerProjectionTestData.Input(WoundContractTestData.CreateActiveWound());
        var effect = EffectMaterializationTestFixture.CreateCanonicalEffect();
        effect["effectId"] = "effect_wound_test_bleeding";
        effect["source"]!["sourceId"] = "wound_other_source";
        effect["display"]!["name"] = "Чужой источник при совпавшем идентификаторе";
        var snapshot = EffectMechanicsSnapshot.Build(new(new EffectCarrierCatalogInput(
            new JsonObject { ["schemaVersion"] = 1, ["activeEffects"] = new JsonArray(effect) },
            null, null, null, null, null), EffectMaterializationTestFixture.CreateIdentityIndex(effect)));
        Assert.True(snapshot.IsAccepted, string.Join(";", snapshot.Issues));
        Assert.DoesNotContain("Чужой источник при совпавшем идентификаторе", Text(WoundPlayerProjection.Build(input with { Effects = snapshot })));
    }

    [Theory]
    [InlineData("chaos_sea")]
    [InlineData("shining_abode")]
    public void Build_AfterlifeOwnedEffectUsesAcceptedProfileAndExcludesForeignOrHiddenFacts(string realm)
    {
        var wound = WoundContractTestData.CreateSpiritualActiveWound("wound_soul", realm,
            ownerId: "player_soul", carrierPath: WoundCarrierCatalog.AfterlifeProfilesPath);
        var input = WoundPlayerProjectionTestData.InputInRealm(realm, wound);
        var effect = EffectMaterializationTestFixture.CreateSpiritualWoundCanonicalEffect(
            "spiritual_roll_hindrance", targetKind: "player", realm: realm, woundId: "wound_soul");
        effect["effectId"] = "effect_spiritual_roll";
        effect["source"]!["definitionKey"] = "definition_spiritual_roll";
        effect["target"]!["targetId"] = "player_soul";
        effect["display"]!["name"] = "Известная помеха душе";
        foreach (var scenario in new[] { "visible", "wrong_source", "wrong_realm", "hidden_effect", "hidden_profile" })
        {
            var current = effect.DeepClone().AsObject();
            var profiles = input.Carriers.AfterlifeProfiles!.DeepClone().AsObject();
            var profile = profiles["profiles"]![0]!;
            if (scenario == "wrong_source") current["source"]!["sourceId"] = "wound_foreign";
            if (scenario == "wrong_realm")
            {
                current["realm"] = realm == "chaos_sea" ? "shining_abode" : "chaos_sea";
                profile["realm"] = realm == "chaos_sea" ? "Shining Abode" : "Chaos Sea";
            }
            if (scenario == "hidden_effect") current["display"]!["visibility"] = "hidden";
            if (scenario == "hidden_profile") profile["visibility"] = "hidden";
            profile["activeEffects"] = new JsonArray(current.DeepClone());
            var index = EffectMaterializationTestFixture.CreateIdentityIndex(current);
            index["entries"]![0]!["owner"] = new JsonObject
            {
                ["kind"] = "player", ["ownerId"] = "player_soul",
                ["carrierPath"] = EffectCarrierCatalog.AfterlifeProfilesPath, ["collection"] = "activeEffects"
            };
            var snapshot = EffectMechanicsSnapshot.Build(new(new EffectCarrierCatalogInput(null, null, null, null, profiles, null), index));
            Assert.True(snapshot.IsAccepted, scenario + ":" + string.Join(";", snapshot.Issues));
            var result = WoundPlayerProjection.Build(input with { Effects = snapshot,
                SelfVisible = scenario != "hidden_profile" });
            Assert.True(result.IsAvailable);
            Assert.Equal(scenario == "visible", Text(result).Contains("Известная помеха душе"));
            Assert.DoesNotContain("effect_spiritual_roll", Text(result));
            Assert.DoesNotContain("wound_foreign", Text(result));
        }
    }

}
