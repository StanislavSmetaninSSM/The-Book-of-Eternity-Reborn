using System.Text.Json;
using System.Text.Json.Nodes;
using BookOfEternityClient.Core;
using BookOfEternityClient.Models;

namespace BookOfEternityClient.Services;

/// <summary>Owns one signed, unpublished self-treatment contribution in the existing turn pipeline.</summary>
internal static partial class AfterlifeWoundHealingPlanner
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

    internal static PendingTurnSnapshotObservedReadResult ReadOriginalAidLocalitySource(FileSystemManager files,
        FileSystemManager.CanonicalWriteLease lease, SpiritualAidSelection? aid = null) =>
        PendingTurnSnapshotReader.ReadObservedCurrent(files, lease,
            aid?.ExplicitLocality?.Realm == "shining_abode"
                ? new PendingTurnSnapshotPathSelection(AfterlifeWoundTreatmentAuthority.AidLocalityPathsFor(aid, includeCoreState: aid.ShiningPatient is not null), Array.Empty<string>())
                : new PendingTurnSnapshotPathSelection(new[] { AfterlifeWoundTreatmentAuthority.AidLocalityPaths[0] },
                    new[] { AfterlifeWoundTreatmentAuthority.AidLocalityPaths[1] }));

    /// <summary>Reads the same signed locality dependency family selected by the parsed quote.</summary>
    /// <param name="files">The current filesystem owner.</param>
    /// <param name="lease">Its actual current lease.</param>
    /// <param name="quote">Null retains the original Elyara-only reader for existing callers.</param>
    internal static PendingTurnSnapshotObservedReadResult ReadOriginalServiceLocalitySource(FileSystemManager files,
        FileSystemManager.CanonicalWriteLease lease, AfterlifeHealingServiceQuote? quote = null) =>
        PendingTurnSnapshotReader.ReadObservedCurrent(files, lease, quote is null
            ? AfterlifeWoundTreatmentAuthority.ServiceLocalityPaths : AfterlifeWoundTreatmentAuthority.ServiceLocalityPathsFor(quote));

    internal sealed partial class Continuation
    {
        private readonly FileSystemManager _files;
        private readonly FileSystemManager.CanonicalWriteLease _lease;
        private readonly PendingTurnSnapshotReadAuthority _snapshot;
        private readonly PendingTurnSnapshotReader.OriginalReadEvidence _original;
        private IReadOnlyDictionary<string, CanonicalBeforeImage> _sources;
        private readonly JsonObject? _request;
        private readonly WoundAcceptedTurnInput _input;
        private readonly IReadOnlyList<SpiritualWoundStage> _stages;
        private readonly IReadOnlyList<WoundMaterializationEnvelope> _wounds;
        private bool _published;
        private readonly AfterlifeWoundTreatmentAuthority.SourceData? _data;
        private readonly AfterlifeWoundTreatmentAuthority.NaturalSourceData? _natural;
        private readonly string _controlJson;
        private readonly CanonicalBeforeImage _responseImage;
        private readonly IReadOnlyDictionary<string, JsonNode?> _parsedSourceRoots;
        private string? _composedEffectInputFingerprint;
        private readonly JsonObject _acceptedProgressionReport;
        internal bool HasCompleteProfileProjection { get; private set; }
        private readonly Dictionary<string, WoundWorkingOwnedGraph> _graphs = new(StringComparer.Ordinal);
        private readonly List<WoundExecutedBoundary> _executed = new();

        private sealed record RecoveryConsumptionProof(Continuation Owner, int Index,
            string BeforeFingerprint, string AfterFingerprint);

        private Continuation(FileSystemManager files, FileSystemManager.CanonicalWriteLease lease,
            PendingTurnSnapshotReadAuthority snapshot, PendingTurnSnapshotReader.OriginalReadEvidence original,
            IReadOnlyDictionary<string, CanonicalBeforeImage> sources, JsonObject? request,
            AfterlifeWoundTreatmentAuthority.SourceData? data, WoundAcceptedTurnInput input,
            IReadOnlyList<WoundMaterializationEnvelope> wounds, IReadOnlyList<SpiritualWoundStage> stages,
            ProgressionControl control, ProgressionScheduleService scheduler,
            ProgressionScheduleService.AcceptedTurnOutcomeMutation schedulerMutation, CanonicalBeforeImage responseImage,
            IReadOnlyDictionary<string, JsonNode?> parsedSourceRoots,
            AfterlifeWoundTreatmentAuthority.NaturalSourceData? natural = null)
        {
            if ((data is null) == (natural is null) || (request is null) != (natural is not null))
                throw new InvalidDataException("A spiritual cycle has exactly one active-self or ordinary-natural source.");
            _files = files; _lease = lease; _snapshot = snapshot; _original = original;
            _sources = AcceptedMechanicsPlanBinding.CloneReadOnlyBeforeImages(sources);
            _request = request?.DeepClone().AsObject(); _input = WoundAcceptedTurnData.CloneInput(input)!;
            _wounds = wounds.Select(w => WoundAcceptedTurnData.CloneWound(w)!).ToArray();
            _stages = stages.ToArray();
            _data = data?.AidWitness is { } aidWitness ? data with { AidWitness = aidWitness.DeepClone().AsObject() } : data;
            _natural = natural; _controlJson = JsonSerializer.Serialize(control);
            Scheduler = scheduler; SchedulerMutation = schedulerMutation;
            var report = StrictJsonAuthority.Deserialize<JsonObject>(CanonicalJsonUtf8.DecodeOneOptionalBom(
                schedulerMutation.ReportBefore.Bytes ?? throw new InvalidDataException("Spiritual recovery has no admitted progression report.")),
                new JsonSerializerOptions(), "admitted spiritual progression report")!;
            var reportBody = report["progressionProcessingReport"] as JsonObject ?? report;
            if (!string.Equals(reportBody["sessionId"]?.GetValue<string>(), input.Binding.SessionId, StringComparison.OrdinalIgnoreCase) ||
                !string.Equals(reportBody["requestId"]?.GetValue<string>(), input.Binding.RequestId, StringComparison.OrdinalIgnoreCase) ||
                reportBody["turnNumber"]?.GetValue<int>() != input.Binding.Turn)
                throw new InvalidDataException("Spiritual profile progression requires its actual admitted report tuple.");
            _acceptedProgressionReport = report;
            _responseImage = new(responseImage.Existed, responseImage.Bytes);
            _parsedSourceRoots = parsedSourceRoots.ToDictionary(p => p.Key, p => p.Value?.DeepClone(), StringComparer.Ordinal);
            Fingerprint = WoundAcceptedTurnFingerprintWriter.Compute(new[]
            {
                "book_of_eternity.spiritual_wound_cycle.continuation", "2", Mode, snapshot.SnapshotToken,
                ProgramReference, SourceFingerprint,
                JsonSerializer.Serialize(_stages), schedulerMutation.Fingerprint, _responseImage.Fingerprint
            });
        }

        internal string Fingerprint { get; }
        internal bool IsNatural => _natural is not null;
        internal string Mode => IsNatural ? "natural" : _data?.ServiceQuote is not null ? "service" : _data?.AidWitness is not null ? "aid" : "self";
        internal bool IsVisibleAidTarget(WoundMaterializationEnvelope wound) => Mode == "aid" &&
            _data!.Wound.WoundId == wound.WoundId && _data.Wound.Owner == wound.Owner &&
            WoundPlayerProjection.Visible(_data.Wound.Display.Visibility);
        internal string SourceFingerprint => _natural?.SourceFingerprint ?? _data!.SourceFingerprint;
        internal string ProgramReference => _request?["requestFingerprint"]!.GetValue<string>() ?? SourceFingerprint;
        internal string CycleKey => AfterlifeWoundTreatmentAuthority.CycleKey(_input.Binding.Realm,
            _natural?.CycleOrdinal ?? _request!["attempt"]!["cycleOrdinal"]!.GetValue<int>());
        internal bool IsClosed => _published;
        internal ProgressionControl Control => JsonSerializer.Deserialize<ProgressionControl>(_controlJson)!;
        internal ProgressionScheduleService Scheduler { get; }
        internal ProgressionScheduleService.AcceptedTurnOutcomeMutation SchedulerMutation { get; }
        internal WoundAcceptedTurnInput Input => WoundAcceptedTurnData.CloneInput(_input)!;
        internal JsonObject? Request => _request?.DeepClone().AsObject();
        internal IReadOnlyList<SpiritualWoundStage> Stages => _stages.ToArray();
        internal IReadOnlyList<WoundMaterializationEnvelope> Wounds => _wounds.Select(w => WoundAcceptedTurnData.CloneWound(w)!).ToArray();
        internal string EventRef => _input.Binding.AcceptedEvents.Single().EventRef;
        internal JsonArray RecoveryTiers => new((_natural?.Members ?? _data!.NaturalMembers)
            .GroupBy(m => m.Wound.Owner).Select(g => (JsonNode)new JsonObject
            {
                ["owner"] = SpiritualHealingRequestProjection.OwnerJson(g.Key), ["healingTier"] = g.First().HealingTier
            }).ToArray());
        internal JsonObject PersistenceSource => IsNatural ? new JsonObject
        {
            ["sourceFingerprint"] = SourceFingerprint, ["graphResponseFingerprint"] = _responseImage.Fingerprint,
            ["ownerTiers"] = RecoveryTiers, ["projection"] = SpiritualHealingRequestProjection.CreateNatural(_natural!)
        } : new()
        {
            ["healingTier"] = _data!.Source.HealingTier, ["complicationModifier"] = _data.Source.ComplicationModifier,
            ["rollMode"] = _data.Source.RollMode, ["sourceFingerprint"] = SourceFingerprint,
            ["rollContributions"] = JsonSerializer.SerializeToNode(_data.Source.RollContributions),
            ["graphResponseFingerprint"] = _responseImage.Fingerprint, ["ownerTiers"] = RecoveryTiers
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
            RequireParticipationCurrent();
            RequireConditionalMemoryCurrent();
            foreach (var pair in _sources)
            {
                // The GM authors the request-bound progression outcome after the original snapshot.
                if (!IsNatural && pair.Key == ProgressionScheduleService.ReportPath) continue;
                var current = files.ReadFileBytesSync(lease, pair.Key);
                if (new CanonicalBeforeImage(current is not null, current).Fingerprint != pair.Value.Fingerprint)
                    throw new InvalidDataException("Spiritual treatment source changed: " + pair.Key);
            }
            var response = files.ReadFileBytesSync(lease, "output/interface_updates.json");
            if (new CanonicalBeforeImage(response is not null, response).Fingerprint != _responseImage.Fingerprint)
                throw new InvalidDataException("The admitted spiritual graph/output response changed.");
            var request = files.ReadFileBytesSync(lease, AcceptedMechanicsPlan.WoundCommandPath);
            if (IsNatural ? request is not null : request is null ||
                !request.AsSpan().SequenceEqual(_snapshot.ReadRequiredBytes(AcceptedMechanicsPlan.WoundCommandPath)))
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

        private string GraphRef(string woundId, int rank) => SpiritualHealingRequestProjection.GraphRef(ProgramReference, woundId, rank);

        private void AdmitGraphs(JsonObject? interfaceUpdates)
        {
            if ((_data?.ServiceQuote is not null || _data?.NegotiatedWalletInvariant is not null) && interfaceUpdates?["metaStateUpdates"] is JsonObject meta && meta.ContainsKey("inkFeatherChanges"))
                throw new InvalidDataException("Healing owns its exact original wallet/payment; competing GM currency changes must be removed.");
            AdmitResidentThoughtResponse(interfaceUpdates);
            if (_data?.AidWitness is not null || _data?.ServiceQuote?.Provider is { Realm: "shining_abode", OwnerKind: "resident" })
                BindResidentThoughts(_parsedSourceRoots.GetValueOrDefault(GuardianAbodeResidentState.StatePath) as JsonObject);
            var required = SpiritualHealingRequestProjection.RequiredGraphs(_wounds, _stages).ToArray();
            var rows = interfaceUpdates?["spiritualHealingGraphs"] as JsonArray;
            if (required.Length == 0 && rows is null) return;
            if (rows is null || rows.Count != required.Length)
                throw new InvalidDataException("GM-authored spiritual destination graphs are required: " +
                    new JsonArray(required.Select(r => (JsonNode)new JsonObject
                    {
                        ["stageRef"] = GraphRef(r.WoundId, r.Rank), ["severityRank"] = r.Rank,
                        ["woundName"] = _wounds.Single(w => w.WoundId == r.WoundId).Display.Name,
                        ["complications"] = ComplicationRefs(_wounds.Single(w => w.WoundId == r.WoundId))
                    }).ToArray()).ToJsonString());
            foreach (var item in rows)
            {
                if (item is not JsonObject row || row.Count != 2 || !row.ContainsKey("stageRef") || !row.ContainsKey("consequenceDefinitions"))
                    throw new InvalidDataException("Each spiritual destination graph has exactly stageRef and consequenceDefinitions.");
                var reference = row["stageRef"]!.GetValue<string>();
                var matching = required.Where(r => GraphRef(r.WoundId, r.Rank) == reference).ToArray();
                if (matching.Length != 1 || _graphs.ContainsKey(reference))
                    throw new InvalidDataException("Duplicate or foreign spiritual destination graph.");
                var target = matching[0];
                var wound = _wounds.Single(w => w.WoundId == target.WoundId);
                var map = wound.Complications.ToDictionary(c => ComplicationRef(c.ComplicationId), c => c.ComplicationId, StringComparer.Ordinal);
                _graphs.Add(reference, WoundResponseInputComposer.ParseSpiritualHealingGraph(
                    JsonSerializer.SerializeToElement(row["consequenceDefinitions"]), wound, target.Rank, reference, map));
            }
        }

        private string ComplicationRef(string id) => SpiritualHealingRequestProjection.ComplicationRef(ProgramReference, id);
        private JsonArray ComplicationRefs(WoundMaterializationEnvelope wound) => new(wound.Complications.Select(c =>
            (JsonNode)new JsonObject { ["complicationRef"] = ComplicationRef(c.ComplicationId), ["displayName"] = c.DisplayName }).ToArray());

        internal EffectAcceptedTurnInput ComposeEffectInput(WoundPreparedAcceptedTurnPlan prepared)
        {
            if (!PreparedAgrees(prepared)) throw new InvalidDataException("Foreign spiritual preparation.");
            var input = Input;
            var roots = SourceRoots.ToDictionary(p => p.Key, p => p.Value, StringComparer.Ordinal);
            var projectedProfiles = ProjectAcceptedProfiles(input.PreTurnEffectCarriers!.AfterlifeProfiles,
                input.PreTurnEffectCarriers.AfterlifeProfiles, roots["game_state/meta/soul_state.json"] as JsonObject,
                roots[ShiningAbodeState.StatePath] as JsonObject);
            var currentCarriers = input.PreTurnEffectCarriers with { AfterlifeProfiles = projectedProfiles };
            roots[AfterlifeEntityProfileState.StatePath] = projectedProfiles;
            var composed = EffectAcceptedTurnInputComposer.Compose(input.Binding.SessionId, input.Binding.SnapshotToken,
                input.Binding.Turn, new JsonObject(), input.PreTurnEffectCarriers!, currentCarriers,
                input.PreTurnEffectIdentityIndex, roots,
                currentWorldTime: EffectAcceptedTurnInputComposer.ReadCanonicalWorldTime(
                    roots[EffectAcceptedTurnInputComposer.WorldTimePath]?.ToJsonString()),
                publicationCarrierBaselines: input.PreTurnEffectCarriers, realm: input.Binding.Realm,
                preparedWoundPlan: prepared);
            var fingerprint = WoundAcceptedTurnFingerprints.ComputeEffectInput(prepared, composed);
            if (_composedEffectInputFingerprint is not null && _composedEffectInputFingerprint != fingerprint)
                throw new InvalidDataException("Spiritual effect DATA changed within its owning operation.");
            _composedEffectInputFingerprint = fingerprint;
            BindProjectedServiceAdmission(projectedProfiles);
            return composed;
        }

        internal JsonObject ProjectAcceptedProfiles(JsonObject? current, JsonObject? previous,
            JsonObject? soul, JsonObject? shining)
        {
            RequireExecutionCurrent();
            var result = AfterlifeEntityProfileState.ProjectAcceptedRoot(current, previous,
                _acceptedProgressionReport, soul, shining);
            _patientMemory?.ApplyProfiles(result);
            HasCompleteProfileProjection = true;
            return result;
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

        internal IReadOnlyList<WoundMaterializationEnvelope> ReadCompletedSourceWounds()
        {
            RequireExecutionCurrent();
            if (_executed.Count != _stages.Count)
                throw new InvalidDataException("Spiritual source projection requires every actual executed stage.");
            return _executed.GroupBy(s => s.After.WoundId).Select(g => WoundAcceptedTurnData.CloneWound(g.Last().After)!).ToArray();
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
                before.WoundId == stage.WoundId && before.Owner == owner._wounds.Single(w => w.WoundId == stage.WoundId).Owner && after.Owner == before.Owner &&
                after.Recovery.LastTickKey == stage.TickKey &&
                proof.BeforeFingerprint == WoundIdentityState.ComputeSemanticFingerprint(before) &&
                proof.AfterFingerprint == WoundIdentityState.ComputeSemanticFingerprint(after) &&
                evidence.ExpectedBeforeFingerprint == proof.BeforeFingerprint && evidence.ExpectedAfterFingerprint == proof.AfterFingerprint;
        }

        private bool ProgramAgrees()
        {
            if (_natural is not null)
                return SpiritualHealingRequestProjection.BuildNaturalProgram(_natural).Stages.SequenceEqual(_stages);
            var attempt = _request!["attempt"]!;
            var resolution = SpiritualHealingResolver.ResolvePrepared(_data!.Source, _data.Source.RollMode,
                attempt["roll"]!["values"]!.AsArray().Select(v => v!.GetValue<int>()).ToArray()).Resolution;
            return resolution is not null && SpiritualHealingRequestProjection.BuildProgram(_data, _request, resolution).Stages.SequenceEqual(_stages);
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
            var sources = ReadSignedSources(snapshot);
            var request = StrictJsonAuthority.Deserialize<JsonObject>(CanonicalJsonUtf8.DecodeOneOptionalBom(
                snapshot.ReadRequiredBytes(AcceptedMechanicsPlan.WoundCommandPath)), new JsonSerializerOptions(), "signed spiritual treatment")!;
            AfterlifeWoundTreatmentAuthority.MaterializeProperties(request);
            var attempt = request["attempt"] as JsonObject ?? throw new InvalidDataException("Spiritual attempt is missing.");
            var selection = SpiritualSelfTreatmentRequestCodec.ReadActiveSelection(request);
            PendingTurnSnapshotObservedReadResult? aidLocality = null;
            if (selection.Aid is not null || selection.Service is not null)
            {
                var localityPaths = selection.Service is not null ? AfterlifeWoundTreatmentAuthority.ServiceLocalityPathsFor(selection.Service.Quote)
                    : AfterlifeWoundTreatmentAuthority.AidLocalityPathsFor(selection.Aid!);
                if (!PendingTurnSnapshotAuthority.HasValidatedRollbackSnapshotCoverage(manifest, m => m.Files,
                        m => m.SnapshotFileHashes, m => m.RollbackBaselineFiles, localityPaths, out _))
                    throw new InvalidDataException("Aid lacks its signed locality baseline coverage.");
                aidLocality = selection.Service is not null ? ReadOriginalServiceLocalitySource(files, lease, selection.Service.Quote) : ReadOriginalAidLocalitySource(files, lease, selection.Aid);
                if (!aidLocality.Success || aidLocality.Snapshot is not { } local || aidLocality.Evidence is not { } localEvidence ||
                    local.SessionId != snapshot.SessionId || local.RequestId != snapshot.RequestId ||
                    local.TurnNumber != snapshot.TurnNumber || local.SnapshotToken != snapshot.SnapshotToken)
                    throw new InvalidDataException("Aid locality is not the same actual signed original.");
                localEvidence.RequireCurrent(files, lease, local);
                sources.AddRange(ReadSignedSources(local, localityPaths));
            }
            var context = new SpiritualSelfTreatmentStageContext(snapshot.SessionId, snapshot.RequestId, snapshot.TurnNumber,
                JsonSerializer.Deserialize<ProgressionControl>(JsonSerializer.Serialize(manifest.ProgressionControl))!);
            var admitted = selection.Service is not null
                ? AfterlifeWoundTreatmentAuthority.PrepareServiceSourceData(context, selection, sources)
                : selection.Aid is null
                    ? AfterlifeWoundTreatmentAuthority.PrepareSourceData(context, selection, sources)
                    : AfterlifeWoundTreatmentAuthority.PrepareAidSourceData(context, selection, sources,
                        request["participation"]!.AsObject());
            if (admitted.Data is not { } data || !SpiritualSelfTreatmentRequestCodec.IsExactActive(request, context, selection, data))
                throw new InvalidDataException("Signed spiritual treatment admission failed: " + admitted.IssueCode);
            var rolls = attempt["roll"]!["values"]!.AsArray().Select(v => v!.GetValue<int>()).ToArray();
            var resolution = SpiritualHealingResolver.ResolvePrepared(data.Source, data.Source.RollMode, rolls).Resolution
                ?? throw new InvalidDataException("The signed healing roll cannot be resolved.");
            var roots = sources.ToDictionary(s => s.Path, s => s.Root, StringComparer.Ordinal);
            var (wounds, stages) = SpiritualHealingRequestProjection.BuildProgram(data, request, resolution);
            var input = CreateCycleInput(snapshot, data.Carriers, roots);
            var (scheduler, mutation) = await PrepareCycleSchedulerAsync(files, lease, context.Progression, data.CycleOrdinal);
            var updatesBytes = await files.ReadFileBytesAsync(lease, "output/interface_updates.json");
            var updates = updatesBytes is null ? null : StrictJsonAuthority.Deserialize<JsonObject>(
                CanonicalJsonUtf8.DecodeOneOptionalBom(updatesBytes), new JsonSerializerOptions(), "spiritual treatment graph response");
            AfterlifeWoundTreatmentAuthority.MaterializeProperties(updates);
            var sourceImages = sources.ToDictionary(s => s.Path, s => s.Image, StringComparer.Ordinal);
            foreach (var resource in resourceImages) sourceImages.Add(resource.Key, resource.Value);
            var owner = new Continuation(files, lease, snapshot, evidence, sourceImages,
                request, data, input, wounds, stages, context.Progression, scheduler, mutation,
                new(updatesBytes is not null, updatesBytes), sources.ToDictionary(s => s.Path, s => (JsonNode?)s.Root, StringComparer.Ordinal));
            if (aidLocality is not null)
            {
                owner._participationSnapshot = aidLocality.Snapshot;
                owner._participationOriginal = aidLocality.Evidence;
                // Fresh live locality is already part of _sources; this field
                // closes only its additional actual-original evidence.
            }
            owner.AdmitGraphs(updates);
            await owner.BindShiningPatientMemoryAsync(roots.GetValueOrDefault(GuardianAbodeResidentState.StatePath));
            owner.RequireMemoryProgramsBound();
            owner.RequireCurrent(files, lease);
            return owner;
        }

        internal static List<AfterlifeWoundTreatmentAuthority.SourceFile> ReadSignedSources(PendingTurnSnapshotReadAuthority snapshot,
            IEnumerable<string>? selectedPaths = null)
        {
            var sources = new List<AfterlifeWoundTreatmentAuthority.SourceFile>();
            foreach (var path in selectedPaths ?? AfterlifeWoundTreatmentAuthority.SourcePaths)
            {
                var bytes = snapshot.CoveredLogicalPaths.Contains(path, StringComparer.Ordinal) ? snapshot.ReadRequiredBytes(path) : null;
                var root = bytes is null ? null : JsonNode.Parse(CanonicalJsonUtf8.DecodeOneOptionalBom(bytes)) as JsonObject
                    ?? throw new InvalidDataException("Spiritual source must be a strict object: " + path);
                AfterlifeWoundTreatmentAuthority.MaterializeProperties(root);
                sources.Add(new(path, new(bytes is not null, bytes), root));
            }
            return sources;
        }

        private static WoundAcceptedTurnInput CreateCycleInput(PendingTurnSnapshotReadAuthority snapshot,
            WoundCarrierCatalogInput carriers, IReadOnlyDictionary<string, JsonObject?> roots)
        {
            var accepted = WoundAcceptedEventAuthorityComposer.ComposeDefaultAcceptedTurn(snapshot.SessionId,
                snapshot.RequestId, snapshot.SnapshotToken, snapshot.TurnNumber);
            if (!accepted.Success) throw new InvalidDataException("Spiritual recovery has no exact accepted event.");
            var binding = new WoundAcceptedTurnBinding(snapshot.SessionId, snapshot.RequestId, snapshot.SnapshotToken,
                snapshot.Realm, snapshot.TurnNumber, accepted.Events, WoundAcceptedEventSetFingerprint.Compute(accepted.Events));
            return new(binding, Array.Empty<WoundOpportunityAuthority>(), Array.Empty<WoundAcceptedTransitionDraft>(), carriers,
                roots[WoundIdentityState.StatePath]!, roots[WoundHistoryState.HistoryPath]!,
                new(roots[EffectCarrierCatalog.PlayerPath], roots[EffectCarrierCatalog.NpcPath], carriers.EnemyCombatants,
                    carriers.AllyCombatants, carriers.AfterlifeProfiles, roots[EffectCarrierCatalog.SpiritualConflictPath]),
                roots[EffectIdentityState.StatePath]);
        }

        private static async Task<(ProgressionScheduleService Scheduler, ProgressionScheduleService.AcceptedTurnOutcomeMutation Mutation)>
            PrepareCycleSchedulerAsync(FileSystemManager files, FileSystemManager.CanonicalWriteLease lease,
                ProgressionControl control, int ordinal)
        {
            var scheduler = new ProgressionScheduleService(files, Microsoft.Extensions.Logging.Abstractions.NullLogger<ProgressionScheduleService>.Instance);
            var errors = await scheduler.ValidateSpiritualTreatmentOutcomeAsync(lease, control);
            if (errors.Any(i => i.Severity == IssueSeverity.Error)) throw new InvalidDataException("Spiritual recovery requires its exact progression report.");
            var mutation = await scheduler.PrepareAcceptedTurnOutcomeMutationAsync(lease, control);
            if (JsonNode.Parse(mutation.ScheduleAfterJson)!["currentChaosSeaTurnOrdinal"]!.GetValue<int>() != ordinal || mutation.ReportAfter.Existed)
                throw new InvalidDataException("Spiritual recovery must advance exactly its one admitted cycle.");
            return (scheduler, mutation);
        }
    }
}
