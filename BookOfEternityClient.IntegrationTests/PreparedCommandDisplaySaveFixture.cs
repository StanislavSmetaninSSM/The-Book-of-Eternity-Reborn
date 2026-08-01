using BookOfEternityClient.Configuration;
using BookOfEternityClient.Core;
using BookOfEternityClient.Services;
using Microsoft.Extensions.Logging.Abstractions;
using Xunit;

namespace BookOfEternityClient.Tests;

public abstract class PreparedCommandDisplaySaveFixture : IAsyncLifetime
{
    private readonly string _saveFileName;
    private readonly string _saveRelativePath;
    private readonly string _templateRootPath;
    private readonly Lazy<Task<string>> _templateRoot;

    protected PreparedCommandDisplaySaveFixture(string saveFileName, string templateRootPrefix)
    {
        _saveFileName = saveFileName;
        _saveRelativePath = "saves/manual_saves/" + saveFileName;
        _templateRootPath = Path.Combine(
            Path.GetTempPath(),
            templateRootPrefix + Guid.NewGuid().ToString("N"));
        _templateRoot = new Lazy<Task<string>>(
            PrepareTemplateAsync,
            LazyThreadSafetyMode.ExecutionAndPublication);
    }

    public Task InitializeAsync() => Task.CompletedTask;

    public async Task ClonePreparedTemplateAsync(string caseRoot)
    {
        var templateRoot = await _templateRoot.Value;
        CopyDirectory(templateRoot, caseRoot);
    }

    public async Task DisposeAsync()
    {
        if (!_templateRoot.IsValueCreated)
            return;

        try
        {
            await _templateRoot.Value;
        }
        finally
        {
            DeleteOwnedRoot(_templateRootPath);
        }
    }

    private async Task<string> PrepareTemplateAsync()
    {
        Directory.CreateDirectory(_templateRootPath);

        try
        {
            var fileSystem = new FileSystemManager(
                _templateRootPath,
                NullLogger<FileSystemManager>.Instance);
            fileSystem.EnsureDirectoryStructure();
            CopyCleanCheckoutDependencies(_templateRootPath);

            var sourceArchive = Path.Combine(
                TestRepoPaths.BaseSessionRoot,
                "saves",
                "manual_saves",
                _saveFileName);
            if (!File.Exists(sourceArchive))
                throw new FileNotFoundException("Missing reusable command-display save.", sourceArchive);

            var savePath = fileSystem.ResolvePath(_saveRelativePath);
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
                throw new InvalidOperationException($"Could not prepare command-display save '{_saveFileName}'.");

            await stateManager.LoadSettingsAsync();
            await stateManager.RefreshGameStateAsync();
            return _templateRootPath;
        }
        catch
        {
            DeleteOwnedRoot(_templateRootPath);
            throw;
        }
    }

    private static void CopyCleanCheckoutDependencies(string templateRoot)
    {
        CopyDirectory(
            Path.Combine(TestRepoPaths.RepoRoot, "BookOfEternityClient", "system_guardians"),
            Path.Combine(templateRoot, "system_guardians"));
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

    private void DeleteOwnedRoot(string rootPath)
    {
        var ownedRoot = Path.GetFullPath(_templateRootPath)
            .TrimEnd(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar);
        var candidate = Path.GetFullPath(rootPath)
            .TrimEnd(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar);
        if (!string.Equals(ownedRoot, candidate, StringComparison.OrdinalIgnoreCase))
            throw new InvalidOperationException($"Refusing to delete unowned template root '{candidate}'.");

        if (Directory.Exists(candidate))
            Directory.Delete(candidate, recursive: true);
    }
}

public sealed class MortalPreparedCommandDisplaySaveFixture : PreparedCommandDisplaySaveFixture
{
    public MortalPreparedCommandDisplaySaveFixture()
        : base("mortal_world_command_display_fixture.zip", "boe-mortal-command-template-")
    {
    }
}

public sealed class ChaosSeaPreparedCommandDisplaySaveFixture : PreparedCommandDisplaySaveFixture
{
    public ChaosSeaPreparedCommandDisplaySaveFixture()
        : base("chaos_sea_command_display_fixture.zip", "boe-chaos-sea-command-template-")
    {
    }
}

public sealed class ShiningAbodePreparedCommandDisplaySaveFixture : PreparedCommandDisplaySaveFixture
{
    public ShiningAbodePreparedCommandDisplaySaveFixture()
        : base("shining_abode_command_display_fixture.zip", "boe-shining-abode-command-template-")
    {
    }
}
