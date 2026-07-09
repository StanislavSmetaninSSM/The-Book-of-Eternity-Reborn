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
    public void TryQueueAction_WhenMenuActionIsNotSelected_QueuesSelectionDigitAndEnter()
    {
        var store = new AgentConsoleStateStore();
        var input = new AgentConsoleLiveInputSource(store, readTimeout: TimeSpan.FromMilliseconds(100));
        store.UpdateSnapshot(BuildMenuSnapshot("main-menu", selectedIndex: 0));

        var result = input.TryQueueAction(new AgentConsoleActionRequest
        {
            ActionId = "exit",
            ScreenId = "main-menu",
            InputKind = AgentConsoleInputKind.MenuSelection
        });

        Assert.True(result.Accepted);
        Assert.Equal(AgentConsoleInputRejectionCode.None, result.RejectionCode);
        Assert.Equal(ConsoleKey.D2, input.ReadKey(intercept: true).Key);
        Assert.Equal(ConsoleKey.Enter, input.ReadKey(intercept: true).Key);
        Assert.False(input.KeyAvailable);
    }

    [Fact]
    public void TryQueueAction_WhenMenuActionIsAfterNine_QueuesInputValueDigitsAndEnter()
    {
        var store = new AgentConsoleStateStore();
        var input = new AgentConsoleLiveInputSource(store, readTimeout: TimeSpan.FromMilliseconds(100));
        store.UpdateSnapshot(BuildLongMenuSnapshot("guardian-presets", selectedIndex: 0, count: 12));

        var result = input.TryQueueAction(new AgentConsoleActionRequest
        {
            ActionId = "option-11",
            ScreenId = "guardian-presets",
            InputKind = AgentConsoleInputKind.MenuSelection
        });

        Assert.True(result.Accepted);
        Assert.Equal(AgentConsoleInputRejectionCode.None, result.RejectionCode);
        Assert.Equal(ConsoleKey.D1, input.ReadKey(intercept: true).Key);
        Assert.Equal(ConsoleKey.D2, input.ReadKey(intercept: true).Key);
        Assert.Equal(ConsoleKey.Enter, input.ReadKey(intercept: true).Key);
        Assert.False(input.KeyAvailable);
    }

    [Fact]
    public void TryQueueAction_WhenTextPromptChoiceIsSelected_QueuesChoiceTextLine()
    {
        var store = new AgentConsoleStateStore();
        var input = new AgentConsoleLiveInputSource(store, readTimeout: TimeSpan.FromMilliseconds(100));
        store.UpdateSnapshot(BuildTextSnapshot("game-loop") with
        {
            Actions =
            [
                new AgentConsoleAction
                {
                    Id = "option-0",
                    Label = "Inspect the sealed letter."
                },
                new AgentConsoleAction
                {
                    Id = "option-1",
                    Label = "Call the trusted servant."
                }
            ],
            Prompt = new AgentConsolePrompt
            {
                PromptId = "prompt",
                Text = "What next?",
                InputKind = AgentConsoleInputKind.Text,
                Choices =
                [
                    "Inspect the sealed letter.",
                    "Call the trusted servant."
                ]
            }
        });

        var result = input.TryQueueAction(new AgentConsoleActionRequest
        {
            ActionId = "option-1",
            ScreenId = "game-loop",
            InputKind = AgentConsoleInputKind.Text
        });

        Assert.True(result.Accepted);
        Assert.Equal(AgentConsoleInputRejectionCode.None, result.RejectionCode);
        Assert.Equal(AgentConsoleEventKind.InputAccepted, result.Event.Kind);
        Assert.Equal(AgentConsoleInputKind.Text, result.Event.InputKind);
        Assert.Equal("Call the trusted servant.", input.ReadLine());
    }

    [Fact]
    public void TryQueueAction_WhenTextPromptActionHasInputValue_QueuesValueInsteadOfLabel()
    {
        var store = new AgentConsoleStateStore();
        var input = new AgentConsoleLiveInputSource(store, readTimeout: TimeSpan.FromMilliseconds(100));
        store.UpdateSnapshot(BuildTextSnapshot("new-game-guardian-mode") with
        {
            Actions =
            [
                new AgentConsoleAction
                {
                    Id = "option-0",
                    Label = "Создать хранителя (описать текстом)",
                    InputValue = "1"
                },
                new AgentConsoleAction
                {
                    Id = "option-1",
                    Label = "Выбрать извечного хранителя",
                    InputValue = "2"
                }
            ],
            Prompt = new AgentConsolePrompt
            {
                PromptId = "prompt",
                Text = "Выберите способ создания Хранителя.",
                InputKind = AgentConsoleInputKind.Text,
                Choices =
                [
                    "Создать хранителя (описать текстом)",
                    "Выбрать извечного хранителя"
                ]
            }
        });

        var result = input.TryQueueAction(new AgentConsoleActionRequest
        {
            ActionId = "option-1",
            ScreenId = "new-game-guardian-mode",
            InputKind = AgentConsoleInputKind.Text
        });

        Assert.True(result.Accepted);
        Assert.Equal(AgentConsoleInputRejectionCode.None, result.RejectionCode);
        Assert.Equal(AgentConsoleInputKind.Text, result.Event.InputKind);
        Assert.Equal("2", input.ReadLine());
    }

    [Fact]
    public void TryQueueDefaultAction_QueuesCurrentSnapshotDefaultWithoutCallerScreenId()
    {
        var store = new AgentConsoleStateStore();
        var input = new AgentConsoleLiveInputSource(store, readTimeout: TimeSpan.FromMilliseconds(100));
        store.UpdateSnapshot(BuildKeySnapshot("stat-allocation-finished") with
        {
            Actions =
            [
                new AgentConsoleAction
                {
                    Id = "continue",
                    Label = "Продолжить",
                    Shortcut = "Enter",
                    IsDefault = true
                }
            ]
        });

        var result = input.TryQueueDefaultAction();

        Assert.True(result.Accepted);
        Assert.Equal(AgentConsoleInputRejectionCode.None, result.RejectionCode);
        Assert.Equal(AgentConsoleInputKind.Key, result.Event.InputKind);
        Assert.Equal("stat-allocation-finished", result.Event.ScreenId);
        Assert.Equal(ConsoleKey.Enter, input.ReadKey(intercept: true).Key);
    }

    [Fact]
    public void TryQueueDefaultAction_WhenNoEnabledDefaultAction_RejectsWithDiagnostic()
    {
        var store = new AgentConsoleStateStore();
        var input = new AgentConsoleLiveInputSource(store, readTimeout: TimeSpan.FromMilliseconds(100));
        store.UpdateSnapshot(BuildMenuSnapshot("main-menu", selectedIndex: 0) with
        {
            Actions =
            [
                new AgentConsoleAction { Id = "continue", Label = "Continue" },
                new AgentConsoleAction { Id = "exit", Label = "Exit" }
            ]
        });

        var result = input.TryQueueDefaultAction();

        Assert.False(result.Accepted);
        Assert.Equal(AgentConsoleInputRejectionCode.ActionMissing, result.RejectionCode);
        Assert.Equal(AgentConsoleEventKind.InputRejected, result.Event.Kind);
        Assert.Equal("main-menu", result.Event.ScreenId);
        Assert.Contains("default", result.Message, StringComparison.OrdinalIgnoreCase);
        Assert.False(input.KeyAvailable);
    }

    [Fact]
    public void TryQueueReturnToGameLoopStep_WhenLocalCommandWaitsForKey_QueuesContinuation()
    {
        var store = new AgentConsoleStateStore();
        var input = new AgentConsoleLiveInputSource(store, readTimeout: TimeSpan.FromMilliseconds(100));
        store.UpdateSnapshot(BuildKeySnapshot("explorer-command-6") with
        {
            Actions =
            [
                new AgentConsoleAction
                {
                    Id = "continue",
                    Label = "Продолжить",
                    Shortcut = "Enter",
                    IsDefault = true
                }
            ]
        });

        var result = input.TryQueueReturnToGameLoopStep();

        Assert.True(result.Accepted);
        Assert.Equal(AgentConsoleInputRejectionCode.None, result.RejectionCode);
        Assert.Equal(ConsoleKey.Enter, input.ReadKey(intercept: true).Key);
    }

    [Fact]
    public void TryQueueReturnToGameLoopStep_WhenLocalCommandMenuHasBackAction_QueuesBackSelection()
    {
        var store = new AgentConsoleStateStore();
        var input = new AgentConsoleLiveInputSource(store, readTimeout: TimeSpan.FromMilliseconds(100));
        store.UpdateSnapshot(BuildMenuSnapshot("explorer-selection-11", selectedIndex: 0) with
        {
            Title = "Действие:",
            PlainText = "Выберите действие.",
            Actions =
            [
                new AgentConsoleAction { Id = "option-0", Label = "Открыть раздел", InputValue = "1", IsDefault = true },
                new AgentConsoleAction { Id = "option-1", Label = "← Назад", InputValue = "2" }
            ]
        });

        var result = input.TryQueueReturnToGameLoopStep();

        Assert.True(result.Accepted);
        Assert.Equal(AgentConsoleInputRejectionCode.None, result.RejectionCode);
        Assert.Equal(ConsoleKey.D2, input.ReadKey(intercept: true).Key);
        Assert.Equal(ConsoleKey.Enter, input.ReadKey(intercept: true).Key);
    }

    [Fact]
    public void TryQueueReturnToGameLoopStep_WhenTrainingMenuReturnsToTeachers_QueuesBackSelection()
    {
        var store = new AgentConsoleStateStore();
        var input = new AgentConsoleLiveInputSource(store, readTimeout: TimeSpan.FromMilliseconds(100));
        store.UpdateSnapshot(BuildMenuSnapshot("explorer-selection-15", selectedIndex: 0) with
        {
            Title = "🎓 Мириэль Пепельная Звезда: предложения",
            PlainText = "🎓 Мириэль Пепельная Звезда: предложения",
            Actions =
            [
                new AgentConsoleAction { Id = "option-0", Label = "• Защита | духовное искусство" },
                new AgentConsoleAction { Id = "option-1", Label = "← К учителям" }
            ]
        });

        var result = input.TryQueueReturnToGameLoopStep();

        Assert.True(result.Accepted);
        Assert.Equal(AgentConsoleInputRejectionCode.None, result.RejectionCode);
        Assert.Equal(ConsoleKey.D2, input.ReadKey(intercept: true).Key);
        Assert.Equal(ConsoleKey.Enter, input.ReadKey(intercept: true).Key);
    }

    [Fact]
    public void TryQueueReturnToGameLoopStep_WhenNpcSectionMenuHasCloseAction_QueuesCloseSelection()
    {
        var store = new AgentConsoleStateStore();
        var input = new AgentConsoleLiveInputSource(store, readTimeout: TimeSpan.FromMilliseconds(100));
        store.UpdateSnapshot(BuildMenuSnapshot("explorer-selection-12", selectedIndex: 0) with
        {
            Title = "Разделы НПС: Ночной сторож архива",
            PlainText = "Разделы НПС: Ночной сторож архива",
            Actions =
            [
                new AgentConsoleAction { Id = "option-0", Label = "Личность / маски — 2 записи", InputValue = "1" },
                new AgentConsoleAction { Id = "option-1", Label = "← Закрыть разделы НПС", InputValue = "2", IsDefault = true }
            ]
        });

        var result = input.TryQueueReturnToGameLoopStep();

        Assert.True(result.Accepted);
        Assert.Equal(AgentConsoleInputRejectionCode.None, result.RejectionCode);
        Assert.Equal(ConsoleKey.D2, input.ReadKey(intercept: true).Key);
        Assert.Equal(ConsoleKey.Enter, input.ReadKey(intercept: true).Key);
    }

    [Fact]
    public void TryQueueReturnToGameLoopStep_WhenDefaultBackActionHasNoInputValue_QueuesExplicitMenuIndex()
    {
        var store = new AgentConsoleStateStore();
        var input = new AgentConsoleLiveInputSource(store, readTimeout: TimeSpan.FromMilliseconds(100));
        store.UpdateSnapshot(BuildMenuSnapshot("explorer-selection-13", selectedIndex: 1) with
        {
            Title = "Персонажи",
            PlainText = "Персонажи",
            Actions =
            [
                new AgentConsoleAction { Id = "option-0", Label = "Ночной сторож архива" },
                new AgentConsoleAction { Id = "option-1", Label = "← Назад", IsDefault = true }
            ]
        });

        var result = input.TryQueueReturnToGameLoopStep();

        Assert.True(result.Accepted);
        Assert.Equal(AgentConsoleInputRejectionCode.None, result.RejectionCode);
        Assert.Equal(ConsoleKey.D2, input.ReadKey(intercept: true).Key);
        Assert.Equal(ConsoleKey.Enter, input.ReadKey(intercept: true).Key);
    }

    [Fact]
    public void TryQueueReturnToGameLoopStep_WhenEntityLabelContainsClosedWord_ChoosesActualBackAction()
    {
        var store = new AgentConsoleStateStore();
        var input = new AgentConsoleLiveInputSource(store, readTimeout: TimeSpan.FromMilliseconds(100));
        store.UpdateSnapshot(BuildMenuSnapshot("explorer-selection-14", selectedIndex: 1) with
        {
            Title = "Персонажи",
            PlainText = "Персонажи",
            Actions =
            [
                new AgentConsoleAction { Id = "option-0", Label = "Ночной сторож закрытого городского архива" },
                new AgentConsoleAction { Id = "option-1", Label = "← Назад", IsDefault = true }
            ]
        });

        var result = input.TryQueueReturnToGameLoopStep();

        Assert.True(result.Accepted);
        Assert.Equal(AgentConsoleInputRejectionCode.None, result.RejectionCode);
        Assert.Equal(ConsoleKey.D2, input.ReadKey(intercept: true).Key);
        Assert.Equal(ConsoleKey.Enter, input.ReadKey(intercept: true).Key);
    }

    [Fact]
    public void TryQueueReturnToGameLoopStep_WhenAlreadyAtGameLoop_RejectsWithoutQueuedInput()
    {
        var store = new AgentConsoleStateStore();
        var input = new AgentConsoleLiveInputSource(store, readTimeout: TimeSpan.FromMilliseconds(100));
        store.UpdateSnapshot(BuildTextSnapshot("game-loop"));

        var result = input.TryQueueReturnToGameLoopStep();

        Assert.False(result.Accepted);
        Assert.Equal(AgentConsoleInputRejectionCode.InvalidRequest, result.RejectionCode);
        Assert.False(input.KeyAvailable);
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

    [Fact]
    public void ReadLine_WhenGameLoopPromptIsConsumed_PublishesTurnPreparationSnapshot()
    {
        var store = new AgentConsoleStateStore();
        var input = new AgentConsoleLiveInputSource(store, readTimeout: TimeSpan.FromMilliseconds(100));
        store.UpdateSnapshot(BuildTextSnapshot("game-loop"));

        var accepted = input.EnqueueLine("Спросить Азалию о правилах Моря Хаоса");
        Assert.True(accepted.Accepted);

        Assert.Equal("Спросить Азалию о правилах Моря Хаоса", input.ReadLine());

        var consumedSnapshot = store.GetSnapshot();
        Assert.NotNull(consumedSnapshot);
        Assert.Equal("turn-preparing", consumedSnapshot.ScreenId);
        Assert.Equal(AgentConsoleMode.Loading, consumedSnapshot.Mode);
        Assert.Equal("Ход принят", consumedSnapshot.Title);
        Assert.Contains("готовит запрос для GM", consumedSnapshot.PlainText, StringComparison.OrdinalIgnoreCase);
        Assert.False(consumedSnapshot.AwaitingInput);
        Assert.Equal(AgentConsoleInputKind.None, consumedSnapshot.InputKind);
        Assert.Empty(consumedSnapshot.Actions);
        Assert.Null(consumedSnapshot.Prompt);
    }

    [Fact]
    public void ReadLine_WhenGameLoopSlashCommandIsConsumed_PublishesLocalCommandProcessingSnapshot()
    {
        var store = new AgentConsoleStateStore();
        var input = new AgentConsoleLiveInputSource(store, readTimeout: TimeSpan.FromMilliseconds(100));
        store.UpdateSnapshot(BuildTextSnapshot("game-loop"));

        var accepted = input.EnqueueLine("/хроники_посмертия");
        Assert.True(accepted.Accepted);

        Assert.Equal("/хроники_посмертия", input.ReadLine());

        var consumedSnapshot = store.GetSnapshot();
        Assert.NotNull(consumedSnapshot);
        Assert.Equal("command-processing", consumedSnapshot.ScreenId);
        Assert.Equal(AgentConsoleMode.Loading, consumedSnapshot.Mode);
        Assert.Equal("Команда выполняется", consumedSnapshot.Title);
        Assert.Contains("локальная команда", consumedSnapshot.PlainText, StringComparison.OrdinalIgnoreCase);
        Assert.False(consumedSnapshot.AwaitingInput);
        Assert.Equal(AgentConsoleInputKind.None, consumedSnapshot.InputKind);
        Assert.Empty(consumedSnapshot.Actions);
        Assert.Null(consumedSnapshot.Prompt);
    }

    [Fact]
    public void EnqueueLine_WhenInputBlockActive_RejectsStaleGameLoopPrompt()
    {
        var store = new AgentConsoleStateStore();
        var input = new AgentConsoleLiveInputSource(store, readTimeout: TimeSpan.FromMilliseconds(100));
        input.PublishSnapshot(BuildTextSnapshot("gm-validation-repair") with
        {
            Mode = AgentConsoleMode.Loading,
            Title = "Ремонт данных",
            PlainText = "GM исправляет невалидное состояние.",
            AwaitingInput = false,
            InputKind = AgentConsoleInputKind.None,
            Prompt = null
        });

        using var block = input.BeginInputBlockFromCurrentSnapshot("Validation repair is active.");
        input.PublishSnapshot(BuildTextSnapshot("game-loop"));

        var snapshot = store.GetSnapshot();
        Assert.NotNull(snapshot);
        Assert.Equal("gm-validation-repair", snapshot!.ScreenId);
        Assert.False(snapshot.AwaitingInput);
        Assert.Equal(AgentConsoleInputKind.None, snapshot.InputKind);

        var rejected = input.EnqueueLine("Продолжить путь");

        Assert.False(rejected.Accepted);
        Assert.Equal(AgentConsoleInputRejectionCode.NotAwaitingInput, rejected.RejectionCode);
        Assert.Contains("Validation repair is active", rejected.Message, StringComparison.OrdinalIgnoreCase);

        block.Dispose();
        input.PublishSnapshot(BuildTextSnapshot("game-loop"));

        var accepted = input.EnqueueLine("Продолжить путь");
        Assert.True(accepted.Accepted);
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

    private static AgentConsoleSnapshot BuildLongMenuSnapshot(string screenId, int selectedIndex, int count)
    {
        var renderedAt = new DateTimeOffset(2026, 5, 31, 10, 0, 0, TimeSpan.Zero);
        return new AgentConsoleSnapshot
        {
            ScreenId = screenId,
            Mode = AgentConsoleMode.Menu,
            Title = "Guardian presets",
            PlainText = "Choose a guardian.",
            AwaitingInput = true,
            InputKind = AgentConsoleInputKind.MenuSelection,
            SelectedIndex = selectedIndex,
            Actions = Enumerable.Range(0, count)
                .Select(index => new AgentConsoleAction
                {
                    Id = $"option-{index}",
                    Label = $"Option {index + 1}",
                    InputValue = (index + 1).ToString(),
                    IsDefault = selectedIndex == index
                })
                .ToArray(),
            RenderedAtUtc = renderedAt,
            UpdatedAtUtc = renderedAt
        };
    }

    private static AgentConsoleSnapshot BuildKeySnapshot(string screenId)
    {
        var renderedAt = new DateTimeOffset(2026, 5, 31, 10, 0, 0, TimeSpan.Zero);
        return new AgentConsoleSnapshot
        {
            ScreenId = screenId,
            Mode = AgentConsoleMode.TextPrompt,
            Title = "Command Output",
            PlainText = "Press any key.",
            AwaitingInput = true,
            InputKind = AgentConsoleInputKind.Key,
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
