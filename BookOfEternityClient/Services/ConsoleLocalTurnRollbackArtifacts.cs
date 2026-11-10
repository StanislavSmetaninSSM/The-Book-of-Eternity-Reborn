using System.Security.Cryptography;
using System.Text.Json;
using System.Text.Json.Serialization;

namespace BookOfEternityClient.Services;

// Refusal/admission evidence for the existing console snapshot owner. This is
// not a restore journal: only the original snapshot or signed turn owns restore.
internal static class ConsoleLocalTurnRollbackArtifacts
{
    internal const string Root = "game_state/control/console_local_turn_before_images_v1";
    internal const string MarkerName = "preparation.rollback.marker";
    private sealed record Marker(int SchemaVersion, string Generation, string SnapshotId);
    private static readonly JsonSerializerOptions Options = new() { UnmappedMemberHandling = JsonUnmappedMemberHandling.Disallow };

    internal static string CreateRoot() => Root + "/" + Guid.NewGuid().ToString("N");
    internal static bool IsArtifact(string path) => path.StartsWith(Root + "/", StringComparison.OrdinalIgnoreCase);
    internal static bool IsMarker(string path) => IsArtifact(path) && path.EndsWith("/" + MarkerName, StringComparison.Ordinal);
    internal static string GetCohortRoot(string path)
    {
        var parts = path.Split('/');
        var depth = Root.Split('/').Length;
        if (!path.StartsWith(Root + "/", StringComparison.Ordinal) || parts.Length != depth + 2 ||
            !Guid.TryParseExact(parts[depth], "N", out var id) || parts[depth] != id.ToString("N") ||
            (parts[^1] != MarkerName && (!parts[^1].Contains(".rollback.", StringComparison.Ordinal) ||
                parts[^1].Any(ch => !char.IsLetterOrDigit(ch) && ch is not ('.' or '-' or '_')))))
            throw new InvalidDataException("Unrecognized console preparation evidence; original bytes retained.");
        return path[..path.LastIndexOf('/')];
    }
    internal static byte[] CreateMarker(string root, string generation) =>
        JsonSerializer.SerializeToUtf8Bytes(new Marker(1, generation, root[(root.LastIndexOf('/') + 1)..]), Options);
    internal static void ValidateMarker(string root, byte[] bytes, string generation)
    {
        try
        {
            var marker = JsonSerializer.Deserialize<Marker>(bytes, Options);
            if (marker?.SchemaVersion != 1 || marker.Generation != generation ||
                marker.SnapshotId != root[(root.LastIndexOf('/') + 1)..])
                throw new InvalidDataException("Console preparation generation or identity differs; evidence retained.");
        }
        catch (JsonException failure) { throw new InvalidDataException("Invalid console preparation marker; evidence retained.", failure); }
    }
    internal static string Hash(byte[] bytes) => Convert.ToHexString(SHA256.HashData(bytes));
}
