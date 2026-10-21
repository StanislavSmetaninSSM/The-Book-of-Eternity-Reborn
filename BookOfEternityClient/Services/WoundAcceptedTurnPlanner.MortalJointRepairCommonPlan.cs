using System.Text.Json.Nodes;
using BookOfEternityClient.Core;

namespace BookOfEternityClient.Services;

internal static partial class WoundAcceptedTurnPlanner
{
    private static readonly object JointCommonAuthorityIssuer = new();
    private static readonly object JointCandidateAdmissionIssuer = new();

    internal sealed class MortalJointRepairCommonAuthority
    {
        private readonly JointResourcePublicationAuthority _resources;
        private readonly Dictionary<string, CanonicalBeforeImage> _before;
        private string? _planningInputFingerprint;
        private string? _bindingFingerprint;
        /// <summary>
        /// Captures the common authority from its genuine resource owner and agreeing progression source.
        /// </summary>
        /// <param name="issuer">
        /// The private common-authority issuer; any other object is refused.
        /// </param>
        /// <param name="resources">
        /// The complete private resource-publication authority, including its retained calculation.
        /// </param>
        /// <param name="progression">
        /// The genuine progression source whose seal and frame must agree with the resource owner's bundle.
        /// </param>
        internal MortalJointRepairCommonAuthority(object issuer, object resources, GameEngine.MortalJointProgressionSource progression)
        {
            using var heavyConstructorTiming = FileSystemManager.MeasureCurrentOperation("JointCommonAuthority.Construct");
            using var heavyOwnerTiming = FileSystemManager.MeasureCurrentOperation("JointCommonAuthority.Construct.CheckOwners");
            if (!ReferenceEquals(issuer, JointCommonAuthorityIssuer) ||
                resources is not JointResourcePublicationAuthority actual || !actual.HasValidSeal() ||
                !actual.HasRetainedCommonCalculation || !progression.HasValidSeal)
                throw new InvalidOperationException("Joint common input requires its private complete resource owner.");
            heavyOwnerTiming?.Dispose();
            _resources = actual;
            Progression = progression;
            using var heavyTimingJointCommonAuthorityConstructCaptureBeforeImages = FileSystemManager.MeasureCurrentOperation("JointCommonAuthority.Construct.CaptureBeforeImages");
            var binding = Bundle.Input.Binding;
            if (!progression.MatchesFrame(binding.SessionId, binding.RequestId, binding.SnapshotToken, binding.Turn))
                throw new InvalidDataException("The authentic progression basis belongs to a different joint original.");
            _before = Baseline.Mechanical.BeforeImages.ToDictionary(row => row.Key,
                row => new CanonicalBeforeImage(row.Value.Existed, row.Value.Bytes?.ToArray()), StringComparer.Ordinal);
            foreach (var row in progression.BeforeImages)
            {
                if (_before.TryGetValue(row.Key, out var existing) && existing.Fingerprint != row.Value.Fingerprint)
                    throw new InvalidDataException("Joint progression/common before-image disagreement: " + row.Key);
                _before[row.Key] = new(row.Value.Existed, row.Value.Bytes?.ToArray());
            }
            heavyTimingJointCommonAuthorityConstructCaptureBeforeImages?.Dispose();
            using var heavyTimingJointCommonAuthorityConstructCaptureOutputBasis = FileSystemManager.MeasureCurrentOperation("JointCommonAuthority.Construct.CaptureOutputBasis");
            var joint = ReadJoint();
            var publishedInterface = joint.ReadPublishedInterface();
            if (publishedInterface is not null)
            {
                var after = publishedInterface.AfterBytes;
                var image = new CanonicalBeforeImage(after is not null, after);
                if (_before.TryGetValue(MortalJointInterfacePublication.InterfacePath, out var existing) &&
                    existing.Fingerprint != image.Fingerprint)
                    throw new InvalidDataException("Joint I/common read-dependency disagreement.");
                _before[MortalJointInterfacePublication.InterfacePath] = image;
            }
            var command = joint.ReadAcquisitionCommands().Single();
            var history = WoundHistoryState.Parse(Bundle.FinalPlan.HistoryAfterImage?.ToJsonString(), WoundHistoryState.HistoryPath);
            var creates = history.State?.Transitions.Where(row => row.Kind == "create" && row.EventRef == command.Opportunity.EventRef &&
                row.SourceFingerprint == command.Opportunity.AuthorityFingerprint).ToArray();
            if (!history.IsValid || creates is not { Length: 1 } ||
                !Bundle.PreparedPlan.AllocatedWoundIds.Contains(creates[0].WoundId, StringComparer.Ordinal) ||
                !Bundle.PreparedPlan.AllocatedTransitionIds.Contains(creates[0].TransitionId, StringComparer.Ordinal))
                throw new InvalidDataException("Joint output basis requires the SAME prepared acquisition CREATE identities.");
            var localRef = command.Decision.GetProperty("woundRef").GetString() ?? throw new InvalidDataException("Missing sealed acquisition local ref.");
            var acquisition = Bundle.PreparedPlan.PreparedWounds.Single(wound => wound.WoundId == creates[0].WoundId);
            var decisionFingerprint = WoundOpportunityDecisionAuthority.ComputeDecisionFingerprint(command.Opportunity.AuthorityFingerprint,
                command.Opportunity.OpportunityId, command.Opportunity.EventRef, command.Opportunity.Owner, null,
                "materialize", acquisition.Severity.Rank, localRef);
            if (creates[0].OperationKey != "wound_operation_" + decisionFingerprint["sha256:".Length..])
                throw new InvalidDataException("The sealed acquisition local ref must reproduce the SAME CREATE operation.");
            OutputBasis = MortalJointAcceptedOutputBasis.Capture(localRef, creates[0].WoundId,
                creates[0].TransitionId, joint.ReadAcceptedNarrative(), publishedInterface);
            heavyTimingJointCommonAuthorityConstructCaptureOutputBasis?.Dispose();
            Fingerprint = ComputeFingerprint();
        }
        internal GameEngine.MortalJointProgressionSource Progression { get; }
        internal AcceptedMechanicsPlanner.CalculatedCommonArtifacts RetainedCommonCalculation =>
            _resources.RetainedCommonCalculation;
        internal AcceptedMechanicsCalculationInputs RetainedCalculationInputs => _resources.RetainedCalculationInputs;
        internal MortalJointAcceptedOutputBasis OutputBasis { get; }
        private JointCommonBaselineAuthority Baseline => _resources.Baseline;
        internal MortalItemAcceptedTurnNormalizationSnapshot ItemsForRegistry => Baseline.Items;
        internal object Continuation => Baseline.Continuation;
        internal object Reservation => Baseline.Reservation;
        /// <summary>
        /// Gets a fully validated detached copy of the common authority's retained wound bundle.
        /// </summary>
        internal AcceptedMechanicsWoundStageBundle Bundle
        {
            get
            {
                using var operationTiming = FileSystemManager.MeasureCurrentOperation("JointCommonAuthority.Bundle");
                return Baseline.Bundle.DetachedCopy();
            }
        }
        internal MortalWoundCanonicalAnchorPlan Anchors => Baseline.Anchors.DetachedCopy();
        internal MortalWoundTreatmentAcceptedStateAuthority Accepted => ReadJointRepairAcceptedState(Continuation);
        internal MortalWoundTreatmentAttemptRequest Request => ReadJoint().Request;
        internal MortalWoundTreatmentResolution Resolution => ReadJoint().Resolution;
        internal MortalWoundTreatmentResourceFinalization Finalization => ReadJoint().Finalization;
        internal string RepairCheckpointFingerprint => ReadJoint().Checkpoint.ToJsonObject()["fingerprint"]!.GetValue<string>();
        internal ResourceDefinitionCatalog Definitions => _resources.Definitions;
        internal ResourceStateLedger State => _resources.State;
        internal ResourceHistoryState History => _resources.History;
        internal ResourceOwnerAuthority Owners => _resources.FinalOwners;
        internal IResourceRegisteredSystemOutcomeDraft RegisteredOutcome => _resources;
        internal ResourceOwnerKey[] TerminalOwners => _resources.TerminalOwners;
        internal AcceptedMechanicsIdentityFactory CreateIdentityFactory() => _resources.CreateIdentityFactory();
        internal IReadOnlyDictionary<string, CanonicalBeforeImage> BeforeImages => _before.ToDictionary(row => row.Key,
            row => new CanonicalBeforeImage(row.Value.Existed, row.Value.Bytes?.ToArray()), StringComparer.Ordinal);
        internal IReadOnlyDictionary<string, JsonNode?> ExactFinalItemRoots => _resources.ExactFinalItemRoots;
        internal string Fingerprint { get; }
        private JointRepairContinuationAuthority ReadJoint() => TryReadJointRepair(Continuation, out var joint)
            ? joint : throw new InvalidDataException("The joint common continuation changed.");
        /// <summary>
        /// Computes the common authority seal from its retained owners, bundle, anchors, progression and output basis.
        /// </summary>
        /// <returns>
        /// The deterministic fingerprint used to verify this common authority.
        /// </returns>
        private string ComputeFingerprint()
        {
            using var operationTiming = FileSystemManager.MeasureCurrentOperation("JointCommonAuthority.ComputeFingerprint");
            return WoundAcceptedTurnFingerprintWriter.Compute(new string?[] {
                "book_of_eternity.mortal_joint_repair.common_authority.v1", _resources.Fingerprint,
                Baseline.Fingerprint, GetJointRepairContinuationFingerprint(Continuation), Bundle.BundleFingerprint, Anchors.Fingerprint,
                Progression.Fingerprint, OutputBasis.Fingerprint });
        }
        /// <summary>
        /// Checks the current retained resource, baseline, progression and common authority seals.
        /// </summary>
        /// <returns>
        /// <see langword="true"/> when every seal agrees; otherwise, <see langword="false"/>.
        /// </returns>
        internal bool HasValidSeal
        {
            get
            {
                using var operationTiming = FileSystemManager.MeasureCurrentOperation("JointCommonAuthority.HasValidSeal");
                return _resources.HasValidSeal() && _resources.HasRetainedCommonCalculation &&
                    Baseline.HasValidSeal && Progression.HasValidSeal && ComputeFingerprint() == Fingerprint;
            }
        }
        /// <summary>
        /// Checks the retained common seal and its exact wound, anchor and resource-owner agreement.
        /// </summary>
        /// <param name="bundle">
        /// The wound bundle to compare; a <see langword="null"/> value does not agree.
        /// </param>
        /// <param name="anchors">
        /// The anchor plan to compare; a <see langword="null"/> value does not agree.
        /// </param>
        /// <param name="owners">
        /// The resource-owner authority whose fingerprint must agree.
        /// </param>
        /// <returns>
        /// <see langword="true"/> when every original short-circuit check agrees; otherwise, <see langword="false"/>.
        /// </returns>
        internal bool AgreesWith(AcceptedMechanicsWoundStageBundle? bundle, MortalWoundCanonicalAnchorPlan? anchors,
            ResourceOwnerAuthority owners)
        {
            using var operationTiming = FileSystemManager.MeasureCurrentOperation("JointCommonAuthority.AgreesWith");
            return HasValidSeal && bundle is not null && anchors is not null &&
                ReferenceEquals(bundle.PreparedPlan.JointRepairContinuationAuthority, Continuation) &&
                bundle.BundleFingerprint == Bundle.BundleFingerprint && anchors.Fingerprint == Anchors.Fingerprint &&
                anchors.AgreesWith(bundle) && owners.Fingerprint == Owners.Fingerprint;
        }
        /// <summary>
        /// Binds the actual common input once, or checks an already-bound input without replacing its fingerprints.
        /// </summary>
        /// <param name="issuer">
        /// The private common-authority issuer.
        /// </param>
        /// <param name="input">
        /// The input whose genuine planning context and complete retained data must agree with this authority.
        /// </param>
        internal void BindInput(object issuer, AcceptedMechanicsInput input)
        {
            if (!ReferenceEquals(issuer, JointCommonAuthorityIssuer) ||
                !ReferenceEquals(input.PlanningContext?.JointRepairCommonAuthority, this) || !AgreesWithContext(input.PlanningContext))
                throw new InvalidOperationException("Joint common input must be bound once by its actual issuer.");
            if (_planningInputFingerprint is not null)
            {
                if (!MatchesInput(input)) throw new InvalidDataException("The already-bound joint input changed.");
                return;
            }
            using (FileSystemManager.MeasureCurrentOperation("JointCommonAuthority.BindInput.ComputePlanningFingerprint"))
                _planningInputFingerprint = AcceptedMechanicsPlanFingerprints.ComputePlanningInput(input);
            using (FileSystemManager.MeasureCurrentOperation("JointCommonAuthority.BindInput.ComputeBindingFingerprint"))
                _bindingFingerprint = AcceptedMechanicsPlanFingerprints.ComputeInput(input.CreateBinding());
        }
        /// <summary>
        /// Checks that the complete input still agrees with this authority's bound planning and binding fingerprints.
        /// </summary>
        /// <param name="input">
        /// The common input whose context, resources and fingerprints must still agree.
        /// </param>
        /// <returns>
        /// <see langword="true"/> when the sealed and bound input agrees; otherwise, <see langword="false"/>.
        /// </returns>
        internal bool MatchesInput(AcceptedMechanicsInput input)
        {
            using var operationTiming = FileSystemManager.MeasureCurrentOperation("JointCommonAuthority.MatchesInput");
            return HasValidSeal && _resources.RetainedInputAgrees(input) &&
                _planningInputFingerprint is not null &&
                ReferenceEquals(input.PlanningContext?.JointRepairCommonAuthority, this) && AgreesWithContext(input.PlanningContext) &&
                _planningInputFingerprint == AcceptedMechanicsPlanFingerprints.ComputePlanningInput(input) &&
                _bindingFingerprint == AcceptedMechanicsPlanFingerprints.ComputeInput(input.CreateBinding());
        }
        /// <summary>
        /// Checks the full wound, resource, owner and retained calculation context in its existing short-circuit order.
        /// </summary>
        /// <param name="context">
        /// The planning context to compare with the issued common authority.
        /// </param>
        /// <returns>
        /// <see langword="true"/> when all context checks agree; otherwise, <see langword="false"/>.
        /// </returns>
        internal bool AgreesWithContext(AcceptedMechanicsPlanningContext context)
        {
            using var operationTiming = FileSystemManager.MeasureCurrentOperation("JointCommonAuthority.AgreesWithContext");
            return AgreesWith(context.WoundStageBundle, context.WoundAnchorPlan, context.Owners) && _resources.RetainedContextAgrees(context) &&
                context.TreatmentResourcePublicationAuthority is null && context.DirectWoundPublicationAuthority is null &&
                context.State.Fingerprint == State.Fingerprint && context.History.Fingerprint == History.Fingerprint &&
                JsonNode.DeepEquals(context.DefinitionRoot, Definitions.ToCanonicalRoot()) &&
                context.RegisteredSystemOutcomes.Count == 1 && ReferenceEquals(context.RegisteredSystemOutcomes[0], RegisteredOutcome) &&
                ResourceMutationSourceCatalog.Create(RegisteredOutcome.SourceExports).Catalog?.Fingerprint == context.Sources.Fingerprint &&
                JsonNode.DeepEquals(context.Commands.Root, ResourceAcceptedTurnInputComposer.Parse(null).Root) &&
                AcceptedMechanicsCarrierAssembler.ValidateInitialEffectPlan(context.EffectPlan, Bundle).Count == 0 &&
                context.CapacityTransitions.Count == 0 && context.OwnerCapacityDrafts.Count == 0 &&
                context.OwnerCompanionAfterImages.Count == 0 && context.OwnerTransitions.Count == 0 &&
                context.ExecutionSequenceOffset == 0 && context.TerminalOwners.ToHashSet().SetEquals(TerminalOwners);
        }
        internal async Task ValidateCurrentAsync(FileSystemManager files, FileSystemManager.CanonicalWriteLease lease)
        {
            using var innerTiming1 = files.MeasureOperation("JointCommonAuthority.ValidateCurrentAsync");
            using var innerTiming2 = files.MeasureOperation("JointCommonAuthority.ValidateSeal");
            if (!HasValidSeal) throw new InvalidDataException("The private joint common authority changed.");
            innerTiming2?.Dispose();
            await ValidateJointRepairResourcePublicationAsync(files, lease, _resources);
            await Progression.ValidateCurrentAsync(files, lease);
        }
        internal async Task<IReadOnlyList<ValidationIssue>> ValidateCandidateResourceBoundsAsync(
            FileSystemManager files, FileSystemManager.CanonicalWriteLease lease, AcceptedMechanicsPlan candidate)
        {
            using var innerTiming3 = files.MeasureOperation("JointCommonAuthority.ValidateCandidateBoundsAsync");
            if (!ReferenceEquals(candidate.JointRepairCommonAuthority, this))
                return JointPreparationFailure("candidate_bounds_provenance", "Foreign joint candidate.").Issues;
            await ValidateCurrentAsync(files, lease);
            using var innerTiming4 = files.MeasureOperation("JointCommonAuthority.ProjectCandidateBounds");
            var definitions = ResourceDefinitionCatalog.ParseCanonical(candidate.DefinitionAfterImage.ToJsonString(), allowMissingPristine: false);
            if (definitions.Catalog is not { } catalog) return definitions.Issues;
            // Existing numeric/quantum/minimum/maximum and complete history replay
            // validations see the final aggregate result of the ONE reduction.
            var state = ResourceStateContract.ParseCanonical(candidate.StateAfterImage.ToJsonString(), catalog, allowMissingPristine: false);
            var history = ResourceHistoryState.ParseCanonical(candidate.HistoryAfterImage.ToJsonString(), catalog, allowMissingPristine: false);
            var issues = definitions.Issues.Concat(state.Issues).Concat(history.Issues).ToList();
            if (state.Ledger is not { } ledger || history.History is not { } typedHistory) return issues;
            issues.AddRange(typedHistory.ValidateStateAgreement(ledger));
            if (issues.Count != 0) return issues;
            var companions = candidate.OwnerCompanionAfterImages;
            var effects = candidate.EffectCarrierAfterImages;
            var wounds = candidate.WoundCarrierAfterImages;
            var exactItems = ExactFinalItemRoots;
            var guarded = BeforeImages;
            string? ReadFinal(string path)
            {
                if (wounds.TryGetValue(path, out var wound)) return wound.ToJsonString();
                if (companions.TryGetValue(path, out var companion))
                    return exactItems.TryGetValue(path, out var exact) && exact is JsonArray
                        ? MortalItemProjectionRootParser.RestorePublicationRoot(exact, companion, path).ToJsonString()
                        : companion.ToJsonString();
                if (effects.TryGetValue(path, out var effect)) return effect.ToJsonString();
                return guarded.TryGetValue(path, out var before)
                    ? before.Existed ? CanonicalJsonUtf8.DecodeOneOptionalBom(before.Bytes!) : null
                    : throw new InvalidDataException("Unowned final resource-bound source: " + path);
            }
            // The existing capacity-draft agreement is recomposed against COMPLETE
            // item/effect/wound successors, never the pre-acquisition candidate.
            var owners = await CanonicalResourceOwnerAuthorityComposer.ComposeAsync(catalog,
                path => Task.FromResult(ReadFinal(path)), ledger, typedHistory, CanonicalResourceOwnerAuthorityPurpose.FinalAfterImage);
            issues.AddRange(owners.Issues);
            if (owners.Authority is null || owners.Authority.Fingerprint != candidate.OwnerAuthority.Fingerprint)
                issues.AddRange(JointPreparationFailure("candidate_final_owner_bounds_changed", "The full candidate resource owner/capacity bounds differ.").Issues);
            innerTiming4?.Dispose();
            await ValidateCurrentAsync(files, lease);
            return issues;
        }

        internal IReadOnlyList<ValidationIssue> ValidateCandidateProjection(AcceptedMechanicsPlan candidate,
            MortalWoundTreatmentResourceFinalization recomposed)
        {
            if (!ReferenceEquals(candidate.JointRepairCommonAuthority, this) || !HasValidSeal || _planningInputFingerprint is null ||
                candidate.InputFingerprint != _planningInputFingerprint ||
                candidate.WoundStageBundle?.BundleFingerprint != Bundle.BundleFingerprint ||
                candidate.WoundAnchorPlanFingerprint != Anchors.Fingerprint ||
                candidate.TreatmentResourcePublicationAuthority is not null || candidate.DirectWoundPublicationAuthority is not null ||
                !MortalWoundTreatmentResourcePublicationAuthority.FinalizationMatches(Finalization, recomposed))
                return JointPreparationFailure("common_candidate_provenance", "Changed full joint candidate/finalization.").Issues;
            foreach (var row in BeforeImages)
                if (!candidate.BeforeImages.TryGetValue(row.Key, out var image) || image.Existed != row.Value.Existed ||
                    !(image.Bytes ?? Array.Empty<byte>()).AsSpan().SequenceEqual(row.Value.Bytes ?? Array.Empty<byte>()))
                    return JointPreparationFailure("common_candidate_guard_changed", row.Key).Issues;
            var resourceIssues = _resources.ValidateCandidateResourceProjection(candidate);
            if (resourceIssues.Count != 0) return resourceIssues;
            // Reassemble typed item -> effect -> wound ownership once as a pure
            // validation calculation. No second resource/effect reduction occurs.
            var expected = AcceptedMechanicsCarrierAssembler.Compose(candidate.EffectPlan, _resources.Companions, Bundle, Anchors);
            if (!expected.Success) return expected.Issues;
            var companions = candidate.OwnerCompanionAfterImages;
            if (companions.Count != expected.OwnerCompanionAfterImages.Count ||
                expected.OwnerCompanionAfterImages.Any(row => !companions.TryGetValue(row.Key, out var actual) ||
                    !JsonNode.DeepEquals(row.Value, actual)) ||
                expected.OwnerCompanionAfterImages.Keys.Any(path => !candidate.TouchedPaths.Contains(path, StringComparer.Ordinal)))
                return JointPreparationFailure("common_candidate_projection_changed", "Changed whole typed companion assembly.").Issues;
            // Empty supported skill commands were owner-proved; the full candidate
            // still exports the provider's actual final capability in the gate.
            foreach (var path in Baseline.Skills.Baselines.Keys)
                if (!candidate.BeforeImages.TryGetValue(path, out var image) ||
                    image.Existed != Baseline.Skills.Baselines[path].Existed ||
                    !(image.Bytes ?? Array.Empty<byte>()).AsSpan().SequenceEqual(Baseline.Skills.Baselines[path].Bytes ?? Array.Empty<byte>()))
                    return JointPreparationFailure("common_candidate_skill_baseline_changed", path).Issues;
            return Array.Empty<ValidationIssue>();
        }
    }

    private sealed record JointCandidateAdmissionProof(object Issuer, MortalJointRepairCommonAuthority Authority,
        AcceptedMechanicsPlan Candidate, string PreparedFingerprint);

    internal static bool JointCandidateAdmissionAgrees(object value, MortalJointRepairCommonAuthority authority,
        AcceptedMechanicsPlan candidate) => value is JointCandidateAdmissionProof proof &&
        ReferenceEquals(proof.Issuer, JointCandidateAdmissionIssuer) && ReferenceEquals(proof.Authority, authority) &&
        ReferenceEquals(proof.Candidate, candidate) && authority.HasValidSeal &&
        proof.PreparedFingerprint == candidate.PreparedPlanFingerprint &&
        proof.PreparedFingerprint == AcceptedMechanicsPlanFingerprints.ComputePrepared(candidate);

    /// <summary>
    /// Carries a genuinely bound common input and its issued authority, or the original preparation issues.
    /// Constructing this result does not issue authority or bypass current candidate validation.
    /// </summary>
    /// <param name="Input">
    /// The input bound by the normal common-input prefix, or <see langword="null"/> when preparation failed.
    /// </param>
    /// <param name="Authority">
    /// The genuine authority that bound <paramref name="Input"/>, or <see langword="null"/> on failure.
    /// </param>
    /// <param name="Issues">
    /// The original calculation or composition issues, or the normal failure issues for a handled exception.
    /// </param>
    internal sealed record JointRepairBoundCommonInputResult(
        AcceptedMechanicsInput? Input,
        MortalJointRepairCommonAuthority? Authority,
        IReadOnlyList<ValidationIssue> Issues)
    {
        /// <summary>
        /// Determines whether preparation returned both the bound input and genuine authority without issues.
        /// </summary>
        /// <returns>
        /// <see langword="true"/> when both values are present and there are no issues; otherwise,
        /// <see langword="false"/>.
        /// </returns>
        internal bool Success => Input is not null && Authority is not null && Issues.Count == 0;
    }

    /// <summary>
    /// Retains actual authority issuance so a later preparation exception follows the normal abort path.
    /// </summary>
    private sealed class JointRepairCommonInputPreparationState
    {
        /// <summary>
        /// Gets or sets the authority returned by the real common-authority factory, remaining
        /// <see langword="null"/> until that factory succeeds.
        /// </summary>
        internal MortalJointRepairCommonAuthority? Authority { get; set; }
    }

    /// <summary>
    /// Runs the normal common-input prefix for a component caller without creating or sealing a candidate.
    /// Handled preparation exceptions abort an issued authority before returning normal failure issues;
    /// an abort exception or an exception outside the normal filter propagates to the caller.
    /// </summary>
    /// <param name="files">
    /// The physical filesystem owning the actual resource and progression authorities.
    /// </param>
    /// <param name="lease">
    /// The active canonical write lease owned and eventually closed by the caller.
    /// </param>
    /// <param name="resourceAuthority">
    /// The genuine resource-publication authority validated by the unchanged normal prefix.
    /// </param>
    /// <param name="progression">
    /// The genuine progression source validated by the unchanged normal prefix.
    /// </param>
    /// <returns>
    /// The actually bound input and common authority on success, or the original preparation issues.
    /// Ordinary issue returns preserve the normal prefix's absence of an exception-driven abort.
    /// </returns>
    internal static async Task<JointRepairBoundCommonInputResult> PrepareJointRepairBoundCommonInputForComponentAsync(
        FileSystemManager files, FileSystemManager.CanonicalWriteLease lease, object resourceAuthority,
        GameEngine.MortalJointProgressionSource progression)
    {
        var preparation = new JointRepairCommonInputPreparationState();
        try
        {
            return await PrepareJointRepairBoundCommonInputCoreAsync(files, lease, resourceAuthority, progression, preparation);
        }
        catch (Exception error) when (error is InvalidDataException or InvalidOperationException or ArgumentException or
            System.Text.Json.JsonException or IOException or OverflowException)
        {
            if (preparation.Authority is { } authority) AcceptedTurnAuthorityRegistry.AbortMortalJointRepairPublication(files, lease,
                authority.Continuation, authority.Reservation);
            return new(null, null, JointPreparationFailure("common_plan_invalid", error.Message).Issues);
        }
    }

    /// <summary>
    /// Runs the shared normal validation, retained calculation, authority issuance, composition and input binding.
    /// </summary>
    /// <param name="files">
    /// The physical filesystem owning the actual resource and progression authorities.
    /// </param>
    /// <param name="lease">
    /// The caller-owned active canonical write lease used by every unchanged prefix operation.
    /// </param>
    /// <param name="resourceAuthority">
    /// The actual resource-publication authority to validate before accessing its retained calculation.
    /// </param>
    /// <param name="progression">
    /// The actual progression source to validate before common calculation and authority issuance.
    /// </param>
    /// <param name="preparation">
    /// Private mutable state that retains the issued authority for the caller's normal exception-driven abort.
    /// </param>
    /// <returns>
    /// The bound input and genuine common authority, or the original calculation or composition issues.
    /// Exceptions propagate to the calling entry's unchanged filter and abort handling.
    /// </returns>
    private static async Task<JointRepairBoundCommonInputResult> PrepareJointRepairBoundCommonInputCoreAsync(
        FileSystemManager files, FileSystemManager.CanonicalWriteLease lease, object resourceAuthority,
        GameEngine.MortalJointProgressionSource progression, JointRepairCommonInputPreparationState preparation)
    {
        await ValidateJointRepairResourcePublicationAsync(files, lease, resourceAuthority);
        await progression.ValidateCurrentAsync(files, lease);
        var resources = (JointResourcePublicationAuthority)resourceAuthority;
        var calculationIssues = await resources.EnsureCommonCalculationAsync(JointCommonCalculationIssuer, files, lease);
        if (calculationIssues.Count != 0) return new(null, null, calculationIssues);
        using var innerTiming5 = files.MeasureOperation("JointCommon.CreateAuthority");
        MortalJointRepairCommonAuthority authority;
        using (files.EnterOperationTimingContext())
        {
            authority = resources.GetOrCreateCommonAuthority(JointCommonAuthorityIssuer, progression);
            preparation.Authority = authority;
        }
        innerTiming5?.Dispose();
        using var innerTiming6 = files.MeasureOperation("JointCommon.ComposeInputAsync");
        var composed = await AcceptedMechanicsWoundCommonInputComposer.ComposeJointRepairAsync(files, lease, authority);
        innerTiming6?.Dispose();
        if (!composed.Success || composed.Input is null) return new(null, null, composed.Issues);
        using var innerTiming7 = files.MeasureOperation("JointCommon.BindInput");
        using (files.EnterOperationTimingContext())
            authority.BindInput(JointCommonAuthorityIssuer, composed.Input);
        innerTiming7?.Dispose();
        return new(composed.Input, authority, composed.Issues);
    }

    // One owner constructs the common input and receives a raw, untakeable cache
    // candidate. Fresh physical/source/capability checks precede sealing that SAME
    // candidate under its unchanged cache fence and current reservation.
    /// <summary>
    /// Creates a raw common candidate from the normal bound input, then applies the existing fresh admission
    /// and resource-bound checks before sealing that same candidate under its current fence and reservation.
    /// Handled exceptions abort any issued common authority; abort exceptions propagate to the caller.
    /// </summary>
    /// <param name="files">
    /// The physical filesystem owning the actual resource and progression authorities.
    /// </param>
    /// <param name="lease">
    /// The active canonical write lease owned and eventually closed by the caller.
    /// </param>
    /// <param name="resourceAuthority">
    /// The genuine resource-publication authority validated by the shared normal prefix.
    /// </param>
    /// <param name="progression">
    /// The genuine progression source validated by the shared normal prefix.
    /// </param>
    /// <returns>
    /// The normal sealed planning result, an unchanged early issue result, or normal handled-exception issues.
    /// </returns>
    internal static async Task<AcceptedMechanicsPlanningResult> CreateJointRepairCommonPlanAsync(
        FileSystemManager files, FileSystemManager.CanonicalWriteLease lease, object resourceAuthority,
        GameEngine.MortalJointProgressionSource progression)
    {
        var preparation = new JointRepairCommonInputPreparationState();
        try
        {
            var composed = await PrepareJointRepairBoundCommonInputCoreAsync(files, lease, resourceAuthority, progression, preparation);
            if (!composed.Success || composed.Input is null) return new(null, composed.Issues);
            var authority = composed.Authority!;
            using var innerTiming8 = files.MeasureOperation("JointCommon.CreateRegistryCandidate");
            var raw = AcceptedTurnAuthorityRegistry.CreateJointRepairCommonCandidate(files, lease, composed.Input, authority);
            innerTiming8?.Dispose();
            if (!raw.Result.Success || raw.Result.Plan is not { } candidate || raw.Fence is null) return raw.Result;
            using var innerTiming9 = files.MeasureOperation("JointCommon.CandidateAdmissionGate");
            var issues = MortalWoundTreatmentCapabilityAuthority.CandidateAdmissionGate.ValidateJoint(authority, candidate);
            innerTiming9?.Dispose();
            if (issues.Count != 0)
            {
                AcceptedTurnAuthorityRegistry.RevokeJointRepairCommonCandidate(files, lease, authority, raw.Fence);
                return new(null, issues.Select(MortalWoundOpportunityAdapter.PrivateSourceIssue).ToArray());
            }
            var boundIssues = await authority.ValidateCandidateResourceBoundsAsync(files, lease, candidate);
            if (boundIssues.Count != 0)
            {
                AcceptedTurnAuthorityRegistry.RevokeJointRepairCommonCandidate(files, lease, authority, raw.Fence);
                return new(null, boundIssues.Select(MortalWoundOpportunityAdapter.PrivateSourceIssue).ToArray());
            }
            await authority.ValidateCurrentAsync(files, lease);
            var proof = new JointCandidateAdmissionProof(JointCandidateAdmissionIssuer, authority, candidate, candidate.PreparedPlanFingerprint);
            using var innerTiming10 = files.MeasureOperation("JointCommon.SealRegistryCandidate");
            return AcceptedTurnAuthorityRegistry.SealJointRepairCommonCandidate(files, lease, composed.Input, authority,
                raw.Fence, proof, candidate);
        }
        catch (Exception error) when (error is InvalidDataException or InvalidOperationException or ArgumentException or
            System.Text.Json.JsonException or IOException or OverflowException)
        {
            if (preparation.Authority is { } authority) AcceptedTurnAuthorityRegistry.AbortMortalJointRepairPublication(files, lease,
                authority.Continuation, authority.Reservation);
            return new(null, JointPreparationFailure("common_plan_invalid", error.Message).Issues);
        }
    }
}
