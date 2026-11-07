using System.Text;
using System.Text.Json;
using System.Text.Json.Nodes;
using BookOfEternityClient.Services;
using Xunit;
using Xunit.Abstractions;

namespace BookOfEternityClient.Tests;

/// <summary>New reconstructed guardian DATA contracts. No test creates an owner or authenticates a historical birth.</summary>
public sealed partial class SpiritualGuardianAttributionDataTests(ITestOutputHelper output)
{
    /// <summary>Requires every complete parent/source/effect/identity/History/receipt join before any real owner fixture.</summary>
    [Fact]
    public void GuardianFixture_CompleteRetainedGraphAndLedgerJoins()
    {
        var fixture = SpiritualGuardianAttributionTestData.Create();
        Assert.Empty(SpiritualGuardianAttributionDataCheck.Check(fixture.Retained, fixture.Profiles, fixture.Conflict));
    }

    /// <summary>Proves lawful ordinary pressure/success, authoritative dice and both standard costs without invoking the missing attribution consumer.</summary>
    [Fact]
    public void GuardianFixture_OrdinaryRulesAndIndependentArithmetic()
    {
        var fixture = SpiritualGuardianAttributionTestData.Create();
        AssertPrerequisites(fixture);
        var ordinary = fixture.Ordinary;
        var evidence = fixture.Source["turnEvidence"]!.AsObject();
        var issues = new List<ValidationIssue>();
        ValidationService.ValidateRetainedSpiritualExchangeShape(ordinary, "guardian.ordinary", issues);
        ValidationService.ValidateSpiritualWoundSourceActionShape(ordinary, "guardian.ordinary", issues);
        ValidationService.ValidateRetainedSpiritualDiceAudit(ordinary, evidence, issues);
        ValidationService.ValidateRetainedSpiritualExchangeRules(ordinary, issues);
        Assert.Empty(issues);
        Assert.Equal((12, 4, 8, "decisive_player_success", "success"),
            (ordinary["diceAudit"]!["playerTotal"]!.GetValue<int>(), ordinary["diceAudit"]!["oppositionTotal"]!.GetValue<int>(),
             ordinary["diceAudit"]!["margin"]!.GetValue<int>(), ordinary["diceAudit"]!["outcomeBand"]!.GetValue<string>(), ordinary["outcome"]!.GetValue<string>()));
        Assert.Equal(new[] { 12, 4 }, evidence["acceptedD20Values"]!.AsArray().Select(x => x!.GetValue<int>()));
        Assert.True(AfterlifeActionCostRules.TryGetDefinition("pressure", out var definition));
        Assert.Equal(3, AfterlifeActionCostRules.ResolveStandardEffectiveCost(definition, 0));
        foreach (var side in new[] { "player", "opposition" })
        {
            var cost = ordinary["actionCostAudit"]![side]!;
            Assert.Equal((3m, 3m, 0m, 0), (cost["effectiveCost"]!.GetValue<decimal>(), cost["before"]!.GetValue<decimal>(),
                cost["after"]!.GetValue<decimal>(), cost["artTier"]!.GetValue<int>()));
        }
        var source = SpiritualGuardianAttributionTestData.CreateSource(ordinary, "ordinary-session", "ordinary-request", "ordinary-snapshot",
            "ordinary-conflict", new CanonicalBeforeImage(true, fixture.RawInput).Fingerprint);
        var ordinaryCalculation = SpiritualWoundSourceWitness.ValidateAndReadStrainData(source, out var noComparison);
        Assert.Null(noComparison); // Ordinary intrinsic harm DATA, not proof that a live guardian burden is resolved.
        Assert.Equal((10L, 1), (ordinaryCalculation.TraumaPressure, ordinaryCalculation.MaximumSeverityRank));
        var actualCalculation = ActualCalculation();
        Assert.Equal((15L, 2), (actualCalculation.TraumaPressure, actualCalculation.MaximumSeverityRank));
    }

    /// <summary>Uses actual accepted typed component DATA to isolate only the selected strain row, preserving both sibling contributions.</summary>
    [Fact]
    public void GuardianFixture_ExactTargetAndSelectedComponentMapping()
    {
        var fixture = SpiritualGuardianAttributionTestData.Create();
        AssertPrerequisites(fixture);
        var carriers = new EffectCarrierCatalogInput(null, null, null, null, fixture.Profiles, fixture.Conflict);
        var snapshot = EffectMechanicsSnapshot.Build(new(carriers, fixture.Retained.EffectIdentities));
        Assert.True(snapshot.IsAccepted, string.Join("\n", snapshot.Issues));
        var roots = SpiritualGuardianAttributionDataCheck.Roots(fixture.Retained, fixture.Profiles, fixture.Conflict);
        var sources = EffectAcceptedTurnInputComposer.BuildCanonicalSourceAuthority(roots);
        var targets = EffectAcceptedTurnInputComposer.BuildCanonicalTargetAuthority(carriers, roots);
        var projection = SpiritualWoundConflictContributionProjector.Project(snapshot, sources, targets, fixture.Conflict);
        Assert.Empty(projection.Issues);
        var selected = Assert.Single(projection.Contributions, row => row.Profile == "spiritual_strain_burden");
        Assert.Equal(("guardian", SpiritualGuardianAttributionTestData.GuardianId, "opposition", "pressure", "oppositionSideStrain", 1, 1),
            (selected.Actor.Kind, selected.Actor.TargetId, selected.ResolvedSide, selected.Operation, selected.ResolvedAxis,
             selected.Magnitude.GetInt32(), selected.CurrentStacks));
        // This detached DATA view grants no live isolation/owner capability.
        var isolated = snapshot with { Components = snapshot.Components.Where(row =>
            row.EffectId != selected.EffectId || row.ComponentId != selected.ComponentId).ToArray() };
        var ordinaryProjection = SpiritualWoundConflictContributionProjector.Project(isolated, sources, targets, fixture.Conflict);
        Assert.Empty(ordinaryProjection.Issues);
        Assert.Equal(2, ordinaryProjection.Contributions.Count);
        Assert.Equal(projection.Contributions.Where(row => row.ComponentId != selected.ComponentId).Select(RowJson),
            ordinaryProjection.Contributions.Select(RowJson));
        Assert.Single(ordinaryProjection.Contributions, row => row.Profile == "spiritual_roll_hindrance" && row.Operation == "counter");
        Assert.Single(ordinaryProjection.Contributions, row => row.Profile == "spiritual_action_cost_burden" && row.Operation == "guard");
        Assert.Equal(3, snapshot.Components.Count);
    }

    /// <summary>Preserves raw-byte identity including BOM/whitespace and changes exactly the selected guardian strain scalar.</summary>
    [Fact]
    public void GuardianFixture_RawInputAndWholeRecordExpectation()
    {
        var fixture = SpiritualGuardianAttributionTestData.Create();
        AssertPrerequisites(fixture);
        Assert.True(fixture.RawInput.AsSpan().StartsWith(Encoding.UTF8.GetPreamble()));
        var decoded = SpiritualWoundStateJson.Parse(SpiritualWoundStateJson.DecodeUtf8JsonText(fixture.RawInput));
        Assert.True(JsonNode.DeepEquals(fixture.Conflict, decoded));
        Assert.Equal(new CanonicalBeforeImage(true, fixture.RawInput).Fingerprint,
            fixture.Source["strainPreimage"]!["originalInput"]!["contentFingerprint"]!.GetValue<string>());
        Assert.NotEqual(new CanonicalBeforeImage(true, Encoding.UTF8.GetBytes(decoded.ToJsonString())).Fingerprint,
            new CanonicalBeforeImage(true, fixture.RawInput).Fingerprint);
        var expected = fixture.Ordinary.DeepClone(); expected["after"]!["oppositionSideStrain"] = "overwhelmed";
        Assert.True(JsonNode.DeepEquals(expected, fixture.Actual));
        Assert.Equal("strained", fixture.Actual["before"]!["oppositionSideStrain"]!.GetValue<string>());
        foreach (var field in new[] { "playerSideStrain", "conflictPosition", "controlState", "tempoAdvantage" })
            Assert.True(JsonNode.DeepEquals(fixture.Ordinary["before"]![field], fixture.Actual["after"]![field]), field);
    }

    /// <summary>A lawful old guardian wound was caused by player harm; that record does not declare a failed guardian action.</summary>
    [Fact]
    public void GuardianFixture_HistoricalHarmIsSeparateFromFailedAction()
    {
        var fixture = SpiritualGuardianAttributionTestData.Create();
        AssertPrerequisites(fixture);
        var source = fixture.Retained.Receipts["sources"]![0]!["witness"]!.AsObject();
        var exchange = SpiritualExistingPressureTestData.ReadActual(source);
        Assert.False(exchange.ContainsKey("spiritualWoundFailedAction"));
        Assert.Equal("success", exchange["outcome"]!.GetValue<string>());
        Assert.Equal("player_soul", source["actingActor"]!["actorKind"]!.GetValue<string>());
        Assert.Equal("guardian", source["affectedActor"]!["actorKind"]!.GetValue<string>());
        var calculation = SpiritualWoundSourceWitness.Validate(source);
        Assert.Equal((20L, 3), (calculation.TraumaPressure, calculation.MaximumSeverityRank));
    }

    /// <summary>The declaration does not turn a state-changing no_effect exchange into a lawful ordinary exchange.</summary>
    [Fact]
    public void GuardianFixture_NoEffectStillRequiresInvariantSnapshots()
    {
        var exchange = SpiritualGuardianAttributionTestData.Create().Ordinary;
        exchange["outcome"] = "no_effect";
        var issues = new List<ValidationIssue>();
        ValidationService.ValidateRetainedSpiritualExchangeShape(exchange, "guardian.no_effect", issues);
        ValidationService.ValidateRetainedSpiritualExchangeRules(exchange, issues);
        Assert.Contains(issues, issue => issue.Code == "afterlife_conflict_no_effect_has_state_delta");
    }

    /// <summary>Every invocation owns detached mutable DATA; no global fixture cache or active owner is shared.</summary>
    [Fact]
    public void GuardianFixture_CopiesAreIndependent()
    {
        var first = SpiritualGuardianAttributionTestData.Create(); var second = SpiritualGuardianAttributionTestData.Create("guardian_fixture_other");
        var original = second.Source.ToJsonString();
        first.Source["affectedActor"]!["actorId"] = "foreign"; first.RawInput[0] ^= 1;
        first.Retained.Parents.Single()["display"]!["name"] = "Изменённая копия";
        Assert.Equal(original, second.Source.ToJsonString());
        AssertPrerequisites(second);
    }

    /// <summary>Existing closed versions remain declaration-free and retain their exact version-specific calculations.</summary>
    /// <param name="version">One existing supported version, without widening v2-only zero-ceiling semantics.</param>
    [Theory]
    [InlineData(1)] [InlineData(2)] [InlineData(3)] [InlineData(4)]
    public void GuardianControl_ExistingVersionsRemainDeclarationFree(int version)
    {
        var source = version switch
        {
            1 => SpiritualStrainPreimageDataTests.CreateData().Source,
            2 => SpiritualExistingStrainPreimageTests.CreateData(3).Source,
            3 => SpiritualExistingPressureTestData.CreateData(),
            4 => SpiritualPressurePairTestData.CreateSource(),
            _ => throw new ArgumentOutOfRangeException(nameof(version))
        };
        var before = source.ToJsonString();
        _ = SpiritualWoundSourceWitness.ValidateAndReadStrainData(source, out var data);
        Assert.Equal(version, Assert.IsType<SpiritualStrainPreimageData>(data).Version);
        Assert.False(SpiritualExistingPressureTestData.ReadActual(source).ContainsKey("spiritualWoundFailedAction"));
        Assert.Equal(before, source.ToJsonString());
    }

    /// <summary>Desired v5 success through current APIs; current ReadVersion(max4) is the first causal missing-feature gate.</summary>
    [Fact]
    public void GuardianAttribution_VersionFiveIntrinsicComparisonSucceeds()
    {
        var fixture = SpiritualGuardianAttributionTestData.Create(); AssertPrerequisites(fixture);
        var source = fixture.Source; var before = source.ToJsonString();
        SpiritualStrainPreimageData? retained = null; SpiritualWoundCalculation? calculated = null;
        var error = Record.Exception(() => calculated = SpiritualWoundSourceWitness.ValidateAndReadStrainData(source, out retained));
        output.WriteLine(JsonSerializer.Serialize(new { phase = "guardian_intrinsic_v5", error = error?.ToString(),
            sourceJson = before, actualJson = fixture.Actual.ToJsonString(), ordinaryJson = fixture.Ordinary.ToJsonString(),
            returnedComparison = retained is not null, calculation = calculated }));
        Assert.Equal(before, source.ToJsonString()); Assert.Null(error);
        Assert.Equal(ActualCalculation(), calculated);
        var data = Assert.IsType<SpiritualStrainPreimageData>(retained);
        Assert.Equal(5, data.Version); Assert.Single(data.Parents); Assert.Null(data.ParentSourceId);
        Assert.Equal("guardian", data.ParentWound.Owner.OwnerKind);
        Assert.Equal(SpiritualGuardianAttributionTestData.GuardianId, data.ParentWound.Owner.OwnerId);
        Assert.Equal(fixture.Retained.Effects.Single()["effectId"]!.GetValue<string>(), data.EffectId);
        Assert.Equal("component_001", data.ComponentId);
        Assert.Equal(SpiritualWoundOpportunityMath.Calculate(new(8, 0, 0, "strained", "fractured", "hostile", 4)), data.OrdinaryCalculation);
        Assert.Same(calculated, data.ActualCalculation);
        Assert.Equal("/activeConflict/exchangeLog/0", data.OriginalExchangePointer);
        Assert.Equal(new CanonicalBeforeImage(true, fixture.RawInput).Fingerprint, data.OriginalInputFingerprint);
        Assert.True(JsonNode.DeepEquals(fixture.Actual, data.ReadActualExchange()));
        Assert.True(JsonNode.DeepEquals(fixture.Ordinary["after"], data.ReadOrdinaryAfter(fixture.Actual)));
        Assert.Equal(data.Version, SpiritualStrainPreimageData.ReadIntrinsic(source, fixture.Actual).Version);
        data.ReadActualExchange()["after"]!["oppositionSideStrain"] = "clear";
        data.ReadOrdinaryAfter(fixture.Actual)["oppositionSideStrain"] = "clear";
        Assert.True(JsonNode.DeepEquals(fixture.Actual, data.ReadActualExchange()));
        Assert.True(JsonNode.DeepEquals(fixture.Ordinary["after"], data.ReadOrdinaryAfter(fixture.Actual)));
        Assert.Equal(before, source.ToJsonString());
    }

    private static void AssertPrerequisites(SpiritualGuardianAttributionFixture fixture)
    {
        Assert.Empty(SpiritualGuardianAttributionDataCheck.Check(fixture.Retained, fixture.Profiles, fixture.Conflict));
        var expected = fixture.Ordinary.DeepClone(); expected["after"]!["oppositionSideStrain"] = "overwhelmed";
        Assert.True(JsonNode.DeepEquals(expected, fixture.Actual));
        var declaration = fixture.Ordinary["spiritualWoundFailedAction"]!;
        Assert.True(JsonNode.DeepEquals(SpiritualGuardianAttributionTestData.FailedAction(fixture.Source["affectedActor"]!["actorId"]!.GetValue<string>()), declaration));
        Assert.True(JsonNode.DeepEquals(declaration, fixture.Actual["spiritualWoundFailedAction"]));
        foreach (var field in new[] { "actorId" })
            Assert.Equal(declaration[field]!.GetValue<string>(), fixture.Ordinary["incomingAction"]![field]!.GetValue<string>());
        Assert.Equal("guardian", fixture.Ordinary["incomingAction"]!["actorType"]!.GetValue<string>());
        Assert.Equal("opposition", fixture.Source["affectedSide"]!.GetValue<string>());
        Assert.Equal(8, fixture.Source["harmfulMargin"]!.GetValue<int>());
        // Full intrinsic actual harm validation is independent of the future comparison feature.
        // This detached proof-free DATA view grants no owner permission and does not resolve an installed live burden.
        var actualOnly = fixture.Source.DeepClone().AsObject(); Assert.True(actualOnly.Remove("strainPreimage"));
        SpiritualExistingPressureTestData.Seal(actualOnly);
        Assert.Equal(ActualCalculation(), SpiritualWoundSourceWitness.ValidateAndReadStrainData(actualOnly, out var noComparison));
        Assert.Null(noComparison);
        Assert.Equal((1, 3, 1, 2), (fixture.Source["priorStrainRank"]!.GetValue<int>(), fixture.Source["destinationStrainRank"]!.GetValue<int>(),
            fixture.Source["extraStrainJumps"]!.GetValue<int>(), fixture.Source["maximumSeverityRank"]!.GetValue<int>()));
    }

    private static SpiritualWoundCalculation ActualCalculation() => SpiritualWoundOpportunityMath.Calculate(new(8, 0, 0, "strained", "overwhelmed", "hostile", 4))!;
    private static string RowJson(SpiritualWoundConflictContribution row) => JsonSerializer.Serialize(row);
}
