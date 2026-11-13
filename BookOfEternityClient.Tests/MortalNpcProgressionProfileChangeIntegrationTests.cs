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
