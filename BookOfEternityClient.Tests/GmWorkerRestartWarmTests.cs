using System.Text.Json;
using Xunit;

namespace BookOfEternityClient.Tests;

public sealed class GmWorkerRestartWarmTests
{
    [Fact]
    public async Task DetectedNativeLoss_ClosesSharedRootBeforeStopAndSurvivesFailedJournalAndClientDisposal()
    {
        var fixture = await GmWorkerRestartAdmissionTests.RestartFixture.Create();
        await fixture.Run("restart-warm", 0);
        using var report = JsonDocument.Parse(File.ReadAllBytes(Path.Combine(fixture.Output, "restart-warm.json")));
        var r = report.RootElement;
        foreach (var key in new[] { "beforeLateStop", "canonicalRefused", "preservedBeforeStop", "lockRetained", "absorbingUncertain", "recoveryPreserved", "poolAttemptedStop" })
            Assert.True(r.GetProperty(key).GetBoolean(), key + ": " + r);
        Assert.Equal(0, r.GetProperty("slots").GetInt32());
        Assert.Equal(0, r.GetProperty("reservations").GetInt32());
        Assert.Equal(0, r.GetProperty("workerStarts").GetInt32());
        Assert.True(r.GetProperty("faults").GetInt32() > 0, r.ToString());
        foreach (var phase in new[] { "beforeRetry", "afterRetry" })
        {
            var snapshot = r.GetProperty(phase);
            Assert.Equal(1, snapshot.GetProperty("capacity").GetInt32());
            Assert.Equal(1, snapshot.GetProperty("entries").GetInt32());
            Assert.True(snapshot.GetProperty("workspaceExists").GetBoolean(), r.ToString());
            foreach (var key in new[] { "accepted", "proposalConsumer", "repairConsumer" }) Assert.False(snapshot.GetProperty(key).GetBoolean());
        }
    }
}
