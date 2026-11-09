using System.Text;
using System.Text.Json;
using System.Text.Json.Nodes;
using BookOfEternityClient.Services;
using Xunit;
using Xunit.Abstractions;

namespace BookOfEternityClient.Tests;

/// <summary>Cheap lawful capped N2/N3/N4 prerequisites and desired v4/v6 success, without files or live authority.</summary>
public sealed partial class SpiritualCappedGroupPressureDataTests(ITestOutputHelper output)
{
    /// <summary>Requires full retained graphs/History/receipts, exact full raw input and standalone literal14/II before comparison.</summary>
    /// <param name="parents">Closed complete N2/N3/N4 parent inventory.</param>
    [Theory]
    [InlineData(2)] [InlineData(3)] [InlineData(4)]
    public void CappedGroupPressure_CompletePrerequisites(int parents) =>
        AssertPrerequisites(SpiritualCappedGroupPressureData.Create(parents), parents);

    /// <summary>Requires both full witness and direct intrinsic success; equal exchanges must retain every comparison parent.</summary>
    /// <param name="parents">N2 uses v4/pair-v1; N3/N4 use v6/group-v1 despite already saturated scalar strain.</param>
    [Theory]
    [InlineData(2)] [InlineData(3)] [InlineData(4)]
    public void CappedGroupPressure_WitnessAndIntrinsicSucceed(int parents)
    {
        var fixture = SpiritualCappedGroupPressureData.Create(parents);
        AssertPrerequisites(fixture, parents);
        var before = SpiritualCappedGroupPressureData.Serialize(fixture);
        // A previously accepted supported comparison must not survive a later failed out-parameter read.
        var supported = SpiritualPairLowPressureData.Create(PairLowPressureContour.OnePositive);
        _ = SpiritualWoundSourceWitness.ValidateAndReadStrainData(supported.Source, out var retained);
        Assert.NotNull(retained);
        SpiritualWoundCalculation? calculation = null;
        var error = Record.Exception(() => calculation = SpiritualWoundSourceWitness.ValidateAndReadStrainData(fixture.Source, out retained));
        SpiritualStrainPreimageData? direct = null;
        var directError = Record.Exception(() => direct = SpiritualStrainPreimageData.ReadIntrinsic(fixture.Source, fixture.Actual));
        output.WriteLine(JsonSerializer.Serialize(new { phase = "capped_group_intrinsic", parents,
            prerequisitesComplete = true, expected = SpiritualCappedGroupPressureData.Calculation,
            error = error?.ToString(), directError = directError?.ToString(), calculation,
            comparisonReturned = retained is not null, directReturned = direct is not null, fixture = before }));
        Assert.Equal(before, SpiritualCappedGroupPressureData.Serialize(fixture));
        if (error is not null) { Assert.Null(calculation); Assert.Null(retained); }
        if (directError is not null) Assert.Null(direct);
        // Unsupported ordinaryIV is the causal RED, never a passing negative expectation.
        Assert.Null(error); Assert.Null(directError);
        Assert.Equal(SpiritualCappedGroupPressureData.Calculation, calculation);
        AssertSupported(fixture, parents, Assert.IsType<SpiritualStrainPreimageData>(retained));
        AssertSupported(fixture, parents, Assert.IsType<SpiritualStrainPreimageData>(direct));
        retained!.ReadActualExchange()["after"]!["playerSideStrain"] = "clear";
        retained.ReadOrdinaryAfter(fixture.Actual)["playerSideStrain"] = "clear";
        Assert.True(JsonNode.DeepEquals(fixture.Actual, retained.ReadActualExchange()));
        Assert.True(JsonNode.DeepEquals(fixture.Ordinary["after"], retained.ReadOrdinaryAfter(fixture.Actual)));
        Assert.Equal(before, SpiritualCappedGroupPressureData.Serialize(fixture));
    }

    /// <summary>Checks one already-authored complete fixture through typed retained validators and comparison-free source readers.</summary>
    /// <param name="fixture">Fresh fixture; no unsupported comparison is used to establish its premises.</param>
    /// <param name="parents">Independent complete list size, not permission to select a subset.</param>
    /// <param name="previous">Independent before0..3 expectation; the accepted Task1 default stays beforeIII.</param>
    /// <param name="ranks">Optional independent complete rank sequence; omission expects all III.</param>
    /// <param name="training">Expected full-root training declaration, allowed only as standalone DATA and rejected by group comparison.</param>
    internal static void AssertPrerequisites(SpiritualCappedGroupPressureFixture fixture, int parents,
        int previous = 3, IReadOnlyList<int>? ranks = null, bool training = false)
    {
        var before = SpiritualCappedGroupPressureData.Serialize(fixture);
        var expected = SpiritualCappedGroupPressureData.Expected(previous, training);
        var admissible = SpiritualPressureRetainedDataAdmissibility.Check(fixture.Retained, fixture.Profiles);
        Assert.True(admissible.IsValid, JsonSerializer.Serialize(admissible));
        Assert.Equal(parents, fixture.Retained.Parents.Count); Assert.Equal(parents, fixture.Retained.Effects.Count);
        foreach (var field in new[] { "instances", "closures", "sources", "decisions" })
            Assert.Equal(parents, fixture.Retained.Receipts[field]!.AsArray().Count);
        Assert.Equal(parents, fixture.Retained.History["transitions"]!.AsArray().Count);
        for (var index = 0; index < parents; index++)
        {
            var rank = ranks?[index] ?? 3;
            var parent = fixture.Retained.Parents[index]; var effect = fixture.Retained.Effects[index];
            var parsed = WoundMaterializationContract.Parse(parent.ToJsonString(), "cappedGroup.prerequisite.parent");
            Assert.True(parsed.IsValid, JsonSerializer.Serialize(parsed.Issues));
            Assert.True(JsonNode.DeepEquals(parent, JsonNode.Parse(WoundMaterializationContract.SerializeCanonical(parsed.Wound!))));
            Assert.Equal(("wound_retained_pressure_owner_" + (index + 1), rank, rank == 4 ? "IV" : "III", 2 * rank),
                (parent["woundId"]!.GetValue<string>(), parent["severity"]!["rank"]!.GetValue<int>(),
                    parent["severity"]!["maximumAtCreation"]!.GetValue<string>(), parent["recovery"]!["currentStepThreshold"]!.GetValue<int>()));
            Assert.True(parent["origin"]!["createdAtTurn"]!.GetValue<int>() < 46);
            Assert.Equal((rank, rank, rank), (parent["consequences"]!["slotBudget"]!.GetValue<int>(),
                parent["consequences"]!["slotsUsed"]!.GetValue<int>(), parent["consequences"]!["entries"]!.AsArray().Count));
            var components = effect["components"]!.AsArray(); Assert.Equal(rank, components.Count);
            Assert.Equal(Enumerable.Range(1, rank).Select(ordinal => "component_00" + ordinal),
                effect["triggers"]![0]!["componentIds"]!.AsArray().Select(row => row!.GetValue<string>()));
            Assert.Equal(("pressure", 1), (components[0]!["payload"]!["operation"]!.GetValue<string>(), components[0]!["payload"]!["magnitude"]!.GetValue<int>()));
            Assert.Equal(("counter", "disadvantage"), (components[1]!["payload"]!["operation"]!.GetValue<string>(), components[1]!["payload"]!["magnitude"]!.GetValue<string>()));
            Assert.Equal(("guard", rank == 4 ? 3 : 2), (components[2]!["payload"]!["operation"]!.GetValue<string>(), components[2]!["payload"]!["magnitude"]!.GetValue<int>()));
            if (rank == 4)
                Assert.Equal(("counter", 3), (components[3]!["payload"]!["operation"]!.GetValue<string>(), components[3]!["payload"]!["magnitude"]!.GetValue<int>()));
            var birth = SpiritualWoundSourceWitness.ValidateAndReadStrainData(fixture.Retained.Receipts["sources"]![index]!["witness"]!.AsObject(), out var birthComparison);
            Assert.Null(birthComparison); Assert.Equal((0, rank, rank == 4 ? 25L : 20L, rank), (birth.PreviousStrainRank, birth.NewStrainRank, birth.TraumaPressure, birth.MaximumSeverityRank));
        }
        var carriers = new EffectCarrierCatalogInput(null, null, null, null, fixture.Profiles, fixture.Conflict);
        var snapshot = EffectMechanicsSnapshot.Build(new(carriers, fixture.Retained.EffectIdentities));
        Assert.True(snapshot.IsAccepted, JsonSerializer.Serialize(snapshot.Issues));
        var roots = SpiritualPressureRetainedDataAdmissibility.CreateSourceRoots(fixture.Retained, fixture.Profiles)
            .ToDictionary(row => row.Key, row => row.Value, StringComparer.Ordinal);
        roots[AfterlifeSpiritualConflictState.StatePath] = fixture.Conflict;
        var sources = EffectAcceptedTurnInputComposer.BuildCanonicalSourceAuthority(roots);
        var targets = EffectAcceptedTurnInputComposer.BuildCanonicalTargetAuthority(carriers, roots);
        var all = SpiritualWoundConflictContributionProjector.Project(snapshot, sources, targets, fixture.Conflict);
        Assert.True(all.IsAccepted, JsonSerializer.Serialize(all.Issues));
        var expectedContributions = ranks?.Sum() ?? 3 * parents;
        Assert.Equal(expectedContributions, all.Contributions.Count);
        var selected = all.Contributions.Where(row => row.Profile == "spiritual_strain_burden" && row.Operation == "pressure").ToArray();
        Assert.Equal(parents, selected.Length); Assert.Equal(parents, selected.Select(row => row.WoundSource.SourceId).Distinct(StringComparer.Ordinal).Count());
        foreach (var row in selected)
            Assert.Equal(("player", "player_soul", "player", 1, 1), (row.Actor.Kind, row.Actor.TargetId, row.ResolvedSide, row.CurrentStacks, row.Magnitude.GetInt32()));
        var selectedIds = selected.Select(row => (row.EffectId, row.ComponentId)).ToHashSet(); Assert.Equal(parents, selectedIds.Count);
        var isolated = snapshot with { Components = snapshot.Components.Where(row => !selectedIds.Contains((row.EffectId, row.ComponentId))).ToArray() };
        var siblings = SpiritualWoundConflictContributionProjector.Project(isolated, sources, targets, fixture.Conflict);
        Assert.True(siblings.IsAccepted, JsonSerializer.Serialize(siblings.Issues)); Assert.Equal(expectedContributions - parents, siblings.Contributions.Count);
        Assert.Equal(all.Contributions.Where(row => !selectedIds.Contains((row.EffectId, row.ComponentId))).Select(row => JsonSerializer.Serialize(row)),
            siblings.Contributions.Select(row => JsonSerializer.Serialize(row)));
        Assert.Equal(2, fixture.Profiles[AfterlifeEntityProfileState.ProfilesProperty]!.AsArray().Count);
        foreach (var profile in fixture.Profiles[AfterlifeEntityProfileState.ProfilesProperty]!.AsArray())
            foreach (var art in AfterlifeEntityProfileState.StandardArtIds) Assert.Equal(0, profile!["standardArts"]![art]!.GetValue<int>());
        Assert.True(JsonNode.DeepEquals(SpiritualCappedGroupPressureData.ReadExchange(previous), fixture.Ordinary));
        Assert.True(JsonNode.DeepEquals(fixture.Ordinary, fixture.Actual));
        var ordinarySource = SpiritualCappedGroupPressureData.CreateStandaloneSource(fixture.Ordinary, fixture.RawInput, training);
        var actualSource = fixture.Source.DeepClone().AsObject(); Assert.True(actualSource.Remove("strainPreimage")); SpiritualExistingPressureTestData.Seal(actualSource);
        foreach (var pair in new[] { (fixture.Ordinary, ordinarySource), (fixture.Actual, actualSource) })
        {
            var issues = new List<ValidationIssue>();
            ValidationService.ValidateRetainedSpiritualExchangeShape(pair.Item1, "cappedGroup.prerequisite.exchange", issues);
            ValidationService.ValidateSpiritualWoundSourceActionShape(pair.Item1, "cappedGroup.prerequisite.exchange", issues);
            ValidationService.ValidateRetainedSpiritualDiceAudit(pair.Item1, pair.Item2["turnEvidence"]!.AsObject(), issues);
            ValidationService.ValidateRetainedSpiritualExchangeRules(pair.Item1, issues); Assert.Empty(issues);
            Assert.Equal(expected, SpiritualWoundSourceWitness.ValidateAndReadStrainData(pair.Item2, out var comparison));
            Assert.Null(comparison);
        }
        Assert.True(JsonNode.DeepEquals(fixture.Ordinary, fixture.Conflict["activeConflict"]!["exchangeLog"]![0]));
        Assert.Equal("broken", fixture.Conflict["activeConflict"]!["playerSideStrain"]!.GetValue<string>());
        Assert.Equal((expected.Input.PreviousStrain, "broken"), (fixture.Ordinary["before"]!["playerSideStrain"]!.GetValue<string>(), fixture.Ordinary["after"]!["playerSideStrain"]!.GetValue<string>()));
        Assert.Equal(training ? "training" : "hostile", fixture.Conflict["activeConflict"]!["dangerMode"]!.GetValue<string>());
        Assert.True(fixture.RawInput.AsSpan().StartsWith(Encoding.UTF8.GetPreamble()));
        var rawRoot = SpiritualWoundStateJson.Parse(SpiritualWoundStateJson.DecodeUtf8JsonText(fixture.RawInput));
        Assert.True(JsonNode.DeepEquals(fixture.Conflict, rawRoot));
        var proof = fixture.Source["strainPreimage"]!;
        Assert.Equal(parents == 2 ? 4 : 6, proof["version"]!.GetValue<int>());
        Assert.Equal(parents == 2 ? "existing_pressure_ordinary_pair_v1" : "existing_pressure_ordinary_group_v1",
            proof["inputRoleRevision"]!.GetValue<string>());
        Assert.Equal(AfterlifeSpiritualConflictState.StatePath, proof["originalInput"]!["path"]!.GetValue<string>());
        Assert.True(proof["originalInput"]!["existed"]!.GetValue<bool>());
        Assert.Equal("/activeConflict/exchangeLog/0", proof["originalInput"]!["exchangePointer"]!.GetValue<string>());
        var encodedOrdinary = SpiritualWoundStateJson.Parse(SpiritualWoundStateJson.DecodeUtf8JsonText(
            Convert.FromBase64String(proof["ordinaryExchangeJsonBase64"]!.GetValue<string>())));
        Assert.True(JsonNode.DeepEquals(fixture.Ordinary, encodedOrdinary));
        Assert.True(JsonNode.DeepEquals(encodedOrdinary, rawRoot["activeConflict"]!["exchangeLog"]![0]));
        Assert.Equal((expected.TraumaPressure, expected.MaximumSeverityRank), (proof["ordinaryCalculation"]!["traumaPressure"]!.GetValue<long>(),
            proof["ordinaryCalculation"]!["maximumSeverityRank"]!.GetValue<int>()));
        var rows = proof["parents"]!.AsArray(); Assert.Equal(parents, rows.Count);
        for (var index = 0; index < parents; index++)
        {
            Assert.True(JsonNode.DeepEquals(fixture.Retained.Parents[index],
                SpiritualWoundStateJson.Parse(SpiritualWoundStateJson.DecodeUtf8JsonText(
                    Convert.FromBase64String(rows[index]!["parentWoundJsonBase64"]!.GetValue<string>())))));
            Assert.Equal((fixture.Retained.Effects[index]["effectId"]!.GetValue<string>(), "component_001"),
                (rows[index]!["effectId"]!.GetValue<string>(), rows[index]!["componentId"]!.GetValue<string>()));
        }
        var fingerprint = new CanonicalBeforeImage(true, fixture.RawInput).Fingerprint;
        Assert.Equal(fingerprint, proof["originalInput"]!["contentFingerprint"]!.GetValue<string>());
        Assert.Equal(fingerprint, fixture.Source["turnEvidence"]!["originalSnapshotFingerprint"]!.GetValue<string>());
        Assert.NotEqual(new CanonicalBeforeImage(true, Encoding.UTF8.GetBytes(fixture.Conflict.ToJsonString())).Fingerprint, fingerprint);
        Assert.Equal(before, SpiritualCappedGroupPressureData.Serialize(fixture));
    }

    /// <summary>Checks full returned v4/v6 DATA against independent parents, complete exchanges and raw-root fingerprint.</summary>
    /// <param name="fixture">Independently authored inputs, not a retained reader result.</param>
    /// <param name="parents">Every complete parent, including those beyond scalar saturation.</param>
    /// <param name="data">Successfully returned intrinsic comparison, never live authority.</param>
    /// <param name="previous">Independent before0..3 arithmetic expectation.</param>
    private static void AssertSupported(SpiritualCappedGroupPressureFixture fixture, int parents, SpiritualStrainPreimageData data,
        int previous = 3)
    {
        Assert.Equal(parents == 2 ? 4 : 6, data.Version); Assert.Equal(parents, data.Parents.Count);
        Assert.Equal(SpiritualCappedGroupPressureData.Expected(previous), data.OrdinaryCalculation);
        Assert.Equal(SpiritualCappedGroupPressureData.Expected(previous), data.ActualCalculation);
        Assert.True(JsonNode.DeepEquals(fixture.Actual, data.ReadActualExchange()));
        Assert.True(JsonNode.DeepEquals(fixture.Ordinary["after"], data.ReadOrdinaryAfter(fixture.Actual)));
        Assert.Equal(new CanonicalBeforeImage(true, fixture.RawInput).Fingerprint, data.OriginalInputFingerprint);
        Assert.Equal("/activeConflict/exchangeLog/0", data.OriginalExchangePointer);
        for (var index = 0; index < parents; index++)
        {
            Assert.Null(data.Parents[index].ParentSourceId);
            Assert.True(JsonNode.DeepEquals(fixture.Retained.Parents[index],
                JsonNode.Parse(WoundMaterializationContract.SerializeCanonical(data.Parents[index].ParentWound))));
            Assert.Equal((fixture.Retained.Effects[index]["effectId"]!.GetValue<string>(), "component_001"),
                (data.Parents[index].EffectId, data.Parents[index].ComponentId));
        }
    }
}
