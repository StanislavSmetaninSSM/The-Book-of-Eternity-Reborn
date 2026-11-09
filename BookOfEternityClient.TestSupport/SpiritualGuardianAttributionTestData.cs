using System.Text;
using System.Text.Json.Nodes;
using BookOfEternityClient.Services;

namespace BookOfEternityClient.Tests;

/// <summary>Two closed guardian fixture destination pairs; neither changes the fixed beforeI or participant/action contract.</summary>
internal enum GuardianSingletonPressureContour { BelowBroken, ReachingBroken }

/// <summary>
/// Reconstructed guardian attribution DATA builders do not execute historical starts or wound births,
/// issue capabilities, or create accepted publication receipts. Each call owns independent mutable DATA.
/// </summary>
internal static class SpiritualGuardianAttributionTestData
{
    /// <summary>Builds an independent guardian singleton III/IV graph and complete synthetic first-create ledger joins.</summary>
    /// <param name="guardianId">Exact guardian source, target and carrier identity fixed before authoring.</param>
    /// <param name="rank">Explicit III or IV, defaulting to the unchanged III envelope and ledger.</param>
    internal static RetainedPressureGroupData CreateRetained(string guardianId = GuardianId, int rank = 3)
    {
        if (rank is not (3 or 4)) throw new ArgumentOutOfRangeException(nameof(rank));
        var rankName = rank == 4 ? "IV" : "III";
        const int parentCount = 1;
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
            var label = "retained_guardian_data_" + index;
            var exchange = CreateExchange(label + "_exchange", turn, "clear", rank == 4 ? "broken" : "overwhelmed", 15, 5, guardianId: guardianId);
            var source = CreateSource(exchange, label + "_session", label + "_request", label + "_snapshot",
                label + "_conflict", Fingerprint(new JsonObject { ["retainedDataSnapshot"] = label }));
            var eventKey = SpiritualWoundSourceWitness.HarmEventKey(label + "_session", label + "_request",
                label + "_snapshot", label + "_conflict", label + "_exchange", "opposition");
            var eventRef = "spiritual_wound_event_" + eventKey;
            var woundId = "wound_" + label;
            var effectId = "effect_" + label;
            var definitionKey = "definition_" + label;
            var woundTransitionId = "wound_transition_" + label;
            var effectTransitionId = "effect_transition_" + label;
            var definition = EffectMaterializationTestFixture.CreateSpiritualWoundDefinition(
                "spiritual_strain_burden", "guardian", "chaos_sea", woundId, definitionKey, "pressure");
            definition["components"]!.AsArray().Add(EffectMaterializationTestFixture.CreateSpiritualWoundComponent(
                "spiritual_roll_hindrance", "component_002", "counter"));
            definition["components"]!.AsArray().Add(EffectMaterializationTestFixture.CreateSpiritualWoundComponent(
                "spiritual_action_cost_burden", "component_003", "guard", JsonValue.Create(rank == 4 ? 3 : 2)));
            definition["triggers"]![0]!["componentIds"] = new JsonArray("component_001", "component_002", "component_003");
            if (rank == 4)
            {
                definition["components"]!.AsArray().Add(EffectMaterializationTestFixture.CreateSpiritualWoundComponent(
                    "spiritual_action_cost_burden", "component_004", "counter", JsonValue.Create(3)));
                definition["triggers"]![0]!["componentIds"]!.AsArray().Add("component_004");
            }
            var parent = WoundContractTestData.CreateSpiritualActiveWound(woundId, ownerKind: "guardian", ownerId: guardianId,
                carrierPath: WoundCarrierCatalog.AfterlifeProfilesPath);
            parent["origin"]!["eventRef"] = eventRef;
            parent["origin"]!["sourceKind"] = "spiritual_standard_art";
            parent["origin"]!["sourceId"] = "player_soul:player_soul:pressure";
            parent["origin"]!["createdAtTurn"] = turn;
            parent["origin"]!["opportunityId"] = "spiritual_wound_" + eventKey;
            parent["severity"] = new JsonObject { ["value"] = rankName, ["rank"] = rank,
                ["maximumAtCreation"] = rankName, ["lastChangeEventRef"] = eventRef };
            parent["recovery"]!["currentStepThreshold"] = rank * 2;
            parent["recovery"]!["recoveryAnchor"] = null;
            parent["recovery"]!["deteriorationAnchor"] = null;
            parent["lastTransition"] = new JsonObject { ["transitionId"] = woundTransitionId,
                ["ordinal"] = 1, ["turn"] = turn, ["kind"] = "create" };
            parent["consequences"] = new JsonObject
            {
                ["slotBudget"] = rank, ["slotsUsed"] = rank,
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
            if (rank == 4) parent["consequences"]!["entries"]!.AsArray().Add(
                Entry(4, "spiritual_action_cost_burden", effectId, "Контрприём требует больше духовных сил."));
            var effect = EffectMaterializationTestFixture.CreateSpiritualWoundCanonicalEffect(
                "spiritual_strain_burden", "guardian", "chaos_sea", woundId);
            effect["effectId"] = effectId;
            effect["target"]!["targetId"] = guardianId;
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
            identities.Add(WoundContractTestData.CreateIdentityEntry(woundId, "chaos_sea", "guardian", guardianId,
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
                ["decision"] = "materialize", ["selectedSeverityRank"] = rank, ["woundId"] = woundId,
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

    internal const string GuardianId = "guardian_frame";
    internal const string Role = "existing_guardian_pressure_failed_action_v1";

    /// <summary>Authors complete player pressure/success versus the guardian's exact incoming pressure action.</summary>
    internal static JsonObject CreateExchange(string exchangeId, int turn, string beforeStrain, string afterStrain,
        int playerDie = 12, int oppositionDie = 4, bool declaration = false, string guardianId = GuardianId)
    {
        var exchange = SpiritualExistingPressureTestData.ReadOrdinary(SpiritualExistingPressureTestData.CreateData());
        exchange["exchangeId"] = exchangeId; exchange["turnNumber"] = turn; exchange["exchangeAtTurn"] = turn;
        exchange["outcome"] = "success";
        exchange["summary"] = "Давление души усиливает напряжение Хранителя.";
        foreach (var phase in new[] { "before", "after" }) exchange[phase]!["playerSideStrain"] = "clear";
        exchange["before"]!["oppositionSideStrain"] = beforeStrain;
        exchange["after"]!["oppositionSideStrain"] = afterStrain;
        exchange["incomingAction"]!["actorId"] = guardianId;
        exchange["diceAudit"]!["diceUsed"]![0]!["value"] = playerDie;
        exchange["diceAudit"]!["diceUsed"]![1]!["value"] = oppositionDie;
        exchange["diceAudit"]!["playerTotal"] = playerDie; exchange["diceAudit"]!["oppositionTotal"] = oppositionDie;
        exchange["diceAudit"]!["margin"] = playerDie - oppositionDie;
        exchange["diceAudit"]!["outcomeBand"] = "decisive_player_success";
        if (declaration) exchange["spiritualWoundFailedAction"] = FailedAction(guardianId);
        return exchange;
    }

    /// <summary>Closed exchange-local declaration DATA; the guardian ID is a fixture example, not a production literal.</summary>
    internal static JsonObject FailedAction(string guardianId = GuardianId) => new()
    {
        ["version"] = 1, ["side"] = "opposition", ["actorType"] = "guardian", ["actorId"] = guardianId,
        ["actionPath"] = "/incomingAction", ["operationType"] = "pressure"
    };

    /// <summary>Builds intrinsically checkable source comparison DATA, never an issued live source or admission.</summary>
    internal static JsonObject CreateSource(JsonObject exchange, string session, string request, string token,
        string conflictId, string originalFingerprint)
    {
        var source = SpiritualExistingPressureTestData.CreateData(); source.Remove("strainPreimage");
        source["coordinate"] = "activeConflict.exchangeLog[0].opposition";
        source["affectedSide"] = "opposition";
        source["actingActor"] = new JsonObject { ["actorKind"] = "player_soul", ["actorId"] = "player_soul" };
        source["affectedActor"] = new JsonObject { ["actorKind"] = "guardian", ["actorId"] = exchange["incomingAction"]!["actorId"]!.DeepClone() }; source["conflictId"] = conflictId;
        source["exchangeId"] = exchange["exchangeId"]!.DeepClone();
        source["appliedArtId"] = "player_soul:player_soul:pressure";
        var playerDie = exchange["diceAudit"]!["diceUsed"]![0]!["value"]!.GetValue<int>();
        var oppositionDie = exchange["diceAudit"]!["diceUsed"]![1]!["value"]!.GetValue<int>();
        var calculation = SpiritualWoundOpportunityMath.Calculate(new(playerDie - oppositionDie, 0, 0,
            exchange["before"]!["oppositionSideStrain"]!.GetValue<string>(),
            exchange["after"]!["oppositionSideStrain"]!.GetValue<string>(), "hostile", 4))!;
        source["priorStrainRank"] = calculation.PreviousStrainRank; source["destinationStrainRank"] = calculation.NewStrainRank;
        source["extraStrainJumps"] = calculation.ExtraJumpSteps; source["harmfulMargin"] = playerDie - oppositionDie;
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

    /// <summary>Builds complete current-shaped DATA and exact BOM/whitespace raw input without issuing owners or signatures.</summary>
    /// <param name="guardianId">Exact declared incoming actor and retained parent owner.</param>
    /// <param name="rank">Explicit complete retained III/IV graph, fixed before comparison sealing.</param>
    /// <param name="contour">Closed ordinaryII/actualIII or ordinaryIII/actualIV DATA, preserving beforeI.</param>
    internal static SpiritualGuardianAttributionFixture Create(string guardianId = GuardianId, int rank = 3,
        GuardianSingletonPressureContour contour = GuardianSingletonPressureContour.BelowBroken)
    {
        var ordinaryStrain = OrdinaryStrain(contour);
        var actualStrain = ActualStrain(contour);
        var retained = CreateRetained(guardianId, rank);
        var profiles = new JsonArray();
        foreach (var (kind, id) in new[] { ("player_soul", "player_soul"), ("guardian", guardianId) })
        {
            var profile = AfterlifeActorMaterializationTestFixture.CreateCompleteProfile(kind, id, "Chaos Sea", 40);
            profile.Remove(ActorMaterializationContract.PropertyName);
            var arts = new JsonObject();
            foreach (var art in AfterlifeEntityProfileState.StandardArtIds) arts[art] = 0;
            profile["standardArts"] = arts;
            profile["activeWounds"] = kind == "guardian" ? new JsonArray(retained.Parents.Select(x => x.DeepClone()).ToArray()) : new JsonArray();
            profile["activeEffects"] = kind == "guardian" ? new JsonArray(retained.Effects.Select(x => x.DeepClone()).ToArray()) : new JsonArray();
            profiles.Add(profile);
        }
        var profileRoot = new JsonObject { ["schemaVersion"] = 1, ["profiles"] = profiles };
        var ordinary = CreateExchange("exchange_guardian_attribution_46", 46, "strained", ordinaryStrain, declaration: true, guardianId: guardianId);
        var actual = ordinary.DeepClone().AsObject(); actual["after"]!["oppositionSideStrain"] = actualStrain;
        var active = new JsonObject
        {
            ["conflictId"] = "conflict_guardian_attribution", ["realm"] = "Chaos Sea", ["sideModel"] = "direct_duel",
            ["dangerMode"] = "hostile", ["resolutionState"] = "active", ["playerSideStrain"] = "clear",
            ["oppositionSideStrain"] = ordinaryStrain, ["conflictPosition"] = "contested", ["controlState"] = null,
            ["tempoAdvantage"] = null, ["exchangeLog"] = new JsonArray(ordinary.DeepClone()),
            ["combatConditions"] = new JsonArray(),
            ["playerSide"] = Side("player", "player_soul"), ["oppositionSide"] = Side("guardian", guardianId)
        };
        var root = new JsonObject { ["schemaVersion"] = 1, ["activeConflict"] = active, ["recentConflicts"] = new JsonArray() };
        var bytes = Encoding.UTF8.GetPreamble().Concat(Encoding.UTF8.GetBytes(" \n" + root.ToJsonString() + "\n")).ToArray();
        var source = CreateSource(actual, "session_guardian_attribution", "request_guardian_attribution", "snapshot_guardian_attribution",
            active["conflictId"]!.GetValue<string>(), new CanonicalBeforeImage(true, bytes).Fingerprint);
        source["strainPreimage"] = new JsonObject
        {
            ["version"] = 5, ["inputRoleRevision"] = Role,
            ["ordinaryExchangeJsonBase64"] = Encode(ordinary), ["parentWoundJsonBase64"] = Encode(retained.Parents.Single()),
            ["effectId"] = retained.Effects.Single()["effectId"]!.DeepClone(), ["componentId"] = "component_001",
            ["originalInput"] = new JsonObject { ["path"] = AfterlifeSpiritualConflictState.StatePath, ["existed"] = true,
                ["contentFingerprint"] = new CanonicalBeforeImage(true, bytes).Fingerprint, ["exchangePointer"] = "/activeConflict/exchangeLog/0" },
            ["ordinaryCalculation"] = new JsonObject { ["traumaPressure"] = OrdinaryCalculation(contour).TraumaPressure,
                ["maximumSeverityRank"] = OrdinaryCalculation(contour).MaximumSeverityRank }
        };
        SpiritualExistingPressureTestData.Seal(source);
        return new(retained, profileRoot, root, ordinary, actual, source, bytes);
    }

    /// <summary>Names the independently authored ordinary destination of one closed guardian fixture contour.</summary>
    internal static string OrdinaryStrain(GuardianSingletonPressureContour contour) => contour switch
    {
        GuardianSingletonPressureContour.BelowBroken => "fractured",
        GuardianSingletonPressureContour.ReachingBroken => "overwhelmed",
        _ => throw new ArgumentOutOfRangeException(nameof(contour))
    };

    /// <summary>Names the independently expected actual destination after exactly one distinct parent contributes.</summary>
    internal static string ActualStrain(GuardianSingletonPressureContour contour) => contour switch
    {
        GuardianSingletonPressureContour.BelowBroken => "overwhelmed",
        GuardianSingletonPressureContour.ReachingBroken => "broken",
        _ => throw new ArgumentOutOfRangeException(nameof(contour))
    };

    /// <summary>Independent literal ordinary arithmetic at unchanged margin8, art0, resilience0 and beforeI.</summary>
    internal static SpiritualWoundCalculation OrdinaryCalculation(GuardianSingletonPressureContour contour) => contour switch
    {
        GuardianSingletonPressureContour.BelowBroken => new(new(8, 0, 0, "strained", "fractured", "hostile", 4), 1, 2, 0, 10, 1, 2, 4, true, 1),
        GuardianSingletonPressureContour.ReachingBroken => new(new(8, 0, 0, "strained", "overwhelmed", "hostile", 4), 1, 3, 1, 15, 2, 3, 4, true, 2),
        _ => throw new ArgumentOutOfRangeException(nameof(contour))
    };

    /// <summary>Independent literal actual arithmetic; reaching broken does not imply a rankIV new wound.</summary>
    internal static SpiritualWoundCalculation ActualCalculation(GuardianSingletonPressureContour contour) => contour switch
    {
        GuardianSingletonPressureContour.BelowBroken => new(new(8, 0, 0, "strained", "overwhelmed", "hostile", 4), 1, 3, 1, 15, 2, 3, 4, true, 2),
        GuardianSingletonPressureContour.ReachingBroken => new(new(8, 0, 0, "strained", "broken", "hostile", 4), 1, 4, 2, 20, 3, 4, 4, true, 3),
        _ => throw new ArgumentOutOfRangeException(nameof(contour))
    };

    /// <summary>Encodes complete semantic DATA; raw-input identity is separately computed from original bytes.</summary>
    internal static string Encode(JsonNode node) => Convert.ToBase64String(Encoding.UTF8.GetBytes(SpiritualWoundStateJson.Canonical(node)));

    private static JsonObject Side(string kind, string id) => new()
    {
        ["leadContestant"] = new JsonObject { ["actorType"] = kind, ["actorId"] = id }, ["supporters"] = new JsonArray()
    };

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

/// <summary>Independent mutable test DATA for one invocation; no field is an owner, lease or authority.</summary>
internal sealed record SpiritualGuardianAttributionFixture(RetainedPressureGroupData Retained, JsonObject Profiles,
    JsonObject Conflict, JsonObject Ordinary, JsonObject Actual, JsonObject Source, byte[] RawInput);
