using System.Text.Json;
using BookOfEternityClient.Core;

namespace BookOfEternityClient.Services;

internal static partial class AcceptedTurnAuthorityRegistry
{
    /// <summary>
    /// Composes recovery through the current registry owner and live canonical lease.
    /// </summary>
    /// <param name="fileSystem">
    /// The canonical filesystem that owns the accepted source.
    /// </param>
    /// <param name="writeLease">
    /// Its active canonical write lease.
    /// </param>
    /// <param name="binding">
    /// The complete original accepted binding.
    /// </param>
    /// <param name="resolution">
    /// The exact planner result to independently recompute.
    /// </param>
    /// <returns>
    /// An authenticated common plan, existing receipt, or structured rejection.
    /// </returns>
    internal static MortalWoundRecoveryAcceptedPlanCompositionResult ComposeMortalWoundRecovery(
        FileSystemManager fileSystem,
        FileSystemManager.CanonicalWriteLease writeLease,
        WoundAcceptedTurnBinding binding,
        MortalWoundRecoveryResolution resolution)
    {
        try
        {
            fileSystem.EnsureCanonicalWriteLeaseActive(writeLease);
            return GetState(fileSystem, writeLease).ComposeMortalWoundRecovery(
                fileSystem, writeLease, binding, resolution);
        }
        catch (Exception exception) when (exception is ArgumentException or
            InvalidOperationException or InvalidDataException or IOException or
            UnauthorizedAccessException or JsonException or NullReferenceException or OverflowException)
        {
            return MortalWoundRecoveryAcceptedPlanComposer.Reject(
                MortalWoundRecoveryPlanner.RegistryFailure(exception.GetType().Name).Issues);
        }
    }

    private sealed partial class AcceptedTurnAuthorityState
    {
        /// <summary>
        /// Retains registry ownership of the exact accepted source through all recovery stages.
        /// </summary>
        /// <param name="prepared">
        /// The prepared private recovery stage.
        /// </param>
        /// <returns>
        /// <see langword="true"/> when the source and prepared seal remain registered;
        /// otherwise, <see langword="false"/>.
        /// </returns>
        private bool RecoveryContinuationRegistryAgrees(WoundPreparedAcceptedTurnPlan prepared) =>
            WoundAcceptedTurnPlanner.TryReadRecoveryContinuation(
                prepared.RecoveryContinuationAuthority, out var continuation) &&
            ReferenceEquals(_mortalWoundTreatmentAcceptedState, continuation.AcceptedStateAuthority) &&
            WoundAcceptedTurnPlanner.RecoveryContinuationPreparedAgrees(prepared);

        /// <summary>
        /// Independently resolves the selected source before deriving any publication authority.
        /// </summary>
        /// <param name="fileSystem">
        /// The registered source filesystem.
        /// </param>
        /// <param name="writeLease">
        /// Its active canonical write lease.
        /// </param>
        /// <param name="binding">
        /// The complete original accepted binding.
        /// </param>
        /// <param name="resolution">
        /// The complete submitted result to compare against current planning.
        /// </param>
        /// <returns>
        /// One common plan, an exact replay receipt, or diagnostics without writes.
        /// </returns>
        internal MortalWoundRecoveryAcceptedPlanCompositionResult ComposeMortalWoundRecovery(
            FileSystemManager fileSystem,
            FileSystemManager.CanonicalWriteLease writeLease,
            WoundAcceptedTurnBinding binding,
            MortalWoundRecoveryResolution resolution)
        {
            lock (_gate)
            {
                var state = _mortalWoundTreatmentAcceptedState;
                if (state is null || resolution is null ||
                    !state.IsLeaseBoundTo(fileSystem, writeLease) ||
                    !state.AgreesWithBindingAndWound(binding, state.CurrentWound.WoundId))
                    return MortalWoundRecoveryAcceptedPlanComposer.Reject(
                        MortalWoundRecoveryPlanner.RegistryFailure("missing or mismatched accepted source").Issues);
                var actual = MortalWoundRecoveryPlanner.PlanRegistered(fileSystem, writeLease,
                    state, binding, state.CurrentWound.WoundId, MortalWoundRecoveryPlannerCapability);
                if (actual.Disposition == MortalWoundRecoveryPlanningDisposition.ExactReplay)
                {
                    var saved = state.History.Transitions
                        .Select(row => row.TransitionResult).OfType<MortalWoundRecoveryPersistedResult>()
                        .SingleOrDefault(row => row.SourceWound.WoundId == state.CurrentWound.WoundId &&
                            row.Resolution.TickKey == resolution.TickKey);
                    if (actual.ReplayReceipt is null || saved is null ||
                        !WoundAcceptedTurnPlanner.RecoveryResolutionsEqual(saved.Resolution, resolution) ||
                        saved.Receipt != actual.ReplayReceipt)
                        return MortalWoundRecoveryAcceptedPlanComposer.Reject(
                            MortalWoundRecoveryPlanner.RegistryFailure("changed replay resolution").Issues);
                    return new(null, MortalWoundRecoveryAcceptedPlanCompositionDisposition.ExactReplay,
                        Array.Empty<ValidationIssue>(), actual.ReplayReceipt, null);
                }
                if (actual.Resolution is null ||
                    actual.Disposition != MortalWoundRecoveryPlanningDisposition.Resolved)
                    return MortalWoundRecoveryAcceptedPlanComposer.Reject(actual.Issues);
                if (!WoundAcceptedTurnPlanner.RecoveryResolutionsEqual(actual.Resolution, resolution))
                    return MortalWoundRecoveryAcceptedPlanComposer.Reject(
                        MortalWoundRecoveryPlanner.RegistryFailure("changed recovery resolution or ordered intents").Issues);
                var baselines = WoundAcceptedTurnPlanner.ReadRecoveryPublicationBaselines(
                    fileSystem, writeLease, binding);
                if (baselines.Input is null || baselines.SourceRoots is null)
                    return MortalWoundRecoveryAcceptedPlanComposer.Reject(baselines.Issues);
                var authority = WoundAcceptedTurnPlanner.MintRecoveryContinuation(
                    MortalWoundRecoveryCompositionCapability, state, actual.Resolution,
                    baselines.Input, baselines.SourceRoots);
                var preparedResult = _woundPlan.GetOrBuildRecoveryContinuationPrepared(
                    baselines.Input, authority, out var reused);
                if (!reused)
                {
                    _effectPlan.InvalidateAll();
                    ClearWoundEffectCore();
                    _commonPlan.InvalidateAll();
                    ClearValidatedTreatmentPublicationCore();
                }
                if (!preparedResult.Success || preparedResult.Plan is null)
                    return MortalWoundRecoveryAcceptedPlanComposer.Reject(preparedResult.Issues);
                var prepared = preparedResult.Plan;
                var effectInput = WoundAcceptedTurnPlanner.ComposeRecoveryEffectInput(prepared);
                var effect = GetOrBuildWoundEffectValidated(prepared, effectInput);
                if (!effect.Success || effect.Plan is null)
                    return MortalWoundRecoveryAcceptedPlanComposer.Reject(effect.Issues);
                var final = GetOrBuildWoundFinal(prepared, effect);
                if (!final.Success || final.Plan is null)
                    return MortalWoundRecoveryAcceptedPlanComposer.Reject(final.Issues);
                var bundle = new AcceptedMechanicsWoundStageBundle(
                    baselines.Input, prepared, effect.Plan, final.Plan);
                var common = AcceptedMechanicsWoundCommonInputComposer.ComposeRecoveryContinuation(
                    fileSystem, writeLease, bundle);
                if (!common.Success || common.Input is null)
                    return MortalWoundRecoveryAcceptedPlanComposer.Reject(common.Issues);
                var accepted = GetOrBuildCommonWoundValidated(common.Input, bundle);
                if (!accepted.Success || accepted.Plan is null)
                    return MortalWoundRecoveryAcceptedPlanComposer.Reject(accepted.Issues);
                var history = WoundHistoryState.Parse(final.Plan.HistoryAfterImage.ToJsonString(),
                    WoundHistoryState.HistoryPath);
                var result = history.State?.Transitions.Select(row => row.TransitionResult)
                    .OfType<MortalWoundRecoveryPersistedResult>()
                    .SingleOrDefault(row => row.Resolution.TickKey == resolution.TickKey);
                if (result is null || history.Issues.Count != 0)
                {
                    _commonPlan.InvalidateAll();
                    return MortalWoundRecoveryAcceptedPlanComposer.Reject(
                        MortalWoundRecoveryPlanner.RegistryFailure("missing finalized recovery receipt").Issues);
                }
                return new(accepted.Plan, MortalWoundRecoveryAcceptedPlanCompositionDisposition.Composed,
                    Array.Empty<ValidationIssue>(), result.Receipt, bundle);
            }
        }
    }
}
