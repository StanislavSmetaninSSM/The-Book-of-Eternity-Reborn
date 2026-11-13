using System.Text.Json.Nodes;
using BookOfEternityClient.Services;
using Xunit;

namespace BookOfEternityClient.Tests;

public sealed class MortalNpcProgressionContractTests
{
    [Fact]
    public void Evaluate_ImpossibleAbsoluteTuple_IsRejected() =>
        AssertRejected(Actor(), Command(0, 99, 0, 100), "level");

    [Theory]
    [InlineData(0, 1, 0, 100)]
    [InlineData(99, 1, 99, 100)]
    [InlineData(100, 2, 0, 565)]
    [InlineData(101, 2, 1, 565)]
    [InlineData(665, 3, 0, 1558)]
    [InlineData(2223, 4, 0, 3200)]
    public void Evaluate_StandardWithinLevelAndRecursiveExactBoundary(int delta, int level, int xp, int threshold) =>
        AssertAccepted(Actor(), Command(delta, level, xp, threshold), level, xp, threshold);

    [Theory]
    [InlineData(2, 565)]
    [InlineData(3, 1558)]
    [InlineData(4, 3200)]
    [InlineData(100, 10000000)]
    public void Evaluate_StandardThresholdUsesExactFloor(int level, int threshold) =>
        AssertAccepted(Actor(level: level, threshold: threshold), Command(0, level, 0, threshold), level, 0, threshold);

    [Fact]
    public void Evaluate_AddsExistingWithinLevelExperience() =>
        AssertAccepted(Actor(xp: 99), Command(2, 2, 1, 565), 2, 1, 565);

    [Theory]
    [InlineData(10, 2, 0, 20)]
    [InlineData(30, 3, 0, 0)]
    [InlineData(100, 3, 0, 0)]
    public void Evaluate_TableEndIsTerminalAndExplicitlyDiscardsExcess(int delta, int level, int xp, int threshold) =>
        AssertAccepted(Actor(Table(), threshold: 10), Command(delta, level, xp, threshold), level, xp, threshold);

    [Theory]
    [InlineData("{\"schemaVersion\":1,\"mode\":\"standard\",\"maxLevel\":2,\"capOverflow\":\"discard\"}", 100)]
    [InlineData("{\"schemaVersion\":1,\"mode\":\"table\",\"thresholds\":[100,200],\"maxLevel\":2,\"capOverflow\":\"discard\"}", 100)]
    public void Evaluate_ExplicitCapDiscardsRemainder(string profile, int threshold) =>
        AssertAccepted(Actor(Parse(profile), threshold: threshold), Command(1000, 2, 0, 0), 2, 0, 0);

    [Fact]
    public void Evaluate_AlreadyCappedDiscardsNewGain() =>
        AssertAccepted(Actor(Table(), level: 3, threshold: 0), Command(100, 3, 0, 0), 3, 0, 0);

    [Fact]
    public void Evaluate_NonePreservesArbitraryNonnegativeLevelAndZeroExperience() =>
        AssertAccepted(Actor(Parse("""{"schemaVersion":1,"mode":"none"}"""), level: 0, threshold: 0), Command(0, 0, 0, 0), 0, 0, 0);

    [Theory]
    [InlineData(1, 7)]
    [InlineData(0, 8)]
    public void Evaluate_NoneCannotGainExperienceOrChangeLevel(int delta, int finalLevel) =>
        Assert.False(MortalNpcProgressionContract.Evaluate(
            Actor(Parse("""{"schemaVersion":1,"mode":"none"}"""), level: 7, threshold: 0), Command(delta, finalLevel, 0, 0)).IsValid);

    [Fact]
    public void Evaluate_ExplicitLossNeverDelevels() =>
        AssertAccepted(Actor(Parse("""{"schemaVersion":1,"mode":"standard","allowExperienceLoss":true}"""), level: 2, xp: 5, threshold: 565), Command(-5, 2, 0, 565), 2, 0, 565);

    [Theory]
    [InlineData(false, -1)]
    [InlineData(true, -6)]
    [InlineData(true, int.MinValue)]
    public void Evaluate_DisallowedLossAndUnderflowAreRejected(bool allow, int delta)
    {
        var profile = Standard();
        profile["allowExperienceLoss"] = allow;
        AssertRejected(Actor(profile, xp: 5), Command(delta, 1, 0, 100), "experienceDelta");
    }

    [Fact]
    public void Evaluate_IntermediateExperienceOverflowIsRejectedEvenAtCap() =>
        AssertRejected(Actor(Table(), xp: 1, threshold: 10), Command(int.MaxValue, 3, 0, 0), "experienceDelta");

    [Theory]
    [InlineData("{}")]
    [InlineData("{\"mode\":\"standard\"}")]
    [InlineData("{\"schemaVersion\":2,\"mode\":\"standard\"}")]
    [InlineData("{\"schemaVersion\":1,\"mode\":\"Standard\"}")]
    [InlineData("{\"schemaVersion\":1,\"mode\":\"standard\",\"formula\":\"100*level\"}")]
    [InlineData("{\"schemaVersion\":1,\"mode\":\"standard\",\"allowExperienceLoss\":\"true\"}")]
    [InlineData("{\"schemaVersion\":1,\"mode\":\"standard\",\"maxLevel\":0,\"capOverflow\":\"discard\"}")]
    [InlineData("{\"schemaVersion\":1,\"mode\":\"standard\",\"maxLevel\":2}")]
    [InlineData("{\"schemaVersion\":1,\"mode\":\"standard\",\"capOverflow\":\"discard\"}")]
    [InlineData("{\"schemaVersion\":1,\"mode\":\"standard\",\"thresholds\":[100]}")]
    [InlineData("{\"schemaVersion\":1,\"mode\":\"standard\",\"maxLevel\":1000,\"capOverflow\":\"discard\"}")]
    [InlineData("{\"schemaVersion\":1,\"mode\":\"table\",\"thresholds\":[] ,\"capOverflow\":\"discard\"}")]
    [InlineData("{\"schemaVersion\":1,\"mode\":\"table\",\"thresholds\":[0],\"capOverflow\":\"discard\"}")]
    [InlineData("{\"schemaVersion\":1,\"mode\":\"table\",\"thresholds\":[1.5],\"capOverflow\":\"discard\"}")]
    [InlineData("{\"schemaVersion\":1,\"mode\":\"table\",\"thresholds\":[100],\"maxLevel\":3,\"capOverflow\":\"discard\"}")]
    [InlineData("{\"schemaVersion\":1,\"mode\":\"table\",\"thresholds\":[100],\"capOverflow\":\"retain\"}")]
    [InlineData("{\"schemaVersion\":1,\"mode\":\"none\",\"allowExperienceLoss\":false}")]
    public void Evaluate_MalformedClosedProfileIsRejected(string json) =>
        Assert.False(MortalNpcProgressionContract.Evaluate(Actor(Parse(json)), Command()).IsValid);

    [Fact]
    public void Evaluate_MissingProfileHasNoFallback()
    {
        var actor = Actor();
        actor.Remove("progressionProfile");
        AssertRejected(actor, Command(), "progressionProfile");
    }

    [Theory]
    [InlineData("level", "-1")]
    [InlineData("level", "1.0")]
    [InlineData("experience", "1.5")]
    [InlineData("experience", "\"0\"")]
    [InlineData("experienceForNextLevel", "2147483648")]
    [InlineData("experienceDelta", "1e0")]
    [InlineData("experienceDelta", "null")]
    [InlineData("experienceDelta", "2147483648")]
    public void Evaluate_CommandRequiresSupportedJsonIntegers(string field, string json)
    {
        var command = Command();
        command[field] = JsonNode.Parse(json);
        AssertRejected(Actor(), command, field);
    }

    [Theory]
    [InlineData("level", 0)]
    [InlineData("experience", -1)]
    [InlineData("experience", 100)]
    [InlineData("experienceForNextLevel", 101)]
    public void Evaluate_IncoherentBaselineIsRejected(string field, int value)
    {
        var actor = Actor();
        actor[field] = value;
        Assert.False(MortalNpcProgressionContract.Evaluate(actor, Command()).IsValid);
    }

    [Fact]
    public void Evaluate_FormulaBeyondIntegerRangeIsRejected() =>
        Assert.False(MortalNpcProgressionContract.Evaluate(Actor(level: int.MaxValue), Command()).IsValid);

    [Fact]
    public void Evaluate_CappedBaselineCannotRetainExperience() =>
        Assert.False(MortalNpcProgressionContract.Evaluate(Actor(Table(), level: 3, xp: 1, threshold: 0), Command(0, 3, 0, 0)).IsValid);

    [Fact]
    public void Evaluate_CompleteTupleAndDeltaAreRequired()
    {
        foreach (var field in new[] { "level", "experience", "experienceForNextLevel", "experienceDelta" })
        {
            var command = Command();
            command.Remove(field);
            AssertRejected(Actor(), command, field);
        }
    }

    [Fact]
    public void Evaluate_ProfileReplacementIsNotImplicitlyAccepted()
    {
        var command = Command();
        command["progressionProfile"] = Standard();
        AssertRejected(Actor(), command, "progressionProfile");
    }

    [Fact]
    public void Evaluate_DoesNotMutateInputsOrInferRoleMechanics()
    {
        var actor = Actor();
        var command = Command(100, 2, 0, 565);
        command["progressionType"] = "unfamiliar narrative role";
        command["lastPlayerXPValueOnSync"] = 40;
        var actorBefore = actor.ToJsonString();
        var commandBefore = command.ToJsonString();
        AssertAccepted(actor, command, 2, 0, 565);
        Assert.Equal(actorBefore, actor.ToJsonString());
        Assert.Equal(commandBefore, command.ToJsonString());
    }

    private static JsonObject Parse(string json) => JsonNode.Parse(json)!.AsObject();
    private static JsonObject Standard() => Parse("""{"schemaVersion":1,"mode":"standard"}""");
    private static JsonObject Table() => Parse("""{"schemaVersion":1,"mode":"table","thresholds":[10,20],"capOverflow":"discard"}""");
    private static JsonObject Actor(JsonObject? profile = null, int level = 1, int xp = 0, int threshold = 100) => new()
    {
        ["level"] = level, ["experience"] = xp, ["experienceForNextLevel"] = threshold,
        ["progressionProfile"] = profile ?? Standard()
    };
    private static JsonObject Command(int delta = 0, int level = 1, int xp = 0, int threshold = 100) => new()
    {
        ["experienceDelta"] = delta, ["level"] = level, ["experience"] = xp, ["experienceForNextLevel"] = threshold
    };
    private static void AssertAccepted(JsonObject actor, JsonObject command, int level, int xp, int threshold)
    {
        var result = MortalNpcProgressionContract.Evaluate(actor, command);
        Assert.Empty(result.Problems);
        Assert.True(result.IsValid);
        Assert.Equal(new MortalNpcProgressionContract.ProgressionTuple(level, xp, threshold), result.Value);
    }
    private static void AssertRejected(JsonObject actor, JsonObject command, string field)
    {
        var result = MortalNpcProgressionContract.Evaluate(actor, command);
        Assert.False(result.IsValid);
        Assert.Null(result.Value);
        Assert.Contains(result.Problems, p => p.Field == field);
    }
}
