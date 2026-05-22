using System.Text.Json.Nodes;

namespace BookOfEternityClient.Services;

internal static class AfterlifeStoryOutlineState
{
    public const string StatePath = "game_state/meta/afterlife_story_outline.json";
    public const string ResponseField = "afterlifeStoryOutline";
    public const int SchemaVersion = 1;

    public static readonly string[] CanonicalFields =
    {
        "mainArc",
        "realmArc",
        "actorSubplots",
        "factionOrInstitutionArcs",
        "loomingThreatsOrOpportunities",
        "pendingRevelations",
        "nextLikelySceneBeats",
        "playerAgencyNotes",
        "lastUpdatedTurn"
    };

    public static JsonObject CreateDefaultRoot() =>
        new()
        {
            ["schemaVersion"] = SchemaVersion,
            ["mainArc"] = "none",
            ["realmArc"] = "none",
            ["actorSubplots"] = new JsonArray(),
            ["factionOrInstitutionArcs"] = new JsonArray(),
            ["loomingThreatsOrOpportunities"] = new JsonArray(),
            ["pendingRevelations"] = new JsonArray(),
            ["nextLikelySceneBeats"] = new JsonArray(),
            ["playerAgencyNotes"] = "Планы Writer's Room гибкие: не форсировать исход и обновлять план при выборе игрока.",
            ["lastUpdatedTurn"] = 0
        };

    public static JsonObject ProjectCanonicalRoot(JsonObject? currentRoot, JsonObject? previousRoot)
    {
        var result = CreateDefaultRoot();
        CopyCanonicalFields(previousRoot, result);
        CopyCanonicalFields(currentRoot, result);

        if (currentRoot?[ResponseField] is JsonObject update)
            CopyCanonicalFields(update, result);

        result.Remove(ResponseField);
        return result;
    }

    private static void CopyCanonicalFields(JsonObject? source, JsonObject target)
    {
        if (source == null)
            return;

        foreach (var field in CanonicalFields)
            CopyIfPresent(source, target, field);
    }

    private static void CopyIfPresent(JsonObject source, JsonObject target, string propertyName)
    {
        if (!source.TryGetPropertyValue(propertyName, out var value))
            return;

        target[propertyName] = value?.DeepClone();
    }
}
