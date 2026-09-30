using System.Reflection;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json.Nodes;
using BookOfEternityClient.Services;
using Xunit;

namespace BookOfEternityClient.Tests;

/// <summary>
/// Checks detached unfinished spiritual decisions without granting live reconstruction authority.
/// </summary>
public sealed class SpiritualWoundDecisionPendingStateTests
{
    private const string Path = "game_state/control/pending_spiritual_wound_decisions.json";
    private const string Fingerprint = "sha256:aaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaa";
    private static readonly string[] Paths = [AfterlifeSpiritualConflictState.StatePath, "output/narrative_response.json"];

    /// <summary>
    /// Preserves exact draft and rollback bytes while serializing detached wrapper values.
    /// </summary>
    [Fact]
    public void Parse_RoundTripsEmptyAndActivePackets()
    {
        var empty = Valid(new JsonObject { ["schemaVersion"] = 1, ["pending"] = null });
        var root = Root();
        var state = Valid(root);
        var serialized = (string)Call("SerializeCanonical", state);
        root["pending"]!["requestId"] = "mutated-alias";
        Assert.Equal(serialized, Call("SerializeCanonical", state));
        Assert.Equal("started", Property<string>(Call("PlanAdvance", empty, state), "Disposition"));
        Assert.Equal("exact_replay", Property<string>(Call("PlanAdvance", state, state), "Disposition"));
        var retained = JsonNode.Parse(serialized)!["pending"]!["preservedDraft"]!["contentBase64"]!.GetValue<string>();
        Assert.Equal(" { \"draftFixture\": true }\r\n", Encoding.UTF8.GetString(Convert.FromBase64String(retained)));
    }

    /// <summary>
    /// Rejects a malformed or re-sealed packet before exposing partial state.
    /// </summary>
    /// <param name="change">
    /// Intrinsic packet contradiction.
    /// </param>
    [Theory]
    [InlineData("root-field")]
    [InlineData("packet-field")]
    [InlineData("generation")]
    [InlineData("cursor")]
    [InlineData("source-order")]
    [InlineData("source-binding")]
    [InlineData("source-digest")]
    [InlineData("packet-digest")]
    [InlineData("prefix-digest")]
    [InlineData("draft-bytes")]
    [InlineData("draft-digest")]
    [InlineData("image-role")]
    [InlineData("image-path")]
    [InlineData("image-duplicate")]
    [InlineData("claims")]
    public void Parse_RejectsPacketContradictions(string change)
    {
        var root = Root();
        var packet = root["pending"]!.AsObject();
        switch (change)
        {
            case "root-field": root["extra"] = true; break;
            case "packet-field": packet["extra"] = true; break;
            case "generation": packet["continuationGeneration"] = 3; break;
            case "cursor": packet["cursor"]!["nextSourceOrdinal"] = 1; break;
            case "source-order": packet["sources"]![0]!["sourceOrdinal"] = 1; SealSource(packet["sources"]![0]!.AsObject()); break;
            case "source-binding": packet["requestId"] = "foreign-original"; break;
            case "source-digest": packet["sources"]![0]!["coordinate"] = "changed"; break;
            case "packet-digest": packet["packetFingerprint"] = Fingerprint; break;
            case "prefix-digest": packet["retainedPrefixFingerprint"] = Fingerprint; break;
            case "draft-bytes": packet["preservedDraft"]!["contentBase64"] = "bm90LWpzb24="; break;
            case "draft-digest": packet["preservedDraft"]!["contentFingerprint"] = Fingerprint; break;
            case "image-role": packet["beforeImages"]![0]!["role"] = "candidate_after"; break;
            case "image-path": packet["beforeImages"]![0]!["path"] = "../outside"; break;
            case "image-duplicate": packet["beforeImages"]!.AsArray().Add(packet["beforeImages"]![0]!.DeepClone()); break;
            case "claims": packet["diceClaims"] = new JsonArray(); break;
        }
        if (change != "packet-digest") SealPacket(packet, change != "prefix-digest");
        Invalid(root.ToJsonString());
    }

    /// <summary>
    /// Advances one explicit decline and rejects removing, rewriting or implicitly consuming it.
    /// </summary>
    [Fact]
    public void PlanAdvance_PreservesFrozenEvidenceAndStagedDecisions()
    {
        var root = Root();
        var before = Valid(root);
        StageDecline(root);
        var staged = Valid(root);
        Assert.Equal("advanced", Property<string>(Call("PlanAdvance", before, staged), "Disposition"));
        Assert.Equal("conflict", Property<string>(Call("PlanAdvance", staged, before), "Disposition"));
        var changed = root.DeepClone().AsObject();
        changed["pending"]!["stagedDecisions"]![0]!["opportunityRef"] = "changed-opportunity";
        SealDecision(changed["pending"]!["stagedDecisions"]![0]!.AsObject());
        SealPacket(changed["pending"]!.AsObject());
        Assert.Equal("conflict", Property<string>(Call("PlanAdvance", staged, Valid(changed)), "Disposition"));
        var empty = Valid(new JsonObject { ["schemaVersion"] = 1, ["pending"] = null });
        Assert.Equal("conflict", Property<string>(Call("PlanAdvance", staged, empty), "Disposition"));
    }

    /// <summary>
    /// Preserves zero-ceiling evidence without fabricating a decision slot for it.
    /// </summary>
    [Fact]
    public void Parse_SkipsZeroCeilingSourcesInDecisionPrefix()
    {
        var root = TwoWaves();
        var packet = root["pending"]!.AsObject();
        packet["continuationGeneration"] = 1;
        packet["cursor"]!["waveOrdinal"] = 0;
        packet["stagedDecisions"] = new JsonArray();
        var zero = packet["sources"]![0]!.AsObject();
        zero["dangerMode"] = "training";
        zero["maximumSeverityRank"] = 0;
        SealSource(zero);
        SealPacket(packet);
        Valid(root);
        packet["sources"]![1]!["maximumSeverityRank"] = 0;
        packet["sources"]![1]!["dangerMode"] = "training";
        SealSource(packet["sources"]![1]!.AsObject());
        SealPacket(packet);
        Invalid(root.ToJsonString());
    }

    /// <summary>
    /// Advances to a later eligible exchange only after the earlier decision is staged.
    /// </summary>
    [Fact]
    public void PlanAdvance_AcceptsNextWaveWithoutChangingOriginalEvidence()
    {
        var first = Root();
        var next = TwoWaves();
        var result = Call("PlanAdvance", Valid(first), Valid(next));
        Assert.Equal("advanced", Property<string>(result, "Disposition"));
        Assert.Equal("conflict", Property<string>(Call("PlanAdvance", Valid(next), Valid(first)), "Disposition"));
    }

    /// <summary>
    /// Rejects crossing an unresolved wave, reusing its wave ordinal or changing the conflict display identity.
    /// </summary>
    /// <param name="change">
    /// Structurally re-sealed cross-source contradiction.
    /// </param>
    [Theory]
    [InlineData("unresolved")]
    [InlineData("wave")]
    [InlineData("conflict")]
    public void Parse_RejectsCrossWaveContradictions(string change)
    {
        var root = TwoWaves();
        Valid(root);
        var packet = root["pending"]!.AsObject();
        if (change == "unresolved")
        {
            packet["stagedDecisions"] = new JsonArray();
            packet["cursor"]!["nextSourceOrdinal"] = 0;
        }
        if (change == "wave") packet["cursor"]!["waveOrdinal"] = 0;
        if (change == "conflict")
        {
            packet["sources"]![1]!["conflictId"] = "another-conflict";
            SealSource(packet["sources"]![1]!.AsObject());
        }
        SealPacket(packet);
        Invalid(root.ToJsonString());
    }

    /// <summary>
    /// Keeps original snapshots and source witnesses immutable while allowing only actual progress.
    /// </summary>
    /// <param name="change">
    /// Independently valid replacement that violates the transition contract.
    /// </param>
    [Theory]
    [InlineData("before-image")]
    [InlineData("source")]
    [InlineData("draft-only")]
    public void PlanAdvance_RejectsChangedFrozenEvidenceOrNoProgress(string change)
    {
        var root = Root();
        var before = Valid(root);
        if (change == "draft-only")
        {
            var bytes = Encoding.UTF8.GetBytes("{\"replacement\":true}");
            root["pending"]!["preservedDraft"]!["contentBase64"] = Convert.ToBase64String(bytes);
            root["pending"]!["preservedDraft"]!["contentFingerprint"] = BytesFingerprint(bytes);
        }
        else
        {
            StageDecline(root);
            if (change == "before-image")
                root["pending"]!["beforeImages"]![0] = Image("original_before", true, []);
            else
            {
                var source = root["pending"]!["sources"]![0]!.AsObject();
                source["coordinate"] = "changed-coordinate";
                SealSource(source);
                var decision = root["pending"]!["stagedDecisions"]![0]!.AsObject();
                decision["sourceId"] = source["sourceId"]!.DeepClone();
                SealDecision(decision);
            }
        }
        SealPacket(root["pending"]!.AsObject());
        var result = Call("PlanAdvance", before, Valid(root));
        Assert.Equal("conflict", Property<string>(result, "Disposition"));
        Assert.Null(Property<object?>(result, "After"));
    }

    /// <summary>
    /// Enforces strict draft bytes and wrapper fields without claiming proposal intake authority.
    /// </summary>
    [Fact]
    public void Parse_RejectsMalformedRootsAndDraftBytes()
    {
        foreach (var text in new[] { "", "null", "[]", "{\"schemaVersion\":1}", "{\"schemaVersion\":1,\"pending\":null,\"pending\":null}" })
            Invalid(text);
        foreach (var text in new[] { "[]", "{\"a\":1,\"a\":1}", "invalid" })
        {
            var root = Root();
            var bytes = Encoding.UTF8.GetBytes(text);
            root["pending"]!["preservedDraft"]!["contentBase64"] = Convert.ToBase64String(bytes);
            root["pending"]!["preservedDraft"]!["contentFingerprint"] = BytesFingerprint(bytes);
            SealPacket(root["pending"]!.AsObject());
            Invalid(root.ToJsonString());
        }
        var empty = Root();
        empty["pending"]!["sources"] = new JsonArray();
        SealPacket(empty["pending"]!.AsObject());
        Invalid(empty.ToJsonString());
    }

    /// <summary>
    /// Checks staged materialization null rules, source bounds and strict retained draft bytes.
    /// </summary>
    /// <param name="change">
    /// Materialization contradiction.
    /// </param>
    [Theory]
    [InlineData("null-draft")]
    [InlineData("ceiling")]
    [InlineData("draft-digest")]
    [InlineData("unknown-field")]
    public void Parse_ChecksStagedMaterializationSyntax(string change)
    {
        var root = Root();
        StageDecline(root);
        var decision = root["pending"]!["stagedDecisions"]![0]!.AsObject();
        var bytes = Encoding.UTF8.GetBytes("{\"proposalFixture\":true}");
        decision["decision"] = "materialize";
        decision["selectedSeverityRank"] = 1;
        decision["woundDraftBase64"] = Convert.ToBase64String(bytes);
        decision["woundDraftFingerprint"] = BytesFingerprint(bytes);
        SealDecision(decision);
        SealPacket(root["pending"]!.AsObject());
        Valid(root);
        switch (change)
        {
            case "null-draft": decision["woundDraftBase64"] = null; break;
            case "ceiling": decision["selectedSeverityRank"] = 2; break;
            case "draft-digest": decision["woundDraftFingerprint"] = Fingerprint; break;
            case "unknown-field": decision["extra"] = true; break;
        }
        SealDecision(decision);
        SealPacket(root["pending"]!.AsObject());
        Invalid(root.ToJsonString());
    }

    /// <summary>
    /// Reserves wave exhaustion for a fully handled and executed packet.
    /// </summary>
    [Fact]
    public void Parse_RejectsPrematureWaveExhaustionAndAllowsCompletedSentinel()
    {
        var root = Root();
        root["pending"]!["cursor"]!["waveOrdinal"] = 2;
        SealPacket(root["pending"]!.AsObject());
        Invalid(root.ToJsonString());
        var complete = TwoWaves();
        var before = Valid(complete);
        StageSecond(complete);
        complete["pending"]!["cursor"]!["waveOrdinal"] = 2;
        SealPacket(complete["pending"]!.AsObject());
        Assert.Equal("advanced", Property<string>(Call("PlanAdvance", before, Valid(complete)), "Disposition"));
    }

    /// <summary>
    /// Prevents a later offer from retaining the old generation or deciding multiple waves at once.
    /// </summary>
    /// <param name="change">
    /// Invalid replacement of the original offered wave.
    /// </param>
    [Theory]
    [InlineData("generation")]
    [InlineData("bundled")]
    public void PlanAdvance_RejectsInvalidWaveProgression(string change)
    {
        var before = Valid(Root());
        var root = TwoWaves();
        if (change == "generation") root["pending"]!["continuationGeneration"] = 1;
        else StageSecond(root);
        SealPacket(root["pending"]!.AsObject());
        Assert.Equal("conflict", Property<string>(Call("PlanAdvance", before, Valid(root)), "Disposition"));
    }

    /// <summary>
    /// Prevents a newly appended side from changing an already completed exchange's source frontier.
    /// </summary>
    [Fact]
    public void PlanAdvance_RejectsLateSourceBehindCompletedFrontier()
    {
        var root = Root();
        var packet = root["pending"]!.AsObject();
        var pair = SpiritualWoundOpportunityReceiptStateTests.TwoSides()["sources"]!.AsArray();
        var first = pair[0]!["witness"]!.DeepClone().AsObject();
        var second = pair[1]!["witness"]!.DeepClone().AsObject();
        foreach (var source in new[] { first, second })
        {
            source["turnEvidence"]!["bounds"]!["imagePathCount"] = Paths.Length;
            SealSource(source);
        }
        packet["sources"] = new JsonArray(first);
        foreach (var field in new[] { "sessionId", "requestId", "snapshotToken", "turn", "realm", "originalSnapshotFingerprint", "bounds" })
            packet[field] = first["turnEvidence"]![field]!.DeepClone();
        packet["diceClaims"] = first["turnEvidence"]!["diceClaims"]!.DeepClone();
        SealPacket(packet);
        var before = Valid(root);
        packet["sources"]!.AsArray().Add(second);
        SealPacket(packet);
        Assert.Equal("conflict", Property<string>(Call("PlanAdvance", before, Valid(root)), "Disposition"));
    }

    /// <summary>
    /// Rejects a staged re-trauma severity that does not worsen the retained wound.
    /// </summary>
    [Fact]
    public void Parse_RequiresRetraumaSeverityIncrease()
    {
        var root = Root();
        var packet = root["pending"]!.AsObject();
        var source = packet["sources"]![0]!.AsObject();
        var wound = WoundContractTestData.CreateSpiritualActiveWound("wound-older", "chaos_sea",
            "guardian", "guardian-a", WoundCarrierCatalog.AfterlifeProfilesPath);
        var rank = WoundMaterializationContract.Parse(wound.ToJsonString(), "test.wound").Wound!.Severity.Rank;
        var exchange = JsonNode.Parse(Encoding.UTF8.GetString(Convert.FromBase64String(source["exchangeJsonBase64"]!.GetValue<string>())))!.AsObject();
        exchange["after"]!["oppositionSideStrain"] = "broken";
        exchange["spiritualWoundTarget"] = new JsonObject
        {
            ["actorType"] = "guardian", ["actorId"] = "guardian-a", ["retraumaWoundRef"] = "wound-older"
        };
        source["destinationStrainRank"] = 4;
        source["extraStrainJumps"] = 3;
        source["maximumSeverityRank"] = 4;
        source["retraumaWoundId"] = "wound-older";
        source["retraumaWoundJsonBase64"] = Convert.ToBase64String(Encoding.UTF8.GetBytes(SpiritualWoundStateJson.Canonical(wound)));
        source["exchangeJsonBase64"] = Convert.ToBase64String(Encoding.UTF8.GetBytes(SpiritualWoundStateJson.Canonical(exchange)));
        SealSource(source);
        StageDecline(root);
        var decision = packet["stagedDecisions"]![0]!.AsObject();
        decision["decision"] = "materialize";
        decision["selectedSeverityRank"] = rank + 1;
        var bytes = Encoding.UTF8.GetBytes("{}");
        decision["woundDraftBase64"] = Convert.ToBase64String(bytes);
        decision["woundDraftFingerprint"] = BytesFingerprint(bytes);
        SealDecision(decision);
        SealPacket(packet);
        Valid(root);
        decision["selectedSeverityRank"] = rank;
        SealDecision(decision);
        SealPacket(packet);
        Invalid(root.ToJsonString());
    }

    /// <summary>
    /// Starts with an offer rather than importing already staged decisions through an empty root.
    /// </summary>
    [Fact]
    public void PlanAdvance_RejectsStagedStart()
    {
        var root = Root();
        StageDecline(root);
        var empty = Valid(new JsonObject { ["schemaVersion"] = 1, ["pending"] = null });
        Assert.Equal("conflict", Property<string>(Call("PlanAdvance", empty, Valid(root)), "Disposition"));
    }

    /// <summary>
    /// Rejects inserting a die claim into a previously completed source-free exchange.
    /// </summary>
    [Fact]
    public void PlanAdvance_RejectsLateClaimBehindCompletedFrontier()
    {
        var root = Root();
        var packet = root["pending"]!.AsObject();
        packet["bounds"]!["exchangeCount"] = 3;
        packet["bounds"]!["sourceSlotCount"] = 6;
        packet["sources"]![0]!["turnEvidence"]!["bounds"] = packet["bounds"]!.DeepClone();
        SealSource(packet["sources"]![0]!.AsObject());
        StageDecline(root);
        packet["cursor"]!["exchangeOrdinal"] = 2;
        SealPacket(packet);
        var before = Valid(root);
        packet["cursor"]!["exchangeOrdinal"] = 3;
        var claim = new JsonObject { ["sourceIndex"] = 2, ["value"] = 15, ["exchangeOrdinal"] = 2, ["claimFingerprint"] = "" };
        claim["claimFingerprint"] = SpiritualWoundTurnEvidence.ComputeClaimFingerprint(claim);
        packet["diceClaims"]!.AsArray().Add(claim);
        SealPacket(packet);
        Assert.Equal("advanced", Property<string>(Call("PlanAdvance", before, Valid(root)), "Disposition"));
        claim["exchangeOrdinal"] = 1;
        claim["claimFingerprint"] = SpiritualWoundTurnEvidence.ComputeClaimFingerprint(claim);
        SealPacket(packet);
        Assert.Equal("conflict", Property<string>(Call("PlanAdvance", before, Valid(root)), "Disposition"));
    }

    /// <summary>
    /// Permits the terminal sentinel after noneligible exchanges without inventing new generations.
    /// </summary>
    [Fact]
    public void PlanAdvance_AllowsExhaustionWithFewerEligibleWaves()
    {
        var root = Root();
        var before = Valid(root);
        StageDecline(root);
        root["pending"]!["cursor"]!["exchangeOrdinal"] = 2;
        root["pending"]!["cursor"]!["waveOrdinal"] = 2;
        SealPacket(root["pending"]!.AsObject());
        Assert.Equal("advanced", Property<string>(Call("PlanAdvance", before, Valid(root)), "Disposition"));
    }

    /// <summary>
    /// Binds replacement to the same trusted capture inventory rather than only its path count.
    /// </summary>
    [Fact]
    public void PlanAdvance_RejectsChangedTrustedInventory()
    {
        var root = Root();
        var before = Valid(root);
        StageDecline(root);
        var otherPaths = new[] { Paths[0], "game_state/meta/other_registered_path.json" };
        root["pending"]!["candidateAfterImages"]![0]!["path"] = otherPaths[1];
        SealPacket(root["pending"]!.AsObject());
        var candidate = Valid(root, otherPaths);
        Assert.Equal("conflict", Property<string>(Call("PlanAdvance", before, candidate), "Disposition"));
    }

    /// <summary>
    /// Keeps an unresolved exchange on its exact existing wave when no next wave is opened.
    /// </summary>
    [Fact]
    public void PlanAdvance_RejectsRelabelingTheCurrentWave()
    {
        var root = Root();
        var before = Valid(root);
        root["pending"]!["cursor"]!["waveOrdinal"] = 1;
        SealPacket(root["pending"]!.AsObject());
        Assert.Equal("conflict", Property<string>(Call("PlanAdvance", before, Valid(root)), "Disposition"));
    }

    /// <summary>
    /// Stages the second exchange's explicit decline without changing its original source.
    /// </summary>
    /// <param name="root">
    /// Two-wave fixture with the first decision already staged.
    /// </param>
    private static void StageSecond(JsonObject root)
    {
        var packet = root["pending"]!.AsObject();
        var decision = packet["stagedDecisions"]![0]!.DeepClone().AsObject();
        decision["opportunityRef"] = "opportunity-b";
        decision["sourceId"] = packet["sources"]![1]!["sourceId"]!.DeepClone();
        decision["sourceOrdinal"] = 1;
        decision["waveOrdinal"] = 1;
        SealDecision(decision);
        packet["stagedDecisions"]!.AsArray().Add(decision);
        packet["cursor"]!["nextSourceOrdinal"] = 2;
        SealPacket(packet);
    }

    /// <summary>
    /// Creates the next wave with the first source declined and original dice reserved only once.
    /// </summary>
    /// <returns>
    /// Mutable two-exchange comparison fixture, not original execution authority.
    /// </returns>
    private static JsonObject TwoWaves()
    {
        var root = Root();
        StageDecline(root);
        var packet = root["pending"]!.AsObject();
        var source = packet["sources"]![0]!.DeepClone().AsObject();
        source["sourceOrdinal"] = 1;
        source["exchangeOrdinal"] = 1;
        source["exchangeId"] = "exchange-b";
        source["selectedDiceIndices"] = new JsonArray(2, 3);
        var exchange = JsonNode.Parse(Encoding.UTF8.GetString(Convert.FromBase64String(source["exchangeJsonBase64"]!.GetValue<string>())))!.AsObject();
        exchange["exchangeId"] = "exchange-b";
        exchange["diceAudit"]!["diceUsed"]![0]!["sourceIndex"] = 2;
        exchange["diceAudit"]!["diceUsed"]![1]!["sourceIndex"] = 3;
        source["exchangeJsonBase64"] = Convert.ToBase64String(Encoding.UTF8.GetBytes(SpiritualWoundStateJson.Canonical(exchange)));
        foreach (var claim in source["turnEvidence"]!["diceClaims"]!.AsArray())
        {
            claim!["exchangeOrdinal"] = 1;
            claim["sourceIndex"] = claim["sourceIndex"]!.GetValue<int>() + 2;
            claim["claimFingerprint"] = SpiritualWoundTurnEvidence.ComputeClaimFingerprint(claim.AsObject());
            packet["diceClaims"]!.AsArray().Add(claim.DeepClone());
        }
        SealSource(source);
        packet["sources"]!.AsArray().Add(source);
        packet["continuationGeneration"] = 2;
        packet["cursor"]!["exchangeOrdinal"] = 2;
        packet["cursor"]!["waveOrdinal"] = 1;
        SealPacket(packet);
        return root;
    }

    /// <summary>
    /// Creates a single offered positive source with strict original context and exact bytes.
    /// </summary>
    /// <returns>
    /// Mutable comparison fixture; the draft is syntax-only and is not an admitted game response.
    /// </returns>
    private static JsonObject Root()
    {
        var source = SpiritualWoundSourceWitnessTests.Source();
        source["turnEvidence"]!["bounds"]!["imagePathCount"] = Paths.Length;
        source["turnEvidence"]!["acceptedD20Values"] = new JsonArray(15, 5, 15, 5);
        SealSource(source);
        var bytes = Encoding.UTF8.GetBytes(" { \"draftFixture\": true }\r\n");
        var packet = new JsonObject
        {
            ["sessionId"] = "session-a", ["requestId"] = "request-a", ["snapshotToken"] = "snapshot-a",
            ["turn"] = 42, ["realm"] = "chaos_sea", ["continuationGeneration"] = 1,
            ["conflictInstanceRef"] = "instance-a", ["originalSnapshotFingerprint"] = Fingerprint,
            ["retainedPrefixFingerprint"] = "", ["bounds"] = source["turnEvidence"]!["bounds"]!.DeepClone(),
            ["cursor"] = new JsonObject { ["exchangeOrdinal"] = 1, ["waveOrdinal"] = 0, ["nextSourceOrdinal"] = 0 },
            ["sources"] = new JsonArray(source), ["stagedDecisions"] = new JsonArray(),
            ["diceClaims"] = source["turnEvidence"]!["diceClaims"]!.DeepClone(),
            ["beforeImages"] = new JsonArray(Image("original_before", false, null)),
            ["candidateAfterImages"] = new JsonArray(Image("candidate_after", true, Encoding.UTF8.GetBytes("{}"))),
            ["preservedDraft"] = new JsonObject
            {
                ["contentBase64"] = Convert.ToBase64String(bytes),
                ["contentFingerprint"] = BytesFingerprint(bytes)
            },
            ["packetFingerprint"] = ""
        };
        SealPacket(packet);
        return new JsonObject { ["schemaVersion"] = 1, ["pending"] = packet };
    }

    /// <summary>
    /// Constructs a raw image using the existing existence-aware canonical fingerprint.
    /// </summary>
    /// <param name="role">
    /// Original or candidate collection role.
    /// </param>
    /// <param name="existed">
    /// Whether the file exists.
    /// </param>
    /// <param name="bytes">
    /// Exact bytes for an existing file, otherwise <see langword="null"/>.
    /// </param>
    /// <returns>
    /// Closed image fixture.
    /// </returns>
    private static JsonObject Image(string role, bool existed, byte[]? bytes) => new()
    {
        ["path"] = Paths[0], ["role"] = role, ["existed"] = existed,
        ["contentBase64"] = bytes is null ? null : Convert.ToBase64String(bytes),
        ["contentFingerprint"] = new CanonicalBeforeImage(existed, bytes).Fingerprint
    };

    /// <summary>
    /// Adds the offered source's explicit no-wound decision to the unfinished candidate.
    /// </summary>
    /// <param name="root">
    /// Mutable active packet fixture.
    /// </param>
    private static void StageDecline(JsonObject root)
    {
        var packet = root["pending"]!.AsObject();
        var source = packet["sources"]![0]!;
        var decision = new JsonObject
        {
            ["opportunityRef"] = "opportunity-a", ["sourceId"] = source["sourceId"]!.DeepClone(),
            ["sourceOrdinal"] = 0, ["waveOrdinal"] = 0, ["decision"] = "none",
            ["selectedSeverityRank"] = null, ["woundDraftBase64"] = null,
            ["woundDraftFingerprint"] = null, ["decisionFingerprint"] = ""
        };
        SealDecision(decision);
        packet["stagedDecisions"]!.AsArray().Add(decision);
        packet["cursor"]!["nextSourceOrdinal"] = 1;
        SealPacket(packet);
    }

    /// <summary>
    /// Recomputes the source's comparison identity without authenticating its origin.
    /// </summary>
    /// <param name="source">
    /// Mutable witness fixture.
    /// </param>
    private static void SealSource(JsonObject source)
    {
        var hash = SpiritualWoundStateJson.Hash(source, "source", "sourceId", "sourceFingerprint");
        source["sourceId"] = "spiritual_source_" + hash[7..];
        source["sourceFingerprint"] = hash;
    }

    /// <summary>
    /// Recomputes a staged decision comparison digest.
    /// </summary>
    /// <param name="decision">
    /// Mutable staged decision.
    /// </param>
    private static void SealDecision(JsonObject decision) =>
        decision["decisionFingerprint"] = SpiritualWoundStateJson.Hash(decision, "staged_decision", "decisionFingerprint");

    /// <summary>
    /// Recomputes prefix and packet comparison digests for semantic negative tests.
    /// </summary>
    /// <param name="packet">
    /// Mutable packet.
    /// </param>
    /// <param name="sealPrefix">
    /// Whether to update the prefix digest; <see langword="false"/> preserves a deliberately corrupted value.
    /// </param>
    private static void SealPacket(JsonObject packet, bool sealPrefix = true)
    {
        if (sealPrefix)
        {
            var prefix = new JsonObject();
            foreach (var field in new[] { "cursor", "sources", "stagedDecisions", "diceClaims", "candidateAfterImages" })
                prefix[field] = packet[field]!.DeepClone();
            packet["retainedPrefixFingerprint"] = SpiritualWoundStateJson.Hash(prefix, "pending_prefix");
        }
        packet["packetFingerprint"] = SpiritualWoundStateJson.Hash(packet, "pending_packet", "packetFingerprint");
    }

    /// <summary>
    /// Hashes exact decoded payload bytes without changing their whitespace.
    /// </summary>
    /// <param name="bytes">
    /// Original payload bytes.
    /// </param>
    /// <returns>
    /// Lowercase SHA-256 comparison fingerprint.
    /// </returns>
    private static string BytesFingerprint(byte[] bytes) => "sha256:" + Convert.ToHexString(SHA256.HashData(bytes)).ToLowerInvariant();

    /// <summary>
    /// Requires a valid detached packet parse.
    /// </summary>
    /// <param name="root">
    /// Serialized fixture root.
    /// </param>
    /// <param name="registeredPaths">
    /// Optional trusted inventory override; <see langword="null"/> uses the fixture inventory.
    /// </param>
    /// <returns>
    /// Opaque validated comparison state.
    /// </returns>
    private static object Valid(JsonObject root, string[]? registeredPaths = null)
    {
        var result = Call("Parse", root.ToJsonString(), Path, registeredPaths ?? Paths);
        Assert.True(Property<bool>(result, "IsValid"));
        return Property<object>(result, "State");
    }

    /// <summary>
    /// Requires diagnostics and no state for invalid packet bytes.
    /// </summary>
    /// <param name="json">
    /// Malformed serialized root.
    /// </param>
    private static void Invalid(string json)
    {
        var result = Call("Parse", json, Path, Paths);
        Assert.False(Property<bool>(result, "IsValid"));
        Assert.Null(Property<object?>(result, "State"));
        Assert.NotEmpty(Property<IReadOnlyList<ValidationIssue>>(result, "Issues"));
    }

    /// <summary>
    /// Invokes the internal packet API, permitting missing-type RED before implementation.
    /// </summary>
    /// <param name="name">
    /// Exact static method name.
    /// </param>
    /// <param name="arguments">
    /// Method arguments.
    /// </param>
    /// <returns>
    /// Non-null method result.
    /// </returns>
    private static object Call(string name, params object?[] arguments)
    {
        var type = typeof(ValidationService).Assembly.GetType("BookOfEternityClient.Services.SpiritualWoundDecisionPendingState");
        Assert.NotNull(type);
        var method = type.GetMethod(name, BindingFlags.Static | BindingFlags.NonPublic);
        Assert.NotNull(method);
        return method.Invoke(null, arguments)!;
    }

    /// <summary>
    /// Reads an opaque parser or reducer result property.
    /// </summary>
    /// <typeparam name="T">
    /// Expected property type.
    /// </typeparam>
    /// <param name="result">
    /// Parsed result object.
    /// </param>
    /// <param name="name">
    /// Property name.
    /// </param>
    /// <returns>
    /// The property value.
    /// </returns>
    private static T Property<T>(object result, string name) => (T)result.GetType().GetProperty(name)!.GetValue(result)!;
}
