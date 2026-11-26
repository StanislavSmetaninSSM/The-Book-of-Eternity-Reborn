using System.Text.Json.Nodes;
using BookOfEternityClient.Core;
using BookOfEternityClient.Services;

namespace BookOfEternityClient.Tests;

internal static partial class MortalLiveSeedBuilder
{
    private const string TownId = "loc_journey_town";
    private const string TownName = "Eastgate";
    private const string ForestId = "loc_journey_forest";
    private const string ForestName = "Alderwood waystation";
    private const string CaptainId = "npc_journey_captain";
    private const string CaptainName = "Captain Irina";
    private const string CourierId = "npc_journey_courier";
    private const string CourierName = "Lev the courier";
    private const string GuildId = "faction_journey_guild";

    private static JsonObject CreateOffscreenNpcCore(JsonObject completeFixture) => new()
    {
        ["UpdateNPCs"] = new JsonArray(
            OffscreenNpc(completeFixture, CaptainId, CaptainName, "Guard captain", "Investigate the Eastgate murder",
                "Compare witness statements and the watch ledger.", 480),
            OffscreenNpc(completeFixture, CourierId, CourierName, "Courier", "Carry the guild letters to Alderwood",
                "Take the existing Eastgate footpath. The walk takes 120 game minutes.", 120)),
        ["NPCsInScene"] = new JsonArray()
    };

    private static JsonObject OffscreenNpc(JsonObject template, string id, string name, string role,
        string activity, string description, int minutes)
    {
        var actor = template.DeepClone().AsObject();
        actor["NPCId"] = id;
        actor["name"] = name;
        actor["role"] = role;
        actor["summary"] = description;
        actor["plans"] = description;
        actor["currentLocationId"] = TownId;
        actor["currentLocationName"] = TownName;
        actor["currentActivity"] = OffscreenActivity(activity, description, minutes);
        actor["materialization"]!["materializationId"] = "mat_" + id + "_40";
        actor["materialization"]!["actorId"] = id;
        return actor;
    }

    private static JsonArray CreateOffscreenJournals(JsonObject template) => new(
        OffscreenJournal(template, CaptainId, CaptainName, "The watch assigned me to investigate the Eastgate murder."),
        OffscreenJournal(template, CourierId, CourierName, "I have collected the guild letters but have not left Eastgate."));

    private static JsonObject OffscreenJournal(JsonObject template, string id, string name, string description)
    {
        var journal = template.DeepClone().AsObject();
        journal["NPCId"] = id;
        journal["NPCName"] = name;
        var entry = journal["journalEntries"]![0]!;
        entry["entryId"] = id + "_before_41";
        entry["description"] = description;
        return journal;
    }

    private static JsonObject OffscreenActivity(string name, string description, int minutes)
    {
        var activity = JourneyScoutActivity();
        activity["activityName"] = name;
        activity["description"] = description;
        activity["totalTimeCostMinutes"] = minutes;
        return activity;
    }

    private static JsonObject GuildRoadProject(string name)
    {
        var project = JourneyBridgeProject();
        project["factionId"] = GuildId;
        project["factionName"] = name;
        project["projectId"] = "project_journey_road";
        project["projectName"] = "Build the Eastgate–Alderwood road";
        project["description"] = "Improve the existing footpath between Eastgate and Alderwood into a market road.";
        project["totalTimeCostMinutes"] = 2880;
        return project;
    }

    private static async Task WriteOffscreenMapAsync(FileSystemManager files, JsonObject map)
    {
        var town = MortalLocationTestFixture.CreateCanonicalLocationWithIdentity(TownId, TownName, x: 24);
        var forest = MortalLocationTestFixture.CreateCanonicalLocationWithIdentity(ForestId, ForestName, x: 28);
        town["description"] = "The watch and market of Eastgate stand at the trail to Alderwood.";
        forest["description"] = "A forest waystation at the end of the footpath from Eastgate.";
        forest["customStates"] = new JsonArray(new JsonObject { ["kind"] = "forest_condition", ["infectionStage"] = 0 });
        forest["activeThreats"] = new JsonArray(new JsonObject
        {
            ["threatId"] = "threat_journey_spores",
            ["name"] = "Chaos spores",
            ["description"] = "An unnatural patch of spores at the edge of the wood.",
            ["intensity"] = 1,
            ["longTermGoal"] = "Spread through the woodland around the waystation.",
            ["currentActivity"] = OffscreenActivity("Spread into Alderwood", "Advance into the forest away from the player.", 480),
            ["threatArchetype"] = new JsonObject
            {
                ["motivation"] = "Corruption", ["method"] = "Covert", ["customMotivation"] = null, ["customMethod"] = null
            },
            ["impactProfile"] = new JsonObject
            {
                ["primaryTargetType"] = "Location", ["primaryTargetId"] = ForestId,
                ["primaryTargetName"] = ForestName, ["primaryImpact"] = "Environment", ["baseImpactValue"] = 2
            }
        });
        var link = MortalLocationTestFixture.CreateCanonicalLink(TownId, ForestId);
        link["name"] = "Eastgate to Alderwood footpath";
        link["description"] = "An open footpath from Eastgate to Alderwood, 120 minutes on foot.";
        foreach (var location in new[] { town, forest })
            location["materialization"]!["sections"]!["topology"] = Populated();
        forest["materialization"]!["sections"]!["customStates"] = Populated();
        forest["materialization"]!["sections"]!["activeThreats"] = Populated();
        foreach (var entity in new[] { town, forest, link })
            MortalLocationTestFixture.SetFixtureSourceTurn(entity, MortalLiveSeedExporter.SetupTurn - 1);

        var index = JsonNode.Parse((await files.ReadFileAsync(MortalLocationIdentityState.StatePath))!)!.AsObject();
        var townIndex = MortalLocationTestFixture.CreateIdentityIndex(town, link);
        var forestIndex = MortalLocationTestFixture.CreateIdentityIndex(forest);
        foreach (var source in new[] { townIndex, forestIndex })
            foreach (var key in new[] { "locationEntries", "linkEntries" })
                foreach (var row in source[key]!.AsArray())
                    index[key]!.AsArray().Add(row!.DeepClone());
        map["locations"]!.AsArray().Add(town);
        map["locations"]!.AsArray().Add(forest);
        map["links"]!.AsArray().Add(link);
        await files.WriteFileAtomicAsync(MortalLocationIdentityState.StatePath, index.ToJsonString());
        await files.WriteFileAtomicAsync(MortalLocationMaterializationContract.WorldMapPath, map.ToJsonString());
    }

    private static JsonObject Populated() => new() { ["disposition"] = "populated", ["reason"] = null };
}
