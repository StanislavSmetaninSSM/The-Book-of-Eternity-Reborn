using Xunit;

namespace BookOfEternityClient.Tests;

public sealed partial class JointInterfaceCrashProcessSupportTests
{
    /// <summary>
    /// Shares the finite observation window while retaining a per-child wait cap.
    /// </summary>
    /// <param name="role">
    /// One of the two explicitly selected Windows crash roles.
    /// </param>
    /// <param name="elapsedMilliseconds">
    /// Monotonic elapsed time since the shared producer start boundary.
    /// </param>
    /// <param name="expectedMilliseconds">
    /// The exact remaining wait required at this boundary.
    /// </param>
    [Theory]
    [InlineData("engine-joint-interface-original-crash",0,600000)]
    [InlineData("engine-joint-interface-resume",0,600000)]
    [InlineData("engine-joint-interface-resume",219999,600000)]
    [InlineData("engine-joint-interface-resume",220000,600000)]
    [InlineData("engine-joint-interface-resume",395163,424837)]
    [InlineData("engine-joint-interface-resume",414722,405278)]
    [InlineData("engine-joint-interface-resume",600000,220000)]
    [InlineData("engine-joint-interface-resume",819999,1)]
    [InlineData("engine-joint-interface-original-crash",819999,1)]
    public void SharedWindowsWait_UsesRemainingWindow(string role, int elapsedMilliseconds, int expectedMilliseconds)
    {
        Assert.Equal(TimeSpan.FromMilliseconds(expectedMilliseconds),
            JointInterfaceCrashProcessSupport.GetSharedWindowsCrashTimeout(true,role,false,true,TimeSpan.FromMilliseconds(elapsedMilliseconds)));
    }

    /// <summary>
    /// Exhausted windows fail explicitly instead of becoming zero or infinite waits.
    /// </summary>
    /// <param name="elapsedMilliseconds">
    /// Elapsed time at or beyond the shared deadline.
    /// </param>
    [Theory]
    [InlineData(820000)]
    [InlineData(999999)]
    public void SharedWindowsWait_RejectsExhaustedWindow(int elapsedMilliseconds)
    {
        Assert.Throws<TimeoutException>(() => JointInterfaceCrashProcessSupport.GetSharedWindowsCrashTimeout(
            true,"engine-joint-interface-resume",false,true,TimeSpan.FromMilliseconds(elapsedMilliseconds)));
    }

    /// <summary>
    /// Negative elapsed time cannot manufacture additional allowance.
    /// </summary>
    [Fact]
    public void SharedWindowsWait_RejectsNegativeElapsed()
    {
        Assert.Throws<ArgumentOutOfRangeException>(() => JointInterfaceCrashProcessSupport.GetSharedWindowsCrashTimeout(
            true,"engine-joint-interface-resume",false,true,TimeSpan.FromMilliseconds(-1)));
    }

    /// <summary>
    /// Shared time is unavailable outside the two explicitly opted-in Windows crash roles.
    /// </summary>
    /// <param name="windows">
    /// Whether the requested platform is Windows.
    /// </param>
    /// <param name="role">
    /// The requested dispatcher role.
    /// </param>
    /// <param name="sourceProbe">
    /// Whether the incompatible source-probe extension was requested.
    /// </param>
    /// <param name="crash">
    /// Whether the explicit Windows crash extension was requested.
    /// </param>
    [Theory]
    [InlineData(false,"engine-joint-interface-original-crash",false,true)]
    [InlineData(false,"engine-joint-interface-resume",false,true)]
    [InlineData(true,"engine-joint-interface-source-probe",false,true)]
    [InlineData(true,"unrelated-role",false,true)]
    [InlineData(true,"engine-joint-interface-resume",false,false)]
    [InlineData(true,"engine-joint-interface-resume",true,true)]
    public void SharedWindowsWait_RejectsIneligibleRequest(bool windows,string role,bool sourceProbe,bool crash)
    {
        Assert.Throws<InvalidDataException>(() => JointInterfaceCrashProcessSupport.GetSharedWindowsCrashTimeout(
            windows,role,sourceProbe,crash,TimeSpan.Zero));
    }
}
