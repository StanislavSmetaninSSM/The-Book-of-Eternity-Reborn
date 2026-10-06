using System.Text.Json;
using Xunit;

namespace BookOfEternityClient.Tests;

public sealed partial class GmWorkerRestartFenceTests
{
    [Theory]
    [InlineData("queued-generation")]
    [InlineData("held-generation")]
    [InlineData("held-owner-loss")]
    public async Task PublicationRace_PreservesUnresolvedWorkspaceAndAuthority(string mode)
    {
        var fixture = await GmWorkerRestartAdmissionTests.RestartFixture.Create();
        await fixture.Run("restart-fence-race-" + mode, 0);
        using var report = JsonDocument.Parse(File.ReadAllBytes(Path.Combine(fixture.Output, "fence-race.json")));
        var r = report.RootElement;
        var queued = mode == "queued-generation";
        Assert.Equal(1, r.GetProperty("publicationCalls").GetInt32());
        Assert.Equal(queued ? 0 : 1, r.GetProperty("mutationWaits").GetInt32());
        if (queued) Assert.True(r.GetProperty("contentionCalls").GetInt32() > 0);
        Assert.Equal(queued ? "StopValidated" : "PublicationIntent", r.GetProperty("heldPhase").GetString());
        Assert.True(r.GetProperty("taskBound").GetBoolean());
        Assert.True(r.GetProperty("originalWorkspaceFiles").GetInt32() > 0);
        Assert.Equal(1, r.GetProperty("workerStarts").GetInt32());
        foreach (var stage in new[] { "beforeRetry", "afterRetry" })
        {
            var p = r.GetProperty(stage);
            foreach (var key in new[] { "accepted", "proposalConsumer", "repairConsumer", "bundleExists", "inboxExists", "publicationAcknowledged" }) Assert.False(p.GetProperty(key).GetBoolean(), r.ToString());
            Assert.Equal(!queued, p.GetProperty("taskPreserved").GetBoolean());
            Assert.Equal(queued, p.GetProperty("taskAbsent").GetBoolean());
            if (queued) Assert.Equal(r.GetProperty("originalTaskHash").GetString(), p.GetProperty("retiredTaskHash").GetString());
            Assert.Equal("StoppedWithinScope", p.GetProperty("stop").GetString());
            Assert.True(p.GetProperty("outputsSettled").GetBoolean());
            foreach (var key in new[] { "capacity", "entries", "active" }) Assert.Equal(queued ? 0 : 1, p.GetProperty(key).GetInt32());
            Assert.Equal(queued ? 1 : 0, p.GetProperty("retired").GetInt32());
            Assert.Equal(!queued, p.GetProperty("workspaceExists").GetBoolean());
            Assert.Equal(!queued, p.GetProperty("workspaceBytesPreserved").GetBoolean());
            if (mode == "held-owner-loss") Assert.True(p.GetProperty("uncertain").GetBoolean());
            if (mode == "held-generation") Assert.Equal("PublicationIntent", p.GetProperty("phase").GetString());
        }
        if (!queued)
        {
            await fixture.Run("restart-fence-probe-denied", 0);
            using var probe = JsonDocument.Parse(File.ReadAllBytes(Path.Combine(fixture.Output, "fence-probe.json")));
            var p = probe.RootElement;
            Assert.True(p.GetProperty("preserved").GetBoolean(), p.ToString());
            foreach (var key in new[] { "recoveries", "slots", "reservations", "owners", "releases", "capacity", "entries" }) Assert.Equal(0, p.GetProperty(key).GetInt32());
            foreach (var key in new[] { "accepted", "proposalConsumer", "repairConsumer" }) Assert.False(p.GetProperty(key).GetBoolean());
        }
    }
}
