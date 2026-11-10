using System.Text;
using System.Text.Json;
using System.Text.Json.Nodes;
using BookOfEternityClient.Services;
using Xunit;
using Xunit.Abstractions;

namespace BookOfEternityClient.Tests;

/// <summary>Cheap full N3/N4 prerequisites and unconditional v6 desired success without files or live authority.</summary>
public sealed partial class SpiritualPressureMultiParentDataTests(ITestOutputHelper output)
{
    /// <summary>Proves every complete retained parent and both standalone calculations before any v6 read.</summary>
    /// <param name="parents">Closed independent N3/N4 retained geometry before source turn46.</param>
    [Theory]
    [InlineData(3)] [InlineData(4)]
    public void MultiParentPressure_CompletePrerequisites(int parents) =>
        AssertPrerequisites(SpiritualPressureMultiParentData.Create(parents), parents);

    /// <summary>Requires full-witness and direct intrinsic v6 success after all independent lawful premises pass.</summary>
    /// <param name="parents">Every parent must be retained even when actual strain has already saturated.</param>
    [Theory]
    [InlineData(3)] [InlineData(4)]
    public void MultiParentPressure_VersionSixWitnessAndIntrinsicSucceed(int parents)
    {
        var fixture = SpiritualPressureMultiParentData.Create(parents);
        AssertPrerequisites(fixture, parents);
        var before = SpiritualPressureMultiParentData.Serialize(fixture);
        SpiritualStrainPreimageData? retained = null; SpiritualWoundCalculation? calculation = null;
        var error = Record.Exception(() => calculation = SpiritualWoundSourceWitness.ValidateAndReadStrainData(fixture.Source, out retained));
        SpiritualStrainPreimageData? direct = null;
        var directError = Record.Exception(() => direct = SpiritualStrainPreimageData.ReadIntrinsic(fixture.Source, fixture.Actual));
        output.WriteLine(JsonSerializer.Serialize(new { phase = "multi_parent_intrinsic_v6", parents,
            prerequisitesComplete = true, error = error?.ToString(), directError = directError?.ToString(), calculation,
            comparisonReturned = retained is not null, directReturned = direct is not null,
            fixture = before }));
        Assert.Equal(before, SpiritualPressureMultiParentData.Serialize(fixture));
        if (error is not null) { Assert.Null(calculation); Assert.Null(retained); }
        if (directError is not null) Assert.Null(direct);
        // The old supported-version refusal is causal RED, never a passing expectation.
        Assert.Null(error); Assert.Null(directError);
        Assert.Equal(SpiritualPressureMultiParentData.ActualCalculation, calculation);
        AssertSupported(fixture, parents, Assert.IsType<SpiritualStrainPreimageData>(retained));
        AssertSupported(fixture, parents, Assert.IsType<SpiritualStrainPreimageData>(direct));
        retained!.ReadActualExchange()["after"]!["playerSideStrain"] = "clear";
        retained.ReadOrdinaryAfter(fixture.Actual)["playerSideStrain"] = "clear";
        Assert.True(JsonNode.DeepEquals(fixture.Actual, retained.ReadActualExchange()));
        Assert.True(JsonNode.DeepEquals(fixture.Ordinary["after"], retained.ReadOrdinaryAfter(fixture.Actual)));
        Assert.Equal(before, SpiritualPressureMultiParentData.Serialize(fixture));
    }

    /// <summary>Validates the one already-authored fixture through real typed graph, receipt, History, projection and source validators.</summary>
    /// <param name="fixture">Complete independently mutable retained DATA, prepared once by this test.</param>
    /// <param name="parents">Exact expected parent count, not an input to production authority.</param>
    internal static void AssertPrerequisites(SpiritualPressureMultiParentFixture fixture, int parents)
    {
        var before = SpiritualPressureMultiParentData.Serialize(fixture);
        var admissible = SpiritualPressureRetainedDataAdmissibility.Check(fixture.Retained, fixture.Profiles);
        Assert.True(admissible.IsValid, JsonSerializer.Serialize(admissible));
        Assert.Equal(parents, fixture.Retained.Parents.Count); Assert.Equal(parents, fixture.Retained.Effects.Count);
        foreach (var field in new[] { "instances", "closures", "sources", "decisions" })
            Assert.Equal(parents, fixture.Retained.Receipts[field]!.AsArray().Count);
        Assert.Equal(parents, fixture.Retained.History["transitions"]!.AsArray().Count);
        for (var index = 0; index < parents; index++)
        {
            var parent = fixture.Retained.Parents[index]; var effect = fixture.Retained.Effects[index];
            var parsed = WoundMaterializationContract.Parse(parent.ToJsonString(), "multiParent.prerequisite.parent");
            Assert.True(parsed.IsValid, JsonSerializer.Serialize(parsed.Issues));
            Assert.True(JsonNode.DeepEquals(parent, JsonNode.Parse(WoundMaterializationContract.SerializeCanonical(parsed.Wound!))));
            Assert.Equal(("wound_retained_pressure_owner_" + (index + 1), 3, "III", 6),
                (parent["woundId"]!.GetValue<string>(), parent["severity"]!["rank"]!.GetValue<int>(),
                    parent["severity"]!["maximumAtCreation"]!.GetValue<string>(), parent["recovery"]!["currentStepThreshold"]!.GetValue<int>()));
            Assert.True(parent["origin"]!["createdAtTurn"]!.GetValue<int>() < 46);
            Assert.Equal((3, 3, 3), (parent["consequences"]!["slotBudget"]!.GetValue<int>(),
                parent["consequences"]!["slotsUsed"]!.GetValue<int>(), parent["consequences"]!["entries"]!.AsArray().Count));
            var components = effect["components"]!.AsArray(); Assert.Equal(3, components.Count);
            Assert.Equal(new[] { "component_001", "component_002", "component_003" },
                effect["triggers"]![0]!["componentIds"]!.AsArray().Select(row => row!.GetValue<string>()));
            Assert.Equal(("pressure", 1), (components[0]!["payload"]!["operation"]!.GetValue<string>(), components[0]!["payload"]!["magnitude"]!.GetValue<int>()));
            Assert.Equal(("counter", "disadvantage"), (components[1]!["payload"]!["operation"]!.GetValue<string>(), components[1]!["payload"]!["magnitude"]!.GetValue<string>()));
            Assert.Equal(("guard", 2), (components[2]!["payload"]!["operation"]!.GetValue<string>(), components[2]!["payload"]!["magnitude"]!.GetValue<int>()));
            var birth = SpiritualWoundSourceWitness.ValidateAndReadStrainData(fixture.Retained.Receipts["sources"]![index]!["witness"]!.AsObject(), out var birthComparison);
            Assert.Null(birthComparison); Assert.Equal((0, 3, 20L, 3), (birth.PreviousStrainRank, birth.NewStrainRank, birth.TraumaPressure, birth.MaximumSeverityRank));
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
        Assert.True(all.IsAccepted, JsonSerializer.Serialize(all.Issues)); Assert.Equal(3 * parents, all.Contributions.Count);
        var selected = all.Contributions.Where(row => row.Profile == "spiritual_strain_burden" && row.Operation == "pressure").ToArray();
        Assert.Equal(parents, selected.Length); Assert.Equal(parents, selected.Select(row => row.WoundSource.SourceId).Distinct(StringComparer.Ordinal).Count());
        foreach (var row in selected)
            Assert.Equal(("player", "player_soul", "player", 1, 1), (row.Actor.Kind, row.Actor.TargetId, row.ResolvedSide, row.CurrentStacks, row.Magnitude.GetInt32()));
        var selectedIds = selected.Select(row => (row.EffectId, row.ComponentId)).ToHashSet();
        Assert.Equal(parents, selectedIds.Count);
        var isolated = snapshot with { Components = snapshot.Components.Where(row => !selectedIds.Contains((row.EffectId, row.ComponentId))).ToArray() };
        var siblings = SpiritualWoundConflictContributionProjector.Project(isolated, sources, targets, fixture.Conflict);
        Assert.True(siblings.IsAccepted, JsonSerializer.Serialize(siblings.Issues)); Assert.Equal(2 * parents, siblings.Contributions.Count);
        Assert.Equal(all.Contributions.Where(row => !selectedIds.Contains((row.EffectId, row.ComponentId))).Select(row => JsonSerializer.Serialize(row)),
            siblings.Contributions.Select(row => JsonSerializer.Serialize(row)));
        var player = Assert.Single(fixture.Profiles[AfterlifeEntityProfileState.ProfilesProperty]!.AsArray(), row => row!["actorType"]!.GetValue<string>() == "player_soul")!;
        Assert.Equal((0, 0), (player["standardArts"]!["pressure"]!.GetValue<int>(), player["standardArts"]!["spiritual_resilience"]!.GetValue<int>()));
        Assert.True(JsonNode.DeepEquals(SpiritualPairLowPressureData.ReadExchange("strained"), fixture.Ordinary));
        Assert.True(JsonNode.DeepEquals(SpiritualPairLowPressureData.ReadExchange("broken"), fixture.Actual));
        var ordinarySource = SpiritualPressureMultiParentData.CreateStandaloneSource(fixture.Ordinary, fixture.RawInput);
        var actualSource = fixture.Source.DeepClone().AsObject(); Assert.True(actualSource.Remove("strainPreimage")); SpiritualExistingPressureTestData.Seal(actualSource);
        foreach (var pair in new[] { (fixture.Ordinary, ordinarySource, SpiritualPressureMultiParentData.OrdinaryCalculation),
                     (fixture.Actual, actualSource, SpiritualPressureMultiParentData.ActualCalculation) })
        {
            var issues = new List<ValidationIssue>();
            ValidationService.ValidateRetainedSpiritualExchangeShape(pair.Item1, "multiParent.prerequisite.exchange", issues);
            ValidationService.ValidateSpiritualWoundSourceActionShape(pair.Item1, "multiParent.prerequisite.exchange", issues);
            ValidationService.ValidateRetainedSpiritualDiceAudit(pair.Item1, pair.Item2["turnEvidence"]!.AsObject(), issues);
            ValidationService.ValidateRetainedSpiritualExchangeRules(pair.Item1, issues); Assert.Empty(issues);
            Assert.Equal(pair.Item3, SpiritualWoundSourceWitness.ValidateAndReadStrainData(pair.Item2, out var comparison)); Assert.Null(comparison);
        }
        var corrected = fixture.Ordinary.DeepClone(); corrected["after"]!["playerSideStrain"] = "broken";
        Assert.True(JsonNode.DeepEquals(corrected, fixture.Actual));
        Assert.True(JsonNode.DeepEquals(fixture.Ordinary, fixture.Conflict["activeConflict"]!["exchangeLog"]![0]));
        Assert.Equal("strained", fixture.Conflict["activeConflict"]!["playerSideStrain"]!.GetValue<string>());
        Assert.True(fixture.RawInput.AsSpan().StartsWith(Encoding.UTF8.GetPreamble()));
        Assert.True(JsonNode.DeepEquals(fixture.Conflict, SpiritualWoundStateJson.Parse(SpiritualWoundStateJson.DecodeUtf8JsonText(fixture.RawInput))));
        Assert.Equal(new CanonicalBeforeImage(true, fixture.RawInput).Fingerprint,
            fixture.Source["strainPreimage"]!["originalInput"]!["contentFingerprint"]!.GetValue<string>());
        Assert.NotEqual(new CanonicalBeforeImage(true, Encoding.UTF8.GetBytes(fixture.Conflict.ToJsonString())).Fingerprint,
            new CanonicalBeforeImage(true, fixture.RawInput).Fingerprint);
        Assert.Equal(before, SpiritualPressureMultiParentData.Serialize(fixture));
    }

    /// <summary>Checks every returned parent, full exchange/calculation and raw-input binding against independent expected DATA.</summary>
    /// <param name="fixture">Complete detached input, not derived from the reader.</param>
    /// <param name="parents">Exact complete list size, including every parent after scalar saturation.</param>
    /// <param name="data">Successfully returned intrinsic comparison, never permission to publish.</param>
    internal static void AssertSupported(SpiritualPressureMultiParentFixture fixture, int parents, SpiritualStrainPreimageData data)
    {
        Assert.Equal(6, data.Version); Assert.Equal(parents, data.Parents.Count);
        Assert.Equal(SpiritualPressureMultiParentData.OrdinaryCalculation, data.OrdinaryCalculation);
        Assert.Equal(SpiritualPressureMultiParentData.ActualCalculation, data.ActualCalculation);
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
