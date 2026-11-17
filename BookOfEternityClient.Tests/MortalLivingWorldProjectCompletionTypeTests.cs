using System.Text.Json.Nodes;
using BookOfEternityClient.Services;
using Xunit;

namespace BookOfEternityClient.Tests;

/// <summary>Detached DATA contract only; no source authentication or publication authority.</summary>
public sealed class MortalLivingWorldProjectCompletionTypeTests
{
    [Fact]
    public void ProjectCompletionRejectsNumericStringCanonicalTurn()
    {
        const string projectsPath = "game_state/factions/faction_projects.json";
        const string factionsPath = "game_state/factions/faction_core.json";
        var before = JsonNode.Parse("""
            {"factionId":"guild","factionName":"Guild","projectId":"bridge",
             "projectName":"Bridge","activeState":"Active","description":"Build the crossing",
             "totalResourceCost":[],"resourcesSpent":[],"totalTimeCostMinutes":240,
             "timeSpentMinutes":240,"totalSteps":4,"currentStep":4}
            """)!.AsObject();
        var after = before.DeepClone().AsObject();
        after.Remove("activeState");
        after["finalState"] = "Completed";
        after["completionTurn"] = 7;
        var owners = JsonNode.Parse("""{"factions":[{"factionId":"guild"}]}""")!.AsObject();
        var original = new Dictionary<string, JsonObject>
        {
            [factionsPath] = owners.DeepClone().AsObject(),
            [projectsPath] = new JsonObject
            {
                ["activeProjects"] = new JsonArray(before.DeepClone()),
                ["completedProjects"] = new JsonArray()
            }
        };
        var current = new Dictionary<string, JsonObject>
        {
            [factionsPath] = owners,
            [projectsPath] = new JsonObject
            {
                ["activeProjects"] = new JsonArray(),
                ["completedProjects"] = new JsonArray(after)
            }
        };
        var report = JsonNode.Parse("""
            {"schemaVersion":1,"sessionId":"session","requestId":"request","turn":7,
             "fromMinute":0,"toMinute":240,"resolution":"changes","audit":[],"events":[
              {"eventId":"bridge_completed","fromMinute":0,"toMinute":240,
               "actors":[{"kind":"system","id":"worldCycle"}],
               "targets":[{"kind":"factionProject","id":"bridge","ownerId":"guild"}],
               "cause":{"summary":"The council finished the existing project.","references":[
                 {"kind":"factionProject","id":"bridge","ownerId":"guild"}]},
               "effects":[{"effectId":"finish_bridge","target":
                 {"kind":"factionProject","id":"bridge","ownerId":"guild"},
                 "operation":"complete","field":"entity",
                 "before":{"present":true},"after":{"present":true}}],
               "consequences":[{"summary":"The crossing can reopen.","effectIds":["finish_bridge"]}],
               "knowledge":{"player":"hidden","newsReferences":[]}}]}
            """)!.AsObject();
        var effect = report["events"]![0]!["effects"]![0]!;
        effect["before"]!["value"] = before.DeepClone();
        effect["after"]!["value"] = after.DeepClone();
        // Typed control proves the same real production entry point accepts this
        // otherwise complete, exactly witnessed active-to-completed transition.
        Assert.Empty(MortalLivingWorldEvents.Validate(report, original, current));

        after["completionTurn"] = "7";
        // Keep the declaration equal to actual canonical DATA. Rejection must be
        // about its invalid integer type, not a before/after evidence mismatch.
        effect["after"]!["value"] = after.DeepClone();

        var issue = Assert.Single(MortalLivingWorldEvents.Validate(report, original, current));

        Assert.Equal("mortal_living_world_lifecycle_invalid", issue.Code);
        Assert.Equal(IssueSeverity.Error, issue.Severity);
    }
}
