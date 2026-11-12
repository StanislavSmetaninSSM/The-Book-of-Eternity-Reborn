using System.Text.Json;

namespace BookOfEternityClient.Services;

public partial class ValidationService
{
    /// <summary>
    /// Current owner/kind skill names for one NpcStateFiles call. Reuses parsed roots,
    /// retains no JSON trees or validation success, and never reads pre-turn snapshots.
    /// </summary>
    internal sealed class NpcSkillMasteryOwnership
    {
        private readonly Dictionary<string, HashSet<string>> _active = new(StringComparer.OrdinalIgnoreCase);
        private readonly Dictionary<string, HashSet<string>> _passive = new(StringComparer.OrdinalIgnoreCase);

        /// <summary>Collects the two canonical current core carriers while their parsed root is alive.</summary>
        /// <param name="root">Current npc_core root; malformed rows are left to shape validators.</param>
        internal void CollectCore(JsonElement root)
        {
            foreach (var actor in GuardianPolicyContracts.EnumerateCanonicalNpcObjects(root))
            {
                var owner = BuildNpcIdentityKey(actor);
                AddSkills(actor, "activeSkills", owner, _active);
                AddSkills(actor, "passiveSkills", owner, _passive);
            }
        }

        /// <summary>Adds same-turn profiles and checks indexed mastery against current owner-local authority.</summary>
        /// <param name="root">Already parsed current npc_skills root.</param>
        /// <param name="path">Canonical diagnostic file path.</param>
        /// <param name="issues">This invocation's findings; other diagnostics remain independent.</param>
        internal void ValidateSkills(JsonElement root, string path, List<ValidationIssue> issues)
        {
            if (root.ValueKind != JsonValueKind.Object) return;
            CollectChanges(root, "NPCActiveSkillChanges", _active);
            CollectChanges(root, "NPCPassiveSkillChanges", _passive);
            ValidateMastery(root, path, "NPCSkillMasteryChanges", "NPCActiveSkillChanges", "activeSkills", _active, issues);
            ValidateMastery(root, path, "NPCPassiveSkillMasteryChanges", "NPCPassiveSkillChanges", "passiveSkills", _passive, issues);
        }

        private static void CollectChanges(JsonElement root, string section, Dictionary<string, HashSet<string>> names)
        {
            if (!root.TryGetProperty(section, out var groups) || groups.ValueKind != JsonValueKind.Array) return;
            foreach (var group in groups.EnumerateArray())
            {
                if (group.ValueKind == JsonValueKind.Object)
                    AddSkills(group, "skillChanges", BuildNpcIdentityKey(group), names);
            }
        }

        private static void AddSkills(JsonElement container, string property, string? owner,
            Dictionary<string, HashSet<string>> names)
        {
            if (owner == null || !container.TryGetProperty(property, out var skills) || skills.ValueKind != JsonValueKind.Array)
                return;
            foreach (var skill in skills.EnumerateArray())
            {
                if (skill.ValueKind != JsonValueKind.Object) continue;
                var name = GetFirstNonEmptyString(skill, "skillName");
                if (name == null) continue;
                if (!names.TryGetValue(owner, out var ownedNames))
                    names.Add(owner, ownedNames = new HashSet<string>(StringComparer.Ordinal));
                ownedNames.Add(name);
            }
        }

        private static void ValidateMastery(JsonElement root, string path, string section, string changesSection,
            string coreSkills, Dictionary<string, HashSet<string>> names, List<ValidationIssue> issues)
        {
            if (!root.TryGetProperty(section, out var rows) || rows.ValueKind != JsonValueKind.Array) return;
            var index = 0;
            foreach (var row in rows.EnumerateArray())
            {
                var rowIndex = index++;
                if (row.ValueKind != JsonValueKind.Object) continue;
                var owner = BuildNpcIdentityKey(row);
                var name = GetFirstNonEmptyString(row, "skillName");
                if (owner == null || name == null ||
                    (names.TryGetValue(owner, out var ownedNames) && ownedNames.Contains(name))) continue;
                issues.Add(new ValidationIssue(
                    $"{path}.{section}[{rowIndex}].skillName", IssueSeverity.Error,
                    "Mastery ссылается на навык, которого нет у этого NPC в текущем состоянии",
                    code: "npc_skill_mastery_unknown_owned_skill", actor: owner,
                    section: coreSkills == "activeSkills" ? "Skills.Active" : "Skills.Passive",
                    expected: $"exact skillName for {owner} in current npc_core.json UpdateNPCs/NPCsInScene.{coreSkills} " +
                        $"or npc_skills.json {changesSection}.skillChanges", actual: name,
                    repairHint: "Сверь текущий навык этого NPC и подтверждённый источник прогрессии; не меняй владельца, " +
                        "не создавай навык, не удаляй прогрессию и не выбирай похожее имя автоматически. " +
                        "Если источник не задаёт однозначное исправление, запроси уточнение."));
            }
        }
    }
}
