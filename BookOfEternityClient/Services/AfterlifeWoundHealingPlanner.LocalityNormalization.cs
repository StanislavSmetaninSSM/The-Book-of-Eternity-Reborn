using System.Text.Json;
using System.Text.Json.Nodes;
using BookOfEternityClient.Core;

namespace BookOfEternityClient.Services;

internal static partial class AfterlifeWoundHealingPlanner
{
    internal sealed partial class Continuation
    {
        internal bool HasLocalityGuards => _participationSources is not null || _data?.AidWitness is not null || HasServicePayment;
        private SpiritualAidSelection? RetainedAidSelection =>
            (_data?.AidWitness ?? _participationOutcomes?.Single()?.AsObject())?["record"] is JsonObject record
                ? SpiritualTreatmentParticipation.Selection(record) : null;
        internal bool OwnsLocalityPath(string path) => HasLocalityGuards &&
            (HasServicePayment ? AfterlifeWoundTreatmentAuthority.ServiceLocalityPathsFor(_data!.ServiceQuote!) :
                AfterlifeWoundTreatmentAuthority.AidLocalityPathsFor(RetainedAidSelection?.ExplicitLocality, includeCoreState: true))
                .Contains(path, StringComparer.Ordinal);

        // Only the actual validated normalizer calls this around its computed
        // write. Original/evidence/source fingerprint remain frozen; only the
        // exact live closing image advances to this verified client-owned output.
        internal async Task PublishOwnedLocalityNormalizationAsync(FileSystemManager files,
            FileSystemManager.CanonicalWriteLease lease, string path, string content, Func<CanonicalBeforeImage, Task> publish)
        {
            if (!OwnsLocalityPath(path)) throw new InvalidDataException("A spiritual owner cannot advance an unrelated normalizer source.");
            using var fresh = FileSystemManager.SuppressValidationReuse();
            RequireCurrent(files, lease);
            var images = _participationSources ?? _sources;
            if (HasServicePayment)
            {
                var beforeRoot = StrictJsonAuthority.Deserialize<JsonObject>(CanonicalJsonUtf8.DecodeOneOptionalBom(images[path].Bytes!), new JsonSerializerOptions(), path)!;
                var afterRoot = StrictJsonAuthority.Deserialize<JsonObject>(content, new JsonSerializerOptions(), path)!;
                // Existing operation-private parsed DATA; RequireCurrent above/below
                // still closes every physical source, including the current hall.
                var serviceShining = _parsedSourceRoots.GetValueOrDefault(ShiningAbodeState.StatePath) as JsonObject;
                var rosterPath = GuardianAbodeResidentState.StatePath;
                var serviceGuardianPath = AfterlifeWoundTreatmentAuthority.ServiceLocalityPaths[0];
                var serviceGuardians = _parsedSourceRoots.GetValueOrDefault(serviceGuardianPath) as JsonObject;
                var serviceResidents = _parsedSourceRoots.GetValueOrDefault(rosterPath) as JsonObject;
                var beforeAdmission = AfterlifeWoundTreatmentAuthority.RequireServiceAdmission(
                    path == serviceGuardianPath ? beforeRoot : serviceGuardians, _data!.Carriers.AfterlifeProfiles!, _data.ServiceQuote!,
                    serviceShining, path == rosterPath ? beforeRoot : serviceResidents);
                var afterAdmission = AfterlifeWoundTreatmentAuthority.RequireServiceAdmission(
                    path == serviceGuardianPath ? afterRoot : serviceGuardians, _data.Carriers.AfterlifeProfiles!, _data.ServiceQuote!,
                    serviceShining, path == rosterPath ? afterRoot : serviceResidents);
                if (beforeAdmission.ProviderFingerprint != _data.ServiceProviderAdmissionFingerprint ||
                    afterAdmission.ProviderFingerprint != beforeAdmission.ProviderFingerprint ||
                    beforeAdmission.Scope.LocationId != afterAdmission.Scope.LocationId ||
                    beforeAdmission.Scope.CurrentGuardianId != afterAdmission.Scope.CurrentGuardianId ||
                    !beforeAdmission.Scope.LocalActorIds.ToHashSet(StringComparer.Ordinal).SetEquals(afterAdmission.Scope.LocalActorIds) ||
                    !beforeAdmission.Scope.LocalFactionIds.ToHashSet(StringComparer.Ordinal).SetEquals(afterAdmission.Scope.LocalFactionIds))
                    throw new InvalidDataException("Locality normalization cannot change the exact admitted public service tuple.");
                if (path == rosterPath) RequireResidentThoughtAfterImage(beforeRoot, afterRoot);
                var expectedService = new CanonicalBeforeImage(true, FileSystemManager.EncodeUtf8WithPreamble(content));
                await publish(images[path]);
                var publishedService = await files.ReadFileBytesAsync(lease, path);
                if (new CanonicalBeforeImage(publishedService is not null, publishedService).Fingerprint != expectedService.Fingerprint)
                    throw new InvalidDataException("Service locality normalization differs from its exact owned image.");
                var advancedService = images.ToDictionary(p => p.Key, p => p.Value, StringComparer.Ordinal);
                advancedService[path] = expectedService; _sources = advancedService;
                RequireCurrent(files, lease);
                return;
            }
            var record = (_data?.AidWitness ?? _participationOutcomes?.Single()?.AsObject())?["record"]?.AsObject()
                ?? throw new InvalidDataException("Locality normalization requires its retained participation record.");
            var aid = SpiritualTreatmentParticipation.Selection(record);
            var profiles = _data?.Carriers.AfterlifeProfiles ?? _natural!.Carriers.AfterlifeProfiles!;
            var wound = _wounds.Single(w => w.WoundId == record["woundId"]!.GetValue<string>());
            JsonObject? Root(string selectedPath, string? replacement = null)
            {
                if (!images.TryGetValue(selectedPath, out var image)) return null;
                return replacement is not null ? StrictJsonAuthority.Deserialize<JsonObject>(replacement, new JsonSerializerOptions(), selectedPath)
                    : !image.Existed ? null : StrictJsonAuthority.Deserialize<JsonObject>(
                        CanonicalJsonUtf8.DecodeOneOptionalBom(image.Bytes!), new JsonSerializerOptions(), selectedPath);
            }
            var guardianPath = AfterlifeWoundTreatmentAuthority.AidLocalityPaths[0];
            var residentPath = AfterlifeWoundTreatmentAuthority.AidLocalityPaths[1];
            var shiningPath = ShiningAbodeState.StatePath;
            var guardians = Root(guardianPath);
            var residents = Root(residentPath);
            var shining = Root(shiningPath);
            var afterGuardians = path == guardianPath ? Root(path, content) : guardians;
            var afterResidents = path == residentPath ? Root(path, content) : residents;
            var afterShining = path == shiningPath ? Root(path, content) : shining;
            var before = SpiritualTreatmentParticipation.RequireAidScope(guardians, residents, profiles, aid, wound, shining);
            var after = SpiritualTreatmentParticipation.RequireAidScope(afterGuardians, afterResidents, profiles, aid, wound, afterShining);
            if (aid.ExplicitLocality is { Realm: "shining_abode" } locality)
            {
                var originalAdmission = SpiritualTreatmentParticipation.RequireShiningProviderAdmission(
                    guardians, residents, profiles, shining, aid.Healer("shining_abode"), locality);
                var normalizedAdmission = SpiritualTreatmentParticipation.RequireShiningProviderAdmission(
                    afterGuardians, afterResidents, profiles, afterShining, aid.Healer("shining_abode"), locality);
                if (originalAdmission.Fingerprint != normalizedAdmission.Fingerprint)
                    throw new InvalidDataException("Normalization cannot change the exact negotiated healer membership/role/locality tuple.");
            }
            if (before.RealmKind != after.RealmKind || before.LocationId != after.LocationId || before.CurrentGuardianId != after.CurrentGuardianId ||
                !before.LocalActorIds.ToHashSet(StringComparer.Ordinal).SetEquals(after.LocalActorIds) ||
                !before.LocalFactionIds.ToHashSet(StringComparer.Ordinal).SetEquals(after.LocalFactionIds))
                throw new InvalidDataException("Client locality normalization cannot change admitted Guardian/Abode/presence authority.");
            if (path == residentPath) RequireResidentThoughtAfterImage(residents!, Root(path, content)!);
            var expected = new CanonicalBeforeImage(true, FileSystemManager.EncodeUtf8WithPreamble(content));
            await publish(images[path]);
            var actual = await files.ReadFileBytesAsync(lease, path);
            if (new CanonicalBeforeImage(actual is not null, actual).Fingerprint != expected.Fingerprint)
                throw new InvalidDataException("Owned locality normalization did not publish its exact computed afterimage.");
            var advanced = images.ToDictionary(p => p.Key, p => p.Value, StringComparer.Ordinal);
            advanced[path] = expected;
            if (_participationSources is not null) _participationSources = advanced;
            if (_sources.ContainsKey(path))
            {
                // Shining state is also a core source. Advance every existing live
                // closing guard after exact read-back, never the signed original DATA.
                var advancedSources = _sources.ToDictionary(p => p.Key, p => p.Value, StringComparer.Ordinal);
                advancedSources[path] = expected;
                _sources = advancedSources;
            }
            RequireCurrent(files, lease);
        }
    }
}
