using System.Text.Json;
using System.Text.Json.Nodes;

namespace BookOfEternityClient.Services;

/// <summary>
/// Pure ordinary NPC candidate DATA. Callers authenticate originals/current inputs and
/// retain responsibility for realm/source fences, leases, publication and rollback.
/// </summary>
internal static class MortalNpcAcceptedSkillCandidate
{
    private const string CorePath = NpcCoreChangesContract.NpcCorePath;
    private const string SidecarPath = "game_state/npcs/npc_skills.json";
    private static readonly string[] PendingProperties =
    ["NPCActiveSkillChanges", "NPCPassiveSkillChanges", "NPCSkillMasteryChanges", "NPCPassiveSkillMasteryChanges"];

    internal sealed record Result(JsonObject? Core, JsonObject? Sidecar, IReadOnlyList<ValidationIssue> Issues)
    {
        internal bool IsValid => Core is not null && Sidecar is not null && Issues.Count == 0;
    }

    /// <summary>
    /// Reduces current bounded core and skill commands against an already authenticated
    /// clean original. No source authentication, I/O, publication or cached authority occurs here.
    /// Returned roots and diagnostics belong exclusively to this invocation.
    /// </summary>
    internal static Result Compose(
        JsonObject originalCore,
        JsonObject originalSidecar,
        JsonObject currentCore,
        JsonObject currentSidecar,
        NpcCoreChangesContract.Authority authority,
        MortalActorAcceptedTurnAuthority? acceptedTurnAuthority = null)
    {
        ArgumentNullException.ThrowIfNull(originalCore);
        ArgumentNullException.ThrowIfNull(originalSidecar);
        ArgumentNullException.ThrowIfNull(currentCore);
        ArgumentNullException.ThrowIfNull(currentSidecar);
        ArgumentNullException.ThrowIfNull(authority);
        var issues = new List<ValidationIssue>();
        foreach (var property in PendingProperties)
        {
            if (originalSidecar.TryGetPropertyValue(property, out var old) &&
                (old is not JsonArray rows || rows.Count != 0))
                issues.Add(Issue(SidecarPath + "." + property, "npc_skill_candidate_original_pending",
                    "absent or empty pending array in authenticated original", "retained or malformed pending commands"));
        }
        ValidatePropertyCasing(originalSidecar, issues);
        ValidatePropertyCasing(currentSidecar, issues);
        if (issues.Count != 0) return Refuse(issues);

        // Direct mutation checks precede any composer-owned changes. Never compare
        // raw authored core to an already patched baseline to excuse a bypass.
        var core = currentCore.DeepClone().AsObject();
        var coreEvaluation = NpcCoreChangesContract.Evaluate(core, originalCore, authority,
            ValidationService.ValidateNpcCoreFateCardsAgainstProductionContract, detectDirectMutations: true, acceptedTurnAuthority);
        issues.AddRange(coreEvaluation.Issues);
        if (issues.Count != 0) return Refuse(issues);
        NpcCoreChangesContract.Apply(core, coreEvaluation);

        using var document = JsonDocument.Parse(currentSidecar.ToJsonString());
        foreach (var property in PendingProperties.Where(currentSidecar.ContainsKey))
        {
            if (property is "NPCActiveSkillChanges" or "NPCPassiveSkillChanges")
                ValidationService.ValidateNpcSkillChanges(document.RootElement, SidecarPath, issues, property);
            else
                ValidationService.ValidateNpcSkillMastery(document.RootElement, SidecarPath, issues, property,
                    property == "NPCPassiveSkillMasteryChanges");
        }
        if (issues.Count != 0) return Refuse(issues);

        var active = ReadProfiles(currentSidecar, PendingProperties[0], issues);
        var passive = ReadProfiles(currentSidecar, PendingProperties[1], issues);
        var activeMastery = ReadMastery(currentSidecar, PendingProperties[2], passive: false, issues);
        var passiveMastery = ReadMastery(currentSidecar, PendingProperties[3], passive: true, issues);
        if (issues.Count != 0) return Refuse(issues);
        var composed = MortalNpcSkillComposer.ComposeWithMastery(core, active, passive,
            activeMastery, passiveMastery, issues, Issue);
        if (composed is null || issues.Count != 0) return Refuse(issues);
        issues.AddRange(EffectAcceptedTurnInputComposer.ValidateAcceptedSkillComposition(
            new Dictionary<string, JsonNode?> { [CorePath] = originalCore },
            new Dictionary<string, JsonNode?> { [CorePath] = composed }));
        if (issues.Count != 0) return Refuse(issues);
        var sidecar = currentSidecar.DeepClone().AsObject();
        foreach (var property in PendingProperties) sidecar.Remove(property);
        return new Result(composed, sidecar, issues.ToArray());
    }

    private static IReadOnlyList<MortalNpcSkillComposer.Command> ReadProfiles(
        JsonObject sidecar, string property, List<ValidationIssue> issues)
    {
        var result = new List<MortalNpcSkillComposer.Command>();
        var seen = new HashSet<string>(StringComparer.Ordinal);
        foreach (var (row, path) in Rows(sidecar, property, issues))
        {
            var start = issues.Count;
            var owner = ReadOwner(row, path, issues);
            Closed(row, path, ["NPCId", "npcId", "id", "NPCName", "skillChanges", "skillsToRemove"], issues);
            ValidateDisplayMetadata(row, path, issues);
            if (owner is not null && !seen.Add(MortalLocationIdentityState.BuildConfusableKey(owner)))
                issues.Add(Issue(path, "npc_skill_candidate_duplicate_owner", "one exact/confusable-unique owner per kind", owner));
            if (!row.ContainsKey("skillChanges") && !row.ContainsKey("skillsToRemove"))
                issues.Add(Issue(path, "npc_skill_candidate_empty_command", "skillChanges or skillsToRemove", "missing"));
            var changes = ReadArray(row, "skillChanges", path, issues);
            var removals = ReadArray(row, "skillsToRemove", path, issues);
            if (changes is not null)
            {
                var stableIds = new HashSet<string>(StringComparer.Ordinal);
                for (var index = 0; index < changes.Count; index++)
                {
                    var skillPath = $"{path}.skillChanges[{index}]";
                    if (changes[index] is not JsonObject profile || ExactText(profile["skillName"]) is null)
                    {
                        issues.Add(Issue(skillPath, "npc_skill_candidate_invalid_skill", "exact canonical skillName", "invalid"));
                        continue;
                    }
                    // Validate authored identities before the shared reducer can upsert
                    // two differently named profiles onto the same stable identity.
                    string? stableId = null;
                    foreach (var alias in new[] { "skillId", "id" })
                    {
                        if (!profile.ContainsKey(alias)) continue;
                        var value = ExactText(profile[alias]);
                        if (value is null || (stableId is not null && !string.Equals(stableId, value, StringComparison.Ordinal)))
                            issues.Add(Issue(skillPath + "." + alias, "npc_skill_candidate_invalid_skill_id",
                                "exact agreeing skillId/id aliases when present", "invalid"));
                        else stableId = value;
                    }
                    if (stableId is not null && !stableIds.Add(MortalLocationIdentityState.BuildConfusableKey(stableId)))
                        issues.Add(Issue(skillPath, "npc_skill_candidate_duplicate_skill_id",
                            "one exact/confusable-unique authored stable skill identity per owner/kind", stableId));
                }
            }
            if (removals is not null)
            {
                var names = new HashSet<string>(StringComparer.Ordinal);
                foreach (var removal in removals)
                {
                    var name = ExactText(removal);
                    if (name is null || !names.Add(MortalLocationIdentityState.BuildConfusableKey(name)))
                        issues.Add(Issue(path + ".skillsToRemove", "npc_skill_candidate_invalid_removal", "exact/confusable-unique selectors", "invalid"));
                }
            }
            if (issues.Count == start && owner is not null)
                result.Add(new(owner, changes?.DeepClone().AsArray(), removals?.DeepClone().AsArray()));
        }
        return result;
    }

    private static IReadOnlyList<MortalNpcSkillComposer.MasteryCommand> ReadMastery(
        JsonObject sidecar, string property, bool passive, List<ValidationIssue> issues)
    {
        var result = new List<MortalNpcSkillComposer.MasteryCommand>();
        foreach (var (row, path) in Rows(sidecar, property, issues))
        {
            var start = issues.Count;
            var owner = ReadOwner(row, path, issues);
            Closed(row, path, passive
                ? ["NPCId", "npcId", "id", "NPCName", "skillName", "skillId", "newMasteryLevel", "newMaxMasteryLevel", "masteryLeveledUp"]
                : ["NPCId", "npcId", "id", "NPCName", "skillName", "skillId", "newMasteryLevel", "newCurrentMasteryProgress", "newMasteryProgressNeeded", "masteryLeveledUp"], issues);
            ValidateDisplayMetadata(row, path, issues);
            if (row.ContainsKey("masteryLeveledUp") &&
                (row["masteryLeveledUp"] is not JsonValue flag || !flag.TryGetValue<bool>(out _)))
                issues.Add(Issue(path + ".masteryLeveledUp", "npc_skill_candidate_invalid_metadata", "JSON Boolean", "invalid"));
            var name = ExactText(row["skillName"]);
            var id = ExactText(row["skillId"]);
            if (name is null || (row.ContainsKey("skillId") && id is null))
                issues.Add(Issue(path, "npc_skill_candidate_invalid_skill", "exact skillName and optional exact skillId", "invalid"));
            var level = Integer(row, "newMasteryLevel", path, required: true, issues);
            var progress = passive ? null : Integer(row, "newCurrentMasteryProgress", path, required: true, issues);
            var needed = passive ? null : Integer(row, "newMasteryProgressNeeded", path, required: true, issues);
            var max = passive ? Integer(row, "newMaxMasteryLevel", path, required: false, issues) : null;
            if (issues.Count == start && owner is not null && name is not null && level.HasValue)
                result.Add(new(owner, name, id, level.Value, progress, needed, max));
        }
        return result;
    }

    private static IEnumerable<(JsonObject Row, string Path)> Rows(JsonObject root, string property, List<ValidationIssue> issues)
    {
        if (!root.ContainsKey(property)) yield break;
        if (root[property] is not JsonArray rows)
        {
            issues.Add(Issue(SidecarPath + "." + property, "npc_skill_candidate_invalid_array", "JSON array", "invalid"));
            yield break;
        }
        for (var i = 0; i < rows.Count; i++)
        {
            var path = $"{SidecarPath}.{property}[{i}]";
            if (rows[i] is JsonObject row) yield return (row, path);
            else issues.Add(Issue(path, "npc_skill_candidate_invalid_row", "object", "invalid"));
        }
    }

    private static string? ReadOwner(JsonObject row, string path, List<ValidationIssue> issues)
    {
        if (GuardianPolicyContracts.TryResolveStrictPermanentNpcId(row, out var id) && ExactText(JsonValue.Create(id)) is not null)
            return id;
        issues.Add(Issue(path, "npc_skill_candidate_invalid_owner", "one exact permanent NPC identity with agreeing aliases", "invalid"));
        return null;
    }

    private static JsonArray? ReadArray(JsonObject row, string key, string path, List<ValidationIssue> issues)
    {
        if (!row.ContainsKey(key)) return null;
        if (row[key] is JsonArray array) return array;
        issues.Add(Issue(path + "." + key, "npc_skill_candidate_invalid_array", "JSON array", "invalid"));
        return null;
    }

    private static int? Integer(JsonObject row, string key, string path, bool required, List<ValidationIssue> issues)
    {
        if (!required && !row.ContainsKey(key)) return null;
        if (row[key] is JsonValue value && value.TryGetValue<int>(out var number)) return number;
        issues.Add(Issue(path + "." + key, "npc_skill_candidate_invalid_integer", "Int32 JSON integer", "invalid"));
        return null;
    }

    private static string? ExactText(JsonNode? node) => node is JsonValue value && value.TryGetValue<string>(out var text) &&
        !string.IsNullOrWhiteSpace(text) && string.Equals(text, text.Trim(), StringComparison.Ordinal) ? text : null;

    private static void Closed(JsonObject row, string path, string[] allowed, List<ValidationIssue> issues)
    {
        foreach (var key in row.Select(pair => pair.Key).Where(key => !allowed.Contains(key, StringComparer.Ordinal)))
            issues.Add(Issue(path + "." + key, "npc_skill_candidate_unknown_field", "supported command field", key));
    }

    // NPCName is optional display metadata. It cannot select, rename or authorize
    // an actor; ReadOwner still requires an exact permanent identity.
    private static void ValidateDisplayMetadata(JsonObject row, string path, List<ValidationIssue> issues)
    {
        if (row.ContainsKey("NPCName") &&
            (row["NPCName"] is not JsonValue value || !value.TryGetValue<string>(out var name) || string.IsNullOrWhiteSpace(name)))
            issues.Add(Issue(path + ".NPCName", "npc_skill_candidate_invalid_metadata", "nonempty display string", "invalid"));
    }

    private static void ValidatePropertyCasing(JsonObject sidecar, List<ValidationIssue> issues)
    {
        foreach (var key in sidecar.Select(pair => pair.Key))
            if (PendingProperties.Any(expected => string.Equals(expected, key, StringComparison.OrdinalIgnoreCase)) &&
                !PendingProperties.Contains(key, StringComparer.Ordinal))
                issues.Add(Issue(SidecarPath + "." + key, "npc_skill_candidate_invalid_property_case", "exact pending property name", key));
    }

    private static Result Refuse(List<ValidationIssue> issues) => new(null, null, issues.ToArray());
    private static ValidationIssue Issue(string path, string code, string expected, string actual) =>
        new(path, IssueSeverity.Error, "Ordinary NPC skill candidate rejected.", code: code,
            section: "NPCSkills", expected: expected, actual: actual);
}

/// <summary>Detached invocation DATA, not a lease, admission token, registry or writer.</summary>
internal sealed class MortalNpcAcceptedSkillInput
{
    internal const string SidecarPath = "game_state/npcs/npc_skills.json";
    private readonly JsonObject[] _roots;
    internal JsonObject OriginalCore => _roots[0].DeepClone().AsObject();
    internal JsonObject OriginalSidecar => _roots[1].DeepClone().AsObject();
    internal JsonObject RawCore => _roots[2].DeepClone().AsObject();
    internal JsonObject RawSidecar => _roots[3].DeepClone().AsObject();
    internal JsonObject CandidateCore => _roots[4].DeepClone().AsObject();
    internal JsonObject ConsumedSidecar => _roots[5].DeepClone().AsObject();
    internal string Fingerprint { get; }
    internal string AuthorityFingerprint { get; }

    private MortalNpcAcceptedSkillInput(JsonObject[] roots, NpcCoreChangesContract.Authority authority,
        string? tradePending, string? trainingPending)
    {
        _roots = roots.Select(root => root.DeepClone().AsObject()).ToArray();
        AuthorityFingerprint = ComputeAuthorityFingerprint(authority);
        Fingerprint = WoundAcceptedTurnFingerprintWriter.Compute(
            new string?[] { "mortal_npc_accepted_skill_input:v1", tradePending, trainingPending, AuthorityFingerprint }
                .Concat(_roots.Select(WoundAcceptedTurnFingerprintWriter.CanonicalJson)).ToArray());
    }

    internal static string ComputeAuthorityFingerprint(NpcCoreChangesContract.Authority authority)
    {
        var authorityJson = new JsonObject
        {
            ["locations"] = System.Text.Json.JsonSerializer.SerializeToNode(authority.KnownPermanentLocationIds.OrderBy(x => x, StringComparer.Ordinal)),
            ["initialLocations"] = System.Text.Json.JsonSerializer.SerializeToNode(authority.SameTurnLocationInitialIds.OrderBy(x => x, StringComparer.Ordinal)),
            ["factions"] = System.Text.Json.JsonSerializer.SerializeToNode(authority.FactionNamesById.OrderBy(x => x.Key, StringComparer.Ordinal)),
            ["characteristics"] = System.Text.Json.JsonSerializer.SerializeToNode(authority.WorldCharacteristicKeys.OrderBy(x => x, StringComparer.Ordinal)),
            ["source"] = System.Text.Json.JsonSerializer.SerializeToNode(authority.ProgressionSource)
        };
        return WoundAcceptedTurnFingerprintWriter.CanonicalJson(authorityJson);
    }

    internal static MortalNpcAcceptedSkillInput? Create(JsonObject originalCore, JsonObject originalSidecar,
        JsonObject rawCore, JsonObject rawSidecar, NpcCoreChangesContract.Authority authority,
        string? originalTradePendingJson, string? originalTrainingPendingJson, out IReadOnlyList<ValidationIssue> issues)
    {
        var accepted = MortalActorAcceptedTurnAuthority.Create(rawCore, originalTradePendingJson, originalTrainingPendingJson);
        var result = MortalNpcAcceptedSkillCandidate.Compose(originalCore, originalSidecar, rawCore, rawSidecar, authority, accepted);
        issues = result.Issues;
        return result.IsValid ? new([originalCore, originalSidecar, rawCore, rawSidecar, result.Core!, result.Sidecar!],
            authority, originalTradePendingJson, originalTrainingPendingJson) : null;
    }

    internal static bool HasPending(JsonObject root) => root.Any(pair =>
        new[] { "NPCActiveSkillChanges", "NPCPassiveSkillChanges", "NPCSkillMasteryChanges", "NPCPassiveSkillMasteryChanges" }
            .Contains(pair.Key, StringComparer.OrdinalIgnoreCase) && (pair.Value is not JsonArray array || array.Count != 0));
}
