using System.Text;
using System.Text.Json.Nodes;
using BookOfEternityClient.Services;
using Xunit;

namespace BookOfEternityClient.Tests;

public sealed partial class SpiritualGuardianBrokenDataTests
{
    /// <summary>Otherwise lawful, independently validated ordinary/actual pressure still refuses each excluded scalar geometry.</summary>
    /// <param name="rank">Independent complete parent III/IV.</param>
    /// <param name="change">One finite out-of-bound geometry, not a malformed source.</param>
    [Theory]
    [InlineData(3, "before-clear")] [InlineData(4, "before-clear")]
    [InlineData(3, "before-fractured")] [InlineData(4, "before-fractured")]
    [InlineData(3, "ordinary-equals-actual")] [InlineData(4, "ordinary-equals-actual")]
    [InlineData(3, "ordinary-capped")] [InlineData(4, "ordinary-capped")]
    public void GuardianBroken_RejectsOtherwiseValidExcludedGeometry(int rank, string change)
    {
        var fixture = AssertPrerequisites(rank);
        Assert.Equal(SpiritualGuardianAttributionTestData.ActualCalculation(Contour),
            SpiritualWoundSourceWitness.ValidateAndReadStrainData(fixture.Source, out var baseline));
        Assert.Equal(5, Assert.IsType<SpiritualStrainPreimageData>(baseline).Version);
        var ordinary = fixture.Ordinary.DeepClone().AsObject();
        var actual = fixture.Actual.DeepClone().AsObject();
        switch (change)
        {
            case "before-clear": ordinary["before"]!["oppositionSideStrain"] = "clear"; actual["before"]!["oppositionSideStrain"] = "clear"; break;
            case "before-fractured": ordinary["before"]!["oppositionSideStrain"] = "fractured"; actual["before"]!["oppositionSideStrain"] = "fractured"; break;
            case "ordinary-equals-actual": actual["after"]!["oppositionSideStrain"] = "overwhelmed"; break;
            case "ordinary-capped": ordinary["after"]!["oppositionSideStrain"] = "broken"; break;
            default: throw new ArgumentOutOfRangeException(nameof(change));
        }
        var root = fixture.Conflict.DeepClone().AsObject();
        root["activeConflict"]!["exchangeLog"]![0] = ordinary.DeepClone();
        root["activeConflict"]!["oppositionSideStrain"] = ordinary["after"]!["oppositionSideStrain"]!.DeepClone();
        var raw = Encoding.UTF8.GetPreamble().Concat(Encoding.UTF8.GetBytes(" \n" + root.ToJsonString() + "\n")).ToArray();
        var fingerprint = new CanonicalBeforeImage(true, raw).Fingerprint;
        JsonObject Unburdened(JsonObject exchange) => SpiritualGuardianAttributionTestData.CreateSource(exchange,
            "boundary-session", "boundary-request", "boundary-snapshot", "boundary-conflict", fingerprint);
        var ordinarySource = Unburdened(ordinary);
        var ordinaryCalculation = SpiritualWoundSourceWitness.ValidateAndReadStrainData(ordinarySource, out var noOrdinary);
        Assert.Null(noOrdinary); Assert.True(ordinaryCalculation.MaximumSeverityRank > 0);
        var source = Unburdened(actual);
        var actualCalculation = SpiritualWoundSourceWitness.ValidateAndReadStrainData(source, out var noActual);
        Assert.Null(noActual); Assert.True(actualCalculation.MaximumSeverityRank > 0);
        var proof = fixture.Source["strainPreimage"]!.DeepClone().AsObject();
        proof["ordinaryExchangeJsonBase64"] = SpiritualGuardianAttributionTestData.Encode(ordinary);
        proof["originalInput"]!["contentFingerprint"] = fingerprint;
        proof["ordinaryCalculation"] = new JsonObject { ["traumaPressure"] = ordinaryCalculation.TraumaPressure,
            ["maximumSeverityRank"] = ordinaryCalculation.MaximumSeverityRank };
        source["strainPreimage"] = proof; SpiritualExistingPressureTestData.Seal(source);
        var before = source.ToJsonString(); SpiritualStrainPreimageData? leaked = null;
        var expected = change.StartsWith("before-", StringComparison.Ordinal)
            ? "Only the last ordinal-zero increasing guardian I-to-III/IV standard pressure source with nonnegative maximum is supported."
            : change == "ordinary-capped" ? "Guardian ordinary strain must be independently proved I-to-II/III."
            : "Increasing ordinary guardian source with nonnegative maximum required.";
        Assert.Equal(expected, Assert.Throws<FormatException>(() =>
            SpiritualWoundSourceWitness.ValidateAndReadStrainData(source, out leaked)).Message);
        Assert.Null(leaked); Assert.Equal(before, source.ToJsonString());
        Assert.Equal(expected, Assert.Throws<FormatException>(() => SpiritualStrainPreimageData.ReadIntrinsic(source, actual)).Message);
        Assert.Equal(before, source.ToJsonString());
    }
}
