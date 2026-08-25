using BookOfEternityClient.Core;
using BookOfEternityClient.Services;
using Microsoft.Extensions.Logging.Abstractions;
using Xunit;

namespace BookOfEternityClient.Tests;

public sealed class CoordinatedStateWriteHelperTests : IDisposable
{
    private readonly string _rootPath;
    private readonly FileSystemManager _fs;

    public CoordinatedStateWriteHelperTests()
    {
        _rootPath = Path.Combine(Path.GetTempPath(), $"boe-coordinated-write-{Guid.NewGuid():N}");
        _fs = new FileSystemManager(_rootPath, NullLogger<FileSystemManager>.Instance);
        _fs.EnsureDirectoryStructure();
    }

    [Fact]
    public async Task TryCommitAsync_ConcurrentChangeAfterFirstWrite_IsNotOverwrittenByRollback()
    {
        const string firstPath = "game_state/meta/coordinated_first.json";
        const string blockedPath = "game_state/meta/coordinated_blocked.json";
        const string previousJson = "{\"value\":\"before\"}";
        const string nextJson = "{\"value\":\"client-next\"}";
        const string concurrentJson = "{\"value\":\"gm-concurrent\"}";
        await _fs.WriteFileAtomicAsync(firstPath, previousJson);

        var concurrentWriteObserved = false;
        var exception = await Record.ExceptionAsync(
            () => CoordinatedStateWriteHelper.TryCommitWithHookAsync(
                _fs,
                async write =>
                {
                    if (!string.Equals(
                            write.Path,
                            firstPath,
                            StringComparison.OrdinalIgnoreCase))
                    {
                        return;
                    }

                    concurrentWriteObserved = true;
                    await File.WriteAllTextAsync(
                        _fs.ResolvePath(firstPath),
                        concurrentJson,
                        new System.Text.UTF8Encoding(
                            encoderShouldEmitUTF8Identifier: false));
                    Directory.CreateDirectory(_fs.ResolvePath(blockedPath));
                },
                new CoordinatedStateWriteHelper.PlannedWrite(
                    firstPath,
                    previousJson,
                    nextJson,
                    true),
                new CoordinatedStateWriteHelper.PlannedWrite(
                    blockedPath,
                    null,
                    "{}",
                    true)));

        Assert.True(
            concurrentWriteObserved,
            "Fault-injection hook did not observe the first coordinated write.");
        Assert.IsType<InvalidOperationException>(exception);
        Assert.Equal(concurrentJson, await _fs.ReadFileAsync(firstPath));
    }

    [Fact]
    public async Task TryCommitAsync_HoldsCanonicalLeaseAcrossBaselineChecksAndEveryWrite()
    {
        const string firstPath = "game_state/meta/coordinated_linear_first.json";
        const string secondPath = "game_state/meta/coordinated_linear_second.json";
        const string previousJson = "{\"value\":\"before\"}";
        const string nextJson = "{\"value\":\"client-next\"}";
        const string concurrentJson = "{\"value\":\"concurrent-after\"}";
        await _fs.WriteFileAtomicAsync(firstPath, previousJson);
        await _fs.WriteFileAtomicAsync(secondPath, previousJson);

        Task? concurrentWrite = null;
        var concurrentWriteCompletedInsideTransaction = false;
        var committed = await CoordinatedStateWriteHelper.TryCommitWithHookAsync(
            _fs,
            async write =>
            {
                if (!string.Equals(write.Path, firstPath, StringComparison.Ordinal))
                    return;

                concurrentWrite = _fs.WriteFileAtomicAsync(firstPath, concurrentJson);
                var completed = await Task.WhenAny(
                    concurrentWrite,
                    Task.Delay(TimeSpan.FromMilliseconds(150)));
                concurrentWriteCompletedInsideTransaction = completed == concurrentWrite;
            },
            new CoordinatedStateWriteHelper.PlannedWrite(
                firstPath,
                previousJson,
                nextJson,
                true),
            new CoordinatedStateWriteHelper.PlannedWrite(
                secondPath,
                previousJson,
                nextJson,
                true));

        Assert.True(committed);
        Assert.False(concurrentWriteCompletedInsideTransaction);
        Assert.NotNull(concurrentWrite);
        await concurrentWrite!;
        Assert.Equal(concurrentJson, await _fs.ReadFileAsync(firstPath));
        Assert.Equal(nextJson, await _fs.ReadFileAsync(secondPath));
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
            // Ignore temp cleanup failures.
        }
    }
}
