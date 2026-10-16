using System.Text;
using System.Text.Json.Nodes;
using BookOfEternityClient.Configuration;
using BookOfEternityClient.Core;
using BookOfEternityClient.Services;
using Microsoft.Extensions.Logging.Abstractions;
using Xunit;
using Xunit.Abstractions;
using M = BookOfEternityClient.Services.MortalQteDecisionPublication;

namespace BookOfEternityClient.Tests;

public sealed class MortalJointAutomaticCalculationTests(ITestOutputHelper output)
{
    [Fact]
    public void OrderedDuplicates_ReachCapThenBlockWithoutExperienceCompensation()
    {
        var stats = Stats(); stats["strength"] = 3; stats["dexterity"] = 100;
        var input = Inputs(stats, "{\"statsIncreased\":[\" Strength \",\"strength\",\"DEXTERITY\",\"unknown\"],\"retained\":{\"n\":7}}",
            "{\"unspentStatPoints\":7,\"levelUpStatPointsAwardedThroughLevel\":1,\"note\":\"keep\"}", "{\"level\":2}");
        var plan = CharacteristicsService.CalculateMortalJointAutomatic(input);
        Assert.Equal(2, plan.TrainingLevel); Assert.Equal(2, plan.AwardLevel);
        Assert.Equal([" Strength ", "strength", "DEXTERITY", "unknown"], plan.OrderedStats);
        Assert.Equal(["strength"], plan.AppliedStats);
        Assert.Equal(["strength", "dexterity"], plan.BlockedStats);
        Assert.Equal(["unknown"], plan.IgnoredStats);
        Assert.Equal(5, plan.PointsAwarded);
        var afterStats = Read(plan, M.JointAutomaticInputs.CharacteristicsPath);
        Assert.Equal(4, afterStats["strength"]!.GetValue<int>());
        Assert.Equal(100, afterStats["dexterity"]!.GetValue<int>());
        Assert.Equal(Characteristics.All.Length, afterStats.Count);
        Assert.Null(Read(plan, M.JointAutomaticInputs.StatusChangesPath)["statsIncreased"]);
        Assert.Equal(7, Read(plan, M.JointAutomaticInputs.StatusChangesPath)["retained"]!["n"]!.GetValue<int>());
        var points = Read(plan, M.JointAutomaticInputs.StatPointsPath);
        Assert.Equal(12, points["unspentStatPoints"]!.GetValue<int>());
        Assert.Equal(2, points["levelUpStatPointsAwardedThroughLevel"]!.GetValue<int>());
        Assert.Equal("keep", points["note"]!.GetValue<string>());
        Assert.Equal(3, plan.AfterImages.Count);
        Assert.DoesNotContain("game_state/player/experience.json", plan.AfterImages.Keys);
    }

    [Theory]
    [InlineData("{\"level\":1}", "{\"level\":3}", "{\"level\":7}", 1, 3)]
    [InlineData("{\"playerLevel\":2}", "{\"level\":4}", "{\"level\":7}", 2, 2)]
    [InlineData("{\"level\":4,\"playerLevel\":9}", "{\"level\":7}", null, 4, 4)]
    [InlineData(null, null, "{\"level\":4}", 4, 1)]
    [InlineData("{}", "{\"level\":3}", "{\"level\":7}", 3, 3)]
    [InlineData(null, null, null, 1, 1)]
    public void TrainingAndAward_KeepTheirDifferentActualSourcePriorities(string? experience, string? player, string? soul,
        int expectedTraining, int expectedAward)
    {
        var plan = CharacteristicsService.CalculateMortalJointAutomatic(Inputs(Stats(), null, Points(1, 0), experience, player, soul));
        Assert.Equal(expectedTraining, plan.TrainingLevel); Assert.Equal(expectedAward, plan.AwardLevel);
        Assert.Equal((expectedAward - 1) * 5, plan.PointsAwarded);
    }

    [Theory]
    [InlineData(3)]
    [InlineData(5)]
    public void CoveredOrLowerLevel_PreservesBalanceWatermarkAndExactPointBytes(int processed)
    {
        var input = Inputs(Stats(), null, Points(processed, 9), "{\"level\":3}");
        var plan = CharacteristicsService.CalculateMortalJointAutomatic(input);
        Assert.Equal(0, plan.PointsAwarded);
        Assert.Equal(input.ReadSourceBytes(M.JointAutomaticInputs.StatPointsPath), plan.AfterImages[M.JointAutomaticInputs.StatPointsPath].Bytes);
        Assert.Equal(processed, Read(plan, M.JointAutomaticInputs.StatPointsPath)["levelUpStatPointsAwardedThroughLevel"]!.GetValue<int>());
    }

    [Theory]
    [InlineData(null)]
    [InlineData("{ \"statsIncreased\":[], \"sibling\":true }")]
    [InlineData("{ \"sibling\":true }")]
    public void NoWork_PreservesAbsenceAndExactBytesWithoutInitialization(string? changes)
    {
        var stats = Stats(); stats["_metadata"] = "keep while unchanged";
        var input = Inputs(stats, changes, Points(1, 0));
        var plan = CharacteristicsService.CalculateMortalJointAutomatic(input);
        foreach (var path in plan.AfterImages.Keys)
            Assert.Equal(input.ReadSourceBytes(path), plan.AfterImages[path].Bytes);
        Assert.Empty(plan.AppliedStats); Assert.Empty(plan.BlockedStats); Assert.Equal(0, plan.PointsAwarded);
    }

    [Theory]
    [InlineData("{\"unspentStatPoints\":0}")]
    [InlineData("{\"unspentStatPoints\":0,\"levelUpStatPointsAwardedThroughLevel\":0}")]
    [InlineData("{\"unspentStatPoints\":-1,\"levelUpStatPointsAwardedThroughLevel\":1}")]
    [InlineData("{\"unspentStatPoints\":0,\"levelUpStatPointsAwardedThroughLevel\":\"1\"}")]
    [InlineData("null")]
    public void MissingOrInvalidInitializerEvidence_RefusesInsteadOfInferringFromBalances(string points)
        => Assert.Throws<InvalidDataException>(() => CharacteristicsService.CalculateMortalJointAutomatic(Inputs(Stats(), null, points)));

    [Theory]
    [InlineData("{\"statsIncreased\":null}")]
    [InlineData("{\"statsIncreased\":[1]}")]
    [InlineData("{\"statsIncreased\":[null]}")]
    [InlineData("[]")]
    public void MalformedStatsInput_Refuses(string changes)
        => Assert.Throws<InvalidDataException>(() => CharacteristicsService.CalculateMortalJointAutomatic(Inputs(Stats(), changes, Points(1, 0))));

    [Theory]
    [InlineData("missing")]
    [InlineData("string")]
    [InlineData("unknown")]
    public void IncompleteOrNoncanonicalBaseStats_Refuse(string mutation)
    {
        var stats = Stats();
        if (mutation == "missing") stats.Remove("strength");
        if (mutation == "string") stats["strength"] = "3";
        if (mutation == "unknown") stats["Strength"] = 3;
        Assert.Throws<InvalidDataException>(() => CharacteristicsService.CalculateMortalJointAutomatic(Inputs(stats, null, Points(1, 0))));
    }

    [Theory]
    [InlineData("{\"level\":0}")]
    [InlineData("{\"level\":-1}")]
    [InlineData("{\"level\":\"3\"}")]
    public void InvalidLevel_Refuses(string level)
        => Assert.Throws<InvalidDataException>(() => CharacteristicsService.CalculateMortalJointAutomatic(Inputs(Stats(), null, Points(1, 0), level)));

    [Theory]
    [InlineData("balance")]
    [InlineData("award")]
    [InlineData("cap")]
    public void Overflow_RefusesBeforeAnyPublication(string kind)
    {
        var level = kind == "award" ? 500_000_000 : kind == "cap" ? int.MaxValue : 2;
        var planInput = Inputs(Stats(), null, Points(1, kind == "balance" ? int.MaxValue : 0), "{\"level\":" + level + "}");
        Assert.Throws<OverflowException>(() => CharacteristicsService.CalculateMortalJointAutomatic(planInput));
    }

    [Fact]
    public void FrozenInputsAndReturnedImages_DoNotAliasCallerArrays()
    {
        var bytes = Bytes(Stats().ToJsonString());
        var input = new M.JointAutomaticInputs(bytes, Bytes("{\"statsIncreased\":[\"strength\"]}"), Bytes(Points(1, 0)), null, null, null);
        var fingerprint = input.Fingerprint; bytes[0] = 0;
        input.ReadSourceBytes(M.JointAutomaticInputs.CharacteristicsPath)![0] = 0;
        var plan = CharacteristicsService.CalculateMortalJointAutomatic(input);
        var mutation = plan.MutationFingerprint;
        plan.AfterImages[M.JointAutomaticInputs.CharacteristicsPath].Bytes![0] = 0;
        Assert.Equal(fingerprint, input.Fingerprint); Assert.Equal(mutation, plan.MutationFingerprint);
        Assert.Equal(2, Read(plan, M.JointAutomaticInputs.CharacteristicsPath)["strength"]!.GetValue<int>());
    }

    [Fact]
    public void ByteDistinctReadSource_ChangesSealEvenWhenResultValuesMatch()
    {
        var a = CharacteristicsService.CalculateMortalJointAutomatic(Inputs(Stats(), null, Points(1, 0), "{\"level\":1}"));
        var b = CharacteristicsService.CalculateMortalJointAutomatic(Inputs(Stats(), null, Points(1, 0), "{ \"level\":1 }"));
        Assert.Equal(a.PointsAwarded, b.PointsAwarded); Assert.NotEqual(a.MutationFingerprint, b.MutationFingerprint);
    }

    [Fact]
    public void ChangedThreeRootPlan_UsesTheExistingOwnersUtf8Preamble()
    {
        var plan = CharacteristicsService.CalculateMortalJointAutomatic(Inputs(Stats(),
            "{\"statsIncreased\":[\"strength\"]}", Points(1, 0), "{\"level\":2}"));
        foreach (var image in plan.AfterImages.Values)
            Assert.True(image.Bytes!.AsSpan().StartsWith(Encoding.UTF8.GetPreamble()));
    }

    [Theory]
    [InlineData("experience")]
    [InlineData("playerLevel")]
    [InlineData("points")]
    [InlineData("changes")]
    public void LoneNoncanonicalCaseAlias_RefusesInsteadOfConsumingOrIgnoringIt(string alias)
    {
        var input = Inputs(Stats(), alias == "changes" ? "{\"StatsIncreased\":[\"strength\"]}" : null,
            alias == "points" ? "{\"UnspentStatPoints\":0,\"LevelUpStatPointsAwardedThroughLevel\":1}" : Points(1, 0),
            alias == "experience" ? "{\"Level\":3}" : alias == "playerLevel" ? "{\"PlayerLevel\":3}" : null);
        Assert.Throws<InvalidDataException>(() => CharacteristicsService.CalculateMortalJointAutomatic(input));
    }

    [Theory]
    [InlineData(2, 3, 4)]
    [InlineData(100, 99, 100)]
    public async Task RealExistingTrainingOwner_ControlHasSameOrderedCapAndExactBytes(int level, int strengthBefore, int expectedStrength)
    {
        var stats = Stats(); stats["strength"] = strengthBefore; stats["dexterity"] = 100;
        var experience = "{\"level\":" + level + "}";
        var input = Inputs(stats, "{\"statsIncreased\":[\"strength\",\"strength\",\"dexterity\"]}", Points(level, 0), experience);
        var plan = CharacteristicsService.CalculateMortalJointAutomatic(input);
        var root = Path.Combine(Path.GetTempPath(), "boe-joint-automatic-control-" + Guid.NewGuid().ToString("N"));
        try
        {
            var files = new FileSystemManager(root, NullLogger<FileSystemManager>.Instance); files.EnsureDirectoryStructure();
            await files.WriteFileAtomicAsync(M.JointAutomaticInputs.CharacteristicsPath, stats.ToJsonString());
            await files.WriteFileAtomicAsync("game_state/player/experience.json", experience);
            var manager = new StateManager(files, new GameSettings(), NullLogger<StateManager>.Instance);
            var owner = new CharacteristicsService(files, manager, NullLogger<CharacteristicsService>.Instance);
            var actual = await owner.ApplyStatsIncreasedAsync(["strength", "strength", "dexterity"]);
            Assert.Equal(["strength"], actual.applied); Assert.Equal(["strength", "dexterity"], actual.blocked);
            Assert.Equal(plan.AppliedStats, actual.applied); Assert.Equal(plan.BlockedStats, actual.blocked);
            var current = JsonNode.Parse((await files.ReadFileAsync(M.JointAutomaticInputs.CharacteristicsPath))!)!;
            Assert.Equal(expectedStrength, current["strength"]!.GetValue<int>()); Assert.Equal(100, current["dexterity"]!.GetValue<int>());
            Assert.Equal(plan.AfterImages[M.JointAutomaticInputs.CharacteristicsPath].Bytes,
                await files.ReadFileBytesAsync(M.JointAutomaticInputs.CharacteristicsPath));
        }
        finally
        {
            if (Directory.Exists(root)) Directory.Delete(root, true);
            output.WriteLine(System.Text.Json.JsonSerializer.Serialize(new { Boundary = "automatic_calculation_control", Root = root, Removed = !Directory.Exists(root) }));
            Assert.False(Directory.Exists(root));
        }
    }

    private static M.JointAutomaticInputs Inputs(JsonObject stats, string? changes, string points,
        string? experience = null, string? player = null, string? soul = null) =>
        new(Bytes(stats.ToJsonString()), changes is null ? null : Bytes(changes), Bytes(points),
            experience is null ? null : Bytes(experience), player is null ? null : Bytes(player), soul is null ? null : Bytes(soul));
    private static JsonObject Stats() => new(Characteristics.All.Select(name => new KeyValuePair<string, JsonNode?>(name, JsonValue.Create(1))));
    private static string Points(int through, int unspent) => "{ \"unspentStatPoints\":" + unspent + ", \"levelUpStatPointsAwardedThroughLevel\":" + through + " }";
    private static byte[] Bytes(string text) => Encoding.UTF8.GetBytes(text);
    private static JsonObject Read(M.JointAutomaticPlan plan, string path) =>
        JsonNode.Parse(CanonicalJsonUtf8.DecodeOneOptionalBom(plan.AfterImages[path].Bytes!))!.AsObject();
}
