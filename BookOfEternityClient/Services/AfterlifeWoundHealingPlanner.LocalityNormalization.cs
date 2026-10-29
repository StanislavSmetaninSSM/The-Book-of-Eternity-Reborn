using System.Text.Json;
using System.Text.Json.Nodes;
using BookOfEternityClient.Core;

namespace BookOfEternityClient.Services;

internal static partial class AfterlifeWoundHealingPlanner
{
    internal sealed partial class Continuation
    {
        internal bool HasLocalityGuards => _participationSources is not null || _data?.AidWitness is not null || HasServicePayment;
        internal bool OwnsLocalityPath(string path) => HasLocalityGuards &&
            (HasServicePayment ? AfterlifeWoundTreatmentAuthority.ServiceLocalityPaths : AfterlifeWoundTreatmentAuthority.AidLocalityPaths)
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
                var shining = _parsedSourceRoots.GetValueOrDefault(ShiningAbodeState.StatePath) as JsonObject;
                var beforeScope = AfterlifeWoundTreatmentAuthority.RequireServiceScope(beforeRoot, _data!.Carriers.AfterlifeProfiles!, _data.ServiceQuote!, shining);
                var afterScope = AfterlifeWoundTreatmentAuthority.RequireServiceScope(afterRoot, _data.Carriers.AfterlifeProfiles!, _data.ServiceQuote!, shining);
                if (beforeScope.LocationId != afterScope.LocationId || beforeScope.CurrentGuardianId != afterScope.CurrentGuardianId ||
                    !beforeScope.LocalActorIds.ToHashSet(StringComparer.Ordinal).SetEquals(afterScope.LocalActorIds) ||
                    !beforeScope.LocalFactionIds.ToHashSet(StringComparer.Ordinal).SetEquals(afterScope.LocalFactionIds))
                    throw new InvalidDataException("Locality normalization cannot move a confirmed public service.");
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
                var image = images[selectedPath];
                return replacement is not null ? StrictJsonAuthority.Deserialize<JsonObject>(replacement, new JsonSerializerOptions(), selectedPath)
                    : !image.Existed ? null : StrictJsonAuthority.Deserialize<JsonObject>(
                        CanonicalJsonUtf8.DecodeOneOptionalBom(image.Bytes!), new JsonSerializerOptions(), selectedPath);
            }
            var guardianPath = AfterlifeWoundTreatmentAuthority.AidLocalityPaths[0];
            var residentPath = AfterlifeWoundTreatmentAuthority.AidLocalityPaths[1];
            var guardians = Root(guardianPath)!;
            var residents = Root(residentPath);
            var before = SpiritualTreatmentParticipation.RequireAidScope(guardians, residents, profiles, aid, wound);
            var after = SpiritualTreatmentParticipation.RequireAidScope(
                path == guardianPath ? Root(path, content)! : guardians,
                path == residentPath ? Root(path, content) : residents, profiles, aid, wound);
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
            else _sources = advanced; // Active aid carries locality in its existing main live closing set.
            RequireCurrent(files, lease);
        }
    }
}
