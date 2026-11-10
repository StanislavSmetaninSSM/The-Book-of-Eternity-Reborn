using Xunit;

namespace BookOfEternityClient.Tests;

public sealed partial class GameEngineTurnLifecycleTests
{
    /// <summary>
    /// Loads a genuine source checkpoint before natural Stage43 and the original cut0 crash/recovery assertions.
    /// </summary>
    /// <returns>
    /// Completion after an actual producer crash and independent current-owner recovery.
    /// </returns>
    [Fact]
    public Task JointInterface_SeededWindowsCrashCut0PreparedReentersCurrentOwner()
        => RunJointInterfaceActualProcessLossAsync(0, extendedWindowsCrash: true,
            seedArtifactPath: Path.Combine(TestRepoPaths.RepoRoot, "specs", "1536-complete-wound-materialization",
                "fixtures", "joint-interface-source42", "artifact"));
}
