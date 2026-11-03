using System.Text;
using System.Text.Json.Nodes;
using BookOfEternityClient.Services;
using Xunit;

namespace BookOfEternityClient.Tests;

/// <summary>Checks closed existing-parent comparison DATA, never current owner or publication authority.</summary>
public sealed class SpiritualExistingStrainPreimageTests
{
    /// <summary>Allows a signed-existing III or IV parent at the first source of a later request.</summary>
    [Theory]
    [InlineData(3)]
    [InlineData(4)]
    public void ExistingParentDataAcceptsClosedComparisonAtFirstSource(int rank)
    {
        var (source, parent, ordinary) = CreateData(rank);
        var parsed = WoundMaterializationContract.Parse(parent.ToJsonString(), "unit.existingParent");
        Assert.True(parsed.IsValid, string.Join("\n", parsed.Issues));
        var issues = new List<ValidationIssue>();
        ValidationService.ValidateRetainedSpiritualExchangeShape(ordinary, "unit.ordinary", issues);
        ValidationService.ValidateSpiritualWoundSourceActionShape(ordinary, "unit.ordinary", issues);
        ValidationService.ValidateRetainedSpiritualDiceAudit(ordinary, source["turnEvidence"]!.AsObject(), issues);
        ValidationService.ValidateRetainedSpiritualExchangeRules(ordinary, issues);
        Assert.DoesNotContain(issues, row => row.Severity == IssueSeverity.Error);
        var before = source.ToJsonString();
        Assert.Equal(2, SpiritualWoundSourceWitness.Validate(source).MaximumSeverityRank);
        var data = SpiritualStrainPreimageData.ReadIntrinsic(source, ReadActual(source));
        Assert.Equal(rank, data.ParentWound.Severity.Rank);
        Assert.Equal(WoundIdentityState.ComputeSemanticFingerprint(parsed.Wound!), data.ParentSemanticFingerprint);
        Assert.Equal("overwhelmed", data.ReadOrdinaryAfter(ReadActual(source))["playerSideStrain"]!.GetValue<string>());
        Assert.Equal(before, source.ToJsonString());
    }

    /// <summary>Retains every sibling and rejects incomplete, foreign or contradictory intrinsic comparisons.</summary>
    [Theory]
    [InlineData("extra-field")]
    [InlineData("parent-source-id")]
    [InlineData("v1-shape")]
    [InlineData("parent-owner")]
    [InlineData("parent-realm")]
    [InlineData("parent-rank")]
    [InlineData("parent-terminal")]
    [InlineData("effect")]
    [InlineData("component")]
    [InlineData("ordinary-opposition")]
    [InlineData("ordinary-position")]
    [InlineData("ordinary-dice")]
    [InlineData("wrong-step")]
    [InlineData("unchanged-ordinary")]
    public void ExistingParentDataRejectsRehashedContradiction(string change)
    {
        var (source, parent, ordinary) = CreateData(3);
        Assert.Equal(2, SpiritualWoundSourceWitness.Validate(source).MaximumSeverityRank);
        var proof = source["strainPreimage"]!.AsObject();
        switch (change)
        {
            case "extra-field": proof["unowned"] = true; break;
            case "parent-source-id": proof["parentSourceId"] = "spiritual_source_foreign"; break;
            case "v1-shape": proof["version"] = 1; break;
            case "parent-owner": parent["owner"]!["ownerId"] = "foreign-soul"; break;
            case "parent-realm": parent["owner"]!["realm"] = "shining_abode"; break;
            case "parent-rank": parent["severity"]!["rank"] = 2; parent["severity"]!["value"] = "II"; break;
            case "parent-terminal": parent["lifecycle"] = "healed"; break;
            case "effect": proof["effectId"] = "effect_foreign"; break;
            case "component": proof["componentId"] = "component_foreign"; break;
            case "ordinary-opposition": ordinary["after"]!["oppositionSideStrain"] = "strained"; break;
            case "ordinary-position": ordinary["after"]!["conflictPosition"] = "player_advantaged"; break;
            case "ordinary-dice": ordinary["diceAudit"]!["margin"] = 99; break;
            case "wrong-step":
                var wrong = ReadActual(source); wrong["after"]!["playerSideStrain"] = "overwhelmed";
                source["exchangeJsonBase64"] = Encode(wrong); break;
            case "unchanged-ordinary":
                ordinary["after"] = ordinary["before"]!.DeepClone();
                var unchanged = ordinary.DeepClone().AsObject(); unchanged["after"]!["playerSideStrain"] = "broken";
                source["exchangeJsonBase64"] = Encode(unchanged); break;
            default: throw new ArgumentOutOfRangeException(nameof(change));
        }
        proof["parentWoundJsonBase64"] = Encode(parent);
        proof["ordinaryExchangeJsonBase64"] = Encode(ordinary);
        Seal(source);
        var before = source.ToJsonString();
        Assert.Throws<FormatException>(() => SpiritualWoundSourceWitness.Validate(source));
        Assert.Equal(before, source.ToJsonString());
    }

    /// <summary>Exposes independent DATA copies and refuses another actual exchange after a valid read.</summary>
    [Fact]
    public void ExistingParentDataReturnsDetachedOrdinaryWithoutPermission()
    {
        var (source, _, ordinary) = CreateData(3);
        Assert.Equal(2, SpiritualWoundSourceWitness.Validate(source).MaximumSeverityRank);
        var actual = ReadActual(source);
        var retained = SpiritualStrainPreimageData.ReadIntrinsic(source, actual);
        var first = retained.ReadOrdinaryAfter(actual);
        first["playerSideStrain"] = "clear";
        Assert.True(JsonNode.DeepEquals(ordinary["after"], retained.ReadOrdinaryAfter(actual)));
        var changed = actual.DeepClone().AsObject(); changed["after"]!["oppositionSideStrain"] = "broken";
        Assert.Throws<FormatException>(() => retained.ReadOrdinaryAfter(changed));
    }

    internal static (JsonObject Source, JsonObject Parent, JsonObject Ordinary) CreateData(int rank)
    {
        var (source, _, parent, ordinary) = SpiritualStrainPreimageDataTests.CreateData();
        Assert.True(rank is 3 or 4);
        if (rank == 4)
        {
            parent["severity"]!["rank"] = 4; parent["severity"]!["value"] = "IV";
            parent["lastTransition"]!["kind"] = "worsen";
            parent["lastTransition"]!["ordinal"] = 2;
            parent["lastTransition"]!["transitionId"] = "transition_existing_worsen_2";
            parent["severity"]!["lastChangeEventRef"] = "event_existing_worsen_42";
            parent["consequences"]!["slotBudget"] = 4; parent["consequences"]!["slotsUsed"] = 4;
            var graph = parent["consequences"]!["ownedEffectSources"]!;
            var cost = graph["definitions"]!.AsArray().Single(row => row!["definitionKey"]!.GetValue<string>() == "definition_parent_cost")!;
            cost["definition"]!["components"]![0]!["payload"]!["magnitude"] = 3;
            graph["definitions"]!.AsArray().Add(EffectMaterializationTestFixture.CreateSpiritualWoundDefinition(
                "spiritual_roll_hindrance", woundId: parent["woundId"]!.GetValue<string>(), realm: "chaos_sea",
                definitionKey: "definition_parent_extra", targetKind: "player", operation: "pressure"));
            graph["rootBindings"]!.AsArray().Add(WoundContractTestData.CreateRootBinding("effect_parent_extra", "definition_parent_extra"));
            parent["consequences"]!["entries"]!.AsArray().Add(new JsonObject
                { ["slot"] = 4, ["profileKey"] = "spiritual_roll_hindrance", ["effectId"] = "effect_parent_extra",
                    ["readableSummary"] = "Давление душе даётся труднее." });
        }
        ordinary["turnNumber"] = 43;
        ordinary["diceAudit"]!["diceUsed"]![0]!["sourceIndex"] = 0;
        ordinary["diceAudit"]!["diceUsed"]![1]!["sourceIndex"] = 1;
        var actual = ordinary.DeepClone().AsObject(); actual["after"]!["playerSideStrain"] = "broken";
        source["coordinate"] = "activeConflict.exchangeLog[0].player";
        source["sourceOrdinal"] = 0; source["exchangeOrdinal"] = 0;
        source["selectedDiceIndices"] = new JsonArray(0, 1);
        var evidence = source["turnEvidence"]!;
        evidence["requestId"] = "request-existing43"; evidence["snapshotToken"] = "snapshot-existing43"; evidence["turn"] = 43;
        evidence["bounds"]!["exchangeCount"] = 1; evidence["bounds"]!["sourceSlotCount"] = 2; evidence["bounds"]!["diceCount"] = 2;
        evidence["acceptedD20Values"] = new JsonArray(4, 12);
        var claims = new JsonArray();
        for (var i = 0; i < 2; i++)
        {
            var row = new JsonObject { ["sourceIndex"] = i, ["value"] = i == 0 ? 4 : 12,
                ["exchangeOrdinal"] = 0, ["claimFingerprint"] = "" };
            row["claimFingerprint"] = SpiritualWoundTurnEvidence.ComputeClaimFingerprint(row); claims.Add(row);
        }
        evidence["diceClaims"] = claims;
        source["exchangeJsonBase64"] = Encode(actual);
        source["strainPreimage"] = new JsonObject { ["version"] = 2,
            ["ordinaryExchangeJsonBase64"] = Encode(ordinary), ["parentWoundJsonBase64"] = Encode(parent),
            ["effectId"] = "effect_parent_strain", ["componentId"] = "component_001" };
        Seal(source);
        return (source, parent, ordinary);
    }

    private static JsonObject ReadActual(JsonObject source) => JsonNode.Parse(Convert.FromBase64String(
        source["exchangeJsonBase64"]!.GetValue<string>()))!.AsObject();
    private static string Encode(JsonNode value) => Convert.ToBase64String(Encoding.UTF8.GetBytes(value.ToJsonString()));
    private static void Seal(JsonObject source)
    {
        var hash = SpiritualWoundStateJson.Hash(source, "source", "sourceId", "sourceFingerprint");
        source["sourceFingerprint"] = hash; source["sourceId"] = "spiritual_source_" + hash[7..];
    }
}
