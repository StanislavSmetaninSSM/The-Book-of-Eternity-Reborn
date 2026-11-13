using System.Text.Json.Nodes;
using Xunit;
using Contract = BookOfEternityClient.Services.MortalNpcProgressionConsequences;

namespace BookOfEternityClient.Tests;

public sealed class MortalNpcProgressionConsequencesParsingTests
{
    [Theory]
    [InlineData("standard")]
    [InlineData("table")]
    public void ProgressingProfile_RequiresExplicitRule19Policy(string mode)
    {
        var profile = Profile(); profile["mode"] = mode;
        var text = profile.ToJsonString();
        var result = Contract.ParseProfile(profile);
        Assert.True(result.IsValid);
        Assert.True(result.Value!.Progresses);
        Assert.Equal(2, result.Value.LevelsPerAward);
        Assert.Equal(text, profile.ToJsonString());
    }

    [Fact]
    public void MissingConsequences_DoesNotInferCadence()
    {
        var profile = Profile(); profile.Remove("consequences");
        Refused(Contract.ParseProfile(profile));
    }

    [Fact]
    public void DifferentCadence_CannotWaiveMandatoryRule19()
    {
        var profile = Profile(); profile["consequences"]!["skills"]!["levelsPerAward"] = 3;
        Refused(Contract.ParseProfile(profile));
    }

    [Fact]
    public void MissingAwardOption_IsNotCompleteRule19Policy()
    {
        var profile = Profile(); profile["consequences"]!["skills"]!["allowedAwards"] = new JsonArray("newSkill");
        Refused(Contract.ParseProfile(profile));
    }

    [Fact]
    public void DuplicateAwardOptions_DoNotCountAsBothOptions()
    {
        var profile = Profile(); profile["consequences"]!["skills"]!["allowedAwards"] = new JsonArray("newSkill", "newSkill");
        Refused(Contract.ParseProfile(profile));
    }

    [Fact]
    public void NoneProfile_IsExplicitlyNonProgressing()
    {
        var result = Contract.ParseProfile(new JsonObject { ["schemaVersion"] = 1, ["mode"] = "none" });
        Assert.True(result.IsValid);
        Assert.False(result.Value!.Progresses);
    }

    [Fact]
    public void NoneProfile_CannotCarryProgressionConsequences()
    {
        var profile = Profile(); profile["mode"] = "none";
        Refused(Contract.ParseProfile(profile));
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public void AbsentWorldDeclaration_MeansNoCharacteristicGrowth(bool filePresent)
    {
        var result = Contract.ParseWorldPolicy(filePresent ? new JsonObject { ["worldType"] = "fantasy" } : null);
        Assert.True(result.IsValid);
        Assert.Equal(Contract.CharacteristicMode.None, result.Value!.Mode);
    }

    [Theory]
    [InlineData("null")]
    [InlineData("3")]
    public void MalformedPresentWorldDeclaration_IsNotAbsence(string json)
    {
        Refused(Contract.ParseWorldPolicy(new JsonObject { ["npcProgression"] = JsonNode.Parse(json) }));
    }

    [Fact]
    public void FixedWorldPolicy_PreservesExactAmountsAndDetachesData()
    {
        var root = World("""{"mode":"fixedIncrements","perLevel":{"strength":1,"endurance":2}}""");
        var result = Contract.ParseWorldPolicy(root);
        Assert.True(result.IsValid);
        Assert.Equal(Contract.CharacteristicMode.FixedIncrements, result.Value!.Mode);
        Assert.Equal(1, result.Value.PerLevel!["strength"]);
        Assert.Equal(2, result.Value.PerLevel["endurance"]);
        root["npcProgression"]!["characteristics"]!["perLevel"]!["strength"] = 99;
        Assert.Equal(1, result.Value.PerLevel["strength"]);
    }

    [Fact]
    public void AllocatedWorldPolicy_PreservesBudgetAndExactKeySet()
    {
        var result = Contract.ParseWorldPolicy(World("""{"mode":"allocatedPoints","pointsPerLevel":3,"allowedKeys":["strength","endurance"]}"""));
        Assert.True(result.IsValid);
        Assert.Equal(Contract.CharacteristicMode.AllocatedPoints, result.Value!.Mode);
        Assert.Equal(3, result.Value.PointsPerLevel);
        Assert.Equal(new[] { "endurance", "strength" }, result.Value.AllocationKeys!.OrderBy(key => key, StringComparer.Ordinal));
    }

    [Fact]
    public void UnknownWorldFormulaField_IsRejectedRatherThanInterpreted()
    {
        Refused(Contract.ParseWorldPolicy(World("""{"mode":"none","formula":"level * 3"}""")));
    }

    [Fact]
    public void AllocatedKeys_RejectWhitespaceInsteadOfNormalizingAuthority()
    {
        Refused(Contract.ParseWorldPolicy(World("""{"mode":"allocatedPoints","pointsPerLevel":3,"allowedKeys":[" strength"]}""")));
    }

    [Theory]
    [InlineData("NPCProgression")]
    [InlineData("npcPr\u043egression")]
    public void WorldOwnedSubtreeLookalike_DoesNotSilentlyBecomeAbsent(string property)
    {
        var root = World("""{"mode":"fixedIncrements","perLevel":{"strength":1}}""");
        var declaration = root["npcProgression"]!.DeepClone();
        root.Remove("npcProgression");
        root[property] = declaration;
        Refused(Contract.ParseWorldPolicy(root));
    }

    [Theory]
    [InlineData("fixedIncrements", "0")]
    [InlineData("fixedIncrements", "-1")]
    [InlineData("fixedIncrements", "1.5")]
    [InlineData("fixedIncrements", "2147483648")]
    [InlineData("allocatedPoints", "0")]
    [InlineData("allocatedPoints", "-1")]
    [InlineData("allocatedPoints", "1.5")]
    [InlineData("allocatedPoints", "2147483648")]
    public void WorldPolicy_RejectsNonpositiveFractionalOrOverflowPointAmounts(string mode, string amountJson)
    {
        var root = mode == "fixedIncrements"
            ? World("""{"mode":"fixedIncrements","perLevel":{"strength":1}}""")
            : World("""{"mode":"allocatedPoints","pointsPerLevel":1,"allowedKeys":["strength"]}""");
        var characteristics = root["npcProgression"]!["characteristics"]!.AsObject();
        if (mode == "fixedIncrements")
            characteristics["perLevel"]!["strength"] = JsonNode.Parse(amountJson);
        else
            characteristics["pointsPerLevel"] = JsonNode.Parse(amountJson);

        Refused(Contract.ParseWorldPolicy(root));
    }

    [Theory]
    [InlineData("fixedIncrements")]
    [InlineData("allocatedPoints")]
    public void WorldPolicy_RejectsConfusableDuplicateCharacteristicKeys(string mode)
    {
        const string confusableStrength = "str\u0435ngth"; // Cyrillic e must not introduce a second authority key.
        var root = mode == "fixedIncrements"
            ? World("""{"mode":"fixedIncrements","perLevel":{"strength":1}}""")
            : World("""{"mode":"allocatedPoints","pointsPerLevel":1,"allowedKeys":["strength"]}""");
        var characteristics = root["npcProgression"]!["characteristics"]!.AsObject();
        if (mode == "fixedIncrements")
            characteristics["perLevel"]![confusableStrength] = 2;
        else
            characteristics["allowedKeys"]!.AsArray().Add(confusableStrength);

        Refused(Contract.ParseWorldPolicy(root));
    }

    [Fact]
    public void AllocatedWorldPolicy_ReturnedKeysCannotBeChangedThroughSourceOrMutableInterface()
    {
        var root = World("""{"mode":"allocatedPoints","pointsPerLevel":3,"allowedKeys":["strength"]}""");
        var result = Contract.ParseWorldPolicy(root);
        Assert.True(result.IsValid);
        var keys = result.Value!.AllocationKeys!;

        root["npcProgression"]!["characteristics"]!["allowedKeys"]![0] = "endurance";
        if (keys is ICollection<string> mutableKeys)
            Assert.Throws<NotSupportedException>(() => mutableKeys.Add("invented"));

        Assert.Equal(new[] { "strength" }, keys.OrderBy(key => key, StringComparer.Ordinal));
    }

    private static void Refused<T>(Contract.ParseResult<T> result) where T : class
    {
        Assert.False(result.IsValid);
        Assert.Null(result.Value);
        Assert.NotEmpty(result.Problems);
    }
    private static JsonObject Profile() => JsonNode.Parse("""
        {"schemaVersion":1,"mode":"standard","consequences":{
          "skills":{"levelsPerAward":2,"allowedAwards":["newSkill","masteryIncrease"]},
          "characteristics":{"mode":"currentWorld"}}}
        """)!.AsObject();
    private static JsonObject World(string characteristics) => new()
    {
        ["worldType"] = "fantasy",
        ["npcProgression"] = new JsonObject { ["schemaVersion"] = 1, ["characteristics"] = JsonNode.Parse(characteristics) }
    };
}
