using BookOfEternityClient.Services;

namespace BookOfEternityClient.Core;

public partial class GameEngine
{
    /// <summary>
    /// Determines whether a diagnostic belongs to skill-reference identity repair.
    /// </summary>
    /// <param name="issue">
    /// The diagnostic to classify.
    /// </param>
    /// <returns>
    /// <see langword="true"/> for one of the three identity codes; otherwise <see langword="false"/>.
    /// </returns>
    private static bool IsMortalSkillReferenceIdentityRepairIssue(ValidationIssue issue)
    {
        var code = issue.Code ?? string.Empty;
        return string.Equals(code, "skill_mastery_confusable_reference", StringComparison.OrdinalIgnoreCase) ||
               string.Equals(code, "player_active_skill_mastery_initialization_confusable", StringComparison.OrdinalIgnoreCase) ||
               string.Equals(code, "npc_skill_mastery_initialization_confusable", StringComparison.OrdinalIgnoreCase);
    }

    /// <summary>
    /// Reduces a skill-reference field coordinate to its containing file.
    /// </summary>
    /// <param name="path">
    /// The diagnostic file or field coordinate to normalize.
    /// </param>
    /// <returns>
    /// The containing known skill file, or the normally normalized path.
    /// </returns>
    private static string NormalizeSkillReferenceRepairTargetPath(string path)
    {
        var normalized = NormalizeRepairTargetPath(path);
        foreach (var skillFile in new[]
                 {
                     "game_state/player/skills_active.json",
                     "game_state/player/skill_mastery.json",
                     "game_state/npcs/npc_skills.json"
                 })
        {
            if (normalized.StartsWith(skillFile, StringComparison.OrdinalIgnoreCase))
                return skillFile;
        }

        return normalized;
    }

    /// <summary>
    /// Builds bounded non-destructive instructions for all supplied identity diagnostics.
    /// </summary>
    /// <param name="referenceIdentityErrors">
    /// The identity errors, retaining every actor and skill correction.
    /// </param>
    /// <returns>
    /// A packet preserving distinct corrections and refusing ambiguous rename targets.
    /// </returns>
    private static ValidationRepairHarnessPacket BuildMortalSkillReferenceIdentityRepairPacket(
        IReadOnlyList<ValidationIssue> referenceIdentityErrors)
    {
        var targetFiles = referenceIdentityErrors
            .Select(issue => NormalizeSkillReferenceRepairTargetPath(issue.FilePath))
            .Where(path => !string.IsNullOrWhiteSpace(path))
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .OrderBy(path => path, StringComparer.OrdinalIgnoreCase)
            .ToList();

        foreach (var path in new[]
                 {
                     "game_state/player/skills_active.json",
                     "game_state/player/skill_mastery.json",
                     "game_state/npcs/npc_skills.json"
                 })
        {
            if (!targetFiles.Contains(path, StringComparer.OrdinalIgnoreCase))
                targetFiles.Add(path);
        }

        var actorNames = CollectRepairActorNames(referenceIdentityErrors)
            .OrderBy(actor => actor, StringComparer.OrdinalIgnoreCase)
            .ToList();
        var exactFieldCorrections = referenceIdentityErrors
            .Select(issue =>
            {
                var correction = BuildExactFieldCorrection(issue);
                correction.RepairHint = issue.RepairHint;
                return correction;
            })
            .Where(correction => !string.IsNullOrWhiteSpace(correction.Path))
            .OrderBy(correction => correction.Path, StringComparer.OrdinalIgnoreCase)
            .ToList();

        return new ValidationRepairHarnessPacket
        {
            Kind = "mortal_skill_reference_identity_repair",
            Priority = "high",
            Title = "Mortal skill reference identity repair",
            TargetFiles = targetFiles,
            TemplateRefs = new List<string> { "Rules/Block_7.txt" },
            CanonicalActorNames = actorNames,
            ExactFieldCorrections = exactFieldCorrections,
            ExpectedShape = new List<string>
            {
                "Every skillMasteryChanges / NPCSkillMasteryChanges / NPCPassiveSkillMasteryChanges entry must reference the exact canonical skillName of the owning actor, character-for-character.",
                "The canonical skill names live in game_state/player/skills_active.json (player) and in the same-actor skill sections of game_state/npcs/npc_skills.json (NPCs).",
                "A case-variant or Unicode-confusable skillName never satisfies a mastery reference or a mastery initialization requirement."
            },
            SafeCorrectionRules = new List<string>
            {
                "The only allowed edit is rewriting the offending skillName to the exact canonical name from exactFieldCorrections; preserve masteryLevel, progress, and every other field byte-for-byte.",
                "If expected describes several distinct canonical names, stop: there is no uniquely proven rename target. The descriptive expected text is not a literal skillName; never guess or rewrite to that text.",
                "Keep mastery entries for all other skills untouched; do not reformat or reorder the file."
            },
            Steps = new List<string>
            {
                "Open validation_repair_request.json and read exactFieldCorrections for every skill reference identity issue.",
                "Open the target file and the canonical skill source (skills_active.json or npc_skills.json) side by side.",
                "For an unambiguous correction only, rewrite the indexed offending skillName to the exact canonical name listed as expected, character-for-character including case. Read repairHint for actor and ambiguity context.",
                "For initialization codes, verify the renamed entry now covers the new skill; if the confusable entry belonged to a different real skill, instead add a new mastery initialization entry under the exact new skill name.",
                "After repairs are complete, call Complete-BoeValidationRepair as the last action, or create validation_repair_ready.json with exact metadata from the current validation_repair_request.json."
            },
            DoNotDo = new List<string>
            {
                "Do not delete mastery entries to silence the error; mastery initialization is mandatory for newly learned skills.",
                "Do not invent new skills or rename canonical skills to match the mastery entry; the mastery entry is always the side that moves.",
                "Do not change mastery levels, progress values, or any other field while fixing identity.",
                "Do not read implementation code such as BookOfEternityClient/**/*.cs to infer skill naming rules; use this packet, Rules/Block_7.txt, and canonical session files."
            }
        };
    }
}
