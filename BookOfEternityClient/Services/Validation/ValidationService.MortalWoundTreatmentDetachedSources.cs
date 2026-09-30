using System.Collections.ObjectModel;
using System.Text.Json;
using System.Text.Json.Nodes;

namespace BookOfEternityClient.Services;

public partial class ValidationService
{
    private const string MortalWoundTreatmentNpcCorePath =
        "game_state/npcs/npc_core.json";
    private const string MortalWoundTreatmentPlayerActiveSkillsPath =
        "game_state/player/skills_active.json";
    private const string MortalWoundTreatmentPlayerPassiveSkillsPath =
        "game_state/player/skills_passive.json";
    private const string MortalWoundTreatmentPlayerSkillMasteryPath =
        "game_state/player/skill_mastery.json";
    private const string MortalWoundTreatmentRegularQuestsPath =
        "game_state/quests/regular_quests.json";

    internal IReadOnlyList<ValidationIssue>
        ValidateMortalWoundTreatmentDetachedSources(
            IReadOnlyDictionary<string, JsonObject> roots)
    {
        ArgumentNullException.ThrowIfNull(roots);
        var issues = new List<ValidationIssue>();

        ValidateDetachedNpcCore(roots, issues);
        ValidateDetachedPlayerSkillRoot(
            roots,
            MortalWoundTreatmentPlayerActiveSkillsPath,
            "activeSkillChanges",
            issues);
        ValidateDetachedPlayerSkillRoot(
            roots,
            MortalWoundTreatmentPlayerPassiveSkillsPath,
            "passiveSkillChanges",
            issues);
        ValidateDetachedPlayerSkillMastery(roots, issues);
        ValidateDetachedCombatRoot(
            roots,
            EffectCarrierCatalog.EnemiesPath,
            "enemiesData",
            issues);
        ValidateDetachedCombatRoot(
            roots,
            EffectCarrierCatalog.AlliesPath,
            "alliesData",
            issues);
        ValidateDetachedRegularQuests(roots, issues);

        return new ReadOnlyCollection<ValidationIssue>(issues.ToArray());
    }

    private void ValidateDetachedNpcCore(
        IReadOnlyDictionary<string, JsonObject> roots,
        List<ValidationIssue> issues)
    {
        if (!roots.TryGetValue(MortalWoundTreatmentNpcCorePath, out var root))
        {
            AddDetachedRootMissingIssue(MortalWoundTreatmentNpcCorePath, issues);
            return;
        }

        using var document = JsonDocument.Parse(root.ToJsonString());
        var element = document.RootElement;
        ValidateCompanionManifestationNpcSources(
            element,
            MortalWoundTreatmentNpcCorePath,
            issues);
        foreach (var section in GuardianPolicyContracts.NpcCoreCanonicalNpcObjectSections)
        {
            if (!TryGetExactDetachedCollection(
                    element,
                    MortalWoundTreatmentNpcCorePath,
                    section,
                    required: false,
                    issues,
                    out var rows))
            {
                continue;
            }

            var index = 0;
            foreach (var row in rows.EnumerateArray())
            {
                var context =
                    $"{MortalWoundTreatmentNpcCorePath}.{section}[{index++}]";
                if (!RequireObject(row, context, issues))
                    continue;
                ValidateFullNpcCoreObject(
                    row,
                    context,
                    issues,
                    section,
                    requiresCompletePersonality: false);
            }
        }
    }

    private void ValidateDetachedPlayerSkillRoot(
        IReadOnlyDictionary<string, JsonObject> roots,
        string path,
        string collection,
        List<ValidationIssue> issues)
    {
        if (!roots.TryGetValue(path, out var root))
        {
            AddDetachedRootMissingIssue(path, issues);
            return;
        }

        using var document = JsonDocument.Parse(root.ToJsonString());
        if (!TryGetExactDetachedCollection(
                document.RootElement,
                path,
                collection,
                required: true,
                issues,
                out _))
        {
            return;
        }

        ValidatePlayerSkillChanges(
            document.RootElement,
            path,
            issues,
            collection);
    }

    private void ValidateDetachedCombatRoot(
        IReadOnlyDictionary<string, JsonObject> roots,
        string path,
        string collection,
        List<ValidationIssue> issues)
    {
        if (!roots.TryGetValue(path, out var root))
            return;

        using var document = JsonDocument.Parse(root.ToJsonString());
        if (!TryGetExactDetachedCollection(
                document.RootElement,
                path,
                collection,
                required: true,
                issues,
                out _))
        {
            return;
        }

        ValidateCombatantArray(
            document.RootElement,
            path,
            issues,
            collection);
    }

    private void ValidateDetachedPlayerSkillMastery(
        IReadOnlyDictionary<string, JsonObject> roots,
        List<ValidationIssue> issues)
    {
        if (!roots.TryGetValue(MortalWoundTreatmentPlayerSkillMasteryPath, out var root))
        {
            AddDetachedRootMissingIssue(MortalWoundTreatmentPlayerSkillMasteryPath, issues);
            return;
        }

        if (!roots.TryGetValue(MortalWoundTreatmentPlayerActiveSkillsPath, out var activeRoot))
            return;

        using var masteryDocument = JsonDocument.Parse(root.ToJsonString());
        if (!TryGetExactDetachedCollection(
                masteryDocument.RootElement,
                MortalWoundTreatmentPlayerSkillMasteryPath,
                "skillMasteryChanges",
                required: true,
                issues,
                out _))
        {
            return;
        }

        using var activeDocument = JsonDocument.Parse(activeRoot.ToJsonString());
        if (!TryGetExactDetachedCollection(
                activeDocument.RootElement,
                MortalWoundTreatmentPlayerActiveSkillsPath,
                "activeSkillChanges",
                required: true,
                issues,
                out var activeRows))
        {
            return;
        }

        var knownActiveSkills = activeRows.EnumerateArray()
            .Where(static row => row.ValueKind == JsonValueKind.Object)
            .Select(static row => GetFirstNonEmptyString(row, "skillName"))
            .Where(static name => !string.IsNullOrWhiteSpace(name))
            .Select(static name => name!)
            .ToHashSet(StringComparer.OrdinalIgnoreCase);
        ValidatePlayerSkillMasteryCore(
            masteryDocument.RootElement,
            MortalWoundTreatmentPlayerSkillMasteryPath,
            issues,
            knownActiveSkills);
    }

    private void ValidateDetachedRegularQuests(
        IReadOnlyDictionary<string, JsonObject> roots,
        List<ValidationIssue> issues)
    {
        if (!roots.TryGetValue(MortalWoundTreatmentRegularQuestsPath, out var root))
            return;

        using var document = JsonDocument.Parse(root.ToJsonString());
        if (!TryGetExactDetachedCollection(
                document.RootElement,
                MortalWoundTreatmentRegularQuestsPath,
                "quests",
                required: true,
                issues,
                out _))
        {
            return;
        }

        ValidateQuestArray(
            document.RootElement,
            MortalWoundTreatmentRegularQuestsPath,
            issues,
            "quests");
    }

    private static bool TryGetExactDetachedCollection(
        JsonElement root,
        string path,
        string collection,
        bool required,
        List<ValidationIssue> issues,
        out JsonElement rows)
    {
        rows = default;
        var candidates = root.EnumerateObject()
            .Where(property => string.Equals(
                property.Name,
                collection,
                StringComparison.OrdinalIgnoreCase))
            .ToArray();
        if (candidates.Length == 0)
        {
            if (required)
            {
                issues.Add(new ValidationIssue(
                    $"{path}.{collection}",
                    IssueSeverity.Error,
                    "Detached accepted-state source is missing its canonical collection.",
                    code: "mortal_wound_treatment_detached_collection_missing",
                    section: "WoundMaterialization",
                    expected: $"exact {collection} JSON array",
                    actual: "missing",
                    repairHint:
                    "Rebuild the signed source root with the exact canonical collection name."));
            }
            return false;
        }

        if (candidates.Length != 1 ||
            !string.Equals(candidates[0].Name, collection, StringComparison.Ordinal))
        {
            issues.Add(new ValidationIssue(
                path,
                IssueSeverity.Error,
                "Detached accepted-state source has a case-variant or ambiguous collection name.",
                code: "mortal_wound_treatment_detached_collection_name_invalid",
                section: "WoundMaterialization",
                expected: $"one exact {collection} property",
                actual: string.Join(",", candidates.Select(static candidate => candidate.Name)),
                repairHint:
                "Use only the exact canonical collection spelling; remove case/confusable aliases."));
            return false;
        }

        return TryGetArray(
            root,
            collection,
            $"{path}.{collection}",
            issues,
            out rows);
    }

    private static void AddDetachedRootMissingIssue(
        string path,
        ICollection<ValidationIssue> issues) =>
        issues.Add(new ValidationIssue(
            path,
            IssueSeverity.Error,
            "Required detached accepted-state source root is missing.",
            code: "mortal_wound_treatment_detached_root_missing",
            section: "WoundMaterialization",
            expected: "detached signed canonical root",
            actual: "missing",
            repairHint: "Rebuild the pending-turn snapshot with all required source roots."));
}
