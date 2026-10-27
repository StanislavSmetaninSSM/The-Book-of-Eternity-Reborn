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
        private static readonly OperationValidationRule ContinuationDataRule = new("Continuation", 1);
        private readonly JointPrivateFingerprintData _fingerprintData;
        private readonly WoundAcceptedTurnInput _acquisitionInput;
        private readonly WoundAcceptedTurnInput _treatmentInput;
        private readonly MortalWoundTreatmentOutcomePreparation _outcome;
        private readonly MortalWoundTreatmentSemanticData _semanticData;
        private readonly bool _stageReplayEligible;
        private readonly WoundEffectOperationBatch? _treatmentBatch;
        /// <summary>
        /// Retains the issued joint preparation's detached DATA and actual companions, then computes its original fingerprint.
        /// </summary>
        /// <param name="issuer">
        /// The existing private issuer identity; a different value is refused.
        /// </param>
        /// <param name="files">
        /// The non-null filesystem host for current validation and synchronous fingerprint diagnostics.
        /// </param>
        /// <param name="generation">
        /// The generation captured by the owning preparation.
        /// </param>
        /// <param name="checkpoint">
        /// The rejected-repair checkpoint whose retained fingerprint participates in the joint seal.
        /// </param>
        /// <param name="acquisitionInput">
        /// The acquisition input copied into private retained DATA with its actual companions.
        /// </param>
        /// <param name="treatmentInput">
        /// The treatment input copied into private retained DATA with its actual companions.
        /// </param>
        /// <param name="acquisitionCommandRoot">
        /// The corrected acquisition command JSON to deep-copy before fingerprinting.
        /// </param>
        /// <param name="accepted">
        /// The genuine accepted treatment-state companion retained by identity.
        /// </param>
        /// <param name="request">
        /// The actual treatment request companion retained by identity.
        /// </param>
        /// <param name="resolution">
        /// The actual treatment resolution companion retained by identity.
        /// </param>
        /// <param name="finalization">
        /// The actual resource finalization companion retained by identity.
        /// </param>
        /// <param name="outcome">
        /// The outcome preparation to retain as a detached copy.
        /// </param>
        /// <param name="rematerialization">
        /// The successful rematerialization result supplying its existing optional batch and authority.
        /// </param>
        /// <param name="before">
        /// The before-image DATA to copy in supplied enumeration order; <see langword="null"/> values denote absent files,
        /// and byte arrays are cloned.
        /// </param>
        internal JointRepairContinuationAuthority(object issuer, FileSystemManager files,
            string generation, MortalJointRepairCheckpoint checkpoint,
            WoundAcceptedTurnInput acquisitionInput, WoundAcceptedTurnInput treatmentInput,
            JsonObject acquisitionCommandRoot,
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
            var commandRoot = acquisitionCommandRoot.DeepClone().AsObject();
            _treatmentInput = WoundAcceptedTurnData.CloneInput(treatmentInput)!;
            Accepted = accepted; Request = request; Resolution = resolution; Finalization = finalization;
            _outcome = outcome.DetachedCopy(); _treatmentBatch = rematerialization.Batch;
            _semanticData = new(_outcome, resolution);
            Rematerialization = rematerialization.Authority;
            _stageReplayEligible = _treatmentBatch is null && Rematerialization is null &&
                _acquisitionInput.Opportunities.All(opportunity => opportunity.OriginalSourceGuarantee is null);
            var beforeImages = JointPrivateFingerprintData.BeforeImages.Copy(before);
            using (files.EnterOperationTimingContext())
            using (FileSystemManager.MeasureCurrentOperation("JointContinuation.InitialFingerprint"))
            {
                Fingerprint = CaptureInitialFingerprint(commandRoot, beforeImages, out var fingerprintData);
                _fingerprintData = fingerprintData;
            }
        }
        internal string Generation { get; }
        internal MortalJointRepairCheckpoint Checkpoint { get; }
        internal MortalWoundTreatmentAcceptedStateAuthority Accepted { get; }
        internal MortalWoundTreatmentAttemptRequest Request { get; }
        internal MortalWoundTreatmentResolution Resolution { get; }
        internal MortalWoundTreatmentResourceFinalization Finalization { get; }
        internal MortalWoundTreatmentRematerializationAuthority? Rematerialization { get; }
        internal WoundAcceptedTurnInput AcquisitionInput => WoundAcceptedTurnData.CloneInput(_acquisitionInput)!;
        /// <summary>
        /// Gets a detached command view reconstructed from its exact immutable serialization.
        /// </summary>
        internal JsonObject AcquisitionCommandRoot => _fingerprintData.ReadCommandRoot();
        /// <summary>
        /// Reads a detached copy of the retained accepted narrative.
        /// </summary>
        /// <returns>
        /// The original narrative bytes; an absent captured narrative is rejected.
        /// </returns>
        internal byte[] ReadAcceptedNarrative() => _fingerprintData.ReadBeforeBytes("output/narrative_response.json")
            ?? throw new InvalidDataException("The sealed joint continuation has no accepted narrative.");
        /// <summary>
        /// Reads and validates the retained interface publication when this checkpoint uses one.
        /// </summary>
        /// <returns>
        /// The retained publication record, or <see langword="null"/> for a legacy checkpoint.
        /// </returns>
        internal MortalJointInterfacePublication.Record? ReadPublishedInterface()
        {
            if (!Checkpoint.UsesInterfacePublication) return null;
            var pending = StrictJsonAuthority.Deserialize<JsonObject>(CanonicalJsonUtf8.DecodeOneOptionalBom(
                _fingerprintData.ReadBeforeBytes(WoundAcceptedTurnSnapshotContract.PendingResolutionPath) ?? throw new InvalidDataException("joint_interface_pending_missing")),
                SharedJsonOptions.PrettyCamelCaseUnsafeRelaxed, "sealed joint interface result")!;
            var result = MortalJointInterfacePublication.Read(System.Text.Json.JsonSerializer.SerializeToElement(
                pending[MortalJointInterfacePublication.PendingField] ?? throw new InvalidDataException("joint_interface_result_missing")));
            result.RequireLegacyPending(pending);
            if (result.State != "Published" || result.HasErrors || !MortalJointInterfacePublication.BytesEqual(
                    result.AfterBytes, _fingerprintData.ReadBeforeBytes(MortalJointInterfacePublication.InterfacePath)))
                throw new InvalidDataException("joint_interface_result_not_consumed_current");
            return result;
        }
        internal WoundAcceptedTurnInput TreatmentInput => WoundAcceptedTurnData.CloneInput(_treatmentInput)!;
        internal MortalWoundTreatmentOutcomePreparation Outcome => _outcome.DetachedCopy();
        internal WoundEffectOperationBatch? TreatmentBatch => _treatmentBatch is null ? null : WoundAcceptedTurnData.CloneOperationBatch(_treatmentBatch);
        internal string Fingerprint { get; }
        /// <summary>
        /// Gets the original operation only when every retained acquisition companion is privately owned.
        /// </summary>
        internal ValidationOperation? StageReplayOperation => _stageReplayEligible
            ? _files.GetCurrentSemanticDataOperation() : null;

        /// <summary>
        /// Gets this exact continuation's non-authorizing semantic DATA evaluator.
        /// </summary>
        internal MortalWoundTreatmentSemanticData SemanticData => _semanticData;

        /// <summary>Reads shared expected DATA behind this scalar consumer's existing fresh actual checks.</summary>
        /// <returns>The expected seal, or the unchanged original source refusal.</returns>
        internal string? ReadPreparationFingerprint()
        {
            var operation = StageReplayOperation;
            var fixedInput = AcquisitionInput;
            ExpectedPreparationReplayProjection? Source()
            {
                using var replayTiming = FileSystemManager.MeasureCurrentOperation("JointPreparation.Replay");
                return ExpectedPreparationReplayProjection.Capture(PrepareJointRepairCandidate(fixedInput, this));
            }
            if (operation is null) return Source()?.WoundPreparationFingerprint;
            OperationStructuralSnapshot? input;
            try
            {
                using var keyTiming = FileSystemManager.MeasureCurrentOperation("JointPreparation.Replay.Key");
                input = WoundAcceptedTurnFingerprints.CapturePreparationReplayInput(fixedInput, ReplayCompanions());
            }
            catch (Exception error) when (IsReplayCaptureFailure(error))
            {
                return MortalWoundTreatmentSemanticData.ReadPreparationProjectionFallback(operation, Source)?.WoundPreparationFingerprint;
            }
            return input is null
                ? MortalWoundTreatmentSemanticData.ReadPreparationProjectionFallback(operation, Source)?.WoundPreparationFingerprint
                : _semanticData.ReadPreparationProjection(operation, input, Source)?.WoundPreparationFingerprint;
        }

        /// <summary>Returns exact stable private companion identities for both expected-DATA consumers.</summary>
        /// <returns>References only; none grants admission or permission.</returns>
        internal object?[] ReplayCompanions() => [this, _acquisitionInput, _treatmentInput, Checkpoint,
            _fingerprintData, Accepted, Request, Resolution, Finalization, _outcome, Generation, Fingerprint];
        /// <summary>
        /// Parses detached effect source roots from the private before-images.
        /// </summary>
        /// <returns>
        /// A fresh source map with null values for captured absent files.
        /// </returns>
        internal IReadOnlyDictionary<string, JsonNode?> ReadEffectSourceRoots() =>
            EffectAcceptedTurnInputComposer.SourceAuthorityPaths.ToDictionary(path => path, path =>
                (JsonNode?)_fingerprintData.ReadEffectSourceRoot(path), StringComparer.Ordinal);
        /// <summary>
        /// Parses and checks the captured acquisition command against its original opportunities.
        /// </summary>
        /// <returns>
        /// The single acquisition command; mixed or malformed command roots are rejected.
        /// </returns>
        internal IReadOnlyList<WoundResponseCommandDraft> ReadAcquisitionCommands()
        {
            // This root came only from the factory's fresh actual corrected QTE
            // adapter, not the physical homogeneous treatment command or a caller.
            var parsed = WoundResponseInputComposer.ParseCommandRoot(
                System.Text.Json.JsonSerializer.SerializeToElement(_fingerprintData.ReadCommandRoot()),
                _acquisitionInput.Opportunities);
            if (!parsed.Success || parsed.Commands.Count != 1 || parsed.TreatmentCommands.Count != 0 ||
                parsed.AcceptedTransitionCommands.Count != 0)
                throw new InvalidDataException("The privately retained corrected acquisition command is not one exact closed CREATE source.");
            return parsed.Commands;
        }
        /// <summary>
        /// Checks retained request, resolution and resource agreement together with the recomputed private graph seal.
        /// </summary>
        /// <value>
        /// <see langword="true"/> when every retained agreement matches; otherwise <see langword="false"/>.
        /// Existing malformed-data exceptions propagate.
        /// </value>
        internal bool HasValidSeal
        {
            get
            {
                using var operationTiming = FileSystemManager.MeasureCurrentOperation("JointContinuation.ValidateSeal");
                var operation = _files.GetCurrentSemanticDataOperation();
                return ReferenceEquals(Resolution.RequestAuthority, Request) &&
                    _semanticData.ReadOutcomeAgreement(operation) && Finalization.RequestFingerprint == Request.RequestFingerprint &&
                    Finalization.ResultFingerprint == Resolution.ResultFingerprint &&
                    Finalization.ResourceAuthorityFingerprint == Request.ResourceAuthority.AuthorityFingerprint &&
                    _semanticData.ReadFinalizationFingerprint(operation) == Finalization.FinalizationFingerprint &&
                    ComputeFingerprint() == Fingerprint;
            }
        }

        /// <summary>
        /// Captures immutable private fields in their original evaluation order and computes the initial seal.
        /// </summary>
        /// <param name="commandRoot">
        /// The private command clone, serialized after acquisition fingerprinting and before treatment fingerprinting.
        /// </param>
        /// <param name="beforeImages">
        /// Bytes already copied in source insertion order before initial fingerprint computation.
        /// </param>
        /// <param name="fingerprintData">
        /// Receives the prefix captured before live scalar reads, together with byte hashes derived after those reads.
        /// </param>
        /// <returns>
        /// The original field-sequence digest; it grants no fresh-owner or current-file guarantee.
        /// </returns>
        private string CaptureInitialFingerprint(JsonObject commandRoot,
            JointPrivateFingerprintData.BeforeImages beforeImages, out JointPrivateFingerprintData fingerprintData)
        {
            using var operationTiming = FileSystemManager.MeasureCurrentOperation("JointContinuation.ComputeFingerprint");
            var fields = new List<string?> { JointRepairDomain, Generation };
            var prefix = JointPrivateFingerprintData.Prefix.Capture(Checkpoint.ToJsonObject(),
                _acquisitionInput, commandRoot, _treatmentInput);
            prefix.AppendTo(fields);
            fields.AddRange(new string?[]
            {
                Accepted.AcceptedStateFingerprint,
                Request.RequestFingerprint, Resolution.ResultFingerprint, Resolution.ResolutionAuthorityFingerprint,
                Finalization.FinalizationFingerprint, _outcome.Fingerprint, Rematerialization?.AuthoritySeal
            });
            fingerprintData = JointPrivateFingerprintData.Capture(prefix, beforeImages);
            fingerprintData.AppendBeforeFields(fields);
            return WoundAcceptedTurnFingerprintWriter.Compute(fields);
        }

        /// <summary>
        /// Recomputes the final seal using immutable private fields and fresh companion scalar reads.
        /// </summary>
        /// <returns>
        /// The digest of the current retained graph; byte hashing is reused only for privately owned immutable data.
        /// </returns>
        private string ComputeFingerprint()
        {
            using var operationTiming = FileSystemManager.MeasureCurrentOperation("JointContinuation.ComputeFingerprint");
            var fields = new List<string?> { JointRepairDomain, Generation };
            _fingerprintData.AppendPrefix(fields);
            fields.AddRange(new string?[]
            {
                Accepted.AcceptedStateFingerprint,
                Request.RequestFingerprint, Resolution.ResultFingerprint, Resolution.ResolutionAuthorityFingerprint,
                Finalization.FinalizationFingerprint, _outcome.Fingerprint, Rematerialization?.AuthoritySeal
            });
            _fingerprintData.AppendBeforeFields(fields);
            return WoundAcceptedTurnFingerprintWriter.Compute(fields);
        }

        /// <summary>
        /// Revalidates the actual owner, original, insertion-ordered file bytes, baselines and confirmed held claim.
        /// </summary>
        /// <param name="files">
        /// The same physical file manager retained by the continuation.
        /// </param>
        /// <param name="lease">
        /// The caller-owned active canonical write lease used for every current file read.
        /// </param>
        /// <returns>
        /// A task completing only after the existing live guards succeed.
        /// </returns>
        internal async Task ValidateCurrentAsync(FileSystemManager files, FileSystemManager.CanonicalWriteLease lease)
        {
            try
            {
                using var namedOperationTimer20 = files.MeasureOperation("Continuation.ValidateCurrentAsync");
                using (files.EnterOperationTimingContext())
                {
                    files.EnsureCanonicalWriteLeaseActive(lease);
                    if (!ReferenceEquals(files, _files) || !files.IsCurrentSessionGeneration(lease, Generation) || !HasValidSeal)
                        throw new InvalidDataException("Joint preparation belongs to a stale or foreign physical owner.");
                }
                await files.ValidateRecoveryDataAsync(ContinuationDataRule, this, lease,
                    () => ValidateSourceDataAsync(files, lease));
                using var namedOperationTimer24 = files.MeasureOperation("Continuation.InspectConfirmedHeldClaim");
                var confirmed = AcceptedTurnAuthorityRegistry.InspectConfirmedMortalWoundTreatmentResourceClaim(files, lease, Request, Finalization);
                namedOperationTimer24?.Dispose();
                if (!confirmed.IsValid || confirmed.State != MortalWoundTreatmentResourceReservationState.ConfirmedHeld)
                    throw new InvalidDataException("The same joint request must still own its confirmed resource agreement.");
            }
            catch { files.InvalidateRecoveryValidationAfterFailure(); throw; }
        }

        /// <summary>
        /// Checks snapshot/frame, ordered before-images and publication-baseline DATA without changing claims.
        /// </summary>
        /// <param name="files">
        /// Exact physical manager already checked by the live continuation wrapper.
        /// </param>
        /// <param name="lease">
        /// Original active lease used for the unchanged real DATA readers.
        /// </param>
        /// <returns>
        /// Completion after all original DATA comparisons succeed.
        /// </returns>
        private async Task ValidateSourceDataAsync(FileSystemManager files, FileSystemManager.CanonicalWriteLease lease)
        {
            using var namedOperationTimer21 = files.MeasureOperation("Continuation.ReadSnapshot");
            var original = MortalQteWoundOpportunityProjection.ReadSnapshot(files, lease);
            namedOperationTimer21?.Dispose();
            var binding = _acquisitionInput.Binding;
            if (original.SessionId != binding.SessionId || original.RequestId != binding.RequestId ||
                original.SnapshotToken != binding.SnapshotToken || original.TurnNumber != binding.Turn || original.Realm != binding.Realm)
                throw new InvalidDataException("The actual joint original no longer matches its independently bound sources.");
            using var namedOperationTimer22 = files.MeasureOperation("Continuation.CompareBeforeImages");
            foreach (var path in _fingerprintData.ReadBeforePaths())
            {
                var actual = await files.ReadFileBytesAsync(lease, path);
                if (!_fingerprintData.MatchesBefore(path, actual))
                    throw new InvalidDataException("Joint preparation before-image changed: " + path);
            }
            namedOperationTimer22?.Dispose();
            using var namedOperationTimer23 = files.MeasureOperation("Continuation.ReadPublicationBaselines");
            var baselines = ReadPublicationBaselines(files, lease);
            namedOperationTimer23?.Dispose();
            using (files.EnterOperationTimingContext())
                if (baselines.Issues.Count != 0 || WoundAcceptedTurnFingerprints.ComputeInput(new WoundAcceptedTurnInput(
                        _acquisitionInput.Binding, _acquisitionInput.Opportunities, _acquisitionInput.Transitions,
                        baselines.WoundCarriers!, baselines.WoundIdentity!, baselines.WoundHistory!,
                        baselines.EffectCarriers, baselines.EffectIdentity)) != WoundAcceptedTurnFingerprints.ComputeInput(_acquisitionInput))
                    throw new InvalidDataException("The joint wound/effect baseline changed while its guards were captured.");
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
        using var namedOperationTimer25 = files.MeasureOperation("Preparation.CreateJointRepairPreparationAsync");
        try
        {
            capture.EnsureCurrent(files, lease);
            if (await files.ReadFileBytesAsync(lease, ResourceMaterializationContract.CommandPath) is not null ||
                await files.ReadFileBytesAsync(lease, EffectAcceptedTurnPlan.CommandPath) is not null)
                return JointPreparationFailure("concurrent_commands", "The first joint contour cannot admit foreign resource/effect commands.");
            var previous = capture;
            var frame = previous.Accepted.Binding;
            using var namedOperationTimer26 = files.MeasureOperation("Preparation.ReconstructAsync");
            var reconstructed = await MortalJointRepairCheckpoint.ReconstructAsync(files, lease,
                frame.SessionId, frame.RequestId, frame.SnapshotToken, frame.Turn);
            namedOperationTimer26?.Dispose();
            if (!reconstructed.IsValid || reconstructed.Capture is not { } current ||
                current.Accepted.AcceptedStateFingerprint != previous.Accepted.AcceptedStateFingerprint ||
                current.Request.RequestFingerprint != previous.Request.RequestFingerprint ||
                current.Resolution.ResolutionAuthorityFingerprint != previous.Resolution.ResolutionAuthorityFingerprint ||
                !JsonNode.DeepEquals(current.Checkpoint.ToJsonObject(), previous.Checkpoint.ToJsonObject()))
                throw new InvalidDataException("Fresh installed-wave treatment reconstruction no longer agrees with the rejected capture.");
            // Use the freshly exported owner/request/claim, never bless new current
            // bytes with a previously captured capability or parsed checkpoint seal.
            capture = current;
            using var namedOperationTimer27 = files.MeasureOperation("Preparation.RevalidateCorrectionAsync");
            var correction = await MortalJointRepairCheckpoint.RevalidateCorrectionAsync(files, lease, capture);
            namedOperationTimer27?.Dispose();
            if (correction.SourceBinding is not { } acquisitionBinding || correction.MaterializedOpportunities.Count != 1 ||
                correction.Transitions.Count != 1 || correction.Transitions[0].Kind != "create" ||
                correction.MaterializedOpportunities[0].WorseningTarget is not null)
                return JointPreparationFailure("acquisition", "The real corrected adapter must provide one independent CREATE binding.");
            if (correction.CommandRoot is not { } acquisitionCommandRoot)
                return JointPreparationFailure("acquisition_command", "The fresh corrected adapter must retain its actual closed acquisition command.");
            var treatmentBinding = capture.Accepted.Binding;
            if (!SameJointFrame(acquisitionBinding, treatmentBinding))
                return JointPreparationFailure("binding", "Each independently validated owner must bind the same actual original frame.");
            using var namedOperationTimer28 = files.MeasureOperation("Preparation.ReadPublicationBaselines");
            var baselines = ReadPublicationBaselines(files, lease);
            namedOperationTimer28?.Dispose();
            if (baselines.Issues.Count != 0) return new(null, null, baselines.Issues);
            var input = new WoundAcceptedTurnInput(acquisitionBinding, correction.MaterializedOpportunities,
                correction.Transitions, baselines.WoundCarriers!, baselines.WoundIdentity!, baselines.WoundHistory!,
                baselines.EffectCarriers, baselines.EffectIdentity);
            var treatmentInput = new WoundAcceptedTurnInput(treatmentBinding, Array.Empty<WoundOpportunityAuthority>(),
                Array.Empty<WoundAcceptedTransitionDraft>(), baselines.WoundCarriers!, baselines.WoundIdentity!,
                baselines.WoundHistory!, baselines.EffectCarriers, baselines.EffectIdentity);
            using var namedOperationTimer29 = files.MeasureOperation("Preparation.ValidateInput");
            var validation = WoundAcceptedTurnPlannerCore.ValidateInput(input);
            namedOperationTimer29?.Dispose();
            if (validation.Issues.Count != 0) return new(null, null, validation.Issues);
            using var namedOperationTimer30 = files.MeasureOperation("Preparation.PrepareTreatmentOutcome");
            var preparation = MortalWoundTreatmentOutcomePublicationPlanner.Prepare(capture.Accepted,
                capture.Request, capture.Resolution, capture.Accepted.CurrentGameMinute);
            namedOperationTimer30?.Dispose();
            if (!preparation.IsValid) return new(null, null, preparation.Issues);
            using var namedOperationTimer31 = files.MeasureOperation("Preparation.ValidateTreatmentPublicationShell");
            var shell = ValidateTreatmentPublicationShell(capture.Accepted, capture.Request, capture.Resolution);
            namedOperationTimer31?.Dispose();
            if (shell.Finalization is null || shell.Issues.Count != 0) return new(null, null, shell.Issues);
            using var namedOperationTimer32 = files.MeasureOperation("Preparation.PrepareSeverityRematerialization");
            var rematerialization = MortalWoundTreatmentSeverityRematerializationPlanner.Prepare(treatmentInput,
                preparation.Preparation!, capture.Request.RequestFingerprint, capture.Resolution.ResolutionAuthorityFingerprint,
                capture.Resolution.ResultFingerprint, capture.Request.Coordinates.AttemptId,
                capture.Request.Coordinates.OperationKey, WoundIdentityState.ComputeSemanticFingerprint(capture.Request.RouteSourceWound));
            namedOperationTimer32?.Dispose();
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
            }).Concat(capture.Checkpoint.UsesInterfacePublication ? new[] { MortalJointInterfacePublication.InterfacePath } : Array.Empty<string>())
                .Distinct(StringComparer.Ordinal).ToArray();
            var before = new Dictionary<string, byte[]?>(StringComparer.Ordinal);
            using var namedOperationTimer33 = files.MeasureOperation("Preparation.CaptureBeforeImages");
            foreach (var path in paths) before.Add(path, await files.ReadFileBytesAsync(lease, path));
            namedOperationTimer33?.Dispose();
            using var namedOperationTimer34 = files.MeasureOperation("Preparation.ConstructContinuationAuthority");
            var authority = new JointRepairContinuationAuthority(JointRepairIssuer, files, capture.Accepted.SessionGeneration,
                capture.Checkpoint, input, treatmentInput, acquisitionCommandRoot, capture.Accepted, capture.Request, capture.Resolution,
                shell.Finalization, preparation.Preparation!, rematerialization, before);
            namedOperationTimer34?.Dispose();
            using var namedOperationTimer35 = files.MeasureOperation("Preparation.ReadPublishedInterface");
            _ = authority.ReadPublishedInterface();
            namedOperationTimer35?.Dispose();
            using var namedOperationTimer36 = files.MeasureOperation("Preparation.ValidateContinuation");
            await authority.ValidateCurrentAsync(files, lease);
            namedOperationTimer36?.Dispose();
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
    internal static MortalWoundTreatmentResourceFinalization ReadJointRepairResourceFinalization(object value) =>
        TryReadJointRepair(value, out var authority) ? authority.Finalization : throw new InvalidDataException("Invalid joint finalization.");

    // Detached views are non-authorizing. All readers first authenticate the whole
    // private prepared graph; neither a common frame nor a caller batch issues one.
    /// <summary>
    /// Exposes detached joint DATA after its original preparation admission.
    /// </summary>
    /// <param name="Outcome">
    /// An independent detached outcome image.
    /// </param>
    /// <param name="Resolution">
    /// The exact retained resolution companion.
    /// </param>
    /// <param name="PersistedResult">
    /// A detached persistence representation of that resolution.
    /// </param>
    /// <param name="TreatmentBatch">
    /// A detached optional treatment batch; <see langword="null"/> means no rematerialization.
    /// </param>
    /// <param name="SemanticData">
    /// The original continuation's structurally guarded DATA evaluator.
    /// </param>
    /// <param name="Operation">
    /// The eligible already owning operation, or <see langword="null"/> for original computation.
    /// </param>
    internal sealed record JointRepairFinalView(
        MortalWoundTreatmentOutcomePreparation Outcome, MortalWoundTreatmentResolution Resolution,
        MortalWoundTreatmentPersistedResult PersistedResult, WoundEffectOperationBatch? TreatmentBatch,
        MortalWoundTreatmentSemanticData SemanticData, ValidationOperation? Operation);

    internal static bool TryReadJointRepairFinal(WoundPreparedAcceptedTurnPlan prepared,
        out JointRepairFinalView view)
    {
        view = null!;
        if (!JointRepairPreparedAgrees(prepared) ||
            !TryReadJointRepair(prepared.JointRepairContinuationAuthority, out var joint)) return false;
        view = new(joint.Outcome, joint.Resolution,
            MortalWoundTreatmentPersistedResult.Create(joint.Resolution), joint.TreatmentBatch,
            joint.SemanticData, joint.StageReplayOperation);
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

    /// <summary>
    /// Composes the joint effect input from its agreeing private preparation and retained acquisition provenance.
    /// </summary>
    /// <param name="prepared">
    /// The non-null whole joint preparation whose actual companion and source data must still agree.
    /// </param>
    /// <returns>
    /// The composed effect input with bound diagnostic locations; mismatched preparation or provenance is refused.
    /// </returns>
    internal static EffectAcceptedTurnInput ComposeJointRepairEffectInput(WoundPreparedAcceptedTurnPlan prepared)
    {
        using var operationTiming = FileSystemManager.MeasureCurrentOperation("JointEffectInput.Compose");
        if (!JointRepairPreparedAgrees(prepared) || !TryReadJointRepair(prepared.JointRepairContinuationAuthority, out var joint))
            throw new InvalidDataException("Joint effect input requires the complete private preparation.");
        using var buildTiming = FileSystemManager.MeasureCurrentOperation("JointEffectInput.Build");
        var binding = joint.AcquisitionInput.Binding;
        var original = joint.AcquisitionInput;
        var sources = joint.ReadEffectSourceRoots();
        var effectInput = EffectAcceptedTurnInputComposer.Compose(binding.SessionId, binding.SnapshotToken, binding.Turn,
            EffectAcceptedTurnInputComposer.CreateEmptyCommandRoot(), original.PreTurnEffectCarriers!,
            original.PreTurnEffectCarriers!, original.PreTurnEffectIdentityIndex, sources,
            currentWorldTime: joint.Accepted.CurrentGameMinute, publicationCarrierBaselines: original.PreTurnEffectCarriers,
            realm: binding.Realm, preparedWoundPlan: prepared);
        // Reproduce the real producer's immutable diagnostic coordinates before
        // ANY effect-input fingerprint/cache use; do not patch only the caller.
        var issues = WoundResponseInputComposer.ValidateSkillScopes(binding,
            joint.ReadAcquisitionCommands(), original.Transitions, effectInput.SkillScopeAuthority, out var locations);
        if (issues.Count != 0 || locations is null)
            throw new InvalidDataException("The private joint acquisition diagnostic provenance was refused: " +
                string.Join("; ", issues.Select(issue => issue.Code + "@" + issue.FilePath + ": " + issue.Message)));
        return effectInput with { WoundApplicationLocations = locations.BindPreparedSources(prepared) };
    }

    internal static bool JointRepairEffectInputAgrees(WoundPreparedAcceptedTurnPlan prepared, EffectAcceptedTurnInput input) =>
        WoundAcceptedTurnFingerprints.ComputeEffectInput(prepared, ComposeJointRepairEffectInput(prepared)) ==
        WoundAcceptedTurnFingerprints.ComputeEffectInput(prepared, input);

    /// <summary>
    /// Replays the retained joint stages and compares their complete final-plan fingerprint.
    /// </summary>
    /// <param name="bundle">
    /// The complete retained joint bundle whose prepared and effect stages must first agree.
    /// </param>
    /// <returns>
    /// <see langword="true"/> when both stage agreements and the real final replay agree; otherwise, <see langword="false"/>.
    /// </returns>
    internal static bool JointRepairFinalPlanAgrees(AcceptedMechanicsWoundStageBundle bundle)
    {
        using var operationTiming = FileSystemManager.MeasureCurrentOperation("JointFinal.ReplayAgreement");
        if (!JointRepairPreparedAgrees(bundle.PreparedPlan) ||
            !JointRepairEffectInputAgrees(bundle.PreparedPlan, bundle.EffectBatchPlan.EffectInput)) return false;
        WoundAcceptedTurnPlanningResult replay;
        using (FileSystemManager.MeasureCurrentOperation("JointFinal.ReplayAgreement.Finalize"))
        {
            replay = WoundAcceptedTurnPlannerCore.Finalize(bundle.PreparedPlan,
                new WoundEffectBatchPlanningResult(bundle.EffectBatchPlan, Array.Empty<ValidationIssue>()));
        }
        return replay.Success && replay.Plan!.WoundFinalPlanFingerprint == bundle.WoundFinalPlanFingerprint;
    }

    internal static WoundAcceptedTurnPreparationResult PrepareJointRepairCandidate(WoundAcceptedTurnInput input, object authority)
    {
        if (!JointPreparationInputAgrees(input, authority, out _))
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
        if (!JointPreparationBaselineAgrees(joint, out var before, out var transition))
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
    /// <summary>Preserves the original candidate's private seal and actual-input admission order.</summary>
    /// <param name="input">The original fixed caller input.</param>
    /// <param name="authority">The same genuine private continuation.</param>
    /// <param name="joint">The freshly authenticated companion.</param>
    /// <returns>The unchanged original input agreement.</returns>
    private static bool JointPreparationInputAgrees(WoundAcceptedTurnInput input, object authority,
        out JointRepairContinuationAuthority joint) => TryReadJointRepair(authority, out joint) &&
        JointPreparationInputDataAgrees(input, joint);

    /// <summary>Compares only deterministic input DATA after the actual private gate has succeeded.</summary>
    /// <param name="input">The same fixed caller input.</param>
    /// <param name="joint">The freshly authenticated real companion.</param>
    /// <returns>The original fingerprint equality, never a permission.</returns>
    private static bool JointPreparationInputDataAgrees(WoundAcceptedTurnInput input, JointRepairContinuationAuthority joint) =>
        WoundAcceptedTurnFingerprints.ComputeInput(input) == WoundAcceptedTurnFingerprints.ComputeInput(joint.AcquisitionInput);

    /// <summary>Preserves the original append's private baseline/history checks and value order.</summary>
    /// <param name="joint">The freshly authenticated actual private continuation.</param>
    /// <param name="before">The original detached private carrier image.</param>
    /// <param name="transition">The original treatment transition.</param>
    /// <returns>Whether the original independently selected treatment baseline agrees.</returns>
    private static bool JointPreparationBaselineAgrees(JointRepairContinuationAuthority joint,
        out WoundMaterializationEnvelope before, out string transition)
    {
        before = joint.Outcome.Before;
        transition = joint.Outcome.TransitionId;
        var history = WoundHistoryState.Parse(joint.AcquisitionInput.PreTurnHistory.ToJsonString(), WoundHistoryState.HistoryPath);
        var selectedBefore = before;
        var transitionId = transition;
        var existing = WoundCarrierCatalog.Build(joint.AcquisitionInput.PreTurnCarriers).Occurrences
            .Where(row => row.WoundId == selectedBefore.WoundId).ToArray();
        return history.IsValid && existing.Length == 1 &&
            WoundIdentityState.ComputeSemanticFingerprint(existing[0].Wound) == WoundIdentityState.ComputeSemanticFingerprint(before) &&
            !history.State!.Transitions.Any(row => row.TransitionId == transitionId);
    }

    /// <summary>Identifies domain DATA capture failures that select the original source, never protocol failures.</summary>
    /// <param name="error">The error from structural capture or moved domain preflight only.</param>
    /// <returns>True for supported malformed DATA exceptions.</returns>
    private static bool IsReplayCaptureFailure(Exception error) => error is ArgumentException or InvalidOperationException or
        System.Text.Json.JsonException or NullReferenceException or OverflowException or NotSupportedException or
        FormatException or InvalidDataException;

    private static bool JointExactUnique(IEnumerable<string> values)
    {
        var seen = new HashSet<string>(StringComparer.Ordinal); var aliases = new HashSet<string>(StringComparer.Ordinal);
        return values.All(value => ResourceMaterializationContract.IsExactIdentifier(value) && seen.Add(value) &&
            aliases.Add(MortalLocationIdentityState.BuildConfusableKey(value)));
    }
    /// <summary>
    /// Checks the complete retained joint preparation against its actual private companion and deterministic replay.
    /// </summary>
    /// <param name="prepared">
    /// The non-null prepared stage whose family, ordered allocations, contribution data and replay seal are compared.
    /// </param>
    /// <returns>
    /// <see langword="true"/> when every retained comparison and replay agrees; otherwise <see langword="false"/>.
    /// Existing malformed-data exceptions propagate.
    /// </returns>
    internal static bool JointRepairPreparedAgrees(WoundPreparedAcceptedTurnPlan prepared)
    {
        using var operationTiming = FileSystemManager.MeasureCurrentOperation("JointPreparation.PreparedAgrees");
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
        // hashes. The deterministic preparation replay also rechecks companion authorities.
        var expectedFingerprint = joint.ReadPreparationFingerprint();
        return expectedFingerprint is not null && expectedFingerprint == prepared.WoundPreparationFingerprint;
    }
}
