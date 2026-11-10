using System.Text.Json.Nodes;
using BookOfEternityClient.Services;
using Xunit;

namespace BookOfEternityClient.Tests;

public sealed partial class SpiritualPressurePairPreimageTests
{
    /// <summary>Isolates the first causal RED: complete lawful III+IV input must survive the intrinsic pair rank gate.</summary>
    [Fact]
    public void PairPressureDataAcceptsThreeFourBoundary() =>
        AssertRankPairAccepted(3, 4, "fractured", 13L, 2);

    /// <summary>All four canonical-ID rank positions retain ordinary13/II or18/III and the same capped actual23/IV.</summary>
    [Theory]
    [InlineData(3, 3, "fractured", 13L, 2)]
    [InlineData(3, 3, "overwhelmed", 18L, 3)]
    [InlineData(3, 4, "fractured", 13L, 2)]
    [InlineData(3, 4, "overwhelmed", 18L, 3)]
    [InlineData(4, 3, "fractured", 13L, 2)]
    [InlineData(4, 3, "overwhelmed", 18L, 3)]
    [InlineData(4, 4, "fractured", 13L, 2)]
    [InlineData(4, 4, "overwhelmed", 18L, 3)]
    public void PairPressureDataAcceptsCompleteRankDomain(int first, int second, string ordinary, long trauma, int maximum) =>
        AssertRankPairAccepted(first, second, ordinary, trauma, maximum);

    /// <summary>Rank-domain fixtures must satisfy the complete existing canonical/identity/History reader independently of v4 support.</summary>
    [Theory]
    [InlineData(3, 4)]
    [InlineData(4, 3)]
    [InlineData(4, 4)]
    public void PairPressureRankFixturesAreCanonicalBeforeIntrinsicAdmission(int first, int second)
    {
        var pair = SpiritualPressureGroupViewTestData.CreatePair(ranks: new(first, second));
        var parents = SpiritualWoundCurrentData.ReadActiveBatch(pair.Carriers, pair.Identity.ToJsonString(),
            pair.History.ToJsonString(), "chaos_sea", pair.WoundIds, "player_soul:player_soul");
        Assert.Equal(new[] { first, second }, parents.Select(parent => parent.Severity.Rank));
        foreach (var parent in parents)
            AssertRankParent(pair.Parent(parent.WoundId), parent.Severity.Rank);
    }

    /// <summary>Ranks cannot make partial graphs, aliases, bad magnitudes or unrelated ordinary siblings admissible.</summary>
    [Theory]
    [InlineData("count1")]
    [InlineData("count3")]
    [InlineData("rank-II")]
    [InlineData("rank-V")]
    [InlineData("duplicate-wound")]
    [InlineData("confusable-wound")]
    [InlineData("duplicate-root")]
    [InlineData("confusable-root")]
    [InlineData("reversed-rows")]
    [InlineData("magnitude2")]
    [InlineData("stacks2")]
    [InlineData("owner")]
    [InlineData("realm")]
    [InlineData("source-affected-actor")]
    [InlineData("source-realm")]
    [InlineData("terminal")]
    [InlineData("future")]
    [InlineData("selected-effect")]
    [InlineData("selected-component")]
    [InlineData("missing-IV-slot")]
    [InlineData("missing-IV-definition")]
    [InlineData("missing-IV-entry")]
    [InlineData("missing-IV-display")]
    [InlineData("III-cost-on-IV")]
    [InlineData("ordinary-sibling")]
    public void PairPressureRankDomainRejectsInvalidCompleteInputs(string change)
    {
        var source = AssertRankPairAccepted(4, 4, "fractured", 13L, 2);
        var parent = PairParent(source, 1);
        var rows = PairProof(source)["parents"]!.AsArray();
        var definition = parent["consequences"]!["ownedEffectSources"]!["definitions"]![0]!.AsObject();
        switch (change)
        {
            case "count1": rows.RemoveAt(1); break;
            case "count3":
                var third = SpiritualWoundStateJson.Parse(parent.ToJsonString().Replace("_view_B", "_view_C", StringComparison.Ordinal));
                AssertValidPairParent(third);
                rows.Add(new JsonObject { ["parentWoundJsonBase64"] = SpiritualPressurePairTestData.Encode(third),
                    ["effectId"] = "effect_pressure_view_C", ["componentId"] = "component_001" });
                break;
            case "rank-II": parent["severity"]!["rank"] = 2; parent["severity"]!["value"] = "II"; break;
            case "rank-V": parent["severity"]!["rank"] = 5; parent["severity"]!["value"] = "V"; break;
            case "duplicate-wound": rows[1] = rows[0]!.DeepClone(); break;
            case "confusable-wound":
                parent = SpiritualWoundStateJson.Parse(parent.ToJsonString().Replace(
                    "wound_pressure_view_B", "wound_pressure_view_\u0410", StringComparison.Ordinal));
                AssertValidPairParent(parent);
                break;
            case "duplicate-root":
            case "confusable-root":
                var root = change == "duplicate-root" ? "effect_pressure_view_A" : "effect_pressure_view_\u0410";
                parent = SpiritualWoundStateJson.Parse(parent.ToJsonString().Replace("effect_pressure_view_B", root, StringComparison.Ordinal));
                rows[1]!["effectId"] = root; AssertValidPairParent(parent);
                break;
            case "reversed-rows":
                var firstRow = rows[0]!.DeepClone(); rows[0] = rows[1]!.DeepClone(); rows[1] = firstRow;
                break;
            case "magnitude2": definition["components"]![0]!["payload"]!["magnitude"] = 2; break;
            case "stacks2": definition["stacking"]!["maxStacks"] = 2; break;
            case "owner": parent["owner"]!["ownerId"] = "foreign_soul"; AssertValidPairParent(parent); break;
            case "realm":
                parent["owner"]!["realm"] = "shining_abode";
                definition["allowedRealms"] = new JsonArray("shining_abode"); AssertValidPairParent(parent);
                break;
            case "source-affected-actor": source["affectedActor"]!["actorId"] = "foreign_soul"; break;
            case "source-realm": source["turnEvidence"]!["realm"] = "shining_abode"; break;
            case "terminal": parent["lifecycle"] = "healed"; break;
            case "future":
                parent["lastTransition"]!["kind"] = "complicate"; parent["lastTransition"]!["ordinal"] = 2;
                parent["lastTransition"]!["turn"] = 47; AssertValidPairParent(parent);
                break;
            case "selected-effect": rows[1]!["effectId"] = "effect_foreign"; break;
            case "selected-component": rows[1]!["componentId"] = "component_foreign"; break;
            case "missing-IV-slot": definition["components"]!.AsArray().RemoveAt(3); break;
            case "missing-IV-definition": parent["consequences"]!["ownedEffectSources"]!["definitions"]!.AsArray().Clear(); break;
            case "missing-IV-entry": parent["consequences"]!["entries"]!.AsArray().RemoveAt(3); break;
            case "missing-IV-display": definition.Remove("display"); break;
            case "III-cost-on-IV": definition["components"]![2]!["payload"]!["magnitude"] = 2; break;
            case "ordinary-sibling":
                var ordinary = PairOrdinary(source); ordinary["incomingAction"]!["summary"] = "Иное входящее действие.";
                AssertValidPairOrdinaryShape(ordinary, source);
                PairProof(source)["ordinaryExchangeJsonBase64"] = SpiritualPressurePairTestData.Encode(ordinary);
                break;
            default: throw new ArgumentOutOfRangeException(nameof(change));
        }
        if (change is not ("count1" or "count3" or "duplicate-wound" or "reversed-rows")) SetPairParent(source, 1, parent);
        AssertPairRejected(source);
    }

    /// <summary>The separate singleton-v3 contract accepts one complete IV graph without becoming a v4 pair.</summary>
    [Fact]
    public void PairPressureRankDomainPreservesSingletonVersionThree()
    {
        var source = SpiritualExistingPressureTestData.CreateData();
        _ = SpiritualWoundSourceWitness.ValidateAndReadStrainData(source, out var baseline);
        Assert.Equal(3, Assert.IsType<SpiritualStrainPreimageData>(baseline).ParentWound.Severity.Rank);
        var parent = SpiritualPressureGroupViewTestData.CreatePair(ranks: new(4, 3)).Parent(
            SpiritualPressureGroupViewTestData.FirstWoundId);
        AssertRankParent(parent, 4);
        var proof = source["strainPreimage"]!;
        proof["parentWoundJsonBase64"] = SpiritualPressurePairTestData.Encode(parent);
        proof["effectId"] = "effect_pressure_view_A";
        SpiritualPressurePairTestData.Seal(source);
        var before = source.ToJsonString();
        var actual = SpiritualWoundSourceWitness.ValidateAndReadStrainData(source, out var retained);
        var singleton = Assert.IsType<SpiritualStrainPreimageData>(retained);
        Assert.Equal(3, singleton.Version); Assert.Single(singleton.Parents);
        Assert.Equal((4, 15L, 2, 10L, 1), (singleton.ParentWound.Severity.Rank, actual.TraumaPressure,
            actual.MaximumSeverityRank, singleton.OrdinaryCalculation!.TraumaPressure, singleton.OrdinaryCalculation.MaximumSeverityRank));
        Assert.True(JsonNode.DeepEquals(parent, JsonNode.Parse(WoundMaterializationContract.SerializeCanonical(singleton.ParentWound))));
        Assert.Equal(3, SpiritualStrainPreimageData.ReadIntrinsic(source, SpiritualExistingPressureTestData.ReadActual(source)).Version);
        Assert.Equal(before, source.ToJsonString());
    }

    /// <summary>Checks lawful full inputs before the pair reader and compares all retained fields against independent literal expectations.</summary>
    private static JsonObject AssertRankPairAccepted(int first, int second, string ordinaryStrain, long trauma, int maximum)
    {
        var source = SpiritualPressurePairTestData.CreateSource(ordinaryStrain, new(first, second));
        var before = source.ToJsonString();
        AssertRankParent(PairParent(source, 0), first);
        AssertRankParent(PairParent(source, 1), second);
        AssertValidPairOrdinaryShape(PairOrdinary(source), source);
        var actualOnly = source.DeepClone().AsObject(); actualOnly.Remove("strainPreimage");
        SpiritualPressurePairTestData.Seal(actualOnly);
        Assert.Equal((23L, 4), PressurePairMath(actualOnly));
        var ordinaryOnly = actualOnly.DeepClone().AsObject();
        ordinaryOnly["exchangeJsonBase64"] = SpiritualPressurePairTestData.Encode(PairOrdinary(source));
        ordinaryOnly["destinationStrainRank"] = maximum;
        ordinaryOnly["extraStrainJumps"] = maximum - 1;
        ordinaryOnly["maximumSeverityRank"] = maximum;
        SpiritualPressurePairTestData.Seal(ordinaryOnly);
        Assert.Equal((trauma, maximum), PressurePairMath(ordinaryOnly));

        var calculation = SpiritualWoundSourceWitness.ValidateAndReadStrainData(source, out var retained);

        Assert.Equal((23L, 4, 4), (calculation.TraumaPressure, calculation.NewStrainRank, calculation.MaximumSeverityRank));
        var data = Assert.IsType<SpiritualStrainPreimageData>(retained);
        Assert.Equal(4, data.Version);
        Assert.Equal(new[] { first, second }, data.Parents.Select(parent => parent.ParentWound.Severity.Rank));
        Assert.Equal((trauma, maximum), (data.OrdinaryCalculation!.TraumaPressure, data.OrdinaryCalculation.MaximumSeverityRank));
        Assert.Equal(new[] { SpiritualPressureGroupViewTestData.FirstWoundId, SpiritualPressureGroupViewTestData.SecondWoundId },
            data.Parents.Select(parent => parent.ParentWound.WoundId));
        for (var index = 0; index < 2; index++)
        {
            var parent = data.Parents[index];
            Assert.True(JsonNode.DeepEquals(PairParent(source, index), JsonNode.Parse(WoundMaterializationContract.SerializeCanonical(parent.ParentWound))));
            Assert.Equal(index == 0 ? "effect_pressure_view_A" : "effect_pressure_view_B", parent.EffectId);
            Assert.Equal("component_001", parent.ComponentId);
        }
        Assert.True(JsonNode.DeepEquals(PairActual(source), data.ReadActualExchange()));
        Assert.True(JsonNode.DeepEquals(PairOrdinary(source)["after"], data.ReadOrdinaryAfter(PairActual(source))));
        var expectedActual = PairOrdinary(source); expectedActual["after"]!["playerSideStrain"] = "broken";
        Assert.True(JsonNode.DeepEquals(expectedActual, data.ReadActualExchange()));
        Assert.Equal(before, source.ToJsonString());
        return source;
    }

    /// <summary>Validates complete canonical slots, root/component/entry joins and severity-specific siblings without the pair rank predicate.</summary>
    private static void AssertRankParent(JsonObject parent, int rank)
    {
        var result = WoundMaterializationContract.Parse(parent.ToJsonString(), "unit.pair.rankParent");
        Assert.True(result.IsValid, string.Join("\n", result.Issues));
        var parsed = result.Wound!;
        Assert.Equal(("active", "spiritual", new WoundOwnerCoordinate("chaos_sea", "player_soul", "player_soul",
            WoundCarrierCatalog.AfterlifeProfilesPath)), (parsed.Lifecycle, parsed.Classification.Domain, parsed.Owner));
        Assert.True(parsed.LastTransition.Turn <= 46);
        Assert.True(JsonNode.DeepEquals(parent, JsonNode.Parse(WoundMaterializationContract.SerializeCanonical(parsed))));
        Assert.Equal((rank, rank, rank, rank == 4 ? 8 : 6), (parsed.Severity.Rank,
            parent["consequences"]!["slotBudget"]!.GetValue<int>(), parent["consequences"]!["slotsUsed"]!.GetValue<int>(),
            parent["recovery"]!["currentStepThreshold"]!.GetValue<int>()));
        var definition = parent["consequences"]!["ownedEffectSources"]!["definitions"]![0]!;
        Assert.Single(parsed.Consequences.OwnedEffectSources.Definitions);
        var root = Assert.Single(parsed.Consequences.OwnedEffectSources.RootBindings);
        Assert.All(parsed.Consequences.Entries, entry => Assert.Equal(root.EffectId, entry.EffectId));
        Assert.Equal(1, definition["stacking"]!["maxStacks"]!.GetValue<int>());
        Assert.Equal(rank, definition["components"]!.AsArray().Count);
        Assert.Equal(rank, definition["triggers"]![0]!["componentIds"]!.AsArray().Count);
        Assert.Equal(rank, parent["consequences"]!["entries"]!.AsArray().Count);
        Assert.Equal(("spiritual_strain_burden", "pressure", 1),
            (definition["components"]![0]!["profile"]!.GetValue<string>(), definition["components"]![0]!["payload"]!["operation"]!.GetValue<string>(),
                definition["components"]![0]!["payload"]!["magnitude"]!.GetValue<int>()));
        Assert.Equal("disadvantage", definition["components"]![1]!["payload"]!["magnitude"]!.GetValue<string>());
        Assert.Equal(rank == 4 ? 3 : 2, definition["components"]![2]!["payload"]!["magnitude"]!.GetValue<int>());
        if (rank == 4) Assert.Equal(("counter", 3), (definition["components"]![3]!["payload"]!["operation"]!.GetValue<string>(),
            definition["components"]![3]!["payload"]!["magnitude"]!.GetValue<int>()));
    }
}
