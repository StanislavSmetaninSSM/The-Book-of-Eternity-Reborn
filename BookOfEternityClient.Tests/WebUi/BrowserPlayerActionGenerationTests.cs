using BookOfEternityClient.Core;
using BookOfEternityClient.Services;
using BookOfEternityClient.WebUi;
using Microsoft.Extensions.Logging.Abstractions;
using Xunit;

namespace BookOfEternityClient.Tests.WebUi;

public sealed class BrowserPlayerActionGenerationTests : IDisposable
{
    private readonly string _rootPath;

    public BrowserPlayerActionGenerationTests()
    {
        _rootPath = Path.Combine(
            Path.GetTempPath(),
            "boe-browser-player-action-generation-" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(_rootPath);
    }

    [Fact]
    public async Task SubmitAsync_ConcurrentNewGameWaitsAndCannotKeepOldPendingAction()
    {
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
                CanonicalWriteLockContendedAsync = () =>
                {
                    replacementContended.TrySetResult();
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
                    afterPreflight.TrySetResult();
                    await continueSubmit.Task;
                }
            });

        var submit = service.SubmitAsync(
            new BrowserPlayerActionRequest("Я открываю запечатанное письмо."));
        await afterPreflight.Task.WaitAsync(TimeSpan.FromSeconds(5));

        var replacement = fs.ClearGameStateAsync();
        await replacementContended.Task.WaitAsync(TimeSpan.FromSeconds(5));
        Assert.False(replacement.IsCompleted);

        continueSubmit.TrySetResult();
        var result = await submit.WaitAsync(TimeSpan.FromSeconds(5));
        await replacement.WaitAsync(TimeSpan.FromSeconds(5));

        Assert.True(result.Success, result.TechnicalDetail ?? result.PlayerMessage);
        Assert.False(fs.FileExists("input/pending_player_action.json"));
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
