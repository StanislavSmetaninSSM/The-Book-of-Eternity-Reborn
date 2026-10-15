using System.Text.Encodings.Web;
using System.Text.Json;
using System.Text.Json.Nodes;
using BookOfEternityClient.CommandProtocol;
using BookOfEternityClient.Configuration;
using BookOfEternityClient.Core;
using BookOfEternityClient.Services;
using BookOfEternityClient.UI;
using BookOfEternityClient.WebUi;
using Microsoft.Extensions.Logging.Abstractions;
using Spectre.Console;
using Xunit;
using Xunit.Abstractions;

namespace BookOfEternityClient.Tests;

/// <summary>Real public status entrypoints, isolated owned roots, no normal-refresh repair.</summary>
public sealed class WoundPlayerStatusTests(ITestOutputHelper output)
{
    [Theory]
    [InlineData("mortal_world")]
    [InlineData("chaos_sea")]
    [InlineData("shining_abode")]
    public async Task Status_VisibleWoundAndKnownMethodAppearWithoutDiagnosisInBothSurfaces(string realm)
    {
        var wound = Wound(realm);
        Assert.Empty(wound["treatment"]!["diagnosisPaths"]!.AsArray());
        if (realm == "mortal_world")
            wound["treatment"]!["routes"]![0]!["requirements"]!.AsArray().Add(new JsonObject
            { ["kind"] = "resource_quantity", ["resourceRef"] = "energy", ["quantity"] = 7, ["ownerRole"] = "target" });
        await using var fixture = await WoundPlayerReadFixture.CreateAsync(output, realm: realm,
            suppliedInput: WoundPlayerProjectionTestData.InputInRealm(realm, wound));
        var result = await ReadBothAsync(fixture);
        var card = Assert.Single(Status(result.Browser).Cards);
        Assert.Equal("Известная рана путника", card.Title);
        Assert.Contains("II", card.Subtitle);
        var method = Assert.Single(card.Cards);
        Assert.Contains(wound["treatment"]!["routes"]![0]!["displayName"]!.GetValue<string>(), method.Title);
        Assert.Contains("не подтверждают наличие ресурсов", method.Summary);
        Assert.NotEmpty(method.List);
        Assert.Null(card.PrimaryAction);
        Assert.Null(method.PrimaryAction);
        Assert.Empty(result.Browser.Prompts);
        if (realm == "mortal_world") Assert.Contains(method.List, q => q.Contains("Энергия") && q.Contains("7"));
        foreach (var text in new[] { result.Console, Text(Status(result.Browser)) })
        {
            Assert.Contains(card.Title, text);
            Assert.Contains(method.Title, text);
            Assert.Contains("не подтверждают наличие ресурсов", text);
            AssertSafe(text);
        }
    }

    [Theory]
    [InlineData("mortal_world")]
    [InlineData("chaos_sea")]
    [InlineData("shining_abode")]
    public async Task Status_MissingHistoryIsUnavailableAndWholeTreeIsPreserved(string realm)
    {
        await using var fixture = await WoundPlayerReadFixture.CreateAsync(output, realm: realm);
        await fixture.DamageAsync("missing_history");
        var result = await ReadBothAsync(fixture);
        var status = Status(result.Browser);
        Assert.Equal(WoundPlayerProjection.UnavailableMessage, status.Summary);
        Assert.Empty(status.Cards);
        Assert.Contains(WoundPlayerProjection.UnavailableMessage, result.Console);
        Assert.DoesNotContain("Рваная рана", result.Console);
        Assert.DoesNotContain("Известная рана души", result.Console);
        Assert.DoesNotContain(result.Browser.Blocks.OfType<UiEntityDossierBlock>(), b => b.EntityType == "wound-history");
    }

    [Theory]
    [InlineData("mortal_world")]
    [InlineData("chaos_sea")]
    [InlineData("shining_abode")]
    public async Task Status_HiddenWoundsForeignOwnersAndUnknownRoutesAreOmittedRecursively(string realm)
    {
        var visible = Wound(realm);
        visible["treatment"]!["knownRouteIds"] = new JsonArray();
        visible["treatment"]!["routes"]![0]!["visibility"] = "hidden";
        visible["treatment"]!["routes"]![0]!["displayName"] = "Секретный способ";
        var hidden = Wound(realm, "wound_status_hidden");
        hidden["display"]!["visibility"] = "hidden";
        hidden["display"]!["name"] = "Скрытая травма";
        var foreign = realm == "mortal_world"
            ? WoundContractTestData.CreateActiveWound("wound_status_foreign", ownerKind: "npc", ownerId: "npc_private", carrierPath: WoundCarrierCatalog.NpcPath)
            : WoundContractTestData.CreateSpiritualActiveWound("wound_status_foreign", realm, ownerId: "other_soul", carrierPath: WoundCarrierCatalog.AfterlifeProfilesPath);
        foreign["display"]!["name"] = "Чужая травма";
        await using var fixture = await WoundPlayerReadFixture.CreateAsync(output, realm: realm,
            suppliedInput: WoundPlayerProjectionTestData.InputInRealm(realm, visible, hidden, foreign));
        var result = await ReadBothAsync(fixture);
        var card = Assert.Single(Status(result.Browser).Cards);
        Assert.Equal("Известная рана путника", card.Title);
        Assert.Empty(card.Cards);
        foreach (var text in new[] { result.Console, Text(Status(result.Browser)) })
        {
            foreach (var forbidden in new[] { "Секретный способ", "Скрытая травма", "Чужая травма", "npc_private", "other_soul" })
                Assert.DoesNotContain(forbidden, text);
            AssertSafe(text);
        }
    }

    [Theory]
    [InlineData("chaos_sea")]
    [InlineData("shining_abode")]
    public async Task Status_HiddenCurrentSoulProfileDoesNotRevealItsWound(string realm)
    {
        await using var fixture = await WoundPlayerReadFixture.CreateAsync(output, realm: realm, hiddenSelf: true);
        var result = await ReadBothAsync(fixture);
        Assert.Empty(Status(result.Browser).Cards);
        Assert.DoesNotContain("Известная рана души", result.Console);
        Assert.DoesNotContain("Известная рана души", Text(Status(result.Browser)));
    }

    [Fact]
    public async Task Status_ConfirmedHealedWoundHasSeparateHistoryInBothSurfaces()
    {
        await using var fixture = await WoundPlayerReadFixture.CreateAsync(output,
            suppliedInput: WoundPlayerHistoryTestData.TerminalInput());
        var result = await ReadBothAsync(fixture);
        Assert.Empty(Status(result.Browser).Cards);
        var history = Assert.Single(result.Browser.Blocks.OfType<UiEntityDossierBlock>(), b => b.EntityType == "wound-history");
        var healed = Assert.Single(history.Cards);
        Assert.Equal("Известная зажившая рана", healed.Title);
        Assert.Equal("Рана зажила", healed.Summary);
        Assert.Contains("История ран", result.Console);
        Assert.Contains(healed.Title, result.Console);
        Assert.DoesNotContain("retained for replay", result.Console + Text(history));
    }

    [Theory]
    [InlineData("mortal_world")]
    [InlineData("chaos_sea")]
    [InlineData("shining_abode")]
    public async Task Status_DynamicMarkupIsLiteralThroughActualConsoleAndBrowserTransport(string realm)
    {
        const string attack = "Рана [red]<img>[/]";
        var wound = Wound(realm);
        wound["display"]!["name"] = attack;
        wound["treatment"]!["routes"]![0]!["displayName"] = attack;
        await using var fixture = await WoundPlayerReadFixture.CreateAsync(output, realm: realm,
            suppliedInput: WoundPlayerProjectionTestData.InputInRealm(realm, wound));
        var result = await ReadBothAsync(fixture);
        var card = Assert.Single(Status(result.Browser).Cards);
        Assert.Equal(attack, card.Title);
        Assert.Equal(attack, Assert.Single(card.Cards).Title);
        Assert.Contains(attack, result.Console);
    }

    private static JsonObject Wound(string realm, string id = "wound_status_visible")
    {
        var wound = realm == "mortal_world" ? WoundContractTestData.CreateActiveWound(id)
            : WoundContractTestData.CreateSpiritualActiveWound(id, realm, ownerId: "player_soul", carrierPath: WoundCarrierCatalog.AfterlifeProfilesPath);
        wound["display"]!["name"] = "Известная рана путника";
        return wound;
    }

    private static async Task<(string Console, ExplorerCommandResult Browser)> ReadBothAsync(WoundPlayerReadFixture fixture)
    {
        // Ordinary transport admission is established before the read-only baseline.
        await using (var lease = await fixture.Files.AcquireCanonicalWriteLeaseAsync())
            _ = fixture.Files.GetOrCreateSessionGeneration(lease);
        var repairs = 0;
        var state = new StateManager(fixture.Files, new GameSettings(), NullLogger<StateManager>.Instance,
            new StateManagerHooks { AfterPlayerSoulProfileInputsReadAsync = () => { repairs++; return Task.CompletedTask; } });
        var console = new TestExplorerConsole();
        var explorer = new ExplorerMode(state, fixture.Files, new LocalizationManager(), console: console);
        var before = fixture.Snapshot();
        var mutations = fixture.MutationCalls;
        Assert.Equal("", await explorer.TryProcessCommand("/статус"));
        var consoleTree = fixture.Snapshot();
        var service = new ExplorerWebCommandService(fixture.Files, state, new LocalizationManager(),
            new ValidationService(fixture.Files, NullLogger<ValidationService>.Instance));
        var browser = await service.ExecuteAsync(new ExplorerWebCommandRequest("/status"));
        Assert.Equal(CommandExecutionState.Completed, browser.State);
        Assert.Equal(before, consoleTree);
        Assert.Equal(before, fixture.Snapshot());
        Assert.Equal(mutations, fixture.MutationCalls);
        Assert.Equal(0, repairs);
        using var writer = new StringWriter();
        var renderer = AnsiConsole.Create(new AnsiConsoleSettings
        { Ansi = AnsiSupport.No, ColorSystem = ColorSystemSupport.NoColors, Interactive = InteractionSupport.No, Out = new AnsiConsoleOutput(writer) });
        renderer.Profile.Width = 200;
        foreach (var rendered in console.Rendered) renderer.Write(rendered);
        foreach (var line in console.MarkupLines) renderer.MarkupLine(line);
        return (writer.ToString(), browser);
    }

    private static UiEntityDossierBlock Status(ExplorerCommandResult result) =>
        Assert.Single(result.Blocks.OfType<UiEntityDossierBlock>(), b => b.EntityType == "wound-status");
    private static string Text(object value) => JsonSerializer.Serialize(value,
        new JsonSerializerOptions { Encoder = JavaScriptEncoder.UnsafeRelaxedJsonEscaping });
    private static void AssertSafe(string text)
    {
        foreach (var token in new[] { "game_state/", "wound_status_", "effect_wound_", "clean_and_suture", "spiritual_route", "sha256:", "authorityRef", "sourceFingerprint", "lastTransition" })
            Assert.DoesNotContain(token, text);
    }
}
