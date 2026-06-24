using Xunit;

namespace BookOfEternityClient.Tests;

public sealed partial class ExplorerModeCommandTests
{
    [Fact]
    public async Task TryProcessCommand_Soul_RendersPlayerFacingSummaryWithoutRawJson()
    {
        await SeedAfterlifeStateAsync();

        var result = await _explorer.TryProcessCommand("/душа");

        Assert.Equal(string.Empty, result);
        var text = ExtractRenderedText();
        Assert.Contains("Душа", text, StringComparison.OrdinalIgnoreCase);
        Assert.Contains("Тестовая Душа", text, StringComparison.Ordinal);
        Assert.Contains("Море Хаоса", text, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("Chaos Sea", text, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("Полный JSON", text, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("soul_state", text, StringComparison.OrdinalIgnoreCase);
        AssertNoHiddenExplorerErrors("soul_summary_player_facing");
    }
}
