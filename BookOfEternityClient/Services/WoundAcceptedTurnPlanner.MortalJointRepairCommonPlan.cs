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
        internal MortalJointRepairCommonAuthority(object issuer, object resources, GameEngine.MortalJointProgressionSource progression)
        {
            if (!ReferenceEquals(issuer, JointCommonAuthorityIssuer) ||
                resources is not JointResourcePublicationAuthority actual || !actual.HasValidSeal() ||
                !actual.HasRetainedCommonCalculation || !progression.HasValidSeal)
                throw new InvalidOperationException("Joint common input requires its private complete resource owner.");
            _resources = actual;
            Progression = progression;
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
        internal AcceptedMechanicsWoundStageBundle Bundle => Baseline.Bundle.DetachedCopy();
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
        private string ComputeFingerprint() => WoundAcceptedTurnFingerprintWriter.Compute(new string?[] {
            "book_of_eternity.mortal_joint_repair.common_authority.v1", _resources.Fingerprint,
            Baseline.Fingerprint, GetJointRepairContinuationFingerprint(Continuation), Bundle.BundleFingerprint, Anchors.Fingerprint,
            Progression.Fingerprint, OutputBasis.Fingerprint });
        internal bool HasValidSeal => _resources.HasValidSeal() && _resources.HasRetainedCommonCalculation &&
            Baseline.HasValidSeal && Progression.HasValidSeal && ComputeFingerprint() == Fingerprint;
        internal bool AgreesWith(AcceptedMechanicsWoundStageBundle? bundle, MortalWoundCanonicalAnchorPlan? anchors,
            ResourceOwnerAuthority owners) => HasValidSeal && bundle is not null && anchors is not null &&
            ReferenceEquals(bundle.PreparedPlan.JointRepairContinuationAuthority, Continuation) &&
            bundle.BundleFingerprint == Bundle.BundleFingerprint && anchors.Fingerprint == Anchors.Fingerprint &&
            anchors.AgreesWith(bundle) && owners.Fingerprint == Owners.Fingerprint;
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
            _planningInputFingerprint = AcceptedMechanicsPlanFingerprints.ComputePlanningInput(input);
            _bindingFingerprint = AcceptedMechanicsPlanFingerprints.ComputeInput(input.CreateBinding());
        }
        internal bool MatchesInput(AcceptedMechanicsInput input) => HasValidSeal && _resources.RetainedInputAgrees(input) &&
            _planningInputFingerprint is not null &&
            ReferenceEquals(input.PlanningContext?.JointRepairCommonAuthority, this) && AgreesWithContext(input.PlanningContext) &&
            _planningInputFingerprint == AcceptedMechanicsPlanFingerprints.ComputePlanningInput(input) &&
            _bindingFingerprint == AcceptedMechanicsPlanFingerprints.ComputeInput(input.CreateBinding());
        internal bool AgreesWithContext(AcceptedMechanicsPlanningContext context) =>
            AgreesWith(context.WoundStageBundle, context.WoundAnchorPlan, context.Owners) && _resources.RetainedContextAgrees(context) &&
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
        internal async Task ValidateCurrentAsync(FileSystemManager files, FileSystemManager.CanonicalWriteLease lease)
        {
            if (!HasValidSeal) throw new InvalidDataException("The private joint common authority changed.");
            await ValidateJointRepairResourcePublicationAsync(files, lease, _resources);
            await Progression.ValidateCurrentAsync(files, lease);
        }
        internal async Task<IReadOnlyList<ValidationIssue>> ValidateCandidateResourceBoundsAsync(
            FileSystemManager files, FileSystemManager.CanonicalWriteLease lease, AcceptedMechanicsPlan candidate)
        {
            if (!ReferenceEquals(candidate.JointRepairCommonAuthority, this))
                return JointPreparationFailure("candidate_bounds_provenance", "Foreign joint candidate.").Issues;
            await ValidateCurrentAsync(files, lease);
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

    // One owner constructs the common input and receives a raw, untakeable cache
    // candidate. Fresh physical/source/capability checks precede sealing that SAME
    // candidate under its unchanged cache fence and current reservation.
    internal static async Task<AcceptedMechanicsPlanningResult> CreateJointRepairCommonPlanAsync(
        FileSystemManager files, FileSystemManager.CanonicalWriteLease lease, object resourceAuthority,
        GameEngine.MortalJointProgressionSource progression)
    {
        MortalJointRepairCommonAuthority? authority = null;
        try
        {
            await ValidateJointRepairResourcePublicationAsync(files, lease, resourceAuthority);
            await progression.ValidateCurrentAsync(files, lease);
            var resources = (JointResourcePublicationAuthority)resourceAuthority;
            var calculationIssues = await resources.EnsureCommonCalculationAsync(JointCommonCalculationIssuer, files, lease);
            if (calculationIssues.Count != 0) return new(null, calculationIssues);
            authority = resources.GetOrCreateCommonAuthority(JointCommonAuthorityIssuer, progression);
            var composed = await AcceptedMechanicsWoundCommonInputComposer.ComposeJointRepairAsync(files, lease, authority);
            if (!composed.Success || composed.Input is null) return new(null, composed.Issues);
            authority.BindInput(JointCommonAuthorityIssuer, composed.Input);
            var raw = AcceptedTurnAuthorityRegistry.CreateJointRepairCommonCandidate(files, lease, composed.Input, authority);
            if (!raw.Result.Success || raw.Result.Plan is not { } candidate || raw.Fence is null) return raw.Result;
            var issues = MortalWoundTreatmentCapabilityAuthority.CandidateAdmissionGate.ValidateJoint(authority, candidate);
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
            return AcceptedTurnAuthorityRegistry.SealJointRepairCommonCandidate(files, lease, composed.Input, authority,
                raw.Fence, proof, candidate);
        }
        catch (Exception error) when (error is InvalidDataException or InvalidOperationException or ArgumentException or
            System.Text.Json.JsonException or IOException or OverflowException)
        {
            if (authority is not null) AcceptedTurnAuthorityRegistry.AbortMortalJointRepairPublication(files, lease,
                authority.Continuation, authority.Reservation);
            return new(null, JointPreparationFailure("common_plan_invalid", error.Message).Issues);
        }
    }
}
