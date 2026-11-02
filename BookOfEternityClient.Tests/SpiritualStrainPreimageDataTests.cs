using System.Text;
using System.Text.Json.Nodes;
using BookOfEternityClient.Services;
using Xunit;

namespace BookOfEternityClient.Tests;

/// <summary>Checks intrinsic comparison DATA, never live source, publication or historical authority.</summary>
public sealed class SpiritualStrainPreimageDataTests
{
    [Fact]
    public void IntrinsicWitnessRetainsOrdinaryManeuverAndParentGraphWithoutMutation()
    {
        var (source, parentSource, parentWound, ordinary) = CreateData();
        Assert.Equal(3, SpiritualWoundSourceWitness.Validate(parentSource).MaximumSeverityRank);
        var parsed = WoundMaterializationContract.Parse(parentWound.ToJsonString(), "unit.parentWound");
        Assert.True(parsed.IsValid, string.Join("\n", parsed.Issues));
        var issues = new List<ValidationIssue>();
        ValidationService.ValidateRetainedSpiritualExchangeShape(ordinary, "unit.ordinary", issues);
        ValidationService.ValidateSpiritualWoundSourceActionShape(ordinary, "unit.ordinary", issues);
        ValidationService.ValidateRetainedSpiritualDiceAudit(ordinary, source["turnEvidence"]!.AsObject(), issues);
        ValidationService.ValidateRetainedSpiritualExchangeRules(ordinary, issues);
        Assert.DoesNotContain(issues, issue => issue.Severity == IssueSeverity.Error);
        var before = source.ToJsonString();
        Assert.Equal(2, SpiritualWoundSourceWitness.Validate(source).MaximumSeverityRank);
        Assert.Equal(before, source.ToJsonString());
    }

    [Theory]
    [InlineData("extra-field")]
    [InlineData("version")]
    [InlineData("parent-id")]
    [InlineData("parent-owner")]
    [InlineData("parent-rank")]
    [InlineData("effect")]
    [InlineData("component")]
    [InlineData("ordinary-position")]
    [InlineData("ordinary-strain")]
    [InlineData("ordinary-dice")]
    [InlineData("duplicate-json")]
    public void IntrinsicWitnessRefusesChangedPreimageEvenAfterSourceRehash(string change)
    {
        var (source, _, parentWound, ordinary) = CreateData();
        var proof = source["strainPreimage"]!.AsObject();
        switch (change)
        {
            case "extra-field": proof["unowned"] = true; break;
            case "version": proof["version"] = 2; break;
            case "parent-id": proof["parentSourceId"] = "foreign"; break;
            case "parent-owner":
                parentWound["owner"]!["ownerId"] = "another-soul";
                proof["parentWoundJsonBase64"] = Encode(parentWound.ToJsonString());
                break;
            case "parent-rank":
                parentWound["severity"]!["rank"] = 2;
                proof["parentWoundJsonBase64"] = Encode(parentWound.ToJsonString());
                break;
            case "effect": proof["effectId"] = "effect_parent_roll"; break;
            case "component": proof["componentId"] = "foreign-component"; break;
            case "ordinary-position": ordinary["after"]!["conflictPosition"] = "balanced"; break;
            case "ordinary-strain": ordinary["after"]!["playerSideStrain"] = "strained"; break;
            case "ordinary-dice": ordinary["diceAudit"]!["margin"] = 99; break;
            case "duplicate-json":
                proof["ordinaryExchangeJsonBase64"] = Encode("{\"after\":{},\"after\":{}}");
                break;
            default: throw new ArgumentOutOfRangeException(nameof(change));
        }
        if (change.StartsWith("ordinary-", StringComparison.Ordinal))
            proof["ordinaryExchangeJsonBase64"] = Encode(ordinary.ToJsonString());
        Seal(source);
        var before = source.ToJsonString();
        Assert.Throws<FormatException>(() => SpiritualWoundSourceWitness.Validate(source));
        Assert.Equal(before, source.ToJsonString());
    }

    [Fact]
    public void IntrinsicDataCopiesOrdinaryAfterAndRefusesAnotherActualExchange()
    {
        var (source, _, _, ordinary) = CreateData();
        var actual = JsonNode.Parse(Decode(source["exchangeJsonBase64"]!))!.AsObject();
        var retained = SpiritualStrainPreimageData.ReadIntrinsic(source, actual);
        var first = retained.ReadOrdinaryAfter(actual);
        Assert.True(JsonNode.DeepEquals(ordinary["after"], first));
        first["playerSideStrain"] = "clear";
        Assert.Equal("overwhelmed", retained.ReadOrdinaryAfter(actual)["playerSideStrain"]!.GetValue<string>());
        var changed = actual.DeepClone().AsObject();
        changed["after"]!["oppositionSideStrain"] = "broken";
        Assert.Throws<FormatException>(() => retained.ReadOrdinaryAfter(changed));
        Assert.True(JsonNode.DeepEquals(ordinary["after"], retained.ReadOrdinaryAfter(actual)));
    }

    /// <summary>Creates independent intrinsic DATA for typed parent-receipt controls without live authority.</summary>
    internal static (JsonObject Source, JsonObject ParentSource, JsonObject ParentWound, JsonObject Ordinary) CreateData()
    {
        var parent = SpiritualWoundSourceWitnessTests.Source();
        var first = JsonNode.Parse(Decode(parent["exchangeJsonBase64"]!))!.AsObject();
        first["outcome"] = "setback";
        first["after"]!["playerSideStrain"] = "overwhelmed";
        first["after"]!["oppositionSideStrain"] = "clear";
        first["incomingAction"] = Incoming("pressure");
        SetDice(first, 0, 5, 15);
        parent["coordinate"] = "activeConflict.exchangeLog[0].player";
        parent["affectedSide"] = "player";
        parent["actingActor"] = new JsonObject { ["actorKind"] = "guardian", ["actorId"] = "guardian-a" };
        parent["affectedActor"] = new JsonObject { ["actorKind"] = "player_soul", ["actorId"] = "player_soul" };
        parent["appliedArtId"] = "guardian:guardian-a:pressure";
        parent["destinationStrainRank"] = 3;
        parent["extraStrainJumps"] = 2;
        parent["maximumSeverityRank"] = 3;
        parent["exchangeJsonBase64"] = Encode(SpiritualWoundStateJson.Canonical(first));
        parent["turnEvidence"]!["acceptedD20Values"] = new JsonArray(5, 15, 4, 12);
        parent["turnEvidence"]!["diceClaims"] = Claims(0, 0, 5, 15);
        Seal(parent);

        var ordinary = first.DeepClone().AsObject();
        ordinary["exchangeId"] = "exchange-b";
        ordinary["operationType"] = "maneuver";
        ordinary["before"] = first["after"]!.DeepClone();
        ordinary["after"] = ordinary["before"]!.DeepClone();
        ordinary["after"]!["conflictPosition"] = "opposition_advantaged";
        ordinary["incomingAction"] = Incoming("guard");
        ordinary["matchupAudit"]!["playerOperation"] = "maneuver";
        ordinary["matchupAudit"]!["primaryResolutionLane"] = "maneuver";
        ordinary["matchupAudit"]!["oppositionOperation"] = "guard";
        ordinary["matchupAudit"]!["riskProfile"] = "position_play";
        var playerCost = ordinary["actionCostAudit"]!["player"]!;
        playerCost["operationType"] = "maneuver";
        playerCost["before"] = 3;
        playerCost["after"] = 0;
        var guardCost = ordinary["actionCostAudit"]!["opposition"]!;
        guardCost["operationType"] = "guard";
        guardCost["baseCost"] = 2;
        guardCost["effectiveCost"] = 2;
        guardCost["before"] = 3;
        guardCost["after"] = 1;
        SetDice(ordinary, 2, 4, 12);
        var actual = ordinary.DeepClone().AsObject();
        actual["after"]!["playerSideStrain"] = "broken";

        const string woundId = "wound_strain_parent";
        var wound = WoundContractTestData.CreateSpiritualActiveWound(woundId, ownerId: "player_soul",
            carrierPath: WoundCarrierCatalog.AfterlifeProfilesPath);
        wound["severity"]!["value"] = "III";
        wound["severity"]!["rank"] = 3;
        wound["severity"]!["maximumAtCreation"] = "III";
        var key = SpiritualWoundSourceWitness.HarmEventKey("session-a", "request-a", "snapshot-a",
            "conflict-a", "exchange-a", "player");
        wound["origin"]!["sourceKind"] = "spiritual_standard_art";
        wound["origin"]!["sourceId"] = "guardian:guardian-a:pressure";
        wound["origin"]!["eventRef"] = "spiritual_wound_event_" + key;
        wound["origin"]!["opportunityId"] = "spiritual_wound_" + key;
        wound["severity"]!["lastChangeEventRef"] = "spiritual_wound_event_" + key;
        var definitions = new JsonArray();
        var roots = new JsonArray();
        var entries = new JsonArray();
        var rows = new[] { ("strain", "spiritual_strain_burden", "maneuver"),
            ("roll", "spiritual_roll_hindrance", "counter"), ("cost", "spiritual_action_cost_burden", "guard") };
        for (var index = 0; index < rows.Length; index++)
        {
            var (name, profile, operation) = rows[index];
            var effect = "effect_parent_" + name;
            var definition = "definition_parent_" + name;
            definitions.Add(EffectMaterializationTestFixture.CreateSpiritualWoundDefinition(profile,
                woundId: woundId, realm: "chaos_sea", definitionKey: definition, targetKind: "player",
                operation: operation, magnitude: profile == "spiritual_action_cost_burden" ? JsonValue.Create(2) : null));
            roots.Add(WoundContractTestData.CreateRootBinding(effect, definition));
            entries.Add(new JsonObject { ["slot"] = index + 1, ["profileKey"] = profile,
                ["effectId"] = effect, ["readableSummary"] = "Духовное последствие: " + name });
        }
        wound["consequences"]!["slotBudget"] = 3;
        wound["consequences"]!["slotsUsed"] = 3;
        wound["consequences"]!["ownedEffectSources"] = new JsonObject
            { ["definitions"] = definitions, ["rootBindings"] = roots };
        wound["consequences"]!["entries"] = entries;

        var source = parent.DeepClone().AsObject();
        source["coordinate"] = "activeConflict.exchangeLog[1].player";
        source["exchangeId"] = "exchange-b";
        source["sourceOrdinal"] = 1;
        source["exchangeOrdinal"] = 1;
        source["operation"] = "guard";
        source["appliedArtId"] = "guardian:guardian-a:guard";
        source["priorStrainRank"] = 3;
        source["destinationStrainRank"] = 4;
        source["extraStrainJumps"] = 0;
        source["harmfulMargin"] = 8;
        source["maximumSeverityRank"] = 2;
        source["exchangeJsonBase64"] = Encode(SpiritualWoundStateJson.Canonical(actual));
        source["selectedDiceIndices"] = new JsonArray(2, 3);
        source["turnEvidence"]!["diceClaims"] = Claims(1, 2, 4, 12);
        source["strainPreimage"] = new JsonObject { ["version"] = 1,
            ["ordinaryExchangeJsonBase64"] = Encode(ordinary.ToJsonString()),
            ["parentSourceId"] = parent["sourceId"]!.GetValue<string>(),
            ["parentWoundJsonBase64"] = Encode(wound.ToJsonString()),
            ["effectId"] = "effect_parent_strain", ["componentId"] = "component_001" };
        Seal(source);
        return (source, parent, wound, ordinary);
    }

    private static JsonObject Incoming(string operation) => new()
    {
        ["operationType"] = operation, ["actorType"] = "guardian", ["actorId"] = "guardian-a",
        ["summary"] = "Подтверждённое действие Хранителя."
    };

    private static void SetDice(JsonObject exchange, int firstIndex, int player, int opposition)
    {
        var audit = exchange["diceAudit"]!;
        audit["diceUsed"]![0]!["sourceIndex"] = firstIndex;
        audit["diceUsed"]![0]!["value"] = player;
        audit["diceUsed"]![1]!["sourceIndex"] = firstIndex + 1;
        audit["diceUsed"]![1]!["value"] = opposition;
        audit["playerTotal"] = player;
        audit["oppositionTotal"] = opposition;
        audit["margin"] = player - opposition;
        audit["outcomeBand"] = "decisive_opposition_success";
    }

    private static JsonArray Claims(int ordinal, int firstIndex, int player, int opposition)
    {
        var claims = new JsonArray();
        foreach (var (index, value) in new[] { (firstIndex, player), (firstIndex + 1, opposition) })
        {
            var claim = new JsonObject { ["sourceIndex"] = index, ["value"] = value,
                ["exchangeOrdinal"] = ordinal, ["claimFingerprint"] = "" };
            claim["claimFingerprint"] = SpiritualWoundTurnEvidence.ComputeClaimFingerprint(claim);
            claims.Add(claim);
        }
        return claims;
    }

    private static string Encode(string value) => Convert.ToBase64String(Encoding.UTF8.GetBytes(value));
    private static string Decode(JsonNode value) => new UTF8Encoding(false, true)
        .GetString(Convert.FromBase64String(value.GetValue<string>()));
    private static void Seal(JsonObject source)
    {
        var fingerprint = SpiritualWoundStateJson.Hash(source, "source", "sourceId", "sourceFingerprint");
        source["sourceFingerprint"] = fingerprint;
        source["sourceId"] = "spiritual_source_" + fingerprint[7..];
    }
}
