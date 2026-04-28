using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using System.Text.Json.Nodes;
using System.Text.RegularExpressions;
using BookOfEternityClient.Core;
using BookOfEternityClient.Configuration;
using BookOfEternityClient.Models;
using Microsoft.Extensions.Logging;

namespace BookOfEternityClient.Services;
public partial class ValidationService
{
    private void ValidateAfterlifeArchiveData(JsonElement root, string contextPrefix, List<ValidationIssue> issues)
    {
        if (root.TryGetProperty("afterlifeArchiveUpdates", out var archiveUpdates))
        {
            var updatesContext = $"{contextPrefix}.afterlifeArchiveUpdates";
            RequireArrayOfObjects(archiveUpdates, updatesContext, issues);
            if (archiveUpdates.ValueKind == JsonValueKind.Array)
            {
                var updateIndex = 0;
                foreach (var update in archiveUpdates.EnumerateArray())
                {
                    var updateContext = $"{updatesContext}[{updateIndex++}]";
                    if (!RequireObject(update, updateContext, issues))
                        continue;

                    var command = RequireString(update, updateContext, issues, "command");
                    if (!string.IsNullOrWhiteSpace(command) &&
                        !string.Equals(command, "add", StringComparison.OrdinalIgnoreCase) &&
                        !string.Equals(command, "remove", StringComparison.OrdinalIgnoreCase))
                    {
                        issues.Add(new ValidationIssue(
                            $"{updateContext}.command",
                            IssueSeverity.Error,
                            "afterlifeArchiveUpdates поддерживает только add или remove",
                            code: "afterlife_archive_invalid_command",
                            section: "AfterlifeArchive",
                            expected: "add | remove",
                            actual: command,
                            repairHint: "Используй для afterlifeArchiveUpdates только команды add или remove."));
                        continue;
                    }

                    if (string.Equals(command, "add", StringComparison.OrdinalIgnoreCase))
                    {
                        if (update.TryGetProperty("entry", out var entry) && RequireObject(entry, $"{updateContext}.entry", issues))
                            ValidateAfterlifeArchiveEntryObject(entry, $"{updateContext}.entry", issues);
                        else
                            issues.Add(new ValidationIssue(
                                $"{updateContext}.entry",
                                IssueSeverity.Error,
                                "afterlifeArchiveUpdates add должен содержать полный entry object",
                                code: "afterlife_archive_add_missing_entry",
                                section: "AfterlifeArchive",
                                repairHint: "Для add передавай полный entry object с archiveId, entryType, title, summary, rarity, sourceLife и acquiredAtUtc."));
                    }
                    else if (string.Equals(command, "remove", StringComparison.OrdinalIgnoreCase))
                    {
                        RequireString(update, updateContext, issues, "archiveId");
                    }
                }
            }
        }

        if (root.TryGetProperty("archiveActionResolutions", out var archiveActionResolutions))
        {
            var resolutionsContext = $"{contextPrefix}.archiveActionResolutions";
            RequireArrayOfObjects(archiveActionResolutions, resolutionsContext, issues);
            if (archiveActionResolutions.ValueKind == JsonValueKind.Array)
            {
                var resolutionIndex = 0;
                foreach (var resolution in archiveActionResolutions.EnumerateArray())
                {
                    var resolutionContext = $"{resolutionsContext}[{resolutionIndex++}]";
                    if (!RequireObject(resolution, resolutionContext, issues))
                        continue;

                    RequireString(resolution, resolutionContext, issues, "requestId");
                    RequireString(resolution, resolutionContext, issues, "archiveId");
                    var requestedMode = RequireString(resolution, resolutionContext, issues, "requestedMode");
                    var status = RequireString(resolution, resolutionContext, issues, "status");
                    ValidateOptionalNullableStringField(resolution, resolutionContext, issues, "guardianId");
                    ValidateOptionalNullableStringField(resolution, resolutionContext, issues, "guardianName");
                    ValidateOptionalNullableStringField(resolution, resolutionContext, issues, "targetProjectId");
                    ValidateOptionalNullableStringField(resolution, resolutionContext, issues, "resultMode");
                    ValidateNonNegativeIntegerField(resolution, resolutionContext, issues, "resultAmount", "AfterlifeArchive");
                    foreach (var outcomeField in AfterlifeArchiveActionState.GetConsultationOutcomeFields())
                        ValidateNonNegativeIntegerField(resolution, resolutionContext, issues, outcomeField, "AfterlifeArchive");
                    ValidateOptionalNullableStringField(resolution, resolutionContext, issues, "reason");
                    ValidateOptionalNullableStringField(resolution, resolutionContext, issues, "resolvedAtUtc");

                    if (!string.IsNullOrWhiteSpace(requestedMode) && !AfterlifeArchiveActionState.IsSupportedRequestedMode(requestedMode))
                    {
                        issues.Add(new ValidationIssue(
                            $"{resolutionContext}.requestedMode",
                            IssueSeverity.Error,
                            "archiveActionResolutions.requestedMode должен быть canonical archive action mode",
                            code: "archive_action_resolution_invalid_requested_mode",
                            section: "AfterlifeArchive",
                            expected: $"{AfterlifeArchiveActionState.RequestedModeConsultation} | {AfterlifeArchiveActionState.RequestedModeProjectFuel}",
                            actual: requestedMode));
                    }

                    if (!string.IsNullOrWhiteSpace(status) && !AfterlifeArchiveActionState.IsSupportedResolutionStatus(status))
                    {
                        issues.Add(new ValidationIssue(
                            $"{resolutionContext}.status",
                            IssueSeverity.Error,
                            "archiveActionResolutions.status должен быть canonical resolution status",
                            code: "archive_action_resolution_invalid_status",
                            section: "AfterlifeArchive",
                            expected: $"{AfterlifeArchiveActionState.ResolutionStatusAccepted} | {AfterlifeArchiveActionState.ResolutionStatusRejected} | {AfterlifeArchiveActionState.ResolutionStatusCancelled}",
                            actual: status));
                    }

                    if (string.Equals(requestedMode, AfterlifeArchiveActionState.RequestedModeProjectFuel, StringComparison.OrdinalIgnoreCase) &&
                        string.Equals(status, AfterlifeArchiveActionState.ResolutionStatusAccepted, StringComparison.OrdinalIgnoreCase))
                    {
                        var resultMode = GetFirstNonEmptyString(resolution, "resultMode");
                        var resultAmount = GetIntOrDefault(resolution, "resultAmount");
                        if (!AfterlifeArchiveActionState.IsSupportedProjectFuelResultMode(resultMode))
                        {
                            issues.Add(new ValidationIssue(
                                $"{resolutionContext}.resultMode",
                                IssueSeverity.Error,
                                "Accepted archive project fuel resolution должен указывать canonical resultMode",
                                code: "archive_project_fuel_resolution_invalid_result_mode",
                                section: "AfterlifeArchive",
                                expected: $"{AfterlifeArchiveActionState.ProjectFuelResultModeProjectWork} | {AfterlifeArchiveActionState.ProjectFuelResultModePressureRelief}",
                                actual: resultMode,
                                repairHint: "Для accepted archive project fuel передавай resultMode = project_work или pressure_relief."));
                        }

                        if (resultAmount <= 0)
                        {
                            issues.Add(new ValidationIssue(
                                $"{resolutionContext}.resultAmount",
                                IssueSeverity.Error,
                                "Accepted archive project fuel resolution должен указывать resultAmount > 0",
                                code: "archive_project_fuel_resolution_invalid_result_amount",
                                section: "AfterlifeArchive",
                                expected: "> 0",
                                actual: resultAmount.ToString(),
                                repairHint: "Для accepted archive project fuel передавай точную положительную величину эффекта в resultAmount."));
                        }
                    }

                    if (string.Equals(requestedMode, AfterlifeArchiveActionState.RequestedModeConsultation, StringComparison.OrdinalIgnoreCase) &&
                        string.Equals(status, AfterlifeArchiveActionState.ResolutionStatusAccepted, StringComparison.OrdinalIgnoreCase) &&
                        GetConsultationOutcomeTotal(resolution) <= 0)
                    {
                        issues.Add(new ValidationIssue(
                            resolutionContext,
                            IssueSeverity.Error,
                            "Accepted archive consultation resolution должен указывать machine-readable consultation outcome",
                            code: "archive_consultation_resolution_missing_outcome",
                            section: "AfterlifeArchive",
                            repairHint: "Для accepted archive consultation передавай хотя бы один whitelist outcome field в archiveActionResolutions: guaranteedArchiveQuestCount, questHookCount, specialQuestLineUnlocks, visibleRivalClueBonus или archiveWarningTierBonus."));
                    }
                }
            }
        }

        if (!root.TryGetProperty("afterlifeArchive", out var archive))
            return;

        var archiveContext = $"{contextPrefix}.afterlifeArchive";
        if (!RequireObject(archive, archiveContext, issues))
            return;

        if (!archive.TryGetProperty("stored", out var stored))
        {
            issues.Add(new ValidationIssue(
                $"{archiveContext}.stored",
                IssueSeverity.Error,
                "afterlifeArchive должен содержать обязательный stored array",
                code: "afterlife_archive_missing_stored",
                section: "AfterlifeArchive",
                repairHint: "Сохраняй afterlifeArchive как объект с обязательным массивом stored."));
            return;
        }

        RequireArrayOfObjects(stored, $"{archiveContext}.stored", issues);
        if (stored.ValueKind != JsonValueKind.Array)
            return;

        var reservedRequestIds = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        var reservedActionIdentityKeys = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        var storedArchiveIds = new HashSet<string>(StringComparer.OrdinalIgnoreCase);

        var entryIndex = 0;
        foreach (var entry in stored.EnumerateArray())
        {
            var entryContext = $"{archiveContext}.stored[{entryIndex++}]";
            if (!RequireObject(entry, entryContext, issues))
                continue;

            ValidateAfterlifeArchiveEntryObject(entry, entryContext, issues);

            var archiveId = GetFirstNonEmptyString(entry, "archiveId");
            if (!string.IsNullOrWhiteSpace(archiveId))
                storedArchiveIds.Add(archiveId);

            if (entry.TryGetProperty("reservation", out var reservation) &&
                reservation.ValueKind == JsonValueKind.Object)
            {
                var requestId = GetFirstNonEmptyString(reservation, "requestId");
                if (!string.IsNullOrWhiteSpace(requestId) && !reservedRequestIds.Add(requestId))
                {
                    issues.Add(new ValidationIssue(
                        $"{entryContext}.reservation.requestId",
                        IssueSeverity.Error,
                        "Один archive requestId не может резервировать несколько архивных записей одновременно",
                        code: "afterlife_archive_duplicate_reservation_request",
                        section: "AfterlifeArchive",
                        actual: requestId));
                }

                var reservationIdentityKey = AfterlifeArchiveState.TryBuildActionIdentityKey(
                    requestId,
                    archiveId,
                    GetFirstNonEmptyString(reservation, "reservationKind"));
                if (!string.IsNullOrWhiteSpace(reservationIdentityKey))
                    reservedActionIdentityKeys.Add(reservationIdentityKey);
            }
        }

        if (archive.TryGetProperty("actionReceipts", out var actionReceipts))
        {
            RequireArrayOfObjects(actionReceipts, $"{archiveContext}.actionReceipts", issues);
            if (actionReceipts.ValueKind == JsonValueKind.Array)
                ValidateAfterlifeArchiveActionReceipts(actionReceipts, $"{archiveContext}.actionReceipts", storedArchiveIds, reservedActionIdentityKeys, issues);
        }
    }


    private void ValidateAfterlifeArchiveEntryObject(JsonElement entry, string context, List<ValidationIssue> issues)
    {
        RequireString(entry, context, issues, "archiveId");
        var entryType = RequireString(entry, context, issues, "entryType");
        RequireString(entry, context, issues, "title");
        RequireString(entry, context, issues, "summary");
        var rarity = RequireString(entry, context, issues, "rarity");
        ValidateNonNegativeIntegerField(entry, context, issues, "sourceLife", "AfterlifeArchive");
        var acquiredAtUtc = RequireString(entry, context, issues, "acquiredAtUtc");
        if (!string.IsNullOrWhiteSpace(acquiredAtUtc) && !DateTimeOffset.TryParse(acquiredAtUtc, out _))
        {
            issues.Add(new ValidationIssue(
                $"{context}.acquiredAtUtc",
                IssueSeverity.Error,
                "afterlife archive acquiredAtUtc должен быть ISO 8601 timestamp",
                code: "afterlife_archive_invalid_acquired_at",
                section: "AfterlifeArchive",
                expected: "ISO 8601 timestamp",
                actual: acquiredAtUtc));
        }

        if (!string.IsNullOrWhiteSpace(entryType) && !AfterlifeArchiveState.IsAllowedEntryType(entryType))
        {
            issues.Add(new ValidationIssue(
                $"{context}.entryType",
                IssueSeverity.Error,
                "afterlife archive entryType должен быть одним из canonical enum значений",
                code: "afterlife_archive_invalid_entry_type",
                section: "AfterlifeArchive",
                expected: $"{AfterlifeArchiveState.EntryTypeLoreFragment} | {AfterlifeArchiveState.EntryTypeSecretRecord}",
                actual: entryType,
                repairHint: "Используй для afterlife archive только canonical entryType: lore_fragment или secret_record."));
        }

        if (!string.IsNullOrWhiteSpace(rarity) && GetRarityRank(rarity) == 0)
        {
            issues.Add(new ValidationIssue(
                $"{context}.rarity",
                IssueSeverity.Error,
                "afterlife archive rarity должна быть canonical rarity tier",
                code: "afterlife_archive_invalid_rarity",
                section: "AfterlifeArchive",
                expected: "Common | Uncommon | Rare | Epic | Legendary | Unique",
                actual: rarity,
                repairHint: "Используй для afterlife archive только canonical rarity tiers."));
        }

        var sourceKind = GetFirstNonEmptyString(entry, "sourceKind");
        if (!string.IsNullOrWhiteSpace(sourceKind) && !AfterlifeArchiveState.IsSupportedSourceKind(sourceKind))
        {
            issues.Add(new ValidationIssue(
                $"{context}.sourceKind",
                IssueSeverity.Error,
                "afterlife archive sourceKind должен быть canonical afterlife source label",
                code: "afterlife_archive_invalid_source_kind",
                section: "AfterlifeArchive",
                expected: $"{AfterlifeArchiveState.SourceKindCodex} | {AfterlifeArchiveState.SourceKindSystem}",
                actual: sourceKind));
        }

        ValidateOptionalNullableStringField(entry, context, issues, "sourceGuardianId");
        ValidateOptionalNullableStringField(entry, context, issues, "sourceEntryId");
        if (entry.TryGetProperty("tags", out var tags))
            RequireArrayOfStrings(tags, $"{context}.tags", issues);

        if (entry.TryGetProperty("reservation", out var reservation))
        {
            if (!RequireObject(reservation, $"{context}.reservation", issues))
                return;

            var reservationKind = RequireString(reservation, $"{context}.reservation", issues, "reservationKind");
            var requestId = RequireString(reservation, $"{context}.reservation", issues, "requestId");
            RequireString(reservation, $"{context}.reservation", issues, "guardianId");
            ValidateOptionalNullableStringField(reservation, $"{context}.reservation", issues, "guardianName");
            ValidateOptionalNullableStringField(reservation, $"{context}.reservation", issues, "targetProjectId");
            ValidateOptionalNullableStringField(reservation, $"{context}.reservation", issues, "targetProjectName");
            ValidateNonNegativeIntegerField(reservation, $"{context}.reservation", issues, "createdAtTurn", "AfterlifeArchive");
            var createdAtUtc = RequireString(reservation, $"{context}.reservation", issues, "createdAtUtc");

            if (!string.IsNullOrWhiteSpace(reservationKind) && !AfterlifeArchiveState.IsSupportedReservationKind(reservationKind))
            {
                issues.Add(new ValidationIssue(
                    $"{context}.reservation.reservationKind",
                    IssueSeverity.Error,
                    "afterlife archive reservationKind должен быть canonical reservation mode",
                    code: "afterlife_archive_invalid_reservation_kind",
                    section: "AfterlifeArchive",
                    expected: $"{AfterlifeArchiveState.ReservationKindConsultation} | {AfterlifeArchiveState.ReservationKindProjectFuel}",
                    actual: reservationKind));
            }

            if (!string.IsNullOrWhiteSpace(createdAtUtc) && !DateTimeOffset.TryParse(createdAtUtc, out _))
            {
                issues.Add(new ValidationIssue(
                    $"{context}.reservation.createdAtUtc",
                    IssueSeverity.Error,
                    "afterlife archive reservation.createdAtUtc должен быть ISO 8601 timestamp",
                    code: "afterlife_archive_invalid_reservation_timestamp",
                    section: "AfterlifeArchive",
                    expected: "ISO 8601 timestamp",
                    actual: createdAtUtc));
            }

            if (string.IsNullOrWhiteSpace(requestId))
                return;
        }
    }


    private void ValidateAfterlifeArchiveActionReceipts(
        JsonElement actionReceipts,
        string context,
        IReadOnlySet<string> storedArchiveIds,
        IReadOnlySet<string> reservedActionIdentityKeys,
        List<ValidationIssue> issues)
    {
        var journalJson = _fs.ReadFileAsync(GuardianProjectState.JournalPath).GetAwaiter().GetResult();
        var seenReceiptIdentityKeys = new HashSet<string>(StringComparer.Ordinal);

        var receiptIndex = 0;
        foreach (var receipt in actionReceipts.EnumerateArray())
        {
            var receiptContext = $"{context}[{receiptIndex++}]";
            if (!RequireObject(receipt, receiptContext, issues))
                continue;

            var requestId = RequireString(receipt, receiptContext, issues, "requestId");
            var archiveId = RequireString(receipt, receiptContext, issues, "archiveId");
            var requestedMode = RequireString(receipt, receiptContext, issues, "requestedMode");
            var status = RequireString(receipt, receiptContext, issues, "status");
            var guardianId = GetFirstNonEmptyString(receipt, "guardianId");
            var targetProjectId = GetFirstNonEmptyString(receipt, "targetProjectId");
            var resultMode = GetFirstNonEmptyString(receipt, "resultMode");
            var resultAmount = GetIntOrDefault(receipt, "resultAmount");
            ValidateOptionalNullableStringField(receipt, receiptContext, issues, "guardianName");
            ValidateOptionalNullableStringField(receipt, receiptContext, issues, "reason");
            foreach (var outcomeField in AfterlifeArchiveActionState.GetConsultationOutcomeFields())
                ValidateNonNegativeIntegerField(receipt, receiptContext, issues, outcomeField, "AfterlifeArchive");
            ValidateNonNegativeIntegerField(receipt, receiptContext, issues, "resolvedAtTurn", "AfterlifeArchive");
            var resolvedAtUtc = RequireString(receipt, receiptContext, issues, "resolvedAtUtc");

            if (!string.IsNullOrWhiteSpace(resolvedAtUtc) && !DateTimeOffset.TryParse(resolvedAtUtc, out _))
            {
                issues.Add(new ValidationIssue(
                    $"{receiptContext}.resolvedAtUtc",
                    IssueSeverity.Error,
                    "afterlife archive action receipt resolvedAtUtc должен быть ISO 8601 timestamp",
                    code: "afterlife_archive_invalid_receipt_timestamp",
                    section: "AfterlifeArchive",
                    expected: "ISO 8601 timestamp",
                    actual: resolvedAtUtc));
            }

            if (!string.IsNullOrWhiteSpace(requestedMode) && !AfterlifeArchiveActionState.IsSupportedRequestedMode(requestedMode))
                continue;
            if (!string.IsNullOrWhiteSpace(status) && !AfterlifeArchiveActionState.IsSupportedResolutionStatus(status))
                continue;

            var receiptIdentityKey = AfterlifeArchiveState.TryBuildActionIdentityKey(requestId, archiveId, requestedMode);
            if (!string.IsNullOrWhiteSpace(receiptIdentityKey) &&
                !seenReceiptIdentityKeys.Add(receiptIdentityKey))
            {
                issues.Add(new ValidationIssue(
                    receiptContext,
                    IssueSeverity.Error,
                    "afterlife archive actionReceipts не должен содержать duplicate full-identity receipt",
                    code: "afterlife_archive_duplicate_receipt_identity",
                    section: "AfterlifeArchive",
                    actual: receiptIdentityKey));
            }

            if (string.Equals(status, AfterlifeArchiveActionState.ResolutionStatusAccepted, StringComparison.OrdinalIgnoreCase))
            {
                if (!string.IsNullOrWhiteSpace(archiveId) && storedArchiveIds.Contains(archiveId))
                {
                    issues.Add(new ValidationIssue(
                        $"{receiptContext}.status",
                        IssueSeverity.Error,
                        "Accepted archive receipt не должен оставлять запись в afterlifeArchive.stored",
                        code: "afterlife_archive_accepted_receipt_entry_not_consumed",
                        section: "AfterlifeArchive",
                        actual: archiveId));
                }

                if (!string.IsNullOrWhiteSpace(receiptIdentityKey) && reservedActionIdentityKeys.Contains(receiptIdentityKey))
                {
                    issues.Add(new ValidationIssue(
                        $"{receiptContext}.status",
                        IssueSeverity.Error,
                        "Accepted archive receipt не должен оставлять reservation активной",
                        code: "afterlife_archive_accepted_receipt_reservation_still_active",
                        section: "AfterlifeArchive",
                        actual: requestId));
                }

                if (string.Equals(requestedMode, AfterlifeArchiveActionState.RequestedModeConsultation, StringComparison.OrdinalIgnoreCase))
                {
                    if (TryResolveGuardianProjectTrackerValidationRoot(
                            receiptContext,
                            "Accepted archive consultation receipt требует readable current guardian project tracker authority и не использует isolated pre-turn tracker baseline как authority fallback.",
                            "afterlife_archive_missing_current_tracker_authority",
                            "AfterlifeArchive",
                            $"Исправь current {GuardianProjectState.TrackerPath} и validated tracker baseline так, чтобы validator построил guardian-backed current tracker authority перед proving completed lore_research consultation receipt.",
                            issues,
                            out var trackerRoot) &&
                        !ArchiveConsultationReceiptHasMatchingCompletedProject(trackerRoot, requestId, archiveId, guardianId, receipt))
                    {
                        issues.Add(new ValidationIssue(
                            receiptContext,
                            IssueSeverity.Error,
                            "Accepted archive consultation receipt не привёл к matching archive_consultation result",
                            code: "afterlife_archive_consultation_receipt_missing_result",
                            section: "AfterlifeArchive"));
                    }
                }

                if (string.Equals(requestedMode, AfterlifeArchiveActionState.RequestedModeConsultation, StringComparison.OrdinalIgnoreCase) &&
                    GetConsultationOutcomeTotal(receipt) <= 0)
                {
                    issues.Add(new ValidationIssue(
                        receiptContext,
                        IssueSeverity.Error,
                        "Accepted archive consultation receipt должен хранить machine-readable consultation outcome",
                        code: "afterlife_archive_consultation_receipt_missing_outcome",
                        section: "AfterlifeArchive",
                        repairHint: "Копируй в actionReceipts те же whitelist outcome fields, что были возвращены в accepted archiveActionResolutions."));
                }

                if (string.Equals(requestedMode, AfterlifeArchiveActionState.RequestedModeProjectFuel, StringComparison.OrdinalIgnoreCase) &&
                    !ArchiveProjectFuelReceiptHasMatchingJournalEntry(journalJson, requestId, guardianId, targetProjectId))
                {
                    issues.Add(new ValidationIssue(
                        receiptContext,
                        IssueSeverity.Error,
                        "Accepted archive project fuel receipt не привёл к matching assisted journal entry",
                        code: "afterlife_archive_project_fuel_receipt_missing_result",
                        section: "AfterlifeArchive"));
                }

                if (string.Equals(requestedMode, AfterlifeArchiveActionState.RequestedModeProjectFuel, StringComparison.OrdinalIgnoreCase))
                {
                    if (!AfterlifeArchiveActionState.IsSupportedProjectFuelResultMode(resultMode))
                    {
                        issues.Add(new ValidationIssue(
                            $"{receiptContext}.resultMode",
                            IssueSeverity.Error,
                            "Accepted archive project fuel receipt должен хранить canonical resultMode",
                            code: "afterlife_archive_project_fuel_receipt_invalid_result_mode",
                            section: "AfterlifeArchive",
                            expected: $"{AfterlifeArchiveActionState.ProjectFuelResultModeProjectWork} | {AfterlifeArchiveActionState.ProjectFuelResultModePressureRelief}",
                            actual: resultMode));
                    }

                    if (resultAmount <= 0)
                    {
                        issues.Add(new ValidationIssue(
                            $"{receiptContext}.resultAmount",
                            IssueSeverity.Error,
                            "Accepted archive project fuel receipt должен хранить resultAmount > 0",
                            code: "afterlife_archive_project_fuel_receipt_invalid_result_amount",
                            section: "AfterlifeArchive",
                            expected: "> 0",
                            actual: resultAmount.ToString()));
                    }
                }
            }
            else
            {
                if (!string.IsNullOrWhiteSpace(archiveId) && !storedArchiveIds.Contains(archiveId))
                {
                    issues.Add(new ValidationIssue(
                        receiptContext,
                        IssueSeverity.Error,
                        "Rejected/cancelled archive receipt должен возвращать запись в afterlifeArchive.stored",
                        code: "afterlife_archive_rejected_receipt_entry_missing",
                        section: "AfterlifeArchive",
                        actual: archiveId));
                }

                if (!string.IsNullOrWhiteSpace(receiptIdentityKey) && reservedActionIdentityKeys.Contains(receiptIdentityKey))
                {
                    issues.Add(new ValidationIssue(
                        receiptContext,
                        IssueSeverity.Error,
                        "Rejected/cancelled archive receipt должен снимать reservation с записи",
                        code: "afterlife_archive_rejected_receipt_reservation_still_active",
                        section: "AfterlifeArchive",
                        actual: requestId));
                }
            }
        }
    }


    private static bool ArchiveConsultationReceiptHasMatchingCompletedProject(
        JsonElement trackerRoot,
        string? requestId,
        string? archiveId,
        string? guardianId,
        JsonElement receipt)
    {
        if (!trackerRoot.TryGetProperty("completedProjects", out var completedProjects) || completedProjects.ValueKind != JsonValueKind.Array)
            return false;

        foreach (var entry in completedProjects.EnumerateArray())
        {
            if (!string.Equals(GetFirstNonEmptyString(entry, "guardianId"), guardianId, StringComparison.OrdinalIgnoreCase) ||
                !entry.TryGetProperty("project", out var project) ||
                project.ValueKind != JsonValueKind.Object ||
                !string.Equals(GetFirstNonEmptyString(project, "projectOrigin"), "archive_consultation", StringComparison.OrdinalIgnoreCase) ||
                !ConsultationProjectMatchesRequest(project, requestId, archiveId))
            {
                continue;
            }

            if (!project.TryGetProperty("projectOutcomeAudit", out var audit) || audit.ValueKind != JsonValueKind.Object)
                return true;

            return ConsultationOutcomeMatchesAudit(receipt, audit);
        }

        return false;
    }

    private static bool ArchiveConsultationReceiptHasMatchingCompletedProject(
        JsonElement trackerRoot,
        string? requestId,
        string? archiveId,
        string? guardianId,
        JsonObject receipt)
    {
        if (!trackerRoot.TryGetProperty("completedProjects", out var completedProjects) || completedProjects.ValueKind != JsonValueKind.Array)
            return false;

        foreach (var entry in completedProjects.EnumerateArray())
        {
            if (!string.Equals(GetFirstNonEmptyString(entry, "guardianId"), guardianId, StringComparison.OrdinalIgnoreCase) ||
                !entry.TryGetProperty("project", out var project) ||
                project.ValueKind != JsonValueKind.Object ||
                !string.Equals(GetFirstNonEmptyString(project, "projectOrigin"), "archive_consultation", StringComparison.OrdinalIgnoreCase) ||
                !ConsultationProjectMatchesRequest(project, requestId, archiveId))
            {
                continue;
            }

            if (!project.TryGetProperty("projectOutcomeAudit", out var audit) || audit.ValueKind != JsonValueKind.Object)
                return true;

            return ConsultationOutcomeMatchesAudit(receipt, audit);
        }

        return false;
    }

    private static bool ArchiveConsultationReceiptHasMatchingCompletedProject(
        string? trackerJson,
        string? requestId,
        string? archiveId,
        string? guardianId,
        JsonElement receipt)
    {
        if (string.IsNullOrWhiteSpace(trackerJson))
            return false;

        try
        {
            using var doc = JsonDocument.Parse(trackerJson);
            if (!doc.RootElement.TryGetProperty("completedProjects", out var completedProjects) || completedProjects.ValueKind != JsonValueKind.Array)
                return false;

            foreach (var entry in completedProjects.EnumerateArray())
            {
                if (!string.Equals(GetFirstNonEmptyString(entry, "guardianId"), guardianId, StringComparison.OrdinalIgnoreCase) ||
                    !entry.TryGetProperty("project", out var project) ||
                    project.ValueKind != JsonValueKind.Object ||
                    !string.Equals(GetFirstNonEmptyString(project, "projectOrigin"), "archive_consultation", StringComparison.OrdinalIgnoreCase) ||
                    !ConsultationProjectMatchesRequest(project, requestId, archiveId))
                {
                    continue;
                }

                if (!project.TryGetProperty("projectOutcomeAudit", out var audit) || audit.ValueKind != JsonValueKind.Object)
                    return true;

                return ConsultationOutcomeMatchesAudit(receipt, audit);
            }

            return false;
        }
        catch
        {
            return false;
        }
    }


    private static bool ArchiveConsultationReceiptHasMatchingCompletedProject(
        string? trackerJson,
        string? requestId,
        string? archiveId,
        string? guardianId,
        JsonObject receipt)
    {
        if (string.IsNullOrWhiteSpace(trackerJson))
            return false;

        try
        {
            using var doc = JsonDocument.Parse(trackerJson);
            if (!doc.RootElement.TryGetProperty("completedProjects", out var completedProjects) || completedProjects.ValueKind != JsonValueKind.Array)
                return false;

            foreach (var entry in completedProjects.EnumerateArray())
            {
                if (!string.Equals(GetFirstNonEmptyString(entry, "guardianId"), guardianId, StringComparison.OrdinalIgnoreCase) ||
                    !entry.TryGetProperty("project", out var project) ||
                    project.ValueKind != JsonValueKind.Object ||
                    !string.Equals(GetFirstNonEmptyString(project, "projectOrigin"), "archive_consultation", StringComparison.OrdinalIgnoreCase) ||
                    !ConsultationProjectMatchesRequest(project, requestId, archiveId))
                {
                    continue;
                }

                if (!project.TryGetProperty("projectOutcomeAudit", out var audit) || audit.ValueKind != JsonValueKind.Object)
                    return true;

                return ConsultationOutcomeMatchesAudit(receipt, audit);
            }

            return false;
        }
        catch
        {
            return false;
        }
    }


    private static int GetConsultationOutcomeTotal(JsonElement root)
    {
        var total = 0;
        foreach (var outcomeField in AfterlifeArchiveActionState.GetConsultationOutcomeFields())
            total += Math.Max(0, GetIntOrDefault(root, outcomeField));
        return total;
    }


    private static int GetConsultationOutcomeTotal(JsonObject root)
    {
        var total = 0;
        foreach (var outcomeField in AfterlifeArchiveActionState.GetConsultationOutcomeFields())
            total += Math.Max(0, GetNodeInt(root[outcomeField]));
        return total;
    }


    private static bool ConsultationOutcomeMatchesAudit(JsonElement receipt, JsonElement audit)
    {
        foreach (var outcomeField in AfterlifeArchiveActionState.GetConsultationOutcomeFields())
        {
            if (GetIntOrDefault(receipt, outcomeField) != GetIntOrDefault(audit, outcomeField))
                return false;
        }

        return true;
    }


    private static bool ConsultationOutcomeMatchesAudit(JsonObject receipt, JsonElement audit)
    {
        foreach (var outcomeField in AfterlifeArchiveActionState.GetConsultationOutcomeFields())
        {
            if (GetNodeInt(receipt[outcomeField]) != GetIntOrDefault(audit, outcomeField))
                return false;
        }

        return true;
    }


    private static bool ArchiveProjectFuelReceiptHasMatchingJournalEntry(
        string? journalJson,
        string? requestId,
        string? guardianId,
        string? targetProjectId)
    {
        if (string.IsNullOrWhiteSpace(journalJson))
            return false;

        try
        {
            using var doc = JsonDocument.Parse(journalJson);
            if (!doc.RootElement.TryGetProperty("entries", out var entries) || entries.ValueKind != JsonValueKind.Array)
                return false;

            return entries.EnumerateArray().Any(entry =>
                string.Equals(GetFirstNonEmptyString(entry, "guardianId"), guardianId, StringComparison.OrdinalIgnoreCase) &&
                string.Equals(GetFirstNonEmptyString(entry, "projectId"), targetProjectId, StringComparison.OrdinalIgnoreCase) &&
                string.Equals(GetFirstNonEmptyString(entry, "eventType"), "assisted", StringComparison.OrdinalIgnoreCase) &&
                string.Equals(GetFirstNonEmptyString(entry, "archiveFuelRequestId"), requestId, StringComparison.OrdinalIgnoreCase));
        }
        catch
        {
            return false;
        }
    }

    private static bool ConsultationProjectMatchesRequest(
        JsonElement project,
        string? requestId,
        string? archiveId)
    {
        if (!string.Equals(GetFirstNonEmptyString(project, "consultationRequestId"), requestId, StringComparison.OrdinalIgnoreCase))
            return false;

        var projectArchiveId = GetFirstNonEmptyString(project, "consultationArchiveId");
        return string.IsNullOrWhiteSpace(projectArchiveId) ||
               string.Equals(projectArchiveId, archiveId, StringComparison.OrdinalIgnoreCase);
    }


    private static bool ArchiveEntryIsAvailableAfterRejectedResolution(
        JsonArray stored,
        string archiveId,
        string requestId,
        string requestedMode)
    {
        var entry = AfterlifeArchiveState.FindEntry(stored, archiveId);
        if (entry == null)
            return false;

        return !AfterlifeArchiveState.ReservationMatchesRequest(
            AfterlifeArchiveState.GetReservationObject(entry),
            requestId,
            requestedMode);
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


    private void ValidatePendingMemoryLegacy(JsonElement root, string contextPrefix, List<ValidationIssue> issues)
    {
        if (!root.TryGetProperty("pendingMemoryLegacy", out var pendingLegacy))
            return;

        var context = $"{contextPrefix}.pendingMemoryLegacy";
        if (pendingLegacy.ValueKind == JsonValueKind.Null)
            return;
        if (!RequireObject(pendingLegacy, context, issues))
            return;

        var missingPendingLegacyFields = GetMissingRequiredNonEmptyStringProperties(
            pendingLegacy,
            "legacyId",
            "legacyType",
            "sourceLifeHint",
            "grantSource",
            "applicationState",
            "grantedAtUtc");
        if (!pendingLegacy.TryGetProperty("grantSnapshot", out var requiredGrantSnapshot) || requiredGrantSnapshot.ValueKind != JsonValueKind.Object)
            missingPendingLegacyFields.Add("grantSnapshot");
        if (missingPendingLegacyFields.Count > 0)
        {
            issues.Add(new ValidationIssue(
                context,
                IssueSeverity.Error,
                "pendingMemoryLegacy не содержит обязательные корневые поля",
                code: "pending_memory_legacy_missing_required_fields",
                section: "MemoryLegacy",
                expected: "Non-empty legacyId, legacyType, sourceLifeHint, grantSource, applicationState, grantedAtUtc, and grantSnapshot object",
                actual: string.Join(", ", missingPendingLegacyFields),
                repairHint: "Сначала собери полный корневой canonical pendingMemoryLegacy contract: legacyId, legacyType, sourceLifeHint, grantSource, applicationState, grantedAtUtc и grantSnapshot обязательны ещё до type-specific полей."));
            return;
        }

        var grantSource = GetFirstNonEmptyString(pendingLegacy, "grantSource") ?? string.Empty;
        var applicationState = GetFirstNonEmptyString(pendingLegacy, "applicationState") ?? string.Empty;

        var legacyType = pendingLegacy.TryGetProperty("legacyType", out var typeEl) && typeEl.ValueKind == JsonValueKind.String
            ? typeEl.GetString() ?? string.Empty
            : string.Empty;

        if (!string.IsNullOrWhiteSpace(grantSource) &&
            !string.Equals(grantSource, "memoryLegacyGrant", StringComparison.Ordinal))
        {
            issues.Add(new ValidationIssue(
                $"{context}.grantSource",
                IssueSeverity.Error,
                "grantSource в pendingMemoryLegacy должен быть 'memoryLegacyGrant'",
                code: "pending_memory_legacy_invalid_grant_source",
                section: "MemoryLegacy",
                expected: "memoryLegacyGrant",
                actual: grantSource,
                repairHint: "Не записывай pendingMemoryLegacy как независимый итог. Сохрани его как canonical projection от structured metaStateUpdates.memoryLegacyGrant."));
        }

        if (!string.Equals(applicationState, "pending", StringComparison.OrdinalIgnoreCase) &&
            !string.Equals(applicationState, "applied-awaiting-turn-accept", StringComparison.OrdinalIgnoreCase))
        {
            issues.Add(new ValidationIssue(
                $"{context}.applicationState",
                IssueSeverity.Error,
                "applicationState в pendingMemoryLegacy должен быть 'pending' или 'applied-awaiting-turn-accept'",
                code: "pending_memory_legacy_invalid_application_state",
                section: "MemoryLegacy",
                expected: "pending | applied-awaiting-turn-accept",
                actual: string.IsNullOrWhiteSpace(applicationState) ? "missing" : applicationState,
                repairHint: "Используй canonical applicationState для pendingMemoryLegacy: до локального применения legacy это pending, после локального применения перед accepted incarnation turn это applied-awaiting-turn-accept."));
        }

        if (!pendingLegacy.TryGetProperty("grantSnapshot", out var grantSnapshot))
        {
            issues.Add(new ValidationIssue(
                $"{context}.grantSnapshot",
                IssueSeverity.Error,
                "pendingMemoryLegacy должен содержать обязательный объект grantSnapshot",
                code: "pending_memory_legacy_missing_grant_snapshot",
                section: "MemoryLegacy",
                expected: "grantSnapshot object",
                actual: "missing",
                repairHint: "Canonical pendingMemoryLegacy должен сохранять structured grantSnapshot рядом с итоговыми полями legacy, чтобы клиент мог сверить semantic consistency."));
        }
        else if (!RequireObject(grantSnapshot, $"{context}.grantSnapshot", issues))
        {
            return;
        }
        else
        {
            ValidateMemoryLegacyGrantObject(grantSnapshot, $"{context}.grantSnapshot", issues);
            ValidatePendingMemoryLegacyMatchesGrantSnapshot(pendingLegacy, grantSnapshot, context, issues);
        }

        if (string.Equals(applicationState, "applied-awaiting-turn-accept", StringComparison.OrdinalIgnoreCase))
        {
            if (!pendingLegacy.TryGetProperty("applicationAudit", out var audit) || !RequireObject(audit, $"{context}.applicationAudit", issues))
                return;
        }
        else if (pendingLegacy.TryGetProperty("applicationAudit", out var pendingAudit) && pendingAudit.ValueKind != JsonValueKind.Null)
        {
            issues.Add(new ValidationIssue(
                $"{context}.applicationAudit",
                IssueSeverity.Error,
                "applicationAudit допустим только при applicationState = applied-awaiting-turn-accept",
                code: "pending_memory_legacy_application_audit_in_wrong_state",
                section: "MemoryLegacy",
                repairHint: "Убери applicationAudit из pending legacy до локального применения награды. Это поле допустимо только в applied-awaiting-turn-accept."));
        }

        if (string.Equals(legacyType, "startingCharacteristicBonus", StringComparison.OrdinalIgnoreCase))
        {
            var missingCharacteristicLegacyFields = GetMissingRequiredNonEmptyStringProperties(pendingLegacy, "characteristic");
            if (missingCharacteristicLegacyFields.Count > 0 || !pendingLegacy.TryGetProperty("bonus", out _))
            {
                var actualMissing = new List<string>(missingCharacteristicLegacyFields);
                if (!pendingLegacy.TryGetProperty("bonus", out _))
                    actualMissing.Add("bonus");

                issues.Add(new ValidationIssue(
                    context,
                    IssueSeverity.Error,
                    "startingCharacteristicBonus pendingMemoryLegacy не содержит type-specific обязательные поля",
                    code: "pending_memory_legacy_characteristic_fields_missing",
                    section: "MemoryLegacy",
                    expected: "characteristic and bonus for startingCharacteristicBonus",
                    actual: string.Join(", ", actualMissing),
                    repairHint: "Для legacyType=startingCharacteristicBonus сохрани characteristic и bonus=2 в canonical pendingMemoryLegacy."));
                return;
            }

            var characteristic = GetFirstNonEmptyString(pendingLegacy, "characteristic") ?? string.Empty;
        ValidatePositiveIntegerField(pendingLegacy, context, issues, "bonus");

            if (!string.IsNullOrWhiteSpace(characteristic) &&
                !Characteristics.All.Contains(characteristic, StringComparer.OrdinalIgnoreCase))
            {
                issues.Add(new ValidationIssue(
                    $"{context}.characteristic",
                    IssueSeverity.Error,
                    "characteristic в pendingMemoryLegacy должен быть допустимым именем характеристики",
                    code: "pending_memory_legacy_invalid_characteristic",
                    section: "MemoryLegacy",
                    expected: "valid characteristic name",
                    actual: characteristic,
                    repairHint: "Используй canonical имя характеристики из rules/spec для startingCharacteristicBonus legacy."));
            }

            if (pendingLegacy.TryGetProperty("bonus", out var bonusEl) &&
                bonusEl.ValueKind == JsonValueKind.Number &&
                bonusEl.TryGetInt32(out var bonus) &&
                bonus != 2)
            {
                issues.Add(new ValidationIssue(
                    $"{context}.bonus",
                    IssueSeverity.Error,
                    "Для startingCharacteristicBonus bonus должен быть ровно 2",
                    code: "pending_memory_legacy_invalid_characteristic_bonus",
                    section: "MemoryLegacy",
                    expected: "2",
                    actual: bonus.ToString(),
                    repairHint: "Memory Legacy типа startingCharacteristicBonus всегда даёт ровно +2 к одной характеристике."));
            }
        }
        else if (string.Equals(legacyType, "startingPassiveKnowledgeSkill", StringComparison.OrdinalIgnoreCase))
        {
            var missingSkillLegacyFields = GetMissingRequiredNonEmptyStringProperties(
                pendingLegacy,
                "skillName",
                "skillDescription",
                "group",
                "playerStatBonus");
            if (missingSkillLegacyFields.Count > 0 || !pendingLegacy.TryGetProperty("structuredBonuses", out _))
            {
                var actualMissing = new List<string>(missingSkillLegacyFields);
                if (!pendingLegacy.TryGetProperty("structuredBonuses", out _))
                    actualMissing.Add("structuredBonuses");

                issues.Add(new ValidationIssue(
                    context,
                    IssueSeverity.Error,
                    "startingPassiveKnowledgeSkill pendingMemoryLegacy не содержит type-specific обязательные поля",
                    code: "pending_memory_legacy_skill_fields_missing",
                    section: "MemoryLegacy",
                    expected: "skillName, skillDescription, group, playerStatBonus, structuredBonuses",
                    actual: string.Join(", ", actualMissing),
                    repairHint: "Для legacyType=startingPassiveKnowledgeSkill сохрани canonical skill payload и непустой structuredBonuses в pendingMemoryLegacy."));
                return;
            }

            ValidateOptionalString(pendingLegacy, context, issues, "rarity");
            ValidateOptionalString(pendingLegacy, context, issues, "type");
            ValidatePositiveIntegerField(pendingLegacy, context, issues, "masteryLevel");
            ValidatePositiveIntegerField(pendingLegacy, context, issues, "maxMasteryLevel");

            if (pendingLegacy.TryGetProperty("group", out var groupEl) &&
                groupEl.ValueKind == JsonValueKind.String &&
                !string.Equals(groupEl.GetString(), "Knowledge", StringComparison.OrdinalIgnoreCase))
            {
                issues.Add(new ValidationIssue(
                    $"{context}.group",
                    IssueSeverity.Error,
                    "startingPassiveKnowledgeSkill должен иметь group = 'Knowledge'",
                    code: "pending_memory_legacy_invalid_skill_group",
                    section: "MemoryLegacy",
                    expected: "Knowledge",
                    actual: groupEl.GetString() ?? string.Empty,
                    repairHint: "Passive-skill Memory Legacy должен использовать canonical group=Knowledge, как это требует contract для startingPassiveKnowledgeSkill."));
            }

            if (!pendingLegacy.TryGetProperty("structuredBonuses", out var bonuses))
            {
                issues.Add(new ValidationIssue(
                    $"{context}.structuredBonuses",
                    IssueSeverity.Error,
                    "startingPassiveKnowledgeSkill должен содержать structuredBonuses",
                    code: "pending_memory_legacy_missing_structured_bonuses",
                    section: "MemoryLegacy",
                    expected: "non-empty structuredBonuses array",
                    actual: "missing",
                    repairHint: "Passive-skill Memory Legacy должен хранить canonical structuredBonuses рядом с skillName/skillDescription/group/playerStatBonus."));
            }
            else
            {
                RequireArrayOfObjects(bonuses, $"{context}.structuredBonuses", issues);
                if (bonuses.ValueKind == JsonValueKind.Array && bonuses.GetArrayLength() == 0)
                {
                    issues.Add(new ValidationIssue(
                        $"{context}.structuredBonuses",
                        IssueSeverity.Error,
                        "structuredBonuses для startingPassiveKnowledgeSkill не должен быть пустым",
                        code: "pending_memory_legacy_empty_structured_bonuses",
                        section: "MemoryLegacy",
                        expected: "non-empty structuredBonuses array",
                        actual: "empty array",
                        repairHint: "Не оставляй passive-skill Memory Legacy без механических бонусов. Сохрани непустой canonical structuredBonuses array."));
                }
            }

            if (string.Equals(applicationState, "applied-awaiting-turn-accept", StringComparison.OrdinalIgnoreCase) &&
                pendingLegacy.TryGetProperty("applicationAudit", out var skillAudit) &&
                skillAudit.ValueKind == JsonValueKind.Object)
            {
                RequireString(skillAudit, $"{context}.applicationAudit", issues, "expectedPassiveSkillName");
                RequireString(skillAudit, $"{context}.applicationAudit", issues, "expectedGroup");
                RequireString(skillAudit, $"{context}.applicationAudit", issues, "expectedPlayerStatBonus");
                ValidatePositiveNumberField(skillAudit, $"{context}.applicationAudit", issues, "expectedStructuredBonusesCount");
                RequireString(skillAudit, $"{context}.applicationAudit", issues, "expectedStructuredBonusesCanonical");
            }
        }
        else if (!string.IsNullOrWhiteSpace(legacyType))
        {
            issues.Add(new ValidationIssue(
                $"{context}.legacyType",
                IssueSeverity.Error,
                "Неподдерживаемый legacyType в pendingMemoryLegacy",
                code: "pending_memory_legacy_unsupported_legacy_type",
                section: "MemoryLegacy",
                expected: "startingCharacteristicBonus | startingPassiveKnowledgeSkill",
                actual: string.IsNullOrWhiteSpace(legacyType) ? "missing" : legacyType,
                repairHint: "Используй только canonical legacyType для Memory Gates: startingCharacteristicBonus или startingPassiveKnowledgeSkill."));
        }
    }


    private void ValidatePendingMemoryLegacyMatchesGrantSnapshot(JsonElement pendingLegacy, JsonElement grantSnapshot, string context, List<ValidationIssue> issues)
    {
        foreach (var fieldName in new[] { "legacyId", "legacyType", "sourceLifeHint" })
        {
            var pendingValue = GetFirstNonEmptyString(pendingLegacy, fieldName) ?? string.Empty;
            var snapshotValue = GetFirstNonEmptyString(grantSnapshot, fieldName) ?? string.Empty;
            if (!string.IsNullOrWhiteSpace(snapshotValue) &&
                !string.Equals(pendingValue, snapshotValue, StringComparison.Ordinal))
            {
                issues.Add(new ValidationIssue(
                    $"{context}.{fieldName}",
                    IssueSeverity.Error,
                    $"{fieldName} в pendingMemoryLegacy должен совпадать с grantSnapshot",
                    code: "pending_memory_legacy_grant_snapshot_mismatch",
                    section: "MemoryLegacy",
                    expected: snapshotValue,
                    actual: string.IsNullOrWhiteSpace(pendingValue) ? "missing" : pendingValue,
                    repairHint: "Синхронизируй canonical pendingMemoryLegacy с данными structured memoryLegacyGrant/grantSnapshot."));
            }
        }

        var legacyType = GetFirstNonEmptyString(pendingLegacy, "legacyType") ?? string.Empty;
        if (string.Equals(legacyType, "startingCharacteristicBonus", StringComparison.OrdinalIgnoreCase))
        {
            var pendingCharacteristic = GetFirstNonEmptyString(pendingLegacy, "characteristic") ?? string.Empty;
            var snapshotCharacteristic = GetFirstNonEmptyString(grantSnapshot, "characteristic") ?? string.Empty;
            if (!string.IsNullOrWhiteSpace(snapshotCharacteristic) &&
                !string.Equals(pendingCharacteristic, snapshotCharacteristic, StringComparison.Ordinal))
            {
                issues.Add(new ValidationIssue(
                    $"{context}.characteristic",
                    IssueSeverity.Error,
                    "characteristic в pendingMemoryLegacy должен совпадать с grantSnapshot",
                    code: "pending_memory_legacy_grant_snapshot_characteristic_mismatch",
                    section: "MemoryLegacy",
                    expected: snapshotCharacteristic,
                    actual: string.IsNullOrWhiteSpace(pendingCharacteristic) ? "missing" : pendingCharacteristic));
            }

            if (TryReadInt(grantSnapshot, "bonus", out var snapshotBonus) &&
                TryReadInt(pendingLegacy, "bonus", out var pendingBonus) &&
                pendingBonus != snapshotBonus)
            {
                issues.Add(new ValidationIssue(
                    $"{context}.bonus",
                    IssueSeverity.Error,
                    "bonus в pendingMemoryLegacy должен совпадать с grantSnapshot",
                    code: "pending_memory_legacy_grant_snapshot_bonus_mismatch",
                    section: "MemoryLegacy",
                    expected: snapshotBonus.ToString(),
                    actual: pendingBonus.ToString()));
            }
        }
        else if (string.Equals(legacyType, "startingPassiveKnowledgeSkill", StringComparison.OrdinalIgnoreCase))
        {
            foreach (var fieldName in new[] { "skillName", "skillDescription", "group" })
            {
                var pendingValue = GetFirstNonEmptyString(pendingLegacy, fieldName) ?? string.Empty;
                var snapshotValue = GetFirstNonEmptyString(grantSnapshot, fieldName) ?? string.Empty;
                if (!string.IsNullOrWhiteSpace(snapshotValue) &&
                    !string.Equals(pendingValue, snapshotValue, StringComparison.Ordinal))
                {
                    issues.Add(new ValidationIssue(
                        $"{context}.{fieldName}",
                        IssueSeverity.Error,
                        $"{fieldName} в pendingMemoryLegacy должен совпадать с grantSnapshot",
                        code: "pending_memory_legacy_grant_snapshot_skill_field_mismatch",
                        section: "MemoryLegacy",
                        expected: snapshotValue,
                        actual: string.IsNullOrWhiteSpace(pendingValue) ? "missing" : pendingValue));
                }
            }

            var pendingPlayerStatBonus = GetFirstNonEmptyString(pendingLegacy, "playerStatBonus") ?? string.Empty;
            var snapshotPlayerStatBonus = GetFirstNonEmptyString(grantSnapshot, "playerStatBonus") ?? string.Empty;
            if (!string.IsNullOrWhiteSpace(snapshotPlayerStatBonus) &&
                string.IsNullOrWhiteSpace(pendingPlayerStatBonus))
            {
                issues.Add(new ValidationIssue(
                    $"{context}.playerStatBonus",
                    IssueSeverity.Error,
                    "playerStatBonus в pendingMemoryLegacy не должен исчезать относительно grantSnapshot",
                    code: "pending_memory_legacy_grant_snapshot_player_stat_bonus_missing",
                    section: "MemoryLegacy",
                    expected: "non-empty playerStatBonus summary",
                    actual: "missing or empty",
                    repairHint: "Сохрани в pendingMemoryLegacy непустой playerStatBonus summary и не убирай это поле относительно grantSnapshot."));
            }

            if (pendingLegacy.TryGetProperty("structuredBonuses", out var pendingStructuredBonuses) &&
                grantSnapshot.TryGetProperty("structuredBonuses", out var snapshotStructuredBonuses) &&
                pendingStructuredBonuses.ValueKind == JsonValueKind.Array &&
                snapshotStructuredBonuses.ValueKind == JsonValueKind.Array)
            {
                var pendingCanonical = StructuredBonusCanonicalizer.Canonicalize(pendingStructuredBonuses);
                var snapshotCanonical = StructuredBonusCanonicalizer.Canonicalize(snapshotStructuredBonuses);
                if (!string.Equals(pendingCanonical, snapshotCanonical, StringComparison.Ordinal))
                {
                    issues.Add(new ValidationIssue(
                        $"{context}.structuredBonuses",
                        IssueSeverity.Error,
                        "structuredBonuses в pendingMemoryLegacy должны совпадать с grantSnapshot",
                        code: "pending_memory_legacy_grant_snapshot_structured_bonuses_mismatch",
                        section: "MemoryLegacy",
                        repairHint: "Не меняй structuredBonuses между structured grantSnapshot и canonical pendingMemoryLegacy."));
                }
            }
        }
    }

    private void ValidatePendingShiningBlessingEffects(JsonElement root, string contextPrefix, List<ValidationIssue> issues)
    {
        if (!root.TryGetProperty(ShiningBlessingEffectState.SoulStateProperty, out var blessingEffects))
            return;

        var context = $"{contextPrefix}.{ShiningBlessingEffectState.SoulStateProperty}";
        if (blessingEffects.ValueKind == JsonValueKind.Null)
            return;
        if (!RequireObject(blessingEffects, context, issues))
            return;

        RequireString(blessingEffects, context, issues, "applicationState");
        var materializedAtUtc = RequireString(blessingEffects, context, issues, "materializedAtUtc");
        ValidateNonNegativeIntegerField(blessingEffects, context, issues, "sourcePackagePreparedAtTurn", "ShiningBlessings");
        ValidateNonNegativeIntegerField(blessingEffects, context, issues, "currentIncarnation", "ShiningBlessings");
        ValidateNonNegativeIntegerField(blessingEffects, context, issues, "sourceCardCount", "ShiningBlessings");
        if (!TryGetArray(blessingEffects, "sourceCardIds", $"{context}.sourceCardIds", issues, out var sourceCardIds))
            return;
        RequireArrayOfStrings(sourceCardIds, $"{context}.sourceCardIds", issues);
        if (!string.IsNullOrWhiteSpace(materializedAtUtc) && !DateTimeOffset.TryParse(materializedAtUtc, out _))
        {
            issues.Add(new ValidationIssue(
                $"{context}.materializedAtUtc",
                IssueSeverity.Error,
                "pendingShiningBlessingEffects.materializedAtUtc должен быть ISO 8601 timestamp",
                code: "pending_shining_blessings_invalid_materialized_at_utc",
                section: "ShiningBlessings",
                expected: "ISO 8601 timestamp",
                actual: materializedAtUtc));
        }

        var sourceCardIdSet = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        if (sourceCardIds.ValueKind == JsonValueKind.Array)
        {
            if (sourceCardIds.GetArrayLength() == 0)
            {
                issues.Add(new ValidationIssue(
                    $"{context}.sourceCardIds",
                    IssueSeverity.Error,
                    "pendingShiningBlessingEffects должен ссылаться хотя бы на одну source card",
                    code: "pending_shining_blessings_empty_source_card_ids",
                    section: "ShiningBlessings",
                    expected: "non-empty sourceCardIds array",
                    actual: "empty array"));
            }

            var index = 0;
            foreach (var sourceCardIdEl in sourceCardIds.EnumerateArray())
            {
                var sourceCardId = sourceCardIdEl.ValueKind == JsonValueKind.String ? sourceCardIdEl.GetString() ?? string.Empty : string.Empty;
                if (string.IsNullOrWhiteSpace(sourceCardId))
                {
                    index++;
                    continue;
                }

                if (!sourceCardIdSet.Add(sourceCardId))
                {
                    issues.Add(new ValidationIssue(
                        $"{context}.sourceCardIds[{index}]",
                        IssueSeverity.Error,
                        "pendingShiningBlessingEffects.sourceCardIds не должен содержать дубликаты",
                        code: "pending_shining_blessings_duplicate_source_card_id",
                        section: "ShiningBlessings",
                        actual: sourceCardId));
                }

                index++;
            }

            var sourceCardCount = GetIntOrDefault(blessingEffects, "sourceCardCount");
            if (sourceCardCount != sourceCardIds.GetArrayLength())
            {
                issues.Add(new ValidationIssue(
                    $"{context}.sourceCardCount",
                    IssueSeverity.Error,
                    "sourceCardCount должен совпадать с количеством sourceCardIds",
                    code: "pending_shining_blessings_source_card_count_mismatch",
                    section: "ShiningBlessings",
                    expected: sourceCardIds.GetArrayLength().ToString(),
                    actual: sourceCardCount.ToString()));
            }
        }

        var applicationState = GetFirstNonEmptyString(blessingEffects, "applicationState") ?? string.Empty;
        if (!string.Equals(applicationState, ShiningBlessingEffectState.ApplicationStateActive, StringComparison.OrdinalIgnoreCase))
        {
            issues.Add(new ValidationIssue(
                $"{context}.applicationState",
                IssueSeverity.Error,
                "pendingShiningBlessingEffects.applicationState должен быть active",
                code: "pending_shining_blessings_invalid_application_state",
                section: "ShiningBlessings",
                expected: ShiningBlessingEffectState.ApplicationStateActive,
                actual: string.IsNullOrWhiteSpace(applicationState) ? "missing" : applicationState,
                repairHint: "Сохраняй active canonical blessing state до тех пор, пока остаются хотя бы audit или pending effect blocks."));
        }

        if (blessingEffects.TryGetProperty("memorySelection", out var memorySelection) && memorySelection.ValueKind != JsonValueKind.Null)
            ValidateShiningBlessingMemorySelection(memorySelection, $"{context}.memorySelection", issues, sourceCardIdSet);
        if (blessingEffects.TryGetProperty("resourceGrant", out var resourceGrant) && resourceGrant.ValueKind != JsonValueKind.Null)
            ValidateShiningBlessingResourceGrant(resourceGrant, $"{context}.resourceGrant", issues, sourceCardIdSet);
        if (blessingEffects.TryGetProperty("relicRefinementEntitlements", out var relicEntitlements) && relicEntitlements.ValueKind != JsonValueKind.Null)
            ValidateShiningBlessingRelicEntitlements(relicEntitlements, $"{context}.relicRefinementEntitlements", issues, sourceCardIdSet);

        ValidateShiningBlessingEffectArray(
            blessingEffects,
            "pendingSocialEffects",
            $"{context}.pendingSocialEffects",
            issues,
            sourceCardIdSet,
            new[] { "effectId", "sourceCardId", "delta", "status" },
            ShiningBlessingEffectState.SocialStatusPendingFirstRelationCommit,
            allowExpiredStatus: false);
        ValidateShiningBlessingEffectArray(
            blessingEffects,
            "pendingRouteEffects",
            $"{context}.pendingRouteEffects",
            issues,
            sourceCardIdSet,
            new[] { "effectId", "sourceCardId", "routeOptions", "latestTurn", "status" },
            ShiningBlessingEffectState.RouteStatusPendingEarlyRouteSeed,
            allowExpiredStatus: true);
        ValidateShiningBlessingEffectArray(
            blessingEffects,
            "pendingLoreEffects",
            $"{context}.pendingLoreEffects",
            issues,
            sourceCardIdSet,
            new[] { "effectId", "sourceCardId", "clueCount", "latestTurn", "status" },
            ShiningBlessingEffectState.LoreStatusPendingLoreInsertion,
            allowExpiredStatus: true);
        ValidateShiningBlessingEffectArray(
            blessingEffects,
            "pendingSurvivalEffects",
            $"{context}.pendingSurvivalEffects",
            issues,
            sourceCardIdSet,
            new[] { "effectId", "sourceCardId", "downgrade", "recovery", "status" },
            ShiningBlessingEffectState.SurvivalStatusPendingFirstRuinousFailure,
            allowExpiredStatus: false);
        ValidateShiningBlessingEffectArray(
            blessingEffects,
            "pendingDescentEffects",
            $"{context}.pendingDescentEffects",
            issues,
            sourceCardIdSet,
            new[] { "effectId", "sourceCardId", "sourceActorId", "latestTurn", "quality", "status" },
            ShiningBlessingEffectState.DescentStatusPendingResidentDescent,
            allowExpiredStatus: true);
    }

    private void ValidateShiningBlessingMemorySelection(JsonElement value, string context, List<ValidationIssue> issues, IReadOnlySet<string> allowedSourceCardIds)
    {
        if (!RequireObject(value, context, issues))
            return;

        ValidateNonNegativeIntegerField(value, context, issues, "options", "ShiningBlessings");
        ValidateNonNegativeIntegerField(value, context, issues, "rerolls", "ShiningBlessings");
        RequireString(value, context, issues, "status");
        if (TryGetArray(value, "sourceCardIds", $"{context}.sourceCardIds", issues, out var sourceCardIds))
        {
            RequireArrayOfStrings(sourceCardIds, $"{context}.sourceCardIds", issues);
            ValidateSourceCardSubset(sourceCardIds, $"{context}.sourceCardIds", issues, allowedSourceCardIds);
        }

        var status = GetFirstNonEmptyString(value, "status") ?? string.Empty;
        if (!string.Equals(status, ShiningBlessingEffectState.MemoryStatusPendingPreTurnOneSelection, StringComparison.OrdinalIgnoreCase) &&
            !string.Equals(status, ShiningBlessingEffectState.GenericStatusConsumed, StringComparison.OrdinalIgnoreCase))
        {
            issues.Add(new ValidationIssue(
                $"{context}.status",
                IssueSeverity.Error,
                "memorySelection.status должен быть pending_pre_turn_one_selection или consumed",
                code: "pending_shining_blessings_invalid_memory_status",
                section: "ShiningBlessings",
                expected: $"{ShiningBlessingEffectState.MemoryStatusPendingPreTurnOneSelection} | {ShiningBlessingEffectState.GenericStatusConsumed}",
                actual: string.IsNullOrWhiteSpace(status) ? "missing" : status));
        }

        if (string.Equals(status, ShiningBlessingEffectState.GenericStatusConsumed, StringComparison.OrdinalIgnoreCase))
        {
            ValidateNonNegativeIntegerField(value, context, issues, "consumedAtTurn", "ShiningBlessings");
            RequireString(value, context, issues, "consumedAtUtc");
            ValidateNonNegativeIntegerField(value, context, issues, "rerollsSpent", "ShiningBlessings");

            var rerollsSpent = GetIntOrDefault(value, "rerollsSpent");
            var rerollsGranted = GetIntOrDefault(value, "rerolls");
            if (rerollsSpent > rerollsGranted)
            {
                issues.Add(new ValidationIssue(
                    $"{context}.rerollsSpent",
                    IssueSeverity.Error,
                    "memorySelection.rerollsSpent не может превышать выданные rerolls",
                    code: "pending_shining_blessings_memory_rerolls_spent_exceeds_grant",
                    section: "ShiningBlessings",
                    expected: $"<= {rerollsGranted}",
                    actual: rerollsSpent.ToString()));
            }

            var hasSelectedIncarnation = TryReadInt(value, "selectedLifeIncarnation", out _);
            var hasSelectedHint = !string.IsNullOrWhiteSpace(GetFirstNonEmptyString(value, "selectedLifeHint"));
            var hasSelectedSummary = !string.IsNullOrWhiteSpace(GetFirstNonEmptyString(value, "selectedLifeSummary"));
            if ((hasSelectedIncarnation || hasSelectedHint || hasSelectedSummary) &&
                (!hasSelectedIncarnation || !hasSelectedSummary))
            {
                issues.Add(new ValidationIssue(
                    context,
                    IssueSeverity.Error,
                    "consumed memorySelection с выбранным echo должен хранить и selectedLifeIncarnation, и selectedLifeSummary",
                    code: "pending_shining_blessings_memory_selected_echo_incomplete",
                    section: "ShiningBlessings",
                    repairHint: "Если blessing memory step действительно выбрал echo, запиши selectedLifeIncarnation вместе с selectedLifeSummary и optional selectedLifeHint."));
            }
        }

        if (GetIntOrDefault(value, "options") <= 0 && GetIntOrDefault(value, "rerolls") <= 0)
        {
            issues.Add(new ValidationIssue(
                context,
                IssueSeverity.Error,
                "memorySelection должен давать хотя бы один дополнительный выбор или reroll",
                code: "pending_shining_blessings_empty_memory_selection",
                section: "ShiningBlessings",
                expected: "options > 0 or rerolls > 0",
                actual: $"options={GetIntOrDefault(value, "options")}, rerolls={GetIntOrDefault(value, "rerolls")}"));
        }
    }

    private void ValidateShiningBlessingResourceGrant(JsonElement value, string context, List<ValidationIssue> issues, IReadOnlySet<string> allowedSourceCardIds)
    {
        if (!RequireObject(value, context, issues))
            return;

        ValidateNonNegativeIntegerField(value, context, issues, "money", "ShiningBlessings");
        ValidateNonNegativeIntegerField(value, context, issues, "common", "ShiningBlessings");
        ValidateNonNegativeIntegerField(value, context, issues, "uncommon", "ShiningBlessings");
        RequireString(value, context, issues, "status");
        var appliedAtUtc = RequireString(value, context, issues, "appliedAtUtc");
        if (TryGetArray(value, "sourceCardIds", $"{context}.sourceCardIds", issues, out var sourceCardIds))
        {
            RequireArrayOfStrings(sourceCardIds, $"{context}.sourceCardIds", issues);
            ValidateSourceCardSubset(sourceCardIds, $"{context}.sourceCardIds", issues, allowedSourceCardIds);
        }

        var status = GetFirstNonEmptyString(value, "status") ?? string.Empty;
        if (!string.Equals(status, ShiningBlessingEffectState.ResourceStatusAppliedAtBootstrap, StringComparison.OrdinalIgnoreCase))
        {
            issues.Add(new ValidationIssue(
                $"{context}.status",
                IssueSeverity.Error,
                "resourceGrant.status должен быть applied_at_bootstrap",
                code: "pending_shining_blessings_invalid_resource_status",
                section: "ShiningBlessings",
                expected: ShiningBlessingEffectState.ResourceStatusAppliedAtBootstrap,
                actual: string.IsNullOrWhiteSpace(status) ? "missing" : status));
        }

        if (!string.IsNullOrWhiteSpace(appliedAtUtc) && !DateTimeOffset.TryParse(appliedAtUtc, out _))
        {
            issues.Add(new ValidationIssue(
                $"{context}.appliedAtUtc",
                IssueSeverity.Error,
                "resourceGrant.appliedAtUtc должен быть ISO 8601 timestamp",
                code: "pending_shining_blessings_invalid_resource_applied_at_utc",
                section: "ShiningBlessings",
                expected: "ISO 8601 timestamp",
                actual: appliedAtUtc));
        }

        if (GetIntOrDefault(value, "money") <= 0 &&
            GetIntOrDefault(value, "common") <= 0 &&
            GetIntOrDefault(value, "uncommon") <= 0)
        {
            issues.Add(new ValidationIssue(
                context,
                IssueSeverity.Error,
                "resourceGrant должен выдавать хотя бы один реальный стартовый ресурс",
                code: "pending_shining_blessings_empty_resource_grant",
                section: "ShiningBlessings",
                expected: "money > 0 or common > 0 or uncommon > 0",
                actual: "all grants are zero"));
        }
    }

    private void ValidateShiningBlessingRelicEntitlements(JsonElement value, string context, List<ValidationIssue> issues, IReadOnlySet<string> allowedSourceCardIds)
    {
        if (!RequireObject(value, context, issues))
            return;

        ValidateNonNegativeIntegerField(value, context, issues, "rerolls", "ShiningBlessings");
        RequireBooleanField(value, context, issues, "freeShape");
        RequireBooleanField(value, context, issues, "freeRetune");
        RequireString(value, context, issues, "status");
        if (TryGetArray(value, "sourceCardIds", $"{context}.sourceCardIds", issues, out var sourceCardIds))
        {
            RequireArrayOfStrings(sourceCardIds, $"{context}.sourceCardIds", issues);
            ValidateSourceCardSubset(sourceCardIds, $"{context}.sourceCardIds", issues, allowedSourceCardIds);
        }
        if (value.TryGetProperty("rerollsSpent", out _))
            ValidateNonNegativeIntegerField(value, context, issues, "rerollsSpent", "ShiningBlessings");

        var status = GetFirstNonEmptyString(value, "status") ?? string.Empty;
        if (!string.Equals(status, ShiningBlessingEffectState.RelicStatusPendingEntitlement, StringComparison.OrdinalIgnoreCase) &&
            !string.Equals(status, ShiningBlessingEffectState.GenericStatusConsumed, StringComparison.OrdinalIgnoreCase))
        {
            issues.Add(new ValidationIssue(
                $"{context}.status",
                IssueSeverity.Error,
                "relicRefinementEntitlements.status должен быть pending_relic_entitlement или consumed",
                code: "pending_shining_blessings_invalid_relic_status",
                section: "ShiningBlessings",
                expected: $"{ShiningBlessingEffectState.RelicStatusPendingEntitlement} | {ShiningBlessingEffectState.GenericStatusConsumed}",
                actual: string.IsNullOrWhiteSpace(status) ? "missing" : status));
        }

        if (string.Equals(status, ShiningBlessingEffectState.GenericStatusConsumed, StringComparison.OrdinalIgnoreCase))
        {
            ValidateNonNegativeIntegerField(value, context, issues, "consumedAtTurn", "ShiningBlessings");
            RequireString(value, context, issues, "consumedAtUtc");
        }

        var rerolls = GetIntOrDefault(value, "rerolls");
        var freeShape = value.TryGetProperty("freeShape", out var freeShapeValue) && freeShapeValue.ValueKind == JsonValueKind.True;
        var freeRetune = value.TryGetProperty("freeRetune", out var freeRetuneValue) && freeRetuneValue.ValueKind == JsonValueKind.True;
        if (string.Equals(status, ShiningBlessingEffectState.RelicStatusPendingEntitlement, StringComparison.OrdinalIgnoreCase) &&
            rerolls <= 0 && !freeShape && !freeRetune)
        {
            issues.Add(new ValidationIssue(
                context,
                IssueSeverity.Error,
                "pending relic entitlements должны содержать хотя бы один неистраченный allowance",
                code: "pending_shining_blessings_empty_relic_entitlement",
                section: "ShiningBlessings",
                expected: "rerolls > 0 or freeShape=true or freeRetune=true",
                actual: $"rerolls={rerolls}, freeShape={freeShape}, freeRetune={freeRetune}"));
        }

        if (string.Equals(status, ShiningBlessingEffectState.GenericStatusConsumed, StringComparison.OrdinalIgnoreCase) &&
            (rerolls > 0 || freeShape || freeRetune))
        {
            issues.Add(new ValidationIssue(
                context,
                IssueSeverity.Error,
                "consumed relic entitlements не должны сохранять неистраченные allowance",
                code: "pending_shining_blessings_relic_entitlement_not_fully_consumed",
                section: "ShiningBlessings",
                expected: "rerolls = 0 and freeShape = false and freeRetune = false",
                actual: $"rerolls={rerolls}, freeShape={freeShape}, freeRetune={freeRetune}"));
        }
    }

    private void ValidateShiningBlessingEffectArray(
        JsonElement root,
        string propertyName,
        string context,
        List<ValidationIssue> issues,
        IReadOnlySet<string> allowedSourceCardIds,
        IReadOnlyCollection<string> requiredProperties,
        string expectedStatus,
        bool allowExpiredStatus)
    {
        if (!root.TryGetProperty(propertyName, out var value) || value.ValueKind == JsonValueKind.Null)
            return;

        RequireArrayOfObjects(value, context, issues);
        if (value.ValueKind != JsonValueKind.Array)
            return;

        var index = 0;
        foreach (var item in value.EnumerateArray())
        {
            var itemContext = $"{context}[{index++}]";
            if (!RequireObject(item, itemContext, issues))
                continue;

            var missingFields = GetMissingRequiredNonEmptyStringProperties(item, requiredProperties.Where(field =>
                !string.Equals(field, "delta", StringComparison.OrdinalIgnoreCase) &&
                !string.Equals(field, "routeOptions", StringComparison.OrdinalIgnoreCase) &&
                !string.Equals(field, "latestTurn", StringComparison.OrdinalIgnoreCase) &&
                !string.Equals(field, "clueCount", StringComparison.OrdinalIgnoreCase) &&
                !string.Equals(field, "downgrade", StringComparison.OrdinalIgnoreCase) &&
                !string.Equals(field, "recovery", StringComparison.OrdinalIgnoreCase) &&
                !string.Equals(field, "quality", StringComparison.OrdinalIgnoreCase)).ToArray());

            foreach (var field in requiredProperties)
            {
                if (string.Equals(field, "delta", StringComparison.OrdinalIgnoreCase) ||
                    string.Equals(field, "routeOptions", StringComparison.OrdinalIgnoreCase) ||
                    string.Equals(field, "latestTurn", StringComparison.OrdinalIgnoreCase) ||
                    string.Equals(field, "clueCount", StringComparison.OrdinalIgnoreCase) ||
                    string.Equals(field, "downgrade", StringComparison.OrdinalIgnoreCase) ||
                    string.Equals(field, "recovery", StringComparison.OrdinalIgnoreCase) ||
                    string.Equals(field, "quality", StringComparison.OrdinalIgnoreCase))
                {
                    if (!item.TryGetProperty(field, out _))
                        missingFields.Add(field);
                }
            }

            if (missingFields.Count > 0)
            {
                issues.Add(new ValidationIssue(
                    itemContext,
                    IssueSeverity.Error,
                    $"{propertyName} effect не содержит обязательные поля",
                    code: "pending_shining_blessings_effect_missing_fields",
                    section: "ShiningBlessings",
                    expected: string.Join(", ", requiredProperties),
                    actual: string.Join(", ", missingFields)));
                continue;
            }

            ValidateOptionalString(item, itemContext, issues, "displayName");
            ValidateOptionalString(item, itemContext, issues, "displaySummary");
            ValidateOptionalString(item, itemContext, issues, "sourceFactionId");
            ValidateOptionalString(item, itemContext, issues, "sourceActorId");
            var sourceCardId = GetFirstNonEmptyString(item, "sourceCardId") ?? string.Empty;
            if (!string.IsNullOrWhiteSpace(sourceCardId) && !allowedSourceCardIds.Contains(sourceCardId))
            {
                issues.Add(new ValidationIssue(
                    $"{itemContext}.sourceCardId",
                    IssueSeverity.Error,
                    $"{propertyName}.sourceCardId должен ссылаться на один из sourceCardIds blessing state",
                    code: "pending_shining_blessings_effect_source_card_unknown",
                    section: "ShiningBlessings",
                    actual: sourceCardId));
            }

            foreach (var field in requiredProperties)
            {
                if (!item.TryGetProperty(field, out _))
                    continue;

                if (!string.Equals(field, "effectId", StringComparison.OrdinalIgnoreCase) &&
                    !string.Equals(field, "sourceCardId", StringComparison.OrdinalIgnoreCase) &&
                    !string.Equals(field, "sourceActorId", StringComparison.OrdinalIgnoreCase) &&
                    !string.Equals(field, "status", StringComparison.OrdinalIgnoreCase))
                {
                    ValidateNonNegativeIntegerField(item, itemContext, issues, field, "ShiningBlessings");
                }
            }

            var status = GetFirstNonEmptyString(item, "status") ?? string.Empty;
            var isPending = string.Equals(status, expectedStatus, StringComparison.OrdinalIgnoreCase);
            var isConsumed = string.Equals(status, ShiningBlessingEffectState.GenericStatusConsumed, StringComparison.OrdinalIgnoreCase);
            var isExpired = string.Equals(status, ShiningBlessingEffectState.GenericStatusExpired, StringComparison.OrdinalIgnoreCase);
            if (!isPending && !isConsumed && !(allowExpiredStatus && isExpired))
            {
                issues.Add(new ValidationIssue(
                    $"{itemContext}.status",
                    IssueSeverity.Error,
                    $"{propertyName}.status должен быть canonical blessing lifecycle status",
                    code: "pending_shining_blessings_invalid_effect_status",
                    section: "ShiningBlessings",
                    expected: allowExpiredStatus
                        ? $"{expectedStatus} | {ShiningBlessingEffectState.GenericStatusConsumed} | {ShiningBlessingEffectState.GenericStatusExpired}"
                        : $"{expectedStatus} | {ShiningBlessingEffectState.GenericStatusConsumed}",
                    actual: string.IsNullOrWhiteSpace(status) ? "missing" : status));
            }

            if (isConsumed)
            {
                ValidateNonNegativeIntegerField(item, itemContext, issues, "consumedAtTurn", "ShiningBlessings");
                RequireString(item, itemContext, issues, "consumedAtUtc");
            }

            if (allowExpiredStatus && isExpired)
            {
                ValidateNonNegativeIntegerField(item, itemContext, issues, "expiredAtTurn", "ShiningBlessings");
                RequireString(item, itemContext, issues, "expiredAtUtc");
            }

            ValidateShiningBlessingEffectPayload(propertyName, item, itemContext, issues, isPending, isConsumed, allowExpiredStatus && isExpired);
        }
    }

    private void ValidateShiningBlessingEffectPayload(
        string propertyName,
        JsonElement item,
        string context,
        List<ValidationIssue> issues,
        bool isPending,
        bool isConsumed,
        bool isExpired)
    {
        switch (propertyName)
        {
            case "pendingSocialEffects":
                if (GetIntOrDefault(item, "delta") <= 0)
                {
                    issues.Add(new ValidationIssue(
                        $"{context}.delta",
                        IssueSeverity.Error,
                        "social blessing delta должен быть > 0",
                        code: "pending_shining_blessings_social_delta_non_positive",
                        section: "ShiningBlessings",
                        expected: "> 0",
                        actual: GetIntOrDefault(item, "delta").ToString()));
                }

                if (isConsumed)
                {
                    var consumedNpcId = GetFirstNonEmptyString(item, "consumedTargetNpcId");
                    var consumedFactionId = GetFirstNonEmptyString(item, "consumedTargetFactionId");
                    if (string.IsNullOrWhiteSpace(consumedNpcId) && string.IsNullOrWhiteSpace(consumedFactionId))
                    {
                        issues.Add(new ValidationIssue(
                            context,
                            IssueSeverity.Error,
                            "consumed social blessing должен указывать NPC или faction target",
                            code: "pending_shining_blessings_social_missing_consumed_target",
                            section: "ShiningBlessings",
                            expected: "consumedTargetNpcId or consumedTargetFactionId",
                            actual: "missing"));
                    }
                }

                break;

            case "pendingRouteEffects":
                ValidatePositiveIntegerField(item, context, issues, "routeOptions");
                ValidatePositiveIntegerField(item, context, issues, "latestTurn");
                if (isConsumed)
                {
                    RequireStringArrayProperty(item, context, issues, "consumedEventIds");
                    RequireStringArrayProperty(item, context, issues, "consumedRouteSeedIds");
                }

                if (isExpired && GetIntOrDefault(item, "latestTurn") <= 0)
                {
                    issues.Add(new ValidationIssue(
                        $"{context}.latestTurn",
                        IssueSeverity.Error,
                        "expired route blessing должен иметь положительный latestTurn",
                        code: "pending_shining_blessings_route_expired_without_deadline",
                        section: "ShiningBlessings",
                        expected: "> 0",
                        actual: GetIntOrDefault(item, "latestTurn").ToString()));
                }

                break;

            case "pendingLoreEffects":
                ValidatePositiveIntegerField(item, context, issues, "clueCount");
                ValidatePositiveIntegerField(item, context, issues, "latestTurn");
                if (isConsumed)
                {
                    RequireStringArrayProperty(item, context, issues, "consumedEventIds");
                    RequireStringArrayProperty(item, context, issues, "consumedAnchorIds");
                }

                if (isExpired && GetIntOrDefault(item, "latestTurn") <= 0)
                {
                    issues.Add(new ValidationIssue(
                        $"{context}.latestTurn",
                        IssueSeverity.Error,
                        "expired lore blessing должен иметь положительный latestTurn",
                        code: "pending_shining_blessings_lore_expired_without_deadline",
                        section: "ShiningBlessings",
                        expected: "> 0",
                        actual: GetIntOrDefault(item, "latestTurn").ToString()));
                }

                break;

            case "pendingSurvivalEffects":
                ValidatePositiveIntegerField(item, context, issues, "downgrade");
                if (isConsumed)
                {
                    RequireString(item, context, issues, "consumedEventId");
                }

                break;

            case "pendingDescentEffects":
                ValidatePositiveIntegerField(item, context, issues, "latestTurn");
                ValidatePositiveIntegerField(item, context, issues, "quality");
                ValidateOptionalString(item, context, issues, "primedRelicId");
                if (!string.IsNullOrWhiteSpace(GetFirstNonEmptyString(item, "primedRelicId")))
                {
                    ValidateNonNegativeIntegerField(item, context, issues, "primedAtTurn", "ShiningBlessings");
                    RequireString(item, context, issues, "primedAtUtc");
                }

                if (isConsumed)
                {
                    RequireString(item, context, issues, "consumedNpcId");
                }

                if (isExpired && GetIntOrDefault(item, "latestTurn") <= 0)
                {
                    issues.Add(new ValidationIssue(
                        $"{context}.latestTurn",
                        IssueSeverity.Error,
                        "expired descent blessing должен иметь положительный latestTurn",
                        code: "pending_shining_blessings_descent_expired_without_deadline",
                        section: "ShiningBlessings",
                        expected: "> 0",
                        actual: GetIntOrDefault(item, "latestTurn").ToString()));
                }

                break;
        }
    }

    private void RequireStringArrayProperty(JsonElement item, string context, List<ValidationIssue> issues, string propertyName)
    {
        if (!item.TryGetProperty(propertyName, out var propertyValue))
        {
            issues.Add(new ValidationIssue(
                $"{context}.{propertyName}",
                IssueSeverity.Error,
                $"{propertyName} обязателен для consumed blessing outcome",
                code: "pending_shining_blessings_missing_consumed_array",
                section: "ShiningBlessings",
                expected: "non-empty string array",
                actual: "missing"));
            return;
        }

        if (TryGetArray(item, propertyName, $"{context}.{propertyName}", issues, out var array))
        {
            RequireArrayOfStrings(array, $"{context}.{propertyName}", issues);
            if (array.ValueKind == JsonValueKind.Array && array.GetArrayLength() == 0)
            {
                issues.Add(new ValidationIssue(
                    $"{context}.{propertyName}",
                    IssueSeverity.Error,
                    $"{propertyName} не должен быть пустым для consumed blessing outcome",
                    code: "pending_shining_blessings_empty_consumed_array",
                    section: "ShiningBlessings",
                    expected: "non-empty string array",
                    actual: "empty array"));
            }

            ValidateUniqueStringArray(array, $"{context}.{propertyName}", issues);
        }
    }

    private void ValidateSourceCardSubset(JsonElement array, string context, List<ValidationIssue> issues, IReadOnlySet<string> allowedSourceCardIds)
    {
        if (array.ValueKind != JsonValueKind.Array)
            return;

        var index = 0;
        foreach (var item in array.EnumerateArray())
        {
            if (item.ValueKind == JsonValueKind.String)
            {
                var sourceCardId = item.GetString() ?? string.Empty;
                if (!string.IsNullOrWhiteSpace(sourceCardId) && !allowedSourceCardIds.Contains(sourceCardId))
                {
                    issues.Add(new ValidationIssue(
                        $"{context}[{index}]",
                        IssueSeverity.Error,
                        "blessing block sourceCardIds должен быть подмножеством root sourceCardIds",
                        code: "pending_shining_blessings_unknown_source_card_id",
                        section: "ShiningBlessings",
                        actual: sourceCardId));
                }
            }

            index++;
        }
    }

    private void ValidateUniqueStringArray(JsonElement array, string context, List<ValidationIssue> issues)
    {
        if (array.ValueKind != JsonValueKind.Array)
            return;

        var seen = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        var index = 0;
        foreach (var item in array.EnumerateArray())
        {
            if (item.ValueKind == JsonValueKind.String)
            {
                var value = item.GetString() ?? string.Empty;
                if (!string.IsNullOrWhiteSpace(value) && !seen.Add(value))
                {
                    issues.Add(new ValidationIssue(
                        $"{context}[{index}]",
                        IssueSeverity.Error,
                        "consumed blessing arrays не должны содержать дубликаты",
                        code: "pending_shining_blessings_duplicate_consumed_array_item",
                        section: "ShiningBlessings",
                        actual: value));
                }
            }

            index++;
        }
    }


    private void ValidateMetaLifeTransitionsObject(JsonElement lifeTransitions, string context, List<ValidationIssue> issues)
    {
        if (!lifeTransitions.TryGetProperty("recordLifeCompletion", out var recordLifeCompletion))
            return;

        if (!RequireObject(recordLifeCompletion, $"{context}.recordLifeCompletion", issues))
            return;

        var recordContext = $"{context}.recordLifeCompletion";
        RequireObjectProperty(recordLifeCompletion, recordContext, issues, "characterFinalState");
        if (recordLifeCompletion.TryGetProperty("majorAchievements", out var majorAchievements))
            RequireArrayOfStrings(majorAchievements, $"{recordContext}.majorAchievements", issues);
        else
            issues.Add(new ValidationIssue(
                $"{recordContext}.majorAchievements",
                IssueSeverity.Error,
                "recordLifeCompletion должен содержать majorAchievements array",
                code: "life_transition_record_missing_major_achievements",
                section: "Lifecycle",
                expected: "majorAchievements array",
                actual: "missing",
                repairHint: "При записи recordLifeCompletion сохрани массив majorAchievements, даже если он пустой."));
        if (recordLifeCompletion.TryGetProperty("relationshipsFormed", out var relationshipsFormed))
            RequireArrayOfObjects(relationshipsFormed, $"{recordContext}.relationshipsFormed", issues);
        else
            issues.Add(new ValidationIssue(
                $"{recordContext}.relationshipsFormed",
                IssueSeverity.Error,
                "recordLifeCompletion должен содержать relationshipsFormed array",
                code: "life_transition_record_missing_relationships",
                section: "Lifecycle",
                expected: "relationshipsFormed array",
                actual: "missing",
                repairHint: "При записи recordLifeCompletion сохрани массив relationshipsFormed, даже если он пустой."));
        if (recordLifeCompletion.TryGetProperty("moralChoices", out var moralChoices))
            RequireArrayOfObjects(moralChoices, $"{recordContext}.moralChoices", issues);
        else
            issues.Add(new ValidationIssue(
                $"{recordContext}.moralChoices",
                IssueSeverity.Error,
                "recordLifeCompletion должен содержать moralChoices array",
                code: "life_transition_record_missing_moral_choices",
                section: "Lifecycle",
                expected: "moralChoices array",
                actual: "missing",
                repairHint: "При записи recordLifeCompletion сохрани массив moralChoices, даже если он пустой."));
        if (recordLifeCompletion.TryGetProperty("skillsLearned", out var skillsLearned))
            RequireArrayOfStrings(skillsLearned, $"{recordContext}.skillsLearned", issues);
        else
            issues.Add(new ValidationIssue(
                $"{recordContext}.skillsLearned",
                IssueSeverity.Error,
                "recordLifeCompletion должен содержать skillsLearned array",
                code: "life_transition_record_missing_skills_learned",
                section: "Lifecycle",
                expected: "skillsLearned array",
                actual: "missing",
                repairHint: "При записи recordLifeCompletion сохрани массив skillsLearned, даже если он пустой."));

        if (!recordLifeCompletion.TryGetProperty("enlightenmentGained", out _))
        {
            issues.Add(new ValidationIssue(
                $"{recordContext}.enlightenmentGained",
                IssueSeverity.Error,
                "recordLifeCompletion должен содержать enlightenmentGained",
                code: "life_transition_record_missing_enlightenment_gained",
                section: "Lifecycle",
                expected: "enlightenmentGained number",
                actual: "missing",
                repairHint: "При записи recordLifeCompletion укажи enlightenmentGained, даже если прирост равен 0."));
        }
        else
        {
            ValidateNonNegativeNumberField(recordLifeCompletion, recordContext, issues, "enlightenmentGained");
        }
    }


    private bool ValidateMemoryLegacyGrantObject(JsonElement grant, string context, List<ValidationIssue> issues)
    {
        var missingGrantFields = GetMissingRequiredNonEmptyStringProperties(grant, "legacyId", "legacyType", "sourceLifeHint");
        if (missingGrantFields.Count > 0)
        {
            issues.Add(new ValidationIssue(
                context,
                IssueSeverity.Error,
                "memoryLegacyGrant не содержит обязательные корневые поля",
                code: "memory_legacy_grant_missing_required_fields",
                section: "MemoryLegacy",
                expected: "Non-empty legacyId, legacyType, sourceLifeHint",
                actual: string.Join(", ", missingGrantFields),
                repairHint: "Сначала собери canonical memoryLegacyGrant root contract с sourceLifeHint, затем добавляй type-specific payload."));
            return false;
        }

        var legacyType = grant.TryGetProperty("legacyType", out var typeEl) && typeEl.ValueKind == JsonValueKind.String
            ? typeEl.GetString() ?? string.Empty
            : string.Empty;

        if (string.Equals(legacyType, "startingCharacteristicBonus", StringComparison.OrdinalIgnoreCase))
        {
            var missingCharacteristicGrantFields = GetMissingRequiredNonEmptyStringProperties(grant, "characteristic");
            if (missingCharacteristicGrantFields.Count > 0 || !grant.TryGetProperty("bonus", out _))
            {
                var actualMissing = new List<string>(missingCharacteristicGrantFields);
                if (!grant.TryGetProperty("bonus", out _))
                    actualMissing.Add("bonus");

                issues.Add(new ValidationIssue(
                    context,
                    IssueSeverity.Error,
                    "startingCharacteristicBonus memoryLegacyGrant не содержит type-specific обязательные поля",
                    code: "memory_legacy_grant_characteristic_fields_missing",
                    section: "MemoryLegacy",
                    expected: "characteristic and bonus for startingCharacteristicBonus",
                    actual: string.Join(", ", actualMissing),
                    repairHint: "Для legacyType=startingCharacteristicBonus передай characteristic и bonus=2 в structured memoryLegacyGrant."));
                return false;
            }

            var characteristic = GetFirstNonEmptyString(grant, "characteristic") ?? string.Empty;
            ValidatePositiveIntegerField(grant, context, issues, "bonus");

            if (!string.IsNullOrWhiteSpace(characteristic) &&
                !Characteristics.All.Contains(characteristic, StringComparer.OrdinalIgnoreCase))
            {
                issues.Add(new ValidationIssue(
                    $"{context}.characteristic",
                    IssueSeverity.Error,
                    "memoryLegacyGrant.characteristic должен быть допустимым именем характеристики",
                    code: "memory_legacy_grant_invalid_characteristic",
                    section: "MemoryLegacy",
                    expected: "valid characteristic name",
                    actual: characteristic,
                    repairHint: "Используй canonical имя характеристики из rules/spec для startingCharacteristicBonus grant."));
                return false;
            }

            if (grant.TryGetProperty("bonus", out var bonusEl) &&
                bonusEl.ValueKind == JsonValueKind.Number &&
                bonusEl.TryGetInt32(out var bonus) &&
                bonus != 2)
            {
                issues.Add(new ValidationIssue(
                    $"{context}.bonus",
                    IssueSeverity.Error,
                    "memoryLegacyGrant для startingCharacteristicBonus должен давать ровно +2",
                    code: "memory_legacy_grant_invalid_characteristic_bonus",
                    section: "MemoryLegacy",
                    expected: "2",
                    actual: bonus.ToString(),
                    repairHint: "Structured memoryLegacyGrant для startingCharacteristicBonus всегда должен задавать bonus=2."));
                return false;
            }
        }
        else if (string.Equals(legacyType, "startingPassiveKnowledgeSkill", StringComparison.OrdinalIgnoreCase))
        {
            var missingSkillGrantFields = GetMissingRequiredNonEmptyStringProperties(
                grant,
                "skillName",
                "skillDescription",
                "group",
                "playerStatBonus");
            if (missingSkillGrantFields.Count > 0 || !grant.TryGetProperty("structuredBonuses", out _))
            {
                var actualMissing = new List<string>(missingSkillGrantFields);
                if (!grant.TryGetProperty("structuredBonuses", out _))
                    actualMissing.Add("structuredBonuses");

                issues.Add(new ValidationIssue(
                    context,
                    IssueSeverity.Error,
                    "startingPassiveKnowledgeSkill memoryLegacyGrant не содержит type-specific обязательные поля",
                    code: "memory_legacy_grant_skill_fields_missing",
                    section: "MemoryLegacy",
                    expected: "skillName, skillDescription, group, playerStatBonus, structuredBonuses",
                    actual: string.Join(", ", actualMissing),
                    repairHint: "Для legacyType=startingPassiveKnowledgeSkill передай полный canonical skill grant и непустой structuredBonuses."));
                return false;
            }

            ValidateOptionalString(grant, context, issues, "rarity");
            ValidateOptionalString(grant, context, issues, "type");
            ValidatePositiveIntegerField(grant, context, issues, "masteryLevel");
            ValidatePositiveIntegerField(grant, context, issues, "maxMasteryLevel");

            if (grant.TryGetProperty("group", out var groupEl) &&
                groupEl.ValueKind == JsonValueKind.String &&
                !string.Equals(groupEl.GetString(), "Knowledge", StringComparison.OrdinalIgnoreCase))
            {
                issues.Add(new ValidationIssue(
                    $"{context}.group",
                    IssueSeverity.Error,
                    "memoryLegacyGrant для passive skill должен иметь group = 'Knowledge'",
                    code: "memory_legacy_grant_invalid_skill_group",
                    section: "MemoryLegacy",
                    expected: "Knowledge",
                    actual: groupEl.GetString() ?? string.Empty,
                    repairHint: "Passive-skill memoryLegacyGrant должен использовать canonical group=Knowledge."));
                return false;
            }

            if (!grant.TryGetProperty("structuredBonuses", out var bonuses))
            {
                issues.Add(new ValidationIssue(
                    $"{context}.structuredBonuses",
                    IssueSeverity.Error,
                    "memoryLegacyGrant для passive skill должен содержать structuredBonuses",
                    code: "memory_legacy_grant_missing_structured_bonuses",
                    section: "MemoryLegacy",
                    expected: "non-empty structuredBonuses array",
                    actual: "missing",
                    repairHint: "Добавь в passive-skill memoryLegacyGrant непустой structuredBonuses array."));
                return false;
            }
            else
            {
                RequireArrayOfObjects(bonuses, $"{context}.structuredBonuses", issues);
                if (bonuses.ValueKind == JsonValueKind.Array && bonuses.GetArrayLength() == 0)
                {
                    issues.Add(new ValidationIssue(
                        $"{context}.structuredBonuses",
                        IssueSeverity.Error,
                        "structuredBonuses для passive skill legacy не должен быть пустым",
                        code: "memory_legacy_grant_empty_structured_bonuses",
                        section: "MemoryLegacy",
                        expected: "non-empty structuredBonuses array",
                        actual: "empty array",
                        repairHint: "Не оставляй passive-skill memoryLegacyGrant без механических бонусов. Сохрани непустой structuredBonuses array."));
                    return false;
                }
            }
        }
        else
        {
            issues.Add(new ValidationIssue(
                $"{context}.legacyType",
                IssueSeverity.Error,
                "memoryLegacyGrant должен использовать legacyType startingCharacteristicBonus или startingPassiveKnowledgeSkill",
                code: "memory_legacy_grant_unsupported_legacy_type",
                section: "MemoryLegacy",
                expected: "startingCharacteristicBonus | startingPassiveKnowledgeSkill",
                actual: string.IsNullOrWhiteSpace(legacyType) ? "missing" : legacyType,
                repairHint: "Используй только canonical legacyType для structured memoryLegacyGrant."));
            return false;
        }

        return true;
    }


    private void ValidateGuardianTradeState(JsonElement guardian, string guardianContext, List<ValidationIssue> issues)
    {
        var guardianId = GetFirstNonEmptyString(guardian, "guardianId");
        var guardianAbodeId = guardian.TryGetProperty("abode", out var abodeNode) && abodeNode.ValueKind == JsonValueKind.Object
            ? GetFirstNonEmptyString(abodeNode, "abodeId")
            : null;

        ValidateGuardianBuybackRelics(guardian, guardianContext, guardianId, issues);

        if (!guardian.TryGetProperty("tradeInventory", out var tradeInventory))
        {
            ValidateGuardianTradeReceipts(guardian, guardianContext, guardianId, guardianAbodeId, tradeCycleId: null, expectedItemCount: null, issues);
            return;
        }

        if (tradeInventory.ValueKind != JsonValueKind.Object)
        {
            issues.Add(new ValidationIssue(
                $"{guardianContext}.tradeInventory",
                IssueSeverity.Warning,
                "Persisted guardian tradeInventory повреждён",
                code: "guardian_trade_inventory_root_malformed",
                section: "tradeInventory",
                expected: "object or omitted tradeInventory",
                actual: tradeInventory.ValueKind.ToString(),
                repairHint: "Заменяй malformed tradeInventory явным authored inventory contract, а не рассчитывай на client-side regeneration."));
            return;
        }

        var tradeContext = $"{guardianContext}.tradeInventory";
        var tradeCycleId = GetFirstNonEmptyString(tradeInventory, "tradeCycleId");
        if (string.IsNullOrWhiteSpace(tradeCycleId))
        {
            issues.Add(new ValidationIssue(
                $"{tradeContext}.tradeCycleId",
                IssueSeverity.Warning,
                "Persisted guardian tradeInventory не содержит tradeCycleId",
                code: "guardian_trade_inventory_missing_trade_cycle_id",
                section: "tradeInventory",
                expected: "non-empty tradeCycleId",
                actual: "missing or empty",
                repairHint: "Author explicit guardian tradeInventory для текущего цикла торговли и сохраняй tradeCycleId явно."));
            return;
        }

        var generatedAtUtc = GetFirstNonEmptyString(tradeInventory, "generatedAtUtc");
        if (string.IsNullOrWhiteSpace(generatedAtUtc))
        {
            issues.Add(new ValidationIssue(
                $"{tradeContext}.generatedAtUtc",
                IssueSeverity.Warning,
                "Persisted guardian tradeInventory не содержит generatedAtUtc",
                code: "guardian_trade_inventory_missing_generated_at",
                section: "tradeInventory",
                expected: "non-empty generatedAtUtc",
                actual: "missing or empty",
                repairHint: "Author explicit guardian tradeInventory и указывай generatedAtUtc как часть authored stock contract."));
            return;
        }
        var generationTier = GetFirstNonEmptyString(tradeInventory, "generationReputationTier");
        if (string.IsNullOrWhiteSpace(generationTier))
        {
            issues.Add(new ValidationIssue(
                $"{tradeContext}.generationReputationTier",
                IssueSeverity.Warning,
                "Persisted guardian tradeInventory не содержит generationReputationTier",
                code: "guardian_trade_inventory_missing_generation_tier",
                section: "tradeInventory",
                expected: "non-empty generationReputationTier",
                actual: "missing or empty",
                repairHint: "Author explicit guardian tradeInventory и сохраняй generationReputationTier как часть явного торгового контракта."));
            return;
        }

        var pricingTier = GetFirstNonEmptyString(tradeInventory, "pricingReputationTier");
        if (string.IsNullOrWhiteSpace(pricingTier))
        {
            issues.Add(new ValidationIssue(
                $"{tradeContext}.pricingReputationTier",
                IssueSeverity.Warning,
                "Persisted guardian tradeInventory не содержит pricingReputationTier",
                code: "guardian_trade_inventory_missing_pricing_tier",
                section: "tradeInventory",
                expected: "non-empty pricingReputationTier",
                actual: "missing or empty",
                repairHint: "Author explicit guardian tradeInventory и сохраняй pricingReputationTier как часть явного торгового контракта."));
            return;
        }

        if (!string.IsNullOrWhiteSpace(generationTier) && !GuardianTradeService.IsValidTradeTierCode(generationTier))
        {
            issues.Add(new ValidationIssue(
                $"{tradeContext}.generationReputationTier",
                IssueSeverity.Warning,
                "tradeInventory.generationReputationTier должен быть допустимым trade tier",
                code: "guardian_trade_inventory_generation_tier_invalid",
                section: "tradeInventory",
                expected: "Hostile | Neutral | Friendly | Devoted | Legendary",
                actual: generationTier));
        }

        if (!string.IsNullOrWhiteSpace(pricingTier) && !GuardianTradeService.IsValidTradeTierCode(pricingTier))
        {
            issues.Add(new ValidationIssue(
                $"{tradeContext}.pricingReputationTier",
                IssueSeverity.Warning,
                "tradeInventory.pricingReputationTier должен быть допустимым trade tier",
                code: "guardian_trade_inventory_pricing_tier_invalid",
                section: "tradeInventory",
                expected: "Hostile | Neutral | Friendly | Devoted | Legendary",
                actual: pricingTier));
        }

        if (!tradeInventory.TryGetProperty("items", out var items) || items.ValueKind != JsonValueKind.Array)
        {
            issues.Add(new ValidationIssue(
                $"{tradeContext}.items",
                IssueSeverity.Warning,
                "tradeInventory.items должен быть массивом торговых слотов"));
            return;
        }

        if (TryFindDuplicateGuardianTradeSlotId(items, out var duplicateSlotId))
        {
            issues.Add(new ValidationIssue(
                $"{tradeContext}.items",
                IssueSeverity.Error,
                "guardian tradeInventory содержит duplicated item.slotId; локальная покупка по slotId становится неоднозначной.",
                code: "guardian_trade_inventory_duplicate_slot_id",
                section: "tradeInventory",
                expected: "unique tradeInventory.items[].slotId per inventory",
                actual: duplicateSlotId,
                repairHint: "Сделай каждый tradeInventory.items[].slotId уникальным внутри одной витрины Хранителя."));
        }

        if (!TryResolveGuardianDerivedStateForValidation(
                guardian,
                tradeContext,
                "Guardian trade inventory validation требует readable current guardian project tracker authority и не использует guardian-only derived state как fallback.",
                "guardian_trade_inventory_missing_current_tracker_authority",
                "tradeInventory",
                $"Исправь current {GuardianProjectState.TrackerPath} и validated tracker baseline так, чтобы validator построил guardian-backed current tracker authority перед trade inventory validation.",
                issues,
                out var derivedState))
        {
            return;
        }

        var expectedSlotCount = derivedState.TradeSlotCount;
        if (items.GetArrayLength() != expectedSlotCount)
        {
            issues.Add(new ValidationIssue(
                $"{tradeContext}.items",
                IssueSeverity.Warning,
                $"tradeInventory.items должен содержать ровно {expectedSlotCount} торговых слотов"));
        }

        var actualProjectBonusSignature = GetFirstNonEmptyString(tradeInventory, "projectBonusSignature");
        var storedUpgradedTradeSlots = GetIntOrDefault(tradeInventory, "upgradedTradeSlots");
        var storedElevatedTradeSlots = GetIntOrDefault(tradeInventory, "elevatedTradeSlots");
        var storedRarityBonusSteps = GetIntOrDefault(tradeInventory, "effectiveRarityCeilingBonusSteps");
        var expectedProjectBonusSignature = $"{storedUpgradedTradeSlots}|{storedElevatedTradeSlots}|{storedRarityBonusSteps}";
        if (!string.IsNullOrWhiteSpace(actualProjectBonusSignature) &&
            !string.Equals(actualProjectBonusSignature, expectedProjectBonusSignature, StringComparison.OrdinalIgnoreCase))
        {
            issues.Add(new ValidationIssue(
                $"{tradeContext}.projectBonusSignature",
                IssueSeverity.Warning,
                "tradeInventory.projectBonusSignature должен совпадать с собственными latched trade bonus fields",
                code: "guardian_trade_inventory_project_bonus_signature_mismatch",
                section: "tradeInventory",
                expected: expectedProjectBonusSignature,
                actual: actualProjectBonusSignature,
                repairHint: "projectBonusSignature должен быть latched подписью generated stock и совпадать с upgraded/elevated/rarity fields самой витрины."));
        }

        var index = 0;
        foreach (var item in items.EnumerateArray())
        {
            var itemContext = $"{tradeContext}.items[{index++}]";
            if (!RequireObject(item, itemContext, issues))
                continue;

            RequireString(item, itemContext, issues, "slotId");
            ValidateOptionalString(item, itemContext, issues, "domainTag");
            ValidateNonNegativeNumberField(item, itemContext, issues, "priceInFeathers");

            if (!item.TryGetProperty("soldOut", out var soldOut) ||
                (soldOut.ValueKind != JsonValueKind.True && soldOut.ValueKind != JsonValueKind.False))
            {
                issues.Add(new ValidationIssue(
                    $"{itemContext}.soldOut",
                    IssueSeverity.Warning,
                    "tradeInventory item.soldOut должен быть boolean"));
            }

            if (!item.TryGetProperty("relicData", out var relicData) || !RequireObject(relicData, $"{itemContext}.relicData", issues))
                continue;

            var relicContext = $"{itemContext}.relicData";
            RequireString(relicData, relicContext, issues, "relicId");
            RequireString(relicData, relicContext, issues, "name");
            var rarity = GetFirstNonEmptyString(relicData, "quality", "rarity");
            if (!relicData.TryGetProperty("quality", out var quality) &&
                !relicData.TryGetProperty("rarity", out quality))
            {
                issues.Add(new ValidationIssue(
                    relicContext,
                    IssueSeverity.Warning,
                    "relicData должен содержать quality или rarity"));
            }

            if (!string.IsNullOrWhiteSpace(rarity) &&
                !string.IsNullOrWhiteSpace(generationTier) &&
                GuardianTradeService.IsValidTradeTierCode(generationTier) &&
                !GuardianTradeService.IsRarityAllowedForGenerationTier(
                    rarity,
                    generationTier,
                    GetIntOrDefault(item, "rarityBonusStepsApplied")))
            {
                issues.Add(new ValidationIssue(
                    $"{relicContext}.quality",
                    IssueSeverity.Warning,
                    "tradeInventory item rarity превышает допустимый потолок редкости для generationReputationTier",
                    code: "guardian_trade_inventory_rarity_cap_mismatch",
                    section: "tradeInventory",
                    expected: generationTier,
                    actual: rarity));
            }

            if (!string.IsNullOrWhiteSpace(rarity) &&
                item.TryGetProperty("priceInFeathers", out var priceNode) &&
                priceNode.ValueKind == JsonValueKind.Number &&
                priceNode.TryGetInt32(out var actualPrice) &&
                !string.IsNullOrWhiteSpace(pricingTier) &&
                GuardianTradeService.IsValidTradeTierCode(pricingTier))
            {
                var expectedPrice = GuardianTradeService.ComputeBuyPriceForTierCode(rarity, pricingTier);
                if (actualPrice != expectedPrice)
                {
                    issues.Add(new ValidationIssue(
                        $"{itemContext}.priceInFeathers",
                        IssueSeverity.Warning,
                        "tradeInventory item.priceInFeathers должен совпадать с канонической ценой для pricingReputationTier",
                        code: "guardian_trade_inventory_price_mismatch",
                        section: "tradeInventory",
                        expected: expectedPrice.ToString(),
                        actual: actualPrice.ToString()));
                }
            }
        }

        ValidateGuardianTradeReceipts(
            guardian,
            guardianContext,
            guardianId,
            guardianAbodeId,
            tradeCycleId,
            expectedItemCount: items.GetArrayLength(),
            issues);
    }

    private void ValidateGuardianBuybackRelics(JsonElement guardian, string guardianContext, string? guardianId, List<ValidationIssue> issues)
    {
        if (!guardian.TryGetProperty("buybackRelics", out var buybackRelicsNode))
            return;

        if (buybackRelicsNode.ValueKind != JsonValueKind.Array)
        {
            issues.Add(new ValidationIssue(
                $"{guardianContext}.buybackRelics",
                IssueSeverity.Warning,
                "guardian buybackRelics должен быть массивом canonical buyback entries",
                code: "guardian_buyback_relics_root_malformed",
                section: "tradeInventory",
                expected: "array",
                actual: buybackRelicsNode.ValueKind.ToString(),
                repairHint: "Храни проданные Хранителю реликвии в guardians[].buybackRelics как массив объектов."));
            return;
        }

        var seenEntryIds = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        var index = 0;
        foreach (var entry in buybackRelicsNode.EnumerateArray())
        {
            var entryContext = $"{guardianContext}.buybackRelics[{index++}]";
            if (!RequireObject(entry, entryContext, issues))
                continue;

            var buybackEntryId = RequireString(entry, entryContext, issues, "buybackEntryId");
            var entryGuardianId = RequireString(entry, entryContext, issues, "guardianId");
            RequireString(entry, entryContext, issues, "guardianName");
            var relicId = RequireString(entry, entryContext, issues, "relicId");
            var soldAtTurn = GetIntOrDefault(entry, "soldByPlayerAtTurn", -1);
            var soldAtUtc = RequireString(entry, entryContext, issues, "soldByPlayerAtUtc");
            var soldForPrice = GetIntOrDefault(entry, "soldForPrice", 0);
            var buybackPrice = GetIntOrDefault(entry, "buybackPrice", 0);
            var status = RequireString(entry, entryContext, issues, "status");

            if (!string.IsNullOrWhiteSpace(buybackEntryId) && !seenEntryIds.Add(buybackEntryId))
            {
                issues.Add(new ValidationIssue(
                    $"{entryContext}.buybackEntryId",
                    IssueSeverity.Error,
                    "guardian buybackRelics содержит duplicate buybackEntryId",
                    code: "guardian_buyback_relic_duplicate_id",
                    section: "tradeInventory",
                    expected: "unique buybackEntryId per guardian",
                    actual: buybackEntryId));
            }

            if (!string.IsNullOrWhiteSpace(guardianId) &&
                !string.IsNullOrWhiteSpace(entryGuardianId) &&
                !string.Equals(entryGuardianId, guardianId, StringComparison.OrdinalIgnoreCase))
            {
                issues.Add(new ValidationIssue(
                    $"{entryContext}.guardianId",
                    IssueSeverity.Error,
                    "guardian buybackRelics entry guardianId должен совпадать с guardianId самого Хранителя",
                    code: "guardian_buyback_relic_guardian_mismatch",
                    section: "tradeInventory",
                    expected: guardianId,
                    actual: entryGuardianId));
            }

            if (soldAtTurn < 0)
            {
                issues.Add(new ValidationIssue(
                    $"{entryContext}.soldByPlayerAtTurn",
                    IssueSeverity.Error,
                    "guardian buybackRelics entry soldByPlayerAtTurn должен быть неотрицательным",
                    code: "guardian_buyback_relic_sold_turn_invalid",
                    section: "tradeInventory",
                    expected: ">= 0",
                    actual: soldAtTurn.ToString()));
            }

            if (!string.IsNullOrWhiteSpace(soldAtUtc) && !DateTimeOffset.TryParse(soldAtUtc, out _))
            {
                issues.Add(new ValidationIssue(
                    $"{entryContext}.soldByPlayerAtUtc",
                    IssueSeverity.Error,
                    "guardian buybackRelics entry soldByPlayerAtUtc должен быть валидным ISO timestamp",
                    code: "guardian_buyback_relic_sold_at_invalid",
                    section: "tradeInventory",
                    expected: "valid ISO-8601 timestamp",
                    actual: soldAtUtc));
            }

            if (soldForPrice <= 0)
            {
                issues.Add(new ValidationIssue(
                    $"{entryContext}.soldForPrice",
                    IssueSeverity.Error,
                    "guardian buybackRelics entry soldForPrice должен быть положительным",
                    code: "guardian_buyback_relic_sold_price_invalid",
                    section: "tradeInventory",
                    expected: "> 0",
                    actual: soldForPrice.ToString()));
            }

            if (buybackPrice <= 0)
            {
                issues.Add(new ValidationIssue(
                    $"{entryContext}.buybackPrice",
                    IssueSeverity.Error,
                    "guardian buybackRelics entry buybackPrice должен быть положительным",
                    code: "guardian_buyback_relic_buyback_price_invalid",
                    section: "tradeInventory",
                    expected: "> 0",
                    actual: buybackPrice.ToString()));
            }

            if (!string.IsNullOrWhiteSpace(status) && !GuardianTradeService.IsValidBuybackStatusCode(status))
            {
                issues.Add(new ValidationIssue(
                    $"{entryContext}.status",
                    IssueSeverity.Error,
                    "guardian buybackRelics entry status должен быть canonical buyback status",
                    code: "guardian_buyback_relic_status_invalid",
                    section: "tradeInventory",
                    expected: "available | rebought | removed",
                    actual: status));
            }

            if (entry.TryGetProperty("acquiredFromPlayer", out var acquiredFromPlayer) &&
                acquiredFromPlayer.ValueKind != JsonValueKind.Null)
            {
                if (acquiredFromPlayer.ValueKind != JsonValueKind.True &&
                    acquiredFromPlayer.ValueKind != JsonValueKind.False)
                {
                    issues.Add(new ValidationIssue(
                        $"{entryContext}.acquiredFromPlayer",
                        IssueSeverity.Error,
                        "guardian buybackRelics entry acquiredFromPlayer должен быть boolean",
                        code: "guardian_buyback_relic_acquired_flag_invalid",
                        section: "tradeInventory",
                        expected: "boolean",
                        actual: acquiredFromPlayer.ValueKind.ToString()));
                }
                else if (!acquiredFromPlayer.GetBoolean())
                {
                    issues.Add(new ValidationIssue(
                        $"{entryContext}.acquiredFromPlayer",
                        IssueSeverity.Error,
                        "guardian buybackRelics entry acquiredFromPlayer должен быть true",
                        code: "guardian_buyback_relic_acquired_flag_false",
                        section: "tradeInventory",
                        expected: "true",
                        actual: "false"));
                }
            }

            if (!entry.TryGetProperty("relicData", out var relicData) || !RequireObject(relicData, $"{entryContext}.relicData", issues))
                continue;

            var relicDataId = GetFirstNonEmptyString(relicData, "relicId", "id");
            if (!string.IsNullOrWhiteSpace(relicId) &&
                !string.IsNullOrWhiteSpace(relicDataId) &&
                !string.Equals(relicId, relicDataId, StringComparison.OrdinalIgnoreCase))
            {
                issues.Add(new ValidationIssue(
                    $"{entryContext}.relicId",
                    IssueSeverity.Error,
                    "guardian buybackRelics entry relicId должен совпадать с relicData.relicId",
                    code: "guardian_buyback_relic_id_mismatch",
                    section: "tradeInventory",
                    expected: relicDataId,
                    actual: relicId));
            }

            if (string.Equals(status, "rebought", StringComparison.OrdinalIgnoreCase))
            {
                var reboughtAtTurn = GetIntOrDefault(entry, "reboughtAtTurn", 0);
                var reboughtAtUtc = GetFirstNonEmptyString(entry, "reboughtAtUtc");
                if (reboughtAtTurn <= 0)
                {
                    issues.Add(new ValidationIssue(
                        $"{entryContext}.reboughtAtTurn",
                        IssueSeverity.Error,
                        "guardian buybackRelics entry reboughtAtTurn должен быть положительным для status=rebought",
                        code: "guardian_buyback_relic_rebought_turn_invalid",
                        section: "tradeInventory",
                        expected: "> 0",
                        actual: reboughtAtTurn.ToString()));
                }

                if (string.IsNullOrWhiteSpace(reboughtAtUtc) || !DateTimeOffset.TryParse(reboughtAtUtc, out _))
                {
                    issues.Add(new ValidationIssue(
                        $"{entryContext}.reboughtAtUtc",
                        IssueSeverity.Error,
                        "guardian buybackRelics entry reboughtAtUtc должен быть валидным ISO timestamp для status=rebought",
                        code: "guardian_buyback_relic_rebought_at_invalid",
                        section: "tradeInventory",
                        expected: "valid ISO-8601 timestamp",
                        actual: reboughtAtUtc ?? "missing"));
                }
            }
        }
    }

    private void ValidateGuardianTradeReceipts(
        JsonElement guardian,
        string guardianContext,
        string? guardianId,
        string? guardianAbodeId,
        string? tradeCycleId,
        int? expectedItemCount,
        List<ValidationIssue> issues)
    {
        if (!guardian.TryGetProperty(GuardianTradeRequestState.ReceiptsProperty, out var receiptsNode))
            return;

        if (receiptsNode.ValueKind != JsonValueKind.Array)
        {
            issues.Add(new ValidationIssue(
                $"{guardianContext}.{GuardianTradeRequestState.ReceiptsProperty}",
                IssueSeverity.Warning,
                "guardian tradeInventoryReceipts должен быть массивом canonical receipts",
                code: "guardian_trade_receipts_root_malformed",
                section: "tradeInventory",
                expected: "array",
                actual: receiptsNode.ValueKind.ToString(),
                repairHint: "Храни guardian trade ready receipts как массив объектов в guardians[].tradeInventoryReceipts."));
            return;
        }

        var requestIds = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        var readyCurrentCycleReceipts = 0;
        var index = 0;
        foreach (var receipt in receiptsNode.EnumerateArray())
        {
            var receiptContext = $"{guardianContext}.{GuardianTradeRequestState.ReceiptsProperty}[{index++}]";
            if (!RequireObject(receipt, receiptContext, issues))
                continue;

            var requestId = RequireString(receipt, receiptContext, issues, "requestId");
            var receiptGuardianId = RequireString(receipt, receiptContext, issues, "guardianId");
            RequireString(receipt, receiptContext, issues, "guardianName");
            var receiptAbodeId = RequireString(receipt, receiptContext, issues, "abodeId");
            var receiptTradeCycleId = RequireString(receipt, receiptContext, issues, "tradeCycleId");
            var receiptStatus = RequireString(receipt, receiptContext, issues, "status");
            var resolvedAtUtc = RequireString(receipt, receiptContext, issues, "resolvedAtUtc");
            var itemCount = GetIntOrDefault(receipt, "itemCount", -1);
            var resolvedAtTurn = GetIntOrDefault(receipt, "resolvedAtTurn", 0);

            if (!string.IsNullOrWhiteSpace(requestId) && !requestIds.Add(requestId))
            {
                issues.Add(new ValidationIssue(
                    $"{receiptContext}.requestId",
                    IssueSeverity.Error,
                    "guardian tradeInventoryReceipts содержит duplicate requestId",
                    code: "guardian_trade_receipt_duplicate_request_id",
                    section: "tradeInventory",
                    expected: "unique requestId per guardian",
                    actual: requestId));
            }

            if (!string.IsNullOrWhiteSpace(guardianId) &&
                !string.IsNullOrWhiteSpace(receiptGuardianId) &&
                !string.Equals(receiptGuardianId, guardianId, StringComparison.OrdinalIgnoreCase))
            {
                issues.Add(new ValidationIssue(
                    $"{receiptContext}.guardianId",
                    IssueSeverity.Error,
                    "guardian tradeInventory receipt guardianId должен совпадать с guardianId самого Хранителя",
                    code: "guardian_trade_receipt_guardian_mismatch",
                    section: "tradeInventory",
                    expected: guardianId,
                    actual: receiptGuardianId));
            }

            if (!string.IsNullOrWhiteSpace(guardianAbodeId) &&
                !string.IsNullOrWhiteSpace(receiptAbodeId) &&
                !string.Equals(receiptAbodeId, guardianAbodeId, StringComparison.OrdinalIgnoreCase))
            {
                issues.Add(new ValidationIssue(
                    $"{receiptContext}.abodeId",
                    IssueSeverity.Error,
                    "guardian tradeInventory receipt abodeId должен совпадать с текущей canonical обителью Хранителя",
                    code: "guardian_trade_receipt_abode_mismatch",
                    section: "tradeInventory",
                    expected: guardianAbodeId,
                    actual: receiptAbodeId));
            }

            if (!string.IsNullOrWhiteSpace(receiptStatus) &&
                !string.Equals(receiptStatus, GuardianTradeRequestState.ReceiptStatusReady, StringComparison.OrdinalIgnoreCase))
            {
                issues.Add(new ValidationIssue(
                    $"{receiptContext}.status",
                    IssueSeverity.Error,
                    "guardian tradeInventory receipt status должен быть ready",
                    code: "guardian_trade_receipt_status_invalid",
                    section: "tradeInventory",
                    expected: GuardianTradeRequestState.ReceiptStatusReady,
                    actual: receiptStatus));
            }

            if (itemCount < 0)
            {
                issues.Add(new ValidationIssue(
                    $"{receiptContext}.itemCount",
                    IssueSeverity.Error,
                    "guardian tradeInventory receipt itemCount должен быть неотрицательным числом",
                    code: "guardian_trade_receipt_item_count_invalid",
                    section: "tradeInventory",
                    expected: ">= 0",
                    actual: itemCount.ToString()));
            }

            if (resolvedAtTurn <= 0)
            {
                issues.Add(new ValidationIssue(
                    $"{receiptContext}.resolvedAtTurn",
                    IssueSeverity.Error,
                    "guardian tradeInventory receipt resolvedAtTurn должен быть положительным",
                    code: "guardian_trade_receipt_resolved_turn_invalid",
                    section: "tradeInventory",
                    expected: "> 0",
                    actual: resolvedAtTurn.ToString()));
            }

            if (!string.IsNullOrWhiteSpace(resolvedAtUtc) && !DateTimeOffset.TryParse(resolvedAtUtc, out _))
            {
                issues.Add(new ValidationIssue(
                    $"{receiptContext}.resolvedAtUtc",
                    IssueSeverity.Error,
                    "guardian tradeInventory receipt resolvedAtUtc должен быть валидным ISO timestamp",
                    code: "guardian_trade_receipt_resolved_at_invalid",
                    section: "tradeInventory",
                    expected: "valid ISO-8601 timestamp",
                    actual: resolvedAtUtc));
            }

            if (!string.IsNullOrWhiteSpace(tradeCycleId) &&
                !string.IsNullOrWhiteSpace(receiptTradeCycleId) &&
                string.Equals(receiptTradeCycleId, tradeCycleId, StringComparison.OrdinalIgnoreCase) &&
                string.Equals(receiptStatus, GuardianTradeRequestState.ReceiptStatusReady, StringComparison.OrdinalIgnoreCase))
            {
                readyCurrentCycleReceipts++;

                if (expectedItemCount.HasValue && itemCount >= 0 && itemCount != expectedItemCount.Value)
                {
                    issues.Add(new ValidationIssue(
                        $"{receiptContext}.itemCount",
                        IssueSeverity.Error,
                        "guardian tradeInventory receipt itemCount должен совпадать с числом tradeInventory.items текущего цикла",
                        code: "guardian_trade_receipt_item_count_mismatch",
                        section: "tradeInventory",
                        expected: expectedItemCount.Value.ToString(),
                        actual: itemCount.ToString()));
                }
            }
        }

        if (!string.IsNullOrWhiteSpace(tradeCycleId) && readyCurrentCycleReceipts > 1)
        {
            issues.Add(new ValidationIssue(
                $"{guardianContext}.{GuardianTradeRequestState.ReceiptsProperty}",
                IssueSeverity.Error,
                "guardian tradeInventory не должен иметь несколько ready receipts для одного и того же tradeCycleId",
                code: "guardian_trade_receipt_duplicate_cycle_resolution",
                section: "tradeInventory",
                expected: "at most one ready receipt for current trade cycle",
                actual: readyCurrentCycleReceipts.ToString()));
        }
    }


    private void ValidateGuaranteedArchiveConsultationQuestPresence(
        JsonElement questManagement,
        string questContext,
        string guardianId,
        JsonElement trackerRoot,
        bool hasTrackerValidationRoot,
        List<ValidationIssue> issues)
    {
        if (questManagement.ValueKind != JsonValueKind.Object || string.IsNullOrWhiteSpace(guardianId))
            return;

        var currentIncarnation = ReadCurrentIncarnationSync();
        if (currentIncarnation <= 0)
            return;

        if (!hasTrackerValidationRoot)
            return;

        try
        {
            if (!trackerRoot.TryGetProperty("completedProjects", out var completedProjects) ||
                completedProjects.ValueKind != JsonValueKind.Array)
            {
                return;
            }

            var presentArchiveQuestSources = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
            foreach (var propName in new[] { "availableQuests", "activeQuests", "completedQuests" })
            {
                if (!questManagement.TryGetProperty(propName, out var questArray) || questArray.ValueKind != JsonValueKind.Array)
                    continue;

                foreach (var quest in questArray.EnumerateArray())
                {
                    if (quest.ValueKind != JsonValueKind.Object)
                        continue;

                    var questOrigin = GetFirstNonEmptyString(quest, "questOrigin");
                    if (!string.Equals(questOrigin, GuardianProjectState.ArchiveConsultationHookOrigin, StringComparison.OrdinalIgnoreCase))
                        continue;

                    var sourceProjectId = GetFirstNonEmptyString(quest, "sourceProjectId");
                    if (!string.IsNullOrWhiteSpace(sourceProjectId))
                        presentArchiveQuestSources.Add(sourceProjectId);
                }
            }

            foreach (var entry in completedProjects.EnumerateArray())
            {
                if (!string.Equals(GetFirstNonEmptyString(entry, "guardianId"), guardianId, StringComparison.OrdinalIgnoreCase) ||
                    !entry.TryGetProperty("project", out var project) ||
                    project.ValueKind != JsonValueKind.Object ||
                    !string.Equals(GetFirstNonEmptyString(project, "projectType"), "lore_research", StringComparison.OrdinalIgnoreCase) ||
                    !string.Equals(GetFirstNonEmptyString(project, "finalState"), "Completed", StringComparison.OrdinalIgnoreCase))
                {
                    continue;
                }

                if (!project.TryGetProperty("effectState", out var effectState) || effectState.ValueKind != JsonValueKind.Object)
                    continue;

                var granted = GetIntOrDefault(effectState, "guaranteedArchiveQuestGranted");
                var consumed = GetIntOrDefault(effectState, "guaranteedArchiveQuestConsumed");
                var targetIncarnation = GetIntOrDefault(effectState, "targetIncarnation");
                if (granted <= consumed || targetIncarnation != currentIncarnation)
                    continue;

                var sourceProjectId = GetFirstNonEmptyString(project, "projectId");
                if (string.IsNullOrWhiteSpace(sourceProjectId) || presentArchiveQuestSources.Contains(sourceProjectId))
                    continue;

                issues.Add(new ValidationIssue(
                    $"{questContext}.availableQuests",
                    IssueSeverity.Error,
                    "Архивная консультация гарантировала квест Хранителя в этой жизни, но такой квест не появился в questManagement",
                    code: "guardian_archive_consultation_guaranteed_quest_missing",
                    section: "Guardians",
                    repairHint: "Если lore_fragment был потрачен на archive consultation и targetIncarnation совпал с текущей жизнью, добавь новый guardian quest с questOrigin = archive_consultation_hook и sourceProjectId этого completed lore_research проекта."));
            }
        }
        catch
        {
            // tracker parse issues are reported elsewhere
        }
    }


    private int ReadCurrentIncarnationSync()
    {
        var soulJson = ReadCurrentTrackedFileSync("game_state/meta/soul_state.json");
        if (string.IsNullOrWhiteSpace(soulJson))
            return 0;

        try
        {
            using var doc = JsonDocument.Parse(soulJson);
            return doc.RootElement.TryGetProperty("currentIncarnation", out var incarnation) &&
                   incarnation.ValueKind == JsonValueKind.Number &&
                   incarnation.TryGetInt32(out var parsed)
                ? parsed
                : 0;
        }
        catch
        {
            return 0;
        }
    }


    private void CompareGuardianTradeState(JsonElement activeGuardian, string activeGuardianContext,
        JsonElement guardianFromArray, string guardianArrayContext, List<ValidationIssue> issues)
    {
        var activeHasTrade = activeGuardian.TryGetProperty("tradeInventory", out var activeTradeInventory) &&
                             activeTradeInventory.ValueKind == JsonValueKind.Object;
        var arrayHasTrade = guardianFromArray.TryGetProperty("tradeInventory", out var arrayTradeInventory) &&
                            arrayTradeInventory.ValueKind == JsonValueKind.Object;

        CompareGuardianBuybackState(activeGuardian, activeGuardianContext, guardianFromArray, guardianArrayContext, issues);

        if (!activeHasTrade && !arrayHasTrade)
            return;

        if (activeHasTrade != arrayHasTrade)
        {
            issues.Add(new ValidationIssue(
                $"{activeGuardianContext}.tradeInventory",
                IssueSeverity.Warning,
                $"activeGuardian расходится с {guardianArrayContext}.tradeInventory",
                code: "guardian_trade_inventory_presence_mismatch",
                section: "tradeInventory",
                expected: "same tradeInventory presence in activeGuardian and guardians[]",
                actual: activeHasTrade ? "present only in activeGuardian" : "present only in guardians[]"));
            return;
        }

        var activeCycle = GetFirstNonEmptyString(activeTradeInventory, "tradeCycleId");
        var arrayCycle = GetFirstNonEmptyString(arrayTradeInventory, "tradeCycleId");
        if (!string.Equals(activeCycle, arrayCycle, StringComparison.OrdinalIgnoreCase))
        {
            issues.Add(new ValidationIssue(
                $"{activeGuardianContext}.tradeInventory.tradeCycleId",
                IssueSeverity.Warning,
                $"activeGuardian расходится с {guardianArrayContext}.tradeInventory.tradeCycleId",
                code: "guardian_trade_inventory_cycle_mismatch",
                section: "tradeInventory",
                expected: arrayCycle ?? "null",
                actual: activeCycle ?? "null"));
        }

        var activeGenerated = GetFirstNonEmptyString(activeTradeInventory, "generatedAtUtc");
        var arrayGenerated = GetFirstNonEmptyString(arrayTradeInventory, "generatedAtUtc");
        if (!string.Equals(activeGenerated, arrayGenerated, StringComparison.Ordinal))
        {
            issues.Add(new ValidationIssue(
                $"{activeGuardianContext}.tradeInventory.generatedAtUtc",
                IssueSeverity.Warning,
                $"activeGuardian расходится с {guardianArrayContext}.tradeInventory.generatedAtUtc",
                code: "guardian_trade_inventory_generated_at_mismatch",
                section: "tradeInventory",
                expected: arrayGenerated ?? "null",
                actual: activeGenerated ?? "null"));
        }

        var activeGenerationTier = GetFirstNonEmptyString(activeTradeInventory, "generationReputationTier");
        var arrayGenerationTier = GetFirstNonEmptyString(arrayTradeInventory, "generationReputationTier");
        if (!string.Equals(activeGenerationTier, arrayGenerationTier, StringComparison.OrdinalIgnoreCase))
        {
            issues.Add(new ValidationIssue(
                $"{activeGuardianContext}.tradeInventory.generationReputationTier",
                IssueSeverity.Warning,
                $"activeGuardian расходится с {guardianArrayContext}.tradeInventory.generationReputationTier",
                code: "guardian_trade_inventory_generation_tier_mismatch",
                section: "tradeInventory",
                expected: arrayGenerationTier ?? "null",
                actual: activeGenerationTier ?? "null"));
        }

        var activePricingTier = GetFirstNonEmptyString(activeTradeInventory, "pricingReputationTier");
        var arrayPricingTier = GetFirstNonEmptyString(arrayTradeInventory, "pricingReputationTier");
        if (!string.Equals(activePricingTier, arrayPricingTier, StringComparison.OrdinalIgnoreCase))
        {
            issues.Add(new ValidationIssue(
                $"{activeGuardianContext}.tradeInventory.pricingReputationTier",
                IssueSeverity.Warning,
                $"activeGuardian расходится с {guardianArrayContext}.tradeInventory.pricingReputationTier",
                code: "guardian_trade_inventory_pricing_tier_mismatch",
                section: "tradeInventory",
                expected: arrayPricingTier ?? "null",
                actual: activePricingTier ?? "null"));
        }

        if (!activeTradeInventory.TryGetProperty("items", out var activeItems) || activeItems.ValueKind != JsonValueKind.Array ||
            !arrayTradeInventory.TryGetProperty("items", out var arrayItems) || arrayItems.ValueKind != JsonValueKind.Array)
        {
            return;
        }

        if (TryFindDuplicateGuardianTradeSlotId(activeItems, out var activeDuplicateSlotId))
        {
            issues.Add(new ValidationIssue(
                $"{activeGuardianContext}.tradeInventory.items",
                IssueSeverity.Error,
                "activeGuardian tradeInventory содержит duplicated item.slotId; mirror нельзя сравнить как однозначную витрину.",
                code: "guardian_trade_inventory_duplicate_slot_id",
                section: "tradeInventory",
                expected: "unique activeGuardian.tradeInventory.items[].slotId",
                actual: activeDuplicateSlotId));
            return;
        }

        if (TryFindDuplicateGuardianTradeSlotId(arrayItems, out var arrayDuplicateSlotId))
        {
            issues.Add(new ValidationIssue(
                $"{guardianArrayContext}.tradeInventory.items",
                IssueSeverity.Error,
                "guardians[] tradeInventory содержит duplicated item.slotId; active/array mirror нельзя сравнить как однозначную витрину.",
                code: "guardian_trade_inventory_duplicate_slot_id",
                section: "tradeInventory",
                expected: "unique guardians[].tradeInventory.items[].slotId",
                actual: arrayDuplicateSlotId));
            return;
        }

        var activeBySlot = activeItems.EnumerateArray()
            .Where(item => item.ValueKind == JsonValueKind.Object)
            .Select(item => (SlotId: GetFirstNonEmptyString(item, "slotId"), Signature: BuildGuardianTradeSlotSignature(item)))
            .Where(item => !string.IsNullOrWhiteSpace(item.SlotId))
            .ToDictionary(item => item.SlotId!, item => item.Signature, StringComparer.OrdinalIgnoreCase);

        var arrayBySlot = arrayItems.EnumerateArray()
            .Where(item => item.ValueKind == JsonValueKind.Object)
            .Select(item => (SlotId: GetFirstNonEmptyString(item, "slotId"), Signature: BuildGuardianTradeSlotSignature(item)))
            .Where(item => !string.IsNullOrWhiteSpace(item.SlotId))
            .ToDictionary(item => item.SlotId!, item => item.Signature, StringComparer.OrdinalIgnoreCase);

        if (activeBySlot.Count != arrayBySlot.Count)
        {
            issues.Add(new ValidationIssue(
                $"{activeGuardianContext}.tradeInventory.items",
                IssueSeverity.Warning,
                $"activeGuardian расходится с {guardianArrayContext}.tradeInventory.items по числу слотов",
                code: "guardian_trade_inventory_slot_count_mismatch",
                section: "tradeInventory",
                expected: arrayBySlot.Count.ToString(),
                actual: activeBySlot.Count.ToString()));
            return;
        }

        foreach (var (slotId, expectedSignature) in arrayBySlot)
        {
            if (!activeBySlot.TryGetValue(slotId, out var actualSignature) || !string.Equals(actualSignature, expectedSignature, StringComparison.Ordinal))
            {
                issues.Add(new ValidationIssue(
                    $"{activeGuardianContext}.tradeInventory.items",
                    IssueSeverity.Warning,
                    $"activeGuardian расходится с {guardianArrayContext}.tradeInventory.items[{slotId}]",
                    code: "guardian_trade_inventory_slot_mismatch",
                    section: "tradeInventory",
                    expected: expectedSignature,
                    actual: actualSignature ?? "missing slot"));
            }
        }
    }

    private void CompareGuardianBuybackState(JsonElement activeGuardian, string activeGuardianContext,
        JsonElement guardianFromArray, string guardianArrayContext, List<ValidationIssue> issues)
    {
        var activeHasBuyback = activeGuardian.TryGetProperty("buybackRelics", out var activeBuybackRelics) &&
                               activeBuybackRelics.ValueKind == JsonValueKind.Array;
        var arrayHasBuyback = guardianFromArray.TryGetProperty("buybackRelics", out var arrayBuybackRelics) &&
                              arrayBuybackRelics.ValueKind == JsonValueKind.Array;

        if (!activeHasBuyback && !arrayHasBuyback)
            return;

        if (activeHasBuyback != arrayHasBuyback)
        {
            issues.Add(new ValidationIssue(
                $"{activeGuardianContext}.buybackRelics",
                IssueSeverity.Warning,
                $"activeGuardian расходится с {guardianArrayContext}.buybackRelics",
                code: "guardian_buyback_relics_presence_mismatch",
                section: "tradeInventory",
                expected: "same buybackRelics presence in activeGuardian and guardians[]",
                actual: activeHasBuyback ? "present only in activeGuardian" : "present only in guardians[]"));
            return;
        }

        var activeSignature = BuildCanonicalJsonSignature(activeBuybackRelics);
        var arraySignature = BuildCanonicalJsonSignature(arrayBuybackRelics);
        if (!string.Equals(activeSignature, arraySignature, StringComparison.Ordinal))
        {
            issues.Add(new ValidationIssue(
                $"{activeGuardianContext}.buybackRelics",
                IssueSeverity.Warning,
                $"activeGuardian расходится с {guardianArrayContext}.buybackRelics",
                code: "guardian_buyback_relics_signature_mismatch",
                section: "tradeInventory",
                expected: arraySignature,
                actual: activeSignature));
        }
    }


    private static bool TryFindDuplicateGuardianTradeSlotId(JsonElement items, out string duplicateSlotId)
    {
        duplicateSlotId = string.Empty;
        if (items.ValueKind != JsonValueKind.Array)
            return false;

        var seen = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        foreach (var item in items.EnumerateArray())
        {
            if (item.ValueKind != JsonValueKind.Object)
                continue;

            var slotId = GetFirstNonEmptyString(item, "slotId");
            if (string.IsNullOrWhiteSpace(slotId))
                continue;

            if (!seen.Add(slotId.Trim()))
            {
                duplicateSlotId = slotId.Trim();
                return true;
            }
        }

        return false;
    }


    private static string BuildGuardianTradeSlotSignature(JsonElement item)
    {
        var slotId = GetFirstNonEmptyString(item, "slotId") ?? "";
        var domainTag = GetFirstNonEmptyString(item, "domainTag") ?? "";
        var price = item.TryGetProperty("priceInFeathers", out var priceNode) && priceNode.ValueKind == JsonValueKind.Number
            ? priceNode.GetInt32().ToString()
            : "";
        var soldOut = item.TryGetProperty("soldOut", out var soldOutNode) &&
                      (soldOutNode.ValueKind == JsonValueKind.True || soldOutNode.ValueKind == JsonValueKind.False)
            ? soldOutNode.GetBoolean().ToString()
            : "";

        if (!item.TryGetProperty("relicData", out var relicData) || relicData.ValueKind != JsonValueKind.Object)
            return $"{slotId}|{domainTag}|{price}|{soldOut}|";

        return $"{slotId}|{domainTag}|{price}|{soldOut}|{BuildCanonicalJsonSignature(relicData)}";
    }


    private static string BuildCanonicalJsonSignature(JsonElement element)
    {
        return element.ValueKind switch
        {
            JsonValueKind.Object => "{" + string.Join(",", element.EnumerateObject()
                .OrderBy(p => p.Name, StringComparer.Ordinal)
                .Select(p => $"{p.Name}:{BuildCanonicalJsonSignature(p.Value)}")) + "}",
            JsonValueKind.Array => "[" + string.Join(",", element.EnumerateArray().Select(BuildCanonicalJsonSignature)) + "]",
            JsonValueKind.String => JsonSerializer.Serialize(element.GetString() ?? string.Empty),
            JsonValueKind.Number => element.ToString(),
            JsonValueKind.True => "true",
            JsonValueKind.False => "false",
            JsonValueKind.Null => "null",
            _ => element.GetRawText()
        };
    }


    private static bool TryReadIntField(JsonElement root, string propertyName, out int value)
    {
        value = 0;
        return root.TryGetProperty(propertyName, out var prop) &&
               prop.ValueKind == JsonValueKind.Number &&
               prop.TryGetInt32(out value);
    }

}
