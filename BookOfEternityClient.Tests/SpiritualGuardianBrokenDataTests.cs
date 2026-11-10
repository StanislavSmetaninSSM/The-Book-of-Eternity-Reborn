using System.Text;
using System.Text.Json;
using System.Text.Json.Nodes;
using BookOfEternityClient.Services;
using Xunit;
using Xunit.Abstractions;

namespace BookOfEternityClient.Tests;

/// <summary>Independent guardian I→ordinaryIII→actualIV DATA, without simulating current authority or historical births.</summary>
public sealed partial class SpiritualGuardianBrokenDataTests(ITestOutputHelper output)
{
    private const GuardianSingletonPressureContour Contour = GuardianSingletonPressureContour.ReachingBroken;

    /// <summary>Proves complete parentIII/IV and ordinary pressure/success arithmetic before the unsupported v5 comparison.</summary>
    /// <param name="rank">Independent full retained parent severity.</param>
    [Theory]
    [InlineData(3)] [InlineData(4)]
    public void GuardianBroken_CompleteParentAndOrdinaryArithmeticPrerequisites(int rank) => AssertPrerequisites(rank);

    /// <summary>Desired witness/direct-reader success; an unsupported comparison refusal is the causal RED, never a PASS.</summary>
    /// <param name="rank">Independent full retained parent severity.</param>
    [Theory]
    [InlineData(3)] [InlineData(4)]
    public void GuardianBroken_VersionFiveWitnessAndIntrinsicSucceed(int rank)
    {
        var fixture = AssertPrerequisites(rank);
        var before = fixture.Source.ToJsonString();
        SpiritualStrainPreimageData? retained = null; SpiritualWoundCalculation? actual = null;
        var error = Record.Exception(() => actual = SpiritualWoundSourceWitness.ValidateAndReadStrainData(fixture.Source, out retained));
        output.WriteLine(JsonSerializer.Serialize(new { phase = "guardian_broken_intrinsic_v5", rank, error = error?.ToString(),
            sourceJson = before, actual, comparisonReturned = retained is not null, rawInputBase64 = Convert.ToBase64String(fixture.RawInput) }));
        Assert.Equal(before, fixture.Source.ToJsonString());
        if (error is not null) Assert.Null(retained);
        Assert.Null(error);
        Assert.Equal(SpiritualGuardianAttributionTestData.ActualCalculation(Contour), actual);
        var data = Assert.IsType<SpiritualStrainPreimageData>(retained);
        Assert.Equal(5, data.Version); Assert.Single(data.Parents); Assert.Null(data.ParentSourceId);
        Assert.Equal((rank, "guardian", SpiritualGuardianAttributionTestData.GuardianId),
            (data.ParentWound.Severity.Rank, data.ParentWound.Owner.OwnerKind, data.ParentWound.Owner.OwnerId));
        Assert.Equal(SpiritualGuardianAttributionTestData.OrdinaryCalculation(Contour), data.OrdinaryCalculation);
        Assert.Same(actual, data.ActualCalculation);
        Assert.True(JsonNode.DeepEquals(fixture.Retained.Parents.Single(), JsonNode.Parse(WoundMaterializationContract.SerializeCanonical(data.ParentWound))));
        Assert.Equal(fixture.Retained.Effects.Single()["effectId"]!.GetValue<string>(), data.EffectId); Assert.Equal("component_001", data.ComponentId);
        Assert.Equal(new CanonicalBeforeImage(true, fixture.RawInput).Fingerprint, data.OriginalInputFingerprint);
        Assert.Equal("/activeConflict/exchangeLog/0", data.OriginalExchangePointer);
        Assert.True(JsonNode.DeepEquals(fixture.Actual, data.ReadActualExchange()));
        Assert.True(JsonNode.DeepEquals(fixture.Ordinary["after"], data.ReadOrdinaryAfter(fixture.Actual)));
        var direct = SpiritualStrainPreimageData.ReadIntrinsic(fixture.Source, fixture.Actual);
        Assert.Equal(data.Version, direct.Version); Assert.Equal(rank, direct.ParentWound.Severity.Rank);
        Assert.Equal(actual, direct.ActualCalculation); Assert.Equal(data.OrdinaryCalculation, direct.OrdinaryCalculation);
        data.ReadActualExchange()["after"]!["oppositionSideStrain"] = "clear";
        data.ReadOrdinaryAfter(fixture.Actual)["oppositionSideStrain"] = "clear";
        Assert.True(JsonNode.DeepEquals(fixture.Actual, data.ReadActualExchange()));
        Assert.True(JsonNode.DeepEquals(fixture.Ordinary["after"], data.ReadOrdinaryAfter(fixture.Actual)));
        Assert.Equal(before, fixture.Source.ToJsonString());
    }

    /// <summary>Uses only current typed validators and unburdened source math to prove the independently authored premise.</summary>
    private static SpiritualGuardianAttributionFixture AssertPrerequisites(int rank)
    {
        var fixture = SpiritualGuardianAttributionTestData.Create(rank: rank, contour: Contour);
        var checks = SpiritualGuardianAttributionDataCheck.Check(fixture.Retained, fixture.Profiles, fixture.Conflict, rank);
        Assert.Empty(checks);
        var issues = new List<ValidationIssue>();
        ValidationService.ValidateRetainedSpiritualExchangeShape(fixture.Ordinary, "guardianBroken.ordinary", issues);
        ValidationService.ValidateSpiritualWoundSourceActionShape(fixture.Ordinary, "guardianBroken.ordinary", issues);
        ValidationService.ValidateRetainedSpiritualDiceAudit(fixture.Ordinary, fixture.Source["turnEvidence"]!.AsObject(), issues);
        ValidationService.ValidateRetainedSpiritualExchangeRules(fixture.Ordinary, issues);
        Assert.True(issues.Count == 0, JsonSerializer.Serialize(issues));
        var ordinarySource = SpiritualGuardianAttributionTestData.CreateSource(fixture.Ordinary, "ordinary-session", "ordinary-request", "ordinary-snapshot",
            "ordinary-conflict", new CanonicalBeforeImage(true, fixture.RawInput).Fingerprint);
        var ordinary = SpiritualWoundSourceWitness.ValidateAndReadStrainData(ordinarySource, out var noOrdinary); Assert.Null(noOrdinary);
        Assert.Equal(SpiritualGuardianAttributionTestData.OrdinaryCalculation(Contour), ordinary);
        Assert.Equal((1, 3, 1, 15L, 2), (ordinary.PreviousStrainRank, ordinary.NewStrainRank, ordinary.ExtraJumpSteps, ordinary.TraumaPressure, ordinary.MaximumSeverityRank));
        var actualSource = fixture.Source.DeepClone().AsObject(); Assert.True(actualSource.Remove("strainPreimage")); SpiritualExistingPressureTestData.Seal(actualSource);
        var actual = SpiritualWoundSourceWitness.ValidateAndReadStrainData(actualSource, out var noActual); Assert.Null(noActual);
        Assert.Equal(SpiritualGuardianAttributionTestData.ActualCalculation(Contour), actual);
        Assert.Equal((1, 4, 2, 20L, 3), (actual.PreviousStrainRank, actual.NewStrainRank, actual.ExtraJumpSteps, actual.TraumaPressure, actual.MaximumSeverityRank));
        Assert.Equal(("pressure", "success", 8), (fixture.Ordinary["operationType"]!.GetValue<string>(), fixture.Ordinary["outcome"]!.GetValue<string>(), fixture.Source["harmfulMargin"]!.GetValue<int>()));
        Assert.True(JsonNode.DeepEquals(SpiritualGuardianAttributionTestData.FailedAction(), fixture.Ordinary["spiritualWoundFailedAction"]));
        Assert.True(JsonNode.DeepEquals(fixture.Ordinary["spiritualWoundFailedAction"], fixture.Actual["spiritualWoundFailedAction"]));
        var expected = fixture.Ordinary.DeepClone(); expected["after"]!["oppositionSideStrain"] = "broken";
        Assert.True(JsonNode.DeepEquals(expected, fixture.Actual));
        Assert.True(fixture.RawInput.AsSpan().StartsWith(Encoding.UTF8.GetPreamble()));
        Assert.True(JsonNode.DeepEquals(fixture.Conflict, SpiritualWoundStateJson.Parse(SpiritualWoundStateJson.DecodeUtf8JsonText(fixture.RawInput))));
        Assert.Equal(new CanonicalBeforeImage(true, fixture.RawInput).Fingerprint, fixture.Source["strainPreimage"]!["originalInput"]!["contentFingerprint"]!.GetValue<string>());
        // The old default retains its complete graph, wire, input bytes and independent calculations.
        var old = SpiritualGuardianAttributionTestData.Create(rank: rank);
        var explicitOld = SpiritualGuardianAttributionTestData.Create(rank: rank, contour: GuardianSingletonPressureContour.BelowBroken);
        Assert.Equal(old.Source.ToJsonString(), explicitOld.Source.ToJsonString());
        Assert.Equal(old.Profiles.ToJsonString(), explicitOld.Profiles.ToJsonString()); Assert.Equal(old.RawInput, explicitOld.RawInput);
        return fixture;
    }
}
