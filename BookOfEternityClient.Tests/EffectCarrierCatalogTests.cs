using System.Text.Json.Nodes;
using BookOfEternityClient.Services;
using Xunit;

namespace BookOfEternityClient.Tests;

public sealed class EffectCarrierCatalogTests
{
    [Fact]
    public void Build_IndexesAllFiveApprovedOwnerFamilies()
    {
        var player = EffectMaterializationTestFixture.CreateCanonicalEffect();
        var npc = Effect("npc", "npc_healer", "effect_npc", "transition_npc", "turn_43:npc");
        var buff = Effect("combatant", "combatant_raider", "effect_buff", "transition_buff", "turn_44:buff");
        buff["display"]!["category"] = "buff";
        var debuff = Effect("combatant", "combatant_raider", "effect_debuff", "transition_debuff", "turn_45:debuff");
        var guardian = Effect("guardian", "guardian_mirror", "effect_guardian", "transition_guardian", "turn_46:guardian");
        guardian["realm"] = "chaos_sea";
        var condition = SpiritualCondition("effect_condition", "conflict_test", "player");

        var catalog = EffectCarrierCatalog.Build(new EffectCarrierCatalogInput(
            PlayerRoot(player),
            NpcRoot("npc_healer", npc),
            CombatRoot("enemiesData", "combatant_raider", buff, debuff),
            null,
            ProfileRoot("guardian", "guardian_mirror", guardian),
            ConflictRoot("conflict_test", condition)));

        Assert.Empty(catalog.Issues);
        Assert.Equal(6, catalog.Occurrences.Count);
        AssertOccurrence(catalog, EffectMaterializationTestFixture.EffectId, "player", "player_current", null);
        AssertOccurrence(catalog, "effect_npc", "npc", "npc_healer", null);
        AssertOccurrence(catalog, "effect_buff", "combatant", "combatant_raider", "buff");
        AssertOccurrence(catalog, "effect_debuff", "combatant", "combatant_raider", "debuff");
        AssertOccurrence(catalog, "effect_guardian", "afterlife_profile", "guardian_mirror", null);
        AssertOccurrence(catalog, "effect_condition", "spiritual_conflict", "conflict_test", "player");
    }

    [Fact]
    public void Build_ByteIdenticalCopiesInDifferentCarriersRemainDuplicateOccurrences()
    {
        var effect = EffectMaterializationTestFixture.CreateCanonicalEffect();
        var catalog = EffectCarrierCatalog.Build(new EffectCarrierCatalogInput(
            PlayerRoot(effect),
            null,
            CombatRoot("enemiesData", "player_current", effect.DeepClone().AsObject(), null),
            null,
            null,
            null));

        Assert.Equal(2, catalog.Occurrences.Count);
        Assert.Contains(catalog.Issues, issue => issue.Code == "effect_materialization_duplicate_carrier");
        Assert.False(catalog.TryResolveOne(EffectMaterializationTestFixture.EffectId, out _));
    }

    [Fact]
    public void Build_TargetCarrierMismatchFailsClosed()
    {
        var effect = EffectMaterializationTestFixture.CreateCanonicalEffect();
        effect["target"]!["targetId"] = "npc_other";

        var catalog = EffectCarrierCatalog.Build(new EffectCarrierCatalogInput(
            PlayerRoot(effect), null, null, null, null, null));

        Assert.Contains(catalog.Issues, issue =>
            issue.Code == "effect_materialization_target_carrier_mismatch" &&
            issue.FilePath.EndsWith(".target", StringComparison.Ordinal));
        Assert.False(catalog.TryResolveOne(EffectMaterializationTestFixture.EffectId, out _));
    }

    [Fact]
    public void Build_NpcScanPreservesAdjacentWoundState()
    {
        var effect = Effect("npc", "npc_healer", "effect_npc", "transition_npc", "turn_43:npc");
        var root = NpcRoot("npc_healer", effect);
        root["entries"]![0]!["wounds"] = new JsonArray(new JsonObject
        {
            ["woundId"] = "wound_npc_arm",
            ["state"] = "open"
        });
        var before = root.DeepClone();

        var catalog = EffectCarrierCatalog.Build(new EffectCarrierCatalogInput(
            null, root, null, null, null, null));

        Assert.Empty(catalog.Issues);
        Assert.True(JsonNode.DeepEquals(before, root));
    }

    [Fact]
    public void Build_ProfileScanPreservesUnrelatedSiblingState()
    {
        var effect = Effect("guardian", "guardian_mirror", "effect_guardian", "transition_guardian", "turn_46:guardian");
        effect["realm"] = "chaos_sea";
        var root = ProfileRoot("guardian", "guardian_mirror", effect);
        root["profiles"]![0]!["progression"] = new JsonObject { ["enlightenment"] = 4 };
        root["profileAudit"] = new JsonObject { ["lastTurn"] = 46 };
        var before = root.DeepClone();

        var catalog = EffectCarrierCatalog.Build(new EffectCarrierCatalogInput(
            null, null, null, null, root, null));

        Assert.Empty(catalog.Issues);
        Assert.True(JsonNode.DeepEquals(before, root));
    }

    [Fact]
    public void Build_NonEmptyLegacyPlayerCarrierIsUnsupported()
    {
        var legacy = new JsonObject
        {
            ["playerActiveEffectsChanges"] = new JsonArray(new JsonObject
            {
                ["name"] = "legacy",
                ["duration"] = 999
            })
        };

        var catalog = EffectCarrierCatalog.Build(new EffectCarrierCatalogInput(
            legacy, null, null, null, null, null));

        Assert.Contains(catalog.Issues, issue => issue.Code == "effect_materialization_legacy_carrier_unsupported");
        Assert.Empty(catalog.Occurrences);
    }

    [Fact]
    public void Build_MissingPristineCarriersAreEmptyAndValid()
    {
        var catalog = EffectCarrierCatalog.Build(new EffectCarrierCatalogInput(
            null, null, null, null, null, null));

        Assert.Empty(catalog.Issues);
        Assert.Empty(catalog.Occurrences);
    }

    private static JsonObject PlayerRoot(JsonObject effect) => new()
    {
        ["schemaVersion"] = 1,
        ["activeEffects"] = new JsonArray(effect)
    };

    private static JsonObject NpcRoot(string npcId, JsonObject effect) => new()
    {
        ["schemaVersion"] = 1,
        ["entries"] = new JsonArray(new JsonObject
        {
            ["NPCId"] = npcId,
            ["activeEffects"] = new JsonArray(effect)
        })
    };

    private static JsonObject CombatRoot(
        string collection,
        string combatantId,
        JsonObject? buff,
        JsonObject? debuff) =>
        new()
        {
            [collection] = new JsonArray(new JsonObject
            {
                ["combatantId"] = combatantId,
                ["activeBuffs"] = buff == null ? new JsonArray() : new JsonArray(buff),
                ["activeDebuffs"] = debuff == null ? new JsonArray() : new JsonArray(debuff)
            })
        };

    private static JsonObject ProfileRoot(string actorType, string actorId, JsonObject effect) => new()
    {
        ["schemaVersion"] = 1,
        ["profiles"] = new JsonArray(new JsonObject
        {
            ["actorType"] = actorType,
            ["actorId"] = actorId,
            ["activeEffects"] = new JsonArray(effect)
        })
    };

    private static JsonObject ConflictRoot(string conflictId, JsonObject condition) => new()
    {
        ["schemaVersion"] = 1,
        ["activeConflict"] = new JsonObject
        {
            ["conflictId"] = conflictId,
            ["combatConditions"] = new JsonArray(condition)
        },
        ["recentConflicts"] = new JsonArray()
    };

    private static JsonObject SpiritualCondition(string effectId, string conflictId, string side) => new()
    {
        ["effectId"] = effectId,
        ["state"] = "active",
        ["realm"] = "chaos_sea",
        ["target"] = new JsonObject
        {
            ["kind"] = "spiritual_conflict_side",
            ["targetId"] = conflictId + ":" + side
        },
        ["source"] = new JsonObject
        {
            ["kind"] = "spiritual_art",
            ["sourceId"] = "art_test",
            ["definitionKey"] = "condition_test"
        },
        ["kind"] = "burden",
        ["targetSide"] = side
    };

    private static JsonObject Effect(
        string targetKind,
        string targetId,
        string effectId,
        string transitionId,
        string eventRef)
    {
        var effect = EffectMaterializationTestFixture.CreateCanonicalEffect(targetKind switch
        {
            "npc" => "npc",
            "combatant" => "combatant",
            _ => "player"
        });
        effect["effectId"] = effectId;
        effect["target"]!["kind"] = targetKind;
        effect["target"]!["targetId"] = targetId;
        effect["chronology"]!["lastTransitionId"] = transitionId;
        effect["chronology"]!["createdEventRef"] = eventRef;
        return effect;
    }

    private static void AssertOccurrence(
        EffectCarrierCatalog catalog,
        string effectId,
        string kind,
        string ownerId,
        string? category)
    {
        Assert.True(catalog.TryResolveOne(effectId, out var occurrence));
        Assert.Equal(kind, occurrence.Coordinate.Kind);
        Assert.Equal(ownerId, occurrence.Coordinate.OwnerId);
        Assert.Equal(category, occurrence.Coordinate.Category);
    }
}
