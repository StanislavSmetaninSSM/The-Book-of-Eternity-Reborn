using System.Reflection;
using System.Text;
using System.Text.Json.Nodes;
using BookOfEternityClient.Services;
using Xunit;

namespace BookOfEternityClient.Tests;

/// <summary>
/// Checks closed spiritual receipt history without constructing live publication authority.
/// </summary>
public sealed class SpiritualWoundOpportunityReceiptStateTests
{
    private const string Path = "game_state/wounds/spiritual_wound_opportunity_receipts.json";
    private const string Fingerprint = "sha256:aaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaa";

    /// <summary>
    /// Round-trips empty and decided history with detached values and exact replay.
    /// </summary>
    [Fact]
    public void Parse_RoundTripsDetachedHistoryAndExactReplay()
    {
        var emptyRoot = Root();
        emptyRoot["instances"] = new JsonArray();
        emptyRoot["nextInstanceOrdinal"] = 1;
        var empty = Valid(emptyRoot);
        var root = Root();
        Append(root, 1, 42);
        var state = Valid(root);
        var serialized = (string)Call("SerializeCanonical", state);
        root["decisions"]![0]!["decision"] = "materialize";
        Assert.Equal(serialized, Call("SerializeCanonical", state));
        var replay = Call("PlanAppend", state, Valid(JsonNode.Parse(serialized)!.AsObject()));
        Assert.Equal("exact_replay", Property<string>(replay, "Disposition"));
        Assert.Equal("appended", Property<string>(Call("PlanAppend", empty, state), "Disposition"));
    }

    /// <summary>
    /// Appends a completed decline to signed existing history without rewriting the prior source or decision.
    /// </summary>
    [Fact]
    public void DeclineReducer_PreservesSignedExistingHistoryAndRejectsAcceptedRequestReplay()
    {
        var priorRoot = Root();
        Append(priorRoot, 1, 42);
        var priorJson = priorRoot.ToJsonString();
        var candidate = priorRoot.DeepClone().AsObject();
        Append(candidate, 2, 43);
        var signed = new ValidationService.SpiritualSignedC1Origin(
            new CanonicalBeforeImage(true, Encoding.UTF8.GetBytes(priorJson)),
            new CanonicalBeforeImage(true, Encoding.UTF8.GetBytes(
                "{\"schemaVersion\":1,\"activeConflict\":{\"conflictId\":\"conflict-a\"}}")),
            Fingerprint);
        var packet = DeclinePacket(candidate, 1, 43);

        var appended = SpiritualWoundDeclineReceiptReducer.Reduce(signed, packet);

        Assert.Empty(appended.Issues);
        var after = Assert.IsType<JsonObject>(appended.AfterImage);
        Assert.Equal(2, after["sources"]!.AsArray().Count);
        Assert.Equal(3, after["nextSourceOrdinal"]!.GetValue<int>());
        Assert.True(JsonNode.DeepEquals(priorRoot["instances"], after["instances"]));
        Assert.True(JsonNode.DeepEquals(priorRoot["sources"]![0], after["sources"]![0]));
        Assert.True(JsonNode.DeepEquals(priorRoot["decisions"]![0], after["decisions"]![0]));
        Assert.Equal("none", after["decisions"]![1]!["decision"]!.GetValue<string>());

        var replay = SpiritualWoundDeclineReceiptReducer.Reduce(signed,
            DeclinePacket(priorRoot, 0, 42));
        Assert.Null(replay.AfterImage);
        Assert.Contains(replay.Issues, issue =>
            issue.Code == "spiritual_first_offer_already_accepted");
    }

    /// <summary>
    /// Creates the narrow completed packet surface consumed by the pure reducer from a valid receipt fixture.
    /// </summary>
    /// <param name="receipt">
    /// Valid receipt whose selected source and decision supply the staged decline.
    /// </param>
    /// <param name="sourceIndex">
    /// Zero-based source and decision row to reduce.
    /// </param>
    /// <param name="turn">
    /// Original turn corresponding to <paramref name="sourceIndex"/>.
    /// </param>
    /// <returns>
    /// Detached packet fields ordinarily reconstructed and validated by the C2 owner.
    /// </returns>
    private static JsonObject DeclinePacket(JsonObject receipt, int sourceIndex, int turn)
    {
        var witness = receipt["sources"]![sourceIndex]!["witness"]!.DeepClone();
        var instanceId = receipt["instances"]![0]!["instanceId"]!.GetValue<string>();
        var stage = new JsonObject
        {
            ["opportunityRef"] = "opportunity-" + (sourceIndex + 1),
            ["sourceId"] = witness["sourceId"]!.DeepClone(),
            ["decisionFingerprint"] = "",
            ["sourceOrdinal"] = witness["sourceOrdinal"]!.DeepClone(),
            ["waveOrdinal"] = 0,
            ["decision"] = "none",
            ["selectedSeverityRank"] = null,
            ["woundDraftBase64"] = null,
            ["woundDraftFingerprint"] = null
        };
        stage["decisionFingerprint"] = SpiritualWoundStateJson.Hash(stage,
            "staged_decision", "decisionFingerprint");
        return new JsonObject
        {
            ["sources"] = new JsonArray(witness),
            ["stagedDecisions"] = new JsonArray(stage),
            ["cursor"] = new JsonObject { ["nextSourceOrdinal"] = 1 },
            ["realm"] = "chaos_sea",
            ["sessionId"] = "session-a",
            ["requestId"] = "request-" + turn,
            ["snapshotToken"] = "snapshot-" + turn,
            ["originalSnapshotFingerprint"] = Fingerprint,
            ["turn"] = turn,
            ["conflictInstanceRef"] = instanceId
        };
    }

    /// <summary>
    /// Rejects missing, duplicate and extended root contracts without exposing partial state.
    /// </summary>
    [Fact]
    public void Parse_RejectsMalformedRoots()
    {
        foreach (var json in new string?[] { null, "", "[]", "{\"schemaVersion\":1,\"schemaVersion\":1}" })
            Invalid(json);
        foreach (var field in Root().Select(pair => pair.Key).Append("extra"))
        {
            var root = Root();
            if (field == "extra") root[field] = true;
            else root.Remove(field);
            Invalid(root.ToJsonString());
        }
    }

    /// <summary>
    /// Rejects re-sealed cross-row contradictions instead of relying on stale fingerprints.
    /// </summary>
    /// <param name="change">
    /// Receipt invariant to violate.
    /// </param>
    [Theory]
    [InlineData("undecided")]
    [InlineData("orphan-decision")]
    [InlineData("unknown-instance")]
    [InlineData("instance-ordinal")]
    [InlineData("global-ordinal")]
    [InlineData("counter")]
    [InlineData("decision-instance")]
    [InlineData("decision-source")]
    [InlineData("decision-witness")]
    [InlineData("binding")]
    [InlineData("generation")]
    [InlineData("wave")]
    [InlineData("decline-identities")]
    [InlineData("materialize-identities")]
    [InlineData("materialize-ceiling")]
    [InlineData("unknown-row-field")]
    public void Parse_RejectsCrossRowContradictions(string change)
    {
        var root = Root();
        Append(root, 1, 42);
        var source = root["sources"]![0]!.AsObject();
        var decision = root["decisions"]![0]!.AsObject();
        switch (change)
        {
            case "undecided": root["decisions"] = new JsonArray(); root["nextDecisionOrdinal"] = 1; break;
            case "orphan-decision": root["sources"] = new JsonArray(); root["nextSourceOrdinal"] = 1; break;
            case "unknown-instance": source["instanceId"] = "foreign-instance"; break;
            case "instance-ordinal": source["instanceSourceOrdinal"] = 2; break;
            case "global-ordinal": source["ordinal"] = 2; break;
            case "counter": root["nextSourceOrdinal"] = 3; break;
            case "decision-instance": decision["instanceId"] = "foreign-instance"; break;
            case "decision-source": decision["sourceId"] = "foreign-source"; break;
            case "decision-witness":
                var other = decision["sourceWitness"]!.AsObject();
                other["coordinate"] = "different-coordinate";
                var otherHash = SpiritualWoundStateJson.Hash(other, "source", "sourceId", "sourceFingerprint");
                other["sourceFingerprint"] = otherHash;
                other["sourceId"] = "spiritual_source_" + otherHash[7..];
                break;
            case "binding": decision["binding"]!["requestId"] = "different-request"; break;
            case "generation": decision["binding"]!["continuationGeneration"] = 3; break;
            case "wave": decision["binding"]!["waveOrdinal"] = 2; break;
            case "decline-identities": decision["woundId"] = "wound-a"; break;
            case "materialize-identities": decision["decision"] = "materialize"; decision["selectedSeverityRank"] = 1; break;
            case "materialize-ceiling": Materialize(decision, "wound-a", 2); break;
            case "unknown-row-field": source["extra"] = true; break;
        }
        SealDecision(decision);
        Invalid(root.ToJsonString());
    }

    /// <summary>
    /// Preserves packet-local gaps while keeping accepted lifetime ordinals contiguous across turns.
    /// </summary>
    [Fact]
    public void Parse_AcceptsPacketLocalGapsAndLifetimeProgression()
    {
        var root = Root();
        Append(root, 1, 42, localOrdinal: 1);
        Append(root, 2, 43, localOrdinal: 0);
        Valid(root);
    }

    /// <summary>
    /// Requires positive accepted sources rather than recording a zero-ceiling opportunity.
    /// </summary>
    [Fact]
    public void Parse_RejectsZeroCeilingAcceptedSource()
    {
        var root = Root();
        Append(root, 1, 42);
        var witness = root["sources"]![0]!["witness"]!.AsObject();
        witness["dangerMode"] = "training";
        witness["maximumSeverityRank"] = 0;
        ResealWitness(root, 0);
        Invalid(root.ToJsonString());
    }

    /// <summary>
    /// Rejects repeated decision, opportunity, transition and exchange-side coordinates after re-sealing.
    /// </summary>
    /// <param name="coordinate">
    /// Coordinate copied from the first accepted row.
    /// </param>
    [Theory]
    [InlineData("decisionId")]
    [InlineData("opportunityRef")]
    [InlineData("transitionId")]
    [InlineData("exchangeId")]
    public void Parse_RejectsRepeatedCoordinates(string coordinate)
    {
        var root = Root();
        Append(root, 1, 42);
        Append(root, 2, 43);
        var first = root["decisions"]![0]!.AsObject();
        var second = root["decisions"]![1]!.AsObject();
        if (coordinate == "transitionId")
        {
            Materialize(first, "wound-a", 1);
            Materialize(second, "wound-a", 1);
            second["transitionId"] = first["transitionId"]!.DeepClone();
            SealDecision(first);
        }
        else if (coordinate == "exchangeId")
        {
            var witness = root["sources"]![1]!["witness"]!.AsObject();
            var exchange = Decode(witness["exchangeJsonBase64"]!);
            exchange["exchangeId"] = "exchange-1";
            witness["exchangeId"] = "exchange-1";
            witness["exchangeJsonBase64"] = Encode(exchange);
            ResealWitness(root, 1);
        }
        else second[coordinate] = first[coordinate]!.DeepClone();
        SealDecision(second);
        Invalid(root.ToJsonString());
    }

    /// <summary>
    /// Rejects deleting or rewriting accepted rows and adding historical sources to a closed instance.
    /// </summary>
    [Fact]
    public void PlanAppend_PreservesHistoryAndClosedInstances()
    {
        var root = Root();
        Append(root, 1, 42);
        var original = Valid(root);
        Assert.Equal("conflict", Property<string>(Call("PlanAppend", original, Valid(Root())), "Disposition"));
        var rewritten = root.DeepClone().AsObject();
        rewritten["decisions"]![0]!["opportunityRef"] = "rewritten-opportunity";
        SealDecision(rewritten["decisions"]![0]!.AsObject());
        Assert.Equal("conflict", Property<string>(Call("PlanAppend", original, Valid(rewritten)), "Disposition"));
        var closure = new JsonObject
        {
            ["closureId"] = "", ["ordinal"] = 1, ["instanceId"] = root["instances"]![0]!["instanceId"]!.DeepClone(),
            ["terminalTurn"] = 43, ["terminalEventRef"] = "terminal-a", ["terminalConflictFingerprint"] = Fingerprint,
            ["closureFingerprint"] = ""
        };
        SealInstanceRow(closure, "closure");
        root["closures"]!.AsArray().Add(closure);
        root["nextClosureOrdinal"] = 2;
        var closed = Valid(root);
        Append(root, 2, 43);
        Assert.Equal("conflict", Property<string>(Call("PlanAppend", closed, Valid(root)), "Disposition"));
    }

    /// <summary>
    /// Rejects conflicting original contexts, reused dice and changed causal ordering after re-sealing.
    /// </summary>
    /// <param name="change">
    /// Cross-source invariant to violate.
    /// </param>
    [Theory]
    [InlineData("snapshot")]
    [InlineData("bounds")]
    [InlineData("pool")]
    [InlineData("dice-reuse")]
    [InlineData("source-order")]
    [InlineData("source-duplicate")]
    [InlineData("exchange-order")]
    [InlineData("decision-order")]
    public void Parse_RejectsCrossSourceContradictions(string change)
    {
        var root = SameTurn();
        Valid(root);
        var first = root["sources"]![0]!["witness"]!.AsObject();
        var second = root["sources"]![1]!["witness"]!.AsObject();
        var evidence = second["turnEvidence"]!.AsObject();
        switch (change)
        {
            case "snapshot": evidence["originalSnapshotFingerprint"] = "sha256:" + new string('b', 64); break;
            case "bounds": evidence["bounds"]!["imagePathCount"] = 41; break;
            case "pool": evidence["acceptedD20Values"]![0] = 14; break;
            case "dice-reuse":
                var exchange = Decode(second["exchangeJsonBase64"]!);
                exchange["diceAudit"]!["diceUsed"]![0]!["sourceIndex"] = 0;
                exchange["diceAudit"]!["diceUsed"]![1]!["sourceIndex"] = 1;
                second["exchangeJsonBase64"] = Encode(exchange);
                second["selectedDiceIndices"] = new JsonArray(0, 1);
                foreach (var claim in evidence["diceClaims"]!.AsArray())
                {
                    claim!["sourceIndex"] = claim["sourceIndex"]!.GetValue<int>() - 2;
                    claim["claimFingerprint"] = SpiritualWoundTurnEvidence.ComputeClaimFingerprint(claim.AsObject());
                }
                break;
            case "source-order": first["sourceOrdinal"] = 3; ResealWitness(root, 0); break;
            case "source-duplicate": second["sourceOrdinal"] = first["sourceOrdinal"]!.DeepClone(); break;
            case "exchange-order":
                first["exchangeOrdinal"] = 1;
                foreach (var claim in first["turnEvidence"]!["diceClaims"]!.AsArray())
                {
                    claim!["exchangeOrdinal"] = 1;
                    claim["claimFingerprint"] = SpiritualWoundTurnEvidence.ComputeClaimFingerprint(claim.AsObject());
                }
                ResealWitness(root, 0);
                break;
            case "decision-order":
                var decisions = root["decisions"]!.AsArray();
                var a = decisions[0]!.DeepClone();
                var b = decisions[1]!.DeepClone();
                decisions[0] = b; decisions[1] = a;
                decisions[0]!["ordinal"] = 1; decisions[1]!["ordinal"] = 2;
                SealDecision(decisions[0]!.AsObject()); SealDecision(decisions[1]!.AsObject());
                Invalid(root.ToJsonString());
                return;
        }
        ResealWitness(root, 1);
        Invalid(root.ToJsonString());
    }

    /// <summary>
    /// Accepts repeated wound references with distinct transitions, as required for later worsening.
    /// </summary>
    [Fact]
    public void Parse_AcceptsMaterializationsOfTheSameWoundAcrossTurns()
    {
        var root = Root();
        Append(root, 1, 42);
        Append(root, 2, 43);
        foreach (var node in root["decisions"]!.AsArray())
        {
            Materialize(node!.AsObject(), "wound-a", 1);
            SealDecision(node.AsObject());
        }
        Valid(root);
    }

    /// <summary>
    /// Accepts an already met source guarantee without inventing a second wound transition.
    /// </summary>
    [Fact]
    public void Parse_AcceptsGuaranteeSatisfiedWithoutTransition()
    {
        var root = SameTurn();
        var first = root["decisions"]![0]!.AsObject();
        Materialize(first, "wound-a", 1);
        SealDecision(first);
        SetGuaranteedSource(root, 1);
        var satisfied = root["decisions"]![1]!.AsObject();
        satisfied["decision"] = "guarantee_satisfied";
        satisfied["woundId"] = "wound-a";
        satisfied["satisfiedSeverityRank"] = 1;
        SealDecision(satisfied);

        var parsed = SpiritualWoundOpportunityReceiptState.Parse(root.ToJsonString(), Path);

        Assert.True(parsed.IsValid, string.Join(Environment.NewLine, parsed.Issues));
        Assert.Equal("wound-a", parsed.State!.FindRecordedConflictSideWound(
            root["instances"]![0]!["instanceId"]!.GetValue<string>(), "opposition")
            .Wound?.WoundId);
    }

    /// <summary>
    /// Rejects malformed satisfaction rows while retaining the existing closed receipt format.
    /// </summary>
    /// <param name="change">
    /// One required no-transition satisfaction invariant to violate.
    /// </param>
    [Theory]
    [InlineData("missing-rank")]
    [InlineData("selected-rank")]
    [InlineData("transition")]
    [InlineData("missing-wound")]
    [InlineData("missing-guarantee")]
    [InlineData("rank-below-guarantee")]
    public void Parse_RejectsMalformedGuaranteeSatisfied(string change)
    {
        var root = SameTurn();
        if (change != "missing-guarantee")
            SetGuaranteedSource(root, 1, change == "rank-below-guarantee" ? 2 : 1);
        var decision = root["decisions"]![1]!.AsObject();
        decision["decision"] = "guarantee_satisfied";
        decision["woundId"] = "wound-a";
        decision["satisfiedSeverityRank"] = 1;
        switch (change)
        {
            case "missing-rank": decision.Remove("satisfiedSeverityRank"); break;
            case "selected-rank": decision["selectedSeverityRank"] = 1; break;
            case "transition": decision["transitionId"] = "transition-extra"; break;
            case "missing-wound": decision["woundId"] = null; break;
        }
        SealDecision(decision);

        Invalid(root.ToJsonString());
    }

    /// <summary>
    /// Rejects duplicate properties even in otherwise valid complete history.
    /// </summary>
    [Fact]
    public void Parse_RejectsDuplicatePropertiesInCompleteHistory()
    {
        var root = Root();
        Append(root, 1, 42);
        var json = root.ToJsonString();
        Invalid(json.Replace("\"schemaVersion\":1", "\"schemaVersion\":1,\"schemaVersion\":1", StringComparison.Ordinal));
        Invalid(json.Replace("\"decision\":\"none\"", "\"decision\":\"none\",\"decision\":\"none\"", StringComparison.Ordinal));
    }

    /// <summary>
    /// Rejects oversized collections at the version-one row bound.
    /// </summary>
    /// <param name="field">
    /// Root collection to exceed.
    /// </param>
    [Theory]
    [InlineData("instances")]
    [InlineData("closures")]
    [InlineData("sources")]
    [InlineData("decisions")]
    public void Parse_RejectsOversizedCollections(string field)
    {
        var root = Root();
        root[field] = new JsonArray(Enumerable.Repeat<JsonNode?>(null, 20_001).ToArray());
        Invalid(root.ToJsonString());
    }

    /// <summary>
    /// Allows both affected sides to retain the same exchange claims without spending dice twice.
    /// </summary>
    [Fact]
    public void Parse_AcceptsTwoSidesSharingOneExchange()
    {
        Valid(TwoSides());
    }

    /// <summary>
    /// Rejects using the opposite side to replay an old exchange from a new original request.
    /// </summary>
    [Fact]
    public void Parse_RejectsExchangeReusedByOppositeSideInAnotherTurn()
    {
        var root = TwoSides();
        Valid(root);
        var witness = root["sources"]![1]!["witness"]!.AsObject();
        var evidence = witness["turnEvidence"]!;
        evidence["turn"] = 43;
        evidence["requestId"] = "request-43";
        evidence["snapshotToken"] = "snapshot-43";
        var binding = root["decisions"]![1]!["binding"]!;
        binding["turn"] = 43;
        binding["requestId"] = "request-43";
        binding["snapshotToken"] = "snapshot-43";
        var exchange = Decode(witness["exchangeJsonBase64"]!);
        exchange["turnNumber"] = 43;
        witness["exchangeJsonBase64"] = Encode(exchange);
        ResealWitness(root, 1);
        Invalid(root.ToJsonString());
    }

    /// <summary>
    /// Preserves consecutive player-then-opposition source order within a shared exchange.
    /// </summary>
    /// <param name="change">
    /// Impossible shared-exchange ordering.
    /// </param>
    [Theory]
    [InlineData("gap")]
    [InlineData("reversed")]
    public void Parse_RejectsSharedExchangeOrderChanges(string change)
    {
        var root = TwoSides();
        Valid(root);
        if (change == "gap")
        {
            for (var index = 0; index < 2; index++)
            {
                var witness = root["sources"]![index]!["witness"]!.AsObject();
                witness["exchangeOrdinal"] = 1;
                witness["sourceOrdinal"] = index == 0 ? 1 : 3;
                foreach (var claim in witness["turnEvidence"]!["diceClaims"]!.AsArray())
                {
                    claim!["exchangeOrdinal"] = 1;
                    claim["claimFingerprint"] = SpiritualWoundTurnEvidence.ComputeClaimFingerprint(claim.AsObject());
                }
                ResealWitness(root, index);
            }
        }
        else
        {
            var a = root["sources"]![0]!["witness"]!.DeepClone();
            var b = root["sources"]![1]!["witness"]!.DeepClone();
            root["sources"]![0]!["witness"] = b;
            root["sources"]![1]!["witness"] = a;
            b["sourceOrdinal"] = 0;
            a["sourceOrdinal"] = 1;
            ResealWitness(root, 0);
            ResealWitness(root, 1);
        }
        Invalid(root.ToJsonString());
    }

    /// <summary>
    /// Rejects source indices that require more omitted harmful sources than the exchange order permits.
    /// </summary>
    /// <param name="change">
    /// Impossible singleton or interval capacity.
    /// </param>
    [Theory]
    [InlineData("first-player")]
    [InlineData("between-exchanges")]
    public void Parse_RejectsUnavailableSourcePositions(string change)
    {
        JsonObject root;
        if (change == "first-player")
        {
            root = TwoSides();
            root["sources"]!.AsArray().RemoveAt(1);
            root["decisions"]!.AsArray().RemoveAt(1);
            root["nextSourceOrdinal"] = 2;
            root["nextDecisionOrdinal"] = 2;
            root["sources"]![0]!["witness"]!["sourceOrdinal"] = 1;
            ResealWitness(root, 0);
        }
        else
        {
            root = SameTurn();
            root["sources"]![1]!["witness"]!["sourceOrdinal"] = 3;
            ResealWitness(root, 1);
        }
        Invalid(root.ToJsonString());
    }

    /// <summary>
    /// Preserves one shared decision wave per exchange and advances later eligible exchanges.
    /// </summary>
    /// <param name="change">
    /// In-range wave-binding contradiction.
    /// </param>
    [Theory]
    [InlineData("split-generation")]
    [InlineData("split-wave")]
    [InlineData("reused-generation")]
    [InlineData("reused-wave")]
    public void Parse_RejectsInconsistentDecisionWaves(string change)
    {
        var root = change.StartsWith("split", StringComparison.Ordinal) ? TwoSides() : SameTurn();
        Valid(root);
        var decision = root["decisions"]![1]!.AsObject();
        var binding = decision["binding"]!;
        switch (change)
        {
            case "split-generation": binding["continuationGeneration"] = 2; break;
            case "split-wave": binding["waveOrdinal"] = 1; break;
            case "reused-generation": binding["continuationGeneration"] = 1; break;
            case "reused-wave": binding["waveOrdinal"] = 0; break;
        }
        SealDecision(decision);
        Invalid(root.ToJsonString());
    }

    /// <summary>
    /// Builds a valid shared exchange with a positive source on each affected side.
    /// </summary>
    /// <returns>
    /// Detached receipt fixture without original profile or publication authority.
    /// </returns>
    internal static JsonObject TwoSides()
    {
        var root = Root();
        Append(root, 1, 42, 0);
        Append(root, 2, 42, 1);
        var player = root["sources"]![0]!["witness"]!.AsObject();
        var opposition = root["sources"]![1]!["witness"]!.AsObject();
        var exchange = Decode(player["exchangeJsonBase64"]!);
        exchange["after"]!["playerSideStrain"] = "overwhelmed";
        exchange["actionCostAudit"]!["opposition"]!["artTier"] = 4;
        exchange["actionCostAudit"]!["opposition"]!["effectiveCost"] = 1;
        exchange["actionCostAudit"]!["opposition"]!["after"] = 5;
        exchange["incomingAction"] = new JsonObject
        {
            ["actorType"] = "guardian", ["actorId"] = "guardian-a", ["operationType"] = "pressure"
        };
        player["affectedSide"] = "player";
        player["actingActor"] = new JsonObject { ["actorKind"] = "guardian", ["actorId"] = "guardian-a" };
        player["affectedActor"] = new JsonObject { ["actorKind"] = "player_soul", ["actorId"] = "player_soul" };
        player["appliedArtId"] = "guardian:guardian-a:pressure";
        player["appliedArtTier"] = 4;
        player["destinationStrainRank"] = 3;
        player["extraStrainJumps"] = 2;
        player["harmfulMargin"] = -10;
        opposition["exchangeId"] = player["exchangeId"]!.DeepClone();
        player["exchangeJsonBase64"] = Encode(exchange);
        opposition["exchangeJsonBase64"] = Encode(exchange);
        ResealWitness(root, 0);
        ResealWitness(root, 1);
        return root;
    }

    /// <summary>
    /// Enforces the owning opportunity's exact guaranteed result, including its upper boundary.
    /// </summary>
    /// <param name="rank">
    /// Selected severity, or zero for an explicit decline.
    /// </param>
    [Theory]
    [InlineData(0)]
    [InlineData(2)]
    public void Parse_RequiresExactGuaranteedSeverity(int rank)
    {
        var root = Root();
        Append(root, 1, 42);
        var witness = root["sources"]![0]!["witness"]!.AsObject();
        var art = JsonNode.Parse("""
            {"artId":"art-source","displayName":"Нить надлома","effectSummary":"Духовное давление.",
             "ownerActorType":"player_soul","ownerActorId":"player_soul","baseOperation":"pressure","tier":0,
             "costMultiplierPercent":200,"upgradeCost":{"inkFeathers":1},"canTeachPlayer":false,"trainingConditions":[],
             "spiritualWoundEnvelope":{"schemaVersion":1,"maximumSeverityRank":2,"guaranteedSeverityRank":1}}
            """)!.AsObject();
        var exchange = Decode(witness["exchangeJsonBase64"]!);
        exchange["specialArtAudit"] = JsonNode.Parse("""
            {"artId":"art-source","ownerActorType":"player_soul","ownerActorId":"player_soul",
             "baseOperation":"pressure","costMultiplierPercent":200,"effectNote":"Надлом духовного узора."}
            """);
        var cost = exchange["actionCostAudit"]!["player"]!;
        cost["specialArtId"] = "art-source";
        cost["specialCostMultiplierPercent"] = 200;
        cost["standardEffectiveCost"] = 3;
        cost["effectiveCost"] = 6;
        cost["after"] = 0;
        exchange["after"]!["oppositionSideStrain"] = "fractured";
        witness["destinationStrainRank"] = 2;
        witness["extraStrainJumps"] = 1;
        witness["maximumSeverityRank"] = 2;
        witness["guaranteedSeverityRank"] = 1;
        witness["appliedArtId"] = "art-source";
        witness["appliedArtKind"] = "special";
        witness["specialArtJsonBase64"] = Encode(art);
        witness["exchangeJsonBase64"] = Encode(exchange);
        ResealWitness(root, 0);
        var decision = root["decisions"]![0]!.AsObject();
        Materialize(decision, "wound-a", 1);
        SealDecision(decision);
        Valid(root);
        if (rank == 0)
        {
            decision["decision"] = "none";
            decision["selectedSeverityRank"] = null;
            decision["woundId"] = null;
            decision["transitionId"] = null;
        }
        else decision["selectedSeverityRank"] = rank;
        SealDecision(decision);
        Invalid(root.ToJsonString());
    }

    /// <summary>
    /// Requires an explicit older-wound decision to retain its target and increase severity.
    /// </summary>
    /// <param name="change">
    /// The decision contradiction introduced after the positive control.
    /// </param>
    [Theory]
    [InlineData("target")]
    [InlineData("severity")]
    public void Parse_JoinsRetainedRetraumaDecision(string change)
    {
        var root = Root();
        Append(root, 1, 42);
        var witness = root["sources"]![0]!["witness"]!.AsObject();
        var wound = WoundContractTestData.CreateSpiritualActiveWound("wound-older", "chaos_sea",
            "guardian", "guardian-a", WoundCarrierCatalog.AfterlifeProfilesPath);
        var originalRank = WoundMaterializationContract.Parse(wound.ToJsonString(), "test.wound").Wound!.Severity.Rank;
        var exchange = Decode(witness["exchangeJsonBase64"]!);
        exchange["after"]!["oppositionSideStrain"] = "broken";
        exchange["spiritualWoundTarget"] = new JsonObject
        {
            ["actorType"] = "guardian", ["actorId"] = "guardian-a", ["retraumaWoundRef"] = "wound-older"
        };
        witness["destinationStrainRank"] = 4;
        witness["extraStrainJumps"] = 3;
        witness["maximumSeverityRank"] = 4;
        witness["retraumaWoundId"] = "wound-older";
        witness["retraumaWoundJsonBase64"] = Encode(wound);
        witness["exchangeJsonBase64"] = Encode(exchange);
        ResealWitness(root, 0);
        var decision = root["decisions"]![0]!.AsObject();
        Materialize(decision, "wound-older", originalRank + 1);
        SealDecision(decision);
        Valid(root);
        if (change == "target") decision["woundId"] = "other-wound";
        else decision["selectedSeverityRank"] = originalRank;
        SealDecision(decision);
        Invalid(root.ToJsonString());
    }

    /// <summary>
    /// Finds the first new same-side wound only within the requested durable conflict instance.
    /// </summary>
    [Fact]
    public void FindRecordedConflictSideWound_UsesInstanceAndRejectsAnotherNewIdentity()
    {
        var root = Root();
        Append(root, 1, 42);
        var first = root["decisions"]![0]!.AsObject();
        Materialize(first, "wound-conflict-a", 1);
        SealDecision(first);
        Append(root, 2, 43);
        var instanceId = root["instances"]![0]!["instanceId"]!.GetValue<string>();
        var parsed = SpiritualWoundOpportunityReceiptState.Parse(root.ToJsonString(), Path);
        Assert.True(parsed.IsValid);
        var recorded = parsed.State!.FindRecordedConflictSideWound(instanceId, "opposition");
        Assert.Empty(recorded.Issues);
        Assert.Equal("wound-conflict-a", recorded.Wound?.WoundId);
        Assert.Equal("transition-1", recorded.Wound?.CreateTransitionId);
        Assert.Null(parsed.State.FindRecordedConflictSideWound(instanceId, "player").Wound);
        Assert.Null(parsed.State.FindRecordedConflictSideWound("another-instance", "opposition").Wound);

        var second = root["decisions"]![1]!.AsObject();
        Materialize(second, "wound-conflict-b", 1);
        SealDecision(second);
        parsed = SpiritualWoundOpportunityReceiptState.Parse(root.ToJsonString(), Path);
        Assert.True(parsed.IsValid);
        var conflict = parsed.State!.FindRecordedConflictSideWound(instanceId, "opposition");
        Assert.Null(conflict.Wound);
        Assert.Contains(conflict.Issues, issue =>
            issue.Code == "spiritual_wound_conflict_side_history_invalid");
    }

    /// <summary>
    /// Rejects original turns that move backward or reopen a completed request group.
    /// </summary>
    /// <param name="change">
    /// Lifetime chronology violation.
    /// </param>
    [Theory]
    [InlineData("backward")]
    [InlineData("reopened")]
    public void Parse_RejectsLifetimeChronologyChanges(string change)
    {
        var root = Root();
        Append(root, 1, 43);
        Append(root, 2, change == "backward" ? 42 : 44);
        if (change == "reopened")
        {
            root = SameTurn();
            Append(root, 3, 42);
            root["sources"]![2]!["witness"]!["turnEvidence"]!["requestId"] = "other-request";
            root["decisions"]![2]!["binding"]!["requestId"] = "other-request";
            ResealWitness(root, 2);
            foreach (var field in new[] { "sources", "decisions" })
            {
                var rows = root[field]!.AsArray();
                var a = rows[1]!.DeepClone();
                var b = rows[2]!.DeepClone();
                rows[1] = b; rows[2] = a;
                rows[1]!["ordinal"] = 2; rows[2]!["ordinal"] = 3;
                if (field == "sources")
                {
                    rows[1]!["instanceSourceOrdinal"] = 2;
                    rows[2]!["instanceSourceOrdinal"] = 3;
                }
                else
                {
                    SealDecision(rows[1]!.AsObject());
                    SealDecision(rows[2]!.AsObject());
                }
            }
        }
        Invalid(root.ToJsonString());
    }

    /// <summary>
    /// Prevents an original packet's sources from being reassigned to another valid instance.
    /// </summary>
    [Fact]
    public void Parse_RejectsOriginalTurnSplitAcrossInstances()
    {
        var root = SameTurn();
        var instance = root["instances"]![0]!.DeepClone().AsObject();
        instance["displayConflictId"] = "conflict-b";
        instance["ordinal"] = 2;
        SealInstanceRow(instance, "instance");
        root["instances"]!.AsArray().Add(instance);
        root["nextInstanceOrdinal"] = 3;
        var source = root["sources"]![1]!.AsObject();
        source["instanceId"] = instance["instanceId"]!.DeepClone();
        source["instanceSourceOrdinal"] = 1;
        source["witness"]!["conflictId"] = "conflict-b";
        root["decisions"]![1]!["instanceId"] = instance["instanceId"]!.DeepClone();
        ResealWitness(root, 1);
        Invalid(root.ToJsonString());
    }

    /// <summary>
    /// Rejects filling more source positions than preceding and current exchanges can supply.
    /// </summary>
    [Fact]
    public void Parse_RejectsSourcePositionAheadOfExchangeCapacity()
    {
        var root = Root();
        Append(root, 1, 42, 3);
        Invalid(root.ToJsonString());
    }

    /// <summary>
    /// Prevents accepted original turns from being extended by a second partial publication.
    /// </summary>
    [Fact]
    public void PlanAppend_RejectsExtensionOfAnAlreadyAcceptedOriginalTurn()
    {
        var complete = SameTurn();
        var prefix = complete.DeepClone().AsObject();
        prefix["sources"]!.AsArray().RemoveAt(1);
        prefix["decisions"]!.AsArray().RemoveAt(1);
        prefix["nextSourceOrdinal"] = 2;
        prefix["nextDecisionOrdinal"] = 2;
        var result = Call("PlanAppend", Valid(prefix), Valid(complete));
        Assert.Equal("conflict", Property<string>(result, "Disposition"));
        Assert.Null(Property<object?>(result, "After"));
    }

    /// <summary>
    /// Joins each source to the exact instance realm, display ID and inclusive lifetime.
    /// </summary>
    /// <param name="change">
    /// Instance binding to contradict.
    /// </param>
    [Theory]
    [InlineData("realm")]
    [InlineData("display")]
    [InlineData("before-start")]
    [InlineData("after-close")]
    public void Parse_RejectsSourceOutsideItsInstance(string change)
    {
        var root = Root();
        Append(root, 1, 43);
        var witness = root["sources"]![0]!["witness"]!.AsObject();
        if (change == "realm") witness["turnEvidence"]!["realm"] = "shining_abode";
        if (change == "display") witness["conflictId"] = "other-conflict";
        if (change == "before-start")
        {
            witness["turnEvidence"]!["turn"] = 41;
            root["decisions"]![0]!["binding"]!["turn"] = 41;
            var exchange = Decode(witness["exchangeJsonBase64"]!);
            exchange["turnNumber"] = 41;
            witness["exchangeJsonBase64"] = Encode(exchange);
        }
        if (change == "after-close")
        {
            var closure = new JsonObject
            {
                ["closureId"] = "", ["ordinal"] = 1, ["instanceId"] = root["instances"]![0]!["instanceId"]!.DeepClone(),
                ["terminalTurn"] = 42, ["terminalEventRef"] = "terminal-a", ["terminalConflictFingerprint"] = Fingerprint,
                ["closureFingerprint"] = ""
            };
            SealInstanceRow(closure, "closure");
            root["closures"]!.AsArray().Add(closure);
            root["nextClosureOrdinal"] = 2;
        }
        ResealWitness(root, 0);
        Invalid(root.ToJsonString());
    }

    /// <summary>
    /// Builds two sequential exchanges from one retained original turn with disjoint dice.
    /// </summary>
    /// <returns>
    /// Complete mutable ledger fixture.
    /// </returns>
    private static JsonObject SameTurn()
    {
        var root = Root();
        Append(root, 1, 42);
        Append(root, 2, 42, 2);
        root["decisions"]![1]!["binding"]!["continuationGeneration"] = 2;
        root["decisions"]![1]!["binding"]!["waveOrdinal"] = 1;
        for (var index = 0; index < 2; index++)
        {
            var witness = root["sources"]![index]!["witness"]!.AsObject();
            witness["turnEvidence"]!["acceptedD20Values"] = new JsonArray(15, 5, 15, 5);
            if (index == 1)
            {
                witness["exchangeOrdinal"] = 1;
                witness["selectedDiceIndices"] = new JsonArray(2, 3);
                var exchange = Decode(witness["exchangeJsonBase64"]!);
                exchange["diceAudit"]!["diceUsed"]![0]!["sourceIndex"] = 2;
                exchange["diceAudit"]!["diceUsed"]![1]!["sourceIndex"] = 3;
                witness["exchangeJsonBase64"] = Encode(exchange);
                foreach (var claim in witness["turnEvidence"]!["diceClaims"]!.AsArray())
                {
                    claim!["exchangeOrdinal"] = 1;
                    claim["sourceIndex"] = claim["sourceIndex"]!.GetValue<int>() + 2;
                    claim["claimFingerprint"] = SpiritualWoundTurnEvidence.ComputeClaimFingerprint(claim.AsObject());
                }
            }
            ResealWitness(root, index);
        }
        return root;
    }

    /// <summary>
    /// Creates an empty complete ledger with one valid instance start.
    /// </summary>
    /// <returns>
    /// Mutable parser fixture, never original or publication authority.
    /// </returns>
    private static JsonObject Root()
    {
        var instance = new JsonObject
        {
            ["instanceId"] = "", ["ordinal"] = 1, ["displayConflictId"] = "conflict-a", ["realm"] = "chaos_sea",
            ["startSessionId"] = "session-a", ["startRequestId"] = "request-42", ["startSnapshotToken"] = "snapshot-42",
            ["startTurn"] = 42, ["baselineConflictFingerprint"] = Fingerprint, ["instanceFingerprint"] = ""
        };
        SealInstanceRow(instance, "instance");
        return new JsonObject
        {
            ["schemaVersion"] = 1, ["nextInstanceOrdinal"] = 2, ["nextClosureOrdinal"] = 1,
            ["nextSourceOrdinal"] = 1, ["nextDecisionOrdinal"] = 1,
            ["instances"] = new JsonArray(instance), ["closures"] = new JsonArray(),
            ["sources"] = new JsonArray(), ["decisions"] = new JsonArray()
        };
    }

    /// <summary>
    /// Appends a valid positive source and its explicit decline for a distinct original turn.
    /// </summary>
    /// <param name="root">
    /// Mutable ledger fixture.
    /// </param>
    /// <param name="ordinal">
    /// Next global and instance source ordinal.
    /// </param>
    /// <param name="turn">
    /// Original turn, at or after instance start.
    /// </param>
    /// <param name="localOrdinal">
    /// Packet-local source position, zero by default.
    /// </param>
    private static void Append(JsonObject root, int ordinal, int turn, int localOrdinal = 0)
    {
        var witness = SpiritualWoundSourceWitnessTests.Source();
        var exchange = Decode(witness["exchangeJsonBase64"]!);
        exchange["exchangeId"] = "exchange-" + ordinal;
        exchange["turnNumber"] = turn;
        witness["exchangeId"] = "exchange-" + ordinal;
        witness["exchangeJsonBase64"] = Encode(exchange);
        witness["sourceOrdinal"] = localOrdinal;
        witness["turnEvidence"]!["requestId"] = "request-" + turn;
        witness["turnEvidence"]!["snapshotToken"] = "snapshot-" + turn;
        witness["turnEvidence"]!["turn"] = turn;
        var instanceId = root["instances"]![0]!["instanceId"]!.GetValue<string>();
        root["sources"]!.AsArray().Add(new JsonObject
        {
            ["ordinal"] = ordinal, ["instanceId"] = instanceId, ["instanceSourceOrdinal"] = ordinal, ["witness"] = witness
        });
        root["decisions"]!.AsArray().Add(new JsonObject
        {
            ["ordinal"] = ordinal, ["decisionId"] = "decision-" + ordinal, ["instanceId"] = instanceId,
            ["sourceId"] = "", ["opportunityRef"] = "opportunity-" + ordinal, ["sourceWitness"] = null,
            ["binding"] = new JsonObject
            {
                ["sessionId"] = "session-a", ["requestId"] = "request-" + turn, ["snapshotToken"] = "snapshot-" + turn,
                ["turn"] = turn, ["continuationGeneration"] = 1, ["waveOrdinal"] = 0, ["sourceOrdinal"] = localOrdinal
            },
            ["decision"] = "none", ["selectedSeverityRank"] = null, ["woundId"] = null, ["transitionId"] = null,
            ["sourceFingerprint"] = "", ["decisionFingerprint"] = ""
        });
        root["nextSourceOrdinal"] = ordinal + 1;
        root["nextDecisionOrdinal"] = ordinal + 1;
        ResealWitness(root, ordinal - 1);
    }

    /// <summary>
    /// Recomputes fixture comparison digests and duplicates the exact source into its decision.
    /// </summary>
    /// <param name="root">
    /// Mutable ledger fixture.
    /// </param>
    /// <param name="index">
    /// Zero-based matching source and decision index.
    /// </param>
    private static void ResealWitness(JsonObject root, int index)
    {
        var witness = root["sources"]![index]!["witness"]!.AsObject();
        var hash = SpiritualWoundStateJson.Hash(witness, "source", "sourceId", "sourceFingerprint");
        witness["sourceId"] = "spiritual_source_" + hash[7..];
        witness["sourceFingerprint"] = hash;
        var decision = root["decisions"]![index]!.AsObject();
        decision["sourceId"] = witness["sourceId"]!.DeepClone();
        decision["sourceFingerprint"] = hash;
        decision["sourceWitness"] = witness.DeepClone();
        decision["binding"]!["sourceOrdinal"] = witness["sourceOrdinal"]!.DeepClone();
        SealDecision(decision);
    }

    /// <summary>
    /// Changes a fixture decision to materialization without authorizing a wound command.
    /// </summary>
    /// <param name="decision">
    /// Decision fixture to modify.
    /// </param>
    /// <param name="woundId">
    /// Referenced wound identity.
    /// </param>
    /// <param name="rank">
    /// Selected severity.
    /// </param>
    private static void Materialize(JsonObject decision, string woundId, int rank)
    {
        decision["decision"] = "materialize";
        decision["selectedSeverityRank"] = rank;
        decision["woundId"] = woundId;
        decision["transitionId"] = "transition-" + decision["ordinal"]!.GetValue<int>();
    }

    /// <summary>
    /// Supplies a valid prior special-art guarantee for one fixture source.
    /// </summary>
    /// <param name="root">
    /// Complete mutable receipt fixture.
    /// </param>
    /// <param name="index">
    /// Zero-based source and decision position to update.
    /// </param>
    /// <param name="guaranteeRank">
    /// Positive guarantee rank, bounded by the fixture's hard maximum.
    /// </param>
    private static void SetGuaranteedSource(JsonObject root, int index, int guaranteeRank = 1)
    {
        var witness = root["sources"]![index]!["witness"]!.AsObject();
        var art = JsonNode.Parse("""
            {"artId":"art-source","displayName":"Нить надлома","effectSummary":"Духовное давление.",
             "ownerActorType":"player_soul","ownerActorId":"player_soul","baseOperation":"pressure","tier":0,
             "costMultiplierPercent":200,"upgradeCost":{"inkFeathers":1},"canTeachPlayer":false,"trainingConditions":[],
             "spiritualWoundEnvelope":{"schemaVersion":1,"maximumSeverityRank":2,"guaranteedSeverityRank":1}}
            """)!.AsObject();
        art["spiritualWoundEnvelope"]!["guaranteedSeverityRank"] = guaranteeRank;
        var exchange = Decode(witness["exchangeJsonBase64"]!);
        exchange["specialArtAudit"] = JsonNode.Parse("""
            {"artId":"art-source","ownerActorType":"player_soul","ownerActorId":"player_soul",
             "baseOperation":"pressure","costMultiplierPercent":200,"effectNote":"Надлом духовного узора."}
            """);
        var cost = exchange["actionCostAudit"]!["player"]!;
        cost["specialArtId"] = "art-source";
        cost["specialCostMultiplierPercent"] = 200;
        cost["standardEffectiveCost"] = 3;
        cost["effectiveCost"] = 6;
        cost["after"] = 0;
        exchange["after"]!["oppositionSideStrain"] = "fractured";
        witness["destinationStrainRank"] = 2;
        witness["extraStrainJumps"] = 1;
        witness["maximumSeverityRank"] = 2;
        witness["guaranteedSeverityRank"] = guaranteeRank;
        witness["appliedArtId"] = "art-source";
        witness["appliedArtKind"] = "special";
        witness["specialArtJsonBase64"] = Encode(art);
        witness["exchangeJsonBase64"] = Encode(exchange);
        ResealWitness(root, index);
    }

    /// <summary>
    /// Recomputes the complete decision comparison digest.
    /// </summary>
    /// <param name="decision">
    /// Mutable decision fixture.
    /// </param>
    private static void SealDecision(JsonObject decision) =>
        decision["decisionFingerprint"] = SpiritualWoundStateJson.Hash(decision, "decision", "decisionFingerprint");

    /// <summary>
    /// Assigns derived identity and comparison digest to an instance or closure fixture.
    /// </summary>
    /// <param name="row">
    /// Mutable lifecycle row.
    /// </param>
    /// <param name="kind">
    /// Instance or closure domain.
    /// </param>
    private static void SealInstanceRow(JsonObject row, string kind)
    {
        var hash = SpiritualWoundConflictInstanceState.ComputeRowFingerprint(row, kind);
        row[kind + "Id"] = "spiritual_" + kind + "_" + hash[7..];
        row[kind + "Fingerprint"] = hash;
    }

    /// <summary>
    /// Encodes canonical JSON comparison evidence.
    /// </summary>
    /// <param name="node">
    /// Detached payload.
    /// </param>
    /// <returns>
    /// Canonical UTF-8 bytes as base64.
    /// </returns>
    private static string Encode(JsonObject node) => Convert.ToBase64String(Encoding.UTF8.GetBytes(SpiritualWoundStateJson.Canonical(node)));

    /// <summary>
    /// Decodes the test fixture's retained object.
    /// </summary>
    /// <param name="node">
    /// Base64 payload from a valid fixture.
    /// </param>
    /// <returns>
    /// Detached mutable object.
    /// </returns>
    private static JsonObject Decode(JsonNode node) => JsonNode.Parse(Encoding.UTF8.GetString(Convert.FromBase64String(node.GetValue<string>())))!.AsObject();

    /// <summary>
    /// Requires successful strict parsing and returns the opaque detached state.
    /// </summary>
    /// <param name="root">
    /// Complete ledger fixture.
    /// </param>
    /// <returns>
    /// Parsed pure state.
    /// </returns>
    private static object Valid(JsonObject root)
    {
        var result = Call("Parse", root.ToJsonString(), Path);
        Assert.True(Property<bool>(result, "IsValid"));
        return Property<object>(result, "State");
    }

    /// <summary>
    /// Requires malformed input to return diagnostics without partial state.
    /// </summary>
    /// <param name="json">
    /// Serialized input, including missing content.
    /// </param>
    private static void Invalid(string? json)
    {
        var result = Call("Parse", json, Path);
        Assert.False(Property<bool>(result, "IsValid"));
        Assert.Null(Property<object?>(result, "State"));
        Assert.NotEmpty(Property<IReadOnlyList<ValidationIssue>>(result, "Issues"));
    }

    /// <summary>
    /// Calls the internal pure state API while allowing a missing-type RED test before implementation.
    /// </summary>
    /// <param name="name">
    /// Static member name.
    /// </param>
    /// <param name="arguments">
    /// Exact method arguments.
    /// </param>
    /// <returns>
    /// The invoked result.
    /// </returns>
    private static object Call(string name, params object?[] arguments)
    {
        var type = typeof(ValidationService).Assembly.GetType("BookOfEternityClient.Services.SpiritualWoundOpportunityReceiptState");
        Assert.NotNull(type);
        var method = type.GetMethod(name, BindingFlags.Static | BindingFlags.Public | BindingFlags.NonPublic);
        Assert.NotNull(method);
        return method.Invoke(null, arguments)!;
    }

    /// <summary>
    /// Reads a result property without making the production state public.
    /// </summary>
    /// <typeparam name="T">
    /// Expected property type.
    /// </typeparam>
    /// <param name="result">
    /// Parser or reducer result.
    /// </param>
    /// <param name="name">
    /// Exact property name.
    /// </param>
    /// <returns>
    /// Property value.
    /// </returns>
    private static T Property<T>(object result, string name) => (T)result.GetType().GetProperty(name)!.GetValue(result)!;
}
