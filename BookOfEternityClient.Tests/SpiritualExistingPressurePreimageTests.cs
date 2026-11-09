using System.Text;
using System.Text.Json;
using System.Text.Json.Nodes;
using BookOfEternityClient.Services;
using Xunit;

namespace BookOfEternityClient.Tests;

/// <summary>Checks closed v3 comparison DATA, never accepted provenance or live owner authority.</summary>
public sealed class SpiritualExistingPressurePreimageTests
{
    private const string ProofFields = "version inputRoleRevision ordinaryExchangeJsonBase64 originalInput parentWoundJsonBase64 effectId componentId ordinaryCalculation";
    private const string InputFields = "path existed contentFingerprint exchangePointer";
    private const string CalculationFields = "traumaPressure maximumSeverityRank";

    /// <summary>Requires a complete pressure comparison whose ordinary result differs from before and actual.</summary>
    [Fact]
    public void PressureDataRetainsIndependentOrdinaryAndActualCalculations()
    {
        var source = CreateData();
        AssertFixturePrerequisites(source);
        var before = source.ToJsonString();
        var calculated = SpiritualWoundSourceWitness.ValidateAndReadStrainData(source, out var retained);
        Assert.Equal((15L, 2), (calculated.TraumaPressure, calculated.MaximumSeverityRank));
        var data = Assert.IsType<SpiritualStrainPreimageData>(retained);
        Assert.Equal(3, data.Version);
        Assert.Null(data.ParentSourceId);
        Assert.Equal((3, "active", "player_soul"),
            (data.ParentWound.Severity.Rank, data.ParentWound.Lifecycle, data.ParentWound.Owner.OwnerId));
        Assert.Equal("effect_pressure_strain", data.EffectId);
        Assert.Equal("component_001", data.ComponentId);
        var ordinaryCalculation = Assert.IsType<SpiritualWoundCalculation>(data.OrdinaryCalculation);
        Assert.Equal((10L, 1), (ordinaryCalculation.TraumaPressure, ordinaryCalculation.MaximumSeverityRank));
        Assert.Equal(("strained", "fractured"), (ordinaryCalculation.Input.PreviousStrain, ordinaryCalculation.Input.NewStrain));
        Assert.Same(calculated, data.ActualCalculation);
        Assert.Equal(source["strainPreimage"]!["originalInput"]!["contentFingerprint"]!.GetValue<string>(),
            data.OriginalInputFingerprint);
        Assert.Equal("/activeConflict/exchangeLog/2", data.OriginalExchangePointer);
        Assert.Equal("fractured", data.ReadOrdinaryAfter(ReadActual(source))["playerSideStrain"]!.GetValue<string>());
        Assert.True(JsonNode.DeepEquals(ReadActual(source), data.ReadActualExchange()));
        Assert.Equal(before, source.ToJsonString());
    }

    /// <summary>The former IV-only refusal is lawful existing DATA, including its separate pressure-roll sibling.</summary>
    [Fact]
    public void PressureDataAcceptsCompleteExistingRankFourGraph()
    {
        var source = CreateSupportedData();
        var parent = SpiritualExistingStrainPreimageTests.CreateData(4).Parent;
        parent["consequences"]!["ownedEffectSources"]!["definitions"]![0]!["components"]![0]!["payload"]!["operation"] = "pressure";
        AssertValidParent(parent);
        Assert.Equal(4, parent["consequences"]!["entries"]!.AsArray().Count);
        var rawParent = parent.ToJsonString();
        var expectedParent = parent.DeepClone().AsObject();
        var expectedGraph = expectedParent["consequences"]!["ownedEffectSources"]!.AsObject();
        var definitions = expectedGraph["definitions"]!.AsArray();
        Assert.Equal(new[] { "definition_parent_strain", "definition_parent_roll", "definition_parent_cost", "definition_parent_extra" },
            definitions.Select(definition => definition!["definitionKey"]!.GetValue<string>()));
        var roots = expectedGraph["rootBindings"]!.AsArray();
        Assert.Equal(new[] { ("effect_parent_strain", "definition_parent_strain"), ("effect_parent_roll", "definition_parent_roll"),
                ("effect_parent_cost", "definition_parent_cost"), ("effect_parent_extra", "definition_parent_extra") },
            roots.Select(root => (root!["effectId"]!.GetValue<string>(), root!["definitionKey"]!.GetValue<string>())));
        // Expected canonical structure is derived independently from the raw fixture, not the production serializer.
        expectedGraph["definitions"] = new JsonArray(definitions
            .OrderBy(definition => definition!["definitionKey"]!.GetValue<string>(), StringComparer.Ordinal)
            .Select(definition => definition!.DeepClone()).ToArray());
        expectedGraph["rootBindings"] = new JsonArray(roots
            .OrderBy(root => root!["effectId"]!.GetValue<string>(), StringComparer.Ordinal)
            .ThenBy(root => root!["definitionKey"]!.GetValue<string>(), StringComparer.Ordinal)
            .Select(root => root!.DeepClone()).ToArray());
        var expectedRecovery = expectedParent["recovery"]!.AsObject();
        Assert.False(expectedRecovery.ContainsKey("recoveryAnchor"));
        Assert.False(expectedRecovery.ContainsKey("deteriorationAnchor"));
        expectedRecovery["recoveryAnchor"] = null;
        expectedRecovery["deteriorationAnchor"] = null;
        Assert.Equal(rawParent, parent.ToJsonString());
        source["strainPreimage"]!["parentWoundJsonBase64"] = Encode(parent);
        source["strainPreimage"]!["effectId"] = "effect_parent_strain";
        Seal(source);
        var before = source.ToJsonString();
        var actual = SpiritualWoundSourceWitness.ValidateAndReadStrainData(source, out var retained);
        var data = Assert.IsType<SpiritualStrainPreimageData>(retained);
        Assert.Equal((3, 4, 15L, 2, 10L, 1), (data.Version, data.ParentWound.Severity.Rank,
            actual.TraumaPressure, actual.MaximumSeverityRank, data.OrdinaryCalculation!.TraumaPressure,
            data.OrdinaryCalculation.MaximumSeverityRank));
        var actualParentJson = WoundMaterializationContract.SerializeCanonical(data.ParentWound);
        Assert.True(JsonNode.DeepEquals(expectedParent, JsonNode.Parse(actualParentJson)),
            $"Expected parent JSON: {expectedParent.ToJsonString()}\nActual parent JSON: {actualParentJson}");
        Assert.Equal(rawParent, parent.ToJsonString());
        Assert.True(JsonNode.DeepEquals(parent, ReadParent(source)));
        Assert.Equal(4, SpiritualStrainPreimageData.ReadIntrinsic(source, ReadActual(source)).ParentWound.Severity.Rank);
        Assert.Equal(before, source.ToJsonString());
    }

    /// <summary>Preserves both old closed wire branches without adding pressure fields or reinterpreting their payloads.</summary>
    [Theory]
    [InlineData(1)]
    [InlineData(2)]
    public void PressureDataPreservesExactVersionOneAndTwoControls(int version)
    {
        var source = version == 1 ? SpiritualStrainPreimageDataTests.CreateData().Source :
            SpiritualExistingStrainPreimageTests.CreateData(3).Source;
        var before = source.ToJsonString();
        Assert.Equal(2, SpiritualWoundSourceWitness.ValidateAndReadStrainData(source, out var data).MaximumSeverityRank);
        Assert.Equal(version, Assert.IsType<SpiritualStrainPreimageData>(data).Version);
        Assert.Null(data!.OrdinaryCalculation);
        Assert.Null(data.ActualCalculation);
        Assert.Null(data.OriginalInputFingerprint);
        Assert.Null(data.OriginalExchangePointer);
        Assert.Equal(before, source.ToJsonString());
    }

    /// <summary>Requires each of the eight proof, four original-input and two calculation fields, and no extensions.</summary>
    [Theory]
    [MemberData(nameof(ClosedFieldCases))]
    public void PressureDataRejectsMissingAndExtraClosedFields(string group, string field)
    {
        var source = CreateSupportedData();
        var target = ReadGroup(source, group);
        if (field == "+unowned") target["unowned"] = true;
        else Assert.True(target.Remove(field));
        AssertRejectedAfterReseal(source);
    }

    /// <summary>Rejects duplicate properties before an owning wire parser can turn them into a JsonObject.</summary>
    [Theory]
    [MemberData(nameof(DuplicateFieldCases))]
    public void PressureDataRejectsDuplicateClosedFields(string group, string field)
    {
        var source = CreateSupportedData();
        var fragment = ReadGroup(source, group).ToJsonString();
        var property = JsonSerializer.Serialize(field) + ":";
        var duplicate = fragment.Replace(property, property + "null," + property, StringComparison.Ordinal);
        Assert.NotEqual(fragment, duplicate);
        var json = source.ToJsonString().Replace(fragment, duplicate, StringComparison.Ordinal);
        Assert.Throws<FormatException>(() => SpiritualWoundSourceWitness.Validate(SpiritualWoundStateJson.Parse(json)));
    }

    /// <summary>Rejects wrong JSON scalar kinds, range errors and malformed closed identifiers after valid v3 control.</summary>
    [Theory]
    [MemberData(nameof(ScalarCases))]
    public void PressureDataRejectsWrongScalarKindsAndValues(string group, string field, string json)
    {
        var source = CreateSupportedData();
        ReadGroup(source, group)[field] = JsonNode.Parse(json);
        AssertRejectedAfterReseal(source);
    }

    /// <summary>Requires canonical direct-root pointers instead of aliases, escaped segments or nondecimal indices.</summary>
    [Theory]
    [InlineData("/activeConflict/exchangeLog/02")]
    [InlineData("/activeConflict/exchangeLog/-1")]
    [InlineData("/activeConflict/exchangeLog/+2")]
    [InlineData("/activeConflict/exchangeLog/2.0")]
    [InlineData("/activeConflict/exchangeLog/2/")]
    [InlineData("activeConflict.exchangeLog[2]")]
    [InlineData("/activeConflict/exchangeLog/~02")]
    [InlineData("/activeConflict/exchangeLog/2/after")]
    [InlineData("/afterlifeSpiritualConflictUpdate/exchange")]
    [InlineData("/activeConflict/exchangeLog/")]
    public void PressureDataRejectsMalformedOriginalExchangePointers(string pointer)
    {
        var source = CreateSupportedData();
        source["strainPreimage"]!["originalInput"]!["exchangePointer"] = pointer;
        AssertRejectedAfterReseal(source);
    }

    /// <summary>Rejects invalid UTF-8/base64, nonobjects and recursive duplicates in either complete embedded payload.</summary>
    [Theory]
    [MemberData(nameof(EmbeddedPayloadCases))]
    public void PressureDataRejectsMalformedEmbeddedObjects(string field, string change)
    {
        var source = CreateSupportedData();
        var proof = source["strainPreimage"]!.AsObject();
        var original = proof[field]!.GetValue<string>();
        proof[field] = change switch
        {
            "base64" => "!not-base64!",
            "base64-whitespace" => original + "\n",
            "utf8" => Convert.ToBase64String(new byte[] { 0x7b, 0x22, 0xc3, 0x28, 0x22, 0x3a, 0x31, 0x7d }),
            "array" => EncodeText("[]"),
            "null" => EncodeText("null"),
            "duplicate-root" => EncodeText("{\"duplicate\":1,\"duplicate\":2}"),
            "duplicate-nested" => EncodeText("{\"nested\":[{\"duplicate\":1,\"duplicate\":2}]}"),
            _ => throw new ArgumentOutOfRangeException(nameof(change))
        };
        AssertRejectedAfterReseal(source);
    }

    /// <summary>Rejects a resealed intrinsic contradiction, retaining all siblings except the one intended strain step.</summary>
    [Theory]
    [MemberData(nameof(SemanticCases))]
    public void PressureDataRejectsRehashedIntrinsicContradictions(string change)
    {
        var source = CreateSupportedData();
        var ordinary = ReadOrdinary(source);
        var actual = ReadActual(source);
        var parent = ReadParent(source);
        var proof = source["strainPreimage"]!.AsObject();
        switch (change)
        {
            case "parent-owner": parent["owner"]!["ownerId"] = "foreign_soul"; break;
            case "parent-realm": parent["owner"]!["realm"] = "shining_abode"; break;
            case "parent-terminal": parent["lifecycle"] = "healed"; break;
            case "parent-future": parent["lastTransition"]!["turn"] = 45; break;
            case "foreign-actor": source["affectedActor"]!["actorId"] = "foreign_soul"; break;
            case "foreign-realm": source["turnEvidence"]!["realm"] = "shining_abode"; break;
            case "foreign-root": proof["effectId"] = "effect_foreign"; break;
            case "foreign-component": proof["componentId"] = "component_foreign"; break;
            case "wrong-parent-operation": parent["consequences"]!["ownedEffectSources"]!["definitions"]![0]!["components"]![0]!["payload"]!["operation"] = "maneuver"; break;
            case "wrong-parent-magnitude": parent["consequences"]!["ownedEffectSources"]!["definitions"]![0]!["components"]![0]!["payload"]!["magnitude"] = 2; break;
            case "actual-contradictory-training-ceiling":
                source["dangerMode"] = "training"; source["maximumSeverityRank"] = 1;
                proof["ordinaryCalculation"]!["maximumSeverityRank"] = 0;
                break;
            case "wrong-operation": ordinary["operationType"] = "maneuver"; actual["operationType"] = "maneuver"; break;
            case "wrong-outcome": ordinary["outcome"] = "no_effect"; actual["outcome"] = "no_effect"; break;
            case "wrong-incoming": ordinary["incomingAction"]!["operationType"] = "guard"; actual["incomingAction"]!["operationType"] = "guard"; break;
            case "ordinary-reset-to-before": ordinary["after"]!["playerSideStrain"] = "strained"; break;
            case "ordinary-equals-actual": ordinary["after"]!["playerSideStrain"] = "overwhelmed"; break;
            case "actual-no-step": actual["after"]!["playerSideStrain"] = "fractured"; break;
            case "actual-capped": actual["after"]!["playerSideStrain"] = "broken"; break;
            case "ordinary-before": ordinary["before"]!["playerSideStrain"] = "clear"; break;
            case "ordinary-opposition": ordinary["after"]!["oppositionSideStrain"] = "strained"; break;
            case "ordinary-position": ordinary["after"]!["conflictPosition"] = "opposition_advantaged"; break;
            case "ordinary-control": ordinary["after"]!["controlState"] = new JsonObject { ["level"] = "none" }; break;
            case "ordinary-tempo": ordinary["after"]!.AsObject().Remove("tempoAdvantage"); break;
            case "ordinary-summary": ordinary["summary"] = "Иное описание обычного обмена."; break;
            case "ordinary-incoming-summary": ordinary["incomingAction"]!["summary"] = "Иное описание входящего действия."; break;
            case "both-ordinary-summaries":
                ordinary["summary"] = "Иное описание обычного обмена.";
                ordinary["incomingAction"]!["summary"] = "Иное описание входящего действия.";
                break;
            case "ordinary-cost": ordinary["actionCostAudit"]!["player"]!["max"] = 11; break;
            case "ordinary-dice": ordinary["diceAudit"]!["margin"] = -7; break;
            case "ordinary-exchange-id": ordinary["exchangeId"] = "exchange_foreign"; break;
            case "ordinary-turn": ordinary["turnNumber"] = 43; break;
            case "ordinary-missing-snapshot": ordinary.Remove("before"); break;
            case "ordinary-calculation-pressure": proof["ordinaryCalculation"]!["traumaPressure"] = 15; break;
            case "ordinary-calculation-maximum": proof["ordinaryCalculation"]!["maximumSeverityRank"] = 2; break;
            default: throw new ArgumentOutOfRangeException(nameof(change));
        }
        proof["ordinaryExchangeJsonBase64"] = Encode(ordinary);
        proof["parentWoundJsonBase64"] = Encode(parent);
        source["exchangeJsonBase64"] = Encode(actual);
        AssertRejectedAfterReseal(source);
    }

    /// <summary>Returns detached snapshots and refuses an actual exchange different from the one checked.</summary>
    [Fact]
    public void PressureDataDetachesBothSnapshotsAndRetainsExactActualBinding()
    {
        var source = CreateSupportedData();
        var actual = ReadActual(source);
        var retained = SpiritualStrainPreimageData.ReadIntrinsic(source, actual);
        retained.ReadOrdinaryAfter(actual)["playerSideStrain"] = "clear";
        var detached = retained.ReadActualExchange();
        detached["summary"] = "Изменённая копия.";
        Assert.Equal("fractured", retained.ReadOrdinaryAfter(actual)["playerSideStrain"]!.GetValue<string>());
        Assert.True(JsonNode.DeepEquals(actual, retained.ReadActualExchange()));
        Assert.Throws<FormatException>(() => retained.ReadOrdinaryAfter(detached));
        actual["after"]!["playerSideStrain"] = "broken";
        Assert.Equal("overwhelmed", retained.ReadActualExchange()["after"]!["playerSideStrain"]!.GetValue<string>());
        source["strainPreimage"]!["ordinaryExchangeJsonBase64"] = EncodeText("{}");
        Assert.Equal("fractured", retained.ReadOrdinaryAfter(retained.ReadActualExchange())["playerSideStrain"]!.GetValue<string>());
    }

    /// <summary>Withholds v3 DATA until every outer actual-source check succeeds, including the direct convenience read.</summary>
    [Theory]
    [InlineData("maximumSeverityRank", 1)]
    [InlineData("harmfulMargin", 9)]
    public void PressureDataNeverEscapesFailedActualWitnessValidation(string field, int value)
    {
        var source = CreateSupportedData();
        source[field] = value;
        Seal(source);
        var before = source.ToJsonString();
        SpiritualStrainPreimageData? retained = null;
        Assert.Throws<FormatException>(() => SpiritualWoundSourceWitness.ValidateAndReadStrainData(source, out retained));
        Assert.Null(retained);
        Assert.Throws<FormatException>(() => SpiritualStrainPreimageData.ReadIntrinsic(source, ReadActual(source)));
        Assert.Equal(before, source.ToJsonString());
    }

    /// <summary>Does not retain provisional actual DATA when the later ordinary comparison fails.</summary>
    [Fact]
    public void PressureDataNeverEscapesFailedOrdinaryValidation()
    {
        var source = CreateSupportedData();
        source["strainPreimage"]!["ordinaryCalculation"]!["traumaPressure"] = 15L;
        Seal(source);
        var before = source.ToJsonString();
        SpiritualStrainPreimageData? retained = null;
        Assert.Throws<FormatException>(() => SpiritualWoundSourceWitness.ValidateAndReadStrainData(source, out retained));
        Assert.Null(retained);
        Assert.Equal(before, source.ToJsonString());
    }

    /// <summary>Enumerates every closed v3 field and one unsupported extension per object.</summary>
    public static IEnumerable<object[]> ClosedFieldCases() => WireFields(includeExtra: true);

    /// <summary>Enumerates every closed v3 property for duplicate rejection by the owning parser.</summary>
    public static IEnumerable<object[]> DuplicateFieldCases() => WireFields(includeExtra: false);

    /// <summary>Enumerates strict scalar, literal, identity and comparison-value refusals.</summary>
    public static IEnumerable<object[]> ScalarCases()
    {
        foreach (var json in new[] { "null", "\"3\"", "3.0", "true", "{}", "0", "4", "9223372036854775808" })
            yield return ["proof", "version", json];
        foreach (var field in new[] { "inputRoleRevision", "ordinaryExchangeJsonBase64", "parentWoundJsonBase64", "effectId", "componentId" })
            foreach (var json in new[] { "null", "3", "{}", "\"\"" }) yield return ["proof", field, json];
        yield return ["proof", "inputRoleRevision", "\"existing_pressure_ordinary_v2\""];
        foreach (var field in new[] { "originalInput", "ordinaryCalculation" })
            foreach (var json in new[] { "null", "\"object\"", "[]" }) yield return ["proof", field, json];
        foreach (var json in new[] { "null", "3", "\"game_state/meta/foreign.json\"" }) yield return ["input", "path", json];
        foreach (var json in new[] { "null", "\"true\"", "1", "false" }) yield return ["input", "existed", json];
        foreach (var json in new[] { "null", "true", "\"not-a-fingerprint\"" }) yield return ["input", "contentFingerprint", json];
        foreach (var json in new[] { "null", "2" }) yield return ["input", "exchangePointer", json];
        foreach (var json in new[] { "null", "\"10\"", "10.0", "true", "9223372036854775808", "-9223372036854775809", "-1" })
            yield return ["calculation", "traumaPressure", json];
        foreach (var json in new[] { "null", "\"1\"", "1.0", "true", "-1", "5", "9223372036854775808" })
            yield return ["calculation", "maximumSeverityRank", json];
    }

    /// <summary>Enumerates malformed encodings and embedded object syntax for both independent payloads.</summary>
    public static IEnumerable<object[]> EmbeddedPayloadCases()
    {
        foreach (var field in new[] { "ordinaryExchangeJsonBase64", "parentWoundJsonBase64" })
            foreach (var change in new[] { "base64", "base64-whitespace", "utf8", "array", "null", "duplicate-root", "duplicate-nested" })
                yield return [field, change];
    }

    /// <summary>Enumerates contradictions inside a completely resealed lexical witness, not provenance forgeries.</summary>
    public static IEnumerable<object[]> SemanticCases()
    {
        foreach (var change in new[] { "parent-owner", "parent-realm", "parent-terminal", "parent-future",
                     "foreign-actor", "foreign-realm", "foreign-root", "foreign-component", "wrong-parent-operation",
                     "wrong-parent-magnitude", "actual-contradictory-training-ceiling", "wrong-operation", "wrong-outcome", "wrong-incoming",
                     "ordinary-reset-to-before", "ordinary-equals-actual", "actual-no-step", "actual-capped", "ordinary-before",
                     "ordinary-opposition", "ordinary-position", "ordinary-control", "ordinary-tempo", "ordinary-summary",
                     "ordinary-incoming-summary", "both-ordinary-summaries", "ordinary-cost", "ordinary-dice", "ordinary-exchange-id",
                     "ordinary-turn", "ordinary-missing-snapshot", "ordinary-calculation-pressure", "ordinary-calculation-maximum" })
            yield return [change];
    }

    /// <summary>Creates independent complete pressure DATA. Arbitrary retained hashes confer no provenance or authority.</summary>
    internal static JsonObject CreateData() => SpiritualExistingPressureTestData.CreateData();

    /// <summary>Checks complete independent ordinary and actual source DATA before asking for the new v3 branch.</summary>
    internal static void AssertFixturePrerequisites(JsonObject source)
    {
        AssertValidParent(ReadParent(source));
        var ordinary = ReadOrdinary(source);
        var actual = ReadActual(source);
        Assert.Equal("pressure", ordinary["operationType"]!.GetValue<string>());
        Assert.Equal("setback", ordinary["outcome"]!.GetValue<string>());
        Assert.Equal("strained", ordinary["before"]!["playerSideStrain"]!.GetValue<string>());
        Assert.Equal("fractured", ordinary["after"]!["playerSideStrain"]!.GetValue<string>());
        Assert.Equal("overwhelmed", actual["after"]!["playerSideStrain"]!.GetValue<string>());
        var comparison = actual.DeepClone().AsObject();
        comparison["after"]!["playerSideStrain"] = "fractured";
        Assert.True(JsonNode.DeepEquals(ordinary, comparison));
        var actualOnly = source.DeepClone().AsObject(); actualOnly.Remove("strainPreimage"); Seal(actualOnly);
        var actualMath = SpiritualWoundSourceWitness.Validate(actualOnly);
        Assert.Equal((15L, 2), (actualMath.TraumaPressure, actualMath.MaximumSeverityRank));
        var ordinaryOnly = actualOnly.DeepClone().AsObject();
        ordinaryOnly["exchangeJsonBase64"] = Encode(ordinary); ordinaryOnly["destinationStrainRank"] = 2;
        ordinaryOnly["extraStrainJumps"] = 0; ordinaryOnly["maximumSeverityRank"] = 1; Seal(ordinaryOnly);
        var ordinaryMath = SpiritualWoundSourceWitness.Validate(ordinaryOnly);
        Assert.Equal((10L, 1), (ordinaryMath.TraumaPressure, ordinaryMath.MaximumSeverityRank));
    }

    /// <summary>Copies the complete intrinsic actual payload without claiming its accepted membership.</summary>
    internal static JsonObject ReadActual(JsonObject source) => DecodeObject(source["exchangeJsonBase64"]);
    /// <summary>Copies the complete proposed ordinary payload.</summary>
    internal static JsonObject ReadOrdinary(JsonObject source) => DecodeObject(source["strainPreimage"]!["ordinaryExchangeJsonBase64"]);
    /// <summary>Copies the retained parent DATA without authenticating the original History transition.</summary>
    internal static JsonObject ReadParent(JsonObject source) => DecodeObject(source["strainPreimage"]!["parentWoundJsonBase64"]);
    /// <summary>Reseals lexical source DATA so semantic controls cannot pass merely through a stale outer digest.</summary>
    internal static void Seal(JsonObject source)
    {
        var digest = SpiritualWoundStateJson.Hash(source, "source", "sourceId", "sourceFingerprint");
        source["sourceFingerprint"] = digest; source["sourceId"] = "spiritual_source_" + digest[7..];
    }

    private static JsonObject CreateSupportedData()
    {
        var source = CreateData();
        AssertFixturePrerequisites(source);
        Assert.Equal(2, SpiritualWoundSourceWitness.ValidateAndReadStrainData(source, out var retained).MaximumSeverityRank);
        Assert.Equal(3, Assert.IsType<SpiritualStrainPreimageData>(retained).Version);
        return source;
    }



    private static void AssertValidParent(JsonObject parent)
    {
        var parsed = WoundMaterializationContract.Parse(parent.ToJsonString(), "unit.pressureParent");
        Assert.True(parsed.IsValid, string.Join("\n", parsed.Issues));
    }

    private static void AssertRejectedAfterReseal(JsonObject source)
    {
        Seal(source);
        var before = source.ToJsonString();
        Assert.Throws<FormatException>(() => SpiritualWoundSourceWitness.Validate(source));
        Assert.Equal(before, source.ToJsonString());
    }

    private static IEnumerable<object[]> WireFields(bool includeExtra)
    {
        foreach (var (group, fields) in new[] { ("proof", ProofFields), ("input", InputFields), ("calculation", CalculationFields) })
        {
            foreach (var field in fields.Split(' ')) yield return [group, field];
            if (includeExtra) yield return [group, "+unowned"];
        }
    }

    private static JsonObject ReadGroup(JsonObject source, string group) => group switch
    {
        "proof" => source["strainPreimage"]!.AsObject(),
        "input" => source["strainPreimage"]!["originalInput"]!.AsObject(),
        "calculation" => source["strainPreimage"]!["ordinaryCalculation"]!.AsObject(),
        _ => throw new ArgumentOutOfRangeException(nameof(group))
    };
    private static JsonObject DecodeObject(JsonNode? node) => SpiritualWoundStateJson.Parse(
        SpiritualWoundStateJson.DecodeUtf8JsonText(SpiritualWoundStateJson.DecodeJsonObjectBytes(node)));
    private static string Encode(JsonNode node) => EncodeText(SpiritualWoundStateJson.Canonical(node));
    private static string EncodeText(string text) => Convert.ToBase64String(Encoding.UTF8.GetBytes(text));
}
