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
}
