using System.Text;
using System.Text.Json.Nodes;
using BookOfEternityClient.Services;
using Xunit;

namespace BookOfEternityClient.Tests;

/// <summary>Checks immutable capped-guard comparison DATA, never source, parent or publication authority.</summary>
public sealed class SpiritualStrainGuardCapDataTests
{
    [Fact]
    public void CappedGuardWitnessKeepsOrdinaryCalculationAndDetachedComparison()
    {
        var (source, parent, ordinary) = CreateData();
        Assert.Equal(3, SpiritualWoundSourceWitness.Validate(parent).MaximumSeverityRank);
        var issues = new List<ValidationIssue>();
        ValidationService.ValidateRetainedSpiritualExchangeShape(ordinary, "unit.guard", issues);
        ValidationService.ValidateSpiritualWoundSourceActionShape(ordinary, "unit.guard", issues);
        ValidationService.ValidateRetainedSpiritualDiceAudit(ordinary, source["turnEvidence"]!.AsObject(), issues);
        ValidationService.ValidateRetainedSpiritualExchangeRules(ordinary, issues);
        Assert.DoesNotContain(issues, issue => issue.Severity == IssueSeverity.Error);
        var before = source.ToJsonString();
        var calculation = SpiritualWoundSourceWitness.Validate(source);
        Assert.Equal(8L, calculation.Input.HarmfulMargin);
        Assert.Equal(2, calculation.MaximumSeverityRank);
        var data = SpiritualStrainPreimageData.ReadIntrinsic(source, ordinary);
        var copy = data.ReadOrdinaryAfter(ordinary);
        Assert.True(JsonNode.DeepEquals(ordinary["after"], copy));
        copy["playerSideStrain"] = "clear";
        Assert.Equal("broken", data.ReadOrdinaryAfter(ordinary)["playerSideStrain"]!.GetValue<string>());
        Assert.Equal(before, source.ToJsonString());
    }

    [Theory]
    [InlineData("ordinary-summary")]
    [InlineData("ordinary-strain")]
    [InlineData("ordinary-other-side")]
    [InlineData("uncapped-before")]
    [InlineData("incoming-art")]
    [InlineData("success")]
    [InlineData("parent-operation")]
    [InlineData("parent-magnitude")]
    [InlineData("foreign-root")]
    [InlineData("foreign-component")]
    public void CappedGuardRefusesChangedOrdinaryOrParentAfterValidBaseline(string change)
    {
        var (source, _, ordinary) = CreateData();
        Assert.Equal(2, SpiritualWoundSourceWitness.Validate(source).MaximumSeverityRank);
        var actual = ordinary.DeepClone().AsObject();
        var proof = source["strainPreimage"]!;
        var wound = JsonNode.Parse(Convert.FromBase64String(proof["parentWoundJsonBase64"]!.GetValue<string>()))!;
        switch (change)
        {
            case "ordinary-summary": ordinary["summary"] = "Иное обычное событие."; break;
            case "ordinary-strain": ordinary["after"]!["playerSideStrain"] = "overwhelmed"; break;
            case "ordinary-other-side": ordinary["after"]!["oppositionSideStrain"] = "strained"; break;
            case "uncapped-before":
                ordinary["before"]!["playerSideStrain"] = "fractured";
                actual["before"]!["playerSideStrain"] = "fractured";
                break;
            case "incoming-art":
                ordinary["incomingAction"]!["operationType"] = "guard";
                actual["incomingAction"]!["operationType"] = "guard";
                break;
            case "success": ordinary["outcome"] = "success"; actual["outcome"] = "success"; break;
            case "parent-operation": wound["consequences"]!["ownedEffectSources"]!["definitions"]![0]!["components"]![0]!["payload"]!["operation"] = "maneuver"; break;
            case "parent-magnitude": wound["consequences"]!["ownedEffectSources"]!["definitions"]![0]!["components"]![0]!["payload"]!["magnitude"] = 2; break;
            case "foreign-root": proof["effectId"] = "effect_parent_roll"; break;
            case "foreign-component": proof["componentId"] = "foreign-component"; break;
            default: throw new ArgumentOutOfRangeException(nameof(change));
        }
        proof["parentWoundJsonBase64"] = Encode(wound.ToJsonString());
        proof["ordinaryExchangeJsonBase64"] = Encode(ordinary.ToJsonString());
        source["exchangeJsonBase64"] = Encode(SpiritualWoundStateJson.Canonical(actual));
        Seal(source);
        var before = source.ToJsonString();
        Assert.Throws<FormatException>(() => SpiritualWoundSourceWitness.Validate(source));
        Assert.Equal(before, source.ToJsonString());
    }

    [Fact]
    public void CappedComparisonExposesNoEditableFieldsAndCannotChangeOrdinarySource()
    {
        var (source, _, ordinary) = CreateData();
        var calculation = SpiritualWoundSourceWitness.Validate(source);
        var expected = new ValidationService.PreparedSpiritualSource("activeConflict.exchangeLog[1].player",
            "conflict-a", "exchange-b", "player", "guardian:guardian-a", "player_soul:player_soul", "pressure",
            null, ordinary.ToJsonString(), calculation, null, null);
        var comparison = new ValidationService.SpiritualStrainDraftCorrection("/activeConflict/exchangeLog/1",
            ordinary, ordinarySource: expected).WithParent(source["strainPreimage"]!.ToJsonString(), "effect_parent_strain", "component_001");
        Assert.Empty(comparison.Fields);
        Assert.True(comparison.IsCorrected(ordinary));
        var changed = ordinary.DeepClone().AsObject();
        changed["after"]!["playerSideStrain"] = "overwhelmed";
        Assert.False(comparison.Allows(changed));
        Assert.True(comparison.PreservesOrdinarySource(expected with { StrainPreimageJson = source["strainPreimage"]!.ToJsonString() }));
        Assert.False(comparison.PreservesOrdinarySource(expected with { AffectedActor = "player_soul:another" }));
        Assert.False(comparison.PreservesOrdinarySource(expected with { Calculation = calculation with { MaximumSeverityRank = 3 } }));
        Assert.False(comparison.PreservesOrdinarySource(expected with { ExchangeJson = changed.ToJsonString() }));
    }

    private static (JsonObject Source, JsonObject Parent, JsonObject Ordinary) CreateData()
    {
        var (source, parent, wound, ordinary) = SpiritualStrainPreimageDataTests.CreateData();
        ordinary["operationType"] = "guard";
        ordinary["after"] = ordinary["before"]!.DeepClone();
        ordinary["after"]!["playerSideStrain"] = "broken";
        ordinary["incomingAction"]!["operationType"] = "pressure";
        ordinary["matchupAudit"]!["playerOperation"] = "guard";
        ordinary["matchupAudit"]!["primaryResolutionLane"] = "guard";
        ordinary["matchupAudit"]!["oppositionOperation"] = "pressure";
        ordinary["matchupAudit"]!["riskProfile"] = "safe_defense";
        var player = ordinary["actionCostAudit"]!["player"]!;
        player["operationType"] = "guard";
        player["baseCost"] = 2;
        player["effectiveCost"] = 2;
        player["after"] = 1;
        var opposition = ordinary["actionCostAudit"]!["opposition"]!;
        opposition["operationType"] = "pressure";
        opposition["baseCost"] = 3;
        opposition["effectiveCost"] = 3;
        opposition["after"] = 0;
        var definitions = wound["consequences"]!["ownedEffectSources"]!["definitions"]!;
        definitions[0]!["components"]![0]!["payload"]!["operation"] = "guard";
        definitions[2]!["components"]![0]!["payload"]!["operation"] = "pressure";
        source["operation"] = "pressure";
        source["appliedArtId"] = "guardian:guardian-a:pressure";
        source["exchangeJsonBase64"] = Encode(SpiritualWoundStateJson.Canonical(ordinary));
        source["strainPreimage"]!["ordinaryExchangeJsonBase64"] = Encode(ordinary.ToJsonString());
        source["strainPreimage"]!["parentWoundJsonBase64"] = Encode(wound.ToJsonString());
        Seal(source);
        return (source, parent, ordinary);
    }

    private static string Encode(string value) => Convert.ToBase64String(Encoding.UTF8.GetBytes(value));

    private static void Seal(JsonObject source)
    {
        var fingerprint = SpiritualWoundStateJson.Hash(source, "source", "sourceId", "sourceFingerprint");
        source["sourceFingerprint"] = fingerprint;
        source["sourceId"] = "spiritual_source_" + fingerprint[7..];
    }
}
