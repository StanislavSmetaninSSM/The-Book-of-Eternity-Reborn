using System.Text.Json;
using System.Text.Json.Nodes;
using BookOfEternityClient.Core;
using Microsoft.Extensions.Logging;

namespace BookOfEternityClient.Services;

public partial class CanonicalStateNormalizer
{
    private async Task NormalizeSoulStateAsync(IReadOnlyDictionary<string, string>? backups)
    {
        const string path = "game_state/meta/soul_state.json";
        var current = await ReadObjectAsync(path);
        if (current == null) return;

        var previous = await ReadBackupObjectAsync(path, backups);
        var result = CloneObject(previous ?? new JsonObject());
        MergeObject(result, current);

        NormalizeInkFeathersShape(result);
        NormalizeSoulRelicsShape(result);
        AfterlifeArchiveState.NormalizeShape(result);
        var currentTurn = await TryReadCurrentTurnNumberAsync();

        if (current["metaStateUpdates"] is JsonObject updates)
            ApplyMetaStateUpdates(result, updates);
        if (current["afterlifeArchiveUpdates"] is JsonArray archiveUpdates)
            AfterlifeArchiveState.ApplyUpdates(result, archiveUpdates);
        if (current["archiveActionResolutions"] is JsonArray archiveActionResolutions)
            AfterlifeArchiveState.ApplyActionResolutions(result, archiveActionResolutions, currentTurn);

        result.Remove("metaStateUpdates");
        result.Remove("afterlifeArchiveUpdates");
        result.Remove("archiveActionResolutions");
        await WriteIfChangedAsync(path, current, result);
    }

    private async Task NormalizeAchievementsAsync(IReadOnlyDictionary<string, string>? backups)
    {
        const string path = "game_state/meta/achievements.json";
        var current = await ReadObjectAsync(path);
        if (current == null) return;

        var previous = await ReadBackupObjectAsync(path, backups);
        var result = CloneObject(previous ?? new JsonObject());
        MergeObject(result, current);

        var unlocked = new List<JsonObject>();
        var tracked = new List<JsonObject>();

        CollectAchievementObjects(previous, "unlockedAchievements", unlocked);
        CollectAchievementObjects(current, "unlockedAchievements", unlocked);
        CollectAchievementObjects(previous, "trackedProgress", tracked);
        CollectAchievementObjects(current, "trackedProgress", tracked);

        if (current["achievementUnlocks"] is JsonArray unlockCommands)
        {
            foreach (var unlock in unlockCommands.OfType<JsonObject>())
                UpsertByIdentity(unlocked, unlock, "achievementId", "name");
        }

        var unlockedIds = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        foreach (var achievement in unlocked)
        {
            var key = GetNodeString(achievement["achievementId"]) ?? GetNodeString(achievement["name"]);
            if (!string.IsNullOrWhiteSpace(key))
                unlockedIds.Add(key);
        }

        tracked = tracked
            .Where(item =>
            {
                var key = GetNodeString(item["achievementId"]) ?? GetNodeString(item["name"]);
                return string.IsNullOrWhiteSpace(key) || !unlockedIds.Contains(key);
            })
            .ToList();

        result["unlockedAchievements"] = ToArray(unlocked);
        result["trackedProgress"] = ToArray(tracked);
        result["stats"] = BuildAchievementStats(unlocked);
        result.Remove("achievementUnlocks");

        await WriteIfChangedAsync(path, current, result);
    }

    private async Task NormalizeCodexAsync(IReadOnlyDictionary<string, string>? backups)
    {
        const string path = "lore/codex_entries.json";
        var current = await ReadObjectAsync(path);
        if (current == null) return;

        var previous = await ReadBackupObjectAsync(path, backups);
        var result = CloneObject(previous ?? new JsonObject());
        MergeObject(result, current);

        var entries = new List<JsonObject>();
        CollectCodexEntries(previous, entries);
        CollectCodexEntries(current, entries);

        if (current["loreCodexUpdates"] is JsonArray updates)
            ApplyCodexUpdates(entries, updates);

        result["entries"] = ToArray(entries);
        result["totalEntries"] = entries.Count;
        result["categories"] = BuildCodexCategoryStats(entries);
        result.Remove("loreCodexUpdates");

        await WriteIfChangedAsync(path, current, result);
    }

}

