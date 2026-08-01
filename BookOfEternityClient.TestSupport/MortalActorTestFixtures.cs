using System.Text.Json.Nodes;

namespace BookOfEternityClient.Tests;

internal static class MortalActorTestFixtures
{
    internal const string DefaultActorId = "npc_life_001_start_teacher";
    internal const string DefaultLocationId = "loc_life_001_start";
    internal const string DefaultLocationName = "Test location";

    internal static JsonObject CreateNpcCoreRoot(
        string actorId = DefaultActorId,
        string currentLocationId = DefaultLocationId,
        string currentLocationName = DefaultLocationName) =>
        new()
        {
            ["NPCsInScene"] = new JsonArray(
                CreateActor(actorId, currentLocationId, currentLocationName))
        };

    internal static JsonObject CreateActor(
        string actorId,
        string currentLocationId = DefaultLocationId,
        string currentLocationName = DefaultLocationName) =>
        new()
        {
            ["NPCId"] = actorId,
            ["name"] = "Test mentor",
            ["role"] = "Test fixture actor",
            ["summary"] = "A complete setting-neutral actor used by contract tests.",
            ["image_prompt"] = "setting neutral test character portrait, realistic lighting",
            ["rarity"] = "Common",
            ["worldview"] = "Knowledge should be applied carefully.",
            ["personalityArchetype"] = "careful mentor",
            ["culturalStance"] = "Pragmatist",
            ["race"] = "Test ancestry",
            ["class"] = "Test profession",
            ["appearanceDescription"] = "A plainly dressed test actor with an attentive expression.",
            ["history"] = "This actor exists only as explicit test authority.",
            ["progressionType"] = "static_test_npc",
            ["currentLocationId"] = currentLocationId,
            ["currentLocationName"] = currentLocationName,
            ["initialLocationId"] = null,
            ["age"] = 43,
            ["level"] = 2,
            ["experience"] = 0,
            ["experienceForNextLevel"] = 150,
            ["relationshipLevel"] = 25,
            ["attitude"] = "Neutral",
            ["playerCompanionDirective"] = "not_companion",
            ["culturalLayer"] = "test culture",
            ["personalityTraits"] = new JsonArray(),
            ["maxWeight"] = 35,
            ["totalWeight"] = 0,
            ["isOverloaded"] = false,
            ["progressionTrackers"] = new JsonObject(),
            ["plans"] = "Exercise the explicit actor contract.",
            ["personalQuests"] = new JsonArray(),
            ["relationshipLock"] = new JsonObject
            {
                ["isLocked"] = false,
                ["breakthroughQuestId"] = null
            },
            ["characteristics"] = new JsonObject
            {
                ["intelligence"] = 5,
                ["setting_defined_focus"] = 4
            },
            ["activeSkills"] = new JsonArray(),
            ["passiveSkills"] = new JsonArray(),
            ["equippedItems"] = new JsonObject(),
            ["fateCards"] = new JsonArray(),
            ["inventory"] = new JsonArray(),
            ["goals"] = new JsonObject
            {
                ["shortTerm"] = "Complete the current contract test.",
                ["longTerm"] = "Remain a stable explicit fixture."
            },
            ["teacherProfile"] = new JsonObject
            {
                ["canTeach"] = true,
                ["relationshipLevel"] = 25,
                ["summary"] = "Can expose one setting-neutral test skill.",
                ["skills"] = new JsonArray
                {
                    new JsonObject
                    {
                        ["skillId"] = "setting_defined_test_skill",
                        ["skillName"] = "Setting-defined test skill",
                        ["displayName"] = "Setting-defined test skill",
                        ["skillKind"] = "passive_skill_mastery",
                        ["masteryLevel"] = 2,
                        ["currentMasteryLevel"] = 2,
                        ["maxMasteryLevel"] = 2,
                        ["summary"] = "A test-only skill supplied by explicit fixture authority."
                    }
                }
            }
        };

    internal static JsonObject CreateInventoryItem(
        string itemId,
        string currentLocationId = DefaultLocationId,
        string currentLocationName = DefaultLocationName) =>
        new()
        {
            ["itemId"] = itemId,
            ["existedId"] = itemId,
            ["name"] = "Test actor item",
            ["description"] = "A complete setting-neutral inventory item used by continuity tests.",
            ["image_prompt"] = "setting neutral test inventory object, realistic studio lighting",
            ["quality"] = "Common",
            ["price"] = 0,
            ["count"] = 1,
            ["weight"] = 0.1,
            ["volume"] = 0.01,
            ["contentsPath"] = null,
            ["isContainer"] = false,
            ["isConsumption"] = false,
            ["requiresTwoHands"] = false,
            ["durability"] = "100%",
            ["type"] = "Test item",
            ["group"] = "Test fixtures",
            ["textContent"] = new JsonArray("Explicit test fixture content."),
            ["journalEntries"] = null,
            ["equipmentSlot"] = null,
            ["accessoryForSlot"] = null,
            ["currentLocationId"] = currentLocationId,
            ["currentLocationName"] = currentLocationName,
            ["isCarried"] = false,
            ["isEquipped"] = false,
            ["visibility"] = "known"
        };
}
