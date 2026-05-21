using BookOfEternityClient.CommandProtocol;
using BookOfEternityClient.Configuration;
using BookOfEternityClient.Core;
using BookOfEternityClient.UI;
using BookOfEternityClient.WebUi;
using Microsoft.Extensions.Logging.Abstractions;
using Xunit;

namespace BookOfEternityClient.Tests;

public sealed class ExplorerWebCommandServiceTests : IDisposable
{
    private readonly string _rootPath;
    private readonly FileSystemManager _fs;
    private readonly StateManager _stateManager;
    private readonly ExplorerWebCommandService _service;

    public ExplorerWebCommandServiceTests()
    {
        _rootPath = Path.Combine(Path.GetTempPath(), "boe-web-command-service-" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(_rootPath);
        _fs = new FileSystemManager(_rootPath, NullLogger<FileSystemManager>.Instance);
        _fs.EnsureDirectoryStructure();
        _stateManager = new StateManager(_fs, new GameSettings(), NullLogger<StateManager>.Instance);
        _service = new ExplorerWebCommandService(_stateManager, new LocalizationManager());
    }

    [Fact]
    public async Task ExecuteAsync_MigratedHelp_ReturnsCompletedDto()
    {
        var result = await _service.ExecuteAsync(new ExplorerWebCommandRequest("/help"));

        Assert.Equal("/help", result.Command);
        Assert.Equal(CommandExecutionState.Completed, result.State);
        Assert.Contains(result.Blocks, static block => block is UiTableBlock table && table.Columns.Contains("Описание"));
    }

    [Fact]
    public async Task ExecuteAsync_MutatingCommand_ReturnsBlockedDto()
    {
        var result = await _service.ExecuteAsync(new ExplorerWebCommandRequest("/spiritual_action"));

        Assert.Equal("/spiritual_action", result.Command);
        Assert.Equal(CommandExecutionState.Blocked, result.State);
        var message = Assert.IsType<UiMessageBlock>(Assert.Single(result.Blocks));
        Assert.Contains("браузерном API", message.Title, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public async Task ExecuteAsync_PlannedCommand_ReturnsBlockedDto()
    {
        var result = await _service.ExecuteAsync(new ExplorerWebCommandRequest("/status"));

        Assert.Equal("/status", result.Command);
        Assert.Equal(CommandExecutionState.Blocked, result.State);
        var message = Assert.IsType<UiMessageBlock>(Assert.Single(result.Blocks));
        Assert.Contains("#569", message.Message, StringComparison.Ordinal);
    }

    [Fact]
    public async Task ExecuteAsync_EmptyCommand_ReturnsFailedDto()
    {
        var result = await _service.ExecuteAsync(new ExplorerWebCommandRequest("   "));

        Assert.Equal(CommandExecutionState.Failed, result.State);
        var message = Assert.IsType<UiMessageBlock>(Assert.Single(result.Blocks));
        Assert.Contains("пустая", message.Message, StringComparison.OrdinalIgnoreCase);
    }

    public void Dispose()
    {
        if (Directory.Exists(_rootPath))
            Directory.Delete(_rootPath, recursive: true);
    }
}
