using System.Text.Json;
using System.Text.Json.Nodes;
using System.Text.Json.Serialization;
using BookOfEternityClient.Configuration;
using BookOfEternityClient.Core;

namespace BookOfEternityClient.Services;

internal static class AfterlifeArchiveActionState
{
    public const string ConsultationRequestPath = "game_state/control/pending_archive_consultation_request.json";
    public const string ProjectFuelRequestPath = "game_state/control/pending_archive_project_fuel_request.json";
    public const string ConsultationActionTag = "ARCHIVE_CONSULTATION_REQUEST";
    public const string ProjectFuelActionTag = "ARCHIVE_PROJECT_FUEL_REQUEST";
    public const string RequestedModeConsultation = AfterlifeArchiveState.ReservationKindConsultation;
    public const string RequestedModeProjectFuel = AfterlifeArchiveState.ReservationKindProjectFuel;
    public const string ResolutionStatusAccepted = "accepted";
    public const string ResolutionStatusRejected = "rejected";
    public const string ResolutionStatusCancelled = "cancelled";
    public const string ProjectFuelResultModeProjectWork = "project_work";
    public const string ProjectFuelResultModePressureRelief = "pressure_relief";
    public const string ConsultationOutcomeGuaranteedArchiveQuestCount = "guaranteedArchiveQuestCount";
    public const string ConsultationOutcomeQuestHookCount = "questHookCount";
    public const string ConsultationOutcomeSpecialQuestLineUnlocks = "specialQuestLineUnlocks";
    public const string ConsultationOutcomeVisibleRivalClueBonus = "visibleRivalClueBonus";
    public const string ConsultationOutcomeArchiveWarningTierBonus = "archiveWarningTierBonus";

    private static readonly string[] ConsultationOutcomeFields =
    {
        ConsultationOutcomeGuaranteedArchiveQuestCount,
        ConsultationOutcomeQuestHookCount,
        ConsultationOutcomeSpecialQuestLineUnlocks,
        ConsultationOutcomeVisibleRivalClueBonus,
        ConsultationOutcomeArchiveWarningTierBonus
    };

    private static readonly JsonSerializerOptions JsonOpts = SharedJsonOptions.PrettyCamelCaseUnsafeRelaxed;

    public sealed class PendingArchiveConsultationRequest
    {
        [JsonPropertyName("requestId")]
        public string RequestId { get; set; } = $"archive_consult_{Guid.NewGuid():N}";

        [JsonPropertyName("guardianId")]
        public string GuardianId { get; set; } = "";

        [JsonPropertyName("guardianName")]
        public string GuardianName { get; set; } = "";

        [JsonPropertyName("archiveId")]
        public string ArchiveId { get; set; } = "";

        [JsonPropertyName("archiveTitle")]
        public string ArchiveTitle { get; set; } = "";

        [JsonPropertyName("archiveEntryType")]
        public string ArchiveEntryType { get; set; } = "";

        [JsonPropertyName("archiveRarity")]
        public string ArchiveRarity { get; set; } = "";

        [JsonPropertyName("archiveSourceKind")]
        public string ArchiveSourceKind { get; set; } = "";

        [JsonPropertyName("targetIncarnation")]
        public int TargetIncarnation { get; set; }

        [JsonPropertyName("createdAtTurn")]
        public int CreatedAtTurn { get; set; }

        [JsonPropertyName("createdAtUtc")]
        public string CreatedAtUtc { get; set; } = DateTime.UtcNow.ToString("o");

        [JsonPropertyName("requestedMode")]
        public string RequestedMode { get; set; } = RequestedModeConsultation;
    }

    public sealed class PendingArchiveProjectFuelRequest
    {
        [JsonPropertyName("requestId")]
        public string RequestId { get; set; } = $"archive_fuel_{Guid.NewGuid():N}";

        [JsonPropertyName("guardianId")]
        public string GuardianId { get; set; } = "";

        [JsonPropertyName("guardianName")]
        public string GuardianName { get; set; } = "";

        [JsonPropertyName("archiveId")]
        public string ArchiveId { get; set; } = "";

        [JsonPropertyName("archiveTitle")]
        public string ArchiveTitle { get; set; } = "";

        [JsonPropertyName("archiveEntryType")]
        public string ArchiveEntryType { get; set; } = "";

        [JsonPropertyName("archiveRarity")]
        public string ArchiveRarity { get; set; } = "";

        [JsonPropertyName("archiveSourceKind")]
        public string ArchiveSourceKind { get; set; } = "";

        [JsonPropertyName("targetProjectId")]
        public string TargetProjectId { get; set; } = "";

        [JsonPropertyName("targetProjectName")]
        public string TargetProjectName { get; set; } = "";

        [JsonPropertyName("createdAtTurn")]
        public int CreatedAtTurn { get; set; }

        [JsonPropertyName("createdAtUtc")]
        public string CreatedAtUtc { get; set; } = DateTime.UtcNow.ToString("o");

        [JsonPropertyName("requestedMode")]
        public string RequestedMode { get; set; } = RequestedModeProjectFuel;
    }

    public static bool IsSupportedRequestedMode(string? requestedMode) =>
        string.Equals(requestedMode, RequestedModeConsultation, StringComparison.OrdinalIgnoreCase) ||
        string.Equals(requestedMode, RequestedModeProjectFuel, StringComparison.OrdinalIgnoreCase);

    public static bool IsSupportedResolutionStatus(string? status) =>
        string.Equals(status, ResolutionStatusAccepted, StringComparison.OrdinalIgnoreCase) ||
        string.Equals(status, ResolutionStatusRejected, StringComparison.OrdinalIgnoreCase) ||
        string.Equals(status, ResolutionStatusCancelled, StringComparison.OrdinalIgnoreCase);

    public static bool IsSupportedProjectFuelResultMode(string? resultMode) =>
        string.Equals(resultMode, ProjectFuelResultModeProjectWork, StringComparison.OrdinalIgnoreCase) ||
        string.Equals(resultMode, ProjectFuelResultModePressureRelief, StringComparison.OrdinalIgnoreCase);

    public static IReadOnlyList<string> GetConsultationOutcomeFields() => ConsultationOutcomeFields;

    public static async Task WriteConsultationAsync(FileSystemManager fs, PendingArchiveConsultationRequest request)
    {
        await fs.WriteFileAtomicAsync(ConsultationRequestPath, JsonSerializer.Serialize(request, JsonOpts));
    }

    public static async Task WriteProjectFuelAsync(FileSystemManager fs, PendingArchiveProjectFuelRequest request)
    {
        await fs.WriteFileAtomicAsync(ProjectFuelRequestPath, JsonSerializer.Serialize(request, JsonOpts));
    }

    public static async Task<PendingArchiveConsultationRequest?> ReadConsultationAsync(FileSystemManager fs)
    {
        var json = await fs.ReadFileAsync(ConsultationRequestPath);
        if (string.IsNullOrWhiteSpace(json))
            return null;

        try
        {
            return JsonSerializer.Deserialize<PendingArchiveConsultationRequest>(json, JsonOpts);
        }
        catch
        {
            return null;
        }
    }

    public static async Task<PendingArchiveProjectFuelRequest?> ReadProjectFuelAsync(FileSystemManager fs)
    {
        var json = await fs.ReadFileAsync(ProjectFuelRequestPath);
        if (string.IsNullOrWhiteSpace(json))
            return null;

        try
        {
            return JsonSerializer.Deserialize<PendingArchiveProjectFuelRequest>(json, JsonOpts);
        }
        catch
        {
            return null;
        }
    }

    public static void ClearConsultation(FileSystemManager fs) => fs.DeleteFile(ConsultationRequestPath);

    public static void ClearProjectFuel(FileSystemManager fs) => fs.DeleteFile(ProjectFuelRequestPath);

    public static async Task EnsureHealthyAsync(FileSystemManager fs, string? currentRealm)
    {
        if (!IsAfterlifeRealm(currentRealm))
        {
            await ReleasePendingReservationsAsync(fs);
            ClearConsultation(fs);
            ClearProjectFuel(fs);
            return;
        }

        await EnsureConsultationHealthyAsync(fs);
        await EnsureProjectFuelHealthyAsync(fs);
    }

    private static async Task EnsureConsultationHealthyAsync(FileSystemManager fs)
    {
        if (!fs.FileExists(ConsultationRequestPath))
            return;

        var request = await ReadConsultationAsync(fs);
        if (request == null ||
            string.IsNullOrWhiteSpace(request.RequestId) ||
            string.IsNullOrWhiteSpace(request.GuardianId) ||
            string.IsNullOrWhiteSpace(request.ArchiveId) ||
            !AfterlifeArchiveState.IsAllowedEntryType(request.ArchiveEntryType) ||
            !AfterlifeArchiveState.IsSupportedArchiveRarity(request.ArchiveRarity) ||
            !string.Equals(request.RequestedMode, RequestedModeConsultation, StringComparison.OrdinalIgnoreCase) ||
            request.TargetIncarnation <= 0)
        {
            ClearConsultation(fs);
            return;
        }

        var soulJson = await fs.ReadFileAsync("game_state/meta/soul_state.json");
        if (string.IsNullOrWhiteSpace(soulJson))
            return;

        try
        {
            if (JsonNode.Parse(soulJson) is not JsonObject soulRoot)
            {
                return;
            }

            AfterlifeArchiveState.NormalizeShape(soulRoot);
            var receipts = AfterlifeArchiveState.EnsureActionReceiptsArray(soulRoot);
            if (AfterlifeArchiveState.HasActionReceipt(receipts, request.RequestId))
                ClearConsultation(fs);
        }
        catch
        {
            // keep request until soul_state is readable again
        }
    }

    private static async Task EnsureProjectFuelHealthyAsync(FileSystemManager fs)
    {
        if (!fs.FileExists(ProjectFuelRequestPath))
            return;

        var request = await ReadProjectFuelAsync(fs);
        if (request == null ||
            string.IsNullOrWhiteSpace(request.RequestId) ||
            string.IsNullOrWhiteSpace(request.GuardianId) ||
            string.IsNullOrWhiteSpace(request.ArchiveId) ||
            !AfterlifeArchiveState.IsAllowedEntryType(request.ArchiveEntryType) ||
            !AfterlifeArchiveState.IsSupportedArchiveRarity(request.ArchiveRarity) ||
            !string.Equals(request.RequestedMode, RequestedModeProjectFuel, StringComparison.OrdinalIgnoreCase) ||
            string.IsNullOrWhiteSpace(request.TargetProjectId))
        {
            ClearProjectFuel(fs);
            return;
        }

        var soulJson = await fs.ReadFileAsync("game_state/meta/soul_state.json");
        if (string.IsNullOrWhiteSpace(soulJson))
            return;

        try
        {
            if (JsonNode.Parse(soulJson) is not JsonObject soulRoot)
            {
                return;
            }

            AfterlifeArchiveState.NormalizeShape(soulRoot);
            var receipts = AfterlifeArchiveState.EnsureActionReceiptsArray(soulRoot);
            if (AfterlifeArchiveState.HasActionReceipt(receipts, request.RequestId))
                ClearProjectFuel(fs);
        }
        catch
        {
            // keep request until soul_state is readable again
        }
    }

    private static async Task ReleasePendingReservationsAsync(FileSystemManager fs)
    {
        var consultation = await ReadConsultationAsync(fs);
        var projectFuel = await ReadProjectFuelAsync(fs);
        if (consultation == null && projectFuel == null)
            return;

        var soulJson = await fs.ReadFileAsync("game_state/meta/soul_state.json");
        if (string.IsNullOrWhiteSpace(soulJson))
            return;

        try
        {
            if (JsonNode.Parse(soulJson) is not JsonObject soulRoot)
                return;

            AfterlifeArchiveState.NormalizeShape(soulRoot);
            var stored = AfterlifeArchiveState.EnsureStoredArray(soulRoot);
            var changed = false;

            if (consultation != null)
            {
                var entry = AfterlifeArchiveState.FindEntry(stored, consultation.ArchiveId);
                if (AfterlifeArchiveState.ReservationMatchesRequest(
                        AfterlifeArchiveState.GetReservationObject(entry),
                        consultation.RequestId,
                        RequestedModeConsultation))
                {
                    AfterlifeArchiveState.ClearReservation(entry!);
                    changed = true;
                }
            }

            if (projectFuel != null)
            {
                var entry = AfterlifeArchiveState.FindEntry(stored, projectFuel.ArchiveId);
                if (AfterlifeArchiveState.ReservationMatchesRequest(
                        AfterlifeArchiveState.GetReservationObject(entry),
                        projectFuel.RequestId,
                        RequestedModeProjectFuel))
                {
                    AfterlifeArchiveState.ClearReservation(entry!);
                    changed = true;
                }
            }

            if (changed)
            {
                await fs.WriteFileAtomicAsync(
                    "game_state/meta/soul_state.json",
                    soulRoot.ToJsonString(new JsonSerializerOptions
                    {
                        WriteIndented = true,
                        DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull,
                        PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
                        Encoder = System.Text.Encodings.Web.JavaScriptEncoder.UnsafeRelaxedJsonEscaping
                    }));
            }
        }
        catch
        {
            // keep best-effort cleanup only
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
}
