using BookOfEternityClient.Core;
using BookOfEternityClient.Services;
using Microsoft.Extensions.Logging.Abstractions;
using Xunit;

namespace BookOfEternityClient.Tests;

public sealed class LocalMediaServiceTests : IDisposable
{
    private readonly string _rootPath;
    private readonly FileSystemManager _fs;
    private readonly LocalMediaService _service;

    public LocalMediaServiceTests()
    {
        _rootPath = Path.Combine(Path.GetTempPath(), "boe-local-media-" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(_rootPath);
        _fs = new FileSystemManager(_rootPath, NullLogger<FileSystemManager>.Instance);
        _fs.EnsureDirectoryStructure();
        _service = new LocalMediaService(_fs);
    }

    [Fact]
    public void EnumerateGallery_ReturnsSafeImageReferences()
    {
        WriteImage("images/npcs/hero.png");

        var item = Assert.Single(_service.EnumerateGallery());

        Assert.Equal("hero.png", item.FileName);
        Assert.Equal("images/npcs/hero.png", item.RelativePath);
        Assert.StartsWith("/api/media/", item.Url, StringComparison.Ordinal);
        Assert.Equal("image/png", item.ContentType);
        Assert.True(item.Length > 0);
    }

    [Fact]
    public void TryResolveMediaId_RejectsTraversalAndNonMediaRoots()
    {
        var traversalId = LocalMediaService.CreateMediaIdForRelativePath("images/../game_state/meta/soul_state.png");
        var stateId = LocalMediaService.CreateMediaIdForRelativePath("game_state/meta/soul_state.png");

        Assert.False(_service.TryResolveMediaId(traversalId, out _, out var traversalError));
        Assert.Contains("разреш", traversalError, StringComparison.OrdinalIgnoreCase);

        Assert.False(_service.TryResolveMediaId(stateId, out _, out var stateError));
        Assert.Contains("разреш", stateError, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public void TryResolveMediaId_RejectsMissingFiles()
    {
        var missingId = LocalMediaService.CreateMediaIdForRelativePath("images/npcs/missing.png");

        Assert.False(_service.TryResolveMediaId(missingId, out _, out var error));

        Assert.Contains("не найден", error, StringComparison.OrdinalIgnoreCase);
    }

    private void WriteImage(string relativePath)
    {
        var fullPath = _fs.ResolvePath(relativePath);
        Directory.CreateDirectory(Path.GetDirectoryName(fullPath)!);
        File.WriteAllBytes(fullPath, [137, 80, 78, 71, 13, 10, 26, 10]);
    }

    public void Dispose()
    {
        if (Directory.Exists(_rootPath))
            Directory.Delete(_rootPath, recursive: true);
    }
}
