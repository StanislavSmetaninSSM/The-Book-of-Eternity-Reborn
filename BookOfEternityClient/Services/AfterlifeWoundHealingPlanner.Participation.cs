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
        internal JsonArray? ParticipationOutcomes => _participationOutcomes?.DeepClone().AsArray();

        internal async Task PrepareParticipationAsync(IReadOnlyList<SpiritualTreatmentParticipation.Change> changes,
            JsonObject acceptedProfiles)
        {
            if (changes.Count == 0) return;
            if (!IsNatural || _participationChanges is not null)
                throw new InvalidDataException("Only one actual natural owner may issue participation outcomes.");
            RequireCurrent(_files, _lease);
            var observed = PendingTurnSnapshotReader.ReadObservedCurrent(_files, _lease,
                new PendingTurnSnapshotPathSelection(new[] { ParticipationGuardiansPath }, new[] { GuardianAbodeResidentState.StatePath }));
            if (!observed.Success || observed.Snapshot is not { } snapshot || observed.Evidence is not { } evidence ||
                snapshot.SessionId != _snapshot.SessionId || snapshot.RequestId != _snapshot.RequestId ||
                snapshot.TurnNumber != _snapshot.TurnNumber || snapshot.SnapshotToken != _snapshot.SnapshotToken)
                throw new InvalidDataException("Participation requires the actual signed locality original.");
            evidence.RequireCurrent(_files, _lease, snapshot);
            var sources = new Dictionary<string, CanonicalBeforeImage>(StringComparer.Ordinal);
            var currentRoots = new Dictionary<string, JsonObject?>(StringComparer.Ordinal);
            foreach (var path in new[] { ParticipationGuardiansPath, GuardianAbodeResidentState.StatePath })
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
            var scope = LocalInteractionScopeService.ResolveChaosSeaScope(currentRoots[ParticipationGuardiansPath],
                currentRoots[GuardianAbodeResidentState.StatePath], Array.Empty<LocalInteractionAuthoritySnapshot>());
            if (!scope.IsResolved || _input.Binding.Realm != "chaos_sea")
                throw new InvalidDataException("Participation requires the exact current safe Chaos Abode.");
            var binding = new JsonObject
            {
                ["sessionId"] = _input.Binding.SessionId, ["requestId"] = _input.Binding.RequestId,
                ["snapshotToken"] = _input.Binding.SnapshotToken, ["realm"] = _input.Binding.Realm, ["turn"] = _input.Binding.Turn
            };
            var outcomes = new JsonArray();
            foreach (var change in changes)
            {
                var guardians = currentRoots[ParticipationGuardiansPath]!;
                var exact = (guardians["guardians"] as JsonArray)?.OfType<JsonObject>().Where(g =>
                    string.Equals(g["guardianId"]?.GetValue<string>(), change.TargetId, StringComparison.OrdinalIgnoreCase)).ToArray();
                if (scope.CurrentGuardianId != change.TargetId || exact is not { Length: 1 } ||
                    exact[0]["guardianId"]?.GetValue<string>() != change.TargetId ||
                    guardians["activeGuardian"]?["guardianId"]?.GetValue<string>() != change.TargetId ||
                    guardians["chaosSeaNavigation"]?["currentGuardianId"]?.GetValue<string>() != change.TargetId ||
                    guardians["chaosSeaNavigation"]?["currentAbodeId"]?.GetValue<string>() != scope.LocationId ||
                    exact[0]["abode"]?["abodeId"]?.GetValue<string>() != scope.LocationId)
                    throw new InvalidDataException("Participation cannot use an alias, ambiguous Guardian or foreign Abode.");
                var target = (acceptedProfiles[AfterlifeEntityProfileState.ProfilesProperty] as JsonArray)?.OfType<JsonObject>()
                    .Where(p => p["actorType"]?.GetValue<string>() == "guardian" && SpiritualTreatmentParticipation.ProfileId(p) == change.TargetId &&
                        AfterlifeEntityProfileState.TryNormalizeEffectRealm(p["realm"]?.GetValue<string>(), out var realm) && realm == "chaos_sea").ToArray();
                if (target is not { Length: 1 } || target[0]["locationId"]?.GetValue<string>() != scope.LocationId ||
                    !_wounds.Any(w => w.Owner.OwnerKind == "guardian" && w.Owner.OwnerId == change.TargetId &&
                        w.WoundId == change.Proposal["woundId"]!.GetValue<string>()))
                    throw new InvalidDataException("Participation lost the exact admitted Guardian/wound binding.");
                outcomes.Add(SpiritualTreatmentParticipation.CreateOutcome(change, scope.LocationId, binding,
                    _natural!.CycleOrdinal, _stages[0].TransitionId));
            }
            _participationSnapshot = snapshot; _participationOriginal = evidence; _participationSources = sources;
            _participationChanges = changes; _participationOutcomes = outcomes;
            RequireCurrent(_files, _lease);
        }

        private void RequireParticipationCurrent()
        {
            if (_participationOriginal is null) return;
            _participationOriginal.RequireCurrent(_files, _lease, _participationSnapshot!);
            foreach (var source in _participationSources!)
            {
                var bytes = _files.ReadFileBytesSync(_lease, source.Key);
                if (new CanonicalBeforeImage(bytes is not null, bytes).Fingerprint != source.Value.Fingerprint)
                    throw new InvalidDataException("Participation locality changed within its publication owner.");
            }
        }

        internal void ApplyParticipationAfterImage(JsonObject profiles)
        {
            if (_participationOutcomes is null) return;
            // Pure detached composition; current authority is checked by the
            // actual owner at preparation and immediately before common publish.
            if (_published) throw new InvalidDataException("A closed participation owner cannot compose another afterimage.");
            foreach (var outcome in _participationOutcomes.OfType<JsonObject>())
            {
                var record = outcome["record"]!.AsObject();
                var id = record["targetId"]!.GetValue<string>();
                var target = profiles[AfterlifeEntityProfileState.ProfilesProperty]!.AsArray().OfType<JsonObject>()
                    .Single(p => p["actorType"]?.GetValue<string>() == "guardian" && SpiritualTreatmentParticipation.ProfileId(p) == id);
                var expected = _participationChanges!.Single(c => c.TargetId == id).Proposal;
                if (!JsonNode.DeepEquals(target[SpiritualTreatmentParticipation.ChangeProperty], expected))
                    throw new InvalidDataException("Participation afterimage lost the actual accepted proposal.");
                target.Remove(SpiritualTreatmentParticipation.ChangeProperty);
                target[SpiritualTreatmentParticipation.RecordProperty] = record.DeepClone();
            }
        }
    }
}
