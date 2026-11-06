using System.Text;
using System.Text.Json.Nodes;
using BookOfEternityClient.Services;

namespace BookOfEternityClient.Tests;

/// <summary>Selects only the independently authored third guard's pre-sign trigger.</summary>
internal enum SpiritualPressureThirdGuardTrigger { TurnEnd, OwnApSpent }

/// <summary>
/// Authors independent retained comparison inputs before signing. The accepted r3 factory is
/// an immutable shape prototype, not a live owner or a claim that a historical birth executed.
/// </summary>
internal static class SpiritualPressureGroupOwnerCaseData
{
    /// <summary>Authors the complete graph, identity, first-create History and receipt joins for one Fact.</summary>
    /// <param name="strainParents">One to three complete strain parents, with a third parent remaining the existing III refusal control.</param>
    /// <param name="selectedPressureCost">Moves the second parent's lawful severity-specific cost to pressure.</param>
    /// <param name="otherPressureCost">Appends the existing independent rank-I pressure-cost-only wound.</param>
    /// <param name="pressureRoll">Moves the second parent's roll hindrance to pressure without manufacturing its later dice audit.</param>
    /// <param name="ranks">Severities of the first two canonical wound-ID positions, fixed before all provenance DATA is authored.</param>
    /// <param name="thirdGuard">The existing III+III transition-only guard variant, incompatible with changed ranks.</param>
    /// <param name="rankFourPressureArt">Uses the second IV parent's fourth slot for a genuine pressure prohibition.</param>
    internal static RetainedPressureGroupData Create(int strainParents, bool selectedPressureCost = false,
        bool otherPressureCost = false, bool pressureRoll = false, SpiritualPressureParentRanks? ranks = null,
        SpiritualPressureThirdGuardTrigger? thirdGuard = null, bool rankFourPressureArt = false)
    {
        ranks ??= new();
        if (strainParents is < 1 or > 3) throw new ArgumentOutOfRangeException(nameof(strainParents));
        if (rankFourPressureArt && (strainParents != 2 || ranks.Second != 4))
            throw new ArgumentException("The pressure prohibition requires the second complete rank-IV parent.", nameof(rankFourPressureArt));
        if (thirdGuard.HasValue && (strainParents != 2 || otherPressureCost || selectedPressureCost || pressureRoll ||
            ranks != new SpiritualPressureParentRanks()))
            throw new ArgumentException("The transition guard requires the unchanged two-parent comparison graph.", nameof(thirdGuard));
        if (thirdGuard.HasValue && !Enum.IsDefined(thirdGuard.Value)) throw new ArgumentOutOfRangeException(nameof(thirdGuard));
        var prototype = SpiritualRetainedPressureGroupTestData.Create(1);
        var count = strainParents + (otherPressureCost || thirdGuard.HasValue ? 1 : 0);
        var parents = new List<JsonObject>();
        var effects = new List<JsonObject>();
        var identities = new List<JsonObject>();
        var history = new List<JsonObject>();
        var receipt = prototype.Receipts.DeepClone().AsObject();
        foreach (var field in new[] { "instances", "closures", "sources", "decisions" }) receipt[field] = new JsonArray();
        foreach (var field in new[] { "nextInstanceOrdinal", "nextClosureOrdinal", "nextSourceOrdinal", "nextDecisionOrdinal" })
            receipt[field] = count + 1;
        for (var index = 1; index <= count; index++)
        {
            var label = "retained_pressure_owner_" + index;
            JsonObject Copy(JsonObject value) => JsonNode.Parse(value.ToJsonString()
                .Replace("retained_pressure_data_1", label, StringComparison.Ordinal))!.AsObject();
            var parent = Copy(prototype.Parents[0]);
            var effect = Copy(prototype.Effects[0]);
            var costOnly = index > strainParents;
            var rank = costOnly ? 1 : index == 1 ? ranks.First : index == 2 ? ranks.Second : 3;
            var rankName = new[] { "", "I", "II", "III", "IV" }[rank];
            var turn = 40 + index;
            var woundId = parent["woundId"]!.GetValue<string>();
            var effectId = effect["effectId"]!.GetValue<string>();
            var definition = parent["consequences"]!["ownedEffectSources"]!["definitions"]![0]!.AsObject();
            if (costOnly)
            {
                definition["components"] = new JsonArray(EffectMaterializationTestFixture.CreateSpiritualWoundComponent(
                    "spiritual_action_cost_burden", "component_001", thirdGuard.HasValue ? "guard" : "pressure", JsonValue.Create(1)));
                definition["triggers"]![0]!["componentIds"] = new JsonArray("component_001");
                parent["consequences"]!["entries"] = new JsonArray(new JsonObject
                {
                    ["slot"] = 1, ["profileKey"] = "spiritual_action_cost_burden", ["effectId"] = effectId,
                    ["readableSummary"] = thirdGuard.HasValue ? "Защита требует дополнительной духовной силы." : "Давление требует дополнительной духовной силы."
                });
            }
            else
            {
                if (pressureRoll && index == 2) definition["components"]![1]!["payload"]!["operation"] = "pressure";
                if (selectedPressureCost && index == 2) definition["components"]![2]!["payload"]!["operation"] = "pressure";
                if (rank == 4)
                {
                    definition["components"]![2]!["payload"]!["magnitude"] = 3;
                    definition["components"]!.AsArray().Add(EffectMaterializationTestFixture.CreateSpiritualWoundComponent(
                        rankFourPressureArt && index == 2 ? "spiritual_art_restriction" : "spiritual_action_cost_burden",
                        "component_004", rankFourPressureArt && index == 2 ? "pressure" : "counter",
                        rankFourPressureArt && index == 2 ? JsonValue.Create("forbid") : JsonValue.Create(3)));
                    definition["triggers"]![0]!["componentIds"]!.AsArray().Add("component_004");
                    parent["consequences"]!["entries"]!.AsArray().Add(new JsonObject
                    {
                        ["slot"] = 4, ["profileKey"] = rankFourPressureArt && index == 2 ? "spiritual_art_restriction" : "spiritual_action_cost_burden",
                        ["effectId"] = effectId,
                        ["readableSummary"] = rankFourPressureArt && index == 2 ? "Рана запрещает давление." : "Контрприём требует больше духовных сил."
                    });
                }
            }
            if (costOnly && thirdGuard.HasValue)
            {
                var spent = thirdGuard == SpiritualPressureThirdGuardTrigger.OwnApSpent;
                definition["triggers"] = new JsonArray(new JsonObject
                {
                    ["triggerId"] = spent ? "own_ap_spent" : "on_owner_turn_end",
                    ["eventType"] = spent ? "resource_spent" : "owner_turn_end", ["priority"] = 100,
                    ["componentIds"] = new JsonArray("component_001"), ["consumeUses"] = false,
                    ["resolutionMode"] = "deterministic"
                });
            }
            parent["consequences"]!["slotBudget"] = rank;
            parent["consequences"]!["slotsUsed"] = rank;
            parent["severity"]!["rank"] = rank;
            parent["severity"]!["value"] = rankName;
            parent["severity"]!["maximumAtCreation"] = rankName;
            parent["recovery"]!["currentStepThreshold"] = 2 * rank;
            foreach (var field in new[] { "components", "triggers", "links" }) effect[field] = definition[field]!.DeepClone();

            var oldExchange = SpiritualRetainedPressureGroupTestData.CreateExchange(label + "_exchange", turn,
                "clear", costOnly ? "strained" : rank == 4 ? "broken" : "overwhelmed", 5, 15);
            var source = SpiritualRetainedPressureGroupTestData.CreateSource(oldExchange, label + "_session",
                label + "_request", label + "_snapshot", label + "_conflict", Fingerprint(new JsonObject
                    { ["retainedDataSnapshot"] = label }));
            var key = SpiritualWoundSourceWitness.HarmEventKey(label + "_session", label + "_request",
                label + "_snapshot", label + "_conflict", label + "_exchange", "player");
            var eventRef = "spiritual_wound_event_" + key;
            parent["origin"]!["eventRef"] = eventRef;
            parent["origin"]!["createdAtTurn"] = turn;
            parent["origin"]!["opportunityId"] = "spiritual_wound_" + key;
            parent["severity"]!["lastChangeEventRef"] = eventRef;
            parent["lastTransition"]!["turn"] = turn;
            effect["chronology"]!["createdAtTurn"] = turn;
            effect["chronology"]!["createdEventRef"] = eventRef;
            effect["chronology"]!["lastTransitionTurn"] = turn;
            var parsed = WoundMaterializationContract.Parse(parent.ToJsonString(), "fixture.authoredPressureOwner");
            if (!parsed.IsValid) throw new InvalidDataException(string.Join("\n", parsed.Issues));
            var canonical = WoundMaterializationContract.SerializeCanonical(parsed.Wound!);
            if (!JsonNode.DeepEquals(parent, JsonNode.Parse(canonical)))
                throw new InvalidDataException("Authored retained pressure parent must already be canonical.");
            var semantic = WoundIdentityState.ComputeSemanticFingerprint(parsed.Wound!);
            identities.Add(WoundContractTestData.CreateIdentityEntry(woundId, "chaos_sea", "player_soul", "player_soul",
                WoundCarrierCatalog.AfterlifeProfilesPath, "spiritual", createdAtTurn: turn,
                createdEventRef: eventRef, semanticFingerprint: semantic));
            var transition = Copy(prototype.History["transitions"]![0]!.AsObject());
            transition["ordinal"] = index;
            transition["turn"] = turn;
            transition["eventRef"] = eventRef;
            transition["beforeFingerprint"] = WoundHistoryState.ComputeNonexistentBeforeFingerprint(woundId);
            transition["afterFingerprint"] = semantic;
            transition["sourceFingerprint"] = source["sourceFingerprint"]!.DeepClone();
            transition["outputFingerprint"] = Fingerprint(parent);
            parents.Add(parent); effects.Add(effect); history.Add(transition);

            var instance = Copy(prototype.Receipts["instances"]![0]!.AsObject());
            instance["ordinal"] = index;
            instance["startTurn"] = turn;
            instance["baselineConflictFingerprint"] = source["conflictBeforeFingerprint"]!.DeepClone();
            SealLifecycle(instance, "instance");
            receipt["instances"]!.AsArray().Add(instance);
            var closure = Copy(prototype.Receipts["closures"]![0]!.AsObject());
            closure["ordinal"] = index;
            closure["instanceId"] = instance["instanceId"]!.DeepClone();
            closure["terminalTurn"] = turn;
            closure["terminalConflictFingerprint"] = source["conflictAfterFingerprint"]!.DeepClone();
            SealLifecycle(closure, "closure");
            receipt["closures"]!.AsArray().Add(closure);
            receipt["sources"]!.AsArray().Add(new JsonObject { ["ordinal"] = index,
                ["instanceId"] = instance["instanceId"]!.DeepClone(), ["instanceSourceOrdinal"] = 1,
                ["witness"] = source.DeepClone() });
            var decision = Copy(prototype.Receipts["decisions"]![0]!.AsObject());
            decision["ordinal"] = index;
            decision["instanceId"] = instance["instanceId"]!.DeepClone();
            decision["sourceId"] = source["sourceId"]!.DeepClone();
            decision["opportunityRef"] = "spiritual_wound_" + key;
            decision["sourceWitness"] = source.DeepClone();
            decision["binding"]!["turn"] = turn;
            decision["selectedSeverityRank"] = rank;
            decision["sourceFingerprint"] = source["sourceFingerprint"]!.DeepClone();
            decision["decisionFingerprint"] = SpiritualWoundStateJson.Hash(decision, "decision", "decisionFingerprint");
            receipt["decisions"]!.AsArray().Add(decision);
        }
        var effectIdentities = EffectMaterializationTestFixture.CreateIdentityIndex(effects.ToArray());
        for (var index = 0; index < effects.Count; index++)
        {
            var effect = effects[index];
            var entry = effectIdentities["entries"]![index]!;
            entry["owner"]!["carrierPath"] = EffectCarrierCatalog.AfterlifeProfilesPath;
            entry["createdAtTurn"] = effect["chronology"]!["createdAtTurn"]!.DeepClone();
            var transition = entry["transitions"]![0]!;
            transition["transitionId"] = effect["chronology"]!["lastTransitionId"]!.DeepClone();
            transition["turn"] = effect["chronology"]!["createdAtTurn"]!.DeepClone();
            transition["eventRef"] = effect["chronology"]!["createdEventRef"]!.DeepClone();
        }
        return new(parents, effects, WoundContractTestData.CreateIdentityIndex(identities.ToArray()),
            WoundContractTestData.CreateHistory(history.ToArray()), effectIdentities, receipt);
    }

    /// <summary>Authors one complete ordinary exchange with a literal signed-dice outcome and exact prospective costs.</summary>
    internal static JsonObject Ordinary(string after = "fractured", int playerDie = 4,
        int playerBefore = 3, int playerCost = 3)
    {
        var exchange = SpiritualRetainedPressureGroupTestData.CreateExchange("exchange_pressure_group_46", 46,
            "clear", after, playerDie, 12);
        exchange["diceAudit"]!["outcomeBand"] = playerDie == 6 ? "opposition_success" : "decisive_opposition_success";
        exchange["actionCostAudit"]!["player"]!["before"] = playerBefore;
        exchange["actionCostAudit"]!["player"]!["effectiveCost"] = playerCost;
        exchange["actionCostAudit"]!["player"]!["after"] = playerBefore - playerCost;
        return exchange;
    }

    /// <summary>Computes a DATA fingerprint without authenticating any historical execution.</summary>
    private static string Fingerprint(JsonNode value) => new CanonicalBeforeImage(true,
        Encoding.UTF8.GetBytes(SpiritualWoundStateJson.Canonical(value))).Fingerprint;

    /// <summary>Seals independently authored lifecycle DATA before the real fixture signature is produced.</summary>
    private static void SealLifecycle(JsonObject row, string kind)
    {
        var hash = SpiritualWoundConflictInstanceState.ComputeRowFingerprint(row, kind);
        row[kind + "Id"] = "spiritual_" + kind + "_" + hash[7..];
        row[kind + "Fingerprint"] = hash;
    }
}
