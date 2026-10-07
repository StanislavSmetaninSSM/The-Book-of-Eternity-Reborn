using Xunit;
namespace BookOfEternityClient.Tests;
public sealed class ConsoleAudioLinuxTests
{
    [Fact]
    public async Task PublicService_SelectsLinuxAndReportsUnavailableWithoutWindowsOrDeviceCalls()
    {
        using var fixture = new AudioLinuxFixture(); var report = await fixture.Run("console");
        Assert.Equal(0, report.GetProperty("WindowsAttempts").GetInt32());
        Assert.Equal("BackendUnavailable", report.GetProperty("Outcome").GetString());
        Assert.True(report.GetProperty("SdlAttempts").GetInt32() > 0);
        Assert.Equal(0, report.GetProperty("DefaultDeviceCalls").GetInt32());
    }
}
