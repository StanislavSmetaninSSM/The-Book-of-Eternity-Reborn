using System.Text.Json;
using Xunit;

namespace BookOfEternityClient.Tests;

public sealed partial class GmWorkerRestartFenceTests
{
    [Theory]
    [InlineData("cleanup-pending", false, false)]
    [InlineData("archive", false, false)]
    [InlineData("terminal-disk", true, false)]
    [InlineData("terminal-ack", true, true)]
    [InlineData("capacity-released", true, true)]
    public async Task RetirementCrash_ExactTerminalCommitGatesNewTask(string cut, bool terminal, bool acknowledged)
    {
        var fixture = await GmWorkerRestartAdmissionTests.RestartFixture.Create();
        await fixture.Run("restart-fence-seed-retirement-" + cut, 77, allowGuardianCleanup: true);
        using var reached = JsonDocument.Parse(File.ReadAllBytes(Path.Combine(fixture.Output, "fence-cut.json")));
        var r = reached.RootElement;
        Assert.Equal(cut, r.GetProperty("cut").GetString());
        Assert.Equal(1, r.GetProperty("cutAttempts").GetInt32());
        Assert.Equal(1, r.GetProperty("failures").GetInt32());
        Assert.Equal(terminal ? 0 : 1, r.GetProperty("active").GetInt32());
        Assert.Equal(terminal ? 1 : 0, r.GetProperty("retired").GetInt32());
        Assert.Equal(acknowledged, r.GetProperty("retirementAcknowledged").GetBoolean());
        foreach (var key in new[] { "originalAccepted", "publicationAcknowledged", "CleanupDeferred", "auditMatches" }) Assert.True(r.GetProperty(key).GetBoolean(), r.ToString());
        Assert.Equal(1, r.GetProperty("auditCount").GetInt32());
        Assert.True(r.GetProperty("cleanup").GetProperty("RequiredAudit").GetBoolean());
        Assert.Equal(r.GetProperty("originalAudit").GetProperty("AuditEventId").GetString(), r.GetProperty("cleanup").GetProperty("AuditEventId").GetString());
        Assert.Equal(r.GetProperty("originalAudit").GetProperty("AuditSha256").GetString(), r.GetProperty("cleanup").GetProperty("AuditSha256").GetString());
        Assert.False(r.GetProperty("workspaceExists").GetBoolean());
        foreach (var key in new[] { "capacity", "entries" }) Assert.Equal(cut == "capacity-released" ? 0 : 1, r.GetProperty(key).GetInt32());
        foreach (var key in new[] { "slotRetained", "runtimeRetained" }) Assert.Equal(cut != "capacity-released", r.GetProperty(key).GetBoolean());
        Assert.Equal(1, r.GetProperty("workerStarts").GetInt32());
        Assert.Equal(cut == "cleanup-pending" ? null : "Retired", r.GetProperty("archivePhase").GetString());
        if (!terminal)
        {
            await fixture.Run("restart-fence-probe-denied", 0);
            using var observed = JsonDocument.Parse(File.ReadAllBytes(Path.Combine(fixture.Output, "fence-probe.json")));
            var p = observed.RootElement;
            Assert.True(p.GetProperty("preserved").GetBoolean(), p.ToString());
            Assert.Equal("Uncertain", p.GetProperty("kind").GetString());
            Assert.Equal(1, p.GetProperty("activeReservations").GetInt32());
            foreach (var key in new[] { "recoveries", "slots", "reservations", "owners", "releases", "capacity", "entries" }) Assert.Equal(0, p.GetProperty(key).GetInt32());
            foreach (var key in new[] { "accepted", "proposalConsumer", "repairConsumer" }) Assert.False(p.GetProperty(key).GetBoolean());
        }
        else
        {
            await fixture.Run("restart-fence-probe-retired", 0);
            using var observed = JsonDocument.Parse(File.ReadAllBytes(Path.Combine(fixture.Output, "fence-retired-probe.json")));
            var p = observed.RootElement;
            foreach (var key in new[] { "oldAccepted", "oldProposalConsumer", "oldRepairConsumer" }) Assert.False(p.GetProperty(key).GetBoolean());
            foreach (var key in new[] { "newAccepted", "newProposalConsumer", "newRepairConsumer", "oldPreserved", "oldBytesPreserved", "oldTombstonePreserved", "auditPrefixPreserved" }) Assert.True(p.GetProperty(key).GetBoolean(), p.ToString());
            foreach (var key in new[] { "oldSlots", "oldOwners", "oldReleases", "active", "capacity", "entries" }) Assert.Equal(0, p.GetProperty(key).GetInt32());
            foreach (var key in new[] { "slots", "owners", "releases" }) Assert.Equal(1, p.GetProperty(key).GetInt32());
            Assert.Equal(2, p.GetProperty("workerStarts").GetInt32());
            Assert.Equal(2, p.GetProperty("retired").GetInt32());
        }
    }
}
