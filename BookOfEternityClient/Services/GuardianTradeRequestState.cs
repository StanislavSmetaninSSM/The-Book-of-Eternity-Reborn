using System.Text.Json;
using System.Text.Json.Nodes;
using System.Text.Json.Serialization;
using BookOfEternityClient.Configuration;
using BookOfEternityClient.Core;

namespace BookOfEternityClient.Services;

internal static class GuardianTradeRequestState
{
    public const string PendingRequestPath = "game_state/control/pending_guardian_trade_request.json";
    public const string ActionTag = "GUARDIAN_TRADE_REQUEST";
    public const string UpdateReceiptsProperty = "UpdateGuardianTradeInventoryReceipts";
    public const string ReceiptsProperty = "tradeInventoryReceipts";
    public const string ReceiptStatusReady = "ready";

    private static readonly JsonSerializerOptions JsonOpts = SharedJsonOptions.PrettyCamelCaseUnsafeRelaxed;

    internal enum PendingGuardianTradeRequestReadStatus
    {
        Missing,
        Valid,
        Malformed
    }

    internal sealed record PendingGuardianTradeRequestReadResult(
        PendingGuardianTradeRequestReadStatus Status,
        PendingGuardianTradeRequest? Request)
    {
        internal bool Exists => Status != PendingGuardianTradeRequestReadStatus.Missing;
        internal bool IsMalformed => Status == PendingGuardianTradeRequestReadStatus.Malformed;
    }

    public sealed class PendingGuardianTradeRequest
    {
        [JsonPropertyName("requestId")]
        public string RequestId { get; set; } = $"guardian_trade_{Guid.NewGuid():N}";

        [JsonPropertyName("guardianId")]
        public string GuardianId { get; set; } = "";

        [JsonPropertyName("guardianName")]
        public string GuardianName { get; set; } = "";

        [JsonPropertyName("abodeId")]
        public string AbodeId { get; set; } = "";

        [JsonPropertyName("returnCycleId")]
        public string ReturnCycleId { get; set; } = "";

        [JsonPropertyName("currentReputation")]
        public int CurrentReputation { get; set; }

        [JsonPropertyName("derivedTradeSlotCount")]
        public int DerivedTradeSlotCount { get; set; }

        [JsonPropertyName("effectiveRarityCeilingBonusSteps")]
        public int EffectiveRarityCeilingBonusSteps { get; set; }

        [JsonPropertyName("projectBonusSignature")]
        public string ProjectBonusSignature { get; set; } = "0|0|0";

        [JsonPropertyName("createdAtUtc")]
        public string CreatedAtUtc { get; set; } = DateTime.UtcNow.ToString("o");

        [JsonPropertyName("createdAtTurn")]
        public int CreatedAtTurn { get; set; }
    }

    public sealed class TradeInventoryReceiptEntry
    {
        [JsonPropertyName("requestId")]
        public string RequestId { get; set; } = "";

        [JsonPropertyName("guardianId")]
        public string GuardianId { get; set; } = "";

        [JsonPropertyName("guardianName")]
        public string GuardianName { get; set; } = "";

        [JsonPropertyName("abodeId")]
        public string AbodeId { get; set; } = "";

        [JsonPropertyName("tradeCycleId")]
        public string TradeCycleId { get; set; } = "";

        [JsonPropertyName("status")]
        public string Status { get; set; } = ReceiptStatusReady;

        [JsonPropertyName("itemCount")]
        public int ItemCount { get; set; }

        [JsonPropertyName("resolvedAtTurn")]
        public int ResolvedAtTurn { get; set; }

        [JsonPropertyName("resolvedAtUtc")]
        public string ResolvedAtUtc { get; set; } = "";
    }

    public static async Task WriteAsync(FileSystemManager fs, PendingGuardianTradeRequest request)
    {
        var existingState = await ReadStateAsync(fs);
        if (existingState.IsMalformed)
            throw new InvalidOperationException("pending_guardian_trade_request.json повреждён и должен быть исправлен или очищен до записи нового запроса.");
        if (existingState.Request != null &&
            !string.Equals(existingState.Request.RequestId, request.RequestId, StringComparison.OrdinalIgnoreCase))
        {
            throw new InvalidOperationException("pending_guardian_trade_request.json already contains a live guardian trade contract and cannot be overwritten without explicit canonical closure.");
        }

        await fs.WriteFileAtomicAsync(PendingRequestPath, JsonSerializer.Serialize(request, JsonOpts));
    }

    public static async Task<PendingGuardianTradeRequest?> ReadAsync(FileSystemManager fs)
        => (await ReadStateAsync(fs)).Request;

    internal static async Task<PendingGuardianTradeRequestReadResult> ReadStateAsync(FileSystemManager fs)
    {
        var json = await fs.ReadFileAsync(PendingRequestPath);
        return ParseState(json, fs.FileExists(PendingRequestPath));
    }

    internal static PendingGuardianTradeRequestReadResult ParseState(string? json, bool fileExists)
    {
        if (string.IsNullOrWhiteSpace(json))
        {
            return new PendingGuardianTradeRequestReadResult(
                fileExists ? PendingGuardianTradeRequestReadStatus.Malformed : PendingGuardianTradeRequestReadStatus.Missing,
                null);
        }

        try
        {
            var request = JsonSerializer.Deserialize<PendingGuardianTradeRequest>(json, JsonOpts);
            return new PendingGuardianTradeRequestReadResult(
                request == null ? PendingGuardianTradeRequestReadStatus.Malformed : PendingGuardianTradeRequestReadStatus.Valid,
                request);
        }
        catch
        {
            return new PendingGuardianTradeRequestReadResult(PendingGuardianTradeRequestReadStatus.Malformed, null);
        }
    }

    public static void Clear(FileSystemManager fs) => fs.DeleteFile(PendingRequestPath);

    public static JsonArray EnsureReceiptsArray(JsonObject guardian)
    {
        NormalizeGuardianTradeReceiptsShape(guardian);
        return guardian[ReceiptsProperty]!.AsArray();
    }

    public static void NormalizeGuardianTradeReceiptsShape(JsonObject guardian)
    {
        if (guardian[ReceiptsProperty] is not JsonArray receipts)
        {
            if (guardian[UpdateReceiptsProperty] is JsonArray updateReceipts)
                guardian[ReceiptsProperty] = updateReceipts.DeepClone();
            else
                guardian[ReceiptsProperty] = new JsonArray();
        }

        if (guardian[ReceiptsProperty] is not JsonArray normalizedReceipts)
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

    public static void ApplyReceiptUpdates(JsonObject guardiansRoot, JsonArray updates)
    {
        if (guardiansRoot["guardians"] is not JsonArray guardians)
            return;

        foreach (var receipt in updates.OfType<JsonObject>())
        {
            NormalizeReceiptObject(receipt);
            var guardianId = GetNodeString(receipt["guardianId"]);
            if (string.IsNullOrWhiteSpace(guardianId))
                continue;

            var guardian = guardians.OfType<JsonObject>()
                .FirstOrDefault(item => string.Equals(GetNodeString(item["guardianId"]), guardianId, StringComparison.OrdinalIgnoreCase));
            if (guardian == null)
                continue;

            UpsertReceipt(EnsureReceiptsArray(guardian), receipt);
        }
    }

    public static bool MatchesCurrentContract(
        PendingGuardianTradeRequest? request,
        string guardianId,
        string returnCycleId,
        int currentReputation,
        GuardianProjectState.ResolvedGuardianDerivedState derivedState)
    {
        if (request == null)
            return false;

        return string.Equals(request.GuardianId, guardianId, StringComparison.OrdinalIgnoreCase) &&
               string.Equals(request.ReturnCycleId, returnCycleId, StringComparison.OrdinalIgnoreCase) &&
               request.CurrentReputation == currentReputation &&
               request.DerivedTradeSlotCount == derivedState.TradeSlotCount &&
               request.EffectiveRarityCeilingBonusSteps == derivedState.EffectiveGuardianRarityCeilingBonusSteps &&
               string.Equals(request.ProjectBonusSignature, GuardianProjectState.BuildTradeBonusSignature(derivedState), StringComparison.OrdinalIgnoreCase);
    }

    public static bool InventoryMatchesRequestContract(JsonObject? tradeInventory, PendingGuardianTradeRequest request)
    {
        if (tradeInventory == null)
            return false;

        if (!string.Equals(GetNodeString(tradeInventory["tradeCycleId"]), request.ReturnCycleId, StringComparison.OrdinalIgnoreCase))
            return false;

        if (string.IsNullOrWhiteSpace(GetNodeString(tradeInventory["generatedAtUtc"])))
            return false;

        var expectedTier = ResolveTradeTierCode(request.CurrentReputation);
        if (!string.Equals(GetNodeString(tradeInventory["generationReputationTier"]), expectedTier, StringComparison.OrdinalIgnoreCase))
            return false;

        if (!string.Equals(GetNodeString(tradeInventory["pricingReputationTier"]), expectedTier, StringComparison.OrdinalIgnoreCase))
            return false;

        if (GetNodeInt(tradeInventory["effectiveRarityCeilingBonusSteps"], int.MinValue) != request.EffectiveRarityCeilingBonusSteps)
            return false;

        if (!string.Equals(GetNodeString(tradeInventory["projectBonusSignature"]), request.ProjectBonusSignature, StringComparison.OrdinalIgnoreCase))
            return false;

        return tradeInventory["items"] is JsonArray items &&
               items.Count == request.DerivedTradeSlotCount;
    }

    public static JsonObject? FindMatchingReceipt(JsonObject guardian, PendingGuardianTradeRequest request)
    {
        if (guardian[ReceiptsProperty] is not JsonArray receipts)
            return null;

        return receipts.OfType<JsonObject>()
            .FirstOrDefault(receipt => ReceiptMatchesRequestContract(receipt, request, guardian["tradeInventory"] as JsonObject));
    }

    public static bool ReceiptMatchesRequestContract(JsonObject? receipt, PendingGuardianTradeRequest request, JsonObject? tradeInventory)
    {
        if (receipt == null || tradeInventory == null)
            return false;

        if (!string.Equals(GetNodeString(receipt["requestId"]), request.RequestId, StringComparison.OrdinalIgnoreCase))
            return false;

        if (!string.Equals(GetNodeString(receipt["guardianId"]), request.GuardianId, StringComparison.OrdinalIgnoreCase))
            return false;

        if (!string.Equals(GetNodeString(receipt["abodeId"]), request.AbodeId, StringComparison.OrdinalIgnoreCase))
            return false;

        if (!string.Equals(GetNodeString(receipt["tradeCycleId"]), request.ReturnCycleId, StringComparison.OrdinalIgnoreCase))
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

        if (!IsAfterlifeRealm(currentRealm))
        {
            fs.DeleteFile(PendingRequestPath);
            return;
        }

        var json = await fs.ReadFileAsync(PendingRequestPath);
        if (string.IsNullOrWhiteSpace(json))
            return;

        PendingGuardianTradeRequest? request;
        try
        {
            request = JsonSerializer.Deserialize<PendingGuardianTradeRequest>(json, JsonOpts);
        }
        catch
        {
            return;
        }

        if (request == null ||
            string.IsNullOrWhiteSpace(request.RequestId) ||
            string.IsNullOrWhiteSpace(request.GuardianId) ||
            string.IsNullOrWhiteSpace(request.GuardianName) ||
            string.IsNullOrWhiteSpace(request.AbodeId) ||
            string.IsNullOrWhiteSpace(request.ReturnCycleId) ||
            request.DerivedTradeSlotCount <= 0)
        {
            return;
        }

        var guardiansJson = await fs.ReadFileAsync("game_state/meta/guardians.json");
        if (string.IsNullOrWhiteSpace(guardiansJson))
            return;

        try
        {
            if (JsonNode.Parse(guardiansJson) is not JsonObject guardiansRoot ||
                guardiansRoot["guardians"] is not JsonArray guardians)
            {
                return;
            }

            var guardian = guardians.OfType<JsonObject>()
                .FirstOrDefault(item =>
                    string.Equals(GetNodeString(item["guardianId"]), request.GuardianId, StringComparison.OrdinalIgnoreCase));
            if (guardian?["tradeInventory"] is not JsonObject tradeInventory)
                return;

            if (!InventoryMatchesRequestContract(tradeInventory, request))
                return;

            if (!ReceiptMatchesRequestContract(FindMatchingReceipt(guardian, request), request, tradeInventory))
                return;

            fs.DeleteFile(PendingRequestPath);
        }
        catch
        {
            // keep pending request until canonical state is readable again
        }
    }

    private static string? GetNodeString(JsonNode? node)
    {
        if (node is JsonValue value && value.TryGetValue<string>(out var text))
            return text;

        return null;
    }

    private static bool IsAfterlifeRealm(string? currentRealm) =>
        string.Equals(currentRealm, "Chaos Sea", StringComparison.OrdinalIgnoreCase) ||
        string.Equals(currentRealm, "Море Хаоса", StringComparison.OrdinalIgnoreCase) ||
        string.Equals(currentRealm, "Shining Abode", StringComparison.OrdinalIgnoreCase) ||
        string.Equals(currentRealm, "Сияющая Обитель", StringComparison.OrdinalIgnoreCase);

    private static string ResolveTradeTierCode(int reputation) => reputation switch
    {
        <= -51 => "Hostile",
        <= 49 => "Neutral",
        <= 129 => "Friendly",
        <= 229 => "Devoted",
        _ => "Legendary"
    };

    private static int GetNodeInt(JsonNode? node, int fallback)
    {
        if (node is JsonValue value && value.TryGetValue<int>(out var number))
            return number;

        return fallback;
    }

    private static void NormalizeReceiptObject(JsonObject receipt)
    {
        receipt["requestId"] = JsonValue.Create(GetNodeString(receipt["requestId"]));
        receipt["guardianId"] = JsonValue.Create(GetNodeString(receipt["guardianId"]));
        receipt["guardianName"] = JsonValue.Create(GetNodeString(receipt["guardianName"]));
        receipt["abodeId"] = JsonValue.Create(GetNodeString(receipt["abodeId"]));
        receipt["tradeCycleId"] = JsonValue.Create(GetNodeString(receipt["tradeCycleId"]));
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
