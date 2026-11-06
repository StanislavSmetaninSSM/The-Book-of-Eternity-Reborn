using System.Text;
using System.Text.Json.Nodes;
using BookOfEternityClient.Services;
using Xunit;

namespace BookOfEternityClient.Tests;

/// <summary>Checks v3 receipt and recorded-parent History DATA joins without granting accepted or current authority.</summary>
public sealed partial class SpiritualExistingPressureReceiptTests
{
    /// <summary>Even a declined positive v3 source requires the exact capture-issued completed C3 pair.</summary>
    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public void PressureDeclineRequiresCurrentAuthenticatedC3Pair(bool forgedPair)
    {
        var source = CreateSupportedSource();
        var (ledger, _) = CreateLedger(source);
        _ = ReadLedger(ledger);
        var staged = new JsonObject
        {
            ["opportunityRef"] = "opportunity_pressure_declined", ["sourceId"] = source["sourceId"]!.DeepClone(),
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
            ["conflictInstanceRef"] = ledger["instances"]![0]!["instanceId"]!.DeepClone()
        };
        var signed = new ValidationService.SpiritualSignedC1Origin(new CanonicalBeforeImage(false, null),
            new CanonicalBeforeImage(true, Encoding.UTF8.GetBytes(
                "{\"schemaVersion\":1,\"activeConflict\":{\"conflictId\":\"conflict-a\"}}")),
            evidence["originalSnapshotFingerprint"]!.GetValue<string>());
        // Calling an internal constructor is deliberately not evidence of the private owner issuance key.
        var pair = forgedPair ? new ValidationService.SpiritualOriginalTurnCapture.AuthenticatedC3Pair(
            new object(), signed, packet, null, new object()) : null;
        var before = packet.ToJsonString();
        var result = SpiritualWoundDeclineReceiptReducer.Reduce(signed, packet, authenticatedPair: pair);
        Assert.Null(result.AfterImage);
        var issue = Assert.Single(result.Issues);
        Assert.Equal("spiritual_c3_materialization_owner_pair_missing", issue.Code);
        Assert.Equal(before, packet.ToJsonString());
    }

    /// <summary>Joins the exact recorded parent, including after a later typed healing transition retires it.</summary>
    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public void PressureHistoryRetainsRecordedParentAfterLaterHealing(bool laterHealing)
    {
        var (root, parent) = CreateLedger(CreateSupportedSource());
        var ledger = ReadLedger(root);
        var history = CreateHistory(parent);
        Assert.True(ledger.HasExactStrainHistory(ReadHistory(history)));
        if (laterHealing)
        {
            var initial = history["transitions"]![0]!.AsObject();
            var heal = initial.DeepClone().AsObject();
            heal["transitionId"] = "transition_pressure_later_heal";
            heal["operationKey"] = "operation_pressure_later_heal";
            heal["ordinal"] = 2; heal["woundTransitionOrdinal"] = 2;
            heal["kind"] = "heal"; heal["turn"] = 45; heal["eventRef"] = "event_pressure_later_heal";
            heal["beforeFingerprint"] = initial["afterFingerprint"]!.DeepClone();
            heal["afterFingerprint"] = "sha256:" + new string('c', 64); heal["terminal"] = true;
            history["transitions"]!.AsArray().Add(heal); history["nextOrdinal"] = 3;
        }
        var before = history.ToJsonString();
        Assert.True(ledger.HasExactStrainHistory(ReadHistory(history)));
        Assert.Equal(before, history.ToJsonString());
        // This checks old immutable DATA only. No active root, current contribution or owner can be inferred.
    }

    /// <summary>Rejects a missing or changed exact recorded transition after valid intrinsic, receipt and History controls.</summary>
    [Theory]
    [InlineData("missing")]
    [InlineData("identity")]
    [InlineData("kind")]
    [InlineData("turn")]
    [InlineData("fingerprint")]
    public void PressureHistoryRequiresExactRecordedParentTransition(string change)
    {
        var source = CreateSupportedSource();
        if (change == "kind")
        {
            // A first History row must be create. Use a valid later anchor so this
            // negative reaches the exact-parent join instead of failing chronology.
            var laterParent = SpiritualExistingPressurePreimageTests.ReadParent(source);
            laterParent["lastTransition"]!["kind"] = "complicate";
            laterParent["lastTransition"]!["ordinal"] = 2;
            laterParent["lastTransition"]!["turn"] = 43;
            laterParent["lastTransition"]!["transitionId"] = "transition_pressure_complicate";
            source["strainPreimage"]!["parentWoundJsonBase64"] = Encode(laterParent);
            SpiritualExistingPressurePreimageTests.Seal(source);
            Assert.Equal(2, SpiritualWoundSourceWitness.Validate(source).MaximumSeverityRank);
        }
        var (root, parent) = CreateLedger(source);
        var ledger = ReadLedger(root);
        var history = CreateHistory(parent);
        Assert.True(ledger.HasExactStrainHistory(ReadHistory(history)));
        var transition = history["transitions"]!.AsArray().Last()!.AsObject();
        switch (change)
        {
            case "missing": history["transitions"]!.AsArray().Clear(); history["nextOrdinal"] = 1; break;
            case "identity": transition["transitionId"] = "foreign_transition"; break;
            case "kind": transition["kind"] = "worsen"; break;
            case "turn": transition["turn"] = 43; break;
            case "fingerprint": transition["afterFingerprint"] = "sha256:" + new string('d', 64); break;
            default: throw new ArgumentOutOfRangeException(nameof(change));
        }
        var before = history.ToJsonString();
        Assert.False(ledger.HasExactStrainHistory(ReadHistory(history)));
        Assert.Equal(before, history.ToJsonString());
    }

    /// <summary>Requires decision duplication and binding to identify the exact valid intrinsic v3 source row.</summary>
    [Theory]
    [InlineData("instance")]
    [InlineData("request")]
    [InlineData("source")]
    [InlineData("exchange")]
    [InlineData("ordinal")]
    public void PressureReceiptRequiresExactDecisionSourceJoins(string change)
    {
        var (root, _) = CreateLedger(CreateSupportedSource());
        _ = ReadLedger(root);
        var decision = root["decisions"]![0]!.AsObject();
        switch (change)
        {
            case "instance": decision["instanceId"] = "spiritual_instance_" + new string('d', 64); break;
            case "request": decision["binding"]!["requestId"] = "foreign_request"; break;
            case "source": decision["sourceId"] = "spiritual_source_" + new string('d', 64); break;
            case "ordinal": decision["binding"]!["sourceOrdinal"] = 1; break;
            case "exchange":
                var duplicate = decision["sourceWitness"]!.AsObject();
                var actual = SpiritualExistingPressurePreimageTests.ReadActual(duplicate);
                var ordinary = SpiritualExistingPressurePreimageTests.ReadOrdinary(duplicate);
                duplicate["exchangeId"] = "exchange_foreign";
                actual["exchangeId"] = "exchange_foreign"; ordinary["exchangeId"] = "exchange_foreign";
                duplicate["exchangeJsonBase64"] = Encode(actual);
                duplicate["strainPreimage"]!["ordinaryExchangeJsonBase64"] = Encode(ordinary);
                SpiritualExistingPressurePreimageTests.Seal(duplicate);
                Assert.Equal(2, SpiritualWoundSourceWitness.Validate(duplicate).MaximumSeverityRank);
                break;
            default: throw new ArgumentOutOfRangeException(nameof(change));
        }
        decision["decisionFingerprint"] = SpiritualWoundStateJson.Hash(decision, "decision", "decisionFingerprint");
        // The primary source still passes intrinsically. The failure must be in its containing decision join.
        Assert.Equal(2, SpiritualWoundSourceWitness.Validate(root["sources"]![0]!["witness"]!.AsObject()).MaximumSeverityRank);
        var before = root.ToJsonString();
        var result = SpiritualWoundOpportunityReceiptState.Parse(before, "unit.pressureLedger");
        Assert.False(result.IsValid);
        Assert.Null(result.State);
        Assert.Contains(result.Issues, issue => issue.Code == "spiritual_wound_receipt_invalid_state");
        Assert.Equal(before, root.ToJsonString());
    }

    private static JsonObject CreateSupportedSource()
    {
        var source = SpiritualExistingPressurePreimageTests.CreateData();
        SpiritualExistingPressurePreimageTests.AssertFixturePrerequisites(source);
        Assert.Equal(2, SpiritualWoundSourceWitness.ValidateAndReadStrainData(source, out var data).MaximumSeverityRank);
        Assert.Equal(3, Assert.IsType<SpiritualStrainPreimageData>(data).Version);
        return source;
    }

    private static (JsonObject Root, JsonObject Parent) CreateLedger(JsonObject source) =>
        SpiritualExistingPressureTestData.CreateLedger(source);

    private static JsonObject CreateHistory(JsonObject parent) => SpiritualExistingPressureTestData.CreateHistory(parent);

    private static SpiritualWoundOpportunityReceiptState ReadLedger(JsonObject root)
    {
        var parsed = SpiritualWoundOpportunityReceiptState.Parse(root.ToJsonString(), "unit.pressureLedger");
        Assert.True(parsed.IsValid, string.Join("\n", parsed.Issues));
        return parsed.State!;
    }

    private static WoundHistoryState ReadHistory(JsonObject root)
    {
        var parsed = WoundHistoryState.Parse(root.ToJsonString(), WoundHistoryState.HistoryPath);
        Assert.True(parsed.IsValid, string.Join("\n", parsed.Issues));
        return parsed.State!;
    }

    private static string Encode(JsonNode value) =>
        Convert.ToBase64String(Encoding.UTF8.GetBytes(SpiritualWoundStateJson.Canonical(value)));
}
