using System.Security.Cryptography;
using System.Text;
using System.Text.Encodings.Web;
using System.Text.Json;
using System.Text.Json.Nodes;

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
            ["materialization"] = materialization.DeepClone()
        };
        var bytes = Encoding.UTF8.GetBytes(input.ToJsonString(CompactJson));
        return "sha256:" + Convert.ToHexString(SHA256.HashData(bytes)).ToLowerInvariant();
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
}
