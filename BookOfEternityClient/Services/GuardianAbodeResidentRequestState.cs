using System.Text.Json;
using System.Text.Json.Nodes;
using System.Text.Json.Serialization;
using BookOfEternityClient.Configuration;
using BookOfEternityClient.Core;

namespace BookOfEternityClient.Services;

internal static class GuardianAbodeResidentRequestState
{
    public const string PendingResidentsRequestPath = "game_state/control/pending_guardian_abode_residents_request.json";
    public const string PendingInteractionsRequestPath = "game_state/control/pending_guardian_abode_resident_interactions.json";
    public const string PendingManifestationRequestPath = "game_state/control/pending_resident_companion_manifestation_request.json";
    public const string ResidentsRequestsProperty = "requests";
    public const string InteractionRequestsProperty = "requests";
    public const string ManifestationRequestsProperty = "requests";

    private static readonly JsonSerializerOptions JsonOpts = SharedJsonOptions.PrettyCamelCaseUnsafeRelaxed;

    public sealed class PendingGuardianAbodeResidentsRequest
    {
        [JsonPropertyName("requestId")]
        public string RequestId { get; set; } = $"abode_residents_{Guid.NewGuid():N}";

        [JsonPropertyName("guardianId")]
        public string GuardianId { get; set; } = "";

        [JsonPropertyName("guardianName")]
        public string GuardianName { get; set; } = "";

        [JsonPropertyName("abodeId")]
        public string AbodeId { get; set; } = "";

        [JsonPropertyName("abodeName")]
        public string AbodeName { get; set; } = "";

        [JsonPropertyName("currentReputation")]
        public int CurrentReputation { get; set; }

        [JsonPropertyName("createdAtTurn")]
        public int CreatedAtTurn { get; set; }

        [JsonPropertyName("createdAtUtc")]
        public string CreatedAtUtc { get; set; } = DateTime.UtcNow.ToString("o");
    }

    public sealed class PendingGuardianAbodeResidentInteractionRequest
    {
        [JsonPropertyName("requestId")]
        public string RequestId { get; set; } = $"abode_resident_interaction_{Guid.NewGuid():N}";

        [JsonPropertyName("guardianId")]
        public string GuardianId { get; set; } = "";

        [JsonPropertyName("guardianName")]
        public string GuardianName { get; set; } = "";

        [JsonPropertyName("abodeId")]
        public string AbodeId { get; set; } = "";

        [JsonPropertyName("abodeName")]
        public string AbodeName { get; set; } = "";

        [JsonPropertyName("residentId")]
        public string ResidentId { get; set; } = "";

        [JsonPropertyName("residentName")]
        public string ResidentName { get; set; } = "";

        [JsonPropertyName("interactionType")]
        public string InteractionType { get; set; } = GuardianAbodeResidentState.InteractionTypeTalk;

        [JsonPropertyName("createdAtTurn")]
        public int CreatedAtTurn { get; set; }

        [JsonPropertyName("createdAtUtc")]
        public string CreatedAtUtc { get; set; } = DateTime.UtcNow.ToString("o");
    }

    public sealed class PendingResidentCompanionManifestationRequest
    {
        [JsonPropertyName("requestId")]
        public string RequestId { get; set; } = $"resident_companion_{Guid.NewGuid():N}";

        [JsonPropertyName("manifestationSource")]
        public string ManifestationSource { get; set; } = "resident_relic";

        [JsonPropertyName("relicId")]
        public string RelicId { get; set; } = "";

        [JsonPropertyName("relicName")]
        public string RelicName { get; set; } = "";

        [JsonPropertyName("sourceResidentId")]
        public string SourceResidentId { get; set; } = "";

        [JsonPropertyName("sourceImprintId")]
        public string SourceImprintId { get; set; } = "";

        [JsonPropertyName("sourceGuardianId")]
        public string SourceGuardianId { get; set; } = "";

        [JsonPropertyName("sourceGuardianName")]
        public string SourceGuardianName { get; set; } = "";

        [JsonPropertyName("targetIncarnation")]
        public int TargetIncarnation { get; set; }

        [JsonPropertyName("companionNameHint")]
        public string CompanionNameHint { get; set; } = "";

        [JsonPropertyName("originWorldSummary")]
        public string OriginWorldSummary { get; set; } = "";

        [JsonPropertyName("futureCompanionPrompt")]
        public string FutureCompanionPrompt { get; set; } = "";

        [JsonPropertyName("bondReason")]
        public string BondReason { get; set; } = "";

        [JsonPropertyName("coreTraits")]
        public List<string> CoreTraits { get; set; } = new();

        [JsonPropertyName("archetypeHints")]
        public List<string> ArchetypeHints { get; set; } = new();

        [JsonPropertyName("appearanceMotifs")]
        public List<string> AppearanceMotifs { get; set; } = new();

        [JsonPropertyName("createdAtUtc")]
        public string CreatedAtUtc { get; set; } = DateTime.UtcNow.ToString("o");
    }

    public static async Task WriteResidentsRequestsAsync(
        FileSystemManager fs,
        IReadOnlyCollection<PendingGuardianAbodeResidentsRequest> requests)
    {
        if (requests.Count == 0)
        {
            ClearResidentsRequest(fs);
            return;
        }

        await fs.WriteFileAtomicAsync(
            PendingResidentsRequestPath,
            JsonSerializer.Serialize(new Dictionary<string, object?>
            {
                [ResidentsRequestsProperty] = requests
            }, JsonOpts));
    }

    public static async Task WriteResidentsRequestAsync(FileSystemManager fs, PendingGuardianAbodeResidentsRequest request)
    {
        var existing = (await ReadResidentsRequestsAsync(fs)).ToList();
        var replaced = false;
        for (var i = 0; i < existing.Count; i++)
        {
            if (!string.Equals(existing[i].GuardianId, request.GuardianId, StringComparison.OrdinalIgnoreCase) ||
                !string.Equals(existing[i].AbodeId, request.AbodeId, StringComparison.OrdinalIgnoreCase))
            {
                continue;
            }

            existing[i] = request;
            replaced = true;
            break;
        }

        if (!replaced)
            existing.Add(request);

        await WriteResidentsRequestsAsync(fs, existing);
    }

    public static async Task WriteInteractionRequestsAsync(
        FileSystemManager fs,
        IReadOnlyCollection<PendingGuardianAbodeResidentInteractionRequest> requests)
    {
        if (requests.Count == 0)
        {
            ClearInteractionRequests(fs);
            return;
        }

        await fs.WriteFileAtomicAsync(
            PendingInteractionsRequestPath,
            JsonSerializer.Serialize(new Dictionary<string, object?>
            {
                [InteractionRequestsProperty] = requests
            }, JsonOpts));
    }

    public static async Task WriteInteractionRequestAsync(FileSystemManager fs, PendingGuardianAbodeResidentInteractionRequest request)
    {
        var existing = (await ReadInteractionRequestsAsync(fs)).ToList();
        var replaced = false;
        for (var i = 0; i < existing.Count; i++)
        {
            if (!string.Equals(existing[i].ResidentId, request.ResidentId, StringComparison.OrdinalIgnoreCase) ||
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

        await WriteInteractionRequestsAsync(fs, existing);
    }

    public static async Task WriteManifestationRequestsAsync(
        FileSystemManager fs,
        IReadOnlyCollection<PendingResidentCompanionManifestationRequest> requests)
    {
        if (requests.Count == 0)
        {
            ClearManifestationRequest(fs);
            return;
        }

        await fs.WriteFileAtomicAsync(
            PendingManifestationRequestPath,
            JsonSerializer.Serialize(new Dictionary<string, object?>
            {
                [ManifestationRequestsProperty] = requests
            }, JsonOpts));
    }

    public static async Task<IReadOnlyList<PendingGuardianAbodeResidentsRequest>> ReadResidentsRequestsAsync(FileSystemManager fs)
    {
        var json = await fs.ReadFileAsync(PendingResidentsRequestPath);
        if (string.IsNullOrWhiteSpace(json))
            return Array.Empty<PendingGuardianAbodeResidentsRequest>();

        try
        {
            using var doc = JsonDocument.Parse(json);
            if (doc.RootElement.ValueKind == JsonValueKind.Object &&
                doc.RootElement.TryGetProperty(ResidentsRequestsProperty, out var requestsNode) &&
                requestsNode.ValueKind == JsonValueKind.Array)
            {
                var result = new List<PendingGuardianAbodeResidentsRequest>();
                foreach (var item in requestsNode.EnumerateArray())
                {
                    try
                    {
                        var request = JsonSerializer.Deserialize<PendingGuardianAbodeResidentsRequest>(item.GetRawText(), JsonOpts);
                        if (request != null)
                            result.Add(request);
                    }
                    catch
                    {
                        // ignore malformed item; EnsureHealthyAsync will clean the file
                    }
                }

                return result;
            }

            var single = JsonSerializer.Deserialize<PendingGuardianAbodeResidentsRequest>(json, JsonOpts);
            return single != null ? new[] { single } : Array.Empty<PendingGuardianAbodeResidentsRequest>();
        }
        catch
        {
            return Array.Empty<PendingGuardianAbodeResidentsRequest>();
        }
    }

    public static async Task<PendingGuardianAbodeResidentsRequest?> ReadResidentsRequestAsync(FileSystemManager fs) =>
        (await ReadResidentsRequestsAsync(fs)).FirstOrDefault();

    public static async Task<IReadOnlyList<PendingGuardianAbodeResidentInteractionRequest>> ReadInteractionRequestsAsync(FileSystemManager fs)
    {
        var json = await fs.ReadFileAsync(PendingInteractionsRequestPath);
        if (string.IsNullOrWhiteSpace(json))
            return Array.Empty<PendingGuardianAbodeResidentInteractionRequest>();

        try
        {
            using var doc = JsonDocument.Parse(json);
            if (doc.RootElement.ValueKind == JsonValueKind.Object &&
                doc.RootElement.TryGetProperty(InteractionRequestsProperty, out var requestsNode) &&
                requestsNode.ValueKind == JsonValueKind.Array)
            {
                var result = new List<PendingGuardianAbodeResidentInteractionRequest>();
                foreach (var item in requestsNode.EnumerateArray())
                {
                    try
                    {
                        var request = JsonSerializer.Deserialize<PendingGuardianAbodeResidentInteractionRequest>(item.GetRawText(), JsonOpts);
                        if (request != null)
                            result.Add(request);
                    }
                    catch
                    {
                        // ignore malformed item; EnsureHealthyAsync will clean the file
                    }
                }

                return result;
            }

            var single = JsonSerializer.Deserialize<PendingGuardianAbodeResidentInteractionRequest>(json, JsonOpts);
            return single != null ? new[] { single } : Array.Empty<PendingGuardianAbodeResidentInteractionRequest>();
        }
        catch
        {
            return Array.Empty<PendingGuardianAbodeResidentInteractionRequest>();
        }
    }

    public static async Task<PendingGuardianAbodeResidentInteractionRequest?> FindPendingInteractionAsync(
        FileSystemManager fs,
        string residentId,
        string interactionType)
    {
        return (await ReadInteractionRequestsAsync(fs)).FirstOrDefault(request =>
            string.Equals(request.ResidentId, residentId, StringComparison.OrdinalIgnoreCase) &&
            string.Equals(request.InteractionType, interactionType, StringComparison.OrdinalIgnoreCase));
    }

    public static async Task<IReadOnlyList<PendingResidentCompanionManifestationRequest>> ReadManifestationRequestsAsync(FileSystemManager fs)
    {
        var json = await fs.ReadFileAsync(PendingManifestationRequestPath);
        if (string.IsNullOrWhiteSpace(json))
            return Array.Empty<PendingResidentCompanionManifestationRequest>();

        try
        {
            using var doc = JsonDocument.Parse(json);
            if (doc.RootElement.ValueKind == JsonValueKind.Object &&
                doc.RootElement.TryGetProperty(ManifestationRequestsProperty, out var requestsNode) &&
                requestsNode.ValueKind == JsonValueKind.Array)
            {
                var result = new List<PendingResidentCompanionManifestationRequest>();
                foreach (var item in requestsNode.EnumerateArray())
                {
                    try
                    {
                        var request = JsonSerializer.Deserialize<PendingResidentCompanionManifestationRequest>(item.GetRawText(), JsonOpts);
                        if (request != null)
                            result.Add(request);
                    }
                    catch
                    {
                        // ignore malformed item; EnsureHealthyAsync will clean the file
                    }
                }

                return result;
            }

            // legacy single-object compatibility
            var single = JsonSerializer.Deserialize<PendingResidentCompanionManifestationRequest>(json, JsonOpts);
            return single != null ? new[] { single } : Array.Empty<PendingResidentCompanionManifestationRequest>();
        }
        catch
        {
            return Array.Empty<PendingResidentCompanionManifestationRequest>();
        }
    }

    public static void ClearResidentsRequest(FileSystemManager fs) => fs.DeleteFile(PendingResidentsRequestPath);

    public static void ClearInteractionRequests(FileSystemManager fs) => fs.DeleteFile(PendingInteractionsRequestPath);

    public static void ClearManifestationRequest(FileSystemManager fs) => fs.DeleteFile(PendingManifestationRequestPath);

    public static async Task EnsureHealthyAsync(FileSystemManager fs, string? currentRealm)
    {
        if (!IsAfterlifeRealm(currentRealm))
        {
            ClearInteractionRequests(fs);
            var manifestationRequests = await ReadManifestationRequestsAsync(fs);
            if (manifestationRequests.Count == 0)
                ClearManifestationRequest(fs);
            else
                await EnsureManifestationRequestsHealthyAsync(fs, manifestationRequests, currentRealm);
        }
        else
        {
            await EnsureResidentsRequestHealthyAsync(fs);
            await EnsureInteractionRequestsHealthyAsync(fs);
            ClearManifestationRequest(fs);
        }
    }

    public static async Task EnsureManifestationRequestForCurrentIncarnationAsync(FileSystemManager fs, string? currentRealm)
    {
        if (!IsMortalRealm(currentRealm))
        {
            ClearManifestationRequest(fs);
            return;
        }

        var existingRequests = (await ReadManifestationRequestsAsync(fs)).ToList();
        if (existingRequests.Count > 0)
            await EnsureManifestationRequestsHealthyAsync(fs, existingRequests, currentRealm);

        var soulJson = await fs.ReadFileAsync("game_state/meta/soul_state.json");
        if (string.IsNullOrWhiteSpace(soulJson))
            return;

        JsonObject? soulRoot;
        try
        {
            soulRoot = JsonNode.Parse(soulJson) as JsonObject;
        }
        catch
        {
            return;
        }

        if (soulRoot == null)
            return;

        var currentIncarnation = GetNodeInt(soulRoot["currentIncarnation"]);
        if (currentIncarnation <= 0)
            return;

        if (soulRoot["soulRelics"] is not JsonObject soulRelics || soulRelics["equipped"] is not JsonArray equipped)
            return;

        var existingRelicIds = existingRequests
            .Where(request => request.TargetIncarnation == currentIncarnation)
            .Select(request => request.RelicId)
            .ToHashSet(StringComparer.OrdinalIgnoreCase);

        var requestsToAppend = new List<PendingResidentCompanionManifestationRequest>();
        foreach (var relic in equipped.OfType<JsonObject>())
        {
            if (!IsEligibleCompanionEchoRelic(relic, currentIncarnation, existingRelicIds))
                continue;

            relic["companionManifestationLastRequestedIncarnation"] = currentIncarnation;
            if (!TryBuildManifestationRequest(relic, currentIncarnation, out var request))
                continue;

            relic["companionManifestationStatus"] = "pending";
            relic["lastManifestationRequestId"] = request.RequestId;
            relic.Remove("companionManifestationResolvedRequestId");
            relic.Remove("companionManifestationResolvedNpcId");
            relic.Remove("companionManifestationResolvedAtTurn");
            relic.Remove("companionManifestationResolvedAtUtc");
            requestsToAppend.Add(request);
            existingRelicIds.Add(request.RelicId);
        }

        if (requestsToAppend.Count == 0)
            return;

        await fs.WriteFileAtomicAsync("game_state/meta/soul_state.json", soulRoot.ToJsonString(JsonOpts));
        foreach (var request in requestsToAppend)
            existingRequests.Add(request);
        await WriteManifestationRequestsAsync(fs, existingRequests);
    }

    public static async Task<string?> BuildSystemReminderFragmentAsync(FileSystemManager fs, string? currentRealm)
    {
        if (IsAfterlifeRealm(currentRealm))
        {
            var rosterRequests = await ReadResidentsRequestsAsync(fs);
            var interactionRequests = await ReadInteractionRequestsAsync(fs);
            if (rosterRequests.Count == 0 && interactionRequests.Count == 0)
                return null;

            var rosterLines = new List<string>();
            if (rosterRequests.Count > 0)
            {
                rosterLines.AddRange(new[]
                {
                    "ABODE RESIDENT ROSTER REQUESTS:",
                    $"There are {rosterRequests.Count} pending entries in pending_guardian_abode_residents_request.json.",
                    "Materialize explicit residents in game_state/meta/guardian_abode_residents.json via UpdateGuardianAbodeResidents and close each request through UpdateGuardianAbodeResidentRosterReceipts.",
                    "Do not derive residents from guardian domain; author the roster explicitly."
                });

                foreach (var request in rosterRequests.Take(5))
                    rosterLines.Add($"- guardianId={request.GuardianId}, abodeId={request.AbodeId}, guardianName={request.GuardianName}, abodeName={request.AbodeName}");
            }

            if (interactionRequests.Count > 0)
            {
                if (rosterLines.Count > 0)
                    rosterLines.Add(string.Empty);

                rosterLines.AddRange(new[]
                {
                    "ABODE RESIDENT INTERACTION REQUESTS:",
                    $"There are {interactionRequests.Count} pending entries in pending_guardian_abode_resident_interactions.json.",
                    "For each request, roleplay the scene in accepted turn and close it canonically through guardian_abode_residents.json.interactionReceipts[].",
                    "Do not ignore resident talk/history requests; close each one with status=accepted|rejected|cancelled."
                });

                foreach (var request in interactionRequests.Take(5))
                    rosterLines.Add($"- residentId={request.ResidentId}, interactionType={request.InteractionType}, guardianId={request.GuardianId}, abodeId={request.AbodeId}");
            }

            return string.Join("\n", rosterLines);
        }

        var manifestationRequests = await ReadManifestationRequestsAsync(fs);
        if (manifestationRequests.Count == 0)
            return null;

        var lines = new List<string>
        {
            "COMPANION ECHO MANIFESTATION REQUESTS:",
            $"There are {manifestationRequests.Count} pending entries in pending_resident_companion_manifestation_request.json.",
            "For each request, materialize an early mortal-world encounter or soul-quest path that leads to this companion. When the companion fully manifests, write an ordinary mortal NPC in npc_core.json and set sourceCompanionRelicId/sourceAfterlifeResidentId/sourceSoulImprintId when applicable.",
            "This is a guaranteed early encounter path, not an instant teleport-spawn in arbitrary combat."
        };
        foreach (var request in manifestationRequests.Take(5))
            lines.Add($"- relicId={request.RelicId}, source={request.ManifestationSource}, residentId={request.SourceResidentId}, imprintId={request.SourceImprintId}, targetIncarnation={request.TargetIncarnation}");

        return string.Join("\n", lines);
    }

    private static async Task EnsureResidentsRequestHealthyAsync(FileSystemManager fs)
    {
        if (!fs.FileExists(PendingResidentsRequestPath))
            return;

        var requests = (await ReadResidentsRequestsAsync(fs)).ToList();
        if (requests.Count == 0)
        {
            ClearResidentsRequest(fs);
            return;
        }

        var json = await fs.ReadFileAsync(GuardianAbodeResidentState.StatePath);
        if (string.IsNullOrWhiteSpace(json))
            return;

        try
        {
            using var doc = JsonDocument.Parse(json);
            var remaining = requests
                .Where(request =>
                    !string.IsNullOrWhiteSpace(request.RequestId) &&
                    !string.IsNullOrWhiteSpace(request.GuardianId) &&
                    !string.IsNullOrWhiteSpace(request.AbodeId) &&
                    !GuardianAbodeResidentState.HasResidentsForAbode(doc.RootElement, request.GuardianId, request.AbodeId))
                .ToList();
            await WriteResidentsRequestsAsync(fs, remaining);
        }
        catch
        {
            // keep request until roster state is readable again
        }
    }

    private static async Task EnsureInteractionRequestsHealthyAsync(FileSystemManager fs)
    {
        if (!fs.FileExists(PendingInteractionsRequestPath))
            return;

        var requests = (await ReadInteractionRequestsAsync(fs)).ToList();
        if (requests.Count == 0)
        {
            ClearInteractionRequests(fs);
            return;
        }

        var json = await fs.ReadFileAsync(GuardianAbodeResidentState.StatePath);
        if (string.IsNullOrWhiteSpace(json))
            return;

        try
        {
            if (JsonNode.Parse(json) is not JsonObject root)
                return;

            GuardianAbodeResidentState.NormalizeShape(root);
            var receipts = GuardianAbodeResidentState.EnsureInteractionReceiptsArray(root);
            var remaining = requests
                .Where(request =>
                    !string.IsNullOrWhiteSpace(request.RequestId) &&
                    !string.IsNullOrWhiteSpace(request.ResidentId) &&
                    GuardianAbodeResidentState.FindInteractionReceipt(receipts, request.RequestId) == null)
                .ToList();
            await WriteInteractionRequestsAsync(fs, remaining);
        }
        catch
        {
            // keep request until resident state is readable again
        }
    }

    private static async Task EnsureManifestationRequestsHealthyAsync(
        FileSystemManager fs,
        IReadOnlyList<PendingResidentCompanionManifestationRequest> requests,
        string? currentRealm)
    {
        if (!IsMortalRealm(currentRealm))
        {
            ClearManifestationRequest(fs);
            return;
        }

        var soulJson = await fs.ReadFileAsync("game_state/meta/soul_state.json");
        if (string.IsNullOrWhiteSpace(soulJson))
            return;

        try
        {
            if (JsonNode.Parse(soulJson) is not JsonObject soulRoot)
                return;

            var currentIncarnation = GetNodeInt(soulRoot["currentIncarnation"]);

            var validForIncarnation = requests
                .Where(request =>
                    !string.IsNullOrWhiteSpace(request.RequestId) &&
                    !string.IsNullOrWhiteSpace(request.RelicId) &&
                    request.TargetIncarnation == currentIncarnation)
                .ToList();

            if (validForIncarnation.Count == 0)
            {
                ClearManifestationRequest(fs);
                return;
            }

            var npcJson = await fs.ReadFileAsync("game_state/npcs/npc_core.json");
            if (string.IsNullOrWhiteSpace(npcJson))
            {
                await WriteManifestationRequestsAsync(fs, validForIncarnation);
                return;
            }

            try
            {
                using var npcDoc = JsonDocument.Parse(npcJson);
                var remaining = new List<PendingResidentCompanionManifestationRequest>();
                var soulChanged = false;
                var matchedNpcIds = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
                foreach (var request in validForIncarnation)
                {
                    if (TryFindManifestedNpc(npcDoc.RootElement, validForIncarnation, request, matchedNpcIds, out var manifestedNpc))
                    {
                        soulChanged |= MarkManifestationResolved(soulRoot, request, manifestedNpc);
                        continue;
                    }

                    remaining.Add(request);
                }

                if (soulChanged)
                    await fs.WriteFileAtomicAsync("game_state/meta/soul_state.json", soulRoot.ToJsonString(JsonOpts));

                await WriteManifestationRequestsAsync(fs, remaining);
            }
            catch
            {
                // keep requests until npc state is readable again
            }

            return;
        }
        catch
        {
            return;
        }
    }

    private static bool TryFindManifestedNpc(
        JsonElement root,
        IReadOnlyList<PendingResidentCompanionManifestationRequest> requests,
        PendingResidentCompanionManifestationRequest request,
        HashSet<string> matchedNpcIds,
        out JsonElement manifestedNpc)
    {
        foreach (var npc in EnumerateNpcObjects(root))
        {
            var npcKey = BuildNpcMatchKey(npc);
            if (!string.IsNullOrWhiteSpace(npcKey) && matchedNpcIds.Contains(npcKey))
                continue;

            var sourceRelicId = GetString(npc, "sourceCompanionRelicId");
            if (!string.IsNullOrWhiteSpace(sourceRelicId) &&
                string.Equals(sourceRelicId, request.RelicId, StringComparison.OrdinalIgnoreCase))
            {
                if (!string.IsNullOrWhiteSpace(npcKey))
                    matchedNpcIds.Add(npcKey);
                manifestedNpc = npc;
                return true;
            }
        }

        if (!HasUniqueFallbackIdentity(requests, request))
        {
            manifestedNpc = default;
            return false;
        }

        foreach (var npc in EnumerateNpcObjects(root))
        {
            var npcKey = BuildNpcMatchKey(npc);
            if (!string.IsNullOrWhiteSpace(npcKey) && matchedNpcIds.Contains(npcKey))
                continue;

            var sourceResidentId = GetString(npc, "sourceAfterlifeResidentId");
            var sourceImprintId = GetString(npc, "sourceSoulImprintId");
            var matchedByFallback =
                !string.IsNullOrWhiteSpace(request.SourceImprintId) &&
                string.Equals(sourceImprintId, request.SourceImprintId, StringComparison.OrdinalIgnoreCase);
            matchedByFallback |=
                !string.IsNullOrWhiteSpace(request.SourceResidentId) &&
                string.Equals(sourceResidentId, request.SourceResidentId, StringComparison.OrdinalIgnoreCase);
            if (!matchedByFallback)
                continue;

            if (!string.IsNullOrWhiteSpace(npcKey))
                matchedNpcIds.Add(npcKey);
            manifestedNpc = npc;
            return true;
        }

        manifestedNpc = default;
        return false;
    }

    private static bool HasUniqueFallbackIdentity(
        IReadOnlyList<PendingResidentCompanionManifestationRequest> requests,
        PendingResidentCompanionManifestationRequest request)
    {
        if (!string.IsNullOrWhiteSpace(request.SourceImprintId))
        {
            return requests.Count(candidate =>
                string.Equals(candidate.SourceImprintId, request.SourceImprintId, StringComparison.OrdinalIgnoreCase)) == 1;
        }

        if (!string.IsNullOrWhiteSpace(request.SourceResidentId))
        {
            return requests.Count(candidate =>
                string.Equals(candidate.SourceResidentId, request.SourceResidentId, StringComparison.OrdinalIgnoreCase)) == 1;
        }

        return false;
    }

    private static string BuildNpcMatchKey(JsonElement npc)
    {
        var npcId = GetString(npc, "NPCId");
        if (string.IsNullOrWhiteSpace(npcId))
            npcId = GetString(npc, "npcId");
        if (string.IsNullOrWhiteSpace(npcId))
            npcId = GetString(npc, "id");
        if (!string.IsNullOrWhiteSpace(npcId))
            return $"npc:{npcId}";

        var sourceRelicId = GetString(npc, "sourceCompanionRelicId");
        if (!string.IsNullOrWhiteSpace(sourceRelicId))
            return $"relic:{sourceRelicId}";

        var sourceImprintId = GetString(npc, "sourceSoulImprintId");
        if (!string.IsNullOrWhiteSpace(sourceImprintId))
            return $"imprint:{sourceImprintId}";

        var sourceResidentId = GetString(npc, "sourceAfterlifeResidentId");
        if (!string.IsNullOrWhiteSpace(sourceResidentId))
            return $"resident:{sourceResidentId}";

        return string.Empty;
    }

    private static IEnumerable<JsonElement> EnumerateNpcObjects(JsonElement root)
    {
        if (root.ValueKind == JsonValueKind.Array)
        {
            foreach (var npc in root.EnumerateArray())
            {
                if (npc.ValueKind == JsonValueKind.Object)
                    yield return npc;
            }
            yield break;
        }

        if (root.ValueKind != JsonValueKind.Object)
            yield break;

        foreach (var propertyName in new[] { "UpdateNPCs", "NPCsInScene", "npcs", "npcDataChanges" })
        {
            if (!root.TryGetProperty(propertyName, out var array) || array.ValueKind != JsonValueKind.Array)
                continue;

            foreach (var npc in array.EnumerateArray())
            {
                if (npc.ValueKind == JsonValueKind.Object)
                    yield return npc;
            }
        }
    }

    private static bool IsEligibleCompanionEchoRelic(JsonObject relic, int currentIncarnation, HashSet<string> existingRelicIds)
    {
        var element = ToElement(relic);
        if (!GuardianAbodeResidentState.IsCompanionEchoRelic(element) && !GuardianAbodeResidentState.HasEmbeddedSoulImprint(element))
            return false;

        var lastRequested = GetNodeInt(relic["companionManifestationLastRequestedIncarnation"]);
        var relicId = GetNodeString(relic["relicId"]);
        return lastRequested != currentIncarnation &&
               !string.IsNullOrWhiteSpace(relicId) &&
               !existingRelicIds.Contains(relicId);
    }

    private static bool TryBuildManifestationRequest(
        JsonObject relic,
        int currentIncarnation,
        out PendingResidentCompanionManifestationRequest request)
    {
        request = new PendingResidentCompanionManifestationRequest
        {
            RelicId = GetNodeString(relic["relicId"]) ?? string.Empty,
            RelicName = GetNodeString(relic["name"]) ?? string.Empty,
            TargetIncarnation = currentIncarnation,
            SourceGuardianId = GetNodeString(relic["sourceGuardianId"]) ?? GetNodeString(relic["guardianId"]) ?? string.Empty,
            SourceGuardianName = GetNodeString(relic["sourceGuardianName"]) ?? GetNodeString(relic["guardianName"]) ?? string.Empty
        };

        if (relic["companionSeed"] is JsonObject companionSeed)
        {
            request.ManifestationSource = "resident_relic";
            request.SourceResidentId = GetNodeString(companionSeed["sourceResidentId"]) ?? string.Empty;
            request.SourceGuardianId = string.IsNullOrWhiteSpace(request.SourceGuardianId)
                ? GetNodeString(companionSeed["sourceGuardianId"]) ?? string.Empty
                : request.SourceGuardianId;
            request.CompanionNameHint = GetNodeString(companionSeed["companionNameHint"]) ?? string.Empty;
            request.OriginWorldSummary = GetNodeString(companionSeed["originWorldSummary"]) ?? string.Empty;
            request.FutureCompanionPrompt = GetNodeString(companionSeed["futureCompanionPrompt"]) ?? string.Empty;
            request.BondReason = GetNodeString(companionSeed["bondReason"]) ?? string.Empty;
            request.CoreTraits = ReadStringList(companionSeed["coreTraits"]);
            request.ArchetypeHints = ReadStringList(companionSeed["archetypeHints"]);
            request.AppearanceMotifs = ReadStringList(companionSeed["appearanceMotifs"]);
            return !string.IsNullOrWhiteSpace(request.SourceResidentId) &&
                   !string.IsNullOrWhiteSpace(request.CompanionNameHint);
        }

        if (TryGetEmbeddedSoulImprint(relic, out var soulImprint))
        {
            request.ManifestationSource = "imprint_relic";
            request.SourceImprintId = GetNodeString(soulImprint["imprintId"]) ??
                                      GetNodeString(soulImprint["id"]) ??
                                      $"imprint_{request.RelicId}";
            request.CompanionNameHint = GetNodeString(soulImprint["NPCName"]) ??
                                        GetNodeString(soulImprint["npcName"]) ??
                                        GetNodeString(soulImprint["name"]) ??
                                        GetNodeString(soulImprint["companionName"]) ??
                                        GetNodeString(soulImprint["originalName"]) ??
                                        request.RelicName;
            request.OriginWorldSummary = GetNodeString(soulImprint["description"]) ??
                                         GetNodeString(soulImprint["summary"]) ??
                                         GetNodeString(soulImprint["backgroundStory"]) ??
                                         GetNodeString(soulImprint["history"]) ??
                                         request.RelicName;
            request.FutureCompanionPrompt = GetNodeString(soulImprint["futureCompanionPrompt"]) ??
                                            request.OriginWorldSummary;
            request.BondReason = GetNodeString(soulImprint["bondReason"]) ?? string.Empty;
            request.CoreTraits = ReadStringList(soulImprint["coreTraitsPreserved"]);
            if (request.CoreTraits.Count == 0)
                request.CoreTraits = ReadStringList(soulImprint["coreTraits"]);
            if (request.CoreTraits.Count == 0)
                request.CoreTraits = ReadStringList(soulImprint["personalityTraits"]);
            return !string.IsNullOrWhiteSpace(request.SourceImprintId) &&
                   !string.IsNullOrWhiteSpace(request.CompanionNameHint);
        }

        return false;
    }

    private static bool TryGetEmbeddedSoulImprint(JsonObject relic, out JsonObject imprint)
    {
        if (relic["soulImprint"] is JsonObject soulImprint)
        {
            imprint = soulImprint;
            return true;
        }

        if (relic["npcSoulImprint"] is JsonObject npcSoulImprint)
        {
            imprint = npcSoulImprint;
            return true;
        }

        imprint = null!;
        return false;
    }

    private static bool MarkManifestationResolved(
        JsonObject soulRoot,
        PendingResidentCompanionManifestationRequest request,
        JsonElement manifestedNpc)
    {
        if (soulRoot["soulRelics"] is not JsonObject soulRelics)
            return false;

        var relic = FindRelicById(soulRelics["equipped"] as JsonArray, request.RelicId) ??
                    FindRelicById(soulRelics["stored"] as JsonArray, request.RelicId);
        if (relic == null)
            return false;

        var changed = false;
        changed |= SetNodeIfDifferent(relic, "companionManifestationStatus", "materialized");
        changed |= SetNodeIfDifferent(relic, "companionManifestationResolvedRequestId", request.RequestId);
        changed |= SetNodeIfDifferent(
            relic,
            "companionManifestationResolvedNpcId",
            GetString(manifestedNpc, "NPCId") is { Length: > 0 } legacyNpcId
                ? legacyNpcId
                : GetString(manifestedNpc, "npcId") is { Length: > 0 } npcId
                    ? npcId
                    : GetString(manifestedNpc, "id"));

        var resolvedAtTurn = TryResolveNpcTurn(manifestedNpc);
        if (resolvedAtTurn > 0)
            changed |= SetNodeIfDifferent(relic, "companionManifestationResolvedAtTurn", resolvedAtTurn);

        var resolvedAtUtc = TryResolveNpcTimestamp(manifestedNpc);
        if (string.IsNullOrWhiteSpace(resolvedAtUtc))
            resolvedAtUtc = DateTime.UtcNow.ToString("o");
        changed |= SetNodeIfDifferent(relic, "companionManifestationResolvedAtUtc", resolvedAtUtc);

        return changed;
    }

    private static JsonObject? FindRelicById(JsonArray? relics, string relicId)
    {
        if (relics == null || string.IsNullOrWhiteSpace(relicId))
            return null;

        return relics.OfType<JsonObject>()
            .FirstOrDefault(relic => string.Equals(GetNodeString(relic["relicId"]), relicId, StringComparison.OrdinalIgnoreCase));
    }

    private static int TryResolveNpcTurn(JsonElement npc)
    {
        foreach (var field in new[] { "introducedAtTurn", "createdAtTurn", "turn", "turnNumber" })
        {
            if (!npc.TryGetProperty(field, out var node))
                continue;

            if (node.ValueKind == JsonValueKind.Number && node.TryGetInt32(out var parsed))
                return Math.Max(0, parsed);

            if (node.ValueKind == JsonValueKind.String && int.TryParse(node.GetString(), out parsed))
                return Math.Max(0, parsed);
        }

        return 0;
    }

    private static string? TryResolveNpcTimestamp(JsonElement npc)
    {
        foreach (var field in new[] { "introducedAtUtc", "createdAtUtc", "timestamp", "lastUpdatedAt", "lastUpdatedUtc" })
        {
            if (!npc.TryGetProperty(field, out var node) || node.ValueKind != JsonValueKind.String)
                continue;

            var text = node.GetString();
            if (!string.IsNullOrWhiteSpace(text))
                return text;
        }

        return null;
    }

    private static bool SetNodeIfDifferent(JsonObject node, string propertyName, string? value)
    {
        var normalized = value ?? string.Empty;
        if (string.Equals(GetNodeString(node[propertyName]), normalized, StringComparison.Ordinal))
            return false;

        node[propertyName] = normalized;
        return true;
    }

    private static bool SetNodeIfDifferent(JsonObject node, string propertyName, int value)
    {
        if (GetNodeInt(node[propertyName]) == value)
            return false;

        node[propertyName] = value;
        return true;
    }

    private static JsonElement ToElement(JsonObject node)
    {
        using var doc = JsonDocument.Parse(node.ToJsonString());
        return doc.RootElement.Clone();
    }

    private static List<string> ReadStringList(JsonNode? node)
    {
        var result = new List<string>();
        if (node is not JsonArray arr)
            return result;

        foreach (var item in arr.OfType<JsonValue>())
        {
            if (!item.TryGetValue<string>(out var text) || string.IsNullOrWhiteSpace(text))
                continue;

            result.Add(text);
        }

        return result;
    }

    private static bool IsAfterlifeRealm(string? currentRealm) =>
        string.Equals(currentRealm, "Chaos Sea", StringComparison.OrdinalIgnoreCase) ||
        string.Equals(currentRealm, "Море Хаоса", StringComparison.OrdinalIgnoreCase) ||
        string.Equals(currentRealm, "Shining Abode", StringComparison.OrdinalIgnoreCase) ||
        string.Equals(currentRealm, "Сияющая Обитель", StringComparison.OrdinalIgnoreCase);

    private static bool IsMortalRealm(string? currentRealm) => !string.IsNullOrWhiteSpace(currentRealm) && !IsAfterlifeRealm(currentRealm);

    private static string GetString(JsonElement root, string propertyName)
    {
        if (!root.TryGetProperty(propertyName, out var node) || node.ValueKind != JsonValueKind.String)
            return string.Empty;

        return node.GetString() ?? string.Empty;
    }

    private static string? GetNodeString(JsonNode? node)
    {
        if (node is JsonValue value && value.TryGetValue<string>(out var text))
            return text;

        return null;
    }

    private static int GetNodeInt(JsonNode? node)
    {
        if (node is JsonValue value && value.TryGetValue<int>(out var number))
            return number;

        return 0;
    }
}
