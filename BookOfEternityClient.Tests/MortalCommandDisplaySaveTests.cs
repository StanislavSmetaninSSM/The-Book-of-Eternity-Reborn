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
        var result = await service.ExecuteAsync(new ExplorerWebCommandRequest(command, AdvancedEnabled: false));

        var violations = CollectDisplayViolations(result);
        Assert.True(
            violations.Count == 0,
            $"{commandId} ({command}) returned unusable player-facing output from the reusable Mortal save:" +
            Environment.NewLine + string.Join(Environment.NewLine, violations));

        var console = new TestExplorerConsole();
        var renderException = Record.Exception(() => ExplorerCommandResultConsoleRenderer.Render(console, result));
        Assert.Null(renderException);
        Assert.NotEmpty(console.Rendered);
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

    private static IReadOnlyList<string> CollectDisplayViolations(ExplorerCommandResult result)
    {
        var violations = new List<string>();
        if (result.State is CommandExecutionState.Failed or CommandExecutionState.Blocked)
            violations.Add($"state is {result.State}");

        if (result.Blocks.Count == 0 && result.Actions.Count == 0 && result.Prompts.Count == 0)
            violations.Add("result has no visible blocks, actions, or prompts");

        if (result.Blocks.OfType<UiRawJsonBlock>().Any())
            violations.Add("default output contains raw JSON block");

        var visibleText = CollectVisibleText(result);
        if (string.IsNullOrWhiteSpace(visibleText))
            violations.Add("default output has no readable text");

        foreach (var forbidden in new[]
                 {
                     "game_state/",
                     ".json",
                     "DTO",
                     "API",
                     "endpoint",
                     "debug_logs",
                     "exception",
                     "UiRawJsonBlock",
                     "JsonObject",
                     "JsonArray",
                     "JsonValue"
                 })
        {
            if (visibleText.Contains(forbidden, StringComparison.OrdinalIgnoreCase))
                violations.Add($"visible text leaks technical marker: {forbidden}");
        }

        return violations;
    }

    private static string CollectVisibleText(ExplorerCommandResult result)
    {
        var parts = new List<string>();
        foreach (var block in result.Blocks)
            CollectBlockText(block, parts);

        parts.AddRange(result.Actions.Select(static action => action.Label));

        foreach (var prompt in result.Prompts)
        {
            parts.Add(prompt.Prompt);
            switch (prompt)
            {
                case UiTextInputPrompt textInput:
                    parts.Add(textInput.Placeholder);
                    break;
                case UiSelectionPrompt selection:
                    parts.AddRange(selection.Options.Select(static option => option.Label));
                    parts.AddRange(selection.Options.Select(static option => option.Description));
                    break;
            }
        }

        foreach (var notification in result.Notifications)
        {
            parts.Add(notification.Title);
            parts.Add(notification.Message);
        }

        return string.Join("\n", parts.Where(static part => !string.IsNullOrWhiteSpace(part)));
    }

    private static void CollectBlockText(UiBlock block, List<string> parts)
    {
        switch (block)
        {
            case UiTextBlock text:
                parts.Add(text.Text);
                break;
            case UiPanelBlock panel:
                parts.Add(panel.Title);
                foreach (var child in panel.Blocks)
                    CollectBlockText(child, parts);
                break;
            case UiTableBlock table:
                parts.Add(table.Title);
                parts.AddRange(table.Columns);
                parts.AddRange(table.Rows.SelectMany(static row => row.Cells));
                break;
            case UiListBlock list:
                parts.AddRange(list.Items);
                break;
            case UiKeyValueGridBlock grid:
                parts.AddRange(grid.Items.SelectMany(static item => new[] { item.Key, item.Value }));
                break;
            case UiMessageBlock message:
                parts.Add(message.Title);
                parts.Add(message.Message);
                break;
            case UiRawJsonBlock raw:
                parts.Add(raw.Title);
                break;
        }
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
