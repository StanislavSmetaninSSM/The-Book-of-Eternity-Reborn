using System.Text;
using System.Text.Json.Nodes;
using BookOfEternityClient.Core;
using BookOfEternityClient.Services;
using Microsoft.Extensions.Logging.Abstractions;

namespace BookOfEternityClient.Tests;

internal sealed partial class ResourceMaterializationTestContext : IAsyncDisposable
{
    internal const string DefinitionsPath = "game_state/resources/resource_definitions.json";
    internal const string StatePath = "game_state/resources/resource_state.json";
    internal const string HistoryPath = "game_state/resources/resource_history.json";
    internal const string CommandsPath = "game_state/resources/resource_commands.json";

    internal static readonly string[] AllResourcePaths =
    {
        DefinitionsPath,
        StatePath,
        HistoryPath,
        CommandsPath
    };

    private readonly string _expectedTempRoot;

    private ResourceMaterializationTestContext(string rootPath)
    {
        RootPath = Path.GetFullPath(rootPath);
        _expectedTempRoot = Path.GetFullPath(Path.GetTempPath()).TrimEnd(
            Path.DirectorySeparatorChar,
            Path.AltDirectorySeparatorChar) + Path.DirectorySeparatorChar;

        Directory.CreateDirectory(RootPath);
        FileSystem = new FileSystemManager(
            RootPath,
            NullLogger<FileSystemManager>.Instance);
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

    internal static Task<ResourceMaterializationTestContext> CreateAsync()
    {
        var rootPath = Path.Combine(
            Path.GetTempPath(),
            "boe-resource-materialization-" + Guid.NewGuid().ToString("N"));
        return Task.FromResult(new ResourceMaterializationTestContext(rootPath));
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

    internal async Task CaptureValidatedPendingSnapshotAsync(int turn = 42)
    {
        const string sessionId = "session_resource_materialization";
        const string requestId = "request_resource_materialization";
        const string playerAction = "Validate unified resource materialization.";

        await WriteExactJsonAsync(
            "input/turn_request.json",
            new JsonObject
            {
                ["sessionId"] = sessionId,
                ["requestId"] = requestId,
                ["turnNumber"] = turn,
                ["playerAction"] = playerAction
            }.ToJsonString());

        var files = new JsonObject();
        var snapshotFileHashes = new JsonObject();
        var rollbackBaselineFiles = new JsonArray();
        var trackedPaths = CanonicalStateNormalizer.NormalizerRollbackTrackedFiles
            .Concat(AllResourcePaths)
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
            snapshotFileHashes[path] = PendingTurnSnapshotAuthority.ComputeSha256(bytes);
            rollbackBaselineFiles.Add(path);
        }

        var manifest = new JsonObject
        {
            ["sessionId"] = sessionId,
            ["requestId"] = requestId,
            ["turnNumber"] = turn,
            ["requestTimestamp"] = "2026-08-15T00:00:00Z",
            ["playerAction"] = playerAction,
            ["files"] = files,
            ["snapshotFileHashes"] = snapshotFileHashes,
            ["clientOwnedValidationHashes"] = new JsonObject(),
            ["rollbackBackups"] = new JsonObject(),
            ["rollbackBaselineFiles"] = rollbackBaselineFiles,
            ["sourceLabel"] = "Unified resource materialization integration test",
            ["manifestPayloadHash"] = string.Empty
        };
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
