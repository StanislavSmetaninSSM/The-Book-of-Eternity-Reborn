using System.Security.Cryptography;
using System.Text;
using System.Text.Encodings.Web;
using System.Text.Json;
using System.Text.Json.Nodes;
using BookOfEternityClient.Services;

namespace BookOfEternityClient.Tests;

internal static class MortalItemTestFixture
{
    internal const string ItemId = "itm_test";
    internal const string CreationRef = "new_item_test";
    internal const string MaterializationId = "mat_item_test";
    internal const string ReceiptId = "mirec_test";

    private static readonly JsonSerializerOptions CompactJson = new()
    {
        WriteIndented = false,
        Encoder = JavaScriptEncoder.UnsafeRelaxedJsonEscaping
    };

    internal static JsonObject CreateRawRoot(
        string route = "player_acquisition",
        string authorityKind = "turn_outcome",
        string authorityId = "turn_42",
        int sourceTurn = 42,
        string creationRef = CreationRef,
        string materializationId = MaterializationId) =>
        new()
        {
            ["existedId"] = null,
            ["creationRef"] = creationRef,
            ["name"] = "Тестовый предмет",
            ["description"] = "Полный нейтральный предмет для проверки materialization-контракта.",
            ["image_prompt"] = "small neutral fantasy test object on dark cloth, realistic lighting",
            ["type"] = "Test item",
            ["group"] = "Test fixtures",
            ["quality"] = "Common",
            ["rarity"] = "Common",
            ["price"] = 10,
            ["count"] = 1,
            ["weight"] = 0.1,
            ["volume"] = 0.05,
            ["durability"] = "100%",
            ["maxDurability"] = "100%",
            ["bonuses"] = new JsonArray(),
            ["effects"] = new JsonArray(),
            ["structuredBonuses"] = new JsonArray(),
            ["combatEffect"] = new JsonArray(),
            ["customProperties"] = new JsonArray(),
            ["mechanicalSummaryAuthority"] = null,
            ["mechanicalSummaryUnresolvedReason"] = null,
            ["equipmentSlot"] = null,
            ["accessoryForSlot"] = null,
            ["requiresTwoHands"] = false,
            ["isContainer"] = false,
            ["capacity"] = null,
            ["containerWeight"] = null,
            ["weightReduction"] = null,
            ["contentsPath"] = null,
            ["isConsumption"] = false,
            ["textContent"] = null,
            ["journalEntries"] = new JsonArray(),
            ["isSentient"] = false,
            ["unreadableReason"] = null,
            ["sealedReason"] = null,
            ["lockedReason"] = null,
            ["disassembleTo"] = null,
            ["ownerBondLevelCurrent"] = null,
            ["ownerBondLevelMax"] = null,
            ["fateCards"] = new JsonArray(),
            ["questLinks"] = new JsonArray(),
            ["materialization"] = new JsonObject
            {
                ["schemaVersion"] = 1,
                ["materializationId"] = materializationId,
                ["realm"] = "Mortal",
                ["route"] = route,
                ["sourceTurn"] = sourceTurn,
                ["sourceAuthority"] = new JsonObject
                {
                    ["kind"] = authorityKind,
                    ["authorityId"] = authorityId
                },
                ["creationRef"] = creationRef,
                ["state"] = "complete",
                ["sections"] = new JsonObject
                {
                    ["presentation"] = Populated(),
                    ["physical"] = Populated(),
                    ["mechanics"] = Empty("У тестового предмета нет самостоятельной механики."),
                    ["equipment"] = Empty("Тестовый предмет не экипируется."),
                    ["container"] = Empty("Тестовый предмет ничего не вмещает."),
                    ["consumption"] = Empty("Тестовый предмет не расходуется при использовании."),
                    ["readableOrSentient"] = Empty("Тестовый предмет не читается и не обладает голосом."),
                    ["craftingAndDisassembly"] = Empty("Тестовый предмет не задаёт рецепт или разборку."),
                    ["bondsAndFateCards"] = Empty("Тестовый предмет не образует связь и не имеет Карт Судьбы."),
                    ["questRole"] = Empty("Тестовый предмет не связан с заданием."),
                    ["provenance"] = Populated(),
                    ["ownershipAndPlacement"] = Populated()
                }
            }
        };

    internal static JsonObject CreateCanonicalRoot(string itemId = ItemId)
    {
        var suffix = IdentitySuffix(itemId);
        var creationRef = string.Equals(itemId, ItemId, StringComparison.Ordinal)
            ? CreationRef
            : $"new_item_{suffix}";
        var materializationId = string.Equals(itemId, ItemId, StringComparison.Ordinal)
            ? MaterializationId
            : $"mat_item_{suffix}";
        var receiptId = string.Equals(itemId, ItemId, StringComparison.Ordinal)
            ? ReceiptId
            : $"mirec_{suffix}";
        var item = CreateRawRoot(
            creationRef: creationRef,
            materializationId: materializationId);
        item["itemId"] = itemId;
        item["existedId"] = itemId;
        item.Remove("creationRef");

        var receipt = new JsonObject
        {
            ["schemaVersion"] = 1,
            ["receiptId"] = receiptId,
            ["itemId"] = itemId,
            ["materializationId"] = materializationId,
            ["acceptedAtTurn"] = 42,
            ["creationRef"] = creationRef,
            ["instanceKind"] = "root",
            ["parentItemIds"] = new JsonArray()
        };
        receipt["seal"] = ComputeSeal(item["materialization"]!.AsObject(), receipt);
        item["materializationReceipt"] = receipt;
        return item;
    }

    internal static JsonObject CreateCanonicalRootAtTurn(
        string itemId,
        int acceptedAtTurn,
        string route,
        string authorityKind,
        string authorityId,
        string? name = null,
        int? price = null,
        int? baseSellPrice = null)
    {
        var suffix = IdentitySuffix(itemId);
        var creationRef = $"new_item_{suffix}";
        var materializationId = $"mat_item_{suffix}";
        var item = CreateRawRoot(
            route,
            authorityKind,
            authorityId,
            acceptedAtTurn,
            creationRef,
            materializationId);
        if (!string.IsNullOrWhiteSpace(name))
        {
            item["name"] = name;
            item["description"] = $"Тестовый canonical предмет «{name}».";
        }
        if (price.HasValue)
            item["price"] = price.Value;
        if (baseSellPrice.HasValue)
            item["baseSellPrice"] = baseSellPrice.Value;

        var receipt = MortalItemIdentityState.CreateRootReceipt(
            item,
            itemId,
            acceptedAtTurn);
        item["itemId"] = itemId;
        item["existedId"] = itemId;
        item.Remove("creationRef");
        item["materializationReceipt"] = receipt;
        return item;
    }

    internal static JsonObject CreateCanonicalTradeStock(
        JsonObject slot,
        string npcId,
        int acceptedAtTurn)
    {
        var itemData = slot["itemData"]?.AsObject() ??
                       throw new InvalidOperationException("Trade slot requires itemData.");
        var itemId = slot["itemId"]?.GetValue<string>() ??
                     itemData["itemId"]?.GetValue<string>() ??
                     throw new InvalidOperationException("Trade slot requires itemId.");
        var item = CreateCanonicalRootAtTurn(
            itemId,
            acceptedAtTurn,
            route: "new_npc_inventory",
            authorityKind: "new_npc",
            authorityId: npcId,
            name: itemData["name"]?.GetValue<string>(),
            price: ReadInt(itemData["price"], 10),
            baseSellPrice: ReadInt(itemData["baseSellPrice"], 0));

        foreach (var property in new[]
                 {
                     "description", "type", "tradeItemClass", "quality", "rarity", "group"
                 })
        {
            if (itemData[property] != null)
                item[property] = itemData[property]!.DeepClone();
        }
        var quality = itemData["quality"]?.GetValue<string>() ??
                      itemData["rarity"]?.GetValue<string>() ??
                      "Common";
        item["quality"] = quality;
        item["rarity"] = itemData["rarity"]?.GetValue<string>() ?? quality;
        item["weight"] = ReadDouble(itemData["weight"], 0.1);
        item["volume"] = ReadDouble(itemData["volume"], 0.05);
        return item;
    }

    internal static JsonObject CreateIndex(params JsonObject[] canonicalItems)
    {
        var entries = new JsonArray();
        foreach (var item in canonicalItems)
        {
            entries.Add(CreateIndexEntry(
                item,
                "player_inventory",
                "player"));
        }

        return new JsonObject
        {
            ["schemaVersion"] = 1,
            ["entries"] = entries
        };
    }

    internal static JsonObject CreateIndexForCarrier(
        JsonObject canonicalItem,
        string kind,
        string ownerId,
        string? containerId = null,
        JsonArray? containerPath = null) =>
        new()
        {
            ["schemaVersion"] = 1,
            ["entries"] = new JsonArray(CreateIndexEntry(
                canonicalItem,
                kind,
                ownerId,
                containerId,
                containerPath))
        };

    internal static JsonObject CreateIndexForCarriers(
        params (JsonObject Item, string Kind, string OwnerId, string? ContainerId)[] carriers) =>
        new()
        {
            ["schemaVersion"] = 1,
            ["entries"] = new JsonArray(
                carriers
                    .Select(carrier => (JsonNode?)CreateIndexEntry(
                        carrier.Item,
                        carrier.Kind,
                        carrier.OwnerId,
                        carrier.ContainerId))
                    .ToArray())
        };

    internal static JsonObject CreateCarrier(
        JsonObject item,
        string kind,
        string ownerId,
        string? containerId = null) =>
        kind switch
        {
            "player_inventory" => new JsonObject
            {
                ["items"] = new JsonArray(item.DeepClone()),
                ["equippedItems"] = new JsonObject()
            },
            "npc_inventory" => new JsonObject
            {
                ["NPCsInScene"] = new JsonArray
                {
                    new JsonObject
                    {
                        ["NPCId"] = ownerId,
                        ["inventory"] = new JsonArray(item.DeepClone()),
                        ["equippedItems"] = new JsonObject()
                    }
                }
            },
            "location_storage" => new JsonObject
            {
                ["currentLocationData"] = new JsonObject
                {
                    ["locationId"] = ownerId,
                    ["locationStorages"] = new JsonArray
                    {
                        new JsonObject
                        {
                            ["storageId"] = containerId ?? throw new ArgumentNullException(nameof(containerId)),
                            ["contents"] = new JsonArray(item.DeepClone())
                        }
                    }
                }
            },
            "vehicle_inventory" => new JsonObject
            {
                ["vehicles"] = new JsonArray
                {
                    new JsonObject
                    {
                        ["vehicleId"] = ownerId,
                        ["inventory"] = new JsonArray(item.DeepClone())
                    }
                }
            },
            _ => throw new ArgumentOutOfRangeException(nameof(kind), kind, "Unsupported Mortal item carrier kind.")
        };

    internal static JsonObject CreateReceiptlessNegative() =>
        new()
        {
            ["itemId"] = ItemId,
            ["existedId"] = ItemId,
            ["name"] = "Намеренно receipt-less предмет"
        };

    internal static JsonObject CreateFragmentOnly(string name = "Фрагмент") =>
        new()
        {
            ["fixtureKind"] = "fragment_only",
            ["fragment"] = new JsonObject
            {
                ["name"] = name
            }
        };

    private static JsonObject CreateIndexEntry(
        JsonObject canonicalItem,
        string kind,
        string ownerId,
        string? containerId = null,
        JsonArray? containerPath = null)
    {
        var itemId = canonicalItem["itemId"]!.GetValue<string>();
        var receipt = canonicalItem["materializationReceipt"]!.AsObject();
        var envelope = canonicalItem["materialization"]!.AsObject();
        var count = canonicalItem["count"]!.GetValue<int>();
        var carrier = new JsonObject
        {
            ["kind"] = kind,
            ["ownerId"] = ownerId,
            ["containerId"] = containerId,
            ["containerPath"] = containerPath?.DeepClone() ?? new JsonArray()
        };
        var authority = envelope["sourceAuthority"]!.AsObject();

        return new JsonObject
        {
            ["itemId"] = itemId,
            ["receiptId"] = receipt["receiptId"]!.GetValue<string>(),
            ["state"] = "active",
            ["currentCarrier"] = carrier.DeepClone(),
            ["originMaterializationIds"] = new JsonArray(
                envelope["materializationId"]!.GetValue<string>()),
            ["originCreationRefs"] = new JsonArray(
                receipt["creationRef"]!.GetValue<string>()),
            ["parentItemIds"] = new JsonArray(),
            ["mergedIntoItemId"] = null,
            ["transitions"] = new JsonArray
            {
                new JsonObject
                {
                    ["transitionId"] = $"mitrn_{IdentitySuffix(itemId)}_create",
                    ["kind"] = "create",
                    ["turn"] = receipt["acceptedAtTurn"]!.GetValue<int>(),
                    ["sourceItemIds"] = new JsonArray(),
                    ["sourceCarrier"] = null,
                    ["destinationCarrier"] = carrier,
                    ["quantityBefore"] = 0,
                    ["quantityAfter"] = count,
                    ["authorityKind"] = authority["kind"]!.GetValue<string>(),
                    ["authorityId"] = authority["authorityId"]!.GetValue<string>()
                }
            }
        };
    }

    private static JsonObject Populated() =>
        new()
        {
            ["state"] = "populated",
            ["reason"] = null
        };

    private static JsonObject Empty(string reason) =>
        new()
        {
            ["state"] = "empty_by_design",
            ["reason"] = reason
        };

    private static string ComputeSeal(JsonObject materialization, JsonObject receiptWithoutSeal)
    {
        var input = new JsonObject
        {
            ["schemaVersion"] = receiptWithoutSeal["schemaVersion"]!.DeepClone(),
            ["receiptId"] = receiptWithoutSeal["receiptId"]!.DeepClone(),
            ["itemId"] = receiptWithoutSeal["itemId"]!.DeepClone(),
            ["materializationId"] = receiptWithoutSeal["materializationId"]!.DeepClone(),
            ["acceptedAtTurn"] = receiptWithoutSeal["acceptedAtTurn"]!.DeepClone(),
            ["creationRef"] = receiptWithoutSeal["creationRef"]!.DeepClone(),
            ["instanceKind"] = receiptWithoutSeal["instanceKind"]!.DeepClone(),
            ["parentItemIds"] = receiptWithoutSeal["parentItemIds"]!.DeepClone(),
            ["materialization"] = Canonicalize(materialization)
        };
        var bytes = Encoding.UTF8.GetBytes(input.ToJsonString(CompactJson));
        return "sha256:" + Convert.ToHexString(SHA256.HashData(bytes)).ToLowerInvariant();
    }

    private static JsonNode Canonicalize(JsonNode value) =>
        value switch
        {
            JsonObject obj => CanonicalizeObject(obj),
            JsonArray array => new JsonArray(
                array.Select(element => element == null ? null : Canonicalize(element)).ToArray()),
            _ => value.DeepClone()
        };

    private static JsonObject CanonicalizeObject(JsonObject value)
    {
        var result = new JsonObject();
        foreach (var pair in value.OrderBy(pair => pair.Key, StringComparer.Ordinal))
            result[pair.Key] = pair.Value == null ? null : Canonicalize(pair.Value);
        return result;
    }

    private static string IdentitySuffix(string itemId)
    {
        var suffix = itemId.StartsWith("itm_", StringComparison.Ordinal)
            ? itemId[4..]
            : itemId;
        return string.Concat(suffix.Select(character =>
            char.IsLetterOrDigit(character) || character is '_' or '-'
                ? character
                : '_'));
    }

    private static int ReadInt(JsonNode? node, int fallback)
    {
        if (node is not JsonValue value)
            return fallback;
        if (value.TryGetValue<int>(out var number))
            return number;
        return value.TryGetValue<string>(out var text) && int.TryParse(text, out number)
            ? number
            : fallback;
    }

    private static double ReadDouble(JsonNode? node, double fallback)
    {
        if (node is not JsonValue value)
            return fallback;
        if (value.TryGetValue<double>(out var number))
            return number;
        return value.TryGetValue<string>(out var text) &&
               double.TryParse(text, System.Globalization.NumberStyles.Float, System.Globalization.CultureInfo.InvariantCulture, out number)
            ? number
            : fallback;
    }
}
