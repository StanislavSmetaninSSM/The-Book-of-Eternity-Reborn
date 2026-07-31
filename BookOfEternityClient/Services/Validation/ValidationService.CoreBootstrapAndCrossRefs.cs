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
    private async Task ValidateCrossReferences(List<ValidationIssue> issues)
    {
        await ValidateGuardianCrossReferencesAsync(issues);

        var knownLocationIds = await ReadKnownLocationIdsAsync();
        await ValidateLocationCrossReferencesAsync(issues, knownLocationIds);
        await ValidateWeatherContextHintsAsync(issues);
        await ValidateNpcLocationCrossReferencesAsync(issues, knownLocationIds);

        var knownNpcReferences = await ReadKnownNpcReferencesAsync();
        var knownGuardianReferences = await ReadKnownGuardianReferencesAsync();
        await ValidateGuardianNpcBoundaryAsync(issues, knownGuardianReferences);
        await ValidateNpcCommandCrossReferencesAsync(issues, knownNpcReferences, knownGuardianReferences);

        var knownInventoryItemReferences = await ReadKnownInventoryItemReferencesAsync();
        var knownNpcInventoryItemReferences = await ReadKnownNpcInventoryItemReferencesAsync();
        var knownNpcInventoryItemReferencesByNpc = await ReadKnownNpcInventoryItemReferencesByNpcAsync();
        var knownNpcInventoryContainerIdsByNpc = await ReadKnownNpcInventoryContainerIdsByNpcAsync();
        await ValidateInventoryItemSidecarCrossReferencesAsync(issues, knownInventoryItemReferences, knownNpcInventoryItemReferences);
        await ValidatePlayerInventoryCrossReferencesAsync(issues, knownInventoryItemReferences);
        await ValidateReadableInventoryDocumentAuthorityAsync(issues);
        await ValidateQuestRewardAuthorityAsync(issues);
        await ValidateNpcInventoryCrossReferencesAsync(issues, knownNpcReferences, knownNpcInventoryItemReferencesByNpc, knownNpcInventoryContainerIdsByNpc);

        await ValidateNpcQuestCrossReferencesAsync(issues, knownNpcReferences);

        var knownGuardianIds = await ReadKnownGuardianIdsAsync();
        await ValidateSoulQuestGuardianCrossReferencesAsync(issues, knownGuardianIds);
        var knownSystemGuardianPresetIds = await ReadKnownSystemGuardianPresetIdsAsync();
        await ValidateRivalSoulArcCrossReferencesAsync(issues, knownGuardianIds, knownSystemGuardianPresetIds);
        await ValidateResidentCrossReferencesWhenRivalArcPassSkippedAsync(issues);
        await ValidateActorJournalCrossReferencesAsync(issues);

        var knownFactionIds = await ReadKnownFactionIdsAsync();
        await ValidateFactionReferenceCrossReferencesAsync(issues, knownFactionIds, knownLocationIds);

        var knownCodexEntryIds = await ReadKnownCodexEntryIdsAsync();
        await ValidateCodexRelatedEntryCrossReferencesAsync(issues, knownCodexEntryIds);
        await ValidateCodexUpdateTargetCrossReferencesAsync(issues);

        var knownWorldStateFlagIds = await ReadKnownWorldStateFlagIdsAsync();
        await ValidateWorldStateFlagCrossReferencesAsync(issues, knownWorldStateFlagIds);

        var knownVehicleIds = await ReadKnownVehicleIdsAsync();
        await ValidateVehicleCrossReferencesAsync(issues, knownVehicleIds, knownLocationIds);
    }

    private async Task ValidateRivalAndResidentCrossReferencesAsync(
        List<ValidationIssue> issues)
    {
        var knownGuardianIds = await ReadKnownGuardianIdsAsync();
        var knownSystemGuardianPresetIds = await ReadKnownSystemGuardianPresetIdsAsync();
        await ValidateRivalSoulArcCrossReferencesAsync(
            issues,
            knownGuardianIds,
            knownSystemGuardianPresetIds);
        await ValidateResidentCrossReferencesWhenRivalArcPassSkippedAsync(issues);
    }


    private async Task ValidateSoulStateConsistency(List<ValidationIssue> issues)
    {
        var soulJson = await _fs.ReadFileAsync("game_state/meta/soul_state.json");
        if (soulJson == null) return;

        try
        {
            using var doc = JsonDocument.Parse(soulJson);
            var root = doc.RootElement;
            var currentSoulName = GetFirstNonEmptyString(root, "soulName") ?? string.Empty;

            // Incarnation number should be >= 0
            if (root.TryGetProperty("currentIncarnation", out var inc))
            {
                var val = inc.GetInt32();
                if (val < 0)
                {
                    issues.Add(new ValidationIssue(
                        "game_state/meta/soul_state.json", IssueSeverity.Error,
                        $"currentIncarnation = {val} (отрицательное значение)"));
                }
            }

            // Ink feathers should be >= 0
            if (root.TryGetProperty("inkFeathers", out var feathers) &&
                feathers.TryGetProperty("current", out var current))
            {
                var val = current.GetInt32();
                if (val < 0)
                {
                    issues.Add(new ValidationIssue(
                        "game_state/meta/soul_state.json", IssueSeverity.Warning,
                        $"inkFeathers.current = {val} (отрицательное значение)"));
                }
            }

            // Lives history should be an array
            if (root.TryGetProperty("livesHistory", out var lh) &&
                lh.ValueKind != JsonValueKind.Array)
            {
                issues.Add(new ValidationIssue(
                    "game_state/meta/soul_state.json", IssueSeverity.Warning,
                    "livesHistory должен быть массивом"));
            }

            ValidateCurrentSoulRelicsCanonicalShape(root, issues);

            if (root.TryGetProperty("soulFormDescription", out var soulFormDescription))
            {
                if (soulFormDescription.ValueKind != JsonValueKind.String)
                {
                    issues.Add(new ValidationIssue(
                        "game_state/meta/soul_state.json.soulFormDescription",
                        IssueSeverity.Error,
                        "soulFormDescription должен быть строковым описанием формы души",
                        code: "soul_form_description_invalid_shape",
                        section: "SoulState",
                        expected: "non-empty string",
                        actual: soulFormDescription.ValueKind.ToString(),
                        repairHint: "Храни форму души как player-authored строку, а не объект, массив или число."));
                }
                else if (string.IsNullOrWhiteSpace(soulFormDescription.GetString()))
                {
                    issues.Add(new ValidationIssue(
                        "game_state/meta/soul_state.json.soulFormDescription",
                        IssueSeverity.Warning,
                        "soulFormDescription не должен быть пустым, если поле присутствует",
                        code: "soul_form_description_empty",
                        section: "SoulState",
                        repairHint: "Либо удали пустое поле, либо заполни его описанием формы души игрока."));
                }
            }

            if (root.TryGetProperty("previousSoulNames", out var previousSoulNames))
            {
                if (previousSoulNames.ValueKind != JsonValueKind.Array)
                {
                    issues.Add(new ValidationIssue(
                        "game_state/meta/soul_state.json.previousSoulNames",
                        IssueSeverity.Error,
                        "previousSoulNames должен быть массивом строк",
                        code: "soul_previous_names_invalid_shape",
                        section: "SoulState",
                        expected: "array of strings",
                        actual: previousSoulNames.ValueKind.ToString(),
                        repairHint: "Сохраняй previousSoulNames как canonical string[] без вложенных объектов."));
                }
                else
                {
                    var seen = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
                    var index = 0;
                    foreach (var entry in previousSoulNames.EnumerateArray())
                    {
                        var entryPath = $"game_state/meta/soul_state.json.previousSoulNames[{index++}]";
                        if (entry.ValueKind != JsonValueKind.String)
                        {
                            issues.Add(new ValidationIssue(
                                entryPath,
                                IssueSeverity.Error,
                                "Каждый элемент previousSoulNames должен быть строкой",
                                code: "soul_previous_names_invalid_entry",
                                section: "SoulState",
                                expected: "string",
                                actual: entry.ValueKind.ToString(),
                                repairHint: "Храни в previousSoulNames только непустые строковые имена души."));
                            continue;
                        }

                        var name = entry.GetString()?.Trim();
                        if (string.IsNullOrWhiteSpace(name))
                        {
                            issues.Add(new ValidationIssue(
                                entryPath,
                                IssueSeverity.Warning,
                                "previousSoulNames не должен содержать пустые имена",
                                code: "soul_previous_names_empty_entry",
                                section: "SoulState",
                                repairHint: "Удали пустые строки из previousSoulNames."));
                            continue;
                        }

                        if (!seen.Add(name))
                        {
                            issues.Add(new ValidationIssue(
                                entryPath,
                                IssueSeverity.Warning,
                                "previousSoulNames не должен содержать дубликаты",
                                code: "soul_previous_names_duplicate_entry",
                                section: "SoulState",
                                actual: name,
                                repairHint: "Оставляй каждое прежнее имя души только один раз."));
                        }

                        if (!string.IsNullOrWhiteSpace(currentSoulName) &&
                            string.Equals(name, currentSoulName, StringComparison.OrdinalIgnoreCase))
                        {
                            issues.Add(new ValidationIssue(
                                entryPath,
                                IssueSeverity.Warning,
                                "previousSoulNames не должен содержать текущее имя души",
                                code: "soul_previous_names_contains_current_name",
                                section: "SoulState",
                                actual: name,
                                repairHint: "Список previousSoulNames должен хранить только прежние, а не текущее имя души."));
                        }
                    }
                }
            }
        }
        catch (Exception ex)
        {
            _logger.LogDebug(ex, "Не удалось проверить согласованность soul_state.");
        }
    }

    private void ValidateCurrentSoulRelicsCanonicalShape(JsonElement root, List<ValidationIssue> issues)
    {
        if (!root.TryGetProperty("soulRelics", out var soulRelics))
            return;

        const string soulRelicsPath = "game_state/meta/soul_state.json.soulRelics";

        if (soulRelics.ValueKind != JsonValueKind.Object)
        {
            AddInvalidCurrentSoulRelicsShapeIssue(
                issues,
                soulRelicsPath,
                "soulRelics должен быть объектом с equipped[] и stored[]",
                soulRelics.ValueKind.ToString());
            return;
        }

        foreach (var property in soulRelics.EnumerateObject())
        {
            if (string.Equals(property.Name, "equipped", StringComparison.OrdinalIgnoreCase) ||
                string.Equals(property.Name, "stored", StringComparison.OrdinalIgnoreCase))
            {
                continue;
            }

            AddInvalidCurrentSoulRelicsShapeIssue(
                issues,
                $"{soulRelicsPath}.{property.Name}",
                "soulRelics содержит неподдерживаемый ключ",
                property.Name);
        }

        ValidateCurrentSoulRelicCollection(soulRelics, "equipped", issues);
        ValidateCurrentSoulRelicCollection(soulRelics, "stored", issues);
    }

    private void ValidateCurrentSoulRelicCollection(JsonElement soulRelics, string collectionName, List<ValidationIssue> issues)
    {
        var collectionPath = $"game_state/meta/soul_state.json.soulRelics.{collectionName}";
        if (!soulRelics.TryGetProperty(collectionName, out var collection) ||
            collection.ValueKind != JsonValueKind.Array)
        {
            AddInvalidCurrentSoulRelicsShapeIssue(
                issues,
                collectionPath,
                $"soulRelics.{collectionName} должен быть массивом",
                soulRelics.TryGetProperty(collectionName, out var actual) ? actual.ValueKind.ToString() : "missing");
            return;
        }

        var index = 0;
        foreach (var relic in collection.EnumerateArray())
        {
            var relicPath = $"{collectionPath}[{index++}]";
            if (relic.ValueKind != JsonValueKind.Object)
            {
                AddInvalidCurrentSoulRelicsShapeIssue(
                    issues,
                    relicPath,
                    "Soul Relic entry должен быть объектом",
                    relic.ValueKind.ToString());
                continue;
            }

            var scratchIssues = new List<ValidationIssue>();
            ValidateMinimalSoulRelicObject(relic, relicPath, scratchIssues, "SoulState");
            if (scratchIssues.Count == 0)
                continue;

            AddInvalidCurrentSoulRelicsShapeIssue(
                issues,
                relicPath,
                "Soul Relic в текущем soul_state не соответствует canonical форме",
                string.Join("; ", scratchIssues.Select(static issue =>
                    string.IsNullOrWhiteSpace(issue.Code)
                        ? issue.Message
                        : $"{issue.Code}: {issue.Message}")));
        }
    }

    private static void AddInvalidCurrentSoulRelicsShapeIssue(
        List<ValidationIssue> issues,
        string path,
        string message,
        string actual)
    {
        issues.Add(new ValidationIssue(
            path,
            IssueSeverity.Error,
            message,
            code: "soul_relic_invalid_canonical_shape",
            section: "SoulState",
            expected: "soulRelics object with equipped[]/stored[] arrays of relic objects containing relicId, name, and canonical rarity/quality",
            actual: actual,
            repairHint: "Исправь current soul_state.json: используй relicId вместо id и rarity/quality вместо legacy tier, чтобы strict canonical normalizer не падал после accepted turn."));
    }
}
