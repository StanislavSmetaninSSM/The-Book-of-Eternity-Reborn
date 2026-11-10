using System.Text.Json.Nodes;
using BookOfEternityClient.Services;
using Xunit;

namespace BookOfEternityClient.Tests;

public sealed partial class SpiritualExistingPressureReceiptTests
{
    /// <summary>Checks whole-log membership without confusing absence with a changed or deleted accepted exchange.</summary>
    [Theory]
    [InlineData("exact")]
    [InlineData("missing")]
    [InlineData("duplicate")]
    [InlineData("id")]
    [InlineData("mechanics")]
    [InlineData("nested-summary")]
    [InlineData("turn-marker")]
    public void PressureHistoryMatchesExactlyOneCompleteAcceptedActual(string change)
    {
        var source = CreateSupportedSource();
        var (root, _) = CreateLedger(source);
        var ledger = ReadLedger(root);
        var active = PressureActiveConflict(source);
        Assert.True(ledger.HasRetainedPressureComparisons);
        Assert.Equal(SpiritualWoundOpportunityReceiptState.RetainedPressureHistoryMatch.Exact,
            ledger.MatchRetainedPressureHistory("chaos_sea", active, 45));
        var log = active["exchangeLog"]!.AsArray();
        var actual = log[0]!.AsObject();
        switch (change)
        {
            case "exact": break;
            case "missing": log.Clear(); break;
            case "duplicate": log.Add(actual.DeepClone()); break;
            case "id": actual["exchangeId"] = "foreign_exchange"; break;
            case "mechanics": actual["after"]!["playerSideStrain"] = "fractured"; break;
            case "nested-summary": actual["incomingAction"]!["summary"] = "Changed nested summary."; break;
            case "turn-marker": actual["exchangeAtTurn"] = 43; break;
            default: throw new ArgumentOutOfRangeException(nameof(change));
        }
        var before = active.ToJsonString();
        Assert.Equal(change == "exact" ? SpiritualWoundOpportunityReceiptState.RetainedPressureHistoryMatch.Exact :
            SpiritualWoundOpportunityReceiptState.RetainedPressureHistoryMatch.Mismatch,
            ledger.MatchRetainedPressureHistory("chaos_sea", active, 45));
        Assert.Equal(before, active.ToJsonString());
    }

    /// <summary>Checks all retained pressure exchanges, not merely the first matching row.</summary>
    [Fact]
    public void PressureHistoryChecksEveryRetainedComparison()
    {
        var first = CreateSupportedSource();
        var second = first.DeepClone().AsObject();
        var actual = SpiritualExistingPressurePreimageTests.ReadActual(second);
        var ordinary = SpiritualExistingPressurePreimageTests.ReadOrdinary(second);
        foreach (var exchange in new[] { actual, ordinary })
        {
            exchange["exchangeId"] = "exchange_existing_pressure_45";
            exchange["turnNumber"] = 45; exchange["exchangeAtTurn"] = 45;
        }
        second["exchangeId"] = "exchange_existing_pressure_45";
        second["exchangeJsonBase64"] = Encode(actual);
        second["strainPreimage"]!["ordinaryExchangeJsonBase64"] = Encode(ordinary);
        second["turnEvidence"]!["turn"] = 45;
        second["turnEvidence"]!["requestId"] = "request-existing-pressure45";
        second["turnEvidence"]!["snapshotToken"] = "snapshot-existing-pressure45";
        SpiritualExistingPressurePreimageTests.Seal(second);
        Assert.Equal(2, SpiritualWoundSourceWitness.Validate(second).MaximumSeverityRank);
        var (root, _) = CreateLedger(first);
        var extra = CreateLedger(second).Root;
        var row = extra["sources"]![0]!.AsObject();
        row["ordinal"] = 2; row["instanceSourceOrdinal"] = 2;
        root["sources"]!.AsArray().Add(row.DeepClone()); root["nextSourceOrdinal"] = 3;
        var decision = extra["decisions"]![0]!.AsObject();
        decision["ordinal"] = 2; decision["decisionId"] = "decision_pressure_declined_second";
        decision["opportunityRef"] = "opportunity_pressure_declined_second";
        decision["decisionFingerprint"] = SpiritualWoundStateJson.Hash(decision, "decision", "decisionFingerprint");
        root["decisions"]!.AsArray().Add(decision.DeepClone()); root["nextDecisionOrdinal"] = 3;
        var ledger = ReadLedger(root);
        var active = PressureActiveConflict(first);
        active["exchangeLog"]!.AsArray().Add(actual);
        Assert.Equal(SpiritualWoundOpportunityReceiptState.RetainedPressureHistoryMatch.Exact,
            ledger.MatchRetainedPressureHistory("chaos_sea", active, 46));
        active["exchangeLog"]!.AsArray().RemoveAt(1);
        Assert.Equal(SpiritualWoundOpportunityReceiptState.RetainedPressureHistoryMatch.Mismatch,
            ledger.MatchRetainedPressureHistory("chaos_sea", active, 46));
    }

    /// <summary>Allows only prior-turn readable top summaries, using the accepted exchange marker rather than the parent turn.</summary>
    [Theory]
    [InlineData(44, false)]
    [InlineData(45, true)]
    public void PressureHistorySummaryDriftUsesSignedCurrentTurn(int signedTurn, bool allowed)
    {
        var source = CreateSupportedSource();
        var ledger = ReadLedger(CreateLedger(source).Root);
        var active = PressureActiveConflict(source);
        active["exchangeLog"]![0]!["summary"] = "Readable later narration.";
        Assert.Equal(allowed ? SpiritualWoundOpportunityReceiptState.RetainedPressureHistoryMatch.Exact :
            SpiritualWoundOpportunityReceiptState.RetainedPressureHistoryMatch.Mismatch,
            ledger.MatchRetainedPressureHistory("chaos_sea", active, signedTurn));
    }

    /// <summary>Does not demand old logs for foreign or closed instances or ordinary pressure without retained v3.</summary>
    [Theory]
    [InlineData("realm")]
    [InlineData("conflict")]
    [InlineData("closed")]
    [InlineData("ordinary")]
    [InlineData("v2")]
    public void PressureHistoryDistinguishesAbsentComparison(string contour)
    {
        var source = contour == "v2" ? SpiritualExistingStrainPreimageTests.CreateData(3).Source : CreateSupportedSource();
        var (root, _) = CreateLedger(source);
        if (contour == "ordinary")
        {
            source.Remove("strainPreimage");
            SpiritualExistingPressurePreimageTests.Seal(source);
            (root, _) = CreateLedger(source);
        }
        if (contour == "closed") ClosePressureInstance(root);
        var ledger = ReadLedger(root);
        var active = PressureActiveConflict(source);
        if (contour == "conflict") active["conflictId"] = "foreign_conflict";
        active["exchangeLog"]!.AsArray().Clear();
        Assert.Equal(contour is not ("ordinary" or "v2"), ledger.HasRetainedPressureComparisons);
        Assert.Equal(SpiritualWoundOpportunityReceiptState.RetainedPressureHistoryMatch.Absent,
            ledger.MatchRetainedPressureHistory(contour == "realm" ? "shining_abode" : "chaos_sea", active, 45));
    }

    /// <summary>Runs the actual publication History gate even when all retained v3 injury decisions were declined.</summary>
    [Theory]
    [InlineData("exact")]
    [InlineData("missing")]
    [InlineData("fingerprint")]
    public async Task PressurePublicationRequiresHistoryWithoutMaterializations(string change)
    {
        var (root, parent) = CreateLedger(CreateSupportedSource());
        var state = ReadLedger(root);
        Assert.All(root["decisions"]!.AsArray(), row => Assert.Equal("none", row!["decision"]!.GetValue<string>()));
        var history = CreateHistory(parent);
        Assert.True(state.HasExactStrainHistory(ReadHistory(history)));
        if (change == "fingerprint") history["transitions"]![0]!["afterFingerprint"] = "sha256:" + new string('d', 64);
        var reads = 0;
        Task Act() => CanonicalStateNormalizer.ValidateSpiritualReceiptPublicationHistoryAsync(state, root, () =>
        {
            reads++;
            return Task.FromResult(change == "missing" ? null : history.ToJsonString());
        });
        if (change == "exact") await Act();
        else await Assert.ThrowsAsync<InvalidDataException>(Act);
        Assert.Equal(1, reads);
    }

    /// <summary>Preserves ordinary and v2 declined publication behavior without introducing a History read.</summary>
    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task PressurePublicationPreservesNonPressureDeclines(bool versionTwo)
    {
        var source = SpiritualExistingStrainPreimageTests.CreateData(3).Source;
        var (root, _) = CreateLedger(source);
        if (!versionTwo)
        {
            root["sources"]!.AsArray().Clear(); root["decisions"]!.AsArray().Clear();
            root["nextSourceOrdinal"] = 1; root["nextDecisionOrdinal"] = 1;
        }
        var state = ReadLedger(root);
        Assert.False(state.HasRetainedPressureComparisons);
        await CanonicalStateNormalizer.ValidateSpiritualReceiptPublicationHistoryAsync(state, root,
            () => throw new InvalidOperationException("Ordinary/v2 declined receipt does not need History."));
    }

    private static JsonObject PressureActiveConflict(JsonObject source) => new()
    {
        ["conflictId"] = source["conflictId"]!.DeepClone(),
        ["exchangeLog"] = new JsonArray(SpiritualExistingPressurePreimageTests.ReadActual(source))
    };

    private static void ClosePressureInstance(JsonObject root)
    {
        var closure = new JsonObject
        {
            ["closureId"] = "", ["ordinal"] = 1, ["instanceId"] = root["instances"]![0]!["instanceId"]!.DeepClone(),
            ["terminalTurn"] = 44, ["terminalEventRef"] = "pressure_terminal_event",
            ["terminalConflictFingerprint"] = "sha256:" + new string('c', 64), ["closureFingerprint"] = ""
        };
        var hash = SpiritualWoundConflictInstanceState.ComputeRowFingerprint(closure, "closure");
        closure["closureId"] = "spiritual_closure_" + hash[7..]; closure["closureFingerprint"] = hash;
        root["closures"]!.AsArray().Add(closure); root["nextClosureOrdinal"] = 2;
    }
}
