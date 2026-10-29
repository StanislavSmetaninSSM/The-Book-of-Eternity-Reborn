using System.Text.Json.Nodes;
using BookOfEternityClient.Configuration;
using BookOfEternityClient.Services;
using BookOfEternityClient.UI;
using Spectre.Console;
using Xunit;

namespace BookOfEternityClient.Tests.WebUi;

public sealed partial class BrowserShiningPoliticsParityTests
{
    [Fact]
    public async Task HealingAccessInteractiveInspectionShowsPublicRestrictedTermsWithoutMutation()
    {
        await SeedHealingRosterAsync(1);
        await SeedPublicHealingAccessTermsAsync();
        var before = SnapshotHealingRosterSources();
        var (text, console) = await InspectHealingAccessThroughConsoleMenuAsync();
        Assert.Contains("Политическое состояние фракции", text);
        Assert.Contains("Лекарь [red]Огонь[/]", text);
        Assert.Contains("Роль во фракции: целитель", text);
        Assert.Contains("Согласие Совета", text); // Existing role control passes; current UI omits these public terms.
        AssertHealingRosterSourcesUnchanged(before);
        Assert.DoesNotContain(console.SelectionChoicesHistory.SelectMany(p => p.Choices), choice =>
            choice.Contains("лечить", StringComparison.OrdinalIgnoreCase));
    }

    private async Task SeedPublicHealingAccessTermsAsync(string id = "resident_healer_0")
    {
        var root = JsonNode.Parse((await _fs.ReadFileAsync(AfterlifeEntityProfileState.StatePath))!)!.AsObject();
        var profile = root["profiles"]!.AsArray().OfType<JsonObject>().Single(p => p["actorId"]?.GetValue<string>() == id);
        profile["lifeState"] = "alive";
        profile["healingServiceProfile"] = new JsonObject
        {
            ["providerId"] = id, ["realm"] = "shining_abode", ["locationRef"] = profile["locationId"]!.DeepClone(),
            ["visibility"] = "public", ["availability"] = "unavailable", ["minimumRelationship"] = 25,
            ["priceMultiplierPercent"] = 125, ["compensationKinds"] = new JsonArray("favor"),
            ["accessConditions"] = new JsonArray("Согласие Совета")
        };
        await _fs.WriteFileAtomicAsync(AfterlifeEntityProfileState.StatePath, root.ToJsonString());
    }

    private async Task<(string Text, TestExplorerConsole Console)> InspectHealingAccessThroughConsoleMenuAsync()
    {
        var console = new TestExplorerConsole();
        console.QueueSelection("Политика Сияющей Обители", "👥 Осмотреть политическое состояние фракции", "← Назад");
        console.QueueSelection("Выберите фракцию для политического осмотра", "Дом Фонарей");
        var explorer = new ExplorerMode(_stateManager, _fs, new LocalizationManager(), console: console);
        Assert.Equal(string.Empty, await explorer.TryProcessCommand("/shining_politics"));
        using var output = new StringWriter();
        var renderer = AnsiConsole.Create(new AnsiConsoleSettings
        {
            Ansi = AnsiSupport.No, ColorSystem = ColorSystemSupport.NoColors, Out = new AnsiConsoleOutput(output)
        });
        renderer.Profile.Width = 240;
        foreach (var renderable in console.Rendered) renderer.Write(renderable);
        return (output.ToString(), console);
    }
}
