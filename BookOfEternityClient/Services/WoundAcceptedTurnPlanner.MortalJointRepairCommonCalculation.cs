using System.Text.Json.Nodes;
using BookOfEternityClient.Core;

namespace BookOfEternityClient.Services;

internal static partial class WoundAcceptedTurnPlanner
{
    private static readonly object JointCommonCalculationIssuer = new();

    private sealed partial class JointResourcePublicationAuthority
    {
        private sealed record RetainedCalculation(JointResourcePublicationAuthority Owner,
            AcceptedMechanicsCalculationInputs Inputs, AcceptedMechanicsPlanner.CalculatedCommonArtifacts Artifacts);
        private RetainedCalculation? _retainedCalculation;
        private int _calculationAttempted;

        internal bool HasRetainedCommonCalculation =>
            Volatile.Read(ref _retainedCalculation) is { } retained && ReferenceEquals(retained.Owner, this);
        private RetainedCalculation RequireRetainedCalculation() =>
            Volatile.Read(ref _retainedCalculation) is { } retained && ReferenceEquals(retained.Owner, this)
                ? retained : throw new InvalidDataException("Missing exact private joint common calculation.");
        internal AcceptedMechanicsPlanner.CalculatedCommonArtifacts RetainedCommonCalculation =>
            RequireRetainedCalculation().Artifacts;
        internal AcceptedMechanicsCalculationInputs RetainedCalculationInputs => RequireRetainedCalculation().Inputs;

        // No caller-supplied C, candidate map or factory. The existing resource
        // owner captures its own data and computes once before common issuance.
        // A failed/in-flight attempt is held; this owner cannot recompute it.
        internal async Task<IReadOnlyList<ValidationIssue>> EnsureCommonCalculationAsync(object issuer,
            FileSystemManager files, FileSystemManager.CanonicalWriteLease lease)
        {
            if (!ReferenceEquals(issuer, JointCommonCalculationIssuer))
                throw new InvalidOperationException("Joint calculation requires its private resource owner.");
            await ValidateJointRepairResourcePublicationAsync(files, lease, this);
            if (HasRetainedCommonCalculation) return Array.Empty<ValidationIssue>();
            if (Interlocked.CompareExchange(ref _calculationAttempted, 1, 0) != 0)
                throw new InvalidDataException("The existing joint calculation attempt cannot be replaced or repeated.");
            var bundle = Baseline.Bundle;
            var captured = await AcceptedMechanicsWoundCommonInputComposer.CaptureJointRepairCalculationInputsAsync(
                files, lease, bundle, Baseline.Anchors, Baseline.Mechanical.BeforeImages,
                Definitions, State, History, FinalOwners, this, TerminalOwners, CreateIdentityFactory());
            if (captured.Inputs is not { } inputs) return captured.Issues;
            var binding = bundle.Input.Binding;
            var calculated = AcceptedMechanicsPlanner.CalculateUnboundedCommonArtifacts(
                inputs, binding.SessionId, binding.RequestId, bundle.EffectBatchPlan.EffectInput.RawCommands);
            if (calculated.Calculation is not { } artifacts) return calculated.Issues;
            await ValidateJointRepairResourcePublicationAsync(files, lease, this);
            Volatile.Write(ref _retainedCalculation, new RetainedCalculation(this, inputs, artifacts));
            return Array.Empty<ValidationIssue>();
        }

        internal bool RetainedContextAgrees(AcceptedMechanicsPlanningContext context)
        {
            if (!HasRetainedCommonCalculation) return false;
            var inputs = RetainedCalculationInputs;
            return inputs.Definitions.ToCanonicalJson() == context.Definitions.ToCanonicalJson() &&
                inputs.State.Fingerprint == context.State.Fingerprint && inputs.History.Fingerprint == context.History.Fingerprint &&
                inputs.Owners.Fingerprint == context.Owners.Fingerprint && inputs.Sources.Fingerprint == context.Sources.Fingerprint &&
                JsonNode.DeepEquals(inputs.Commands.Root, context.Commands.Root) &&
                JsonNode.DeepEquals(inputs.EffectIdentityRoot, context.EffectIdentityRoot) &&
                inputs.PendingResolutionState?.ToCanonicalJson() == context.PendingResolutionState?.ToCanonicalJson() &&
                ReferenceEquals(inputs.ResourceIdentityFactory, context.ResourceIdentityFactory) &&
                ReferenceEquals(inputs.EffectIdentityFactory, context.EffectIdentityFactory) &&
                WoundAcceptedTurnFingerprints.ComputeAcceptedEffectPlanPayload(inputs.EffectPlan!) ==
                    WoundAcceptedTurnFingerprints.ComputeAcceptedEffectPlanPayload(context.EffectPlan!);
        }

        internal bool RetainedInputAgrees(AcceptedMechanicsInput input)
        {
            var binding = Baseline.Bundle.Input.Binding;
            return HasRetainedCommonCalculation && input.PlanningContext is { } context && RetainedContextAgrees(context) &&
                (input.SessionId, input.RequestId, input.SnapshotToken, input.Realm, input.Turn) ==
                    (binding.SessionId, binding.RequestId, binding.SnapshotToken, binding.Realm, binding.Turn) &&
                JsonNode.DeepEquals(input.EffectCommands, Baseline.Bundle.EffectBatchPlan.EffectInput.RawCommands);
        }
    }
}
