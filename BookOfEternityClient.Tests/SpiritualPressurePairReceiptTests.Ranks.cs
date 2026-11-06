using System.Text.Json.Nodes;
using BookOfEternityClient.Services;
using Xunit;

namespace BookOfEternityClient.Tests;

public sealed partial class SpiritualPressurePairReceiptTests
{
    /// <summary>Each newly admitted complete rank configuration retains one comparison/source and both exact historical anchors.</summary>
    [Theory]
    [InlineData(3, 4, "fractured")]
    [InlineData(3, 4, "overwhelmed")]
    [InlineData(4, 3, "fractured")]
    [InlineData(4, 3, "overwhelmed")]
    [InlineData(4, 4, "fractured")]
    [InlineData(4, 4, "overwhelmed")]
    public void PairRankReceiptRetainsOneComparisonAndBothExactAnchors(int first, int second, string ordinary)
    {
        var source = SpiritualPressurePairTestData.CreateSource(ordinary, new(first, second));
        AssertSupported(source);
        var root = CreateLedger(source);
        var historyRoot = CreateHistory(source);
        var before = root.ToJsonString(); var historyBefore = historyRoot.ToJsonString();
        var state = ReadLedger(root); var history = ReadHistory(historyRoot);
        Assert.Single(root["sources"]!.AsArray());
        Assert.Single(root["decisions"]!.AsArray());
        Assert.Equal(2, historyRoot["transitions"]!.AsArray().Count);
        Assert.True(state.HasRetainedPressureComparisons);
        Assert.True(state.HasExactStrainHistory(history));
        for (var index = 0; index < 2; index++)
        {
            var parent = Decode(source["strainPreimage"]!["parents"]![index]!["parentWoundJsonBase64"]);
            var parsed = WoundMaterializationContract.Parse(parent.ToJsonString(), "unit.pair.rankReceipt");
            Assert.True(parsed.IsValid, string.Join("\n", parsed.Issues));
            Assert.Equal(index == 0 ? first : second, parsed.Wound!.Severity.Rank);
            var anchor = Assert.Single(history.ResolveWoundTransitions(parsed.Wound.WoundId));
            Assert.Equal((parsed.Wound.LastTransition.TransitionId, parsed.Wound.LastTransition.Ordinal,
                parsed.Wound.LastTransition.Turn, WoundIdentityState.ComputeSemanticFingerprint(parsed.Wound)),
                (anchor.TransitionId, anchor.WoundTransitionOrdinal, anchor.Turn, anchor.AfterFingerprint));
        }
        var actual = ReadActual(source);
        var retained = state.ReadRetainedStrainOrdinaryAfter("chaos_sea", "conflict-a", actual, false);
        Assert.True(JsonNode.DeepEquals(ReadOrdinary(source)["after"], retained));
        Assert.Equal(ordinary, retained!["playerSideStrain"]!.GetValue<string>());
        AssertHistoryMatch(state, ActiveConflict(source), 47, SpiritualWoundOpportunityReceiptState.RetainedPressureHistoryMatch.Exact);
        Assert.Equal(before, root.ToJsonString()); Assert.Equal(historyBefore, historyRoot.ToJsonString());
    }

    /// <summary>A lawful IV envelope cannot bypass exact containing source/decision joins or either recorded historical anchor.</summary>
    [Theory]
    [InlineData("missing-first")]
    [InlineData("missing-second")]
    [InlineData("stale-first")]
    [InlineData("stale-second")]
    [InlineData("resealed-IV-envelope")]
    [InlineData("source-parent-disagreement")]
    [InlineData("decision-parent-disagreement")]
    [InlineData("duplicate-comparison")]
    public void PairRankReceiptRejectsBrokenJoinsWithoutPartialData(string change)
    {
        var source = SpiritualPressurePairTestData.CreateSource(ranks: new(3, 4));
        AssertSupported(source);
        var root = CreateLedger(source); var historyRoot = CreateHistory(source);
        var baseline = ReadLedger(root);
        Assert.True(baseline.HasExactStrainHistory(ReadHistory(historyRoot)));
        switch (change)
        {
            case "missing-first": historyRoot["transitions"]!.AsArray().RemoveAt(0); RenumberHistory(historyRoot); break;
            case "missing-second": historyRoot["transitions"]!.AsArray().RemoveAt(1); RenumberHistory(historyRoot); break;
            case "stale-first": historyRoot["transitions"]![0]!["afterFingerprint"] = "sha256:" + new string('d', 64); break;
            case "stale-second": historyRoot["transitions"]![1]!["afterFingerprint"] = "sha256:" + new string('d', 64); break;
            case "resealed-IV-envelope":
                ChangeSecondParentDescription(source); AssertSupported(source); root = CreateLedger(source);
                break;
            case "source-parent-disagreement":
                ChangeSecondParentDescription(root["sources"]![0]!["witness"]!.AsObject());
                AssertSupported(root["sources"]![0]!["witness"]!.AsObject());
                break;
            case "decision-parent-disagreement":
                var decision = root["decisions"]![0]!.AsObject();
                ChangeSecondParentDescription(decision["sourceWitness"]!.AsObject());
                AssertSupported(decision["sourceWitness"]!.AsObject()); SealDecision(decision);
                break;
            case "duplicate-comparison":
                var duplicate = source.DeepClone().AsObject();
                duplicate["turnEvidence"]!["requestId"] = "request_rank_pair_duplicate";
                duplicate["turnEvidence"]!["snapshotToken"] = "snapshot_rank_pair_duplicate";
                Seal(duplicate); AssertSupported(duplicate); AppendSource(root, duplicate);
                break;
            default: throw new ArgumentOutOfRangeException(nameof(change));
        }
        if (change is "source-parent-disagreement" or "decision-parent-disagreement" or "duplicate-comparison")
        {
            AssertInvalidLedger(root);
            return;
        }
        var before = root.ToJsonString(); var historyBefore = historyRoot.ToJsonString();
        Assert.False(ReadLedger(root).HasExactStrainHistory(ReadHistory(historyRoot)));
        Assert.Equal(before, root.ToJsonString()); Assert.Equal(historyBefore, historyRoot.ToJsonString());
    }
}
