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
    private void ValidatePlayerBehavior(JsonElement root, string contextPrefix, List<ValidationIssue> issues)
    {
        if (root.TryGetProperty("historyManipulationCoefficient", out var coeff) &&
            coeff.ValueKind != JsonValueKind.Number)
        {
            issues.Add(new ValidationIssue($"{contextPrefix}.historyManipulationCoefficient", IssueSeverity.Error,
                "historyManipulationCoefficient должен быть числом"));
        }

        if (!root.TryGetProperty("playerBehaviorAssessment", out var assessment))
            return;

        var context = $"{contextPrefix}.playerBehaviorAssessment";
        if (!RequireObject(assessment, context, issues))
            return;

        if (assessment.TryGetProperty("historyManipulationCoefficient", out var nestedCoeff) &&
            nestedCoeff.ValueKind != JsonValueKind.Number)
        {
            issues.Add(new ValidationIssue($"{context}.historyManipulationCoefficient", IssueSeverity.Error,
                "historyManipulationCoefficient должен быть числом"));
        }
    }

    private void ValidateCharacterChronicle(JsonElement root, string contextPrefix, List<ValidationIssue> issues)
    {
        if (root.TryGetProperty("characterChronicleUpdates", out var updates))
        {
            if (updates.ValueKind != JsonValueKind.Null)
            {
                RequireArrayOfObjects(updates, $"{contextPrefix}.characterChronicleUpdates", issues);
                if (updates.ValueKind == JsonValueKind.Array)
                {
                    var index = 0;
                    foreach (var item in updates.EnumerateArray())
                    {
                        var itemContext = $"{contextPrefix}.characterChronicleUpdates[{index++}]";
                        if (!RequireObject(item, itemContext, issues))
                            continue;

                        ValidateCharacterChronicleUpdateObject(item, itemContext, issues);
                    }
                }
            }
        }

        if (root.TryGetProperty("entries", out var entries))
            ValidateLooseStringOrObjectArray(entries, $"{contextPrefix}.entries", issues);
    }

    private void ValidateAchievementData(JsonElement root, string contextPrefix, List<ValidationIssue> issues)
    {
        var hasUnlockCommands = root.TryGetProperty("achievementUnlocks", out var unlockCommands) &&
                                unlockCommands.ValueKind != JsonValueKind.Null;
        if (contextPrefix.EndsWith("game_state/meta/achievements.json", StringComparison.OrdinalIgnoreCase) &&
            !hasUnlockCommands)
        {
            foreach (var requiredProp in new[] { "unlockedAchievements", "trackedProgress", "stats" })
            {
                if (!root.TryGetProperty(requiredProp, out _))
                {
                    issues.Add(new ValidationIssue(
                        $"{contextPrefix}.{requiredProp}",
                        IssueSeverity.Error,
                        "Canonical achievements file не содержит обязательный top-level ключ",
                        code: "achievements_missing_required_top_level_key",
                        section: "Achievements",
                        expected: "unlockedAchievements + trackedProgress + stats",
                        actual: $"missing {requiredProp}",
                        repairHint: "Сохраняй canonical achievements.json с top-level ключами unlockedAchievements, trackedProgress и stats даже если некоторые секции пока пустые."));
                }
            }
        }

        ValidateAchievementEntryArray(root, contextPrefix, issues, "achievementUnlocks", persisted: false);
        ValidateAchievementEntryArray(root, contextPrefix, issues, "unlockedAchievements", persisted: true);
        ValidateAchievementTrackedProgress(root, contextPrefix, issues);
        ValidateAchievementStatsObject(root, contextPrefix, issues);
    }

    private void ValidateCodexData(JsonElement root, string contextPrefix, List<ValidationIssue> issues)
    {
        var hasCodexCommands = root.TryGetProperty("loreCodexUpdates", out var codexCommands) &&
                               codexCommands.ValueKind != JsonValueKind.Null;
        if (contextPrefix.EndsWith("lore/codex_entries.json", StringComparison.OrdinalIgnoreCase) &&
            !hasCodexCommands)
        {
            foreach (var requiredProp in new[] { "entries", "totalEntries", "categories" })
            {
                if (!root.TryGetProperty(requiredProp, out _))
                {
                    issues.Add(new ValidationIssue(
                        $"{contextPrefix}.{requiredProp}",
                        IssueSeverity.Error,
                        "Canonical codex file не содержит обязательный top-level ключ",
                        code: "codex_missing_required_top_level_key",
                        section: "Codex",
                        expected: "entries + totalEntries + categories",
                        actual: $"missing {requiredProp}",
                        repairHint: "Сохраняй canonical lore/codex_entries.json с top-level ключами entries, totalEntries и categories даже если часть разделов пока пустая."));
                }
            }
        }

        ValidateCodexEntryArray(root, contextPrefix, issues, "loreCodexUpdates");
        ValidateCodexEntryArray(root, contextPrefix, issues, "entries");

        if (root.TryGetProperty("totalEntries", out var totalEntries) &&
            (totalEntries.ValueKind != JsonValueKind.Number || !totalEntries.TryGetInt32(out _)))
        {
            issues.Add(new ValidationIssue(
                $"{contextPrefix}.totalEntries",
                IssueSeverity.Error,
                "totalEntries должен быть числом"));
        }

        if (root.TryGetProperty("categories", out var categories))
        {
            if (!RequireObject(categories, $"{contextPrefix}.categories", issues))
                return;

            foreach (var requiredCategory in AllowedCodexCategories)
            {
                if (!categories.TryGetProperty(requiredCategory, out var bucketValue) ||
                    bucketValue.ValueKind != JsonValueKind.Number ||
                    !bucketValue.TryGetInt32(out _))
                {
                    issues.Add(new ValidationIssue(
                        $"{contextPrefix}.categories.{requiredCategory}",
                        IssueSeverity.Error,
                        "Canonical codex categories должны содержать полный набор category buckets",
                        code: "codex_missing_category_bucket",
                        section: "Codex",
                        expected: string.Join(", ", AllowedCodexCategories),
                        actual: requiredCategory,
                        repairHint: "Сохраняй lore/codex_entries.json.categories с полным canonical набором buckets, даже если значение конкретной категории равно 0."));
                }
            }

            foreach (var category in categories.EnumerateObject())
            {
                if (!AllowedCodexCategories.Contains(category.Name))
                {
                    issues.Add(new ValidationIssue(
                        $"{contextPrefix}.categories.{category.Name}",
                        IssueSeverity.Error,
                        "categories содержит неподдерживаемый codex bucket",
                        code: "codex_unknown_category_bucket",
                        section: "Codex",
                        expected: string.Join(" | ", AllowedCodexCategories),
                        actual: category.Name,
                        repairHint: "Используй в lore/codex_entries.json.categories только canonical codex category buckets."));
                }
                else if (category.Value.ValueKind != JsonValueKind.Number || !category.Value.TryGetInt32(out _))
                {
                    issues.Add(new ValidationIssue(
                        $"{contextPrefix}.categories.{category.Name}",
                        IssueSeverity.Error,
                        "Значение категории codex должно быть числом"));
                }
            }
        }

        if (root.TryGetProperty("entries", out var entriesNode) &&
            entriesNode.ValueKind == JsonValueKind.Array &&
            root.TryGetProperty("totalEntries", out var totalEntriesNode) &&
            totalEntriesNode.ValueKind == JsonValueKind.Number &&
            totalEntriesNode.TryGetInt32(out var totalEntriesCount) &&
            totalEntriesCount != entriesNode.GetArrayLength())
        {
            issues.Add(new ValidationIssue(
                $"{contextPrefix}.totalEntries",
                IssueSeverity.Error,
                "totalEntries должен совпадать с фактическим числом codex entries",
                code: "codex_total_entries_mismatch",
                section: "Codex",
                expected: entriesNode.GetArrayLength().ToString(),
                actual: totalEntriesCount.ToString(),
                repairHint: "Синхронизируй totalEntries с реальным количеством объектов в lore/codex_entries.json.entries."));
        }

        if (contextPrefix.EndsWith("lore/codex_entries.json", StringComparison.OrdinalIgnoreCase) &&
            root.TryGetProperty("entries", out entriesNode) &&
            entriesNode.ValueKind == JsonValueKind.Array &&
            root.TryGetProperty("categories", out var categoriesNode) &&
            categoriesNode.ValueKind == JsonValueKind.Object)
        {
            var actualCategoryCounts = AllowedCodexCategories.ToDictionary(key => key, _ => 0, StringComparer.OrdinalIgnoreCase);
            foreach (var entry in entriesNode.EnumerateArray())
            {
                var category = GetFirstNonEmptyString(entry, "category");
                if (!string.IsNullOrWhiteSpace(category) && actualCategoryCounts.ContainsKey(category))
                    actualCategoryCounts[category]++;
            }

            foreach (var (categoryName, actualCount) in actualCategoryCounts)
            {
                if (!categoriesNode.TryGetProperty(categoryName, out var categoryValue) ||
                    categoryValue.ValueKind != JsonValueKind.Number ||
                    !categoryValue.TryGetInt32(out var storedCount))
                {
                    continue;
                }

                if (storedCount != actualCount)
                {
                    issues.Add(new ValidationIssue(
                        $"{contextPrefix}.categories.{categoryName}",
                        IssueSeverity.Error,
                        "Codex category bucket должен совпадать с фактическим числом entries этой категории",
                        code: "codex_category_count_mismatch",
                        section: "Codex",
                        expected: actualCount.ToString(),
                        actual: storedCount.ToString(),
                        repairHint: "Синхронизируй lore/codex_entries.json.categories с реальным распределением entries по category."));
                }
            }
        }
    }

    private void ValidateAchievementStatsObject(JsonElement root, string contextPrefix, List<ValidationIssue> issues)
    {
        if (!root.TryGetProperty("stats", out var stats))
            return;

        var statsContext = $"{contextPrefix}.stats";
        if (!RequireObject(stats, statsContext, issues))
            return;

        ValidateNonNegativeNumberField(stats, statsContext, issues, "totalUnlocked");

        foreach (var propName in new[] { "byCategory", "byRarity" })
        {
            if (!stats.TryGetProperty(propName, out var bucket))
                continue;

            if (!RequireObject(bucket, $"{statsContext}.{propName}", issues))
                continue;

            foreach (var entry in bucket.EnumerateObject())
            {
                var allowedBucketNames = string.Equals(propName, "byCategory", StringComparison.OrdinalIgnoreCase)
                    ? AllowedAchievementCategories
                    : AllowedAchievementRarities;

                if (!allowedBucketNames.Contains(entry.Name))
                {
                    issues.Add(new ValidationIssue(
                        $"{statsContext}.{propName}.{entry.Name}",
                        IssueSeverity.Error,
                        "Achievement stats содержит неподдерживаемый bucket",
                        code: "achievement_stats_unknown_bucket",
                        section: "Achievements",
                        expected: string.Join(" | ", allowedBucketNames),
                        actual: entry.Name,
                        repairHint: "Используй в achievements stats только canonical category/rarity buckets из spec."));
                    continue;
                }

                if (entry.Value.ValueKind != JsonValueKind.Number || !entry.Value.TryGetInt32(out var count) || count < 0)
                {
                    issues.Add(new ValidationIssue(
                        $"{statsContext}.{propName}.{entry.Name}",
                        IssueSeverity.Error,
                        "Achievement stats bucket должен содержать только неотрицательные integer counts",
                        code: "achievement_stats_invalid_bucket_value",
                        section: "Achievements",
                        repairHint: "Сохраняй stats.byCategory и stats.byRarity как объекты с неотрицательными integer counters."));
                }
            }

            var expectedBuckets = string.Equals(propName, "byCategory", StringComparison.OrdinalIgnoreCase)
                ? AllowedAchievementCategories
                : AllowedAchievementRarities;
            foreach (var requiredBucket in expectedBuckets)
            {
                if (!bucket.TryGetProperty(requiredBucket, out var _))
                {
                    issues.Add(new ValidationIssue(
                        $"{statsContext}.{propName}.{requiredBucket}",
                        IssueSeverity.Error,
                        "Achievement stats должен содержать полный набор canonical buckets",
                        code: "achievement_stats_missing_bucket",
                        section: "Achievements",
                        expected: string.Join(", ", expectedBuckets),
                        actual: requiredBucket,
                        repairHint: "Сохраняй в achievements stats полный canonical набор category/rarity buckets, даже если значение конкретного bucket равно 0."));
                }
            }
        }

        if (root.TryGetProperty("unlockedAchievements", out var unlockedAchievements) &&
            unlockedAchievements.ValueKind == JsonValueKind.Array &&
            stats.TryGetProperty("totalUnlocked", out var totalUnlocked) &&
            totalUnlocked.ValueKind == JsonValueKind.Number &&
            totalUnlocked.TryGetInt32(out var totalUnlockedCount) &&
            totalUnlockedCount != unlockedAchievements.GetArrayLength())
        {
            issues.Add(new ValidationIssue(
                $"{statsContext}.totalUnlocked",
                IssueSeverity.Error,
                "stats.totalUnlocked должен совпадать с числом unlockedAchievements",
                code: "achievement_total_unlocked_mismatch",
                section: "Achievements",
                expected: unlockedAchievements.GetArrayLength().ToString(),
                actual: totalUnlockedCount.ToString(),
                repairHint: "Синхронизируй stats.totalUnlocked с фактическим числом объектов в unlockedAchievements."));
        }

        if (root.TryGetProperty("unlockedAchievements", out unlockedAchievements) &&
            unlockedAchievements.ValueKind == JsonValueKind.Array)
        {
            if (stats.TryGetProperty("byCategory", out var byCategory) && byCategory.ValueKind == JsonValueKind.Object)
            {
                var actualByCategory = AllowedAchievementCategories.ToDictionary(key => key, _ => 0, StringComparer.OrdinalIgnoreCase);
                foreach (var achievement in unlockedAchievements.EnumerateArray())
                {
                    var category = GetFirstNonEmptyString(achievement, "category");
                    if (!string.IsNullOrWhiteSpace(category) && actualByCategory.ContainsKey(category))
                        actualByCategory[category]++;
                }

                foreach (var (bucketName, actualCount) in actualByCategory)
                {
                    if (!byCategory.TryGetProperty(bucketName, out var bucketValue) ||
                        bucketValue.ValueKind != JsonValueKind.Number ||
                        !bucketValue.TryGetInt32(out var parsedBucket))
                    {
                        continue;
                    }

                    if (parsedBucket != actualCount)
                    {
                        issues.Add(new ValidationIssue(
                            $"{statsContext}.byCategory.{bucketName}",
                            IssueSeverity.Error,
                            "stats.byCategory должен совпадать с фактическим числом unlockedAchievements по категориям",
                            code: "achievement_stats_category_count_mismatch",
                            section: "Achievements",
                            expected: actualCount.ToString(),
                            actual: parsedBucket.ToString(),
                            repairHint: "Синхронизируй achievements stats.byCategory с реальным распределением unlockedAchievements по category."));
                    }
                }
            }

            if (stats.TryGetProperty("byRarity", out var byRarity) && byRarity.ValueKind == JsonValueKind.Object)
            {
                var actualByRarity = AllowedAchievementRarities.ToDictionary(key => key, _ => 0, StringComparer.OrdinalIgnoreCase);
                foreach (var achievement in unlockedAchievements.EnumerateArray())
                {
                    var rarity = GetFirstNonEmptyString(achievement, "rarity");
                    if (!string.IsNullOrWhiteSpace(rarity) && actualByRarity.ContainsKey(rarity))
                        actualByRarity[rarity]++;
                }

                foreach (var (bucketName, actualCount) in actualByRarity)
                {
                    if (!byRarity.TryGetProperty(bucketName, out var bucketValue) ||
                        bucketValue.ValueKind != JsonValueKind.Number ||
                        !bucketValue.TryGetInt32(out var parsedBucket))
                    {
                        continue;
                    }

                    if (parsedBucket != actualCount)
                    {
                        issues.Add(new ValidationIssue(
                            $"{statsContext}.byRarity.{bucketName}",
                            IssueSeverity.Error,
                            "stats.byRarity должен совпадать с фактическим числом unlockedAchievements по rarity",
                            code: "achievement_stats_rarity_count_mismatch",
                            section: "Achievements",
                            expected: actualCount.ToString(),
                            actual: parsedBucket.ToString(),
                            repairHint: "Синхронизируй achievements stats.byRarity с реальным распределением unlockedAchievements по rarity."));
                    }
                }
            }
        }
    }

    private void ValidateAchievementEntryArray(JsonElement root, string contextPrefix, List<ValidationIssue> issues, string propName, bool persisted)
    {
        if (!root.TryGetProperty(propName, out var arr))
            return;

        RequireArrayOfObjects(arr, $"{contextPrefix}.{propName}", issues);
        if (arr.ValueKind != JsonValueKind.Array)
            return;

        var index = 0;
        foreach (var item in arr.EnumerateArray())
        {
            if (item.ValueKind == JsonValueKind.Object)
            ValidateAchievementEntry(
                item,
                $"{contextPrefix}.{propName}[{index}]",
                issues,
                persisted,
                requireUnlockMetadata: persisted || propName.Equals("achievementUnlocks", StringComparison.OrdinalIgnoreCase));
            index++;
        }
    }

    private void ValidateAchievementTrackedProgress(JsonElement root, string contextPrefix, List<ValidationIssue> issues)
    {
        if (!root.TryGetProperty("trackedProgress", out var tracked))
            return;

        RequireArrayOfObjects(tracked, $"{contextPrefix}.trackedProgress", issues);
        if (tracked.ValueKind != JsonValueKind.Array)
            return;

        var index = 0;
        foreach (var item in tracked.EnumerateArray())
        {
            var itemContext = $"{contextPrefix}.trackedProgress[{index++}]";
            if (!RequireObject(item, itemContext, issues))
                continue;

            ValidateAchievementEntry(item, itemContext, issues, persisted: false, requireUnlockMetadata: false);
            ValidateAchievementProgressObject(item, itemContext, issues, required: true);
        }
    }

    private void ValidateAchievementEntry(JsonElement item, string itemContext, List<ValidationIssue> issues, bool persisted, bool requireUnlockMetadata)
    {
        RequireString(item, itemContext, issues, "achievementId");
        RequireString(item, itemContext, issues, "name");
        RequireString(item, itemContext, issues, "description");
        var category = RequireString(item, itemContext, issues, "category");
        var rarity = RequireString(item, itemContext, issues, "rarity");
        RequireString(item, itemContext, issues, "icon");

        if (!string.IsNullOrWhiteSpace(category) && !AllowedAchievementCategories.Contains(category))
        {
            issues.Add(new ValidationIssue(
                $"{itemContext}.category",
                IssueSeverity.Error,
                "achievement category должен быть одним из canonical enum значений",
                code: "achievement_invalid_category",
                section: "Achievements",
                expected: string.Join(" | ", AllowedAchievementCategories),
                actual: category,
                repairHint: "Используй одну из achievement categories из CLI contract: combat, exploration, story, social, crafting, meta, death, secret."));
        }

        if (!string.IsNullOrWhiteSpace(rarity) && !AllowedAchievementRarities.Contains(rarity))
        {
            issues.Add(new ValidationIssue(
                $"{itemContext}.rarity",
                IssueSeverity.Error,
                "achievement rarity должен быть одним из canonical enum значений",
                code: "achievement_invalid_rarity",
                section: "Achievements",
                expected: string.Join(" | ", AllowedAchievementRarities),
                actual: rarity,
                repairHint: "Используй canonical achievement rarity: common, uncommon, rare, epic или legendary."));
        }

        if (requireUnlockMetadata)
        {
            ValidateNonNegativeIntegerField(item, itemContext, issues, "incarnation", "Achievements");
            var unlockedAt = RequireString(item, itemContext, issues, "unlockedAt");
            if (!string.IsNullOrWhiteSpace(unlockedAt) && !DateTimeOffset.TryParse(unlockedAt, out _))
            {
                issues.Add(new ValidationIssue(
                    $"{itemContext}.unlockedAt",
                    IssueSeverity.Error,
                    "achievement unlockedAt должен быть ISO 8601 timestamp",
                    code: "achievement_invalid_unlocked_at",
                    section: "Achievements",
                    expected: "ISO 8601 timestamp",
                    actual: unlockedAt,
                    repairHint: "Записывай unlockedAt в ISO 8601 формате, чтобы chronology achievement unlocks оставалась воспроизводимой."));
            }
        }

        if (item.TryGetProperty("hidden", out var hidden) &&
            hidden.ValueKind is not (JsonValueKind.True or JsonValueKind.False))
        {
            issues.Add(new ValidationIssue(
                $"{itemContext}.hidden",
                IssueSeverity.Error,
                "hidden должен быть boolean",
                code: "achievement_hidden_not_boolean",
                section: "Achievements",
                expected: "boolean",
                actual: hidden.ValueKind.ToString(),
                repairHint: "Передавай hidden только как true/false, если это поле присутствует в achievement object."));
        }

        ValidateAchievementProgressObject(item, itemContext, issues, required: false);

        if (item.TryGetProperty("reward", out var reward))
        {
            if (!RequireObject(reward, $"{itemContext}.reward", issues))
                return;

            var rewardType = GetFirstNonEmptyString(reward, "type");
            ValidateOptionalString(reward, $"{itemContext}.reward", issues, "type");
            if (!string.IsNullOrWhiteSpace(rewardType) && !AllowedAchievementRewardTypes.Contains(rewardType))
            {
                issues.Add(new ValidationIssue(
                    $"{itemContext}.reward.type",
                    IssueSeverity.Error,
                    "achievement reward.type должен быть одним из canonical enum значений",
                    code: "achievement_invalid_reward_type",
                    section: "Achievements",
                    expected: string.Join(" | ", AllowedAchievementRewardTypes),
                    actual: rewardType,
                    repairHint: "Используй achievement reward.type только из contract enum: inkFeathers, soulXP, title, none."));
            }
            if (reward.TryGetProperty("value", out var rewardValue) &&
                rewardValue.ValueKind is not (JsonValueKind.String or JsonValueKind.Number))
            {
                issues.Add(new ValidationIssue(
                    $"{itemContext}.reward.value",
                    IssueSeverity.Error,
                    "reward.value должен быть строкой или числом"));
            }
        }
    }

    private void ValidateAchievementProgressObject(JsonElement item, string itemContext, List<ValidationIssue> issues, bool required)
    {
        if (!item.TryGetProperty("progress", out var progress))
        {
            if (required)
                RequireObjectProperty(item, itemContext, issues, "progress");
            return;
        }

        if (!RequireObject(progress, $"{itemContext}.progress", issues))
            return;

        ValidateNonNegativeIntegerField(progress, $"{itemContext}.progress", issues, "current", "Achievements");
        ValidatePositiveIntegerField(progress, $"{itemContext}.progress", issues, "target");
    }

    private void ValidateCodexEntryArray(JsonElement root, string contextPrefix, List<ValidationIssue> issues, string propName)
    {
        if (!root.TryGetProperty(propName, out var arr))
            return;

        RequireArrayOfObjects(arr, $"{contextPrefix}.{propName}", issues);
        if (arr.ValueKind != JsonValueKind.Array)
            return;

        var index = 0;
        foreach (var item in arr.EnumerateArray())
        {
            var itemContext = $"{contextPrefix}.{propName}[{index++}]";
            if (!RequireObject(item, itemContext, issues))
                continue;

            if (propName.Equals("loreCodexUpdates", StringComparison.OrdinalIgnoreCase))
            {
                var command = RequireString(item, itemContext, issues, "command");
                if (string.Equals(command, "add", StringComparison.OrdinalIgnoreCase))
                {
                    if (item.TryGetProperty("entry", out var entry) && entry.ValueKind == JsonValueKind.Object)
                        ValidateCodexEntry(entry, $"{itemContext}.entry", issues);
                    else
                        issues.Add(new ValidationIssue(
                            $"{itemContext}.entry",
                            IssueSeverity.Error,
                            "loreCodexUpdates add command должен содержать entry",
                            code: "codex_add_missing_entry",
                            section: "Codex",
                            expected: "entry object for add command",
                            actual: "missing",
                            repairHint: "Для loreCodexUpdates add command передай полный entry object с entryId, title, category, content, discoveredAt, discoveryContext и incarnation."));
                }
                else if (string.Equals(command, "update", StringComparison.OrdinalIgnoreCase))
                {
                    RequireString(item, itemContext, issues, "entryId");
                    RequireObjectProperty(item, itemContext, issues, "updates");
                    if (item.TryGetProperty("updates", out var updates) && RequireObject(updates, $"{itemContext}.updates", issues))
                    {
                        ValidateCodexUpdatePayload(updates, $"{itemContext}.updates", issues);
                        var hasVisibleUpdateFields = updates.EnumerateObject()
                            .Any(prop => !prop.Name.StartsWith("_", StringComparison.OrdinalIgnoreCase));
                        if (!hasVisibleUpdateFields)
                        {
                            issues.Add(new ValidationIssue(
                                $"{itemContext}.updates",
                                IssueSeverity.Error,
                                "loreCodexUpdates update command должен содержать хотя бы одно изменяемое поле",
                                code: "codex_update_missing_changes",
                                section: "Codex",
                                repairHint: "Передай в updates только реально изменившиеся поля codex entry, а не пустой объект."));
                        }
                    }
                }
                else if (!string.IsNullOrWhiteSpace(command))
                {
                    issues.Add(new ValidationIssue(
                        $"{itemContext}.command",
                        IssueSeverity.Error,
                        "loreCodexUpdates поддерживает только add или update",
                        code: "codex_update_invalid_command",
                        section: "Codex",
                        expected: "add | update",
                        actual: command,
                        repairHint: "Используй canonical loreCodexUpdates command add или update и не вводи собственные aliases."));
                }
            }
            else
            {
                ValidateCodexEntry(item, itemContext, issues);
            }
        }
    }

    private void ValidateCodexEntry(JsonElement item, string itemContext, List<ValidationIssue> issues)
    {
        RequireString(item, itemContext, issues, "entryId");
        RequireString(item, itemContext, issues, "title");
        var category = RequireString(item, itemContext, issues, "category");
        RequireString(item, itemContext, issues, "content");
        var discoveredAt = RequireString(item, itemContext, issues, "discoveredAt");
        RequireString(item, itemContext, issues, "discoveryContext");
        ValidateNonNegativeIntegerField(item, itemContext, issues, "incarnation", "Codex");
        ValidateOptionalString(item, itemContext, issues, "subcategory");
        ValidateOptionalString(item, itemContext, issues, "sourceFile");

        if (!string.IsNullOrWhiteSpace(category) && !AllowedCodexCategories.Contains(category))
        {
            issues.Add(new ValidationIssue(
                $"{itemContext}.category",
                IssueSeverity.Error,
                "Codex category должен быть одним из canonical enum значений",
                code: "codex_invalid_category",
                section: "Codex",
                expected: string.Join(" | ", AllowedCodexCategories),
                actual: category,
                repairHint: "Используй только canonical lore codex categories из CLI contract."));
        }

        if (!string.IsNullOrWhiteSpace(discoveredAt) && !DateTimeOffset.TryParse(discoveredAt, out _))
        {
            issues.Add(new ValidationIssue(
                $"{itemContext}.discoveredAt",
                IssueSeverity.Error,
                "Codex discoveredAt должен быть ISO 8601 timestamp",
                code: "codex_invalid_discovered_at",
                section: "Codex",
                expected: "ISO 8601 timestamp",
                actual: discoveredAt,
                repairHint: "Записывай discoveredAt в ISO 8601 формате, чтобы codex chronology корректно сортировалась в UI."));
        }

        if (item.TryGetProperty("relatedEntries", out var relatedEntries))
            RequireArrayOfStrings(relatedEntries, $"{itemContext}.relatedEntries", issues);
        if (item.TryGetProperty("tags", out var tags))
            RequireArrayOfStrings(tags, $"{itemContext}.tags", issues);
    }

    private void ValidateCodexUpdatePayload(JsonElement updates, string context, List<ValidationIssue> issues)
    {
        ValidateOptionalString(updates, context, issues, "content");
        if (updates.TryGetProperty("relatedEntries", out var relatedEntries))
            RequireArrayOfStrings(relatedEntries, $"{context}.relatedEntries", issues);
        if (updates.TryGetProperty("tags", out var tags))
            RequireArrayOfStrings(tags, $"{context}.tags", issues);

        foreach (var prop in updates.EnumerateObject())
        {
            if (prop.Name.StartsWith("_", StringComparison.OrdinalIgnoreCase))
                continue;

            if (!string.Equals(prop.Name, "content", StringComparison.OrdinalIgnoreCase) &&
                !string.Equals(prop.Name, "relatedEntries", StringComparison.OrdinalIgnoreCase) &&
                !string.Equals(prop.Name, "tags", StringComparison.OrdinalIgnoreCase))
            {
                issues.Add(new ValidationIssue(
                    $"{context}.{prop.Name}",
                    IssueSeverity.Error,
                    "loreCodexUpdates.update содержит неподдерживаемое поле в update payload",
                    code: "codex_update_unknown_field",
                    section: "Codex",
                    expected: "content | relatedEntries | tags",
                    actual: prop.Name,
                    repairHint: "Для loreCodexUpdates.update передавай в updates только documented partial fields: content, relatedEntries, tags."));
            }
        }
    }

    private async Task ValidateAchievementUnlockNarrativeMarkersAsync(string responseText, List<ValidationIssue> issues)
    {
        var json = await _fs.ReadFileAsync("game_state/meta/achievements.json");
        if (string.IsNullOrWhiteSpace(json))
            return;

        JsonDocument? previousDoc = null;
        try
        {
            using var doc = JsonDocument.Parse(json);
            if (doc.RootElement.ValueKind != JsonValueKind.Object)
                return;

            var previousJson = await ReadPreTurnTrackedFileAsync("game_state/meta/achievements.json");
            if (!string.IsNullOrWhiteSpace(previousJson))
            {
                try
                {
                    previousDoc = JsonDocument.Parse(previousJson);
                }
                catch
                {
                    previousDoc = null;
                }
            }

            var unlockNames = CollectAchievementNamesRequiringNarrativeMarkers(doc.RootElement, previousDoc?.RootElement);
            if (unlockNames.Count == 0)
                return;

            var index = 0;
            foreach (var achievementName in unlockNames)
            {
                var itemContext = $"game_state/meta/achievements.json.unlockedAchievements[new:{index++}]";

                var expectedMarker = $"[ACHIEVEMENT_UNLOCK: {achievementName}]";
                if (!responseText.Contains(expectedMarker, StringComparison.Ordinal))
                {
                    issues.Add(new ValidationIssue(
                        "output/narrative_response.json.response",
                        IssueSeverity.Error,
                        "Нарратив обязан явно упомянуть achievement unlock marker для каждого achievementUnlocks entry",
                        code: "achievement_unlock_marker_missing_in_narrative",
                        section: "Achievements",
                        expected: expectedMarker,
                        actual: $"missing marker for achievement '{achievementName}'",
                        repairHint: "Добавь в narrative response exact marker вида [ACHIEVEMENT_UNLOCK: Achievement Name] для каждого achievementUnlocks entry этого хода."));
                }
            }
        }
        catch
        {
            // achievements.json shape is validated elsewhere; marker check is best-effort
        }
        finally
        {
            previousDoc?.Dispose();
        }
    }

    private void ValidateVehicleData(JsonElement root, string contextPrefix, List<ValidationIssue> issues)
    {
        if (root.TryGetProperty("UpdateVehicles", out var updates))
            ValidateVehicleArray(updates, $"{contextPrefix}.UpdateVehicles", issues, requireCanonicalStoredShape: false);
        if (root.TryGetProperty("vehicles", out var vehicles))
            ValidateVehicleArray(vehicles, $"{contextPrefix}.vehicles", issues, requireCanonicalStoredShape: true);
        if (root.TryGetProperty("removeVehicles", out var removals))
            RequireArrayOfStrings(removals, $"{contextPrefix}.removeVehicles", issues);
        if (root.TryGetProperty("activeVehicleChange", out var activeVehicle) &&
            activeVehicle.ValueKind != JsonValueKind.String &&
            activeVehicle.ValueKind != JsonValueKind.Null)
        {
            issues.Add(new ValidationIssue(
                $"{contextPrefix}.activeVehicleChange",
                IssueSeverity.Error,
                "activeVehicleChange должен быть строкой или null",
                code: "active_vehicle_change_invalid_type",
                section: "Vehicles",
                expected: "string vehicleId or null",
                actual: activeVehicle.ValueKind.ToString(),
                repairHint: "Передавай в activeVehicleChange либо существующий string vehicleId, либо null для сброса активного транспорта."));
        }
    }

    private void ValidateStorageAccessData(JsonElement root, string contextPrefix, List<ValidationIssue> issues)
    {
        ValidateStorageAccessCommandArray(root, contextPrefix, issues, "grantStorageAccess");
        ValidateStorageAccessCommandArray(root, contextPrefix, issues, "revokeStorageAccess");
        ValidateStorageAccessCommandArray(root, contextPrefix, issues, "shareStorageAccess");
    }

    private void ValidateOtherPlayersInteractions(JsonElement root, string contextPrefix, List<ValidationIssue> issues)
    {
        if (!root.TryGetProperty("otherPlayersInteractions", out var interactions))
            return;

        var context = $"{contextPrefix}.otherPlayersInteractions";
        if (interactions.ValueKind == JsonValueKind.Object)
        {
            foreach (var playerEntry in interactions.EnumerateObject())
            {
                if (string.IsNullOrWhiteSpace(playerEntry.Name))
                {
                    issues.Add(new ValidationIssue(
                        context,
                        IssueSeverity.Error,
                        "otherPlayersInteractions object должен быть keyed by non-empty playerId"));
                    continue;
                }

                if (playerEntry.Value.ValueKind != JsonValueKind.Array)
                {
                    issues.Add(new ValidationIssue(
                        $"{context}.{playerEntry.Name}",
                        IssueSeverity.Error,
                        "Значение otherPlayersInteractions[playerId] должно быть массивом command objects",
                        code: "other_player_interactions_invalid_player_bucket",
                        section: "OtherPlayers",
                        expected: "array of command objects",
                        actual: playerEntry.Value.ValueKind.ToString(),
                        repairHint: "Для каждого target playerId передавай массив command objects, которые обычно шли бы на top-level."));
                    continue;
                }

                var index = 0;
                foreach (var command in playerEntry.Value.EnumerateArray())
                {
                    var commandContext = $"{context}.{playerEntry.Name}[{index++}]";
                    if (!RequireObject(command, commandContext, issues))
                        continue;

                    if (!command.EnumerateObject().Any())
                    {
                        issues.Add(new ValidationIssue(
                            commandContext,
                            IssueSeverity.Error,
                            "Command object для otherPlayersInteractions не должен быть пустым",
                            code: "other_player_interactions_empty_command",
                            section: "OtherPlayers",
                            repairHint: "Передавай в otherPlayersInteractions только непустые top-level command objects для целевого игрока."));
                    }
                }
            }
            return;
        }

        if (interactions.ValueKind == JsonValueKind.Array)
        {
            RequireArrayOfObjects(interactions, context, issues);
            return;
        }

        issues.Add(new ValidationIssue(context, IssueSeverity.Error,
            "otherPlayersInteractions должен быть объектом или массивом"));
    }

    private void ValidateCharacterChronicleUpdateObject(JsonElement item, string context, List<ValidationIssue> issues)
    {
        var entryToAppend = RequireString(item, context, issues, "entryToAppend");
        if (!string.IsNullOrWhiteSpace(entryToAppend) &&
            !CharacterChronicleEntryPrefixRegex.IsMatch(entryToAppend))
        {
            issues.Add(new ValidationIssue(
                $"{context}.entryToAppend",
                IssueSeverity.Error,
                "characterChronicleUpdates.entryToAppend должен начинаться с canonical turn prefix",
                code: "character_chronicle_entry_prefix_invalid",
                section: "CharacterChronicle",
                expected: "#[turn_number] - ...",
                actual: entryToAppend,
                repairHint: "Начинай каждую новую хронику с префикса '#[turn_number] - ' по правилам characterChronicleUpdates."));
        }
    }

    private void ValidateVehicleArray(JsonElement value, string context, List<ValidationIssue> issues, bool requireCanonicalStoredShape)
    {
        RequireArrayOfObjects(value, context, issues);
        if (value.ValueKind != JsonValueKind.Array)
            return;

        var simulatedVehicleStates = !requireCanonicalStoredShape
            ? TryResolvePreTurnVehicleStateMapSync() ?? new Dictionary<string, VehicleStateSnapshot>(StringComparer.OrdinalIgnoreCase)
            : null;
        var index = 0;
        foreach (var item in value.EnumerateArray())
        {
            var itemContext = $"{context}[{index++}]";
            if (!RequireObject(item, itemContext, issues))
                continue;

            var hasVehicleId = item.TryGetProperty("vehicleId", out var vehicleIdValue);
            var vehicleIdIsNull = false;
            if (!hasVehicleId)
            {
                issues.Add(new ValidationIssue(
                    $"{itemContext}.vehicleId",
                    IssueSeverity.Error,
                    "Vehicle object должен содержать обязательное поле vehicleId",
                    code: "vehicle_missing_vehicle_id",
                    section: "Vehicles",
                    expected: requireCanonicalStoredShape ? "non-empty string vehicleId" : "string vehicleId or null for brand-new vehicle creation",
                    actual: "missing",
                    repairHint: requireCanonicalStoredShape
                        ? "Сохраняй canonical vehicles[] только с непустым string vehicleId."
                        : "Для UpdateVehicles передай существующий string vehicleId или null для brand-new vehicle object по Block 10."));
                continue;
            }

            if (requireCanonicalStoredShape)
            {
                if (vehicleIdValue.ValueKind != JsonValueKind.String || string.IsNullOrWhiteSpace(vehicleIdValue.GetString()))
                {
                    issues.Add(new ValidationIssue(
                        $"{itemContext}.vehicleId",
                        IssueSeverity.Error,
                        "Canonical vehicles[].vehicleId должен быть непустой строкой",
                        code: "vehicle_canonical_id_invalid",
                        section: "Vehicles",
                        expected: "non-empty string vehicleId",
                        actual: vehicleIdValue.ValueKind == JsonValueKind.String ? "empty string" : vehicleIdValue.ValueKind.ToString(),
                        repairHint: "В canonical vehicles[] сохраняй уже назначенный string vehicleId. Null допустим только в brand-new UpdateVehicles object до нормализации."));
                }
            }
            else if (vehicleIdValue.ValueKind == JsonValueKind.Null)
            {
                vehicleIdIsNull = true;
            }
            else if (vehicleIdValue.ValueKind != JsonValueKind.String || string.IsNullOrWhiteSpace(vehicleIdValue.GetString()))
            {
                issues.Add(new ValidationIssue(
                    $"{itemContext}.vehicleId",
                    IssueSeverity.Error,
                    "UpdateVehicles.vehicleId должен быть непустой строкой или null",
                    code: "vehicle_command_id_invalid",
                    section: "Vehicles",
                    expected: "non-empty string vehicleId or null for brand-new vehicle object",
                    actual: vehicleIdValue.ValueKind == JsonValueKind.String ? "empty string" : vehicleIdValue.ValueKind.ToString(),
                    repairHint: "Для существующего транспорта передавай string vehicleId. Для brand-new vehicle object Block 10 допускает vehicleId = null."));
            }

            var mustLookLikeFullVehicleObject = requireCanonicalStoredShape || vehicleIdIsNull;
            if (!mustLookLikeFullVehicleObject && item.EnumerateObject().Count() == 1)
            {
                issues.Add(new ValidationIssue(
                    itemContext,
                    IssueSeverity.Error,
                    "UpdateVehicles object должен содержать хотя бы одно изменение помимо vehicleId",
                    code: "vehicle_update_missing_changes",
                    section: "Vehicles",
                    expected: "vehicleId plus at least one changed property, or full vehicle object for a new vehicle",
                    actual: "vehicleId only",
                    repairHint: "Для существующего транспорта передай vehicleId и реально изменившиеся поля. Для выдачи нового транспорта передай полный Vehicle Object по Block 10."));
            }

            if (mustLookLikeFullVehicleObject)
            {
                RequireString(item, itemContext, issues, "name");
                RequireString(item, itemContext, issues, "description");
                RequireString(item, itemContext, issues, "image_prompt");
                RequireString(item, itemContext, issues, "type");
                RequireBooleanField(item, itemContext, issues, "isSentient");
                RequireString(item, itemContext, issues, "availability");
                ValidateRequiredNullableStringField(item, itemContext, issues, "currentLocationId");
                RequireString(item, itemContext, issues, "maxHealth");
                RequireString(item, itemContext, issues, "currentHealth");
                ValidateIntegerField(item, itemContext, issues, "speedBonus");
                RequireObjectArrayField(item, itemContext, issues, "actions");
                RequireObjectArrayField(item, itemContext, issues, "resistances");
                RequireObjectArrayField(item, itemContext, issues, "inventory");
            }
            else
            {
                ValidateOptionalString(item, itemContext, issues, "name");
                ValidateOptionalString(item, itemContext, issues, "description");
                ValidateOptionalString(item, itemContext, issues, "image_prompt");
            }

            var vehicleType = GetFirstNonEmptyString(item, "type");
            if (!mustLookLikeFullVehicleObject)
                ValidateOptionalString(item, itemContext, issues, "type");
            if (!string.IsNullOrWhiteSpace(vehicleType) && !AllowedVehicleTypes.Contains(vehicleType))
            {
                issues.Add(new ValidationIssue(
                    $"{itemContext}.type",
                    IssueSeverity.Error,
                    "Vehicle type должен быть одним из canonical enum значений",
                    code: "vehicle_invalid_type",
                    section: "Vehicles",
                    expected: string.Join(" | ", AllowedVehicleTypes),
                    actual: vehicleType,
                    repairHint: "Используй для vehicle.type только Mount, Vehicle или Summonable по Block 10 contract."));
            }

            var availability = GetFirstNonEmptyString(item, "availability");
            if (!mustLookLikeFullVehicleObject)
                ValidateOptionalString(item, itemContext, issues, "availability");
            if (!string.IsNullOrWhiteSpace(availability) && !AllowedVehicleAvailabilities.Contains(availability))
            {
                issues.Add(new ValidationIssue(
                    $"{itemContext}.availability",
                    IssueSeverity.Error,
                    "Vehicle availability должен быть одним из canonical enum значений",
                    code: "vehicle_invalid_availability",
                    section: "Vehicles",
                    expected: string.Join(" | ", AllowedVehicleAvailabilities),
                    actual: availability,
                    repairHint: "Используй для vehicle.availability только Active, Parked или Pocket по Block 10 contract."));
            }

            if (item.TryGetProperty("isSentient", out _) && !mustLookLikeFullVehicleObject)
                RequireBooleanField(item, itemContext, issues, "isSentient");
            if (item.TryGetProperty("currentLocationId", out _) && !mustLookLikeFullVehicleObject)
                ValidateRequiredNullableStringField(item, itemContext, issues, "currentLocationId");
            if (item.TryGetProperty("maxHealth", out _))
                ValidatePercentageStringField(item, itemContext, issues, "maxHealth", requirePositive: true);
            if (item.TryGetProperty("currentHealth", out _))
                ValidatePercentageStringField(item, itemContext, issues, "currentHealth", requirePositive: false);
            if (item.TryGetProperty("speedBonus", out _))
                ValidateIntegerField(item, itemContext, issues, "speedBonus");
            if (item.TryGetProperty("actions", out var actions))
                ValidateCombatActionArray(actions, $"{itemContext}.actions", issues, section: "Vehicles");
            if (item.TryGetProperty("resistances", out var resistances))
                ValidateCombatResistanceArray(resistances, $"{itemContext}.resistances", issues);
            if (item.TryGetProperty("inventory", out var inventory))
            {
                RequireArrayOfObjects(inventory, $"{itemContext}.inventory", issues);
                if (inventory.ValueKind == JsonValueKind.Array)
                {
                    var inventoryIndex = 0;
                    foreach (var inventoryItem in inventory.EnumerateArray())
                    {
                        var inventoryItemContext = $"{itemContext}.inventory[{inventoryIndex++}]";
                        if (!RequireObject(inventoryItem, inventoryItemContext, issues))
                            continue;

                        ValidateFullInventoryItemObject(inventoryItem, inventoryItemContext, issues, requireStringExistedId: false);
                    }
                }
            }

            var currentLocationId = item.TryGetProperty("currentLocationId", out var currentLocationNode) &&
                                    currentLocationNode.ValueKind == JsonValueKind.String
                ? currentLocationNode.GetString() ?? string.Empty
                : string.Empty;
            if ((string.Equals(availability, "Active", StringComparison.OrdinalIgnoreCase) ||
                 string.Equals(availability, "Pocket", StringComparison.OrdinalIgnoreCase)) &&
                !string.IsNullOrWhiteSpace(currentLocationId))
            {
                issues.Add(new ValidationIssue(
                    $"{itemContext}.currentLocationId",
                    IssueSeverity.Error,
                    "Vehicle с availability=Active или Pocket должен иметь currentLocationId = null",
                    code: "vehicle_active_or_pocket_requires_null_location",
                    section: "Vehicles",
                    expected: "null currentLocationId for Active/Pocket vehicle",
                    actual: currentLocationId,
                    repairHint: "Для availability=Active или Pocket оставляй currentLocationId = null. Указывай locationId только для Parked vehicle."));
            }

            var hasParkedLocationNode = item.TryGetProperty("currentLocationId", out var parkedLocationNode);
            if (string.Equals(availability, "Parked", StringComparison.OrdinalIgnoreCase) &&
                !hasParkedLocationNode)
            {
                issues.Add(new ValidationIssue(
                    $"{itemContext}.currentLocationId",
                    IssueSeverity.Error,
                    "Vehicle с availability=Parked должен явно содержать currentLocationId",
                    code: "vehicle_parked_missing_location",
                    section: "Vehicles",
                    expected: "parked vehicle location id",
                    actual: "missing",
                    repairHint: "Для availability=Parked передай currentLocationId локации, где транспорт припаркован."));
            }
            else if (string.Equals(availability, "Parked", StringComparison.OrdinalIgnoreCase) &&
                     hasParkedLocationNode &&
                     parkedLocationNode.ValueKind == JsonValueKind.Null)
            {
                issues.Add(new ValidationIssue(
                    $"{itemContext}.currentLocationId",
                    IssueSeverity.Error,
                    "Vehicle с availability=Parked не может иметь currentLocationId = null",
                    code: "vehicle_parked_null_location_forbidden",
                    section: "Vehicles",
                    expected: "non-null parked vehicle location id",
                    actual: "null",
                    repairHint: "Для availability=Parked передай currentLocationId конкретной локации, где оставлен транспорт."));
            }

            var vehicleId = vehicleIdValue.ValueKind == JsonValueKind.String
                ? vehicleIdValue.GetString()
                : null;
            if (!requireCanonicalStoredShape &&
                !mustLookLikeFullVehicleObject &&
                !string.IsNullOrWhiteSpace(vehicleId) &&
                simulatedVehicleStates != null &&
                (item.TryGetProperty("availability", out _) || item.TryGetProperty("currentLocationId", out _)))
            {
                if (simulatedVehicleStates.TryGetValue(vehicleId!, out var previousState))
                    ValidateMergedVehicleAvailabilityLocationInvariant(previousState, item, itemContext, issues);
            }

            if (!string.IsNullOrWhiteSpace(vehicleId) && simulatedVehicleStates != null)
                simulatedVehicleStates[vehicleId!] = MergeVehicleStateSnapshot(
                    simulatedVehicleStates.TryGetValue(vehicleId!, out var previousState) ? previousState : null,
                    item);
        }
    }

    private Dictionary<string, VehicleStateSnapshot>? TryResolvePreTurnVehicleStateMapSync()
        => TryReadVehicleStateMapFromJson(ReadPreTurnTrackedFileSync("game_state/misc/vehicles.json"));

    private static Dictionary<string, VehicleStateSnapshot>? TryReadVehicleStateMapFromJson(string? json)
    {
        if (string.IsNullOrWhiteSpace(json))
            return null;

        try
        {
            using var doc = JsonDocument.Parse(json);
            if (!doc.RootElement.TryGetProperty("vehicles", out var vehicles) || vehicles.ValueKind != JsonValueKind.Array)
                return null;

            var map = new Dictionary<string, VehicleStateSnapshot>(StringComparer.OrdinalIgnoreCase);
            foreach (var vehicle in vehicles.EnumerateArray())
            {
                if (vehicle.ValueKind != JsonValueKind.Object)
                    continue;

                var vehicleId = GetFirstNonEmptyString(vehicle, "vehicleId");
                if (string.IsNullOrWhiteSpace(vehicleId))
                    continue;

                map[vehicleId] = ReadVehicleStateSnapshot(vehicle);
            }

            return map;
        }
        catch
        {
            return null;
        }
    }

    private static VehicleStateSnapshot ReadVehicleStateSnapshot(JsonElement vehicle)
    {
        var hasCurrentLocationNode = vehicle.TryGetProperty("currentLocationId", out var currentLocationNode);
        return new VehicleStateSnapshot
        {
            Availability = GetFirstNonEmptyString(vehicle, "availability"),
            HasCurrentLocationNode = hasCurrentLocationNode,
            CurrentLocationExplicitNull = hasCurrentLocationNode && currentLocationNode.ValueKind == JsonValueKind.Null,
            CurrentLocationId = hasCurrentLocationNode && currentLocationNode.ValueKind == JsonValueKind.String
                ? currentLocationNode.GetString()
                : null
        };
    }

    private static VehicleStateSnapshot MergeVehicleStateSnapshot(VehicleStateSnapshot? previousState, JsonElement patch)
    {
        var hasCurrentLocationNode = patch.TryGetProperty("currentLocationId", out var currentLocationNode);
        return new VehicleStateSnapshot
        {
            Availability = GetFirstNonEmptyString(patch, "availability") ?? previousState?.Availability,
            HasCurrentLocationNode = hasCurrentLocationNode || previousState?.HasCurrentLocationNode == true,
            CurrentLocationExplicitNull = hasCurrentLocationNode
                ? currentLocationNode.ValueKind == JsonValueKind.Null
                : previousState?.CurrentLocationExplicitNull == true,
            CurrentLocationId = hasCurrentLocationNode
                ? currentLocationNode.ValueKind == JsonValueKind.String ? currentLocationNode.GetString() : null
                : previousState?.CurrentLocationId
        };
    }

    private static void ValidateMergedVehicleAvailabilityLocationInvariant(
        VehicleStateSnapshot previousState,
        JsonElement patch,
        string itemContext,
        List<ValidationIssue> issues)
    {
        var merged = MergeVehicleStateSnapshot(previousState, patch);
        var availability = merged.Availability;
        var currentLocationId = merged.CurrentLocationId;

        if ((string.Equals(availability, "Active", StringComparison.OrdinalIgnoreCase) ||
             string.Equals(availability, "Pocket", StringComparison.OrdinalIgnoreCase)) &&
            !string.IsNullOrWhiteSpace(currentLocationId))
        {
            issues.Add(new ValidationIssue(
                $"{itemContext}.currentLocationId",
                IssueSeverity.Error,
                "Partial UpdateVehicles нарушает canonical invariant: Active/Pocket vehicle не может иметь currentLocationId",
                code: "vehicle_partial_update_active_or_pocket_with_location",
                section: "Vehicles",
                expected: "merged vehicle state with currentLocationId = null for Active/Pocket availability",
                actual: $"availability={availability}, currentLocationId={currentLocationId}",
                repairHint: "Если транспорт становится Active или Pocket, одновременно обнули currentLocationId. Если транспорт остаётся Parked, не присваивай ему Active/Pocket availability в partial update."));
        }

        if (string.Equals(availability, "Parked", StringComparison.OrdinalIgnoreCase) &&
            (!merged.HasCurrentLocationNode || merged.CurrentLocationExplicitNull || string.IsNullOrWhiteSpace(currentLocationId)))
        {
            issues.Add(new ValidationIssue(
                $"{itemContext}.currentLocationId",
                IssueSeverity.Error,
                "Partial UpdateVehicles нарушает canonical invariant: Parked vehicle обязан иметь non-null currentLocationId",
                code: "vehicle_partial_update_parked_missing_location",
                section: "Vehicles",
                expected: "merged vehicle state with non-null currentLocationId for Parked availability",
                actual: merged.HasCurrentLocationNode
                    ? merged.CurrentLocationExplicitNull ? "null" : "missing/empty"
                    : "missing",
                repairHint: "Если транспорт остаётся или становится Parked, одновременно передай currentLocationId существующей локации. Если переносишь его в Active/Pocket, измени availability и обнули currentLocationId в этом же partial update."));
        }
    }

    private void ValidateStorageAccessCommandArray(JsonElement root, string contextPrefix, List<ValidationIssue> issues, string propName)
    {
        if (!root.TryGetProperty(propName, out var value))
            return;

        var context = $"{contextPrefix}.{propName}";
        RequireArrayOfObjects(value, context, issues);
        if (value.ValueKind != JsonValueKind.Array)
            return;

        var index = 0;
        foreach (var item in value.EnumerateArray())
        {
            var itemContext = $"{context}[{index++}]";
            if (!RequireObject(item, itemContext, issues))
                continue;

            var missingFields = GetMissingRequiredNonEmptyStringProperties(item, "storageId", "targetPlayerId", "targetPlayerName");
            if (missingFields.Count > 0)
            {
                issues.Add(new ValidationIssue(
                    itemContext,
                    IssueSeverity.Error,
                    $"{propName} object не содержит обязательные поля доступа к хранилищу",
                    code: "storage_access_missing_required_fields",
                    section: "StorageAccess",
                    expected: "storageId + targetPlayerId + targetPlayerName",
                    actual: string.Join(", ", missingFields),
                    repairHint: "Передавай storageId хранилища и полные targetPlayerId/targetPlayerName для grant/share/revoke storage access commands."));
            }
        }
    }

}

