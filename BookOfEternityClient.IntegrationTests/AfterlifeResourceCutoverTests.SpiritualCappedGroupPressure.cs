using BookOfEternityClient.Services;
using Xunit;

namespace BookOfEternityClient.Tests;

public sealed partial class AfterlifeResourceCutoverTests
{
    /// <summary>Requires real owned capped N2/v4 and N3/v6 actual14/II plus complete C1 while preserving every signed parent and fresh fence.</summary>
    /// <param name="parents">Only separately owned N2/N3 normal admission cases; no native-birth or publication claim.</param>
    [Theory]
    [InlineData(2)] [InlineData(3)]
    public Task SpiritualCappedGroupPressure_RealOwnerAdvancesAndComposesC1(int parents)
    {
        if (parents is not (2 or 3)) throw new ArgumentOutOfRangeException(nameof(parents));
        var spec = new PressureGroupCase("capped_group_owner_" + parents, Parents: parents, After: "broken",
            AdvanceExpected: true, CappedOrdinary: true);
        return WithPressureMultiParentFailureAsync(spec.Name, () => WithPressureGroupCaseAsync(spec, async window =>
        {
            // The existing owner fixture already authenticated the complete signed/current graphs,
            // every creation receipt/History join and the entire contribution inventory once.
            var raw = window.Files[AfterlifeSpiritualConflictState.StatePath];
            foreach (var exchange in new[] { SpiritualCappedGroupPressureData.ReadExchange(), window.Copy.Ordinary })
            {
                var source = SpiritualCappedGroupPressureData.CreateStandaloneSource(exchange, raw);
                Assert.Equal(SpiritualCappedGroupPressureData.Calculation,
                    SpiritualWoundSourceWitness.ValidateAndReadStrainData(source, out var comparison));
                Assert.Null(comparison);
            }
            await AssertPressureOwnedGroupActualAdmissionAsync(window, SpiritualCappedGroupPressureData.Calculation,
                "Every complete retained capped group must admit ordinaryIV=actualIV pressure14/maxII and preserve all parents through its v4/v6 source and first C1.",
                multiParent: parents >= 3);
        }));
    }
}
