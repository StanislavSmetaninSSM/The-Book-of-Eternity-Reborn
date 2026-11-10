using System.Text;
using System.Text.Json.Nodes;
using BookOfEternityClient.Services;
using Xunit;

namespace BookOfEternityClient.Tests;

public sealed partial class SpiritualGuardianGroupPressureReceiptTests
{
    /// <summary>After independently requiring Exact at both turns, missing, duplicated or replaced complete accepted rows must refuse.</summary>
    /// <param name="change">One full-row identity, mechanics, nested payload or declaration contradiction.</param>
    [Theory]
    [InlineData("missing")]
    [InlineData("duplicate")]
    [InlineData("replaced")]
    [InlineData("mechanics")]
    [InlineData("nested-summary")]
    [InlineData("declaration")]
    [InlineData("turn-marker")]
    public void GuardianGroupReceipt_AcceptedLogRequiresCompleteActual(string change)
    {
        var inputs = CreateInputs();
        var frame = CreateFrame(inputs);
        var active = ActiveConflict(inputs.Source);
        var receiptBefore = frame.Root.ToJsonString();
        foreach (var turn in new[] { 46, 47 })
            AssertHistoryMatch(frame.State, active, turn, SpiritualWoundOpportunityReceiptState.RetainedPressureHistoryMatch.Exact);
        var ordinary = frame.State.ReadRetainedStrainOrdinaryAfter("chaos_sea", Text(inputs.Source["conflictId"]),
            inputs.Fixture.Actual, false);
        Log("accepted_log_exact_ordinary_lookup", new { ordinary, expected = inputs.Fixture.Ordinary["after"], active, receipt = frame.Root });
        Assert.True(JsonNode.DeepEquals(inputs.Fixture.Ordinary["after"], ordinary));
        var log = active["exchangeLog"]!.AsArray();
        var actual = log[0]!.AsObject();
        switch (change)
        {
            case "missing": log.Clear(); break;
            case "duplicate": log.Add(actual.DeepClone()); break;
            case "replaced": actual["exchangeId"] = "exchange_guardian_group_foreign"; break;
            case "mechanics": actual["after"]!["oppositionSideStrain"] = "overwhelmed"; break;
            case "nested-summary": actual["incomingAction"]!["summary"] = "Иное описание внутри принятого действия."; break;
            case "declaration": Assert.True(actual.Remove(SpiritualWoundFailedAction.Property)); break;
            case "turn-marker": actual["exchangeAtTurn"] = 45; break;
            default: throw new ArgumentOutOfRangeException(nameof(change));
        }
        foreach (var turn in new[] { 46, 47 })
            AssertHistoryMatch(frame.State, active, turn, SpiritualWoundOpportunityReceiptState.RetainedPressureHistoryMatch.Mismatch);
        if (change is not ("missing" or "duplicate"))
        {
            var changedOrdinary = frame.State.ReadRetainedStrainOrdinaryAfter("chaos_sea", Text(inputs.Source["conflictId"]), actual, true);
            Log("accepted_log_changed_ordinary_lookup", new { change, actual, changedOrdinary });
            Assert.Null(changedOrdinary);
        }
        Assert.Equal(receiptBefore, frame.Root.ToJsonString());
    }

    /// <summary>Two complete valid v7 comparisons in one open instance, not two parents in one source, bind retained source order.</summary>
    /// <param name="change">A pure position swap, missing first comparison or duplicated second comparison.</param>
    [Theory]
    [InlineData("swap-positions")]
    [InlineData("missing-first")]
    [InlineData("duplicate-second")]
    public void GuardianGroupReceipt_AcceptedLogRequiresEveryComparisonInOrder(string change)
    {
        var (frame, _, active) = CreateTwoComparisonFrame();
        AssertHistoryMatch(frame.State, active, 49, SpiritualWoundOpportunityReceiptState.RetainedPressureHistoryMatch.Exact);
        var log = active["exchangeLog"]!.AsArray();
        switch (change)
        {
            case "swap-positions":
                var first = log[0]!.DeepClone(); var second = log[1]!.DeepClone();
                log[0] = second; log[1] = first;
                break;
            case "missing-first": log.RemoveAt(0); break;
            case "duplicate-second": log.Add(log[1]!.DeepClone()); break;
            default: throw new ArgumentOutOfRangeException(nameof(change));
        }
        AssertHistoryMatch(frame.State, active, 49, SpiritualWoundOpportunityReceiptState.RetainedPressureHistoryMatch.Mismatch);
    }

    /// <summary>Relative retained order permits a complete unrelated exchange between two v7 rows while their raw input pointers remain zero.</summary>
    [Fact]
    public void GuardianGroupReceipt_AcceptedLogAllowsUnrelatedBetweenComparisons()
    {
        var (frame, second, active) = CreateTwoComparisonFrame();
        AssertHistoryMatch(frame.State, active, 49, SpiritualWoundOpportunityReceiptState.RetainedPressureHistoryMatch.Exact);
        var unrelated = SpiritualRetainedPressureGroupTestData.CreateExchange("exchange_guardian_group_unrelated_47", 47,
            "clear", "fractured");
        var unrelatedSource = SpiritualRetainedPressureGroupTestData.CreateSource(unrelated, "session_guardian_group",
            "request_guardian_group_unrelated_47", "snapshot_guardian_group_unrelated_47", Text(frame.Inputs.Source["conflictId"]),
            "sha256:" + new string('a', 64));
        SpiritualWoundCalculation? calculation = null;
        SpiritualStrainPreimageData? comparison = null;
        var error = Record.Exception(() => calculation = SpiritualWoundSourceWitness.ValidateAndReadStrainData(unrelatedSource, out comparison));
        Log("unrelated_complete_exchange", new { unrelated, unrelatedSource, calculation, error = error?.ToString() });
        Assert.Null(error); Assert.Null(comparison);
        Assert.Equal((13L, 0, 2, 1, 2), (calculation!.TraumaPressure, calculation.PreviousStrainRank,
            calculation.NewStrainRank, calculation.ExtraJumpSteps, calculation.MaximumSeverityRank));
        Assert.True(JsonNode.DeepEquals(unrelated, Decode(unrelatedSource["exchangeJsonBase64"])));
        var receiptBefore = frame.Root.ToJsonString(); var sourceBefore = frame.Inputs.Source.ToJsonString();
        var secondBefore = second.ToJsonString(); var unrelatedBefore = unrelatedSource.ToJsonString();
        active["exchangeLog"]!.AsArray().Insert(1, unrelated.DeepClone());
        AssertHistoryMatch(frame.State, active, 49, SpiritualWoundOpportunityReceiptState.RetainedPressureHistoryMatch.Exact);
        Assert.Equal("/activeConflict/exchangeLog/0", Text(frame.Inputs.Source["strainPreimage"]!["originalInput"]!["exchangePointer"]));
        Assert.Equal("/activeConflict/exchangeLog/0", Text(second["strainPreimage"]!["originalInput"]!["exchangePointer"]));
        Assert.Equal(2, active["exchangeLog"]!.AsArray().ToList().FindIndex(row => Text(row!["exchangeId"]) == Text(second["exchangeId"])));
        Assert.Equal(receiptBefore, frame.Root.ToJsonString()); Assert.Equal(sourceBefore, frame.Inputs.Source.ToJsonString());
        Assert.Equal(secondBefore, second.ToJsonString()); Assert.Equal(unrelatedBefore, unrelatedSource.ToJsonString());
    }

    /// <summary>Authors a distinct later request's full capped comparison, raw root and original snapshot digest, then parses both rows in one open instance.</summary>
    /// <returns>Private invocation-local receipt, complete second source and minimal accepted-log matcher DATA.</returns>
    private (Frame Frame, JsonObject Second, JsonObject Active) CreateTwoComparisonFrame()
    {
        var inputs = CreateInputs();
        var first = CreateFrame(inputs);
        var ordinary = SpiritualGuardianAttributionTestData.CreateCappedExchange("exchange_guardian_group_48", 48);
        var ordinaryRoot = inputs.Fixture.OrdinaryRoot.DeepClone().AsObject();
        ordinaryRoot["activeConflict"]!["exchangeLog"] = new JsonArray(ordinary.DeepClone());
        ordinaryRoot["activeConflict"]!["oppositionSideStrain"] = "broken";
        var raw = Encoding.UTF8.GetPreamble().Concat(Encoding.UTF8.GetBytes(" \n" + ordinaryRoot.ToJsonString() + "\n")).ToArray();
        var snapshot = JsonNode.Parse(inputs.Fixture.OriginalSnapshotJson)!.AsObject();
        snapshot[SpiritualWoundOpportunityReceiptState.StatePath] = first.Root.DeepClone();
        var snapshotFingerprint = new CanonicalBeforeImage(true, Encoding.UTF8.GetBytes(SpiritualWoundStateJson.Canonical(snapshot))).Fingerprint;
        var second = SpiritualGuardianAttributionTestData.CreateSource(ordinary, "session_guardian_group",
            "request_guardian_group_48", "snapshot_guardian_group_48", Text(inputs.Source["conflictId"]), snapshotFingerprint);
        SpiritualWoundCalculation? plainCalculation = null;
        SpiritualStrainPreimageData? plainComparison = null;
        var plainError = Record.Exception(() => plainCalculation = SpiritualWoundSourceWitness.ValidateAndReadStrainData(second, out plainComparison));
        Log("second_request_comparison_free_premise", new { snapshot, snapshotFingerprint, ordinaryRoot,
            rawOrdinaryBytesBase64 = Convert.ToBase64String(raw), second, plainCalculation, error = plainError?.ToString() });
        Assert.Null(plainError); Assert.Null(plainComparison); Assert.Equal(ActualCalculation(), plainCalculation);
        var input = new JsonObject { ["path"] = AfterlifeSpiritualConflictState.StatePath, ["existed"] = true,
            ["contentFingerprint"] = new CanonicalBeforeImage(true, raw).Fingerprint, ["exchangePointer"] = "/activeConflict/exchangeLog/0" };
        second["strainPreimage"] = SpiritualGuardianGroupPressureProofData.CreateProof(inputs.Fixture.Retained.Parents,
            ordinary, input, ActualCalculation());
        SpiritualExistingPressureTestData.Seal(second);
        var secondData = ReadSupportedSource(second, inputs.Fixture.Retained.Parents, ActualCalculation());
        Assert.Equal(new CanonicalBeforeImage(true, raw).Fingerprint, secondData.OriginalInputFingerprint);
        Assert.True(JsonNode.DeepEquals(ordinary, ordinaryRoot["activeConflict"]!["exchangeLog"]![0]));
        Assert.True(JsonNode.DeepEquals(ordinaryRoot, SpiritualWoundStateJson.Parse(SpiritualWoundStateJson.DecodeUtf8JsonText(raw))));
        Assert.NotEqual(Text(inputs.Source["sourceId"]), Text(second["sourceId"]));
        Assert.NotEqual(Text(inputs.Source["exchangeId"]), Text(second["exchangeId"]));
        foreach (var field in new[] { "requestId", "snapshotToken", "originalSnapshotFingerprint" })
            Assert.NotEqual(Text(inputs.Source["turnEvidence"]![field]), Text(second["turnEvidence"]![field]));
        Assert.Equal(48, second["turnEvidence"]!["turn"]!.GetValue<int>());
        var root = first.Root.DeepClone().AsObject();
        AppendSource(root, second, first.InstanceId);
        var state = ReadReceipt(root, "two_distinct_v7_comparisons_one_open_instance");
        Assert.Single(root["instances"]!.AsArray()); Assert.Empty(root["closures"]!.AsArray());
        Assert.Equal(2, root["sources"]!.AsArray().Count);
        Assert.Equal(2, root["decisions"]!.AsArray().Count);
        Assert.All(root["sources"]!.AsArray(), row => Assert.Equal(first.InstanceId, Text(row!["instanceId"])));
        var active = ActiveConflict(inputs.Source);
        active["exchangeLog"]!.AsArray().Add(Decode(second["exchangeJsonBase64"]));
        Log("two_comparison_inputs", new { root, active, first = inputs.Source, second, secondOrdinary = ordinary,
            scope = "Two independently validated request contexts and DATA rows; no live whole-conflict chronology or publication authority." });
        return (new(inputs, root, state, first.InstanceId), second, active);
    }

    /// <summary>Creates minimal detached active-log DATA for the typed full-row matcher, without a live conflict claim.</summary>
    /// <param name="source">Complete checked source whose exact actual row starts the log.</param>
    /// <returns>Private mutable conflict-id/log DATA.</returns>
    private static JsonObject ActiveConflict(JsonObject source) => new()
    {
        ["conflictId"] = source["conflictId"]!.DeepClone(), ["exchangeLog"] = new JsonArray(Decode(source["exchangeJsonBase64"]))
    };

    /// <summary>Logs the direct full-row matcher observation and any exception before asserting the selected desired result.</summary>
    /// <param name="state">Containing parsed receipt premise, with no presence-property assertion masking this matcher.</param>
    /// <param name="active">Complete matcher input, including every retained and unrelated row.</param>
    /// <param name="turn">Explicit signed-turn DATA controlling top-summary drift.</param>
    /// <param name="expected">Unconditional Exact baseline or strict Mismatch mutation oracle.</param>
    private void AssertHistoryMatch(SpiritualWoundOpportunityReceiptState state, JsonObject active, int turn,
        SpiritualWoundOpportunityReceiptState.RetainedPressureHistoryMatch expected)
    {
        var before = active.ToJsonString();
        SpiritualWoundOpportunityReceiptState.RetainedPressureHistoryMatch? actual = null;
        var error = Record.Exception(() => actual = state.MatchRetainedPressureHistory("chaos_sea", active, turn));
        Log("direct_accepted_log_match", new { turn, expected, actual, error = error?.ToString(), activeJson = before,
            receiptJson = SpiritualWoundOpportunityReceiptState.SerializeCanonical(state) });
        Assert.Null(error); Assert.Equal(expected, actual); Assert.Equal(before, active.ToJsonString());
    }
}
