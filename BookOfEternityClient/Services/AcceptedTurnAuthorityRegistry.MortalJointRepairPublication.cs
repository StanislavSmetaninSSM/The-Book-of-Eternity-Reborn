using BookOfEternityClient.Core;

namespace BookOfEternityClient.Services;

internal static partial class AcceptedTurnAuthorityRegistry
{
    private static readonly object JointRepairPublicationReceiptCapability = new();
    internal static bool IsJointRepairPublicationReceiptCapability(object capability) =>
        ReferenceEquals(capability, JointRepairPublicationReceiptCapability);

    internal sealed record JointRepairPublicationTakeResult(MortalJointRepairPublicationTakeReceipt? Receipt,
        IReadOnlyList<ValidationIssue> Issues)
    {
        internal bool Success => Receipt is not null && Issues.Count == 0;
    }
    internal sealed record JointRepairPublicationTakenProbe(bool IsValid, IReadOnlyList<ValidationIssue> Issues,
        MortalWoundTreatmentResourceReservationState? State);

    internal static async Task<JointRepairPublicationTakeResult> TakeJointRepairPublicationAsync(
        FileSystemManager files, FileSystemManager.CanonicalWriteLease lease,
        MortalJointRepairPublicationPreparation preparation)
    {
        // Full source/before-image proof precedes BOTH cache takes. The owning
        // lease remains participating through the future batch and settlement.
        await preparation.ValidateInventoryCurrentAsync(files, lease);
        await preparation.Authority.ValidateCurrentAsync(files, lease);
        if (!preparation.Authority.Accepted.IsLeaseBoundTo(files, lease))
            throw new InvalidDataException("Joint publication requires its current participating accepted-state lease.");
        return GetState(files, lease).TakeJointRepairPublication(files, lease, preparation);
    }

    internal static JointRepairPublicationTakenProbe ProbeTakenJointRepairPublication(
        FileSystemManager files, FileSystemManager.CanonicalWriteLease lease,
        MortalJointRepairPublicationTakeReceipt receipt) =>
        GetState(files, lease).ProbeTakenJointRepairPublication(files, lease, receipt);

    internal static MortalJointRepairPublicationSettlement CompleteTakenJointRepairPublication(FileSystemManager files,
        FileSystemManager.CanonicalWriteLease lease, MortalJointRepairPublicationTakeReceipt receipt,
        MortalJointRepairPublicationTransaction.ReadbackProof proof) =>
        GetState(files, lease).CompleteTakenJointRepairPublication(files, lease, receipt, proof);

    internal static async Task<MortalJointRepairPublicationSettlement> RearmTakenJointRepairPublicationAsync(
        FileSystemManager files, FileSystemManager.CanonicalWriteLease lease,
        MortalJointRepairPublicationTakeReceipt receipt)
    {
        // Called only after the future coordinator compensates the WHOLE union.
        // Current guarded bytes and the final candidate proof are mandatory;
        // generic old rearm cannot populate the joint validated cache slot.
        await receipt.Preparation.ValidateInventoryCurrentAsync(files, lease);
        var issues = MortalWoundTreatmentCapabilityAuthority.CandidateAdmissionGate.ValidateJoint(receipt.Authority, receipt.Plan);
        if (issues.Count != 0)
            return new(MortalJointRepairPublicationDisposition.PublicationUncertain,
                issues.Select(MortalWoundOpportunityAdapter.PrivateSourceIssue).ToArray());
        return GetState(files, lease).RearmTakenJointRepairPublication(files, lease, receipt);
    }

    private sealed partial class AcceptedTurnAuthorityState
    {
        private MortalJointRepairPublicationTakeReceipt? _openJointRepairPublicationReceipt;

        internal JointRepairPublicationTakeResult TakeJointRepairPublication(FileSystemManager files,
            FileSystemManager.CanonicalWriteLease lease, MortalJointRepairPublicationPreparation preparation)
        {
            lock (_gate)
            {
                var authority = preparation.Authority;
                if (_openTreatmentPublicationReceipt is not null || _openJointRepairPublicationReceipt is not null ||
                    _treatmentPublicationRestartBlockerFingerprint is not null ||
                    !ReferenceEquals(_validatedJointRepairCommonAuthority, authority) ||
                    !ReferenceEquals(_validatedJointRepairCommonPlan, preparation.Plan) ||
                    !preparation.AgreesWith(files, lease, preparation.Plan, preparation.Binding) ||
                    preparation.Generation != _sessionGeneration ||
                    !OwnsJointRepairPublicationReservation(authority.Continuation, authority.Reservation) ||
                    !ConfirmsMortalJointRepairItemBaseline(authority.Continuation, authority.Reservation, authority.ItemsForRegistry))
                    return new(null, JointTakeIssue("current joint owner/item slot/reservation changed"));
                var hold = _treatmentResources.ProbeConfirmed(TreatmentResourceRegistryCapability, authority.Request, authority.Finalization);
                if (!hold.IsValid) return new(null, JointHoldIssues(hold));
                if (!_mortalItems.TryTakeValidatedTreatmentPublication(authority.ItemsForRegistry, out var items))
                    return new(null, JointTakeIssue("actual item baseline take refused"));
                if (!_commonPlan.TryTakeValidatedJointPublication(preparation, out var result, out var common) ||
                    !result.Success || !ReferenceEquals(result.Plan, preparation.Plan))
                {
                    if (!_mortalItems.TryRearmValidatedTreatmentPublication(items)) _mortalItems.InvalidateValidated();
                    return new(null, JointTakeIssue("exact joint common cache take refused"));
                }
                var receipt = MortalJointRepairPublicationTakeReceipt.Mint(JointRepairPublicationReceiptCapability,
                    files, _authorityStateToken, _sessionGeneration, _sessionGenerationRevision, preparation, common, items);
                _openJointRepairPublicationReceipt = receipt;
                _effectPlan.InvalidateAll(); ClearWoundEffectCore(); _woundPlan.InvalidateAll();
                return new(receipt, Array.Empty<ValidationIssue>());
            }
        }

        internal JointRepairPublicationTakenProbe ProbeTakenJointRepairPublication(FileSystemManager files,
            FileSystemManager.CanonicalWriteLease lease, MortalJointRepairPublicationTakeReceipt receipt)
        {
            lock (_gate)
            {
                if (!JointReceiptCurrent(files, lease, receipt))
                    return new(false, JointTakeIssue("stale or foreign joint take receipt/cache fence"), null);
                var hold = _treatmentResources.ProbeConfirmed(TreatmentResourceRegistryCapability,
                    receipt.Authority.Request, receipt.Authority.Finalization);
                return new(hold.IsValid, hold.IsValid ? Array.Empty<ValidationIssue>() : JointHoldIssues(hold), hold.State);
            }
        }

        internal MortalJointRepairPublicationSettlement CompleteTakenJointRepairPublication(FileSystemManager files,
            FileSystemManager.CanonicalWriteLease lease, MortalJointRepairPublicationTakeReceipt receipt,
            MortalJointRepairPublicationTransaction.ReadbackProof proof)
        {
            lock (_gate)
            {
                if (!JointReceiptCurrent(files, lease, receipt) || !proof.AgreesWith(receipt))
                    return new(MortalJointRepairPublicationDisposition.PublicationUncertain, JointTakeIssue("exact complete readback/receipt changed"));
                var hold = _treatmentResources.ProbeConfirmed(TreatmentResourceRegistryCapability, receipt.Authority.Request, receipt.Authority.Finalization);
                if (!hold.IsValid) return new(MortalJointRepairPublicationDisposition.PublicationUncertain, JointHoldIssues(hold));
                var committed = _treatmentResources.Commit(TreatmentResourceRegistryCapability, receipt.Authority.Finalization);
                if (!committed.IsValid || committed.ChangedCount != 1)
                    return new(MortalJointRepairPublicationDisposition.PublicationUncertain,
                        committed.Issues.Count != 0 ? committed.Issues.Select(MortalWoundOpportunityAdapter.PrivateSourceIssue).ToArray() :
                            JointTakeIssue("original resource claim did not finalize exactly once"));
                if (!receipt.TryConsume(JointRepairPublicationReceiptCapability))
                    throw new InvalidOperationException("The exact current joint receipt was consumed twice at settlement.");
                _commonPlan.InvalidateAll(); _mortalItems.InvalidateValidated();
                ClearMortalWoundTreatmentPublicationReservationCore();
                return new(MortalJointRepairPublicationDisposition.Committed, Array.Empty<ValidationIssue>());
            }
        }

        internal MortalJointRepairPublicationSettlement RearmTakenJointRepairPublication(FileSystemManager files,
            FileSystemManager.CanonicalWriteLease lease, MortalJointRepairPublicationTakeReceipt receipt)
        {
            lock (_gate)
            {
                if (!JointReceiptCurrent(files, lease, receipt))
                    return new(MortalJointRepairPublicationDisposition.PublicationUncertain,
                        JointTakeIssue("stale joint receipt cannot rearm a cache slot"));
                var hold = _treatmentResources.ProbeConfirmed(TreatmentResourceRegistryCapability,
                    receipt.Authority.Request, receipt.Authority.Finalization);
                if (!hold.IsValid) return new(MortalJointRepairPublicationDisposition.PublicationUncertain, JointHoldIssues(hold));
                if (_mortalItems.TryRearmValidatedTreatmentPublication(receipt.Items) &&
                    _commonPlan.TryRearmValidatedJointPublication(receipt.Preparation, receipt.Common))
                {
                    if (!receipt.TryConsume(JointRepairPublicationReceiptCapability))
                        throw new InvalidOperationException("The exact current joint receipt was consumed twice.");
                    _openJointRepairPublicationReceipt = null;
                    return new(MortalJointRepairPublicationDisposition.RetryableHeld, Array.Empty<ValidationIssue>());
                }
                // Known cache refusal is NOT claim release or publication uncertainty.
                // Byte compensation was proved above; retain the full durable request
                // and confirmed agreement for genuine owner reconstruction.
                _mortalItems.InvalidateValidated(); _commonPlan.InvalidateAll();
                _validatedJointRepairCommonAuthority = null; _validatedJointRepairCommonPlan = null;
                receipt.TryConsume(JointRepairPublicationReceiptCapability);
                _openJointRepairPublicationReceipt = null;
                return new(MortalJointRepairPublicationDisposition.RetryableHeld,
                    JointTakeIssue("exact rearm refused; the full held graph requires owner reconstruction"));
            }
        }

        private bool JointReceiptCurrent(FileSystemManager files, FileSystemManager.CanonicalWriteLease lease,
            MortalJointRepairPublicationTakeReceipt receipt) =>
            ReferenceEquals(_openJointRepairPublicationReceipt, receipt) && ReferenceEquals(receipt.FileSystem, files) &&
            ReferenceEquals(receipt.StateToken, _authorityStateToken) && receipt.Generation == _sessionGeneration &&
            receipt.GenerationRevision == _sessionGenerationRevision && !receipt.IsConsumed && receipt.HasValidSeal &&
            receipt.Preparation.AgreesWith(files, lease, receipt.Plan, receipt.Common.Binding) &&
            ReferenceEquals(_validatedJointRepairCommonAuthority, receipt.Authority) &&
            ReferenceEquals(_validatedJointRepairCommonPlan, receipt.Plan) &&
            ReferenceEquals(_mortalWoundTreatmentAcceptedState, receipt.Authority.Accepted) &&
            JointRepairPublicationReservationAgrees(receipt.Authority.Continuation, receipt.Authority.Reservation) &&
            _commonPlan.IsTreatmentPublicationTakeCurrent(receipt.Common) && _mortalItems.IsTreatmentPublicationTakeCurrent(receipt.Items);

        private static IReadOnlyList<ValidationIssue> JointHoldIssues(MortalWoundTreatmentResourceConfirmedHoldProbe hold) =>
            hold.Issues.Count != 0 ? hold.Issues.Select(MortalWoundOpportunityAdapter.PrivateSourceIssue).ToArray() :
                JointTakeIssue("confirmed claim refused: " + hold.State);
        private static IReadOnlyList<ValidationIssue> JointTakeIssue(string reason) =>
            [MortalWoundOpportunityAdapter.PrivateSourceIssue(WoundAcceptedTurnPlannerCore.NewIssue(
                "mortal_joint_repair_publication_take_refused", reason, "the exact private candidate/receipt and confirmed agreement", "refused"))];
    }
}
