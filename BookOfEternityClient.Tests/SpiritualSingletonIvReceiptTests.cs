using System.Text.Json.Nodes;
using BookOfEternityClient.Services;
using Xunit;

namespace BookOfEternityClient.Tests;

/// <summary>Detached complete-IV player receipt/History/log joins, never original or current publication authority.</summary>
public sealed class SpiritualSingletonIvReceiptTests
{
    /// <summary>The valid v3 IV baseline precedes every exact History, complete parent and accepted-log refusal.</summary>
    /// <param name="change">One independently joined receipt/History/log contradiction, or the unchanged control.</param>
    [Theory]
    [InlineData("exact")] [InlineData("history-drift")] [InlineData("parent-drift")] [InlineData("log-drift")]
    public void PlayerSingletonIvReceipt_PreservesExactParentHistoryAndLog(string change)
    {
        var source = SpiritualExistingPressureTestData.CreateData(4);
        var calculation = SpiritualWoundSourceWitness.ValidateAndReadStrainData(source, out var comparison);
        Assert.Equal((15L, 2), (calculation.TraumaPressure, calculation.MaximumSeverityRank));
        Assert.Equal(4, Assert.IsType<SpiritualStrainPreimageData>(comparison).ParentWound.Severity.Rank);
        var (root, parent) = SpiritualExistingPressureTestData.CreateLedger(source);
        var history = SpiritualExistingPressureTestData.CreateHistory(parent);
        var state = ReadReceipt(root); var parsedHistory = ReadHistory(history);
        var actual = SpiritualExistingPressureTestData.ReadActual(source);
        var ordinary = SpiritualExistingPressureTestData.ReadOrdinary(source);
        var active = new JsonObject { ["conflictId"] = "conflict-a", ["exchangeLog"] = new JsonArray(actual.DeepClone()) };
        Assert.True(state.HasExactStrainHistory(parsedHistory));
        Assert.Equal(SpiritualWoundOpportunityReceiptState.RetainedPressureHistoryMatch.Exact,
            state.MatchRetainedPressureHistory("chaos_sea", active, 45));
        Assert.True(JsonNode.DeepEquals(ordinary["after"], state.ReadRetainedStrainOrdinaryAfter("chaos_sea", "conflict-a", actual, false)));
        switch (change)
        {
            case "exact": break;
            case "history-drift": history["transitions"]![0]!["afterFingerprint"] = "sha256:" + new string('d', 64); break;
            case "parent-drift":
                var retained = root["sources"]![0]!["witness"]!.AsObject();
                parent["display"]!["description"] = "Иное полное описание существующей IV раны.";
                var changed = WoundMaterializationContract.Parse(parent.ToJsonString(), "unit.singletonIV.changedParent");
                Assert.True(changed.IsValid, string.Join("\n", changed.Issues));
                retained["strainPreimage"]!["parentWoundJsonBase64"] = SpiritualGuardianAttributionTestData.Encode(parent);
                SpiritualStrainReceiptJoinTests.SealSourceAndDecision(root, 0);
                _ = SpiritualWoundSourceWitness.ValidateAndReadStrainData(retained, out var changedComparison);
                Assert.Equal(4, Assert.IsType<SpiritualStrainPreimageData>(changedComparison).ParentWound.Severity.Rank);
                break;
            case "log-drift": active["exchangeLog"]![0]!["after"]!["playerSideStrain"] = "fractured"; break;
            default: throw new ArgumentOutOfRangeException(nameof(change));
        }
        var receiptBefore = root.ToJsonString(); var historyBefore = history.ToJsonString(); var activeBefore = active.ToJsonString();
        state = ReadReceipt(root);
        Assert.Equal(change is not ("history-drift" or "parent-drift"), state.HasExactStrainHistory(ReadHistory(history)));
        Assert.Equal(change == "log-drift" ? SpiritualWoundOpportunityReceiptState.RetainedPressureHistoryMatch.Mismatch :
            SpiritualWoundOpportunityReceiptState.RetainedPressureHistoryMatch.Exact, state.MatchRetainedPressureHistory("chaos_sea", active, 45));
        var recovered = state.ReadRetainedStrainOrdinaryAfter("chaos_sea", "conflict-a", active["exchangeLog"]![0]!.AsObject(), false);
        if (change == "log-drift") Assert.Null(recovered);
        else Assert.True(JsonNode.DeepEquals(ordinary["after"], recovered));
        Assert.Equal(receiptBefore, root.ToJsonString()); Assert.Equal(historyBefore, history.ToJsonString()); Assert.Equal(activeBefore, active.ToJsonString());
    }

    private static SpiritualWoundOpportunityReceiptState ReadReceipt(JsonObject root)
    {
        var parsed = SpiritualWoundOpportunityReceiptState.Parse(root.ToJsonString(), "unit.singletonIV.receipt");
        Assert.True(parsed.IsValid, string.Join("\n", parsed.Issues)); return Assert.IsType<SpiritualWoundOpportunityReceiptState>(parsed.State);
    }

    private static WoundHistoryState ReadHistory(JsonObject root)
    {
        var parsed = WoundHistoryState.Parse(root.ToJsonString(), WoundHistoryState.HistoryPath);
        Assert.True(parsed.IsValid, string.Join("\n", parsed.Issues)); return Assert.IsType<WoundHistoryState>(parsed.State);
    }
}
