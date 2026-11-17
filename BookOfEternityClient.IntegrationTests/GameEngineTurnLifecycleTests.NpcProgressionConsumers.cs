using System.Text.Json.Nodes;
using BookOfEternityClient.Core;
using BookOfEternityClient.Services;
using Microsoft.Extensions.Logging.Abstractions;
using Xunit;

namespace BookOfEternityClient.Tests;

public sealed partial class GameEngineTurnLifecycleTests
{
    private async Task SeedProgressionJourneyConsumersAsync()
    {
        var core = JsonNode.Parse((await _fs.ReadFileAsync(NpcCoreChangesContract.NpcCorePath))!)!.AsObject();
        var actor = core["NPCsInScene"]![0]!.AsObject();
        actor["level"] = 15; actor["experience"] = 87141; actor["experienceForNextLevel"] = 87142;
        actor["characteristics"]!["modifiedTrade"] = 14;
        actor["tradeState"] = new JsonObject { ["canTrade"] = true, ["merchantProfile"] = "GeneralGoods" };
        var playerCharacteristics = JsonNode.Parse((await _fs.ReadFileAsync("game_state/misc/characteristics.json"))!)!.AsObject();
        playerCharacteristics["modifiedTrade"] = 12;
        playerCharacteristics["trade"] = 12;
        await _fs.WriteFileAtomicAsync("game_state/misc/characteristics.json", playerCharacteristics.ToJsonString());
        var slots = new JsonArray();
        var inventory = new JsonArray();
        var index = JsonNode.Parse((await _fs.ReadFileAsync(MortalItemIdentityState.StatePath))!)!.AsObject();
        for (var i = 1; i <= 7; i++)
        {
            var slot = new JsonObject
            {
                ["slotId"] = $"journey_slot_{i}", ["itemId"] = $"journey_item_{i}",
                ["price"] = 25, ["merchantProfile"] = "GeneralGoods", ["soldOut"] = false,
                ["itemData"] = new JsonObject
                {
                    ["itemId"] = $"journey_item_{i}", ["name"] = $"Записная книжка {i}",
                    ["description"] = "Принятый товар до получения опыта.", ["type"] = "Document",
                    ["tradeItemClass"] = "FlavorOrUtility", ["quality"] = "Common", ["price"] = 20,
                    ["baseSellPrice"] = 8, ["weight"] = 0.1, ["group"] = "Документы и медиа"
                }
            };
            var item = MortalItemTestFixture.CreateCanonicalTradeStock(slot, ProgressionJourneyActor, acceptedAtTurn: 40);
            inventory.Add(item);
            var itemIndex = MortalItemTestFixture.CreateIndexForCarrier(item, "npc_inventory", ProgressionJourneyActor);
            index["entries"]!.AsArray().Add(itemIndex["entries"]![0]!.DeepClone());
            slots.Add(slot);
        }
        actor["inventory"] = inventory;
        actor["totalWeight"] = 0.7;
        actor["tradeInventory"] = new JsonObject
        {
            ["tradeCycleId"] = "world_trade_0", ["generatedAtWorldDate"] = 480,
            ["refreshAfterWorldDate"] = 43200, ["generationTradeTier"] = "Good", ["pricingTradeTier"] = "Neutral",
            ["items"] = slots
        };
        actor["tradeInventoryReceipts"] = new JsonArray(new JsonObject
        {
            ["requestId"] = "journey_trade_seed", ["npcId"] = ProgressionJourneyActor, ["npcName"] = "Лев Наблюдатель",
            ["tradeCycleId"] = "world_trade_0", ["merchantProfile"] = "GeneralGoods", ["status"] = "ready",
            ["itemCount"] = 7, ["resolvedAtTurn"] = 40, ["resolvedAtUtc"] = "2026-11-17T00:00:00Z"
        });
        actor["teacherProfile"] = MortalActorTestFixtures.CreateActor(ProgressionJourneyActor)["teacherProfile"]!.DeepClone();
        actor["passiveSkills"] = JsonNode.Parse("""
        [{"skillId":"setting_defined_test_skill","skillName":"Setting-defined test skill",
          "skillDescription":"Setting-neutral observation knowledge.","rarity":"Common","type":"KnowledgeBased",
          "group":"Observation","masteryLevel":2,"currentMasteryLevel":2,"maxMasteryLevel":2,
          "currentMasteryProgress":0,"masteryProgressNeeded":100,"structuredBonuses":null,"playerStatBonus":null}]
        """);
        actor["trainingShowcase"] = new JsonObject
        {
            ["showcaseId"] = "journey_teacher", ["sourceActorId"] = ProgressionJourneyActor,
            ["sourceActorName"] = "Лев Наблюдатель", ["realm"] = "mortal",
            ["sourceActorSnapshotHash"] = TrainingService.ComputeSourceSnapshotHash(actor),
            ["offers"] = JsonNode.Parse("""
            [{"offerId":"journey_observation_unlock","targetId":"setting_defined_test_skill",
              "targetName":"Setting-defined test skill","targetKind":"passive_skill_unlock",
              "currentValue":0,"targetValue":1,"sourceCap":2,"summary":"Наставник объясняет основы наблюдения.",
              "requirements":{"minimumRelationship":0},"cost":{"money":1,"currentLevelExperiencePercent":0}}]
            """)
        };
        await _fs.WriteFileAtomicAsync(MortalItemIdentityState.StatePath, index.ToJsonString());
        await _fs.WriteFileAtomicAsync(NpcCoreChangesContract.NpcCorePath, core.ToJsonString());
    }

    private async Task<JsonObject> CaptureReadyJourneyConsumersAsync()
    {
        var trade = await new NpcTradeService(_fs, NullLogger<NpcTradeService>.Instance)
            .EnsureTradeInventoryAsync(ProgressionJourneyActor, currentTurn: 41);
        Assert.NotNull(trade); Assert.True(trade!.InventoryReady); Assert.Equal(7, trade.Offers.Count);
        Assert.Empty(await NpcTradeRequestState.ReadRequestsAsync(_fs));
        var training = await new TrainingService(_fs, NullLogger<TrainingService>.Instance)
            .EnsureTrainingAsync(41, createPendingRequests: false);
        var teacher = Assert.Single(training.Teachers);
        Assert.True(teacher.ShowcaseReady); Assert.False(teacher.ShowcaseStale);
        Assert.True(Assert.Single(teacher.Offers).Available);
        Assert.Empty(await TrainingRequestState.ReadRequestsAsync(_fs));
        return JsonNode.Parse((await _fs.ReadFileAsync(NpcCoreChangesContract.NpcCorePath))!)!["NPCsInScene"]![0]!.AsObject();
    }

    private static async Task AssertJourneyConsumersAfterProgressionAsync(FileSystemManager files, JsonObject baseline)
    {
        var protectedPaths = new[] { NpcCoreChangesContract.NpcCorePath, "game_state/core/player_status.json",
            "game_state/player/experience.json", "game_state/player/skills_active.json", "game_state/player/skills_passive.json",
            "game_state/player/skill_mastery.json", MortalItemIdentityState.StatePath };
        var protectedBefore = new Dictionary<string, byte[]?>();
        foreach (var path in protectedPaths) protectedBefore[path] = await files.ReadFileBytesAsync(path);
        var actor = JsonNode.Parse((await files.ReadFileAsync(NpcCoreChangesContract.NpcCorePath))!)!["NPCsInScene"]![0]!;
        Assert.Equal(16, actor["level"]!.GetValue<int>());
        foreach (var property in new[] { "tradeInventory", "tradeInventoryReceipts", "inventory", "trainingShowcase", "passiveSkills" })
            Assert.True(JsonNode.DeepEquals(baseline[property], actor[property]), property);
        var trainingService = new TrainingService(files, NullLogger<TrainingService>.Instance);
        var training = await trainingService.EnsureTrainingAsync(42, createPendingRequests: false);
        var teacher = Assert.Single(training.Teachers);
        Assert.False(teacher.ShowcaseReady); Assert.True(teacher.ShowcaseStale); Assert.Empty(teacher.Offers);
        var purchase = await trainingService.BuyTrainingAsync(ProgressionJourneyActor, "journey_observation_unlock", 42);
        Assert.False(purchase.Success); Assert.False(purchase.StateChanged); Assert.Contains("устарела", purchase.Message);
        var trade = await new NpcTradeService(files, NullLogger<NpcTradeService>.Instance)
            .EnsureTradeInventoryAsync(ProgressionJourneyActor, currentTurn: 42);
        Assert.NotNull(trade); Assert.False(trade!.InventoryReady); Assert.Empty(trade.Offers);
        Assert.True(trade.InventoryRequestCreatedThisCall);
        var request = Assert.Single(await NpcTradeRequestState.ReadRequestsAsync(files));
        Assert.Equal(8, request.DerivedTradeSlotCount); Assert.Equal(ProgressionJourneyActor, request.NpcId);
        foreach (var path in protectedPaths) Assert.Equal(protectedBefore[path], await files.ReadFileBytesAsync(path));
    }
}
