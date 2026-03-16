using System.Text.Json;

namespace BookOfEternityClient.Services;

internal sealed record LifeEvaluationRewardRelic(string RelicId, string Name, string Rarity);

internal sealed record LifeEvaluationRewardDelta(
    int PreInkFeathers,
    int PostInkFeathers,
    IReadOnlyList<LifeEvaluationRewardRelic> NewRelics)
{
    public int InkFeathersEarned => PostInkFeathers - PreInkFeathers;
}

internal static class LifeEvaluationRewardAnalyzer
{
    public const string VoluntaryLifeEvaluationSourceLabel = "оценки жизни";
    public const string AutomaticLifeEvaluationSourceLabel = "автоматической оценки жизни";

    public static bool IsLifeEvaluationSourceLabel(string? sourceLabel)
    {
        return string.Equals(sourceLabel, VoluntaryLifeEvaluationSourceLabel, StringComparison.OrdinalIgnoreCase) ||
               string.Equals(sourceLabel, AutomaticLifeEvaluationSourceLabel, StringComparison.OrdinalIgnoreCase);
    }

    public static bool TryComputeDelta(string? preSoulStateJson, string? postSoulStateJson,
        out LifeEvaluationRewardDelta? delta, out string? error)
    {
        delta = null;
        error = null;

        if (string.IsNullOrWhiteSpace(preSoulStateJson))
        {
            error = "missing pre-turn soul_state snapshot";
            return false;
        }

        if (string.IsNullOrWhiteSpace(postSoulStateJson))
        {
            error = "missing current soul_state";
            return false;
        }

        try
        {
            using var preDoc = JsonDocument.Parse(preSoulStateJson);
            using var postDoc = JsonDocument.Parse(postSoulStateJson);

            var preInkFeathers = ReadInkFeathersCurrent(preDoc.RootElement);
            var postInkFeathers = ReadInkFeathersCurrent(postDoc.RootElement);

            var preRelics = ReadRelics(preDoc.RootElement);
            var postRelics = ReadRelics(postDoc.RootElement);

            var newRelics = postRelics.Values
                .Where(relic => !preRelics.ContainsKey(relic.RelicId))
                .OrderBy(relic => relic.Name, StringComparer.OrdinalIgnoreCase)
                .ToList();

            delta = new LifeEvaluationRewardDelta(preInkFeathers, postInkFeathers, newRelics);
            return true;
        }
        catch (JsonException ex)
        {
            error = ex.Message;
            return false;
        }
    }

    private static int ReadInkFeathersCurrent(JsonElement root)
    {
        if (!root.TryGetProperty("inkFeathers", out var inkNode))
            return 0;

        if (inkNode.ValueKind == JsonValueKind.Number && inkNode.TryGetInt32(out var numeric))
            return numeric;

        if (inkNode.ValueKind == JsonValueKind.Object &&
            inkNode.TryGetProperty("current", out var currentNode))
        {
            if (currentNode.ValueKind == JsonValueKind.Number && currentNode.TryGetInt32(out var currentNumeric))
                return currentNumeric;
            if (currentNode.ValueKind == JsonValueKind.String &&
                int.TryParse(currentNode.GetString(), out var currentString))
                return currentString;
        }

        return 0;
    }

    private static Dictionary<string, LifeEvaluationRewardRelic> ReadRelics(JsonElement root)
    {
        var relics = new Dictionary<string, LifeEvaluationRewardRelic>(StringComparer.OrdinalIgnoreCase);
        if (!root.TryGetProperty("soulRelics", out var soulRelics) || soulRelics.ValueKind != JsonValueKind.Object)
            return relics;

        foreach (var arrayName in new[] { "stored", "equipped" })
        {
            if (!soulRelics.TryGetProperty(arrayName, out var relicArray) || relicArray.ValueKind != JsonValueKind.Array)
                continue;

            foreach (var relic in relicArray.EnumerateArray())
            {
                if (relic.ValueKind != JsonValueKind.Object)
                    continue;

                var relicId = GetFirstNonEmptyString(relic, "relicId", "id");
                if (string.IsNullOrWhiteSpace(relicId))
                    continue;

                var name = GetFirstNonEmptyString(relic, "name") ?? relicId;
                var rarity = GetFirstNonEmptyString(relic, "rarity") ?? string.Empty;
                relics[relicId] = new LifeEvaluationRewardRelic(relicId, name, rarity);
            }
        }

        return relics;
    }

    private static string? GetFirstNonEmptyString(JsonElement element, params string[] propertyNames)
    {
        foreach (var propertyName in propertyNames)
        {
            if (!element.TryGetProperty(propertyName, out var node) || node.ValueKind != JsonValueKind.String)
                continue;

            var value = node.GetString();
            if (!string.IsNullOrWhiteSpace(value))
                return value;
        }

        return null;
    }
}
