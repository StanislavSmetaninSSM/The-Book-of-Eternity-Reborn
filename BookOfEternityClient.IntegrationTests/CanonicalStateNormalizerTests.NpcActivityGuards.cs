using System.Text.Json;
using System.Text.Json.Nodes;
using BookOfEternityClient.Services;
using Microsoft.Extensions.Logging.Abstractions;
using Xunit;

namespace BookOfEternityClient.Tests;

public sealed partial class CanonicalStateNormalizerTests
{
    [Theory]
    [InlineData("original", "conflicting-alias")]
    [InlineData("current", "conflicting-alias")]
    [InlineData("original", "null-alias")]
    [InlineData("current", "null-alias")]
    [InlineData("original", "generic-alias")]
    [InlineData("current", "generic-alias")]
    [InlineData("original", "case-variant-alias-key")]
    [InlineData("current", "case-variant-alias-key")]
    [InlineData("original", "case-variant-actor")]
    [InlineData("current", "case-variant-actor")]
    [InlineData("original", "duplicate-actor")]
    [InlineData("current", "duplicate-actor")]
    [InlineData("original", "divergent-mirrors")]
    [InlineData("current", "divergent-mirrors")]
    public async Task NormalizeAccumulatedStateAsync_NpcActivityGuard_InvalidIdentityFailsWithoutMutation(
        string source, string defect)
    {
        var original = CreateNpcActivityGuardCore();
        var current = original.DeepClone().AsObject();
        var malformed = source == "original" ? original : current;
        if (defect is "case-variant-actor" or "duplicate-actor")
        {
            var duplicate = malformed["UpdateNPCs"]![0]!.DeepClone().AsObject();
            if (defect == "case-variant-actor")
                duplicate["NPCId"] = "NPC_ACTIVITY_GUARD";
            malformed["UpdateNPCs"]!.AsArray().Add(duplicate);
        }
        else if (defect == "divergent-mirrors")
        {
            // Keep activity identical: divergence of another actor field must still
            // make this identity unsafe, not become a license to choose one copy.
            malformed["NPCsInScene"]![0]!["worldview"] = "Only this mirror changed.";
        }
        else
        {
            foreach (var section in new[] { "UpdateNPCs", "NPCsInScene" })
            {
                var actor = malformed[section]![0]!.AsObject();
                switch (defect)
                {
                    case "conflicting-alias":
                        actor["npcId"] = "npc_someone_else";
                        break;
                    case "null-alias":
                        actor["npcId"] = null;
                        break;
                    case "generic-alias":
                        actor["id"] = "npc_activity_guard";
                        break;
                    case "case-variant-alias-key":
                        actor["NPCID"] = "npc_activity_guard";
                        break;
                    default:
                        throw new ArgumentOutOfRangeException(nameof(defect), defect, null);
                }
            }
        }

        await AssertNpcActivityGuardRejectedWithoutMutationAsync(original, current,
            new JsonObject { ["timeSpentMinutes"] = 240 });
    }

    [Theory]
    [InlineData("totalTimeCostMinutes")]
    [InlineData("timeSpentMinutes")]
    [InlineData("currentStepNumber")]
    [InlineData("totalStepsInActivity")]
    [InlineData("Completed")]
    [InlineData("Abandoned")]
    [InlineData("unsupported-field")]
    public async Task NormalizeAccumulatedStateAsync_NpcActivityGuard_InvalidPatchFailsWithoutMutation(
        string defect)
    {
        var original = CreateNpcActivityGuardCore();
        var patch = defect switch
        {
            "Completed" or "Abandoned" => new JsonObject { ["activeState"] = defect },
            "unsupported-field" => new JsonObject { ["unrecognizedProgress"] = 1 },
            // All four canonical integer fields are Int32, not arbitrary Int64.
            _ => new JsonObject { [defect] = 2147483648L }
        };

        await AssertNpcActivityGuardRejectedWithoutMutationAsync(original,
            original.DeepClone().AsObject(), patch);
    }

    [Theory]
    [InlineData("missing-original-backup")]
    [InlineData("missing-original-actor")]
    [InlineData("missing-original-activity")]
    [InlineData("null-original-activity")]
    [InlineData("command-only-current-core")]
    [InlineData("missing-current-activity")]
    [InlineData("null-current-activity")]
    [InlineData("direct-activity-drift")]
    public async Task NormalizeAccumulatedStateAsync_NpcActivityGuard_InvalidBaselineFailsWithoutMutation(
        string defect)
    {
        JsonObject? original = CreateNpcActivityGuardCore();
        var current = original.DeepClone().AsObject();
        switch (defect)
        {
            case "missing-original-backup":
                original = null;
                break;
            case "missing-original-actor":
                original["UpdateNPCs"] = new JsonArray();
                original["NPCsInScene"] = new JsonArray();
                break;
            case "command-only-current-core":
                var actor = current["UpdateNPCs"]![0]!.AsObject();
                current["NPCCoreChanges"] = new JsonArray(new JsonObject
                {
                    ["NPCId"] = "npc_activity_guard",
                    ["reason"] = "Record the scout's verified observation policy.",
                    ["profile"] = new JsonObject
                    {
                        ["worldview"] = "Report only verified observations.",
                        ["race"] = actor["race"]!.DeepClone(),
                        ["history"] = actor["history"]!.DeepClone()
                    }
                });
                current.Remove("UpdateNPCs");
                current.Remove("NPCsInScene");
                break;
            case "missing-original-activity":
            case "null-original-activity":
            case "missing-current-activity":
            case "null-current-activity":
            case "direct-activity-drift":
                var affected = defect.Contains("original", StringComparison.Ordinal) ? original : current;
                foreach (var section in new[] { "UpdateNPCs", "NPCsInScene" })
                {
                    var affectedActor = affected[section]![0]!.AsObject();
                    if (defect.StartsWith("missing-", StringComparison.Ordinal))
                        affectedActor.Remove("currentActivity");
                    else if (defect.StartsWith("null-", StringComparison.Ordinal))
                        affectedActor["currentActivity"] = null;
                    else
                        affectedActor["currentActivity"]!["timeSpentMinutes"] = 15;
                }
                break;
            default:
                throw new ArgumentOutOfRangeException(nameof(defect), defect, null);
        }

        await AssertNpcActivityGuardRejectedWithoutMutationAsync(original, current,
            new JsonObject { ["timeSpentMinutes"] = 240 });
    }

    private static JsonObject CreateNpcActivityGuardCore()
    {
        var actor = EffectMaterializationTestFixture.CreateSameTurnMortalActor("npc_activity_guard");
        actor["NPCId"] = "npc_activity_guard";
        actor.Remove("initialId");
        actor["name"] = "Mira the scout";
        actor["materialization"]!["materializedAtTurn"] = 40;
        actor["currentActivity"] = new JsonObject
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
        actor["completedActivities"] = new JsonArray();
        actor[NpcTradeRequestState.ReceiptsProperty] = new JsonArray();
        return new JsonObject
        {
            ["UpdateNPCs"] = new JsonArray(actor.DeepClone()),
            ["NPCsInScene"] = new JsonArray(actor)
        };
    }

    private async Task AssertNpcActivityGuardRejectedWithoutMutationAsync(
        JsonObject? original, JsonObject current, JsonObject patch)
    {
        const string corePath = "game_state/npcs/npc_core.json";
        const string activitiesPath = "game_state/npcs/npc_activities.json";
        const string backupPath = "test_backups/npc_activity_guard_core.json";
        var formatting = new JsonSerializerOptions { WriteIndented = true };
        if (original is not null)
            await _fs.WriteFileAtomicAsync(backupPath, original.ToJsonString(formatting) + "\n");
        await _fs.WriteFileAtomicAsync(corePath, current.ToJsonString(formatting) + "\n");
        await _fs.WriteFileAtomicAsync("game_state/meta/soul_state.json",
            """{"currentRealm":"Mortal World","currentIncarnation":1}""");
        await _fs.WriteFileAtomicAsync(activitiesPath, new JsonObject
        {
            ["NPCActivityUpdates"] = new JsonArray(new JsonObject
            {
                ["NPCId"] = "npc_activity_guard",
                ["name"] = "Mira the scout",
                ["activityUpdate"] = patch
            }),
            ["completeNPCActivities"] = new JsonArray()
        }.ToJsonString(formatting) + "\n");
        var coreBefore = await _fs.ReadFileBytesAsync(corePath);
        var activitiesBefore = await _fs.ReadFileBytesAsync(activitiesPath);
        var originalBefore = await _fs.ReadFileBytesAsync(backupPath);
        Assert.NotNull(coreBefore);
        Assert.NotNull(activitiesBefore);
        var normalizer = new CanonicalStateNormalizer(_fs, NullLogger<CanonicalStateNormalizer>.Instance);

        // No accepted-plan authority is fabricated. Except for the explicit
        // command-only case, no core command can cause a different reducer's
        // validation failure to impersonate the activity preflight guard.
        await Assert.ThrowsAsync<InvalidDataException>(() => normalizer.NormalizeAccumulatedStateAsync(
            new Dictionary<string, string> { [corePath] = backupPath }));

        Assert.Equal(coreBefore, await _fs.ReadFileBytesAsync(corePath));
        Assert.Equal(activitiesBefore, await _fs.ReadFileBytesAsync(activitiesPath));
        Assert.Equal(originalBefore, await _fs.ReadFileBytesAsync(backupPath));
    }
}
