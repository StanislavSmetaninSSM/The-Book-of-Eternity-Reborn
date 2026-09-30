using BookOfEternityClient.Core;

namespace BookOfEternityClient.Services;

/// <summary>
/// Identifies whether recovery produced a common plan, replayed history or was rejected.
/// </summary>
internal enum MortalWoundRecoveryAcceptedPlanCompositionDisposition
{
    Composed,
    ExactReplay,
    Rejected
}

/// <summary>
/// Carries the authenticated recovery composition result without publishing files.
/// </summary>
/// <param name="AcceptedPlan">
/// The common publication plan when composition succeeds; otherwise, null.
/// </param>
/// <param name="Disposition">
/// The result of authenticating and composing the submitted resolution.
/// </param>
/// <param name="Issues">
/// Admission or planning diagnostics; empty for composition and exact replay.
/// </param>
/// <param name="Receipt">
/// The accepted recovery receipt; null when the resolution is rejected.
/// </param>
/// <param name="WoundStageBundle">
/// The authenticated stages for a new publication; null for replay and rejection.
/// </param>
internal sealed record MortalWoundRecoveryAcceptedPlanCompositionResult(
    AcceptedMechanicsPlan? AcceptedPlan,
    MortalWoundRecoveryAcceptedPlanCompositionDisposition Disposition,
    IReadOnlyList<ValidationIssue> Issues,
    MortalWoundRecoveryReceipt? Receipt,
    AcceptedMechanicsWoundStageBundle? WoundStageBundle);

/// <summary>
/// Routes recovery through the registry that owns its accepted source and publication.
/// </summary>
internal static class MortalWoundRecoveryAcceptedPlanComposer
{
    /// <summary>
    /// Authenticates a complete recovery resolution and composes its single common plan.
    /// This operation registers a plan without writing canonical state.
    /// </summary>
    /// <param name="fileSystem">
    /// The filesystem that owns the registered accepted source.
    /// </param>
    /// <param name="writeLease">
    /// Its active canonical write lease.
    /// </param>
    /// <param name="binding">
    /// The complete original accepted event binding.
    /// </param>
    /// <param name="resolution">
    /// The complete planner result; changed or detached semantics are rejected.
    /// </param>
    /// <returns>
    /// One common plan and receipt, an existing replay receipt, or rejection diagnostics.
    /// </returns>
    internal static MortalWoundRecoveryAcceptedPlanCompositionResult Compose(
        FileSystemManager fileSystem,
        FileSystemManager.CanonicalWriteLease writeLease,
        WoundAcceptedTurnBinding binding,
        MortalWoundRecoveryResolution resolution) =>
        AcceptedTurnAuthorityRegistry.ComposeMortalWoundRecovery(
            fileSystem, writeLease, binding, resolution);

    /// <summary>
    /// Creates a closed rejection with no publication or receipt authority.
    /// </summary>
    /// <param name="issues">
    /// Diagnostics that explain the failed admission or planning stage.
    /// </param>
    /// <returns>
    /// A rejection result with no plan, bundle or receipt.
    /// </returns>
    internal static MortalWoundRecoveryAcceptedPlanCompositionResult Reject(
        IReadOnlyList<ValidationIssue> issues) => new(null,
            MortalWoundRecoveryAcceptedPlanCompositionDisposition.Rejected,
            issues, null, null);
}
