using System.Text.Json;
using System.Text.Json.Nodes;
using BookOfEternityClient.Configuration;
using BookOfEternityClient.Core;
using M = BookOfEternityClient.Services.MortalQteDecisionPublication;

namespace BookOfEternityClient.Services;

public partial class CharacteristicsService
{
    // Pure extraction of ApplyStatsIncreased + durable level-award arithmetic.
    // Caller-supplied bytes are calculation data, never original/phase authority.
    internal static M.JointAutomaticPlan CalculateMortalJointAutomatic(M.JointAutomaticInputs inputs)
    {
        ArgumentNullException.ThrowIfNull(inputs);
        var stats = ReadAutomaticRoot(inputs, M.JointAutomaticInputs.CharacteristicsPath, required: true)!;
        var changes = ReadAutomaticRoot(inputs, M.JointAutomaticInputs.StatusChangesPath);
        var points = ReadAutomaticRoot(inputs, M.JointAutomaticInputs.StatPointsPath, required: true)!;
        var experience = ReadAutomaticRoot(inputs, M.JointAutomaticInputs.ExperiencePath);
        var player = ReadAutomaticRoot(inputs, M.JointAutomaticInputs.PlayerStatusPath);
        var soul = ReadAutomaticRoot(inputs, M.JointAutomaticInputs.SoulPath);
        RejectAutomaticCaseAliases(changes, "statsIncreased");
        RejectAutomaticCaseAliases(points, "unspentStatPoints", "levelUpStatPointsAwardedThroughLevel");
        RejectAutomaticCaseAliases(experience, "level", "playerLevel");
        RejectAutomaticCaseAliases(player, "level"); RejectAutomaticCaseAliases(soul, "level");
        var names = Characteristics.All.ToArray();
        var baseStats = new Dictionary<string, int>(StringComparer.Ordinal);
        foreach (var name in names) baseStats.Add(name, AutomaticInteger(stats, name));
        if (stats.Any(row => !baseStats.ContainsKey(row.Key) && !row.Key.StartsWith('_')))
            throw new InvalidDataException("Automatic calculation requires canonical base-stat names.");
        // Missing initializer evidence is not repaired from computed level, balance
        // or an in-memory _lastKnownLevel. No initial eight-point award lives here.
        var processed = AutomaticInteger(points, "levelUpStatPointsAwardedThroughLevel");
        var unspent = AutomaticInteger(points, "unspentStatPoints");
        if (processed < 1 || unspent < 0) throw new InvalidDataException("Invalid automatic initializer/balance evidence.");
        var expLevel = AutomaticLevel(experience, "level");
        var expPlayerLevel = AutomaticLevel(experience, "playerLevel");
        var playerLevel = AutomaticLevel(player, "level");
        var soulLevel = AutomaticLevel(soul, "level");
        var trainingLevel = expLevel ?? expPlayerLevel ?? playerLevel ?? soulLevel ?? 1;
        var awardLevel = expLevel ?? expPlayerLevel ?? 1;
        if (awardLevel <= 1) awardLevel = playerLevel ?? 1; // CheckLevelUp has no soul fallback.
        var cap = checked(trainingLevel * 2);
        var pointsAwarded = checked(5 * Math.Max(0, awardLevel - Math.Max(1, processed)));
        var finalBalance = checked(unspent + pointsAwarded);

        var ordered = new List<string>(); var applied = new List<string>();
        var blocked = new List<string>(); var ignored = new List<string>();
        if (changes?.ContainsKey("statsIncreased") == true)
        {
            if (changes["statsIncreased"] is not JsonArray array)
                throw new InvalidDataException("statsIncreased must be an array of strings.");
            foreach (var entry in array)
            {
                if (entry is not JsonValue value || !value.TryGetValue<string>(out var raw) || raw is null)
                    throw new InvalidDataException("statsIncreased must contain strings only.");
                ordered.Add(raw);
                var name = raw.ToLowerInvariant().Trim();
                if (!baseStats.TryGetValue(name, out var current)) { ignored.Add(name); continue; }
                if (current >= cap || current >= 100) { blocked.Add(name); continue; }
                baseStats[name] = Math.Min(checked(current + 1), 100); applied.Add(name);
            }
        }
        var statsAfter = inputs.ReadSourceBytes(M.JointAutomaticInputs.CharacteristicsPath)!;
        if (applied.Count > 0)
        {
            // SaveBaseStats writes exactly the canonical twelve keys, in owner order.
            var root = new JsonObject(); foreach (var name in names) root.Add(name, baseStats[name]);
            statsAfter = EncodeAutomaticRoot(root);
        }
        var changesAfter = inputs.ReadSourceBytes(M.JointAutomaticInputs.StatusChangesPath);
        if (ordered.Count > 0)
        {
            changes!.Remove("statsIncreased"); changesAfter = EncodeAutomaticRoot(changes);
        }
        var pointsAfter = inputs.ReadSourceBytes(M.JointAutomaticInputs.StatPointsPath)!;
        if (pointsAwarded > 0)
        {
            points["unspentStatPoints"] = finalBalance;
            points["levelUpStatPointsAwardedThroughLevel"] = Math.Max(processed, awardLevel);
            pointsAfter = EncodeAutomaticRoot(points);
        }
        return new(inputs, trainingLevel, awardLevel, processed, unspent, pointsAwarded,
            ordered, applied, blocked, ignored, statsAfter, changesAfter, pointsAfter);
    }

    private static JsonObject? ReadAutomaticRoot(M.JointAutomaticInputs inputs, string path, bool required = false)
    {
        var bytes = inputs.ReadSourceBytes(path);
        if (bytes is null)
        {
            if (required) throw new InvalidDataException("Missing automatic source: " + path);
            return null;
        }
        try
        {
            var strict = new JsonSerializerOptions(SharedJsonOptions.PrettyCamelCaseUnsafeRelaxed)
            { PropertyNameCaseInsensitive = true };
            // Case-insensitive uniqueness detects aliases in duplicate pairs, but
            // must not make subsequent JsonObject field lookup case-insensitive.
            var element = StrictJsonAuthority.Deserialize<JsonElement>(CanonicalJsonUtf8.DecodeOneOptionalBom(bytes), strict, path);
            return element.Deserialize<JsonObject>(SharedJsonOptions.PrettyCamelCaseUnsafeRelaxed) ??
                throw new InvalidDataException("Automatic source must be a non-null object: " + path);
        }
        catch (JsonException failure) { throw new InvalidDataException("Malformed automatic source: " + path, failure); }
    }
    private static int AutomaticInteger(JsonObject root, string field) =>
        root[field] is JsonValue value && value.TryGetValue<int>(out var n) ? n :
            throw new InvalidDataException("Missing/non-integer automatic field: " + field);
    private static int? AutomaticLevel(JsonObject? root, string field)
    {
        if (root?.ContainsKey(field) != true) return null;
        var level = AutomaticInteger(root, field);
        return level > 0 ? level : throw new InvalidDataException("Invalid automatic level: " + field);
    }
    private static void RejectAutomaticCaseAliases(JsonObject? root, params string[] names)
    {
        if (root is null) return;
        foreach (var row in root)
            if (names.Any(name => name != row.Key && string.Equals(name, row.Key, StringComparison.OrdinalIgnoreCase)))
                throw new InvalidDataException("Noncanonical automatic field alias: " + row.Key);
    }
    private static byte[] EncodeAutomaticRoot(JsonObject root) => FileSystemManager.EncodeUtf8WithPreamble(root.ToJsonString(JsonOpts));
}
