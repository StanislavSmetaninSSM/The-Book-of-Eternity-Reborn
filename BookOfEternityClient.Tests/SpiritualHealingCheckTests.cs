using System.Text.Json;
using System.Text.Json.Nodes;
using BookOfEternityClient.Services;
using Xunit;

namespace BookOfEternityClient.Tests;

public sealed partial class SpiritualHealingCheckTests
{
    [Theory]
    [InlineData(2, "NoImprovement", 0, false)]
    [InlineData(5, "NoImprovement", 0, false)]
    [InlineData(6, "RecoveryPoint", 0, false)]
    [InlineData(9, "RecoveryPoint", 0, false)]
    [InlineData(10, "ReduceOne", 1, false)]
    [InlineData(17, "ReduceOne", 1, false)]
    [InlineData(18, "ReduceTwo", 2, true)]
    [InlineData(19, "ReduceTwo", 2, true)]
    public void Resolve_ActualSoulArtAndSealedDrawDetermineBand(
        int die, string band, int steps, bool heals)
    {
        var input = Input(die: die);
        var result = SpiritualHealingResolver.Resolve(input);
        Assert.True(result.IsValid, string.Join("; ", result.Issues.Select(i => i.Message)));
        var resolution = Assert.IsType<SpiritualHealingResolution>(result.Resolution);
        Assert.Equal(input.Before.WoundId, resolution.WoundId);
        Assert.Equal(input.Before.Owner, resolution.Target);
        Assert.Equal(input.Healer, resolution.Healer);
        Assert.Equal(2, resolution.Calculation.Input.HealingTier);
        Assert.Equal(die, resolution.Calculation.Input.NaturalRoll);
        Assert.Equal(0, resolution.Calculation.Input.ValidatedModifiers);
        Assert.Equal(band, resolution.Calculation.ResultBand.ToString());
        Assert.Equal(steps, resolution.Calculation.ImprovementSteps);
        Assert.Equal(heals, resolution.Calculation.HealsWound);
    }

    [Theory]
    [InlineData("advantage", 1, 20, 20, "ReduceTwo")]
    [InlineData("disadvantage", 1, 20, 1, "NoImprovement")]
    public void Resolve_DerivesEffectiveDieFromExactActorEffectEvidence(
        string mode, int first, int second, int effective, string band)
    {
        var input = Input() with { SealedRollMode = mode, SealedRolls = new[] { first, second },
            Effects = Effects(mode) };
        var result = SpiritualHealingResolver.Resolve(input);
        Assert.True(result.IsValid);
        Assert.Equal(effective, result.Resolution!.Calculation.Input.NaturalRoll);
        Assert.Equal(band, result.Resolution.Calculation.ResultBand.ToString());
        Assert.Equal("effect_healing", Assert.Single(result.Resolution.RollContributions).EffectId);
    }

    [Fact]
    public void Resolve_ActiveComplicationsContributeAndResolvedOnesDoNot()
    {
        var input = Input(die: 14);
        input = input with { Before = input.Before with { Complications = new[]
        {
            new WoundComplication("active_a", "spiritual_instability", "active", "Помеха", 2, [], "public"),
            new WoundComplication("active_b", "spiritual_instability", "active", "Помеха", 1, [], "hidden"),
            new WoundComplication("resolved", "spiritual_instability", "resolved", "Устранено", 4, [], "public")
        } } };
        var result = SpiritualHealingResolver.Resolve(input);
        Assert.True(result.IsValid);
        Assert.Equal(3, result.Resolution!.Calculation.Input.ComplicationModifier);
        Assert.Equal(1, result.Resolution.Calculation.Margin);
    }

    [Theory]
    [InlineData(0)] [InlineData(1)]
    public void Resolve_InsufficientActualTierPrecedesNaturalTwenty(int tier)
    {
        var input = Input(tier: tier, die: 20);
        var result = SpiritualHealingResolver.Resolve(input);
        Assert.True(result.IsValid);
        Assert.Equal(SpiritualHealingResultBand.InsufficientTier, result.Resolution!.Calculation.ResultBand);
        Assert.False(result.Resolution.Calculation.HealsWound);
    }

    [Fact]
    public void Resolve_ReturnedDrawDoesNotAliasRetainedRequest()
    {
        var draws = new[] { 20 };
        var result = SpiritualHealingResolver.Resolve(Input() with { SealedRolls = draws });
        Assert.True(result.IsValid);
        draws[0] = 1;
        Assert.Equal(20, Assert.Single(result.Resolution!.SourceRolls));
        Assert.Equal(20, result.Resolution.Calculation.Input.NaturalRoll);
    }

    internal static SpiritualHealingResolverInput Input(int tier = 2, int die = 10)
    {
        var node = WoundContractTestData.CreateSpiritualActiveWound(
            ownerId: "player_soul", carrierPath: WoundCarrierCatalog.AfterlifeProfilesPath);
        var parsed = WoundMaterializationContract.Parse(node.ToJsonString(), "fixture.wound");
        Assert.True(parsed.IsValid, string.Join(";", parsed.Issues.Select(i => i.Message)));
        var wound = parsed.Wound! with { Complications = [] };
        var soul = new JsonObject { ["currentRealm"] = "Chaos Sea",
            ["afterlifeCombatProfile"] = new JsonObject
            { ["artTiers"] = new JsonObject { ["spiritual_healing"] = tier } } };
        var profile = new JsonObject { ["actorType"] = "player_soul", ["actorId"] = "player_soul",
            ["realm"] = "Chaos Sea", ["standardArts"] = new JsonObject { ["spiritual_healing"] = tier } };
        return new(wound, wound.Owner, profile, soul, new(true, [], [], []), "normal", new[] { die });
    }

    private static EffectMechanicsSnapshot Effects(string contribution)
    {
        var payload = new JsonObject { ["operations"] = new JsonArray("skill_check"),
            ["contribution"] = contribution, ["scope"] = new JsonObject { ["kind"] = "all" } };
        return new(true, new[] { new EffectMechanicalComponent("effect_healing", "chaos_sea", "player",
            "player_soul", true, "Поддержка", "Поддержка исцеления", "component_healing", "roll_modifier",
            0, 1, JsonSerializer.SerializeToElement(payload)) }, [], []);
    }
}
