using BookOfEternityClient.CommandProtocol;
using BookOfEternityClient.Configuration;
using Spectre.Console;
using Spectre.Console.Rendering;

namespace BookOfEternityClient.UI;

public static class ExplorerCommandResultConsoleRenderer
{
    public static void Render(IExplorerConsole console, ExplorerCommandResult result)
    {
        ArgumentNullException.ThrowIfNull(console);
        ArgumentNullException.ThrowIfNull(result);

        foreach (var notification in result.Notifications)
            console.Write(RenderNotification(notification));

        foreach (var block in result.Blocks)
            console.Write(RenderBlock(block));

        if (result.Actions.Count > 0)
            console.Write(RenderActions(result.Actions));

        if (result.Prompts.Count > 0)
            console.Write(RenderPrompts(result.Prompts));
    }

    private static IRenderable RenderBlock(UiBlock block) => block switch
    {
        UiTextBlock text => RenderText(text),
        UiPanelBlock panel => RenderPanel(panel),
        UiTableBlock table => RenderTable(table),
        UiListBlock list => RenderList(list),
        UiKeyValueGridBlock grid => RenderKeyValueGrid(grid),
        UiMessageBlock message => RenderMessage(message),
        UiRawJsonBlock rawJson => RenderRawJson(rawJson),
        UiImageBlock image => RenderImage(image),
        UiMapBlock map => RenderMap(map),
        _ => new Markup(Markup.Escape(block.ToString() ?? string.Empty))
    };

    private static IRenderable RenderText(UiTextBlock block) =>
        new Markup(ApplyTone(Markup.Escape(block.Text), block.Tone));

    private static IRenderable RenderPanel(UiPanelBlock block)
    {
        var grid = new Grid();
        grid.AddColumn();
        foreach (var child in block.Blocks)
            grid.AddRow(RenderBlock(child));

        return new Panel(grid)
        {
            Header = new PanelHeader($" {Markup.Escape(block.Title)} ", Justify.Center),
            Border = BoxBorder.Rounded,
            BorderStyle = new Style(Color.Grey),
            Padding = new Padding(1, 0),
            Expand = true
        };
    }

    private static IRenderable RenderTable(UiTableBlock block)
    {
        var table = new Table()
            .Border(TableBorder.Rounded)
            .BorderColor(Color.Grey)
            .Expand();

        foreach (var column in block.Columns)
            table.AddColumn(new TableColumn(Markup.Escape(column)));

        if (block.Columns.Count == 0)
            table.AddColumn(string.Empty);

        foreach (var row in block.Rows)
        {
            var cells = row.Cells
                .Select(static cell => (IRenderable)new Markup(Markup.Escape(cell)))
                .ToArray();
            table.AddRow(cells.Length == 0 ? [new Markup(string.Empty)] : cells);
        }

        return string.IsNullOrWhiteSpace(block.Title)
            ? table
            : new Panel(table)
            {
                Header = new PanelHeader($" {Markup.Escape(block.Title)} ", Justify.Center),
                Border = BoxBorder.Rounded,
                BorderStyle = new Style(Color.Grey),
                Padding = new Padding(1, 0),
                Expand = true
            };
    }

    private static IRenderable RenderList(UiListBlock block)
    {
        var grid = new Grid();
        grid.AddColumn();

        for (var index = 0; index < block.Items.Count; index++)
        {
            var prefix = block.Ordered ? $"{index + 1}." : "•";
            grid.AddRow(new Markup($"{prefix} {Markup.Escape(block.Items[index])}"));
        }

        return grid;
    }

    private static IRenderable RenderKeyValueGrid(UiKeyValueGridBlock block)
    {
        var table = new Table()
            .Border(TableBorder.None)
            .HideHeaders()
            .Expand()
            .AddColumn(new TableColumn(string.Empty).NoWrap())
            .AddColumn(string.Empty);

        foreach (var item in block.Items)
        {
            table.AddRow(
                new Markup($"[bold]{Markup.Escape(item.Key)}[/]"),
                new Markup(Markup.Escape(item.Value)));
        }

        return table;
    }

    private static IRenderable RenderMessage(UiMessageBlock block)
    {
        var color = SeverityColor(block.Severity);
        return new Panel(new Markup(Markup.Escape(block.Message)))
        {
            Header = new PanelHeader($" {Markup.Escape(block.Title)} ", Justify.Center),
            Border = BoxBorder.Rounded,
            BorderStyle = new Style(color),
            Padding = new Padding(1, 0),
            Expand = true
        };
    }

    private static IRenderable RenderRawJson(UiRawJsonBlock block)
    {
        var json = block.Json?.ToJsonString(SharedJsonOptions.PrettyCamelCaseUnsafeRelaxed) ?? "null";
        return new Panel(new Markup(Markup.Escape(json)))
        {
            Header = new PanelHeader($" {Markup.Escape(block.Title)} ", Justify.Center),
            Border = BoxBorder.Rounded,
            BorderStyle = new Style(Color.Grey),
            Padding = new Padding(1, 0),
            Expand = true
        };
    }

    private static IRenderable RenderImage(UiImageBlock block) =>
        new Panel(new Markup(Markup.Escape($"{block.Title}\n{block.RelativePath}\n{block.ContentType}, {block.Length} байт")))
        {
            Header = new PanelHeader(" Изображение ", Justify.Center),
            Border = BoxBorder.Rounded,
            BorderStyle = new Style(Color.Grey),
            Padding = new Padding(1, 0),
            Expand = true
        };

    private static IRenderable RenderMap(UiMapBlock block)
    {
        var table = new Table()
            .Border(TableBorder.Rounded)
            .BorderColor(Color.Green)
            .Expand()
            .AddColumn("Поле")
            .AddColumn("Значение");

        table.AddRow("Царство", Markup.Escape(block.Map.Realm));
        table.AddRow("Локаций", block.Map.Nodes.Count.ToString());
        table.AddRow("Связей", block.Map.Links.Count.ToString());
        table.AddRow("Уровни", Markup.Escape(string.Join(", ", block.Map.ZLevels.Select(static level => level.Label))));
        if (!string.IsNullOrWhiteSpace(block.Map.CurrentNodeId))
            table.AddRow("Текущая точка", Markup.Escape(block.Map.CurrentNodeId));

        return new Panel(table)
        {
            Header = new PanelHeader($" {Markup.Escape(string.IsNullOrWhiteSpace(block.Title) ? "Карта" : block.Title)} ", Justify.Center),
            Border = BoxBorder.Rounded,
            BorderStyle = new Style(Color.Green),
            Padding = new Padding(1, 0),
            Expand = true
        };
    }

    private static IRenderable RenderActions(IReadOnlyList<UiAction> actions)
    {
        var table = new Table()
            .Border(TableBorder.Rounded)
            .BorderColor(Color.Cyan1)
            .Expand()
            .AddColumn("Действие")
            .AddColumn("Команда")
            .AddColumn("Стиль");

        foreach (var action in actions)
        {
            table.AddRow(
                new Markup(Markup.Escape(action.Label)),
                new Markup(Markup.Escape(action.Command)),
                new Markup(Markup.Escape(action.Style.ToString())));
        }

        return new Panel(table)
        {
            Header = new PanelHeader(" Доступные действия ", Justify.Center),
            Border = BoxBorder.Rounded,
            BorderStyle = new Style(Color.Cyan1),
            Padding = new Padding(1, 0),
            Expand = true
        };
    }

    private static IRenderable RenderPrompts(IReadOnlyList<UiPrompt> prompts)
    {
        var table = new Table()
            .Border(TableBorder.Rounded)
            .BorderColor(Color.Yellow)
            .Expand()
            .AddColumn("Поле")
            .AddColumn("Тип")
            .AddColumn("Варианты");

        foreach (var prompt in prompts)
        {
            table.AddRow(
                new Markup(Markup.Escape(prompt.Prompt)),
                new Markup(Markup.Escape(DescribePromptType(prompt))),
                new Markup(Markup.Escape(DescribePromptOptions(prompt))));
        }

        return new Panel(table)
        {
            Header = new PanelHeader(" Подсказки ввода ", Justify.Center),
            Border = BoxBorder.Rounded,
            BorderStyle = new Style(Color.Yellow),
            Padding = new Padding(1, 0),
            Expand = true
        };
    }

    private static IRenderable RenderNotification(UiNotification notification)
    {
        var color = SeverityColor(notification.Severity);
        var title = string.IsNullOrWhiteSpace(notification.Title)
            ? "Уведомления"
            : $"Уведомления: {notification.Title}";

        return new Panel(new Markup(Markup.Escape(notification.Message)))
        {
            Header = new PanelHeader($" {Markup.Escape(title)} ", Justify.Center),
            Border = BoxBorder.Rounded,
            BorderStyle = new Style(color),
            Padding = new Padding(1, 0),
            Expand = true
        };
    }

    private static string DescribePromptType(UiPrompt prompt) => prompt switch
    {
        UiConfirmationPrompt => "подтверждение",
        UiSelectionPrompt => "выбор",
        UiTextInputPrompt => "короткий текст",
        UiLongTextInputPrompt => "длинный текст",
        _ => prompt.GetType().Name
    };

    private static string DescribePromptOptions(UiPrompt prompt) => prompt switch
    {
        UiSelectionPrompt selection => string.Join(", ", selection.Options.Select(static option => option.Label)),
        UiConfirmationPrompt confirmation => confirmation.DefaultValue ? "по умолчанию: да" : "по умолчанию: нет",
        UiTextInputPrompt text when !string.IsNullOrWhiteSpace(text.DefaultValue) => text.DefaultValue,
        UiLongTextInputPrompt text when !string.IsNullOrWhiteSpace(text.DefaultValue) => text.DefaultValue,
        _ => string.Empty
    };

    private static string ApplyTone(string escapedText, UiTone tone) => tone switch
    {
        UiTone.Muted => $"[grey]{escapedText}[/]",
        UiTone.Subtle => $"[dim]{escapedText}[/]",
        UiTone.Accent => $"[cyan]{escapedText}[/]",
        UiTone.Success => $"[green]{escapedText}[/]",
        UiTone.Warning => $"[yellow]{escapedText}[/]",
        UiTone.Error => $"[red]{escapedText}[/]",
        _ => escapedText
    };

    private static Color SeverityColor(UiNotificationSeverity severity) => severity switch
    {
        UiNotificationSeverity.Success => Color.Green,
        UiNotificationSeverity.Warning => Color.Yellow,
        UiNotificationSeverity.Error => Color.Red,
        _ => Color.Cyan1
    };
}
