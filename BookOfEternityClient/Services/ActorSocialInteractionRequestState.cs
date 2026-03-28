using System.Text.Json;
using System.Text.Json.Nodes;
using System.Text.Json.Serialization;
using BookOfEternityClient.Configuration;
using BookOfEternityClient.Core;

namespace BookOfEternityClient.Services;

internal static class ActorSocialInteractionRequestState
{
    public const string PendingGuardianRequestPath = "game_state/control/pending_guardian_social_interactions.json";
    public const string PendingNpcRequestPath = "game_state/control/pending_npc_social_interactions.json";
    public const string RequestsProperty = "requests";

    public const string GuardianInteractionTypeTalk = "talk";
    public const string GuardianInteractionTypeLore = "lore";
    public const string NpcInteractionTypeTalk = "talk";

    public const string ResolutionStatusAccepted = "accepted";
    public const string ResolutionStatusRejected = "rejected";
    public const string ResolutionStatusCancelled = "cancelled";

    public const string ResponseModeTalkScene = "talk_scene";
    public const string ResponseModeLoreRevealed = "lore_revealed";
    public const string ResponseModeLoreRefused = "lore_refused";
    public const string ResponseModeWarning = "warning";
    public const string ResponseModeRefusal = "refusal";
    public const string ResponseModeTrustShift = "trust_shift";
    public const string ResponseModeAttitudeShift = "attitude_shift";

    private static readonly JsonSerializerOptions JsonOpts = SharedJsonOptions.PrettyCamelCaseUnsafeRelaxed;

    public sealed class PendingGuardianSocialInteractionRequest
    {
        [JsonPropertyName("requestId")]
        public string RequestId { get; set; } = $"guardian_social_{Guid.NewGuid():N}";

        [JsonPropertyName("guardianId")]
        public string GuardianId { get; set; } = "";

        [JsonPropertyName("guardianName")]
        public string GuardianName { get; set; } = "";

        [JsonPropertyName("interactionType")]
        public string InteractionType { get; set; } = GuardianInteractionTypeTalk;

        [JsonPropertyName("createdAtTurn")]
        public int CreatedAtTurn { get; set; }

        [JsonPropertyName("createdAtUtc")]
        public string CreatedAtUtc { get; set; } = DateTime.UtcNow.ToString("o");
    }

    public sealed class PendingNpcSocialInteractionRequest
    {
        [JsonPropertyName("requestId")]
        public string RequestId { get; set; } = $"npc_social_{Guid.NewGuid():N}";

        [JsonPropertyName("npcId")]
        public string NpcId { get; set; } = "";

        [JsonPropertyName("npcName")]
        public string NpcName { get; set; } = "";

        [JsonPropertyName("interactionType")]
        public string InteractionType { get; set; } = NpcInteractionTypeTalk;

        [JsonPropertyName("createdAtTurn")]
        public int CreatedAtTurn { get; set; }

        [JsonPropertyName("createdAtUtc")]
        public string CreatedAtUtc { get; set; } = DateTime.UtcNow.ToString("o");
    }

    public static bool IsSupportedGuardianInteractionType(string? interactionType) =>
        string.Equals(interactionType, GuardianInteractionTypeTalk, StringComparison.OrdinalIgnoreCase) ||
        string.Equals(interactionType, GuardianInteractionTypeLore, StringComparison.OrdinalIgnoreCase);

    public static bool IsSupportedNpcInteractionType(string? interactionType) =>
        string.Equals(interactionType, NpcInteractionTypeTalk, StringComparison.OrdinalIgnoreCase);

    public static bool IsSupportedResolutionStatus(string? status) =>
        string.Equals(status, ResolutionStatusAccepted, StringComparison.OrdinalIgnoreCase) ||
        string.Equals(status, ResolutionStatusRejected, StringComparison.OrdinalIgnoreCase) ||
        string.Equals(status, ResolutionStatusCancelled, StringComparison.OrdinalIgnoreCase);

    public static bool IsSupportedResponseMode(string? responseMode) =>
        string.Equals(responseMode, ResponseModeTalkScene, StringComparison.OrdinalIgnoreCase) ||
        string.Equals(responseMode, ResponseModeLoreRevealed, StringComparison.OrdinalIgnoreCase) ||
        string.Equals(responseMode, ResponseModeLoreRefused, StringComparison.OrdinalIgnoreCase) ||
        string.Equals(responseMode, ResponseModeWarning, StringComparison.OrdinalIgnoreCase) ||
        string.Equals(responseMode, ResponseModeRefusal, StringComparison.OrdinalIgnoreCase) ||
        string.Equals(responseMode, ResponseModeTrustShift, StringComparison.OrdinalIgnoreCase) ||
        string.Equals(responseMode, ResponseModeAttitudeShift, StringComparison.OrdinalIgnoreCase);

    public static async Task WriteGuardianRequestsAsync(FileSystemManager fs, IReadOnlyCollection<PendingGuardianSocialInteractionRequest> requests)
    {
        if (requests.Count == 0)
        {
            ClearGuardianRequests(fs);
            return;
        }

        await fs.WriteFileAtomicAsync(
            PendingGuardianRequestPath,
            JsonSerializer.Serialize(new Dictionary<string, object?> { [RequestsProperty] = requests }, JsonOpts));
    }

    public static async Task WriteGuardianRequestAsync(FileSystemManager fs, PendingGuardianSocialInteractionRequest request)
    {
        var existing = (await ReadGuardianRequestsAsync(fs)).ToList();
        var replaced = false;
        for (var i = 0; i < existing.Count; i++)
        {
            if (!string.Equals(existing[i].GuardianId, request.GuardianId, StringComparison.OrdinalIgnoreCase) ||
                !string.Equals(existing[i].InteractionType, request.InteractionType, StringComparison.OrdinalIgnoreCase))
            {
                continue;
            }

            existing[i] = request;
            replaced = true;
            break;
        }

        if (!replaced)
            existing.Add(request);

        await WriteGuardianRequestsAsync(fs, existing);
    }

    public static async Task WriteNpcRequestsAsync(FileSystemManager fs, IReadOnlyCollection<PendingNpcSocialInteractionRequest> requests)
    {
        if (requests.Count == 0)
        {
            ClearNpcRequests(fs);
            return;
        }

        await fs.WriteFileAtomicAsync(
            PendingNpcRequestPath,
            JsonSerializer.Serialize(new Dictionary<string, object?> { [RequestsProperty] = requests }, JsonOpts));
    }

    public static async Task WriteNpcRequestAsync(FileSystemManager fs, PendingNpcSocialInteractionRequest request)
    {
        var existing = (await ReadNpcRequestsAsync(fs)).ToList();
        var replaced = false;
        for (var i = 0; i < existing.Count; i++)
        {
            if (!string.Equals(existing[i].NpcId, request.NpcId, StringComparison.OrdinalIgnoreCase) ||
                !string.Equals(existing[i].InteractionType, request.InteractionType, StringComparison.OrdinalIgnoreCase))
            {
                continue;
            }

            existing[i] = request;
            replaced = true;
            break;
        }

        if (!replaced)
            existing.Add(request);

        await WriteNpcRequestsAsync(fs, existing);
    }

    public static async Task<IReadOnlyList<PendingGuardianSocialInteractionRequest>> ReadGuardianRequestsAsync(FileSystemManager fs) =>
        await ReadRequestsAsync(fs, PendingGuardianRequestPath, static json => JsonSerializer.Deserialize<PendingGuardianSocialInteractionRequest>(json, JsonOpts));

    public static async Task<IReadOnlyList<PendingNpcSocialInteractionRequest>> ReadNpcRequestsAsync(FileSystemManager fs) =>
        await ReadRequestsAsync(fs, PendingNpcRequestPath, static json => JsonSerializer.Deserialize<PendingNpcSocialInteractionRequest>(json, JsonOpts));

    public static async Task<PendingGuardianSocialInteractionRequest?> FindPendingGuardianRequestAsync(FileSystemManager fs, string guardianId, string interactionType)
    {
        return (await ReadGuardianRequestsAsync(fs)).FirstOrDefault(request =>
            string.Equals(request.GuardianId, guardianId, StringComparison.OrdinalIgnoreCase) &&
            string.Equals(request.InteractionType, interactionType, StringComparison.OrdinalIgnoreCase));
    }

    public static async Task<PendingNpcSocialInteractionRequest?> FindPendingNpcRequestAsync(FileSystemManager fs, string npcId, string interactionType)
    {
        return (await ReadNpcRequestsAsync(fs)).FirstOrDefault(request =>
            string.Equals(request.NpcId, npcId, StringComparison.OrdinalIgnoreCase) &&
            string.Equals(request.InteractionType, interactionType, StringComparison.OrdinalIgnoreCase));
    }

    public static void ClearGuardianRequests(FileSystemManager fs) => fs.DeleteFile(PendingGuardianRequestPath);

    public static void ClearNpcRequests(FileSystemManager fs) => fs.DeleteFile(PendingNpcRequestPath);

    public static async Task EnsureHealthyAsync(FileSystemManager fs, string? currentRealm)
    {
        if (IsAfterlifeRealm(currentRealm))
        {
            await EnsureGuardianRequestsHealthyAsync(fs);
            ClearNpcRequests(fs);
            return;
        }

        if (IsMortalRealm(currentRealm))
        {
            ClearGuardianRequests(fs);
            await EnsureNpcRequestsHealthyAsync(fs);
            return;
        }

        ClearGuardianRequests(fs);
        ClearNpcRequests(fs);
    }

    public static async Task<string?> BuildSystemReminderFragmentAsync(FileSystemManager fs, string? currentRealm)
    {
        if (IsAfterlifeRealm(currentRealm))
        {
            var guardianRequests = await ReadGuardianRequestsAsync(fs);
            if (guardianRequests.Count == 0)
                return null;

            var lines = new List<string>
            {
                "GUARDIAN SOCIAL REQUESTS:",
                $"There are {guardianRequests.Count} pending entries in pending_guardian_social_interactions.json.",
                "For each request, roleplay the scene in accepted turn and close it canonically through guardianSocialJournalUpdates.",
                "Each closure entry must carry requestId, guardianId, interactionType, status=accepted|rejected|cancelled, optional responseMode, title, summary, turn, and timestamp."
            };

            foreach (var request in guardianRequests.Take(5))
                lines.Add($"- guardianId={request.GuardianId}, interactionType={request.InteractionType}, guardianName={request.GuardianName}");

            return string.Join("\n", lines);
        }

        if (!IsMortalRealm(currentRealm))
            return null;

        var npcRequests = await ReadNpcRequestsAsync(fs);
        if (npcRequests.Count == 0)
            return null;

        var npcLines = new List<string>
        {
            "NPC SOCIAL REQUESTS:",
            $"There are {npcRequests.Count} pending entries in pending_npc_social_interactions.json.",
            "For each request, roleplay the scene in accepted turn and close it canonically through npcInteractionJournalUpdates.",
            "Each closure entry must carry requestId, npcId, interactionType, status=accepted|rejected|cancelled, optional responseMode, title, summary, turn, and timestamp."
        };

        foreach (var request in npcRequests.Take(5))
            npcLines.Add($"- npcId={request.NpcId}, interactionType={request.InteractionType}, npcName={request.NpcName}");

        return string.Join("\n", npcLines);
    }

    public static JsonObject? FindGuardianResolutionEntry(JsonObject? journalRoot, string guardianId, string requestId) =>
        ActorJournalState.FindResolutionEntry(journalRoot, GuardianSocialJournalState.ActorIdProperty, guardianId, requestId);

    public static JsonObject? FindNpcResolutionEntry(JsonObject? journalRoot, string npcId, string requestId) =>
        ActorJournalState.FindResolutionEntry(journalRoot, NpcInteractionJournalState.ActorIdProperty, npcId, requestId);

    private static async Task<IReadOnlyList<T>> ReadRequestsAsync<T>(
        FileSystemManager fs,
        string path,
        Func<string, T?> singleDeserializer) where T : class
    {
        var json = await fs.ReadFileAsync(path);
        if (string.IsNullOrWhiteSpace(json))
            return Array.Empty<T>();

        try
        {
            using var doc = JsonDocument.Parse(json);
            if (doc.RootElement.ValueKind == JsonValueKind.Object &&
                doc.RootElement.TryGetProperty(RequestsProperty, out var requestsNode) &&
                requestsNode.ValueKind == JsonValueKind.Array)
            {
                var result = new List<T>();
                foreach (var item in requestsNode.EnumerateArray())
                {
                    try
                    {
                        var request = singleDeserializer(item.GetRawText());
                        if (request != null)
                            result.Add(request);
                    }
                    catch
                    {
                        // keep file readable; validator/health pass will clean malformed entries
                    }
                }

                return result;
            }

            var single = singleDeserializer(json);
            return single != null ? new[] { single } : Array.Empty<T>();
        }
        catch
        {
            return Array.Empty<T>();
        }
    }

    private static async Task EnsureGuardianRequestsHealthyAsync(FileSystemManager fs)
    {
        if (!fs.FileExists(PendingGuardianRequestPath))
            return;

        var requests = (await ReadGuardianRequestsAsync(fs)).ToList();
        if (requests.Count == 0)
        {
            ClearGuardianRequests(fs);
            return;
        }

        JsonObject? guardiansRoot = null;
        var guardiansJson = await fs.ReadFileAsync("game_state/meta/guardians.json");
        if (!string.IsNullOrWhiteSpace(guardiansJson))
        {
            try
            {
                guardiansRoot = JsonNode.Parse(guardiansJson) as JsonObject;
            }
            catch
            {
                guardiansRoot = null;
            }
        }

        JsonObject? journalRoot = null;
        var journalJson = await fs.ReadFileAsync(GuardianSocialJournalState.StatePath);
        if (!string.IsNullOrWhiteSpace(journalJson))
        {
            try
            {
                journalRoot = JsonNode.Parse(journalJson) as JsonObject;
                if (journalRoot != null)
                    ActorJournalState.NormalizeShape(journalRoot, GuardianSocialJournalState.ActorIdProperty, GuardianSocialJournalState.UpdateProperty);
            }
            catch
            {
                journalRoot = null;
            }
        }

        var remaining = requests
            .Where(request =>
                !string.IsNullOrWhiteSpace(request.RequestId) &&
                !string.IsNullOrWhiteSpace(request.GuardianId) &&
                FindGuardianResolutionEntry(journalRoot, request.GuardianId, request.RequestId) == null &&
                GuardianExists(guardiansRoot, request.GuardianId))
            .ToList();

        await WriteGuardianRequestsAsync(fs, remaining);
    }

    private static async Task EnsureNpcRequestsHealthyAsync(FileSystemManager fs)
    {
        if (!fs.FileExists(PendingNpcRequestPath))
            return;

        var requests = (await ReadNpcRequestsAsync(fs)).ToList();
        if (requests.Count == 0)
        {
            ClearNpcRequests(fs);
            return;
        }

        JsonObject? npcRoot = null;
        var npcJson = await fs.ReadFileAsync("game_state/npcs/npc_core.json");
        if (!string.IsNullOrWhiteSpace(npcJson))
        {
            try
            {
                npcRoot = JsonNode.Parse(npcJson) as JsonObject;
            }
            catch
            {
                npcRoot = null;
            }
        }

        JsonObject? journalRoot = null;
        var journalJson = await fs.ReadFileAsync(NpcInteractionJournalState.StatePath);
        if (!string.IsNullOrWhiteSpace(journalJson))
        {
            try
            {
                journalRoot = JsonNode.Parse(journalJson) as JsonObject;
                if (journalRoot != null)
                    ActorJournalState.NormalizeShape(journalRoot, NpcInteractionJournalState.ActorIdProperty, NpcInteractionJournalState.UpdateProperty);
            }
            catch
            {
                journalRoot = null;
            }
        }

        var remaining = requests
            .Where(request =>
                !string.IsNullOrWhiteSpace(request.RequestId) &&
                !string.IsNullOrWhiteSpace(request.NpcId) &&
                FindNpcResolutionEntry(journalRoot, request.NpcId, request.RequestId) == null &&
                NpcExists(npcRoot, request.NpcId))
            .ToList();

        await WriteNpcRequestsAsync(fs, remaining);
    }

    private static bool GuardianExists(JsonObject? guardiansRoot, string guardianId)
    {
        if (guardiansRoot == null || string.IsNullOrWhiteSpace(guardianId))
            return false;

        if (guardiansRoot["activeGuardian"] is JsonObject activeGuardian &&
            string.Equals(GetNodeString(activeGuardian["guardianId"]), guardianId, StringComparison.OrdinalIgnoreCase))
        {
            return true;
        }

        return guardiansRoot["guardians"] is JsonArray guardians &&
               guardians.OfType<JsonObject>().Any(guardian =>
                   string.Equals(GetNodeString(guardian["guardianId"]), guardianId, StringComparison.OrdinalIgnoreCase));
    }

    private static bool NpcExists(JsonObject? npcRoot, string npcId)
    {
        if (npcRoot == null || string.IsNullOrWhiteSpace(npcId))
            return false;

        foreach (var propertyName in new[] { "UpdateNPCs", "NPCsInScene", "NPCs", "npcs", "npcDataChanges" })
        {
            if (npcRoot[propertyName] is not JsonArray npcs)
                continue;

            if (npcs.OfType<JsonObject>().Any(npc =>
                    string.Equals(GetNodeString(npc["NPCId"]) ?? GetNodeString(npc["npcId"]) ?? GetNodeString(npc["id"]), npcId, StringComparison.OrdinalIgnoreCase)))
            {
                return true;
            }
        }

        return false;
    }

    private static bool IsAfterlifeRealm(string? currentRealm) =>
        string.Equals(currentRealm, "Chaos Sea", StringComparison.OrdinalIgnoreCase) ||
        string.Equals(currentRealm, "Море Хаоса", StringComparison.OrdinalIgnoreCase) ||
        string.Equals(currentRealm, "Shining Abode", StringComparison.OrdinalIgnoreCase) ||
        string.Equals(currentRealm, "Сияющая Обитель", StringComparison.OrdinalIgnoreCase);

    private static bool IsMortalRealm(string? currentRealm) => !string.IsNullOrWhiteSpace(currentRealm) && !IsAfterlifeRealm(currentRealm);

    private static string? GetNodeString(JsonNode? node)
    {
        if (node is JsonValue value && value.TryGetValue<string>(out var text))
            return text;

        return null;
    }
}
