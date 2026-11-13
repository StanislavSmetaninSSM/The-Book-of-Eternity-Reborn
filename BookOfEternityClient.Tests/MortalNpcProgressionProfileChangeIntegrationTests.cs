using System.Text.Json.Nodes;
using BookOfEternityClient.Services;
using Xunit;

namespace BookOfEternityClient.Tests;

public sealed class MortalNpcProgressionProfileChangeIntegrationTests
{
    [Theory]
    [InlineData("install")]
    [InlineData("replace")]
    public void ExactMortalSource_ProfileChangeUpdatesBothMirrorsAndConsumesCommand(string operation)
    {
        var (before, current) = Roots(operation, mirrors: true);
        var evaluated = Evaluate(before, current, Source());
        Assert.Empty(evaluated.Issues);
        Assert.True(evaluated.CanApply);
        NpcCoreChangesContract.Apply(current, evaluated);
        foreach (var section in new[] { "NPCsInScene", "UpdateNPCs" })
        {
            var actor = current[section]![0]!;
            Assert.Equal(2, actor["level"]!.GetValue<int>());
            Assert.Equal(40, actor["experience"]!.GetValue<int>());
            Assert.Equal(600, actor["experienceForNextLevel"]!.GetValue<int>());
            Assert.Equal("table", actor["progressionProfile"]!["mode"]!.GetValue<string>());
        }
        Assert.False(current.ContainsKey("NPCCoreChanges"));
    }

    [Theory]
    [InlineData("missing", "npc_core_changes_progression_source_unavailable")]
    [InlineData("stale", "npc_core_changes_progression_source_invalid")]
    [InlineData("realm", "npc_core_changes_progression_realm_invalid")]
    public void ProfileChange_RequiresExistingAuthenticatedSourceAndMortalRealm(string failure, string code)
    {
        var (before, current) = Roots("replace");
        var source = failure == "missing" ? null : failure == "realm" ? Source() with { Realm = "chaos_sea" } : Source();
        if (failure == "stale") current["NPCCoreChanges"]![0]!["progressionProfileChange"]!["source"]!["requestId"] = "stale_request";
        // Both public opening detection and the actual core evaluator must enforce the same source contract.
        Assert.True(NpcCoreChangesContract.HasProgressionCommand(current));
        Assert.Contains(NpcCoreChangesContract.ValidateProgressionSources(current, source), issue => issue.Code == code);
        var evaluated = Evaluate(before, current, source);
        Assert.False(evaluated.CanApply);
        Assert.Contains(evaluated.Issues, issue => issue.Code == code);
    }

    [Fact]
    public void ProfileChange_DirectCanonicalRewriteRemainsForbidden()
    {
        var (before, current) = Roots("replace");
        var change = current["NPCCoreChanges"]![0]!["progressionProfileChange"]!;
        current["NPCsInScene"]![0]!["progressionProfile"] = change["profile"]!.DeepClone();
        current["NPCsInScene"]![0]!["experienceForNextLevel"] = 600;
        var evaluated = Evaluate(before, current, Source());
        Assert.False(evaluated.CanApply);
        Assert.Contains(evaluated.Issues, issue => issue.Code == "npc_existing_core_direct_mutation_forbidden");
    }

    [Fact]
    public void AcceptedReplacement_SubsequentCoreTransitionUsesNewCurveWithoutRegrant()
    {
        // This is the pure existing core owner sequence, not a fabricated authenticated disk snapshot.
        var (original, accepted) = Roots("replace");
        var replacement = Evaluate(original, accepted, Source());
        Assert.Empty(replacement.Issues);
        Assert.True(replacement.CanApply);
        NpcCoreChangesContract.Apply(accepted, replacement);
        var nextOriginal = accepted.DeepClone().AsObject();
        var next = accepted.DeepClone().AsObject();
        var nextSource = Source() with { RequestId = "request_profile_next", TurnNumber = 43, SnapshotToken = "snapshot_profile_next" };
        next["NPCCoreChanges"] = JsonNode.Parse("""
            [{"NPCId":"npc_profile","reason":"Earn XP under accepted table","progression":{
              "source":{"sessionId":"session_profile","requestId":"request_profile_next","turnNumber":43},
              "experienceDelta":560,"level":3,"experience":0,"experienceForNextLevel":1600}}]
            """);
        var evaluated = Evaluate(nextOriginal, next, nextSource);
        Assert.Empty(evaluated.Issues);
        Assert.True(evaluated.CanApply);
        NpcCoreChangesContract.Apply(next, evaluated);
        var actor = next["NPCsInScene"]![0]!;
        Assert.Equal(3, actor["level"]!.GetValue<int>());
        Assert.Equal(0, actor["experience"]!.GetValue<int>());
        Assert.Equal(1600, actor["experienceForNextLevel"]!.GetValue<int>());
        Assert.Equal("table", actor["progressionProfile"]!["mode"]!.GetValue<string>());
        Assert.Empty(actor["activeSkills"]!.AsArray()); Assert.Empty(actor["passiveSkills"]!.AsArray());
        var published = next.ToJsonString();
        var repeated = Evaluate(next.DeepClone().AsObject(), next, nextSource);
        Assert.False(repeated.HasCommand);
        NpcCoreChangesContract.Apply(next, repeated);
        Assert.Equal(published, next.ToJsonString());
    }

    private static NpcCoreChangesContract.Evaluation Evaluate(JsonObject before, JsonObject current,
        NpcCoreChangesContract.ProgressionSourceContext? source) => NpcCoreChangesContract.Evaluate(current, before,
            new(new(StringComparer.Ordinal), new(StringComparer.Ordinal), new(StringComparer.Ordinal), new(StringComparer.Ordinal))
                { ProgressionSource = source }, ValidationService.ValidateNpcCoreFateCardsAgainstProductionContract, true);
    private static NpcCoreChangesContract.ProgressionSourceContext Source() =>
        new("session_profile", "request_profile", 42, "snapshot_profile", "mortal_world");

    private static (JsonObject Before, JsonObject Current) Roots(string operation, bool mirrors = false)
    {
        var actor = JsonNode.Parse("""
            {"NPCId":"npc_profile","level":2,"experience":40,"experienceForNextLevel":565,
             "progressionProfile":{"schemaVersion":1,"mode":"standard","consequences":{
               "skills":{"levelsPerAward":2,"allowedAwards":["newSkill","masteryIncrease"]},
               "characteristics":{"mode":"currentWorld"}}},
             "activeSkills":[],"passiveSkills":[],"characteristics":{}}
            """)!.AsObject();
        if (operation == "install") actor.Remove("progressionProfile");
        var before = new JsonObject { ["NPCsInScene"] = new JsonArray(actor) };
        if (mirrors) before["UpdateNPCs"] = new JsonArray(actor.DeepClone());
        var current = before.DeepClone().AsObject();
        var command = JsonNode.Parse("""
            {"NPCId":"npc_profile","reason":"Adopt explicit curve","progressionProfileChange":{
             "source":{"sessionId":"session_profile","requestId":"request_profile","turnNumber":42},
             "operation":"replace","level":2,"experience":40,"experienceForNextLevel":600,
             "profile":{"schemaVersion":1,"mode":"table","thresholds":[100,600,1600],"capOverflow":"discard",
              "consequences":{"skills":{"levelsPerAward":2,"allowedAwards":["newSkill","masteryIncrease"]},
               "characteristics":{"mode":"currentWorld"}}}}}
            """)!.AsObject();
        command["progressionProfileChange"]!["operation"] = operation;
        current["NPCCoreChanges"] = new JsonArray(command);
        return (before, current);
    }
}
