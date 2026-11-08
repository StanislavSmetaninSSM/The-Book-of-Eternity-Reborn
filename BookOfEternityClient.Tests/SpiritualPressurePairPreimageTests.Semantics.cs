using System.Text.Json.Nodes;
using BookOfEternityClient.Services;
using Xunit;

namespace BookOfEternityClient.Tests;

public sealed partial class SpiritualPressurePairPreimageTests
{
    /// <summary>Requires supported v4 before testing bounded arithmetic or outer actual range refusals.</summary>
    [Theory]
    [InlineData("ordinary-I-actual-IV-overincrement")]
    [InlineData("ordinary-IV")]
    [InlineData("nonincreasing-ordinary")]
    [InlineData("boundary-underincrement")]
    [InlineData("outer-overflow-unclamped-range")]
    [InlineData("ordinary-trauma")]
    [InlineData("ordinary-maximum")]
    [InlineData("outer-actual-rank-scalar")]
    [InlineData("outer-actual-maximum")]
    [InlineData("nonpositive-actual")]
    public void PairPressureDataRejectsArithmeticContradictionsAfterPositiveBaseline(string change)
    {
        var source = SupportedPairSource(change == "outer-overflow-unclamped-range" ? "overwhelmed" : "fractured");
        var proof = PairProof(source);
        var ordinary = PairOrdinary(source);
        var actual = PairActual(source);
        switch (change)
        {
            case "ordinary-I-actual-IV-overincrement":
                ordinary["after"]!["playerSideStrain"] = "strained";
                proof["ordinaryCalculation"]!["traumaPressure"] = 8L;
                proof["ordinaryCalculation"]!["maximumSeverityRank"] = 1;
                break;
            case "ordinary-IV":
                ordinary["after"]!["playerSideStrain"] = "broken";
                proof["ordinaryCalculation"]!["traumaPressure"] = 23L;
                proof["ordinaryCalculation"]!["maximumSeverityRank"] = 4;
                break;
            case "nonincreasing-ordinary":
                ordinary["after"]!["playerSideStrain"] = "clear";
                proof["ordinaryCalculation"]!["traumaPressure"] = 6L;
                proof["ordinaryCalculation"]!["maximumSeverityRank"] = 0;
                break;
            case "boundary-underincrement":
                actual["after"]!["playerSideStrain"] = "overwhelmed";
                source["destinationStrainRank"] = 3; source["extraStrainJumps"] = 2;
                source["maximumSeverityRank"] = 3;
                var actualOnly = source.DeepClone().AsObject();
                actualOnly.Remove("strainPreimage"); actualOnly["exchangeJsonBase64"] = SpiritualPressurePairTestData.Encode(actual);
                SpiritualPressurePairTestData.Seal(actualOnly);
                Assert.Equal((18L, 3), PressurePairMath(actualOnly));
                break;
            case "outer-overflow-unclamped-range":
                // A malformed rank5 is range coverage, not evidence that the cap calculation ran.
                actual["after"]!["playerSideStrain"] = "rank5";
                source["destinationStrainRank"] = 5; source["extraStrainJumps"] = 4;
                break;
            case "ordinary-trauma": proof["ordinaryCalculation"]!["traumaPressure"] = 14L; break;
            case "ordinary-maximum": proof["ordinaryCalculation"]!["maximumSeverityRank"] = 3; break;
            case "outer-actual-rank-scalar": source["destinationStrainRank"] = 3; break;
            case "outer-actual-maximum": source["maximumSeverityRank"] = 3; break;
            case "nonpositive-actual":
                source["dangerMode"] = "training"; source["maximumSeverityRank"] = 0;
                proof["ordinaryCalculation"]!["maximumSeverityRank"] = 0;
                break;
            default: throw new ArgumentOutOfRangeException(nameof(change));
        }
        proof["ordinaryExchangeJsonBase64"] = SpiritualPressurePairTestData.Encode(ordinary);
        source["exchangeJsonBase64"] = SpiritualPressurePairTestData.Encode(actual);
        AssertPairRejected(source);
    }

    /// <summary>Rejects a selected subset, noncanonical order and aliases anywhere in either complete parent graph.</summary>
    [Theory]
    [InlineData("count1")]
    [InlineData("count3")]
    [InlineData("exact-duplicate")]
    [InlineData("wound-case-alias")]
    [InlineData("wound-confusable-alias")]
    [InlineData("reversed-rows")]
    [InlineData("shared-selected-root")]
    [InlineData("confusable-selected-root")]
    [InlineData("unselected-sibling-root-collision")]
    public void PairPressureDataRejectsCardinalityOrderAndGlobalAliases(string change)
    {
        var source = SupportedPairSource();
        var rows = PairProof(source)["parents"]!.AsArray();
        var second = PairParent(source, 1);
        switch (change)
        {
            case "count1": rows.RemoveAt(1); break;
            case "count3":
                var third = SpiritualWoundStateJson.Parse(second.ToJsonString()
                    .Replace("_view_B", "_view_C", StringComparison.Ordinal));
                AssertValidPairParent(third);
                rows.Add(new JsonObject { ["parentWoundJsonBase64"] = SpiritualPressurePairTestData.Encode(third),
                    ["effectId"] = "effect_pressure_view_C", ["componentId"] = "component_001" });
                break;
            case "exact-duplicate": rows[1] = rows[0]!.DeepClone(); break;
            case "wound-case-alias":
            case "wound-confusable-alias":
                second = SpiritualWoundStateJson.Parse(second.ToJsonString().Replace(
                    SpiritualPressureGroupViewTestData.SecondWoundId,
                    change == "wound-case-alias" ? "WOUND_PRESSURE_VIEW_A" : "wound_pressure_view_\u0410",
                    StringComparison.Ordinal));
                AssertValidPairParent(second); SetPairParent(source, 1, second);
                break;
            case "reversed-rows":
                var firstRow = rows[0]!.DeepClone(); var secondRow = rows[1]!.DeepClone();
                rows.Clear(); rows.Add(secondRow); rows.Add(firstRow);
                break;
            case "shared-selected-root":
            case "confusable-selected-root":
                var alias = change == "shared-selected-root" ? "effect_pressure_view_A" : "effect_pressure_view_\u0410";
                second = SpiritualWoundStateJson.Parse(second.ToJsonString()
                    .Replace("effect_pressure_view_B", alias, StringComparison.Ordinal));
                AssertValidPairParent(second); SetPairParent(source, 1, second);
                rows[1]!["effectId"] = alias;
                break;
            case "unselected-sibling-root-collision":
                SplitPairSiblingRoot(second, "effect_pressure_view_A");
                AssertValidPairParent(second); SetPairParent(source, 1, second);
                Assert.Equal("effect_pressure_view_B", rows[1]!["effectId"]!.GetValue<string>());
                break;
            default: throw new ArgumentOutOfRangeException(nameof(change));
        }
        if (change is "wound-case-alias" or "wound-confusable-alias")
        {
            // Isolate cross-parent identity refusal from canonical-order refusal, including uppercase aliases.
            var canonicalRows = rows.OfType<JsonObject>().Select(row =>
                (Id: PairDecode(row["parentWoundJsonBase64"])["woundId"]!.GetValue<string>(), Row: row.DeepClone()))
                .OrderBy(row => row.Id, StringComparer.Ordinal).ToArray();
            Assert.True(StringComparer.Ordinal.Compare(canonicalRows[0].Id, canonicalRows[1].Id) < 0);
            rows.Clear();
            foreach (var row in canonicalRows)
            {
                AssertValidPairParent(PairDecode(row.Row!["parentWoundJsonBase64"]));
                rows.Add(row.Row);
            }
        }
        AssertPairRejected(source);
    }

    /// <summary>Validates the whole second snapshot; row/rank/owner controls prove valid individual envelopes first.</summary>
    [Theory]
    [InlineData("owner", true)]
    [InlineData("realm", true)]
    [InlineData("domain", false)]
    [InlineData("terminal", false)]
    [InlineData("future", true)]
    [InlineData("selected-effect", true)]
    [InlineData("selected-component", true)]
    [InlineData("pressure-operation", true)]
    [InlineData("magnitude", false)]
    [InlineData("profile-entry", false)]
    [InlineData("max-stacks", false)]
    public void PairPressureDataRequiresLawfulCompleteSecondParent(string change, bool individuallyValid)
    {
        var source = SupportedPairSource();
        var parent = PairParent(source, 1);
        var graph = parent["consequences"]!["ownedEffectSources"]!.AsObject();
        var definition = graph["definitions"]![0]!.AsObject();
        switch (change)
        {
            case "owner": parent["owner"]!["ownerId"] = "foreign_soul"; break;
            case "realm":
                parent["owner"]!["realm"] = "shining_abode";
                definition["allowedRealms"] = new JsonArray("shining_abode");
                break;
            case "domain": parent["classification"]!["domain"] = "physical"; break;
            case "terminal": parent["lifecycle"] = "healed"; break;
            case "future":
                parent["lastTransition"]!["turn"] = 47;
                parent["lastTransition"]!["kind"] = "complicate";
                parent["lastTransition"]!["ordinal"] = 2;
                break;
            case "selected-effect": PairProof(source)["parents"]![1]!["effectId"] = "effect_foreign"; break;
            case "selected-component": PairProof(source)["parents"]![1]!["componentId"] = "component_foreign"; break;
            case "pressure-operation": definition["components"]![0]!["payload"]!["operation"] = "maneuver"; break;
            case "magnitude": definition["components"]![0]!["payload"]!["magnitude"] = 2; break;
            case "profile-entry": parent["consequences"]!["entries"]!.AsArray().RemoveAt(0); break;
            case "max-stacks": definition["stacking"]!["maxStacks"] = 2; break;
            default: throw new ArgumentOutOfRangeException(nameof(change));
        }
        if (individuallyValid) AssertValidPairParent(parent);
        SetPairParent(source, 1, parent);
        AssertPairRejected(source);
    }

    /// <summary>Preserves every complete exchange sibling instead of accepting after-strain-only comparisons.</summary>
    [Theory]
    [InlineData("before")]
    [InlineData("opposition-strain")]
    [InlineData("position")]
    [InlineData("control")]
    [InlineData("tempo")]
    [InlineData("summary")]
    [InlineData("incoming-summary")]
    [InlineData("player-cost")]
    [InlineData("opposition-cost")]
    [InlineData("dice-total-shape")]
    [InlineData("exchange-id")]
    [InlineData("turn-marker")]
    public void PairPressureDataRejectsChangedCompleteOrdinarySiblings(string change)
    {
        var source = SupportedPairSource();
        var ordinary = PairOrdinary(source);
        switch (change)
        {
            case "before": ordinary["before"]!["playerSideStrain"] = "strained"; break;
            case "opposition-strain": ordinary["after"]!["oppositionSideStrain"] = "strained"; break;
            case "position": ordinary["after"]!["conflictPosition"] = "opposition_advantaged"; break;
            case "control":
                ordinary["before"]!["controlState"] = new JsonObject { ["level"] = "none" };
                ordinary["after"]!["controlState"] = new JsonObject { ["level"] = "none" };
                break;
            case "tempo":
                var expired = new JsonObject { ["status"] = "expired", ["level"] = "advantage",
                    ["ownerSide"] = "player", ["sourceOperation"] = "guard", ["advantageId"] = "prior_tempo",
                    ["summary"] = "Предыдущее преимущество уже истекло." };
                ordinary["before"]!["tempoAdvantage"] = expired.DeepClone();
                ordinary["after"]!["tempoAdvantage"] = expired;
                break;
            case "summary": ordinary["summary"] = "Иное полное описание."; break;
            case "incoming-summary": ordinary["incomingAction"]!["summary"] = "Иное входящее действие."; break;
            case "player-cost": ordinary["actionCostAudit"]!["player"]!["max"] = 11; break;
            case "opposition-cost": ordinary["actionCostAudit"]!["opposition"]!["max"] = 10; break;
            case "dice-total-shape": ordinary["diceAudit"]!["oppositionTotal"] = 13; break;
            case "exchange-id": ordinary["exchangeId"] = "exchange_foreign"; break;
            case "turn-marker": ordinary["turnNumber"] = 45; break;
            default: throw new ArgumentOutOfRangeException(nameof(change));
        }
        if (change is "control" or "tempo" or "summary" or "incoming-summary" or "player-cost" or "opposition-cost")
            AssertValidPairOrdinaryShape(ordinary, source);
        PairProof(source)["ordinaryExchangeJsonBase64"] = SpiritualPressurePairTestData.Encode(ordinary);
        AssertPairRejected(source);
    }

    /// <summary>Never exposes provisional DATA after actual, ordinary or second-parent refusal, including convenience reads.</summary>
    [Theory]
    [InlineData("actual-margin")]
    [InlineData("actual-maximum")]
    [InlineData("actual-claim")]
    [InlineData("ordinary-calculation")]
    [InlineData("second-parent")]
    public void PairPressureDataWithholdsDataAfterFailedWitness(string change)
    {
        var source = SupportedPairSource(out var previouslyRetained);
        switch (change)
        {
            case "actual-margin": source["harmfulMargin"] = 9; break;
            case "actual-maximum": source["maximumSeverityRank"] = 3; break;
            case "actual-claim": source["turnEvidence"]!["diceClaims"]![1]!["value"] = 13; break;
            case "ordinary-calculation": PairProof(source)["ordinaryCalculation"]!["traumaPressure"] = 14L; break;
            case "second-parent": PairProof(source)["parents"]![1]!["componentId"] = "component_foreign"; break;
            default: throw new ArgumentOutOfRangeException(nameof(change));
        }
        AssertPairRejected(source, previouslyRetained);
        Assert.Throws<FormatException>(() => SpiritualStrainPreimageData.ReadIntrinsic(source, PairActual(source)));
    }

    /// <summary>A supported v4 baseline is mandatory outside every negative's expected-refusal assertion.</summary>
    private static JsonObject SupportedPairSource(string ordinaryStrain = "fractured") =>
        SupportedPairSource(out _, ordinaryStrain);

    private static JsonObject SupportedPairSource(out SpiritualStrainPreimageData data, string ordinaryStrain = "fractured")
    {
        var source = SpiritualPressurePairTestData.CreateSource(ordinaryStrain);
        var before = source.ToJsonString();
        var calculation = SpiritualWoundSourceWitness.ValidateAndReadStrainData(source, out var retained);
        Assert.Equal((23L, 4), (calculation.TraumaPressure, calculation.MaximumSeverityRank));
        data = Assert.IsType<SpiritualStrainPreimageData>(retained);
        Assert.Equal(4, data.Version);
        var ordinary = Assert.IsType<SpiritualWoundCalculation>(data.OrdinaryCalculation);
        Assert.Equal(ordinaryStrain == "fractured" ? (13L, 2) : (18L, 3),
            (ordinary.TraumaPressure, ordinary.MaximumSeverityRank));
        Assert.True(JsonNode.DeepEquals(PairActual(source), data.ReadActualExchange()));
        Assert.Equal(before, source.ToJsonString());
        return source;
    }

    private static JsonObject PairProof(JsonObject source) => source["strainPreimage"]!.AsObject();
    private static JsonObject PairActual(JsonObject source) => PairDecode(source["exchangeJsonBase64"]);
    private static JsonObject PairOrdinary(JsonObject source) => PairDecode(PairProof(source)["ordinaryExchangeJsonBase64"]);
    private static JsonObject PairParent(JsonObject source, int index) =>
        PairDecode(PairProof(source)["parents"]![index]!["parentWoundJsonBase64"]);
    private static JsonObject PairDecode(JsonNode? encoded) => SpiritualWoundStateJson.Parse(
        SpiritualWoundStateJson.DecodeUtf8JsonText(SpiritualWoundStateJson.DecodeJsonObjectBytes(encoded)));
    private static void SetPairParent(JsonObject source, int index, JsonObject parent) =>
        PairProof(source)["parents"]![index]!["parentWoundJsonBase64"] = SpiritualPressurePairTestData.Encode(parent);

    private static void AssertPairRejected(JsonObject source, SpiritualStrainPreimageData? previouslyRetained = null)
    {
        SpiritualPressurePairTestData.Seal(source);
        var before = source.ToJsonString();
        SpiritualStrainPreimageData? retained = previouslyRetained;
        Assert.Throws<FormatException>(() => SpiritualWoundSourceWitness.ValidateAndReadStrainData(source, out retained));
        Assert.Null(retained);
        Assert.Equal(before, source.ToJsonString());
    }

    private static void AssertValidPairParent(JsonObject parent)
    {
        var parsed = WoundMaterializationContract.Parse(parent.ToJsonString(), "unit.pairPressure.parentControl");
        Assert.True(parsed.IsValid, string.Join("\n", parsed.Issues));
    }

    private static (long Trauma, int Maximum) PressurePairMath(JsonObject source)
    {
        var calculation = SpiritualWoundSourceWitness.Validate(source);
        return (calculation.TraumaPressure, calculation.MaximumSeverityRank);
    }

    private static void AssertValidPairOrdinaryShape(JsonObject ordinary, JsonObject source)
    {
        var issues = new List<ValidationIssue>();
        ValidationService.ValidateRetainedSpiritualExchangeShape(ordinary, "unit.pairPressure.ordinary", issues);
        ValidationService.ValidateSpiritualWoundSourceActionShape(ordinary, "unit.pairPressure.ordinary", issues);
        ValidationService.ValidateRetainedSpiritualDiceAudit(ordinary, source["turnEvidence"]!.AsObject(), issues);
        ValidationService.ValidateRetainedSpiritualExchangeRules(ordinary, issues);
        Assert.DoesNotContain(issues, issue => issue.Severity == IssueSeverity.Error);
    }

    /// <summary>Splits lawful nonselected siblings to expose a cross-parent collision outside the selected root.</summary>
    private static void SplitPairSiblingRoot(JsonObject parent, string siblingEffectId)
    {
        var graph = parent["consequences"]!["ownedEffectSources"]!.AsObject();
        var definition = graph["definitions"]![0]!.AsObject();
        var sibling = definition.DeepClone().AsObject();
        sibling["definitionKey"] = "definition_pressure_view_B_sibling";
        sibling["stacking"]!["stackKey"] = "stack_definition_pressure_view_B_sibling";
        sibling["components"] = new JsonArray(definition["components"]![1]!.DeepClone(), definition["components"]![2]!.DeepClone());
        sibling["triggers"]![0]!["componentIds"] = new JsonArray("component_002", "component_003");
        definition["components"] = new JsonArray(definition["components"]![0]!.DeepClone());
        definition["triggers"]![0]!["componentIds"] = new JsonArray("component_001");
        graph["definitions"]!.AsArray().Add(sibling);
        graph["rootBindings"]!.AsArray().Add(WoundContractTestData.CreateRootBinding(
            siblingEffectId, "definition_pressure_view_B_sibling"));
        parent["consequences"]!["entries"]![1]!["effectId"] = siblingEffectId;
        parent["consequences"]!["entries"]![2]!["effectId"] = siblingEffectId;
    }
}
