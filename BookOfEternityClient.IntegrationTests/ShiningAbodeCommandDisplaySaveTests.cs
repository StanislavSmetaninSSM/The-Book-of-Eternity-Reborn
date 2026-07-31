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

[Trait("Category", "FullValidation")]
public sealed class ShiningAbodeCommandDisplaySaveTests : IDisposable
{
    private const string SaveFileName = "shining_abode_command_display_fixture.zip";
    private const string MetadataFileName = "shining_abode_command_display_fixture_metadata.json";
    private const string SaveName = "Shining Abode Command Display Fixture (#1097)";
    private const string SaveRelativePath = "saves/manual_saves/" + SaveFileName;

    private readonly string _rootPath = Path.Combine(Path.GetTempPath(), "boe-shining-abode-command-save-" + Guid.NewGuid().ToString("N"));

    [Fact]
    public async Task NamedShiningAbodeCommandDisplaySave_IsDiscoverableLoadableValidAndRepeatable()
    {
        var sourceArchive = GetSourceArchivePath();
        var sourceMetadata = GetSourceMetadataPath();
        Assert.True(File.Exists(sourceArchive), $"Missing reusable Shining Abode command display save: {sourceArchive}");
        Assert.True(File.Exists(sourceMetadata), $"Missing reusable Shining Abode command display save metadata: {sourceMetadata}");

        using (var metadata = JsonDocument.Parse(await File.ReadAllTextAsync(sourceMetadata)))
        {
            Assert.Equal(SaveName, metadata.RootElement.GetProperty("saveName").GetString());
            Assert.Equal(SaveFileName, metadata.RootElement.GetProperty("archiveFile").GetString());
            Assert.Equal("Shining Abode", metadata.RootElement.GetProperty("currentRealm").GetString());
            Assert.Contains(
                metadata.RootElement.GetProperty("sourceIssues").EnumerateArray(),
                issue => string.Equals(
                    issue.GetString(),
                    "https://github.com/StanislavSmetaninSSM/The-Book-of-Eternity-Reborn/issues/1097",
                    StringComparison.OrdinalIgnoreCase));
        }

        using (var archive = ZipFile.OpenRead(sourceArchive))
        {
            Assert.NotNull(archive.GetEntry("save_metadata.json"));
            Assert.NotNull(archive.GetEntry("game_state/meta/soul_state.json"));
            Assert.NotNull(archive.GetEntry("game_state/meta/guardians.json"));
            Assert.NotNull(archive.GetEntry("game_state/meta/shining_abode_state.json"));
            Assert.NotNull(archive.GetEntry("game_state/meta/guardian_abode_residents.json"));
            Assert.NotNull(archive.GetEntry("game_state/meta/afterlife_entity_profiles.json"));
            Assert.NotNull(archive.GetEntry("game_state/meta/afterlife_active_threats.json"));
            Assert.NotNull(archive.GetEntry("game_state/meta/afterlife_chronicles.json"));
            Assert.NotNull(archive.GetEntry("game_state/meta/afterlife_spiritual_conflict_state.json"));
            Assert.NotNull(archive.GetEntry("game_state/control/afterlife_notifications.json"));
            Assert.DoesNotContain(archive.Entries, static entry =>
                entry.FullName.StartsWith("game_session/", StringComparison.OrdinalIgnoreCase) ||
                entry.FullName.StartsWith("saves/", StringComparison.OrdinalIgnoreCase));
            Assert.DoesNotContain(archive.Entries, static entry =>
                entry.FullName.StartsWith("game_state/control/pending_turn_snapshot", StringComparison.OrdinalIgnoreCase) ||
                entry.FullName.Equals("game_state/control/validation_repair_request.json", StringComparison.OrdinalIgnoreCase) ||
                entry.FullName.StartsWith("input/", StringComparison.OrdinalIgnoreCase));
            AssertAfterlifeArchiveStoredEntriesHaveSourceLife(archive);
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
        Assert.Equal("Shining Abode", stateManager.CurrentState.CurrentRealm);

        var issues = await new ValidationService(fs, NullLogger<ValidationService>.Instance).ValidateGameStateAsync();
        var blockingIssues = issues.Where(static issue => issue.Severity == IssueSeverity.Error).ToArray();
        Assert.True(
            blockingIssues.Length == 0,
            "Loaded reusable Shining Abode command display save has blocking validation issues:" +
            Environment.NewLine + string.Join(Environment.NewLine, blockingIssues.Select(static issue => issue.ToString())));

        Assert.Equal(sourceHashBefore, await ComputeSha256Async(sourceArchive));
        Assert.True(await saveLoad.LoadGameAsync(sourceArchive));
        Assert.Equal(sourceHashBefore, await ComputeSha256Async(sourceArchive));
    }

    [Theory]
    [MemberData(nameof(CoveredShiningAbodeCommandInvocations))]
    public async Task LoadedShiningAbodeCommandDisplaySave_RendersAvailableCommandInBrowserAndConsole(
        string commandId,
        string command)
    {
        var result = await ExecuteFromLoadedSaveAsync(command);

        var report = ConsoleCommandOutputQualityClassifier.Classify(result);
        Assert.True(
            report.IsUsablePlayerOutput,
            $"{commandId} ({command}) returned unusable player-facing output from the reusable Shining Abode save:" +
            Environment.NewLine + string.Join(Environment.NewLine, report.Violations));

        var console = new TestExplorerConsole();
        var renderException = Record.Exception(() => ExplorerCommandResultConsoleRenderer.Render(console, result));
        Assert.Null(renderException);
        Assert.NotEmpty(console.Rendered);
    }

    [Theory]
    [MemberData(nameof(ShiningAbodeDetailInvocations))]
    public async Task LoadedShiningAbodeCommandDisplaySave_RendersRepresentativeDetailTargets(
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
        if (string.Equals(commandId, "archive_project_fuel", StringComparison.OrdinalIgnoreCase))
        {
            if (!visibleText.Contains("снимок для отображения", StringComparison.OrdinalIgnoreCase))
                violations.Add("archive project fuel detail should localize display_snapshot project type.");
            if (!visibleText.Contains("видимый", StringComparison.OrdinalIgnoreCase))
                violations.Add("archive project fuel detail should localize visible project tier.");
            if (!visibleText.Contains("просмотр", StringComparison.OrdinalIgnoreCase))
                violations.Add("archive project fuel detail should localize display project mode.");
            if (visibleText.Contains("display_snapshot", StringComparison.OrdinalIgnoreCase) ||
                visibleText.Contains("visible", StringComparison.OrdinalIgnoreCase) ||
                visibleText.Contains("display", StringComparison.OrdinalIgnoreCase))
                violations.Add("archive project fuel detail should not leak raw project protocol values.");
        }

        Assert.True(
            violations.Count == 0,
            $"{commandId} ({command}) returned unusable detail output from the reusable Shining Abode save:" +
            Environment.NewLine + string.Join(Environment.NewLine, violations));

        var console = new TestExplorerConsole();
        var renderException = Record.Exception(() => ExplorerCommandResultConsoleRenderer.Render(console, result));
        Assert.Null(renderException);
        Assert.NotEmpty(console.Rendered);
    }

    public static IEnumerable<object[]> CoveredShiningAbodeCommandInvocations()
    {
        foreach (var descriptor in ExplorerCommandCatalog.Descriptors
                     .Where(static descriptor =>
                         descriptor.Group is ExplorerCommandGroup.ShiningAbode or ExplorerCommandGroup.AfterlifeCombatAndEntities)
                     .OrderBy(static descriptor => descriptor.Group.ToString(), StringComparer.OrdinalIgnoreCase)
                     .ThenBy(static descriptor => descriptor.Id, StringComparer.OrdinalIgnoreCase))
        {
            foreach (var alias in descriptor.Aliases)
                yield return [descriptor.Id, alias];
        }

        foreach (var (id, command) in PracticalUniversalShiningAbodePreviewCommands())
            yield return [id, command];
    }

    public static IEnumerable<object[]> ShiningAbodeDetailInvocations()
    {
        yield return ["shining_abode", "/shining_abode врата card_social", "Песнь Рассвета"];
        yield return ["shining_abode", "/shining_abode проект faction_lanterns::project_dawn", "Проект Рассвета"];
        yield return ["shining_abode", "/shining_abode исход core_receipt_open", "Врата открылись"];
        yield return ["shining_politics", "/shining_politics фракция faction_lanterns", "Дом Фонарей"];
        yield return ["shining_politics", "/shining_politics хроника chronicle_dawn", "Рассветный спор"];
        yield return ["shining_politics", "/shining_politics ресурс ledger_sparks", "Искры Света"];
        yield return ["shining_politics", "/shining_politics решение founding_receipt_dawn", "Дом Рассвета"];
        yield return ["afterlife_profiles", "/afterlife_profiles профиль player_soul", "Пепельная Искра"];
        yield return ["afterlife_profiles", "/afterlife_profiles профиль resident_mirel", "Мирель"];
        yield return ["afterlife_threats", "/afterlife_threats угроза shining_oath_cell_fixture", "Тихая ячейка"];
        yield return ["afterlife_chronicles", "/afterlife_chronicles хроника chronicle_shining_silver_hall_oath", "Серебряный Зал"];
        yield return ["afterlife_inbox", "/afterlife_inbox уведомление notif_shining_trade_ready_001", "Сияющая витрина"];
        yield return ["spiritual_conflict", "/spiritual_conflict обмен exchange_shining_oath_001", "серебряная печать"];
        yield return ["spiritual_combat_log", "/spiritual_combat_log обмен exchange_shining_oath_001", "серебряная печать"];
        yield return ["spiritual_combat_log", "/spiritual_combat_log итог recent_shining_oath_cell_001", "оттиск клятвы"];
        yield return ["spiritual_arts", "/spiritual_arts искусство pressure", "Давление"];
        yield return ["spiritual_arts", "/spiritual_arts особое radiance_oath_cut", "Разрез клятвы"];
        yield return ["soul_relics", "/soul_relics реликвия relic_lantern_memory", "Фонарь Памяти"];
        yield return ["afterlife_archive", "/afterlife_archive запись archive_silver_hall_oath", "Серебряный Зал"];
        yield return ["archive_candidates", "/archive_candidates кандидат candidate_shining_oath_trace", "оттиск клятвы"];
        yield return ["archive_consultation", "/archive_consultation хранитель guardian_azalia", "Азалия"];
        yield return ["archive_project_fuel", "/archive_project_fuel проект guardian_azalia::project_shining_archive_lighthouse", "Архивный маяк"];
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
        Assert.True(File.Exists(sourceArchive), $"Missing reusable Shining Abode command display save: {sourceArchive}");

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

    private static void AssertAfterlifeArchiveStoredEntriesHaveSourceLife(ZipArchive archive)
    {
        var soulStateEntry = archive.GetEntry("game_state/meta/soul_state.json");
        Assert.NotNull(soulStateEntry);
        using var stream = soulStateEntry!.Open();
        using var document = JsonDocument.Parse(stream);
        if (!document.RootElement.TryGetProperty("afterlifeArchive", out var archiveElement) ||
            !archiveElement.TryGetProperty("stored", out var storedElement))
        {
            return;
        }

        var index = 0;
        foreach (var storedEntry in storedElement.EnumerateArray())
        {
            Assert.True(
                storedEntry.TryGetProperty("sourceLife", out var sourceLife) &&
                sourceLife.ValueKind == JsonValueKind.Number &&
                sourceLife.TryGetInt32(out _),
                $"Shining Abode display save archive entry #{index} must include numeric sourceLife.");
            index++;
        }
    }

    private static IEnumerable<(string Id, string Command)> PracticalUniversalShiningAbodePreviewCommands()
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
