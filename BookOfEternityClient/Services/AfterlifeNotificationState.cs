using System.Text.Json;
using System.Text.Json.Nodes;
using System.Text.Json.Serialization;
using BookOfEternityClient.Configuration;
using BookOfEternityClient.Core;

namespace BookOfEternityClient.Services;

internal static class AfterlifeNotificationState
{
    public const string NotificationsPath = "game_state/control/afterlife_notifications.json";
    private const string NotificationsProperty = "notifications";
    private const int MaxRetainedReadNotifications = 100;

    public const string StatusUnread = "unread";
    public const string StatusRead = "read";

    public const string TypeGuardianTradeInventoryReady = "guardian_trade_inventory_ready";
    public const string TypeGuardianQuestAvailable = "guardian_quest_available";
    public const string TypeAbodeResidentsReady = "abode_residents_ready";
    public const string TypeAbodeResidentQuestAvailable = "abode_resident_quest_available";
    public const string TypeAbodeResidentRelicGranted = "abode_resident_relic_granted";
    public const string TypeAbodeResidentManifestationReady = "abode_resident_manifestation_ready";
    public const string TypeCompanionImprintManifestationReady = "companion_imprint_manifestation_ready";
    public const string TypeAbodeResidentTalkAnswered = "abode_resident_talk_answered";
    public const string TypeAbodeResidentHistoryRevealed = "abode_resident_history_revealed";
    public const string TypeAbodeResidentHistoryRefused = "abode_resident_history_refused";
    public const string TypeArchiveConsultationAccepted = "archive_consultation_accepted";
    public const string TypeArchiveConsultationRejected = "archive_consultation_rejected";
    public const string TypeArchiveConsultationCancelled = "archive_consultation_cancelled";
    public const string TypeArchiveProjectFuelAccepted = "archive_project_fuel_accepted";
    public const string TypeArchiveProjectFuelRejected = "archive_project_fuel_rejected";
    public const string TypeArchiveProjectFuelCancelled = "archive_project_fuel_cancelled";

    private static readonly HashSet<string> AllowedStatuses = new(StringComparer.OrdinalIgnoreCase)
    {
        StatusUnread,
        StatusRead
    };

    private static readonly HashSet<string> AllowedTypes = new(StringComparer.OrdinalIgnoreCase)
    {
        TypeGuardianTradeInventoryReady,
        TypeGuardianQuestAvailable,
        TypeAbodeResidentsReady,
        TypeAbodeResidentQuestAvailable,
        TypeAbodeResidentRelicGranted,
        TypeAbodeResidentManifestationReady,
        TypeCompanionImprintManifestationReady,
        TypeAbodeResidentTalkAnswered,
        TypeAbodeResidentHistoryRevealed,
        TypeAbodeResidentHistoryRefused,
        TypeArchiveConsultationAccepted,
        TypeArchiveConsultationRejected,
        TypeArchiveConsultationCancelled,
        TypeArchiveProjectFuelAccepted,
        TypeArchiveProjectFuelRejected,
        TypeArchiveProjectFuelCancelled
    };

    private static readonly JsonSerializerOptions JsonOpts = SharedJsonOptions.PrettyCamelCaseUnsafeRelaxed;

    public sealed class NotificationEntry
    {
        [JsonPropertyName("notificationId")]
        public string NotificationId { get; set; } = "";

        [JsonPropertyName("notificationType")]
        public string NotificationType { get; set; } = "";

        [JsonPropertyName("requestId")]
        public string RequestId { get; set; } = "";

        [JsonPropertyName("status")]
        public string Status { get; set; } = StatusUnread;

        [JsonPropertyName("guardianId")]
        public string GuardianId { get; set; } = "";

        [JsonPropertyName("guardianName")]
        public string GuardianName { get; set; } = "";

        [JsonPropertyName("archiveId")]
        public string ArchiveId { get; set; } = "";

        [JsonPropertyName("archiveTitle")]
        public string ArchiveTitle { get; set; } = "";

        [JsonPropertyName("targetProjectId")]
        public string TargetProjectId { get; set; } = "";

        [JsonPropertyName("targetProjectName")]
        public string TargetProjectName { get; set; } = "";

        [JsonPropertyName("summary")]
        public string Summary { get; set; } = "";

        [JsonPropertyName("createdAtTurn")]
        public int CreatedAtTurn { get; set; }

        [JsonPropertyName("createdAtUtc")]
        public string CreatedAtUtc { get; set; } = DateTime.UtcNow.ToString("o");
    }

    public static async Task EnsureHealthyAsync(FileSystemManager fs)
    {
        if (!fs.FileExists(NotificationsPath))
            return;

        var root = await ReadRootAsync(fs);
        NormalizeShape(root);
        var notifications = EnsureNotificationsArray(root);
        var seenIds = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        var changed = false;

        for (var i = notifications.Count - 1; i >= 0; i--)
        {
            if (notifications[i] is not JsonObject notification)
            {
                notifications.RemoveAt(i);
                changed = true;
                continue;
            }

            var notificationId = GetNodeString(notification["notificationId"]);
            var notificationType = GetNodeString(notification["notificationType"]);
            var requestId = GetNodeString(notification["requestId"]);
            var status = GetNodeString(notification["status"]);
            var summary = GetNodeString(notification["summary"]);

            if (string.IsNullOrWhiteSpace(notificationId) ||
                string.IsNullOrWhiteSpace(requestId) ||
                !IsSupportedType(notificationType) ||
                !IsSupportedStatus(status) ||
                string.IsNullOrWhiteSpace(summary) ||
                !seenIds.Add(notificationId!))
            {
                notifications.RemoveAt(i);
                changed = true;
                continue;
            }

            if (!DateTimeOffset.TryParse(GetNodeString(notification["createdAtUtc"]), out _))
            {
                notification["createdAtUtc"] = DateTime.UtcNow.ToString("o");
                changed = true;
            }
        }

        changed |= TrimReadNotifications(notifications);

        if (changed)
            await WriteRootAsync(fs, root);
    }

    public static async Task SyncFromCurrentStateAsync(FileSystemManager fs)
    {
        var root = await ReadRootAsync(fs);
        NormalizeShape(root);
        var notifications = EnsureNotificationsArray(root);
        var changed = false;

        changed |= await SyncTradeReadyNotificationAsync(fs, notifications);
        changed |= await SyncGuardianQuestNotificationsAsync(fs, notifications);
        changed |= await SyncAbodeResidentsReadyNotificationsAsync(fs, notifications);
        changed |= await SyncAbodeResidentQuestNotificationsAsync(fs, notifications);
        changed |= await SyncAbodeResidentRelicNotificationsAsync(fs, notifications);
        changed |= await SyncAbodeResidentManifestationNotificationsAsync(fs, notifications);
        changed |= await SyncCompanionImprintManifestationNotificationsAsync(fs, notifications);
        changed |= await SyncAbodeResidentInteractionNotificationsAsync(fs, notifications);
        changed |= await SyncArchiveConsultationNotificationsAsync(fs, notifications);
        changed |= await SyncArchiveProjectFuelNotificationsAsync(fs, notifications);

        if (changed || fs.FileExists(NotificationsPath))
            await WriteRootAsync(fs, root);
    }

    public static async Task<IReadOnlyList<NotificationEntry>> ReadAsync(FileSystemManager fs)
    {
        if (!fs.FileExists(NotificationsPath))
            return Array.Empty<NotificationEntry>();

        var root = await ReadRootAsync(fs);
        NormalizeShape(root);
        var notifications = EnsureNotificationsArray(root);
        var result = new List<NotificationEntry>();

        foreach (var node in notifications.OfType<JsonObject>())
        {
            var notificationType = GetNodeString(node["notificationType"]);
            var status = GetNodeString(node["status"]);
            if (!IsSupportedType(notificationType) || !IsSupportedStatus(status))
                continue;

            result.Add(new NotificationEntry
            {
                NotificationId = GetNodeString(node["notificationId"]) ?? "",
                NotificationType = notificationType ?? "",
                RequestId = GetNodeString(node["requestId"]) ?? "",
                Status = status ?? StatusUnread,
                GuardianId = GetNodeString(node["guardianId"]) ?? "",
                GuardianName = GetNodeString(node["guardianName"]) ?? "",
                ArchiveId = GetNodeString(node["archiveId"]) ?? "",
                ArchiveTitle = GetNodeString(node["archiveTitle"]) ?? "",
                TargetProjectId = GetNodeString(node["targetProjectId"]) ?? "",
                TargetProjectName = GetNodeString(node["targetProjectName"]) ?? "",
                Summary = GetNodeString(node["summary"]) ?? "",
                CreatedAtTurn = GetNodeInt(node["createdAtTurn"], 0),
                CreatedAtUtc = GetNodeString(node["createdAtUtc"]) ?? ""
            });
        }

        return result
            .OrderBy(entry => string.Equals(entry.Status, StatusRead, StringComparison.OrdinalIgnoreCase) ? 1 : 0)
            .ThenByDescending(entry => entry.CreatedAtTurn)
            .ThenByDescending(entry => entry.CreatedAtUtc, StringComparer.OrdinalIgnoreCase)
            .ToList();
    }

    public static async Task MarkReadAsync(FileSystemManager fs, string notificationId)
    {
        if (!fs.FileExists(NotificationsPath) || string.IsNullOrWhiteSpace(notificationId))
            return;

        var root = await ReadRootAsync(fs);
        NormalizeShape(root);
        var notifications = EnsureNotificationsArray(root);
        var changed = false;

        foreach (var notification in notifications.OfType<JsonObject>())
        {
            if (!string.Equals(GetNodeString(notification["notificationId"]), notificationId, StringComparison.OrdinalIgnoreCase))
                continue;

            if (!string.Equals(GetNodeString(notification["status"]), StatusRead, StringComparison.OrdinalIgnoreCase))
            {
                notification["status"] = StatusRead;
                changed = true;
            }
        }

        if (changed)
            await WriteRootAsync(fs, root);
    }

    public static async Task MarkAllReadAsync(FileSystemManager fs)
    {
        if (!fs.FileExists(NotificationsPath))
            return;

        var root = await ReadRootAsync(fs);
        NormalizeShape(root);
        var notifications = EnsureNotificationsArray(root);
        var changed = false;

        foreach (var notification in notifications.OfType<JsonObject>())
        {
            if (!string.Equals(GetNodeString(notification["status"]), StatusRead, StringComparison.OrdinalIgnoreCase))
            {
                notification["status"] = StatusRead;
                changed = true;
            }
        }

        if (changed)
            await WriteRootAsync(fs, root);
    }

    public static string GetTypeLabel(string? notificationType) =>
        (notificationType ?? string.Empty).Trim().ToLowerInvariant() switch
        {
            TypeGuardianTradeInventoryReady => "Витрина готова",
            TypeGuardianQuestAvailable => "Новый квест Хранителя",
            TypeAbodeResidentsReady => "Обитатели проявились",
            TypeAbodeResidentQuestAvailable => "Просьба резидента стала квестом",
            TypeAbodeResidentRelicGranted => "Дарована реликвия связи",
            TypeAbodeResidentManifestationReady => "Путь воплощения найден",
            TypeCompanionImprintManifestationReady => "Путь спутника найден",
            TypeAbodeResidentTalkAnswered => "Резидент ответил",
            TypeAbodeResidentHistoryRevealed => "История раскрыта",
            TypeAbodeResidentHistoryRefused => "История не раскрыта",
            TypeArchiveConsultationAccepted => "Консультация принята",
            TypeArchiveConsultationRejected => "Консультация отклонена",
            TypeArchiveConsultationCancelled => "Консультация отменена",
            TypeArchiveProjectFuelAccepted => "Подпитка проекта принята",
            TypeArchiveProjectFuelRejected => "Подпитка проекта отклонена",
            TypeArchiveProjectFuelCancelled => "Подпитка проекта отменена",
            _ => "Уведомление"
        };

    private static async Task<bool> SyncTradeReadyNotificationAsync(FileSystemManager fs, JsonArray notifications)
    {
        var guardiansJson = await fs.ReadFileAsync("game_state/meta/guardians.json");
        if (string.IsNullOrWhiteSpace(guardiansJson))
            return false;

        try
        {
            if (JsonNode.Parse(guardiansJson) is not JsonObject guardiansRoot ||
                guardiansRoot["guardians"] is not JsonArray guardians)
            {
                return false;
            }

            var changed = false;
            foreach (var guardian in guardians.OfType<JsonObject>())
            {
                GuardianTradeRequestState.NormalizeGuardianTradeReceiptsShape(guardian);

                if (guardian["tradeInventory"] is not JsonObject tradeInventory)
                    continue;

                var tradeCycleId = GetNodeString(tradeInventory["tradeCycleId"]);
                if (string.IsNullOrWhiteSpace(tradeCycleId) || guardian[GuardianTradeRequestState.ReceiptsProperty] is not JsonArray receipts)
                    continue;

                var guardianId = GetNodeString(guardian["guardianId"]) ?? string.Empty;
                var guardianName = GuardianManifestation.GetDisplayName(guardian);
                if (string.IsNullOrWhiteSpace(guardianName))
                    guardianName = guardianId;

                foreach (var receipt in receipts.OfType<JsonObject>())
                {
                    if (!string.Equals(GetNodeString(receipt["status"]), GuardianTradeRequestState.ReceiptStatusReady, StringComparison.OrdinalIgnoreCase) ||
                        !string.Equals(GetNodeString(receipt["tradeCycleId"]), tradeCycleId, StringComparison.OrdinalIgnoreCase))
                    {
                        continue;
                    }

                    var requestId = GetNodeString(receipt["requestId"]);
                    if (string.IsNullOrWhiteSpace(requestId))
                        continue;

                    var itemCount = GuardianTradeRequestState.GetTradeInventoryItemCount(tradeInventory);
                    if (GetNodeInt(receipt["itemCount"], -1) != itemCount)
                        continue;

                    var resolvedGuardianName = GetNodeString(receipt["guardianName"]);
                    if (string.IsNullOrWhiteSpace(resolvedGuardianName))
                        resolvedGuardianName = guardianName;

                    changed |= UpsertNotification(
                        notifications,
                        BuildNotificationId(TypeGuardianTradeInventoryReady, requestId),
                        TypeGuardianTradeInventoryReady,
                        requestId,
                        guardianId,
                        resolvedGuardianName,
                        archiveId: null,
                        archiveTitle: null,
                        targetProjectId: null,
                        targetProjectName: null,
                        summary: itemCount > 0
                            ? $"Витрина Хранителя {resolvedGuardianName} готова: {itemCount} позиций. Можно открывать торговлю."
                            : $"Витрина Хранителя {resolvedGuardianName} готова. Можно открывать торговлю.",
                        createdAtTurn: GetNodeInt(receipt["resolvedAtTurn"], 0),
                        createdAtUtc: GetNodeString(receipt["resolvedAtUtc"]) ?? DateTime.UtcNow.ToString("o"));
                }
            }

            var request = await GuardianTradeRequestState.ReadAsync(fs);
            if (request == null)
                return changed;

            var compatibilityGuardian = guardians.OfType<JsonObject>()
                .FirstOrDefault(item => string.Equals(GetNodeString(item["guardianId"]), request.GuardianId, StringComparison.OrdinalIgnoreCase));
            if (compatibilityGuardian?["tradeInventory"] is not JsonObject compatibilityTradeInventory ||
                !GuardianTradeRequestState.InventoryMatchesRequestContract(compatibilityTradeInventory, request) ||
                GuardianTradeRequestState.ReceiptMatchesRequestContract(
                    GuardianTradeRequestState.FindMatchingReceipt(compatibilityGuardian, request),
                    request,
                    compatibilityTradeInventory))
            {
                return changed;
            }

            var compatibilityItemCount = GuardianTradeRequestState.GetTradeInventoryItemCount(compatibilityTradeInventory);
            changed |= UpsertNotification(
                notifications,
                BuildNotificationId(TypeGuardianTradeInventoryReady, request.RequestId),
                TypeGuardianTradeInventoryReady,
                request.RequestId,
                request.GuardianId,
                request.GuardianName,
                archiveId: null,
                archiveTitle: null,
                targetProjectId: null,
                targetProjectName: null,
                summary: compatibilityItemCount > 0
                    ? $"Витрина Хранителя {request.GuardianName} готова: {compatibilityItemCount} позиций. Можно открывать торговлю."
                    : $"Витрина Хранителя {request.GuardianName} готова. Можно открывать торговлю.",
                createdAtTurn: request.CreatedAtTurn,
                createdAtUtc: request.CreatedAtUtc);
            return changed;
        }
        catch
        {
            return false;
        }
    }

    private static async Task<bool> SyncGuardianQuestNotificationsAsync(FileSystemManager fs, JsonArray notifications)
    {
        var guardiansJson = await fs.ReadFileAsync("game_state/meta/guardians.json");
        if (string.IsNullOrWhiteSpace(guardiansJson))
            return false;

        try
        {
            if (JsonNode.Parse(guardiansJson) is not JsonObject guardiansRoot ||
                guardiansRoot["guardians"] is not JsonArray guardians)
            {
                return false;
            }

            var changed = false;
            foreach (var guardian in guardians.OfType<JsonObject>())
            {
                var guardianId = GetNodeString(guardian["guardianId"]) ?? string.Empty;
                if (string.IsNullOrWhiteSpace(guardianId) ||
                    guardian["questManagement"] is not JsonObject questManagement)
                {
                    continue;
                }

                var guardianName = GuardianManifestation.GetDisplayName(guardian);
                if (string.IsNullOrWhiteSpace(guardianName))
                    guardianName = guardianId;

                var seenQuestKeys = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
                changed |= SyncGuardianQuestNotificationsFromArray(
                    notifications,
                    guardianId,
                    guardianName,
                    questManagement["availableQuests"] as JsonArray,
                    seenQuestKeys);
                changed |= SyncGuardianQuestNotificationsFromArray(
                    notifications,
                    guardianId,
                    guardianName,
                    questManagement["activeQuests"] as JsonArray,
                    seenQuestKeys);
            }

            return changed;
        }
        catch
        {
            return false;
        }
    }

    private static async Task<bool> SyncArchiveConsultationNotificationsAsync(FileSystemManager fs, JsonArray notifications)
    {
        var request = await AfterlifeArchiveActionState.ReadConsultationAsync(fs);
        if (request == null)
            return false;

        var receipt = await ReadArchiveReceiptAsync(fs, request.RequestId);
        if (receipt == null)
            return false;

        var status = GetNodeString(receipt["status"]) ?? AfterlifeArchiveActionState.ResolutionStatusAccepted;
        var notificationType = status.Trim().ToLowerInvariant() switch
        {
            AfterlifeArchiveActionState.ResolutionStatusAccepted => TypeArchiveConsultationAccepted,
            AfterlifeArchiveActionState.ResolutionStatusRejected => TypeArchiveConsultationRejected,
            AfterlifeArchiveActionState.ResolutionStatusCancelled => TypeArchiveConsultationCancelled,
            _ => string.Empty
        };

        if (string.IsNullOrWhiteSpace(notificationType))
            return false;

        var summary = notificationType switch
        {
            TypeArchiveConsultationAccepted => await BuildConsultationAcceptedSummaryAsync(fs, request, receipt),
            TypeArchiveConsultationRejected => $"Хранитель {request.GuardianName} отклонил архивную консультацию по записи «{request.ArchiveTitle}». Запись возвращена в Архив души.",
            TypeArchiveConsultationCancelled => $"Архивная консультация по записи «{request.ArchiveTitle}» отменена. Запись возвращена в Архив души.",
            _ => string.Empty
        };

        return UpsertNotification(
            notifications,
            BuildNotificationId(notificationType, request.RequestId),
            notificationType,
            request.RequestId,
            request.GuardianId,
            request.GuardianName,
            request.ArchiveId,
            request.ArchiveTitle,
            targetProjectId: null,
            targetProjectName: null,
            summary,
            createdAtTurn: GetNodeInt(receipt["resolvedAtTurn"], request.CreatedAtTurn),
            createdAtUtc: GetNodeString(receipt["resolvedAtUtc"]) ?? DateTime.UtcNow.ToString("o"));
    }

    private static async Task<bool> SyncAbodeResidentsReadyNotificationsAsync(FileSystemManager fs, JsonArray notifications)
    {
        var residentsJson = await fs.ReadFileAsync(GuardianAbodeResidentState.StatePath);
        if (string.IsNullOrWhiteSpace(residentsJson))
            return false;

        try
        {
            if (JsonNode.Parse(residentsJson) is not JsonObject residentsRoot)
                return false;

            GuardianAbodeResidentState.NormalizeShape(residentsRoot);
            var changed = false;

            if (residentsRoot[GuardianAbodeResidentState.RosterReceiptsProperty] is JsonArray rosterReceipts)
            {
                foreach (var receipt in rosterReceipts.OfType<JsonObject>())
                {
                    var requestId = GetNodeString(receipt["requestId"]);
                    var guardianId = GetNodeString(receipt["guardianId"]);
                    var abodeId = GetNodeString(receipt["abodeId"]);
                    if (string.IsNullOrWhiteSpace(requestId) ||
                        string.IsNullOrWhiteSpace(guardianId) ||
                        string.IsNullOrWhiteSpace(abodeId))
                    {
                        continue;
                    }

                    var count = GetNodeInt(receipt["rosterCount"], 0);
                    if (count <= 0)
                        count = CountPresentResidentsForAbode(residentsRoot, guardianId, abodeId);

                    var guardianName = GetNodeString(receipt["guardianName"]) ?? guardianId;
                    var abodeName = !string.IsNullOrWhiteSpace(GetNodeString(receipt["abodeName"]))
                        ? GetNodeString(receipt["abodeName"])!
                        : abodeId;
                    var summary = count > 0
                        ? $"В Обители «{abodeName}» Хранителя {guardianName} проявились обитатели: {count} записей в roster."
                        : $"В Обители «{abodeName}» Хранителя {guardianName} проявились обитатели.";

                    changed |= UpsertNotification(
                        notifications,
                        BuildNotificationId(TypeAbodeResidentsReady, requestId),
                        TypeAbodeResidentsReady,
                        requestId,
                        guardianId,
                        guardianName,
                        archiveId: null,
                        archiveTitle: null,
                        targetProjectId: null,
                        targetProjectName: null,
                        summary: summary,
                        createdAtTurn: GetNodeInt(receipt["resolvedAtTurn"], 0),
                        createdAtUtc: GetNodeString(receipt["resolvedAtUtc"]) ?? DateTime.UtcNow.ToString("o"));
                }
            }

            var requests = await GuardianAbodeResidentRequestState.ReadResidentsRequestsAsync(fs);
            if (requests.Count == 0)
                return changed;

            foreach (var request in requests)
            {
                if (string.IsNullOrWhiteSpace(request.RequestId) ||
                    string.IsNullOrWhiteSpace(request.GuardianId) ||
                    string.IsNullOrWhiteSpace(request.AbodeId) ||
                    GuardianAbodeResidentState.HasRosterReceipt(
                        GuardianAbodeResidentState.EnsureRosterReceiptsArray(residentsRoot),
                        request.RequestId) ||
                    CountPresentResidentsForAbode(residentsRoot, request.GuardianId, request.AbodeId) <= 0)
                {
                    continue;
                }

                var count = CountPresentResidentsForAbode(residentsRoot, request.GuardianId, request.AbodeId);
                var abodeName = string.IsNullOrWhiteSpace(request.AbodeName) ? request.AbodeId : request.AbodeName;
                var summary = count > 0
                    ? $"В Обители «{abodeName}» Хранителя {request.GuardianName} проявились обитатели: {count} записей в roster."
                    : $"В Обители «{abodeName}» Хранителя {request.GuardianName} проявились обитатели.";

                changed |= UpsertNotification(
                    notifications,
                    BuildNotificationId(TypeAbodeResidentsReady, request.RequestId),
                    TypeAbodeResidentsReady,
                    request.RequestId,
                    request.GuardianId,
                    request.GuardianName,
                    archiveId: null,
                    archiveTitle: null,
                    targetProjectId: null,
                    targetProjectName: null,
                    summary: summary,
                    createdAtTurn: request.CreatedAtTurn,
                    createdAtUtc: request.CreatedAtUtc);
            }

            return changed;
        }
        catch
        {
            return false;
        }
    }

    private static async Task<bool> SyncAbodeResidentQuestNotificationsAsync(FileSystemManager fs, JsonArray notifications)
    {
        var residentsJson = await fs.ReadFileAsync(GuardianAbodeResidentState.StatePath);
        var soulQuestJson = await fs.ReadFileAsync("game_state/quests/soul_quests.json");
        if (string.IsNullOrWhiteSpace(residentsJson) || string.IsNullOrWhiteSpace(soulQuestJson))
            return false;

        try
        {
            if (JsonNode.Parse(residentsJson) is not JsonObject residentsRoot ||
                JsonNode.Parse(soulQuestJson) is not JsonObject soulQuestRoot)
            {
                return false;
            }

            JsonObject? guardiansRoot = null;
            var guardiansJson = await fs.ReadFileAsync("game_state/meta/guardians.json");
            if (!string.IsNullOrWhiteSpace(guardiansJson))
                guardiansRoot = JsonNode.Parse(guardiansJson) as JsonObject;

            var residentMap = BuildResidentNotificationMap(residentsRoot, guardiansRoot);
            if (residentMap.Count == 0)
                return false;

            var questArray = soulQuestRoot["quests"] as JsonArray ?? soulQuestRoot["UpdateSoulQuests"] as JsonArray;
            if (questArray == null)
                return false;

            var changed = false;
            foreach (var quest in questArray.OfType<JsonObject>())
            {
                var residentId = GetNodeString(quest["relatedAfterlifeResidentId"]);
                var questId = GetNodeString(quest["questId"]) ?? GetNodeString(quest["id"]);
                if (string.IsNullOrWhiteSpace(residentId) ||
                    string.IsNullOrWhiteSpace(questId) ||
                    !residentMap.TryGetValue(residentId, out var resident))
                {
                    continue;
                }

                var status = (GetNodeString(quest["status"]) ?? string.Empty).Trim().ToLowerInvariant();
                if (status is "completed" or "failed" or "завершён" or "провален")
                    continue;

                var questTitle = GetNodeString(quest["title"]) ??
                                 GetNodeString(quest["questName"]) ??
                                 GetNodeString(quest["name"]) ??
                                 questId;
                var createdAtTurn = GetNodeInt(quest["createdAtTurn"], GetNodeInt(quest["offeredAtTurn"], 0));
                var createdAtUtc = GetNodeString(quest["createdAtUtc"]) ??
                                   GetNodeString(quest["offeredAtUtc"]) ??
                                   DateTime.UtcNow.ToString("o");

                changed |= UpsertNotification(
                    notifications,
                    BuildNotificationId(TypeAbodeResidentQuestAvailable, $"{residentId}:{questId}"),
                    TypeAbodeResidentQuestAvailable,
                    $"{residentId}:{questId}",
                    resident.GuardianId,
                    resident.GuardianName,
                    archiveId: null,
                    archiveTitle: null,
                    targetProjectId: null,
                    targetProjectName: null,
                    summary: $"Просьба резидента «{resident.DisplayName}» стала квестом души «{questTitle}».",
                    createdAtTurn: createdAtTurn,
                    createdAtUtc: createdAtUtc);
            }

            return changed;
        }
        catch
        {
            return false;
        }
    }

    private static async Task<bool> SyncAbodeResidentRelicNotificationsAsync(FileSystemManager fs, JsonArray notifications)
    {
        var residentsJson = await fs.ReadFileAsync(GuardianAbodeResidentState.StatePath);
        var soulJson = await fs.ReadFileAsync("game_state/meta/soul_state.json");
        if (string.IsNullOrWhiteSpace(residentsJson) || string.IsNullOrWhiteSpace(soulJson))
            return false;

        try
        {
            if (JsonNode.Parse(residentsJson) is not JsonObject residentsRoot ||
                JsonNode.Parse(soulJson) is not JsonObject soulRoot)
            {
                return false;
            }

            JsonObject? guardiansRoot = null;
            var guardiansJson = await fs.ReadFileAsync("game_state/meta/guardians.json");
            if (!string.IsNullOrWhiteSpace(guardiansJson))
                guardiansRoot = JsonNode.Parse(guardiansJson) as JsonObject;

            var residentMap = BuildResidentNotificationMap(residentsRoot, guardiansRoot);
            if (residentMap.Count == 0)
                return false;

            var relicMap = BuildSoulRelicMap(soulRoot);
            if (relicMap.Count == 0)
                return false;

            var changed = false;
            foreach (var resident in residentMap.Values)
            {
                if (string.IsNullOrWhiteSpace(resident.GrantedRelicId) ||
                    !relicMap.TryGetValue(resident.GrantedRelicId, out var relic))
                {
                    continue;
                }

                var timing = TryResolveResidentRelicGrantTiming(residentsRoot, resident.ResidentId, resident.GrantedRelicId);

                changed |= UpsertNotification(
                    notifications,
                    BuildNotificationId(TypeAbodeResidentRelicGranted, $"{resident.ResidentId}:{resident.GrantedRelicId}"),
                    TypeAbodeResidentRelicGranted,
                    $"{resident.ResidentId}:{resident.GrantedRelicId}",
                    resident.GuardianId,
                    resident.GuardianName,
                    archiveId: null,
                    archiveTitle: null,
                    targetProjectId: null,
                    targetProjectName: null,
                    summary: $"Резидент Обители «{resident.DisplayName}» даровал реликвию связи «{relic.RelicName}».",
                    createdAtTurn: timing.CreatedAtTurn,
                    createdAtUtc: timing.CreatedAtUtc);
            }

            return changed;
        }
        catch
        {
            return false;
        }
    }

    private static async Task<bool> SyncAbodeResidentManifestationNotificationsAsync(FileSystemManager fs, JsonArray notifications)
    {
        var residentsJson = await fs.ReadFileAsync(GuardianAbodeResidentState.StatePath);
        var soulJson = await fs.ReadFileAsync("game_state/meta/soul_state.json");
        if (string.IsNullOrWhiteSpace(residentsJson) || string.IsNullOrWhiteSpace(soulJson))
            return false;

        try
        {
            if (JsonNode.Parse(residentsJson) is not JsonObject residentsRoot ||
                JsonNode.Parse(soulJson) is not JsonObject soulRoot)
            {
                return false;
            }

            JsonObject? guardiansRoot = null;
            var guardiansJson = await fs.ReadFileAsync("game_state/meta/guardians.json");
            if (!string.IsNullOrWhiteSpace(guardiansJson))
                guardiansRoot = JsonNode.Parse(guardiansJson) as JsonObject;

            JsonObject? npcRoot = null;
            var npcJson = await fs.ReadFileAsync("game_state/npcs/npc_core.json");
            if (!string.IsNullOrWhiteSpace(npcJson))
                npcRoot = JsonNode.Parse(npcJson) as JsonObject;

            var residentMap = BuildResidentNotificationMap(residentsRoot, guardiansRoot);
            if (residentMap.Count == 0)
                return false;

            var relicMap = BuildSoulRelicMap(soulRoot);
            if (relicMap.Count == 0)
                return false;

            var npcMap = BuildNpcNotificationMap(npcRoot);
            var changed = false;
            foreach (var resident in residentMap.Values)
            {
                if (string.IsNullOrWhiteSpace(resident.GrantedRelicId) ||
                    !relicMap.TryGetValue(resident.GrantedRelicId, out var relic) ||
                    !string.Equals(relic.CompanionManifestationStatus, "materialized", StringComparison.OrdinalIgnoreCase))
                {
                    continue;
                }

                var requestId = !string.IsNullOrWhiteSpace(relic.CompanionManifestationResolvedRequestId)
                    ? relic.CompanionManifestationResolvedRequestId
                    : $"manifestation:{resident.ResidentId}:{resident.GrantedRelicId}";
                var npcName = !string.IsNullOrWhiteSpace(relic.CompanionManifestationResolvedNpcId) &&
                              npcMap.TryGetValue(relic.CompanionManifestationResolvedNpcId, out var resolvedNpcName)
                    ? resolvedNpcName
                    : string.Empty;

                var summary = string.IsNullOrWhiteSpace(npcName)
                    ? $"Путь воплощения для связи с резидентом «{resident.DisplayName}» найден в новой жизни."
                    : $"Путь воплощения для связи с резидентом «{resident.DisplayName}» найден: в новой жизни проявился спутник «{npcName}».";

                changed |= UpsertNotification(
                    notifications,
                    BuildNotificationId(TypeAbodeResidentManifestationReady, requestId),
                    TypeAbodeResidentManifestationReady,
                    requestId,
                    resident.GuardianId,
                    resident.GuardianName,
                    archiveId: null,
                    archiveTitle: null,
                    targetProjectId: null,
                    targetProjectName: null,
                    summary: summary,
                    createdAtTurn: relic.CompanionManifestationResolvedAtTurn,
                    createdAtUtc: !string.IsNullOrWhiteSpace(relic.CompanionManifestationResolvedAtUtc)
                        ? relic.CompanionManifestationResolvedAtUtc
                        : DateTime.UtcNow.ToString("o"));
            }

            return changed;
        }
        catch
        {
            return false;
        }
    }

    private static async Task<bool> SyncCompanionImprintManifestationNotificationsAsync(FileSystemManager fs, JsonArray notifications)
    {
        var soulJson = await fs.ReadFileAsync("game_state/meta/soul_state.json");
        if (string.IsNullOrWhiteSpace(soulJson))
            return false;

        try
        {
            if (JsonNode.Parse(soulJson) is not JsonObject soulRoot)
                return false;

            JsonObject? npcRoot = null;
            var npcJson = await fs.ReadFileAsync("game_state/npcs/npc_core.json");
            if (!string.IsNullOrWhiteSpace(npcJson))
                npcRoot = JsonNode.Parse(npcJson) as JsonObject;

            var relicMap = BuildSoulRelicMap(soulRoot);
            if (relicMap.Count == 0)
                return false;

            var npcMap = BuildNpcNotificationMap(npcRoot);
            var changed = false;
            foreach (var relic in relicMap.Values)
            {
                if (!string.Equals(relic.CompanionManifestationStatus, "materialized", StringComparison.OrdinalIgnoreCase) ||
                    string.IsNullOrWhiteSpace(relic.SourceImprintId) ||
                    !string.IsNullOrWhiteSpace(relic.SourceResidentId))
                {
                    continue;
                }

                var requestId = !string.IsNullOrWhiteSpace(relic.CompanionManifestationResolvedRequestId)
                    ? relic.CompanionManifestationResolvedRequestId
                    : $"manifestation:imprint:{relic.RelicId}";
                var npcName = !string.IsNullOrWhiteSpace(relic.CompanionManifestationResolvedNpcId) &&
                              npcMap.TryGetValue(relic.CompanionManifestationResolvedNpcId, out var resolvedNpcName)
                    ? resolvedNpcName
                    : string.Empty;
                var companionName = !string.IsNullOrWhiteSpace(npcName)
                    ? npcName
                    : !string.IsNullOrWhiteSpace(relic.CompanionNameHint)
                        ? relic.CompanionNameHint
                        : relic.RelicName;

                changed |= UpsertNotification(
                    notifications,
                    BuildNotificationId(TypeCompanionImprintManifestationReady, requestId),
                    TypeCompanionImprintManifestationReady,
                    requestId,
                    relic.SourceGuardianId,
                    relic.SourceGuardianName,
                    archiveId: null,
                    archiveTitle: null,
                    targetProjectId: null,
                    targetProjectName: null,
                    summary: $"Путь воплощения для слепка спутника «{companionName}» найден в новой жизни.",
                    createdAtTurn: relic.CompanionManifestationResolvedAtTurn,
                    createdAtUtc: !string.IsNullOrWhiteSpace(relic.CompanionManifestationResolvedAtUtc)
                        ? relic.CompanionManifestationResolvedAtUtc
                        : DateTime.UtcNow.ToString("o"));
            }

            return changed;
        }
        catch
        {
            return false;
        }
    }

    private static async Task<bool> SyncAbodeResidentInteractionNotificationsAsync(FileSystemManager fs, JsonArray notifications)
    {
        var residentsJson = await fs.ReadFileAsync(GuardianAbodeResidentState.StatePath);
        if (string.IsNullOrWhiteSpace(residentsJson))
            return false;

        try
        {
            if (JsonNode.Parse(residentsJson) is not JsonObject residentsRoot)
                return false;

            GuardianAbodeResidentState.NormalizeShape(residentsRoot);
            var receipts = GuardianAbodeResidentState.EnsureInteractionReceiptsArray(residentsRoot);
            var historyLog = GuardianAbodeResidentState.EnsureHistoryLogArray(residentsRoot);
            JsonObject? guardiansRoot = null;
            var guardiansJson = await fs.ReadFileAsync("game_state/meta/guardians.json");
            if (!string.IsNullOrWhiteSpace(guardiansJson))
                guardiansRoot = JsonNode.Parse(guardiansJson) as JsonObject;
            var residentMap = BuildResidentNotificationMap(residentsRoot, guardiansRoot);
            var changed = false;

            foreach (var receipt in receipts.OfType<JsonObject>())
            {
                var requestId = GetNodeString(receipt["requestId"]);
                if (string.IsNullOrWhiteSpace(requestId))
                    continue;

                var residentId = GetNodeString(receipt["residentId"]) ?? string.Empty;
                var interactionType = (GetNodeString(receipt["interactionType"]) ?? string.Empty).Trim().ToLowerInvariant();
                var status = (GetNodeString(receipt["status"]) ?? string.Empty).Trim().ToLowerInvariant();
                var responseMode = (GetNodeString(receipt["responseMode"]) ?? string.Empty).Trim().ToLowerInvariant();
                residentMap.TryGetValue(residentId, out var residentInfo);
                var residentName = residentInfo?.DisplayName;
                if (string.IsNullOrWhiteSpace(residentName))
                    residentName = GetNodeString(receipt["residentName"]) ?? residentId;
                var guardianId = residentInfo?.GuardianId;
                if (string.IsNullOrWhiteSpace(guardianId))
                    guardianId = GetNodeString(receipt["guardianId"]) ?? string.Empty;
                var guardianName = residentInfo?.GuardianName;
                if (string.IsNullOrWhiteSpace(guardianName))
                    guardianName = GetNodeString(receipt["guardianName"]) ?? guardianId;
                var abodeName = GetNodeString(receipt["abodeName"]) ?? GetNodeString(receipt["abodeId"]) ?? string.Empty;

                string notificationType;
                string summary;
                if (string.Equals(interactionType, GuardianAbodeResidentState.InteractionTypeTalk, StringComparison.OrdinalIgnoreCase))
                {
                    notificationType = TypeAbodeResidentTalkAnswered;
                    summary = status switch
                    {
                        GuardianAbodeResidentState.InteractionStatusRejected =>
                            $"Резидент «{residentName}» отказался от разговора в Обители «{abodeName}».",
                        GuardianAbodeResidentState.InteractionStatusCancelled =>
                            $"Разговор с резидентом «{residentName}» не состоялся.",
                        _ =>
                            $"Резидент «{residentName}» ответил на разговор в Обители «{abodeName}»."
                    };
                }
                else
                {
                    var historyEntryId = GetNodeString(receipt["historyEntryId"]);
                    var historyTitle = historyLog.OfType<JsonObject>()
                        .FirstOrDefault(entry => string.Equals(GetNodeString(entry["entryId"]), historyEntryId, StringComparison.OrdinalIgnoreCase) &&
                                                 string.Equals(GetNodeString(entry["residentId"]), residentId, StringComparison.OrdinalIgnoreCase))?["title"]?
                        .GetValue<string>();
                    var revealed = string.Equals(status, GuardianAbodeResidentState.InteractionStatusAccepted, StringComparison.OrdinalIgnoreCase) &&
                                   !string.Equals(responseMode, GuardianAbodeResidentState.ResponseModeHistoryRefused, StringComparison.OrdinalIgnoreCase);
                    notificationType = revealed ? TypeAbodeResidentHistoryRevealed : TypeAbodeResidentHistoryRefused;
                    summary = notificationType == TypeAbodeResidentHistoryRevealed
                        ? string.IsNullOrWhiteSpace(historyTitle)
                            ? $"Резидент «{residentName}» раскрыл часть своей прошлой истории."
                            : $"Резидент «{residentName}» раскрыл фрагмент прошлого: «{historyTitle}»."
                        : $"Резидент «{residentName}» не раскрыл своё прошлое.";
                }

                changed |= UpsertNotification(
                    notifications,
                    BuildNotificationId(notificationType, requestId),
                    notificationType,
                    requestId,
                    guardianId ?? string.Empty,
                    guardianName ?? string.Empty,
                    archiveId: null,
                    archiveTitle: null,
                    targetProjectId: null,
                    targetProjectName: null,
                    summary: summary,
                    createdAtTurn: GetNodeInt(receipt["resolvedAtTurn"], 0),
                    createdAtUtc: GetNodeString(receipt["resolvedAtUtc"]) ?? DateTime.UtcNow.ToString("o"));
            }

            return changed;
        }
        catch
        {
            return false;
        }
    }

    private static async Task<bool> SyncArchiveProjectFuelNotificationsAsync(FileSystemManager fs, JsonArray notifications)
    {
        var request = await AfterlifeArchiveActionState.ReadProjectFuelAsync(fs);
        if (request == null)
            return false;

        var receipt = await ReadArchiveReceiptAsync(fs, request.RequestId);
        if (receipt == null)
            return false;

        var status = GetNodeString(receipt["status"]) ?? AfterlifeArchiveActionState.ResolutionStatusAccepted;
        var notificationType = status.Trim().ToLowerInvariant() switch
        {
            AfterlifeArchiveActionState.ResolutionStatusAccepted => TypeArchiveProjectFuelAccepted,
            AfterlifeArchiveActionState.ResolutionStatusRejected => TypeArchiveProjectFuelRejected,
            AfterlifeArchiveActionState.ResolutionStatusCancelled => TypeArchiveProjectFuelCancelled,
            _ => string.Empty
        };

        if (string.IsNullOrWhiteSpace(notificationType))
            return false;

        var projectName = string.IsNullOrWhiteSpace(request.TargetProjectName) ? request.TargetProjectId : request.TargetProjectName;
        var summary = notificationType switch
        {
            TypeArchiveProjectFuelAccepted => await BuildProjectFuelAcceptedSummaryAsync(fs, request, receipt),
            TypeArchiveProjectFuelRejected => $"Хранитель {request.GuardianName} отклонил архивную подпитку проекта «{projectName}». Запись возвращена в Архив души.",
            TypeArchiveProjectFuelCancelled => $"Архивная подпитка проекта «{projectName}» отменена. Запись возвращена в Архив души.",
            _ => string.Empty
        };

        return UpsertNotification(
            notifications,
            BuildNotificationId(notificationType, request.RequestId),
            notificationType,
            request.RequestId,
            request.GuardianId,
            request.GuardianName,
            request.ArchiveId,
            request.ArchiveTitle,
            request.TargetProjectId,
            request.TargetProjectName,
            summary,
            createdAtTurn: GetNodeInt(receipt["resolvedAtTurn"], request.CreatedAtTurn),
            createdAtUtc: GetNodeString(receipt["resolvedAtUtc"]) ?? DateTime.UtcNow.ToString("o"));
    }

    private static bool UpsertNotification(
        JsonArray notifications,
        string notificationId,
        string notificationType,
        string requestId,
        string? guardianId,
        string? guardianName,
        string? archiveId,
        string? archiveTitle,
        string? targetProjectId,
        string? targetProjectName,
        string summary,
        int createdAtTurn,
        string createdAtUtc)
    {
        if (string.IsNullOrWhiteSpace(notificationId) ||
            string.IsNullOrWhiteSpace(requestId) ||
            !IsSupportedType(notificationType) ||
            string.IsNullOrWhiteSpace(summary))
        {
            return false;
        }

        foreach (var existing in notifications.OfType<JsonObject>())
        {
            if (!string.Equals(GetNodeString(existing["notificationId"]), notificationId, StringComparison.OrdinalIgnoreCase))
                continue;

            var changed = false;
            changed |= SetIfDifferent(existing, "notificationType", notificationType);
            changed |= SetIfDifferent(existing, "requestId", requestId);
            changed |= SetIfDifferent(existing, "guardianId", guardianId ?? string.Empty);
            changed |= SetIfDifferent(existing, "guardianName", guardianName ?? string.Empty);
            changed |= SetIfDifferent(existing, "archiveId", archiveId ?? string.Empty);
            changed |= SetIfDifferent(existing, "archiveTitle", archiveTitle ?? string.Empty);
            changed |= SetIfDifferent(existing, "targetProjectId", targetProjectId ?? string.Empty);
            changed |= SetIfDifferent(existing, "targetProjectName", targetProjectName ?? string.Empty);
            changed |= SetIfDifferent(existing, "summary", summary);
            return changed;
        }

        notifications.Add(new JsonObject
        {
            ["notificationId"] = notificationId,
            ["notificationType"] = notificationType,
            ["requestId"] = requestId,
            ["status"] = StatusUnread,
            ["guardianId"] = guardianId ?? string.Empty,
            ["guardianName"] = guardianName ?? string.Empty,
            ["archiveId"] = archiveId ?? string.Empty,
            ["archiveTitle"] = archiveTitle ?? string.Empty,
            ["targetProjectId"] = targetProjectId ?? string.Empty,
            ["targetProjectName"] = targetProjectName ?? string.Empty,
            ["summary"] = summary,
            ["createdAtTurn"] = Math.Max(0, createdAtTurn),
            ["createdAtUtc"] = createdAtUtc
        });
        return true;
    }

    private static string BuildNotificationId(string notificationType, string requestId) =>
        $"{notificationType}:{requestId}";

    private static bool SyncGuardianQuestNotificationsFromArray(
        JsonArray notifications,
        string guardianId,
        string guardianName,
        JsonArray? quests,
        HashSet<string> seenQuestKeys)
    {
        if (quests == null)
            return false;

        var changed = false;
        foreach (var quest in quests.OfType<JsonObject>())
        {
            var questOrigin = GetNodeString(quest["questOrigin"]);
            if (!IsGuardianQuestNotificationOrigin(questOrigin))
                continue;

            var questId = GetNodeString(quest["questId"]);
            if (string.IsNullOrWhiteSpace(questId))
                continue;

            var questTitle = GetNodeString(quest["title"]) ??
                             GetNodeString(quest["name"]) ??
                             questId ??
                             "Новый квест";
            var questKey = BuildGuardianQuestNotificationKey(guardianId, questId!);
            if (!seenQuestKeys.Add(questKey))
                continue;

            var createdAtTurn = GetNodeInt(quest["createdAtTurn"], GetNodeInt(quest["offeredAtTurn"], 0));
            var createdAtUtc = GetNodeString(quest["createdAtUtc"]) ??
                               GetNodeString(quest["offeredAtUtc"]) ??
                               DateTime.UtcNow.ToString("o");

            changed |= UpsertNotification(
                notifications,
                BuildNotificationId(TypeGuardianQuestAvailable, questKey),
                TypeGuardianQuestAvailable,
                questKey,
                guardianId,
                guardianName,
                archiveId: null,
                archiveTitle: null,
                targetProjectId: null,
                targetProjectName: null,
                summary: BuildGuardianQuestSummary(guardianName, questTitle, questOrigin),
                createdAtTurn: createdAtTurn,
                createdAtUtc: createdAtUtc);
        }

        return changed;
    }

    private static async Task<string> BuildConsultationAcceptedSummaryAsync(
        FileSystemManager fs,
        AfterlifeArchiveActionState.PendingArchiveConsultationRequest request,
        JsonObject? receipt)
    {
        var receiptOutcomeLabel = BuildConsultationOutcomeLabel(receipt);
        if (!string.IsNullOrWhiteSpace(receiptOutcomeLabel))
            return $"Хранитель {request.GuardianName} принял архивную консультацию по записи «{request.ArchiveTitle}»: {receiptOutcomeLabel}.";

        var trackerJson = await fs.ReadFileAsync(GuardianProjectState.TrackerPath);
        if (string.IsNullOrWhiteSpace(trackerJson))
            return $"Хранитель {request.GuardianName} принял архивную консультацию по записи «{request.ArchiveTitle}».";

        try
        {
            if (JsonNode.Parse(trackerJson) is not JsonObject trackerRoot ||
                trackerRoot["completedProjects"] is not JsonArray completedProjects)
            {
                return $"Хранитель {request.GuardianName} принял архивную консультацию по записи «{request.ArchiveTitle}».";
            }

            var project = completedProjects
                .OfType<JsonObject>()
                .Where(entry => string.Equals(GetNodeString(entry["guardianId"]), request.GuardianId, StringComparison.OrdinalIgnoreCase))
                .Select(entry => entry["project"] as JsonObject)
                .FirstOrDefault(projectNode =>
                    projectNode != null &&
                    string.Equals(GetNodeString(projectNode["projectOrigin"]), "archive_consultation", StringComparison.OrdinalIgnoreCase) &&
                    (string.Equals(GetNodeString(projectNode["consultationRequestId"]), request.RequestId, StringComparison.OrdinalIgnoreCase) ||
                     string.Equals(GetNodeString(projectNode["consultationArchiveId"]), request.ArchiveId, StringComparison.OrdinalIgnoreCase)));

            var outcomeLabel = BuildConsultationOutcomeLabel(project?["projectOutcomeAudit"] as JsonObject);
            if (string.IsNullOrWhiteSpace(outcomeLabel))
                return $"Хранитель {request.GuardianName} принял архивную консультацию по записи «{request.ArchiveTitle}».";

            return $"Хранитель {request.GuardianName} принял архивную консультацию по записи «{request.ArchiveTitle}»: {outcomeLabel}.";
        }
        catch
        {
            return $"Хранитель {request.GuardianName} принял архивную консультацию по записи «{request.ArchiveTitle}».";
        }
    }

    private static async Task<string> BuildProjectFuelAcceptedSummaryAsync(
        FileSystemManager fs,
        AfterlifeArchiveActionState.PendingArchiveProjectFuelRequest request,
        JsonObject? receipt)
    {
        var projectName = string.IsNullOrWhiteSpace(request.TargetProjectName) ? request.TargetProjectId : request.TargetProjectName;
        var resultMode = GetNodeString(receipt?["resultMode"]);
        var resultAmount = GetNodeInt(receipt?["resultAmount"], 0);
        if (string.Equals(resultMode, AfterlifeArchiveActionState.ProjectFuelResultModeProjectWork, StringComparison.OrdinalIgnoreCase) &&
            resultAmount > 0)
        {
            return $"Хранитель {request.GuardianName} усилил проект «{projectName}»: работа +{resultAmount}.";
        }

        if (string.Equals(resultMode, AfterlifeArchiveActionState.ProjectFuelResultModePressureRelief, StringComparison.OrdinalIgnoreCase) &&
            resultAmount > 0)
        {
            return $"Хранитель {request.GuardianName} ослабил давление на проект «{projectName}»: давление -{resultAmount}.";
        }

        var journalJson = await fs.ReadFileAsync(GuardianProjectState.JournalPath);
        if (string.IsNullOrWhiteSpace(journalJson))
            return $"Хранитель {request.GuardianName} обработал архивную подпитку проекта «{projectName}».";

        try
        {
            if (JsonNode.Parse(journalJson) is not JsonObject journalRoot ||
                journalRoot["entries"] is not JsonArray entries)
            {
                return $"Хранитель {request.GuardianName} обработал архивную подпитку проекта «{projectName}».";
            }

            var entry = entries.OfType<JsonObject>()
                .FirstOrDefault(item =>
                    string.Equals(GetNodeString(item["guardianId"]), request.GuardianId, StringComparison.OrdinalIgnoreCase) &&
                    string.Equals(GetNodeString(item["projectId"]), request.TargetProjectId, StringComparison.OrdinalIgnoreCase) &&
                    string.Equals(GetNodeString(item["eventType"]), "assisted", StringComparison.OrdinalIgnoreCase) &&
                    (string.Equals(GetNodeString(item["archiveFuelRequestId"]), request.RequestId, StringComparison.OrdinalIgnoreCase) ||
                     JournalEntryContainsDetail(item, request.ArchiveId)));

            if (entry == null)
                return $"Хранитель {request.GuardianName} обработал архивную подпитку проекта «{projectName}».";

            var workDelta = TryResolveDiffDelta(entry, "Работа");
            if (workDelta is > 0)
                return $"Хранитель {request.GuardianName} усилил проект «{projectName}»: работа +{workDelta.Value}.";

            var pressureDelta = TryResolveDiffDelta(entry, "Pressure");
            if (pressureDelta is < 0)
                return $"Хранитель {request.GuardianName} ослабил давление на проект «{projectName}»: давление -{Math.Abs(pressureDelta.Value)}.";

            var summary = GetNodeString(entry["summary"]);
            if (!string.IsNullOrWhiteSpace(summary))
                return $"Хранитель {request.GuardianName} обработал архивную подпитку проекта «{projectName}»: {summary.Trim().TrimEnd('.')}.";

            return $"Хранитель {request.GuardianName} обработал архивную подпитку проекта «{projectName}».";
        }
        catch
        {
            return $"Хранитель {request.GuardianName} обработал архивную подпитку проекта «{projectName}».";
        }
    }

    private static string BuildConsultationOutcomeLabel(JsonObject? audit)
    {
        if (audit == null)
            return string.Empty;

        var parts = new List<string>();
        var guaranteedArchiveQuestCount = GetNodeInt(audit["guaranteedArchiveQuestCount"], 0);
        if (guaranteedArchiveQuestCount > 0)
            parts.Add(guaranteedArchiveQuestCount == 1
                ? "гарантирован 1 квест Хранителя"
                : $"гарантировано квестов Хранителя: {guaranteedArchiveQuestCount}");

        var hookCount = GetNodeInt(audit["questHookCount"], 0);
        if (hookCount > 0)
            parts.Add($"+{hookCount} квестовых зацепок");

        var specialLines = GetNodeInt(audit["specialQuestLineUnlocks"], 0);
        if (specialLines > 0)
            parts.Add($"+{specialLines} особых квестовых линий");

        var clueBonus = GetNodeInt(audit["visibleRivalClueBonus"], 0);
        if (clueBonus > 0)
            parts.Add($"+{clueBonus} видимых подсказок по чужим нитям");

        var warningBonus = GetNodeInt(audit["archiveWarningTierBonus"], 0);
        if (warningBonus > 0)
            parts.Add($"уровень предупреждения +{warningBonus}");

        return string.Join(", ", parts);
    }

    private sealed class ResidentNotificationInfo
    {
        public string ResidentId { get; init; } = "";
        public string DisplayName { get; init; } = "";
        public string GuardianId { get; init; } = "";
        public string GuardianName { get; init; } = "";
        public string GrantedRelicId { get; init; } = "";
    }

    private sealed class SoulRelicNotificationInfo
    {
        public string RelicId { get; init; } = "";
        public string RelicName { get; init; } = "";
        public string SourceResidentId { get; init; } = "";
        public string SourceImprintId { get; init; } = "";
        public string SourceGuardianId { get; init; } = "";
        public string SourceGuardianName { get; init; } = "";
        public string CompanionNameHint { get; init; } = "";
        public string CompanionManifestationStatus { get; init; } = "";
        public string CompanionManifestationResolvedRequestId { get; init; } = "";
        public string CompanionManifestationResolvedNpcId { get; init; } = "";
        public int CompanionManifestationResolvedAtTurn { get; init; }
        public string CompanionManifestationResolvedAtUtc { get; init; } = "";
    }

    private static Dictionary<string, ResidentNotificationInfo> BuildResidentNotificationMap(JsonObject residentsRoot, JsonObject? guardiansRoot = null)
    {
        GuardianAbodeResidentState.NormalizeShape(residentsRoot);
        var guardianNameMap = BuildGuardianNameMap(guardiansRoot);
        return ((residentsRoot[GuardianAbodeResidentState.EntriesProperty] as JsonArray)?
            .OfType<JsonObject>()
            .Select(resident => new ResidentNotificationInfo
            {
                ResidentId = GetNodeString(resident["residentId"]) ?? string.Empty,
                DisplayName = GetNodeString(resident["displayName"]) ?? string.Empty,
                GuardianId = GetNodeString(resident["guardianId"]) ?? string.Empty,
                GuardianName = guardianNameMap.TryGetValue(GetNodeString(resident["guardianId"]) ?? string.Empty, out var guardianName)
                    ? guardianName
                    : string.Empty,
                GrantedRelicId = GetNodeString(resident["grantedRelicId"]) ?? string.Empty
            })
            .Where(info => !string.IsNullOrWhiteSpace(info.ResidentId))
            .ToDictionary(info => info.ResidentId, info => info, StringComparer.OrdinalIgnoreCase))
            ?? new Dictionary<string, ResidentNotificationInfo>(StringComparer.OrdinalIgnoreCase);
    }

    private static int CountPresentResidentsForAbode(JsonObject residentsRoot, string guardianId, string abodeId)
    {
        GuardianAbodeResidentState.NormalizeShape(residentsRoot);
        if (residentsRoot[GuardianAbodeResidentState.EntriesProperty] is not JsonArray entries)
            return 0;

        return entries.OfType<JsonObject>().Count(resident =>
            string.Equals(GetNodeString(resident["guardianId"]), guardianId, StringComparison.OrdinalIgnoreCase) &&
            string.Equals(GetNodeString(resident["abodeId"]), abodeId, StringComparison.OrdinalIgnoreCase) &&
            !string.Equals(GetNodeString(resident["isPresent"]), "false", StringComparison.OrdinalIgnoreCase) &&
            !(resident["isPresent"] is JsonValue isPresentValue &&
              isPresentValue.TryGetValue<bool>(out var isPresent) &&
              !isPresent));
    }

    private static Dictionary<string, SoulRelicNotificationInfo> BuildSoulRelicMap(JsonObject soulRoot)
    {
        var result = new Dictionary<string, SoulRelicNotificationInfo>(StringComparer.OrdinalIgnoreCase);
        if (soulRoot["soulRelics"] is not JsonObject soulRelics)
            return result;

        foreach (var collectionName in new[] { "equipped", "stored" })
        {
            if (soulRelics[collectionName] is not JsonArray collection)
                continue;

            foreach (var relic in collection.OfType<JsonObject>())
            {
                var relicId = GetNodeString(relic["relicId"]) ?? GetNodeString(relic["id"]);
                if (string.IsNullOrWhiteSpace(relicId))
                    continue;

                var sourceResidentId = string.Empty;
                var companionNameHint = string.Empty;
                if (relic["companionSeed"] is JsonObject companionSeed)
                {
                    sourceResidentId = GetNodeString(companionSeed["sourceResidentId"]) ?? string.Empty;
                    companionNameHint = GetNodeString(companionSeed["companionNameHint"]) ?? string.Empty;
                }

                var sourceImprintId = string.Empty;
                if (TryGetEmbeddedImprint(relic, out var imprint))
                {
                    sourceImprintId = GetNodeString(imprint["imprintId"]) ?? GetNodeString(imprint["id"]) ?? string.Empty;
                    if (string.IsNullOrWhiteSpace(companionNameHint))
                    {
                        companionNameHint = GetNodeString(imprint["NPCName"]) ??
                                            GetNodeString(imprint["npcName"]) ??
                                            GetNodeString(imprint["name"]) ??
                                            GetNodeString(imprint["companionName"]) ??
                                            GetNodeString(imprint["originalName"]) ??
                                            string.Empty;
                    }
                }

                result[relicId] = new SoulRelicNotificationInfo
                {
                    RelicId = relicId,
                    RelicName = GetNodeString(relic["name"]) ?? relicId,
                    SourceResidentId = sourceResidentId,
                    SourceImprintId = sourceImprintId,
                    SourceGuardianId = GetNodeString(relic["sourceGuardianId"]) ?? GetNodeString(relic["guardianId"]) ?? string.Empty,
                    SourceGuardianName = GetNodeString(relic["sourceGuardianName"]) ?? GetNodeString(relic["guardianName"]) ?? string.Empty,
                    CompanionNameHint = companionNameHint,
                    CompanionManifestationStatus = GetNodeString(relic["companionManifestationStatus"]) ?? string.Empty,
                    CompanionManifestationResolvedRequestId = GetNodeString(relic["companionManifestationResolvedRequestId"]) ?? string.Empty,
                    CompanionManifestationResolvedNpcId = GetNodeString(relic["companionManifestationResolvedNpcId"]) ?? string.Empty,
                    CompanionManifestationResolvedAtTurn = GetNodeInt(relic["companionManifestationResolvedAtTurn"], 0),
                    CompanionManifestationResolvedAtUtc = GetNodeString(relic["companionManifestationResolvedAtUtc"]) ?? string.Empty
                };
            }
        }

        return result;
    }

    private static bool TryGetEmbeddedImprint(JsonObject relic, out JsonObject imprint)
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

    private static Dictionary<string, string> BuildGuardianNameMap(JsonObject? guardiansRoot)
    {
        var result = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
        if (guardiansRoot == null)
            return result;

        if (guardiansRoot["guardians"] is JsonArray guardians)
        {
            foreach (var guardian in guardians.OfType<JsonObject>())
            {
                var guardianId = GetNodeString(guardian["guardianId"]);
                if (string.IsNullOrWhiteSpace(guardianId))
                    continue;

                var name = GuardianManifestation.GetDisplayName(guardian);
                if (string.IsNullOrWhiteSpace(name))
                    name = GetNodeString(guardian["canonicalName"]) ?? GetNodeString(guardian["name"]) ?? guardianId;

                result[guardianId] = name;
            }
        }

        if (guardiansRoot["activeGuardian"] is JsonObject activeGuardian)
        {
            var guardianId = GetNodeString(activeGuardian["guardianId"]);
            if (!string.IsNullOrWhiteSpace(guardianId) && !result.ContainsKey(guardianId))
            {
                var name = GuardianManifestation.GetDisplayName(activeGuardian);
                if (string.IsNullOrWhiteSpace(name))
                    name = GetNodeString(activeGuardian["canonicalName"]) ?? GetNodeString(activeGuardian["name"]) ?? guardianId;

                result[guardianId] = name;
            }
        }

        return result;
    }

    private static Dictionary<string, string> BuildNpcNotificationMap(JsonObject? npcRoot)
    {
        var result = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
        if (npcRoot == null)
            return result;

        foreach (var propertyName in new[] { "UpdateNPCs", "NPCsInScene", "NPCs", "npcs", "npcDataChanges" })
        {
            if (npcRoot[propertyName] is not JsonArray npcs)
                continue;

            foreach (var npc in npcs.OfType<JsonObject>())
            {
                var npcId = GetNodeString(npc["NPCId"]) ?? GetNodeString(npc["npcId"]) ?? GetNodeString(npc["id"]);
                if (string.IsNullOrWhiteSpace(npcId))
                    continue;

                var name = GetNodeString(npc["NPCName"]) ?? GetNodeString(npc["npcName"]) ?? GetNodeString(npc["name"]) ?? GetNodeString(npc["displayName"]) ?? npcId;
                result[npcId] = name;
            }
        }

        return result;
    }

    private static (int CreatedAtTurn, string CreatedAtUtc) TryResolveResidentRelicGrantTiming(JsonObject residentsRoot, string residentId, string relicId)
    {
        GuardianAbodeResidentState.NormalizeShape(residentsRoot);
        var createdAtTurn = 0;
        var createdAtUtc = string.Empty;

        if (residentsRoot[GuardianAbodeResidentState.InteractionLogProperty] is JsonArray interactionLog)
        {
            foreach (var entry in interactionLog.OfType<JsonObject>()
                         .OrderByDescending(item => GetNodeInt(item["turn"], 0))
                         .ThenByDescending(item => GetNodeString(item["timestamp"]), StringComparer.OrdinalIgnoreCase))
            {
                if (!string.Equals(GetNodeString(entry["residentId"]), residentId, StringComparison.OrdinalIgnoreCase) ||
                    (!string.Equals(GetNodeString(entry["relatedRelicId"]), relicId, StringComparison.OrdinalIgnoreCase) &&
                     !string.Equals(GetNodeString(entry["eventType"]), "relic_grant", StringComparison.OrdinalIgnoreCase)))
                {
                    continue;
                }

                createdAtTurn = GetNodeInt(entry["turn"], 0);
                createdAtUtc = GetNodeString(entry["timestamp"]) ?? string.Empty;
                if (createdAtTurn > 0 || !string.IsNullOrWhiteSpace(createdAtUtc))
                    break;
            }
        }

        if (string.IsNullOrWhiteSpace(createdAtUtc) && residentsRoot[GuardianAbodeResidentState.InteractionReceiptsProperty] is JsonArray receipts)
        {
            foreach (var receipt in receipts.OfType<JsonObject>()
                         .OrderByDescending(item => GetNodeInt(item["resolvedAtTurn"], 0))
                         .ThenByDescending(item => GetNodeString(item["resolvedAtUtc"]), StringComparer.OrdinalIgnoreCase))
            {
                if (!string.Equals(GetNodeString(receipt["residentId"]), residentId, StringComparison.OrdinalIgnoreCase) ||
                    !string.Equals(GetNodeString(receipt["interactionType"]), "reward", StringComparison.OrdinalIgnoreCase))
                {
                    continue;
                }

                createdAtTurn = GetNodeInt(receipt["resolvedAtTurn"], createdAtTurn);
                createdAtUtc = GetNodeString(receipt["resolvedAtUtc"]) ?? createdAtUtc;
                if (createdAtTurn > 0 || !string.IsNullOrWhiteSpace(createdAtUtc))
                    break;
            }
        }

        if (string.IsNullOrWhiteSpace(createdAtUtc))
            createdAtUtc = DateTime.UtcNow.ToString("o");

        return (createdAtTurn, createdAtUtc);
    }

    private static bool IsGuardianQuestNotificationOrigin(string? questOrigin) =>
        string.Equals(questOrigin, GuardianProjectState.LoreResearchHookOrigin, StringComparison.OrdinalIgnoreCase) ||
        string.Equals(questOrigin, GuardianProjectState.LoreResearchSpecialLineOrigin, StringComparison.OrdinalIgnoreCase) ||
        string.Equals(questOrigin, GuardianProjectState.ArchiveConsultationHookOrigin, StringComparison.OrdinalIgnoreCase);

    private static string BuildGuardianQuestNotificationKey(string guardianId, string questId) =>
        $"{guardianId}:{questId}";

    private static string BuildGuardianQuestSummary(string guardianName, string questTitle, string? questOrigin) =>
        (questOrigin ?? string.Empty).Trim().ToLowerInvariant() switch
        {
            GuardianProjectState.ArchiveConsultationHookOrigin =>
                $"У Хранителя {guardianName} появился архивный квест «{questTitle}».",
            GuardianProjectState.LoreResearchSpecialLineOrigin =>
                $"У Хранителя {guardianName} открылась особая квестовая линия «{questTitle}».",
            GuardianProjectState.LoreResearchHookOrigin =>
                $"У Хранителя {guardianName} появилась новая квестовая зацепка «{questTitle}».",
            _ =>
                $"У Хранителя {guardianName} появился новый квест «{questTitle}»."
        };

    private static bool JournalEntryContainsDetail(JsonObject entry, string fragment)
    {
        if (string.IsNullOrWhiteSpace(fragment) || entry["details"] is not JsonArray details)
            return false;

        return details.OfType<JsonValue>().Any(detail =>
            detail.TryGetValue<string>(out var text) &&
            text.Contains(fragment, StringComparison.OrdinalIgnoreCase));
    }

    private static int? TryResolveDiffDelta(JsonObject entry, string label)
    {
        if (entry["details"] is not JsonArray details)
            return null;

        foreach (var detail in details.OfType<JsonValue>())
        {
            if (!detail.TryGetValue<string>(out var text) || string.IsNullOrWhiteSpace(text))
                continue;

            var prefix = label + ":";
            if (!text.StartsWith(prefix, StringComparison.OrdinalIgnoreCase))
                continue;

            var payload = text[prefix.Length..].Trim();
            var parts = payload.Split("->", StringSplitOptions.TrimEntries | StringSplitOptions.RemoveEmptyEntries);
            if (parts.Length != 2)
                continue;

            if (int.TryParse(parts[0], out var previousValue) && int.TryParse(parts[1], out var currentValue))
                return currentValue - previousValue;
        }

        return null;
    }

    private static async Task<JsonObject?> ReadArchiveReceiptAsync(FileSystemManager fs, string requestId)
    {
        var soulJson = await fs.ReadFileAsync("game_state/meta/soul_state.json");
        if (string.IsNullOrWhiteSpace(soulJson))
            return null;

        try
        {
            if (JsonNode.Parse(soulJson) is not JsonObject soulRoot)
                return null;

            AfterlifeArchiveState.NormalizeShape(soulRoot);
            var receipts = AfterlifeArchiveState.EnsureActionReceiptsArray(soulRoot);
            return receipts.OfType<JsonObject>()
                .FirstOrDefault(receipt => string.Equals(GetNodeString(receipt["requestId"]), requestId, StringComparison.OrdinalIgnoreCase));
        }
        catch
        {
            return null;
        }
    }

    private static async Task<JsonObject> ReadRootAsync(FileSystemManager fs)
    {
        var json = await fs.ReadFileAsync(NotificationsPath);
        if (string.IsNullOrWhiteSpace(json))
            return new JsonObject();

        try
        {
            return JsonNode.Parse(json) as JsonObject ?? new JsonObject();
        }
        catch
        {
            return new JsonObject();
        }
    }

    private static async Task WriteRootAsync(FileSystemManager fs, JsonObject root)
    {
        NormalizeShape(root);
        await fs.WriteFileAtomicAsync(NotificationsPath, root.ToJsonString(JsonOpts));
    }

    private static bool TrimReadNotifications(JsonArray notifications)
    {
        var readNotifications = notifications
            .Select((node, index) => (node as JsonObject, index))
            .Where(pair => pair.Item1 != null &&
                           string.Equals(GetNodeString(pair.Item1["status"]), StatusRead, StringComparison.OrdinalIgnoreCase))
            .Select(pair => (Notification: pair.Item1!, Index: pair.index))
            .OrderByDescending(pair => GetNodeInt(pair.Notification["createdAtTurn"], 0))
            .ThenByDescending(pair => ParseCreatedAt(pair.Notification["createdAtUtc"]))
            .ToList();

        if (readNotifications.Count <= MaxRetainedReadNotifications)
            return false;

        var indexesToRemove = readNotifications
            .Skip(MaxRetainedReadNotifications)
            .Select(pair => pair.Index)
            .OrderByDescending(index => index)
            .ToList();

        foreach (var index in indexesToRemove)
            notifications.RemoveAt(index);

        return indexesToRemove.Count > 0;
    }

    private static void NormalizeShape(JsonObject root)
    {
        if (root[NotificationsProperty] is not JsonArray)
            root[NotificationsProperty] = new JsonArray();
    }

    private static JsonArray EnsureNotificationsArray(JsonObject root)
    {
        NormalizeShape(root);
        return root[NotificationsProperty]!.AsArray();
    }

    private static bool IsSupportedStatus(string? status) =>
        !string.IsNullOrWhiteSpace(status) && AllowedStatuses.Contains(status.Trim());

    private static bool IsSupportedType(string? notificationType) =>
        !string.IsNullOrWhiteSpace(notificationType) && AllowedTypes.Contains(notificationType.Trim());

    private static string? GetNodeString(JsonNode? node)
    {
        if (node is JsonValue value && value.TryGetValue<string>(out var text))
            return text;

        return null;
    }

    private static int GetNodeInt(JsonNode? node, int fallback)
    {
        if (node is JsonValue value && value.TryGetValue<int>(out var number))
            return number;

        return fallback;
    }

    private static bool SetIfDifferent(JsonObject obj, string propertyName, string value)
    {
        var current = GetNodeString(obj[propertyName]) ?? string.Empty;
        if (string.Equals(current, value, StringComparison.Ordinal))
            return false;

        obj[propertyName] = value;
        return true;
    }

    private static bool SetIfDifferent(JsonObject obj, string propertyName, int value)
    {
        var current = GetNodeInt(obj[propertyName], int.MinValue);
        if (current == value)
            return false;

        obj[propertyName] = value;
        return true;
    }

    private static DateTimeOffset ParseCreatedAt(JsonNode? node)
    {
        var text = GetNodeString(node);
        return DateTimeOffset.TryParse(text, out var parsed)
            ? parsed
            : DateTimeOffset.MinValue;
    }
}
