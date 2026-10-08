using System.Collections;
using System.Reflection;
using System.Text.Json;
using BookOfEternityClient.Core;
using BookOfEternityClient.Configuration;
using BookOfEternityClient.UI;
using Spectre.Console;
using Spectre.Console.Rendering;
using BookOfEternityClient.Services;
using BookOfEternityClient.Models;
using Microsoft.Extensions.Logging.Abstractions;
using Xunit;

namespace BookOfEternityClient.Tests;

public sealed partial class GameEngineTurnLifecycleTests
{
    /// <summary>Preserves confirmed commit and blocks both an existing loop and menu continuation after required refresh fails.</summary>
    /// <param name="serviceRefresh">Cuts the service refresh when true and console schedule refresh otherwise.</param>
    /// <returns>A task completing after the actual load decision and stopped continuation are verified.</returns>
    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task PortableLoadConsole_RequiredRefreshFailureRetainsCommitAndStopsContinuation(bool serviceRefresh)
    {
        var path = await CreateConsoleLoadArchiveAsync();
        var engine = CreateGameEngine(new QueuedConsoleInputSource([]), loadHooks: serviceRefresh
            ? new SaveLoadServiceHooks { AfterLoadPublicationValidatedAsync = () => throw new InvalidOperationException("synthetic required refresh") }
            : null);
        if (!serviceRefresh) ArmCanonicalWriteFailure(ProgressionScheduleService.SchedulePath);
        SetPrivateField(engine, "_inGame", true);
        var loop = GetPrivateField<GameLoop>(engine, "_gameLoop");
        loop.SetSession("old-session", 99);

        var observed = await InvokeConsoleLoadResultAsync(engine, path);

        Assert.False(GetPrivateFieldValue<bool>(engine, "_inGame"));
        Assert.Equal(string.Empty, GetPrivateField<GameLoop>(engine, "_gameLoop").SessionId);
        var result = Assert.IsType<LoadReplacementResult>(observed);
        Assert.Equal(LoadReplacementDisposition.Committed, result.Disposition);
        Assert.Equal(path, result.SelectedSourcePath);
        Assert.NotNull(result.EstablishedGeneration);
        Assert.True(result.NeedsFollowUp);
        Assert.True(result.ContinuationBlocked);
        Assert.False(GetPrivateFieldValue<bool>(engine, "_inGame"));
        Assert.Equal(string.Empty, loop.SessionId);
        Assert.False(await InvokePrivateAsync<bool>(engine, "HasCurrentSessionAsync"));
        var menu = Assert.IsAssignableFrom<IEnumerable>(await InvokePrivateTaskResultAsync(engine, "BuildMainMenuOptionsAsync"));
        Assert.Equal(new[] { "about", "exit" }, menu.Cast<object>().Select(item => item.GetType().GetProperty("Key")!.GetValue(item)));
        await InvokePrivateTaskAsync(engine, "EnterGameLoop");
        Assert.False(GetPrivateFieldValue<bool>(engine, "_inGame"));
        var generation = File.ReadAllBytes(_fs.SessionGenerationPath);
        var repeated = await InvokePrivateAsync<LoadReplacementResult>(engine, "LoadSelectedSaveAndRebindRuntimeAsync", path);
        Assert.Equal(result, repeated);
        Assert.Equal(generation, File.ReadAllBytes(_fs.SessionGenerationPath));
    }

    /// <summary>Does not adopt a newer generation while refreshing the established replacement.</summary>
    /// <returns>A task completing after stale replacement continuation is refused.</returns>
    [Fact]
    public async Task PortableLoadConsole_RebindUsesEstablishedGeneration()
    {
        var path = await CreateConsoleLoadArchiveAsync();
        var newer = Guid.NewGuid().ToString("N");
        var engine = CreateGameEngine(new QueuedConsoleInputSource([]), loadHooks: new SaveLoadServiceHooks
        {
            AfterLoadPublicationValidatedAsync = () =>
            {
                File.WriteAllBytes(_fs.SessionGenerationPath, JsonSerializer.SerializeToUtf8Bytes(new { schemaVersion = 1, generationId = newer }));
                return Task.CompletedTask;
            }
        });
        var observed = await InvokeConsoleLoadResultAsync(engine, path);
        Assert.False(GetPrivateFieldValue<bool>(engine, "_inGame"));
        Assert.Equal(string.Empty, GetPrivateField<GameLoop>(engine, "_gameLoop").SessionId);
        var result = Assert.IsType<LoadReplacementResult>(observed);
        Assert.Equal(LoadReplacementDisposition.Committed, result.Disposition);
        Assert.NotEqual(newer, result.EstablishedGeneration);
        Assert.True(result.ContinuationBlocked);
        Assert.Equal(string.Empty, GetPrivateField<GameLoop>(engine, "_gameLoop").SessionId);
    }

    /// <summary>Repeats pending and UI ownership admission after preparation, on the held replacement lease.</summary>
    /// <param name="activeOwner">Introduces a new UI owner when true, and a pending turn otherwise.</param>
    /// <returns>A task completing after late admission refusal preserves the old session and source.</returns>
    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task PortableLoadConsole_LatePendingOrOwnerRefusesBeforeMutation(bool activeOwner)
    {
        var path = await CreateConsoleLoadArchiveAsync();
        var beforeGeneration = File.ReadAllBytes(_fs.SessionGenerationPath);
        var source = File.ReadAllBytes(path);
        var reached = 0;
        var engine = CreateGameEngine(new QueuedConsoleInputSource([]), loadHooks: new SaveLoadServiceHooks
        {
            BeforeLoadLeaseAcquisitionAsync = async () =>
            {
                reached++;
                if (activeOwner)
                {
                    var owner = new LocalUiSessionLockOwner("late-owner", "browser", "late owner", TimeSpan.FromMinutes(2));
                    Assert.True((await new LocalUiSessionLockService(_fs).AcquireOrRefreshAsync(owner, "test late owner")).Acquired);
                }
                else await _fs.WriteFileAtomicAsync("input/turn_request.json", "{}");
            }
        });
        var loop = GetPrivateField<GameLoop>(engine, "_gameLoop");
        loop.SetSession("old-session", 99);
        var observed = await InvokeConsoleLoadResultAsync(engine, path);
        Assert.Equal(1, reached);
        Assert.Equal(beforeGeneration, File.ReadAllBytes(_fs.SessionGenerationPath));
        var result = Assert.IsType<LoadReplacementResult>(observed);
        Assert.Equal(LoadReplacementDisposition.NotLoaded, result.Disposition);
        Assert.False(result.ContinuationBlocked);
        Assert.Equal(beforeGeneration, File.ReadAllBytes(_fs.SessionGenerationPath));
        Assert.Equal(source, File.ReadAllBytes(path));
        Assert.Equal("old-session", loop.SessionId);
        Assert.True(File.Exists(_fs.ResolvePath(activeOwner ? LocalUiSessionLockService.LockPath : "input/turn_request.json")));
    }

    /// <summary>Keeps known non-loading distinct from a committed replacement without clearing the live loop.</summary>
    /// <returns>A task completing after the absent source is refused.</returns>
    [Fact]
    public async Task PortableLoadConsole_InvalidSourceRetainsOldRuntime()
    {
        var engine = CreateGameEngine(new QueuedConsoleInputSource([]));
        var loop = GetPrivateField<GameLoop>(engine, "_gameLoop");
        loop.SetSession("old-session", 99);
        SetPrivateField(engine, "_inGame", true);
        var result = Assert.IsType<LoadReplacementResult>(await InvokeConsoleLoadResultAsync(engine, "saves/manual_saves/absent.zip"));
        Assert.Equal(LoadReplacementDisposition.NotLoaded, result.Disposition);
        Assert.False(result.ContinuationBlocked);
        Assert.True(GetPrivateFieldValue<bool>(engine, "_inGame"));
        Assert.Equal("old-session", loop.SessionId);
    }

    /// <summary>Distinguishes exact rollback from retained uncertainty in the actual console caller.</summary>
    /// <param name="conflict">Changes a declared member to unknown bytes after publication when true.</param>
    /// <returns>A task completing after decision, generation and continuation assertions.</returns>
    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task PortableLoadConsole_RollbackAndUncertaintyKeepDistinctContinuation(bool conflict)
    {
        var armed = false;
        var cuts = 0;
        FileSystemManager? files = null;
        const string marker = "lore/console-load.bin";
        files = new FileSystemManager(_rootPath, NullLogger<FileSystemManager>.Instance,
            PhysicalLoadTransactionOperations.Instance, new FileSystemManagerHooks
            {
                LocalPublicationObserver = (phase, _) =>
                {
                    if (!armed || phase != TrustedLocalPublicationPhase.CommitStaged) return;
                    cuts++;
                    if (conflict) File.WriteAllBytes(files!.ResolvePath(marker), [90, 91, 92]);
                    throw new InvalidOperationException("console precommit cut");
                }
            });
        var state = PortableSaveFixture.Seed(files);
        await files.WriteFileAtomicBytesAsync(marker, [1, 2, 3]);
        var save = new SaveLoadService(files, state, NullLogger<SaveLoadService>.Instance);
        Assert.True(await save.SaveGameAsync("console-cut", "typed console cut"));
        var path = Assert.Single(Directory.GetFiles(files.ResolvePath("saves/manual_saves"), "*.zip"));
        await files.WriteFileAtomicBytesAsync(marker, [4, 5, 6]);
        var generation = File.ReadAllBytes(files.SessionGenerationPath);
        var engine = CreateGameEngine(new QueuedConsoleInputSource([]), fileSystem: files);
        var loop = GetPrivateField<GameLoop>(engine, "_gameLoop");
        loop.SetSession("old-session", 99);
        SetPrivateField(engine, "_inGame", true);
        armed = true;

        var result = Assert.IsType<LoadReplacementResult>(await InvokeConsoleLoadResultAsync(engine, path));

        Assert.Equal(1, cuts);
        Assert.Equal(conflict ? LoadReplacementDisposition.Uncertain : LoadReplacementDisposition.RolledBack, result.Disposition);
        Assert.Equal(conflict, result.ContinuationBlocked);
        Assert.Equal(path, result.SelectedSourcePath);
        Assert.Equal(!conflict, GetPrivateFieldValue<bool>(engine, "_inGame"));
        Assert.Equal(conflict ? string.Empty : "old-session", loop.SessionId);
        if (conflict)
        {
            Assert.Null(result.EstablishedGeneration);
            Assert.True(File.Exists(Path.Combine(files.RuntimeRootPath, "trusted-local-publication-v1/active.json")));
            Assert.Equal(new byte[] { 90, 91, 92 }, File.ReadAllBytes(files.ResolvePath(marker)));
        }
        else
        {
            Assert.NotNull(result.EstablishedGeneration);
            Assert.Equal(generation, File.ReadAllBytes(files.SessionGenerationPath));
            Assert.Equal(new byte[] { 4, 5, 6 }, File.ReadAllBytes(files.ResolvePath(marker)));
        }
    }

    /// <summary>Retains commitment when the real required settings/UI refresh cannot complete.</summary>
    /// <returns>A task completing after the continuation stage returns a committed blocked decision.</returns>
    [Fact]
    public async Task PortableLoadConsole_RequiredUiRefreshFailureCannotEraseCommit()
    {
        var path = await CreateConsoleLoadArchiveAsync(bootstrapSettings: true);
        var engine = CreateGameEngine(new QueuedConsoleInputSource([]));
        var loaded = Assert.IsType<LoadReplacementResult>(await InvokeConsoleLoadResultAsync(engine, path));
        Assert.Equal(LoadReplacementDisposition.Committed, loaded.Disposition);
        Assert.False(loaded.ContinuationBlocked);
        ArmCanonicalWriteFailure("game_state/core/game_settings.json");
        var result = await InvokePrivateAsync<LoadReplacementResult>(engine, "PrepareLoadedConsoleContinuationAsync", loaded);
        Assert.True(_armedCanonicalWriteFailurePath == null, result.Failure?.ToString());
        Assert.Equal(loaded.Disposition, result.Disposition);
        Assert.Equal(loaded.SelectedSourcePath, result.SelectedSourcePath);
        Assert.Equal(loaded.EstablishedGeneration, result.EstablishedGeneration);
        Assert.True(result.NeedsFollowUp);
        Assert.True(result.ContinuationBlocked);
        Assert.False(GetPrivateFieldValue<bool>(engine, "_inGame"));
        Assert.Equal(string.Empty, GetPrivateField<GameLoop>(engine, "_gameLoop").SessionId);
    }

    /// <summary>Client admission cannot create previously absent generation authority.</summary>
    /// <param name="malformedOwner">Refuses a fresh malformed UI lock when true; otherwise cuts publication to exact rollback.</param>
    /// <returns>A task completing after prior generation absence is preserved.</returns>
    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task PortableLoadConsole_AbsentGenerationAdmissionPreservesAbsence(bool malformedOwner)
    {
        var path = await CreateConsoleLoadArchiveAsync();
        File.Delete(_fs.SessionGenerationPath);
        var cuts = 0;
        var files = new FileSystemManager(_rootPath, NullLogger<FileSystemManager>.Instance,
            PhysicalLoadTransactionOperations.Instance, new FileSystemManagerHooks
            {
                LocalPublicationObserver = (phase, _) =>
                {
                    if (phase != TrustedLocalPublicationPhase.CommitStaged) return;
                    cuts++;
                    throw new InvalidOperationException("absent generation rollback cut");
                }
            });
        if (malformedOwner) File.WriteAllText(files.ResolvePath(LocalUiSessionLockService.LockPath), "{");
        var engine = CreateGameEngine(new QueuedConsoleInputSource([]), fileSystem: files);
        var result = Assert.IsType<LoadReplacementResult>(await InvokeConsoleLoadResultAsync(engine, path));
        Assert.Equal(malformedOwner ? 0 : 1, cuts);
        Assert.Equal(malformedOwner ? LoadReplacementDisposition.NotLoaded : LoadReplacementDisposition.RolledBack, result.Disposition);
        Assert.False(File.Exists(files.SessionGenerationPath));
        Assert.Null(result.EstablishedGeneration);
    }

    /// <summary>Runs the actual in-game load menu and full required refresh, retaining or stopping its existing loop.</summary>
    /// <param name="failRefresh">Cuts the confirmed service refresh when true; otherwise executes healthy required continuation.</param>
    /// <returns>A task completing after actual menu output, generation and loop disposition are verified.</returns>
    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task PortableLoadConsole_InGameMenuReportsCommitAndResumesOnlyAfterRequiredRefresh(bool failRefresh)
    {
        CopyDirectory(TestRepoPaths.BaseSessionRoot, _fs.GameSessionPath);
        var input = new LoadMenuConsoleInput([Key(ConsoleKey.DownArrow), Key(ConsoleKey.Enter)]);
        var engine = CreateGameEngine(input, loadHooks: failRefresh
            ? new SaveLoadServiceHooks { AfterLoadPublicationValidatedAsync = () => throw new InvalidOperationException("/private/console-refresh-cut") }
            : null);
        input.UnexpectedRead = () => SetPrivateField(engine, "_inGame", false);
        var state = GetPrivateField<StateManager>(engine, "_stateManager");
        state.Settings.MusicEnabled = false;
        state.Settings.SoundEnabled = false;
        await state.BootstrapLocalStorageAsync();
        await state.RefreshGameStateAsync();
        var save = GetPrivateField<SaveLoadService>(engine, "_saveLoad");
        Assert.True(await save.SaveGameAsync("menu-load", "real console menu fixture"));
        var generation = File.ReadAllBytes(_fs.SessionGenerationPath);
        SetPrivateField(engine, "_inGame", true);
        var original = AnsiConsole.Console;
        using var console = new LoadMenuAnsiConsole();
        AnsiConsole.Console = console;
        bool continued;
        try { continued = await InvokePrivateAsync<bool>(engine, "InGameOptionsMenu"); }
        finally { AnsiConsole.Console = original; }

        Assert.Equal(!failRefresh, continued);
        Assert.Equal(!failRefresh, GetPrivateFieldValue<bool>(engine, "_inGame"));
        Assert.NotEqual(generation, File.ReadAllBytes(_fs.SessionGenerationPath));
        Assert.Contains("Сохранение загружено.", console.Output);
        Assert.DoesNotContain("/private/console-refresh-cut", console.Output);
        Assert.DoesNotContain("Сохранение не загружено", console.Output);
        Assert.Equal(failRefresh ? 3 : 2, input.Reads);
        Assert.Equal(1, console.Reads);
        if (failRefresh)
        {
            Assert.Contains("Продолжение остановлено", console.Output);
            Assert.True(GetPrivateField<LoadReplacementResult>(engine, "_blockedLoadContinuation").ContinuationBlocked);
        }
        else
        {
            Assert.Null(GetPrivateFieldValue<LoadReplacementResult?>(engine, "_blockedLoadContinuation"));
            Assert.NotNull(GetPrivateFieldValue<GameResponse?>(engine, "_lastResponse"));
        }
    }

    private sealed class LoadMenuConsoleInput(IEnumerable<ConsoleKeyInfo> keys) : IConsoleInputSource
    {
        private readonly Queue<ConsoleKeyInfo> _keys = new(keys);
        internal Action? UnexpectedRead { get; set; }
        internal int Reads { get; private set; }
        public bool IsScripted => true;
        public bool KeyAvailable => true;
        public ConsoleKeyInfo ReadKey(bool intercept = true)
        {
            Reads++;
            if (_keys.TryDequeue(out var key)) return key;
            UnexpectedRead?.Invoke();
            return Key(ConsoleKey.Enter);
        }
        public string ReadLine() { Reads++; UnexpectedRead?.Invoke(); return string.Empty; }
        public void AssertCompleted() => Assert.Empty(_keys);
    }

    private sealed class LoadMenuAnsiConsole : IAnsiConsole, IAnsiConsoleInput, IDisposable
    {
        private readonly StringWriter _writer = new();
        private readonly IAnsiConsole _inner;
        internal int Reads { get; private set; }
        internal string Output => _writer.ToString();
        internal LoadMenuAnsiConsole()
        {
            _inner = AnsiConsole.Create(new AnsiConsoleSettings
            {
                Ansi = AnsiSupport.Yes,
                ColorSystem = ColorSystemSupport.NoColors,
                Interactive = InteractionSupport.Yes,
                Out = new AnsiConsoleOutput(_writer)
            });
            // Redirected Linux output reports Height=-1; the interactive test surface owns its dimensions.
            _inner.Profile.Width = 120;
            _inner.Profile.Height = 40;
        }
        public Profile Profile => _inner.Profile;
        public IAnsiConsoleCursor Cursor => _inner.Cursor;
        public IAnsiConsoleInput Input => this;
        public RenderPipeline Pipeline => _inner.Pipeline;
        public IExclusivityMode ExclusivityMode => _inner.ExclusivityMode;
        public void Clear(bool home) { }
        public void Write(IRenderable renderable) => _inner.Write(renderable);
        public bool IsKeyAvailable() => true;
        public ConsoleKeyInfo? ReadKey(bool intercept)
        {
            Assert.Equal(0, Reads++);
            return Key(ConsoleKey.Enter);
        }
        public Task<ConsoleKeyInfo?> ReadKeyAsync(bool intercept, CancellationToken cancellationToken) => Task.FromResult(ReadKey(intercept));
        public void Dispose() => _writer.Dispose();
    }

    /// <summary>Waits for the actual operation before asserting the revised typed return contract.</summary>
    /// <param name="engine">The actual console engine.</param>
    /// <param name="path">The selected source path.</param>
    /// <returns>The completed method result, including the old bool only during causal RED.</returns>
    private static async Task<object?> InvokeConsoleLoadResultAsync(GameEngine engine, string path)
    {
        var method = typeof(GameEngine).GetMethod("LoadSelectedSaveAndRebindRuntimeAsync", BindingFlags.Instance | BindingFlags.NonPublic)!;
        var task = Assert.IsAssignableFrom<Task>(method.Invoke(engine, [path]));
        await task;
        return task.GetType().GetProperty("Result")!.GetValue(task);
    }

    /// <summary>Creates one independent current-producer archive for console load tests.</summary>
    /// <param name="bootstrapSettings">Prepares the actual console settings prerequisites when requested.</param>
    /// <returns>The exact closed archive path.</returns>
    private async Task<string> CreateConsoleLoadArchiveAsync(bool bootstrapSettings = false)
    {
        var state = PortableSaveFixture.Seed(_fs);
        if (bootstrapSettings) await state.BootstrapLocalStorageAsync();
        var save = new SaveLoadService(_fs, state, NullLogger<SaveLoadService>.Instance);
        Assert.True(await save.SaveGameAsync("console-load", "typed console test"));
        return Assert.Single(Directory.GetFiles(_fs.ResolvePath("saves/manual_saves"), "*.zip"));
    }
}
