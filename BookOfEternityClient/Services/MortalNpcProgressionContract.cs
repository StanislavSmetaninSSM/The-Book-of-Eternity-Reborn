using System.Numerics;
using System.Text.Json.Nodes;

namespace BookOfEternityClient.Services;

/// <summary>
/// Pure numeric progression validation. No file reads, mutation, caches, or publication authority.
/// The caller supplies detached authenticated baseline DATA and owns identity, realm and turn checks.
/// </summary>
internal static class MortalNpcProgressionContract
{
    /// <summary>Absolute Int32 tuple; experience is within-level, never cumulative.</summary>
    internal sealed record ProgressionTuple(int Level, int Experience, int ExperienceForNextLevel);

    /// <summary>A bounded diagnostic with a path relative to the supplied progression package.</summary>
    internal sealed record Problem(string Field, string Message);

    /// <summary>Only a valid evaluation has a value; no partial tuple can be published.</summary>
    internal sealed record Evaluation(ProgressionTuple? Value, IReadOnlyList<Problem> Problems)
    {
        internal bool IsValid => Value is not null && Problems.Count == 0;
    }

    private sealed record Profile(string Mode, bool AllowLoss, int? MaxLevel, int[]? Thresholds);

    /// <summary>
    /// Requires baseline progressionProfile {schemaVersion:1,mode:"standard"|"table"|"none"}.
    /// Standard uses floor(100*level^2.5); table thresholds[L-1] is the cost leaving level L,
    /// with implicit cap thresholds.Length+1. Optional maxLevel may shorten that cap.
    /// Capped profiles require capOverflow:"discard" and terminal experience/threshold are zero.
    /// Optional allowExperienceLoss:true permits loss only within the current level, never deleveling.
    /// None permits no additional profile options, retains any nonnegative level, and requires zero
    /// experience, threshold and delta. Commands require signed Int32 experienceDelta and a complete
    /// nonnegative absolute tuple. Intermediate XP overflow is rejected before cap discard.
    /// Profiles cannot be replaced here. Narrative progressionType/tracker validation remains caller-owned.
    /// </summary>
    internal static Evaluation Evaluate(JsonObject baselineActor, JsonObject progression)
    {
        foreach (var property in progression)
        {
            if (property.Key is not ("level" or "experience" or "experienceForNextLevel" or
                "experienceDelta" or "source" or "progressionType" or "lastPlayerXPValueOnSync"))
                return Reject(property.Key, "Unknown progression field; profile replacement requires a separate explicit transition.");
        }

        var profileProblem = ReadProfile(baselineActor["progressionProfile"], out var profile);
        if (profileProblem is not null)
            return new Evaluation(null, [profileProblem]);

        var baselineProblem = ReadTuple(baselineActor, "baseline.", out var baseline);
        if (baselineProblem is not null)
            return new Evaluation(null, [baselineProblem]);
        var requestedProblem = ReadTuple(progression, "", out var requested);
        if (requestedProblem is not null)
            return new Evaluation(null, [requestedProblem]);
        if (!TryInteger(progression["experienceDelta"], out var delta))
            return Reject("experienceDelta", "experienceDelta must be a signed Int32 JSON integer.");

        if (profile!.Mode == "none")
        {
            if (baseline!.Experience != 0 || baseline.ExperienceForNextLevel != 0)
                return Reject("baseline.experience", "A non-progressing actor requires zero experience and threshold.");
            if (delta != 0)
                return Reject("experienceDelta", "A non-progressing actor cannot gain or lose experience.");
            return Match(baseline, requested!);
        }

        if (baseline!.Level < 1 || (profile.MaxLevel is int maximum && baseline.Level > maximum))
            return Reject("baseline.level", "The baseline level is outside the explicit progression profile.");
        if (!TryThreshold(profile, baseline.Level, out var threshold))
            return Reject("baseline.experienceForNextLevel", "The profile threshold exceeds the supported Int32 range.");
        if (baseline.ExperienceForNextLevel != threshold)
            return Reject("baseline.experienceForNextLevel", "The baseline threshold does not match its explicit profile.");
        if (threshold == 0 ? baseline.Experience != 0 : baseline.Experience >= threshold)
            return Reject("baseline.experience", "Baseline experience must be below its threshold, or zero at the cap.");
        if (delta < 0 && !profile.AllowLoss)
            return Reject("experienceDelta", "Experience loss requires allowExperienceLoss:true.");

        var accumulated = (long)baseline.Experience + delta;
        if (accumulated < 0 || accumulated > int.MaxValue)
            return Reject("experienceDelta", "Within-level experience underflow or Int32 overflow is not permitted.");
        var experience = (int)accumulated;
        var level = baseline.Level;
        while (threshold > 0 && experience >= threshold)
        {
            experience -= threshold;
            if (level == int.MaxValue)
                return Reject("level", "The resulting level exceeds the supported Int32 range.");
            level++;
            if (!TryThreshold(profile, level, out threshold))
                return Reject("experienceForNextLevel", "The resulting threshold exceeds the supported Int32 range.");
        }
        if (threshold == 0)
            experience = 0; // ReadProfile has required the explicit discard policy.
        return Match(new ProgressionTuple(level, experience, threshold), requested!);
    }

    /// <summary>Parses the closed, versioned numeric profile without inferring mechanics.</summary>
    private static Problem? ReadProfile(JsonNode? node, out Profile? result)
    {
        result = null;
        if (node is not JsonObject profile)
            return new Problem("progressionProfile", "An explicit canonical progressionProfile object is required.");
        foreach (var property in profile)
        {
            if (property.Key is not ("schemaVersion" or "mode" or "allowExperienceLoss" or "maxLevel" or "capOverflow" or "thresholds"))
                return new Problem($"progressionProfile.{property.Key}", "Unknown progression profile field.");
        }
        if (!TryInteger(profile["schemaVersion"], out var version) || version != 1)
            return new Problem("progressionProfile.schemaVersion", "Only integer schemaVersion:1 is supported.");
        var mode = ReadString(profile["mode"]);
        if (mode is not ("standard" or "table" or "none"))
            return new Problem("progressionProfile.mode", "Select exactly standard, table, or none.");
        if (mode == "none")
        {
            if (profile.Count != 2)
                return new Problem("progressionProfile", "Mode none accepts only schemaVersion and mode.");
            result = new Profile(mode, false, null, null);
            return null;
        }
        var allowLoss = false;
        if (profile.ContainsKey("allowExperienceLoss") &&
            (profile["allowExperienceLoss"] is not JsonValue allowNode || !allowNode.TryGetValue(out allowLoss)))
            return new Problem("progressionProfile.allowExperienceLoss", "allowExperienceLoss must be a JSON Boolean.");
        int? cap = null;
        if (profile.ContainsKey("maxLevel"))
        {
            if (!TryInteger(profile["maxLevel"], out var maximum) || maximum < 1)
                return new Problem("progressionProfile.maxLevel", "maxLevel must be a positive Int32 integer.");
            cap = maximum;
        }
        int[]? thresholds = null;
        if (mode == "table")
        {
            if (profile["thresholds"] is not JsonArray table || table.Count == 0 || table.Count == int.MaxValue)
                return new Problem("progressionProfile.thresholds", "A table requires a nonempty finite array of positive Int32 thresholds.");
            thresholds = new int[table.Count];
            for (var i = 0; i < table.Count; i++)
            {
                if (!TryInteger(table[i], out thresholds[i]) || thresholds[i] <= 0)
                    return new Problem($"progressionProfile.thresholds[{i}]", "Every threshold must be a positive Int32 integer.");
            }
            if (cap > table.Count + 1)
                return new Problem("progressionProfile.maxLevel", "maxLevel cannot extend beyond the explicit table.");
            cap ??= table.Count + 1;
        }
        else if (profile.ContainsKey("thresholds"))
            return new Problem("progressionProfile.thresholds", "Only table mode accepts thresholds.");

        if (cap.HasValue)
        {
            if (ReadString(profile["capOverflow"]) != "discard")
                return new Problem("progressionProfile.capOverflow", "A cap requires explicit capOverflow:discard.");
            if (mode == "standard" && cap.Value > 1 && !TryStandardThreshold(cap.Value - 1, out _))
                return new Problem("progressionProfile.maxLevel", "The capped profile contains thresholds beyond the supported Int32 range.");
        }
        else if (profile.ContainsKey("capOverflow"))
            return new Problem("progressionProfile.capOverflow", "An uncapped profile cannot declare capOverflow.");
        result = new Profile(mode, allowLoss, cap, thresholds);
        return null;
    }

    /// <summary>Reads exact nonnegative Int32 JSON tuple members, rejecting floating/string coercion.</summary>
    private static Problem? ReadTuple(JsonObject source, string prefix, out ProgressionTuple? result)
    {
        result = null;
        if (!TryInteger(source["level"], out var level) || level < 0)
            return new Problem(prefix + "level", "level must be a nonnegative Int32 JSON integer.");
        if (!TryInteger(source["experience"], out var experience) || experience < 0)
            return new Problem(prefix + "experience", "experience must be a nonnegative Int32 JSON integer.");
        if (!TryInteger(source["experienceForNextLevel"], out var threshold) || threshold < 0)
            return new Problem(prefix + "experienceForNextLevel", "experienceForNextLevel must be a nonnegative Int32 JSON integer.");
        result = new ProgressionTuple(level, experience, threshold);
        return null;
    }

    /// <summary>Resolves a validated profile's current within-level cost, with zero only at its cap.</summary>
    private static bool TryThreshold(Profile profile, int level, out int threshold)
    {
        threshold = 0;
        if (profile.MaxLevel == level)
            return true;
        if (profile.Thresholds is { } table)
        {
            threshold = table[level - 1];
            return true;
        }
        return TryStandardThreshold(level, out threshold);
    }

    /// <summary>
    /// floor(100*L^2.5) equals floor(sqrt(10000*L^5)). Integer-only bounded binary search
    /// avoids floating rounding authority; supported results need at most 31 iterations.
    /// </summary>
    private static bool TryStandardThreshold(int level, out int threshold)
    {
        threshold = 0;
        var square = 10000 * BigInteger.Pow(new BigInteger(level), 5);
        long low = 0;
        long high = (long)int.MaxValue + 1;
        if (square >= new BigInteger(high) * high)
            return false;
        while (high - low > 1)
        {
            var middle = low + (high - low) / 2;
            if (new BigInteger(middle) * middle <= square)
                low = middle;
            else
                high = middle;
        }
        threshold = (int)low;
        return true;
    }

    /// <summary>Rejects numeric coercion, fractional representations and out-of-range values.</summary>
    private static bool TryInteger(JsonNode? node, out int value)
    {
        value = 0;
        return node is JsonValue scalar && scalar.TryGetValue(out value);
    }

    /// <summary>Reads only an actual JSON string.</summary>
    private static string? ReadString(JsonNode? node) =>
        node is JsonValue value && value.TryGetValue<string>(out var text) ? text : null;

    /// <summary>Compares every absolute member against independently evaluated arithmetic.</summary>
    private static Evaluation Match(ProgressionTuple expected, ProgressionTuple requested)
    {
        if (expected.Level != requested.Level)
            return Reject("level", $"The evaluated level is {expected.Level}.");
        if (expected.Experience != requested.Experience)
            return Reject("experience", $"The evaluated within-level experience is {expected.Experience}.");
        if (expected.ExperienceForNextLevel != requested.ExperienceForNextLevel)
            return Reject("experienceForNextLevel", $"The evaluated threshold is {expected.ExperienceForNextLevel}.");
        return new Evaluation(expected, []);
    }

    /// <summary>Produces a structured refusal without exposing a publishable partial tuple.</summary>
    private static Evaluation Reject(string field, string message) => new(null, [new Problem(field, message)]);
}
