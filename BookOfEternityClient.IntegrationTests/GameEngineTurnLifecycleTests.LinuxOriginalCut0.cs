using Xunit;

namespace BookOfEternityClient.Tests;

public sealed partial class GameEngineTurnLifecycleTests
{
    /// <summary>
    /// Replays only original cut0 through the unchanged ordinary NativeLinux body to retain primary failure DATA.
    /// </summary>
    /// <returns>The same complete original game/identity/EOF/join/cleanup assertions for cut0.</returns>
    [Fact]
    public async Task GuaranteedTreatment_LinuxOriginalCut0RetainsCompleteResumeReport()
    {
        Assert.True(OperatingSystem.IsLinux());
        Assert.Equal(JointInterfaceCrashBackend.NativeLinux,
            JointInterfaceCrashProcessSupport.SelectBackend(false, true, new JointInterfaceCrashMode(0, false, null)));
        await RunJointInterfaceActualProcessLossAsync(0);
    }
}
