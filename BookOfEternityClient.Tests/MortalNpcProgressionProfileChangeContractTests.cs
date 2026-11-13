using System.Text.Json.Nodes;
using Xunit;
using Contract = BookOfEternityClient.Services.MortalNpcProgressionProfileChangeContract;

namespace BookOfEternityClient.Tests;

public sealed class MortalNpcProgressionProfileChangeContractTests
{
    [Fact]
    public void Install_AbsentProfile_PreservesAssetsAndUsesExplicitThreshold()
    {
        var original = Actor(); original.Remove("progressionProfile");
        var command = Command("install");
        var before = original.ToJsonString(); var raw = command.ToJsonString();
        var result = Contract.Evaluate(original, command);
        Assert.True(result.IsValid);
        Assert.Equal(2, result.CandidateActor!["level"]!.GetValue<int>());
        Assert.Equal(40, result.CandidateActor["experience"]!.GetValue<int>());
        Assert.Equal(600, result.CandidateActor["experienceForNextLevel"]!.GetValue<int>());
        Assert.True(JsonNode.DeepEquals(command["progressionProfileChange"]!["profile"], result.CandidateActor["progressionProfile"]));
        Assert.Equal(before, original.ToJsonString()); Assert.Equal(raw, command.ToJsonString());
    }

    [Theory]
    [InlineData("existing")]
    [InlineData("null")]
    [InlineData("malformed")]
    public void Install_RequiresTrueAbsenceNotPresentInvalidProfile(string kind)
    {
        var original = Actor();
        if (kind != "existing") original["progressionProfile"] = kind == "null" ? null : JsonValue.Create(3);
        Refused(Contract.Evaluate(original, Command("install")), "npc_profile_change_install_requires_absence");
    }

    [Fact]
    public void Install_ConfusableProfileProperty_IsNotAbsence()
    {
        var original = Actor(); var profile = original["progressionProfile"]!.DeepClone();
        original.Remove("progressionProfile"); original["progressionPr\u043efile"] = profile;
        Refused(Contract.Evaluate(original, Command("install")), "npc_profile_change_install_requires_absence");
    }

    [Fact]
    public void Replace_ChangesCurveWithoutReinterpretingStoredXp()
    {
        var result = Contract.Evaluate(Actor(), Command("replace"));
        Assert.True(result.IsValid);
        Assert.Equal(2, result.CandidateActor!["level"]!.GetValue<int>());
        Assert.Equal(40, result.CandidateActor["experience"]!.GetValue<int>());
        Assert.Equal(600, result.CandidateActor["experienceForNextLevel"]!.GetValue<int>());
        Assert.Equal("table", result.CandidateActor["progressionProfile"]!["mode"]!.GetValue<string>());
    }

    [Fact]
    public void Replace_WrongAuthoredThreshold_RefusesRatherThanCorrects()
    {
        var command = Command("replace"); command["progressionProfileChange"]!["experienceForNextLevel"] = 601;
        Refused(Contract.Evaluate(Actor(), command), "npc_profile_change_result_invalid");
    }

    [Theory]
    [InlineData("level", 3)]
    [InlineData("experience", 39)]
    public void Replace_CannotChangeStoredLevelOrXp(string field, int value)
    {
        var command = Command("replace"); command["progressionProfileChange"]![field] = value;
        Refused(Contract.Evaluate(Actor(), command), "npc_profile_change_assets_changed");
    }

    [Fact]
    public void Replace_NewCurveCannotImplicitlyAdvanceAtExactThreshold()
    {
        var command = Command("replace");
        command["progressionProfileChange"]!["profile"]!["thresholds"] = new JsonArray(10, 40, 100);
        command["progressionProfileChange"]!["experienceForNextLevel"] = 40;
        Refused(Contract.Evaluate(Actor(), command), "npc_profile_change_result_invalid");
    }

    [Fact]
    public void Replace_NewCapCannotDiscardStoredXp()
    {
        var command = Command("replace");
        command["progressionProfileChange"]!["profile"]!["thresholds"] = new JsonArray(100);
        command["progressionProfileChange"]!["experienceForNextLevel"] = 0;
        Refused(Contract.Evaluate(Actor(), command), "npc_profile_change_result_invalid");
    }

    [Fact]
    public void Replace_ToNoneWithZeroXp_PreservesLevel()
    {
        var original = Actor(); original["experience"] = 0;
        var command = Command("replace");
        command["progressionProfileChange"]!["experience"] = 0;
        command["progressionProfileChange"]!["experienceForNextLevel"] = 0;
        command["progressionProfileChange"]!["profile"] = None();
        var result = Contract.Evaluate(original, command);
        Assert.True(result.IsValid);
        Assert.Equal(2, result.CandidateActor!["level"]!.GetValue<int>());
        Assert.Equal(0, result.CandidateActor["experienceForNextLevel"]!.GetValue<int>());
        Assert.True(JsonNode.DeepEquals(None(), result.CandidateActor["progressionProfile"]));
    }

    [Fact]
    public void Replace_FromNoneWithPositiveLevel_UsesDeclaredCurve()
    {
        var original = Actor(); original["experience"] = 0; original["experienceForNextLevel"] = 0;
        original["progressionProfile"] = None();
        var command = Command("replace"); command["progressionProfileChange"]!["experience"] = 0;
        var result = Contract.Evaluate(original, command);
        Assert.True(result.IsValid);
        Assert.Equal(600, result.CandidateActor!["experienceForNextLevel"]!.GetValue<int>());
    }

    [Fact]
    public void Replace_FromLevelZeroNone_DoesNotInventLevelOne()
    {
        var original = Actor(); original["level"] = 0; original["experience"] = 0;
        original["experienceForNextLevel"] = 0; original["progressionProfile"] = None();
        var command = Command("replace"); command["progressionProfileChange"]!["level"] = 0;
        command["progressionProfileChange"]!["experience"] = 0;
        Refused(Contract.Evaluate(original, command), "npc_profile_change_result_invalid");
    }

    [Fact]
    public void ProfileChange_CannotShareRowWithXpProgression()
    {
        var command = Command("replace"); command["progression"] = new JsonObject();
        Refused(Contract.Evaluate(Actor(), command), "npc_profile_change_command_invalid");
    }

    [Fact]
    public void ProfileChange_RejectsUnknownPackageFields()
    {
        var command = Command("replace"); command["progressionProfileChange"]!["convertXp"] = true;
        Refused(Contract.Evaluate(Actor(), command), "npc_profile_change_command_invalid");
    }

    [Fact]
    public void ProfileChange_RequiresMandatoryConsequencesOnNewCurve()
    {
        var command = Command("replace"); command["progressionProfileChange"]!["profile"]!.AsObject().Remove("consequences");
        Refused(Contract.Evaluate(Actor(), command), "npc_profile_change_result_invalid");
    }

    [Fact]
    public void Replace_RejectsIncoherentOriginalTuple()
    {
        var original = Actor(); original["experienceForNextLevel"] = 500;
        Refused(Contract.Evaluate(original, Command("replace")), "npc_profile_change_original_invalid");
    }

    [Theory]
    [InlineData("absent")]
    [InlineData("null")]
    [InlineData("malformed")]
    public void Replace_RequiresPresentValidOriginalProfile(string state)
    {
        var original = Actor();
        if (state == "absent") original.Remove("progressionProfile");
        else original["progressionProfile"] = state == "null" ? null : JsonValue.Create(3);
        Refused(Contract.Evaluate(original, Command("replace")), "npc_profile_change_original_invalid");
    }

    [Fact]
    public void UnknownOperation_CannotBecomeInstallOrReplace()
    {
        Refused(Contract.Evaluate(Actor(), Command("reset")), "npc_profile_change_command_invalid");
    }

    [Fact]
    public void Replacement_PreservesAndDetachesUnrelatedActorPayload()
    {
        var original = Actor();
        original["passiveSkills"] = JsonNode.Parse("""[{"skillName":"Archive","masteryLevel":2}]""");
        original["inventory"] = JsonNode.Parse("""[{"itemId":"item_archive","quantity":3}]""");
        var command = Command("replace");
        var before = original.ToJsonString(); var raw = command.ToJsonString();
        var result = Contract.Evaluate(original, command);
        Assert.True(result.IsValid);
        Assert.Equal("npc_profile", result.CandidateActor!["NPCId"]!.GetValue<string>());
        Assert.True(JsonNode.DeepEquals(original["passiveSkills"], result.CandidateActor["passiveSkills"]));
        Assert.True(JsonNode.DeepEquals(original["inventory"], result.CandidateActor["inventory"]));
        Assert.True(JsonNode.DeepEquals(command["progressionProfileChange"]!["profile"], result.CandidateActor["progressionProfile"]));
        result.CandidateActor["passiveSkills"]![0]!["masteryLevel"] = 5;
        result.CandidateActor["inventory"]![0]!["quantity"] = 9;
        result.CandidateActor["progressionProfile"]!["thresholds"]![1] = 999;
        Assert.Equal(before, original.ToJsonString());
        Assert.Equal(raw, command.ToJsonString());
    }

    private static void Refused(Contract.Result result, string code)
    {
        Assert.False(result.IsValid); Assert.Null(result.CandidateActor);
        Assert.Contains(result.Problems, problem => problem.Code == code);
    }
    private static JsonObject None() => new() { ["schemaVersion"] = 1, ["mode"] = "none" };
    private static JsonObject Actor() => JsonNode.Parse("""
        {"NPCId":"npc_profile","level":2,"experience":40,"experienceForNextLevel":565,
         "progressionProfile":{"schemaVersion":1,"mode":"standard","consequences":{
           "skills":{"levelsPerAward":2,"allowedAwards":["newSkill","masteryIncrease"]},
           "characteristics":{"mode":"currentWorld"}}}}
        """)!.AsObject();
    private static JsonObject Command(string operation)
    {
        var command = JsonNode.Parse("""
            {"NPCId":"npc_profile","reason":"Adopt explicit curve","progressionProfileChange":{
             "source":{"sessionId":"session_profile","requestId":"request_profile","turnNumber":42},
             "operation":"replace","level":2,"experience":40,"experienceForNextLevel":600,
             "profile":{"schemaVersion":1,"mode":"table","thresholds":[100,600,1600],"capOverflow":"discard",
              "consequences":{"skills":{"levelsPerAward":2,"allowedAwards":["newSkill","masteryIncrease"]},
               "characteristics":{"mode":"currentWorld"}}}}}
            """)!.AsObject();
        command["progressionProfileChange"]!["operation"] = operation;
        return command;
    }
}
