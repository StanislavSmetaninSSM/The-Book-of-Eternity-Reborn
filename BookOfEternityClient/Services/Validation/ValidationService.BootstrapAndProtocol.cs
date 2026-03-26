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
    private async Task ValidateJsonIntegrity(List<ValidationIssue> issues)
    {
        var sessionRoot = _fs.ResolvePath("");
        foreach (var rootDirName in new[] { "game_state", "lore" })
        {
            var absoluteRoot = _fs.ResolvePath(rootDirName);
            if (!Directory.Exists(absoluteRoot))
                continue;

            foreach (var file in Directory.GetFiles(absoluteRoot, "*.json", SearchOption.AllDirectories))
            {
                var relativePath = Path.GetRelativePath(sessionRoot, file).Replace('\\', '/');
                if (IsClientOwnedSurfaceValidationPath(relativePath) ||
                    string.Equals(relativePath, "game_state/control/validation_repair_ready.json", StringComparison.OrdinalIgnoreCase) ||
                    relativePath.Contains(".rollback.", StringComparison.OrdinalIgnoreCase))
                {
                    continue;
                }

                try
                {
                    var content = await File.ReadAllTextAsync(file);
                    if (string.IsNullOrWhiteSpace(content))
                        continue;

                    JsonDocument.Parse(content).Dispose();
                }
                catch (JsonException ex)
                {
                    issues.Add(new ValidationIssue(
                        relativePath,
                        IssueSeverity.Error,
                        $"Невалидный JSON: {ex.Message}",
                        code: "invalid_json_file",
                        section: relativePath.StartsWith("lore/", StringComparison.OrdinalIgnoreCase) ? "LoreJson" : "StateJson",
                        repairHint: "Исправь файл до валидного JSON, не меняя его доменный контракт."));
                }
            }
        }
    }


    private void ValidateRequiredFiles(List<ValidationIssue> issues)
    {
        var requiredWhenInGame = new[]
        {
            "game_state/meta/soul_state.json"
        };

        foreach (var file in requiredWhenInGame)
        {
            if (!_fs.FileExists(file))
            {
                issues.Add(new ValidationIssue(
                    file,
                    IssueSeverity.Error,
                    $"Обязательный файл не найден: {file}",
                    code: "required_state_file_missing",
                    section: "RequiredFiles",
                    expected: "required file exists",
                    actual: "missing",
                    repairHint: $"Восстанови обязательный canonical state file {file} перед продолжением хода."));
            }
        }
    }


    private async Task ValidateRequiredFields(List<ValidationIssue> issues)
    {
        // Soul state must have soulName and currentRealm
        await ValidateFileFields("game_state/meta/soul_state.json",
            new[] { "soulName", "currentRealm" }, issues);

        // Player status must have health/energy/poise when in mortal life
        var soulJson = await _fs.ReadFileAsync("game_state/meta/soul_state.json");
        if (soulJson != null)
        {
            try
            {
                using var doc = JsonDocument.Parse(soulJson);
                if (!doc.RootElement.TryGetProperty("currentRealm", out var r) ||
                    r.ValueKind != JsonValueKind.String ||
                    string.IsNullOrWhiteSpace(r.GetString()))
                {
                    return;
                }

                var realm = r.GetString()!;
                if (!IsChaosSeaRealm(realm))
                {
                    if (!_fs.FileExists("game_state/core/player_status.json"))
                    {
                        issues.Add(new ValidationIssue(
                            "game_state/core/player_status.json",
                            IssueSeverity.Error,
                            "В Mortal World обязателен game_state/core/player_status.json",
                            code: "missing_player_status_in_mortal_world",
                            section: "RequiredFields",
                            expected: "game_state/core/player_status.json exists in Mortal World",
                            actual: "missing",
                            repairHint: "В Mortal World сохраняй canonical game_state/core/player_status.json с healthPercentage, energyPercentage, poisePercentage и money."));
                        return;
                    }

                    await ValidateFileFields("game_state/core/player_status.json",
                        new[] { "healthPercentage", "energyPercentage", "poisePercentage", "money" }, issues);
                }
            }
            catch { }
        }
    }


    private async Task ValidateLoreBootstrapRequiredFilesAsync(List<ValidationIssue> issues)
    {
        var manifest = await LoadValidationPendingTurnSnapshotManifestAsync();
        var readyTurnCompleteExists = _fs.FileExists("ready/turn_complete.json");
        var readyTurnErrorExists = _fs.FileExists("ready/turn_error.json");
        var soulJson = await _fs.ReadFileAsync("game_state/meta/soul_state.json");
        if (string.IsNullOrWhiteSpace(soulJson))
            return;

        try
        {
            using var doc = JsonDocument.Parse(soulJson);
            var root = doc.RootElement;
            if (!root.TryGetProperty("currentRealm", out var realmEl) ||
                realmEl.ValueKind != JsonValueKind.String ||
                string.IsNullOrWhiteSpace(realmEl.GetString()))
            {
                return;
            }

            var currentRealm = realmEl.GetString()!;
            var currentIncarnation = root.TryGetProperty("currentIncarnation", out var incarnationEl) &&
                                     incarnationEl.ValueKind == JsonValueKind.Number &&
                                     incarnationEl.TryGetInt32(out var parsedIncarnation)
                ? parsedIncarnation
                : 0;
            var livesHistoryCount = root.TryGetProperty("livesHistory", out var livesHistory) &&
                                    livesHistory.ValueKind == JsonValueKind.Array
                ? livesHistory.GetArrayLength()
                : 0;
            var hasAnyTurns = await ChatLogHasTurnsAsync();
            var isInitialChaosSeaBootstrapTurn = string.Equals(manifest?.SourceLabel, "первого описания Моря Хаоса", StringComparison.OrdinalIgnoreCase);

            if (!hasAnyTurns && currentIncarnation <= 0 && !isInitialChaosSeaBootstrapTurn)
                return;

            if (!readyTurnCompleteExists &&
                !readyTurnErrorExists &&
                IsLoreBootstrapPendingTransitionSource(manifest?.SourceLabel))
            {
                return;
            }

            var requiredFiles = new HashSet<string>(StringComparer.OrdinalIgnoreCase)
            {
                "game_state/meta/achievements.json",
                "lore/codex_entries.json"
            };
            if (isInitialChaosSeaBootstrapTurn)
                requiredFiles.Add("game_state/meta/character_chronicle.json");

            if (IsChaosSeaRealm(currentRealm))
            {
                if (string.Equals(manifest?.SourceLabel, "первого описания Моря Хаоса", StringComparison.OrdinalIgnoreCase) ||
                    currentIncarnation > 0 ||
                    livesHistoryCount > 0)
                {
                    requiredFiles.Add("lore/chaos_sea/cosmology.json");
                    requiredFiles.Add("lore/chaos_sea/soul_system_lore.json");
                    requiredFiles.Add("lore/chaos_sea/guardians_lore.json");
                }
                if (livesHistoryCount > 0 || currentIncarnation > 0)
                    requiredFiles.Add("lore/chaos_sea/player_chronicle.json");
            }
            else
            {
                requiredFiles.Add("lore/current_world/world_setting.json");
                requiredFiles.Add("lore/current_world/geography.json");
                requiredFiles.Add("lore/current_world/history.json");
                requiredFiles.Add("lore/current_world/cultures.json");
                requiredFiles.Add("lore/current_world/threats.json");
            }

            foreach (var filePath in requiredFiles)
            {
                if (_fs.FileExists(filePath))
                    continue;

                issues.Add(new ValidationIssue(
                    filePath,
                    IssueSeverity.Error,
                    $"Отсутствует обязательный lore/meta bootstrap файл: {filePath}",
                    code: "missing_lore_bootstrap_file",
                    section: "LoreBootstrap",
                    repairHint: "Создай обязательные lore/codex/achievement файлы для текущего realm и стадии игры согласно CLI Lore Initialization Protocol."));
            }

            if (isInitialChaosSeaBootstrapTurn)
            {
                await ValidateJsonFileHasMeaningfulContentAsync("game_state/meta/character_chronicle.json", issues, "Turn 1 Character Chronicle bootstrap");
                await ValidateJsonFileHasMeaningfulContentAsync("lore/chaos_sea/cosmology.json", issues, "Turn 1 Chaos Sea bootstrap");
                await ValidateJsonFileHasMeaningfulContentAsync("lore/chaos_sea/soul_system_lore.json", issues, "Turn 1 Chaos Sea bootstrap");
                await ValidateJsonFileHasMeaningfulContentAsync("lore/chaos_sea/guardians_lore.json", issues, "Turn 1 Chaos Sea bootstrap");
                await ValidateCodexBootstrapEntriesAsync(issues, "Turn 1 Chaos Sea bootstrap");

                var staleStoryFiles = EnumerateStoryContinuityFiles().ToList();
                if (staleStoryFiles.Count > 0)
                {
                    issues.Add(new ValidationIssue(
                        "stories",
                        IssueSeverity.Warning,
                        "На стартовом bootstrap новой игры обнаружены старые stories/*.jsonl continuity files",
                        code: "bootstrap_stale_story_continuity_detected",
                        section: "LoreBootstrap",
                        expected: "no stale stories/*.jsonl continuity on a fresh new game",
                        actual: string.Join(", ", staleStoryFiles),
                        repairHint: "Это client-owned continuity surface. Для действительно новой игры очисти старые stories/*.jsonl, чтобы GM не опирался на чужую историю.",
                        category: IssueCategory.ClientOwnedSurface));
                }
            }

            if (IsLoreBootstrapPendingTransitionSource(manifest?.SourceLabel))
            {
                var mortalBootstrapFiles = new[]
                {
                    "lore/current_world/world_setting.json",
                    "lore/current_world/geography.json",
                    "lore/current_world/history.json",
                    "lore/current_world/cultures.json",
                    "lore/current_world/threats.json"
                };

                await ValidateJsonFileHasMeaningfulContentAsync("lore/current_world/world_setting.json", issues, "Mortal World bootstrap");
                await ValidateJsonFileHasMeaningfulContentAsync("lore/current_world/geography.json", issues, "Mortal World bootstrap");
                await ValidateJsonFileHasMeaningfulContentAsync("lore/current_world/history.json", issues, "Mortal World bootstrap");
                await ValidateJsonFileHasMeaningfulContentAsync("lore/current_world/cultures.json", issues, "Mortal World bootstrap");
                await ValidateJsonFileHasMeaningfulContentAsync("lore/current_world/threats.json", issues, "Mortal World bootstrap");
                await ValidateCodexBootstrapEntriesAsync(issues, "Mortal World bootstrap", requiredSourcePrefix: "current_world/");

                if (manifest != null)
                {
                    foreach (var bootstrapFile in mortalBootstrapFiles)
                    {
                        if (await DidFileChangeAgainstManifestAsync(manifest, bootstrapFile))
                            continue;

                        issues.Add(new ValidationIssue(
                            bootstrapFile,
                            IssueSeverity.Error,
                            "Новый Mortal World bootstrap не должен переиспользовать pre-turn current_world lore файл без изменений",
                            code: "mortal_bootstrap_reused_previous_world_lore",
                            section: "LoreBootstrap",
                            repairHint: "Сгенерируй свежий current_world lore для новой жизни вместо простого сохранения старого файла из предыдущего мира."));
                    }
                }
            }
        }
        catch (JsonException ex)
        {
            issues.Add(new ValidationIssue(
                "game_state/meta/soul_state.json",
                IssueSeverity.Error,
                $"Не удалось определить bootstrap requirements из soul_state.json: {ex.Message}",
                code: "lore_bootstrap_state_unreadable",
                section: "LoreBootstrap"));
        }
    }


    private async Task ValidateLifeEvaluationRewardCycleAsync(List<ValidationIssue> issues)
    {
        var manifest = await LoadValidationPendingTurnSnapshotManifestAsync();
        if (manifest == null || !LifeEvaluationRewardAnalyzer.IsLifeEvaluationSourceLabel(manifest.SourceLabel))
            return;

        const string soulStatePath = "game_state/meta/soul_state.json";
        var preSoulStateJson = await ReadPreTurnTrackedFileAsync(soulStatePath);
        var postSoulStateJson = await _fs.ReadFileAsync(soulStatePath);

        if (!LifeEvaluationRewardAnalyzer.TryComputeDelta(preSoulStateJson, postSoulStateJson, out var delta, out var error) ||
            delta == null)
        {
            issues.Add(new ValidationIssue(
                soulStatePath,
                IssueSeverity.Error,
                $"Не удалось вычислить reward delta для life evaluation: {error ?? "unknown error"}",
                code: "life_evaluation_reward_delta_unreadable",
                section: "LifeEvaluation",
                expected: "валидный pre/post diff по soul_state.json",
                repairHint: "Убедись, что pending turn snapshot сохранил pre-turn soul_state.json, а текущий soul_state.json корректно обновлён после оценки жизни."));
            return;
        }

        if (delta.InkFeathersEarned < 10)
        {
            issues.Add(new ValidationIssue(
                soulStatePath,
                IssueSeverity.Error,
                "Life evaluation обязан начислить минимум 10 Чернильных Перьев.",
                code: "life_evaluation_missing_ink_feather_reward",
                section: "LifeEvaluation",
                expected: "ink feathers delta >= 10",
                actual: delta.InkFeathersEarned.ToString(),
                repairHint: "После завершения смертной жизни увеличь soul_state.inkFeathers.current минимум на 10 и отрази survival minimum / основной расчёт награды в GM response."));
        }

        if (delta.NewRelics.Count == 0)
        {
            issues.Add(new ValidationIssue(
                soulStatePath,
                IssueSeverity.Error,
                "Life evaluation обязан добавить хотя бы одну новую Реликвию Души.",
                code: "life_evaluation_missing_soul_relic_reward",
                section: "LifeEvaluation",
                expected: "at least one new soulRelic.relicId",
                actual: "0 new relics",
                repairHint: "Создай минимум одну новую Soul Relic с новым relicId в soulRelics.stored[] или soulRelics.equipped[]."));
        }

        const string playerChroniclePath = "lore/chaos_sea/player_chronicle.json";
        var preChronicleJson = await ReadPreTurnTrackedFileAsync(playerChroniclePath);
        var postChronicleJson = await _fs.ReadFileAsync(playerChroniclePath);
        if (!DidChronicleGainMeaningfulSummaryEntry(preChronicleJson, postChronicleJson))
        {
            issues.Add(new ValidationIssue(
                playerChroniclePath,
                IssueSeverity.Error,
                "Life evaluation обязан добавить life summary в lore/chaos_sea/player_chronicle.json.",
                code: "life_evaluation_missing_player_chronicle_update",
                section: "LifeEvaluation",
                expected: "player_chronicle appends a new non-empty summary entry for the completed life",
                repairHint: "Добавь в конец player_chronicle.json новую непустую summary entry с итогами завершённой жизни и не перезаписывай предыдущие записи."));
        }
    }


    private async Task ValidateNoLifeEvaluationRewardsOnTriggerTurnAsync(List<ValidationIssue> issues)
    {
        var manifest = await LoadValidationPendingTurnSnapshotManifestAsync();
        if (manifest == null || LifeEvaluationRewardAnalyzer.IsLifeEvaluationSourceLabel(manifest.SourceLabel))
            return;

        const string lifeTransitionsPath = "game_state/control/life_transitions.json";
        var lifeTransitionsJson = await _fs.ReadFileAsync(lifeTransitionsPath);
        if (string.IsNullOrWhiteSpace(lifeTransitionsJson))
            return;

        try
        {
            using var doc = JsonDocument.Parse(lifeTransitionsJson);
            if (!TryReadLifeTransitionControlPayload(doc.RootElement, out _, out _))
                return;
        }
        catch (JsonException)
        {
            return;
        }

        const string soulStatePath = "game_state/meta/soul_state.json";
        var preSoulStateJson = await ReadPreTurnTrackedFileAsync(soulStatePath);
        var postSoulStateJson = await _fs.ReadFileAsync(soulStatePath);
        if (LifeEvaluationRewardAnalyzer.TryComputeDelta(preSoulStateJson, postSoulStateJson, out var delta, out _) &&
            delta != null)
        {
            if (delta.InkFeathersEarned > 0)
            {
                issues.Add(new ValidationIssue(
                    soulStatePath,
                    IssueSeverity.Error,
                    "TriggerLifeEnd turn не должен начислять финальную Ink Feather награду; она принадлежит отдельному Life Evaluation turn.",
                    code: "life_trigger_turn_awarded_ink_feathers",
                    section: "LifeEvaluation",
                    expected: "ink feathers delta = 0 on TriggerLifeEnd turn",
                    actual: delta.InkFeathersEarned.ToString(),
                    repairHint: "Оставь TriggerLifeEnd turn только для запуска lifecycle. Финальные Ink Feather rewards начисляй на следующем отдельном Life Evaluation turn."));
            }

            if (delta.NewRelics.Count > 0)
            {
                issues.Add(new ValidationIssue(
                    soulStatePath,
                    IssueSeverity.Error,
                    "TriggerLifeEnd turn не должен выдавать Soul Relic reward; она принадлежит отдельному Life Evaluation turn.",
                    code: "life_trigger_turn_awarded_soul_relic",
                    section: "LifeEvaluation",
                    expected: "0 new soul relics on TriggerLifeEnd turn",
                    actual: delta.NewRelics.Count.ToString(),
                    repairHint: "Не смешивай TriggerLifeEnd с финальной наградой. Новые Soul Relics создавай только на отдельном Life Evaluation turn."));
            }
        }

        const string playerChroniclePath = "lore/chaos_sea/player_chronicle.json";
        var preChronicleJson = await ReadPreTurnTrackedFileAsync(playerChroniclePath);
        var postChronicleJson = await _fs.ReadFileAsync(playerChroniclePath);
        if (DidChronicleGainEntries(preChronicleJson, postChronicleJson))
        {
            issues.Add(new ValidationIssue(
                playerChroniclePath,
                IssueSeverity.Error,
                "TriggerLifeEnd turn не должен писать финальный life summary в player_chronicle; это отдельный Life Evaluation outcome.",
                code: "life_trigger_turn_updated_player_chronicle",
                section: "LifeEvaluation",
                expected: "no new player_chronicle entry on TriggerLifeEnd turn",
                actual: "player_chronicle entry added",
                repairHint: "Оставь player_chronicle update на отдельный Life Evaluation turn после принятого TriggerLifeEnd."));
        }
    }


    private async Task ValidateClientOwnedControlFilesAsync(List<ValidationIssue> issues)
    {
        var manifest = await LoadValidationPendingTurnSnapshotManifestAsync();
        await ValidateValidationRepairReadyAsync(issues);
        await ValidateArchiveCandidateManifestAsync(issues);
        await ValidatePendingAbodeOfferingContextAsync(issues);
        await ValidatePendingGuardianTradeRequestContextAsync(issues);
        await ValidatePendingArchiveConsultationRequestContextAsync(issues);
        await ValidatePendingArchiveProjectFuelRequestContextAsync(issues);
        if (_fs.FileExists("ready/turn_complete.json") ||
            _fs.FileExists("ready/turn_error.json"))
        {
            await ValidatePendingAbodeOfferingResolutionAsync(issues);
            await ValidatePendingGuardianTradeRequestResolutionAsync(issues);
            await ValidatePendingArchiveConsultationResolutionAsync(issues);
            await ValidatePendingArchiveProjectFuelResolutionAsync(issues);
        }

        if (manifest == null)
            return;

        if (!string.IsNullOrWhiteSpace(manifest.ManifestPayloadHash))
        {
            var actualHash = ComputeManifestPayloadHash(manifest);
            if (!string.Equals(actualHash, manifest.ManifestPayloadHash, StringComparison.OrdinalIgnoreCase))
            {
                issues.Add(new ValidationIssue(
                    PendingTurnSnapshotManifestPath,
                    IssueSeverity.Error,
                    "pending_turn_snapshot.json был изменён после создания клиентом и больше не совпадает с исходным snapshot manifest.",
                    code: "client_owned_pending_snapshot_manifest_modified",
                    section: "PendingTurnSnapshot",
                    expected: manifest.ManifestPayloadHash,
                    actual: actualHash,
                    repairHint: "Не изменяй pending_turn_snapshot.json в GM turn; это client-owned transient control file."));
            }
        }

        foreach (var (originalPath, snapshotPath) in manifest.Files)
        {
            if (!manifest.SnapshotFileHashes.TryGetValue(originalPath, out var expectedHash) || string.IsNullOrWhiteSpace(expectedHash))
                continue;

            var currentSnapshotContent = await _fs.ReadFileAsync(snapshotPath);
            var actualHash = currentSnapshotContent == null ? string.Empty : ComputeSha256(currentSnapshotContent);
            if (!string.Equals(actualHash, expectedHash, StringComparison.OrdinalIgnoreCase))
            {
                issues.Add(new ValidationIssue(
                    snapshotPath,
                    IssueSeverity.Error,
                    $"Файл client-owned snapshot '{snapshotPath}' был изменён после создания pending turn snapshot.",
                    code: "client_owned_pending_snapshot_file_modified",
                    section: "PendingTurnSnapshot",
                    expected: expectedHash,
                    actual: actualHash,
                    repairHint: "Не изменяй содержимое game_state/control/pending_turn_snapshot/* в GM turn."));
            }
        }

        foreach (var (baselinePath, expectedHash) in manifest.ClientOwnedValidationHashes)
        {
            // validation_repair_request.json and terminal_protocol_failure_request.json are
            // runtime-authored protocol surfaces. They can legitimately change while the client
            // advances the repair/protocol loop, so blaming the GM for their hash drift creates a
            // self-sustaining deadlock. Only history surfaces remain in the GM-blame hash pass.
            if (!IsClientOwnedHistoryValidationPath(baselinePath))
                continue;

            var currentContent = await _fs.ReadFileAsync(baselinePath);
            var actualHash = currentContent == null ? string.Empty : ComputeSha256(currentContent);
            if (string.Equals(actualHash, expectedHash, StringComparison.OrdinalIgnoreCase))
                continue;

            issues.Add(new ValidationIssue(
                baselinePath,
                IssueSeverity.Error,
                $"{Path.GetFileName(baselinePath)} является client-maintained history surface и не должен изменяться GM-ходом.",
                code: "client_owned_history_surface_modified",
                section: "ClientHistory",
                expected: "unchanged client-maintained chat/story history surface",
                actual: actualHash,
                repairHint: "Не записывай chat_log.json или stories/*.jsonl в GM response. Эти continuity/history surfaces поддерживаются клиентом."));
        }

        var baselineHistoryPaths = new HashSet<string>(
            manifest.ClientOwnedValidationHashes.Keys.Where(IsClientOwnedHistoryValidationPath),
            StringComparer.OrdinalIgnoreCase);
        foreach (var storyPath in EnumerateStoryContinuityFiles())
        {
            if (baselineHistoryPaths.Contains(storyPath))
                continue;

            issues.Add(new ValidationIssue(
                storyPath,
                IssueSeverity.Error,
                "stories/*.jsonl являются client-maintained continuity history и не должны создаваться GM-ходом.",
                code: "client_owned_story_continuity_created_by_gm",
                section: "ClientHistory",
                expected: "no new GM-authored stories/*.jsonl files",
                actual: storyPath,
                repairHint: "Не создавай и не переписывай stories/*.jsonl в GM response; их поддерживает клиент как narrative continuity surface."));
        }

        foreach (var clientOwnedPath in new[]
                 {
                     WorldDirectiveService.PendingSetupPath,
                     WorldDirectiveService.ActiveDirectivesPath,
                     AfterlifeReturnGuardService.GuardPath,
                     ScenarioCoreService.ManifestPath,
                     AfterlifeArchiveCandidateService.ManifestPath,
                     GuardianCorrectionService.StatePath,
                     GuardianAbodeOfferingState.PendingRequestPath
                 })
        {
            if (!await DidFileChangeAgainstManifestAsync(manifest, clientOwnedPath))
                continue;

            issues.Add(new ValidationIssue(
                clientOwnedPath,
                IssueSeverity.Error,
                $"{Path.GetFileName(clientOwnedPath)} является client-authored control state и не должен изменяться GM-ходом.",
                code: clientOwnedPath.Equals(AfterlifeReturnGuardService.GuardPath, StringComparison.OrdinalIgnoreCase)
                    ? "client_owned_afterlife_return_guard_modified"
                    : clientOwnedPath.Equals(ScenarioCoreService.ManifestPath, StringComparison.OrdinalIgnoreCase)
                        ? "client_owned_scenario_core_modified"
                    : clientOwnedPath.Equals(AfterlifeArchiveCandidateService.ManifestPath, StringComparison.OrdinalIgnoreCase)
                        ? "client_owned_archive_candidate_manifest_modified"
                    : clientOwnedPath.Equals(GuardianCorrectionService.StatePath, StringComparison.OrdinalIgnoreCase)
                            ? "client_owned_guardian_corrections_modified"
                    : clientOwnedPath.Equals(GuardianAbodeOfferingState.PendingRequestPath, StringComparison.OrdinalIgnoreCase)
                        ? "client_owned_abode_offering_request_modified"
                    : "client_owned_world_setup_state_modified",
                section: clientOwnedPath.Equals(AfterlifeReturnGuardService.GuardPath, StringComparison.OrdinalIgnoreCase)
                    ? "Lifecycle"
                    : clientOwnedPath.Equals(AfterlifeArchiveCandidateService.ManifestPath, StringComparison.OrdinalIgnoreCase)
                        ? "AfterlifeArchive"
                    : clientOwnedPath.Equals(GuardianCorrectionService.StatePath, StringComparison.OrdinalIgnoreCase)
                        ? "GuardianCorrections"
                    : clientOwnedPath.Equals(GuardianAbodeOfferingState.PendingRequestPath, StringComparison.OrdinalIgnoreCase)
                        ? "GuardianOfferings"
                    : "WorldSetup",
                repairHint: $"Не записывай {clientOwnedPath} в GM response; этот файл поддерживается клиентом/игроком и должен читаться GM как входной контракт."));
        }
    }


    private async Task ValidateValidationRepairReadyAsync(List<ValidationIssue> issues)
    {
        const string readyPath = "game_state/control/validation_repair_ready.json";
        const string requestPath = "game_state/control/validation_repair_request.json";

        var readyJson = await _fs.ReadFileAsync(readyPath);
        if (string.IsNullOrWhiteSpace(readyJson))
            return;

        JsonElement readyRoot;
        try
        {
            using var readyDoc = JsonDocument.Parse(readyJson);
            if (readyDoc.RootElement.ValueKind != JsonValueKind.Object)
            {
                issues.Add(new ValidationIssue(
                    readyPath,
                    IssueSeverity.Error,
                    "validation_repair_ready.json должен быть JSON object",
                    code: "invalid_repair_ready_json",
                    section: "validation_repair_ready",
                    expected: "JSON object with sessionId/requestId/turnNumber",
                    actual: readyDoc.RootElement.ValueKind.ToString(),
                    repairHint: "Перезапиши validation_repair_ready.json как valid JSON object и скопируй в него точные sessionId/requestId/turnNumber из validation_repair_request.json."));
                return;
            }

            readyRoot = readyDoc.RootElement.Clone();
        }
        catch (JsonException ex)
        {
            issues.Add(new ValidationIssue(
                readyPath,
                IssueSeverity.Error,
                $"validation_repair_ready.json не является валидным JSON: {ex.Message}",
                code: "invalid_repair_ready_json",
                section: "validation_repair_ready",
                repairHint: "Перезапиши validation_repair_ready.json валидным JSON и скопируй в него точные sessionId/requestId/turnNumber из validation_repair_request.json."));
            return;
        }

        var requestJson = await _fs.ReadFileAsync(requestPath);
        if (string.IsNullOrWhiteSpace(requestJson))
        {
            issues.Add(new ValidationIssue(
                readyPath,
                IssueSeverity.Error,
                "validation_repair_ready.json не должен существовать без текущего validation_repair_request.json",
                code: "repair_ready_without_request",
                section: "validation_repair_ready",
                repairHint: "Используй validation_repair_ready.json только как ответ на активный validation_repair_request.json."));
            return;
        }

        try
        {
            using var requestDoc = JsonDocument.Parse(requestJson);
            if (requestDoc.RootElement.ValueKind != JsonValueKind.Object)
                return;

            var expectedSessionId = GetFirstNonEmptyString(requestDoc.RootElement, "sessionId") ?? string.Empty;
            var expectedRequestId = GetFirstNonEmptyString(requestDoc.RootElement, "requestId") ?? string.Empty;
            var expectedTurnNumber = requestDoc.RootElement.TryGetProperty("turnNumber", out var turnNumberNode) &&
                                     turnNumberNode.ValueKind == JsonValueKind.Number &&
                                     turnNumberNode.TryGetInt32(out var parsedTurn)
                ? parsedTurn
                : (int?)null;

            var actualSessionId = GetFirstNonEmptyString(readyRoot, "sessionId") ?? string.Empty;
            var actualRequestId = GetFirstNonEmptyString(readyRoot, "requestId") ?? string.Empty;
            var actualTurnNumber = readyRoot.TryGetProperty("turnNumber", out var readyTurnNode) &&
                                   readyTurnNode.ValueKind == JsonValueKind.Number &&
                                   readyTurnNode.TryGetInt32(out var parsedReadyTurn)
                ? parsedReadyTurn
                : (int?)null;

            if (!string.Equals(expectedSessionId, actualSessionId, StringComparison.Ordinal) ||
                !string.Equals(expectedRequestId, actualRequestId, StringComparison.Ordinal) ||
                expectedTurnNumber != actualTurnNumber)
            {
                issues.Add(new ValidationIssue(
                    readyPath,
                    IssueSeverity.Error,
                    "validation_repair_ready.json должен копировать точные sessionId/requestId/turnNumber из validation_repair_request.json",
                    code: "mismatched_repair_ready_context",
                    section: "validation_repair_ready",
                    expected: $"sessionId={expectedSessionId}, requestId={expectedRequestId}, turnNumber={expectedTurnNumber?.ToString() ?? "missing"}",
                    actual: $"sessionId={actualSessionId}, requestId={actualRequestId}, turnNumber={actualTurnNumber?.ToString() ?? "missing"}",
                    repairHint: "Скопируй sessionId/requestId/turnNumber ровно из текущего validation_repair_request.json без переиспользования старого ready signal."));
            }
        }
        catch
        {
            // ignored; request file shape is client-owned and validated elsewhere in client flow
        }
    }


    private async Task ValidateRealmSegregationAsync(List<ValidationIssue> issues)
    {
        var manifest = await LoadValidationPendingTurnSnapshotManifestAsync();
        if (manifest == null)
            return;

        if (IsRealmTransitionSourceLabel(manifest.SourceLabel) ||
            LifeEvaluationRewardAnalyzer.IsLifeEvaluationSourceLabel(manifest.SourceLabel))
        {
            return;
        }

        var preTurnRealm = await TryResolvePreTurnRealmAsync();
        if (string.IsNullOrWhiteSpace(preTurnRealm))
            return;
        var changedFiles = await GetChangedTrackedFilesAgainstManifestAsync(manifest);
        if (changedFiles.Count == 0)
            return;

        var forbiddenFiles = IsChaosSeaRealm(preTurnRealm)
            ? changedFiles.Where(IsForbiddenChaosSeaChangedFile).ToList()
            : changedFiles.Where(IsForbiddenMortalWorldChangedFile).ToList();

        if (forbiddenFiles.Count == 0)
            return;

        var expected = IsChaosSeaRealm(preTurnRealm)
            ? "Afterlife turn should mutate only soul/guardian/meta/codex/achievement/story surfaces and explicit cross-realm exceptions"
            : "Mortal World turn should mutate only mortal-world systems plus explicitly allowed meta exceptions";
        var forbiddenGroups = DescribeRealmSegregationGroups(forbiddenFiles);
        var actual = string.Join(", ", forbiddenFiles);
        if (forbiddenGroups.Count > 0)
            actual += $" | surfaces: {string.Join(", ", forbiddenGroups)}";
        var repairHint = IsChaosSeaRealm(preTurnRealm)
            ? "Убери mortal-world state mutations из afterlife-хода. В Chaos Sea/Shining Abode допустимы только soul_state, guardians, lore/chaos_sea, codex, achievements, story outputs и явные lifecycle/cross-realm исключения."
            : "Убери guardian / Chaos Sea / afterlife-only mutations из mortal-world хода. Оставь NPC, world, combat, quest, inventory, faction системы и только явно разрешённые meta-исключения.";

        issues.Add(new ValidationIssue(
            "game_state/meta/soul_state.json.currentRealm",
            IssueSeverity.Error,
            $"Нарушение Realm Segregation: pre-turn realm '{preTurnRealm}' несовместим с изменениями файлов {string.Join(", ", forbiddenFiles)}",
            code: "realm_segregation_violation",
            section: "RealmSegregation",
            expected: expected,
            actual: actual,
            repairHint: repairHint));
    }

}
