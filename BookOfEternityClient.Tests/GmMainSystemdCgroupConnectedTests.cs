using Xunit;
namespace BookOfEternityClient.Tests;
public sealed class GmMainSystemdCgroupConnectedTests {
    [Fact] public async Task ConcreteSource_OriginalBridgeTwoInputsAndStoppedAck()=>await GmOwnedTerminalLinuxTests.RunAsync("terminal-systemd-cgroup-connected");
    [Theory][InlineData("terminal-systemd-cgroup-late-replacement")][InlineData("terminal-systemd-cgroup-late-read-fault")]
    public async Task ConcreteSource_PostReapFailureRetainsOriginalUncertain(string mode)=>await GmOwnedTerminalLinuxTests.RunAsync(mode);
}
