using System.Text.Json;
using Xunit;

namespace BookOfEternityClient.Tests;

public sealed class GmWorkerRestartBoundaryTests
{
    [Theory]
    [InlineData("partial-workspace")]
    [InlineData("publication-ack")]
    [InlineData("retirement-ack")]
    [InlineData("retirement-authority-loss")]
    [InlineData("prepared-registration-loss")]
    public async Task OriginalAuthorityAndCapacity_SurviveUnresolvedBoundary(string mode)
    {
        var fixture = await GmWorkerRestartAdmissionTests.RestartFixture.Create();
        await fixture.Run("restart-boundary-" + mode, 0);
        using var document = JsonDocument.Parse(File.ReadAllBytes(Path.Combine(fixture.Output, "restart-boundary.json")));
        var r = document.RootElement; var before = r.GetProperty("before"); var after = r.GetProperty("after");
        Assert.True(before.GetProperty("entries").GetInt32() == 1 && before.GetProperty("capacity").GetInt32() == 1,
            "Original unresolved execution must retain capacity: " + r);
        Assert.True(r.GetProperty("lockRetainedAfterClientDispose").GetBoolean(), r.ToString());
        Assert.True(r.GetProperty("canonicalRefused").GetBoolean(), r.ToString());
        Assert.Equal(0, r.GetProperty("probeReservations").GetInt32());
        var accepted = mode == "retirement-ack";
        foreach (var key in new[] { "accepted", "proposalConsumer", "repairConsumer" })
        {
            Assert.True(before.GetProperty(key).GetBoolean() == accepted, "Original consumer permit at failure: " + r);
            Assert.True(after.GetProperty(key).GetBoolean() == accepted, "Retry must not manufacture or restore a success permit: " + r);
        }
        var authorityLost = mode.EndsWith("authority-loss", StringComparison.Ordinal) || mode == "prepared-registration-loss";
        Assert.Equal(authorityLost ? 1 : 0, after.GetProperty("entries").GetInt32());
        Assert.Equal(authorityLost ? 1 : 0, after.GetProperty("capacity").GetInt32());
        Assert.Equal(mode is "partial-workspace" or "prepared-registration-loss" ? 0 : 1, r.GetProperty("workerStarts").GetInt32());
        if (mode == "retirement-ack")
        {
            Assert.True(r.GetProperty("exactTerminalRetry").GetBoolean(), r.ToString());
            Assert.False(r.GetProperty("terminal").GetProperty("Progress").GetProperty("Cleanup").GetProperty("RequiredAudit").GetBoolean());
        }
        if (mode == "partial-workspace")
        {
            Assert.Equal("AbortedBeforeLaunch", r.GetProperty("terminalPhase").GetString());
            Assert.True(r.GetProperty("terminal").GetProperty("Progress").GetProperty("Cleanup").GetProperty("RequiredAudit").GetBoolean());
        }
        if (mode is "publication-ack" or "retirement-ack" or "retirement-authority-loss")
            Assert.True(r.GetProperty("proposalPreserved").GetBoolean());
    }
}
