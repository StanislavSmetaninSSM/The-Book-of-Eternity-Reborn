using System.Text.Json;
using Xunit;

namespace BookOfEternityClient.Tests;

public sealed partial class GmWorkerRestartFenceTests
{
    [Theory]
    [InlineData("intent", "PublicationIntent", false, false, false)]
    [InlineData("bundle", "PublicationIntent", true, false, false)]
    [InlineData("published-disk", "Published", true, false, false)]
    [InlineData("inbox", "Published", true, true, true)]
    [InlineData("live-publication", "Published", true, true, true)]
    public async Task PublicationCrash_ActualArtifactsCannotBecomeColdSuccess(string cut, string phase, bool bundle, bool inbox, bool acknowledged)
    {
        var fixture = await GmWorkerRestartAdmissionTests.RestartFixture.Create();
        await fixture.Run("restart-fence-seed-publication-" + cut, 77, allowGuardianCleanup: true);
        using var reached = JsonDocument.Parse(File.ReadAllBytes(Path.Combine(fixture.Output, "fence-cut.json")));
        var r = reached.RootElement;
        Assert.Equal(cut, r.GetProperty("cut").GetString());
        Assert.Equal(1, r.GetProperty("cutAttempts").GetInt32());
        Assert.Equal(phase, r.GetProperty("diskPhase").GetString());
        Assert.True(r.GetProperty("originalTaskBound").GetBoolean());
        Assert.True(r.GetProperty("workspaceExists").GetBoolean());
        Assert.Equal(1, r.GetProperty("workerStarts").GetInt32());
        Assert.Equal(0, r.GetProperty("completion").GetInt32());
        Assert.Equal("StoppedWithinScope", r.GetProperty("stop").GetString());
        Assert.True(r.GetProperty("outputsSettled").GetBoolean());
        Assert.Equal(bundle, r.GetProperty("bundleHash").ValueKind != JsonValueKind.Null);
        Assert.Equal(inbox, r.GetProperty("inboxHash").ValueKind != JsonValueKind.Null);
        if (inbox) Assert.Equal(r.GetProperty("bundleHash").GetString(), r.GetProperty("inboxHash").GetString());
        Assert.Equal(acknowledged, r.GetProperty("publicationAcknowledged").GetBoolean());
        Assert.Equal(cut == "live-publication", r.GetProperty("originalPublicationRecorded").GetBoolean());
        Assert.Equal(cut == "live-publication" ? 1 : 0, r.GetProperty("receivedEvents").GetInt32());
        if (cut == "published-disk") Assert.Equal("PublicationIntent", r.GetProperty("livePhase").GetString());
        await fixture.Run("restart-fence-probe-denied", 0);
        using var observed = JsonDocument.Parse(File.ReadAllBytes(Path.Combine(fixture.Output, "fence-probe.json")));
        var p = observed.RootElement;
        Assert.True(p.GetProperty("preserved").GetBoolean(), p.ToString());
        Assert.Equal("Uncertain", p.GetProperty("kind").GetString());
        Assert.Equal(1, p.GetProperty("activeReservations").GetInt32());
        Assert.Equal(1, p.GetProperty("workerStarts").GetInt32());
        foreach (var key in new[] { "recoveries", "slots", "reservations", "owners", "releases", "capacity", "entries" })
            Assert.Equal(0, p.GetProperty(key).GetInt32());
        foreach (var key in new[] { "accepted", "proposalConsumer", "repairConsumer" })
            Assert.False(p.GetProperty(key).GetBoolean());
    }
}
