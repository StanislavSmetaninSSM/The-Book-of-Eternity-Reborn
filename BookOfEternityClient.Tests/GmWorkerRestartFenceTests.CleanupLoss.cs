using System.Text.Json;
using Xunit;

namespace BookOfEternityClient.Tests;

public sealed partial class GmWorkerRestartFenceTests
{
    [Theory]
    [InlineData("cleanup-await")]
    [InlineData("pending-ack")]
    public async Task CleanupAuthorityLoss_CannotDeleteOrLoseStickyUncertainty(string mode)
    {
        var fixture = await GmWorkerRestartAdmissionTests.RestartFixture.Create();
        await fixture.Run("restart-fence-cleanup-loss-" + mode, 0);
        using var report = JsonDocument.Parse(File.ReadAllBytes(Path.Combine(fixture.Output, "fence-cleanup-loss.json")));
        var r = report.RootElement;
        Assert.Equal(1, r.GetProperty("faults").GetInt32());
        Assert.Equal(1, r.GetProperty("workerStarts").GetInt32());
        Assert.Equal("Published", r.GetProperty("heldDisk").GetString());
        Assert.Equal(mode == "pending-ack" ? "PublicationIntent" : "Published", r.GetProperty("heldLive").GetString());
        Assert.Equal("StoppedWithinScope", r.GetProperty("heldStop").GetString());
        Assert.True(r.GetProperty("heldOutputs").GetBoolean());
        Assert.True(r.GetProperty("originalWorkspaceFiles").GetInt32() > 0);
        foreach (var stage in new[] { "beforeRetry", "afterRetry" })
        {
            var p = r.GetProperty(stage);
            foreach (var key in new[] { "accepted", "proposalConsumer", "repairConsumer", "outputsSettled" }) Assert.False(p.GetProperty(key).GetBoolean(), r.ToString());
            foreach (var key in new[] { "workspaceExists", "workspaceBytesPreserved", "taskPreserved", "uncertain", "bundleExists" }) Assert.True(p.GetProperty(key).GetBoolean(), r.ToString());
            foreach (var key in new[] { "capacity", "entries", "active" }) Assert.Equal(1, p.GetProperty(key).GetInt32());
            Assert.Equal(0, p.GetProperty("retired").GetInt32());
            Assert.Equal("Uncertain", p.GetProperty("stop").GetString());
            Assert.Equal(mode == "cleanup-await", p.GetProperty("inboxExists").GetBoolean());
        }
        await fixture.Run("restart-fence-probe-denied", 0);
        using var probe = JsonDocument.Parse(File.ReadAllBytes(Path.Combine(fixture.Output, "fence-probe.json")));
        var cold = probe.RootElement;
        Assert.True(cold.GetProperty("preserved").GetBoolean());
        foreach (var key in new[] { "recoveries", "slots", "reservations", "owners", "releases", "capacity", "entries" }) Assert.Equal(0, cold.GetProperty(key).GetInt32());
        foreach (var key in new[] { "accepted", "proposalConsumer", "repairConsumer" }) Assert.False(cold.GetProperty(key).GetBoolean());
    }
}
