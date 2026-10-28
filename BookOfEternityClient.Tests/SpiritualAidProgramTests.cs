using BookOfEternityClient.Services;
using Xunit;

namespace BookOfEternityClient.Tests;

public sealed class SpiritualAidProgramTests
{
    [Fact]
    public void ActiveHealerTierDoesNotReplaceTheTargetNaturalTierInTheSameProgram()
    {
        var parsed = WoundMaterializationContract.Parse(WoundContractTestData.CreateSpiritualActiveWound(
            "wound_aid_program", "chaos_sea", "guardian", "guardian_aid_program",
            WoundCarrierCatalog.AfterlifeProfilesPath).ToJsonString(), "aid.program.original");
        Assert.True(parsed.IsValid, string.Join(";", parsed.Issues.Select(i => i.Actual)));
        var wound = parsed.Wound!;
        Assert.Equal(2, wound.Severity.Rank);
        Assert.Equal(0, wound.Recovery.CurrentStepProgress);
        // Real active formula: H3 + d20=8 gives total14/difficulty14, one step.
        var active = Assert.IsType<SpiritualHealingCalculation>(SpiritualHealingOutcomeMath.Calculate(new(3, 2, 8, 0, 0)));
        Assert.True(active.HasSufficientTier);
        Assert.Equal(SpiritualHealingResultBand.ReduceOne, active.ResultBand);
        Assert.Equal(1, active.ResultingSeverityRank);
        var stages = SpiritualWoundRecoveryPlanner.Build(wound, 0, "attempt_aid_program", "cycle_aid_program", active);
        Assert.Equal(new[] { "treat", "recover" }, stages.Select(s => s.Kind));
        Assert.All(stages, s => Assert.Equal(1, s.SeverityRank));
        Assert.Equal(0, stages[0].Progress);
        Assert.Equal(1, stages[1].Progress); // Target H0 adds1, not healer H3's4.
        Assert.Equal("cycle_aid_program", stages[1].TickKey);
        Assert.DoesNotContain(stages, s => s.Kind == "heal");
        var sameTier = SpiritualWoundRecoveryPlanner.Build(wound, 3, "attempt_equal_tier", "cycle_equal_tier", active);
        Assert.Equal("heal", sameTier[^1].Kind); // Wrongly using healer tier would terminate.
        Assert.Throws<InvalidDataException>(() => SpiritualWoundRecoveryPlanner.Build(wound, 0,
            "attempt_forged_calculation", "cycle_forged_calculation", active with { ResultingSeverityRank = 2 }));
    }
}
