using System.Text.Json.Nodes;
using BookOfEternityClient.Core;
using BookOfEternityClient.Services;
using Microsoft.Extensions.Logging.Abstractions;

namespace BookOfEternityClient.Tests;

internal sealed partial class MortalItemMaterializationTestContext : IAsyncDisposable
{
    private readonly string _expectedTempRoot;

    private MortalItemMaterializationTestContext(string rootPath)
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

    internal static Task<MortalItemMaterializationTestContext> CreateAsync()
    {
        var rootPath = Path.Combine(
            Path.GetTempPath(),
            "boe-mortal-item-materialization-" + Guid.NewGuid().ToString("N"));
        return Task.FromResult(new MortalItemMaterializationTestContext(rootPath));
    }

    internal Task WriteJsonAsync(string relativePath, JsonNode root)
    {
        ArgumentNullException.ThrowIfNull(root);
        return FileSystem.WriteFileAtomicAsync(relativePath, root.ToJsonString());
    }

    internal async Task<JsonNode?> ReadJsonAsync(string relativePath)
    {
        var json = await FileSystem.ReadFileAsync(relativePath);
        return string.IsNullOrWhiteSpace(json) ? null : JsonNode.Parse(json);
    }

    internal async Task CaptureValidatedPendingSnapshotAsync(int turn = 42)
    {
        const string sessionId = "session_mortal_item_materialization";
        const string requestId = "request_mortal_item_materialization";
        const string playerAction = "Validate Mortal item materialization.";

        await WriteJsonAsync(
            "input/turn_request.json",
            new JsonObject
            {
                ["sessionId"] = sessionId,
                ["requestId"] = requestId,
                ["turnNumber"] = turn,
                ["playerAction"] = playerAction
            });

        var files = new JsonObject();
        var snapshotFileHashes = new JsonObject();
        var rollbackBaselineFiles = new JsonArray();
        foreach (var path in CanonicalStateNormalizer.NormalizerRollbackTrackedFiles
                     .Distinct(StringComparer.OrdinalIgnoreCase)
                     .OrderBy(value => value, StringComparer.Ordinal))
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
            ["requestTimestamp"] = "2026-08-11T00:00:00Z",
            ["playerAction"] = playerAction,
            ["files"] = files,
            ["snapshotFileHashes"] = snapshotFileHashes,
            ["clientOwnedValidationHashes"] = new JsonObject(),
            ["rollbackBackups"] = new JsonObject(),
            ["rollbackBaselineFiles"] = rollbackBaselineFiles,
            ["sourceLabel"] = "Mortal item materialization integration test",
            ["manifestPayloadHash"] = string.Empty
        };
        manifest["manifestPayloadHash"] =
            PendingTurnSnapshotTestAuthority.ComputeManifestPayloadHash(manifest);

        await WriteJsonAsync(
            "game_state/control/pending_turn_snapshot.json",
            manifest);
        await PendingTurnSnapshotTestAuthority.SyncAuthorityForCurrentManifestAsync(FileSystem);
    }

    public ValueTask DisposeAsync()
    {
        if (!RootPath.StartsWith(_expectedTempRoot, StringComparison.OrdinalIgnoreCase) ||
            !Path.GetFileName(RootPath).StartsWith(
                "boe-mortal-item-materialization-",
                StringComparison.Ordinal))
        {
            throw new InvalidOperationException(
                $"Refusing to remove unexpected Mortal item test root '{RootPath}'.");
        }

        try
        {
            if (Directory.Exists(RootPath))
                Directory.Delete(RootPath, recursive: true);
        }
        catch (IOException)
        {
            // A later test run can reclaim an isolated temp directory left by an open handle.
        }
        catch (UnauthorizedAccessException)
        {
            // Keep test cleanup best-effort without hiding test assertions.
        }

        return ValueTask.CompletedTask;
    }
}
