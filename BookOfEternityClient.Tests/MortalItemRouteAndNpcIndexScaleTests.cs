using System.Text.Json.Nodes;
using BookOfEternityClient.Services;
using Xunit;

namespace BookOfEternityClient.Tests;

public sealed class MortalItemRouteAndNpcIndexScaleTests
{
    [Fact]
    public void TradeAuthorityIndex_DoublingPopulationStaysWithinTwoPointFiveTimesWork()
    {
        var onePopulation = CreateTradePopulation(200);
        var twoPopulation = CreateTradePopulation(400);

        var one = MortalItemRouteAuthorityCatalog.MeasureTradeAuthorityWork(
            onePopulation.Requests,
            onePopulation.Npcs);
        var two = MortalItemRouteAuthorityCatalog.MeasureTradeAuthorityWork(
            twoPopulation.Requests,
            twoPopulation.Npcs);

        Assert.True(one > 0);
        Assert.True(
            two <= one * 2.5,
            $"Expected linear trade-authority work, but {one} visits became {two}.");
    }

    [Fact]
    public void TradeAuthorityIndex_DoublingRequestsAndOffersOnOneSurfaceStaysWithinTwoPointFiveTimesWork()
    {
        var onePopulation = CreateDenseTradePopulation(200);
        var twoPopulation = CreateDenseTradePopulation(400);

        var one = MortalItemRouteAuthorityCatalog.MeasureTradeAuthorityWork(
            onePopulation.Requests,
            onePopulation.Npcs);
        var two = MortalItemRouteAuthorityCatalog.MeasureTradeAuthorityWork(
            twoPopulation.Requests,
            twoPopulation.Npcs);

        Assert.True(one > 0);
        Assert.True(
            two <= one * 2.5,
            $"Expected linear dense trade-authority work, but {one} visits became {two}.");
    }

    [Fact]
    public void NpcCommandIndex_DoublingPopulationStaysWithinTwoPointFiveTimesWork()
    {
        var onePopulation = CreateNpcCommandPopulation(200);
        var twoPopulation = CreateNpcCommandPopulation(400);

        var one = CanonicalStateNormalizer.MeasureMortalNpcCommandIndexWork(
            onePopulation.Npcs,
            onePopulation.Commands);
        var two = CanonicalStateNormalizer.MeasureMortalNpcCommandIndexWork(
            twoPopulation.Npcs,
            twoPopulation.Commands);

        Assert.True(one > 0);
        Assert.True(
            two <= one * 2.5,
            $"Expected linear NPC-command work, but {one} visits became {two}.");
    }

    private static (JsonObject Requests, JsonObject Npcs) CreateTradePopulation(int count)
    {
        var requests = new JsonArray();
        var npcs = new JsonArray();
        for (var index = 0; index < count; index++)
        {
            var npcId = $"npc_trade_scale_{index}";
            var requestId = $"trade_request_scale_{index}";
            var cycleId = $"trade_cycle_scale_{index}";
            var creationRef = $"trade_slot_scale_{index}";
            var item = MortalItemTestFixture.CreateRawRoot(
                "trade_output",
                "npc_trade_receipt",
                requestId,
                creationRef: creationRef,
                materializationId: $"mat_trade_item_scale_{index}");
            item["tradeItemClass"] = "Functional";
            item["baseSellPrice"] = 4;
            var offerItemData = item.DeepClone().AsObject();
            offerItemData.Remove("existedId");
            offerItemData.Remove("creationRef");
            offerItemData.Remove("materialization");
            offerItemData["itemId"] = $"trade_offer_item_scale_{index}";
            requests.Add(new JsonObject
            {
                ["requestId"] = requestId,
                ["npcId"] = npcId,
                ["merchantProfile"] = "GeneralGoods",
                ["tradeCycleId"] = cycleId,
                ["derivedTradeSlotCount"] = 1,
                ["refreshAfterWorldDate"] = 2
            });
            npcs.Add(new JsonObject
            {
                ["NPCId"] = npcId,
                ["tradeInventory"] = new JsonObject
                {
                    ["tradeCycleId"] = cycleId,
                    ["refreshAfterWorldDate"] = 2,
                    ["items"] = new JsonArray(new JsonObject
                    {
                        ["slotId"] = creationRef,
                        ["itemId"] = $"trade_offer_item_scale_{index}",
                        ["merchantProfile"] = "GeneralGoods",
                        ["itemData"] = offerItemData
                    })
                },
            });
        }

        return (
            new JsonObject { [NpcTradeRequestState.RequestsProperty] = requests },
            new JsonObject
            {
                ["UpdateNPCs"] = new JsonArray(),
                ["NPCsInScene"] = npcs,
                [NpcTradeRequestState.UpdateReceiptsProperty] = new JsonArray(
                    Enumerable.Range(0, count)
                        .Select(index => (JsonNode)new JsonObject
                        {
                            ["requestId"] = $"trade_request_scale_{index}",
                            ["npcId"] = $"npc_trade_scale_{index}",
                            ["tradeCycleId"] = $"trade_cycle_scale_{index}",
                            ["merchantProfile"] = "GeneralGoods",
                            ["status"] = NpcTradeRequestState.ReceiptStatusReady,
                            ["itemCount"] = 1,
                            ["resolvedAtTurn"] = 42,
                            ["resolvedAtUtc"] = "2026-08-11T00:00:01Z"
                        })
                        .ToArray())
            });
    }

    private static (JsonObject Npcs, JsonObject Commands) CreateNpcCommandPopulation(int count)
    {
        var npcs = new JsonArray();
        var adds = new JsonArray();
        var equipment = new JsonArray();
        for (var index = 0; index < count; index++)
        {
            var npcId = $"npc_command_scale_{index}";
            var itemId = $"itm_command_scale_{index}";
            npcs.Add(new JsonObject
            {
                ["NPCId"] = npcId,
                ["inventory"] = new JsonArray(new JsonObject { ["itemId"] = itemId })
            });
            adds.Add(new JsonObject
            {
                ["npcId"] = npcId,
                ["item"] = new JsonObject
                {
                    ["existedId"] = null,
                    ["creationRef"] = $"new_command_scale_{index}"
                }
            });
            equipment.Add(new JsonObject
            {
                ["npcId"] = npcId,
                ["itemId"] = itemId
            });
        }

        return (
            new JsonObject
            {
                ["UpdateNPCs"] = new JsonArray(),
                ["NPCsInScene"] = npcs
            },
            new JsonObject
            {
                ["NPCInventoryAdds"] = adds,
                ["NPCEquipmentChanges"] = equipment
            });
    }

    private static (JsonObject Requests, JsonObject Npcs) CreateDenseTradePopulation(int count)
    {
        const string npcId = "npc_trade_dense";
        const string cycleId = "trade_cycle_dense";
        var requests = new JsonArray();
        var offers = new JsonArray();
        var receipts = new JsonArray();
        for (var index = 0; index < count; index++)
        {
            var requestId = $"trade_request_dense_{index}";
            requests.Add(new JsonObject
            {
                ["requestId"] = requestId,
                ["npcId"] = npcId,
                ["merchantProfile"] = "GeneralGoods",
                ["tradeCycleId"] = cycleId,
                ["derivedTradeSlotCount"] = count,
                ["refreshAfterWorldDate"] = 2
            });
            offers.Add(new JsonObject
            {
                ["slotId"] = $"trade_slot_dense_{index}",
                ["itemId"] = $"trade_offer_item_dense_{index}",
                ["price"] = 10,
                ["merchantProfile"] = "GeneralGoods",
                ["soldOut"] = false,
                ["itemData"] = new JsonObject
                {
                    ["itemId"] = $"trade_offer_item_dense_{index}",
                    ["name"] = $"Dense item {index}",
                    ["quality"] = "Common",
                    ["tradeItemClass"] = "Functional",
                    ["price"] = 10,
                    ["baseSellPrice"] = 4
                }
            });
            receipts.Add(new JsonObject
            {
                ["requestId"] = requestId,
                ["npcId"] = npcId,
                ["tradeCycleId"] = cycleId,
                ["merchantProfile"] = "GeneralGoods",
                ["status"] = NpcTradeRequestState.ReceiptStatusReady,
                ["itemCount"] = count,
                ["resolvedAtTurn"] = 42,
                ["resolvedAtUtc"] = "2026-08-11T00:00:01Z"
            });
        }

        return (
            new JsonObject { [NpcTradeRequestState.RequestsProperty] = requests },
            new JsonObject
            {
                ["UpdateNPCs"] = new JsonArray(),
                ["NPCsInScene"] = new JsonArray(new JsonObject
                {
                    ["NPCId"] = npcId,
                    ["tradeInventory"] = new JsonObject
                    {
                        ["tradeCycleId"] = cycleId,
                        ["refreshAfterWorldDate"] = 2,
                        ["items"] = offers
                    }
                }),
                [NpcTradeRequestState.UpdateReceiptsProperty] = receipts
            });
    }
}
