using System.Text;
using System.Text.Json.Nodes;
using BookOfEternityClient.Services;

namespace BookOfEternityClient.Tests;

/// <summary>Independent pressure comparison DATA shared by typed and genuine signed-read tests, never source or publication authority.</summary>
internal static class SpiritualExistingPressureTestData
{
    private const string FixtureFingerprint = "sha256:aaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaa";
    /// <summary>Builds complete independent ordinary/actual/parent comparison DATA.</summary>
    internal static JsonObject CreateData()
    {
        var ordinary = SpiritualWoundStateJson.Parse("""
            {"exchangeId":"exchange_existing_pressure_44","turnNumber":44,"exchangeAtTurn":44,
             "operationType":"pressure","outcome":"setback","summary":"Душа встречает давление Хранителя.",
             "before":{"playerSideStrain":"strained","oppositionSideStrain":"clear","conflictPosition":"contested","controlState":null,"tempoAdvantage":null},
             "after":{"playerSideStrain":"fractured","oppositionSideStrain":"clear","conflictPosition":"contested","controlState":null,"tempoAdvantage":null},
             "incomingAction":{"operationType":"pressure","actorType":"guardian","actorId":"guardian-a","summary":"Хранитель оказывает давление."},
             "matchupAudit":{"playerOperation":"pressure","oppositionOperation":"pressure","primaryResolutionLane":"pressure","riskProfile":"offensive_pressure","matchupRationale":"Встречное давление."},
             "actionCostAudit":{
               "player":{"operationType":"pressure","baseCost":3,"minCost":1,"artTier":0,"effectiveCost":3,"before":3,"after":0,"max":10},
               "opposition":{"operationType":"pressure","baseCost":3,"minCost":1,"artTier":0,"effectiveCost":3,"before":3,"after":0,"max":9}},
             "diceAudit":{"formulaVersion":"afterlife_spiritual_conflict_v1","diceSource":"input/turn_request.json.preGeneratedDices1d20",
               "diceUsed":[{"side":"player","sourceIndex":0,"sides":20,"value":4},{"side":"opposition","sourceIndex":1,"sides":20,"value":12}],
               "playerTotal":4,"oppositionTotal":12,"margin":-8,"outcomeBand":"decisive_opposition_success","modifierBreakdown":{"player":[],"opposition":[]}}}
            """);
        var actual = ordinary.DeepClone().AsObject();
        actual["after"]!["playerSideStrain"] = "overwhelmed";
        var parent = CreateParent();
        var source = new JsonObject
        {
            ["sourceId"] = "", ["conflictId"] = "conflict-a", ["sourceOrdinal"] = 0, ["exchangeOrdinal"] = 0,
            ["turnEvidence"] = new JsonObject
            {
                ["sessionId"] = "session-a", ["requestId"] = "request-a", ["snapshotToken"] = "snapshot-a",
                ["turn"] = 42, ["realm"] = "chaos_sea", ["originalSnapshotFingerprint"] = FixtureFingerprint,
                ["bounds"] = new JsonObject { ["imagePathCount"] = 40 }
            },
            ["operation"] = "pressure", ["appliedArtKind"] = "standard", ["appliedArtTier"] = 0,
            ["targetResilienceTier"] = 0, ["guaranteedSeverityRank"] = null,
            ["dangerMode"] = "hostile", ["dangerDeclarationFingerprint"] = FixtureFingerprint,
            ["escalationFingerprint"] = null, ["retraumaWoundId"] = null, ["retraumaWoundJsonBase64"] = null,
            ["specialArtJsonBase64"] = null, ["selectedDiceIndices"] = new JsonArray(0, 1),
            ["resourceResultFingerprint"] = FixtureFingerprint, ["effectPlanFingerprint"] = FixtureFingerprint,
            ["conflictBeforeFingerprint"] = FixtureFingerprint, ["conflictAfterFingerprint"] = FixtureFingerprint,
            ["sourceFingerprint"] = ""
        };
        source["coordinate"] = "activeConflict.exchangeLog[2].player";
        source["exchangeId"] = "exchange_existing_pressure_44";
        source["affectedSide"] = "player";
        source["actingActor"] = new JsonObject { ["actorKind"] = "guardian", ["actorId"] = "guardian-a" };
        source["affectedActor"] = new JsonObject { ["actorKind"] = "player_soul", ["actorId"] = "player_soul" };
        source["appliedArtId"] = "guardian:guardian-a:pressure";
        source["priorStrainRank"] = 1; source["destinationStrainRank"] = 3;
        source["extraStrainJumps"] = 1; source["harmfulMargin"] = 8; source["maximumSeverityRank"] = 2;
        source["exchangeJsonBase64"] = Encode(actual);
        var evidence = source["turnEvidence"]!.AsObject();
        evidence["requestId"] = "request-existing-pressure44"; evidence["snapshotToken"] = "snapshot-existing-pressure44";
        evidence["turn"] = 44;
        evidence["bounds"]!["exchangeCount"] = 1; evidence["bounds"]!["sourceSlotCount"] = 2; evidence["bounds"]!["diceCount"] = 2;
        evidence["acceptedD20Values"] = new JsonArray(4, 12);
        var claims = new JsonArray();
        for (var index = 0; index < 2; index++)
        {
            var claim = new JsonObject { ["sourceIndex"] = index, ["value"] = index == 0 ? 4 : 12,
                ["exchangeOrdinal"] = 0, ["claimFingerprint"] = "" };
            claim["claimFingerprint"] = SpiritualWoundTurnEvidence.ComputeClaimFingerprint(claim); claims.Add(claim);
        }
        evidence["diceClaims"] = claims;
        // Deliberately retain exact whitespace/BOM bytes and a global pointer different from the turn-local ordinal.
        var originalBytes = Encoding.UTF8.GetPreamble().Concat(Encoding.UTF8.GetBytes(" \n" + new JsonObject
        {
            ["schemaVersion"] = 1, ["activeConflict"] = new JsonObject
            {
                ["exchangeLog"] = new JsonArray(new JsonObject(), new JsonObject(), ordinary.DeepClone()),
                ["playerSideStrain"] = "fractured"
            }
        }.ToJsonString() + "\n")).ToArray();
        source["strainPreimage"] = new JsonObject
        {
            ["version"] = 3, ["inputRoleRevision"] = "existing_pressure_ordinary_v1",
            ["ordinaryExchangeJsonBase64"] = Encode(ordinary), ["parentWoundJsonBase64"] = Encode(parent),
            ["effectId"] = "effect_pressure_strain", ["componentId"] = "component_001",
            ["originalInput"] = new JsonObject { ["path"] = AfterlifeSpiritualConflictState.StatePath,
                ["existed"] = true, ["contentFingerprint"] = new CanonicalBeforeImage(true, originalBytes).Fingerprint,
                ["exchangePointer"] = "/activeConflict/exchangeLog/2" },
            ["ordinaryCalculation"] = new JsonObject { ["traumaPressure"] = 10L, ["maximumSeverityRank"] = 1 }
        };
        Seal(source);
        return source;
    }

    private static JsonObject CreateParent()
    {
        const string woundId = "wound_pressure_parent";
        var parent = WoundContractTestData.CreateSpiritualActiveWound(woundId, ownerId: "player_soul",
            carrierPath: WoundCarrierCatalog.AfterlifeProfilesPath);
        parent["severity"]!["rank"] = 3; parent["severity"]!["value"] = "III"; parent["severity"]!["maximumAtCreation"] = "III";
        var definitions = new JsonArray(); var roots = new JsonArray(); var entries = new JsonArray();
        var rows = new[] { ("strain", "spiritual_strain_burden", "pressure"),
            ("roll", "spiritual_roll_hindrance", "counter"), ("cost", "spiritual_action_cost_burden", "guard") };
        for (var index = 0; index < rows.Length; index++)
        {
            var (name, profile, operation) = rows[index];
            var effect = "effect_pressure_" + name; var definition = "definition_pressure_" + name;
            definitions.Add(EffectMaterializationTestFixture.CreateSpiritualWoundDefinition(profile,
                woundId: woundId, realm: "chaos_sea", definitionKey: definition, targetKind: "player", operation: operation,
                magnitude: profile == "spiritual_action_cost_burden" ? JsonValue.Create(2) : null));
            roots.Add(WoundContractTestData.CreateRootBinding(effect, definition));
            entries.Add(new JsonObject { ["slot"] = index + 1, ["profileKey"] = profile,
                ["effectId"] = effect, ["readableSummary"] = "Духовное последствие: " + name });
        }
        parent["consequences"]!["slotBudget"] = 3; parent["consequences"]!["slotsUsed"] = 3;
        parent["consequences"]!["ownedEffectSources"] = new JsonObject { ["definitions"] = definitions, ["rootBindings"] = roots };
        parent["consequences"]!["entries"] = entries;
        return parent;
    }

    /// <summary>Builds a declined positive source receipt without claiming its accepted provenance.</summary>
    internal static (JsonObject Root, JsonObject Parent) CreateLedger(JsonObject source)
    {
        var parent = source["strainPreimage"] is null ? CreateParent() : ReadParent(source);
        var instance = new JsonObject
        {
            ["instanceId"] = "", ["ordinal"] = 1, ["displayConflictId"] = "conflict-a", ["realm"] = "chaos_sea",
            ["startSessionId"] = "session-a", ["startRequestId"] = "request-a", ["startSnapshotToken"] = "snapshot-a",
            ["startTurn"] = 42, ["baselineConflictFingerprint"] = "sha256:" + new string('a', 64), ["instanceFingerprint"] = ""
        };
        var hash = SpiritualWoundConflictInstanceState.ComputeRowFingerprint(instance, "instance");
        instance["instanceId"] = "spiritual_instance_" + hash[7..]; instance["instanceFingerprint"] = hash;
        var evidence = source["turnEvidence"]!;
        var decision = new JsonObject
        {
            ["ordinal"] = 1, ["decisionId"] = "decision_pressure_declined", ["instanceId"] = instance["instanceId"]!.DeepClone(),
            ["sourceId"] = source["sourceId"]!.DeepClone(), ["opportunityRef"] = "opportunity_pressure_declined",
            ["sourceWitness"] = source.DeepClone(),
            ["binding"] = new JsonObject
            {
                ["sessionId"] = evidence["sessionId"]!.DeepClone(), ["requestId"] = evidence["requestId"]!.DeepClone(),
                ["snapshotToken"] = evidence["snapshotToken"]!.DeepClone(), ["turn"] = evidence["turn"]!.DeepClone(),
                ["continuationGeneration"] = 1, ["waveOrdinal"] = 0, ["sourceOrdinal"] = 0
            },
            ["decision"] = "none", ["selectedSeverityRank"] = null, ["woundId"] = null, ["transitionId"] = null,
            ["sourceFingerprint"] = source["sourceFingerprint"]!.DeepClone(), ["decisionFingerprint"] = ""
        };
        decision["decisionFingerprint"] = SpiritualWoundStateJson.Hash(decision, "decision", "decisionFingerprint");
        return (new JsonObject
        {
            ["schemaVersion"] = 1, ["nextInstanceOrdinal"] = 2, ["nextClosureOrdinal"] = 1,
            ["nextSourceOrdinal"] = 2, ["nextDecisionOrdinal"] = 2,
            ["instances"] = new JsonArray(instance), ["closures"] = new JsonArray(),
            ["sources"] = new JsonArray(new JsonObject
            {
                ["ordinal"] = 1, ["instanceId"] = instance["instanceId"]!.DeepClone(),
                ["instanceSourceOrdinal"] = 1, ["witness"] = source.DeepClone()
            }),
            ["decisions"] = new JsonArray(decision)
        }, parent);
    }

    /// <summary>Builds History DATA with the exact recorded parent transition.</summary>
    internal static JsonObject CreateHistory(JsonObject parent)
    {
        var wound = WoundMaterializationContract.Parse(parent.ToJsonString(), "unit.pressureParent").Wound!;
        var transition = WoundContractTestData.CreateTransition();
        transition["transitionId"] = wound.LastTransition.TransitionId; transition["woundId"] = wound.WoundId;
        transition["turn"] = wound.LastTransition.Turn; transition["eventRef"] = wound.Origin.EventRef;
        transition["attemptId"] = null;
        transition["beforeFingerprint"] = WoundHistoryState.ComputeNonexistentBeforeFingerprint(wound.WoundId);
        transition["afterFingerprint"] = WoundIdentityState.ComputeSemanticFingerprint(wound);
        transition["sourceFingerprint"] = "sha256:" + new string('a', 64);
        if (wound.LastTransition.Ordinal == 1)
            return WoundContractTestData.CreateHistory(transition);
        if ((wound.LastTransition.Ordinal, wound.LastTransition.Kind) != (2, "complicate"))
            throw new InvalidOperationException("Unsupported fixture parent transition.");
        var initial = transition.DeepClone().AsObject();
        initial["transitionId"] = "transition_pressure_create";
        initial["turn"] = wound.Origin.CreatedAtTurn;
        initial["afterFingerprint"] = "sha256:" + new string('b', 64);
        transition["ordinal"] = 2; transition["woundTransitionOrdinal"] = 2;
        transition["kind"] = wound.LastTransition.Kind;
        transition["operationKey"] = "operation_pressure_complicate";
        transition["eventRef"] = "event_pressure_complicate";
        transition["beforeFingerprint"] = initial["afterFingerprint"]!.DeepClone();
        var history = WoundContractTestData.CreateHistory(initial);
        history["transitions"]!.AsArray().Add(transition); history["nextOrdinal"] = 3;
        return history;
    }

    /// <summary>Returns detached actual exchange DATA, never membership or provenance.</summary>
    internal static JsonObject ReadActual(JsonObject source) => DecodeObject(source["exchangeJsonBase64"]);
    /// <summary>Returns detached proposed ordinary exchange DATA.</summary>
    internal static JsonObject ReadOrdinary(JsonObject source) => DecodeObject(source["strainPreimage"]!["ordinaryExchangeJsonBase64"]);
    /// <summary>Returns detached recorded parent DATA.</summary>
    internal static JsonObject ReadParent(JsonObject source) => DecodeObject(source["strainPreimage"]!["parentWoundJsonBase64"]);
    /// <summary>Reseals lexical fixture DATA without creating accepted provenance.</summary>
    internal static void Seal(JsonObject source)
    {
        var digest = SpiritualWoundStateJson.Hash(source, "source", "sourceId", "sourceFingerprint");
        source["sourceFingerprint"] = digest; source["sourceId"] = "spiritual_source_" + digest[7..];
    }
    private static JsonObject DecodeObject(JsonNode? node) => SpiritualWoundStateJson.Parse(
        SpiritualWoundStateJson.DecodeUtf8JsonText(SpiritualWoundStateJson.DecodeJsonObjectBytes(node)));
    private static string Encode(JsonNode node) => Convert.ToBase64String(Encoding.UTF8.GetBytes(SpiritualWoundStateJson.Canonical(node)));
}
