using System.Text;
using System.Text.Json.Nodes;
using BookOfEternityClient.Services;
using Xunit;

namespace BookOfEternityClient.Tests;

/// <summary>Checks comparison-only receipt DATA; signed originals, current owners and publication remain independent.</summary>
public sealed partial class SpiritualZeroStrainReceiptTests
{
    /// <summary>Retains a real intrinsic zero ceiling without inventing a wound decision, with empty or existing injury history.</summary>
    [Theory]
    [InlineData("zero-only")]
    [InlineData("prior-injury")]
    [InlineData("mixed-request")]
    public void ZeroComparisonExtendsExistingLedgerWithoutDecision(string mode)
    {
        var (before, next) = CreateLedger(mode);
        var baseline = SpiritualWoundOpportunityReceiptState.Parse(before.ToJsonString(), "unit.zeroBefore");
        Assert.True(baseline.IsValid, string.Join("\n", baseline.Issues));
        var comparison = next["strainComparisons"]![0]!["witness"]!.AsObject();
        var calculation = SpiritualWoundSourceWitness.Validate(comparison);
        Assert.True(calculation.HasIncreasingStrain);
        Assert.Equal(0, calculation.MaximumSeverityRank);
        Assert.Equal(7, calculation.TraumaPressure);
        var input = next.ToJsonString();
        var parsed = SpiritualWoundOpportunityReceiptState.Parse(input, "unit.zeroAfter");
        Assert.True(parsed.IsValid, string.Join("\n", parsed.Issues));
        Assert.Equal(input, next.ToJsonString());
        Assert.Equal(before["decisions"]!.AsArray().Count + (mode == "mixed-request" ? 1 : 0),
            next["decisions"]!.AsArray().Count);
        var append = SpiritualWoundOpportunityReceiptState.PlanAppend(baseline.State!, parsed.State!);
        Assert.Equal("appended", append.Disposition);
        Assert.Equal("exact_replay", SpiritualWoundOpportunityReceiptState.PlanAppend(parsed.State!, parsed.State!).Disposition);
    }

    /// <summary>Requires a valid v3 baseline before testing rehashed cross-family or closed-shape contradictions.</summary>
    [Theory]
    [InlineData("extra-row-field")]
    [InlineData("positive-ceiling")]
    [InlineData("comparison-ordinal")]
    [InlineData("instance-ordinal")]
    [InlineData("foreign-instance")]
    [InlineData("duplicate-row")]
    [InlineData("injury-coordinate")]
    [InlineData("original-context")]
    [InlineData("claimed-pool")]
    public void ZeroComparisonRefusesCrossFamilyContradiction(string change)
    {
        var (_, root) = CreateLedger(change == "positive-ceiling" ? "prior-injury" : "mixed-request");
        Assert.True(SpiritualWoundOpportunityReceiptState.Parse(root.ToJsonString(), "unit.zeroBaseline").IsValid);
        var row = root["strainComparisons"]![0]!.AsObject();
        var witness = row["witness"]!.AsObject();
        switch (change)
        {
            case "extra-row-field": row["unowned"] = true; break;
            case "positive-ceiling":
                var (positive, _, _) = SpiritualExistingStrainPreimageTests.CreateData(3);
                Assert.Equal(2, SpiritualWoundSourceWitness.Validate(positive).MaximumSeverityRank);
                row["witness"] = positive; break;
            case "comparison-ordinal": row["ordinal"] = 2; break;
            case "instance-ordinal": row["instanceComparisonOrdinal"] = 2; break;
            case "foreign-instance": row["instanceId"] = "foreign_instance"; break;
            case "duplicate-row": root["strainComparisons"]!.AsArray().Add(row.DeepClone()); root["nextStrainComparisonOrdinal"] = 3; break;
            case "injury-coordinate":
                var injury = root["sources"]![1]!["witness"]!;
                witness["exchangeId"] = injury["exchangeId"]!.DeepClone();
                var actual = Decode(witness["exchangeJsonBase64"]!); actual["exchangeId"] = witness["exchangeId"]!.DeepClone();
                witness["exchangeJsonBase64"] = Encode(actual);
                var ordinary = Decode(witness["strainPreimage"]!["ordinaryExchangeJsonBase64"]!);
                ordinary["exchangeId"] = witness["exchangeId"]!.DeepClone();
                witness["strainPreimage"]!["ordinaryExchangeJsonBase64"] = Encode(ordinary); Seal(witness); break;
            case "original-context": witness["turnEvidence"]!["originalSnapshotFingerprint"] = "sha256:" + new string('c', 64); Seal(witness); break;
            case "claimed-pool":
                var evidence = witness["turnEvidence"]!;
                evidence["acceptedD20Values"] = new JsonArray(4, 5, 4, 5);
                SetClaims(witness, 0, 1);
                var reused = Decode(witness["exchangeJsonBase64"]!); SetDice(reused, 0, 1);
                witness["exchangeJsonBase64"] = Encode(reused);
                var comparison = Decode(witness["strainPreimage"]!["ordinaryExchangeJsonBase64"]!); SetDice(comparison, 0, 1);
                witness["strainPreimage"]!["ordinaryExchangeJsonBase64"] = Encode(comparison); Seal(witness); break;
        }
        var bytes = root.ToJsonString();
        Assert.False(SpiritualWoundOpportunityReceiptState.Parse(bytes, "unit.zeroChanged").IsValid);
        Assert.Equal(bytes, root.ToJsonString());
    }

    /// <summary>A request accepted through comparison-only DATA cannot later acquire an injury decision.</summary>
    [Fact]
    public void ZeroComparisonAppendCannotReopenAcceptedRequest()
    {
        var (_, complete) = CreateLedger("mixed-request");
        var comparisonOnly = complete.DeepClone().AsObject();
        comparisonOnly["sources"]!.AsArray().RemoveAt(1); comparisonOnly["decisions"]!.AsArray().RemoveAt(1);
        comparisonOnly["nextSourceOrdinal"] = 2; comparisonOnly["nextDecisionOrdinal"] = 2;
        var before = ParseLedger(comparisonOnly); var next = ParseLedger(complete);
        Assert.Equal("conflict", SpiritualWoundOpportunityReceiptState.PlanAppend(before, next).Disposition);
    }

    /// <summary>Empty v3 healing is semantically absent; v2's nonempty family rule is retained.</summary>
    [Fact]
    public void ZeroComparisonEmptyHealingDoesNotBecomeCombatHealing()
    {
        var (_, root) = CreateLedger("prior-injury");
        Assert.False(ParseLedger(root).HasCombatHealing);
        var v2 = root.DeepClone().AsObject(); v2["schemaVersion"] = 2;
        v2.Remove("strainComparisons"); v2.Remove("nextStrainComparisonOrdinal");
        Assert.False(SpiritualWoundOpportunityReceiptState.Parse(v2.ToJsonString(), "unit.emptyV2").IsValid);
        root["nextHealingSourceOrdinal"] = 2;
        Assert.False(SpiritualWoundOpportunityReceiptState.Parse(root.ToJsonString(), "unit.badEmptyV3").IsValid);
    }

    /// <summary>Retained zero comparison joins its exact creation anchor even after later lawful terminal History DATA.</summary>
    [Fact]
    public void ZeroComparisonHistorySurvivesLaterHealingWithoutLivePermission()
    {
        var (_, root) = CreateLedger("prior-injury"); var ledger = ParseLedger(root);
        var history = SpiritualStrainHistoryDataTests.CreateHistory(SpiritualStrainReceiptJoinTests.CreateLedger());
        Assert.True(ledger.HasExactStrainHistory(ParseHistory(history)));
        var first = history["transitions"]![0]!.AsObject(); var heal = first.DeepClone().AsObject();
        heal["transitionId"] = "zero_later_heal"; heal["operationKey"] = "zero_later_heal_operation";
        heal["ordinal"] = 2; heal["woundTransitionOrdinal"] = 2; heal["kind"] = "heal"; heal["turn"] = 44;
        heal["eventRef"] = "zero_later_heal_event"; heal["beforeFingerprint"] = first["afterFingerprint"]!.DeepClone();
        heal["afterFingerprint"] = "sha256:" + new string('d', 64); heal["terminal"] = true;
        history["transitions"]!.AsArray().Add(heal); history["nextOrdinal"] = 3;
        Assert.True(ledger.HasExactStrainHistory(ParseHistory(history)));
        var actual = Decode(root["strainComparisons"]![0]!["witness"]!["exchangeJsonBase64"]!);
        Assert.NotNull(ledger.ReadRetainedStrainOrdinaryAfter("chaos_sea", "conflict-a", actual, false));
        first["afterFingerprint"] = "sha256:" + new string('c', 64); heal["beforeFingerprint"] = first["afterFingerprint"]!.DeepClone();
        Assert.False(ledger.HasExactStrainHistory(ParseHistory(history)));
    }

    /// <summary>Valid DATA and a receipt identity cannot construct the private exhausted source owner.</summary>
    [Fact]
    public void ZeroComparisonDataCannotForgeSourceOnlyOwner()
    {
        var witness = CreateZeroWitness(); Assert.Equal(0, SpiritualWoundSourceWitness.Validate(witness).MaximumSeverityRank);
        var signed = new ValidationService.SpiritualSignedC1Origin(new CanonicalBeforeImage(false, null),
            new CanonicalBeforeImage(true, Encoding.UTF8.GetBytes("{\"schemaVersion\":1,\"activeConflict\":{\"conflictId\":\"conflict-a\"}}")),
            "sha256:" + new string('a', 64));
        var owner = new ValidationService.SpiritualOriginalTurnCapture.AuthenticatedC3SourceOnly(new object(), signed,
            null!, "chaos_sea", null, new object(), [witness]);
        witness["maximumSeverityRank"] = 4;
        Assert.Equal(0, owner.ReadStrainComparisons()[0]["maximumSeverityRank"]!.GetValue<int>());
        var escaped = owner.ReadStrainComparisons()[0]; escaped["maximumSeverityRank"] = 3;
        Assert.Equal(0, owner.ReadStrainComparisons()[0]["maximumSeverityRank"]!.GetValue<int>());
        Assert.False(owner.IsAuthentic);
        Assert.Contains(SpiritualWoundDeclineReceiptReducer.ReduceSourceOnly(owner).Issues,
            issue => issue.Code == "spiritual_c3_source_only_owner_missing");
    }

    /// <summary>Request histories must preserve both tables' order; a numeric sort must not hide a cross-family cycle.</summary>
    [Fact]
    public void ZeroComparisonCannotSortAwayContradictoryRequestOrder()
    {
        var (_, root) = CreateLedger("mixed-request");
        var sourceRow = root["sources"]![1]!.DeepClone().AsObject();
        sourceRow["ordinal"] = 3; sourceRow["instanceSourceOrdinal"] = 3;
        RetupleWitness(sourceRow["witness"]!.AsObject(), "b"); root["sources"]!.AsArray().Add(sourceRow);
        var decision = root["decisions"]![1]!.DeepClone().AsObject(); decision["ordinal"] = 3;
        decision["decisionId"] = "decision_current_positive_b"; decision["opportunityRef"] = "opportunity_current_positive_b";
        decision["binding"]!["requestId"] = "request-existing43_b"; decision["binding"]!["snapshotToken"] = "snapshot-existing43_b";
        root["decisions"]!.AsArray().Add(decision); SpiritualStrainReceiptJoinTests.SealSourceAndDecision(root, 2);
        root["nextSourceOrdinal"] = 4; root["nextDecisionOrdinal"] = 4;
        var comparison = root["strainComparisons"]![0]!.DeepClone().AsObject();
        comparison["ordinal"] = 2; comparison["instanceComparisonOrdinal"] = 2;
        RetupleWitness(comparison["witness"]!.AsObject(), "b"); root["strainComparisons"]!.AsArray().Add(comparison);
        root["nextStrainComparisonOrdinal"] = 3;
        _ = ParseLedger(root);
        var rows = root["strainComparisons"]!.AsArray();
        var first = rows[0]!.DeepClone(); var second = rows[1]!.DeepClone(); rows.Clear();
        second["ordinal"] = 1; second["instanceComparisonOrdinal"] = 1;
        first["ordinal"] = 2; first["instanceComparisonOrdinal"] = 2; rows.Add(second); rows.Add(first);
        Assert.False(SpiritualWoundOpportunityReceiptState.Parse(root.ToJsonString(), "unit.requestCycle").IsValid);
    }

    /// <summary>Adding a zero table must not let the merge repair reversed injury sides from one original exchange.</summary>
    [Fact]
    public void ZeroComparisonCannotSortAwayOriginalInjurySideOrder()
    {
        var (_, root) = CreateLedger("prior-injury");
        var twoSides = SpiritualWoundOpportunityReceiptStateTests.TwoSides();
        _ = ParseLedger(twoSides);
        for (var index = 0; index < 2; index++)
        {
            var source = twoSides["sources"]![index]!.DeepClone().AsObject();
            source["ordinal"] = index + 2; source["instanceSourceOrdinal"] = index + 2;
            source["instanceId"] = root["instances"]![0]!["instanceId"]!.DeepClone();
            var decision = twoSides["decisions"]![index]!.DeepClone().AsObject();
            decision["ordinal"] = index + 2; decision["instanceId"] = source["instanceId"]!.DeepClone();
            root["sources"]!.AsArray().Add(source); root["decisions"]!.AsArray().Add(decision);
            SpiritualStrainReceiptJoinTests.SealSourceAndDecision(root, index + 1);
        }
        root["nextSourceOrdinal"] = 4; root["nextDecisionOrdinal"] = 4;
        _ = ParseLedger(root);
        foreach (var table in new[] { "sources", "decisions" })
        {
            var rows = root[table]!.AsArray();
            var player = rows[1]!.DeepClone(); var opposition = rows[2]!.DeepClone();
            rows[1] = opposition; rows[2] = player;
            for (var index = 1; index < 3; index++)
            {
                rows[index]!["ordinal"] = index + 1;
                if (table == "sources") rows[index]!["instanceSourceOrdinal"] = index + 1;
            }
        }
        SpiritualStrainReceiptJoinTests.SealSourceAndDecision(root, 1);
        SpiritualStrainReceiptJoinTests.SealSourceAndDecision(root, 2);
        var bytes = root.ToJsonString();
        var causal = Assert.Throws<FormatException>(() => SpiritualWoundOpportunityReceiptState.ValidateTurnCoherence(
            root["sources"]!.AsArray().Select(row => row!.AsObject())));
        Assert.Contains("Source positions exceed available causal side slots.", causal.Message, StringComparison.Ordinal);
        var refused = SpiritualWoundOpportunityReceiptState.Parse(bytes, "unit.reversedOriginalSides");
        Assert.False(refused.IsValid);
        Assert.Contains(refused.Issues, issue => issue.Code == "spiritual_wound_receipt_invalid_state");
        Assert.Equal(bytes, root.ToJsonString());
    }

    private static void RetupleWitness(JsonObject witness, string suffix)
    {
        witness["turnEvidence"]!["requestId"] = "request-existing43_" + suffix;
        witness["turnEvidence"]!["snapshotToken"] = "snapshot-existing43_" + suffix;
        var id = witness["exchangeId"]!.GetValue<string>() + "_" + suffix; witness["exchangeId"] = id;
        var actual = Decode(witness["exchangeJsonBase64"]!); actual["exchangeId"] = id;
        witness["exchangeJsonBase64"] = Encode(actual);
        if (witness["strainPreimage"] is JsonObject proof)
        { var ordinary = Decode(proof["ordinaryExchangeJsonBase64"]!); ordinary["exchangeId"] = id; proof["ordinaryExchangeJsonBase64"] = Encode(ordinary); }
        Seal(witness); _ = SpiritualWoundSourceWitness.Validate(witness);
    }
    private static SpiritualWoundOpportunityReceiptState ParseLedger(JsonObject root)
    {
        var result = SpiritualWoundOpportunityReceiptState.Parse(root.ToJsonString(), "unit.zeroLedger");
        Assert.True(result.IsValid, string.Join("\n", result.Issues)); return result.State!;
    }
    private static WoundHistoryState ParseHistory(JsonObject root)
    {
        var result = WoundHistoryState.Parse(root.ToJsonString(), WoundHistoryState.HistoryPath);
        Assert.True(result.IsValid, string.Join("\n", result.Issues)); return result.State!;
    }

    internal static (JsonObject Before, JsonObject Next) CreateLedger(string mode)
    {
        var before = SpiritualStrainReceiptJoinTests.CreateLedger();
        before["sources"]!.AsArray().RemoveAt(1); before["decisions"]!.AsArray().RemoveAt(1);
        before["nextSourceOrdinal"] = 2; before["nextDecisionOrdinal"] = 2;
        if (mode == "zero-only")
        {
            before["sources"]!.AsArray().Clear(); before["decisions"]!.AsArray().Clear();
            before["nextSourceOrdinal"] = 1; before["nextDecisionOrdinal"] = 1;
        }
        var originalBefore = before.DeepClone().AsObject();
        var zero = CreateZeroWitness();
        if (mode == "mixed-request")
        {
            var (_, positive, _, _) = SpiritualStrainPreimageDataTests.CreateData();
            var actual = Decode(positive["exchangeJsonBase64"]!);
            actual["exchangeId"] = "exchange_current_positive43"; actual["turnNumber"] = 43;
            positive["exchangeId"] = "exchange_current_positive43";
            positive["exchangeJsonBase64"] = Encode(actual);
            positive["turnEvidence"]!["requestId"] = "request-existing43";
            positive["turnEvidence"]!["snapshotToken"] = "snapshot-existing43";
            positive["turnEvidence"]!["turn"] = 43;
            positive["turnEvidence"]!["acceptedD20Values"] = new JsonArray(5, 15, 4, 5);
            Seal(positive);
            var old = before["decisions"]![0]!.DeepClone();
            old["ordinal"] = 2; old["decisionId"] = "decision_current_positive43";
            old["opportunityRef"] = "opportunity_current_positive43";
            old["decision"] = "none"; old["selectedSeverityRank"] = null; old["woundId"] = null; old["transitionId"] = null;
            old["binding"] = new JsonObject { ["sessionId"] = "session-a", ["requestId"] = "request-existing43",
                ["snapshotToken"] = "snapshot-existing43", ["turn"] = 43,
                ["continuationGeneration"] = 1, ["waveOrdinal"] = 0, ["sourceOrdinal"] = 0 };
            before["sources"]!.AsArray().Add(new JsonObject { ["ordinal"] = 2,
                ["instanceId"] = before["instances"]![0]!["instanceId"]!.DeepClone(), ["instanceSourceOrdinal"] = 2,
                ["witness"] = positive });
            before["decisions"]!.AsArray().Add(old); before["nextSourceOrdinal"] = 3; before["nextDecisionOrdinal"] = 3;
            SpiritualStrainReceiptJoinTests.SealSourceAndDecision(before, 1);
            zero["coordinate"] = "activeConflict.exchangeLog[1].player"; zero["sourceOrdinal"] = 1; zero["exchangeOrdinal"] = 1;
            zero["selectedDiceIndices"] = new JsonArray(2, 3);
            var evidence = zero["turnEvidence"]!;
            evidence["bounds"]!["exchangeCount"] = 2; evidence["bounds"]!["sourceSlotCount"] = 4; evidence["bounds"]!["diceCount"] = 4;
            evidence["acceptedD20Values"] = new JsonArray(5, 15, 4, 5);
            SetClaims(zero, 2, 3);
            var corrected = Decode(zero["exchangeJsonBase64"]!); SetDice(corrected, 2, 3);
            zero["exchangeJsonBase64"] = Encode(corrected);
            var ordinary = Decode(zero["strainPreimage"]!["ordinaryExchangeJsonBase64"]!); SetDice(ordinary, 2, 3);
            zero["strainPreimage"]!["ordinaryExchangeJsonBase64"] = Encode(ordinary); Seal(zero);
        }
        var next = before.DeepClone().AsObject(); next["schemaVersion"] = 3;
        next["nextHealingSourceOrdinal"] = 1; next["nextHealingDecisionOrdinal"] = 1;
        next["healingSources"] = new JsonArray(); next["healingDecisions"] = new JsonArray();
        next["nextStrainComparisonOrdinal"] = 2;
        next["strainComparisons"] = new JsonArray(new JsonObject { ["ordinal"] = 1,
            ["instanceId"] = next["instances"]![0]!["instanceId"]!.DeepClone(), ["instanceComparisonOrdinal"] = 1,
            ["witness"] = zero });
        return (mode == "mixed-request" ? originalBefore : before, next);
    }

    internal static JsonObject CreateZeroWitness()
    {
        var (source, _, ordinary) = SpiritualExistingStrainPreimageTests.CreateData(3);
        var actual = Decode(source["exchangeJsonBase64"]!);
        foreach (var exchange in new[] { actual, ordinary })
        {
            exchange["diceAudit"]!["diceUsed"]![1]!["value"] = 5;
            exchange["diceAudit"]!["oppositionTotal"] = 5; exchange["diceAudit"]!["margin"] = -1;
            exchange["diceAudit"]!["outcomeBand"] = "mixed_or_no_effect";
        }
        source["harmfulMargin"] = 1; source["maximumSeverityRank"] = 0;
        source["exchangeJsonBase64"] = Encode(actual);
        source["strainPreimage"]!["ordinaryExchangeJsonBase64"] = Encode(ordinary);
        source["turnEvidence"]!["acceptedD20Values"] = new JsonArray(4, 5);
        SetClaims(source, 0, 1); Seal(source);
        return source;
    }

    private static void SetClaims(JsonObject source, int playerIndex, int oppositionIndex)
    {
        var claims = new JsonArray();
        foreach (var (index, value) in new[] { (playerIndex, 4), (oppositionIndex, 5) })
        {
            var row = new JsonObject { ["sourceIndex"] = index, ["value"] = value,
                ["exchangeOrdinal"] = source["exchangeOrdinal"]!.DeepClone(), ["claimFingerprint"] = "" };
            row["claimFingerprint"] = SpiritualWoundTurnEvidence.ComputeClaimFingerprint(row); claims.Add(row);
        }
        source["turnEvidence"]!["diceClaims"] = claims;
    }
    private static void SetDice(JsonObject exchange, int playerIndex, int oppositionIndex)
    {
        exchange["diceAudit"]!["diceUsed"]![0]!["sourceIndex"] = playerIndex;
        exchange["diceAudit"]!["diceUsed"]![1]!["sourceIndex"] = oppositionIndex;
    }
    private static JsonObject Decode(JsonNode encoded) => JsonNode.Parse(Convert.FromBase64String(encoded.GetValue<string>()))!.AsObject();
    private static string Encode(JsonNode value) => Convert.ToBase64String(Encoding.UTF8.GetBytes(SpiritualWoundStateJson.Canonical(value)));
    private static void Seal(JsonObject source)
    {
        var hash = SpiritualWoundStateJson.Hash(source, "source", "sourceId", "sourceFingerprint");
        source["sourceFingerprint"] = hash; source["sourceId"] = "spiritual_source_" + hash[7..];
    }
}
