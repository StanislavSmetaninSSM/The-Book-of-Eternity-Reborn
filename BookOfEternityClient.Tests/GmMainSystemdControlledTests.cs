using Xunit;
namespace BookOfEternityClient.Tests;
public sealed class GmMainSystemdControlledTests
{
    [Fact]
    public async Task ActualHeldRoot_AttachesBeforeRunningAndSingleRelease() => await GmOwnedTerminalLinuxTests.RunAsync("terminal-systemd-connected");
    [Theory]
    [InlineData("terminal-systemd-start-lost")]
    [InlineData("terminal-systemd-capture-error")]
    [InlineData("terminal-systemd-start-cancel")]
    [InlineData("terminal-systemd-wrong-fd")]
    [InlineData("terminal-systemd-wrong-manager")]
    [InlineData("terminal-systemd-release-loss")]
    [InlineData("terminal-systemd-release-ack")]
    [InlineData("terminal-systemd-bind-deadline")]
    [InlineData("terminal-systemd-stop-lost")]
    [InlineData("terminal-systemd-populated")]
    [InlineData("terminal-systemd-pruned")]
    [InlineData("terminal-systemd-stale")]
    [InlineData("terminal-systemd-read-error")]
    [InlineData("terminal-systemd-changed-cgroup")]
    [InlineData("terminal-systemd-late-loss")]
    [InlineData("terminal-systemd-native-fault")]
    [InlineData("terminal-systemd-changed-unit")]
    [InlineData("terminal-systemd-changed-invocation")]
    public async Task AmbiguousOriginalScope_RetainsLogicalUncertainAfterNarrowCleanup(string mode)
    { await GmOwnedTerminalLinuxTests.RunAsync(mode); }
    [Theory]
    [InlineData("terminal-systemd-io-drain")]
    [InlineData("terminal-systemd-io-fault")]
    [InlineData("terminal-systemd-stopped-debt")]
    [InlineData("terminal-systemd-native-dispose-fault")]
    [InlineData("terminal-systemd-bus-dispose-fault")]
    public async Task ActualBridge_RequiresIoDisposalAndOriginalStoppedAck(string mode)
    { await GmOwnedTerminalLinuxTests.RunAsync(mode); }
}
