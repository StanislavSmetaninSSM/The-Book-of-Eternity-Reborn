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
}
