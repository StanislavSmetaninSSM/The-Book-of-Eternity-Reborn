using System.IO.Compression;
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

public sealed class MortalCommandDisplaySaveTests : IDisposable
{
    private const string SaveFileName = "mortal_world_command_display_fixture.zip";
    private const string SaveName = "Mortal World Command Display Fixture (#1095)";
    private const string SaveRelativePath = "saves/manual_saves/" + SaveFileName;

    private readonly string _rootPath = Path.Combine(Path.GetTempPath(), "boe-mortal-command-save-" + Guid.NewGuid().ToString("N"));

    [Fact]
    public async Task NamedMortalCommandDisplaySave_IsDiscoverableLoadableValidAndRepeatable()
    {
        var sourceArchive = GetSourceArchivePath();
        Assert.True(File.Exists(sourceArchive), $"Missing reusable Mortal World command display save: {sourceArchive}");

        using (var archive = ZipFile.OpenRead(sourceArchive))
        {
            Assert.NotNull(archive.GetEntry("save_metadata.json"));
            Assert.NotNull(archive.GetEntry("game_state/meta/soul_state.json"));
            Assert.NotNull(archive.GetEntry("game_state/inventory/items.json"));
            Assert.DoesNotContain(archive.Entries, static entry =>
                entry.FullName.StartsWith("game_session/", StringComparison.OrdinalIgnoreCase) ||
                entry.FullName.StartsWith("saves/", StringComparison.OrdinalIgnoreCase));
        }

        var sourceHashBefore = await ComputeSha256Async(sourceArchive);
        var loadRoot = CreateIsolatedRoot();
        var fs = new FileSystemManager(loadRoot, NullLogger<FileSystemManager>.Instance);
        fs.EnsureDirectoryStructure();
        CopyCleanCheckoutDependencies(loadRoot);
        var savePath = fs.ResolvePath(SaveRelativePath);
        Directory.CreateDirectory(Path.GetDirectoryName(savePath)!);
        File.Copy(sourceArchive, savePath, overwrite: true);

        var stateManager = new StateManager(fs, new GameSettings(), NullLogger<StateManager>.Instance);
        await stateManager.RefreshGameStateAsync();
        var saveLoad = new SaveLoadService(fs, stateManager, NullLogger<SaveLoadService>.Instance);

        var availableSaves = await saveLoad.GetAvailableSavesAsync("saves/manual_saves");
        var displaySave = Assert.Single(availableSaves, save => Path.GetFileName(save.FileName) == SaveFileName);
        Assert.Equal(SaveName, displaySave.Metadata?.SaveName);

        Assert.True(await saveLoad.LoadGameAsync(sourceArchive));
        await stateManager.RefreshGameStateAsync();
        Assert.Equal("Mortal World", stateManager.CurrentState.CurrentRealm);

        var issues = await new ValidationService(fs, NullLogger<ValidationService>.Instance).ValidateGameStateAsync();
        var blockingIssues = issues.Where(static issue => issue.Severity == IssueSeverity.Error).ToArray();
        Assert.True(
            blockingIssues.Length == 0,
            "Loaded reusable Mortal command display save has blocking validation issues:" +
            Environment.NewLine + string.Join(Environment.NewLine, blockingIssues.Select(static issue => issue.ToString())));

        Assert.Equal(sourceHashBefore, await ComputeSha256Async(sourceArchive));
        Assert.True(await saveLoad.LoadGameAsync(sourceArchive));
        Assert.Equal(sourceHashBefore, await ComputeSha256Async(sourceArchive));
    }

    [Theory]
    [MemberData(nameof(CoveredMortalCommandInvocations))]
    public async Task LoadedMortalCommandDisplaySave_RendersCoveredCommandInBrowserAndConsole(
        string commandId,
        string command)
    {
        var result = await ExecuteFromLoadedSaveAsync(command);

        var report = ConsoleCommandOutputQualityClassifier.Classify(result);
        Assert.True(
            report.IsUsablePlayerOutput,
            $"{commandId} ({command}) returned unusable player-facing output from the reusable Mortal save:" +
            Environment.NewLine + string.Join(Environment.NewLine, report.Violations));

        var console = new TestExplorerConsole();
        var renderException = Record.Exception(() => ExplorerCommandResultConsoleRenderer.Render(console, result));
        Assert.Null(renderException);
        Assert.NotEmpty(console.Rendered);
    }

    [Theory]
    [MemberData(nameof(MortalWorldNewsFixtureInvocations))]
    public async Task LoadedMortalCommandDisplaySave_WorldNewsLocalizesVisibilityEnums(
        string command,
        string expectedText,
        string rawVisibility)
    {
        var result = await ExecuteFromLoadedSaveAsync(command);

        Assert.Equal(CommandExecutionState.Completed, result.State);
        var visibleText = ConsoleCommandOutputQualityClassifier.Classify(result).VisibleText;
        Assert.Contains(expectedText, visibleText, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain(rawVisibility, visibleText, StringComparison.OrdinalIgnoreCase);
    }

    public static IEnumerable<object[]> CoveredMortalCommandInvocations()
    {
        foreach (var descriptor in ExplorerCommandCatalog.Descriptors
                     .Where(static descriptor => descriptor.Group == ExplorerCommandGroup.MortalWorld)
                     .OrderBy(static descriptor => descriptor.Id, StringComparer.OrdinalIgnoreCase))
        {
            foreach (var alias in descriptor.Aliases)
                yield return [descriptor.Id, alias];
        }

        foreach (var (id, command) in PracticalUniversalMortalPreviewCommands())
            yield return [id, command];
    }

    public static IEnumerable<object[]> MortalWorldNewsFixtureInvocations()
    {
        yield return ["/новости_мира", "Письмо появилось ночью", "local"];
        yield return ["/новости_мира", "Слухи в купеческом квартале", "rumor"];
        yield return ["/новости_мира событие world_event_valmont_letter", "местные новости", "local"];
        yield return ["/новости_мира событие world_event_market_whispers", "слух", "rumor"];
    }

    public void Dispose()
    {
        try
        {
            if (Directory.Exists(_rootPath))
                Directory.Delete(_rootPath, recursive: true);
        }
        catch
        {
            // ignore temp cleanup failures
        }
    }

    private string CreateIsolatedRoot()
    {
        var root = Path.Combine(_rootPath, Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(root);
        return root;
    }

    private static string GetSourceArchivePath() =>
        Path.Combine(TestRepoPaths.BaseSessionRoot, "saves", "manual_saves", SaveFileName);

    private async Task<ExplorerCommandResult> ExecuteFromLoadedSaveAsync(string command)
    {
        var sourceArchive = GetSourceArchivePath();
        Assert.True(File.Exists(sourceArchive), $"Missing reusable Mortal World command display save: {sourceArchive}");

        var loadRoot = CreateIsolatedRoot();
        var fs = new FileSystemManager(loadRoot, NullLogger<FileSystemManager>.Instance);
        fs.EnsureDirectoryStructure();
        CopyCleanCheckoutDependencies(loadRoot);
        var savePath = fs.ResolvePath(SaveRelativePath);
        Directory.CreateDirectory(Path.GetDirectoryName(savePath)!);
        File.Copy(sourceArchive, savePath, overwrite: true);

        var stateManager = new StateManager(fs, new GameSettings(), NullLogger<StateManager>.Instance);
        await stateManager.RefreshGameStateAsync();
        var saveLoad = new SaveLoadService(fs, stateManager, NullLogger<SaveLoadService>.Instance);
        Assert.True(await saveLoad.LoadGameAsync(savePath));
        await stateManager.LoadSettingsAsync();
        await stateManager.RefreshGameStateAsync();

        var validation = new ValidationService(fs, NullLogger<ValidationService>.Instance);
        var service = new ExplorerWebCommandService(fs, stateManager, new LocalizationManager(), validation);
        return await service.ExecuteAsync(new ExplorerWebCommandRequest(command, AdvancedEnabled: false));
    }

    private static void CopyCleanCheckoutDependencies(string loadRoot)
    {
        CopyDirectory(
            Path.Combine(TestRepoPaths.RepoRoot, "BookOfEternityClient", "system_guardians"),
            Path.Combine(loadRoot, "system_guardians"));
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

    private static async Task<string> ComputeSha256Async(string path)
    {
        await using var stream = File.OpenRead(path);
        var hash = await SHA256.HashDataAsync(stream);
        return Convert.ToHexString(hash);
    }

    private static IEnumerable<(string Id, string Command)> PracticalUniversalMortalPreviewCommands()
    {
        yield return ("help", "/help");
        yield return ("status", "/статус");
        yield return ("soul", "/душа");
        yield return ("achievements", "/достижения");
        yield return ("chronicle", "/хроника");
        yield return ("story", "/story");
        yield return ("behavior", "/поведение");
        yield return ("lives", "/жизни");
        yield return ("feathers", "/перья");
        yield return ("codex", "/кодекс");
        yield return ("world_rules", "/правила_мира");
        yield return ("gallery", "/галерея");
        yield return ("mods", "/моды");
        yield return ("validate", "/валидация");
    }
}
