using System.Text.Json;
using System.Text.Json.Nodes;
using BookOfEternityClient.UI;
using Xunit;

namespace BookOfEternityClient.Tests;

public sealed class MortalItemPlayerProjectionTests
{
    [Fact]
    public void ItemProjection_SuppressesAuthorityDtoShapesAndPreservesAdjacentSemantics()
    {
        var source = new JsonObject
        {
            ["serviceInstructions"] = CreateRepairPacket(),
            ["movementRecord"] = CreateTransition(),
            ["placementCoordinate"] = CreateCarrier(),
            ["annotatedPlacementCoordinate"] = CreateAnnotatedCarrier(),
            ["embeddedEnvelope"] = MortalItemTestFixture.CreateCanonicalRoot()["materialization"]!.DeepClone(),
            ["embeddedIdentityIndex"] = CreateIdentityIndex(),
            ["embeddedSourceAuthority"] = new JsonObject
            {
                ["kind"] = "npc_trade_receipt",
                ["authorityId"] = "trade_cycle_private"
            },
            ["annotatedSourceAuthority"] = new JsonObject
            {
                ["kind"] = "npc_trade_receipt",
                ["authorityId"] = "trade_cycle_private",
                ["note"] = "PRIVATE_ANNOTATED_AUTHORITY_NOTE"
            },
            ["legitimateMechanic"] = new JsonObject
            {
                ["kind"] = "ritual",
                ["title"] = "Памятка кузнеца",
                ["turn"] = 3
            },
            ["mixedSemantic"] = new JsonObject
            {
                ["title"] = "Обычная игровая подсказка",
                ["steps"] = new JsonArray("Ударить по наковальне трижды"),
                ["expectedAuthority"] = "PRIVATE_SINGLE_ACCIDENTAL_FIELD"
            },
            ["legitimateWorldState"] = new JsonObject
            {
                ["realm"] = "Мир смертных",
                ["state"] = "ожидание",
                ["sections"] = new JsonArray("кузница", "ворота")
            }
        };

        var projected = Assert.IsType<JsonObject>(MortalItemPlayerProjection.CloneItemSemanticValue(source));

        Assert.Null(projected["serviceInstructions"]);
        Assert.Null(projected["movementRecord"]);
        Assert.Null(projected["placementCoordinate"]);
        Assert.Null(projected["annotatedPlacementCoordinate"]);
        Assert.Null(projected["embeddedEnvelope"]);
        Assert.Null(projected["embeddedIdentityIndex"]);
        Assert.Null(projected["embeddedSourceAuthority"]);
        Assert.Null(projected["annotatedSourceAuthority"]);
        Assert.Equal("ritual", projected["legitimateMechanic"]?["kind"]?.GetValue<string>());
        Assert.Equal("Памятка кузнеца", projected["legitimateMechanic"]?["title"]?.GetValue<string>());
        Assert.Equal(3, projected["legitimateMechanic"]?["turn"]?.GetValue<int>());
        Assert.Equal("Обычная игровая подсказка", projected["mixedSemantic"]?["title"]?.GetValue<string>());
        Assert.Equal(
            "Ударить по наковальне трижды",
            projected["mixedSemantic"]?["steps"]?[0]?.GetValue<string>());
        Assert.Null(projected["mixedSemantic"]?["expectedAuthority"]);
        Assert.Equal("Мир смертных", projected["legitimateWorldState"]?["realm"]?.GetValue<string>());
        Assert.Equal("ожидание", projected["legitimateWorldState"]?["state"]?.GetValue<string>());

        using var document = JsonDocument.Parse(source.ToJsonString());
        var formatted = MortalItemPlayerProjection.FormatSemanticValue(document.RootElement);
        Assert.Contains("Памятка кузнеца", formatted, StringComparison.Ordinal);
        Assert.Contains("Обычная игровая подсказка", formatted, StringComparison.Ordinal);
        Assert.DoesNotContain("Служебное задание ремонта предмета", formatted, StringComparison.Ordinal);
        Assert.DoesNotContain("transfer", formatted, StringComparison.Ordinal);
        Assert.DoesNotContain("player_inventory", formatted, StringComparison.Ordinal);
        Assert.DoesNotContain("ownershipAndPlacement", formatted, StringComparison.Ordinal);
        Assert.DoesNotContain("active", formatted, StringComparison.Ordinal);
    }

    private static JsonObject CreateRepairPacket() =>
        new()
        {
            ["kind"] = "mortal_item_materialization_repair",
            ["priority"] = "critical",
            ["title"] = "Служебное задание ремонта предмета",
            ["targetFiles"] = new JsonArray("game_state/inventory/items.json"),
            ["expectedAuthority"] = new JsonArray("receipt"),
            ["actualEvidence"] = new JsonArray("creationRef"),
            ["steps"] = new JsonArray("Открыть validation_repair_request.json"),
            ["doNotDo"] = new JsonArray("Не изменять item_identity_index.json")
        };

    private static JsonObject CreateTransition() =>
        new()
        {
            ["transitionId"] = "mitrn_private",
            ["kind"] = "transfer",
            ["turn"] = 12,
            ["sourceItemIds"] = new JsonArray("itm_private"),
            ["sourceCarrier"] = CreateCarrier(),
            ["destinationCarrier"] = CreateCarrier(),
            ["quantityBefore"] = 1,
            ["quantityAfter"] = 1,
            ["authorityKind"] = "trade_receipt",
            ["authorityId"] = "receipt_private"
        };

    private static JsonObject CreateCarrier() =>
        new()
        {
            ["kind"] = "player_inventory",
            ["ownerId"] = "player",
            ["containerId"] = null,
            ["containerPath"] = new JsonArray()
        };

    private static JsonObject CreateAnnotatedCarrier()
    {
        var carrier = CreateCarrier();
        carrier["note"] = "PRIVATE_ANNOTATED_CARRIER_NOTE";
        return carrier;
    }

    private static JsonObject CreateIdentityIndex() =>
        new()
        {
            ["schemaVersion"] = 1,
            ["entries"] = new JsonArray(
                new JsonObject
                {
                    ["itemId"] = "itm_private",
                    ["receiptId"] = "mirec_private",
                    ["state"] = "active",
                    ["currentCarrier"] = CreateCarrier(),
                    ["originMaterializationIds"] = new JsonArray("mat_private"),
                    ["originCreationRefs"] = new JsonArray("new_item_private"),
                    ["parentItemIds"] = new JsonArray(),
                    ["mergedIntoItemId"] = null,
                    ["transitions"] = new JsonArray(CreateTransition())
                })
        };
}
