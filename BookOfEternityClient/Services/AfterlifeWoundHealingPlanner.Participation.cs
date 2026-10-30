using System.Text.Json;
using System.Text.Json.Nodes;
using BookOfEternityClient.Core;

namespace BookOfEternityClient.Services;

internal static partial class AfterlifeWoundHealingPlanner
{
    internal sealed partial class Continuation
    {
        private const string ParticipationGuardiansPath = "game_state/meta/guardians.json";
        private JsonArray? _participationOutcomes;
        private IReadOnlyList<SpiritualTreatmentParticipation.Change>? _participationChanges;
        private PendingTurnSnapshotReadAuthority? _participationSnapshot;
        private PendingTurnSnapshotReader.OriginalReadEvidence? _participationOriginal;
        private IReadOnlyDictionary<string, CanonicalBeforeImage>? _participationSources;
        private IReadOnlyDictionary<string, JsonObject?>? _parsedParticipationRoots;
        internal JsonArray? ParticipationOutcomes => _participationOutcomes?.DeepClone().AsArray();

        internal async Task PrepareParticipationAsync(IReadOnlyList<SpiritualTreatmentParticipation.Change> changes,
            JsonObject acceptedProfiles)
        {
            if (changes.Count == 0) return;
            if (!IsNatural || _participationChanges is not null)
                throw new InvalidDataException("Only one actual natural owner may issue participation outcomes.");
            var detachedChanges = changes.Select(c => new SpiritualTreatmentParticipation.Change(c.Owner,
                c.Proposal.DeepClone().AsObject())).ToArray();
            RequireCurrent(_files, _lease);
            var localities = detachedChanges.Select(c => c.Owner.Realm == "shining_abode"
                ? SpiritualTreatmentParticipation.ReadLocality(c.Proposal["locality"]) : null).ToArray();
            var localityPaths = detachedChanges.SelectMany(AfterlifeWoundTreatmentAuthority.ParticipationLocalityPathsFor)
                .Distinct(StringComparer.Ordinal).ToArray();
            var requiredPaths = localityPaths.Where(p => p != GuardianAbodeResidentState.StatePath ||
                localities.Any(l => l?.Value is SpiritualAidLocality.ShiningFactionHall)).ToArray();
            var observed = PendingTurnSnapshotReader.ReadObservedCurrent(_files, _lease,
                new PendingTurnSnapshotPathSelection(requiredPaths, localityPaths.Except(requiredPaths, StringComparer.Ordinal).ToArray()));
            if (!observed.Success || observed.Snapshot is not { } snapshot || observed.Evidence is not { } evidence ||
                snapshot.SessionId != _snapshot.SessionId || snapshot.RequestId != _snapshot.RequestId ||
                snapshot.TurnNumber != _snapshot.TurnNumber || snapshot.SnapshotToken != _snapshot.SnapshotToken)
                throw new InvalidDataException("Participation requires the actual signed locality original.");
            evidence.RequireCurrent(_files, _lease, snapshot);
            var sources = new Dictionary<string, CanonicalBeforeImage>(StringComparer.Ordinal);
            var currentRoots = new Dictionary<string, JsonObject?>(StringComparer.Ordinal);
            foreach (var path in localityPaths)
            {
                var original = snapshot.CoveredLogicalPaths.Contains(path, StringComparer.Ordinal) ? snapshot.ReadRequiredBytes(path) : null;
                var current = await _files.ReadFileBytesAsync(_lease, path);
                var image = new CanonicalBeforeImage(current is not null, current);
                if (image.Fingerprint != new CanonicalBeforeImage(original is not null, original).Fingerprint)
                    throw new InvalidDataException("Participation requires unchanged signed/current Guardian locality.");
                sources.Add(path, image);
                var root = current is null ? null : StrictJsonAuthority.Deserialize<JsonObject>(
                    CanonicalJsonUtf8.DecodeOneOptionalBom(current), new JsonSerializerOptions(), path);
                currentRoots.Add(path, root);
            }
            if (_input.Binding.Realm is not ("chaos_sea" or "shining_abode"))
                throw new InvalidDataException("Participation requires the exact current safe afterlife realm.");
            var binding = new JsonObject
            {
                ["sessionId"] = _input.Binding.SessionId, ["requestId"] = _input.Binding.RequestId,
                ["snapshotToken"] = _input.Binding.SnapshotToken, ["realm"] = _input.Binding.Realm, ["turn"] = _input.Binding.Turn
            };
            var outcomes = new JsonArray();
            foreach (var change in detachedChanges)
            {
                var resident = change.Owner.OwnerKind == "resident";
                var woundOwner = change.Proposal["schemaVersion"]!.GetValue<int>() is 2 or 4
                    ? new WoundOwnerCoordinate(_input.Binding.Realm, "player_soul", "player_soul", WoundCarrierCatalog.AfterlifeProfilesPath) : change.Owner;
                var wound = _wounds.SingleOrDefault(w => w.Owner == woundOwner && w.WoundId == change.Proposal["woundId"]!.GetValue<string>())
                    ?? throw new InvalidDataException("Participation lost the exact admitted typed wound binding.");
                SpiritualAidLocality locality;
                LocalInteractionScope scope;
                if (change.Owner.Realm == "shining_abode")
                {
                    locality = SpiritualTreatmentParticipation.ReadLocality(change.Proposal["locality"]);
                    if (SpiritualTreatmentParticipation.IsShiningPatientParticipation(change.Proposal))
                        scope = SpiritualTreatmentParticipation.RequireShiningPatientAdmission(
                            currentRoots.GetValueOrDefault(ShiningAbodeState.StatePath), acceptedProfiles,
                            currentRoots.GetValueOrDefault(GuardianAbodeResidentState.StatePath), change.Owner, locality).Scope;
                    else scope = SpiritualTreatmentParticipation.RequireShiningProviderAdmission(
                        currentRoots.GetValueOrDefault(ParticipationGuardiansPath),
                        currentRoots.GetValueOrDefault(GuardianAbodeResidentState.StatePath), acceptedProfiles,
                        currentRoots.GetValueOrDefault(ShiningAbodeState.StatePath), change.Owner, locality).Scope;
                }
                else
                {
                    scope = resident && SpiritualTreatmentParticipation.IsResidentProviderOffer(change.Proposal)
                        ? SpiritualTreatmentParticipation.RequireResidentProviderScope(currentRoots[ParticipationGuardiansPath]!,
                            currentRoots[GuardianAbodeResidentState.StatePath], acceptedProfiles, change.Owner, wound)
                        : resident ? SpiritualTreatmentParticipation.RequireResidentScope(currentRoots[ParticipationGuardiansPath]!,
                            currentRoots[GuardianAbodeResidentState.StatePath], acceptedProfiles, change.Owner, wound)
                        : SpiritualTreatmentParticipation.RequireCurrentGuardianScope(currentRoots[ParticipationGuardiansPath]!,
                            currentRoots[GuardianAbodeResidentState.StatePath], change.TargetId);
                    locality = new SpiritualAidLocality(scope.CurrentGuardianId!, scope.LocationId!);
                }
                var target = (acceptedProfiles[AfterlifeEntityProfileState.ProfilesProperty] as JsonArray)?.OfType<JsonObject>()
                    .Where(p => p["actorType"]?.GetValue<string>() == change.Owner.OwnerKind && SpiritualTreatmentParticipation.ProfileId(p) == change.TargetId &&
                        AfterlifeEntityProfileState.TryNormalizeEffectRealm(p["realm"]?.GetValue<string>(), out var realm) && realm == change.Owner.Realm).ToArray();
                if (target is not { Length: 1 } || target[0]["locationId"]?.GetValue<string>() != scope.LocationId)
                    throw new InvalidDataException("Participation lost the exact admitted profile/locality binding.");
                outcomes.Add(SpiritualTreatmentParticipation.CreateOutcome(change, locality, binding,
                    _natural!.CycleOrdinal, _stages[0].TransitionId));
            }
            _participationSnapshot = snapshot; _participationOriginal = evidence; _participationSources = sources;
            _parsedParticipationRoots = currentRoots;
            _participationChanges = detachedChanges;
            _participationOutcomes = outcomes;
            BindResidentThoughts(currentRoots.GetValueOrDefault(GuardianAbodeResidentState.StatePath));
            await BindShiningPatientMemoryAsync(currentRoots.GetValueOrDefault(GuardianAbodeResidentState.StatePath));
            _patientMemory?.ApplyProfiles(acceptedProfiles);
            if (HasShiningPatientAdmission) BindProjectedServiceAdmission(acceptedProfiles);
            RequireCurrent(_files, _lease);
        }

        private void RequireParticipationCurrent()
        {
            if (_participationOriginal is null) return;
            _participationOriginal.RequireCurrent(_files, _lease, _participationSnapshot!);
            if (_participationSources is null) return; // Active aid guards these live bytes in the main source set.
            foreach (var source in _participationSources)
            {
                var bytes = _files.ReadFileBytesSync(_lease, source.Key);
                if (new CanonicalBeforeImage(bytes is not null, bytes).Fingerprint != source.Value.Fingerprint)
                    throw new InvalidDataException("Participation locality changed within its publication owner.");
            }
        }

        internal void ApplyParticipationAfterImage(JsonObject profiles)
        {
            RequireMemoryProgramsBound();
            RequireResidentThoughtFamily();
            if (_participationOutcomes is null) return;
            // Pure detached composition; current authority is checked by the
            // actual owner at preparation and immediately before common publish.
            if (_published) throw new InvalidDataException("A closed participation owner cannot compose another afterimage.");
            foreach (var outcome in _participationOutcomes.OfType<JsonObject>())
            {
                var record = outcome["record"]!.AsObject();
                var id = SpiritualTreatmentParticipation.RecordOwnerId(record);
                var target = profiles[AfterlifeEntityProfileState.ProfilesProperty]!.AsArray().OfType<JsonObject>()
                    .Single(p => WoundCarrierCollectionAuthority.AfterlifeProfileMatches(p, SpiritualTreatmentParticipation.RecordOwner(record)) && SpiritualTreatmentParticipation.ProfileId(p) == id);
                var expected = _participationChanges!.Single(c => c.Owner == SpiritualTreatmentParticipation.RecordOwner(record)).Proposal;
                if (!JsonNode.DeepEquals(target[SpiritualTreatmentParticipation.ChangeProperty], expected))
                    throw new InvalidDataException("Participation afterimage lost the actual accepted proposal.");
                target.Remove(SpiritualTreatmentParticipation.ChangeProperty);
                target[SpiritualTreatmentParticipation.RecordProperty] = record.DeepClone();
            }
        }
    }
}
