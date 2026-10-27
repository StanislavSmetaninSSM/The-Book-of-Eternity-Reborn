using System.Text.Json.Nodes;
using BookOfEternityClient.Services;
using Xunit;

namespace BookOfEternityClient.Tests;

public sealed partial class SpiritualHealingCheckTests
{
    [Theory]
    [InlineData("healer_id", "spiritual_healing_actor_mismatch")]
    [InlineData("healer_realm", "spiritual_healing_target_invalid")]
    [InlineData("healer_carrier", "spiritual_healing_target_invalid")]
    [InlineData("profile_kind", "spiritual_healing_actor_mismatch")]
    [InlineData("profile_tier_object", "spiritual_healing_art_invalid")]
    [InlineData("profile_arts_scalar", "spiritual_healing_art_invalid")]
    [InlineData("soul_tier_object", "spiritual_healing_player_source_invalid")]
    [InlineData("soul_profile_scalar", "spiritual_healing_player_source_invalid")]
    [InlineData("soul_realm", "spiritual_healing_player_source_invalid")]
    [InlineData("mirror_mismatch", "spiritual_healing_player_art_mismatch")]
    [InlineData("missing_tier", "spiritual_healing_art_invalid")]
    [InlineData("tier_six", "spiritual_healing_art_invalid")]
    [InlineData("inactive_wound", "spiritual_healing_target_invalid")]
    public void Resolve_RejectsDifferentActorAndMalformedArtWithoutFallback(string change, string code)
    {
        var input = Input();
        input = change switch
        {
            "healer_id" => input with { Healer = input.Healer with { OwnerId = "other_soul" } },
            "healer_realm" => input with { Healer = input.Healer with { Realm = "shining_abode" } },
            "healer_carrier" => input with { Healer = input.Healer with { CarrierPath = WoundCarrierCatalog.PlayerPath } },
            "inactive_wound" => input with { Before = input.Before with { Lifecycle = "healed" } },
            _ => input
        };
        switch (change)
        {
            case "profile_kind": input.HealerProfile["actorType"] = "guardian"; break;
            case "profile_tier_object": input.HealerProfile["standardArts"]!["spiritual_healing"] = new JsonObject { ["tier"] = 5 }; break;
            case "profile_arts_scalar": input.HealerProfile["standardArts"] = 5; break;
            case "soul_tier_object": input.PlayerSoul!["afterlifeCombatProfile"]!["artTiers"]!["spiritual_healing"] = new JsonObject { ["tier"] = 5 }; break;
            case "soul_profile_scalar": input.PlayerSoul!["afterlifeCombatProfile"] = 5; break;
            case "soul_realm": input.PlayerSoul!["currentRealm"] = "Shining Abode"; break;
            case "mirror_mismatch": input.PlayerSoul!["afterlifeCombatProfile"]!["artTiers"]!["spiritual_healing"] = 5; break;
            case "missing_tier": input.HealerProfile["standardArts"]!.AsObject().Remove("spiritual_healing"); break;
            case "tier_six": input.HealerProfile["standardArts"]!["spiritual_healing"] = 6; break;
        }
        var result = SpiritualHealingResolver.Resolve(input);
        Assert.False(result.IsValid);
        Assert.Null(result.Resolution);
        Assert.Equal(code, Assert.Single(result.Issues).Code);
    }

    [Theory]
    [InlineData(0)] [InlineData(21)]
    public void Resolve_RejectsOutOfRangeRetainedDie(int die)
    {
        var result = SpiritualHealingResolver.Resolve(Input(die: die));
        Assert.Equal("spiritual_healing_draw_invalid", Assert.Single(result.Issues).Code);
        Assert.Null(result.Resolution);
    }

    [Fact]
    public void Resolve_RejectsDrawCountThatCouldHideAnotherRoll()
    {
        var result = SpiritualHealingResolver.Resolve(Input() with { SealedRolls = new[] { 10, 20 } });
        Assert.Equal("spiritual_healing_draw_invalid", Assert.Single(result.Issues).Code);
        Assert.Null(result.Resolution);
    }

    [Fact]
    public void Resolve_RejectsModeChangedFromItsAcceptedEffectSources()
    {
        var result = SpiritualHealingResolver.Resolve(Input() with { Effects = Effects("disadvantage") });
        Assert.Equal("spiritual_healing_roll_mode_mismatch", Assert.Single(result.Issues).Code);
        Assert.Null(result.Resolution);
    }

    [Fact]
    public void Resolve_ForeignActorEffectCannotChooseHealersDie()
    {
        var source = Effects("advantage");
        var input = Input() with { Effects = source with
        { Components = source.Components.Select(c => c with { TargetId = "foreign_actor" }).ToArray() } };
        var result = SpiritualHealingResolver.Resolve(input);
        Assert.True(result.IsValid);
        Assert.Empty(result.Resolution!.RollContributions);
        Assert.Equal(10, result.Resolution.Calculation.Input.NaturalRoll);
    }

    [Fact]
    public void Resolve_RejectedEffectSnapshotCannotBecomeNormalRoll()
    {
        var result = SpiritualHealingResolver.Resolve(Input() with { Effects = new(false, [], [],
            new[] { new ValidationIssue("effects", IssueSeverity.Error, "Rejected effect authority.", code: "effect_rejected") }) });
        Assert.Null(result.Resolution);
        Assert.Equal("effect_rejected", Assert.Single(result.Issues).Code);
    }

    [Theory]
    [InlineData(1, 5, "NoImprovement")]
    [InlineData(20, 2, "ReduceTwo")]
    public void Resolve_NaturalResultOverridesOppositeNumericBand(int die, int tier, string band)
    {
        var input = Input(tier, die);
        if (die == 20)
            input = input with { Before = input.Before with { Complications = Enumerable.Range(1, 3)
                .Select(i => new WoundComplication("comp_" + i, "strain", "active", "Помеха", 4, [], "public")).ToArray() } };
        var result = SpiritualHealingResolver.Resolve(input);
        Assert.True(result.IsValid);
        Assert.Equal(band, result.Resolution!.Calculation.ResultBand.ToString());
        Assert.Equal(die == 1 ? -3 : -2, result.Resolution.Calculation.Margin);
    }
}
