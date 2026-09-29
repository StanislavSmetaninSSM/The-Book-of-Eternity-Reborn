using System.Text.Json.Nodes;
using BookOfEternityClient.Services;
using Xunit;

namespace BookOfEternityClient.Tests;

public sealed partial class AfterlifeResourceCutoverTests
{
    /// <summary>
    /// Excludes an exact signed historical exchange while retaining the newly authored suffix.
    /// </summary>
    [Fact]
    public async Task OriginalExchangeInventory_ExcludesExactSignedHistoricalPrefix()
    {
        await using var context = await CreateCompleteConflictFrameContextAsync();
        var original = Assert.IsType<JsonObject>(await context.ReadJsonAsync(AfterlifeSpiritualConflictState.StatePath));
        await WriteCompleteConflictFrameExchangeAsync(context);
        var proposal = Assert.IsType<JsonObject>(await context.ReadJsonAsync(AfterlifeSpiritualConflictState.StatePath));
        var historical = proposal[AfterlifeSpiritualConflictState.ResponseField]!["exchange"]!.DeepClone().AsObject();
        historical["exchangeId"] = "exchange_source_historical";
        historical["exchangeAtTurn"] = 41;
        historical["turnNumber"] = 41;
        original["activeConflict"]!["exchangeLog"]!.AsArray().Add(historical.DeepClone());
        await context.WriteExactJsonAsync(AfterlifeSpiritualConflictState.StatePath, original.ToJsonString());
        await context.CaptureValidatedPendingSnapshotAsync(
            turn: 42, currentRealm: "Chaos Sea", preGeneratedDices1d20: [15, 5, 12, 8]);
        var current = proposal[AfterlifeSpiritualConflictState.ResponseField]!["activeConflictAfter"]!
            .DeepClone().AsObject();
        current["exchangeLog"]!.AsArray().Insert(0, historical.DeepClone());
        original["activeConflict"] = current;
        await context.WriteExactJsonAsync(AfterlifeSpiritualConflictState.StatePath, original.ToJsonString());
        await using var lease = await context.FileSystem.AcquireCanonicalWriteLeaseAsync();
        var result = await context.Validator.BeginSpiritualWoundSourceSessionAsync(lease);
        AssertNoConflictFrameErrors(result.Issues);
        var source = Assert.IsType<ValidationService.SpiritualWoundSourceSession>(result.Session);

        Assert.True(source.TryReadInitialActiveExchangeInventory(out _, out var newIds));
        Assert.Equal(new[] { proposal[AfterlifeSpiritualConflictState.ResponseField]!["exchange"]!["exchangeId"]!
            .GetValue<string>() }, newIds);
        Assert.DoesNotContain("exchange_source_historical", newIds);
    }

    /// <summary>
    /// Rejects malformed and duplicate rows while projecting an original suffix.
    /// </summary>
    [Fact]
    public void OriginalExchangeInventory_MalformedRowsReturnPending()
    {
        var before = new JsonObject
        {
            ["activeConflict"] = new JsonObject
            {
                ["conflictId"] = "same",
                ["exchangeLog"] = new JsonArray(new JsonObject { ["exchangeId"] = "historical" })
            },
            ["recentConflicts"] = new JsonArray()
        };
        var candidate = before.DeepClone().AsObject();
        candidate["activeConflict"]!["exchangeLog"]!.AsArray()
            .Add(new JsonObject { ["exchangeId"] = "future" });
        Assert.True(ValidationService.SpiritualWoundSourceSession.TryProjectInitialActiveExchangeInventory(
            before, candidate, out _, out var ids));
        Assert.Equal(new[] { "future" }, ids);

        candidate["activeConflict"]!["exchangeLog"]![1] = JsonValue.Create("malformed-future");
        Assert.False(ValidationService.SpiritualWoundSourceSession.TryProjectInitialActiveExchangeInventory(
            before, candidate, out _, out _));

        candidate["activeConflict"]!["exchangeLog"]![1] = new JsonObject { ["exchangeId"] = "historical" };
        Assert.False(ValidationService.SpiritualWoundSourceSession.TryProjectInitialActiveExchangeInventory(
            before, candidate, out _, out _));

        before["activeConflict"]!["exchangeLog"]![0] = JsonValue.Create("malformed-historical");
        candidate["activeConflict"]!["exchangeLog"]![0] = JsonValue.Create("malformed-historical");
        Assert.False(ValidationService.SpiritualWoundSourceSession.TryProjectInitialActiveExchangeInventory(
            before, candidate, out _, out _));

        before["activeConflict"]!["exchangeLog"]![0] = new JsonObject { ["exchangeId"] = "historical" };
        before["activeConflict"]!["exchangeLog"]!.AsArray()
            .Add(new JsonObject { ["exchangeId"] = "historical" });
        candidate["activeConflict"]!["exchangeLog"]![0] = new JsonObject { ["exchangeId"] = "historical" };
        candidate["activeConflict"]!["exchangeLog"]!.AsArray()
            .Add(new JsonObject { ["exchangeId"] = "future" });
        Assert.False(ValidationService.SpiritualWoundSourceSession.TryProjectInitialActiveExchangeInventory(
            before, candidate, out _, out _));
    }
}
