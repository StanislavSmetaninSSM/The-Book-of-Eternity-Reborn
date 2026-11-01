using System.Text.Json;
using System.Text.Json.Nodes;
using BookOfEternityClient.Core;
using BookOfEternityClient.Models;

namespace BookOfEternityClient.Services;

internal static partial class AfterlifeWoundHealingPlanner
{
    internal static PendingTurnSnapshotObservedReadResult ReadOriginalDiagnosisSource(FileSystemManager files,
        FileSystemManager.CanonicalWriteLease lease, string realm)
    {
        var baseline = AfterlifeWoundTreatmentAuthority.DiagnosisSourcePaths(realm);
        var observed = PendingTurnSnapshotReader.ReadObservedCurrent(files, lease,
            new PendingTurnSnapshotPathSelection([AcceptedMechanicsPlan.WoundCommandPath], baseline));
        if (!observed.Success || observed.Snapshot is not { } snapshot) return observed;
        var request = StrictJsonAuthority.Deserialize<JsonObject>(CanonicalJsonUtf8.DecodeOneOptionalBom(
            snapshot.ReadRequiredBytes(AcceptedMechanicsPlan.WoundCommandPath)), new JsonSerializerOptions(), "diagnosis original selector")!;
        var profiles = StrictJsonAuthority.Deserialize<JsonObject>(CanonicalJsonUtf8.DecodeOneOptionalBom(
            snapshot.ReadRequiredBytes(WoundCarrierCatalog.AfterlifeProfilesPath)), new JsonSerializerOptions(), "diagnosis original profiles");
        var paths = AfterlifeWoundTreatmentAuthority.DiagnosisSourcePaths(realm,
            AfterlifeWoundTreatmentAuthority.DiagnosisRequirementKinds(profiles, SpiritualDiagnosisRequestCodec.ReadSelection(request)));
        if (paths.SequenceEqual(baseline, StringComparer.Ordinal)) return observed;
        observed.Evidence!.RequireCurrent(files, lease, snapshot);
        return PendingTurnSnapshotReader.ReadObservedCurrent(files, lease,
            PendingTurnSnapshotPathSelection.CreateWithObservedOptionalPaths([AcceptedMechanicsPlan.WoundCommandPath], paths));
    }

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

        /// <summary>Reproves live diagnosis ownership and exact manifest binding before reading its prepared-command DATA role.</summary>
        /// <param name="files">The actual filesystem retained by this owner.</param>
        /// <param name="lease">The same active canonical lease retained by this owner.</param>
        /// <param name="sessionId">The current validated manifest's session.</param>
        /// <param name="requestId">The current validated manifest's request.</param>
        /// <param name="turn">The current validated manifest's turn.</param>
        /// <param name="manifestHash">The current manifest's declared hash.</param>
        /// <param name="computedManifestHash">The complete hash recomputed from the actual current manifest, including control.</param>
        /// <returns>True only for this current diagnosis's exact signed-only command; baseline-present inputs keep normal coverage.</returns>
        internal bool HasCurrentDiagnosisPreparedCommandRole(FileSystemManager files, FileSystemManager.CanonicalWriteLease lease,
            string sessionId, string requestId, int turn, string manifestHash, string computedManifestHash)
        {
            if (!IsDiagnosis) throw new InvalidDataException("Only an actual diagnosis owner may prove its prepared-command role.");
            RequireCurrent(files, lease);
            var binding = _input.Binding;
            if (sessionId != _snapshot.SessionId || requestId != _snapshot.RequestId || turn != _snapshot.TurnNumber ||
                sessionId != binding.SessionId || requestId != binding.RequestId || turn != binding.Turn ||
                binding.SnapshotToken != _snapshot.SnapshotToken || manifestHash != _snapshot.SnapshotToken ||
                computedManifestHash != _snapshot.SnapshotToken)
                throw new InvalidDataException("Diagnosis dynamic rollback role differs from its complete current manifest binding.");
            return _original.HasExactPreparedWoundCommandRole();
        }

        internal static async Task<Continuation> AdmitDiagnosisAsync(FileSystemManager files,
            FileSystemManager.CanonicalWriteLease lease, LiveTurnPendingSnapshotManifest manifest,
            PendingTurnSnapshotObservedReadResult? observedSource = null)
        {
            var realm = manifest.ProgressionControl?.CurrentRealm;
            if (!AfterlifeEntityProfileState.TryNormalizeEffectRealm(realm, out var normalized) || normalized is not ("chaos_sea" or "shining_abode"))
                throw new InvalidDataException("Diagnosis requires its actual spiritual control realm.");
            var observed = observedSource ?? ReadOriginalDiagnosisSource(files, lease, normalized);
            var selectedSnapshot = observed.Snapshot ?? throw new InvalidDataException("Missing diagnosis original source.");
            var selectedRequest = StrictJsonAuthority.Deserialize<JsonObject>(CanonicalJsonUtf8.DecodeOneOptionalBom(
                selectedSnapshot.ReadRequiredBytes(AcceptedMechanicsPlan.WoundCommandPath)), new JsonSerializerOptions(), "diagnosis original selector")!;
            var selectedProfiles = StrictJsonAuthority.Deserialize<JsonObject>(CanonicalJsonUtf8.DecodeOneOptionalBom(
                selectedSnapshot.ReadRequiredBytes(WoundCarrierCatalog.AfterlifeProfilesPath)), new JsonSerializerOptions(), "diagnosis original profiles");
            var paths = AfterlifeWoundTreatmentAuthority.DiagnosisSourcePaths(normalized,
                AfterlifeWoundTreatmentAuthority.DiagnosisRequirementKinds(selectedProfiles, SpiritualDiagnosisRequestCodec.ReadSelection(selectedRequest)));
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
            owner._diagnosisLocalityRoots = currentImages.Keys.ToDictionary(path => path, path => currentImages[path].Bytes is not { } bytes ? null :
                    StrictJsonAuthority.Deserialize<JsonObject>(CanonicalJsonUtf8.DecodeOneOptionalBom(bytes), new JsonSerializerOptions(), path), StringComparer.Ordinal);
            _ = owner.RequireDiagnosisTarget(candidate.Carriers.AfterlifeProfiles!,
                owner.DiagnosisLocalityRoot(AfterlifeWoundTreatmentAuthority.AidLocalityPaths[0]),
                owner.DiagnosisLocalityRoot(GuardianAbodeResidentState.StatePath), owner.DiagnosisLocalityRoot(ShiningAbodeState.StatePath));
            owner.AdmitGraphs(updates);
            owner.RequireCurrent(files, lease);
            return owner;
        }

        private string? _expectedDiagnosisPermissionFingerprint;
        private string? _expectedDiagnosisStableRequirements;
        private int? _expectedDiagnosisHealingTier;
        private AcceptedMechanicsPlan? _validatedDiagnosisPlan;
        private string? _validatedDiagnosisPlanFingerprint;
        private IReadOnlyDictionary<string, JsonObject?>? _validatedDiagnosisImages;
        private IReadOnlyDictionary<string, JsonObject?>? _validatedDiagnosisPublicationBaselines;
        private IReadOnlySet<string>? _validatedDiagnosisWritePaths;
        private IReadOnlySet<string>? _validatedDiagnosisDeletePaths;
        internal int DiagnosisCommonImageBuilds { get; private set; }
        internal bool HasCanonicalDiagnosisRequirements => _diagnosis?.Candidate.Admission.RequirementBindings.OfType<JsonObject>()
            .Any(row => row["kind"] is JsonValue kind && kind.TryGetValue<string>(out var text) && SpiritualDiagnosisCanonicalSources.IsCanonicalKind(text)) == true;
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
            var roots = (_diagnosisLocalityRoots ?? throw new InvalidDataException("Missing current diagnosis source DATA."))
                .ToDictionary(pair => pair.Key, pair => pair.Value, StringComparer.Ordinal);
            roots[WoundCarrierCatalog.AfterlifeProfilesPath] = profiles; roots[ServiceSoulPath] = soul;
            roots[AfterlifeWoundTreatmentAuthority.AidLocalityPaths[0]] = guardians;
            roots[GuardianAbodeResidentState.StatePath] = residents; roots[ShiningAbodeState.StatePath] = shining;
            var admitted = new SpiritualDiagnosisSourceData(data.Candidate.Catalog, profiles, soul, data.Candidate.Realm, true, false, roots)
                .Admit(data.Candidate.Admission.Wound, data.Candidate.Admission.Selection.Diagnosis!.DiagnosisPathId, target).Data
                ?? throw new InvalidDataException("Diagnosis current requirements are no longer proved.");
            // Dependency byte images have their independent physical/original fences.
            // Compare the actual semantic permission facts across the trusted client
            // projection and composed after-images; wound recovery and ordinary income
            // legitimately alter the encompassing profile/soul roots in this operation.
            var facts = admitted.RequirementBindings;
            _expectedDiagnosisStableRequirements ??= StableDiagnosisFingerprint(new JsonArray(facts.OfType<JsonObject>()
                .Where(row => !SpiritualDiagnosisCanonicalSources.IsCanonicalKind(row["kind"]!.GetValue<string>()))
                .Select(row => (JsonNode)row.DeepClone()).ToArray()));
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
            var player = profiles["profiles"]!.AsArray().OfType<JsonObject>().Single(row =>
                WoundCarrierCollectionAuthority.AfterlifeProfileMatches(row, new(_diagnosis!.Candidate.Realm, "player_soul", "player_soul", WoundCarrierCatalog.AfterlifeProfilesPath)));
            if (!AfterlifeEntityProfileState.TryReadRequiredWoundArtTier(player, "spiritual_healing", out _, out var tier))
                throw new InvalidDataException("Missing diagnosis projected Healing source.");
            _expectedDiagnosisHealingTier = tier;
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
            if (HasCanonicalDiagnosisRequirements)
            {
                if (_published || _expectedDiagnosisPermissionFingerprint is null ||
                    !writes.TryGetValue(WoundCarrierCatalog.AfterlifeProfilesPath, out var projected) || Root(ServiceSoulPath) is not { } projectedSoul ||
                    Root(AfterlifeSpiritualConflictState.StatePath)?["activeConflict"] is not null)
                    throw new InvalidDataException("Diagnosis lost its pre-execution admission or complete actual target projection.");
                _ = RequireDiagnosisTarget(projected, Root(AfterlifeWoundTreatmentAuthority.AidLocalityPaths[0]),
                    Root(GuardianAbodeResidentState.StatePath), Root(ShiningAbodeState.StatePath));
                var player = projected["profiles"]!.AsArray().OfType<JsonObject>().Single(row =>
                    WoundCarrierCollectionAuthority.AfterlifeProfileMatches(row, new(_diagnosis!.Candidate.Realm, "player_soul", "player_soul", WoundCarrierCatalog.AfterlifeProfilesPath)));
                if (!AfterlifeEntityProfileState.TryReadRequiredWoundArtTier(player, "spiritual_healing", out _, out var tier) ||
                    tier != _expectedDiagnosisHealingTier || projectedSoul["afterlifeCombatProfile"]?["artTiers"]?["spiritual_healing"]?.GetValue<int>() != tier ||
                    !AfterlifeEntityProfileState.TryNormalizeEffectRealm(projectedSoul["currentRealm"]?.GetValue<string>(), out var realm) || realm != _diagnosis!.Candidate.Realm)
                    throw new InvalidDataException("Diagnosis composed source tier differs from its admitted ordinary source.");
                var stable = SpiritualDiagnosisSourceData.ReadStableRequirementBindings(_diagnosis!.Candidate.Catalog, projected, projectedSoul,
                    _diagnosis.Candidate.Realm, _diagnosis.Candidate.Admission.Wound, _diagnosis.Candidate.Admission.Selection.Diagnosis!.DiagnosisPathId);
                if (StableDiagnosisFingerprint(stable) != _expectedDiagnosisStableRequirements)
                    throw new InvalidDataException("Diagnosis composed capability/provider/stabilization requirements changed.");
                // Quantities and qualifying wound effects may lawfully change during
                // the natural suffix. Exact common outputs are checked after validation.
                return;
            }
            if (_published || Root(AfterlifeSpiritualConflictState.StatePath)?["activeConflict"] is not null || _expectedDiagnosisPermissionFingerprint is null || !writes.TryGetValue(WoundCarrierCatalog.AfterlifeProfilesPath, out var profiles) ||
                Root(ServiceSoulPath) is not { } soul ||
                DiagnosisPermissionFingerprint(profiles, soul, Root(AfterlifeWoundTreatmentAuthority.AidLocalityPaths[0]),
                    Root(GuardianAbodeResidentState.StatePath), Root(ShiningAbodeState.StatePath)) != _expectedDiagnosisPermissionFingerprint)
                throw new InvalidDataException("Diagnosis composed target/locality/requirements differ from the actual trusted projection.");
        }

        private static string StableDiagnosisFingerprint(JsonArray rows)
        {
            foreach (var row in rows.OfType<JsonObject>())
                foreach (var dependency in row["sourceDependencies"]!.AsArray().OfType<JsonObject>()) dependency.Remove("fingerprint");
            return WoundAcceptedTurnFingerprintWriter.Compute(new[] { "spiritual_diagnosis.stable_requirements", "1",
                WoundAcceptedTurnFingerprintWriter.CanonicalJson(rows) });
        }

        internal IReadOnlyList<ValidationIssue> BindValidatedDiagnosisPlan(AcceptedMechanicsPlan plan, object capability)
        {
            if (!AcceptedTurnAuthorityRegistry.IsDiagnosisValidatedCommonCapability(capability))
                throw new InvalidDataException("Only the existing validated common callback may bind a diagnosis plan.");
            RequireExecutionCurrent();
            if (!HasCanonicalDiagnosisRequirements || !ReferenceEquals(plan.SpiritualHealingContinuation, this) ||
                _executed.Count != _stages.Count || _expectedDiagnosisPermissionFingerprint is null ||
                plan.AwaitsPendingResolution || plan.PreparedPlanFingerprint != AcceptedMechanicsPlanFingerprints.ComputePrepared(plan))
                throw new InvalidDataException("Diagnosis requires its exact completed, validated common plan and source owner.");
            if (_validatedDiagnosisPlan is not null)
            {
                if (!ReferenceEquals(_validatedDiagnosisPlan, plan) || _validatedDiagnosisPlanFingerprint != plan.PreparedPlanFingerprint)
                    throw new InvalidDataException("The live diagnosis common binding changed.");
                return []; // Same immutable DATA after all fresh owner/cursor/fingerprint checks.
            }
            DiagnosisCommonImageBuilds++;
            var images = (_diagnosisLocalityRoots ?? throw new InvalidDataException("Missing current source images."))
                .ToDictionary(pair => pair.Key, pair => pair.Value?.DeepClone().AsObject(), StringComparer.Ordinal);
            images[ResourceMaterializationContract.DefinitionsPath] = plan.DefinitionAfterImage;
            images[ResourceMaterializationContract.StatePath] = plan.StateAfterImage;
            images[ResourceMaterializationContract.HistoryPath] = plan.HistoryAfterImage;
            var definitions = ResourceDefinitionCatalog.ParseCanonical(plan.DefinitionAfterImage.ToJsonString(), false).Catalog
                ?? throw new InvalidDataException("Invalid validated common definitions.");
            var resources = ResourceStateContract.ParseCanonical(plan.StateAfterImage.ToJsonString(), definitions, false).Ledger
                ?? throw new InvalidDataException("Invalid validated common state.");
            var history = ResourceHistoryState.ParseCanonical(plan.HistoryAfterImage.ToJsonString(), definitions, false).History
                ?? throw new InvalidDataException("Invalid validated common History.");
            images[CanonicalResourceOwnerAuthorityComposer.AuthorityPath] = JsonNode.Parse(
                CanonicalResourceOwnerAuthorityComposer.CreateCanonicalAuthorityJson(plan.OwnerAuthority, resources, history))!.AsObject();
            images[EffectIdentityState.StatePath] = plan.EffectIdentityAfterImage;
            foreach (var pair in plan.OwnerCompanionAfterImages) images[pair.Key] = pair.Value;
            foreach (var pair in plan.EffectCarrierAfterImages) images[pair.Key] = pair.Value;
            foreach (var pair in plan.WoundCarrierAfterImages) images[pair.Key] = pair.Value;
            images[WoundIdentityState.StatePath] = plan.WoundIdentityAfterImage ?? throw new InvalidDataException("Missing common wound identity.");
            images[WoundHistoryState.HistoryPath] = plan.WoundHistoryAfterImage ?? throw new InvalidDataException("Missing common wound History.");
            var writePaths = new HashSet<string>(plan.OwnerCompanionAfterImages.Keys.Concat(plan.EffectCarrierAfterImages.Keys)
                .Concat(plan.WoundCarrierAfterImages.Keys).Concat(new[] { ResourceMaterializationContract.DefinitionsPath,
                    ResourceMaterializationContract.StatePath, ResourceMaterializationContract.HistoryPath,
                    CanonicalResourceOwnerAuthorityComposer.AuthorityPath, EffectIdentityState.StatePath,
                    WoundIdentityState.StatePath, WoundHistoryState.HistoryPath }), StringComparer.Ordinal);
            var currentSources = _sources.ToDictionary(pair => pair.Key, pair => pair.Value, StringComparer.Ordinal);
            foreach (var group in plan.OwnerTransitions.GroupBy(transition => transition.Path, StringComparer.Ordinal))
            {
                if (!writePaths.Add(group.Key)) throw new InvalidDataException("Diagnosis common plan has competing whole-root/typed owner outputs.");
                if (!currentSources.TryGetValue(group.Key, out var baseline))
                {
                    if (!plan.BeforeImages.ContainsKey(group.Key)) throw new InvalidDataException("Typed owner output lacks its validated source binding.");
                    var bytes = _files.ReadFileBytesSync(_lease, group.Key);
                    baseline = new(bytes is not null, bytes); currentSources[group.Key] = baseline;
                }
                if (baseline.Bytes is not { } original) throw new InvalidDataException("Typed owner output lost its actual current root.");
                var root = StrictJsonAuthority.Deserialize<JsonObject>(CanonicalJsonUtf8.DecodeOneOptionalBom(original), new JsonSerializerOptions(), group.Key)!;
                foreach (var transition in group.OrderBy(transition => transition.OwnerRef, StringComparer.Ordinal))
                    CanonicalStateNormalizer.ApplyOwnerTransition(root, transition);
                images[group.Key] = root;
            }
            foreach (var pair in plan.PendingAfterImages) images[pair.Key] = pair.Value;
            writePaths.UnionWith(plan.PendingAfterImages.Where(pair => pair.Value is not null).Select(pair => pair.Key));
            var deletePaths = plan.ConsumedPaths.Concat(plan.PendingAfterImages.Where(pair => pair.Value is null).Select(pair => pair.Key)).ToHashSet(StringComparer.Ordinal);
            if (writePaths.Overlaps(deletePaths)) throw new InvalidDataException("Diagnosis common plan has competing write/delete outputs.");
            foreach (var path in plan.ConsumedPaths) images[path] = null;
            var beforeImages = plan.BeforeImages;
            var baselines = new Dictionary<string, JsonObject?>(StringComparer.Ordinal);
            foreach (var path in writePaths)
            {
                var before = currentSources.GetValueOrDefault(path) ?? beforeImages.GetValueOrDefault(path)
                    ?? throw new InvalidDataException("Diagnosis common output has no actual bound baseline: " + path);
                baselines[path] = before.Bytes is not { } bytes ? null :
                    StrictJsonAuthority.Deserialize<JsonObject>(CanonicalJsonUtf8.DecodeOneOptionalBom(bytes), new JsonSerializerOptions(), path);
            }
            _sources = currentSources;
            RequireExecutionCurrent();
            _validatedDiagnosisPlan = plan; _validatedDiagnosisPlanFingerprint = plan.PreparedPlanFingerprint;
            _validatedDiagnosisImages = images;
            _validatedDiagnosisPublicationBaselines = baselines;
            _validatedDiagnosisWritePaths = writePaths; _validatedDiagnosisDeletePaths = deletePaths;
            return [];
        }

        internal JsonObject DiagnosisResourceOwnerAfterImage(AcceptedMechanicsPlan plan)
        {
            RequireValidatedDiagnosisPlan(plan);
            return _validatedDiagnosisImages![CanonicalResourceOwnerAuthorityComposer.AuthorityPath]!.DeepClone().AsObject();
        }

        private void RequireValidatedDiagnosisPlan(AcceptedMechanicsPlan plan)
        {
            RequireExecutionCurrent();
            if (!ReferenceEquals(plan, _validatedDiagnosisPlan) || !ReferenceEquals(plan.SpiritualHealingContinuation, this) ||
                _validatedDiagnosisImages is null || _executed.Count != _stages.Count ||
                _validatedDiagnosisPlanFingerprint != plan.PreparedPlanFingerprint || plan.PreparedPlanFingerprint != AcceptedMechanicsPlanFingerprints.ComputePrepared(plan))
                throw new InvalidDataException("Diagnosis publication requires its same validated/taken common plan.");
        }

        private void RequireCanonicalDiagnosisPublication(AcceptedMechanicsPlan? plan,
            IReadOnlyDictionary<string, JsonObject> writes, IReadOnlyCollection<string>? deletes)
        {
            if (!HasCanonicalDiagnosisRequirements) return;
            if (plan is null || deletes is null) throw new InvalidDataException("Diagnosis publication lost its actual validated plan/deletion set.");
            RequireValidatedDiagnosisPlan(plan);
            var deleted = deletes.ToHashSet(StringComparer.Ordinal);
            if (deleted.Count != deletes.Count || !_validatedDiagnosisDeletePaths!.SetEquals(deleted) ||
                writes.Keys.Any(path => !_validatedDiagnosisWritePaths!.Contains(path) || deleted.Contains(path)))
                throw new InvalidDataException("Diagnosis publication has an extra write/delete or omits a required consumed path.");
            foreach (var path in _validatedDiagnosisWritePaths!)
            {
                var expected = _validatedDiagnosisImages![path];
                if (writes.TryGetValue(path, out var proposed))
                {
                    if (expected is null || !JsonNode.DeepEquals(proposed, expected))
                        throw new InvalidDataException("Diagnosis publication output differs from its validated common image: " + path);
                }
                else if (!JsonNode.DeepEquals(expected, _validatedDiagnosisPublicationBaselines![path]))
                    throw new InvalidDataException("Diagnosis omits a changed common output: " + path);
            }
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
