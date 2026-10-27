using System.Text.Json;
using System.Text.Json.Nodes;
using BookOfEternityClient.Core;
using BookOfEternityClient.Models;

namespace BookOfEternityClient.Services;

/// <summary>Owns one signed, unpublished self-treatment contribution in the existing turn pipeline.</summary>
internal static class AfterlifeWoundHealingPlanner
{
    internal sealed class Continuation
    {
        private readonly FileSystemManager _files;
        private readonly FileSystemManager.CanonicalWriteLease _lease;
        private readonly PendingTurnSnapshotReadAuthority _snapshot;
        private readonly PendingTurnSnapshotReader.OriginalReadEvidence _original;
        private readonly IReadOnlyDictionary<string, CanonicalBeforeImage> _sources;
        private readonly JsonObject _request;
        private readonly WoundAcceptedTurnInput _input;
        private readonly IReadOnlyList<SpiritualWoundStage> _stages;
        private readonly IReadOnlyList<WoundMaterializationEnvelope> _wounds;
        private bool _published;

        private Continuation(FileSystemManager files, FileSystemManager.CanonicalWriteLease lease,
            PendingTurnSnapshotReadAuthority snapshot, PendingTurnSnapshotReader.OriginalReadEvidence original,
            IReadOnlyDictionary<string, CanonicalBeforeImage> sources, JsonObject request,
            AfterlifeWoundTreatmentAuthority.SourceData data, WoundAcceptedTurnInput input,
            IReadOnlyList<WoundMaterializationEnvelope> wounds, IReadOnlyList<SpiritualWoundStage> stages,
            ProgressionControl control, ProgressionScheduleService scheduler,
            ProgressionScheduleService.AcceptedTurnOutcomeMutation schedulerMutation)
        {
            _files = files; _lease = lease; _snapshot = snapshot; _original = original;
            _sources = AcceptedMechanicsPlanBinding.CloneReadOnlyBeforeImages(sources);
            _request = request.DeepClone().AsObject(); _input = WoundAcceptedTurnData.CloneInput(input)!;
            _wounds = wounds.Select(w => WoundAcceptedTurnData.CloneWound(w)!).ToArray();
            _stages = stages.ToArray(); Data = data; Control = control;
            Scheduler = scheduler; SchedulerMutation = schedulerMutation;
            Fingerprint = WoundAcceptedTurnFingerprintWriter.Compute(new[]
            {
                "book_of_eternity.spiritual_self_treatment.continuation", "1", snapshot.SnapshotToken,
                request["requestFingerprint"]!.GetValue<string>(), data.SourceFingerprint,
                JsonSerializer.Serialize(_stages), schedulerMutation.Fingerprint
            });
        }

        internal string Fingerprint { get; }
        internal AfterlifeWoundTreatmentAuthority.SourceData Data { get; }
        internal ProgressionControl Control { get; }
        internal ProgressionScheduleService Scheduler { get; }
        internal ProgressionScheduleService.AcceptedTurnOutcomeMutation SchedulerMutation { get; }
        internal WoundAcceptedTurnInput Input => WoundAcceptedTurnData.CloneInput(_input)!;
        internal JsonObject Request => _request.DeepClone().AsObject();
        internal IReadOnlyList<SpiritualWoundStage> Stages => _stages.ToArray();
        internal IReadOnlyList<WoundMaterializationEnvelope> Wounds => _wounds.Select(w => WoundAcceptedTurnData.CloneWound(w)!).ToArray();
        internal string EventRef => _input.Binding.AcceptedEvents.Single().EventRef;
        internal IReadOnlyDictionary<string, JsonNode?> SourceRoots => _sources.ToDictionary(p => p.Key,
            p => p.Value.Existed ? JsonNode.Parse(CanonicalJsonUtf8.DecodeOneOptionalBom(p.Value.Bytes!)) : null,
            StringComparer.Ordinal);

        internal void RequireCurrent(FileSystemManager files, FileSystemManager.CanonicalWriteLease lease)
        {
            if (_published || !ReferenceEquals(files, _files) || !ReferenceEquals(lease, _lease))
                throw new InvalidDataException("Spiritual treatment requires its live original owner and lease.");
            files.EnsureCanonicalWriteLeaseActive(lease);
            files.VerifyCurrentSessionOperation(lease);
            _original.RequireCurrent(files, lease, _snapshot);
            foreach (var pair in _sources)
            {
                // The GM authors the request-bound progression outcome after the original snapshot.
                if (pair.Key == ProgressionScheduleService.ReportPath) continue;
                var current = files.ReadFileBytesSync(lease, pair.Key);
                if (new CanonicalBeforeImage(current is not null, current).Fingerprint != pair.Value.Fingerprint)
                    throw new InvalidDataException("Spiritual treatment source changed: " + pair.Key);
            }
            var request = files.ReadFileBytesSync(lease, AcceptedMechanicsPlan.WoundCommandPath);
            if (request is null || !request.AsSpan().SequenceEqual(_snapshot.ReadRequiredBytes(AcceptedMechanicsPlan.WoundCommandPath)))
                throw new InvalidDataException("The retained spiritual treatment request changed.");
        }

        internal bool PreparedAgrees(WoundPreparedAcceptedTurnPlan prepared) =>
            ReferenceEquals(prepared.SpiritualHealingContinuationAuthority, this) &&
            WoundAcceptedTurnFingerprints.ComputeInput(_input) == prepared.InputFingerprint &&
            prepared.AllocatedTransitionIds.SequenceEqual(_stages.Select(s => s.TransitionId), StringComparer.Ordinal) &&
            prepared.AllocatedWoundIds.SequenceEqual(_wounds.Select(w => w.WoundId), StringComparer.Ordinal) &&
            prepared.EffectOperationBatches.Count == 0 &&
            prepared.PreparedWounds.Select(WoundMaterializationContract.SerializeCanonical)
                .SequenceEqual(_wounds.Select(WoundMaterializationContract.SerializeCanonical), StringComparer.Ordinal);

        internal WoundPreparedAcceptedTurnPlan Prepare()
        {
            RequireCurrent(_files, _lease);
            var input = Input;
            var fingerprint = WoundAcceptedTurnFingerprints.ComputeInput(input);
            var baseline = WoundAcceptedTurnPlannerCore.CreateBaselineAuthority(fingerprint, input);
            WoundPreparedAcceptedTurnPlan Build(string seal) => new(input.Binding,
                WoundAcceptedTurnFingerprints.ComputeBinding(input.Binding), fingerprint, seal,
                _wounds.Select(w => w.WoundId).ToArray(), _stages.Select(s => s.TransitionId).ToArray(),
                _wounds, Array.Empty<WoundEffectOperationBatch>(), baseline, spiritualHealingContinuationAuthority: this);
            var provisional = Build(string.Empty);
            return Build(WoundAcceptedTurnFingerprints.ComputePreparation(provisional));
        }

        internal void MarkPublished() => _published = true;

        internal static async Task<Continuation> AdmitAsync(FileSystemManager files,
            FileSystemManager.CanonicalWriteLease lease, LiveTurnPendingSnapshotManifest manifest)
        {
            var paths = AfterlifeWoundTreatmentAuthority.SourcePaths;
            var observed = PendingTurnSnapshotReader.ReadObservedCurrent(files, lease,
                new PendingTurnSnapshotPathSelection(new[] { AcceptedMechanicsPlan.WoundCommandPath }, paths));
            if (!observed.Success || observed.Snapshot is not { } snapshot || observed.Evidence is not { } evidence)
                throw new InvalidDataException("Signed spiritual treatment source is unavailable: " +
                    string.Join(";", observed.Issues.Select(i => i.Code)));
            if (snapshot.SessionId != manifest.SessionId || snapshot.RequestId != manifest.RequestId ||
                snapshot.TurnNumber != manifest.TurnNumber || snapshot.SnapshotToken != manifest.ManifestPayloadHash ||
                PendingTurnSnapshotAuthority.ComputeManifestPayloadHash(manifest) != snapshot.SnapshotToken || manifest.ProgressionControl is null ||
                !PendingTurnSnapshotAuthority.HasValidatedRollbackSnapshotCoverage(manifest, m => m.Files,
                    m => m.SnapshotFileHashes, m => m.RollbackBaselineFiles, paths, out _))
                throw new InvalidDataException("Spiritual treatment does not identify the signed current turn.");
            var sources = new List<AfterlifeWoundTreatmentAuthority.SourceFile>();
            foreach (var path in paths)
            {
                var bytes = snapshot.CoveredLogicalPaths.Contains(path, StringComparer.Ordinal) ? snapshot.ReadRequiredBytes(path) : null;
                var root = bytes is null ? null : JsonNode.Parse(CanonicalJsonUtf8.DecodeOneOptionalBom(bytes)) as JsonObject
                    ?? throw new InvalidDataException("Spiritual source must be a strict object: " + path);
                AfterlifeWoundTreatmentAuthority.MaterializeProperties(root);
                sources.Add(new(path, new(bytes is not null, bytes), root));
            }
            var request = StrictJsonAuthority.Deserialize<JsonObject>(CanonicalJsonUtf8.DecodeOneOptionalBom(
                snapshot.ReadRequiredBytes(AcceptedMechanicsPlan.WoundCommandPath)), new JsonSerializerOptions(), "signed spiritual treatment")!;
            AfterlifeWoundTreatmentAuthority.MaterializeProperties(request);
            var attempt = request["attempt"] as JsonObject ?? throw new InvalidDataException("Spiritual attempt is missing.");
            var selection = new SpiritualSelfTreatmentSelection(attempt["woundId"]!.GetValue<string>(), attempt["woundFingerprint"]!.GetValue<string>());
            var context = new SpiritualSelfTreatmentStageContext(snapshot.SessionId, snapshot.RequestId, snapshot.TurnNumber,
                JsonSerializer.Deserialize<ProgressionControl>(JsonSerializer.Serialize(manifest.ProgressionControl))!);
            var admitted = AfterlifeWoundTreatmentAuthority.PrepareSourceData(context, selection, sources);
            if (admitted.Data is not { } data || !SpiritualSelfTreatmentRequestCodec.IsExact(request, context,
                    selection, snapshot.Realm, data.CycleOrdinal, data.SourceFingerprint, data.Source))
                throw new InvalidDataException("Signed spiritual treatment admission failed: " + admitted.IssueCode);
            var rolls = attempt["roll"]!["values"]!.AsArray().Select(v => v!.GetValue<int>()).ToArray();
            var resolution = SpiritualHealingResolver.ResolvePrepared(data.Source, data.Source.RollMode, rolls).Resolution
                ?? throw new InvalidDataException("The signed healing roll cannot be resolved.");
            var acceptedEvents = WoundAcceptedEventAuthorityComposer.ComposeDefaultAcceptedTurn(snapshot.SessionId,
                snapshot.RequestId, snapshot.SnapshotToken, snapshot.TurnNumber);
            if (!acceptedEvents.Success) throw new InvalidDataException("Spiritual treatment has no exact accepted event.");
            var binding = new WoundAcceptedTurnBinding(snapshot.SessionId, snapshot.RequestId, snapshot.SnapshotToken,
                snapshot.Realm, snapshot.TurnNumber, acceptedEvents.Events, WoundAcceptedEventSetFingerprint.Compute(acceptedEvents.Events));
            var roots = sources.ToDictionary(s => s.Path, s => s.Root, StringComparer.Ordinal);
            var wounds = WoundCarrierCatalog.Build(data.Carriers).Occurrences.Where(o => o.Wound.Owner == data.Wound.Owner)
                .Select(o => o.Wound).OrderBy(w => w.WoundId, StringComparer.Ordinal).ToArray();
            var cycleKey = AfterlifeWoundTreatmentAuthority.CycleKey(data.Realm, data.CycleOrdinal);
            var stages = wounds.SelectMany(w => SpiritualWoundRecoveryPlanner.Build(w, data.Source.HealingTier,
                attempt["attemptId"]!.GetValue<string>(), cycleKey, w.WoundId == data.Wound.WoundId ? resolution.Calculation : null)).ToArray();
            var input = new WoundAcceptedTurnInput(binding, Array.Empty<WoundOpportunityAuthority>(),
                Array.Empty<WoundAcceptedTransitionDraft>(), data.Carriers,
                roots[WoundIdentityState.StatePath]!, roots[WoundHistoryState.HistoryPath]!,
                new(roots[EffectCarrierCatalog.PlayerPath], roots[EffectCarrierCatalog.NpcPath], data.Carriers.EnemyCombatants,
                    data.Carriers.AllyCombatants, data.Carriers.AfterlifeProfiles, roots[EffectCarrierCatalog.SpiritualConflictPath]),
                roots[EffectIdentityState.StatePath]);
            var scheduler = new ProgressionScheduleService(files, Microsoft.Extensions.Logging.Abstractions.NullLogger<ProgressionScheduleService>.Instance);
            var errors = await scheduler.ValidateAcceptedTurnOutcomeAsync(context.Progression);
            if (errors.Any(i => i.Severity == IssueSeverity.Error)) throw new InvalidDataException("Spiritual treatment requires its exact progression report.");
            var mutation = await scheduler.PrepareAcceptedTurnOutcomeMutationAsync(lease, context.Progression);
            var schedule = JsonNode.Parse(mutation.ScheduleAfterJson)!;
            if (schedule["currentChaosSeaTurnOrdinal"]!.GetValue<int>() != data.CycleOrdinal || mutation.ReportAfter.Existed)
                throw new InvalidDataException("Spiritual treatment must advance exactly its one admitted cycle.");
            var owner = new Continuation(files, lease, snapshot, evidence, sources.ToDictionary(s => s.Path, s => s.Image,
                StringComparer.Ordinal), request, data, input, wounds, stages, context.Progression, scheduler, mutation);
            owner.RequireCurrent(files, lease);
            return owner;
        }
    }
}
