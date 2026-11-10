using System.Text.Json.Nodes;
using BookOfEternityClient.Core;

namespace BookOfEternityClient.Services;

internal static partial class WoundAcceptedTurnPlanner
{
    private static readonly object JointCommonBaselineIssuer = new();

    private sealed class JointCommonBaselineAuthority
    {
        private readonly MortalItemAcceptedTurnNormalizationSnapshot _items;
        private readonly Func<OperationStructuralSnapshot?> _qteDataCapture;
        internal JointCommonBaselineAuthority(object issuer, object continuation, object reservation,
            GameEngine.MortalJointRepairMechanicalAdmission mechanical, AcceptedMechanicsWoundStageBundle bundle,
            MortalWoundCanonicalAnchorPlan anchors, TreatmentSkillProjectionAuthority skills,
            MortalTreatmentItemCommandEnvelope itemEnvelope, MortalItemAcceptedTurnNormalizationSnapshot items)
        {
            if (!ReferenceEquals(issuer, JointCommonBaselineIssuer))
                throw new InvalidOperationException("Joint common baseline requires its own private issuer.");
            Continuation = continuation; Reservation = reservation; Mechanical = mechanical;
            Bundle = bundle.DetachedCopy(); Anchors = anchors.DetachedCopy(); Skills = skills;
            ItemEnvelope = itemEnvelope.Clone(); _items = items.Clone();
            Fingerprint = ComputeFingerprint();
            _qteDataCapture = CaptureQteCalculationInput;
        }
        internal object Continuation { get; }
        internal object Reservation { get; }
        internal GameEngine.MortalJointRepairMechanicalAdmission Mechanical { get; }
        internal AcceptedMechanicsWoundStageBundle Bundle { get; }
        internal MortalWoundCanonicalAnchorPlan Anchors { get; }
        internal TreatmentSkillProjectionAuthority Skills { get; }
        internal MortalTreatmentItemCommandEnvelope ItemEnvelope { get; }
        internal MortalItemAcceptedTurnNormalizationSnapshot Items => _items.Clone();
        internal string Fingerprint { get; }

        /// <summary>
        /// Gets the stable policy for this baseline's complete current QTE calculation DATA.
        /// </summary>
        internal Func<OperationStructuralSnapshot?> QteDataCapture => _qteDataCapture;

        /// <summary>
        /// Captures actual mutable calculation inputs and immutable private companion identities without issuing authority.
        /// </summary>
        /// <returns>
        /// Complete ordered comparison DATA, or <see langword="null"/> when the original calculation must handle an unsupported input.
        /// </returns>
        private OperationStructuralSnapshot? CaptureQteCalculationInput()
        {
            if (Continuation is not JointRepairContinuationAuthority joint ||
                !Mechanical.BeforeImages.TryGetValue(MortalWoundOpportunityAdapter.DecisionPath, out var draft)) return null;
            var binding = joint.AcquisitionInput.Binding;
            return new(new string?[]
            {
                Fingerprint, joint.Fingerprint,
                System.Text.Json.JsonSerializer.Serialize(binding),
                joint.Checkpoint.ToJsonObject().ToJsonString(), joint.AcquisitionCommandRoot.ToJsonString(),
                joint.Accepted.AcceptedStateFingerprint, joint.Request.RequestFingerprint,
                joint.Resolution.ResolutionAuthorityFingerprint,
                Bundle.FinalPlan.HistoryAfterImage.ToJsonString(),
                draft.Existed.ToString(), draft.Bytes is { } bytes ? Convert.ToBase64String(bytes) : null, draft.Fingerprint
            }, new object?[]
            {
                this, joint, Mechanical, Bundle, Anchors, Skills, ItemEnvelope, Reservation,
                joint.Accepted, joint.Request, joint.Resolution
            });
        }
        /// <summary>
        /// Computes the retained common-baseline seal from its original companion fingerprints.
        /// </summary>
        /// <returns>
        /// The deterministic digest; it does not establish current file or lease ownership.
        /// </returns>
        private string ComputeFingerprint()
        {
            using var operationTiming = FileSystemManager.MeasureCurrentOperation("JointBaseline.ComputeFingerprint");
            return WoundAcceptedTurnFingerprintWriter.Compute(new string?[]
            {
                "book_of_eternity.mortal_joint_repair.common_baseline.v1",
                GetJointRepairContinuationFingerprint(Continuation), Mechanical.Fingerprint,
                Bundle.BundleFingerprint, Anchors.Fingerprint, Skills.Fingerprint, ItemEnvelope.Fingerprint,
                _items.ProofFingerprint, _items.ProjectionProofFingerprint
            });
        }
        /// <summary>
        /// Gets whether the retained joint stages, anchors, empty envelope, skill semantics and full item-baseline seal agree.
        /// Returns <see langword="true"/> only when every original short-circuit check agrees; otherwise, <see langword="false"/>.
        /// </summary>
        internal bool HasValidSeal
        {
            get
            {
                using var operationTiming = FileSystemManager.MeasureCurrentOperation("JointBaseline.HasValidSeal");
                return ReferenceEquals(Bundle.PreparedPlan.JointRepairContinuationAuthority, Continuation) &&
                    JointRepairFinalPlanAgrees(Bundle) && Anchors.AgreesWith(Bundle) && ItemEnvelope.IsEmpty &&
                    TryReadTreatmentSkillProjectionForEnvelopeSemantics(Skills, Reservation, out _) &&
                    _items.HasFinalPublicationBaseline && _items.RecomputesFinalPublicationBaseline() && ComputeFingerprint() == Fingerprint;
            }
        }
    }

    internal sealed record JointCommonBaselineResult(object? Authority, IReadOnlyList<ValidationIssue> Issues)
    {
        internal bool IsValid => Authority is not null && Issues.Count == 0;
    }

    // Common/resource authority is not issued here. This establishes mandatory
    // owner-proven mechanical emptiness plus an ACTUAL ordinary item slot baseline.
    internal static async Task<JointCommonBaselineResult> CreateJointRepairCommonBaselineAsync(
        FileSystemManager files, FileSystemManager.CanonicalWriteLease lease,
        GameEngine.MortalJointRepairMechanicalAdmission mechanical, AcceptedMechanicsWoundStageBundle bundle,
        MortalWoundCanonicalAnchorPlan anchors, object continuation, object reservation)
    {
        try
        {
            if (!ReferenceEquals(bundle.PreparedPlan.JointRepairContinuationAuthority, continuation) ||
                !JointRepairFinalPlanAgrees(bundle) || !anchors.AgreesWith(bundle))
                throw new InvalidDataException("The complete joint stages and joint-created anchors must retain their private owner.");
            await ValidateJointRepairCurrentAsync(files, lease, continuation);
            var response = await mechanical.ReadProvedEmptyResponseAsync(files, lease, bundle.Input.Binding);
            var frozenSkills = FreezeTreatmentSkillCommands(response);
            var frozenItems = FreezeTreatmentItemCommands(response);
            if (frozenSkills.Envelope is not { IsEmpty: true } skills || frozenItems.Envelope is not { IsEmpty: true } items ||
                frozenSkills.Issues.Count != 0 || frozenItems.Issues.Count != 0)
                throw new InvalidDataException("The actual mechanical proof must issue only its genuinely empty supported command envelope.");
            var semantic = ComputeTreatmentSkillSemanticFingerprint(skills);
            var projected = CreateTreatmentSkillProjection(files, lease, skills, semantic, reservation, suppliedNpcSemanticBaseline: null);
            if (projected.Authority is not TreatmentSkillProjectionAuthority projection || projected.Issues.Count != 0)
                return new(null, projected.Issues);
            var roots = new Dictionary<string, JsonNode?>(StringComparer.Ordinal);
            foreach (var path in MortalItemCanonicalProjectionPlanner.ProjectionRootPaths)
            {
                var text = await files.ReadFileAsync(lease, path);
                roots.Add(path, text is null ? null : StrictJsonAuthority.Deserialize<JsonNode>(text,
                    BookOfEternityClient.Configuration.SharedJsonOptions.PrettyCamelCaseUnsafeRelaxed, path));
            }
            var npc = NpcCoreChangesContract.CreateAuthorityFromCanonicalJson(
                await files.ReadFileAsync(lease, MortalLocationMaterializationContract.WorldMapPath),
                await files.ReadFileAsync(lease, MortalLocationMaterializationContract.CurrentLocationPath),
                await files.ReadFileAsync(lease, "game_state/factions/faction_core.json"),
                await files.ReadFileAsync(lease, "game_state/misc/characteristics.json"));
            var tradeBytes = await files.ReadFileBytesAsync(lease, NpcTradeRequestState.PendingRequestPath);
            var trainingBytes = await files.ReadFileBytesAsync(lease, TrainingRequestState.PendingRequestPath);
            await mechanical.ValidateCurrentAsync(files, lease, bundle.Input.Binding);
            var baseline = await AcceptedTurnAuthorityRegistry.SealMortalJointRepairItemBaselineAsync(files, lease,
                continuation, reservation, mechanical, bundle, roots, items, npc,
                new CanonicalBeforeImage(tradeBytes is not null, tradeBytes),
                new CanonicalBeforeImage(trainingBytes is not null, trainingBytes));
            if (baseline.Snapshot is null || baseline.Issues.Count != 0) return new(null, baseline.Issues);
            var authority = new JointCommonBaselineAuthority(JointCommonBaselineIssuer, continuation, reservation,
                mechanical, bundle, anchors, projection, items, baseline.Snapshot);
            await ValidateJointRepairCommonBaselineAsync(files, lease, authority);
            return new(authority, Array.Empty<ValidationIssue>());
        }
        catch (Exception error) when (error is InvalidDataException or InvalidOperationException or ArgumentException or
            System.Text.Json.JsonException or IOException)
        { return new(null, JointPreparationFailure("common_baseline_invalid", error.Message).Issues); }
    }

    /// <summary>
    /// Revalidates the private baseline seal, current continuation, mechanical source and confirmed item slot.
    /// </summary>
    /// <param name="files">
    /// The physical file manager used by every current check and passive timing.
    /// </param>
    /// <param name="lease">
    /// The caller-owned active canonical write lease.
    /// </param>
    /// <param name="value">
    /// The actual private common baseline; foreign values are refused before later validation.
    /// </param>
    /// <returns>
    /// Completion after the existing checks succeed; failures propagate unchanged.
    /// </returns>
    internal static async Task ValidateJointRepairCommonBaselineAsync(FileSystemManager files,
        FileSystemManager.CanonicalWriteLease lease, object value)
    {
        try
        {
            using var innerTiming1 = files.MeasureOperation("JointBaseline.ValidateCurrentAsync");
            using var innerTiming2 = files.MeasureOperation("JointBaseline.ValidateSeal");
            JointCommonBaselineAuthority authority;
            using (files.EnterOperationTimingContext())
            {
                if (value is not JointCommonBaselineAuthority actual || !actual.HasValidSeal)
                    throw new InvalidDataException("Invalid private joint common baseline.");
                authority = actual;
            }
            innerTiming2?.Dispose();
            await ValidateJointRepairCurrentAsync(files, lease, authority.Continuation);
            using var innerTiming3 = files.MeasureOperation("JointBaseline.ValidateMechanicalAsync");
            await authority.Mechanical.ValidateCurrentAsync(files, lease, authority.Bundle.Input.Binding);
            innerTiming3?.Dispose();
            using var innerTiming4 = files.MeasureOperation("JointBaseline.ConfirmItemBaseline");
            if (!AcceptedTurnAuthorityRegistry.ConfirmsMortalJointRepairItemBaseline(files, lease,
                    authority.Continuation, authority.Reservation, authority.Items))
                throw new InvalidDataException("The actual current item slot no longer owns the complete sealed joint baseline.");
            innerTiming4?.Dispose();
        }
        catch
        {
            files.InvalidateRecoveryValidationAfterFailure();
            throw;
        }
    }
}
