using System.Text.Json.Nodes;
using BookOfEternityClient.Core;
using BookOfEternityClient.Services;
using Microsoft.Extensions.Logging.Abstractions;

namespace BookOfEternityClient.Tests;

internal sealed class MortalLocationMaterializationTestContext : IAsyncDisposable
{
    internal const string WorldMapPath = "game_state/world/world_map.json";
    internal const string CurrentLocationPath = "game_state/world/current_location.json";
    internal const string IdentityIndexPath = "game_state/world/location_identity_index.json";

    private readonly string _expectedTempRoot;
    private string? _armedWriteFailurePath;
    private int _remainingWriteFailureMatches;

    private MortalLocationMaterializationTestContext(string rootPath)
    {
        RootPath = Path.GetFullPath(rootPath);
        _expectedTempRoot = Path.GetFullPath(Path.GetTempPath()).TrimEnd(
            Path.DirectorySeparatorChar,
            Path.AltDirectorySeparatorChar) + Path.DirectorySeparatorChar;

        Directory.CreateDirectory(RootPath);
        var hooks = new FileSystemManagerHooks
        {
            AfterPhysicalFilePublishedAsync = AfterPhysicalFilePublishedAsync
        };
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

    internal string? InjectedPublishedPath { get; private set; }

    internal static Task<MortalLocationMaterializationTestContext> CreateAsync()
    {
        var rootPath = Path.Combine(
            Path.GetTempPath(),
            "boe-mortal-location-materialization-" + Guid.NewGuid().ToString("N"));
        return Task.FromResult(new MortalLocationMaterializationTestContext(rootPath));
    }

    internal Task WriteJsonAsync(string relativePath, JsonNode value)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(relativePath);
        ArgumentNullException.ThrowIfNull(value);
        return FileSystem.WriteFileAtomicAsync(relativePath, value.ToJsonString());
    }

    internal async Task<JsonNode?> ReadJsonAsync(string relativePath)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(relativePath);
        var json = await FileSystem.ReadFileAsync(relativePath);
        return string.IsNullOrWhiteSpace(json) ? null : JsonNode.Parse(json);
    }

    internal async Task WritePreTurnCanonicalStateAsync(
        JsonObject location,
        JsonObject? link = null)
    {
        ArgumentNullException.ThrowIfNull(location);
        var map = MortalLocationTestFixture.CreateWorldMap(location);
        if (link != null)
            map["links"]!.AsArray().Add(link.DeepClone());

        await WriteJsonAsync(WorldMapPath, map);
        await WriteJsonAsync(
            CurrentLocationPath,
            MortalLocationTestFixture.CreateCurrentProjection(location));
        await WriteJsonAsync(
            IdentityIndexPath,
            MortalLocationTestFixture.CreateIdentityIndex(location, link));
    }

    internal async Task WriteRawTurnStateAsync(
        JsonObject? currentLocationData,
        JsonObject? worldMapUpdates)
    {
        if (currentLocationData != null)
        {
            await WriteJsonAsync(
                CurrentLocationPath,
                new JsonObject
                {
                    ["currentLocationData"] = currentLocationData.DeepClone()
                });
        }

        if (worldMapUpdates != null)
        {
            await WriteJsonAsync(
                WorldMapPath,
                new JsonObject
                {
                    ["worldMapUpdates"] = worldMapUpdates.DeepClone()
                });
        }
    }

    internal void ArmInjectedWriteFailure(string relativePath, int matchingWrite = 1)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(relativePath);
        if (matchingWrite < 1)
            throw new ArgumentOutOfRangeException(nameof(matchingWrite));

        _armedWriteFailurePath = relativePath;
        _remainingWriteFailureMatches = matchingWrite;
        InjectedPublishedPath = null;
    }

    internal async Task CaptureValidatedPendingSnapshotAsync(int turn = 42)
    {
        const string sessionId = "session_mortal_location_materialization";
        const string requestId = "request_mortal_location_materialization";
        const string playerAction = "Validate Mortal location materialization.";

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
        var trackedPaths = CanonicalStateNormalizer.NormalizerRollbackTrackedFiles
            .Concat(new[] { WorldMapPath, CurrentLocationPath, IdentityIndexPath })
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .OrderBy(static value => value, StringComparer.Ordinal);
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
            ["requestTimestamp"] = "2026-08-12T00:00:00Z",
            ["playerAction"] = playerAction,
            ["files"] = files,
            ["snapshotFileHashes"] = snapshotFileHashes,
            ["clientOwnedValidationHashes"] = new JsonObject(),
            ["rollbackBackups"] = new JsonObject(),
            ["rollbackBaselineFiles"] = rollbackBaselineFiles,
            ["sourceLabel"] = "Mortal location materialization integration test",
            ["manifestPayloadHash"] = string.Empty
        };
        manifest["manifestPayloadHash"] =
            PendingTurnSnapshotTestAuthority.ComputeManifestPayloadHash(manifest);

        await WriteJsonAsync(
            "game_state/control/pending_turn_snapshot.json",
            manifest);
        await PendingTurnSnapshotTestAuthority.SyncAuthorityForCurrentManifestAsync(FileSystem);
    }

    internal async Task<IReadOnlyDictionary<string, string?>> CaptureBytesAsync(params string[] paths)
    {
        ArgumentNullException.ThrowIfNull(paths);
        var result = new Dictionary<string, string?>(StringComparer.Ordinal);
        foreach (var path in paths.Distinct(StringComparer.Ordinal))
        {
            ArgumentException.ThrowIfNullOrWhiteSpace(path);
            var bytes = await FileSystem.ReadFileBytesAsync(path);
            result[path] = bytes == null ? null : Convert.ToBase64String(bytes);
        }

        return result;
    }

    public ValueTask DisposeAsync()
    {
        if (!RootPath.StartsWith(_expectedTempRoot, StringComparison.OrdinalIgnoreCase) ||
            !Path.GetFileName(RootPath).StartsWith(
                "boe-mortal-location-materialization-",
                StringComparison.Ordinal))
        {
            throw new InvalidOperationException(
                $"Refusing to remove unexpected Mortal location test root '{RootPath}'.");
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

    private Task AfterPhysicalFilePublishedAsync(string absolutePath)
    {
        if (_armedWriteFailurePath == null ||
            !string.Equals(
                Path.GetFullPath(absolutePath),
                Path.GetFullPath(FileSystem.ResolvePath(_armedWriteFailurePath)),
                StringComparison.OrdinalIgnoreCase))
        {
            return Task.CompletedTask;
        }

        _remainingWriteFailureMatches--;
        if (_remainingWriteFailureMatches > 0)
            return Task.CompletedTask;

        InjectedPublishedPath = _armedWriteFailurePath;
        _armedWriteFailurePath = null;
        return Task.FromException(
            new IOException($"Injected Mortal location write failure for '{absolutePath}'."));
    }
}
