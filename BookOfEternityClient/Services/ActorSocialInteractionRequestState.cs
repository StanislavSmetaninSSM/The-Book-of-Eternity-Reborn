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

    internal sealed record RequestReadState<T>(
        bool FilePresent,
        bool IsMalformed,
        IReadOnlyList<T> Requests) where T : class;

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
        var existingState = await ReadGuardianRequestsStateAsync(fs);
        if (existingState.IsMalformed)
            throw new InvalidOperationException("pending_guardian_social_interactions.json повреждён и должен быть исправлен или очищен до записи нового guardian social request.");

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
        var existingState = await ReadGuardianRequestsStateAsync(fs);
        if (existingState.IsMalformed)
            throw new InvalidOperationException("pending_guardian_social_interactions.json повреждён и должен быть исправлен или очищен до записи нового guardian social request.");

        var existing = existingState.Requests.ToList();
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
        var existingState = await ReadNpcRequestsStateAsync(fs);
        if (existingState.IsMalformed)
            throw new InvalidOperationException("pending_npc_social_interactions.json повреждён и должен быть исправлен или очищен до записи нового NPC social request.");

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
        var existingState = await ReadNpcRequestsStateAsync(fs);
        if (existingState.IsMalformed)
            throw new InvalidOperationException("pending_npc_social_interactions.json повреждён и должен быть исправлен или очищен до записи нового NPC social request.");

        var existing = existingState.Requests.ToList();
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
        (await ReadGuardianRequestsStateAsync(fs)).Requests;

    public static async Task<IReadOnlyList<PendingNpcSocialInteractionRequest>> ReadNpcRequestsAsync(FileSystemManager fs) =>
        (await ReadNpcRequestsStateAsync(fs)).Requests;

    internal static async Task<RequestReadState<PendingGuardianSocialInteractionRequest>> ReadGuardianRequestsStateAsync(FileSystemManager fs) =>
        await ReadRequestsStateAsync(fs, PendingGuardianRequestPath, static json => JsonSerializer.Deserialize<PendingGuardianSocialInteractionRequest>(json, JsonOpts));

    internal static async Task<RequestReadState<PendingNpcSocialInteractionRequest>> ReadNpcRequestsStateAsync(FileSystemManager fs) =>
        await ReadRequestsStateAsync(fs, PendingNpcRequestPath, static json => JsonSerializer.Deserialize<PendingNpcSocialInteractionRequest>(json, JsonOpts));

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
        var guardianState = await ReadGuardianRequestsStateAsync(fs);
        var npcState = await ReadNpcRequestsStateAsync(fs);

        if (!RealmSemantics.HasResolvedRealm(currentRealm))
            return;

        if (IsAfterlifeRealm(currentRealm))
        {
            if (!guardianState.IsMalformed)
                await EnsureGuardianRequestsHealthyAsync(fs, guardianState);
            // NPC social requests are Mortal-only, but afterlife health checks must preserve
            // wrong-realm files as repair evidence; validation surfaces the realm mismatch.
            return;
        }

        if (IsMortalRealm(currentRealm))
        {
            if (!guardianState.IsMalformed)
                ClearGuardianRequests(fs);
            if (!npcState.IsMalformed)
                await EnsureNpcRequestsHealthyAsync(fs, npcState);
            return;
        }

        if (!guardianState.IsMalformed)
            ClearGuardianRequests(fs);
        if (!npcState.IsMalformed)
            ClearNpcRequests(fs);
    }

    public static async Task<string?> BuildSystemReminderFragmentAsync(FileSystemManager fs, string? currentRealm)
    {
        if (IsAfterlifeRealm(currentRealm))
        {
            var guardianState = await ReadGuardianRequestsStateAsync(fs);
            if (guardianState.IsMalformed)
            {
                return "GUARDIAN SOCIAL REQUEST CORRUPTION:\n" +
                       "pending_guardian_social_interactions.json unreadable or malformed.\n" +
                       "Preserve the pending guardian-social contract until validation/repair resolves it.";
            }

            var guardianRequests = guardianState.Requests;
            if (guardianRequests.Count == 0)
                return null;

            var lines = new List<string>
            {
                "GUARDIAN SOCIAL REQUESTS:",
                $"There are {guardianRequests.Count} pending entries in pending_guardian_social_interactions.json.",
                "For each request, roleplay the scene in accepted turn and close it canonically through guardianSocialJournalUpdates.",
                "Each closure entry must carry requestId, guardianId, interactionType, status=accepted|rejected|cancelled, optional responseMode, title, summary, turn, and timestamp."
            };

            foreach (var request in guardianRequests)
            {
                lines.Add($"- requestId={request.RequestId}, guardianId={request.GuardianId}, interactionType={request.InteractionType}, guardianName={request.GuardianName}, createdAtTurn={request.CreatedAtTurn}, createdAtUtc={request.CreatedAtUtc}");
                AppendSerializedJsonLines(lines, "Full pending guardian-social DTO", request);
            }

            return string.Join("\n", lines);
        }

        if (!IsMortalRealm(currentRealm))
            return null;

        var npcState = await ReadNpcRequestsStateAsync(fs);
        if (npcState.IsMalformed)
        {
            return "NPC SOCIAL REQUEST CORRUPTION:\n" +
                   "pending_npc_social_interactions.json unreadable or malformed.\n" +
                   "Preserve the pending NPC-social contract until validation/repair resolves it.";
        }

        var npcRequests = npcState.Requests;
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

    private static void AppendSerializedJsonLines(List<string> lines, string title, object payload)
    {
        lines.Add($"  {title}:");
        var json = JsonSerializer.Serialize(payload, JsonOpts).Replace("\r\n", "\n", StringComparison.Ordinal);
        foreach (var line in json.Split('\n'))
            lines.Add($"    {line}");
    }

    public static JsonObject? FindGuardianResolutionEntry(JsonObject? journalRoot, string guardianId, string requestId) =>
        ActorJournalState.FindResolutionEntry(journalRoot, GuardianSocialJournalState.ActorIdProperty, guardianId, requestId);

    public static JsonObject? FindNpcResolutionEntry(JsonObject? journalRoot, string npcId, string requestId) =>
        ActorJournalState.FindResolutionEntry(journalRoot, NpcInteractionJournalState.ActorIdProperty, npcId, requestId);

    private static async Task<RequestReadState<T>> ReadRequestsStateAsync<T>(
        FileSystemManager fs,
        string path,
        Func<string, T?> singleDeserializer) where T : class
    {
        var json = await fs.ReadFileAsync(path);
        return AnalyzeRequests(json, fs.FileExists(path), singleDeserializer);
    }

    private static RequestReadState<T> AnalyzeRequests<T>(
        string? json,
        bool filePresent,
        Func<string, T?> singleDeserializer) where T : class
    {
        if (string.IsNullOrWhiteSpace(json))
            return new RequestReadState<T>(filePresent, filePresent, Array.Empty<T>());

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
                    var request = singleDeserializer(item.GetRawText());
                    if (request == null)
                        return new RequestReadState<T>(filePresent, true, Array.Empty<T>());

                    result.Add(request);
                }

                return new RequestReadState<T>(filePresent, false, result);
            }

            var single = singleDeserializer(json);
            return single != null
                ? new RequestReadState<T>(filePresent, false, new[] { single })
                : new RequestReadState<T>(filePresent, true, Array.Empty<T>());
        }
        catch
        {
            return new RequestReadState<T>(filePresent, true, Array.Empty<T>());
        }
    }

    private static async Task EnsureGuardianRequestsHealthyAsync(
        FileSystemManager fs,
        RequestReadState<PendingGuardianSocialInteractionRequest> requestState)
    {
        if (!fs.FileExists(PendingGuardianRequestPath))
            return;

        if (requestState.IsMalformed)
            return;

        var requests = requestState.Requests.ToList();
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

    private static async Task EnsureNpcRequestsHealthyAsync(
        FileSystemManager fs,
        RequestReadState<PendingNpcSocialInteractionRequest> requestState)
    {
        if (!fs.FileExists(PendingNpcRequestPath))
            return;

        if (requestState.IsMalformed)
            return;

        var requests = requestState.Requests.ToList();
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

        return GuardianPolicyContracts.ContainsCanonicalNpcObject(npcRoot, npcId);
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
