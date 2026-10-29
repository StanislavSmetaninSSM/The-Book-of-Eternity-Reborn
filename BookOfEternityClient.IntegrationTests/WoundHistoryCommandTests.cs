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
using Xunit.Abstractions;

namespace BookOfEternityClient.Tests;

/// <summary>Actual minimal read handlers navigate current known History without game setup or repair.</summary>
public sealed class WoundHistoryCommandTests(ITestOutputHelper output)
{
    private const string Name = "[red]Зажившая[/] <b>рана</b>";

    /// <summary>A retained public action reloads hidden changes rather than borrowing the previous result.</summary>
    [Fact]
    public async Task BrowserHistoryAndBackUseFreshReadOnlyData()
    {
        var reads = new CanonicalReadActivityAccumulator(); var mutations = new List<string>(); var armed = false;
        await using var fixture = await WoundPlayerReadFixture.CreateAsync(output,
            suppliedInput: WoundPlayerHistoryTestData.TerminalInput(name: Name), reads: reads,
            beforeMutation: path => { if (armed) mutations.Add(path); });
        await PrepareGeneration(fixture);
        var repairs = 0; var state = State(fixture, () => repairs++);
        var commands = Commands(fixture, state); armed = true;
        async Task<ExplorerCommandResult> Read(string command)
        {
            var before = fixture.Snapshot(); var count = reads.ReadCount(WoundHistoryState.HistoryPath);
            var result = await commands.ExecuteAsync(new(command, "history_browser"));
            Assert.Equal(CommandExecutionState.Completed, result.State); Assert.Empty(result.Prompts);
            Assert.Null(result.InteractiveSession); Assert.Null(result.PendingGmAction);
            Assert.Equal(before, fixture.Snapshot());
            Assert.Equal(1L, reads.ReadCount(WoundHistoryState.HistoryPath) - count);
            return result;
        }
        var active = await Read("/раны"); Assert.DoesNotContain(Name, Text(active));
        var historyAction = Assert.Single(active.Actions); Assert.Equal("История заживших ран", historyAction.Label);
        var history = await Read(historyAction.Command); Assert.Contains(Name, Text(history));
        Assert.DoesNotContain("нет видимых активных ран", Text(history));
        Assert.Equal("Активные раны", Assert.Single(history.Actions).Label);
        // A controlled accepted-state replacement belongs to fixture preparation,
        // not a read. Reconstruct both terminal History and identity before rearming.
        armed = false;
        var hidden = WoundPlayerHistoryTestData.TerminalInput(hidden: true, name: Name);
        await using (var lease = await fixture.Files.AcquireCanonicalWriteLeaseAsync())
        {
            await fixture.Files.WriteFileAtomicAsync(lease, WoundHistoryState.HistoryPath, WoundHistoryState.SerializeCanonical(hidden.History));
            await fixture.Files.WriteFileAtomicAsync(lease, WoundIdentityState.StatePath, WoundIdentityState.SerializeCanonical(hidden.Identities));
        }
        armed = true;
        var current = await Read(historyAction.Command);
        Assert.Contains("История заживших ран пока пуста.", Text(current)); Assert.DoesNotContain(Name, Text(current));
        var back = await Read(Assert.Single(current.Actions).Command);
        Assert.Contains("нет видимых активных ран", Text(back)); Assert.DoesNotContain(Name, Text(back));
        Assert.Equal(0, repairs);
        Assert.All(mutations, path => Assert.Equal(LocalUiSessionLockService.LockPath, path));
        Assert.Null(await fixture.Files.ReadFileBytesAsync(PendingPlayerActionService.PendingPath));
    }

    /// <summary>The console follows the same public actions, safely renders text and cancels without treatment.</summary>
    [Fact]
    public async Task ConsoleHistoryBackAndCloseAreLiteralAndInert()
    {
        var reads = new CanonicalReadActivityAccumulator(); var mutations = new List<string>(); var armed = false;
        await using var fixture = await WoundPlayerReadFixture.CreateAsync(output,
            suppliedInput: WoundPlayerHistoryTestData.TerminalInput(name: Name), reads: reads,
            beforeMutation: path => { if (armed) mutations.Add(path); });
        await PrepareGeneration(fixture); var repairs = 0; var state = State(fixture, () => repairs++);
        var console = new TestExplorerConsole(); console.QueueAnySelection("История заживших ран", "Активные раны", "Закрыть");
        var locks = new LocalUiSessionLockService(fixture.Files);
        var explorer = Explorer(fixture, state, console, locks); var before = fixture.Snapshot();
        var count = reads.ReadCount(WoundHistoryState.HistoryPath); armed = true;
        Assert.Equal("", await explorer.TryProcessCommand("/wounds"));
        Assert.Equal(3, console.SelectionTitles.Count); Assert.Empty(console.ConfirmPrompts);
        Assert.All(console.SelectionTitles, title => Assert.Equal("Действие с ранами", title));
        Assert.Equal(new[] { "Закрыть", "История заживших ран" }, console.SelectionChoicesHistory[0].Choices);
        Assert.Equal(new[] { "Закрыть", "Активные раны" }, console.SelectionChoicesHistory[1].Choices);
        var text = Render(console); Assert.Contains(Name, text); Assert.DoesNotContain("Ошибка", text);
        Assert.Equal(1, text.Split(Name, StringSplitOptions.None).Length - 1);
        Assert.Equal(3L, reads.ReadCount(WoundHistoryState.HistoryPath) - count);
        await ReleaseOwnLock(locks);
        Assert.Equal(before, fixture.Snapshot()); Assert.Equal(0, repairs);
        Assert.All(mutations, path => Assert.Equal(LocalUiSessionLockService.LockPath, path));
        Assert.Null(await fixture.Files.ReadFileBytesAsync(PendingPlayerActionService.PendingPath));
    }

    /// <summary>Both canonical and English History commands invoke the same real read handler.</summary>
    [Theory]
    [InlineData("/раны история")]
    [InlineData("/wounds history")]
    public async Task DirectHistoryAliasShowsKnownRecordWithoutASecondSelection(string command)
    {
        var reads = new CanonicalReadActivityAccumulator(); var mutations = new List<string>(); var armed = false;
        await using var fixture = await WoundPlayerReadFixture.CreateAsync(output,
            suppliedInput: WoundPlayerHistoryTestData.TerminalInput(name: Name), reads: reads,
            beforeMutation: path => { if (armed) mutations.Add(path); });
        await PrepareGeneration(fixture); var repairs = 0; var state = State(fixture, () => repairs++);
        var before = fixture.Snapshot(); var count = reads.ReadCount(WoundHistoryState.HistoryPath); armed = true;
        var result = await Commands(fixture, state).ExecuteAsync(new(command, "history_alias"));
        Assert.Equal(CommandExecutionState.Completed, result.State); Assert.Empty(result.Prompts); Assert.Null(result.PendingGmAction);
        Assert.Contains(Name, Text(result)); Assert.Equal("Активные раны", Assert.Single(result.Actions).Label);
        Assert.Equal(1L, reads.ReadCount(WoundHistoryState.HistoryPath) - count);
        Assert.Equal(before, fixture.Snapshot()); Assert.Equal(0, repairs);
        Assert.All(mutations, path => Assert.Equal(LocalUiSessionLockService.LockPath, path));
    }

    /// <summary>Invalid History syntax is a local error, never a GM action or an implicit active read.</summary>
    [Theory]
    [InlineData(false, "/раны неизвестно")]
    [InlineData(false, "/wounds history extra")]
    [InlineData(true, "/раны неизвестно")]
    [InlineData(true, "/wounds history extra")]
    public async Task InvalidHistorySyntaxNeverFallsThroughToGameplay(bool browser, string command)
    {
        var reads = new CanonicalReadActivityAccumulator(); var mutations = new List<string>(); var armed = false;
        await using var fixture = await WoundPlayerReadFixture.CreateAsync(output,
            suppliedInput: WoundPlayerHistoryTestData.TerminalInput(), reads: reads,
            beforeMutation: path => { if (armed) mutations.Add(path); });
        await PrepareGeneration(fixture); var repairs = 0; var state = State(fixture, () => repairs++);
        var before = fixture.Snapshot(); var count = reads.ReadCount(WoundHistoryState.HistoryPath); armed = true;
        if (browser)
        {
            var result = await Commands(fixture, state).ExecuteAsync(new(command, "history_invalid"));
            Assert.Equal(CommandExecutionState.Failed, result.State); Assert.Null(result.PendingGmAction);
            Assert.Empty(result.Prompts); Assert.Empty(result.Actions);
        }
        else
        {
            var console = new TestExplorerConsole(); var locks = new LocalUiSessionLockService(fixture.Files);
            Assert.Equal("", await Explorer(fixture, state, console, locks).TryProcessCommand(command));
            Assert.Empty(console.SelectionTitles); Assert.Empty(console.ConfirmPrompts);
            Assert.NotEmpty(Render(console)); await ReleaseOwnLock(locks);
        }
        Assert.Equal(0L, reads.ReadCount(WoundHistoryState.HistoryPath) - count);
        Assert.Equal(before, fixture.Snapshot()); Assert.Equal(0, repairs);
        Assert.All(mutations, path => Assert.Equal(LocalUiSessionLockService.LockPath, path));
        Assert.Null(await fixture.Files.ReadFileBytesAsync(PendingPlayerActionService.PendingPath));
    }

    private static async Task PrepareGeneration(WoundPlayerReadFixture fixture)
    { await using var lease = await fixture.Files.AcquireCanonicalWriteLeaseAsync(); _ = fixture.Files.GetOrCreateSessionGeneration(lease); }

    private static StateManager State(WoundPlayerReadFixture fixture, Action repair) => new(fixture.Files, new GameSettings(),
        NullLogger<StateManager>.Instance, new StateManagerHooks { AfterPlayerSoulProfileInputsReadAsync = () => { repair(); return Task.CompletedTask; } });

    private static ExplorerWebCommandService Commands(WoundPlayerReadFixture fixture, StateManager state) =>
        new(fixture.Files, state, new LocalizationManager(), new ValidationService(fixture.Files, NullLogger<ValidationService>.Instance));

    private static ExplorerMode Explorer(WoundPlayerReadFixture fixture, StateManager state, TestExplorerConsole console, LocalUiSessionLockService locks) =>
        new(state, fixture.Files, new LocalizationManager(), console: console, localUiSessionLockService: locks,
            localUiSessionLockOwner: new("history_console", "console", "История ран", TimeSpan.FromSeconds(120)));

    private static async Task ReleaseOwnLock(LocalUiSessionLockService locks)
    {
        if (await locks.InspectAsync() is not { } owned) return;
        Assert.Equal("history_console", owned.OwnerId);
        Assert.True(await locks.ReleaseAsync(new(owned.SessionGeneration, owned.OwnerId, owned.LeaseToken)));
    }

    private static string Text(object value) => JsonSerializer.Serialize(value,
        new JsonSerializerOptions { Encoder = JavaScriptEncoder.UnsafeRelaxedJsonEscaping });

    private static string Render(TestExplorerConsole console)
    {
        using var writer = new StringWriter();
        var renderer = AnsiConsole.Create(new AnsiConsoleSettings { Ansi = AnsiSupport.No, ColorSystem = ColorSystemSupport.NoColors,
            Interactive = InteractionSupport.No, Out = new AnsiConsoleOutput(writer) }); renderer.Profile.Width = 240;
        foreach (var item in console.Rendered) renderer.Write(item);
        foreach (var line in console.MarkupLines) renderer.MarkupLine(line);
        return writer.ToString();
    }
}
