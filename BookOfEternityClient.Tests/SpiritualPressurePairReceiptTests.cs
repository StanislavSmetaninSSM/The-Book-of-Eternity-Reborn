using System.Text;
using System.Text.Json.Nodes;
using BookOfEternityClient.Services;
using Xunit;

namespace BookOfEternityClient.Tests;

/// <summary>Checks immutable pair receipt DATA and joins without manufacturing signed provenance or live C3 authority.</summary>
public sealed partial class SpiritualPressurePairReceiptTests
{
    /// <summary>One positive v4 source has one declined decision, one comparison and both exact recorded parent anchors.</summary>
    [Theory]
    [InlineData("fractured")]
    [InlineData("overwhelmed")]
    public void PairReceiptRetainsOneDeclinedComparisonAndTwoAnchors(string ordinaryStrain)
    {
        var source = CreateSupportedSource(ordinaryStrain);
        var root = CreateLedger(source);
        var before = root.ToJsonString();
        var state = ReadLedger(root);
        Assert.Single(root["sources"]!.AsArray());
        var decision = Assert.Single(root["decisions"]!.AsArray())!;
        Assert.Equal("none", decision["decision"]!.GetValue<string>());
        Assert.Null(decision["selectedSeverityRank"]);
        Assert.Null(decision["woundId"]);
        Assert.Null(decision["transitionId"]);
        Assert.True(state.HasRetainedPressureComparisons);
        var history = CreateHistory(source);
        Assert.Equal(2, history["transitions"]!.AsArray().Count);
        Assert.True(state.HasExactStrainHistory(ReadHistory(history)));
        var actual = ReadActual(source);
        var retained = state.ReadRetainedStrainOrdinaryAfter("chaos_sea", "conflict-a", actual, false);
        Assert.NotNull(retained);
        Assert.True(JsonNode.DeepEquals(ReadOrdinary(source)["after"], retained));
        Assert.Equal(ordinaryStrain, retained!["playerSideStrain"]!.GetValue<string>());
        Assert.Equal(before, root.ToJsonString());
    }

    /// <summary>Valid intrinsic v4 and receipt DATA do not replace the capture's private pair issuance key.</summary>
    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public void PairDeclineRequiresCurrentAuthenticatedC3Pair(bool forgedPair)
    {
        var source = CreateSupportedSource();
        var root = CreateLedger(source);
        var state = ReadLedger(root);
        Assert.True(state.HasExactStrainHistory(ReadHistory(CreateHistory(source))));
        var staged = new JsonObject
        {
            ["opportunityRef"] = "opportunity_pressure_pair_declined", ["sourceId"] = source["sourceId"]!.DeepClone(),
            ["decisionFingerprint"] = "", ["sourceOrdinal"] = 0, ["waveOrdinal"] = 0,
            ["decision"] = "none", ["selectedSeverityRank"] = null,
            ["woundDraftBase64"] = null, ["woundDraftFingerprint"] = null
        };
        staged["decisionFingerprint"] = SpiritualWoundStateJson.Hash(staged, "staged_decision", "decisionFingerprint");
        var evidence = source["turnEvidence"]!;
        var packet = new JsonObject
        {
            ["sources"] = new JsonArray(source.DeepClone()), ["stagedDecisions"] = new JsonArray(staged),
            ["cursor"] = new JsonObject { ["nextSourceOrdinal"] = 1 }, ["realm"] = evidence["realm"]!.DeepClone(),
            ["sessionId"] = evidence["sessionId"]!.DeepClone(), ["requestId"] = evidence["requestId"]!.DeepClone(),
            ["snapshotToken"] = evidence["snapshotToken"]!.DeepClone(), ["turn"] = evidence["turn"]!.DeepClone(),
            ["originalSnapshotFingerprint"] = evidence["originalSnapshotFingerprint"]!.DeepClone(),
            ["conflictInstanceRef"] = root["instances"]![0]!["instanceId"]!.DeepClone()
        };
        var signed = new ValidationService.SpiritualSignedC1Origin(new CanonicalBeforeImage(false, null),
            new CanonicalBeforeImage(true, Encoding.UTF8.GetBytes(
                "{\"schemaVersion\":1,\"activeConflict\":{\"conflictId\":\"conflict-a\"}}")),
            evidence["originalSnapshotFingerprint"]!.GetValue<string>());
        // Deliberately arbitrary objects: this constructor must not counterfeit private owner issuance.
        var pair = forgedPair ? new ValidationService.SpiritualOriginalTurnCapture.AuthenticatedC3Pair(
            new object(), signed, packet, null, new object()) : null;
        var before = packet.ToJsonString();
        var result = SpiritualWoundDeclineReceiptReducer.Reduce(signed, packet, authenticatedPair: pair);
        Assert.Null(result.AfterImage);
        Assert.Equal("spiritual_c3_materialization_owner_pair_missing", Assert.Single(result.Issues).Code);
        Assert.Equal(before, packet.ToJsonString());
    }

    /// <summary>Each mutation starts from supported v4 and leaves the primary witness valid before a containing join refuses.</summary>
    [Theory]
    [InlineData("instance")]
    [InlineData("source-row-instance")]
    [InlineData("source")]
    [InlineData("session")]
    [InlineData("request")]
    [InlineData("snapshot-token")]
    [InlineData("turn")]
    [InlineData("source-ordinal")]
    [InlineData("wave-ordinal")]
    [InlineData("global-ordinal")]
    [InlineData("instance-source-ordinal")]
    [InlineData("source-fingerprint")]
    [InlineData("decision-fingerprint")]
    [InlineData("exchange-witness")]
    [InlineData("second-parent-witness")]
    public void PairReceiptRequiresExactDecisionSourceJoins(string change)
    {
        var source = CreateSupportedSource();
        var root = CreateLedger(source);
        _ = ReadLedger(root);
        var decision = root["decisions"]![0]!.AsObject();
        switch (change)
        {
            case "instance": decision["instanceId"] = "spiritual_instance_" + new string('d', 64); break;
            case "source-row-instance": root["sources"]![0]!["instanceId"] = "spiritual_instance_" + new string('d', 64); break;
            case "source": decision["sourceId"] = "spiritual_source_" + new string('d', 64); break;
            case "session": decision["binding"]!["sessionId"] = "foreign_session"; break;
            case "request": decision["binding"]!["requestId"] = "foreign_request"; break;
            case "snapshot-token": decision["binding"]!["snapshotToken"] = "foreign_snapshot"; break;
            case "turn": decision["binding"]!["turn"] = 47; break;
            case "source-ordinal": decision["binding"]!["sourceOrdinal"] = 1; break;
            case "wave-ordinal": decision["binding"]!["waveOrdinal"] = 1; break;
            case "global-ordinal": decision["ordinal"] = 2; break;
            case "instance-source-ordinal": root["sources"]![0]!["instanceSourceOrdinal"] = 2; break;
            case "source-fingerprint": decision["sourceFingerprint"] = "sha256:" + new string('d', 64); break;
            case "decision-fingerprint": break;
            case "exchange-witness":
                var duplicate = decision["sourceWitness"]!.AsObject();
                var actual = ReadActual(duplicate); var ordinary = ReadOrdinary(duplicate);
                duplicate["exchangeId"] = "exchange_pair_foreign";
                actual["exchangeId"] = "exchange_pair_foreign"; ordinary["exchangeId"] = "exchange_pair_foreign";
                duplicate["exchangeJsonBase64"] = Encode(actual);
                duplicate["strainPreimage"]!["ordinaryExchangeJsonBase64"] = Encode(ordinary);
                Seal(duplicate); AssertSupported(duplicate);
                break;
            case "second-parent-witness":
                var altered = decision["sourceWitness"]!.AsObject();
                ChangeSecondParentDescription(altered);
                AssertSupported(altered);
                break;
            default: throw new ArgumentOutOfRangeException(nameof(change));
        }
        decision["decisionFingerprint"] = change == "decision-fingerprint"
            ? "sha256:" + new string('e', 64)
            : SpiritualWoundStateJson.Hash(decision, "decision", "decisionFingerprint");
        AssertSupported(root["sources"]![0]!["witness"]!.AsObject());
        AssertInvalidLedger(root);
    }

    /// <summary>A second valid source cannot allocate another comparison at an already retained exchange-side coordinate.</summary>
    [Fact]
    public void PairReceiptRejectsDuplicateActualComparisonCoordinate()
    {
        var first = CreateSupportedSource();
        var root = CreateLedger(first);
        _ = ReadLedger(root);
        var duplicate = first.DeepClone().AsObject();
        duplicate["turnEvidence"]!["requestId"] = "request_pair_duplicate";
        duplicate["turnEvidence"]!["snapshotToken"] = "snapshot_pair_duplicate";
        Seal(duplicate); AssertSupported(duplicate);
        AppendSource(root, duplicate);
        AssertInvalidLedger(root);
    }

    /// <summary>Builds fresh fixture DATA and requires the real supported v4 reader before a semantic case proceeds.</summary>
    private static JsonObject CreateSupportedSource(string ordinaryStrain = "fractured")
    {
        var source = SpiritualPressurePairTestData.CreateSource(ordinaryStrain);
        AssertSupported(source);
        return source;
    }

    /// <summary>Proves intrinsic v4 support with the independently fixed actual23/IV oracle and no input mutation.</summary>
    private static void AssertSupported(JsonObject source)
    {
        var before = source.ToJsonString();
        var calculation = SpiritualWoundSourceWitness.ValidateAndReadStrainData(source, out var retained);
        Assert.Equal((23L, 4, 4), (calculation.TraumaPressure, calculation.NewStrainRank, calculation.MaximumSeverityRank));
        var data = Assert.IsType<SpiritualStrainPreimageData>(retained);
        Assert.Equal(4, data.Version);
        Assert.Equal(2, source["strainPreimage"]!["parents"]!.AsArray().Count);
        Assert.Equal(before, source.ToJsonString());
    }

    /// <summary>Reuses only the existing small receipt shape, then installs the complete witness and its matching decision.</summary>
    private static JsonObject CreateLedger(JsonObject source)
    {
        // Reuse the established cheap ledger shape. Its scalar-parent convenience reader cannot read v4.
        var shapeInput = source.DeepClone().AsObject(); shapeInput.Remove("strainPreimage");
        var root = SpiritualExistingPressureTestData.CreateLedger(shapeInput).Root;
        root["sources"]![0]!["witness"] = source.DeepClone();
        var decision = root["decisions"]![0]!.AsObject();
        decision["sourceWitness"] = source.DeepClone();
        SealDecision(decision);
        return root;
    }

    /// <summary>Appends a detached DATA row and one none decision with exact global and instance ordinals.</summary>
    private static void AppendSource(JsonObject root, JsonObject source)
    {
        var addition = CreateLedger(source);
        var ordinal = root["sources"]!.AsArray().Count + 1;
        var row = addition["sources"]![0]!.DeepClone().AsObject();
        row["ordinal"] = ordinal; row["instanceSourceOrdinal"] = ordinal;
        row["instanceId"] = root["instances"]![0]!["instanceId"]!.DeepClone();
        root["sources"]!.AsArray().Add(row); root["nextSourceOrdinal"] = ordinal + 1;
        var decision = addition["decisions"]![0]!.DeepClone().AsObject();
        decision["ordinal"] = ordinal; decision["instanceId"] = row["instanceId"]!.DeepClone();
        decision["decisionId"] = "decision_pair_declined_" + ordinal;
        decision["opportunityRef"] = "opportunity_pair_declined_" + ordinal;
        SealDecision(decision);
        root["decisions"]!.AsArray().Add(decision); root["nextDecisionOrdinal"] = ordinal + 1;
    }

    /// <summary>Changes a lawful complete second envelope and reseals lexical DATA without creating provenance.</summary>
    private static void ChangeSecondParentDescription(JsonObject source)
    {
        var row = source["strainPreimage"]!["parents"]![1]!.AsObject();
        var parent = Decode(row["parentWoundJsonBase64"]);
        parent["display"]!["description"] = "Духовная рана хранит иное полное описание.";
        var parsed = WoundMaterializationContract.Parse(parent.ToJsonString(), "unit.pair.changedParent");
        Assert.True(parsed.IsValid, string.Join("\n", parsed.Issues));
        row["parentWoundJsonBase64"] = Encode(parent); Seal(source);
    }

    /// <summary>Requires the real receipt parser to accept the full fixture before returning its immutable state.</summary>
    private static SpiritualWoundOpportunityReceiptState ReadLedger(JsonObject root)
    {
        var parsed = SpiritualWoundOpportunityReceiptState.Parse(root.ToJsonString(), "unit.pairPressureLedger");
        Assert.True(parsed.IsValid, string.Join("\n", parsed.Issues));
        return parsed.State!;
    }

    /// <summary>Requires a containing receipt refusal without state escape or caller mutation.</summary>
    private static void AssertInvalidLedger(JsonObject root)
    {
        var before = root.ToJsonString();
        var parsed = SpiritualWoundOpportunityReceiptState.Parse(before, "unit.pairPressureLedger");
        Assert.False(parsed.IsValid); Assert.Null(parsed.State);
        Assert.Contains(parsed.Issues, issue => issue.Code == "spiritual_wound_receipt_invalid_state");
        Assert.Equal(before, root.ToJsonString());
    }

    /// <summary>Requires independently valid immutable chronology before an exact-anchor join is tested.</summary>
    private static WoundHistoryState ReadHistory(JsonObject root)
    {
        var parsed = WoundHistoryState.Parse(root.ToJsonString(), WoundHistoryState.HistoryPath);
        Assert.True(parsed.IsValid, string.Join("\n", parsed.Issues));
        return parsed.State!;
    }

    /// <summary>Returns detached actual exchange DATA from the supplied full source.</summary>
    private static JsonObject ReadActual(JsonObject source) => Decode(source["exchangeJsonBase64"]);
    /// <summary>Returns detached ordinary exchange DATA from the supplied comparison.</summary>
    private static JsonObject ReadOrdinary(JsonObject source) => Decode(source["strainPreimage"]!["ordinaryExchangeJsonBase64"]);
    /// <summary>Uses the strict shared object codec for an owned detached fixture tree.</summary>
    private static JsonObject Decode(JsonNode? encoded) => SpiritualWoundStateJson.Parse(
        SpiritualWoundStateJson.DecodeUtf8JsonText(SpiritualWoundStateJson.DecodeJsonObjectBytes(encoded)));
    /// <summary>Encodes complete fixture JSON without invoking a live source producer.</summary>
    private static string Encode(JsonObject value) => SpiritualPressurePairTestData.Encode(value);
    /// <summary>Updates only lexical DATA digests, never accepted provenance or owner permission.</summary>
    private static void Seal(JsonObject source) => SpiritualPressurePairTestData.Seal(source);
    /// <summary>Recomputes the decision DATA digest after an explicit containing-join mutation.</summary>
    private static void SealDecision(JsonObject decision) => decision["decisionFingerprint"] =
        SpiritualWoundStateJson.Hash(decision, "decision", "decisionFingerprint");
}
