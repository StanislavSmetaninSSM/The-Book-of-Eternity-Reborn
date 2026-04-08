using System.Text.Json;
using System.Text.Json.Nodes;
using BookOfEternityClient.Core;
using Microsoft.Extensions.Logging;

namespace BookOfEternityClient.Services;

public partial class CanonicalStateNormalizer
{
    private static JsonObject BuildNormalizedSoulStateRoot(
        JsonObject current,
        JsonObject? previous,
        int currentTurn)
    {
        var result = CloneObject(previous ?? new JsonObject());
        MergeObject(result, current);

        NormalizeInkFeathersShape(result);
        NormalizeSoulRelicsShape(result);
        AfterlifeArchiveState.NormalizeShape(result);

        if (current["metaStateUpdates"] is JsonObject updates)
            ApplyMetaStateUpdates(result, updates);
        if (current["afterlifeArchiveUpdates"] is JsonArray archiveUpdates)
            AfterlifeArchiveState.ApplyUpdates(result, archiveUpdates);
        if (current["archiveActionResolutions"] is JsonArray archiveActionResolutions)
            AfterlifeArchiveState.ApplyActionResolutions(result, archiveActionResolutions, currentTurn);

        result.Remove("metaStateUpdates");
        result.Remove("afterlifeArchiveUpdates");
        result.Remove("archiveActionResolutions");
        return result;
    }

    private static bool TryReadGuardianProjectAuthoritySoulStateRoot(
        string? soulStateJson,
        bool required,
        out JsonObject? root,
        out string? failureDescription)
    {
        root = null;
        failureDescription = null;

        if (string.IsNullOrWhiteSpace(soulStateJson))
        {
            if (!required)
                return true;

            failureDescription = GuardianProjectCurrentSoulStateReadableRequiredMessage;
            return false;
        }

        try
        {
            if (JsonNode.Parse(soulStateJson) is JsonObject parsedRoot)
            {
                root = parsedRoot;
                return true;
            }
        }
        catch
        {
        }

        if (!required)
            return true;

        failureDescription = GuardianProjectCurrentSoulStateReadableRequiredMessage;
        return false;
    }

    private static bool HasCompleteGuardianProjectCurrentIncarnation(JsonObject soulStateRoot)
    {
        return soulStateRoot["currentIncarnation"] is JsonValue value && value.TryGetValue<int>(out _);
    }

    private static bool HasCompleteGuardianProjectAuthoritySoulContext(
        JsonObject soulStateRoot,
        GuardianProjectSoulContextRequirements requirements)
    {
        if (requirements.RequiresCurrentIncarnation &&
            !HasCompleteGuardianProjectCurrentIncarnation(soulStateRoot))
        {
            return false;
        }

        if (requirements.RequiresCurrentRealm &&
            string.IsNullOrWhiteSpace(GetNodeString(soulStateRoot["currentRealm"])))
        {
            return false;
        }

        return true;
    }

    internal static bool TryResolveGuardianProjectAuthoritySoulContext(
        string? currentSoulStateJson,
        string? preTurnSoulStateJson,
        int currentTurn,
        GuardianProjectSoulContextRequirements requirements,
        out int currentIncarnation,
        out string? currentRealm,
        out string failureDescription)
    {
        currentIncarnation = 0;
        currentRealm = null;
        failureDescription = string.Empty;

        if (!TryReadGuardianProjectAuthoritySoulStateRoot(
                currentSoulStateJson,
                requirements.RequiresReadableCurrentSoulState,
                out var currentRoot,
                out var currentFailureDescription))
        {
            failureDescription = currentFailureDescription ?? GuardianProjectCurrentSoulStateReadableRequiredMessage;
            return false;
        }

        if (currentRoot == null)
            return true;

        TryReadGuardianProjectAuthoritySoulStateRoot(
            preTurnSoulStateJson,
            required: false,
            out var preTurnRoot,
            out _);
        var soulStateRoot = BuildNormalizedSoulStateRoot(currentRoot, preTurnRoot, currentTurn);
        if (requirements.RequiresReadableCurrentSoulState &&
            !HasCompleteGuardianProjectAuthoritySoulContext(soulStateRoot, requirements))
        {
            failureDescription = GuardianProjectCurrentSoulStateReadableRequiredMessage;
            return false;
        }

        currentIncarnation = GetNodeInt(soulStateRoot["currentIncarnation"], 0);
        currentRealm = GetNodeString(soulStateRoot["currentRealm"]);
        return true;
    }

    private async Task<JsonObject?> BuildNormalizedSoulStateRootAsync(
        IReadOnlyDictionary<string, string>? backups,
        JsonObject? current = null)
    {
        const string path = "game_state/meta/soul_state.json";
        current ??= await ReadObjectAsync(path);
        if (current == null) return null;

        var previous = await ReadBackupObjectAsync(path, backups);
        var currentTurn = await TryReadCurrentTurnNumberAsync();
        return BuildNormalizedSoulStateRoot(current, previous, currentTurn);
    }

    private async Task<(int CurrentIncarnation, string? CurrentRealm)> ReadEffectiveGuardianProjectSoulContextAsync(
        IReadOnlyDictionary<string, string>? backups,
        GuardianProjectSoulContextRequirements requirements,
        JsonObject? currentSoulStateRoot = null)
    {
        var soulStateRoot = await BuildNormalizedSoulStateRootAsync(backups, currentSoulStateRoot);
        if (requirements.RequiresReadableCurrentSoulState &&
            (soulStateRoot == null || !HasCompleteGuardianProjectAuthoritySoulContext(soulStateRoot, requirements)))
        {
            throw new InvalidOperationException(GuardianProjectCurrentSoulStateReadableRequiredMessage);
        }

        return (
            GetNodeInt(soulStateRoot?["currentIncarnation"], 0),
            GetNodeString(soulStateRoot?["currentRealm"]));
    }

    private async Task NormalizeSoulStateAsync(IReadOnlyDictionary<string, string>? backups)
    {
        const string path = "game_state/meta/soul_state.json";
        var current = await ReadObjectAsync(path);
        if (current == null) return;

        var result = await BuildNormalizedSoulStateRootAsync(backups, current);
        if (result == null) return;

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

