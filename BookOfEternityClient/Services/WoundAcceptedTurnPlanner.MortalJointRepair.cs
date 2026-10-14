using System.Security.Cryptography;
using System.Text;
using System.Text.Json.Nodes;
using BookOfEternityClient.Configuration;
using BookOfEternityClient.Core;

namespace BookOfEternityClient.Services;

internal static partial class WoundAcceptedTurnPlanner
{
    private const string JointRepairDomain = "book_of_eternity.wound.joint_repair.preparation.v1";
    private static readonly object JointRepairIssuer = new();

    private sealed class JointRepairContinuationAuthority
    {
        private readonly FileSystemManager _files;
        private readonly Dictionary<string, byte[]?> _before;
        private readonly WoundAcceptedTurnInput _acquisitionInput;
        private readonly WoundAcceptedTurnInput _treatmentInput;
        private readonly MortalWoundTreatmentOutcomePreparation _outcome;
        private readonly WoundEffectOperationBatch? _treatmentBatch;
        internal JointRepairContinuationAuthority(object issuer, FileSystemManager files,
            string generation, MortalJointRepairCheckpoint checkpoint,
            WoundAcceptedTurnInput acquisitionInput, WoundAcceptedTurnInput treatmentInput,
            MortalWoundTreatmentAcceptedStateAuthority accepted,
            MortalWoundTreatmentAttemptRequest request, MortalWoundTreatmentResolution resolution,
            MortalWoundTreatmentResourceFinalization finalization,
            MortalWoundTreatmentOutcomePreparation outcome,
            MortalWoundTreatmentSeverityRematerializationResult rematerialization,
            IReadOnlyDictionary<string, byte[]?> before)
        {
            if (!ReferenceEquals(issuer, JointRepairIssuer)) throw new InvalidOperationException("Joint preparation requires its private issuer.");
            _files = files; Generation = generation; Checkpoint = checkpoint;
            _acquisitionInput = WoundAcceptedTurnData.CloneInput(acquisitionInput)!;
            _treatmentInput = WoundAcceptedTurnData.CloneInput(treatmentInput)!;
            Accepted = accepted; Request = request; Resolution = resolution; Finalization = finalization;
            _outcome = outcome.DetachedCopy(); _treatmentBatch = rematerialization.Batch;
            Rematerialization = rematerialization.Authority;
            _before = before.ToDictionary(pair => pair.Key, pair => pair.Value?.ToArray(), StringComparer.Ordinal);
            Fingerprint = ComputeFingerprint();
        }
        internal string Generation { get; }
        internal MortalJointRepairCheckpoint Checkpoint { get; }
        internal MortalWoundTreatmentAcceptedStateAuthority Accepted { get; }
        internal MortalWoundTreatmentAttemptRequest Request { get; }
        internal MortalWoundTreatmentResolution Resolution { get; }
        internal MortalWoundTreatmentResourceFinalization Finalization { get; }
        internal MortalWoundTreatmentRematerializationAuthority? Rematerialization { get; }
        internal WoundAcceptedTurnInput AcquisitionInput => WoundAcceptedTurnData.CloneInput(_acquisitionInput)!;
        internal WoundAcceptedTurnInput TreatmentInput => WoundAcceptedTurnData.CloneInput(_treatmentInput)!;
        internal MortalWoundTreatmentOutcomePreparation Outcome => _outcome.DetachedCopy();
        internal WoundEffectOperationBatch? TreatmentBatch => _treatmentBatch is null ? null : WoundAcceptedTurnData.CloneOperationBatch(_treatmentBatch);
        internal string Fingerprint { get; }
        internal IReadOnlyDictionary<string, JsonNode?> ReadEffectSourceRoots() =>
            EffectAcceptedTurnInputComposer.SourceAuthorityPaths.ToDictionary(path => path, path =>
                _before[path] is not { } bytes ? null : (JsonNode?)StrictJsonAuthority.Deserialize<JsonObject>(
                    new UTF8Encoding(false, true).GetString(bytes).TrimStart('\uFEFF'),
                    SharedJsonOptions.PrettyCamelCaseUnsafeRelaxed, path), StringComparer.Ordinal);
        internal bool HasValidSeal => ReferenceEquals(Resolution.RequestAuthority, Request) &&
            _outcome.AgreesWith(Resolution) && Finalization.RequestFingerprint == Request.RequestFingerprint &&
            Finalization.ResultFingerprint == Resolution.ResultFingerprint &&
            Finalization.ResourceAuthorityFingerprint == Request.ResourceAuthority.AuthorityFingerprint &&
            MortalWoundTreatmentResourceComposer.Finalize(Resolution).Finalization?.FinalizationFingerprint == Finalization.FinalizationFingerprint &&
            ComputeFingerprint() == Fingerprint;
        private string ComputeFingerprint()
        {
            var fields = new List<string?>
            {
                JointRepairDomain, Generation, Checkpoint.ToJsonObject()["fingerprint"]!.GetValue<string>(),
                WoundAcceptedTurnFingerprints.ComputeInput(_acquisitionInput),
                WoundAcceptedTurnFingerprints.ComputeInput(_treatmentInput), Accepted.AcceptedStateFingerprint,
                Request.RequestFingerprint, Resolution.ResultFingerprint, Resolution.ResolutionAuthorityFingerprint,
                Finalization.FinalizationFingerprint, _outcome.Fingerprint, Rematerialization?.AuthoritySeal
            };
            foreach (var (path, bytes) in _before.OrderBy(pair => pair.Key, StringComparer.Ordinal))
            { fields.Add(path); fields.Add(bytes is null ? null : Convert.ToHexString(SHA256.HashData(bytes))); }
            return WoundAcceptedTurnFingerprintWriter.Compute(fields);
        }
        internal async Task ValidateCurrentAsync(FileSystemManager files, FileSystemManager.CanonicalWriteLease lease)
        {
            files.EnsureCanonicalWriteLeaseActive(lease);
            if (!ReferenceEquals(files, _files) || !files.IsCurrentSessionGeneration(lease, Generation) || !HasValidSeal)
                throw new InvalidDataException("Joint preparation belongs to a stale or foreign physical owner.");
            var original = MortalQteWoundOpportunityProjection.ReadSnapshot(files, lease);
            var binding = _acquisitionInput.Binding;
            if (original.SessionId != binding.SessionId || original.RequestId != binding.RequestId ||
                original.SnapshotToken != binding.SnapshotToken || original.TurnNumber != binding.Turn || original.Realm != binding.Realm)
                throw new InvalidDataException("The actual joint original no longer matches its independently bound sources.");
            foreach (var (path, expected) in _before)
            {
                var actual = await files.ReadFileBytesAsync(lease, path);
                if (expected is null ? actual is not null : actual is null || !expected.AsSpan().SequenceEqual(actual))
                    throw new InvalidDataException("Joint preparation before-image changed: " + path);
            }
            var baselines = ReadPublicationBaselines(files, lease);
            if (baselines.Issues.Count != 0 || WoundAcceptedTurnFingerprints.ComputeInput(new WoundAcceptedTurnInput(
                    _acquisitionInput.Binding, _acquisitionInput.Opportunities, _acquisitionInput.Transitions,
                    baselines.WoundCarriers!, baselines.WoundIdentity!, baselines.WoundHistory!,
                    baselines.EffectCarriers, baselines.EffectIdentity)) != WoundAcceptedTurnFingerprints.ComputeInput(_acquisitionInput))
                throw new InvalidDataException("The joint wound/effect baseline changed while its guards were captured.");
            var confirmed = AcceptedTurnAuthorityRegistry.InspectConfirmedMortalWoundTreatmentResourceClaim(files, lease, Request, Finalization);
            if (!confirmed.IsValid || confirmed.State != MortalWoundTreatmentResourceReservationState.ConfirmedHeld)
                throw new InvalidDataException("The same joint request must still own its confirmed resource agreement.");
        }
    }

    internal sealed record JointRepairPreparationAuthorityResult(
        WoundAcceptedTurnInput? Input, object? Authority, IReadOnlyList<ValidationIssue> Issues)
    {
        internal bool IsValid => Input is not null && Authority is not null && Issues.Count == 0;
    }

    // This factory re-runs the actual adapter correction. Caller-provided parsed
    // checkpoints, public packets and detached result/binding views cannot issue it.
    internal static async Task<JointRepairPreparationAuthorityResult> CreateJointRepairPreparationAsync(
        FileSystemManager files, FileSystemManager.CanonicalWriteLease lease,
        MortalJointRepairCheckpoint.RejectedCapture capture)
    {
        try
        {
            capture.EnsureCurrent(files, lease);
            if (await files.ReadFileBytesAsync(lease, ResourceMaterializationContract.CommandPath) is not null ||
                await files.ReadFileBytesAsync(lease, EffectAcceptedTurnPlan.CommandPath) is not null)
                return JointPreparationFailure("concurrent_commands", "The first joint contour cannot admit foreign resource/effect commands.");
            var previous = capture;
            var frame = previous.Accepted.Binding;
            var reconstructed = await MortalJointRepairCheckpoint.ReconstructAsync(files, lease,
                frame.SessionId, frame.RequestId, frame.SnapshotToken, frame.Turn);
            if (!reconstructed.IsValid || reconstructed.Capture is not { } current ||
                current.Accepted.AcceptedStateFingerprint != previous.Accepted.AcceptedStateFingerprint ||
                current.Request.RequestFingerprint != previous.Request.RequestFingerprint ||
                current.Resolution.ResolutionAuthorityFingerprint != previous.Resolution.ResolutionAuthorityFingerprint ||
                !JsonNode.DeepEquals(current.Checkpoint.ToJsonObject(), previous.Checkpoint.ToJsonObject()))
                throw new InvalidDataException("Fresh installed-wave treatment reconstruction no longer agrees with the rejected capture.");
            // Use the freshly exported owner/request/claim, never bless new current
            // bytes with a previously captured capability or parsed checkpoint seal.
            capture = current;
            var correction = await MortalJointRepairCheckpoint.RevalidateCorrectionAsync(files, lease, capture);
            if (correction.SourceBinding is not { } acquisitionBinding || correction.MaterializedOpportunities.Count != 1 ||
                correction.Transitions.Count != 1 || correction.Transitions[0].Kind != "create" ||
                correction.MaterializedOpportunities[0].WorseningTarget is not null)
                return JointPreparationFailure("acquisition", "The real corrected adapter must provide one independent CREATE binding.");
            var treatmentBinding = capture.Accepted.Binding;
            if (!SameJointFrame(acquisitionBinding, treatmentBinding))
                return JointPreparationFailure("binding", "Each independently validated owner must bind the same actual original frame.");
            var baselines = ReadPublicationBaselines(files, lease);
            if (baselines.Issues.Count != 0) return new(null, null, baselines.Issues);
            var input = new WoundAcceptedTurnInput(acquisitionBinding, correction.MaterializedOpportunities,
                correction.Transitions, baselines.WoundCarriers!, baselines.WoundIdentity!, baselines.WoundHistory!,
                baselines.EffectCarriers, baselines.EffectIdentity);
            var treatmentInput = new WoundAcceptedTurnInput(treatmentBinding, Array.Empty<WoundOpportunityAuthority>(),
                Array.Empty<WoundAcceptedTransitionDraft>(), baselines.WoundCarriers!, baselines.WoundIdentity!,
                baselines.WoundHistory!, baselines.EffectCarriers, baselines.EffectIdentity);
            var validation = WoundAcceptedTurnPlannerCore.ValidateInput(input);
            if (validation.Issues.Count != 0) return new(null, null, validation.Issues);
            var preparation = MortalWoundTreatmentOutcomePublicationPlanner.Prepare(capture.Accepted,
                capture.Request, capture.Resolution, capture.Accepted.CurrentGameMinute);
            if (!preparation.IsValid) return new(null, null, preparation.Issues);
            var shell = ValidateTreatmentPublicationShell(capture.Accepted, capture.Request, capture.Resolution);
            if (shell.Finalization is null || shell.Issues.Count != 0) return new(null, null, shell.Issues);
            var rematerialization = MortalWoundTreatmentSeverityRematerializationPlanner.Prepare(treatmentInput,
                preparation.Preparation!, capture.Request.RequestFingerprint, capture.Resolution.ResolutionAuthorityFingerprint,
                capture.Resolution.ResultFingerprint, capture.Request.Coordinates.AttemptId,
                capture.Request.Coordinates.OperationKey, WoundIdentityState.ComputeSemanticFingerprint(capture.Request.RouteSourceWound));
            if (!rematerialization.IsValid) return new(null, null, rematerialization.Issues);
            var paths = AcceptedMechanicsWoundCommonInputComposer.TreatmentOrdinaryGuardPaths.Concat(new[]
            {
                AcceptedMechanicsPlan.WoundCommandPath, WoundAcceptedTurnSnapshotContract.PendingResolutionPath,
                MortalWoundOpportunityAdapter.DecisionPath, "output/narrative_response.json",
                LiveTurnPreparationService.PendingTurnSnapshotManifestPath, PendingTurnSnapshotAuthority.AuthorityPath,
                WoundCarrierCatalog.PlayerPath, WoundCarrierCatalog.NpcPath, WoundCarrierCatalog.EnemiesPath,
                WoundCarrierCatalog.AlliesPath, WoundCarrierCatalog.AfterlifeProfilesPath,
                WoundIdentityState.StatePath, WoundHistoryState.HistoryPath, EffectIdentityState.StatePath,
                EffectCarrierCatalog.PlayerPath, EffectCarrierCatalog.NpcPath, EffectCarrierCatalog.SpiritualConflictPath,
                MortalWoundOccurrenceState.StatePath, MortalWoundOpportunityReceiptState.StatePath
            }).Distinct(StringComparer.Ordinal).ToArray();
            var before = new Dictionary<string, byte[]?>(StringComparer.Ordinal);
            foreach (var path in paths) before.Add(path, await files.ReadFileBytesAsync(lease, path));
            var authority = new JointRepairContinuationAuthority(JointRepairIssuer, files, capture.Accepted.SessionGeneration,
                capture.Checkpoint, input, treatmentInput, capture.Accepted, capture.Request, capture.Resolution,
                shell.Finalization, preparation.Preparation!, rematerialization, before);
            await authority.ValidateCurrentAsync(files, lease);
            return new(input, authority, Array.Empty<ValidationIssue>());
        }
        catch (Exception error) when (error is InvalidDataException or InvalidOperationException or ArgumentException or IOException)
        { return JointPreparationFailure("input_changed", error.Message); }
    }

    private static bool SameJointFrame(WoundAcceptedTurnBinding left, WoundAcceptedTurnBinding right) =>
        left.SessionId == right.SessionId && left.RequestId == right.RequestId && left.SnapshotToken == right.SnapshotToken &&
        left.Turn == right.Turn && left.Realm == right.Realm && left.Realm == "mortal_world";
    private static JointRepairPreparationAuthorityResult JointPreparationFailure(string suffix, string message) =>
        new(null, null, [MortalWoundOpportunityAdapter.PrivateSourceIssue(
            WoundAcceptedTurnPlannerCore.NewIssue("wound_plan_joint_" + suffix, message,
                "one current privately issued joint preparation", "refused"))]);
    private static bool TryReadJointRepair(object? value, out JointRepairContinuationAuthority authority)
    {
        authority = value as JointRepairContinuationAuthority ?? null!;
        return authority is not null && authority.HasValidSeal;
    }
    internal static string GetJointRepairContinuationFingerprint(object value) => TryReadJointRepair(value, out var authority)
        ? authority.Fingerprint : throw new InvalidDataException("Joint preparation is not private-minted and sealed.");
    internal static async Task ValidateJointRepairCurrentAsync(FileSystemManager files,
        FileSystemManager.CanonicalWriteLease lease, object value)
    {
        if (!TryReadJointRepair(value, out var authority)) throw new InvalidDataException("Invalid private joint preparation.");
        await authority.ValidateCurrentAsync(files, lease);
    }
    internal static bool JointRepairAcceptedStateAgrees(object value, MortalWoundTreatmentAcceptedStateAuthority accepted) =>
        TryReadJointRepair(value, out var authority) && ReferenceEquals(authority.Accepted, accepted);
    internal static MortalWoundTreatmentAcceptedStateAuthority ReadJointRepairAcceptedState(object value) =>
        TryReadJointRepair(value, out var authority) ? authority.Accepted : throw new InvalidDataException("Invalid joint state.");

    // Detached views are non-authorizing. All readers first authenticate the whole
    // private prepared graph; neither a common frame nor a caller batch issues one.
    internal sealed record JointRepairFinalView(
        MortalWoundTreatmentOutcomePreparation Outcome, MortalWoundTreatmentResolution Resolution,
        MortalWoundTreatmentPersistedResult PersistedResult, WoundEffectOperationBatch? TreatmentBatch);

    internal static bool TryReadJointRepairFinal(WoundPreparedAcceptedTurnPlan prepared,
        out JointRepairFinalView view)
    {
        view = null!;
        if (!JointRepairPreparedAgrees(prepared) ||
            !TryReadJointRepair(prepared.JointRepairContinuationAuthority, out var joint)) return false;
        view = new(joint.Outcome, joint.Resolution,
            MortalWoundTreatmentPersistedResult.Create(joint.Resolution), joint.TreatmentBatch);
        return true;
    }

    private static JointRepairContinuationAuthority ReadJointRepairBatchOwner(
        WoundPreparedAcceptedTurnPlan prepared, WoundEffectOperationBatch batch)
    {
        if (!JointRepairPreparedAgrees(prepared) ||
            !TryReadJointRepair(prepared.JointRepairContinuationAuthority, out var joint) ||
            batch.SourceExportFingerprint != WoundAcceptedTurnFingerprints.ComputeSourceExport(batch) ||
            prepared.EffectOperationBatches.Count(candidate => candidate.PreparedWoundId == batch.PreparedWoundId &&
                candidate.SourceExportFingerprint == batch.SourceExportFingerprint &&
                candidate.TransitionAuthority.AuthoritySeal == batch.TransitionAuthority.AuthoritySeal) != 1)
            throw new InvalidDataException("The batch does not belong to the complete private joint preparation.");
        return joint;
    }

    internal static WoundAcceptedTurnBinding ReadJointRepairBatchBinding(
        WoundPreparedAcceptedTurnPlan prepared, WoundEffectOperationBatch batch)
    {
        var joint = ReadJointRepairBatchOwner(prepared, batch);
        return batch.PreparedWoundId == joint.Outcome.Before.WoundId
            ? joint.TreatmentInput.Binding : joint.AcquisitionInput.Binding;
    }

    internal static IReadOnlyList<WoundAcceptedTurnBinding> ReadJointRepairOwnerBindings(WoundPreparedAcceptedTurnPlan prepared)
    {
        if (!JointRepairPreparedAgrees(prepared) || !TryReadJointRepair(prepared.JointRepairContinuationAuthority, out var joint))
            throw new InvalidDataException("Joint collision inventory requires both privately authenticated owners.");
        return [joint.AcquisitionInput.Binding, joint.TreatmentInput.Binding];
    }

    internal static string ReadJointRepairBatchInputFingerprint(
        WoundPreparedAcceptedTurnPlan prepared, WoundEffectOperationBatch batch)
    {
        var joint = ReadJointRepairBatchOwner(prepared, batch);
        return WoundAcceptedTurnFingerprints.ComputeInput(batch.PreparedWoundId == joint.Outcome.Before.WoundId
            ? joint.TreatmentInput : joint.AcquisitionInput);
    }

    internal static bool TryReadJointRepairTreatmentBatch(WoundPreparedAcceptedTurnPlan prepared,
        WoundEffectOperationBatch batch, out JointRepairFinalView view)
    {
        view = null!;
        if (prepared.JointRepairContinuationAuthority is null) return false;
        var joint = ReadJointRepairBatchOwner(prepared, batch);
        return batch.PreparedWoundId == joint.Outcome.Before.WoundId && TryReadJointRepairFinal(prepared, out view);
    }

    internal static EffectAcceptedTurnInput ComposeJointRepairEffectInput(WoundPreparedAcceptedTurnPlan prepared)
    {
        if (!JointRepairPreparedAgrees(prepared) || !TryReadJointRepair(prepared.JointRepairContinuationAuthority, out var joint))
            throw new InvalidDataException("Joint effect input requires the complete private preparation.");
        var binding = joint.AcquisitionInput.Binding;
        var original = joint.AcquisitionInput;
        var sources = joint.ReadEffectSourceRoots();
        return EffectAcceptedTurnInputComposer.Compose(binding.SessionId, binding.SnapshotToken, binding.Turn,
            EffectAcceptedTurnInputComposer.CreateEmptyCommandRoot(), original.PreTurnEffectCarriers!,
            original.PreTurnEffectCarriers!, original.PreTurnEffectIdentityIndex, sources,
            currentWorldTime: joint.Accepted.CurrentGameMinute, publicationCarrierBaselines: original.PreTurnEffectCarriers,
            realm: binding.Realm, preparedWoundPlan: prepared);
    }

    internal static bool JointRepairEffectInputAgrees(WoundPreparedAcceptedTurnPlan prepared, EffectAcceptedTurnInput input) =>
        WoundAcceptedTurnFingerprints.ComputeEffectInput(prepared, ComposeJointRepairEffectInput(prepared)) ==
        WoundAcceptedTurnFingerprints.ComputeEffectInput(prepared, input);

    internal static bool JointRepairFinalPlanAgrees(AcceptedMechanicsWoundStageBundle bundle)
    {
        if (!JointRepairPreparedAgrees(bundle.PreparedPlan) ||
            !JointRepairEffectInputAgrees(bundle.PreparedPlan, bundle.EffectBatchPlan.EffectInput)) return false;
        var replay = WoundAcceptedTurnPlannerCore.Finalize(bundle.PreparedPlan,
            new WoundEffectBatchPlanningResult(bundle.EffectBatchPlan, Array.Empty<ValidationIssue>()));
        return replay.Success && replay.Plan!.WoundFinalPlanFingerprint == bundle.WoundFinalPlanFingerprint;
    }

    internal static WoundAcceptedTurnPreparationResult PrepareJointRepairCandidate(WoundAcceptedTurnInput input, object authority)
    {
        if (!TryReadJointRepair(authority, out var joint) ||
            WoundAcceptedTurnFingerprints.ComputeInput(input) != WoundAcceptedTurnFingerprints.ComputeInput(joint.AcquisitionInput))
            return new(null, JointPreparationFailure("input_invalid", "Joint preparation cannot accept a substituted acquisition input.").Issues);
        return PrepareCore(input, new WoundAcceptedTurnIdentityAllocator(), WoundOperationBeforeData.FromOriginal(input), null, authority);
    }

    private static IReadOnlyList<ValidationIssue> AppendJointRepairPreparation(object value,
        List<string> woundIds, List<string> transitionIds, List<string> applicationRefs,
        List<WoundMaterializationEnvelope> wounds, List<WoundEffectOperationBatch> batches)
    {
        if (!TryReadJointRepair(value, out var joint) || wounds.Count != 1 || batches.Count != 1 ||
            batches[0].TransitionAuthority.TransitionKind != "create")
            return JointPreparationFailure("prepared_invalid", "The joint contribution requires exactly one validated acquisition.").Issues;
        var before = joint.Outcome.Before;
        var transition = joint.Outcome.TransitionId;
        var history = WoundHistoryState.Parse(joint.AcquisitionInput.PreTurnHistory.ToJsonString(), WoundHistoryState.HistoryPath);
        var existing = WoundCarrierCatalog.Build(joint.AcquisitionInput.PreTurnCarriers).Occurrences
            .Where(row => row.WoundId == before.WoundId).ToArray();
        if (!history.IsValid || existing.Length != 1 ||
            WoundIdentityState.ComputeSemanticFingerprint(existing[0].Wound) != WoundIdentityState.ComputeSemanticFingerprint(before) ||
            history.State!.Transitions.Any(row => row.TransitionId == transition))
            return JointPreparationFailure("baseline_invalid", "The independently selected existing treatment wound/history changed.").Issues;
        var applications = joint.TreatmentBatch?.RootApplications.Select(row => row.ApplicationRef).ToArray() ?? [];
        if (!JointExactUnique(woundIds.Append(before.WoundId)) || !JointExactUnique(transitionIds.Append(transition)) ||
            !JointExactUnique(applicationRefs.Concat(applications)))
            return JointPreparationFailure("allocation_collision", "Joint allocation collides with the derived treatment identities.").Issues;
        woundIds.Add(before.WoundId); transitionIds.Add(transition); applicationRefs.AddRange(applications);
        wounds.Add(joint.Outcome.GetPreparedCarrierImage());
        if (joint.TreatmentBatch is { } batch) batches.Add(batch);
        return Array.Empty<ValidationIssue>();
    }
    private static bool JointExactUnique(IEnumerable<string> values)
    {
        var seen = new HashSet<string>(StringComparer.Ordinal); var aliases = new HashSet<string>(StringComparer.Ordinal);
        return values.All(value => ResourceMaterializationContract.IsExactIdentifier(value) && seen.Add(value) &&
            aliases.Add(MortalLocationIdentityState.BuildConfusableKey(value)));
    }
    internal static bool JointRepairPreparedAgrees(WoundPreparedAcceptedTurnPlan prepared)
    {
        if (!TryReadJointRepair(prepared.JointRepairContinuationAuthority, out var joint) ||
            prepared.TreatmentContinuationAuthority is not null || prepared.RecoveryContinuationAuthority is not null || prepared.DraftBefore is not null ||
            prepared.PreparedWounds.Count != 2 || prepared.AllocatedWoundIds.Count != 2 || prepared.AllocatedTransitionIds.Count != 2 ||
            prepared.EffectOperationBatches.Count != (joint.TreatmentBatch is null ? 1 : 2) ||
            prepared.BindingFingerprint != WoundAcceptedTurnFingerprints.ComputeBinding(joint.AcquisitionInput.Binding) ||
            prepared.InputFingerprint != WoundAcceptedTurnFingerprints.ComputeInput(joint.AcquisitionInput)) return false;
        var acquisition = prepared.EffectOperationBatches[0];
        var treatment = joint.Outcome.GetPreparedCarrierImage();
        if (acquisition.PreparedWoundId != prepared.PreparedWounds[0].WoundId ||
            acquisition.TransitionAuthority.TransitionKind != "create" ||
            acquisition.TransitionAuthority.PreparedInputFingerprint != prepared.InputFingerprint ||
            acquisition.SourceExportFingerprint != WoundAcceptedTurnFingerprints.ComputeSourceExport(acquisition) ||
            prepared.AllocatedWoundIds[0] != prepared.PreparedWounds[0].WoundId ||
            prepared.AllocatedTransitionIds[0] != prepared.PreparedWounds[0].LastTransition.TransitionId ||
            prepared.AllocatedWoundIds[1] != treatment.WoundId || prepared.AllocatedTransitionIds[1] != joint.Outcome.TransitionId ||
            WoundMaterializationContract.SerializeCanonical(prepared.PreparedWounds[1]) != WoundMaterializationContract.SerializeCanonical(treatment)) return false;
        var batchAgrees = joint.TreatmentBatch is not { } expected ||
            prepared.EffectOperationBatches[1].SourceExportFingerprint == expected.SourceExportFingerprint &&
            WoundAcceptedTurnFingerprints.ComputeSourceExport(prepared.EffectOperationBatches[1]) == expected.SourceExportFingerprint &&
            prepared.EffectOperationBatches[1].TransitionAuthority.AuthoritySeal == expected.TransitionAuthority.AuthoritySeal;
        if (!batchAgrees) return false;
        // Recompute the complete private contribution graph, not just caller-supplied
        // hashes. This is an uncached pure replay with the same deterministic allocator.
        var replay = PrepareJointRepairCandidate(joint.AcquisitionInput, prepared.JointRepairContinuationAuthority!);
        return replay.Success && replay.Plan is not null &&
            replay.Plan.WoundPreparationFingerprint == prepared.WoundPreparationFingerprint;
    }
}
