using System.Text;
using System.Text.Json;
using System.Text.Json.Nodes;
using System.Text.Json.Serialization;
using BookOfEternityClient.Configuration;
using BookOfEternityClient.Core;

namespace BookOfEternityClient.Services;

internal static class ShiningTradeRequestState
{
    public const string PendingRequestsPath = "game_state/control/pending_shining_trade_inventory_requests.json";
    public const string RequestsProperty = "requests";
    public const string ReceiptsProperty = "tradeInventoryReceipts";
    public const string MerchantProfileShiningFaction = "shining_faction";
    public const string ReceiptStatusReady = "ready";

    private static readonly JsonSerializerOptions JsonOpts = SharedJsonOptions.PrettyCamelCaseUnsafeRelaxed;

    public sealed class PendingShiningTradeInventoryRequest
    {
        [JsonPropertyName("requestId")]
        public string RequestId { get; set; } = $"shining_trade_{Guid.NewGuid():N}";

        [JsonPropertyName("factionId")]
        public string FactionId { get; set; } = "";

        [JsonPropertyName("factionName")]
        public string FactionName { get; set; } = "";

        [JsonPropertyName("tradeCycleId")]
        public string TradeCycleId { get; set; } = "";

        [JsonPropertyName("derivedTradeTier")]
        public int DerivedTradeTier { get; set; }

        [JsonPropertyName("derivedTradeSlotCount")]
        public int DerivedTradeSlotCount { get; set; }

        [JsonPropertyName("derivedRarityCeiling")]
        public string DerivedRarityCeiling { get; set; } = "";

        [JsonPropertyName("derivedServiceMultiplier")]
        public double DerivedServiceMultiplier { get; set; }

        [JsonPropertyName("merchantProfile")]
        public string MerchantProfile { get; set; } = MerchantProfileShiningFaction;

        [JsonPropertyName("createdAtTurn")]
        public int CreatedAtTurn { get; set; }

        [JsonPropertyName("createdAtUtc")]
        public string CreatedAtUtc { get; set; } = DateTime.UtcNow.ToString("o");
    }

    public sealed class TradeInventoryReceiptEntry
    {
        [JsonPropertyName("requestId")]
        public string RequestId { get; set; } = "";

        [JsonPropertyName("factionId")]
        public string FactionId { get; set; } = "";

        [JsonPropertyName("factionName")]
        public string FactionName { get; set; } = "";

        [JsonPropertyName("tradeCycleId")]
        public string TradeCycleId { get; set; } = "";

        [JsonPropertyName("status")]
        public string Status { get; set; } = ReceiptStatusReady;

        [JsonPropertyName("itemCount")]
        public int ItemCount { get; set; }

        [JsonPropertyName("soldOutCount")]
        public int? SoldOutCount { get; set; }

        [JsonPropertyName("resolvedAtTurn")]
        public int ResolvedAtTurn { get; set; }

        [JsonPropertyName("resolvedAtUtc")]
        public string ResolvedAtUtc { get; set; } = "";
    }

    internal sealed record PendingRequestReadState(
        bool FilePresent,
        bool IsMalformed,
        IReadOnlyList<PendingShiningTradeInventoryRequest> Requests);

    public static async Task<IReadOnlyList<PendingShiningTradeInventoryRequest>> ReadRequestsAsync(FileSystemManager fs)
    {
        return (await ReadRequestsStateAsync(fs)).Requests;
    }

    public static IReadOnlyList<PendingShiningTradeInventoryRequest> ReadRequests(string? json)
    {
        return AnalyzeRequests(json, json != null).Requests;
    }

    internal static async Task<PendingRequestReadState> ReadRequestsStateAsync(FileSystemManager fs)
    {
        var json = await fs.ReadFileAsync(PendingRequestsPath);
        return AnalyzeRequests(json, fs.FileExists(PendingRequestsPath));
    }

    private static PendingRequestReadState AnalyzeRequests(string? json, bool filePresent)
    {
        if (json == null)
            return new PendingRequestReadState(filePresent, false, Array.Empty<PendingShiningTradeInventoryRequest>());
        if (string.IsNullOrWhiteSpace(json))
            return new PendingRequestReadState(filePresent, filePresent, Array.Empty<PendingShiningTradeInventoryRequest>());

        try
        {
            using var doc = JsonDocument.Parse(json);
            if (!doc.RootElement.TryGetProperty(RequestsProperty, out var requestsNode) ||
                requestsNode.ValueKind != JsonValueKind.Array)
            {
                return new PendingRequestReadState(filePresent, true, Array.Empty<PendingShiningTradeInventoryRequest>());
            }

            var requests = new List<PendingShiningTradeInventoryRequest>();
            foreach (var requestNode in requestsNode.EnumerateArray())
            {
                var request = JsonSerializer.Deserialize<PendingShiningTradeInventoryRequest>(requestNode.GetRawText(), JsonOpts);
                if (request == null)
                    return new PendingRequestReadState(filePresent, true, Array.Empty<PendingShiningTradeInventoryRequest>());

                requests.Add(request);
            }

            return new PendingRequestReadState(filePresent, false, requests);
        }
        catch
        {
            return new PendingRequestReadState(filePresent, true, Array.Empty<PendingShiningTradeInventoryRequest>());
        }
    }

    public static async Task WriteRequestAsync(FileSystemManager fs, PendingShiningTradeInventoryRequest request)
    {
        var existingState = await ReadRequestsStateAsync(fs);
        if (existingState.IsMalformed)
            throw new InvalidOperationException("pending_shining_trade_inventory_requests.json повреждён и должен быть исправлен или очищен до записи новых торговых запросов.");

        var requests = (await ReadRequestsAsync(fs)).ToList();
        requests.RemoveAll(existing =>
            string.Equals(existing.FactionId, request.FactionId, StringComparison.OrdinalIgnoreCase) &&
            string.Equals(existing.TradeCycleId, request.TradeCycleId, StringComparison.OrdinalIgnoreCase));
        requests.Add(request);

        await WriteRequestsAsync(fs, requests);
    }

    public static async Task WriteRequestsAsync(FileSystemManager fs, IReadOnlyList<PendingShiningTradeInventoryRequest> requests)
    {
        var existingState = await ReadRequestsStateAsync(fs);
        if (existingState.IsMalformed)
            throw new InvalidOperationException("pending_shining_trade_inventory_requests.json повреждён и должен быть исправлен или очищен до записи новых торговых запросов.");

        if (requests.Count == 0)
        {
            fs.DeleteFile(PendingRequestsPath);
            return;
        }

        await fs.WriteFileAtomicAsync(PendingRequestsPath, JsonSerializer.Serialize(new Dictionary<string, object?>
        {
            [RequestsProperty] = requests
        }, JsonOpts));
    }

    public static void ClearRequests(FileSystemManager fs) => fs.DeleteFile(PendingRequestsPath);

    public static JsonArray EnsureTradeInventoryReceiptsArray(JsonObject faction)
    {
        NormalizeTradeInventoryReceiptsShape(faction);
        return faction[ReceiptsProperty]!.AsArray();
    }

    public static void NormalizeTradeInventoryReceiptsShape(JsonObject faction)
    {
        if (faction[ReceiptsProperty] is not JsonArray receipts)
        {
            faction[ReceiptsProperty] = new JsonArray();
            receipts = faction[ReceiptsProperty]!.AsArray();
        }

        for (var i = receipts.Count - 1; i >= 0; i--)
        {
            if (receipts[i] is not JsonObject receipt)
            {
                receipts.RemoveAt(i);
                continue;
            }

            NormalizeReceiptObject(receipt);
        }
    }

    public static JsonObject? FindMatchingReceipt(JsonObject faction, PendingShiningTradeInventoryRequest request)
    {
        if (faction[ReceiptsProperty] is not JsonArray receipts)
            return null;

        JsonObject? match = null;
        foreach (var receipt in receipts.OfType<JsonObject>())
        {
            if (!ReceiptMatchesRequestContract(receipt, request, faction["tradeInventory"] as JsonObject))
                continue;

            if (match != null)
                return null;

            match = receipt;
        }

        return match;
    }

    public static bool HasReadyInventoryForCurrentContract(JsonObject faction, PendingShiningTradeInventoryRequest request)
    {
        var tradeInventory = faction["tradeInventory"] as JsonObject;
        if (!InventoryMatchesRequestContract(tradeInventory, request))
            return false;

        return FindMatchingReceipt(faction, request) != null;
    }

    public static JsonObject? FindLatestAuthoritativeReadyReceiptForCurrentCycle(JsonObject faction, string? tradeCycleId)
    {
        if (string.IsNullOrWhiteSpace(tradeCycleId) ||
            faction[ReceiptsProperty] is not JsonArray receipts ||
            faction["tradeInventory"] is not JsonObject tradeInventory)
        {
            return null;
        }

        var matchingReceipts = receipts.OfType<JsonObject>()
            .Where(receipt =>
                string.Equals(GetNodeString(receipt["tradeCycleId"]), tradeCycleId, StringComparison.OrdinalIgnoreCase) &&
                string.Equals(GetNodeString(receipt["status"]), ReceiptStatusReady, StringComparison.OrdinalIgnoreCase) &&
                GetNodeInt(receipt["itemCount"], -1) == GetTradeInventoryItemCount(tradeInventory) &&
                TryReadIntegerNode(receipt["soldOutCount"], out var soldOutCount) &&
                soldOutCount == GetTradeInventorySoldOutCount(tradeInventory) &&
                !string.IsNullOrWhiteSpace(GetNodeString(receipt["requestId"])) &&
                !string.IsNullOrWhiteSpace(GetNodeString(receipt["resolvedAtUtc"])) &&
                GetNodeInt(receipt["resolvedAtTurn"], 0) > 0)
            .OrderByDescending(receipt => GetNodeInt(receipt["resolvedAtTurn"], 0))
            .ThenByDescending(receipt => GetNodeString(receipt["resolvedAtUtc"]) ?? string.Empty, StringComparer.OrdinalIgnoreCase)
            .ToList();

        return matchingReceipts.Count == 1 ? matchingReceipts[0] : null;
    }

    public static int GetTradeInventoryItemCount(JsonObject? tradeInventory) =>
        tradeInventory?["items"] is JsonArray items
            ? items.OfType<JsonObject>().Count()
            : 0;

    public static int GetTradeInventorySoldOutCount(JsonObject? tradeInventory) =>
        tradeInventory?["items"] is JsonArray items
            ? items.OfType<JsonObject>().Count(item => TryReadBool(item["soldOut"], out var soldOut) && soldOut)
            : 0;

    public static bool InventoryMatchesRequestContract(JsonObject? tradeInventory, PendingShiningTradeInventoryRequest request)
    {
        if (tradeInventory == null)
            return false;

        if (!string.Equals(GetNodeString(tradeInventory["tradeCycleId"]), request.TradeCycleId, StringComparison.OrdinalIgnoreCase))
            return false;

        if (string.IsNullOrWhiteSpace(GetNodeString(tradeInventory["generatedAtUtc"])))
            return false;

        if (GetNodeInt(tradeInventory["generationTradeTier"], int.MinValue) != request.DerivedTradeTier)
            return false;

        if (!string.Equals(GetNodeString(tradeInventory["generationRarityCeiling"]), request.DerivedRarityCeiling, StringComparison.OrdinalIgnoreCase))
            return false;

        if (!string.Equals(GetNodeString(tradeInventory["merchantProfile"]) ?? MerchantProfileShiningFaction, request.MerchantProfile, StringComparison.OrdinalIgnoreCase))
            return false;

        if (!TryReadDouble(tradeInventory["serviceMultiplierSnapshot"], out var inventoryMultiplier) ||
            Math.Abs(inventoryMultiplier - request.DerivedServiceMultiplier) > 0.001)
        {
            return false;
        }

        if (tradeInventory["items"] is not JsonArray items || items.Count != request.DerivedTradeSlotCount)
            return false;

        var seenSlotIds = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        var seenRelicIds = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        foreach (var item in items.OfType<JsonObject>())
        {
            var slotId = GetNodeString(item["slotId"]);
            if (string.IsNullOrWhiteSpace(slotId) ||
                !seenSlotIds.Add(slotId) ||
                GetNodeInt(item["priceInFeathers"], -1) <= 0 ||
                !TryReadBool(item["soldOut"], out _) ||
                item["relicData"] is not JsonObject relicData)
            {
                return false;
            }

            var relicId = GetNodeString(relicData["relicId"]) ?? GetNodeString(relicData["id"]);
            if (string.IsNullOrWhiteSpace(relicId) || !seenRelicIds.Add(relicId))
                return false;

            var rarity = GetNodeString(relicData["quality"]) ?? GetNodeString(relicData["rarity"]) ?? string.Empty;
            if (!ShiningAbodeState.IsSoulRelicRarityAllowedForTradeCeiling(rarity, request.DerivedRarityCeiling))
                return false;
        }

        return true;
    }

    public static bool ReceiptMatchesRequestContract(
        JsonObject? receipt,
        PendingShiningTradeInventoryRequest request,
        JsonObject? tradeInventory)
    {
        if (receipt == null || tradeInventory == null)
            return false;

        if (!string.Equals(GetNodeString(receipt["requestId"]), request.RequestId, StringComparison.OrdinalIgnoreCase))
            return false;

        if (!string.Equals(GetNodeString(receipt["factionId"]), request.FactionId, StringComparison.OrdinalIgnoreCase))
            return false;

        if (!string.Equals(GetNodeString(receipt["tradeCycleId"]), request.TradeCycleId, StringComparison.OrdinalIgnoreCase))
            return false;

        if (!string.Equals(GetNodeString(receipt["status"]), ReceiptStatusReady, StringComparison.OrdinalIgnoreCase))
            return false;

        if (string.IsNullOrWhiteSpace(GetNodeString(receipt["resolvedAtUtc"])) || GetNodeInt(receipt["resolvedAtTurn"], 0) <= 0)
            return false;

        if (GetNodeInt(receipt["itemCount"], -1) != GetTradeInventoryItemCount(tradeInventory))
            return false;

        return TryReadIntegerNode(receipt["soldOutCount"], out var soldOutCount) &&
               soldOutCount == GetTradeInventorySoldOutCount(tradeInventory);
    }

    public static async Task EnsureHealthyAsync(FileSystemManager fs, string? currentRealm)
    {
        if (!fs.FileExists(PendingRequestsPath))
            return;

        if (!RealmSemantics.HasResolvedRealm(currentRealm))
            return;

        if (!IsShiningRealm(currentRealm))
        {
            var state = await ReadRequestsStateAsync(fs);
            if (!state.IsMalformed && state.Requests.Count == 0)
                fs.DeleteFile(PendingRequestsPath);
            return;
        }

        var requestState = await ReadRequestsStateAsync(fs);
        if (requestState.IsMalformed)
            return;

        var requests = requestState.Requests.ToList();
        if (requests.Count == 0)
        {
            fs.DeleteFile(PendingRequestsPath);
            return;
        }

        var shiningJson = await fs.ReadFileAsync(ShiningAbodeState.StatePath);
        var residentJson = await fs.ReadFileAsync(GuardianAbodeResidentState.StatePath);
        var guardiansJson = await fs.ReadFileAsync("game_state/meta/guardians.json");
        if (string.IsNullOrWhiteSpace(shiningJson))
            return;

        try
        {
            var shiningRoot = JsonNode.Parse(shiningJson) as JsonObject;
            var residentRoot = !string.IsNullOrWhiteSpace(residentJson) ? JsonNode.Parse(residentJson) as JsonObject : null;
            var guardiansRoot = !string.IsNullOrWhiteSpace(guardiansJson) ? JsonNode.Parse(guardiansJson) as JsonObject : null;
            if (shiningRoot == null)
                return;
            if (ShiningAbodeState.ValidateRawOwnerStateForActionableMode(shiningRoot) != null)
                return;

            ShiningAbodeState.NormalizeStateRoot(shiningRoot, residentRoot, guardiansRoot);
            if (!string.Equals(GetNodeString(shiningRoot["availability"]), ShiningAbodeState.AvailabilityActive, StringComparison.OrdinalIgnoreCase))
            {
                fs.DeleteFile(PendingRequestsPath);
                return;
            }
            if (ShiningAbodeState.GetPreparedIncarnationPackageMode(shiningRoot) != ShiningAbodeState.PreparedIncarnationPackageMode.Absent)
                return;

            requests.RemoveAll(request =>
            {
                var faction = ShiningAbodeState.FindFaction(shiningRoot, request.FactionId);
                if (faction == null)
                    return false;

                return FindMatchingReceipt(faction, request) != null;
            });

            if (requests.Count == 0)
            {
                fs.DeleteFile(PendingRequestsPath);
                return;
            }

            await fs.WriteFileAtomicAsync(PendingRequestsPath, JsonSerializer.Serialize(new Dictionary<string, object?>
            {
                [RequestsProperty] = requests
            }, JsonOpts));
        }
        catch
        {
            // keep pending requests until canonical state is readable again
        }
    }

    public static async Task<string?> BuildSystemReminderFragmentAsync(FileSystemManager fs, string? currentRealm)
    {
        if (!RealmSemantics.IsAfterlifeRealm(currentRealm))
            return null;

        var requestState = await ReadRequestsStateAsync(fs);
        if (requestState.IsMalformed)
        {
            return "SHINING TRADE REQUEST CORRUPTION:\n" +
                   "  - pending_shining_trade_inventory_requests.json unreadable or malformed.\n" +
                   "  - Preserve the file and surface validation/repair for the broken Shining trade contract.";
        }

        var requests = requestState.Requests;
        if (requests.Count == 0)
            return null;

        if (IsShiningRealm(currentRealm))
        {
            var shiningRoot = await ReadJsonObjectAsync(fs, ShiningAbodeState.StatePath);
            var packageMode = ShiningAbodeState.GetPreparedIncarnationPackageMode(shiningRoot);
            if (packageMode != ShiningAbodeState.PreparedIncarnationPackageMode.Absent)
            {
                var blocked = new StringBuilder();
                blocked.AppendLine("SHINING TRADE REQUESTS BLOCKED:");
                blocked.AppendLine(packageMode == ShiningAbodeState.PreparedIncarnationPackageMode.ValidHandoff
                    ? "  - Valid preparedIncarnationPackage puts the realm in pending-bootstrap handoff mode."
                    : "  - preparedIncarnationPackage is malformed or fails bootstrap validation, so the realm mode is fail-closed.");
                blocked.AppendLine("  - Preserve pending_shining_trade_inventory_requests.json; do not delete, truncate, or process ordinary Shining trade during this mode.");
                blocked.AppendLine($"  - Pending requests detected: {requests.Count}");
                foreach (var request in requests)
                    AppendSerializedJsonBlock(blocked, "Blocked pending trade DTO", request);
                return blocked.ToString();
            }
        }

        var sb = new StringBuilder();
        sb.AppendLine("SHINING TRADE REQUESTS:");
        sb.AppendLine("  - Treat these as client-authored explicit inventory contracts, not as permission to infer stock on the client.");
        sb.AppendLine("  - Resolve each request in accepted turn through faction.tradeInventory plus matching tradeInventoryReceipts[].");
        foreach (var request in requests)
        {
            sb.AppendLine($"  - {request.FactionName} ({request.FactionId}) -> cycle {request.TradeCycleId}, tier {request.DerivedTradeTier}, slots {request.DerivedTradeSlotCount}, ceiling {request.DerivedRarityCeiling}, service x{request.DerivedServiceMultiplier:0.00}, merchant {request.MerchantProfile}, request {request.RequestId}, created turn {request.CreatedAtTurn}, UTC {request.CreatedAtUtc}.");
            AppendSerializedJsonBlock(sb, "Full pending trade DTO", request);
        }

        return sb.ToString();
    }

    private static void AppendSerializedJsonBlock(StringBuilder sb, string title, object payload)
    {
        sb.AppendLine($"  - {title}:");
        var json = JsonSerializer.Serialize(payload, JsonOpts).Replace("\r\n", "\n", StringComparison.Ordinal);
        foreach (var line in json.Split('\n'))
            sb.AppendLine($"    {line}");
    }

    public static async Task<string?> ValidateRequestAgainstCurrentStateAsync(
        FileSystemManager fs,
        PendingShiningTradeInventoryRequest request)
    {
        var soulRoot = await ReadJsonObjectAsync(fs, "game_state/meta/soul_state.json");
        var shiningRoot = await ReadJsonObjectAsync(fs, ShiningAbodeState.StatePath);
        var residentRoot = await ReadJsonObjectAsync(fs, GuardianAbodeResidentState.StatePath);
        var guardiansRoot = await ReadJsonObjectAsync(fs, "game_state/meta/guardians.json");
        var pendingState = await ReadRequestsStateAsync(fs);
        if (shiningRoot == null)
            return "shining_abode_state.json недоступен.";
        var rawOwnerStateError = ShiningAbodeState.ValidateRawOwnerStateForActionableMode(shiningRoot);
        if (rawOwnerStateError != null)
            return rawOwnerStateError;
        if (pendingState.IsMalformed)
            return "pending_shining_trade_inventory_requests.json повреждён. Исправьте или очистите pending trade contract перед созданием нового запроса.";

        ShiningAbodeState.NormalizeStateRoot(shiningRoot, residentRoot, guardiansRoot);
        var ordinaryModeError = ValidateOrdinaryActiveShiningMode(soulRoot, shiningRoot);
        if (ordinaryModeError != null)
            return ordinaryModeError;

        if (string.IsNullOrWhiteSpace(request.FactionId))
            return "Shining trade request требует factionId.";
        if (string.IsNullOrWhiteSpace(request.TradeCycleId))
            return "Shining trade request требует tradeCycleId.";
        if (string.IsNullOrWhiteSpace(request.MerchantProfile) ||
            !string.Equals(request.MerchantProfile, MerchantProfileShiningFaction, StringComparison.OrdinalIgnoreCase))
        {
            return $"merchantProfile должен быть {MerchantProfileShiningFaction}.";
        }

        var faction = ShiningAbodeState.FindFaction(shiningRoot, request.FactionId);
        if (faction == null)
            return "Указанная фракция не найдена.";

        var strength = GetNodeInt(faction["factionStrength"], 0);
        var derivedTradeTier = ShiningAbodeState.GetTradeTier(strength);
        if (derivedTradeTier <= 0)
            return "Для этой фракции trade dormant: запрос витрины недопустим.";

        var derivedSlotCount = ShiningAbodeState.GetTradeStockItemCount(faction, residentRoot);
        var derivedRarityCeiling = ShiningAbodeState.GetTradeRarityCeiling(strength);
        var derivedServiceMultiplier = ShiningAbodeState.GetServiceMultiplier(strength);
        var currentTradeCycleId = ShiningAbodeState.GetTradeCycleId(GetNodeInt(soulRoot?["currentIncarnation"], 0));

        if (!string.Equals(request.TradeCycleId, currentTradeCycleId, StringComparison.OrdinalIgnoreCase))
            return "tradeCycleId должен совпадать с текущим Shining trade cycle.";
        if (request.DerivedTradeTier != derivedTradeTier)
            return "derivedTradeTier не совпадает с canonical значением фракции.";
        if (request.DerivedTradeSlotCount != derivedSlotCount)
            return "derivedTradeSlotCount не совпадает с canonical значением фракции.";
        if (!string.Equals(request.DerivedRarityCeiling, derivedRarityCeiling, StringComparison.OrdinalIgnoreCase))
            return "derivedRarityCeiling не совпадает с canonical значением фракции.";
        if (Math.Abs(request.DerivedServiceMultiplier - derivedServiceMultiplier) > 0.001)
            return "derivedServiceMultiplier не совпадает с canonical значением фракции.";

        if (HasReadyInventoryForCurrentContract(faction, request) ||
            (FindLatestAuthoritativeReadyReceiptForCurrentCycle(faction, request.TradeCycleId) != null &&
             InventoryMatchesRequestContract(faction["tradeInventory"] as JsonObject, request)))
        {
            return "Для этой фракции уже materialized matching Shining trade inventory текущего цикла.";
        }

        var existingRequests = pendingState.Requests;
        if (existingRequests.Any(existing =>
                !string.Equals(existing.RequestId, request.RequestId, StringComparison.OrdinalIgnoreCase) &&
                string.Equals(existing.FactionId, request.FactionId, StringComparison.OrdinalIgnoreCase) &&
                string.Equals(existing.TradeCycleId, request.TradeCycleId, StringComparison.OrdinalIgnoreCase)))
        {
            return "Для этой фракции уже существует pending Shining trade request текущего цикла.";
        }

        return null;
    }

    private static void NormalizeReceiptObject(JsonObject receipt)
    {
        receipt["requestId"] = JsonValue.Create(GetNodeString(receipt["requestId"]));
        receipt["factionId"] = JsonValue.Create(GetNodeString(receipt["factionId"]));
        receipt["factionName"] = JsonValue.Create(GetNodeString(receipt["factionName"]));
        receipt["tradeCycleId"] = JsonValue.Create(GetNodeString(receipt["tradeCycleId"]));
        receipt["status"] = JsonValue.Create(GetNodeString(receipt["status"]));
        receipt["itemCount"] = JsonValue.Create(GetNodeInt(receipt["itemCount"], 0));
        if (receipt.ContainsKey("soldOutCount"))
        {
            receipt["soldOutCount"] = TryReadIntegerNode(receipt["soldOutCount"], out var soldOutCount)
                ? JsonValue.Create(soldOutCount)
                : null;
        }
        receipt["resolvedAtTurn"] = JsonValue.Create(GetNodeInt(receipt["resolvedAtTurn"], 0));
        receipt["resolvedAtUtc"] = JsonValue.Create(GetNodeString(receipt["resolvedAtUtc"]));
    }

    private static string? ValidateOrdinaryActiveShiningMode(JsonObject? soulRoot, JsonObject shiningRoot)
    {
        var currentRealm = GetNodeString(soulRoot?["currentRealm"]);
        if (!string.Equals(currentRealm, "Shining Abode", StringComparison.OrdinalIgnoreCase) &&
            !string.Equals(currentRealm, "Сияющая Обитель", StringComparison.OrdinalIgnoreCase))
        {
            return "Shining trade request допустим только при currentRealm = Shining Abode.";
        }

        if (!string.Equals(GetNodeString(shiningRoot["availability"]), ShiningAbodeState.AvailabilityActive, StringComparison.OrdinalIgnoreCase))
            return "Shining trade request допустим только при availability = active.";
        var packageMode = ShiningAbodeState.GetPreparedIncarnationPackageMode(shiningRoot);
        if (packageMode == ShiningAbodeState.PreparedIncarnationPackageMode.ValidHandoff)
            return "Shining trade request недопустим, пока preparedIncarnationPackage ожидает bootstrap.";
        if (packageMode == ShiningAbodeState.PreparedIncarnationPackageMode.InvalidFault)
            return "Shining trade request недопустим: preparedIncarnationPackage повреждён или не проходит bootstrap validation.";

        return null;
    }

    private static async Task<JsonObject?> ReadJsonObjectAsync(FileSystemManager fs, string path)
    {
        var json = await fs.ReadFileAsync(path);
        if (string.IsNullOrWhiteSpace(json))
            return null;

        try
        {
            return JsonNode.Parse(json) as JsonObject;
        }
        catch
        {
            return null;
        }
    }

    private static bool TryReadBool(JsonNode? node, out bool value)
    {
        value = false;
        if (node == null)
            return false;

        try
        {
            value = node.GetValue<bool>();
            return true;
        }
        catch
        {
            return bool.TryParse(node.ToString(), out value);
        }
    }

    private static bool TryReadDouble(JsonNode? node, out double value)
    {
        value = 0;
        if (node == null)
            return false;

        try
        {
            value = node.GetValue<double>();
            return true;
        }
        catch
        {
            return double.TryParse(node.ToString(), out value);
        }
    }

    private static bool TryReadIntegerNode(JsonNode? node, out int value)
    {
        value = 0;
        if (node == null)
            return false;

        try
        {
            value = node.GetValue<int>();
            return true;
        }
        catch
        {
            return false;
        }
    }

    private static string? GetNodeString(JsonNode? node)
    {
        if (node == null)
            return null;

        try
        {
            return node.GetValue<string>();
        }
        catch
        {
            return node.ToJsonString().Trim('"');
        }
    }

    private static int GetNodeInt(JsonNode? node, int fallback)
    {
        if (node == null)
            return fallback;

        try
        {
            return node.GetValue<int>();
        }
        catch
        {
            return fallback;
        }
    }

    private static bool IsShiningRealm(string? currentRealm) => RealmSemantics.IsShiningRealm(currentRealm);
}
