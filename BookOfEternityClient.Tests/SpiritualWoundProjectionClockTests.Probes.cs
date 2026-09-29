using System.Text.Json.Nodes;
using BookOfEternityClient.Services;
using Xunit;

namespace BookOfEternityClient.Tests;

public sealed partial class SpiritualWoundProjectionClockTests
{
    /// <summary>
    /// Preserves the closure instant when its separately validated missing witness arrives.
    /// </summary>
    [Fact]
    public void ClosureWitnessCompletionRetainsTimeAndReplayCoordinate()
    {
        var journal = SpiritualWoundReplayJournal.CreateAppend("[]");
        var ordinary = new CountingClock();
        var clock = new SpiritualWoundProjectionClock(journal, ordinary);
        var first = Project("resolve", clock);
        var completed = Project("resolve", clock, terminalWitness: true);
        Assert.NotEqual(first, completed);
        Assert.Equal(ClosureTime(first), ClosureTime(completed));
        Assert.Equal(1, ordinary.Calls);
        var rows = journal.Export().ToJsonString();
        var replay = SpiritualWoundReplayJournal.CreateReplay(rows);
        var replayClock = new SpiritualWoundProjectionClock(replay, new CountingClock { Reject = true });
        Assert.Equal(first, Project("resolve", replayClock));
        Assert.Equal(completed, Project("resolve", replayClock, terminalWitness: true));
        Assert.Equal(rows, replay.Export().ToJsonString());
        Assert.Throws<InvalidOperationException>(() => Project("conflict", replayClock));
    }

    /// <summary>
    /// Keeps discarded source validation independent of later retained identity allocations.
    /// </summary>
    [Fact]
    public void ConflictProbeDoesNotConsumeStrictReplayOrReadClock()
    {
        const string identity = "effect_0123456789abcdef0123456789abcdef";
        var record = SpiritualWoundReplayJournal.CreateAppend("[]");
        record.Request("effect", "effects", "next/root", () => identity);
        var rows = record.Export().ToJsonString();
        var replay = SpiritualWoundReplayJournal.CreateReplay(rows);
        var clock = new SpiritualWoundProjectionClock(replay, new CountingClock { Reject = true });
        using (clock.BeginConflictValidationProbe(Array.Empty<JsonNode?>()))
        {
            var first = Project("resolve", clock);
            Assert.Equal(first, Project("resolve", clock));
            Assert.Throws<InvalidOperationException>(() => replay.Export());
        }
        Assert.Equal(identity, replay.Request("effect", "effects", "next/root",
            () => throw new NotSupportedException("Replay allocated an identity.")));
        Assert.Equal(rows, replay.Export().ToJsonString());
    }

    /// <summary>
    /// Avoids equality with retained explicit timestamps and removes provisional memo after the probe.
    /// </summary>
    [Fact]
    public void ConflictProbeAvoidsRetainedTimesAndCannotBecomeAcceptedTime()
    {
        var journal = SpiritualWoundReplayJournal.CreateAppend("[]");
        var ordinary = new CountingClock();
        var clock = new SpiritualWoundProjectionClock(journal, ordinary);
        var excluded = new JsonObject { ["recentConflicts"] = new JsonArray(
            new JsonObject { ["resolvedAtUtc"] = "0001-01-01T00:00:00.0000000Z" },
            new JsonObject { ["resolvedAtUtc"] = "0001-01-01T00:00:00.0000001Z" }) };
        string provisional;
        using (clock.BeginConflictValidationProbe(new JsonNode?[] { excluded }))
        {
            excluded.Clear();
            provisional = ClosureTime(Project("resolve", clock));
            Assert.Equal("0001-01-01T00:00:00.0000002Z", provisional);
        }
        Assert.Empty(journal.Export());
        Assert.Equal(0, ordinary.Calls);
        Assert.NotEqual(provisional, ClosureTime(Project("resolve", clock)));
        Assert.Equal(1, ordinary.Calls);
        Assert.Single(journal.Export());
    }

    /// <summary>
    /// Reuses an accepted memo entry while discarding only previously unknown probe values.
    /// </summary>
    [Fact]
    public void ConflictProbePreservesAcceptedMemoAndRepeatedCleanup()
    {
        var journal = SpiritualWoundReplayJournal.CreateAppend("[]");
        var clock = new SpiritualWoundProjectionClock(journal, new CountingClock());
        var accepted = Project("resolve", clock);
        var rows = journal.Export().ToJsonString();
        var probe = clock.BeginConflictValidationProbe(Array.Empty<JsonNode?>());
        Assert.Equal(accepted, Project("resolve", clock));
        Project("conflict", clock);
        probe.Dispose();
        using (var scope = clock.BeginSpeculation())
        {
            probe.Dispose();
            Assert.Equal(accepted, Project("resolve", clock));
            scope.Commit();
        }
        Assert.Equal(rows, journal.Export().ToJsonString());
    }

    /// <summary>
    /// Rejects using the discarded conflict probe for another allocation owner.
    /// </summary>
    [Fact]
    public void ConflictProbeRejectsOtherProjectionRoles()
    {
        var journal = SpiritualWoundReplayJournal.CreateAppend("[]");
        var clock = new SpiritualWoundProjectionClock(journal, new CountingClock());
        using (clock.BeginConflictValidationProbe(Array.Empty<JsonNode?>()))
            Assert.Throws<InvalidOperationException>(() => Project("legacy", clock));
        Assert.Throws<InvalidOperationException>(() => journal.Export());
    }

    /// <summary>
    /// Reads the generated timestamp from the actual conflict owner's projected closure.
    /// </summary>
    /// <param name="json">
    /// Complete projected conflict JSON containing one recent closure.
    /// </param>
    /// <returns>
    /// Exact stored closure timestamp text.
    /// </returns>
    private static string ClosureTime(string json) =>
        JsonNode.Parse(json)!["recentConflicts"]!.AsArray().Last()!["resolvedAtUtc"]!.GetValue<string>();

    /// <summary>
    /// Retains allocations made by evidence enumeration before the discarded probe begins.
    /// </summary>
    [Fact]
    public void ConflictProbeSnapshotsAfterEvidenceEnumeration()
    {
        var journal = SpiritualWoundReplayJournal.CreateAppend("[]");
        var ordinary = new CountingClock();
        var clock = new SpiritualWoundProjectionClock(journal, ordinary);
        string? accepted = null;
        IEnumerable<JsonNode?> Evidence()
        {
            accepted = Project("resolve", clock);
            yield return JsonNode.Parse(accepted);
        }
        using (clock.BeginConflictValidationProbe(Evidence()))
            Project("conflict", clock);
        Assert.Equal(accepted, Project("resolve", clock));
        Assert.Equal(1, ordinary.Calls);
        Assert.Single(journal.Export());
    }
}
