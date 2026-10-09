using BookOfEternityClient.Core;
using BookOfEternityClient.Services;
using BookOfEternityClient.WebUi;
using Microsoft.Extensions.Logging.Abstractions;
using Xunit;
using Xunit.Abstractions;

namespace BookOfEternityClient.Tests.WebUi;

[Trait("Category", "RegressionIntegration")]
public sealed class BrowserPlayerActionGenerationTests : IDisposable
{
    private readonly string _rootPath;
    private readonly ITestOutputHelper _output;

    public BrowserPlayerActionGenerationTests(ITestOutputHelper output)
    {
        _output = output;
        _rootPath = Path.Combine(
            Path.GetTempPath(),
            "boe-browser-player-action-generation-" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(_rootPath);
    }

    /// <summary>
    /// Proves replacement waits for the admitted submission and then removes its
    /// pending action, using one bounded deadline for the whole concurrent operation.
    /// </summary>
    [Fact]
    public async Task SubmitAsync_ConcurrentNewGameWaitsAndCannotKeepOldPendingAction()
    {
        var paused = 0;
        var mainContentions = 0;
        var canonicalContentions = 0;
        using var testDeadline = new CancellationTokenSource(TimeSpan.FromSeconds(30));
        var afterPreflight = new TaskCompletionSource(
            TaskCreationOptions.RunContinuationsAsynchronously);
        var continueSubmit = new TaskCompletionSource(
            TaskCreationOptions.RunContinuationsAsynchronously);
        var replacementContended = new TaskCompletionSource(
            TaskCreationOptions.RunContinuationsAsynchronously);
        var fs = new FileSystemManager(
            _rootPath,
            NullLogger<FileSystemManager>.Instance,
            PhysicalLoadTransactionOperations.Instance,
            new FileSystemManagerHooks
            {
                MainOwnerLockContendedAsync = () =>
                {
                    Interlocked.Increment(ref mainContentions);
                    replacementContended.TrySetResult();
                    return Task.CompletedTask;
                },
                CanonicalWriteLockContendedAsync = () =>
                {
                    Interlocked.Increment(ref canonicalContentions);
                    return Task.CompletedTask;
                }
            });
        fs.EnsureDirectoryStructure();
        var coordinator = new BrowserLocalWriteCoordinator(
            fs,
            new LocalUiSessionLockService(fs));
        var service = new BrowserPlayerActionService(
            fs,
            coordinator,
            TimeProvider.System,
            new BrowserPlayerActionServiceHooks
            {
                AfterPreflightAsync = async () =>
                {
                    Interlocked.Increment(ref paused);
                    afterPreflight.TrySetResult();
                    await continueSubmit.Task;
                }
            });

        var submit = service.SubmitAsync(
            new BrowserPlayerActionRequest("Я открываю запечатанное письмо."));
        Task? replacement = null;
        try
        {
            Assert.Same(afterPreflight.Task, await Task.WhenAny(submit, afterPreflight.Task).WaitAsync(testDeadline.Token));

            replacement = fs.ClearGameStateAsync();
            await replacementContended.Task.WaitAsync(testDeadline.Token);
            Assert.Equal(1, paused);
            Assert.True(mainContentions > 0);
            Assert.Equal(0, canonicalContentions);
            Assert.False(replacement.IsCompleted);

            continueSubmit.TrySetResult();
            var result = await submit.WaitAsync(testDeadline.Token);
            await replacement.WaitAsync(testDeadline.Token);

            Assert.True(result.Success, result.TechnicalDetail ?? result.PlayerMessage);
            Assert.False(fs.FileExists("input/pending_player_action.json"));
        }
        finally
        {
            continueSubmit.TrySetResult();
            await Record.ExceptionAsync(() => Task.WhenAll(submit, replacement ?? Task.CompletedTask));
            _output.WriteLine(System.Text.Json.JsonSerializer.Serialize(new
            {
                kind = "f18-admission-contention",
                method = nameof(SubmitAsync_ConcurrentNewGameWaitsAndCannotKeepOldPendingAction), root = _rootPath, paused,
                mainContentions, canonicalContentions,
                firstSettled = submit?.IsCompleted ?? false,
                secondSettled = replacement?.IsCompleted ?? false
            }));
        }
    }

    public void Dispose()
    {
        try
        {
            if (Directory.Exists(_rootPath))
                Directory.Delete(_rootPath, recursive: true);
        }
        catch
        {
            // ignored
        }
    }
}
