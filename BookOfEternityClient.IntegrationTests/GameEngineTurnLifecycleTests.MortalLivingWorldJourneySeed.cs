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
    private static Task SeedMortalLivingWorldJourneyActorsAsync(FileSystemManager files)
        => MortalLiveSeedBuilder.SeedActorsAsync(files);

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

}
