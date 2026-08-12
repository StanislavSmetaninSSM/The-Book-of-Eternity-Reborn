using System.Collections;
using System.Reflection;
using BookOfEternityClient.CommandProtocol;
using BookOfEternityClient.Configuration;
using BookOfEternityClient.Core;
using BookOfEternityClient.Services;
using BookOfEternityClient.UI;
using Microsoft.Extensions.Logging.Abstractions;
using Spectre.Console;
using Spectre.Console.Rendering;
using Xunit;

namespace BookOfEternityClient.Tests;

public sealed partial class BrowserCommandPresentationAuditTests
{
    [Fact]
    public async Task BrowserHelpCommand_KeepsCommandAliasesAndDescriptionsVisible()
    {
        var result = await ExecuteFromLoadedSaveAsync("mortal_world_command_display_fixture.zip", "/help");

        var dossier = Assert.Single(result.Blocks.OfType<UiEntityDossierBlock>());
        Assert.Equal("help", dossier.EntityType);
        Assert.Contains(dossier.Sections.SelectMany(static section => section.Cards), static card =>
            card.Facts.Any(static fact =>
                fact.Label.Equals("Команда", StringComparison.OrdinalIgnoreCase) &&
                fact.Value.Equals("/inv", StringComparison.OrdinalIgnoreCase)) &&
            card.Facts.Any(static fact =>
                fact.Label.Equals("Русская команда", StringComparison.OrdinalIgnoreCase) &&
                fact.Value.Equals("/инв", StringComparison.OrdinalIgnoreCase)));
        Assert.Contains(dossier.Sections.SelectMany(static section => section.Cards), static card =>
            card.Facts.Any(static fact =>
                fact.Label.Equals("Команда", StringComparison.OrdinalIgnoreCase) &&
                fact.Value.Equals("/npc /npcs", StringComparison.OrdinalIgnoreCase)) &&
            card.Facts.Any(static fact =>
                fact.Label.Equals("Русская команда", StringComparison.OrdinalIgnoreCase) &&
                fact.Value.Equals("/нпс", StringComparison.OrdinalIgnoreCase)));

        var text = CollectResultText(result);
        Assert.Contains("/инв", text, StringComparison.OrdinalIgnoreCase);
        Assert.Contains("/нпс", text, StringComparison.OrdinalIgnoreCase);
        Assert.Contains("/квесты", text, StringComparison.OrdinalIgnoreCase);
        Assert.Contains("Показать инвентарь", text, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("объекта в разделе", text, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain(result.Blocks, static block => block is UiTableBlock);
    }

    [Theory]
    [MemberData(nameof(ConsoleBrowserParitySmokeCommands))]
    public async Task RepresentativeCommands_ExposeSamePlayerFacingAnchorsInConsoleAndBrowser(
        string saveFileName,
        string command,
        string[] expectedAnchors)
    {
        var consoleText = await ExecuteConsoleCommandFromLoadedSaveAsync(saveFileName, command);
        Assert.False(
            string.IsNullOrWhiteSpace(consoleText),
            $"{command} did not produce readable console output from {saveFileName}.");

        var browserResult = await ExecuteFromLoadedSaveAsync(saveFileName, command);
        Assert.Equal(CommandExecutionState.Completed, browserResult.State);
        var browserReport = ConsoleCommandOutputQualityClassifier.Classify(browserResult);
        Assert.True(
            browserReport.IsUsablePlayerOutput,
            $"{command} returned unusable browser output from {saveFileName}:" +
            Environment.NewLine + string.Join(Environment.NewLine, browserReport.Violations));

        foreach (var anchor in expectedAnchors)
        {
            AssertContainsParityAnchor(anchor, consoleText, command, "console");
            AssertContainsParityAnchor(anchor, browserReport.VisibleText, command, "browser");
        }
    }

    public static IEnumerable<object[]> ConsoleBrowserParitySmokeCommands()
    {
        yield return ["mortal_world_command_display_fixture.zip", "/help", new[] { "Показать инвентарь", "Показать персонажей", "Показать квесты" }];
        yield return ["mortal_world_command_display_fixture.zip", "/инв", new[] { "Руническая перчатка", "Инвентарь" }];
        yield return ["mortal_world_command_display_fixture.zip", "/нпс", new[] { "Магистра Селена", "Персонажи" }];
        yield return ["mortal_world_command_display_fixture.zip", "/фракции", new[] { "Купеческая гильдия", "Почётный должник" }];
        yield return ["mortal_world_command_display_fixture.zip", "/локации", new[] { "Покои виконта", "Поместье Вальмонт" }];
        yield return ["mortal_world_command_display_fixture.zip", "/новости_мира событие world_event_valmont_letter", new[] { "Письмо появилось ночью", "переплетённые крылья" }];
        yield return ["mortal_world_command_display_fixture.zip", "/навыки", new[] { "Аристократический этикет", "Чувство магических потоков" }];
        yield return ["mortal_world_command_display_fixture.zip", "/эффекты", new[] { "Тяжёлые сны", "Руническая перчатка" }];

        yield return ["chaos_sea_command_display_fixture.zip", "/хранители", new[] { "Азалия", "Шелковый Архив" }];
        yield return ["chaos_sea_command_display_fixture.zip", "/профили_загробья", new[] { "Пепельная Искра", "Море Хаоса" }];
        yield return ["chaos_sea_command_display_fixture.zip", "/реликвии", new[] { "Реликвии", "Зеркало Пепельной Искры" }];
        yield return ["chaos_sea_command_display_fixture.zip", "/духовный_конфликт", new[] { "духовный конфликт", "Активный конфликт" }];

        yield return ["shining_abode_command_display_fixture.zip", "/сияющая_обитель", new[] { "Сияющая Обитель", "Дом Фонарей" }];
        yield return ["shining_abode_command_display_fixture.zip", "/сияющая_политика", new[] { "Дом Фонарей", "Ресурсы" }];
        yield return ["shining_abode_command_display_fixture.zip", "/профили_загробья", new[] { "Пепельная Искра", "Мирель" }];
        yield return ["shining_abode_command_display_fixture.zip", "/угрозы_загробья", new[] { "Тихая ячейка", "Напряжённость" }];
    }

    private async Task<string> ExecuteConsoleCommandFromLoadedSaveAsync(string saveFileName, string command)
    {
        return await _fixture.ExecuteConsoleCommandAsync(
            saveFileName,
            async (fs, stateManager) =>
            {
                var console = new TestExplorerConsole();
                var explorer = BuildConsoleExplorer(fs, stateManager, console);
                var exception = await Record.ExceptionAsync(() => explorer.TryProcessCommand(command));
                Assert.Null(exception);

                return ExtractConsoleText(console);
            });
    }

    private static ExplorerMode BuildConsoleExplorer(
        FileSystemManager fs,
        StateManager stateManager,
        TestExplorerConsole console)
    {
        var settings = new GameSettings();
        var localization = new LocalizationManager { CurrentLanguage = "ru" };
        var scenarioCoreService = new ScenarioCoreService(fs, NullLogger<ScenarioCoreService>.Instance);
        var storyService = new StoryService(fs, NullLogger<StoryService>.Instance);
        var worldDirectiveService = new WorldDirectiveService(fs, NullLogger<WorldDirectiveService>.Instance);
        var systemModService = new SystemModService(fs, settings, NullLogger<SystemModService>.Instance);
        var systemGuardianLibraryService = new SystemGuardianLibraryService(fs, NullLogger<SystemGuardianLibraryService>.Instance);

        return new ExplorerMode(
            stateManager,
            fs,
            localization,
            validator: new ValidationService(fs, NullLogger<ValidationService>.Instance),
            storyService: storyService,
            pendingTurnState: new PendingTurnStateService(fs, NullLogger<PendingTurnStateService>.Instance),
            guardianTradeService: new GuardianTradeService(fs, NullLogger<GuardianTradeService>.Instance),
            npcTradeService: new NpcTradeService(fs, NullLogger<NpcTradeService>.Instance),
            systemModService: systemModService,
            systemGuardianLibraryService: systemGuardianLibraryService,
            worldDirectiveService: worldDirectiveService,
            scenarioCoreService: scenarioCoreService,
            afterlifeArchiveCandidateService: new AfterlifeArchiveCandidateService(fs, NullLogger<AfterlifeArchiveCandidateService>.Instance),
            afterlifeArchiveConsultationService: new AfterlifeArchiveConsultationService(fs, NullLogger<AfterlifeArchiveConsultationService>.Instance),
            afterlifeArchiveProjectFuelService: new AfterlifeArchiveProjectFuelService(fs, NullLogger<AfterlifeArchiveProjectFuelService>.Instance),
            guardianCorrectionService: new GuardianCorrectionService(fs, scenarioCoreService, NullLogger<GuardianCorrectionService>.Instance),
            soulIdentityService: new SoulIdentityService(fs, NullLogger<SoulIdentityService>.Instance),
            console: console);
    }

    private static string ExtractConsoleText(TestExplorerConsole console)
    {
        var parts = new List<string>();
        parts.AddRange(console.Rendered.Select(ExtractRenderableTextForParity));
        parts.AddRange(console.MarkupLines.Select(RemoveSpectreMarkupForParity));
        parts.AddRange(console.SelectionTitles.Select(RemoveSpectreMarkupForParity));
        parts.AddRange(console.SelectionChoicesHistory.SelectMany(entry => entry.Choices).Select(RemoveSpectreMarkupForParity));
        return string.Join(Environment.NewLine, parts.Where(static part => !string.IsNullOrWhiteSpace(part)));
    }

    private static string ExtractRenderableTextForParity(IRenderable renderable)
    {
        return renderable switch
        {
            Panel panel => ExtractPanelTextForParity(panel),
            Tree tree => ExtractTreeTextForParity(tree),
            Grid grid => ExtractGridTextForParity(grid),
            Table table => ExtractTableTextForParity(table),
            Markup markup => ExtractParagraphTextForParity(markup),
            Text text => ExtractParagraphTextForParity(text),
            _ => renderable.ToString() ?? string.Empty
        };
    }

    private static string ExtractPanelTextForParity(Panel panel)
    {
        var parts = new List<string>();
        if (panel.Header is { } header && !string.IsNullOrWhiteSpace(header.Text))
            parts.Add(header.Text);

        var childField = typeof(Panel).GetField("_child", BindingFlags.Instance | BindingFlags.NonPublic);
        if (childField?.GetValue(panel) is IRenderable child)
            parts.Add(ExtractRenderableTextForParity(child));

        return string.Join(Environment.NewLine, parts.Where(static part => !string.IsNullOrWhiteSpace(part)));
    }

    private static string ExtractTreeTextForParity(Tree tree)
    {
        var rootField = typeof(Tree).GetField("_root", BindingFlags.Instance | BindingFlags.NonPublic);
        if (rootField?.GetValue(tree) is not TreeNode root)
            return string.Empty;

        var parts = new List<string>();
        AppendTreeNodeTextForParity(parts, root);
        return string.Join(Environment.NewLine, parts);
    }

    private static void AppendTreeNodeTextForParity(List<string> parts, TreeNode node)
    {
        var renderableProperty = node.GetType().GetProperty("Renderable", BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic);
        if (renderableProperty?.GetValue(node) is IRenderable renderable)
        {
            var text = ExtractRenderableTextForParity(renderable);
            if (!string.IsNullOrWhiteSpace(text))
                parts.Add(text);
        }

        var nodesProperty = node.GetType().GetProperty("Nodes", BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic);
        if (nodesProperty?.GetValue(node) is IEnumerable children)
        {
            foreach (var child in children.OfType<TreeNode>())
                AppendTreeNodeTextForParity(parts, child);
        }
    }

    private static string ExtractGridTextForParity(Grid grid)
    {
        var rowTexts = new List<string>();
        foreach (var row in grid.Rows)
        {
            var itemsField = row.GetType().GetField("_items", BindingFlags.Instance | BindingFlags.NonPublic);
            if (itemsField?.GetValue(row) is not IEnumerable<IRenderable> items)
                continue;

            foreach (var item in items)
            {
                var text = ExtractRenderableTextForParity(item);
                if (!string.IsNullOrWhiteSpace(text))
                    rowTexts.Add(text);
            }
        }

        return string.Join(Environment.NewLine, rowTexts);
    }

    private static string ExtractTableTextForParity(Table table)
    {
        var rowTexts = new List<string>();
        if (table.ShowHeaders)
        {
            var headerTexts = table.Columns
                .Select(column => ExtractRenderableTextForParity(column.Header))
                .Where(static text => !string.IsNullOrWhiteSpace(text));
            rowTexts.Add(string.Join(" | ", headerTexts));
        }

        foreach (var row in table.Rows)
        {
            var cells = new List<string>();
            for (var index = 0; index < row.Count; index++)
            {
                var text = ExtractRenderableTextForParity(row[index]);
                if (!string.IsNullOrWhiteSpace(text))
                    cells.Add(text);
            }

            rowTexts.Add(string.Join(" | ", cells));
        }

        return string.Join(Environment.NewLine, rowTexts.Where(static row => !string.IsNullOrWhiteSpace(row)));
    }

    private static string ExtractParagraphTextForParity(object renderable)
    {
        var paragraphField = renderable.GetType().GetField("_paragraph", BindingFlags.Instance | BindingFlags.NonPublic);
        var paragraph = paragraphField?.GetValue(renderable);
        if (paragraph == null)
            return string.Empty;

        var linesField = paragraph.GetType().GetField("_lines", BindingFlags.Instance | BindingFlags.NonPublic);
        if (linesField?.GetValue(paragraph) is not IEnumerable<object> lines)
            return string.Empty;

        var lineTexts = new List<string>();
        foreach (var line in lines)
        {
            var itemsField = line.GetType().GetField("_items", BindingFlags.Instance | BindingFlags.NonPublic);
            if (itemsField?.GetValue(line) is not Array items)
                continue;

            var text = string.Concat(items.Cast<object?>().Where(static segment => segment != null).Select(static segment =>
            {
                var textProperty = segment!.GetType().GetProperty("Text", BindingFlags.Instance | BindingFlags.Public);
                return textProperty?.GetValue(segment)?.ToString() ?? string.Empty;
            }));
            lineTexts.Add(text);
        }

        return string.Join(Environment.NewLine, lineTexts);
    }

    private static string RemoveSpectreMarkupForParity(string text)
    {
        try
        {
            return Markup.Remove(text);
        }
        catch
        {
            return text;
        }
    }

    private static void AssertContainsParityAnchor(string anchor, string text, string command, string surface)
    {
        Assert.True(
            text.Contains(anchor, StringComparison.OrdinalIgnoreCase),
            $"{command} {surface} output does not contain expected player-facing anchor '{anchor}'." +
            Environment.NewLine +
            TrimParityTextForFailure(text));
    }

    private static string TrimParityTextForFailure(string text)
    {
        var normalized = string.Join(
            Environment.NewLine,
            text.Split(["\r\n", "\n"], StringSplitOptions.None)
                .Select(static line => line.TrimEnd()));
        return normalized.Length <= 5000 ? normalized : normalized[..5000] + Environment.NewLine + "...";
    }
}
