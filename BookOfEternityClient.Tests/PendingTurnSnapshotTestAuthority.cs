using System.Text.Json;
using System.Text.Json.Nodes;
using System.Text.Json.Serialization;
using BookOfEternityClient.Core;
using BookOfEternityClient.Services;

namespace BookOfEternityClient.Tests;

internal static class PendingTurnSnapshotTestAuthority
{
    private const string ManifestPath = "game_state/control/pending_turn_snapshot.json";

    private static readonly JsonSerializerOptions ManifestHashJsonOpts = new()
    {
        DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull,
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
        Encoder = System.Text.Encodings.Web.JavaScriptEncoder.UnsafeRelaxedJsonEscaping
    };

    internal static async Task SyncAuthorityForCurrentManifestAsync(FileSystemManager fs, bool deleteOnInvalid = true)
    {
        var manifestJson = await fs.ReadFileAsync(ManifestPath);
        if (string.IsNullOrWhiteSpace(manifestJson))
        {
            DeleteAuthorityIfPresent(fs);
            return;
        }

        JsonObject? manifest;
        try
        {
            manifest = JsonNode.Parse(manifestJson) as JsonObject;
        }
        catch
        {
            manifest = null;
        }

        if (manifest == null || !HasSelfConsistentManifestHash(manifest))
        {
            if (deleteOnInvalid)
                DeleteAuthorityIfPresent(fs);
            return;
        }

        await WriteAuthorityForManifestAsync(fs, manifest);
    }

    internal static async Task WriteAuthorityForManifestAsync(FileSystemManager fs, JsonObject manifest)
    {
        var authorityJson = PendingTurnSnapshotAuthority.CreateDetachedAuthorityJson(
            manifest,
            ManifestHashJsonOpts,
            static snapshotManifest => snapshotManifest["manifestPayloadHash"]?.GetValue<string>() ?? string.Empty,
            static (snapshotManifest, hash) => snapshotManifest["manifestPayloadHash"] = hash,
            static snapshotManifest => snapshotManifest["sessionId"]?.GetValue<string>() ?? string.Empty,
            static snapshotManifest => snapshotManifest["requestId"]?.GetValue<string>() ?? string.Empty,
            static snapshotManifest => snapshotManifest["turnNumber"]?.GetValue<int>() ?? 0,
            static snapshotManifest => ToStringDictionary(snapshotManifest["files"] as JsonObject),
            static snapshotManifest => ToStringDictionary(snapshotManifest["snapshotFileHashes"] as JsonObject),
            static snapshotManifest => ToStringDictionary(snapshotManifest["clientOwnedValidationHashes"] as JsonObject),
            static snapshotManifest => ToStringList(snapshotManifest["rollbackBaselineFiles"] as JsonArray),
            static snapshotManifest => snapshotManifest["sourceLabel"]?.GetValue<string>(),
            static snapshotManifest => ToStringDictionary(snapshotManifest["rollbackBackups"] as JsonObject),
            relativePath => ReadRelativeFile(fs, relativePath));

        await fs.WriteFileAtomicAsync(PendingTurnSnapshotAuthority.AuthorityPath, authorityJson);
    }

    internal static string ComputeManifestPayloadHash(JsonObject manifest)
    {
        var clone = manifest.DeepClone().AsObject();
        clone["manifestPayloadHash"] = string.Empty;
        return PendingTurnSnapshotAuthority.ComputeSha256(JsonSerializer.Serialize(clone, ManifestHashJsonOpts));
    }

    private static bool HasSelfConsistentManifestHash(JsonObject manifest)
    {
        var currentHash = manifest["manifestPayloadHash"]?.GetValue<string>() ?? string.Empty;
        if (string.IsNullOrWhiteSpace(currentHash))
            return false;

        return string.Equals(
            currentHash,
            ComputeManifestPayloadHash(manifest),
            StringComparison.OrdinalIgnoreCase);
    }

    private static Dictionary<string, string> ToStringDictionary(JsonObject? root)
    {
        var result = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
        if (root == null)
            return result;

        foreach (var pair in root)
        {
            if (pair.Value is not JsonValue valueNode ||
                !valueNode.TryGetValue<string>(out var value) ||
                string.IsNullOrWhiteSpace(pair.Key) ||
                string.IsNullOrWhiteSpace(value))
            {
                continue;
            }

            result[pair.Key.Replace('\\', '/')] = value.Replace('\\', '/');
        }

        return result;
    }

    private static List<string> ToStringList(JsonArray? root)
    {
        var result = new List<string>();
        if (root == null)
            return result;

        foreach (var item in root)
        {
            if (item is not JsonValue valueNode ||
                !valueNode.TryGetValue<string>(out var value) ||
                string.IsNullOrWhiteSpace(value))
            {
                continue;
            }

            result.Add(value.Replace('\\', '/'));
        }

        return result;
    }

    private static void DeleteAuthorityIfPresent(FileSystemManager fs)
    {
        if (fs.FileExists(PendingTurnSnapshotAuthority.AuthorityPath))
            fs.DeleteFile(PendingTurnSnapshotAuthority.AuthorityPath);
    }

    private static string? ReadRelativeFile(FileSystemManager fs, string relativePath)
    {
        if (!PendingTurnSnapshotAuthority.IsSafeRelativePath(relativePath))
            return null;

        var fullPath = fs.ResolvePath(relativePath);
        if (!File.Exists(fullPath))
            return null;

        return File.ReadAllText(fullPath);
    }
}
