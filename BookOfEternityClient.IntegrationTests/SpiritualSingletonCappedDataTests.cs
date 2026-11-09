using System.Text;
using System.Text.Json.Nodes;
using BookOfEternityClient.Services;
using Xunit;

namespace BookOfEternityClient.Tests;

/// <summary>Independent capped singleton prerequisites and v3 desired success, without live authority.</summary>
public sealed class SpiritualSingletonCappedDataTests
{
    /// <summary>Validates complete retained III/IV graphs and literal III→IV arithmetic before the v3 reader.</summary>
    [Theory]
    [InlineData(3)] [InlineData(4)]
    public void SingletonCapped_CompletePrerequisites(int rank)
    {
        var fixture = SpiritualSingletonIvPlayerDataTests.Create(rank, PlayerSingletonPressureContour.CappedBroken);
        SpiritualSingletonIvPlayerDataTests.AssertPrerequisites(fixture, rank, PlayerSingletonPressureContour.CappedBroken);
        Assert.True(JsonNode.DeepEquals(fixture.Ordinary, fixture.Actual));
    }

    /// <summary>Requires retained owned comparison DATA even though ordinary and actual exchanges are semantically equal.</summary>
    [Theory]
    [InlineData(3)] [InlineData(4)]
    public void SingletonCapped_WitnessAndIntrinsicSucceed(int rank)
    {
        var fixture = SpiritualSingletonIvPlayerDataTests.Create(rank, PlayerSingletonPressureContour.CappedBroken);
        SpiritualSingletonIvPlayerDataTests.AssertPrerequisites(fixture, rank, PlayerSingletonPressureContour.CappedBroken);
        var original = fixture.Source.ToJsonString();
        var calculation = SpiritualWoundSourceWitness.ValidateAndReadStrainData(fixture.Source, out var comparison);
        var data = Assert.IsType<SpiritualStrainPreimageData>(comparison);
        Assert.Equal(3, data.Version); Assert.Equal(rank, data.ParentWound.Severity.Rank);
        Assert.Equal(SpiritualSingletonIvPlayerDataTests.OrdinaryCalculation(PlayerSingletonPressureContour.CappedBroken), calculation);
        Assert.Equal(calculation, data.OrdinaryCalculation);
        Assert.True(JsonNode.DeepEquals(fixture.Actual, data.ReadActualExchange()));
        Assert.True(JsonNode.DeepEquals(fixture.Ordinary["after"], data.ReadOrdinaryAfter(fixture.Actual)));
        Assert.Equal(calculation, SpiritualStrainPreimageData.ReadIntrinsic(fixture.Source, fixture.Actual).ActualCalculation);
        data.ReadActualExchange()["after"]!["playerSideStrain"] = "clear";
        Assert.True(JsonNode.DeepEquals(fixture.Actual, data.ReadActualExchange()));
        Assert.Equal(original, fixture.Source.ToJsonString());
    }
    /// <summary>Cheap independent capped geometry and trainingmax0 intrinsic DATA, never live publication qualification.</summary>
    [Theory]
    [InlineData(0, false, 23, 4)] [InlineData(1, false, 20, 3)]
    [InlineData(2, false, 17, 2)] [InlineData(3, false, 14, 2)]
    [InlineData(0, true, 23, 0)] [InlineData(1, true, 20, 0)]
    [InlineData(2, true, 17, 0)] [InlineData(3, true, 14, 0)]
    public void SingletonCapped_IntrinsicGeometryAndZeroCap(int previous, bool training, long pressure, int maximum)
    {
        var fixture = SpiritualSingletonIvPlayerDataTests.Create(3, PlayerSingletonPressureContour.CappedBroken);
        var before = new[] { "clear", "strained", "fractured", "overwhelmed" }[previous];
        fixture.Ordinary["before"]!["playerSideStrain"] = before;
        fixture.Actual["before"]!["playerSideStrain"] = before;
        var source = fixture.Source;
        source["priorStrainRank"] = previous; source["extraStrainJumps"] = 3 - previous;
        source["dangerMode"] = training ? "training" : "hostile"; source["maximumSeverityRank"] = maximum;
        source["exchangeJsonBase64"] = SpiritualGuardianAttributionTestData.Encode(fixture.Actual);
        source["strainPreimage"]!["ordinaryExchangeJsonBase64"] = SpiritualGuardianAttributionTestData.Encode(fixture.Ordinary);
        source["strainPreimage"]!["ordinaryCalculation"]!["traumaPressure"] = pressure;
        source["strainPreimage"]!["ordinaryCalculation"]!["maximumSeverityRank"] = maximum;
        fixture.Conflict["activeConflict"]!["dangerMode"] = training ? "training" : "hostile";
        fixture.Conflict["activeConflict"]!["exchangeLog"]![0] = fixture.Ordinary.DeepClone();
        var raw = Encoding.UTF8.GetPreamble().Concat(Encoding.UTF8.GetBytes(" \n" + fixture.Conflict.ToJsonString() + "\n")).ToArray();
        source["strainPreimage"]!["originalInput"]!["contentFingerprint"] = new CanonicalBeforeImage(true, raw).Fingerprint;
        source["turnEvidence"]!["originalSnapshotFingerprint"] = new CanonicalBeforeImage(true, raw).Fingerprint;
        source["conflictBeforeFingerprint"] = new CanonicalBeforeImage(true,
            Encoding.UTF8.GetBytes(SpiritualWoundStateJson.Canonical(fixture.Actual["before"]!))).Fingerprint;
        source["dangerDeclarationFingerprint"] = new CanonicalBeforeImage(true,
            Encoding.UTF8.GetBytes(SpiritualWoundStateJson.Canonical(new JsonObject { ["dangerMode"] = training ? "training" : "hostile" }))).Fingerprint;
        SpiritualExistingPressureTestData.Seal(source);
        var actual = SpiritualWoundSourceWitness.ValidateAndReadStrainData(source, out var comparison);
        var data = Assert.IsType<SpiritualStrainPreimageData>(comparison);
        Assert.Equal((previous, 4, 3 - previous, pressure, maximum, true),
            (actual.PreviousStrainRank, actual.NewStrainRank, actual.ExtraJumpSteps, actual.TraumaPressure, actual.MaximumSeverityRank, actual.HasIncreasingStrain));
        Assert.Equal(actual, data.OrdinaryCalculation);
        Assert.True(JsonNode.DeepEquals(fixture.Ordinary, data.ReadActualExchange()));
        Assert.Equal(actual, SpiritualStrainPreimageData.ReadIntrinsic(source, fixture.Actual).ActualCalculation);
    }

    /// <summary>Every resealed mutation follows an accepted capped baseline, so equality cannot hide sibling/parent/calculation contradictions.</summary>
    [Theory]
    [InlineData("ordinary-sibling")] [InlineData("ordinary-pressure")] [InlineData("ordinary-maximum")]
    [InlineData("parent-owner")] [InlineData("component")] [InlineData("already-broken")]
    [InlineData("no-effect")] [InlineData("ordinary-dice")]
    public void SingletonCapped_ResealedContradictionsRefuse(string mutation)
    {
        var fixture = SpiritualSingletonIvPlayerDataTests.Create(4, PlayerSingletonPressureContour.CappedBroken);
        var source = fixture.Source;
        _ = SpiritualWoundSourceWitness.ValidateAndReadStrainData(source, out var accepted);
        Assert.NotNull(accepted);
        var proof = source["strainPreimage"]!.AsObject();
        switch (mutation)
        {
            case "ordinary-sibling": fixture.Ordinary["summary"] = "A changed ordinary sibling."; break;
            case "ordinary-pressure": proof["ordinaryCalculation"]!["traumaPressure"] = 15; break;
            case "ordinary-maximum": proof["ordinaryCalculation"]!["maximumSeverityRank"] = 3; break;
            case "parent-owner":
                var parent = fixture.Retained.Parents.Single().DeepClone().AsObject();
                parent["owner"]!["ownerId"] = "foreign_soul";
                proof["parentWoundJsonBase64"] = SpiritualGuardianAttributionTestData.Encode(parent); break;
            case "component": proof["componentId"] = "foreign_component"; break;
            case "already-broken":
                fixture.Ordinary["before"]!["playerSideStrain"] = "broken";
                fixture.Actual["before"]!["playerSideStrain"] = "broken";
                source["priorStrainRank"] = 4; source["maximumSeverityRank"] = 0;
                proof["ordinaryCalculation"]!["maximumSeverityRank"] = 0; break;
            case "no-effect": fixture.Ordinary["outcome"] = "no_effect"; fixture.Actual["outcome"] = "no_effect"; break;
            case "ordinary-dice": fixture.Ordinary["diceAudit"]!["diceUsed"]![0]!["value"] = 5; break;
            default: throw new ArgumentOutOfRangeException(nameof(mutation));
        }
        proof["ordinaryExchangeJsonBase64"] = SpiritualGuardianAttributionTestData.Encode(fixture.Ordinary);
        source["exchangeJsonBase64"] = SpiritualGuardianAttributionTestData.Encode(fixture.Actual);
        SpiritualExistingPressureTestData.Seal(source);
        Assert.Throws<FormatException>(() => SpiritualWoundSourceWitness.ValidateAndReadStrainData(source, out _));
        Assert.Throws<FormatException>(() => SpiritualStrainPreimageData.ReadIntrinsic(source, fixture.Actual));
    }

    /// <summary>Exact capped source, retained parent, accepted log and History joins at both complete ranks.</summary>
    [Theory]
    [InlineData(3, "exact")] [InlineData(3, "history-drift")] [InlineData(3, "parent-drift")] [InlineData(3, "log-drift")]
    [InlineData(4, "exact")] [InlineData(4, "history-drift")] [InlineData(4, "parent-drift")] [InlineData(4, "log-drift")]
    public void SingletonCapped_ReceiptHistoryAndLogRemainExact(int rank, string change) =>
        SpiritualSingletonBrokenDataTests.AssertPlayerSingletonReceipt(rank, change, PlayerSingletonPressureContour.CappedBroken);
}
