using System.Reflection;
using System.Text.Json;
using BookOfEternityClient.Core;
using BookOfEternityClient.Services;
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
    /// <returns>The exact closed archive path.</returns>
    private async Task<string> CreateConsoleLoadArchiveAsync()
    {
        var state = PortableSaveFixture.Seed(_fs);
        var save = new SaveLoadService(_fs, state, NullLogger<SaveLoadService>.Instance);
        Assert.True(await save.SaveGameAsync("console-load", "typed console test"));
        return Assert.Single(Directory.GetFiles(_fs.ResolvePath("saves/manual_saves"), "*.zip"));
    }
}
