using System.Text.Json;
using Xunit;

namespace BookOfEternityClient.Tests;

public sealed class GmWorkerRestartDispatchTests
{
    [Theory]
    [InlineData("exact")]
    [InlineData("changed")]
    [InlineData("new")]
    public async Task ColdRetired_OnlyDistinctTaskMayRecoverAndDispatch(string mode)
    {
        var fixture = await GmWorkerRestartAdmissionTests.RestartFixture.Create();
        await fixture.Run("restart-retired-seed", 0);
        using (var seed = JsonDocument.Parse(File.ReadAllBytes(Path.Combine(fixture.Output, "restart-result.json"))))
        {
            Assert.True(seed.RootElement.GetProperty("actualSuccess").GetBoolean());
            Assert.Equal(1, seed.RootElement.GetProperty("retiredEntries").GetInt32());
        }
        await fixture.Run("restart-retired-" + mode, 0);
        using var report = JsonDocument.Parse(File.ReadAllBytes(Path.Combine(fixture.Output, "restart-result.json")));
        var r = report.RootElement;
        Assert.Equal(0, r.GetProperty("reaperCapacity").GetInt32());
        Assert.True(r.GetProperty("workspaceCleaned").GetBoolean());
        if (mode == "new")
        {
            Assert.True(r.GetProperty("actualSuccess").GetBoolean() && r.GetProperty("contentImportedExactly").GetBoolean() &&
                r.GetProperty("originalRunBound").GetBoolean() && r.GetProperty("originalTaskBound").GetBoolean(), r.ToString());
            Assert.Equal(1, r.GetProperty("boundOwners").GetInt32());
            Assert.Equal(1, r.GetProperty("releases").GetInt32());
            Assert.Equal(2, r.GetProperty("workerStarts").GetInt32());
            Assert.Equal(2, r.GetProperty("retiredEntries").GetInt32());
            Assert.True(r.GetProperty("recoveryObservations").GetInt32() > 0);
        }
        else
        {
            Assert.True(r.GetProperty("preservedRoot").GetBoolean() && r.GetProperty("recoveryObservations").GetInt32() == 0 &&
                r.GetProperty("slotWaits").GetInt32() == 0 && r.GetProperty("reservationCalls").GetInt32() == 0 &&
                r.GetProperty("boundOwners").GetInt32() == 0 && r.GetProperty("releases").GetInt32() == 0 &&
                !r.GetProperty("actualSuccess").GetBoolean(), "Retired task identity must refuse before recovery and capacity, even with changed body: " + r);
            Assert.Equal(1, r.GetProperty("workerStarts").GetInt32());
        }
    }

    [Theory]
    [InlineData("task")]
    [InlineData("generation")]
    public async Task GatedHost_ChangedOriginalBindingRefusesRelease(string mode)
    {
        var fixture = await GmWorkerRestartAdmissionTests.RestartFixture.Create();
        await fixture.Run("restart-release-" + mode, 0);
        using var report = JsonDocument.Parse(File.ReadAllBytes(Path.Combine(fixture.Output, "restart-result.json")));
        var r = report.RootElement;
        Assert.Equal(1, r.GetProperty("boundOwners").GetInt32());
        Assert.Equal(1, r.GetProperty("releases").GetInt32());
        Assert.True(r.GetProperty("workerStarts").GetInt32() == 0 && !r.GetProperty("actualSuccess").GetBoolean() &&
            !r.GetProperty("proposalPublished").GetBoolean(), "A changed task/generation must refuse while the host is still gated: " + r);
        Assert.Equal(0, r.GetProperty("reaperCapacity").GetInt32());
        Assert.True(r.GetProperty("workspaceCleaned").GetBoolean());
    }
}
