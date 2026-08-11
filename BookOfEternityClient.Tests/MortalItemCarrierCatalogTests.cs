using System.Text.Json.Nodes;
using BookOfEternityClient.Services;
using Xunit;

namespace BookOfEternityClient.Tests;

public sealed class MortalItemCarrierCatalogTests
{
    [Fact]
    public void Build_IndexesEverySupportedCarrierAndRawNpcAdd()
    {
        var playerItem = MortalItemTestFixture.CreateCanonicalRoot("itm_player");
        var npcItem = MortalItemTestFixture.CreateCanonicalRoot("itm_npc");
        var storageItem = MortalItemTestFixture.CreateCanonicalRoot("itm_storage");
        var vehicleItem = MortalItemTestFixture.CreateCanonicalRoot("itm_vehicle");
        var rawNpcItem = MortalItemTestFixture.CreateRawRoot(
            route: "npc_acquisition",
            authorityKind: "npc_inventory_add",
            authorityId: "npc_add_42",
            creationRef: "new_item_npc_add",
            materializationId: "mat_item_npc_add");

        var result = MortalItemCarrierCatalog.Build(new MortalItemCarrierCatalogInput(
            PlayerInventory(playerItem),
            NpcCore("npc_owner", npcItem),
            NpcAdds("npc_existing", rawNpcItem),
            CurrentLocation("loc_test", "storage_test", storageItem),
            Vehicles("vehicle_test", vehicleItem),
            EmptyCompanions()));

        Assert.Empty(result.Issues);
        Assert.Equal(5, result.Occurrences.Count);
        Assert.Equal(4, result.ByItemId.Count);
        Assert.Equal(5, result.ByCreationRef.Count);
        Assert.Equal(4, result.ByReceiptId.Count);
        Assert.Equal(5, result.ByMaterializationId.Count);

        AssertCarrier(result, "itm_player", "player_inventory", "player", null);
        AssertCarrier(result, "itm_npc", "npc_inventory", "npc_owner", null);
        AssertCarrier(result, "itm_storage", "location_storage", "loc_test", "storage_test");
        AssertCarrier(result, "itm_vehicle", "vehicle_inventory", "vehicle_test", null);

        var raw = Assert.Single(result.ByCreationRef["new_item_npc_add"]);
        Assert.Null(raw.ItemId);
        Assert.Equal("npc_inventory", raw.Carrier.Kind);
        Assert.Equal("npc_existing", raw.Carrier.OwnerId);
        Assert.Equal(
            "game_state/npcs/npc_inventory.json.NPCInventoryAdds[0].item",
            raw.JsonPath);
    }

    [Fact]
    public void Build_UsesContentsPathAsExactNestedContainerCoordinate()
    {
        var parent = MortalItemTestFixture.CreateCanonicalRoot("itm_satchel");
        parent["isContainer"] = true;
        parent["capacity"] = 20;
        var child = MortalItemTestFixture.CreateCanonicalRoot("itm_note");
        child["contentsPath"] = new JsonArray("itm_satchel");

        var result = MortalItemCarrierCatalog.Build(InputWithPlayerItems(parent, child));

        var occurrence = Assert.Single(result.ByItemId["itm_note"]);
        Assert.Equal(new[] { "itm_satchel" }, occurrence.Carrier.ContainerPath);
        Assert.Equal(
            "game_state/inventory/items.json.items[1]",
            occurrence.JsonPath);
    }

    [Fact]
    public void Build_ReportsDuplicatePermanentAndReceiptIdentityAcrossCarriers()
    {
        var item = MortalItemTestFixture.CreateCanonicalRoot("itm_duplicate");
        var result = MortalItemCarrierCatalog.Build(new MortalItemCarrierCatalogInput(
            PlayerInventory(item),
            NpcCore("npc_duplicate", item),
            null,
            null,
            null,
            EmptyCompanions()));

        Assert.Equal(2, result.ByItemId["itm_duplicate"].Count);
        Assert.Equal(2, Assert.Single(result.ByReceiptId).Value.Count);
        Assert.Contains(result.Issues, issue =>
            issue.Code == "mortal_item_materialization_duplicate_item_id");
        Assert.Contains(result.Issues, issue =>
            issue.Code == "mortal_item_materialization_duplicate_receipt_id");
    }

    [Fact]
    public void Build_DoesNotCollapseCaseDistinctIds()
    {
        var lower = MortalItemTestFixture.CreateCanonicalRoot("itm_case");
        var upper = MortalItemTestFixture.CreateCanonicalRoot("ITM_CASE");

        var result = MortalItemCarrierCatalog.Build(InputWithPlayerItems(lower, upper));

        Assert.Equal(2, result.ByItemId.Count);
        Assert.True(result.ByItemId.ContainsKey("itm_case"));
        Assert.True(result.ByItemId.ContainsKey("ITM_CASE"));
        Assert.Contains(result.Issues, issue =>
            issue.Code == "mortal_item_materialization_identity_ambiguity" &&
            issue.IdentityKind == "itemId");
    }

    [Fact]
    public void Build_DoesNotTrimWhitespaceDistinctIds()
    {
        var exact = MortalItemTestFixture.CreateCanonicalRoot("itm_space");
        var padded = MortalItemTestFixture.CreateCanonicalRoot(" itm_space ");

        var result = MortalItemCarrierCatalog.Build(InputWithPlayerItems(exact, padded));

        Assert.Equal(2, result.ByItemId.Count);
        Assert.True(result.ByItemId.ContainsKey("itm_space"));
        Assert.True(result.ByItemId.ContainsKey(" itm_space "));
        Assert.Contains(result.Issues, issue =>
            issue.Code == "mortal_item_materialization_identity_ambiguity" &&
            issue.IdentityKind == "itemId");
    }

    [Fact]
    public void Build_DoesNotNormalizeUnicodeDistinctIds()
    {
        const string composed = "itm_caf\u00e9";
        const string decomposed = "itm_cafe\u0301";
        var first = MortalItemTestFixture.CreateCanonicalRoot(composed);
        var second = MortalItemTestFixture.CreateCanonicalRoot(decomposed);

        var result = MortalItemCarrierCatalog.Build(InputWithPlayerItems(first, second));

        Assert.Equal(2, result.ByItemId.Count);
        Assert.True(result.ByItemId.ContainsKey(composed));
        Assert.True(result.ByItemId.ContainsKey(decomposed));
        Assert.Contains(result.Issues, issue =>
            issue.Code == "mortal_item_materialization_identity_ambiguity" &&
            issue.IdentityKind == "itemId");
    }

    [Fact]
    public void Build_IndexesRawCreationRefWithoutInventingPermanentIdentity()
    {
        var raw = MortalItemTestFixture.CreateRawRoot(
            creationRef: "new_item_raw_catalog",
            materializationId: "mat_item_raw_catalog");

        var result = MortalItemCarrierCatalog.Build(new MortalItemCarrierCatalogInput(
            new JsonObject { ["UpdateInventory"] = Items(raw) },
            null,
            null,
            null,
            null,
            EmptyCompanions()));

        Assert.Empty(result.ByItemId);
        Assert.Empty(result.ByReceiptId);
        var occurrence = Assert.Single(result.ByCreationRef["new_item_raw_catalog"]);
        Assert.Null(occurrence.ItemId);
        Assert.Equal("mat_item_raw_catalog", occurrence.MaterializationId);
        Assert.Equal("player_inventory", occurrence.Carrier.Kind);
    }

    [Fact]
    public void Build_IndexesCompanionReferencesSeparatelyAndNeverTreatsThemAsCarrierItems()
    {
        var canonical = MortalItemTestFixture.CreateCanonicalRoot("itm_book");
        var companionRoot = new JsonObject
        {
            ["entries"] = new JsonArray
            {
                new JsonObject
                {
                    ["itemId"] = "itm_book",
                    ["creationRef"] = "new_item_book",
                    ["journalEntries"] = new JsonArray
                    {
                        new JsonObject
                        {
                            ["entryId"] = "entry_1",
                            ["text"] = "Не предмет, а запись о предмете."
                        }
                    }
                },
                new JsonObject
                {
                    ["itemId"] = "itm_companion_only",
                    ["materializationReceipt"] = new JsonObject
                    {
                        ["receiptId"] = "must_not_be_catalogued_as_item"
                    }
                }
            }
        };
        var companions = new Dictionary<string, JsonObject>(StringComparer.Ordinal)
        {
            ["game_state/npcs/item_journals.json"] = companionRoot
        };

        var result = MortalItemCarrierCatalog.Build(new MortalItemCarrierCatalogInput(
            PlayerInventory(canonical),
            null,
            null,
            null,
            null,
            companions));

        Assert.Single(result.Occurrences);
        Assert.False(result.ByItemId.ContainsKey("itm_companion_only"));
        Assert.Single(result.ByCompanionReference["itm_book"]);
        Assert.Single(result.ByCompanionReference["itm_companion_only"]);
        Assert.True(result.Metrics.Companions > 0);
    }

    [Fact]
    public void Build_DuplicateIndependentMaterializationAndCreationRefAreIndexedAndRejected()
    {
        var first = MortalItemTestFixture.CreateCanonicalRoot("itm_root_a");
        var second = MortalItemTestFixture.CreateCanonicalRoot("itm_root_b");
        second["materialization"]!["materializationId"] =
            first["materialization"]!["materializationId"]!.GetValue<string>();
        second["materialization"]!["creationRef"] =
            first["materialization"]!["creationRef"]!.GetValue<string>();
        second["materializationReceipt"]!["materializationId"] =
            first["materializationReceipt"]!["materializationId"]!.GetValue<string>();
        second["materializationReceipt"]!["creationRef"] =
            first["materializationReceipt"]!["creationRef"]!.GetValue<string>();

        var result = MortalItemCarrierCatalog.Build(InputWithPlayerItems(first, second));

        var materializationId = first["materialization"]!["materializationId"]!.GetValue<string>();
        var creationRef = first["materialization"]!["creationRef"]!.GetValue<string>();
        Assert.Equal(2, result.ByMaterializationId[materializationId].Count);
        Assert.Equal(2, result.ByCreationRef[creationRef].Count);
        Assert.Contains(result.Issues, issue =>
            issue.Code == "mortal_item_materialization_duplicate_materialization_id");
        Assert.Contains(result.Issues, issue =>
            issue.Code == "mortal_item_materialization_duplicate_creation_ref");
    }

    [Fact]
    public void Build_AllowsSplitChildToShareRootOriginIndexes()
    {
        var parent = MortalItemTestFixture.CreateCanonicalRoot("itm_split_parent");
        var child = MortalItemTestFixture.CreateCanonicalRoot("itm_split_child");
        var parentMaterializationId =
            parent["materialization"]!["materializationId"]!.GetValue<string>();
        var parentCreationRef =
            parent["materialization"]!["creationRef"]!.GetValue<string>();
        child["materialization"]!["materializationId"] = parentMaterializationId;
        child["materialization"]!["creationRef"] = parentCreationRef;
        child["materializationReceipt"]!["materializationId"] = parentMaterializationId;
        child["materializationReceipt"]!["creationRef"] = parentCreationRef;
        child["materializationReceipt"]!["instanceKind"] = "split_derived";
        child["materializationReceipt"]!["parentItemIds"] = new JsonArray("itm_split_parent");

        var result = MortalItemCarrierCatalog.Build(InputWithPlayerItems(parent, child));

        Assert.Equal(2, result.ByMaterializationId[parentMaterializationId].Count);
        Assert.Equal(2, result.ByCreationRef[parentCreationRef].Count);
        Assert.DoesNotContain(result.Issues, issue =>
            issue.Code is "mortal_item_materialization_duplicate_materialization_id" or
                "mortal_item_materialization_duplicate_creation_ref");
    }

    [Fact]
    public void Build_AllowsCarrierEntityShellsThatContainNoItems()
    {
        var result = MortalItemCarrierCatalog.Build(new MortalItemCarrierCatalogInput(
            new JsonObject { ["items"] = new JsonArray() },
            new JsonObject
            {
                ["NPCsInScene"] = new JsonArray(new JsonObject { ["name"] = "Без инвентаря" })
            },
            new JsonObject
            {
                ["NPCInventoryAdds"] = new JsonArray(new JsonObject { ["NPCName"] = "Без предмета" })
            },
            new JsonObject
            {
                ["locationStorages"] = new JsonArray(new JsonObject { ["name"] = "Пустое хранилище" })
            },
            new JsonObject
            {
                ["vehicles"] = new JsonArray(new JsonObject { ["name"] = "Транспорт без инвентаря" })
            },
            EmptyCompanions()));

        Assert.Empty(result.Occurrences);
        Assert.Empty(result.Issues);
    }

    [Fact]
    public void Build_DoublingPopulationStaysWithinTwoPointFiveTimesWork()
    {
        var one = MortalItemCarrierCatalog.Build(RepresentativeInput(200));
        var two = MortalItemCarrierCatalog.Build(RepresentativeInput(400));

        Assert.True(one.Metrics.TotalVisited > 0);
        Assert.True(
            two.Metrics.TotalVisited <= one.Metrics.TotalVisited * 2.5,
            $"Expected linear catalog work, but {one.Metrics.TotalVisited} visits became {two.Metrics.TotalVisited}.");
    }

    [Fact]
    public void Build_ClonesIndexedItemEvidence()
    {
        var item = MortalItemTestFixture.CreateCanonicalRoot("itm_clone");
        var input = InputWithPlayerItems(item);

        var result = MortalItemCarrierCatalog.Build(input);
        input.PlayerInventory!["items"]![0]!["name"] = "Изменено после построения";

        var occurrence = Assert.Single(result.ByItemId["itm_clone"]);
        Assert.Equal("Тестовый предмет", occurrence.Item["name"]!.GetValue<string>());
    }

    private static void AssertCarrier(
        MortalItemCarrierCatalog result,
        string itemId,
        string kind,
        string ownerId,
        string? containerId)
    {
        var occurrence = Assert.Single(result.ByItemId[itemId]);
        Assert.Equal(kind, occurrence.Carrier.Kind);
        Assert.Equal(ownerId, occurrence.Carrier.OwnerId);
        Assert.Equal(containerId, occurrence.Carrier.ContainerId);
        Assert.Empty(occurrence.Carrier.ContainerPath);
    }

    private static MortalItemCarrierCatalogInput InputWithPlayerItems(params JsonObject[] items) =>
        new(
            PlayerInventory(items),
            null,
            null,
            null,
            null,
            EmptyCompanions());

    private static MortalItemCarrierCatalogInput RepresentativeInput(int itemCount) =>
        InputWithPlayerItems(Enumerable.Range(0, itemCount)
            .Select(index => MortalItemTestFixture.CreateCanonicalRoot($"itm_{index:D5}"))
            .ToArray());

    private static JsonObject PlayerInventory(params JsonObject[] items) =>
        new() { ["items"] = Items(items) };

    private static JsonObject NpcCore(string npcId, params JsonObject[] items) =>
        new()
        {
            ["NPCsInScene"] = new JsonArray
            {
                new JsonObject
                {
                    ["NPCId"] = npcId,
                    ["inventory"] = Items(items)
                }
            }
        };

    private static JsonObject NpcAdds(string npcId, params JsonObject[] items) =>
        new()
        {
            ["NPCInventoryAdds"] = new JsonArray(items.Select(item => (JsonNode?)new JsonObject
            {
                ["NPCId"] = npcId,
                ["item"] = item.DeepClone()
            }).ToArray())
        };

    private static JsonObject CurrentLocation(
        string locationId,
        string storageId,
        params JsonObject[] items) =>
        new()
        {
            ["locationId"] = locationId,
            ["locationStorages"] = new JsonArray
            {
                new JsonObject
                {
                    ["storageId"] = storageId,
                    ["contents"] = Items(items)
                }
            }
        };

    private static JsonObject Vehicles(string vehicleId, params JsonObject[] items) =>
        new()
        {
            ["vehicles"] = new JsonArray
            {
                new JsonObject
                {
                    ["vehicleId"] = vehicleId,
                    ["inventory"] = Items(items)
                }
            }
        };

    private static JsonArray Items(params JsonObject[] items) =>
        new(items.Select(item => (JsonNode?)item.DeepClone()).ToArray());

    private static IReadOnlyDictionary<string, JsonObject> EmptyCompanions() =>
        new Dictionary<string, JsonObject>(StringComparer.Ordinal);
}
