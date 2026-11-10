using System.Text;
using System.Text.Json.Nodes;
using BookOfEternityClient.Services;

namespace BookOfEternityClient.Tests;

/// <summary>
/// Complete synthetic retained pressure DATA. These builders do not execute historical
/// starts or wound births, issue capabilities, or create accepted publication receipts.
/// Each call returns independent mutable DATA for one test's real current owners.
/// </summary>
internal static class SpiritualRetainedPressureGroupTestData
{
    /// <summary>Builds one or two independent III parent graphs and their complete first-create ledger joins.</summary>
    internal static RetainedPressureGroupData Create(int parentCount)
    {
        if (parentCount is not (1 or 2)) throw new ArgumentOutOfRangeException(nameof(parentCount));
        var parents = new List<JsonObject>();
        var effects = new List<JsonObject>();
        var identities = new List<JsonObject>();
        var transitions = new List<JsonObject>();
        var receipt = new JsonObject
        {
            ["schemaVersion"] = 1, ["nextInstanceOrdinal"] = parentCount + 1,
            ["nextClosureOrdinal"] = parentCount + 1, ["nextSourceOrdinal"] = parentCount + 1,
            ["nextDecisionOrdinal"] = parentCount + 1, ["instances"] = new JsonArray(),
            ["closures"] = new JsonArray(), ["sources"] = new JsonArray(), ["decisions"] = new JsonArray()
        };
        for (var index = 1; index <= parentCount; index++)
        {
            var turn = 40 + 2 * index;
            var label = "retained_pressure_data_" + index;
            var exchange = CreateExchange(label + "_exchange", turn, "clear", "overwhelmed", 5, 15);
            var source = CreateSource(exchange, label + "_session", label + "_request", label + "_snapshot",
                label + "_conflict", Fingerprint(new JsonObject { ["retainedDataSnapshot"] = label }));
            var eventKey = SpiritualWoundSourceWitness.HarmEventKey(label + "_session", label + "_request",
                label + "_snapshot", label + "_conflict", label + "_exchange", "player");
            var eventRef = "spiritual_wound_event_" + eventKey;
            var woundId = "wound_" + label;
            var effectId = "effect_" + label;
            var definitionKey = "definition_" + label;
            var woundTransitionId = "wound_transition_" + label;
            var effectTransitionId = "effect_transition_" + label;
            var definition = EffectMaterializationTestFixture.CreateSpiritualWoundDefinition(
                "spiritual_strain_burden", "player", "chaos_sea", woundId, definitionKey, "pressure");
            definition["components"]!.AsArray().Add(EffectMaterializationTestFixture.CreateSpiritualWoundComponent(
                "spiritual_roll_hindrance", "component_002", "counter"));
            definition["components"]!.AsArray().Add(EffectMaterializationTestFixture.CreateSpiritualWoundComponent(
                "spiritual_action_cost_burden", "component_003", "guard", JsonValue.Create(2)));
            definition["triggers"]![0]!["componentIds"] = new JsonArray("component_001", "component_002", "component_003");
            var parent = WoundContractTestData.CreateSpiritualActiveWound(woundId, ownerId: "player_soul",
                carrierPath: WoundCarrierCatalog.AfterlifeProfilesPath);
            parent["origin"]!["eventRef"] = eventRef;
            parent["origin"]!["sourceKind"] = "spiritual_standard_art";
            parent["origin"]!["sourceId"] = "guardian:guardian_frame:pressure";
            parent["origin"]!["createdAtTurn"] = turn;
            parent["origin"]!["opportunityId"] = "spiritual_wound_" + eventKey;
            parent["severity"] = new JsonObject { ["value"] = "III", ["rank"] = 3,
                ["maximumAtCreation"] = "III", ["lastChangeEventRef"] = eventRef };
            parent["recovery"]!["currentStepThreshold"] = 6;
            parent["recovery"]!["recoveryAnchor"] = null;
            parent["recovery"]!["deteriorationAnchor"] = null;
            parent["lastTransition"] = new JsonObject { ["transitionId"] = woundTransitionId,
                ["ordinal"] = 1, ["turn"] = turn, ["kind"] = "create" };
            parent["consequences"] = new JsonObject
            {
                ["slotBudget"] = 3, ["slotsUsed"] = 3,
                ["ownedEffectSources"] = new JsonObject
                {
                    ["definitions"] = new JsonArray(definition),
                    ["rootBindings"] = new JsonArray(WoundContractTestData.CreateRootBinding(effectId, definitionKey))
                },
                ["entries"] = new JsonArray(
                    Entry(1, "spiritual_strain_burden", effectId, "Провал давления усиливает надлом."),
                    Entry(2, "spiritual_roll_hindrance", effectId, "Контрприём душе даётся труднее."),
                    Entry(3, "spiritual_action_cost_burden", effectId, "Защита требует больше духовных сил."))
            };
            var effect = EffectMaterializationTestFixture.CreateSpiritualWoundCanonicalEffect(
                "spiritual_strain_burden", "player", "chaos_sea", woundId);
            effect["effectId"] = effectId;
            effect["target"]!["targetId"] = "player_soul";
            effect["source"]!["definitionKey"] = definitionKey;
            // Bind the complete retained display before signing; the optional source label is instance-owned.
            var sourceLabel = effect["display"]!["sourceLabel"]?.DeepClone();
            effect["display"] = definition["display"]!.DeepClone();
            if (sourceLabel is not null) effect["display"]!["sourceLabel"] = sourceLabel;
            foreach (var field in new[] { "components", "triggers", "links" }) effect[field] = definition[field]!.DeepClone();
            effect["stacking"]!["stackKey"] = definition["stacking"]!["stackKey"]!.DeepClone();
            effect["chronology"] = new JsonObject { ["createdAtTurn"] = turn, ["createdEventRef"] = eventRef,
                ["lastTransitionId"] = effectTransitionId, ["lastTransitionTurn"] = turn };
            var parsed = WoundMaterializationContract.Parse(parent.ToJsonString(), "fixture.retainedPressureParent");
            if (!parsed.IsValid) throw new InvalidDataException(string.Join("\n", parsed.Issues));
            var canonicalParentJson = WoundMaterializationContract.SerializeCanonical(parsed.Wound!);
            if (!JsonNode.DeepEquals(parent, JsonNode.Parse(canonicalParentJson)))
                throw new InvalidDataException($"Retained pressure parent '{woundId}' failed the complete-envelope " +
                    $"authored JSON -> WoundMaterializationContract.SerializeCanonical boundary.\n" +
                    $"Authored JSON: {parent.ToJsonString()}\nCanonical JSON: {canonicalParentJson}");
            var semanticFingerprint = WoundIdentityState.ComputeSemanticFingerprint(parsed.Wound!);
            identities.Add(WoundContractTestData.CreateIdentityEntry(woundId, "chaos_sea", "player_soul", "player_soul",
                WoundCarrierCatalog.AfterlifeProfilesPath, "spiritual", createdAtTurn: turn,
                createdEventRef: eventRef, semanticFingerprint: semanticFingerprint));
            var transition = WoundContractTestData.CreateTransition(ordinal: index);
            transition["transitionId"] = woundTransitionId; transition["woundId"] = woundId;
            transition["turn"] = turn; transition["eventRef"] = eventRef;
            transition["operationKey"] = "operation_" + label; transition["attemptId"] = null;
            transition["beforeFingerprint"] = WoundHistoryState.ComputeNonexistentBeforeFingerprint(woundId);
            transition["afterFingerprint"] = semanticFingerprint;
            transition["sourceFingerprint"] = source["sourceFingerprint"]!.DeepClone();
            transition["outputFingerprint"] = Fingerprint(parent);
            transitions.Add(transition); parents.Add(parent); effects.Add(effect);

            var instance = new JsonObject
            {
                ["instanceId"] = "", ["ordinal"] = index, ["displayConflictId"] = label + "_conflict",
                ["realm"] = "chaos_sea", ["startSessionId"] = label + "_session",
                ["startRequestId"] = label + "_request", ["startSnapshotToken"] = label + "_snapshot",
                ["startTurn"] = turn, ["baselineConflictFingerprint"] = source["conflictBeforeFingerprint"]!.DeepClone(),
                ["instanceFingerprint"] = ""
            };
            SealLifecycleRow(instance, "instance");
            receipt["instances"]!.AsArray().Add(instance);
            var closure = new JsonObject
            {
                ["closureId"] = "", ["ordinal"] = index, ["instanceId"] = instance["instanceId"]!.DeepClone(),
                ["terminalTurn"] = turn, ["terminalEventRef"] = label + "_terminal",
                ["terminalConflictFingerprint"] = source["conflictAfterFingerprint"]!.DeepClone(), ["closureFingerprint"] = ""
            };
            SealLifecycleRow(closure, "closure"); receipt["closures"]!.AsArray().Add(closure);
            receipt["sources"]!.AsArray().Add(new JsonObject { ["ordinal"] = index,
                ["instanceId"] = instance["instanceId"]!.DeepClone(), ["instanceSourceOrdinal"] = 1, ["witness"] = source });
            var evidence = source["turnEvidence"]!;
            var decision = new JsonObject
            {
                ["ordinal"] = index, ["decisionId"] = "decision_" + label,
                ["instanceId"] = instance["instanceId"]!.DeepClone(), ["sourceId"] = source["sourceId"]!.DeepClone(),
                ["opportunityRef"] = "spiritual_wound_" + eventKey, ["sourceWitness"] = source.DeepClone(),
                ["binding"] = new JsonObject { ["sessionId"] = evidence["sessionId"]!.DeepClone(),
                    ["requestId"] = evidence["requestId"]!.DeepClone(), ["snapshotToken"] = evidence["snapshotToken"]!.DeepClone(),
                    ["turn"] = turn, ["continuationGeneration"] = 1, ["waveOrdinal"] = 0, ["sourceOrdinal"] = 0 },
                ["decision"] = "materialize", ["selectedSeverityRank"] = 3, ["woundId"] = woundId,
                ["transitionId"] = woundTransitionId, ["sourceFingerprint"] = source["sourceFingerprint"]!.DeepClone(),
                ["decisionFingerprint"] = ""
            };
            decision["decisionFingerprint"] = SpiritualWoundStateJson.Hash(decision, "decision", "decisionFingerprint");
            receipt["decisions"]!.AsArray().Add(decision);
        }
        var effectIdentity = EffectMaterializationTestFixture.CreateIdentityIndex(effects.ToArray());
        for (var index = 0; index < parentCount; index++)
        {
            var entry = effectIdentity["entries"]![index]!;
            var effect = effects[index];
            entry["owner"]!["carrierPath"] = EffectCarrierCatalog.AfterlifeProfilesPath;
            entry["createdAtTurn"] = effect["chronology"]!["createdAtTurn"]!.DeepClone();
            var transition = entry["transitions"]![0]!;
            transition["transitionId"] = effect["chronology"]!["lastTransitionId"]!.DeepClone();
            transition["turn"] = effect["chronology"]!["createdAtTurn"]!.DeepClone();
            transition["eventRef"] = effect["chronology"]!["createdEventRef"]!.DeepClone();
        }
        return new(parents, effects, WoundContractTestData.CreateIdentityIndex(identities.ToArray()),
            WoundContractTestData.CreateHistory(transitions.ToArray()), effectIdentity, receipt);
    }

    /// <summary>Authors a complete ordinary standard pressure/setback DATA exchange, independently of any live reader.</summary>
    internal static JsonObject CreateExchange(string exchangeId, int turn, string beforeStrain, string afterStrain,
        int playerDie = 4, int oppositionDie = 12)
    {
        var exchange = SpiritualExistingPressureTestData.ReadOrdinary(SpiritualExistingPressureTestData.CreateData());
        exchange["exchangeId"] = exchangeId; exchange["turnNumber"] = turn; exchange["exchangeAtTurn"] = turn;
        exchange["before"]!["playerSideStrain"] = beforeStrain; exchange["after"]!["playerSideStrain"] = afterStrain;
        exchange["incomingAction"]!["actorId"] = "guardian_frame";
        exchange["diceAudit"]!["diceUsed"]![0]!["value"] = playerDie;
        exchange["diceAudit"]!["diceUsed"]![1]!["value"] = oppositionDie;
        exchange["diceAudit"]!["playerTotal"] = playerDie; exchange["diceAudit"]!["oppositionTotal"] = oppositionDie;
        exchange["diceAudit"]!["margin"] = playerDie - oppositionDie;
        return exchange;
    }

    /// <summary>Builds intrinsically checkable source comparison DATA, never an issued live source or admission.</summary>
    internal static JsonObject CreateSource(JsonObject exchange, string session, string request, string token,
        string conflictId, string originalFingerprint)
    {
        var source = SpiritualExistingPressureTestData.CreateData(); source.Remove("strainPreimage");
        source["coordinate"] = "activeConflict.exchangeLog[0].player"; source["conflictId"] = conflictId;
        source["exchangeId"] = exchange["exchangeId"]!.DeepClone();
        source["actingActor"]!["actorId"] = "guardian_frame";
        source["appliedArtId"] = "guardian:guardian_frame:pressure";
        var playerDie = exchange["diceAudit"]!["diceUsed"]![0]!["value"]!.GetValue<int>();
        var oppositionDie = exchange["diceAudit"]!["diceUsed"]![1]!["value"]!.GetValue<int>();
        var calculation = SpiritualWoundOpportunityMath.Calculate(new(oppositionDie - playerDie, 0, 0,
            exchange["before"]!["playerSideStrain"]!.GetValue<string>(),
            exchange["after"]!["playerSideStrain"]!.GetValue<string>(), "hostile", 4))!;
        source["priorStrainRank"] = calculation.PreviousStrainRank; source["destinationStrainRank"] = calculation.NewStrainRank;
        source["extraStrainJumps"] = calculation.ExtraJumpSteps; source["harmfulMargin"] = oppositionDie - playerDie;
        source["maximumSeverityRank"] = calculation.MaximumSeverityRank;
        source["exchangeJsonBase64"] = Convert.ToBase64String(Encoding.UTF8.GetBytes(SpiritualWoundStateJson.Canonical(exchange)));
        var evidence = source["turnEvidence"]!;
        evidence["sessionId"] = session; evidence["requestId"] = request; evidence["snapshotToken"] = token;
        evidence["turn"] = exchange["turnNumber"]!.DeepClone(); evidence["originalSnapshotFingerprint"] = originalFingerprint;
        evidence["acceptedD20Values"] = new JsonArray(playerDie, oppositionDie);
        var claims = new JsonArray();
        for (var index = 0; index < 2; index++)
        {
            var claim = new JsonObject { ["sourceIndex"] = index, ["value"] = index == 0 ? playerDie : oppositionDie,
                ["exchangeOrdinal"] = 0, ["claimFingerprint"] = "" };
            claim["claimFingerprint"] = SpiritualWoundTurnEvidence.ComputeClaimFingerprint(claim); claims.Add(claim);
        }
        evidence["diceClaims"] = claims;
        source["conflictBeforeFingerprint"] = Fingerprint(exchange["before"]!);
        source["conflictAfterFingerprint"] = Fingerprint(exchange["after"]!);
        source["resourceResultFingerprint"] = Fingerprint(exchange["actionCostAudit"]!);
        source["effectPlanFingerprint"] = Fingerprint(new JsonObject { ["retainedDataEffectPlan"] = conflictId });
        source["dangerDeclarationFingerprint"] = Fingerprint(new JsonObject { ["dangerMode"] = "hostile" });
        SpiritualExistingPressureTestData.Seal(source);
        return source;
    }

    /// <summary>Creates one readable consequence slot linked to this retained parent's exact root effect.</summary>
    private static JsonObject Entry(int slot, string profile, string effectId, string summary) => new()
        { ["slot"] = slot, ["profileKey"] = profile, ["effectId"] = effectId, ["readableSummary"] = summary };
    /// <summary>Computes a canonical synthetic DATA fingerprint without authenticating historical execution.</summary>
    private static string Fingerprint(JsonNode data) => new CanonicalBeforeImage(true,
        Encoding.UTF8.GetBytes(SpiritualWoundStateJson.Canonical(data))).Fingerprint;
    /// <summary>Derives a retained instance or closure DATA row's identity and digest, without issuing live authority.</summary>
    private static void SealLifecycleRow(JsonObject row, string kind)
    {
        var hash = SpiritualWoundConflictInstanceState.ComputeRowFingerprint(row, kind);
        row[kind + "Id"] = "spiritual_" + kind + "_" + hash[7..]; row[kind + "Fingerprint"] = hash;
    }
}

/// <summary>One Fact's independent synthetic graphs and complete recorded-provenance comparison DATA.</summary>
internal sealed record RetainedPressureGroupData(IReadOnlyList<JsonObject> Parents, IReadOnlyList<JsonObject> Effects,
    JsonObject WoundIdentities, JsonObject History, JsonObject EffectIdentities, JsonObject Receipts);
