using System.Text.Json;
using Xunit;

namespace BookOfEternityClient.Tests;

public sealed class GmWorkerRestartRootTests
{
    [Theory]
    [InlineData("foreign-pending")]
    [InlineData("uncertain-recovery")]
    public async Task IndependentOriginalEpochs_DoNotBorrowJournalOrRecoveryAuthority(string mode)
    {
        var fixture = await GmWorkerRestartAdmissionTests.RestartFixture.Create();
        await fixture.Run("restart-root-" + mode, 0);
        using var report = JsonDocument.Parse(File.ReadAllBytes(Path.Combine(fixture.Output, "restart-root.json")));
        var r = report.RootElement;
        var b = r.GetProperty("afterB");
        if (mode == "foreign-pending")
        {
            Assert.True(r.GetProperty("beforeB").GetProperty("accepted").GetBoolean() && b.GetProperty("accepted").GetBoolean(),
                "Another original entry's pending journal must not make healthy B absorbing Uncertain: " + r);
            Assert.Equal(0, r.GetProperty("afterA").GetProperty("capacity").GetInt32());
            Assert.Equal(0, b.GetProperty("capacity").GetInt32());
            Assert.False(r.GetProperty("afterA").GetProperty("accepted").GetBoolean());
            Assert.True(r.GetProperty("distinctOriginalEpochs").GetBoolean(), r.ToString());
            Assert.Equal(2, r.GetProperty("retired").GetInt32());
            Assert.Equal(2, r.GetProperty("workerStarts").GetInt32());
        }
        else
        {
            Assert.True(r.GetProperty("seeded").GetBoolean(), r.ToString());
            Assert.True(r.GetProperty("recoveryWrites").GetInt32() == 0 && r.GetProperty("recoveryEvidencePreserved").GetBoolean(),
                "B's exact cleanup audit token cannot recover an unrelated canonical decision while A is Uncertain: " + r);
            Assert.Equal(1, r.GetProperty("afterA").GetProperty("capacity").GetInt32());
            Assert.Equal(1, b.GetProperty("capacity").GetInt32());
            Assert.True(b.GetProperty("accepted").GetBoolean());
            Assert.Equal(1, r.GetProperty("workerStarts").GetInt32());
        }
    }
}
