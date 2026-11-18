using System.Diagnostics;
using System.Text.Json;
using BookOfEternityClient.Core;
using BookOfEternityClient.Services;
using Microsoft.Extensions.Logging.Abstractions;
using Spectre.Console;
using Xunit;

namespace BookOfEternityClient.Tests;

public sealed partial class GameEngineTurnLifecycleTests
{
    [Theory]
    [InlineData("exhausted")]
    [InlineData("released")]
    [InlineData("post-open-integrity")]
    public async Task TerminalWaitDistinguishesPhysicalContentionFromPostOpenIntegrityFailure(string mode)
    {
        // Diagnostic qualification of the existing local waiter, not remote-owner admission,
        // a prescription to suppress contention, or identification of the mm5 exception.
        Assert.True(OperatingSystem.IsLinux());
        var armed = false;
        var contentions = 0;
        var opened = 0;
        var inspections = 0;
        FileStream? held = null;
        var integrityFailure = new InvalidDataException("terminal inspection post-open integrity sentinel");
        var files = new FileSystemManager(_rootPath, NullLogger<FileSystemManager>.Instance,
            PhysicalLoadTransactionOperations.Instance, new FileSystemManagerHooks
            {
                CanonicalWriteLockContendedAsync = () =>
                {
                    if (armed)
                    {
                        Interlocked.Increment(ref contentions);
                        if (mode == "released") Interlocked.Exchange(ref held, null)?.Dispose();
                    }
                    return Task.CompletedTask;
                },
                AfterCanonicalWriteLockOpenedAsync = () =>
                {
                    if (armed)
                    {
                        Interlocked.Increment(ref opened);
                        if (mode == "post-open-integrity") return Task.FromException(integrityFailure);
                    }
                    return Task.CompletedTask;
                }
            });
        var input = new QueuedConsoleInputSource([]);
        var engine = CreateGameEngine(input, settings =>
        {
            InertRealmSettings(settings);
            settings.GmTimeoutSeconds = 900;
        }, new GameEngineSessionFinalizationHooks
        {
            AtCheckpointAsync = checkpoint =>
            {
                if (checkpoint == SessionFinalizationCheckpoint.TerminalWaitStarted)
                {
                    // Timeout/status inspection has finished. The next real polling lease,
                    // not a preparation write or diagnostic read, meets this independent handle.
                    if (mode != "post-open-integrity")
                        held = new FileStream(files.CanonicalWriteLockPath, FileMode.Open,
                            FileAccess.ReadWrite, FileShare.None);
                    armed = true;
                }
                if (checkpoint == SessionFinalizationCheckpoint.TerminalSignalInspectionLeaseAcquired)
                    Interlocked.Increment(ref inspections);
                return Task.CompletedTask;
            }
        }, files);
        var paths = new[] { "input/turn_request.json", "ready/turn_complete.json", "game_state/world/weather.json" };
        await files.WriteFileAtomicAsync(paths[0], "{\"sessionId\":\"terminal-contention\",\"requestId\":\"original\",\"turnNumber\":42}");
        await files.WriteFileAtomicAsync(paths[1], "{\"status\":\"success\",\"requestId\":\"original\"}");
        await files.WriteFileAtomicAsync(paths[2], "{\"description\":\"unchanged\"}");
        var before = paths.ToDictionary(path => path, path => File.ReadAllBytes(files.ResolvePath(path)));
        var originalConsole = AnsiConsole.Console;
        using var output = new StringWriter();
        Task<GameEngine.TerminalSignalWaitOutcome>? operation = null;
        GameEngine.TerminalSignalWaitOutcome? outcome = null;
        Exception? failure = null;
        Exception? cleanupFailure = null;
        var elapsed = Stopwatch.StartNew();
        try
        {
            AnsiConsole.Console = AnsiConsole.Create(new AnsiConsoleSettings
            {
                Ansi = AnsiSupport.No, Interactive = InteractionSupport.No,
                Out = new AnsiConsoleOutput(output)
            });
            operation = engine.WaitForTerminalSignalAsync();
            try { outcome = await operation.WaitAsync(TimeSpan.FromSeconds(25)); }
            catch (Exception error) { failure = error; }
        }
        finally
        {
            armed = false;
            try { Interlocked.Exchange(ref held, null)?.Dispose(); }
            catch (Exception error) { cleanupFailure = error; }
            // A failed assertion/observation does not abandon the original waiter or its tasks.
            // The original ready signal remains available after release; Escape is only teardown.
            input.Enqueue(Key(ConsoleKey.Escape));
            if (operation != null)
            {
                try { await operation.WaitAsync(TimeSpan.FromSeconds(5)); }
                catch (Exception error)
                {
                    if (!ReferenceEquals(error, failure))
                        cleanupFailure = cleanupFailure == null ? error : new AggregateException(cleanupFailure, error);
                }
            }
            AnsiConsole.Console = originalConsole;
        }
        var observedContentions = contentions;
        var observedOpened = opened;
        var observedInspections = inspections;
        await using (var fresh = await files.AcquireCanonicalWriteLeaseAsync()) Assert.True(fresh.IsActive);
        _directGachaOutput?.WriteLine(JsonSerializer.Serialize(new
        {
            mode, outcome, Failure = failure?.ToString(), CleanupFailure = cleanupFailure?.ToString(),
            observedContentions, observedOpened, observedInspections,
            OriginalTaskCompleted = operation?.IsCompleted == true,
            OriginalTaskStatus = operation?.Status.ToString(), FreshLeaseAcquired = true,
            ElapsedSeconds = elapsed.Elapsed.TotalSeconds
        }));
        Assert.Null(cleanupFailure);
        Assert.True(operation?.IsCompleted == true);
        foreach (var pair in before) Assert.Equal(pair.Value, File.ReadAllBytes(files.ResolvePath(pair.Key)));
        Assert.False(File.Exists(files.ResolvePath("ready/turn_error.json")));
        if (mode == "released")
        {
            Assert.Null(failure);
            Assert.Equal(GameEngine.TerminalSignalWaitOutcome.Completed, outcome);
            Assert.Equal(1, observedContentions);
            Assert.Equal(1, observedOpened);
            Assert.Equal(1, observedInspections);
        }
        else
        {
            Assert.Null(outcome);
            Assert.NotNull(failure);
            Assert.Equal(0, observedInspections);
            if (mode == "exhausted")
            {
                Assert.True(observedContentions > 0);
                Assert.Equal(0, observedOpened);
                Assert.True(files.IsOriginalCanonicalLockContention(failure!));
                Assert.Equal(11, failure!.InnerException!.HResult);
            }
            else
            {
                Assert.Same(integrityFailure, failure);
                Assert.False(files.IsOriginalCanonicalLockContention(failure!));
                Assert.Equal(0, observedContentions);
                Assert.Equal(1, observedOpened);
            }
        }
    }
}
