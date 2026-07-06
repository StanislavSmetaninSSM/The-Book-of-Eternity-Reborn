using System.Text.Json;
using System.Text.Json.Nodes;
using System.Text.Json.Serialization;
using BookOfEternityClient.Configuration;
using BookOfEternityClient.Core;

namespace BookOfEternityClient.Services;

internal static class NpcTradeRequestState
{
    public const string PendingRequestPath = "game_state/control/pending_npc_trade_inventory_requests.json";
    public const string RequestsProperty = "requests";
    public const string ActionTag = "NPC_TRADE_REQUEST";
    public const string UpdateReceiptsProperty = "UpdateNpcTradeInventoryReceipts";
    public const string ReceiptsProperty = "tradeInventoryReceipts";
    public const string ReceiptStatusReady = "ready";

    private static readonly JsonSerializerOptions JsonOpts = SharedJsonOptions.PrettyCamelCaseUnsafeRelaxed;

    public sealed class PendingNpcTradeInventoryRequest
    {
        [JsonPropertyName("requestId")]
        public string RequestId { get; set; } = $"npc_trade_{Guid.NewGuid():N}";

        [JsonPropertyName("npcId")]
        public string NpcId { get; set; } = "";

        [JsonPropertyName("npcName")]
        public string NpcName { get; set; } = "";

        [JsonPropertyName("merchantProfile")]
        public string MerchantProfile { get; set; } = "";

        [JsonPropertyName("tradeCycleId")]
        public string TradeCycleId { get; set; } = "";

        [JsonPropertyName("derivedTradeSlotCount")]
        public int DerivedTradeSlotCount { get; set; }

        [JsonPropertyName("createdAtTurn")]
        public int CreatedAtTurn { get; set; }

        [JsonPropertyName("createdAtUtc")]
        public string CreatedAtUtc { get; set; } = DateTime.UtcNow.ToString("o");

        [JsonPropertyName("createdAtWorldDate")]
        public int CreatedAtWorldDate { get; set; }

        [JsonPropertyName("refreshAfterWorldDate")]
        public int RefreshAfterWorldDate { get; set; }
    }

    public sealed class TradeInventoryReceiptEntry
    {
        [JsonPropertyName("requestId")]
        public string RequestId { get; set; } = "";

        [JsonPropertyName("npcId")]
        public string NpcId { get; set; } = "";

        [JsonPropertyName("npcName")]
        public string NpcName { get; set; } = "";

        [JsonPropertyName("tradeCycleId")]
        public string TradeCycleId { get; set; } = "";

        [JsonPropertyName("merchantProfile")]
        public string MerchantProfile { get; set; } = "";

        [JsonPropertyName("status")]
        public string Status { get; set; } = ReceiptStatusReady;

        [JsonPropertyName("itemCount")]
        public int ItemCount { get; set; }

        [JsonPropertyName("resolvedAtTurn")]
        public int ResolvedAtTurn { get; set; }

        [JsonPropertyName("resolvedAtUtc")]
        public string ResolvedAtUtc { get; set; } = "";
    }

    public static async Task WriteRequestsAsync(FileSystemManager fs, IReadOnlyCollection<PendingNpcTradeInventoryRequest> requests)
    {
        if (requests.Count == 0)
        {
            ClearRequests(fs);
            return;
        }

        await fs.WriteFileAtomicAsync(
            PendingRequestPath,
            JsonSerializer.Serialize(new Dictionary<string, object?> { [RequestsProperty] = requests }, JsonOpts));
    }

    public static async Task WriteRequestAsync(FileSystemManager fs, PendingNpcTradeInventoryRequest request)
    {
        var existing = (await ReadRequestsAsync(fs)).ToList();
        var replaced = false;
        for (var i = 0; i < existing.Count; i++)
        {
            if (!string.Equals(existing[i].NpcId, request.NpcId, StringComparison.OrdinalIgnoreCase) ||
                !string.Equals(existing[i].TradeCycleId, request.TradeCycleId, StringComparison.OrdinalIgnoreCase))
            {
                continue;
            }

            existing[i] = request;
            replaced = true;
            break;
        }

        if (!replaced)
            existing.Add(request);

        await WriteRequestsAsync(fs, existing);
    }

    public static async Task<IReadOnlyList<PendingNpcTradeInventoryRequest>> ReadRequestsAsync(FileSystemManager fs)
    {
        var json = await fs.ReadFileAsync(PendingRequestPath);
        return ParseRequests(json);
    }

    public static IReadOnlyList<PendingNpcTradeInventoryRequest> ParseRequests(string? json)
    {
        if (string.IsNullOrWhiteSpace(json))
            return Array.Empty<PendingNpcTradeInventoryRequest>();

        try
        {
            using var doc = JsonDocument.Parse(json);
            if (doc.RootElement.ValueKind == JsonValueKind.Object &&
                doc.RootElement.TryGetProperty(RequestsProperty, out var requestsNode) &&
                requestsNode.ValueKind == JsonValueKind.Array)
            {
                var requests = new List<PendingNpcTradeInventoryRequest>();
                foreach (var item in requestsNode.EnumerateArray())
                {
                    try
                    {
                        var request = JsonSerializer.Deserialize<PendingNpcTradeInventoryRequest>(item.GetRawText(), JsonOpts);
                        if (request != null)
                            requests.Add(request);
                    }
                    catch
                    {
                        // validator and health checks report malformed entries elsewhere
                    }
                }

                return requests;
            }

            var single = JsonSerializer.Deserialize<PendingNpcTradeInventoryRequest>(json, JsonOpts);
            return single == null ? Array.Empty<PendingNpcTradeInventoryRequest>() : new[] { single };
        }
        catch
        {
            return Array.Empty<PendingNpcTradeInventoryRequest>();
        }
    }

    public static async Task<PendingNpcTradeInventoryRequest?> FindPendingRequestAsync(FileSystemManager fs, string npcId, string tradeCycleId)
    {
        return (await ReadRequestsAsync(fs)).FirstOrDefault(request =>
            string.Equals(request.NpcId, npcId, StringComparison.OrdinalIgnoreCase) &&
            string.Equals(request.TradeCycleId, tradeCycleId, StringComparison.OrdinalIgnoreCase));
    }

    public static void ClearRequests(FileSystemManager fs) => fs.DeleteFile(PendingRequestPath);

    public static JsonArray EnsureReceiptsArray(JsonObject npc)
    {
        NormalizeNpcTradeReceiptsShape(npc);
        return npc[ReceiptsProperty]!.AsArray();
    }

    public static void NormalizeNpcTradeReceiptsShape(JsonObject npc)
    {
        if (npc[ReceiptsProperty] is not JsonArray receipts)
        {
            if (npc[UpdateReceiptsProperty] is JsonArray updateReceipts)
                npc[ReceiptsProperty] = updateReceipts.DeepClone();
            else
                npc[ReceiptsProperty] = new JsonArray();
        }

        if (npc[ReceiptsProperty] is not JsonArray normalizedReceipts)
            return;

        for (var i = normalizedReceipts.Count - 1; i >= 0; i--)
        {
            if (normalizedReceipts[i] is not JsonObject receipt)
            {
                normalizedReceipts.RemoveAt(i);
                continue;
            }

            NormalizeReceiptObject(receipt);
        }
    }

    public static void ApplyReceiptUpdates(JsonObject npcRoot, JsonArray updates)
    {
        foreach (var receipt in updates.OfType<JsonObject>())
        {
            NormalizeReceiptObject(receipt);
            var npcId = GetNodeString(receipt["npcId"]);
            if (string.IsNullOrWhiteSpace(npcId))
                continue;

            var npc = FindNpcEntry(npcRoot, npcId);
            if (npc == null)
                continue;

            UpsertReceipt(EnsureReceiptsArray(npc), receipt);
        }
    }

    public static JsonObject? CreateReceiptAppliedValidationView(JsonElement root)
    {
        if (root.ValueKind != JsonValueKind.Object)
            return null;

        var clone = JsonNode.Parse(root.GetRawText()) as JsonObject;
        return clone == null ? null : CreateReceiptAppliedValidationView(clone);
    }

    public static JsonObject CreateReceiptAppliedValidationView(JsonObject root)
    {
        var clone = root.DeepClone() as JsonObject ?? new JsonObject();

        if (clone[UpdateReceiptsProperty] is JsonArray receiptUpdates)
            ApplyReceiptUpdates(clone, receiptUpdates);

        foreach (var npc in GuardianPolicyContracts.EnumerateCanonicalNpcObjects(clone))
            NormalizeNpcTradeReceiptsShape(npc);

        return clone;
    }

    public static bool MatchesCurrentContract(
        PendingNpcTradeInventoryRequest? request,
        string npcId,
        string merchantProfile,
        string tradeCycleId,
        int derivedTradeSlotCount,
        int refreshAfterWorldDate)
    {
        if (request == null)
            return false;

        return string.Equals(request.NpcId, npcId, StringComparison.OrdinalIgnoreCase) &&
               string.Equals(request.MerchantProfile, merchantProfile, StringComparison.OrdinalIgnoreCase) &&
               string.Equals(request.TradeCycleId, tradeCycleId, StringComparison.OrdinalIgnoreCase) &&
               request.DerivedTradeSlotCount == derivedTradeSlotCount &&
               request.RefreshAfterWorldDate == refreshAfterWorldDate;
    }

    public static bool InventoryMatchesRequestContract(JsonObject? tradeInventory, PendingNpcTradeInventoryRequest request)
    {
        if (tradeInventory == null)
            return false;

        if (!string.Equals(GetNodeString(tradeInventory["tradeCycleId"]), request.TradeCycleId, StringComparison.OrdinalIgnoreCase))
            return false;

        var generatedAtWorldDate = GetNodeInt(tradeInventory["generatedAtWorldDate"], -1);
        var refreshAfterWorldDate = GetNodeInt(tradeInventory["refreshAfterWorldDate"], -1);
        if (generatedAtWorldDate < 0 || refreshAfterWorldDate <= generatedAtWorldDate)
            return false;

        if (refreshAfterWorldDate != request.RefreshAfterWorldDate)
            return false;

        if (tradeInventory["items"] is not JsonArray items || items.Count != request.DerivedTradeSlotCount)
            return false;

        foreach (var item in items.OfType<JsonObject>())
        {
            if (!string.Equals(GetNodeString(item["merchantProfile"]), request.MerchantProfile, StringComparison.OrdinalIgnoreCase))
                return false;
        }

        return true;
    }

    public static JsonObject? FindMatchingReceipt(JsonObject npc, PendingNpcTradeInventoryRequest request)
    {
        if (npc[ReceiptsProperty] is not JsonArray receipts)
            return null;

        return receipts.OfType<JsonObject>()
            .FirstOrDefault(receipt => ReceiptMatchesRequestContract(receipt, request, npc["tradeInventory"] as JsonObject));
    }

    public static bool ReceiptMatchesRequestContract(JsonObject? receipt, PendingNpcTradeInventoryRequest request, JsonObject? tradeInventory)
    {
        if (receipt == null || tradeInventory == null)
            return false;

        if (!string.Equals(GetNodeString(receipt["requestId"]), request.RequestId, StringComparison.OrdinalIgnoreCase))
            return false;

        if (!string.Equals(GetNodeString(receipt["npcId"]), request.NpcId, StringComparison.OrdinalIgnoreCase))
            return false;

        if (!string.Equals(GetNodeString(receipt["tradeCycleId"]), request.TradeCycleId, StringComparison.OrdinalIgnoreCase))
            return false;

        if (!string.Equals(GetNodeString(receipt["merchantProfile"]), request.MerchantProfile, StringComparison.OrdinalIgnoreCase))
            return false;

        if (!string.Equals(GetNodeString(receipt["status"]), ReceiptStatusReady, StringComparison.OrdinalIgnoreCase))
            return false;

        if (string.IsNullOrWhiteSpace(GetNodeString(receipt["resolvedAtUtc"])) || GetNodeInt(receipt["resolvedAtTurn"], 0) <= 0)
            return false;

        return GetNodeInt(receipt["itemCount"], -1) == GetTradeInventoryItemCount(tradeInventory);
    }

    public static int GetTradeInventoryItemCount(JsonObject? tradeInventory) =>
        tradeInventory?["items"] is JsonArray items
            ? items.OfType<JsonObject>().Count()
            : 0;

    public static async Task EnsureHealthyAsync(FileSystemManager fs, string? currentRealm)
    {
        if (!fs.FileExists(PendingRequestPath))
            return;

        if (!RealmSemantics.HasResolvedRealm(currentRealm))
            return;

        if (!IsMortalRealm(currentRealm))
            return;

        var requests = (await ReadRequestsAsync(fs)).ToList();
        if (requests.Count == 0)
        {
            fs.DeleteFile(PendingRequestPath);
            return;
        }

        var npcJson = await fs.ReadFileAsync("game_state/npcs/npc_core.json");
        if (string.IsNullOrWhiteSpace(npcJson))
            return;

        try
        {
            if (JsonNode.Parse(npcJson) is not JsonObject npcRoot)
                return;

            var remaining = new List<PendingNpcTradeInventoryRequest>();
            foreach (var request in requests)
            {
                if (string.IsNullOrWhiteSpace(request.RequestId) ||
                    string.IsNullOrWhiteSpace(request.NpcId) ||
                    string.IsNullOrWhiteSpace(request.NpcName) ||
                    string.IsNullOrWhiteSpace(request.MerchantProfile) ||
                    string.IsNullOrWhiteSpace(request.TradeCycleId) ||
                    request.DerivedTradeSlotCount <= 0 ||
                    request.RefreshAfterWorldDate <= request.CreatedAtWorldDate)
                {
                    continue;
                }

                var npc = FindNpcEntry(npcRoot, request.NpcId);
                if (npc?["tradeInventory"] is not JsonObject tradeInventory ||
                    !InventoryMatchesRequestContract(tradeInventory, request) ||
                    !ReceiptMatchesRequestContract(FindMatchingReceipt(npc, request), request, tradeInventory))
                {
                    remaining.Add(request);
                }
            }

            await WriteRequestsAsync(fs, remaining);
        }
        catch
        {
            // keep pending requests until canonical npc state is readable again
        }
    }

    public static async Task<string?> BuildSystemReminderFragmentAsync(FileSystemManager fs, string? currentRealm)
    {
        if (!IsMortalRealm(currentRealm))
            return null;

        var requests = await ReadRequestsAsync(fs);
        if (requests.Count == 0)
            return null;

        var lines = new List<string>
        {
            "NPC TRADE INVENTORY REQUESTS:",
            $"There are {requests.Count} pending entries in pending_npc_trade_inventory_requests.json.",
            "Prefer the session-local GM helper instead of hand-editing the receipt path: dot-source gm_turn_helper.bootstrap.ps1, build an $items array, then call Complete-BoeNpcTradeInventoryRequest -RequestId '<requestId>' -Items $items.",
            "The helper finds same-turn NPCs by NPCId/npcId/id/initialId, validates required itemData fields including tradeItemClass, recalculates slot prices from pricingTradeTier, writes npc.tradeInventory for the requested world-time cycle, and closes the contract through UpdateNpcTradeInventoryReceipts.",
            "If you cannot use the helper, each receipt must carry requestId, npcId, tradeCycleId, merchantProfile, itemCount, resolvedAtTurn, and resolvedAtUtc."
        };

        foreach (var request in requests.Take(5))
            lines.Add($"- requestId={request.RequestId}, npcId={request.NpcId}, tradeCycleId={request.TradeCycleId}, merchantProfile={request.MerchantProfile}, npcName={request.NpcName}");

        return string.Join("\n", lines);
    }

    private static JsonObject? FindNpcEntry(JsonObject root, string npcId)
    {
        return GuardianPolicyContracts.FindCanonicalNpcObject(root, npcId);
    }

    private static IEnumerable<JsonArray> EnumerateNpcArrays(JsonObject root) =>
        GuardianPolicyContracts.EnumerateCanonicalNpcObjectArrays(root);

    private static bool IsMortalRealm(string? currentRealm)
    {
        if (string.IsNullOrWhiteSpace(currentRealm))
            return false;

        return !string.Equals(currentRealm, "Chaos Sea", StringComparison.OrdinalIgnoreCase) &&
               !string.Equals(currentRealm, "Море Хаоса", StringComparison.OrdinalIgnoreCase) &&
               !string.Equals(currentRealm, "Shining Abode", StringComparison.OrdinalIgnoreCase) &&
               !string.Equals(currentRealm, "Сияющая Обитель", StringComparison.OrdinalIgnoreCase);
    }

    private static string? GetNodeString(JsonNode? node)
    {
        if (node is JsonValue value)
        {
            if (value.TryGetValue<string>(out var text))
                return text;
            if (value.TryGetValue<int>(out var number))
                return number.ToString();
        }

        return null;
    }

    private static int GetNodeInt(JsonNode? node, int fallback)
    {
        if (node is JsonValue value)
        {
            if (value.TryGetValue<int>(out var number))
                return number;
            if (value.TryGetValue<string>(out var text) && int.TryParse(text, out var parsed))
                return parsed;
        }

        return fallback;
    }

    private static void NormalizeReceiptObject(JsonObject receipt)
    {
        receipt["requestId"] = JsonValue.Create(GetNodeString(receipt["requestId"]));
        receipt["npcId"] = JsonValue.Create(GetNodeString(receipt["npcId"]));
        receipt["npcName"] = JsonValue.Create(GetNodeString(receipt["npcName"]));
        receipt["tradeCycleId"] = JsonValue.Create(GetNodeString(receipt["tradeCycleId"]));
        receipt["merchantProfile"] = JsonValue.Create(GetNodeString(receipt["merchantProfile"]));
        receipt["status"] = JsonValue.Create(GetNodeString(receipt["status"]));
        receipt["itemCount"] = JsonValue.Create(GetNodeInt(receipt["itemCount"], 0));
        receipt["resolvedAtTurn"] = JsonValue.Create(GetNodeInt(receipt["resolvedAtTurn"], 0));
        receipt["resolvedAtUtc"] = JsonValue.Create(GetNodeString(receipt["resolvedAtUtc"]));
    }

    private static void UpsertReceipt(JsonArray receipts, JsonObject receipt)
    {
        var requestId = GetNodeString(receipt["requestId"]);
        if (string.IsNullOrWhiteSpace(requestId))
            return;

        for (var i = 0; i < receipts.Count; i++)
        {
            if (receipts[i] is not JsonObject existing)
                continue;

            if (!string.Equals(GetNodeString(existing["requestId"]), requestId, StringComparison.OrdinalIgnoreCase))
                continue;

            receipts[i] = receipt.DeepClone();
            return;
        }

        receipts.Add(receipt.DeepClone());
    }
}
