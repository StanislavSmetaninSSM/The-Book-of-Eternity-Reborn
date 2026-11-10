using System.Text;
using System.Text.Json;
using System.Text.Json.Nodes;
using BookOfEternityClient.Services;
using Xunit;
using Xunit.Abstractions;

namespace BookOfEternityClient.Tests;

/// <summary>Independent lawful premises precede desired-success v4/v6 actual-zero readers; no live authority is fabricated.</summary>
public sealed partial class SpiritualGroupZeroPressureDataTests(ITestOutputHelper output)
{
    /// <summary>Catches malformed setup independently of the unsupported comparison reader.</summary>
    /// <param name="contour">N2 low, N3 near cap, N4 capped hostile and N4 training.</param>
    [Theory]
    [InlineData(0)] [InlineData(1)] [InlineData(2)] [InlineData(3)]
    public void GroupZero_PrerequisiteIsLawful(int contour)
    {
        var kind = SpiritualGroupZeroPressureData.Contour(contour);
        AssertPrerequisites(SpiritualGroupZeroPressureData.Create(kind), kind);
    }

    /// <summary>Catches full-witness rejection of lawful actual-zero groups and stale out-DATA after refusal.</summary>
    /// <param name="contour">Four independently complete finite fixtures.</param>
    [Theory]
    [InlineData(0)] [InlineData(1)] [InlineData(2)] [InlineData(3)]
    public void GroupZero_FullWitnessAccepts(int contour)
    {
        var kind = SpiritualGroupZeroPressureData.Contour(contour);
        var fixture = SpiritualGroupZeroPressureData.Create(kind);
        AssertPrerequisites(fixture, kind);
        var before = SpiritualGroupZeroPressureData.Serialize(fixture);
        var supported = SpiritualPairLowPressureData.Create(PairLowPressureContour.OnePositive);
        _ = SpiritualWoundSourceWitness.ValidateAndReadStrainData(supported.Source, out var retained);
        Assert.NotNull(retained);
        SpiritualWoundCalculation? calculation = null;
        var error = Record.Exception(() => calculation = SpiritualWoundSourceWitness.ValidateAndReadStrainData(fixture.Source, out retained));
        output.WriteLine(JsonSerializer.Serialize(new { phase = "group_zero_full_witness", contour,
            prerequisitesComplete = true, error = error?.ToString(), calculation, comparisonReturned = retained is not null, fixture = before }));
        Assert.Equal(before, SpiritualGroupZeroPressureData.Serialize(fixture));
        if (error is not null) { Assert.Null(calculation); Assert.Null(retained); }
        Assert.Null(error);
        Assert.Equal(SpiritualGroupZeroPressureData.Expected(kind).Actual, calculation);
        AssertSupported(fixture, kind, Assert.IsType<SpiritualStrainPreimageData>(retained));
    }

    /// <summary>Separately catches direct intrinsic refusal, so a failed full reader cannot hide this result.</summary>
    /// <param name="contour">The same four complete inputs, independently allocated for this reader.</param>
    [Theory]
    [InlineData(0)] [InlineData(1)] [InlineData(2)] [InlineData(3)]
    public void GroupZero_DirectIntrinsicAccepts(int contour)
    {
        var kind = SpiritualGroupZeroPressureData.Contour(contour);
        var fixture = SpiritualGroupZeroPressureData.Create(kind);
        AssertPrerequisites(fixture, kind);
        var before = SpiritualGroupZeroPressureData.Serialize(fixture);
        SpiritualStrainPreimageData? retained = null;
        var error = Record.Exception(() => retained = SpiritualStrainPreimageData.ReadIntrinsic(fixture.Source, fixture.Actual));
        output.WriteLine(JsonSerializer.Serialize(new { phase = "group_zero_direct_intrinsic", contour,
            prerequisitesComplete = true, error = error?.ToString(), comparisonReturned = retained is not null, fixture = before }));
        Assert.Equal(before, SpiritualGroupZeroPressureData.Serialize(fixture));
        if (error is not null) Assert.Null(retained);
        Assert.Null(error);
        AssertSupported(fixture, kind, Assert.IsType<SpiritualStrainPreimageData>(retained));
    }

    /// <summary>Checks complete retained provenance, unchanged siblings, literal full calculations and exact raw/root membership without reading the comparison.</summary>
    /// <param name="fixture">Fresh independent mutable DATA.</param>
    /// <param name="contour">Independent closed expectation.</param>
    /// <param name="parents">Optional independently expected complete inventory, not read from returned DATA.</param>
    /// <param name="ranks">Optional independently expected complete rank sequence.</param>
    internal static void AssertPrerequisites(SpiritualGroupZeroPressureFixture fixture, GroupZeroPressureContour contour,
        int? parents = null, IReadOnlyList<int>? ranks = null)
    {
        var expected = SpiritualGroupZeroPressureData.Expected(contour) with { Parents = parents ?? SpiritualGroupZeroPressureData.Expected(contour).Parents };
        var before = SpiritualGroupZeroPressureData.Serialize(fixture);
        var admissible = SpiritualPressureRetainedDataAdmissibility.Check(fixture.Retained, fixture.Profiles);
        Assert.True(admissible.IsValid, JsonSerializer.Serialize(admissible));
        Assert.Equal(expected.Parents, fixture.Retained.Parents.Count);
        Assert.Equal(expected.Parents, fixture.Retained.Effects.Count);
        Assert.Equal(expected.Parents, fixture.Retained.History["transitions"]!.AsArray().Count);
        foreach (var field in new[] { "instances", "closures", "sources", "decisions" })
            Assert.Equal(expected.Parents, fixture.Retained.Receipts[field]!.AsArray().Count);
        var expectedComponentCount = 0;
        for (var index = 0; index < expected.Parents; index++)
        {
            var rank = ranks?[index] ?? (contour == GroupZeroPressureContour.Low && index == 1 ? 4 : 3);
            expectedComponentCount += rank;
            var parent = fixture.Retained.Parents[index]; var effect = fixture.Retained.Effects[index];
            var parsed = WoundMaterializationContract.Parse(parent.ToJsonString(), "groupZero.prerequisite.parent");
            Assert.True(parsed.IsValid, JsonSerializer.Serialize(parsed.Issues));
            Assert.True(JsonNode.DeepEquals(parent, JsonNode.Parse(WoundMaterializationContract.SerializeCanonical(parsed.Wound!))));
            Assert.Equal(("wound_retained_pressure_owner_" + (index + 1), rank, rank == 4 ? "IV" : "III", 2 * rank),
                (parent["woundId"]!.GetValue<string>(), parent["severity"]!["rank"]!.GetValue<int>(),
                    parent["severity"]!["maximumAtCreation"]!.GetValue<string>(), parent["recovery"]!["currentStepThreshold"]!.GetValue<int>()));
            Assert.True(parent["origin"]!["createdAtTurn"]!.GetValue<int>() < 46);
            Assert.Equal((rank, rank, rank), (parent["consequences"]!["slotBudget"]!.GetValue<int>(),
                parent["consequences"]!["slotsUsed"]!.GetValue<int>(), parent["consequences"]!["entries"]!.AsArray().Count));
            var components = effect["components"]!.AsArray(); Assert.Equal(rank, components.Count);
            Assert.Equal(Enumerable.Range(1, rank).Select(i => "component_00" + i),
                effect["triggers"]![0]!["componentIds"]!.AsArray().Select(row => row!.GetValue<string>()));
            Assert.Equal(("pressure", 1), (components[0]!["payload"]!["operation"]!.GetValue<string>(), components[0]!["payload"]!["magnitude"]!.GetValue<int>()));
            Assert.Equal(("counter", "disadvantage"), (components[1]!["payload"]!["operation"]!.GetValue<string>(), components[1]!["payload"]!["magnitude"]!.GetValue<string>()));
            Assert.Equal(("guard", rank == 4 ? 3 : 2), (components[2]!["payload"]!["operation"]!.GetValue<string>(), components[2]!["payload"]!["magnitude"]!.GetValue<int>()));
            if (rank == 4) Assert.Equal(("counter", 3), (components[3]!["payload"]!["operation"]!.GetValue<string>(), components[3]!["payload"]!["magnitude"]!.GetValue<int>()));
            var birth = SpiritualWoundSourceWitness.ValidateAndReadStrainData(fixture.Retained.Receipts["sources"]![index]!["witness"]!.AsObject(), out var birthComparison);
            Assert.Null(birthComparison);
            Assert.Equal((0, rank, rank == 4 ? 25L : 20L, rank), (birth.PreviousStrainRank, birth.NewStrainRank, birth.TraumaPressure, birth.MaximumSeverityRank));
        }
        Assert.Equal(2, fixture.Profiles[AfterlifeEntityProfileState.ProfilesProperty]!.AsArray().Count);
        foreach (var profile in fixture.Profiles[AfterlifeEntityProfileState.ProfilesProperty]!.AsArray())
            foreach (var art in AfterlifeEntityProfileState.StandardArtIds)
                Assert.Equal(profile!["actorType"]!.GetValue<string>() == "player_soul" && art == "spiritual_resilience" ? expected.Resilience : 0,
                    profile["standardArts"]![art]!.GetValue<int>());
        var carriers = new EffectCarrierCatalogInput(null, null, null, null, fixture.Profiles, fixture.Conflict);
        var snapshot = EffectMechanicsSnapshot.Build(new(carriers, fixture.Retained.EffectIdentities));
        Assert.True(snapshot.IsAccepted, JsonSerializer.Serialize(snapshot.Issues));
        var roots = SpiritualPressureRetainedDataAdmissibility.CreateSourceRoots(fixture.Retained, fixture.Profiles)
            .ToDictionary(row => row.Key, row => row.Value, StringComparer.Ordinal);
        roots[AfterlifeSpiritualConflictState.StatePath] = fixture.Conflict;
        var sources = EffectAcceptedTurnInputComposer.BuildCanonicalSourceAuthority(roots);
        var targets = EffectAcceptedTurnInputComposer.BuildCanonicalTargetAuthority(carriers, roots);
        Assert.Empty(sources.Issues); Assert.Empty(targets.Issues);
        var all = SpiritualWoundConflictContributionProjector.Project(snapshot, sources, targets, fixture.Conflict);
        Assert.True(all.IsAccepted, JsonSerializer.Serialize(all.Issues)); Assert.Equal(expectedComponentCount, all.Contributions.Count);
        var selected = all.Contributions.Where(row => row.Profile == "spiritual_strain_burden" && row.Operation == "pressure").ToArray();
        Assert.Equal(expected.Parents, selected.Length);
        Assert.Equal(expected.Parents, selected.Select(row => row.WoundSource.SourceId).Distinct(StringComparer.Ordinal).Count());
        foreach (var row in selected)
            Assert.Equal(("player", "player_soul", "player", 1, 1), (row.Actor.Kind, row.Actor.TargetId, row.ResolvedSide, row.CurrentStacks, row.Magnitude.GetInt32()));
        var selectedIds = selected.Select(row => (row.EffectId, row.ComponentId)).ToHashSet(); Assert.Equal(expected.Parents, selectedIds.Count);
        var isolated = snapshot with { Components = snapshot.Components.Where(row => !selectedIds.Contains((row.EffectId, row.ComponentId))).ToArray() };
        var siblings = SpiritualWoundConflictContributionProjector.Project(isolated, sources, targets, fixture.Conflict);
        Assert.True(siblings.IsAccepted, JsonSerializer.Serialize(siblings.Issues)); Assert.Equal(expectedComponentCount - expected.Parents, siblings.Contributions.Count);
        Assert.Equal(all.Contributions.Where(row => !selectedIds.Contains((row.EffectId, row.ComponentId))).Select(row => JsonSerializer.Serialize(row)),
            siblings.Contributions.Select(row => JsonSerializer.Serialize(row)));
        Assert.True(JsonNode.DeepEquals(SpiritualGroupZeroPressureData.ReadExchange(contour), fixture.Ordinary));
        Assert.True(JsonNode.DeepEquals(SpiritualGroupZeroPressureData.ReadExchange(contour, actual: true), fixture.Actual));
        var expectedOrdinaryRoot = ReadExpectedRoot(contour, actual: false);
        var expectedActualRoot = ReadExpectedRoot(contour, actual: true);
        Assert.True(JsonNode.DeepEquals(expectedOrdinaryRoot, fixture.Conflict));
        var actualRoot = fixture.Conflict.DeepClone();
        actualRoot["activeConflict"]!["exchangeLog"]![0] = fixture.Actual.DeepClone();
        actualRoot["activeConflict"]!["playerSideStrain"] = expected.Actual.Input.NewStrain;
        Assert.True(JsonNode.DeepEquals(expectedActualRoot, actualRoot));
        var active = fixture.Conflict["activeConflict"]!;
        foreach (var pair in new[] { (fixture.Ordinary, expected.Ordinary), (fixture.Actual, expected.Actual) })
        {
            var source = SpiritualGroupZeroPressureData.CreateStandaloneSource(pair.Item1, pair.Item2, fixture.RawInput, active["conflictId"]!.GetValue<string>());
            var issues = new List<ValidationIssue>();
            ValidationService.ValidateRetainedSpiritualExchangeShape(pair.Item1, "groupZero.prerequisite.exchange", issues);
            ValidationService.ValidateSpiritualWoundSourceActionShape(pair.Item1, "groupZero.prerequisite.exchange", issues);
            ValidationService.ValidateRetainedSpiritualDiceAudit(pair.Item1, source["turnEvidence"]!.AsObject(), issues);
            ValidationService.ValidateRetainedSpiritualExchangeRules(pair.Item1, issues); Assert.Empty(issues);
            Assert.Equal(pair.Item2, SpiritualWoundSourceWitness.ValidateAndReadStrainData(source, out var comparison)); Assert.Null(comparison);
        }
        var actualSource = fixture.Source.DeepClone().AsObject(); Assert.True(actualSource.Remove("strainPreimage")); SpiritualExistingPressureTestData.Seal(actualSource);
        Assert.Equal(expected.Actual, SpiritualWoundSourceWitness.ValidateAndReadStrainData(actualSource, out var noActual)); Assert.Null(noActual);
        Assert.Equal(expected.Ordinary.Input.PreviousStrain, fixture.Ordinary["before"]!["playerSideStrain"]!.GetValue<string>());
        Assert.Equal(expected.Ordinary.Input.NewStrain, active["playerSideStrain"]!.GetValue<string>());
        Assert.Equal(expected.Ordinary.Input.DangerMode, active["dangerMode"]!.GetValue<string>());
        Assert.True(JsonNode.DeepEquals(fixture.Ordinary, Assert.Single(active["exchangeLog"]!.AsArray())));
        var onlyStrainChanged = fixture.Actual.DeepClone(); onlyStrainChanged["after"]!["playerSideStrain"] = expected.Ordinary.Input.NewStrain;
        Assert.True(JsonNode.DeepEquals(fixture.Ordinary, onlyStrainChanged));
        Assert.True(fixture.RawInput.AsSpan().StartsWith(Encoding.UTF8.GetPreamble()));
        var rawRoot = SpiritualWoundStateJson.Parse(SpiritualWoundStateJson.DecodeUtf8JsonText(fixture.RawInput));
        Assert.True(JsonNode.DeepEquals(expectedOrdinaryRoot, rawRoot));
        Assert.True(JsonNode.DeepEquals(fixture.Ordinary, rawRoot["activeConflict"]!["exchangeLog"]![0]));
        var proof = fixture.Source["strainPreimage"]!;
        Assert.Equal(expected.Parents == 2 ? 4 : 6, proof["version"]!.GetValue<int>());
        Assert.Equal(expected.Parents == 2 ? "existing_pressure_ordinary_pair_v1" : "existing_pressure_ordinary_group_v1", proof["inputRoleRevision"]!.GetValue<string>());
        var fingerprint = new CanonicalBeforeImage(true, fixture.RawInput).Fingerprint;
        Assert.Equal(AfterlifeSpiritualConflictState.StatePath, proof["originalInput"]!["path"]!.GetValue<string>());
        Assert.True(proof["originalInput"]!["existed"]!.GetValue<bool>());
        Assert.Equal("/activeConflict/exchangeLog/0", proof["originalInput"]!["exchangePointer"]!.GetValue<string>());
        Assert.Equal(fingerprint, proof["originalInput"]!["contentFingerprint"]!.GetValue<string>());
        Assert.Equal(fingerprint, fixture.Source["turnEvidence"]!["originalSnapshotFingerprint"]!.GetValue<string>());
        Assert.NotEqual(new CanonicalBeforeImage(true, Encoding.UTF8.GetBytes(fixture.Conflict.ToJsonString())).Fingerprint, fingerprint);
        Assert.True(JsonNode.DeepEquals(fixture.Ordinary, SpiritualWoundStateJson.Parse(SpiritualWoundStateJson.DecodeUtf8JsonText(
            Convert.FromBase64String(proof["ordinaryExchangeJsonBase64"]!.GetValue<string>())))));
        Assert.Equal((expected.Ordinary.TraumaPressure, 0), (proof["ordinaryCalculation"]!["traumaPressure"]!.GetValue<long>(), proof["ordinaryCalculation"]!["maximumSeverityRank"]!.GetValue<int>()));
        var parentRows = proof["parents"]!.AsArray(); Assert.Equal(expected.Parents, parentRows.Count);
        for (var index = 0; index < parentRows.Count; index++)
        {
            Assert.True(JsonNode.DeepEquals(fixture.Retained.Parents[index], SpiritualWoundStateJson.Parse(SpiritualWoundStateJson.DecodeUtf8JsonText(
                Convert.FromBase64String(parentRows[index]!["parentWoundJsonBase64"]!.GetValue<string>())))));
            Assert.Equal((fixture.Retained.Effects[index]["effectId"]!.GetValue<string>(), "component_001"),
                (parentRows[index]!["effectId"]!.GetValue<string>(), parentRows[index]!["componentId"]!.GetValue<string>()));
        }
        Assert.Equal(before, SpiritualGroupZeroPressureData.Serialize(fixture));
    }

    /// <summary>Requires every retained parent and complete literal result, and proves returned exchange copies are detached.</summary>
    /// <param name="fixture">Independent expected complete inputs.</param>
    /// <param name="contour">Closed literal calculation and parent-count expectation.</param>
    /// <param name="data">Actual returned reader DATA, never used to author expected inputs.</param>
    /// <param name="parents">Optional independent complete count for geometry controls.</param>
    internal static void AssertSupported(SpiritualGroupZeroPressureFixture fixture, GroupZeroPressureContour contour,
        SpiritualStrainPreimageData data, int? parents = null)
    {
        var before = SpiritualGroupZeroPressureData.Serialize(fixture);
        var expected = SpiritualGroupZeroPressureData.Expected(contour) with { Parents = parents ?? SpiritualGroupZeroPressureData.Expected(contour).Parents };
        Assert.Equal(expected.Parents == 2 ? 4 : 6, data.Version); Assert.Equal(expected.Parents, data.Parents.Count);
        Assert.Equal(expected.Ordinary, data.OrdinaryCalculation); Assert.Equal(expected.Actual, data.ActualCalculation);
        Assert.Equal(new CanonicalBeforeImage(true, fixture.RawInput).Fingerprint, data.OriginalInputFingerprint);
        Assert.Equal("/activeConflict/exchangeLog/0", data.OriginalExchangePointer);
        for (var index = 0; index < expected.Parents; index++)
        {
            Assert.Null(data.Parents[index].ParentSourceId);
            Assert.True(JsonNode.DeepEquals(fixture.Retained.Parents[index], JsonNode.Parse(WoundMaterializationContract.SerializeCanonical(data.Parents[index].ParentWound))));
            Assert.Equal((fixture.Retained.Effects[index]["effectId"]!.GetValue<string>(), "component_001"), (data.Parents[index].EffectId, data.Parents[index].ComponentId));
        }
        Assert.True(JsonNode.DeepEquals(fixture.Actual, data.ReadActualExchange()));
        Assert.True(JsonNode.DeepEquals(fixture.Ordinary["after"], data.ReadOrdinaryAfter(fixture.Actual)));
        var detachedActual = data.ReadActualExchange(); var detachedOrdinary = data.ReadOrdinaryAfter(fixture.Actual);
        Assert.NotEqual("clear", detachedActual["after"]!["playerSideStrain"]!.GetValue<string>());
        Assert.NotEqual("clear", detachedOrdinary["playerSideStrain"]!.GetValue<string>());
        detachedActual["after"]!["playerSideStrain"] = "clear"; detachedOrdinary["playerSideStrain"] = "clear";
        Assert.True(JsonNode.DeepEquals(fixture.Actual, data.ReadActualExchange()));
        Assert.True(JsonNode.DeepEquals(fixture.Ordinary["after"], data.ReadOrdinaryAfter(fixture.Actual)));
        Assert.Equal(before, SpiritualGroupZeroPressureData.Serialize(fixture));
    }

    /// <summary>Independently enumerates the entire expected ordinary/actual companion, including unchanged root siblings.</summary>
    /// <param name="contour">Closed independent identity, danger and arithmetic inputs.</param>
    /// <param name="actual">Chooses ordinary or actual full-root destination and exchange.</param>
    /// <returns>A literal full-root oracle never used by the fixture factory.</returns>
    private static JsonObject ReadExpectedRoot(GroupZeroPressureContour contour, bool actual)
    {
        var expected = SpiritualGroupZeroPressureData.Expected(contour);
        var calculation = actual ? expected.Actual : expected.Ordinary;
        var conflictId = contour switch
        {
            GroupZeroPressureContour.Low => "pair_low_data",
            GroupZeroPressureContour.Training => "capped_group_data",
            _ => "group_zero_data"
        };
        var exchange = SpiritualGroupZeroPressureData.ReadExchange(contour, actual);
        return JsonNode.Parse($$$"""
        {"schemaVersion":1,"activeConflict":{
          "conflictId":"{{{conflictId}}}","realm":"Chaos Sea","sideModel":"direct_duel",
          "dangerMode":"{{{calculation.Input.DangerMode}}}","resolutionState":"active",
          "playerSideStrain":"{{{calculation.Input.NewStrain}}}","oppositionSideStrain":"clear",
          "conflictPosition":"contested","controlState":null,"tempoAdvantage":null,
          "exchangeLog":[{{{exchange.ToJsonString()}}}],"combatConditions":[],
          "playerSide":{"leadContestant":{"actorType":"player","actorId":"player_soul"},"supporters":[]},
          "oppositionSide":{"leadContestant":{"actorType":"guardian","actorId":"guardian_frame"},"supporters":[]}},
          "recentConflicts":[]}
        """)!.AsObject();
    }
}
