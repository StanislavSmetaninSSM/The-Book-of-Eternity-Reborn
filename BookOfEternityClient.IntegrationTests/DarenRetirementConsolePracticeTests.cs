using System.Text.Json;
using BookOfEternityClient.AgentConsole;
using BookOfEternityClient.Configuration;
using BookOfEternityClient.Core;
using BookOfEternityClient.Services;
using Microsoft.Extensions.Logging.Abstractions;
using Xunit;
using Xunit.Abstractions;

namespace BookOfEternityClient.Tests;

public sealed class DarenRetirementConsolePracticeTests(ITestOutputHelper output)
{
    public static IEnumerable<object[]> Types() => DarenRetirementClientTests.Types();

    [Theory]
    [MemberData(nameof(Types))]
    public async Task RealConsolePractice_RunsSelectedTypeAndReturnsWithoutPersistentRewards(string type)
    {
        var root = Path.Combine(Path.GetTempPath(), "boe-daren-retirement-console-" + Guid.NewGuid().ToString("N"));
        using var cleanup = new CleanupOwnedFixture(root, output.WriteLine);
        var files = new FileSystemManager(root, NullLogger<FileSystemManager>.Instance);
        await new StateManager(files, new GameSettings(), NullLogger<StateManager>.Instance).BootstrapLocalStorageAsync();
        var state = PortableSaveFixture.Seed(files);
        state.Settings.SoundEnabled = false; state.Settings.MusicEnabled = false;
        DarenRetirementEvidenceFixtures.Put(root, DarenRetirementEvidenceFixtures.Profile, DarenRetirementEvidenceFixtures.Before);
        var soulBefore = File.ReadAllBytes(files.ResolvePath("game_state/meta/soul_state.json"));
        var store = new AgentConsoleStateStore();
        using var input = new AgentConsoleLiveInputSource(store, readTimeout: TimeSpan.FromSeconds(5));
        var loc = new LocalizationManager { CurrentLanguage = "ru" };
        var service = new QteSceneService(files, state.Settings,
            new CharacteristicsService(files, state, NullLogger<CharacteristicsService>.Instance),
            new ImageService(files, state.Settings, loc, NullLogger<ImageService>.Instance),
            new AudioService(files, state.Settings, loc, NullLogger<AudioService>.Instance),
            null!, null!, null!, state, NullLogger<QteSceneService>.Instance, input);
        var types = QteSceneService.GetPracticeCatalog().Select(x => x.TypeId).ToArray();
        var index = Array.IndexOf(types, type); Assert.True(index >= 0);
        var handled = new HashSet<string>(StringComparer.Ordinal);
        var screens = new List<object>();
        var running = Task.Run(service.RunPracticeModeAsync);
        var deadline = DateTime.UtcNow.AddSeconds(35);
        try
        {
            while (!running.IsCompleted && DateTime.UtcNow < deadline)
            {
                var snapshot = store.GetSnapshot();
                if (snapshot is { AwaitingInput: true } &&
                    (snapshot.Mode != AgentConsoleMode.QteLive || snapshot.QteFrame?.AwaitingInputKind == AgentConsoleInputKind.MenuSelection) &&
                    handled.Add(snapshot.ScreenId))
                {
                    screens.Add(new { snapshot.ScreenId, snapshot.Title, snapshot.PlainText, snapshot.Mode, Type = snapshot.QteFrame?.Type });
                    var down = snapshot.ScreenId == "qte-practice-type" ? index : snapshot.ScreenId == "qte-practice-next" ? 3 : 0;
                    for (var i = 0; i < down; i++) input.EnqueueKey(new('\0', ConsoleKey.DownArrow, false, false, false));
                    input.EnqueueKey(new('\r', ConsoleKey.Enter, false, false, false));
                }
                // Live timed games receive no fabricated grade. Their actual timeout/failure
                // path completes normally; BranchChoice resolves its authored choice.
                await Task.Delay(15);
            }
            Assert.True(running.IsCompleted, "Actual console practice failed to finish inside its owned35s deadline.");
            await running;
        }
        finally
        {
            input.Shutdown("owned retirement console fixture finished");
            if (!running.IsCompleted) await running.WaitAsync(TimeSpan.FromSeconds(10));
        }
        Assert.Contains("qte-practice-type", handled);
        Assert.Contains("qte-practice-difficulty", handled);
        Assert.Contains("qte-practice-next", handled);
        Assert.Equal(DarenRetirementEvidenceFixtures.Before, File.ReadAllBytes(Path.Combine(root, DarenRetirementEvidenceFixtures.Profile)));
        Assert.Equal(soulBefore, File.ReadAllBytes(files.ResolvePath("game_state/meta/soul_state.json")));
        output.WriteLine(JsonSerializer.Serialize(new { Type = type, ActualRunPracticeMode = true, Screens = screens,
            ProofLimit = "real console menus/RunCheck/timed completion; timed no-input outcome is not a successful minigame-input score" }));
    }
}
