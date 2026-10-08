using System.Text.Json;
using Xunit;

namespace BookOfEternityClient.Tests;

public sealed class GmWorkerRestartWitnessTests
{
    [Theory]
    [InlineData("forged")]
    [InlineData("foreign")]
    public async Task TerminalCleanup_RequiresOriginalRetainedCompletedInstance(string mode)
    {
        var fixture = await GmWorkerRestartAdmissionTests.RestartFixture.Create();
        await fixture.Run("restart-witness-" + mode, 0);
        using var report = JsonDocument.Parse(File.ReadAllBytes(Path.Combine(fixture.Output, "restart-witness.json")));
        var r = report.RootElement;
        Assert.True(r.GetProperty("refused").GetBoolean() && r.GetProperty("preserved").GetBoolean(),
            "Foreign or fabricated completion must retain original state: " + r);
        Assert.Equal(mode == "foreign", r.GetProperty("workspaceRetained").GetBoolean());
        if (mode == "forged") Assert.True(r.GetProperty("terminalFaults").GetInt32() > 0);
        Assert.Equal(0, r.GetProperty("Disposals").GetInt32());
        Assert.False(r.GetProperty("RetirementAcknowledged").GetBoolean());
        if (mode == "foreign") Assert.True(r.GetProperty("foreignCompleted").GetBoolean());
    }
}
