using BookOfEternityClient.CommandProtocol;
using Xunit;

namespace BookOfEternityClient.Tests;

public sealed class ExplorerHelpCommandResultBuilderTests
{
    [Fact]
    public void Build_ChaosSeaHelp_ReturnsLogicalDtoRowsWithoutSpectreMarkup()
    {
        var result = ExplorerHelpCommandResultBuilder.Build(new ExplorerHelpCommandContext
        {
            Command = "/помощь",
            Title = "Помощь",
            IsChaosSea = true,
            IsShiningAbode = false,
            IsPendingShiningAbodeBootstrap = false,
            CanReenterShiningAbode = true
        });

        Assert.Equal("/помощь", result.Command);
        Assert.Equal(CommandExecutionState.Completed, result.State);
        var table = Assert.IsType<UiTableBlock>(Assert.Single(result.Blocks));
        Assert.Equal("Помощь", table.Title);
        Assert.Equal(["EN", "RU", "Описание"], table.Columns);
        Assert.Contains(table.Rows, row =>
            row.Cells.SequenceEqual(["/chaos_sea", "/море_хаоса", "Обзор Моря Хаоса: активный Хранитель, навигация, ожидающие контракты и доступные действия"]));
        Assert.Contains(table.Rows, row =>
            row.Cells.Count >= 3 &&
            row.Cells[0] == "/reenter_shining_abode" &&
            row.Cells[1] == "/вернуться_в_обитель");
        Assert.DoesNotContain(table.Rows.SelectMany(static row => row.Cells), cell => cell.Contains("[", StringComparison.Ordinal));
        Assert.DoesNotContain(table.Rows.SelectMany(static row => row.Cells), cell => cell.Contains("[/]", StringComparison.Ordinal));
    }
}
