using System.Diagnostics;
using System.Text;
using System.Text.RegularExpressions;
using System.Text.Json;
using System.Text.Json.Nodes;
using System.Text.Json.Serialization;
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
    private async Task EnsureClientOwnedSystemFilesHealthyAsync()
    {
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

    private async Task<bool> ValidateCurrentGameStateOrShowErrorsAsync(string source,
        RollbackSnapshot? rollbackSnapshot = null,
        ProgressionControl? progressionControl = null,
        bool allowRepairLoop = false)
    {
        var repairAttempt = 0;

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
            }
            issues.AddRange(await _validator.ValidatePendingMemoryLegacyApplicationAsync());
            if (progressionControl != null)
                issues.AddRange(await _progressionSchedule.ValidateAcceptedTurnOutcomeAsync(progressionControl));
            var errors = PrioritizeValidationErrors(issues.Where(i => i.Severity == IssueSeverity.Error)).ToList();

            if (allowRepairLoop)
                errors = await FilterRestoredForbiddenRealmBaselineErrorsAsync(source, errors);

            if (errors.Count == 0)
            {
                await DeleteValidationRepairFilesAsync();
                if (progressionControl != null)
                    await _progressionSchedule.ApplyAcceptedTurnOutcomeAsync(progressionControl);
                return true;
            }

            if (allowRepairLoop && await TryAutoRollbackRealmSegregationViolationsAsync(source, errors))
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
            if (!await WaitForContractRepairAsync(source, errors, repairAttempt, rollbackSnapshot))
                return false;
        }
    }

    private async Task<bool> ValidateAcceptedTurnOutcomeWithRepairLoopAsync(
        string source,
        RollbackSnapshot? rollbackSnapshot,
        int expectedTurn,
        ProgressionControl? progressionControl)
    {
        var criticalRepairAttempt = 0;

        while (true)
        {
            await EnsureClientOwnedSystemFilesHealthyAsync();
            var rawIssues = await _criticalStateHealth.ValidateAcceptedTurnRawStateAsync();
            var rawErrors = PrioritizeValidationErrors(rawIssues.Where(i => i.Severity == IssueSeverity.Error)).ToList();
            if (rawErrors.Count > 0)
            {
                criticalRepairAttempt++;
                _logger.LogError(
                    "Critical accepted-turn raw state corruption after {Source}: {Count} errors",
                    source,
                    rawErrors.Count);

                if (!await WaitForContractRepairAsync(source, rawErrors, criticalRepairAttempt, rollbackSnapshot))
                    return false;

                continue;
            }

            if (!await RefreshAcceptedTurnCanonicalStateForValidationAsync(expectedTurn))
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

                if (!await WaitForContractRepairAsync(source, baselineErrors, criticalRepairAttempt, rollbackSnapshot))
                    return false;

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

                if (!await WaitForContractRepairAsync(source, canonicalErrors, criticalRepairAttempt, rollbackSnapshot))
                    return false;

                continue;
            }

            if (!await ValidateCurrentGameStateOrShowErrorsAsync(source, rollbackSnapshot, progressionControl, allowRepairLoop: true))
                return false;

            await CleanupAcceptedTurnCommandSurfacesAsync();
            await RefreshRuntimeStateAsync();
            return true;
        }
    }

    private async Task<bool> RefreshAcceptedTurnCanonicalStateForValidationAsync(int expectedTurn)
    {
        var snapshot = await LoadCanonicalBaselineSnapshotAsync(expectedTurn);
        if (snapshot == null)
            return false;

        await RefreshCanonicalStateAsync(snapshot);
        return true;
    }

    private async Task CleanupAcceptedTurnCommandSurfacesAsync()
    {
        await RemoveGuardianQuestProgressUpdatesCommandSurfaceAsync();
    }

    private async Task RemoveGuardianQuestProgressUpdatesCommandSurfaceAsync()
    {
        const string path = "game_state/meta/guardians.json";
        var json = await _fs.ReadFileAsync(path);
        if (string.IsNullOrWhiteSpace(json))
            return;

        try
        {
            if (JsonNode.Parse(json) is not JsonObject root ||
                !root.Remove(GuardianProjectState.QuestProgressUpdatesProperty))
            {
                return;
            }

            await _fs.WriteFileAtomicAsync(path, root.ToJsonString(JsonOpts));
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
        _inputSource.ReadKey(intercept: true);
    }

    private async Task<bool> WaitForContractRepairAsync(string source, List<ValidationIssue> errors,
        int attempt, RollbackSnapshot? rollbackSnapshot)
    {
        await WriteValidationRepairRequestAsync(source, errors, attempt);
        var rollbackAvailable = HasRollbackCapability(rollbackSnapshot);
        while (true)
        {
            using var cts = new CancellationTokenSource();
            var startTime = DateTime.UtcNow;

            var waitTask = Task.Run(async () =>
            {
                while (!cts.Token.IsCancellationRequested)
                {
                    if (_fs.FileExists(ValidationRepairReadyPath))
                        return true;
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
                    await RestorePreTurnBackup(rollbackSnapshot!);
                    CleanupBackup(rollbackSnapshot!);
                    AnsiConsole.MarkupLine("[yellow]⏹ Ремонтный цикл прерван. Состояние откатилось к последней стабильной версии.[/]");
                }
                else
                {
                    AnsiConsole.MarkupLine("[yellow]⏹ Ремонтный цикл прерван. Автоматический откат для этого режима недоступен; текущее состояние оставлено как есть.[/]");
                }

                await _progressionSchedule.DeleteTransientReportAsync();
                await DeleteValidationRepairFilesAsync();
                return false;
            }

            if (!result)
                continue;

            var readyJson = await _fs.ReadFileAsync(ValidationRepairReadyPath);
            var ready = await ReadValidationRepairReadyAsync();
            var pendingSnapshot = await ResolveActivePendingTurnSnapshotContextAsync();
            if (ready == null)
            {
                _logger.LogWarning("Отклонён validation_repair_ready: файл не читается как валидный JSON");
                await ReportRejectedRepairReadyAsync(
                    source,
                    errors,
                    attempt,
                    "invalid_repair_ready_json",
                    "Клиент отклонил validation_repair_ready.json: файл не читается как валидный JSON.",
                    "Valid JSON object with matching sessionId/requestId/turnNumber for the active repair cycle",
                    string.IsNullOrWhiteSpace(readyJson) ? "missing or empty file" : TruncateDiagnosticValue(readyJson),
                    BuildInvalidRepairReadyRepairHint(pendingSnapshot));
                await DeleteValidationRepairReadyAsync();
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

                await ReportRejectedRepairReadyAsync(
                    source,
                    errors,
                    attempt,
                    "mismatched_repair_ready_context",
                    "Клиент отклонил validation_repair_ready.json: metadata не совпадает с активным repair cycle.",
                    BuildExpectedRepairContext(pendingSnapshot),
                    BuildActualRepairContext(ready, pendingSnapshot),
                    BuildMismatchedRepairReadyRepairHint(pendingSnapshot));
                await DeleteValidationRepairReadyAsync();
                AnsiConsole.MarkupLine("[yellow]⚠ Клиент запросил новую попытку исправления. GM продолжает корректировать данные.[/]");
                await Task.Delay(500);
                continue;
            }

            await DeleteValidationRepairReadyAsync();
            return true;
        }
    }

    private async Task WriteValidationRepairRequestAsync(string source, List<ValidationIssue> errors, int attempt)
    {
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

        await _fs.WriteFileAtomicAsync(ValidationRepairRequestPath, JsonSerializer.Serialize(request, JsonOpts));
        await RunWorkerValidationRepairIfAvailableAsync(prioritizedErrors, requestMetadata, request.DetectedAtUtc, attempt);
    }

    private static List<ValidationRepairHarnessPacket> BuildValidationRepairHarnessPackets(
        IReadOnlyList<ValidationIssue> errors,
        IReadOnlyCollection<string>? guardianActorNameHints = null)
    {
        var packets = new List<ValidationRepairHarnessPacket>();
        var guardianScopeErrors = errors.Where(IsGuardianScopeRepairIssue).ToList();
        var guardianScopeActorNames = CollectRepairActorNames(guardianScopeErrors, guardianActorNameHints);
        var actorReasoningSubpointErrors = errors
            .Where(IsActorReasoningSubpointRepairIssue)
            .Where(issue => !guardianScopeActorNames.Contains(NormalizeRepairActorName(issue.Actor)))
            .ToList();
        var factionIdentityErrors = errors.Where(IsFactionIdentityRepairIssue).ToList();

        if (guardianScopeErrors.Count > 0)
            packets.Add(BuildGuardianScopeRepairPacket(guardianScopeErrors, guardianActorNameHints));

        if (actorReasoningSubpointErrors.Count > 0)
            packets.Add(BuildActorReasoningSubpointRepairPacket(actorReasoningSubpointErrors));

        if (factionIdentityErrors.Count > 0)
            packets.Add(BuildFactionIdentityRepairPacket(factionIdentityErrors));

        return packets;
    }

    private static bool IsGuardianScopeRepairIssue(ValidationIssue issue)
    {
        return string.Equals(issue.Code, "guardian_materialized_state_outside_authority", StringComparison.OrdinalIgnoreCase) ||
               string.Equals(issue.Code, "guardian_project_materialized_state_outside_authority", StringComparison.OrdinalIgnoreCase) ||
               string.Equals(issue.Code, "structured_guardian_update_out_of_scope", StringComparison.OrdinalIgnoreCase) ||
               string.Equals(issue.Code, "active_guardian_missing_from_scope", StringComparison.OrdinalIgnoreCase);
    }

    private static bool IsActorReasoningSubpointRepairIssue(ValidationIssue issue)
    {
        return IsGuardianReasoningSection(issue.Section) &&
               (string.Equals(issue.Code, "missing_actor_block", StringComparison.OrdinalIgnoreCase) ||
                string.Equals(issue.Code, "missing_actor_situation", StringComparison.OrdinalIgnoreCase) ||
                string.Equals(issue.Code, "missing_actor_thoughts", StringComparison.OrdinalIgnoreCase) ||
                string.Equals(issue.Code, "missing_actor_actions", StringComparison.OrdinalIgnoreCase));
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
                "Inside every listed actor block, include separate bullet subpoints with these exact client-recognized labels: - Ситуация:, - Мысли:, and - Действия:.",
                "Keep the actor heading shape as ### <actor name>. Do not merge the three subpoints into one paragraph or one bullet.",
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
            Title = "Faction identity repair",
            TargetFiles = targetFiles,
            Steps = new List<string>
            {
                "Use game_state/control/pending_turn_snapshot/game_state/factions/faction_core.json as the authority for whether the faction was already permanent before this turn or was still a same-turn temporary faction.",
                "If the matching faction in pending_turn_snapshot has factionId = null plus a non-empty initialId, restore the current object to the same temporary identity shape: factionId = null, the exact initialId, and isNewFaction = true.",
                "If the matching faction in pending_turn_snapshot has a non-empty factionId, use that exact permanent factionId and remove initialId/isNewFaction from the existing faction object.",
                "After restoring the identity shape, preserve the intended turn content updates around the faction instead of replacing the whole file with a minimal skeleton.",
                "After file repairs are complete, call Complete-BoeValidationRepair as the last action, or create game_state/control/validation_repair_ready.json with exact sessionId/requestId/turnNumber from the current validation_repair_request.json."
            },
            DoNotDo = new List<string>
            {
                "Do not invent a permanent factionId from the faction name.",
                "Do not convert a same-turn temporary faction into an existing permanent faction just because validation mentions existing faction wording.",
                "Do not delete unrelated faction ranks, resources, projects, chronicles, or reputation details to silence identity validation.",
                "Do not read implementation code such as BookOfEternityClient/**/*.cs to infer repair rules; use this repair packet, the validation request, and session snapshot/control files."
            }
        };
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

    private static string NormalizeRepairTargetPath(string path)
    {
        var normalized = path.Replace('\\', '/');
        if (normalized.StartsWith("game_state/meta/guardians.json", StringComparison.OrdinalIgnoreCase))
            return "game_state/meta/guardians.json";
        if (normalized.StartsWith("game_state/meta/guardian_projects.json", StringComparison.OrdinalIgnoreCase))
            return "game_state/meta/guardian_projects.json";
        if (normalized.StartsWith("output/debug_logs.json", StringComparison.OrdinalIgnoreCase))
            return "output/debug_logs.json";
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
            builder.AppendLine("- Ситуация: кратко опиши текущую ситуацию Хранителя в этом ходе.");
            builder.AppendLine("- Мысли: кратко опиши мотивы/оценку Хранителя.");
            builder.AppendLine("- Действия: кратко опиши, что Хранитель делает или меняет в состоянии.");
            builder.AppendLine();
        }

        return builder.ToString().TrimEnd();
    }

    private static string BuildActorReasoningSubpointDebugLogTemplate(IReadOnlyList<string> actorNames)
    {
        var builder = new StringBuilder();
        builder.AppendLine("## Размышления акторов");
        foreach (var actor in actorNames)
        {
            builder.AppendLine($"### {actor}");
            builder.AppendLine("- Ситуация: кратко опиши, в каком положении актор находится в этом ходе.");
            builder.AppendLine("- Мысли: кратко опиши мотивы, оценку или внутреннюю реакцию актора.");
            builder.AppendLine("- Действия: кратко опиши, что актор делает, решает или меняет в состоянии.");
            builder.AppendLine();
        }

        return builder.ToString().TrimEnd();
    }

    private async Task RunWorkerValidationRepairIfAvailableAsync(
        IReadOnlyList<ValidationIssue> prioritizedErrors,
        (string SessionId, string RequestId, int TurnNumber) requestMetadata,
        string createdAtUtc,
        int attempt)
    {
        try
        {
            var audit = new GmWorkerAuditLog(_fs);
            var delegator = new GmWorkerValidationRepairDelegator(
                _fs,
                new GmWorkerBridgePool(_fs, new GmWorkerProposalStore(_fs), audit),
                new GmWorkerApplyGate(
                    _fs,
                    async () => (IReadOnlyList<ValidationIssue>)await _validator.ValidateGameStateAsync(),
                    audit),
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
                attempt);

            if (result.Outcome is not GmWorkerValidationRepairOutcome.SkippedNoWorker and
                not GmWorkerValidationRepairOutcome.Applied)
            {
                _logger.LogWarning(
                    "GM worker validation repair ended with {Outcome}: {Reason}. Legacy repair loop remains active.",
                    result.Outcome,
                    result.FallbackReason);
            }
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Failed to run GM worker validation repair. Legacy repair loop remains active.");
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

    private async Task ReportRejectedRepairReadyAsync(string source, List<ValidationIssue> baseErrors, int attempt,
        string code, string message, string expected, string actual, string repairHint)
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

        await WriteValidationRepairRequestAsync(source, reportErrors, attempt);
    }

    private async Task<ValidationRepairReady?> ReadValidationRepairReadyAsync()
    {
        var json = await _fs.ReadFileAsync(ValidationRepairReadyPath);
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
        await DeleteValidationRepairReadyAsync();
        if (_fs.FileExists(ValidationRepairRequestPath))
            _fs.DeleteFile(ValidationRepairRequestPath);
    }

    private Task DeleteTerminalProtocolFailureRequestAsync()
    {
        if (_fs.FileExists(TerminalProtocolFailureRequestPath))
            _fs.DeleteFile(TerminalProtocolFailureRequestPath);
        return Task.CompletedTask;
    }

    private Task DeleteValidationRepairReadyAsync()
    {
        if (_fs.FileExists(ValidationRepairReadyPath))
            _fs.DeleteFile(ValidationRepairReadyPath);
        return Task.CompletedTask;
    }

    private async Task ShowTurnErrorMessageAsync(string readyErrorPath)
    {
        var errorJson = await _fs.ReadFileAsync(readyErrorPath);
        if (errorJson == null)
        {
            AnsiConsole.MarkupLine("[red]❌ Ошибка ожидания ответа GM[/]");
            return;
        }

        try
        {
            using var errorDoc = JsonDocument.Parse(errorJson);
            var errorMsg = errorDoc.RootElement.TryGetProperty("error", out var e) ? e.GetString() : errorJson;
            AnsiConsole.MarkupLine($"[red]❌ Ошибка GM: {GameInterface.EscapeMarkup(errorMsg ?? errorJson)}[/]");
        }
        catch
        {
            AnsiConsole.MarkupLine($"[red]❌ Ошибка GM: {GameInterface.EscapeMarkup(errorJson)}[/]");
        }
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

    private async Task HandleMissingActiveTerminalOutcomeAsync(ValidatedPendingTurnSnapshotContext? snapshotContext,
        RollbackSnapshot? rollbackSnapshot)
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
                actual: BuildMissingActiveTerminalOutcomeActual(snapshotContext),
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

    private async Task<bool> ResolveConcurrentActiveTerminalSignalsAsync(ValidatedPendingTurnSnapshotContext? snapshotContext,
        RollbackSnapshot? rollbackSnapshot)
    {
        if (!_fs.FileExists("ready/turn_complete.json") || !_fs.FileExists("ready/turn_error.json"))
            return false;

        if (snapshotContext == null)
            return false;

        var completionSignal = await ReadReadySignalMetadataAsync("ready/turn_complete.json");
        var errorSignal = await ReadReadySignalMetadataAsync("ready/turn_error.json");
        if (completionSignal != null &&
            IsMatchingReadySignal(completionSignal, snapshotContext) &&
            !HasValidTerminalSignalContract("turn_complete", completionSignal))
        {
            return await HandleRejectedActiveReadySignalAsync("turn_complete", completionSignal, snapshotContext, rollbackSnapshot);
        }

        if (errorSignal != null &&
            IsMatchingReadySignal(errorSignal, snapshotContext) &&
            !HasValidTerminalSignalContract("turn_error", errorSignal))
        {
            return await HandleRejectedActiveReadySignalAsync("turn_error", errorSignal, snapshotContext, rollbackSnapshot);
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
            return false;
        }

        if (errorMatches && !completionMatches)
        {
            _logger.LogWarning("Удаляется competing terminal success signal во время active wait; error signal остаётся authoritative.");
            _fs.DeleteFile("ready/turn_complete.json");
            return false;
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
        return true;
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

    private string BuildMissingActiveTerminalOutcomeActual(ValidatedPendingTurnSnapshotContext? snapshotContext)
    {
        var turnCompleteExists = _fs.FileExists("ready/turn_complete.json");
        var turnErrorExists = _fs.FileExists("ready/turn_error.json");
        var manifestDescription = snapshotContext == null
            ? "pendingSnapshot=missing"
            : $"pendingSnapshot=sessionId={snapshotContext.SessionId}, requestId={snapshotContext.RequestId}, turnNumber={snapshotContext.TurnNumber}";
        return $"turn_complete_exists={turnCompleteExists}; turn_error_exists={turnErrorExists}; {manifestDescription}";
    }

    private async Task<ActiveTerminalOutcomeResolution> ResolveFinalActiveTerminalOutcomeAsync(
        ValidatedPendingTurnSnapshotContext? snapshotContext,
        RollbackSnapshot? rollbackSnapshot)
    {
        if (await ResolveConcurrentActiveTerminalSignalsAsync(snapshotContext, rollbackSnapshot))
            return new ActiveTerminalOutcomeResolution { Kind = "failure" };

        if (_fs.FileExists("ready/turn_complete.json"))
        {
            var completionSignal = await ReadReadySignalMetadataAsync("ready/turn_complete.json");
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

        if (_fs.FileExists("ready/turn_error.json"))
        {
            var errorSignal = await ReadReadySignalMetadataAsync("ready/turn_error.json");
            if (errorSignal != null &&
                snapshotContext != null &&
                IsMatchingReadySignal(errorSignal, snapshotContext) &&
                HasValidTerminalSignalContract("turn_error", errorSignal))
            {
                return new ActiveTerminalOutcomeResolution
                {
                    Kind = "error",
                    Signal = errorSignal
                };
            }

            if (await HandleRejectedActiveReadySignalAsync("turn_error", errorSignal, snapshotContext, rollbackSnapshot))
                return new ActiveTerminalOutcomeResolution { Kind = "failure" };
        }

        await HandleMissingActiveTerminalOutcomeAsync(snapshotContext, rollbackSnapshot);
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

