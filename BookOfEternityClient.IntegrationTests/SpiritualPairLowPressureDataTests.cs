using System.Text;
using System.Text.Json;
using System.Text.Json.Nodes;
using BookOfEternityClient.Services;
using Xunit;
using Xunit.Abstractions;

namespace BookOfEternityClient.Tests;

/// <summary>Cheap full exact-two low-pressure DATA prerequisites and mixed causal v4 gates, without files or authority.</summary>
public sealed partial class SpiritualPairLowPressureDataTests(ITestOutputHelper output)
{
    /// <summary>Sixteen independent A–D × ordered III/IV complete-pair prerequisites, never invoking unsupported v4.</summary>
    /// <param name="contour">A=0, B=1, C=2, D=3.</param>
    /// <param name="first">Independent rank at the first canonical parent coordinate.</param>
    /// <param name="second">Independent rank at the second canonical parent coordinate.</param>
    [Theory]
    [InlineData(0, 3, 3)]
    [InlineData(0, 3, 4)]
    [InlineData(0, 4, 3)]
    [InlineData(0, 4, 4)]
    [InlineData(1, 3, 3)]
    [InlineData(1, 3, 4)]
    [InlineData(1, 4, 3)]
    [InlineData(1, 4, 4)]
    [InlineData(2, 3, 3)]
    [InlineData(2, 3, 4)]
    [InlineData(2, 4, 3)]
    [InlineData(2, 4, 4)]
    [InlineData(3, 3, 3)]
    [InlineData(3, 3, 4)]
    [InlineData(3, 4, 3)]
    [InlineData(3, 4, 4)]
    public void PairLow_CompleteParentsAndArithmeticPrerequisites(int contour, int first, int second)
    {
        var kind = SpiritualPairLowPressureData.Boundary(contour);
        AssertPrerequisites(SpiritualPairLowPressureData.Create(kind, first, second), kind, first, second);
    }

    /// <summary>Preserves the old die6/res0 ordinaryI/max0 premise and distinguishes a lawful true actual-max0 refusal.</summary>
    /// <param name="actualZero">False maps the exact legacy owner premise; true proves ordinary−4/max0 and hypothetical actual6/max0.</param>
    [Theory]
    [InlineData(false)] [InlineData(true)]
    public void PairLow_LegacyAndActualZeroPrerequisites(bool actualZero)
    {
        var kind = actualZero ? PairLowPressureContour.ActualZero : PairLowPressureContour.LegacyOneZero;
        AssertPrerequisites(SpiritualPairLowPressureData.Create(kind, 3, 4), kind, 3, 4);
    }

    /// <summary>Unknown closed contours reject before setup, and every mutable graph/profile/ledger/input/source copy is independent.</summary>
    [Fact]
    public void PairLow_FixtureCopiesAreIndependentAndUnknownContoursRefuse()
    {
        Assert.Throws<ArgumentOutOfRangeException>(() => SpiritualPairLowPressureData.Expected((PairLowPressureContour)99));
        Assert.Throws<ArgumentOutOfRangeException>(() => SpiritualPairLowPressureData.Create((PairLowPressureContour)99));
        Assert.Throws<ArgumentOutOfRangeException>(() => SpiritualPairLowPressureData.Boundary(4));
        Assert.Throws<ArgumentOutOfRangeException>(() => SpiritualPairLowPressureData.Create(PairLowPressureContour.OneZero, 2, 4));
        Assert.Throws<ArgumentOutOfRangeException>(() => SpiritualPairLowPressureData.ReadExchange("strained", 5));
        var first = SpiritualPairLowPressureData.Create(PairLowPressureContour.OneZero, 3, 4);
        var second = SpiritualPairLowPressureData.Create(PairLowPressureContour.OneZero, 3, 4);
        var before = Serialize(second);
        first.Retained.Parents[0]["display"]!["description"] = "Изменена только первая независимая копия.";
        first.Retained.Effects[1]["components"]!.AsArray().Clear();
        first.Retained.WoundIdentities.Clear(); first.Retained.EffectIdentities.Clear();
        first.Retained.History.Clear(); first.Retained.Receipts.Clear(); first.Profiles.Clear();
        first.Conflict.Clear(); first.Ordinary.Clear(); first.Actual.Clear(); first.Source.Clear(); first.RawInput[0] = 0;
        Assert.Equal(before, Serialize(second));
        AssertPrerequisites(second, PairLowPressureContour.OneZero, 3, 4);
    }

    /// <summary>Desired success is unconditional: A/B naturally fail the ordinaryII/III v4 guard; C/D already support ordinarymax0.</summary>
    /// <param name="contour">A=0, B=1, C=2, D=3.</param>
    /// <param name="first">Independent first parentIII/IV.</param>
    /// <param name="second">Independent second parentIII/IV.</param>
    [Theory]
    [InlineData(0, 3, 3)]
    [InlineData(0, 3, 4)]
    [InlineData(0, 4, 3)]
    [InlineData(0, 4, 4)]
    [InlineData(1, 3, 3)]
    [InlineData(1, 3, 4)]
    [InlineData(1, 4, 3)]
    [InlineData(1, 4, 4)]
    [InlineData(2, 3, 3)]
    [InlineData(2, 3, 4)]
    [InlineData(2, 4, 3)]
    [InlineData(2, 4, 4)]
    [InlineData(3, 3, 3)]
    [InlineData(3, 3, 4)]
    [InlineData(3, 4, 3)]
    [InlineData(3, 4, 4)]
    public void PairLow_VersionFourWitnessAndIntrinsicSucceed(int contour, int first, int second)
    {
        var kind = SpiritualPairLowPressureData.Boundary(contour);
        var fixture = SpiritualPairLowPressureData.Create(kind, first, second);
        AssertPrerequisites(fixture, kind, first, second);
        var before = Serialize(fixture);
        SpiritualStrainPreimageData? retained = null; SpiritualWoundCalculation? actual = null;
        var error = Record.Exception(() => actual = SpiritualWoundSourceWitness.ValidateAndReadStrainData(fixture.Source, out retained));
        SpiritualStrainPreimageData? direct = null;
        var directError = Record.Exception(() => direct = SpiritualStrainPreimageData.ReadIntrinsic(fixture.Source, fixture.Actual));
        output.WriteLine(JsonSerializer.Serialize(new { phase = "pair_low_intrinsic_v4", contour, first, second,
            error = error?.ToString(), directError = directError?.ToString(), actual, comparisonReturned = retained is not null,
            directReturned = direct is not null, sourceJson = fixture.Source.ToJsonString(), rawInputBase64 = Convert.ToBase64String(fixture.RawInput) }));
        Assert.Equal(before, Serialize(fixture));
        if (error is not null) { Assert.Null(retained); Assert.Null(actual); }
        if (directError is not null) Assert.Null(direct);
        Assert.Null(error); Assert.Null(directError);
        AssertSupported(fixture, kind, first, second, Assert.IsType<SpiritualStrainPreimageData>(retained));
        AssertSupported(fixture, kind, first, second, Assert.IsType<SpiritualStrainPreimageData>(direct));
        Assert.Equal(SpiritualPairLowPressureData.Expected(kind).Actual, actual);
        retained!.ReadActualExchange()["after"]!["playerSideStrain"] = "clear";
        retained.ReadOrdinaryAfter(fixture.Actual)["playerSideStrain"] = "clear";
        Assert.True(JsonNode.DeepEquals(fixture.Actual, retained.ReadActualExchange()));
        Assert.True(JsonNode.DeepEquals(fixture.Ordinary["after"], retained.ReadOrdinaryAfter(fixture.Actual)));
        Assert.Equal(before, Serialize(fixture));
    }

    /// <summary>Future GREEN only: low-rank under/overincrement, false ordinarymax0 and genuinely nonpositive actual remain refused with no out-DATA.</summary>
    /// <param name="change">One independently named contradiction, starting after a supported lawful A baseline.</param>
    [Theory]
    [InlineData("actual-I")] [InlineData("actual-II")] [InlineData("actual-IV")]
    [InlineData("ordinary-maximum")] [InlineData("actual-maximum-zero")]
    public void PairLow_IntrinsicRejectsContradictoryRankAndMaximum(string change)
    {
        var fixture = SpiritualPairLowPressureData.Create(PairLowPressureContour.OnePositive);
        var accepted = SpiritualWoundSourceWitness.ValidateAndReadStrainData(fixture.Source, out var baseline);
        Assert.Equal(SpiritualPairLowPressureData.Expected(PairLowPressureContour.OnePositive).Actual, accepted);
        AssertSupported(fixture, PairLowPressureContour.OnePositive, 3, 3, baseline!);
        if (change == "actual-maximum-zero")
        {
            fixture = SpiritualPairLowPressureData.Create(PairLowPressureContour.ActualZero);
            AssertPrerequisites(fixture, PairLowPressureContour.ActualZero, 3, 3);
        }
        else if (change == "ordinary-maximum") fixture.Source["strainPreimage"]!["ordinaryCalculation"]!["maximumSeverityRank"] = 0;
        else
        {
            var calculation = change switch
            {
                "actual-I" => new SpiritualWoundCalculation(new(8, 0, 0, "clear", "strained", "hostile", 4), 0, 1, 0, 8, 1, 1, 4, true, 1),
                "actual-II" => new SpiritualWoundCalculation(new(8, 0, 0, "clear", "fractured", "hostile", 4), 0, 2, 1, 13, 2, 2, 4, true, 2),
                "actual-IV" => new SpiritualWoundCalculation(new(8, 0, 0, "clear", "broken", "hostile", 4), 0, 4, 3, 23, 4, 4, 4, true, 4),
                _ => throw new ArgumentOutOfRangeException(nameof(change))
            };
            var actual = SpiritualPairLowPressureData.ReadExchange(calculation.Input.NewStrain!);
            var source = SpiritualPairLowPressureData.CreateSource(actual, calculation, fixture.RawInput);
            source["strainPreimage"] = fixture.Source["strainPreimage"]!.DeepClone();
            fixture = fixture with { Actual = actual, Source = source };
            var standalone = source.DeepClone().AsObject(); standalone.Remove("strainPreimage"); SpiritualExistingPressureTestData.Seal(standalone);
            Assert.Equal(calculation, SpiritualWoundSourceWitness.ValidateAndReadStrainData(standalone, out var noComparison)); Assert.Null(noComparison);
        }
        SpiritualExistingPressureTestData.Seal(fixture.Source);
        var before = Serialize(fixture); SpiritualStrainPreimageData? retained = null; SpiritualWoundCalculation? result = null;
        var error = Record.Exception(() => result = SpiritualWoundSourceWitness.ValidateAndReadStrainData(fixture.Source, out retained));
        Assert.NotNull(error); Assert.Null(retained); Assert.Null(result); Assert.Equal(before, Serialize(fixture));
    }

    /// <summary>Checks every noncomparison prerequisite through complete typed graph/identity/History/receipt, projection, shape/dice/rules and real arithmetic.</summary>
    /// <param name="fixture">Independent complete mutable fixture.</param>
    /// <param name="contour">Closed independently expected calculation/dice/profile tuple.</param>
    /// <param name="first">Expected rank at canonical first parent.</param>
    /// <param name="second">Expected rank at canonical second parent.</param>
    internal static void AssertPrerequisites(PairLowPressureFixture fixture, PairLowPressureContour contour, int first, int second)
    {
        var expected = SpiritualPairLowPressureData.Expected(contour);
        var before = Serialize(fixture);
        var checkedData = SpiritualPressureRetainedDataAdmissibility.Check(fixture.Retained, fixture.Profiles);
        Assert.True(checkedData.IsValid, JsonSerializer.Serialize(checkedData));
        Assert.Equal(2, fixture.Retained.Parents.Count); Assert.Equal(2, fixture.Retained.Effects.Count);
        for (var index = 0; index < 2; index++)
        {
            var rank = index == 0 ? first : second;
            var parent = fixture.Retained.Parents[index]; var effect = fixture.Retained.Effects[index];
            var parsed = WoundMaterializationContract.Parse(parent.ToJsonString(), "pairLow.parent");
            Assert.True(parsed.IsValid, JsonSerializer.Serialize(parsed.Issues));
            Assert.True(JsonNode.DeepEquals(parent, JsonNode.Parse(WoundMaterializationContract.SerializeCanonical(parsed.Wound!))));
            Assert.Equal(("wound_retained_pressure_owner_" + (index + 1), rank, rank == 4 ? "IV" : "III", 2 * rank),
                (parent["woundId"]!.GetValue<string>(), parent["severity"]!["rank"]!.GetValue<int>(),
                    parent["severity"]!["maximumAtCreation"]!.GetValue<string>(), parent["recovery"]!["currentStepThreshold"]!.GetValue<int>()));
            Assert.Equal(rank, parent["consequences"]!["slotBudget"]!.GetValue<int>()); Assert.Equal(rank, parent["consequences"]!["slotsUsed"]!.GetValue<int>());
            Assert.Equal(rank, parent["consequences"]!["entries"]!.AsArray().Count);
            var components = effect["components"]!.AsArray(); Assert.Equal(rank, components.Count);
            Assert.Equal(Enumerable.Range(1, rank).Select(i => "component_00" + i), effect["triggers"]![0]!["componentIds"]!.AsArray().Select(x => x!.GetValue<string>()));
            Assert.Equal(1, components[0]!["payload"]!["magnitude"]!.GetValue<int>());
            Assert.Equal(("counter", "disadvantage"), (components[1]!["payload"]!["operation"]!.GetValue<string>(), components[1]!["payload"]!["magnitude"]!.GetValue<string>()));
            Assert.Equal(("guard", rank == 4 ? 3 : 2), (components[2]!["payload"]!["operation"]!.GetValue<string>(), components[2]!["payload"]!["magnitude"]!.GetValue<int>()));
            if (rank == 4) Assert.Equal(("counter", 3), (components[3]!["payload"]!["operation"]!.GetValue<string>(), components[3]!["payload"]!["magnitude"]!.GetValue<int>()));
            Assert.Equal(("player", "player_soul"), (effect["target"]!["kind"]!.GetValue<string>(), effect["target"]!["targetId"]!.GetValue<string>()));
            var birth = SpiritualWoundSourceWitness.ValidateAndReadStrainData(fixture.Retained.Receipts["sources"]![index]!["witness"]!.AsObject(), out var noBirth);
            Assert.Null(noBirth); Assert.Equal((0, rank, rank == 4 ? 25L : 20L, rank), (birth.PreviousStrainRank, birth.NewStrainRank, birth.TraumaPressure, birth.MaximumSeverityRank));
        }
        var player = Assert.Single(fixture.Profiles[AfterlifeEntityProfileState.ProfilesProperty]!.AsArray(), p => p!["actorType"]!.GetValue<string>() == "player_soul")!;
        Assert.Equal(expected.Resilience, player["standardArts"]!["spiritual_resilience"]!.GetValue<int>());
        Assert.Equal(0, player["standardArts"]!["pressure"]!.GetValue<int>());
        var carriers = new EffectCarrierCatalogInput(null, null, null, null, fixture.Profiles, fixture.Conflict);
        var snapshot = EffectMechanicsSnapshot.Build(new(carriers, fixture.Retained.EffectIdentities));
        Assert.True(snapshot.IsAccepted, JsonSerializer.Serialize(snapshot.Issues));
        var roots = SpiritualPressureRetainedDataAdmissibility.CreateSourceRoots(fixture.Retained, fixture.Profiles).ToDictionary(row => row.Key, row => row.Value, StringComparer.Ordinal);
        roots[AfterlifeSpiritualConflictState.StatePath] = fixture.Conflict;
        var sources = EffectAcceptedTurnInputComposer.BuildCanonicalSourceAuthority(roots);
        var targets = EffectAcceptedTurnInputComposer.BuildCanonicalTargetAuthority(carriers, roots);
        var all = SpiritualWoundConflictContributionProjector.Project(snapshot, sources, targets, fixture.Conflict);
        Assert.True(all.IsAccepted, JsonSerializer.Serialize(all.Issues)); Assert.Equal(first + second, all.Contributions.Count);
        var selected = all.Contributions.Where(row => row.Profile == "spiritual_strain_burden" && row.Operation == "pressure").ToArray();
        Assert.Equal(2, selected.Length); Assert.Equal(2, selected.Select(row => row.WoundSource.SourceId).Distinct(StringComparer.Ordinal).Count());
        Assert.Equal(2, selected.Select(row => (row.EffectId, row.ComponentId)).Distinct().Count());
        foreach (var row in selected) Assert.Equal(("player", "player_soul", "player", 1, 1), (row.Actor.Kind, row.Actor.TargetId, row.ResolvedSide, row.CurrentStacks, row.Magnitude.GetInt32()));
        var selectedIds = selected.Select(row => (row.EffectId, row.ComponentId)).ToHashSet();
        var isolated = snapshot with { Components = snapshot.Components.Where(row => !selectedIds.Contains((row.EffectId, row.ComponentId))).ToArray() };
        var siblings = SpiritualWoundConflictContributionProjector.Project(isolated, sources, targets, fixture.Conflict);
        Assert.True(siblings.IsAccepted, JsonSerializer.Serialize(siblings.Issues)); Assert.Equal(first + second - 2, siblings.Contributions.Count);
        Assert.Equal(all.Contributions.Where(row => !selectedIds.Contains((row.EffectId, row.ComponentId))).Select(row => JsonSerializer.Serialize(row)), siblings.Contributions.Select(row => JsonSerializer.Serialize(row)));
        Assert.True(JsonNode.DeepEquals(SpiritualPairLowPressureData.ReadExchange(expected.Ordinary.Input.NewStrain!, expected.PlayerDie), fixture.Ordinary));
        Assert.True(JsonNode.DeepEquals(SpiritualPairLowPressureData.ReadExchange(expected.Actual.Input.NewStrain!, expected.PlayerDie), fixture.Actual));
        foreach (var pair in new[] { (fixture.Ordinary, expected.Ordinary), (fixture.Actual, expected.Actual) })
        {
            var source = SpiritualPairLowPressureData.CreateSource(pair.Item1, pair.Item2, fixture.RawInput);
            var issues = new List<ValidationIssue>();
            ValidationService.ValidateRetainedSpiritualExchangeShape(pair.Item1, "pairLow.exchange", issues);
            ValidationService.ValidateSpiritualWoundSourceActionShape(pair.Item1, "pairLow.exchange", issues);
            ValidationService.ValidateRetainedSpiritualDiceAudit(pair.Item1, source["turnEvidence"]!.AsObject(), issues);
            ValidationService.ValidateRetainedSpiritualExchangeRules(pair.Item1, issues);
            Assert.Empty(issues);
            Assert.Equal(pair.Item2, SpiritualWoundSourceWitness.ValidateAndReadStrainData(source, out var noComparison)); Assert.Null(noComparison);
        }
        var standalone = fixture.Source.DeepClone().AsObject(); Assert.True(standalone.Remove("strainPreimage")); SpiritualExistingPressureTestData.Seal(standalone);
        Assert.Equal(expected.Actual, SpiritualWoundSourceWitness.ValidateAndReadStrainData(standalone, out var noActual)); Assert.Null(noActual);
        var actualFromOrdinary = fixture.Ordinary.DeepClone(); actualFromOrdinary["after"]!["playerSideStrain"] = expected.Actual.Input.NewStrain;
        Assert.True(JsonNode.DeepEquals(actualFromOrdinary, fixture.Actual));
        Assert.Equal(expected.Ordinary.Input.NewStrain, fixture.Conflict["activeConflict"]!["playerSideStrain"]!.GetValue<string>());
        Assert.True(JsonNode.DeepEquals(fixture.Ordinary, fixture.Conflict["activeConflict"]!["exchangeLog"]![0]));
        Assert.True(fixture.RawInput.AsSpan().StartsWith(Encoding.UTF8.GetPreamble()));
        Assert.True(JsonNode.DeepEquals(fixture.Conflict, SpiritualWoundStateJson.Parse(SpiritualWoundStateJson.DecodeUtf8JsonText(fixture.RawInput))));
        Assert.Equal(new CanonicalBeforeImage(true, fixture.RawInput).Fingerprint, fixture.Source["strainPreimage"]!["originalInput"]!["contentFingerprint"]!.GetValue<string>());
        Assert.NotEqual(new CanonicalBeforeImage(true, Encoding.UTF8.GetBytes(fixture.Conflict.ToJsonString())).Fingerprint, new CanonicalBeforeImage(true, fixture.RawInput).Fingerprint);
        Assert.Equal(before, Serialize(fixture));
    }

    /// <summary>Checks complete returned immutable v4 DATA against independent parents, full arithmetic and raw input.</summary>
    /// <param name="fixture">Independent expected complete source and pair.</param>
    /// <param name="contour">Closed literal arithmetic/dice contour.</param>
    /// <param name="first">First canonical expected rank.</param>
    /// <param name="second">Second canonical expected rank.</param>
    /// <param name="data">Actual typed reader result, never used to author expectations.</param>
    internal static void AssertSupported(PairLowPressureFixture fixture, PairLowPressureContour contour, int first, int second, SpiritualStrainPreimageData data)
    {
        var expected = SpiritualPairLowPressureData.Expected(contour);
        Assert.Equal(4, data.Version); Assert.Equal(2, data.Parents.Count);
        Assert.All(data.Parents, parent => Assert.Null(parent.ParentSourceId));
        Assert.Equal(expected.Ordinary, data.OrdinaryCalculation); Assert.Equal(expected.Actual, data.ActualCalculation);
        Assert.Equal(new CanonicalBeforeImage(true, fixture.RawInput).Fingerprint, data.OriginalInputFingerprint);
        Assert.Equal("/activeConflict/exchangeLog/0", data.OriginalExchangePointer);
        Assert.True(JsonNode.DeepEquals(fixture.Actual, data.ReadActualExchange()));
        Assert.True(JsonNode.DeepEquals(fixture.Ordinary["after"], data.ReadOrdinaryAfter(fixture.Actual)));
        for (var index = 0; index < 2; index++)
        {
            var parent = data.Parents[index];
            Assert.Equal(index == 0 ? first : second, parent.ParentWound.Severity.Rank);
            Assert.True(JsonNode.DeepEquals(fixture.Retained.Parents[index], JsonNode.Parse(WoundMaterializationContract.SerializeCanonical(parent.ParentWound))));
            Assert.Equal(fixture.Retained.Effects[index]["effectId"]!.GetValue<string>(), parent.EffectId); Assert.Equal("component_001", parent.ComponentId);
        }
    }

    private static string Serialize(PairLowPressureFixture fixture) => JsonSerializer.Serialize(new
    {
        parents = fixture.Retained.Parents.Select(p => p.ToJsonString()), effects = fixture.Retained.Effects.Select(p => p.ToJsonString()),
        wounds = fixture.Retained.WoundIdentities.ToJsonString(), history = fixture.Retained.History.ToJsonString(),
        effectIdentities = fixture.Retained.EffectIdentities.ToJsonString(), receipts = fixture.Retained.Receipts.ToJsonString(),
        profiles = fixture.Profiles.ToJsonString(), conflict = fixture.Conflict.ToJsonString(), ordinary = fixture.Ordinary.ToJsonString(),
        actual = fixture.Actual.ToJsonString(), source = fixture.Source.ToJsonString(), raw = Convert.ToBase64String(fixture.RawInput)
    });
}
