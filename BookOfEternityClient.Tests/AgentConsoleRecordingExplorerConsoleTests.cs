using BookOfEternityClient.AgentConsole;
using BookOfEternityClient.Core;
using BookOfEternityClient.UI;
using Spectre.Console;
using Spectre.Console.Rendering;
using Xunit;

namespace BookOfEternityClient.Tests;

public sealed class AgentConsoleRecordingExplorerConsoleTests
{
    [Fact]
    public void Prompt_SelectionFallbackCapturesChoicesWithoutAgentMetaText()
    {
        var console = new AgentConsoleRecordingExplorerConsole(new PromptlessExplorerConsole());
        var prompt = new SelectionPrompt<string>()
            .Title("Выберите действие")
            .AddChoices("Открыть письмо", "← Назад");

        var selected = console.Prompt(prompt);
        var captured = console.ReadCapturedText();

        Assert.Equal("← Назад", selected);
        Assert.Contains("Выберите действие", captured, StringComparison.Ordinal);
        Assert.Contains("Открыть письмо", captured, StringComparison.Ordinal);
        Assert.Contains("← Назад", captured, StringComparison.Ordinal);
        Assert.DoesNotContain("Agent Console", captured, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("выбран безопасный пункт", captured, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public async Task Confirm_LiveConfirmationPublishesSnapshotAndAcceptsYesAction()
    {
        var store = new AgentConsoleStateStore();
        var liveInput = new AgentConsoleLiveInputSource(store, readTimeout: TimeSpan.FromSeconds(2));
        var console = new AgentConsoleRecordingExplorerConsole(
            new LiveInputExplorerConsole(liveInput),
            liveInput);

        var promptTask = Task.Run(() => console.Confirm("[yellow]Подтвердить локальную прокачку духовного искусства?[/]", defaultValue: false));

        var published = SpinWait.SpinUntil(
            () => store.GetSnapshot()?.InputKind == AgentConsoleInputKind.Confirmation || promptTask.IsCompleted,
            TimeSpan.FromSeconds(1));

        Assert.True(published);
        Assert.False(promptTask.IsCompleted);
        var snapshot = Assert.IsType<AgentConsoleSnapshot>(store.GetSnapshot());
        Assert.Equal(AgentConsoleMode.Confirmation, snapshot.Mode);
        Assert.Equal(AgentConsoleInputKind.Confirmation, snapshot.InputKind);
        Assert.True(snapshot.AwaitingInput);
        Assert.Contains("Подтвердить локальную прокачку духовного искусства", snapshot.PlainText, StringComparison.Ordinal);
        Assert.Contains(snapshot.Actions, action => action.Id == "yes" && action.IsEnabled);
        Assert.Contains(snapshot.Actions, action => action.Id == "no" && action.IsEnabled && action.IsDefault);

        var accepted = liveInput.TryQueueAction(new AgentConsoleActionRequest
        {
            ScreenId = snapshot.ScreenId,
            ActionId = "yes",
            InputKind = AgentConsoleInputKind.Confirmation
        });

        Assert.True(accepted.Accepted, accepted.Message);
        Assert.True(await promptTask.WaitAsync(TimeSpan.FromSeconds(2)));
    }

    [Fact]
    public async Task Ask_LiveTextPromptPublishesSnapshotAndAcceptsTextLine()
    {
        var store = new AgentConsoleStateStore();
        var liveInput = new AgentConsoleLiveInputSource(store, readTimeout: TimeSpan.FromSeconds(2));
        var console = new AgentConsoleRecordingExplorerConsole(
            new LiveInputExplorerConsole(liveInput),
            liveInput);

        var promptTask = Task.Run(() => console.Ask("[cyan]Действие:[/]", defaultValue: "guard"));

        var published = SpinWait.SpinUntil(
            () => store.GetSnapshot()?.InputKind == AgentConsoleInputKind.Text || promptTask.IsCompleted,
            TimeSpan.FromSeconds(1));

        Assert.True(published);
        Assert.False(promptTask.IsCompleted);
        var snapshot = Assert.IsType<AgentConsoleSnapshot>(store.GetSnapshot());
        Assert.Equal(AgentConsoleMode.TextPrompt, snapshot.Mode);
        Assert.Equal(AgentConsoleInputKind.Text, snapshot.InputKind);
        Assert.True(snapshot.AwaitingInput);
        Assert.Contains("Действие:", snapshot.PlainText, StringComparison.Ordinal);
        Assert.Equal("guard", snapshot.Prompt?.DefaultValue);

        var accepted = liveInput.EnqueueLine("recover_spiritual_power");

        Assert.True(accepted.Accepted, accepted.Message);
        Assert.Equal("recover_spiritual_power", await promptTask.WaitAsync(TimeSpan.FromSeconds(2)));
    }

    [Fact]
    public async Task Prompt_LiveIntTextPromptPublishesSnapshotAndAcceptsNumberLine()
    {
        var store = new AgentConsoleStateStore();
        var liveInput = new AgentConsoleLiveInputSource(store, readTimeout: TimeSpan.FromSeconds(2));
        var console = new AgentConsoleRecordingExplorerConsole(
            new LiveInputExplorerConsole(liveInput),
            liveInput);

        var prompt = new TextPrompt<int>("[yellow]Сколько Перьев потратить?[/]")
            .Validate(value => value > 0
                ? ValidationResult.Success()
                : ValidationResult.Error("[red]Нужно потратить хотя бы 1 перо[/]"));
        var promptTask = Task.Run(() => console.Prompt(prompt));

        var published = SpinWait.SpinUntil(
            () => store.GetSnapshot()?.InputKind == AgentConsoleInputKind.Text || promptTask.IsCompleted,
            TimeSpan.FromSeconds(1));

        Assert.True(published);
        Assert.False(promptTask.IsCompleted);
        var snapshot = Assert.IsType<AgentConsoleSnapshot>(store.GetSnapshot());
        Assert.Equal(AgentConsoleMode.TextPrompt, snapshot.Mode);
        Assert.Equal(AgentConsoleInputKind.Text, snapshot.InputKind);
        Assert.True(snapshot.AwaitingInput);
        Assert.Contains("Сколько Перьев потратить", snapshot.PlainText, StringComparison.Ordinal);

        var accepted = liveInput.EnqueueLine("7");

        Assert.True(accepted.Accepted, accepted.Message);
        Assert.Equal(7, await promptTask.WaitAsync(TimeSpan.FromSeconds(2)));
    }

    [Fact]
    public async Task Prompt_LiveSelectionMarksLockedTrainingChoicesAsDisabledActions()
    {
        var store = new AgentConsoleStateStore();
        var liveInput = new AgentConsoleLiveInputSource(store, readTimeout: TimeSpan.FromSeconds(2));
        var console = new AgentConsoleRecordingExplorerConsole(
            new LiveInputExplorerConsole(liveInput),
            liveInput);
        var prompt = new SelectionPrompt<string>()
            .Title("🎓 Самостоятельная прокачка")
            .AddChoices(
                "• Pressure | духовное искусство: самостоятельная прокачка | закрыто: нужно открыть уровень искусства 1",
                "← К обучению души");

        var promptTask = Task.Run(() => console.Prompt(prompt));

        var published = SpinWait.SpinUntil(
            () => store.GetSnapshot()?.InputKind == AgentConsoleInputKind.MenuSelection || promptTask.IsCompleted,
            TimeSpan.FromSeconds(1));

        Assert.True(published);
        Assert.False(promptTask.IsCompleted);
        var snapshot = Assert.IsType<AgentConsoleSnapshot>(store.GetSnapshot());
        var lockedAction = Assert.Single(snapshot.Actions, action => action.Label.Contains("закрыто:", StringComparison.OrdinalIgnoreCase));

        Assert.False(lockedAction.IsEnabled);

        var backAccepted = liveInput.TryQueueReturnToGameLoopStep();

        Assert.True(backAccepted.Accepted, backAccepted.Message);
        Assert.Equal("← К обучению души", await promptTask.WaitAsync(TimeSpan.FromSeconds(2)));
    }

    private sealed class PromptlessExplorerConsole : IExplorerConsole
    {
        public bool KeyAvailable => false;

        public void Clear()
        {
        }

        public void Write(IRenderable content)
        {
        }

        public void WriteLine()
        {
        }

        public void Markup(string markup)
        {
        }

        public void MarkupLine(string markup)
        {
        }

        public string Ask(string prompt, string defaultValue = "") => defaultValue;

        public bool Confirm(string prompt, bool defaultValue = false) => defaultValue;

        public T Prompt<T>(IPrompt<T> prompt) =>
            throw new NotSupportedException("Interactive prompts are unavailable in Agent Console recording mode.");

        public string? ReadLine() => string.Empty;

        public ConsoleKeyInfo ReadKey() => new('\r', ConsoleKey.Enter, false, false, false);
    }

    private sealed class LiveInputExplorerConsole : IExplorerConsole
    {
        private readonly AgentConsoleLiveInputSource _liveInput;

        public LiveInputExplorerConsole(AgentConsoleLiveInputSource liveInput)
        {
            _liveInput = liveInput;
        }

        public bool KeyAvailable => _liveInput.KeyAvailable;

        public void Clear()
        {
        }

        public void Write(IRenderable content)
        {
        }

        public void WriteLine()
        {
        }

        public void Markup(string markup)
        {
        }

        public void MarkupLine(string markup)
        {
        }

        public string Ask(string prompt, string defaultValue = "") => defaultValue;

        public bool Confirm(string prompt, bool defaultValue = false) => defaultValue;

        public T Prompt<T>(IPrompt<T> prompt) =>
            throw new NotSupportedException("Interactive prompts must be published through Agent Console.");

        public string? ReadLine() => _liveInput.ReadLine();

        public ConsoleKeyInfo ReadKey() => _liveInput.ReadKey(intercept: true);
    }
}
