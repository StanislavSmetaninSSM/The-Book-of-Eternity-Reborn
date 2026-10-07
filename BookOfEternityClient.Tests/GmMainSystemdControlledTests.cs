using Xunit;
namespace BookOfEternityClient.Tests;
public sealed class GmMainSystemdControlledTests
{
    [Fact]
    public async Task ActualHeldRoot_AttachesBeforeRunningAndSingleRelease() => await GmOwnedTerminalLinuxTests.RunAsync("terminal-systemd-connected");
}
