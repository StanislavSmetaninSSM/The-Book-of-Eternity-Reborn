using System.Security.Cryptography;
using System.Text;
using System.Text.Json.Nodes;
using BookOfEternityClient.Configuration;
using BookOfEternityClient.Models;
using BookOfEternityClient.Services;

namespace BookOfEternityClient.Core;

public partial class GameEngine
{
    private static readonly object JointMechanicalAdmissionIssuer = new();

    // The private validated frame never leaves GameEngine. This proof is issued
    // from actual bytes, not from a caller GameResponse or bare frame coordinates.
    internal sealed class MortalJointRepairMechanicalAdmission
    {
        private readonly FileSystemManager _files;
        private readonly FileSystemManager.CanonicalWriteLease _lease;
        private readonly Dictionary<string, byte[]?> _before;
        private readonly WoundAcceptedTurnBinding _binding;
        private readonly string _generation;
        private readonly PendingTurnSnapshotReadAuthority _originalSnapshot;
        private readonly PendingTurnSnapshotReader.OriginalReadEvidence _originalEvidence;
        private static readonly OperationValidationRule MechanicalDataRule = new("Mechanical", 1);
        internal MortalJointRepairMechanicalAdmission(object issuer, FileSystemManager files,
            FileSystemManager.CanonicalWriteLease lease, string generation, WoundAcceptedTurnBinding binding,
            IReadOnlyDictionary<string, byte[]?> before, PendingTurnSnapshotReadAuthority originalSnapshot,
            PendingTurnSnapshotReader.OriginalReadEvidence originalEvidence)
        {
            if (!ReferenceEquals(issuer, JointMechanicalAdmissionIssuer))
                throw new InvalidOperationException("Mechanical admission requires its actual GameEngine owner.");
            _files = files; _lease = lease; _generation = generation;
            _originalSnapshot = originalSnapshot; _originalEvidence = originalEvidence;
            _binding = WoundAcceptedTurnData.CloneBinding(binding)!;
            _before = before.ToDictionary(row => row.Key, row => row.Value?.ToArray(), StringComparer.Ordinal);
            Fingerprint = ComputeFingerprint();
        }
        internal string Fingerprint { get; }
        internal IReadOnlyDictionary<string, CanonicalBeforeImage> BeforeImages => _before.ToDictionary(
            row => row.Key, row => new CanonicalBeforeImage(row.Value is not null, row.Value?.ToArray()), StringComparer.Ordinal);
        private string ComputeFingerprint()
        {
            var fields = new List<string?> { "book_of_eternity.mortal_joint_repair.mechanical_admission.v1",
                _generation, WoundAcceptedTurnFingerprints.ComputeBinding(_binding) };
            foreach (var (path, bytes) in _before.OrderBy(row => row.Key, StringComparer.Ordinal))
            { fields.Add(path); fields.Add(bytes is null ? null : Convert.ToHexString(SHA256.HashData(bytes))); }
            return WoundAcceptedTurnFingerprintWriter.Compute(fields);
        }
        /// <summary>
        /// Rechecks live ownership and the exact private proof before validating its reusable source DATA.
        /// </summary>
        /// <param name="files">
        /// The exact manager retained by the issued mechanical proof.
        /// </param>
        /// <param name="lease">
        /// Original active physical lease, never replaced by a cached fact.
        /// </param>
        /// <param name="binding">
        /// Current caller frame which must agree with the private captured binding.
        /// </param>
        /// <returns>
        /// Completion after live guards and current source DATA succeed.
        /// </returns>
        internal async Task ValidateCurrentAsync(FileSystemManager files, FileSystemManager.CanonicalWriteLease lease,
            WoundAcceptedTurnBinding binding)
        {
            using var innerTiming1 = files.MeasureOperation("JointMechanical.ValidateCurrentAsync");
            try
            {
                using (files.MeasureOperation("JointMechanical.ValidateOwnerAndFingerprint"))
                {
                    files.EnsureCanonicalWriteLeaseActive(lease);
                    if (!ReferenceEquals(files, _files) || !ReferenceEquals(lease, _lease) ||
                        !files.IsCurrentSessionGeneration(lease, _generation) || ComputeFingerprint() != Fingerprint ||
                        WoundAcceptedTurnFingerprints.ComputeBinding(binding) != WoundAcceptedTurnFingerprints.ComputeBinding(_binding))
                        throw new InvalidDataException("Joint mechanical admission belongs to a stale or foreign owner/frame.");
                    _originalEvidence.RequireOwner(files, lease, _originalSnapshot);
                }
                await files.ValidateRecoveryDataAsync(MechanicalDataRule, this, lease,
                    () => ValidateSourceDataAsync(files, lease, _binding));
            }
            catch { files.InvalidateRecoveryValidationAfterFailure(); throw; }
        }

        /// <summary>
        /// Checks the original source closure, snapshot frame and ordered before-images without issuing authority.
        /// </summary>
        /// <param name="files">
        /// Exact source manager already checked by the live wrapper.
        /// </param>
        /// <param name="lease">
        /// Original active lease used for all real source reads.
        /// </param>
        /// <param name="binding">
        /// Private captured frame, immutable for this proof identity.
        /// </param>
        /// <returns>
        /// Completion after the unchanged physical DATA validation body succeeds.
        /// </returns>
        private async Task ValidateSourceDataAsync(FileSystemManager files, FileSystemManager.CanonicalWriteLease lease,
            WoundAcceptedTurnBinding binding)
        {
            using var innerTiming3 = files.MeasureOperation("JointMechanical.RequireCurrentOriginalEvidence");
            _originalEvidence.RequireCurrent(files, lease, _originalSnapshot);
            innerTiming3?.Dispose();
            using var innerTiming4 = files.MeasureOperation("JointMechanical.ReadAndCheckSnapshot");
            var original = MortalQteWoundOpportunityProjection.ReadSnapshot(files, lease);
            if (original.SessionId != binding.SessionId || original.RequestId != binding.RequestId ||
                original.SnapshotToken != binding.SnapshotToken || original.TurnNumber != binding.Turn || original.Realm != binding.Realm)
                throw new InvalidDataException("The official original no longer matches mechanical admission.");
            innerTiming4?.Dispose();
            using var innerTiming5 = files.MeasureOperation("JointMechanical.CompareBeforeImages");
            foreach (var (path, expected) in _before)
            {
                var current = await files.ReadFileBytesAsync(lease, path);
                if (expected is null ? current is not null : current is null || !expected.AsSpan().SequenceEqual(current))
                    throw new InvalidDataException("Joint mechanical admission changed: " + path);
            }
            innerTiming5?.Dispose();
        }
        internal async Task<GameResponse> ReadProvedEmptyResponseAsync(FileSystemManager files,
            FileSystemManager.CanonicalWriteLease lease, WoundAcceptedTurnBinding binding)
        {
            await ValidateCurrentAsync(files, lease, binding);
            // The owner below independently proved every concurrent mechanical input
            // empty/unchanged. This is not a stripped display response or omission.
            return new GameResponse();
        }
    }

    // Called by the fresh canonical joint owner after real resource admission.
    // Its private actual frame never crosses into a caller-supplied envelope.
    private async Task<MortalJointRepairMechanicalAdmission> CreateMortalJointRepairMechanicalAdmissionAsync(
        FileSystemManager.CanonicalWriteLease lease, ValidatedPendingTurnSnapshotContext frame,
        AcceptedMechanicsWoundStageBundle bundle, object continuation)
    {
        _fs.EnsureCanonicalWriteLeaseActive(lease);
        if (!ReferenceEquals(bundle.PreparedPlan.JointRepairContinuationAuthority, continuation) ||
            !WoundAcceptedTurnPlanner.JointRepairFinalPlanAgrees(bundle))
            throw new InvalidDataException("Mechanical admission requires the actual complete private joint stages.");
        await WoundAcceptedTurnPlanner.ValidateJointRepairCurrentAsync(_fs, lease, continuation);
        var binding = bundle.Input.Binding;
        var paths = AcceptedMechanicsWoundCommonInputComposer.TreatmentOrdinaryGuardPaths.Concat(new[]
            { NpcTradeRequestState.PendingRequestPath, TrainingRequestState.PendingRequestPath, MortalQteDecisionPublication.StatePath })
            .Concat(MortalJointInactiveTailPaths)
            .Concat(WoundAcceptedTurnSnapshotContract.CarrierPaths)
            .Concat(bundle.EffectBatchPlan.EffectPlan.CarrierBeforeImages.Keys)
            .Concat(bundle.EffectBatchPlan.EffectPlan.TouchedPaths)
            .Concat(bundle.EffectBatchPlan.EffectPlan.DeletedPaths)
            .Distinct(StringComparer.Ordinal).ToArray();
        var read = PendingTurnSnapshotReader.ReadObservedCurrent(_fs, lease, new PendingTurnSnapshotPathSelection(
            new[] { "game_state/meta/soul_state.json" }, paths.Where(path => path != "game_state/meta/soul_state.json" &&
                path != MortalWoundOpportunityAdapter.DecisionPath)));
        if (!read.Success || read.Snapshot is not { } original || read.Evidence is not { } originalEvidence)
            throw new InvalidDataException("Joint mechanical original read failed: " + string.Join(";", read.Issues.Select(row => row.Code)));
        if (binding.Realm != "mortal_world" || original.SessionId != frame.SessionId || original.RequestId != frame.RequestId ||
            original.SnapshotToken != frame.Payload.ManifestPayloadHash || original.TurnNumber != frame.TurnNumber ||
            original.SessionId != binding.SessionId || original.RequestId != binding.RequestId ||
            original.SnapshotToken != binding.SnapshotToken || original.TurnNumber != binding.Turn || original.Realm != binding.Realm)
            throw new InvalidDataException("The actual validated frame and official original must agree with both joint owners.");
        var before = new Dictionary<string, byte[]?>(StringComparer.Ordinal);
        foreach (var path in paths)
        {
            var current = await _fs.ReadFileBytesAsync(lease, path);
            before.Add(path, current);
            // Exactly this already authenticated public draft is owned by the real
            // adapter/private preparation. It grants no exemption to other work.
            if (path == MortalWoundOpportunityAdapter.DecisionPath) continue;
            if (path == "game_state/meta/guardians.json") continue;
            if (path == ResourceMaterializationContract.CommandPath || path == EffectAcceptedTurnPlan.CommandPath)
            {
                if (current is not null) throw new InvalidDataException("Joint concurrent command is unsupported: " + path);
                continue;
            }
            if (!original.CoveredLogicalPaths.Contains(path, StringComparer.Ordinal))
            {
                if (current is not null) throw new InvalidDataException("Uncovered joint mechanical input is present: " + path);
                // Refusal-only absence never grants pristine canonical authority.
                continue;
            }
            if (current is null || !JsonNode.DeepEquals(ReadJointMechanicalNode(original.ReadRequiredBytes(path), path),
                    ReadJointMechanicalNode(current, path)))
                throw new InvalidDataException("Concurrent joint mechanical input changed: " + path);
        }
        foreach (var path in new[] { AcceptedMechanicsPlan.WoundCommandPath, WoundAcceptedTurnSnapshotContract.PendingResolutionPath,
            "output/narrative_response.json", LiveTurnPreparationService.TurnRequestPath,
            LiveTurnPreparationService.PendingTurnSnapshotManifestPath, PendingTurnSnapshotAuthority.AuthorityPath })
            before.TryAdd(path, await _fs.ReadFileBytesAsync(lease, path));
        // Complete current guard coverage separately from original-source admission:
        // current GM-owned outputs need not equal their signed original images.
        foreach (var path in WoundAcceptedTurnSnapshotContract.RequiredPaths)
        {
            var current = await _fs.ReadFileBytesAsync(lease, path);
            if (before.TryGetValue(path, out var captured))
            {
                if (captured is null ? current is not null : current is null || !captured.AsSpan().SequenceEqual(current))
                    throw new InvalidDataException("Joint required before-image changed during capture: " + path);
            }
            else before.Add(path, current);
        }
        // Unchanged bytes alone are not no-work evidence. Prove the approved
        // inactive completion tail BEFORE common fingerprints/cache/take.
        RequireInactiveMortalJointTail(before, binding.Turn);
        // Revalidate exact owned command/pending/draft/scene after the complete read.
        await WoundAcceptedTurnPlanner.ValidateJointRepairCurrentAsync(_fs, lease, continuation);
        originalEvidence.RequireCurrent(_fs, lease, original);
        _mortalJointPublicationAttempt?.RequireMechanicalOriginalAgreement(_fs, lease, original);
        var proof = new MortalJointRepairMechanicalAdmission(JointMechanicalAdmissionIssuer, _fs, lease,
            _fs.ReadLocalGenerationSnapshot(lease).Binding.Id, binding, before, original, originalEvidence);
        await proof.ValidateCurrentAsync(_fs, lease, binding);
        return proof;
    }

    private static JsonNode ReadJointMechanicalNode(byte[] bytes, string path) =>
        StrictJsonAuthority.Deserialize<JsonNode>(new UTF8Encoding(false, true).GetString(bytes).TrimStart('\uFEFF'),
            SharedJsonOptions.PrettyCamelCaseUnsafeRelaxed, path) ??
        throw new InvalidDataException("Present joint mechanical JSON null is invalid: " + path);
}
