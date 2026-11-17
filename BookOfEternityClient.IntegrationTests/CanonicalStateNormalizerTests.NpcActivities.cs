using System.Text.Json.Nodes;
using BookOfEternityClient.Services;
using Microsoft.Extensions.Logging.Abstractions;
using Xunit;

namespace BookOfEternityClient.Tests;

public sealed partial class CanonicalStateNormalizerTests
{
    [Fact]
    public async Task NormalizeAccumulatedStateAsync_NpcActivityProgress_PreservesCoreChangesMirrorsAndReceipts()
    {
        const string corePath = "game_state/npcs/npc_core.json";
        const string activitiesPath = "game_state/npcs/npc_activities.json";
        const string backupPath = "test_backups/npc_activity_core.json";
        const string scoutId = "npc_activity_scout";
        const string siblingId = "npc_activity_sibling";
        const string changedWorldview = "Verified observations must be shared with the council.";
        var scout = ExistingActor(scoutId, "Mira the scout");
        scout["currentActivity"] = new JsonObject
        {
            ["activityName"] = "Inspect the crossing",
            ["description"] = "Observe the bridge supports before recommending passage.",
            ["activeState"] = "Active",
            ["totalTimeCostMinutes"] = 480,
            ["timeSpentMinutes"] = 0,
            ["currentStepNumber"] = 0,
            ["totalStepsInActivity"] = 2,
            ["linkedQuestId"] = null,
            ["linkedPlotOutlineNode"] = null
        };
        scout["completedActivities"] = new JsonArray(new JsonObject
        {
            ["activityName"] = "Read the flood report",
            ["completionTurn"] = 39,
            ["finalOutcome"] = "Success",
            ["narrativeSummary"] = "The scout recorded the earlier flood warning."
        });
        var sibling = ExistingActor(siblingId, "Council recorder");
        var original = new JsonObject
        {
            ["UpdateNPCs"] = new JsonArray(scout.DeepClone(), sibling.DeepClone()),
            ["NPCsInScene"] = new JsonArray(scout, sibling)
        };
        await _fs.WriteFileAtomicAsync(backupPath, original.ToJsonString());
        var current = original.DeepClone().AsObject();
        // Exercise the existing real core reducer first. The activity consumer must
        // preserve its result rather than rebuild the whole actor from the backup.
        current["NPCCoreChanges"] = new JsonArray(new JsonObject
        {
            ["NPCId"] = scoutId,
            ["reason"] = "The scout commits to sharing verified observations.",
            ["profile"] = new JsonObject
            {
                ["worldview"] = changedWorldview,
                ["race"] = scout["race"]!.DeepClone(),
                ["history"] = scout["history"]!.DeepClone()
            }
        });
        await _fs.WriteFileAtomicAsync(corePath, current.ToJsonString());
        await _fs.WriteFileAtomicAsync("game_state/meta/soul_state.json",
            """{"currentRealm":"Mortal World","currentIncarnation":1}""");
        await _fs.WriteFileAtomicAsync(activitiesPath, new JsonObject
        {
            ["NPCActivityUpdates"] = new JsonArray(new JsonObject
            {
                ["NPCId"] = scoutId,
                ["name"] = "Mira the scout",
                ["activityUpdate"] = new JsonObject
                {
                    ["timeSpentMinutes"] = 240,
                    ["currentStepNumber"] = 1
                }
            }),
            ["completeNPCActivities"] = new JsonArray()
        }.ToJsonString());
        var expected = original.DeepClone().AsObject();
        foreach (var section in new[] { "UpdateNPCs", "NPCsInScene" })
        {
            var expectedScout = Assert.Single(expected[section]!.AsArray().OfType<JsonObject>(),
                actor => actor["NPCId"]!.GetValue<string>() == scoutId);
            expectedScout["worldview"] = changedWorldview;
            expectedScout["currentActivity"]!["timeSpentMinutes"] = 240;
            expectedScout["currentActivity"]!["currentStepNumber"] = 1;
        }
        var normalizer = new CanonicalStateNormalizer(_fs, NullLogger<CanonicalStateNormalizer>.Instance);
        var backups = new Dictionary<string, string> { [corePath] = backupPath };

        // This is the actual component transform, not forged accepted-turn authority.
        // The separate real-engine journey remains responsible for turn admission.
        await normalizer.NormalizeAccumulatedStateAsync(backups);

        var normalizedJson = (await _fs.ReadFileAsync(corePath))!;
        var normalized = JsonNode.Parse(normalizedJson)!.AsObject();
        Assert.False(normalized.ContainsKey("NPCCoreChanges"));
        foreach (var section in new[] { "UpdateNPCs", "NPCsInScene" })
        {
            var normalizedScout = Assert.Single(normalized[section]!.AsArray().OfType<JsonObject>(),
                actor => actor["NPCId"]!.GetValue<string>() == scoutId);
            Assert.Equal(240, normalizedScout["currentActivity"]!["timeSpentMinutes"]!.GetValue<int>());
            Assert.Equal(1, normalizedScout["currentActivity"]!["currentStepNumber"]!.GetValue<int>());
            Assert.Equal(changedWorldview, normalizedScout["worldview"]!.GetValue<string>());
        }
        // Whole-root equality also protects the sibling, historical completed activity,
        // materialization receipts and every unrelated field in both canonical copies.
        Assert.True(JsonNode.DeepEquals(expected, normalized), normalizedJson);
        var consumedJson = await _fs.ReadFileAsync(activitiesPath);
        if (consumedJson is not null)
        {
            var consumed = JsonNode.Parse(consumedJson)!.AsObject();
            foreach (var command in new[] { "NPCActivityUpdates", "completeNPCActivities" })
                Assert.True(!consumed.ContainsKey(command) || consumed[command] is JsonArray { Count: 0 });
        }

        await normalizer.NormalizeAccumulatedStateAsync(backups);

        Assert.Equal(normalizedJson, await _fs.ReadFileAsync(corePath));
        Assert.Equal(original.ToJsonString(), await _fs.ReadFileAsync(backupPath));

        static JsonObject ExistingActor(string id, string name)
        {
            var actor = EffectMaterializationTestFixture.CreateSameTurnMortalActor(id);
            actor["NPCId"] = id;
            actor.Remove("initialId");
            actor["name"] = name;
            actor["materialization"]!["materializedAtTurn"] = 40;
            actor["currentActivity"] = null;
            actor["completedActivities"] = new JsonArray();
            actor[NpcTradeRequestState.ReceiptsProperty] = new JsonArray();
            return actor;
        }
    }
}
