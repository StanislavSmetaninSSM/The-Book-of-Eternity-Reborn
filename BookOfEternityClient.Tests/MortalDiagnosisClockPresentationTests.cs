using System.Text.Json;
using System.Text.Json.Nodes;
using BookOfEternityClient.Core;
using BookOfEternityClient.UI;
using Xunit;

namespace BookOfEternityClient.Tests;

public sealed class MortalDiagnosisClockPresentationTests
{
    [Theory]
    [InlineData("{\"currentTimeInMinutes\":780,\"timeOfDay\":\"08:00\"}", "780", null)]
    [InlineData("{\"currentTimeInMinutes\":1460,\"timeOfDay\":\"23:50\"}", "1460", null)]
    [InlineData("{\"currentTimeInMinutes\":0,\"timeOfDay\":\"08:00\"}", "0", null)]
    [InlineData("{\"currentTimeInMinutes\":480}", "480", null)]
    [InlineData("{\"currentTimeInMinutes\":0,\"setWorldTime\":{\"currentTimeInMinutes\":999,\"timeOfDay\":\"08:00\"}}", "0", null)]
    [InlineData("{\"setWorldTime\":{\"currentTimeInMinutes\":780,\"timeOfDay\":\"08:00\"}}", "780", null)]
    [InlineData("{\"year\":\"12\",\"monthName\":\"Света\",\"dayOfMonth\":3,\"timeOfDay\":\"08:00\"}", null, "08:00")]
    [InlineData("{\"setWorldTime\":{\"timeOfDay\":\"08:00\"}}", null, "08:00")]
    [InlineData("{\"timeChange\":25}", null, "25")]
    [InlineData("{\"currentTimeInMinutes\":-1,\"timeOfDay\":\"08:00\"}", null, "08:00")]
    public void ActualClockConsumersPreferCumulativeMinutesWithoutInventingCalendar(string json, string? numeric, string? fallback)
    {
        using var parsed = JsonDocument.Parse(json);
        var root = JsonNode.Parse(json)!.AsObject(); var original = root.ToJsonString();
        var status = StateManager.FormatWorldTime(parsed.RootElement);
        var console = new List<string>(); ExplorerMode.AppendWorldTimeLines(console, parsed.RootElement, "");
        var browser = ExplorerMortalWorldCommandResultBuilder.BuildWorldTimeItems(root);
        if (numeric is not null)
        {
            var expected = "Игровое время: " + numeric + " мин.";
            Assert.Equal(expected, status);
            Assert.Contains(console, line => line.Contains(expected, StringComparison.Ordinal));
            var fact = Assert.Single(browser); Assert.Equal("Игровое время", fact.Key); Assert.Equal(numeric + " мин.", fact.Value);
            Assert.DoesNotContain("08:00", status); Assert.DoesNotContain("23:50", status);
            Assert.DoesNotContain(console, line => line.Contains("08:00", StringComparison.Ordinal) || line.Contains("23:50", StringComparison.Ordinal));
        }
        else
        {
            Assert.Contains(fallback!, status);
            Assert.Contains(console, line => line.Contains(fallback!, StringComparison.Ordinal));
            Assert.Contains(browser, fact => fact.Value.Contains(fallback!, StringComparison.Ordinal));
        }
        Assert.DoesNotContain(browser, fact => fact.Key == "Минут от начала суток");
        Assert.Equal(original, root.ToJsonString());
    }
}
