using System.Text.Json;
using BookOfEternityClient.Core;
using BookOfEternityClient.WebUi;
using Microsoft.Extensions.Logging.Abstractions;
using Xunit;

namespace BookOfEternityClient.Tests.WebUi;

public sealed class BrowserPendingPlayerActionTests : IDisposable
{
    private readonly string _root = Path.Combine(Path.GetTempPath(), "boe-pending-action-" + Guid.NewGuid().ToString("N"));
    private readonly FileSystemManager _fs;
    private readonly BrowserLocalWriteCoordinator _coordinator;
    private readonly BrowserPlayerActionService _service;

    public BrowserPendingPlayerActionTests()
    {
        _fs = new FileSystemManager(_root, NullLogger<FileSystemManager>.Instance);
        _fs.EnsureDirectoryStructure();
        _coordinator = new BrowserLocalWriteCoordinator(_fs, new LocalUiSessionLockService(_fs));
        _service = new BrowserPlayerActionService(_fs, _coordinator);
    }

    [Fact]
    public async Task Submit_SecondActionCannotOverwriteFirstPendingAction()
    {
        var first = await _service.SubmitAsync(new("Я открываю письмо."));
        Assert.True(first.Success, first.PlayerMessage);
        var before = File.ReadAllBytes(_fs.ResolvePath("input/pending_player_action.json"));
        var second = await _service.SubmitAsync(new("Я закрываю дверь."));
        Assert.False(second.Success);
        Assert.Equal(before, File.ReadAllBytes(_fs.ResolvePath("input/pending_player_action.json")));
    }

    [Fact]
    public async Task Submit_EnvelopeBindsExactGenerationAndUniqueActionIdentity()
    {
        var generation = await _coordinator.RunBoundTransactionAsync(
            lease => Task.FromResult(_fs.GetOrCreateSessionGeneration(lease)));
        var result = await _service.SubmitAsync(new("Я читаю письмо."));
        Assert.True(result.Success, result.PlayerMessage);
        using var pending = JsonDocument.Parse(File.ReadAllBytes(_fs.ResolvePath("input/pending_player_action.json")));
        Assert.Equal(generation, pending.RootElement.GetProperty("sessionGeneration").GetString());
        Assert.True(Guid.TryParseExact(pending.RootElement.GetProperty("actionId").GetString(), "N", out _));
        Assert.Equal("browser-composer", pending.RootElement.GetProperty("source").GetString());
    }

    public void Dispose()
    {
        if (Directory.Exists(_root)) Directory.Delete(_root, recursive: true);
    }
}
