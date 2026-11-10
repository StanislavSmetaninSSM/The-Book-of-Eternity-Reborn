using System.Text.Json;
using System.Text.Json.Nodes;
using BookOfEternityClient.Core;
using BookOfEternityClient.Models;

namespace BookOfEternityClient.Services;

internal static partial class PendingTurnSnapshotPathPresenceV1
{
    internal const string CanonicalDiagnosisHint = "canonicalRequirementSources";

    // The finite family differs only by realm and the actual resource/quest
    // declarations. All other canonical kinds use the same complete base read set.
    private static IReadOnlyList<IReadOnlyList<string>> BuildCanonicalDiagnosisForms() => Array.AsReadOnly(
        new[] { "chaos_sea", "shining_abode" }.SelectMany(realm => Enumerable.Range(0, 4).Select(flags =>
            CanonicalDiagnosisPaths(realm, new[] { "item_quantity" }
                .Concat((flags & 1) != 0 ? new[] { "resource_quantity" } : [])
                .Concat((flags & 2) != 0 ? new[] { "quest_state" } : [])))).ToArray());

    internal static bool IsCanonicalDiagnosisForm(IReadOnlySet<string> paths) =>
        CanonicalDiagnosisForms.Any(form => paths.Count == form.Count && paths.SetEquals(form));

    private static IReadOnlyList<string> CanonicalDiagnosisPaths(string realm, IEnumerable<string> kinds) =>
        Array.AsReadOnly(LogicalPaths.Concat(AfterlifeWoundTreatmentAuthority.DiagnosisSourcePaths(realm, kinds))
            .Distinct(StringComparer.Ordinal).ToArray());

    internal static IReadOnlyList<string> SelectDiagnosisObservation(TurnRequest request, JsonObject staged, JsonObject profiles)
    {
        if (request.SpiritualHealing?["mode"]?.GetValue<string>() != "diagnose" || request.ProgressionControl is null ||
            !SpiritualDiagnosisRequestCodec.IsBound(staged, new(request.SessionId, request.RequestId, request.TurnNumber, request.ProgressionControl)) ||
            request.SpiritualHealing["requestFingerprint"]?.GetValue<string>() != staged["requestFingerprint"]?.GetValue<string>())
            throw new InvalidDataException("Diagnosis observation requires its actual bound staged request/projection.");
        var selection = SpiritualDiagnosisRequestCodec.ReadSelection(staged);
        var kinds = AfterlifeWoundTreatmentAuthority.DiagnosisRequirementKinds(profiles, selection);
        return kinds.Count == 0 ? LogicalPaths : CanonicalDiagnosisPaths(selection.Diagnosis!.Target.Realm, kinds);
    }

    internal static async Task<IReadOnlyList<string>> SelectObservationAsync(FileSystemManager files,
        FileSystemManager.CanonicalWriteLease lease, TurnRequest request)
    {
        // A missing hint is never a permission. Readers still independently prove
        // every selected original source/absence against actual declarations.
        if (request.SpiritualHealing?[CanonicalDiagnosisHint] is not JsonValue hint ||
            !hint.TryGetValue<bool>(out var include) || !include)
            return GetLogicalPaths(HasCombatProjection(request.SpiritualHealing));
        files.EnsureCanonicalWriteLeaseActive(lease); files.VerifyCurrentSessionOperation(lease);
        async Task<JsonObject> Root(string path)
        {
            var bytes = await files.ReadFileBytesAsync(lease, path) ?? throw new InvalidDataException("Missing diagnosis observation source: " + path);
            return StrictJsonAuthority.Deserialize<JsonObject>(CanonicalJsonUtf8.DecodeOneOptionalBom(bytes), new JsonSerializerOptions(), path)
                ?? throw new InvalidDataException("Invalid diagnosis observation source: " + path);
        }
        var staged = await Root(AcceptedMechanicsPlan.WoundCommandPath);
        var profiles = await Root(WoundCarrierCatalog.AfterlifeProfilesPath);
        var paths = SelectDiagnosisObservation(request, staged, profiles);
        files.EnsureCanonicalWriteLeaseActive(lease); files.VerifyCurrentSessionOperation(lease);
        return paths;
    }
}
