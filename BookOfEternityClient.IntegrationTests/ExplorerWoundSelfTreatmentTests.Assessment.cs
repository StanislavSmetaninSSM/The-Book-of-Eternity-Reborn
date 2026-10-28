using System.Security.Cryptography;
using System.Text.Encodings.Web;
using System.Text.Json;
using BookOfEternityClient.CommandProtocol;
using BookOfEternityClient.Configuration;
using BookOfEternityClient.Core;
using BookOfEternityClient.Services;
using BookOfEternityClient.UI;
using Microsoft.Extensions.Logging.Abstractions;
using Spectre.Console;
using Xunit;
using HealingFixture = BookOfEternityClient.Tests.AfterlifeWoundHealingLifecycleTests.Fixture;

namespace BookOfEternityClient.Tests;

public sealed partial class ExplorerWoundSelfTreatmentTests
{
    [Theory]
    [InlineData("Chaos Sea", 0)]
    [InlineData("Chaos Sea", 1)]
    [InlineData("Shining Abode", 0)]
    [InlineData("Shining Abode", 1)]
    public async Task InsufficientHealingAssessmentIsReadonlyThroughActualConsoleAndBrowser(string realm, int tier)
    {
        var armed = false;
        var mutations = 0;
        await using var fixture = await HealingFixture.CreateAsync(2, tier, 20, stageRequest: false, realm: realm,
            hooks: new FileSystemManagerHooks { BeforeCanonicalMutationBoundaryAsync = _ =>
                { if (armed) mutations++; return Task.CompletedTask; } });
        await using (var lease = await fixture.Files.AcquireCanonicalWriteLeaseAsync())
            _ = fixture.Files.GetOrCreateSessionGeneration(lease);
        var repairs = 0;
        var state = new StateManager(fixture.Files, new GameSettings(), NullLogger<StateManager>.Instance,
            new StateManagerHooks { AfterPlayerSoulProfileInputsReadAsync = () => { repairs++; return Task.CompletedTask; } });
        var console = new TestExplorerConsole();
        var explorer = new ExplorerMode(state, fixture.Files, new LocalizationManager(), console: console);
        string[] Snapshot() => Directory.EnumerateFileSystemEntries(fixture.Files.GameSessionPath, "*", SearchOption.AllDirectories)
            .OrderBy(p => p, StringComparer.Ordinal).Select(p => Path.GetRelativePath(fixture.Files.GameSessionPath, p) + ":" +
                (Directory.Exists(p) ? "directory" : Convert.ToHexString(SHA256.HashData(File.ReadAllBytes(p))))).ToArray();
        var before = Snapshot();
        armed = true;
        Assert.Equal(string.Empty, await explorer.TryProcessCommand("/лечить"));
        Assert.Equal(before, Snapshot());
        Assert.Empty(console.SelectionTitles);
        Assert.Empty(console.ConfirmPrompts);
        var browser = await CreateCommands(fixture.Files).ExecuteAsync(new("/treat", Owner));
        Assert.Equal(CommandExecutionState.Completed, browser.State);
        Assert.Empty(browser.Prompts);
        Assert.Empty(browser.Actions);
        Assert.Null(browser.InteractiveSession);
        Assert.Equal(before, Snapshot());
        Assert.Equal(0, mutations);
        Assert.Equal(0, repairs);
        await using (var lease = await fixture.Files.AcquireCanonicalWriteLeaseAsync())
            Assert.Null(explorer.ConsumePendingSpiritualSelfTreatment(WoundApplicationService.ActionText,
                fixture.Files.GetOrCreateSessionGeneration(lease)));
        using var writer = new StringWriter();
        var renderer = AnsiConsole.Create(new AnsiConsoleSettings { Ansi = AnsiSupport.No,
            ColorSystem = ColorSystemSupport.NoColors, Interactive = InteractionSupport.No, Out = new AnsiConsoleOutput(writer) });
        renderer.Profile.Width = 200;
        foreach (var rendered in console.Rendered) renderer.Write(rendered);
        foreach (var line in console.MarkupLines) renderer.MarkupLine(line);
        var browserText = JsonSerializer.Serialize(browser, new JsonSerializerOptions { Encoder = JavaScriptEncoder.UnsafeRelaxedJsonEscaping });
        foreach (var text in new[] { writer.ToString(), browserText })
        {
            Assert.Contains($"Исцеление: {tier}", text);
            Assert.Contains("ступень 2", text);
            Assert.Contains("только оценка", text);
            Assert.DoesNotContain("wound_spiritual_session", text);
        }
        foreach (var path in new[] { AcceptedMechanicsPlan.WoundCommandPath, PendingPlayerActionService.PendingPath,
                     LiveTurnPreparationService.PendingTurnSnapshotManifestPath, "input/turn_request.json" })
            Assert.Null(await fixture.Files.ReadFileBytesAsync(path));
        output.WriteLine($"Known II / H{tier} / {realm}: both real handlers; no prompt/action/write/repair or canonical tree change.");
    }
}
