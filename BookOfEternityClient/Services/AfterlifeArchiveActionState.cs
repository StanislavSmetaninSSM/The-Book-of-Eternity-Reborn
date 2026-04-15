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

    private enum PendingArchiveRequestCleanupOutcome
    {
        NoRequest,
        RequestClearedAfterReservationRelease,
        RequestClearedAfterMaterialization,
        RequestRetainedPendingUnreadableSoulState,
        RequestRetainedPendingUnreconciledReservation,
        RequestRetainedMalformedInsufficientIdentity,
        RequestRetainedPendingActiveRequest
    }

    private sealed record PendingArchiveRequestIdentity(
        string? RequestId,
        string? ArchiveId,
        string? RequestedMode,
        bool IsStructurallyValid);

    internal enum PendingArchiveRequestReadStatus
    {
        Missing,
        Valid,
        Malformed
    }

    internal sealed record PendingArchiveRequestReadResult<TRequest>(
        PendingArchiveRequestReadStatus Status,
        TRequest? Request,
        string? RawJson)
        where TRequest : class
    {
        internal bool Exists => Status != PendingArchiveRequestReadStatus.Missing;
        internal bool IsValid => Status == PendingArchiveRequestReadStatus.Valid;
        internal bool IsMalformed => Status == PendingArchiveRequestReadStatus.Malformed;
    }

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

    public static async Task<PendingArchiveConsultationRequest?> ReadConsultationAsync(FileSystemManager fs) =>
        (await ReadConsultationStateAsync(fs)).Request;

    public static async Task<PendingArchiveProjectFuelRequest?> ReadProjectFuelAsync(FileSystemManager fs) =>
        (await ReadProjectFuelStateAsync(fs)).Request;

    internal static Task<PendingArchiveRequestReadResult<PendingArchiveConsultationRequest>> ReadConsultationStateAsync(FileSystemManager fs) =>
        ReadPendingRequestStateAsync<PendingArchiveConsultationRequest>(fs, ConsultationRequestPath, BuildConsultationRequestIdentity);

    internal static Task<PendingArchiveRequestReadResult<PendingArchiveProjectFuelRequest>> ReadProjectFuelStateAsync(FileSystemManager fs) =>
        ReadPendingRequestStateAsync<PendingArchiveProjectFuelRequest>(fs, ProjectFuelRequestPath, BuildProjectFuelRequestIdentity);

    internal static PendingArchiveRequestReadResult<PendingArchiveConsultationRequest> ParseConsultationState(string? requestJson) =>
        ParsePendingRequestState<PendingArchiveConsultationRequest>(requestJson, BuildConsultationRequestIdentity);

    internal static PendingArchiveRequestReadResult<PendingArchiveProjectFuelRequest> ParseProjectFuelState(string? requestJson) =>
        ParsePendingRequestState<PendingArchiveProjectFuelRequest>(requestJson, BuildProjectFuelRequestIdentity);

    public static void ClearConsultation(FileSystemManager fs) => fs.DeleteFile(ConsultationRequestPath);

    public static void ClearProjectFuel(FileSystemManager fs) => fs.DeleteFile(ProjectFuelRequestPath);

    public static async Task EnsureHealthyAsync(FileSystemManager fs, string? currentRealm)
    {
        if (!IsAfterlifeRealm(currentRealm))
        {
            await ReconcileConsultationRequestAsync(fs, allowPendingReservationRelease: true);
            await ReconcileProjectFuelRequestAsync(fs, allowPendingReservationRelease: true);
            return;
        }

        await ReconcileConsultationRequestAsync(fs, allowPendingReservationRelease: false);
        await ReconcileProjectFuelRequestAsync(fs, allowPendingReservationRelease: false);
    }

    private static async Task<PendingArchiveRequestCleanupOutcome> ReconcileConsultationRequestAsync(
        FileSystemManager fs,
        bool allowPendingReservationRelease)
    {
        if (!fs.FileExists(ConsultationRequestPath))
            return PendingArchiveRequestCleanupOutcome.NoRequest;

        var requestJson = await fs.ReadFileAsync(ConsultationRequestPath);
        var identity = BuildConsultationRequestIdentity(requestJson);
        return await ReconcileArchiveRequestAsync(
            fs,
            ConsultationRequestPath,
            identity,
            allowPendingReservationRelease,
            ClearConsultation);
    }

    private static async Task<PendingArchiveRequestCleanupOutcome> ReconcileProjectFuelRequestAsync(
        FileSystemManager fs,
        bool allowPendingReservationRelease)
    {
        if (!fs.FileExists(ProjectFuelRequestPath))
            return PendingArchiveRequestCleanupOutcome.NoRequest;

        var requestJson = await fs.ReadFileAsync(ProjectFuelRequestPath);
        var identity = BuildProjectFuelRequestIdentity(requestJson);
        return await ReconcileArchiveRequestAsync(
            fs,
            ProjectFuelRequestPath,
            identity,
            allowPendingReservationRelease,
            ClearProjectFuel);
    }

    private static async Task<PendingArchiveRequestCleanupOutcome> ReconcileArchiveRequestAsync(
        FileSystemManager fs,
        string requestPath,
        PendingArchiveRequestIdentity identity,
        bool allowPendingReservationRelease,
        Action<FileSystemManager> clearRequest)
    {
        if (!fs.FileExists(requestPath))
            return PendingArchiveRequestCleanupOutcome.NoRequest;

        var soulJson = await fs.ReadFileAsync("game_state/meta/soul_state.json");
        if (string.IsNullOrWhiteSpace(soulJson))
            return PendingArchiveRequestCleanupOutcome.RequestRetainedPendingUnreadableSoulState;

        try
        {
            if (JsonNode.Parse(soulJson) is not JsonObject soulRoot)
                return PendingArchiveRequestCleanupOutcome.RequestRetainedPendingUnreadableSoulState;

            GuardianPolicyContracts.EnsureStrictCanonicalSoulStateRootsForPolicySensitiveWrite(soulRoot);
            AfterlifeArchiveState.NormalizeShape(soulRoot);
            var stored = AfterlifeArchiveState.EnsureStoredArray(soulRoot);
            var receipts = AfterlifeArchiveState.EnsureActionReceiptsArray(soulRoot);
            var hasReceipt = identity.IsStructurallyValid &&
                             AfterlifeArchiveState.HasActionReceipt(
                                 receipts,
                                 identity.RequestId!,
                                 identity.ArchiveId!,
                                 identity.RequestedMode!);
            var reservationReleased = false;
            string? affectedArchiveId = null;
            string? affectedRequestId = null;
            if (identity.IsStructurallyValid && (hasReceipt || allowPendingReservationRelease))
            {
                reservationReleased = TryReleaseMatchingReservation(
                    stored,
                    identity,
                    out affectedArchiveId,
                    out affectedRequestId);
            }

            if (reservationReleased)
            {
                await fs.WriteFileAtomicAsync(
                    "game_state/meta/soul_state.json",
                    GuardianPolicyContracts.CreatePatchedSoulStateWriteRoot(
                        soulRoot,
                        new GuardianPolicyContracts.SoulStatePatchConflictContext(
                            GuardianPolicyContracts.SoulStatePatchTouchedDomains.AfterlifeArchive,
                            affectedArchiveIds: new[] { affectedArchiveId! },
                            affectedArchiveRequestIds: new[] { affectedRequestId! })).ToJsonString(JsonOpts));
            }

            if (hasReceipt)
            {
                if (reservationReleased || CanClearRequestAfterReceiptMaterialization(stored, identity))
                {
                    clearRequest(fs);
                    return reservationReleased
                        ? PendingArchiveRequestCleanupOutcome.RequestClearedAfterReservationRelease
                        : PendingArchiveRequestCleanupOutcome.RequestClearedAfterMaterialization;
                }

                return PendingArchiveRequestCleanupOutcome.RequestRetainedPendingUnreconciledReservation;
            }

            if (!allowPendingReservationRelease)
            {
                return identity.IsStructurallyValid
                    ? PendingArchiveRequestCleanupOutcome.RequestRetainedPendingActiveRequest
                    : PendingArchiveRequestCleanupOutcome.RequestRetainedMalformedInsufficientIdentity;
            }

            if (reservationReleased)
            {
                clearRequest(fs);
                return PendingArchiveRequestCleanupOutcome.RequestClearedAfterReservationRelease;
            }

            return !string.IsNullOrWhiteSpace(identity.RequestId)
                ? PendingArchiveRequestCleanupOutcome.RequestRetainedPendingUnreconciledReservation
                : PendingArchiveRequestCleanupOutcome.RequestRetainedMalformedInsufficientIdentity;
        }
        catch
        {
            return PendingArchiveRequestCleanupOutcome.RequestRetainedPendingUnreadableSoulState;
        }
    }

    private static async Task<PendingArchiveRequestReadResult<TRequest>> ReadPendingRequestStateAsync<TRequest>(
        FileSystemManager fs,
        string requestPath,
        Func<string?, PendingArchiveRequestIdentity> buildIdentity)
        where TRequest : class
    {
        if (!fs.FileExists(requestPath))
            return new PendingArchiveRequestReadResult<TRequest>(PendingArchiveRequestReadStatus.Missing, null, null);

        var json = await fs.ReadFileAsync(requestPath);
        return ParsePendingRequestState<TRequest>(json, buildIdentity);
    }

    private static PendingArchiveRequestReadResult<TRequest> ParsePendingRequestState<TRequest>(
        string? json,
        Func<string?, PendingArchiveRequestIdentity> buildIdentity)
        where TRequest : class
    {
        if (string.IsNullOrWhiteSpace(json))
            return new PendingArchiveRequestReadResult<TRequest>(PendingArchiveRequestReadStatus.Malformed, null, json);

        try
        {
            var request = JsonSerializer.Deserialize<TRequest>(json, JsonOpts);
            if (request == null)
                return new PendingArchiveRequestReadResult<TRequest>(PendingArchiveRequestReadStatus.Malformed, null, json);

            var identity = buildIdentity(json);
            var status = identity.IsStructurallyValid
                ? PendingArchiveRequestReadStatus.Valid
                : PendingArchiveRequestReadStatus.Malformed;
            return new PendingArchiveRequestReadResult<TRequest>(status, request, json);
        }
        catch
        {
            return new PendingArchiveRequestReadResult<TRequest>(PendingArchiveRequestReadStatus.Malformed, null, json);
        }
    }

    private static PendingArchiveRequestIdentity BuildConsultationRequestIdentity(string? requestJson)
    {
        if (string.IsNullOrWhiteSpace(requestJson))
            return new PendingArchiveRequestIdentity(null, null, null, false);

        try
        {
            if (JsonNode.Parse(requestJson) is not JsonObject requestRoot)
                return new PendingArchiveRequestIdentity(null, null, null, false);

            var requestId = GetNodeString(requestRoot["requestId"]);
            var archiveId = GetNodeString(requestRoot["archiveId"]);
            var requestedMode = GetNodeString(requestRoot["requestedMode"]);
            var isStructurallyValid =
                !string.IsNullOrWhiteSpace(requestId) &&
                !string.IsNullOrWhiteSpace(GetNodeString(requestRoot["guardianId"])) &&
                !string.IsNullOrWhiteSpace(archiveId) &&
                AfterlifeArchiveState.IsAllowedEntryType(GetNodeString(requestRoot["archiveEntryType"])) &&
                AfterlifeArchiveState.IsSupportedArchiveRarity(GetNodeString(requestRoot["archiveRarity"])) &&
                string.Equals(requestedMode, RequestedModeConsultation, StringComparison.OrdinalIgnoreCase) &&
                GetNodeInt(requestRoot["targetIncarnation"]) > 0;

            return new PendingArchiveRequestIdentity(requestId, archiveId, requestedMode, isStructurallyValid);
        }
        catch
        {
            return new PendingArchiveRequestIdentity(null, null, null, false);
        }
    }

    private static PendingArchiveRequestIdentity BuildProjectFuelRequestIdentity(string? requestJson)
    {
        if (string.IsNullOrWhiteSpace(requestJson))
            return new PendingArchiveRequestIdentity(null, null, null, false);

        try
        {
            if (JsonNode.Parse(requestJson) is not JsonObject requestRoot)
                return new PendingArchiveRequestIdentity(null, null, null, false);

            var requestId = GetNodeString(requestRoot["requestId"]);
            var archiveId = GetNodeString(requestRoot["archiveId"]);
            var requestedMode = GetNodeString(requestRoot["requestedMode"]);
            var isStructurallyValid =
                !string.IsNullOrWhiteSpace(requestId) &&
                !string.IsNullOrWhiteSpace(GetNodeString(requestRoot["guardianId"])) &&
                !string.IsNullOrWhiteSpace(archiveId) &&
                AfterlifeArchiveState.IsAllowedEntryType(GetNodeString(requestRoot["archiveEntryType"])) &&
                AfterlifeArchiveState.IsSupportedArchiveRarity(GetNodeString(requestRoot["archiveRarity"])) &&
                string.Equals(requestedMode, RequestedModeProjectFuel, StringComparison.OrdinalIgnoreCase) &&
                !string.IsNullOrWhiteSpace(GetNodeString(requestRoot["targetProjectId"]));

            return new PendingArchiveRequestIdentity(requestId, archiveId, requestedMode, isStructurallyValid);
        }
        catch
        {
            return new PendingArchiveRequestIdentity(null, null, null, false);
        }
    }

    private static bool TryReleaseMatchingReservation(
        JsonArray stored,
        PendingArchiveRequestIdentity identity,
        out string? affectedArchiveId,
        out string? affectedRequestId)
    {
        affectedArchiveId = null;
        affectedRequestId = null;
        if (!identity.IsStructurallyValid ||
            string.IsNullOrWhiteSpace(identity.RequestId) ||
            string.IsNullOrWhiteSpace(identity.ArchiveId) ||
            string.IsNullOrWhiteSpace(identity.RequestedMode))
        {
            return false;
        }

        var matchedEntry = AfterlifeArchiveState.FindEntry(stored, identity.ArchiveId);
        if (matchedEntry == null)
            return false;

        var reservation = AfterlifeArchiveState.GetReservationObject(matchedEntry);
        if (!AfterlifeArchiveState.ReservationMatchesRequest(reservation, identity.RequestId, identity.RequestedMode))
            return false;

        AfterlifeArchiveState.ClearReservation(matchedEntry);
        affectedArchiveId = identity.ArchiveId;
        affectedRequestId = identity.RequestId;
        return true;
    }

    private static bool CanClearRequestAfterReceiptMaterialization(
        JsonArray stored,
        PendingArchiveRequestIdentity identity)
    {
        if (!identity.IsStructurallyValid ||
            string.IsNullOrWhiteSpace(identity.RequestId) ||
            string.IsNullOrWhiteSpace(identity.ArchiveId) ||
            string.IsNullOrWhiteSpace(identity.RequestedMode))
        {
            return false;
        }

        foreach (var entry in stored.OfType<JsonObject>())
        {
            var entryArchiveId = GetNodeString(entry["archiveId"]);
            var reservation = AfterlifeArchiveState.GetReservationObject(entry);
            if (reservation == null)
                continue;

            if (string.Equals(entryArchiveId, identity.ArchiveId, StringComparison.OrdinalIgnoreCase))
                return false;

            if (string.Equals(GetNodeString(reservation["requestId"]), identity.RequestId, StringComparison.OrdinalIgnoreCase))
                return false;
        }

        return true;
    }

    private static int GetNodeInt(JsonNode? node)
    {
        if (node is JsonValue value && value.TryGetValue<int>(out var number))
            return number;

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
