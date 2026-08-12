using System.Security.Cryptography;
using BookOfEternityClient.CommandProtocol;
using BookOfEternityClient.Configuration;
using BookOfEternityClient.Core;
using BookOfEternityClient.Services;
using BookOfEternityClient.UI;
using BookOfEternityClient.WebUi;
using Microsoft.Extensions.Logging.Abstractions;
using Xunit;

namespace BookOfEternityClient.Tests;

public sealed class BrowserCommandPresentationAuditFixture : IAsyncLifetime
{
    private const string MortalSave = "mortal_world_command_display_fixture.zip";
    private const string ChaosSeaSave = "chaos_sea_command_display_fixture.zip";
    private const string ShiningAbodeSave = "shining_abode_command_display_fixture.zip";

    private readonly string _fixtureRootPath = Path.Combine(
        Path.GetTempPath(),
        "boe-browser-command-presentation-fixture-" + Guid.NewGuid().ToString("N"));
    private readonly Lazy<Task<PreparedSaveContext>> _mortalContext;
    private readonly Lazy<Task<PreparedSaveContext>> _chaosSeaContext;
    private readonly Lazy<Task<PreparedSaveContext>> _shiningAbodeContext;

    public BrowserCommandPresentationAuditFixture()
    {
        _mortalContext = CreateLazyContext(MortalSave, "mortal");
        _chaosSeaContext = CreateLazyContext(ChaosSeaSave, "chaos-sea");
        _shiningAbodeContext = CreateLazyContext(ShiningAbodeSave, "shining-abode");
    }

    public Task InitializeAsync() => Task.CompletedTask;

    public async Task<ExplorerCommandResult> ExecuteBrowserCommandAsync(
        string saveFileName,
        string command)
    {
        var prepared = await GetPreparedContextAsync(saveFileName);
        var invocation = await CreateStateManagerAsync(prepared);
        var validation = new ValidationService(
            invocation.FileSystem,
            NullLogger<ValidationService>.Instance);
        var service = new ExplorerWebCommandService(
            invocation.FileSystem,
            invocation.StateManager,
            new LocalizationManager(),
            validation);

        try
        {
            return await service.ExecuteAsync(
                new ExplorerWebCommandRequest(command, AdvancedEnabled: false));
        }
        finally
        {
            DeleteOwnedCaseRoot(invocation.RootPath);
        }
    }

    public async Task<TResult> ExecuteConsoleCommandAsync<TResult>(
        string saveFileName,
        Func<FileSystemManager, StateManager, Task<TResult>> executeAsync)
    {
        ArgumentNullException.ThrowIfNull(executeAsync);

        var prepared = await GetPreparedContextAsync(saveFileName);
        var invocation = await CreateStateManagerAsync(prepared);
        try
        {
            return await executeAsync(invocation.FileSystem, invocation.StateManager);
        }
        finally
        {
            DeleteOwnedCaseRoot(invocation.RootPath);
        }
    }

    public async Task DisposeAsync()
    {
        try
        {
            await VerifyPreparedRootsUnchangedAsync();
        }
        finally
        {
            DeleteOwnedFixtureRoot();
        }
    }

    private Lazy<Task<PreparedSaveContext>> CreateLazyContext(
        string saveFileName,
        string directoryName)
    {
        return new Lazy<Task<PreparedSaveContext>>(
            () => PrepareSaveContextAsync(saveFileName, directoryName),
            LazyThreadSafetyMode.ExecutionAndPublication);
    }

    private Task<PreparedSaveContext> GetPreparedContextAsync(string saveFileName)
    {
        return saveFileName switch
        {
            MortalSave => _mortalContext.Value,
            ChaosSeaSave => _chaosSeaContext.Value,
            ShiningAbodeSave => _shiningAbodeContext.Value,
            _ => throw new ArgumentOutOfRangeException(
                nameof(saveFileName),
                saveFileName,
                "The browser presentation audit has no prepared context for this save.")
        };
    }

    private async Task<PreparedSaveContext> PrepareSaveContextAsync(
        string saveFileName,
        string directoryName)
    {
        var rootPath = Path.Combine(_fixtureRootPath, directoryName);
        Directory.CreateDirectory(rootPath);

        var fileSystem = new FileSystemManager(
            rootPath,
            NullLogger<FileSystemManager>.Instance);
        fileSystem.EnsureDirectoryStructure();
        CopyDirectory(
            Path.Combine(TestRepoPaths.RepoRoot, "BookOfEternityClient", "system_guardians"),
            Path.Combine(rootPath, "system_guardians"));

        var sourceArchive = Path.Combine(
            TestRepoPaths.BaseSessionRoot,
            "saves",
            "manual_saves",
            saveFileName);
        if (!File.Exists(sourceArchive))
            throw new FileNotFoundException("Missing reusable command-display save.", sourceArchive);

        var savePath = fileSystem.ResolvePath("saves/manual_saves/" + saveFileName);
        Directory.CreateDirectory(Path.GetDirectoryName(savePath)!);
        File.Copy(sourceArchive, savePath, overwrite: true);

        var stateManager = new StateManager(
            fileSystem,
            new GameSettings(),
            NullLogger<StateManager>.Instance);
        await stateManager.RefreshGameStateAsync();
        var saveLoad = new SaveLoadService(
            fileSystem,
            stateManager,
            NullLogger<SaveLoadService>.Instance);
        if (!await saveLoad.LoadGameAsync(savePath))
            throw new InvalidOperationException($"Could not load command-display save '{saveFileName}'.");

        await stateManager.LoadSettingsAsync();
        await stateManager.RefreshGameStateAsync();

        return new PreparedSaveContext(
            rootPath,
            await CaptureFileHashesAsync(rootPath));
    }

    private async Task<StateManagerContext> CreateStateManagerAsync(PreparedSaveContext prepared)
    {
        var rootPath = Path.Combine(
            _fixtureRootPath,
            "cases",
            Guid.NewGuid().ToString("N"));
        CopyDirectory(prepared.RootPath, rootPath);

        var fileSystem = new FileSystemManager(
            rootPath,
            NullLogger<FileSystemManager>.Instance);
        var stateManager = new StateManager(
            fileSystem,
            new GameSettings(),
            NullLogger<StateManager>.Instance);
        await stateManager.LoadSettingsAsync();
        await stateManager.RefreshGameStateAsync();
        return new StateManagerContext(rootPath, fileSystem, stateManager);
    }

    private async Task VerifyPreparedRootsUnchangedAsync()
    {
        foreach (var lazyContext in CreatedContexts())
        {
            var prepared = await lazyContext.Value;
            var currentHashes = await CaptureFileHashesAsync(prepared.RootPath);
            var differences = DescribeHashDifferences(prepared.FileHashes, currentHashes);
            if (differences.Count > 0)
            {
                throw new InvalidOperationException(
                    "A read-only browser presentation command changed its prepared save root:" +
                    Environment.NewLine +
                    string.Join(Environment.NewLine, differences));
            }
        }
    }

    private IEnumerable<Lazy<Task<PreparedSaveContext>>> CreatedContexts()
    {
        if (_mortalContext.IsValueCreated)
            yield return _mortalContext;
        if (_chaosSeaContext.IsValueCreated)
            yield return _chaosSeaContext;
        if (_shiningAbodeContext.IsValueCreated)
            yield return _shiningAbodeContext;
    }

    private static async Task<IReadOnlyDictionary<string, string>> CaptureFileHashesAsync(
        string rootPath)
    {
        var hashes = new Dictionary<string, string>(StringComparer.Ordinal);
        foreach (var filePath in Directory
                     .EnumerateFiles(rootPath, "*", SearchOption.AllDirectories)
                     .Order(StringComparer.Ordinal))
        {
            await using var stream = File.OpenRead(filePath);
            var hash = await SHA256.HashDataAsync(stream);
            var relativePath = Path.GetRelativePath(rootPath, filePath)
                .Replace(Path.DirectorySeparatorChar, '/');
            hashes.Add(relativePath, Convert.ToHexString(hash));
        }

        return hashes;
    }

    private static IReadOnlyList<string> DescribeHashDifferences(
        IReadOnlyDictionary<string, string> expected,
        IReadOnlyDictionary<string, string> actual)
    {
        var differences = new List<string>();
        foreach (var path in expected.Keys.Union(actual.Keys).Order(StringComparer.Ordinal))
        {
            if (!expected.TryGetValue(path, out var expectedHash))
                differences.Add($"added: {path}");
            else if (!actual.TryGetValue(path, out var actualHash))
                differences.Add($"removed: {path}");
            else if (!string.Equals(expectedHash, actualHash, StringComparison.Ordinal))
                differences.Add($"changed: {path}");
        }

        return differences;
    }

    private static void CopyDirectory(string source, string destination)
    {
        Directory.CreateDirectory(destination);
        foreach (var directory in Directory.GetDirectories(source, "*", SearchOption.AllDirectories))
        {
            var target = Path.Combine(destination, Path.GetRelativePath(source, directory));
            Directory.CreateDirectory(target);
        }

        foreach (var file in Directory.GetFiles(source, "*", SearchOption.AllDirectories))
        {
            var target = Path.Combine(destination, Path.GetRelativePath(source, file));
            Directory.CreateDirectory(Path.GetDirectoryName(target)!);
            File.Copy(file, target, overwrite: true);
        }
    }

    private void DeleteOwnedFixtureRoot()
    {
        var candidate = Path.GetFullPath(_fixtureRootPath)
            .TrimEnd(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar);
        var expectedPrefix = Path.GetFullPath(Path.GetTempPath())
            .TrimEnd(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar) +
            Path.DirectorySeparatorChar +
            "boe-browser-command-presentation-fixture-";

        if (!candidate.StartsWith(expectedPrefix, StringComparison.OrdinalIgnoreCase))
            throw new InvalidOperationException($"Refusing to delete unowned fixture root '{candidate}'.");

        if (Directory.Exists(candidate))
            Directory.Delete(candidate, recursive: true);
    }

    private void DeleteOwnedCaseRoot(string rootPath)
    {
        var candidate = Path.GetFullPath(rootPath)
            .TrimEnd(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar);
        var caseRootPrefix = Path.GetFullPath(Path.Combine(_fixtureRootPath, "cases"))
            .TrimEnd(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar) +
            Path.DirectorySeparatorChar;

        if (!candidate.StartsWith(caseRootPrefix, StringComparison.OrdinalIgnoreCase))
            throw new InvalidOperationException($"Refusing to delete unowned audit case root '{candidate}'.");

        if (Directory.Exists(candidate))
            Directory.Delete(candidate, recursive: true);
    }

    private sealed record PreparedSaveContext(
        string RootPath,
        IReadOnlyDictionary<string, string> FileHashes);

    private sealed record StateManagerContext(
        string RootPath,
        FileSystemManager FileSystem,
        StateManager StateManager);
}
