using System.Text.Json;
using System.Text.Json.Nodes;
using Xunit;

namespace BookOfEternityClient.Tests;

public sealed partial class GmWorkerRestartFenceTests
{
    [Theory]
    [InlineData("parallel-lower-limit")]
    [InlineData("parallel-corrupt")]
    [InlineData("runtime-missing")]
    [InlineData("journal-conflict")]
    public async Task ColdInventory_FencesWholeRootAndOriginalJournalConflict(string mode)
    {
        var fixture = await GmWorkerRestartAdmissionTests.RestartFixture.Create();
        await fixture.Run("restart-fence-seed-inventory-" + mode, 77, allowGuardianCleanup: true);
        using var cut = JsonDocument.Parse(File.ReadAllBytes(Path.Combine(fixture.Output, "fence-inventory-cut.json")));
        var r = cut.RootElement;
        var journal = mode == "journal-conflict";
        var parallel = mode.StartsWith("parallel", StringComparison.Ordinal);
        Assert.Equal(1, r.GetProperty("cuts").GetInt32());
        Assert.Equal(parallel ? 2 : 1, r.GetProperty("ownedEntries").GetInt32());
        Assert.Equal(journal ? 0 : parallel ? 2 : 1, r.GetProperty("active").GetInt32());
        Assert.Equal(journal ? 1 : 0, r.GetProperty("retired").GetInt32());
        Assert.Equal(journal ? 1 : 0, r.GetProperty("workerStarts").GetInt32());
        Assert.Equal(journal, r.GetProperty("originalAccepted").GetBoolean());
        Assert.True(r.GetProperty("exactOriginalBindings").GetBoolean());
        Assert.Equal(1, r.GetProperty("coldLimit").GetInt32());
        Assert.Equal(parallel ? 2 : 1, r.GetProperty("sourceLimit").GetInt32());
        if (!journal) Assert.All(r.GetProperty("phases").EnumerateArray(), phase => Assert.Equal("LaunchIntent", phase.GetString()));
        else
        {
            Assert.Equal("Committed", r.GetProperty("phase").GetString());
            Assert.True(r.GetProperty("journalExists").GetBoolean());
            Assert.Equal(0, r.GetProperty("capacity").GetInt32());
        }
        // Guardian ECHILD precedes every owned fixture mutation and cold process.
        var root = Path.Combine(fixture.Output, "state-copy");
        var statePath = Path.Combine(root, ".boe_runtime", "worker-runs-v1", "state.json");
        if (mode == "parallel-corrupt")
        {
            var state = JsonNode.Parse(File.ReadAllBytes(statePath))!;
            state["Entries"]![1]!["Identity"]!["TaskSha256"] = "invalid-owned-fixture-digest";
            File.WriteAllText(statePath, state.ToJsonString());
        }
        if (mode == "runtime-missing")
            Directory.Move(Path.Combine(root, "native-runtime"), Path.Combine(root, "native-runtime-moved"));
        if (journal)
            File.WriteAllText(Path.Combine(root, "game_session", "game_state", "world", "weather.json"), "{\"fixture\":\"third-conflicting-bytes\"}");
        await fixture.Run("restart-fence-probe-denied", 0);
        using var observed = JsonDocument.Parse(File.ReadAllBytes(Path.Combine(fixture.Output, "fence-probe.json")));
        var p = observed.RootElement;
        Assert.True(p.GetProperty("preserved").GetBoolean(), p.ToString());
        Assert.Equal(mode == "parallel-corrupt" ? "Blocked" : journal ? "Quiescent" : "Uncertain", p.GetProperty("kind").GetString());
        Assert.Equal(mode == "parallel-corrupt" || journal ? 0 : parallel ? 2 : 1, p.GetProperty("activeReservations").GetInt32());
        foreach (var key in new[] { "recoveries", "slots", "reservations", "owners", "releases", "capacity", "entries" }) Assert.Equal(0, p.GetProperty(key).GetInt32());
        foreach (var key in new[] { "accepted", "proposalConsumer", "repairConsumer" }) Assert.False(p.GetProperty(key).GetBoolean());
        Assert.Equal(journal ? 1 : 0, p.GetProperty("workerStarts").GetInt32());
        if (journal) Assert.Contains("publication member contains unknown bytes", p.GetProperty("error").GetString());
        if (mode == "runtime-missing") Assert.False(Directory.Exists(Path.Combine(root, "native-runtime")));
    }
}
