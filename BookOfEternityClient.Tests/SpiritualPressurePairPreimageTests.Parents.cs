using System.Text.Json.Nodes;
using BookOfEternityClient.Services;
using Xunit;

namespace BookOfEternityClient.Tests;

public sealed partial class SpiritualPressurePairPreimageTests
{
    /// <summary>Definition and stack names are source-scoped, while wounds and root effects remain globally distinct.</summary>
    [Fact]
    public void PairPressureDataAllowsSourceScopedDefinitionAndStackNames()
    {
        var source = SupportedPairSource();
        var first = PairParent(source, 0);
        var second = PairParent(source, 1);
        var firstGraph = first["consequences"]!["ownedEffectSources"]!.AsObject();
        var secondGraph = second["consequences"]!["ownedEffectSources"]!.AsObject();
        var firstDefinition = firstGraph["definitions"]![0]!.AsObject();
        var secondDefinition = secondGraph["definitions"]![0]!.AsObject();
        secondDefinition["definitionKey"] = firstDefinition["definitionKey"]!.DeepClone();
        secondDefinition["stacking"]!["stackKey"] = firstDefinition["stacking"]!["stackKey"]!.DeepClone();
        secondGraph["rootBindings"]![0]!["definitionKey"] = firstDefinition["definitionKey"]!.DeepClone();
        Assert.NotEqual(first["woundId"]!.GetValue<string>(), second["woundId"]!.GetValue<string>());
        Assert.NotEqual(firstGraph["rootBindings"]![0]!["effectId"]!.GetValue<string>(),
            secondGraph["rootBindings"]![0]!["effectId"]!.GetValue<string>());
        Assert.Equal(second["woundId"]!.GetValue<string>(), secondDefinition["links"]![0]!["targetId"]!.GetValue<string>());
        AssertValidPairParent(first);
        AssertValidPairParent(second);
        SetPairParent(source, 1, second);
        SpiritualPressurePairTestData.Seal(source);
        var before = source.ToJsonString();
        var calculation = SpiritualWoundSourceWitness.ValidateAndReadStrainData(source, out var retained);
        Assert.Equal((23L, 4), (calculation.TraumaPressure, calculation.MaximumSeverityRank));
        Assert.Equal(4, Assert.IsType<SpiritualStrainPreimageData>(retained).Version);
        Assert.Equal(before, source.ToJsonString());
    }

    /// <summary>The shared new list retains exactly one complete parent and optional v1 source coordinate for each old version.</summary>
    [Theory]
    [InlineData(1)]
    [InlineData(2)]
    [InlineData(3)]
    public void PairPressureDataPreservesLegacySingleParentList(int version)
    {
        var source = version switch
        {
            1 => SpiritualStrainPreimageDataTests.CreateData().Source,
            2 => SpiritualExistingStrainPreimageTests.CreateData(3).Source,
            3 => SpiritualExistingPressureTestData.CreateData(),
            _ => throw new ArgumentOutOfRangeException(nameof(version))
        };
        var before = source.ToJsonString();
        _ = SpiritualWoundSourceWitness.ValidateAndReadStrainData(source, out var retained);
        var data = Assert.IsType<SpiritualStrainPreimageData>(retained);
        Assert.Equal(version, data.Version);
        var parent = Assert.Single(data.Parents);
        Assert.Equal(WoundMaterializationContract.SerializeCanonical(data.ParentWound),
            WoundMaterializationContract.SerializeCanonical(parent.ParentWound));
        Assert.Equal(data.ParentSemanticFingerprint, parent.ParentSemanticFingerprint);
        Assert.Equal(data.EffectId, parent.EffectId);
        Assert.Equal(data.ComponentId, parent.ComponentId);
        Assert.Equal(data.ParentSourceId, parent.ParentSourceId);
        AssertPairCollectionRejectsMutation(data.Parents);
        Assert.Equal(before, source.ToJsonString());
    }

    /// <summary>Exercises the proposed typed immutable Parents contract; it is not present on the current production type yet.</summary>
    [Theory]
    [InlineData("fractured")]
    [InlineData("overwhelmed")]
    public void PairPressureDataRetainsImmutableCanonicalParentFacts(string ordinaryStrain)
    {
        var source = SupportedPairSource(out var data, ordinaryStrain);
        IReadOnlyList<SpiritualStrainParentData> parents = data.Parents;
        Assert.Equal(2, parents.Count);
        Assert.Equal(new[] { SpiritualPressureGroupViewTestData.FirstWoundId,
            SpiritualPressureGroupViewTestData.SecondWoundId }, parents.Select(parent => parent.ParentWound.WoundId));
        Assert.Equal(new[] { "component_001", "component_001" }, parents.Select(parent => parent.ComponentId));
        AssertPairCollectionRejectsMutation(parents);
        for (var index = 0; index < parents.Count; index++)
        {
            var parent = parents[index];
            var expected = WoundMaterializationContract.Parse(PairParent(source, index).ToJsonString(), "unit.pairPressure.expected");
            Assert.True(expected.IsValid, string.Join("\n", expected.Issues));
            var expectedJson = WoundMaterializationContract.SerializeCanonical(expected.Wound!);
            Assert.Equal(expectedJson, WoundMaterializationContract.SerializeCanonical(parent.ParentWound));
            Assert.Equal(WoundIdentityState.ComputeSemanticFingerprint(expected.Wound!), parent.ParentSemanticFingerprint);
            Assert.Equal(index == 0 ? "effect_pressure_view_A" : "effect_pressure_view_B", parent.EffectId);
            var wound = parent.ParentWound;
            AssertPairCollectionRejectsMutation(wound.Display.VisibleSymptoms);
            AssertPairCollectionRejectsMutation(wound.Complications);
            AssertPairCollectionRejectsMutation(wound.Consequences.Entries);
            AssertPairCollectionRejectsMutation(wound.Consequences.OwnedEffectSources.Definitions);
            AssertPairCollectionRejectsMutation(wound.Consequences.OwnedEffectSources.RootBindings);
            AssertPairCollectionRejectsMutation(wound.Consequences.OwnedEffectSources.DefinitionFacts);
            AssertPairCollectionRejectsMutation(wound.Treatment.DiagnosisPaths);
            AssertPairCollectionRejectsMutation(wound.Treatment.Routes);
            AssertPairCollectionRejectsMutation(wound.Treatment.KnownRouteIds);
            AssertPairCollectionRejectsMutation(wound.Treatment.CompletedRouteIds);
            AssertPairCollectionRejectsMutation(wound.Recovery.Blockers);
            AssertPairCollectionRejectsMutation(wound.Relations.LegacyRefs);
            AssertPairCollectionRejectsMutation(wound.Relations.IndependentEffectRefs);
            Assert.Equal(expectedJson, WoundMaterializationContract.SerializeCanonical(wound));
            Assert.Equal(parent.ParentSemanticFingerprint, WoundIdentityState.ComputeSemanticFingerprint(wound));
        }
    }

    /// <summary>No legacy singular accessor may silently return the first of two v4 parents.</summary>
    [Fact]
    public void PairPressureDataLegacySingularAccessorsRefusePair()
    {
        _ = SupportedPairSource(out var data);
        Assert.Throws<FormatException>(() => data.ParentWound);
        Assert.Throws<FormatException>(() => data.ParentSemanticFingerprint);
        Assert.Throws<FormatException>(() => data.EffectId);
        Assert.Throws<FormatException>(() => data.ComponentId);
        Assert.Throws<FormatException>(() => data.ParentSourceId);
    }

    /// <summary>Retained full parent facts survive changes to caller wire and detached serialization without reparsing getters.</summary>
    [Fact]
    public void PairPressureDataDetachesParentsAndSnapshotsFromCallerMutation()
    {
        var source = SupportedPairSource(out var data);
        var facts = data.Parents;
        var parentImages = facts.Select(parent => WoundMaterializationContract.SerializeCanonical(parent.ParentWound)).ToArray();
        var fingerprints = facts.Select(parent => parent.ParentSemanticFingerprint).ToArray();
        var actual = data.ReadActualExchange();
        var expectedActual = actual.DeepClone().AsObject();
        var expectedOrdinaryAfter = PairOrdinary(source)["after"]!.DeepClone();
        for (var index = 0; index < 2; index++)
        {
            var callerParent = PairParent(source, index);
            callerParent["display"]!["name"] = "Changed caller parent";
            SetPairParent(source, index, callerParent);
            var exported = SpiritualWoundStateJson.Parse(parentImages[index]);
            exported["display"]!["name"] = "Changed exported parent";
            exported["consequences"]!["ownedEffectSources"]!["definitions"]![0]!["display"]!["description"] = "Changed exported graph";
            Assert.NotEqual(parentImages[index], exported.ToJsonString());
        }
        PairProof(source)["ordinaryExchangeJsonBase64"] = PairEncodeText("{}");
        source["exchangeJsonBase64"] = PairEncodeText("{}");
        actual["summary"] = "Changed returned actual";
        data.ReadOrdinaryAfter(expectedActual)["playerSideStrain"] = "clear";
        Assert.Equal(parentImages, facts.Select(parent => WoundMaterializationContract.SerializeCanonical(parent.ParentWound)));
        Assert.Equal(fingerprints, facts.Select(parent => parent.ParentSemanticFingerprint));
        Assert.True(JsonNode.DeepEquals(expectedActual, data.ReadActualExchange()));
        Assert.True(JsonNode.DeepEquals(expectedOrdinaryAfter, data.ReadOrdinaryAfter(expectedActual)));
        Assert.Throws<FormatException>(() => data.ReadOrdinaryAfter(actual));
    }

    /// <summary>A lawful changed full envelope remains intrinsic DATA; only an external recorded History anchor can reject it.</summary>
    [Fact]
    public void PairPressureDataAcceptsResealedLawfulSecondEnvelopeChange()
    {
        var source = SupportedPairSource(out var original);
        var beforeFingerprint = original.Parents[1].ParentSemanticFingerprint;
        var changedParent = PairParent(source, 1);
        changedParent["display"]!["description"] = "Иное допустимое полное описание второй раны.";
        AssertValidPairParent(changedParent);
        var expected = WoundMaterializationContract.Parse(changedParent.ToJsonString(), "unit.pairPressure.changedParent").Wound!;
        SetPairParent(source, 1, changedParent);
        SpiritualPressurePairTestData.Seal(source);
        var before = source.ToJsonString();
        var calculation = SpiritualWoundSourceWitness.ValidateAndReadStrainData(source, out var changed);
        var data = Assert.IsType<SpiritualStrainPreimageData>(changed);
        Assert.Equal((23L, 4), (calculation.TraumaPressure, calculation.MaximumSeverityRank));
        Assert.NotEqual(beforeFingerprint, data.Parents[1].ParentSemanticFingerprint);
        Assert.Equal(WoundIdentityState.ComputeSemanticFingerprint(expected), data.Parents[1].ParentSemanticFingerprint);
        Assert.Equal(before, source.ToJsonString());
    }

    /// <summary>The convenience path authenticates the complete containing witness and the exact supplied actual exchange.</summary>
    [Fact]
    public void PairPressureDataConvenienceReadRequiresExactActual()
    {
        var source = SupportedPairSource();
        var before = source.ToJsonString();
        var actual = PairActual(source);
        var retained = SpiritualStrainPreimageData.ReadIntrinsic(source, actual);
        actual["summary"] = "Another otherwise well-shaped actual exchange";
        Assert.Throws<FormatException>(() => SpiritualStrainPreimageData.ReadIntrinsic(source, actual));
        Assert.Throws<FormatException>(() => retained.ReadOrdinaryAfter(actual));
        Assert.Equal(before, source.ToJsonString());
    }

    private static void AssertPairCollectionRejectsMutation<T>(IReadOnlyList<T> values)
    {
        // ImmutableArray and read-only wrappers expose this explicit interface, while a hidden raw array/list is caught here.
        if (values is ICollection<T> mutable)
        {
            Assert.True(mutable.IsReadOnly);
            Assert.Throws<NotSupportedException>(() => mutable.Add(values.Count == 0 ? default! : values[0]));
        }
        if (values is IList<T> indexed && values.Count != 0)
            Assert.Throws<NotSupportedException>(() => indexed[0] = values[0]);
    }
}
