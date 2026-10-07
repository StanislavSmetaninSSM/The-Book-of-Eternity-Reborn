using Xunit;
namespace BookOfEternityClient.Tests;
public sealed class BrowserAudioIsolationTests
{
    [Fact]
    public async Task ActualHostRegistration_LeavesPlaybackInBrowserWithoutServerNativeCalls()
    {
        using var fixture = new AudioLinuxFixture(); var report = await fixture.Run("browser");
        Assert.Equal(0, report.GetProperty("WindowsAttempts").GetInt32());
        Assert.Equal(0, report.GetProperty("SdlAttempts").GetInt32());
        Assert.Equal("BrowserManaged", report.GetProperty("Outcome").GetString());
    }

    [Fact]
    public async Task ActualBrowserSettingsCommit_AppliesAcceptedVolumesWithoutServerOutput()
    {
        using var fixture = new AudioLinuxFixture(); var r = await fixture.Run("browser-settings");
        Assert.True(r.GetProperty("Committed").GetBoolean());
        Assert.Equal("BrowserManaged", r.GetProperty("Outcome").GetString());
        Assert.Equal(0, r.GetProperty("WindowsAttempts").GetInt32());
        Assert.Equal(0, r.GetProperty("SdlAttempts").GetInt32());
    }
}
