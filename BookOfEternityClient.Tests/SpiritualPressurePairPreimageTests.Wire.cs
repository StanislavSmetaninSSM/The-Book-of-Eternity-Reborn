using System.Text;
using System.Text.Json;
using System.Text.Json.Nodes;
using BookOfEternityClient.Services;
using Xunit;

namespace BookOfEternityClient.Tests;

public sealed partial class SpiritualPressurePairPreimageTests
{
    /// <summary>Requires every field of all five closed v4 objects and rejects extensions after supported baseline success.</summary>
    [Theory]
    [MemberData(nameof(PairClosedFieldCases))]
    public void PairPressureDataRejectsMissingOrExtraClosedFields(string scope, string field)
    {
        var source = SupportedPairSource();
        var target = PairWireObject(source, scope);
        if (field == "+unowned") target["unowned"] = true;
        else Assert.True(target.Remove(field));
        AssertPairRejected(source);
    }

    /// <summary>Tests duplicate properties in actual serialized input, never impossible duplicate JsonObject keys.</summary>
    [Theory]
    [MemberData(nameof(PairDuplicateFieldCases))]
    public void PairPressureDataRejectsDuplicateClosedFields(string scope, string field)
    {
        var source = SupportedPairSource();
        var fragment = PairWireObject(source, scope).ToJsonString();
        var property = JsonSerializer.Serialize(field) + ":";
        var duplicate = fragment.Replace(property, property + "null," + property, StringComparison.Ordinal);
        Assert.NotEqual(fragment, duplicate);
        var json = source.ToJsonString().Replace(fragment, duplicate, StringComparison.Ordinal);
        Assert.Throws<FormatException>(() => SpiritualWoundStateJson.Parse(json));
    }

    /// <summary>Rejects unsupported kinds, roles and closed scalar values without mistaking the old version fence for coverage.</summary>
    [Theory]
    [MemberData(nameof(PairScalarCases))]
    public void PairPressureDataRejectsWrongWireKindsAndValues(string scope, string field, string json)
    {
        var source = SupportedPairSource();
        if (scope == "parents") PairProof(source)["parents"]![int.Parse(field)] = JsonNode.Parse(json);
        else PairWireObject(source, scope)[field] = JsonNode.Parse(json);
        AssertPairRejected(source);
    }

    /// <summary>Retains canonical direct-root raw-input comparators, not aliases or malformed index syntax.</summary>
    [Theory]
    [InlineData("/activeConflict/exchangeLog/00")]
    [InlineData("/activeConflict/exchangeLog/-1")]
    [InlineData("/activeConflict/exchangeLog/+0")]
    [InlineData("/activeConflict/exchangeLog/0.0")]
    [InlineData("/activeConflict/exchangeLog/0/")]
    [InlineData("/activeConflict/exchangeLog/~00")]
    [InlineData("activeConflict.exchangeLog[0]")]
    [InlineData("/activeConflict/exchangeLog/0/after")]
    public void PairPressureDataRejectsMalformedOriginalPointers(string pointer)
    {
        var source = SupportedPairSource();
        PairProof(source)["originalInput"]!["exchangePointer"] = pointer;
        AssertPairRejected(source);
    }

    /// <summary>Rejects malformed encoding and embedded object syntax for ordinary and each complete parent.</summary>
    [Theory]
    [MemberData(nameof(PairEmbeddedPayloadCases))]
    public void PairPressureDataRejectsMalformedEmbeddedObjects(string payload, string change)
    {
        var source = SupportedPairSource();
        var target = payload == "ordinary" ? PairProof(source) : PairProof(source)["parents"]![payload == "parentA" ? 0 : 1]!.AsObject();
        var field = payload == "ordinary" ? "ordinaryExchangeJsonBase64" : "parentWoundJsonBase64";
        var encoded = target[field]!.GetValue<string>();
        target[field] = change switch
        {
            "base64" => "!not-base64!",
            "base64-whitespace" => encoded + "\n",
            "utf8" => Convert.ToBase64String(new byte[] { 0x7b, 0x22, 0xc3, 0x28, 0x22, 0x3a, 0x31, 0x7d }),
            "nonobject" => PairEncodeText("[]"),
            "duplicate-root" => PairEncodeText("{\"duplicate\":1,\"duplicate\":2}"),
            "duplicate-nested" => PairEncodeText("{\"nested\":[{\"duplicate\":1,\"duplicate\":2}]}"),
            _ => throw new ArgumentOutOfRangeException(nameof(change))
        };
        AssertPairRejected(source);
    }

    /// <summary>Object-property order is not semantic; canonical parent row order remains unchanged.</summary>
    [Fact]
    public void PairPressureDataAcceptsObjectPropertyOrderPermutation()
    {
        var source = SupportedPairSource();
        var proof = PairProof(source);
        proof["originalInput"] = ReversePairObject(proof["originalInput"]!.AsObject());
        proof["ordinaryCalculation"] = ReversePairObject(proof["ordinaryCalculation"]!.AsObject());
        for (var index = 0; index < 2; index++)
            proof["parents"]![index] = ReversePairObject(proof["parents"]![index]!.AsObject());
        source["strainPreimage"] = ReversePairObject(proof);
        SpiritualPressurePairTestData.Seal(source);
        var before = source.ToJsonString();
        var calculation = SpiritualWoundSourceWitness.ValidateAndReadStrainData(source, out var retained);
        Assert.Equal((23L, 4), (calculation.TraumaPressure, calculation.MaximumSeverityRank));
        Assert.Equal(4, Assert.IsType<SpiritualStrainPreimageData>(retained).Version);
        Assert.Equal(before, source.ToJsonString());
    }

    /// <summary>Enumerates eighteen required fields plus one extra-field case per closed object.</summary>
    public static IEnumerable<object[]> PairClosedFieldCases()
    {
        foreach (var (scope, fields) in PairWireFields())
        {
            foreach (var field in fields.Split(' ')) yield return [scope, field];
            yield return [scope, "+unowned"];
        }
    }

    /// <summary>Enumerates each of the eighteen closed properties for lexical duplicate rejection.</summary>
    public static IEnumerable<object[]> PairDuplicateFieldCases()
    {
        foreach (var (scope, fields) in PairWireFields())
            foreach (var field in fields.Split(' ')) yield return [scope, field];
    }

    /// <summary>Enumerates twenty-four deliberately bounded kind, role and scalar controls.</summary>
    public static IEnumerable<object[]> PairScalarCases()
    {
        foreach (var json in new[] { "\"4\"", "4.0", "null", "5" }) yield return ["proof", "version", json];
        yield return ["proof", "inputRoleRevision", "\"existing_pressure_ordinary_v1\""];
        yield return ["proof", "inputRoleRevision", "4"];
        yield return ["proof", "parents", "null"];
        yield return ["proof", "parents", "{}"];
        yield return ["parents", "0", "null"];
        yield return ["parents", "1", "[]"];
        yield return ["input", "path", "\"game_state/meta/foreign.json\""];
        yield return ["input", "existed", "false"];
        yield return ["input", "existed", "\"true\""];
        yield return ["input", "contentFingerprint", "\"not-a-fingerprint\""];
        yield return ["input", "exchangePointer", "0"];
        yield return ["parentB", "effectId", "null"];
        yield return ["parentB", "componentId", "3"];
        yield return ["parentB", "parentWoundJsonBase64", "42"];
        yield return ["calculation", "traumaPressure", "\"13\""];
        yield return ["calculation", "traumaPressure", "13.0"];
        yield return ["calculation", "maximumSeverityRank", "5"];
        yield return ["calculation", "maximumSeverityRank", "2.0"];
        yield return ["proof", "originalInput", "[]"];
        yield return ["proof", "ordinaryCalculation", "null"];
    }

    /// <summary>Enumerates three independent payloads times six encoding/object-syntax controls.</summary>
    public static IEnumerable<object[]> PairEmbeddedPayloadCases()
    {
        foreach (var payload in new[] { "ordinary", "parentA", "parentB" })
            foreach (var change in new[] { "base64", "base64-whitespace", "utf8", "nonobject", "duplicate-root", "duplicate-nested" })
                yield return [payload, change];
    }

    private static (string Scope, string Fields)[] PairWireFields() =>
    [
        ("proof", "version inputRoleRevision ordinaryExchangeJsonBase64 originalInput parents ordinaryCalculation"),
        ("input", "path existed contentFingerprint exchangePointer"),
        ("calculation", "traumaPressure maximumSeverityRank"),
        ("parentA", "parentWoundJsonBase64 effectId componentId"),
        ("parentB", "parentWoundJsonBase64 effectId componentId")
    ];

    private static JsonObject PairWireObject(JsonObject source, string scope) => scope switch
    {
        "proof" => PairProof(source),
        "input" => PairProof(source)["originalInput"]!.AsObject(),
        "calculation" => PairProof(source)["ordinaryCalculation"]!.AsObject(),
        "parentA" => PairProof(source)["parents"]![0]!.AsObject(),
        "parentB" => PairProof(source)["parents"]![1]!.AsObject(),
        _ => throw new ArgumentOutOfRangeException(nameof(scope))
    };

    private static JsonObject ReversePairObject(JsonObject original)
    {
        var result = new JsonObject();
        foreach (var property in original.Reverse()) result[property.Key] = property.Value?.DeepClone();
        return result;
    }

    private static string PairEncodeText(string json) => Convert.ToBase64String(Encoding.UTF8.GetBytes(json));
}
