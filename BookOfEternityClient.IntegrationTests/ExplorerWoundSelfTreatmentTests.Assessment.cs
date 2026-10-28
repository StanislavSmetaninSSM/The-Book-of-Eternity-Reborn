using System.Security.Cryptography;
using System.Text.Encodings.Web;
using System.Text.Json;
using BookOfEternityClient.CommandProtocol;
using BookOfEternityClient.Configuration;
using BookOfEternityClient.Core;
using BookOfEternityClient.Services;
using BookOfEternityClient.UI;
using BookOfEternityClient.WebUi;
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
        var mutations = new List<string>();
        var uiLockMutations = 0;
        await using var fixture = await HealingFixture.CreateAsync(2, tier, 20, stageRequest: false, realm: realm,
            hooks: new FileSystemManagerHooks { BeforeCanonicalMutationBoundaryAsync = _ =>
                { if (armed) { if (_ == LocalUiSessionLockService.LockPath) uiLockMutations++; else mutations.Add(_); }
                    return Task.CompletedTask; } });
        string generation;
        await using (var lease = await fixture.Files.AcquireCanonicalWriteLeaseAsync())
            generation = fixture.Files.GetOrCreateSessionGeneration(lease);
        var repairs = 0;
        var state = new StateManager(fixture.Files, new GameSettings(), NullLogger<StateManager>.Instance,
            new StateManagerHooks { AfterPlayerSoulProfileInputsReadAsync = () => { repairs++; return Task.CompletedTask; } });
        var console = new TestExplorerConsole();
        var locks = new LocalUiSessionLockService(fixture.Files);
        var owner = new LocalUiSessionLockOwner("wound-assessment-console", "console_explorer", "Оценка раны", TimeSpan.FromMinutes(2));
        var explorer = new ExplorerMode(state, fixture.Files, new LocalizationManager(), console: console,
            localUiSessionLockService: locks, localUiSessionLockOwner: owner);
        Assert.Null(await locks.InspectAsync());
        async Task<LocalUiSessionLockSnapshot> OwnedLock()
        {
            var current = Assert.IsType<LocalUiSessionLockSnapshot>(await locks.InspectAsync());
            Assert.True(current.IsReadable);
            Assert.False(current.IsStale);
            Assert.Equal(owner.OwnerId, current.OwnerId);
            Assert.Equal(owner.OwnerKind, current.OwnerKind);
            Assert.Equal(generation, current.SessionGeneration);
            Assert.False(string.IsNullOrWhiteSpace(current.LeaseToken));
            return current;
        }
        string[] Snapshot() => Directory.EnumerateFileSystemEntries(fixture.Files.GameSessionPath, "*", SearchOption.AllDirectories)
            .Select(p => (Path: p, Relative: Path.GetRelativePath(fixture.Files.GameSessionPath, p).Replace('\\', '/')))
            // UI transport ownership is checked separately; every other control child remains in the gameplay snapshot.
            .Where(p => p.Relative != LocalUiSessionLockService.LockPath && p.Relative != "game_state/control")
            .OrderBy(p => p.Relative, StringComparer.Ordinal).Select(p => p.Relative + ":" +
                (Directory.Exists(p.Path) ? "directory" : Convert.ToHexString(SHA256.HashData(File.ReadAllBytes(p.Path))))).ToArray();
        var before = Snapshot();
        armed = true;
        Assert.Equal(string.Empty, await explorer.TryProcessCommand("/лечить"));
        var consoleLock = await OwnedLock();
        Assert.True(uiLockMutations > 0);
        var consoleLockWrites = uiLockMutations;
        output.WriteLine(JsonSerializer.Serialize(new { realm, tier, phase = "console", mutations, uiLockMutations, repairs }));
        Assert.Equal(before, Snapshot());
        Assert.Empty(mutations);
        Assert.Empty(console.SelectionTitles);
        Assert.Empty(console.ConfirmPrompts);
        var commands = new ExplorerWebCommandService(fixture.Files, state, new LocalizationManager(),
            new ValidationService(fixture.Files, NullLogger<ValidationService>.Instance));
        var browser = await commands.ExecuteAsync(new("/treat", Owner));
        Assert.Equal(CommandExecutionState.Completed, browser.State);
        Assert.Empty(browser.Prompts);
        Assert.Empty(browser.Actions);
        Assert.Null(browser.InteractiveSession);
        output.WriteLine(JsonSerializer.Serialize(new { realm, tier, phase = "browser", mutations, uiLockMutations, repairs }));
        Assert.Equal(consoleLock, await OwnedLock());
        Assert.Equal(consoleLockWrites, uiLockMutations);
        Assert.Equal(before, Snapshot());
        Assert.Empty(mutations);
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
        output.WriteLine($"Known II / H{tier} / {realm}: both real handlers; verified console transport lock only; no prompt/action/gameplay write/repair or gameplay tree change.");
    }
}
