using System.Text.Json.Nodes;

namespace BookOfEternityClient.Services;

/// <summary>Pure consequence validation over detached original and accepted candidate DATA.</summary>
internal static class MortalNpcProgressionConsequences
{
    internal sealed record Problem(string Code, string Field);
    internal sealed record ProfileConsequences(bool Progresses, int LevelsPerAward = 2);
    internal enum CharacteristicMode { None, FixedIncrements, AllocatedPoints }
    internal sealed record CharacteristicPolicy(CharacteristicMode Mode,
        IReadOnlyDictionary<string, int>? PerLevel = null, int PointsPerLevel = 0,
        IReadOnlySet<string>? AllocationKeys = null);

    internal static IReadOnlyList<Problem> Evaluate(JsonObject originalActor, JsonObject candidateActor,
        JsonArray? skillAwards, ProfileConsequences profile, CharacteristicPolicy worldPolicy,
        IReadOnlySet<string> allowedKeys)
    {
        var issues = new List<Problem>();
        if (!GuardianPolicyContracts.TryResolveStrictPermanentNpcId(originalActor, out var originalId) ||
            !GuardianPolicyContracts.TryResolveStrictPermanentNpcId(candidateActor, out var candidateId) ||
            Exact(originalActor["NPCId"]) != originalId || Exact(candidateActor["NPCId"]) != candidateId || originalId != candidateId)
            return [new("npc_progression_consequence_owner", "NPCId")];
        if (!Integer(originalActor["level"], out var before) || !Integer(candidateActor["level"], out var after) ||
            before < 0 || after < before || (profile.Progresses && (before < 1 || profile.LevelsPerAward != 2)))
            return [new("npc_progression_consequence_profile", "level")];
        var gained = after - before;
        var required = profile.Progresses ? gained / 2 : 0;
        if ((!profile.Progresses && gained != 0) || (skillAwards?.Count ?? 0) != required)
            issues.Add(new("npc_progression_skill_award_count", "skillAwards"));

        // Catalogs are projections of profiles already validated by their existing owners.
        // Read each once; these local indexes are not retained/cached or publication authority.
        var originals = Catalog(originalActor, issues);
        var candidates = Catalog(candidateActor, issues);
        var used = new Dictionary<(string Kind, string Name, string Award), int>();
        foreach (var node in skillAwards ?? [])
        {
            if (node is not JsonObject row || row.Any(p => p.Key is not ("kind" or "skillName" or "skillId" or "award")) ||
                Exact(row["kind"]) is not ("active" or "passive") || Exact(row["skillName"]) is null ||
                Exact(row["award"]) is not ("newSkill" or "masteryIncrease") ||
                (row.ContainsKey("skillId") && Exact(row["skillId"]) is null))
            {
                issues.Add(new("npc_progression_skill_award_invalid", "skillAwards"));
                continue;
            }
            var kind = Exact(row["kind"])!; var name = Exact(row["skillName"])!;
            var award = Exact(row["award"])!; var id = Exact(row["skillId"]);
            var key = (kind, name);
            originals.TryGetValue(key, out var prior);
            candidates.TryGetValue(key, out var final);
            var budget = 0;
            if (final is not null && (id is null || id == final.Id))
            {
                if (award == "newSkill" && prior is null &&
                    (final.Id is null || !originals.Any(p => p.Key.Kind == kind && p.Value.Id == final.Id))) budget = 1;
                if (award == "masteryIncrease" && prior is not null && prior.Id == final.Id &&
                    final.Mastery >= prior.Mastery) budget = final.Mastery - prior.Mastery;
            }
            var claim = (kind, name, award);
            used.TryGetValue(claim, out var consumed);
            used[claim] = consumed + 1;
            if (consumed >= budget)
                issues.Add(new("npc_progression_skill_award_unfulfilled", "skillAwards." + name));
        }
        ValidateCharacteristics(originalActor, candidateActor, gained, worldPolicy, allowedKeys, issues);
        return issues.ToArray();
    }

    private sealed record Skill(string? Id, int Mastery);
    private static Dictionary<(string Kind, string Name), Skill> Catalog(JsonObject actor, List<Problem> issues)
    {
        var result = new Dictionary<(string Kind, string Name), Skill>();
        foreach (var kind in new[] { "active", "passive" })
        {
            if (actor[kind + "Skills"] is not JsonArray rows)
            {
                issues.Add(new("npc_progression_skill_catalog_invalid", kind + "Skills"));
                continue;
            }
            var names = new HashSet<string>(StringComparer.Ordinal);
            var ids = new HashSet<string>(StringComparer.Ordinal);
            foreach (var node in rows)
            {
                if (node is not JsonObject row || Exact(row["skillName"]) is not { } name)
                {
                    issues.Add(new("npc_progression_skill_catalog_invalid", kind + "Skills"));
                    continue;
                }
                var id = Exact(row["skillId"]); var alias = Exact(row["id"]);
                var valid = (!row.ContainsKey("skillId") || id is not null) && (!row.ContainsKey("id") || alias is not null) &&
                    (id is null || alias is null || id == alias);
                id ??= alias;
                valid &= names.Add(MortalLocationIdentityState.BuildConfusableKey(name));
                if (id is not null) valid &= ids.Add(MortalLocationIdentityState.BuildConfusableKey(id));
                var hasLevel = Integer(row["masteryLevel"], out var mastery);
                var hasAlias = Integer(row["currentMasteryLevel"], out var aliasMastery);
                valid &= (hasLevel || hasAlias) && (!row.ContainsKey("masteryLevel") || hasLevel) &&
                    (!row.ContainsKey("currentMasteryLevel") || hasAlias) && (!hasLevel || !hasAlias || mastery == aliasMastery);
                if (!hasLevel) mastery = aliasMastery;
                valid &= mastery >= 1;
                if (!valid || !result.TryAdd((kind, name), new(id, mastery)))
                    issues.Add(new("npc_progression_skill_catalog_invalid", kind + "Skills." + name));
            }
        }
        return result;
    }

    private static void ValidateCharacteristics(JsonObject before, JsonObject after, int gained,
        CharacteristicPolicy policy, IReadOnlySet<string> allowed, List<Problem> issues)
    {
        void Invalid(string field) => issues.Add(new("npc_progression_characteristic_authority", field));
        var keys = new HashSet<string>(StringComparer.Ordinal);
        if (policy.Mode == CharacteristicMode.FixedIncrements)
        {
            if (policy.PerLevel is null || policy.PerLevel.Count == 0 || policy.PointsPerLevel != 0 || policy.AllocationKeys is not null)
                Invalid("characteristics.policy");
            foreach (var pair in policy.PerLevel ?? new Dictionary<string, int>())
            {
                if (!ValidKey(pair.Key) || !allowed.Any(key => key == pair.Key) || pair.Value <= 0 ||
                    (long)pair.Value * gained > int.MaxValue) Invalid(pair.Key);
                keys.Add(pair.Key);
            }
        }
        else if (policy.Mode == CharacteristicMode.AllocatedPoints)
        {
            if (policy.PerLevel is not null || policy.PointsPerLevel <= 0 || policy.AllocationKeys is null ||
                policy.AllocationKeys.Count == 0 || (long)policy.PointsPerLevel * gained > int.MaxValue) Invalid("characteristics.policy");
            foreach (var key in policy.AllocationKeys ?? new HashSet<string>())
            {
                if (!ValidKey(key) || !allowed.Any(allowedKey => allowedKey == key)) Invalid(key);
                keys.Add(key);
            }
        }
        else if (policy.Mode != CharacteristicMode.None || policy.PerLevel is not null ||
            policy.PointsPerLevel != 0 || policy.AllocationKeys is not null) Invalid("characteristics.policy");
        if (keys.Select(MortalLocationIdentityState.BuildConfusableKey).Distinct(StringComparer.Ordinal).Count() != keys.Count)
            Invalid("characteristics.keys");
        if (before["characteristics"] is not JsonObject oldValues || after["characteristics"] is not JsonObject newValues)
        {
            issues.Add(new("npc_progression_characteristic_delta", "characteristics"));
            return;
        }
        decimal allocated = 0;
        foreach (var key in oldValues.Select(p => p.Key).Concat(newValues.Select(p => p.Key)).Concat(keys).Distinct(StringComparer.Ordinal))
        {
            if (!Number(oldValues[key], out var oldValue) || !Number(newValues[key], out var newValue))
            {
                issues.Add(new("npc_progression_characteristic_delta", key));
                continue;
            }
            try
            {
                var delta = checked(newValue - oldValue);
                decimal expected = 0;
                if (policy.Mode == CharacteristicMode.FixedIncrements && policy.PerLevel is { } increments &&
                    increments.TryGetValue(key, out var increment)) expected = checked((decimal)increment * gained);
                if (policy.Mode == CharacteristicMode.AllocatedPoints && keys.Contains(key))
                {
                    if (delta < 0 || decimal.Truncate(delta) != delta) issues.Add(new("npc_progression_characteristic_delta", key));
                    allocated = checked(allocated + delta);
                }
                else if (delta != expected) issues.Add(new("npc_progression_characteristic_delta", key));
            }
            catch (OverflowException) { issues.Add(new("npc_progression_characteristic_delta", key)); }
        }
        if (policy.Mode == CharacteristicMode.AllocatedPoints && allocated != (decimal)policy.PointsPerLevel * gained)
            issues.Add(new("npc_progression_characteristic_delta", "characteristics.total"));
    }

    private static string? Exact(JsonNode? node) => node is JsonValue value && value.TryGetValue<string>(out var text) &&
        ValidKey(text) ? text : null;
    private static bool ValidKey(string text) => !string.IsNullOrWhiteSpace(text) && text == text.Trim();
    private static bool Integer(JsonNode? node, out int result)
    {
        result = 0;
        return node is JsonValue value && value.TryGetValue(out result);
    }
    private static bool Number(JsonNode? node, out decimal result)
    {
        result = 0;
        if (node is not JsonValue value) return false;
        if (value.TryGetValue<decimal>(out result)) return true;
        if (value.TryGetValue<int>(out var integer)) { result = integer; return true; }
        if (value.TryGetValue<long>(out var wide)) { result = wide; return true; }
        return false;
    }
}
