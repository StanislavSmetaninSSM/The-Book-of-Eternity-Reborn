using System.Text.Json;
using System.Text.Json.Nodes;
using System.Text.Json.Serialization;
using BookOfEternityClient.Configuration;
using BookOfEternityClient.Core;

namespace BookOfEternityClient.Services;

internal static class GuardianAbodeResidentRequestState
{
    private enum RequestBundleReadStatus
    {
        Missing,
        Valid,
        Malformed
    }

    public const string PendingResidentsRequestPath = "game_state/control/pending_guardian_abode_residents_request.json";
    public const string PendingInteractionsRequestPath = "game_state/control/pending_guardian_abode_resident_interactions.json";
    public const string PendingTransfersRequestPath = "game_state/control/pending_guardian_abode_resident_transfers.json";
    public const string PendingManifestationRequestPath = "game_state/control/pending_resident_companion_manifestation_request.json";
    public const string ResidentsRequestsProperty = "requests";
    public const string InteractionRequestsProperty = "requests";
    public const string TransferRequestsProperty = "requests";
    public const string ManifestationRequestsProperty = "requests";
    public const string ResidentsRequestModeStandardRoster = "standard_roster";
    public const string ResidentsRequestModeFounderAttraction = "founder_attraction";
    public const string TransferSelectionModeCompetitionRecommended = "competition_recommended";
    public const string TransferSelectionModeManualOverride = "manual_override";
    public const string TransferSelectionModeDepartureOnly = "departure_only";

    private static readonly JsonSerializerOptions JsonOpts = SharedJsonOptions.PrettyCamelCaseUnsafeRelaxed;
    private static readonly string[] ManifestationTouchedRelicFields =
    {
        "companionManifestationLastRequestedIncarnation",
        "companionManifestationStatus",
        "lastManifestationRequestId",
        "companionManifestationResolvedRequestId",
        "companionManifestationResolvedNpcId",
        "companionManifestationResolvedAtTurn",
        "companionManifestationResolvedAtUtc"
    };

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

        [JsonPropertyName("requestMode")]
        public string RequestMode { get; set; } = ResidentsRequestModeStandardRoster;

        [JsonPropertyName("founderFeatureTitle")]
        public string? FounderFeatureTitle { get; set; }

        [JsonPropertyName("founderFeatureSummary")]
        public string? FounderFeatureSummary { get; set; }

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

    public sealed class PendingGuardianAbodeResidentTransferRequest
    {
        [JsonPropertyName("requestId")]
        public string RequestId { get; set; } = $"abode_resident_transfer_{Guid.NewGuid():N}";

        [JsonPropertyName("residentId")]
        public string ResidentId { get; set; } = "";

        [JsonPropertyName("residentName")]
        public string ResidentName { get; set; } = "";

        [JsonPropertyName("sourceGuardianId")]
        public string SourceGuardianId { get; set; } = "";

        [JsonPropertyName("sourceGuardianName")]
        public string SourceGuardianName { get; set; } = "";

        [JsonPropertyName("sourceAbodeId")]
        public string SourceAbodeId { get; set; } = "";

        [JsonPropertyName("sourceAbodeName")]
        public string SourceAbodeName { get; set; } = "";

        [JsonPropertyName("targetGuardianId")]
        public string TargetGuardianId { get; set; } = "";

        [JsonPropertyName("targetGuardianName")]
        public string TargetGuardianName { get; set; } = "";

        [JsonPropertyName("targetAbodeId")]
        public string TargetAbodeId { get; set; } = "";

        [JsonPropertyName("targetAbodeName")]
        public string TargetAbodeName { get; set; } = "";

        [JsonPropertyName("abodeDevotionLevel")]
        public int AbodeDevotionLevel { get; set; }

        [JsonPropertyName("abodeDevotionTier")]
        public string AbodeDevotionTier { get; set; } = GuardianAbodeResidentState.AbodeDevotionTierAttached;

        [JsonPropertyName("restlessness")]
        public int Restlessness { get; set; }

        [JsonPropertyName("migrationState")]
        public string MigrationState { get; set; } = GuardianAbodeResidentState.MigrationStateReadyToTransfer;

        [JsonPropertyName("transferMode")]
        public string TransferMode { get; set; } = GuardianAbodeResidentState.TransferModeAcceptedTransfer;

        [JsonPropertyName("selectionMode")]
        public string? SelectionMode { get; set; }

        [JsonPropertyName("competitionScore")]
        public int? CompetitionScore { get; set; }

        [JsonPropertyName("competitionLabel")]
        public string? CompetitionLabel { get; set; }

        [JsonPropertyName("competitionReason")]
        public string? CompetitionReason { get; set; }

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

        [JsonPropertyName("personalityProfile")]
        public GuardianAbodeResidentState.ResidentPersonalityProfile? PersonalityProfile { get; set; }

        [JsonPropertyName("abodeDisposition")]
        public GuardianAbodeResidentState.ResidentAbodeDisposition? AbodeDisposition { get; set; }

        [JsonPropertyName("abodeDevotionLevel")]
        public int? AbodeDevotionLevel { get; set; }

        [JsonPropertyName("abodeDevotionTier")]
        public string? AbodeDevotionTier { get; set; }

        [JsonPropertyName("restlessness")]
        public int? Restlessness { get; set; }

        [JsonPropertyName("migrationState")]
        public string? MigrationState { get; set; }

        [JsonPropertyName("createdAtUtc")]
        public string CreatedAtUtc { get; set; } = DateTime.UtcNow.ToString("o");
    }

    public static async Task WriteResidentsRequestsAsync(
        FileSystemManager fs,
        IReadOnlyCollection<PendingGuardianAbodeResidentsRequest> requests)
    {
        await EnsureBundleFileWritableAsync(
            fs,
            PendingResidentsRequestPath,
            ResidentsRequestsProperty,
            static item => JsonSerializer.Deserialize<PendingGuardianAbodeResidentsRequest>(item, JsonOpts));

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
        await EnsureBundleFileWritableAsync(
            fs,
            PendingInteractionsRequestPath,
            InteractionRequestsProperty,
            static item => JsonSerializer.Deserialize<PendingGuardianAbodeResidentInteractionRequest>(item, JsonOpts));

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
        await EnsureBundleFileWritableAsync(
            fs,
            PendingManifestationRequestPath,
            ManifestationRequestsProperty,
            static item => JsonSerializer.Deserialize<PendingResidentCompanionManifestationRequest>(item, JsonOpts));

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

    public static async Task WriteTransferRequestsAsync(
        FileSystemManager fs,
        IReadOnlyCollection<PendingGuardianAbodeResidentTransferRequest> requests)
    {
        await EnsureBundleFileWritableAsync(
            fs,
            PendingTransfersRequestPath,
            TransferRequestsProperty,
            static item => JsonSerializer.Deserialize<PendingGuardianAbodeResidentTransferRequest>(item, JsonOpts));

        if (requests.Count == 0)
        {
            ClearTransferRequests(fs);
            return;
        }

        await fs.WriteFileAtomicAsync(
            PendingTransfersRequestPath,
            JsonSerializer.Serialize(new Dictionary<string, object?>
            {
                [TransferRequestsProperty] = requests
            }, JsonOpts));
    }

    public static async Task WriteTransferRequestAsync(FileSystemManager fs, PendingGuardianAbodeResidentTransferRequest request)
    {
        var existing = (await ReadTransferRequestsAsync(fs)).ToList();
        var replaced = false;
        for (var i = 0; i < existing.Count; i++)
        {
            if (!string.Equals(existing[i].ResidentId, request.ResidentId, StringComparison.OrdinalIgnoreCase))
                continue;

            existing[i] = request;
            replaced = true;
            break;
        }

        if (!replaced)
            existing.Add(request);

        await WriteTransferRequestsAsync(fs, existing);
    }

    public static async Task<IReadOnlyList<PendingGuardianAbodeResidentsRequest>> ReadResidentsRequestsAsync(FileSystemManager fs)
    {
        var json = await fs.ReadFileAsync(PendingResidentsRequestPath);
        return ReadRequestBundle(
            json,
            ResidentsRequestsProperty,
            static item => JsonSerializer.Deserialize<PendingGuardianAbodeResidentsRequest>(item, JsonOpts),
            out var requests) == RequestBundleReadStatus.Valid
            ? requests
            : Array.Empty<PendingGuardianAbodeResidentsRequest>();
    }

    public static async Task<PendingGuardianAbodeResidentsRequest?> ReadResidentsRequestAsync(FileSystemManager fs) =>
        (await ReadResidentsRequestsAsync(fs)).FirstOrDefault();

    public static async Task<IReadOnlyList<PendingGuardianAbodeResidentInteractionRequest>> ReadInteractionRequestsAsync(FileSystemManager fs)
    {
        var json = await fs.ReadFileAsync(PendingInteractionsRequestPath);
        return ReadRequestBundle(
            json,
            InteractionRequestsProperty,
            static item => JsonSerializer.Deserialize<PendingGuardianAbodeResidentInteractionRequest>(item, JsonOpts),
            out var requests) == RequestBundleReadStatus.Valid
            ? requests
            : Array.Empty<PendingGuardianAbodeResidentInteractionRequest>();
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

    public static async Task<PendingGuardianAbodeResidentTransferRequest?> FindPendingTransferAsync(
        FileSystemManager fs,
        string residentId)
    {
        return (await ReadTransferRequestsAsync(fs)).FirstOrDefault(request =>
            string.Equals(request.ResidentId, residentId, StringComparison.OrdinalIgnoreCase));
    }

    public static async Task<IReadOnlyList<PendingResidentCompanionManifestationRequest>> ReadManifestationRequestsAsync(FileSystemManager fs)
    {
        var json = await fs.ReadFileAsync(PendingManifestationRequestPath);
        return ReadRequestBundle(
            json,
            ManifestationRequestsProperty,
            static item => JsonSerializer.Deserialize<PendingResidentCompanionManifestationRequest>(item, JsonOpts),
            out var requests) == RequestBundleReadStatus.Valid
            ? requests
            : Array.Empty<PendingResidentCompanionManifestationRequest>();
    }

    public static async Task<IReadOnlyList<PendingGuardianAbodeResidentTransferRequest>> ReadTransferRequestsAsync(FileSystemManager fs)
    {
        var json = await fs.ReadFileAsync(PendingTransfersRequestPath);
        return ReadRequestBundle(
            json,
            TransferRequestsProperty,
            static item => JsonSerializer.Deserialize<PendingGuardianAbodeResidentTransferRequest>(item, JsonOpts),
            out var requests) == RequestBundleReadStatus.Valid
            ? requests
            : Array.Empty<PendingGuardianAbodeResidentTransferRequest>();
    }

    public static async Task<bool> IsResidentsRequestFileMalformedAsync(FileSystemManager fs) =>
        ReadRequestBundle(
            await fs.ReadFileAsync(PendingResidentsRequestPath),
            ResidentsRequestsProperty,
            static item => JsonSerializer.Deserialize<PendingGuardianAbodeResidentsRequest>(item, JsonOpts),
            out _) == RequestBundleReadStatus.Malformed;

    public static async Task<bool> IsInteractionRequestFileMalformedAsync(FileSystemManager fs) =>
        ReadRequestBundle(
            await fs.ReadFileAsync(PendingInteractionsRequestPath),
            InteractionRequestsProperty,
            static item => JsonSerializer.Deserialize<PendingGuardianAbodeResidentInteractionRequest>(item, JsonOpts),
            out _) == RequestBundleReadStatus.Malformed;

    public static async Task<bool> IsTransferRequestFileMalformedAsync(FileSystemManager fs) =>
        ReadRequestBundle(
            await fs.ReadFileAsync(PendingTransfersRequestPath),
            TransferRequestsProperty,
            static item => JsonSerializer.Deserialize<PendingGuardianAbodeResidentTransferRequest>(item, JsonOpts),
            out _) == RequestBundleReadStatus.Malformed;

    public static async Task<bool> IsManifestationRequestFileMalformedAsync(FileSystemManager fs) =>
        ReadRequestBundle(
            await fs.ReadFileAsync(PendingManifestationRequestPath),
            ManifestationRequestsProperty,
            static item => JsonSerializer.Deserialize<PendingResidentCompanionManifestationRequest>(item, JsonOpts),
            out _) == RequestBundleReadStatus.Malformed;

    public static void ClearResidentsRequest(FileSystemManager fs) => fs.DeleteFile(PendingResidentsRequestPath);

    public static void ClearInteractionRequests(FileSystemManager fs) => fs.DeleteFile(PendingInteractionsRequestPath);

    public static void ClearTransferRequests(FileSystemManager fs) => fs.DeleteFile(PendingTransfersRequestPath);

    public static void ClearManifestationRequest(FileSystemManager fs) => fs.DeleteFile(PendingManifestationRequestPath);

    public static bool IsSupportedTransferSelectionMode(string? selectionMode) =>
        (selectionMode ?? string.Empty).Trim().ToLowerInvariant() is
            TransferSelectionModeCompetitionRecommended or
            TransferSelectionModeManualOverride or
            TransferSelectionModeDepartureOnly;

    public static string GetTransferSelectionModeLabel(string? selectionMode) =>
        (selectionMode ?? string.Empty).Trim().ToLowerInvariant() switch
        {
            TransferSelectionModeCompetitionRecommended => "системная рекомендация",
            TransferSelectionModeManualOverride => "ручной выбор поверх слабого системного зова",
            TransferSelectionModeDepartureOnly => "уход без новой Обители",
            _ => string.Empty
        };

    public static string DescribeTransferCompetitionMetadata(PendingGuardianAbodeResidentTransferRequest request)
    {
        var parts = new List<string>();
        var selectionLabel = GetTransferSelectionModeLabel(request.SelectionMode);
        if (!string.IsNullOrWhiteSpace(selectionLabel))
            parts.Add($"selection={selectionLabel}");

        if (request.CompetitionScore.HasValue && !string.IsNullOrWhiteSpace(request.CompetitionLabel))
        {
            parts.Add(
                $"competition={GuardianAbodeResidentState.GetTransferCompetitionLabelText(request.CompetitionLabel)} {request.CompetitionScore.Value}/100");
        }

        if (!string.IsNullOrWhiteSpace(request.CompetitionReason))
            parts.Add($"reason=\"{request.CompetitionReason}\"");

        return string.Join(", ", parts);
    }

    public static string BuildTransferCompetitionNarrative(PendingGuardianAbodeResidentTransferRequest request)
    {
        if (string.Equals(request.TransferMode, GuardianAbodeResidentState.TransferModeDepartureOnly, StringComparison.OrdinalIgnoreCase))
            return string.Empty;

        var labelText = GuardianAbodeResidentState.GetTransferCompetitionLabelText(request.CompetitionLabel);
        return (request.SelectionMode ?? string.Empty).Trim().ToLowerInvariant() switch
        {
            TransferSelectionModeCompetitionRecommended when request.CompetitionScore.HasValue && !string.IsNullOrWhiteSpace(labelText) =>
                $" Система видит {labelText} {request.CompetitionScore.Value}/100 в пользу этой цели. {request.CompetitionReason}".TrimEnd(),
            TransferSelectionModeManualOverride when request.CompetitionScore.HasValue && !string.IsNullOrWhiteSpace(labelText) =>
                $" Цель выбрана вручную поверх системной оценки {labelText} {request.CompetitionScore.Value}/100. {request.CompetitionReason}".TrimEnd(),
            _ => string.Empty
        };
    }

    public static async Task EnsureHealthyAsync(FileSystemManager fs, string? currentRealm)
    {
        var manifestationFileMalformed = await IsManifestationRequestFileMalformedAsync(fs);
        if (!RealmSemantics.HasResolvedRealm(currentRealm))
            return;

        if (!IsAfterlifeRealm(currentRealm))
        {
            ClearResidentsRequest(fs);
            ClearInteractionRequests(fs);
            ClearTransferRequests(fs);
            if (manifestationFileMalformed)
                return;

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
            await EnsureTransferRequestsHealthyAsync(fs);
            if (!manifestationFileMalformed)
            {
                var manifestationRequests = await ReadManifestationRequestsAsync(fs);
                if (manifestationRequests.Count == 0)
                    ClearManifestationRequest(fs);
            }
        }
    }

    public static async Task EnsureManifestationRequestForCurrentIncarnationAsync(FileSystemManager fs, string? currentRealm)
    {
        if (!IsMortalRealm(currentRealm))
        {
            return;
        }

        if (await IsManifestationRequestFileMalformedAsync(fs))
            return;

        var existingRequests = (await ReadManifestationRequestsAsync(fs)).ToList();
        if (existingRequests.Count > 0)
        {
            await EnsureManifestationRequestsHealthyAsync(fs, existingRequests, currentRealm);
            existingRequests = (await ReadManifestationRequestsAsync(fs)).ToList();
        }

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

        var preManifestationRequestJson = await fs.ReadFileAsync(PendingManifestationRequestPath);
        var preSoulJson = soulJson;

        var lifeTransitionsJson = await fs.ReadFileAsync("game_state/control/life_transitions.json");
        var hasCanonicalTriggerLifeEnd = await CanonicalStateNormalizer.HasLifecycleAuthorizedTriggerLifeEndFromPendingSnapshotAsync(
            fs,
            lifeTransitionsJson,
            soulRoot);

        if (!GuardianPolicyContracts.TryReadStrictCurrentManifestationSoulRelicCollections(
                soulRoot,
                hasCanonicalTriggerLifeEnd,
                out var currentIncarnation,
                out var equipped,
                out _,
                out _) ||
            equipped == null)
        {
            return;
        }

        var existingRelicIds = existingRequests
            .Where(request => request.TargetIncarnation == currentIncarnation)
            .Select(request => request.RelicId)
            .ToHashSet(StringComparer.OrdinalIgnoreCase);

        var requestsToAppend = new List<PendingResidentCompanionManifestationRequest>();
        foreach (var relic in equipped.OfType<JsonObject>())
        {
            if (!IsEligibleCompanionEchoRelic(relic, currentIncarnation, existingRelicIds))
                continue;

            if (!TryBuildManifestationRequest(relic, currentIncarnation, out var request))
                continue;

            relic["companionManifestationLastRequestedIncarnation"] = currentIncarnation;
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

        foreach (var request in requestsToAppend)
            existingRequests.Add(request);

        var postSoulJson = GuardianPolicyContracts.CreatePatchedSoulStateWriteRoot(
            soulRoot,
            new GuardianPolicyContracts.SoulStatePatchConflictContext(
                GuardianPolicyContracts.SoulStatePatchTouchedDomains.SoulRelics,
                unsafeToReplayAddedSoulRelicIds: requestsToAppend.Select(request => request.RelicId),
                updatedSoulRelicFieldsById: BuildManifestationRelicFieldUpdates(requestsToAppend.Select(request => request.RelicId)))).ToJsonString(JsonOpts);
        var postManifestationRequestJson = existingRequests.Count == 0
            ? null
            : JsonSerializer.Serialize(new Dictionary<string, object?>
            {
                [ManifestationRequestsProperty] = existingRequests
            }, JsonOpts);

        if (!await CoordinatedStateWriteHelper.TryCommitAsync(
                fs,
                new CoordinatedStateWriteHelper.PlannedWrite("game_state/meta/soul_state.json", preSoulJson, postSoulJson),
                new CoordinatedStateWriteHelper.PlannedWrite(PendingManifestationRequestPath, preManifestationRequestJson, postManifestationRequestJson)))
        {
            return;
        }
    }

    private const int ReminderEntryLimit = 5;

    public static async Task<string?> BuildSystemReminderFragmentAsync(FileSystemManager fs, string? currentRealm)
    {
        if (IsAfterlifeRealm(currentRealm))
        {
            var malformedRoster = await IsResidentsRequestFileMalformedAsync(fs);
            var malformedInteractions = await IsInteractionRequestFileMalformedAsync(fs);
            var malformedTransfers = await IsTransferRequestFileMalformedAsync(fs);
            if (malformedRoster || malformedInteractions || malformedTransfers)
            {
                var issues = new List<string>();
                if (malformedRoster)
                    issues.Add("pending_guardian_abode_residents_request.json");
                if (malformedInteractions)
                    issues.Add("pending_guardian_abode_resident_interactions.json");
                if (malformedTransfers)
                    issues.Add("pending_guardian_abode_resident_transfers.json");

                return "ABODE RESIDENT REQUEST CORRUPTION:" + Environment.NewLine +
                       $"Unreadable or malformed pending files: {string.Join(", ", issues)}." + Environment.NewLine +
                       "Preserve the pending resident contracts and repair them before ordinary afterlife resolution continues.";
            }

            var rosterRequests = await ReadResidentsRequestsAsync(fs);
            var interactionRequests = await ReadInteractionRequestsAsync(fs);
            var transferRequests = await ReadTransferRequestsAsync(fs);
            var manifestationMalformed = await IsManifestationRequestFileMalformedAsync(fs);
            var pressuredResidents = await ReadResidentPressureReminderInfosAsync(fs, transferRequests);
            var afterlifeManifestationRequests = manifestationMalformed ? Array.Empty<PendingResidentCompanionManifestationRequest>() : await ReadManifestationRequestsAsync(fs);
            if (rosterRequests.Count == 0 &&
                interactionRequests.Count == 0 &&
                transferRequests.Count == 0 &&
                pressuredResidents.Count == 0 &&
                !manifestationMalformed &&
                afterlifeManifestationRequests.Count == 0)
                return null;

            var rosterLines = new List<string>();
            if (manifestationMalformed)
            {
                rosterLines.AddRange(new[]
                {
                    "COMPANION MANIFESTATION REQUEST CORRUPTION:",
                    $"There is malformed {PendingManifestationRequestPath}.",
                    "This is a MortalWorldProfile-only next-life contract. In afterlife, preserve it for repair; do not materialize mortal NPCs or close it with afterlife receipts."
                });
            }
            else if (afterlifeManifestationRequests.Count > 0)
            {
                rosterLines.AddRange(new[]
                {
                    "COMPANION MANIFESTATION REQUESTS PRESERVED FOR NEXT LIFE:",
                    $"There are {afterlifeManifestationRequests.Count} pending entries in pending_resident_companion_manifestation_request.json.",
                    "This file may originate from afterlife resident rewards or imprint-bearing Soul Relics, but it closes only after Mortal bootstrap.",
                    "In Chaos Sea or Shining Abode, do not materialize mortal NPCs, encounters, NPC social journals, quests, or afterlife receipts from it."
                });

                foreach (var request in afterlifeManifestationRequests.Take(ReminderEntryLimit))
                {
                    var snapshotSummary = DescribeManifestationRequestSnapshot(request);
                    rosterLines.Add($"- requestId={request.RequestId}, relicId={request.RelicId}, source={request.ManifestationSource}, residentId={request.SourceResidentId}, imprintId={request.SourceImprintId}, targetIncarnation={request.TargetIncarnation}{snapshotSummary}");
                }
            }

            if (rosterRequests.Count > 0)
            {
                if (rosterLines.Count > 0)
                    rosterLines.Add(string.Empty);

                rosterLines.AddRange(new[]
                {
                    "ABODE RESIDENT ROSTER REQUESTS:",
                    $"There are {rosterRequests.Count} pending entries in pending_guardian_abode_residents_request.json.",
                    "Materialize explicit residents in game_state/meta/guardian_abode_residents.json via UpdateGuardianAbodeResidents and close each request through UpdateGuardianAbodeResidentRosterReceipts.",
                    "Do not derive residents from guardian domain; author the roster explicitly.",
                    "Each resident should include personalityProfile, abodeDisposition, abodeDevotionLevel, abodeDevotionTier, restlessness, and migrationState in addition to the existing resident contract."
                });

                foreach (var request in rosterRequests)
                {
                    var founderPart = string.Equals(request.RequestMode, ResidentsRequestModeFounderAttraction, StringComparison.OrdinalIgnoreCase)
                        ? $", mode={request.RequestMode}, feature=\"{request.FounderFeatureTitle ?? "founder_call"}\""
                        : string.Empty;
                    rosterLines.Add($"- requestId={request.RequestId}, guardianId={request.GuardianId}, abodeId={request.AbodeId}, guardianName={request.GuardianName}, abodeName={request.AbodeName}, currentReputation={request.CurrentReputation}, requestMode={request.RequestMode}, createdAtTurn={request.CreatedAtTurn}, createdAtUtc={request.CreatedAtUtc}{founderPart}");
                    AppendSerializedJsonLines(rosterLines, "Full pending resident-roster DTO", request);
                }
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
                    "Do not ignore resident talk/history requests; close each one with status=accepted|rejected|cancelled.",
                    "If the scene changes resident attitude toward the Abode, update abodeDevotionLevel/restlessness/migrationState in small canonical steps derived from the outcome, current Abode Power tier, abodeDisposition, and bondLevel, and leave curated resident memory."
                });

                foreach (var request in interactionRequests)
                {
                    rosterLines.Add($"- requestId={request.RequestId}, residentId={request.ResidentId}, residentName={request.ResidentName}, interactionType={request.InteractionType}, guardianId={request.GuardianId}, abodeId={request.AbodeId}, createdAtTurn={request.CreatedAtTurn}, createdAtUtc={request.CreatedAtUtc}");
                    AppendSerializedJsonLines(rosterLines, "Full pending resident-interaction DTO", request);
                }
            }

            var visiblePressuredResidents = pressuredResidents.Take(ReminderEntryLimit).ToList();
            if (pressuredResidents.Count > 0)
            {
                if (rosterLines.Count > 0)
                    rosterLines.Add(string.Empty);

                rosterLines.AddRange(new[]
                {
                    "ABODE RESIDENT PRESSURE STATES:",
                    $"There are {pressuredResidents.Count} residents currently in wavering/restless/departure pressure.",
                    "Surface these states through journals, interaction tone, and resident-facing consequences.",
                    "Do not reassign or transfer residents automatically; only explicit pending_guardian_abode_resident_transfers.json requests may resolve a transfer."
                });

                foreach (var resident in visiblePressuredResidents)
                {
                    var guardianPart = string.IsNullOrWhiteSpace(resident.GuardianName)
                        ? string.Empty
                        : $", guardian={resident.GuardianName}";
                    rosterLines.Add(
                        $"- resident={resident.DisplayName}{guardianPart}, migrationState={resident.MigrationState}, abodeDevotion={resident.AbodeDevotionLevel}/100, restlessness={resident.Restlessness}/100, pressure=\"{resident.PressureNarrative}\"");
                    if (!string.IsNullOrWhiteSpace(resident.TransferRequestSummary))
                        rosterLines.Add($"  transferRequestPending={resident.TransferRequestSummary}");
                    else if (!string.IsNullOrWhiteSpace(resident.TopCompetitionSummary))
                        rosterLines.Add($"  competition={resident.TopCompetitionSummary}");
                }
            }

            if (transferRequests.Count > 0)
            {
                if (rosterLines.Count > 0)
                    rosterLines.Add(string.Empty);

                rosterLines.AddRange(new[]
                {
                    "ABODE RESIDENT TRANSFER REQUESTS:",
                    $"There are {transferRequests.Count} pending entries in pending_guardian_abode_resident_transfers.json.",
                    "Resolve each request canonically through guardian_abode_residents.json: accepted transfer moves the same residentId into the target Abode, refused transfer leaves the resident in place, departure_only removes the resident from the source presence without silently teleporting elsewhere.",
                    "selectionMode/competition metadata are advisory only. Every transfer resolution must write guardian_abode_residents.json.transferReceipts[] and matching history entries; do not resolve transfer by prose alone."
                });

                foreach (var request in transferRequests)
                {
                    var targetPart = string.IsNullOrWhiteSpace(request.TargetGuardianName) && string.IsNullOrWhiteSpace(request.TargetAbodeName)
                        ? "offscreen departure"
                        : $"targetGuardian={request.TargetGuardianName} ({request.TargetGuardianId}), targetAbode={request.TargetAbodeName} ({request.TargetAbodeId})";
                    var competitionPart = DescribeTransferCompetitionMetadata(request);
                    rosterLines.Add(
                        $"- requestId={request.RequestId}, resident={request.ResidentName} ({request.ResidentId}), sourceGuardian={request.SourceGuardianName} ({request.SourceGuardianId}), sourceAbode={request.SourceAbodeName} ({request.SourceAbodeId}), mode={request.TransferMode}, migrationState={request.MigrationState}, devotion={request.AbodeDevotionLevel}/100 ({request.AbodeDevotionTier}), restlessness={request.Restlessness}/100, createdAtTurn={request.CreatedAtTurn}, createdAtUtc={request.CreatedAtUtc}, {targetPart}{(string.IsNullOrWhiteSpace(competitionPart) ? string.Empty : $", {competitionPart}")}");
                    AppendSerializedJsonLines(rosterLines, "Full pending resident-transfer DTO", request);
                }
            }

            return string.Join("\n", rosterLines);
        }

        if (await IsManifestationRequestFileMalformedAsync(fs))
        {
            return "COMPANION MANIFESTATION REQUEST CORRUPTION:" + Environment.NewLine +
                   $"  - {PendingManifestationRequestPath} unreadable or malformed." + Environment.NewLine +
                   "  - Preserve the pending manifestation contract until validation/repair resolves it.";
        }

        var manifestationRequests = await ReadManifestationRequestsAsync(fs);
        if (manifestationRequests.Count == 0)
            return null;

        var lines = new List<string>
        {
            "COMPANION ECHO MANIFESTATION REQUESTS:",
            $"There are {manifestationRequests.Count} pending entries in pending_resident_companion_manifestation_request.json.",
            "For each request, materialize an early mortal-world encounter or soul-quest path that leads to this companion. Use captured personalityProfile, abodeDisposition, and devotion snapshot from the request when they are present; when they are absent, fall back to the legacy imprint fields. When the companion fully manifests, write an ordinary mortal NPC in npc_core.json and set sourceCompanionRelicId/sourceAfterlifeResidentId/sourceSoulImprintId when applicable.",
            "This is a guaranteed early encounter path, not an instant teleport-spawn in arbitrary combat."
        };
        foreach (var request in manifestationRequests.Take(ReminderEntryLimit))
        {
            var snapshotSummary = DescribeManifestationRequestSnapshot(request);
            lines.Add($"- relicId={request.RelicId}, source={request.ManifestationSource}, residentId={request.SourceResidentId}, imprintId={request.SourceImprintId}, targetIncarnation={request.TargetIncarnation}{snapshotSummary}");
        }

        return string.Join("\n", lines);
    }

    private static void AppendSerializedJsonLines(List<string> lines, string title, object payload)
    {
        lines.Add($"  {title}:");
        var json = JsonSerializer.Serialize(payload, JsonOpts).Replace("\r\n", "\n", StringComparison.Ordinal);
        foreach (var line in json.Split('\n'))
            lines.Add($"    {line}");
    }

    public static string BuildResidentsRosterPendingGmActionText(PendingGuardianAbodeResidentsRequest request)
    {
        if (string.Equals(request.RequestMode, ResidentsRequestModeFounderAttraction, StringComparison.OrdinalIgnoreCase))
        {
            var featureTitle = string.IsNullOrWhiteSpace(request.FounderFeatureTitle) ? "Зов основанной мантии" : request.FounderFeatureTitle!;
            var featureSummary = string.IsNullOrWhiteSpace(request.FounderFeatureSummary)
                ? "Новая founded-Обитель притягивает первых резидентов и не переносит автоматически старый roster."
                : request.FounderFeatureSummary!;
            return
                $"[ABODE_RESIDENT_ROSTER_REQUEST] Игрок открыл founder-attraction roster новой Обители Хранителя '{request.GuardianName}' (requestId={request.RequestId}, guardianId={request.GuardianId}, abodeId={request.AbodeId}, abodeName={request.AbodeName}, currentReputation={request.CurrentReputation}, requestMode={request.RequestMode}, createdAtTurn={request.CreatedAtTurn}, createdAtUtc={request.CreatedAtUtc}). " +
                "В accepted turn materialize explicit residents через UpdateGuardianAbodeResidents в guardian_abode_residents.json и закрой request через UpdateGuardianAbodeResidentRosterReceipts. " +
                $"Используй founder feature '{featureTitle}': {featureSummary} " +
                "Не переноси автоматически резидентов прежнего patron guardian. Авторски создай 1-3 afterlife residents, которых новая мантия только начинает притягивать, с canonical personalityProfile, abodeDisposition, abodeDevotionLevel, abodeDevotionTier, restlessness и migrationState.";
        }

        return
            $"[ABODE_RESIDENT_ROSTER_REQUEST] Игрок открыл roster Обители Хранителя '{request.GuardianName}' (requestId={request.RequestId}, guardianId={request.GuardianId}, abodeId={request.AbodeId}, abodeName={request.AbodeName}, currentReputation={request.CurrentReputation}, requestMode={request.RequestMode}, createdAtTurn={request.CreatedAtTurn}, createdAtUtc={request.CreatedAtUtc}). " +
            "В accepted turn materialize explicit residents через UpdateGuardianAbodeResidents в guardian_abode_residents.json и закрой request через UpdateGuardianAbodeResidentRosterReceipts. " +
            "Не выводи roster из домена Хранителя. Авторски создай 2-4 afterlife residents с residentId, residentKind, roleLabel, bondLevel, bondTier, canGrantCompanionRelic, bondRewardState, personalityProfile, abodeDisposition, abodeDevotionLevel, abodeDevotionTier, restlessness, migrationState и mortalWorldImprint. " +
            "abodeDevotionTier и migrationState должны быть canonical derived values, а не свободным prose.";
    }

    private static List<PendingGuardianAbodeResidentTransferRequest> SelectVisibleTransferReminderRequests(
        IReadOnlyList<PendingGuardianAbodeResidentTransferRequest> transferRequests,
        IReadOnlyList<ResidentPressureReminderInfo> visiblePressuredResidents,
        int limit)
    {
        if (transferRequests.Count <= limit)
            return transferRequests.ToList();

        var selectedIndexes = new HashSet<int>();
        var selectedRequests = new List<PendingGuardianAbodeResidentTransferRequest>();
        foreach (var resident in visiblePressuredResidents)
        {
            if (string.IsNullOrWhiteSpace(resident.ResidentId))
                continue;

            for (var index = 0; index < transferRequests.Count; index++)
            {
                if (selectedIndexes.Contains(index))
                    continue;

                if (!string.Equals(transferRequests[index].ResidentId, resident.ResidentId, StringComparison.OrdinalIgnoreCase))
                    continue;

                selectedIndexes.Add(index);
                selectedRequests.Add(transferRequests[index]);
                break;
            }

            if (selectedRequests.Count >= limit)
                return selectedRequests;
        }

        for (var index = 0; index < transferRequests.Count && selectedRequests.Count < limit; index++)
        {
            if (selectedIndexes.Add(index))
                selectedRequests.Add(transferRequests[index]);
        }

        return selectedRequests;
    }

    private sealed class ResidentPressureReminderInfo
    {
        public string ResidentId { get; init; } = "";
        public string DisplayName { get; init; } = "";
        public string GuardianName { get; init; } = "";
        public string MigrationState { get; init; } = GuardianAbodeResidentState.MigrationStateSettled;
        public int AbodeDevotionLevel { get; init; }
        public int Restlessness { get; init; }
        public string PressureNarrative { get; init; } = "";
        public string TopCompetitionSummary { get; init; } = "";
        public string TransferRequestSummary { get; init; } = "";
    }

    private static async Task<List<ResidentPressureReminderInfo>> ReadResidentPressureReminderInfosAsync(
        FileSystemManager fs,
        IReadOnlyList<PendingGuardianAbodeResidentTransferRequest> transferRequests)
    {
        var residentsJson = await fs.ReadFileAsync(GuardianAbodeResidentState.StatePath);
        if (string.IsNullOrWhiteSpace(residentsJson))
            return new List<ResidentPressureReminderInfo>();

        try
        {
            if (JsonNode.Parse(residentsJson) is not JsonObject residentsRoot)
                return new List<ResidentPressureReminderInfo>();

            GuardianAbodeResidentState.NormalizeShape(residentsRoot);
            JsonObject? guardiansRoot = null;
            var guardiansJson = await fs.ReadFileAsync("game_state/meta/guardians.json");
            if (!string.IsNullOrWhiteSpace(guardiansJson))
                guardiansRoot = JsonNode.Parse(guardiansJson) as JsonObject;

            var guardianNames = await ReadGuardianNameMapAsync(fs);
            var guardianPowers = GuardianAbodeResidentState.CollectGuardianAbodePowerById(guardiansRoot);
            var residentsWithPendingTransfer = transferRequests
                .Where(request => !string.IsNullOrWhiteSpace(request.ResidentId))
                .Select(request => request.ResidentId)
                .ToHashSet(StringComparer.OrdinalIgnoreCase);
            var result = new List<ResidentPressureReminderInfo>();

            if (residentsRoot[GuardianAbodeResidentState.EntriesProperty] is not JsonArray entries)
                return result;

            foreach (var resident in entries.OfType<JsonObject>())
            {
                GuardianAbodeResidentState.NormalizeResidentObject(resident);
                if (resident["isPresent"] is JsonValue isPresentValue &&
                    isPresentValue.TryGetValue<bool>(out var isPresent) &&
                    !isPresent)
                {
                    continue;
                }

                var migrationState = GetNodeString(resident["migrationState"]) ?? GuardianAbodeResidentState.MigrationStateSettled;
                if (!IsResidentPressureState(migrationState))
                    continue;

                var displayName = GetNodeString(resident["displayName"]) ?? GetNodeString(resident["residentId"]) ?? "resident";
                var guardianId = GetNodeString(resident["guardianId"]) ?? string.Empty;
                guardianPowers.TryGetValue(guardianId, out var currentAbodePower);
                var residentEntry = GuardianAbodeResidentState.ReadResidentEntry(resident, currentAbodePower);
                var hasPendingTransferRequest = !string.IsNullOrWhiteSpace(residentEntry.ResidentId) &&
                    residentsWithPendingTransfer.Contains(residentEntry.ResidentId);
                result.Add(new ResidentPressureReminderInfo
                {
                    ResidentId = residentEntry.ResidentId,
                    DisplayName = displayName,
                    GuardianName = guardianNames.TryGetValue(guardianId, out var guardianName) ? guardianName : guardianId,
                    MigrationState = migrationState,
                    AbodeDevotionLevel = residentEntry.AbodeDevotionLevel,
                    Restlessness = residentEntry.Restlessness,
                    PressureNarrative = GuardianAbodeResidentState.GetMigrationStatePressureNarrative(migrationState),
                    TopCompetitionSummary = hasPendingTransferRequest
                        ? string.Empty
                        : BuildPressureCompetitionSummary(residentEntry, guardiansRoot, residentsRoot),
                    TransferRequestSummary = hasPendingTransferRequest
                        ? "explicit transfer request already exists; see transfer request block below"
                        : string.Empty
                });
            }

            return result
                .OrderByDescending(info => GetResidentPressureSeverity(info.MigrationState))
                .ThenByDescending(info => info.Restlessness)
                .ThenBy(info => info.DisplayName, StringComparer.OrdinalIgnoreCase)
                .ToList();
        }
        catch
        {
            return new List<ResidentPressureReminderInfo>();
        }
    }

    private static async Task<Dictionary<string, string>> ReadGuardianNameMapAsync(FileSystemManager fs)
    {
        var guardiansJson = await fs.ReadFileAsync("game_state/meta/guardians.json");
        if (string.IsNullOrWhiteSpace(guardiansJson))
            return new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);

        try
        {
            if (JsonNode.Parse(guardiansJson) is not JsonObject guardiansRoot)
                return new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);

            var result = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
            if (guardiansRoot["guardians"] is JsonArray guardians)
            {
                foreach (var guardian in guardians.OfType<JsonObject>())
                {
                    var guardianId = GetNodeString(guardian["guardianId"]);
                    if (string.IsNullOrWhiteSpace(guardianId))
                        continue;

                    var guardianName = GuardianManifestation.GetDisplayName(guardian);
                    if (string.IsNullOrWhiteSpace(guardianName))
                        guardianName = GetNodeString(guardian["canonicalName"]) ?? GetNodeString(guardian["name"]) ?? guardianId;

                    result[guardianId] = guardianName;
                }
            }

            return result;
        }
        catch
        {
            return new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
        }
    }

    private static string BuildPressureCompetitionSummary(
        GuardianAbodeResidentState.ResidentEntry resident,
        JsonObject? guardiansRoot,
        JsonObject residentsRoot)
    {
        if (!string.Equals(resident.MigrationState, GuardianAbodeResidentState.MigrationStateReadyToTransfer, StringComparison.OrdinalIgnoreCase))
            return string.Empty;

        var candidates = GuardianAbodeResidentState.BuildTransferCompetitionCandidates(resident, guardiansRoot, residentsRoot);
        if (candidates.Count == 0)
            return "нет другой materialized Обители; основной путь сейчас — offscreen departure";

        var bestCandidate = candidates[0];
        var competitionText = $"{GuardianAbodeResidentState.GetTransferCompetitionLabelText(bestCandidate.CompetitionLabel)} {bestCandidate.CompetitionScore}/100";
        if (string.Equals(bestCandidate.CompetitionLabel, GuardianAbodeResidentState.TransferCompetitionLabelWeakPull, StringComparison.OrdinalIgnoreCase))
        {
            return $"сейчас нет убедительной competing Обители; strongest visible pull = {bestCandidate.TargetGuardianName} / {bestCandidate.TargetAbodeName} ({competitionText})";
        }

        return $"bestTarget={bestCandidate.TargetGuardianName} / {bestCandidate.TargetAbodeName}, competition={competitionText}, reason=\"{bestCandidate.CompetitionReason}\"";
    }

    private static bool IsResidentPressureState(string migrationState) =>
        string.Equals(migrationState, GuardianAbodeResidentState.MigrationStateWavering, StringComparison.OrdinalIgnoreCase) ||
        string.Equals(migrationState, GuardianAbodeResidentState.MigrationStateRestless, StringComparison.OrdinalIgnoreCase) ||
        string.Equals(migrationState, GuardianAbodeResidentState.MigrationStateConsideringDeparture, StringComparison.OrdinalIgnoreCase) ||
        string.Equals(migrationState, GuardianAbodeResidentState.MigrationStateReadyToTransfer, StringComparison.OrdinalIgnoreCase);

    private static int GetResidentPressureSeverity(string migrationState) =>
        (migrationState ?? string.Empty).Trim().ToLowerInvariant() switch
        {
            GuardianAbodeResidentState.MigrationStateReadyToTransfer => 4,
            GuardianAbodeResidentState.MigrationStateConsideringDeparture => 3,
            GuardianAbodeResidentState.MigrationStateRestless => 2,
            GuardianAbodeResidentState.MigrationStateWavering => 1,
            _ => 0
        };

    private static async Task EnsureResidentsRequestHealthyAsync(FileSystemManager fs)
    {
        if (!fs.FileExists(PendingResidentsRequestPath))
            return;

        var json = await fs.ReadFileAsync(PendingResidentsRequestPath);
        var status = ReadRequestBundle(
            json,
            ResidentsRequestsProperty,
            static item => JsonSerializer.Deserialize<PendingGuardianAbodeResidentsRequest>(item, JsonOpts),
            out var requests);
        if (status == RequestBundleReadStatus.Malformed)
            return;

        if (status != RequestBundleReadStatus.Valid)
            return;

        var mutableRequests = requests.ToList();
        if (mutableRequests.Count == 0)
        {
            ClearResidentsRequest(fs);
            return;
        }

        var residentJson = await fs.ReadFileAsync(GuardianAbodeResidentState.StatePath);
        if (string.IsNullOrWhiteSpace(residentJson))
            return;

        try
        {
            if (JsonNode.Parse(residentJson) is not JsonObject root)
                return;

            GuardianAbodeResidentState.NormalizeShape(root);
            var rosterReceipts = GuardianAbodeResidentState.EnsureRosterReceiptsArray(root);
            var remaining = mutableRequests
                .Where(request =>
                    string.IsNullOrWhiteSpace(request.RequestId) ||
                    GuardianAbodeResidentState.FindRosterReceipt(rosterReceipts, request.RequestId) == null)
                .ToList();
            await WriteResidentsRequestsAsync(fs, remaining);
        }
        catch
        {
            // keep request until resident state is readable again
        }
    }

    private static async Task EnsureInteractionRequestsHealthyAsync(FileSystemManager fs)
    {
        if (!fs.FileExists(PendingInteractionsRequestPath))
            return;

        var json = await fs.ReadFileAsync(PendingInteractionsRequestPath);
        var status = ReadRequestBundle(
            json,
            InteractionRequestsProperty,
            static item => JsonSerializer.Deserialize<PendingGuardianAbodeResidentInteractionRequest>(item, JsonOpts),
            out var requests);
        if (status == RequestBundleReadStatus.Malformed)
            return;

        if (status != RequestBundleReadStatus.Valid)
            return;

        var mutableRequests = requests.ToList();
        if (mutableRequests.Count == 0)
        {
            ClearInteractionRequests(fs);
            return;
        }

        var residentJson = await fs.ReadFileAsync(GuardianAbodeResidentState.StatePath);
        if (string.IsNullOrWhiteSpace(residentJson))
            return;

        try
        {
            if (JsonNode.Parse(residentJson) is not JsonObject root)
                return;

            GuardianAbodeResidentState.NormalizeShape(root);
            var receipts = GuardianAbodeResidentState.EnsureInteractionReceiptsArray(root);
            var remaining = mutableRequests
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

    private static async Task EnsureTransferRequestsHealthyAsync(FileSystemManager fs)
    {
        if (!fs.FileExists(PendingTransfersRequestPath))
            return;

        var json = await fs.ReadFileAsync(PendingTransfersRequestPath);
        var status = ReadRequestBundle(
            json,
            TransferRequestsProperty,
            static item => JsonSerializer.Deserialize<PendingGuardianAbodeResidentTransferRequest>(item, JsonOpts),
            out var requests);
        if (status == RequestBundleReadStatus.Malformed)
            return;

        if (status != RequestBundleReadStatus.Valid)
            return;

        var mutableRequests = requests.ToList();
        if (mutableRequests.Count == 0)
        {
            ClearTransferRequests(fs);
            return;
        }

        var residentJson = await fs.ReadFileAsync(GuardianAbodeResidentState.StatePath);
        if (string.IsNullOrWhiteSpace(residentJson))
            return;

        try
        {
            if (JsonNode.Parse(residentJson) is not JsonObject root)
                return;

            GuardianAbodeResidentState.NormalizeShape(root);
            var receipts = GuardianAbodeResidentState.EnsureTransferReceiptsArray(root);
            var remaining = mutableRequests
                .Where(request =>
                    !string.IsNullOrWhiteSpace(request.RequestId) &&
                    !string.IsNullOrWhiteSpace(request.ResidentId) &&
                    GuardianAbodeResidentState.FindTransferReceipt(receipts, request.RequestId) == null)
                .ToList();
            await WriteTransferRequestsAsync(fs, remaining);
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
            // Manifestation requests are Mortal-only, but afterlife runtime must not delete them:
            // a wrong-realm file is repair evidence and should block Soul Gates until resolved.
            return;
        }

        var soulJson = await fs.ReadFileAsync("game_state/meta/soul_state.json");
        if (string.IsNullOrWhiteSpace(soulJson))
            return;

        try
        {
            if (JsonNode.Parse(soulJson) is not JsonObject soulRoot)
                return;

            var lifeTransitionsJson = await fs.ReadFileAsync("game_state/control/life_transitions.json");
            var hasCanonicalTriggerLifeEnd = await CanonicalStateNormalizer.HasLifecycleAuthorizedTriggerLifeEndFromPendingSnapshotAsync(
                fs,
                lifeTransitionsJson,
                soulRoot);

            if (!GuardianPolicyContracts.TryReadStrictCurrentManifestationSoulRelicCollections(
                    soulRoot,
                    hasCanonicalTriggerLifeEnd,
                    out var currentIncarnation,
                    out _,
                    out _,
                    out _))
            {
                return;
            }

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
                var resolvedRelicIds = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
                foreach (var request in validForIncarnation)
                {
                    if (TryFindManifestedNpc(npcDoc.RootElement, validForIncarnation, request, matchedNpcIds, out var manifestedNpc))
                    {
                        var requestResolved = MarkManifestationResolved(
                            soulRoot,
                            request,
                            manifestedNpc,
                            hasCanonicalTriggerLifeEnd);
                        soulChanged |= requestResolved;
                        if (requestResolved)
                        {
                            if (!string.IsNullOrWhiteSpace(request.RelicId))
                                resolvedRelicIds.Add(request.RelicId);

                            continue;
                        }
                    }

                    remaining.Add(request);
                }

                if (soulChanged)
                {
                    var unsafeToReplayAddedRelicIds = BuildUnsafeReplayAddedRelicIds(
                        soulRoot,
                        resolvedRelicIds,
                        hasCanonicalTriggerLifeEnd);
                }
                var preManifestationJson = await fs.ReadFileAsync(PendingManifestationRequestPath);
                var preSoulJson = soulJson;
                var postSoulJson = soulChanged
                    ? GuardianPolicyContracts.CreatePatchedSoulStateWriteRoot(
                        soulRoot,
                        new GuardianPolicyContracts.SoulStatePatchConflictContext(
                            GuardianPolicyContracts.SoulStatePatchTouchedDomains.SoulRelics,
                            unsafeToReplayAddedSoulRelicIds: BuildUnsafeReplayAddedRelicIds(
                                soulRoot,
                                resolvedRelicIds,
                                hasCanonicalTriggerLifeEnd),
                            updatedSoulRelicFieldsById: BuildManifestationRelicFieldUpdates(resolvedRelicIds))).ToJsonString(JsonOpts)
                    : preSoulJson;
                var postManifestationJson = remaining.Count == 0
                    ? null
                    : JsonSerializer.Serialize(new Dictionary<string, object?>
                    {
                        [ManifestationRequestsProperty] = remaining
                    }, JsonOpts);

                if (!await CoordinatedStateWriteHelper.TryCommitAsync(
                        fs,
                        new CoordinatedStateWriteHelper.PlannedWrite("game_state/meta/soul_state.json", preSoulJson, postSoulJson),
                        new CoordinatedStateWriteHelper.PlannedWrite(PendingManifestationRequestPath, preManifestationJson, postManifestationJson)))
                {
                    return;
                }
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

    private static IReadOnlyDictionary<string, IEnumerable<string>> BuildManifestationRelicFieldUpdates(IEnumerable<string> relicIds)
    {
        var result = new Dictionary<string, IEnumerable<string>>(StringComparer.OrdinalIgnoreCase);
        foreach (var relicId in relicIds.Where(id => !string.IsNullOrWhiteSpace(id)).Distinct(StringComparer.OrdinalIgnoreCase))
            result[relicId] = ManifestationTouchedRelicFields;

        return result;
    }

    private static IReadOnlyCollection<string> BuildUnsafeReplayAddedRelicIds(JsonObject soulRoot, IEnumerable<string> relicIds)
        => BuildUnsafeReplayAddedRelicIds(soulRoot, relicIds, hasCanonicalTriggerLifeEnd: false);

    private static IReadOnlyCollection<string> BuildUnsafeReplayAddedRelicIds(
        JsonObject soulRoot,
        IEnumerable<string> relicIds,
        bool hasCanonicalTriggerLifeEnd)
    {
        var result = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        if (!GuardianPolicyContracts.TryReadStrictCurrentSoulRelicCollections(
                soulRoot,
                hasCanonicalTriggerLifeEnd,
                out var equipped,
                out var stored,
                out _) ||
            equipped == null ||
            stored == null)
        {
            return result;
        }

        foreach (var relicId in relicIds.Where(id => !string.IsNullOrWhiteSpace(id)).Distinct(StringComparer.OrdinalIgnoreCase))
        {
            var existsInEquipped = FindRelicById(equipped, relicId) != null;
            var existsInStored = FindRelicById(stored, relicId) != null;
            if (existsInEquipped || !existsInStored)
                result.Add(relicId);
        }

        return result;
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

    private static IEnumerable<JsonElement> EnumerateNpcObjects(JsonElement root) =>
        GuardianPolicyContracts.EnumerateCanonicalNpcObjects(root);

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
            request.PersonalityProfile = ReadResidentPersonalityProfile(companionSeed["personalityProfile"]);
            request.AbodeDisposition = ReadResidentAbodeDisposition(companionSeed["abodeDisposition"]);
            if (TryReadResidentMeter(companionSeed["abodeDevotionLevel"], out var abodeDevotionLevel))
                request.AbodeDevotionLevel = abodeDevotionLevel;

            var abodeDevotionTier = GetNodeString(companionSeed["abodeDevotionTier"]);
            if (!string.IsNullOrWhiteSpace(abodeDevotionTier))
                request.AbodeDevotionTier = abodeDevotionTier;

            if (TryReadResidentMeter(companionSeed["restlessness"], out var restlessness))
                request.Restlessness = restlessness;

            var migrationState = GetNodeString(companionSeed["migrationState"]);
            if (!string.IsNullOrWhiteSpace(migrationState))
                request.MigrationState = migrationState;
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
        => MarkManifestationResolved(soulRoot, request, manifestedNpc, hasCanonicalTriggerLifeEnd: false);

    private static bool MarkManifestationResolved(
        JsonObject soulRoot,
        PendingResidentCompanionManifestationRequest request,
        JsonElement manifestedNpc,
        bool hasCanonicalTriggerLifeEnd)
    {
        if (!GuardianPolicyContracts.TryReadStrictCurrentSoulRelicCollections(
                soulRoot,
                hasCanonicalTriggerLifeEnd,
                out var equipped,
                out var stored,
                out _) ||
            (equipped == null && stored == null))
        {
            return false;
        }

        var relic = FindRelicById(equipped, request.RelicId) ??
                    FindRelicById(stored, request.RelicId);
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

    private static async Task EnsureBundleFileWritableAsync<T>(
        FileSystemManager fs,
        string path,
        string propertyName,
        Func<string, T?> deserialize)
        where T : class
    {
        if (!fs.FileExists(path))
            return;

        var status = ReadRequestBundle(
            await fs.ReadFileAsync(path),
            propertyName,
            deserialize,
            out _);
        if (status == RequestBundleReadStatus.Malformed)
            throw new InvalidOperationException($"{Path.GetFileName(path)} повреждён и должен быть исправлен или очищен до записи новых запросов.");
    }

    private static RequestBundleReadStatus ReadRequestBundle<T>(
        string? json,
        string propertyName,
        Func<string, T?> deserialize,
        out IReadOnlyList<T> requests)
        where T : class
    {
        requests = Array.Empty<T>();
        if (json == null)
            return RequestBundleReadStatus.Missing;

        if (string.IsNullOrWhiteSpace(json))
            return RequestBundleReadStatus.Malformed;

        try
        {
            using var doc = JsonDocument.Parse(json);
            if (doc.RootElement.ValueKind == JsonValueKind.Object &&
                doc.RootElement.TryGetProperty(propertyName, out var requestsNode))
            {
                if (requestsNode.ValueKind != JsonValueKind.Array)
                    return RequestBundleReadStatus.Malformed;

                var parsedRequests = new List<T>();
                foreach (var item in requestsNode.EnumerateArray())
                {
                    T? request;
                    try
                    {
                        request = deserialize(item.GetRawText());
                    }
                    catch
                    {
                        return RequestBundleReadStatus.Malformed;
                    }

                    if (request == null)
                        return RequestBundleReadStatus.Malformed;

                    parsedRequests.Add(request);
                }

                requests = parsedRequests;
                return RequestBundleReadStatus.Valid;
            }

            var single = deserialize(json);
            if (single == null)
                return RequestBundleReadStatus.Malformed;

            requests = new[] { single };
            return RequestBundleReadStatus.Valid;
        }
        catch
        {
            return RequestBundleReadStatus.Malformed;
        }
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

    public static string DescribeManifestationRequestSnapshot(PendingResidentCompanionManifestationRequest request)
    {
        var parts = new List<string>();
        if (!string.IsNullOrWhiteSpace(request.PersonalityProfile?.Archetype))
            parts.Add($"архетип={request.PersonalityProfile.Archetype}");

        if (request.AbodeDevotionLevel is int devotionLevel)
        {
            var devotionTier = !string.IsNullOrWhiteSpace(request.AbodeDevotionTier)
                ? request.AbodeDevotionTier!
                : GuardianAbodeResidentState.ResolveAbodeDevotionTier(devotionLevel);
            parts.Add($"преданность={GuardianAbodeResidentState.GetAbodeDevotionTierLabel(devotionTier)} {devotionLevel}/100");
        }

        if (request.Restlessness is int restlessness)
            parts.Add($"неспокойствие={restlessness}/100");

        if (!string.IsNullOrWhiteSpace(request.MigrationState))
            parts.Add($"состояние={GuardianAbodeResidentState.GetMigrationStateLabel(request.MigrationState)}");

        return parts.Count == 0 ? string.Empty : $", {string.Join(", ", parts)}";
    }

    private static GuardianAbodeResidentState.ResidentPersonalityProfile? ReadResidentPersonalityProfile(JsonNode? node)
    {
        if (node is not JsonObject personalityProfile)
            return null;

        try
        {
            return JsonSerializer.Deserialize<GuardianAbodeResidentState.ResidentPersonalityProfile>(
                personalityProfile.ToJsonString(),
                JsonOpts);
        }
        catch
        {
            return null;
        }
    }

    private static GuardianAbodeResidentState.ResidentAbodeDisposition? ReadResidentAbodeDisposition(JsonNode? node)
    {
        if (node is not JsonObject abodeDisposition)
            return null;

        try
        {
            return JsonSerializer.Deserialize<GuardianAbodeResidentState.ResidentAbodeDisposition>(
                abodeDisposition.ToJsonString(),
                JsonOpts);
        }
        catch
        {
            return null;
        }
    }

    private static bool TryReadResidentMeter(JsonNode? node, out int value)
    {
        value = 0;
        if (node is not JsonValue jsonValue || !jsonValue.TryGetValue<int>(out value))
            return false;

        return true;
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
