using System.Text.Json;
using System.Text.Json.Nodes;

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
        => TryComputeDelta(
            preSoulStateJson,
            postSoulStateJson,
            hasCanonicalTriggerLifeEnd: false,
            out delta,
            out error);

    public static bool TryComputeDelta(
        string? preSoulStateJson,
        string? postSoulStateJson,
        bool hasCanonicalTriggerLifeEnd,
        out LifeEvaluationRewardDelta? delta,
        out string? error)
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
            if (JsonNode.Parse(preSoulStateJson) is not JsonObject preRoot)
            {
                error = "pre-turn soul_state snapshot must be a JsonObject";
                return false;
            }

            if (JsonNode.Parse(postSoulStateJson) is not JsonObject postRoot)
            {
                error = "current soul_state must be a JsonObject";
                return false;
            }

            if (GuardianPolicyContracts.TryDescribeInvalidPolicySensitiveReadableSoulStateRoot(preRoot, out var preFailure))
            {
                error = $"invalid pre-turn soul_state snapshot: {preFailure}";
                return false;
            }

            if (GuardianPolicyContracts.TryDescribeInvalidPolicySensitiveReadableSoulStateRoot(
                    postRoot,
                    hasCanonicalTriggerLifeEnd,
                    out var postFailure))
            {
                error = $"invalid current soul_state: {postFailure}";
                return false;
            }

            var preInkFeathers = ReadCanonicalInkFeathersCurrent(preRoot);
            var postInkFeathers = ReadCanonicalInkFeathersCurrent(postRoot);

            var preRelics = ReadCanonicalRelics(preRoot);
            var postRelics = ReadCanonicalRelics(postRoot);

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

    private static int ReadCanonicalInkFeathersCurrent(JsonObject root)
    {
        if (root["inkFeathers"] is not JsonObject inkNode)
            return 0;

        if (inkNode["current"] is JsonValue currentNode &&
            currentNode.TryGetValue<int>(out var numeric))
        {
            return numeric;
        }

        if (inkNode["current"] is JsonValue longCurrentNode &&
            longCurrentNode.TryGetValue<long>(out var longNumeric) &&
            longNumeric is >= 0 and <= int.MaxValue)
        {
            return (int)longNumeric;
        }

        return 0;
    }

    private static Dictionary<string, LifeEvaluationRewardRelic> ReadCanonicalRelics(JsonObject root)
    {
        var relics = new Dictionary<string, LifeEvaluationRewardRelic>(StringComparer.OrdinalIgnoreCase);
        if (root["soulRelics"] is not JsonObject soulRelics)
            return relics;

        foreach (var arrayName in new[] { "stored", "equipped" })
        {
            if (soulRelics[arrayName] is not JsonArray relicArray)
                continue;

            foreach (var relic in relicArray.OfType<JsonObject>())
            {
                var relicId = GetFirstNonEmptyString(relic, "relicId");
                if (string.IsNullOrWhiteSpace(relicId))
                    continue;

                var name = GetFirstNonEmptyString(relic, "name") ?? relicId;
                var rarity = GetFirstNonEmptyString(relic, "rarity") ?? string.Empty;
                relics[relicId] = new LifeEvaluationRewardRelic(relicId, name, rarity);
            }
        }

        return relics;
    }

    private static string? GetFirstNonEmptyString(JsonObject element, params string[] propertyNames)
    {
        foreach (var propertyName in propertyNames)
        {
            if (element[propertyName] is not JsonValue node ||
                !node.TryGetValue<string>(out var value) ||
                string.IsNullOrWhiteSpace(value))
            {
                continue;
            }

            return value;
        }

        return null;
    }
}
