using Xunit;

namespace BookOfEternityClient.Tests;

public sealed partial class ExplorerModeCommandTests
{
    [Fact]
    public async Task TryProcessCommand_MathSimpleExpression_RendersResult()
    {
        var result = await _explorer.TryProcessCommand("/math 2 + 3 * 5");

        Assert.Equal(string.Empty, result);
        var text = ExtractRenderedText();
        Assert.Contains("Математик", text, StringComparison.OrdinalIgnoreCase);
        Assert.Contains("Результат", text, StringComparison.OrdinalIgnoreCase);
        Assert.Contains("17", text, StringComparison.Ordinal);
        Assert.DoesNotContain("JSON результата", text, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("\"success\"", text, StringComparison.OrdinalIgnoreCase);
        AssertNoHiddenExplorerErrors("math_simple_expression");
    }

    [Fact]
    public async Task TryProcessCommand_MathVariables_RendersVariablesAndResult()
    {
        var result = await _explorer.TryProcessCommand("/математик baseDamage * difficultyModifier - resistanceMultiplier baseDamage=12 difficultyModifier=2 resistanceMultiplier=5");

        Assert.Equal(string.Empty, result);
        var text = ExtractRenderedText();
        Assert.Contains("baseDamage", text, StringComparison.Ordinal);
        Assert.Contains("difficultyModifier", text, StringComparison.Ordinal);
        Assert.Contains("resistanceMultiplier", text, StringComparison.Ordinal);
        Assert.Contains("19", text, StringComparison.Ordinal);
        AssertNoHiddenExplorerErrors("math_variables");
    }

    [Fact]
    public async Task TryProcessCommand_MathRounding_RendersLocalizedRoundingMode()
    {
        var result = await _explorer.TryProcessCommand("/math 10 / 3 rounding=floor decimalPlaces=0");

        Assert.Equal(string.Empty, result);
        var text = ExtractRenderedText();
        Assert.Contains("вниз", text, StringComparison.OrdinalIgnoreCase);
        Assert.Contains("знаков: 0", text, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("Floor", text, StringComparison.Ordinal);
        AssertNoHiddenExplorerErrors("math_rounding_localized");
    }

    [Fact]
    public async Task TryProcessCommand_MathInvalidExpression_RendersSafeError()
    {
        var result = await _explorer.TryProcessCommand("/math 2 apples + 3");

        Assert.Equal(string.Empty, result);
        var text = ExtractRenderedText();
        Assert.Contains("Формула не вычислена", text, StringComparison.OrdinalIgnoreCase);
        Assert.Contains("Формула не разобрана", text, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("unexpected_token", text, StringComparison.OrdinalIgnoreCase);
        AssertNoHiddenExplorerErrors("math_invalid_expression");
    }
}
