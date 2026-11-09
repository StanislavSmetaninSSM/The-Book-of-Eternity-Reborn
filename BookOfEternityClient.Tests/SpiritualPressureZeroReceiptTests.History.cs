using System.Text.Json.Nodes;
using BookOfEternityClient.Services;
using Xunit;

namespace BookOfEternityClient.Tests;

public sealed partial class SpiritualPressureZeroReceiptTests
{
    /// <summary>The comparison retains its exact parent anchor after lawful later healing, but not after anchor loss or replacement.</summary>
    [Theory]
    [InlineData("exact", 3)]
    [InlineData("exact", 4)]
    [InlineData("missing", 3)]
    [InlineData("identity", 3)]
    [InlineData("fingerprint", 3)]
    [InlineData("later-healing", 3)]
    public void PressureZeroHistoryRequiresExactParent(string change, int rank)
    {
        var (_, root) = CreateLedger("zero-only", rank); var state = ReadLedger(root);
        var source = root["strainComparisons"]![0]!["witness"]!.AsObject();
        var history = CreateHistory(source);
        Assert.True(state.HasRetainedPressureComparisons);
        Assert.Empty(root["decisions"]!.AsArray()); Assert.True(state.HasExactStrainHistory(ReadHistory(history)));
        var anchor = history["transitions"]![0]!.AsObject();
        switch (change)
        {
            case "exact": break;
            case "missing": history["transitions"]!.AsArray().Clear(); history["nextOrdinal"] = 1; break;
            case "identity": anchor["transitionId"] = "transition_pressure_zero_foreign"; break;
            case "fingerprint": anchor["afterFingerprint"] = "sha256:" + new string('d', 64); break;
            case "later-healing":
                var heal = anchor.DeepClone().AsObject();
                heal["transitionId"] = "transition_pressure_zero_healed"; heal["operationKey"] = "operation_pressure_zero_healed";
                heal["ordinal"] = 2; heal["woundTransitionOrdinal"] = 2; heal["kind"] = "heal"; heal["turn"] = 45;
                heal["eventRef"] = "event_pressure_zero_healed"; heal["terminal"] = true;
                heal["beforeFingerprint"] = anchor["afterFingerprint"]!.DeepClone();
                heal["afterFingerprint"] = "sha256:" + new string('d', 64);
                history["transitions"]!.AsArray().Add(heal); history["nextOrdinal"] = 3;
                break;
            default: throw new ArgumentOutOfRangeException(nameof(change));
        }
        var bytes = history.ToJsonString(); var receiptBytes = root.ToJsonString();
        Assert.Equal(change is "exact" or "later-healing", state.HasExactStrainHistory(ReadHistory(history)));
        if (change == "later-healing")
        {
            var actual = ReadActual(source);
            Assert.True(JsonNode.DeepEquals(ReadOrdinary(source)["after"],
                state.ReadRetainedStrainOrdinaryAfter("chaos_sea", "conflict-a", actual, false)));
            // Historical projection is detached DATA, not permission to use the now-healed parent.
            var escaped = state.ReadRetainedStrainOrdinaryAfter("chaos_sea", "conflict-a", actual, false)!;
            escaped["playerSideStrain"] = "broken";
            Assert.Equal("strained", state.ReadRetainedStrainOrdinaryAfter("chaos_sea", "conflict-a", actual, false)!["playerSideStrain"]!.GetValue<string>());
        }
        Assert.Equal(bytes, history.ToJsonString()); Assert.Equal(receiptBytes, root.ToJsonString());
    }

    /// <summary>The real normalizer reads History once despite empty decisions, refusing missing, malformed or mismatched anchors.</summary>
    [Theory]
    [InlineData("exact")]
    [InlineData("missing")]
    [InlineData("malformed")]
    [InlineData("mismatch")]
    public async Task PressureZeroPublicationRequiresHistoryWithoutDecisions(string change)
    {
        var (_, root) = CreateLedger("zero-only"); var state = ReadLedger(root);
        var history = CreateHistory(root["strainComparisons"]![0]!["witness"]!.AsObject());
        Assert.Empty(root["sources"]!.AsArray()); Assert.Empty(root["decisions"]!.AsArray());
        Assert.True(state.HasRetainedPressureComparisons); Assert.True(state.HasExactStrainHistory(ReadHistory(history)));
        if (change == "mismatch")
        {
            history["transitions"]![0]!["afterFingerprint"] = "sha256:" + new string('d', 64);
            Assert.False(state.HasExactStrainHistory(ReadHistory(history)));
        }
        var bytes = history.ToJsonString(); var receiptBytes = root.ToJsonString(); var reads = 0;
        Task Act() => CanonicalStateNormalizer.ValidateSpiritualReceiptPublicationHistoryAsync(state, root, () =>
        {
            reads++;
            return Task.FromResult(change switch { "missing" => null, "malformed" => "{", _ => history.ToJsonString() });
        });
        if (change == "exact") await Act();
        else await Assert.ThrowsAsync<InvalidDataException>(Act);
        Assert.Equal(1, reads); Assert.Equal(bytes, history.ToJsonString()); Assert.Equal(receiptBytes, root.ToJsonString());
    }

    /// <summary>Every retained zero actual requires exactly one complete accepted log entry, independently of its parent's older turn.</summary>
    [Theory]
    [InlineData("exact")]
    [InlineData("missing")]
    [InlineData("duplicate")]
    [InlineData("identity")]
    [InlineData("mechanics")]
    [InlineData("nested-summary")]
    [InlineData("turn-marker")]
    public void PressureZeroHistoryMatchesSignedActual(string change)
    {
        var (_, root) = CreateLedger("zero-only"); var state = ReadLedger(root);
        var source = root["strainComparisons"]![0]!["witness"]!.AsObject();
        var active = CreateActiveConflict(source); var log = active["exchangeLog"]!.AsArray(); var actual = log[0]!.AsObject();
        Assert.True(state.HasRetainedPressureComparisons);
        Assert.Equal(SpiritualWoundOpportunityReceiptState.RetainedPressureHistoryMatch.Exact,
            state.MatchRetainedPressureHistory("chaos_sea", active, 45));
        switch (change)
        {
            case "exact": break;
            case "missing": log.Clear(); break;
            case "duplicate": log.Add(actual.DeepClone()); break;
            case "identity": actual["exchangeId"] = "exchange_pressure_zero_foreign"; break;
            case "mechanics": actual["after"]!["playerSideStrain"] = "overwhelmed"; break;
            case "nested-summary": actual["incomingAction"]!["summary"] = "Changed nested action."; break;
            case "turn-marker": actual["exchangeAtTurn"] = 43; break;
            default: throw new ArgumentOutOfRangeException(nameof(change));
        }
        var bytes = active.ToJsonString(); var receiptBytes = root.ToJsonString();
        Assert.Equal(change == "exact" ? SpiritualWoundOpportunityReceiptState.RetainedPressureHistoryMatch.Exact :
            SpiritualWoundOpportunityReceiptState.RetainedPressureHistoryMatch.Mismatch,
            state.MatchRetainedPressureHistory("chaos_sea", active, 45));
        Assert.Equal(bytes, active.ToJsonString()); Assert.Equal(receiptBytes, root.ToJsonString());
    }

    /// <summary>Only a top-level summary from an accepted prior turn may drift; the signed current turn remains exact.</summary>
    [Theory]
    [InlineData(44, false)]
    [InlineData(45, true)]
    public void PressureZeroHistorySummaryUsesSignedCurrentTurn(int signedCurrentTurn, bool allowed)
    {
        var (_, root) = CreateLedger("zero-only"); var state = ReadLedger(root);
        var source = root["strainComparisons"]![0]!["witness"]!.AsObject(); var active = CreateActiveConflict(source);
        Assert.Equal(SpiritualWoundOpportunityReceiptState.RetainedPressureHistoryMatch.Exact,
            state.MatchRetainedPressureHistory("chaos_sea", active, signedCurrentTurn));
        active["exchangeLog"]![0]!["summary"] = "Later readable narration.";
        var bytes = active.ToJsonString();
        Assert.Equal(allowed ? SpiritualWoundOpportunityReceiptState.RetainedPressureHistoryMatch.Exact :
            SpiritualWoundOpportunityReceiptState.RetainedPressureHistoryMatch.Mismatch,
            state.MatchRetainedPressureHistory("chaos_sea", active, signedCurrentTurn));
        Assert.Equal(bytes, active.ToJsonString());
    }

    /// <summary>Builds only the exact recorded History DATA; it never authenticates the parent or creates a live owner.</summary>
    private static JsonObject CreateHistory(JsonObject source) =>
        SpiritualExistingPressureTestData.CreateHistory(SpiritualExistingPressureTestData.ReadParent(source));

    /// <summary>Validates History independently before the receipt's exact-anchor join.</summary>
    private static WoundHistoryState ReadHistory(JsonObject root)
    {
        var bytes = root.ToJsonString(); var parsed = WoundHistoryState.Parse(bytes, WoundHistoryState.HistoryPath);
        Assert.True(parsed.IsValid, string.Join("\n", parsed.Issues)); Assert.Equal(bytes, root.ToJsonString());
        return Assert.IsType<WoundHistoryState>(parsed.State);
    }

    /// <summary>Builds a detached accepted-log comparison surface, not a signed frame or source owner.</summary>
    private static JsonObject CreateActiveConflict(JsonObject source) => new()
    {
        ["conflictId"] = source["conflictId"]!.DeepClone(), ["exchangeLog"] = new JsonArray(ReadActual(source))
    };
}
