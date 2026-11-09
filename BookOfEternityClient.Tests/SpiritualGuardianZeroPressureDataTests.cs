using System.Text;
using System.Text.Json;
using System.Text.Json.Nodes;
using BookOfEternityClient.Services;
using Xunit;
using Xunit.Abstractions;

namespace BookOfEternityClient.Tests;

/// <summary>Independent lawful prerequisites and separate desired-success v5 readers; detached DATA never grants current authority.</summary>
public sealed class SpiritualGuardianZeroPressureDataTests(ITestOutputHelper output)
{
    /// <summary>Proves complete rankIII/IV originals and comparison-free ordinary2/max0 and actual7/max0 before interpreting RED.</summary>
    /// <param name="rank">Independent complete retained parent rank.</param>
    [Theory]
    [InlineData(3)] [InlineData(4)]
    public void GuardianZero_PrerequisiteIsLawful(int rank) => AssertPrerequisites(SpiritualGuardianZeroPressureTestData.Create(rank));

    /// <summary>Separately proves the ordinary6/max0 to actual11/maxI sentinel without invoking a v5 comparison reader.</summary>
    [Fact]
    public void GuardianZero_OrdinaryZeroPositiveActualPrerequisiteIsLawful() =>
        AssertPrerequisites(SpiritualGuardianZeroPressureTestData.CreateOrdinaryZeroPositiveActual());

    /// <summary>Requires unconditional full-witness success for each lawful actual-zero v5 rank, preserving failure output-clearing evidence.</summary>
    /// <param name="rank">Independent complete retained parent rank.</param>
    [Theory]
    [InlineData(3)] [InlineData(4)]
    public void GuardianZero_FullWitnessAccepts(int rank) => AssertFullReader(SpiritualGuardianZeroPressureTestData.Create(rank));

    /// <summary>Requires a separate direct-reader success, so a failing full-reader test cannot hide its result.</summary>
    /// <param name="rank">Independent complete retained parent rank.</param>
    [Theory]
    [InlineData(3)] [InlineData(4)]
    public void GuardianZero_DirectIntrinsicAccepts(int rank) => AssertDirectReader(SpiritualGuardianZeroPressureTestData.Create(rank));

    /// <summary>Exposes the ordinary-positive guard independently while the actual maximum remains positive.</summary>
    [Fact]
    public void GuardianZero_FullWitnessAcceptsOrdinaryZeroPositiveActual() =>
        AssertFullReader(SpiritualGuardianZeroPressureTestData.CreateOrdinaryZeroPositiveActual());

    /// <summary>Separately requires the direct v5 reader to retain ordinary-zero/positive-actual DATA.</summary>
    [Fact]
    public void GuardianZero_DirectIntrinsicAcceptsOrdinaryZeroPositiveActual() =>
        AssertDirectReader(SpiritualGuardianZeroPressureTestData.CreateOrdinaryZeroPositiveActual());

    /// <summary>Audits exact original profile/snapshot, independent historical ancestry, source/dice/root and literal calculation prerequisites.</summary>
    /// <param name="fixture">Fresh complete primary or sentinel DATA, never an owner.</param>
    private void AssertPrerequisites(GuardianZeroPressureData fixture)
    {
        var original = fixture.Source.ToJsonString();
        var issues = SpiritualGuardianAttributionDataCheck.Check(fixture.Retained, fixture.Profiles, fixture.Conflict, fixture.Rank);
        Assert.True(issues.Count == 0, JsonSerializer.Serialize(issues));
        var active = fixture.Conflict["activeConflict"]!.AsObject();
        var guardian = Assert.Single(fixture.Profiles["profiles"]!.AsArray().OfType<JsonObject>(),
            row => row["actorType"]!.GetValue<string>() == "guardian");
        Assert.Equal(fixture.Resilience, guardian["standardArts"]![AfterlifeSpiritualConflictState.SpiritualResilienceArtId]!.GetValue<int>());
        Assert.True(JsonNode.DeepEquals(guardian["standardArts"], active["oppositionSide"]!["leadContestant"]!["actorArtTierSnapshot"]));
        Assert.Equal(("direct_duel", "hostile", "clear", "fractured"), (active["sideModel"]!.GetValue<string>(),
            active["dangerMode"]!.GetValue<string>(), active["playerSideStrain"]!.GetValue<string>(), active["oppositionSideStrain"]!.GetValue<string>()));
        Assert.True(JsonNode.DeepEquals(fixture.Ordinary, Assert.Single(active["exchangeLog"]!.AsArray())));
        var expectedBirth = SpiritualGuardianAttributionTestData.CreateRetained(rank: fixture.Rank);
        foreach (var (expected, retainedImage) in new[]
        {
            (expectedBirth.Parents.Single(), fixture.Retained.Parents.Single()),
            (expectedBirth.Effects.Single(), fixture.Retained.Effects.Single()),
            (expectedBirth.WoundIdentities, fixture.Retained.WoundIdentities), (expectedBirth.History, fixture.Retained.History),
            (expectedBirth.EffectIdentities, fixture.Retained.EffectIdentities), (expectedBirth.Receipts, fixture.Retained.Receipts)
        }) Assert.Equal(expected.ToJsonString(), retainedImage.ToJsonString());
        var birthSource = fixture.Retained.Receipts["sources"]![0]!["witness"]!.AsObject();
        Assert.Equal(0, birthSource["targetResilienceTier"]!.GetValue<int>());
        var birth = SpiritualWoundSourceWitness.Validate(birthSource);
        Assert.Equal(fixture.Rank == 4 ? 25L : 20L, birth.TraumaPressure);
        Assert.Equal(fixture.Rank, birth.MaximumSeverityRank);

        var checks = new List<ValidationIssue>();
        foreach (var exchange in new[] { fixture.Ordinary, fixture.Actual })
        {
            ValidationService.ValidateRetainedSpiritualExchangeShape(exchange, "guardianZero.prerequisite", checks);
            ValidationService.ValidateSpiritualWoundSourceActionShape(exchange, "guardianZero.prerequisite", checks);
            ValidationService.ValidateRetainedSpiritualDiceAudit(exchange, fixture.Source["turnEvidence"]!.AsObject(), checks);
            ValidationService.ValidateRetainedSpiritualExchangeRules(exchange, checks);
            Assert.Equal(("pressure", "success", 12, 4, 8, "decisive_player_success"),
                (exchange["operationType"]!.GetValue<string>(), exchange["outcome"]!.GetValue<string>(),
                    exchange["diceAudit"]!["playerTotal"]!.GetValue<int>(), exchange["diceAudit"]!["oppositionTotal"]!.GetValue<int>(),
                    exchange["diceAudit"]!["margin"]!.GetValue<int>(), exchange["diceAudit"]!["outcomeBand"]!.GetValue<string>()));
            Assert.Equal(SpiritualGuardianAttributionTestData.GuardianId, SpiritualWoundFailedAction.ReadGuardianDeclaration(exchange));
            Assert.True(JsonNode.DeepEquals(SpiritualGuardianAttributionTestData.FailedAction(), exchange[SpiritualWoundFailedAction.Property]));
            foreach (var alias in new[] { "actorId", "actorRef", "ownerActorId", "guardianId", "id" })
                Assert.Equal(SpiritualGuardianAttributionTestData.GuardianId, exchange["incomingAction"]![alias]!.GetValue<string>());
            foreach (var side in new[] { "player", "opposition" })
                Assert.Equal((0, 3m, 3m, 0m), (exchange["actionCostAudit"]![side]!["artTier"]!.GetValue<int>(),
                    exchange["actionCostAudit"]![side]!["effectiveCost"]!.GetValue<decimal>(),
                    exchange["actionCostAudit"]![side]!["before"]!.GetValue<decimal>(), exchange["actionCostAudit"]![side]!["after"]!.GetValue<decimal>()));
        }
        Assert.True(checks.Count == 0, JsonSerializer.Serialize(checks));
        var ordinarySource = SpiritualGuardianZeroPressureTestData.CreateComparisonFreeSource(fixture.Ordinary, fixture.Resilience,
            "ordinary_session", "ordinary_request", "ordinary_snapshot", "conflict_guardian_zero",
            new CanonicalBeforeImage(true, fixture.RawOrdinaryBytes).Fingerprint);
        var ordinary = SpiritualWoundSourceWitness.ValidateAndReadStrainData(ordinarySource, out var noOrdinary);
        Assert.Null(noOrdinary); Assert.Equal(fixture.OrdinaryCalculation, ordinary);
        var actualOnly = fixture.Source.DeepClone().AsObject(); Assert.True(actualOnly.Remove("strainPreimage"));
        SpiritualExistingPressureTestData.Seal(actualOnly);
        var actual = SpiritualWoundSourceWitness.ValidateAndReadStrainData(actualOnly, out var noActual);
        Assert.Null(noActual); Assert.Equal(fixture.ActualCalculation, actual);
        Assert.Equal(fixture.OrdinaryCalculation, SpiritualWoundOpportunityMath.Calculate(fixture.OrdinaryCalculation.Input));
        Assert.Equal(fixture.ActualCalculation, SpiritualWoundOpportunityMath.Calculate(fixture.ActualCalculation.Input));
        Assert.Equal((fixture.Resilience == 4 ? 2L : 6L, 0, 0, 2),
            (ordinary.TraumaPressure, ordinary.MaximumSeverityRank, ordinary.ExtraJumpSteps, ordinary.DestinationSeverityCap));
        Assert.Equal((fixture.Resilience == 4 ? 7L : 11L, fixture.Resilience == 4 ? 0 : 1, 1, 3),
            (actual.TraumaPressure, actual.MaximumSeverityRank, actual.ExtraJumpSteps, actual.DestinationSeverityCap));
        Assert.True(ordinary.HasIncreasingStrain); Assert.True(actual.HasIncreasingStrain);
        Assert.Equal((4, 4), (ordinary.DangerModeSeverityCap, actual.DangerModeSeverityCap));
        Assert.Null(ordinarySource["guaranteedSeverityRank"]); Assert.Null(actualOnly["guaranteedSeverityRank"]);
        Assert.True(fixture.RawOrdinaryBytes.AsSpan().StartsWith(Encoding.UTF8.GetPreamble()));
        Assert.True(JsonNode.DeepEquals(fixture.Conflict,
            SpiritualWoundStateJson.Parse(SpiritualWoundStateJson.DecodeUtf8JsonText(fixture.RawOrdinaryBytes))));
        var fingerprint = new CanonicalBeforeImage(true, fixture.RawOrdinaryBytes).Fingerprint;
        Assert.NotEqual(fingerprint, new CanonicalBeforeImage(true, Encoding.UTF8.GetBytes(fixture.Conflict.ToJsonString())).Fingerprint);
        Assert.Equal(fingerprint, fixture.Source["strainPreimage"]!["originalInput"]!["contentFingerprint"]!.GetValue<string>());
        Assert.Equal("/activeConflict/exchangeLog/0", fixture.Source["strainPreimage"]!["originalInput"]!["exchangePointer"]!.GetValue<string>());
        var expectedActual = fixture.Ordinary.DeepClone(); expectedActual["after"]!["oppositionSideStrain"] = "overwhelmed";
        Assert.True(JsonNode.DeepEquals(expectedActual, fixture.Actual));
        Assert.Equal(original, fixture.Source.ToJsonString());
        output.WriteLine(JsonSerializer.Serialize(new { phase = "guardian_zero_lawful_prerequisites", fixture.Rank, fixture.Resilience,
            ordinary, actual, comparisonFreeOrdinary = ordinarySource, comparisonFreeActual = actualOnly,
            rawOrdinaryBytesBase64 = Convert.ToBase64String(fixture.RawOrdinaryBytes), fingerprint,
            parentJson = fixture.Retained.Parents.Single().ToJsonString(), historicalReceiptJson = fixture.Retained.Receipts.ToJsonString() }));
    }

    /// <summary>Captures the exact failing full read while retaining unconditional desired success and cleared preloaded out-DATA.</summary>
    private void AssertFullReader(GuardianZeroPressureData fixture)
    {
        AssertPrerequisites(fixture);
        _ = SpiritualWoundSourceWitness.ValidateAndReadStrainData(SpiritualGuardianAttributionTestData.Create().Source, out var data);
        Assert.NotNull(data);
        var original = fixture.Source.ToJsonString();
        SpiritualWoundCalculation? actual = null;
        var error = Record.Exception(() => actual = SpiritualWoundSourceWitness.ValidateAndReadStrainData(fixture.Source, out data));
        output.WriteLine(JsonSerializer.Serialize(new { phase = "guardian_zero_full_reader_desired_success", fixture.Rank, fixture.Resilience,
            error = error?.ToString(), actual, comparisonReturned = data is not null, sourceJson = original }));
        Assert.Equal(original, fixture.Source.ToJsonString());
        if (error is not null) Assert.Null(data);
        Assert.Null(error);
        Assert.Equal(fixture.ActualCalculation, actual);
        AssertComparison(fixture, Assert.IsType<SpiritualStrainPreimageData>(data));
    }

    /// <summary>Captures direct-reader failure independently of the full-reader test's outcome.</summary>
    private void AssertDirectReader(GuardianZeroPressureData fixture)
    {
        AssertPrerequisites(fixture);
        var original = fixture.Source.ToJsonString(); var actual = fixture.Actual.ToJsonString();
        SpiritualStrainPreimageData? data = null;
        var error = Record.Exception(() => data = SpiritualStrainPreimageData.ReadIntrinsic(fixture.Source, fixture.Actual));
        output.WriteLine(JsonSerializer.Serialize(new { phase = "guardian_zero_direct_reader_desired_success", fixture.Rank, fixture.Resilience,
            error = error?.ToString(), comparisonReturned = data is not null, sourceJson = original, actualJson = actual }));
        Assert.Equal(original, fixture.Source.ToJsonString()); Assert.Equal(actual, fixture.Actual.ToJsonString());
        Assert.Null(error);
        AssertComparison(fixture, Assert.IsType<SpiritualStrainPreimageData>(data));
    }

    /// <summary>Checks every retained comparison coordinate and detached exchange view without treating DATA as an owner.</summary>
    private static void AssertComparison(GuardianZeroPressureData fixture, SpiritualStrainPreimageData data)
    {
        Assert.Equal(5, data.Version); Assert.Single(data.Parents); Assert.Null(data.ParentSourceId);
        Assert.Equal(fixture.Rank, data.ParentWound.Severity.Rank);
        Assert.Equal(("guardian", SpiritualGuardianAttributionTestData.GuardianId), (data.ParentWound.Owner.OwnerKind, data.ParentWound.Owner.OwnerId));
        Assert.True(JsonNode.DeepEquals(fixture.Retained.Parents.Single(), JsonNode.Parse(WoundMaterializationContract.SerializeCanonical(data.ParentWound))));
        Assert.Equal(fixture.Retained.Effects.Single()["effectId"]!.GetValue<string>(), data.EffectId); Assert.Equal("component_001", data.ComponentId);
        Assert.Equal(fixture.OrdinaryCalculation, data.OrdinaryCalculation); Assert.Equal(fixture.ActualCalculation, data.ActualCalculation);
        Assert.Equal(new CanonicalBeforeImage(true, fixture.RawOrdinaryBytes).Fingerprint, data.OriginalInputFingerprint);
        Assert.Equal("/activeConflict/exchangeLog/0", data.OriginalExchangePointer);
        Assert.True(JsonNode.DeepEquals(fixture.Actual, data.ReadActualExchange()));
        Assert.True(JsonNode.DeepEquals(fixture.Ordinary["after"], data.ReadOrdinaryAfter(fixture.Actual)));
        data.ReadActualExchange()["after"]!["oppositionSideStrain"] = "broken";
        data.ReadOrdinaryAfter(fixture.Actual)["oppositionSideStrain"] = "clear";
        Assert.True(JsonNode.DeepEquals(fixture.Actual, data.ReadActualExchange()));
        Assert.True(JsonNode.DeepEquals(fixture.Ordinary["after"], data.ReadOrdinaryAfter(fixture.Actual)));
    }
}
