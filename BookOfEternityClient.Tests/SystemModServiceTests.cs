using System.Text.Json;
using BookOfEternityClient.Configuration;
using BookOfEternityClient.Core;
using BookOfEternityClient.Services;
using Microsoft.Extensions.Logging.Abstractions;
using Xunit;

namespace BookOfEternityClient.Tests;

public sealed class SystemModServiceTests : IDisposable
{
    private readonly string _rootPath;
    private readonly FileSystemManager _fs;
    private readonly GameSettings _settings;
    private readonly SystemModService _service;

    public SystemModServiceTests()
    {
        _rootPath = Path.Combine(Path.GetTempPath(), "boe-tests-" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(_rootPath);
        _fs = new FileSystemManager(_rootPath, NullLogger<FileSystemManager>.Instance);
        _fs.EnsureDirectoryStructure();
        _settings = new GameSettings();
        _service = new SystemModService(_fs, _settings, NullLogger<SystemModService>.Instance);
    }

    [Fact]
    public async Task WriteManifestForGmAsync_DoesNotRewrite_WhenSemanticsUnchanged()
    {
        await File.WriteAllTextAsync(
            Path.Combine(_fs.ResolvePath("mods"), "21+.md"),
            "# Adult Rules\nAlways on.\n");
        _settings.EnabledSystemMods = new List<string> { "21+.md" };

        await _service.WriteManifestForGmAsync();
        var first = await _fs.ReadFileAsync(SystemModService.ManifestPath);
        Assert.False(string.IsNullOrWhiteSpace(first));

        await Task.Delay(20);
        await _service.WriteManifestForGmAsync();
        var second = await _fs.ReadFileAsync(SystemModService.ManifestPath);

        Assert.Equal(first, second);
    }

    [Fact]
    public async Task WriteManifestForGmAsync_RebuildsInvalidManifest()
    {
        await File.WriteAllTextAsync(
            Path.Combine(_fs.ResolvePath("mods"), "21+.md"),
            "# Adult Rules\nAlways on.\n");
        _settings.EnabledSystemMods = new List<string> { "21+.md" };

        await _fs.WriteFileAtomicAsync(SystemModService.ManifestPath, "{ invalid");
        await _service.WriteManifestForGmAsync();

        var json = await _fs.ReadFileAsync(SystemModService.ManifestPath);
        Assert.False(string.IsNullOrWhiteSpace(json));

        using var doc = JsonDocument.Parse(json!);
        Assert.Equal(JsonValueKind.Object, doc.RootElement.ValueKind);
        Assert.True(doc.RootElement.TryGetProperty("activeMods", out var activeMods));
        Assert.Equal(1, activeMods.GetArrayLength());
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
