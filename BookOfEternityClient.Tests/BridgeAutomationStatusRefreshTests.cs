using System.Diagnostics;
using System.Reflection;
using BookOfEternityGMBridge;
using Xunit;

namespace BookOfEternityClient.Tests;

public sealed class BridgeAutomationStatusRefreshTests
{
    [Fact]
    public async Task IdenticalPollsCoalesceButStatusChangeAndNextHeartbeatStillQueue()
    {
        using var host = new BridgeHost(Path.Combine(Path.GetTempPath(), "boe-status-poll", "game_session"), "unused");
        const BindingFlags fields = BindingFlags.Instance | BindingFlags.NonPublic;
        var refresh = typeof(BridgeHost).GetMethod("RefreshBridgeAutomationStateAsync", fields)!;
        var status = (BridgeStatus)typeof(BridgeHost).GetField("_status", fields)!.GetValue(host)!;
        Task Poll() => (Task)refresh.Invoke(host, null)!;

        await Poll();
        Assert.Equal("OperatorNotReady", status.State);
        var first = status.UpdatedAtUtc;
        for (var i = 0; i < 10; i++)
            await Poll();
        Assert.Equal(first, status.UpdatedAtUtc);

        // Actual change to terminal uncertainty reaches the same publication
        // entry even inside the refresh coalescing interval.
        typeof(BridgeHost).GetField("_terminalUncertain", fields)!.SetValue(host, true);
        await Poll();
        Assert.Equal("TerminalUncertain", status.State);
        Assert.False(status.Ready);
        var changed = status.UpdatedAtUtc;
        Assert.NotEqual(first, changed);
        await Poll();
        Assert.Equal(changed, status.UpdatedAtUtc);

        typeof(BridgeHost).GetField("_lastStatusQueueTimestamp", fields)!
            .SetValue(host, (long?)(Stopwatch.GetTimestamp() - 2 * Stopwatch.Frequency));
        await Poll();
        Assert.NotEqual(changed, status.UpdatedAtUtc);
    }
}
