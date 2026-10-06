using System.Text.Json;
using Xunit;

namespace BookOfEternityClient.Tests;

public sealed partial class GmWorkerRestartFenceTests
{
    [Theory]
    [InlineData("prepared", "Prepared", 0)]
    [InlineData("helper", "LaunchIntent", 0)]
    [InlineData("release-intent", "ReleaseIntent", 0)]
    [InlineData("release-sent", "ReleaseIntent", 1)]
    [InlineData("released-ack", "Released", 1)]
    [InlineData("completed", "Released", 1)]
    [InlineData("scoped-stop", "Released", 1)]
    [InlineData("outputs-stop-record", "StopValidated", 1)]
    public async Task LaunchCrash_ActualCutNeverReconstructsAuthority(string cut, string phase, int starts)
    {
        var fixture = await GmWorkerRestartAdmissionTests.RestartFixture.Create();
        await fixture.Run("restart-fence-seed-launch-" + cut, 77, allowGuardianCleanup: true);
        using var reached = JsonDocument.Parse(File.ReadAllBytes(Path.Combine(fixture.Output, "fence-cut.json")));
        var r = reached.RootElement;
        Assert.Equal(cut, r.GetProperty("cut").GetString());
        Assert.Equal(1, r.GetProperty("cutAttempts").GetInt32());
        if (cut is not ("scoped-stop" or "outputs-stop-record"))
            Assert.Equal(JsonValueKind.Null, r.GetProperty("stop").ValueKind);
        Assert.Equal(phase, r.GetProperty("diskPhase").GetString());
        Assert.True(r.GetProperty("originalTaskBound").GetBoolean(), r.ToString());
        Assert.Equal(starts, r.GetProperty("workerStarts").GetInt32());
        Assert.Equal(starts, r.GetProperty("releaseFrames").GetInt32());
        Assert.Equal(cut != "prepared", r.GetProperty("ownerBound").GetBoolean());
        if (cut == "release-sent") Assert.Equal("ReleaseIntent", r.GetProperty("livePhase").GetString());
        if (cut == "released-ack")
        {
            Assert.Equal("Released", r.GetProperty("livePhase").GetString());
            Assert.False(r.GetProperty("completionTaskCompleted").GetBoolean());
        }
        if (cut == "completed") Assert.Equal(0, r.GetProperty("originalCompletionExit").GetInt32());
        if (cut is "scoped-stop" or "outputs-stop-record")
        {
            Assert.Equal("StoppedWithinScope", r.GetProperty("stop").GetString());
            Assert.Equal(0, r.GetProperty("recordedCompletion").GetInt32());
            Assert.Equal(cut == "outputs-stop-record", r.GetProperty("outputsSettled").GetBoolean());
        }
        // A second independent process receives no original execution, lease or kernel handle.
        await fixture.Run("restart-fence-probe-denied", 0);
        using var observed = JsonDocument.Parse(File.ReadAllBytes(Path.Combine(fixture.Output, "fence-probe.json")));
        var p = observed.RootElement;
        Assert.True(p.GetProperty("preserved").GetBoolean(), p.ToString());
        Assert.Equal("Uncertain", p.GetProperty("kind").GetString());
        Assert.Equal(1, p.GetProperty("activeReservations").GetInt32());
        Assert.Equal(starts, p.GetProperty("workerStarts").GetInt32());
        foreach (var key in new[] { "recoveries", "slots", "reservations", "owners", "releases", "capacity", "entries" })
            Assert.Equal(0, p.GetProperty(key).GetInt32());
        foreach (var key in new[] { "accepted", "proposalConsumer", "repairConsumer" })
            Assert.False(p.GetProperty(key).GetBoolean());
    }
}
