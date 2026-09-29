using System.Text.Json.Nodes;
using BookOfEternityClient.Services;
using Xunit;

namespace BookOfEternityClient.Tests;

/// <summary>
/// Checks retained client times through actual soul, archive and conflict projection owners.
/// </summary>
public sealed partial class SpiritualWoundProjectionClockTests
{
    /// <summary>
    /// Repeats a pure projection and then reconstructs it with a fresh clock without reading current time.
    /// </summary>
    /// <param name="owner">
    /// Actual projection owner exercised by the fixture.
    /// </param>
    [Theory]
    [InlineData("legacy")]
    [InlineData("archive")]
    [InlineData("conflict")]
    [InlineData("resolve")]
    public void ActualProjectionReplaysAndMemoizesTime(string owner)
    {
        var journal = SpiritualWoundReplayJournal.CreateAppend("[]");
        var ordinary = new CountingClock();
        var clock = new SpiritualWoundProjectionClock(journal, ordinary);
        var first = Project(owner, clock);
        Assert.Contains("2026-09-22T08:00:01.0000000Z", first);
        Assert.Equal(first, Project(owner, clock));
        Assert.Equal(1, ordinary.Calls);
        Assert.Single(journal.Export());
        var rows = journal.Export().ToJsonString();
        var replay = SpiritualWoundReplayJournal.CreateReplay(rows);
        var replayClock = new SpiritualWoundProjectionClock(replay, new CountingClock { Reject = true });
        Assert.Equal(first, Project(owner, replayClock));
        Assert.Equal(first, Project(owner, replayClock));
        Assert.Equal(rows, replay.Export().ToJsonString());
    }

    /// <summary>
    /// Does not synthesize timestamps when the actual owners already received explicit ones.
    /// </summary>
    /// <param name="owner">
    /// Archive or conflict owner supporting an authored timestamp.
    /// </param>
    [Theory]
    [InlineData("archive")]
    [InlineData("conflict")]
    [InlineData("resolve")]
    public void ExplicitTimestampDoesNotAllocate(string owner)
    {
        var journal = SpiritualWoundReplayJournal.CreateAppend("[]");
        var clock = new SpiritualWoundProjectionClock(journal, new CountingClock { Reject = true });
        Assert.Contains("2026-01-01T00:00:00Z", Project(owner, clock, explicitTime: true));
        Assert.Empty(journal.Export());
    }

    /// <summary>
    /// Prevents cached projection values from escaping a revoked attempt.
    /// </summary>
    [Fact]
    public void MemoHitAfterInvalidationFails()
    {
        var journal = SpiritualWoundReplayJournal.CreateAppend("[]");
        var clock = new SpiritualWoundProjectionClock(journal, new CountingClock());
        Project("legacy", clock);
        journal.Invalidate();
        Assert.Throws<InvalidOperationException>(() => Project("legacy", clock));
    }

    /// <summary>
    /// Shares one effective closure timestamp across projections with different unrelated root history.
    /// </summary>
    [Fact]
    public void ConflictClosureTimeDoesNotDependOnUnrelatedRootHistory()
    {
        var journal = SpiritualWoundReplayJournal.CreateAppend("[]");
        var ordinary = new CountingClock();
        var clock = new SpiritualWoundProjectionClock(journal, ordinary);
        var first = JsonNode.Parse(Project("resolve", clock))!;
        var second = JsonNode.Parse(Project("resolve", clock, unrelatedHistory: true))!;
        Assert.NotEqual(first.ToJsonString(), second.ToJsonString());
        Assert.Equal(first["recentConflicts"]!.AsArray().Last()!["resolvedAtUtc"]!.GetValue<string>(),
            second["recentConflicts"]!.AsArray().Last()!["resolvedAtUtc"]!.GetValue<string>());
        Assert.Equal(1, ordinary.Calls);
        Assert.Single(journal.Export());
    }

    /// <summary>
    /// Rejects changed causal evidence before an underlying clock can run during strict replay.
    /// </summary>
    [Fact]
    public void ChangedEvidenceRejectsReplay()
    {
        var journal = SpiritualWoundReplayJournal.CreateAppend("[]");
        new SpiritualWoundProjectionClock(journal, new CountingClock()).GetUtcNow(
            AcceptedTurnProjectionTimeKind.SurvivalConsumption, new JsonObject { ["turn"] = 42 });
        var replay = SpiritualWoundReplayJournal.CreateReplay(journal.Export().ToJsonString());
        var clock = new SpiritualWoundProjectionClock(replay, new CountingClock { Reject = true });
        Assert.Throws<InvalidOperationException>(() => clock.GetUtcNow(
            AcceptedTurnProjectionTimeKind.SurvivalConsumption, new JsonObject { ["turn"] = 43 }));
        Assert.Throws<InvalidOperationException>(() => replay.Export());
    }

    /// <summary>
    /// Insulates both the caller's evidence and the memo key from a mutating underlying callback.
    /// </summary>
    [Fact]
    public void CallbackCannotMutateEvidenceOrMemoKey()
    {
        var journal = SpiritualWoundReplayJournal.CreateAppend("[]");
        var ordinary = new CountingClock { Mutate = true };
        var clock = new SpiritualWoundProjectionClock(journal, ordinary);
        var evidence = new JsonObject { ["turn"] = 42 };
        var first = clock.GetUtcNow(AcceptedTurnProjectionTimeKind.SurvivalConsumption, evidence);
        Assert.Equal(42, evidence["turn"]!.GetValue<int>());
        Assert.Equal(first, clock.GetUtcNow(AcceptedTurnProjectionTimeKind.SurvivalConsumption, evidence));
        Assert.Equal(1, ordinary.Calls);
    }

    /// <summary>
    /// Runs a real owner from an unchanged detached fixture, without filesystem or publication authority.
    /// </summary>
    /// <param name="owner">
    /// Legacy grant, archive receipt or conflict closure.
    /// </param>
    /// <param name="clock">
    /// Attempt-owned clock passed explicitly to the projection owner.
    /// </param>
    /// <param name="explicitTime">
    /// Whether archive/conflict input already carries an exact timestamp.
    /// </param>
    /// <param name="unrelatedHistory">
    /// Whether the conflict carrier contains an unrelated prior closure.
    /// </param>
    /// <param name="terminalWitness">
    /// Whether the resolution includes its subsequently supplied terminal exchange witness.
    /// </param>
    /// <returns>
    /// Complete projected root JSON for byte comparison.
    /// </returns>
    private static string Project(string owner, AcceptedTurnProjectionClock clock, bool explicitTime = false,
        bool unrelatedHistory = false, bool terminalWitness = false)
    {
        if (owner == "legacy")
        {
            var root = JsonNode.Parse("""
                {"metaStateUpdates":{"memoryLegacyGrant":{"legacyId":"legacy_probe",
                "legacyType":"startingCharacteristicBonus","sourceLifeHint":"life_1",
                "characteristic":"strength","bonus":2}}}
                """)!.AsObject();
            return CanonicalStateNormalizer.BuildNormalizedSoulStateRoot(root, null, 42, false, true, clock).ToJsonString();
        }
        if (owner == "archive")
        {
            var root = JsonNode.Parse("""
                {"afterlifeArchive":{"stored":[],"actionReceipts":[{"requestId":"req_probe",
                "archiveId":"archive_probe","requestedMode":"consultation","status":"rejected"}]}}
                """)!.AsObject();
            var resolution = new JsonObject
            {
                ["requestId"] = "req_probe", ["archiveId"] = "archive_probe",
                ["requestedMode"] = "consultation", ["status"] = "rejected"
            };
            if (explicitTime) resolution["resolvedAtUtc"] = "2026-01-01T00:00:00Z";
            AfterlifeArchiveState.ApplyActionResolutions(root, new JsonArray(resolution), 42, clock);
            return root.ToJsonString();
        }
        var conflict = JsonNode.Parse("""
            {"schemaVersion":1,"activeConflict":{"conflictId":"conflict_probe",
            "dangerMode":"training","realm":"Chaos Sea","sideModel":"direct_duel",
            "status":"active","resolutionState":"active","exchangeLog":[],"combatConditions":[],
            "oppositionSide":{"leadContestant":{"actorType":"guardian","actorId":"guardian_probe"}}},
            "recentConflicts":[]}
            """)!.AsObject();
        var update = new JsonObject
        {
            ["mode"] = owner == "resolve" ? "resolve" : "repair_cancel",
            ["resolution"] = new JsonObject
            {
                ["resolvedAtTurn"] = 42, ["operationType"] = "guard",
                ["guardianId"] = "guardian_probe", ["playerOutcome"] = "won"
            }
        };
        if (unrelatedHistory)
            conflict["recentConflicts"]!.AsArray().Add(new JsonObject { ["conflictId"] = "prior_conflict" });
        if (explicitTime) update["resolution"]!["resolvedAtUtc"] = "2026-01-01T00:00:00Z";
        if (terminalWitness) update["resolution"]!["terminalExchange"] = new JsonObject { ["exchangeId"] = "terminal_probe" };
        var result = AfterlifeSpiritualConflictState.ApplyUpdate(conflict, update, clock);
        Assert.False(result.ContainsKey("lastInvalidUpdate"));
        return result.ToJsonString();
    }

    /// <summary>
    /// Makes clock invocation, callback mutation and forbidden replay reads observable.
    /// </summary>
    private sealed class CountingClock : AcceptedTurnProjectionClock
    {
        /// <summary>
        /// Gets the number of actual clock reads.
        /// </summary>
        internal int Calls { get; private set; }
        /// <summary>
        /// Gets or sets whether every read is forbidden.
        /// </summary>
        internal bool Reject { get; init; }
        /// <summary>
        /// Gets or sets whether the callback mutates the evidence it receives.
        /// </summary>
        internal bool Mutate { get; init; }
        /// <inheritdoc/>
        internal override DateTimeOffset GetUtcNow(AcceptedTurnProjectionTimeKind role, JsonObject evidence)
        {
            if (Reject) throw new NotSupportedException("Replay read the clock.");
            Calls++;
            if (Mutate) evidence.Clear();
            return new DateTimeOffset(2026, 9, 22, 8, 0, 0, TimeSpan.Zero).AddSeconds(Calls);
        }
    }
}
