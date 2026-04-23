using System.Text.Json;
using System.Text.Json.Nodes;
using System.Text.Json.Serialization;
using System.Text.Json.Serialization.Metadata;
using BookOfEternityClient.Core;
using Microsoft.Extensions.Logging;

namespace BookOfEternityClient.Services;

public sealed class AfterlifeArchiveProjectFuelService
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
    private readonly ILogger<AfterlifeArchiveProjectFuelService> _logger;

    public AfterlifeArchiveProjectFuelService(
        FileSystemManager fs,
        ILogger<AfterlifeArchiveProjectFuelService> logger)
    {
        _fs = fs;
        _logger = logger;
    }

    public sealed record ProjectFuelRequestResult(
        string RequestId,
        string GuardianId,
        string GuardianName,
        string ProjectId,
        string ProjectName,
        string ArchiveId,
        string ArchiveTitle,
        string Summary,
        string PendingGmAction);

    public async Task<ProjectFuelRequestResult?> CreateRequestAsync(
        string guardianId,
        string guardianName,
        string archiveId,
        string? currentRealm,
        int currentTurn)
    {
        if (!IsAfterlifeRealm(currentRealm) ||
            string.IsNullOrWhiteSpace(guardianId) ||
            string.IsNullOrWhiteSpace(archiveId))
        {
            return null;
        }

        var reputation = await ReadGuardianReputationAsync(guardianId);
        if (reputation < 50)
            return null;

        var pendingRequestState = await AfterlifeArchiveActionState.ReadProjectFuelStateAsync(_fs);
        if (pendingRequestState.Exists)
            return null;

        var soulJson = await _fs.ReadFileAsync("game_state/meta/soul_state.json");
        if (string.IsNullOrWhiteSpace(soulJson))
            return null;

        var trackerJson = await _fs.ReadFileAsync(GuardianProjectState.TrackerPath);
        if (string.IsNullOrWhiteSpace(trackerJson))
            return null;

        JsonObject? soulRoot;
        JsonObject? trackerRoot;
        try
        {
            soulRoot = JsonNode.Parse(soulJson) as JsonObject;
            trackerRoot = JsonNode.Parse(trackerJson) as JsonObject;
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Не удалось распарсить состояние для archive project fuel request");
            return null;
        }

        if (soulRoot == null || trackerRoot == null)
            return null;

        GuardianPolicyContracts.EnsureStrictCanonicalSoulStateRootsForPolicySensitiveWrite(soulRoot);
        AfterlifeArchiveState.NormalizeShape(soulRoot);
        var stored = AfterlifeArchiveState.EnsureStoredArray(soulRoot);
        var archiveEntry = stored.OfType<JsonObject>()
            .FirstOrDefault(item => string.Equals(GetNodeString(item["archiveId"]), archiveId, StringComparison.OrdinalIgnoreCase));
        if (archiveEntry == null)
            return null;

        if (trackerRoot["activeProjects"] is not JsonArray activeProjects)
            return null;

        var activeEntry = activeProjects.OfType<JsonObject>()
            .FirstOrDefault(item => string.Equals(GuardianProjectState.GetGuardianId(item), guardianId, StringComparison.OrdinalIgnoreCase));
        if (activeEntry?["project"] is not JsonObject targetProject)
            return null;

        var projectId = GetNodeString(targetProject["projectId"]) ?? "";
        var projectName = GetNodeString(targetProject["projectName"]) ?? projectId;
        if (string.IsNullOrWhiteSpace(projectId))
            return null;

        var entryType = GetNodeString(archiveEntry["entryType"]);
        if (!AfterlifeArchiveState.IsAllowedEntryType(entryType))
            return null;

        var request = new AfterlifeArchiveActionState.PendingArchiveProjectFuelRequest
        {
            GuardianId = guardianId,
            GuardianName = string.IsNullOrWhiteSpace(guardianName) ? guardianId : guardianName,
            ArchiveId = archiveId,
            ArchiveTitle = GetNodeString(archiveEntry["title"]) ?? archiveId,
            ArchiveEntryType = entryType!,
            ArchiveRarity = GetNodeString(archiveEntry["rarity"]) ?? "Common",
            ArchiveSourceKind = GetNodeString(archiveEntry["sourceKind"]) ?? AfterlifeArchiveState.SourceKindCodex,
            TargetProjectId = projectId,
            TargetProjectName = projectName,
            CreatedAtTurn = Math.Max(0, currentTurn)
        };

        if (!AfterlifeArchiveState.TryReserveEntry(
                stored,
                archiveId,
                AfterlifeArchiveState.ReservationKindProjectFuel,
                request.RequestId,
                guardianId,
                request.GuardianName,
                request.CreatedAtTurn,
                projectId,
                projectName))
        {
            return null;
        }

        await AfterlifeArchiveActionState.WriteProjectFuelAsync(_fs, request);
        await _fs.WriteFileAtomicAsync(
            "game_state/meta/soul_state.json",
            GuardianPolicyContracts.CreatePatchedSoulStateWriteRoot(
                soulRoot,
                new GuardianPolicyContracts.SoulStatePatchConflictContext(
                    GuardianPolicyContracts.SoulStatePatchTouchedDomains.AfterlifeArchive,
                    affectedArchiveIds: new[] { archiveId },
                    affectedArchiveRequestIds: new[] { request.RequestId })).ToJsonString(JsonOpts));

        var summary = string.Equals(entryType, AfterlifeArchiveState.EntryTypeSecretRecord, StringComparison.OrdinalIgnoreCase)
            ? "Запрос на подпитку архивной записью создан. Запись зарезервирована до ответа GM; затем она либо вернётся в Архив, либо превратится в ослабление давления."
            : "Запрос на подпитку архивной записью создан. Запись зарезервирована до ответа GM; затем она либо вернётся в Архив, либо превратится в ускорение работы над проектом.";
        var gmAction =
            $"[{AfterlifeArchiveActionState.ProjectFuelActionTag}] Игрок тратит архивную запись «{request.ArchiveTitle}» ({request.ArchiveEntryType}, {request.ArchiveRarity}) на подпитку активного проекта Хранителя {request.GuardianName} ({guardianId}). " +
            $"Обязательно прочитай {AfterlifeArchiveActionState.ProjectFuelRequestPath} как client-authored contract. " +
            "Запись уже зарезервирована клиентом в soul_state.afterlifeArchive.stored и недоступна для других действий. " +
            $"Применяй результат только к targetProjectId={projectId}. " +
            $"В accepted turn обязательно верни archiveActionResolutions с requestId={request.RequestId}, archiveId={request.ArchiveId}, requestedMode={AfterlifeArchiveActionState.RequestedModeProjectFuel}, guardianId={guardianId}, targetProjectId={projectId} и status=accepted|rejected|cancelled. " +
            "Используй guardianProjectUpdates / tracker/journal, а не narrative-only ответ. " +
            "Для lore_fragment разрешён только project_work effect; для secret_record — только pressure_relief. " +
            $"Для accepted project fuel дополнительно передай machine-readable resultMode={AfterlifeArchiveActionState.ProjectFuelResultModeProjectWork}|{AfterlifeArchiveActionState.ProjectFuelResultModePressureRelief} и resultAmount>0 в archiveActionResolutions. " +
            $"В journal entry eventType=assisted сохрани archiveFuelRequestId={request.RequestId} и archiveId={request.ArchiveId}, чтобы клиент мог закрыть pending request.";

        return new ProjectFuelRequestResult(
            request.RequestId,
            guardianId,
            request.GuardianName,
            projectId,
            projectName,
            archiveId,
            request.ArchiveTitle,
            summary,
            gmAction);
    }

    private async Task<int> ReadGuardianReputationAsync(string guardianId)
    {
        var guardiansJson = await _fs.ReadFileAsync("game_state/meta/guardians.json");
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
            _logger.LogWarning(ex, "Не удалось прочитать guardian reputation для archive project fuel request");
        }

        return 0;
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
}
