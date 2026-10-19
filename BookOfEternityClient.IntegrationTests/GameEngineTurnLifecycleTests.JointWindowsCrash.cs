using Xunit;

namespace BookOfEternityClient.Tests;

/// <summary>
/// Exercises game-engine turn lifecycle, persistence and recovery contracts.
/// </summary>
public sealed partial class GameEngineTurnLifecycleTests
{
    /// <summary>
    /// Runs the original prepared-record crash and current-owner recovery with explicit Windows waits.
    /// </summary>
    /// <returns>
    /// Completion after every original cut-zero process and gameplay assertion succeeds.
    /// </returns>
    [Fact]
    public Task JointInterface_WindowsCrashCut0PreparedReentersCurrentOwner() =>
        RunJointInterfaceActualProcessLossAsync(0, extendedWindowsCrash: true);

    /// <summary>
    /// Runs the original physically applied interface crash and recovery with explicit Windows waits.
    /// </summary>
    /// <returns>
    /// Completion after every original cut-one process and gameplay assertion succeeds.
    /// </returns>
    [Fact]
    public Task JointInterface_WindowsCrashCut1AppliedReentersCurrentOwner() =>
        RunJointInterfaceActualProcessLossAsync(1, extendedWindowsCrash: true);

    /// <summary>
    /// Runs the original published-record crash with its independent culture rejection contrast on Windows.
    /// </summary>
    /// <returns>
    /// Completion after every original cut-two identity, culture and gameplay assertion succeeds.
    /// </returns>
    [Fact]
    public Task JointInterface_WindowsCrashCut2PublishedPreservesCultureProof() =>
        RunJointInterfaceActualProcessLossAsync(2, extendedWindowsCrash: true);

    /// <summary>
    /// Runs the original installed-graph crash without a result and verifies current-owner hold on Windows.
    /// </summary>
    /// <returns>
    /// Completion after every original cut-three process and held-state assertion succeeds.
    /// </returns>
    [Fact]
    public Task JointInterface_WindowsCrashCut3MissingResultRemainsHeld() =>
        RunJointInterfaceActualProcessLossAsync(3, extendedWindowsCrash: true);

    /// <summary>
    /// Runs the original prepared-error crash and verifies frozen-error hold on Windows.
    /// </summary>
    /// <returns>
    /// Completion after every original cut-four process and frozen-error assertion succeeds.
    /// </returns>
    [Fact]
    public Task JointInterface_WindowsCrashCut4FrozenErrorRemainsHeld() =>
        RunJointInterfaceActualProcessLossAsync(4, extendedWindowsCrash: true);
}
