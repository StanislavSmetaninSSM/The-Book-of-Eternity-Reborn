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
        Directory.CreateDirectory(_fs.ResolvePath(blockedPath));

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
