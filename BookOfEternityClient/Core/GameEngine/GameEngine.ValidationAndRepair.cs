using System.Diagnostics;
using System.Text;
using System.Text.RegularExpressions;
using System.Text.Json;
using System.Text.Json.Nodes;
using System.Text.Json.Serialization;
using BookOfEternityClient.AgentConsole;
using BookOfEternityClient.Configuration;
using BookOfEternityClient.Models;
using BookOfEternityClient.Services;
using BookOfEternityClient.Services.GmWorkers;
using BookOfEternityClient.UI;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Abstractions;
using Spectre.Console;

namespace BookOfEternityClient.Core;

public partial class GameEngine
{
    private const string GmBridgeIdleWithoutTerminalSignalHarnessSource = "gm_bridge_idle_without_terminal_signal";
    private const string GmOutputWithoutTerminalSignalHarnessSource = "gm_output_without_terminal_signal";
    private const string ClientRecoveredMissingTerminalSignalHarnessSource = "client_recovered_gm_output_without_terminal_signal";
    private static readonly string[] RecoverableGmOutputRequiredFiles =
    [
        "output/narrative_response.json",
        "output/debug_logs.json"
    ];

    private async Task EnsureClientOwnedSystemFilesHealthyAsync()
    {
        await CleanupOrphanedTurnRequestBeforeValidationAsync();
        await _stateManager.RefreshGameStateAsync();
        var preserveControlFilesForTerminalValidation =
            await ShouldPreserveClientOwnedControlFilesForTerminalValidationAsync();

        if (await _systemModService.WriteManifestForGmAsync())
            await _stateManager.SaveSettingsAsync();

        await AfterlifeNotificationState.EnsureHealthyAsync(_fs);
        if (!preserveControlFilesForTerminalValidation)
        {
            await _afterlifeReturnGuardService.EnsureHealthyAsync(_stateManager.CurrentState.CurrentRealm);
            await _systemGuardianLibraryService.EnsureAttractionRequestHealthyAsync(_stateManager.CurrentState.CurrentRealm);
        }

        await _qteSceneService.EnsureRuntimeStateHealthyAsync();
        await _progressionSchedule.EnsureInitializedAsync();
    }

    private async Task CleanupOrphanedTurnRequestBeforeValidationAsync()
    {
        if (!_fs.FileExists("input/turn_request.json"))
            return;

        if (HasTerminalReadySignal() ||
            _fs.FileExists(ValidationRepairRequestPath) ||
            _fs.FileExists(ValidationRepairReadyPath))
            return;

        var pendingSnapshot = await ResolveActivePendingTurnSnapshotContextAsync();
        if (pendingSnapshot.Status == PendingTurnSnapshotResolutionStatus.Usable)
            return;

        _logger.LogWarning(
            pendingSnapshot.Status == PendingTurnSnapshotResolutionStatus.Missing
                ? "Найден orphaned input/turn_request.json без pending snapshot manifest перед validation. Удаление как stale runtime artifact."
                : "Найден input/turn_request.json с unreadable/invalid validated pending snapshot authority перед validation. Удаление как stale runtime artifact.");
        _fs.DeleteFile("input/turn_request.json");
    }

    private async Task<bool> ValidateCurrentGameStateOrShowErrorsAsync(string source,
        RollbackSnapshot? rollbackSnapshot = null,
        ProgressionControl? progressionControl = null,
        bool allowRepairLoop = false,
        DateTime? initialCanonicalRepairBoundaryUtc = null,
        IReadOnlyCollection<ValidationIssue>? initialCanonicalRepairErrors = null,
        DateTime? initialCanonicalRepairStartedAtUtc = null)
    {
        var repairAttempt = 0;
        List<ValidationIssue>? lastRepairErrors = null;
        IReadOnlyCollection<ValidationIssue>? outputFreshnessRepairErrors = initialCanonicalRepairErrors;
        var lastRepairAttempt = 0;
        var lastCanonicalRepairBoundaryUtc = initialCanonicalRepairBoundaryUtc;
        DateTime? lastCanonicalRepairStartedAtUtc = initialCanonicalRepairStartedAtUtc;
        string? lastRepairSessionGeneration = null;

        while (true)
        {
            await EnsureClientOwnedSystemFilesHealthyAsync();
            var issues = await _validator.ValidateGameStateAsync();
            if (RequiresAcceptedTurnPayloadValidation(source))
            {
                if (RequiresFreshNarrativePayload(source))
                    issues.AddRange(await _validator.ValidateAcceptedTurnNarrativePayloadAsync());
                issues.AddRange(await _validator.ValidateAcceptedTurnInterfacePayloadAsync());
                issues.AddRange(await _validator.ValidateAcceptedTurnReasoningAsync());
                issues.AddRange(await _validator.ValidateAcceptedTurnSpecialActionOutcomesAsync());
                issues.AddRange(await _validator.ValidateAcceptedTurnQteOfferAsync());
                issues.AddRange(await _validator.ValidateAcceptedTurnMortalCombatMaterializationAsync());
                issues.AddRange(await _validator.ValidateAcceptedTurnMortalLevelUpMaterializationAsync());
            }
            if (allowRepairLoop &&
                outputFreshnessRepairErrors is { Count: > 0 } &&
                lastCanonicalRepairStartedAtUtc.HasValue)
            {
                lastCanonicalRepairBoundaryUtc = ResolveCanonicalRepairOutputFreshnessBoundaryUtc(
                    outputFreshnessRepairErrors,
                    lastCanonicalRepairStartedAtUtc.Value,
                    lastCanonicalRepairBoundaryUtc);
            }
            if (allowRepairLoop &&
                outputFreshnessRepairErrors is { Count: > 0 } &&
                lastCanonicalRepairBoundaryUtc.HasValue)
            {
                issues.AddRange(CollectPlayerFacingOutputStaleAfterCanonicalRepairIssues(
                    outputFreshnessRepairErrors,
                    lastCanonicalRepairBoundaryUtc.Value));
            }
            issues.AddRange(await _validator.ValidatePendingMemoryLegacyApplicationAsync());
            if (progressionControl != null)
                issues.AddRange(await _progressionSchedule.ValidateAcceptedTurnOutcomeAsync(progressionControl));
            var errors = PrioritizeValidationErrors(issues.Where(i => i.Severity == IssueSeverity.Error)).ToList();

            if (allowRepairLoop)
                errors = await FilterRestoredForbiddenRealmBaselineErrorsAsync(source, errors);

            if (errors.Count == 0)
            {
                if (allowRepairLoop && lastRepairErrors is { Count: > 0 })
                {
                    await AppendClearedValidationRepairTrajectoryAsync(
                        source,
                        lastRepairErrors,
                        lastRepairAttempt,
                        lastRepairSessionGeneration!);
                    await DeleteValidationRepairFilesForSessionAsync(lastRepairSessionGeneration!);
                }
                else
                {
                    await DeleteValidationRepairFilesAsync();
                }
                if (progressionControl != null)
                    await _progressionSchedule.ApplyAcceptedTurnOutcomeAsync(progressionControl);
                return true;
            }

            if (allowRepairLoop && await TryAutoRollbackRealmSegregationViolationsAsync(source, errors))
            {
                await RefreshRuntimeStateAsync();
                continue;
            }

            if (allowRepairLoop && await TryAutoRepairStartupGuardianDirectMaterializationAsync(source, errors))
            {
                await RefreshRuntimeStateAsync();
                continue;
            }

            _logger.LogError("Нарушение контракта состояния после {Source}: {Count} ошибок", source, errors.Count);

            if (!allowRepairLoop)
            {
                if (HasRollbackCapability(rollbackSnapshot))
                {
                    await RestorePreTurnBackup(rollbackSnapshot!);
                    CleanupBackup(rollbackSnapshot!);
                }

                await _progressionSchedule.DeleteTransientReportAsync();
                ShowContractValidationErrors(source, errors);
                return false;
            }

            repairAttempt++;
            lastRepairErrors = errors;
            lastRepairAttempt = repairAttempt;
            lastRepairSessionGeneration = await CaptureCurrentSessionGenerationAsync();
            var repairStartedAtUtc = DateTime.UtcNow;
            if (!await WaitForContractRepairAsync(
                    source,
                    errors,
                    repairAttempt,
                    rollbackSnapshot,
                    lastRepairSessionGeneration))
                return false;
            var canonicalRepairErrors = errors
                .Where(issue => IsCanonicalStateRepairIssue(issue) &&
                                !IsPlayerFacingNeutralActorMemoryRepairIssue(issue))
                .ToArray();
            if (canonicalRepairErrors.Length > 0)
            {
                outputFreshnessRepairErrors = MergeCanonicalRepairErrors(
                    outputFreshnessRepairErrors,
                    canonicalRepairErrors);
                lastCanonicalRepairStartedAtUtc = repairStartedAtUtc;
            }

            if (outputFreshnessRepairErrors is { Count: > 0 } &&
                lastCanonicalRepairStartedAtUtc.HasValue)
            {
                var unobservableFallbackUtc = canonicalRepairErrors.Length > 0
                    ? DateTime.UtcNow
                    : lastCanonicalRepairBoundaryUtc;
                lastCanonicalRepairBoundaryUtc = ResolveCanonicalRepairOutputFreshnessBoundaryUtc(
                    outputFreshnessRepairErrors,
                    lastCanonicalRepairStartedAtUtc.Value,
                    unobservableFallbackUtc);
            }
        }
    }

    private async Task<bool> ValidateAcceptedTurnOutcomeWithRepairLoopAsync(
        string source,
        ValidatedPendingTurnSnapshotContext? activeSnapshotContext,
        RollbackSnapshot? rollbackSnapshot,
        int expectedTurn,
        ProgressionControl? progressionControl)
    {
        var criticalRepairAttempt = 0;
        List<ValidationIssue>? lastCriticalRepairErrors = null;
        var lastCriticalRepairAttempt = 0;
        DateTime? lastCriticalRepairBoundaryUtc = null;
        DateTime? lastCriticalRepairStartedAtUtc = null;
        string? lastCriticalRepairSessionGeneration = null;
        using var pendingSnapshotScope = _validator.UsePrevalidatedPendingTurnSnapshotScope(activeSnapshotContext?.Manifest);

        while (true)
        {
            await EnsureClientOwnedSystemFilesHealthyAsync();
            var rawIssues = await CollectAcceptedTurnRawStateIssuesAsync();
            var rawErrors = PrioritizeValidationErrors(rawIssues.Where(i => i.Severity == IssueSeverity.Error)).ToList();
            if (rawErrors.Count > 0)
            {
                criticalRepairAttempt++;
                _logger.LogError(
                    "Critical accepted-turn raw state corruption after {Source}: {Count} errors",
                    source,
                    rawErrors.Count);

                lastCriticalRepairErrors = rawErrors;
                lastCriticalRepairAttempt = criticalRepairAttempt;
                lastCriticalRepairSessionGeneration = await CaptureCurrentSessionGenerationAsync();
                var rawRepairStartedAtUtc = DateTime.UtcNow;
                if (!await WaitForContractRepairAsync(
                        source,
                        rawErrors,
                        criticalRepairAttempt,
                        rollbackSnapshot,
                        lastCriticalRepairSessionGeneration))
                    return false;
                lastCriticalRepairStartedAtUtc = rawRepairStartedAtUtc;
                lastCriticalRepairBoundaryUtc = ResolveCanonicalRepairOutputFreshnessBoundaryUtc(
                    rawErrors,
                    rawRepairStartedAtUtc);

                continue;
            }

            if (!await RefreshAcceptedTurnCanonicalStateForValidationAsync(expectedTurn, activeSnapshotContext))
            {
                criticalRepairAttempt++;
                var baselineErrors = new List<ValidationIssue>
                {
                    new(
                        "game_state/control/pending_turn_snapshot.json",
                        IssueSeverity.Error,
                        "Accepted-turn canonical materialization requires a readable validated pending-turn snapshot baseline.",
                        code: "accepted_turn_invalid_snapshot_baseline",
                        section: "AcceptedTurnCanonicalState",
                        expected: "usable current pending turn snapshot manifest with detached authority and hash-validated canonical baseline files",
                        actual: "validated snapshot baseline is missing, detached-authority-invalid, modified, structurally invalid, or mismatched to the active request context",
                        repairHint: "Восстанови current pending_turn_snapshot.json, detached snapshot authority и canonical snapshot files без tampering; accepted-turn canonical validation должна читать pre-turn baseline только из validated snapshot authority.")
                };

                _logger.LogError(
                    "Critical accepted-turn canonical baseline authority failure after {Source}: {Count} errors",
                    source,
                    baselineErrors.Count);

                lastCriticalRepairErrors = baselineErrors;
                lastCriticalRepairAttempt = criticalRepairAttempt;
                lastCriticalRepairSessionGeneration = await CaptureCurrentSessionGenerationAsync();
                var baselineRepairStartedAtUtc = DateTime.UtcNow;
                if (!await WaitForContractRepairAsync(
                        source,
                        baselineErrors,
                        criticalRepairAttempt,
                        rollbackSnapshot,
                        lastCriticalRepairSessionGeneration))
                    return false;
                lastCriticalRepairStartedAtUtc = baselineRepairStartedAtUtc;
                lastCriticalRepairBoundaryUtc = ResolveCanonicalRepairOutputFreshnessBoundaryUtc(
                    baselineErrors,
                    baselineRepairStartedAtUtc);

                continue;
            }

            await EnsureClientOwnedSystemFilesHealthyAsync();
            var canonicalIssues = await _criticalStateHealth.ValidateCriticalCanonicalStateAsync();
            var canonicalErrors = PrioritizeValidationErrors(canonicalIssues.Where(i => i.Severity == IssueSeverity.Error)).ToList();
            if (canonicalErrors.Count > 0)
            {
                criticalRepairAttempt++;
                _logger.LogError(
                    "Critical accepted-turn canonical state corruption after {Source}: {Count} errors",
                    source,
                    canonicalErrors.Count);

                lastCriticalRepairErrors = canonicalErrors;
                lastCriticalRepairAttempt = criticalRepairAttempt;
                lastCriticalRepairSessionGeneration = await CaptureCurrentSessionGenerationAsync();
                var canonicalRepairStartedAtUtc = DateTime.UtcNow;
                if (!await WaitForContractRepairAsync(
                        source,
                        canonicalErrors,
                        criticalRepairAttempt,
                        rollbackSnapshot,
                        lastCriticalRepairSessionGeneration))
                    return false;
                lastCriticalRepairStartedAtUtc = canonicalRepairStartedAtUtc;
                lastCriticalRepairBoundaryUtc = ResolveCanonicalRepairOutputFreshnessBoundaryUtc(
                    canonicalErrors,
                    canonicalRepairStartedAtUtc);

                continue;
            }

            if (lastCriticalRepairErrors is { Count: > 0 } &&
                lastCriticalRepairStartedAtUtc.HasValue)
            {
                lastCriticalRepairBoundaryUtc = ResolveCanonicalRepairOutputFreshnessBoundaryUtc(
                    lastCriticalRepairErrors,
                    lastCriticalRepairStartedAtUtc.Value);
            }

            if (!await ValidateCurrentGameStateOrShowErrorsAsync(
                    source,
                    rollbackSnapshot,
                    progressionControl,
                    allowRepairLoop: true,
                    initialCanonicalRepairBoundaryUtc: lastCriticalRepairBoundaryUtc,
                    initialCanonicalRepairErrors: lastCriticalRepairErrors,
                    initialCanonicalRepairStartedAtUtc: lastCriticalRepairStartedAtUtc))
                return false;

            if (lastCriticalRepairErrors is { Count: > 0 })
            {
                await AppendClearedValidationRepairTrajectoryAsync(
                    source,
                    lastCriticalRepairErrors,
                    lastCriticalRepairAttempt,
                    lastCriticalRepairSessionGeneration!);
                await CleanupAcceptedTurnCommandSurfacesAsync(lastCriticalRepairSessionGeneration);
            }
            else
            {
                await CleanupAcceptedTurnCommandSurfacesAsync();
            }
            await RefreshRuntimeStateAsync();
            return true;
        }
    }

    private async Task<List<ValidationIssue>> CollectAcceptedTurnRawStateIssuesAsync()
    {
        var issues = await _criticalStateHealth.ValidateAcceptedTurnRawStateAsync();
        issues.AddRange(await _validator.ValidateNpcCoreChangesBeforeNormalizationAsync());
        issues.AddRange(await _validator.ValidateFactionCoreChangesBeforeNormalizationAsync());
        issues.AddRange(await _validator.ValidateAcceptedTurnRawFactionMaterializationAsync());
        issues.AddRange(await _validator.ValidateAcceptedTurnRawMortalItemMaterializationAsync());
        return issues;
    }

    private async Task<bool> ValidatePostAcceptedMaterializedStateWithRepairLoopAsync(
        RollbackSnapshot? rollbackSnapshot)
    {
        var accepted = await ValidateCurrentGameStateOrShowErrorsAsync(
            PostAcceptedMaterializedStateValidationSource,
            rollbackSnapshot,
            progressionControl: null,
            allowRepairLoop: true);

        if (accepted)
            await RefreshRuntimeStateAsync();

        return accepted;
    }

    private async Task<bool> RefreshAcceptedTurnCanonicalStateForValidationAsync(
        int expectedTurn,
        ValidatedPendingTurnSnapshotContext? activeSnapshotContext)
    {
        var snapshot = await LoadCanonicalBaselineSnapshotAsync(expectedTurn, activeSnapshotContext);
        if (snapshot == null)
            return false;

        await RefreshCanonicalStateAsync(snapshot);
        return true;
    }

    private async Task CleanupAcceptedTurnCommandSurfacesAsync(string? expectedSessionGeneration = null)
    {
        await RemoveGuardianQuestProgressUpdatesCommandSurfaceAsync(expectedSessionGeneration);
    }

    private async Task RemoveGuardianQuestProgressUpdatesCommandSurfaceAsync(
        string? expectedSessionGeneration = null)
    {
        const string path = "game_state/meta/guardians.json";
        await using var writeLease = await _fs.AcquireCanonicalWriteLeaseAsync();
        if (!string.IsNullOrWhiteSpace(expectedSessionGeneration))
            ThrowIfRepairSessionReplaced(writeLease, expectedSessionGeneration);

        var json = await _fs.ReadFileAsync(writeLease, path);
        if (string.IsNullOrWhiteSpace(json))
            return;

        try
        {
            if (JsonNode.Parse(json) is not JsonObject root ||
                !root.Remove(GuardianProjectState.QuestProgressUpdatesProperty))
            {
                return;
            }

            await _fs.WriteFileAtomicAsync(writeLease, path, root.ToJsonString(JsonOpts));
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Failed to remove accepted-turn Guardian quest progress command surface.");
        }
    }

    private static bool RequiresFreshNarrativePayload(string source)
    {
        return source is "ответа GM" or "late response GM" or "обработки хода" or "оценки жизни";
    }

    private List<ValidationIssue> CollectPlayerFacingOutputStaleAfterCanonicalRepairIssues(
        IReadOnlyCollection<ValidationIssue> repairedErrors,
        DateTime canonicalRepairBoundaryUtc)
    {
        var canonicalRepairCodes = repairedErrors
            .Where(issue => IsCanonicalStateRepairIssue(issue) &&
                            !IsPlayerFacingNeutralActorMemoryRepairIssue(issue))
            .Select(issue => string.IsNullOrWhiteSpace(issue.Code) ? issue.Category.ToString() : issue.Code!)
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .OrderBy(code => code, StringComparer.OrdinalIgnoreCase)
            .ToArray();
        if (canonicalRepairCodes.Length == 0)
            return [];

        var issues = new List<ValidationIssue>();
        foreach (var outputPath in new[]
                 {
                     "output/narrative_response.json",
                     "output/interface_updates.json"
                 })
        {
            var outputFullPath = _fs.ResolvePath(outputPath);
            if (!File.Exists(outputFullPath))
                continue;

            var outputWrittenAtUtc = File.GetLastWriteTimeUtc(outputFullPath);
            if (outputWrittenAtUtc > canonicalRepairBoundaryUtc)
                continue;

            issues.Add(new ValidationIssue(
                outputPath,
                IssueSeverity.Error,
                $"{outputPath} был записан до canonical validation repair и может противоречить исправленному состоянию.",
                code: "accepted_turn_stale_player_facing_output_after_canonical_repair",
                section: "PlayerFacingOutput",
                expected: $"player-facing output rewritten after repaired canonical state was last written at {canonicalRepairBoundaryUtc:o}",
                actual: $"{outputPath} last write {outputWrittenAtUtc:o}; repaired canonical issues: {string.Join(", ", canonicalRepairCodes)}",
                repairHint: "Перепиши player-facing output под уже исправленное canonical state: обнови output/narrative_response.json.response и, если есть варианты выбора, output/interface_updates.json.dialogueOptions. Не меняй canonical state повторно, если validation_repair_request.json не перечисляет новые canonical ошибки."));
        }

        return issues;
    }

    private DateTime ResolveCanonicalRepairOutputFreshnessBoundaryUtc(
        IReadOnlyCollection<ValidationIssue> repairedErrors,
        DateTime repairStartedAtUtc,
        DateTime? unobservableFallbackUtc = null)
    {
        var canonicalTargetPaths = repairedErrors
            .Where(issue => IsCanonicalStateRepairIssue(issue) &&
                            !IsPlayerFacingNeutralActorMemoryRepairIssue(issue))
            .Select(issue => NormalizeCanonicalRepairTargetFilePath(issue.FilePath))
            .Where(PendingTurnSnapshotAuthority.IsSafeRelativePath)
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .ToArray();

        if (canonicalTargetPaths.Length == 0)
            return unobservableFallbackUtc ?? DateTime.UtcNow;

        var latestWriteUtc = DateTime.MinValue;
        var hasUnobservableTarget = false;
        foreach (var targetPath in canonicalTargetPaths)
        {
            try
            {
                var targetFullPath = _fs.ResolvePath(targetPath);
                if (!File.Exists(targetFullPath))
                {
                    hasUnobservableTarget = true;
                    continue;
                }

                var targetWriteUtc = File.GetLastWriteTimeUtc(targetFullPath);
                if (targetWriteUtc < repairStartedAtUtc)
                {
                    hasUnobservableTarget = true;
                    continue;
                }

                if (targetWriteUtc > latestWriteUtc)
                    latestWriteUtc = targetWriteUtc;
            }
            catch (Exception ex)
            {
                _logger.LogWarning(
                    ex,
                    "Не удалось определить фактическую границу записи repaired canonical target {TargetPath}; freshness validation будет fail-closed.",
                    targetPath);
                hasUnobservableTarget = true;
            }
        }

        var fallbackUtc = unobservableFallbackUtc ?? DateTime.UtcNow;
        if (hasUnobservableTarget)
            return latestWriteUtc > fallbackUtc ? latestWriteUtc : fallbackUtc;
        return latestWriteUtc == DateTime.MinValue ? fallbackUtc : latestWriteUtc;
    }

    private static IReadOnlyCollection<ValidationIssue> MergeCanonicalRepairErrors(
        IReadOnlyCollection<ValidationIssue>? retainedErrors,
        IReadOnlyCollection<ValidationIssue> currentErrors)
    {
        return (retainedErrors ?? [])
            .Concat(currentErrors)
            .GroupBy(
                issue =>
                    $"{NormalizeCanonicalRepairTargetFilePath(issue.FilePath)}\u001f{issue.Code ?? issue.Category.ToString()}",
                StringComparer.OrdinalIgnoreCase)
            .Select(group => group.Last())
            .ToArray();
    }

    private static string NormalizeCanonicalRepairTargetFilePath(string path)
    {
        var normalized = NormalizeRepairTargetPath(path).Replace('\\', '/');
        var jsonExtensionIndex = normalized.IndexOf(".json", StringComparison.OrdinalIgnoreCase);
        return jsonExtensionIndex < 0
            ? normalized
            : normalized[..(jsonExtensionIndex + ".json".Length)];
    }

    private static bool IsCanonicalStateRepairIssue(ValidationIssue issue)
    {
        var path = NormalizeRepairTargetPath(issue.FilePath);
        return path.StartsWith("game_state/", StringComparison.OrdinalIgnoreCase) ||
               path.StartsWith("lore/", StringComparison.OrdinalIgnoreCase);
    }

    private static bool IsPlayerFacingNeutralActorMemoryRepairIssue(ValidationIssue issue)
    {
        if (IsGuardianThoughtJournalShapeRepairIssue(issue))
            return true;

        return (issue.Code ?? string.Empty).ToLowerInvariant() switch
        {
            "guardian_relevant_actor_missing_thought_journal_delta" => true,
            "mortal_npc_relevant_actor_missing_thought_journal_delta" => true,
            "afterlife_resident_relevant_actor_missing_thought_journal_delta" => true,
            "afterlife_entity_relevant_actor_missing_memory_ledger_delta" => true,
            "shining_faction_relevant_actor_missing_strategic_memory_delta" => true,
            "actor_thought_journal_not_first_person" => true,
            _ => false
        };
    }

    private async Task<bool> TryAutoRollbackRealmSegregationViolationsAsync(
        string source,
        IReadOnlyCollection<ValidationIssue> errors)
    {
        var realmSegregationErrors = errors
            .Where(error => string.Equals(error.Code, "realm_segregation_violation", StringComparison.OrdinalIgnoreCase))
            .ToList();
        if (realmSegregationErrors.Count == 0)
            return false;

        var forbiddenPaths = realmSegregationErrors
            .SelectMany(ExtractRealmSegregationPaths)
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .ToList();
        if (forbiddenPaths.Count == 0)
            return false;

        var sourceRealm = realmSegregationErrors
            .Select(ExtractRealmSegregationSourceRealm)
            .FirstOrDefault(realm => !string.IsNullOrWhiteSpace(realm));

        var rollbackService = new RealmSegregationAutoRollbackService(
            _fs,
            NullLogger<RealmSegregationAutoRollbackService>.Instance);
        var result = await rollbackService.TryRollbackForbiddenRealmMutationsAsync(
            sourceRealm,
            forbiddenPaths,
            source);

        if (!result.RolledBack)
            return false;

        _logger.LogWarning(
            "Client auto-rolled back {Count} forbidden realm mutations after {Source}; report: {ReportPath}",
            result.Actions.Count,
            source,
            result.ReportPath);
        return true;
    }

    private async Task<bool> TryAutoRepairStartupGuardianDirectMaterializationAsync(
        string source,
        IReadOnlyCollection<ValidationIssue> errors)
    {
        if (!errors.Any(IsGuardianPendingCreationMaterializationRepairIssue))
            return false;

        const string path = "game_state/meta/guardians.json";
        var json = await _fs.ReadFileAsync(path);
        if (string.IsNullOrWhiteSpace(json))
            return false;

        JsonObject root;
        try
        {
            if (JsonNode.Parse(json) is not JsonObject parsedRoot)
                return false;

            root = parsedRoot;
        }
        catch
        {
            return false;
        }

        if (root["UpdateGuardians"] is JsonArray existingUpdates && existingUpdates.Count > 0)
            return false;

        if (root["pendingGuardianCreation"] is not JsonObject pendingCreation ||
            !string.Equals(GetNodeString(pendingCreation["mode"]), "freeform", StringComparison.OrdinalIgnoreCase))
        {
            return false;
        }

        if (root["guardians"] is not JsonArray guardians)
            return false;

        var materializedGuardians = guardians.OfType<JsonObject>().ToList();
        if (materializedGuardians.Count != 1)
            return false;

        var guardian = materializedGuardians[0];
        if (!IsAutoRepairableStartupGuardianCreateCandidate(guardian))
            return false;

        var createData = CloneJsonObjectNode(guardian);
        NormalizeStartupGuardianCreateCandidate(createData);
        var guardianId = GetNodeString(createData["guardianId"]);
        root["UpdateGuardians"] = new JsonArray
        {
            new JsonObject
            {
                ["command"] = "create",
                ["data"] = createData.DeepClone()
            }
        };
        root["guardians"] = new JsonArray(createData.DeepClone());
        root["activeGuardian"] = createData.DeepClone();

        if (root["chaosSeaNavigation"] is JsonObject navigation)
        {
            navigation["currentGuardianId"] = guardianId;
            if (createData["abode"] is JsonObject abode)
            {
                var abodeId = GetNodeString(abode["abodeId"]);
                if (!string.IsNullOrWhiteSpace(abodeId))
                {
                    navigation["currentAbodeId"] = abodeId;
                    if (navigation["discoveredAbodes"] is not JsonArray discoveredAbodes ||
                        !discoveredAbodes.OfType<JsonValue>().Any(value =>
                            value.TryGetValue<string>(out var discovered) &&
                            string.Equals(discovered, abodeId, StringComparison.OrdinalIgnoreCase)))
                    {
                        navigation["discoveredAbodes"] = new JsonArray(abodeId);
                    }
                }
            }
        }

        root.Remove("pendingGuardianCreation");
        await _fs.WriteFileAtomicAsync(path, root.ToJsonString(JsonOpts));
        _logger.LogWarning(
            "Auto-repaired startup Guardian direct materialization after {Source}: synthesized UpdateGuardians.create for {GuardianId} and cleared pendingGuardianCreation.",
            source,
            guardianId);
        return true;
    }

    private static bool IsAutoRepairableStartupGuardianCreateCandidate(JsonObject guardian)
    {
        if (string.IsNullOrWhiteSpace(GetNodeString(guardian["guardianId"])) ||
            string.IsNullOrWhiteSpace(GetNodeString(guardian["canonicalName"])) ||
            guardian["manifestation"] is not JsonObject ||
            guardian["abode"] is not JsonObject ||
            guardian["personalityProfile"] is not JsonObject ||
            guardian["relationshipData"] is not JsonObject ||
            guardian["abodePower"] is not JsonObject ||
            guardian["questManagement"] is not JsonObject ||
            guardian["gachaSystem"] is not JsonObject ||
            guardian["mood"] is not JsonObject ||
            guardian["loreFragments"] is not JsonArray loreFragments ||
            loreFragments.Count < 7 ||
            guardian["musings"] is not JsonArray musings ||
            musings.Count == 0)
        {
            return false;
        }

        return true;
    }

    private static JsonObject CloneJsonObjectNode(JsonObject source)
    {
        return JsonNode.Parse(source.ToJsonString(JsonOpts))!.AsObject();
    }

    private static void NormalizeStartupGuardianCreateCandidate(JsonObject guardian)
    {
        if (guardian["relationshipData"] is JsonObject relationshipData)
        {
            if (relationshipData.TryGetPropertyValue("guardianRoleToPlayer", out var roleNode) &&
                !string.Equals(GetNodeString(roleNode), PlayerGuardianFoundationState.GuardianRoleFormerPatron, StringComparison.OrdinalIgnoreCase))
            {
                relationshipData.Remove("guardianRoleToPlayer");
            }

            var lastInteraction = GetNodeString(relationshipData["lastInteraction"]);
            if (!string.IsNullOrWhiteSpace(lastInteraction) &&
                !DateTimeOffset.TryParse(lastInteraction, out _))
            {
                relationshipData["lastInteraction"] = null;
            }
        }

        if (guardian["abodePower"] is JsonObject)
            AbodePowerRules.EnsureCanonicalState(guardian);
        GuardianGachaChargeRules.NormalizeGuardianGachaState(guardian);
        GuardianTradeRequestState.NormalizeGuardianTradeReceiptsShape(guardian);

        if (guardian["musings"] is JsonArray musings)
        {
            foreach (var musing in musings.OfType<JsonObject>())
            {
                var topic = GetNodeString(musing["topic"]);
                if (!IsAllowedStartupGuardianMusingTopic(topic))
                    musing["topic"] = "soul_assessment";

                var mood = GetNodeString(musing["mood"]);
                if (!IsAllowedStartupGuardianMusingMood(mood))
                    musing["mood"] = "contemplative";

                if (string.IsNullOrWhiteSpace(GetNodeString(musing["thought"])) &&
                    string.IsNullOrWhiteSpace(GetNodeString(musing["text"])))
                {
                    musing["thought"] = "Хранитель оценивает новую душу и условия первой встречи.";
                }
            }
        }
    }

    private static bool IsAllowedStartupGuardianMusingTopic(string topic)
    {
        return topic is "soul_assessment" or "domain_insight" or "guardian_politics" or "chaos_sea" or "personal_reflection" or "quest_planning";
    }

    private static bool IsAllowedStartupGuardianMusingMood(string mood)
    {
        return mood is "content" or "intrigued" or "concerned" or "amused" or "proud" or "disappointed" or "wary" or "nostalgic" or "determined" or "melancholic" or "excited" or "contemplative" or "irritated" or "hopeful";
    }

    private async Task<List<ValidationIssue>> FilterRestoredForbiddenRealmBaselineErrorsAsync(
        string source,
        List<ValidationIssue> errors)
    {
        if (errors.Count == 0)
            return errors;

        var rollbackService = new RealmSegregationAutoRollbackService(
            _fs,
            NullLogger<RealmSegregationAutoRollbackService>.Instance);
        var result = await rollbackService.FilterRestoredForbiddenBaselineIssuesAsync(
            _stateManager.CurrentState.CurrentRealm,
            errors);

        if (result.SuppressedIssues.Count == 0)
            return errors;

        _logger.LogWarning(
            "Suppressed {Count} restored forbidden-realm baseline validation errors after {Source}; these files match the validated pending-turn snapshot and remain outside GM repair scope for the current realm.",
            result.SuppressedIssues.Count,
            source);

        return PrioritizeValidationErrors(result.RemainingIssues).ToList();
    }

    private static IEnumerable<string> ExtractRealmSegregationPaths(ValidationIssue issue)
    {
        var actual = issue.Actual ?? string.Empty;
        if (string.IsNullOrWhiteSpace(actual))
            yield break;

        var pathList = actual.Split(" | surfaces:", 2, StringSplitOptions.None)[0];
        foreach (var candidate in pathList.Split(',', StringSplitOptions.TrimEntries | StringSplitOptions.RemoveEmptyEntries))
        {
            var normalized = candidate.Replace('\\', '/');
            if (normalized.StartsWith("game_state/", StringComparison.OrdinalIgnoreCase))
                yield return normalized;
        }
    }

    private static string? ExtractRealmSegregationSourceRealm(ValidationIssue issue)
    {
        var match = Regex.Match(
            issue.Message,
            @"pre-turn realm '([^']+)'",
            RegexOptions.IgnoreCase | RegexOptions.CultureInvariant);
        return match.Success ? match.Groups[1].Value : null;
    }

    private static bool RequiresAcceptedTurnPayloadValidation(string source)
    {
        return source is "ответа GM" or "late response GM" or "обработки хода" or "оценки жизни";
    }

    private void ShowContractValidationErrors(string source, List<ValidationIssue> errors)
    {
        var summaryLines = BuildValidationSummaryLines(errors, 5);
        var lines = new List<string>
        {
            $"[bold red]Нарушение контракта GM после {GameInterface.EscapeMarkup(source)}[/]",
            "[red]Клиент отклонил состояние как несовместимое с Rules/API.[/]",
            ""
        };

        if (summaryLines.Count > 0)
        {
            lines.Add("[bold yellow]Основные группы ошибок:[/]");
            foreach (var summary in summaryLines)
                lines.Add($"[yellow]• {GameInterface.EscapeMarkup(summary)}[/]");
            lines.Add("");
        }

        foreach (var issue in errors.Take(10))
        {
            var label = BuildIssueDisplayLabel(issue);
            lines.Add($"[red]• {GameInterface.EscapeMarkup(label)}[/]");
            if (!string.IsNullOrWhiteSpace(issue.RepairHint))
                lines.Add($"  [grey]Исправление:[/] {GameInterface.EscapeMarkup(issue.RepairHint)}");
        }

        if (errors.Count > 10)
            lines.Add($"[yellow]... и ещё {errors.Count - 10} ошибок[/]");

        AnsiConsole.Write(new Panel(GameInterface.SafeMarkup(string.Join("\n", lines)))
        {
            Header = new PanelHeader(" Contract Error ", Justify.Center),
            Border = BoxBorder.Double,
            BorderStyle = new Style(Color.Red),
            Padding = new Padding(2, 1),
            Expand = true
        });
        AnsiConsole.MarkupLine($"[grey]{_loc.T("press_any_key")}[/]");

        if (_inputSource is AgentConsoleLiveInputSource liveInput)
        {
            var plainLines = new List<string>
            {
                $"Нарушение контракта GM после {source}",
                "Клиент отклонил состояние как несовместимое с Rules/API.",
                ""
            };

            if (summaryLines.Count > 0)
            {
                plainLines.Add("Основные группы ошибок:");
                plainLines.AddRange(summaryLines.Select(summary => "• " + summary));
                plainLines.Add("");
            }

            foreach (var issue in errors.Take(10))
            {
                plainLines.Add("• " + BuildIssueDisplayLabel(issue));
                if (!string.IsNullOrWhiteSpace(issue.RepairHint))
                    plainLines.Add("  Исправление: " + issue.RepairHint);
            }

            if (errors.Count > 10)
                plainLines.Add($"... и ещё {errors.Count - 10} ошибок");

            plainLines.Add("");
            plainLines.Add(_loc.T("press_any_key"));

            liveInput.PublishSnapshot(new AgentConsoleSnapshot
            {
                ScreenId = "contract-validation-error",
                Mode = AgentConsoleMode.Error,
                Title = "Нарушение контракта GM",
                PlainText = string.Join(Environment.NewLine, plainLines),
                AwaitingInput = true,
                InputKind = AgentConsoleInputKind.Key,
                Actions =
                [
                    new AgentConsoleAction
                    {
                        Id = "continue",
                        Label = "Продолжить",
                        Shortcut = "enter",
                        IsDefault = true
                    }
                ],
                RenderedAtUtc = DateTimeOffset.UtcNow,
                UpdatedAtUtc = DateTimeOffset.UtcNow,
                Diagnostics =
                [
                    new AgentConsoleDiagnostic
                    {
                        Severity = AgentConsoleDiagnosticSeverity.Error,
                        Code = "contract-validation-error",
                        Message = $"Клиент отклонил состояние после {source}."
                    }
                ]
            }, "Rendered contract validation error.");
        }

        _inputSource.ReadKey(intercept: true);
    }

    private async Task<string> CaptureCurrentSessionGenerationAsync()
    {
        if (SessionOperationContext.TryGetExpectedGeneration(
                _fs.BasePath,
                out var boundSessionGeneration))
        {
            return boundSessionGeneration;
        }

        await using var writeLease = await _fs.AcquireCanonicalWriteLeaseAsync();
        return _fs.GetOrCreateSessionGeneration(writeLease);
    }

    private async Task<bool> WaitForContractRepairAsync(string source, List<ValidationIssue> errors,
        int attempt, RollbackSnapshot? rollbackSnapshot, string repairSessionGeneration)
    {
        await EnsureRepairSessionCurrentAsync(repairSessionGeneration);
        var dispatch = await WriteValidationRepairRequestForSessionAsync(
            source,
            errors,
            attempt,
            repairSessionGeneration);
        ThrowIfValidationRepairDispatchSessionReplaced(dispatch);
        if (dispatch.MetadataDiagnosticOnly)
            return await FailClosedDiagnosticOnlyValidationRepairAsync(
                source,
                errors,
                attempt,
                rollbackSnapshot,
                repairSessionGeneration);

        if (dispatch.WorkerApplyAccepted && !dispatch.ReadySignalCreated)
        {
            await AppendWorkerAcceptedValidationRepairTrajectoryAsync(source, errors, attempt, dispatch);
            return true;
        }

        using var agentConsoleRepairInputBlock = BeginAgentConsoleInputBlockFromCurrentSnapshot(
            "Validation repair is active. Agent Console input is blocked until GM finishes data repair.");

        var rollbackAvailable = HasRollbackCapability(rollbackSnapshot);
        while (true)
        {
            using var cts = new CancellationTokenSource();
            var startTime = DateTime.UtcNow;
            var harnessTerminalErrorPublished = false;

            var waitTask = Task.Run(async () =>
            {
                while (!cts.Token.IsCancellationRequested)
                {
                    await EnsureRepairSessionCurrentAsync(repairSessionGeneration);
                    if (_fs.FileExists(ValidationRepairReadyPath))
                        return true;
                    if (_fs.FileExists(ValidationRepairArtifactStallReportPath))
                    {
                        var pendingSnapshot = await ResolveActivePendingTurnSnapshotContextAsync();
                        if (pendingSnapshot is { Status: PendingTurnSnapshotResolutionStatus.Usable, Context: not null } &&
                            await TryPromoteValidationRepairArtifactStallToTerminalErrorAsync(
                                pendingSnapshot.Context,
                                repairSessionGeneration))
                        {
                            harnessTerminalErrorPublished = true;
                            return false;
                        }
                    }

                    await Task.Delay(500, cts.Token);
                }
                return false;
            }, cts.Token);

            var result = await AnsiConsole.Status()
                .Spinner(Spinner.Known.Dots12)
                .SpinnerStyle(Style.Parse("yellow"))
                .StartAsync(rollbackAvailable
                    ? "[yellow]⛏ GM исправляет невалидное состояние... (Escape = откатить изменения)[/]"
                    : "[yellow]⛏ GM исправляет невалидное состояние... (Escape = выйти из ожидания)[/]", async ctx =>
                {
                    while (!waitTask.IsCompleted && !cts.IsCancellationRequested)
                    {
                        var elapsed = (int)(DateTime.UtcNow - startTime).TotalSeconds;
                        ctx.Status(rollbackAvailable
                            ? $"[yellow]⛏ Ожидание исправления GM... попытка проверки #{attempt} ({elapsed}с) (Escape = откатить)[/]"
                            : $"[yellow]⛏ Ожидание исправления GM... попытка проверки #{attempt} ({elapsed}с) (Escape = выйти)[/]");

                        if (_inputSource.KeyAvailable)
                        {
                            var key = _inputSource.ReadKey(intercept: true);
                            if (key.Key == ConsoleKey.Escape)
                                cts.Cancel();
                        }

                        await Task.Delay(1000);
                    }

                    try { return await waitTask; }
                    catch (OperationCanceledException) { return false; }
                });

            if (cts.IsCancellationRequested)
            {
                if (rollbackAvailable)
                {
                    await RestorePreTurnBackupForSessionAsync(
                        rollbackSnapshot!,
                        repairSessionGeneration);
                    AnsiConsole.MarkupLine("[yellow]⏹ Ремонтный цикл прерван. Состояние откатилось к последней стабильной версии.[/]");
                }
                else
                {
                    AnsiConsole.MarkupLine("[yellow]⏹ Ремонтный цикл прерван. Автоматический откат для этого режима недоступен; текущее состояние оставлено как есть.[/]");
                }

                if (!await _progressionSchedule.DeleteTransientReportIfCurrentSessionAsync(
                        repairSessionGeneration))
                {
                    throw new GmWorkerSessionReplacedException(
                        "The validation-repair cycle belongs to a game session that is no longer current.");
                }
                await DeleteValidationRepairFilesForSessionAsync(repairSessionGeneration);
                return false;
            }

            if (harnessTerminalErrorPublished)
            {
                AnsiConsole.MarkupLine("[yellow]⏹ Ремонтный цикл остановлен harness: GM bridge завис во время исправления, создан terminal error для восстановления.[/]");
                return false;
            }

            if (!result)
                continue;

            var readyJson = await ReadValidationRepairFileForSessionAsync(
                ValidationRepairReadyPath,
                repairSessionGeneration);
            var ready = ReadValidationRepairReady(readyJson);
            var pendingSnapshot = await ResolveActivePendingTurnSnapshotContextAsync();
            if (ready == null)
            {
                _logger.LogWarning("Отклонён validation_repair_ready: файл не читается как валидный JSON");
                var rejectedReadyRepair = await ReportRejectedRepairReadyAsync(
                    source,
                    errors,
                    attempt,
                    "invalid_repair_ready_json",
                    "Клиент отклонил validation_repair_ready.json: файл не читается как валидный JSON.",
                    "Valid JSON object with matching sessionId/requestId/turnNumber for the active repair cycle",
                    string.IsNullOrWhiteSpace(readyJson) ? "missing or empty file" : TruncateDiagnosticValue(readyJson),
                    BuildInvalidRepairReadyRepairHint(pendingSnapshot),
                    repairSessionGeneration);
                ThrowIfValidationRepairDispatchSessionReplaced(rejectedReadyRepair.Dispatch);
                if (rejectedReadyRepair.Dispatch.MetadataDiagnosticOnly)
                    return await FailClosedDiagnosticOnlyValidationRepairAsync(
                        source,
                        rejectedReadyRepair.ReportErrors,
                        attempt,
                        rollbackSnapshot,
                        repairSessionGeneration);

                if (rejectedReadyRepair.Dispatch.WorkerApplyAccepted)
                {
                    if (!rejectedReadyRepair.Dispatch.ReadySignalCreated)
                    {
                        await AppendWorkerAcceptedValidationRepairTrajectoryAsync(
                            source,
                            rejectedReadyRepair.ReportErrors,
                            attempt,
                            rejectedReadyRepair.Dispatch);
                        return true;
                    }

                    continue;
                }

                await DeleteValidationRepairReadyForSessionAsync(repairSessionGeneration);
                AnsiConsole.MarkupLine("[yellow]⚠ Клиент запросил новую попытку исправления. GM продолжает корректировать данные.[/]");
                await Task.Delay(500);
                continue;
            }

            if (!IsMatchingRepairReady(ready, pendingSnapshot.Context))
            {
                _logger.LogWarning(
                    "Отклонён validation_repair_ready(session={Session}, request={Request}, turn={Turn}) — ожидается (session={ExpectedSession}, request={ExpectedRequest}, turn={ExpectedTurn})",
                    ready.SessionId,
                    ready.RequestId,
                    ready.TurnNumber,
                    pendingSnapshot.Context?.SessionId,
                    pendingSnapshot.Context?.RequestId,
                    pendingSnapshot.Context?.TurnNumber);

                var rejectedReadyRepair = await ReportRejectedRepairReadyAsync(
                    source,
                    errors,
                    attempt,
                    "mismatched_repair_ready_context",
                    "Клиент отклонил validation_repair_ready.json: metadata не совпадает с активным repair cycle.",
                    BuildExpectedRepairContext(pendingSnapshot),
                    BuildActualRepairContext(ready, pendingSnapshot),
                    BuildMismatchedRepairReadyRepairHint(pendingSnapshot),
                    repairSessionGeneration);
                ThrowIfValidationRepairDispatchSessionReplaced(rejectedReadyRepair.Dispatch);
                if (rejectedReadyRepair.Dispatch.MetadataDiagnosticOnly)
                    return await FailClosedDiagnosticOnlyValidationRepairAsync(
                        source,
                        rejectedReadyRepair.ReportErrors,
                        attempt,
                        rollbackSnapshot,
                        repairSessionGeneration);

                if (rejectedReadyRepair.Dispatch.WorkerApplyAccepted)
                {
                    if (!rejectedReadyRepair.Dispatch.ReadySignalCreated)
                    {
                        await AppendWorkerAcceptedValidationRepairTrajectoryAsync(
                            source,
                            rejectedReadyRepair.ReportErrors,
                            attempt,
                            rejectedReadyRepair.Dispatch);
                        return true;
                    }

                    continue;
                }

                await DeleteValidationRepairReadyForSessionAsync(repairSessionGeneration);
                AnsiConsole.MarkupLine("[yellow]⚠ Клиент запросил новую попытку исправления. GM продолжает корректировать данные.[/]");
                await Task.Delay(500);
                continue;
            }

            await AppendAcceptedValidationRepairTrajectoryAsync(
                source,
                errors,
                attempt,
                ready,
                pendingSnapshot,
                repairSessionGeneration);
            await DeleteValidationRepairReadyForSessionAsync(repairSessionGeneration);
            return true;
        }
    }

    private static void ThrowIfValidationRepairDispatchSessionReplaced(
        ValidationRepairDispatchState dispatch)
    {
        if (dispatch.SessionReplaced)
        {
            throw new GmWorkerSessionReplacedException(
                "The validation-repair worker belongs to a game session that is no longer current.");
        }
    }

    private async Task AppendAcceptedValidationRepairTrajectoryAsync(
        string source,
        List<ValidationIssue> errors,
        int attempt,
        ValidationRepairReady ready,
        PendingTurnSnapshotResolution pendingSnapshot,
        string expectedSessionGeneration,
        string acceptanceScope = "correlated_repair_ready",
        string terminalKind = "validation_repair_ready",
        string? signalPath = ValidationRepairReadyPath,
        string dispatchStatus = "client_observed_terminal")
    {
        const string ledgerPath = "game_state/control/gm_trajectory_ledger.jsonl";

        try
        {
            var context = pendingSnapshot.Context;
            var realmResolution = BuildValidationRepairTrajectoryRealmResolution(context);
            var repairPacketRefs = await BuildValidationRepairTrajectoryRepairPacketRefsAsync(errors);
            var record = new
            {
                recordId = "gmtraj_" + Guid.NewGuid().ToString("N"),
                kind = "repair",
                sessionId = ready.SessionId,
                turnId = ready.RequestId,
                requestId = ready.RequestId,
                turnNumber = ready.TurnNumber,
                realm = realmResolution.Realm,
                realmResolution,
                mode = "validation_repair",
                actionSummary = BuildValidationRepairTrajectoryActionSummary(context),
                contextPackPath = "game_state/control/gm_context_pack",
                templateVersions = new
                {
                    turnOutput = "v1",
                    validationRepair = "v1",
                    progressionReport = "v1",
                    actorReasoning = "v1",
                    tempoAdvantage = "v1"
                },
                outputFiles = Array.Empty<string>(),
                dispatch = new
                {
                    attempts = 0,
                    busyRetries = 0,
                    timeout = false,
                    status = dispatchStatus
                },
                validation = new
                {
                    status = "accepted",
                    source,
                    acceptanceScope,
                    fullCanonicalStateAccepted = false,
                    issueKinds = BuildValidationRepairTrajectoryIssueKinds(errors),
                    repairPacketRefs
                },
                repair = new
                {
                    attempts = attempt,
                    status = "accepted"
                },
                workerEvents = Array.Empty<object>(),
                rollbackEvents = Array.Empty<object>(),
                terminal = new
                {
                    kind = terminalKind,
                    signalPath
                },
                durationSeconds = (double?)null,
                rubric = new
                {
                    validTurn = true,
                    playerFacingOutputPresent = _fs.FileExists("output/narrative_response.json"),
                    implementationSourceRead = false,
                    rawWrongRealmWrite = false,
                    manualReasoningNeeded = false,
                    missingHarnessTool = (string?)null
                },
                createdAt = DateTime.UtcNow.ToString("o"),
                observedBy = "client"
            };

            var json = JsonSerializer.Serialize(record, new JsonSerializerOptions(JsonOpts)
            {
                WriteIndented = false
            });
            if (!await _fs.AppendFileAtomicIfCurrentSessionAsync(
                    ledgerPath,
                    json + Environment.NewLine,
                    expectedSessionGeneration))
            {
                throw new GmWorkerSessionReplacedException(
                    "The accepted validation-repair trajectory belongs to a game session that is no longer current.");
            }
        }
        catch (SessionReplacedException)
        {
            throw;
        }
        catch (Exception ex)
        {
            _logger.LogWarning(
                ex,
                "Failed to append accepted validation repair trajectory after {Source}. Gameplay continues without ledger entry.",
                source);
        }
    }

    private async Task EnsureRepairSessionCurrentAsync(string expectedSessionGeneration)
    {
        await using var writeLease = await _fs.AcquireCanonicalWriteLeaseAsync();
        if (!_fs.IsCurrentSessionGeneration(writeLease, expectedSessionGeneration))
        {
            throw new GmWorkerSessionReplacedException(
                "The validation-repair cycle belongs to a game session that is no longer current.");
        }
    }

    private async Task AppendWorkerAcceptedValidationRepairTrajectoryAsync(
        string source,
        List<ValidationIssue> errors,
        int attempt,
        ValidationRepairDispatchState dispatch)
    {
        var pendingSnapshot = await ResolveActivePendingTurnSnapshotContextAsync();
        var sourceTurn = dispatch.WorkerResult?.Task?.SourceTurn;
        var workerSessionGeneration = dispatch.WorkerResult?.Task?.SessionGeneration;
        if (string.IsNullOrWhiteSpace(workerSessionGeneration))
        {
            _logger.LogWarning(
                "Skipped worker accepted validation repair trajectory because its reserved task session generation is unavailable after {Source}.",
                source);
            return;
        }
        var ready = new ValidationRepairReady
        {
            SessionId = sourceTurn?.SessionId ?? pendingSnapshot.Context?.SessionId ?? string.Empty,
            RequestId = sourceTurn?.RequestId ?? pendingSnapshot.Context?.RequestId ?? string.Empty,
            TurnNumber = sourceTurn?.TurnNumber ?? pendingSnapshot.Context?.TurnNumber ?? 0,
            UpdatedAtUtc = DateTime.UtcNow.ToString("o"),
            Note = dispatch.WorkerResult?.FallbackReason
        };

        await AppendAcceptedValidationRepairTrajectoryAsync(
            source,
            errors,
            attempt,
            ready,
            pendingSnapshot,
            workerSessionGeneration,
            acceptanceScope: "worker_apply_gate",
            terminalKind: "worker_apply_gate_accepted",
            signalPath: null,
            dispatchStatus: "worker_applied_without_ready_signal");
    }

    private async Task AppendClearedValidationRepairTrajectoryAsync(
        string source,
        IReadOnlyCollection<ValidationIssue> errors,
        int attempt,
        string expectedSessionGeneration)
    {
        const string ledgerPath = "game_state/control/gm_trajectory_ledger.jsonl";

        try
        {
            var pendingSnapshot = await ResolveActivePendingTurnSnapshotContextAsync();
            var context = pendingSnapshot.Context;
            var requestMetadata = BuildProtocolRequestMetadata(pendingSnapshot);
            var realmResolution = BuildValidationRepairTrajectoryRealmResolution(context);
            var repairPacketRefs = await BuildValidationRepairTrajectoryRepairPacketRefsAsync(errors);
            var record = new
            {
                recordId = "gmtraj_" + Guid.NewGuid().ToString("N"),
                kind = "repair",
                sessionId = requestMetadata.SessionId,
                turnId = requestMetadata.RequestId,
                requestId = requestMetadata.RequestId,
                turnNumber = requestMetadata.TurnNumber,
                realm = realmResolution.Realm,
                realmResolution,
                mode = "validation_repair",
                actionSummary = BuildValidationRepairTrajectoryActionSummary(context),
                contextPackPath = "game_state/control/gm_context_pack",
                templateVersions = new
                {
                    turnOutput = "v1",
                    validationRepair = "v1",
                    progressionReport = "v1",
                    actorReasoning = "v1",
                    tempoAdvantage = "v1"
                },
                outputFiles = Array.Empty<string>(),
                dispatch = new
                {
                    attempts = 0,
                    busyRetries = 0,
                    timeout = false,
                    status = "client_revalidated_terminal"
                },
                validation = new
                {
                    status = "accepted",
                    source,
                    acceptanceScope = "full_canonical_state_after_repair",
                    fullCanonicalStateAccepted = true,
                    issueKinds = BuildValidationRepairTrajectoryIssueKinds(errors),
                    repairPacketRefs
                },
                repair = new
                {
                    attempts = attempt,
                    status = "cleared"
                },
                workerEvents = Array.Empty<object>(),
                rollbackEvents = Array.Empty<object>(),
                terminal = new
                {
                    kind = "validation_repair_cleared",
                    signalPath = (string?)null
                },
                durationSeconds = (double?)null,
                rubric = new
                {
                    validTurn = true,
                    playerFacingOutputPresent = _fs.FileExists("output/narrative_response.json"),
                    implementationSourceRead = false,
                    rawWrongRealmWrite = false,
                    manualReasoningNeeded = false,
                    missingHarnessTool = (string?)null
                },
                createdAt = DateTime.UtcNow.ToString("o"),
                observedBy = "client"
            };

            var json = JsonSerializer.Serialize(record, new JsonSerializerOptions(JsonOpts)
            {
                WriteIndented = false
            });
            if (!await _fs.AppendFileAtomicIfCurrentSessionAsync(
                    ledgerPath,
                    json + Environment.NewLine,
                    expectedSessionGeneration))
            {
                throw new GmWorkerSessionReplacedException(
                    "The cleared validation-repair trajectory belongs to a game session that is no longer current.");
            }
        }
        catch (SessionReplacedException)
        {
            throw;
        }
        catch (Exception ex)
        {
            _logger.LogWarning(
                ex,
                "Failed to append cleared validation repair trajectory after {Source}. Gameplay continues without ledger entry.",
                source);
        }
    }

    private static string[] BuildValidationRepairTrajectoryIssueKinds(IEnumerable<ValidationIssue> errors)
    {
        return errors
            .Select(error => string.IsNullOrWhiteSpace(error.Code)
                ? error.Category.ToString()
                : error.Code!)
            .Where(kind => !string.IsNullOrWhiteSpace(kind))
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .OrderBy(kind => kind, StringComparer.OrdinalIgnoreCase)
            .ToArray();
    }

    private async Task<string[]> BuildValidationRepairTrajectoryRepairPacketRefsAsync(IEnumerable<ValidationIssue> errors)
    {
        var refs = new List<string>();

        if (_fs.FileExists(ValidationRepairRequestPath))
        {
            try
            {
                var json = await _fs.ReadFileAsync(ValidationRepairRequestPath);
                var request = string.IsNullOrWhiteSpace(json)
                    ? null
                    : JsonSerializer.Deserialize<ValidationRepairRequest>(json, JsonOpts);

                if (request?.HarnessRepairPackets is { Count: > 0 })
                {
                    refs.AddRange(request.HarnessRepairPackets
                        .Select(packet => packet.Kind)
                        .Where(kind => !string.IsNullOrWhiteSpace(kind))!);
                }
            }
            catch (Exception ex)
            {
                _logger.LogWarning(
                    ex,
                    "Failed to read validation repair packet refs from active repair request for trajectory ledger.");
            }
        }

        if (refs.Count == 0)
        {
            refs.AddRange(BuildValidationRepairHarnessPackets(
                    PrioritizeValidationErrors(errors).ToList(),
                    await ReadCurrentGuardianRepairActorNameHintsAsync())
                .Select(packet => packet.Kind)
                .Where(kind => !string.IsNullOrWhiteSpace(kind))!);
        }

        return refs
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .OrderBy(reference => reference, StringComparer.OrdinalIgnoreCase)
            .ToArray();
    }

    private static string BuildValidationRepairTrajectoryActionSummary(ValidatedPendingTurnSnapshotContext? context)
    {
        var text = context?.PlayerAction?.Trim();
        if (string.IsNullOrWhiteSpace(text))
            return string.Empty;
        return text.Length <= 160 ? text : text[..157] + "...";
    }

    private sealed class ValidationRepairTrajectoryRealmResolution
    {
        public string Realm { get; init; } = "Unknown";
        public string Source { get; init; } = "unavailable";
        public string RawValue { get; init; } = string.Empty;
        public string Reason { get; init; } = string.Empty;
    }

    private static ValidationRepairTrajectoryRealmResolution BuildValidationRepairTrajectoryRealmResolution(
        ValidatedPendingTurnSnapshotContext? context)
    {
        var rawRealm = context?.ProgressionControl?.CurrentRealm;
        if (!string.IsNullOrWhiteSpace(rawRealm))
        {
            var realm = NormalizeValidationRepairTrajectoryRealm(rawRealm);
            return new ValidationRepairTrajectoryRealmResolution
            {
                Realm = realm,
                Source = "pending_turn_snapshot.progressionControl.currentRealm",
                RawValue = rawRealm,
                Reason = realm == "Unknown" ? "unrecognized_current_realm" : string.Empty
            };
        }

        return new ValidationRepairTrajectoryRealmResolution
        {
            Realm = "Unknown",
            Source = context == null ? "pending_turn_snapshot.unavailable" : "pending_turn_snapshot.progressionControl.currentRealm",
            RawValue = string.Empty,
            Reason = context == null ? "missing_pending_turn_snapshot_context" : "missing_current_realm"
        };
    }

    private static string NormalizeValidationRepairTrajectoryRealm(string realm)
    {
        var normalized = realm.Trim();
        if (string.Equals(normalized, "Chaos Sea", StringComparison.OrdinalIgnoreCase) ||
            string.Equals(normalized, "Море Хаоса", StringComparison.OrdinalIgnoreCase))
        {
            return "ChaosSea";
        }

        if (string.Equals(normalized, "Shining Abode", StringComparison.OrdinalIgnoreCase) ||
            string.Equals(normalized, "Сияющая Обитель", StringComparison.OrdinalIgnoreCase))
        {
            return "ShiningAbode";
        }

        if (string.Equals(normalized, "Mortal World", StringComparison.OrdinalIgnoreCase) ||
            string.Equals(normalized, "MortalWorld", StringComparison.OrdinalIgnoreCase) ||
            string.Equals(normalized, "Смертный мир", StringComparison.OrdinalIgnoreCase))
        {
            return "MortalWorld";
        }

        return "Unknown";
    }

    private async Task<ValidationRepairDispatchState> WriteValidationRepairRequestAsync(
        string source,
        List<ValidationIssue> errors,
        int attempt)
    {
        var sessionGeneration = await CaptureCurrentSessionGenerationAsync();
        return await WriteValidationRepairRequestForSessionAsync(
            source,
            errors,
            attempt,
            sessionGeneration);
    }

    private async Task<ValidationRepairDispatchState> WriteValidationRepairRequestForSessionAsync(
        string source,
        List<ValidationIssue> errors,
        int attempt,
        string expectedSessionGeneration)
    {
        await DeleteValidationRepairFilesForSessionAsync(expectedSessionGeneration);
        var prioritizedErrors = PrioritizeValidationErrors(errors).ToList();
        var pendingSnapshot = await ResolveActivePendingTurnSnapshotContextAsync();
        var requestMetadata = BuildProtocolRequestMetadata(pendingSnapshot);
        var metadataDiagnosticOnly = BuildProtocolRequestMetadataDiagnosticOnly(pendingSnapshot);
        var gmInstructions = BuildValidationRepairRequestInstructions(pendingSnapshot);

        var request = new ValidationRepairRequest
        {
            SessionId = requestMetadata.SessionId,
            RequestId = requestMetadata.RequestId,
            TurnNumber = requestMetadata.TurnNumber,
            MetadataDiagnosticOnly = metadataDiagnosticOnly,
            Source = source,
            DetectedAtUtc = DateTime.UtcNow.ToString("o"),
            RevalidationAttempt = attempt,
            GmInstructions = gmInstructions,
            SummaryGroups = BuildValidationSummaryLines(prioritizedErrors, 6),
            HarnessRepairPackets = BuildValidationRepairHarnessPackets(
                prioritizedErrors,
                await ReadCurrentGuardianRepairActorNameHintsAsync()),
            Errors = prioritizedErrors.Select(e => new ValidationRepairIssue
            {
                Code = e.Code ?? "validation_error",
                FilePath = e.FilePath,
                Severity = e.Severity.ToString(),
                Category = e.Category.ToString(),
                Message = e.Message,
                Actor = e.Actor,
                Section = e.Section,
                Expected = e.Expected,
                Actual = e.Actual,
                RepairHint = e.RepairHint ?? "Исправь состояние/структуру так, чтобы оно соответствовало Rules/API contract."
            }).ToList()
        };

        PublishAgentConsoleValidationRepairSnapshot(request);
        GmWorkerValidationRepairDispatchResult? workerResult = null;
        if (!metadataDiagnosticOnly)
        {
            workerResult = await RunWorkerValidationRepairIfAvailableAsync(
                prioritizedErrors,
                requestMetadata,
                request.DetectedAtUtc,
                attempt,
                expectedSessionGeneration);
            if (workerResult.Outcome == GmWorkerValidationRepairOutcome.Applied)
            {
                return new ValidationRepairDispatchState
                {
                    WorkerApplyAccepted = true,
                    ReadySignalCreated = workerResult.ReadySignalCreated,
                    WorkerResult = workerResult
                };
            }
            if (workerResult.Outcome == GmWorkerValidationRepairOutcome.SessionReplaced)
            {
                return new ValidationRepairDispatchState
                {
                    SessionReplaced = true,
                    WorkerResult = workerResult
                };
            }
        }

        await WriteValidationRepairFileForSessionAsync(
            ValidationRepairRequestPath,
            JsonSerializer.Serialize(request, JsonOpts),
            expectedSessionGeneration);
        return new ValidationRepairDispatchState
        {
            MetadataDiagnosticOnly = metadataDiagnosticOnly,
            WorkerResult = workerResult
        };
    }

    private async Task<bool> FailClosedDiagnosticOnlyValidationRepairAsync(
        string source,
        List<ValidationIssue> errors,
        int attempt,
        RollbackSnapshot? rollbackSnapshot,
        string expectedSessionGeneration)
    {
        var prioritizedErrors = PrioritizeValidationErrors(errors).ToList();
        _logger.LogError(
            "Diagnostic-only validation repair request cannot be completed by GM after {Source}; failing closed instead of waiting for validation_repair_ready.json.",
            source);

        var report = new
        {
            source,
            detectedAtUtc = DateTime.UtcNow.ToString("o"),
            attempt,
            reason = "Diagnostic-only validation repair request cannot be completed by GM because active pending-turn metadata is missing or unusable.",
            rollbackAvailable = HasRollbackCapability(rollbackSnapshot),
            summaryGroups = BuildValidationSummaryLines(prioritizedErrors, 6),
            errors = prioritizedErrors.Select(e => new
            {
                code = e.Code ?? "validation_error",
                filePath = e.FilePath,
                severity = e.Severity.ToString(),
                category = e.Category.ToString(),
                message = e.Message,
                actor = e.Actor,
                section = e.Section,
                expected = e.Expected,
                actual = e.Actual,
                repairHint = e.RepairHint
            }).ToList()
        };

        if (HasRollbackCapability(rollbackSnapshot))
        {
            await RestorePreTurnBackupForSessionAsync(
                rollbackSnapshot!,
                expectedSessionGeneration);
            AnsiConsole.MarkupLine("[yellow]↩ Клиент остановил неремонтопригодный diagnostic-only repair и восстановил состояние из rollback backup.[/]");
        }
        else
        {
            AnsiConsole.MarkupLine("[yellow]⚠ Клиент остановил неремонтопригодный diagnostic-only repair. Автоматический rollback для этого режима недоступен.[/]");
        }

        await WriteValidationRepairFileForSessionAsync(
            ValidationDiagnosticFailureReportPath,
            JsonSerializer.Serialize(report, JsonOpts),
            expectedSessionGeneration);
        if (!await _progressionSchedule.DeleteTransientReportIfCurrentSessionAsync(
                expectedSessionGeneration))
        {
            throw new GmWorkerSessionReplacedException(
                "The validation-repair cycle belongs to a game session that is no longer current.");
        }
        await DeleteValidationRepairFilesForSessionAsync(expectedSessionGeneration);
        return false;
    }

    private static List<ValidationRepairHarnessPacket> BuildValidationRepairHarnessPackets(
        IReadOnlyList<ValidationIssue> errors,
        IReadOnlyCollection<string>? guardianActorNameHints = null)
    {
        var packets = new List<ValidationRepairHarnessPacket>();
        var guardianPendingCreationMaterializationErrors = errors.Where(IsGuardianPendingCreationMaterializationRepairIssue).ToList();
        var guardianScopeErrors = errors.Where(IsGuardianScopeRepairIssue).ToList();
        var guardianScopeActorNames = CollectRepairActorNames(guardianScopeErrors, guardianActorNameHints);
        var actorReasoningSubpointErrors = errors
            .Where(IsActorReasoningSubpointRepairIssue)
            .Where(issue => !guardianScopeActorNames.Contains(NormalizeRepairActorName(issue.Actor)))
            .ToList();
        var actorMemoryPersistenceErrors = errors.Where(IsActorMemoryPersistenceRepairIssue).ToList();
        var actorMaterializationErrors = errors.Where(IsActorMaterializationRepairIssue).ToList();
        var factionMaterializationGroups = errors
            .Where(IsFactionMaterializationRepairIssue)
            .Select(issue => (
                Issue: issue,
                Coordinate: ResolveFactionMaterializationRepairCoordinate(issue)))
            .Where(candidate => candidate.Coordinate != null)
            .GroupBy(
                candidate => candidate.Coordinate!,
                candidate => candidate.Issue,
                StringComparer.Ordinal)
            .OrderBy(group => group.Key, StringComparer.Ordinal)
            .ToList();
        var factionIdentityErrors = errors
            .Where(issue => !IsRoutableFactionMaterializationRepairIssue(issue))
            .Where(IsFactionIdentityRepairIssue)
            .ToList();
        var mortalFactionResourceErrors = errors
            .Where(issue => !IsRoutableFactionMaterializationRepairIssue(issue))
            .Where(IsMortalFactionResourceRepairIssue)
            .ToList();
        var mortalBootstrapMaterializationErrors = errors.Where(IsMortalBootstrapMaterializationRepairIssue).ToList();
        var mortalWorldMapAdjacencyErrors = errors.Where(IsMortalWorldMapAdjacencyRepairIssue).ToList();
        var mortalLocationTransitionErrors = errors.Where(IsMortalLocationTransitionRepairIssue).ToList();
        var mortalNpcScopeErrors = errors.Where(IsMortalNpcScopeRepairIssue).ToList();
        var mortalNpcLocationErrors = errors.Where(IsMortalNpcLocationRepairIssue).ToList();
        var mortalNpcInventoryUpdateErrors = errors.Where(IsMortalNpcInventoryUpdateRepairIssue).ToList();
        var mortalNpcFullObjectErrors = errors.Where(IsMortalNpcFullObjectRepairIssue).ToList();
        var mortalNpcRelationshipEnumErrors = errors.Where(IsMortalNpcRelationshipEnumRepairIssue).ToList();
        var mortalNpcReferenceErrors = errors.Where(IsMortalNpcReferenceRepairIssue).ToList();
        var mortalCombatStateErrors = errors.Where(IsMortalCombatStateRepairIssue).ToList();
        var mortalSkillProgressionShapeErrors = errors.Where(IsMortalSkillProgressionShapeRepairIssue).ToList();
        var afterlifeChronicleStringArrayErrors = errors.Where(IsAfterlifeChronicleStringArrayRepairIssue).ToList();
        var guardianTradeInventoryResolutionErrors = errors.Where(IsGuardianTradeInventoryResolutionRepairIssue).ToList();
        var afterlifeActionCostErrors = errors.Where(IsAfterlifeSpiritualConflictActionCostRepairIssue).ToList();
        var afterlifeConflictRewardErrors = errors.Where(IsAfterlifeSpiritualConflictRewardRepairIssue).ToList();
        var afterlifeEntityProfileScaffoldErrors = errors.Where(IsAfterlifeEntityProfileScaffoldRepairIssue).ToList();
        var mortalTrainingSkillEvolutionErrors = errors.Where(IsMortalTrainingSkillEvolutionRepairIssue).ToList();
        var trainingShowcaseSnapshotErrors = errors.Where(IsTrainingShowcaseSnapshotRepairIssue).ToList();
        var npcScopeDeclarationErrors = errors.Where(IsNpcScopeDeclarationRepairIssue).ToList();
        var acceptedTurnOutputArtifactErrors = errors.Where(IsAcceptedTurnOutputArtifactRepairIssue).ToList();

        if (guardianPendingCreationMaterializationErrors.Count > 0)
            packets.Add(BuildGuardianPendingCreationMaterializationRepairPacket(guardianPendingCreationMaterializationErrors));

        if (guardianScopeErrors.Count > 0)
            packets.Add(BuildGuardianScopeRepairPacket(guardianScopeErrors, guardianActorNameHints));

        if (npcScopeDeclarationErrors.Count > 0)
            packets.Add(BuildNpcScopeDeclarationRepairPacket(npcScopeDeclarationErrors));

        if (acceptedTurnOutputArtifactErrors.Count > 0)
        {
            var outputArtifactPacket = BuildAcceptedTurnOutputArtifactRepairPacket(acceptedTurnOutputArtifactErrors);
            if (outputArtifactPacket.TargetFiles.Count > 0)
                packets.Add(outputArtifactPacket);
        }

        if (actorReasoningSubpointErrors.Count > 0)
            packets.Add(BuildActorReasoningSubpointRepairPacket(actorReasoningSubpointErrors));

        if (actorMemoryPersistenceErrors.Count > 0)
            packets.Add(BuildActorMemoryPersistenceRepairPacket(actorMemoryPersistenceErrors, guardianActorNameHints));

        if (actorMaterializationErrors.Count > 0)
            packets.Add(BuildActorMaterializationRepairPacket(actorMaterializationErrors));

        foreach (var factionMaterializationGroup in factionMaterializationGroups)
        {
            packets.Add(BuildFactionMaterializationRepairPacket(
                factionMaterializationGroup.Key,
                factionMaterializationGroup.ToList()));
        }

        if (factionIdentityErrors.Count > 0)
            packets.Add(BuildFactionIdentityRepairPacket(factionIdentityErrors));

        if (mortalFactionResourceErrors.Count > 0)
            packets.Add(BuildMortalFactionResourceRepairPacket(mortalFactionResourceErrors));

        if (mortalBootstrapMaterializationErrors.Count > 0)
            packets.Add(BuildMortalBootstrapMaterializationRepairPacket(mortalBootstrapMaterializationErrors));

        if (mortalWorldMapAdjacencyErrors.Count > 0)
            packets.Add(BuildMortalWorldMapAdjacencyRepairPacket(mortalWorldMapAdjacencyErrors));

        if (mortalLocationTransitionErrors.Count > 0)
            packets.Add(BuildMortalLocationTransitionRepairPacket(mortalLocationTransitionErrors));

        if (mortalNpcScopeErrors.Count > 0)
            packets.Add(BuildMortalNpcScopeRepairPacket(mortalNpcScopeErrors));

        if (mortalNpcLocationErrors.Count > 0)
            packets.Add(BuildMortalNpcLocationRepairPacket(mortalNpcLocationErrors));

        if (mortalNpcInventoryUpdateErrors.Count > 0)
            packets.Add(BuildMortalNpcInventoryUpdateRepairPacket(mortalNpcInventoryUpdateErrors));

        if (mortalNpcFullObjectErrors.Count > 0)
            packets.Add(BuildMortalNpcFullObjectRepairPacket(mortalNpcFullObjectErrors));

        if (mortalNpcRelationshipEnumErrors.Count > 0)
            packets.Add(BuildMortalNpcRelationshipEnumRepairPacket(mortalNpcRelationshipEnumErrors));

        if (mortalNpcReferenceErrors.Count > 0)
            packets.Add(BuildMortalNpcReferenceRepairPacket(mortalNpcReferenceErrors));

        if (mortalCombatStateErrors.Count > 0)
            packets.Add(BuildMortalCombatStateRepairPacket(mortalCombatStateErrors));

        if (mortalSkillProgressionShapeErrors.Count > 0)
            packets.Add(BuildMortalSkillProgressionShapeRepairPacket(mortalSkillProgressionShapeErrors));

        if (mortalTrainingSkillEvolutionErrors.Count > 0)
            packets.Add(BuildMortalTrainingSkillEvolutionRepairPacket(mortalTrainingSkillEvolutionErrors));

        if (trainingShowcaseSnapshotErrors.Count > 0)
            packets.Add(BuildTrainingShowcaseSnapshotHashRepairPacket(trainingShowcaseSnapshotErrors));

        if (afterlifeChronicleStringArrayErrors.Count > 0)
            packets.Add(BuildAfterlifeChronicleStringArrayRepairPacket(afterlifeChronicleStringArrayErrors));

        if (guardianTradeInventoryResolutionErrors.Count > 0)
            packets.Add(BuildGuardianTradeInventoryResolutionRepairPacket(guardianTradeInventoryResolutionErrors));

        if (afterlifeActionCostErrors.Count > 0)
            packets.Add(BuildAfterlifeSpiritualConflictActionCostRepairPacket(afterlifeActionCostErrors));

        if (afterlifeConflictRewardErrors.Count > 0)
            packets.Add(BuildAfterlifeSpiritualConflictRewardRepairPacket(afterlifeConflictRewardErrors));

        if (afterlifeEntityProfileScaffoldErrors.Count > 0)
            packets.Add(BuildAfterlifeEntityProfileScaffoldRepairPacket(afterlifeEntityProfileScaffoldErrors));

        return packets;
    }

    private static bool IsGuardianScopeRepairIssue(ValidationIssue issue)
    {
        return string.Equals(issue.Code, "guardian_materialized_state_outside_authority", StringComparison.OrdinalIgnoreCase) ||
               string.Equals(issue.Code, "guardian_project_materialized_state_outside_authority", StringComparison.OrdinalIgnoreCase) ||
               string.Equals(issue.Code, "structured_guardian_update_out_of_scope", StringComparison.OrdinalIgnoreCase) ||
               string.Equals(issue.Code, "active_guardian_missing_from_scope", StringComparison.OrdinalIgnoreCase);
    }

    private static bool IsGuardianPendingCreationMaterializationRepairIssue(ValidationIssue issue)
    {
        return string.Equals(issue.Code, "guardian_materialized_without_create_surface", StringComparison.OrdinalIgnoreCase) ||
               string.Equals(issue.Code, "stale_pending_guardian_creation_after_materialization", StringComparison.OrdinalIgnoreCase) ||
               string.Equals(issue.Code, "pending_guardian_creation_missing_materialized_guardian", StringComparison.OrdinalIgnoreCase) ||
               string.Equals(issue.Code, "pending_guardian_creation_unresolved_after_startup_turn", StringComparison.OrdinalIgnoreCase);
    }

    private static bool IsActorReasoningSubpointRepairIssue(ValidationIssue issue)
    {
        var code = issue.Code ?? string.Empty;
        return IsGuardianReasoningSection(issue.Section) &&
               (string.Equals(code, "missing_actor_block", StringComparison.OrdinalIgnoreCase) ||
                string.Equals(code, "missing_actor_situation", StringComparison.OrdinalIgnoreCase) ||
                string.Equals(code, "missing_actor_thoughts", StringComparison.OrdinalIgnoreCase) ||
                string.Equals(code, "missing_actor_actions", StringComparison.OrdinalIgnoreCase) ||
                code.StartsWith("actor_brain_", StringComparison.OrdinalIgnoreCase));
    }

    private static bool IsActorMemoryPersistenceRepairIssue(ValidationIssue issue)
    {
        return string.Equals(issue.Code, "guardian_relevant_actor_missing_thought_journal_delta", StringComparison.OrdinalIgnoreCase) ||
               string.Equals(issue.Code, "mortal_npc_relevant_actor_missing_thought_journal_delta", StringComparison.OrdinalIgnoreCase) ||
               string.Equals(issue.Code, "afterlife_resident_relevant_actor_missing_thought_journal_delta", StringComparison.OrdinalIgnoreCase) ||
               string.Equals(issue.Code, "afterlife_entity_relevant_actor_missing_memory_ledger_delta", StringComparison.OrdinalIgnoreCase) ||
               string.Equals(issue.Code, "shining_faction_relevant_actor_missing_strategic_memory_delta", StringComparison.OrdinalIgnoreCase) ||
               string.Equals(issue.Code, "actor_thought_journal_not_first_person", StringComparison.OrdinalIgnoreCase) ||
               IsGuardianThoughtJournalShapeRepairIssue(issue);
    }

    private static bool IsActorMaterializationRepairIssue(ValidationIssue issue)
    {
        return (issue.Code ?? string.Empty).ToLowerInvariant() switch
        {
            "actor_materialization_missing" => true,
            "actor_materialization_invalid_envelope" => true,
            "actor_materialization_actor_binding_mismatch" => true,
            "actor_materialization_duplicate_id" => true,
            "actor_materialization_duplicate_property" => true,
            "actor_materialization_invalid_actor_type" => true,
            "actor_materialization_inventory_reference_mismatch" => true,
            "actor_materialization_section_missing" => true,
            "actor_materialization_section_content_mismatch" => true,
            "actor_materialization_section_empty_surface_invalid" => true,
            "actor_materialization_capability_mismatch" => true,
            "actor_materialization_existing_resend_forbidden" => true,
            "actor_materialization_historical_envelope_changed" => true,
            "actor_materialization_afterlife_missing_appearance" => true,
            "actor_materialization_afterlife_missing_profile_summary" => true,
            "actor_materialization_afterlife_missing_personality" => true,
            "actor_materialization_afterlife_missing_motivation" => true,
            "actor_materialization_afterlife_missing_worldview" => true,
            "actor_materialization_afterlife_missing_realm" => true,
            "actor_materialization_afterlife_missing_location" => true,
            "actor_materialization_afterlife_missing_goals_plan" => true,
            "afterlife_actor_materialization_profile_missing" => true,
            "afterlife_actor_materialization_profile_ambiguous" => true,
            "afterlife_actor_materialization_memory_missing" => true,
            "npc_new_update_location_authority_not_exactly_one" => true,
            "npc_new_update_current_location_unknown" => true,
            _ => false
        };
    }

    private static bool IsFactionMaterializationRepairIssue(ValidationIssue issue) =>
        issue.Code?.StartsWith(
            "faction_materialization_",
            StringComparison.Ordinal) == true ||
        string.Equals(
            issue.Code,
            "faction_existing_full_resend_forbidden",
            StringComparison.Ordinal);

    private static bool IsRoutableFactionMaterializationRepairIssue(ValidationIssue issue) =>
        IsFactionMaterializationRepairIssue(issue) &&
        ResolveFactionMaterializationRepairCoordinate(issue) != null;

    private static string? ResolveFactionMaterializationRepairCoordinate(ValidationIssue issue)
    {
        var actor = issue.Actor;
        if (string.IsNullOrEmpty(actor))
            return null;

        var prefix = actor.StartsWith("mortal_faction:", StringComparison.Ordinal)
            ? "mortal_faction:"
            : actor.StartsWith("shining_faction:", StringComparison.Ordinal)
                ? "shining_faction:"
                : null;
        if (prefix == null || actor.Length == prefix.Length)
            return null;

        for (var index = prefix.Length; index < actor.Length; index++)
        {
            if (char.IsWhiteSpace(actor[index]) || actor[index] == ':')
                return null;
        }

        return actor;
    }

    private static bool IsGuardianThoughtJournalShapeRepairIssue(ValidationIssue issue)
    {
        var path = NormalizeRepairTargetPath(issue.FilePath);
        if (!path.StartsWith(GuardianThoughtJournalState.StatePath, StringComparison.OrdinalIgnoreCase))
            return false;

        return (issue.Code ?? string.Empty).ToLowerInvariant() switch
        {
            "strict_state_missing_allowed_top_level_key" => true,
            "missing_allowed_top_level_key" => true,
            "flexible_state_unknown_top_level_key" => true,
            "expected_array" => true,
            _ => false
        };
    }

    private static bool IsAfterlifeChronicleStringArrayRepairIssue(ValidationIssue issue)
    {
        return string.Equals(issue.Section, "AfterlifeChronicles", StringComparison.OrdinalIgnoreCase) &&
               (string.Equals(issue.Code, "afterlife_chronicle_persistent_consequences_entry_invalid", StringComparison.OrdinalIgnoreCase) ||
                string.Equals(issue.Code, "afterlife_chronicle_open_threads_entry_invalid", StringComparison.OrdinalIgnoreCase) ||
                string.Equals(issue.Code, "afterlife_chronicle_persistent_consequences_not_array", StringComparison.OrdinalIgnoreCase) ||
                string.Equals(issue.Code, "afterlife_chronicle_open_threads_not_array", StringComparison.OrdinalIgnoreCase));
    }

    private static bool IsFactionIdentityRepairIssue(ValidationIssue issue)
    {
        return string.Equals(issue.Code, "faction_full_object_existing_requires_faction_id", StringComparison.OrdinalIgnoreCase) ||
               string.Equals(issue.Code, "faction_full_object_unknown_faction_id", StringComparison.OrdinalIgnoreCase) ||
               string.Equals(issue.Code, "faction_full_object_conflicting_identity", StringComparison.OrdinalIgnoreCase) ||
               string.Equals(issue.Code, "faction_full_object_requires_explicit_null_faction_id", StringComparison.OrdinalIgnoreCase) ||
               string.Equals(issue.Code, "faction_full_object_requires_explicit_create_flag", StringComparison.OrdinalIgnoreCase) ||
               string.Equals(issue.Code, "faction_command_unknown_faction_id", StringComparison.OrdinalIgnoreCase) ||
               string.Equals(issue.Code, "canonical_faction_sidecar_requires_permanent_faction_id", StringComparison.OrdinalIgnoreCase) ||
               string.Equals(issue.Code, "canonical_faction_sidecar_unknown_faction_id", StringComparison.OrdinalIgnoreCase);
    }

    private static bool IsMortalNpcLocationRepairIssue(ValidationIssue issue)
    {
        return string.Equals(issue.Code, "missing_actor_current_location", StringComparison.OrdinalIgnoreCase) ||
               (IsMortalNpcRepairPath(issue.FilePath) &&
                (string.Equals(issue.Code, "current_location_new_scene_missing_initial_id_for_npc_scene", StringComparison.OrdinalIgnoreCase) ||
                 string.Equals(issue.Code, "missing_actor_current_location", StringComparison.OrdinalIgnoreCase) ||
                 string.Equals(issue.Code, "npc_scene_missing_current_location_id", StringComparison.OrdinalIgnoreCase) ||
                 string.Equals(issue.Code, "npc_scene_location_mismatch", StringComparison.OrdinalIgnoreCase) ||
                 string.Equals(issue.Code, "npc_scene_missing_initial_location_id", StringComparison.OrdinalIgnoreCase) ||
                 string.Equals(issue.Code, "npc_scene_initial_location_mismatch", StringComparison.OrdinalIgnoreCase) ||
                 string.Equals(issue.Code, "npc_initial_location_same_turn_target_unknown", StringComparison.OrdinalIgnoreCase) ||
                 string.Equals(issue.Code, "npc_same_turn_initial_location_requires_null_current_location", StringComparison.OrdinalIgnoreCase)));
    }

    private static bool IsMortalFactionResourceRepairIssue(ValidationIssue issue)
    {
        return string.Equals(issue.Code, "canonical_faction_resource_entry_missing_required_fields", StringComparison.OrdinalIgnoreCase) ||
               string.Equals(issue.Code, "faction_full_object_missing_meta_resources", StringComparison.OrdinalIgnoreCase) ||
               string.Equals(issue.Code, "faction_full_object_missing_strategic_goods", StringComparison.OrdinalIgnoreCase);
    }

    private static bool IsMortalLocationTransitionRepairIssue(ValidationIssue issue)
    {
        return string.Equals(issue.Code, "current_location_unknown_location_id", StringComparison.OrdinalIgnoreCase) ||
               string.Equals(issue.Code, "npc_unknown_current_location_id", StringComparison.OrdinalIgnoreCase) ||
               string.Equals(issue.Code, "location_missing_active_threat_array", StringComparison.OrdinalIgnoreCase) ||
               string.Equals(issue.Code, "location_missing_adjacency_array", StringComparison.OrdinalIgnoreCase) ||
               string.Equals(issue.Code, "location_missing_difficulty_profile", StringComparison.OrdinalIgnoreCase) ||
               string.Equals(issue.Code, "location_outdoor_biome_missing", StringComparison.OrdinalIgnoreCase) ||
               string.Equals(issue.Code, "location_missing_storage_array", StringComparison.OrdinalIgnoreCase) ||
               string.Equals(issue.Code, "world_map_new_link_missing_required_fields", StringComparison.OrdinalIgnoreCase) ||
               string.Equals(issue.Code, "world_map_new_location_coordinates_duplicate_same_turn", StringComparison.OrdinalIgnoreCase) ||
               string.Equals(issue.Code, "world_map_new_location_coordinates_conflict_existing", StringComparison.OrdinalIgnoreCase) ||
               string.Equals(issue.Code, "world_map_new_location_requires_null_location_id", StringComparison.OrdinalIgnoreCase) ||
               string.Equals(issue.Code, "world_map_new_location_missing_description", StringComparison.OrdinalIgnoreCase);
    }

    private static bool IsMortalWorldMapAdjacencyRepairIssue(ValidationIssue issue)
    {
        return (issue.Code ?? string.Empty).ToLowerInvariant() switch
        {
            "world_map_adjacency_unknown_target" => true,
            "world_map_link_update_unknown_source" => true,
            "world_map_link_remove_unknown_source" => true,
            "world_map_storage_update_unknown_target" => true,
            "world_map_storage_remove_unknown_target" => true,
            "world_map_threat_add_unknown_target" => true,
            "world_map_threat_add_unknown_same_turn_initial_id" => true,
            "world_map_threat_update_unknown_target" => true,
            "world_map_threat_remove_unknown_target" => true,
            "world_map_threat_complete_unknown_target" => true,
            _ => false
        };
    }

    private static bool IsMortalNpcFullObjectRepairIssue(ValidationIssue issue)
    {
        if (!IsMortalNpcRepairPath(issue.FilePath))
            return false;

        if (IsMortalNpcLocationRepairIssue(issue) || IsMortalNpcInventoryUpdateRepairIssue(issue) || IsMortalNpcRelationshipEnumRepairIssue(issue))
            return false;

        if (string.Equals(issue.Code, "npc_full_object_missing_required_fields", StringComparison.OrdinalIgnoreCase))
            return true;

        if (string.Equals(issue.Code, "expected_object", StringComparison.OrdinalIgnoreCase) &&
            IsMortalNpcSkillObjectRepairPath(issue.FilePath))
            return true;

        var code = issue.Code ?? string.Empty;
        return code.StartsWith("npc_", StringComparison.OrdinalIgnoreCase) &&
               (code.Contains("object", StringComparison.OrdinalIgnoreCase) ||
                code.Contains("null", StringComparison.OrdinalIgnoreCase) ||
                code.Contains("shape", StringComparison.OrdinalIgnoreCase));
    }

    private static bool IsMortalNpcScopeRepairIssue(ValidationIssue issue)
    {
        return string.Equals(issue.Code, "structured_npc_update_out_of_scope", StringComparison.OrdinalIgnoreCase) ||
               string.Equals(issue.Code, "mortal_relevant_actor_missing_persistence", StringComparison.OrdinalIgnoreCase);
    }

    private static bool IsNpcScopeDeclarationRepairIssue(ValidationIssue issue)
    {
        return (issue.Code ?? string.Empty).ToLowerInvariant() switch
        {
            "missing_scope_mode" => true,
            "invalid_scope_mode" => true,
            "missing_relevant_actors_field" => true,
            "empty_relevant_actors_for_mode" => true,
            "missing_scope_relevance_reason" => true,
            "missing_out_of_scope_actors_field" => true,
            "missing_scope_out_of_scope_reason" => true,
            "missing_actor_reasoning_section" => true,
            "directly_addressed_actor_missing_from_scope" => true,
            "structured_resident_update_out_of_scope" => true,
            "structured_shining_actor_update_out_of_scope" => true,
            "structured_shining_faction_update_out_of_scope" => true,
            "structured_afterlife_entity_update_out_of_scope" => true,
            _ => false
        };
    }

    private static bool IsMortalNpcInventoryUpdateRepairIssue(ValidationIssue issue)
    {
        return IsMortalNpcRepairPath(issue.FilePath) &&
               string.Equals(issue.Code, "npc_existing_inventory_resend_forbidden", StringComparison.OrdinalIgnoreCase);
    }

    private static bool IsMortalBootstrapMaterializationRepairIssue(ValidationIssue issue)
    {
        return (issue.Code ?? string.Empty).ToLowerInvariant() switch
        {
            "bootstrap_codex_missing_current_world_entries" => true,
            "mortal_bootstrap_reused_previous_world_lore" => true,
            "canonical_faction_custom_state_missing_required_fields" => true,
            "canonical_faction_custom_state_missing_progression_rule" => true,
            "readable_document_missing_detail_authority" => true,
            "item_invalid_quality" => true,
            "item_missing_accessory_for_slot" => true,
            "item_missing_equipment_slot" => true,
            "current_location_coordinates_mismatch" => true,
            "location_faction_control_invalid_type" => true,
            "world_map_active_threat_missing_archetype" => true,
            "codex_related_entry_unknown_target" => true,
            "npc_contract_unknown_top_level_key" => true,
            "flexible_state_unknown_top_level_key" => IsMortalBootstrapGenericShapeRepairIssue(issue),
            "mortal_bootstrap_requested_teacher_missing" => true,
            "mortal_bootstrap_requested_trade_missing" => true,
            "mortal_bootstrap_explicit_competency_missing" => true,
            "mortal_bootstrap_world_event_missing" => true,
            "missing_required_string" => IsMortalBootstrapGenericShapeRepairIssue(issue),
            "missing_required_boolean_field" => IsMortalBootstrapGenericShapeRepairIssue(issue),
            "expected_string_array" => IsMortalBootstrapGenericShapeRepairIssue(issue),
            "missing_required_string_array_field" => IsMortalBootstrapGenericShapeRepairIssue(issue),
            "item_invalid_accessory_slot" => IsMortalBootstrapItemRepairPath(issue.FilePath),
            "item_invalid_equipment_slot" => IsMortalBootstrapItemRepairPath(issue.FilePath),
            "validation_error" => IsMortalBootstrapGenericShapeRepairIssue(issue),
            _ => false
        };
    }

    private static bool IsMortalBootstrapGenericShapeRepairIssue(ValidationIssue issue)
    {
        return IsMortalBootstrapItemRepairPath(issue.FilePath) ||
               IsMortalBootstrapCodexRepairPath(issue.FilePath) ||
               IsMortalBootstrapCurrentWorldRepairPath(issue.FilePath);
    }

    private static bool IsMortalBootstrapItemRepairPath(string? path)
    {
        if (string.IsNullOrWhiteSpace(path))
            return false;

        return path.Replace('\\', '/').StartsWith("game_state/inventory/items.json", StringComparison.OrdinalIgnoreCase);
    }

    private static bool IsMortalBootstrapCodexRepairPath(string? path)
    {
        if (string.IsNullOrWhiteSpace(path))
            return false;

        return path.Replace('\\', '/').StartsWith("lore/codex_entries.json", StringComparison.OrdinalIgnoreCase);
    }

    private static bool IsMortalBootstrapCurrentWorldRepairPath(string? path)
    {
        if (string.IsNullOrWhiteSpace(path))
            return false;

        return path.Replace('\\', '/').StartsWith("lore/current_world/", StringComparison.OrdinalIgnoreCase);
    }

    private static bool IsMortalNpcRelationshipEnumRepairIssue(ValidationIssue issue)
    {
        return IsMortalNpcRepairPath(issue.FilePath) &&
               (string.Equals(issue.Code, "npc_attitude_relationship_tier_mismatch", StringComparison.OrdinalIgnoreCase) ||
                string.Equals(issue.Code, "npc_invalid_cultural_stance", StringComparison.OrdinalIgnoreCase));
    }

    private static bool IsMortalNpcReferenceRepairIssue(ValidationIssue issue)
    {
        return string.Equals(issue.Code, "npc_journal_unknown_npc_reference", StringComparison.OrdinalIgnoreCase);
    }

    private static bool IsMortalCombatStateRepairIssue(ValidationIssue issue)
    {
        return string.Equals(issue.Code, "mortal_combat_state_missing", StringComparison.OrdinalIgnoreCase);
    }

    private static bool IsMortalTrainingSkillEvolutionRepairIssue(ValidationIssue issue)
    {
        return string.Equals(issue.Code, "skill_mastery_unknown_active_skill", StringComparison.OrdinalIgnoreCase) &&
               NormalizeRepairTargetPath(issue.FilePath).StartsWith("game_state/player/skill_mastery.json", StringComparison.OrdinalIgnoreCase);
    }

    private static bool IsMortalSkillProgressionShapeRepairIssue(ValidationIssue issue)
    {
        var code = issue.Code ?? string.Empty;
        if (!string.Equals(code, "expected_array", StringComparison.OrdinalIgnoreCase) &&
            !string.Equals(code, "expected_array_of_objects", StringComparison.OrdinalIgnoreCase) &&
            !string.Equals(code, "expected_string_array", StringComparison.OrdinalIgnoreCase))
        {
            return false;
        }

        var path = (issue.FilePath ?? string.Empty).Replace('\\', '/');
        if (!path.StartsWith("game_state/player/skills_active.json", StringComparison.OrdinalIgnoreCase) &&
            !path.StartsWith("game_state/player/skills_passive.json", StringComparison.OrdinalIgnoreCase) &&
            !path.StartsWith("game_state/player/skill_mastery.json", StringComparison.OrdinalIgnoreCase))
        {
            return false;
        }

        return path.Contains("activeSkillChanges", StringComparison.OrdinalIgnoreCase) ||
               path.Contains("passiveSkillChanges", StringComparison.OrdinalIgnoreCase) ||
               path.Contains("skillMasteryChanges", StringComparison.OrdinalIgnoreCase) ||
               path.Contains("removeActiveSkills", StringComparison.OrdinalIgnoreCase) ||
               path.Contains("removePassiveSkills", StringComparison.OrdinalIgnoreCase);
    }

    private static bool IsTrainingShowcaseSnapshotRepairIssue(ValidationIssue issue)
    {
        return string.Equals(issue.Code, "training_showcase_stale_source_actor_snapshot", StringComparison.OrdinalIgnoreCase);
    }

    private static bool IsMortalNpcRepairPath(string? path)
    {
        if (string.IsNullOrWhiteSpace(path))
            return false;

        var normalized = path.Replace('\\', '/');
        return normalized.StartsWith("game_state/npcs/", StringComparison.OrdinalIgnoreCase);
    }

    private static bool IsMortalNpcSkillObjectRepairPath(string? path)
    {
        if (string.IsNullOrWhiteSpace(path))
            return false;

        var normalized = path.Replace('\\', '/');
        return normalized.StartsWith("game_state/npcs/npc_core.json", StringComparison.OrdinalIgnoreCase) &&
               (normalized.Contains(".activeSkills[", StringComparison.OrdinalIgnoreCase) ||
                normalized.Contains(".passiveSkills[", StringComparison.OrdinalIgnoreCase));
    }

    private static bool IsAfterlifeSpiritualConflictActionCostRepairIssue(ValidationIssue issue)
    {
        var code = issue.Code ?? string.Empty;
        if (!code.StartsWith("afterlife_conflict_", StringComparison.OrdinalIgnoreCase))
            return false;

        return code.Contains("action_cost", StringComparison.OrdinalIgnoreCase) ||
               code.Contains("action_economy", StringComparison.OrdinalIgnoreCase) ||
               string.Equals(code, "afterlife_conflict_dice_value_not_authorized", StringComparison.OrdinalIgnoreCase) ||
               string.Equals(code, "afterlife_conflict_maneuver_changes_strain", StringComparison.OrdinalIgnoreCase);
    }

    private static bool IsGuardianTradeInventoryResolutionRepairIssue(ValidationIssue issue)
    {
        return (issue.Code ?? string.Empty).ToLowerInvariant() switch
        {
            "guardian_trade_request_missing_guardian_resolution" => true,
            "guardian_trade_request_missing_inventory_resolution" => true,
            "guardian_trade_request_missing_receipt_resolution" => true,
            _ => false
        };
    }

    private static bool IsAfterlifeSpiritualConflictRewardRepairIssue(ValidationIssue issue)
    {
        var code = issue.Code ?? string.Empty;
        return code.StartsWith("afterlife_conflict_reward_", StringComparison.OrdinalIgnoreCase);
    }

    private static bool IsAfterlifeEntityProfileScaffoldRepairIssue(ValidationIssue issue)
    {
        var code = issue.Code ?? string.Empty;
        return string.Equals(code, "afterlife_relevant_actor_missing_canonical_memory_owner", StringComparison.OrdinalIgnoreCase) ||
               code.StartsWith("afterlife_entity_profile_", StringComparison.OrdinalIgnoreCase) ||
               string.Equals(code, "afterlife_entity_profile_agency_goals_not_object", StringComparison.OrdinalIgnoreCase) ||
               code.StartsWith("afterlife_entity_profile_agency_goal_", StringComparison.OrdinalIgnoreCase) ||
               string.Equals(code, "afterlife_entity_profile_missing_progression_strategy", StringComparison.OrdinalIgnoreCase) ||
               code.StartsWith("afterlife_entity_profile_strategy_", StringComparison.OrdinalIgnoreCase) ||
               string.Equals(code, "afterlife_entity_profile_missing_ledger", StringComparison.OrdinalIgnoreCase) ||
               code.StartsWith("afterlife_entity_profile_ledger_", StringComparison.OrdinalIgnoreCase) ||
               string.Equals(code, "special_art_learning_receipts_not_array", StringComparison.OrdinalIgnoreCase) ||
               string.Equals(code, "special_art_learning_receipt_not_object", StringComparison.OrdinalIgnoreCase) ||
               string.Equals(code, "incomplete_special_art_learning_receipt", StringComparison.OrdinalIgnoreCase) ||
               code.StartsWith("invalid_special_art_learning_", StringComparison.OrdinalIgnoreCase) ||
               code.StartsWith("special_art_learning_", StringComparison.OrdinalIgnoreCase);
    }

    private static bool IsAcceptedTurnOutputArtifactRepairIssue(ValidationIssue issue)
    {
        return (issue.Code ?? string.Empty).ToLowerInvariant() switch
        {
            "accepted_turn_missing_narrative_response" => true,
            "accepted_turn_empty_narrative_response" => true,
            "accepted_turn_stale_narrative_response" => true,
            "accepted_turn_stale_player_facing_output_after_canonical_repair" => true,
            "narrative_response_technical_repair_leak" => true,
            "accepted_turn_invalid_narrative_json_root" => true,
            "accepted_turn_invalid_narrative_json" => true,
            "narrative_response_unknown_field" => true,
            "narrative_response_missing_timestamp" => true,
            "narrative_response_invalid_timestamp" => true,
            "accepted_turn_empty_interface_updates" => true,
            "accepted_turn_stale_interface_updates" => true,
            "accepted_turn_invalid_interface_updates_root" => true,
            "accepted_turn_invalid_interface_updates_json" => true,
            "interface_updates_missing_timestamp" => true,
            "interface_updates_invalid_timestamp" => true,
            "interface_updates_missing_payload" => true,
            "interface_updates_unknown_field" => true,
            "missing_gm_thoughts" => true,
            "accepted_turn_stale_debug_logs" => true,
            "invalid_debug_logs_json_root" => true,
            "invalid_debug_logs_json" => true,
            "debug_logs_unknown_field" => true,
            "debug_logs_missing_timestamp" => true,
            "debug_logs_invalid_timestamp" => true,
            _ => false
        };
    }

    private static ValidationRepairHarnessPacket BuildAcceptedTurnOutputArtifactRepairPacket(
        IReadOnlyList<ValidationIssue> outputArtifactErrors)
    {
        var targetFiles = outputArtifactErrors
            .Select(ResolveAcceptedTurnOutputArtifactTargetPath)
            .Where(path => !string.IsNullOrWhiteSpace(path))
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .OrderBy(path => path, StringComparer.OrdinalIgnoreCase)
            .ToList();

        var repairsNarrative = targetFiles.Contains("output/narrative_response.json", StringComparer.OrdinalIgnoreCase);
        var repairsInterface = targetFiles.Contains("output/interface_updates.json", StringComparer.OrdinalIgnoreCase);
        var repairsDebugLog = targetFiles.Contains("output/debug_logs.json", StringComparer.OrdinalIgnoreCase);

        var expectedShape = new List<string>();
        if (repairsNarrative)
        {
            expectedShape.Add(
                "output/narrative_response.json must be a fresh JSON object for the current accepted turn: { \"response\": \"player-facing narrative text\", \"timestamp\": \"ISO-8601 UTC timestamp\" }.");
        }
        if (repairsInterface)
        {
            expectedShape.Add(
                "output/interface_updates.json must be a fresh JSON object for the same accepted turn: { \"dialogueOptions\": [ { \"text\": \"visible option\", \"inputValue\": \"player input\" } ], \"timestamp\": \"ISO-8601 UTC timestamp\" }.");
        }
        if (repairsDebugLog)
        {
            expectedShape.Add(
                "output/debug_logs.json must be a fresh JSON object for the current accepted turn: { \"gm_thoughts_markdown\": \"## Охват NPC-анализа\\n...\", \"timestamp\": \"ISO-8601 UTC timestamp\" }.");
            expectedShape.Add(
                "gm_thoughts_markdown must contain a separate `## Охват NPC-анализа` / `## NPC Scope` section before full Actor Brain reasoning blocks for every relevant actor.");
            expectedShape.Add(
                "If no NPC, Guardian, faction, or other actor meaningfully acts or changes, explicitly preserve an actorless scope and explain why; do not invent an actor.");
        }

        var steps = new List<string>
        {
            "Open game_state/control/validation_repair_request.json first and repair only the listed accepted-turn output artifact errors and targetFiles."
        };
        if (repairsNarrative)
        {
            steps.Add(
                "Rewrite output/narrative_response.json with a fresh non-empty response for this same player action; preserve the already accepted narrative meaning instead of inventing a new turn.");
            steps.Add(
                "If validation_repair_request.json says player-facing output is stale after canonical state repair, base the rewritten narrative on the current canonical game_state files, not the pre-repair wording.");
            steps.Add(
                "If validation reports narrative_response_technical_repair_leak, rewrite response as an in-world scene only; keep validation/repair/JSON/storage details out of player-facing prose.");
        }
        if (repairsInterface)
        {
            steps.Add(
                "Rewrite output/interface_updates.json dialogueOptions/inputValue choices so they match the repaired canonical state and current player-facing narrative.");
        }
        if (repairsDebugLog)
        {
            steps.Add(
                "Rewrite output/debug_logs.json.gm_thoughts_markdown only because output/debug_logs.json is listed in targetFiles. Preserve every already valid full Actor Brain block and its exact `Изменения состояния` journal/ledger command or canonical surface; repair only the invalid or stale portions and refresh the file timestamp.");
            steps.Add(
                "For any relevant actor, keep the full Actor Brain fields from debugLogTemplate, including profile inputs, motivation, constraints, at least two strategies with benefit/risk, the chosen strategy, rejected alternatives, actions, and exact state changes.");
            steps.Add(
                "Preserve the exact actor-memory surface already used: Mortal NPC `NPCJournals[].journalEntries[]`; Guardian `guardianThoughtJournalUpdates` or `UpdateGuardians.addMusings`; resident `residentThoughtJournalUpdates`; existing afterlife entity actor-owned `ledger/progressionLedger`; existing Shining faction `shiningFactionChronicleUpdates`.");
            steps.Add(
                "If the accepted scope was genuinely actorless, preserve the actorless scope explanation and omit Actor Brain actor blocks; do not invent an actor for output repair.");
        }
        steps.Add("Do not touch canonical game_state files unless validation_repair_request.json lists a canonical state error as well.");
        steps.Add(
            "After all listed output artifacts are repaired, call Complete-BoeValidationRepair as the last action, or create game_state/control/validation_repair_ready.json with exact sessionId/requestId/turnNumber from validation_repair_request.json.");

        var doNotDo = new List<string>
        {
            "Do not write ready/turn_complete.json for validation repair.",
            "Do not create a new turn, reroll, advance time, or change player choice while repairing missing output artifacts.",
            "Do not mention JSON, validation, repair, canonical state, arrays, file paths, field names, or storage mechanics inside output/narrative_response.json.response.",
            "Do not read implementation code such as BookOfEternityClient/**/*.cs to infer repair rules; use this packet, validation_repair_request.json, GM docs, and session files."
        };
        if (repairsDebugLog)
        {
            doNotDo.Add(
                "Do not shorten a valid Actor Brain block or replace its exact journal/ledger surface with generic wording such as `state unchanged` or `the prior memory remains valid`.");
        }
        else
        {
            doNotDo.Add(
                "Do not rewrite output/debug_logs.json because it is not listed in targetFiles; preserve its valid Actor Brain and exact journal/ledger surface unchanged.");
        }

        return new ValidationRepairHarnessPacket
        {
            Kind = "accepted_turn_output_artifact_repair",
            Priority = "high",
            Title = "Accepted turn output artifact repair",
            TargetFiles = targetFiles,
            ExpectedShape = expectedShape,
            Steps = steps,
            DebugLogTemplate = repairsDebugLog
                ? BuildAcceptedTurnOutputArtifactDebugLogTemplate()
                : string.Empty,
            DoNotDo = doNotDo
        };
    }

    private static string ResolveAcceptedTurnOutputArtifactTargetPath(ValidationIssue issue)
    {
        var normalized = NormalizeRepairTargetPath(issue.FilePath);
        if (normalized.StartsWith("output/narrative_response.json", StringComparison.OrdinalIgnoreCase))
            return "output/narrative_response.json";
        if (normalized.StartsWith("output/interface_updates.json", StringComparison.OrdinalIgnoreCase))
            return "output/interface_updates.json";
        if (normalized.StartsWith("output/debug_logs.json", StringComparison.OrdinalIgnoreCase))
            return "output/debug_logs.json";

        return (issue.Code ?? string.Empty).ToLowerInvariant() switch
        {
            "accepted_turn_missing_narrative_response" or
            "accepted_turn_empty_narrative_response" or
            "accepted_turn_stale_narrative_response" or
            "narrative_response_technical_repair_leak" or
            "accepted_turn_invalid_narrative_json_root" or
            "accepted_turn_invalid_narrative_json" or
            "narrative_response_unknown_field" or
            "narrative_response_missing_timestamp" or
            "narrative_response_invalid_timestamp" => "output/narrative_response.json",

            "accepted_turn_empty_interface_updates" or
            "accepted_turn_stale_interface_updates" or
            "accepted_turn_invalid_interface_updates_root" or
            "accepted_turn_invalid_interface_updates_json" or
            "interface_updates_missing_timestamp" or
            "interface_updates_invalid_timestamp" or
            "interface_updates_missing_payload" or
            "interface_updates_unknown_field" => "output/interface_updates.json",

            "missing_gm_thoughts" or
            "accepted_turn_stale_debug_logs" or
            "invalid_debug_logs_json_root" or
            "invalid_debug_logs_json" or
            "debug_logs_unknown_field" or
            "debug_logs_missing_timestamp" or
            "debug_logs_invalid_timestamp" => "output/debug_logs.json",

            _ => string.Empty
        };
    }

    private static ValidationRepairHarnessPacket BuildNpcScopeDeclarationRepairPacket(
        IReadOnlyList<ValidationIssue> npcScopeErrors)
    {
        var actorNames = CollectRepairActorNames(npcScopeErrors)
            .OrderBy(actor => actor, StringComparer.OrdinalIgnoreCase)
            .ToList();
        var missingFields = npcScopeErrors
            .Select(issue => issue.Code ?? "npc_scope_validation_error")
            .Where(code => !string.IsNullOrWhiteSpace(code))
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .OrderBy(code => code, StringComparer.OrdinalIgnoreCase)
            .ToList();

        var targetFiles = npcScopeErrors
            .Select(issue => NormalizeRepairTargetPath(issue.FilePath))
            .Where(path => !string.IsNullOrWhiteSpace(path))
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .OrderBy(path => path, StringComparer.OrdinalIgnoreCase)
            .ToList();

        if (!targetFiles.Contains("output/debug_logs.json", StringComparer.OrdinalIgnoreCase))
            targetFiles.Add("output/debug_logs.json");

        return new ValidationRepairHarnessPacket
        {
            Kind = "npc_scope_declaration_repair",
            Priority = "high",
            Title = "NPC Scope declaration repair",
            TargetFiles = targetFiles,
            MissingFields = missingFields,
            ExpectedShape = new List<string>
            {
                "Repair only output/debug_logs.json.gm_thoughts_markdown unless validation_repair_request.json lists additional state errors.",
                "The NPC Scope block must declare Mode, Relevant actors, Why relevant, Actors outside scope, and Why outside scope.",
                "World-progression, Guardian-centric, and Mixed modes require at least one actor in Relevant actors.",
                "Scene-local may use Relevant actors: нет / none only when no NPC, Guardian, faction, resident, opponent, or other actor-specific structured state changed.",
                "If Relevant actors is not empty, add a reasoning section with an exact `### <actor name>` block and a full Actor Brain decision audit for every listed actor."
            },
            SafeCorrectionRules = new List<string>
            {
                "Use actor names that are visible in the current turn, current state, pending snapshot, or listed validation errors.",
                "For afterlife spiritual conflict turns, include the player soul, active Guardian or teacher, and current opponent/trial actor when they act, anchor, or receive structured state.",
                "Use Scene-local with empty actors only for purely player/internal output with no structured actor mutations.",
                "Keep the accepted player action and already written canonical state; repair the reasoning declaration instead of inventing a new event."
            },
            Steps = new List<string>
            {
                "Open game_state/control/validation_repair_request.json and output/debug_logs.json.",
                "Replace or patch only gm_thoughts_markdown so it starts with a complete `## NPC Scope` block.",
                "Choose the narrowest correct mode: Scene-local, World-progression, Guardian-centric, or Mixed.",
                "Fill Relevant actors as comma-separated canonical names, or `нет` only if Scene-local and no actor-specific state changed.",
                "Add one full Actor Brain block per relevant actor using every field in debugLogTemplate, including two distinct strategies with explicit benefit/risk and exact state changes.",
                "After the markdown is repaired, call Complete-BoeValidationRepair as the last action, or create validation_repair_ready.json with exact metadata from validation_repair_request.json."
            },
            CanonicalActorNames = actorNames,
            DebugLogTemplate = BuildNpcScopeDeclarationDebugLogTemplate(actorNames),
            DoNotDo = new List<string>
            {
                "Do not create a new turn, reroll dice, advance time, or change the player choice while repairing NPC Scope.",
                "Do not delete meaningful actor state just to make Scene-local with empty actors pass.",
                "Do not list an actor in Relevant actors without a matching `### <actor name>` reasoning block.",
                "Do not read implementation code such as BookOfEternityClient/**/*.cs to infer repair rules; use this packet, validation_repair_request.json, templates, and session files."
            }
        };
    }

    private static ValidationRepairHarnessPacket BuildGuardianPendingCreationMaterializationRepairPacket(
        IReadOnlyList<ValidationIssue> creationErrors)
    {
        var targetFiles = creationErrors
            .Select(issue => NormalizeRepairTargetPath(issue.FilePath))
            .Where(path => !string.IsNullOrWhiteSpace(path))
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .OrderBy(path => path, StringComparer.OrdinalIgnoreCase)
            .ToList();

        if (!targetFiles.Contains("game_state/meta/guardians.json", StringComparer.OrdinalIgnoreCase))
            targetFiles.Add("game_state/meta/guardians.json");
        if (!targetFiles.Contains("output/debug_logs.json", StringComparer.OrdinalIgnoreCase))
            targetFiles.Add("output/debug_logs.json");

        return new ValidationRepairHarnessPacket
        {
            Kind = "guardian_pending_creation_materialization_repair",
            Priority = "high",
            Title = "Startup pending Guardian creation materialization repair",
            TargetFiles = targetFiles,
            TemplateRefs = new List<string>
            {
                "TaskGuides/CLI_Step_Main.txt",
                "OtherGuides/Afterlife_Contract_Matrix.md",
                "Examples/E_CLI_Afterlife_Turns.txt"
            },
            CanonicalCreateSkeleton = BuildGuardianPendingCreationCanonicalCreateSkeleton(),
            AllowedEnums = BuildGuardianPendingCreationAllowedEnums(),
            ExpectedShape = new List<string>
            {
                "Read game_state/meta/guardians.json.pendingGuardianCreation as the startup request authority. For mode=freeform, use soulName and description; for mode=system_preset, use the exact requested preset identity.",
                "For a freeform New Game startup request, repair through the supported Guardian create surface `UpdateGuardians.create`: `UpdateGuardians`: [{ `command`: `create`, `data`: <full canonical Guardian> }]. The `data` object is the authority for the new Guardian identity.",
                "Use harnessRepairPackets[].canonicalCreateSkeleton as the bounded machine-readable starting point. Replace placeholder ids/names/prose from pendingGuardianCreation, but keep the same command/data shape and canonical field families.",
                "Keep scalar validation fields valid while replacing prose: relationshipData.lastInteraction must be null or ISO 8601, abodePower.tier must match abodePower.currentPower, timestamps must be ISO 8601, and abodePower.history may stay empty unless you can write canonical entries with timestamp/change/reason/source.",
                "Do not satisfy guardian_materialized_without_create_surface by editing only materialized mirrors. `guardians[]` and `activeGuardian` must match the create result, but the explicit `UpdateGuardians.create` command=create data=<full canonical Guardian> surface must be present in the repaired response.",
                "The accepted game_state/meta/guardians.json must contain the new Guardian in guardians[] with stable guardianId, displayName/canonicalName, title/domain/originType, abode identity, relationshipData/currentReputation, loreFragments, musings, trade/project/profile arrays or empty containers required by the canonical Guardian shape.",
                "Canonical Guardian create data must include at least 7 loreFragments. Use the allowed category and requiredReputation values from harnessRepairPackets[].allowedEnums.",
                "Canonical musings must include turn, topic, mood, and thought/text. Use only guardianMusingTopics and guardianMusingMoods from harnessRepairPackets[].allowedEnums.",
                "Do not invent relationshipData.guardianRoleToPlayer for normal startup Guardians. Omit it unless repairing an explicit foundation/former patron branch; in v1 the only allowed value is former_patron.",
                "activeGuardian must mirror the created Guardian, and chaosSeaNavigation.currentAbodeId must point to the created/discovered abode when the Guardian is now accessible.",
                "After guardians[] and activeGuardian are materialized, remove pendingGuardianCreation from guardians.json. Leaving pendingGuardianCreation unresolved or beside the materialized Guardian keeps /хранители empty or pending.",
                "output/debug_logs.json.gm_thoughts_markdown must be Guardian-centric or Mixed, list the Guardian by player-facing name in Relevant actors, and include situation/thoughts/actions explaining the materialization."
            },
            SafeCorrectionRules = new List<string>
            {
                "Preserve the player's requested soul name and freeform Guardian description as the source of the created Guardian's identity and domain details.",
                "If the already written Guardian has useful prose/domain details, copy/reshape them into UpdateGuardians[0].data as the full canonical Guardian object instead of leaving them only in guardians[] or activeGuardian.",
                "For New Game freeform startup repairs, do not keep the request as pending-only: the accepted repair must create the requested Guardian or remain rejected.",
                "For system_preset requests, do not substitute a similar Guardian; materialize the exact preset or route to the already materialized exact preset.",
                "Repair only the startup Guardian creation fields and matching debug-log actor coverage unless validation_repair_request.json lists additional errors."
            },
            Steps = new List<string>
            {
                "Open game_state/control/validation_repair_request.json first and repair only the listed startup Guardian creation errors in place.",
                "Use Read-BoeJson -RelativePath 'game_state/meta/guardians.json' and inspect pendingGuardianCreation before editing.",
                "Write or repair `UpdateGuardians.create` first: `UpdateGuardians`: [{ `command`: `create`, `data`: <full canonical Guardian> }]. This create command is the authority; do not repair only guardians[]/activeGuardian.",
                "Start from harnessRepairPackets[].canonicalCreateSkeleton. Replace placeholders with pendingGuardianCreation soulName/description, keep 7 loreFragments, keep mood/musings enum values from allowedEnums, and omit guardianRoleToPlayer unless the repair request explicitly names a former_patron foundation branch.",
                "Before writing, preserve canonical scalar constraints from the skeleton: lastInteraction stays null until a real ISO timestamp exists, abodePower.tier stays derived from currentPower, all timestamp fields stay ISO 8601, and abodePower.history stays empty unless every entry has timestamp/change/reason/source.",
                "For materialization, write a full canonical Guardian into guardians[], set activeGuardian to the matching mirror, update chaosSeaNavigation.currentAbodeId, and remove pendingGuardianCreation only after those roots are present.",
                "Repair output/debug_logs.json.gm_thoughts_markdown with Guardian-centric/Mixed scope, the Guardian's player-facing name in Relevant actors, and a matching Guardian Thoughts block.",
                "Write repaired files with Write-BoeJson, then call Complete-BoeValidationRepair as the last action, or create validation_repair_ready.json with exact metadata from validation_repair_request.json."
            },
            DebugLogTemplate = string.Join(
                Environment.NewLine,
                "## Охват NPC-анализа",
                "Режим: Guardian-centric",
                "Релевантные акторы: <имя создаваемого Хранителя>",
                "Почему они релевантны: pendingGuardianCreation просит материализовать этого Хранителя как доступного покровителя души.",
                "Акторы вне охвата: NPC смертного мира, другие Хранители",
                "Почему они вне охвата: repair закрывает только стартовое создание Хранителя.",
                "",
                "## Guardian Thoughts",
                "### <имя создаваемого Хранителя>",
                "- Ситуация: душа завершает стартовую встречу с Хранителем, описанным в pendingGuardianCreation.",
                "- Мысли: Хранитель оценивает форму души и решает, как открыть свою Обитель.",
                "- Действия: Хранитель материализуется с полной canonical identity, activeGuardian mirror, abode navigation и без stale pendingGuardianCreation."),
            DoNotDo = new List<string>
            {
                "Do not create a new turn or write ready/turn_complete.json during validation repair.",
                "Do not delete pendingGuardianCreation without materializing the requested Guardian into both guardians[] and activeGuardian.",
                "Do not keep a direct materialized Guardian object that lacks the supported UpdateGuardians.create semantics and full canonical Guardian shape.",
                "Do not repair only guardians[] or activeGuardian without UpdateGuardians command=create data=<full canonical Guardian>.",
                "Do not invent relationshipData.guardianRoleToPlayer such as mentor, teacher, patron, owner, bonded, or ally; omit the field unless the explicit valid value is former_patron.",
                "Do not use sentinel strings such as startup_turn for relationshipData.lastInteraction; use null or a real ISO 8601 timestamp.",
                "Do not hand-write abodePower.tier from prose; derive it from abodePower.currentPower.",
                "Do not add non-canonical abodePower.history entries such as delta/resultingPower-only audit notes; leave history empty instead.",
                "Do not use arbitrary musing topics/moods, arbitrary mood.current values, or fewer than 7 loreFragments.",
                "Do not leave the Guardian only in narrative prose while /хранители still has no canonical Guardian.",
                "Do not rewrite the requested freeform Guardian into an unrelated system preset or Mortal NPC.",
                "Do not read implementation code such as BookOfEternityClient/**/*.cs to infer startup Guardian rules; use this packet, validation_repair_request.json, afterlife docs/examples, and session state."
            }
        };
    }

    private static JsonObject BuildGuardianPendingCreationCanonicalCreateSkeleton()
    {
        const int startupAbodePower = 10;
        var startupTimestamp = DateTimeOffset.UtcNow.ToString("O");
        var guardianData = new JsonObject
        {
            ["guardianId"] = "guardian_<stable_slug_from_pending_guardian_creation>",
            ["canonicalName"] = "<guardian name from pendingGuardianCreation.description>",
            ["originType"] = "freeform",
            ["domain"] = "<short domain inferred from pendingGuardianCreation.description>",
            ["nameVariants"] = new JsonObject
            {
                ["default"] = "<guardian display name>",
                ["feminine"] = "<optional feminine form or same display name>",
                ["masculine"] = "<optional masculine form or same display name>",
                ["neutral"] = "<optional neutral form or same display name>"
            },
            ["manifestation"] = new JsonObject
            {
                ["currentDisplayName"] = "<guardian display name>",
                ["formFlexibility"] = "selective",
                ["currentPresentationStyle"] = "neutral",
                ["currentPronouns"] = "они/их",
                ["appearanceDescription"] = "<visual form from pendingGuardianCreation.description>"
            },
            ["manifestationHistory"] = new JsonArray(),
            ["abode"] = new JsonObject
            {
                ["abodeId"] = "abode_<stable_slug_from_guardian>",
                ["name"] = "<guardian abode name>",
                ["isDiscovered"] = true
            },
            ["personalityProfile"] = new JsonObject
            {
                ["archetype"] = "<short archetype>",
                ["speechPattern"] = "<how the guardian speaks>",
                ["coreValues"] = new JsonArray("memory", "discipline", "truth")
            },
            ["relationshipData"] = new JsonObject
            {
                ["currentReputation"] = 0,
                ["reputationHistory"] = new JsonArray(),
                ["lastInteraction"] = null
            },
            ["abodePower"] = new JsonObject
            {
                ["currentPower"] = startupAbodePower,
                ["tier"] = AbodePowerRules.GetTierLabel(startupAbodePower),
                ["lastUpdatedAt"] = startupTimestamp,
                ["history"] = new JsonArray()
            },
            ["guardianRelationships"] = new JsonArray(),
            ["questManagement"] = new JsonObject
            {
                ["availableQuests"] = new JsonArray(),
                ["activeQuests"] = new JsonArray(),
                ["completedQuests"] = new JsonArray()
            },
            ["gachaSystem"] = new JsonObject
            {
                ["chargesPerReturn"] = 1,
                ["chargesUsedThisReturn"] = 0,
                ["gachaHistory"] = new JsonArray()
            },
            ["mood"] = new JsonObject
            {
                ["current"] = "focused",
                ["intensity"] = 40,
                ["reason"] = "startup Guardian materialization",
                ["since"] = 1
            },
            ["loreFragments"] = new JsonArray
            {
                BuildGuardianLoreFragmentSkeleton(1, "personal_history", 0),
                BuildGuardianLoreFragmentSkeleton(2, "domain_mastery", 0),
                BuildGuardianLoreFragmentSkeleton(3, "soul_mechanics", 50),
                BuildGuardianLoreFragmentSkeleton(4, "lost_world", 50),
                BuildGuardianLoreFragmentSkeleton(5, "other_guardians", 130),
                BuildGuardianLoreFragmentSkeleton(6, "cosmic_secret", 130),
                BuildGuardianLoreFragmentSkeleton(7, "personal_history", 230)
            },
            ["musings"] = new JsonArray
            {
                new JsonObject
                {
                    ["turn"] = 1,
                    ["topic"] = "soul_assessment",
                    ["mood"] = "contemplative",
                    ["thought"] = "<short private thought about the newly created soul>"
                }
            }
        };

        return new JsonObject
        {
            ["authoritySurface"] = "UpdateGuardians.create",
            ["UpdateGuardians"] = new JsonArray
            {
                new JsonObject
                {
                    ["command"] = "create",
                    ["data"] = guardianData
                }
            },
            ["materializedMirrorRules"] = new JsonObject
            {
                ["guardians[]"] = "copy the same created Guardian data into guardians[] after the create surface is present",
                ["activeGuardian"] = "mirror the same created Guardian",
                ["chaosSeaNavigation.currentAbodeId"] = "use data.abode.abodeId",
                ["pendingGuardianCreation"] = "remove only after create data, guardians[], activeGuardian, and chaosSeaNavigation are present"
            },
            ["forbiddenDirectMirrorRepair"] = new JsonArray(
                "do not repair only guardians[]",
                "do not repair only activeGuardian",
                "do not omit UpdateGuardians[0].command=create",
                "do not invent relationshipData.guardianRoleToPlayer for normal startup")
        };
    }

    private static JsonObject BuildGuardianLoreFragmentSkeleton(int index, string category, int requiredReputation) => new()
    {
        ["fragmentId"] = $"lore_<stable_slug>_{index}",
        ["category"] = category,
        ["title"] = $"<planned lore fragment {index}>",
        ["content"] = index <= 2 ? $"<visible starter lore fragment {index}>" : null,
        ["requiredReputation"] = requiredReputation
    };

    private static JsonObject BuildGuardianPendingCreationAllowedEnums() => new()
    {
        ["guardianMusingTopics"] = new JsonArray(
            "soul_assessment",
            "domain_insight",
            "guardian_politics",
            "chaos_sea",
            "personal_reflection",
            "quest_planning"),
        ["guardianMusingMoods"] = new JsonArray(
            "content",
            "intrigued",
            "concerned",
            "amused",
            "proud",
            "disappointed",
            "wary",
            "nostalgic",
            "determined",
            "melancholic",
            "excited",
            "contemplative",
            "irritated",
            "hopeful"),
        ["guardianMoodCurrent"] = new JsonArray(
            "welcoming",
            "contemplative",
            "energized",
            "melancholic",
            "irritated",
            "proud",
            "suspicious",
            "playful",
            "focused",
            "nostalgic"),
        ["guardianLoreFragmentCategories"] = new JsonArray(
            "personal_history",
            "cosmic_secret",
            "domain_mastery",
            "lost_world",
            "other_guardians",
            "soul_mechanics"),
        ["guardianLoreFragmentRequiredReputation"] = new JsonArray("0", "50", "130", "230"),
        ["guardianRoleToPlayerV1"] = new JsonArray("former_patron")
    };

    private static ValidationRepairHarnessPacket BuildGuardianScopeRepairPacket(
        IReadOnlyList<ValidationIssue> guardianScopeErrors,
        IReadOnlyCollection<string>? guardianActorNameHints = null)
    {
        var actorNames = CollectRepairActorNames(guardianScopeErrors, guardianActorNameHints)
            .OrderBy(actor => actor, StringComparer.OrdinalIgnoreCase)
            .ToList();

        if (actorNames.Count == 0)
            actorNames.Add("активный Хранитель из game_state/meta/guardians.json");

        var targetFiles = guardianScopeErrors
            .Select(issue => NormalizeRepairTargetPath(issue.FilePath))
            .Where(path => !string.IsNullOrWhiteSpace(path))
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .OrderBy(path => path, StringComparer.OrdinalIgnoreCase)
            .ToList();

        if (!targetFiles.Contains("output/debug_logs.json", StringComparer.OrdinalIgnoreCase))
            targetFiles.Add("output/debug_logs.json");
        if (!targetFiles.Contains("game_state/meta/guardians.json", StringComparer.OrdinalIgnoreCase))
            targetFiles.Add("game_state/meta/guardians.json");
        if (guardianScopeErrors.Any(issue => string.Equals(issue.Code, "guardian_project_materialized_state_outside_authority", StringComparison.OrdinalIgnoreCase)) &&
            !targetFiles.Contains("game_state/meta/guardian_projects.json", StringComparer.OrdinalIgnoreCase))
        {
            targetFiles.Add("game_state/meta/guardian_projects.json");
        }

        var actorList = string.Join(", ", actorNames);
        var headingExamples = string.Join(", ", actorNames.Select(actor => $"### {actor}"));
        var template = BuildGuardianScopeDebugLogTemplate(actorList, actorNames);

        return new ValidationRepairHarnessPacket
        {
            Kind = "guardian_scope_repair",
            Priority = "high",
            Title = "Guardian scope and materialized mirror repair",
            TargetFiles = targetFiles,
            CanonicalActorNames = actorNames,
            Steps = new List<string>
            {
                "In game_state/meta/guardians.json, do not treat current activeGuardian or current guardians[] as authority. Rewrite activeGuardian and guardians[] to the kernel-authoritative Guardian state reconstructed from the validated pre-turn snapshot plus authorized same-turn Guardian mutations.",
                "If game_state/meta/guardian_projects.json is listed, rewrite activeProjects to the kernel-authoritative project tracker state from the validated pre-turn snapshot plus authorized same-turn project commands; do not invent or start a project just because the current materialized file contains it.",
                $"In output/debug_logs.json.gm_thoughts_markdown, set NPC scope mode to Guardian-centric or Mixed and include exactly these Guardian names in Relevant actors: {actorList}.",
                $"Add a reasoning block for every listed Guardian. Minimal required heading shapes: {headingExamples}; use client-recognized bullet labels exactly like - Ситуация:, - Мысли:, and - Действия:.",
                "After file repairs are complete, call Complete-BoeValidationRepair as the last action, or create game_state/control/validation_repair_ready.json with exact sessionId/requestId/turnNumber from the current validation_repair_request.json."
            },
            DebugLogTemplate = template,
            DoNotDo = new List<string>
            {
                "Do not copy stale mirror-only activeGuardian aliases into Relevant actors.",
                "Do not use raw guardianId as the actor name in Relevant actors or reasoning headings.",
                "Do not remove the Guardian mutation from the turn just to silence scope validation unless the mutation was actually unintended.",
                "Do not read implementation code such as BookOfEternityClient/**/*.cs to infer repair rules; use this repair packet, the validation request, GM docs, and session snapshot/control files."
            }
        };
    }

    private static ValidationRepairHarnessPacket BuildActorReasoningSubpointRepairPacket(
        IReadOnlyList<ValidationIssue> actorReasoningErrors)
    {
        var actorNames = actorReasoningErrors
            .Select(issue => NormalizeRepairActorName(issue.Actor))
            .Where(actor => !string.IsNullOrWhiteSpace(actor))
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .OrderBy(actor => actor, StringComparer.OrdinalIgnoreCase)
            .ToList();

        if (actorNames.Count == 0)
            actorNames.Add("актор из ошибки validation_repair_request.json");

        var targetFiles = actorReasoningErrors
            .Select(issue => NormalizeRepairTargetPath(issue.FilePath))
            .Where(path => !string.IsNullOrWhiteSpace(path))
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .OrderBy(path => path, StringComparer.OrdinalIgnoreCase)
            .ToList();

        if (!targetFiles.Contains("output/debug_logs.json", StringComparer.OrdinalIgnoreCase))
            targetFiles.Add("output/debug_logs.json");

        var actorList = string.Join(", ", actorNames);

        return new ValidationRepairHarnessPacket
        {
            Kind = "actor_reasoning_subpoint_repair",
            Priority = "high",
            Title = "Actor reasoning block/subpoint repair",
            TargetFiles = targetFiles,
            CanonicalActorNames = actorNames,
            Steps = new List<string>
            {
                $"In output/debug_logs.json.gm_thoughts_markdown, repair or create a missing reasoning block for exactly these actors: {actorList}.",
                "Inside every listed actor block, include separate bullet subpoints with these exact client-recognized labels: - Текущая локация:, - Ситуация:, - Данные профиля:, - Мотивация:, - Ограничения:, - Мысли:, - Варианты стратегий:, - Выбранная стратегия:, - Почему альтернативы отвергнуты:, - Действия:, and - Изменения состояния:.",
                "Use - Текущая локация: to state where the NPC is now, whether it remains there, or whether it moves to a known current/same-turn location.",
                "Under - Варианты стратегий: list at least two genuinely different numbered strategies; each strategy must explicitly contain Выгода: and Риск: on the same numbered line.",
                "Name the final choice under - Выбранная стратегия: and explain why the other options lost under - Почему альтернативы отвергнуты:.",
                "Under - Изменения состояния: name the exact canonical journal/state delta, or explicitly state that no state delta is justified; never hide an intended change only in prose.",
                "Keep the actor heading shape as ### <actor name>. Do not merge the required subpoints into one paragraph or one bullet.",
                "Preserve unrelated accepted debug log content and do not create a new turn.",
                "After file repairs are complete, call Complete-BoeValidationRepair as the last action, or create game_state/control/validation_repair_ready.json with exact sessionId/requestId/turnNumber from the current validation_repair_request.json."
            },
            DebugLogTemplate = BuildActorReasoningSubpointDebugLogTemplate(actorNames),
            DoNotDo = new List<string>
            {
                "Do not write ready/turn_complete.json for validation repair.",
                "Do not use slash-only labels or a single combined sentence when the validator requested separate subpoints.",
                "Do not read implementation code such as BookOfEternityClient/**/*.cs to infer repair rules; use this packet, the validation request, GM docs, and session files."
            }
        };
    }

    private static ValidationRepairHarnessPacket BuildActorMemoryPersistenceRepairPacket(
        IReadOnlyList<ValidationIssue> actorMemoryErrors,
        IReadOnlyCollection<string>? guardianActorNameHints = null)
    {
        var actorNames = CollectRepairActorNames(actorMemoryErrors, guardianActorNameHints)
            .OrderBy(actor => actor, StringComparer.OrdinalIgnoreCase)
            .ToList();
        var targetFiles = actorMemoryErrors
            .Select(issue => NormalizeRepairTargetPath(issue.FilePath))
            .Where(path => !string.IsNullOrWhiteSpace(path))
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .OrderBy(path => path, StringComparer.OrdinalIgnoreCase)
            .ToList();

        var hasGuardianMemoryError = actorMemoryErrors.Any(issue =>
            string.Equals(issue.Code, "guardian_relevant_actor_missing_thought_journal_delta", StringComparison.OrdinalIgnoreCase) ||
            IsGuardianThoughtJournalShapeRepairIssue(issue));
        if (hasGuardianMemoryError)
        {
            targetFiles.RemoveAll(path =>
                string.Equals(path, "game_state/meta/guardians.json", StringComparison.OrdinalIgnoreCase) ||
                path.StartsWith(GuardianThoughtJournalState.StatePath, StringComparison.OrdinalIgnoreCase));
        }
        if (hasGuardianMemoryError &&
            !targetFiles.Contains(GuardianThoughtJournalState.StatePath, StringComparer.OrdinalIgnoreCase))
        {
            targetFiles.Add(GuardianThoughtJournalState.StatePath);
        }

        if (actorMemoryErrors.Any(issue =>
                string.Equals(issue.Code, "mortal_npc_relevant_actor_missing_thought_journal_delta", StringComparison.OrdinalIgnoreCase)) &&
            !targetFiles.Contains("game_state/npcs/npc_journals.json", StringComparer.OrdinalIgnoreCase))
        {
            targetFiles.Add("game_state/npcs/npc_journals.json");
        }

        if (actorMemoryErrors.Any(issue =>
                string.Equals(issue.Code, "afterlife_resident_relevant_actor_missing_thought_journal_delta", StringComparison.OrdinalIgnoreCase)) &&
            !targetFiles.Contains(GuardianAbodeResidentState.StatePath, StringComparer.OrdinalIgnoreCase))
        {
            targetFiles.Add(GuardianAbodeResidentState.StatePath);
        }

        if (!targetFiles.Contains("output/debug_logs.json", StringComparer.OrdinalIgnoreCase))
            targetFiles.Add("output/debug_logs.json");

        return new ValidationRepairHarnessPacket
        {
            Kind = "actor_memory_persistence_repair",
            Priority = "high",
            Title = "Relevant actor thought-journal persistence repair",
            TargetFiles = targetFiles,
            TemplateRefs = new List<string>
            {
                "OtherGuides/Actor_Brain_2_0.md",
                "Templates/MORTAL_NPC_UPDATE_TEMPLATE.md",
                "OtherGuides/Afterlife_Contract_Matrix.md",
                "Examples/E_CLI_Afterlife_Turns.txt"
            },
            CanonicalActorNames = actorNames,
            ExpectedShape = new List<string>
            {
                "A relevant actor that evaluated the player action and chose a strategy must gain a new canonical thought-journal entry in the same accepted turn.",
                $"Guardian memory repair writes canonical on-disk shape directly to {GuardianThoughtJournalState.StatePath}: {{ \"entries\": [ {{ \"entryId\": \"fresh-stable-id\", \"guardianId\": \"existing-guardian-id\", \"title\": \"short title\", \"summary\": \"Я ...\", \"turn\": <current turn>, \"timestamp\": \"ISO-8601 UTC\" }} ] }}. Preserve every old entry.",
                "guardianThoughtJournalUpdates is a first-pass response command surface, not the sole top-level canonical repair shape for a newly created journal file.",
                "Mortal NPC inner thoughts are appended to game_state/npcs/npc_journals.json NPCJournals[].journalEntries[] for the matching canonical NPC id/name.",
                $"Guardian Abode resident inner thoughts are appended through {GuardianAbodeResidentState.UpdateThoughtJournalProperty} and normalize into {GuardianAbodeResidentState.ThoughtJournalProperty}.",
                $"Other afterlife entity memory initializes gmThoughtsSummary when a profile is first materialized; every later decision appends ledger/progressionLedger evidence in {AfterlifeEntityProfileState.StatePath}, even when the current gmThoughtsSummary also changes.",
                $"A newly created Shining faction may initialize {ShiningAbodeState.FactionStrategicMemoryProperty}; every later decision appends {ShiningAbodeState.FactionChronicleProperty} through {ShiningAbodeState.FactionChronicleUpdatesProperty} in {ShiningAbodeState.StatePath}."
            },
            SafeCorrectionRules = new List<string>
            {
                "Add one concise first-person thought describing the actor's reaction, conclusion, fear, intent, or changed expectation; keep the Actor Brain strategy audit in debug logs and the actor's subjective memory in canonical state.",
                "Append a new entry with a fresh stable entryId/turn/timestamp where that journal contract supports them; do not mutate an old entry to imitate a new thought.",
                "Use the actor id and display name already present in canonical state; do not create a duplicate actor or rename the actor during memory repair.",
                "Patch only the listed actor journal surface and the matching Actor Brain state-change line in output/debug_logs.json; preserve the already accepted narrative, decision, relationships, inventory, currencies, and unrelated state."
            },
            Steps = new List<string>
            {
                "Open game_state/control/validation_repair_request.json and use errors[].actor plus errors[].code to choose the exact journal surface.",
                $"For Guardian thought-memory errors, append one valid first-person entry directly to the packet-listed {GuardianThoughtJournalState.StatePath} entries[] array. If the file is absent or malformed, create/repair the canonical root as {{ \"entries\": [...] }}; do not edit guardians.json unless a separate current repair error explicitly lists it.",
                "For mortal_npc_relevant_actor_missing_thought_journal_delta, append a new journalEntries[] object to the matching NPCJournals record; keep the thought in first person.",
                $"For afterlife_resident_relevant_actor_missing_thought_journal_delta, append a complete {GuardianAbodeResidentState.UpdateThoughtJournalProperty} entry for the matching residentId.",
                "For afterlife_entity_relevant_actor_missing_memory_ledger_delta, append actor-owned ledger/progressionLedger evidence to an existing profile; gmThoughtsSummary-only memory is valid only while first materializing a new profile.",
                $"For shining_faction_relevant_actor_missing_strategic_memory_delta, append one current-turn faction memory entry through {ShiningAbodeState.FactionChronicleUpdatesProperty}; preserve every previous chronicle entry. Do not treat a mutable strategicMemory rewrite as historical persistence for an existing faction.",
                "In output/debug_logs.json, update only the listed actor's - Изменения состояния: line so it names the exact journal command and canonical journal surface that this repair actually used.",
                "Do not rewrite output/narrative_response.json or re-resolve the actor decision; this repair persists the missing internal memory of the already authored response.",
                "After repairs are complete, call Complete-BoeValidationRepair as the last action, or create validation_repair_ready.json with exact metadata from the current validation_repair_request.json."
            },
            DoNotDo = new List<string>
            {
                "Do not replace the actor's first-person thought journal with an external chronicle, interaction log, or third-person scene summary.",
                "Do not persist schemaVersion or a guardianThoughtJournalUpdates-only root in game_state/meta/guardian_thought_journal.json; the repaired canonical file must contain entries[].",
                "Do not edit or delete previous journal entries; append a new entry for the current turn.",
                "Do not modify unrelated actors, prose, quests, factions, inventory, combat, currencies, or world state.",
                "Do not read implementation source to infer the repair; use this packet, its templateRefs, canonical state, and the validated repair request."
            }
        };
    }

    private static ValidationRepairHarnessPacket BuildFactionMaterializationRepairPacket(
        string coordinate,
        IReadOnlyList<ValidationIssue> factionIssues)
    {
        var isMortal = coordinate.StartsWith("mortal_faction:", StringComparison.Ordinal);
        var targetFiles = ResolveFactionMaterializationRepairTargetFiles(
            coordinate,
            factionIssues);
        var missingFields = factionIssues
            .Where(issue => HasExactMissingCodeComponent(issue.Code))
            .Select(issue => issue.FilePath)
            .Where(path => !string.IsNullOrEmpty(path))
            .Distinct(StringComparer.Ordinal)
            .OrderBy(path => path, StringComparer.Ordinal)
            .ToList();
        var expectedShape = new List<string>
        {
            "The exact faction coordinate has one immutable Faction Materialization v1 receipt; an accepted materializationId remains unchanged.",
            "Every governed materialization section is physically present in its exact state=populated or exact empty_by_design shape.",
            "Every faction-bound actor, location, and sidecar link resolves to the exact packet coordinate without changing another faction."
        };
        expectedShape.AddRange(
            factionIssues
                .Where(issue => issue.FactionRepairClassification.HasValue)
                .Select(issue => GetFactionMaterializationRepairClassification(
                    issue.FactionRepairClassification!.Value))
                .Distinct(StringComparer.Ordinal)
                .OrderBy(classification => classification, StringComparer.Ordinal));

        return new ValidationRepairHarnessPacket
        {
            Kind = "faction_materialization_repair",
            Priority = "critical",
            Title = $"Bounded Faction Materialization repair for {coordinate}",
            TargetFiles = targetFiles,
            TemplateRefs = isMortal
                ? new List<string> { "Templates/MORTAL_FACTION_UPDATE_TEMPLATE.md" }
                : new List<string>
                {
                    "OtherGuides/Afterlife_Contract_Matrix.md",
                    "Examples/E_CLI_Afterlife_Turns.txt"
                },
            CanonicalActorNames = new List<string> { coordinate },
            MissingFields = missingFields.Count == 0 ? null : missingFields,
            ExactFieldCorrections = factionIssues
                .Select(BuildExactFieldCorrection)
                .OrderBy(correction => correction.Path, StringComparer.Ordinal)
                .ThenBy(correction => correction.Code, StringComparer.Ordinal)
                .ToList(),
            ExpectedShape = expectedShape,
            SafeCorrectionRules = new List<string>
            {
                $"Change only the listed coordinate {coordinate} and the exact listed targetFiles; no other domain root is writable.",
                "Preserve all valid sections, accepted receipts, validated histories, chronicles, and every unrelated faction.",
                "Restore a missing bounded surface in place and keep every valid populated or exact empty governed section unchanged.",
                "Apply only corrections supported by the current validation issue and canonical evidence in the listed targets."
            },
            Steps = new List<string>
            {
                "Open game_state/control/validation_repair_request.json and only this packet's listed targetFiles and templateRefs.",
                $"Locate only the exact coordinate {coordinate}; do not resolve a faction from a display name, prose, file path, or another issue.",
                "Apply exactFieldCorrections one by one, changing only the named coordinate, path, and listed target root.",
                "Rerun raw Faction Materialization validation before normalization and do not accept a partially repaired bundle.",
                "After every exact repair passes, call Complete-BoeValidationRepair as the final action, or create validation_repair_ready.json with exact metadata from the current validation_repair_request.json."
            },
            DoNotDo = new List<string>
            {
                "Do not change another faction or broaden this repair to an unlisted target root.",
                "Do not change an accepted materializationId or replace an accepted historical receipt.",
                "Do not rewrite validated history, chronicles, or valid governed sections.",
                "Do not use whole-file replacement to repair one exact faction surface.",
                "Do not invent faction identity, links, sections, or content from names, tags, descriptions, or genre.",
                "Do not create a new turn or write ready/turn_complete.json during validation repair."
            }
        };
    }

    private static List<string> ResolveFactionMaterializationRepairTargetFiles(
        string coordinate,
        IReadOnlyList<ValidationIssue> factionIssues)
    {
        var targetFiles = new SortedSet<string>(StringComparer.Ordinal);
        string[] allowedTargets;
        if (coordinate.StartsWith("mortal_faction:", StringComparison.Ordinal))
        {
            targetFiles.Add("game_state/factions/faction_core.json");
            allowedTargets =
            [
                "game_state/factions/faction_core.json",
                "game_state/factions/faction_structure.json",
                "game_state/factions/faction_resources.json",
                "game_state/factions/faction_projects.json",
                "game_state/factions/faction_custom.json",
                "game_state/factions/faction_chronicles.json",
                "game_state/npcs/npc_core.json",
                "game_state/npcs/npc_journals.json",
                "game_state/npcs/npc_interaction_journal.json",
                "game_state/npcs/npc_masks.json",
                "game_state/npcs/npc_memory.json",
                "game_state/world/current_location.json",
                "game_state/world/world_map.json"
            ];
        }
        else
        {
            targetFiles.Add("game_state/meta/shining_abode_state.json");
            allowedTargets =
            [
                "game_state/meta/shining_abode_state.json",
                "game_state/meta/guardian_abode_residents.json",
                "game_state/meta/afterlife_entity_profiles.json",
                "game_state/meta/main_story_saref_state.json"
            ];
        }

        foreach (var issue in factionIssues)
        {
            foreach (var repairTarget in issue.RepairTargetFiles)
            {
                if (allowedTargets.Contains(
                        repairTarget,
                        StringComparer.Ordinal))
                {
                    targetFiles.Add(repairTarget);
                }
            }

            foreach (var allowedTarget in allowedTargets)
            {
                if (IssuePathNamesExactRepairRoot(issue.FilePath, allowedTarget))
                    targetFiles.Add(allowedTarget);
            }
        }

        return targetFiles.ToList();
    }

    private static bool IssuePathNamesExactRepairRoot(
        string? issuePath,
        string targetRoot)
    {
        if (issuePath == null ||
            !issuePath.StartsWith(targetRoot, StringComparison.Ordinal))
        {
            return false;
        }

        return issuePath.Length == targetRoot.Length ||
               issuePath[targetRoot.Length] is '.' or '[' or ':';
    }

    private static bool HasExactMissingCodeComponent(string? issueCode) =>
        (issueCode ?? string.Empty)
        .Split('_')
        .Contains("missing", StringComparer.Ordinal);

    private static string GetFactionMaterializationRepairClassification(
        FactionTouchKind classification) =>
        classification switch
        {
            FactionTouchKind.New => "new",
            FactionTouchKind.AlreadyMaterialized =>
                "already_materialized",
            FactionTouchKind.InvalidReceiptless =>
                "invalid_receiptless",
            _ => throw new ArgumentOutOfRangeException(
                nameof(classification),
                classification,
                "Unsupported faction repair classification.")
        };

    private static ValidationRepairHarnessPacket BuildFactionIdentityRepairPacket(
        IReadOnlyList<ValidationIssue> factionIdentityErrors)
    {
        var targetFiles = factionIdentityErrors
            .Select(issue => NormalizeRepairTargetPath(issue.FilePath))
            .Where(path => !string.IsNullOrWhiteSpace(path))
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .OrderBy(path => path, StringComparer.OrdinalIgnoreCase)
            .ToList();

        if (!targetFiles.Contains("game_state/factions/faction_core.json", StringComparer.OrdinalIgnoreCase))
            targetFiles.Add("game_state/factions/faction_core.json");
        if (!targetFiles.Contains("game_state/control/pending_turn_snapshot/game_state/factions/faction_core.json", StringComparer.OrdinalIgnoreCase))
            targetFiles.Add("game_state/control/pending_turn_snapshot/game_state/factions/faction_core.json");

        return new ValidationRepairHarnessPacket
        {
            Kind = "faction_identity_repair",
            Priority = "high",
            Title = "Mortal faction identity repair",
            TargetFiles = targetFiles,
            TemplateRefs = new List<string> { "Templates/MORTAL_FACTION_UPDATE_TEMPLATE.md" },
            ExpectedShape = new List<string>
            {
                "game_state/factions/faction_core.json contains canonical factions[] with stable factionId, name/displayName, description, status, reputation/influence, ranks/rankBranches, relations, projects, chronicle, resources, and custom states preserved.",
                "Every faction sidecar entry references an existing canonical factionId from factions[] unless the sidecar entry is removed as invalid speculative data.",
                "Same-turn temporary faction identity remains factionId = null, initialId = <stable temporary id>, isNewFaction = true until the canonical promotion path creates a permanent faction."
            },
            SafeCorrectionRules = new List<string>
            {
                "If the bad id was meant to update an existing faction, replace it with the exact existing canonical factionId from game_state/factions/faction_core.json.",
                "If the story truly introduced a durable missing faction, create the missing faction as a complete factions[] object before any sidecar references it.",
                "If the sidecar is speculative, duplicate, or belongs to a faction that should not exist, remove the invalid sidecar entry or retarget it to an existing canonical factionId.",
                "Preserve unrelated faction ranks, rankBranches, chronicles, relations, projects, resources, reputation, and custom states while repairing identity."
            },
            Steps = new List<string>
            {
                "Open Templates/MORTAL_FACTION_UPDATE_TEMPLATE.md before editing game_state/factions/*.",
                "Use game_state/control/pending_turn_snapshot/game_state/factions/faction_core.json as the authority for whether the faction was already permanent before this turn or was still a same-turn temporary faction.",
                "If the matching faction in pending_turn_snapshot has factionId = null plus a non-empty initialId, restore the current object to the same temporary identity shape: factionId = null, the exact initialId, and isNewFaction = true.",
                "If the matching faction in pending_turn_snapshot has a non-empty factionId, use that exact permanent factionId and remove initialId/isNewFaction from the existing faction object.",
                "For unknown faction ids, choose exactly one correction path: reference an existing canonical factionId, create the missing faction as a complete factions[] object, or remove/retarget the invalid sidecar entry.",
                "After restoring the identity shape, preserve the intended turn content updates around the faction instead of replacing the whole file with a minimal skeleton.",
                "After file repairs are complete, call Complete-BoeValidationRepair as the last action, or create game_state/control/validation_repair_ready.json with exact sessionId/requestId/turnNumber from the current validation_repair_request.json."
            },
            DoNotDo = new List<string>
            {
                "Do not invent a permanent factionId from the faction name.",
                "Do not convert a same-turn temporary faction into an existing permanent faction just because validation mentions existing faction wording.",
                "Do not leave faction sidecar entries pointing at missing faction ids.",
                "Do not delete unrelated faction ranks, resources, projects, chronicles, or reputation details to silence identity validation.",
                "Do not read implementation code such as BookOfEternityClient/**/*.cs to infer repair rules; use this repair packet, the validation request, and session snapshot/control files."
            }
        };
    }

    private static ValidationRepairHarnessPacket BuildMortalFactionResourceRepairPacket(
        IReadOnlyList<ValidationIssue> factionResourceErrors)
    {
        var targetFiles = factionResourceErrors
            .Select(issue => NormalizeRepairTargetPath(issue.FilePath))
            .Where(path => !string.IsNullOrWhiteSpace(path))
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .OrderBy(path => path, StringComparer.OrdinalIgnoreCase)
            .ToList();

        if (!targetFiles.Contains("game_state/factions/faction_resources.json", StringComparer.OrdinalIgnoreCase))
            targetFiles.Add("game_state/factions/faction_resources.json");
        if (!targetFiles.Contains("game_state/factions/faction_core.json", StringComparer.OrdinalIgnoreCase))
            targetFiles.Add("game_state/factions/faction_core.json");

        var missingFields = CollectRepairMissingFields(factionResourceErrors);
        var expectedShape = new List<string>
        {
            "canonical_faction_resource_entry_missing_required_fields means a canonical faction resource entry was written as a partial delta; repair it into a full resource object.",
            "metaResources entries require resourceName, currentStockpile, incomePerCycle, and upkeepPerCycle.",
            "strategicGoods entries require resourceName, currentStockpile, and incomePerCycle.",
            "Keep faction resources linked to the existing canonical faction and preserve unrelated ranks, branches, projects, chronicles, relations, and custom states."
        };

        if (missingFields.Count > 0)
            expectedShape.Add($"Validator reported these exact missing fields: {string.Join(", ", missingFields)}.");

        var steps = new List<string>
        {
            "Open Templates/MORTAL_FACTION_UPDATE_TEMPLATE.md before editing game_state/factions/*.",
            "Find the resource entry named by validation_repair_request.json.errors[].filePath and expand it to a full resource object instead of deleting the faction or replacing the file with a minimal skeleton.",
            "If the entry belongs to metaResources, include resourceName, currentStockpile, incomePerCycle, and upkeepPerCycle; if it belongs to strategicGoods, include resourceName, currentStockpile, and incomePerCycle.",
            "Use numeric values for currentStockpile/incomePerCycle/upkeepPerCycle and keep player-facing resource names meaningful in the current Mortal World scene.",
            "After file repairs are complete, call Complete-BoeValidationRepair as the last action, or create validation_repair_ready.json with exact metadata from the current validation_repair_request.json."
        };

        if (missingFields.Count > 0)
            steps.Insert(2, $"Patch the exact missing fields first: {string.Join(", ", missingFields)}.");

        return new ValidationRepairHarnessPacket
        {
            Kind = "mortal_faction_resource_repair",
            Priority = "high",
            Title = "Mortal faction resource entry repair",
            TargetFiles = targetFiles,
            TemplateRefs = new List<string> { "Templates/MORTAL_FACTION_UPDATE_TEMPLATE.md" },
            MissingFields = missingFields.Count == 0 ? null : missingFields,
            ExpectedShape = expectedShape,
            SafeCorrectionRules = new List<string>
            {
                "Complete the resource entry in place whenever it represents a real faction resource introduced or updated by the turn.",
                "Remove the entry only if it is speculative, duplicate, or not supported by the accepted scene.",
                "Preserve all unrelated faction state while repairing resource shape."
            },
            Steps = steps,
            DoNotDo = new List<string>
            {
                "Do not delete the whole faction to silence a resource-entry validation error.",
                "Do not leave resource entries as identity-only stubs or partial deltas.",
                "Do not read implementation code such as BookOfEternityClient/**/*.cs to infer resource rules; use this packet, the validation request, GM docs, and session files."
            }
        };
    }

    private static ValidationRepairHarnessPacket BuildMortalLocationTransitionRepairPacket(
        IReadOnlyList<ValidationIssue> locationTransitionErrors)
    {
        var issueCodes = locationTransitionErrors
            .Select(issue => issue.Code ?? "validation_error")
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .OrderBy(code => code, StringComparer.OrdinalIgnoreCase)
            .ToList();
        var actorNames = CollectRepairActorNames(locationTransitionErrors)
            .OrderBy(actor => actor, StringComparer.OrdinalIgnoreCase)
            .ToList();
        var targetFiles = locationTransitionErrors
            .Select(issue => NormalizeRepairTargetPath(issue.FilePath))
            .Where(path => !string.IsNullOrWhiteSpace(path))
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .OrderBy(path => path, StringComparer.OrdinalIgnoreCase)
            .ToList();

        if (!targetFiles.Contains("game_state/world/current_location.json", StringComparer.OrdinalIgnoreCase))
            targetFiles.Add("game_state/world/current_location.json");
        if (!targetFiles.Contains("game_state/world/world_map.json", StringComparer.OrdinalIgnoreCase))
            targetFiles.Add("game_state/world/world_map.json");
        if (locationTransitionErrors.Any(issue =>
                string.Equals(issue.Code, "npc_unknown_current_location_id", StringComparison.OrdinalIgnoreCase)) &&
            !targetFiles.Contains("game_state/npcs/npc_core.json", StringComparer.OrdinalIgnoreCase))
            targetFiles.Add("game_state/npcs/npc_core.json");

        return new ValidationRepairHarnessPacket
        {
            Kind = "mortal_location_transition_repair",
            Priority = "high",
            Title = "Mortal location transition repair",
            TargetFiles = targetFiles,
            TemplateRefs = new List<string> { "Templates/MORTAL_LOCATION_TRANSITION_TEMPLATE.md" },
            CanonicalActorNames = actorNames,
            ExpectedShape = new List<string>
            {
                $"Observed location/map issue codes: {string.Join(", ", issueCodes)}.",
                "Register any durable new location in game_state/world/world_map.json before current_location.json or NPC currentLocationId references it.",
                "game_state/world/current_location.json must reference a known world_map location id, name, region, description, exits, and last-events summary.",
                "Durable location objects must carry required arrays/collections: knownExits, adjacencyMap, factionControl, locationStorages, and activeThreats. Use [] for empty locationStorages/activeThreats/adjacencyMap when no entries exist.",
                "Durable location objects must carry both internalDifficultyProfile and externalDifficultyProfile with combat/environment/social/exploration facets.",
                "Every outdoor durable/current location must carry a canonical biome value: TemperateForest, ColdForest, Swamp, Urban, Plains, Mountains, Desert, Coast, or Unique. Use biomeDescription when biome is Unique.",
                "World-map link previews must include targetName, targetCoordinates, estimatedInternalDifficultyProfile, and estimatedExternalDifficultyProfile.",
                "NPC currentLocationId values must point only to known ids from world_map; same-turn scene color should not invent canonical ids.",
                "Same-turn world_map new location coordinates must be unique and must not conflict with existing map coordinates."
            },
            SafeCorrectionRules = new List<string>
            {
                "Register the destination in world_map first, then update current_location.json and NPC currentLocationId/currentLocationName to that known id.",
                "Use one stable location id for the destination across world_map, current_location, NPCs, exits, and debug reasoning; do not alternate ids for the same room.",
                "Fix duplicate coordinates by moving one same-turn new location to unique adjacent coordinates or by merging duplicate entries that describe the same place.",
                "If the new place is narrative color inside the current room rather than a durable location, keep current_location unchanged and phrase the action as happening within the existing location.",
                "Do not repair an unknown location by deleting NPCs, quests, faction links, exits, or map history that should still reference the canonical destination."
            },
            Steps = new List<string>
            {
                "Open Templates/MORTAL_LOCATION_TRANSITION_TEMPLATE.md before editing game_state/world/current_location.json, game_state/world/world_map.json, or NPC location ids.",
                "Decide whether the destination is a durable location or only narrative color inside the current location.",
                "For a durable destination, create or repair the world_map entry first, including stable id, visible name, description, region, exits, and unique coordinates.",
                "Patch required arrays on every durable/current location object: knownExits, adjacencyMap, factionControl, locationStorages, and activeThreats.",
                "Patch difficulty profile objects on every durable/current location object before completing repair.",
                "Patch biome on every outdoor durable/current location before completing repair; choose the canonical biome that matches the scene, and add biomeDescription for Unique.",
                "Patch each world-map link preview with targetName, targetCoordinates, and both estimated difficulty profiles.",
                "After the map entry exists, update current_location.json and any moved NPC currentLocationId/currentLocationName to the known id/name.",
                "For duplicate same-turn coordinates, assign unique coordinates before completing repair.",
                "After repairs are complete, call Complete-BoeValidationRepair as the last action, or create validation_repair_ready.json with exact metadata from the current validation_repair_request.json."
            },
            DoNotDo = new List<string>
            {
                "Do not point current_location.json to a location id that is absent from world_map.",
                "Do not point NPC currentLocationId to an unknown location.",
                "Do not create two same-turn locations with identical coordinates unless they are merged into one canonical location.",
                "Do not turn a purely descriptive corner of the current room into a new canonical location just to satisfy a narrative sentence."
            }
        };
    }

    private static ValidationRepairHarnessPacket BuildMortalWorldMapAdjacencyRepairPacket(
        IReadOnlyList<ValidationIssue> worldMapAdjacencyErrors)
    {
        var issueCodes = worldMapAdjacencyErrors
            .Select(issue => issue.Code ?? "validation_error")
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .OrderBy(code => code, StringComparer.OrdinalIgnoreCase)
            .ToList();
        var targetFiles = worldMapAdjacencyErrors
            .Select(issue => NormalizeRepairTargetPath(issue.FilePath))
            .Where(path => !string.IsNullOrWhiteSpace(path))
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .OrderBy(path => path, StringComparer.OrdinalIgnoreCase)
            .ToList();

        if (!targetFiles.Contains("game_state/world/world_map.json", StringComparer.OrdinalIgnoreCase))
            targetFiles.Add("game_state/world/world_map.json");
        if (!targetFiles.Contains("game_state/world/current_location.json", StringComparer.OrdinalIgnoreCase))
            targetFiles.Add("game_state/world/current_location.json");

        return new ValidationRepairHarnessPacket
        {
            Kind = "mortal_world_map_adjacency_repair",
            Priority = "high",
            Title = "Mortal world-map adjacency/link repair",
            TargetFiles = targetFiles,
            TemplateRefs = new List<string> { "Templates/MORTAL_LOCATION_TRANSITION_TEMPLATE.md" },
            ExpectedShape = new List<string>
            {
                $"Observed world-map reference codes: {string.Join(", ", issueCodes)}.",
                "world_map adjacency/link/storage/threat references must point to an existing locationId in world_map or to a same-turn newLocations.initialId that is fully materialized in the same repair.",
                "If the target is a durable place the player or NPC can later reach, materialize it as a full world_map location with stable id, visible name, description, region, exits, and coordinates before linking to it.",
                "If the target is only a clue, direction, route hint, or descriptive corner inside the current location, do not create an adjacency/link; keep it in current_location summary/knownExits/point text until it becomes reachable."
            },
            SafeCorrectionRules = new List<string>
            {
                "Materialize the missing location only when the accepted narrative made it a real reachable place or a moved actor's durable destination.",
                "Remove or downgrade a speculative link when the prose only mentioned a route clue, hidden panel, service passage, storage hint, or offscreen direction.",
                "Preserve existing valid locations, coordinates, exits, storages, threats, and current_location text while fixing only the unknown target/source references.",
                "When creating a new location for a link, keep one stable id across world_map, current_location exits, NPC locations, debug reasoning, quests, and map history."
            },
            Steps = new List<string>
            {
                "Open Templates/MORTAL_LOCATION_TRANSITION_TEMPLATE.md and game_state/world/world_map.json before repairing unknown target/source world-map links.",
                "For each validation error, inspect the exact path and actual unknown target/source id from validation_repair_request.json.",
                "Decide whether the unknown target is a durable reachable location or only a narrative hint inside the current scene.",
                "For a durable location, add/repair the full world_map location first, then add or correct the adjacency/link/storage/threat reference to that known id.",
                "For a narrative hint, remove the invalid adjacency/link/storage/threat command and preserve the clue in current_location narrative fields or quest log instead.",
                "After repairs are complete, call Complete-BoeValidationRepair as the last action, or create validation_repair_ready.json with exact metadata from the current validation_repair_request.json."
            },
            DoNotDo = new List<string>
            {
                "Do not leave adjacency, linkUpdates, storageUpdates, or threat updates pointing to unknown target/source ids.",
                "Do not create a bare id-only location just to satisfy a link; a durable location must be a full location object.",
                "Do not delete unrelated map history or valid exits to silence one unknown target.",
                "Do not read implementation code such as BookOfEternityClient/**/*.cs to infer map rules; use this packet, the template, validation request, and session state."
            }
        };
    }

    private static ValidationRepairHarnessPacket BuildMortalBootstrapMaterializationRepairPacket(
        IReadOnlyList<ValidationIssue> mortalBootstrapErrors)
    {
        var issueCodes = mortalBootstrapErrors
            .Select(issue => issue.Code ?? "validation_error")
            .Where(code => !string.IsNullOrWhiteSpace(code))
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .OrderBy(code => code, StringComparer.OrdinalIgnoreCase)
            .ToList();
        var targetFiles = mortalBootstrapErrors
            .Select(issue => NormalizeMortalBootstrapRepairTargetPath(issue.FilePath))
            .Where(path => !string.IsNullOrWhiteSpace(path))
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .OrderBy(path => path, StringComparer.OrdinalIgnoreCase)
            .ToList();

        if (!targetFiles.Contains("game_state/control/mortal_bootstrap_scaffold.json", StringComparer.OrdinalIgnoreCase))
            targetFiles.Insert(0, "game_state/control/mortal_bootstrap_scaffold.json");

        return new ValidationRepairHarnessPacket
        {
            Kind = "mortal_bootstrap_materialization_repair",
            Priority = "high",
            Title = "Mortal bootstrap materialization repair",
            TargetFiles = targetFiles,
            TemplateRefs = new List<string>
            {
                "Templates/MORTAL_LOCATION_TRANSITION_TEMPLATE.md",
                "Templates/MORTAL_FACTION_UPDATE_TEMPLATE.md",
                "Templates/MORTAL_NPC_UPDATE_TEMPLATE.md",
                "Templates/MORTAL_SKILL_PROGRESSION_TEMPLATE.md"
            },
            ExpectedShape = new List<string>
            {
                $"Repair these bootstrap issue codes first: {string.Join(", ", issueCodes)}.",
                "The first Mortal World turn must materialize only explicit GM-authored player-facing anchors from game_state/control/mortal_bootstrap_scaffold.json into canonical current-world files; the neutral baseline does not require factions, quests, or setting-owned location mechanics.",
                "Every structuredGmAuthority.playerProgression, carryingRules, or factionMechanics record must name the exact canonicalPath and contain a non-empty values object that repeats every authorized canonical value; faction records also identify factionId. Empty objects, reasons, unrelated paths, and prose do not grant authority.",
                "Current-world codex entries must describe this Mortal World, not afterlife lore or a previous world; sourceFile must start with current_world/ (for example current_world/world_setting.json), not lore/current_world/.",
                "Readable document/book inventory items need matching detail authority so /книги or document-reading surfaces can show contents.",
                "Starting items need canonical quality/rarity, durability when inspectable, and a valid equipment/accessory slot when item type implies one.",
                "Starting inventory items need the full canonical item shape: itemId/existedId, image_prompt, isConsumption, isContainer, requiresTwoHands, contentsPath, durability as a percentage string such as 100% (never bare number 100), and array-shaped text/bonus fields when present.",
                "Item journalEntries must be an array of non-empty string notes, not objects; do not write { text, turn, summary } objects into journalEntries[].",
                "equipmentSlot and accessoryForSlot must use canonical slot names, arrays of canonical slot names, or null; do not invent slots such as Pocket/Hands when the contract expects a fixed enum.",
                "Mortal World Relevant actors must be backed by a persistent NPC/faction/quest/inventory surface, or moved to Actors outside scope when they are only background scenery.",
            "The player character is not an NPC persistence target. If the current protagonist is named in Relevant actors, mark them as player character and do not create NPCsInScene/UpdateNPCs for them.",
            "NPCsInScene is only for actors physically present in currentLocationData. Offscreen voices, people behind a door, nearbyExitLocationId actors, and route pressure do not belong in NPCsInScene for the current room.",
            "Only an explicit structuredGmAuthority.actorCapabilities[] declaration with capability=canTeach and required=true requires a matching NPC with teacherProfile.canTeach=true and non-empty teacherProfile.skills[]. playerAuthoredStart prose alone never creates this requirement.",
            "Only an explicit structuredGmAuthority.actorCapabilities[] declaration with capability=canTrade and required=true requires a matching NPC with tradeState.canTrade=true, an explicit valid merchantProfile, relationshipLevel, and summary. Never infer merchantProfile from role, occupation, class, progression type, name, description, genre, or keywords. Leave tradeInventory absent until a trade vitrine is actually ready; if tradeInventory is present, it must be a full object, never a scalar/string/array placeholder.",
            "Every structuredGmAuthority.playerSkills[] entry is explicit GM authority. Preserve each matching active/passive skill and the active-skill mastery entry; do not infer any skill from playerAuthoredStart prose.",
            "worldEventRequirements requires the client-authored opening event to remain in game_state/world/world_events.json.worldEventsLog so /новости_мира is useful immediately after incarnation.",
            "Faction custom sidecars must carry full Custom State Objects: stateId/name, currentValue, minValue, maxValue, description, progressionRule { changePerTurn, description }, and thresholds[]; if you only need a narrative note, use faction_core chronicle instead.",
            "Active threats must be full objects, not strings: threatId/name/longTermGoal plus threatArchetype { motivation, method } and impactProfile { primaryTargetType, primaryTargetId, primaryTargetName, primaryImpact, baseImpactValue }. Use canonical enum values or keep activeThreats empty for vague pressure.",
            "current_location and world_map must agree on shared identity, coordinates, and navigation links. Location type, traversal, safety, biome, difficulty, faction control, factions, chronicles, and quests are setting-owned and remain absent or empty unless the GM explicitly materializes them with complete canonical data."
            },
            SafeCorrectionRules = new List<string>
            {
                "Use mortal_bootstrap_scaffold.json as the checklist and patch only targetFiles unless validation errors explicitly name another file.",
                "Preserve the accepted narrative and player-facing hooks; repair their canonical backing data instead of deleting the hooks.",
                "Prefer adding missing detail records, sidecar fields, codex links, and canonical enum values over replacing whole files with minimal skeletons."
            },
            Steps = new List<string>
            {
                "Open game_state/control/mortal_bootstrap_scaffold.json first and compare it with the listed targetFiles.",
                "Patch current-world lore/codex first: add at least one current-world codex entry with sourceFile starting current_world/ and remove stale reused-world references if validation names them.",
                "Patch readable document authority: every readable book/document item needs a concrete text/detail surface linked to that item.",
                "Patch inventory items: canonicalize quality/rarity, write durability as a percentage string such as 100% for intact inspectable items, and use valid equipment/accessory slots for wearable items.",
                "Patch complete item shape fields exactly where validation names them: image_prompt, existedId, isConsumption, isContainer, requiresTwoHands, contentsPath, durability, equipmentSlot, and accessoryForSlot.",
                "Patch item journalEntries as a JSON array of non-empty strings, not objects; preserve useful text by flattening each malformed object into one player-facing note string.",
                "Patch string-array fields as JSON arrays of strings, not scalar text or semicolon-delimited strings.",
                "Patch output/debug_logs.json Relevant actors: keep the current protagonist as player character, materialize real non-player Mortal actors through NPC/faction/quest/inventory surfaces, or move background objects to Actors outside scope with a clear reason.",
                "Patch requested training anchors only from structuredGmAuthority.actorCapabilities: for each required canTeach declaration, add the explicitly selected NPC to NPCsInScene or UpdateNPCs with teacherProfile.canTeach=true, relationshipLevel, summary, and skills[] entries containing skillId, skillName, displayName, skillKind, and masteryLevel.",
                "Patch requested trade anchors only from structuredGmAuthority.actorCapabilities: for each required canTrade declaration, add the explicitly selected NPC to NPCsInScene or UpdateNPCs with tradeState.canTrade=true, an explicit valid tradeState.merchantProfile, relationshipLevel, summary, and a player-facing role that makes /торговля_нпс discoverable. Never infer the profile from NPC prose or genre.",
                "Patch explicit GM-authored starter competencies: copy every structuredGmAuthority.playerSkills[] entry into the matching skills_active.json or skills_passive.json canonical collection and restore matching skill_mastery.json data for active skills.",
                "Patch opening world news: restore each worldEventRequirements.requiredEventIds entry in world_events.json.worldEventsLog and keep its title/description grounded in playerAuthoredStart.startingCircumstances.",
                "For a promised trader, use tradeBlockedReason only when canTrade=false, and keep it a string explaining the story gate. When canTrade=true, omit tradeBlockedReason or keep it as an empty string; never write null/object/array.",
                "For a promised trader, do not include inventory in UpdateNPCs when updating an existing NPC. Use inventory: [] only for a genuinely new full NPC object; use NPC inventory delta commands for existing NPC inventory changes.",
                "For a promised trader, do not fabricate a partial tradeInventory just to prove the NPC can trade. Leave tradeInventory absent so /торговля_нпс can request a vitrine, unless you are writing a complete canonical tradeInventory object with valid items and matching receipts.",
                "Patch NPCsInScene location scope: if an actor is behind a door, near nearbyExitLocationId, in another corridor, or only heard offscreen, remove them from NPCsInScene and represent them through narrative/location/quest/faction memory or UpdateNPCs at their actual location only when they are durable known actors.",
                "Patch factions only when the GM explicitly created them: complete faction custom/progression sidecar fields with full Custom State Objects, or move narrative-only pressure into an explicitly materialized faction chronicle. Keep neutral bootstrap faction collections empty.",
                "Patch active threats: either write complete Active Threat Objects with canonical enum values, or remove vague string-only threats and represent pressure through location events/faction chronicle.",
                "Patch location/map data: synchronize shared identity, coordinates, and navigation links. Do not invent location type, traversal, safety, biome, difficulty, faction control, faction chronicles, or quest mechanics merely to satisfy bootstrap.",
                "After repairs are complete, call Complete-BoeValidationRepair as the last action, or create validation_repair_ready.json with exact metadata from the current validation_repair_request.json."
            },
            DoNotDo = new List<string>
            {
                "Do not delete an explicitly GM-authored opening book, letter, NPC, faction, quest, map exit, or codex hook just to silence validation; neutral empty faction and quest collections do not require replacement entities.",
                "Do not copy afterlife lore or previous-world lore into current-world bootstrap files.",
                "Do not write item durability as bare numbers such as 100; use percentage strings such as 100%.",
                "Do not write item journalEntries as objects; journalEntries[] entries must be non-empty strings.",
                "Do not delete a structuredGmAuthority canTeach declaration just to avoid creating its complete teacherProfile.",
                "Do not delete a structuredGmAuthority canTrade declaration just to avoid creating its complete tradeState.",
                "Do not delete an explicit structuredGmAuthority player skill or the client-authored opening world event to reduce bootstrap scope.",
                "Do not write tradeInventory as a scalar, string, array, or empty placeholder; omit it until a complete trade vitrine exists.",
                "Do not read implementation code such as BookOfEternityClient/**/*.cs to infer bootstrap rules; use this packet, the scaffold, templates, and validation errors."
            }
        };
    }

    private static ValidationRepairHarnessPacket BuildMortalNpcLocationRepairPacket(
        IReadOnlyList<ValidationIssue> mortalNpcLocationErrors)
    {
        var actorNames = CollectRepairActorNames(mortalNpcLocationErrors)
            .OrderBy(actor => actor, StringComparer.OrdinalIgnoreCase)
            .ToList();
        var targetFiles = BuildMortalNpcTargetFiles(mortalNpcLocationErrors, includeNpcCoreWhenMissing: false);
        var touchesNpcCore = targetFiles.Contains("game_state/npcs/npc_core.json", StringComparer.OrdinalIgnoreCase);
        var touchesDebugLog = targetFiles.Contains("output/debug_logs.json", StringComparer.OrdinalIgnoreCase);
        var knownSceneLocationErrors = mortalNpcLocationErrors
            .Where(issue =>
                string.Equals(issue.Code, "npc_scene_missing_current_location_id", StringComparison.OrdinalIgnoreCase) ||
                string.Equals(issue.Code, "npc_scene_location_mismatch", StringComparison.OrdinalIgnoreCase))
            .ToList();
        var sameTurnNewLocationErrors = mortalNpcLocationErrors
            .Where(issue =>
                string.Equals(issue.Code, "current_location_new_scene_missing_initial_id_for_npc_scene", StringComparison.OrdinalIgnoreCase) ||
                string.Equals(issue.Code, "npc_scene_missing_initial_location_id", StringComparison.OrdinalIgnoreCase) ||
                string.Equals(issue.Code, "npc_scene_initial_location_mismatch", StringComparison.OrdinalIgnoreCase) ||
                string.Equals(issue.Code, "npc_initial_location_same_turn_target_unknown", StringComparison.OrdinalIgnoreCase) ||
                string.Equals(issue.Code, "npc_same_turn_initial_location_requires_null_current_location", StringComparison.OrdinalIgnoreCase))
            .ToList();
        var steps = new List<string>
        {
            "Open Templates/MORTAL_NPC_UPDATE_TEMPLATE.md before repairing NPC location validation errors."
        };
        if (touchesNpcCore)
        {
            if (knownSceneLocationErrors.Count > 0)
            {
                var expectedRules = knownSceneLocationErrors
                    .Select(issue => string.IsNullOrWhiteSpace(issue.Expected) ? "currentLocationId = currentLocationData.locationId" : issue.Expected.Trim())
                    .Distinct(StringComparer.OrdinalIgnoreCase)
                    .OrderBy(rule => rule, StringComparer.OrdinalIgnoreCase)
                    .ToList();
                steps.Add($"For NPCsInScene entries in a known current scene location, set {string.Join("; ", expectedRules)} and keep initialLocationId as JSON null unless validation separately reports a same-turn new-location initialId for that entry.");
            }
            if (sameTurnNewLocationErrors.Count > 0)
            {
                steps.Add("For NPCsInScene entries in a same-turn new location, set initialLocationId to currentLocationData.initialId or the matching newLocations.initialId, set currentLocationId to JSON null, and keep currentLocationName as the visible location name.");
            }
            steps.Add("If a persistent NPC location is missing in game_state/npcs/npc_core.json, set currentLocationId/currentLocationName from the intended scene or move command; do not leave both location ids empty.");
            steps.Add("Keep NPCsInScene and NPCs entries as full canonical objects; do not replace them with short display-only rows.");
        }
        if (touchesDebugLog)
        {
            steps.Add("In output/debug_logs.json.gm_thoughts_markdown, add a current location / Текущая локация subpoint inside each listed NPC reasoning block.");
            steps.Add("The current-location subpoint must say where the NPC is now and whether the NPC stays there or moves during this turn.");
        }
        steps.Add("After repairs are complete, call Complete-BoeValidationRepair as the last action, or create validation_repair_ready.json with exact metadata from the current validation_repair_request.json.");

        return new ValidationRepairHarnessPacket
        {
            Kind = "mortal_npc_location_repair",
            Priority = "high",
            Title = "Mortal NPC same-turn location repair",
            TargetFiles = targetFiles,
            TemplateRefs = new List<string> { "Templates/MORTAL_NPC_UPDATE_TEMPLATE.md" },
            CanonicalActorNames = actorNames,
            ExpectedShape = new List<string>
            {
                "Same-turn scene NPCs live in game_state/npcs/npc_core.json under NPCsInScene and/or NPCs as full objects, not as partial name-only stubs.",
                "NPCsInScene is only for actors physically present in currentLocationData; actors heard behind a door, placed near a nearbyExitLocationId, or waiting in another corridor are offscreen for the current scene.",
                "For a known current location: NPCsInScene.currentLocationId = currentLocationData.locationId, initialLocationId = JSON null, currentLocationName = visible current location name.",
                "For a same-turn new location: NPCsInScene.initialLocationId = currentLocationData.initialId or matching newLocations.initialId, currentLocationId = JSON null, currentLocationName = visible current location name.",
                "For an already persistent NPC moved by the turn: keep permanent npcId, set currentLocationId/currentLocationName to the destination, and do not invent a same-turn initialId."
            },
            SafeCorrectionRules = new List<string>
            {
                "Patch only game_state/npcs/npc_core.json unless another file is explicitly listed by validation_repair_request.json.",
                "Use the current turn/request location authority already present in session state or pending snapshot; do not invent a location id from prose.",
                "Preserve intended NPC personality, goals, relationshipLock, journals, and unrelated NPC entries while repairing only the location fields."
            },
            Steps = steps,
            DoNotDo = new List<string>
            {
                "Do not read implementation code such as BookOfEternityClient/**/*.cs to infer NPC location rules.",
                "Do not silence the issue by deleting a meaningful NPC from Relevant actors or NPCsInScene.",
                "Do not keep an offscreen voice, nearbyExitLocationId actor, or corridor/door pressure in NPCsInScene for the current room.",
                "Do not set currentLocationId to JSON null for NPCsInScene when validation expects currentLocationId for a known current location.",
                "Do not set currentLocationId to a non-null value for a same-turn new location NPC that validator says must use initialLocationId."
            }
        };
    }

    private static ValidationRepairHarnessPacket BuildMortalNpcScopeRepairPacket(
        IReadOnlyList<ValidationIssue> mortalNpcScopeErrors)
    {
        var actorNames = CollectRepairActorNames(mortalNpcScopeErrors)
            .OrderBy(actor => actor, StringComparer.OrdinalIgnoreCase)
            .ToList();
        var targetFiles = BuildMortalNpcTargetFiles(mortalNpcScopeErrors, includeNpcCoreWhenMissing: true);
        if (!targetFiles.Contains("output/debug_logs.json", StringComparer.OrdinalIgnoreCase))
            targetFiles.Add("output/debug_logs.json");

        return new ValidationRepairHarnessPacket
        {
            Kind = "mortal_npc_scope_repair",
            Priority = "high",
            Title = "Mortal NPC relevant-actor scope repair",
            TargetFiles = targetFiles,
            TemplateRefs = new List<string> { "Templates/MORTAL_NPC_UPDATE_TEMPLATE.md" },
            CanonicalActorNames = actorNames,
            ExpectedShape = new List<string>
            {
                "Every structured Mortal NPC update must be covered by output/debug_logs.json NPC scope: the actor name appears in Relevant actors and has a reasoning block.",
                "The reasoning block must include current location, situation, thoughts, and actions for that actor in the accepted turn.",
                "Every non-player Mortal relevant actor must have a persistent NPC/faction/quest/inventory surface, or be moved to Actors outside scope when it is only background context.",
                "When a direct-speaking or directly addressed actor is an NPC, game_state/npcs/npc_core.json must keep or restore a full canonical NPC object for that actor.",
                "If the NPC is only offscreen continuity, route color, background pressure, or unchanged existing state, do not emit UpdateNPCs/NPCsInScene/other structured NPC updates for that actor this turn.",
                "Actors outside scope is for named actors not structurally changed this turn; it is not enough when UpdateNPCs changes that actor."
            },
            SafeCorrectionRules = new List<string>
            {
                "Add the actor to Relevant actors and a full reasoning block only when the accepted player action truly changes, addresses, observes, or depends on that actor this turn.",
                "Materialize or restore a missing direct NPC as a full canonical NPC object instead of deleting the actor from the scene to silence validation.",
                "Remove the structured NPC update when the actor is merely offscreen, unchanged, or mentioned only as context; preserve the information in narrative, quest log, location summary, or Actors outside scope instead.",
                "Keep canonical NPC names identical across Relevant actors, reasoning headings, and game_state/npcs/npc_core.json.",
                "Preserve unrelated NPC state while repairing only the out-of-scope update or its missing reasoning coverage."
            },
            Steps = new List<string>
            {
                "Open output/debug_logs.json and game_state/npcs/npc_core.json before repairing structured_npc_update_out_of_scope.",
                "For each named actor, decide whether the accepted turn really changed or directly used that NPC.",
                "For mortal_relevant_actor_missing_persistence, either restore/materialize the missing persistent NPC/faction/quest/inventory surface, or move the actor to Actors outside scope if the actor was only background context.",
                "If the actor is a direct-speaking or directly addressed NPC, keep a full persistent NPC object in game_state/npcs/npc_core.json and preserve existing canonical fields from the pre-turn state when available.",
                "If yes, add the exact canonical name to Relevant actors and add a matching reasoning block with current location, situation, thoughts, and actions.",
                "If no, remove that actor's structured NPC update from this turn and keep any useful clue in non-NPC structured surfaces such as quest log or current_location summary.",
                "After repairs are complete, call Complete-BoeValidationRepair as the last action, or create validation_repair_ready.json with exact metadata from the current validation_repair_request.json."
            },
            DoNotDo = new List<string>
            {
                "Do not silence the error by deleting the NPC permanently when only the current turn update is out of scope.",
                "Do not add an actor to Relevant actors without a matching reasoning block.",
                "Do not keep a structured NPC update for an offscreen unchanged actor just because the actor exists in npc_core.json.",
                "Do not read implementation code such as BookOfEternityClient/**/*.cs to infer scope rules; use this packet, the validation request, and GM docs."
            }
        };
    }

    private static ValidationRepairHarnessPacket BuildMortalNpcInventoryUpdateRepairPacket(
        IReadOnlyList<ValidationIssue> mortalNpcInventoryUpdateErrors)
    {
        var actorNames = CollectRepairActorNames(mortalNpcInventoryUpdateErrors)
            .OrderBy(actor => actor, StringComparer.OrdinalIgnoreCase)
            .ToList();
        var targetFiles = BuildMortalNpcTargetFiles(mortalNpcInventoryUpdateErrors, includeNpcCoreWhenMissing: true);

        return new ValidationRepairHarnessPacket
        {
            Kind = "mortal_npc_inventory_update_repair",
            Priority = "high",
            Title = "Mortal NPC existing-inventory update repair",
            TargetFiles = targetFiles,
            TemplateRefs = new List<string> { "Templates/MORTAL_NPC_UPDATE_TEMPLATE.md" },
            CanonicalActorNames = actorNames,
            ExpectedShape = new List<string>
            {
                "A genuinely new NPC with NPCId = JSON null and a non-empty initialId may carry its initial inventory inside the full UpdateNPCs/NPCsInScene object.",
                "An ordinary existing NPC must remove the whole ordinary-existing full-object resend from UpdateNPCs. Every legitimate supported change uses its dedicated delta/command surface.",
                "A true legacy promotion must retain schema-required inventory and restore the exact semantically unchanged validated pre-turn inventory snapshot carried by validationIssues[].expected; real mutations still use dedicated inventory commands."
            },
            SafeCorrectionRules = new List<string>
            {
                "Patch only the listed NPC entries and inventory command surfaces.",
                "For an ordinary existing NPC, remove the whole ordinary-existing full-object resend; never retain a schema-invalid UpdateNPCs object with inventory omitted.",
                "Express every legitimate skill, inventory, relationship, journal, activity, equipment/resource, or other supported change through its dedicated delta/command surface.",
                "If a required delta surface does not exist, use the main-GM rollback/repair path instead of constructing a partial full object.",
                "For a true legacy promotion, copy the exact inventory JSON from validationIssues[].expected back into the full promotion object without semantic changes.",
                "Keep initial inventory only for a genuinely new NPC that was already new in validated continuity authority; never change an existing identity into NPCId = JSON null plus initialId to evade this error."
            },
            Steps = new List<string>
            {
                "Open Templates/MORTAL_NPC_UPDATE_TEMPLATE.md before repairing NPC inventory update validation errors.",
                "For each named actor, classify the object as a genuinely new NPC, an ordinary existing NPC, or a true legacy promotion using validated identity/materialization continuity and the issue metadata.",
                "For an ordinary existing NPC, remove the whole ordinary-existing full-object resend from UpdateNPCs. Re-author every legitimate supported change through NPCInventoryAdds, NPCInventoryUpdates, NPCInventoryRemovals, skill, relationship, journal, activity, equipment/resource, or another documented dedicated command surface.",
                "If any required ordinary-existing change has no dedicated delta/command surface, stop and use the main-GM rollback/repair path; do not retain a partial full object.",
                "For a true legacy promotion, keep inventory present and restore the exact semantically unchanged validated pre-turn inventory snapshot from validationIssues[].expected.",
                "After repairs are complete, call Complete-BoeValidationRepair as the last action, or create validation_repair_ready.json with exact metadata from the current validation_repair_request.json."
            },
            DoNotDo = new List<string>
            {
                "Do not delete a meaningful NPC to silence an inventory resend error.",
                "Do not keep an ordinary existing full UpdateNPCs object after removing only its inventory property; that partial object is schema-invalid.",
                "Do not remove the schema-required inventory from a full true legacy promotion.",
                "Do not mutate or reconstruct the validated pre-turn promotion snapshot; copy validationIssues[].expected exactly.",
                "Do not reclassify an existing NPC as genuinely new by replacing its permanent NPCId with initialId.",
                "Do not read implementation code such as BookOfEternityClient/**/*.cs to infer NPC inventory rules."
            }
        };
    }

    private static ValidationRepairHarnessPacket BuildMortalNpcFullObjectRepairPacket(
        IReadOnlyList<ValidationIssue> mortalNpcFullObjectErrors)
    {
        var actorNames = CollectRepairActorNames(mortalNpcFullObjectErrors)
            .OrderBy(actor => actor, StringComparer.OrdinalIgnoreCase)
            .ToList();
        var targetFiles = BuildMortalNpcTargetFiles(mortalNpcFullObjectErrors, includeNpcCoreWhenMissing: true);
        var missingFields = CollectRepairMissingFields(mortalNpcFullObjectErrors);
        var hasMalformedSkillArrays = mortalNpcFullObjectErrors.Any(issue => IsMortalNpcSkillObjectRepairPath(issue.FilePath));
        var expectedShape = new List<string>
        {
            "Every meaningful Mortal World NPC update must materialize a full NPC object in game_state/npcs/npc_core.json.",
            "Required profile/social fields include display identity, rarity, worldview, personalityArchetype, culturalStance, race, class, appearanceDescription, history, progressionType, relationshipLevel, attitude, relationshipLock, goals, inventory, and personalityTraits.",
            "Collections such as relationshipLock, goals, inventory, personalityTraits, customProperties, journalEntries, and related arrays stay JSON arrays/objects even when they contain one item."
        };
        var steps = new List<string>
        {
            "Open Templates/MORTAL_NPC_UPDATE_TEMPLATE.md before editing game_state/npcs/npc_core.json.",
            "Find each NPC named by the validation errors and expand it to the canonical full NPC object shape instead of leaving a partial row.",
            "Ensure relationshipLock is an object/array in the expected canonical shape, goals is a collection of concrete goals, and personalityTraits is a collection of concrete traits.",
            "Ensure attitude is synchronized with relationshipLevel and culturalStance uses Conformist, Pragmatist, or Dissident."
        };

        if (missingFields.Count > 0)
        {
            expectedShape.Add($"Validator reported these exact missing fields: {string.Join(", ", missingFields)}.");
            steps.Add($"Patch the exact missing fields first: {string.Join(", ", missingFields)}.");
        }

        if (missingFields.Contains("inventory", StringComparer.OrdinalIgnoreCase))
            steps.Add("For a newly created NPC without carried items, add inventory: [] rather than omitting the inventory field.");

        if (hasMalformedSkillArrays)
        {
            expectedShape.Add("NPC activeSkills/passiveSkills arrays must contain full skill objects, not string names. Put names in skillName/displayName fields inside each object.");
            steps.Add("Replace activeSkills/passiveSkills string names with full skill objects: active skills need skillName, skillDescription, rarity, actionCost, and combatEffect; passive skills need skillName, skillDescription, rarity, type/group, bonuses, and structuredBonuses/playerStatBonus where applicable.");
        }

        steps.Add("After repairs are complete, call Complete-BoeValidationRepair as the last action, or create validation_repair_ready.json with exact metadata from the current validation_repair_request.json.");

        return new ValidationRepairHarnessPacket
        {
            Kind = "mortal_npc_full_object_repair",
            Priority = "high",
            Title = "Mortal NPC full object shape repair",
            TargetFiles = targetFiles,
            TemplateRefs = new List<string> { "Templates/MORTAL_NPC_UPDATE_TEMPLATE.md" },
            CanonicalActorNames = actorNames,
            MissingFields = missingFields.Count == 0 ? null : missingFields,
            ExpectedShape = expectedShape,
            SafeCorrectionRules = new List<string>
            {
                "Complete the existing NPC object instead of replacing the file with a minimal skeleton.",
                "For background-only names that should not persist, remove them from structured actor/NPC updates instead of creating a partial NPC.",
                "Keep all user-visible NPC prose meaningful; required fields should not be filled with placeholders like unknown/TBD unless the story explicitly supports uncertainty."
            },
            Steps = steps,
            DoNotDo = new List<string>
            {
                "Do not delete meaningful NPCs or story hooks to avoid filling required fields.",
                "Do not use raw nulls for required strings or collapse single-item arrays into scalars.",
                "Do not read implementation code such as BookOfEternityClient/**/*.cs to infer NPC object rules."
            }
        };
    }

    private static ValidationRepairHarnessPacket BuildActorMaterializationRepairPacket(
        IReadOnlyList<ValidationIssue> materializationErrors)
    {
        var actorIdentities = CollectRepairActorNames(materializationErrors)
            .OrderBy(actor => actor, StringComparer.Ordinal)
            .ToList();
        var targetFiles = materializationErrors
            .SelectMany(ResolveActorMaterializationRepairTargetFiles)
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .OrderBy(path => path, StringComparer.Ordinal)
            .ToList();
        var missingTargets = materializationErrors
            .Select(DescribeMissingActorMaterializationTarget)
            .Where(target => !string.IsNullOrWhiteSpace(target))
            .Select(target => target!)
            .Distinct(StringComparer.Ordinal)
            .OrderBy(target => target, StringComparer.Ordinal)
            .ToList();
        var hasMortalTargets = targetFiles.Contains(
            "game_state/npcs/npc_core.json",
            StringComparer.OrdinalIgnoreCase);
        var hasAfterlifeTargets = targetFiles.Contains(
            AfterlifeEntityProfileState.StatePath,
            StringComparer.OrdinalIgnoreCase);
        var templateRefs = new List<string>();
        if (hasMortalTargets)
            templateRefs.Add("Templates/MORTAL_NPC_UPDATE_TEMPLATE.md");
        if (hasAfterlifeTargets)
        {
            templateRefs.Add("OtherGuides/Afterlife_Contract_Matrix.md");
            templateRefs.Add("Examples/E_CLI_Afterlife_Turns.txt");
        }

        return new ValidationRepairHarnessPacket
        {
            Kind = "actor_materialization_repair",
            Priority = "critical",
            Title = "Bounded actor materialization repair",
            TargetFiles = targetFiles,
            TemplateRefs = templateRefs,
            CanonicalActorNames = actorIdentities,
            MissingFields = missingTargets.Count == 0 ? null : missingTargets,
            ExactFieldCorrections = materializationErrors
                .Select(BuildExactFieldCorrection)
                .OrderBy(correction => correction.Path, StringComparer.Ordinal)
                .ThenBy(correction => correction.Code, StringComparer.Ordinal)
                .ToList(),
            ExpectedShape = new List<string>
            {
                "Each listed actor uses one exact canonical actorType:actorId identity; display names, prose, and setting genre are never identity authority.",
                "A new or promoted significant actor has one complete actor-bound materialization v1 envelope with exact capabilities and every required section disposition.",
                "A section with canonical content uses state=populated; state=empty_by_design requires a non-empty in-world reason and keeps every governed canonical empty array, object, or null field physically present.",
                "A new afterlife actor explicitly carries appearanceDescription, profileSummary, personalityProfile.archetype, motivation, personalityProfile.worldview, realm, locationId, goals with a non-empty plan, and exact actor-owned memory.",
                "A new Mortal UpdateNPCs actor carries exactly one non-empty location authority: a known currentLocationId or a valid same-turn initialLocationId, never neither or both.",
                "An existing materialized afterlife profile is never resent through the full afterlifeEntityProfileUpdates carrier; later changes use the exact dedicated delta, while a legacy first-envelope migration preserves every historical field.",
                "Existing valid actor fields, valid sections, and accepted historical materialization envelopes remain unchanged unless an exact correction explicitly targets them."
            },
            SafeCorrectionRules = new List<string>
            {
                "Repair only the actors and materialization sections listed in canonicalActorNames, missingFields, and exactFieldCorrections.",
                "Preserve every valid actor field and valid materialization section that is not named by an exact validation error.",
                "For afterlife_actor_materialization_profile_missing, add exactly one common profile for the listed exact actorType:actorId in game_state/meta/afterlife_entity_profiles.json.",
                "For profile ambiguity, keep one canonical exact type-and-ID profile and preserve its valid sections; do not merge records by displayName.",
                "For actor-owned memory errors, initialize memory from facts of the current accepted turn in the exact actor profile or documented type-specific journal.",
                "For an empty-surface error, restore only the named governed field in its exact canonical empty array, object, or null shape; omission is not empty_by_design.",
                "For afterlife presentation, personality, realm/location, or goals/plan errors, author only the exact missing structured field from current canonical evidence.",
                "For a new Mortal location error, select exactly one known currentLocationId or valid same-turn initialLocationId and preserve every unrelated NPC field."
            },
            Steps = new List<string>
            {
                "Open game_state/control/validation_repair_request.json and only the packet-listed targetFiles/templates first.",
                "Locate each actor by the exact actorType:actorId value in canonicalActorNames; do not resolve an actor by displayName or narrative description.",
                "Apply missingFields and exactFieldCorrections one by one, changing only the named envelope/profile/section target.",
                "Remove any historical afterlife full-carrier resend and express its intended change through the documented exact dedicated delta surface.",
                "Recheck that all unrelated actor data, already valid materialization sections, and historical envelopes are byte-for-byte or semantically preserved as required by the listed error.",
                "After repairs are complete, call Complete-BoeValidationRepair as the last action, or create validation_repair_ready.json with exact metadata from the current validation_repair_request.json."
            },
            DoNotDo = new List<string>
            {
                "Do not read implementation code such as BookOfEternityClient/**/*.cs to infer Actor Materialization rules; use this packet, validation_repair_request.json, templates, docs, examples, and canonical state.",
                "Do not rewrite the whole actor, entire profile collection, or unrelated materialization sections to fix one listed target.",
                "Do not delete an actor, profile, item, memory record, or valid section merely to silence a materialization error.",
                "Do not infer identity, capabilities, skills, inventory, equipment slots, trade, or section content from actor names, archetype prose, item types, genre keywords, or narrative descriptions.",
                "Do not ask the client to invent GM-authored actor content or fabricate missing narrative facts; the GM must author only the exact bounded repair from current canonical evidence.",
                "Do not create a new turn or write ready/turn_complete.json during validation repair."
            }
        };
    }

    private static IEnumerable<string> ResolveActorMaterializationRepairTargetFiles(ValidationIssue issue)
    {
        var authorityPath = GmWorkerTaskPacketBuilder.ResolveActorMaterializationAuthorityPath(issue);
        if (authorityPath != null)
        {
            yield return authorityPath;
            yield break;
        }

        var actor = NormalizeRepairActorName(issue.Actor);
        if (!string.IsNullOrWhiteSpace(actor) &&
            actor.StartsWith("mortal_npc:", StringComparison.Ordinal))
        {
            yield return "game_state/npcs/npc_core.json";
            yield break;
        }

        if ((issue.Code ?? string.Empty).StartsWith("afterlife_actor_materialization_", StringComparison.OrdinalIgnoreCase) ||
            (!string.IsNullOrWhiteSpace(actor) && actor.Contains(':', StringComparison.Ordinal)))
        {
            yield return AfterlifeEntityProfileState.StatePath;
            yield break;
        }

        var normalizedPath = NormalizeRepairTargetPath(issue.FilePath);
        if (normalizedPath.StartsWith("game_state/npcs/npc_core.json", StringComparison.OrdinalIgnoreCase))
        {
            yield return "game_state/npcs/npc_core.json";
            yield break;
        }

        if (normalizedPath.StartsWith(AfterlifeEntityProfileState.StatePath, StringComparison.OrdinalIgnoreCase))
            yield return AfterlifeEntityProfileState.StatePath;
    }

    private static string? DescribeMissingActorMaterializationTarget(ValidationIssue issue)
    {
        var actor = NormalizeRepairActorName(issue.Actor);
        if (string.IsNullOrWhiteSpace(actor))
            actor = "exact actor from errors[]";

        return (issue.Code ?? string.Empty).ToLowerInvariant() switch
        {
            "actor_materialization_missing" => $"{actor} / materialization",
            "actor_materialization_section_missing" => $"{actor} / {issue.Section ?? "required section"}",
            "actor_materialization_section_empty_surface_invalid" => $"{actor} / {issue.Section ?? "required section"} canonical empty surface",
            "actor_materialization_afterlife_missing_appearance" => $"{actor} / appearanceDescription",
            "actor_materialization_afterlife_missing_profile_summary" => $"{actor} / profileSummary",
            "actor_materialization_afterlife_missing_personality" => $"{actor} / personalityProfile.archetype",
            "actor_materialization_afterlife_missing_motivation" => $"{actor} / motivation",
            "actor_materialization_afterlife_missing_worldview" => $"{actor} / personalityProfile.worldview",
            "actor_materialization_afterlife_missing_realm" => $"{actor} / realm",
            "actor_materialization_afterlife_missing_location" => $"{actor} / locationId",
            "actor_materialization_afterlife_missing_goals_plan" => $"{actor} / goals plan",
            "afterlife_actor_materialization_profile_missing" => $"{actor} / common profile",
            "afterlife_actor_materialization_memory_missing" => $"{actor} / actor-owned memory",
            "npc_new_update_location_authority_not_exactly_one" => $"{actor} / exactly one currentLocationId or initialLocationId",
            "npc_new_update_current_location_unknown" => $"{actor} / known currentLocationId",
            _ => null
        };
    }

    private static List<string> CollectRepairMissingFields(IReadOnlyList<ValidationIssue> issues)
    {
        var fields = new SortedSet<string>(StringComparer.OrdinalIgnoreCase);
        foreach (var issue in issues)
        {
            foreach (var part in (issue.Actual ?? string.Empty).Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries))
            {
                var field = part;
                const string MissingPrefix = "missing ";
                if (field.StartsWith(MissingPrefix, StringComparison.OrdinalIgnoreCase))
                    field = field[MissingPrefix.Length..].Trim();
                if (!string.IsNullOrWhiteSpace(field))
                    fields.Add(field);
            }
        }

        return fields.ToList();
    }

    private static ValidationRepairHarnessPacket BuildMortalNpcRelationshipEnumRepairPacket(
        IReadOnlyList<ValidationIssue> mortalNpcRelationshipEnumErrors)
    {
        var actorNames = CollectRepairActorNames(mortalNpcRelationshipEnumErrors)
            .OrderBy(actor => actor, StringComparer.OrdinalIgnoreCase)
            .ToList();
        var targetFiles = BuildMortalNpcTargetFiles(mortalNpcRelationshipEnumErrors, includeNpcCoreWhenMissing: true);

        return new ValidationRepairHarnessPacket
        {
            Kind = "mortal_npc_relationship_enum_repair",
            Priority = "high",
            Title = "Mortal NPC relationship tier and cultural stance repair",
            TargetFiles = targetFiles,
            TemplateRefs = new List<string> { "Templates/MORTAL_NPC_UPDATE_TEMPLATE.md" },
            CanonicalActorNames = actorNames,
            ExpectedShape = new List<string>
            {
                "NPC attitude is a canonical Russian relationship tier derived from relationshipLevel.",
                "Allowed attitude tiers: Непримиримый Враг, Противник, Неприязнь, Нейтралитет, Доверие и Расположение, Глубокая Связь, Легендарная Преданность.",
                "NPC culturalStance is one of the canonical enum values: Conformist, Pragmatist, Dissident."
            },
            SafeCorrectionRules = new List<string>
            {
                "Change attitude and/or relationshipLevel together so the tier and score agree.",
                "Use culturalStance enum values exactly as written; put localized prose in separate display/description fields, not in culturalStance.",
                "Preserve the NPC's existing story relationship meaning; do not reset relationshipLevel to zero unless neutral is the intended repair."
            },
            Steps = new List<string>
            {
                "Open Templates/MORTAL_NPC_UPDATE_TEMPLATE.md and patch the listed NPC entries in game_state/npcs/npc_core.json.",
                "Map relationshipLevel to one canonical attitude tier: Непримиримый Враг / Противник / Неприязнь / Нейтралитет / Доверие и Расположение / Глубокая Связь / Легендарная Преданность.",
                "Set culturalStance only to Conformist, Pragmatist, or Dissident.",
                "If the relationship meaning in prose conflicts with the numeric level, adjust the smallest necessary field pair and keep a coherent repair note in debug output.",
                "After repairs are complete, call Complete-BoeValidationRepair as the last action, or create validation_repair_ready.json with exact metadata from the current validation_repair_request.json."
            },
            DoNotDo = new List<string>
            {
                "Do not write English attitude labels such as Friendly/Hostile into NPC attitude.",
                "Do not localize culturalStance into Russian inside the enum field.",
                "Do not read implementation code such as BookOfEternityClient/**/*.cs to infer relationship enum rules."
            }
        };
    }

    private static ValidationRepairHarnessPacket BuildMortalNpcReferenceRepairPacket(
        IReadOnlyList<ValidationIssue> mortalNpcReferenceErrors)
    {
        var actorNames = CollectRepairActorNames(mortalNpcReferenceErrors)
            .OrderBy(actor => actor, StringComparer.OrdinalIgnoreCase)
            .ToList();
        var targetFiles = BuildMortalNpcTargetFiles(mortalNpcReferenceErrors, includeNpcCoreWhenMissing: true);

        return new ValidationRepairHarnessPacket
        {
            Kind = "mortal_npc_reference_repair",
            Priority = "high",
            Title = "Mortal NPC reference repair",
            TargetFiles = targetFiles,
            TemplateRefs = new List<string> { "Templates/MORTAL_NPC_UPDATE_TEMPLATE.md" },
            CanonicalActorNames = actorNames,
            ExpectedShape = new List<string>
            {
                "NPCJournals and other NPC sidecar updates must reference an existing NPCId or NPCName from canonical game_state/npcs/npc_core.json.",
                "If the actor became meaningful this turn, materialize a full NPC object first; if the actor was only background color, remove the orphan sidecar/journal update."
            },
            SafeCorrectionRules = new List<string>
            {
                "Do not create orphan NPCJournals entries for names that are absent from npc_core.json.",
                "Preserve journal prose only after it is attached to an existing or newly materialized full NPC.",
                "Prefer correcting the reference to an existing NPC over inventing a new NPC when the name clearly points to a current character."
            },
            Steps = new List<string>
            {
                "Open Templates/MORTAL_NPC_UPDATE_TEMPLATE.md before repairing NPCJournals or other NPC references.",
                "Compare the referenced NPCId/NPCName with game_state/npcs/npc_core.json.",
                "If the NPC exists, correct the journal/reference to the exact existing NPCId or NPCName.",
                "If the NPC is genuinely introduced by this turn, add/complete the full NPC object in game_state/npcs/npc_core.json before keeping NPCJournals.",
                "If the reference was only background-only color, remove the orphan NPCJournals/reference update instead of creating a partial NPC.",
                "After repairs are complete, call Complete-BoeValidationRepair as the last action, or create validation_repair_ready.json with exact metadata from the current validation_repair_request.json."
            },
            DoNotDo = new List<string>
            {
                "Do not leave NPCJournals pointing at unknown names or ids.",
                "Do not create a partial NPC object just to satisfy the reference.",
                "Do not read implementation code such as BookOfEternityClient/**/*.cs to infer NPC reference rules."
            }
        };
    }

    private static ValidationRepairHarnessPacket BuildMortalCombatStateRepairPacket(
        IReadOnlyList<ValidationIssue> mortalCombatStateErrors)
    {
        var targetFiles = mortalCombatStateErrors
            .Select(issue => NormalizeRepairTargetPath(issue.FilePath))
            .Where(path => !string.IsNullOrWhiteSpace(path))
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .OrderBy(path => path, StringComparer.OrdinalIgnoreCase)
            .ToList();

        foreach (var path in new[]
                 {
                     "game_state/combat/enemies.json",
                     "game_state/combat/allies.json",
                     "game_state/combat/combat_log.json"
                 })
        {
            if (!targetFiles.Contains(path, StringComparer.OrdinalIgnoreCase))
                targetFiles.Add(path);
        }

        return new ValidationRepairHarnessPacket
        {
            Kind = "mortal_combat_state_repair",
            Priority = "high",
            Title = "Mortal combat state materialization repair",
            TargetFiles = targetFiles,
            TemplateRefs = new List<string> { "Templates/MORTAL_COMBAT_STATE_TEMPLATE.md" },
            ExpectedShape = new List<string>
            {
                "A Mortal World turn that explicitly resolves open combat and updates XP, active skill mastery, or combat resources must leave a player-inspectable combat surface.",
                "At minimum, write game_state/combat/combat_log.json with a recent combat summary that /бой can show.",
                "If enemies or allies remain tactically relevant after the turn, also write game_state/combat/enemies.json and game_state/combat/allies.json.",
                "If the fight fully ended in the same turn, enemies may be absent or marked defeated, but combat_log.json must still explain what happened."
            },
            SafeCorrectionRules = new List<string>
            {
                "Repair the already accepted combat scene; do not invent a different enemy, reroll dice, or rewrite the player action.",
                "Preserve the XP, skill mastery, and resource changes already written unless validation explicitly rejects them.",
                "Use player-facing Russian summaries in combat_log.json, while keeping canonical JSON keys and enum values valid.",
                "If there is no ongoing combat after the exchange, make that explicit in the combat log instead of leaving /бой empty."
            },
            Steps = new List<string>
            {
                "Open Templates/MORTAL_COMBAT_STATE_TEMPLATE.md and validation_repair_request.json first.",
                "Write or patch game_state/combat/combat_log.json so it summarizes the same combat exchange described in output/narrative_response.json.",
                "If the enemy still exists or its defeated state matters, write game_state/combat/enemies.json with the opponent, health/poise state, actions, and defeated/retreated status.",
                "If named allies participated tactically, write game_state/combat/allies.json with their roles and current state.",
                "Include every touched combat file in the repair completion evidence.",
                "After repairs are complete, call Complete-BoeValidationRepair as the last action, or create validation_repair_ready.json with exact metadata from the current validation_repair_request.json."
            },
            DoNotDo = new List<string>
            {
                "Do not delete XP, skill mastery, or player_status changes just to silence this repair.",
                "Do not leave /бой empty after a player-facing open combat scene.",
                "Do not read implementation code such as BookOfEternityClient/**/*.cs to infer combat shape; use this packet, the compact template, and session files."
            }
        };
    }

    private static ValidationRepairHarnessPacket BuildMortalSkillProgressionShapeRepairPacket(
        IReadOnlyList<ValidationIssue> skillShapeErrors)
    {
        var targetFiles = skillShapeErrors
            .Select(issue => NormalizeMortalSkillProgressionRepairTargetPath(issue.FilePath))
            .Where(path => !string.IsNullOrWhiteSpace(path))
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .OrderBy(path => path, StringComparer.OrdinalIgnoreCase)
            .ToList();

        foreach (var path in new[]
                 {
                     "game_state/control/pending_training_showcase_requests.json",
                     "game_state/player/skills_active.json",
                     "game_state/player/skills_passive.json",
                     "game_state/player/skill_mastery.json"
                 })
        {
            if (!targetFiles.Contains(path, StringComparer.OrdinalIgnoreCase))
                targetFiles.Add(path);
        }

        var actorNames = CollectRepairActorNames(skillShapeErrors)
            .OrderBy(actor => actor, StringComparer.OrdinalIgnoreCase)
            .ToList();
        var exactFieldCorrections = skillShapeErrors
            .Select(BuildExactFieldCorrection)
            .Where(correction => !string.IsNullOrWhiteSpace(correction.Path))
            .DistinctBy(correction => correction.Path, StringComparer.OrdinalIgnoreCase)
            .OrderBy(correction => correction.Path, StringComparer.OrdinalIgnoreCase)
            .ToList();

        return new ValidationRepairHarnessPacket
        {
            Kind = "mortal_skill_progression_shape_repair",
            Priority = "high",
            Title = "Mortal skill progression array-shape repair",
            TargetFiles = targetFiles,
            TemplateRefs = new List<string>
            {
                "Templates/MORTAL_SKILL_PROGRESSION_TEMPLATE.md",
                "Examples/E_CLI_Training_Showcases.txt"
            },
            CanonicalActorNames = actorNames,
            ExactFieldCorrections = exactFieldCorrections,
            ExpectedShape = new List<string>
            {
                "Mortal skill progression fields are always JSON arrays, even when the turn changes exactly one skill.",
                "game_state/player/skills_active.json.activeSkillChanges, removeActiveSkills, and related active skill change fields must be arrays, not a single object.",
                "game_state/player/skills_passive.json.passiveSkillChanges, removePassiveSkills, and related passive skill change fields must be arrays, not a single object.",
                "game_state/player/skill_mastery.json.skillMasteryChanges must be an array, not a single mastery object.",
                "If game_state/control/pending_training_showcase_requests.json contains requestKind = mortal_training_skill_evolution, use that pending paid request as authority for targetKind, targetName, currentLevel, nextLevel, and purchaseReceipt.",
                "For an active skill unlock from paid training, write activeSkillChanges as an array with the complete active skill object and skillMasteryChanges as an array with the matching mastery entry.",
                "For a passive skill unlock or passive mastery training, write passiveSkillChanges as an array and do not invent an active skill solely to satisfy skillMasteryChanges."
            },
            SafeCorrectionRules = new List<string>
            {
                "Do not charge money, experience, ink feathers, or any other currency again; a paid training showcase purchase already created the pending request and receipt.",
                "If a field contains a valid single object where an array is expected, wrap that single object in an array instead of rewriting unrelated skill data.",
                "Patch only the listed player skill files and the directly related pending training request evidence; do not rewrite unrelated NPCs, inventory, combat, map, or world state.",
                "Preserve localized names, descriptions, structuredBonuses, playerStatBonus, mastery progress, sourceCap, and teacher identity while correcting array shape.",
                "If details.targetKind in the pending request conflicts with the malformed file, follow details.targetKind and remove the malformed change from the wrong skill surface."
            },
            Steps = new List<string>
            {
                "Open game_state/control/validation_repair_request.json first and inspect harnessRepairPackets[].exactFieldCorrections for the malformed paths.",
                "Open game_state/control/pending_training_showcase_requests.json and check whether a requestKind = mortal_training_skill_evolution is still pending.",
                "Open the listed player skill files: game_state/player/skills_active.json, game_state/player/skills_passive.json, and game_state/player/skill_mastery.json.",
                "For each malformed activeSkillChanges, passiveSkillChanges, removeActiveSkills, removePassiveSkills, or skillMasteryChanges field, wrap a valid single object/value into an array or replace malformed scalar content with the correct empty array when validation expected an empty collection.",
                "When a paid training request is pending, make the repaired skill change match details.targetKind: active targets use activeSkillChanges plus skillMasteryChanges; passive targets use passiveSkillChanges and no invented active skill.",
                "Do not apply another purchase, refund, or new turn result; this repair only fixes the shape and placement of the already authored skill progression.",
                "After repairs are complete, call Complete-BoeValidationRepair as the last action, or create validation_repair_ready.json with exact metadata from the current validation_repair_request.json."
            },
            DoNotDo = new List<string>
            {
                "Do not leave activeSkillChanges, passiveSkillChanges, removeActiveSkills, removePassiveSkills, or skillMasteryChanges as a single object.",
                "Do not delete pending_training_showcase_requests.json to silence the error.",
                "Do not charge resources a second time or create a second training receipt.",
                "Do not read implementation code such as BookOfEternityClient/**/*.cs to infer skill progression shape; use this packet, MORTAL_SKILL_PROGRESSION_TEMPLATE.md, Examples/E_CLI_Training_Showcases.txt, and canonical session files."
            }
        };
    }

    private static ValidationRepairHarnessPacket BuildMortalTrainingSkillEvolutionRepairPacket(
        IReadOnlyList<ValidationIssue> skillEvolutionErrors)
    {
        var targetFiles = skillEvolutionErrors
            .Select(issue => NormalizeRepairTargetPath(issue.FilePath))
            .Where(path => !string.IsNullOrWhiteSpace(path))
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .OrderBy(path => path, StringComparer.OrdinalIgnoreCase)
            .ToList();

        foreach (var path in new[]
                 {
                     "game_state/control/pending_training_showcase_requests.json",
                     "game_state/player/skills_active.json",
                     "game_state/player/skills_passive.json",
                     "game_state/player/skill_mastery.json",
                     "game_state/npcs/npc_core.json"
                 })
        {
            if (!targetFiles.Contains(path, StringComparer.OrdinalIgnoreCase))
                targetFiles.Add(path);
        }

        var actorNames = CollectRepairActorNames(skillEvolutionErrors)
            .OrderBy(actor => actor, StringComparer.OrdinalIgnoreCase)
            .ToList();
        var exactFieldCorrections = skillEvolutionErrors
            .Select(BuildExactFieldCorrection)
            .Where(correction => !string.IsNullOrWhiteSpace(correction.Path))
            .DistinctBy(correction => correction.Path, StringComparer.OrdinalIgnoreCase)
            .OrderBy(correction => correction.Path, StringComparer.OrdinalIgnoreCase)
            .ToList();

        return new ValidationRepairHarnessPacket
        {
            Kind = "mortal_training_skill_evolution_repair",
            Priority = "high",
            Title = "Mortal training skill evolution target-kind repair",
            TargetFiles = targetFiles,
            TemplateRefs = new List<string> { "Examples/E_CLI_Training_Showcases.txt" },
            CanonicalActorNames = actorNames,
            ExactFieldCorrections = exactFieldCorrections,
            ExpectedShape = new List<string>
            {
                "A paid mortal training request in game_state/control/pending_training_showcase_requests.json is authoritative for the trained target.",
                "Open the pending request with requestKind = mortal_training_skill_evolution and follow details.targetKind exactly.",
                "If details.targetKind is passive_skill_mastery or passive_skill_unlock, patch game_state/player/skills_passive.json through passiveSkillChanges, not skillMasteryChanges.",
                "If details.targetKind is active_skill_mastery or active_skill_unlock, patch game_state/player/skills_active.json and only reference skills that already exist in canonical active skills state unless the request is an unlock.",
                "When updating passive skills, preserve structuredBonuses, playerStatBonus, source tags, and player-facing Russian descriptions unless the pending request explicitly changes them."
            },
            SafeCorrectionRules = new List<string>
            {
                "Do not charge money, experience, ink feathers, or any other currency again; the local showcase purchase already created the paid pending request and receipt.",
                "Resolve only the pending training evolution described by the current validation request; do not rewrite unrelated skills, NPCs, inventory, or world state.",
                "If validation reports skill_mastery_unknown_active_skill for a passive skill, remove the invalid skillMasteryChanges active entry and express the same evolution through passiveSkillChanges.",
                "Preserve structuredBonuses, playerStatBonus, source labels, and existing localized descriptions when patching passive skill data.",
                "Use the teacher's canonical skill profile in game_state/npcs/npc_core.json only to confirm caps and story wording; pending details.targetKind remains the source of truth for active vs passive."
            },
            Steps = new List<string>
            {
                "Open validation_repair_request.json and game_state/control/pending_training_showcase_requests.json first.",
                "Find the pending request with requestKind = mortal_training_skill_evolution and read details.targetKind, details.targetName, details.currentLevel, details.nextLevel, and purchaseReceipt.",
                "If details.targetKind says passive_skill_mastery or passive_skill_unlock, remove the invalid active skillMasteryChanges entry and patch game_state/player/skills_passive.json with passiveSkillChanges for that same target.",
                "If details.targetKind says active_skill_mastery or active_skill_unlock, keep the change on the active skill surface and ensure the active skill name/id exists or is created by the same unlock request.",
                "Preserve structuredBonuses, playerStatBonus, mastery progress, sourceCap, teacher identity, and player-facing Russian skill prose when patching the skill object.",
                "Include the touched skill file and pending request in repair completion evidence.",
                "After repairs are complete, call Complete-BoeValidationRepair as the last action, or create validation_repair_ready.json with exact metadata from the current validation_repair_request.json."
            },
            DoNotDo = new List<string>
            {
                "Do not invent an active skill just because skillMasteryChanges rejected a passive skill name.",
                "Do not delete pending_training_showcase_requests.json to silence the error.",
                "Do not apply a second purchase cost or refund the existing purchase unless validation explicitly asks for a receipt rollback.",
                "Do not read implementation code such as BookOfEternityClient/**/*.cs to infer training rules; use this packet, Examples/E_CLI_Training_Showcases.txt, the pending request, and canonical session files."
            }
        };
    }

    private static ValidationRepairHarnessPacket BuildTrainingShowcaseSnapshotHashRepairPacket(
        IReadOnlyList<ValidationIssue> trainingShowcaseSnapshotErrors)
    {
        var targetFiles = trainingShowcaseSnapshotErrors
            .Select(issue => NormalizeTrainingShowcaseRepairTargetPath(issue.FilePath))
            .Where(path => !string.IsNullOrWhiteSpace(path))
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .OrderBy(path => path, StringComparer.OrdinalIgnoreCase)
            .ToList();

        if (!targetFiles.Contains("game_state/control/pending_training_showcase_requests.json", StringComparer.OrdinalIgnoreCase))
            targetFiles.Add("game_state/control/pending_training_showcase_requests.json");

        var actorNames = CollectRepairActorNames(trainingShowcaseSnapshotErrors)
            .OrderBy(actor => actor, StringComparer.OrdinalIgnoreCase)
            .ToList();
        var exactFieldCorrections = trainingShowcaseSnapshotErrors
            .Select(BuildExactFieldCorrection)
            .Where(correction => !string.IsNullOrWhiteSpace(correction.Path))
            .DistinctBy(correction => correction.Path, StringComparer.OrdinalIgnoreCase)
            .OrderBy(correction => correction.Path, StringComparer.OrdinalIgnoreCase)
            .ToList();

        return new ValidationRepairHarnessPacket
        {
            Kind = "training_showcase_snapshot_hash_repair",
            Priority = "high",
            Title = "Training showcase source snapshot hash repair",
            TargetFiles = targetFiles,
            TemplateRefs = new List<string> { "Examples/E_CLI_Training_Showcases.txt" },
            CanonicalActorNames = actorNames,
            ExactFieldCorrections = exactFieldCorrections,
            ExpectedShape = new List<string>
            {
                "trainingShowcase/mentorTrainingShowcase must describe the current teacher or mentor profile and carry sourceActorSnapshotHash that matches that same current source actor.",
                "If the repair only refreshes the showcase, preserve the source actor's profile and set sourceActorSnapshotHash exactly from exactFieldCorrections[].",
                "If the repair also changes the teacher profile, finish those profile fields first, then use exactFieldCorrections[] from the current validation request as the authoritative hash for this repair attempt.",
                "The showcase must remain a data vitrines surface only: it lists offers, costs, source caps, and conditions; it does not spend player currency or grant skills directly."
            },
            SafeCorrectionRules = new List<string>
            {
                "Open game_state/control/pending_training_showcase_requests.json first and preserve the requested requestId/requestKind/sourceActorId/sourceActorName/realm.",
                "Apply exactFieldCorrections[] path -> expected replacements before trying to infer hashes from prose.",
                "Patch only the listed teacher/mentor showcase and directly related source actor fields required by validation; preserve unrelated NPCs, guardians, memories, relationships, and location state.",
                "Do not update an unrelated NPC teacher just because the file contains multiple actors."
            },
            Steps = new List<string>
            {
                "Open game_state/control/validation_repair_request.json and game_state/control/pending_training_showcase_requests.json.",
                "Open the target source actor file named in targetFiles, usually game_state/npcs/npc_core.json for Mortal teachers or game_state/meta/afterlife_entity_profiles.json for afterlife mentors.",
                "Find the teacher/mentor showcase field named by exactFieldCorrections[].path.",
                "Set each exactFieldCorrections[].path to exactFieldCorrections[].expected.",
                "Recheck that every offer still obeys sourceCap <= teacher skill/art level and that costs are present; do not apply the purchase locally.",
                "After repairs are complete, call Complete-BoeValidationRepair as the last action, or create validation_repair_ready.json with exact metadata from the current validation_repair_request.json."
            },
            DoNotDo = new List<string>
            {
                "Do not guess sourceActorSnapshotHash manually.",
                "Do not delete the training showcase just to silence stale-hash validation while a pending training showcase request exists.",
                "Do not spend player money, experience, ink feathers, or change skill mastery during showcase repair.",
                "Do not read implementation code such as BookOfEternityClient/**/*.cs to infer training rules; use this packet, Examples/E_CLI_Training_Showcases.txt, the validation request, and pending_training_showcase_requests.json."
            }
        };
    }

    private static ValidationRepairHarnessPacket BuildAfterlifeSpiritualConflictActionCostRepairPacket(
        IReadOnlyList<ValidationIssue> actionCostErrors)
    {
        var targetFiles = actionCostErrors
            .Select(issue => NormalizeRepairTargetPath(issue.FilePath))
            .Where(path => !string.IsNullOrWhiteSpace(path))
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .OrderBy(path => path, StringComparer.OrdinalIgnoreCase)
            .ToList();

        if (!targetFiles.Contains("game_state/meta/afterlife_spiritual_conflict_state.json", StringComparer.OrdinalIgnoreCase))
            targetFiles.Add("game_state/meta/afterlife_spiritual_conflict_state.json");

        var issueDetails = actionCostErrors
            .Select(DescribeAfterlifeActionCostRepairIssue)
            .Where(detail => !string.IsNullOrWhiteSpace(detail))
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .OrderBy(detail => detail, StringComparer.OrdinalIgnoreCase)
            .ToList();
        var issueSummary = issueDetails.Count == 0
            ? "see validation_repair_request.json.errors for exact paths and expected/actual values"
            : string.Join("; ", issueDetails);
        var exactFieldCorrections = actionCostErrors
            .Select(BuildExactFieldCorrection)
            .Where(correction => !string.IsNullOrWhiteSpace(correction.Path))
            .DistinctBy(correction => correction.Path, StringComparer.OrdinalIgnoreCase)
            .OrderBy(correction => correction.Path, StringComparer.OrdinalIgnoreCase)
            .ToList();

        return new ValidationRepairHarnessPacket
        {
            Kind = "afterlife_spiritual_conflict_action_cost_repair",
            Priority = "high",
            Title = "Afterlife spiritual conflict action-cost sequence repair",
            TargetFiles = targetFiles,
            TemplateRefs = new List<string>
            {
                "OtherGuides/Afterlife_Contract_Matrix.md",
                "Examples/E_CLI_Afterlife_Turns.txt"
            },
            ExactFieldCorrections = exactFieldCorrections,
            ExpectedShape = new List<string>
            {
                "For every current exchange, actionCostAudit.<side>.before must equal the previous current exchange's actionCostAudit.<side>.after; for the first current exchange, before must equal pre-turn activeConflict.actionEconomy.<side>.current.",
                "For paid actions, actionCostAudit.<side>.after must equal before - effectiveCost; for recovery, after must follow the documented recovery formula and stay within max.",
                "activeConflict.actionEconomy.<side>.current must equal the last current exchange actionCostAudit.<side>.after, or remain at the pre-turn value when the side has no current audit.",
                "Dice values, operationType/finalOperationType, incomingAction, maneuver outcome, specialArtAudit, and matchupAudit must remain authority-bound; repair arithmetic and audit fields without inventing a new exchange."
            },
            SafeCorrectionRules = new List<string>
            {
                "Use validation_repair_request.json.errors as the immediate repair checklist; its expected/actual values are authoritative for the listed fields.",
                "Recompute actionCostAudit sequentially across activeConflict.exchangeLog from the pre-turn actionEconomy baseline and the previous exchange result; do not copy a later current value backward.",
                "When fixing a before value, also recompute the same side's after and activeConflict.actionEconomy.<side>.current if they depend on that audit.",
                "If the request includes dice authorization errors, replace only the unauthorized dice/audit value with a pre-generated value from pending-turn authority; do not roll new dice manually.",
                "If the request includes maneuver strain errors, keep strain changes out of activeConflict unless the player action was the documented strain-conversion maneuver."
            },
            Steps = new List<string>
            {
                "Open game_state/control/validation_repair_request.json first and repair only the listed validation errors in place.",
                $"Patch these action-cost/audit fields exactly: {issueSummary}.",
                "Use exactFieldCorrections[] as the machine-readable checklist: set each listed path to expected, then recompute dependent actionEconomy current values.",
                "In game_state/meta/afterlife_spiritual_conflict_state.json, inspect activeConflict.exchangeLog in order and recompute actionCostAudit.player/actionCostAudit.opposition before/after values from the previous current exchange.",
                "For every listed exchangeLog[n], patch actionCostAudit.<side>.before to the expected value, then recompute that side's after using effectiveCost or the recovery rule; update activeConflict.actionEconomy.<side>.current to the final audited after value.",
                "Use pending_turn_snapshot and control authority only as read-only baselines for pre-turn action economy, dice, and authorized operations.",
                "After file repairs are complete, call Complete-BoeValidationRepair as the last action, or create game_state/control/validation_repair_ready.json with exact sessionId/requestId/turnNumber from the current validation_repair_request.json."
            },
            DoNotDo = new List<string>
            {
                "Do not create a new turn or write ready/turn_complete.json during validation repair.",
                "Do not edit game_state/control/pending_turn_snapshot or other authority snapshot files; use pending_turn_snapshot only as a read-only baseline.",
                "Do not change player prose, operation choices, dice rolls, special art ids, or exchange outcomes just to silence arithmetic validation.",
                "Do not read implementation code such as BookOfEternityClient/**/*.cs to infer repair rules; use this packet, validation_repair_request.json, afterlife docs/examples, and session control files."
            }
        };
    }

    private static ValidationRepairHarnessPacket BuildAfterlifeSpiritualConflictRewardRepairPacket(
        IReadOnlyList<ValidationIssue> rewardErrors)
    {
        var targetFiles = rewardErrors
            .Select(issue => NormalizeRepairTargetPath(issue.FilePath))
            .Where(path => !string.IsNullOrWhiteSpace(path))
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .OrderBy(path => path, StringComparer.OrdinalIgnoreCase)
            .ToList();

        if (!targetFiles.Contains("game_state/meta/afterlife_spiritual_conflict_state.json", StringComparer.OrdinalIgnoreCase))
            targetFiles.Add("game_state/meta/afterlife_spiritual_conflict_state.json");

        var issueDetails = rewardErrors
            .Select(DescribeAfterlifeRewardRepairIssue)
            .Where(detail => !string.IsNullOrWhiteSpace(detail))
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .OrderBy(detail => detail, StringComparer.OrdinalIgnoreCase)
            .ToList();
        var issueSummary = issueDetails.Count == 0
            ? "see validation_repair_request.json.errors for exact rewardAudit paths and expected/actual values"
            : string.Join("; ", issueDetails);

        return new ValidationRepairHarnessPacket
        {
            Kind = "afterlife_spiritual_conflict_reward_repair",
            Priority = "high",
            Title = "Afterlife spiritual conflict reward eligibility repair",
            TargetFiles = targetFiles,
            TemplateRefs = new List<string>
            {
                "OtherGuides/Afterlife_Contract_Matrix.md",
                "Examples/E_CLI_Afterlife_Turns.txt"
            },
            ExpectedShape = new List<string>
            {
                "Currency rewardAudit is allowed only for a resolved contested player victory with diceAudit.outcomeBand = player_success or decisive_player_success.",
                "Negotiated, withdrawn, failed, training-only, stalemate, teaching, or non-contested spiritual conflict outcomes must not grant currency rewards.",
                "If the conflict did not qualify for a reward, preserve the narrative/training outcome and remove rewardAudit/currency deltas instead of upgrading the outcome.",
                "If a reward is legitimately allowed, rewardAudit.realm, currency, conflictId, anti-farm uniqueness, and authority realm must match the resolved conflict proof."
            },
            SafeCorrectionRules = new List<string>
            {
                "Use validation_repair_request.json.errors as the immediate checklist; expected/actual values are authoritative for reward fields.",
                "For afterlife_conflict_reward_not_allowed, remove the currency reward path and any matching currency delta; keep non-currency learning, chronicle, relationship, or narrative consequences if they are otherwise valid.",
                "For reward realm/currency/conflictId errors on a valid victory, patch only rewardAudit identity/currency fields and preserve the resolved exchange outcome.",
                "Do not convert negotiated training into a victory just to keep feathers or light-spark rewards."
            },
            Steps = new List<string>
            {
                "Open game_state/control/validation_repair_request.json first and repair only the listed afterlife conflict reward errors in place.",
                $"Patch these rewardAudit fields exactly: {issueSummary}.",
                "In game_state/meta/afterlife_spiritual_conflict_state.json, inspect the listed recentConflicts[] or activeConflict terminal proof and decide whether diceAudit.outcomeBand is player_success/decisive_player_success.",
                "If the outcome is negotiated/training/withdrawn/non-victory, remove rewardAudit and matching currency reward deltas; keep the conflict resolution, learning evidence, chronicle, and prose unchanged.",
                "If the outcome is a valid reward-bearing victory, patch rewardAudit.realm/currency/conflictId to match the conflict proof and realm authority without changing dice or outcome.",
                "After file repairs are complete, call Complete-BoeValidationRepair as the last action, or create game_state/control/validation_repair_ready.json with exact sessionId/requestId/turnNumber from the current validation_repair_request.json."
            },
            DoNotDo = new List<string>
            {
                "Do not create a new turn or write ready/turn_complete.json during validation repair.",
                "Do not upgrade a negotiated, training-only, withdrawn, or failed conflict into a victory to preserve a reward.",
                "Do not reroll dice, change exchangeLog outcomes, or invent a new conflictId just to satisfy reward validation.",
                "Do not delete valid non-reward consequences such as special-art learning evidence, relationship changes, or afterlife chronicles unless validation explicitly rejects them.",
                "Do not read implementation code such as BookOfEternityClient/**/*.cs to infer reward rules; use this packet, validation_repair_request.json, afterlife docs/examples, and session state."
            }
        };
    }

    private static ValidationRepairHarnessPacket BuildAfterlifeEntityProfileScaffoldRepairPacket(
        IReadOnlyList<ValidationIssue> profileErrors)
    {
        var targetFiles = profileErrors
            .Select(issue => NormalizeRepairTargetPath(issue.FilePath))
            .Where(path => !string.IsNullOrWhiteSpace(path))
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .OrderBy(path => path, StringComparer.OrdinalIgnoreCase)
            .ToList();

        if (!targetFiles.Contains("game_state/meta/afterlife_entity_profiles.json", StringComparer.OrdinalIgnoreCase))
            targetFiles.Add("game_state/meta/afterlife_entity_profiles.json");

        var actorNames = CollectRepairActorNames(profileErrors)
            .OrderBy(actor => actor, StringComparer.OrdinalIgnoreCase)
            .ToList();
        if (actorNames.Count == 0)
            actorNames.Add("afterlife entity profile from validation_repair_request.json");

        var issueDetails = profileErrors
            .Select(DescribeAfterlifeEntityProfileScaffoldRepairIssue)
            .Where(detail => !string.IsNullOrWhiteSpace(detail))
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .OrderBy(detail => detail, StringComparer.OrdinalIgnoreCase)
            .ToList();
        var issueSummary = issueDetails.Count == 0
            ? "see validation_repair_request.json.errors for exact profile fields and expected/actual values"
            : string.Join("; ", issueDetails);
        var hasMissingMemoryOwner = profileErrors.Any(issue => string.Equals(
            issue.Code,
            "afterlife_relevant_actor_missing_canonical_memory_owner",
            StringComparison.OrdinalIgnoreCase));

        return new ValidationRepairHarnessPacket
        {
            Kind = "afterlife_entity_profile_scaffold_repair",
            Priority = "high",
            Title = "Afterlife entity profile scaffold and special-art learning repair",
            TargetFiles = targetFiles,
            TemplateRefs = new List<string>
            {
                "OtherGuides/Afterlife_Contract_Matrix.md",
                "Examples/E_CLI_Afterlife_Turns.txt"
            },
            CanonicalActorNames = actorNames,
            ExpectedShape = new List<string>
            {
                "Every significant afterlife entity profile must keep goals as an object with goalId, shortTermGoal, longTermGoal, plan, gmThoughtsSummary, and updatedAtTurn.",
                "progressionStrategy is a required object with strategyId, summary, priorityOrder[], resourceReserve, allowedSpends[], forbiddenSpends[], and optional lastUpdatedAtTurn.",
                "ledger is a required array; use [] when no visible profile events exist yet. progressionLedger is optional but must be an array of complete entries if present.",
                "profileCommands.specialArtLearningReceipts[] entries require receiptId, artId, teacherActorType, teacherActorId, playerActorId, trainingConditionSatisfied=true, learnedAtTurn, roleplayEvidence, summary, and initialTier absent or 0.",
                "A special-art learning receipt must not grant a higher tier; make sure the source teacher art canTeachPlayer=true.",
                "Every afterlife name kept in relevant actors as an independent decision-maker resolves to a canonical Guardian, resident, afterlife entity profile, or Shining faction with its own memory surface."
            },
            SafeCorrectionRules = new List<string>
            {
                "Use validation_repair_request.json.errors as the immediate checklist; patch only the listed profile/receipt fields unless adjacent minimal scaffold is required to validate.",
                "Repair missing profile scaffold in place; do not delete the profile, teacher, learned art, or relationship evidence to silence shape errors.",
                "For a missing memory owner, either materialize the genuine independent actor with a stable id and complete afterlife profile, or remove a non-actor/invented label from relevant actors and its standalone Actor Brain block.",
                "Use player-facing Russian prose in summaries/goals while keeping canonical JSON keys and enum-like values valid.",
                "If special-art learning happened, preserve the learning proof and complete the receipt shape instead of converting it into unrelated narrative only."
            },
            Steps = new List<string>
            {
                "Open game_state/control/validation_repair_request.json and game_state/meta/afterlife_entity_profiles.json first.",
                hasMissingMemoryOwner
                    ? "For afterlife_relevant_actor_missing_canonical_memory_owner, decide from existing canonical state whether the named independent actor is genuine: materialize it as a complete supported afterlife profile, or remove it from output/debug_logs.json relevant actors and the standalone Actor Brain block. Do not invent a profile merely to silence validation."
                    : "Keep every already resolved canonical actor owner unchanged while repairing the listed profile fields.",
                $"Patch this minimum profile scaffold and receipt checklist: {issueSummary}.",
                "For every listed profile, make goals an object with goalId, shortTermGoal, longTermGoal, plan, gmThoughtsSummary, and updatedAtTurn; do not use an array for goals.",
                "Add or repair progressionStrategy with strategyId, summary, priorityOrder[], resourceReserve, allowedSpends[], forbiddenSpends[], and lastUpdatedAtTurn when known.",
                "Add ledger: [] when missing, or repair each ledger[] entry to an object with entryId, summary, and optional valid turnNumber.",
                "For profileCommands.specialArtLearningReceipts[], complete receiptId/artId/teacherActorType/teacherActorId/playerActorId/trainingConditionSatisfied/learnedAtTurn/roleplayEvidence/summary and keep initialTier absent or 0.",
                "After file repairs are complete, call Complete-BoeValidationRepair as the last action, or create game_state/control/validation_repair_ready.json with exact sessionId/requestId/turnNumber from the current validation_repair_request.json."
            },
            DoNotDo = new List<string>
            {
                "Do not move afterlife profile state into Mortal World NPC/player files.",
                "Do not delete learned special art data, teacher proof, goals, or relationship hooks just to avoid completing the scaffold.",
                "Do not grant initialTier > 0 through specialArtLearningReceipts; upgrades must happen through the documented progression/upgrade path.",
                "Do not collapse arrays such as priorityOrder, allowedSpends, forbiddenSpends, ledger, or progressionLedger into scalar strings.",
                "Do not read implementation code such as BookOfEternityClient/**/*.cs to infer profile scaffold rules; use this packet, validation_repair_request.json, afterlife docs/examples, and session state."
            }
        };
    }

    private static ValidationRepairHarnessPacket BuildAfterlifeChronicleStringArrayRepairPacket(
        IReadOnlyList<ValidationIssue> chronicleErrors)
    {
        var targetFiles = chronicleErrors
            .Select(issue => NormalizeRepairTargetPath(issue.FilePath))
            .Where(path => !string.IsNullOrWhiteSpace(path))
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .OrderBy(path => path, StringComparer.OrdinalIgnoreCase)
            .ToList();

        if (!targetFiles.Contains("game_state/meta/afterlife_chronicles.json", StringComparer.OrdinalIgnoreCase))
            targetFiles.Add("game_state/meta/afterlife_chronicles.json");

        var issueDetails = chronicleErrors
            .Select(DescribeAfterlifeChronicleStringArrayRepairIssue)
            .Where(detail => !string.IsNullOrWhiteSpace(detail))
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .OrderBy(detail => detail, StringComparer.OrdinalIgnoreCase)
            .ToList();
        var issueSummary = issueDetails.Count == 0
            ? "see validation_repair_request.json.errors for exact paths and expected/actual values"
            : string.Join("; ", issueDetails);

        return new ValidationRepairHarnessPacket
        {
            Kind = "afterlife_chronicle_string_array_repair",
            Priority = "high",
            Title = "Afterlife chronicle string-array shape repair",
            TargetFiles = targetFiles,
            TemplateRefs = new List<string>
            {
                "OtherGuides/Afterlife_Contract_Matrix.md",
                "Examples/E_CLI_Afterlife_Turns.txt"
            },
            ExpectedShape = new List<string>
            {
                "GM-authored changes use afterlifeChronicleUpdates[]; canonical game_state/meta/afterlife_chronicles.json stores chronicles[].",
                "persistentConsequences[] must be an array of non-empty strings. Each string is one durable consequence; do not use objects, bullets with nested fields, nulls, or empty strings.",
                "openThreads[] must be an array of non-empty strings. Each string is one unresolved hook; do not use objects, bullets with nested fields, nulls, or empty strings.",
                "eventDescriptions[] is canonical archive memory and must not be added to afterlifeChronicleUpdates[]. Keep lastEventsDescription as the current readable summary."
            },
            SafeCorrectionRules = new List<string>
            {
                "Use validation_repair_request.json.errors as the immediate checklist; its file paths and expected/actual values are authoritative.",
                "Convert each invalid persistentConsequences/openThreads element into one concise player-meaningful string, preserving the same narrative meaning.",
                "If the invalid value is an object, summarize its meaning into a single sentence string instead of keeping nested keys.",
                "Patch only afterlife chronicle fields named by the errors unless the same chronicle entry needs a minimal adjacent string-array correction to validate."
            },
            Steps = new List<string>
            {
                "Open game_state/control/validation_repair_request.json first and repair only the listed validation errors in place.",
                $"Patch these afterlife chronicle string-array fields exactly: {issueSummary}.",
                "In game_state/meta/afterlife_chronicles.json, inspect each listed chronicles[n].persistentConsequences/openThreads path and replace every invalid element with a non-empty string.",
                "Remove afterlifeChronicleUpdates from output/narrative_response.json if it is present there; output/narrative_response.json may contain only response and timestamp.",
                "Keep afterlifeChronicleUpdates only on the accepted afterlife chronicle update surface for game_state/meta/afterlife_chronicles.json, then repair the listed canonical chronicle fields.",
                "Preserve chronicleId, scopeType, scopeId, displayName, lastEventsDescription, and lastUpdatedTurn unless those exact fields are listed as validation errors.",
                "After file repairs are complete, call Complete-BoeValidationRepair as the last action, or create game_state/control/validation_repair_ready.json with exact sessionId/requestId/turnNumber from the current validation_repair_request.json."
            },
            DoNotDo = new List<string>
            {
                "Do not create a new turn or write ready/turn_complete.json during validation repair.",
                "Do not put afterlifeChronicleUpdates inside output/narrative_response.json; use the afterlife chronicle surface for game_state/meta/afterlife_chronicles.json.",
                "Do not put eventDescriptions[] inside afterlifeChronicleUpdates[]; archive eventDescriptions are read-only canonical memory.",
                "Do not replace the afterlife chronicle with Mortal worldEventsLog, NPC journals, faction chronicles, or location news.",
                "Do not read implementation code such as BookOfEternityClient/**/*.cs to infer repair rules; use this packet, validation_repair_request.json, afterlife docs/examples, and session control files."
            }
        };
    }

    private static ValidationRepairHarnessPacket BuildGuardianTradeInventoryResolutionRepairPacket(
        IReadOnlyList<ValidationIssue> tradeErrors)
    {
        var targetFiles = tradeErrors
            .Select(issue => NormalizeRepairTargetPath(issue.FilePath))
            .Where(path => !string.IsNullOrWhiteSpace(path))
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .OrderBy(path => path, StringComparer.OrdinalIgnoreCase)
            .ToList();

        if (!targetFiles.Contains("game_state/meta/guardians.json", StringComparer.OrdinalIgnoreCase))
            targetFiles.Add("game_state/meta/guardians.json");
        if (!targetFiles.Contains(GuardianTradeRequestState.PendingRequestPath, StringComparer.OrdinalIgnoreCase))
            targetFiles.Add(GuardianTradeRequestState.PendingRequestPath);
        if (!targetFiles.Contains("output/debug_logs.json", StringComparer.OrdinalIgnoreCase))
            targetFiles.Add("output/debug_logs.json");

        var issueDetails = tradeErrors
            .Select(DescribeGuardianTradeInventoryResolutionRepairIssue)
            .Where(detail => !string.IsNullOrWhiteSpace(detail))
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .OrderBy(detail => detail, StringComparer.OrdinalIgnoreCase)
            .ToList();
        var issueSummary = issueDetails.Count == 0
            ? "see validation_repair_request.json.errors for exact Guardian trade resolution errors"
            : string.Join("; ", issueDetails);

        return new ValidationRepairHarnessPacket
        {
            Kind = "guardian_trade_inventory_resolution_repair",
            Priority = "high",
            Title = "Guardian trade inventory request resolution repair",
            TargetFiles = targetFiles,
            TemplateRefs = new List<string>
            {
                "OtherGuides/Afterlife_Contract_Matrix.md",
                "Examples/E_CLI_Afterlife_Turns.txt"
            },
            ExpectedShape = new List<string>
            {
                "Read game_state/control/pending_guardian_trade_request.json as read-only authority. It supplies requestId, guardianId, guardianName, abodeId, returnCycleId, currentReputation, derivedTradeSlotCount, effectiveRarityCeilingBonusSteps, projectBonusSignature, and createdAtTurn.",
                "In game_state/meta/guardians.json, patch the matching guardian so guardian.tradeInventory.tradeCycleId equals request.returnCycleId and generatedAtUtc is a fresh ISO-8601 UTC timestamp.",
                "guardian.tradeInventory.generationReputationTier and pricingReputationTier must match the request currentReputation tier; effectiveRarityCeilingBonusSteps and projectBonusSignature must exactly match the request.",
                "guardian.tradeInventory.items must be an array with exactly request.derivedTradeSlotCount entries. Every item needs a unique non-empty slotId and player-facing item data suitable for the Guardian's domain.",
                $"Close the request with {GuardianTradeRequestState.UpdateReceiptsProperty} or guardians[].{GuardianTradeRequestState.ReceiptsProperty}: requestId, guardianId, abodeId, tradeCycleId, status=ready, itemCount matching tradeInventory.items, resolvedAtTurn > 0, and resolvedAtUtc ISO-8601 UTC timestamp."
            },
            SafeCorrectionRules = new List<string>
            {
                "Patch only the Guardian trade resolution fields named by validation plus adjacent receipt/inventory fields required for the same request to validate.",
                "If tradeInventory already contains useful items, preserve and reshape them to the request contract instead of deleting the vitrine.",
                "If tradeInventory is missing, create a compact valid vitrine with request.derivedTradeSlotCount domain-appropriate offers; use player-facing Russian names/descriptions and stable slotId values.",
                "Use the active/matching Guardian from guardians.json; do not create a new Guardian just to close the trade request.",
                "Keep pending_guardian_trade_request.json unchanged; it is client-owned authority for this repair."
            },
            Steps = new List<string>
            {
                "Open game_state/control/validation_repair_request.json first and repair only the listed Guardian trade errors in place.",
                $"Patch this Guardian trade checklist: {issueSummary}.",
                "Use Read-BoeJson -RelativePath 'game_state/control/pending_guardian_trade_request.json' to read the exact client-authored request; do not edit that file.",
                "Use Read-BoeJson -RelativePath 'game_state/meta/guardians.json' and find the guardian whose guardianId matches request.guardianId.",
                "Set that guardian.tradeInventory to a valid object matching request.returnCycleId, currentReputation tier, effectiveRarityCeilingBonusSteps, projectBonusSignature, and derivedTradeSlotCount.",
                $"Add a matching receipt through {GuardianTradeRequestState.UpdateReceiptsProperty} at the guardians root or through guardians[].{GuardianTradeRequestState.ReceiptsProperty}; include requestId, guardianId, guardianName, abodeId, tradeCycleId, status=ready, itemCount, resolvedAtTurn, and resolvedAtUtc.",
                "Repair output/debug_logs.json.gm_thoughts_markdown so the active Guardian is in Relevant actors and has situation/thoughts/actions explaining the vitrine.",
                "Write the repaired guardians.json with Write-BoeJson, then call Complete-BoeValidationRepair as the last action, or create validation_repair_ready.json with exact metadata from validation_repair_request.json."
            },
            DebugLogTemplate = string.Join(
                Environment.NewLine,
                "## Охват NPC-анализа",
                "Режим: Guardian-centric",
                "Релевантные акторы: <имя активного Хранителя>",
                "Почему они релевантны: Хранитель подготавливает торговую витрину по pending_guardian_trade_request.json.",
                "Акторы вне охвата: нет",
                "Почему они вне охвата: repair закрывает только торговую витрину активного Хранителя.",
                "",
                "## Guardian Thoughts",
                "### <имя активного Хранителя>",
                "- Ситуация: игрок запросил торговлю, и Хранитель подготавливает ограниченную витрину своего домена.",
                "- Мысли: кратко объясни, почему эти реликвии/услуги соответствуют домену и текущей репутации.",
                "- Действия: Хранитель materialize-ит tradeInventory и закрывает request receipt."),
            DoNotDo = new List<string>
            {
                "Do not create a new turn or write ready/turn_complete.json during validation repair.",
                "Do not rewrite pending_guardian_trade_request.json; it is the read-only client-authored contract for this repair.",
                "Do not delete the pending request from game_state/control; the client clears it after a valid receipt is accepted.",
                "Do not leave guardian.tradeInventory prose-only in narrative output; the vitrine must be materialized in game_state/meta/guardians.json.",
                "Do not change requestId, guardianId, returnCycleId/tradeCycleId, derivedTradeSlotCount, effectiveRarityCeilingBonusSteps, or projectBonusSignature to fit already written inventory.",
                "Do not read implementation code such as BookOfEternityClient/**/*.cs to infer trade rules; use this packet, validation_repair_request.json, afterlife docs/examples, and session state."
            }
        };
    }

    private static string DescribeAfterlifeChronicleStringArrayRepairIssue(ValidationIssue issue)
    {
        var normalizedPath = (issue.FilePath ?? string.Empty).Replace('\\', '/');
        var match = Regex.Match(
            normalizedPath,
            @"chronicles\[(?<index>\d+)\]\.(?<field>persistentConsequences|openThreads)(?:\[(?<entry>\d+)\])?",
            RegexOptions.IgnoreCase);
        var location = match.Success
            ? string.IsNullOrWhiteSpace(match.Groups["entry"].Value)
                ? $"chronicles[{match.Groups["index"].Value}].{match.Groups["field"].Value}"
                : $"chronicles[{match.Groups["index"].Value}].{match.Groups["field"].Value}[{match.Groups["entry"].Value}]"
            : NormalizeRepairTargetPath(normalizedPath);
        var expected = string.IsNullOrWhiteSpace(issue.Expected) ? "see error.expected" : issue.Expected.Trim();
        var actual = string.IsNullOrWhiteSpace(issue.Actual) ? "see error.actual" : issue.Actual.Trim();
        var code = string.IsNullOrWhiteSpace(issue.Code) ? "validation_error" : issue.Code.Trim();

        return $"{location}: expected {expected}, actual {actual} ({code})";
    }

    private static string DescribeGuardianTradeInventoryResolutionRepairIssue(ValidationIssue issue)
    {
        var location = NormalizeRepairTargetPath(issue.FilePath);
        var expected = string.IsNullOrWhiteSpace(issue.Expected) ? "matching Guardian tradeInventory/receipt for pending request" : issue.Expected.Trim();
        var actual = string.IsNullOrWhiteSpace(issue.Actual) ? "see error.actual" : issue.Actual.Trim();
        var code = string.IsNullOrWhiteSpace(issue.Code) ? "validation_error" : issue.Code.Trim();

        return $"{location}: expected {expected}, actual {actual} ({code})";
    }

    private static string DescribeAfterlifeRewardRepairIssue(ValidationIssue issue)
    {
        var normalizedPath = (issue.FilePath ?? string.Empty).Replace('\\', '/');
        var match = Regex.Match(
            normalizedPath,
            @"(?:recentConflicts|resolvedConflicts)\[(?<index>\d+)\](?:\.(?<field>rewardAudit(?:\.[A-Za-z0-9_]+)?|conflictId|diceAudit(?:\.[A-Za-z0-9_]+)?))?",
            RegexOptions.IgnoreCase);
        var location = match.Success
            ? string.IsNullOrWhiteSpace(match.Groups["field"].Value)
                ? $"recentConflicts[{match.Groups["index"].Value}]"
                : $"recentConflicts[{match.Groups["index"].Value}].{match.Groups["field"].Value}"
            : NormalizeRepairTargetPath(normalizedPath);
        var expected = string.IsNullOrWhiteSpace(issue.Expected) ? "see error.expected" : issue.Expected.Trim();
        var actual = string.IsNullOrWhiteSpace(issue.Actual) ? "see error.actual" : issue.Actual.Trim();
        var code = string.IsNullOrWhiteSpace(issue.Code) ? "validation_error" : issue.Code.Trim();

        return $"{location}: expected {expected}, actual {actual} ({code})";
    }

    private static string DescribeAfterlifeEntityProfileScaffoldRepairIssue(ValidationIssue issue)
    {
        var normalizedPath = (issue.FilePath ?? string.Empty).Replace('\\', '/');
        var match = Regex.Match(
            normalizedPath,
            @"(?:profiles\[(?<profileIndex>\d+)\]\.(?<profileField>[A-Za-z0-9_.\[\]]+)|profileCommands\.specialArtLearningReceipts\[(?<receiptIndex>\d+)\](?:\.(?<receiptField>[A-Za-z0-9_]+))?)",
            RegexOptions.IgnoreCase);
        string location;
        if (match.Success && !string.IsNullOrWhiteSpace(match.Groups["profileIndex"].Value))
        {
            location = $"profiles[{match.Groups["profileIndex"].Value}].{match.Groups["profileField"].Value}";
        }
        else if (match.Success && !string.IsNullOrWhiteSpace(match.Groups["receiptIndex"].Value))
        {
            location = string.IsNullOrWhiteSpace(match.Groups["receiptField"].Value)
                ? $"profileCommands.specialArtLearningReceipts[{match.Groups["receiptIndex"].Value}]"
                : $"profileCommands.specialArtLearningReceipts[{match.Groups["receiptIndex"].Value}].{match.Groups["receiptField"].Value}";
        }
        else
        {
            location = NormalizeRepairTargetPath(normalizedPath);
        }

        var expected = string.IsNullOrWhiteSpace(issue.Expected) ? "see error.expected" : issue.Expected.Trim();
        var actual = string.IsNullOrWhiteSpace(issue.Actual) ? "see error.actual" : issue.Actual.Trim();
        var code = string.IsNullOrWhiteSpace(issue.Code) ? "validation_error" : issue.Code.Trim();

        return $"{location}: expected {expected}, actual {actual} ({code})";
    }

    private static ValidationRepairExactFieldCorrection BuildExactFieldCorrection(ValidationIssue issue)
    {
        return new ValidationRepairExactFieldCorrection
        {
            Path = issue.FilePath?.Trim() ?? "",
            Expected = string.IsNullOrWhiteSpace(issue.Expected) ? "see error.expected" : issue.Expected.Trim(),
            Actual = string.IsNullOrWhiteSpace(issue.Actual) ? "see error.actual" : issue.Actual.Trim(),
            Code = string.IsNullOrWhiteSpace(issue.Code) ? "validation_error" : issue.Code.Trim()
        };
    }

    private static string DescribeAfterlifeActionCostRepairIssue(ValidationIssue issue)
    {
        var normalizedPath = (issue.FilePath ?? string.Empty).Replace('\\', '/');
        var match = Regex.Match(
            normalizedPath,
            @"exchangeLog\[(?<index>\d+)\]\.actionCostAudit\.(?<side>player|opposition)\.(?<field>[A-Za-z0-9_]+)",
            RegexOptions.IgnoreCase);
        var location = match.Success
            ? $"exchangeLog[{match.Groups["index"].Value}] {match.Groups["side"].Value}.{match.Groups["field"].Value}"
            : NormalizeRepairTargetPath(normalizedPath);
        var expected = string.IsNullOrWhiteSpace(issue.Expected) ? "see error.expected" : issue.Expected.Trim();
        var actual = string.IsNullOrWhiteSpace(issue.Actual) ? "see error.actual" : issue.Actual.Trim();
        var code = string.IsNullOrWhiteSpace(issue.Code) ? "validation_error" : issue.Code.Trim();

        return $"{location}: expected {expected}, actual {actual} ({code})";
    }

    private static List<string> BuildMortalNpcTargetFiles(
        IReadOnlyList<ValidationIssue> errors,
        bool includeNpcCoreWhenMissing)
    {
        var targetFiles = errors
            .Select(issue => NormalizeRepairTargetPath(issue.FilePath))
            .Where(path => !string.IsNullOrWhiteSpace(path))
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .OrderBy(path => path, StringComparer.OrdinalIgnoreCase)
            .ToList();

        if (includeNpcCoreWhenMissing &&
            !targetFiles.Contains("game_state/npcs/npc_core.json", StringComparer.OrdinalIgnoreCase))
        {
            targetFiles.Add("game_state/npcs/npc_core.json");
        }

        return targetFiles;
    }

    private static string NormalizeTrainingShowcaseRepairTargetPath(string path)
    {
        var normalized = path.Replace('\\', '/');
        if (normalized.StartsWith("game_state/npcs/npc_core.json", StringComparison.OrdinalIgnoreCase))
            return "game_state/npcs/npc_core.json";
        if (normalized.StartsWith("game_state/meta/afterlife_entity_profiles.json", StringComparison.OrdinalIgnoreCase))
            return "game_state/meta/afterlife_entity_profiles.json";
        if (normalized.StartsWith("game_state/control/pending_training_showcase_requests.json", StringComparison.OrdinalIgnoreCase))
            return "game_state/control/pending_training_showcase_requests.json";

        return NormalizeRepairTargetPath(path);
    }

    private static string NormalizeMortalSkillProgressionRepairTargetPath(string path)
    {
        var normalized = path.Replace('\\', '/');
        if (normalized.StartsWith("game_state/player/skills_active.json", StringComparison.OrdinalIgnoreCase))
            return "game_state/player/skills_active.json";
        if (normalized.StartsWith("game_state/player/skills_passive.json", StringComparison.OrdinalIgnoreCase))
            return "game_state/player/skills_passive.json";
        if (normalized.StartsWith("game_state/player/skill_mastery.json", StringComparison.OrdinalIgnoreCase))
            return "game_state/player/skill_mastery.json";
        if (normalized.StartsWith("game_state/control/pending_training_showcase_requests.json", StringComparison.OrdinalIgnoreCase))
            return "game_state/control/pending_training_showcase_requests.json";

        return NormalizeRepairTargetPath(path);
    }

    private async Task<IReadOnlyCollection<string>> ReadCurrentGuardianRepairActorNameHintsAsync()
    {
        const string path = "game_state/meta/guardians.json";
        var json = await _fs.ReadFileAsync(path);
        if (string.IsNullOrWhiteSpace(json))
            return Array.Empty<string>();

        try
        {
            if (JsonNode.Parse(json) is not JsonObject root)
                return Array.Empty<string>();

            var names = new List<string>();
            AddGuardianRepairActorNameHint(names, root["activeGuardian"]);
            if (root["guardians"] is JsonArray guardians)
            {
                foreach (var guardian in guardians)
                    AddGuardianRepairActorNameHint(names, guardian);
            }

            return names
                .Select(NormalizeRepairActorName)
                .Where(name => !string.IsNullOrWhiteSpace(name))
                .Distinct(StringComparer.OrdinalIgnoreCase)
                .OrderBy(name => name, StringComparer.OrdinalIgnoreCase)
                .ToArray();
        }
        catch
        {
            return Array.Empty<string>();
        }
    }

    private static HashSet<string> CollectRepairActorNames(
        IReadOnlyList<ValidationIssue> errors,
        IReadOnlyCollection<string>? fallbackHints = null)
    {
        var names = errors
            .Select(issue => NormalizeRepairActorName(issue.Actor))
            .Where(actor => !string.IsNullOrWhiteSpace(actor))
            .ToHashSet(StringComparer.OrdinalIgnoreCase);

        if (names.Count == 0 && fallbackHints is { Count: > 0 })
        {
            foreach (var actor in fallbackHints.Select(NormalizeRepairActorName).Where(actor => !string.IsNullOrWhiteSpace(actor)))
                names.Add(actor);
        }

        return names;
    }

    private static void AddGuardianRepairActorNameHint(List<string> names, JsonNode? node)
    {
        if (node is not JsonObject guardian)
            return;

        foreach (var key in new[] { "displayName", "name", "guardianName" })
        {
            var value = guardian[key]?.GetValue<string>();
            if (!string.IsNullOrWhiteSpace(value))
            {
                names.Add(value);
                return;
            }
        }
    }

    private static bool IsGuardianReasoningSection(string? section)
    {
        return string.Equals(section, "npc_scope", StringComparison.OrdinalIgnoreCase) ||
               string.Equals(section, "npc_reasoning", StringComparison.OrdinalIgnoreCase) ||
               string.Equals(section, "Guardians", StringComparison.OrdinalIgnoreCase) ||
               string.Equals(section, "UpdateGuardians", StringComparison.OrdinalIgnoreCase);
    }

    private static string NormalizeRepairActorName(string? actor)
    {
        if (string.IsNullOrWhiteSpace(actor))
            return string.Empty;

        return actor.Trim().TrimEnd('.', ',', ';', ':', '!', '?').Trim();
    }

    private static string NormalizeMortalBootstrapRepairTargetPath(string path)
    {
        var normalized = path.Replace('\\', '/');
        foreach (var bootstrapFile in new[]
                 {
                     "game_state/control/mortal_bootstrap_scaffold.json",
                     "lore/codex_entries.json",
                     "game_state/inventory/items.json",
                     "game_state/world/current_location.json",
                     "game_state/world/world_map.json",
                     "game_state/world/world_events.json",
                     "game_state/player/skills_active.json",
                     "game_state/player/skills_passive.json",
                     "game_state/player/skill_mastery.json"
                 })
        {
            if (normalized.StartsWith(bootstrapFile, StringComparison.OrdinalIgnoreCase))
                return bootstrapFile;
        }

        if (normalized.StartsWith("lore/current_world/", StringComparison.OrdinalIgnoreCase))
        {
            var jsonIndex = normalized.IndexOf(".json", StringComparison.OrdinalIgnoreCase);
            return jsonIndex >= 0 ? normalized[..(jsonIndex + ".json".Length)] : normalized;
        }

        return NormalizeRepairTargetPath(path);
    }

    private static string NormalizeRepairTargetPath(string path)
    {
        var normalized = path.Replace('\\', '/');
        if (normalized.StartsWith("game_state/meta/guardians.json", StringComparison.OrdinalIgnoreCase))
            return "game_state/meta/guardians.json";
        if (normalized.StartsWith("game_state/meta/guardian_projects.json", StringComparison.OrdinalIgnoreCase))
            return "game_state/meta/guardian_projects.json";
        if (normalized.StartsWith("game_state/meta/afterlife_chronicles.json", StringComparison.OrdinalIgnoreCase))
            return "game_state/meta/afterlife_chronicles.json";
        if (normalized.StartsWith("game_state/meta/afterlife_spiritual_conflict_state.json", StringComparison.OrdinalIgnoreCase))
            return "game_state/meta/afterlife_spiritual_conflict_state.json";
        if (normalized.StartsWith(GuardianAbodeResidentState.StatePath, StringComparison.OrdinalIgnoreCase))
            return GuardianAbodeResidentState.StatePath;
        if (normalized.StartsWith("game_state/world/current_location.json", StringComparison.OrdinalIgnoreCase))
            return "game_state/world/current_location.json";
        if (normalized.StartsWith("game_state/world/world_map.json", StringComparison.OrdinalIgnoreCase))
            return "game_state/world/world_map.json";
        if (normalized.StartsWith("output/narrative_response.json", StringComparison.OrdinalIgnoreCase))
            return "output/narrative_response.json";
        if (normalized.StartsWith("output/debug_logs.json", StringComparison.OrdinalIgnoreCase))
            return "output/debug_logs.json";
        foreach (var npcFile in new[]
                 {
                     "game_state/npcs/npc_core.json",
                     "game_state/npcs/npc_journals.json",
                     "game_state/npcs/npc_interaction_journal.json",
                     "game_state/npcs/npc_masks.json",
                     "game_state/npcs/npc_memory.json"
                 })
        {
            if (normalized.StartsWith(npcFile, StringComparison.OrdinalIgnoreCase))
                return npcFile;
        }
        foreach (var factionFile in new[]
                 {
                     "game_state/factions/faction_core.json",
                     "game_state/factions/faction_structure.json",
                     "game_state/factions/faction_resources.json",
                     "game_state/factions/faction_projects.json",
                     "game_state/factions/faction_custom.json",
                     "game_state/factions/faction_chronicles.json"
                 })
        {
            if (normalized.StartsWith(factionFile, StringComparison.OrdinalIgnoreCase))
                return factionFile;
        }
        return normalized;
    }

    private static string BuildGuardianScopeDebugLogTemplate(string actorList, IReadOnlyList<string> actorNames)
    {
        var builder = new StringBuilder();
        builder.AppendLine("## Охват NPC-анализа");
        builder.AppendLine("Режим: Guardian-centric");
        builder.AppendLine($"Релевантные акторы: {actorList}");
        builder.AppendLine("Почему они релевантны: ход меняет или использует активного Хранителя, поэтому его нужно явно покрыть reasoning scope.");
        builder.AppendLine("Акторы вне охвата: нет");
        builder.AppendLine("Почему они вне охвата: все измененные Guardian-сущности перечислены выше.");
        builder.AppendLine();
        builder.AppendLine("## Guardian Thoughts");
        foreach (var actor in actorNames)
        {
            builder.AppendLine($"### {actor}");
            builder.AppendLine("- Текущая локация: кратко укажи, где актор находится и остаётся ли он там или перемещается.");
            builder.AppendLine("- Ситуация: кратко опиши текущую ситуацию Хранителя в этом ходе.");
            AppendFullActorBrainDecisionTemplate(builder);
            builder.AppendLine();
        }

        return builder.ToString().TrimEnd();
    }

    private static string BuildAcceptedTurnOutputArtifactDebugLogTemplate()
    {
        var builder = new StringBuilder();
        builder.AppendLine("## Охват NPC-анализа");
        builder.AppendLine("Режим: None | Scene-local | World-progression | Guardian-centric | Mixed");
        builder.AppendLine("Релевантные акторы: <точные имена из уже принятого хода или нет только для действительно actorless scope>");
        builder.AppendLine("Почему они релевантны: <сохрани уже принятое обоснование>");
        builder.AppendLine("Акторы вне охвата: <точные имена или нет>");
        builder.AppendLine("Почему они вне охвата: <сохрани уже принятое обоснование>");
        builder.AppendLine();
        builder.AppendLine("Если релевантных акторов нет, остановись после полного scope-блока и не выдумывай Actor Brain.");
        builder.AppendLine("Если релевантные акторы есть, сохрани отдельный полный блок для каждого:");
        builder.AppendLine();
        builder.AppendLine("## Actor Brain 2.0");
        builder.AppendLine("### <точное имя релевантного актора>");
        builder.AppendLine("- Текущая локация: сохрани принятую локацию и решение остаться/переместиться.");
        builder.AppendLine("- Ситуация: сохрани принятую ситуацию этого же хода.");
        AppendFullActorBrainDecisionTemplate(builder);

        return builder.ToString().TrimEnd();
    }

    private static string BuildActorReasoningSubpointDebugLogTemplate(IReadOnlyList<string> actorNames)
    {
        var builder = new StringBuilder();
        builder.AppendLine("## Размышления акторов");
        foreach (var actor in actorNames)
        {
            builder.AppendLine($"### {actor}");
            builder.AppendLine("- Текущая локация: кратко укажи, где NPC находится сейчас и остаётся ли он там или перемещается.");
            builder.AppendLine("- Ситуация: кратко опиши, в каком положении актор находится в этом ходе.");
            AppendFullActorBrainDecisionTemplate(builder);
            builder.AppendLine();
        }

        return builder.ToString().TrimEnd();
    }

    private static string BuildNpcScopeDeclarationDebugLogTemplate(IReadOnlyList<string> actorNames)
    {
        var requiredActors = actorNames.Count > 0
            ? actorNames
            : new[] { "<actor name>" };
        var actorList = actorNames.Count > 0
            ? string.Join(", ", actorNames)
            : "<actor name 1>, <actor name 2> OR нет only for a truly actorless Scene-local turn";
        var builder = new StringBuilder();
        builder.AppendLine("## NPC Scope");
        builder.AppendLine("- Mode: Scene-local | World-progression | Guardian-centric | Mixed");
        builder.AppendLine($"- Relevant actors: {actorList}");
        builder.AppendLine("- Why relevant: <why these actors directly act, react, anchor the scene, or receive structured state>");
        builder.AppendLine("- Actors outside scope: <names or нет>");
        builder.AppendLine("- Why outside scope: <why other mentioned actors do not receive structured updates>");
        builder.AppendLine();
        builder.AppendLine("## Actor Brain 2.0");
        foreach (var actor in requiredActors)
        {
            builder.AppendLine($"### {actor}");
            builder.AppendLine("- Текущая локация: укажи, где актор находится и остаётся ли он там или перемещается.");
            builder.AppendLine("- Ситуация: опиши, что актор воспринимает и решает в этом ходу.");
            AppendFullActorBrainDecisionTemplate(builder);
            builder.AppendLine();
        }

        return builder.ToString().TrimEnd();
    }

    private static void AppendFullActorBrainDecisionTemplate(StringBuilder builder)
    {
        builder.AppendLine("- Данные профиля: перечисли только релевантные черты, отношения, память, роль, домен и текущее состояние.");
        builder.AppendLine("- Мотивация: чего актор хочет добиться именно сейчас и почему.");
        builder.AppendLine("- Ограничения: чего актор не знает, не может или не станет делать.");
        builder.AppendLine("- Мысли: кратко опиши внутреннюю оценку ситуации от лица актора.");
        builder.AppendLine("- Варианты стратегий:");
        builder.AppendLine("  1. <реально отличимая стратегия>. Выгода: <что получает актор>. Риск: <чем он рискует>.");
        builder.AppendLine("  2. <реально отличимая стратегия>. Выгода: <что получает актор>. Риск: <чем он рискует>.");
        builder.AppendLine("- Выбранная стратегия: назови итоговую линию поведения.");
        builder.AppendLine("- Почему альтернативы отвергнуты: объясни, почему остальные стратегии хуже в текущем контексте.");
        builder.AppendLine("- Действия: кратко опиши, что актор делает, решает или меняет.");
        builder.AppendLine("- Изменения состояния: перечисли точные canonical surfaces, включая собственный журнал мыслей, или явно обоснуй отсутствие изменения.");
    }

    private async Task<GmWorkerValidationRepairDispatchResult> RunWorkerValidationRepairIfAvailableAsync(
        IReadOnlyList<ValidationIssue> prioritizedErrors,
        (string SessionId, string RequestId, int TurnNumber) requestMetadata,
        string createdAtUtc,
        int attempt,
        string expectedSessionGeneration)
    {
        try
        {
            var workerFileSystem = _validator.CanonicalFileSystem;
            var audit = new GmWorkerAuditLog(workerFileSystem);
            var delegator = new GmWorkerValidationRepairDelegator(
                workerFileSystem,
                new GmWorkerBridgePool(
                    workerFileSystem,
                    new GmWorkerProposalStore(workerFileSystem),
                    audit),
                new GmWorkerApplyGate(_validator, audit),
                audit);
            var result = await delegator.TryRunAsync(
                _stateManager.Settings.GmWorkerBridgeProfiles,
                prioritizedErrors,
                new WorkerTurnReference
                {
                    SessionId = requestMetadata.SessionId,
                    RequestId = requestMetadata.RequestId,
                    TurnNumber = requestMetadata.TurnNumber
                },
                createdAtUtc,
                attempt,
                expectedSessionGeneration);

            if (result.Outcome is not GmWorkerValidationRepairOutcome.SkippedNoWorker and
                not GmWorkerValidationRepairOutcome.Applied)
            {
                _logger.LogWarning(
                    "GM worker validation repair ended with {Outcome}: {Reason}. Legacy repair loop remains active.",
                    result.Outcome,
                    result.FallbackReason);
            }

            return result;
        }
        catch (SessionReplacedException ex)
        {
            return new GmWorkerValidationRepairDispatchResult
            {
                Outcome = GmWorkerValidationRepairOutcome.SessionReplaced,
                FallbackReason = ex.Message
            };
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Failed to run GM worker validation repair. Legacy repair loop remains active.");
            return new GmWorkerValidationRepairDispatchResult
            {
                Outcome = GmWorkerValidationRepairOutcome.WorkerFailed,
                FallbackReason = ex.Message
            };
        }
    }

    private async Task WriteTerminalProtocolFailureRequestAsync(string source, List<ValidationIssue> errors)
    {
        var prioritizedErrors = PrioritizeValidationErrors(errors).ToList();
        var pendingSnapshot = await ResolveActivePendingTurnSnapshotContextAsync();
        var requestMetadata = BuildProtocolRequestMetadata(pendingSnapshot);
        var metadataDiagnosticOnly = BuildProtocolRequestMetadataDiagnosticOnly(pendingSnapshot);
        var degradedMetadataWarning = BuildProtocolRequestMetadataWarning(pendingSnapshot);
        var gmInstructions =
            "Клиент отклонил terminal ready signal как protocol failure. Это НЕ validation_repair_request.json и НЕ repair loop. " +
            "Не создавай validation_repair_ready.json и не пытайся продолжать этот уже закрытый wait cycle. " +
            "Прочитай TaskGuides/CLI_Step_Main.txt и Examples/E_CLI_Step_Main.txt, разберись с terminal protocol problem по списку ошибок ниже и исправь логику для следующего корректного хода.";
        if (!string.IsNullOrWhiteSpace(degradedMetadataWarning))
            gmInstructions += " " + degradedMetadataWarning;

        var request = new TerminalProtocolFailureRequest
        {
            SessionId = requestMetadata.SessionId,
            RequestId = requestMetadata.RequestId,
            TurnNumber = requestMetadata.TurnNumber,
            MetadataDiagnosticOnly = metadataDiagnosticOnly,
            Source = source,
            DetectedAtUtc = DateTime.UtcNow.ToString("o"),
            GmInstructions = gmInstructions,
            SummaryGroups = BuildValidationSummaryLines(prioritizedErrors, 6),
            Errors = prioritizedErrors.Select(e => new ValidationRepairIssue
            {
                Code = e.Code ?? "terminal_protocol_failure",
                FilePath = e.FilePath,
                Severity = e.Severity.ToString(),
                Category = e.Category.ToString(),
                Message = e.Message,
                Actor = e.Actor,
                Section = e.Section,
                Expected = e.Expected,
                Actual = e.Actual,
                RepairHint = e.RepairHint ?? "Исправь terminal completion protocol так, чтобы клиент получил ровно один корректный terminal signal."
            }).ToList()
        };

        await _fs.WriteFileAtomicAsync(TerminalProtocolFailureRequestPath, JsonSerializer.Serialize(request, JsonOpts));
    }

    private static string BuildValidationRepairRequestInstructions(PendingTurnSnapshotResolution pendingSnapshot)
    {
        const string commonPrefix =
            "Текущий ответ/состояние отклонены клиентом. Исправь уже записанные файлы in place, ориентируясь на список ошибок ниже. " +
            "Прочитай TaskGuides/CLI_Step_Main.txt и Examples/E_CLI_Step_Main.txt. ";
        const string commonSuffix =
            "Если ошибка касается canonical accumulated state (guardians/quests/factions/rival_soul_arcs и т.п.), правь итоговое canonical состояние явно: нужное удаление или замена должны остаться и после повторной нормализации. " +
            "Если клиент переписал этот repair request повторно, используй ТОЛЬКО самые свежие metadata из текущего файла.";

        var repairReadyInstruction = pendingSnapshot.Status switch
        {
            PendingTurnSnapshotResolutionStatus.Missing =>
                "Текущий validation_repair_request.json использует diagnostic-only sentinel metadata. Не создавай validation_repair_ready.json по этим sessionId/requestId/turnNumber. Сначала восстанови pending snapshot context, дождись самого свежего client-authored repair request с authoritative metadata и только потом используй его ids для validation_repair_ready.json. ",
            PendingTurnSnapshotResolutionStatus.Unusable =>
                "Текущий validation_repair_request.json использует diagnostic-only sentinel metadata. Не создавай validation_repair_ready.json по этим sessionId/requestId/turnNumber. Сначала восстанови pending snapshot authority/integrity, дождись самого свежего client-authored repair request с authoritative metadata и только потом используй его ids для validation_repair_ready.json. ",
            _ =>
                "После исправлений создай game_state/control/validation_repair_ready.json с sessionId/requestId/turnNumber. "
        };

        return commonPrefix + repairReadyInstruction + commonSuffix;
    }

    private static (string SessionId, string RequestId, int TurnNumber) BuildProtocolRequestMetadata(
        PendingTurnSnapshotResolution pendingSnapshot)
    {
        return pendingSnapshot is
        {
            Status: PendingTurnSnapshotResolutionStatus.Usable,
            Context: not null
        }
            ? (pendingSnapshot.Context.SessionId, pendingSnapshot.Context.RequestId, pendingSnapshot.Context.TurnNumber)
            : (string.Empty, string.Empty, 0);
    }

    private static bool BuildProtocolRequestMetadataDiagnosticOnly(PendingTurnSnapshotResolution pendingSnapshot)
    {
        return pendingSnapshot.Status is PendingTurnSnapshotResolutionStatus.Missing or PendingTurnSnapshotResolutionStatus.Unusable;
    }

    private static string BuildProtocolRequestMetadataWarning(PendingTurnSnapshotResolution pendingSnapshot)
    {
        return pendingSnapshot.Status switch
        {
            PendingTurnSnapshotResolutionStatus.Missing =>
                "Validated pending snapshot context сейчас отсутствует, поэтому sessionId/requestId/turnNumber в этом файле заполнены sentinel-значениями и служат только для диагностики. Не копируй их в validation_repair_ready.json и не используй как active correlation metadata; сначала восстанови pending snapshot context и затем используй metadata из самого свежего client-authored request.",
            PendingTurnSnapshotResolutionStatus.Unusable =>
                "Validated pending snapshot context сейчас unreadable или invalid, поэтому sessionId/requestId/turnNumber в этом файле заполнены sentinel-значениями и служат только для диагностики. Не копируй их в validation_repair_ready.json и не используй как active correlation metadata; сначала восстанови pending snapshot authority/integrity и затем используй metadata из самого свежего client-authored request.",
            _ => string.Empty
        };
    }

    private static string BuildInvalidRepairReadyRepairHint(PendingTurnSnapshotResolution pendingSnapshot)
    {
        return pendingSnapshot.Status switch
        {
            PendingTurnSnapshotResolutionStatus.Missing =>
                "Не копируй sentinel metadata из текущего validation_repair_request.json. Сначала восстанови pending snapshot context, дождись самого свежего client-authored repair request с authoritative metadata и только потом перепиши validation_repair_ready.json валидным JSON.",
            PendingTurnSnapshotResolutionStatus.Unusable =>
                "Не копируй sentinel metadata из текущего validation_repair_request.json. Сначала восстанови pending snapshot authority/integrity, дождись самого свежего client-authored repair request с authoritative metadata и только потом перепиши validation_repair_ready.json валидным JSON.",
            _ =>
                "Перезапиши validation_repair_ready.json валидным JSON и скопируй в него точные sessionId/requestId/turnNumber из validation_repair_request.json."
        };
    }

    private static string BuildMismatchedRepairReadyRepairHint(PendingTurnSnapshotResolution pendingSnapshot)
    {
        return pendingSnapshot.Status switch
        {
            PendingTurnSnapshotResolutionStatus.Missing =>
                "Не копируй sentinel metadata из текущего validation_repair_request.json. Сначала восстанови pending snapshot context, дождись самого свежего client-authored repair request с authoritative metadata и только потом пересоздай validation_repair_ready.json.",
            PendingTurnSnapshotResolutionStatus.Unusable =>
                "Не копируй sentinel metadata из текущего validation_repair_request.json. Сначала восстанови pending snapshot authority/integrity, дождись самого свежего client-authored repair request с authoritative metadata и только потом пересоздай validation_repair_ready.json.",
            _ =>
                "Пересоздай validation_repair_ready.json и скопируй sessionId/requestId/turnNumber ровно из validation_repair_request.json."
        };
    }

    private static List<string> BuildValidationSummaryLines(IEnumerable<ValidationIssue> issues, int maxGroups)
    {
        return issues
            .GroupBy(issue => new
            {
                issue.Category,
                Section = string.IsNullOrWhiteSpace(issue.Section) ? "General" : issue.Section
            })
            .OrderByDescending(group => group.Count())
            .ThenBy(group => group.Key.Category.ToString(), StringComparer.OrdinalIgnoreCase)
            .ThenBy(group => group.Key.Section, StringComparer.OrdinalIgnoreCase)
            .Take(maxGroups)
            .Select(group => $"{FormatIssueCategory(group.Key.Category)} / {group.Key.Section}: {group.Count()}")
            .ToList();
    }

    private static IEnumerable<ValidationIssue> PrioritizeValidationErrors(IEnumerable<ValidationIssue> errors)
    {
        return errors
            .OrderByDescending(GetValidationIssuePriority)
            .ThenBy(issue => string.IsNullOrWhiteSpace(issue.Section) ? "zzzz" : issue.Section, StringComparer.OrdinalIgnoreCase)
            .ThenBy(issue => issue.FilePath, StringComparer.OrdinalIgnoreCase)
            .ThenBy(issue => issue.Code ?? "zzzz", StringComparer.OrdinalIgnoreCase)
            .ThenBy(issue => issue.Message, StringComparer.OrdinalIgnoreCase);
    }

    private static int GetValidationIssuePriority(ValidationIssue issue)
    {
        var score = 0;

        if (!string.IsNullOrWhiteSpace(issue.RepairHint))
            score += 40;
        if (!string.IsNullOrWhiteSpace(issue.Code))
            score += 30;
        if (!string.IsNullOrWhiteSpace(issue.Expected) || !string.IsNullOrWhiteSpace(issue.Actual))
            score += 20;
        if (issue.Category == IssueCategory.ProtocolViolation)
            score += 10;

        if (IsGenericShapeError(issue))
            score -= 60;

        return score;
    }

    private static bool IsGenericShapeError(ValidationIssue issue)
    {
        if (!string.IsNullOrWhiteSpace(issue.Code))
            return false;

        var message = issue.Message ?? string.Empty;
        return message.StartsWith("Отсутствует обязательное поле", StringComparison.OrdinalIgnoreCase) ||
               message.StartsWith("Отсутствует обязательное строковое поле", StringComparison.OrdinalIgnoreCase) ||
               message.StartsWith("Отсутствует обязательное числовое или строковое поле", StringComparison.OrdinalIgnoreCase) ||
               message.StartsWith("Отсутствует обязательный объект", StringComparison.OrdinalIgnoreCase) ||
               message.StartsWith("Поле должно быть", StringComparison.OrdinalIgnoreCase) ||
               message.StartsWith("Элемент должен быть", StringComparison.OrdinalIgnoreCase) ||
               message.StartsWith("Требуется хотя бы одно поле", StringComparison.OrdinalIgnoreCase);
    }

    private static string BuildIssueDisplayLabel(ValidationIssue issue)
    {
        var prefix = $"[{FormatIssueCategory(issue.Category)}";
        if (!string.IsNullOrWhiteSpace(issue.Section))
            prefix += $" / {issue.Section}";
        if (!string.IsNullOrWhiteSpace(issue.Code))
            prefix += $" / {issue.Code}";
        prefix += "]";

        return $"{prefix} {issue.Message}";
    }

    private static string FormatIssueCategory(IssueCategory category) => category switch
    {
        IssueCategory.ProtocolViolation => "Protocol",
        IssueCategory.ClientOwnedSurface => "Client-Owned",
        _ => "State"
    };

    private async Task<(ValidationRepairDispatchState Dispatch, List<ValidationIssue> ReportErrors)> ReportRejectedRepairReadyAsync(
        string source, List<ValidationIssue> baseErrors, int attempt,
        string code, string message, string expected, string actual, string repairHint,
        string expectedSessionGeneration)
    {
        var reportErrors = new List<ValidationIssue>
        {
            new(
                ValidationRepairReadyPath,
                IssueSeverity.Error,
                message,
                code: code,
                section: "validation_repair_ready",
                expected: expected,
                actual: actual,
                repairHint: repairHint)
        };

        var dispatch = await WriteValidationRepairRequestForSessionAsync(
            source,
            reportErrors,
            attempt,
            expectedSessionGeneration);
        return (dispatch, reportErrors);
    }

    private static ValidationRepairReady? ReadValidationRepairReady(string? json)
    {
        if (string.IsNullOrWhiteSpace(json))
            return null;

        try
        {
            return JsonSerializer.Deserialize<ValidationRepairReady>(json, JsonOpts);
        }
        catch
        {
            return null;
        }
    }

    private bool IsMatchingRepairReady(ValidationRepairReady ready, ValidatedPendingTurnSnapshotContext? snapshotContext)
    {
        if (snapshotContext == null)
            return false;

        return ready.TurnNumber == snapshotContext.TurnNumber &&
               !string.IsNullOrWhiteSpace(ready.RequestId) &&
               string.Equals(ready.RequestId, snapshotContext.RequestId, StringComparison.OrdinalIgnoreCase) &&
               !string.IsNullOrWhiteSpace(ready.SessionId) &&
               string.Equals(ready.SessionId, snapshotContext.SessionId, StringComparison.OrdinalIgnoreCase);
    }

    private static string BuildExpectedRepairContext(PendingTurnSnapshotResolution resolution)
    {
        return resolution.Status switch
        {
            PendingTurnSnapshotResolutionStatus.Missing =>
                "Existing validated pending turn snapshot context for the active repair cycle",
            PendingTurnSnapshotResolutionStatus.Unusable =>
                "Readable and validated pending turn snapshot context for the active repair cycle",
            _ when resolution.Context != null =>
                $"sessionId={resolution.Context.SessionId}, requestId={resolution.Context.RequestId}, turnNumber={resolution.Context.TurnNumber}",
            _ => "Readable and validated pending turn snapshot context for the active repair cycle"
        };
    }

    private static string BuildActualRepairContext(ValidationRepairReady ready, PendingTurnSnapshotResolution resolution)
    {
        if (resolution.Status == PendingTurnSnapshotResolutionStatus.Missing)
        {
            return $"ready signal sessionId={ready.SessionId}, requestId={ready.RequestId}, turnNumber={ready.TurnNumber}; validated pending snapshot context is missing";
        }

        if (resolution.Status == PendingTurnSnapshotResolutionStatus.Unusable)
        {
            return $"ready signal sessionId={ready.SessionId}, requestId={ready.RequestId}, turnNumber={ready.TurnNumber}; validated pending snapshot context is unreadable or invalid";
        }

        return $"ready signal sessionId={ready.SessionId}, requestId={ready.RequestId}, turnNumber={ready.TurnNumber}";
    }

    private static string TruncateDiagnosticValue(string value, int maxLength = 280)
    {
        var normalized = value.Replace("\r\n", " ").Replace('\n', ' ').Trim();
        if (normalized.Length <= maxLength)
            return normalized;
        return normalized[..maxLength] + "...";
    }

    private async Task DeleteValidationRepairFilesAsync()
    {
        await using var writeLease = await _fs.AcquireCanonicalWriteLeaseAsync();
        DeleteValidationRepairFiles(writeLease);
    }

    private async Task DeleteValidationRepairFilesForSessionAsync(string expectedSessionGeneration)
    {
        await using var writeLease = await _fs.AcquireCanonicalWriteLeaseAsync();
        ThrowIfRepairSessionReplaced(writeLease, expectedSessionGeneration);
        DeleteValidationRepairFiles(writeLease);
    }

    private void DeleteValidationRepairFiles(FileSystemManager.CanonicalWriteLease writeLease)
    {
        _fs.DeleteFile(writeLease, ValidationRepairReadyPath);
        _fs.DeleteFile(writeLease, ValidationRepairRequestPath);
        _fs.DeleteFile(writeLease, ValidationRepairArtifactStallReportPath);
    }

    private Task DeleteTerminalProtocolFailureRequestAsync()
    {
        if (_fs.FileExists(TerminalProtocolFailureRequestPath))
            _fs.DeleteFile(TerminalProtocolFailureRequestPath);
        return Task.CompletedTask;
    }

    private Task DeleteTerminalProtocolFailureRequestAsync(
        FileSystemManager.CanonicalWriteLease writeLease)
    {
        _fs.DeleteFile(writeLease, TerminalProtocolFailureRequestPath);
        return Task.CompletedTask;
    }

    private Task DeleteValidationRepairReadyAsync()
    {
        return DeleteValidationRepairReadyCoreAsync();
    }

    private async Task DeleteValidationRepairReadyCoreAsync()
    {
        await using var writeLease = await _fs.AcquireCanonicalWriteLeaseAsync();
        _fs.DeleteFile(writeLease, ValidationRepairReadyPath);
    }

    private async Task DeleteValidationRepairReadyForSessionAsync(string expectedSessionGeneration)
    {
        await using var writeLease = await _fs.AcquireCanonicalWriteLeaseAsync();
        ThrowIfRepairSessionReplaced(writeLease, expectedSessionGeneration);
        _fs.DeleteFile(writeLease, ValidationRepairReadyPath);
    }

    private async Task WriteValidationRepairFileForSessionAsync(
        string relativePath,
        string content,
        string expectedSessionGeneration)
    {
        await using var writeLease = await _fs.AcquireCanonicalWriteLeaseAsync();
        ThrowIfRepairSessionReplaced(writeLease, expectedSessionGeneration);
        await _fs.WriteFileAtomicAsync(writeLease, relativePath, content);
    }

    private async Task<string?> ReadValidationRepairFileForSessionAsync(
        string relativePath,
        string expectedSessionGeneration)
    {
        await using var writeLease = await _fs.AcquireCanonicalWriteLeaseAsync();
        ThrowIfRepairSessionReplaced(writeLease, expectedSessionGeneration);
        return await _fs.ReadFileAsync(writeLease, relativePath);
    }

    private void ThrowIfRepairSessionReplaced(
        FileSystemManager.CanonicalWriteLease writeLease,
        string expectedSessionGeneration)
    {
        if (!_fs.IsCurrentSessionGeneration(writeLease, expectedSessionGeneration))
        {
            throw new GmWorkerSessionReplacedException(
                "The validation-repair cycle belongs to a game session that is no longer current.");
        }
    }

    private async Task ShowTurnErrorMessageAsync(string readyErrorPath)
    {
        ShowTurnErrorMessage(await _fs.ReadFileAsync(readyErrorPath));
    }

    private void ShowTurnErrorMessage(string? errorJson)
    {
        string errorMsg;
        if (errorJson == null)
        {
            errorMsg = "Ошибка ожидания ответа GM";
        }
        else
        {
            try
            {
                using var errorDoc = JsonDocument.Parse(errorJson);
                errorMsg = errorDoc.RootElement.TryGetProperty("error", out var e)
                    ? e.GetString() ?? errorJson
                    : errorJson;
            }
            catch
            {
                errorMsg = errorJson;
            }
        }

        var recoveryText = "Действие не было применено. Состояние возвращается к последней стабильной версии; после возврата к ходу можно повторить действие или выбрать другой путь.";
        var pressAnyKey = _loc.T("press_any_key");
        AnsiConsole.MarkupLine($"[red]❌ Ошибка GM: {GameInterface.EscapeMarkup(errorMsg)}[/]");
        AnsiConsole.MarkupLine($"[yellow]{GameInterface.EscapeMarkup(recoveryText)}[/]");
        AnsiConsole.MarkupLine($"[grey]{GameInterface.EscapeMarkup(pressAnyKey)}[/]");

        if (_inputSource is AgentConsoleLiveInputSource liveInput)
        {
            var plainText = string.Join(Environment.NewLine, new[]
            {
                "Ошибка GM",
                errorMsg,
                "",
                recoveryText,
                pressAnyKey
            });
            liveInput.PublishSnapshot(new AgentConsoleSnapshot
            {
                ScreenId = "gm-turn-error",
                Mode = AgentConsoleMode.Error,
                Title = "Ошибка GM",
                PlainText = plainText,
                AwaitingInput = true,
                InputKind = AgentConsoleInputKind.Key,
                Actions =
                [
                    new AgentConsoleAction
                    {
                        Id = "continue",
                        Label = "Продолжить",
                        Shortcut = "Enter",
                        IsDefault = true
                    }
                ],
                RenderedAtUtc = DateTimeOffset.UtcNow,
                UpdatedAtUtc = DateTimeOffset.UtcNow,
                Diagnostics =
                [
                    new AgentConsoleDiagnostic
                    {
                        Severity = AgentConsoleDiagnosticSeverity.Error,
                        Code = "gm-turn-error",
                        Message = "GM turn ended with a terminal error.",
                        Detail = errorMsg
                    }
                ]
            }, "Rendered GM turn error.");
        }

        _inputSource.ReadKey(intercept: true);
    }

    private async Task CleanupUndispatchedTransitionPrepAsync(RollbackSnapshot? rollbackSnapshot,
        bool localStateMutated, bool manifestCreated)
    {
        if (HasRollbackCapability(rollbackSnapshot))
        {
            if (localStateMutated)
                await RestorePreTurnBackup(rollbackSnapshot!);
            CleanupBackup(rollbackSnapshot!);
        }

        if (manifestCreated)
            await CleanupPendingTurnSnapshotAsync();
    }

    private async Task<bool> HandleRejectedActiveReadySignalAsync(string sourceLabel,
        ReadySignalMetadata? signal,
        ValidatedPendingTurnSnapshotContext? snapshotContext,
        RollbackSnapshot? rollbackSnapshot)
    {
        var protocolErrors = BuildRejectedActiveReadySignalIssues(sourceLabel, signal, snapshotContext);
        if (!await DiscardMismatchedReadySignalAsync(sourceLabel, signal, snapshotContext, preservePendingSnapshot: true))
            return false;

        await WriteTerminalProtocolFailureRequestAsync($"terminal protocol failure: {sourceLabel}", protocolErrors);
        _fs.DeleteFile("input/turn_request.json");

        AnsiConsole.MarkupLine("[yellow]⚠ Текущий ответ GM отклонён клиентом. Состояние возвращено к последней стабильной версии.[/]");

        if (HasRollbackCapability(rollbackSnapshot))
        {
            await RestorePreTurnBackup(rollbackSnapshot!);
            CleanupBackup(rollbackSnapshot!);
            AnsiConsole.MarkupLine("[yellow]↩ Последняя стабильная версия состояния восстановлена после отклонения ответа GM.[/]");
        }

        await CleanupPendingTurnSnapshotAsync();
        return true;
    }

    private async Task HandleMissingActiveTerminalOutcomeAsync(
        ValidatedPendingTurnSnapshotContext? snapshotContext,
        RollbackSnapshot? rollbackSnapshot,
        bool turnCompleteExists,
        bool turnErrorExists)
    {
        var errors = new List<ValidationIssue>
        {
            new(
                "ready/turn_complete.json",
                IssueSeverity.Error,
                "После завершения ожидания не осталось ни одного коррелированного terminal signal для активного хода",
                code: "missing_correlated_terminal_signal_after_wait",
                section: "terminal_ready",
                expected: "Exactly one correlated ready/turn_complete.json or ready/turn_error.json for the active turn",
                actual: BuildMissingActiveTerminalOutcomeActual(
                    snapshotContext,
                    turnCompleteExists,
                    turnErrorExists),
                repairHint: "Записывай ровно один terminal signal с точными sessionId/requestId/turnNumber, не удаляй и не перезаписывай его после записи и не смешивай terminal protocol failure с validation repair loop.")
        };

        await WriteTerminalProtocolFailureRequestAsync("missing correlated terminal signal after wait", errors);
        _fs.DeleteFile("input/turn_request.json");
        ClearReadySignals();
        ClearTransientOutputFiles();

        AnsiConsole.MarkupLine("[yellow]⚠ Клиент не смог безопасно принять ответ GM и восстановил последнюю стабильную версию состояния.[/]");

        if (HasRollbackCapability(rollbackSnapshot))
        {
            await RestorePreTurnBackup(rollbackSnapshot!);
            CleanupBackup(rollbackSnapshot!);
            AnsiConsole.MarkupLine("[yellow]↩ Последняя стабильная версия состояния восстановлена после потери корректного ответа GM.[/]");
        }

        await CleanupPendingTurnSnapshotAsync();
    }

    private async Task<ConcurrentTerminalSignalResolution> ResolveConcurrentActiveTerminalSignalsAsync(
        TerminalSignalSnapshot terminalSignals,
        ReadySignalMetadata? completionSignal,
        ReadySignalMetadata? errorSignal,
        ValidatedPendingTurnSnapshotContext? snapshotContext,
        RollbackSnapshot? rollbackSnapshot)
    {
        if (!terminalSignals.CompletionExists || !terminalSignals.ErrorExists)
        {
            return new ConcurrentTerminalSignalResolution(
                Failed: false,
                UseCompletion: terminalSignals.CompletionExists,
                UseError: terminalSignals.ErrorExists);
        }

        if (snapshotContext == null)
            return new ConcurrentTerminalSignalResolution(false, true, true);

        if (completionSignal != null &&
            IsMatchingReadySignal(completionSignal, snapshotContext) &&
            !HasValidTerminalSignalContract("turn_complete", completionSignal))
        {
            var failed = await HandleRejectedActiveReadySignalAsync(
                "turn_complete",
                completionSignal,
                snapshotContext,
                rollbackSnapshot);
            return new ConcurrentTerminalSignalResolution(failed, false, false);
        }

        if (errorSignal != null &&
            IsMatchingReadySignal(errorSignal, snapshotContext) &&
            !HasValidTerminalSignalContract("turn_error", errorSignal))
        {
            var failed = await HandleRejectedActiveReadySignalAsync(
                "turn_error",
                errorSignal,
                snapshotContext,
                rollbackSnapshot);
            return new ConcurrentTerminalSignalResolution(failed, false, false);
        }

        var completionMatches = completionSignal != null &&
                                IsMatchingReadySignal(completionSignal, snapshotContext) &&
                                HasValidTerminalSignalContract("turn_complete", completionSignal);
        var errorMatches = errorSignal != null &&
                           IsMatchingReadySignal(errorSignal, snapshotContext) &&
                           HasValidTerminalSignalContract("turn_error", errorSignal);

        if (completionMatches && !errorMatches)
        {
            _logger.LogWarning("Удаляется competing terminal error signal во время active wait; success signal остаётся authoritative.");
            _fs.DeleteFile("ready/turn_error.json");
            return new ConcurrentTerminalSignalResolution(false, true, false);
        }

        if (errorMatches && !completionMatches)
        {
            _logger.LogWarning("Удаляется competing terminal success signal во время active wait; error signal остаётся authoritative.");
            _fs.DeleteFile("ready/turn_complete.json");
            return new ConcurrentTerminalSignalResolution(false, false, true);
        }

        var errors = new List<ValidationIssue>
        {
            new(
                "ready/turn_complete.json",
                IssueSeverity.Error,
                completionMatches && errorMatches
                    ? "Для одного и того же sessionId/requestId/turnNumber одновременно обнаружены ready/turn_complete.json и ready/turn_error.json"
                    : "Одновременное наличие competing terminal signals не удалось однозначно сопоставить активному ходу",
                code: "dual_terminal_ready_signals",
                section: "terminal_ready",
                expected: "Exactly one terminal signal for the active turn",
                actual: BuildConcurrentTerminalSignalActual(completionSignal, errorSignal),
                repairHint: "Для одного хода записывай ровно один terminal signal: либо ready/turn_complete.json, либо ready/turn_error.json. Не оставляй второй ready-файл как запасной вариант и не запускай repair loop для terminal conflict.")
        };

        await WriteTerminalProtocolFailureRequestAsync("dual terminal ready signals", errors);
        _fs.DeleteFile("input/turn_request.json");
        ClearReadySignals();
        ClearTransientOutputFiles();

        AnsiConsole.MarkupLine("[yellow]⚠ Клиент обнаружил внутреннюю несогласованность в ответе GM и восстановил последнюю стабильную версию состояния.[/]");

        if (HasRollbackCapability(rollbackSnapshot))
        {
            await RestorePreTurnBackup(rollbackSnapshot!);
            CleanupBackup(rollbackSnapshot!);
            AnsiConsole.MarkupLine("[yellow]↩ Последняя стабильная версия состояния восстановлена после конфликтующих ответов GM.[/]");
        }

        await CleanupPendingTurnSnapshotAsync();
        return new ConcurrentTerminalSignalResolution(true, false, false);
    }

    private static string BuildConcurrentTerminalSignalActual(ReadySignalMetadata? completionSignal,
        ReadySignalMetadata? errorSignal)
    {
        static string Describe(string label, ReadySignalMetadata? signal)
        {
            return signal == null
                ? $"{label}=missing_or_unreadable"
                : $"{label}(sessionId={signal.SessionId}, requestId={signal.RequestId}, turnNumber={signal.TurnNumber}, status={signal.Status})";
        }

        return $"{Describe("turn_complete", completionSignal)}; {Describe("turn_error", errorSignal)}";
    }

    private static string BuildMissingActiveTerminalOutcomeActual(
        ValidatedPendingTurnSnapshotContext? snapshotContext,
        bool turnCompleteExists,
        bool turnErrorExists)
    {
        var manifestDescription = snapshotContext == null
            ? "pendingSnapshot=missing"
            : $"pendingSnapshot=sessionId={snapshotContext.SessionId}, requestId={snapshotContext.RequestId}, turnNumber={snapshotContext.TurnNumber}";
        return $"turn_complete_exists={turnCompleteExists}; turn_error_exists={turnErrorExists}; {manifestDescription}";
    }

    private async Task<ReadySignalMetadata?> TryRecoverGmOutputWithoutTerminalSignalAsync(
        ReadySignalMetadata errorSignal,
        ValidatedPendingTurnSnapshotContext snapshotContext)
    {
        if (!IsRecoverableMissingTerminalSignal(errorSignal))
            return null;

        if (!IsMatchingReadySignal(errorSignal, snapshotContext))
            return null;

        if (!TryResolveTurnRequestTimestampUtc(snapshotContext, out var requestTimestampUtc))
            return null;

        foreach (var requiredFile in RecoverableGmOutputRequiredFiles)
        {
            if (!await HasFreshRecoverableGmOutputArtifactAsync(requiredFile, requestTimestampUtc))
                return null;
        }

        var filesModified = EnumerateRecoverableGmModifiedFiles(requestTimestampUtc).ToArray();
        if (filesModified.Length == 0)
            return null;

        var recoveredSignal = new
        {
            sessionId = snapshotContext.SessionId,
            requestId = snapshotContext.RequestId,
            turnNumber = snapshotContext.TurnNumber,
            timestamp = DateTime.UtcNow.ToString("o"),
            status = "success",
            harnessSource = ClientRecoveredMissingTerminalSignalHarnessSource,
            recoveredFrom = errorSignal.HarnessSource,
            originalError = errorSignal.Error,
            filesModified
        };

        var recoveredSignalJson = JsonSerializer.Serialize(recoveredSignal, JsonOpts);
        await _fs.WriteFileAtomicAsync("ready/turn_complete.json", recoveredSignalJson);
        _fs.DeleteFile("ready/turn_error.json");
        _logger.LogWarning(
            "Recovered GM output for turn {TurnNumber} after daemon emitted {HarnessSource}; synthesized ready/turn_complete.json with {FileCount} modified files.",
            snapshotContext.TurnNumber,
            errorSignal.HarnessSource,
            filesModified.Length);

        return ParseReadySignalMetadata(recoveredSignalJson, "ready/turn_complete.json");
    }

    private static bool IsRecoverableMissingTerminalSignal(ReadySignalMetadata signal) =>
        string.Equals(signal.HarnessSource, GmBridgeIdleWithoutTerminalSignalHarnessSource, StringComparison.OrdinalIgnoreCase) ||
        string.Equals(signal.HarnessSource, GmOutputWithoutTerminalSignalHarnessSource, StringComparison.OrdinalIgnoreCase);

    private bool TryResolveTurnRequestTimestampUtc(
        ValidatedPendingTurnSnapshotContext snapshotContext,
        out DateTime requestTimestampUtc)
    {
        requestTimestampUtc = DateTime.MinValue;
        var requestPath = _fs.ResolvePath("input/turn_request.json");
        if (File.Exists(requestPath))
        {
            requestTimestampUtc = File.GetLastWriteTimeUtc(requestPath);
            if (requestTimestampUtc > DateTime.MinValue)
                return true;
        }

        if (DateTimeOffset.TryParse(snapshotContext.Manifest.RequestTimestamp, out var requestTimestamp))
        {
            requestTimestampUtc = requestTimestamp.UtcDateTime;
            return true;
        }

        return false;
    }

    private async Task<bool> HasFreshRecoverableGmOutputArtifactAsync(string relativePath, DateTime requestTimestampUtc)
    {
        if (!_fs.FileExists(relativePath))
            return false;

        var json = await _fs.ReadFileAsync(relativePath);
        if (string.IsNullOrWhiteSpace(json))
            return false;

        try
        {
            using var doc = JsonDocument.Parse(json);
            if (doc.RootElement.ValueKind != JsonValueKind.Object)
                return false;

            var requiredTextProperty = relativePath.EndsWith("narrative_response.json", StringComparison.OrdinalIgnoreCase)
                ? "response"
                : "gm_thoughts_markdown";
            if (!doc.RootElement.TryGetProperty(requiredTextProperty, out var requiredText) ||
                requiredText.ValueKind != JsonValueKind.String ||
                string.IsNullOrWhiteSpace(requiredText.GetString()))
            {
                return false;
            }

            if (!doc.RootElement.TryGetProperty("timestamp", out var timestamp) ||
                timestamp.ValueKind != JsonValueKind.String ||
                !DateTimeOffset.TryParse(timestamp.GetString(), out var parsedTimestamp))
            {
                return false;
            }

            return parsedTimestamp.UtcDateTime >= requestTimestampUtc;
        }
        catch (JsonException)
        {
            return false;
        }
    }

    private IEnumerable<string> EnumerateRecoverableGmModifiedFiles(DateTime requestTimestampUtc)
    {
        var files = new SortedSet<string>(StringComparer.OrdinalIgnoreCase);
        foreach (var requiredFile in RecoverableGmOutputRequiredFiles)
        {
            if (_fs.FileExists(requiredFile))
                files.Add(requiredFile);
        }

        if (_fs.FileExists("output/interface_updates.json"))
            files.Add("output/interface_updates.json");

        var sessionRoot = _fs.ResolvePath("");
        foreach (var root in new[] { "output", "game_state", "lore" })
        {
            var absoluteRoot = _fs.ResolvePath(root);
            if (!Directory.Exists(absoluteRoot))
                continue;

            foreach (var absolutePath in Directory.GetFiles(absoluteRoot, "*.json", SearchOption.AllDirectories))
            {
                var relativePath = Path.GetRelativePath(sessionRoot, absolutePath).Replace('\\', '/');
                if (!IsRecoverableFilesModifiedPath(relativePath))
                    continue;

                if (File.GetLastWriteTimeUtc(absolutePath) >= requestTimestampUtc)
                    files.Add(relativePath);
            }
        }

        return files;
    }

    private static bool IsRecoverableFilesModifiedPath(string relativePath)
    {
        var normalized = relativePath.Replace('\\', '/');
        if (normalized.Contains(".rollback.", StringComparison.OrdinalIgnoreCase) ||
            normalized.EndsWith(".tmp", StringComparison.OrdinalIgnoreCase) ||
            normalized.StartsWith("game_state/control/pending_turn_snapshot", StringComparison.OrdinalIgnoreCase) ||
            string.Equals(normalized, "game_state/control/validation_repair_request.json", StringComparison.OrdinalIgnoreCase) ||
            string.Equals(normalized, "game_state/control/validation_repair_ready.json", StringComparison.OrdinalIgnoreCase) ||
            string.Equals(normalized, "game_state/control/terminal_protocol_failure_request.json", StringComparison.OrdinalIgnoreCase) ||
            string.Equals(normalized, "game_state/control/gm_bridge_status.json", StringComparison.OrdinalIgnoreCase))
        {
            return false;
        }

        return PendingTurnSnapshotAuthority.IsSafeRelativePath(normalized);
    }

    private async Task<ActiveTerminalOutcomeResolution> ResolveFinalActiveTerminalOutcomeAsync(
        ValidatedPendingTurnSnapshotContext? snapshotContext,
        RollbackSnapshot? rollbackSnapshot,
        TerminalSignalSnapshot terminalSignals)
    {
        var completionSignal = ParseReadySignalMetadata(
            terminalSignals.CompletionJson,
            "ready/turn_complete.json");
        var errorSignal = ParseReadySignalMetadata(
            terminalSignals.ErrorJson,
            "ready/turn_error.json");
        var concurrentResolution = await ResolveConcurrentActiveTerminalSignalsAsync(
            terminalSignals,
            completionSignal,
            errorSignal,
            snapshotContext,
            rollbackSnapshot);
        if (concurrentResolution.Failed)
            return new ActiveTerminalOutcomeResolution { Kind = "failure" };

        if (concurrentResolution.UseCompletion)
        {
            if (completionSignal != null &&
                snapshotContext != null &&
                IsMatchingReadySignal(completionSignal, snapshotContext) &&
                HasValidTerminalSignalContract("turn_complete", completionSignal))
            {
                return new ActiveTerminalOutcomeResolution
                {
                    Kind = "success",
                    Signal = completionSignal
                };
            }

            if (await HandleRejectedActiveReadySignalAsync("turn_complete", completionSignal, snapshotContext, rollbackSnapshot))
                return new ActiveTerminalOutcomeResolution { Kind = "failure" };
        }

        if (concurrentResolution.UseError)
        {
            if (errorSignal != null &&
                snapshotContext != null &&
                IsMatchingReadySignal(errorSignal, snapshotContext) &&
                HasValidTerminalSignalContract("turn_error", errorSignal))
            {
                var recoveredSignal = await TryRecoverGmOutputWithoutTerminalSignalAsync(errorSignal, snapshotContext);
                if (recoveredSignal != null &&
                    HasValidTerminalSignalContract("turn_complete", recoveredSignal))
                {
                    return new ActiveTerminalOutcomeResolution
                    {
                        Kind = "success",
                        Signal = recoveredSignal
                    };
                }

                return new ActiveTerminalOutcomeResolution
                {
                    Kind = "error",
                    Signal = errorSignal
                };
            }

            if (await HandleRejectedActiveReadySignalAsync("turn_error", errorSignal, snapshotContext, rollbackSnapshot))
                return new ActiveTerminalOutcomeResolution { Kind = "failure" };
        }

        await HandleMissingActiveTerminalOutcomeAsync(
            snapshotContext,
            rollbackSnapshot,
            concurrentResolution.UseCompletion,
            concurrentResolution.UseError);
        return new ActiveTerminalOutcomeResolution { Kind = "failure" };
    }

    private List<ValidationIssue> BuildRejectedActiveReadySignalIssues(string sourceLabel,
        ReadySignalMetadata? signal, ValidatedPendingTurnSnapshotContext? snapshotContext)
    {
        if (signal == null)
        {
            return
            [
                new ValidationIssue(
                    sourceLabel.Contains("error", StringComparison.OrdinalIgnoreCase)
                        ? "ready/turn_error.json"
                        : "ready/turn_complete.json",
                    IssueSeverity.Error,
                    "Terminal ready signal не читается как валидный JSON с полными metadata",
                    code: "invalid_terminal_ready_json",
                    section: "terminal_ready",
                    expected: "Valid JSON with sessionId/requestId/turnNumber",
                    actual: "missing, empty, unreadable or incomplete ready signal metadata",
                    repairHint: "Перезапиши terminal ready file валидным JSON, скопируй точные sessionId/requestId/turnNumber из текущего turn_request.json и записывай terminal signal самым последним шагом хода.")
            ];
        }

        if (!HasValidTerminalSignalContract(sourceLabel, signal))
        {
            var expectsError = sourceLabel.Contains("error", StringComparison.OrdinalIgnoreCase);
            var expectedStatus = expectsError ? "error" : "success";
            var issues = new List<ValidationIssue>();

            if (!string.Equals(signal.Status, expectedStatus, StringComparison.OrdinalIgnoreCase))
            {
                issues.Add(new ValidationIssue(
                    expectsError ? "ready/turn_error.json" : "ready/turn_complete.json",
                    IssueSeverity.Error,
                    "Terminal ready signal содержит неверный status для этого terminal channel",
                    code: "invalid_terminal_ready_status",
                    section: "terminal_ready",
                    expected: expectedStatus,
                    actual: string.IsNullOrWhiteSpace(signal.Status) ? "missing/empty" : signal.Status,
                    repairHint: expectsError
                        ? "Для ready/turn_error.json указывай status=\"error\" и заполняй error с описанием терминальной причины."
                        : "Для ready/turn_complete.json указывай status=\"success\" и не смешивай success signal с error channel."));
            }

            if (string.IsNullOrWhiteSpace(signal.Timestamp) || !DateTimeOffset.TryParse(signal.Timestamp, out _))
            {
                issues.Add(new ValidationIssue(
                    expectsError ? "ready/turn_error.json.timestamp" : "ready/turn_complete.json.timestamp",
                    IssueSeverity.Error,
                    "Terminal ready signal обязан содержать валидный ISO 8601 timestamp",
                    code: "terminal_ready_missing_or_invalid_timestamp",
                    section: "terminal_ready",
                    expected: "ISO 8601 timestamp",
                    actual: string.IsNullOrWhiteSpace(signal.Timestamp) ? "missing/empty" : signal.Timestamp,
                    repairHint: "Добавь в terminal ready signal поле timestamp в ISO 8601 формате и записывай ready-файл только после завершения всех остальных файлов хода."));
            }

            if (expectsError && string.IsNullOrWhiteSpace(signal.Error))
            {
                issues.Add(new ValidationIssue(
                    "ready/turn_error.json.error",
                    IssueSeverity.Error,
                    "ready/turn_error.json обязан содержать непустое поле error",
                    code: "terminal_error_missing_error_message",
                    section: "terminal_ready",
                    expected: "non-empty error string",
                    actual: "missing/empty",
                    repairHint: "Добавь в ready/turn_error.json краткое непустое описание терминальной ошибки в поле error."));
            }

            if (!expectsError && (!signal.HasFilesModified || !signal.FilesModifiedValid))
            {
                issues.Add(new ValidationIssue(
                    "ready/turn_complete.json.filesModified",
                    IssueSeverity.Error,
                    "ready/turn_complete.json обязан содержать filesModified как массив непустых путей",
                    code: "terminal_success_missing_or_invalid_files_modified",
                    section: "terminal_ready",
                    expected: "filesModified array of non-empty relative file paths",
                    actual: signal.HasFilesModified ? "invalid filesModified payload" : "missing",
                    repairHint: "Добавь в ready/turn_complete.json поле filesModified как массив относительных путей файлов, которые были записаны для этого хода."));
            }

            if (issues.Count > 0)
                return issues;
        }

        if (snapshotContext == null)
        {
            return
            [
                new ValidationIssue(
                    sourceLabel.Contains("error", StringComparison.OrdinalIgnoreCase)
                        ? "ready/turn_error.json"
                        : "ready/turn_complete.json",
                    IssueSeverity.Error,
                    "Terminal ready signal не удалось сопоставить активному pending turn context",
                    code: "missing_pending_context_for_terminal_ready",
                    section: "terminal_ready",
                    expected: "Existing pending turn snapshot manifest for the active request",
                    actual: $"ready signal sessionId={signal.SessionId}, requestId={signal.RequestId}, turnNumber={signal.TurnNumber}; pending snapshot manifest is missing",
                    repairHint: "Не пиши terminal ready signal вне активного correlated turn context, не переиспользуй stale ready files и не пытайся чинить terminal failure через validation_repair_ready.json.")
            ];
        }

        return
        [
            new ValidationIssue(
                sourceLabel.Contains("error", StringComparison.OrdinalIgnoreCase)
                    ? "ready/turn_error.json"
                    : "ready/turn_complete.json",
                IssueSeverity.Error,
                "Terminal ready signal содержит metadata, не совпадающие с активным ходом",
                code: "mismatched_terminal_ready_context",
                section: "terminal_ready",
                expected: $"sessionId={snapshotContext.SessionId}, requestId={snapshotContext.RequestId}, turnNumber={snapshotContext.TurnNumber}",
                actual: $"sessionId={signal.SessionId}, requestId={signal.RequestId}, turnNumber={signal.TurnNumber}",
                repairHint: "Копируй sessionId/requestId/turnNumber в terminal ready signal ровно из текущего turn_request.json и записывай ready-файл только после завершения всех остальных файлов хода.")
        ];
    }
}

