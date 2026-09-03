using System.Text;
using System.Text.Json.Nodes;

namespace BookOfEternityClient.Services;

public partial class CanonicalStateNormalizer
{
    internal const string MortalItemFinalPublicationBaselineMismatchCode =
        "mortal_wound_treatment_publication_live_item_baseline_mismatch";

    private async Task ValidateMortalItemFinalPublicationBaselineAsync(
        AcceptedMechanicsPlan plan)
    {
        ArgumentNullException.ThrowIfNull(plan);
        var itemAuthority = plan.TreatmentResourcePublicationAuthority?
            .ItemPublicationAuthority;
        if (itemAuthority is null)
            return;

        var writeLease = _writeLease ?? throw new InvalidOperationException(
            "Mortal item final publication baseline validation requires the " +
            "owning canonical write lease.");
        if (!itemAuthority.HasValidSeal())
        {
            throw FinalBaselineMismatch(
                "game_state/wounds/accepted_turn_plan",
                "sealed item publication authority changed");
        }

        var paths = MortalItemCanonicalProjectionPlanner.ProjectionRootPaths;
        var baseline = itemAuthority.Baseline;
        var distinctPaths = paths.ToHashSet(StringComparer.Ordinal);
        if (paths.Count != distinctPaths.Count ||
            baseline.Issues.Count != 0 ||
            baseline.FinalCarrierRoots.Count != paths.Count ||
            paths.Any(path => !baseline.FinalCarrierRoots.ContainsKey(path)) ||
            baseline.FinalCarrierRoots.Keys.Any(path =>
                !distinctPaths.Contains(path)))
        {
            throw FinalBaselineMismatch(
                "game_state/wounds/accepted_turn_plan",
                "sealed final baseline root set changed");
        }

        if (paths.Count(path => string.Equals(
                path,
                MortalItemIdentityState.StatePath,
                StringComparison.Ordinal)) != 1 ||
            baseline.FinalCarrierRoots[MortalItemIdentityState.StatePath]
                is not JsonObject baselineIdentity ||
            !JsonNode.DeepEquals(
                baselineIdentity,
                baseline.IdentityIndexAfterImage))
        {
            throw FinalBaselineMismatch(
                MortalItemIdentityState.StatePath,
                "sealed final identity baseline changed");
        }

        foreach (var path in paths)
        {
            if (!HasSupportedExpectedTopology(
                    path,
                    baseline.FinalCarrierRoots[path]))
            {
                throw FinalBaselineMismatch(
                    path,
                    "sealed final baseline topology changed");
            }
        }

        var liveRoots = new Dictionary<string, JsonNode?>(
            paths.Count,
            StringComparer.Ordinal);
        foreach (var path in paths)
        {
            var bytes = await _fs.ReadFileBytesAsync(writeLease, path);
            string? json = null;
            if (bytes is not null)
            {
                try
                {
                    json = DecodeStrictUtf8(bytes);
                }
                catch (DecoderFallbackException)
                {
                    throw FinalBaselineMismatch(
                        path,
                        "live root is not strict UTF-8 JSON");
                }
            }

            var parsed = MortalItemProjectionRootParser.Parse(json, path);
            if (!parsed.IsValid)
            {
                throw FinalBaselineMismatch(
                    path,
                    parsed.Issues[0].Code ??
                    "mortal_item_projection_root_invalid");
            }
            liveRoots.Add(path, parsed.Root);
        }

        foreach (var path in paths)
        {
            if (!JsonNode.DeepEquals(
                    baseline.FinalCarrierRoots[path],
                    liveRoots[path]))
            {
                throw FinalBaselineMismatch(
                    path,
                    "live presence, topology, or content changed");
            }
        }
    }

    private static bool HasSupportedExpectedTopology(
        string path,
        JsonNode? root) =>
        root is null or JsonObject ||
        root is JsonArray && string.Equals(
            path,
            StorageTransportMoveService.VehiclesPath,
            StringComparison.Ordinal);

    private static string DecodeStrictUtf8(byte[] bytes)
    {
        var json = new UTF8Encoding(
                encoderShouldEmitUTF8Identifier: false,
                throwOnInvalidBytes: true)
            .GetString(bytes);
        return json.Length > 0 && json[0] == '\uFEFF'
            ? json[1..]
            : json;
    }

    private static InvalidDataException FinalBaselineMismatch(
        string path,
        string actual) => new(
        $"{MortalItemFinalPublicationBaselineMismatchCode}: " +
        $"final ordinary Mortal-item baseline changed at '{path}' ({actual}).");
}
