using System.Text.Json.Nodes;
using BookOfEternityClient.Services;

namespace BookOfEternityClient.Tests;

/// <summary>Independent per-test parsed DATA for the faction service boundary, not a complete Engine world.</summary>
internal sealed record ShiningFactionServiceInputs(JsonObject Shining, JsonObject Residents, JsonObject Profiles);

/// <summary>Small independent roster/profile inputs; no filesystem, owner, permission or shared mutable template.</summary>
internal static class ShiningFactionHealingServiceTestData
{
    internal const string FactionId = "faction_public_healing";
    internal const string HallId = "hall_public_healing";

    /// <summary>Creates fresh exact current membership and complete actor envelopes for each test.</summary>
    /// <param name="providers">Number of independent explicitly public healers in one current faction.</param>
    /// <returns>Mutable caller-owned inputs for the pure service projection/admission boundary only.</returns>
    internal static ShiningFactionServiceInputs Create(int providers = 1)
    {
        var sections = new JsonObject();
        foreach (var section in new[] { "projects", "territorialInfluence", "resourceLedger", "trade", "leadershipHistory", "storyState" })
            sections[section] = new JsonObject { ["state"] = "empty_by_design", ["reason"] = "Not used by this independent service input." };
        sections["residentAffiliations"] = new JsonObject { ["state"] = "populated" };
        var faction = new JsonObject
        {
            ["factionId"] = FactionId, ["hallId"] = HallId, ["visibility"] = "revealed",
            ["factionLifecycle"] = new JsonObject { ["state"] = "active" },
            ["materialization"] = new JsonObject
            {
                ["schemaVersion"] = 1, ["materializationId"] = "fmat_public_healing", ["factionType"] = "shining_faction",
                ["factionId"] = FactionId, ["materializedAtTurn"] = 42, ["state"] = "complete",
                ["capabilities"] = new JsonObject
                {
                    ["runsProjects"] = false, ["holdsTerritorialInfluence"] = false, ["usesResourceLedger"] = false,
                    ["hasResidentAffiliations"] = true, ["canTrade"] = false, ["hasLeadershipHistory"] = false, ["usesStoryState"] = false
                },
                ["sections"] = sections
            }
        };
        var shining = new JsonObject
        {
            ["availability"] = ShiningAbodeState.AvailabilityActive, ["currentHallId"] = HallId,
            ["halls"] = new JsonArray(new JsonObject { ["hallId"] = HallId, ["hallName"] = "Зал исцеления" }),
            ["factions"] = new JsonArray(faction)
        };
        var residents = new JsonObject { ["entries"] = new JsonArray(), ["thoughtJournal"] = new JsonArray() };
        var profiles = new JsonObject { ["profiles"] = new JsonArray() };
        for (var index = 0; index < providers; index++)
        {
            var id = "resident_public_healer_" + index;
            residents["entries"]!.AsArray().Add(new JsonObject
            {
                ["residentId"] = id, ["displayName"] = "Целитель " + (index + 1), ["ascensionState"] = "ascended",
                ["shiningFactionId"] = FactionId, ["residentRole"] = "healing_support", ["isPresent"] = false
            });
            var profile = AfterlifeActorMaterializationTestFixture.CreateCompleteProfile("resident", id, "Shining Abode", 42);
            profile["lifeState"] = "alive"; profile["visibility"] = "public"; profile["locationId"] = HallId;
            profile["locationName"] = "Зал исцеления";
            profile["standardArts"]!["spiritual_healing"] = 3;
            profile["healingServiceProfile"] = new JsonObject
            {
                ["providerId"] = id, ["realm"] = "shining_abode", ["locationRef"] = HallId,
                ["visibility"] = "public", ["availability"] = "available", ["minimumRelationship"] = null,
                ["priceMultiplierPercent"] = 101, ["compensationKinds"] = new JsonArray("ink_feathers"), ["accessConditions"] = new JsonArray()
            };
            profiles["profiles"]!.AsArray().Add(profile);
        }
        return new(shining, residents, profiles);
    }
}
