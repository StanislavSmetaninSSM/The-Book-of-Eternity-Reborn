using BookOfEternityClient.Core;
using BookOfEternityClient.Configuration;
using BookOfEternityClient.Services;
using Microsoft.Extensions.Logging.Abstractions;
using System.Reflection;
using Xunit;

namespace BookOfEternityClient.Tests;

public sealed class SessionOperationContextTests : IDisposable
{
    private readonly string _rootPath;
    private readonly FileSystemManager _fs;

    public SessionOperationContextTests()
    {
        _rootPath = Path.Combine(
            Path.GetTempPath(),
            "boe-session-operation-" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(_rootPath);
        _fs = new FileSystemManager(_rootPath, NullLogger<FileSystemManager>.Instance);
        _fs.EnsureDirectoryStructure();
    }

    [Fact]
    public async Task BoundWriter_ClearRotatesGeneration_ThrowsBeforeReplacementMutation()
    {
        const string sentinelPath = "game_state/world/replacement.json";
        var generation = await GetOrCreateSessionGenerationAsync(_fs);
        var operationStarted = new TaskCompletionSource(
            TaskCreationOptions.RunContinuationsAsynchronously);
        var releaseOldOperation = new TaskCompletionSource(
            TaskCreationOptions.RunContinuationsAsynchronously);

        var oldOperation = Task.Run(() => SessionOperationContext.RunBoundAsync(
            _fs,
            generation,
            async () =>
            {
                operationStarted.TrySetResult();
                await releaseOldOperation.Task;
                await _fs.WriteFileAtomicAsync(sentinelPath, "{\"owner\":\"old\"}");
            }));

        await operationStarted.Task.WaitAsync(TimeSpan.FromSeconds(5));
        await _fs.ClearGameStateAsync();
        await _fs.WriteFileAtomicAsync(sentinelPath, "{\"owner\":\"replacement\"}");
        releaseOldOperation.TrySetResult();

        await Assert.ThrowsAsync<SessionReplacedException>(() => oldOperation);
        Assert.Equal(
            "{\"owner\":\"replacement\"}",
            await _fs.ReadFileAsync(sentinelPath));
    }

    [Fact]
    public async Task BoundWriter_CurrentGeneration_UsesOrdinaryCanonicalWriter()
    {
        const string targetPath = "game_state/world/current.json";
        var generation = await GetOrCreateSessionGenerationAsync(_fs);

        await SessionOperationContext.RunBoundAsync(
            _fs,
            generation,
            () => _fs.WriteFileAtomicAsync(targetPath, "{\"owner\":\"current\"}"));

        Assert.Equal("{\"owner\":\"current\"}", await _fs.ReadFileAsync(targetPath));
    }

    [Fact]
    public async Task BoundWriter_FenceAppliesAcrossFileSystemManagerInstancesForSameRoot()
    {
        const string sentinelPath = "game_state/world/cross-instance.json";
        var secondFs = new FileSystemManager(
            _rootPath,
            NullLogger<FileSystemManager>.Instance);
        secondFs.EnsureDirectoryStructure();
        var generation = await GetOrCreateSessionGenerationAsync(_fs);
        var operationStarted = new TaskCompletionSource(
            TaskCreationOptions.RunContinuationsAsynchronously);
        var releaseOldOperation = new TaskCompletionSource(
            TaskCreationOptions.RunContinuationsAsynchronously);

        var oldOperation = Task.Run(() => SessionOperationContext.RunBoundAsync(
            _fs,
            generation,
            async () =>
            {
                operationStarted.TrySetResult();
                await releaseOldOperation.Task;
                await secondFs.WriteFileAtomicAsync(sentinelPath, "{\"owner\":\"old\"}");
            }));

        await operationStarted.Task.WaitAsync(TimeSpan.FromSeconds(5));
        await _fs.ClearGameStateAsync();
        await _fs.WriteFileAtomicAsync(sentinelPath, "{\"owner\":\"replacement\"}");
        releaseOldOperation.TrySetResult();

        await Assert.ThrowsAsync<SessionReplacedException>(() => oldOperation);
        Assert.Equal(
            "{\"owner\":\"replacement\"}",
            await _fs.ReadFileAsync(sentinelPath));
    }

    [Fact]
    public async Task BoundOperation_LeafSwallowsWriteFailure_OuterBoundaryStillThrows()
    {
        var generation = await GetOrCreateSessionGenerationAsync(_fs);
        var operationStarted = new TaskCompletionSource(
            TaskCreationOptions.RunContinuationsAsynchronously);
        var releaseOldOperation = new TaskCompletionSource(
            TaskCreationOptions.RunContinuationsAsynchronously);

        var oldOperation = Task.Run(() => SessionOperationContext.RunBoundAsync(
            _fs,
            generation,
            async () =>
            {
                operationStarted.TrySetResult();
                await releaseOldOperation.Task;
                try
                {
                    await _fs.WriteFileAtomicAsync(
                        "game_state/world/swallowed.json",
                        "{\"owner\":\"old\"}");
                }
                catch (SessionReplacedException)
                {
                    // Simulates a legacy leaf service that logs and suppresses writer failures.
                }
            }));

        await operationStarted.Task.WaitAsync(TimeSpan.FromSeconds(5));
        await _fs.ClearGameStateAsync();
        releaseOldOperation.TrySetResult();

        await Assert.ThrowsAsync<SessionReplacedException>(() => oldOperation);
        Assert.False(_fs.FileExists("game_state/world/swallowed.json"));
    }

    [Fact]
    public async Task BoundOperation_ReplacementWithoutLaterWrite_FinalCheckStillThrows()
    {
        var generation = await GetOrCreateSessionGenerationAsync(_fs);
        var operationStarted = new TaskCompletionSource(
            TaskCreationOptions.RunContinuationsAsynchronously);
        var releaseOldOperation = new TaskCompletionSource(
            TaskCreationOptions.RunContinuationsAsynchronously);

        var oldOperation = Task.Run(() => SessionOperationContext.RunBoundAsync(
            _fs,
            generation,
            async () =>
            {
                operationStarted.TrySetResult();
                await releaseOldOperation.Task;
            }));

        await operationStarted.Task.WaitAsync(TimeSpan.FromSeconds(5));
        await _fs.ClearGameStateAsync();
        releaseOldOperation.TrySetResult();

        await Assert.ThrowsAsync<SessionReplacedException>(() => oldOperation);
    }

    [Fact]
    public async Task BoundWriter_LoadRotatesGeneration_ThrowsBeforeReplacementMutation()
    {
        const string targetPath = "game_state/world/load-owner.json";
        await _fs.WriteFileAtomicAsync(
            "game_state/meta/soul_state.json",
            """{"currentRealm":"Mortal World"}""");
        await _fs.WriteFileAtomicAsync(targetPath, "{\"owner\":\"saved\"}");
        var stateManager = new StateManager(
            _fs,
            new GameSettings(),
            NullLogger<StateManager>.Instance);
        await stateManager.RefreshGameStateAsync();
        var saveLoad = new SaveLoadService(
            _fs,
            stateManager,
            NullLogger<SaveLoadService>.Instance);
        Assert.True(await saveLoad.SaveGameAsync("session_fence", "session fence regression"));
        var savePath = Directory.GetFiles(
            _fs.ResolvePath("saves/manual_saves"),
            "*.zip").Single();
        await _fs.WriteFileAtomicAsync(targetPath, "{\"owner\":\"old-live\"}");

        var generation = await GetOrCreateSessionGenerationAsync(_fs);
        var operationStarted = new TaskCompletionSource(
            TaskCreationOptions.RunContinuationsAsynchronously);
        var releaseOldOperation = new TaskCompletionSource(
            TaskCreationOptions.RunContinuationsAsynchronously);
        var oldOperation = Task.Run(() => SessionOperationContext.RunBoundAsync(
            _fs,
            generation,
            async () =>
            {
                operationStarted.TrySetResult();
                await releaseOldOperation.Task;
                await _fs.WriteFileAtomicAsync(targetPath, "{\"owner\":\"stale\"}");
            }));

        await operationStarted.Task.WaitAsync(TimeSpan.FromSeconds(5));
        Assert.True(await saveLoad.LoadGameAsync(savePath));
        releaseOldOperation.TrySetResult();

        await Assert.ThrowsAsync<SessionReplacedException>(() => oldOperation);
        Assert.Equal("{\"owner\":\"saved\"}", await _fs.ReadFileAsync(targetPath));
    }

    [Fact]
    public async Task BoundOperation_EscapedTaskCannotWriteAfterOwningScopeCloses()
    {
        var generation = await GetOrCreateSessionGenerationAsync(_fs);
        var releaseEscapedTask = new TaskCompletionSource(
            TaskCreationOptions.RunContinuationsAsynchronously);
        Task? escapedTask = null;

        await SessionOperationContext.RunBoundAsync(
            _fs,
            generation,
            () =>
            {
                escapedTask = Task.Run(async () =>
                {
                    await releaseEscapedTask.Task;
                    await _fs.WriteFileAtomicAsync(
                        "game_state/world/escaped.json",
                        "{\"owner\":\"escaped\"}");
                });
                return Task.CompletedTask;
            });

        Assert.NotNull(escapedTask);
        releaseEscapedTask.TrySetResult();
        await Assert.ThrowsAsync<SessionReplacedException>(() => escapedTask!);
        Assert.False(_fs.FileExists("game_state/world/escaped.json"));
    }

    [Fact]
    public async Task BoundOperation_ClosingStartsBeforeFinalLeaseAndRejectsEscapedWriter()
    {
        var closingStarted = new TaskCompletionSource(
            TaskCreationOptions.RunContinuationsAsynchronously);
        var releaseClosing = new TaskCompletionSource(
            TaskCreationOptions.RunContinuationsAsynchronously);
        var hooks = new FileSystemManagerHooks();
        var closingHook = typeof(FileSystemManagerHooks).GetProperty(
            "SessionOperationClosingAsync",
            BindingFlags.Instance | BindingFlags.NonPublic);
        Assert.NotNull(closingHook);
        closingHook!.SetValue(
            hooks,
            (Func<Task>)(async () =>
            {
                closingStarted.TrySetResult();
                await releaseClosing.Task;
            }));

        var hookedFs = new FileSystemManager(
            _rootPath,
            NullLogger<FileSystemManager>.Instance,
            PhysicalLoadTransactionOperations.Instance,
            hooks);
        var generation = await GetOrCreateSessionGenerationAsync(hookedFs);
        var releaseEscapedWriter = new TaskCompletionSource(
            TaskCreationOptions.RunContinuationsAsynchronously);
        Task? escapedWriter = null;

        var owner = SessionOperationContext.RunBoundAsync(
            hookedFs,
            generation,
            () =>
            {
                escapedWriter = Task.Run(async () =>
                {
                    await releaseEscapedWriter.Task;
                    await hookedFs.WriteFileAtomicAsync(
                        "game_state/world/closing-race.json",
                        "{\"owner\":\"escaped\"}");
                });
                return Task.CompletedTask;
            });

        await closingStarted.Task.WaitAsync(TimeSpan.FromSeconds(5));
        Assert.NotNull(escapedWriter);
        releaseEscapedWriter.TrySetResult();
        await Assert.ThrowsAsync<SessionReplacedException>(
            () => escapedWriter!.WaitAsync(TimeSpan.FromSeconds(5)));
        releaseClosing.TrySetResult();
        await owner.WaitAsync(TimeSpan.FromSeconds(5));

        Assert.False(hookedFs.FileExists("game_state/world/closing-race.json"));
    }

    private static async Task<string> GetOrCreateSessionGenerationAsync(FileSystemManager fs)
    {
        await using var writeLease = await fs.AcquireCanonicalWriteLeaseAsync();
        return fs.GetOrCreateSessionGeneration(writeLease);
    }

    public void Dispose()
    {
        if (Directory.Exists(_rootPath))
            Directory.Delete(_rootPath, recursive: true);
    }
}
