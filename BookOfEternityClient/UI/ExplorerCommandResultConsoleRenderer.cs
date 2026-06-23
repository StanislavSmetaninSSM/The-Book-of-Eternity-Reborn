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
        UiEntityDossierBlock dossier => RenderEntityDossier(dossier),
        _ => GameInterface.SafeMarkupText(block.ToString() ?? string.Empty)
    };

    private static IRenderable RenderText(UiTextBlock block) =>
        GameInterface.SafeMarkup(
            ApplyTone(GameInterface.EscapeMarkup(block.Text), block.Tone),
            "command result text");

    private static IRenderable RenderPanel(UiPanelBlock block)
    {
        var grid = new Grid();
        grid.AddColumn();
        foreach (var child in block.Blocks)
            grid.AddRow(RenderBlock(child));

        return new Panel(grid)
        {
            Header = GameInterface.SafePanelHeader(block.Title),
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
            table.AddColumn(new TableColumn(GameInterface.EscapeMarkup(column)));

        if (block.Columns.Count == 0)
            table.AddColumn(string.Empty);

        foreach (var row in block.Rows)
        {
            var cells = row.Cells
                .Select(static cell => (IRenderable)GameInterface.SafeMarkupText(cell))
                .ToArray();
            table.AddRow(cells.Length == 0 ? [GameInterface.SafeMarkupText(string.Empty)] : cells);
        }

        return string.IsNullOrWhiteSpace(block.Title)
            ? table
            : new Panel(table)
            {
                Header = GameInterface.SafePanelHeader(block.Title),
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
            grid.AddRow(GameInterface.SafeMarkupText($"{prefix} {block.Items[index]}"));
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
                GameInterface.SafeMarkup(
                    $"[bold]{GameInterface.EscapeMarkup(item.Key)}[/]",
                    "command result key"),
                GameInterface.SafeMarkupText(item.Value));
        }

        return table;
    }

    private static IRenderable RenderMessage(UiMessageBlock block)
    {
        var color = SeverityColor(block.Severity);
        return new Panel(GameInterface.SafeMarkupText(block.Message))
        {
            Header = GameInterface.SafePanelHeader(block.Title),
            Border = BoxBorder.Rounded,
            BorderStyle = new Style(color),
            Padding = new Padding(1, 0),
            Expand = true
        };
    }

    private static IRenderable RenderRawJson(UiRawJsonBlock block)
    {
        var json = block.Json?.ToJsonString(SharedJsonOptions.PrettyCamelCaseUnsafeRelaxed) ?? "null";
        return new Panel(GameInterface.SafeMarkupText(json))
        {
            Header = GameInterface.SafePanelHeader(block.Title),
            Border = BoxBorder.Rounded,
            BorderStyle = new Style(Color.Grey),
            Padding = new Padding(1, 0),
            Expand = true
        };
    }

    private static IRenderable RenderImage(UiImageBlock block) =>
        new Panel(GameInterface.SafeMarkupText($"{block.Title}\n{block.RelativePath}\n{block.ContentType}, {block.Length} байт"))
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

        table.AddRow(GameInterface.SafeMarkupText("Царство"), GameInterface.SafeMarkupText(ExplorerPlayerFacingLabels.Realm(block.Map.Realm)));
        table.AddRow(GameInterface.SafeMarkupText("Локаций"), GameInterface.SafeMarkupText(block.Map.Nodes.Count.ToString()));
        table.AddRow(GameInterface.SafeMarkupText("Связей"), GameInterface.SafeMarkupText(block.Map.Links.Count.ToString()));
        table.AddRow(
            GameInterface.SafeMarkupText("Уровни"),
            GameInterface.SafeMarkupText(string.Join(", ", block.Map.ZLevels.Select(static level => level.Label))));
        var currentNode = ExplorerPlayerFacingLabels.CurrentMapNode(block.Map);
        if (!string.IsNullOrWhiteSpace(currentNode))
            table.AddRow(GameInterface.SafeMarkupText("Текущая точка"), GameInterface.SafeMarkupText(currentNode));

        return new Panel(table)
        {
            Header = GameInterface.SafePanelHeader(string.IsNullOrWhiteSpace(block.Title) ? "Карта" : block.Title),
            Border = BoxBorder.Rounded,
            BorderStyle = new Style(Color.Green),
            Padding = new Padding(1, 0),
            Expand = true
        };
    }

    private static IRenderable RenderEntityDossier(UiEntityDossierBlock block)
    {
        var grid = new Grid();
        grid.AddColumn();

        AddTextRow(grid, block.Subtitle, UiTone.Muted);
        AddTextRow(grid, block.Summary, UiTone.Default);
        AddBadgeRow(grid, block.Badges);
        AddFactRows(grid, block.Facts);
        AddMetricRows(grid, block.Metrics);
        AddHintRows(grid, block.Hints);
        AddListRows(grid, block.List);

        foreach (var card in block.Cards)
            grid.AddRow(RenderEntityCard(card));

        foreach (var section in block.Sections)
            grid.AddRow(RenderEntitySection(section));

        return new Panel(grid)
        {
            Header = GameInterface.SafePanelHeader(string.IsNullOrWhiteSpace(block.Title) ? "Сведения" : block.Title),
            Border = BoxBorder.Rounded,
            BorderStyle = new Style(Color.Grey),
            Padding = new Padding(1, 0),
            Expand = true
        };
    }

    private static IRenderable RenderEntitySection(UiEntityDossierSection section)
    {
        var grid = new Grid();
        grid.AddColumn();

        AddTextRow(grid, section.Summary, UiTone.Muted);
        AddFactRows(grid, section.Facts);
        AddMetricRows(grid, section.Metrics);
        AddHintRows(grid, section.Hints);
        AddListRows(grid, section.List);

        foreach (var card in section.Cards)
            grid.AddRow(RenderEntityCard(card));

        foreach (var child in section.Blocks)
            grid.AddRow(RenderBlock(child));

        return new Panel(grid)
        {
            Header = GameInterface.SafePanelHeader(string.IsNullOrWhiteSpace(section.Title) ? "Раздел" : section.Title),
            Border = BoxBorder.Rounded,
            BorderStyle = new Style(Color.Grey),
            Padding = new Padding(1, 0),
            Expand = true
        };
    }

    private static IRenderable RenderEntityCard(UiEntityCard card)
    {
        var grid = new Grid();
        grid.AddColumn();

        AddTextRow(grid, card.Subtitle, UiTone.Muted);
        AddTextRow(grid, card.Summary, UiTone.Default);
        AddBadgeRow(grid, card.Badges);
        AddFactRows(grid, card.Facts);
        AddMetricRows(grid, card.Metrics);
        AddHintRows(grid, card.Hints);
        AddListRows(grid, card.List);

        foreach (var child in card.Nested)
            grid.AddRow(RenderEntityCard(child));

        foreach (var child in card.Cards)
            grid.AddRow(RenderEntityCard(child));

        return new Panel(grid)
        {
            Header = GameInterface.SafePanelHeader(string.IsNullOrWhiteSpace(card.Title) ? "Запись" : card.Title),
            Border = BoxBorder.Rounded,
            BorderStyle = new Style(Color.Grey),
            Padding = new Padding(1, 0),
            Expand = true
        };
    }

    private static void AddTextRow(Grid grid, string? text, UiTone tone)
    {
        if (string.IsNullOrWhiteSpace(text))
            return;

        grid.AddRow(GameInterface.SafeMarkup(ApplyTone(GameInterface.EscapeMarkup(text), tone), "entity text"));
    }

    private static void AddBadgeRow(Grid grid, IReadOnlyList<UiEntityBadge> badges)
    {
        var labels = badges
            .Select(static badge => badge.Label)
            .Where(static label => !string.IsNullOrWhiteSpace(label))
            .ToList();
        if (labels.Count == 0)
            return;

        grid.AddRow(GameInterface.SafeMarkupText(string.Join(" • ", labels)));
    }

    private static void AddFactRows(Grid grid, IReadOnlyList<UiEntityFact> facts)
    {
        if (facts.Count == 0)
            return;

        var table = new Table()
            .Border(TableBorder.None)
            .HideHeaders()
            .Expand()
            .AddColumn(new TableColumn(string.Empty).NoWrap())
            .AddColumn(string.Empty);

        foreach (var fact in facts)
        {
            table.AddRow(
                GameInterface.SafeMarkup($"[bold]{GameInterface.EscapeMarkup(fact.Label)}[/]", "entity fact label"),
                GameInterface.SafeMarkupText(fact.Value));
        }

        grid.AddRow(table);
    }

    private static void AddMetricRows(Grid grid, IReadOnlyList<UiEntityMetric> metrics)
    {
        foreach (var metric in metrics)
        {
            var value = metric.Max > 0 ? $"{metric.Value:0}/{metric.Max:0}" : metric.Value.ToString("0");
            if (!string.IsNullOrWhiteSpace(metric.Note))
                value += $" ({metric.Note})";

            grid.AddRow(GameInterface.SafeMarkupText($"{metric.Label}: {value}"));
        }
    }

    private static void AddHintRows(Grid grid, IReadOnlyList<UiEntityHint> hints)
    {
        foreach (var hint in hints)
        {
            var title = string.IsNullOrWhiteSpace(hint.Title) ? "Заметка" : hint.Title;
            grid.AddRow(new Panel(GameInterface.SafeMarkupText(hint.Text))
            {
                Header = GameInterface.SafePanelHeader(title),
                Border = BoxBorder.Rounded,
                BorderStyle = new Style(Color.Grey),
                Padding = new Padding(1, 0),
                Expand = true
            });
        }
    }

    private static void AddListRows(Grid grid, IReadOnlyList<string> items)
    {
        if (items.Count == 0)
            return;

        grid.AddRow(RenderList(new UiListBlock { Items = items.ToList() }));
    }

    private static IRenderable RenderActions(IReadOnlyList<UiAction> actions)
    {
        var table = new Table()
            .Border(TableBorder.Rounded)
            .BorderColor(Color.Cyan1)
            .Expand()
            .AddColumn("Действие")
            .AddColumn("Команда");

        foreach (var action in actions)
        {
            table.AddRow(
                GameInterface.SafeMarkupText(action.Label),
                GameInterface.SafeMarkupText(action.Command));
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
                GameInterface.SafeMarkupText(prompt.Prompt),
                GameInterface.SafeMarkupText(DescribePromptType(prompt)),
                GameInterface.SafeMarkupText(DescribePromptOptions(prompt)));
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

        return new Panel(GameInterface.SafeMarkupText(notification.Message))
        {
            Header = GameInterface.SafePanelHeader(title),
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
