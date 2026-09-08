using System.Text;
using System.Text.Json;
using System.Text.Json.Nodes;
using BookOfEternityClient.Core;
using BookOfEternityClient.Models;
using BookOfEternityClient.Services;
using Microsoft.Extensions.Logging.Abstractions;

namespace BookOfEternityClient.Tests;

internal sealed partial class ResourceMaterializationTestContext : IAsyncDisposable
{
    internal const string DefinitionsPath = "game_state/resources/resource_definitions.json";
    internal const string StatePath = "game_state/resources/resource_state.json";
    internal const string HistoryPath = "game_state/resources/resource_history.json";
    internal const string AuthorityPath =
        "game_state/resources/resource_owner_authority.json";
    internal const string CommandsPath = "game_state/resources/resource_commands.json";
    internal const string FullPartyInteractionsPath =
        "game_state/misc/player_interactions.json";

    internal static readonly string[] AllResourcePaths =
    {
        DefinitionsPath,
        StatePath,
        HistoryPath,
        AuthorityPath,
        CommandsPath
    };

    private readonly string _expectedTempRoot;

    private ResourceMaterializationTestContext(
        string rootPath,
        FileSystemManagerHooks? hooks = null)
    {
        RootPath = Path.GetFullPath(rootPath);
        _expectedTempRoot = Path.GetFullPath(Path.GetTempPath()).TrimEnd(
            Path.DirectorySeparatorChar,
            Path.AltDirectorySeparatorChar) + Path.DirectorySeparatorChar;

        Directory.CreateDirectory(RootPath);
        FileSystem = new FileSystemManager(
            RootPath,
            NullLogger<FileSystemManager>.Instance,
            PhysicalLoadTransactionOperations.Instance,
            hooks);
        FileSystem.EnsureDirectoryStructure();
        Validator = new ValidationService(
            FileSystem,
            NullLogger<ValidationService>.Instance);
        Normalizer = new CanonicalStateNormalizer(
            FileSystem,
            NullLogger<CanonicalStateNormalizer>.Instance);
    }

    internal FileSystemManager FileSystem { get; }

    internal ValidationService Validator { get; }

    internal CanonicalStateNormalizer Normalizer { get; }

    internal string RootPath { get; }

    internal static Task<ResourceMaterializationTestContext> CreateAsync(
        FileSystemManagerHooks? hooks = null)
    {
        var rootPath = Path.Combine(
            Path.GetTempPath(),
            "boe-resource-materialization-" + Guid.NewGuid().ToString("N"));
        return Task.FromResult(new ResourceMaterializationTestContext(rootPath, hooks));
    }

    internal Task WriteExactBytesAsync(string relativePath, byte[] bytes)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(relativePath);
        ArgumentNullException.ThrowIfNull(bytes);
        return FileSystem.WriteFileAtomicBytesAsync(relativePath, bytes.ToArray());
    }

    internal Task WriteExactJsonAsync(string relativePath, string json)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(relativePath);
        ArgumentNullException.ThrowIfNull(json);
        return WriteExactBytesAsync(relativePath, new UTF8Encoding(false).GetBytes(json));
    }

    internal async Task<JsonNode?> ReadJsonAsync(string relativePath)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(relativePath);
        var json = await FileSystem.ReadFileAsync(relativePath);
        return json == null ? null : JsonNode.Parse(json);
    }

    internal Task DeleteAsync(string relativePath)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(relativePath);
        FileSystem.DeleteFile(relativePath);
        return Task.CompletedTask;
    }

    internal async Task CaptureValidatedPendingSnapshotAsync(
        int turn = 42,
        string currentRealm = "Mortal World",
        IEnumerable<string>? additionalTrackedPaths = null,
        int[]? preGeneratedDices1d20 = null)
    {
        const string sessionId = "session_resource_materialization";
        const string requestId = "request_resource_materialization";
        const string playerAction = "Validate unified resource materialization.";

        var request = new JsonObject
        {
            ["sessionId"] = sessionId,
            ["requestId"] = requestId,
            ["turnNumber"] = turn,
            ["playerAction"] = playerAction,
            ["currentRealm"] = currentRealm
        };
        if (preGeneratedDices1d20 != null)
            request["preGeneratedDices1d20"] = JsonSerializer.SerializeToNode(preGeneratedDices1d20);
        await WriteExactJsonAsync("input/turn_request.json", request.ToJsonString());

        var files = new JsonObject();
        var snapshotFileHashes = new JsonObject();
        var typedFiles = new Dictionary<string, string>(StringComparer.Ordinal);
        var typedSnapshotFileHashes = new Dictionary<string, string>(StringComparer.Ordinal);
        var rollbackBaselineFiles = new JsonArray();
        var trackedPaths = CanonicalStateNormalizer.NormalizerRollbackTrackedFiles
            .Concat(AllResourcePaths)
            .Append(FullPartyInteractionsPath)
            .Concat(PendingTurnSnapshotPathPresenceV1.LogicalPaths)
            .Concat(additionalTrackedPaths ?? Array.Empty<string>())
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .OrderBy(static path => path, StringComparer.Ordinal);

        foreach (var path in trackedPaths)
        {
            var bytes = await FileSystem.ReadFileBytesAsync(path);
            if (bytes == null)
                continue;

            var snapshotPath = $"game_state/control/pending_turn_snapshot/{path}";
            await FileSystem.WriteFileAtomicBytesAsync(snapshotPath, bytes);
            files[path] = snapshotPath;
            var snapshotHash = PendingTurnSnapshotAuthority.ComputeSha256(bytes);
            snapshotFileHashes[path] = snapshotHash;
            typedFiles.Add(path, snapshotPath);
            typedSnapshotFileHashes.Add(path, snapshotHash);
            rollbackBaselineFiles.Add(path);
        }
        var originalPathPresenceV1 = JsonSerializer.SerializeToNode(
            PendingTurnSnapshotPathPresenceV1.Create(
                typedFiles,
                typedSnapshotFileHashes))!.AsObject();

        var manifest = new JsonObject
        {
            ["sessionId"] = sessionId,
            ["requestId"] = requestId,
            ["turnNumber"] = turn,
            ["requestTimestamp"] = "2026-08-15T00:00:00Z",
            ["playerAction"] = playerAction
        };
        if (preGeneratedDices1d20 != null)
            manifest["preGeneratedDices1d20"] = JsonSerializer.SerializeToNode(preGeneratedDices1d20);
        manifest["progressionControl"] = JsonSerializer.SerializeToNode(
            new ProgressionControl { CurrentRealm = currentRealm });
        manifest["files"] = files;
        manifest["snapshotFileHashes"] = snapshotFileHashes;
        manifest["originalPathPresenceV1"] = originalPathPresenceV1;
        manifest["clientOwnedValidationHashes"] = new JsonObject();
        manifest["rollbackBackups"] = new JsonObject();
        manifest["rollbackBaselineFiles"] = rollbackBaselineFiles;
        manifest["sourceLabel"] = "Unified resource materialization integration test";
        manifest["manifestPayloadHash"] = string.Empty;
        manifest["manifestPayloadHash"] =
            PendingTurnSnapshotTestAuthority.ComputeManifestPayloadHash(manifest);

        await WriteExactJsonAsync(
            "game_state/control/pending_turn_snapshot.json",
            manifest.ToJsonString());
        await PendingTurnSnapshotTestAuthority.SyncAuthorityForCurrentManifestAsync(FileSystem);
    }

    public ValueTask DisposeAsync()
    {
        if (!RootPath.StartsWith(_expectedTempRoot, StringComparison.OrdinalIgnoreCase) ||
            !Path.GetFileName(RootPath).StartsWith(
                "boe-resource-materialization-",
                StringComparison.Ordinal))
        {
            throw new InvalidOperationException(
                $"Refusing to remove unexpected resource test root '{RootPath}'.");
        }

        try
        {
            if (Directory.Exists(RootPath))
                Directory.Delete(RootPath, recursive: true);
        }
        catch (IOException)
        {
            // A later test run can reclaim an isolated temp root left by an open handle.
        }
        catch (UnauthorizedAccessException)
        {
            // Keep cleanup best-effort without hiding test assertions.
        }

        return ValueTask.CompletedTask;
    }
}
