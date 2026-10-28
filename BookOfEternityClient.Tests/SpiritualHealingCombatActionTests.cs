using BookOfEternityClient.Services;
using BookOfEternityClient.Models;
using System.Text.Json;
using System.Text.Json.Nodes;
using Xunit;

namespace BookOfEternityClient.Tests;

public sealed class SpiritualHealingCombatActionTests
{
    [Fact]
    public void StandardHealingUsesItsOwnBaseFiveCostAndNeverFallsBelowTwo()
    {
        Assert.True(AfterlifeActionCostRules.TryGetDefinition("pressure", out var pressure));
        Assert.Equal(2, AfterlifeActionCostRules.ResolveStandardEffectiveCost(pressure, 1));

        Assert.True(AfterlifeActionCostRules.TryGetDefinition("heal_spiritual_wound", out var healing));
        Assert.Equal(4, AfterlifeActionCostRules.ResolveStandardEffectiveCost(healing, 1));
        Assert.Equal(3, AfterlifeActionCostRules.ResolveStandardEffectiveCost(healing, 2));
        Assert.Equal(2, AfterlifeActionCostRules.ResolveStandardEffectiveCost(healing, 3));
        Assert.Equal(2, AfterlifeActionCostRules.ResolveStandardEffectiveCost(healing, 5));
        Assert.False(AfterlifeActionCostRules.HasCost(AfterlifeSpiritualConflictState.SpiritualResilienceArtId));
    }

    [Fact]
    public void CombatUsesOnlyTheSharedActivePrefixWhileSafeTreatmentKeepsItsNaturalSuffix()
    {
        var parsed = WoundMaterializationContract.Parse(WoundContractTestData.CreateSpiritualActiveWound(
            "wound_combat_program", "chaos_sea", "player_soul", "player_soul",
            WoundCarrierCatalog.AfterlifeProfilesPath).ToJsonString(), "combat.original");
        Assert.True(parsed.IsValid);
        var before = parsed.Wound!;
        Assert.Equal(2, before.Severity.Rank);
        var active = Assert.IsType<SpiritualHealingCalculation>(SpiritualHealingOutcomeMath.Calculate(new(3, 2, 8, 0, 0)));
        Assert.Equal(SpiritualHealingResultBand.ReduceOne, active.ResultBand);
        var combat = SpiritualWoundRecoveryPlanner.BuildActiveOnly(before, "combat_attempt", "combat_exchange", active);
        var treated = Assert.Single(combat);
        Assert.Equal("treat", treated.Kind);
        Assert.Equal(1, treated.SeverityRank);
        Assert.Equal(0, treated.Progress);
        Assert.False(treated.PreparesHeal);
        var safe = SpiritualWoundRecoveryPlanner.Build(before, 0, "combat_attempt", "combat_exchange", active);
        Assert.Equal(treated, safe[0]);
        Assert.Equal("recover", safe[1].Kind);
        Assert.Equal(1, safe[1].Progress);
        Assert.Throws<InvalidDataException>(() => SpiritualWoundRecoveryPlanner.BuildActiveOnly(before,
            "combat_attempt", "combat_exchange", active with { ResultingSeverityRank = 2 }));
        Assert.Equal("spiritual_healing", AfterlifeActionCostRules.ResolveStandardArtId("heal_spiritual_wound"));
        Assert.Equal("pressure", AfterlifeActionCostRules.ResolveStandardArtId("pressure"));
    }

    [Fact]
    public void CombatSelectionAndIntentCannotCarrySafeAidOrAnIndependentDie()
    {
        var fingerprint = "sha256:" + new string('a', 64);
        var selection = new SpiritualSelfTreatmentSelection("wound_combat", fingerprint)
            { Combat = new("conflict_combat", fingerprint, 2) };
        Assert.Equal(selection, SpiritualSelfTreatmentSelection.Parse(selection.ToJson()));
        Assert.Throws<InvalidDataException>(() => (selection with { Aid = new("player_soul", "guardian", fingerprint) }).ToJson());
        var malformed = selection.ToJson(); malformed["nextExchangeOrdinal"] = 0;
        Assert.Throws<InvalidDataException>(() => SpiritualSelfTreatmentSelection.Parse(malformed));
        var sources = AfterlifeWoundTreatmentAuthority.CombatSourcePaths.Select(path =>
            new AfterlifeWoundTreatmentAuthority.SourceFile(path, new(false, null), null)).ToArray();
        var intent = SpiritualCombatHealingIntent.Create("session_combat", "request_combat", 43, selection, 3,
            JsonSerializer.SerializeToNode(new ProgressionControl())!.AsObject(), sources);
        Assert.True(intent.Matches("session_combat", "request_combat", 43));
        Assert.False(intent.Matches("session_combat", "request_combat", 44));
        Assert.Equal(3, intent.HealingTier);
        Assert.Equal(2, intent.NextExchangeOrdinal);
        var injected = intent.Root; injected["roll"] = 20;
        Assert.Throws<InvalidDataException>(() => SpiritualCombatHealingIntent.Parse(injected));
        var detached = intent.Root; detached["healingTier"] = 5;
        Assert.Throws<InvalidDataException>(() => SpiritualCombatHealingIntent.Parse(detached));
        Assert.Equal(3, intent.HealingTier);
    }
}
