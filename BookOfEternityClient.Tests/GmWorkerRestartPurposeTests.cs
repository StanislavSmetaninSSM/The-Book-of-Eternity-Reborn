using System.Text.Json;
using Xunit;

namespace BookOfEternityClient.Tests;

public sealed class GmWorkerRestartPurposeTests
{
    [Theory]
    [InlineData("cold")]
    [InlineData("reservation")]
    [InlineData("cleanup")]
    public async Task OriginalPurpose_RefusesForeignUnboundRootBeforeRecovery(string mode)
    {
        var fixture = await GmWorkerRestartAdmissionTests.RestartFixture.Create();
        await fixture.Run("restart-purpose-" + mode, 0);
        using var report = JsonDocument.Parse(File.ReadAllBytes(Path.Combine(fixture.Output, "restart-purpose.json")));
        var r = report.RootElement;
        Assert.True(r.GetProperty("refused").GetBoolean() && r.GetProperty("foreignPreserved").GetBoolean() &&
            r.GetProperty("sourcePreserved").GetBoolean(), "Original purpose cannot borrow a root without a context: " + r);
    }

    [Theory]
    [InlineData("own")]
    [InlineData("foreign")]
    [InlineData("wrong-purpose")]
    public async Task Release_RequiresOriginalTypedOperationLease(string mode)
    {
        var fixture = await GmWorkerRestartAdmissionTests.RestartFixture.Create();
        await fixture.Run("restart-release-lease-" + mode, 0);
        using var report = JsonDocument.Parse(File.ReadAllBytes(Path.Combine(fixture.Output, "restart-result.json")));
        var r = report.RootElement;
        Assert.True(r.GetProperty("releaseProbeRefused").GetBoolean() && r.GetProperty("releaseProbePreserved").GetBoolean(),
            "Untyped or wrong-purpose lease cannot persist a Release intent: " + r);
        Assert.Equal(0, r.GetProperty("workerStarts").GetInt32());
        Assert.Equal(0, r.GetProperty("reaperCapacity").GetInt32());
        Assert.True(r.GetProperty("workspaceCleaned").GetBoolean());
    }
}
