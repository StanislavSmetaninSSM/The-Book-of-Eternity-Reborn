using BookOfEternityClient.Services;
using Xunit;

namespace BookOfEternityClient.Tests;

public sealed partial class ExplorerModeCommandTests
{
    [Fact]
    public async Task TryProcessCommand_ReadOnlyHelp_IgnoresActiveOtherOwnerLock()
    {
        await SeedSessionForCommandAsync("/душа");
        await _stateManager.RefreshGameStateAsync();
        await new LocalUiSessionLockService(_fs).AcquireOrRefreshAsync(
            new LocalUiSessionLockOwner("browser-tab", "browser", "Браузерная вкладка", TimeSpan.FromMinutes(2)),
            "браузерная операция");

        var result = await _explorer.TryProcessCommand("/помощь");

        Assert.Equal("", result);
        var renderedText = ExtractRenderedText();
        Assert.Contains("МОРЕ ХАОСА", renderedText, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("заблокировано", renderedText, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public async Task TryProcessCommand_MutatingSpiritualAction_BlockedByActiveOtherOwnerLock()
    {
        await SeedSessionForCommandAsync("/душа");
        await _stateManager.RefreshGameStateAsync();
        await new LocalUiSessionLockService(_fs).AcquireOrRefreshAsync(
            new LocalUiSessionLockOwner("browser-tab", "browser", "Браузерная вкладка", TimeSpan.FromMinutes(2)),
            "браузерная операция");

        var result = await _explorer.TryProcessCommand("/spiritual_action pressure");

        Assert.Equal("", result);
        var renderedText = string.Join("\n", _console.MarkupLines);
        Assert.Contains("заблокировано", renderedText, StringComparison.OrdinalIgnoreCase);
        Assert.Contains("Браузерная вкладка", renderedText, StringComparison.OrdinalIgnoreCase);
        Assert.Contains(LocalUiSessionLockService.LockPath, renderedText, StringComparison.OrdinalIgnoreCase);
    }
}
