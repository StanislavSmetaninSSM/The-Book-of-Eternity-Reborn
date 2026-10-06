using System.Text.Json;
using Xunit;

namespace BookOfEternityClient.Tests;

public sealed class GmWorkerRestartCleanupTests
{
    [Theory]
    [InlineData("premature-retire")]
    [InlineData("r1-abort")]
    [InlineData("r1-start")]
    [InlineData("r1-live-retry")]
    [InlineData("native-loss")]
    public async Task OriginalCleanup_CannotBeSkippedOrReplaced(string mode)
    {
        var fixture = await GmWorkerRestartAdmissionTests.RestartFixture.Create();
        await fixture.Run(mode == "native-loss" ? "restart-root-uncertain-recovery" : "restart-cleanup-" + mode, 0);
        using var report = JsonDocument.Parse(File.ReadAllBytes(Path.Combine(fixture.Output,
            mode == "native-loss" ? "restart-root.json" : "restart-cleanup.json")));
        var r = report.RootElement;
        if (mode == "native-loss")
        {
            Assert.True(r.GetProperty("poolAttemptedStop").GetBoolean(), "Original cleanup must attempt bounded stop before refusing uncertain authority: " + r);
            Assert.Equal(1, r.GetProperty("afterA").GetProperty("capacity").GetInt32());
            Assert.Equal(1, r.GetProperty("afterB").GetProperty("capacity").GetInt32());
            Assert.True(r.GetProperty("recoveryEvidencePreserved").GetBoolean());
        }
        else Assert.True(r.GetProperty("refused").GetBoolean() && r.GetProperty("preserved").GetBoolean() &&
            r.GetProperty("workspaceRetained").GetBoolean(), "Only original completed cleanup may retire or retry its plan: " + r);
    }
}
