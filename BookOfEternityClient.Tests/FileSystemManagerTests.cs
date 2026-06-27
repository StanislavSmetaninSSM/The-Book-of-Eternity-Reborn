using BookOfEternityClient.Core;
using Microsoft.Extensions.Logging.Abstractions;
using Xunit;

namespace BookOfEternityClient.Tests;

public sealed class FileSystemManagerTests : IDisposable
{
    private readonly string _rootPath;
    private readonly FileSystemManager _fs;

    public FileSystemManagerTests()
    {
        _rootPath = Path.Combine(Path.GetTempPath(), "boe-filesystem-manager-" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(_rootPath);
        _fs = new FileSystemManager(_rootPath, NullLogger<FileSystemManager>.Instance);
        _fs.EnsureDirectoryStructure();
    }

    [Fact]
    public async Task ReadFileAsync_WhenFileIsBrieflyLocked_RetriesUntilContentIsReadable()
    {
        await _fs.WriteFileAtomicAsync("input/turn_request.json", "stable content");
        var fullPath = _fs.ResolvePath("input/turn_request.json");
        using var lockStream = new FileStream(fullPath, FileMode.Open, FileAccess.Read, FileShare.None);

        var readTask = _fs.ReadFileAsync("input/turn_request.json");
        await Task.Delay(100);
        await lockStream.DisposeAsync();

        var content = await readTask;

        Assert.Equal("stable content", content);
    }

    [Fact]
    public async Task WriteFileAtomicAsync_WhenTargetIsBrieflyLocked_RetriesUntilReplacementSucceeds()
    {
        await _fs.WriteFileAtomicAsync("input/turn_request.json", "old content");
        var fullPath = _fs.ResolvePath("input/turn_request.json");
        using var lockStream = new FileStream(fullPath, FileMode.Open, FileAccess.Read, FileShare.Read);

        var writeTask = _fs.WriteFileAtomicAsync("input/turn_request.json", "new content");
        await Task.Delay(100);
        await lockStream.DisposeAsync();

        await writeTask;

        Assert.Equal("new content", await _fs.ReadFileAsync("input/turn_request.json"));
    }

    [Fact]
    public async Task DeleteFile_WhenTargetIsBrieflyLocked_RetriesUntilDeleteSucceeds()
    {
        await _fs.WriteFileAtomicAsync("input/turn_request.json", "content");
        var fullPath = _fs.ResolvePath("input/turn_request.json");
        await using var lockStream = new FileStream(fullPath, FileMode.Open, FileAccess.Read, FileShare.Read);

        var deleteTask = Task.Run(() => _fs.DeleteFile("input/turn_request.json"));
        await Task.Delay(100);
        await lockStream.DisposeAsync();

        await deleteTask;

        Assert.False(File.Exists(fullPath));
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
