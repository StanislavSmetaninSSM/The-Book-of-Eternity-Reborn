using System.Collections;
using System.Text;
using System.Text.Json;
using System.Text.Json.Nodes;
using BookOfEternityClient.Services;
using Xunit;
using Xunit.Abstractions;

namespace BookOfEternityClient.Tests;

/// <summary>Independent capped guardian prerequisites and separate desired v5 successes; DATA never supplies current authority.</summary>
/// <param name="output">This invocation's complete prerequisite and reader diagnostics.</param>
public sealed class SpiritualGuardianCappedPressureDataTests(ITestOutputHelper output)
{
    private const GuardianSingletonPressureContour Contour = GuardianSingletonPressureContour.CappedBroken;

    /// <summary>Proves complete parentIII/IV, exact raw ordinary root and both comparison-free pressure20/maxIII sources before v5 is read.</summary>
    /// <param name="rank">Independent complete retained parent severity fixed before authoring.</param>
    [Theory]
    [InlineData(3)] [InlineData(4)]
    public void GuardianCapped_CompletePrerequisites(int rank) => AssertPrerequisites(rank);

    /// <summary>Unconditional desired full-witness success; the current guardian ordinary-IV refusal must be a failure, never expected success.</summary>
    /// <param name="rank">Independent complete retained parent severity.</param>
    [Theory]
    [InlineData(3)] [InlineData(4)]
    public void GuardianCapped_FullWitnessAccepts(int rank)
    {
        var fixture = AssertPrerequisites(rank);
        var before = fixture.Source.ToJsonString();
        SpiritualStrainPreimageData? retained = null;
        SpiritualWoundCalculation? calculation = null;
        var error = Record.Exception(() => calculation = SpiritualWoundSourceWitness.ValidateAndReadStrainData(fixture.Source, out retained));
        WriteReaderObservation("guardian_capped_full_witness_desired_success", rank, fixture, error, retained);
        Assert.Equal(before, fixture.Source.ToJsonString());
        if (error is not null) Assert.Null(retained);
        Assert.Null(error);
        Assert.Equal(ExpectedCalculation(), calculation);
        var data = Assert.IsType<SpiritualStrainPreimageData>(retained);
        Assert.Same(calculation, data.ActualCalculation);
        AssertRetainedComparison(fixture, rank, data);
    }

    /// <summary>Separate desired direct-reader success, so a failed full-reader leaf cannot hide the direct intrinsic result.</summary>
    /// <param name="rank">Independent complete retained parent severity.</param>
    [Theory]
    [InlineData(3)] [InlineData(4)]
    public void GuardianCapped_DirectIntrinsicAccepts(int rank)
    {
        var fixture = AssertPrerequisites(rank);
        var before = fixture.Source.ToJsonString();
        SpiritualStrainPreimageData? retained = null;
        var error = Record.Exception(() => retained = SpiritualStrainPreimageData.ReadIntrinsic(fixture.Source, fixture.Actual));
        WriteReaderObservation("guardian_capped_direct_intrinsic_desired_success", rank, fixture, error, retained);
        Assert.Equal(before, fixture.Source.ToJsonString());
        Assert.Null(error);
        AssertRetainedComparison(fixture, rank, Assert.IsType<SpiritualStrainPreimageData>(retained));
    }

    /// <summary>Uses complete graph/current-shaped DATA readers and comparison-free witnesses, never the currently refusing desired v5 path.</summary>
    private SpiritualGuardianAttributionFixture AssertPrerequisites(int rank)
    {
        var fixture = SpiritualGuardianAttributionTestData.Create(rank: rank, contour: Contour);
        var before = fixture.Source.ToJsonString();
        var checks = SpiritualGuardianAttributionDataCheck.Check(fixture.Retained, fixture.Profiles, fixture.Conflict, rank);
        Assert.True(checks.Count == 0, JsonSerializer.Serialize(checks));
        var active = fixture.Conflict["activeConflict"]!.AsObject();
        var guardian = Assert.Single(fixture.Profiles["profiles"]!.AsArray().OfType<JsonObject>(),
            row => row["actorType"]!.GetValue<string>() == "guardian");
        Assert.Equal(0, guardian["standardArts"]![AfterlifeSpiritualConflictState.SpiritualResilienceArtId]!.GetValue<int>());
        Assert.True(JsonNode.DeepEquals(guardian["standardArts"], active["oppositionSide"]!["leadContestant"]!["actorArtTierSnapshot"]));
        Assert.Equal(("direct_duel", "hostile", "active", "clear", "broken"),
            (active["sideModel"]!.GetValue<string>(), active["dangerMode"]!.GetValue<string>(), active["resolutionState"]!.GetValue<string>(),
                active["playerSideStrain"]!.GetValue<string>(), active["oppositionSideStrain"]!.GetValue<string>()));
        Assert.True(JsonNode.DeepEquals(fixture.Ordinary, Assert.Single(active["exchangeLog"]!.AsArray())));
        Assert.True(JsonNode.DeepEquals(fixture.Ordinary, fixture.Actual));

        var issues = new List<ValidationIssue>();
        foreach (var exchange in new[] { fixture.Ordinary, fixture.Actual })
        {
            ValidationService.ValidateRetainedSpiritualExchangeShape(exchange, "guardianCapped.prerequisite", issues);
            ValidationService.ValidateSpiritualWoundSourceActionShape(exchange, "guardianCapped.prerequisite", issues);
            ValidationService.ValidateRetainedSpiritualDiceAudit(exchange, fixture.Source["turnEvidence"]!.AsObject(), issues);
            ValidationService.ValidateRetainedSpiritualExchangeRules(exchange, issues);
            Assert.Equal(("pressure", "success", "strained", "broken", 12, 4, 8, "decisive_player_success"),
                (exchange["operationType"]!.GetValue<string>(), exchange["outcome"]!.GetValue<string>(),
                    exchange["before"]!["oppositionSideStrain"]!.GetValue<string>(), exchange["after"]!["oppositionSideStrain"]!.GetValue<string>(),
                    exchange["diceAudit"]!["playerTotal"]!.GetValue<int>(), exchange["diceAudit"]!["oppositionTotal"]!.GetValue<int>(),
                    exchange["diceAudit"]!["margin"]!.GetValue<int>(), exchange["diceAudit"]!["outcomeBand"]!.GetValue<string>()));
            Assert.Equal(SpiritualGuardianAttributionTestData.GuardianId, SpiritualWoundFailedAction.ReadGuardianDeclaration(exchange));
            Assert.True(JsonNode.DeepEquals(SpiritualGuardianAttributionTestData.FailedAction(), exchange[SpiritualWoundFailedAction.Property]));
            foreach (var alias in new[] { "actorType", "ownerActorType" })
                Assert.Equal("guardian", exchange["incomingAction"]![alias]!.GetValue<string>());
            foreach (var alias in new[] { "actorId", "actorRef", "ownerActorId", "guardianId", "id" })
                Assert.Equal(SpiritualGuardianAttributionTestData.GuardianId, exchange["incomingAction"]![alias]!.GetValue<string>());
            foreach (var field in new[] { "playerSideStrain", "conflictPosition", "controlState", "tempoAdvantage" })
                Assert.True(JsonNode.DeepEquals(exchange["before"]![field], exchange["after"]![field]), field);
            foreach (var side in new[] { "player", "opposition" })
                Assert.Equal((0, 3m, 3m, 0m), (exchange["actionCostAudit"]![side]!["artTier"]!.GetValue<int>(),
                    exchange["actionCostAudit"]![side]!["effectiveCost"]!.GetValue<decimal>(),
                    exchange["actionCostAudit"]![side]!["before"]!.GetValue<decimal>(), exchange["actionCostAudit"]![side]!["after"]!.GetValue<decimal>()));
        }
        Assert.True(issues.Count == 0, JsonSerializer.Serialize(issues));
        var fingerprint = new CanonicalBeforeImage(true, fixture.RawInput).Fingerprint;
        var ordinarySource = SpiritualGuardianAttributionTestData.CreateSource(fixture.Ordinary,
            "capped_ordinary_session", "capped_ordinary_request", "capped_ordinary_snapshot", "capped_ordinary_conflict", fingerprint);
        var actualSource = fixture.Source.DeepClone().AsObject();
        Assert.True(actualSource.Remove("strainPreimage"));
        SpiritualExistingPressureTestData.Seal(actualSource);
        Assert.False(ordinarySource.ContainsKey("strainPreimage"));
        Assert.False(actualSource.ContainsKey("strainPreimage"));
        var ordinary = SpiritualWoundSourceWitness.ValidateAndReadStrainData(ordinarySource, out var noOrdinary);
        var actual = SpiritualWoundSourceWitness.ValidateAndReadStrainData(actualSource, out var noActual);
        Assert.Null(noOrdinary); Assert.Null(noActual);
        foreach (var calculation in new[] { ordinary, actual })
        {
            Assert.Equal(ExpectedCalculation(), calculation);
            Assert.Equal((1, 4, 2, 20L, 3, 4, 4, true, 3),
                (calculation.PreviousStrainRank, calculation.NewStrainRank, calculation.ExtraJumpSteps,
                    calculation.TraumaPressure, calculation.FormulaSeverityRank, calculation.DestinationSeverityCap,
                    calculation.DangerModeSeverityCap, calculation.HasIncreasingStrain, calculation.MaximumSeverityRank));
        }
        Assert.Equal(ExpectedCalculation(), SpiritualGuardianAttributionTestData.OrdinaryCalculation(Contour));
        Assert.Equal(ExpectedCalculation(), SpiritualGuardianAttributionTestData.ActualCalculation(Contour));
        Assert.Equal(ExpectedCalculation(), SpiritualWoundOpportunityMath.Calculate(ExpectedCalculation().Input));
        foreach (var source in new[] { ordinarySource, actualSource })
        {
            Assert.Equal(("standard", 0, 0, 8, 3), (source["appliedArtKind"]!.GetValue<string>(), source["appliedArtTier"]!.GetValue<int>(),
                source["targetResilienceTier"]!.GetValue<int>(), source["harmfulMargin"]!.GetValue<int>(), source["maximumSeverityRank"]!.GetValue<int>()));
            Assert.Null(source["guaranteedSeverityRank"]); Assert.Null(source["retraumaWoundId"]);
        }
        Assert.True(fixture.RawInput.AsSpan().StartsWith(Encoding.UTF8.GetPreamble()));
        Assert.True(JsonNode.DeepEquals(fixture.Conflict, SpiritualWoundStateJson.Parse(SpiritualWoundStateJson.DecodeUtf8JsonText(fixture.RawInput))));
        Assert.NotEqual(fingerprint, new CanonicalBeforeImage(true, Encoding.UTF8.GetBytes(fixture.Conflict.ToJsonString())).Fingerprint);
        var input = fixture.Source["strainPreimage"]!["originalInput"]!;
        Assert.Equal((AfterlifeSpiritualConflictState.StatePath, true, fingerprint, "/activeConflict/exchangeLog/0"),
            (input["path"]!.GetValue<string>(), input["existed"]!.GetValue<bool>(), input["contentFingerprint"]!.GetValue<string>(), input["exchangePointer"]!.GetValue<string>()));
        var actualRoot = fixture.Conflict.DeepClone();
        actualRoot["activeConflict"]!["exchangeLog"]![0] = fixture.Actual.DeepClone();
        actualRoot["activeConflict"]!["oppositionSideStrain"] = "broken";
        Assert.True(JsonNode.DeepEquals(fixture.Conflict, actualRoot));
        Assert.Equal(before, fixture.Source.ToJsonString());
        output.WriteLine(JsonSerializer.Serialize(new { phase = "guardian_capped_lawful_comparison_free_prerequisites", rank,
            ordinary, actual, comparisonFreeOrdinary = ordinarySource, comparisonFreeActual = actualSource,
            profiles = fixture.Profiles, ordinaryRoot = fixture.Conflict, retained = fixture.Retained,
            rawInputBase64 = Convert.ToBase64String(fixture.RawInput), fingerprint }));
        return fixture;
    }

    /// <summary>Requires complete detached v5 comparison evidence despite equal ordinary/actual roots.</summary>
    private static void AssertRetainedComparison(SpiritualGuardianAttributionFixture fixture, int rank, SpiritualStrainPreimageData data)
    {
        var before = fixture.Source.ToJsonString();
        Assert.Equal(5, data.Version); Assert.Single(data.Parents); Assert.Null(data.ParentSourceId);
        Assert.Equal((rank, "guardian", SpiritualGuardianAttributionTestData.GuardianId),
            (data.ParentWound.Severity.Rank, data.ParentWound.Owner.OwnerKind, data.ParentWound.Owner.OwnerId));
        Assert.Equal(ExpectedCalculation(), data.OrdinaryCalculation); Assert.Equal(ExpectedCalculation(), data.ActualCalculation);
        Assert.True(JsonNode.DeepEquals(Assert.Single(fixture.Retained.Parents), JsonNode.Parse(WoundMaterializationContract.SerializeCanonical(data.ParentWound))));
        Assert.Equal(Assert.Single(fixture.Retained.Effects)["effectId"]!.GetValue<string>(), data.EffectId);
        Assert.Equal("component_001", data.ComponentId);
        Assert.Equal(new CanonicalBeforeImage(true, fixture.RawInput).Fingerprint, data.OriginalInputFingerprint);
        Assert.Equal("/activeConflict/exchangeLog/0", data.OriginalExchangePointer);
        Assert.True(JsonNode.DeepEquals(fixture.Actual, data.ReadActualExchange()));
        Assert.True(JsonNode.DeepEquals(fixture.Ordinary["after"], data.ReadOrdinaryAfter(fixture.Actual)));
        Assert.True(JsonNode.DeepEquals(SpiritualGuardianAttributionTestData.FailedAction(), data.ReadActualExchange()[SpiritualWoundFailedAction.Property]));
        data.ReadActualExchange()["after"]!["oppositionSideStrain"] = "clear";
        data.ReadOrdinaryAfter(fixture.Actual)["oppositionSideStrain"] = "clear";
        Assert.True(JsonNode.DeepEquals(fixture.Actual, data.ReadActualExchange()));
        Assert.True(JsonNode.DeepEquals(fixture.Ordinary["after"], data.ReadOrdinaryAfter(fixture.Actual)));
        Assert.Equal(before, fixture.Source.ToJsonString());
    }

    /// <summary>Logs the full actual reader refusal before its unconditional success assertion; no exception is converted to a passing result.</summary>
    private void WriteReaderObservation(string phase, int rank, SpiritualGuardianAttributionFixture fixture,
        Exception? error, SpiritualStrainPreimageData? retained) => output.WriteLine(JsonSerializer.Serialize(new
        {
            phase, rank, causalTarget = "existing guardian-v5 independently ordinary-IV rank eligibility, not ordinary pressure legality",
            error = error?.ToString(), exceptionDataCount = error?.Data.Count,
            exceptionData = error?.Data.Cast<DictionaryEntry>().Select(entry => new
                { key = entry.Key?.ToString(), value = entry.Value?.ToString() }).ToArray(),
            comparisonReturned = retained is not null, source = fixture.Source, ordinaryRoot = fixture.Conflict,
            ordinary = fixture.Ordinary, actual = fixture.Actual, rawInputBase64 = Convert.ToBase64String(fixture.RawInput)
        }));

    /// <summary>Independent literal arithmetic; no production result selects the expected capped geometry.</summary>
    private static SpiritualWoundCalculation ExpectedCalculation() =>
        new(new(8, 0, 0, "strained", "broken", "hostile", 4), 1, 4, 2, 20, 3, 4, 4, true, 3);
}
