using System.Text;
using System.Text.Json;
using System.Text.Json.Nodes;
using System.Text.Json.Serialization;
using BookOfEternityClient.Configuration;
using BookOfEternityClient.Core;

namespace BookOfEternityClient.Services;

internal static class ShiningCoreActionRequestState
{
    public const string PendingActionsRequestPath = "game_state/control/pending_shining_abode_actions.json";
    public const string RequestsProperty = "requests";

    public const string ActionTypeDiscoverNativeFaction = "discover_native_faction";
    public const string ActionTypeInvestInFaction = "invest_in_faction";
    public const string ActionTypeCompleteProject = "complete_project";
    public const string ActionTypeSupportProject = "support_project";
    public const string ActionTypeUnsupportProject = "unsupport_project";
    public const string ActionTypeRetireProject = "retire_project";
    public const string ActionTypeOpenGates = "open_gates";
    public const string ActionTypePrepareIncarnationPackage = "prepare_incarnation_package";
    public const string ActionTypePullRelicGacha = "pull_relic_gacha";
    public const string ActionTypeForgeRelicReshape = "forge_relic.reshape";
    public const string ActionTypeForgeRelicRetuneProperty = "forge_relic.retune_property";
    public const string ActionTypeForgeRelicStrengthenBand = "forge_relic.strengthen_band";
    public const string ActionTypeForgeRelicStabilizeEcho = "forge_relic.stabilize_echo";
    public const string ActionTypeForgeRelicUpliftRarity = "forge_relic.uplift_rarity";

    public const string RequestStatusAccepted = "accepted";
    public const string RequestStatusRefused = "refused";
    public const string RequestStatusWithdrawn = "withdrawn";

    private static readonly JsonSerializerOptions JsonOpts = SharedJsonOptions.PrettyCamelCaseUnsafeRelaxed;

    internal sealed record PendingCoreRequestReadState(
        bool FilePresent,
        bool IsMalformed,
        IReadOnlyList<PendingShiningCoreActionRequest> Requests);

    private static readonly HashSet<string> AllowedActionTypes = new(StringComparer.OrdinalIgnoreCase)
    {
        ActionTypeDiscoverNativeFaction,
        ActionTypeInvestInFaction,
        ActionTypeCompleteProject,
        ActionTypeSupportProject,
        ActionTypeUnsupportProject,
        ActionTypeRetireProject,
        ActionTypeOpenGates,
        ActionTypePrepareIncarnationPackage,
        ActionTypePullRelicGacha,
        ActionTypeForgeRelicReshape,
        ActionTypeForgeRelicRetuneProperty,
        ActionTypeForgeRelicStrengthenBand,
        ActionTypeForgeRelicStabilizeEcho,
        ActionTypeForgeRelicUpliftRarity
    };

    private static readonly HashSet<string> AllowedStatuses = new(StringComparer.OrdinalIgnoreCase)
    {
        RequestStatusAccepted,
        RequestStatusRefused,
        RequestStatusWithdrawn
    };

    public sealed class PendingShiningCoreActionRequest
    {
        [JsonPropertyName("requestId")]
        public string RequestId { get; set; } = $"shining_core_action_{Guid.NewGuid():N}";

        [JsonPropertyName("actionType")]
        public string ActionType { get; set; } = "";

        [JsonPropertyName("factionId")]
        public string FactionId { get; set; } = "";

        [JsonPropertyName("factionName")]
        public string FactionName { get; set; } = "";

        [JsonPropertyName("projectId")]
        public string ProjectId { get; set; } = "";

        [JsonPropertyName("projectDisplayName")]
        public string ProjectDisplayName { get; set; } = "";

        [JsonPropertyName("projectDraft")]
        public JsonObject? ProjectDraft { get; set; }

        [JsonPropertyName("radianceTierAtRequest")]
        public int RadianceTierAtRequest { get; set; }

        [JsonPropertyName("quotedCostFeathers")]
        public int QuotedCostFeathers { get; set; }

        [JsonPropertyName("quotedCostLightSparks")]
        public int QuotedCostLightSparks { get; set; }

        [JsonPropertyName("sourceDraftVersion")]
        public int SourceDraftVersion { get; set; }

        [JsonPropertyName("selectedCardIds")]
        public List<string> SelectedCardIds { get; set; } = new();

        [JsonPropertyName("selectedCards")]
        public JsonArray? SelectedCards { get; set; }

        [JsonPropertyName("returnCycleId")]
        public string ReturnCycleId { get; set; } = "";

        [JsonPropertyName("projectedGachaBonusSteps")]
        public int ProjectedGachaBonusSteps { get; set; }

        [JsonPropertyName("relicId")]
        public string RelicId { get; set; } = "";

        [JsonPropertyName("relicName")]
        public string RelicName { get; set; } = "";

        [JsonPropertyName("targetFormTag")]
        public string TargetFormTag { get; set; } = "";

        [JsonPropertyName("propertyIndex")]
        public int PropertyIndex { get; set; } = -1;

        [JsonPropertyName("replacementProperty")]
        public JsonObject? ReplacementProperty { get; set; }

        [JsonPropertyName("addedProperties")]
        public JsonArray? AddedProperties { get; set; }

        [JsonPropertyName("createdAtTurn")]
        public int CreatedAtTurn { get; set; }

        [JsonPropertyName("createdAtUtc")]
        public string CreatedAtUtc { get; set; } = DateTime.UtcNow.ToString("o");
    }

    public static bool IsSupportedActionType(string? value) => !string.IsNullOrWhiteSpace(value) && AllowedActionTypes.Contains(value);
    public static bool IsSupportedStatus(string? value) => !string.IsNullOrWhiteSpace(value) && AllowedStatuses.Contains(value);

    public static async Task<string?> ValidateRequestAgainstCurrentStateAsync(
        FileSystemManager fs,
        PendingShiningCoreActionRequest request)
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
            return "pending_shining_abode_actions.json повреждён. Исправьте или очистите pending core-action contract перед созданием нового запроса.";

        ShiningAbodeState.NormalizeStateRoot(shiningRoot, residentRoot, guardiansRoot);
        var ordinaryModeError = ValidateOrdinaryActiveShiningMode(soulRoot, shiningRoot);
        if (ordinaryModeError != null)
            return ordinaryModeError;

        if (!IsSupportedActionType(request.ActionType))
            return "actionType использует неподдерживаемое значение.";

        var existingRequests = pendingState.Requests;
        if (existingRequests.Count > 0)
        {
            var sameRequest = existingRequests[0];
            if (!string.Equals(sameRequest.RequestId, request.RequestId, StringComparison.OrdinalIgnoreCase))
                return "Пока не разрешён предыдущий pending Shining core action request.";
        }

        return request.ActionType.Trim().ToLowerInvariant() switch
        {
            ActionTypeDiscoverNativeFaction => await ValidateDiscoverNativeFactionRequestAsync(fs, request, shiningRoot),
            ActionTypeInvestInFaction => await ValidateFactionInvestmentRequestAsync(fs, request, shiningRoot, residentRoot),
            ActionTypeCompleteProject => await ValidateCompleteProjectRequestAsync(fs, request, shiningRoot, residentRoot),
            ActionTypeSupportProject => await ValidateSupportToggleRequestAsync(request, shiningRoot, support: true),
            ActionTypeUnsupportProject => await ValidateSupportToggleRequestAsync(request, shiningRoot, support: false),
            ActionTypeRetireProject => await ValidateRetireProjectRequestAsync(request, shiningRoot, residentRoot),
            ActionTypeOpenGates => await ValidateOpenGatesRequestAsync(request, shiningRoot, residentRoot),
            ActionTypePrepareIncarnationPackage => await ValidatePreparePackageRequestAsync(request, shiningRoot),
            ActionTypePullRelicGacha => await ValidateRelicGachaPullRequestAsync(fs, request, shiningRoot, residentRoot),
            ActionTypeForgeRelicReshape or
            ActionTypeForgeRelicRetuneProperty or
            ActionTypeForgeRelicStrengthenBand or
            ActionTypeForgeRelicStabilizeEcho or
            ActionTypeForgeRelicUpliftRarity => await ValidateForgeActionRequestAsync(fs, request, shiningRoot, residentRoot),
            _ => "actionType использует неподдерживаемое значение."
        };
    }

    public static async Task<IReadOnlyList<PendingShiningCoreActionRequest>> ReadRequestsAsync(FileSystemManager fs)
        => (await ReadRequestsStateAsync(fs)).Requests;

    public static IReadOnlyList<PendingShiningCoreActionRequest> ReadRequests(string? json)
        => AnalyzeRequests(json, !string.IsNullOrEmpty(json)).Requests;

    internal static async Task<PendingCoreRequestReadState> ReadRequestsStateAsync(FileSystemManager fs)
    {
        var json = await fs.ReadFileAsync(PendingActionsRequestPath);
        return AnalyzeRequests(json, fs.FileExists(PendingActionsRequestPath));
    }

    private static PendingCoreRequestReadState AnalyzeRequests(string? json, bool filePresent)
    {
        if (string.IsNullOrWhiteSpace(json))
            return new PendingCoreRequestReadState(filePresent, filePresent, Array.Empty<PendingShiningCoreActionRequest>());

        try
        {
            using var doc = JsonDocument.Parse(json);
            if (doc.RootElement.ValueKind != JsonValueKind.Object ||
                !doc.RootElement.TryGetProperty(RequestsProperty, out var requestsNode) ||
                requestsNode.ValueKind != JsonValueKind.Array)
            {
                return new PendingCoreRequestReadState(filePresent, true, Array.Empty<PendingShiningCoreActionRequest>());
            }

            var result = new List<PendingShiningCoreActionRequest>();
            foreach (var requestNode in requestsNode.EnumerateArray())
            {
                var request = JsonSerializer.Deserialize<PendingShiningCoreActionRequest>(requestNode.GetRawText(), JsonOpts);
                if (request == null)
                    return new PendingCoreRequestReadState(filePresent, true, Array.Empty<PendingShiningCoreActionRequest>());

                result.Add(request);
            }

            return new PendingCoreRequestReadState(filePresent, false, result);
        }
        catch
        {
            return new PendingCoreRequestReadState(filePresent, true, Array.Empty<PendingShiningCoreActionRequest>());
        }
    }

    public static async Task WriteRequestAsync(FileSystemManager fs, PendingShiningCoreActionRequest request)
    {
        var existingState = await ReadRequestsStateAsync(fs);
        if (existingState.IsMalformed)
            throw new InvalidOperationException("pending_shining_abode_actions.json повреждён и должен быть исправлен или очищен до записи нового core action request.");

        await fs.WriteFileAtomicAsync(PendingActionsRequestPath, JsonSerializer.Serialize(new Dictionary<string, object?>
        {
            [RequestsProperty] = new[] { request }
        }, JsonOpts));
    }

    public static bool TryBuildProjectedShiningRootForPreview(
        PendingShiningCoreActionRequest request,
        JsonObject shiningRoot,
        JsonObject? residentRoot,
        out JsonObject projectedRoot)
    {
        projectedRoot = JsonNode.Parse(shiningRoot.ToJsonString())!.AsObject();
        var projectedResidents = residentRoot == null ? null : JsonNode.Parse(residentRoot.ToJsonString())!.AsObject();

        switch ((request.ActionType ?? string.Empty).Trim().ToLowerInvariant())
        {
            case ActionTypeInvestInFaction:
                return ShiningAbodeState.TryInvestInFaction(projectedRoot, projectedResidents, request.FactionId, out _);

            case ActionTypeCompleteProject:
                return request.ProjectDraft != null &&
                       ShiningAbodeState.TryCompleteProject(
                           projectedRoot,
                           projectedResidents,
                           request.FactionId,
                           request.ProjectDraft.DeepClone().AsObject(),
                           request.CreatedAtTurn,
                           projectIdOverride: null,
                           completedAtUtc: request.CreatedAtUtc,
                           out _,
                           out _);

            case ActionTypeSupportProject:
                return ShiningAbodeState.TrySupportProject(projectedRoot, request.FactionId, request.ProjectId, out _);

            case ActionTypeUnsupportProject:
                return ShiningAbodeState.TryUnsupportProject(projectedRoot, request.FactionId, request.ProjectId, out _);

            case ActionTypeRetireProject:
                return ShiningAbodeState.TryRetireProject(projectedRoot, projectedResidents, request.FactionId, request.ProjectId, out _);

            case ActionTypeOpenGates:
                return ShiningAbodeState.TryOpenGates(projectedRoot, projectedResidents, out _);

            case ActionTypePrepareIncarnationPackage:
                if (projectedRoot["gates"] is JsonObject gates)
                {
                    gates["selectedBlessingCardIds"] = new JsonArray(
                        request.SelectedCardIds
                            .Where(id => !string.IsNullOrWhiteSpace(id))
                            .Select(id => (JsonNode?)id.Trim())
                            .ToArray());
                }

                return ShiningAbodeState.TryPrepareIncarnationPackage(projectedRoot, request.CreatedAtTurn, out _);

            default:
                return false;
        }
    }

    public static void ClearRequests(FileSystemManager fs) => fs.DeleteFile(PendingActionsRequestPath);

    public static async Task EnsureHealthyAsync(FileSystemManager fs, string? currentRealm)
    {
        if (!RealmSemantics.HasResolvedRealm(currentRealm))
            return;

        if (!IsShiningRealm(currentRealm))
        {
            ClearRequests(fs);
            return;
        }

        var shiningRoot = await ReadJsonObjectAsync(fs, ShiningAbodeState.StatePath);
        if (shiningRoot == null)
            return;
        if (ShiningAbodeState.ValidateRawOwnerStateForActionableMode(shiningRoot) != null)
            return;

        if (!string.Equals(GetNodeString(shiningRoot["availability"]), ShiningAbodeState.AvailabilityActive, StringComparison.OrdinalIgnoreCase))
        {
            ClearRequests(fs);
            return;
        }
        if (ShiningAbodeState.GetPreparedIncarnationPackageMode(shiningRoot) != ShiningAbodeState.PreparedIncarnationPackageMode.Absent)
            return;

        var requestState = await ReadRequestsStateAsync(fs);
        if (requestState.IsMalformed)
            return;

        var requests = requestState.Requests;
        if (requests.Count == 0)
            return;

        if (requests.Count > 1)
            return;

        if (shiningRoot["coreActionReceipts"] is not JsonArray receipts)
            return;

        if (HasMatchingCoreActionClosure(shiningRoot, receipts, requests[0]))
            ClearRequests(fs);
    }

    public static async Task<string?> BuildSystemReminderFragmentAsync(FileSystemManager fs, string? currentRealm)
    {
        if (!RealmSemantics.IsAfterlifeRealm(currentRealm))
            return null;

        var packageMode = ShiningAbodeState.PreparedIncarnationPackageMode.Absent;
        if (IsShiningRealm(currentRealm))
        {
            var handoffRoot = await ReadJsonObjectAsync(fs, ShiningAbodeState.StatePath);
            packageMode = ShiningAbodeState.GetPreparedIncarnationPackageMode(handoffRoot);
        }

        var requestState = await ReadRequestsStateAsync(fs);
        if (requestState.IsMalformed)
        {
            var sb = new StringBuilder();
            sb.AppendLine("SHINING ABODE CORE ACTION CORRUPTION:");
            sb.AppendLine("  - pending_shining_abode_actions.json unreadable or malformed.");
            sb.AppendLine("  - Preserve the file and repair the pending core-action contract before authoring a new request.");
            return sb.ToString();
        }

        var requests = requestState.Requests;
        if (IsShiningRealm(currentRealm) &&
            packageMode != ShiningAbodeState.PreparedIncarnationPackageMode.Absent)
        {
            if (requests.Count == 0)
            {
                return packageMode == ShiningAbodeState.PreparedIncarnationPackageMode.InvalidFault
                    ? "SHINING ABODE CORE ACTIONS BLOCKED:\n" +
                      "  - preparedIncarnationPackage is malformed or fails bootstrap validation, so the realm mode is fail-closed.\n" +
                      "  - Do not process ordinary Shining core actions until the package is repaired or cleared by valid runtime flow."
                    : null;
            }

            var blocked = new StringBuilder();
            blocked.AppendLine("SHINING ABODE CORE ACTIONS BLOCKED:");
            blocked.AppendLine(packageMode == ShiningAbodeState.PreparedIncarnationPackageMode.ValidHandoff
                ? "  - Valid preparedIncarnationPackage puts the realm in pending-bootstrap handoff mode."
                : "  - preparedIncarnationPackage is malformed or fails bootstrap validation, so the realm mode is fail-closed.");
            blocked.AppendLine("  - Preserve pending_shining_abode_actions.json; do not delete, truncate, or process ordinary Shining core actions during this mode.");
            blocked.AppendLine($"  - Pending requests detected: {requests.Count}");
            AppendSerializedJsonBlock(blocked, "Blocked pending core-action DTOs", requests);
            return blocked.ToString();
        }

        if (requests.Count > 1)
        {
            var sb = new StringBuilder();
            sb.AppendLine("SHINING ABODE CORE ACTION CORRUPTION:");
            sb.AppendLine("  - pending_shining_abode_actions.json contains multiple pending requests and is malformed.");
            sb.AppendLine("  - Do not auto-select, truncate, or resolve only the first request.");
            sb.AppendLine("  - Preserve the file and surface validation/repair for shining_core_action_multiple_pending_requests.");
            sb.AppendLine($"  - Pending requests detected: {requests.Count}");
            AppendSerializedJsonBlock(sb, "Full pending core-action DTOs", requests);
            return sb.ToString();
        }

        if (requests.Count == 1)
        {
            var request = requests[0];
            var sb = new StringBuilder();
            sb.AppendLine("SHINING ABODE CORE ACTION:");
            sb.AppendLine("  - Treat this as a client-authored canonical action contract, not as optional prose.");
            sb.AppendLine("  - Resolve it in accepted turn through shining_abode_state mutation plus matching coreActionReceipts[].");
            sb.AppendLine($"  - Pending action: {request.ActionType}");
            if (!string.IsNullOrWhiteSpace(request.FactionName) || !string.IsNullOrWhiteSpace(request.FactionId))
                sb.AppendLine($"  - Faction: {request.FactionName} ({request.FactionId})".Replace(" ()", string.Empty, StringComparison.Ordinal));
            if (!string.IsNullOrWhiteSpace(request.ProjectDisplayName) || !string.IsNullOrWhiteSpace(request.ProjectId))
                sb.AppendLine($"  - Project: {request.ProjectDisplayName} ({request.ProjectId})".Replace(" ()", string.Empty, StringComparison.Ordinal));
            if (!string.IsNullOrWhiteSpace(request.RelicName) || !string.IsNullOrWhiteSpace(request.RelicId))
                sb.AppendLine($"  - Relic: {request.RelicName} ({request.RelicId})".Replace(" ()", string.Empty, StringComparison.Ordinal));
            if (!string.IsNullOrWhiteSpace(request.ReturnCycleId))
                sb.AppendLine($"  - Return cycle: {request.ReturnCycleId}");
            if (request.QuotedCostFeathers > 0 || request.QuotedCostLightSparks > 0)
                sb.AppendLine($"  - Quoted cost: {request.QuotedCostFeathers} feathers / {request.QuotedCostLightSparks} light sparks.");
            if (request.ActionType.Equals(ActionTypePrepareIncarnationPackage, StringComparison.OrdinalIgnoreCase))
            {
                sb.AppendLine($"  - Draft version: {request.SourceDraftVersion}");
                sb.AppendLine($"  - Selected cards: {request.SelectedCardIds.Count}");
            }
            else if (request.ActionType.Equals(ActionTypePullRelicGacha, StringComparison.OrdinalIgnoreCase))
            {
                sb.AppendLine($"  - Projected bonus steps: {request.ProjectedGachaBonusSteps}");
                sb.AppendLine("  - Use turn_request.gachaBaseResult.baseRarity as the minimum rarity floor for this Shining banner pull.");
            }
            else if (ShiningAbodeState.IsForgeActionType(request.ActionType))
            {
                if (!string.IsNullOrWhiteSpace(request.TargetFormTag))
                    sb.AppendLine($"  - Target formTag: {request.TargetFormTag}");
                if (request.PropertyIndex >= 0)
                    sb.AppendLine($"  - Property index: {request.PropertyIndex}");
            }

            AppendSerializedJsonBlock(sb, "Full pending core-action DTO", request);
            return sb.ToString();
        }

        if (!string.Equals(currentRealm, "Shining Abode", StringComparison.OrdinalIgnoreCase) &&
            !string.Equals(currentRealm, "Сияющая Обитель", StringComparison.OrdinalIgnoreCase))
        {
            return null;
        }

        var shiningRoot = await ReadJsonObjectAsync(fs, ShiningAbodeState.StatePath);
        if (shiningRoot == null)
            return null;

        var residentRoot = await ReadJsonObjectAsync(fs, GuardianAbodeResidentState.StatePath);
        ShiningAbodeState.NormalizeStateRoot(shiningRoot, residentRoot);
        var lines = new List<string>();
        if (shiningRoot["gates"] is JsonObject gates &&
            GetNodeBool(gates["hasOpenDraft"]) &&
            GetNodeBool(gates["isStale"]))
        {
            lines.Add("  - Gates draft is stale and needs a new open_gates before package preparation.");
        }

        var factions = ShiningAbodeState.EnsureFactionsArray(shiningRoot).OfType<JsonObject>().ToList();
        if (factions.Count > 0)
        {
            if (factions.All(faction => !ShiningAbodeState.FactionHasAvailableTrade(faction)))
                lines.Add("  - Trade is dormant across all current Shining factions.");

            var forgeReadyFactions = factions
                .Where(faction => ShiningAbodeState.FactionHasSupportedProjectArchetype(faction, ShiningAbodeState.ProjectArchetypeRefinement))
                .OrderByDescending(faction => GetNodeInt(faction["factionStrength"]))
                .ToList();
            if (forgeReadyFactions.Count == 0)
            {
                lines.Add("  - Forge is currently unavailable: no faction has a supported refinement project.");
            }
            else
            {
                var strongest = forgeReadyFactions[0];
                var factionName = GetNodeString(strongest["charter"]?["factionName"]) ??
                                  GetNodeString(strongest["factionId"]) ??
                                  "faction";
                lines.Add($"  - Strongest forge-ready faction: {factionName} (tradeTier {ShiningAbodeState.GetTradeTier(GetNodeInt(strongest["factionStrength"]))}, stock {ShiningAbodeState.GetTradeStockItemCount(strongest, residentRoot)}).");
            }
        }

        return lines.Count == 0
            ? null
            : "SHINING ABODE STATE:\n" + string.Join('\n', lines);
    }

    private static void AppendSerializedJsonBlock(StringBuilder sb, string title, object payload)
    {
        sb.AppendLine($"  - {title}:");
        var json = JsonSerializer.Serialize(payload, JsonOpts).Replace("\r\n", "\n", StringComparison.Ordinal);
        foreach (var line in json.Split('\n'))
            sb.AppendLine($"    {line}");
    }

    private static async Task<string?> ValidateDiscoverNativeFactionRequestAsync(
        FileSystemManager fs,
        PendingShiningCoreActionRequest request,
        JsonObject shiningRoot)
    {
        if (shiningRoot["pendingNativeFactionDiscovery"] is JsonObject)
            return "pendingNativeFactionDiscovery уже существует.";

        var radianceTier = GetNodeInt(shiningRoot["radiance"]?["tier"]);
        if (radianceTier < 1)
            return "Открытие нативной фракции доступно только с Radiance tier 1 или выше.";

        var discoveryCost = ShiningAbodeState.GetNativeDiscoveryCost();
        if (request.QuotedCostFeathers != discoveryCost.Feathers || request.QuotedCostLightSparks != discoveryCost.LightSparks)
            return "Quoted cost для discover_native_faction не совпадает с canonical стоимостью.";
        if (request.RadianceTierAtRequest != radianceTier)
            return "radianceTierAtRequest должен совпадать с текущим canonical radiance tier.";

        var currentFeathers = await ReadCurrentInkFeathersAsync(fs);
        if (currentFeathers < discoveryCost.Feathers)
            return $"Недостаточно Перьев. Нужно {discoveryCost.Feathers}.";
        if (GetNodeInt(shiningRoot["lightSparks"]) < discoveryCost.LightSparks)
            return $"Недостаточно Искр Света. Нужно {discoveryCost.LightSparks}.";

        return null;
    }

    private static async Task<string?> ValidateFactionInvestmentRequestAsync(
        FileSystemManager fs,
        PendingShiningCoreActionRequest request,
        JsonObject shiningRoot,
        JsonObject? residentRoot)
    {
        if (string.IsNullOrWhiteSpace(request.FactionId))
            return "invest_in_faction требует factionId.";

        var cost = ShiningAbodeState.GetFactionInvestmentCost();
        if (request.QuotedCostFeathers != cost.Feathers || request.QuotedCostLightSparks != cost.LightSparks)
            return "Quoted cost для invest_in_faction не совпадает с canonical стоимостью.";

        if (await ReadCurrentInkFeathersAsync(fs) < cost.Feathers)
            return $"Недостаточно Перьев. Нужно {cost.Feathers}.";

        var cloneRoot = JsonNode.Parse(shiningRoot.ToJsonString())!.AsObject();
        var cloneResidents = residentRoot == null ? null : JsonNode.Parse(residentRoot.ToJsonString())!.AsObject();
        return ShiningAbodeState.TryInvestInFaction(cloneRoot, cloneResidents, request.FactionId, out var error)
            ? null
            : error ?? "Не удалось создать canonical invest_in_faction request.";
    }

    private static async Task<string?> ValidateCompleteProjectRequestAsync(
        FileSystemManager fs,
        PendingShiningCoreActionRequest request,
        JsonObject shiningRoot,
        JsonObject? residentRoot)
    {
        if (string.IsNullOrWhiteSpace(request.FactionId))
            return "complete_project требует factionId.";
        if (request.ProjectDraft == null)
            return "complete_project требует projectDraft.";

        if (await ReadCurrentInkFeathersAsync(fs) < request.QuotedCostFeathers)
            return $"Недостаточно Перьев. Нужно {request.QuotedCostFeathers}.";

        var cloneRoot = JsonNode.Parse(shiningRoot.ToJsonString())!.AsObject();
        var cloneResidents = residentRoot == null ? null : JsonNode.Parse(residentRoot.ToJsonString())!.AsObject();
        if (!ShiningAbodeState.TryQuoteProjectCompletion(
                cloneRoot,
                cloneResidents,
                request.FactionId,
                request.ProjectDraft.DeepClone().AsObject(),
                out var quotedCost,
                out var error))
        {
            return error ?? "Проект не прошёл canonical quote validation.";
        }

        if (quotedCost.Feathers != request.QuotedCostFeathers || quotedCost.LightSparks != request.QuotedCostLightSparks)
            return "Quoted cost для complete_project не совпадает с canonical quote.";

        return null;
    }

    private static Task<string?> ValidateSupportToggleRequestAsync(
        PendingShiningCoreActionRequest request,
        JsonObject shiningRoot,
        bool support)
    {
        var zeroCostError = ValidateZeroCostRequest(request, support ? ActionTypeSupportProject : ActionTypeUnsupportProject);
        if (zeroCostError != null)
            return Task.FromResult<string?>(zeroCostError);

        if (string.IsNullOrWhiteSpace(request.FactionId) || string.IsNullOrWhiteSpace(request.ProjectId))
            return Task.FromResult<string?>("Project support mutation требует factionId и projectId.");

        var cloneRoot = JsonNode.Parse(shiningRoot.ToJsonString())!.AsObject();
        var success = support
            ? ShiningAbodeState.TrySupportProject(cloneRoot, request.FactionId, request.ProjectId, out var error)
            : ShiningAbodeState.TryUnsupportProject(cloneRoot, request.FactionId, request.ProjectId, out error);
        return Task.FromResult(success ? null : error ?? "Project support mutation не прошла canonical validation.");
    }

    private static Task<string?> ValidateRetireProjectRequestAsync(
        PendingShiningCoreActionRequest request,
        JsonObject shiningRoot,
        JsonObject? residentRoot)
    {
        var zeroCostError = ValidateZeroCostRequest(request, ActionTypeRetireProject);
        if (zeroCostError != null)
            return Task.FromResult<string?>(zeroCostError);

        if (string.IsNullOrWhiteSpace(request.FactionId) || string.IsNullOrWhiteSpace(request.ProjectId))
            return Task.FromResult<string?>("retire_project требует factionId и projectId.");

        var cloneRoot = JsonNode.Parse(shiningRoot.ToJsonString())!.AsObject();
        var cloneResidents = residentRoot == null ? null : JsonNode.Parse(residentRoot.ToJsonString())!.AsObject();
        var success = ShiningAbodeState.TryRetireProject(cloneRoot, cloneResidents, request.FactionId, request.ProjectId, out var error);
        return Task.FromResult(success ? null : error ?? "retire_project не прошёл canonical validation.");
    }

    private static Task<string?> ValidateOpenGatesRequestAsync(
        PendingShiningCoreActionRequest request,
        JsonObject shiningRoot,
        JsonObject? residentRoot)
    {
        var zeroCostError = ValidateZeroCostRequest(request, ActionTypeOpenGates);
        if (zeroCostError != null)
            return Task.FromResult<string?>(zeroCostError);

        var cloneRoot = JsonNode.Parse(shiningRoot.ToJsonString())!.AsObject();
        var cloneResidents = residentRoot == null ? null : JsonNode.Parse(residentRoot.ToJsonString())!.AsObject();
        var success = ShiningAbodeState.TryOpenGates(cloneRoot, cloneResidents, out var error);
        return Task.FromResult(success ? null : error ?? "open_gates не прошёл canonical validation.");
    }

    private static Task<string?> ValidatePreparePackageRequestAsync(
        PendingShiningCoreActionRequest request,
        JsonObject shiningRoot)
    {
        var zeroCostError = ValidateZeroCostRequest(request, ActionTypePrepareIncarnationPackage);
        if (zeroCostError != null)
            return Task.FromResult<string?>(zeroCostError);

        if (request.SourceDraftVersion <= 0)
            return Task.FromResult<string?>("prepare_incarnation_package требует sourceDraftVersion.");

        var selectedCardIds = request.SelectedCardIds
            .Where(id => !string.IsNullOrWhiteSpace(id))
            .Select(id => id.Trim())
            .ToList();
        if (selectedCardIds.Count == 0)
            return Task.FromResult<string?>("prepare_incarnation_package требует минимум одну выбранную карту.");
        if (selectedCardIds.Count != selectedCardIds.Distinct(StringComparer.OrdinalIgnoreCase).Count())
            return Task.FromResult<string?>("prepare_incarnation_package не допускает duplicate selectedCardIds.");

        var cloneRoot = JsonNode.Parse(shiningRoot.ToJsonString())!.AsObject();
        if (cloneRoot["gates"] is not JsonObject gates)
            return Task.FromResult<string?>("gates недоступны.");
        if (!SelectedCardSnapshotMatchesDraft(request.SelectedCards, selectedCardIds, gates))
            return Task.FromResult<string?>("prepare_incarnation_package.selectedCards должен быть ordered snapshot текущих gates.availableBlessingCards[].");

        gates["selectedBlessingCardIds"] = new JsonArray(selectedCardIds.Select(id => (JsonNode?)id).ToArray());
        var currentDraftVersion = GetNodeInt(gates["draftVersion"]);
        if (currentDraftVersion != request.SourceDraftVersion)
            return Task.FromResult<string?>("sourceDraftVersion должен совпадать с текущим draftVersion.");

        var success = ShiningAbodeState.TryPrepareIncarnationPackage(cloneRoot, request.CreatedAtTurn, out var error);
        return Task.FromResult(success ? null : error ?? "prepare_incarnation_package не прошёл canonical validation.");
    }

    private static string? ValidateZeroCostRequest(PendingShiningCoreActionRequest request, string actionType)
    {
        return request.QuotedCostFeathers == 0 && request.QuotedCostLightSparks == 0
            ? null
            : $"Quoted cost для {actionType} должен быть ровно 0 Feathers / 0 Light Sparks.";
    }

    private static async Task<string?> ValidateForgeActionRequestAsync(
        FileSystemManager fs,
        PendingShiningCoreActionRequest request,
        JsonObject shiningRoot,
        JsonObject? residentRoot)
    {
        if (string.IsNullOrWhiteSpace(request.FactionId))
            return "Forge action требует factionId.";
        if (string.IsNullOrWhiteSpace(request.RelicId))
            return "Forge action требует relicId.";

        var soulRoot = await ReadJsonObjectAsync(fs, "game_state/meta/soul_state.json");
        if (soulRoot == null)
            return "soul_state.json недоступен для forge action.";

        var radianceTier = GetNodeInt(shiningRoot["radiance"]?["tier"]);
        if (request.RadianceTierAtRequest != radianceTier)
            return "radianceTierAtRequest должен совпадать с текущим canonical radiance tier.";
        if (await ReadCurrentInkFeathersAsync(fs) < request.QuotedCostFeathers)
            return $"Недостаточно Перьев. Нужно {request.QuotedCostFeathers}.";

        if (!ShiningAbodeState.TryQuoteForgeAction(
                JsonNode.Parse(shiningRoot.ToJsonString())!.AsObject(),
                JsonNode.Parse(soulRoot.ToJsonString())!.AsObject(),
                residentRoot == null ? null : JsonNode.Parse(residentRoot.ToJsonString())!.AsObject(),
                request.ActionType,
                request.FactionId,
                request.RelicId,
                request.TargetFormTag,
                request.PropertyIndex,
                request.ReplacementProperty?.DeepClone().AsObject(),
                request.AddedProperties?.DeepClone().AsArray(),
                out var quotedCost,
                out var error))
        {
            return error ?? "Forge action не прошёл canonical validation.";
        }

        if (quotedCost.Feathers != request.QuotedCostFeathers || quotedCost.LightSparks != request.QuotedCostLightSparks)
            return "Quoted cost для forge action не совпадает с canonical quote.";

        return null;
    }

    private static async Task<string?> ValidateRelicGachaPullRequestAsync(
        FileSystemManager fs,
        PendingShiningCoreActionRequest request,
        JsonObject shiningRoot,
        JsonObject? residentRoot)
    {
        if (string.IsNullOrWhiteSpace(request.FactionId))
            return "pull_relic_gacha требует factionId.";

        var soulRoot = await ReadJsonObjectAsync(fs, "game_state/meta/soul_state.json");
        if (soulRoot == null)
            return "soul_state.json недоступен для сияющей гачи.";

        var radianceTier = GetNodeInt(shiningRoot["radiance"]?["tier"]);
        if (request.RadianceTierAtRequest != radianceTier)
            return "radianceTierAtRequest должен совпадать с текущим canonical radiance tier.";

        if (!ShiningAbodeState.TryQuoteRelicGachaPull(
                JsonNode.Parse(shiningRoot.ToJsonString())!.AsObject(),
                JsonNode.Parse(soulRoot.ToJsonString())!.AsObject(),
                residentRoot == null ? null : JsonNode.Parse(residentRoot.ToJsonString())!.AsObject(),
                request.FactionId,
                out var quotedCost,
                out var projectedBonusSteps,
                out var returnCycleId,
                out var error))
        {
            return error ?? "Shining relic gacha request не прошёл canonical validation.";
        }

        if (quotedCost.Feathers != request.QuotedCostFeathers || quotedCost.LightSparks != request.QuotedCostLightSparks)
            return "Quoted cost для pull_relic_gacha не совпадает с canonical quote.";
        if (!string.Equals(request.ReturnCycleId, returnCycleId, StringComparison.OrdinalIgnoreCase))
            return "returnCycleId должен совпадать с текущим Shining return cycle.";
        if (request.ProjectedGachaBonusSteps != projectedBonusSteps)
            return "projectedGachaBonusSteps должен совпадать с canonical Shining banner bonus.";

        return null;
    }

    private static string? ValidateOrdinaryActiveShiningMode(JsonObject? soulRoot, JsonObject shiningRoot)
    {
        var currentRealm = GetNodeString(soulRoot?["currentRealm"]);
        if (!string.Equals(currentRealm, "Shining Abode", StringComparison.OrdinalIgnoreCase) &&
            !string.Equals(currentRealm, "Сияющая Обитель", StringComparison.OrdinalIgnoreCase))
        {
            return "Shining core action допустим только при currentRealm = Shining Abode.";
        }

        if (!string.Equals(GetNodeString(shiningRoot["availability"]), ShiningAbodeState.AvailabilityActive, StringComparison.OrdinalIgnoreCase))
            return "Shining core action допустим только при availability = active.";
        var packageMode = ShiningAbodeState.GetPreparedIncarnationPackageMode(shiningRoot);
        if (packageMode == ShiningAbodeState.PreparedIncarnationPackageMode.ValidHandoff)
            return "Shining core action недопустим, пока preparedIncarnationPackage ожидает bootstrap.";
        if (packageMode == ShiningAbodeState.PreparedIncarnationPackageMode.InvalidFault)
            return "Shining core action недопустим: preparedIncarnationPackage повреждён или не проходит bootstrap validation.";

        return null;
    }

    private static async Task<int> ReadCurrentInkFeathersAsync(FileSystemManager fs)
    {
        var soulRoot = await ReadJsonObjectAsync(fs, "game_state/meta/soul_state.json");
        if (soulRoot == null)
            return 0;

        if (soulRoot["inkFeathers"] is JsonObject inkFeathers)
            return GetNodeInt(inkFeathers["current"]);

        return GetNodeInt(soulRoot["inkFeathers"]);
    }

    private static JsonObject? FindCompatibleReceipt(JsonArray receipts, PendingShiningCoreActionRequest request)
    {
        JsonObject? match = null;
        foreach (var receipt in receipts.OfType<JsonObject>())
        {
            if (!string.Equals(GetNodeString(receipt["requestId"]), request.RequestId, StringComparison.OrdinalIgnoreCase))
                continue;

            if (!ReceiptMatchesRequestContract(receipt, request))
                continue;

            if (match != null)
                return null;

            match = receipt;
        }

        return match;
    }

    private static bool HasMatchingCoreActionClosure(
        JsonObject shiningRoot,
        JsonArray receipts,
        PendingShiningCoreActionRequest request)
    {
        var receipt = FindCompatibleReceipt(receipts, request);
        if (receipt == null)
            return false;

        var status = GetNodeString(receipt["status"]);
        if (!string.Equals(status, RequestStatusAccepted, StringComparison.OrdinalIgnoreCase))
            return true;

        if (request.ActionType.Equals(ActionTypePrepareIncarnationPackage, StringComparison.OrdinalIgnoreCase))
        {
            if (shiningRoot["preparedIncarnationPackage"] is not JsonObject preparedPackage)
                return false;

            if (!string.IsNullOrWhiteSpace(ShiningAbodeState.ValidatePreparedIncarnationPackageForBootstrap(preparedPackage)))
                return false;

            var selectedPackageCards = (preparedPackage["selectedCardIds"] as JsonArray)?
                .OfType<JsonValue>()
                .Select(card => card.TryGetValue<string>(out var id) ? id?.Trim() : null)
                .Where(id => !string.IsNullOrWhiteSpace(id))
                .Select(id => id!)
                .ToList()
                ?? new List<string>();
            return selectedPackageCards.SequenceEqual(
                request.SelectedCardIds.Where(id => !string.IsNullOrWhiteSpace(id)).Select(id => id.Trim()),
                StringComparer.OrdinalIgnoreCase);
        }

        if (request.ActionType.Equals(ActionTypePullRelicGacha, StringComparison.OrdinalIgnoreCase))
        {
            var gachaHistory = shiningRoot["gachaSystem"]?["gachaHistory"] as JsonArray;
            return gachaHistory?.OfType<JsonObject>().Any(entry =>
                string.Equals(GetNodeString(entry["requestId"]), request.RequestId, StringComparison.OrdinalIgnoreCase) &&
                string.Equals(GetNodeString(entry["relicId"]) ?? string.Empty, GetNodeString(receipt["relicId"]) ?? string.Empty, StringComparison.OrdinalIgnoreCase)) == true;
        }

        return true;
    }

    private static bool ReceiptMatchesRequestContract(JsonObject? receipt, PendingShiningCoreActionRequest request)
    {
        if (receipt == null || !IsSupportedStatus(GetNodeString(receipt["status"])))
            return false;
        if (GetNodeInt(receipt["resolvedAtTurn"]) <= 0 || string.IsNullOrWhiteSpace(GetNodeString(receipt["resolvedAtUtc"])))
            return false;

        var receiptPropertyIndex = receipt["propertyIndex"] is JsonValue propertyIndexNode &&
                                   propertyIndexNode.TryGetValue<int>(out var propertyIndex)
            ? propertyIndex
            : -1;
        var selectedReceiptCards = (receipt["selectedCardIds"] as JsonArray)?
            .OfType<JsonValue>()
            .Select(card => card.TryGetValue<string>(out var id) ? id?.Trim() : null)
            .Where(id => !string.IsNullOrWhiteSpace(id))
            .Select(id => id!)
            .ToList()
            ?? new List<string>();
        var selectedRequestCards = request.SelectedCardIds
            .Where(id => !string.IsNullOrWhiteSpace(id))
            .Select(id => id.Trim())
            .ToList();

        var status = GetNodeString(receipt["status"]);
        return string.Equals(GetNodeString(receipt["requestId"]), request.RequestId, StringComparison.OrdinalIgnoreCase) &&
               string.Equals(GetNodeString(receipt["actionType"]), request.ActionType, StringComparison.OrdinalIgnoreCase) &&
               string.Equals(GetNodeString(receipt["factionId"]) ?? string.Empty, request.FactionId ?? string.Empty, StringComparison.OrdinalIgnoreCase) &&
               ProjectIdentityMatches(request, GetNodeString(receipt["projectId"])) &&
               RelicIdentityMatches(request, receipt, status) &&
               string.Equals(GetNodeString(receipt["returnCycleId"]) ?? string.Empty, request.ReturnCycleId ?? string.Empty, StringComparison.OrdinalIgnoreCase) &&
               string.Equals(GetNodeString(receipt["targetFormTag"]) ?? string.Empty, request.TargetFormTag ?? string.Empty, StringComparison.OrdinalIgnoreCase) &&
               receiptPropertyIndex == request.PropertyIndex &&
               selectedReceiptCards.SequenceEqual(selectedRequestCards, StringComparer.OrdinalIgnoreCase);
    }

    private static bool RelicIdentityMatches(
        PendingShiningCoreActionRequest request,
        JsonObject receipt,
        string? status)
    {
        var requestRelicId = request.RelicId ?? string.Empty;
        var receiptRelicId = GetNodeString(receipt["relicId"]) ?? string.Empty;
        if (string.Equals(request.ActionType, ActionTypePullRelicGacha, StringComparison.OrdinalIgnoreCase) &&
            string.Equals(status, RequestStatusAccepted, StringComparison.OrdinalIgnoreCase))
        {
            return !string.IsNullOrWhiteSpace(receiptRelicId) &&
                   (string.IsNullOrWhiteSpace(requestRelicId) ||
                    string.Equals(receiptRelicId, requestRelicId, StringComparison.OrdinalIgnoreCase));
        }

        return string.Equals(receiptRelicId, requestRelicId, StringComparison.OrdinalIgnoreCase);
    }

    private static bool SelectedCardSnapshotMatchesDraft(JsonArray? selectedCards, IReadOnlyList<string> selectedCardIds, JsonObject gates)
    {
        if (selectedCards == null || selectedCards.Count == 0)
            return false;

        if (selectedCards.Count != selectedCardIds.Count)
            return false;

        if (gates["availableBlessingCards"] is not JsonArray availableCards)
            return false;

        for (var i = 0; i < selectedCardIds.Count; i++)
        {
            if (selectedCards[i] is not JsonObject card)
                return false;

            if (!string.Equals(GetNodeString(card["cardId"])?.Trim() ?? string.Empty, selectedCardIds[i], StringComparison.OrdinalIgnoreCase))
                return false;

            var draftCard = availableCards.OfType<JsonObject>().FirstOrDefault(candidate =>
                string.Equals(GetNodeString(candidate["cardId"])?.Trim() ?? string.Empty, selectedCardIds[i], StringComparison.OrdinalIgnoreCase));
            if (draftCard == null)
                return false;

            if (!ShiningAbodeState.IsSupportedCardSourceType(GetNodeString(card["sourceType"])) ||
                !ShiningAbodeState.IsSupportedEffectFamily(GetNodeString(card["effectFamily"])) ||
                !ShiningAbodeState.IsSupportedRarity(GetNodeString(card["rarity"])) ||
                card["effectPayload"] is not JsonObject)
            {
                return false;
            }

            if (!JsonNode.DeepEquals(CloneCardForRequestSnapshotComparison(card), CloneCardForRequestSnapshotComparison(draftCard)))
                return false;
        }

        return true;
    }

    private static JsonObject CloneCardForRequestSnapshotComparison(JsonObject card)
    {
        var clone = card.DeepClone().AsObject();
        clone.Remove("_effectiveStrength");
        return clone;
    }

    private static bool ProjectIdentityMatches(PendingShiningCoreActionRequest request, string? receiptProjectId)
    {
        if (string.Equals(request.ActionType, ActionTypeCompleteProject, StringComparison.OrdinalIgnoreCase) &&
            string.IsNullOrWhiteSpace(request.ProjectId))
        {
            return !string.IsNullOrWhiteSpace(receiptProjectId);
        }

        return string.Equals(receiptProjectId ?? string.Empty, request.ProjectId ?? string.Empty, StringComparison.OrdinalIgnoreCase);
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

    private static int GetNodeInt(JsonNode? node)
    {
        if (node == null)
            return 0;

        try
        {
            return node.GetValue<int>();
        }
        catch
        {
            return 0;
        }
    }

    private static bool GetNodeBool(JsonNode? node)
    {
        if (node == null)
            return false;

        try
        {
            return node.GetValue<bool>();
        }
        catch
        {
            return false;
        }
    }

    private static bool IsShiningRealm(string? currentRealm) => RealmSemantics.IsShiningRealm(currentRealm);
}
