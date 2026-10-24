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
    /// Selects the current diagnostic package and original shared Windows crash bounds.
    /// An unset <c>BOE_TEST_JOINT_CRASH_PARENT</c> retains repository-parent storage; a provided value must be absolute.
    /// Validates optional BOE_TEST_DIAGNOSTIC_ROOT configuration before creating the disposable fixture.
    /// BOE_TEST_JOINT_TIMINGS defaults to full; an explicit off value omits only detailed timing observation.
    /// BOE_TEST_JOINT_SEED_PACKAGE optionally selects an external genuine current-source package through ordinary fingerprint validation.
    /// BOE_TEST_JOINT_CONTROL_DATA_CACHE optionally selects the restricted fresh control/realm DATA comparison.
    /// BOE_TEST_JOINT_ADMISSION_CACHE optionally selects the restricted fresh-classifier comparison.
    /// BOE_TEST_JOINT_PRODUCER_READ_FORWARDERS accepts on or off; absent settings disable
    /// empty read hooks for cut zero and retain forwarding for the other cuts.
    /// </summary>
    /// <param name="cut">
    /// The original cut index from zero through four.
    /// </param>
    /// <returns>
    /// The unchanged original scenario flow after explicit checked fixture selection.
    /// </returns>
    private Task RunFixtureBackedWindowsCutAsync(int cut)
    {
        var requestedParent = Environment.GetEnvironmentVariable("BOE_TEST_JOINT_CRASH_PARENT");
        if (requestedParent is not null && !Path.IsPathFullyQualified(requestedParent))
            throw new InvalidDataException("The diagnostic crash parent must be an absolute path when provided.");
        _ = JointChildOutputCapture.ResolveDefaultArtifactParent();
        var observationMode = JointInterfaceCrashObservation.Parse(Environment.GetEnvironmentVariable("BOE_TEST_JOINT_TIMINGS"));
        var seedPackage = Environment.GetEnvironmentVariable("BOE_TEST_JOINT_SEED_PACKAGE");
        if (seedPackage is not null && !Path.IsPathFullyQualified(seedPackage))
            throw new InvalidDataException("An external source42 package must have an absolute path.");
        var reuse = JointAdmissionDirectoryControl.Parse(Environment.GetEnvironmentVariable("BOE_TEST_JOINT_ADMISSION_CACHE"));
        var controlDataReuse = JointControlGameStateDataControl.Parse(Environment.GetEnvironmentVariable("BOE_TEST_JOINT_CONTROL_DATA_CACHE"));
        var producerReadForwarders = JointProducerReadForwarderControl.Parse(
            Environment.GetEnvironmentVariable("BOE_TEST_JOINT_PRODUCER_READ_FORWARDERS")) ?? cut != 0;
        return RunJointInterfaceActualProcessLossAsync(cut,
            extendedWindowsCrash: true,
            seedPackageRoot: seedPackage ?? Path.Combine(TestRepoPaths.RepoRoot, "specs", "1536-complete-wound-materialization",
                "fixtures", "joint-interface-source42-cleanup-proof-cache-v1", "package"),
            seedUsage: JointInterfaceSeedCrashInput.OrdinaryWindowsFiveCutsUsage,
            ownedContainerParent: requestedParent ?? (Path.GetDirectoryName(TestRepoPaths.RepoRoot)
                ?? throw new InvalidDataException("Fixture-backed crash storage requires the checkout's explicit parent.")),
            observationMode: observationMode, admissionDirectoryReuse: reuse, controlGameStateDataReuse: controlDataReuse,
            producerReadForwarders: producerReadForwarders);
    }
}
