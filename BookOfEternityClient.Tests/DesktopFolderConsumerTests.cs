using BookOfEternityClient.Core;
using BookOfEternityClient.UI;
using BookOfEternityClient.WebUi;
using Xunit;
namespace BookOfEternityClient.Tests;

[Collection("desktop-helper-console")]
public sealed class DesktopFolderConsumerTests
{
    [Theory]
    [InlineData("requested")]
    [InlineData("unavailable")]
    public async Task ActualExplorerModsKeepsOutcomeAndManualPathAtNextPrompt(string mode)
    {
        using var f = new DesktopHelpersFixture(mode);
        await f.State.BootstrapLocalStorageAsync();
        var console = new DesktopExplorerConsole("📂 Открыть папку модов", "← Назад");
        Assert.Equal("", await f.Explorer(console).TryProcessCommand("/mods"));
        f.ExactRequest(f.Mods.GetModsDirectoryPath());
        Assert.Equal(2, console.PromptScreens.Count);
        Assert.Contains(f.Mods.GetModsDirectoryPath(), console.PromptScreens[1]);
        Assert.Contains(mode == "requested" ? "Запрос открытия" : "Не удалось", console.PromptScreens[1]);
        Assert.Equal(mode == "requested" ? 0 : 1, console.KeyReads);
    }
    [Fact]
    public async Task ActualSettingsModsMenuShowsFailureAndKeepsItsOriginalAcknowledgement()
    {
        using var f = new DesktopHelpersFixture("unavailable");
        await f.State.BootstrapLocalStorageAsync();
        var session = await ConsoleSettingsSession.OpenAsync(f.Files, f.State, f.Mods);
        var input = new DesktopInput(ConsoleKey.DownArrow, ConsoleKey.Enter, ConsoleKey.Enter, ConsoleKey.Escape);
        var task = (Task)DesktopHelpersFixture.Invoke(f.Engine(input), "ShowSystemModsMenu", session, (Func<Task<BrowserPreparedWriteResult>>)(() => throw new InvalidOperationException("No settings save requested")))!;
        await task;
        f.ExactRequest(f.Mods.GetModsDirectoryPath()); input.AssertCompleted(); Assert.Equal(4, input.Reads);
        Assert.Contains("Не удалось", f.Output.ToString());
    }
    [Fact]
    public void ActualMainMenuFolderCreationFailureIsHandledBeforeAssociation()
    {
        using var f = new DesktopHelpersFixture();
        var blocker = f.Image("folder blocker"); var input = new DesktopInput(ConsoleKey.Enter);
        var error = Record.Exception(() => DesktopHelpersFixture.Invoke(f.Engine(input), "OpenFolderOrPrintPath", blocker, input));
        Assert.Null(error); Assert.Empty(f.Requests); input.AssertCompleted(); Assert.Contains(blocker, f.Output.ToString());
    }
    [Theory]
    [InlineData("requested", 0)]
    [InlineData("cancelled", 1)]
    [InlineData("unavailable", 1)]
    public void ActualMainAndRelatedExplorerFolderHelpersPreservePausePolicy(string mode, int waits)
    {
        using var f = new DesktopHelpersFixture(mode);
        var path = Path.Combine(f.Root, "Папка [yellow] 🌌"); var input = new DesktopInput(waits == 1 ? [ConsoleKey.Enter] : []);
        DesktopHelpersFixture.Invoke(f.Engine(input), "OpenFolderOrPrintPath", path, input);
        Assert.Equal(waits, input.Reads); input.AssertCompleted(); Assert.Contains(path, f.Output.ToString()); f.ExactRequest(path);
        f.Requests.Clear(); var console = new DesktopExplorerConsole();
        DesktopHelpersFixture.Invoke(f.Explorer(console), "OpenFolderOrPrintPath", path);
        Assert.Equal(waits, console.KeyReads); Assert.Contains(path, console.Output.ToString()); f.ExactRequest(path);
    }
    [Fact]
    public async Task ActualSettingsRequestedOutcomeRetainsItsOriginalSuccessAcknowledgement()
    {
        using var f = new DesktopHelpersFixture(); await f.State.BootstrapLocalStorageAsync();
        var session = await ConsoleSettingsSession.OpenAsync(f.Files, f.State, f.Mods);
        var input = new DesktopInput(ConsoleKey.DownArrow, ConsoleKey.Enter, ConsoleKey.Enter, ConsoleKey.Escape);
        await (Task)DesktopHelpersFixture.Invoke(f.Engine(input), "ShowSystemModsMenu", session,
            (Func<Task<BrowserPreparedWriteResult>>)(() => throw new InvalidOperationException("No settings save requested")))!;
        input.AssertCompleted(); Assert.Equal(4, input.Reads); f.ExactRequest(f.Mods.GetModsDirectoryPath());
        Assert.Contains("Запрос открытия", f.Output.ToString());
    }

}
