using System.Text.Json.Nodes;
using BookOfEternityClient.Core;
using BookOfEternityClient.Services;
using Microsoft.Extensions.Logging.Abstractions;

namespace BookOfEternityClient.Tests;

internal sealed class EffectMaterializationTestContext : IAsyncDisposable
{
    internal const string PlayerEffectsPath = "game_state/player/effects.json";
    internal const string NpcEffectsPath = "game_state/npcs/npc_effects.json";
    internal const string EnemyCombatantsPath = "game_state/combat/enemies.json";
    internal const string AllyCombatantsPath = "game_state/combat/allies.json";
    internal const string AfterlifeProfilesPath = "game_state/meta/afterlife_entity_profiles.json";
    internal const string SpiritualConflictPath = "game_state/meta/afterlife_spiritual_conflict_state.json";
    internal const string IdentityIndexPath = "game_state/effects/effect_identity_index.json";
    internal const string CommandPath = "game_state/effects/effect_commands.json";
    internal const string PendingResolutionPath = "game_state/control/pending_effect_resolutions.json";

    internal static readonly string[] OwnedPaths =
    {
        PlayerEffectsPath,
        NpcEffectsPath,
        EnemyCombatantsPath,
        AllyCombatantsPath,
        AfterlifeProfilesPath,
        SpiritualConflictPath,
        IdentityIndexPath,
        CommandPath,
        PendingResolutionPath
    };

    private readonly string _expectedTempRoot;

    private EffectMaterializationTestContext(string rootPath)
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

    internal static Task<EffectMaterializationTestContext> CreateAsync()
    {
        var rootPath = Path.Combine(
            Path.GetTempPath(),
            "boe-effect-materialization-" + Guid.NewGuid().ToString("N"));
        return Task.FromResult(new EffectMaterializationTestContext(rootPath));
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

    internal async Task CaptureValidatedPendingSnapshotAsync(int turn = 42)
    {
        const string sessionId = "session_effect_materialization";
        const string requestId = "request_effect_materialization";
        const string playerAction = "Validate complete effect materialization.";

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
            .Concat(OwnedPaths)
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
            ["requestTimestamp"] = "2026-08-14T00:00:00Z",
            ["playerAction"] = playerAction,
            ["files"] = files,
            ["snapshotFileHashes"] = snapshotFileHashes,
            ["clientOwnedValidationHashes"] = new JsonObject(),
            ["rollbackBackups"] = new JsonObject(),
            ["rollbackBaselineFiles"] = rollbackBaselineFiles,
            ["sourceLabel"] = "Effect materialization integration test",
            ["manifestPayloadHash"] = string.Empty
        };
        manifest["manifestPayloadHash"] =
            PendingTurnSnapshotTestAuthority.ComputeManifestPayloadHash(manifest);

        await WriteJsonAsync(
            "game_state/control/pending_turn_snapshot.json",
            manifest);
        await PendingTurnSnapshotTestAuthority.SyncAuthorityForCurrentManifestAsync(FileSystem);
    }

    internal async Task<IReadOnlyDictionary<string, string?>> CaptureBytesAsync(
        params string[] paths)
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
                "boe-effect-materialization-",
                StringComparison.Ordinal))
        {
            throw new InvalidOperationException(
                $"Refusing to remove unexpected effect test root '{RootPath}'.");
        }

        try
        {
            if (Directory.Exists(RootPath))
                Directory.Delete(RootPath, recursive: true);
        }
        catch (IOException)
        {
            // A later run can reclaim an isolated temp root left by an open handle.
        }
        catch (UnauthorizedAccessException)
        {
            // Keep cleanup best-effort without hiding test assertions.
        }

        return ValueTask.CompletedTask;
    }
}
