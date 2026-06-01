using System.Text.Json;
using BookOfEternityClient.AgentConsole;
using BookOfEternityClient.Configuration;
using Xunit;

namespace BookOfEternityClient.Tests;

public sealed class AgentConsoleObservationTests
{
    [Fact]
    public void SnapshotAndEventDtosSerializeWithStableCamelCaseJsonShape()
    {
        var renderedAt = new DateTimeOffset(2026, 5, 31, 10, 0, 0, TimeSpan.Zero);
        var snapshot = new AgentConsoleSnapshot
        {
            ScreenId = "main-menu",
            Mode = AgentConsoleMode.Menu,
            Title = "Main Menu",
            PlainText = "Choose your path.",
            AnsiText = "\u001b[1mChoose your path.\u001b[0m",
            AwaitingInput = true,
            InputKind = AgentConsoleInputKind.MenuSelection,
            SelectedIndex = 1,
            Actions =
            [
                new AgentConsoleAction
                {
                    Id = "new-game",
                    Label = "New Game",
                    Shortcut = "1"
                },
                new AgentConsoleAction
                {
                    Id = "load-game",
                    Label = "Load Game",
                    IsDefault = true
                }
            ],
            Prompt = new AgentConsolePrompt
            {
                PromptId = "main-menu-choice",
                Text = "Choose an option",
                InputKind = AgentConsoleInputKind.MenuSelection,
                Choices = ["New Game", "Load Game"]
            },
            RenderedAtUtc = renderedAt,
            UpdatedAtUtc = renderedAt.AddSeconds(5),
            Diagnostics =
            [
                new AgentConsoleDiagnostic
                {
                    Severity = AgentConsoleDiagnosticSeverity.Warning,
                    Code = "layout-truncated",
                    Message = "The rendered menu was truncated.",
                    Detail = "Console height was 12 rows."
                }
            ]
        };

        var screenJson = JsonSerializer.Serialize(snapshot, AgentConsoleJson.Options);

        using var screenDoc = JsonDocument.Parse(screenJson);
        var root = screenDoc.RootElement;
        Assert.Equal(1, root.GetProperty("schemaVersion").GetInt32());
        Assert.Equal("main-menu", root.GetProperty("screenId").GetString());
        Assert.Equal("menu", root.GetProperty("mode").GetString());
        Assert.Equal("Main Menu", root.GetProperty("title").GetString());
        Assert.Equal("Choose your path.", root.GetProperty("plainText").GetString());
        Assert.Equal("\u001b[1mChoose your path.\u001b[0m", root.GetProperty("ansiText").GetString());
        Assert.True(root.GetProperty("awaitingInput").GetBoolean());
        Assert.Equal("menuSelection", root.GetProperty("inputKind").GetString());
        Assert.Equal(1, root.GetProperty("selectedIndex").GetInt32());
        Assert.Equal("load-game", root.GetProperty("actions")[1].GetProperty("id").GetString());
        Assert.True(root.GetProperty("actions")[1].GetProperty("isDefault").GetBoolean());
        Assert.Equal("main-menu-choice", root.GetProperty("prompt").GetProperty("promptId").GetString());
        Assert.Equal("menuSelection", root.GetProperty("prompt").GetProperty("inputKind").GetString());
        Assert.Equal("warning", root.GetProperty("diagnostics")[0].GetProperty("severity").GetString());
        Assert.DoesNotContain("InputKind", screenJson, StringComparison.Ordinal);

        var agentEvent = new AgentConsoleEvent
        {
            SequenceId = 42,
            Kind = AgentConsoleEventKind.InputRejected,
            OccurredAtUtc = renderedAt.AddSeconds(10),
            ScreenId = "main-menu",
            InputKind = AgentConsoleInputKind.Text,
            Message = "Input was not valid for this prompt.",
            Diagnostic = new AgentConsoleDiagnostic
            {
                Severity = AgentConsoleDiagnosticSeverity.Error,
                Code = "invalid-input",
                Message = "Only menu shortcuts are accepted."
            }
        };

        var eventJson = JsonSerializer.Serialize(agentEvent, AgentConsoleJson.Options);

        using var eventDoc = JsonDocument.Parse(eventJson);
        var eventRoot = eventDoc.RootElement;
        Assert.Equal(42, eventRoot.GetProperty("sequenceId").GetInt64());
        Assert.Equal("inputRejected", eventRoot.GetProperty("kind").GetString());
        Assert.Equal("main-menu", eventRoot.GetProperty("screenId").GetString());
        Assert.Equal("text", eventRoot.GetProperty("inputKind").GetString());
        Assert.Equal("error", eventRoot.GetProperty("diagnostic").GetProperty("severity").GetString());
        Assert.DoesNotContain("SequenceId", eventJson, StringComparison.Ordinal);
    }

    [Fact]
    public void StateStoreStartsWithNoScreenAndEmptyHistory()
    {
        var store = new AgentConsoleStateStore(eventCapacity: 3);

        var state = store.ReadState();

        Assert.Null(state.CurrentSnapshot);
        Assert.Empty(state.Events);
    }

    [Fact]
    public void StateStoreAssignsMonotonicSequenceIdsAndPreservesEventOrdering()
    {
        var clock = new IncrementingClock(new DateTimeOffset(2026, 5, 31, 10, 0, 0, TimeSpan.Zero));
        var store = new AgentConsoleStateStore(eventCapacity: 10, utcNow: clock.GetUtcNow);
        var snapshot = BuildMenuSnapshot("main-menu", selectedIndex: 0);

        var stateChanged = store.AppendEvent(AgentConsoleEventKind.StateChanged, message: "Console attached.");
        var screenRendered = store.UpdateSnapshot(snapshot);
        var promptStarted = store.AppendEvent(
            AgentConsoleEventKind.PromptStarted,
            screenId: snapshot.ScreenId,
            inputKind: AgentConsoleInputKind.MenuSelection,
            message: "Menu selection is active.");

        var state = store.ReadState();
        var events = state.Events.ToArray();

        Assert.Same(snapshot, state.CurrentSnapshot);
        Assert.Equal([1L, 2L, 3L], events.Select(agentEvent => agentEvent.SequenceId).ToArray());
        Assert.Equal(AgentConsoleEventKind.StateChanged, stateChanged.Kind);
        Assert.Equal(AgentConsoleEventKind.ScreenRendered, screenRendered.Kind);
        Assert.Equal(AgentConsoleEventKind.PromptStarted, promptStarted.Kind);
        Assert.Equal([stateChanged.Kind, screenRendered.Kind, promptStarted.Kind], events.Select(agentEvent => agentEvent.Kind).ToArray());
        Assert.True(events[0].OccurredAtUtc < events[1].OccurredAtUtc);
        Assert.True(events[1].OccurredAtUtc < events[2].OccurredAtUtc);
    }

    [Fact]
    public void StateStoreBoundsEventHistoryWithoutResettingSequenceIds()
    {
        var clock = new IncrementingClock(new DateTimeOffset(2026, 5, 31, 10, 0, 0, TimeSpan.Zero));
        var store = new AgentConsoleStateStore(eventCapacity: 3, utcNow: clock.GetUtcNow);

        for (var index = 0; index < 5; index++)
            store.AppendEvent(AgentConsoleEventKind.StateChanged, message: $"event-{index}");

        var events = store.ReadState().Events.ToArray();

        Assert.Equal(3, events.Length);
        Assert.Equal([3L, 4L, 5L], events.Select(agentEvent => agentEvent.SequenceId).ToArray());
        Assert.Equal(["event-2", "event-3", "event-4"], events.Select(agentEvent => agentEvent.Message ?? string.Empty).ToArray());
    }

    [Fact]
    public void StateStoreRejectsUnboundedEventCapacity()
    {
        var ex = Assert.Throws<ArgumentOutOfRangeException>(() => new AgentConsoleStateStore(eventCapacity: 0));

        Assert.Equal("eventCapacity", ex.ParamName);
    }

    [Fact]
    public void SnapshotBoundsDiagnosticState()
    {
        var diagnostics = Enumerable
            .Range(0, AgentConsoleLimits.MaxDiagnostics + 2)
            .Select(index => new AgentConsoleDiagnostic
            {
                Severity = AgentConsoleDiagnosticSeverity.Warning,
                Code = $"diag-{index}",
                Message = $"Diagnostic {index}"
            })
            .ToArray();

        var snapshot = BuildMenuSnapshot("main-menu", selectedIndex: 0) with
        {
            Diagnostics = diagnostics
        };

        Assert.Equal(AgentConsoleLimits.MaxDiagnostics, snapshot.Diagnostics.Count);
        Assert.Equal("diag-0", snapshot.Diagnostics[0].Code);
        Assert.Equal($"diag-{AgentConsoleLimits.MaxDiagnostics - 1}", snapshot.Diagnostics[^1].Code);
    }

    [Fact]
    public void E2EObservationSnapshotMapsToAgentConsoleSnapshotWithoutFileDependencies()
    {
        var capturedAt = new DateTimeOffset(2026, 5, 31, 10, 0, 0, TimeSpan.Zero);
        var e2eSnapshot = new ConsoleE2EObservationSnapshot(
            RunId: "run-main-menu",
            StepIndex: 7,
            CapturedAtUtc: capturedAt,
            InputMode: ConsoleE2EInputMode.Menu,
            ScreenTitle: "Main Menu",
            PlayerFacingText: "Choose your path.",
            Options: ["Continue", "Options", "Exit"],
            SelectedOption: "Options",
            ArtifactRoot: "artifacts/console-e2e/run-main-menu",
            LogPath: "artifacts/console-e2e/run-main-menu/stdout.txt");

        var snapshot = AgentConsoleE2EObservationMapper.ToAgentConsoleSnapshot(e2eSnapshot);

        Assert.Equal("e2e:run-main-menu:7", snapshot.ScreenId);
        Assert.Equal(AgentConsoleMode.Menu, snapshot.Mode);
        Assert.Equal("Main Menu", snapshot.Title);
        Assert.Equal("Choose your path.", snapshot.PlainText);
        Assert.True(snapshot.AwaitingInput);
        Assert.Equal(AgentConsoleInputKind.MenuSelection, snapshot.InputKind);
        Assert.Equal(1, snapshot.SelectedIndex);
        Assert.Equal(capturedAt, snapshot.RenderedAtUtc);
        Assert.Equal(capturedAt, snapshot.UpdatedAtUtc);
        Assert.Equal(["Continue", "Options", "Exit"], snapshot.Actions.Select(action => action.Label).ToArray());
        Assert.Equal("option-1", snapshot.Actions[1].Id);
        Assert.True(snapshot.Actions[1].IsDefault);
        Assert.Empty(snapshot.Diagnostics);
    }

    [Fact]
    public void E2EExceptionObservationMapsToBoundedAgentConsoleDiagnostic()
    {
        var e2eSnapshot = new ConsoleE2EObservationSnapshot(
            RunId: "run-error",
            StepIndex: 2,
            CapturedAtUtc: DateTimeOffset.UnixEpoch,
            InputMode: ConsoleE2EInputMode.Error,
            ScreenTitle: "Timeout",
            PlayerFacingText: "The console E2E run timed out before the next prompt.",
            Options: [],
            SelectedOption: null,
            ArtifactRoot: "artifacts/console-e2e/run-error",
            LogPath: "E:/Games/The Book of Eternity Reborn/artifacts/console-e2e/run-error/stdout.txt",
            ErrorType: "InvalidOperationException",
            ErrorMessage: "Scripted input timed out waiting for prompt.");

        var snapshot = AgentConsoleE2EObservationMapper.ToAgentConsoleSnapshot(e2eSnapshot);

        Assert.Equal(AgentConsoleMode.Error, snapshot.Mode);
        Assert.False(snapshot.AwaitingInput);
        Assert.Equal(AgentConsoleInputKind.None, snapshot.InputKind);
        var diagnostic = Assert.Single(snapshot.Diagnostics);
        Assert.Equal(AgentConsoleDiagnosticSeverity.Error, diagnostic.Severity);
        Assert.Equal("console-e2e", diagnostic.Code);
        Assert.Equal("Scripted input timed out waiting for prompt.", diagnostic.Message);
        Assert.Equal("InvalidOperationException", diagnostic.ExceptionType);
        Assert.Null(diagnostic.Detail);
    }

    [Fact]
    public void E2ECompatibilityMappingIsDocumentedForFutureApiConsumers()
    {
        var doc = ReadRepoFile("docs", "agent-console", "snapshot-event-model.md");

        foreach (var requiredText in new[]
        {
            "Issue: #750",
            "ConsoleE2EObservationSnapshot",
            "screenTitle -> title",
            "playerFacingText -> plainText",
            "options -> actions",
            "selectedOption -> selectedIndex",
            "inputMode -> mode",
            "inputMode -> inputKind",
            "file-independent"
        })
        {
            Assert.Contains(requiredText, doc, StringComparison.Ordinal);
        }
    }

    [Fact]
    public void LiveInputQueueContractIsDocumentedForFutureApiConsumers()
    {
        var doc = ReadRepoFile("docs", "agent-console", "snapshot-event-model.md");

        foreach (var requiredText in new[]
        {
            "Issue: #751",
            "AgentConsoleLiveInputSource",
            "AgentConsoleActionRequest",
            "key input",
            "text line input",
            "InputAccepted",
            "InputRejected",
            "Issue: #752",
            "GET /api/agent-console/snapshot",
            "GET /api/agent-console/events",
            "POST /api/agent-console/key",
            "POST /api/agent-console/text",
            "POST /api/agent-console/action",
            "Bearer token"
        })
        {
            Assert.Contains(requiredText, doc, StringComparison.Ordinal);
        }
    }

    [Fact]
    public void ProgramWiresAgentConsoleInputWithoutIdleTimeout()
    {
        var program = ReadRepoFile("BookOfEternityClient", "Program.cs");

        Assert.Contains(
            "new AgentConsoleLiveInputSource(agentConsoleStateStore, readTimeout: Timeout.InfiniteTimeSpan)",
            program,
            StringComparison.Ordinal);
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

    private static string ReadRepoFile(params string[] relativePathParts)
    {
        var dir = new DirectoryInfo(AppContext.BaseDirectory);
        while (dir != null)
        {
            var candidate = Path.Combine(new[] { dir.FullName }.Concat(relativePathParts).ToArray());
            if (File.Exists(candidate))
                return File.ReadAllText(candidate);

            dir = dir.Parent;
        }

        throw new FileNotFoundException(
            "Could not locate repository file: " + Path.Combine(relativePathParts));
    }

    private sealed class IncrementingClock
    {
        private DateTimeOffset _next;

        public IncrementingClock(DateTimeOffset start) => _next = start;

        public DateTimeOffset GetUtcNow()
        {
            var value = _next;
            _next = _next.AddSeconds(1);
            return value;
        }
    }
}
