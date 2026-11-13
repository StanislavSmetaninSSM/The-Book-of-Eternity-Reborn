using System.Text.Json.Nodes;
using System.Text.Json;
using System.Numerics;
using System.Globalization;

namespace BookOfEternityClient.Services;

/// <summary>Pure consequence validation over detached original and accepted candidate DATA.</summary>
internal static partial class MortalNpcProgressionConsequences
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
        var exactIncrements = new Dictionary<string, int>(StringComparer.Ordinal);
        if (policy.Mode == CharacteristicMode.FixedIncrements)
        {
            if (policy.PerLevel is null || policy.PerLevel.Count == 0 || policy.PointsPerLevel != 0 || policy.AllocationKeys is not null)
                Invalid("characteristics.policy");
            foreach (var pair in policy.PerLevel ?? new Dictionary<string, int>())
            {
                if (!ValidKey(pair.Key) || !allowed.Any(key => key == pair.Key) || pair.Value <= 0 ||
                    (long)pair.Value * gained > int.MaxValue) Invalid(pair.Key);
                keys.Add(pair.Key);
                if (!exactIncrements.TryAdd(pair.Key, pair.Value)) Invalid(pair.Key);
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
        BigInteger allocated = 0;
        foreach (var key in oldValues.Select(p => p.Key).Concat(newValues.Select(p => p.Key)).Concat(keys).Distinct(StringComparer.Ordinal))
        {
            if (!TryIntegerDelta(oldValues[key], newValues[key], out var delta))
            {
                issues.Add(new("npc_progression_characteristic_delta", key));
                continue;
            }
            BigInteger expected = 0;
            if (policy.Mode == CharacteristicMode.FixedIncrements && exactIncrements.TryGetValue(key, out var increment))
                expected = (BigInteger)increment * gained;
            if (policy.Mode == CharacteristicMode.AllocatedPoints && keys.Contains(key))
            {
                if (delta < 0) issues.Add(new("npc_progression_characteristic_delta", key));
                allocated += delta;
            }
            else if (delta != expected) issues.Add(new("npc_progression_characteristic_delta", key));
        }
        if (policy.Mode == CharacteristicMode.AllocatedPoints && allocated != (BigInteger)policy.PointsPerLevel * gained)
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
    // Integer point policies require exact integer differences, not decimal/double rounding.
    // JSON serialization handles typed numeric JsonValues; numeric text remains the authority.
    private static bool TryIntegerDelta(JsonNode? before, JsonNode? after, out BigInteger delta)
    {
        delta = 0;
        if (!NumericText(before, out var oldText) || !NumericText(after, out var newText)) return false;
        if (oldText == newText) return true; // Preserve any unchanged finite supported JSON value.
        if (!Rational(oldText, out var oldCoefficient, out var oldExponent) ||
            !Rational(newText, out var newCoefficient, out var newExponent)) return false;
        if (oldCoefficient == newCoefficient && oldExponent == newExponent) return true;
        var exponent = Math.Min(oldExponent, newExponent);
        var oldShift = oldExponent - exponent; var newShift = newExponent - exponent;
        // Bound expansion, never round. Unsupported changed ranges fail closed.
        if (oldShift > 1024 || newShift > 1024 || exponent is < -1024 or > 1024) return false;
        var coefficient = newCoefficient * BigInteger.Pow(10, (int)newShift) -
            oldCoefficient * BigInteger.Pow(10, (int)oldShift);
        if (exponent >= 0) delta = coefficient * BigInteger.Pow(10, (int)exponent);
        else
        {
            delta = BigInteger.DivRem(coefficient, BigInteger.Pow(10, (int)-exponent), out var remainder);
            if (!remainder.IsZero) return false;
        }
        return true;
    }

    private static bool NumericText(JsonNode? node, out string text)
    {
        text = "";
        if (node is not JsonValue) return false;
        try
        {
            text = node.ToJsonString();
            using var document = JsonDocument.Parse(text);
            return document.RootElement.ValueKind == JsonValueKind.Number &&
                document.RootElement.TryGetDouble(out var finite) && double.IsFinite(finite);
        }
        catch (Exception error) when (error is JsonException or ArgumentException or InvalidOperationException)
        {
            return false;
        }
    }

    private static bool Rational(string text, out BigInteger coefficient, out long exponent)
    {
        coefficient = 0; exponent = 0;
        // This is a small point-delta adapter, not an unbounded arbitrary-precision arithmetic service.
        if (text.Length > 4096) return false;
        var exponentIndex = text.IndexOfAny(['e', 'E']);
        var mantissa = exponentIndex < 0 ? text : text[..exponentIndex];
        if (exponentIndex >= 0)
        {
            if (!int.TryParse(text[(exponentIndex + 1)..], NumberStyles.AllowLeadingSign,
                CultureInfo.InvariantCulture, out var parsedExponent)) return false;
            exponent = parsedExponent;
        }
        var point = mantissa.IndexOf('.');
        if (point >= 0) exponent -= mantissa.Length - point - 1;
        if (!BigInteger.TryParse(mantissa.Replace(".", ""), NumberStyles.AllowLeadingSign,
            CultureInfo.InvariantCulture, out coefficient)) return false;
        if (coefficient.IsZero) { exponent = 0; return true; }
        while (coefficient % 10 == 0) { coefficient /= 10; exponent++; }
        return true;
    }
}
