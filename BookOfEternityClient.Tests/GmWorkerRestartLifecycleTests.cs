using System.Text.Json;
using Xunit;

namespace BookOfEternityClient.Tests;

public sealed class GmWorkerRestartLifecycleTests
{
    [Fact]
    public async Task ActualContentPool_PublishesAndCommitsBoundRetirementBeforeCapacityRelease()
    {
        var fixture = await GmWorkerRestartAdmissionTests.RestartFixture.Create();
        await fixture.Run("restart-happy-run", 0);
        using var report = JsonDocument.Parse(File.ReadAllBytes(Path.Combine(fixture.Output, "restart-result.json")));
        var r = report.RootElement;
        Assert.True(r.GetProperty("actualSuccess").GetBoolean(), r.ToString());
        Assert.True(r.GetProperty("contentImportedExactly").GetBoolean());
        Assert.True(r.GetProperty("workspaceCleaned").GetBoolean());
        Assert.Equal(1, r.GetProperty("workerStarts").GetInt32());
        Assert.Equal(1, r.GetProperty("releases").GetInt32());
        Assert.Equal(1, r.GetProperty("publicationCalls").GetInt32());
        Assert.Equal(0, r.GetProperty("reaperEntries").GetInt32());
        Assert.Equal(0, r.GetProperty("reaperCapacity").GetInt32());
        Assert.True(r.GetProperty("ledgerKind").GetString() == "Quiescent" &&
            r.GetProperty("activeEntries").ValueKind == JsonValueKind.Number && r.GetProperty("activeEntries").GetInt32() == 0 &&
            r.GetProperty("retiredEntries").ValueKind == JsonValueKind.Number && r.GetProperty("retiredEntries").GetInt32() == 1 &&
            r.GetProperty("retiredPhase").GetString() == "Retired" &&
            r.GetProperty("originalRunBound").GetBoolean() && r.GetProperty("originalTaskBound").GetBoolean() &&
            r.GetProperty("publicationCommitted").GetBoolean() && r.GetProperty("normalCleanupWithoutAudit").GetBoolean(),
            "Successful actual execution must acknowledge its exact durable retirement: " + r);
    }
}
