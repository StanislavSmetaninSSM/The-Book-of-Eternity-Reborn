using System.Text.Json;
using System.Text.Json.Nodes;
using System.Text.Json.Serialization;
using BookOfEternityClient.Core;

namespace BookOfEternityClient.Services;

internal static class GuardianTradeRequestState
{
    public const string PendingRequestPath = "game_state/control/pending_guardian_trade_request.json";
    public const string ActionTag = "GUARDIAN_TRADE_REQUEST";

    private static readonly JsonSerializerOptions JsonOpts = new()
    {
        WriteIndented = true,
        DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull,
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
        Encoder = System.Text.Encodings.Web.JavaScriptEncoder.UnsafeRelaxedJsonEscaping
    };

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
    }

    public static async Task WriteAsync(FileSystemManager fs, PendingGuardianTradeRequest request)
    {
        await fs.WriteFileAtomicAsync(PendingRequestPath, JsonSerializer.Serialize(request, JsonOpts));
    }

    public static async Task<PendingGuardianTradeRequest?> ReadAsync(FileSystemManager fs)
    {
        var json = await fs.ReadFileAsync(PendingRequestPath);
        if (string.IsNullOrWhiteSpace(json))
            return null;

        try
        {
            return JsonSerializer.Deserialize<PendingGuardianTradeRequest>(json, JsonOpts);
        }
        catch
        {
            return null;
        }
    }

    public static void Clear(FileSystemManager fs) => fs.DeleteFile(PendingRequestPath);

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

    public static async Task EnsureHealthyAsync(FileSystemManager fs, string? currentRealm)
    {
        if (!fs.FileExists(PendingRequestPath))
            return;

        if (!IsAfterlifeRealm(currentRealm))
        {
            fs.DeleteFile(PendingRequestPath);
            return;
        }

        var request = await ReadAsync(fs);
        if (request == null ||
            string.IsNullOrWhiteSpace(request.RequestId) ||
            string.IsNullOrWhiteSpace(request.GuardianId) ||
            string.IsNullOrWhiteSpace(request.GuardianName) ||
            string.IsNullOrWhiteSpace(request.AbodeId) ||
            string.IsNullOrWhiteSpace(request.ReturnCycleId) ||
            request.DerivedTradeSlotCount <= 0)
        {
            fs.DeleteFile(PendingRequestPath);
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
}
