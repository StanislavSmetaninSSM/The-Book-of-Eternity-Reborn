using System.Text;
using System.Text.Json.Nodes;
using BookOfEternityClient.Services;

namespace BookOfEternityClient.Tests;

/// <summary>Closed literal pair contours: four new positive-actual boundaries and two cheap discriminator controls.</summary>
internal enum PairLowPressureContour { OnePositive, OneZero, TwoZero, ThreeZero, LegacyOneZero, ActualZero }

/// <summary>Independent complete arithmetic and signed dice, never inferred from an issued production proof.</summary>
internal sealed record PairLowPressureExpectation(int PlayerDie, int Resilience,
    SpiritualWoundCalculation Ordinary, SpiritualWoundCalculation Actual);

/// <summary>Per-call detached complete parents, mirrored profiles, ordinary/actual exchanges and exact raw ordinary input.</summary>
internal sealed record PairLowPressureFixture(RetainedPressureGroupData Retained, JsonObject Profiles,
    JsonObject Conflict, JsonObject Ordinary, JsonObject Actual, JsonObject Source, byte[] RawInput);

/// <summary>Pure Integration DATA. No world, authority, native ancestry, cache, wire schema or execution adapter is added.</summary>
internal static class SpiritualPairLowPressureData
{
    /// <summary>Literal full calculations for the closed lawful matrix and retained refusal discriminators.</summary>
    /// <param name="contour">Unknown values fail before any fixture signing or owner allocation.</param>
    internal static PairLowPressureExpectation Expected(PairLowPressureContour contour) => contour switch
    {
        PairLowPressureContour.OnePositive => new(4, 0,
            new(new(8, 0, 0, "clear", "strained", "hostile", 4), 0, 1, 0, 8, 1, 1, 4, true, 1),
            new(new(8, 0, 0, "clear", "overwhelmed", "hostile", 4), 0, 3, 2, 18, 3, 3, 4, true, 3)),
        PairLowPressureContour.OneZero => new(4, 1,
            new(new(8, 0, 1, "clear", "strained", "hostile", 4), 0, 1, 0, 6, 0, 1, 4, true, 0),
            new(new(8, 0, 1, "clear", "overwhelmed", "hostile", 4), 0, 3, 2, 16, 2, 3, 4, true, 2)),
        PairLowPressureContour.TwoZero => new(4, 3,
            new(new(8, 0, 3, "clear", "fractured", "hostile", 4), 0, 2, 1, 7, 0, 2, 4, true, 0),
            new(new(8, 0, 3, "clear", "broken", "hostile", 4), 0, 4, 3, 17, 2, 4, 4, true, 2)),
        PairLowPressureContour.ThreeZero => new(6, 5,
            new(new(6, 0, 5, "clear", "overwhelmed", "hostile", 4), 0, 3, 2, 6, 0, 3, 4, true, 0),
            new(new(6, 0, 5, "clear", "broken", "hostile", 4), 0, 4, 3, 11, 1, 4, 4, true, 1)),
        PairLowPressureContour.LegacyOneZero => new(6, 0,
            new(new(6, 0, 0, "clear", "strained", "hostile", 4), 0, 1, 0, 6, 0, 1, 4, true, 0),
            new(new(6, 0, 0, "clear", "overwhelmed", "hostile", 4), 0, 3, 2, 16, 2, 3, 4, true, 2)),
        PairLowPressureContour.ActualZero => new(6, 5,
            new(new(6, 0, 5, "clear", "strained", "hostile", 4), 0, 1, 0, -4, 0, 1, 4, true, 0),
            new(new(6, 0, 5, "clear", "overwhelmed", "hostile", 4), 0, 3, 2, 6, 0, 3, 4, true, 0)),
        _ => throw new ArgumentOutOfRangeException(nameof(contour))
    };

    /// <summary>Maps only the four accepted new positive-actual contours from public finite Theory arguments.</summary>
    /// <param name="index">A=0, B=1, C=2, D=3; no arbitrary enum cast is accepted.</param>
    internal static PairLowPressureContour Boundary(int index) => index switch
    {
        0 => PairLowPressureContour.OnePositive, 1 => PairLowPressureContour.OneZero,
        2 => PairLowPressureContour.TwoZero, 3 => PairLowPressureContour.ThreeZero,
        _ => throw new ArgumentOutOfRangeException(nameof(index))
    };

    /// <summary>Creates complete independent pair graphs and raw-bound v4 DATA without reading unsupported v4.</summary>
    /// <param name="contour">Closed ordinary/actual/dice/resilience expectation.</param>
    /// <param name="first">Parent rank at the first canonical wound-ID position.</param>
    /// <param name="second">Parent rank at the second canonical wound-ID position.</param>
    internal static PairLowPressureFixture Create(PairLowPressureContour contour, int first = 3, int second = 3)
    {
        var expected = Expected(contour);
        if (first is not (3 or 4)) throw new ArgumentOutOfRangeException(nameof(first));
        if (second is not (3 or 4)) throw new ArgumentOutOfRangeException(nameof(second));
        var retained = SpiritualPressureGroupOwnerCaseData.Create(2, ranks: new(first, second));
        var profiles = SpiritualPressureRetainedDataAdmissibility.CreateProfiles(retained);
        var guardian = AfterlifeActorMaterializationTestFixture.CreateCompleteProfile("guardian", "guardian_frame", "Chaos Sea", 40);
        guardian.Remove(ActorMaterializationContract.PropertyName);
        guardian["activeWounds"] = new JsonArray(); guardian["activeEffects"] = new JsonArray();
        profiles[AfterlifeEntityProfileState.ProfilesProperty]!.AsArray().Add(guardian);
        foreach (var profile in profiles[AfterlifeEntityProfileState.ProfilesProperty]!.AsArray())
        {
            var arts = new JsonObject();
            foreach (var art in AfterlifeEntityProfileState.StandardArtIds) arts[art] = 0;
            if (profile!["actorType"]!.GetValue<string>() == "player_soul") arts["spiritual_resilience"] = expected.Resilience;
            profile["standardArts"] = arts;
        }
        var ordinary = SpiritualPressureGroupOwnerCaseData.Ordinary(expected.Ordinary.Input.NewStrain, expected.PlayerDie);
        var actual = ordinary.DeepClone().AsObject(); actual["after"]!["playerSideStrain"] = expected.Actual.Input.NewStrain;
        var root = new JsonObject { ["schemaVersion"] = 1, ["activeConflict"] = new JsonObject
        {
            ["conflictId"] = "pair_low_data", ["realm"] = "Chaos Sea", ["sideModel"] = "direct_duel",
            ["dangerMode"] = "hostile", ["resolutionState"] = "active", ["playerSideStrain"] = expected.Ordinary.Input.NewStrain,
            ["oppositionSideStrain"] = "clear", ["conflictPosition"] = "contested", ["controlState"] = null,
            ["tempoAdvantage"] = null, ["exchangeLog"] = new JsonArray(ordinary.DeepClone()), ["combatConditions"] = new JsonArray(),
            ["playerSide"] = new JsonObject { ["leadContestant"] = new JsonObject { ["actorType"] = "player", ["actorId"] = "player_soul" }, ["supporters"] = new JsonArray() },
            ["oppositionSide"] = new JsonObject { ["leadContestant"] = new JsonObject { ["actorType"] = "guardian", ["actorId"] = "guardian_frame" }, ["supporters"] = new JsonArray() }
        }, ["recentConflicts"] = new JsonArray() };
        var raw = Encoding.UTF8.GetPreamble().Concat(Encoding.UTF8.GetBytes(" \n" + root.ToJsonString() + "\n")).ToArray();
        var source = CreateSource(actual, expected.Actual, raw);
        source["strainPreimage"] = new JsonObject
        {
            ["version"] = 4, ["inputRoleRevision"] = "existing_pressure_ordinary_pair_v1",
            ["ordinaryExchangeJsonBase64"] = SpiritualGuardianAttributionTestData.Encode(ordinary),
            ["parents"] = new JsonArray(retained.Parents.Select((parent, index) => (JsonNode)new JsonObject
            {
                ["parentWoundJsonBase64"] = SpiritualGuardianAttributionTestData.Encode(parent),
                ["effectId"] = retained.Effects[index]["effectId"]!.DeepClone(), ["componentId"] = "component_001"
            }).ToArray()),
            ["originalInput"] = new JsonObject { ["path"] = AfterlifeSpiritualConflictState.StatePath, ["existed"] = true,
                ["contentFingerprint"] = new CanonicalBeforeImage(true, raw).Fingerprint, ["exchangePointer"] = "/activeConflict/exchangeLog/0" },
            ["ordinaryCalculation"] = new JsonObject { ["traumaPressure"] = expected.Ordinary.TraumaPressure,
                ["maximumSeverityRank"] = expected.Ordinary.MaximumSeverityRank }
        };
        SpiritualExistingPressureTestData.Seal(source);
        return new(retained, profiles, root, ordinary, actual, source, raw);
    }

    /// <summary>Authors standalone source scalars from a literal expectation, then reseals after resilience replacement.</summary>
    /// <param name="exchange">The independently authored complete ordinary or actual exchange.</param>
    /// <param name="calculation">Independent full literal expectation, not a runtime result.</param>
    /// <param name="raw">Exact BOM/whitespace ordinary root used for original-input binding.</param>
    internal static JsonObject CreateSource(JsonObject exchange, SpiritualWoundCalculation calculation, byte[] raw)
    {
        var source = SpiritualRetainedPressureGroupTestData.CreateSource(exchange, "pair_low_session", "pair_low_request",
            "pair_low_snapshot", "pair_low_data", new CanonicalBeforeImage(true, raw).Fingerprint);
        source["targetResilienceTier"] = calculation.Input.TargetResilienceTier;
        source["maximumSeverityRank"] = calculation.MaximumSeverityRank;
        SpiritualExistingPressureTestData.Seal(source);
        return source;
    }

    /// <summary>Complete literal exchange oracle with only closed 4/12 or 6/12 signed dice.</summary>
    /// <param name="strain">The closed caller's independent ordinary or actual strain.</param>
    /// <param name="playerDie">Only the existing signed4/12 and signed6/12 contours.</param>
    internal static JsonObject ReadExchange(string strain, int playerDie = 4)
    {
        if (playerDie is not (4 or 6)) throw new ArgumentOutOfRangeException(nameof(playerDie));
        return JsonNode.Parse($$$$"""
        {"exchangeId":"exchange_pressure_group_46","turnNumber":46,"exchangeAtTurn":46,
         "operationType":"pressure","outcome":"setback","summary":"Душа встречает давление Хранителя.",
         "before":{"playerSideStrain":"clear","oppositionSideStrain":"clear","conflictPosition":"contested","controlState":null,"tempoAdvantage":null},
         "after":{"playerSideStrain":"{{{{strain}}}}","oppositionSideStrain":"clear","conflictPosition":"contested","controlState":null,"tempoAdvantage":null},
         "incomingAction":{"operationType":"pressure","actorType":"guardian","actorId":"guardian_frame","summary":"Хранитель оказывает давление."},
         "matchupAudit":{"playerOperation":"pressure","oppositionOperation":"pressure","primaryResolutionLane":"pressure","riskProfile":"offensive_pressure","matchupRationale":"Встречное давление."},
         "actionCostAudit":{
           "player":{"operationType":"pressure","baseCost":3,"minCost":1,"artTier":0,"effectiveCost":3,"before":3,"after":0,"max":10},
           "opposition":{"operationType":"pressure","baseCost":3,"minCost":1,"artTier":0,"effectiveCost":3,"before":3,"after":0,"max":9}},
         "diceAudit":{"formulaVersion":"afterlife_spiritual_conflict_v1","diceSource":"input/turn_request.json.preGeneratedDices1d20",
           "diceUsed":[{"side":"player","sourceIndex":0,"sides":20,"value":{{{{playerDie}}}}},{"side":"opposition","sourceIndex":1,"sides":20,"value":12}],
           "playerTotal":{{{{playerDie}}}},"oppositionTotal":12,"margin":{{{{(playerDie == 6 ? -6 : -8)}}}},"outcomeBand":"{{{{(playerDie == 6 ? "opposition_success" : "decisive_opposition_success")}}}}","modifierBreakdown":{"player":[],"opposition":[]}}}
        """)!.AsObject();
    }
}
