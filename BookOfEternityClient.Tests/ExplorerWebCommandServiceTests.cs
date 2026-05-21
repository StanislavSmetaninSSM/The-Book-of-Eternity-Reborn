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
        _service = new ExplorerWebCommandService(_fs, _stateManager, new LocalizationManager());
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
        var result = await _service.ExecuteAsync(new ExplorerWebCommandRequest("/inv"));

        Assert.Equal("/inv", result.Command);
        Assert.Equal(CommandExecutionState.Blocked, result.State);
        var message = Assert.IsType<UiMessageBlock>(Assert.Single(result.Blocks));
        Assert.Contains("#570", message.Message, StringComparison.Ordinal);
    }

    [Theory]
    [InlineData("/status")]
    [InlineData("/soul")]
    [InlineData("/codex")]
    [InlineData("/story")]
    [InlineData("/debug")]
    [InlineData("/галерея")]
    [InlineData("/saref")]
    public async Task ExecuteAsync_MigratedUniversalMetaCommands_ReturnCompletedDtos(string command)
    {
        await SeedUniversalMetaFilesAsync();

        var result = await _service.ExecuteAsync(new ExplorerWebCommandRequest(command));

        Assert.Equal(command, result.Command);
        Assert.Equal(CommandExecutionState.Completed, result.State);
        Assert.NotEmpty(result.Blocks);
        Assert.DoesNotContain(
            result.Blocks,
            static block => block is UiMessageBlock message &&
                            message.Title.Contains("пока недоступна", StringComparison.OrdinalIgnoreCase));
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

    private async Task SeedUniversalMetaFilesAsync()
    {
        await _fs.WriteFileAtomicAsync("game_state/meta/soul_state.json", """
        {
          "soulName": "Test Soul",
          "currentRealm": "Chaos Sea",
          "currentIncarnation": 3,
          "inkFeathers": { "current": 12, "total": 34 },
          "enlightenment": { "currentTier": "Искра", "experience": 42 },
          "livesHistory": [
            { "incarnation": 1, "summary": "Первая жизнь" }
          ]
        }
        """);

        await _fs.WriteFileAtomicAsync("lore/codex_entries.json", """
        {
          "entries": [
            { "title": "Первый знак", "content": "Тестовая запись кодекса" }
          ]
        }
        """);

        await _fs.WriteFileAtomicAsync("output/debug_logs.json", """
        {
          "gm_thoughts_markdown": "Тестовые мысли ГМ"
        }
        """);

        await _fs.WriteFileAtomicAsync("stories/chaos_sea.jsonl", """
        {"turn":1,"timestamp":"2026-05-20T00:00:00Z","realm":"Chaos Sea","player":"test","narrative":"story"}

        """);
    }
}
