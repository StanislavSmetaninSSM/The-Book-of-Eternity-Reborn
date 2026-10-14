using BookOfEternityClient.Core;

namespace BookOfEternityClient.Services;

public partial class CanonicalStateNormalizer
{
    private static readonly object JointBatchPublicationIssuer = new();
    internal static bool IsJointBatchPublicationIssuer(object issuer) => ReferenceEquals(issuer, JointBatchPublicationIssuer);
    internal sealed class JointRepairPublicationPreflight
    {
        private JointRepairPublicationPreflight(AcceptedMechanicsNormalizationPreflight.Validated validated,
            MortalJointRepairPublicationPreparation preparation)
        { Validated = validated; Preparation = preparation; }
        internal AcceptedMechanicsNormalizationPreflight.Validated Validated { get; }
        internal MortalJointRepairPublicationPreparation Preparation { get; }

        internal static async Task<JointRepairPublicationPreflight> CreateAsync(CanonicalStateNormalizer owner,
            AcceptedMechanicsNormalizationPreflight.Validated validated)
        {
            var lease = owner._writeLease ?? throw new InvalidOperationException("Joint coordinator requires the participating issuing lease.");
            var preparation = await MortalJointRepairPublicationPreparation.CreateAsync(owner._fs, lease,
                validated.Plan, validated.Binding);
            return new(validated, preparation);
        }
    }

    // The distinct coordinator must retain this SAME bound lease while building,
    // taking, publishing, reading back and settling. The ordinary coordinator's
    // separately acquired lease cannot inherit the mechanical admission proof.
    internal async Task<JointRepairPublicationPreflight?> PrevalidateJointRepairPublicationTransactionAsync()
    {
        var preflight = await PrevalidateAcceptedMechanicsBeforeNormalizationAsync();
        return preflight is AcceptedMechanicsNormalizationPreflight.Validated validated &&
            validated.Plan.JointRepairCommonAuthority is not null
            ? await JointRepairPublicationPreflight.CreateAsync(this, validated) : null;
    }

    internal Task<AcceptedTurnAuthorityRegistry.JointRepairPublicationTakeResult> TakeJointRepairPublicationAsync(
        JointRepairPublicationPreflight preflight) =>
        AcceptedTurnAuthorityRegistry.TakeJointRepairPublicationAsync(_fs,
            _writeLease ?? throw new InvalidOperationException("Joint take requires its participating issuing lease."),
            preflight.Preparation);

    private async Task ValidateTakenJointPublicationInputsAsync(MortalJointRepairPublicationTakeReceipt receipt)
    {
        var lease = _writeLease ?? throw new InvalidOperationException("Joint publication requires its issuing lease.");
        await receipt.Preparation.ValidateInventoryCurrentAsync(_fs, lease);
        var probe = AcceptedTurnAuthorityRegistry.ProbeTakenJointRepairPublication(_fs, lease, receipt);
        if (!probe.IsValid) throw new InvalidDataException(string.Join(";", probe.Issues.Select(row => row.Code + ": " + row.FilePath)));
        var plan = receipt.Plan;
        var bundle = receipt.Authority.Bundle;
        if (plan.EffectPlan is null || plan.WoundStageBundle is null || !receipt.HasValidSeal ||
            plan.WoundStageBundle.BundleFingerprint != bundle.BundleFingerprint ||
            WoundAcceptedTurnFingerprints.ComputeAcceptedEffectPlanPayload(plan.EffectPlan) !=
                WoundAcceptedTurnFingerprints.ComputeAcceptedEffectPlanPayload(bundle.EffectBatchPlan.EffectPlan) ||
            !receipt.Authority.AgreesWith(plan.WoundStageBundle, receipt.Authority.Anchors, plan.OwnerAuthority))
            throw new InvalidDataException("Joint publication lost its exclusive complete effect/wound/item lineage.");
        // Preflight validated the complete effect/source/target stages and final
        // aggregate bounds. This taken capability additionally proves exact
        // unchanged original/current bytes and the actual vacant item cache slot.
        // It never uses the old singleton normalized-source fallback.
        var finalIssues = MortalWoundTreatmentCapabilityAuthority.CandidateAdmissionGate.ValidateJoint(receipt.Authority, plan);
        if (finalIssues.Count != 0) throw new InvalidDataException(string.Join(";", finalIssues.Select(row => row.Code + ": " + row.FilePath)));
    }

    internal sealed record JointRepairPublicationResult(MortalJointRepairPublicationDisposition Disposition,
        AcceptedMechanicsPlan? Plan, IReadOnlyList<ValidationIssue> Issues);

    // The actual GameEngine entry is connected later. This owner is deliberately
    // bound to the SAME participating issuing lease, with no independent writes,
    // accumulated-state normalization, second reduction or second scheduler.
    internal async Task<JointRepairPublicationResult> NormalizeAndValidateJointRepairAsync(
        JointRepairPublicationPreflight preflight, ValidationService validator,
        IReadOnlyDictionary<string, string> backups)
    {
        var lease = _writeLease ?? throw new InvalidOperationException("Joint coordinator requires its issuing lease.");
        AcceptedTurnAuthorityRegistry.JointRepairPublicationTakeResult taken;
        try { taken = await TakeJointRepairPublicationAsync(preflight); }
        catch (Exception error) when (error is InvalidDataException or IOException or InvalidOperationException or ArgumentException or System.Text.Json.JsonException)
        { return new(MortalJointRepairPublicationDisposition.PublicationUncertain, null, JointPublicationIssue(error)); }
        if (!taken.Success || taken.Receipt is null)
            return new(MortalJointRepairPublicationDisposition.PublicationUncertain, null, taken.Issues);
        MortalJointRepairPublicationTransaction transaction;
        try { transaction = MortalJointRepairPublicationTransaction.Create(_fs, lease, taken.Receipt); }
        catch (Exception error) when (error is InvalidDataException or IOException or InvalidOperationException or ArgumentException)
        { return new(MortalJointRepairPublicationDisposition.PublicationUncertain, null, JointPublicationIssue(error)); }
        try
        {
            var plan = await PublishAcceptedMechanicsAsync(backups, null, preflight.Validated,
                normalizedAcceptedCarrierBaselines: false, jointTransaction: transaction) ??
                throw new InvalidDataException("Joint common publication returned no plan.");
            var issues = new List<ValidationIssue>();
            issues.AddRange(await validator.ValidateAcceptedTurnCanonicalMortalLocationMaterializationAsync(lease));
            issues.AddRange(await validator.ValidateAcceptedTurnCanonicalMortalItemMaterializationAsync(lease));
            issues.AddRange(await validator.ValidateAcceptedTurnCanonicalResourceMaterializationAsync(lease));
            issues.AddRange(await validator.ValidateAcceptedTurnCanonicalEffectMaterializationAsync(lease));
            issues.AddRange(await validator.ValidateAcceptedTurnCanonicalWoundMaterializationAsync(lease));
            issues.AddRange(await WoundAcceptedTurnSnapshotContract.ValidatePublishedOutputAuthorityAsync(plan,
                path => _fs.ReadFileBytesAsync(lease, path)));
            if (issues.Any(row => row.Severity == IssueSeverity.Error))
            {
                var compensation = await transaction.CompensateAndRearmAsync(_fs, lease);
                return new(compensation.Disposition, null, issues.Concat(compensation.Issues).ToArray());
            }
            var proof = await transaction.ValidatePublishedReadbackAsync(_fs, lease);
            var settled = AcceptedTurnAuthorityRegistry.CompleteTakenJointRepairPublication(_fs, lease, transaction.Receipt, proof);
            if (settled.Disposition != MortalJointRepairPublicationDisposition.Committed)
            {
                var compensation = await transaction.CompensateAndRearmAsync(_fs, lease);
                return new(compensation.Disposition, null, settled.Issues.Concat(compensation.Issues).ToArray());
            }
            return new(settled.Disposition, plan, settled.Issues);
        }
        catch (CoordinatedStatePublicationUncertainException error)
        {
            // No outer baseline restore or claim release may reinterpret this as
            // ordinary rejected work. Durable recovery owns the uncertain cohort.
            return new(MortalJointRepairPublicationDisposition.PublicationUncertain, null, JointPublicationIssue(error));
        }
        catch (Exception error) when (error is InvalidDataException or IOException or InvalidOperationException or ArgumentException or System.Text.Json.JsonException or AggregateException)
        {
            var compensation = await transaction.CompensateAndRearmAsync(_fs, lease);
            return new(compensation.Disposition, null, JointPublicationIssue(error).Concat(compensation.Issues).ToArray());
        }
    }

    private static IReadOnlyList<ValidationIssue> JointPublicationIssue(Exception error) =>
        [MortalWoundOpportunityAdapter.PrivateSourceIssue(WoundAcceptedTurnPlannerCore.NewIssue(
            "mortal_joint_repair_publication_refused", error.Message, "one complete coordinated joint publication", "refused"))];
}
