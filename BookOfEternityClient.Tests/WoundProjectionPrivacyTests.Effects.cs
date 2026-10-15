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
        var snapshot = EffectMechanicsSnapshot.Build(new(new EffectCarrierCatalogInput(
            new JsonObject { ["schemaVersion"] = 1, ["activeEffects"] = new JsonArray(owned.DeepClone(), unrelated.DeepClone()) },
            null, null, null, null, null), EffectMaterializationTestFixture.CreateIdentityIndex(owned, unrelated)));
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
            { ["npcId"] = "npc_test_healer", ["activeEffects"] = new JsonArray(effect) }) },
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

}
