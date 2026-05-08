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
    private sealed class ReasoningScopeManifest
    {
        public string Mode { get; set; } = "";
        public bool HasModeField { get; set; }
        public List<string> RelevantActors { get; } = new();
        public bool HasRelevantActorsField { get; set; }
        public string WhyRelevant { get; set; } = "";
        public bool HasWhyRelevantField { get; set; }
        public List<string> OutOfScopeActors { get; } = new();
        public bool HasOutOfScopeActorsField { get; set; }
        public string OutOfScopeReason { get; set; } = "";
        public bool HasOutOfScopeReasonField { get; set; }
    }

    private sealed class StructuredActorUpdate
    {
        public string ActorType { get; set; } = "Actor";
        public string FilePath { get; set; } = "";
        public string Section { get; set; } = "";
        public string DisplayName { get; set; } = "";
        public bool HasResolvedName { get; set; }
        public HashSet<string> Aliases { get; } = new(StringComparer.OrdinalIgnoreCase);
    }

    private sealed class StructuredActorExtractionResult
    {
        public List<StructuredActorUpdate> Updates { get; } = new();
        public bool DirectCanonicalGuardianDiffRequiredButSnapshotMissing { get; set; }
    }

    private sealed class GuardianReasoningIdentityContext
    {
        public bool GuardianStateReadable { get; set; } = true;
        public bool HasActiveGuardianMirror { get; set; }
        public bool HasCanonicalActiveGuardian { get; set; }
        public GuardianReasoningActiveGuardianStatus ActiveGuardianStatus { get; set; }
        public bool HasUsableValidatedPreTurnGuardiansSnapshot { get; set; }
        public HashSet<string> ActiveGuardianNames { get; } = new(StringComparer.OrdinalIgnoreCase);
        public HashSet<string> CanonicalGuardianAliases { get; } = new(StringComparer.OrdinalIgnoreCase);
        public Dictionary<string, List<string>> CanonicalGuardianAliasLookup { get; } = new(StringComparer.OrdinalIgnoreCase);
        public HashSet<string> BaselineGuardianIds { get; } = new(StringComparer.OrdinalIgnoreCase);
        public HashSet<string> AuthoritativeGuardianIds { get; } = new(StringComparer.OrdinalIgnoreCase);
        public HashSet<string> MirrorGuardianAliases { get; } = new(StringComparer.OrdinalIgnoreCase);
    }

    private static readonly HashSet<string> NpcStructuredSingleActorSections = new(StringComparer.OrdinalIgnoreCase)
    {
        GuardianPolicyContracts.NpcCoreUpdateSectionName,
        NpcTradeRequestState.UpdateReceiptsProperty,
        "NPCGoalUpdates",
        "NPCQuestUpdates",
        "NPCRelationshipChanges",
        "NPCRelationshipLockUpdates",
        "NPCEffectChanges",
        "NPCWoundChanges",
        "NPCPersonalityTraitChanges",
        "NPCActivityUpdates",
        "completeNPCActivities",
        "NPCMaskAdds",
        "NPCMaskUpdates",
        "NPCMaskRemovals",
        "NPCActiveMaskChange",
        "NPCFateCardUnlocks",
        "NPCCustomStateChanges",
        "NPCJournals",
        "NPCUnlockedMemories",
        "NPCActiveSkillChanges",
        "NPCPassiveSkillChanges",
        "NPCSkillMasteryChanges",
        "NPCPassiveSkillMasteryChanges",
        "NPCInventoryAdds",
        "NPCInventoryUpdates",
        "NPCInventoryRemovals",
        "NPCEquipmentChanges",
        "NPCInventoryResourcesChanges"
    };

    private static readonly HashSet<string> NpcStructuredSpecialSections = new(StringComparer.OrdinalIgnoreCase)
    {
        GuardianPolicyContracts.NpcCoreRenameSectionName,
        "interNPCRelationshipChanges"
    };

    private const string GuardianStructuredUpdateSection = "UpdateGuardians";

    private enum ReasoningScopeMode
    {
        Unknown,
        SceneLocal,
        WorldProgression,
        GuardianCentric,
        Mixed
    }

    private enum GuardianReasoningActiveGuardianStatus
    {
        NoActiveGuardian,
        MirrorMissingCanonical,
        CanonicalResolved,
        GuardianStateUnreadable
    }

    private static bool TryParseReasoningScope(string thoughts, out ReasoningScopeManifest scope)
    {
        scope = new ReasoningScopeManifest();
        var lines = thoughts.Replace("\r\n", "\n").Split('\n');
        var start = -1;
        for (var i = 0; i < lines.Length; i++)
        {
            var trimmed = lines[i].Trim();
            if (!trimmed.StartsWith("##", StringComparison.Ordinal))
                continue;

            if (trimmed.Contains("охват npc", StringComparison.OrdinalIgnoreCase) ||
                trimmed.Contains("npc scope", StringComparison.OrdinalIgnoreCase))
            {
                start = i + 1;
                break;
            }
        }

        if (start < 0)
            return false;

        for (var i = start; i < lines.Length; i++)
        {
            var trimmed = lines[i].Trim();
            if (trimmed.StartsWith("##", StringComparison.Ordinal))
                break;

            var normalized = trimmed.TrimStart('-', '*', ' ').Trim();
            if (normalized.Length == 0)
                continue;

            if (!TrySplitScopeField(normalized, out var label, out var value))
                continue;

            if (LabelContainsAny(label, "режим", "mode"))
            {
                scope.HasModeField = true;
                scope.Mode = value;
                continue;
            }

            if (LabelContainsAny(label, "релевантные акторы", "relevant actors"))
            {
                scope.HasRelevantActorsField = true;
                scope.RelevantActors.AddRange(ParseActorList(value));
                continue;
            }

            if (LabelContainsAny(label, "почему они релевантны", "почему релевантны", "why they are relevant", "why relevant"))
            {
                scope.HasWhyRelevantField = true;
                scope.WhyRelevant = value;
                continue;
            }

            if (LabelContainsAny(label, "акторы вне охвата", "actors outside scope", "actors out of scope", "out-of-scope actors"))
            {
                scope.HasOutOfScopeActorsField = true;
                scope.OutOfScopeActors.AddRange(ParseActorList(value));
                continue;
            }

            if (LabelContainsAny(label, "почему они вне охвата", "почему вне охвата", "why they are outside scope", "why outside scope"))
            {
                scope.HasOutOfScopeReasonField = true;
                scope.OutOfScopeReason = value;
            }
        }

        var distinctRelevantActors = scope.RelevantActors.Distinct(StringComparer.OrdinalIgnoreCase).ToList();
        scope.RelevantActors.Clear();
        scope.RelevantActors.AddRange(distinctRelevantActors);

        var distinctOutOfScopeActors = scope.OutOfScopeActors.Distinct(StringComparer.OrdinalIgnoreCase).ToList();
        scope.OutOfScopeActors.Clear();
        scope.OutOfScopeActors.AddRange(distinctOutOfScopeActors);

        return true;
    }

    private static bool TrySplitScopeField(string normalized, out string label, out string value)
    {
        var separatorIndex = normalized.IndexOf(':');
        if (separatorIndex < 0)
        {
            label = "";
            value = "";
            return false;
        }

        label = normalized[..separatorIndex].Trim();
        value = normalized[(separatorIndex + 1)..].Trim();
        return true;
    }

    private static bool LabelContainsAny(string label, params string[] fragments)
    {
        var lower = label.ToLowerInvariant();
        return fragments.Any(fragment => lower.Contains(fragment.ToLowerInvariant(), StringComparison.Ordinal));
    }

    private static ReasoningScopeMode ParseReasoningScopeMode(string mode)
    {
        if (string.IsNullOrWhiteSpace(mode))
            return ReasoningScopeMode.Unknown;

        var normalized = mode.Trim().ToLowerInvariant();
        if (normalized.Contains("scene"))
            return ReasoningScopeMode.SceneLocal;
        if (normalized.Contains("world"))
            return ReasoningScopeMode.WorldProgression;
        if (normalized.Contains("guardian"))
            return ReasoningScopeMode.GuardianCentric;
        if (normalized.Contains("mixed"))
            return ReasoningScopeMode.Mixed;
        return ReasoningScopeMode.Unknown;
    }

    private static IEnumerable<string> ParseActorList(string value)
    {
        var trimmed = value.Trim();
        if (trimmed.Length == 0 ||
            string.Equals(trimmed, "нет", StringComparison.OrdinalIgnoreCase) ||
            string.Equals(trimmed, "none", StringComparison.OrdinalIgnoreCase) ||
            string.Equals(trimmed, "n/a", StringComparison.OrdinalIgnoreCase))
        {
            return Array.Empty<string>();
        }

        var cleaned = trimmed.Trim('[', ']');
        return cleaned
            .Split(new[] { ',', ';' }, StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries)
            .Where(item =>
                !string.Equals(item, "нет", StringComparison.OrdinalIgnoreCase) &&
                !string.Equals(item, "none", StringComparison.OrdinalIgnoreCase));
    }

    private async Task<string> ResolvePreTurnRealmAsync()
    {
        return await TryResolvePreTurnRealmAsync() ?? string.Empty;
    }

    private async Task<string?> TryResolvePreTurnRealmAsync()
    {
        var validatedManifest = await LoadValidatedCurrentPendingTurnSnapshotManifestAsync();
        if (validatedManifest != null)
        {
            return await TryReadValidatedPendingTurnSnapshotRealmAsync(validatedManifest);
        }

        return null;
    }

    private async Task<int> ReadCurrentIncarnationAsync()
    {
        var soulJson = await _fs.ReadFileAsync("game_state/meta/soul_state.json");
        if (string.IsNullOrWhiteSpace(soulJson))
            return 0;

        try
        {
            using var doc = JsonDocument.Parse(soulJson);
            return doc.RootElement.TryGetProperty("currentIncarnation", out var incarnation) &&
                   incarnation.ValueKind == JsonValueKind.Number &&
                   incarnation.TryGetInt32(out var parsed)
                ? parsed
                : 0;
        }
        catch
        {
            return 0;
        }
    }

    private async Task<bool> ChatLogHasTurnsAsync()
    {
        var chatJson = await _fs.ReadFileAsync("game_state/history/chat_log.json");
        if (string.IsNullOrWhiteSpace(chatJson))
            return await StoriesHaveEntriesAsync();

        try
        {
            using var doc = JsonDocument.Parse(chatJson);
            if (doc.RootElement.TryGetProperty("turns", out var turns) &&
                turns.ValueKind == JsonValueKind.Array &&
                turns.GetArrayLength() > 0)
            {
                return true;
            }
        }
        catch
        {
            // ignored
        }

        return await StoriesHaveEntriesAsync();
    }

    private Task<bool> StoriesHaveEntriesAsync()
    {
        try
        {
            var storiesPath = _fs.ResolvePath("stories");
            if (!Directory.Exists(storiesPath))
                return Task.FromResult(false);

            foreach (var file in Directory.EnumerateFiles(storiesPath, "*.jsonl", SearchOption.TopDirectoryOnly))
            {
                if (File.ReadLines(file).Any(line => !string.IsNullOrWhiteSpace(line)))
                    return Task.FromResult(true);
            }
        }
        catch
        {
            // ignored
        }

        return Task.FromResult(false);
    }

    private void ValidateRequiredBootstrapFileExists(string relativePath, List<ValidationIssue> issues)
    {
        if (_fs.FileExists(relativePath))
            return;

        issues.Add(new ValidationIssue(
            relativePath,
            IssueSeverity.Error,
            $"Отсутствует обязательный bootstrap file: {relativePath}",
            code: "missing_lore_bootstrap_file",
            section: "LoreBootstrap",
            expected: relativePath,
            actual: "missing",
            repairHint: "Создай обязательные lore/codex/achievement bootstrap files для текущего realm before accepting the turn."));
    }

    private static bool IsLoreBootstrapPendingTransitionSource(string? sourceLabel)
    {
        return string.Equals(sourceLabel, "воплощения", StringComparison.OrdinalIgnoreCase) ||
               string.Equals(sourceLabel, "GM-инициированного воплощения", StringComparison.OrdinalIgnoreCase);
    }

    private static bool IsRealmTransitionSourceLabel(string? sourceLabel)
    {
        return string.Equals(sourceLabel, "воплощения", StringComparison.OrdinalIgnoreCase) ||
               string.Equals(sourceLabel, "GM-инициированного воплощения", StringComparison.OrdinalIgnoreCase) ||
               LifeEvaluationRewardAnalyzer.IsLifeEvaluationSourceLabel(sourceLabel);
    }

    private static bool IsForbiddenChaosSeaChangedFile(string relativePath)
    {
        var normalized = relativePath.Replace('\\', '/');
        return normalized.Equals("game_state/core/player_status.json", StringComparison.OrdinalIgnoreCase) ||
               normalized.StartsWith("game_state/player/", StringComparison.OrdinalIgnoreCase) ||
               normalized.StartsWith("game_state/inventory/", StringComparison.OrdinalIgnoreCase) ||
               normalized.StartsWith("game_state/world/", StringComparison.OrdinalIgnoreCase) ||
               normalized.StartsWith("game_state/npcs/", StringComparison.OrdinalIgnoreCase) ||
               normalized.StartsWith("game_state/combat/", StringComparison.OrdinalIgnoreCase) ||
               normalized.StartsWith("game_state/factions/", StringComparison.OrdinalIgnoreCase) ||
               normalized.StartsWith("lore/current_world/", StringComparison.OrdinalIgnoreCase) ||
               normalized.Equals("game_state/quests/regular_quests.json", StringComparison.OrdinalIgnoreCase) ||
               normalized.Equals("game_state/quests/quest_history.json", StringComparison.OrdinalIgnoreCase) ||
               normalized.Equals("game_state/quests/plot_outline.json", StringComparison.OrdinalIgnoreCase) ||
               normalized.Equals("game_state/misc/characteristics.json", StringComparison.OrdinalIgnoreCase) ||
               normalized.Equals("game_state/misc/vehicles.json", StringComparison.OrdinalIgnoreCase) ||
               normalized.Equals("game_state/misc/storage_access.json", StringComparison.OrdinalIgnoreCase) ||
               normalized.Equals("game_state/misc/player_interactions.json", StringComparison.OrdinalIgnoreCase);
    }

    private static bool IsForbiddenMortalWorldChangedFile(string relativePath)
    {
        var normalized = relativePath.Replace('\\', '/');
        return normalized.Equals("game_state/meta/guardians.json", StringComparison.OrdinalIgnoreCase) ||
               normalized.Equals(AfterlifeSpiritualConflictState.StatePath, StringComparison.OrdinalIgnoreCase) ||
               normalized.StartsWith("lore/chaos_sea/", StringComparison.OrdinalIgnoreCase);
    }

    private static List<string> DescribeRealmSegregationGroups(IEnumerable<string> relativePaths)
    {
        var groups = new HashSet<string>(StringComparer.OrdinalIgnoreCase);

        foreach (var relativePath in relativePaths)
        {
            var normalized = relativePath.Replace('\\', '/');
            if (normalized.Equals("game_state/core/player_status.json", StringComparison.OrdinalIgnoreCase))
            {
                groups.Add("player core status");
            }
            else if (normalized.StartsWith("game_state/player/", StringComparison.OrdinalIgnoreCase))
            {
                groups.Add("player progression/state");
            }
            else if (normalized.StartsWith("game_state/inventory/", StringComparison.OrdinalIgnoreCase))
            {
                groups.Add("inventory");
            }
            else if (normalized.StartsWith("game_state/world/", StringComparison.OrdinalIgnoreCase))
            {
                groups.Add("world state");
            }
            else if (normalized.StartsWith("game_state/npcs/", StringComparison.OrdinalIgnoreCase))
            {
                groups.Add("NPC state");
            }
            else if (normalized.StartsWith("game_state/combat/", StringComparison.OrdinalIgnoreCase))
            {
                groups.Add("combat");
            }
            else if (normalized.StartsWith("game_state/factions/", StringComparison.OrdinalIgnoreCase))
            {
                groups.Add("factions");
            }
            else if (normalized.StartsWith("lore/current_world/", StringComparison.OrdinalIgnoreCase))
            {
                groups.Add("current-world lore");
            }
            else if (normalized.Equals("game_state/quests/regular_quests.json", StringComparison.OrdinalIgnoreCase) ||
                     normalized.Equals("game_state/quests/quest_history.json", StringComparison.OrdinalIgnoreCase) ||
                     normalized.Equals("game_state/quests/plot_outline.json", StringComparison.OrdinalIgnoreCase))
            {
                groups.Add("quest state");
            }
            else if (normalized.Equals("game_state/misc/characteristics.json", StringComparison.OrdinalIgnoreCase))
            {
                groups.Add("mortal characteristics");
            }
            else if (normalized.Equals("game_state/misc/vehicles.json", StringComparison.OrdinalIgnoreCase) ||
                     normalized.Equals("game_state/misc/storage_access.json", StringComparison.OrdinalIgnoreCase) ||
                     normalized.Equals("game_state/misc/player_interactions.json", StringComparison.OrdinalIgnoreCase))
            {
                groups.Add("mortal misc systems");
            }
            else if (normalized.Equals("game_state/meta/guardians.json", StringComparison.OrdinalIgnoreCase))
            {
                groups.Add("guardian state");
            }
            else if (normalized.Equals(AfterlifeSpiritualConflictState.StatePath, StringComparison.OrdinalIgnoreCase))
            {
                groups.Add("afterlife spiritual conflict state");
            }
            else if (normalized.StartsWith("lore/chaos_sea/", StringComparison.OrdinalIgnoreCase))
            {
                groups.Add("Chaos Sea lore");
            }
            else
            {
                groups.Add(normalized);
            }
        }

        return groups.OrderBy(group => group, StringComparer.OrdinalIgnoreCase).ToList();
    }

    private static void CollectQteSuccessOutcomeIds(
        string chapterId,
        IReadOnlyDictionary<string, JsonElement> chapters,
        ISet<string> outcomeIds,
        ISet<string> visitedChapters)
    {
        if (!visitedChapters.Add(chapterId))
            return;
        if (!chapters.TryGetValue(chapterId, out var chapter))
            return;
        if (!chapter.TryGetProperty("actions", out var actions) || actions.ValueKind != JsonValueKind.Array)
            return;

        foreach (var action in actions.EnumerateArray())
        {
            if (!action.TryGetProperty("routing", out var routing) || routing.ValueKind != JsonValueKind.Object)
                continue;
            if (!routing.TryGetProperty("success", out var successBranch) || successBranch.ValueKind != JsonValueKind.Object)
                continue;

            var nextChapterId = GetFirstNonEmptyString(successBranch, "nextChapterId");
            var terminalOutcomeId = GetFirstNonEmptyString(successBranch, "terminalOutcomeId");
            if (!string.IsNullOrWhiteSpace(terminalOutcomeId))
                outcomeIds.Add(terminalOutcomeId);
            if (!string.IsNullOrWhiteSpace(nextChapterId))
                CollectQteSuccessOutcomeIds(nextChapterId, chapters, outcomeIds, visitedChapters);
        }
    }

    private static void CollectReachableQteChapterIds(
        string chapterId,
        IReadOnlyDictionary<string, JsonElement> chapters,
        ISet<string> reachableChapterIds,
        ISet<string> visitedChapters)
    {
        if (!visitedChapters.Add(chapterId))
            return;
        if (!chapters.TryGetValue(chapterId, out var chapter))
            return;

        reachableChapterIds.Add(chapterId);
        if (!chapter.TryGetProperty("actions", out var actions) || actions.ValueKind != JsonValueKind.Array)
            return;

        foreach (var action in actions.EnumerateArray())
        {
            if (!action.TryGetProperty("routing", out var routing) || routing.ValueKind != JsonValueKind.Object)
                continue;

            foreach (var branchName in new[] { "success", "partial", "fail" })
            {
                if (!routing.TryGetProperty(branchName, out var branch) || branch.ValueKind != JsonValueKind.Object)
                    continue;

                var nextChapterId = GetFirstNonEmptyString(branch, "nextChapterId");
                if (!string.IsNullOrWhiteSpace(nextChapterId))
                    CollectReachableQteChapterIds(nextChapterId, chapters, reachableChapterIds, visitedChapters);
            }
        }
    }

    private async Task<string?> TryResolveCurrentRealmAsync()
    {
        var soulJson = await _fs.ReadFileAsync("game_state/meta/soul_state.json");
        if (string.IsNullOrWhiteSpace(soulJson))
            return null;

        try
        {
            using var doc = JsonDocument.Parse(soulJson);
            return doc.RootElement.TryGetProperty("currentRealm", out var realm) &&
                   realm.ValueKind == JsonValueKind.String &&
                   !string.IsNullOrWhiteSpace(realm.GetString())
                ? realm.GetString()
                : null;
        }
        catch
        {
            return null;
        }
    }

    private static bool TryReadLifeTransitionControlFile(string json)
    {
        return CanonicalStateNormalizer.TryReadCanonicalTriggerLifeEnd(json, out _, out _);
    }

    private static bool TryReadLifeTransitionControlPayload(JsonElement root, out string reason, out string summary)
    {
        return CanonicalStateNormalizer.TryReadCanonicalTriggerLifeEnd(root, out reason, out summary);
    }

    private static bool TryReadIncarnationControlFile(string json)
        => IncarnationTriggerContract.TryParse(json, out _);

    private static bool TryReadAscensionControlFile(string json)
    {
        try
        {
            using var doc = JsonDocument.Parse(json);
            if (doc.RootElement.ValueKind != JsonValueKind.Object)
                return false;

            var hasTrigger =
                doc.RootElement.TryGetProperty("AscensionTrigger", out var triggerNode) &&
                triggerNode.ValueKind == JsonValueKind.True;
            var playerChoice = GetFirstNonEmptyString(doc.RootElement, "playerChoice");
            return hasTrigger && string.Equals(playerChoice, "Ascension", StringComparison.OrdinalIgnoreCase);
        }
        catch
        {
            return false;
        }
    }

    private async Task<bool> ReadQteEnabledAsync()
    {
        var settingsJson = await _fs.ReadFileAsync("game_state/core/game_settings.json");
        if (string.IsNullOrWhiteSpace(settingsJson))
            return true;

        try
        {
            using var doc = JsonDocument.Parse(settingsJson);
            if (doc.RootElement.TryGetProperty("qteEventsEnabled", out var qteEnabled) &&
                qteEnabled.ValueKind == JsonValueKind.True)
                return true;
            if (doc.RootElement.TryGetProperty("qteEventsEnabled", out qteEnabled) &&
                qteEnabled.ValueKind == JsonValueKind.False)
                return false;
        }
        catch
        {
            // ignore and use permissive default
        }

        return true;
    }

    private static bool IsChaosSeaRealm(string? realm)
    {
        return string.Equals(realm, "Chaos Sea", StringComparison.OrdinalIgnoreCase) ||
               string.Equals(realm, "Shining Abode", StringComparison.OrdinalIgnoreCase) ||
               string.Equals(realm, "Море Хаоса", StringComparison.OrdinalIgnoreCase) ||
               string.Equals(realm, "Сияющая Обитель", StringComparison.OrdinalIgnoreCase);
    }

    private static bool IsExactChaosSeaRealm(string? realm)
    {
        return string.Equals(realm, "Chaos Sea", StringComparison.OrdinalIgnoreCase) ||
               string.Equals(realm, "Море Хаоса", StringComparison.OrdinalIgnoreCase);
    }

    private async Task<HashSet<string>> CollectImportantGuardianNamesAsync(string realm)
    {
        if (!IsChaosSeaRealm(realm))
            return new HashSet<string>(StringComparer.OrdinalIgnoreCase);

        var context = await ResolveGuardianReasoningIdentityContextAsync();
        return context.ActiveGuardianStatus == GuardianReasoningActiveGuardianStatus.CanonicalResolved
            ? new HashSet<string>(context.ActiveGuardianNames, StringComparer.OrdinalIgnoreCase)
            : new HashSet<string>(StringComparer.OrdinalIgnoreCase);
    }

    private async Task<GuardianReasoningIdentityContext> ResolveGuardianReasoningIdentityContextAsync()
    {
        var policyContext = await ResolveGuardianPolicyContextAsync();
        return BuildGuardianReasoningIdentityContext(policyContext);
    }

    private GuardianReasoningIdentityContext BuildGuardianReasoningIdentityContext(GuardianPolicyContext policyContext)
    {
        var context = new GuardianReasoningIdentityContext
        {
            GuardianStateReadable = policyContext.CurrentStateReadable,
            HasUsableValidatedPreTurnGuardiansSnapshot = policyContext.HasUsableValidatedPreTurnGuardiansSnapshot
        };

        foreach (var (guardianId, aliases) in policyContext.ReasoningAliasLookup)
        {
            context.CanonicalGuardianAliasLookup[guardianId] = new List<string>(aliases);
            foreach (var alias in aliases)
                context.CanonicalGuardianAliases.Add(alias);
        }

        foreach (var guardianId in policyContext.BaselineGuardianIds)
            context.BaselineGuardianIds.Add(guardianId);
        foreach (var guardianId in policyContext.AuthoritativeGuardianIds)
            context.AuthoritativeGuardianIds.Add(guardianId);

        if (!policyContext.CurrentStateReadable)
        {
            context.ActiveGuardianStatus = GuardianReasoningActiveGuardianStatus.GuardianStateUnreadable;
            return context;
        }

        if (policyContext.HasCurrentActiveGuardian)
        {
            context.HasActiveGuardianMirror = true;
            AddGuardianAliases(context.MirrorGuardianAliases, policyContext.CurrentActiveGuardian, includeGuardianId: false);
        }

        if (!TryGetCurrentAuthorityActiveGuardian(policyContext, out var authoritativeActiveGuardian))
        {
            context.ActiveGuardianStatus = context.HasActiveGuardianMirror
                ? GuardianReasoningActiveGuardianStatus.MirrorMissingCanonical
                : GuardianReasoningActiveGuardianStatus.NoActiveGuardian;
            return context;
        }

        var activeGuardianId = GetFirstNonEmptyString(authoritativeActiveGuardian, "guardianId", "id");
        if (string.IsNullOrWhiteSpace(activeGuardianId) ||
            !policyContext.ReasoningAliasLookup.TryGetValue(activeGuardianId, out var currentGuardianAliases))
        {
            context.ActiveGuardianStatus = GuardianReasoningActiveGuardianStatus.MirrorMissingCanonical;
            return context;
        }

        context.HasCanonicalActiveGuardian = true;
        context.ActiveGuardianStatus = GuardianReasoningActiveGuardianStatus.CanonicalResolved;
        foreach (var alias in currentGuardianAliases)
            context.ActiveGuardianNames.Add(alias);

        return context;
    }

    private async Task<StructuredActorExtractionResult> CollectStructuredActorUpdatesAsync(GuardianPolicyContext guardianPolicyContext)
    {
        var result = new StructuredActorExtractionResult();
        await CollectStructuredNpcUpdatesAsync(result.Updates);
        await CollectStructuredResidentUpdatesAsync(result.Updates);
        CollectStructuredGuardianUpdates(result, guardianPolicyContext);
        return result;
    }

    private async Task CollectStructuredNpcUpdatesAsync(List<StructuredActorUpdate> updates)
    {
        var npcJson = await _fs.ReadFileAsync("game_state/npcs/npc_core.json");
        if (string.IsNullOrWhiteSpace(npcJson))
            return;

        try
        {
            using var doc = JsonDocument.Parse(npcJson);
            if (doc.RootElement.ValueKind != JsonValueKind.Object)
                return;

            var aliasLookup = BuildNpcAliasLookup(doc.RootElement);

            foreach (var property in doc.RootElement.EnumerateObject())
            {
                if (property.Value.ValueKind != JsonValueKind.Array ||
                    (!NpcStructuredSingleActorSections.Contains(property.Name) &&
                     !NpcStructuredSpecialSections.Contains(property.Name)))
                    continue;

                foreach (var item in property.Value.EnumerateArray())
                {
                    if (property.Name.Equals("NPCsRenameData", StringComparison.OrdinalIgnoreCase))
                    {
                        updates.AddRange(CreateNpcRenameStructuredActorUpdates(item, property.Name));
                        continue;
                    }

                    if (property.Name.Equals("interNPCRelationshipChanges", StringComparison.OrdinalIgnoreCase))
                    {
                        updates.AddRange(CreateInterNpcRelationshipStructuredActorUpdates(item, property.Name, aliasLookup));
                        continue;
                    }

                    if (NpcStructuredSingleActorSections.Contains(property.Name) &&
                        TryCreateNpcStructuredActorUpdate(item, aliasLookup, property.Name, out var update))
                        updates.Add(update);
                }
            }
        }
        catch
        {
            // Ignore consistency extraction failures; generic validation will surface malformed JSON separately.
        }
    }

    private async Task CollectStructuredResidentUpdatesAsync(List<StructuredActorUpdate> updates)
    {
        var residentJson = await _fs.ReadFileAsync(GuardianAbodeResidentState.StatePath);
        if (string.IsNullOrWhiteSpace(residentJson))
            return;

        try
        {
            if (JsonNode.Parse(residentJson) is not JsonObject currentRoot)
                return;

            GuardianAbodeResidentState.NormalizeShape(currentRoot);
            var aliasLookup = BuildResidentAliasLookup(currentRoot);

            if (currentRoot[GuardianAbodeResidentState.UpdateProperty] is JsonArray residentUpdates)
            {
                foreach (var item in residentUpdates.OfType<JsonObject>())
                {
                    if (TryCreateResidentStructuredActorUpdate(item, aliasLookup, GuardianAbodeResidentState.UpdateProperty, out var update))
                        updates.Add(update);
                }
            }

            AddResidentJournalStructuredActorUpdates(currentRoot[GuardianAbodeResidentState.UpdateThoughtJournalProperty], aliasLookup, GuardianAbodeResidentState.UpdateThoughtJournalProperty, updates);
            AddResidentJournalStructuredActorUpdates(currentRoot[GuardianAbodeResidentState.UpdateInteractionLogProperty], aliasLookup, GuardianAbodeResidentState.UpdateInteractionLogProperty, updates);
            AddResidentJournalStructuredActorUpdates(currentRoot[GuardianAbodeResidentState.UpdateHistoryLogProperty], aliasLookup, GuardianAbodeResidentState.UpdateHistoryLogProperty, updates);
            AddResidentJournalStructuredActorUpdates(currentRoot[GuardianAbodeResidentState.UpdateInteractionReceiptsProperty], aliasLookup, GuardianAbodeResidentState.UpdateInteractionReceiptsProperty, updates);

            var preTurnResidentJson = await ReadValidatedCurrentPreTurnTrackedFileAsync(GuardianAbodeResidentState.StatePath);
            if (string.IsNullOrWhiteSpace(preTurnResidentJson))
                return;

            if (JsonNode.Parse(preTurnResidentJson) is not JsonObject preTurnRoot)
                return;

            GuardianAbodeResidentState.NormalizeShape(preTurnRoot);
            CollectResidentCanonicalDiffStructuredActorTouches(preTurnRoot, currentRoot, aliasLookup, updates);
        }
        catch
        {
            // Ignore consistency extraction failures; generic validation will surface malformed JSON separately.
        }
    }

    private void CollectStructuredGuardianUpdates(
        StructuredActorExtractionResult extractionResult,
        GuardianPolicyContext guardianPolicyContext)
    {
        if (!guardianPolicyContext.CurrentStateReadable || !guardianPolicyContext.HasCurrentRoot)
            return;

        try
        {
            var aliasLookup = guardianPolicyContext.ReasoningAliasLookup
                .Where(entry => guardianPolicyContext.AuthoritativeGuardianIds.Contains(entry.Key))
                .ToDictionary(entry => entry.Key, entry => new List<string>(entry.Value), StringComparer.OrdinalIgnoreCase);
            var authoritativeGuardianIds = new HashSet<string>(guardianPolicyContext.BaselineGuardianIds, StringComparer.OrdinalIgnoreCase);
            var scratchIssues = new List<ValidationIssue>();
            if (guardianPolicyContext.HasCurrentRoot &&
                guardianPolicyContext.CurrentRoot.TryGetProperty(GuardianStructuredUpdateSection, out var arr) &&
                arr.ValueKind == JsonValueKind.Array)
            {
                var commandIndex = 0;
                foreach (var item in arr.EnumerateArray())
                {
                    var commandContext = $"game_state/meta/guardians.json.{GuardianStructuredUpdateSection}[{commandIndex++}]";
                    if (TryCreateGuardianStructuredActorUpdate(item, aliasLookup, authoritativeGuardianIds, scratchIssues, commandContext, out var update))
                        extractionResult.Updates.Add(update);
                }
            }

            if (guardianPolicyContext.CurrentRoot.TryGetProperty("guardianPowerEvents", out var guardianPowerEvents) &&
                guardianPowerEvents.ValueKind == JsonValueKind.Array)
            {
                var eventIndex = 0;
                foreach (var evt in guardianPowerEvents.EnumerateArray())
                {
                    var eventContext = $"game_state/meta/guardians.json.guardianPowerEvents[{eventIndex++}]";
                    if (evt.ValueKind != JsonValueKind.Object)
                    {
                        extractionResult.Updates.Add(CreateUnresolvedGuardianStructuredActorUpdate(eventContext, "guardianPowerEvents"));
                        continue;
                    }

                    AddGuardianStructuredActorTouchFromReference(
                        extractionResult.Updates,
                        evt,
                        "guardianId",
                        "guardianPowerEvents",
                        eventContext,
                        aliasLookup,
                        authoritativeGuardianIds);
                    AddGuardianStructuredActorTouchFromReference(
                        extractionResult.Updates,
                        evt,
                        "relatedGuardianId",
                        "guardianPowerEvents",
                        eventContext,
                        aliasLookup,
                        authoritativeGuardianIds);
                }
            }

            if (guardianPolicyContext.CurrentRoot.TryGetProperty("guardians", out var currentGuardians) &&
                currentGuardians.ValueKind == JsonValueKind.Array)
            {
                if (!TryGetGenericSharedStrictPreTurnGuardianAuthorityRoot(guardianPolicyContext, out var preTurnAuthorityRoot))
                {
                    extractionResult.DirectCanonicalGuardianDiffRequiredButSnapshotMissing = true;
                    return;
                }

                CollectGuardianArrayDiffStructuredActorTouches(
                    extractionResult,
                    preTurnAuthorityRoot,
                    guardianPolicyContext.CurrentRoot,
                    aliasLookup,
                    authoritativeGuardianIds);
            }
        }
        catch
        {
            // Ignore consistency extraction failures; generic validation will surface malformed JSON separately.
        }
    }

    private static Dictionary<string, string> BuildNpcAliasLookup(JsonElement root)
    {
        var aliases = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
        foreach (var sectionName in GuardianPolicyContracts.NpcCoreCanonicalNpcObjectSections)
        {
            if (!root.TryGetProperty(sectionName, out var arr) || arr.ValueKind != JsonValueKind.Array)
                continue;

            foreach (var item in arr.EnumerateArray())
            {
                var npcId = GetFirstNonEmptyString(item, "NPCId", "npcId", "id");
                var name = GetFirstNonEmptyString(item, "name", "npcName", "NPCName");
                if (!string.IsNullOrWhiteSpace(npcId) && !string.IsNullOrWhiteSpace(name))
                    aliases[npcId] = name;
            }
        }

        return aliases;
    }

    private static Dictionary<string, string> BuildResidentAliasLookup(JsonObject root)
    {
        var aliases = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);

        if (root[GuardianAbodeResidentState.EntriesProperty] is JsonArray entries)
        {
            foreach (var resident in entries.OfType<JsonObject>())
            {
                var residentId = GetNodeString(resident["residentId"]);
                var displayName = GetNodeString(resident["displayName"]);
                if (!string.IsNullOrWhiteSpace(residentId) && !string.IsNullOrWhiteSpace(displayName))
                    aliases[residentId] = displayName;
            }
        }

        if (root[GuardianAbodeResidentState.UpdateProperty] is JsonArray updateEntries)
        {
            foreach (var resident in updateEntries.OfType<JsonObject>())
            {
                var residentId = GetNodeString(resident["residentId"]);
                var displayName = GetNodeString(resident["displayName"]);
                if (!string.IsNullOrWhiteSpace(residentId) && !string.IsNullOrWhiteSpace(displayName))
                    aliases[residentId] = displayName;
            }
        }

        return aliases;
    }

    private static Dictionary<string, (JsonElement Npc, string Context)> BuildCanonicalNpcTradeValidationMap(
        JsonElement root,
        string contextPrefix)
    {
        var map = new Dictionary<string, (JsonElement Npc, string Context)>(StringComparer.OrdinalIgnoreCase);

        foreach (var sectionName in GuardianPolicyContracts.NpcCoreCanonicalNpcObjectSections)
        {
            if (!root.TryGetProperty(sectionName, out var arr) || arr.ValueKind != JsonValueKind.Array)
                continue;

            var index = 0;
            foreach (var item in arr.EnumerateArray())
            {
                var itemContext = $"{contextPrefix}.{sectionName}[{index++}]";
                if (item.ValueKind != JsonValueKind.Object)
                    continue;

                var npcId = GetFirstNonEmptyString(item, "NPCId", "npcId", "id");
                if (!string.IsNullOrWhiteSpace(npcId))
                    map[npcId] = (item, itemContext);
            }
        }

        return map;
    }

    private static Dictionary<string, List<string>> BuildGuardianAliasLookup(JsonElement root)
    {
        var aliases = new Dictionary<string, List<string>>(StringComparer.OrdinalIgnoreCase);

        void RegisterGuardian(JsonElement guardian)
        {
            var guardianId = GetFirstNonEmptyString(guardian, "guardianId", "id");
            if (string.IsNullOrWhiteSpace(guardianId))
                return;

            var names = new List<string>();
            foreach (var alias in EnumerateGuardianAliases(guardian, includeGuardianId: false))
                names.Add(alias);

            if (names.Count > 0)
                aliases[guardianId] = names;
        }

        if (root.TryGetProperty("guardians", out var guardians) && guardians.ValueKind == JsonValueKind.Array)
        {
            foreach (var guardian in guardians.EnumerateArray())
            {
                if (guardian.ValueKind == JsonValueKind.Object)
                    RegisterGuardian(guardian);
            }
        }

        return aliases;
    }

    private static bool MergeGuardianAliasLookupFromStoredGuardians(
        JsonElement root,
        IDictionary<string, List<string>> aliasLookup,
        ISet<string> authoritativeGuardianIds,
        bool onlyKnownGuardianIds = false)
    {
        if (!root.TryGetProperty("guardians", out var guardians) || guardians.ValueKind != JsonValueKind.Array)
            return false;

        var mergedAny = false;
        foreach (var guardian in guardians.EnumerateArray())
        {
            if (guardian.ValueKind != JsonValueKind.Object)
                continue;

            var guardianId = GetFirstNonEmptyString(guardian, "guardianId", "id");
            if (string.IsNullOrWhiteSpace(guardianId))
                continue;

            if (onlyKnownGuardianIds && !authoritativeGuardianIds.Contains(guardianId))
                continue;

            RegisterGuardianAliasLookup(aliasLookup, guardian);
            authoritativeGuardianIds.Add(guardianId);
            mergedAny = true;
        }

        return mergedAny;
    }

    private void MergeGuardianAliasLookupFromCreateCommands(
        JsonElement root,
        IDictionary<string, List<string>> aliasLookup,
        ISet<string> authoritativeGuardianIds,
        ISet<string>? authorizedCreateGuardianIds = null,
        IDictionary<string, JsonElement>? authorizedCreateGuardiansById = null)
    {
        if (!root.TryGetProperty("UpdateGuardians", out var updates) || updates.ValueKind != JsonValueKind.Array)
            return;

        var scratchIssues = new List<ValidationIssue>();
        var commandIndex = 0;
        foreach (var command in updates.EnumerateArray())
        {
            var commandContext = $"game_state/meta/guardians.json.UpdateGuardians[{commandIndex++}]";
            var authorized = TryAuthorizeGuardianCreateForReasoning(
                command,
                aliasLookup,
                authoritativeGuardianIds,
                scratchIssues,
                commandContext,
                out var identitySource,
                out var guardianId);

            if (authorized && !string.IsNullOrWhiteSpace(guardianId))
            {
                authorizedCreateGuardianIds?.Add(guardianId);
                if (authorizedCreateGuardiansById != null)
                    authorizedCreateGuardiansById[guardianId] = identitySource.Clone();
            }
        }
    }

    private bool TryAuthorizeGuardianCreateForReasoning(
        JsonElement item,
        IDictionary<string, List<string>> aliasLookup,
        ISet<string> authoritativeGuardianIds,
        List<ValidationIssue> scratchIssues,
        string commandContext,
        out JsonElement identitySource,
        out string? guardianId)
    {
        identitySource = item;
        guardianId = null;

        if (item.ValueKind != JsonValueKind.Object ||
            !string.Equals(GetFirstNonEmptyString(item, "command"), "create", StringComparison.OrdinalIgnoreCase))
        {
            return false;
        }

        if (!item.TryGetProperty("data", out identitySource) || identitySource.ValueKind != JsonValueKind.Object)
        {
            identitySource = item;
            return false;
        }

        guardianId = GetFirstNonEmptyString(identitySource, "guardianId", "id");
        if (string.IsNullOrWhiteSpace(guardianId) ||
            authoritativeGuardianIds.Contains(guardianId))
        {
            return false;
        }

        var issuesBeforeValidation = scratchIssues.Count;
        ValidateGuardianCanonicalObject(identitySource, $"{commandContext}.data", scratchIssues);
        var hasValidationErrors = scratchIssues
            .Skip(issuesBeforeValidation)
            .Any(issue => issue.Severity == IssueSeverity.Error);
        if (hasValidationErrors)
            return false;

        RegisterGuardianAliasLookup(aliasLookup, identitySource);
        authoritativeGuardianIds.Add(guardianId);
        return true;
    }

    private static void CollectGuardianArrayDiffStructuredActorTouches(
        StructuredActorExtractionResult extractionResult,
        JsonElement preTurnRoot,
        JsonElement currentRoot,
        IDictionary<string, List<string>> aliasLookup,
        ISet<string> authoritativeGuardianIds)
    {
        var preTurnGuardians = ReadGuardianStateMap(preTurnRoot);
        var currentGuardians = ReadGuardianStateMap(currentRoot);

        foreach (var (guardianId, currentGuardian) in currentGuardians)
        {
            if (!preTurnGuardians.TryGetValue(guardianId, out var preTurnGuardian) ||
                !JsonElementsSemanticallyEqual(preTurnGuardian, currentGuardian))
            {
                extractionResult.Updates.Add(CreateGuardianStructuredActorTouch(
                    guardianId,
                    "guardians",
                    aliasLookup,
                    authoritativeGuardianIds));
            }
        }

        foreach (var (guardianId, _) in preTurnGuardians)
        {
            if (!currentGuardians.ContainsKey(guardianId))
            {
                extractionResult.Updates.Add(CreateGuardianStructuredActorTouch(
                    guardianId,
                    "guardians",
                    aliasLookup,
                    authoritativeGuardianIds));
            }
        }
    }

    private static bool JsonElementsSemanticallyEqual(JsonElement left, JsonElement right)
    {
        var leftNode = JsonNode.Parse(left.GetRawText());
        var rightNode = JsonNode.Parse(right.GetRawText());
        return JsonNode.DeepEquals(leftNode, rightNode);
    }

    private static Dictionary<string, JsonElement> ReadGuardianStateMap(JsonElement root)
    {
        var guardians = new Dictionary<string, JsonElement>(StringComparer.OrdinalIgnoreCase);
        if (!root.TryGetProperty("guardians", out var guardianArray) || guardianArray.ValueKind != JsonValueKind.Array)
            return guardians;

        foreach (var guardian in guardianArray.EnumerateArray())
        {
            if (guardian.ValueKind != JsonValueKind.Object)
                continue;

            var guardianId = GetFirstNonEmptyString(guardian, "guardianId", "id");
            if (string.IsNullOrWhiteSpace(guardianId))
                continue;

            guardians[guardianId] = guardian.Clone();
        }

        return guardians;
    }

    private static void AddGuardianStructuredActorTouchFromReference(
        ICollection<StructuredActorUpdate> updates,
        JsonElement item,
        string guardianIdPropertyName,
        string section,
        string context,
        IDictionary<string, List<string>> aliasLookup,
        ISet<string> authoritativeGuardianIds)
    {
        if (!item.TryGetProperty(guardianIdPropertyName, out var guardianIdNode))
            return;

        var guardianId = guardianIdNode.ValueKind == JsonValueKind.String
            ? guardianIdNode.GetString()
            : null;
        updates.Add(CreateGuardianStructuredActorTouch(
            guardianId,
            section,
            aliasLookup,
            authoritativeGuardianIds,
            fallbackDisplayName: $"{context}.{guardianIdPropertyName}"));
    }

    private static StructuredActorUpdate CreateGuardianStructuredActorTouch(
        string? guardianId,
        string section,
        IDictionary<string, List<string>> aliasLookup,
        ISet<string> authoritativeGuardianIds,
        string? fallbackDisplayName = null)
    {
        if (!string.IsNullOrWhiteSpace(guardianId) &&
            authoritativeGuardianIds.Contains(guardianId) &&
            aliasLookup.TryGetValue(guardianId, out var resolvedNames))
        {
            var canonicalScopeName = resolvedNames.FirstOrDefault(alias =>
                !string.IsNullOrWhiteSpace(alias) &&
                !string.Equals(alias, guardianId, StringComparison.OrdinalIgnoreCase));
            if (!string.IsNullOrWhiteSpace(canonicalScopeName))
            {
                var update = new StructuredActorUpdate
                {
                    ActorType = "Guardian",
                    FilePath = "game_state/meta/guardians.json",
                    Section = section,
                    DisplayName = canonicalScopeName!,
                    HasResolvedName = true
                };

                foreach (var resolvedName in resolvedNames)
                {
                    if (!string.IsNullOrWhiteSpace(resolvedName) &&
                        !string.Equals(resolvedName, guardianId, StringComparison.OrdinalIgnoreCase))
                    {
                        update.Aliases.Add(resolvedName);
                    }
                }

                return update;
            }
        }

        return CreateUnresolvedGuardianStructuredActorUpdate(
            !string.IsNullOrWhiteSpace(guardianId) ? guardianId! : fallbackDisplayName ?? section,
            section);
    }

    private static void RegisterGuardianAliasLookup(IDictionary<string, List<string>> aliasLookup, JsonElement guardian)
    {
        var guardianId = GetFirstNonEmptyString(guardian, "guardianId", "id");
        if (string.IsNullOrWhiteSpace(guardianId))
            return;

        var aliases = EnumerateGuardianAliases(guardian, includeGuardianId: false).ToList();
        if (aliases.Count == 0)
            return;

        aliasLookup[guardianId] = aliases;
    }

    private static void AddGuardianAliases(ISet<string> aliases, JsonElement guardian, bool includeGuardianId = true)
    {
        foreach (var alias in EnumerateGuardianAliases(guardian, includeGuardianId))
            aliases.Add(alias);
    }

    private static IEnumerable<string> EnumerateGuardianAliases(JsonElement guardian, bool includeGuardianId = true)
    {
        var seen = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        var values = new List<string>();

        void YieldIfUnique(string? value)
        {
            if (!string.IsNullOrWhiteSpace(value) && seen.Add(value))
                values.Add(value);
        }

        if (includeGuardianId)
            YieldIfUnique(GetFirstNonEmptyString(guardian, "guardianId", "id"));

        if (guardian.TryGetProperty("nameVariants", out var nameVariants) &&
            nameVariants.ValueKind == JsonValueKind.Object)
        {
            YieldIfUnique(GetFirstNonEmptyString(nameVariants, "default"));
        }

        YieldIfUnique(GuardianManifestation.GetDisplayName(guardian));
        YieldIfUnique(GuardianManifestation.GetCanonicalName(guardian));
        return values;
    }

    private static bool TryCreateNpcStructuredActorUpdate(JsonElement item, Dictionary<string, string> aliasLookup,
        string sectionName, out StructuredActorUpdate update)
    {
        update = new StructuredActorUpdate();
        if (item.ValueKind != JsonValueKind.Object)
            return false;

        var name = GetFirstNonEmptyString(item, "name", "npcName", "NPCName");
        var npcId = GetFirstNonEmptyString(item, "NPCId", "npcId", "id");
        if (string.IsNullOrWhiteSpace(name) &&
            !string.IsNullOrWhiteSpace(npcId) &&
            aliasLookup.TryGetValue(npcId, out var mappedName))
        {
            name = mappedName;
        }

        if (string.IsNullOrWhiteSpace(name) && string.IsNullOrWhiteSpace(npcId))
            return false;

        update = new StructuredActorUpdate
        {
            ActorType = "NPC",
            FilePath = "game_state/npcs/npc_core.json",
            Section = sectionName,
            DisplayName = !string.IsNullOrWhiteSpace(name) ? name! : npcId!,
            HasResolvedName = !string.IsNullOrWhiteSpace(name)
        };

        if (!string.IsNullOrWhiteSpace(name))
            update.Aliases.Add(name);
        if (!string.IsNullOrWhiteSpace(npcId))
        {
            update.Aliases.Add(npcId);
            if (aliasLookup.TryGetValue(npcId, out var resolvedName))
                update.Aliases.Add(resolvedName);
        }

        return update.Aliases.Count > 0;
    }

    private static bool TryCreateResidentStructuredActorUpdate(
        JsonObject item,
        Dictionary<string, string> aliasLookup,
        string sectionName,
        out StructuredActorUpdate update)
    {
        update = new StructuredActorUpdate();

        var residentId = GetNodeString(item["residentId"]);
        var displayName = GetNodeString(item["displayName"]);
        if (string.IsNullOrWhiteSpace(displayName) &&
            !string.IsNullOrWhiteSpace(residentId) &&
            aliasLookup.TryGetValue(residentId, out var mappedName))
        {
            displayName = mappedName;
        }

        if (string.IsNullOrWhiteSpace(displayName) && string.IsNullOrWhiteSpace(residentId))
            return false;

        update = new StructuredActorUpdate
        {
            ActorType = "Resident",
            FilePath = GuardianAbodeResidentState.StatePath,
            Section = sectionName,
            DisplayName = !string.IsNullOrWhiteSpace(displayName) ? displayName! : residentId!,
            HasResolvedName = !string.IsNullOrWhiteSpace(displayName)
        };

        if (!string.IsNullOrWhiteSpace(displayName))
            update.Aliases.Add(displayName);
        if (!string.IsNullOrWhiteSpace(residentId))
        {
            update.Aliases.Add(residentId);
            if (aliasLookup.TryGetValue(residentId, out var resolvedName))
                update.Aliases.Add(resolvedName);
        }

        return update.Aliases.Count > 0;
    }

    private static void AddResidentJournalStructuredActorUpdates(
        JsonNode? node,
        Dictionary<string, string> aliasLookup,
        string sectionName,
        List<StructuredActorUpdate> updates)
    {
        if (node is not JsonArray entries)
            return;

        foreach (var item in entries.OfType<JsonObject>())
        {
            if (TryCreateResidentStructuredActorUpdate(item, aliasLookup, sectionName, out var update))
                updates.Add(update);
        }
    }

    private static void CollectResidentCanonicalDiffStructuredActorTouches(
        JsonObject preTurnRoot,
        JsonObject currentRoot,
        Dictionary<string, string> aliasLookup,
        List<StructuredActorUpdate> updates)
    {
        var previousFingerprints = BuildResidentEntryFingerprints(preTurnRoot);
        var currentFingerprints = BuildResidentEntryFingerprints(currentRoot);

        foreach (var pair in currentFingerprints)
        {
            if (previousFingerprints.TryGetValue(pair.Key, out var previousFingerprint) &&
                string.Equals(previousFingerprint, pair.Value, StringComparison.Ordinal))
            {
                continue;
            }

            var resident = GuardianAbodeResidentState.FindResident(currentRoot, pair.Key);
            if (resident != null &&
                TryCreateResidentStructuredActorUpdate(resident, aliasLookup, GuardianAbodeResidentState.EntriesProperty, out var update))
            {
                updates.Add(update);
            }
        }
    }

    private static Dictionary<string, string> BuildResidentEntryFingerprints(JsonObject root)
    {
        GuardianAbodeResidentState.NormalizeShape(root);
        var result = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
        if (root[GuardianAbodeResidentState.EntriesProperty] is not JsonArray entries)
            return result;

        foreach (var resident in entries.OfType<JsonObject>())
        {
            var residentId = GetNodeString(resident["residentId"]);
            if (string.IsNullOrWhiteSpace(residentId))
                continue;

            result[residentId] = resident.ToJsonString();
        }

        return result;
    }

    private bool TryCreateGuardianStructuredActorUpdate(
        JsonElement item,
        Dictionary<string, List<string>> aliasLookup,
        ISet<string> authoritativeGuardianIds,
        List<ValidationIssue> scratchIssues,
        string commandContext,
        out StructuredActorUpdate update)
    {
        update = new StructuredActorUpdate();
        var command = item.ValueKind == JsonValueKind.Object
            ? GetFirstNonEmptyString(item, "command")
            : null;
        var unresolvedDisplayName = !string.IsNullOrWhiteSpace(command)
            ? $"UpdateGuardians.{command}"
            : "UpdateGuardians command";

        if (item.ValueKind != JsonValueKind.Object)
        {
            update = CreateUnresolvedGuardianStructuredActorUpdate(unresolvedDisplayName, GuardianStructuredUpdateSection);
            return true;
        }

        var identitySource = item;
        var guardianId = GetFirstNonEmptyString(identitySource, "guardianId", "id");
        var allowCanonicalResolution = !string.IsNullOrWhiteSpace(guardianId);
        if (string.Equals(command, "create", StringComparison.OrdinalIgnoreCase))
        {
            allowCanonicalResolution = TryAuthorizeGuardianCreateForReasoning(
                item,
                aliasLookup,
                authoritativeGuardianIds,
                scratchIssues,
                commandContext,
                out identitySource,
                out guardianId);

            if (!allowCanonicalResolution)
            {
                if (item.TryGetProperty("data", out var data) && data.ValueKind == JsonValueKind.Object)
                {
                    identitySource = data;
                    guardianId = GetFirstNonEmptyString(data, "guardianId", "id");
                }
                else
                {
                    identitySource = item;
                    guardianId = GetFirstNonEmptyString(item, "guardianId", "id");
                }
            }
        }

        List<string>? resolvedNames = null;
        if (allowCanonicalResolution &&
            !string.IsNullOrWhiteSpace(guardianId) &&
            authoritativeGuardianIds.Contains(guardianId) &&
            aliasLookup.TryGetValue(guardianId, out var mappedNames))
        {
            resolvedNames = mappedNames;
        }

        var canonicalScopeName = resolvedNames?.FirstOrDefault(alias =>
            !string.IsNullOrWhiteSpace(alias) &&
            !string.Equals(alias, guardianId, StringComparison.OrdinalIgnoreCase));

        if (string.IsNullOrWhiteSpace(canonicalScopeName))
        {
            update = CreateUnresolvedGuardianStructuredActorUpdate(
                !string.IsNullOrWhiteSpace(guardianId) ? guardianId! : unresolvedDisplayName,
                GuardianStructuredUpdateSection);
            return true;
        }

        update = new StructuredActorUpdate
        {
            ActorType = "Guardian",
            FilePath = "game_state/meta/guardians.json",
            Section = GuardianStructuredUpdateSection,
            DisplayName = !string.IsNullOrWhiteSpace(canonicalScopeName) ? canonicalScopeName! : guardianId!,
            HasResolvedName = !string.IsNullOrWhiteSpace(canonicalScopeName)
        };

        if (!string.IsNullOrWhiteSpace(canonicalScopeName))
            update.Aliases.Add(canonicalScopeName);
        if (resolvedNames != null)
        {
            foreach (var resolvedName in resolvedNames)
            {
                if (!string.IsNullOrWhiteSpace(resolvedName) &&
                    !string.Equals(resolvedName, guardianId, StringComparison.OrdinalIgnoreCase))
                {
                    update.Aliases.Add(resolvedName);
                }
            }
        }

        return true;
    }

    private static StructuredActorUpdate CreateUnresolvedGuardianStructuredActorUpdate(string displayName, string section)
    {
        return new StructuredActorUpdate
        {
            ActorType = "Guardian",
            FilePath = "game_state/meta/guardians.json",
            Section = section,
            DisplayName = displayName,
            HasResolvedName = false
        };
    }

    private static IEnumerable<StructuredActorUpdate> CreateNpcRenameStructuredActorUpdates(JsonElement item, string sectionName)
    {
        if (item.ValueKind != JsonValueKind.Object)
            yield break;

        var oldName = GetFirstNonEmptyString(item, "oldName");
        var newName = GetFirstNonEmptyString(item, "newName");
        var aliases = new[] { oldName, newName }
            .Where(value => !string.IsNullOrWhiteSpace(value))
            .Cast<string>()
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .ToList();

        if (aliases.Count == 0)
            yield break;

        var update = new StructuredActorUpdate
        {
            ActorType = "NPC",
            FilePath = "game_state/npcs/npc_core.json",
            Section = sectionName,
            DisplayName = oldName ?? newName ?? aliases[0],
            HasResolvedName = true
        };

        foreach (var alias in aliases)
            update.Aliases.Add(alias);

        yield return update;
    }

    private static IEnumerable<StructuredActorUpdate> CreateInterNpcRelationshipStructuredActorUpdates(
        JsonElement item, string sectionName, Dictionary<string, string> aliasLookup)
    {
        if (item.ValueKind != JsonValueKind.Object)
            yield break;

        var source = CreateNamedNpcStructuredActorUpdate(
            sectionName,
            GetFirstNonEmptyString(item, "sourceNpcName", "sourceName"),
            GetFirstNonEmptyString(item, "sourceNpcId"),
            aliasLookup);
        if (source != null)
            yield return source;

        var target = CreateNamedNpcStructuredActorUpdate(
            sectionName,
            GetFirstNonEmptyString(item, "targetNpcName"),
            GetFirstNonEmptyString(item, "targetNpcId", "initialTargetNpcId"),
            aliasLookup);
        if (target != null)
            yield return target;
    }

    private static StructuredActorUpdate? CreateNamedNpcStructuredActorUpdate(string sectionName,
        string? name, string? npcId, Dictionary<string, string> aliasLookup)
    {
        var resolvedName = name;
        if (string.IsNullOrWhiteSpace(resolvedName) &&
            !string.IsNullOrWhiteSpace(npcId) &&
            aliasLookup.TryGetValue(npcId, out var mappedName))
        {
            resolvedName = mappedName;
        }

        if (string.IsNullOrWhiteSpace(resolvedName) && string.IsNullOrWhiteSpace(npcId))
            return null;

        var update = new StructuredActorUpdate
        {
            ActorType = "NPC",
            FilePath = "game_state/npcs/npc_core.json",
            Section = sectionName,
            DisplayName = !string.IsNullOrWhiteSpace(resolvedName) ? resolvedName! : npcId!,
            HasResolvedName = !string.IsNullOrWhiteSpace(resolvedName)
        };

        if (!string.IsNullOrWhiteSpace(resolvedName))
            update.Aliases.Add(resolvedName);
        if (!string.IsNullOrWhiteSpace(npcId))
            update.Aliases.Add(npcId);

        return update;
    }

    private static void ValidateStructuredActorUpdatesAgainstScope(ReasoningScopeManifest scope,
        IEnumerable<StructuredActorUpdate> updates, List<ValidationIssue> issues)
    {
        var seen = new HashSet<string>(StringComparer.OrdinalIgnoreCase);

        foreach (var update in updates)
        {
            if (string.Equals(update.ActorType, "Guardian", StringComparison.OrdinalIgnoreCase) &&
                !update.HasResolvedName)
            {
                var unresolvedKey = $"{update.ActorType}:{update.DisplayName}:unresolved";
                if (!seen.Add(unresolvedKey))
                    continue;

                issues.Add(new ValidationIssue(
                    update.FilePath,
                    IssueSeverity.Error,
                    $"Структурированное обновление Guardian '{update.DisplayName}' не имеет canonical guardian identity для scope validation",
                    code: "structured_guardian_update_missing_canonical_identity",
                    actor: update.DisplayName,
                    section: update.Section,
                    expected: "guardianId resolvable through canonical guardians[] / validated pre-turn guardian baseline",
                    actual: update.DisplayName,
                    repairHint: "Любой guardian-touching surface должен резолвиться через существующий canonical guardianId и authoritative guardian baseline. Guardian scope validation не опирается на unresolved identity или raw payload aliases."));
                continue;
            }

            if (update.Aliases.Any(alias => ScopeContainsRelevantActor(scope, alias)))
                continue;

            var dedupeKey = $"{update.ActorType}:{update.DisplayName}";
            if (!seen.Add(dedupeKey))
                continue;

            issues.Add(new ValidationIssue(
                update.FilePath,
                IssueSeverity.Error,
                $"Структурированное обновление {update.ActorType} '{update.DisplayName}' не покрыто declared relevant actors",
                code: update.ActorType switch
                {
                    "Guardian" => "structured_guardian_update_out_of_scope",
                    "Resident" => "structured_resident_update_out_of_scope",
                    _ => "structured_npc_update_out_of_scope"
                },
                actor: update.DisplayName,
                section: update.Section,
                expected: $"'{update.DisplayName}' declared in Relevant actors",
                actual: $"{update.Section} changed actor outside declared scope",
                repairHint: $"Либо добавь '{update.DisplayName}' в Relevant actors и reasoning blocks, либо не изменяй его через {update.Section} в этом ходу."));
        }
    }

    private static bool ScopeContainsRelevantActor(ReasoningScopeManifest scope, string alias)
    {
        return scope.RelevantActors.Any(actor => string.Equals(actor, alias, StringComparison.OrdinalIgnoreCase));
    }
    private void ValidateActorReasoningBlock(string thoughts, string actorName, string actorType, bool requiresNpcLocationAudit, List<ValidationIssue> issues)
    {
        if (!TryExtractReasoningBlock(thoughts, actorName, out var block))
        {
            issues.Add(new ValidationIssue(
                "output/debug_logs.json", IssueSeverity.Error,
                $"Отсутствует reasoning block для {actorType} '{actorName}' в gm_thoughts_markdown",
                code: "missing_actor_block",
                actor: actorName,
                section: "npc_reasoning",
                expected: $"### {actorName} block",
                actual: "missing",
                repairHint: $"Добавь блок '### {actorName}' с ситуацией, мыслями и действиями."));
            return;
        }

        var lower = block.ToLowerInvariant();
        if (!ContainsAny(lower, "ситуац", "current situation"))
        {
            issues.Add(new ValidationIssue(
                "output/debug_logs.json", IssueSeverity.Error,
                $"Для {actorType} '{actorName}' отсутствует подпункт ситуации/current situation",
                code: "missing_actor_situation",
                actor: actorName,
                section: "npc_reasoning",
                expected: "Situation / Current situation",
                actual: "missing",
                repairHint: $"Добавь подпункт ситуации для актора '{actorName}'."));
        }

        if (!ContainsAny(lower, "мысл", "internal thoughts", "внутрен"))
        {
            issues.Add(new ValidationIssue(
                "output/debug_logs.json", IssueSeverity.Error,
                $"Для {actorType} '{actorName}' отсутствует подпункт мыслей/internal thoughts",
                code: "missing_actor_thoughts",
                actor: actorName,
                section: "npc_reasoning",
                expected: "Thoughts / Internal thoughts",
                actual: "missing",
                repairHint: $"Добавь подпункт мыслей для актора '{actorName}'."));
        }

        if (!ContainsAny(lower, "действ", "intended actions", "planned actions"))
        {
            issues.Add(new ValidationIssue(
                "output/debug_logs.json", IssueSeverity.Error,
                $"Для {actorType} '{actorName}' отсутствует подпункт действий/intended actions",
                code: "missing_actor_actions",
                actor: actorName,
                section: "npc_reasoning",
                expected: "Actions / Intended actions",
                actual: "missing",
                repairHint: $"Добавь подпункт действий для актора '{actorName}'."));
        }

        if (requiresNpcLocationAudit && !HasActorLocationAudit(block))
        {
            issues.Add(new ValidationIssue(
                "output/debug_logs.json", IssueSeverity.Error,
                $"Для NPC '{actorName}' отсутствует обязательный подпункт текущей локации/current location",
                code: "missing_actor_current_location",
                actor: actorName,
                section: "npc_reasoning",
                expected: "Current location / Текущая локация / currentLocationId line inside the actor block",
                actual: "missing",
                repairHint: $"Добавь в блок '### {actorName}' явный подпункт текущей локации: где NPC находится сейчас и остаётся ли он там или перемещается."));
        }
    }

    private static bool TryExtractReasoningBlock(string text, string actorName, out string block)
    {
        block = "";
        var lines = text.Replace("\r\n", "\n").Split('\n');
        for (var i = 0; i < lines.Length; i++)
        {
            var trimmed = lines[i].Trim();
            if (!trimmed.StartsWith("###", StringComparison.Ordinal))
                continue;

            if (!trimmed.Contains(actorName, StringComparison.OrdinalIgnoreCase))
                continue;

            var buffer = new List<string> { lines[i] };
            for (var j = i + 1; j < lines.Length; j++)
            {
                var next = lines[j].Trim();
                if (next.StartsWith("###", StringComparison.Ordinal) || next.StartsWith("## ", StringComparison.Ordinal))
                    break;
                buffer.Add(lines[j]);
            }

            block = string.Join("\n", buffer);
            return true;
        }

        return false;
    }

    private static bool ContainsAny(string text, params string[] fragments)
    {
        foreach (var fragment in fragments)
        {
            if (text.Contains(fragment, StringComparison.OrdinalIgnoreCase))
                return true;
        }

        return false;
    }

    private static bool HasActorLocationAudit(string block)
    {
        foreach (var line in block.Replace("\r\n", "\n").Split('\n'))
        {
            var trimmed = line.TrimStart().ToLowerInvariant();
            if (!trimmed.StartsWith("-") && !trimmed.StartsWith("*"))
                continue;

            if (trimmed.Contains("текущая локац") ||
                trimmed.Contains("локация:") ||
                trimmed.Contains("местонахожд") ||
                trimmed.Contains("current location") ||
                trimmed.Contains("currentlocationid") ||
                trimmed.Contains("location audit"))
            {
                return true;
            }
        }

        return false;
    }

    private static string? GetFirstNonEmptyString(JsonElement root, params string[] propertyNames)
    {
        foreach (var propertyName in propertyNames)
        {
            if (root.TryGetProperty(propertyName, out var value) &&
                value.ValueKind == JsonValueKind.String &&
                !string.IsNullOrWhiteSpace(value.GetString()))
            {
                return value.GetString();
            }
        }

        return null;
    }

    private async Task ValidateNpcFile(string filePath, HashSet<string> allowedKeys, List<ValidationIssue> issues)
    {
        var json = await _fs.ReadFileAsync(filePath);
        if (string.IsNullOrWhiteSpace(json)) return;

        try
        {
            using var doc = JsonDocument.Parse(json);
            if (doc.RootElement.ValueKind != JsonValueKind.Object)
            {
                issues.Add(new ValidationIssue(
                    filePath,
                    IssueSeverity.Error,
                    "NPC-файл должен иметь корневой JSON object",
                    code: "npc_contract_invalid_root",
                    section: "NpcContractFile",
                    expected: "JSON object",
                    actual: doc.RootElement.ValueKind.ToString(),
                    repairHint: $"Сохрани {filePath} как JSON object с допустимыми NPC top-level ключами: {string.Join(", ", allowedKeys.OrderBy(x => x))}."));
                return;
            }

            var visibleProps = doc.RootElement.EnumerateObject()
                .Where(prop => !prop.Name.StartsWith("_", StringComparison.OrdinalIgnoreCase))
                .ToList();
            var hasUnsupportedVisibleTopLevelKeys = visibleProps.Any(prop => !allowedKeys.Contains(prop.Name));
            if (visibleProps.Count > 0 && !visibleProps.Any(prop => allowedKeys.Contains(prop.Name)))
            {
                issues.Add(new ValidationIssue(
                    filePath,
                    IssueSeverity.Error,
                    "NPC-файл не содержит ни одного допустимого top-level ключа для своего контракта",
                    code: "npc_contract_missing_allowed_top_level_key",
                    section: "NpcContractFile",
                    expected: string.Join(", ", allowedKeys.OrderBy(x => x)),
                    actual: string.Join(", ", visibleProps.Select(prop => prop.Name)),
                    repairHint: "Используй только canonical NPC command names для этого файла и не подменяй их произвольными alias-ами."));
                return;
            }

            foreach (var prop in visibleProps)
            {
                if (!allowedKeys.Contains(prop.Name))
                {
                    issues.Add(new ValidationIssue(
                        $"{filePath}.{prop.Name}",
                        IssueSeverity.Error,
                        $"Недопустимый top-level ключ: {prop.Name}",
                        code: "npc_contract_unknown_top_level_key",
                        section: "NpcContractFile",
                        expected: string.Join(", ", allowedKeys.OrderBy(x => x)),
                        actual: prop.Name,
                        repairHint: "Удали неподдерживаемый top-level ключ и используй только canonical NPC contract surfaces для этого файла."));
                }
            }

            ValidateNpcContract(
                doc.RootElement,
                filePath,
                issues,
                skipManifestedCompanionSourceValidation:
                    filePath.Equals("game_state/npcs/npc_core.json", StringComparison.OrdinalIgnoreCase) &&
                    hasUnsupportedVisibleTopLevelKeys);
        }
        catch (JsonException ex)
        {
            issues.Add(new ValidationIssue(
                filePath,
                IssueSeverity.Error,
                $"Невалидный JSON: {ex.Message}",
                code: "npc_contract_invalid_json",
                section: "NpcContractFile",
                expected: "valid JSON object",
                actual: "invalid JSON",
                repairHint: $"Исправь {filePath} до валидного JSON-объекта, не меняя NPC contract."));
        }
    }

    private void ValidateInventoryItemResourcesStateFile(JsonElement root, string contextPrefix, List<ValidationIssue> issues)
    {
        if (root.TryGetProperty("inventoryItemsResources", out var resourceChanges))
        {
            RequireArrayOfObjects(resourceChanges, $"{contextPrefix}.inventoryItemsResources", issues);
            if (resourceChanges.ValueKind == JsonValueKind.Array)
            {
                var index = 0;
                foreach (var item in resourceChanges.EnumerateArray())
                {
                    var itemContext = $"{contextPrefix}.inventoryItemsResources[{index++}]";
                    RequireString(item, itemContext, issues, "name");
                    RequireString(item, itemContext, issues, "existedId");
                    if (!item.TryGetProperty("contentsPath", out var contentsPath))
                    {
                        issues.Add(new ValidationIssue(
                            $"{itemContext}.contentsPath",
                            IssueSeverity.Error,
                            "inventoryItemsResources item должен содержать contentsPath (массив строк или null)"));
                    }
                    else if (contentsPath.ValueKind != JsonValueKind.Null)
                    {
                        RequireArrayOfStrings(contentsPath, $"{itemContext}.contentsPath", issues);
                    }

                    ValidateNonNegativeNumericLikeField(item, itemContext, issues, "resource");
                    ValidateNonNegativeNumericLikeField(item, itemContext, issues, "maximumResource");
                    RequireString(item, itemContext, issues, "resourceType");
                    ValidateNumericUpperBound(item, itemContext, issues, "resource", "maximumResource", "inventory_item_resource_exceeds_maximum");
                }
            }
        }

        if (root.TryGetProperty("entries", out var entries))
        {
            RequireArrayOfObjects(entries, $"{contextPrefix}.entries", issues);
            if (entries.ValueKind == JsonValueKind.Array)
            {
                var index = 0;
                foreach (var entry in entries.EnumerateArray())
                {
                    var itemContext = $"{contextPrefix}.entries[{index++}]";
                    ValidateInventoryItemReference(entry, itemContext, issues);
                    ValidateNonNegativeNumericLikeField(entry, itemContext, issues, "resource");
                    ValidateNonNegativeNumericLikeField(entry, itemContext, issues, "maximumResource");
                    RequireString(entry, itemContext, issues, "resourceType");
                    ValidateNumericUpperBound(entry, itemContext, issues, "resource", "maximumResource", "inventory_item_resource_exceeds_maximum");
                }
            }
        }
    }

    private void ValidateInventoryItemBondsStateFile(JsonElement root, string contextPrefix, List<ValidationIssue> issues)
    {
        var inventoryRefs = ReadKnownInventoryItemReferencesSync();

        if (root.TryGetProperty("itemBondLevelChanges", out var bondChanges))
        {
            RequireArrayOfObjects(bondChanges, $"{contextPrefix}.itemBondLevelChanges", issues);
            if (bondChanges.ValueKind == JsonValueKind.Array)
            {
                var index = 0;
                foreach (var item in bondChanges.EnumerateArray())
                {
                    var itemContext = $"{contextPrefix}.itemBondLevelChanges[{index++}]";
                    RequireString(item, itemContext, issues, "itemId");
                    RequireString(item, itemContext, issues, "itemName");
                    ValidateItemBondLevelField(item, itemContext, issues, "newBondLevel", required: true, allowNull: false);
                    RequireString(item, itemContext, issues, "changeReason");

                    if (!InventoryReferenceExists(item, inventoryRefs))
                        continue;

                    var itemQuality = TryResolveCurrentInventoryItemQualitySync(GetFirstNonEmptyString(item, "itemId"));
                    if (!string.IsNullOrWhiteSpace(itemQuality) && !IsRareOrHigherItemQuality(itemQuality))
                    {
                        issues.Add(new ValidationIssue(
                            $"{itemContext}.newBondLevel",
                            IssueSeverity.Error,
                            "itemBondLevelChanges допустим только для Rare+ предметов",
                            code: "item_bond_change_forbidden_for_non_rare_quality",
                            section: "Inventory",
                            expected: "Rare | Epic | Legendary | Unique item quality",
                            actual: itemQuality,
                            repairHint: "Не используй itemBondLevelChanges для Trash/Common/Uncommon/Good предметов. Bond system применяется только к Rare+ item quality."));
                    }
                }
            }
        }

        if (root.TryGetProperty("itemFateCardUnlocks", out var fateCardUnlocks))
        {
            RequireArrayOfObjects(fateCardUnlocks, $"{contextPrefix}.itemFateCardUnlocks", issues);
            if (fateCardUnlocks.ValueKind == JsonValueKind.Array)
            {
                var index = 0;
                foreach (var item in fateCardUnlocks.EnumerateArray())
                {
                    var itemContext = $"{contextPrefix}.itemFateCardUnlocks[{index++}]";
                    RequireString(item, itemContext, issues, "itemId");
                    RequireString(item, itemContext, issues, "cardId");
                    RequireString(item, itemContext, issues, "cardName");
                }
            }
        }

        if (root.TryGetProperty("entries", out var entries))
        {
            RequireArrayOfObjects(entries, $"{contextPrefix}.entries", issues);
            if (entries.ValueKind == JsonValueKind.Array)
            {
                var index = 0;
                foreach (var entry in entries.EnumerateArray())
                {
                    var itemContext = $"{contextPrefix}.entries[{index++}]";
                    ValidateInventoryItemReference(entry, itemContext, issues);
                    ValidateItemBondLevelField(entry, itemContext, issues, "ownerBondLevelCurrent", required: true, allowNull: false);

                    var itemQuality = TryResolveCurrentInventoryItemQualitySync(GetFirstNonEmptyString(entry, "itemId", "existedId", "id"));
                    if (entry.TryGetProperty("fateCards", out _ ) || entry.TryGetProperty("ownerBondLevelCurrent", out _))
                        ValidateItemBondAndFateCardContract(entry, itemContext, issues, itemQuality);
                }
            }
        }
    }

    private void ValidateItemJournalStateFile(JsonElement root, string contextPrefix, List<ValidationIssue> issues)
    {
        if (root.TryGetProperty("itemJournalUpdates", out var journalUpdates))
        {
            RequireArrayOfObjects(journalUpdates, $"{contextPrefix}.itemJournalUpdates", issues);
            if (journalUpdates.ValueKind == JsonValueKind.Array)
            {
                var index = 0;
                foreach (var entry in journalUpdates.EnumerateArray())
                {
                    var itemContext = $"{contextPrefix}.itemJournalUpdates[{index++}]";
                    RequireString(entry, itemContext, issues, "itemId");
                    RequireString(entry, itemContext, issues, "itemName");

                    var hasLegacyJournalEntries = entry.TryGetProperty("journalEntries", out var legacyJournalEntries) &&
                                                 legacyJournalEntries.ValueKind != JsonValueKind.Null;
                    var hasEntryToAppend = entry.TryGetProperty("entryToAppend", out var entryToAppend) &&
                                           entryToAppend.ValueKind == JsonValueKind.String &&
                                           !string.IsNullOrWhiteSpace(entryToAppend.GetString());

                    if (hasLegacyJournalEntries)
                    {
                        issues.Add(new ValidationIssue(
                            $"{itemContext}.journalEntries",
                            IssueSeverity.Error,
                            "itemJournalUpdates uses append-only authoring",
                            code: "item_journal_update_uses_legacy_entries_shape",
                            section: "ItemJournals",
                            expected: "entryToAppend string inside itemJournalUpdates[]",
                            actual: "journalEntries fragment inside append-only update",
                            repairHint: "Use entryToAppend instead of journalEntries fragments. itemJournalUpdates должен добавлять одну новую запись, а не пересылать canonical journalEntries."));
                    }

                    if (!hasEntryToAppend)
                    {
                        issues.Add(new ValidationIssue(
                            $"{itemContext}.entryToAppend",
                            IssueSeverity.Error,
                            hasLegacyJournalEntries
                                ? "Use entryToAppend instead of journalEntries fragments"
                                : "itemJournalUpdates должен содержать непустой entryToAppend",
                            code: hasLegacyJournalEntries
                                ? "item_journal_update_missing_entry_to_append"
                                : "item_journal_update_missing_entry_to_append",
                            section: "ItemJournals",
                            expected: "Non-empty entryToAppend string",
                            actual: hasLegacyJournalEntries ? "journalEntries provided instead" : "missing or empty",
                            repairHint: "Передавай append-only delta через entryToAppend. Полный canonical journalEntries массив допустим только в itemJournals/entries."));
                    }
                }
            }
        }

        foreach (var propName in new[] { "itemJournals", "entries" })
        {
            if (!root.TryGetProperty(propName, out var entries))
                continue;

            RequireArrayOfObjects(entries, $"{contextPrefix}.{propName}", issues);
            if (entries.ValueKind != JsonValueKind.Array)
                continue;

            var index = 0;
            foreach (var entry in entries.EnumerateArray())
            {
                var itemContext = $"{contextPrefix}.{propName}[{index++}]";
                ValidateInventoryItemReference(entry, itemContext, issues);
                if (!entry.TryGetProperty("journalEntries", out var journalEntries))
                {
                    issues.Add(new ValidationIssue(
                        $"{itemContext}.journalEntries",
                        IssueSeverity.Error,
                        "Canonical item journal entry должен содержать journalEntries",
                        code: "item_journal_canonical_entries_missing",
                        section: "ItemJournals",
                        expected: "journalEntries array in canonical item journal entry",
                        actual: "missing journalEntries",
                        repairHint: "Для canonical item journal state используй entries[]/itemJournals[] с полным journalEntries массивом. Append-only updates выноси в itemJournalUpdates[]."));
                    continue;
                }

                if (journalEntries.ValueKind != JsonValueKind.Array)
                {
                    issues.Add(new ValidationIssue(
                        $"{itemContext}.journalEntries",
                        IssueSeverity.Error,
                        "journalEntries должен быть массивом",
                        code: "item_journal_entries_not_array",
                        section: "ItemJournals",
                        expected: "Array of strings or entry objects",
                        actual: journalEntries.ValueKind.ToString(),
                        repairHint: "Сохраняй canonical journal history как массив journalEntries[]. Каждый элемент должен быть строкой или объектом записи журнала."));
                    continue;
                }

                var journalIndex = 0;
                foreach (var journalEntry in journalEntries.EnumerateArray())
                {
                    if (journalEntry.ValueKind is JsonValueKind.String)
                    {
                        journalIndex++;
                        continue;
                    }

                    if (journalEntry.ValueKind != JsonValueKind.Object)
                    {
                        issues.Add(new ValidationIssue(
                            $"{itemContext}.journalEntries[{journalIndex}]",
                            IssueSeverity.Error,
                            "journalEntries item должен быть строкой или объектом",
                            code: "item_journal_entry_invalid_shape",
                            section: "ItemJournals",
                            expected: "String or object journal entry",
                            actual: journalEntry.ValueKind.ToString(),
                            repairHint: "Используй либо строковую запись журнала, либо объект с текстом/описанием/voice/timestamp. Не передавай числа, bool или вложенные массивы."));
                        journalIndex++;
                        continue;
                    }

                    ValidateOptionalString(journalEntry, $"{itemContext}.journalEntries[{journalIndex}]", issues, "timestamp");
                    ValidateOptionalString(journalEntry, $"{itemContext}.journalEntries[{journalIndex}]", issues, "event");
                    ValidateOptionalString(journalEntry, $"{itemContext}.journalEntries[{journalIndex}]", issues, "description");
                    ValidateOptionalString(journalEntry, $"{itemContext}.journalEntries[{journalIndex}]", issues, "text");
                    ValidateOptionalString(journalEntry, $"{itemContext}.journalEntries[{journalIndex}]", issues, "spiritVoice");
                    journalIndex++;
                }
            }
        }
    }

    private void ValidateInventoryItemReference(JsonElement item, string itemContext, List<ValidationIssue> issues)
    {
        if (!HasAnyNonEmptyString(item, "existedId", "itemId", "id", "itemName", "name"))
        {
            var isItemJournal = itemContext.Contains("item_journals", StringComparison.OrdinalIgnoreCase) ||
                                itemContext.Contains("itemJournal", StringComparison.OrdinalIgnoreCase);
            issues.Add(new ValidationIssue(
                itemContext,
                IssueSeverity.Error,
                isItemJournal
                    ? "Item journal entry должен идентифицировать предмет через existedId/itemId/id/itemName/name"
                    : "Item sidecar entry должен содержать existedId, itemId, id, itemName или name",
                code: isItemJournal ? "item_journal_missing_item_reference" : "item_sidecar_missing_item_reference",
                section: isItemJournal ? "ItemJournals" : "ItemSidecars",
                expected: "Item reference via existedId, itemId, id, itemName or name",
                actual: "No usable item reference fields",
                repairHint: isItemJournal
                    ? "Привяжи journal entry к конкретному предмету через canonical itemId/existedId или хотя бы itemName/name."
                    : "Привяжи sidecar entry к существующему предмету через canonical itemId/existedId или itemName/name."));
        }
    }

    private static string DescribeInventoryReference(JsonElement item)
    {
        if (item.ValueKind != JsonValueKind.Object)
            return item.ValueKind.ToString();

        var parts = new List<string>();

        foreach (var propName in new[] { "existedId", "itemId", "id", "itemName", "name" })
        {
            if (!item.TryGetProperty(propName, out var value))
                continue;

            if (value.ValueKind == JsonValueKind.String && !string.IsNullOrWhiteSpace(value.GetString()))
            {
                parts.Add($"{propName}={value.GetString()}");
            }
            else if (value.ValueKind != JsonValueKind.Null)
            {
                parts.Add($"{propName}={value.ValueKind}");
            }
        }

        return parts.Count > 0
            ? string.Join(", ", parts)
            : "missing itemId/existedId/id/itemName/name";
    }

    private async Task ValidateFileFields(string filePath, string[] requiredFields,
        List<ValidationIssue> issues)
    {
        var json = await _fs.ReadFileAsync(filePath);
        if (json == null) return;

        try
        {
            using var doc = JsonDocument.Parse(json);
            foreach (var field in requiredFields)
            {
                if (!doc.RootElement.TryGetProperty(field, out _))
                {
                    issues.Add(new ValidationIssue(
                        $"{filePath}.{field}",
                        IssueSeverity.Error,
                        $"Отсутствует обязательное поле: {field}",
                        code: "missing_required_top_level_field",
                        section: "RequiredFields",
                        expected: field,
                        actual: "missing",
                        repairHint: $"Добавь обязательное поле {field} в canonical файл {filePath}."));
                }
            }
        }
        catch (Exception ex)
        {
            _logger.LogDebug(ex, "Не удалось проверить обязательные поля файла {FilePath}.", filePath);
        }
    }

    private static bool IsClientOwnedSurfaceValidationPath(string normalizedPath)
    {
        return normalizedPath.Equals("game_state/control/pending_turn_snapshot.json", StringComparison.OrdinalIgnoreCase) ||
               normalizedPath.Equals(PendingTurnSnapshotAuthority.AuthorityPath, StringComparison.OrdinalIgnoreCase) ||
               normalizedPath.StartsWith("game_state/control/pending_turn_snapshot/", StringComparison.OrdinalIgnoreCase) ||
               normalizedPath.StartsWith(QteSceneService.QteNormalizerBackupDirectory + "/", StringComparison.OrdinalIgnoreCase) ||
               normalizedPath.Equals("game_state/control/validation_repair_request.json", StringComparison.OrdinalIgnoreCase) ||
               normalizedPath.Equals("game_state/control/validation_repair_ready.json", StringComparison.OrdinalIgnoreCase) ||
               normalizedPath.Equals("game_state/control/terminal_protocol_failure_request.json", StringComparison.OrdinalIgnoreCase) ||
               normalizedPath.Equals("game_state/control/progression_schedule.json", StringComparison.OrdinalIgnoreCase) ||
               normalizedPath.Equals("game_state/control/incarnation_world_setup.json", StringComparison.OrdinalIgnoreCase) ||
               normalizedPath.Equals(ScenarioCoreService.ManifestPath, StringComparison.OrdinalIgnoreCase) ||
               normalizedPath.Equals(AfterlifeArchiveCandidateService.ManifestPath, StringComparison.OrdinalIgnoreCase) ||
               normalizedPath.Equals(GuardianAbodeOfferingState.PendingRequestPath, StringComparison.OrdinalIgnoreCase) ||
               normalizedPath.Equals(GuardianTradeRequestState.PendingRequestPath, StringComparison.OrdinalIgnoreCase) ||
               normalizedPath.Equals(ShiningCoreActionRequestState.PendingActionsRequestPath, StringComparison.OrdinalIgnoreCase) ||
               normalizedPath.Equals(ShiningTradeRequestState.PendingRequestsPath, StringComparison.OrdinalIgnoreCase) ||
               normalizedPath.Equals(ShiningFactionRequestState.PendingFoundingsRequestPath, StringComparison.OrdinalIgnoreCase) ||
               normalizedPath.Equals(ShiningFactionRequestState.PendingRealignmentsRequestPath, StringComparison.OrdinalIgnoreCase) ||
               normalizedPath.Equals(ShiningFactionRequestState.PendingLeadershipTransitionsRequestPath, StringComparison.OrdinalIgnoreCase) ||
               normalizedPath.Equals(GuardianAbodeResidentRequestState.PendingResidentsRequestPath, StringComparison.OrdinalIgnoreCase) ||
               normalizedPath.Equals(GuardianAbodeResidentRequestState.PendingInteractionsRequestPath, StringComparison.OrdinalIgnoreCase) ||
               normalizedPath.Equals(GuardianAbodeResidentRequestState.PendingTransfersRequestPath, StringComparison.OrdinalIgnoreCase) ||
               normalizedPath.Equals(ActorSocialInteractionRequestState.PendingGuardianRequestPath, StringComparison.OrdinalIgnoreCase) ||
               normalizedPath.Equals(AfterlifeArchiveActionState.ConsultationRequestPath, StringComparison.OrdinalIgnoreCase) ||
               normalizedPath.Equals(AfterlifeArchiveActionState.ProjectFuelRequestPath, StringComparison.OrdinalIgnoreCase) ||
               normalizedPath.Equals(SystemGuardianLibraryService.AttractionRequestPath, StringComparison.OrdinalIgnoreCase) ||
               normalizedPath.Equals("game_state/control/afterlife_return_guard.json", StringComparison.OrdinalIgnoreCase) ||
               normalizedPath.Equals(GuardianCorrectionService.StatePath, StringComparison.OrdinalIgnoreCase) ||
               normalizedPath.Equals("game_state/control/gm_cli_window_binding.json", StringComparison.OrdinalIgnoreCase) ||
               normalizedPath.Equals("game_state/control/gm_bridge_status.json", StringComparison.OrdinalIgnoreCase) ||
               normalizedPath.Equals("game_state/history/chat_log.json", StringComparison.OrdinalIgnoreCase) ||
               normalizedPath.StartsWith("stories/", StringComparison.OrdinalIgnoreCase) ||
               normalizedPath.Equals("game_state/core/system_mods.json", StringComparison.OrdinalIgnoreCase) ||
               normalizedPath.Equals("lore/current_world/world_directives.json", StringComparison.OrdinalIgnoreCase);
    }

    private void ValidateNpcContract(
        JsonElement root,
        string contextPrefix,
        List<ValidationIssue> issues,
        bool skipManifestedCompanionSourceValidation = false)
    {
        ValidateNpcSceneArray(root, contextPrefix, issues, skipManifestedCompanionSourceValidation);
        ValidateNpcTradeReceiptUpdateCommands(root, contextPrefix, issues);
        ValidateNpcRenameData(root, contextPrefix, issues);
        ValidateNpcJournals(root, contextPrefix, issues);
        ValidateItemJournalUpdateCommands(root, contextPrefix, issues);
        ValidateNpcUnlockedMemories(root, contextPrefix, issues);
        ValidateNpcSkillChanges(root, contextPrefix, issues, "NPCActiveSkillChanges");
        ValidateNpcSkillChanges(root, contextPrefix, issues, "NPCPassiveSkillChanges");
        ValidateNpcSkillMastery(root, contextPrefix, issues, "NPCSkillMasteryChanges", false);
        ValidateNpcSkillMastery(root, contextPrefix, issues, "NPCPassiveSkillMasteryChanges", true);
        ValidateNpcInventoryAdds(root, contextPrefix, issues);
        ValidateNpcInventoryUpdates(root, contextPrefix, issues);
        ValidateNpcInventoryRemovals(root, contextPrefix, issues);
        ValidateNpcEquipmentChanges(root, contextPrefix, issues);
        ValidateNpcInventoryResources(root, contextPrefix, issues);
        ValidateNpcGoalUpdates(root, contextPrefix, issues);
        ValidateNpcQuestUpdates(root, contextPrefix, issues);
        ValidateNpcRelationshipChanges(root, contextPrefix, issues);
        ValidateInterNpcRelationshipChanges(root, contextPrefix, issues);
        ValidateNpcRelationshipLockUpdates(root, contextPrefix, issues);
        ValidateNpcIdentityArray(root, contextPrefix, issues, "NPCEffectChanges", "effectsApplied");
        ValidateNpcIdentityOnlyArray(root, contextPrefix, issues, "NPCWoundChanges");
        ValidateNpcIdentityOnlyArray(root, contextPrefix, issues, "NPCPersonalityTraitChanges");
        ValidateNpcActivityUpdates(root, contextPrefix, issues);
        ValidateCompleteNpcActivities(root, contextPrefix, issues);
        ValidateNpcMaskChanges(root, contextPrefix, issues, "NPCMaskAdds", "mask");
        ValidateNpcMaskChanges(root, contextPrefix, issues, "NPCMaskUpdates", "maskUpdate");
        ValidateNpcIdentityArray(root, contextPrefix, issues, "NPCMaskRemovals", "maskId");
        ValidateNpcIdentityArray(root, contextPrefix, issues, "NPCActiveMaskChange", "newActiveMaskId");
        ValidateNpcFateCardUnlocks(root, contextPrefix, issues);
        ValidateNpcCustomStateChanges(root, contextPrefix, issues);
    }

    private void ValidateNpcSceneArray(
        JsonElement root,
        string contextPrefix,
        List<ValidationIssue> issues,
        bool skipManifestedCompanionSourceValidation = false)
    {
        var tradeSignaturesByNpc = new Dictionary<string, (string Context, string? TradeStateSignature, string? TradeInventorySignature, string? BuybackInventorySignature)>(StringComparer.OrdinalIgnoreCase);
        var sameTurnLocationInitialIds = CollectSameTurnLocationInitialIds(root);
        var currentSceneAnchor = ReadCurrentSceneLocationAnchorSync();
        var currentSceneLocationId = currentSceneAnchor.LocationId;
        var currentSceneInitialId = currentSceneAnchor.InitialId;
        var currentSceneMissingInitialAnchor = IsCurrentSceneNewLocationWithoutInitialIdSync();
        if (!skipManifestedCompanionSourceValidation)
            ValidateCompanionManifestationNpcSources(root, contextPrefix, issues);

        foreach (var sectionName in GuardianPolicyContracts.NpcCoreCanonicalNpcObjectSections)
        {
            if (!TryGetArray(root, sectionName, $"{contextPrefix}.{sectionName}", issues, out var arr))
                continue;

            if (string.Equals(sectionName, "NPCsInScene", StringComparison.OrdinalIgnoreCase) &&
                currentSceneMissingInitialAnchor &&
                arr.GetArrayLength() > 0)
            {
                issues.Add(new ValidationIssue(
                    "game_state/world/current_location.json.currentLocationData.initialId",
                    IssueSeverity.Error,
                    "Same-turn новая currentLocationData с NPCsInScene должна явно задавать initialId для cross-reference linking",
                    code: "current_location_new_scene_missing_initial_id_for_npc_scene",
                    section: "Location",
                    expected: "non-empty initialId when currentLocationData.locationId = null and NPCsInScene is present",
                    actual: "missing",
                    repairHint: "Если текущая сцена является genuinely new location и в ней есть NPCsInScene, добавь в currentLocationData непустой initialId и используй exact это значение в NPC.initialLocationId. Иначе вернись к known location через permanent locationId."));
            }

            var index = 0;
            foreach (var item in arr.EnumerateArray())
            {
                var itemContext = $"{contextPrefix}.{sectionName}[{index++}]";
                if (item.ValueKind != JsonValueKind.Object)
                {
                    issues.Add(new ValidationIssue(itemContext, IssueSeverity.Error, $"Элемент {sectionName} должен быть объектом"));
                    continue;
                }

                ValidateNpcSceneIdentity(item, itemContext, issues);
                RequireString(item, itemContext, issues, "name");
                ValidateNpcCoreObjectShape(item, itemContext, issues, sectionName);
                ValidateNpcTradeState(item, itemContext, issues);

                var npcId = GetFirstNonEmptyString(item, "NPCId", "npcId", "id");
                var initialLocationId = GetFirstNonEmptyString(item, "initialLocationId");
                var currentLocationId = GetFirstNonEmptyString(item, "currentLocationId");
                if (string.Equals(sectionName, "NPCsInScene", StringComparison.OrdinalIgnoreCase) &&
                    !string.IsNullOrWhiteSpace(currentSceneLocationId) &&
                    string.IsNullOrWhiteSpace(currentLocationId))
                {
                    issues.Add(new ValidationIssue(
                        $"{itemContext}.currentLocationId",
                        IssueSeverity.Error,
                        "NPC из NPCsInScene для известной текущей локации должен явно нести currentLocationId",
                        code: "npc_scene_missing_current_location_id",
                        section: "NPCsInScene",
                        expected: $"currentLocationId = {currentSceneLocationId}",
                        actual: "missing",
                        repairHint: "Если NPC реально присутствует в текущей сцене известной локации, передай его currentLocationId равным currentLocationData.locationId. Если NPC не в сцене, убери его из NPCsInScene."));
                }
                else if (string.Equals(sectionName, "NPCsInScene", StringComparison.OrdinalIgnoreCase) &&
                         !string.IsNullOrWhiteSpace(currentSceneLocationId) &&
                         !string.IsNullOrWhiteSpace(currentLocationId) &&
                         !string.Equals(currentLocationId, currentSceneLocationId, StringComparison.OrdinalIgnoreCase))
                {
                    issues.Add(new ValidationIssue(
                        $"{itemContext}.currentLocationId",
                        IssueSeverity.Error,
                        "NPC из NPCsInScene должен иметь currentLocationId, совпадающий с currentLocationData.locationId",
                        code: "npc_scene_location_mismatch",
                        section: "NPCsInScene",
                        expected: $"currentLocationId = {currentSceneLocationId}",
                        actual: currentLocationId,
                        repairHint: "Если NPC действительно находится в текущей сцене, синхронизируй его currentLocationId с currentLocationData.locationId. Если он не в сцене, убери его из NPCsInScene."));
                }
                if (string.Equals(sectionName, "NPCsInScene", StringComparison.OrdinalIgnoreCase) &&
                    !string.IsNullOrWhiteSpace(currentSceneInitialId) &&
                    string.IsNullOrWhiteSpace(initialLocationId))
                {
                    issues.Add(new ValidationIssue(
                        $"{itemContext}.initialLocationId",
                        IssueSeverity.Error,
                        "NPC из NPCsInScene для same-turn новой текущей локации должен ссылаться на exact initialLocationId этой сцены",
                        code: "npc_scene_missing_initial_location_id",
                        section: "NPCsInScene",
                        expected: $"initialLocationId = {currentSceneInitialId}",
                        actual: "missing",
                        repairHint: "Если текущая сцена сама является same-turn новой локацией, для NPC из NPCsInScene передай exact currentLocationData.initialId в initialLocationId и оставь currentLocationId = null."));
                }
                else if (string.Equals(sectionName, "NPCsInScene", StringComparison.OrdinalIgnoreCase) &&
                         !string.IsNullOrWhiteSpace(currentSceneInitialId) &&
                         !string.IsNullOrWhiteSpace(initialLocationId) &&
                         !string.Equals(initialLocationId, currentSceneInitialId, StringComparison.OrdinalIgnoreCase))
                {
                    issues.Add(new ValidationIssue(
                        $"{itemContext}.initialLocationId",
                        IssueSeverity.Error,
                        "NPC из NPCsInScene должен ссылаться именно на initialId текущей same-turn локации, а не на другую новую локацию",
                        code: "npc_scene_initial_location_mismatch",
                        section: "NPCsInScene",
                        expected: $"initialLocationId = {currentSceneInitialId}",
                        actual: initialLocationId,
                        repairHint: "Если NPC находится в текущей same-turn новой сцене, скопируй exact currentLocationData.initialId. Не ссылай NPCsInScene на initialId другой новой локации."));
                }
                if (string.Equals(sectionName, "UpdateNPCs", StringComparison.OrdinalIgnoreCase) &&
                    !string.IsNullOrWhiteSpace(npcId) &&
                    item.TryGetProperty("inventory", out _))
                {
                    issues.Add(new ValidationIssue(
                        $"{itemContext}.inventory",
                        IssueSeverity.Error,
                        "UpdateNPCs не должен пересылать inventory для existing NPC",
                        code: "npc_existing_inventory_resend_forbidden",
                        section: "NPCInventory",
                        repairHint: "Для existing NPC меняй инвентарь только через NPCInventoryAdds/Updates/Removals. inventory внутри UpdateNPCs допустим только при создании нового NPC."));
                }

                if (!string.IsNullOrWhiteSpace(initialLocationId) &&
                    sameTurnLocationInitialIds.Count > 0 &&
                    !sameTurnLocationInitialIds.Contains(initialLocationId))
                {
                    issues.Add(new ValidationIssue(
                        $"{itemContext}.initialLocationId",
                        IssueSeverity.Error,
                        "NPC initialLocationId не совпадает ни с одной new location, созданной в этом же accepted turn",
                        code: "npc_initial_location_same_turn_target_unknown",
                        section: "NPC",
                        expected: "initialLocationId from same-turn currentLocationData/newLocations.initialId",
                        actual: initialLocationId,
                        repairHint: "Если NPC должен оказаться в новой same-turn location, скопируй exact initialId этой локации в NPC.initialLocationId. Иначе используй currentLocationId существующей локации."));
                }

                if (!string.IsNullOrWhiteSpace(initialLocationId) &&
                    item.TryGetProperty("currentLocationId", out var currentLocationNode) &&
                    currentLocationNode.ValueKind != JsonValueKind.Null)
                {
                    issues.Add(new ValidationIssue(
                        $"{itemContext}.currentLocationId",
                        IssueSeverity.Error,
                        "NPC, адресованный через initialLocationId, не должен одновременно задавать non-null currentLocationId",
                        code: "npc_same_turn_initial_location_requires_null_current_location",
                        section: "NPC",
                        expected: "currentLocationId = null when initialLocationId targets same-turn new location",
                        actual: currentLocationNode.ValueKind == JsonValueKind.String ? (currentLocationNode.GetString() ?? string.Empty) : currentLocationNode.ValueKind.ToString(),
                        repairHint: "Для same-turn новой локации используй initialLocationId и оставляй currentLocationId = null. После создания canonical location система сама свяжет NPC с её постоянным locationId."));
                }

                if (string.IsNullOrWhiteSpace(npcId) &&
                    item.TryGetProperty("currentActivity", out var currentActivityNode) &&
                    currentActivityNode.ValueKind != JsonValueKind.Null)
                {
                    issues.Add(new ValidationIssue(
                        $"{itemContext}.currentActivity",
                        IssueSeverity.Error,
                        "Новый NPC должен начинать с currentActivity = null",
                        code: "npc_new_current_activity_must_start_null",
                        section: "NPC",
                        expected: "null currentActivity for newly created NPC",
                        actual: currentActivityNode.ValueKind.ToString(),
                        repairHint: "Для нового NPC инициализируй currentActivity = null. Persistent off-screen activity задавай только после того, как NPC уже создан в canonical state."));
                }

                if (string.IsNullOrWhiteSpace(npcId))
                    continue;

                string? tradeStateSignature = null;
                string? tradeInventorySignature = null;
                string? buybackInventorySignature = null;
                if (item.TryGetProperty("tradeState", out var tradeState) && tradeState.ValueKind == JsonValueKind.Object)
                    tradeStateSignature = BuildCanonicalJsonSignature(tradeState);
                if (item.TryGetProperty("tradeInventory", out var tradeInventory) && tradeInventory.ValueKind == JsonValueKind.Object)
                    tradeInventorySignature = BuildCanonicalJsonSignature(tradeInventory);
                if (item.TryGetProperty("buybackInventory", out var buybackInventory) && buybackInventory.ValueKind == JsonValueKind.Array)
                    buybackInventorySignature = BuildCanonicalJsonSignature(buybackInventory);

                if (tradeSignaturesByNpc.TryGetValue(npcId!, out var existing))
                {
                    if (!string.Equals(existing.TradeStateSignature, tradeStateSignature, StringComparison.Ordinal) ||
                        !string.Equals(existing.TradeInventorySignature, tradeInventorySignature, StringComparison.Ordinal) ||
                        !string.Equals(existing.BuybackInventorySignature, buybackInventorySignature, StringComparison.Ordinal))
                    {
                        issues.Add(new ValidationIssue(
                            itemContext,
                            IssueSeverity.Error,
                            $"Локальная торговля NPC {npcId} расходится между {existing.Context} и {itemContext}",
                            code: "npc_trade_state_mismatch",
                            section: "tradeInventory",
                            expected: existing.TradeInventorySignature ?? existing.BuybackInventorySignature ?? existing.TradeStateSignature ?? "none",
                            actual: tradeInventorySignature ?? buybackInventorySignature ?? tradeStateSignature ?? "none"));
                    }
                }
                else
                {
                    tradeSignaturesByNpc[npcId!] = (itemContext, tradeStateSignature, tradeInventorySignature, buybackInventorySignature);
                }
            }
        }
    }

    private void ValidateCompanionManifestationNpcSources(JsonElement root, string contextPrefix, List<ValidationIssue> issues)
    {
        var seenCompanionSourceRelicIds = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        foreach (var sectionName in GuardianPolicyContracts.ManifestedCompanionNpcCarrierSections)
        {
            if (!root.TryGetProperty(sectionName, out var arr) || arr.ValueKind != JsonValueKind.Array)
                continue;

            var index = 0;
            foreach (var item in arr.EnumerateArray())
            {
                var itemContext = $"{contextPrefix}.{sectionName}[{index++}]";
                if (item.ValueKind != JsonValueKind.Object)
                    continue;

                var sourceCompanionRelicId = GetFirstNonEmptyString(item, "sourceCompanionRelicId");
                var sourceAfterlifeResidentId = GetFirstNonEmptyString(item, "sourceAfterlifeResidentId");
                var sourceSoulImprintId = GetFirstNonEmptyString(item, "sourceSoulImprintId");
                if ((!string.IsNullOrWhiteSpace(sourceAfterlifeResidentId) || !string.IsNullOrWhiteSpace(sourceSoulImprintId)) &&
                    string.IsNullOrWhiteSpace(sourceCompanionRelicId))
                {
                    issues.Add(new ValidationIssue(
                        $"{itemContext}.sourceCompanionRelicId",
                        IssueSeverity.Error,
                        "Manifested companion NPC должен хранить sourceCompanionRelicId для однозначного closure",
                        code: "manifested_companion_missing_source_relic_id",
                        section: "AfterlifeResidents",
                        repairHint: "Когда companion manifestation fully materializes mortal NPC, всегда записывай sourceCompanionRelicId вместе с sourceAfterlifeResidentId/sourceSoulImprintId."));
                    continue;
                }

                if (!string.IsNullOrWhiteSpace(sourceCompanionRelicId) && !seenCompanionSourceRelicIds.Add(sourceCompanionRelicId))
                {
                    issues.Add(new ValidationIssue(
                        $"{itemContext}.sourceCompanionRelicId",
                        IssueSeverity.Error,
                        "Несколько manifested NPC не должны делить один sourceCompanionRelicId",
                        code: "manifested_companion_duplicate_source_relic_id",
                        section: "AfterlifeResidents",
                        expected: "unique sourceCompanionRelicId",
                        actual: sourceCompanionRelicId,
                        repairHint: "Один companion-carrying relic должен materialize максимум один mortal companion path/NPC."));
                }
            }
        }
    }

    private void ValidateNpcCoreObjectShape(JsonElement item, string itemContext, List<ValidationIssue> issues, string sectionName)
    {
        var missingFields = new List<string>();
        foreach (var requiredStringField in new[] { "image_prompt", "rarity", "worldview", "personalityArchetype", "culturalStance", "race", "class", "appearanceDescription", "history", "progressionType" })
        {
            if (!HasNonEmptyString(item, requiredStringField))
                missingFields.Add(requiredStringField);
        }

        foreach (var requiredPresentField in new[]
                 {
                     "currentLocationId",
                     "initialLocationId",
                     "age",
                     "level",
                     "experience",
                     "experienceForNextLevel",
                     "relationshipLevel",
                     "attitude",
                     "playerCompanionDirective",
                     "culturalLayer",
                     "personalityTraits",
                     "maxWeight",
                     "totalWeight",
                     "isOverloaded",
                     "progressionTrackers",
                     "plans",
                     "personalQuests",
                     "relationshipLock",
                     "characteristics",
                     "activeSkills",
                     "passiveSkills",
                     "equippedItems",
                     "fateCards",
                     "inventory",
                     "goals"
                 })
        {
            if (!item.TryGetProperty(requiredPresentField, out _))
                missingFields.Add(requiredPresentField);
        }

        if (missingFields.Count > 0)
        {
            issues.Add(new ValidationIssue(
                itemContext,
                IssueSeverity.Error,
                $"{sectionName} должен использовать complete NPC object, а не skeletal identity-only shape",
                code: "npc_full_object_missing_required_fields",
                section: "NPC",
                expected: "Complete NPC object with canonical core fields",
                actual: string.Join(", ", missingFields),
                repairHint: "Передай полный NPC Object по Block 19 contract: core identity, appearance/background, progression fields, currentLocationId и goals."));
            return;
        }

        ValidateRequiredNullableStringField(item, itemContext, issues, "currentLocationId");
        ValidateRequiredNullableStringField(item, itemContext, issues, "initialLocationId");
        ValidateRequiredNullableStringField(item, itemContext, issues, "playerCompanionDirective");
        ValidateRequiredNullableStringField(item, itemContext, issues, "culturalLayer");
        ValidateOptionalString(item, itemContext, issues, "image_prompt");
        ValidateIntegerField(item, itemContext, issues, "age");
        ValidateIntegerField(item, itemContext, issues, "level");
        ValidateIntegerField(item, itemContext, issues, "experience");
        ValidateIntegerField(item, itemContext, issues, "experienceForNextLevel");
        ValidateIntegerField(item, itemContext, issues, "relationshipLevel");
        ValidateOptionalNullableNonNegativeNumericField(item, itemContext, issues, "maxWeight");
        ValidateOptionalNullableNonNegativeNumericField(item, itemContext, issues, "totalWeight");
        RequireBooleanField(item, itemContext, issues, "isOverloaded");
        RequireString(item, itemContext, issues, "attitude");

        if (TryReadInt(item, "relationshipLevel", out var relationshipLevel) &&
            (relationshipLevel < -400 || relationshipLevel > 400))
        {
            issues.Add(new ValidationIssue(
                $"{itemContext}.relationshipLevel",
                IssueSeverity.Error,
                "NPC relationshipLevel должен быть в диапазоне -400..400",
                code: "npc_relationship_level_out_of_bounds",
                section: "NPC",
                expected: "-400..400",
                actual: relationshipLevel.ToString(),
                repairHint: "Сохраняй relationshipLevel в canonical диапазоне -400..400 по Block 19.4.A."));
        }

        var attitude = GetFirstNonEmptyString(item, "attitude");
        if (TryReadInt(item, "relationshipLevel", out relationshipLevel) &&
            !string.IsNullOrWhiteSpace(attitude))
        {
            var expectedAttitudes = GetExpectedNpcAttitudeValues(relationshipLevel);
            if (!expectedAttitudes.Contains(attitude))
            {
                issues.Add(new ValidationIssue(
                    $"{itemContext}.attitude",
                    IssueSeverity.Error,
                    "NPC attitude должен быть machine-readable tier, синхронизированным с relationshipLevel",
                    code: "npc_attitude_relationship_tier_mismatch",
                    section: "NPC",
                    expected: string.Join(" | ", expectedAttitudes),
                    actual: attitude,
                    repairHint: "Синхронизируй attitude с relationshipLevel по 800-point scale из Block 19.4.A. Канонический перевод: Непримиримый Враг / Противник / Неприязнь / Нейтралитет / Доверие и Расположение / Глубокая Связь / Легендарная Преданность."));
            }
        }

        var culturalStance = GetFirstNonEmptyString(item, "culturalStance");
        if (!string.IsNullOrWhiteSpace(culturalStance) && !AllowedNpcCulturalStances.Contains(culturalStance))
        {
            issues.Add(new ValidationIssue(
                $"{itemContext}.culturalStance",
                IssueSeverity.Error,
                "NPC culturalStance должен быть одним из canonical enum значений",
                code: "npc_invalid_cultural_stance",
                section: "NPC",
                expected: string.Join(" | ", AllowedNpcCulturalStances),
                actual: culturalStance,
                repairHint: "Используй для NPC culturalStance только Conformist, Pragmatist или Dissident по Block 19 contract."));
        }

        if (TryReadDouble(item, "maxWeight", out var maxWeight) &&
            TryReadDouble(item, "totalWeight", out var totalWeight) &&
            item.TryGetProperty("isOverloaded", out var isOverloadedNode) &&
            isOverloadedNode.ValueKind is JsonValueKind.True or JsonValueKind.False)
        {
            var expectedOverloaded = totalWeight > maxWeight;
            var actualOverloaded = isOverloadedNode.GetBoolean();
            if (expectedOverloaded != actualOverloaded)
            {
                issues.Add(new ValidationIssue(
                    $"{itemContext}.isOverloaded",
                    IssueSeverity.Error,
                    "NPC isOverloaded должен соответствовать сравнению totalWeight > maxWeight",
                    code: "npc_overloaded_flag_mismatch",
                    section: "NPC",
                    expected: expectedOverloaded.ToString(),
                    actual: actualOverloaded.ToString(),
                    repairHint: "Синхронизируй isOverloaded с фактическим сравнением totalWeight и maxWeight по Block 19."));   
            }
        }

        if (item.TryGetProperty("characteristics", out var characteristics) &&
            RequireObject(characteristics, $"{itemContext}.characteristics", issues))
        {
            foreach (var characteristic in characteristics.EnumerateObject())
            {
                if (characteristic.Value.ValueKind != JsonValueKind.Number)
                {
                    issues.Add(new ValidationIssue(
                        $"{itemContext}.characteristics.{characteristic.Name}",
                        IssueSeverity.Error,
                        "NPC characteristic values должны быть числами",
                        code: "npc_characteristic_invalid_value",
                        section: "NPC",
                        repairHint: "Сохраняй characteristics как object с числовыми значениями по Block 19."));
                }
            }
        }

        if (item.TryGetProperty("progressionTrackers", out var progressionTrackers) &&
            RequireObject(progressionTrackers, $"{itemContext}.progressionTrackers", issues))
        {
            if (progressionTrackers.TryGetProperty("lastPlayerXPValueOnSync", out var lastPlayerXp) &&
                lastPlayerXp.ValueKind != JsonValueKind.Null)
            {
                ValidateIntegerField(progressionTrackers, $"{itemContext}.progressionTrackers", issues, "lastPlayerXPValueOnSync");
            }

            if (progressionTrackers.TryGetProperty("dailyEffortHoursSpent", out var dailyEffort) &&
                dailyEffort.ValueKind != JsonValueKind.Null)
            {
                ValidateNonNegativeNumberField(progressionTrackers, $"{itemContext}.progressionTrackers", issues, "dailyEffortHoursSpent");
            }
        }

        if (item.TryGetProperty("relationshipLock", out var relationshipLock) &&
            RequireObject(relationshipLock, $"{itemContext}.relationshipLock", issues))
        {
            RequireBooleanField(relationshipLock, $"{itemContext}.relationshipLock", issues, "isLocked");
            if (relationshipLock.TryGetProperty("currentCap", out var currentCap) &&
                currentCap.ValueKind != JsonValueKind.Null)
            {
                ValidateIntegerField(relationshipLock, $"{itemContext}.relationshipLock", issues, "currentCap");
            }

            ValidateRequiredNullableStringField(relationshipLock, $"{itemContext}.relationshipLock", issues, "breakthroughQuestId");
        }

        if (item.TryGetProperty("personalityTraits", out var personalityTraits) &&
            personalityTraits.ValueKind != JsonValueKind.Null)
        {
            ValidateArrayItems(personalityTraits, $"{itemContext}.personalityTraits", issues, ValidateNpcPersonalityTraitObject);
        }

        if (item.TryGetProperty("activeSkills", out var activeSkills) && activeSkills.ValueKind != JsonValueKind.Null)
            ValidateArrayItems(activeSkills, $"{itemContext}.activeSkills", issues, ValidateActiveSkillObject);
        if (item.TryGetProperty("passiveSkills", out var passiveSkills) && passiveSkills.ValueKind != JsonValueKind.Null)
            ValidateArrayItems(passiveSkills, $"{itemContext}.passiveSkills", issues, ValidatePassiveSkillObject);
        if (item.TryGetProperty("equippedItems", out var equippedItems))
            ValidateNpcEquippedItemsObject(equippedItems, $"{itemContext}.equippedItems", issues);
        if (item.TryGetProperty("fateCards", out var fateCards))
            ValidateNpcFateCardArray(fateCards, $"{itemContext}.fateCards", issues);
        if (item.TryGetProperty("inventory", out var inventory) && inventory.ValueKind != JsonValueKind.Null)
        {
            ValidateArrayItems(
                inventory,
                $"{itemContext}.inventory",
                issues,
                (inventoryItem, inventoryContext, inventoryIssues) =>
                    ValidateFullInventoryItemObject(inventoryItem, inventoryContext, inventoryIssues, requireStringExistedId: false));
        }

        if (item.TryGetProperty("customStates", out var customStates) && customStates.ValueKind != JsonValueKind.Null)
            ValidateCustomStatesContainer(customStates, $"{itemContext}.customStates", issues);

        if (item.TryGetProperty("personalQuests", out var personalQuests) && personalQuests.ValueKind != JsonValueKind.Null)
            ValidateArrayItems(personalQuests, $"{itemContext}.personalQuests", issues, ValidateNpcPersonalQuestObject);

        ValidateRequiredNullableStringField(item, itemContext, issues, "plans");

        if (item.TryGetProperty("goals", out var goals) && RequireObject(goals, $"{itemContext}.goals", issues))
        {
            RequireString(goals, $"{itemContext}.goals", issues, "longTerm");
            RequireString(goals, $"{itemContext}.goals", issues, "shortTerm");
        }

        if (item.TryGetProperty("currentHealthPercentage", out _))
            ValidatePercentageStringField(item, itemContext, issues, "currentHealthPercentage", requirePositive: false);
        if (item.TryGetProperty("maxHealthPercentage", out _))
            ValidatePercentageStringField(item, itemContext, issues, "maxHealthPercentage", requirePositive: true);

        if (item.TryGetProperty("factionAffiliations", out var factionAffiliations) &&
            factionAffiliations.ValueKind != JsonValueKind.Null)
        {
            ValidateNpcFactionAffiliationsArray(factionAffiliations, $"{itemContext}.factionAffiliations", issues);
        }

        if (item.TryGetProperty("npcRelationships", out var npcRelationships) &&
            npcRelationships.ValueKind != JsonValueKind.Null)
        {
            ValidateNpcRelationshipsArray(npcRelationships, $"{itemContext}.npcRelationships", issues);
        }

        if (item.TryGetProperty("currentActivity", out var currentActivity) &&
            currentActivity.ValueKind != JsonValueKind.Null)
        {
            ValidateNpcCurrentActivityObject(currentActivity, $"{itemContext}.currentActivity", issues);
        }

        if (item.TryGetProperty("completedActivities", out var completedActivities) &&
            completedActivities.ValueKind != JsonValueKind.Null)
        {
            ValidateNpcCompletedActivitiesArray(completedActivities, $"{itemContext}.completedActivities", issues);
        }

        if (item.TryGetProperty("masks", out var masks) && masks.ValueKind != JsonValueKind.Null)
        {
            ValidateNpcMaskArray(masks, $"{itemContext}.masks", issues);
        }

        if (item.TryGetProperty("activeMaskId", out _))
            ValidateOptionalNullableStringField(item, itemContext, issues, "activeMaskId");
    }

    private void ValidateNpcPersonalityTraitObject(JsonElement item, string itemContext, List<ValidationIssue> issues)
    {
        if (!RequireObject(item, itemContext, issues))
            return;

        RequireString(item, itemContext, issues, "traitName");
        RequireString(item, itemContext, issues, "description");
        RequireString(item, itemContext, issues, "valueDescription");
        ValidateIntegerField(item, itemContext, issues, "value");
        if (TryReadInt(item, "value", out var value) && (value < 1 || value > 10))
        {
            issues.Add(new ValidationIssue(
                $"{itemContext}.value",
                IssueSeverity.Error,
                "NPC personality trait value должен быть в диапазоне 1..10",
                code: "npc_personality_trait_value_out_of_bounds",
                section: "NPC",
                expected: "1..10",
                actual: value.ToString(),
                repairHint: "Сохраняй personalityTraits[].value как integer от 1 до 10 по Block 19.1.2.A."));
        }
    }

    private void ValidateNpcEquippedItemsObject(JsonElement value, string context, List<ValidationIssue> issues)
    {
        if (!RequireObject(value, context, issues))
            return;

        foreach (var slot in value.EnumerateObject())
        {
            if (!AllowedEquipmentSlots.Contains(slot.Name))
            {
                issues.Add(new ValidationIssue(
                    $"{context}.{slot.Name}",
                    IssueSeverity.Error,
                    "NPC equippedItems использует неизвестный equipment slot",
                    code: "npc_equipped_items_invalid_slot",
                    section: "NPC",
                    expected: string.Join(" | ", AllowedEquipmentSlots),
                    actual: slot.Name,
                    repairHint: "Используй в NPC equippedItems только canonical slot names из Block 10."));
                continue;
            }

            if (slot.Value.ValueKind != JsonValueKind.Null &&
                (slot.Value.ValueKind != JsonValueKind.String || string.IsNullOrWhiteSpace(slot.Value.GetString())))
            {
                issues.Add(new ValidationIssue(
                    $"{context}.{slot.Name}",
                    IssueSeverity.Error,
                    "NPC equippedItems должен хранить itemId string или null",
                    code: "npc_equipped_items_invalid_value",
                    section: "NPC",
                    expected: "itemId string or null",
                    actual: slot.Value.ValueKind.ToString(),
                    repairHint: "Сохраняй NPC equippedItems как map slot -> equipped itemId или null для пустого слота."));
            }
        }
    }

    private void ValidatePlayerEquippedItemsObject(JsonElement value, string context, List<ValidationIssue> issues)
    {
        if (!RequireObject(value, context, issues))
            return;

        var inventoryRefs = ReadKnownInventoryItemReferencesSync();
        foreach (var slot in value.EnumerateObject())
        {
            if (!AllowedEquipmentSlots.Contains(slot.Name))
            {
                issues.Add(new ValidationIssue(
                    $"{context}.{slot.Name}",
                    IssueSeverity.Error,
                    "Player equippedItems использует неизвестный equipment slot",
                    code: "player_equipped_items_invalid_slot",
                    section: "Inventory",
                    expected: string.Join(" | ", AllowedEquipmentSlots),
                    actual: slot.Name,
                    repairHint: "Используй в player equippedItems только canonical slot names из Block 10."));
                continue;
            }

            if (slot.Value.ValueKind != JsonValueKind.Null &&
                (slot.Value.ValueKind != JsonValueKind.String || string.IsNullOrWhiteSpace(slot.Value.GetString())))
            {
                issues.Add(new ValidationIssue(
                    $"{context}.{slot.Name}",
                    IssueSeverity.Error,
                    "Player equippedItems должен хранить itemId string или null",
                    code: "player_equipped_items_invalid_value",
                    section: "Inventory",
                    expected: "itemId string or null",
                    actual: slot.Value.ValueKind.ToString(),
                    repairHint: "Сохраняй player equippedItems как map slot -> equipped itemId или null для пустого слота."));
                continue;
            }

            var itemId = slot.Value.GetString();
            if ((inventoryRefs.Ids.Count > 0 || inventoryRefs.Names.Count > 0) &&
                !string.IsNullOrWhiteSpace(itemId) &&
                !inventoryRefs.Ids.Contains(itemId))
            {
                issues.Add(new ValidationIssue(
                    $"{context}.{slot.Name}",
                    IssueSeverity.Error,
                    "Player equippedItems ссылается на itemId, которого нет в canonical inventory/items state",
                    code: "player_equipped_items_unknown_item_reference",
                    section: "Inventory",
                    expected: "existing itemId from pre-turn/current inventory/items.json",
                    actual: itemId,
                    repairHint: "В equippedItems используй только itemId реально существующего предмета из inventory/items.json. Сначала создай предмет в canonical inventory state, потом экипируй его."));
            }
        }
    }

    private void ValidateNpcFateCardArray(JsonElement value, string context, List<ValidationIssue> issues)
    {
        RequireArrayOfObjects(value, context, issues);
        if (value.ValueKind != JsonValueKind.Array)
            return;

        var index = 0;
        foreach (var card in value.EnumerateArray())
        {
            var cardContext = $"{context}[{index++}]";
            if (!RequireObject(card, cardContext, issues))
                continue;

            RequireString(card, cardContext, issues, "cardId");
            RequireString(card, cardContext, issues, "name");
            var imagePrompt = RequireString(card, cardContext, issues, "image_prompt");
            RequireString(card, cardContext, issues, "description");
            RequireBooleanField(card, cardContext, issues, "isUnlocked");

            if (!string.IsNullOrWhiteSpace(imagePrompt) && !LooksLikeEnglishImagePrompt(imagePrompt))
            {
                issues.Add(new ValidationIssue(
                    $"{cardContext}.image_prompt",
                    IssueSeverity.Error,
                    "NPC Fate Card image_prompt должен быть English-only и не длиннее 150 символов",
                    code: "npc_fate_card_invalid_image_prompt",
                    section: "NPC",
                    expected: "English prompt, <= 150 chars",
                    actual: imagePrompt.Length > 150 ? $">150 chars ({imagePrompt.Length})" : imagePrompt,
                    repairHint: "Используй для NPC Fate Card краткий English-only image_prompt без кириллицы и не длиннее 150 символов."));
            }

            if (card.TryGetProperty("unlockConditions", out var unlockConditions) &&
                unlockConditions.ValueKind != JsonValueKind.Null &&
                RequireObject(unlockConditions, $"{cardContext}.unlockConditions", issues))
            {
                if (unlockConditions.TryGetProperty("requiredRelationshipLevel", out var requiredRelationshipLevel) &&
                    requiredRelationshipLevel.ValueKind != JsonValueKind.Null)
                {
                    ValidateIntegerField(unlockConditions, $"{cardContext}.unlockConditions", issues, "requiredRelationshipLevel");
                }

                ValidateOptionalString(unlockConditions, $"{cardContext}.unlockConditions", issues, "plotConditionDescription");
                if (unlockConditions.TryGetProperty("conjunction", out var conjunction) &&
                    conjunction.ValueKind == JsonValueKind.String)
                {
                    var conjunctionValue = conjunction.GetString() ?? string.Empty;
                    if (!string.Equals(conjunctionValue, "AND", StringComparison.OrdinalIgnoreCase) &&
                        !string.Equals(conjunctionValue, "OR", StringComparison.OrdinalIgnoreCase))
                    {
                        issues.Add(new ValidationIssue(
                            $"{cardContext}.unlockConditions.conjunction",
                            IssueSeverity.Error,
                            "NPC Fate Card unlockConditions.conjunction должен быть AND или OR",
                            code: "npc_fate_card_invalid_conjunction",
                            section: "NPC",
                            expected: "AND | OR",
                            actual: conjunctionValue,
                            repairHint: "Используй в NPC Fate Card unlockConditions.conjunction только AND или OR."));
                    }
                }
            }

            if (!card.TryGetProperty("rewards", out var rewards) || !RequireObject(rewards, $"{cardContext}.rewards", issues))
                continue;

            RequireString(rewards, $"{cardContext}.rewards", issues, "description");
            if (rewards.TryGetProperty("newActiveSkills", out var newActiveSkills))
                ValidateArrayItems(newActiveSkills, $"{cardContext}.rewards.newActiveSkills", issues, ValidateActiveSkillObject);
            if (rewards.TryGetProperty("newPassiveSkills", out var newPassiveSkills))
                ValidateArrayItems(newPassiveSkills, $"{cardContext}.rewards.newPassiveSkills", issues, ValidatePassiveSkillObject);
            if (rewards.TryGetProperty("statBoosts", out var statBoosts))
                RequireArrayOfStrings(statBoosts, $"{cardContext}.rewards.statBoosts", issues);
            if (rewards.TryGetProperty("newServices", out var newServices))
                RequireArrayOfStrings(newServices, $"{cardContext}.rewards.newServices", issues);
            ValidateOptionalString(rewards, $"{cardContext}.rewards", issues, "otherNarrativeRewards");
            if (rewards.TryGetProperty("tacticalTriggers", out var tacticalTriggers))
            {
                RequireArrayOfObjects(tacticalTriggers, $"{cardContext}.rewards.tacticalTriggers", issues);
                if (tacticalTriggers.ValueKind == JsonValueKind.Array)
                {
                    var triggerIndex = 0;
                    foreach (var trigger in tacticalTriggers.EnumerateArray())
                    {
                        var triggerContext = $"{cardContext}.rewards.tacticalTriggers[{triggerIndex++}]";
                        if (!RequireObject(trigger, triggerContext, issues))
                            continue;

                        RequireString(trigger, triggerContext, issues, "triggerCondition");
                        RequireString(trigger, triggerContext, issues, "newTargetPriority");
                        RequireString(trigger, triggerContext, issues, "description");
                        if (trigger.TryGetProperty("newActionPreference", out var newActionPreference))
                            RequireArrayOfStrings(newActionPreference, $"{triggerContext}.newActionPreference", issues);
                    }
                }
            }
        }
    }

    private void ValidateNpcFactionAffiliationsArray(JsonElement value, string context, List<ValidationIssue> issues)
    {
        RequireArrayOfObjects(value, context, issues);
        if (value.ValueKind != JsonValueKind.Array)
            return;

        var index = 0;
        foreach (var affiliation in value.EnumerateArray())
        {
            var affiliationContext = $"{context}[{index++}]";
            if (!RequireObject(affiliation, affiliationContext, issues))
                continue;

            RequireString(affiliation, affiliationContext, issues, "factionId");
            RequireString(affiliation, affiliationContext, issues, "factionName");
            RequireString(affiliation, affiliationContext, issues, "rank");
            ValidateRequiredNullableStringField(affiliation, affiliationContext, issues, "branch");
            var membershipStatus = RequireString(affiliation, affiliationContext, issues, "membershipStatus");
            if (!string.IsNullOrWhiteSpace(membershipStatus) && !AllowedNpcFactionMembershipStatuses.Contains(membershipStatus))
            {
                issues.Add(new ValidationIssue(
                    $"{affiliationContext}.membershipStatus",
                    IssueSeverity.Error,
                    "NPC faction affiliation membershipStatus должен быть одним из canonical enum значений",
                    code: "npc_faction_affiliation_invalid_status",
                    section: "NPC",
                    expected: string.Join(" | ", AllowedNpcFactionMembershipStatuses),
                    actual: membershipStatus,
                    repairHint: "Используй в NPC factionAffiliations membershipStatus только Active, Former, Exiled, Undercover, Ally или Enemy."));
            }
        }
    }

    private void ValidateNpcMaskArray(JsonElement value, string context, List<ValidationIssue> issues)
    {
        RequireArrayOfObjects(value, context, issues);
        if (value.ValueKind != JsonValueKind.Array)
            return;

        var index = 0;
        foreach (var mask in value.EnumerateArray())
        {
            var maskContext = $"{context}[{index++}]";
            ValidateNpcMaskObject(mask, maskContext, issues);
        }
    }

    private void ValidateNpcMaskObject(JsonElement mask, string maskContext, List<ValidationIssue> issues)
    {
        if (!RequireObject(mask, maskContext, issues))
            return;

        ValidateRequiredNullableStringField(mask, maskContext, issues, "maskId");
        RequireString(mask, maskContext, issues, "maskName");
        RequireString(mask, maskContext, issues, "personalityArchetype");
        RequireString(mask, maskContext, issues, "attitude");
        RequireString(mask, maskContext, issues, "behavioralDirectives");
    }

    private void ValidateNpcRelationshipsArray(JsonElement value, string context, List<ValidationIssue> issues)
    {
        RequireArrayOfObjects(value, context, issues);
        if (value.ValueKind != JsonValueKind.Array)
            return;

        var index = 0;
        foreach (var relation in value.EnumerateArray())
        {
            var relationContext = $"{context}[{index++}]";
            if (!RequireObject(relation, relationContext, issues))
                continue;

            RequireString(relation, relationContext, issues, "targetNpcId");
            RequireString(relation, relationContext, issues, "targetNpcName");
            var status = RequireString(relation, relationContext, issues, "relationshipStatus");
            RequireString(relation, relationContext, issues, "statusReason");
            if (!string.IsNullOrWhiteSpace(status) && !AllowedInterNpcRelationshipStatuses.Contains(status))
            {
                issues.Add(new ValidationIssue(
                    $"{relationContext}.relationshipStatus",
                    IssueSeverity.Error,
                    "npcRelationships.relationshipStatus должен быть одним из canonical enum значений",
                    code: "npc_relationships_invalid_status",
                    section: "NPC",
                    expected: string.Join(" | ", AllowedInterNpcRelationshipStatuses),
                    actual: status,
                    repairHint: "Используй в npcRelationships только Ally, Friend, Neutral, Rival, Enemy, Subordinate, Superior или Family."));
            }
        }
    }

    private void ValidateNpcCurrentActivityObject(JsonElement value, string context, List<ValidationIssue> issues)
    {
        if (!RequireObject(value, context, issues))
            return;

        RequireString(value, context, issues, "activityName");
        RequireString(value, context, issues, "description");
        ValidateIntegerField(value, context, issues, "totalTimeCostMinutes");
        ValidateIntegerField(value, context, issues, "timeSpentMinutes");
        ValidateIntegerField(value, context, issues, "currentStepNumber");
        ValidateIntegerField(value, context, issues, "totalStepsInActivity");
        if (value.TryGetProperty("linkedQuestId", out _))
            ValidateOptionalNullableStringField(value, context, issues, "linkedQuestId");
        if (value.TryGetProperty("linkedPlotOutlineNode", out _))
            ValidateOptionalNullableStringField(value, context, issues, "linkedPlotOutlineNode");
    }

    private void ValidateNpcCompletedActivitiesArray(JsonElement value, string context, List<ValidationIssue> issues)
    {
        RequireArrayOfObjects(value, context, issues);
        if (value.ValueKind != JsonValueKind.Array)
            return;

        var index = 0;
        foreach (var activity in value.EnumerateArray())
        {
            var activityContext = $"{context}[{index++}]";
            if (!RequireObject(activity, activityContext, issues))
                continue;

            RequireString(activity, activityContext, issues, "activityName");
            ValidateIntegerField(activity, activityContext, issues, "completionTurn");
            var finalOutcome = RequireString(activity, activityContext, issues, "finalOutcome");
            RequireString(activity, activityContext, issues, "narrativeSummary");
            if (!string.IsNullOrWhiteSpace(finalOutcome) && !AllowedNpcCompletedActivityOutcomes.Contains(finalOutcome))
            {
                issues.Add(new ValidationIssue(
                    $"{activityContext}.finalOutcome",
                    IssueSeverity.Error,
                    "completedActivities.finalOutcome должен быть одним из canonical enum значений",
                    code: "npc_completed_activity_invalid_outcome",
                    section: "NPC",
                    expected: string.Join(" | ", AllowedNpcCompletedActivityOutcomes),
                    actual: finalOutcome,
                    repairHint: "Используй в completedActivities.finalOutcome только Success, SuccessWithComplication или Failure."));
            }
        }
    }

    private static HashSet<string> CollectSameTurnLocationInitialIds(JsonElement root)
    {
        var ids = new HashSet<string>(StringComparer.OrdinalIgnoreCase);

        if (root.TryGetProperty("currentLocationData", out var currentLocation) &&
            currentLocation.ValueKind == JsonValueKind.Object &&
            currentLocation.TryGetProperty("locationId", out var locationId) &&
            locationId.ValueKind == JsonValueKind.Null)
        {
            var initialId = GetFirstNonEmptyString(currentLocation, "initialId");
            if (!string.IsNullOrWhiteSpace(initialId))
                ids.Add(initialId);
        }

        JsonElement updatesRoot = root;
        if (root.TryGetProperty("worldMapUpdates", out var worldMapUpdates) && worldMapUpdates.ValueKind == JsonValueKind.Object)
            updatesRoot = worldMapUpdates;

        if (updatesRoot.TryGetProperty("newLocations", out var newLocations) && newLocations.ValueKind == JsonValueKind.Array)
        {
            foreach (var location in newLocations.EnumerateArray())
            {
                var initialId = GetFirstNonEmptyString(location, "initialId");
                if (!string.IsNullOrWhiteSpace(initialId))
                    ids.Add(initialId);
            }
        }

        return ids;
    }

    private void ValidateNpcSceneIdentity(JsonElement item, string itemContext, List<ValidationIssue> issues)
    {
        if (!item.TryGetProperty("NPCId", out var npcId) &&
            !item.TryGetProperty("npcId", out npcId) &&
            !item.TryGetProperty("id", out npcId))
        {
            issues.Add(new ValidationIssue(
                $"{itemContext}.NPCId",
                IssueSeverity.Error,
                "NPC object должен содержать NPCId (string для existing NPC или null для new NPC)",
                code: "npc_scene_missing_npc_id",
                section: "NPC",
                repairHint: "Для existing NPC передай permanent NPCId строкой. Для genuinely new NPC явно передай NPCId = null и initialId для same-turn ссылок."));
            return;
        }

        if (npcId.ValueKind == JsonValueKind.Null)
        {
            if (!HasAnyNonEmptyString(item, "initialId"))
            {
                issues.Add(new ValidationIssue(
                    $"{itemContext}.initialId",
                    IssueSeverity.Error,
                    "New NPC с NPCId = null должен содержать non-empty initialId",
                    code: "npc_scene_new_missing_initial_id",
                    section: "NPC",
                    repairHint: "Для genuinely new NPC передай initialId, чтобы same-turn cross-references могли ссылаться на него."));
            }

            return;
        }

        if (npcId.ValueKind != JsonValueKind.String || string.IsNullOrWhiteSpace(npcId.GetString()))
        {
            issues.Add(new ValidationIssue(
                $"{itemContext}.NPCId",
                IssueSeverity.Error,
                "NPCId должен быть непустой строкой или null",
                code: "npc_scene_invalid_npc_id",
                section: "NPC",
                expected: "non-empty NPCId string for existing NPC or null for same-turn new NPC",
                actual: npcId.ValueKind == JsonValueKind.String ? npcId.GetString() ?? string.Empty : npcId.ValueKind.ToString(),
                repairHint: "Для existing NPC передай permanent NPCId непустой строкой. Для genuinely new NPC в этом же accepted turn передай NPCId = null и используй initialId для same-turn linking."));
        }
    }

    private readonly struct NpcTradeReceiptValidationSnapshot
    {
        public NpcTradeReceiptValidationSnapshot(
            string requestId,
            string npcId,
            string tradeCycleId,
            string merchantProfile,
            string status,
            int itemCount,
            string resolvedAtUtc)
        {
            RequestId = requestId;
            NpcId = npcId;
            TradeCycleId = tradeCycleId;
            MerchantProfile = merchantProfile;
            Status = status;
            ItemCount = itemCount;
            ResolvedAtUtc = resolvedAtUtc;
        }

        public string RequestId { get; }
        public string NpcId { get; }
        public string TradeCycleId { get; }
        public string MerchantProfile { get; }
        public string Status { get; }
        public int ItemCount { get; }
        public string ResolvedAtUtc { get; }
    }

    private void ValidateNpcTradeReceiptUpdateCommands(JsonElement root, string contextPrefix, List<ValidationIssue> issues)
    {
        if (!TryGetArray(root, NpcTradeRequestState.UpdateReceiptsProperty, $"{contextPrefix}.{NpcTradeRequestState.UpdateReceiptsProperty}", issues, out var receiptUpdates))
            return;

        var effectiveRoot = NpcTradeRequestState.CreateReceiptAppliedValidationView(root);
        if (effectiveRoot == null)
            return;

        using var effectiveDoc = JsonDocument.Parse(effectiveRoot.ToJsonString());
        var npcTradeValidationMap = BuildCanonicalNpcTradeValidationMap(effectiveDoc.RootElement, contextPrefix);
        var seenRequestIds = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        var affectedNpcIds = new HashSet<string>(StringComparer.OrdinalIgnoreCase);

        var receiptIndex = 0;
        foreach (var receipt in receiptUpdates.EnumerateArray())
        {
            var receiptContext = $"{contextPrefix}.{NpcTradeRequestState.UpdateReceiptsProperty}[{receiptIndex++}]";
            if (!RequireObject(receipt, receiptContext, issues))
                continue;

            var receiptSnapshot = ValidateNpcTradeReceiptSchema(receipt, receiptContext, issues, seenRequestIds);

            if (string.IsNullOrWhiteSpace(receiptSnapshot.RequestId) || string.IsNullOrWhiteSpace(receiptSnapshot.NpcId))
                continue;

            if (!npcTradeValidationMap.ContainsKey(receiptSnapshot.NpcId))
            {
                issues.Add(new ValidationIssue(
                    $"{receiptContext}.npcId",
                    IssueSeverity.Error,
                    "UpdateNpcTradeInventoryReceipts должен ссылаться на существующего NPC из canonical npc_core state",
                    code: "npc_trade_receipt_unknown_npc",
                    section: "tradeInventory",
                    expected: "existing NPC reference from UpdateNPCs or NPCsInScene",
                    actual: receiptSnapshot.NpcId,
                    repairHint: "Закрывай UpdateNpcTradeInventoryReceipts только для NPC из canonical UpdateNPCs/NPCsInScene текущего accepted turn."));
                continue;
            }

            affectedNpcIds.Add(receiptSnapshot.NpcId);
        }

        foreach (var npcId in affectedNpcIds)
        {
            if (npcTradeValidationMap.TryGetValue(npcId, out var validationEntry))
                ValidateNpcTradeReceiptStateConsistency(validationEntry.Npc, validationEntry.Context, issues);
        }
    }

    private NpcTradeReceiptValidationSnapshot ValidateNpcTradeReceiptSchema(
        JsonElement receipt,
        string receiptContext,
        List<ValidationIssue> issues,
        HashSet<string> seenRequestIds)
    {
        var requestId = RequireString(receipt, receiptContext, issues, "requestId");
        var receiptNpcId = RequireString(receipt, receiptContext, issues, "npcId");
        ValidateOptionalString(receipt, receiptContext, issues, "npcName");
        var receiptCycleId = RequireString(receipt, receiptContext, issues, "tradeCycleId");
        var receiptMerchantProfile = RequireString(receipt, receiptContext, issues, "merchantProfile");
        var receiptStatus = RequireString(receipt, receiptContext, issues, "status");
        RequireNonNegativeNumberField(receipt, receiptContext, issues, "itemCount");
        RequirePositiveNumberField(receipt, receiptContext, issues, "resolvedAtTurn");
        var resolvedAtUtc = RequireString(receipt, receiptContext, issues, "resolvedAtUtc");

        if (!string.IsNullOrWhiteSpace(requestId) && !seenRequestIds.Add(requestId))
        {
            issues.Add(new ValidationIssue(
                $"{receiptContext}.requestId",
                IssueSeverity.Error,
                "npc trade receipts содержит duplicate requestId",
                code: "npc_trade_receipts_duplicate_request_id",
                section: "tradeInventory",
                actual: requestId));
        }

        if (!string.IsNullOrWhiteSpace(receiptStatus) &&
            !string.Equals(receiptStatus, NpcTradeRequestState.ReceiptStatusReady, StringComparison.OrdinalIgnoreCase))
        {
            issues.Add(new ValidationIssue(
                $"{receiptContext}.status",
                IssueSeverity.Error,
                "npc trade receipt.status должен быть ready",
                code: "npc_trade_receipt_status_invalid",
                section: "tradeInventory",
                expected: NpcTradeRequestState.ReceiptStatusReady,
                actual: receiptStatus));
        }

        if (!string.IsNullOrWhiteSpace(receiptMerchantProfile) &&
            !NpcTradeService.IsValidMerchantProfileCode(receiptMerchantProfile))
        {
            issues.Add(new ValidationIssue(
                $"{receiptContext}.merchantProfile",
                IssueSeverity.Error,
                "npc trade receipt.merchantProfile должен быть допустимым merchant profile",
                code: "npc_trade_receipt_profile_invalid",
                section: "tradeInventory",
                actual: receiptMerchantProfile));
        }

        if (!string.IsNullOrWhiteSpace(resolvedAtUtc) &&
            !DateTimeOffset.TryParse(resolvedAtUtc, out _))
        {
            issues.Add(new ValidationIssue(
                $"{receiptContext}.resolvedAtUtc",
                IssueSeverity.Error,
                "npc trade receipt.resolvedAtUtc должен быть валидным ISO timestamp",
                code: "npc_trade_receipt_timestamp_invalid",
                section: "tradeInventory",
                actual: resolvedAtUtc));
        }

        var itemCount = receipt.TryGetProperty("itemCount", out var itemCountNode) &&
                        itemCountNode.ValueKind == JsonValueKind.Number &&
                        itemCountNode.TryGetInt32(out var parsedItemCount)
            ? parsedItemCount
            : -1;

        return new NpcTradeReceiptValidationSnapshot(
            requestId,
            receiptNpcId,
            receiptCycleId,
            receiptMerchantProfile,
            receiptStatus,
            itemCount,
            resolvedAtUtc);
    }

    private NpcTradeReceiptValidationSnapshot ReadNpcTradeReceiptValidationSnapshot(JsonElement receipt)
    {
        var itemCount = receipt.TryGetProperty("itemCount", out var itemCountNode) &&
                        itemCountNode.ValueKind == JsonValueKind.Number &&
                        itemCountNode.TryGetInt32(out var parsedItemCount)
            ? parsedItemCount
            : -1;

        return new NpcTradeReceiptValidationSnapshot(
            GetFirstNonEmptyString(receipt, "requestId") ?? "",
            GetFirstNonEmptyString(receipt, "npcId") ?? "",
            GetFirstNonEmptyString(receipt, "tradeCycleId") ?? "",
            GetFirstNonEmptyString(receipt, "merchantProfile") ?? "",
            GetFirstNonEmptyString(receipt, "status") ?? "",
            itemCount,
            GetFirstNonEmptyString(receipt, "resolvedAtUtc") ?? "");
    }

    private void RequirePositiveNumberField(JsonElement root, string contextPrefix, List<ValidationIssue> issues, string propName)
    {
        if (!root.TryGetProperty(propName, out _))
        {
            issues.Add(new ValidationIssue(
                $"{contextPrefix}.{propName}",
                IssueSeverity.Error,
                $"Отсутствует обязательное положительное числовое поле: {propName}",
                code: "missing_required_positive_integer_field",
                expected: "positive integer",
                actual: "missing",
                repairHint: $"Добавь обязательное числовое поле {propName} и сохраняй его как положительное целое число."));
            return;
        }

        ValidatePositiveNumberField(root, contextPrefix, issues, propName);
    }

    private void ValidateNpcTradeReceiptStateConsistency(JsonElement npc, string npcContext, List<ValidationIssue> issues)
    {
        if (!npc.TryGetProperty(NpcTradeRequestState.ReceiptsProperty, out var receiptsNode) ||
            receiptsNode.ValueKind != JsonValueKind.Array)
        {
            return;
        }

        var npcId = GetFirstNonEmptyString(npc, "npcId", "NPCId") ?? "";
        var normalizedMerchantProfile = ResolveNormalizedMerchantProfileForValidation(npc);
        var hasTradeInventory = npc.TryGetProperty("tradeInventory", out var tradeInventory) && tradeInventory.ValueKind == JsonValueKind.Object;
        var tradeCycleId = hasTradeInventory ? GetFirstNonEmptyString(tradeInventory, "tradeCycleId") ?? "" : "";
        var tradeItemCount = -1;
        var hasTradeInventoryItems = false;
        if (hasTradeInventory &&
            tradeInventory.TryGetProperty("items", out var items) &&
            items.ValueKind == JsonValueKind.Array)
        {
            hasTradeInventoryItems = true;
            tradeItemCount = items.GetArrayLength();
        }

        var seenRequestIds = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        var currentCycleReadyReceipts = 0;
        var receiptIndex = 0;
        foreach (var receipt in receiptsNode.EnumerateArray())
        {
            var receiptContext = $"{npcContext}.{NpcTradeRequestState.ReceiptsProperty}[{receiptIndex++}]";
            if (!RequireObject(receipt, receiptContext, issues))
                continue;

            var receiptSnapshot = ReadNpcTradeReceiptValidationSnapshot(receipt);
            if (!string.IsNullOrWhiteSpace(receiptSnapshot.RequestId) && !seenRequestIds.Add(receiptSnapshot.RequestId))
            {
                issues.Add(new ValidationIssue(
                    $"{receiptContext}.requestId",
                    IssueSeverity.Error,
                    "npc trade receipts содержит duplicate requestId",
                    code: "npc_trade_receipts_duplicate_request_id",
                    section: "tradeInventory",
                    actual: receiptSnapshot.RequestId));
            }

            if (!string.IsNullOrWhiteSpace(receiptSnapshot.NpcId) &&
                !string.Equals(receiptSnapshot.NpcId, npcId, StringComparison.OrdinalIgnoreCase))
            {
                issues.Add(new ValidationIssue(
                    $"{receiptContext}.npcId",
                    IssueSeverity.Error,
                    "npc trade receipt.npcId должен совпадать с владельцем receipt",
                    code: "npc_trade_receipt_npc_mismatch",
                    section: "tradeInventory",
                    expected: npcId,
                    actual: receiptSnapshot.NpcId));
            }

            if (!string.IsNullOrWhiteSpace(receiptSnapshot.MerchantProfile) &&
                !string.IsNullOrWhiteSpace(normalizedMerchantProfile) &&
                !string.Equals(NpcTradeService.ResolveMerchantProfileCode(receiptSnapshot.MerchantProfile), normalizedMerchantProfile, StringComparison.OrdinalIgnoreCase))
            {
                issues.Add(new ValidationIssue(
                    $"{receiptContext}.merchantProfile",
                    IssueSeverity.Error,
                    "npc trade receipt.merchantProfile должен совпадать с merchantProfile НПС",
                    code: "npc_trade_receipt_profile_mismatch",
                    section: "tradeInventory",
                    expected: normalizedMerchantProfile,
                    actual: receiptSnapshot.MerchantProfile));
            }

            if (hasTradeInventoryItems &&
                !string.IsNullOrWhiteSpace(receiptSnapshot.TradeCycleId) &&
                !string.IsNullOrWhiteSpace(tradeCycleId) &&
                string.Equals(receiptSnapshot.TradeCycleId, tradeCycleId, StringComparison.OrdinalIgnoreCase))
            {
                currentCycleReadyReceipts++;

                if (receiptSnapshot.ItemCount != tradeItemCount)
                {
                    issues.Add(new ValidationIssue(
                        $"{receiptContext}.itemCount",
                        IssueSeverity.Error,
                        "npc trade receipt.itemCount должен совпадать с количеством tradeInventory.items текущего цикла",
                        code: "npc_trade_receipt_item_count_mismatch",
                        section: "tradeInventory",
                        expected: tradeItemCount.ToString(),
                        actual: receiptSnapshot.ItemCount < 0 ? "missing" : receiptSnapshot.ItemCount.ToString()));
                }
            }
        }

        if (currentCycleReadyReceipts > 1)
        {
            issues.Add(new ValidationIssue(
                $"{npcContext}.{NpcTradeRequestState.ReceiptsProperty}",
                IssueSeverity.Error,
                "Для одного NPC и текущего tradeCycleId допустим только один ready receipt",
                code: "npc_trade_receipts_duplicate_current_cycle",
                section: "tradeInventory",
                actual: currentCycleReadyReceipts.ToString()));
        }
    }

    private static string ResolveNormalizedMerchantProfileForValidation(JsonElement npc)
    {
        var merchantProfile = "";
        if (npc.TryGetProperty("tradeState", out var tradeState) && tradeState.ValueKind == JsonValueKind.Object)
            merchantProfile = GetFirstNonEmptyString(tradeState, "merchantProfile") ?? "";

        return NpcTradeService.ResolveMerchantProfileCode(
            merchantProfile,
            GetFirstNonEmptyString(npc, "role"),
            GetFirstNonEmptyString(npc, "occupation"),
            GetFirstNonEmptyString(npc, "class"),
            GetFirstNonEmptyString(npc, "name"));
    }

    private void ValidateNpcTradeState(JsonElement npc, string npcContext, List<ValidationIssue> issues)
    {
        if (npc.TryGetProperty("tradeState", out var tradeState))
        {
            if (!RequireObject(tradeState, $"{npcContext}.tradeState", issues))
                return;

            if (tradeState.TryGetProperty("canTrade", out var canTrade) &&
                canTrade.ValueKind != JsonValueKind.True &&
                canTrade.ValueKind != JsonValueKind.False)
            {
                issues.Add(new ValidationIssue(
                    $"{npcContext}.tradeState.canTrade",
                    IssueSeverity.Error,
                    "tradeState.canTrade должен быть boolean",
                    code: "npc_trade_can_trade_invalid",
                    section: "tradeInventory",
                    expected: "true or false",
                    actual: canTrade.ValueKind.ToString(),
                    repairHint: "Сохраняй tradeState.canTrade как boolean. Если NPC временно не может торговать, оставь canTrade = false и заполни tradeBlockedReason."));
            }

            ValidateOptionalString(tradeState, $"{npcContext}.tradeState", issues, "tradeBlockedReason");
            ValidateOptionalString(tradeState, $"{npcContext}.tradeState", issues, "merchantProfile");
            var merchantProfileCode = GetFirstNonEmptyString(tradeState, "merchantProfile");
            if (!string.IsNullOrWhiteSpace(merchantProfileCode) && !NpcTradeService.IsValidMerchantProfileCode(merchantProfileCode))
            {
                issues.Add(new ValidationIssue(
                    $"{npcContext}.tradeState.merchantProfile",
                    IssueSeverity.Error,
                    "tradeState.merchantProfile должен быть допустимым NPC merchant profile",
                    code: "npc_trade_profile_invalid",
                    section: "tradeInventory",
                    expected: "GeneralGoods | Equipment | CraftingSupplies | Consumables | KnowledgeAndMedia | LuxuryAndDecor | ArtifactsAndCurios | TechnicalGoods | IllicitGoods",
                    actual: merchantProfileCode));
            }
        }
        else if (npc.TryGetProperty("tradeInventory", out _))
        {
            issues.Add(new ValidationIssue(
                $"{npcContext}.tradeState",
                IssueSeverity.Error,
                "NPC с tradeInventory должен иметь tradeState.canTrade = true",
                code: "npc_trade_state_missing_for_inventory",
                section: "tradeInventory",
                expected: "tradeState.canTrade = true",
                actual: "tradeState missing"));
        }

        var hasTradeState = npc.TryGetProperty("tradeState", out tradeState) && tradeState.ValueKind == JsonValueKind.Object;
        var hasCanTradeTrue = false;
        if (hasTradeState)
        {
            if (tradeState.TryGetProperty("canTrade", out var canTradeNode) && canTradeNode.ValueKind == JsonValueKind.True)
                hasCanTradeTrue = true;
        }
        var normalizedMerchantProfile = ResolveNormalizedMerchantProfileForValidation(npc);

        ValidateNpcBuybackInventory(npc, npcContext, normalizedMerchantProfile, issues);

        if (!npc.TryGetProperty("tradeInventory", out var tradeInventory))
            return;

        if (!RequireObject(tradeInventory, $"{npcContext}.tradeInventory", issues))
            return;

        var tradeContext = $"{npcContext}.tradeInventory";
        var tradeCycleId = RequireString(tradeInventory, tradeContext, issues, "tradeCycleId");
        ValidateNonNegativeNumberField(tradeInventory, tradeContext, issues, "generatedAtWorldDate");
        ValidatePositiveNumberField(tradeInventory, tradeContext, issues, "refreshAfterWorldDate");
        var generationTier = RequireString(tradeInventory, tradeContext, issues, "generationTradeTier");
        var pricingTier = RequireString(tradeInventory, tradeContext, issues, "pricingTradeTier");

        if (!string.IsNullOrWhiteSpace(generationTier) && !NpcTradeService.IsValidGenerationTierCode(generationTier))
        {
            issues.Add(new ValidationIssue(
                $"{tradeContext}.generationTradeTier",
                IssueSeverity.Error,
                "tradeInventory.generationTradeTier должен быть допустимым NPC trade tier",
                code: "npc_trade_inventory_generation_tier_invalid",
                section: "tradeInventory",
                expected: "Poor | Standard | Good | Premium | Elite",
                actual: generationTier));
        }

        if (!string.IsNullOrWhiteSpace(pricingTier) && !NpcTradeService.IsValidPricingTierCode(pricingTier))
        {
            issues.Add(new ValidationIssue(
                $"{tradeContext}.pricingTradeTier",
                IssueSeverity.Error,
                "tradeInventory.pricingTradeTier должен быть допустимым NPC pricing tier",
                code: "npc_trade_inventory_pricing_tier_invalid",
                section: "tradeInventory",
                expected: "Hostile | Wary | Neutral | Warm | Trusted",
                actual: pricingTier));
        }

        if (tradeInventory.TryGetProperty("generatedAtWorldDate", out var generatedAtNode) &&
            tradeInventory.TryGetProperty("refreshAfterWorldDate", out var refreshAfterNode) &&
            generatedAtNode.ValueKind == JsonValueKind.Number &&
            refreshAfterNode.ValueKind == JsonValueKind.Number &&
            generatedAtNode.TryGetInt32(out var generatedAt) &&
            refreshAfterNode.TryGetInt32(out var refreshAfter) &&
            refreshAfter <= generatedAt)
        {
            issues.Add(new ValidationIssue(
                $"{tradeContext}.refreshAfterWorldDate",
                IssueSeverity.Error,
                "tradeInventory.refreshAfterWorldDate должен быть больше generatedAtWorldDate",
                code: "npc_trade_inventory_refresh_window_invalid",
                section: "tradeInventory",
                expected: "> generatedAtWorldDate",
                actual: refreshAfter.ToString()));
        }

        if (string.IsNullOrWhiteSpace(tradeCycleId))
        {
            issues.Add(new ValidationIssue(
                $"{tradeContext}.tradeCycleId",
                IssueSeverity.Error,
                "tradeInventory.tradeCycleId должен быть непустой строкой world-time цикла",
                code: "npc_trade_inventory_cycle_id_missing",
                section: "tradeInventory",
                expected: "non-empty tradeCycleId",
                actual: "missing-or-empty"));
        }

        if (!tradeInventory.TryGetProperty("items", out var items) || items.ValueKind != JsonValueKind.Array)
        {
            issues.Add(new ValidationIssue(
                $"{tradeContext}.items",
                IssueSeverity.Error,
                "tradeInventory.items должен быть массивом торговых слотов",
                code: "npc_trade_inventory_items_invalid",
                section: "tradeInventory",
                expected: "array of trade slot objects",
                actual: !tradeInventory.TryGetProperty("items", out _) ? "missing" : items.ValueKind.ToString(),
                repairHint: "Сохрани tradeInventory.items как массив торговых слотов NPC. Не заменяй его scalar/object заглушкой и не убирай поле при наличии tradeInventory."));
            return;
        }

        if (items.GetArrayLength() < 6 || items.GetArrayLength() > 20)
        {
            issues.Add(new ValidationIssue(
                $"{tradeContext}.items",
                IssueSeverity.Error,
                "tradeInventory.items должен содержать от 6 до 20 торговых слотов",
                code: "npc_trade_inventory_items_count_invalid",
                section: "tradeInventory",
                expected: "6-20 trade slots",
                actual: items.GetArrayLength().ToString(),
                repairHint: "Сохраняй в tradeInventory.items от 6 до 20 торговых слотов по canonical NPC trade contract."));
        }

        if (hasCanTradeTrue && string.IsNullOrWhiteSpace(normalizedMerchantProfile))
        {
            issues.Add(new ValidationIssue(
                $"{npcContext}.tradeState.merchantProfile",
                IssueSeverity.Error,
                "tradeState.canTrade = true требует валидный merchantProfile или разрешимый торговый archetype NPC",
                code: "npc_trade_requires_valid_profile",
                section: "tradeInventory",
                expected: "valid merchant profile",
                actual: hasTradeState ? GetFirstNonEmptyString(tradeState, "merchantProfile") ?? "missing" : "missing"));
        }

        if (!hasCanTradeTrue)
        {
            issues.Add(new ValidationIssue(
                $"{npcContext}.tradeState.canTrade",
                IssueSeverity.Error,
                "NPC tradeInventory допустим только при tradeState.canTrade = true",
                code: "npc_trade_inventory_requires_can_trade",
                section: "tradeInventory",
                expected: "true",
                actual: hasTradeState ? "missing-or-false" : "tradeState missing"));
        }

        var npcTrade = ReadNpcTradeValueFromValidationState(npc);
        var playerTrade = ReadPlayerTradeValueForValidation();

        var index = 0;
        foreach (var item in items.EnumerateArray())
        {
            var itemContext = $"{tradeContext}.items[{index++}]";
            if (!RequireObject(item, itemContext, issues))
                continue;

            RequireString(item, itemContext, issues, "slotId");
            ValidateNonNegativeNumberField(item, itemContext, issues, "price");
            var itemMerchantProfileRequired = RequireString(item, itemContext, issues, "merchantProfile");
            if (!string.IsNullOrWhiteSpace(itemMerchantProfileRequired) && !NpcTradeService.IsValidMerchantProfileCode(itemMerchantProfileRequired))
            {
                issues.Add(new ValidationIssue(
                    $"{itemContext}.merchantProfile",
                    IssueSeverity.Error,
                    "tradeInventory item.merchantProfile должен быть допустимым NPC merchant profile",
                    code: "npc_trade_inventory_item_profile_invalid",
                    section: "tradeInventory",
                    expected: "GeneralGoods | Equipment | CraftingSupplies | Consumables | KnowledgeAndMedia | LuxuryAndDecor | ArtifactsAndCurios | TechnicalGoods | IllicitGoods",
                    actual: itemMerchantProfileRequired));
            }

            if (item.TryGetProperty("soldOut", out var soldOut) &&
                soldOut.ValueKind != JsonValueKind.True &&
                soldOut.ValueKind != JsonValueKind.False)
            {
                issues.Add(new ValidationIssue(
                    $"{itemContext}.soldOut",
                    IssueSeverity.Error,
                    "tradeInventory item.soldOut должен быть boolean",
                    code: "npc_trade_inventory_item_sold_out_invalid",
                    section: "tradeInventory",
                    expected: "true or false",
                    actual: soldOut.ValueKind.ToString(),
                    repairHint: "Сохраняй soldOut как boolean. Если слот временно недоступен, оставь soldOut = true, а не string/number surrogate."));
            }

            if (!string.IsNullOrWhiteSpace(normalizedMerchantProfile))
            {
                var itemMerchantProfile = GetFirstNonEmptyString(item, "merchantProfile");
                var normalizedItemProfile = NpcTradeService.ResolveMerchantProfileCode(itemMerchantProfile);
                if (!string.IsNullOrWhiteSpace(itemMerchantProfile) &&
                    !string.Equals(normalizedItemProfile, normalizedMerchantProfile, StringComparison.OrdinalIgnoreCase))
                {
                    issues.Add(new ValidationIssue(
                        $"{itemContext}.merchantProfile",
                        IssueSeverity.Error,
                        "tradeInventory item.merchantProfile должен совпадать с merchantProfile НПС",
                        code: "npc_trade_inventory_profile_mismatch",
                        section: "tradeInventory",
                        expected: normalizedMerchantProfile,
                        actual: itemMerchantProfile,
                        repairHint: "В tradeInventory.items используй тот же merchantProfile, что и у самого NPC merchant surface. Не смешивай предметы разных торговых профилей в одном canonical trade inventory."));
                }
            }

            if (!item.TryGetProperty("itemData", out var itemData) || !RequireObject(itemData, $"{itemContext}.itemData", issues))
                continue;

            var itemDataContext = $"{itemContext}.itemData";
            RequireString(itemData, itemDataContext, issues, "itemId");
            RequireString(itemData, itemDataContext, issues, "name");
            var tradeItemClass = RequireString(itemData, itemDataContext, issues, "tradeItemClass");
            ValidatePositiveNumberField(itemData, itemDataContext, issues, "price");
            ValidateNonNegativeNumberField(itemData, itemDataContext, issues, "baseSellPrice");
            var rarity = GetFirstNonEmptyString(itemData, "quality", "rarity");
            if (string.IsNullOrWhiteSpace(rarity))
            {
                issues.Add(new ValidationIssue(
                    itemDataContext,
                    IssueSeverity.Error,
                    "itemData должен содержать quality или rarity"));
                continue;
            }

            if (!string.IsNullOrWhiteSpace(generationTier) &&
                NpcTradeService.IsValidGenerationTierCode(generationTier) &&
                !NpcTradeService.IsRarityAllowedForGenerationTier(rarity, generationTier))
            {
                issues.Add(new ValidationIssue(
                    $"{itemDataContext}.quality",
                    IssueSeverity.Error,
                    "tradeInventory item rarity превышает допустимый потолок редкости для generationTradeTier",
                    code: "npc_trade_inventory_rarity_cap_mismatch",
                    section: "tradeInventory",
                    expected: generationTier,
                    actual: rarity));
            }

            if (!string.IsNullOrWhiteSpace(tradeItemClass) && !NpcTradeService.IsValidTradeItemClassCode(tradeItemClass))
            {
                issues.Add(new ValidationIssue(
                    $"{itemDataContext}.tradeItemClass",
                    IssueSeverity.Error,
                    "tradeInventory itemData.tradeItemClass должен быть допустимым trade item class",
                    code: "npc_trade_inventory_item_class_invalid",
                    section: "tradeInventory",
                    expected: "Functional | Material | FlavorOrUtility",
                    actual: tradeItemClass));
            }

            if (!string.IsNullOrWhiteSpace(pricingTier) &&
                NpcTradeService.IsValidPricingTierCode(pricingTier) &&
                item.TryGetProperty("price", out var priceNode) &&
                priceNode.ValueKind == JsonValueKind.Number &&
                priceNode.TryGetInt32(out var actualPrice) &&
                itemData.TryGetProperty("price", out var basePriceNode) &&
                basePriceNode.ValueKind == JsonValueKind.Number &&
                basePriceNode.TryGetInt32(out var basePrice))
            {
                var expectedPrice = NpcTradeService.ComputeBuyPriceForValidation(basePrice, playerTrade, npcTrade, pricingTier);
                if (actualPrice != expectedPrice)
                {
                    issues.Add(new ValidationIssue(
                        $"{itemContext}.price",
                        IssueSeverity.Error,
                        "tradeInventory item.price должен совпадать с канонической ценой для pricingTradeTier",
                        code: "npc_trade_inventory_price_mismatch",
                        section: "tradeInventory",
                        expected: expectedPrice.ToString(),
                        actual: actualPrice.ToString()));
                }
            }
        }

        ValidateNpcTradeReceipts(npc, npcContext, issues);
    }

    private void ValidateNpcTradeReceipts(JsonElement npc, string npcContext, List<ValidationIssue> issues)
    {
        if (!npc.TryGetProperty(NpcTradeRequestState.ReceiptsProperty, out var receiptsNode))
            return;

        if (receiptsNode.ValueKind != JsonValueKind.Array)
        {
            issues.Add(new ValidationIssue(
                $"{npcContext}.{NpcTradeRequestState.ReceiptsProperty}",
                IssueSeverity.Error,
                "npc tradeInventoryReceipts должен быть массивом canonical receipts",
                code: "npc_trade_receipts_invalid_root",
                section: "tradeInventory",
                expected: "array",
                actual: receiptsNode.ValueKind.ToString(),
                repairHint: "Храни npc trade ready receipts как массив объектов в npc.tradeInventoryReceipts."));
            return;
        }

        var seenRequestIds = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        var receiptIndex = 0;
        foreach (var receipt in receiptsNode.EnumerateArray())
        {
            var receiptContext = $"{npcContext}.{NpcTradeRequestState.ReceiptsProperty}[{receiptIndex++}]";
            if (!RequireObject(receipt, receiptContext, issues))
                continue;

            ValidateNpcTradeReceiptSchema(receipt, receiptContext, issues, seenRequestIds);
        }

        ValidateNpcTradeReceiptStateConsistency(npc, npcContext, issues);
    }

    private void ValidateNpcBuybackInventory(JsonElement npc, string npcContext, string? normalizedMerchantProfile, List<ValidationIssue> issues)
    {
        if (!npc.TryGetProperty("buybackInventory", out var buybackInventoryNode))
            return;

        if (buybackInventoryNode.ValueKind != JsonValueKind.Array)
        {
            issues.Add(new ValidationIssue(
                $"{npcContext}.buybackInventory",
                IssueSeverity.Error,
                "npc buybackInventory должен быть массивом canonical buyback entries",
                code: "npc_buyback_inventory_invalid_root",
                section: "tradeInventory",
                expected: "array",
                actual: buybackInventoryNode.ValueKind.ToString(),
                repairHint: "Храни выкупленные у игрока товары в npc.buybackInventory как массив объектов, а не scalar/object surrogate."));
            return;
        }

        var npcId = GetFirstNonEmptyString(npc, "npcId", "NPCId") ?? "";
        var seenEntryIds = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        var index = 0;
        foreach (var entry in buybackInventoryNode.EnumerateArray())
        {
            var entryContext = $"{npcContext}.buybackInventory[{index++}]";
            if (!RequireObject(entry, entryContext, issues))
                continue;

            var buybackEntryId = RequireString(entry, entryContext, issues, "buybackEntryId");
            var entryNpcId = RequireString(entry, entryContext, issues, "npcId");
            ValidateOptionalString(entry, entryContext, issues, "npcName");
            var itemId = RequireString(entry, entryContext, issues, "itemId");
            ValidateNonNegativeNumberField(entry, entryContext, issues, "soldByPlayerAtTurn");
            ValidateOptionalString(entry, entryContext, issues, "soldByPlayerAtUtc");
            ValidateNonNegativeNumberField(entry, entryContext, issues, "soldAtWorldDate");
            ValidatePositiveNumberField(entry, entryContext, issues, "soldForPrice");
            ValidatePositiveNumberField(entry, entryContext, issues, "buybackPrice");
            if (entry.TryGetProperty("acquiredFromPlayer", out var acquiredFromPlayerNode) &&
                acquiredFromPlayerNode.ValueKind != JsonValueKind.True &&
                acquiredFromPlayerNode.ValueKind != JsonValueKind.False)
            {
                issues.Add(new ValidationIssue(
                    $"{entryContext}.acquiredFromPlayer",
                    IssueSeverity.Error,
                    "npc buyback entry.acquiredFromPlayer должен быть boolean",
                    code: "npc_buyback_entry_acquired_flag_invalid",
                    section: "tradeInventory",
                    expected: "true or false",
                    actual: acquiredFromPlayerNode.ValueKind.ToString()));
            }
            var sourceMerchantProfile = RequireString(entry, entryContext, issues, "sourceMerchantProfile");
            var status = RequireString(entry, entryContext, issues, "status");

            if (!string.IsNullOrWhiteSpace(buybackEntryId) && !seenEntryIds.Add(buybackEntryId))
            {
                issues.Add(new ValidationIssue(
                    $"{entryContext}.buybackEntryId",
                    IssueSeverity.Error,
                    "npc buybackInventory содержит duplicate buybackEntryId",
                    code: "npc_buyback_entry_duplicate_id",
                    section: "tradeInventory",
                    actual: buybackEntryId));
            }

            if (!string.IsNullOrWhiteSpace(entryNpcId) &&
                !string.Equals(entryNpcId, npcId, StringComparison.OrdinalIgnoreCase))
            {
                issues.Add(new ValidationIssue(
                    $"{entryContext}.npcId",
                    IssueSeverity.Error,
                    "npc buyback entry.npcId должен совпадать с владельцем записи",
                    code: "npc_buyback_entry_npc_mismatch",
                    section: "tradeInventory",
                    expected: npcId,
                    actual: entryNpcId));
            }

            if (!string.IsNullOrWhiteSpace(sourceMerchantProfile) &&
                !NpcTradeService.IsValidMerchantProfileCode(sourceMerchantProfile))
            {
                issues.Add(new ValidationIssue(
                    $"{entryContext}.sourceMerchantProfile",
                    IssueSeverity.Error,
                    "npc buyback entry.sourceMerchantProfile должен быть допустимым merchant profile",
                    code: "npc_buyback_entry_profile_invalid",
                    section: "tradeInventory",
                    actual: sourceMerchantProfile));
            }

            if (!string.IsNullOrWhiteSpace(sourceMerchantProfile) &&
                !string.IsNullOrWhiteSpace(normalizedMerchantProfile) &&
                !string.Equals(NpcTradeService.ResolveMerchantProfileCode(sourceMerchantProfile), normalizedMerchantProfile, StringComparison.OrdinalIgnoreCase))
            {
                issues.Add(new ValidationIssue(
                    $"{entryContext}.sourceMerchantProfile",
                    IssueSeverity.Error,
                    "npc buyback entry.sourceMerchantProfile должен совпадать с merchantProfile НПС",
                    code: "npc_buyback_entry_profile_mismatch",
                    section: "tradeInventory",
                    expected: normalizedMerchantProfile,
                    actual: sourceMerchantProfile));
            }

            if (!string.IsNullOrWhiteSpace(status) && !NpcTradeService.IsValidBuybackStatusCode(status))
            {
                issues.Add(new ValidationIssue(
                    $"{entryContext}.status",
                    IssueSeverity.Error,
                    "npc buyback entry.status должен быть допустимым состоянием buyback",
                    code: "npc_buyback_entry_status_invalid",
                    section: "tradeInventory",
                    expected: "available | rebought | removed",
                    actual: status));
            }

            if (!entry.TryGetProperty("itemData", out var itemData) || !RequireObject(itemData, $"{entryContext}.itemData", issues))
                continue;

            var itemDataContext = $"{entryContext}.itemData";
            var itemDataItemId = RequireString(itemData, itemDataContext, issues, "itemId");
            RequireString(itemData, itemDataContext, issues, "name");
            ValidatePositiveNumberField(itemData, itemDataContext, issues, "price");
            ValidateNonNegativeNumberField(itemData, itemDataContext, issues, "baseSellPrice");

            if (!string.IsNullOrWhiteSpace(itemId) &&
                !string.IsNullOrWhiteSpace(itemDataItemId) &&
                !string.Equals(itemId, itemDataItemId, StringComparison.OrdinalIgnoreCase))
            {
                issues.Add(new ValidationIssue(
                    $"{entryContext}.itemId",
                    IssueSeverity.Error,
                    "npc buyback entry.itemId должен совпадать с itemData.itemId",
                    code: "npc_buyback_entry_item_mismatch",
                    section: "tradeInventory",
                    expected: itemDataItemId,
                    actual: itemId));
            }

            if (string.Equals(status, "rebought", StringComparison.OrdinalIgnoreCase))
            {
                if (!entry.TryGetProperty("reboughtAtTurn", out var reboughtAtTurnNode) ||
                    reboughtAtTurnNode.ValueKind != JsonValueKind.Number ||
                    !reboughtAtTurnNode.TryGetInt32(out var reboughtAtTurn) ||
                    reboughtAtTurn <= 0)
                {
                    issues.Add(new ValidationIssue(
                        $"{entryContext}.reboughtAtTurn",
                        IssueSeverity.Error,
                        "rebought buyback entry должен иметь положительный reboughtAtTurn",
                        code: "npc_buyback_entry_rebought_turn_invalid",
                        section: "tradeInventory",
                        expected: "positive integer",
                        actual: entry.TryGetProperty("reboughtAtTurn", out var rawTurn) ? rawTurn.ToString() : "missing"));
                }

                var reboughtAtUtc = GetFirstNonEmptyString(entry, "reboughtAtUtc");
                if (string.IsNullOrWhiteSpace(reboughtAtUtc) || !DateTimeOffset.TryParse(reboughtAtUtc, out _))
                {
                    issues.Add(new ValidationIssue(
                        $"{entryContext}.reboughtAtUtc",
                        IssueSeverity.Error,
                        "rebought buyback entry должен иметь валидный reboughtAtUtc",
                        code: "npc_buyback_entry_rebought_timestamp_invalid",
                        section: "tradeInventory",
                        actual: reboughtAtUtc ?? "missing"));
                }
            }
        }
    }

    private int ReadNpcTradeValueFromValidationState(JsonElement npc)
    {
        if (npc.TryGetProperty("characteristics", out var chars) && chars.ValueKind == JsonValueKind.Object)
        {
            if (TryReadInt(chars, "modifiedTrade", out var modified))
                return modified;
            if (TryReadInt(chars, "standardTrade", out var standard))
                return standard;
            if (TryReadInt(chars, "trade", out var flat))
                return flat;
        }

        return 10;
    }

    private int ReadPlayerTradeValueForValidation()
    {
        foreach (var path in new[] { "game_state/misc/characteristics.json", "game_state/player/player_status.json", "game_state/core/player_status.json" })
        {
            try
            {
                var json = _fs.ReadFileAsync(path).GetAwaiter().GetResult();
                if (string.IsNullOrWhiteSpace(json))
                    continue;

                using var doc = JsonDocument.Parse(json);
                var root = doc.RootElement;
                if (TryReadInt(root, "modifiedTrade", out var modified))
                    return modified;
                if (TryReadInt(root, "trade", out var flat))
                    return flat;
            }
            catch
            {
                // ignore and try next source
            }
        }

        return 10;
    }

    private static bool TryReadInt(JsonElement root, string propertyName, out int value)
    {
        value = 0;
        if (!root.TryGetProperty(propertyName, out var prop))
            return false;

        return prop.ValueKind switch
        {
            JsonValueKind.Number => prop.TryGetInt32(out value),
            JsonValueKind.String => int.TryParse(prop.GetString(), out value),
            _ => false
        };
    }

    private static bool TryReadDouble(JsonElement root, string propertyName, out double value)
    {
        value = 0;
        if (!root.TryGetProperty(propertyName, out var prop))
            return false;

        return prop.ValueKind switch
        {
            JsonValueKind.Number => prop.TryGetDouble(out value),
            JsonValueKind.String => double.TryParse(prop.GetString(), out value),
            _ => false
        };
    }

    private void ValidateNpcRenameData(JsonElement root, string contextPrefix, List<ValidationIssue> issues)
    {
        if (!TryGetArray(root, "NPCsRenameData", $"{contextPrefix}.NPCsRenameData", issues, out var arr))
            return;

        var knownNpcReferences = ReadKnownNpcReferencesSync();
        var index = 0;
        foreach (var item in arr.EnumerateArray())
        {
            var itemContext = $"{contextPrefix}.NPCsRenameData[{index++}]";
            if (!RequireObject(item, itemContext, issues)) continue;
            var oldName = RequireString(item, itemContext, issues, "oldName");
            RequireString(item, itemContext, issues, "newName");

            if ((knownNpcReferences.Ids.Count > 0 || knownNpcReferences.Names.Count > 0) &&
                !string.IsNullOrWhiteSpace(oldName) &&
                !knownNpcReferences.Names.Contains(oldName))
            {
                issues.Add(new ValidationIssue(
                    $"{itemContext}.oldName",
                    IssueSeverity.Error,
                    "NPCsRenameData.oldName должен ссылаться на точное текущее имя NPC из Context/pre-turn npc_core state",
                    code: "npc_rename_unknown_old_name",
                    section: "NPC",
                    expected: "exact old NPC name from pre-turn/current npc_core.json",
                    actual: oldName,
                    repairHint: "Для NPC rename используй exact oldName из Context / npc_core.json. Не придумывай промежуточное имя и не переименовывай неизвестного NPC."));
            }
        }
    }

    private void ValidateNpcJournals(JsonElement root, string contextPrefix, List<ValidationIssue> issues)
    {
        if (!(TryGetArray(root, "NPCJournals", $"{contextPrefix}.NPCJournals", issues, out var arr) ||
              TryGetArray(root, "npcJournals", $"{contextPrefix}.npcJournals", issues, out arr)))
            return;

        var knownNpcReferences = ReadKnownNpcReferencesSync();
        var index = 0;
        foreach (var item in arr.EnumerateArray())
        {
            var itemContext = $"{contextPrefix}.npcJournals[{index++}]";
            if (!RequireObject(item, itemContext, issues)) continue;
            RequireNpcIdentity(item, itemContext, issues);

            if ((knownNpcReferences.Ids.Count > 0 || knownNpcReferences.Names.Count > 0) &&
                !NpcReferenceExists(item, knownNpcReferences))
            {
                issues.Add(new ValidationIssue(
                    itemContext,
                    IssueSeverity.Error,
                    "NPCJournals ссылается на NPC, которого нет в canonical npc_core state",
                    code: "npc_journal_unknown_npc_reference",
                    section: "NPCJournals",
                    expected: "existing NPCId/NPCName from pre-turn/current npc_core.json",
                    actual: GetFirstNonEmptyString(item, "NPCId", "npcId", "id", "NPCName", "npcName", "name") ?? "missing",
                    repairHint: "Пиши NPCJournals только для реально существующего NPC из Context / npc_core.json. Не создавай orphan journal entry без соответствующего NPC."));
                continue;
            }

            var hasLegacyNote = HasNonEmptyString(item, "lastJournalNote");
            var hasStructuredEntries = item.TryGetProperty("journalEntries", out var journalEntries) &&
                                       journalEntries.ValueKind == JsonValueKind.Array;
            if (!hasLegacyNote && !hasStructuredEntries)
            {
                issues.Add(new ValidationIssue(
                    itemContext,
                    IssueSeverity.Error,
                    "NPCJournals entry должен содержать lastJournalNote или journalEntries[]"));
                continue;
            }

            if (hasStructuredEntries)
            {
                var entryIndex = 0;
                foreach (var journalEntry in journalEntries.EnumerateArray())
                {
                    var journalEntryContext = $"{itemContext}.journalEntries[{entryIndex++}]";
                    if (!RequireObject(journalEntry, journalEntryContext, issues))
                        continue;

                    RequireString(journalEntry, journalEntryContext, issues, "description");
                    ValidateOptionalString(journalEntry, journalEntryContext, issues, "timestamp");
                    ValidateOptionalString(journalEntry, journalEntryContext, issues, "event");
                    ValidateOptionalString(journalEntry, journalEntryContext, issues, "emotionalImpact");
                    ValidateOptionalString(journalEntry, journalEntryContext, issues, "relationshipChange");
                }
            }
        }
    }

    private void ValidateNpcUnlockedMemories(JsonElement root, string contextPrefix, List<ValidationIssue> issues)
    {
        if (!TryGetArray(root, "NPCUnlockedMemories", $"{contextPrefix}.NPCUnlockedMemories", issues, out var arr))
            return;

        var knownNpcReferences = ReadKnownNpcReferencesSync();
        var preTurnMemoryIdsByNpc = ReadPreTurnNpcUnlockedMemoryIdsByNpcSync();
        var currentTurnMemoryIdsByNpc = new Dictionary<string, HashSet<string>>(StringComparer.OrdinalIgnoreCase);
        var index = 0;
        foreach (var item in arr.EnumerateArray())
        {
            var itemContext = $"{contextPrefix}.NPCUnlockedMemories[{index++}]";
            if (!RequireObject(item, itemContext, issues)) continue;
            RequireNpcIdentity(item, itemContext, issues);
            if ((knownNpcReferences.Ids.Count > 0 || knownNpcReferences.Names.Count > 0) &&
                !NpcReferenceExists(item, knownNpcReferences))
            {
                issues.Add(new ValidationIssue(
                    itemContext,
                    IssueSeverity.Error,
                    "NPCUnlockedMemories ссылается на NPC, которого нет в canonical npc_core state",
                    code: "npc_unlocked_memory_unknown_npc_reference",
                    section: "NPCMemory",
                    expected: "existing NPCId/NPCName from pre-turn/current npc_core.json",
                    actual: GetFirstNonEmptyString(item, "NPCId", "npcId", "id", "NPCName", "npcName", "name") ?? "missing",
                    repairHint: "Открывай воспоминания только для реально существующего NPC из Context / npc_core.json. Не создавай orphan memory entry."));
                continue;
            }

            var memoryId = RequireString(item, itemContext, issues, "memoryId");
            RequireString(item, itemContext, issues, "title");
            RequireString(item, itemContext, issues, "content");
            RequireNumberOrString(item, itemContext, issues, "unlockedAtRelationshipLevel");

            if (!string.IsNullOrWhiteSpace(memoryId))
            {
                if (NpcMemoryIdExists(item, memoryId, preTurnMemoryIdsByNpc))
                {
                    issues.Add(new ValidationIssue(
                        $"{itemContext}.memoryId",
                        IssueSeverity.Error,
                        "NPCUnlockedMemories должен содержать только NEWLY unlocked memories текущего хода",
                        code: "npc_unlocked_memory_already_known",
                        section: "NPCMemory",
                        expected: "new memoryId not already present in pre-turn npc_memory.json for this NPC",
                        actual: memoryId,
                        repairHint: "Не переотправляй уже сохранённую память. В NPCUnlockedMemories добавляй только новые memoryId, которые открылись именно в текущем turn."));
                }
                else if (NpcMemoryIdExists(item, memoryId, currentTurnMemoryIdsByNpc))
                {
                    issues.Add(new ValidationIssue(
                        $"{itemContext}.memoryId",
                        IssueSeverity.Error,
                        "NPCUnlockedMemories содержит дубликат memoryId для одного и того же NPC в текущем accepted turn",
                        code: "npc_unlocked_memory_duplicate_in_turn",
                        section: "NPCMemory",
                        expected: "unique new memoryId per NPC within the current turn",
                        actual: memoryId,
                        repairHint: "Не дублируй одну и ту же память в NPCUnlockedMemories. Оставь только одну запись на каждый новый memoryId для конкретного NPC."));
                }

                RegisterNpcMemoryId(item, memoryId, currentTurnMemoryIdsByNpc);
            }
        }
    }

    private void ValidateNpcSkillChanges(JsonElement root, string contextPrefix, List<ValidationIssue> issues, string propName)
    {
        if (!TryGetArray(root, propName, $"{contextPrefix}.{propName}", issues, out var arr))
            return;

        var isActiveSkillArray = string.Equals(propName, "NPCActiveSkillChanges", StringComparison.OrdinalIgnoreCase);
        var index = 0;
        foreach (var item in arr.EnumerateArray())
        {
            var itemContext = $"{contextPrefix}.{propName}[{index++}]";
            if (!RequireObject(item, itemContext, issues)) continue;
            RequireNpcIdentity(item, itemContext, issues);

            var hasSkillChanges = item.TryGetProperty("skillChanges", out var skillChanges);
            var hasSkillsToRemove = item.TryGetProperty("skillsToRemove", out var skillsToRemove);
            if (!hasSkillChanges && !hasSkillsToRemove)
            {
                issues.Add(new ValidationIssue(itemContext, IssueSeverity.Error,
                    "Требуется хотя бы одно из полей: skillChanges или skillsToRemove"));
                continue;
            }

            if (hasSkillChanges)
            {
                RequireArrayOfObjects(skillChanges, $"{itemContext}.skillChanges", issues);
                if (skillChanges.ValueKind == JsonValueKind.Array)
                {
                    var skillIndex = 0;
                    foreach (var skill in skillChanges.EnumerateArray())
                    {
                        var skillContext = $"{itemContext}.skillChanges[{skillIndex++}]";
                        if (!RequireObject(skill, skillContext, issues))
                            continue;

                        if (isActiveSkillArray)
                            ValidateActiveSkillObject(skill, skillContext, issues);
                        else
                            ValidatePassiveSkillObject(skill, skillContext, issues);
                    }
                }
            }
            if (hasSkillsToRemove)
                RequireArrayOfStrings(skillsToRemove, $"{itemContext}.skillsToRemove", issues);
        }
    }

    private void ValidateNpcSkillMastery(JsonElement root, string contextPrefix, List<ValidationIssue> issues,
        string propName, bool passive)
    {
        if (!TryGetArray(root, propName, $"{contextPrefix}.{propName}", issues, out var arr))
            return;

        var index = 0;
        foreach (var item in arr.EnumerateArray())
        {
            var itemContext = $"{contextPrefix}.{propName}[{index++}]";
            if (!RequireObject(item, itemContext, issues)) continue;
            RequireNpcIdentity(item, itemContext, issues);
            RequireString(item, itemContext, issues, "skillName");
            if (!item.TryGetProperty("newMasteryLevel", out _))
                issues.Add(new ValidationIssue($"{itemContext}.newMasteryLevel", IssueSeverity.Error, "Отсутствует обязательное поле: newMasteryLevel"));
            else
                ValidateIntegerField(item, itemContext, issues, "newMasteryLevel");
            if (passive)
            {
                if (item.TryGetProperty("newMaxMasteryLevel", out _))
                    ValidateIntegerField(item, itemContext, issues, "newMaxMasteryLevel");
            }
            else
            {
                if (!item.TryGetProperty("newCurrentMasteryProgress", out _))
                    issues.Add(new ValidationIssue($"{itemContext}.newCurrentMasteryProgress", IssueSeverity.Error, "Отсутствует обязательное поле: newCurrentMasteryProgress"));
                else
                    ValidateIntegerField(item, itemContext, issues, "newCurrentMasteryProgress");
                if (!item.TryGetProperty("newMasteryProgressNeeded", out _))
                    issues.Add(new ValidationIssue($"{itemContext}.newMasteryProgressNeeded", IssueSeverity.Error, "Отсутствует обязательное поле: newMasteryProgressNeeded"));
                else
                    ValidateIntegerField(item, itemContext, issues, "newMasteryProgressNeeded");
            }
        }
    }

    private void ValidateNpcInventoryAdds(JsonElement root, string contextPrefix, List<ValidationIssue> issues)
    {
        if (!TryGetArray(root, "NPCInventoryAdds", $"{contextPrefix}.NPCInventoryAdds", issues, out var arr))
            return;

        var index = 0;
        foreach (var item in arr.EnumerateArray())
        {
            var itemContext = $"{contextPrefix}.NPCInventoryAdds[{index++}]";
            if (!RequireObject(item, itemContext, issues)) continue;
            RequireNpcIdentity(item, itemContext, issues);
            ValidateOptionalNullableStringField(item, itemContext, issues, "destinationContainerId");
            if (item.TryGetProperty("item", out var inventoryItem) && RequireObject(inventoryItem, $"{itemContext}.item", issues))
            {
                ValidateFullInventoryItemObject(inventoryItem, $"{itemContext}.item", issues, requireStringExistedId: false);
                if (inventoryItem.TryGetProperty("existedId", out var existedId) &&
                    existedId.ValueKind != JsonValueKind.Null)
                {
                    issues.Add(new ValidationIssue(
                        $"{itemContext}.item.existedId",
                        IssueSeverity.Error,
                        "NPCInventoryAdds.item.existedId должен быть null для нового предмета",
                        code: "npc_inventory_add_item_existed_id_must_be_null",
                        section: "NPCInventory",
                        expected: "null for new NPC inventory item",
                        actual: existedId.ValueKind == JsonValueKind.String ? existedId.GetString() ?? string.Empty : existedId.ValueKind.ToString(),
                        repairHint: "Для NPCInventoryAdds передавай полный новый Item Object с existedId = null; existing NPC item меняй через NPCInventoryUpdates."));
                }
            }
            else
                issues.Add(new ValidationIssue(
                    $"{itemContext}.item",
                    IssueSeverity.Error,
                    "Отсутствует объект item",
                    code: "npc_inventory_add_missing_item",
                    section: "NPCInventory",
                    expected: "full new Item Object in item",
                    actual: "missing",
                    repairHint: "Для NPCInventoryAdds передай nested item с полным новым Item Object. Existing NPC item изменяй через NPCInventoryUpdates.itemUpdate."));
        }
    }

    private void ValidateNpcInventoryUpdates(JsonElement root, string contextPrefix, List<ValidationIssue> issues)
    {
        if (!TryGetArray(root, "NPCInventoryUpdates", $"{contextPrefix}.NPCInventoryUpdates", issues, out var arr))
            return;

        var index = 0;
        foreach (var item in arr.EnumerateArray())
        {
            var itemContext = $"{contextPrefix}.NPCInventoryUpdates[{index++}]";
            if (!RequireObject(item, itemContext, issues)) continue;
            RequireNpcIdentity(item, itemContext, issues);
            if (item.TryGetProperty("itemUpdate", out var itemUpdate) && itemUpdate.ValueKind == JsonValueKind.Object)
                ValidatePartialInventoryItemUpdate(itemUpdate, $"{itemContext}.itemUpdate", issues, section: "NPCInventory", forbidContentsPathMutation: false);
            else
                issues.Add(new ValidationIssue(
                    $"{itemContext}.itemUpdate",
                    IssueSeverity.Error,
                    "Отсутствует объект itemUpdate",
                    code: "npc_inventory_update_missing_item_update",
                    section: "NPCInventory",
                    expected: "itemUpdate object with existedId and changed fields",
                    actual: item.TryGetProperty("itemUpdate", out var missingUpdateNode) ? missingUpdateNode.ValueKind.ToString() : "missing",
                    repairHint: "Для NPCInventoryUpdates передай nested itemUpdate с existedId и хотя бы одним реально изменённым полем existing NPC item."));
        }
    }

    private void ValidateNpcInventoryRemovals(JsonElement root, string contextPrefix, List<ValidationIssue> issues)
    {
        if (!TryGetArray(root, "NPCInventoryRemovals", $"{contextPrefix}.NPCInventoryRemovals", issues, out var arr))
            return;

        var index = 0;
        foreach (var item in arr.EnumerateArray())
        {
            var itemContext = $"{contextPrefix}.NPCInventoryRemovals[{index++}]";
            if (!RequireObject(item, itemContext, issues)) continue;
            RequireNpcIdentity(item, itemContext, issues);
            RequireString(item, itemContext, issues, "itemId");
        }
    }

    private void ValidateNpcEquipmentChanges(JsonElement root, string contextPrefix, List<ValidationIssue> issues)
    {
        if (!TryGetArray(root, "NPCEquipmentChanges", $"{contextPrefix}.NPCEquipmentChanges", issues, out var arr))
            return;

        var index = 0;
        foreach (var item in arr.EnumerateArray())
        {
            var itemContext = $"{contextPrefix}.NPCEquipmentChanges[{index++}]";
            if (!RequireObject(item, itemContext, issues)) continue;
            RequireNpcIdentity(item, itemContext, issues);

            var action = RequireString(item, itemContext, issues, "action");
            var hasItemLink = HasNonEmptyString(item, "itemId") || HasNonEmptyString(item, "itemName");
            if (!hasItemLink)
                issues.Add(new ValidationIssue(
                    itemContext,
                    IssueSeverity.Error,
                    "Требуется itemId или itemName",
                    code: "npc_equipment_missing_item_link",
                    section: "NPCInventory",
                    expected: "itemId or itemName",
                    actual: "missing both itemId and itemName",
                    repairHint: "Для NPCEquipmentChanges укажи itemId или itemName, чтобы клиент мог однозначно разрешить предмет экипировки."));

            if (string.Equals(action, "equip", StringComparison.OrdinalIgnoreCase))
                RequireArrayOfStringsProperty(item, itemContext, issues, "targetSlots");
            else if (string.Equals(action, "unequip", StringComparison.OrdinalIgnoreCase))
                RequireArrayOfStringsProperty(item, itemContext, issues, "sourceSlots");
            else if (!string.IsNullOrWhiteSpace(action))
                issues.Add(new ValidationIssue(
                    $"{itemContext}.action",
                    IssueSeverity.Error,
                    "action должен быть 'equip' или 'unequip'",
                    code: "npc_equipment_invalid_action",
                    section: "NPCInventory",
                    expected: "equip or unequip",
                    actual: action,
                    repairHint: "Для NPCEquipmentChanges используй только action = equip или action = unequip."));
        }
    }

    private void ValidateNpcInventoryResources(JsonElement root, string contextPrefix, List<ValidationIssue> issues)
    {
        if (!TryGetArray(root, "NPCInventoryResourcesChanges", $"{contextPrefix}.NPCInventoryResourcesChanges", issues, out var arr))
            return;

        var index = 0;
        foreach (var item in arr.EnumerateArray())
        {
            var itemContext = $"{contextPrefix}.NPCInventoryResourcesChanges[{index++}]";
            if (!RequireObject(item, itemContext, issues)) continue;
            RequireNpcIdentity(item, itemContext, issues);
            RequireAnyString(item, itemContext, issues, "NPCName", "npcName", "name");
            RequireString(item, itemContext, issues, "itemId");
            RequireString(item, itemContext, issues, "itemName");
            ValidateIntegerField(item, itemContext, issues, "newResourceValue");
        }
    }

    private void ValidateNpcGoalUpdates(JsonElement root, string contextPrefix, List<ValidationIssue> issues)
    {
        if (!TryGetArray(root, "NPCGoalUpdates", $"{contextPrefix}.NPCGoalUpdates", issues, out var arr))
            return;

        var index = 0;
        foreach (var item in arr.EnumerateArray())
        {
            var itemContext = $"{contextPrefix}.NPCGoalUpdates[{index++}]";
            if (!RequireObject(item, itemContext, issues)) continue;
            RequireNpcIdentity(item, itemContext, issues);

            if (!HasAnyNonEmptyString(item, "newShortTermGoal", "newLongTermGoal", "newPlan"))
            {
                issues.Add(new ValidationIssue(
                    itemContext,
                    IssueSeverity.Error,
                    "NPCGoalUpdates должен содержать хотя бы одно реально изменяемое поле цели",
                    code: "npc_goal_update_missing_changes",
                    section: "NPC",
                    expected: "at least one of newShortTermGoal, newLongTermGoal, newPlan",
                    actual: "no goal change fields",
                    repairHint: "Передай хотя бы одно непустое поле из newShortTermGoal, newLongTermGoal или newPlan. Не отправляй identity-only NPCGoalUpdates без изменения целей."));
            }
        }
    }

    private void ValidateNpcQuestUpdates(JsonElement root, string contextPrefix, List<ValidationIssue> issues)
    {
        if (!TryGetArray(root, "NPCQuestUpdates", $"{contextPrefix}.NPCQuestUpdates", issues, out var arr))
            return;

        var index = 0;
        foreach (var item in arr.EnumerateArray())
        {
            var itemContext = $"{contextPrefix}.NPCQuestUpdates[{index++}]";
            if (!RequireObject(item, itemContext, issues)) continue;
            RequireNpcIdentity(item, itemContext, issues);

            var hasAddOrUpdate = item.TryGetProperty("questsToAddOrUpdate", out var addOrUpdate);
            var hasRemove = item.TryGetProperty("questsToRemove", out var remove);
            if (!hasAddOrUpdate && !hasRemove)
            {
                issues.Add(new ValidationIssue(
                    itemContext,
                    IssueSeverity.Error,
                    "NPCQuestUpdates должен содержать хотя бы одну команду изменения personal quests",
                    code: "npc_quest_update_missing_commands",
                    section: "NPC",
                    expected: "questsToAddOrUpdate and/or questsToRemove",
                    actual: "no quest command arrays",
                    repairHint: "Передай хотя бы один из массивов questsToAddOrUpdate или questsToRemove. Не отправляй identity-only NPCQuestUpdates без фактических изменений."));
                continue;
            }

            if (hasAddOrUpdate)
            {
                RequireArrayOfObjects(addOrUpdate, $"{itemContext}.questsToAddOrUpdate", issues);
                if (addOrUpdate.ValueKind == JsonValueKind.Array)
                {
                    var questIndex = 0;
                    foreach (var quest in addOrUpdate.EnumerateArray())
                    {
                        var questContext = $"{itemContext}.questsToAddOrUpdate[{questIndex++}]";
                        if (!RequireObject(quest, questContext, issues))
                            continue;

                        ValidateNpcPersonalQuestObject(quest, questContext, issues);
                    }
                }
            }
            if (hasRemove)
                RequireArrayOfStrings(remove, $"{itemContext}.questsToRemove", issues);
        }
    }

    private void ValidateNpcRelationshipLockUpdates(JsonElement root, string contextPrefix, List<ValidationIssue> issues)
    {
        if (!TryGetArray(root, "NPCRelationshipLockUpdates", $"{contextPrefix}.NPCRelationshipLockUpdates", issues, out var arr))
            return;

        var index = 0;
        foreach (var item in arr.EnumerateArray())
        {
            var itemContext = $"{contextPrefix}.NPCRelationshipLockUpdates[{index++}]";
            if (!RequireObject(item, itemContext, issues)) continue;
            RequireNpcIdentity(item, itemContext, issues);

            if (item.TryGetProperty("lockUpdate", out var lockUpdate) && lockUpdate.ValueKind == JsonValueKind.Object)
            {
                if (!HasAnyNonEmptyString(lockUpdate, "newBreakthroughQuestId") &&
                    !lockUpdate.TryGetProperty("newIsLocked", out _) &&
                    !lockUpdate.TryGetProperty("newCurrentCap", out _))
                {
                    issues.Add(new ValidationIssue(
                        $"{itemContext}.lockUpdate",
                        IssueSeverity.Error,
                        "NPCRelationshipLockUpdates.lockUpdate должен содержать хотя бы одно реально изменяемое поле",
                        code: "npc_relationship_lock_update_missing_changes",
                        section: "NPC",
                        expected: "at least one of newBreakthroughQuestId, newIsLocked, newCurrentCap",
                        actual: "empty lockUpdate payload",
                        repairHint: "Передай в lockUpdate хотя бы одно поле из newBreakthroughQuestId, newIsLocked или newCurrentCap. Не отправляй пустой lockUpdate object."));
                }
            }
            else
            {
                issues.Add(new ValidationIssue(
                    $"{itemContext}.lockUpdate",
                    IssueSeverity.Error,
                    "NPCRelationshipLockUpdates должен содержать обязательный объект lockUpdate",
                    code: "npc_relationship_lock_update_missing_payload",
                    section: "NPC",
                    expected: "lockUpdate object",
                    actual: "missing or non-object",
                    repairHint: "Добавь в NPCRelationshipLockUpdates объект lockUpdate с хотя бы одним изменяемым полем: newBreakthroughQuestId, newIsLocked или newCurrentCap."));
            }
        }
    }

    private void ValidateNpcIdentityArray(JsonElement root, string contextPrefix, List<ValidationIssue> issues,
        string propName, string requiredPayloadField)
    {
        if (!TryGetArray(root, propName, $"{contextPrefix}.{propName}", issues, out var arr))
            return;

        var index = 0;
        foreach (var item in arr.EnumerateArray())
        {
            var itemContext = $"{contextPrefix}.{propName}[{index++}]";
            if (!RequireObject(item, itemContext, issues)) continue;
            RequireNpcIdentity(item, itemContext, issues);

            if (!item.TryGetProperty(requiredPayloadField, out _))
            {
                issues.Add(new ValidationIssue(
                    $"{itemContext}.{requiredPayloadField}",
                    IssueSeverity.Error,
                    $"Отсутствует обязательное поле: {requiredPayloadField}",
                    code: "npc_identity_command_missing_payload_field",
                    section: "NPC",
                    expected: requiredPayloadField,
                    actual: "missing property",
                    repairHint: $"Добавь в {propName} обязательное поле {requiredPayloadField} рядом с NPC identity, чтобы команда несла реально изменяемый payload."));
            }
        }
    }

    private void ValidateNpcActivityUpdates(JsonElement root, string contextPrefix, List<ValidationIssue> issues)
    {
        if (!TryGetArray(root, "NPCActivityUpdates", $"{contextPrefix}.NPCActivityUpdates", issues, out var arr))
            return;

        var index = 0;
        foreach (var item in arr.EnumerateArray())
        {
            var itemContext = $"{contextPrefix}.NPCActivityUpdates[{index++}]";
            if (!RequireObject(item, itemContext, issues))
                continue;

            RequireNpcIdentity(item, itemContext, issues);
            if (!item.TryGetProperty("activityUpdate", out var activityUpdate) ||
                !RequireObject(activityUpdate, $"{itemContext}.activityUpdate", issues))
            {
                issues.Add(new ValidationIssue(
                    $"{itemContext}.activityUpdate",
                    IssueSeverity.Error,
                    "NPCActivityUpdates должен содержать activityUpdate object",
                    code: "npc_activity_update_missing_payload",
                    section: "NPCActivities",
                    expected: "activityUpdate object with changed currentActivity fields",
                    actual: item.TryGetProperty("activityUpdate", out var actualPayload) ? actualPayload.ValueKind.ToString() : "missing",
                    repairHint: "Передавай в NPCActivityUpdates object activityUpdate с реально изменившимися non-terminal полями currentActivity. Для завершения активности используй completeNPCActivities."));
                continue;
            }

            ValidatePartialNpcActivityUpdateObject(activityUpdate, $"{itemContext}.activityUpdate", issues);
        }
    }

    private void ValidatePartialNpcActivityUpdateObject(JsonElement value, string context, List<ValidationIssue> issues)
    {
        if (!RequireObject(value, context, issues))
            return;

        if (!value.EnumerateObject().Any())
        {
            issues.Add(new ValidationIssue(
                context,
                IssueSeverity.Error,
                "NPCActivityUpdates.activityUpdate не должен быть пустым",
                code: "npc_activity_update_empty_payload",
                section: "NPCActivities",
                expected: "at least one changed currentActivity field",
                actual: "empty object",
                repairHint: "Передавай в activityUpdate только реально изменившиеся поля currentActivity. Не отправляй пустой no-op update."));
            return;
        }

        if (value.TryGetProperty("activityName", out _))
            ValidateOptionalString(value, context, issues, "activityName");
        if (value.TryGetProperty("description", out _))
            ValidateOptionalString(value, context, issues, "description");
        if (value.TryGetProperty("totalTimeCostMinutes", out _))
            ValidateIntegerField(value, context, issues, "totalTimeCostMinutes");
        if (value.TryGetProperty("timeSpentMinutes", out _))
            ValidateIntegerField(value, context, issues, "timeSpentMinutes");
        if (value.TryGetProperty("currentStepNumber", out _))
            ValidateIntegerField(value, context, issues, "currentStepNumber");
        if (value.TryGetProperty("totalStepsInActivity", out _))
            ValidateIntegerField(value, context, issues, "totalStepsInActivity");
        if (value.TryGetProperty("linkedQuestId", out _))
            ValidateOptionalNullableStringField(value, context, issues, "linkedQuestId");
        if (value.TryGetProperty("linkedPlotOutlineNode", out _))
            ValidateOptionalNullableStringField(value, context, issues, "linkedPlotOutlineNode");

        if (value.TryGetProperty("activeState", out _))
        {
            var activeState = RequireString(value, context, issues, "activeState");
            if (string.Equals(activeState, "Completed", StringComparison.OrdinalIgnoreCase) ||
                string.Equals(activeState, "Abandoned", StringComparison.OrdinalIgnoreCase))
            {
                issues.Add(new ValidationIssue(
                    $"{context}.activeState",
                    IssueSeverity.Error,
                    "NPCActivityUpdates не должен завершать активность через activityUpdate.activeState",
                    code: "npc_activity_update_terminal_state_forbidden",
                    section: "NPCActivities",
                    expected: "non-terminal activityUpdate fields or completeNPCActivities command",
                    actual: activeState,
                repairHint: "Если активность NPC завершена или abandoned, не ставь terminal activeState внутри NPCActivityUpdates. Используй completeNPCActivities с finalState = Completed или Abandoned."));
            }
        }
    }

    private void ValidatePartialThreatActivityUpdateObject(JsonElement value, string context, List<ValidationIssue> issues)
    {
        if (!RequireObject(value, context, issues))
            return;

        if (!value.EnumerateObject().Any())
        {
            issues.Add(new ValidationIssue(
                context,
                IssueSeverity.Error,
                "threatsToUpdate.currentActivity не должен быть пустым",
                code: "world_map_threat_update_empty_current_activity",
                section: "WorldMap",
                expected: "at least one changed currentActivity field",
                actual: "empty object",
                repairHint: "Передавай в threatsToUpdate.currentActivity только реально изменившиеся non-terminal поля. Не отправляй пустой no-op patch."));
            return;
        }

        if (value.TryGetProperty("activityName", out _))
            ValidateOptionalString(value, context, issues, "activityName");
        if (value.TryGetProperty("description", out _))
            ValidateOptionalString(value, context, issues, "description");
        if (value.TryGetProperty("totalTimeCostMinutes", out _))
            ValidateIntegerField(value, context, issues, "totalTimeCostMinutes");
        if (value.TryGetProperty("timeSpentMinutes", out _))
            ValidateIntegerField(value, context, issues, "timeSpentMinutes");
        if (value.TryGetProperty("currentStepNumber", out _))
            ValidateIntegerField(value, context, issues, "currentStepNumber");
        if (value.TryGetProperty("totalStepsInActivity", out _))
            ValidateIntegerField(value, context, issues, "totalStepsInActivity");
        if (value.TryGetProperty("linkedQuestId", out _))
            ValidateOptionalNullableStringField(value, context, issues, "linkedQuestId");
        if (value.TryGetProperty("linkedPlotOutlineNode", out _))
            ValidateOptionalNullableStringField(value, context, issues, "linkedPlotOutlineNode");
    }

    private void ValidateCompleteNpcActivities(JsonElement root, string contextPrefix, List<ValidationIssue> issues)
    {
        if (!TryGetArray(root, "completeNPCActivities", $"{contextPrefix}.completeNPCActivities", issues, out var arr))
            return;

        var index = 0;
        foreach (var item in arr.EnumerateArray())
        {
            var itemContext = $"{contextPrefix}.completeNPCActivities[{index++}]";
            if (!RequireObject(item, itemContext, issues))
                continue;

            RequireNpcIdentity(item, itemContext, issues);
            RequireString(item, itemContext, issues, "activityName");
            var finalState = RequireString(item, itemContext, issues, "finalState");
            RequireString(item, itemContext, issues, "narrativeSummary");
            if (!string.IsNullOrWhiteSpace(finalState) &&
                !string.Equals(finalState, "Completed", StringComparison.OrdinalIgnoreCase) &&
                !string.Equals(finalState, "Abandoned", StringComparison.OrdinalIgnoreCase))
            {
                issues.Add(new ValidationIssue(
                    $"{itemContext}.finalState",
                    IssueSeverity.Error,
                    "completeNPCActivities.finalState должен быть Completed или Abandoned",
                    code: "npc_complete_activity_invalid_final_state",
                    section: "NPCActivities",
                    expected: "Completed | Abandoned",
                    actual: finalState,
                    repairHint: "Для completeNPCActivities используй только finalState = Completed или Abandoned."));
            }
        }
    }

    private void ValidateNpcRelationshipChanges(JsonElement root, string contextPrefix, List<ValidationIssue> issues)
    {
        if (!TryGetArray(root, "NPCRelationshipChanges", $"{contextPrefix}.NPCRelationshipChanges", issues, out var arr))
            return;

        var index = 0;
        foreach (var item in arr.EnumerateArray())
        {
            var itemContext = $"{contextPrefix}.NPCRelationshipChanges[{index++}]";
            if (!RequireObject(item, itemContext, issues))
                continue;

            RequireNpcIdentity(item, itemContext, issues);
            ValidateIntegerField(item, itemContext, issues, "newRelationshipLevel");
            RequireString(item, itemContext, issues, "changeReason");
            if (TryReadInt(item, "newRelationshipLevel", out var relationshipLevel) &&
                (relationshipLevel < -400 || relationshipLevel > 400))
            {
                issues.Add(new ValidationIssue(
                    $"{itemContext}.newRelationshipLevel",
                    IssueSeverity.Error,
                    "NPCRelationshipChanges.newRelationshipLevel должен быть в диапазоне -400..400",
                    code: "npc_relationship_level_out_of_bounds",
                    section: "NPCRelationships",
                    expected: "-400..400",
                    actual: relationshipLevel.ToString(),
                    repairHint: "Сохрани newRelationshipLevel в canonical диапазоне -400..400 и при необходимости синхронизируй его с attitude по NPC relationship rules."));
            }
        }
    }

    private void ValidateNpcFateCardUnlocks(JsonElement root, string contextPrefix, List<ValidationIssue> issues)
    {
        if (!TryGetArray(root, "NPCFateCardUnlocks", $"{contextPrefix}.NPCFateCardUnlocks", issues, out var arr))
            return;

        var index = 0;
        foreach (var item in arr.EnumerateArray())
        {
            var itemContext = $"{contextPrefix}.NPCFateCardUnlocks[{index++}]";
            if (!RequireObject(item, itemContext, issues))
                continue;

            RequireNpcIdentity(item, itemContext, issues);
            RequireString(item, itemContext, issues, "cardId");
            RequireString(item, itemContext, issues, "cardName");
        }
    }

    private void ValidateInterNpcRelationshipChanges(JsonElement root, string contextPrefix, List<ValidationIssue> issues)
    {
        if (!TryGetArray(root, "interNPCRelationshipChanges", $"{contextPrefix}.interNPCRelationshipChanges", issues, out var arr))
            return;

        var index = 0;
        foreach (var item in arr.EnumerateArray())
        {
            var itemContext = $"{contextPrefix}.interNPCRelationshipChanges[{index++}]";
            if (!RequireObject(item, itemContext, issues)) continue;

            if (!HasAnyNonEmptyString(item, "sourceNpcId", "sourceNpcName", "sourceName"))
                issues.Add(new ValidationIssue(
                    itemContext,
                    IssueSeverity.Error,
                    "interNPCRelationshipChanges требует sourceNpcId или sourceNpcName",
                    code: "inter_npc_relationship_missing_source",
                    section: "NPCRelationships",
                    expected: "sourceNpcId or sourceNpcName/sourceName",
                    actual: "missing",
                    repairHint: "Передай в interNPCRelationshipChanges permanent sourceNpcId или, если ID ещё недоступен, canonical sourceNpcName/sourceName."));

            if (!HasAnyNonEmptyString(item, "targetNpcId", "initialTargetNpcId", "targetNpcName"))
                issues.Add(new ValidationIssue(
                    itemContext,
                    IssueSeverity.Error,
                    "interNPCRelationshipChanges требует targetNpcId, initialTargetNpcId или targetNpcName",
                    code: "inter_npc_relationship_missing_target",
                    section: "NPCRelationships",
                    expected: "targetNpcId or initialTargetNpcId or targetNpcName",
                    actual: "missing",
                    repairHint: "Передай permanent targetNpcId, same-turn initialTargetNpcId или canonical targetNpcName для целевого NPC."));

            if (!HasNonEmptyString(item, "newRelationshipStatus"))
                issues.Add(new ValidationIssue(
                    $"{itemContext}.newRelationshipStatus",
                    IssueSeverity.Error,
                    "interNPCRelationshipChanges требует newRelationshipStatus",
                    code: "inter_npc_relationship_missing_status",
                    section: "NPCRelationships",
                    expected: string.Join(" | ", AllowedInterNpcRelationshipStatuses),
                    actual: "missing",
                    repairHint: "Передай canonical newRelationshipStatus для новой связи между NPC."));
            else
            {
                var status = GetFirstNonEmptyString(item, "newRelationshipStatus");
                if (!string.IsNullOrWhiteSpace(status) && !AllowedInterNpcRelationshipStatuses.Contains(status))
                {
                    issues.Add(new ValidationIssue(
                        $"{itemContext}.newRelationshipStatus",
                        IssueSeverity.Error,
                        "newRelationshipStatus должен быть одним из canonical inter-NPC relationship enum значений",
                        code: "inter_npc_relationship_invalid_status",
                        section: "NPCRelationships",
                        expected: string.Join(" | ", AllowedInterNpcRelationshipStatuses),
                        actual: status,
                        repairHint: "Используй в interNPCRelationshipChanges только Ally, Friend, Neutral, Rival, Enemy, Subordinate, Superior или Family."));
                }
            }

            if (!HasNonEmptyString(item, "newStatusReason"))
                issues.Add(new ValidationIssue(
                    $"{itemContext}.newStatusReason",
                    IssueSeverity.Error,
                    "interNPCRelationshipChanges требует newStatusReason",
                    code: "inter_npc_relationship_missing_reason",
                    section: "NPCRelationships",
                    expected: "non-empty newStatusReason",
                    actual: "missing",
                    repairHint: "Добавь newStatusReason с кратким объяснением, почему отношение между NPC изменилось."));

            if (item.TryGetProperty("relationshipType", out _))
            {
                issues.Add(new ValidationIssue(
                    $"{itemContext}.relationshipType",
                    IssueSeverity.Error,
                    "relationshipType не входит в canonical interNPCRelationshipChanges contract",
                    code: "inter_npc_relationship_legacy_alias_forbidden",
                    section: "NPCRelationships",
                    repairHint: "Используй newRelationshipStatus + newStatusReason и не передавай legacy alias relationshipType."));
            }
        }
    }

    private void ValidateNpcIdentityOnlyArray(JsonElement root, string contextPrefix, List<ValidationIssue> issues,
        string propName)
    {
        if (!TryGetArray(root, propName, $"{contextPrefix}.{propName}", issues, out var arr))
            return;

        var index = 0;
        foreach (var item in arr.EnumerateArray())
        {
            var itemContext = $"{contextPrefix}.{propName}[{index++}]";
            if (!RequireObject(item, itemContext, issues)) continue;
            RequireNpcIdentity(item, itemContext, issues);
        }
    }

    private void ValidateNpcCustomStateChanges(JsonElement root, string contextPrefix, List<ValidationIssue> issues)
    {
        if (!TryGetArray(root, "NPCCustomStateChanges", $"{contextPrefix}.NPCCustomStateChanges", issues, out var arr))
            return;

        var index = 0;
        foreach (var item in arr.EnumerateArray())
        {
            var itemContext = $"{contextPrefix}.NPCCustomStateChanges[{index++}]";
            if (!RequireObject(item, itemContext, issues))
                continue;

            RequireNpcIdentity(item, itemContext, issues);
            if (!item.TryGetProperty("stateChanges", out var stateChanges))
            {
                issues.Add(new ValidationIssue(
                    $"{itemContext}.stateChanges",
                    IssueSeverity.Error,
                    "NPCCustomStateChanges item должен содержать stateChanges",
                    code: "npc_custom_state_changes_missing_state_changes",
                    section: "NPCCustomState",
                    expected: "stateChanges[] with complete Custom State Objects",
                    actual: "missing",
                    repairHint: "Для NPCCustomStateChanges передай stateChanges[] с одним или несколькими полными Custom State Objects по Block 25.A."));
                continue;
            }

            ValidateCustomStatesContainer(stateChanges, $"{itemContext}.stateChanges", issues);
        }
    }

    private void ValidateNpcMaskChanges(JsonElement root, string contextPrefix, List<ValidationIssue> issues, string propName, string payloadField)
    {
        if (!TryGetArray(root, propName, $"{contextPrefix}.{propName}", issues, out var arr))
            return;

        var index = 0;
        foreach (var item in arr.EnumerateArray())
        {
            var itemContext = $"{contextPrefix}.{propName}[{index++}]";
            if (!RequireObject(item, itemContext, issues))
                continue;

            RequireNpcIdentity(item, itemContext, issues);
            if (!item.TryGetProperty(payloadField, out var mask))
            {
                issues.Add(new ValidationIssue(
                    $"{itemContext}.{payloadField}",
                    IssueSeverity.Error,
                    $"Отсутствует обязательное поле: {payloadField}",
                    code: "npc_mask_change_missing_payload",
                    section: "NPC",
                    expected: "Mask Object payload",
                    actual: "missing",
                    repairHint: "Для NPCMaskAdds/NPCMaskUpdates передай полный Mask Object с maskId/maskName/personalityArchetype/attitude/behavioralDirectives."));
                continue;
            }

            ValidateNpcMaskObject(mask, $"{itemContext}.{payloadField}", issues);
        }
    }

    private void ValidateWorldQuestCombatFactionContract(JsonElement root, string contextPrefix, List<ValidationIssue> issues)
    {
        if (root.TryGetProperty("timeChange", out var timeChange) &&
            timeChange.ValueKind != JsonValueKind.Null &&
            root.TryGetProperty("setWorldTime", out var setWorldTime) &&
            setWorldTime.ValueKind != JsonValueKind.Null)
        {
            issues.Add(new ValidationIssue(
                contextPrefix,
                IssueSeverity.Error,
                "setWorldTime нельзя смешивать с incremental timeChange в одном accepted turn",
                code: "world_time_mutually_exclusive_commands",
                section: "WorldTime",
                expected: "either timeChange or setWorldTime",
                actual: "both commands are present",
                repairHint: "Используй либо incremental timeChange, либо absolute setWorldTime. Для большого time skip/rewind оставь только setWorldTime, потому что он override'ит обычный timeChange для этого хода."));
        }

        ValidateCurrentLocationData(root, contextPrefix, issues);
        ValidateWorldMapUpdates(root, contextPrefix, issues);
        ValidateArrayOfObjectsField(root, contextPrefix, issues, "worldEventsLog");
        ValidateRivalSoulArcArray(root, contextPrefix, issues, "UpdateRivalSoulArcs");
        ValidateRivalSoulArcArray(root, contextPrefix, issues, "arcs");
        ValidateWorldStateFlagsArray(root, contextPrefix, issues, "worldStateFlags");
        ValidateArrayOfStringsField(root, contextPrefix, issues, "removeWorldStateFlags");
        ValidateTimeChangeField(root, contextPrefix, issues, "timeChange");
        ValidateWorldTimeObject(root, contextPrefix, issues, "setWorldTime");
        ValidateDirectWorldTimeState(root, contextPrefix, issues);
        ValidateWeatherObject(root, contextPrefix, issues, "weatherChange");
        ValidateDirectWeatherState(root, contextPrefix, issues);
        ValidateObjectOrArrayOfObjectsField(root, contextPrefix, issues, "updateWorldProgressionTracker");
        ValidateObjectOrArrayOfObjectsField(root, contextPrefix, issues, "updateFactionProgressionTracker");
        ValidateProgressionProcessingReport(root, contextPrefix, issues);

        ValidateQuestArray(root, contextPrefix, issues, "UpdateQuests");
        ValidateQuestArray(root, contextPrefix, issues, "UpdateSoulQuests");
        ValidateQuestArray(root, contextPrefix, issues, "quests");
        ValidateQuestHistoryData(root, contextPrefix, issues);
        ValidateQuestLog(root, contextPrefix, issues);
        ValidatePlotOutline(root, contextPrefix, issues);

        ValidateCombatantArray(root, contextPrefix, issues, "enemiesData");
        ValidateCombatantArray(root, contextPrefix, issues, "alliesData");
        ValidateCombatLog(root, contextPrefix, issues);

        ValidateFactionArray(root, contextPrefix, issues, "factionDataChanges");
        ValidateFactionArray(root, contextPrefix, issues, "factions");
        ValidateFactionArray(root, contextPrefix, issues, "factionRankChanges");
        ValidateFactionArray(root, contextPrefix, issues, "factionBonusChanges");
        ValidateFactionArray(root, contextPrefix, issues, "factionResourceChanges");
        ValidateFactionArray(root, contextPrefix, issues, "factionProjectUpdates");
        ValidateFactionArray(root, contextPrefix, issues, "completeFactionProjects");
        ValidateFactionArray(root, contextPrefix, issues, "factionCustomStateChanges");
        ValidateFactionChronicles(root, contextPrefix, issues);
    }

    private void ValidateProgressionProcessingReport(JsonElement root, string contextPrefix, List<ValidationIssue> issues)
    {
        if (!root.TryGetProperty("progressionProcessingReport", out var report))
            return;

        ValidateProgressionReportObject(report, $"{contextPrefix}.progressionProcessingReport", issues);
    }

    private void ValidateProgressionScheduleStateFile(JsonElement root, string contextPrefix, List<ValidationIssue> issues)
    {
        if (!RequireObject(root, contextPrefix, issues))
            return;

        RequireString(root, contextPrefix, issues, "currentRealm");
        ValidateNonNegativeIntegerField(root, contextPrefix, issues, "currentWorldTimeInMinutes", "ProgressionSchedule");
        ValidateNonNegativeIntegerField(root, contextPrefix, issues, "lastWorldSimulationTimeInMinutes", "ProgressionSchedule");
        ValidateNonNegativeIntegerField(root, contextPrefix, issues, "lastFactionSimulationTimeInMinutes", "ProgressionSchedule");
        ValidatePositiveIntegerField(root, contextPrefix, issues, "worldCycleMinutes");
        ValidatePositiveIntegerField(root, contextPrefix, issues, "factionCycleMinutes");
        ValidateNonNegativeIntegerField(root, contextPrefix, issues, "pendingWorldCycles", "ProgressionSchedule");
        ValidateNonNegativeIntegerField(root, contextPrefix, issues, "pendingFactionCycles", "ProgressionSchedule");
        ValidateNonNegativeIntegerField(root, contextPrefix, issues, "currentChaosSeaTurnOrdinal", "ProgressionSchedule");
        ValidateNonNegativeIntegerField(root, contextPrefix, issues, "lastChaosSeaSimulationOrdinal", "ProgressionSchedule");
        ValidateNonNegativeIntegerField(root, contextPrefix, issues, "lastGuardianProjectCycleOrdinal", "ProgressionSchedule");
        ValidateNonNegativeIntegerField(root, contextPrefix, issues, "lastResidentAgencyCycleOrdinal", "ProgressionSchedule");
        ValidateNonNegativeIntegerField(root, contextPrefix, issues, "lastShiningAbodeCycleOrdinal", "ProgressionSchedule");
        ValidateNonNegativeIntegerField(root, contextPrefix, issues, "lastShiningFactionCycleOrdinal", "ProgressionSchedule");
        ValidateNonNegativeIntegerField(root, contextPrefix, issues, "lastShiningTradeCycleOrdinal", "ProgressionSchedule");
        ValidateNonNegativeIntegerField(root, contextPrefix, issues, "pendingChaosSeaCycles", "ProgressionSchedule");
        ValidateNonNegativeIntegerField(root, contextPrefix, issues, "pendingGuardianProjectCycles", "ProgressionSchedule");
        ValidateNonNegativeIntegerField(root, contextPrefix, issues, "pendingResidentAgencyCycles", "ProgressionSchedule");
        ValidateNonNegativeIntegerField(root, contextPrefix, issues, "pendingShiningAbodeCycles", "ProgressionSchedule");
        ValidateNonNegativeIntegerField(root, contextPrefix, issues, "pendingShiningFactionCycles", "ProgressionSchedule");
        ValidateNonNegativeIntegerField(root, contextPrefix, issues, "pendingShiningTradeCycles", "ProgressionSchedule");
        ValidatePositiveIntegerField(root, contextPrefix, issues, "chaosSeaCycleEquivalentHours");
        ValidatePositiveIntegerField(root, contextPrefix, issues, "afterlifeCatchupCycleEquivalentMinutes");
        ValidateNonNegativeIntegerField(root, contextPrefix, issues, "lastAfterlifeCatchupWorldTimeInMinutes", "ProgressionSchedule");
        RequireBooleanField(root, contextPrefix, issues, "hasAfterlifeCatchupWorldTimeBaseline");
    }

    private void ValidateProgressionReportStateFile(JsonElement root, string contextPrefix, List<ValidationIssue> issues)
    {
        if (root.TryGetProperty("progressionProcessingReport", out var wrapped))
        {
            ValidateProgressionReportObject(wrapped, $"{contextPrefix}.progressionProcessingReport", issues);
            return;
        }

        ValidateProgressionReportObject(root, contextPrefix, issues);
    }

    private void ValidatePendingInkActionsStateFile(JsonElement root, string contextPrefix, List<ValidationIssue> issues)
    {
        if (!TryGetArray(root, "pendingActions", $"{contextPrefix}.pendingActions", issues, out var arr))
            return;

        var index = 0;
        foreach (var item in arr.EnumerateArray())
        {
            var itemContext = $"{contextPrefix}.pendingActions[{index++}]";
            if (!RequireObject(item, itemContext, issues))
                continue;

            var missingPendingActionFields = GetMissingRequiredNonEmptyStringProperties(item, "actionId", "actionTag", "status", "createdAtUtc");
            if (missingPendingActionFields.Count > 0)
            {
                issues.Add(new ValidationIssue(
                    itemContext,
                    IssueSeverity.Error,
                    "pending action не содержит обязательные корневые поля",
                    code: "pending_ink_action_missing_required_fields",
                    section: "Ink",
                    expected: "Non-empty actionId, actionTag, status, createdAtUtc",
                    actual: string.Join(", ", missingPendingActionFields),
                    repairHint: "Сначала собери корневой pending action contract, потом добавляй costInFeathers и SEAL_IN_INK-specific поля."));
                continue;
            }

            var actionTag = GetFirstNonEmptyString(item, "actionTag") ?? string.Empty;
            var status = GetFirstNonEmptyString(item, "status") ?? string.Empty;
            ValidatePositiveNumberField(item, itemContext, issues, "costInFeathers");

            if (!string.IsNullOrWhiteSpace(actionTag) &&
                !string.Equals(actionTag, "SEAL_IN_INK", StringComparison.OrdinalIgnoreCase))
            {
                issues.Add(new ValidationIssue(
                    $"{itemContext}.actionTag",
                    IssueSeverity.Error,
                    "pending_ink_actions.json currently supports only SEAL_IN_INK",
                    code: "pending_ink_action_unsupported_tag",
                    section: "Ink",
                    expected: "SEAL_IN_INK",
                    actual: actionTag,
                    repairHint: "Для deferred Ink Feather state используй только actionTag=SEAL_IN_INK, как описано в CLI Ink contract."));
            }

            if (!string.IsNullOrWhiteSpace(status) &&
                !string.Equals(status, "awaiting-item-choice", StringComparison.OrdinalIgnoreCase))
            {
                issues.Add(new ValidationIssue(
                    $"{itemContext}.status",
                    IssueSeverity.Error,
                    "pending ink action status должен быть awaiting-item-choice",
                    code: "pending_ink_action_invalid_status",
                    section: "Ink",
                    expected: "awaiting-item-choice",
                    actual: status,
                    repairHint: "Для deferred SEAL_IN_INK сохраняй status=awaiting-item-choice до следующего хода выбора предмета."));
            }

            if (string.Equals(actionTag, "SEAL_IN_INK", StringComparison.OrdinalIgnoreCase))
            {
                if (!item.TryGetProperty("upgradeTierDelta", out var upgradeTierDelta) ||
                    upgradeTierDelta.ValueKind != JsonValueKind.Number ||
                    !upgradeTierDelta.TryGetInt32(out var parsedUpgradeTierDelta) ||
                    parsedUpgradeTierDelta <= 0)
                {
                    ValidatePositiveNumberField(item, itemContext, issues, "upgradeTierDelta");
                }
                else if (parsedUpgradeTierDelta != 1)
                {
                    issues.Add(new ValidationIssue(
                        $"{itemContext}.upgradeTierDelta",
                        IssueSeverity.Error,
                        "SEAL_IN_INK pending action должен повышать качество ровно на 1 tier",
                        code: "pending_ink_action_invalid_upgrade_tier_delta",
                        section: "Ink",
                        expected: "upgradeTierDelta = 1",
                        actual: parsedUpgradeTierDelta.ToString(),
                        repairHint: "Для deferred SEAL_IN_INK сохраняй canonical upgradeTierDelta = 1; следующий ход выбора предмета применяет ровно одно повышение качества."));
                }
            }
        }
    }

    private void ValidateProgressionReportObject(JsonElement report, string contextPrefix, List<ValidationIssue> issues)
    {
        if (!RequireObject(report, contextPrefix, issues))
            return;

        RequireString(report, contextPrefix, issues, "sessionId");
        RequireString(report, contextPrefix, issues, "requestId");
        ValidatePositiveIntegerField(report, contextPrefix, issues, "turnNumber");
        ValidateNonNegativeIntegerField(report, contextPrefix, issues, "worldCyclesProcessed", "ProgressionReport");
        ValidateNonNegativeIntegerField(report, contextPrefix, issues, "factionCyclesProcessed", "ProgressionReport");
        ValidateNonNegativeIntegerField(report, contextPrefix, issues, "chaosSeaCyclesProcessed", "ProgressionReport");
        ValidateNonNegativeIntegerField(report, contextPrefix, issues, "guardianProjectCyclesProcessed", "ProgressionReport");
        ValidateNonNegativeIntegerField(report, contextPrefix, issues, "residentAgencyCyclesProcessed", "ProgressionReport");
        ValidateNonNegativeIntegerField(report, contextPrefix, issues, "shiningAbodeCyclesProcessed", "ProgressionReport");
        ValidateNonNegativeIntegerField(report, contextPrefix, issues, "shiningFactionCyclesProcessed", "ProgressionReport");
        ValidateNonNegativeIntegerField(report, contextPrefix, issues, "shiningTradeCyclesProcessed", "ProgressionReport");
        ValidateNonNegativeIntegerField(report, contextPrefix, issues, "newLastWorldSimulationTimeInMinutes", "ProgressionReport");
        ValidateNonNegativeIntegerField(report, contextPrefix, issues, "newLastFactionSimulationTimeInMinutes", "ProgressionReport");
        ValidateNonNegativeIntegerField(report, contextPrefix, issues, "newLastChaosSeaSimulationOrdinal", "ProgressionReport");
        ValidateNonNegativeIntegerField(report, contextPrefix, issues, "newLastGuardianProjectCycleOrdinal", "ProgressionReport");
        ValidateNonNegativeIntegerField(report, contextPrefix, issues, "newLastResidentAgencyCycleOrdinal", "ProgressionReport");
        ValidateNonNegativeIntegerField(report, contextPrefix, issues, "newLastShiningAbodeCycleOrdinal", "ProgressionReport");
        ValidateNonNegativeIntegerField(report, contextPrefix, issues, "newLastShiningFactionCycleOrdinal", "ProgressionReport");
        ValidateNonNegativeIntegerField(report, contextPrefix, issues, "newLastShiningTradeCycleOrdinal", "ProgressionReport");
        if (report.TryGetProperty("afterlifeCatchupProcessed", out var afterlifeCatchupProcessed) &&
            afterlifeCatchupProcessed.ValueKind != JsonValueKind.True &&
            afterlifeCatchupProcessed.ValueKind != JsonValueKind.False)
        {
            issues.Add(new ValidationIssue(
                $"{contextPrefix}.afterlifeCatchupProcessed",
                IssueSeverity.Error,
                "afterlifeCatchupProcessed должен быть boolean",
                code: "progression_report_afterlife_catchup_processed_not_boolean",
                section: "ProgressionReport",
                expected: "boolean",
                actual: afterlifeCatchupProcessed.ValueKind.ToString(),
                repairHint: "Сохраняй afterlifeCatchupProcessed как true/false, не строку или число."));
        }
        ValidateNonNegativeIntegerField(report, contextPrefix, issues, "afterlifeCatchupSummaryEventsProcessed", "ProgressionReport");
    }

    private void ValidateMetaMiscContract(JsonElement root, string contextPrefix, List<ValidationIssue> issues)
    {
        ValidateSoulStateRealmContract(root, contextPrefix, issues);
        ValidateMetaStateUpdates(root, contextPrefix, issues);
        ValidateAfterlifeArchiveData(root, contextPrefix, issues);
        ValidatePendingMemoryLegacy(root, contextPrefix, issues);
        ValidatePendingShiningBlessingEffects(root, contextPrefix, issues);
        ValidateAfterlifeCombatProfile(root, contextPrefix, issues);
        ValidateAfterlifeSpiritualConflictUpdateContract(root, contextPrefix, issues);
        ValidatePlayerGuardianFoundationSoulStateFields(root, contextPrefix, issues);
        ValidateGuardianCommands(root, contextPrefix, issues);
        ValidateGuardianStateData(root, contextPrefix, issues);
        ValidateGuardianPowerEventData(root, contextPrefix, issues);
        ValidateGuardianPowerJournalData(root, contextPrefix, issues);
        ValidateGuardianProjectStateData(root, contextPrefix, issues);
        ValidateGuardianProjectJournalData(root, contextPrefix, issues);
        ValidatePlayerBehavior(root, contextPrefix, issues);
        ValidateCharacterChronicle(root, contextPrefix, issues);
        ValidateAchievementData(root, contextPrefix, issues);
        ValidateCodexData(root, contextPrefix, issues);
        ValidateVehicleData(root, contextPrefix, issues);
        ValidateStorageAccessData(root, contextPrefix, issues);
        ValidateOtherPlayersInteractions(root, contextPrefix, issues);
        ValidateDialogueOptionsData(root, contextPrefix, issues);
        ValidateMultipliersData(root, contextPrefix, issues);
        ValidateOptionalString(root, contextPrefix, issues, "image_prompt");
    }

    private void ValidateSoulStateRealmContract(JsonElement root, string contextPrefix, List<ValidationIssue> issues)
    {
        if (!contextPrefix.EndsWith("game_state/meta/soul_state.json", StringComparison.OrdinalIgnoreCase))
            return;

        if (root.TryGetProperty("currentRealm", out var currentRealm) &&
            currentRealm.ValueKind == JsonValueKind.String &&
            !string.IsNullOrWhiteSpace(currentRealm.GetString()))
        {
            return;
        }

        issues.Add(new ValidationIssue(
            $"{contextPrefix}.currentRealm",
            IssueSeverity.Error,
            "soul_state.currentRealm должен быть явно задан; пустой/null realm не является Chaos Sea и не может использовать afterlife fallback.",
            code: "soul_state_unresolved_current_realm",
            section: "Realm",
            expected: "non-empty currentRealm such as Chaos Sea, Shining Abode, or a Mortal World realm",
            actual: root.TryGetProperty("currentRealm", out var actualValue) ? actualValue.ValueKind.ToString() : "missing",
            repairHint: "Восстанови authoritative soul_state.currentRealm перед обработкой хода. Не выводи realm из pending files, prompts или старого scheduler state."));
    }

    private void ValidatePlayerGuardianFoundationSoulStateFields(JsonElement root, string contextPrefix, List<ValidationIssue> issues)
    {
        if (root.TryGetProperty(PlayerGuardianFoundationState.SoulStateGuardianIdProperty, out var foundedGuardianId))
        {
            if (foundedGuardianId.ValueKind != JsonValueKind.String ||
                string.IsNullOrWhiteSpace(foundedGuardianId.GetString()))
            {
                issues.Add(new ValidationIssue(
                    $"{contextPrefix}.{PlayerGuardianFoundationState.SoulStateGuardianIdProperty}",
                    IssueSeverity.Error,
                    "playerFoundedGuardianId должен быть непустой строкой",
                    code: "player_guardian_foundation_invalid_soul_link",
                    section: "PlayerGuardianFoundation",
                    expected: "non-empty guardianId string",
                    actual: foundedGuardianId.ValueKind.ToString()));
            }
        }

        if (root.TryGetProperty(PlayerGuardianFoundationState.SoulStateFoundationStatusProperty, out var foundationStatus))
        {
            if (foundationStatus.ValueKind != JsonValueKind.String ||
                string.IsNullOrWhiteSpace(foundationStatus.GetString()) ||
                !string.Equals(foundationStatus.GetString(), PlayerGuardianFoundationState.SoulStateFoundationStatusFounded, StringComparison.OrdinalIgnoreCase))
            {
                issues.Add(new ValidationIssue(
                    $"{contextPrefix}.{PlayerGuardianFoundationState.SoulStateFoundationStatusProperty}",
                    IssueSeverity.Error,
                    "playerGuardianFoundationStatus поддерживает только canonical значение founded",
                    code: "player_guardian_foundation_invalid_soul_status",
                    section: "PlayerGuardianFoundation",
                    expected: PlayerGuardianFoundationState.SoulStateFoundationStatusFounded,
                    actual: foundationStatus.ValueKind == JsonValueKind.String ? foundationStatus.GetString() ?? "empty" : foundationStatus.ValueKind.ToString()));
            }
        }
    }

    private void ValidateDialogueOptionsData(JsonElement root, string contextPrefix, List<ValidationIssue> issues)
    {
        if (!root.TryGetProperty("dialogueOptions", out var dialogueOptions))
            return;

        var context = $"{contextPrefix}.dialogueOptions";
        if (dialogueOptions.ValueKind == JsonValueKind.Null)
            return;

        if (dialogueOptions.ValueKind != JsonValueKind.Array)
        {
            issues.Add(new ValidationIssue(
                context,
                IssueSeverity.Error,
                "dialogueOptions должен быть массивом объектов или null"));
            return;
        }

        var index = 0;
        foreach (var option in dialogueOptions.EnumerateArray())
        {
            var itemContext = $"{context}[{index++}]";
            if (!RequireObject(option, itemContext, issues))
                continue;

            RequireString(option, itemContext, issues, "text");
            ValidateOptionalNullableStringField(option, itemContext, issues, "category");
        }
    }

    private void ValidateMultipliersData(JsonElement root, string contextPrefix, List<ValidationIssue> issues)
    {
        if (!root.TryGetProperty("multipliers", out var multipliers))
        {
            if (contextPrefix.EndsWith("game_state/misc/multipliers.json", StringComparison.OrdinalIgnoreCase))
            {
                issues.Add(new ValidationIssue(
                    $"{contextPrefix}.multipliers",
                    IssueSeverity.Error,
                    "Canonical multipliers file не содержит обязательный top-level ключ multipliers",
                    code: "multipliers_missing_top_level_key",
                    section: "Multipliers",
                    repairHint: "Сохраняй game_state/misc/multipliers.json как объект с обязательным массивом multipliers."));
            }
            return;
        }

        var context = $"{contextPrefix}.multipliers";
        if (multipliers.ValueKind != JsonValueKind.Array)
        {
            issues.Add(new ValidationIssue(
                context,
                IssueSeverity.Error,
                "multipliers должен быть массивом из пяти числовых коэффициентов",
                code: "multipliers_not_array",
                section: "Multipliers",
                expected: "array[5] of numbers",
                actual: multipliers.ValueKind.ToString(),
                repairHint: "Сохрани multipliers как массив ровно из пяти числовых коэффициентов в canonical порядке из Block 24."));
            return;
        }

        if (multipliers.GetArrayLength() != 5)
        {
            issues.Add(new ValidationIssue(
                context,
                IssueSeverity.Error,
                "multipliers должен содержать ровно пять коэффициентов",
                code: "multipliers_invalid_length",
                section: "Multipliers",
                expected: "exactly 5 numbers",
                actual: multipliers.GetArrayLength().ToString(),
                repairHint: "Передай multipliers как массив из пяти чисел в canonical порядке buy, sell, xp, stealth, encounter."));
        }

        var index = 0;
        foreach (var coefficient in multipliers.EnumerateArray())
        {
            if (coefficient.ValueKind != JsonValueKind.Number)
            {
                issues.Add(new ValidationIssue(
                    $"{context}[{index}]",
                    IssueSeverity.Error,
                    "Каждый multiplier coefficient должен быть числом",
                    code: "multipliers_non_numeric_value",
                    section: "Multipliers"));
            }
            index++;
        }
    }

    private void ValidateWorldStateFlagsArray(JsonElement root, string contextPrefix, List<ValidationIssue> issues, string propName)
    {
        if (!root.TryGetProperty(propName, out var arr))
            return;

        var context = $"{contextPrefix}.{propName}";
        RequireArrayOfObjects(arr, context, issues);
        if (arr.ValueKind != JsonValueKind.Array)
            return;

        var index = 0;
        foreach (var item in arr.EnumerateArray())
        {
            var itemContext = $"{context}[{index++}]";
            if (!RequireObject(item, itemContext, issues))
                continue;

            var flagId = RequireString(item, itemContext, issues, "flagId");
            RequireString(item, itemContext, issues, "displayName");
            RequireString(item, itemContext, issues, "description");

            if (!string.IsNullOrWhiteSpace(flagId) && !SnakeCaseIdRegex.IsMatch(flagId))
            {
                issues.Add(new ValidationIssue(
                    $"{itemContext}.flagId",
                    IssueSeverity.Error,
                    "worldStateFlags.flagId должен использовать English snake_case",
                    code: "world_state_flag_invalid_flag_id",
                    section: "WorldStateFlags",
                    expected: "english_snake_case flagId",
                    actual: flagId,
                    repairHint: "Используй для worldStateFlags.flagId стабильный English snake_case идентификатор вроде city_in_lockdown."));
            }

            if (!item.TryGetProperty("value", out var value) ||
                value.ValueKind is not (JsonValueKind.True or JsonValueKind.False or JsonValueKind.Number or JsonValueKind.String) ||
                (value.ValueKind == JsonValueKind.Number && !value.TryGetInt32(out _)))
            {
                issues.Add(new ValidationIssue(
                    $"{itemContext}.value",
                    IssueSeverity.Error,
                    "worldStateFlags.value должен быть boolean, integer или string",
                    code: "world_state_flag_invalid_value_type",
                    section: "WorldStateFlags",
                    expected: "boolean | integer | string",
                    actual: item.TryGetProperty("value", out var actualValue) ? actualValue.ValueKind.ToString() : "missing",
                    repairHint: "Передавай flag value только как boolean, integer или string по Block 21.5 contract."));
            }
        }
    }

    private void ValidateItemJournalUpdateCommands(JsonElement root, string contextPrefix, List<ValidationIssue> issues)
    {
        if (!root.TryGetProperty("itemJournalUpdates", out var journalUpdates))
            return;

        var wrapper = new Dictionary<string, JsonElement>(StringComparer.OrdinalIgnoreCase)
        {
            ["itemJournalUpdates"] = journalUpdates
        };
        using var doc = JsonDocument.Parse(JsonSerializer.Serialize(wrapper));
        ValidateItemJournalStateFile(doc.RootElement, contextPrefix, issues);
    }

    private void ValidateMetaStateUpdates(JsonElement root, string contextPrefix, List<ValidationIssue> issues)
    {
        if (!root.TryGetProperty("metaStateUpdates", out var metaState))
            return;

        var context = $"{contextPrefix}.metaStateUpdates";
        if (metaState.ValueKind == JsonValueKind.Null)
            return;

        if (!RequireObject(metaState, context, issues))
            return;

        foreach (var property in metaState.EnumerateObject())
        {
            if (property.Name.StartsWith("_", StringComparison.OrdinalIgnoreCase))
                continue;

            if (GuardianPolicyContracts.MetaStateVisibleTopLevelCommandKeys.Contains(property.Name))
                continue;

            issues.Add(new ValidationIssue(
                $"{context}.{property.Name}",
                IssueSeverity.Error,
                "metaStateUpdates содержит unsupported visible key",
                code: "meta_state_unknown_top_level_update_key",
                section: "Lifecycle",
                expected: $"visible keys limited to {string.Join("/", GuardianPolicyContracts.MetaStateVisibleTopLevelCommandKeys.OrderBy(key => key, StringComparer.OrdinalIgnoreCase))}",
                actual: property.Name,
                repairHint: "Используй в metaStateUpdates только canonical visible subcommands inkFeatherChanges/enlightenmentProgression/soulRelicOperations/lifeTransitions/memoryLegacyGrant."));
        }

        if (metaState.TryGetProperty("inkFeatherChanges", out var feathers) && !RequireObject(feathers, $"{context}.inkFeatherChanges", issues))
            return;
        if (metaState.TryGetProperty("enlightenmentProgression", out var enlightenment) && !RequireObject(enlightenment, $"{context}.enlightenmentProgression", issues))
            return;
        if (metaState.TryGetProperty("soulRelicOperations", out var relicOps) && !RequireObject(relicOps, $"{context}.soulRelicOperations", issues))
            return;
        if (metaState.TryGetProperty("lifeTransitions", out var lifeTransitions) && !RequireObject(lifeTransitions, $"{context}.lifeTransitions", issues))
            return;
        if (metaState.TryGetProperty("memoryLegacyGrant", out var memoryLegacyGrant) && !RequireObject(memoryLegacyGrant, $"{context}.memoryLegacyGrant", issues))
            return;

        if (metaState.TryGetProperty("lifeTransitions", out var transitions) && transitions.ValueKind == JsonValueKind.Object)
            ValidateMetaLifeTransitionsObject(transitions, $"{context}.lifeTransitions", issues);
        if (metaState.TryGetProperty("memoryLegacyGrant", out var grant) && grant.ValueKind == JsonValueKind.Object)
            ValidateMemoryLegacyGrantObject(grant, $"{context}.memoryLegacyGrant", issues);
        if (metaState.TryGetProperty("inkFeatherChanges", out var validatedFeathers) && validatedFeathers.ValueKind == JsonValueKind.Object)
            ValidateMetaInkFeatherChangesObject(validatedFeathers, $"{context}.inkFeatherChanges", issues);
        if (metaState.TryGetProperty("enlightenmentProgression", out var progression) && progression.ValueKind == JsonValueKind.Object)
            ValidateMetaEnlightenmentProgressionObject(progression, $"{context}.enlightenmentProgression", issues);
        if (metaState.TryGetProperty("soulRelicOperations", out var validatedRelicOps) && validatedRelicOps.ValueKind == JsonValueKind.Object)
            ValidateMetaSoulRelicOperationsObject(validatedRelicOps, $"{context}.soulRelicOperations", issues);
    }

    private void ValidateMetaInkFeatherChangesObject(JsonElement feathers, string context, List<ValidationIssue> issues)
    {
        foreach (var property in feathers.EnumerateObject())
        {
            if (property.Name.StartsWith("_", StringComparison.OrdinalIgnoreCase))
                continue;

            if (!string.Equals(property.Name, "add", StringComparison.Ordinal) &&
                !string.Equals(property.Name, "spend", StringComparison.Ordinal))
            {
                issues.Add(new ValidationIssue(
                    $"{context}.{property.Name}",
                    IssueSeverity.Error,
                    "inkFeatherChanges содержит unsupported visible key",
                    code: "meta_state_unknown_ink_feather_change_key",
                    section: "Lifecycle",
                    expected: "visible keys limited to add/spend",
                    actual: property.Name,
                    repairHint: "Используй в metaStateUpdates.inkFeatherChanges только integer buckets add и spend."));
                continue;
            }

            if (property.Value.ValueKind != JsonValueKind.Number ||
                !property.Value.TryGetInt32(out var amount) ||
                amount < 0)
            {
                issues.Add(new ValidationIssue(
                    $"{context}.{property.Name}",
                    IssueSeverity.Error,
                    "inkFeatherChanges bucket должен быть non-negative integer",
                    code: "meta_state_invalid_ink_feather_change_value",
                    section: "Lifecycle",
                    expected: "non-negative integer",
                    actual: property.Value.ValueKind == JsonValueKind.Number ? property.Value.GetRawText() : property.Value.ValueKind.ToString(),
                    repairHint: "Передавай inkFeatherChanges.add/spend только как non-negative integer deltas."));
            }
        }
    }

    private void ValidateMetaEnlightenmentProgressionObject(JsonElement progression, string context, List<ValidationIssue> issues)
    {
        foreach (var property in progression.EnumerateObject())
        {
            if (property.Name.StartsWith("_", StringComparison.OrdinalIgnoreCase))
                continue;

            if (!string.Equals(property.Name, "newTier", StringComparison.Ordinal) &&
                !string.Equals(property.Name, "experience", StringComparison.Ordinal))
            {
                issues.Add(new ValidationIssue(
                    $"{context}.{property.Name}",
                    IssueSeverity.Error,
                    "enlightenmentProgression содержит unsupported visible key",
                    code: "meta_state_unknown_enlightenment_progression_key",
                    section: "Lifecycle",
                    expected: "visible keys limited to newTier/experience",
                    actual: property.Name,
                    repairHint: "Используй в metaStateUpdates.enlightenmentProgression только canonical keys newTier и experience."));
                continue;
            }

            if (property.Value.ValueKind != JsonValueKind.Number ||
                !property.Value.TryGetInt32(out var amount) ||
                amount < 0)
            {
                issues.Add(new ValidationIssue(
                    $"{context}.{property.Name}",
                    IssueSeverity.Error,
                    "enlightenmentProgression value должен быть non-negative integer",
                    code: "meta_state_invalid_enlightenment_progression_value",
                    section: "Lifecycle",
                    expected: "non-negative integer",
                    actual: property.Value.ValueKind == JsonValueKind.Number ? property.Value.GetRawText() : property.Value.ValueKind.ToString(),
                    repairHint: "Передавай metaStateUpdates.enlightenmentProgression.newTier/experience только как non-negative integer values."));
            }
        }

        if (!progression.TryGetProperty("experience", out _))
        {
            issues.Add(new ValidationIssue(
                $"{context}.experience",
                IssueSeverity.Error,
                "enlightenmentProgression должен содержать experience",
                code: "meta_state_missing_enlightenment_progression_experience",
                section: "Lifecycle",
                expected: "experience non-negative integer",
                actual: "missing",
                repairHint: "Сохраняй в metaStateUpdates.enlightenmentProgression canonical experience даже если прирост tier не меняется."));
        }
    }

    private void ValidateMetaSoulRelicOperationsObject(JsonElement soulRelicOperations, string context, List<ValidationIssue> issues)
    {
        foreach (var property in soulRelicOperations.EnumerateObject())
        {
            if (property.Name.StartsWith("_", StringComparison.OrdinalIgnoreCase))
                continue;

            switch (property.Name)
            {
                case "addRelic":
                case "removeRelic":
                case "equipRelic":
                case "unequipRelic":
                    if (!RequireObject(property.Value, $"{context}.{property.Name}", issues))
                        continue;

                    RequireString(property.Value, $"{context}.{property.Name}", issues, "relicId");
                    break;
                case "updateRelicField":
                    if (!RequireObject(property.Value, $"{context}.{property.Name}", issues))
                        continue;

                    RequireString(property.Value, $"{context}.updateRelicField", issues, "relicId");
                    RequireString(property.Value, $"{context}.updateRelicField", issues, "field");
                    break;
                default:
                    issues.Add(new ValidationIssue(
                        $"{context}.{property.Name}",
                        IssueSeverity.Error,
                        "soulRelicOperations содержит unsupported visible key",
                        code: "meta_state_unknown_soul_relic_operation_key",
                        section: "Lifecycle",
                        expected: "visible keys limited to addRelic/removeRelic/equipRelic/unequipRelic/updateRelicField",
                        actual: property.Name,
                        repairHint: "Используй в metaStateUpdates.soulRelicOperations только canonical relic ops."));
                    break;
            }
        }
    }

}

