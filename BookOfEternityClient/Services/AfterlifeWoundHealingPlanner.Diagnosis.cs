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
                    m => m.RollbackBaselineFiles, paths, out _))
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
            owner._diagnosisLocalityRoots = new[] { AfterlifeWoundTreatmentAuthority.AidLocalityPaths[0], GuardianAbodeResidentState.StatePath, ShiningAbodeState.StatePath }
                .Where(currentImages.ContainsKey).ToDictionary(path => path, path => currentImages[path].Bytes is not { } bytes ? null :
                    StrictJsonAuthority.Deserialize<JsonObject>(CanonicalJsonUtf8.DecodeOneOptionalBom(bytes), new JsonSerializerOptions(), path), StringComparer.Ordinal);
            _ = owner.RequireDiagnosisTarget(candidate.Carriers.AfterlifeProfiles!,
                owner.DiagnosisLocalityRoot(AfterlifeWoundTreatmentAuthority.AidLocalityPaths[0]),
                owner.DiagnosisLocalityRoot(GuardianAbodeResidentState.StatePath), owner.DiagnosisLocalityRoot(ShiningAbodeState.StatePath));
            owner.AdmitGraphs(updates);
            owner.RequireCurrent(files, lease);
            return owner;
        }

        private string? _expectedDiagnosisPermissionFingerprint;
        private IReadOnlyDictionary<string, JsonObject?>? _diagnosisLocalityRoots;
        private JsonObject? DiagnosisLocalityRoot(string path) => _diagnosisLocalityRoots?.GetValueOrDefault(path);

        private WoundTargetChoice RequireDiagnosisTarget(JsonObject profiles, JsonObject? guardians, JsonObject? residents, JsonObject? shining)
        {
            var data = _diagnosis ?? throw new InvalidDataException("No current diagnosis owner.");
            var candidate = data.Candidate;
            var playerOwner = new WoundOwnerCoordinate(candidate.Realm, "player_soul", "player_soul", WoundCarrierCatalog.AfterlifeProfilesPath);
            var players = (profiles["profiles"] as JsonArray)?.OfType<JsonObject>().Where(p =>
                WoundCarrierCollectionAuthority.AfterlifeProfileMatches(p, playerOwner)).ToArray();
            if (players is not { Length: 1 } || !AfterlifeProfileVisibility.IsVisibleToPlayer(players[0]))
                throw new InvalidDataException("Diagnosis lost its exact currently visible player source.");
            if (!AfterlifeWoundTreatmentAuthority.HasCurrentDiagnosisRealmSource(candidate.Realm, shining))
                throw new InvalidDataException("Diagnosis requires the actual current safe Shining locality, without an incarnation package.");
            var patients = candidate.Realm == "shining_abode" ? SpiritualTreatmentParticipation.ReadShiningPatientAdmissions(shining,
                profiles, residents) : Array.Empty<SpiritualShiningPatientAdmission>();
            var targets = WoundTargetSelectionService.Build(candidate.Catalog, profiles, candidate.Realm, true,
                _parsedSourceRoots.GetValueOrDefault(AfterlifeSpiritualConflictState.StatePath) as JsonObject, patients, guardians, residents);
            return WoundTargetSelectionService.RequireCurrent(candidate.Target, targets);
        }

        private string DiagnosisPermissionFingerprint(JsonObject profiles, JsonObject soul, JsonObject? guardians, JsonObject? residents, JsonObject? shining)
        {
            var data = _diagnosis!;
            var target = RequireDiagnosisTarget(profiles, guardians, residents, shining);
            var admitted = new SpiritualDiagnosisSourceData(data.Candidate.Catalog, profiles, soul, data.Candidate.Realm, true, false)
                .Admit(data.Candidate.Admission.Wound, data.Candidate.Admission.Selection.Diagnosis!.DiagnosisPathId, target).Data
                ?? throw new InvalidDataException("Diagnosis current requirements are no longer proved.");
            // Dependency byte images have their independent physical/original fences.
            // Compare the actual semantic permission facts across the trusted client
            // projection and composed after-images; wound recovery and ordinary income
            // legitimately alter the encompassing profile/soul roots in this operation.
            var facts = admitted.RequirementBindings;
            foreach (var row in facts.OfType<JsonObject>())
                foreach (var dependency in row["sourceDependencies"]!.AsArray().OfType<JsonObject>()) dependency.Remove("fingerprint");
            return WoundAcceptedTurnFingerprintWriter.Compute(new[] { "spiritual_diagnosis.projected_permission", "1",
                target.BindingFingerprint, admitted.HealingTier.ToString(System.Globalization.CultureInfo.InvariantCulture),
                admitted.PathFingerprint, admitted.RequirementFingerprint, WoundAcceptedTurnFingerprintWriter.CanonicalJson(facts) });
        }

        private void BindDiagnosisProjectedAdmission(JsonObject? profiles, JsonObject? soul, JsonObject? shining)
        {
            if (!IsDiagnosis) return;
            if (profiles is null || soul is null) throw new InvalidDataException("Diagnosis requires the actual ordinary profile and Soul projection.");
            var fingerprint = DiagnosisPermissionFingerprint(profiles, soul,
                DiagnosisLocalityRoot(AfterlifeWoundTreatmentAuthority.AidLocalityPaths[0]),
                DiagnosisLocalityRoot(GuardianAbodeResidentState.StatePath), shining ?? DiagnosisLocalityRoot(ShiningAbodeState.StatePath));
            if (_expectedDiagnosisPermissionFingerprint is not null && _expectedDiagnosisPermissionFingerprint != fingerprint)
                throw new InvalidDataException("Diagnosis trusted ordinary projection changed within its current owner.");
            _expectedDiagnosisPermissionFingerprint = fingerprint;
        }

        internal void RequireDiagnosisComposedImages(IReadOnlyDictionary<string, JsonObject> writes)
        {
            if (!IsDiagnosis) return;
            JsonObject? Root(string path)
            {
                if (writes.TryGetValue(path, out var written)) return written;
                if (_diagnosisLocalityRoots?.TryGetValue(path, out var currentLocality) == true) return currentLocality;
                return _parsedSourceRoots.GetValueOrDefault(path) as JsonObject;
            }
            if (_published || Root(AfterlifeSpiritualConflictState.StatePath)?["activeConflict"] is not null || _expectedDiagnosisPermissionFingerprint is null || !writes.TryGetValue(WoundCarrierCatalog.AfterlifeProfilesPath, out var profiles) ||
                Root(ServiceSoulPath) is not { } soul ||
                DiagnosisPermissionFingerprint(profiles, soul, Root(AfterlifeWoundTreatmentAuthority.AidLocalityPaths[0]),
                    Root(GuardianAbodeResidentState.StatePath), Root(ShiningAbodeState.StatePath)) != _expectedDiagnosisPermissionFingerprint)
                throw new InvalidDataException("Diagnosis composed target/locality/requirements differ from the actual trusted projection.");
        }

        private async Task PublishDiagnosisLocalityNormalizationAsync(FileSystemManager files, FileSystemManager.CanonicalWriteLease lease,
            string path, string content, Func<CanonicalBeforeImage, Task> publish)
        {
            JsonObject? Root(string selectedPath, bool after) => selectedPath == path && after
                ? StrictJsonAuthority.Deserialize<JsonObject>(content, new JsonSerializerOptions(), path)
                : _sources.TryGetValue(selectedPath, out var source) && source.Bytes is { } bytes
                    ? StrictJsonAuthority.Deserialize<JsonObject>(CanonicalJsonUtf8.DecodeOneOptionalBom(bytes), new JsonSerializerOptions(), selectedPath) : null;
            var profiles = _diagnosis!.Candidate.Carriers.AfterlifeProfiles!;
            _ = RequireDiagnosisTarget(profiles, Root(AfterlifeWoundTreatmentAuthority.AidLocalityPaths[0], false),
                Root(GuardianAbodeResidentState.StatePath, false), Root(ShiningAbodeState.StatePath, false));
            _ = RequireDiagnosisTarget(profiles, Root(AfterlifeWoundTreatmentAuthority.AidLocalityPaths[0], true),
                Root(GuardianAbodeResidentState.StatePath, true), Root(ShiningAbodeState.StatePath, true));
            var expected = new CanonicalBeforeImage(true, FileSystemManager.EncodeUtf8WithPreamble(content));
            await publish(_sources[path]);
            var actual = await files.ReadFileBytesAsync(lease, path);
            if (new CanonicalBeforeImage(actual is not null, actual).Fingerprint != expected.Fingerprint)
                throw new InvalidDataException("Diagnosis locality normalizer did not publish its exact computed output.");
            var advanced = _sources.ToDictionary(p => p.Key, p => p.Value, StringComparer.Ordinal);
            advanced[path] = expected; _sources = advanced;
            var advancedRoots = _diagnosisLocalityRoots!.ToDictionary(p => p.Key, p => p.Value, StringComparer.Ordinal);
            advancedRoots[path] = StrictJsonAuthority.Deserialize<JsonObject>(content, new JsonSerializerOptions(), path);
            _diagnosisLocalityRoots = advancedRoots;
            RequireCurrent(files, lease);
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
