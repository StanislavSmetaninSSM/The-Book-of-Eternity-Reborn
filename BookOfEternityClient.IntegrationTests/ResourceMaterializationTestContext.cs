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

    /// <summary>
    /// Creates an isolated resource fixture with its own real runtime session generation.
    /// </summary>
    /// <param name="hooks">
    /// Optional hooks applied to this fixture's filesystem; <see langword="null"/> uses ordinary operations.
    /// </param>
    /// <returns>
    /// A fresh context whose generation was initialized under its own canonical write lease.
    /// </returns>
    internal static async Task<ResourceMaterializationTestContext> CreateAsync(
        FileSystemManagerHooks? hooks = null)
    {
        var rootPath = Path.Combine(
            Path.GetTempPath(),
            "boe-resource-materialization-" + Guid.NewGuid().ToString("N"));
        var context = new ResourceMaterializationTestContext(rootPath, hooks);
        try
        {
            await using var lease = await context.FileSystem.AcquireCanonicalWriteLeaseAsync();
            context.FileSystem.GetOrCreateSessionGeneration(lease);
            return context;
        }
        catch
        {
            await context.DisposeAsync();
            throw;
        }
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

    /// <summary>
    /// Captures a signed test snapshot for one independently identified turn request.
    /// </summary>
    /// <param name="turn">
    /// Turn number recorded in the request and signed manifest.
    /// </param>
    /// <param name="currentRealm">
    /// Realm recorded in the request and progression control.
    /// </param>
    /// <param name="additionalTrackedPaths">
    /// Extra canonical paths to include in the rollback baseline.
    /// </param>
    /// <param name="preGeneratedDices1d20">
    /// Signed D20 sequence, or <see langword="null"/> when no dice are supplied.
    /// </param>
    /// <param name="sessionId">
    /// Session identity; the default preserves existing one-turn fixtures.
    /// </param>
    /// <param name="requestId">
    /// Request identity; use a distinct value for a later-turn fixture.
    /// </param>
    /// <returns>
    /// A task that completes after the signed test manifest and its authority are written.
    /// </returns>
    internal async Task CaptureValidatedPendingSnapshotAsync(
        int turn = 42,
        string currentRealm = "Mortal World",
        IEnumerable<string>? additionalTrackedPaths = null,
        int[]? preGeneratedDices1d20 = null,
        string sessionId = "session_resource_materialization",
        string requestId = "request_resource_materialization")
    {
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
            .Concat(additionalTrackedPaths ?? Array.Empty<string>())
            .ToHashSet(StringComparer.OrdinalIgnoreCase);
        var snapshotPaths = trackedPaths
            .Concat(PendingTurnSnapshotPathPresenceV1.LogicalPaths)
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .OrderBy(static path => path, StringComparer.Ordinal);

        foreach (var path in snapshotPaths)
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
            if (trackedPaths.Contains(path))
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
