using System.Security.Cryptography;
using System.Text;
using System.Text.Encodings.Web;
using System.Text.Json;
using System.Text.Json.Nodes;

namespace BookOfEternityClient.Tests;

public static class MortalLocationTestFixture
{
    public const string LocationInitialId = "locref_test_black_ford";
    public const string LocationId = "loc_test_black_ford";
    public const string LocationMaterializationId = "mlocmat_test_black_ford";
    public const string LocationReceiptId = "mlocrec_test_black_ford";
    public const string LinkInitialId = "linkref_test_ford_to_tower";
    public const string LinkId = "lnk_test_ford_to_tower";

    public const string LinkMaterializationId = "mlinkmat_test_ford_to_tower";
    public const string LinkReceiptId = "mlinkrec_test_ford_to_tower";

    private const int SourceTurn = 42;
    private const string SourceAuthorityKind = "turn_outcome";
    private const string SourceAuthorityId = "turn_42";

    private static readonly JsonSerializerOptions CompactJson = new()
    {
        WriteIndented = false,
        Encoder = JavaScriptEncoder.UnsafeRelaxedJsonEscaping
    };

    public static JsonObject CreateRawLocation(string route = "world_map_creation") =>
        new()
        {
            ["locationId"] = null,
            ["initialId"] = LocationInitialId,
            ["realm"] = "mortal_world",
            ["name"] = "Чёрный брод",
            ["displayName"] = "Чёрный брод",
            ["purpose"] = "Безопасная детерминированная проверка материала локации",
            ["description"] = "Холодная река пересекает старый тракт между двумя каменистыми берегами.",
            ["image_prompt"] = "A grounded dark fantasy river ford at dusk, black stones, cold water, no text",
            ["locationType"] = "outdoor",
            ["biome"] = "riverlands",
            ["biomeDescription"] = "Каменистые берега и холодная проточная вода.",
            ["indoorType"] = null,
            ["features"] = new JsonArray(
                "чёрные камни",
                "разбитый тракт"),
            ["region"] = "Северная марка",
            ["parentLocationId"] = null,
            ["parentInitialId"] = null,
            ["coordinates"] = new JsonObject
            {
                ["x"] = 14,
                ["y"] = -3,
                ["z"] = 0
            },
            ["discovery"] = CreateDiscovery("visited"),
            ["internalDifficulty"] = CreateDifficulty(
                "low",
                1,
                "На переправе нет постоянной внутренней угрозы."),
            ["externalDifficulty"] = CreateDifficulty(
                "moderate",
                2,
                "За бродом тракт становится опаснее."),
            ["lastEventsDescription"] = "Первое полное тестовое состояние Чёрного брода.",
            ["eventDescriptions"] = new JsonArray(),
            ["factionControl"] = new JsonArray(),
            ["actorBindings"] = new JsonArray(),
            ["locationStorages"] = new JsonArray(),
            ["activeThreats"] = new JsonArray(),
            ["loreBindings"] = new JsonArray(),
            ["customStates"] = new JsonArray(),
            ["materialization"] = CreateLocationEnvelope(route)
        };

    public static JsonObject CreateCanonicalLocation(string discoveryTier = "visited")
    {
        var location = CreateRawLocation();
        location["locationId"] = LocationId;
        location.Remove("initialId");
        location.Remove("parentInitialId");
        location["discovery"] = CreateDiscovery(discoveryTier);

        var envelope = location["materialization"]!.AsObject();
        var receipt = new JsonObject
        {
            ["schemaVersion"] = 1,
            ["receiptId"] = LocationReceiptId,
            ["locationId"] = LocationId,
            ["initialId"] = LocationInitialId,
            ["materializationId"] = LocationMaterializationId,
            ["realm"] = "mortal_world",
            ["route"] = envelope["route"]!.DeepClone(),
            ["sourceTurn"] = SourceTurn,
            ["sourceAuthorityKind"] = SourceAuthorityKind,
            ["sourceAuthorityId"] = SourceAuthorityId
        };
        receipt["seal"] = ComputeSeal(envelope, receipt);
        location["materializationReceipt"] = receipt;
        return location;
    }

    public static JsonObject CreateRawLink(string sourceLocationId, string targetLocationId)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(sourceLocationId);
        ArgumentException.ThrowIfNullOrWhiteSpace(targetLocationId);

        return new JsonObject
        {
            ["linkId"] = null,
            ["initialId"] = LinkInitialId,
            ["sourceLocationId"] = sourceLocationId,
            ["sourceInitialId"] = null,
            ["targetLocationId"] = targetLocationId,
            ["targetInitialId"] = null,
            ["name"] = "Тропа к башне",
            ["description"] = "Узкая тропа проходит под каменистым обрывом.",
            ["directionLabel"] = "на северо-восток",
            ["linkType"] = "path",
            ["travelMode"] = "foot",
            ["access"] = new JsonObject
            {
                ["state"] = "open",
                ["reason"] = null,
                ["requirements"] = new JsonArray()
            },
            ["discovery"] = CreateDiscovery("discovered"),
            ["customStates"] = new JsonArray(),
            ["materialization"] = CreateLinkEnvelope()
        };
    }

    public static JsonObject CreateCanonicalLink(string sourceLocationId, string targetLocationId)
    {
        var link = CreateRawLink(sourceLocationId, targetLocationId);
        link["linkId"] = LinkId;
        link.Remove("initialId");
        link.Remove("sourceInitialId");
        link.Remove("targetInitialId");

        var envelope = link["materialization"]!.AsObject();
        var receipt = new JsonObject
        {
            ["schemaVersion"] = 1,
            ["receiptId"] = LinkReceiptId,
            ["linkId"] = LinkId,
            ["initialId"] = LinkInitialId,
            ["materializationId"] = LinkMaterializationId,
            ["realm"] = "mortal_world",
            ["route"] = "world_map_link_creation",
            ["sourceTurn"] = SourceTurn,
            ["sourceAuthorityKind"] = SourceAuthorityKind,
            ["sourceAuthorityId"] = SourceAuthorityId,
            ["sourceLocationId"] = sourceLocationId,
            ["targetLocationId"] = targetLocationId
        };
        receipt["seal"] = ComputeSeal(envelope, receipt);
        link["materializationReceipt"] = receipt;
        return link;
    }

    public static JsonObject CreateWorldMap(params JsonObject[] locations)
    {
        ArgumentNullException.ThrowIfNull(locations);
        return new JsonObject
        {
            ["schemaVersion"] = 1,
            ["realm"] = "mortal_world",
            ["locations"] = new JsonArray(
                locations.Select(static location => (JsonNode?)location.DeepClone()).ToArray()),
            ["links"] = new JsonArray()
        };
    }

    public static JsonObject CreateIdentityIndex(JsonObject location, JsonObject? link = null)
    {
        ArgumentNullException.ThrowIfNull(location);
        var locationReceipt = location["materializationReceipt"]?.AsObject()
            ?? throw new InvalidOperationException("Canonical location fixture requires a receipt.");
        var locationEnvelope = location["materialization"]?.AsObject()
            ?? throw new InvalidOperationException("Canonical location fixture requires an envelope.");

        var locationEntry = new JsonObject
        {
            ["locationId"] = location["locationId"]!.DeepClone(),
            ["initialId"] = locationReceipt["initialId"]!.DeepClone(),
            ["materializationId"] = locationEnvelope["materializationId"]!.DeepClone(),
            ["receiptId"] = locationReceipt["receiptId"]!.DeepClone(),
            ["realm"] = "mortal_world",
            ["route"] = locationEnvelope["route"]!.DeepClone(),
            ["sourceTurn"] = locationEnvelope["sourceTurn"]!.DeepClone(),
            ["sourceAuthorityKind"] = locationReceipt["sourceAuthorityKind"]!.DeepClone(),
            ["sourceAuthorityId"] = locationReceipt["sourceAuthorityId"]!.DeepClone(),
            ["coordinatesAtCreation"] = location["coordinates"]!.DeepClone(),
            ["state"] = "active",
            ["transitions"] = new JsonArray()
        };

        var linkEntries = new JsonArray();
        if (link != null)
        {
            var linkReceipt = link["materializationReceipt"]?.AsObject()
                ?? throw new InvalidOperationException("Canonical link fixture requires a receipt.");
            var linkEnvelope = link["materialization"]?.AsObject()
                ?? throw new InvalidOperationException("Canonical link fixture requires an envelope.");
            linkEntries.Add(new JsonObject
            {
                ["linkId"] = link["linkId"]!.DeepClone(),
                ["initialId"] = linkReceipt["initialId"]!.DeepClone(),
                ["materializationId"] = linkEnvelope["materializationId"]!.DeepClone(),
                ["receiptId"] = linkReceipt["receiptId"]!.DeepClone(),
                ["realm"] = "mortal_world",
                ["route"] = linkEnvelope["route"]!.DeepClone(),
                ["sourceTurn"] = linkEnvelope["sourceTurn"]!.DeepClone(),
                ["sourceAuthorityKind"] = linkReceipt["sourceAuthorityKind"]!.DeepClone(),
                ["sourceAuthorityId"] = linkReceipt["sourceAuthorityId"]!.DeepClone(),
                ["sourceLocationId"] = link["sourceLocationId"]!.DeepClone(),
                ["targetLocationId"] = link["targetLocationId"]!.DeepClone(),
                ["state"] = "active",
                ["transitions"] = new JsonArray()
            });
        }

        return new JsonObject
        {
            ["schemaVersion"] = 1,
            ["realm"] = "mortal_world",
            ["locationEntries"] = new JsonArray(locationEntry),
            ["linkEntries"] = linkEntries
        };
    }

    public static JsonObject CreateCurrentProjection(JsonObject canonicalLocation)
    {
        ArgumentNullException.ThrowIfNull(canonicalLocation);
        var projection = canonicalLocation.DeepClone().AsObject();
        projection["currentWeather"] = new JsonObject
        {
            ["summary"] = "Холодная морось",
            ["visibility"] = "normal"
        };
        projection["currentInteractions"] = new JsonArray();
        return projection;
    }

    public static JsonObject CreateReceiptlessNegative()
    {
        var invalid = CreateCanonicalLocation();
        invalid.Remove("materializationReceipt");
        invalid["name"] = "[INVALID FIXTURE: receiptless] Чёрный брод";
        return invalid;
    }

    private static JsonObject CreateLocationEnvelope(string route) =>
        new()
        {
            ["schemaVersion"] = 1,
            ["materializationId"] = LocationMaterializationId,
            ["entityKind"] = "mortal_location",
            ["realm"] = "mortal_world",
            ["route"] = route,
            ["sourceTurn"] = SourceTurn,
            ["sourceAuthority"] = new JsonObject
            {
                ["kind"] = SourceAuthorityKind,
                ["authorityId"] = SourceAuthorityId
            },
            ["initialId"] = LocationInitialId,
            ["state"] = "complete",
            ["sections"] = new JsonObject
            {
                ["presentation"] = Populated(),
                ["physical"] = Populated(),
                ["placement"] = Populated(),
                ["discovery"] = Populated(),
                ["difficulty"] = Populated(),
                ["chronicle"] = Populated(),
                ["factionControl"] = Empty("Ни одна фракция не удерживает это место."),
                ["actorBindings"] = Empty("Постоянных обитателей здесь нет."),
                ["storageMetadata"] = Empty("Оборудованных хранилищ здесь нет."),
                ["activeThreats"] = Empty("Постоянной активной угрозы здесь нет."),
                ["loreBindings"] = Empty("Сюжетные и справочные привязки пока не требуются."),
                ["customStates"] = Empty("Особые состояния места не требуются."),
                ["topology"] = Empty("Тестовая локация намеренно изолирована.")
            }
        };

    private static JsonObject CreateLinkEnvelope() =>
        new()
        {
            ["schemaVersion"] = 1,
            ["materializationId"] = LinkMaterializationId,
            ["entityKind"] = "mortal_location_link",
            ["realm"] = "mortal_world",
            ["route"] = "world_map_link_creation",
            ["sourceTurn"] = SourceTurn,
            ["sourceAuthority"] = new JsonObject
            {
                ["kind"] = SourceAuthorityKind,
                ["authorityId"] = SourceAuthorityId
            },
            ["initialId"] = LinkInitialId,
            ["state"] = "complete",
            ["sections"] = new JsonObject
            {
                ["endpoints"] = Populated(),
                ["presentation"] = Populated(),
                ["traversal"] = Populated(),
                ["access"] = Populated(),
                ["discovery"] = Populated(),
                ["customStates"] = Empty("Особые состояния пути не требуются.")
            }
        };

    private static JsonObject CreateDiscovery(string tier) =>
        tier switch
        {
            "hidden" => new JsonObject
            {
                ["tier"] = tier,
                ["audience"] = "gm_only",
                ["rumorSummary"] = null
            },
            "rumored" => new JsonObject
            {
                ["tier"] = tier,
                ["audience"] = "player_known",
                ["rumorSummary"] = "На старом тракте рассказывают о холодной переправе."
            },
            "discovered" or "visited" => new JsonObject
            {
                ["tier"] = tier,
                ["audience"] = "player_known",
                ["rumorSummary"] = null
            },
            _ => throw new ArgumentOutOfRangeException(nameof(tier), tier, "Unsupported discovery tier.")
        };

    private static JsonObject CreateDifficulty(string danger, int recommendedLevel, string description) =>
        new()
        {
            ["danger"] = danger,
            ["recommendedLevel"] = recommendedLevel,
            ["description"] = description
        };

    private static JsonObject Populated() =>
        new()
        {
            ["disposition"] = "populated",
            ["reason"] = null
        };

    private static JsonObject Empty(string reason) =>
        new()
        {
            ["disposition"] = "empty_by_design",
            ["reason"] = reason
        };

    private static string ComputeSeal(JsonObject materialization, JsonObject receiptWithoutSeal)
    {
        var input = new JsonObject();
        foreach (var property in receiptWithoutSeal.OrderBy(static pair => pair.Key, StringComparer.Ordinal))
            input[property.Key] = property.Value?.DeepClone();
        input["materialization"] = Canonicalize(materialization);

        var bytes = Encoding.UTF8.GetBytes(CanonicalizeObject(input).ToJsonString(CompactJson));
        return "sha256:" + Convert.ToHexString(SHA256.HashData(bytes)).ToLowerInvariant();
    }

    private static JsonNode Canonicalize(JsonNode value) =>
        value switch
        {
            JsonObject obj => CanonicalizeObject(obj),
            JsonArray array => new JsonArray(
                array.Select(static element => element == null ? null : Canonicalize(element)).ToArray()),
            _ => value.DeepClone()
        };

    private static JsonObject CanonicalizeObject(JsonObject value)
    {
        var result = new JsonObject();
        foreach (var property in value.OrderBy(static pair => pair.Key, StringComparer.Ordinal))
            result[property.Key] = property.Value == null ? null : Canonicalize(property.Value);
        return result;
    }
}
