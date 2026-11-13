using System.Collections.Frozen;
using System.Text.Json.Nodes;

namespace BookOfEternityClient.Services;

internal static partial class MortalNpcProgressionConsequences
{
    internal sealed record ParseResult<T>(T? Value, IReadOnlyList<Problem> Problems) where T : class
    {
        internal bool IsValid => Value is not null && Problems.Count == 0;
    }

    /// <summary>Owns consequence declarations only; numeric options are validated by the numeric contract.</summary>
    internal static ParseResult<ProfileConsequences> ParseProfile(JsonObject progressionProfile)
    {
        const string path = "progressionProfile";
        if (!Only(progressionProfile, "schemaVersion", "mode", "allowExperienceLoss", "maxLevel", "capOverflow", "thresholds", "consequences") ||
            !Integer(progressionProfile["schemaVersion"], out var version) || version != 1)
            return InvalidParse<ProfileConsequences>(path);
        var mode = Exact(progressionProfile["mode"]);
        if (mode == "none")
            return progressionProfile.Count == 2
                ? new(new(false), []) : InvalidParse<ProfileConsequences>(path);
        if (mode is not ("standard" or "table")) return InvalidParse<ProfileConsequences>(path + ".mode");
        if (progressionProfile["consequences"] is not JsonObject consequences ||
            !Only(consequences, "skills", "characteristics"))
            return InvalidParse<ProfileConsequences>(path + ".consequences");
        if (consequences["skills"] is not JsonObject skills || !Only(skills, "levelsPerAward", "allowedAwards") ||
            !Integer(skills["levelsPerAward"], out var cadence) || cadence != 2 ||
            skills["allowedAwards"] is not JsonArray { Count: 2 } awards)
            return InvalidParse<ProfileConsequences>(path + ".consequences.skills");
        var options = new HashSet<string>(StringComparer.Ordinal);
        foreach (var node in awards)
        {
            var option = Exact(node);
            if (option is not ("newSkill" or "masteryIncrease") || !options.Add(option))
                return InvalidParse<ProfileConsequences>(path + ".consequences.skills.allowedAwards");
        }
        if (consequences["characteristics"] is not JsonObject characteristics ||
            !Only(characteristics, "mode") || Exact(characteristics["mode"]) != "currentWorld")
            return InvalidParse<ProfileConsequences>(path + ".consequences.characteristics");
        return new(new(true, cadence), []);
    }

    /// <summary>
    /// Missing file/subtree means no characteristic growth. A present malformed declaration
    /// never becomes absence. Caller owns parsing/authentication of the world-setting document.
    /// Returned collections are detached and frozen for reuse as invocation DATA.
    /// </summary>
    internal static ParseResult<CharacteristicPolicy> ParseWorldPolicy(JsonObject? worldSetting)
    {
        const string property = "npcProgression";
        if (worldSetting is null) return new(new(CharacteristicMode.None), []);
        var ownedKey = MortalLocationIdentityState.BuildConfusableKey(property);
        foreach (var pair in worldSetting)
            if (pair.Key != property && MortalLocationIdentityState.BuildConfusableKey(pair.Key) == ownedKey)
                return InvalidParse<CharacteristicPolicy>(pair.Key);
        if (!worldSetting.ContainsKey(property)) return new(new(CharacteristicMode.None), []);
        if (worldSetting[property] is not JsonObject progression || !Only(progression, "schemaVersion", "characteristics") ||
            !Integer(progression["schemaVersion"], out var version) || version != 1 ||
            progression["characteristics"] is not JsonObject characteristics)
            return InvalidParse<CharacteristicPolicy>(property);
        var path = property + ".characteristics";
        switch (Exact(characteristics["mode"]))
        {
            case "none":
                return Only(characteristics, "mode")
                    ? new(new(CharacteristicMode.None), []) : InvalidParse<CharacteristicPolicy>(path);
            case "fixedIncrements":
            {
                if (!Only(characteristics, "mode", "perLevel") ||
                    characteristics["perLevel"] is not JsonObject { Count: > 0 } increments)
                    return InvalidParse<CharacteristicPolicy>(path);
                var values = new Dictionary<string, int>(StringComparer.Ordinal);
                var keys = new HashSet<string>(StringComparer.Ordinal);
                foreach (var pair in increments)
                {
                    if (!ValidKey(pair.Key) || !keys.Add(MortalLocationIdentityState.BuildConfusableKey(pair.Key)) ||
                        !Integer(pair.Value, out var increment) || increment <= 0)
                        return InvalidParse<CharacteristicPolicy>(path + ".perLevel." + pair.Key);
                    values.Add(pair.Key, increment);
                }
                return new(new(CharacteristicMode.FixedIncrements, values.ToFrozenDictionary(StringComparer.Ordinal)), []);
            }
            case "allocatedPoints":
            {
                if (!Only(characteristics, "mode", "pointsPerLevel", "allowedKeys") ||
                    !Integer(characteristics["pointsPerLevel"], out var points) || points <= 0 ||
                    characteristics["allowedKeys"] is not JsonArray { Count: > 0 } allowed)
                    return InvalidParse<CharacteristicPolicy>(path);
                var values = new HashSet<string>(StringComparer.Ordinal);
                var keys = new HashSet<string>(StringComparer.Ordinal);
                foreach (var node in allowed)
                {
                    if (Exact(node) is not { } key || !keys.Add(MortalLocationIdentityState.BuildConfusableKey(key)))
                        return InvalidParse<CharacteristicPolicy>(path + ".allowedKeys");
                    values.Add(key);
                }
                return new(new(CharacteristicMode.AllocatedPoints, PointsPerLevel: points,
                    AllocationKeys: values.ToFrozenSet(StringComparer.Ordinal)), []);
            }
            default:
                return InvalidParse<CharacteristicPolicy>(path + ".mode");
        }
    }

    private static bool Only(JsonObject root, params string[] fields) =>
        root.All(pair => fields.Contains(pair.Key, StringComparer.Ordinal));

    private static ParseResult<T> InvalidParse<T>(string field) where T : class =>
        new(null, [new("npc_progression_consequence_policy_invalid", field)]);
}
