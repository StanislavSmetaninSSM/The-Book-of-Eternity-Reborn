using System.Text.Json.Nodes;
using BookOfEternityClient.Services;
using Xunit;

namespace BookOfEternityClient.Tests;

public sealed class MortalNpcProgressionSourceBindingTests
{
    [Fact]
    public void ExistingProgression_WithoutTrustedTurnContext_IsRejected()
    {
        var actor = JsonNode.Parse("""
            {"NPCId":"npc_source","level":1,"experience":99,"experienceForNextLevel":100,
             "progressionProfile":{"schemaVersion":1,"mode":"standard","consequences":{"skills":{"levelsPerAward":2,"allowedAwards":["newSkill","masteryIncrease"]},"characteristics":{"mode":"currentWorld"}}}}
            """)!.AsObject();
        var before = new JsonObject { ["NPCsInScene"] = new JsonArray(actor) };
        var current = before.DeepClone().AsObject();
        current["NPCCoreChanges"] = JsonNode.Parse("""
            [{"NPCId":"npc_source","reason":"Earned two XP",
              "progression":{"experienceDelta":2,"level":2,"experience":1,"experienceForNextLevel":565}}]
            """);
        var authority = new NpcCoreChangesContract.Authority(
            new(StringComparer.Ordinal), new(StringComparer.Ordinal),
            new(StringComparer.Ordinal), new(StringComparer.Ordinal));

        var evaluated = NpcCoreChangesContract.Evaluate(current, before, authority,
            ValidationService.ValidateNpcCoreFateCardsAgainstProductionContract, detectDirectMutations: true);

        Assert.False(evaluated.CanApply);
    }

    [Fact]
    public void ExactSource_AppliesOnceAndDoesNotPersistPrivateCorrelation()
    {
        var (before, current) = Roots();
        var evaluated = Evaluate(before, current, Context());
        Assert.Empty(evaluated.Issues);
        Assert.True(evaluated.CanApply);
        NpcCoreChangesContract.Apply(current, evaluated);
        var actor = current["NPCsInScene"]![0]!.AsObject();
        Assert.Equal(2, actor["level"]!.GetValue<int>());
        Assert.Equal(1, actor["experience"]!.GetValue<int>());
        Assert.False(actor.ContainsKey("source"));
        Assert.DoesNotContain("private_snapshot", current.ToJsonString());
        var accepted = current.ToJsonString();
        var repeated = Evaluate(current.DeepClone().AsObject(), current, Context());
        Assert.False(repeated.HasCommand);
        NpcCoreChangesContract.Apply(current, repeated);
        Assert.Equal(accepted, current.ToJsonString());
    }

    [Theory]
    [InlineData("sessionId", "\"other_session\"")]
    [InlineData("sessionId", "\"session_source \"")]
    [InlineData("sessionId", "\"SESSION_SOURCE\"")]
    [InlineData("requestId", "\"other_request\"")]
    [InlineData("turnNumber", "41")]
    [InlineData("turnNumber", "42.0")]
    [InlineData("turnNumber", "\"42\"")]
    [InlineData("turnNumber", "2147483648")]
    [InlineData("snapshotToken", "\"private_snapshot\"")]
    public void AuthoredSource_MustExactlyMatchClosedCurrentRequest(string field, string json)
    {
        var (before, current) = Roots();
        current["NPCCoreChanges"]![0]!["progression"]!["source"]![field] = JsonNode.Parse(json);
        var evaluated = Evaluate(before, current, Context());
        Assert.False(evaluated.CanApply);
        Assert.Contains(evaluated.Issues, issue => issue.Code == "npc_core_changes_progression_source_invalid");
    }

    [Theory]
    [InlineData("chaos_sea")]
    [InlineData("shining_abode")]
    [InlineData("")]
    [InlineData("Mortal World")]
    public void TrustedContext_RequiresNormalizedMortalRealm(string realm)
    {
        var (before, current) = Roots();
        var evaluated = Evaluate(before, current, Context() with { Realm = realm });
        Assert.False(evaluated.CanApply);
        Assert.Contains(evaluated.Issues, issue => issue.Code == "npc_core_changes_progression_realm_invalid");
    }

    [Theory]
    [InlineData("sessionId")]
    [InlineData("requestId")]
    [InlineData("turnNumber")]
    public void AuthoredSource_RequiresEveryMember(string field)
    {
        var (before, current) = Roots();
        current["NPCCoreChanges"]![0]!["progression"]!["source"]!.AsObject().Remove(field);
        Assert.False(Evaluate(before, current, Context()).CanApply);
    }

    [Fact]
    public void SourcePreflight_RejectsBeforeReductionAndLeavesAllInputsUnchanged()
    {
        var (_, current) = Roots();
        var saved = current.ToJsonString();
        var issues = NpcCoreChangesContract.ValidateProgressionSources(current, Context() with { TurnNumber = 43 });
        Assert.Contains(issues, issue => issue.Code == "npc_core_changes_progression_source_invalid");
        Assert.Equal(saved, current.ToJsonString());
    }

    [Fact]
    public void UnrelatedCoreCommand_DoesNotRequireProgressionSource()
    {
        var (before, current) = Roots();
        var command = current["NPCCoreChanges"]![0]!.AsObject();
        command.Remove("progression");
        command["profile"] = new JsonObject { ["worldview"] = "Protect knowledge" };
        Assert.Empty(NpcCoreChangesContract.ValidateProgressionSources(current, null));
        Assert.True(Evaluate(before, current, null).CanApply);
    }

    internal static NpcCoreChangesContract.ProgressionSourceContext Context() =>
        new("session_source", "request_source", 42, "private_snapshot", "mortal_world");

    internal static (JsonObject Before, JsonObject Current) Roots()
    {
        var before = JsonNode.Parse("""
            {"NPCsInScene":[{"NPCId":"npc_source","level":1,"experience":99,"experienceForNextLevel":100,
              "progressionProfile":{"schemaVersion":1,"mode":"standard","consequences":{"skills":{"levelsPerAward":2,"allowedAwards":["newSkill","masteryIncrease"]},"characteristics":{"mode":"currentWorld"}}}}]}
            """)!.AsObject();
        var current = before.DeepClone().AsObject();
        current["NPCCoreChanges"] = JsonNode.Parse("""
            [{"NPCId":"npc_source","reason":"Earned two XP","progression":{
              "source":{"sessionId":"session_source","requestId":"request_source","turnNumber":42},
              "experienceDelta":2,"level":2,"experience":1,"experienceForNextLevel":565}}]
            """);
        return (before, current);
    }

    private static NpcCoreChangesContract.Evaluation Evaluate(JsonObject before, JsonObject current,
        NpcCoreChangesContract.ProgressionSourceContext? context) =>
        NpcCoreChangesContract.Evaluate(current, before, new NpcCoreChangesContract.Authority(
            new(StringComparer.Ordinal), new(StringComparer.Ordinal), new(StringComparer.Ordinal), new(StringComparer.Ordinal))
            { ProgressionSource = context }, ValidationService.ValidateNpcCoreFateCardsAgainstProductionContract,
            detectDirectMutations: true);
}
