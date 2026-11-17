using System.Text.Json;
using System.Text.Json.Nodes;
using BookOfEternityClient.Core;
using BookOfEternityClient.Services;
using Xunit;

namespace BookOfEternityClient.Tests;

public sealed partial class GameEngineTurnLifecycleTests
{
    private const string JourneyScoutId = "npc_journey_scout";
    private const string JourneyFactionId = "faction_journey_council";
    private const string JourneyProjectId = "project_journey_bridge";
    private const string JourneyThreatId = "threat_journey_crossing";
    private const string JourneyLocationId = "loc_valmont_private_chambers";

    // Initial canonical arrangement only. No signed turn exists yet; later turns must use
    // existing update commands and preserve these original materialization receipts.
    private static async Task SeedMortalLivingWorldJourneyActorsAsync(FileSystemManager files)
    {
        var map = JsonNode.Parse(await files.ReadFileAsync("game_state/world/world_map.json"))!.AsObject();
        var location = Assert.Single(map["locations"]!.AsArray()).AsObject();
        var locationName = location["name"]!.GetValue<string>();
        var scout = MortalActorTestFixtures.CreateActor(JourneyScoutId, JourneyLocationId, locationName);
        scout["name"] = "Mira the scout";
        scout["role"] = "Road observer";
        scout["summary"] = "A noncombatant observer reporting bridge safety to the local council.";
        scout["plans"] = "Observe the crossing and report changes to the council.";
        scout["currentActivity"] = JourneyScoutActivity();
        scout["completedActivities"] = new JsonArray();
        scout["teacherProfile"] = new JsonObject { ["canTeach"] = false, ["skills"] = new JsonArray() };
        scout["experienceForNextLevel"] = 0;
        scout["progressionProfile"] = new JsonObject { ["schemaVersion"] = 1, ["mode"] = "none" };
        scout["personalityTraits"] = new JsonArray(
            JourneyTrait("Observant", "Checks bridge supports before allowing travel.", 7),
            JourneyTrait("Cautious", "Reports uncertainty rather than promising safe passage.", 6),
            JourneyTrait("Dutiful", "Returns reports to the council.", 8));
        scout["materialization"] = new JsonObject
        {
            ["schemaVersion"] = 1, ["materializationId"] = "mat_journey_scout_40", ["actorType"] = "mortal_npc",
            ["actorId"] = JourneyScoutId, ["materializedAtTurn"] = 40, ["state"] = "complete",
            ["capabilities"] = new JsonObject { ["canFight"] = false, ["canTeach"] = false, ["canTrade"] = false, ["ownsItems"] = false },
            ["sections"] = new JsonObject
            {
                ["skills"] = JourneyEmpty("The observer has no mechanical skills."),
                ["inventory"] = JourneyEmpty("The observer carries no personal items."),
                ["fateCards"] = JourneyEmpty("No fate card has been revealed."),
                ["personalQuests"] = JourneyEmpty("No personal quest is active."),
                ["relationships"] = new JsonObject { ["state"] = "populated" }
            }
        };
        await Write("game_state/npcs/npc_core.json", new JsonObject
        { ["UpdateNPCs"] = new JsonArray(), ["NPCsInScene"] = new JsonArray(scout) });

        await Write("game_state/npcs/npc_journals.json", new JsonObject { ["NPCJournals"] = new JsonArray(new JsonObject
        {
            ["NPCId"] = JourneyScoutId, ["NPCName"] = "Mira the scout",
            ["journalEntries"] = new JsonArray(new JsonObject
            {
                ["entryId"] = "journey_scout_before_41", ["timestamp"] = "2026-01-01T00:00:00Z",
                ["event"] = "inspection_commission", ["description"] = "I will inspect the crossing before I tell the council it is safe.",
                ["emotionalImpact"] = "concern", ["relationshipChange"] = "0"
            })
        }) });

        var faction = JourneyFaction();
        faction["factionId"] = JourneyFactionId;
        faction.Remove("initialId"); faction.Remove("isNewFaction");
        faction["name"] = "Crossing Council";
        faction["currentAgenda"] = "Await the scout's bridge report before committing repair workers.";
        faction["materialization"]!["factionId"] = JourneyFactionId;
        faction["materialization"]!["materializationId"] = "fmat_journey_council_40";
        faction["materialization"]!["materializedAtTurn"] = 40;
        faction["materialization"]!["capabilities"]!["runsProjects"] = true;
        faction["materialization"]!["sections"]!["projects"] = new JsonObject { ["state"] = "populated" };
        var structure = JourneyFactionCompanion();
        foreach (var key in new[] { "governance", "leadership", "ranks", "structuredBonuses" })
        { structure[key] = faction[key]!.DeepClone(); faction.Remove(key); }
        // Canonical resources and structure live in their exact sidecars.
        faction.Remove("resources");
        foreach (var key in new[] { "playerBranch", "playerRank", "playerStrategyDirective", "reputationDescription" })
            faction[key] = null;
        var resources = JourneyFactionCompanion();
        resources["metaResources"] = new JsonArray(); resources["strategicGoods"] = new JsonArray();
        var custom = JourneyFactionCompanion(); custom["customStates"] = new JsonArray();
        foreach (var key in new[] { "activeProjects", "completedProjects", "customStates", "scribeChronicle" }) faction.Remove(key);
        await Write("game_state/factions/faction_core.json", new JsonObject { ["factions"] = new JsonArray(faction) });
        await Write("game_state/factions/faction_structure.json", new JsonObject { ["entries"] = new JsonArray(structure) });
        await Write("game_state/factions/faction_resources.json", new JsonObject { ["entries"] = new JsonArray(resources) });
        await Write("game_state/factions/faction_custom.json", new JsonObject { ["entries"] = new JsonArray(custom) });
        await Write("game_state/factions/faction_chronicles.json", new JsonObject { ["entries"] = new JsonArray(new JsonObject
        { ["factionId"] = JourneyFactionId, ["entry"] = "#40 - The council commissions inspection before repairing the crossing." }) });
        await Write("game_state/factions/faction_projects.json", new JsonObject
        { ["activeProjects"] = new JsonArray(JourneyBridgeProject()), ["completedProjects"] = new JsonArray() });

        location["activeThreats"] = new JsonArray(new JsonObject
        {
            ["threatId"] = JourneyThreatId, ["name"] = "Unstable crossing",
            ["description"] = "Floodwater has weakened the bridge supports near the estate.",
            ["intensity"] = 3, ["longTermGoal"] = "Collapse the crossing if the supports remain unrepaired.", ["currentActivity"] = null,
            ["threatArchetype"] = new JsonObject { ["motivation"] = "Preservation", ["method"] = "Covert", ["customMotivation"] = null, ["customMethod"] = null },
            ["impactProfile"] = new JsonObject { ["primaryTargetType"] = "Location", ["primaryTargetId"] = null,
                ["primaryTargetName"] = locationName, ["primaryImpact"] = "Stability", ["baseImpactValue"] = 2 }
        });
        location["materialization"]!["sections"]!["activeThreats"] = new JsonObject { ["disposition"] = "populated", ["reason"] = null };
        MortalLocationTestFixture.ResealCanonicalLocation(location);
        using (var document = JsonDocument.Parse(location.ToJsonString()))
            Assert.Empty(MortalLocationMaterializationContract.ValidateCanonicalLocation(document.RootElement, "journey initial location"));
        await Write("game_state/world/world_map.json", map);
        await Write("game_state/world/current_location.json", location);

        async Task Write(string path, JsonObject value) => await files.WriteFileAtomicAsync(path, value.ToJsonString());
    }

    private static string JourneyActorReasoning(int stage)
    {
        var observation = stage switch
        {
            0 => "Inspection advances and reveals weakened bridge supports; a public warning is issued.",
            1 => "The council allocates the already declared bridge project to repair; its existing work advances.",
            2 => "The repair completes and the crossing threat diminishes; the accepted completion becomes public news.",
            3 => "The bounded quiet interval adds no significant mechanical change; the repaired crossing remains under observation.",
            _ => throw new ArgumentOutOfRangeException(nameof(stage))
        };
        return $$"""
        ## NPC Scope
        - Mode: World-cycle
        - Relevant actors: Mira the scout, Crossing Council
        - Why relevant: The observer and council are the only autonomous participants in this bounded crossing journey.
        - Actors outside scope: нет
        - Why outside scope: No other canonical actor participates; no off-screen actor is inferred.

        ## Actor Brain
        ### Mira the scout
        - Current location: loc_valmont_private_chambers; remains at the estate observing the nearby crossing, without travel.
        - Situation: {{observation}}
        - Profile inputs: Observant 7, Cautious 6, Dutiful 8; a noncombatant road observer with no mechanical skills or owned items.
        - Motivation: I want travelers to receive verified information about crossing safety.
        - Constraints: I cannot repair the crossing alone or invent observations outside this interval.
        - Thoughts: I record only what I observed and preserve my earlier reports.
        - Strategy options:
        1. Observe and report the bounded result. Benefit: verified safety information. Risk: waiting delays travelers.
        2. Promise immediate safe passage without observation. Benefit: travelers leave sooner. Risk: unsupported advice exposes them to harm.
        - Chosen strategy: Observe and report the bounded result.
        - Rejected alternatives: An unsupported promise ignores my cautious profile and remaining uncertainty.
        - Actions: {{observation}}
        - State changes: Only the explicitly declared living-world effects apply; NPCJournals[].journalEntries[] appends my new first-person observation. No implicit skill, resource, movement or relationship changes.

        ### Crossing Council
        - Situation: {{observation}}
        - Profile inputs: Existing local road council; open-moot governance; an already active bridge repair project and no claimed territory, resource ledger or military capability.
        - Motivation: Maintain a reliable crossing without claiming unsupported safety.
        - Constraints: Only the existing project and recorded elapsed time may justify repair progress; no invented workers or resources.
        - Thoughts: We must distinguish an inspection warning, work progress, completion and an uneventful interval.
        - Strategy options:
        1. Act on the bounded inspection and project record. Benefit: accountable repair decisions. Risk: repair consumes elapsed time.
        2. Declare success without the project record. Benefit: immediate reassurance. Risk: false completion leaves the hazard unresolved.
        - Chosen strategy: Act on the bounded inspection and project record.
        - Rejected alternatives: Unrecorded success would contradict the canonical project and threat.
        - Actions: {{observation}}
        - State changes: Exactly the declared faction agenda, project and threat effects for this stage; public worldEventsLog proves disclosed consequences. A no-change stage has no mechanical effects. No membership, resource or territory change is inferred.
        """;
    }

    private async Task WriteJourneyScoutJournalAsync(int turn, int stage)
    {
        const string path = "game_state/npcs/npc_journals.json";
        var root = JsonNode.Parse(await _fs.ReadFileAsync(path))!.AsObject();
        var row = Assert.Single(root["NPCJournals"]!.AsArray().OfType<JsonObject>(),
            candidate => candidate["NPCId"]?.GetValue<string>() == JourneyScoutId);
        var description = stage switch
        {
            0 => "I inspected the crossing supports; I will warn the council before I recommend passage.",
            1 => "I observed the council's repair progress; I will keep travelers informed without claiming completion yet.",
            2 => "I observed the completed repair and reduced threat; I can now report the recorded result.",
            3 => "I watched the repaired crossing during this quiet interval; I found no new significant change to report.",
            _ => throw new ArgumentOutOfRangeException(nameof(stage))
        };
        row["journalEntries"]!.AsArray().Add(new JsonObject
        {
            ["entryId"] = $"journey_scout_turn_{turn}", ["timestamp"] = DateTime.UtcNow.ToString("O"),
            ["event"] = $"crossing_journey_stage_{stage}", ["description"] = description,
            ["emotionalImpact"] = stage < 2 ? "concern" : "relief", ["relationshipChange"] = "0"
        });
        await _fs.WriteFileAtomicAsync(path, root.ToJsonString());
    }

    private static JsonObject JourneyScoutActivity() => new()
    {
        ["activityName"] = "Inspect the crossing", ["description"] = "Observe the bridge supports and floodwater from the estate.",
        ["activeState"] = "Active", ["totalTimeCostMinutes"] = 480, ["timeSpentMinutes"] = 0,
        ["currentStepNumber"] = 0, ["totalStepsInActivity"] = 2,
        ["linkedQuestId"] = null, ["linkedPlotOutlineNode"] = null
    };

    private static JsonObject JourneyBridgeProject() => new()
    {
        ["factionId"] = JourneyFactionId, ["factionName"] = "Crossing Council", ["projectId"] = JourneyProjectId,
        ["projectName"] = "Repair the crossing", ["activeState"] = "Active", ["description"] = "Inspect and reinforce bridge supports before reopening travel.",
        ["totalResourceCost"] = new JsonArray(), ["resourcesSpent"] = new JsonArray(), ["totalTimeCostMinutes"] = 480,
        ["timeSpentMinutes"] = 0, ["totalSteps"] = 2, ["currentStep"] = 0
    };
    private static JsonObject JourneyFactionCompanion() => new() { ["factionId"] = JourneyFactionId, ["factionName"] = "Crossing Council" };
    private static JsonObject JourneyTrait(string name, string description, int value) => new()
    { ["traitName"] = name, ["description"] = description, ["value"] = value, ["valueDescription"] = "A stable explicit personality disposition." };
    private static JsonObject JourneyEmpty(string reason) => new() { ["state"] = "empty_by_design", ["reason"] = reason };
    private static JsonObject JourneyFaction() =>
        new()
        {
            ["factionId"] = null,
            ["initialId"] = "temp-faction-watch",
            ["isNewFaction"] = true,
            ["name"] = "Wayfarer Watch",
            ["description"] = "A small watch formed to keep one road safe.",
            ["image_prompt"] = "weathered road wardens beneath a wooden watchtower",
            ["factionColor"] = "#7B6852",
            ["purpose"] = "Keep the old western road open.",
            ["currentAgenda"] = "Repair the bridge before the spring thaw.",
            ["principles"] = new JsonArray(
                "Every traveler receives warning before judgment."),
            ["memory"] = new JsonObject
            {
                ["summary"] = "The watch formed after the bridge massacre.",
                ["lastUpdatedTurn"] = 12,
                ["enduringFacts"] = new JsonArray(
                    "The first wardens were caravan survivors."),
                ["openThreads"] = new JsonArray(
                    "The bridge attackers were never identified.")
            },
            ["governance"] = new JsonObject
            {
                ["model"] = "Open moot",
                ["decisionProcess"] = "Active wardens decide by simple majority."
            },
            ["leadership"] = new JsonObject
            {
                ["leadershipState"] = "vacant",
                ["summary"] = "The founder died and no successor has been chosen.",
                ["leaderNpcIds"] = new JsonArray()
            },
            ["powerProfile"] = new JsonObject
            {
                ["military"] = 0,
                ["economic"] = 0,
                ["social"] = 0,
                ["covert"] = 0,
                ["logistics"] = 0,
                ["stability"] = 0,
                ["arcane_tech"] = 0,
                ["exploration"] = 0
            },
            ["ranks"] = new JsonObject
            {
                ["branches"] = new JsonArray()
            },
            ["structuredBonuses"] = new JsonArray(),
            ["resources"] = new JsonObject
            {
                ["metaResources"] = new JsonArray(),
                ["strategicGoods"] = new JsonArray()
            },
            ["relations"] = new JsonArray(),
            ["activeProjects"] = new JsonArray(),
            ["completedProjects"] = new JsonArray(),
            ["controlledTerritories"] = new JsonArray(),
            ["customStates"] = new JsonArray(),
            ["scribeChronicle"] = new JsonArray(
                "#12 - The Wayfarer Watch took responsibility for the western road."),
            ["isPlayerFaction"] = false,
            ["isPlayerMember"] = false,
            ["playerRank"] = null,
            ["playerBranch"] = null,
            ["playerStrategyDirective"] = null,
            ["reputation"] = 0,
            ["reputationDescription"] = null,
            ["level"] = 1,
            ["experience"] = 0,
            ["experienceForNextLevel"] = 100,
            ["developmentArchetype"] = "Custodian",
            ["materialization"] = JourneyFactionEnvelope(
                "temp-faction-watch",
                "fmat_watch_creation")
        };

    private static JsonObject JourneyFactionEnvelope(
        string factionId,
        string materializationId) =>
        new()
        {
            ["schemaVersion"] = 1,
            ["materializationId"] = materializationId,
            ["factionType"] = "mortal_faction",
            ["factionId"] = factionId,
            ["materializedAtTurn"] = 12,
            ["state"] = "complete",
            ["capabilities"] = new JsonObject
            {
                ["hasFormalHierarchy"] = false,
                ["usesFactionResources"] = false,
                ["maintainsRelations"] = false,
                ["runsProjects"] = false,
                ["holdsTerritoryOrInfluence"] = false,
                ["supportsPlayerMembership"] = false,
                ["usesCustomMechanics"] = false
            },
            ["sections"] = new JsonObject
            {
                ["hierarchy"] = JourneyEmpty("No ranks exist yet."),
                ["resources"] = JourneyEmpty("No formal resources exist yet."),
                ["relations"] = JourneyEmpty("No formal relations exist yet."),
                ["projects"] = JourneyEmpty("No projects exist yet."),
                ["territoryAndInfluence"] = JourneyEmpty("No territory is claimed."),
                ["playerMembership"] = JourneyEmpty("The player is not a member."),
                ["customStates"] = JourneyEmpty("No custom state exists.")
            }
        };

}
