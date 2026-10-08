using System.Text.Json;
using Xunit;

namespace BookOfEternityClient.Tests;

public sealed class GmWorkerRestartLaunchTests
{
    [Theory]
    [InlineData("busy")]
    [InlineData("omitted")]
    [InlineData("foreign")]
    public async Task OriginalLaunch_RefusedIntentRetainsNeverStartAndHostRequiresOwnRelease(string mode)
    {
        var fixture = await GmWorkerRestartAdmissionTests.RestartFixture.Create();
        await fixture.Run("restart-launch-" + mode, 0);
        using var report = JsonDocument.Parse(File.ReadAllBytes(Path.Combine(fixture.Output, "restart-launch.json")));
        var r = report.RootElement; var b = r.GetProperty("afterB");
        if (mode == "busy")
        {
            Assert.True(r.GetProperty("faults").GetInt32() > 0, r.ToString());
            Assert.Equal(0, r.GetProperty("bOwners").GetInt32());
            Assert.Contains("Another original worker metadata plan is pending", r.GetProperty("beforeB").GetProperty("lastError").GetString());
            Assert.True(r.GetProperty("bNeverStartedRetired").GetBoolean(), r.ToString());
            Assert.Equal(2, r.GetProperty("distinctEpochs").GetInt32());
        }
        else Assert.True(r.GetProperty("refused").GetBoolean(), "A fresh host must reject missing/foreign original execution before writing Release: " + r);
        Assert.Equal(mode == "busy" ? 1 : 0, r.GetProperty("workerStarts").GetInt32());
        Assert.Equal(0, r.GetProperty("releaseFrames").GetInt32());
        Assert.Equal(0, r.GetProperty("afterA").GetProperty("capacity").GetInt32());
        Assert.Equal(0, b.GetProperty("capacity").GetInt32());
        Assert.Equal(0, b.GetProperty("entries").GetInt32());
        Assert.Equal(mode == "omitted" ? 1 : 2, r.GetProperty("retired").GetInt32());
        foreach (var result in new[] { r.GetProperty("afterA"), b })
            foreach (var key in new[] { "accepted", "proposalConsumer", "repairConsumer" }) Assert.False(result.GetProperty(key).GetBoolean());
        Assert.False(r.GetProperty("detachedRemaining").GetBoolean());
    }
}
