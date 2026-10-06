using System.Text.Json;
using Xunit;

namespace BookOfEternityClient.Tests;

public sealed class GmWorkerRestartAuditTests
{
    [Theory]
    [InlineData("exact-once")]
    [InlineData("wrong-path")]
    [InlineData("wrong-bytes")]
    [InlineData("replace-audit")]
    [InlineData("extra-member")]
    [InlineData("empty-prune")]
    [InlineData("direct-publish")]
    [InlineData("legacy-append")]
    [InlineData("held-closure")]
    public async Task OriginalCleanupPurpose_OnlyAppendsItsExactAuditOnce(string mode)
    {
        var fixture = await GmWorkerRestartAdmissionTests.RestartFixture.Create();
        await fixture.Run("restart-audit-" + mode, 0);
        using var report = JsonDocument.Parse(File.ReadAllBytes(Path.Combine(fixture.Output, "restart-audit.json")));
        var r = report.RootElement;
        Assert.True(r.GetProperty("retained").GetBoolean(), r.ToString());
        if (mode == "exact-once")
            Assert.True(!r.GetProperty("refused").GetBoolean() && r.GetProperty("appendedTwice").GetBoolean() &&
                r.GetProperty("exactOnce").GetBoolean(), r.ToString());
        else
            Assert.True(r.GetProperty("refused").GetBoolean() && r.GetProperty("preserved").GetBoolean(),
                "Original cleanup purpose must refuse before unrelated side effects: " + r);
    }
}
