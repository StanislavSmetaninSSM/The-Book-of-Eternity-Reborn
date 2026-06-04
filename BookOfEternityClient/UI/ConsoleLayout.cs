using Spectre.Console;

namespace BookOfEternityClient.UI;

internal static class ConsoleLayout
{
    public static Grid WithHorizontalMargin(Spectre.Console.Rendering.IRenderable content, int margin = 2)
    {
        var safeMargin = Math.Max(0, margin);
        var grid = new Grid();
        if (safeMargin == 0)
        {
            grid.AddColumn(new GridColumn());
            grid.AddRow(content);
            return grid;
        }

        grid.AddColumn(new GridColumn().Width(safeMargin));
        grid.AddColumn(new GridColumn());
        grid.AddColumn(new GridColumn().Width(safeMargin));
        grid.AddRow(new Text(""), content, new Text(""));
        return grid;
    }

    public static string CreateBarFromPercent(int percentage, int width, string filledColor, string emptyColor = "grey")
    {
        var filled = Math.Clamp(percentage * width / 100, 0, width);
        return CreateBar(filled, width, filledColor, emptyColor);
    }

    public static string CreateBar(int filled, int width, string filledColor, string emptyColor = "grey")
    {
        var clamped = Math.Clamp(filled, 0, width);
        var empty = width - clamped;
        return $"[{filledColor}]{new string('█', clamped)}[/][dim {emptyColor}]{new string('░', empty)}[/]";
    }

    public static Grid CreateFactGrid(params string[] items)
    {
        var nonEmpty = items.Where(i => !string.IsNullOrWhiteSpace(i)).ToArray();
        var grid = new Grid();
        foreach (var _ in nonEmpty)
            grid.AddColumn(new GridColumn().NoWrap());

        if (nonEmpty.Length > 0)
            grid.AddRow(nonEmpty.Select(i => new Markup(i)).ToArray());

        return grid;
    }

    public static string PlainChoiceLabel(params string[] parts)
    {
        return GameInterface.SafePromptChoice(parts);
    }

    public static Table CreateInfoTable(int labelWidth = 22)
    {
        return new Table()
            .Border(TableBorder.None)
            .HideHeaders()
            .Expand()
            .AddColumn(new TableColumn("").NoWrap().Width(labelWidth))
            .AddColumn(new TableColumn(""));
    }

    public static Table CreateBarMetricTable(int labelWidth = 18, int barWidth = 16, int valueWidth = 16)
    {
        return new Table()
            .Border(TableBorder.None)
            .HideHeaders()
            .Expand()
            .AddColumn(new TableColumn("").NoWrap().Width(labelWidth))
            .AddColumn(new TableColumn("").NoWrap().Width(barWidth))
            .AddColumn(new TableColumn("").RightAligned().NoWrap().Width(valueWidth))
            .AddColumn(new TableColumn(""));
    }
}
