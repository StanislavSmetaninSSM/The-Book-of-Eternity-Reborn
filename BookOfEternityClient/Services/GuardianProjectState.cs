using System.Text.Json;
using System.Text.Json.Nodes;
using BookOfEternityClient.Core;

namespace BookOfEternityClient.Services;

internal readonly record struct GuardianProjectSoulContextRequirements(
    bool RequiresCurrentIncarnation,
    bool RequiresCurrentRealm)
{
    public bool RequiresReadableCurrentSoulState => RequiresCurrentIncarnation || RequiresCurrentRealm;

    public GuardianProjectSoulContextRequirements Merge(GuardianProjectSoulContextRequirements other) =>
        new(
            RequiresCurrentIncarnation || other.RequiresCurrentIncarnation,
            RequiresCurrentRealm || other.RequiresCurrentRealm);
}

internal static class GuardianProjectState
{
    public const string TrackerPath = "game_state/meta/guardian_projects.json";
    public const string JournalPath = "game_state/meta/guardian_project_journal.json";
    public const string LoreResearchHookOrigin = "lore_research_hook";
    public const string LoreResearchSpecialLineOrigin = "lore_research_special_line";
    public const string ArchiveConsultationHookOrigin = "archive_consultation_hook";

    public static readonly string[] AllowedProjectTiers = { "minor", "major", "grand" };
    public static readonly string[] AllowedProjectModes = { "internal", "supportive", "offensive" };
    public static readonly string[] AllowedFinalStates = { "Completed", "Abandoned", "Sabotaged", "Collapsed" };
    public static readonly string[] AllowedPoliticalProjectTypes =
    {
        "abode_fortification",
        "offensive_intrigue",
        "counter_rival_operation"
    };

    internal sealed record DerivedProjectEffects(
        int UpgradedTradeSlots,
        int ElevatedTradeSlots,
        int GuardianRarityCeilingBonusSteps,
        int BonusLoreUnlocks,
        int QuestHookCount,
        int GuaranteedArchiveQuestCount,
        int SpecialQuestLineUnlocks,
        int VisibleRivalClueBonus,
        int ArchiveWarningTierBonus,
        int PreparationBudgetPoints,
        int PreparationClaimPriorityBonus,
        int HostilePriorityTokensGranted,
        int FortificationSafePressureBonus,
        int FortificationDefenseRatingBonus);

    internal sealed record ResolvedGuardianDerivedState(
        int CurrentPower,
        string TierLabel,
        string TierColor,
        int TradeSlotCount,
        int GuardianQuestCap,
        string GuardianQuestDifficultyCeiling,
        int BonusGachaCharges,
        int BaseGuardianRarityCeilingBonusSteps,
        int EffectiveGuardianRarityCeilingBonusSteps,
        int BaseNextLifeCorrectionBudgetPoints,
        int EffectiveNextLifeCorrectionBudgetPoints,
        int BaseRivalArcDefenseClues,
        int EffectiveRivalArcDefenseClues,
        int RivalArcClarityTier,
        bool RivalArcCounterQuestAccess,
        int RivalArcWarningTier,
        string RivalArcOffenseCap,
        int EffectiveUpgradedTradeSlots,
        int EffectiveElevatedTradeSlots,
        int FortificationSafePressureBonus,
        int FortificationDefenseRatingBonus,
        int ActiveTemporaryModifierCount,
        DerivedProjectEffects ProjectEffects);

    public sealed record PoliticalShieldBreakdown(
        int BaseTargetShield,
        int FortificationBonus,
        int CounterOperationBonus,
        int PlayerDefenseBonus,
        int TargetShield);

    public sealed record OffensiveImpactResult(
        int BaseLoss,
        int AttackerBonus,
        int BaseTargetShield,
        int FortificationBonus,
        int CounterOperationBonus,
        int PlayerDefenseBonus,
        int TargetShield,
        int TargetLoss,
        int PressureDelta,
        int StabilityDamage);

    public sealed record TemporaryModifierSnapshot(
        string ModifierId,
        string ModifierType,
        int Value,
        int RemainingApplications);

    public sealed record GuardianDerivedDiagnosticSnapshot(
        string GuardianId,
        ResolvedGuardianDerivedState DerivedState,
        IReadOnlyList<TemporaryModifierSnapshot> ActiveTemporaryModifiers);

    public static string BuildKey(string guardianId, string projectId) => $"{guardianId}::{projectId}";

    public static string GetProjectId(JsonObject entry)
    {
        if (entry["project"] is JsonObject project)
            return GetNodeString(project["projectId"]);

        return GetNodeString(entry["projectId"]);
    }

    public static string GetProjectId(JsonElement entry)
    {
        if (entry.TryGetProperty("project", out var project) && project.ValueKind == JsonValueKind.Object)
            return GetString(project, "projectId");

        return GetString(entry, "projectId");
    }

    public static string GetGuardianId(JsonObject entry) => GetNodeString(entry["guardianId"]);

    public static string GetGuardianId(JsonElement entry) => GetString(entry, "guardianId");

    public static bool IsValidProjectTier(string? value) =>
        AllowedProjectTiers.Contains((value ?? "").Trim(), StringComparer.OrdinalIgnoreCase);

    public static bool IsValidProjectMode(string? value) =>
        AllowedProjectModes.Contains((value ?? "").Trim(), StringComparer.OrdinalIgnoreCase);

    public static bool IsValidFinalState(string? value) =>
        AllowedFinalStates.Contains((value ?? "").Trim(), StringComparer.OrdinalIgnoreCase);

    public static int GetTierWeight(string? value) =>
        (value ?? "").Trim().ToLowerInvariant() switch
        {
            "grand" => 3,
            "major" => 2,
            _ => 1
        };

    public static int GetFortificationBonus(string? projectTier) => GetTierWeight(projectTier);

    public static int GetCounterOperationBonus(string? projectTier) => GetTierWeight(projectTier);

    public static int GetOffensiveProjectBonus(string? projectTier) => GetTierWeight(projectTier);

    public static int GetBaseLossByTier(string? projectTier) =>
        (projectTier ?? "").Trim().ToLowerInvariant() switch
        {
            "grand" => 8,
            "major" => 5,
            _ => 2
        };

    public static int GetDefaultTerminalAbodePowerDelta(string? projectType, string? finalState, string? projectTier)
    {
        var normalizedType = (projectType ?? string.Empty).Trim().ToLowerInvariant();
        var normalizedState = (finalState ?? string.Empty).Trim().ToLowerInvariant();
        var tierWeight = GetTierWeight(projectTier);

        return (normalizedType, normalizedState) switch
        {
            ("offensive_intrigue", "completed") => tierWeight switch { 3 => 12, 2 => 8, _ => 4 },
            ("offensive_intrigue", "abandoned") => tierWeight switch { 3 => -3, 2 => -2, _ => -1 },
            ("offensive_intrigue", "collapsed") => tierWeight switch { 3 => -7, 2 => -5, _ => -3 },
            ("offensive_intrigue", "sabotaged") => -tierWeight,
            ("counter_rival_operation", "completed") => tierWeight,
            ("abode_fortification", "collapsed") => tierWeight switch { 3 => -5, 2 => -3, _ => -2 },
            _ => 0
        };
    }

    public static int GetPoliticalPressureByTier(string? projectTier) =>
        (projectTier ?? "").Trim().ToLowerInvariant() switch
        {
            "grand" => 24,
            "major" => 16,
            _ => 8
        };

    public static int GetPoliticalBaseHitDamageByTier(string? projectTier) =>
        (projectTier ?? "").Trim().ToLowerInvariant() switch
        {
            "grand" => 9,
            "major" => 6,
            _ => 3
        };

    public static int GetCounterOperationPressureRelief(string? projectTier) =>
        (projectTier ?? "").Trim().ToLowerInvariant() switch
        {
            "grand" => 26,
            "major" => 18,
            _ => 10
        };

    public static int GetCounterOperationStabilityRelief(string? projectTier) =>
        (projectTier ?? "").Trim().ToLowerInvariant() switch
        {
            "grand" => 12,
            "major" => 8,
            _ => 4
        };

    public static int GetCounterOperationAbodePowerGain(string? projectTier) => GetTierWeight(projectTier);

    public static int GetBaseTargetShield(int currentPower) => Math.Clamp(currentPower / 30, 0, 3);

    public static int GetAttackerBonus(int attackerCurrentPower) => Math.Clamp(attackerCurrentPower / 25, 0, 4);

    public static JsonObject EnsureRecipeEffectState(JsonObject project, int currentIncarnation, string? currentRealm)
    {
        var effectState = project["effectState"] as JsonObject;
        if (effectState != null)
            return effectState;

        effectState = BuildDefaultEffectState(project, currentIncarnation, currentRealm);
        project["effectState"] = effectState;
        return effectState;
    }

    public static bool ExpireLifeBoundEffects(JsonObject? trackerRoot, int lifeIncarnation)
    {
        if (trackerRoot == null || trackerRoot["completedProjects"] is not JsonArray completedProjects)
            return false;

        var changed = false;
        foreach (var entry in completedProjects.OfType<JsonObject>())
        {
            if (entry["project"] is not JsonObject project)
                continue;

            var projectType = GetNodeString(project["projectType"]);
            if (!string.Equals(projectType, "lore_research", StringComparison.OrdinalIgnoreCase) &&
                !string.Equals(projectType, "soul_preparation", StringComparison.OrdinalIgnoreCase))
            {
                continue;
            }

            var effectState = EnsureRecipeEffectState(project, lifeIncarnation, null);
            var targetIncarnation = GetNodeInt(effectState["targetIncarnation"]);
            if (targetIncarnation <= 0 || targetIncarnation >= lifeIncarnation)
                continue;

            changed |= ExpireEffectState(effectState, lifeIncarnation);
        }

        return changed;
    }

    public static bool ConsumeSoulPreparationForLife(JsonObject? trackerRoot, int lifeIncarnation)
    {
        if (trackerRoot == null || trackerRoot["completedProjects"] is not JsonArray completedProjects)
            return false;

        var changed = false;
        foreach (var entry in completedProjects.OfType<JsonObject>())
        {
            if (entry["project"] is not JsonObject project)
                continue;

            if (!string.Equals(GetNodeString(project["projectType"]), "soul_preparation", StringComparison.OrdinalIgnoreCase))
                continue;

            var effectState = EnsureRecipeEffectState(project, lifeIncarnation, null);
            if (GetNodeInt(effectState["targetIncarnation"]) != lifeIncarnation ||
                GetNodeBool(effectState["consumedAtLifeStart"]))
            {
                continue;
            }

            var budgetGranted = GetNodeInt(effectState["preparationBudgetPointsGranted"]);
            var hostileGranted = GetNodeInt(effectState["hostilePriorityTokensGranted"]);
            effectState["preparationBudgetPointsSpent"] = budgetGranted;
            effectState["hostilePriorityTokensSpent"] = hostileGranted;
            effectState["consumedAtLifeStart"] = true;
            effectState["consumedAtLifeIncarnation"] = lifeIncarnation;
            changed = true;
        }

        return changed;
    }

    public static bool TryConsumeRelicForgingTradeRefresh(JsonObject? trackerRoot, string guardianId)
    {
        if (trackerRoot == null || trackerRoot["completedProjects"] is not JsonArray completedProjects)
            return false;

        foreach (var entry in GetCompletedProjectsForGuardian(completedProjects, guardianId, "relic_forging"))
        {
            var project = (JsonObject)entry["project"]!;
            var effectState = EnsureRecipeEffectState(project, 0, null);
            if (GetTradeRefreshUsesRemaining(effectState) <= 0)
                continue;

            effectState["tradeRefreshUsesSpent"] = GetNodeInt(effectState["tradeRefreshUsesSpent"]) + 1;
            return true;
        }

        return false;
    }

    public static bool TryConsumeRelicForgingGachaUse(JsonObject? trackerRoot, string guardianId, string? sourceProjectId)
    {
        if (trackerRoot == null || trackerRoot["completedProjects"] is not JsonArray completedProjects)
            return false;

        foreach (var entry in GetCompletedProjectsForGuardian(completedProjects, guardianId, "relic_forging"))
        {
            if (entry["project"] is not JsonObject project)
                continue;

            var projectId = GetNodeString(project["projectId"]);
            if (!string.IsNullOrWhiteSpace(sourceProjectId) &&
                !string.Equals(projectId, sourceProjectId, StringComparison.OrdinalIgnoreCase))
            {
                continue;
            }

            var effectState = EnsureRecipeEffectState(project, 0, null);
            if (GetGachaUsesRemaining(effectState) <= 0)
                continue;

            effectState["gachaUsesSpent"] = GetNodeInt(effectState["gachaUsesSpent"]) + 1;
            return true;
        }

        return false;
    }

    public static bool TryConsumeRelicForgingGachaUse(IReadOnlyList<JsonObject> completedProjects, string guardianId, string? sourceProjectId)
    {
        foreach (var entry in completedProjects)
        {
            if (!string.Equals(GetGuardianId(entry), guardianId, StringComparison.OrdinalIgnoreCase) ||
                entry["project"] is not JsonObject project ||
                !string.Equals(GetNodeString(project["projectType"]), "relic_forging", StringComparison.OrdinalIgnoreCase) ||
                !string.Equals(GetNodeString(project["finalState"]), "Completed", StringComparison.OrdinalIgnoreCase))
            {
                continue;
            }

            var projectId = GetNodeString(project["projectId"]);
            if (!string.IsNullOrWhiteSpace(sourceProjectId) &&
                !string.Equals(projectId, sourceProjectId, StringComparison.OrdinalIgnoreCase))
            {
                continue;
            }

            var effectState = EnsureRecipeEffectState(project, 0, null);
            if (GetGachaUsesRemaining(effectState) <= 0)
                continue;

            effectState["gachaUsesSpent"] = GetNodeInt(effectState["gachaUsesSpent"]) + 1;
            return true;
        }

        return false;
    }

    public static bool TryConsumeLoreQuestToken(
        JsonObject? trackerRoot,
        string guardianId,
        string sourceProjectId,
        string questOrigin,
        int currentIncarnation)
    {
        if (trackerRoot == null || trackerRoot["completedProjects"] is not JsonArray completedProjects)
            return false;

        foreach (var entry in GetCompletedProjectsForGuardian(completedProjects, guardianId, "lore_research"))
        {
            if (entry["project"] is not JsonObject project ||
                !string.Equals(GetNodeString(project["projectId"]), sourceProjectId, StringComparison.OrdinalIgnoreCase))
            {
                continue;
            }

            var effectState = EnsureRecipeEffectState(project, currentIncarnation, null);
            if (GetNodeInt(effectState["targetIncarnation"]) != currentIncarnation)
                return false;

            if (string.Equals(questOrigin, LoreResearchSpecialLineOrigin, StringComparison.OrdinalIgnoreCase))
            {
                var remaining = GetSpecialQuestLineTokensRemaining(effectState);
                if (remaining <= 0)
                    return false;

                effectState["specialQuestLineTokensSpent"] = GetNodeInt(effectState["specialQuestLineTokensSpent"]) + 1;
                return true;
            }

            if (string.Equals(questOrigin, ArchiveConsultationHookOrigin, StringComparison.OrdinalIgnoreCase))
            {
                var guaranteedRemaining = GetGuaranteedArchiveQuestRemaining(effectState);
                if (guaranteedRemaining <= 0)
                    return false;

                effectState["guaranteedArchiveQuestSpawned"] = GetNodeInt(effectState["guaranteedArchiveQuestSpawned"]) + 1;
                effectState["guaranteedArchiveQuestConsumed"] = GetNodeInt(effectState["guaranteedArchiveQuestConsumed"]) + 1;
                return true;
            }

            if (!string.Equals(questOrigin, LoreResearchHookOrigin, StringComparison.OrdinalIgnoreCase))
                return false;

            var hookRemaining = GetQuestHookTokensRemaining(effectState);
            if (hookRemaining <= 0)
                return false;

            effectState["questHookTokensSpent"] = GetNodeInt(effectState["questHookTokensSpent"]) + 1;
            return true;
        }

        return false;
    }

    public static bool TryConsumeLoreQuestToken(
        IReadOnlyList<JsonObject> completedProjects,
        string guardianId,
        string sourceProjectId,
        string questOrigin,
        int currentIncarnation)
    {
        foreach (var entry in completedProjects)
        {
            if (!string.Equals(GetGuardianId(entry), guardianId, StringComparison.OrdinalIgnoreCase) ||
                entry["project"] is not JsonObject project ||
                !string.Equals(GetNodeString(project["projectType"]), "lore_research", StringComparison.OrdinalIgnoreCase) ||
                !string.Equals(GetNodeString(project["finalState"]), "Completed", StringComparison.OrdinalIgnoreCase) ||
                !string.Equals(GetNodeString(project["projectId"]), sourceProjectId, StringComparison.OrdinalIgnoreCase))
            {
                continue;
            }

            var effectState = EnsureRecipeEffectState(project, currentIncarnation, null);
            if (GetNodeInt(effectState["targetIncarnation"]) != currentIncarnation)
                return false;

            if (string.Equals(questOrigin, LoreResearchSpecialLineOrigin, StringComparison.OrdinalIgnoreCase))
            {
                if (GetSpecialQuestLineTokensRemaining(effectState) <= 0)
                    return false;

                effectState["specialQuestLineTokensSpent"] = GetNodeInt(effectState["specialQuestLineTokensSpent"]) + 1;
                return true;
            }

            if (string.Equals(questOrigin, ArchiveConsultationHookOrigin, StringComparison.OrdinalIgnoreCase))
            {
                if (GetGuaranteedArchiveQuestRemaining(effectState) <= 0)
                    return false;

                effectState["guaranteedArchiveQuestSpawned"] = GetNodeInt(effectState["guaranteedArchiveQuestSpawned"]) + 1;
                effectState["guaranteedArchiveQuestConsumed"] = GetNodeInt(effectState["guaranteedArchiveQuestConsumed"]) + 1;
                return true;
            }

            if (!string.Equals(questOrigin, LoreResearchHookOrigin, StringComparison.OrdinalIgnoreCase))
                return false;

            if (GetQuestHookTokensRemaining(effectState) <= 0)
                return false;

            effectState["questHookTokensSpent"] = GetNodeInt(effectState["questHookTokensSpent"]) + 1;
            return true;
        }

        return false;
    }

    internal static bool RequiresCurrentGuardianSideReconciliation(
        JsonObject? completedProjectEntry,
        int currentIncarnation,
        string? currentRealm)
    {
        if (completedProjectEntry?["project"] is not JsonObject project ||
            !string.Equals(GetNodeString(project["finalState"]), "Completed", StringComparison.OrdinalIgnoreCase))
        {
            return false;
        }

        var projectType = GetNodeString(project["projectType"]);
        var effectState = project["effectState"] as JsonObject;
        var audit = project["projectOutcomeAudit"] as JsonObject;
        var projectTier = GetNodeString(project["projectTier"]);

        if (string.Equals(projectType, "lore_research", StringComparison.OrdinalIgnoreCase))
        {
            var effectiveEffectState = effectState ?? BuildDefaultEffectState(project, currentIncarnation, currentRealm);
            if (GetNodeInt(effectiveEffectState["targetIncarnation"]) != currentIncarnation)
                return false;

            return GetQuestHookTokensRemaining(effectiveEffectState, audit, projectTier) > 0 ||
                   GetGuaranteedArchiveQuestRemaining(effectiveEffectState, audit, projectTier) > 0 ||
                   GetSpecialQuestLineTokensRemaining(effectiveEffectState, audit, projectTier) > 0;
        }

        if (string.Equals(projectType, "relic_forging", StringComparison.OrdinalIgnoreCase))
            return GetGachaUsesRemaining(effectState, audit, projectTier) > 0;

        return false;
    }

    internal static GuardianProjectSoulContextRequirements GetCurrentSoulContextRequirementsForNormalization(JsonObject? project)
    {
        var projectType = GetNodeString(project?["projectType"]);
        if (string.Equals(projectType, "lore_research", StringComparison.OrdinalIgnoreCase))
            return new GuardianProjectSoulContextRequirements(RequiresCurrentIncarnation: true, RequiresCurrentRealm: true);

        if (string.Equals(projectType, "soul_preparation", StringComparison.OrdinalIgnoreCase))
            return new GuardianProjectSoulContextRequirements(RequiresCurrentIncarnation: true, RequiresCurrentRealm: false);

        return default;
    }

    internal static GuardianProjectSoulContextRequirements GetCurrentSoulContextRequirementsForCompletedProjectNormalization(JsonObject? project)
    {
        var projectType = GetNodeString(project?["projectType"]);
        if (string.Equals(projectType, "soul_preparation", StringComparison.OrdinalIgnoreCase))
            return new GuardianProjectSoulContextRequirements(RequiresCurrentIncarnation: true, RequiresCurrentRealm: false);

        if (!string.Equals(projectType, "lore_research", StringComparison.OrdinalIgnoreCase))
            return default;

        if (project?["effectState"] is not JsonObject effectState)
            return GetCurrentSoulContextRequirementsForNormalization(project);

        var audit = project["projectOutcomeAudit"] as JsonObject;
        var projectTier = GetNodeString(project["projectTier"]);
        return GetQuestHookTokensRemaining(effectState, audit, projectTier) > 0 ||
               GetGuaranteedArchiveQuestRemaining(effectState, audit, projectTier) > 0 ||
               GetSpecialQuestLineTokensRemaining(effectState, audit, projectTier) > 0
            ? new GuardianProjectSoulContextRequirements(RequiresCurrentIncarnation: true, RequiresCurrentRealm: false)
            : default;
    }

    internal static bool RequiresCurrentSoulContextForNormalization(JsonObject? project)
    {
        return GetCurrentSoulContextRequirementsForNormalization(project).RequiresReadableCurrentSoulState;
    }

    public static DerivedProjectEffects ResolveDerivedEffects(JsonObject? trackerRoot, string guardianId)
    {
        if (trackerRoot == null || string.IsNullOrWhiteSpace(guardianId))
            return new DerivedProjectEffects(0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0);

        if (trackerRoot["completedProjects"] is not JsonArray completedProjects)
            return new DerivedProjectEffects(0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0);

        return ResolveDerivedEffectsInternal(
            completedProjects.OfType<JsonObject>()
                .Where(entry => string.Equals(GetGuardianId(entry), guardianId, StringComparison.OrdinalIgnoreCase))
                .Select(entry => entry["project"] as JsonObject)
                .Where(project => project != null)!
                .Cast<JsonObject>());
    }

    public static DerivedProjectEffects ResolveDerivedEffects(JsonElement trackerRoot, string guardianId)
    {
        if (trackerRoot.ValueKind != JsonValueKind.Object || string.IsNullOrWhiteSpace(guardianId))
            return new DerivedProjectEffects(0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0);

        if (!trackerRoot.TryGetProperty("completedProjects", out var completedProjects) || completedProjects.ValueKind != JsonValueKind.Array)
            return new DerivedProjectEffects(0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0);

        var converted = new List<JsonObject>();
        foreach (var entry in completedProjects.EnumerateArray())
        {
            if (entry.ValueKind != JsonValueKind.Object ||
                !string.Equals(GetGuardianId(entry), guardianId, StringComparison.OrdinalIgnoreCase) ||
                !entry.TryGetProperty("project", out var project) ||
                project.ValueKind != JsonValueKind.Object)
            {
                continue;
            }

            var node = JsonNode.Parse(project.GetRawText()) as JsonObject;
            if (node != null)
                converted.Add(node);
        }

        return ResolveDerivedEffectsInternal(converted);
    }

    public static ResolvedGuardianDerivedState ResolveGuardianDerivedState(JsonObject guardian, JsonObject? trackerRoot)
    {
        var currentPower = AbodePowerRules.GetCurrentPower(guardian);
        var guardianId = GetNodeString(guardian["guardianId"]);
        var effects = ResolveDerivedEffects(trackerRoot, guardianId);
        var activeTemporaryModifierCount = GetActiveTemporaryModifierCount(trackerRoot, guardianId);
        return ResolveGuardianDerivedStateInternal(currentPower, effects, activeTemporaryModifierCount);
    }

    public static ResolvedGuardianDerivedState ResolveGuardianDerivedState(JsonElement guardian, JsonElement trackerRoot)
    {
        var currentPower = AbodePowerRules.GetCurrentPower(guardian);
        var guardianId = GetFirstNonEmptyString(guardian, "guardianId");
        var effects = ResolveDerivedEffects(trackerRoot, guardianId ?? string.Empty);
        var activeTemporaryModifierCount = GetActiveTemporaryModifierCount(trackerRoot, guardianId ?? string.Empty);
        return ResolveGuardianDerivedStateInternal(currentPower, effects, activeTemporaryModifierCount);
    }

    public static ResolvedGuardianDerivedState ResolveGuardianDerivedState(JsonElement guardian)
    {
        var currentPower = AbodePowerRules.GetCurrentPower(guardian);
        return ResolveGuardianDerivedStateInternal(
            currentPower,
            new DerivedProjectEffects(0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0),
            0);
    }

    public static GuardianDerivedDiagnosticSnapshot BuildDiagnosticSnapshot(JsonObject guardian, JsonObject? trackerRoot)
    {
        var guardianId = GetNodeString(guardian["guardianId"]);
        var derivedState = ResolveGuardianDerivedState(guardian, trackerRoot);
        var modifiers = CollectActiveTemporaryModifiers(trackerRoot, guardianId);
        return new GuardianDerivedDiagnosticSnapshot(guardianId, derivedState, modifiers);
    }

    public static GuardianDerivedDiagnosticSnapshot BuildDiagnosticSnapshot(JsonElement guardian, JsonElement trackerRoot)
    {
        var guardianId = GetFirstNonEmptyString(guardian, "guardianId") ?? string.Empty;
        var derivedState = ResolveGuardianDerivedState(guardian, trackerRoot);
        var modifiers = CollectActiveTemporaryModifiers(trackerRoot, guardianId);
        return new GuardianDerivedDiagnosticSnapshot(guardianId, derivedState, modifiers);
    }

    public static int GetEffectiveNextLifeCorrectionBudgetPoints(int currentPower, DerivedProjectEffects effects) =>
        AbodePowerRules.GetNextLifeCorrectionBudgetPoints(currentPower) + effects.PreparationBudgetPoints;

    public static int GetEffectiveGuardianRarityCeilingBonusSteps(int currentPower, DerivedProjectEffects effects) =>
        AbodePowerRules.GetGuardianRarityCeilingBonusSteps(currentPower) + effects.GuardianRarityCeilingBonusSteps;

    public static int GetEffectiveRivalArcDefenseClues(int currentPower, DerivedProjectEffects effects) =>
        AbodePowerRules.GetRivalArcDefenseClues(currentPower) + effects.VisibleRivalClueBonus;

    public static int GetEffectiveRivalArcClarityTier(int currentPower, DerivedProjectEffects effects) =>
        AbodePowerRules.GetRivalArcClarityTier(currentPower);

    public static bool GetEffectiveRivalArcCounterQuestAccess(int currentPower, DerivedProjectEffects effects) =>
        AbodePowerRules.GetRivalArcCounterQuestAccess(currentPower);

    public static int GetEffectiveRivalArcWarningTier(int currentPower, DerivedProjectEffects effects) =>
        AbodePowerRules.GetRivalArcWarningTier(currentPower) + effects.ArchiveWarningTierBonus;

    public static string GetEffectiveRivalArcOffenseCap(int currentPower, DerivedProjectEffects effects) =>
        AbodePowerRules.GetRivalArcOffenseCap(currentPower);

    public static int GetEffectiveUpgradedTradeSlots(int currentPower, DerivedProjectEffects effects)
    {
        var slotCount = AbodePowerRules.GetTradeSlotCount(currentPower);
        var guaranteedUpgraded = GetEffectiveGuardianRarityCeilingBonusSteps(currentPower, effects) > 0 ? 1 : 0;
        return Math.Min(slotCount, Math.Max(guaranteedUpgraded, effects.UpgradedTradeSlots + effects.ElevatedTradeSlots));
    }

    public static int GetEffectiveElevatedTradeSlots(int currentPower, DerivedProjectEffects effects) =>
        Math.Min(AbodePowerRules.GetTradeSlotCount(currentPower), effects.ElevatedTradeSlots);

    public static int GetEffectiveFortificationSafePressureBonus(DerivedProjectEffects effects) =>
        effects.FortificationSafePressureBonus;

    public static int GetEffectiveFortificationDefenseRatingBonus(DerivedProjectEffects effects) =>
        effects.FortificationDefenseRatingBonus;

    public static int GetActiveTemporaryModifierCount(JsonObject? trackerRoot, string guardianId)
    {
        if (trackerRoot?["temporaryProjectModifiers"] is not JsonArray modifiers ||
            string.IsNullOrWhiteSpace(guardianId))
        {
            return 0;
        }

        return modifiers
            .OfType<JsonObject>()
            .Count(modifier =>
                string.Equals(GetNodeString(modifier["guardianId"]), guardianId, StringComparison.OrdinalIgnoreCase) &&
                GetNodeInt(modifier["remainingApplications"]) > 0);
    }

    public static int GetActiveTemporaryModifierCount(JsonElement trackerRoot, string guardianId)
    {
        if (trackerRoot.ValueKind != JsonValueKind.Object ||
            string.IsNullOrWhiteSpace(guardianId) ||
            !trackerRoot.TryGetProperty("temporaryProjectModifiers", out var modifiers) ||
            modifiers.ValueKind != JsonValueKind.Array)
        {
            return 0;
        }

        var count = 0;
        foreach (var modifier in modifiers.EnumerateArray())
        {
            if (modifier.ValueKind != JsonValueKind.Object ||
                !string.Equals(GetFirstNonEmptyString(modifier, "guardianId"), guardianId, StringComparison.OrdinalIgnoreCase))
            {
                continue;
            }

            if (TryReadIntField(modifier, "remainingApplications", out var remainingApplications) &&
                remainingApplications > 0)
            {
                count++;
            }
        }

        return count;
    }

    public static IReadOnlyList<TemporaryModifierSnapshot> CollectActiveTemporaryModifiers(JsonObject? trackerRoot, string guardianId)
    {
        if (trackerRoot?["temporaryProjectModifiers"] is not JsonArray modifiers ||
            string.IsNullOrWhiteSpace(guardianId))
        {
            return Array.Empty<TemporaryModifierSnapshot>();
        }

        return modifiers
            .OfType<JsonObject>()
            .Where(modifier =>
                string.Equals(GetNodeString(modifier["guardianId"]), guardianId, StringComparison.OrdinalIgnoreCase) &&
                GetNodeInt(modifier["remainingApplications"]) > 0)
            .Select(modifier => new TemporaryModifierSnapshot(
                GetNodeString(modifier["modifierId"]),
                GetNodeString(modifier["modifierType"]),
                GetNodeInt(modifier["value"]),
                GetNodeInt(modifier["remainingApplications"])))
            .ToList();
    }

    public static IReadOnlyList<TemporaryModifierSnapshot> CollectActiveTemporaryModifiers(JsonElement trackerRoot, string guardianId)
    {
        if (trackerRoot.ValueKind != JsonValueKind.Object ||
            string.IsNullOrWhiteSpace(guardianId) ||
            !trackerRoot.TryGetProperty("temporaryProjectModifiers", out var modifiers) ||
            modifiers.ValueKind != JsonValueKind.Array)
        {
            return Array.Empty<TemporaryModifierSnapshot>();
        }

        var result = new List<TemporaryModifierSnapshot>();
        foreach (var modifier in modifiers.EnumerateArray())
        {
            if (modifier.ValueKind != JsonValueKind.Object ||
                !string.Equals(GetFirstNonEmptyString(modifier, "guardianId"), guardianId, StringComparison.OrdinalIgnoreCase))
            {
                continue;
            }

            if (!TryReadIntField(modifier, "remainingApplications", out var remainingApplications) ||
                remainingApplications <= 0)
            {
                continue;
            }

            result.Add(new TemporaryModifierSnapshot(
                GetFirstNonEmptyString(modifier, "modifierId") ?? string.Empty,
                GetFirstNonEmptyString(modifier, "modifierType") ?? string.Empty,
                TryReadIntField(modifier, "value", out var value) ? value : 0,
                remainingApplications));
        }

        return result;
    }

    public static string BuildTradeBonusSignature(ResolvedGuardianDerivedState derivedState) =>
        $"{derivedState.EffectiveUpgradedTradeSlots}|{derivedState.EffectiveElevatedTradeSlots}|{derivedState.EffectiveGuardianRarityCeilingBonusSteps}";

    public static int GetLatestCompletedFortificationBonus(JsonObject? trackerRoot, string guardianId) =>
        GetTierBonusForLatestCompletedProject(trackerRoot, guardianId, "abode_fortification", null);

    public static int GetLatestCompletedCounterOperationBonus(JsonObject? trackerRoot, string guardianId, string? targetGuardianId) =>
        GetTierBonusForLatestCompletedProject(trackerRoot, guardianId, "counter_rival_operation", targetGuardianId);

    public static int GetLatestCompletedOffensiveBonus(JsonObject? trackerRoot, string guardianId, string? targetGuardianId) =>
        GetTierBonusForLatestCompletedProject(trackerRoot, guardianId, "offensive_intrigue", targetGuardianId);

    public static PoliticalShieldBreakdown ResolvePoliticalShield(
        JsonObject? trackerRoot,
        string targetGuardianId,
        string? attackerGuardianId,
        int targetCurrentPower,
        int playerDefenseBonus = 0)
    {
        var baseTargetShield = GetBaseTargetShield(targetCurrentPower);
        var fortificationBonus = GetLatestCompletedFortificationBonus(trackerRoot, targetGuardianId);
        var counterOperationBonus = string.IsNullOrWhiteSpace(attackerGuardianId)
            ? 0
            : GetLatestCompletedCounterOperationBonus(trackerRoot, targetGuardianId, attackerGuardianId);
        var normalizedPlayerDefense = Math.Clamp(playerDefenseBonus, 0, 2);
        var targetShield = Math.Max(0, baseTargetShield + fortificationBonus + counterOperationBonus + normalizedPlayerDefense);
        return new PoliticalShieldBreakdown(
            baseTargetShield,
            fortificationBonus,
            counterOperationBonus,
            normalizedPlayerDefense,
            targetShield);
    }

    public static OffensiveImpactResult ResolveOffensiveImpact(
        JsonObject? trackerRoot,
        string attackerGuardianId,
        string targetGuardianId,
        string? projectTier,
        int attackerCurrentPower,
        int targetCurrentPower,
        int playerDefenseBonus = 0)
    {
        var baseLoss = GetBaseLossByTier(projectTier);
        var attackerBonus = GetAttackerBonus(attackerCurrentPower);
        var shield = ResolvePoliticalShield(trackerRoot, targetGuardianId, attackerGuardianId, targetCurrentPower, playerDefenseBonus);
        var targetLoss = Math.Max(0, baseLoss + attackerBonus - shield.TargetShield);
        var pressureDelta = Math.Max(0, GetPoliticalPressureByTier(projectTier) - shield.TargetShield * 5);
        var stabilityDamage = Math.Max(0, GetPoliticalBaseHitDamageByTier(projectTier) + 4 - shield.TargetShield);
        return new OffensiveImpactResult(
            baseLoss,
            attackerBonus,
            shield.BaseTargetShield,
            shield.FortificationBonus,
            shield.CounterOperationBonus,
            shield.PlayerDefenseBonus,
            shield.TargetShield,
            targetLoss,
            pressureDelta,
            stabilityDamage);
    }

    public static int GetRemainingVisibleRivalClueBudget(JsonObject? trackerRoot, string guardianId, string sourceProjectId, int currentIncarnation)
    {
        if (trackerRoot == null || trackerRoot["completedProjects"] is not JsonArray completedProjects)
            return 0;

        foreach (var entry in GetCompletedProjectsForGuardian(completedProjects, guardianId, "lore_research"))
        {
            if (entry["project"] is not JsonObject project ||
                !string.Equals(GetNodeString(project["projectId"]), sourceProjectId, StringComparison.OrdinalIgnoreCase))
            {
                continue;
            }

            var effectState = EnsureRecipeEffectState(project, currentIncarnation, null);
            if (GetNodeInt(effectState["targetIncarnation"]) != currentIncarnation)
                return 0;

            return GetVisibleRivalClueBudgetRemaining(effectState, project["projectOutcomeAudit"] as JsonObject, GetNodeString(project["projectTier"]));
        }

        return 0;
    }

    public static int GetGrantedVisibleRivalClueBudgetForCurrentLife(JsonObject? project, int currentIncarnation)
    {
        if (project == null ||
            !string.Equals(GetNodeString(project["projectType"]), "lore_research", StringComparison.OrdinalIgnoreCase) ||
            !string.Equals(GetNodeString(project["finalState"]), "Completed", StringComparison.OrdinalIgnoreCase) ||
            project["effectState"] is not JsonObject effectState ||
            GetNodeInt(effectState["targetIncarnation"]) != currentIncarnation)
        {
            return 0;
        }

        return GetNodeInt(
            effectState["visibleRivalClueBudgetGranted"],
            GetOutcomeInt(
                project["projectOutcomeAudit"] as JsonObject,
                "visibleRivalClueBonus",
                GetDefaultLoreResearchVisibleRivalClueBonus(GetNodeString(project["projectTier"]))));
    }

    public static bool TryConsumeVisibleRivalClue(
        JsonObject? trackerRoot,
        string guardianId,
        string sourceProjectId,
        int currentIncarnation,
        int clueCost)
    {
        if (trackerRoot == null || trackerRoot["completedProjects"] is not JsonArray completedProjects)
            return false;

        foreach (var entry in GetCompletedProjectsForGuardian(completedProjects, guardianId, "lore_research"))
        {
            if (entry["project"] is not JsonObject project ||
                !string.Equals(GetNodeString(project["projectId"]), sourceProjectId, StringComparison.OrdinalIgnoreCase))
            {
                continue;
            }

            var effectState = EnsureRecipeEffectState(project, currentIncarnation, null);
            if (GetNodeInt(effectState["targetIncarnation"]) != currentIncarnation)
                return false;

            if (GetVisibleRivalClueBudgetRemaining(effectState, project["projectOutcomeAudit"] as JsonObject, GetNodeString(project["projectTier"])) < clueCost)
                return false;

            effectState["visibleRivalClueBudgetSpent"] = GetNodeInt(effectState["visibleRivalClueBudgetSpent"]) + Math.Max(1, clueCost);
            return true;
        }

        return false;
    }

    public static bool TryConsumeVisibleRivalClue(
        IReadOnlyList<JsonObject> completedProjects,
        string guardianId,
        string sourceProjectId,
        int currentIncarnation,
        int clueCost)
    {
        foreach (var entry in completedProjects)
        {
            if (!string.Equals(GetGuardianId(entry), guardianId, StringComparison.OrdinalIgnoreCase) ||
                entry["project"] is not JsonObject project ||
                !string.Equals(GetNodeString(project["projectType"]), "lore_research", StringComparison.OrdinalIgnoreCase) ||
                !string.Equals(GetNodeString(project["finalState"]), "Completed", StringComparison.OrdinalIgnoreCase) ||
                !string.Equals(GetNodeString(project["projectId"]), sourceProjectId, StringComparison.OrdinalIgnoreCase))
            {
                continue;
            }

            var effectState = EnsureRecipeEffectState(project, currentIncarnation, null);
            if (GetNodeInt(effectState["targetIncarnation"]) != currentIncarnation)
                return false;

            if (GetVisibleRivalClueBudgetRemaining(effectState, project["projectOutcomeAudit"] as JsonObject, GetNodeString(project["projectTier"])) < clueCost)
                return false;

            effectState["visibleRivalClueBudgetSpent"] = GetNodeInt(effectState["visibleRivalClueBudgetSpent"]) + Math.Max(1, clueCost);
            return true;
        }

        return false;
    }

    public static string BuildTradeBonusSignature(int currentPower, DerivedProjectEffects effects) =>
        BuildTradeBonusSignature(ResolveGuardianDerivedStateInternal(currentPower, effects, 0));

    public static JsonArray BuildSystemEffectSummary(string projectType, string finalState, string projectTier, JsonObject? projectOutcomeAudit)
    {
        var lines = new JsonArray();
        switch (projectType)
        {
            case "abode_fortification" when string.Equals(finalState, "Completed", StringComparison.OrdinalIgnoreCase):
                lines.Add($"+ safe pressure bonus: {GetOutcomeInt(projectOutcomeAudit, "safePressureBonus", GetDefaultFortificationSafePressureBonus(projectTier))}");
                lines.Add($"+ defense rating bonus: {GetOutcomeInt(projectOutcomeAudit, "defenseRatingBonus", GetDefaultFortificationDefenseRatingBonus(projectTier))}");
                lines.Add("⏳ действует как постоянный defensive multiplier до замены новым terminal проектом укрепления");
                break;

            case "abode_fortification" when string.Equals(finalState, "Sabotaged", StringComparison.OrdinalIgnoreCase):
                lines.Add("+ штраф к следующему internal project: starting pressure +10");
                lines.Add("⏳ применяется один раз при старте следующего internal проекта");
                break;

            case "relic_forging" when string.Equals(finalState, "Completed", StringComparison.OrdinalIgnoreCase):
                lines.Add($"+ улучшенные торговые слоты: {GetOutcomeInt(projectOutcomeAudit, "upgradedTradeSlots", GetDefaultRelicForgingUpgradedTradeSlots(projectTier))}");
                var elevatedSlots = GetOutcomeInt(projectOutcomeAudit, "elevatedTradeSlots", GetDefaultRelicForgingElevatedTradeSlots(projectTier));
                if (elevatedSlots > 0)
                    lines.Add($"+ возвышенные торговые слоты: {elevatedSlots}");
                var rarityBonus = GetOutcomeInt(projectOutcomeAudit, "guardianRarityCeilingBonusSteps", GetDefaultRelicForgingRarityBonusSteps(projectTier));
                if (rarityBonus > 0)
                    lines.Add($"+ шаги потолка редкости: {rarityBonus}");
                lines.Add("⏳ действует до следующего обновления торговли и одной guardian-mediated гача-попытки");
                break;

            case "lore_research" when string.Equals(finalState, "Completed", StringComparison.OrdinalIgnoreCase):
                lines.Add($"+ раскрытые фрагменты знаний: {GetOutcomeInt(projectOutcomeAudit, "bonusLoreUnlocks", GetDefaultLoreResearchBonusLoreUnlocks(projectTier))}");
                var hookCount = GetOutcomeInt(projectOutcomeAudit, "questHookCount", GetDefaultLoreResearchQuestHookCount(projectTier));
                if (hookCount > 0)
                    lines.Add($"+ квестовые зацепки: {hookCount}");
                var guaranteedArchiveQuests = GetOutcomeInt(projectOutcomeAudit, "guaranteedArchiveQuestCount", 0);
                if (guaranteedArchiveQuests > 0)
                    lines.Add($"+ гарантированные архивные квесты Хранителя: {guaranteedArchiveQuests}");
                var specialLines = GetOutcomeInt(projectOutcomeAudit, "specialQuestLineUnlocks", GetDefaultLoreResearchSpecialQuestLineUnlocks(projectTier));
                if (specialLines > 0)
                    lines.Add($"+ особые квестовые линии: {specialLines}");
                var clueBonus = GetOutcomeInt(projectOutcomeAudit, "visibleRivalClueBonus", GetDefaultLoreResearchVisibleRivalClueBonus(projectTier));
                if (clueBonus > 0)
                    lines.Add($"+ бюджет видимых clue для rival-нитей: {clueBonus}");
                var warningBonus = GetOutcomeInt(projectOutcomeAudit, "archiveWarningTierBonus", 0);
                if (warningBonus > 0)
                    lines.Add($"+ warning tier для rival-нитей: {warningBonus}");
                lines.Add("⏳ действует только в целевой смертной жизни");
                break;

            case "soul_preparation" when string.Equals(finalState, "Completed", StringComparison.OrdinalIgnoreCase):
                lines.Add($"+ очки бюджета подготовки: {GetOutcomeInt(projectOutcomeAudit, "preparationBudgetPoints", GetDefaultSoulPreparationBudgetPoints(projectTier))}");
                var claimBonus = GetOutcomeInt(projectOutcomeAudit, "preparationClaimPriorityBonus", GetDefaultSoulPreparationClaimPriorityBonus(projectTier));
                if (claimBonus > 0)
                    lines.Add($"+ приоритет claim-а для корректив: {claimBonus}");
                lines.Add("⏳ действует только на следующую жизнь и сгорает после correction resolution");
                break;

            case "soul_preparation" when string.Equals(finalState, "Sabotaged", StringComparison.OrdinalIgnoreCase):
                lines.Add($"+ враждебные токены приоритета: {GetOutcomeInt(projectOutcomeAudit, "hostilePriorityTokensGranted", GetDefaultSoulPreparationHostilePriorityTokens(projectTier))}");
                lines.Add("⏳ действует только на следующую жизнь и сгорает после correction resolution");
                break;

            case "offensive_intrigue" when string.Equals(finalState, "Completed", StringComparison.OrdinalIgnoreCase):
                lines.Add($"+ урон rival-Обители по формуле: target loss {GetOutcomeInt(projectOutcomeAudit, "targetLoss", 0)}");
                lines.Add($"+ hostile pressure к активному проекту цели: {GetOutcomeInt(projectOutcomeAudit, "pressureDelta", GetPoliticalPressureByTier(projectTier))}");
                lines.Add($"+ direct stability damage к активному проекту цели: {GetOutcomeInt(projectOutcomeAudit, "stabilityDamage", GetPoliticalBaseHitDamageByTier(projectTier) + 4)}");
                var hostilityWeight = GetOutcomeInt(projectOutcomeAudit, "hostilityWeight", 0);
                if (hostilityWeight > 0)
                    lines.Add($"+ preferred hostile target weight: {hostilityWeight}");
                var targetAttitudeTier = GetNodeString(projectOutcomeAudit?["targetAttitudeTier"]);
                if (string.Equals(targetAttitudeTier, GuardianRelationshipRules.NeutralTier, StringComparison.OrdinalIgnoreCase))
                    lines.Add("! neutral target: политическое давление считается слабо мотивированным");
                break;

            case "counter_rival_operation" when string.Equals(finalState, "Completed", StringComparison.OrdinalIgnoreCase):
                lines.Add($"+ relief pressure defended project: {GetOutcomeInt(projectOutcomeAudit, "pressureRelief", GetCounterOperationPressureRelief(projectTier))}");
                lines.Add($"+ relief stability defended project: {GetOutcomeInt(projectOutcomeAudit, "stabilityRelief", GetCounterOperationStabilityRelief(projectTier))}");
                lines.Add($"+ сила Обители: {GetOutcomeInt(projectOutcomeAudit, "abodePowerGain", GetCounterOperationAbodePowerGain(projectTier))}");
                var coalitionSupportBonus = GetOutcomeInt(projectOutcomeAudit, "coalitionSupportBonus", 0);
                if (coalitionSupportBonus > 0)
                    lines.Add($"+ coalition support bonus: {coalitionSupportBonus}");
                break;
        }

        return lines;
    }

    public static int GetDefaultRelicForgingUpgradedTradeSlots(string? projectTier) => 1;

    public static int GetDefaultFortificationSafePressureBonus(string? projectTier) =>
        (projectTier ?? "").Trim().ToLowerInvariant() switch
        {
            "grand" => 15,
            "major" => 10,
            _ => 5
        };

    public static int GetDefaultFortificationDefenseRatingBonus(string? projectTier) =>
        (projectTier ?? "").Trim().ToLowerInvariant() switch
        {
            "grand" => 3,
            "major" => 2,
            _ => 1
        };

    public static int GetDefaultRelicForgingElevatedTradeSlots(string? projectTier) =>
        string.Equals(projectTier, "grand", StringComparison.OrdinalIgnoreCase) ? 1 : 0;

    public static int GetDefaultRelicForgingRarityBonusSteps(string? projectTier) =>
        string.Equals(projectTier, "major", StringComparison.OrdinalIgnoreCase) || string.Equals(projectTier, "grand", StringComparison.OrdinalIgnoreCase) ? 1 : 0;

    public static int GetDefaultLoreResearchBonusLoreUnlocks(string? projectTier) => 1;

    public static int GetDefaultLoreResearchQuestHookCount(string? projectTier) =>
        string.Equals(projectTier, "major", StringComparison.OrdinalIgnoreCase) || string.Equals(projectTier, "grand", StringComparison.OrdinalIgnoreCase) ? 1 : 0;

    public static int GetDefaultLoreResearchSpecialQuestLineUnlocks(string? projectTier) =>
        string.Equals(projectTier, "grand", StringComparison.OrdinalIgnoreCase) ? 1 : 0;

    public static int GetDefaultLoreResearchVisibleRivalClueBonus(string? projectTier) =>
        (projectTier ?? "").Trim().ToLowerInvariant() switch
        {
            "grand" => 3,
            "major" => 2,
            _ => 1
        };

    public static int GetDefaultSoulPreparationBudgetPoints(string? projectTier) =>
        (projectTier ?? "").Trim().ToLowerInvariant() switch
        {
            "grand" => 3,
            "major" => 2,
            _ => 1
        };

    public static int GetDefaultSoulPreparationClaimPriorityBonus(string? projectTier) => 1;

    public static int GetDefaultSoulPreparationHostilePriorityTokens(string? projectTier) => 2;

    private static DerivedProjectEffects ResolveDerivedEffectsInternal(IEnumerable<JsonObject> projects)
    {
        var upgradedTradeSlots = 0;
        var elevatedTradeSlots = 0;
        var guardianRarityCeilingBonusSteps = 0;
        var bonusLoreUnlocks = 0;
        var questHookCount = 0;
        var guaranteedArchiveQuestCount = 0;
        var specialQuestLineUnlocks = 0;
        var visibleRivalClueBonus = 0;
        var archiveWarningTierBonus = 0;
        var preparationBudgetPoints = 0;
        var preparationClaimPriorityBonus = 0;
        var hostilePriorityTokensGranted = 0;
        var fortificationSafePressureBonus = 0;
        var fortificationDefenseRatingBonus = 0;

        foreach (var project in projects)
        {
            var projectType = GetNodeString(project["projectType"]);
            var projectTier = GetNodeString(project["projectTier"]);
            var finalState = GetNodeString(project["finalState"]);
            var audit = project["projectOutcomeAudit"] as JsonObject;
            var effectState = project["effectState"] as JsonObject;

            switch (projectType)
            {
                case "abode_fortification" when string.Equals(finalState, "Completed", StringComparison.OrdinalIgnoreCase):
                    fortificationSafePressureBonus = Math.Max(
                        fortificationSafePressureBonus,
                        GetEffectOrAuditInt(effectState, "safePressureBonusGranted", audit, "safePressureBonus", GetDefaultFortificationSafePressureBonus(projectTier)));
                    fortificationDefenseRatingBonus = Math.Max(
                        fortificationDefenseRatingBonus,
                        GetEffectOrAuditInt(effectState, "defenseRatingBonusGranted", audit, "defenseRatingBonus", GetDefaultFortificationDefenseRatingBonus(projectTier)));
                    break;

                case "relic_forging" when string.Equals(finalState, "Completed", StringComparison.OrdinalIgnoreCase):
                    if (GetTradeRefreshUsesRemaining(effectState, audit, projectTier) > 0)
                    {
                        upgradedTradeSlots += GetEffectOrAuditInt(effectState, "upgradedTradeSlotsGranted", audit, "upgradedTradeSlots", GetDefaultRelicForgingUpgradedTradeSlots(projectTier));
                        elevatedTradeSlots += GetEffectOrAuditInt(effectState, "elevatedTradeSlotsGranted", audit, "elevatedTradeSlots", GetDefaultRelicForgingElevatedTradeSlots(projectTier));
                        guardianRarityCeilingBonusSteps = Math.Max(
                            guardianRarityCeilingBonusSteps,
                            GetEffectOrAuditInt(effectState, "rarityCeilingBonusStepsGranted", audit, "guardianRarityCeilingBonusSteps", GetDefaultRelicForgingRarityBonusSteps(projectTier)));
                    }
                    break;

                case "lore_research" when string.Equals(finalState, "Completed", StringComparison.OrdinalIgnoreCase):
                    bonusLoreUnlocks += GetEffectOrAuditInt(effectState, "bonusLoreUnlocksApplied", audit, "bonusLoreUnlocks", GetDefaultLoreResearchBonusLoreUnlocks(projectTier));
                    questHookCount += GetQuestHookTokensRemaining(effectState, audit, projectTier);
                    guaranteedArchiveQuestCount += GetGuaranteedArchiveQuestRemaining(effectState, audit, projectTier);
                    specialQuestLineUnlocks += GetSpecialQuestLineTokensRemaining(effectState, audit, projectTier);
                    visibleRivalClueBonus += GetVisibleRivalClueBudgetRemaining(effectState, audit, projectTier);
                    archiveWarningTierBonus += GetArchiveWarningTierBonus(effectState, audit);
                    break;

                case "soul_preparation" when string.Equals(finalState, "Completed", StringComparison.OrdinalIgnoreCase):
                    preparationBudgetPoints += GetPreparationBudgetPointsRemaining(effectState, audit, projectTier);
                    preparationClaimPriorityBonus += GetPreparationClaimPriorityBonusRemaining(effectState, audit, projectTier);
                    break;

                case "soul_preparation" when string.Equals(finalState, "Sabotaged", StringComparison.OrdinalIgnoreCase):
                    hostilePriorityTokensGranted += GetHostilePriorityTokensRemaining(effectState, audit, projectTier);
                    break;
            }
        }

        return new DerivedProjectEffects(
            upgradedTradeSlots,
            elevatedTradeSlots,
            guardianRarityCeilingBonusSteps,
            bonusLoreUnlocks,
            questHookCount,
            guaranteedArchiveQuestCount,
            specialQuestLineUnlocks,
            visibleRivalClueBonus,
            archiveWarningTierBonus,
            preparationBudgetPoints,
            preparationClaimPriorityBonus,
            hostilePriorityTokensGranted,
            fortificationSafePressureBonus,
            fortificationDefenseRatingBonus);
    }

    private static ResolvedGuardianDerivedState ResolveGuardianDerivedStateInternal(
        int currentPower,
        DerivedProjectEffects effects,
        int activeTemporaryModifierCount)
    {
        var clampedPower = AbodePowerRules.ClampCurrentPower(currentPower);
        var tradeSlotCount = AbodePowerRules.GetTradeSlotCount(clampedPower);
        var guardianQuestCap = AbodePowerRules.GetGuardianQuestCap(clampedPower);
        var guardianQuestDifficultyCeiling = AbodePowerRules.GetGuardianQuestDifficultyCeiling(clampedPower);
        var bonusGachaCharges = AbodePowerRules.GetBonusGachaCharges(clampedPower);
        var baseGuardianRarityCeilingBonusSteps = AbodePowerRules.GetGuardianRarityCeilingBonusSteps(clampedPower);
        var effectiveGuardianRarityCeilingBonusSteps = GetEffectiveGuardianRarityCeilingBonusSteps(clampedPower, effects);
        var baseNextLifeCorrectionBudgetPoints = AbodePowerRules.GetNextLifeCorrectionBudgetPoints(clampedPower);
        var effectiveNextLifeCorrectionBudgetPoints = GetEffectiveNextLifeCorrectionBudgetPoints(clampedPower, effects);
        var baseRivalArcDefenseClues = AbodePowerRules.GetRivalArcDefenseClues(clampedPower);
        var effectiveRivalArcDefenseClues = GetEffectiveRivalArcDefenseClues(clampedPower, effects);
        var rivalArcClarityTier = GetEffectiveRivalArcClarityTier(clampedPower, effects);
        var rivalArcCounterQuestAccess = GetEffectiveRivalArcCounterQuestAccess(clampedPower, effects);
        var rivalArcWarningTier = GetEffectiveRivalArcWarningTier(clampedPower, effects);
        var rivalArcOffenseCap = GetEffectiveRivalArcOffenseCap(clampedPower, effects);
        var effectiveUpgradedTradeSlots = GetEffectiveUpgradedTradeSlots(clampedPower, effects);
        var effectiveElevatedTradeSlots = GetEffectiveElevatedTradeSlots(clampedPower, effects);
        var fortificationSafePressureBonus = GetEffectiveFortificationSafePressureBonus(effects);
        var fortificationDefenseRatingBonus = GetEffectiveFortificationDefenseRatingBonus(effects);

        return new ResolvedGuardianDerivedState(
            clampedPower,
            AbodePowerRules.GetTierLabel(clampedPower),
            AbodePowerRules.GetTierColor(clampedPower),
            tradeSlotCount,
            guardianQuestCap,
            guardianQuestDifficultyCeiling,
            bonusGachaCharges,
            baseGuardianRarityCeilingBonusSteps,
            effectiveGuardianRarityCeilingBonusSteps,
            baseNextLifeCorrectionBudgetPoints,
            effectiveNextLifeCorrectionBudgetPoints,
            baseRivalArcDefenseClues,
            effectiveRivalArcDefenseClues,
            rivalArcClarityTier,
            rivalArcCounterQuestAccess,
            rivalArcWarningTier,
            rivalArcOffenseCap,
            effectiveUpgradedTradeSlots,
            effectiveElevatedTradeSlots,
            fortificationSafePressureBonus,
            fortificationDefenseRatingBonus,
            Math.Max(0, activeTemporaryModifierCount),
            effects);
    }

    private static JsonObject BuildDefaultEffectState(JsonObject project, int currentIncarnation, string? currentRealm)
    {
        var projectType = GetNodeString(project["projectType"]);
        var finalState = GetNodeString(project["finalState"]);
        var projectTier = GetNodeString(project["projectTier"]);
        var audit = project["projectOutcomeAudit"] as JsonObject;

        return projectType switch
        {
            "abode_fortification" when string.Equals(finalState, "Completed", StringComparison.OrdinalIgnoreCase) => new JsonObject
            {
                ["safePressureBonusGranted"] = GetOutcomeInt(audit, "safePressureBonus", GetDefaultFortificationSafePressureBonus(projectTier)),
                ["defenseRatingBonusGranted"] = GetOutcomeInt(audit, "defenseRatingBonus", GetDefaultFortificationDefenseRatingBonus(projectTier))
            },
            "relic_forging" when string.Equals(finalState, "Completed", StringComparison.OrdinalIgnoreCase) => new JsonObject
            {
                ["tradeRefreshUsesGranted"] = 1,
                ["tradeRefreshUsesSpent"] = 0,
                ["gachaUsesGranted"] = 1,
                ["gachaUsesSpent"] = 0,
                ["upgradedTradeSlotsGranted"] = GetOutcomeInt(audit, "upgradedTradeSlots", GetDefaultRelicForgingUpgradedTradeSlots(projectTier)),
                ["elevatedTradeSlotsGranted"] = GetOutcomeInt(audit, "elevatedTradeSlots", GetDefaultRelicForgingElevatedTradeSlots(projectTier)),
                ["rarityCeilingBonusStepsGranted"] = GetOutcomeInt(audit, "guardianRarityCeilingBonusSteps", GetDefaultRelicForgingRarityBonusSteps(projectTier))
            },
            "lore_research" when string.Equals(finalState, "Completed", StringComparison.OrdinalIgnoreCase) => new JsonObject
            {
                ["targetIncarnation"] = ResolveLoreResearchTargetIncarnation(currentIncarnation, currentRealm),
                ["bonusLoreUnlocksApplied"] = GetOutcomeInt(audit, "bonusLoreUnlocks", GetDefaultLoreResearchBonusLoreUnlocks(projectTier)),
                ["questHookTokensGranted"] = GetOutcomeInt(audit, "questHookCount", GetDefaultLoreResearchQuestHookCount(projectTier)),
                ["questHookTokensSpent"] = 0,
                ["guaranteedArchiveQuestGranted"] = GetOutcomeInt(audit, "guaranteedArchiveQuestCount", 0),
                ["guaranteedArchiveQuestSpawned"] = 0,
                ["guaranteedArchiveQuestConsumed"] = 0,
                ["specialQuestLineTokensGranted"] = GetOutcomeInt(audit, "specialQuestLineUnlocks", GetDefaultLoreResearchSpecialQuestLineUnlocks(projectTier)),
                ["specialQuestLineTokensSpent"] = 0,
                ["visibleRivalClueBudgetGranted"] = GetOutcomeInt(audit, "visibleRivalClueBonus", GetDefaultLoreResearchVisibleRivalClueBonus(projectTier)),
                ["visibleRivalClueBudgetSpent"] = 0,
                ["archiveWarningTierBonusGranted"] = GetOutcomeInt(audit, "archiveWarningTierBonus", 0)
            },
            "soul_preparation" when string.Equals(finalState, "Completed", StringComparison.OrdinalIgnoreCase) => new JsonObject
            {
                ["targetIncarnation"] = Math.Max(1, currentIncarnation + 1),
                ["preparationBudgetPointsGranted"] = GetOutcomeInt(audit, "preparationBudgetPoints", GetDefaultSoulPreparationBudgetPoints(projectTier)),
                ["preparationBudgetPointsSpent"] = 0,
                ["preparationClaimPriorityBonusGranted"] = GetOutcomeInt(audit, "preparationClaimPriorityBonus", GetDefaultSoulPreparationClaimPriorityBonus(projectTier)),
                ["consumedAtLifeStart"] = false
            },
            "soul_preparation" when string.Equals(finalState, "Sabotaged", StringComparison.OrdinalIgnoreCase) => new JsonObject
            {
                ["targetIncarnation"] = Math.Max(1, currentIncarnation + 1),
                ["hostilePriorityTokensGranted"] = GetOutcomeInt(audit, "hostilePriorityTokensGranted", GetDefaultSoulPreparationHostilePriorityTokens(projectTier)),
                ["hostilePriorityTokensSpent"] = 0,
                ["consumedAtLifeStart"] = false
            },
            _ => new JsonObject()
        };
    }

    private static int GetTierBonusForLatestCompletedProject(JsonObject? trackerRoot, string guardianId, string projectType, string? targetGuardianId)
    {
        var project = FindLatestCompletedProject(trackerRoot, guardianId, projectType, targetGuardianId);
        if (project == null || !string.Equals(GetNodeString(project["finalState"]), "Completed", StringComparison.OrdinalIgnoreCase))
            return 0;

        return projectType switch
        {
            "abode_fortification" => GetFortificationBonus(GetNodeString(project["projectTier"])),
            "counter_rival_operation" => GetCounterOperationBonus(GetNodeString(project["projectTier"])),
            "offensive_intrigue" => GetOffensiveProjectBonus(GetNodeString(project["projectTier"])),
            _ => GetTierWeight(GetNodeString(project["projectTier"]))
        };
    }

    private static JsonObject? FindLatestCompletedProject(JsonObject? trackerRoot, string guardianId, string projectType, string? targetGuardianId)
    {
        if (trackerRoot?["completedProjects"] is not JsonArray completedProjects)
            return null;

        return completedProjects
            .OfType<JsonObject>()
            .Where(item => string.Equals(GetGuardianId(item), guardianId, StringComparison.OrdinalIgnoreCase))
            .Select(item => item["project"] as JsonObject)
            .Where(project => project != null)
            .Where(project =>
                string.Equals(GetNodeString(project!["projectType"]), projectType, StringComparison.OrdinalIgnoreCase) &&
                (string.IsNullOrWhiteSpace(targetGuardianId) ||
                 string.Equals(GetNodeString(project!["targetGuardianId"]), targetGuardianId, StringComparison.OrdinalIgnoreCase)))
            .OrderByDescending(project => GetNodeInt(project!["completionTurn"]))
            .FirstOrDefault();
    }

    private static IEnumerable<JsonObject> GetCompletedProjectsForGuardian(JsonArray completedProjects, string guardianId, string projectType) =>
        completedProjects.OfType<JsonObject>()
            .Where(entry =>
                string.Equals(GetGuardianId(entry), guardianId, StringComparison.OrdinalIgnoreCase) &&
                entry["project"] is JsonObject project &&
                string.Equals(GetNodeString(project["projectType"]), projectType, StringComparison.OrdinalIgnoreCase) &&
                string.Equals(GetNodeString(project["finalState"]), "Completed", StringComparison.OrdinalIgnoreCase))
            .OrderBy(entry => GetNodeInt((entry["project"] as JsonObject)?["completionTurn"]))
            .ThenBy(entry => GetProjectId(entry), StringComparer.OrdinalIgnoreCase);

    private static bool ExpireEffectState(JsonObject effectState, int lifeIncarnation)
    {
        var changed = false;
        changed |= SetIfDifferent(effectState, "questHookTokensSpent", GetNodeInt(effectState["questHookTokensGranted"]));
        changed |= SetIfDifferent(effectState, "guaranteedArchiveQuestSpawned", GetNodeInt(effectState["guaranteedArchiveQuestGranted"]));
        changed |= SetIfDifferent(effectState, "guaranteedArchiveQuestConsumed", GetNodeInt(effectState["guaranteedArchiveQuestGranted"]));
        changed |= SetIfDifferent(effectState, "specialQuestLineTokensSpent", GetNodeInt(effectState["specialQuestLineTokensGranted"]));
        changed |= SetIfDifferent(effectState, "visibleRivalClueBudgetSpent", GetNodeInt(effectState["visibleRivalClueBudgetGranted"]));
        changed |= SetIfDifferent(effectState, "preparationBudgetPointsSpent", GetNodeInt(effectState["preparationBudgetPointsGranted"]));
        changed |= SetIfDifferent(effectState, "hostilePriorityTokensSpent", GetNodeInt(effectState["hostilePriorityTokensGranted"]));
        changed |= SetIfDifferent(effectState, "expiredAtLifeIncarnation", lifeIncarnation);
        return changed;
    }

    private static bool SetIfDifferent(JsonObject effectState, string fieldName, int targetValue)
    {
        if (GetNodeInt(effectState[fieldName]) == targetValue)
            return false;

        effectState[fieldName] = targetValue;
        return true;
    }

    private static int ResolveLoreResearchTargetIncarnation(int currentIncarnation, string? currentRealm)
    {
        return RealmSemantics.IsMortalRealm(currentRealm)
            ? Math.Max(1, currentIncarnation)
            : Math.Max(1, currentIncarnation + 1);
    }

    private static int GetTradeRefreshUsesRemaining(JsonObject? effectState, JsonObject? audit = null, string? projectTier = null)
    {
        if (effectState == null)
            return audit == null ? 0 : 1;

        var granted = GetNodeInt(effectState["tradeRefreshUsesGranted"], 1);
        var spent = GetNodeInt(effectState["tradeRefreshUsesSpent"]);
        return Math.Max(0, granted - spent);
    }

    private static int GetGachaUsesRemaining(JsonObject? effectState, JsonObject? audit = null, string? projectTier = null)
    {
        if (effectState == null)
            return audit == null ? 0 : 1;

        var granted = GetNodeInt(effectState["gachaUsesGranted"], 1);
        var spent = GetNodeInt(effectState["gachaUsesSpent"]);
        return Math.Max(0, granted - spent);
    }

    private static int GetQuestHookTokensRemaining(JsonObject? effectState, JsonObject? audit = null, string? projectTier = null)
    {
        if (effectState == null)
            return GetOutcomeInt(audit, "questHookCount", GetDefaultLoreResearchQuestHookCount(projectTier));

        var granted = GetNodeInt(effectState["questHookTokensGranted"], GetOutcomeInt(audit, "questHookCount", GetDefaultLoreResearchQuestHookCount(projectTier)));
        var spent = GetNodeInt(effectState["questHookTokensSpent"]);
        return Math.Max(0, granted - spent);
    }

    private static int GetGuaranteedArchiveQuestRemaining(JsonObject? effectState, JsonObject? audit = null, string? projectTier = null)
    {
        if (effectState == null)
            return GetOutcomeInt(audit, "guaranteedArchiveQuestCount", 0);

        var granted = GetNodeInt(effectState["guaranteedArchiveQuestGranted"], GetOutcomeInt(audit, "guaranteedArchiveQuestCount", 0));
        var consumed = GetNodeInt(effectState["guaranteedArchiveQuestConsumed"]);
        return Math.Max(0, granted - consumed);
    }

    private static int GetSpecialQuestLineTokensRemaining(JsonObject? effectState, JsonObject? audit = null, string? projectTier = null)
    {
        if (effectState == null)
            return GetOutcomeInt(audit, "specialQuestLineUnlocks", GetDefaultLoreResearchSpecialQuestLineUnlocks(projectTier));

        var granted = GetNodeInt(effectState["specialQuestLineTokensGranted"], GetOutcomeInt(audit, "specialQuestLineUnlocks", GetDefaultLoreResearchSpecialQuestLineUnlocks(projectTier)));
        var spent = GetNodeInt(effectState["specialQuestLineTokensSpent"]);
        return Math.Max(0, granted - spent);
    }

    private static int GetVisibleRivalClueBudgetRemaining(JsonObject? effectState, JsonObject? audit = null, string? projectTier = null)
    {
        if (effectState == null)
            return GetOutcomeInt(audit, "visibleRivalClueBonus", GetDefaultLoreResearchVisibleRivalClueBonus(projectTier));

        var granted = GetNodeInt(effectState["visibleRivalClueBudgetGranted"], GetOutcomeInt(audit, "visibleRivalClueBonus", GetDefaultLoreResearchVisibleRivalClueBonus(projectTier)));
        var spent = GetNodeInt(effectState["visibleRivalClueBudgetSpent"]);
        return Math.Max(0, granted - spent);
    }

    private static int GetArchiveWarningTierBonus(JsonObject? effectState, JsonObject? audit = null)
    {
        if (effectState == null)
            return GetOutcomeInt(audit, "archiveWarningTierBonus", 0);

        return GetNodeInt(effectState["archiveWarningTierBonusGranted"], GetOutcomeInt(audit, "archiveWarningTierBonus", 0));
    }

    private static int GetPreparationBudgetPointsRemaining(JsonObject? effectState, JsonObject? audit = null, string? projectTier = null)
    {
        if (effectState == null)
            return GetOutcomeInt(audit, "preparationBudgetPoints", GetDefaultSoulPreparationBudgetPoints(projectTier));
        if (GetNodeBool(effectState["consumedAtLifeStart"]))
            return 0;

        var granted = GetNodeInt(effectState["preparationBudgetPointsGranted"], GetOutcomeInt(audit, "preparationBudgetPoints", GetDefaultSoulPreparationBudgetPoints(projectTier)));
        var spent = GetNodeInt(effectState["preparationBudgetPointsSpent"]);
        return Math.Max(0, granted - spent);
    }

    private static int GetPreparationClaimPriorityBonusRemaining(JsonObject? effectState, JsonObject? audit = null, string? projectTier = null)
    {
        if (effectState == null)
            return GetOutcomeInt(audit, "preparationClaimPriorityBonus", GetDefaultSoulPreparationClaimPriorityBonus(projectTier));
        if (GetNodeBool(effectState["consumedAtLifeStart"]))
            return 0;

        return GetNodeInt(effectState["preparationClaimPriorityBonusGranted"], GetOutcomeInt(audit, "preparationClaimPriorityBonus", GetDefaultSoulPreparationClaimPriorityBonus(projectTier)));
    }

    private static int GetHostilePriorityTokensRemaining(JsonObject? effectState, JsonObject? audit = null, string? projectTier = null)
    {
        if (effectState == null)
            return GetOutcomeInt(audit, "hostilePriorityTokensGranted", GetDefaultSoulPreparationHostilePriorityTokens(projectTier));
        if (GetNodeBool(effectState["consumedAtLifeStart"]))
            return 0;

        var granted = GetNodeInt(effectState["hostilePriorityTokensGranted"], GetOutcomeInt(audit, "hostilePriorityTokensGranted", GetDefaultSoulPreparationHostilePriorityTokens(projectTier)));
        var spent = GetNodeInt(effectState["hostilePriorityTokensSpent"]);
        return Math.Max(0, granted - spent);
    }

    private static int GetEffectOrAuditInt(JsonObject? effectState, string effectField, JsonObject? audit, string auditField, int fallback)
    {
        if (effectState != null && effectState[effectField] is JsonNode node)
            return GetNodeInt(node, fallback);

        return GetOutcomeInt(audit, auditField, fallback);
    }

    private static int GetOutcomeInt(JsonObject? audit, string key, int fallback)
    {
        if (audit?[key] is JsonNode node)
        {
            if (node is JsonValue value && value.TryGetValue<int>(out var parsed))
                return parsed;
            if (int.TryParse(node.ToJsonString().Trim('"'), out var parsedText))
                return parsedText;
        }

        return fallback;
    }

    private static int GetNodeInt(JsonNode? node, int fallback = 0)
    {
        if (node is JsonValue value)
        {
            if (value.TryGetValue<int>(out var parsed))
                return parsed;
            if (value.TryGetValue<string>(out var text) && int.TryParse(text, out var parsedText))
                return parsedText;
        }

        return fallback;
    }

    private static bool TryReadIntField(JsonElement element, string propertyName, out int value)
    {
        value = 0;
        if (!element.TryGetProperty(propertyName, out var property))
            return false;

        if (property.ValueKind == JsonValueKind.Number && property.TryGetInt32(out var parsed))
        {
            value = parsed;
            return true;
        }

        if (property.ValueKind == JsonValueKind.String && int.TryParse(property.GetString(), out var parsedString))
        {
            value = parsedString;
            return true;
        }

        return false;
    }

    private static string GetFirstNonEmptyString(JsonElement element, params string[] propertyNames)
    {
        foreach (var propertyName in propertyNames)
        {
            if (!element.TryGetProperty(propertyName, out var property) || property.ValueKind != JsonValueKind.String)
                continue;

            var value = property.GetString();
            if (!string.IsNullOrWhiteSpace(value))
                return value!;
        }

        return string.Empty;
    }

    private static bool GetNodeBool(JsonNode? node)
    {
        if (node is JsonValue value)
        {
            if (value.TryGetValue<bool>(out var parsed))
                return parsed;
            if (value.TryGetValue<string>(out var text) && bool.TryParse(text, out var parsedText))
                return parsedText;
        }

        return false;
    }

    private static string GetNodeString(JsonNode? node) =>
        node is JsonValue value && value.TryGetValue<string>(out var stringValue)
            ? stringValue ?? ""
            : "";

    private static string GetString(JsonElement element, string propertyName)
    {
        if (!element.TryGetProperty(propertyName, out var value) || value.ValueKind != JsonValueKind.String)
            return "";

        return value.GetString() ?? "";
    }
}
