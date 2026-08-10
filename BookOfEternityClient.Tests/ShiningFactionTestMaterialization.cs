using System.Text.Json.Nodes;

namespace BookOfEternityClient.Tests;

internal static class ShiningFactionTestMaterialization
{
    public static JsonObject Apply(
        JsonObject faction,
        int materializedAtTurn,
        bool hasResidentAffiliations,
        bool canTrade,
        bool usesStoryState = false)
    {
        var factionId = faction["factionId"]?.GetValue<string>();
        if (string.IsNullOrWhiteSpace(factionId))
        {
            throw new ArgumentException(
                "A Shining faction test fixture requires factionId.",
                nameof(faction));
        }

        var hasProjects = HasObjectEntries(faction["projects"]);
        var hasInfluence = HasObjectEntries(
            faction["territorialInfluence"]);
        var hasResources = HasObjectEntries(faction["resourceLedger"]);
        var hasTradeContent = faction["tradeInventory"] is JsonObject ||
                              HasObjectEntries(
                                  faction["tradeInventoryReceipts"]);
        var hasLeadershipHistory = HasObjectEntries(
                                       faction["leadershipHistory"]) ||
                                   HasObjectEntries(
                                       faction["leadershipReceipts"]);

        faction["materialization"] = new JsonObject
        {
            ["schemaVersion"] = 1,
            ["materializationId"] =
                $"mat_test_{factionId}_turn_{materializedAtTurn}",
            ["factionType"] = "shining_faction",
            ["factionId"] = factionId,
            ["materializedAtTurn"] = materializedAtTurn,
            ["state"] = "complete",
            ["capabilities"] = new JsonObject
            {
                ["runsProjects"] = hasProjects,
                ["holdsTerritorialInfluence"] = hasInfluence,
                ["usesResourceLedger"] = hasResources,
                ["hasResidentAffiliations"] = hasResidentAffiliations,
                ["canTrade"] = canTrade,
                ["hasLeadershipHistory"] = hasLeadershipHistory,
                ["usesStoryState"] = usesStoryState
            },
            ["sections"] = new JsonObject
            {
                ["projects"] = Disposition(
                    hasProjects,
                    "This test faction has no authored projects."),
                ["territorialInfluence"] = Disposition(
                    hasInfluence,
                    "This test faction has no authored influence."),
                ["resourceLedger"] = Disposition(
                    hasResources,
                    "This test faction has no authored resource entries."),
                ["residentAffiliations"] = Disposition(
                    hasResidentAffiliations,
                    "This test faction has no resident affiliations."),
                ["trade"] = Disposition(
                    hasTradeContent,
                    "This test faction has no authored trade content."),
                ["leadershipHistory"] = Disposition(
                    hasLeadershipHistory,
                    "This test faction has no leadership history."),
                ["storyState"] = Disposition(
                    usesStoryState,
                    "This test faction has no story authority state.")
            }
        };
        return faction;
    }

    private static bool HasObjectEntries(JsonNode? node) =>
        node is JsonArray array && array.OfType<JsonObject>().Any();

    private static JsonObject Disposition(bool populated, string reason) =>
        populated
            ? new JsonObject { ["state"] = "populated" }
            : new JsonObject
            {
                ["state"] = "empty_by_design",
                ["reason"] = reason
            };
}
