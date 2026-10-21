using Xunit;

namespace BookOfEternityClient.Tests;

public sealed partial class GameEngineTurnLifecycleTests
{
    /// <summary>
    /// Exercises the original Prepared cut and current-owner recovery using a fresh copy of the common source42 fixture.
    /// </summary>
    /// <returns>
    /// Completion after the actual producer exit and all original cut0 recovery assertions.
    /// </returns>
    [Fact]
    public Task JointInterfaceFixtureBackedWindows_Cut0_Prepared() => RunFixtureBackedWindowsCutAsync(0);

    /// <summary>
    /// Exercises the original physically-applied I cut using a fresh copy of the common source42 fixture.
    /// </summary>
    /// <returns>
    /// Completion after the actual producer exit and all original cut1 recovery assertions.
    /// </returns>
    [Fact]
    public Task JointInterfaceFixtureBackedWindows_Cut1_IApplied() => RunFixtureBackedWindowsCutAsync(1);

    /// <summary>
    /// Exercises the original Published cut with real en-GB production and en-US frozen-policy recovery.
    /// </summary>
    /// <returns>
    /// Completion after the actual producer exit, ordinary-parser contrast and all original cut2 assertions.
    /// </returns>
    [Fact]
    public Task JointInterfaceFixtureBackedWindows_Cut2_PublishedCulture() => RunFixtureBackedWindowsCutAsync(2);

    /// <summary>
    /// Exercises the original FIRST2-without-result cut and held turn42 using a fresh common source42 copy.
    /// </summary>
    /// <returns>
    /// Completion after the actual producer exit and all original cut3 held-state assertions.
    /// </returns>
    [Fact]
    public Task JointInterfaceFixtureBackedWindows_Cut3_First2Held() => RunFixtureBackedWindowsCutAsync(3);

    /// <summary>
    /// Exercises the original ErrorPrepared cut and frozen-error hold using a fresh common source42 copy.
    /// </summary>
    /// <returns>
    /// Completion after the actual producer exit and all original cut4 error and inert-repeat assertions.
    /// </returns>
    [Fact]
    public Task JointInterfaceFixtureBackedWindows_Cut4_ErrorPrepared() => RunFixtureBackedWindowsCutAsync(4);

    /// <summary>
    /// Selects the distinct current five-cut artifact and the existing shared Windows crash observation bounds.
    /// </summary>
    /// <param name="cut">
    /// The original cut index from zero through four.
    /// </param>
    /// <returns>
    /// The unchanged original scenario flow after explicit checked fixture selection.
    /// </returns>
    private Task RunFixtureBackedWindowsCutAsync(int cut) => RunJointInterfaceActualProcessLossAsync(cut,
        extendedWindowsCrash: true,
        seedArtifactPath: Path.Combine(TestRepoPaths.RepoRoot, "specs", "1536-complete-wound-materialization",
            "fixtures", "joint-interface-source42-five-v1", "artifact"),
        seedUsage: JointInterfaceSeedCrashInput.OrdinaryWindowsFiveCutsUsage);
}
