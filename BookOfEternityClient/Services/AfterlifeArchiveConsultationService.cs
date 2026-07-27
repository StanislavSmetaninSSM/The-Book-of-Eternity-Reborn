using System.Text.Json;
using System.Text.Json.Nodes;
using System.Text.Json.Serialization;
using System.Text.Json.Serialization.Metadata;
using BookOfEternityClient.Core;
using Microsoft.Extensions.Logging;

namespace BookOfEternityClient.Services;

public sealed class AfterlifeArchiveConsultationService
{
    private static readonly JsonSerializerOptions JsonOpts = new()
    {
        WriteIndented = true,
        DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull,
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
        TypeInfoResolver = new DefaultJsonTypeInfoResolver(),
        Encoder = System.Text.Encodings.Web.JavaScriptEncoder.UnsafeRelaxedJsonEscaping
    };

    private readonly FileSystemManager _fs;
    private readonly ILogger<AfterlifeArchiveConsultationService> _logger;

    public AfterlifeArchiveConsultationService(
        FileSystemManager fs,
        ILogger<AfterlifeArchiveConsultationService> logger)
    {
        _fs = fs;
        _logger = logger;
    }

    public sealed record ConsultationRequestResult(
        string RequestId,
        string GuardianId,
        string GuardianName,
        string ArchiveId,
        string ArchiveTitle,
        string ArchiveEntryType,
        string Summary,
        int TargetIncarnation,
        string PendingGmAction,
        string PendingRequestPath,
        string PendingRequestJson,
        string? PreviousPendingRequestJson,
        string SoulStatePath,
        string ReservedSoulStateJson,
        string? PreviousSoulStateJson);

    public Task<ConsultationRequestResult?> CreateRequestAsync(
        string guardianId,
        string guardianName,
        string archiveId,
        int currentIncarnation,
        string? currentRealm,
        int currentTurn,
        bool commit = true) =>
        CreateRequestCoreAsync(
            writeLease: null,
            guardianId,
            guardianName,
            archiveId,
            currentIncarnation,
            currentRealm,
            currentTurn,
            commit);

    internal Task<ConsultationRequestResult?> CreateRequestAsync(
        FileSystemManager.CanonicalWriteLease writeLease,
        string guardianId,
        string guardianName,
        string archiveId,
        int currentIncarnation,
        string? currentRealm,
        int currentTurn,
        bool commit = true) =>
        CreateRequestCoreAsync(
            writeLease,
            guardianId,
            guardianName,
            archiveId,
            currentIncarnation,
            currentRealm,
            currentTurn,
            commit);

    private async Task<ConsultationRequestResult?> CreateRequestCoreAsync(
        FileSystemManager.CanonicalWriteLease? writeLease,
        string guardianId,
        string guardianName,
        string archiveId,
        int currentIncarnation,
        string? currentRealm,
        int currentTurn,
        bool commit)
    {
        if (!IsAfterlifeRealm(currentRealm) ||
            string.IsNullOrWhiteSpace(guardianId) ||
            string.IsNullOrWhiteSpace(archiveId))
        {
            return null;
        }

        var soulJson = await ReadFileAsync(writeLease, "game_state/meta/soul_state.json");
        if (string.IsNullOrWhiteSpace(soulJson))
            return null;

        var pendingRequestState = writeLease == null
            ? await AfterlifeArchiveActionState.ReadConsultationStateAsync(_fs)
            : await AfterlifeArchiveActionState.ReadConsultationStateAsync(_fs, writeLease);
        if (pendingRequestState.Exists)
            return null;

        JsonObject? soulRoot;
        try
        {
            soulRoot = JsonNode.Parse(soulJson) as JsonObject;
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Не удалось распарсить soul_state для archive consultation request");
            return null;
        }

        if (soulRoot == null)
            return null;

        var preConsultationRequestJson = await ReadFileAsync(writeLease, AfterlifeArchiveActionState.ConsultationRequestPath);
        var preSoulJson = soulJson;

        GuardianPolicyContracts.EnsureStrictCanonicalSoulStateRootsForPolicySensitiveWrite(soulRoot);
        AfterlifeArchiveState.NormalizeShape(soulRoot);
        var stored = AfterlifeArchiveState.EnsureStoredArray(soulRoot);
        var archiveEntry = stored.OfType<JsonObject>()
            .FirstOrDefault(item => string.Equals(GetNodeString(item["archiveId"]), archiveId, StringComparison.OrdinalIgnoreCase));
        if (archiveEntry == null)
            return null;

        var entryType = GetNodeString(archiveEntry["entryType"]);
        if (!AfterlifeArchiveState.IsAllowedEntryType(entryType))
            return null;

        var reputation = await ReadGuardianReputationAsync(writeLease, guardianId);
        if (reputation < 50)
            return null;

        var request = new AfterlifeArchiveActionState.PendingArchiveConsultationRequest
        {
            GuardianId = guardianId,
            GuardianName = string.IsNullOrWhiteSpace(guardianName) ? guardianId : guardianName,
            ArchiveId = archiveId,
            ArchiveTitle = GetNodeString(archiveEntry["title"]) ?? archiveId,
            ArchiveEntryType = entryType!,
            ArchiveRarity = GetNodeString(archiveEntry["rarity"]) ?? "Common",
            ArchiveSourceKind = GetNodeString(archiveEntry["sourceKind"]) ?? AfterlifeArchiveState.SourceKindCodex,
            TargetIncarnation = Math.Max(1, currentIncarnation + 1),
            CreatedAtTurn = Math.Max(0, currentTurn)
        };

        if (!AfterlifeArchiveState.TryReserveEntry(
                stored,
                archiveId,
                AfterlifeArchiveState.ReservationKindConsultation,
                request.RequestId,
                guardianId,
                request.GuardianName,
                request.CreatedAtTurn))
        {
            return null;
        }

        var postConsultationRequestJson = JsonSerializer.Serialize(request, JsonOpts);
        var postSoulJson = GuardianPolicyContracts.CreatePatchedSoulStateWriteRoot(
            soulRoot,
            new GuardianPolicyContracts.SoulStatePatchConflictContext(
                GuardianPolicyContracts.SoulStatePatchTouchedDomains.AfterlifeArchive,
                affectedArchiveIds: new[] { archiveId },
                affectedArchiveRequestIds: new[] { request.RequestId })).ToJsonString(JsonOpts);

        if (commit &&
            !await TryCommitAsync(
                    writeLease,
                    new CoordinatedStateWriteHelper.PlannedWrite(
                        AfterlifeArchiveActionState.ConsultationRequestPath,
                        preConsultationRequestJson,
                        postConsultationRequestJson,
                        RequireCurrentBaseline: true),
                    new CoordinatedStateWriteHelper.PlannedWrite(
                        "game_state/meta/soul_state.json",
                        preSoulJson,
                        postSoulJson,
                        RequireCurrentBaseline: true)))
        {
            return null;
        }

        var summary = string.Equals(entryType, AfterlifeArchiveState.EntryTypeSecretRecord, StringComparison.OrdinalIgnoreCase)
            ? "Запрос на архивную консультацию создан. Запись зарезервирована до ответа GM; затем она либо вернётся в Архив, либо превратится в заметную подсказку о нити соперника или предупреждение."
            : "Запрос на архивную консультацию создан. Запись зарезервирована до ответа GM; затем она либо вернётся в Архив, либо превратится в гарантированный архивный квест или подготовку знания.";
        var gmAction =
            $"[{AfterlifeArchiveActionState.ConsultationActionTag}] Игрок тратит архивную запись «{request.ArchiveTitle}» ({request.ArchiveEntryType}, {request.ArchiveRarity}) на консультацию у Хранителя {request.GuardianName} ({guardianId}). " +
            $"Обязательно прочитай {AfterlifeArchiveActionState.ConsultationRequestPath} как client-authored contract. " +
            "Запись уже зарезервирована клиентом в soul_state.afterlifeArchive.stored и недоступна для других действий. " +
            "Сконвертируй запрос в explicit canonical result, а не в narrative-only ответ. " +
            $"В accepted turn обязательно верни archiveActionResolutions с requestId={request.RequestId}, archiveId={request.ArchiveId}, requestedMode={AfterlifeArchiveActionState.RequestedModeConsultation}, guardianId={guardianId} и status=accepted|rejected|cancelled. " +
            "При status=accepted используй completed lore_research project с projectOrigin=archive_consultation, consultationRequestId, consultationArchiveId и effectState/projectOutcomeAudit. " +
            $"Для accepted consultation также передай machine-readable outcome fields в archiveActionResolutions: {AfterlifeArchiveActionState.ConsultationOutcomeGuaranteedArchiveQuestCount}, {AfterlifeArchiveActionState.ConsultationOutcomeQuestHookCount}, {AfterlifeArchiveActionState.ConsultationOutcomeSpecialQuestLineUnlocks}, {AfterlifeArchiveActionState.ConsultationOutcomeVisibleRivalClueBonus}, {AfterlifeArchiveActionState.ConsultationOutcomeArchiveWarningTierBonus}. " +
            "Для lore_fragment разрешены только whitelist outcomes: guaranteedArchiveQuestCount, questHookCount, specialQuestLineUnlocks. " +
            "Для secret_record разрешены только whitelist outcomes: visibleRivalClueBonus, archiveWarningTierBonus. " +
            "Не вычисляй совместимость записи с доменом клиента; просто materialize-ируй выбранный structured outcome.";

        return new ConsultationRequestResult(
            request.RequestId,
            guardianId,
            request.GuardianName,
            archiveId,
            request.ArchiveTitle,
            request.ArchiveEntryType,
            summary,
            request.TargetIncarnation,
            gmAction,
            AfterlifeArchiveActionState.ConsultationRequestPath,
            postConsultationRequestJson,
            preConsultationRequestJson,
            "game_state/meta/soul_state.json",
            postSoulJson,
            preSoulJson);
    }

    public Task<bool> CommitPreparedRequestAsync(ConsultationRequestResult result) =>
        CommitPreparedRequestCoreAsync(writeLease: null, result);

    internal Task<bool> CommitPreparedRequestAsync(
        FileSystemManager.CanonicalWriteLease writeLease,
        ConsultationRequestResult result) =>
        CommitPreparedRequestCoreAsync(writeLease, result);

    private async Task<bool> CommitPreparedRequestCoreAsync(
        FileSystemManager.CanonicalWriteLease? writeLease,
        ConsultationRequestResult result) =>
        await TryCommitAsync(
            writeLease,
            new CoordinatedStateWriteHelper.PlannedWrite(
                result.PendingRequestPath,
                result.PreviousPendingRequestJson,
                result.PendingRequestJson,
                RequireCurrentBaseline: true),
            new CoordinatedStateWriteHelper.PlannedWrite(
                result.SoulStatePath,
                result.PreviousSoulStateJson,
                result.ReservedSoulStateJson,
                RequireCurrentBaseline: true));

    private async Task<int> ReadGuardianReputationAsync(
        FileSystemManager.CanonicalWriteLease? writeLease,
        string guardianId)
    {
        var guardiansJson = await ReadFileAsync(writeLease, "game_state/meta/guardians.json");
        if (string.IsNullOrWhiteSpace(guardiansJson))
            return 0;

        try
        {
            using var doc = JsonDocument.Parse(guardiansJson);
            if (!doc.RootElement.TryGetProperty("guardians", out var guardians) || guardians.ValueKind != JsonValueKind.Array)
                return 0;

            foreach (var guardian in guardians.EnumerateArray())
            {
                if (!guardian.TryGetProperty("guardianId", out var idNode) ||
                    idNode.ValueKind != JsonValueKind.String ||
                    !string.Equals(idNode.GetString(), guardianId, StringComparison.OrdinalIgnoreCase))
                {
                    continue;
                }

                if (guardian.TryGetProperty("relationshipData", out var relationshipData) &&
                    relationshipData.ValueKind == JsonValueKind.Object &&
                    relationshipData.TryGetProperty("currentReputation", out var reputationNode) &&
                    reputationNode.ValueKind == JsonValueKind.Number &&
                    reputationNode.TryGetInt32(out var parsed))
                {
                    return parsed;
                }

                return 0;
            }
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Не удалось прочитать guardian reputation для archive consultation request");
        }

        return 0;
    }

    private Task<string?> ReadFileAsync(
        FileSystemManager.CanonicalWriteLease? writeLease,
        string path) =>
        writeLease == null
            ? _fs.ReadFileAsync(path)
            : _fs.ReadFileAsync(writeLease, path);

    private Task<bool> TryCommitAsync(
        FileSystemManager.CanonicalWriteLease? writeLease,
        params CoordinatedStateWriteHelper.PlannedWrite[] writes) =>
        writeLease == null
            ? CoordinatedStateWriteHelper.TryCommitAsync(_fs, writes)
            : CoordinatedStateWriteHelper.TryCommitAsync(_fs, writeLease, writes);

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
}
