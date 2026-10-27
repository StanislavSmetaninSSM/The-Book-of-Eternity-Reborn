using System.Text.Json;
using System.Text.Json.Nodes;
using BookOfEternityClient.Core;
using BookOfEternityClient.Models;

namespace BookOfEternityClient.Services;

/// <summary>Owns one signed, unpublished self-treatment contribution in the existing turn pipeline.</summary>
internal static class AfterlifeWoundHealingPlanner
{
    // The qualified staging fingerprint remains its original sixteen source paths.
    // Acceptance also owns the signed no-cost resource baseline, through the same reader.
    private static readonly string[] ResourceSourcePaths =
    [
        ResourceMaterializationContract.DefinitionsPath, ResourceMaterializationContract.StatePath,
        ResourceMaterializationContract.HistoryPath, CanonicalResourceOwnerAuthorityComposer.AuthorityPath
    ];

    internal static PendingTurnSnapshotObservedReadResult ReadOriginalSource(FileSystemManager files,
        FileSystemManager.CanonicalWriteLease lease) => PendingTurnSnapshotReader.ReadObservedCurrent(files, lease,
            new PendingTurnSnapshotPathSelection(new[] { AcceptedMechanicsPlan.WoundCommandPath }.Concat(ResourceSourcePaths).ToArray(),
                AfterlifeWoundTreatmentAuthority.SourcePaths));

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
        private readonly AfterlifeWoundTreatmentAuthority.SourceData _data;
        private readonly string _controlJson;
        private readonly CanonicalBeforeImage _responseImage;
        private readonly IReadOnlyDictionary<string, JsonNode?> _parsedSourceRoots;
        private string? _composedEffectInputFingerprint;
        private readonly Dictionary<string, WoundWorkingOwnedGraph> _graphs = new(StringComparer.Ordinal);
        private readonly List<WoundExecutedBoundary> _executed = new();

        private sealed record RecoveryConsumptionProof(Continuation Owner, int Index,
            string BeforeFingerprint, string AfterFingerprint);

        private Continuation(FileSystemManager files, FileSystemManager.CanonicalWriteLease lease,
            PendingTurnSnapshotReadAuthority snapshot, PendingTurnSnapshotReader.OriginalReadEvidence original,
            IReadOnlyDictionary<string, CanonicalBeforeImage> sources, JsonObject request,
            AfterlifeWoundTreatmentAuthority.SourceData data, WoundAcceptedTurnInput input,
            IReadOnlyList<WoundMaterializationEnvelope> wounds, IReadOnlyList<SpiritualWoundStage> stages,
            ProgressionControl control, ProgressionScheduleService scheduler,
            ProgressionScheduleService.AcceptedTurnOutcomeMutation schedulerMutation, CanonicalBeforeImage responseImage,
            IReadOnlyDictionary<string, JsonNode?> parsedSourceRoots)
        {
            _files = files; _lease = lease; _snapshot = snapshot; _original = original;
            _sources = AcceptedMechanicsPlanBinding.CloneReadOnlyBeforeImages(sources);
            _request = request.DeepClone().AsObject(); _input = WoundAcceptedTurnData.CloneInput(input)!;
            _wounds = wounds.Select(w => WoundAcceptedTurnData.CloneWound(w)!).ToArray();
            _stages = stages.ToArray(); _data = data; _controlJson = JsonSerializer.Serialize(control);
            Scheduler = scheduler; SchedulerMutation = schedulerMutation;
            _responseImage = new(responseImage.Existed, responseImage.Bytes);
            _parsedSourceRoots = parsedSourceRoots.ToDictionary(p => p.Key, p => p.Value?.DeepClone(), StringComparer.Ordinal);
            Fingerprint = WoundAcceptedTurnFingerprintWriter.Compute(new[]
            {
                "book_of_eternity.spiritual_self_treatment.continuation", "1", snapshot.SnapshotToken,
                request["requestFingerprint"]!.GetValue<string>(), data.SourceFingerprint,
                JsonSerializer.Serialize(_stages), schedulerMutation.Fingerprint, _responseImage.Fingerprint
            });
        }

        internal string Fingerprint { get; }
        internal ProgressionControl Control => JsonSerializer.Deserialize<ProgressionControl>(_controlJson)!;
        internal ProgressionScheduleService Scheduler { get; }
        internal ProgressionScheduleService.AcceptedTurnOutcomeMutation SchedulerMutation { get; }
        internal WoundAcceptedTurnInput Input => WoundAcceptedTurnData.CloneInput(_input)!;
        internal JsonObject Request => _request.DeepClone().AsObject();
        internal IReadOnlyList<SpiritualWoundStage> Stages => _stages.ToArray();
        internal IReadOnlyList<WoundMaterializationEnvelope> Wounds => _wounds.Select(w => WoundAcceptedTurnData.CloneWound(w)!).ToArray();
        internal string EventRef => _input.Binding.AcceptedEvents.Single().EventRef;
        internal JsonObject PersistenceSource => new()
        {
            ["healingTier"] = _data.Source.HealingTier, ["complicationModifier"] = _data.Source.ComplicationModifier,
            ["rollMode"] = _data.Source.RollMode, ["sourceFingerprint"] = _data.SourceFingerprint,
            ["rollContributions"] = JsonSerializer.SerializeToNode(_data.Source.RollContributions),
            ["graphResponseFingerprint"] = _responseImage.Fingerprint
        };
        internal IReadOnlyDictionary<string, JsonNode?> SourceRoots => _parsedSourceRoots.ToDictionary(
            p => p.Key, p => p.Value?.DeepClone(), StringComparer.Ordinal);

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
            var response = files.ReadFileBytesSync(lease, "output/interface_updates.json");
            if (new CanonicalBeforeImage(response is not null, response).Fingerprint != _responseImage.Fingerprint)
                throw new InvalidDataException("The admitted spiritual graph/output response changed.");
            var request = files.ReadFileBytesSync(lease, AcceptedMechanicsPlan.WoundCommandPath);
            if (request is null || !request.AsSpan().SequenceEqual(_snapshot.ReadRequiredBytes(AcceptedMechanicsPlan.WoundCommandPath)))
                throw new InvalidDataException("The retained spiritual treatment request changed.");
        }

        internal bool PreparedAgrees(WoundPreparedAcceptedTurnPlan prepared) =>
            ReferenceEquals(prepared.SpiritualHealingContinuationAuthority, this) &&
            ProgramAgrees() &&
            WoundAcceptedTurnFingerprints.ComputeInput(_input) == prepared.InputFingerprint &&
            prepared.AllocatedTransitionIds.SequenceEqual(_stages.Select(s => s.TransitionId), StringComparer.Ordinal) &&
            prepared.AllocatedWoundIds.SequenceEqual(_wounds.Select(w => w.WoundId), StringComparer.Ordinal) &&
            prepared.EffectOperationBatches.Count == 0 &&
            prepared.PreparedWounds.Select(WoundMaterializationContract.SerializeCanonical)
                .SequenceEqual(_wounds.Select(WoundMaterializationContract.SerializeCanonical), StringComparer.Ordinal);

        internal WoundPreparedAcceptedTurnPlan Prepare()
        {
            // Reconstruct frozen DATA for copies as well as initial preparation.
            // Execution and publication re-prove the actual owner separately.
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
        internal void Close() => _published = true;
        internal void RequireExecutionCurrent() => RequireCurrent(_files, _lease);

        internal WoundWorkingOwnedGraph ResolveGraph(WoundMaterializationEnvelope before, SpiritualWoundStage stage)
        {
            if (stage.SeverityRank == before.Severity.Rank || stage.Kind == "heal")
                return WoundMaterializationContract.ImportOwnedGraph(before.Complications, before.Consequences);
            var key = GraphRef(before.WoundId, stage.SeverityRank);
            if (!_graphs.TryGetValue(key, out var graph))
                throw new InvalidDataException("Missing GM-authored spiritual destination graph: " + key);
            return WoundResponseInputComposer.BindSpiritualHealingGraphPredecessors(graph, before);
        }

        private string GraphRef(string woundId, int rank) => SpiritualWoundRecoveryPlanner.Coordinate(
            "spiritual_healing_graph", _request["requestFingerprint"]!.GetValue<string>(), woundId, rank);

        private void AdmitGraphs(JsonObject? interfaceUpdates)
        {
            var required = _stages.Where(s => s.Kind != "heal" && s.SeverityRank < _wounds.Single(w => w.WoundId == s.WoundId).Severity.Rank)
                .Select(s => (s.WoundId, s.SeverityRank)).Distinct().ToArray();
            var rows = interfaceUpdates?["spiritualHealingGraphs"] as JsonArray;
            if (required.Length == 0 && rows is null) return;
            if (rows is null || rows.Count != required.Length)
                throw new InvalidDataException("GM-authored spiritual destination graphs are required: " +
                    new JsonArray(required.Select(r => (JsonNode)new JsonObject
                    {
                        ["stageRef"] = GraphRef(r.WoundId, r.SeverityRank), ["severityRank"] = r.SeverityRank,
                        ["woundName"] = _wounds.Single(w => w.WoundId == r.WoundId).Display.Name,
                        ["complications"] = ComplicationRefs(_wounds.Single(w => w.WoundId == r.WoundId))
                    }).ToArray()).ToJsonString());
            foreach (var item in rows)
            {
                if (item is not JsonObject row || row.Count != 2 || !row.ContainsKey("stageRef") || !row.ContainsKey("consequenceDefinitions"))
                    throw new InvalidDataException("Each spiritual destination graph has exactly stageRef and consequenceDefinitions.");
                var reference = row["stageRef"]!.GetValue<string>();
                var matching = required.Where(r => GraphRef(r.WoundId, r.SeverityRank) == reference).ToArray();
                if (matching.Length != 1 || _graphs.ContainsKey(reference))
                    throw new InvalidDataException("Duplicate or foreign spiritual destination graph.");
                var target = matching[0];
                var wound = _wounds.Single(w => w.WoundId == target.WoundId);
                var map = wound.Complications.ToDictionary(c => ComplicationRef(c.ComplicationId), c => c.ComplicationId, StringComparer.Ordinal);
                _graphs.Add(reference, WoundResponseInputComposer.ParseSpiritualHealingGraph(
                    JsonSerializer.SerializeToElement(row["consequenceDefinitions"]), wound, target.SeverityRank, reference, map));
            }
        }

        private string ComplicationRef(string id) => SpiritualWoundRecoveryPlanner.Coordinate("spiritual_complication_ref",
            _request["requestFingerprint"]!.GetValue<string>(), id, 0);
        private JsonArray ComplicationRefs(WoundMaterializationEnvelope wound) => new(wound.Complications.Select(c =>
            (JsonNode)new JsonObject { ["complicationRef"] = ComplicationRef(c.ComplicationId), ["displayName"] = c.DisplayName }).ToArray());

        internal EffectAcceptedTurnInput ComposeEffectInput(WoundPreparedAcceptedTurnPlan prepared)
        {
            if (!PreparedAgrees(prepared)) throw new InvalidDataException("Foreign spiritual preparation.");
            var input = Input;
            var roots = SourceRoots;
            var composed = EffectAcceptedTurnInputComposer.Compose(input.Binding.SessionId, input.Binding.SnapshotToken,
                input.Binding.Turn, new JsonObject(), input.PreTurnEffectCarriers!, input.PreTurnEffectCarriers!,
                input.PreTurnEffectIdentityIndex, roots,
                currentWorldTime: EffectAcceptedTurnInputComposer.ReadCanonicalWorldTime(
                    roots[EffectAcceptedTurnInputComposer.WorldTimePath]?.ToJsonString()),
                publicationCarrierBaselines: input.PreTurnEffectCarriers, realm: input.Binding.Realm,
                preparedWoundPlan: prepared);
            var fingerprint = WoundAcceptedTurnFingerprints.ComputeEffectInput(prepared, composed);
            if (_composedEffectInputFingerprint is not null && _composedEffectInputFingerprint != fingerprint)
                throw new InvalidDataException("Spiritual effect DATA changed within its owning operation.");
            _composedEffectInputFingerprint = fingerprint;
            return composed;
        }

        internal bool EffectInputAgrees(WoundPreparedAcceptedTurnPlan prepared, EffectAcceptedTurnInput input) =>
            _composedEffectInputFingerprint is not null && PreparedAgrees(prepared) &&
            _composedEffectInputFingerprint == WoundAcceptedTurnFingerprints.ComputeEffectInput(prepared, input);

        internal object CreateRecoveryConsumptionProof(int index, WoundMaterializationEnvelope before,
            WoundMaterializationEnvelope after)
        {
            if (_published || index != _executed.Count || index >= _stages.Count)
                throw new InvalidDataException("Spiritual stage execution lost its actual cursor.");
            var expectedBefore = _executed.LastOrDefault(s => s.Before.WoundId == before.WoundId)?.After ??
                _wounds.Single(w => w.WoundId == before.WoundId);
            if (WoundMaterializationContract.SerializeCanonical(expectedBefore) != WoundMaterializationContract.SerializeCanonical(before))
                throw new InvalidDataException("Spiritual recovery does not follow its actual previous stage.");
            return new RecoveryConsumptionProof(this, index, WoundIdentityState.ComputeSemanticFingerprint(before),
                WoundIdentityState.ComputeSemanticFingerprint(after));
        }

        internal void AcceptExecuted(WoundExecutedBoundary stage)
        {
            var expected = _stages[_executed.Count];
            if (stage.OperationKey != expected.OperationKey || stage.TransitionId != expected.TransitionId ||
                stage.Kind != expected.Kind || stage.After.Severity.Rank != expected.SeverityRank ||
                stage.After.Recovery.CurrentStepProgress != expected.Progress)
                throw new InvalidDataException("Actual spiritual stage differs from the owned scalar program.");
            _executed.Add(stage.DetachedCopy());
        }

        internal static bool ProvesRecoveryConsumption(object? authority, WoundTransitionRequest request,
            WoundMaterializationEnvelope before, WoundMaterializationEnvelope after)
        {
            if (authority is not RecoveryConsumptionProof proof || proof.Owner._published ||
                request.Evidence is not WoundRecoveryEvidence { MortalContinuationAuthority: null } evidence)
                return false;
            var owner = proof.Owner;
            owner._files.EnsureCanonicalWriteLeaseActive(owner._lease);
            owner._files.VerifyCurrentSessionOperation(owner._lease);
            owner._original.RequireOwner(owner._files, owner._lease, owner._snapshot);
            var stage = owner._stages[proof.Index];
            return owner.ProgramAgrees() && proof.Index + 1 < owner._stages.Count &&
                owner._stages[proof.Index + 1] is { Kind: "heal" } next && next.WoundId == stage.WoundId &&
                stage.Kind == "recover" && stage.PreparesHeal && before.Lifecycle == "active" && after.Lifecycle == "active" &&
                before.Severity.Rank == 1 && after.Severity.Rank == 1 &&
                before.Recovery.CurrentStepThreshold == 2 && after.Recovery.CurrentStepThreshold == 2 &&
                stage.Progress == 0 && after.Recovery.CurrentStepProgress == 0 && evidence.Outcome.Heals && !evidence.Outcome.AllowsWorsening &&
                request.Kind == stage.Kind && request.OperationKey == stage.OperationKey && request.TransitionId == stage.TransitionId &&
                request.EventRef == owner.EventRef && request.Turn == owner._input.Binding.Turn &&
                before.WoundId == stage.WoundId && before.Owner == owner._data.Wound.Owner && after.Owner == before.Owner &&
                after.Recovery.LastTickKey == stage.TickKey &&
                proof.BeforeFingerprint == WoundIdentityState.ComputeSemanticFingerprint(before) &&
                proof.AfterFingerprint == WoundIdentityState.ComputeSemanticFingerprint(after) &&
                evidence.ExpectedBeforeFingerprint == proof.BeforeFingerprint && evidence.ExpectedAfterFingerprint == proof.AfterFingerprint;
        }

        private bool ProgramAgrees()
        {
            var attempt = _request["attempt"]!;
            var resolution = SpiritualHealingResolver.ResolvePrepared(_data.Source, _data.Source.RollMode,
                attempt["roll"]!["values"]!.AsArray().Select(v => v!.GetValue<int>()).ToArray()).Resolution;
            return resolution is not null && _wounds.SelectMany(w => SpiritualWoundRecoveryPlanner.Build(w,
                _data.Source.HealingTier, attempt["attemptId"]!.GetValue<string>(), attempt["cycleKey"]!.GetValue<string>(),
                w.WoundId == _data.Wound.WoundId ? resolution.Calculation : null)).SequenceEqual(_stages);
        }

        internal static async Task<Continuation> AdmitAsync(FileSystemManager files,
            FileSystemManager.CanonicalWriteLease lease, LiveTurnPendingSnapshotManifest manifest,
            PendingTurnSnapshotObservedReadResult? observedSource = null)
        {
            var paths = AfterlifeWoundTreatmentAuthority.SourcePaths;
            var observed = observedSource ?? ReadOriginalSource(files, lease);
            if (!observed.Success || observed.Snapshot is not { } snapshot || observed.Evidence is not { } evidence)
                throw new InvalidDataException("Signed spiritual treatment source is unavailable: " +
                    string.Join(";", observed.Issues.Select(i => i.Code)));
            if (snapshot.SessionId != manifest.SessionId || snapshot.RequestId != manifest.RequestId ||
                snapshot.TurnNumber != manifest.TurnNumber || snapshot.SnapshotToken != manifest.ManifestPayloadHash ||
                PendingTurnSnapshotAuthority.ComputeManifestPayloadHash(manifest, LiveTurnPreparationService.ManifestHashJsonOptions,
                    static m => m.ManifestPayloadHash, static (m, hash) => m.ManifestPayloadHash = hash) != snapshot.SnapshotToken || manifest.ProgressionControl is null ||
                !PendingTurnSnapshotAuthority.HasValidatedRollbackSnapshotCoverage(manifest, m => m.Files,
                    m => m.SnapshotFileHashes, m => m.RollbackBaselineFiles, paths.Concat(ResourceSourcePaths).ToArray(), out _))
                throw new InvalidDataException("Spiritual treatment does not identify the signed current turn.");
            evidence.RequireCurrent(files, lease, snapshot);
            var resourceImages = ResourceSourcePaths.ToDictionary(path => path,
                path => new CanonicalBeforeImage(true, snapshot.ReadRequiredBytes(path)), StringComparer.Ordinal);
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
            var wounds = data.Catalog.Occurrences.Where(o => o.Wound.Owner == data.Wound.Owner)
                .Select(o => o.Wound).OrderBy(w => w.WoundId == data.Wound.WoundId ? 0 : 1)
                .ThenBy(w => w.WoundId, StringComparer.Ordinal).ToArray();
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
            var errors = await scheduler.ValidateSpiritualTreatmentOutcomeAsync(lease, context.Progression);
            if (errors.Any(i => i.Severity == IssueSeverity.Error)) throw new InvalidDataException("Spiritual treatment requires its exact progression report.");
            var mutation = await scheduler.PrepareAcceptedTurnOutcomeMutationAsync(lease, context.Progression);
            var schedule = JsonNode.Parse(mutation.ScheduleAfterJson)!;
            if (schedule["currentChaosSeaTurnOrdinal"]!.GetValue<int>() != data.CycleOrdinal || mutation.ReportAfter.Existed)
                throw new InvalidDataException("Spiritual treatment must advance exactly its one admitted cycle.");
            var updatesBytes = await files.ReadFileBytesAsync(lease, "output/interface_updates.json");
            var updates = updatesBytes is null ? null : StrictJsonAuthority.Deserialize<JsonObject>(
                CanonicalJsonUtf8.DecodeOneOptionalBom(updatesBytes), new JsonSerializerOptions(), "spiritual treatment graph response");
            AfterlifeWoundTreatmentAuthority.MaterializeProperties(updates);
            var sourceImages = sources.ToDictionary(s => s.Path, s => s.Image, StringComparer.Ordinal);
            foreach (var resource in resourceImages) sourceImages.Add(resource.Key, resource.Value);
            var owner = new Continuation(files, lease, snapshot, evidence, sourceImages,
                request, data, input, wounds, stages, context.Progression, scheduler, mutation,
                new(updatesBytes is not null, updatesBytes), sources.ToDictionary(s => s.Path, s => (JsonNode?)s.Root, StringComparer.Ordinal));
            owner.AdmitGraphs(updates);
            owner.RequireCurrent(files, lease);
            return owner;
        }
    }
}
