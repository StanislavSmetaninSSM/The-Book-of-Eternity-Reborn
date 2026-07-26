using BookOfEternityClient.Core;
using BookOfEternityClient.Services;
using Microsoft.Extensions.Logging.Abstractions;
using Xunit;

namespace BookOfEternityClient.Tests;

public sealed class StoryServiceTests : IDisposable
{
    private readonly string _rootPath;
    private readonly FileSystemManager _fs;
    private readonly StoryService _storyService;

    public StoryServiceTests()
    {
        _rootPath = Path.Combine(
            Path.GetTempPath(),
            "boe-story-service-" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(_rootPath);
        _fs = new FileSystemManager(_rootPath, NullLogger<FileSystemManager>.Instance);
        _fs.EnsureDirectoryStructure();
        _storyService = new StoryService(_fs, NullLogger<StoryService>.Instance);
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task StoryAppend_WaitsForCanonicalWriteLease(bool marker)
    {
        Task appendTask;
        var writeLease = await _fs.AcquireCanonicalWriteLeaseAsync();
        try
        {
            appendTask = marker
                ? _storyService.AppendMarkerAsync(
                    "Mortal World",
                    1,
                    "transition",
                    "marker narrative")
                : _storyService.AppendTurnAsync(
                    7,
                    "Mortal World",
                    1,
                    "player action",
                    "turn narrative");
            await Task.Delay(150);
            Assert.False(appendTask.IsCompleted);
        }
        finally
        {
            await writeLease.DisposeAsync();
        }

        await appendTask;
        var story = await _fs.ReadFileAsync("stories/mortal_life_1.jsonl");
        Assert.Contains(marker ? "marker narrative" : "turn narrative", story, StringComparison.Ordinal);
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task StoryAppend_EscapedClosedSession_PropagatesSessionReplaced(bool marker)
    {
        string generation;
        await using (var lease = await _fs.AcquireCanonicalWriteLeaseAsync())
            generation = _fs.GetOrCreateSessionGeneration(lease);

        var releaseEscapedAppend = new TaskCompletionSource(
            TaskCreationOptions.RunContinuationsAsynchronously);
        Task? escapedAppend = null;
        await SessionOperationContext.RunBoundAsync(
            _fs,
            generation,
            () =>
            {
                escapedAppend = Task.Run(async () =>
                {
                    await releaseEscapedAppend.Task;
                    if (marker)
                    {
                        await _storyService.AppendMarkerAsync(
                            "Mortal World",
                            1,
                            "transition",
                            "must not be swallowed");
                    }
                    else
                    {
                        await _storyService.AppendTurnAsync(
                            7,
                            "Mortal World",
                            1,
                            "player action",
                            "must not be swallowed");
                    }
                });
                return Task.CompletedTask;
            });

        Assert.NotNull(escapedAppend);
        releaseEscapedAppend.TrySetResult();
        await Assert.ThrowsAsync<SessionReplacedException>(
            () => escapedAppend!.WaitAsync(TimeSpan.FromSeconds(5)));
        Assert.False(_fs.FileExists("stories/mortal_life_1.jsonl"));
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
            // Best effort cleanup for temp test data.
        }
    }
}
