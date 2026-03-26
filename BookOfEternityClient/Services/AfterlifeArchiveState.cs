using System.Text.Json;
using System.Text.Json.Nodes;
using BookOfEternityClient.Core;

namespace BookOfEternityClient.Services;

internal static class AfterlifeArchiveState
{
    public const string ContainerProperty = "afterlifeArchive";
    public const string StoredProperty = "stored";
    public const string ActionReceiptsProperty = "actionReceipts";
    public const string ReservationProperty = "reservation";

    public const string EntryTypeLoreFragment = "lore_fragment";
    public const string EntryTypeSecretRecord = "secret_record";
    public const string SourceKindCodex = "codex";
    public const string SourceKindSystem = "system";
    public const string ReservationKindConsultation = "consultation";
    public const string ReservationKindProjectFuel = "project_fuel";

    private static readonly HashSet<string> AllowedEntryTypes = new(StringComparer.OrdinalIgnoreCase)
    {
        EntryTypeLoreFragment,
        EntryTypeSecretRecord
    };

    private static readonly HashSet<string> AllowedSourceKinds = new(StringComparer.OrdinalIgnoreCase)
    {
        SourceKindCodex,
        SourceKindSystem
    };

    private static readonly HashSet<string> AllowedReservationKinds = new(StringComparer.OrdinalIgnoreCase)
    {
        ReservationKindConsultation,
        ReservationKindProjectFuel
    };

    public static bool IsAllowedEntryType(string? entryType) =>
        !string.IsNullOrWhiteSpace(entryType) && AllowedEntryTypes.Contains(entryType.Trim());

    public static bool IsSupportedSourceKind(string? sourceKind) =>
        !string.IsNullOrWhiteSpace(sourceKind) && AllowedSourceKinds.Contains(sourceKind.Trim());

    public static bool IsSupportedReservationKind(string? reservationKind) =>
        !string.IsNullOrWhiteSpace(reservationKind) && AllowedReservationKinds.Contains(reservationKind.Trim());

    public static void NormalizeShape(JsonObject root)
    {
        if (root[ContainerProperty] is JsonArray legacyArray)
        {
            root[ContainerProperty] = new JsonObject
            {
                [StoredProperty] = legacyArray.DeepClone(),
                [ActionReceiptsProperty] = new JsonArray()
            };
            return;
        }

        if (root[ContainerProperty] is not JsonObject container)
        {
            root[ContainerProperty] = new JsonObject
            {
                [StoredProperty] = new JsonArray(),
                [ActionReceiptsProperty] = new JsonArray()
            };
            return;
        }

        if (container[StoredProperty] is not JsonArray)
            container[StoredProperty] = new JsonArray();
        if (container[ActionReceiptsProperty] is not JsonArray)
            container[ActionReceiptsProperty] = new JsonArray();

        CleanupLegacyArchiveMetadata(container[StoredProperty]!.AsArray());
    }

    public static JsonArray EnsureStoredArray(JsonObject root)
    {
        NormalizeShape(root);
        return ((JsonObject)root[ContainerProperty]!)[StoredProperty]!.AsArray();
    }

    public static JsonArray EnsureActionReceiptsArray(JsonObject root)
    {
        NormalizeShape(root);
        return ((JsonObject)root[ContainerProperty]!)[ActionReceiptsProperty]!.AsArray();
    }

    public static void ApplyUpdates(JsonObject root, JsonArray updates)
    {
        var stored = EnsureStoredArray(root);
        foreach (var commandNode in updates.OfType<JsonObject>())
        {
            var command = GetNodeString(commandNode["command"]);
            if (string.IsNullOrWhiteSpace(command))
                continue;

            switch (command.Trim().ToLowerInvariant())
            {
                case "add":
                    if (commandNode["entry"] is JsonObject entry)
                        UpsertEntry(stored, entry);
                    break;

                case "remove":
                    var archiveId = GetNodeString(commandNode["archiveId"]);
                    if (!string.IsNullOrWhiteSpace(archiveId))
                        RemoveEntry(stored, archiveId!);
                    break;
            }
        }
    }

    public static void ApplyActionResolutions(JsonObject root, JsonArray resolutions, int currentTurn)
    {
        var stored = EnsureStoredArray(root);
        var receipts = EnsureActionReceiptsArray(root);

        foreach (var resolution in resolutions.OfType<JsonObject>())
        {
            var requestId = GetNodeString(resolution["requestId"]);
            var archiveId = GetNodeString(resolution["archiveId"]);
            var requestedMode = GetNodeString(resolution["requestedMode"]);
            var status = GetNodeString(resolution["status"]);
            if (string.IsNullOrWhiteSpace(requestId) ||
                string.IsNullOrWhiteSpace(archiveId) ||
                !AfterlifeArchiveActionState.IsSupportedRequestedMode(requestedMode) ||
                !AfterlifeArchiveActionState.IsSupportedResolutionStatus(status))
            {
                continue;
            }

            var entry = FindEntry(stored, archiveId!);
            var reservation = GetReservationObject(entry);
            if (ReservationMatchesRequest(reservation, requestId!, requestedMode!))
            {
                if (string.Equals(status, AfterlifeArchiveActionState.ResolutionStatusAccepted, StringComparison.OrdinalIgnoreCase))
                    RemoveEntry(stored, archiveId!);
                else if (entry != null)
                    ClearReservation(entry);
            }

            UpsertActionReceipt(receipts, new JsonObject
            {
                ["requestId"] = requestId,
                ["archiveId"] = archiveId,
                ["requestedMode"] = requestedMode,
                ["status"] = status,
                ["guardianId"] = GetNodeString(resolution["guardianId"]) ?? string.Empty,
                ["guardianName"] = GetNodeString(resolution["guardianName"]) ?? string.Empty,
                ["targetProjectId"] = GetNodeString(resolution["targetProjectId"]) ?? string.Empty,
                ["resultMode"] = GetNodeString(resolution["resultMode"]) ?? string.Empty,
                ["resultAmount"] = GetNodeInt(resolution["resultAmount"]),
                [AfterlifeArchiveActionState.ConsultationOutcomeGuaranteedArchiveQuestCount] = GetNodeInt(resolution[AfterlifeArchiveActionState.ConsultationOutcomeGuaranteedArchiveQuestCount]),
                [AfterlifeArchiveActionState.ConsultationOutcomeQuestHookCount] = GetNodeInt(resolution[AfterlifeArchiveActionState.ConsultationOutcomeQuestHookCount]),
                [AfterlifeArchiveActionState.ConsultationOutcomeSpecialQuestLineUnlocks] = GetNodeInt(resolution[AfterlifeArchiveActionState.ConsultationOutcomeSpecialQuestLineUnlocks]),
                [AfterlifeArchiveActionState.ConsultationOutcomeVisibleRivalClueBonus] = GetNodeInt(resolution[AfterlifeArchiveActionState.ConsultationOutcomeVisibleRivalClueBonus]),
                [AfterlifeArchiveActionState.ConsultationOutcomeArchiveWarningTierBonus] = GetNodeInt(resolution[AfterlifeArchiveActionState.ConsultationOutcomeArchiveWarningTierBonus]),
                ["reason"] = GetNodeString(resolution["reason"]) ?? string.Empty,
                ["resolvedAtTurn"] = Math.Max(0, currentTurn),
                ["resolvedAtUtc"] = GetNodeString(resolution["resolvedAtUtc"]) ?? DateTime.UtcNow.ToString("o")
            });
        }
    }

    public static void UpsertEntry(JsonArray stored, JsonObject entry)
    {
        var archiveId = GetNodeString(entry["archiveId"]);
        if (string.IsNullOrWhiteSpace(archiveId))
            return;

        for (var i = 0; i < stored.Count; i++)
        {
            if (stored[i] is not JsonObject existing)
                continue;

            if (string.Equals(GetNodeString(existing["archiveId"]), archiveId, StringComparison.OrdinalIgnoreCase))
            {
                stored[i] = entry.DeepClone();
                return;
            }
        }

        stored.Add(entry.DeepClone());
    }

    public static bool RemoveEntry(JsonArray stored, string archiveId)
    {
        for (var i = 0; i < stored.Count; i++)
        {
            if (stored[i] is not JsonObject existing)
                continue;

            if (!string.Equals(GetNodeString(existing["archiveId"]), archiveId, StringComparison.OrdinalIgnoreCase))
                continue;

            stored.RemoveAt(i);
            return true;
        }

        return false;
    }

    public static JsonObject? FindEntry(JsonArray stored, string archiveId)
    {
        return stored.OfType<JsonObject>()
            .FirstOrDefault(existing => string.Equals(GetNodeString(existing["archiveId"]), archiveId, StringComparison.OrdinalIgnoreCase));
    }

    public static bool TryReserveEntry(
        JsonArray stored,
        string archiveId,
        string reservationKind,
        string requestId,
        string guardianId,
        string guardianName,
        int createdAtTurn,
        string? targetProjectId = null,
        string? targetProjectName = null)
    {
        var entry = FindEntry(stored, archiveId);
        if (entry == null || IsReserved(entry) || !IsSupportedReservationKind(reservationKind))
            return false;

        entry[ReservationProperty] = new JsonObject
        {
            ["reservationKind"] = reservationKind,
            ["requestId"] = requestId,
            ["guardianId"] = guardianId,
            ["guardianName"] = guardianName,
            ["targetProjectId"] = targetProjectId ?? string.Empty,
            ["targetProjectName"] = targetProjectName ?? string.Empty,
            ["createdAtTurn"] = Math.Max(0, createdAtTurn),
            ["createdAtUtc"] = DateTime.UtcNow.ToString("o")
        };
        return true;
    }

    public static bool IsReserved(JsonObject? entry) =>
        GetReservationObject(entry) != null;

    public static JsonObject? GetReservationObject(JsonObject? entry)
    {
        if (entry?[ReservationProperty] is JsonObject reservation &&
            IsSupportedReservationKind(GetNodeString(reservation["reservationKind"])) &&
            !string.IsNullOrWhiteSpace(GetNodeString(reservation["requestId"])))
        {
            return reservation;
        }

        return null;
    }

    public static bool ReservationMatchesRequest(JsonObject? reservation, string requestId, string requestedMode)
    {
        if (reservation == null)
            return false;

        return string.Equals(GetNodeString(reservation["requestId"]), requestId, StringComparison.OrdinalIgnoreCase) &&
               string.Equals(GetNodeString(reservation["reservationKind"]), requestedMode, StringComparison.OrdinalIgnoreCase);
    }

    public static void ClearReservation(JsonObject entry)
    {
        entry.Remove(ReservationProperty);
    }

    public static bool HasMatchingReservation(JsonArray stored, string archiveId, string requestId, string requestedMode)
    {
        return ReservationMatchesRequest(GetReservationObject(FindEntry(stored, archiveId)), requestId, requestedMode);
    }

    public static bool HasActionReceipt(JsonArray receipts, string requestId)
    {
        return receipts.OfType<JsonObject>()
            .Any(receipt => string.Equals(GetNodeString(receipt["requestId"]), requestId, StringComparison.OrdinalIgnoreCase));
    }

    public static int ResolvePowerGainForArchiveRarity(string? rarity) =>
        AbodePowerRules.ResolvePowerGainForArchiveRarity(rarity);

    public static bool IsSupportedArchiveRarity(string? rarity) =>
        (rarity ?? string.Empty).Trim().ToLowerInvariant() is
            "common" or "uncommon" or "rare" or "epic" or "legendary" or "unique";

    public static string GetEntryTypeLabel(string? entryType) =>
        (entryType ?? string.Empty).Trim().ToLowerInvariant() switch
        {
            EntryTypeLoreFragment => "Фрагмент знания",
            EntryTypeSecretRecord => "Запись тайны",
            _ => "Архивная запись"
        };

    public static string GetSourceKindLabel(string? sourceKind) =>
        (sourceKind ?? string.Empty).Trim().ToLowerInvariant() switch
        {
            SourceKindCodex => "Кодекс",
            SourceKindSystem => "Системная награда",
            _ => "Неизвестный источник"
        };

    public static string GetReservationLabel(string? reservationKind) =>
        (reservationKind ?? string.Empty).Trim().ToLowerInvariant() switch
        {
            ReservationKindConsultation => "архивная консультация",
            ReservationKindProjectFuel => "подпитка проекта",
            _ => "ожидающее действие"
        };

    public static bool TryGetOfferingTypeForEntryType(string? entryType, out string offeringType)
    {
        switch ((entryType ?? string.Empty).Trim().ToLowerInvariant())
        {
            case EntryTypeLoreFragment:
                offeringType = GuardianAbodeOfferingState.OfferingTypeArchiveLoreFragment;
                return true;
            case EntryTypeSecretRecord:
                offeringType = GuardianAbodeOfferingState.OfferingTypeArchiveSecretRecord;
                return true;
            default:
                offeringType = string.Empty;
                return false;
        }
    }

    public static bool OfferingTypeMatchesEntryType(string? offeringType, string? entryType)
    {
        if (!TryGetOfferingTypeForEntryType(entryType, out var expectedOfferingType))
            return false;

        return string.Equals(offeringType, expectedOfferingType, StringComparison.OrdinalIgnoreCase);
    }

    private static void UpsertActionReceipt(JsonArray receipts, JsonObject receipt)
    {
        var requestId = GetNodeString(receipt["requestId"]);
        if (string.IsNullOrWhiteSpace(requestId))
            return;

        for (var i = 0; i < receipts.Count; i++)
        {
            if (receipts[i] is not JsonObject existing)
                continue;

            if (!string.Equals(GetNodeString(existing["requestId"]), requestId, StringComparison.OrdinalIgnoreCase))
                continue;

            receipts[i] = receipt;
            return;
        }

        receipts.Add(receipt);
    }

    private static void CleanupLegacyArchiveMetadata(JsonArray stored)
    {
        foreach (var entry in stored.OfType<JsonObject>())
        {
            entry.Remove("codexCategory");
            entry.Remove("facets");
        }
    }

    private static string? GetNodeString(JsonNode? node)
    {
        if (node is JsonValue value)
        {
            if (value.TryGetValue<string>(out var str))
                return str;
        }

        return null;
    }

    private static int GetNodeInt(JsonNode? node)
    {
        if (node is JsonValue value && value.TryGetValue<int>(out var number))
            return number;

        return 0;
    }
}
