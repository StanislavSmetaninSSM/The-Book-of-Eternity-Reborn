using System.Text.Json;
using Xunit;

namespace BookOfEternityClient.Tests;

public sealed class GmWorkerRestartOwnershipTests
{
    [Theory]
    [InlineData("durable-durable")]
    [InlineData("durable-legacy")]
    [InlineData("legacy-durable")]
    [InlineData("legacy-legacy")]
    public async Task ActualPoolOwner_ExcludesOtherProcessThroughClientDisposalAndQuarantine(string mode)
    {
        var fixture = await GmWorkerRestartAdmissionTests.RestartFixture.Create();
        await fixture.Run("restart-ownership-" + mode, 0);
        foreach (var stage in new[] { "active", "retained" })
        {
            using var contender = JsonDocument.Parse(File.ReadAllBytes(Path.Combine(fixture.Output, "contender-" + mode.Split('-')[1] + "-" + stage + ".json")));
            var c = contender.RootElement;
            Assert.True(c.GetProperty("slots").GetInt32() == 0 && c.GetProperty("reservations").GetInt32() == 0 &&
                c.GetProperty("owners").GetInt32() == 0 && c.GetProperty("capacity").GetInt32() == 0, c.ToString());
            Assert.False(c.GetProperty("success").GetBoolean());
        }
        using var report = JsonDocument.Parse(File.ReadAllBytes(Path.Combine(fixture.Output, "restart-ownership.json")));
        var r = report.RootElement;
        Assert.True(r.GetProperty("activePreserved").GetBoolean() && r.GetProperty("retainedPreserved").GetBoolean(), r.ToString());
        Assert.Equal(1, r.GetProperty("retainedCapacity").GetInt32());
        Assert.Equal(0, r.GetProperty("finalCapacity").GetInt32());
        Assert.Equal(0, r.GetProperty("finalEntries").GetInt32());
        Assert.True(r.GetProperty("cleanupCalls").GetInt32() >= 2, r.ToString());
        Assert.False(r.GetProperty("detachedRemaining").GetBoolean());
        Assert.True(r.GetProperty("otherSuccess").GetBoolean());
        Assert.Equal(0, r.GetProperty("otherCapacity").GetInt32());
    }
    [Fact]
    public async Task FirstAttachRace_OnlyOneModeMayEnterAndPublish()
    {
        var fixture = await GmWorkerRestartAdmissionTests.RestartFixture.Create();
        await fixture.Run("restart-ownership-race", 0);
        using var durable = JsonDocument.Parse(File.ReadAllBytes(Path.Combine(fixture.Output, "contender-durable-race.json")));
        using var legacy = JsonDocument.Parse(File.ReadAllBytes(Path.Combine(fixture.Output, "contender-legacy-race.json")));
        var rows = new[] { durable.RootElement, legacy.RootElement };
        Assert.Single(rows.Where(x => x.GetProperty("success").GetBoolean()));
        var refused = Assert.Single(rows.Where(x => !x.GetProperty("success").GetBoolean()));
        Assert.Equal(0, refused.GetProperty("slots").GetInt32());
        Assert.Equal(1, rows.Sum(x => x.GetProperty("owners").GetInt32()));
        Assert.All(rows, x => Assert.Equal(0, x.GetProperty("capacity").GetInt32()));
        Assert.Single(File.ReadAllLines(Path.Combine(fixture.Output, "worker-starts")));
    }
}
