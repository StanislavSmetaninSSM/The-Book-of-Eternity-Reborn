using System.Diagnostics;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using System.Text.Json.Nodes;
using System.Text.Json.Serialization;
using BookOfEternityClient.Configuration;
using BookOfEternityClient.Models;
using BookOfEternityClient.Services;
using BookOfEternityClient.UI;
using Microsoft.Extensions.Logging;
using Spectre.Console;

namespace BookOfEternityClient.Core;

public partial class GameEngine
{
    private sealed record CoordinatedGameStateWrite(string RelativePath, string? PreviousJson, string NextJson);

    private Task WriteCanonicalSoulStateAsync(JsonObject root)
    {
        return _fs.WriteFileAtomicAsync(
            "game_state/meta/soul_state.json",
            GuardianPolicyContracts.CreateCanonicalSoulStateWriteRoot(root).ToJsonString(JsonOpts));
    }

    private Task WriteCanonicalSoulStateAsync(object payload)
    {
        var root = JsonSerializer.SerializeToNode(payload, JsonOpts) as JsonObject ?? new JsonObject();
        return WriteCanonicalSoulStateAsync(root);
    }

    private string BuildLifeSummary(string? playerSummary)
    {
        var state = _stateManager.CurrentState;
        var parts = new List<string>();

        if (!string.IsNullOrEmpty(state.CharacterName))
            parts.Add($"Имя: {state.CharacterName}");
        if (!string.IsNullOrEmpty(state.CharacterRace))
            parts.Add($"Раса: {state.CharacterRace}");
        if (!string.IsNullOrEmpty(state.CharacterClass))
            parts.Add($"Класс: {state.CharacterClass}");
        if (!string.IsNullOrEmpty(state.CurrentLocation))
            parts.Add($"Последнее местоположение: {state.CurrentLocation}");
        parts.Add($"Ходов прожито: {_gameLoop.TurnNumber}");

        if (!string.IsNullOrWhiteSpace(playerSummary))
            parts.Add($"Заметка игрока: {playerSummary}");

        return string.Join(". ", parts);
    }

    /// <summary>
    /// Updates the soul state realm and optionally appends a life entry to livesHistory.
    /// Eliminates code duplication across HandleEndOfLife, CheckLifeTransitions, HandleIncarnation.
    /// </summary>
    private async Task<bool> UpdateSoulStateRealm(string newRealm, string? lifeSummaryToAppend = null, bool incrementIncarnation = false)
    {
        var soulJson = await _fs.ReadFileAsync("game_state/meta/soul_state.json");
        if (string.IsNullOrWhiteSpace(soulJson))
        {
            _logger.LogWarning("Не удалось обновить soul_state.currentRealm до {NewRealm}: soul_state.json отсутствует или unreadable.", newRealm);
            return false;
        }

        try
        {
            using var doc = JsonDocument.Parse(soulJson);
            var root = doc.RootElement;

            // Reconstruct all existing properties
            var dict = new Dictionary<string, object?>();

            dict["soulName"] = root.TryGetProperty("soulName", out var sn) ? sn.GetString() : "";
            dict["soulFormDescription"] = root.TryGetProperty("soulFormDescription", out var sfd) ? sfd.GetString() : "";
            dict["previousSoulNames"] = root.TryGetProperty("previousSoulNames", out var previousSoulNames)
                ? JsonSerializer.Deserialize<object>(previousSoulNames.GetRawText())
                : Array.Empty<string>();
            dict["currentRealm"] = newRealm;
            var existingInc = root.TryGetProperty("currentIncarnation", out var inc) && inc.TryGetInt32(out var incVal) ? incVal : 0;
            dict["currentIncarnation"] = incrementIncarnation ? existingInc + 1 : existingInc;

            // Preserve complex objects
            dict["enlightenment"] = root.TryGetProperty("enlightenment", out var enl)
                ? JsonSerializer.Deserialize<object>(enl.GetRawText())
                : new { currentTier = "Новичок", experience = 0, level = 0 };
            dict["inkFeathers"] = root.TryGetProperty("inkFeathers", out var f)
                ? JsonSerializer.Deserialize<object>(f.GetRawText())
                : new { current = 0, total = 0 };
            dict["soulRelics"] = root.TryGetProperty("soulRelics", out var sr)
                ? JsonSerializer.Deserialize<object>(sr.GetRawText())
                : new { equipped = Array.Empty<object>(), stored = Array.Empty<object>() };

            // Handle livesHistory — optionally append a new life entry
            var existingHistory = new List<object>();
            if (root.TryGetProperty("livesHistory", out var lh) &&
                lh.ValueKind == JsonValueKind.Array)
            {
                foreach (var entry in lh.EnumerateArray())
                    existingHistory.Add(JsonSerializer.Deserialize<object>(entry.GetRawText())!);
            }

            if (!string.IsNullOrWhiteSpace(lifeSummaryToAppend))
            {
                var lifeEntry = new
                {
                    incarnation = dict["currentIncarnation"],
                    summary = lifeSummaryToAppend,
                    endedAt = DateTime.UtcNow.ToString("o"),
                    turnsLived = _gameLoop.TurnNumber
                };
                existingHistory.Add(lifeEntry);
            }

            dict["livesHistory"] = existingHistory;

            foreach (var prop in root.EnumerateObject())
            {
                if (!dict.ContainsKey(prop.Name))
                    dict[prop.Name] = JsonSerializer.Deserialize<object>(prop.Value.GetRawText());
            }

            await WriteCanonicalSoulStateAsync(dict);

            if (string.Equals(newRealm, "Chaos Sea", StringComparison.OrdinalIgnoreCase) &&
                !string.IsNullOrWhiteSpace(lifeSummaryToAppend))
            {
                _fs.ClearCurrentWorldLore();
                await _rivalSoulArcService.ResetForNewLifeAsync();
                await _guardianCorrectionService.ResetForAfterlifeAsync();
                await _scenarioCoreService.ClearAsync();
                _afterlifeArchiveCandidateService.Clear();
                await ResetGuardianGachaChargesForNewReturn();
            }

            return true;
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Ошибка обновления soul_state.json");
            return false;
        }
    }

    private async Task<bool> TryCommitCoordinatedGameStateWritesAsync(params CoordinatedGameStateWrite[] writes)
    {
        var completedWrites = new List<CoordinatedGameStateWrite>();
        try
        {
            foreach (var write in writes)
            {
                await _fs.WriteFileAtomicAsync(write.RelativePath, write.NextJson);
                completedWrites.Add(write);
            }

            return true;
        }
        catch (Exception ex)
        {
            for (var index = completedWrites.Count - 1; index >= 0; index--)
            {
                if (await TryRestoreJsonFileAsync(completedWrites[index].RelativePath, completedWrites[index].PreviousJson))
                    continue;

                throw new InvalidOperationException(
                    $"Не удалось безопасно откатить coordinated state write для {completedWrites[index].RelativePath}.",
                    ex);
            }

            _logger.LogWarning(ex, "Coordinated state write прерван до завершения всех write-paths. Уже записанные файлы откатились к предыдущим snapshot-версиям.");
            return false;
        }
    }

    private async Task<bool> TryRestoreJsonFileAsync(string relativePath, string? previousJson)
    {
        if (previousJson == null)
        {
            _fs.DeleteFile(relativePath);
            return true;
        }

        try
        {
            await _fs.WriteFileAtomicAsync(relativePath, previousJson);
            return true;
        }
        catch
        {
            return false;
        }
    }

    private async Task<string?> ApplyPendingMemoryLegacyForIncarnationAsync()
    {
        var soulJson = await _fs.ReadFileAsync("game_state/meta/soul_state.json");
        if (string.IsNullOrWhiteSpace(soulJson))
            return null;

        try
        {
            var root = JsonNode.Parse(soulJson) as JsonObject;
            var legacy = root?["pendingMemoryLegacy"] as JsonObject;
            if (legacy == null)
            {
                _pendingMemoryLegacyAwaitingConsumption = false;
                return null;
            }

            var legacyType = legacy["legacyType"]?.GetValue<string>() ?? string.Empty;
            var applicationState = legacy["applicationState"]?.GetValue<string>() ?? "pending";
            string? summary = null;

            if (string.Equals(applicationState, "applied-awaiting-turn-accept", StringComparison.OrdinalIgnoreCase))
            {
                var survives = await PendingMemoryLegacyEffectStillPresentAsync(legacy);
                summary = BuildPendingMemoryLegacySummary(legacy);
                if (!survives)
                {
                    legacy["applicationState"] = "pending";
                    legacy.Remove("applicationAudit");
                    await WriteCanonicalSoulStateAsync(root!);
                }
                else
                {
                    _pendingMemoryLegacyAwaitingConsumption = !string.IsNullOrWhiteSpace(summary);
                    return summary;
                }
            }

            if (string.Equals(legacyType, "startingCharacteristicBonus", StringComparison.OrdinalIgnoreCase))
            {
                var characteristic = legacy["characteristic"]?.GetValue<string>() ?? string.Empty;
                var bonus = legacy["bonus"]?.GetValue<int>() ?? 0;
                if (Characteristics.All.Contains(characteristic, StringComparer.OrdinalIgnoreCase) && bonus > 0)
                {
                    await ApplyMemoryLegacyCharacteristicBonusAsync(characteristic, bonus);
                    var statName = Characteristics.RussianNames.GetValueOrDefault(characteristic, characteristic);
                    summary = $"+{bonus} к характеристике «{statName}» в этой инкарнации";
                }
            }
            else if (string.Equals(legacyType, "startingPassiveKnowledgeSkill", StringComparison.OrdinalIgnoreCase))
            {
                await ApplyMemoryLegacyPassiveSkillAsync(legacy);
                var skillName = legacy["skillName"]?.GetValue<string>() ?? "Неизвестный навык";
                summary = $"получен пассивный навык «{skillName}» для этой инкарнации";
            }

            if (!string.IsNullOrWhiteSpace(summary))
            {
                legacy["applicationState"] = "applied-awaiting-turn-accept";
                await WriteCanonicalSoulStateAsync(root!);
            }

            _pendingMemoryLegacyAwaitingConsumption = !string.IsNullOrWhiteSpace(summary);
            return summary;
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Не удалось применить pendingMemoryLegacy при начале новой инкарнации");
            _pendingMemoryLegacyAwaitingConsumption = false;
            return null;
        }
    }

    private async Task ApplyMemoryLegacyCharacteristicBonusAsync(string characteristic, int bonus)
    {
        const string path = "game_state/misc/characteristics.json";
        var stats = new Dictionary<string, int>(StringComparer.OrdinalIgnoreCase);
        foreach (var name in Characteristics.All)
            stats[name] = 1;

        var json = await _fs.ReadFileAsync(path);
        if (!string.IsNullOrWhiteSpace(json))
        {
            try
            {
                using var doc = JsonDocument.Parse(json);
                foreach (var name in Characteristics.All)
                {
                    if (doc.RootElement.TryGetProperty(name, out var value) &&
                        value.ValueKind == JsonValueKind.Number &&
                        value.TryGetInt32(out var parsed))
                    {
                        stats[name] = parsed;
                    }
                }
            }
            catch (Exception ex)
            {
                _logger.LogWarning(ex, "Не удалось прочитать characteristics.json перед применением Наследия Памяти");
            }
        }

        stats[characteristic] = Math.Min(100, stats.GetValueOrDefault(characteristic, 1) + bonus);
        var payload = new Dictionary<string, object>(stats.Select(kv => new KeyValuePair<string, object>(kv.Key, kv.Value)));
        await _fs.WriteFileAtomicAsync(path, JsonSerializer.Serialize(payload, JsonOpts));
    }

    private async Task ApplyMemoryLegacyPassiveSkillAsync(JsonObject legacy)
    {
        const string path = "game_state/player/skills_passive.json";
        JsonObject root;

        var json = await _fs.ReadFileAsync(path);
        try
        {
            root = !string.IsNullOrWhiteSpace(json)
                ? JsonNode.Parse(json) as JsonObject ?? new JsonObject()
                : new JsonObject();
        }
        catch
        {
            root = new JsonObject();
        }

        var skills = root["passiveSkillChanges"] as JsonArray ?? new JsonArray();
        root["passiveSkillChanges"] = skills;

        var skillName = legacy["skillName"]?.GetValue<string>() ?? "Наследие Памяти";
        for (var i = skills.Count - 1; i >= 0; i--)
        {
            if (skills[i] is JsonObject existing &&
                string.Equals(existing["skillName"]?.GetValue<string>(), skillName, StringComparison.OrdinalIgnoreCase))
            {
                skills.RemoveAt(i);
            }
        }

        var skill = new JsonObject
        {
            ["skillName"] = skillName,
            ["skillDescription"] = legacy["skillDescription"]?.GetValue<string>() ?? "",
            ["rarity"] = legacy["rarity"]?.GetValue<string>() ?? "Uncommon",
            ["type"] = legacy["type"]?.GetValue<string>() ?? "MemoryLegacy",
            ["group"] = legacy["group"]?.GetValue<string>() ?? "Knowledge",
            ["playerStatBonus"] = legacy["playerStatBonus"]?.GetValue<string>() ?? "",
            ["masteryLevel"] = legacy["masteryLevel"]?.GetValue<int>() ?? 1,
            ["maxMasteryLevel"] = legacy["maxMasteryLevel"]?.GetValue<int>() ?? 1,
            ["structuredBonuses"] = legacy["structuredBonuses"]?.DeepClone() ?? new JsonArray()
        };

        skills.Add(skill);
        await _fs.WriteFileAtomicAsync(path, root.ToJsonString(JsonOpts));
    }

    private async Task CapturePendingMemoryLegacyApplicationAuditAsync()
    {
        try
        {
            var soulJson = await _fs.ReadFileAsync("game_state/meta/soul_state.json");
            if (string.IsNullOrWhiteSpace(soulJson))
                return;

            var root = JsonNode.Parse(soulJson) as JsonObject;
            var legacy = root?["pendingMemoryLegacy"] as JsonObject;
            if (legacy == null)
                return;

            var legacyType = legacy["legacyType"]?.GetValue<string>() ?? string.Empty;
            var audit = legacy["applicationAudit"] as JsonObject ?? new JsonObject();

            if (string.Equals(legacyType, "startingCharacteristicBonus", StringComparison.OrdinalIgnoreCase))
            {
                var characteristic = legacy["characteristic"]?.GetValue<string>() ?? string.Empty;
                if (!string.IsNullOrWhiteSpace(characteristic))
                {
                    var currentValue = await ReadCurrentCharacteristicValueAsync(characteristic);
                    if (currentValue.HasValue)
                        audit["expectedCharacteristicValue"] = currentValue.Value;
                }
            }
            else if (string.Equals(legacyType, "startingPassiveKnowledgeSkill", StringComparison.OrdinalIgnoreCase))
            {
                var skillName = legacy["skillName"]?.GetValue<string>() ?? string.Empty;
                if (!string.IsNullOrWhiteSpace(skillName))
                {
                    audit["expectedPassiveSkillName"] = skillName;
                    audit["expectedGroup"] = legacy["group"]?.GetValue<string>() ?? "Knowledge";
                    audit["expectedPlayerStatBonus"] = legacy["playerStatBonus"]?.GetValue<string>() ?? "";
                    if (legacy["structuredBonuses"] is JsonArray bonusArr)
                    {
                        audit["expectedStructuredBonusesCount"] = bonusArr.Count;
                        audit["expectedStructuredBonusesCanonical"] = StructuredBonusCanonicalizer.Canonicalize(bonusArr);
                    }
                }
            }

            if (audit.Count > 0)
            {
                legacy["applicationAudit"] = audit;
                await WriteCanonicalSoulStateAsync(root!);
            }
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Не удалось записать applicationAudit для pendingMemoryLegacy");
        }
    }

    private async Task<int?> ReadCurrentCharacteristicValueAsync(string characteristic)
    {
        var json = await _fs.ReadFileAsync("game_state/misc/characteristics.json");
        if (string.IsNullOrWhiteSpace(json))
            return null;

        try
        {
            using var doc = JsonDocument.Parse(json);
            if (doc.RootElement.TryGetProperty(characteristic, out var value) &&
                value.ValueKind == JsonValueKind.Number &&
                value.TryGetInt32(out var parsed))
            {
                return parsed;
            }
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Не удалось прочитать characteristics.json для проверки Наследия Памяти");
        }

        return null;
    }

    private static string? BuildPendingMemoryLegacySummary(JsonObject legacy)
    {
        var legacyType = legacy["legacyType"]?.GetValue<string>() ?? string.Empty;
        if (string.Equals(legacyType, "startingCharacteristicBonus", StringComparison.OrdinalIgnoreCase))
        {
            var characteristic = legacy["characteristic"]?.GetValue<string>() ?? string.Empty;
            var bonus = legacy["bonus"]?.GetValue<int>() ?? 0;
            if (string.IsNullOrWhiteSpace(characteristic) || bonus <= 0)
                return null;

            var statName = Characteristics.RussianNames.GetValueOrDefault(characteristic, characteristic);
            return $"+{bonus} к характеристике «{statName}» в этой инкарнации";
        }

        if (string.Equals(legacyType, "startingPassiveKnowledgeSkill", StringComparison.OrdinalIgnoreCase))
        {
            var skillName = legacy["skillName"]?.GetValue<string>() ?? string.Empty;
            if (string.IsNullOrWhiteSpace(skillName))
                return null;

            return $"получен пассивный навык «{skillName}» для этой инкарнации";
        }

        return null;
    }

    private async Task<bool> PendingMemoryLegacyEffectStillPresentAsync(JsonObject legacy)
    {
        var legacyType = legacy["legacyType"]?.GetValue<string>() ?? string.Empty;
        var audit = legacy["applicationAudit"] as JsonObject;
        if (audit == null)
            return false;

        if (string.Equals(legacyType, "startingCharacteristicBonus", StringComparison.OrdinalIgnoreCase))
        {
            var characteristic = legacy["characteristic"]?.GetValue<string>() ?? string.Empty;
            var expectedValue = audit["expectedCharacteristicValue"]?.GetValue<int?>() ?? null;
            if (string.IsNullOrWhiteSpace(characteristic) || !expectedValue.HasValue)
                return false;

            var currentValue = await ReadCurrentCharacteristicValueAsync(characteristic);
            return currentValue.HasValue && currentValue.Value >= expectedValue.Value;
        }

        if (string.Equals(legacyType, "startingPassiveKnowledgeSkill", StringComparison.OrdinalIgnoreCase))
        {
            var expectedSkillName = audit["expectedPassiveSkillName"]?.GetValue<string>() ?? string.Empty;
            var expectedGroup = audit["expectedGroup"]?.GetValue<string>() ?? "Knowledge";
            var expectedPlayerStatBonus = audit["expectedPlayerStatBonus"]?.GetValue<string>() ?? string.Empty;
            var expectedStructuredBonusesCount = audit["expectedStructuredBonusesCount"]?.GetValue<int?>() ?? null;
            var expectedStructuredBonusesCanonical = audit["expectedStructuredBonusesCanonical"]?.GetValue<string>() ?? string.Empty;
            return await PassiveSkillMatchesExpectedShapeAsync(expectedSkillName, expectedGroup, expectedPlayerStatBonus, expectedStructuredBonusesCount, expectedStructuredBonusesCanonical);
        }

        return false;
    }

    private async Task<bool> PassiveSkillMatchesExpectedShapeAsync(
        string skillName,
        string expectedGroup,
        string expectedPlayerStatBonus,
        int? expectedStructuredBonusesCount,
        string expectedStructuredBonusesCanonical)
    {
        if (string.IsNullOrWhiteSpace(skillName))
            return false;

        var json = await _fs.ReadFileAsync("game_state/player/skills_passive.json");
        if (string.IsNullOrWhiteSpace(json))
            return false;

        try
        {
            var root = JsonNode.Parse(json) as JsonObject;
            var skills = root?["passiveSkillChanges"] as JsonArray;
            if (skills == null)
                return false;

            foreach (var item in skills.OfType<JsonObject>())
            {
                if (!string.Equals(item["skillName"]?.GetValue<string>(), skillName, StringComparison.OrdinalIgnoreCase))
                    continue;

                if (!string.Equals(item["group"]?.GetValue<string>(), expectedGroup, StringComparison.OrdinalIgnoreCase))
                    return false;

                var playerStatBonus = item["playerStatBonus"]?.GetValue<string>() ?? string.Empty;
                if (string.IsNullOrWhiteSpace(playerStatBonus) ||
                    !string.Equals(playerStatBonus, expectedPlayerStatBonus, StringComparison.Ordinal))
                    return false;

                var structuredBonuses = item["structuredBonuses"] as JsonArray;
                if (structuredBonuses == null || structuredBonuses.Count == 0)
                    return false;

                if (expectedStructuredBonusesCount.HasValue && structuredBonuses.Count < expectedStructuredBonusesCount.Value)
                    return false;

                if (!string.IsNullOrWhiteSpace(expectedStructuredBonusesCanonical) &&
                    !string.Equals(StructuredBonusCanonicalizer.Canonicalize(structuredBonuses), expectedStructuredBonusesCanonical, StringComparison.Ordinal))
                    return false;

                return true;
            }
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Не удалось проверить форму пассивного навыка для Наследия Памяти");
        }

        return false;
    }

    private async Task FinalizePendingMemoryLegacyConsumptionAsync()
    {
        try
        {
            var soulJson = await _fs.ReadFileAsync("game_state/meta/soul_state.json");
            if (string.IsNullOrWhiteSpace(soulJson))
            {
                _pendingMemoryLegacyAwaitingConsumption = false;
                return;
            }

            var root = JsonNode.Parse(soulJson) as JsonObject;
            if (root == null)
            {
                _pendingMemoryLegacyAwaitingConsumption = false;
                return;
            }

            if (root["pendingMemoryLegacy"] is not JsonObject legacy)
            {
                _pendingMemoryLegacyAwaitingConsumption = false;
                return;
            }

            var applicationState = legacy["applicationState"]?.GetValue<string>() ?? string.Empty;
            if (!string.Equals(applicationState, "applied-awaiting-turn-accept", StringComparison.OrdinalIgnoreCase))
            {
                _pendingMemoryLegacyAwaitingConsumption = false;
                return;
            }

            root["pendingMemoryLegacy"] = null;
            await WriteCanonicalSoulStateAsync(root);
            await RefreshRuntimeStateAsync();
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Не удалось очистить pendingMemoryLegacy после успешного воплощения");
        }
        finally
        {
            _pendingMemoryLegacyAwaitingConsumption = false;
        }
    }

    private async Task<string?> ConsumePendingShiningMemorySelectionAsync()
    {
        var pendingSelection = await ShiningBlessingEffectState.ReadPendingMemorySelectionAsync(_fs);
        if (pendingSelection == null)
            return null;

        var candidates = pendingSelection.Candidates;
        if (candidates.Count == 0)
        {
            await ShiningBlessingEffectState.ConsumePendingMemorySelectionAsync(_fs, _gameLoop.TurnNumber, null, 0);
            return null;
        }

        var displayCount = Math.Min(candidates.Count, Math.Max(2, 2 + pendingSelection.Options));
        var rerollsRemaining = pendingSelection.Rerolls;
        var rerollsSpent = 0;
        var batchStart = 0;

        while (true)
        {
            var shownCandidates = BuildMemorySelectionWindow(candidates, batchStart, displayCount);
            SpectreConsoleSafe.Clear();
            var lines = new List<string>
            {
                "[bold gold1]🧠 Эхо-память Сияющей Обители[/]",
                $"[dim]Дополнительные варианты: {pendingSelection.Options} • memory rerolls: {rerollsRemaining}[/]",
                "",
                "[dim]Выберите один memory echo для старта этой жизни. Это отдельный blessing-step и он не связан с draft Врат.[/]"
            };
            for (var index = 0; index < shownCandidates.Count; index++)
            {
                var candidate = shownCandidates[index];
                lines.Add($"  {index + 1}. [white]Инкарнация #{candidate.Incarnation}[/] — {Markup.Escape(TrimMemorySelectionText(candidate.Summary, 120))}");
            }

            AnsiConsole.Write(new Panel(GameInterface.SafeMarkup(string.Join("\n", lines)))
            {
                Header = new PanelHeader(" Память следующей жизни ", Justify.Center),
                Border = BoxBorder.Rounded,
                BorderStyle = new Style(Color.Gold1),
                Padding = new Padding(1, 1),
                Expand = true
            });

            var choiceMap = new Dictionary<string, ShiningBlessingEffectState.MemoryEchoCandidate>(StringComparer.Ordinal);
            var choices = new List<string>();
            for (var index = 0; index < shownCandidates.Count; index++)
            {
                var candidate = shownCandidates[index];
                var label = $"{index + 1}. Инкарнация #{candidate.Incarnation} — {TrimMemorySelectionText(candidate.Summary, 70)}";
                choices.Add(label);
                choiceMap[label] = candidate;
            }

            var canReroll = rerollsRemaining > 0 && candidates.Count > shownCandidates.Count;
            if (canReroll)
                choices.Add($"🔄 Сменить набор эхо-памяти ({rerollsRemaining})");
            choices.Add("⏭ Пропустить выбор");

            var choice = AnsiConsole.Prompt(new SelectionPrompt<string>()
                .Title("[bold yellow]Выберите memory echo[/]")
                .HighlightStyle(new Style(Color.Gold1))
                .PageSize(10)
                .AddChoices(choices));

            if (choiceMap.TryGetValue(choice, out var selectedCandidate))
            {
                await ShiningBlessingEffectState.ConsumePendingMemorySelectionAsync(
                    _fs,
                    _gameLoop.TurnNumber,
                    selectedCandidate,
                    rerollsSpent);
                await RefreshRuntimeStateAsync();
                AnsiConsole.MarkupLine($"[gold1]🧠 Эхо-память:[/] {Markup.Escape(selectedCandidate.Summary)}");
                AnsiConsole.WriteLine();
                return $"инкарнация #{selectedCandidate.Incarnation}: {selectedCandidate.Summary}";
            }

            if (canReroll && choice.Contains("Сменить набор", StringComparison.OrdinalIgnoreCase))
            {
                rerollsRemaining = Math.Max(0, rerollsRemaining - 1);
                rerollsSpent += 1;
                batchStart = (batchStart + shownCandidates.Count) % candidates.Count;
                continue;
            }

            await ShiningBlessingEffectState.ConsumePendingMemorySelectionAsync(
                _fs,
                _gameLoop.TurnNumber,
                null,
                rerollsSpent);
            await RefreshRuntimeStateAsync();
            return null;
        }
    }

    private static List<ShiningBlessingEffectState.MemoryEchoCandidate> BuildMemorySelectionWindow(
        IReadOnlyList<ShiningBlessingEffectState.MemoryEchoCandidate> candidates,
        int batchStart,
        int displayCount)
    {
        var result = new List<ShiningBlessingEffectState.MemoryEchoCandidate>();
        if (candidates.Count == 0 || displayCount <= 0)
            return result;

        var seenHints = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        for (var offset = 0; offset < candidates.Count && result.Count < displayCount; offset++)
        {
            var candidate = candidates[(batchStart + offset) % candidates.Count];
            if (!seenHints.Add(candidate.LifeHint))
                continue;

            result.Add(candidate);
        }

        return result;
    }

    private static string TrimMemorySelectionText(string value, int maxLength)
    {
        if (string.IsNullOrWhiteSpace(value) || value.Length <= maxLength)
            return value;

        return value[..Math.Max(0, maxLength - 1)] + "…";
    }

    private async Task<JsonObject?> TryReadPreparedShiningPackageAsync()
    {
        var shiningJson = await _fs.ReadFileAsync(ShiningAbodeState.StatePath);
        if (string.IsNullOrWhiteSpace(shiningJson))
            return null;

        try
        {
            var root = JsonNode.Parse(shiningJson) as JsonObject;
            if (root?["preparedIncarnationPackage"] is not JsonObject preparedPackage)
                return null;

            var validationError = ShiningAbodeState.ValidatePreparedIncarnationPackageForBootstrap(preparedPackage);
            if (!string.IsNullOrWhiteSpace(validationError))
            {
                _logger.LogWarning("Shining pending-bootstrap handoff blocked by invalid preparedIncarnationPackage: {ValidationError}", validationError);
                return null;
            }

            return preparedPackage;
        }
        catch
        {
            return null;
        }
    }

    private async Task ApplyPreparedShiningPackageToPendingWorldSetupAsync(JsonObject preparedPackage)
    {
        var selectedCards = preparedPackage["selectedCards"] as JsonArray;
        if (selectedCards == null || selectedCards.Count == 0)
            return;

        var pendingSetup = await _worldDirectiveService.ReadPendingSetupAsync() ?? new WorldDirectiveService.PendingWorldSetup
        {
            Mode = "manual",
            WorldDirectives = new WorldDirectiveService.WorldDirectives()
        };
        if (string.Equals(pendingSetup.Mode, "profile", StringComparison.OrdinalIgnoreCase))
            pendingSetup.Mode = "mixed";

        const string blessingPrefix = "Shining blessing: ";
        const string blessingEffectPrefix = "Shining blessing effect:";
        const string packageNotePrefix = "Frozen Shining package:";
        pendingSetup.WorldDirectives.PlayerAmendments.RemoveAll(item => item.StartsWith(blessingPrefix, StringComparison.OrdinalIgnoreCase));
        pendingSetup.WorldDirectives.ContinuityNotes.RemoveAll(item => item.StartsWith(packageNotePrefix, StringComparison.OrdinalIgnoreCase));
        pendingSetup.WorldDirectives.ContinuityNotes.RemoveAll(item => item.StartsWith(blessingEffectPrefix, StringComparison.OrdinalIgnoreCase));

        foreach (var card in selectedCards.OfType<JsonObject>())
        {
            var displayName = card["displayName"]?.GetValue<string>() ?? card["cardId"]?.GetValue<string>() ?? "unknown_card";
            var displaySummary = card["displaySummary"]?.GetValue<string>() ?? "";
            pendingSetup.WorldDirectives.PlayerAmendments.Add($"{blessingPrefix}{displayName} — {displaySummary}".TrimEnd(' ', '—'));
        }

        foreach (var line in ShiningBlessingEffectState.BuildPendingWorldDirectiveLines(preparedPackage))
            pendingSetup.WorldDirectives.ContinuityNotes.Add(line);

        pendingSetup.WorldDirectives.ContinuityNotes.Add(
            $"{packageNotePrefix} use exactly these {selectedCards.Count} blessing card(s) as next-life bootstrap input; do not reconstruct them from mutable Shining faction state.");

        await _worldDirectiveService.WritePendingSetupAsync(pendingSetup);
    }

    private async Task ClearPreparedShiningPackageAfterBootstrapAsync()
    {
        var shiningJson = await _fs.ReadFileAsync(ShiningAbodeState.StatePath);
        if (string.IsNullOrWhiteSpace(shiningJson))
            return;

        try
        {
            var root = JsonNode.Parse(shiningJson) as JsonObject;
            if (root == null || root["preparedIncarnationPackage"] == null)
                return;

            root["preparedIncarnationPackage"] = null;
            await _fs.WriteFileAtomicAsync(ShiningAbodeState.StatePath, root.ToJsonString(JsonOpts));
            await RefreshRuntimeStateAsync();
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Не удалось очистить preparedIncarnationPackage после успешного mortal bootstrap");
        }
    }

    private async Task RepairStalePreparedShiningPackageAfterMortalBootstrapAsync(
        PendingTurnSnapshotResolution pendingSnapshot,
        bool hasReadySignals)
    {
        if (!RealmSemantics.IsMortalRealm(_stateManager.CurrentState.CurrentRealm))
            return;

        if (hasReadySignals || pendingSnapshot.Status == PendingTurnSnapshotResolutionStatus.Usable)
            return;

        if (_fs.FileExists("input/turn_request.json") ||
            _fs.FileExists("game_state/control/incarnation_trigger.json"))
        {
            return;
        }

        var shiningJson = await _fs.ReadFileAsync(ShiningAbodeState.StatePath);
        if (string.IsNullOrWhiteSpace(shiningJson))
            return;

        try
        {
            var root = JsonNode.Parse(shiningJson) as JsonObject;
            if (root?["preparedIncarnationPackage"] is not JsonObject preparedPackage)
                return;

            var validationError = ShiningAbodeState.ValidatePreparedIncarnationPackageForBootstrap(preparedPackage);
            if (!string.IsNullOrWhiteSpace(validationError))
                return;

            root["preparedIncarnationPackage"] = null;
            await _fs.WriteFileAtomicAsync(ShiningAbodeState.StatePath, root.ToJsonString(JsonOpts));
            _logger.LogInformation("Cleared stale Shining preparedIncarnationPackage after confirmed Mortal World bootstrap.");
            await RefreshRuntimeStateAsync();
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Не удалось проверить stale preparedIncarnationPackage after Mortal World bootstrap");
        }
    }

    private async Task<bool> HasPendingMemoryLegacyAwaitingConsumptionAsync()
    {
        var soulJson = await _fs.ReadFileAsync("game_state/meta/soul_state.json");
        if (string.IsNullOrWhiteSpace(soulJson))
            return false;

        try
        {
            var root = JsonNode.Parse(soulJson) as JsonObject;
            var legacy = root?["pendingMemoryLegacy"] as JsonObject;
            var applicationState = legacy?["applicationState"]?.GetValue<string>() ?? string.Empty;
            return string.Equals(applicationState, "applied-awaiting-turn-accept", StringComparison.OrdinalIgnoreCase);
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Не удалось проверить applicationState pendingMemoryLegacy");
            return false;
        }
    }

    private async Task ResetGuardianGachaChargesForNewReturn()
    {
        var guardiansJson = await _fs.ReadFileAsync("game_state/meta/guardians.json");
        if (string.IsNullOrWhiteSpace(guardiansJson))
            return;

        try
        {
            if (JsonNode.Parse(guardiansJson) is not JsonObject root)
                return;

            var changed = false;

            void ResetGuardian(JsonObject guardian)
            {
                var existingGachaSystem = guardian["gachaSystem"] as JsonObject;
                var hadChargesPerReturn = existingGachaSystem?["chargesPerReturn"] != null;
                var hadChargesUsedThisReturn = existingGachaSystem?["chargesUsedThisReturn"] != null;
                var hadReadableChargesPerReturn = TryReadInt(existingGachaSystem?["chargesPerReturn"], out var previousChargesPerReturn);
                var (chargesPerReturn, currentUsedCharges) = GuardianGachaChargeRules.NormalizeGuardianGachaState(guardian);
                var gachaSystem = guardian["gachaSystem"] as JsonObject ?? new JsonObject();
                if (!hadChargesPerReturn || !hadChargesUsedThisReturn)
                    changed = true;
                if (currentUsedCharges != 0)
                    changed = true;
                if (!hadReadableChargesPerReturn || previousChargesPerReturn != chargesPerReturn)
                {
                    changed = true;
                }

                gachaSystem["chargesPerReturn"] = chargesPerReturn;
                gachaSystem["chargesUsedThisReturn"] = 0;
                guardian["gachaSystem"] = gachaSystem;
            }

            if (root["guardians"] is JsonArray guardians)
            {
                foreach (var guardian in guardians.OfType<JsonObject>())
                    ResetGuardian(guardian);
            }

            if (root["activeGuardian"] is JsonObject activeGuardian)
                ResetGuardian(activeGuardian);

            if (changed)
            {
                await _fs.WriteFileAtomicAsync("game_state/meta/guardians.json",
                    root.ToJsonString(JsonOpts));
            }
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Ошибка сброса guardian gacha charges после возвращения в Море Хаоса");
        }
    }

    private static bool TryReadInt(JsonNode? node, out int value)
    {
        value = 0;
        if (node == null)
            return false;

        try
        {
            value = node.GetValue<int>();
            return true;
        }
        catch
        {
            return false;
        }
    }

    /// <summary>
    /// Waits for the GM to respond (reused for incarnation/end-of-life transitions).
    /// Waits indefinitely — only Escape cancels. No hard timeout.
    /// </summary>

    private async Task ConsumeAfterlifeReturnProtectionIfNeededAsync(ValidatedPendingTurnSnapshotContext? snapshotContext)
    {
        if (!string.Equals(snapshotContext?.SourceLabel, OrdinaryPlayerTurnSourceLabel, StringComparison.OrdinalIgnoreCase))
            return;

        if (!_stateManager.CurrentState.IsInAfterlifeRealm ||
            _stateManager.CurrentState.IsInShiningAbodePendingBootstrap)
            return;

        if (!WasPreTurnAfterlifeRealm(snapshotContext))
            return;

        await _afterlifeReturnGuardService.ConsumeAfterAcceptedAfterlifeTurnAsync(_gameLoop.TurnNumber);
    }

    private bool WasPreTurnAfterlifeRealm(ValidatedPendingTurnSnapshotContext? snapshotContext)
    {
        var preTurnSoulJson = ReadPreTurnSnapshotFile(snapshotContext, "game_state/meta/soul_state.json");
        if (string.IsNullOrWhiteSpace(preTurnSoulJson))
            return false;

        try
        {
            if (JsonNode.Parse(preTurnSoulJson) is not JsonObject preTurnSoulRoot)
                return false;

            var preTurnRealm = preTurnSoulRoot["currentRealm"]?.GetValue<string>();
            return RealmSemantics.IsAfterlifeRealm(preTurnRealm);
        }
        catch
        {
            return false;
        }
    }

    /// <summary>
     /// Interactive stat distribution UI. Shows all 12 characteristics and lets the player
    /// allocate available points. Used at incarnation (8 pts) and level-up (5 pts).
    /// </summary>
}
