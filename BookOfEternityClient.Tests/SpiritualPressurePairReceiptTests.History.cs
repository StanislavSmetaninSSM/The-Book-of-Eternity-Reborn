using System.Text.Json.Nodes;
using BookOfEternityClient.Services;
using Xunit;

namespace BookOfEternityClient.Tests;

public sealed partial class SpiritualPressurePairReceiptTests
{
    /// <summary>Both recorded anchors remain valid historical DATA after lawful later healing retires either or both parents.</summary>
    [Theory]
    [InlineData("first")]
    [InlineData("second")]
    [InlineData("both")]
    public void PairHistoryPreservesBothRecordedAnchorsAfterLaterHealing(string healed)
    {
        var source = CreateSupportedSource();
        var ledger = ReadLedger(CreateLedger(source));
        var history = CreateHistory(source);
        Assert.True(ledger.HasExactStrainHistory(ReadHistory(history)));
        var anchors = history["transitions"]!.AsArray().OfType<JsonObject>().ToArray();
        foreach (var index in healed switch { "first" => new[] { 0 }, "second" => new[] { 1 }, _ => new[] { 0, 1 } })
        {
            var anchor = anchors[index];
            var heal = anchor.DeepClone().AsObject();
            heal["transitionId"] = "transition_pair_later_heal_" + index;
            heal["operationKey"] = "operation_pair_later_heal_" + index;
            heal["eventRef"] = "event_pair_later_heal_" + index;
            heal["woundTransitionOrdinal"] = 2; heal["kind"] = "heal"; heal["turn"] = 47 + index;
            heal["beforeFingerprint"] = anchor["afterFingerprint"]!.DeepClone();
            heal["afterFingerprint"] = "sha256:" + new string(index == 0 ? 'c' : 'd', 64);
            heal["terminal"] = true; history["transitions"]!.AsArray().Add(heal);
        }
        RenumberHistory(history);
        var before = history.ToJsonString();
        Assert.True(ledger.HasExactStrainHistory(ReadHistory(history)));
        Assert.Equal(before, history.ToJsonString());
    }

    /// <summary>Every changed second anchor is independently parseable History, with the first anchor left exact.</summary>
    [Theory]
    [InlineData("missing")]
    [InlineData("identity")]
    [InlineData("kind")]
    [InlineData("wound")]
    [InlineData("ordinal")]
    [InlineData("turn")]
    [InlineData("terminal")]
    [InlineData("fingerprint")]
    public void PairHistoryRequiresExactSecondRecordedAnchor(string change)
    {
        var source = CreateSupportedSource();
        SetSecondParentLaterAnchor(source);
        AssertSupported(source);
        var state = ReadLedger(CreateLedger(source));
        var history = CreateHistory(source);
        Assert.True(state.HasExactStrainHistory(ReadHistory(history)));
        var rows = history["transitions"]!.AsArray();
        var firstBefore = rows[0]!.ToJsonString();
        var secondCreate = rows[1]!.AsObject();
        var anchor = rows[2]!.AsObject();
        switch (change)
        {
            case "missing": rows.RemoveAt(2); rows.RemoveAt(1); break;
            case "identity": anchor["transitionId"] = "transition_pair_foreign"; break;
            case "kind": anchor["kind"] = "worsen"; break;
            case "wound":
                secondCreate["woundId"] = "wound_pair_foreign"; anchor["woundId"] = "wound_pair_foreign";
                secondCreate["beforeFingerprint"] = WoundHistoryState.ComputeNonexistentBeforeFingerprint("wound_pair_foreign");
                break;
            case "ordinal":
                var inserted = anchor.DeepClone().AsObject();
                inserted["transitionId"] = "transition_pair_intermediate";
                inserted["operationKey"] = "operation_pair_intermediate";
                inserted["eventRef"] = "event_pair_intermediate";
                inserted["afterFingerprint"] = "sha256:" + new string('e', 64);
                anchor["beforeFingerprint"] = inserted["afterFingerprint"]!.DeepClone();
                anchor["woundTransitionOrdinal"] = 3; rows.Insert(2, inserted);
                break;
            case "turn": anchor["turn"] = 46; break;
            case "terminal": anchor["kind"] = "heal"; anchor["terminal"] = true; break;
            case "fingerprint": anchor["afterFingerprint"] = "sha256:" + new string('d', 64); break;
            default: throw new ArgumentOutOfRangeException(nameof(change));
        }
        RenumberHistory(history);
        Assert.Equal(firstBefore, rows[0]!.ToJsonString());
        var before = history.ToJsonString();
        var validChangedHistory = ReadHistory(history);
        Assert.False(state.HasExactStrainHistory(validChangedHistory));
        Assert.Equal(before, history.ToJsonString());
    }

    /// <summary>Intrinsic parsing accepts lawful resealed second-parent facts, but those facts must still match unchanged History.</summary>
    [Theory]
    [InlineData("full-envelope")]
    [InlineData("transition-identity")]
    [InlineData("later-anchor")]
    [InlineData("creation-turn")]
    public void PairHistoryRejectsLawfulResealedSecondParentAgainstUnchangedHistory(string change)
    {
        var source = CreateSupportedSource();
        var originalHistory = CreateHistory(source);
        var originalState = ReadLedger(CreateLedger(source));
        Assert.True(originalState.HasExactStrainHistory(ReadHistory(originalHistory)));
        switch (change)
        {
            case "full-envelope": ChangeSecondParentDescription(source); break;
            case "transition-identity":
                MutateSecondParent(source, parent => parent["lastTransition"]!["transitionId"] = "transition_pair_other_anchor");
                break;
            case "later-anchor": SetSecondParentLaterAnchor(source); break;
            case "creation-turn":
                MutateSecondParent(source, parent =>
                {
                    parent["origin"]!["createdAtTurn"] = 45;
                    parent["lastTransition"]!["turn"] = 45;
                });
                break;
            default: throw new ArgumentOutOfRangeException(nameof(change));
        }
        AssertSupported(source);
        var resealedState = ReadLedger(CreateLedger(source));
        var before = originalHistory.ToJsonString();
        Assert.False(resealedState.HasExactStrainHistory(ReadHistory(originalHistory)));
        Assert.Equal(before, originalHistory.ToJsonString());
    }

    /// <summary>One comparison with two anchors resolves ordinary DATA; a changed actual still returns no DATA.</summary>
    [Fact]
    public void PairHistoryOrdinaryLookupRequiresOneExactComparison()
    {
        var source = CreateSupportedSource();
        var state = ReadLedger(CreateLedger(source));
        Assert.True(state.HasExactStrainHistory(ReadHistory(CreateHistory(source))));
        var actual = ReadActual(source);
        var ordinary = state.ReadRetainedStrainOrdinaryAfter("chaos_sea", "conflict-a", actual, false);
        Assert.NotNull(ordinary);
        Assert.True(JsonNode.DeepEquals(ReadOrdinary(source)["after"], ordinary));
        ordinary!["playerSideStrain"] = "clear";
        Assert.Equal("fractured", state.ReadRetainedStrainOrdinaryAfter("chaos_sea", "conflict-a", actual, false)!
            ["playerSideStrain"]!.GetValue<string>());
        actual["after"]!["oppositionSideStrain"] = "strained";
        Assert.Null(state.ReadRetainedStrainOrdinaryAfter("chaos_sea", "conflict-a", actual, false));
    }

    /// <summary>Requires one complete exact accepted v4 row, after a valid baseline reaches the supported matcher.</summary>
    [Theory]
    [InlineData("missing")]
    [InlineData("duplicate")]
    [InlineData("id")]
    [InlineData("mechanics")]
    [InlineData("nested-summary")]
    [InlineData("turn-marker")]
    public void PairHistoryRequiresCompleteAcceptedActual(string change)
    {
        var source = CreateSupportedSource();
        var state = ReadLedger(CreateLedger(source));
        var active = ActiveConflict(source);
        AssertHistoryMatch(state, active, 47, SpiritualWoundOpportunityReceiptState.RetainedPressureHistoryMatch.Exact);
        var log = active["exchangeLog"]!.AsArray();
        var actual = log[0]!.AsObject();
        switch (change)
        {
            case "missing": log.Clear(); break;
            case "duplicate": log.Add(actual.DeepClone()); break;
            case "id": actual["exchangeId"] = "exchange_pair_foreign"; break;
            case "mechanics": actual["after"]!["playerSideStrain"] = "overwhelmed"; break;
            case "nested-summary": actual["incomingAction"]!["summary"] = "Changed nested action narration."; break;
            case "turn-marker": actual["exchangeAtTurn"] = 45; break;
            default: throw new ArgumentOutOfRangeException(nameof(change));
        }
        AssertHistoryMatch(state, active, 47, SpiritualWoundOpportunityReceiptState.RetainedPressureHistoryMatch.Mismatch);
    }

    /// <summary>All retained v3/v4 rows must match in retained source order; swapping only log positions is not exact.</summary>
    [Theory]
    [InlineData("remove-first")]
    [InlineData("remove-second")]
    [InlineData("swap-positions")]
    [InlineData("change-first")]
    [InlineData("change-second")]
    [InlineData("duplicate-first")]
    [InlineData("duplicate-second")]
    public void PairHistoryRequiresEveryMixedVersionRowInSourceOrder(string change)
    {
        var pair = CreateSupportedSource();
        var singleton = SpiritualExistingPressureTestData.CreateData();
        Assert.Equal(2, SpiritualWoundSourceWitness.ValidateAndReadStrainData(singleton, out var singletonData).MaximumSeverityRank);
        Assert.Equal(3, Assert.IsType<SpiritualStrainPreimageData>(singletonData).Version);
        var root = CreateLedger(singleton); AppendSource(root, pair);
        var state = ReadLedger(root);
        var active = ActiveConflict(singleton);
        active["exchangeLog"]!.AsArray().Add(ReadActual(pair));
        AssertHistoryMatch(state, active, 47, SpiritualWoundOpportunityReceiptState.RetainedPressureHistoryMatch.Exact);
        var log = active["exchangeLog"]!.AsArray();
        switch (change)
        {
            case "remove-first": log.RemoveAt(0); break;
            case "remove-second": log.RemoveAt(1); break;
            case "swap-positions":
                var first = log[0]!.DeepClone(); var second = log[1]!.DeepClone();
                log[0] = second; log[1] = first;
                break;
            case "change-first": log[0]!["after"]!["oppositionSideStrain"] = "strained"; break;
            case "change-second": log[1]!["after"]!["oppositionSideStrain"] = "strained"; break;
            case "duplicate-first": log.Add(log[0]!.DeepClone()); break;
            case "duplicate-second": log.Add(log[1]!.DeepClone()); break;
            default: throw new ArgumentOutOfRangeException(nameof(change));
        }
        AssertHistoryMatch(state, active, 47, SpiritualWoundOpportunityReceiptState.RetainedPressureHistoryMatch.Mismatch);
    }

    /// <summary>Relative retained order permits lawful unrelated log rows and does not turn a raw input pointer into an absolute slot.</summary>
    [Fact]
    public void PairHistoryAllowsUnrelatedExchangeBetweenOrderedMixedVersionRows()
    {
        var pair = CreateSupportedSource();
        var singleton = SpiritualExistingPressureTestData.CreateData();
        Assert.Equal(2, SpiritualWoundSourceWitness.ValidateAndReadStrainData(singleton, out var singletonData).MaximumSeverityRank);
        Assert.Equal(3, Assert.IsType<SpiritualStrainPreimageData>(singletonData).Version);
        var root = CreateLedger(singleton); AppendSource(root, pair);
        var state = ReadLedger(root);
        var active = ActiveConflict(singleton);
        active["exchangeLog"]!.AsArray().Add(ReadActual(pair));
        AssertHistoryMatch(state, active, 47, SpiritualWoundOpportunityReceiptState.RetainedPressureHistoryMatch.Exact);

        var unrelated = SpiritualRetainedPressureGroupTestData.CreateExchange(
            "exchange_pair_unrelated_45", 45, "clear", "fractured");
        var unrelatedSource = SpiritualRetainedPressureGroupTestData.CreateSource(unrelated, "session-a",
            "request_pair_unrelated45", "snapshot_pair_unrelated45", "conflict-a", "sha256:" + new string('a', 64));
        Assert.False(unrelatedSource.ContainsKey("strainPreimage"));
        var unrelatedBefore = unrelatedSource.ToJsonString();
        var calculation = SpiritualWoundSourceWitness.ValidateAndReadStrainData(unrelatedSource, out var unrelatedData);
        Assert.Equal((13L, 0, 2, 1, 2), (calculation.TraumaPressure, calculation.PreviousStrainRank,
            calculation.NewStrainRank, calculation.ExtraJumpSteps, calculation.MaximumSeverityRank));
        Assert.Null(unrelatedData);
        Assert.True(JsonNode.DeepEquals(unrelated, ReadActual(unrelatedSource)));
        var receiptBefore = root.ToJsonString(); var pairBefore = pair.ToJsonString();
        // The pair's raw original pointer remains0 while its accepted row moves from global position1 to2.
        Assert.Equal("/activeConflict/exchangeLog/0", pair["strainPreimage"]!["originalInput"]!["exchangePointer"]!.GetValue<string>());
        active["exchangeLog"]!.AsArray().Insert(1, ReadActual(unrelatedSource));
        AssertHistoryMatch(state, active, 47, SpiritualWoundOpportunityReceiptState.RetainedPressureHistoryMatch.Exact);
        Assert.Equal(receiptBefore, root.ToJsonString()); Assert.Equal(pairBefore, pair.ToJsonString());
        Assert.Equal(unrelatedBefore, unrelatedSource.ToJsonString());
    }

    /// <summary>Only top-level narration may drift after the signed current turn passes the accepted exchange's turn46 marker.</summary>
    [Theory]
    [InlineData(46, false)]
    [InlineData(47, true)]
    public void PairHistorySummaryDriftUsesSignedCurrentTurn(int signedTurn, bool allowed)
    {
        var source = CreateSupportedSource();
        var state = ReadLedger(CreateLedger(source)); var active = ActiveConflict(source);
        AssertHistoryMatch(state, active, signedTurn, SpiritualWoundOpportunityReceiptState.RetainedPressureHistoryMatch.Exact);
        active["exchangeLog"]![0]!["summary"] = "Readable later narration.";
        AssertHistoryMatch(state, active, signedTurn, allowed
            ? SpiritualWoundOpportunityReceiptState.RetainedPressureHistoryMatch.Exact
            : SpiritualWoundOpportunityReceiptState.RetainedPressureHistoryMatch.Mismatch);
    }

    /// <summary>Closed, foreign and null active contours remain absent historical DATA rather than active applicability.</summary>
    [Theory]
    [InlineData("realm")]
    [InlineData("conflict")]
    [InlineData("closed")]
    [InlineData("null-active")]
    public void PairHistoryDistinguishesAbsentComparison(string contour)
    {
        var source = CreateSupportedSource(); var root = CreateLedger(source);
        var baseline = ReadLedger(root); var active = ActiveConflict(source);
        AssertHistoryMatch(baseline, active, 47, SpiritualWoundOpportunityReceiptState.RetainedPressureHistoryMatch.Exact);
        if (contour == "closed") CloseInstance(root);
        var state = ReadLedger(root); Assert.True(state.HasRetainedPressureComparisons);
        if (contour == "conflict") active["conflictId"] = "foreign_conflict";
        active["exchangeLog"]!.AsArray().Clear();
        Assert.Equal(SpiritualWoundOpportunityReceiptState.RetainedPressureHistoryMatch.Absent,
            state.MatchRetainedPressureHistory(contour == "realm" ? "shining_abode" : "chaos_sea",
                contour == "null-active" ? null : active, 47));
    }

    /// <summary>Combines both small recorded-parent chains with distinct operation identities and global ordinals.</summary>
    private static JsonObject CreateHistory(JsonObject source)
    {
        var rows = new List<JsonObject>();
        var parents = source["strainPreimage"]!["parents"]!.AsArray();
        for (var index = 0; index < parents.Count; index++)
        {
            var parent = Decode(parents[index]!["parentWoundJsonBase64"]);
            var chain = SpiritualExistingPressureTestData.CreateHistory(parent)["transitions"]!.AsArray();
            for (var entry = 0; entry < chain.Count; entry++)
            {
                var row = chain[entry]!.DeepClone().AsObject();
                row["operationKey"] = "operation_pair_history_" + index + "_" + entry;
                if (entry != chain.Count - 1) row["transitionId"] = "transition_pair_history_create_" + index;
                rows.Add(row);
            }
        }
        var root = WoundContractTestData.CreateHistory(rows.ToArray());
        RenumberHistory(root);
        return root;
    }

    /// <summary>Restores only global row coordinates after an explicitly authored chronology mutation.</summary>
    private static void RenumberHistory(JsonObject history)
    {
        var rows = history["transitions"]!.AsArray();
        for (var index = 0; index < rows.Count; index++) rows[index]!["ordinal"] = index + 1;
        history["nextOrdinal"] = rows.Count + 1;
    }

    /// <summary>Authors a valid later complicate anchor so kind, ordinal and terminal negatives reach the History join.</summary>
    private static void SetSecondParentLaterAnchor(JsonObject source) => MutateSecondParent(source, parent =>
    {
        parent["lastTransition"]!["kind"] = "complicate";
        parent["lastTransition"]!["ordinal"] = 2;
        parent["lastTransition"]!["turn"] = 45;
        parent["lastTransition"]!["transitionId"] = "transition_pair_second_complicate";
    });

    /// <summary>Requires a lawful complete second envelope before encoding and resealing the changed source.</summary>
    private static void MutateSecondParent(JsonObject source, Action<JsonObject> change)
    {
        var row = source["strainPreimage"]!["parents"]![1]!.AsObject();
        var parent = Decode(row["parentWoundJsonBase64"]); change(parent);
        var parsed = WoundMaterializationContract.Parse(parent.ToJsonString(), "unit.pair.recordedParent");
        Assert.True(parsed.IsValid, string.Join("\n", parsed.Issues));
        row["parentWoundJsonBase64"] = Encode(parent); Seal(source);
    }

    /// <summary>Creates minimal detached accepted-log DATA for the typed whole-history matcher.</summary>
    private static JsonObject ActiveConflict(JsonObject source) => new()
    {
        ["conflictId"] = source["conflictId"]!.DeepClone(), ["exchangeLog"] = new JsonArray(ReadActual(source))
    };

    /// <summary>Checks the exact matcher result and verifies it leaves the supplied active log unchanged.</summary>
    private static void AssertHistoryMatch(SpiritualWoundOpportunityReceiptState state, JsonObject active, int turn,
        SpiritualWoundOpportunityReceiptState.RetainedPressureHistoryMatch expected)
    {
        var before = active.ToJsonString();
        Assert.Equal(expected, state.MatchRetainedPressureHistory("chaos_sea", active, turn));
        Assert.Equal(before, active.ToJsonString());
    }

    /// <summary>Authors a lawful immutable closure after the source turn without granting active applicability.</summary>
    private static void CloseInstance(JsonObject root)
    {
        var closure = new JsonObject
        {
            ["closureId"] = "", ["ordinal"] = 1, ["instanceId"] = root["instances"]![0]!["instanceId"]!.DeepClone(),
            ["terminalTurn"] = 46, ["terminalEventRef"] = "pair_terminal_event",
            ["terminalConflictFingerprint"] = "sha256:" + new string('c', 64), ["closureFingerprint"] = ""
        };
        var hash = SpiritualWoundConflictInstanceState.ComputeRowFingerprint(closure, "closure");
        closure["closureId"] = "spiritual_closure_" + hash[7..]; closure["closureFingerprint"] = hash;
        root["closures"]!.AsArray().Add(closure); root["nextClosureOrdinal"] = 2;
    }
}
