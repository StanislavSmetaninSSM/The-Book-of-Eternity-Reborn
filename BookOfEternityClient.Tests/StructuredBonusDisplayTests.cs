using System.Text.Json;
using BookOfEternityClient.UI;
using Xunit;

namespace BookOfEternityClient.Tests;

public sealed class StructuredBonusDisplayTests
{
    [Fact]
    public void FormatValue_NullJsonValueDoesNotLeakToPlayerOutput()
    {
        using var document = JsonDocument.Parse("null");

        var formatted = StructuredBonusDisplay.FormatValue(document.RootElement);

        Assert.Equal(string.Empty, formatted);
    }

    [Fact]
    public void FormatValue_UndefinedJsonValueDoesNotLeakToPlayerOutput()
    {
        var formatted = StructuredBonusDisplay.FormatValue(default(JsonElement));

        Assert.Equal(string.Empty, formatted);
    }

    [Fact]
    public void FormatValue_ObjectSkipsTechnicalEmptyChildValues()
    {
        using var document = JsonDocument.Parse("""{"accessoryForSlot":null,"value":2}""");

        var formatted = StructuredBonusDisplay.FormatValue(document.RootElement);

        Assert.DoesNotContain("null", formatted, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("Аксессуар для", formatted, StringComparison.OrdinalIgnoreCase);
        Assert.Contains("Значение: 2", formatted, StringComparison.OrdinalIgnoreCase);
    }
}
