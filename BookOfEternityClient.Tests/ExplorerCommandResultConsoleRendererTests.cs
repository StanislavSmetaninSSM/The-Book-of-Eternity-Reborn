using System.Collections;
using System.Text.Json.Nodes;
using BookOfEternityClient.CommandProtocol;
using BookOfEternityClient.UI;
using Spectre.Console;
using Spectre.Console.Rendering;
using Xunit;

namespace BookOfEternityClient.Tests;

public sealed class ExplorerCommandResultConsoleRendererTests
{
    [Fact]
    public void Render_WritesRepresentativeDtoSurfacesToConsole()
    {
        var console = new TestExplorerConsole();
        var result = new ExplorerCommandResult
        {
            Command = "/spiritual_combat_help",
            State = CommandExecutionState.RequiresInput,
            Blocks =
            [
                new UiTextBlock { Text = "Духовный бой: выберите действие.", Tone = UiTone.Accent },
                new UiPanelBlock
                {
                    Title = "Сводка",
                    Blocks =
                    [
                        new UiTextBlock { Text = "Позиция: спорная", Tone = UiTone.Default }
                    ]
                },
                new UiTableBlock
                {
                    Title = "Ресурсы",
                    Columns = ["Параметр", "Значение"],
                    Rows =
                    [
                        new UiTableRow { Cells = ["ОД", "7"] },
                        new UiTableRow { Cells = ["Напряжение", "чисто"] }
                    ]
                },
                new UiListBlock
                {
                    Ordered = false,
                    Items = ["Давление ухудшает напряжение врага", "Защита ослабляет входящее действие"]
                },
                new UiKeyValueGridBlock
                {
                    Items =
                    [
                        new UiKeyValueItem { Key = "Царство", Value = "Море Хаоса" },
                        new UiKeyValueItem { Key = "Валюта", Value = "Чернильные Перья" }
                    ]
                },
                new UiMessageBlock
                {
                    Severity = UiNotificationSeverity.Warning,
                    Title = "Внимание",
                    Message = "Активен незавершенный конфликт."
                },
                new UiRawJsonBlock
                {
                    Title = "Raw state",
                    Json = JsonNode.Parse("""{"activeConflict":{"conflictId":"conflict_1"}}""")!
                }
            ],
            Actions =
            [
                new UiAction
                {
                    Id = "open-log",
                    Label = "Открыть журнал",
                    Command = "/spiritual_combat_log",
                    Style = UiActionStyle.Primary
                }
            ],
            Prompts =
            [
                new UiSelectionPrompt
                {
                    Id = "operation",
                    Prompt = "Выберите духовное искусство",
                    Options =
                    [
                        new UiSelectionOption { Value = "guard", Label = "Защита" },
                        new UiSelectionOption { Value = "counter", Label = "Контрприем" }
                    ]
                },
                new UiLongTextInputPrompt
                {
                    Id = "narrative",
                    Prompt = "Опишите художественное действие"
                }
            ],
            Notifications =
            [
                new UiNotification
                {
                    Severity = UiNotificationSeverity.Info,
                    Title = "Подсказка",
                    Message = "Renderer не исполняет prompt, только отображает его."
                }
            ]
        };

        ExplorerCommandResultConsoleRenderer.Render(console, result);

        var renderedText = string.Join("\n", console.Rendered.Select(ExtractRenderableText));

        Assert.Contains("Духовный бой", renderedText, StringComparison.Ordinal);
        Assert.Contains("Сводка", renderedText, StringComparison.Ordinal);
        Assert.Contains("Позиция: спорная", renderedText, StringComparison.Ordinal);
        Assert.Contains("Параметр", renderedText, StringComparison.Ordinal);
        Assert.Contains("ОД", renderedText, StringComparison.Ordinal);
        Assert.Contains("7", renderedText, StringComparison.Ordinal);
        Assert.Contains("Давление ухудшает напряжение врага", renderedText, StringComparison.Ordinal);
        Assert.Contains("Царство", renderedText, StringComparison.Ordinal);
        Assert.Contains("Чернильные Перья", renderedText, StringComparison.Ordinal);
        Assert.Contains("Внимание", renderedText, StringComparison.Ordinal);
        Assert.Contains("activeConflict", renderedText, StringComparison.Ordinal);
        Assert.Contains("Доступные действия", renderedText, StringComparison.Ordinal);
        Assert.Contains("Открыть журнал", renderedText, StringComparison.Ordinal);
        Assert.DoesNotContain("Стиль", renderedText, StringComparison.Ordinal);
        Assert.DoesNotContain("Primary", renderedText, StringComparison.Ordinal);
        Assert.DoesNotContain("Secondary", renderedText, StringComparison.Ordinal);
        Assert.Contains("Подсказки ввода", renderedText, StringComparison.Ordinal);
        Assert.Contains("Выберите духовное искусство", renderedText, StringComparison.Ordinal);
        Assert.Contains("Защита", renderedText, StringComparison.Ordinal);
        Assert.Contains("Уведомления", renderedText, StringComparison.Ordinal);
        Assert.Contains("Renderer не исполняет prompt", renderedText, StringComparison.Ordinal);
    }

    [Fact]
    public void Render_MapUsesPlayerFacingRealmAndCurrentLocationName()
    {
        var console = new TestExplorerConsole();
        var result = new ExplorerCommandResult
        {
            Command = "/map",
            State = CommandExecutionState.Completed,
            Blocks =
            [
                new UiMapBlock
                {
                    Title = "Карта",
                    Map = new MapViewDto
                    {
                        Realm = "Mortal World",
                        CurrentNodeId = "valmont_estate_corridor_1",
                        ZLevels = [new MapZLevelDto { Z = 0, Label = "этаж поместья" }],
                        Nodes =
                        [
                            new MapNodeDto
                            {
                                Id = "valmont_estate_corridor_1",
                                Label = "Коридор поместья Вальмонт",
                                IsCurrent = true
                            }
                        ]
                    }
                }
            ]
        };

        ExplorerCommandResultConsoleRenderer.Render(console, result);

        var renderedText = string.Join("\n", console.Rendered.Select(ExtractRenderableText));
        Assert.Contains("Смертный мир", renderedText, StringComparison.OrdinalIgnoreCase);
        Assert.Contains("Коридор поместья Вальмонт", renderedText, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("Mortal World", renderedText, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("valmont_estate_corridor_1", renderedText, StringComparison.OrdinalIgnoreCase);
    }

    private static string ExtractRenderableText(IRenderable renderable)
    {
        return renderable switch
        {
            Panel panel => ExtractPanelText(panel),
            Grid grid => ExtractGridText(grid),
            Table table => ExtractTableText(table),
            Markup markup => ExtractParagraphText(markup),
            Text text => text.ToString() ?? string.Empty,
            _ => renderable.ToString() ?? string.Empty
        };
    }

    private static string ExtractPanelText(Panel panel)
    {
        var parts = new List<string>();
        if (panel.Header is { } header && !string.IsNullOrWhiteSpace(header.Text))
            parts.Add(header.Text);

        var childField = typeof(Panel).GetField("_child", System.Reflection.BindingFlags.Instance | System.Reflection.BindingFlags.NonPublic);
        if (childField?.GetValue(panel) is IRenderable child)
        {
            var childText = ExtractRenderableText(child);
            if (!string.IsNullOrWhiteSpace(childText))
                parts.Add(childText);
        }

        return string.Join("\n", parts);
    }

    private static string ExtractGridText(Grid grid)
    {
        var rowTexts = new List<string>();
        foreach (var row in grid.Rows)
        {
            var itemsField = row.GetType().GetField("_items", System.Reflection.BindingFlags.Instance | System.Reflection.BindingFlags.NonPublic);
            if (itemsField?.GetValue(row) is not IEnumerable<IRenderable> items)
                continue;

            var cells = items
                .Select(ExtractRenderableText)
                .Where(static text => !string.IsNullOrWhiteSpace(text))
                .ToArray();
            if (cells.Length > 0)
                rowTexts.Add(string.Join(" | ", cells));
        }

        return string.Join("\n", rowTexts);
    }

    private static string ExtractTableText(Table table)
    {
        var rowTexts = new List<string>();

        if (table.ShowHeaders)
        {
            var headerTexts = table.Columns
                .Select(static column => ExtractRenderableText(column.Header))
                .Where(static text => !string.IsNullOrWhiteSpace(text))
                .ToArray();
            if (headerTexts.Length > 0)
                rowTexts.Add(string.Join(" | ", headerTexts));
        }

        foreach (var row in table.Rows)
        {
            var cells = new List<string>();
            for (var index = 0; index < row.Count; index++)
            {
                var text = ExtractRenderableText(row[index]);
                if (!string.IsNullOrWhiteSpace(text))
                    cells.Add(text);
            }

            if (cells.Count > 0)
                rowTexts.Add(string.Join(" | ", cells));
        }

        return string.Join("\n", rowTexts);
    }

    private static string ExtractParagraphText(object renderable)
    {
        var paragraphField = renderable.GetType().GetField("_paragraph", System.Reflection.BindingFlags.Instance | System.Reflection.BindingFlags.NonPublic);
        var paragraph = paragraphField?.GetValue(renderable);
        if (paragraph == null)
            return string.Empty;

        var linesField = paragraph.GetType().GetField("_lines", System.Reflection.BindingFlags.Instance | System.Reflection.BindingFlags.NonPublic);
        if (linesField?.GetValue(paragraph) is not IEnumerable<object> lines)
            return string.Empty;

        var lineTexts = new List<string>();
        foreach (var line in lines)
        {
            var itemsField = line.GetType().GetField("_items", System.Reflection.BindingFlags.Instance | System.Reflection.BindingFlags.NonPublic);
            if (itemsField?.GetValue(line) is not Array items)
                continue;

            lineTexts.Add(string.Concat(items.Cast<object?>().Where(static segment => segment != null).Select(static segment =>
            {
                var textProperty = segment!.GetType().GetProperty("Text", System.Reflection.BindingFlags.Instance | System.Reflection.BindingFlags.Public);
                return textProperty?.GetValue(segment)?.ToString() ?? string.Empty;
            })));
        }

        return string.Join("\n", lineTexts);
    }
}
