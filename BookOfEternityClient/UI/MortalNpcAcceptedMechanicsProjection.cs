using System.Globalization;
using System.Text.Json.Nodes;

namespace BookOfEternityClient.UI;

/// <summary>Player-facing DATA from one already selected canonical actor, never command or publication authority.</summary>
internal sealed record MortalNpcAcceptedMechanicsProjection(
    int? Level, int? Experience, int? ExperienceForNextLevel,
    IReadOnlyList<MortalNpcAcceptedSkillProjection> Active,
    IReadOnlyList<MortalNpcAcceptedSkillProjection> Passive)
{
    internal static MortalNpcAcceptedMechanicsProjection Build(JsonObject actor) => new(
        Number(actor, "level"), Number(actor, "experience"), Number(actor, "experienceForNextLevel"),
        Skills(actor["activeSkills"]), Skills(actor["passiveSkills"]));

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
