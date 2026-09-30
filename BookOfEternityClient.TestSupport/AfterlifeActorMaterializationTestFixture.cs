using System.Text.Json.Nodes;
using BookOfEternityClient.Services;

namespace BookOfEternityClient.Tests;

internal static class AfterlifeActorMaterializationTestFixture
{
    internal static JsonObject CreateCompleteProfile(
        string actorType,
        string actorId,
        string realm,
        int materializedAtTurn,
        JsonArray? specialArts = null)
    {
        specialArts ??= new JsonArray();
        var hasSpecialArts = specialArts.Count > 0;
        return new JsonObject
        {
            ["actorType"] = actorType,
            ["actorId"] = actorId,
            ["displayName"] = "Хранитель точного договора",
            ["appearanceDescription"] =
                "Силуэт сущности очерчен ровным сиянием и строгими линиями духовного одеяния.",
            ["profileSummary"] =
                "Хранитель точных договоров и непрерывности личной истории.",
            ["personalityProfile"] = new JsonObject
            {
                ["archetype"] = "Хранитель договора",
                ["worldview"] = "Личная власть требует доказуемой ответственности."
            },
            ["motivation"] =
                "Сохранять точную связь между решениями и их последствиями.",
            ["realm"] = realm,
            ["locationId"] = "location_exact_afterlife_gate",
            ["locationName"] = "Врата точного договора",
            ["currencies"] = new JsonObject
            {
                ["inkFeathers"] = 0,
                ["lightSparks"] = 0
            },
            ["progression"] = new JsonObject
            {
                ["enlightenment"] = new JsonObject
                {
                    ["experience"] = 0,
                    ["tier"] = 0
                },
                ["radiance"] = new JsonObject
                {
                    ["experience"] = 0,
                    ["tier"] = 0
                }
            },
            ["standardArts"] = new JsonObject
            {
                ["guard"] = 1,
                [AfterlifeSpiritualConflictState.SpiritualResilienceArtId] = 0,
                [AfterlifeSpiritualConflictState.SpiritualHealingArtId] = 0
            },
            ["specialArts"] = specialArts.DeepClone(),
            ["customStates"] = new JsonArray(),
            ["fateCards"] = new JsonArray(),
            ["relationships"] = new JsonArray(),
            ["goals"] = new JsonObject
            {
                ["goalId"] = $"goal_{actorId}",
                ["shortTermGoal"] = "Сохранить точную связь между источниками.",
                ["longTermGoal"] = "Поддерживать целостность своей роли.",
                ["plan"] = "Проверять собственные записи и действовать согласно роли.",
                ["gmThoughtsSummary"] =
                    "Я должен сохранить точную связь между источниками.",
                ["updatedAtTurn"] = materializedAtTurn
            },
            ["personalQuests"] = new JsonArray(),
            ["currentActivity"] = null,
            ["completedActivities"] = new JsonArray(),
            ["soulDissipationTier"] = 0,
            ["progressionStrategy"] = new JsonObject
            {
                ["strategyId"] = $"strategy_{actorId}",
                ["summary"] = "Сохраняет точную связь между источниками.",
                ["priorityOrder"] = new JsonArray("guard")
            },
            ["ledger"] = new JsonArray(),
            ["progressionLedger"] = new JsonArray(),
            ["gmThoughtsSummary"] = "Я помню, зачем принял эту роль.",
            [ActorMaterializationContract.PropertyName] = new JsonObject
            {
                ["schemaVersion"] = 1,
                ["materializationId"] = $"mat_{actorType}_{actorId}_{materializedAtTurn}",
                ["actorType"] = actorType,
                ["actorId"] = actorId,
                ["materializedAtTurn"] = materializedAtTurn,
                ["state"] = "complete",
                ["capabilities"] = new JsonObject
                {
                    ["canFight"] = true,
                    ["canTeach"] = false,
                    ["canTrade"] = false
                },
                ["sections"] = new JsonObject
                {
                    ["standardArts"] = new JsonObject { ["state"] = "populated" },
                    ["specialArts"] = hasSpecialArts
                        ? new JsonObject { ["state"] = "populated" }
                        : EmptyByDesign("Личное духовное искусство ещё не сформировано."),
                    ["customStates"] = EmptyByDesign("Особых духовных состояний сейчас нет."),
                    ["fateCards"] = EmptyByDesign("Карта судьбы ещё не открыта."),
                    ["relationships"] = EmptyByDesign("Устойчивые связи ещё не сложились."),
                    ["agency"] = new JsonObject { ["state"] = "populated" },
                    ["progressionHistory"] =
                        EmptyByDesign("История развития ещё не началась.")
                }
            }
        };
    }

    private static JsonObject EmptyByDesign(string reason) => new()
    {
        ["state"] = "empty_by_design",
        ["reason"] = reason
    };
}
