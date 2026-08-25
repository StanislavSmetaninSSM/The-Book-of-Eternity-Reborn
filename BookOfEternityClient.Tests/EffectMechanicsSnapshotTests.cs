using System.Text.Json.Nodes;
using BookOfEternityClient.Configuration;
using BookOfEternityClient.Services;
using Xunit;

namespace BookOfEternityClient.Tests;

public sealed partial class EffectMechanicsSnapshotTests
{
    private static readonly string[] ExpectedRuntimeCharacteristics =
    [
        "strength",
        "dexterity",
        "constitution",
        "intelligence",
        "wisdom",
        "faith",
        "attractiveness",
        "trade",
        "persuasion",
        "perception",
        "luck",
        "speed"
    ];

    public static IEnumerable<object[]> RuntimeCharacteristics =>
        ExpectedRuntimeCharacteristics.Select(static characteristic => new object[] { characteristic });

    [Fact]
    public void RuntimeCharacteristicCatalogIsTheExactTwelveValueBlock5Set()
    {
        Assert.Equal(ExpectedRuntimeCharacteristics, Characteristics.All);
    }

    [Fact]
    public void Build_CompleteCanonicalSetProjectsOneImmutableComponent()
    {
        var effect = EffectMaterializationTestFixture.CreateCanonicalEffect(
            profile: "characteristic_modifier");
        var input = CreateInput(
            new JsonArray(effect.DeepClone()),
            EffectMaterializationTestFixture.CreateIdentityIndex(effect));

        var snapshot = EffectMechanicsSnapshot.Build(input);

        Assert.True(snapshot.IsAccepted);
        Assert.Empty(snapshot.Issues);
        var component = Assert.Single(snapshot.Components);
        Assert.Equal(EffectMaterializationTestFixture.EffectId, component.EffectId);
        Assert.Equal("player", component.TargetKind);
        Assert.Equal("player_current", component.TargetId);
        Assert.Equal("component_001", component.ComponentId);
        Assert.Equal("characteristic_modifier", component.Profile);
        Assert.Equal("dexterity", component.Payload.GetProperty("characteristic").GetString());
        Assert.Equal(-2, component.Payload.GetProperty("value").GetInt32());

        effect["components"]![0]!["payload"]!["value"] = -99;
        Assert.Equal(-2, component.Payload.GetProperty("value").GetInt32());
        Assert.Single(snapshot.Audit);
    }

    [Theory]
    [InlineData("malformed_component")]
    [InlineData("unsupported_profile")]
    [InlineData("target_mismatch")]
    [InlineData("bad_index")]
    public void Build_OneInvalidSiblingInvalidatesAllMechanics(string defect)
    {
        var valid = EffectMaterializationTestFixture.CreateCanonicalEffect(
            profile: "characteristic_modifier");
        var invalid = CreateDistinctEffect(
            "effect_test_invalid_sibling",
            "effect_transition_invalid_sibling",
            "turn_43:invalid_sibling");

        switch (defect)
        {
            case "malformed_component":
                invalid["components"]![0]!.AsObject().Remove("payload");
                break;
            case "unsupported_profile":
                invalid["components"]![0]!["profile"] = "unregistered_profile";
                break;
            case "target_mismatch":
                invalid["target"]!["targetId"] = "npc_other";
                break;
        }

        var index = EffectMaterializationTestFixture.CreateIdentityIndex(valid, invalid);
        if (defect == "bad_index")
            index["entries"]![1]!["owner"]!["ownerId"] = "npc_other";

        var snapshot = EffectMechanicsSnapshot.Build(CreateInput(
            new JsonArray(valid.DeepClone(), invalid.DeepClone()),
            index));

        Assert.False(snapshot.IsAccepted);
        Assert.NotEmpty(snapshot.Issues);
        Assert.Empty(snapshot.Components);
        Assert.Empty(snapshot.Audit);
    }

    [Fact]
    public void Build_DuplicateCarrierOccurrenceInvalidatesAllMechanics()
    {
        var effect = EffectMaterializationTestFixture.CreateCanonicalEffect(
            profile: "characteristic_modifier");

        var snapshot = EffectMechanicsSnapshot.Build(CreateInput(
            new JsonArray(effect.DeepClone(), effect.DeepClone()),
            EffectMaterializationTestFixture.CreateIdentityIndex(effect)));

        Assert.False(snapshot.IsAccepted);
        Assert.Contains(snapshot.Issues, issue =>
            issue.Code == "effect_materialization_duplicate_carrier");
        Assert.Empty(snapshot.Components);
    }

    [Theory]
    [InlineData("combat_collection_wrong_type")]
    [InlineData("combat_owner_wrong_type")]
    [InlineData("profile_collection_wrong_type")]
    [InlineData("profile_owner_wrong_type")]
    [InlineData("conflict_collection_wrong_type")]
    [InlineData("conflict_condition_wrong_type")]
    public void Build_MalformedGovernedSiblingCarrierInvalidatesAllMechanics(string defect)
    {
        var effect = EffectMaterializationTestFixture.CreateCanonicalEffect(
            profile: "characteristic_modifier");
        var input = CreateInput(
            new JsonArray(effect.DeepClone()),
            EffectMaterializationTestFixture.CreateIdentityIndex(effect));

        input = defect switch
        {
            "combat_collection_wrong_type" => input with
            {
                Carriers = input.Carriers with
                {
                    EnemyCombatants = new JsonObject
                    {
                        ["enemiesData"] = new JsonArray(new JsonObject
                        {
                            ["combatantId"] = "combatant_malformed",
                            ["activeBuffs"] = new JsonObject(),
                            ["activeDebuffs"] = new JsonArray()
                        })
                    }
                }
            },
            "combat_owner_wrong_type" => input with
            {
                Carriers = input.Carriers with
                {
                    EnemyCombatants = new JsonObject
                    {
                        ["enemiesData"] = new JsonArray("malformed")
                    }
                }
            },
            "profile_collection_wrong_type" => input with
            {
                Carriers = input.Carriers with
                {
                    AfterlifeProfiles = new JsonObject
                    {
                        ["schemaVersion"] = 1,
                        ["profiles"] = new JsonArray(new JsonObject
                        {
                            ["actorType"] = "guardian",
                            ["actorId"] = "guardian_malformed",
                            ["activeEffects"] = new JsonObject()
                        })
                    }
                }
            },
            "profile_owner_wrong_type" => input with
            {
                Carriers = input.Carriers with
                {
                    AfterlifeProfiles = new JsonObject
                    {
                        ["schemaVersion"] = 1,
                        ["profiles"] = new JsonArray("malformed")
                    }
                }
            },
            "conflict_collection_wrong_type" => input with
            {
                Carriers = input.Carriers with
                {
                    SpiritualConflict = new JsonObject
                    {
                        ["schemaVersion"] = 1,
                        ["activeConflict"] = new JsonObject
                        {
                            ["conflictId"] = "conflict_malformed",
                            ["combatConditions"] = new JsonObject()
                        },
                        ["recentConflicts"] = new JsonArray()
                    }
                }
            },
            "conflict_condition_wrong_type" => input with
            {
                Carriers = input.Carriers with
                {
                    SpiritualConflict = new JsonObject
                    {
                        ["schemaVersion"] = 1,
                        ["activeConflict"] = new JsonObject
                        {
                            ["conflictId"] = "conflict_malformed",
                            ["combatConditions"] = new JsonArray("malformed")
                        },
                        ["recentConflicts"] = new JsonArray()
                    }
                }
            },
            _ => throw new ArgumentOutOfRangeException(nameof(defect), defect, null)
        };

        var snapshot = EffectMechanicsSnapshot.Build(input);

        Assert.False(snapshot.IsAccepted);
        Assert.Contains(snapshot.Issues, issue =>
            issue.Code == "effect_materialization_invalid_field");
        Assert.Empty(snapshot.Components);
        Assert.Empty(snapshot.Audit);
    }

    [Fact]
    public void Build_SuspendedEffectIsAcceptedButContributesNoMechanics()
    {
        var effect = EffectMaterializationTestFixture.CreateCanonicalEffect(
            profile: "characteristic_modifier");
        effect["state"] = "suspended";
        var index = EffectMaterializationTestFixture.CreateIdentityIndex(effect);
        index["entries"]![0]!["state"] = "suspended";

        var snapshot = EffectMechanicsSnapshot.Build(CreateInput(
            new JsonArray(effect.DeepClone()),
            index));

        Assert.True(snapshot.IsAccepted);
        Assert.Empty(snapshot.Components);
        Assert.Single(snapshot.Audit);
    }

    [Fact]
    public void Build_HiddenEffectKeepsMechanicsButDoesNotEnterPlayerSafeAudit()
    {
        var effect = EffectMaterializationTestFixture.CreateCanonicalEffect(
            profile: "characteristic_modifier");
        effect["display"]!["visibility"] = "hidden";

        var snapshot = EffectMechanicsSnapshot.Build(CreateInput(
            new JsonArray(effect.DeepClone()),
            EffectMaterializationTestFixture.CreateIdentityIndex(effect)));

        Assert.True(snapshot.IsAccepted);
        var component = Assert.Single(snapshot.Components);
        Assert.False(component.IsPlayerVisible);
        Assert.Equal("Скрытый эффект", component.EffectName);
        Assert.Equal(string.Empty, component.EffectDescription);
        Assert.Empty(snapshot.Audit);
    }

    [Theory]
    [MemberData(nameof(RuntimeCharacteristics))]
    public void Build_CharacteristicModifierUsesTheRuntimeCharacteristicCatalog(
        string characteristic)
    {
        var effect = EffectMaterializationTestFixture.CreateCanonicalEffect(
            profile: "characteristic_modifier");
        effect["components"]![0]!["payload"]!["characteristic"] = characteristic;

        var snapshot = EffectMechanicsSnapshot.Build(CreateInput(
            new JsonArray(effect.DeepClone()),
            EffectMaterializationTestFixture.CreateIdentityIndex(effect)));

        Assert.True(snapshot.IsAccepted);
        Assert.Equal(
            characteristic,
            Assert.Single(snapshot.Components).Payload
                .GetProperty("characteristic")
                .GetString());
    }

    [Theory]
    [InlineData("agility")]
    [InlineData("charisma")]
    [InlineData("willpower")]
    [InlineData("endurance")]
    public void Build_CharacteristicModifierRejectsLegacyCharacteristicAliases(string alias)
    {
        var effect = EffectMaterializationTestFixture.CreateCanonicalEffect(
            profile: "characteristic_modifier");
        effect["components"]![0]!["payload"]!["characteristic"] = alias;

        var snapshot = EffectMechanicsSnapshot.Build(CreateInput(
            new JsonArray(effect.DeepClone()),
            EffectMaterializationTestFixture.CreateIdentityIndex(effect)));

        Assert.False(snapshot.IsAccepted);
        Assert.Contains(snapshot.Issues, issue =>
            issue.Code == "effect_materialization_invalid_component");
        Assert.Empty(snapshot.Components);
    }

    private static EffectMechanicsInput CreateInput(
        JsonArray activeEffects,
        JsonObject? identityIndex) =>
        new(
            new EffectCarrierCatalogInput(
                new JsonObject
                {
                    ["schemaVersion"] = 1,
                    ["activeEffects"] = activeEffects
                },
                null,
                null,
                null,
                null,
                null),
            identityIndex);

    private static JsonObject CreateDistinctEffect(
        string effectId,
        string transitionId,
        string eventRef)
    {
        var effect = EffectMaterializationTestFixture.CreateCanonicalEffect(
            profile: "characteristic_modifier");
        effect["effectId"] = effectId;
        effect["source"]!["sourceId"] = "wound_test_invalid_sibling";
        effect["stacking"]!["stackKey"] = "invalid_sibling";
        effect["chronology"]!["lastTransitionId"] = transitionId;
        effect["chronology"]!["createdEventRef"] = eventRef;
        return effect;
    }
}
