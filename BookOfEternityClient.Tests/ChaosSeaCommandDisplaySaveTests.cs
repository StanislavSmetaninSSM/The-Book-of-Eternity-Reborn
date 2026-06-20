using System.IO.Compression;
using System.Security.Cryptography;
using System.Text.Json;
using BookOfEternityClient.CommandProtocol;
using BookOfEternityClient.Configuration;
using BookOfEternityClient.Core;
using BookOfEternityClient.Services;
using BookOfEternityClient.UI;
using BookOfEternityClient.WebUi;
using Microsoft.Extensions.Logging.Abstractions;
using Xunit;

namespace BookOfEternityClient.Tests;

public sealed class ChaosSeaCommandDisplaySaveTests : IDisposable
{
    private const string SaveFileName = "chaos_sea_command_display_fixture.zip";
    private const string MetadataFileName = "chaos_sea_command_display_fixture_metadata.json";
    private const string SaveName = "Chaos Sea Command Display Fixture (#1096)";
    private const string SaveRelativePath = "saves/manual_saves/" + SaveFileName;

    private readonly string _rootPath = Path.Combine(Path.GetTempPath(), "boe-chaos-sea-command-save-" + Guid.NewGuid().ToString("N"));

    [Fact]
    public async Task NamedChaosSeaCommandDisplaySave_IsDiscoverableLoadableValidAndRepeatable()
    {
        var sourceArchive = GetSourceArchivePath();
        var sourceMetadata = GetSourceMetadataPath();
        Assert.True(File.Exists(sourceArchive), $"Missing reusable Chaos Sea command display save: {sourceArchive}");
        Assert.True(File.Exists(sourceMetadata), $"Missing reusable Chaos Sea command display save metadata: {sourceMetadata}");

        using (var metadata = JsonDocument.Parse(await File.ReadAllTextAsync(sourceMetadata)))
        {
            Assert.Equal(SaveName, metadata.RootElement.GetProperty("saveName").GetString());
            Assert.Equal(SaveFileName, metadata.RootElement.GetProperty("archiveFile").GetString());
            Assert.Equal("Chaos Sea", metadata.RootElement.GetProperty("currentRealm").GetString());
            Assert.Contains(
                metadata.RootElement.GetProperty("sourceIssues").EnumerateArray(),
                issue => string.Equals(
                    issue.GetString(),
                    "https://github.com/StanislavSmetaninSSM/The-Book-of-Eternity-Reborn/issues/1096",
                    StringComparison.OrdinalIgnoreCase));
        }

        using (var archive = ZipFile.OpenRead(sourceArchive))
        {
            Assert.NotNull(archive.GetEntry("save_metadata.json"));
            Assert.NotNull(archive.GetEntry("game_state/meta/soul_state.json"));
            Assert.NotNull(archive.GetEntry("game_state/meta/guardians.json"));
            Assert.NotNull(archive.GetEntry("game_state/meta/afterlife_entity_profiles.json"));
            Assert.NotNull(archive.GetEntry("game_state/meta/afterlife_active_threats.json"));
            Assert.NotNull(archive.GetEntry("game_state/meta/afterlife_chronicles.json"));
            Assert.NotNull(archive.GetEntry("game_state/meta/afterlife_spiritual_conflict_state.json"));
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
        Assert.Equal("Chaos Sea", stateManager.CurrentState.CurrentRealm);

        var issues = await new ValidationService(fs, NullLogger<ValidationService>.Instance).ValidateGameStateAsync();
        var blockingIssues = issues.Where(static issue => issue.Severity == IssueSeverity.Error).ToArray();
        Assert.True(
            blockingIssues.Length == 0,
            "Loaded reusable Chaos Sea command display save has blocking validation issues:" +
            Environment.NewLine + string.Join(Environment.NewLine, blockingIssues.Select(static issue => issue.ToString())));

        Assert.Equal(sourceHashBefore, await ComputeSha256Async(sourceArchive));
        Assert.True(await saveLoad.LoadGameAsync(sourceArchive));
        Assert.Equal(sourceHashBefore, await ComputeSha256Async(sourceArchive));
    }

    [Theory]
    [MemberData(nameof(CoveredChaosSeaCommandInvocations))]
    public async Task LoadedChaosSeaCommandDisplaySave_RendersAvailableCommandInBrowserAndConsole(
        string commandId,
        string command)
    {
        var result = await ExecuteFromLoadedSaveAsync(command);

        var report = ConsoleCommandOutputQualityClassifier.Classify(result);
        Assert.True(
            report.IsUsablePlayerOutput,
            $"{commandId} ({command}) returned unusable player-facing output from the reusable Chaos Sea save:" +
            Environment.NewLine + string.Join(Environment.NewLine, report.Violations));

        var console = new TestExplorerConsole();
        var renderException = Record.Exception(() => ExplorerCommandResultConsoleRenderer.Render(console, result));
        Assert.Null(renderException);
        Assert.NotEmpty(console.Rendered);
    }

    [Theory]
    [MemberData(nameof(ChaosSeaDetailInvocations))]
    public async Task LoadedChaosSeaCommandDisplaySave_RendersRepresentativeDetailTargets(
        string commandId,
        string command,
        string expectedText)
    {
        var result = await ExecuteFromLoadedSaveAsync(command);

        var report = ConsoleCommandOutputQualityClassifier.Classify(result);
        var violations = report.Violations.ToList();
        var visibleText = report.VisibleText;
        if (!visibleText.Contains(expectedText, StringComparison.OrdinalIgnoreCase))
            violations.Add($"visible text does not include expected detail text: {expectedText}");

        Assert.True(
            violations.Count == 0,
            $"{commandId} ({command}) returned unusable detail output from the reusable Chaos Sea save:" +
            Environment.NewLine + string.Join(Environment.NewLine, violations));

        var console = new TestExplorerConsole();
        var renderException = Record.Exception(() => ExplorerCommandResultConsoleRenderer.Render(console, result));
        Assert.Null(renderException);
        Assert.NotEmpty(console.Rendered);
    }

    public static IEnumerable<object[]> CoveredChaosSeaCommandInvocations()
    {
        foreach (var descriptor in ExplorerCommandCatalog.Descriptors
                     .Where(static descriptor =>
                         descriptor.Group is ExplorerCommandGroup.ChaosSea or ExplorerCommandGroup.AfterlifeCombatAndEntities)
                     .OrderBy(static descriptor => descriptor.Group.ToString(), StringComparer.OrdinalIgnoreCase)
                     .ThenBy(static descriptor => descriptor.Id, StringComparer.OrdinalIgnoreCase))
        {
            foreach (var alias in descriptor.Aliases)
                yield return [descriptor.Id, alias];
        }

        foreach (var (id, command) in PracticalUniversalChaosSeaPreviewCommands())
            yield return [id, command];
    }

    public static IEnumerable<object[]> ChaosSeaDetailInvocations()
    {
        yield return ["guardians", "/guardians хранитель guardian_azalia", "Азалия"];
        yield return ["abode_power", "/abode_power запись power_azalia_archive_oath_001", "Клятва архивного света"];
        yield return ["guardian_projects", "/guardian_projects проект guardian_azalia::project_archive_lighthouse", "Архивный маяк"];
        yield return ["abodes", "/abodes обитель abode_azalia", "Шелковый Архив"];
        yield return ["afterlife_profiles", "/afterlife_profiles профиль player_soul", "Пепельная Искра"];
        yield return ["afterlife_profiles", "/afterlife_profiles профиль guardian_azalia", "Азалия"];
        yield return ["afterlife_threats", "/afterlife_threats угроза chaos_soul_hunter_pack_example", "Стая охотников"];
        yield return ["afterlife_chronicles", "/afterlife_chronicles хроника chronicle_chaos_black_tide_example", "Черный прилив"];
        yield return ["afterlife_inbox", "/afterlife_inbox уведомление notif_guardian_trade_ready_001", "витрина Азалии"];
        yield return ["spiritual_conflict", "/spiritual_conflict обмен exchange_chaos_hunter_001", "зеркальная защита"];
        yield return ["spiritual_combat_log", "/spiritual_combat_log обмен exchange_chaos_hunter_001", "зеркальная защита"];
        yield return ["spiritual_combat_log", "/spiritual_combat_log итог recent_conflict_hunter_pack_044", "охотники отступили"];
        yield return ["spiritual_arts", "/spiritual_arts искусство pressure", "Давление"];
        yield return ["spiritual_arts", "/spiritual_arts особое ash_mirror_guard", "Зеркальная защита"];
        yield return ["soul_relics", "/soul_relics реликвия relic_ash_mirror", "Зеркало Пепельной Искры"];
        yield return ["afterlife_archive", "/afterlife_archive запись archive_black_tide_oath", "Черный прилив"];
        yield return ["archive_candidates", "/archive_candidates кандидат candidate_hunter_echo", "эхо охотников"];
        yield return ["archive_consultation", "/archive_consultation хранитель guardian_azalia", "Азалия"];
        yield return ["archive_project_fuel", "/archive_project_fuel проект guardian_azalia::project_archive_lighthouse", "Архивный маяк"];
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

    private async Task<ExplorerCommandResult> ExecuteFromLoadedSaveAsync(string command)
    {
        var sourceArchive = GetSourceArchivePath();
        Assert.True(File.Exists(sourceArchive), $"Missing reusable Chaos Sea command display save: {sourceArchive}");

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

    private string CreateIsolatedRoot()
    {
        var root = Path.Combine(_rootPath, Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(root);
        return root;
    }

    private static string GetSourceArchivePath() =>
        Path.Combine(TestRepoPaths.BaseSessionRoot, "saves", "manual_saves", SaveFileName);

    private static string GetSourceMetadataPath() =>
        Path.Combine(TestRepoPaths.BaseSessionRoot, "saves", "manual_saves", MetadataFileName);

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

    private static IEnumerable<(string Id, string Command)> PracticalUniversalChaosSeaPreviewCommands()
    {
        yield return ("help", "/help");
        yield return ("status", "/статус");
        yield return ("soul", "/душа");
        yield return ("soul_relics", "/реликвии");
        yield return ("afterlife_archive", "/архив_души");
        yield return ("archive_candidates", "/архив_кандидаты");
        yield return ("soul_quests", "/квесты_души");
        yield return ("achievements", "/достижения");
        yield return ("chronicle", "/хроника");
        yield return ("story", "/story");
        yield return ("behavior", "/поведение");
        yield return ("lives", "/жизни");
        yield return ("feathers", "/перья");
        yield return ("codex", "/кодекс");
        yield return ("world_rules", "/правила_мира");
        yield return ("gallery", "/галерея");
        yield return ("validate", "/валидация");
        yield return ("world_setup", "/настройка_мира");
    }
}
