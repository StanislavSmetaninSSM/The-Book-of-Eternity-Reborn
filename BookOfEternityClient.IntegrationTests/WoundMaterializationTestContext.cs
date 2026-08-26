using BookOfEternityClient.Core;
using BookOfEternityClient.Services;
using Microsoft.Extensions.Logging.Abstractions;

namespace BookOfEternityClient.Tests;

/// <summary>
/// Isolated file-backed state and exact byte snapshots for wound integration tests.
/// This deliberately has no wound-schema builders: those belong to the contract-data
/// fixture so this context can be reused by lifecycle, replay, and rollback tests.
/// </summary>
internal sealed class WoundMaterializationTestContext : IAsyncDisposable
{
    internal const string PlayerWoundsPath = "game_state/player/wounds.json";
    internal const string NamedNpcWoundsPath = "game_state/npcs/npc_wounds.json";
    internal const string EnemiesPath = "game_state/combat/enemies.json";
    internal const string AlliesPath = "game_state/combat/allies.json";
    internal const string AfterlifeEntityProfilesPath =
        "game_state/meta/afterlife_entity_profiles.json";
    internal const string GuardianAbodeResidentsPath =
        "game_state/meta/guardian_abode_residents.json";
    internal const string AfterlifeSpiritualConflictPath =
        "game_state/meta/afterlife_spiritual_conflict_state.json";
    internal const string IdentityIndexPath =
        "game_state/wounds/wound_identity_index.json";
    internal const string HistoryPath = "game_state/wounds/wound_history.json";
    internal const string CommandsPath = "game_state/wounds/wound_commands.json";
    internal const string PendingResolutionsPath =
        "game_state/control/pending_wound_resolutions.json";
    internal const string ProgressionSchedulePath =
        ProgressionScheduleService.SchedulePath;
    internal const string ProgressionReportPath = ProgressionScheduleService.ReportPath;
    internal const string NarrativeOutputPath = "output/narrative_response.json";
    internal const string InterfaceUpdatesOutputPath = "output/interface_updates.json";
    internal const string DebugLogsOutputPath = "output/debug_logs.json";

    internal static readonly string[] CanonicalWoundPaths =
    {
        PlayerWoundsPath,
        NamedNpcWoundsPath,
        EnemiesPath,
        AlliesPath,
        AfterlifeEntityProfilesPath,
        GuardianAbodeResidentsPath,
        AfterlifeSpiritualConflictPath,
        IdentityIndexPath,
        HistoryPath,
        CommandsPath,
        PendingResolutionsPath,
        ProgressionSchedulePath,
        ProgressionReportPath,
        NarrativeOutputPath,
        InterfaceUpdatesOutputPath,
        DebugLogsOutputPath
    };

    private readonly string _expectedTempRoot;

    private WoundMaterializationTestContext(
        string rootPath,
        FileSystemManagerHooks? hooks)
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
        Validator = new ValidationService(FileSystem, NullLogger<ValidationService>.Instance);
        Normalizer = new CanonicalStateNormalizer(
            FileSystem,
            NullLogger<CanonicalStateNormalizer>.Instance);
    }

    internal FileSystemManager FileSystem { get; }

    internal ValidationService Validator { get; }

    internal CanonicalStateNormalizer Normalizer { get; }

    internal string RootPath { get; }

    internal static Task<WoundMaterializationTestContext> CreateAsync(
        FileSystemManagerHooks? hooks = null)
    {
        var rootPath = Path.Combine(
            Path.GetTempPath(),
            "boe-wound-materialization-" + Guid.NewGuid().ToString("N"));
        return Task.FromResult(new WoundMaterializationTestContext(rootPath, hooks));
    }

    internal static WoundTestIdentity CreateIdentity(string scenarioKey = "primary")
    {
        var key = RequireScenarioKey(scenarioKey);
        return new WoundTestIdentity(
            WoundId: $"wound_test_{key}",
            PlayerOwnerId: $"player_test_{key}",
            NamedNpcOwnerId: $"npc_test_{key}",
            EnemyCombatantId: $"enemy_test_{key}",
            AllyCombatantId: $"ally_test_{key}",
            AfterlifeActorId: $"afterlife_actor_test_{key}",
            GuardianResidentId: $"resident_test_{key}");
    }

    internal static WoundOperationCoordinates CreateOperationCoordinates(
        string scenarioKey = "primary",
        int turn = 42)
    {
        var key = RequireScenarioKey(scenarioKey);
        if (turn < 1)
            throw new ArgumentOutOfRangeException(nameof(turn));

        return new WoundOperationCoordinates(
            SessionId: $"session_wound_{key}",
            RequestId: $"request_wound_{key}",
            EventRef: $"turn_{turn}:event:wound_{key}",
            OperationKey: $"turn_{turn}:wound:{key}",
            AttemptId: $"attempt_wound_{key}",
            CycleKey: $"cycle_{turn}_{key}",
            Turn: turn);
    }

    internal Task WriteExactBytesAsync(string relativePath, byte[] bytes)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(relativePath);
        ArgumentNullException.ThrowIfNull(bytes);
        return FileSystem.WriteFileAtomicBytesAsync(relativePath, bytes.ToArray());
    }

    internal Task DeleteAsync(string relativePath)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(relativePath);
        FileSystem.DeleteFile(relativePath);
        return Task.CompletedTask;
    }

    internal async Task<IReadOnlyDictionary<string, WoundFileBeforeImage>>
        CaptureBeforeImagesAsync(params string[] paths)
    {
        ArgumentNullException.ThrowIfNull(paths);
        var result = new Dictionary<string, WoundFileBeforeImage>(StringComparer.Ordinal);
        foreach (var path in paths.Distinct(StringComparer.Ordinal))
        {
            ArgumentException.ThrowIfNullOrWhiteSpace(path);
            var bytes = await FileSystem.ReadFileBytesAsync(path);
            result.Add(path, WoundFileBeforeImage.FromRead(bytes));
        }

        return result;
    }

    internal Task<IReadOnlyDictionary<string, WoundFileBeforeImage>>
        CaptureCanonicalBeforeImagesAsync() =>
        CaptureBeforeImagesAsync(CanonicalWoundPaths);

    internal async Task AssertBeforeImagesUnchangedAsync(
        IReadOnlyDictionary<string, WoundFileBeforeImage> beforeImages)
    {
        ArgumentNullException.ThrowIfNull(beforeImages);
        foreach (var (path, beforeImage) in beforeImages)
        {
            ArgumentException.ThrowIfNullOrWhiteSpace(path);
            ArgumentNullException.ThrowIfNull(beforeImage);
            var actual = await FileSystem.ReadFileBytesAsync(path);
            beforeImage.AssertMatches(path, actual);
        }
    }

    public ValueTask DisposeAsync()
    {
        if (!RootPath.StartsWith(_expectedTempRoot, StringComparison.OrdinalIgnoreCase) ||
            !Path.GetFileName(RootPath).StartsWith(
                "boe-wound-materialization-",
                StringComparison.Ordinal))
        {
            throw new InvalidOperationException(
                $"Refusing to remove unexpected wound test root '{RootPath}'.");
        }

        try
        {
            if (Directory.Exists(RootPath))
                Directory.Delete(RootPath, recursive: true);
        }
        catch (IOException)
        {
            // An isolated root with an open handle can be reclaimed by a later test run.
        }
        catch (UnauthorizedAccessException)
        {
            // Cleanup remains best-effort without hiding an assertion failure.
        }

        return ValueTask.CompletedTask;
    }

    private static string RequireScenarioKey(string scenarioKey)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(scenarioKey);
        if (scenarioKey.Any(static character =>
                !(char.IsAsciiLetterOrDigit(character) || character == '_')))
        {
            throw new ArgumentException(
                "Scenario keys may contain only ASCII letters, digits, and underscores.",
                nameof(scenarioKey));
        }

        return scenarioKey.ToLowerInvariant();
    }
}

internal sealed record WoundTestIdentity(
    string WoundId,
    string PlayerOwnerId,
    string NamedNpcOwnerId,
    string EnemyCombatantId,
    string AllyCombatantId,
    string AfterlifeActorId,
    string GuardianResidentId);

internal sealed record WoundOperationCoordinates(
    string SessionId,
    string RequestId,
    string EventRef,
    string OperationKey,
    string AttemptId,
    string CycleKey,
    int Turn);

internal sealed class WoundFileBeforeImage
{
    private WoundFileBeforeImage(bool existed, byte[]? bytes)
    {
        Existed = existed;
        Bytes = bytes?.ToArray();
    }

    internal bool Existed { get; }

    internal byte[]? Bytes { get; }

    internal static WoundFileBeforeImage FromRead(byte[]? bytes) =>
        new(bytes != null, bytes);

    internal void AssertMatches(string path, byte[]? actualBytes)
    {
        if (Existed != (actualBytes != null))
        {
            throw new InvalidOperationException(
                $"Exact before-image existence changed for '{path}'. Expected exists={Existed}; " +
                $"actual exists={actualBytes != null}.");
        }

        if (Existed && !Bytes!.AsSpan().SequenceEqual(actualBytes))
        {
            throw new InvalidOperationException(
                $"Exact before-image bytes changed for '{path}'.");
        }
    }
}
