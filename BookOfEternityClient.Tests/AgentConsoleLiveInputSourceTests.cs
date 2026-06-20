using BookOfEternityClient.AgentConsole;
using Xunit;

namespace BookOfEternityClient.Tests;

public sealed class AgentConsoleLiveInputSourceTests
{
    [Fact]
    public void ReadKey_ReturnsQueuedConsoleKeyInfo()
    {
        var store = new AgentConsoleStateStore();
        var input = new AgentConsoleLiveInputSource(store, readTimeout: TimeSpan.FromMilliseconds(100));
        var key = new ConsoleKeyInfo('x', ConsoleKey.X, shift: false, alt: false, control: false);

        var result = input.EnqueueKey(key);

        Assert.True(result.Accepted);
        Assert.True(input.KeyAvailable);
        Assert.Equal(key, input.ReadKey(intercept: true));
        Assert.False(input.KeyAvailable);
        var agentEvent = Assert.Single(store.GetEvents());
        Assert.Equal(AgentConsoleEventKind.InputAccepted, agentEvent.Kind);
        Assert.Equal(AgentConsoleInputKind.Key, agentEvent.InputKind);
    }

    [Fact]
    public void ReadLine_ReturnsQueuedTextLine()
    {
        var store = new AgentConsoleStateStore();
        var input = new AgentConsoleLiveInputSource(store, readTimeout: TimeSpan.FromMilliseconds(100));

        var result = input.EnqueueLine("look north");

        Assert.True(result.Accepted);
        Assert.False(input.KeyAvailable);
        Assert.Equal("look north", input.ReadLine());
        var agentEvent = Assert.Single(store.GetEvents());
        Assert.Equal(AgentConsoleEventKind.InputAccepted, agentEvent.Kind);
        Assert.Equal(AgentConsoleInputKind.Text, agentEvent.InputKind);
    }

    [Fact]
    public void TryQueueAction_WhenEnabledShortcutMatchesCurrentSnapshot_QueuesResolvedKey()
    {
        var store = new AgentConsoleStateStore();
        var input = new AgentConsoleLiveInputSource(store, readTimeout: TimeSpan.FromMilliseconds(100));
        store.UpdateSnapshot(BuildMenuSnapshot("main-menu", selectedIndex: 0) with
        {
            Actions =
            [
                new AgentConsoleAction
                {
                    Id = "continue",
                    Label = "Continue",
                    Shortcut = "Enter"
                }
            ]
        });

        var result = input.TryQueueAction(new AgentConsoleActionRequest
        {
            ActionId = "continue",
            ScreenId = "main-menu",
            InputKind = AgentConsoleInputKind.MenuSelection
        });

        Assert.True(result.Accepted);
        Assert.Equal(AgentConsoleInputRejectionCode.None, result.RejectionCode);
        Assert.Equal(ConsoleKey.Enter, input.ReadKey(intercept: true).Key);
        Assert.Equal(AgentConsoleEventKind.InputAccepted, result.Event.Kind);
        Assert.Equal("main-menu", result.Event.ScreenId);
    }

    [Fact]
    public void PublishSnapshot_UpdatesSharedStoreForApiReaders()
    {
        var store = new AgentConsoleStateStore();
        var input = new AgentConsoleLiveInputSource(store, readTimeout: TimeSpan.FromMilliseconds(100));
        var snapshot = BuildMenuSnapshot("main-menu", selectedIndex: 0);

        var agentEvent = input.PublishSnapshot(snapshot, "Rendered main menu.");

        Assert.Same(snapshot, store.GetSnapshot());
        Assert.Equal(AgentConsoleEventKind.ScreenRendered, agentEvent.Kind);
        Assert.Equal("main-menu", agentEvent.ScreenId);
        Assert.Equal("Rendered main menu.", agentEvent.Message);
    }

    [Fact]
    public void TryQueueAction_WhenSelectedActionHasNoShortcut_QueuesEnter()
    {
        var store = new AgentConsoleStateStore();
        var input = new AgentConsoleLiveInputSource(store, readTimeout: TimeSpan.FromMilliseconds(100));
        store.UpdateSnapshot(BuildMenuSnapshot("main-menu", selectedIndex: 1));

        var result = input.TryQueueAction(new AgentConsoleActionRequest
        {
            ActionId = "exit",
            ScreenId = "main-menu"
        });

        Assert.True(result.Accepted);
        Assert.Equal(ConsoleKey.Enter, input.ReadKey(intercept: true).Key);
    }

    [Fact]
    public void TryQueueAction_WhenActionIsDisabled_RejectsWithoutConsumingQueuedInput()
    {
        var store = new AgentConsoleStateStore();
        var input = new AgentConsoleLiveInputSource(store, readTimeout: TimeSpan.FromMilliseconds(100));
        store.UpdateSnapshot(BuildMenuSnapshot("main-menu", selectedIndex: 0) with
        {
            Actions =
            [
                new AgentConsoleAction
                {
                    Id = "continue",
                    Label = "Continue",
                    Shortcut = "Enter",
                    IsEnabled = false
                }
            ]
        });
        input.EnqueueKey(new ConsoleKeyInfo('\0', ConsoleKey.Escape, shift: false, alt: false, control: false));

        var result = input.TryQueueAction(new AgentConsoleActionRequest
        {
            ActionId = "continue",
            ScreenId = "main-menu"
        });

        Assert.False(result.Accepted);
        Assert.Equal(AgentConsoleInputRejectionCode.ActionDisabled, result.RejectionCode);
        Assert.Equal(AgentConsoleEventKind.InputRejected, result.Event.Kind);
        Assert.True(input.KeyAvailable);
        Assert.Equal(ConsoleKey.Escape, input.ReadKey(intercept: true).Key);
    }

    [Fact]
    public void TryQueueAction_WhenActionIsMissing_RejectsWithoutConsumingQueuedInput()
    {
        var store = new AgentConsoleStateStore();
        var input = new AgentConsoleLiveInputSource(store, readTimeout: TimeSpan.FromMilliseconds(100));
        input.EnqueueLine("already queued");
        store.UpdateSnapshot(BuildMenuSnapshot("main-menu", selectedIndex: 0));

        var result = input.TryQueueAction(new AgentConsoleActionRequest
        {
            ActionId = "missing",
            ScreenId = "main-menu"
        });

        Assert.False(result.Accepted);
        Assert.Equal(AgentConsoleInputRejectionCode.ActionMissing, result.RejectionCode);
        Assert.Equal(AgentConsoleEventKind.InputRejected, result.Event.Kind);
        Assert.Equal("already queued", input.ReadLine());
    }

    [Fact]
    public void TryQueueAction_WhenScreenDoesNotMatchCurrentSnapshot_RejectsWithoutConsumingQueuedInput()
    {
        var store = new AgentConsoleStateStore();
        var input = new AgentConsoleLiveInputSource(store, readTimeout: TimeSpan.FromMilliseconds(100));
        input.EnqueueLine("already queued");
        store.UpdateSnapshot(BuildMenuSnapshot("main-menu", selectedIndex: 0));

        var result = input.TryQueueAction(new AgentConsoleActionRequest
        {
            ActionId = "continue",
            ScreenId = "stale-menu"
        });

        Assert.False(result.Accepted);
        Assert.Equal(AgentConsoleInputRejectionCode.ScreenMismatch, result.RejectionCode);
        Assert.Equal(AgentConsoleEventKind.InputRejected, result.Event.Kind);
        Assert.Equal("already queued", input.ReadLine());
    }

    [Fact]
    public async Task CancelPendingReads_UnblocksReadLineWithoutClosingSource()
    {
        var store = new AgentConsoleStateStore();
        var input = new AgentConsoleLiveInputSource(store, readTimeout: TimeSpan.FromSeconds(5));
        var readTask = Task.Run(() => Record.Exception(() => { _ = input.ReadLine(); }));

        await Task.Delay(50);
        input.CancelPendingReads("test cancel");
        var exception = await readTask.WaitAsync(TimeSpan.FromSeconds(1));

        var liveException = Assert.IsType<AgentConsoleLiveInputException>(exception);
        Assert.Equal(AgentConsoleInputReadFailureReason.Cancelled, liveException.Reason);

        input.EnqueueLine("after cancel");
        Assert.Equal("after cancel", input.ReadLine());
    }

    [Fact]
    public async Task InfiniteReadTimeout_WaitsForOperatorInputUntilQueued()
    {
        var store = new AgentConsoleStateStore();
        using var input = new AgentConsoleLiveInputSource(store, readTimeout: Timeout.InfiniteTimeSpan);
        var readTask = Task.Run(() => input.ReadLine());

        await Task.Delay(50);

        Assert.False(readTask.IsCompleted);
        input.EnqueueLine("operator command");
        Assert.Equal("operator command", await readTask.WaitAsync(TimeSpan.FromSeconds(1)));
    }

    [Fact]
    public async Task Shutdown_UnblocksReadKeyWithControlledException()
    {
        var store = new AgentConsoleStateStore();
        var input = new AgentConsoleLiveInputSource(store, readTimeout: TimeSpan.FromSeconds(5));
        var readTask = Task.Run(() => Record.Exception(() => { _ = input.ReadKey(intercept: true); }));

        await Task.Delay(50);
        input.Shutdown("test shutdown");
        var exception = await readTask.WaitAsync(TimeSpan.FromSeconds(1));

        var liveException = Assert.IsType<AgentConsoleLiveInputException>(exception);
        Assert.Equal(AgentConsoleInputReadFailureReason.Shutdown, liveException.Reason);
    }

    [Fact]
    public async Task EnqueueLine_WhenReadKeyIsPending_RejectsWithoutPoisoningQueue()
    {
        var store = new AgentConsoleStateStore();
        var input = new AgentConsoleLiveInputSource(store, readTimeout: TimeSpan.FromSeconds(5));
        var readTask = Task.Run(() => input.ReadKey(intercept: true));

        await Task.Delay(50);
        var rejected = input.EnqueueLine("/directive");

        Assert.False(rejected.Accepted);
        Assert.Equal(AgentConsoleInputRejectionCode.InputKindMismatch, rejected.RejectionCode);
        Assert.False(readTask.IsCompleted);

        var expectedKey = new ConsoleKeyInfo('\0', ConsoleKey.Enter, shift: false, alt: false, control: false);
        var accepted = input.EnqueueKey(expectedKey);

        Assert.True(accepted.Accepted);
        Assert.Equal(expectedKey, await readTask.WaitAsync(TimeSpan.FromSeconds(1)));
        Assert.DoesNotContain(store.GetEvents(), e =>
            e.Kind == AgentConsoleEventKind.InputAccepted &&
            e.InputKind == AgentConsoleInputKind.Text);
    }

    [Fact]
    public void ReadLine_WhenSnapshotPromptIsConsumed_RejectsSecondLineUntilNextPrompt()
    {
        var store = new AgentConsoleStateStore();
        var input = new AgentConsoleLiveInputSource(store, readTimeout: TimeSpan.FromMilliseconds(100));
        store.UpdateSnapshot(BuildTextSnapshot("game-loop"));

        var accepted = input.EnqueueLine("/валидация");
        Assert.True(accepted.Accepted);
        Assert.Equal("/валидация", input.ReadLine());

        var consumedSnapshot = store.GetSnapshot();
        Assert.NotNull(consumedSnapshot);
        Assert.False(consumedSnapshot.AwaitingInput);
        Assert.Equal(AgentConsoleInputKind.None, consumedSnapshot.InputKind);

        var rejected = input.EnqueueLine("/моды");

        Assert.False(rejected.Accepted);
        Assert.Equal(AgentConsoleInputRejectionCode.NotAwaitingInput, rejected.RejectionCode);
    }

    private static AgentConsoleSnapshot BuildMenuSnapshot(string screenId, int selectedIndex)
    {
        var renderedAt = new DateTimeOffset(2026, 5, 31, 10, 0, 0, TimeSpan.Zero);
        return new AgentConsoleSnapshot
        {
            ScreenId = screenId,
            Mode = AgentConsoleMode.Menu,
            Title = "Main Menu",
            PlainText = "Choose your path.",
            AwaitingInput = true,
            InputKind = AgentConsoleInputKind.MenuSelection,
            SelectedIndex = selectedIndex,
            Actions =
            [
                new AgentConsoleAction { Id = "continue", Label = "Continue", IsDefault = selectedIndex == 0 },
                new AgentConsoleAction { Id = "exit", Label = "Exit", IsDefault = selectedIndex == 1 }
            ],
            RenderedAtUtc = renderedAt,
            UpdatedAtUtc = renderedAt
        };
    }

    private static AgentConsoleSnapshot BuildTextSnapshot(string screenId)
    {
        var renderedAt = new DateTimeOffset(2026, 5, 31, 10, 0, 0, TimeSpan.Zero);
        return new AgentConsoleSnapshot
        {
            ScreenId = screenId,
            Mode = AgentConsoleMode.TextPrompt,
            Title = "Your turn",
            PlainText = "What next?",
            AwaitingInput = true,
            InputKind = AgentConsoleInputKind.Text,
            RenderedAtUtc = renderedAt,
            UpdatedAtUtc = renderedAt,
            Prompt = new AgentConsolePrompt
            {
                PromptId = "prompt",
                Text = "What next?",
                InputKind = AgentConsoleInputKind.Text
            }
        };
    }
}
