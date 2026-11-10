using System.Text.Json;
using BookOfEternityClient.Services;
using BookOfEternityClient.UI;
using Microsoft.Extensions.Logging.Abstractions;
using Spectre.Console;
using Spectre.Console.Rendering;
using Xunit;
using Xunit.Abstractions;

namespace BookOfEternityClient.Tests;

[Collection("desktop-helper-console")]
public sealed class WorldProfileFolderConsumerTests(ITestOutputHelper output)
{
    [Fact]
    public void OriginalWorldSetupFolderRouteUsesManagedAssociation()
    {
        var path = Path.Combine(TestRepoPaths.RepoRoot,
            "BookOfEternityClient/UI/ExplorerMode/ExplorerMode.MetaWorldSetupAndDebug.cs");
        var source = File.ReadAllText(path);
        var start = source.IndexOf("private async Task ShowWorldSetup()", StringComparison.Ordinal);
        var end = source.IndexOf("private async Task ShowWorldRules()", start, StringComparison.Ordinal);
        Assert.True(start >= 0 && end > start);
        var body = source[start..end];
        Assert.DoesNotContain("Process.Start", body, StringComparison.Ordinal);
        Assert.Contains("OpenFolderOrPrintPath(profilesDir);", body, StringComparison.Ordinal);
    }

    [Theory]
    [InlineData("requested", 0)]
    [InlineData("unavailable", 1)]
    public async Task ActualWorldSetupFolderKeepsManualPathAndOriginalPause(string mode, int waits)
    {
        var fixture = new DesktopHelpersFixture(mode);
        var root = Directory.GetParent(fixture.Root)!.FullName;
        using (fixture)
        {
            await fixture.State.BootstrapLocalStorageAsync();
            await fixture.Files.WriteFileAtomicAsync("game_state/meta/soul_state.json",
                """{ "soulName":"Облачная душа", "currentRealm":"Chaos Sea", "currentIncarnation":1, "inkFeathers":{"current":3} }""");
            await fixture.State.RefreshGameStateAsync();
            Assert.True(fixture.State.CurrentState.IsInAfterlifeRealm);
            var world = new WorldDirectiveService(fixture.Files, NullLogger<WorldDirectiveService>.Instance);
            var scripted = new DesktopExplorerConsole("📂 Открыть папку профилей", "← Назад");
            var capture = new FolderTranscriptConsole(scripted);
            var explorer = new ExplorerMode(fixture.State, fixture.Files, new(), worldDirectiveService: world,
                console: capture, desktopPathOpener: fixture.Opener);
            Assert.Equal("", await explorer.TryProcessCommand("/world_setup"));
            capture.Screens.Add(scripted.Output.ToString());
            fixture.ExactRequest(world.GetProfilesDirectoryPath());
            Assert.Equal(2, scripted.PromptScreens.Count); Assert.Equal(waits, scripted.KeyReads);
            var transcript = string.Join("\n", capture.Screens);
            Assert.Contains(world.GetProfilesDirectoryPath(), transcript);
            Assert.Contains(mode == "requested" ? "Запрос открытия" : "Не удалось", transcript);
            output.WriteLine(JsonSerializer.Serialize(new { mode, root, waits, scripted.KeyReads,
                path = world.GetProfilesDirectoryPath(), requests = fixture.Requests.Select(r => new { r.FileName, r.UseShellExecute, r.Arguments }),
                transcript, promptCount = scripted.PromptScreens.Count }));
        }
        output.WriteLine(JsonSerializer.Serialize(new { CleanupOwnedRoot = root, OwnedFixtureRemoved = !Directory.Exists(root) }));
        Assert.False(Directory.Exists(root));
    }

    private sealed class FolderTranscriptConsole(DesktopExplorerConsole inner) : IExplorerConsole
    {
        internal List<string> Screens { get; } = [];
        public void Clear() { Screens.Add(inner.Output.ToString()); inner.Clear(); }
        public void Write(IRenderable content) => inner.Write(content);
        public void Markup(string markup) => inner.Markup(markup);
        public void MarkupLine(string markup) => inner.MarkupLine(markup);
        public void WriteLine() => inner.WriteLine();
        public string Ask(string prompt, string defaultValue = "") => inner.Ask(prompt, defaultValue);
        public bool Confirm(string prompt, bool defaultValue = false) => inner.Confirm(prompt, defaultValue);
        public T Prompt<T>(IPrompt<T> prompt) => inner.Prompt(prompt);
        public string? ReadLine() => inner.ReadLine();
        public bool KeyAvailable => inner.KeyAvailable;
        public ConsoleKeyInfo ReadKey() => inner.ReadKey();
    }
}
