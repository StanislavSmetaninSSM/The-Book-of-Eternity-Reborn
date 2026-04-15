using System.Text.Json;
using System.Text.Json.Nodes;
using BookOfEternityClient.Core;

namespace BookOfEternityClient.Services;

internal static class AfterlifeArchiveState
{
    internal const string InvalidAfterlifeArchiveUpdateItemMessage =
        "game_state/meta/soul_state.json current afterlifeArchiveUpdates must contain only canonical archive update objects with supported command and canonical add/remove payload.";

    internal const string InvalidArchiveActionResolutionItemMessage =
        "game_state/meta/soul_state.json current archiveActionResolutions must contain only canonical archive resolution objects with required identity, supported mode/status, and canonical accepted-result payload.";

    internal const string InvalidCanonicalAfterlifeArchiveRootMessage =
        "game_state/meta/soul_state.json current afterlifeArchive must already be a canonical object with stored JsonArray and canonical stored entries/actionReceipts when present.";

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

    internal static bool TryDescribeInvalidCanonicalArchiveRoot(
        JsonObject root,
        out string failureDescription)
    {
        if (!root.TryGetPropertyValue(ContainerProperty, out var containerNode))
        {
            failureDescription = string.Empty;
            return false;
        }

        if (containerNode is not JsonObject container ||
            HasUnsupportedVisibleKeys(container, StoredProperty, ActionReceiptsProperty) ||
            !container.TryGetPropertyValue(StoredProperty, out var storedNode) ||
            storedNode is not JsonArray stored)
        {
            failureDescription = InvalidCanonicalAfterlifeArchiveRootMessage;
            return true;
        }

        foreach (var entryNode in stored)
        {
            if (entryNode is not JsonObject entry ||
                TryDescribeInvalidArchiveEntry(entry, out _))
            {
                failureDescription = InvalidCanonicalAfterlifeArchiveRootMessage;
                return true;
            }
        }

        if (container.TryGetPropertyValue(ActionReceiptsProperty, out var actionReceiptsNode))
        {
            if (actionReceiptsNode is not JsonArray actionReceipts ||
                TryDescribeInvalidCanonicalActionReceipts(actionReceipts, out _))
            {
                failureDescription = InvalidCanonicalAfterlifeArchiveRootMessage;
                return true;
            }
        }

        failureDescription = string.Empty;
        return false;
    }

    public static void ApplyUpdates(JsonObject root, JsonArray updates)
    {
        if (TryDescribeInvalidArchiveUpdates(updates, out var failureDescription))
            throw new InvalidOperationException(failureDescription);

        var stored = EnsureStoredArray(root);
        foreach (var commandNode in updates.OfType<JsonObject>())
        {
            var command = GetNodeString(commandNode["command"])!;
            switch (command.Trim().ToLowerInvariant())
            {
                case "add":
                    UpsertEntry(stored, commandNode["entry"]!.AsObject());
                    break;

                case "remove":
                    RemoveEntry(stored, GetNodeString(commandNode["archiveId"])!);
                    break;
            }
        }
    }

    public static void ApplyActionResolutions(JsonObject root, JsonArray resolutions, int currentTurn)
    {
        if (TryDescribeInvalidArchiveActionResolutions(resolutions, out var failureDescription))
            throw new InvalidOperationException(failureDescription);

        var stored = EnsureStoredArray(root);
        var receipts = EnsureActionReceiptsArray(root);

        foreach (var resolution in resolutions.OfType<JsonObject>())
        {
            var requestId = GetNodeString(resolution["requestId"]);
            var archiveId = GetNodeString(resolution["archiveId"]);
            var requestedMode = GetNodeString(resolution["requestedMode"]);
            var status = GetNodeString(resolution["status"]);

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

    internal static bool TryDescribeInvalidArchiveUpdates(
        JsonArray updates,
        out string failureDescription)
    {
        foreach (var updateNode in updates)
        {
            if (updateNode is not JsonObject update)
            {
                failureDescription = InvalidAfterlifeArchiveUpdateItemMessage;
                return true;
            }

            var command = GetNodeString(update["command"]);
            if (string.IsNullOrWhiteSpace(command))
            {
                failureDescription = InvalidAfterlifeArchiveUpdateItemMessage;
                return true;
            }

            switch (command.Trim().ToLowerInvariant())
            {
                case "add":
                    if (update["entry"] is not JsonObject entry ||
                        TryDescribeInvalidArchiveEntry(entry, out _))
                    {
                        failureDescription = InvalidAfterlifeArchiveUpdateItemMessage;
                        return true;
                    }

                    break;
                case "remove":
                    if (string.IsNullOrWhiteSpace(GetNodeString(update["archiveId"])))
                    {
                        failureDescription = InvalidAfterlifeArchiveUpdateItemMessage;
                        return true;
                    }

                    break;
                default:
                    failureDescription = InvalidAfterlifeArchiveUpdateItemMessage;
                    return true;
            }
        }

        failureDescription = string.Empty;
        return false;
    }

    internal static bool TryDescribeInvalidArchiveActionResolutions(
        JsonArray resolutions,
        out string failureDescription)
    {
        foreach (var resolutionNode in resolutions)
        {
            if (resolutionNode is not JsonObject resolution)
            {
                failureDescription = InvalidArchiveActionResolutionItemMessage;
                return true;
            }

            var requestId = GetNodeString(resolution["requestId"]);
            var archiveId = GetNodeString(resolution["archiveId"]);
            var requestedMode = GetNodeString(resolution["requestedMode"]);
            var status = GetNodeString(resolution["status"]);
            if (string.IsNullOrWhiteSpace(requestId) ||
                string.IsNullOrWhiteSpace(archiveId) ||
                !AfterlifeArchiveActionState.IsSupportedRequestedMode(requestedMode) ||
                !AfterlifeArchiveActionState.IsSupportedResolutionStatus(status) ||
                !HasOptionalNullableStringShape(resolution["guardianId"]) ||
                !HasOptionalNullableStringShape(resolution["guardianName"]) ||
                !HasOptionalNullableStringShape(resolution["targetProjectId"]) ||
                !HasOptionalNullableStringShape(resolution["resultMode"]) ||
                !HasOptionalNullableStringShape(resolution["reason"]) ||
                !HasOptionalNullableStringShape(resolution["resolvedAtUtc"]) ||
                !HasOptionalNonNegativeIntegerShape(resolution["resultAmount"]))
            {
                failureDescription = InvalidArchiveActionResolutionItemMessage;
                return true;
            }

            foreach (var outcomeField in AfterlifeArchiveActionState.GetConsultationOutcomeFields())
            {
                if (!HasOptionalNonNegativeIntegerShape(resolution[outcomeField]))
                {
                    failureDescription = InvalidArchiveActionResolutionItemMessage;
                    return true;
                }
            }

            if (string.Equals(requestedMode, AfterlifeArchiveActionState.RequestedModeConsultation, StringComparison.OrdinalIgnoreCase) &&
                string.Equals(status, AfterlifeArchiveActionState.ResolutionStatusAccepted, StringComparison.OrdinalIgnoreCase) &&
                GetConsultationOutcomeTotal(resolution) <= 0)
            {
                failureDescription = InvalidArchiveActionResolutionItemMessage;
                return true;
            }

            if (string.Equals(requestedMode, AfterlifeArchiveActionState.RequestedModeProjectFuel, StringComparison.OrdinalIgnoreCase) &&
                string.Equals(status, AfterlifeArchiveActionState.ResolutionStatusAccepted, StringComparison.OrdinalIgnoreCase))
            {
                var resultMode = GetNodeString(resolution["resultMode"]);
                if (!AfterlifeArchiveActionState.IsSupportedProjectFuelResultMode(resultMode) ||
                    !TryGetPositiveIntegerValue(resolution["resultAmount"], out _))
                {
                    failureDescription = InvalidArchiveActionResolutionItemMessage;
                    return true;
                }
            }
        }

        failureDescription = string.Empty;
        return false;
    }

    private static bool TryDescribeInvalidCanonicalActionReceipts(
        JsonArray receipts,
        out string failureDescription)
    {
        var seenIdentityKeys = new HashSet<string>(StringComparer.Ordinal);

        foreach (var receiptNode in receipts)
        {
            if (receiptNode is not JsonObject receipt)
            {
                failureDescription = InvalidCanonicalAfterlifeArchiveRootMessage;
                return true;
            }

            var requestId = GetNodeString(receipt["requestId"]);
            var archiveId = GetNodeString(receipt["archiveId"]);
            var requestedMode = GetNodeString(receipt["requestedMode"]);
            var status = GetNodeString(receipt["status"]);
            var identityKey = TryBuildActionIdentityKey(requestId, archiveId, requestedMode);
            var resolvedAtUtc = GetNodeString(receipt["resolvedAtUtc"]);
            if (string.IsNullOrWhiteSpace(identityKey) ||
                !AfterlifeArchiveActionState.IsSupportedRequestedMode(requestedMode) ||
                !AfterlifeArchiveActionState.IsSupportedResolutionStatus(status) ||
                !HasOptionalNullableStringShape(receipt["guardianId"]) ||
                !HasOptionalNullableStringShape(receipt["guardianName"]) ||
                !HasOptionalNullableStringShape(receipt["targetProjectId"]) ||
                !HasOptionalNullableStringShape(receipt["resultMode"]) ||
                !HasOptionalNullableStringShape(receipt["reason"]) ||
                !TryGetNonNegativeIntegerValue(receipt["resolvedAtTurn"], out _) ||
                string.IsNullOrWhiteSpace(resolvedAtUtc) ||
                !DateTimeOffset.TryParse(resolvedAtUtc, out _) ||
                !HasOptionalNonNegativeIntegerShape(receipt["resultAmount"]))
            {
                failureDescription = InvalidCanonicalAfterlifeArchiveRootMessage;
                return true;
            }

            if (!seenIdentityKeys.Add(identityKey))
            {
                failureDescription = InvalidCanonicalAfterlifeArchiveRootMessage;
                return true;
            }

            foreach (var outcomeField in AfterlifeArchiveActionState.GetConsultationOutcomeFields())
            {
                if (!HasOptionalNonNegativeIntegerShape(receipt[outcomeField]))
                {
                    failureDescription = InvalidCanonicalAfterlifeArchiveRootMessage;
                    return true;
                }
            }

            if (string.Equals(requestedMode, AfterlifeArchiveActionState.RequestedModeConsultation, StringComparison.OrdinalIgnoreCase) &&
                string.Equals(status, AfterlifeArchiveActionState.ResolutionStatusAccepted, StringComparison.OrdinalIgnoreCase) &&
                GetConsultationOutcomeTotal(receipt) <= 0)
            {
                failureDescription = InvalidCanonicalAfterlifeArchiveRootMessage;
                return true;
            }

            if (string.Equals(requestedMode, AfterlifeArchiveActionState.RequestedModeProjectFuel, StringComparison.OrdinalIgnoreCase) &&
                string.Equals(status, AfterlifeArchiveActionState.ResolutionStatusAccepted, StringComparison.OrdinalIgnoreCase))
            {
                var resultMode = GetNodeString(receipt["resultMode"]);
                if (!AfterlifeArchiveActionState.IsSupportedProjectFuelResultMode(resultMode) ||
                    !TryGetPositiveIntegerValue(receipt["resultAmount"], out _))
                {
                    failureDescription = InvalidCanonicalAfterlifeArchiveRootMessage;
                    return true;
                }
            }
        }

        failureDescription = string.Empty;
        return false;
    }

    private static bool TryDescribeInvalidArchiveEntry(
        JsonObject entry,
        out string failureDescription)
    {
        var archiveId = GetNodeString(entry["archiveId"]);
        var entryType = GetNodeString(entry["entryType"]);
        var title = GetNodeString(entry["title"]);
        var summary = GetNodeString(entry["summary"]);
        var rarity = GetNodeString(entry["rarity"]);
        var acquiredAtUtc = GetNodeString(entry["acquiredAtUtc"]);
        var sourceKind = GetNodeString(entry["sourceKind"]);

        if (string.IsNullOrWhiteSpace(archiveId) ||
            string.IsNullOrWhiteSpace(entryType) ||
            !IsAllowedEntryType(entryType) ||
            string.IsNullOrWhiteSpace(title) ||
            string.IsNullOrWhiteSpace(summary) ||
            string.IsNullOrWhiteSpace(rarity) ||
            !IsSupportedArchiveRarity(rarity) ||
            !TryGetNonNegativeIntegerValue(entry["sourceLife"], out _) ||
            string.IsNullOrWhiteSpace(acquiredAtUtc) ||
            !DateTimeOffset.TryParse(acquiredAtUtc, out _) ||
            !HasOptionalNullableStringShape(entry["sourceGuardianId"]) ||
            !HasOptionalNullableStringShape(entry["sourceEntryId"]))
        {
            failureDescription = InvalidAfterlifeArchiveUpdateItemMessage;
            return true;
        }

        if (!string.IsNullOrWhiteSpace(sourceKind) && !IsSupportedSourceKind(sourceKind))
        {
            failureDescription = InvalidAfterlifeArchiveUpdateItemMessage;
            return true;
        }

        if (entry.TryGetPropertyValue("tags", out var tagsNode) &&
            !IsArrayOfStrings(tagsNode))
        {
            failureDescription = InvalidAfterlifeArchiveUpdateItemMessage;
            return true;
        }

        if (entry.TryGetPropertyValue("reservation", out var reservationNode))
        {
            if (reservationNode is not JsonObject reservation ||
                TryDescribeInvalidReservation(reservation, out _))
            {
                failureDescription = InvalidAfterlifeArchiveUpdateItemMessage;
                return true;
            }
        }

        failureDescription = string.Empty;
        return false;
    }

    private static bool TryDescribeInvalidReservation(
        JsonObject reservation,
        out string failureDescription)
    {
        var reservationKind = GetNodeString(reservation["reservationKind"]);
        var requestId = GetNodeString(reservation["requestId"]);
        var guardianId = GetNodeString(reservation["guardianId"]);
        var createdAtUtc = GetNodeString(reservation["createdAtUtc"]);
        if (string.IsNullOrWhiteSpace(reservationKind) ||
            !IsSupportedReservationKind(reservationKind) ||
            string.IsNullOrWhiteSpace(requestId) ||
            string.IsNullOrWhiteSpace(guardianId) ||
            !HasOptionalNullableStringShape(reservation["guardianName"]) ||
            !HasOptionalNullableStringShape(reservation["targetProjectId"]) ||
            !HasOptionalNullableStringShape(reservation["targetProjectName"]) ||
            !TryGetNonNegativeIntegerValue(reservation["createdAtTurn"], out _) ||
            string.IsNullOrWhiteSpace(createdAtUtc) ||
            !DateTimeOffset.TryParse(createdAtUtc, out _))
        {
            failureDescription = InvalidAfterlifeArchiveUpdateItemMessage;
            return true;
        }

        failureDescription = string.Empty;
        return false;
    }

    private static bool HasUnsupportedVisibleKeys(JsonObject obj, params string[] allowedVisibleKeys)
    {
        var allowed = allowedVisibleKeys.ToHashSet(StringComparer.OrdinalIgnoreCase);
        foreach (var property in obj)
        {
            if (property.Key.StartsWith("_", StringComparison.OrdinalIgnoreCase))
                continue;

            if (!allowed.Contains(property.Key))
                return true;
        }

        return false;
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

    public static bool ActionReceiptMatchesRequest(
        JsonObject? receipt,
        string requestId,
        string archiveId,
        string requestedMode)
    {
        if (receipt == null ||
            string.IsNullOrWhiteSpace(requestId) ||
            string.IsNullOrWhiteSpace(archiveId) ||
            string.IsNullOrWhiteSpace(requestedMode))
        {
            return false;
        }

        var requestIdentityKey = TryBuildActionIdentityKey(requestId, archiveId, requestedMode);
        var receiptIdentityKey = TryBuildActionIdentityKey(receipt);
        return !string.IsNullOrWhiteSpace(requestIdentityKey) &&
               string.Equals(receiptIdentityKey, requestIdentityKey, StringComparison.Ordinal);
    }

    public static bool HasActionReceipt(
        JsonArray receipts,
        string requestId,
        string archiveId,
        string requestedMode)
    {
        if (string.IsNullOrWhiteSpace(requestId) ||
            string.IsNullOrWhiteSpace(archiveId) ||
            string.IsNullOrWhiteSpace(requestedMode))
        {
            return false;
        }

        return receipts.OfType<JsonObject>().Any(receipt =>
            ActionReceiptMatchesRequest(receipt, requestId, archiveId, requestedMode));
    }

    public static JsonObject? FindActionReceipt(
        JsonArray receipts,
        string requestId,
        string archiveId,
        string requestedMode)
    {
        if (string.IsNullOrWhiteSpace(requestId) ||
            string.IsNullOrWhiteSpace(archiveId) ||
            string.IsNullOrWhiteSpace(requestedMode))
        {
            return null;
        }

        return receipts.OfType<JsonObject>().FirstOrDefault(receipt =>
            ActionReceiptMatchesRequest(receipt, requestId, archiveId, requestedMode));
    }

    public static string? TryBuildActionIdentityKey(
        string? requestId,
        string? archiveId,
        string? requestedMode)
    {
        if (string.IsNullOrWhiteSpace(requestId) ||
            string.IsNullOrWhiteSpace(archiveId) ||
            string.IsNullOrWhiteSpace(requestedMode))
        {
            return null;
        }

        return $"{requestId.Trim().ToLowerInvariant()}|{archiveId.Trim().ToLowerInvariant()}|{requestedMode.Trim().ToLowerInvariant()}";
    }

    public static string? TryBuildActionIdentityKey(JsonObject? receipt)
    {
        if (receipt == null)
            return null;

        return TryBuildActionIdentityKey(
            GetNodeString(receipt["requestId"]),
            GetNodeString(receipt["archiveId"]),
            GetNodeString(receipt["requestedMode"]));
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
        var receiptIdentityKey = TryBuildActionIdentityKey(receipt);
        if (string.IsNullOrWhiteSpace(receiptIdentityKey))
        {
            return;
        }

        for (var i = 0; i < receipts.Count; i++)
        {
            if (receipts[i] is not JsonObject existing)
                continue;

            if (!string.Equals(TryBuildActionIdentityKey(existing), receiptIdentityKey, StringComparison.Ordinal))
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

    private static bool HasOptionalNullableStringShape(JsonNode? node)
    {
        if (node == null)
            return true;

        return node is JsonValue value && value.TryGetValue<string>(out _);
    }

    private static bool HasOptionalNonNegativeIntegerShape(JsonNode? node)
    {
        if (node == null)
            return true;

        return TryGetNonNegativeIntegerValue(node, out _);
    }

    private static bool TryGetNonNegativeIntegerValue(JsonNode? node, out int value)
    {
        value = 0;
        if (node is not JsonValue jsonValue)
            return false;

        if (jsonValue.TryGetValue<int>(out var intValue) && intValue >= 0)
        {
            value = intValue;
            return true;
        }

        if (jsonValue.TryGetValue<long>(out var longValue) &&
            longValue is >= 0 and <= int.MaxValue)
        {
            value = (int)longValue;
            return true;
        }

        return false;
    }

    private static bool TryGetPositiveIntegerValue(JsonNode? node, out int value)
    {
        value = 0;
        return TryGetNonNegativeIntegerValue(node, out value) && value > 0;
    }

    private static bool IsArrayOfStrings(JsonNode? node)
    {
        return node is JsonArray array &&
               array.All(item => item is JsonValue value && value.TryGetValue<string>(out _));
    }

    private static int GetConsultationOutcomeTotal(JsonObject resolution)
    {
        var total = 0;
        foreach (var outcomeField in AfterlifeArchiveActionState.GetConsultationOutcomeFields())
        {
            total += GetNodeInt(resolution[outcomeField]);
        }

        return total;
    }

    private static int GetNodeInt(JsonNode? node)
    {
        if (node is JsonValue value && value.TryGetValue<int>(out var number))
            return number;

        return 0;
    }
}
