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

    /// <summary>
    /// Checks actual taken inventory, receipt lineage and candidate admission within a fresh source boundary.
    /// </summary>
    /// <param name="receipt">
    /// The exact exclusive receipt retained by the owning joint transaction.
    /// </param>
    /// <returns>
    /// Completion after the original taken-input checks succeed; refusals propagate to the owning coordinator.
    /// </returns>
    private async Task ValidateTakenJointPublicationInputsAsync(MortalJointRepairPublicationTakeReceipt receipt)
    {
        using var freshSource = _fs.ForceFreshRecoveryValidation();
        using var innerTiming1 = _fs.MeasureOperation("JointPublish.ValidateTakenInputsAsync");
        var lease = _writeLease ?? throw new InvalidOperationException("Joint publication requires its issuing lease.");
        using var innerTiming2 = _fs.MeasureOperation("JointPublish.ValidateTakenInventoryAsync");
        await receipt.Preparation.ValidateInventoryCurrentAsync(_fs, lease);
        innerTiming2?.Dispose();
        using var innerTiming3 = _fs.MeasureOperation("JointPublish.ValidateReceiptLineage");
        var probe = AcceptedTurnAuthorityRegistry.ProbeTakenJointRepairPublication(_fs, lease, receipt);
        if (!probe.IsValid) throw new InvalidDataException(string.Join(";", probe.Issues.Select(row => row.Code + ": " + row.FilePath)));
        var plan = receipt.Plan;
        var bundle = receipt.Authority.Bundle;
        string? firstFailure = null;
        var details = string.Empty;
        if (plan.EffectPlan is null) firstFailure = "1:effect_plan_missing";
        else if (plan.WoundStageBundle is null) firstFailure = "2:wound_bundle_missing";
        else if (!receipt.HasValidSeal) firstFailure = "3:receipt_seal_invalid";
        else if (plan.WoundStageBundle.BundleFingerprint != bundle.BundleFingerprint)
            firstFailure = "4:wound_bundle_fingerprint_mismatch";
        else
        {
            var currentPayload = WoundAcceptedTurnFingerprints.ComputeAcceptedEffectPlanPayload(plan.EffectPlan);
            var initialPayload = WoundAcceptedTurnFingerprints.ComputeAcceptedEffectPlanPayload(bundle.EffectBatchPlan.EffectPlan);
            if (!plan.EffectPlan.IsAcceptedBoundaryComplete ||
                !string.Equals(plan.EffectPlan.AcceptedBoundaryBasePlanFingerprint, initialPayload, StringComparison.Ordinal) ||
                !string.Equals(plan.EffectPlan.AcceptedBoundaryFinalPlanFingerprint, currentPayload, StringComparison.Ordinal))
            {
                firstFailure = "5:effect_payload_mismatch";
                details = $"; currentPayload={BoundedFingerprint(currentPayload)}; initialPayload={BoundedFingerprint(initialPayload)}" +
                    $"; storedBase={BoundedFingerprint(plan.EffectPlan.AcceptedBoundaryBasePlanFingerprint)}" +
                    $"; storedFinal={BoundedFingerprint(plan.EffectPlan.AcceptedBoundaryFinalPlanFingerprint)}" +
                    $"; boundaryComplete={plan.EffectPlan.IsAcceptedBoundaryComplete}";
            }
            else if (!receipt.Authority.AgreesWith(plan.WoundStageBundle, receipt.Authority.Anchors, plan.OwnerAuthority))
                firstFailure = "6:joint_authority_agreement_invalid";
        }
        if (firstFailure is not null)
            throw new InvalidDataException("Joint publication lost its exclusive complete effect/wound/item lineage. First failed predicate: " +
                firstFailure + details);
        innerTiming3?.Dispose();

        static string BoundedFingerprint(string? value) => value is null ? "<null>" :
            value.Length <= 80 ? value : value[..80] + "<truncated>";
        // Preflight validated the complete effect/source/target stages and final
        // aggregate bounds. This taken capability additionally proves exact
        // unchanged original/current bytes and the actual vacant item cache slot.
        // It never uses the old singleton normalized-source fallback.
        using var innerTiming4 = _fs.MeasureOperation("JointPublish.CandidateAdmissionGate");
        var finalIssues = MortalWoundTreatmentCapabilityAuthority.CandidateAdmissionGate.ValidateJoint(receipt.Authority, plan);
        if (finalIssues.Count != 0) throw new InvalidDataException(string.Join(";", finalIssues.Select(row => row.Code + ": " + row.FilePath)));
        innerTiming4?.Dispose();
    }

    internal sealed record JointRepairPublicationResult(MortalJointRepairPublicationDisposition Disposition,
        AcceptedMechanicsPlan? Plan, IReadOnlyList<ValidationIssue> Issues);

    // The actual GameEngine canonical entry retains this SAME participating
    // issuing lease through the full pipeline, with no independent writes,
    // accumulated-state normalization, second reduction or second scheduler.
    internal async Task<JointRepairPublicationResult> NormalizeAndValidateJointRepairAsync(
        JointRepairPublicationPreflight preflight, ValidationService validator,
        IReadOnlyDictionary<string, string> backups)
    {
        using var innerTiming5 = _fs.MeasureOperation("JointPublish.NormalizeAndValidateAsync");
        var lease = _writeLease ?? throw new InvalidOperationException("Joint coordinator requires its issuing lease.");
        AcceptedTurnAuthorityRegistry.JointRepairPublicationTakeResult taken;
        using var innerTiming6 = _fs.MeasureOperation("JointPublish.TakeAsync");
        try { taken = await TakeJointRepairPublicationAsync(preflight); }
        catch (Exception error) when (error is InvalidDataException or IOException or InvalidOperationException or ArgumentException or System.Text.Json.JsonException)
        { return new(MortalJointRepairPublicationDisposition.PublicationUncertain, null, JointPublicationIssue(error)); }
        innerTiming6?.Dispose();
        if (!taken.Success || taken.Receipt is null)
            return new(MortalJointRepairPublicationDisposition.PublicationUncertain, null, taken.Issues);
        MortalJointRepairPublicationTransaction transaction;
        using var innerTiming7 = _fs.MeasureOperation("JointPublish.CreateTransaction");
        try { transaction = MortalJointRepairPublicationTransaction.Create(_fs, lease, taken.Receipt); }
        catch (Exception error) when (error is InvalidDataException or IOException or InvalidOperationException or ArgumentException)
        { return new(MortalJointRepairPublicationDisposition.PublicationUncertain, null, JointPublicationIssue(error)); }
        innerTiming7?.Dispose();
        try
        {
            using var innerTiming8 = _fs.MeasureOperation("JointPublish.PublishAcceptedMechanicsAsync");
            var plan = await PublishAcceptedMechanicsAsync(backups, null, preflight.Validated,
                normalizedAcceptedCarrierBaselines: false, jointTransaction: transaction) ??
                throw new InvalidDataException("Joint common publication returned no plan.");
            innerTiming8?.Dispose();
            var issues = new List<ValidationIssue>();
            using var innerTiming9 = _fs.MeasureOperation("JointPublish.ValidateCanonicalLocationAsync");
            issues.AddRange(await validator.ValidateAcceptedTurnCanonicalMortalLocationMaterializationAsync(lease));
            innerTiming9?.Dispose();
            using var innerTiming10 = _fs.MeasureOperation("JointPublish.ValidateCanonicalItemAsync");
            issues.AddRange(await validator.ValidateAcceptedTurnCanonicalMortalItemMaterializationAsync(lease));
            innerTiming10?.Dispose();
            using var innerTiming11 = _fs.MeasureOperation("JointPublish.ValidateCanonicalResourceAsync");
            issues.AddRange(await validator.ValidateAcceptedTurnCanonicalResourceMaterializationAsync(lease));
            innerTiming11?.Dispose();
            using var innerTiming12 = _fs.MeasureOperation("JointPublish.ValidateCanonicalEffectAsync");
            issues.AddRange(await validator.ValidateAcceptedTurnCanonicalEffectMaterializationAsync(lease));
            innerTiming12?.Dispose();
            using var innerTiming13 = _fs.MeasureOperation("JointPublish.ValidateCanonicalWoundAsync");
            issues.AddRange(await validator.ValidateAcceptedTurnCanonicalWoundMaterializationAsync(lease));
            innerTiming13?.Dispose();
            using var innerTiming14 = _fs.MeasureOperation("JointPublish.ValidateOutputAuthorityAsync");
            issues.AddRange(await WoundAcceptedTurnSnapshotContract.ValidatePublishedOutputAuthorityAsync(plan,
                path => _fs.ReadFileBytesAsync(lease, path)));
            innerTiming14?.Dispose();
            if (issues.Any(row => row.Severity == IssueSeverity.Error))
            {
                var compensation = await transaction.CompensateAndRearmAsync(_fs, lease);
                return new(compensation.Disposition, null, issues.Concat(compensation.Issues).ToArray());
            }
            using var innerTiming15 = _fs.MeasureOperation("JointPublish.ValidateTransactionReadbackAsync");
            var proof = await transaction.ValidatePublishedReadbackAsync(_fs, lease);
            innerTiming15?.Dispose();
            using var innerTiming16 = _fs.MeasureOperation("JointPublish.CompleteRegistry");
            var settled = AcceptedTurnAuthorityRegistry.CompleteTakenJointRepairPublication(_fs, lease, transaction.Receipt, proof);
            innerTiming16?.Dispose();
            if (settled.Disposition != MortalJointRepairPublicationDisposition.Committed)
            {
                // Completion refusal may mean stale ownership or a claim that
                // already advanced. It grants no authority to undo known batch
                // evidence, even when the current bytes match intended images.
                return new(MortalJointRepairPublicationDisposition.PublicationUncertain, null, settled.Issues);
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
