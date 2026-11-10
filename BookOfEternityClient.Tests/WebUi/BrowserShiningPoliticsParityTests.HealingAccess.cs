using System.Text.Json.Nodes;
using BookOfEternityClient.CommandProtocol;
using BookOfEternityClient.Configuration;
using BookOfEternityClient.Core;
using BookOfEternityClient.Services;
using BookOfEternityClient.UI;
using Spectre.Console;
using Spectre.Console.Rendering;
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

    [Theory]
    [InlineData(true)] [InlineData(false)]
    public async Task HealingAccessPublicTermsHaveConsoleBrowserParityWithoutGrant(bool available)
    {
        await SeedHealingRosterAsync(1); await SeedPublicHealingAccessTermsAsync();
        await ChangeAccessProfileAsync(profile => profile["healingServiceProfile"]!["availability"] = available ? "available" : "unavailable");
        var before = SnapshotHealingRosterSources();
        var browser = await ReadHealingRosterAsync();
        var (consoleText, _) = await InspectHealingAccessThroughConsoleMenuAsync();
        foreach (var label in new[] { "Лояльность к фракции", "Тир лояльности", "Внутреннее брожение", "Состояние перестройки", "Проекты фракции" })
            Assert.Contains(label, consoleText);
        foreach (var text in new[] { CollectResultAndPromptText(browser), consoleText })
        {
            Assert.Contains("Согласие Совета", text); Assert.Contains("не ниже 25", text);
            Assert.Contains("ответная услуга", text); Assert.DoesNotContain("Объявленный тариф", text);
            Assert.Contains(available ? "Целитель объявил приём открытым" : "Объявленный приём временно недоступен", text);
            Assert.Contains("Лечение через команду этого целителя пока недоступно", text);
            Assert.DoesNotContain("resident_healer_0", text);
        }
        Assert.Empty(browser.Actions); Assert.Empty(browser.Prompts); Assert.Null(browser.InteractiveSession);
        AssertHealingRosterSourcesUnchanged(before);
    }

    [Theory]
    [InlineData("absent")] [InlineData("private")] [InlineData("malformed")] [InlineData("missing_life")]
    public async Task HealingAccessUnknownTermsKeepHealerVisibleWithoutPrivateText(string change)
    {
        await SeedHealingRosterAsync(1); await SeedPublicHealingAccessTermsAsync();
        await ChangeAccessProfileAsync(profile =>
        {
            if (change == "absent") profile.Remove("healingServiceProfile");
            else if (change == "missing_life") profile.Remove("lifeState");
            else if (change == "private") profile["healingServiceProfile"]!["visibility"] = "restricted";
            else profile["healingServiceProfile"]!["accessConditions"] = new JsonArray(new JsonObject { ["secret"] = "private_payload" });
        });
        var before = SnapshotHealingRosterSources();
        var browser = await ReadHealingRosterAsync(); var (console, _) = await InspectHealingAccessThroughConsoleMenuAsync();
        foreach (var text in new[] { CollectResultAndPromptText(browser), console })
        {
            Assert.Contains("целитель; Исцеление I", text);
            Assert.Contains("Подтверждённые публичные условия помощи пока не объявлены", text);
            Assert.DoesNotContain("Согласие Совета", text); Assert.DoesNotContain("private_payload", text);
        }
        AssertHealingRosterSourcesUnchanged(before);
    }

    [Fact]
    public async Task HealingAccessNextActionReadsNewLiteralConditionsAndPrice()
    {
        await SeedHealingRosterAsync(1); await SeedPublicHealingAccessTermsAsync();
        var old = await ReadHealingRosterAsync();
        await ChangeAccessProfileAsync(profile =>
        {
            profile["healingServiceProfile"]!["accessConditions"]![0] = "Новое [red]согласие[/]";
            profile["healingServiceProfile"]!["compensationKinds"] = new JsonArray("ink_feathers");
        });
        var before = SnapshotHealingRosterSources();
        var fresh = await ReadHealingRosterAsync(); var (console, _) = await InspectHealingAccessThroughConsoleMenuAsync();
        foreach (var text in new[] { CollectResultAndPromptText(fresh), console })
        {
            Assert.Contains("Новое [red]согласие[/]", text); Assert.Contains("для раны I: 32 Чернильных Перьев", text);
            Assert.DoesNotContain("для раны II", text);
        }
        Assert.Contains("Согласие Совета", CollectResultAndPromptText(old));
        Assert.DoesNotContain("Новое [red]согласие[/]", CollectResultAndPromptText(old));
        AssertHealingRosterSourcesUnchanged(before);
    }

    [Theory]
    [InlineData("hidden_faction")] [InlineData("removed_faction")] [InlineData("membership")]
    [InlineData("hidden_profile")] [InlineData("realm")] [InlineData("duplicate_faction")]
    [InlineData("malformed_profiles")] [InlineData("malformed_residents")]
    public async Task HealingAccessSelectionRechecksFreshSourcesBeforeDisplay(string change)
    {
        await SeedHealingRosterAsync(1); await SeedPublicHealingAccessTermsAsync();
        var start = 0; Dictionary<string, byte[]?>? afterMutation = null;
        var (_, console) = await InspectHealingAccessThroughConsoleMenuAsync(afterSelection: inner =>
        {
            start = inner.Rendered.Count;
            var path = change switch
            {
                "membership" or "malformed_residents" => GuardianAbodeResidentState.StatePath,
                "hidden_profile" or "malformed_profiles" => AfterlifeEntityProfileState.StatePath,
                "realm" => "game_state/meta/soul_state.json", _ => ShiningAbodeState.StatePath
            };
            if (change.StartsWith("malformed_", StringComparison.Ordinal)) File.WriteAllText(_fs.ResolvePath(path), "{private_payload");
            else
            {
                var root = JsonNode.Parse(File.ReadAllText(_fs.ResolvePath(path)))!.AsObject();
                if (change == "realm") root["currentRealm"] = "Mortal World";
                else if (change == "membership") root["entries"]!.AsArray().OfType<JsonObject>()
                    .Single(r => r["residentId"]?.GetValue<string>() == "resident_healer_0")["shiningFactionId"] = "faction_mirrors";
                else if (change == "hidden_profile") root["profiles"]!.AsArray().OfType<JsonObject>()
                    .Single(p => p["actorId"]?.GetValue<string>() == "resident_healer_0")["isPlayerVisible"] = false;
                else
                {
                    var faction = root["factions"]!.AsArray().OfType<JsonObject>().Single(f => f["factionId"]?.GetValue<string>() == "faction_lanterns");
                    if (change == "hidden_faction") faction["visibility"] = "hidden";
                    else if (change == "removed_faction") root["factions"]!.AsArray().Remove(faction);
                    else root["factions"]!.AsArray().Add(faction.DeepClone());
                }
                File.WriteAllText(_fs.ResolvePath(path), root.ToJsonString());
            }
            afterMutation = SnapshotHealingRosterSources();
        });
        var text = RenderAccessConsole(console, start);
        Assert.DoesNotContain("Согласие Совета", text); Assert.DoesNotContain("Исцеление I", text);
        Assert.DoesNotContain("private_payload", text);
        AssertHealingRosterSourcesUnchanged(Assert.IsType<Dictionary<string, byte[]?>>(afterMutation));
    }

    [Fact]
    public async Task HealingAccessCancelDoesNotReadProfilesOrChangeState()
    {
        var reads = 0; var measure = false;
        using var context = new BrowserShiningPoliticsParityTests(new FileSystemManagerHooks
        {
            BeforeCanonicalReadOpenAsync = path => { if (measure && path == AfterlifeEntityProfileState.StatePath) reads++; return Task.CompletedTask; }
        });
        await context.SeedHealingRosterAsync(1); await context.SeedPublicHealingAccessTermsAsync();
        var before = context.SnapshotHealingRosterSources(); measure = true;
        var (text, _) = await context.InspectHealingAccessThroughConsoleMenuAsync(cancelFaction: true);
        measure = false;
        Assert.DoesNotContain("Согласие Совета", text); Assert.Equal(0, reads);
        context.AssertHealingRosterSourcesUnchanged(before);
    }

    [Fact]
    public async Task HealingAccessPostSelectionReadsFiveSourcesForOneOrTwentyMembers()
    {
        foreach (var count in new[] { 1, 20 })
        {
            var selectedReads = new Dictionary<string, int>(StringComparer.Ordinal); var measuring = false;
            using var context = new BrowserShiningPoliticsParityTests(new FileSystemManagerHooks
            {
                BeforeCanonicalReadOpenAsync = path => { if (measuring) selectedReads[path] = selectedReads.GetValueOrDefault(path) + 1; return Task.CompletedTask; }
            });
            await context.SeedHealingRosterAsync(count); await context.SeedPublicHealingAccessTermsAsync(allHealers: true);
            var before = context.SnapshotHealingRosterSources();
            var (text, _) = await context.InspectHealingAccessThroughConsoleMenuAsync(
                afterSelection: _ => measuring = true, afterInspection: _ => measuring = false);
            Assert.Contains("Согласие Совета", text); Assert.Equal(5, selectedReads.Values.Sum());
            Assert.Equal(new[] { AfterlifeEntityProfileState.StatePath, GuardianAbodeResidentState.StatePath,
                "game_state/meta/guardians.json", ShiningAbodeState.StatePath, "game_state/meta/soul_state.json" }.OrderBy(p => p), selectedReads.Keys.OrderBy(p => p));
            Assert.All(selectedReads.Values, value => Assert.Equal(1, value));
            _healingRosterOutput!.WriteLine("interactive members={0}; post-selection actual opens={1}; {2}", count,
                selectedReads.Values.Sum(), string.Join(", ", selectedReads.OrderBy(p => p.Key).Select(p => p.Key + "=" + p.Value)));
            context.AssertHealingRosterSourcesUnchanged(before);
        }
    }

    [Fact]
    public async Task HealingAccessSourceExtractionPreservesFullOverviewResourceRefusals()
    {
        var reads = new Dictionary<string, int>(StringComparer.Ordinal); var measuring = false;
        using var context = new BrowserShiningPoliticsParityTests(new FileSystemManagerHooks
        {
            BeforeCanonicalReadOpenAsync = path => { if (measuring) reads[path] = reads.GetValueOrDefault(path) + 1; return Task.CompletedTask; }
        });
        await context.SeedHealingRosterAsync(1);
        var soulPath = "game_state/meta/soul_state.json";
        var soul = JsonNode.Parse((await context._fs.ReadFileAsync(soulPath))!)!.AsObject();
        soul[ShiningBlessingEffectState.SoulStateProperty] = new JsonObject { ["relicRefinementEntitlements"] = new JsonObject() };
        await context._fs.WriteFileAtomicAsync(soulPath, soul.ToJsonString());
        var before = context.SnapshotHealingRosterSources();
        var console = new TestExplorerConsole(); console.QueueAnySelection("← Назад");
        var explorer = new ExplorerMode(context._stateManager, context._fs, new LocalizationManager(), console: console);
        measuring = true; Assert.Equal(string.Empty, await explorer.TryProcessCommand("/shining_abode")); measuring = false;
        var text = RenderAccessConsole(console);
        Assert.Contains("Сияющая гача", text); Assert.Contains("Перебросы реликвий", text);
        // The thin current fixture deliberately has no quartet bindings. Both
        // unchanged real projectors must read their source and fail closed.
        Assert.True(reads.GetValueOrDefault(ShiningAbodeState.StatePath) >= 2);
        Assert.True(reads.GetValueOrDefault(soulPath) >= 2);
        Assert.Contains(ResourcePlayerFailureMessages.Unavailable, text);
        context.AssertHealingRosterSourcesUnchanged(before);
    }

    private async Task SeedPublicHealingAccessTermsAsync(string id = "resident_healer_0", bool allHealers = false)
    {
        var root = JsonNode.Parse((await _fs.ReadFileAsync(AfterlifeEntityProfileState.StatePath))!)!.AsObject();
        foreach (var profile in root["profiles"]!.AsArray().OfType<JsonObject>().Where(p =>
            p["actorType"]?.GetValue<string>() == "resident" && (allHealers || p["actorId"]?.GetValue<string>() == id)))
        {
            profile["lifeState"] = "alive";
            profile["healingServiceProfile"] = new JsonObject
            {
                ["providerId"] = profile["actorId"]!.DeepClone(), ["realm"] = "shining_abode", ["locationRef"] = profile["locationId"]!.DeepClone(),
                ["visibility"] = "public", ["availability"] = "unavailable", ["minimumRelationship"] = 25,
                ["priceMultiplierPercent"] = 125, ["compensationKinds"] = new JsonArray("favor"),
                ["accessConditions"] = new JsonArray("Согласие Совета")
            };
        }
        await _fs.WriteFileAtomicAsync(AfterlifeEntityProfileState.StatePath, root.ToJsonString());
    }

    private async Task ChangeAccessProfileAsync(Action<JsonObject> change)
    {
        var root = JsonNode.Parse((await _fs.ReadFileAsync(AfterlifeEntityProfileState.StatePath))!)!.AsObject();
        change(root["profiles"]!.AsArray().OfType<JsonObject>().Single(p => p["actorId"]?.GetValue<string>() == "resident_healer_0"));
        await _fs.WriteFileAtomicAsync(AfterlifeEntityProfileState.StatePath, root.ToJsonString());
    }

    private async Task<(string Text, TestExplorerConsole Console)> InspectHealingAccessThroughConsoleMenuAsync(
        Action<TestExplorerConsole>? afterSelection = null, Action<TestExplorerConsole>? afterInspection = null, bool cancelFaction = false)
    {
        var console = new TestExplorerConsole();
        console.QueueSelection("Политика Сияющей Обители", "👥 Осмотреть политическое состояние фракции — целители и условия помощи", "← Назад");
        console.QueueSelection("Выберите фракцию для политического осмотра", cancelFaction ? "← Назад" : "Дом Фонарей");
        console.ReadKeyCallback = () => { console.ReadKeyCallback = null; afterInspection?.Invoke(console); };
        var explorer = new ExplorerMode(_stateManager, _fs, new LocalizationManager(), console:
            afterSelection is null ? console : new AfterFactionSelectionConsole(console, () => afterSelection(console)));
        Assert.Equal(string.Empty, await explorer.TryProcessCommand("/shining_politics"));
        Assert.Contains(console.SelectionChoicesHistory.First().Choices, choice => choice.Contains("целители и условия помощи", StringComparison.Ordinal));
        return (RenderAccessConsole(console), console);
    }

    private static string RenderAccessConsole(TestExplorerConsole console, int skip = 0)
    {
        using var output = new StringWriter();
        var renderer = AnsiConsole.Create(new AnsiConsoleSettings
        {
            Ansi = AnsiSupport.No, ColorSystem = ColorSystemSupport.NoColors, Out = new AnsiConsoleOutput(output)
        });
        renderer.Profile.Width = 240;
        foreach (var renderable in console.Rendered.Skip(skip)) renderer.Write(renderable);
        return output.ToString();
    }

    private sealed class AfterFactionSelectionConsole(TestExplorerConsole inner, Action afterSelection) : IExplorerConsole
    {
        private int _selections;
        public T Prompt<T>(IPrompt<T> prompt)
        {
            var value = inner.Prompt(prompt);
            if (prompt is SelectionPrompt<string> && ++_selections == 2) afterSelection();
            return value;
        }
        public void Clear() => inner.Clear();
        public void Write(IRenderable content) => inner.Write(content);
        public string Ask(string prompt, string defaultValue = "") => inner.Ask(prompt, defaultValue);
        public bool Confirm(string prompt, bool defaultValue = false) => inner.Confirm(prompt, defaultValue);
        public void Markup(string markup) => inner.Markup(markup);
        public void MarkupLine(string markup) => inner.MarkupLine(markup);
        public void WriteLine() => inner.WriteLine();
        public string? ReadLine() => inner.ReadLine();
        public bool KeyAvailable => inner.KeyAvailable;
        public ConsoleKeyInfo ReadKey() => inner.ReadKey();
    }
}
