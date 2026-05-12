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
            catch (Exception ex)
            {
                _logger.LogDebug(ex, "Не удалось проверить required fields в soul_state/player_status.");
            }
        }
    }


    private async Task ValidateLoreBootstrapRequiredFilesAsync(List<ValidationIssue> issues)
    {
        var manifest = await LoadValidatedCurrentPendingTurnSnapshotManifestAsync();
        var readyTurnCompleteExists = _fs.FileExists("ready/turn_complete.json");
        var readyTurnErrorExists = _fs.FileExists("ready/turn_error.json");
        var soulJson = await _fs.ReadFileAsync("game_state/meta/soul_state.json");
        if (string.IsNullOrWhiteSpace(soulJson))
            return;

        try
        {
            using var doc = JsonDocument.Parse(soulJson);
            var root = doc.RootElement;
            if (root.ValueKind != JsonValueKind.Object)
                return;

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
                        switch (await DescribeTrackedFileChangeAgainstManifestAsync(manifest, bootstrapFile))
                        {
                            case ValidatedTrackedFileChangeStatus.Changed:
                                continue;
                            case ValidatedTrackedFileChangeStatus.MissingValidatedBaseline:
                                AddMissingValidatedTrackedBaselineIssue(
                                    issues,
                                    bootstrapFile,
                                    "mortal_bootstrap_missing_validated_world_lore_baseline",
                                    "LoreBootstrap",
                                    $"Нельзя строго доказать freshness для {bootstrapFile}: validated previous-world lore baseline missing.",
                                    "Для current_world bootstrap сохраняй validated pre-turn snapshot entry для каждого reused lore/current_world/* файла. Если файл участвует в freshness proof, baseline не может отсутствовать.");
                                continue;
                        }

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
        var validatedManifest = await LoadValidatedCurrentPendingTurnSnapshotManifestAsync();
        if (validatedManifest == null || !LifeEvaluationRewardAnalyzer.IsLifeEvaluationSourceLabel(validatedManifest.SourceLabel))
            return;

        const string soulStatePath = "game_state/meta/soul_state.json";
        var preSoulStateJson = await ReadValidatedPendingTurnSnapshotFileAsync(validatedManifest, soulStatePath);
        var postSoulStateJson = await _fs.ReadFileAsync(soulStatePath);

        if (!LifeEvaluationRewardAnalyzer.TryComputeDelta(
                preSoulStateJson,
                postSoulStateJson,
                hasCanonicalTriggerLifeEnd: false,
                out var delta,
                out var error) ||
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
        var preChronicleJson = await ReadValidatedPendingTurnSnapshotFileAsync(validatedManifest, playerChroniclePath);
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
        var validatedManifest = await LoadValidatedCurrentPendingTurnSnapshotManifestAsync();
        if (validatedManifest == null || LifeEvaluationRewardAnalyzer.IsLifeEvaluationSourceLabel(validatedManifest.SourceLabel))
            return;

        const string lifeTransitionsPath = "game_state/control/life_transitions.json";
        var lifeTransitionsJson = await _fs.ReadFileAsync(lifeTransitionsPath);
        var preTurnRealm = validatedManifest != null
            ? await TryReadValidatedPendingTurnSnapshotRealmAsync(validatedManifest)
            : null;
        var currentRealm = await TryResolveCurrentRealmAsync();
        if (!CanonicalStateNormalizer.TryReadCanonicalTriggerLifeEnd(lifeTransitionsJson, out _, out _))
            return;

        var triggerAuthority = CanonicalStateNormalizer.ResolveLifecycleAuthorizedTriggerLifeEnd(
            lifeTransitionsJson,
            preTurnRealm,
            currentRealm);
        if (!triggerAuthority.IsAuthorized)
        {
            issues.Add(new ValidationIssue(
                lifeTransitionsPath,
                IssueSeverity.Error,
                $"TriggerLifeEnd turn не подтвердил canonical realm authority: {triggerAuthority.Description}",
                code: "life_trigger_turn_missing_realm_authority",
                section: "LifeEvaluation",
                expected: "canonical TriggerLifeEnd with readable mortal pre-turn and current realm authority",
                actual: $"{triggerAuthority.PreTriggerRealm ?? "missing"} -> {triggerAuthority.CurrentRealm ?? "missing"}",
                repairHint: "На TriggerLifeEnd turn сохраняй readable currentRealm в pre-turn snapshot и текущем soul_state. Unresolved realm не должен bypass-ить reward-delta validation."));
            return;
        }

        const string soulStatePath = "game_state/meta/soul_state.json";
        var preSoulStateJson = validatedManifest != null
            ? await ReadValidatedPendingTurnSnapshotFileAsync(validatedManifest, soulStatePath)
            : null;
        var postSoulStateJson = await _fs.ReadFileAsync(soulStatePath);
        if (!LifeEvaluationRewardAnalyzer.TryComputeDelta(
                preSoulStateJson,
                postSoulStateJson,
                hasCanonicalTriggerLifeEnd: true,
                out var delta,
                out var error) ||
            delta == null)
        {
            issues.Add(new ValidationIssue(
                soulStatePath,
                IssueSeverity.Error,
                $"Не удалось вычислить reward delta для TriggerLifeEnd turn: {error ?? "unknown error"}",
                code: "life_trigger_turn_reward_delta_unreadable",
                section: "LifeEvaluation",
                expected: "валидный pre/post diff по soul_state.json на canonical TriggerLifeEnd turn",
                actual: error ?? "unknown error",
                repairHint: "На accepted turn с TriggerLifeEnd сохрани валидный pre-turn snapshot soul_state.json и не ломай текущий canonical soul_state до отдельного Life Evaluation turn."));
        }
        else
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
        var preChronicleJson = validatedManifest != null
            ? await ReadValidatedPendingTurnSnapshotFileAsync(validatedManifest, playerChroniclePath)
            : null;
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
        var manifestExists = _fs.FileExists(PendingTurnSnapshotManifestPath);
        var manifest = await LoadValidationPendingTurnSnapshotManifestAsync();
        var validatedLookup = await LoadValidatedPendingTurnSnapshotLookupAsync();
        var hasPendingSnapshotManifestIssue = false;
        await ValidateValidationRepairReadyAsync(issues);
        await ValidateArchiveCandidateManifestAsync(issues);
        await ValidatePendingAbodeOfferingContextAsync(issues);
        await ValidatePendingGuardianTradeRequestContextAsync(issues);
        await ValidatePendingPlayerGuardianFoundationContextAsync(issues);
        await ValidatePendingNpcTradeInventoryRequestContextAsync(issues);
        await ValidatePendingArchiveConsultationRequestContextAsync(issues);
        await ValidatePendingArchiveProjectFuelRequestContextAsync(issues);
        await ValidatePendingGuardianAbodeResidentsRequestContextAsync(issues);
        await ValidatePendingGuardianAbodeResidentInteractionRequestContextAsync(issues);
        await ValidatePendingGuardianAbodeResidentTransferRequestContextAsync(issues);
        await ValidatePendingGuardianSocialInteractionRequestContextAsync(issues);
        await ValidatePendingNpcSocialInteractionRequestContextAsync(issues);
        if (_fs.FileExists("ready/turn_complete.json") ||
            _fs.FileExists("ready/turn_error.json"))
        {
            await ValidatePendingShiningCoreActionResolutionAsync(issues);
            if (_fs.FileExists("ready/turn_complete.json"))
                await ValidateLegacyPendingShiningNativeFactionDiscoveryResolutionAsync(issues);
            await ValidatePendingShiningTradeInventoryResolutionAsync(issues);
            await ValidatePendingAbodeOfferingResolutionAsync(issues);
            await ValidatePendingGuardianTradeRequestResolutionAsync(issues);
            await ValidatePendingPlayerGuardianFoundationResolutionAsync(issues);
            await ValidatePendingNpcTradeInventoryRequestResolutionAsync(issues);
            await ValidatePendingArchiveConsultationResolutionAsync(issues);
            await ValidatePendingArchiveProjectFuelResolutionAsync(issues);
            await ValidatePendingGuardianAbodeResidentsResolutionAsync(issues);
            await ValidatePendingGuardianAbodeResidentInteractionResolutionAsync(issues);
            await ValidatePendingGuardianAbodeResidentTransferResolutionAsync(issues);
            await ValidatePendingShiningFoundingResolutionAsync(issues);
            await ValidatePendingShiningRealignmentResolutionAsync(issues);
            await ValidatePendingShiningLeadershipTransitionResolutionAsync(issues);
            await ValidateShiningClosureCompositeDiffAsync(issues);
            await ValidatePendingGuardianSocialInteractionResolutionAsync(issues);
            await ValidatePendingNpcSocialInteractionResolutionAsync(issues);
            await ValidateResidentMechanicalOutcomeMemoryAsync(issues);
        }

        if (manifest == null)
        {
            if (manifestExists && validatedLookup.Status == ValidatedPendingTurnSnapshotStatus.Unusable)
            {
                issues.Add(new ValidationIssue(
                    PendingTurnSnapshotManifestPath,
                    IssueSeverity.Error,
                    "pending_turn_snapshot.json больше не является читаемым client-owned snapshot manifest и не может использоваться как authority surface.",
                    code: "client_owned_pending_snapshot_manifest_modified",
                    section: "PendingTurnSnapshot",
                    expected: "readable validated pending turn snapshot manifest",
                    actual: "missing or invalid JSON payload",
                    repairHint: "Не изменяй pending_turn_snapshot.json в GM turn; это client-owned transient control file."));
                hasPendingSnapshotManifestIssue = true;
            }

            return;
        }

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
                hasPendingSnapshotManifestIssue = true;
            }
        }

        if (validatedLookup.Status != ValidatedPendingTurnSnapshotStatus.Usable || validatedLookup.Manifest == null)
        {
            if (!hasPendingSnapshotManifestIssue)
            {
                issues.Add(new ValidationIssue(
                    PendingTurnSnapshotManifestPath,
                    IssueSeverity.Error,
                    "pending_turn_snapshot.json больше не является пригодным validated snapshot manifest и не может использоваться как authority surface.",
                    code: "client_owned_pending_snapshot_manifest_modified",
                    section: "PendingTurnSnapshot",
                    expected: "usable validated pending turn snapshot manifest with readable sourceLabel/files/snapshot hashes",
                    actual: DescribeValidatedPendingTurnSnapshotStatus(validatedLookup.Status),
                    repairHint: "Не изменяй pending_turn_snapshot.json в GM turn; это client-owned transient control file."));
            }

            return;
        }

        var validatedManifest = validatedLookup.Manifest;

        foreach (var (originalPath, snapshotPath) in validatedManifest.Files)
        {
            if (!validatedManifest.SnapshotFileHashes.TryGetValue(originalPath, out var expectedHash) || string.IsNullOrWhiteSpace(expectedHash))
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

        foreach (var (baselinePath, expectedHash) in validatedManifest.ClientOwnedValidationHashes)
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
            validatedManifest.ClientOwnedValidationHashes.Keys.Where(IsClientOwnedHistoryValidationPath),
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
                     GuardianAbodeOfferingState.PendingRequestPath,
                     GuardianTradeRequestState.PendingRequestPath,
                     PlayerGuardianFoundationState.PendingRequestPath,
                     NpcTradeRequestState.PendingRequestPath,
                     AfterlifeArchiveActionState.ConsultationRequestPath,
                     AfterlifeArchiveActionState.ProjectFuelRequestPath,
                     GuardianAbodeResidentRequestState.PendingResidentsRequestPath,
                     GuardianAbodeResidentRequestState.PendingInteractionsRequestPath,
                     GuardianAbodeResidentRequestState.PendingTransfersRequestPath,
                     GuardianAbodeResidentRequestState.PendingManifestationRequestPath,
                     ActorSocialInteractionRequestState.PendingGuardianRequestPath,
                     ActorSocialInteractionRequestState.PendingNpcRequestPath,
                     SystemGuardianLibraryService.AttractionRequestPath,
                     ShiningCoreActionRequestState.PendingActionsRequestPath,
                     ShiningTradeRequestState.PendingRequestsPath,
                     ShiningFactionRequestState.PendingFoundingsRequestPath,
                     ShiningFactionRequestState.PendingRealignmentsRequestPath,
                     ShiningFactionRequestState.PendingLeadershipTransitionsRequestPath
                 })
        {
            switch (await DescribeTrackedFileChangeAgainstManifestAsync(manifest, clientOwnedPath))
            {
                case ValidatedTrackedFileChangeStatus.Unchanged:
                    continue;
                case ValidatedTrackedFileChangeStatus.MissingValidatedBaseline:
                    AddMissingValidatedTrackedBaselineIssue(
                        issues,
                        clientOwnedPath,
                        "client_owned_control_missing_validated_baseline",
                        clientOwnedPath.Equals(AfterlifeReturnGuardService.GuardPath, StringComparison.OrdinalIgnoreCase)
                            ? "Lifecycle"
                            : clientOwnedPath.Equals(AfterlifeArchiveCandidateService.ManifestPath, StringComparison.OrdinalIgnoreCase)
                                ? "AfterlifeArchive"
                            : clientOwnedPath.Equals(AfterlifeArchiveActionState.ConsultationRequestPath, StringComparison.OrdinalIgnoreCase) ||
                              clientOwnedPath.Equals(AfterlifeArchiveActionState.ProjectFuelRequestPath, StringComparison.OrdinalIgnoreCase)
                                ? "AfterlifeArchive"
                            : clientOwnedPath.Equals(SystemGuardianLibraryService.AttractionRequestPath, StringComparison.OrdinalIgnoreCase)
                                ? "SystemGuardianPresets"
                            : clientOwnedPath.Equals(GuardianTradeRequestState.PendingRequestPath, StringComparison.OrdinalIgnoreCase)
                                ? "GuardianTrade"
                            : clientOwnedPath.Equals(PlayerGuardianFoundationState.PendingRequestPath, StringComparison.OrdinalIgnoreCase)
                                ? "PlayerGuardianFoundation"
                            : clientOwnedPath.Equals(NpcTradeRequestState.PendingRequestPath, StringComparison.OrdinalIgnoreCase) ||
                              clientOwnedPath.Equals(ActorSocialInteractionRequestState.PendingNpcRequestPath, StringComparison.OrdinalIgnoreCase)
                                ? "NpcContracts"
                            : clientOwnedPath.Equals(GuardianAbodeResidentRequestState.PendingResidentsRequestPath, StringComparison.OrdinalIgnoreCase) ||
                              clientOwnedPath.Equals(GuardianAbodeResidentRequestState.PendingInteractionsRequestPath, StringComparison.OrdinalIgnoreCase) ||
                              clientOwnedPath.Equals(GuardianAbodeResidentRequestState.PendingTransfersRequestPath, StringComparison.OrdinalIgnoreCase) ||
                              clientOwnedPath.Equals(GuardianAbodeResidentRequestState.PendingManifestationRequestPath, StringComparison.OrdinalIgnoreCase)
                                ? "GuardianAbodeResidents"
                            : clientOwnedPath.Equals(ActorSocialInteractionRequestState.PendingGuardianRequestPath, StringComparison.OrdinalIgnoreCase)
                                ? "GuardianSocial"
                                : "BootstrapProtocol",
                        $"{Path.GetFileName(clientOwnedPath)} нельзя проверить строго: validated pre-turn baseline отсутствует.",
                        "Для client-authored control surfaces сохраняй validated snapshot entry в pending turn snapshot, чтобы GM-side diff checks не опирались на missing baseline.");
                    continue;
            }

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
                    : clientOwnedPath.Equals(GuardianTradeRequestState.PendingRequestPath, StringComparison.OrdinalIgnoreCase)
                        ? "client_owned_guardian_trade_request_modified"
                    : clientOwnedPath.Equals(PlayerGuardianFoundationState.PendingRequestPath, StringComparison.OrdinalIgnoreCase)
                        ? "client_owned_player_guardian_foundation_request_modified"
                    : clientOwnedPath.Equals(NpcTradeRequestState.PendingRequestPath, StringComparison.OrdinalIgnoreCase)
                        ? "client_owned_npc_trade_request_modified"
                    : clientOwnedPath.Equals(AfterlifeArchiveActionState.ConsultationRequestPath, StringComparison.OrdinalIgnoreCase)
                        ? "client_owned_archive_consultation_request_modified"
                    : clientOwnedPath.Equals(AfterlifeArchiveActionState.ProjectFuelRequestPath, StringComparison.OrdinalIgnoreCase)
                        ? "client_owned_archive_project_fuel_request_modified"
                    : clientOwnedPath.Equals(GuardianAbodeResidentRequestState.PendingResidentsRequestPath, StringComparison.OrdinalIgnoreCase)
                        ? "client_owned_resident_roster_request_modified"
                    : clientOwnedPath.Equals(GuardianAbodeResidentRequestState.PendingInteractionsRequestPath, StringComparison.OrdinalIgnoreCase)
                        ? "client_owned_resident_interaction_request_modified"
                    : clientOwnedPath.Equals(GuardianAbodeResidentRequestState.PendingTransfersRequestPath, StringComparison.OrdinalIgnoreCase)
                        ? "client_owned_resident_transfer_request_modified"
                    : clientOwnedPath.Equals(GuardianAbodeResidentRequestState.PendingManifestationRequestPath, StringComparison.OrdinalIgnoreCase)
                        ? "client_owned_resident_manifestation_request_modified"
                    : clientOwnedPath.Equals(ActorSocialInteractionRequestState.PendingGuardianRequestPath, StringComparison.OrdinalIgnoreCase)
                        ? "client_owned_guardian_social_request_modified"
                    : clientOwnedPath.Equals(ActorSocialInteractionRequestState.PendingNpcRequestPath, StringComparison.OrdinalIgnoreCase)
                        ? "client_owned_npc_social_request_modified"
                    : clientOwnedPath.Equals(SystemGuardianLibraryService.AttractionRequestPath, StringComparison.OrdinalIgnoreCase)
                        ? "client_owned_system_guardian_attraction_modified"
                    : clientOwnedPath.Equals(ShiningCoreActionRequestState.PendingActionsRequestPath, StringComparison.OrdinalIgnoreCase)
                        ? "client_owned_shining_core_action_request_modified"
                    : clientOwnedPath.Equals(ShiningTradeRequestState.PendingRequestsPath, StringComparison.OrdinalIgnoreCase)
                        ? "client_owned_shining_trade_request_modified"
                    : clientOwnedPath.Equals(ShiningFactionRequestState.PendingFoundingsRequestPath, StringComparison.OrdinalIgnoreCase)
                        ? "client_owned_shining_founding_request_modified"
                    : clientOwnedPath.Equals(ShiningFactionRequestState.PendingRealignmentsRequestPath, StringComparison.OrdinalIgnoreCase)
                        ? "client_owned_shining_realignment_request_modified"
                    : clientOwnedPath.Equals(ShiningFactionRequestState.PendingLeadershipTransitionsRequestPath, StringComparison.OrdinalIgnoreCase)
                        ? "client_owned_shining_leadership_request_modified"
                    : "client_owned_world_setup_state_modified",
                section: clientOwnedPath.Equals(AfterlifeReturnGuardService.GuardPath, StringComparison.OrdinalIgnoreCase)
                    ? "Lifecycle"
                    : clientOwnedPath.Equals(AfterlifeArchiveCandidateService.ManifestPath, StringComparison.OrdinalIgnoreCase)
                        ? "AfterlifeArchive"
                    : clientOwnedPath.Equals(GuardianCorrectionService.StatePath, StringComparison.OrdinalIgnoreCase)
                        ? "GuardianCorrections"
                    : clientOwnedPath.Equals(GuardianAbodeOfferingState.PendingRequestPath, StringComparison.OrdinalIgnoreCase)
                        ? "GuardianOfferings"
                    : clientOwnedPath.Equals(GuardianTradeRequestState.PendingRequestPath, StringComparison.OrdinalIgnoreCase)
                        ? "GuardianTrade"
                    : clientOwnedPath.Equals(PlayerGuardianFoundationState.PendingRequestPath, StringComparison.OrdinalIgnoreCase)
                        ? "PlayerGuardianFoundation"
                    : clientOwnedPath.Equals(NpcTradeRequestState.PendingRequestPath, StringComparison.OrdinalIgnoreCase) ||
                      clientOwnedPath.Equals(ActorSocialInteractionRequestState.PendingNpcRequestPath, StringComparison.OrdinalIgnoreCase)
                        ? "NpcContracts"
                    : clientOwnedPath.Equals(AfterlifeArchiveActionState.ConsultationRequestPath, StringComparison.OrdinalIgnoreCase) ||
                      clientOwnedPath.Equals(AfterlifeArchiveActionState.ProjectFuelRequestPath, StringComparison.OrdinalIgnoreCase)
                        ? "AfterlifeArchive"
                    : clientOwnedPath.Equals(GuardianAbodeResidentRequestState.PendingResidentsRequestPath, StringComparison.OrdinalIgnoreCase) ||
                      clientOwnedPath.Equals(GuardianAbodeResidentRequestState.PendingInteractionsRequestPath, StringComparison.OrdinalIgnoreCase) ||
                      clientOwnedPath.Equals(GuardianAbodeResidentRequestState.PendingTransfersRequestPath, StringComparison.OrdinalIgnoreCase) ||
                      clientOwnedPath.Equals(GuardianAbodeResidentRequestState.PendingManifestationRequestPath, StringComparison.OrdinalIgnoreCase)
                        ? "GuardianAbodeResidents"
                    : clientOwnedPath.Equals(ActorSocialInteractionRequestState.PendingGuardianRequestPath, StringComparison.OrdinalIgnoreCase)
                        ? "GuardianSocial"
                    : clientOwnedPath.Equals(SystemGuardianLibraryService.AttractionRequestPath, StringComparison.OrdinalIgnoreCase)
                        ? "SystemGuardianPresets"
                    : clientOwnedPath.Equals(ShiningCoreActionRequestState.PendingActionsRequestPath, StringComparison.OrdinalIgnoreCase) ||
                      clientOwnedPath.Equals(ShiningTradeRequestState.PendingRequestsPath, StringComparison.OrdinalIgnoreCase) ||
                      clientOwnedPath.Equals(ShiningFactionRequestState.PendingFoundingsRequestPath, StringComparison.OrdinalIgnoreCase) ||
                      clientOwnedPath.Equals(ShiningFactionRequestState.PendingRealignmentsRequestPath, StringComparison.OrdinalIgnoreCase) ||
                      clientOwnedPath.Equals(ShiningFactionRequestState.PendingLeadershipTransitionsRequestPath, StringComparison.OrdinalIgnoreCase)
                        ? "ShiningAbode"
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

        var requestJson = await _fs.ReadFileAsync(requestPath);

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
                    repairHint: BuildInvalidRepairReadyRepairHint(requestJson, requireJsonObject: true)));
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
                repairHint: BuildInvalidRepairReadyRepairHint(requestJson, requireJsonObject: false)));
            return;
        }

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

        var hasAuthoritativeRepairRequestMetadata = false;
        string expectedSessionId = string.Empty;
        string expectedRequestId = string.Empty;
        int? expectedTurnNumber = null;
        try
        {
            using var requestDoc = JsonDocument.Parse(requestJson);
            if (requestDoc.RootElement.ValueKind != JsonValueKind.Object)
                return;

            var metadataDiagnosticOnly = requestDoc.RootElement.TryGetProperty("metadataDiagnosticOnly", out var metadataDiagnosticOnlyNode) &&
                                         metadataDiagnosticOnlyNode.ValueKind is JsonValueKind.True or JsonValueKind.False &&
                                         metadataDiagnosticOnlyNode.GetBoolean();

            if (metadataDiagnosticOnly)
            {
                issues.Add(new ValidationIssue(
                    readyPath,
                    IssueSeverity.Error,
                    "validation_repair_ready.json не может использоваться, пока текущий validation_repair_request.json помечен diagnostic-only metadata",
                    code: "repair_ready_against_diagnostic_only_request",
                    section: "validation_repair_ready",
                    expected: "Current validation_repair_request.json with authoritative sessionId/requestId/turnNumber",
                    actual: "Current validation_repair_request.json has metadataDiagnosticOnly=true and only sentinel correlation metadata",
                    repairHint: "Не создавай validation_repair_ready.json по sentinel metadata из текущего validation_repair_request.json. Сначала восстанови pending snapshot context/authority и дождись самого свежего repair request с authoritative metadata."));
                return;
            }

            expectedSessionId = GetFirstNonEmptyString(requestDoc.RootElement, "sessionId") ?? string.Empty;
            expectedRequestId = GetFirstNonEmptyString(requestDoc.RootElement, "requestId") ?? string.Empty;
            expectedTurnNumber = requestDoc.RootElement.TryGetProperty("turnNumber", out var turnNumberNode) &&
                                 turnNumberNode.ValueKind == JsonValueKind.Number &&
                                 turnNumberNode.TryGetInt32(out var parsedTurn)
                ? parsedTurn
                : (int?)null;
            hasAuthoritativeRepairRequestMetadata = true;
        }
        catch
        {
            // ignored; request file shape is client-owned and validated elsewhere in client flow
        }

        if (!hasAuthoritativeRepairRequestMetadata)
            return;

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

    private static string BuildInvalidRepairReadyRepairHint(string? requestJson, bool requireJsonObject)
    {
        var authoritativeMetadataHint = requireJsonObject
            ? "Перезапиши validation_repair_ready.json как valid JSON object и скопируй в него точные sessionId/requestId/turnNumber из validation_repair_request.json."
            : "Перезапиши validation_repair_ready.json валидным JSON и скопируй в него точные sessionId/requestId/turnNumber из validation_repair_request.json.";
        const string diagnosticOnlyMetadataHint = "Не создавай validation_repair_ready.json по sentinel metadata из текущего validation_repair_request.json. Сначала восстанови pending snapshot context/authority и дождись самого свежего repair request с authoritative metadata.";

        if (string.IsNullOrWhiteSpace(requestJson))
            return authoritativeMetadataHint;

        try
        {
            using var requestDoc = JsonDocument.Parse(requestJson);
            if (requestDoc.RootElement.ValueKind != JsonValueKind.Object)
                return authoritativeMetadataHint;

            var metadataDiagnosticOnly = requestDoc.RootElement.TryGetProperty("metadataDiagnosticOnly", out var metadataDiagnosticOnlyNode) &&
                                         metadataDiagnosticOnlyNode.ValueKind is JsonValueKind.True or JsonValueKind.False &&
                                         metadataDiagnosticOnlyNode.GetBoolean();

            return metadataDiagnosticOnly
                ? diagnosticOnlyMetadataHint
                : authoritativeMetadataHint;
        }
        catch
        {
            return authoritativeMetadataHint;
        }
    }


    private async Task ValidateRealmSegregationAsync(List<ValidationIssue> issues)
    {
        if (!_fs.FileExists(PendingTurnSnapshotManifestPath))
            return;

        var manifest = await LoadRequiredValidatedCurrentPendingTurnSnapshotManifestAsync(
            "game_state/meta/soul_state.json.currentRealm",
            issues,
            code: "realm_segregation_missing_validated_snapshot_context",
            section: "RealmSegregation",
            message: "Realm Segregation требует current validated pending turn snapshot manifest.",
            repairHint: "Для accepted-turn realm segregation validation сохраняй untampered current pending turn snapshot manifest и не опирайся на отсутствующий или modified manifest.");
        if (manifest == null)
            return;

        if (IsRealmTransitionSourceLabel(manifest.SourceLabel) ||
            LifeEvaluationRewardAnalyzer.IsLifeEvaluationSourceLabel(manifest.SourceLabel))
        {
            return;
        }

        var preTurnRealm = await ReadRequiredValidatedPendingTurnSnapshotRealmAsync(
            manifest,
            "game_state/meta/soul_state.json.currentRealm",
            issues,
            code: "realm_segregation_invalid_validated_snapshot_realm",
            section: "RealmSegregation",
            message: "Realm Segregation требует validated pre-turn realm из snapshot soul_state.",
            repairHint: "Для accepted-turn realm segregation validation сохраняй validated snapshot copy of game_state/meta/soul_state.json с canonical currentRealm.");
        if (string.IsNullOrWhiteSpace(preTurnRealm))
            return;
        var changedFiles = await GetChangedTrackedFilesAgainstManifestAsync(
            manifest,
            issues,
            "realm_segregation_missing_validated_tracked_baseline",
            "RealmSegregation",
            "Для realm segregation validation tracked files из validated pre-turn surface должны иметь snapshot entry/hash; missing validated baseline недопустим.");
        if (changedFiles.Count == 0)
            return;

        var forbiddenFiles = IsChaosSeaRealm(preTurnRealm)
            ? changedFiles.Where(IsForbiddenChaosSeaChangedFile).ToList()
            : changedFiles.Where(IsForbiddenMortalWorldChangedFile).ToList();
        if (!IsChaosSeaRealm(preTurnRealm))
            forbiddenFiles = await FilterAllowedMortalGuardianQuestProgressFilesAsync(manifest, forbiddenFiles, preTurnRealm);

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

    private async Task<List<string>> FilterAllowedMortalGuardianQuestProgressFilesAsync(
        ValidationPendingTurnSnapshotManifest manifest,
        List<string> forbiddenFiles,
        string? preTurnRealm)
    {
        if (!forbiddenFiles.Any(path => path.Replace('\\', '/').Equals("game_state/meta/guardians.json", StringComparison.OrdinalIgnoreCase)))
            return forbiddenFiles;

        if (!RealmSemantics.IsMortalRealm(preTurnRealm) ||
            !await IsAllowedMortalGuardianQuestProgressDeltaAsync(manifest))
        {
            return forbiddenFiles;
        }

        return forbiddenFiles
            .Where(path => !path.Replace('\\', '/').Equals("game_state/meta/guardians.json", StringComparison.OrdinalIgnoreCase))
            .ToList();
    }

    private async Task<bool> IsAllowedMortalGuardianQuestProgressDeltaAsync(ValidationPendingTurnSnapshotManifest manifest)
    {
        try
        {
            var preTurnJson = await ReadValidatedPendingTurnSnapshotFileAsync(manifest, "game_state/meta/guardians.json");
            var currentJson = await _fs.ReadFileAsync("game_state/meta/guardians.json");
            if (string.IsNullOrWhiteSpace(preTurnJson) || string.IsNullOrWhiteSpace(currentJson))
                return false;

            if (JsonNode.Parse(preTurnJson) is not JsonObject preTurnRoot ||
                JsonNode.Parse(currentJson) is not JsonObject currentRoot)
            {
                return false;
            }

            if (TryBuildAuthorizedMortalGuardianQuestProgressUpdates(preTurnRoot, currentRoot, out _))
                return true;

            return false;
        }
        catch
        {
            return false;
        }
    }

    private static bool TryBuildAuthorizedMortalGuardianQuestProgressUpdates(
        JsonObject preTurnRoot,
        JsonObject currentRoot,
        out List<JsonObject> updates)
    {
        updates = new List<JsonObject>();
        if (!TryCollectExplicitGuardianQuestProgressUpdates(currentRoot, out var explicitUpdates))
            return false;

        var comparablePreTurn = preTurnRoot.DeepClone().AsObject();
        var comparableCurrent = currentRoot.DeepClone().AsObject();
        NormalizeGuardianQuestProgressDeltaComparableRoot(comparablePreTurn);
        NormalizeGuardianQuestProgressDeltaComparableRoot(comparableCurrent);

        var resetCurrent = comparableCurrent.DeepClone().AsObject();
        if (!TryResetAllowedGuardianQuestProgressDeltas(comparablePreTurn.DeepClone().AsObject(), resetCurrent))
            return false;

        if (!JsonNode.DeepEquals(comparablePreTurn, resetCurrent))
            return false;

        var materializedUpdates = new List<JsonObject>();
        CollectGuardianQuestProgressAuthorityUpdates(comparablePreTurn, comparableCurrent, materializedUpdates);
        if (!MaterializedGuardianQuestProgressUpdatesMatchExplicitCommands(comparablePreTurn, materializedUpdates, explicitUpdates))
            return false;

        updates.AddRange(materializedUpdates.Select(update => update.DeepClone().AsObject()));
        return true;
    }

    private static bool TryCollectExplicitGuardianQuestProgressUpdates(
        JsonObject currentRoot,
        out List<JsonObject> updates)
    {
        updates = new List<JsonObject>();
        if (currentRoot[GuardianProjectState.QuestProgressUpdatesProperty] is not JsonArray arr ||
            arr.Count == 0)
        {
            return false;
        }

        var seenKeys = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        foreach (var item in arr)
        {
            if (item is not JsonObject update)
                return false;

            var guardianId = GetNodeString(update["guardianId"]);
            var questId = GetNodeString(update["questId"]);
            if (string.IsNullOrWhiteSpace(guardianId) || string.IsNullOrWhiteSpace(questId))
                return false;

            var key = $"{guardianId}::{questId}";
            if (!seenKeys.Add(key))
                return false;

            updates.Add(update.DeepClone().AsObject());
        }

        return true;
    }

    private static bool MaterializedGuardianQuestProgressUpdatesMatchExplicitCommands(
        JsonObject preTurnRoot,
        IReadOnlyCollection<JsonObject> materializedUpdates,
        IReadOnlyCollection<JsonObject> explicitUpdates)
    {
        var explicitByKey = new Dictionary<string, JsonObject>(StringComparer.OrdinalIgnoreCase);
        foreach (var update in explicitUpdates)
        {
            var guardianId = GetNodeString(update["guardianId"]);
            var questId = GetNodeString(update["questId"]);
            if (string.IsNullOrWhiteSpace(guardianId) || string.IsNullOrWhiteSpace(questId))
                return false;

            explicitByKey[$"{guardianId}::{questId}"] = update;
        }

        foreach (var materialized in materializedUpdates)
        {
            var guardianId = GetNodeString(materialized["guardianId"]);
            var questId = GetNodeString(materialized["questId"]);
            if (string.IsNullOrWhiteSpace(guardianId) || string.IsNullOrWhiteSpace(questId))
                return false;

            if (!explicitByKey.TryGetValue($"{guardianId}::{questId}", out var explicitUpdate))
                return false;

            if (!TryFindGuardianQuest(preTurnRoot, guardianId!, questId!, out var preTurnQuest) ||
                preTurnQuest == null ||
                !GuardianQuestProgressMaterializedUpdateMatchesExplicitCommand(preTurnQuest, materialized, explicitUpdate))
            {
                return false;
            }
        }

        return true;
    }

    private static bool TryFindGuardianQuest(JsonObject root, string guardianId, string questId, out JsonObject? quest)
    {
        quest = null;
        if (root["guardians"] is not JsonArray guardians)
            return false;

        var guardian = guardians
            .OfType<JsonObject>()
            .FirstOrDefault(item => string.Equals(GetNodeString(item["guardianId"]), guardianId, StringComparison.OrdinalIgnoreCase));
        if (guardian?["questManagement"] is not JsonObject questManagement ||
            questManagement["activeQuests"] is not JsonArray activeQuests)
        {
            return false;
        }

        quest = activeQuests
            .OfType<JsonObject>()
            .FirstOrDefault(item => string.Equals(GetNodeString(item["questId"]), questId, StringComparison.OrdinalIgnoreCase));
        return quest != null;
    }

    private static bool GuardianQuestProgressMaterializedUpdateMatchesExplicitCommand(
        JsonObject preTurnQuest,
        JsonObject materialized,
        JsonObject explicitUpdate)
    {
        foreach (var fieldName in GuardianQuestProgressExplicitCommandFields)
        {
            var hasMaterializedValue = materialized.TryGetPropertyValue(fieldName, out var materializedValue);
            var hasExplicitValue = explicitUpdate.TryGetPropertyValue(fieldName, out var explicitValue);
            var fieldChanged = GuardianQuestProgressFieldChanged(preTurnQuest, materialized, fieldName);
            if (fieldChanged && (!hasMaterializedValue || !hasExplicitValue))
                return false;

            if (hasExplicitValue &&
                (!hasMaterializedValue || !JsonNode.DeepEquals(materializedValue, explicitValue)))
            {
                return false;
            }
        }

        return true;
    }

    private static bool GuardianQuestProgressFieldChanged(JsonObject preTurnQuest, JsonObject materialized, string fieldName)
    {
        var hasPreTurnValue = preTurnQuest.TryGetPropertyValue(fieldName, out var preTurnValue);
        var hasMaterializedValue = materialized.TryGetPropertyValue(fieldName, out var materializedValue);
        return hasPreTurnValue != hasMaterializedValue ||
               (hasPreTurnValue && hasMaterializedValue && !JsonNode.DeepEquals(preTurnValue, materializedValue));
    }

    private static void NormalizeGuardianQuestProgressDeltaComparableRoot(JsonObject root)
    {
        root.Remove("_lastUpdated");
        root.Remove(GuardianProjectState.QuestProgressUpdatesProperty);

        if (root["guardians"] is JsonArray guardians)
        {
            foreach (var guardian in guardians.OfType<JsonObject>())
                GuardianTradeRequestState.NormalizeGuardianTradeReceiptsShape(guardian);
        }

        if (root["activeGuardian"] is JsonObject activeGuardian)
            GuardianTradeRequestState.NormalizeGuardianTradeReceiptsShape(activeGuardian);
    }

    private static void CollectGuardianQuestProgressAuthorityUpdates(
        JsonObject preTurnRoot,
        JsonObject currentRoot,
        List<JsonObject> updates)
    {
        if (preTurnRoot["guardians"] is not JsonArray preTurnGuardians ||
            currentRoot["guardians"] is not JsonArray currentGuardians)
        {
            return;
        }

        foreach (var currentGuardian in currentGuardians.OfType<JsonObject>())
        {
            var guardianId = GetNodeString(currentGuardian["guardianId"]);
            if (string.IsNullOrWhiteSpace(guardianId))
                continue;

            var preTurnGuardian = preTurnGuardians
                .OfType<JsonObject>()
                .FirstOrDefault(guardian => string.Equals(GetNodeString(guardian["guardianId"]), guardianId, StringComparison.OrdinalIgnoreCase));
            if (preTurnGuardian == null)
                continue;

            CollectGuardianQuestProgressAuthorityUpdatesForGuardian(guardianId!, preTurnGuardian, currentGuardian, updates);
        }
    }

    private static void CollectGuardianQuestProgressAuthorityUpdatesForGuardian(
        string guardianId,
        JsonObject preTurnGuardian,
        JsonObject currentGuardian,
        List<JsonObject> updates)
    {
        if (currentGuardian["questManagement"] is not JsonObject currentQuestManagement ||
            currentQuestManagement["activeQuests"] is not JsonArray currentActiveQuests)
        {
            return;
        }

        if (preTurnGuardian["questManagement"] is not JsonObject preTurnQuestManagement ||
            preTurnQuestManagement["activeQuests"] is not JsonArray preTurnActiveQuests)
        {
            return;
        }

        foreach (var currentQuest in currentActiveQuests.OfType<JsonObject>())
        {
            var questId = GetNodeString(currentQuest["questId"]);
            if (string.IsNullOrWhiteSpace(questId))
                continue;

            var preTurnQuest = preTurnActiveQuests
                .OfType<JsonObject>()
                .FirstOrDefault(quest => string.Equals(GetNodeString(quest["questId"]), questId, StringComparison.OrdinalIgnoreCase));
            if (preTurnQuest == null)
                continue;

            if (!TryBuildGuardianQuestProgressAuthorityUpdate(guardianId, questId!, preTurnQuest, currentQuest, out var update))
                continue;

            if (update != null)
                updates.Add(update);
        }
    }

    private static bool TryBuildGuardianQuestProgressAuthorityUpdate(
        string guardianId,
        string questId,
        JsonObject preTurnQuest,
        JsonObject currentQuest,
        out JsonObject? update)
    {
        update = null;

        if (!IsAllowedMortalGuardianQuestProgressState(preTurnQuest, currentQuest))
            return false;

        var changed = false;
        var result = new JsonObject
        {
            ["guardianId"] = guardianId,
            ["questId"] = questId
        };

        foreach (var fieldName in GuardianQuestProgressMutableFields)
        {
            var hasPreTurnValue = preTurnQuest.TryGetPropertyValue(fieldName, out var preTurnValue);
            var hasCurrentValue = currentQuest.TryGetPropertyValue(fieldName, out var currentValue);
            if (hasPreTurnValue != hasCurrentValue ||
                (hasPreTurnValue && hasCurrentValue && !JsonNode.DeepEquals(preTurnValue, currentValue)))
            {
                changed = true;
            }

            if (hasCurrentValue)
                result[fieldName] = currentValue?.DeepClone();
        }

        if (!changed)
            return true;

        update = result;
        return true;
    }

    private static bool TryResetAllowedGuardianQuestProgressDeltas(JsonObject preTurnRoot, JsonObject currentRoot)
    {
        if (preTurnRoot["guardians"] is JsonArray preTurnGuardians &&
            currentRoot["guardians"] is JsonArray currentGuardians)
        {
            foreach (var currentGuardian in currentGuardians.OfType<JsonObject>())
            {
                var guardianId = GetNodeString(currentGuardian["guardianId"]);
                if (string.IsNullOrWhiteSpace(guardianId))
                    return false;

                var preTurnGuardian = preTurnGuardians
                    .OfType<JsonObject>()
                    .FirstOrDefault(guardian => string.Equals(GetNodeString(guardian["guardianId"]), guardianId, StringComparison.OrdinalIgnoreCase));
                if (preTurnGuardian == null ||
                    !TryResetAllowedGuardianQuestProgressDeltasForGuardian(preTurnGuardian, currentGuardian))
                {
                    return false;
                }
            }
        }

        if (currentRoot["activeGuardian"] is JsonObject currentActiveGuardian)
        {
            if (preTurnRoot["activeGuardian"] is not JsonObject preTurnActiveGuardian)
                return false;
            if (!TryResetAllowedGuardianQuestProgressDeltasForGuardian(preTurnActiveGuardian, currentActiveGuardian))
                return false;
        }

        return true;
    }

    private static bool TryResetAllowedGuardianQuestProgressDeltasForGuardian(JsonObject preTurnGuardian, JsonObject currentGuardian)
    {
        if (currentGuardian["questManagement"] is not JsonObject currentQuestManagement ||
            currentQuestManagement["activeQuests"] is not JsonArray currentActiveQuests)
        {
            return true;
        }

        if (preTurnGuardian["questManagement"] is not JsonObject preTurnQuestManagement ||
            preTurnQuestManagement["activeQuests"] is not JsonArray preTurnActiveQuests)
        {
            return false;
        }

        foreach (var currentQuest in currentActiveQuests.OfType<JsonObject>())
        {
            var questId = GetNodeString(currentQuest["questId"]);
            if (string.IsNullOrWhiteSpace(questId))
                return false;

            var preTurnQuest = preTurnActiveQuests
                .OfType<JsonObject>()
                .FirstOrDefault(quest => string.Equals(GetNodeString(quest["questId"]), questId, StringComparison.OrdinalIgnoreCase));
            if (preTurnQuest == null)
                return false;

            if (!IsAllowedMortalGuardianQuestProgressState(preTurnQuest, currentQuest))
                return false;

            foreach (var fieldName in GuardianQuestProgressMutableFields)
            {
                if (preTurnQuest.TryGetPropertyValue(fieldName, out var preTurnValue))
                    currentQuest[fieldName] = preTurnValue?.DeepClone();
                else
                    currentQuest.Remove(fieldName);
            }
        }

        return true;
    }

    private static readonly string[] GuardianQuestProgressMutableFields =
    {
        "status",
        "progressSummary",
        "objectiveState",
        "readyToTurnInEvidence",
        "turnInRequirement",
        "readyToTurnInAtTurn",
        "updatedAtTurn",
        "updatedAtUtc"
    };

    private static readonly string[] GuardianQuestProgressExplicitCommandFields =
    {
        "status",
        "progressSummary",
        "objectiveState",
        "readyToTurnInEvidence",
        "turnInRequirement"
    };

    private static bool IsAllowedMortalGuardianQuestProgressState(JsonObject preTurnQuest, JsonObject currentQuest)
    {
        var status = GetNodeString(currentQuest["status"]);
        if (string.IsNullOrWhiteSpace(status))
            return !HasGuardianQuestProgressMutableDelta(preTurnQuest, currentQuest);

        if (!GuardianProjectState.IsSupportedActiveQuestProgressStatus(status))
            return false;

        var evidenceNode = currentQuest["readyToTurnInEvidence"];
        if (evidenceNode != null && evidenceNode is not JsonObject)
            return false;
        var evidence = evidenceNode as JsonObject;
        if (evidence != null && GuardianProjectState.ContainsForbiddenQuestPhysicalEvidenceField(evidence))
            return false;

        if (!string.Equals(status, GuardianProjectState.QuestStatusReadyToTurnIn, StringComparison.OrdinalIgnoreCase))
            return true;

        if (evidence == null)
            return false;

        if (!GuardianQuestProgressEvidenceHasAllowedProof(evidence))
            return false;

        return true;
    }

    private static bool HasGuardianQuestProgressMutableDelta(JsonObject preTurnQuest, JsonObject currentQuest)
    {
        foreach (var fieldName in GuardianQuestProgressMutableFields)
        {
            var hasPreTurnValue = preTurnQuest.TryGetPropertyValue(fieldName, out var preTurnValue);
            var hasCurrentValue = currentQuest.TryGetPropertyValue(fieldName, out var currentValue);
            if (hasPreTurnValue != hasCurrentValue ||
                (hasPreTurnValue && hasCurrentValue && !JsonNode.DeepEquals(preTurnValue, currentValue)))
            {
                return true;
            }
        }

        return false;
    }

    private static bool GuardianQuestProgressEvidenceHasAllowedProof(JsonObject evidence)
    {
        foreach (var fieldName in new[] { "memoryImprint", "lifeEventEvidence", "itemEcho", "locationWitness", "craftedOutcome", "knowledgeTrace", "soulResonance" })
        {
            var node = evidence[fieldName];
            if (node is JsonObject or JsonArray)
                return true;
            if (node is JsonValue value && value.TryGetValue<string>(out var text) && !string.IsNullOrWhiteSpace(text))
                return true;
        }

        return false;
    }

}
