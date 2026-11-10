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

namespace BookOfEternityClient.Tests;

public sealed partial class WoundForeignDetailTests
{
    /// <summary>Actual minimal console and public browser read handlers agree on visible detail without repair or treatment.</summary>
    [Theory]
    [InlineData("guardian")]
    [InlineData("resident")]
    public async Task KnownForeignCardHasReadOnlyConsoleBrowserParityAndLiteralText(string kind)
    {
        const string symptom = "[red]Заметное дрожание[/]";
        var data = WoundForeignDetailTestData.Create(kind, wound =>
        {
            wound["display"]!["visibleSymptoms"] = new JsonArray(symptom);
            wound["treatment"]!["routes"]![0]!["requirements"] = new JsonArray(new JsonObject
            { ["kind"] = "source_capability", ["capabilityRef"] = "private_spiritual_support", ["actorRole"] = "provider",
                ["privateOpaque"] = "ПРИВАТНЫЕ_УСЛОВИЯ" });
            var hidden = wound["treatment"]!["routes"]![0]!.DeepClone().AsObject();
            hidden["routeId"] = "route_unknown"; hidden["displayName"] = "НЕИЗВЕСТНЫЙ_СПОСОБ"; hidden["visibility"] = "hidden";
            wound["treatment"]!["routes"]!.AsArray().Add(hidden);
            wound["treatment"]!["diagnosisPaths"] = new JsonArray(new JsonObject
            { ["diagnosisPathId"] = "examine_detail", ["displayName"] = "Осмотр духовной ткани", ["visibility"] = "public",
                ["requiresKnownFacts"] = new JsonArray(), ["requirements"] = new JsonArray(), ["check"] = new JsonObject(),
                ["reveals"] = new JsonArray("route:route_unknown"), ["failurePolicy"] = "no_reveal" });
        });
        var effects = WoundForeignDetailTestData.WithEffect(data.Input, kind);
        var results = new List<string>();
        foreach (var browser in new[] { false, true })
        {
            var reads = new CanonicalReadActivityAccumulator(); var mutationPaths = new List<string>(); var armed = false;
            await using var fixture = await WoundPlayerReadFixture.CreateAsync(output, realm: "chaos_sea", suppliedInput: effects.Input, reads: reads,
                beforeMutation: path => { if (armed) mutationPaths.Add(path); });
            await using (var prep = await fixture.Files.AcquireCanonicalWriteLeaseAsync())
            {
                await fixture.Files.WriteFileAtomicAsync(prep, AfterlifeWoundTreatmentAuthority.AidLocalityPaths[0], data.Guardians.ToJsonString());
                await fixture.Files.WriteFileAtomicAsync(prep, GuardianAbodeResidentState.StatePath, data.Residents.ToJsonString());
                await fixture.Files.WriteFileAtomicAsync(prep, EffectIdentityState.StatePath, effects.Identity.ToJsonString());
                _ = fixture.Files.GetOrCreateSessionGeneration(prep);
            }
            var repairs = 0;
            var state = new StateManager(fixture.Files, new GameSettings(), NullLogger<StateManager>.Instance,
                new StateManagerHooks { AfterPlayerSoulProfileInputsReadAsync = () => { repairs++; return Task.CompletedTask; } });
            var before = fixture.Snapshot(); armed = true;
            var paths = new[] { AfterlifeWoundTreatmentAuthority.AidLocalityPaths[0], GuardianAbodeResidentState.StatePath };
            var countBefore = paths.Select(reads.ReadCount).ToArray();
            string text;
            if (browser)
            {
                var commands = new ExplorerWebCommandService(fixture.Files, state, new LocalizationManager(),
                    new ValidationService(fixture.Files, NullLogger<ValidationService>.Instance));
                var first = await commands.ExecuteAsync(new("/лечить", "detail_browser"));
                var options = Assert.IsType<UiSelectionPrompt>(Assert.Single(first.Prompts));
                var option = Assert.Single(options.Options, o => o.Label == (kind == "guardian" ? "Хранитель подробностей" : "Житель подробностей"));
                var card = await commands.SubmitPromptSessionAsync(new(first.InteractiveSession!.SessionId,
                    new() { ["wound_target"] = JsonValue.Create(option.Value) }, "detail_browser"));
                Assert.Equal(CommandExecutionState.Completed, card.State); Assert.Empty(card.Prompts); Assert.Empty(card.Actions);
                Assert.Null(card.InteractiveSession); Assert.DoesNotContain(card.Notifications, n => n.Severity == UiNotificationSeverity.Error);
                text = JsonSerializer.Serialize(card, new JsonSerializerOptions { Encoder = JavaScriptEncoder.UnsafeRelaxedJsonEscaping });
            }
            else
            {
                var console = new TestExplorerConsole();
                console.QueueAnySelection(kind == "guardian" ? "2. Хранитель подробностей" : "3. Житель подробностей");
                var locks = new LocalUiSessionLockService(fixture.Files);
                var explorer = new ExplorerMode(state, fixture.Files, new LocalizationManager(), console: console,
                    localUiSessionLockService: locks, localUiSessionLockOwner: new("detail_console", "console", "Карточка раны", TimeSpan.FromSeconds(120)));
                Assert.Equal("", await explorer.TryProcessCommand("/лечить"));
                Assert.Single(console.SelectionTitles); Assert.Empty(console.ConfirmPrompts);
                using var writer = new StringWriter();
                var renderer = AnsiConsole.Create(new AnsiConsoleSettings { Ansi = AnsiSupport.No, ColorSystem = ColorSystemSupport.NoColors,
                    Interactive = InteractionSupport.No, Out = new AnsiConsoleOutput(writer) }); renderer.Profile.Width = 240;
                foreach (var item in console.Rendered) renderer.Write(item);
                foreach (var line in console.MarkupLines) renderer.MarkupLine(line);
                text = writer.ToString(); Assert.DoesNotContain("Ошибка", text);
                var owned = Assert.IsType<LocalUiSessionLockSnapshot>(await locks.InspectAsync());
                Assert.Equal("detail_console", owned.OwnerId);
                Assert.True(await locks.ReleaseAsync(new(owned.SessionGeneration, owned.OwnerId, owned.LeaseToken)));
            }
            Assert.Equal(0, repairs); Assert.Equal(before, fixture.Snapshot());
            Assert.NotEmpty(mutationPaths); Assert.All(mutationPaths, path => Assert.Equal(LocalUiSessionLockService.LockPath, path));
            for (var i = 0; i < paths.Length; i++) Assert.Equal(2L, reads.ReadCount(paths[i]) - countBefore[i]);
            foreach (var token in new[] { "Известная рана обитателя", symptom, "Видимая помеха обитателю", "Укрепить духовную ткань", "Осмотр духовной ткани", "рекомендация", "пока недоступно" })
                Assert.Contains(token, text);
            foreach (var token in new[] { "ЛИЧНАЯ_РАНА_ИГРОКА", "НЕИЗВЕСТНЫЙ_СПОСОБ", "ПРИВАТНЫЕ_УСЛОВИЯ", "wound_foreign_detail", "foreign_effect_", "route_unknown", "sha256:" })
                Assert.DoesNotContain(token, text);
            Assert.Null(await fixture.Files.ReadFileBytesAsync(PendingPlayerActionService.PendingPath));
            results.Add(text);
        }
        Assert.Equal(2, results.Count); // Each surface independently proved the same literal known facts and inert canonical state.
    }
}
