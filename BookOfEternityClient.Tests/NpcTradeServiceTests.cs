using System.Text.Json;
using System.Text.Json.Nodes;
using BookOfEternityClient.Services;
using Xunit;

namespace BookOfEternityClient.Tests;

public sealed class NpcTradeServiceTests
{
    [Fact]
    public void EvaluateTradeAvailability_MatchesForJsonElementAndJsonObject_WhenTradeIsAvailable()
    {
        const string json = """
        {
          "npcId": "npc_merchant_001",
          "name": "Марек",
          "currentLocationId": "loc_market_square",
          "currentLocation": "Рыночная площадь",
          "tradeState": {
            "canTrade": true,
            "merchantProfile": "GeneralGoods"
          }
        }
        """;

        using var doc = JsonDocument.Parse(json);
        var node = JsonNode.Parse(json)!.AsObject();

        var fromElement = NpcTradeService.EvaluateTradeAvailability(doc.RootElement, "loc_market_square", "Рыночная площадь");
        var fromObject = NpcTradeService.EvaluateTradeAvailability(node, "loc_market_square", "Рыночная площадь");

        Assert.True(fromElement.IsMerchant);
        Assert.True(fromElement.TradeAvailable);
        Assert.Null(fromElement.BlockReason);
        Assert.Equal("GeneralGoods", fromElement.MerchantProfile);
        Assert.Equal(fromElement, fromObject);
    }

    [Fact]
    public void EvaluateTradeAvailability_BlocksWhenNpcIsOutsideCurrentLocation()
    {
        const string json = """
        {
          "npcId": "npc_merchant_001",
          "name": "Марек",
          "currentLocationId": "loc_market_square",
          "currentLocation": "Рыночная площадь",
          "tradeState": {
            "canTrade": true,
            "merchantProfile": "GeneralGoods"
          }
        }
        """;

        using var doc = JsonDocument.Parse(json);

        var availability = NpcTradeService.EvaluateTradeAvailability(doc.RootElement, "loc_other_square", "Другая площадь");

        Assert.True(availability.IsMerchant);
        Assert.False(availability.TradeAvailable);
        Assert.Equal("Доступна только в текущей локации торговца.", availability.BlockReason);
    }

    [Fact]
    public void EvaluateTradeAvailability_BlocksWhenTradeStateIsDisabled()
    {
        const string json = """
        {
          "npcId": "npc_merchant_001",
          "name": "Марек",
          "currentLocationId": "loc_market_square",
          "currentLocation": "Рыночная площадь",
          "tradeState": {
            "canTrade": false,
            "merchantProfile": "GeneralGoods",
            "tradeBlockedReason": "Торговля сейчас недоступна."
          }
        }
        """;

        using var doc = JsonDocument.Parse(json);

        var availability = NpcTradeService.EvaluateTradeAvailability(doc.RootElement, "loc_market_square", "Рыночная площадь");

        Assert.True(availability.IsMerchant);
        Assert.False(availability.TradeAvailable);
        Assert.Equal("Торговля сейчас недоступна.", availability.BlockReason);
    }

    [Fact]
    public void EvaluateTradeAvailability_UsesPlayerFacingReasonWhenMerchantTradeStateIsMissing()
    {
        const string json = """
        {
          "npcId": "npc_merchant_001",
          "name": "Марек",
          "currentLocationId": "loc_market_square",
          "currentLocation": "Рыночная площадь",
          "tradeState": {
            "merchantProfile": "GeneralGoods"
          }
        }
        """;

        using var doc = JsonDocument.Parse(json);
        var node = JsonNode.Parse(json)!.AsObject();

        var fromElement = NpcTradeService.EvaluateTradeAvailability(doc.RootElement, "loc_market_square", "Рыночная площадь");
        var fromObject = NpcTradeService.EvaluateTradeAvailability(node, "loc_market_square", "Рыночная площадь");

        Assert.True(fromElement.IsMerchant);
        Assert.False(fromElement.TradeAvailable);
        Assert.Equal("Торговля сейчас недоступна.", fromElement.BlockReason);
        Assert.DoesNotContain("tradeState", fromElement.BlockReason, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("canTrade", fromElement.BlockReason, StringComparison.OrdinalIgnoreCase);
        Assert.Equal(fromElement, fromObject);
    }

    [Fact]
    public void EvaluateTradeAvailability_RecognizesNonMerchantNpc()
    {
        const string json = """
        {
          "npcId": "npc_commoner_001",
          "name": "Ивар",
          "currentLocationId": "loc_market_square",
          "currentLocation": "Рыночная площадь"
        }
        """;

        using var doc = JsonDocument.Parse(json);

        var availability = NpcTradeService.EvaluateTradeAvailability(doc.RootElement, "loc_market_square", "Рыночная площадь");

        Assert.False(availability.IsMerchant);
        Assert.False(availability.TradeAvailable);
        Assert.Equal("Этот НПС не является торговцем.", availability.BlockReason);
    }
}
