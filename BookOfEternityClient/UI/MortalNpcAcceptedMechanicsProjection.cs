using System.Globalization;
using System.Text.Json;
using System.Text.Json.Nodes;
using BookOfEternityClient.Configuration;
using BookOfEternityClient.Services;

namespace BookOfEternityClient.UI;

/// <summary>Player-facing DATA from one already selected canonical actor, never command or publication authority.</summary>
internal sealed record MortalNpcAcceptedMechanicsProjection(
    int? Level, int? Experience, int? ExperienceForNextLevel,
    IReadOnlyList<MortalNpcAcceptedSkillProjection> Active,
    IReadOnlyList<MortalNpcAcceptedSkillProjection> Passive)
{
    internal IReadOnlyList<KeyValuePair<string, string>> ProfileRows { get; init; } = [];
    internal IReadOnlyList<KeyValuePair<string, string>> CharacteristicRows { get; init; } = [];

    internal static MortalNpcAcceptedMechanicsProjection Build(JsonObject actor) => new(
        Number(actor, "level"), Number(actor, "experience"), Number(actor, "experienceForNextLevel"),
        Skills(actor["activeSkills"]), Skills(actor["passiveSkills"]))
    {
        ProfileRows = Profile(actor["progressionProfile"] as JsonObject),
        CharacteristicRows = CharacteristicsData(actor["characteristics"] as JsonObject)
    };

    private static IReadOnlyList<KeyValuePair<string, string>> Profile(JsonObject? profile)
    {
        // Read only the existing closed declaration. Never infer a missing profile,
        // echo nested JSON, or reconstruct consumed award/history commands for display.
        if (profile is null || !MortalNpcProgressionConsequences.ParseProfile(profile).IsValid) return [];
        var mode = Text(profile, "mode");
        var details = new List<string>
        {
            mode switch { "standard" => "Стандартный", "table" => "Табличный", _ => "Без развития" }
        };
        if (mode == "none") return [new("Профиль развития", details[0])];
        if (mode == "standard") details.Add("Порог опыта: floor(100 × L^2.5)");
        if (mode == "table" && profile["thresholds"] is JsonArray thresholds &&
            thresholds.All(value => value is JsonValue number && number.TryGetValue<int>(out var threshold) && threshold > 0))
            details.Add("Пороги: " + string.Join(", ", thresholds.Select(value => value!.GetValue<int>().ToString(CultureInfo.InvariantCulture))));
        if (Number(profile, "maxLevel") is { } cap && cap > 0)
            details.Add("Предел уровня: " + cap.ToString(CultureInfo.InvariantCulture));
        else if (mode == "table" && profile["thresholds"] is JsonArray { Count: > 0 } table)
            details.Add("Предел уровня: " + ((long)table.Count + 1).ToString(CultureInfo.InvariantCulture));
        if (Text(profile, "capOverflow") == "discard") details.Add("Излишек опыта: отбрасывается");
        if (profile["allowExperienceLoss"] is JsonValue loss && loss.TryGetValue<bool>(out var allowed) && allowed)
            details.Add("Потеря опыта: разрешена");
        return
        [
            new("Профиль развития", string.Join("; ", details)),
            new("Награды развития", "Навыки: 1 награда за каждые 2 полученных уровня; новый навык или повышение мастерства"),
            new("Рост характеристик", "Характеристики: правила текущего мира")
        ];
    }

    private static readonly HashSet<string> PrivateCharacteristicFields = new(StringComparer.OrdinalIgnoreCase)
    {
        "source", "sessionId", "requestId", "turnNumber", "snapshotToken", "snapshotHash", "manifestPayloadHash",
        "receipt", "receiptId", "progressionReceipt", "progressionReceipts", "transitionId"
    };

    private static IReadOnlyList<KeyValuePair<string, string>> CharacteristicsData(JsonObject? characteristics)
    {
        if (characteristics is null) return [];
        var rows = new List<KeyValuePair<string, string>>();
        foreach (var (key, node) in characteristics.OrderBy(pair => pair.Key, StringComparer.Ordinal))
        {
            if (PrivateCharacteristicFields.Contains(key) || node is not JsonValue value ||
                (value.TryGetValue<double>(out var number) && !double.IsFinite(number)) ||
                (value.TryGetValue<float>(out var single) && !float.IsFinite(single)) ||
                value.GetValueKind() != JsonValueKind.Number) continue;
            // Preserve exact JSON numeric text, including decimal values and zero;
            // do not narrow setting-owned values to Int32 or assign universal ranges.
            rows.Add(new(key, value.ToJsonString()));
        }
        return rows.ToArray();
    }

    internal static string CharacteristicLabel(string key)
    {
        if (Characteristics.RussianNames.TryGetValue(key, out var translated)) return translated;
        foreach (var (prefix, suffix) in new[] { ("standard", "базовая"), ("modified", "текущая") })
        {
            if (!key.StartsWith(prefix, StringComparison.Ordinal) || key.Length == prefix.Length) continue;
            var name = char.ToLowerInvariant(key[prefix.Length]) + key[(prefix.Length + 1)..];
            if (Characteristics.RussianNames.TryGetValue(name, out translated)) return translated + " (" + suffix + ")";
        }
        return key;
    }

    private static IReadOnlyList<MortalNpcAcceptedSkillProjection> Skills(JsonNode? node)
    {
        if (node is not JsonArray skills) return Array.Empty<MortalNpcAcceptedSkillProjection>();
        var result = new List<MortalNpcAcceptedSkillProjection>();
        foreach (var skill in skills.OfType<JsonObject>())
        {
            var name = Text(skill, "skillName");
            if (string.IsNullOrWhiteSpace(name)) continue;
            // Copy only public scalars. Nested metadata never reaches console debug rendering or browser DTOs.
            var data = new JsonObject { ["skillName"] = name };
            foreach (var field in new[] { "description", "skillDescription", "type", "category", "rarity", "cooldownTurns", "cooldown", "actionCost", "energyCost", "timeCost" })
                if (skill[field] is JsonValue value &&
                    (value.TryGetValue<string>(out _) || value.TryGetValue<decimal>(out _)))
                    data[field] = value.DeepClone();
            result.Add(new(data, Number(skill, "masteryLevel") ?? Number(skill, "currentMasteryLevel"),
                Number(skill, "currentMasteryProgress"), Number(skill, "masteryProgressNeeded"),
                Number(skill, "maxMasteryLevel")));
        }
        return result.OrderBy(skill => Text(skill.Data, "skillName"), StringComparer.OrdinalIgnoreCase).ToArray();
    }

    private static int? Number(JsonObject node, string field) =>
        node[field] is JsonValue value && value.TryGetValue<int>(out var number) && number >= 0 ? number : null;

    internal static string Text(JsonObject node, string field) => node[field] is JsonValue value
        ? value.TryGetValue<string>(out var text) ? text :
          value.TryGetValue<decimal>(out var number) ? number.ToString(CultureInfo.InvariantCulture) : string.Empty
        : string.Empty;
}

internal sealed record MortalNpcAcceptedSkillProjection(JsonObject Data, int? MasteryLevel,
    int? CurrentMasteryProgress, int? MasteryProgressNeeded, int? MaxMasteryLevel);
