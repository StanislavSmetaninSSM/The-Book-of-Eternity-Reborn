using System.Text.Json;
using BookOfEternityClient.Core;

namespace BookOfEternityClient.Services;

public partial class ValidationService
{
    /// <summary>Current composed player active-skill source.</summary>
    internal const string CanonicalActiveSkillsPath = "game_state/player/skills_active.json";
    /// <summary>Current player mastery-reference source.</summary>
    internal const string CanonicalSkillMasteryPath = "game_state/player/skill_mastery.json";

    /// <summary>Checks the actual composed pair while the caller retains its canonical write lease.</summary>
    /// <param name="writeLease">Existing post-normalization publication lease, not acquired or released here.</param>
    /// <returns>Current-only closure findings, without consulting snapshots or mutating state.</returns>
    internal async Task<IReadOnlyList<ValidationIssue>> ValidateAcceptedTurnCanonicalSkillMasteryAsync(
        FileSystemManager.CanonicalWriteLease writeLease)
    {
        ArgumentNullException.ThrowIfNull(writeLease);
        return ValidateCanonicalPlayerSkillMastery(
            await _fs.ReadFileAsync(writeLease, CanonicalActiveSkillsPath),
            await _fs.ReadFileAsync(writeLease, CanonicalSkillMasteryPath));
    }

    /// <summary>Validates exact mastery closure against current composed active skills only.</summary>
    /// <param name="activeJson">Current active catalog, or null for an absent pristine catalog.</param>
    /// <param name="masteryJson">Current mastery state, or null when absent.</param>
    /// <returns>Shape, mastery-field and exact/confusable findings with existing repair diagnostics.</returns>
    internal static IReadOnlyList<ValidationIssue> ValidateCanonicalPlayerSkillMastery(
        string? activeJson, string? masteryJson)
    {
        var issues = new List<ValidationIssue>();
        using var active = ParseCanonicalSkillRoot(activeJson, CanonicalActiveSkillsPath, issues);
        using var mastery = ParseCanonicalSkillRoot(masteryJson, CanonicalSkillMasteryPath, issues);
        var names = new HashSet<string>(StringComparer.Ordinal);
        if (active is not null && TryGetArray(active.RootElement, "activeSkillChanges",
                CanonicalActiveSkillsPath + ".activeSkillChanges", issues, out var skills))
        {
            var index = 0;
            foreach (var item in skills.EnumerateArray())
            {
                var path = $"{CanonicalActiveSkillsPath}.activeSkillChanges[{index++}]";
                if (!RequireObject(item, path, issues)) continue;
                var name = RequireString(item, path, issues, "skillName");
                if (!string.IsNullOrWhiteSpace(name)) names.Add(name.Trim());
            }
        }
        if (mastery is not null)
        {
            ValidatePlayerSkillMasteryCore(mastery.RootElement, CanonicalSkillMasteryPath, issues, names);
            ValidatePlayerMasterySkillReferenceIdentityCore(mastery.RootElement, CanonicalSkillMasteryPath, names, issues);
        }
        return issues;
    }

    private static JsonDocument? ParseCanonicalSkillRoot(string? json, string path, List<ValidationIssue> issues)
    {
        if (json is null) return null;
        JsonDocument document;
        try { document = JsonDocument.Parse(json); }
        catch (JsonException)
        {
            issues.Add(new ValidationIssue(path, IssueSeverity.Error, "Canonical skill state must be valid JSON",
                code: "invalid_json_file", section: "Skills.Active", expected: "JSON object", actual: "invalid JSON"));
            return null;
        }
        if (RequireObject(document.RootElement, path, issues)) return document;
        document.Dispose();
        return null;
    }
}
