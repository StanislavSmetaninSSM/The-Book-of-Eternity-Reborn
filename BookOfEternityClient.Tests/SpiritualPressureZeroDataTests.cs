using System.Text;
using System.Text.Json;
using System.Text.Json.Nodes;
using BookOfEternityClient.Services;
using Xunit;
using Xunit.Abstractions;

namespace BookOfEternityClient.Tests;

/// <summary>Desired-success v3 zero DATA regressions; no source membership, live owner or publication is manufactured.</summary>
public sealed class SpiritualPressureZeroDataTests(ITestOutputHelper output)
{
    private const string BaselineRefusal = "Only positive non-guaranteed standard pressure/setback comparison is supported.";

    /// <summary>Training retains the accepted beforeI/ordinaryII/actualIII geometry and both existing parent ranks.</summary>
    [Theory]
    [InlineData(3)]
    [InlineData(4)]
    public void PressureZeroDataAcceptsTrainingCapWithoutNarrowingStrainGeometry(int parentRank)
    {
        var fixture = SpiritualPressureZeroTestData.CreateData(parentRank, trainingCap: true);
        var before = fixture.Source.ToJsonString();
        var actual = SpiritualWoundSourceWitness.ValidateAndReadStrainData(fixture.Source, out var retained);
        Assert.Equal(new SpiritualWoundCalculation(new(8, 0, 0, "strained", "overwhelmed", "training", 4),
            1, 3, 1, 15, 2, 3, 0, true, 0), actual);
        var data = Assert.IsType<SpiritualStrainPreimageData>(retained);
        Assert.Equal(new SpiritualWoundCalculation(new(8, 0, 0, "strained", "fractured", "training", 4),
            1, 2, 0, 10, 1, 2, 0, true, 0), data.OrdinaryCalculation);
        Assert.Equal((3, parentRank), (data.Version, data.ParentWound.Severity.Rank));
        Assert.True(JsonNode.DeepEquals(fixture.Actual, data.ReadActualExchange()));
        Assert.True(JsonNode.DeepEquals(fixture.Ordinary["after"], data.ReadOrdinaryAfter(fixture.Actual)));
        var restored = fixture.Actual.DeepClone().AsObject();
        restored["after"]!["playerSideStrain"] = "fractured";
        Assert.True(JsonNode.DeepEquals(fixture.Ordinary, restored));
        Assert.Equal(new CanonicalBeforeImage(true, fixture.RawInput).Fingerprint, data.OriginalInputFingerprint);
        Assert.Equal("/activeConflict/exchangeLog/2", data.OriginalExchangePointer);
        var direct = SpiritualStrainPreimageData.ReadIntrinsic(fixture.Source, fixture.Actual);
        Assert.Equal(data.ActualCalculation, direct.ActualCalculation);
        Assert.Equal(data.OrdinaryCalculation, direct.OrdinaryCalculation);
        Assert.Equal(before, fixture.Source.ToJsonString());
    }

    /// <summary>Each independent mutation follows a successful baseline and a fresh lexical seal, preserving semantic refusal coverage.</summary>
    [Theory]
    [InlineData("extra-proof-field")]
    [InlineData("version-type")]
    [InlineData("noncanonical-pointer")]
    [InlineData("ordinary-pressure")]
    [InlineData("ordinary-maximum")]
    [InlineData("ordinary-sibling")]
    [InlineData("foreign-parent")]
    [InlineData("foreign-component")]
    [InlineData("guardian-attribution")]
    [InlineData("forged-actual-maximum")]
    [InlineData("ordinary-equals-actual")]
    public void PressureZeroDataRejectsResealedContradictions(string mutation)
    {
        var fixture = SpiritualPressureZeroTestData.CreateData(3);
        Assert.Equal(SpiritualPressureZeroTestData.ActualCalculation(),
            SpiritualWoundSourceWitness.ValidateAndReadStrainData(fixture.Source, out var valid));
        Assert.Equal(3, Assert.IsType<SpiritualStrainPreimageData>(valid).Version);
        var source = fixture.Source;
        var proof = source["strainPreimage"]!.AsObject();
        switch (mutation)
        {
            case "extra-proof-field": proof["permission"] = true; break;
            case "version-type": proof["version"] = "3"; break;
            case "noncanonical-pointer": proof["originalInput"]!["exchangePointer"] = "/activeConflict/exchangeLog/02"; break;
            case "ordinary-pressure": proof["ordinaryCalculation"]!["traumaPressure"] = 3L; break;
            case "ordinary-maximum": proof["ordinaryCalculation"]!["maximumSeverityRank"] = 1; break;
            case "ordinary-sibling": fixture.Ordinary["summary"] = "Changed ordinary sibling."; break;
            case "foreign-parent": fixture.Parent["owner"]!["ownerId"] = "foreign_soul"; break;
            case "foreign-component": proof["componentId"] = "component_foreign"; break;
            case "guardian-attribution": fixture.Actual[SpiritualWoundFailedAction.Property] = new JsonObject
                { ["version"] = 1, ["side"] = "opposition", ["actorType"] = "guardian", ["actorId"] = "guardian-a",
                    ["actionPath"] = "/incomingAction", ["operationType"] = "pressure" }; break;
            case "forged-actual-maximum": source["maximumSeverityRank"] = 1; break;
            case "ordinary-equals-actual": fixture.Ordinary["after"]!["playerSideStrain"] = "fractured"; break;
            default: throw new ArgumentOutOfRangeException(nameof(mutation));
        }
        proof["ordinaryExchangeJsonBase64"] = Convert.ToBase64String(Encoding.UTF8.GetBytes(fixture.Ordinary.ToJsonString()));
        proof["parentWoundJsonBase64"] = Convert.ToBase64String(Encoding.UTF8.GetBytes(fixture.Parent.ToJsonString()));
        source["exchangeJsonBase64"] = Convert.ToBase64String(Encoding.UTF8.GetBytes(fixture.Actual.ToJsonString()));
        SpiritualExistingPressureTestData.Seal(source);
        var changed = source.ToJsonString();
        Assert.Throws<FormatException>(() => SpiritualWoundSourceWitness.ValidateAndReadStrainData(source, out _));
        Assert.Throws<FormatException>(() => SpiritualStrainPreimageData.ReadIntrinsic(source, fixture.Actual));
        Assert.Equal(changed, source.ToJsonString());
    }

    /// <summary>Both complete existing singleton ranks retain ordinary and actual zero results after independent lawful prerequisites.</summary>
    [Theory]
    [InlineData(3)]
    [InlineData(4)]
    public void PressureZeroDataRetainsOrdinaryAndActual(int parentRank)
    {
        var fixture = SpiritualPressureZeroTestData.CreateData(parentRank);
        var source = fixture.Source;
        var original = source.ToJsonString();
        var parent = WoundMaterializationContract.Parse(fixture.Parent.ToJsonString(), "test.pressureZero.expectedParent");
        Assert.True(parent.IsValid, string.Join("\n", parent.Issues));
        Assert.Equal((parentRank, "active", "spiritual", "player_soul"),
            (parent.Wound!.Severity.Rank, parent.Wound.Lifecycle, parent.Wound.Classification.Domain, parent.Wound.Owner.OwnerId));
        Assert.Equal(2 * parentRank, fixture.Parent["recovery"]!["currentStepThreshold"]!.GetValue<int>());
        Assert.Equal(parentRank, fixture.Parent["consequences"]!["entries"]!.AsArray().Count);
        Assert.Equal(parentRank, fixture.Parent["consequences"]!["ownedEffectSources"]!["rootBindings"]!.AsArray().Count);
        Assert.Equal(parentRank, fixture.Parent["consequences"]!["ownedEffectSources"]!["definitions"]!.AsArray().Count);
        Assert.Equal("clear", fixture.Ordinary["before"]!["playerSideStrain"]!.GetValue<string>());
        Assert.Equal("strained", fixture.Ordinary["after"]!["playerSideStrain"]!.GetValue<string>());
        Assert.Equal("fractured", fixture.Actual["after"]!["playerSideStrain"]!.GetValue<string>());
        var restored = fixture.Actual.DeepClone().AsObject();
        restored["after"]!["playerSideStrain"] = "strained";
        Assert.True(JsonNode.DeepEquals(fixture.Ordinary, restored));
        var input = source["strainPreimage"]!["originalInput"]!;
        Assert.Equal(AfterlifeSpiritualConflictState.StatePath, input["path"]!.GetValue<string>());
        Assert.True(input["existed"]!.GetValue<bool>());
        Assert.Equal("/activeConflict/exchangeLog/2", input["exchangePointer"]!.GetValue<string>());
        Assert.Equal(new CanonicalBeforeImage(true, fixture.RawInput).Fingerprint, input["contentFingerprint"]!.GetValue<string>());
        Assert.Equal(Encoding.UTF8.GetPreamble(), fixture.RawInput.Take(3));
        var rawRoot = JsonNode.Parse(Encoding.UTF8.GetString(fixture.RawInput.AsSpan(3)))!;
        Assert.True(JsonNode.DeepEquals(fixture.Ordinary, rawRoot["activeConflict"]!["exchangeLog"]![2]));
        Assert.Equal("strained", rawRoot["activeConflict"]!["playerSideStrain"]!.GetValue<string>());

        // These comparison-free witnesses must succeed before the new v3 gate is reached.
        var actualOnly = source.DeepClone().AsObject();
        actualOnly.Remove("strainPreimage");
        SpiritualExistingPressureTestData.Seal(actualOnly);
        var actualMath = SpiritualWoundSourceWitness.ValidateAndReadStrainData(actualOnly, out var noActualComparison);
        Assert.Null(noActualComparison);
        Assert.Equal(SpiritualPressureZeroTestData.ActualCalculation(), actualMath);
        var ordinaryOnly = actualOnly.DeepClone().AsObject();
        ordinaryOnly["exchangeJsonBase64"] = Convert.ToBase64String(Encoding.UTF8.GetBytes(fixture.Ordinary.ToJsonString()));
        ordinaryOnly["destinationStrainRank"] = 1;
        ordinaryOnly["extraStrainJumps"] = 0;
        SpiritualExistingPressureTestData.Seal(ordinaryOnly);
        var ordinaryMath = SpiritualWoundSourceWitness.ValidateAndReadStrainData(ordinaryOnly, out var noOrdinaryComparison);
        Assert.Null(noOrdinaryComparison);
        Assert.Equal(SpiritualPressureZeroTestData.OrdinaryCalculation(), ordinaryMath);

        SpiritualWoundCalculation? actual = null;
        SpiritualStrainPreimageData? retained = null;
        var witnessFailure = Record.Exception(() =>
            actual = SpiritualWoundSourceWitness.ValidateAndReadStrainData(source, out retained));
        SpiritualStrainPreimageData? intrinsic = null;
        var intrinsicFailure = Record.Exception(() =>
            intrinsic = SpiritualStrainPreimageData.ReadIntrinsic(source, fixture.Actual));
        output.WriteLine(JsonSerializer.Serialize(new
        {
            phase = "pressure_zero_intrinsic_desired_success", parentRank,
            ordinaryMath, actualMath, sourceJson = original, rawInputBase64 = Convert.ToBase64String(fixture.RawInput),
            witnessFailure = witnessFailure?.ToString(), intrinsicFailure = intrinsicFailure?.ToString(),
            downstream = witnessFailure is null && intrinsicFailure is null ? "REACHED" : "UNREACHED"
        }));
        Assert.Equal(original, source.ToJsonString());
        // Exact unsupported failure is diagnostic evidence, never an expected-refusal PASS.
        if (witnessFailure is not null)
        {
            Assert.Equal(BaselineRefusal, Assert.IsType<FormatException>(witnessFailure).Message);
            Assert.Null(retained);
        }
        if (intrinsicFailure is not null)
            Assert.Equal(BaselineRefusal, Assert.IsType<FormatException>(intrinsicFailure).Message);
        Assert.Null(witnessFailure);
        Assert.Null(intrinsicFailure);

        // UNREACHED on the pre-implementation baseline: full intended supported-DATA contract.
        Assert.Equal(actualMath, actual);
        var data = Assert.IsType<SpiritualStrainPreimageData>(retained);
        Assert.Equal(3, data.Version);
        Assert.Null(data.ParentSourceId);
        Assert.Equal("effect_pressure_strain", data.EffectId);
        Assert.Equal("component_001", data.ComponentId);
        Assert.Equal(WoundMaterializationContract.SerializeCanonical(parent.Wound),
            WoundMaterializationContract.SerializeCanonical(data.ParentWound));
        Assert.Equal(ordinaryMath, data.OrdinaryCalculation);
        Assert.Same(actual, data.ActualCalculation);
        Assert.Equal(input["contentFingerprint"]!.GetValue<string>(), data.OriginalInputFingerprint);
        Assert.Equal("/activeConflict/exchangeLog/2", data.OriginalExchangePointer);
        Assert.True(JsonNode.DeepEquals(fixture.Actual, data.ReadActualExchange()));
        Assert.True(JsonNode.DeepEquals(fixture.Ordinary["after"], data.ReadOrdinaryAfter(fixture.Actual)));
        var proof = source["strainPreimage"]!.AsObject();
        Assert.Equal(new[] { "componentId", "effectId", "inputRoleRevision", "ordinaryCalculation", "ordinaryExchangeJsonBase64", "originalInput", "parentWoundJsonBase64", "version" },
            proof.Select(row => row.Key).OrderBy(key => key, StringComparer.Ordinal));
        Assert.Equal("existing_pressure_ordinary_v1", proof["inputRoleRevision"]!.GetValue<string>());
        var direct = Assert.IsType<SpiritualStrainPreimageData>(intrinsic);
        Assert.Equal(data.OrdinaryCalculation, direct.OrdinaryCalculation);
        Assert.Equal(data.ActualCalculation, direct.ActualCalculation);
        Assert.Equal(WoundMaterializationContract.SerializeCanonical(data.ParentWound),
            WoundMaterializationContract.SerializeCanonical(direct.ParentWound));
        data.ReadActualExchange()["after"]!["playerSideStrain"] = "broken";
        data.ReadOrdinaryAfter(fixture.Actual)["playerSideStrain"] = "broken";
        Assert.True(JsonNode.DeepEquals(fixture.Actual, data.ReadActualExchange()));
        Assert.True(JsonNode.DeepEquals(fixture.Ordinary["after"], data.ReadOrdinaryAfter(fixture.Actual)));
        Assert.Equal(original, source.ToJsonString());
    }
}
