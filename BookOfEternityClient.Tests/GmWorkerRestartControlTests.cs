using System.Text.Json;
using Xunit;

namespace BookOfEternityClient.Tests;

public sealed class GmWorkerRestartControlTests
{
    [Theory]
    [InlineData("released-ack")]
    [InlineData("cancel")]
    public async Task OriginalRelease_OneSendAndNoLateSuccessAfterAckLossOrCancellation(string mode)
    {
        var fixture = await GmWorkerRestartAdmissionTests.RestartFixture.Create();
        await fixture.Run("restart-control-" + mode, 0);
        using var report = JsonDocument.Parse(File.ReadAllBytes(Path.Combine(fixture.Output, "restart-control.json")));
        var r = report.RootElement; var before = r.GetProperty("before"); var after = r.GetProperty("after");
        foreach (var key in new[] { "accepted", "proposalConsumer", "repairConsumer" })
        { Assert.False(before.GetProperty(key).GetBoolean()); Assert.False(after.GetProperty(key).GetBoolean()); }
        var sent = mode == "released-ack";
        Assert.Equal(sent ? 1 : 0, r.GetProperty("writes").GetInt32());
        Assert.Equal(sent ? 1 : 0, r.GetProperty("releaseFrames").GetInt32());
        Assert.Equal(sent ? 1 : 0, r.GetProperty("workerStarts").GetInt32());
        Assert.Equal(0, r.GetProperty("pendingRepeats").GetInt32());
        Assert.Equal(sent ? 1 : 0, before.GetProperty("capacity").GetInt32());
        Assert.Equal(0, after.GetProperty("capacity").GetInt32());
        Assert.Equal(0, after.GetProperty("entries").GetInt32());
        Assert.False(r.GetProperty("detachedRemaining").GetBoolean());
        if (sent) Assert.True(r.GetProperty("faults").GetInt32() > 0 && r.GetProperty("repeatedCalls").GetInt32() > 0, r.ToString());
    }
}
