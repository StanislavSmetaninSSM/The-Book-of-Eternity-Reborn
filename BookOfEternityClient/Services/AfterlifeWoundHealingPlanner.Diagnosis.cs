using System.Text.Json;
using System.Text.Json.Nodes;
using BookOfEternityClient.Core;
using BookOfEternityClient.Models;

namespace BookOfEternityClient.Services;

internal static partial class AfterlifeWoundHealingPlanner
{
    internal static PendingTurnSnapshotObservedReadResult ReadOriginalDiagnosisSource(FileSystemManager files,
        FileSystemManager.CanonicalWriteLease lease, string realm) => PendingTurnSnapshotReader.ReadObservedCurrent(files, lease,
            new PendingTurnSnapshotPathSelection([AcceptedMechanicsPlan.WoundCommandPath],
                AfterlifeWoundTreatmentAuthority.DiagnosisSourcePaths(realm).ToArray()));

    internal sealed partial class Continuation
    {
        internal AfterlifeWoundTreatmentAuthority.DiagnosisSourceData? DiagnosisSource => _diagnosis;
        internal string GraphResponseFingerprint => _responseImage.Fingerprint;
        internal JsonObject? DiagnosisProjection => _diagnosis?.Program.Projection;
        internal JsonObject DiagnosisNaturalPersistenceSource => _diagnosis is { } data ? new()
        {
            ["sourceFingerprint"] = data.Program.NaturalProgramReference,
            ["graphResponseFingerprint"] = _responseImage.Fingerprint,
            ["ownerTiers"] = RecoveryTiers,
            ["projection"] = data.Program.Projection["naturalRecovery"]!.DeepClone()
        } : throw new InvalidDataException("A diagnosis natural suffix requires its live diagnosis owner.");

        internal static async Task<Continuation> AdmitDiagnosisAsync(FileSystemManager files,
            FileSystemManager.CanonicalWriteLease lease, LiveTurnPendingSnapshotManifest manifest,
            PendingTurnSnapshotObservedReadResult? observedSource = null)
        {
            var realm = manifest.ProgressionControl?.CurrentRealm;
            if (!AfterlifeEntityProfileState.TryNormalizeEffectRealm(realm, out var normalized) || normalized is not ("chaos_sea" or "shining_abode"))
                throw new InvalidDataException("Diagnosis requires its actual spiritual control realm.");
            var paths = AfterlifeWoundTreatmentAuthority.DiagnosisSourcePaths(normalized);
            var observed = observedSource ?? ReadOriginalDiagnosisSource(files, lease, normalized);
            if (!observed.Success || observed.Snapshot is not { } snapshot || observed.Evidence is not { } evidence ||
                snapshot.SessionId != manifest.SessionId || snapshot.RequestId != manifest.RequestId || snapshot.TurnNumber != manifest.TurnNumber ||
                snapshot.SnapshotToken != manifest.ManifestPayloadHash || manifest.ProgressionControl is null ||
                PendingTurnSnapshotAuthority.ComputeManifestPayloadHash(manifest, LiveTurnPreparationService.ManifestHashJsonOptions,
                    static m => m.ManifestPayloadHash, static (m, hash) => m.ManifestPayloadHash = hash) != snapshot.SnapshotToken ||
                !PendingTurnSnapshotAuthority.HasValidatedRollbackSnapshotCoverage(manifest, m => m.Files, m => m.SnapshotFileHashes,
                    m => m.RollbackBaselineFiles, paths.Append(AcceptedMechanicsPlan.WoundCommandPath).ToArray(), out _))
                throw new InvalidDataException("Diagnosis requires its exact signed turn and complete rollback baseline.");
            evidence.RequireCurrent(files, lease, snapshot);
            var sources = ReadSignedSources(snapshot, paths);
            var request = StrictJsonAuthority.Deserialize<JsonObject>(CanonicalJsonUtf8.DecodeOneOptionalBom(
                snapshot.ReadRequiredBytes(AcceptedMechanicsPlan.WoundCommandPath)), new JsonSerializerOptions(), "signed spiritual diagnosis")!;
            var selection = SpiritualDiagnosisRequestCodec.ReadSelection(request);
            var context = new SpiritualSelfTreatmentStageContext(snapshot.SessionId, snapshot.RequestId, snapshot.TurnNumber,
                JsonSerializer.Deserialize<ProgressionControl>(JsonSerializer.Serialize(manifest.ProgressionControl))!);
            var candidate = AfterlifeWoundTreatmentAuthority.PrepareDiagnosisCandidate(selection, sources, context.Turn);
            var data = AfterlifeWoundTreatmentAuthority.BindPreparedDiagnosisSource(context, candidate, sources, request);
            if (!SpiritualDiagnosisRequestCodec.IsExact(request, context, selection, candidate.Admission))
                throw new InvalidDataException("Signed diagnosis request differs from its actual current admission.");
            var currentImages = new Dictionary<string, CanonicalBeforeImage>(StringComparer.Ordinal);
            foreach (var source in sources)
            {
                var current = await files.ReadFileBytesAsync(lease, source.Path);
                var image = new CanonicalBeforeImage(current is not null, current);
                // Ordinary input may author lawful siblings. The common composer
                // rechecks protected wounds, effects and target/requirement tuples.
                if (source.Path is WoundIdentityState.StatePath or WoundHistoryState.HistoryPath or EffectIdentityState.StatePath or
                    ProgressionScheduleService.SchedulePath or EffectAcceptedTurnInputComposer.WorldTimePath or
                    AfterlifeSpiritualConflictState.StatePath)
                    if (image.Fingerprint != source.Image.Fingerprint)
                        throw new InvalidDataException("Diagnosis original authority changed: " + source.Path);
                currentImages.Add(source.Path, image);
            }
            var roots = sources.ToDictionary(s => s.Path, s => s.Root, StringComparer.Ordinal);
            var input = CreateCycleInput(snapshot, candidate.Carriers, roots);
            var (scheduler, mutation) = await PrepareCycleSchedulerAsync(files, lease, context.Progression, data.Natural.CycleOrdinal);
            var response = await files.ReadFileBytesAsync(lease, "output/interface_updates.json");
            var updates = response is null ? null : StrictJsonAuthority.Deserialize<JsonObject>(
                CanonicalJsonUtf8.DecodeOneOptionalBom(response), new JsonSerializerOptions(), "diagnosis graph response");
            AfterlifeWoundTreatmentAuthority.MaterializeProperties(updates);
            var owner = new Continuation(files, lease, snapshot, evidence, currentImages, request, null, input,
                candidate.Members.Select(m => m.Wound).ToArray(), data.Program.Stages, context.Progression, scheduler, mutation,
                new(response is not null, response), sources.ToDictionary(s => s.Path, s => (JsonNode?)s.Root, StringComparer.Ordinal), diagnosis: data);
            owner.AdmitGraphs(updates);
            owner.RequireCurrent(files, lease);
            return owner;
        }

        internal WoundExecutedBoundary ExecuteDiagnosis(WoundMaterializationEnvelope before, SpiritualWoundStage stage)
        {
            RequireExecutionCurrent();
            var data = _diagnosis ?? throw new InvalidDataException("Only a diagnosis owner may execute its knowledge step.");
            if (_executed.Count != 0 || stage != _stages[0] || stage.Kind != "diagnose" ||
                WoundMaterializationContract.SerializeCanonical(before) != WoundMaterializationContract.SerializeCanonical(data.Program.KnowledgeBefore))
                throw new InvalidDataException("Diagnosis must precede the complete ordinary natural cohort.");
            var request = SpiritualDiagnosisTransitionPlanner.CreateKnowledgeRequest(data.Context, data.Request,
                data.Candidate.Admission, data.Program.KnowledgeAfter, stage.TransitionId, stage.OperationKey, EventRef);
            var reduction = WoundTransitionReducer.Reduce(request);
            if (!reduction.IsValid || reduction.ProposedAfter is not { } after)
                throw new InvalidDataException("The sealed diagnosis reducer refused its complete knowledge-only step.");
            return new(before, after, stage.OperationKey, EventRef, "", [], [], reduction.Intents);
        }
    }
}
