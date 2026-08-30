using System.Text.Json.Nodes;
using BookOfEternityClient.Core;
using BookOfEternityClient.Services;
using Microsoft.Extensions.Logging.Abstractions;
using Xunit;

namespace BookOfEternityClient.Tests;

public sealed partial class MortalWoundTreatmentResolverTests
{
    [Fact]
    public void AcceptedStateExport_AcceptsProductionValidDetachedActorSkillCombatAndQuestSources()
    {
        using var fixture = AcceptedStateFixture.Create(CreateScenario(
            "procedure_normal_uses_lowest_free_die",
            "procedure"));

        var result = Assert.IsType<MortalWoundTreatmentAcceptedStateAuthorityResult>(
            fixture.ExportCurrent());

        Assert.True(result.IsValid, DescribeIssues(result.Issues));
        Assert.Empty(result.Issues);
        Assert.NotNull(result.Authority);
    }

    [Theory]
    [InlineData("npc", "npc_full_object_missing_required_fields")]
    [InlineData("player_active", "missing_required_string")]
    [InlineData("player_passive", "passive_skill_missing_structured_bonuses")]
    [InlineData("player_mastery", "skill_mastery_change_missing_progress_needed")]
    [InlineData("combat", "missing_required_string")]
    [InlineData("quest", "missing_required_string")]
    public void AcceptedStateExport_RejectsProductionInvalidDetachedSourceRows(
        string source,
        string expectedCode)
    {
        using var fixture = AcceptedStateFixture.Create(CreateScenario(
            "procedure_normal_uses_lowest_free_die",
            "procedure"));
        fixture.RemoveProductionRequiredDetachedSourceField(source);

        var result = Assert.IsType<MortalWoundTreatmentAcceptedStateAuthorityResult>(
            fixture.ExportCurrent());

        Assert.False(result.IsValid);
        Assert.Null(result.Authority);
        Assert.Contains(result.Issues, issue => issue.Code == expectedCode);
        Assert.True(Assert.IsAssignableFrom<IList<ValidationIssue>>(result.Issues).IsReadOnly);
    }

    [Fact]
    public void AcceptedStateExport_RejectsCaseVariantRequiredCollectionName()
    {
        using var fixture = AcceptedStateFixture.Create(CreateScenario(
            "procedure_normal_uses_lowest_free_die",
            "procedure"));
        fixture.ReplaceActiveSkillCollectionWithCaseVariant();

        var result = Assert.IsType<MortalWoundTreatmentAcceptedStateAuthorityResult>(
            fixture.ExportCurrent());

        Assert.False(result.IsValid);
        Assert.Null(result.Authority);
        Assert.Contains(result.Issues, issue =>
            issue.Code == "mortal_wound_treatment_detached_collection_name_invalid");
    }

    [Fact]
    public void AcceptedStateProjection_UsesOnlyCanonicalCombatIdentityKinds()
    {
        using var fixture = AcceptedStateFixture.Create(CreateScenario(
            "procedure_normal_uses_lowest_free_die",
            "procedure"));
        fixture.MoveProviderOffScene();
        fixture.AddProductionValidCombatProjectionRows();

        var actors = fixture.GetRequirementSnapshot().Actors;

        var provider = Assert.Single(actors, actor =>
            actor.ActorKind == "npc" && actor.ActorId == "field_medic_01");
        Assert.False(provider.Reachable);
        Assert.Contains(actors, actor =>
            actor.ActorKind == "combatant" && actor.ActorId == "combatant_detached_t066");
        Assert.Contains(actors, actor =>
            actor.ActorKind == "combatant_member" && actor.ActorId == "member_nested_t066");
        Assert.Contains(actors, actor =>
            actor.ActorKind == "combatant_member" && actor.ActorId == "member_detached_t066");
        Assert.DoesNotContain(actors, actor => actor.ActorId == "combatant_group_t066");
    }

    [Fact]
    public void DetachedSourceValidation_UsesOnlySuppliedSignedRoots()
    {
        using var fixture = AcceptedStateFixture.Create(CreateScenario(
            "procedure_normal_uses_lowest_free_die",
            "procedure"));
        var paths = new[]
        {
            "game_state/npcs/npc_core.json",
            "game_state/player/skills_active.json",
            "game_state/player/skills_passive.json",
            "game_state/player/skill_mastery.json",
            EffectCarrierCatalog.EnemiesPath,
            EffectCarrierCatalog.AlliesPath,
            "game_state/quests/regular_quests.json"
        };
        var roots = paths.Where(path => File.Exists(
                fixture.FileSystem.ResolvePath(path)))
            .ToDictionary(
            static path => path,
            path => JsonNode.Parse(File.ReadAllText(
                fixture.FileSystem.ResolvePath(path)))!.AsObject(),
            StringComparer.Ordinal);

        var armed = false;
        var readCount = 0;
        Task RejectRead(string _)
        {
            if (armed)
                readCount++;
            return Task.CompletedTask;
        }
        var hooks = new FileSystemManagerHooks
        {
            BeforeCanonicalReadOpenAsync = RejectRead,
            BeforeRuntimeFileReadOpenAsync = RejectRead
        };
        var fileSystem = new FileSystemManager(
            fixture.Root,
            NullLogger<FileSystemManager>.Instance,
            PhysicalLoadTransactionOperations.Instance,
            hooks);
        armed = true;

        var issues = new ValidationService(
                fileSystem,
                NullLogger<ValidationService>.Instance)
            .ValidateMortalWoundTreatmentDetachedSources(roots);

        Assert.Empty(issues);
        Assert.Equal(0, readCount);
    }

    private static JsonObject CreateProductionPassiveSkill() => new()
    {
        ["skillId"] = "skill_patient_observation_01",
        ["displayName"] = "Patient observation",
        ["skillName"] = "Patient Observation",
        ["skillDescription"] = "Tracks changes in a patient's condition.",
        ["rarity"] = "Common",
        ["type"] = "Utility",
        ["group"] = "Medicine",
        ["masteryLevel"] = 2,
        ["maxMasteryLevel"] = 4,
        ["structuredBonuses"] = null,
        ["playerStatBonus"] = null
    };

    private static JsonObject CreateProductionRegularQuest() => new()
    {
        ["questId"] = "quest_field_clinic_intro",
        ["questName"] = "Field clinic introduction",
        ["status"] = "Completed",
        ["questGiver"] = "Field medic",
        ["questBackground"] = "The player learned how the clinic operates.",
        ["description"] = "Complete an introduction to field treatment.",
        ["objectives"] = new JsonArray(),
        ["detailsLog"] = new JsonArray()
    };

    private static JsonObject CreateProductionCombatant(
        string combatantId,
        JsonObject? wound = null)
    {
        var row = CreateProductionCombatRow("Detached combatant");
        row["combatantId"] = combatantId;
        row["activeWounds"] = wound is null
            ? new JsonArray()
            : new JsonArray(wound.DeepClone());
        return row;
    }

    private static JsonObject CreateProductionCombatGroup(
        string combatantId,
        string memberId,
        JsonObject? wound = null)
    {
        var row = CreateProductionCombatRow("Detached combat group");
        row["combatantId"] = combatantId;
        row["isGroup"] = true;
        row["count"] = 1;
        row["unitName"] = "member";
        row["members"] = new JsonArray(new JsonObject
        {
            ["memberId"] = memberId,
            ["displayName"] = "Detached group member",
            ["activeWounds"] = wound is null
                ? new JsonArray()
                : new JsonArray(wound.DeepClone())
        });
        return row;
    }

    private static JsonObject CreateProductionNamedNpcCombatant(string npcId)
    {
        var row = CreateProductionCombatRow("Named NPC combatant");
        row["NPCId"] = npcId;
        return row;
    }

    private static JsonObject CreateProductionDetachedMember(string memberId)
    {
        var row = CreateProductionCombatRow("Detached group member");
        row["memberId"] = memberId;
        row["activeWounds"] = new JsonArray();
        return row;
    }

    private static JsonObject CreateProductionCombatRow(string name) => new()
    {
        ["NPCId"] = null,
        ["name"] = name,
        ["image_prompt"] = "setting neutral combatant portrait",
        ["description"] = "A complete canonical combat row used by accepted-state tests.",
        ["type"] = "Test combatant",
        ["isGroup"] = false,
        ["actions"] = new JsonArray(),
        ["resistances"] = new JsonArray(),
        ["activeBuffs"] = new JsonArray(),
        ["activeDebuffs"] = new JsonArray()
    };

    private sealed partial class AcceptedStateFixture
    {
        internal void RemoveProductionRequiredDetachedSourceField(string source)
        {
            switch (source)
            {
                case "npc":
                {
                    var root = ReadObject(NpcCorePath);
                    root["NPCsInScene"]![0]!.AsObject().Remove("image_prompt");
                    WriteObject(NpcCorePath, root);
                    WriteCanonicalResourceAuthority(FileSystem);
                    break;
                }
                case "player_active":
                {
                    var root = ReadObject("game_state/player/skills_active.json");
                    root["activeSkillChanges"]![0]!.AsObject().Remove("skillDescription");
                    WriteObject("game_state/player/skills_active.json", root);
                    break;
                }
                case "player_passive":
                {
                    var root = ReadObject("game_state/player/skills_passive.json");
                    root["passiveSkillChanges"]![0]!.AsObject().Remove("structuredBonuses");
                    WriteObject("game_state/player/skills_passive.json", root);
                    break;
                }
                case "player_mastery":
                {
                    var root = ReadObject("game_state/player/skill_mastery.json");
                    root["skillMasteryChanges"]![0]!.AsObject()
                        .Remove("newMasteryProgressNeeded");
                    WriteObject("game_state/player/skill_mastery.json", root);
                    break;
                }
                case "combat":
                {
                    var combatant = CreateProductionCombatant("combatant_invalid_t066");
                    combatant.Remove("description");
                    WriteObject(
                        EffectCarrierCatalog.EnemiesPath,
                        new JsonObject
                        {
                            ["enemiesData"] = new JsonArray(combatant)
                        });
                    WriteCanonicalResourceAuthority(FileSystem);
                    break;
                }
                case "quest":
                {
                    var root = ReadObject("game_state/quests/regular_quests.json");
                    root["quests"]![0]!.AsObject().Remove("questGiver");
                    WriteObject("game_state/quests/regular_quests.json", root);
                    break;
                }
                default:
                    throw new ArgumentOutOfRangeException(nameof(source), source, null);
            }

            PrepareFreshSnapshot("invalid_detached_" + source);
        }

        internal void ReplaceActiveSkillCollectionWithCaseVariant()
        {
            var root = ReadObject("game_state/player/skills_active.json");
            var rows = root["activeSkillChanges"]!.DeepClone();
            root.Remove("activeSkillChanges");
            root["ActiveSkillChanges"] = rows;
            WriteObject("game_state/player/skills_active.json", root);
            PrepareFreshSnapshot("case_variant_active_skills");
        }

        internal void AddProductionValidCombatProjectionRows()
        {
            WriteObject(
                EffectCarrierCatalog.EnemiesPath,
                new JsonObject
                {
                    ["enemiesData"] = new JsonArray(
                        CreateProductionNamedNpcCombatant("field_medic_01"),
                        CreateProductionCombatant("combatant_detached_t066"))
                });
            WriteObject(
                EffectCarrierCatalog.AlliesPath,
                new JsonObject
                {
                    ["alliesData"] = new JsonArray(
                        CreateProductionCombatGroup(
                            "combatant_group_t066",
                            "member_nested_t066"),
                        CreateProductionDetachedMember("member_detached_t066"))
                });
            WriteCanonicalResourceAuthority(FileSystem);
            PrepareFreshSnapshot("production_combat_projection");
        }
    }
}
