using System.Text.Json.Nodes;
using BookOfEternityClient.Services;
using Xunit;

namespace BookOfEternityClient.Tests;

/// <summary>
/// Checks exact replay and rejected-attempt isolation for spiritual capture allocations.
/// </summary>
public sealed class SpiritualWoundReplayJournalTests
{
    private const string Effect = "effect_0123456789abcdef0123456789abcdef";
    private const string Member = "member_1123456789abcdef0123456789abcdef";
    private const string Time = "2026-09-22T08:00:00.0000000+00:00";

    /// <summary>
    /// Retains every ordinary random allocation family without changing its prefix.
    /// </summary>
    /// <param name="kind">
    /// Closed allocation kind and its existing identifier prefix.
    /// </param>
    [Theory]
    [InlineData("effect")]
    [InlineData("effect_transition")]
    [InlineData("effect_resolution")]
    [InlineData("combatant")]
    [InlineData("member")]
    [InlineData("vehicle")]
    [InlineData("resource_definition")]
    [InlineData("resource_definition_seal")]
    [InlineData("resource_resolution")]
    [InlineData("resource_operation")]
    [InlineData("resource_transition")]
    public void AllocationFamiliesRoundTrip(string kind)
    {
        var value = kind + "_0123456789abcdef0123456789abcdef";
        var record = SpiritualWoundReplayJournal.CreateAppend("[]");
        Assert.Equal(value, record.Request(kind, "owner", "operation:1/slot:0", () => value));
        var replay = SpiritualWoundReplayJournal.CreateReplay(record.Export().ToJsonString());
        Assert.Equal(value, replay.Request(kind, "owner", "operation:1/slot:0", ThrowGenerator));
        Assert.Single(replay.Export());
    }

    /// <summary>
    /// Replays a detached journal without touching randomness or the clock, then appends once.
    /// </summary>
    [Fact]
    public void RecordingAndColdReplayPreserveValuesAndDetachRows()
    {
        var original = SpiritualWoundReplayJournal.CreateAppend("[]");
        Assert.Equal(Effect, original.Request("effect", "effects", "exchange:1/root:0", () => Effect));
        Assert.Equal(Time, original.Request("utc_time", "resources", "exchange:1/request:0", () => Time));
        var rows = original.Export();
        var cold = SpiritualWoundReplayJournal.CreateReplay(rows.ToJsonString());
        rows[0]!["value"] = "changed";
        Assert.Equal(Effect, cold.Request("effect", "effects", "exchange:1/root:0", ThrowGenerator));
        Assert.Equal(Time, cold.Request("utc_time", "resources", "exchange:1/request:0", ThrowGenerator));
        var append = SpiritualWoundReplayJournal.CreateAppend(cold.Export().ToJsonString());
        Assert.Throws<InvalidOperationException>(() => append.Export());
        Assert.Equal(Effect, append.Request("effect", "effects", "exchange:1/root:0", ThrowGenerator));
        Assert.Equal(Time, append.Request("utc_time", "resources", "exchange:1/request:0", ThrowGenerator));
        var count = 0;
        Assert.Equal(Member, append.Request("member", "combat", "exchange:2/member:0", () => { count++; return Member; }));
        Assert.Equal(1, count);
        Assert.Equal(3, append.Export().Count);
        Assert.Equal(2, cold.Export().Count);
    }

    /// <summary>
    /// Reads only committed replay progress while a complete future journal remains retained.
    /// </summary>
    [Fact]
    public void ColdCursor_ExcludesSpeculativeProgressAndPermitsPartialReplay()
    {
        var record = SpiritualWoundReplayJournal.CreateAppend("[]");
        record.Request("effect", "effects", "exchange:1/root:0", () => Effect);
        record.Request("utc_time", "resources", "exchange:2/request:0", () => Time);
        var replay = SpiritualWoundReplayJournal.CreateReplay(record.Export().ToJsonString(), requireScope: true);
        Assert.Equal(0, replay.ReadCursor());
        using (replay.BeginSpeculation())
        {
            replay.Request("effect", "effects", "exchange:1/root:0", ThrowGenerator);
            Assert.Throws<InvalidOperationException>(() => replay.ReadCursor());
        }
        Assert.Equal(0, replay.ReadCursor());
        using (var committed = replay.BeginSpeculation())
        {
            replay.Request("effect", "effects", "exchange:1/root:0", ThrowGenerator);
            committed.Commit();
        }
        Assert.Equal(1, replay.ReadCursor());
        Assert.Single(replay.ExportConsumedPrefix());
        Assert.Throws<InvalidOperationException>(() => replay.Export());
        using (var committed = replay.BeginSpeculation())
        {
            replay.Request("utc_time", "resources", "exchange:2/request:0", ThrowGenerator);
            committed.Commit();
        }
        Assert.Equal(2, replay.ReadCursor());
        Assert.Equal(2, replay.Export().Count);
        replay.Invalidate();
        Assert.Throws<InvalidOperationException>(() => replay.ReadCursor());
    }

    /// <summary>
    /// Opens new allocation only after the complete retained stream has been replayed outside a scope.
    /// </summary>
    [Fact]
    public void ColdReplay_AppendHandoffRequiresCompleteCommittedPrefix()
    {
        var replay = SpiritualWoundReplayJournal.CreateReplay(OneRow(), requireScope: true);
        Assert.Throws<InvalidOperationException>(() => replay.EnableAppendAfterReplay());
        using (var scope = replay.BeginSpeculation())
        {
            Assert.Equal(Effect, replay.Request("effect", "effects", "exchange:1/root:0", ThrowGenerator));
            Assert.Throws<InvalidOperationException>(() => replay.EnableAppendAfterReplay());
            scope.Commit();
        }

        replay.EnableAppendAfterReplay();
        using (var scope = replay.BeginSpeculation())
        {
            Assert.Equal(Member, replay.Request("member", "combat", "exchange:2/member:0", () => Member));
            scope.Commit();
        }
        Assert.Equal(2, replay.Export().Count);
    }

    /// <summary>
    /// Faults mismatched requests before executing a generator and prevents candidate export.
    /// </summary>
    /// <param name="kind">
    /// Requested allocation kind.
    /// </param>
    /// <param name="owner">
    /// Requested owning subsystem.
    /// </param>
    /// <param name="coordinate">
    /// Requested causal coordinate.
    /// </param>
    [Theory]
    [InlineData("member", "effects", "exchange:1/root:0")]
    [InlineData("effect", "other", "exchange:1/root:0")]
    [InlineData("effect", "effects", "exchange:2/root:0")]
    public void ChangedRequestFaultsReplay(string kind, string owner, string coordinate)
    {
        var replay = SpiritualWoundReplayJournal.CreateReplay(OneRow());
        Assert.Throws<InvalidOperationException>(() => replay.Request(kind, owner, coordinate, ThrowGenerator));
        Assert.Throws<InvalidOperationException>(() => replay.Export());
        Assert.Throws<InvalidOperationException>(() => replay.Request("effect", "effects", "exchange:1/root:0", ThrowGenerator));
    }

    /// <summary>
    /// Rejects extra replay requests and allocation exceptions without exporting partial attempts.
    /// </summary>
    [Fact]
    public void ExhaustionAndGeneratorFailureCannotExport()
    {
        var replay = SpiritualWoundReplayJournal.CreateReplay("[]");
        Assert.Throws<InvalidOperationException>(() => replay.Request("effect", "effects", "new", ThrowGenerator));
        Assert.Throws<InvalidOperationException>(() => replay.Export());
        var record = SpiritualWoundReplayJournal.CreateAppend("[]");
        Assert.Throws<NotSupportedException>(() => record.Request("effect", "effects", "new", ThrowGenerator));
        Assert.Throws<InvalidOperationException>(() => record.Export());
    }

    /// <summary>
    /// Rejects closed-shape, identity, timestamp and ordinal corruption during loading.
    /// </summary>
    /// <param name="mutation">
    /// Corruption applied to a valid retained row.
    /// </param>
    [Theory]
    [InlineData("extra")]
    [InlineData("ordinal")]
    [InlineData("fraction")]
    [InlineData("kind")]
    [InlineData("value")]
    [InlineData("owner")]
    [InlineData("time")]
    [InlineData("duplicate_key")]
    [InlineData("duplicate_value")]
    public void MalformedRowsFailClosed(string mutation)
    {
        var rows = JsonNode.Parse(OneRow())!.AsArray();
        var row = rows[0]!.AsObject();
        switch (mutation)
        {
            case "extra": row["extra"] = true; break;
            case "ordinal": row["ordinal"] = 1; break;
            case "fraction": row["ordinal"] = JsonNode.Parse("0.0"); break;
            case "kind": row["kind"] = "arbitrary"; break;
            case "value": row["value"] = Effect.ToUpperInvariant(); break;
            case "owner": row["owner"] = " effects "; break;
            case "time": row["kind"] = "utc_time"; row["value"] = "2026-09-22T08:00:00.0000000+01:00"; break;
            default:
                var duplicate = row.DeepClone().AsObject();
                duplicate["ordinal"] = 1;
                if (mutation == "duplicate_value") duplicate["coordinate"] = "another";
                rows.Add(duplicate);
                break;
        }
        Assert.Throws<FormatException>(() => SpiritualWoundReplayJournal.CreateReplay(rows.ToJsonString()));
    }

    /// <summary>
    /// Rejects duplicate JSON properties before a parse tree could discard them.
    /// </summary>
    [Fact]
    public void DuplicatePropertiesAreRejected()
    {
        var json = OneRow().Replace("\"ordinal\":0", "\"ordinal\":0,\"ordinal\":0", StringComparison.Ordinal);
        Assert.Throws<FormatException>(() => SpiritualWoundReplayJournal.CreateReplay(json));
    }

    /// <summary>
    /// Rejects repeated identity values and repeated causal keys while recording.
    /// </summary>
    [Fact]
    public void RecordingRejectsRepeatedKeysAndValues()
    {
        var keys = SpiritualWoundReplayJournal.CreateAppend(OneRow());
        keys.Request("effect", "effects", "exchange:1/root:0", ThrowGenerator);
        Assert.Throws<InvalidOperationException>(() => keys.Request("effect", "effects", "exchange:1/root:0", ThrowGenerator));
        Assert.Throws<InvalidOperationException>(() => keys.Export());
        var values = SpiritualWoundReplayJournal.CreateAppend(OneRow());
        values.Request("effect", "effects", "exchange:1/root:0", ThrowGenerator);
        Assert.Throws<FormatException>(() => values.Request("effect", "effects", "new", () => Effect));
        Assert.Throws<InvalidOperationException>(() => values.Export());
    }

    /// <summary>
    /// Builds a valid single retained allocation.
    /// </summary>
    /// <returns>
    /// Closed journal JSON.
    /// </returns>
    private static string OneRow() => "[{\"ordinal\":0,\"kind\":\"effect\",\"owner\":\"effects\",\"coordinate\":\"exchange:1/root:0\",\"value\":\"" + Effect + "\"}]";

    /// <summary>
    /// Makes accidental allocation during replay observable.
    /// </summary>
    /// <returns>
    /// Never returns a value.
    /// </returns>
    private static string ThrowGenerator() => throw new NotSupportedException("Generator must not run during replay.");
}
