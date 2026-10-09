using System.Reflection;
using System.Text.Json;
using BookOfEternityClient.Core;
using BookOfEternityClient.Services;
using Microsoft.Extensions.Logging.Abstractions;
using Spectre.Console;
using Spectre.Console.Rendering;
using Xunit;

namespace BookOfEternityClient.Tests;

public sealed partial class GameEngineTurnLifecycleTests
{
    [Theory]
    [InlineData("timeout_unknown")]
    [InlineData("runtime_unknown")]
    [InlineData("ready_key_failure")]
    [InlineData("escape")]
    [InlineData("timeout_known")]
    [InlineData("timeout_success")]
    [InlineData("ui_failure")]
    public async Task OriginalTerminalWaitOwnsTasksAndPreservesStorageOutcome(string mode)
    {
        Assert.True(OperatingSystem.IsLinux());
        using var owned = new CleanupOwnedFixture(_rootPath, line => _directGachaOutput?.WriteLine(line));
        using var probe = new WorkerStorageProbe();
        var fs = new FileSystemManager(_rootPath, NullLogger<FileSystemManager>.Instance,
            PhysicalLoadTransactionOperations.Instance, probe.Hooks);
        probe.Attach(fs); probe.Cut.Armed = true;
        var uiFailure = new InvalidOperationException("controlled terminal Status startup failure");
        var keyFailure = new InvalidOperationException("controlled terminal key failure");
        var writeRefusal = new InvalidDataException("known terminal publication refusal");
        var inspectionEntered = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        var releaseInspection = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        using var input = new TerminalWaitInput(mode == "escape",
            mode is "timeout_unknown" or "ready_key_failure" ? keyFailure : null);
        using var console = new TerminalWaitConsole(mode == "ui_failure" ? uiFailure : null, inspectionEntered.Task);
        var inspectionActive = 0;
        var inspections = 0;
        var engine = CreateGameEngine(input, settings =>
        {
            InertRealmSettings(settings);
            settings.GmTimeoutSeconds = mode == "runtime_unknown" ? 30 : 15;
        }, new GameEngineSessionFinalizationHooks
        {
            AtCheckpointAsync = async checkpoint =>
            {
                if (checkpoint != SessionFinalizationCheckpoint.TerminalSignalInspectionLeaseAcquired) return;
                Interlocked.Increment(ref inspections);
                Interlocked.Increment(ref inspectionActive);
                inspectionEntered.TrySetResult();
                try
                {
                    if (mode == "ui_failure") await releaseInspection.Task;
                    else await input.Entered.Task;
                }
                finally { Interlocked.Decrement(ref inspectionActive); }
            }
        }, fs);
        await fs.WriteFileAtomicAsync("input/turn_request.json", JsonSerializer.Serialize(new
        {
            sessionId = "terminal-storage", requestId = "original-terminal-request", turnNumber = 9
        }));
        if (mode == "runtime_unknown")
            await fs.WriteFileAtomicAsync("game_state/control/gm_daemon_status.json", "{\"status\":\"failed\"}");
        if (mode == "ready_key_failure")
            await fs.WriteFileAtomicAsync("ready/turn_complete.json", "{\"status\":\"success\"}");
        var prior = probe.Committed.ToDictionary(pair => pair.Key, pair => pair.Value, StringComparer.Ordinal);
        var uncertain = mode.EndsWith("_unknown", StringComparison.Ordinal);
        if (uncertain) probe.Target = fs.ResolvePath("ready/turn_error.json");
        var knownHits = 0;
        if (mode == "timeout_known")
            probe.BeforeMutation = path =>
            {
                if (path == "ready/turn_error.json") { knownHits++; throw writeRefusal; }
                return Task.CompletedTask;
            };
        var originalConsole = AnsiConsole.Console;
        Task? operation = null;
        Task? originalCall = null;
        Task? originalWaitTask = null;
        Exception? failure = null;
        Exception? waitFailure = null;
        string? outcome = null;
        var originalOperationJoined = false;
        var completedBeforeRelease = false;
        var activeInputAtReturn = -1;
        var activeInspectionAtReturn = -1;
        var waitTaskCompletedAtReturn = false;
        var stopwatch = System.Diagnostics.Stopwatch.StartNew();
        try
        {
            AnsiConsole.Console = console;
            operation = ObserveOriginalOperationAsync();
            await console.Started.Task.WaitAsync(TimeSpan.FromSeconds(5));
            // Retain the actual compiler-owned wait task for RED teardown too:
            // the old UI-error path never awaited it. This observes, not replaces,
            // the original task; no synthetic task is passed to production.
            var stateMachine = originalCall!.GetType().GetField("StateMachine", BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic)!.GetValue(originalCall)!;
            originalWaitTask = Assert.Single(FindOriginalWaitTasks(stateMachine).Distinct());
            console.ReleaseUi.TrySetResult();
            // Positive original Status-delegate completion (or injected startup failure),
            // not a timer pretending the storage branch was reached.
            await console.Exited.Task.WaitAsync(TimeSpan.FromSeconds(35));
            if (mode != "escape")
            {
                // Once the real branch has completed, its owner must remain pending
                // while the already-entered key callback / wait inspection is held.
                await Task.WhenAny(operation, Task.Delay(300));
                completedBeforeRelease = operation.IsCompleted;
            }
        }
        finally
        {
            console.ReleaseUi.TrySetResult(); input.Release(); releaseInspection.TrySetResult();
            if (operation != null) { await operation; originalOperationJoined = true; }
            if (originalWaitTask != null) waitFailure = await Record.ExceptionAsync(() => originalWaitTask);
            if (input.Entered.Task.IsCompleted) await input.CallbackExited.Task;
            AnsiConsole.Console = originalConsole;
        }
        stopwatch.Stop();
        var terminalAfter = CleanupPublicationCut.ReadOptional(fs.ResolvePath("ready/turn_error.json"));
        var afterImages = prior.ToDictionary(pair => pair.Key,
            pair => CleanupPublicationCut.ReadOptional(pair.Key), StringComparer.Ordinal);
        string? harnessSource = null;
        var publishedTerminal = uncertain ? probe.Cut.PublishedBytes : terminalAfter;
        if (publishedTerminal != null)
        {
            using var terminal = JsonDocument.Parse(System.Text.Encoding.UTF8.GetString(publishedTerminal).TrimStart('\uFEFF'));
            harnessSource = terminal.RootElement.GetProperty("harnessSource").GetString();
            Assert.Equal("original-terminal-request", terminal.RootElement.GetProperty("requestId").GetString());
        }
        _directGachaOutput?.WriteLine(JsonSerializer.Serialize(new
        {
            mode, outcome, Failure = failure?.ToString(), originalOperationJoined, completedBeforeRelease,
            activeInputAtReturn, activeInspectionAtReturn, inspections, input.Calls, input.Reads,
            waitTaskCompletedAtReturn, OriginalWaitTaskJoined = originalWaitTask?.IsCompleted == true,
            WaitFailure = waitFailure?.ToString(),
            InputCallbackSettled = input.Entered.Task.IsCompleted && input.CallbackExited.Task.IsCompleted,
            console.Invocations, UiFailureObserved = console.FailureObserved, knownHits, harnessSource,
            KeyFailureRetained = ReferenceEquals(failure?.Data["TerminalWaitKeyFailure"], keyFailure),
            ElapsedSeconds = stopwatch.Elapsed.TotalSeconds, prior, afterImages, terminalAfter,
            terminalCommitted = probe.Committed.GetValueOrDefault(fs.ResolvePath("ready/turn_error.json")),
            Cut = probe.Cut.Evidence()
        }));
        Assert.True(originalOperationJoined);
        Assert.True(inspections > 0);
        Assert.Equal(1, console.Invocations);
        foreach (var pair in prior) Assert.Equal(pair.Value, afterImages[pair.Key]);
        if (mode != "escape") Assert.False(completedBeforeRelease);
        Assert.Equal(0, activeInputAtReturn); Assert.Equal(0, activeInspectionAtReturn);
        Assert.True(waitTaskCompletedAtReturn);
        Assert.Equal(mode == "ui_failure" ? 0 : 1, input.Calls);
        Assert.Equal(mode == "escape" ? 1 : 0, input.Reads);
        if (uncertain)
        {
            probe.Cut.AssertReachedAndStopped(); Assert.Same(probe.Cut.OriginalUncertainty, failure);
            Assert.Null(outcome);
            Assert.Equal(mode == "runtime_unknown" ? "gm_runtime_unavailable" : "gm_terminal_wait_timeout", harnessSource);
            if (mode == "timeout_unknown") Assert.Same(keyFailure, failure!.Data["TerminalWaitKeyFailure"]);
        }
        else
        {
            Assert.Equal(0, probe.Cut.Cuts); Assert.False(File.Exists(probe.Cut.JournalPath));
            if (mode == "ui_failure") { Assert.Same(uiFailure, failure); Assert.Null(outcome); }
            else
            {
                Assert.Null(failure);
                Assert.Equal(mode is "escape" or "timeout_known" ? "Cancelled" : "Completed", outcome);
            }
            Assert.Equal(mode == "timeout_known" ? 1 : 0, knownHits);
            if (mode == "timeout_success")
            {
                Assert.Equal("gm_terminal_wait_timeout", harnessSource);
                Assert.NotNull(terminalAfter);
                Assert.Equal(probe.Committed[fs.ResolvePath("ready/turn_error.json")], terminalAfter);
            }
            else Assert.Null(terminalAfter);
        }

        async Task ObserveOriginalOperationAsync()
        {
            try
            {
                var method = typeof(GameEngine).GetMethod("WaitForTerminalSignalAsync", BindingFlags.Instance | BindingFlags.NonPublic)!;
                originalCall = Assert.IsAssignableFrom<Task>(method.Invoke(engine, null));
                await originalCall;
                outcome = originalCall.GetType().GetProperty("Result")!.GetValue(originalCall)!.ToString();
            }
            catch (Exception error) { failure = error; }
            finally
            {
                activeInputAtReturn = input.Active;
                activeInspectionAtReturn = Volatile.Read(ref inspectionActive);
                waitTaskCompletedAtReturn = originalWaitTask?.IsCompleted == true;
            }
        }
    }

    private static IEnumerable<Task> FindOriginalWaitTasks(object holder)
    {
        foreach (var field in holder.GetType().GetFields(BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic))
        {
            var value = field.GetValue(holder);
            if (field.Name == "waitTask" || field.Name.StartsWith("<waitTask>", StringComparison.Ordinal))
                yield return Assert.IsAssignableFrom<Task>(value);
            else if (value?.GetType().FullName?.StartsWith(typeof(GameEngine).FullName + "+<>c__DisplayClass", StringComparison.Ordinal) == true)
                foreach (var task in FindOriginalWaitTasks(value)) yield return task;
        }
    }

    private sealed class TerminalWaitInput(bool escape, Exception? failure) : IConsoleInputSource, IDisposable
    {
        private readonly ManualResetEventSlim _release = new(escape);
        private int _active, _calls, _reads;
        internal TaskCompletionSource Entered { get; } = new(TaskCreationOptions.RunContinuationsAsynchronously);
        internal TaskCompletionSource CallbackExited { get; } = new(TaskCreationOptions.RunContinuationsAsynchronously);
        internal int Active => Volatile.Read(ref _active);
        internal int Calls => Volatile.Read(ref _calls);
        internal int Reads => Volatile.Read(ref _reads);
        public bool IsScripted => true;
        public bool KeyAvailable
        {
            get
            {
                Interlocked.Increment(ref _calls); Interlocked.Increment(ref _active); Entered.TrySetResult();
                try { _release.Wait(); if (failure != null) throw failure; return escape; }
                finally { Interlocked.Decrement(ref _active); CallbackExited.TrySetResult(); }
            }
        }
        public ConsoleKeyInfo ReadKey(bool intercept = true) { Interlocked.Increment(ref _reads); return Key(ConsoleKey.Escape); }
        public string? ReadLine() => throw new InvalidOperationException("no terminal line input");
        public void AssertCompleted() => Assert.Equal(0, Active);
        internal void Release() => _release.Set();
        public void Dispose() => _release.Dispose();
    }

    private sealed class TerminalWaitConsole(Exception? startupFailure, Task inspectionEntered) : IAnsiConsole, IExclusivityMode, IDisposable
    {
        private readonly StringWriter _writer = new();
        private IAnsiConsole? _inner;
        private IAnsiConsole Inner => _inner ??= Create();
        internal TaskCompletionSource Exited { get; } = new(TaskCreationOptions.RunContinuationsAsynchronously);
        internal TaskCompletionSource Started { get; } = new(TaskCreationOptions.RunContinuationsAsynchronously);
        internal TaskCompletionSource ReleaseUi { get; } = new(TaskCreationOptions.RunContinuationsAsynchronously);
        internal int Invocations { get; private set; }
        internal bool FailureObserved { get; private set; }
        public Profile Profile => Inner.Profile;
        public IAnsiConsoleCursor Cursor => Inner.Cursor;
        public IAnsiConsoleInput Input => Inner.Input;
        public RenderPipeline Pipeline => Inner.Pipeline;
        public IExclusivityMode ExclusivityMode => this;
        public void Clear(bool home) { }
        public void Write(IRenderable renderable) => Inner.Write(renderable);
        public T Run<T>(Func<T> func) => func();
        public async Task<T> RunAsync<T>(Func<Task<T>> func)
        {
            Invocations++;
            Started.TrySetResult();
            await ReleaseUi.Task;
            await inspectionEntered;
            try
            {
                if (startupFailure != null)
                {
                    FailureObserved = true; throw startupFailure;
                }
                return await func();
            }
            finally { Exited.TrySetResult(); }
        }
        private IAnsiConsole Create()
        {
            var console = AnsiConsole.Create(new AnsiConsoleSettings
            {
                Ansi = AnsiSupport.Yes, ColorSystem = ColorSystemSupport.NoColors,
                Interactive = InteractionSupport.Yes, Out = new AnsiConsoleOutput(_writer)
            });
            console.Profile.Width = 120; console.Profile.Height = 40;
            return console;
        }
        public void Dispose() => _writer.Dispose();
    }
}
