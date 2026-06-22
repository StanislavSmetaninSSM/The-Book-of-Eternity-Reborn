using System.Text.RegularExpressions;
using BookOfEternityClient.CommandProtocol;
using BookOfEternityClient.Configuration;
using BookOfEternityClient.Core;
using BookOfEternityClient.Services;
using BookOfEternityClient.UI;
using BookOfEternityClient.WebUi;
using Microsoft.Extensions.Logging.Abstractions;
using Xunit;

namespace BookOfEternityClient.Tests;

public sealed partial class BrowserCommandPresentationAuditTests : IDisposable
{
    private readonly string _rootPath = Path.Combine(Path.GetTempPath(), "boe-browser-command-presentation-" + Guid.NewGuid().ToString("N"));

    [Theory]
    [MemberData(nameof(MortalEntityCommandInvocations))]
    public async Task MortalEntityCommands_DoNotFlattenStructuredDataIntoPlayerFacingText(string command)
    {
        var result = await ExecuteFromLoadedSaveAsync(
            "mortal_world_command_display_fixture.zip",
            command);

        AssertNoPresentationAntiPatterns(command, result);
    }

    [Theory]
    [MemberData(nameof(ChaosSeaEntityCommandInvocations))]
    public async Task ChaosSeaEntityCommands_DoNotFlattenStructuredDataIntoPlayerFacingText(string command)
    {
        var result = await ExecuteFromLoadedSaveAsync(
            "chaos_sea_command_display_fixture.zip",
            command);

        AssertNoPresentationAntiPatterns(command, result);
    }

    [Theory]
    [MemberData(nameof(ShiningAbodeEntityCommandInvocations))]
    public async Task ShiningAbodeEntityCommands_DoNotFlattenStructuredDataIntoPlayerFacingText(string command)
    {
        var result = await ExecuteFromLoadedSaveAsync(
            "shining_abode_command_display_fixture.zip",
            command);

        AssertNoPresentationAntiPatterns(command, result);
    }

    public static IEnumerable<object[]> MortalEntityCommandInvocations()
    {
        yield return ["/статус"];
        yield return ["/инв"];
        yield return ["/навыки"];
        yield return ["/статы"];
        yield return ["/новости_мира"];
        yield return ["/нпс"];
        yield return ["/квесты"];
        yield return ["/фракции"];
        yield return ["/эффекты"];
        yield return ["/книги"];
        yield return ["/локации"];
    }

    public static IEnumerable<object[]> ChaosSeaEntityCommandInvocations()
    {
        yield return ["/статус"];
        yield return ["/душа"];
        yield return ["/реликвии"];
        yield return ["/хранители"];
        yield return ["/хранители хранитель guardian_azalia"];
        yield return ["/обители"];
        yield return ["/обители обитель abode_azalia"];
        yield return ["/сила_обители"];
        yield return ["/сила_обители запись power_azalia_archive_oath_001"];
        yield return ["/проекты_хранителей"];
        yield return ["/проекты_хранителей проект guardian_azalia::project_archive_lighthouse"];
        yield return ["/профили_загробья"];
        yield return ["/профили_загробья профиль player_soul"];
        yield return ["/угрозы_загробья"];
        yield return ["/хроники_посмертия"];
        yield return ["/духовный_конфликт"];
        yield return ["/журнал_духовного_боя"];
        yield return ["/духовные_искусства"];
    }

    public static IEnumerable<object[]> ShiningAbodeEntityCommandInvocations()
    {
        yield return ["/статус"];
        yield return ["/душа"];
        yield return ["/реликвии"];
        yield return ["/сияющая_обитель"];
        yield return ["/сияющая_обитель врата card_social"];
        yield return ["/сияющая_обитель проект faction_lanterns::project_dawn"];
        yield return ["/сияющая_политика"];
        yield return ["/сияющая_политика фракция faction_lanterns"];
        yield return ["/профили_загробья"];
        yield return ["/профили_загробья профиль player_soul"];
        yield return ["/угрозы_загробья"];
        yield return ["/хроники_посмертия"];
        yield return ["/духовный_конфликт"];
        yield return ["/журнал_духовного_боя"];
        yield return ["/духовные_искусства"];
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
            // Best-effort temp cleanup.
        }
    }

    private async Task<ExplorerCommandResult> ExecuteFromLoadedSaveAsync(string saveFileName, string command)
    {
        var sourceArchive = Path.Combine(TestRepoPaths.BaseSessionRoot, "saves", "manual_saves", saveFileName);
        Assert.True(File.Exists(sourceArchive), $"Missing reusable command display save: {sourceArchive}");

        var loadRoot = CreateIsolatedRoot();
        var fs = new FileSystemManager(loadRoot, NullLogger<FileSystemManager>.Instance);
        fs.EnsureDirectoryStructure();
        CopyCleanCheckoutDependencies(loadRoot);

        var savePath = fs.ResolvePath("saves/manual_saves/" + saveFileName);
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

    private static void AssertNoPresentationAntiPatterns(string command, ExplorerCommandResult result)
    {
        var violations = new List<string>();
        foreach (var block in result.Blocks)
            CollectPresentationViolations(block, violations, "root");

        Assert.True(
            violations.Count == 0,
            $"{command} browser DTO violates the entity dossier presentation contract:" +
            Environment.NewLine +
            string.Join(Environment.NewLine, violations));
    }

    private static void CollectPresentationViolations(UiBlock block, List<string> violations, string path)
    {
        switch (block)
        {
            case UiEntityDossierBlock dossier:
                CollectTextViolations(dossier.Title, violations, $"{path}/{dossier.Title}.title");
                CollectTextViolations(dossier.Subtitle, violations, $"{path}/{dossier.Title}.subtitle");
                CollectTextViolations(dossier.Summary, violations, $"{path}/{dossier.Title}.summary");
                foreach (var fact in dossier.Facts)
                    CollectTextViolations(fact.Value, violations, $"{path}/{dossier.Title}.fact[{fact.Label}]");
                foreach (var hint in dossier.Hints)
                    CollectTextViolations(hint.Text, violations, $"{path}/{dossier.Title}.hint[{hint.Title}]");
                foreach (var item in dossier.List)
                    CollectTextViolations(item, violations, $"{path}/{dossier.Title}.list");
                foreach (var card in dossier.Cards)
                    CollectPresentationViolations(card, violations, $"{path}/{dossier.Title}/card[{card.Title}]");
                foreach (var section in dossier.Sections)
                    CollectPresentationViolations(section, violations, $"{path}/{dossier.Title}/section[{section.Title}]");
                break;

            case UiPanelBlock panel:
                foreach (var child in panel.Blocks)
                    CollectPresentationViolations(child, violations, $"{path}/{panel.Title}");
                break;

            case UiKeyValueGridBlock grid:
                foreach (var item in grid.Items)
                    CollectTextViolations(item.Value, violations, $"{path}/grid[{item.Key}]");
                break;

            case UiTableBlock table:
                violations.Add($"{path}/{table.Title}: entity command exposes a raw table instead of dossier cards ({string.Join(", ", table.Columns)})");
                foreach (var row in table.Rows)
                foreach (var cell in row.Cells)
                    CollectTextViolations(cell, violations, $"{path}/{table.Title}.cell");
                break;

            case UiListBlock list:
                foreach (var item in list.Items)
                    CollectTextViolations(item, violations, $"{path}/list");
                break;

            case UiTextBlock text:
                CollectTextViolations(text.Text, violations, $"{path}/text");
                break;
        }
    }

    private static void CollectPresentationViolations(UiEntityDossierSection section, List<string> violations, string path)
    {
        CollectTextViolations(section.Summary, violations, $"{path}.summary");
        foreach (var fact in section.Facts)
            CollectTextViolations(fact.Value, violations, $"{path}.fact[{fact.Label}]");
        foreach (var hint in section.Hints)
            CollectTextViolations(hint.Text, violations, $"{path}.hint[{hint.Title}]");
        foreach (var item in section.List)
            CollectTextViolations(item, violations, $"{path}.list");
        foreach (var card in section.Cards)
            CollectPresentationViolations(card, violations, $"{path}/card[{card.Title}]");
        foreach (var block in section.Blocks)
            CollectPresentationViolations(block, violations, $"{path}/block");
    }

    private static void CollectPresentationViolations(UiEntityCard card, List<string> violations, string path)
    {
        CollectTextViolations(card.Title, violations, $"{path}.title");
        CollectTextViolations(card.Subtitle, violations, $"{path}.subtitle");
        CollectTextViolations(card.Summary, violations, $"{path}.summary");
        foreach (var fact in card.Facts)
            CollectTextViolations(fact.Value, violations, $"{path}.fact[{fact.Label}]");
        foreach (var hint in card.Hints)
            CollectTextViolations(hint.Text, violations, $"{path}.hint[{hint.Title}]");
        foreach (var item in card.List)
            CollectTextViolations(item, violations, $"{path}.list");
        foreach (var child in card.Nested)
            CollectPresentationViolations(child, violations, $"{path}/nested[{child.Title}]");
        foreach (var child in card.Cards)
            CollectPresentationViolations(child, violations, $"{path}/card[{child.Title}]");
    }

    private static void CollectTextViolations(string? value, List<string> violations, string path)
    {
        if (string.IsNullOrWhiteSpace(value))
            return;

        if (FlattenedStructuredTextPattern().IsMatch(value))
            violations.Add($"{path}: flattened structured fields in one string: {TrimForAssertion(value)}");

        if (RawProtocolTokenPattern().IsMatch(value))
            violations.Add($"{path}: raw protocol token leaks into player-facing text: {TrimForAssertion(value)}");
    }

    private static bool IsGenericDetailsColumn(string column)
    {
        var normalized = column.Trim().ToLowerInvariant();
        return normalized is "подробно" or "подробности" or "детали" or "detail" or "details";
    }

    private static string TrimForAssertion(string value)
    {
        var normalized = string.Join(' ', value.Split((char[]?)null, StringSplitOptions.RemoveEmptyEntries));
        return normalized.Length <= 180 ? normalized : normalized[..180] + "...";
    }

    [GeneratedRegex(@";\s*[\p{L}\p{N}_/\- ]{2,40}:\s*\S", RegexOptions.CultureInvariant)]
    private static partial Regex FlattenedStructuredTextPattern();

    [GeneratedRegex(@"\b(?:DTO|Ui[A-Z]\w+|game_state/|pending_|debug|internal|[a-z]+[A-Z][a-zA-Z]+|[a-z]+_[a-z0-9_]+)\b", RegexOptions.CultureInvariant)]
    private static partial Regex RawProtocolTokenPattern();
}
