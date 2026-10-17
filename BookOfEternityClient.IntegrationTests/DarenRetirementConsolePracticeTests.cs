using System.Text.Json;
using BookOfEternityClient.AgentConsole;
using BookOfEternityClient.Configuration;
using BookOfEternityClient.Core;
using BookOfEternityClient.Services;
using BookOfEternityClient.UI;
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
        var cleanup = new CleanupOwnedFixture(root, output.WriteLine);
        AgentConsoleLiveInputSource? input = null;
        AgentConsoleStateStore? store = null;
        Task? running = null;
        var inputs = new List<object>();
        var rejectedInputs = new List<object>();
        var screens = new List<object>();
        var seenScreens = new HashSet<string>(StringComparer.Ordinal);
        var submitted = new HashSet<(string Screen, DateTimeOffset Updated)>();
        AgentConsoleSnapshot? difficultyWitness = null, chapterWitness = null, resultWitness = null, liveWitness = null;
        var chargeKeys = 0;
        var expectedChapter = $"qte-chapter-practice-{type.ToLowerInvariant()}-easy-practice-challenge";
        var expectedResult = $"qte-result-practice-{type.ToLowerInvariant()}-easy-practice-challenge";
        var catalog = QteSceneService.GetPracticeCatalog();
        var index = Array.FindIndex(catalog.ToArray(), entry => entry.TypeId == type);
        Assert.True(index >= 0);

        // One real accepted key per ready snapshot prevents a lost Down/Enter from
        // silently selecting another game. No direct grade or scripted result is supplied.
        bool Send(AgentConsoleSnapshot snapshot, ConsoleKey key, bool teardown)
        {
            if (!submitted.Add((snapshot.ScreenId, snapshot.UpdatedAtUtc))) return false;
            var character = key == ConsoleKey.Spacebar ? ' ' : key == ConsoleKey.Enter ? '\r' : '\0';
            var result = input!.EnqueueKey(new(character, key, false, false, false));
            inputs.Add(new { snapshot.ScreenId, snapshot.UpdatedAtUtc, snapshot.SelectedIndex, Key = key.ToString(),
                result.Accepted, result.RejectionCode, AcceptedScreenId = result.Event.ScreenId, Teardown = teardown });
            if (!teardown && (!result.Accepted || result.Event.ScreenId != snapshot.ScreenId))
                rejectedInputs.Add(new { snapshot.ScreenId, result.Accepted, result.RejectionCode, ActualScreen = result.Event.ScreenId });
            return result.Accepted;
        }

        void Drive(bool teardown)
        {
            var snapshot = store!.GetSnapshot();
            if (snapshot is not { AwaitingInput: true }) return;
            if (seenScreens.Add(snapshot.ScreenId))
                screens.Add(new { snapshot.ScreenId, snapshot.Title, snapshot.PlainText, snapshot.Mode, Type = snapshot.QteFrame?.Type });
            if (!teardown)
            {
                if (snapshot.ScreenId == "qte-practice-difficulty") difficultyWitness ??= snapshot;
                if (snapshot.ScreenId == expectedChapter) chapterWitness ??= snapshot;
                if (snapshot.ScreenId == expectedResult) resultWitness ??= snapshot;
            }
            if (snapshot.Mode == AgentConsoleMode.QteLive && snapshot.InputKind == AgentConsoleInputKind.Key)
            {
                if (!teardown) liveWitness ??= snapshot;
                if (teardown) Send(snapshot, ConsoleKey.Escape, teardown: true);
                else if (snapshot.QteFrame?.Type == "ChargeRelease" && chargeKeys < 2 && Send(snapshot, ConsoleKey.Spacebar, teardown: false))
                    chargeKeys++; // Actual begin/release inputs; no target-score or fake-clock assumption.
                return;
            }
            if (snapshot.InputKind == AgentConsoleInputKind.MenuSelection)
            {
                var target = snapshot.ScreenId == "qte-practice-type" ? (teardown ? catalog.Count : index)
                    : snapshot.ScreenId == "qte-practice-next" ? 3
                    : teardown && snapshot.ScreenId.StartsWith("qte-result-", StringComparison.Ordinal) ? snapshot.Actions.Count - 1 : 0;
                var selected = snapshot.SelectedIndex.GetValueOrDefault();
                Send(snapshot, selected == target ? ConsoleKey.Enter : selected < target ? ConsoleKey.DownArrow : ConsoleKey.UpArrow, teardown);
            }
            else if (snapshot.InputKind == AgentConsoleInputKind.Key)
                Send(snapshot, ConsoleKey.Enter, teardown); // The actual chapter ready gate.
        }

        try
        {
            var files = new FileSystemManager(root, NullLogger<FileSystemManager>.Instance);
            await new StateManager(files, new GameSettings(), NullLogger<StateManager>.Instance).BootstrapLocalStorageAsync();
            var state = PortableSaveFixture.Seed(files);
            state.Settings.SoundEnabled = false; state.Settings.MusicEnabled = false;
            DarenRetirementEvidenceFixtures.Put(root, DarenRetirementEvidenceFixtures.Profile, DarenRetirementEvidenceFixtures.Before);
            var soulBefore = File.ReadAllBytes(files.ResolvePath("game_state/meta/soul_state.json"));
            store = new AgentConsoleStateStore();
            input = new AgentConsoleLiveInputSource(store, readTimeout: TimeSpan.FromSeconds(5));
            var loc = new LocalizationManager { CurrentLanguage = "ru" };
            var service = new QteSceneService(files, state.Settings,
                new CharacteristicsService(files, state, NullLogger<CharacteristicsService>.Instance),
                new ImageService(files, state.Settings, loc, NullLogger<ImageService>.Instance),
                new AudioService(files, state.Settings, NullLogger<AudioService>.Instance),
                null!, null!, null!, state, NullLogger<QteSceneService>.Instance, input);
            running = Task.Run(service.RunPracticeModeAsync);
            var deadline = DateTime.UtcNow.AddSeconds(35);
            while (!running.IsCompleted && DateTime.UtcNow < deadline)
            {
                Drive(teardown: false);
                await Task.Delay(15);
            }
            Assert.True(running.IsCompleted, "Actual console practice did not finish inside its owned35s deadline.");
            await running;
            Assert.Empty(rejectedInputs);
            Assert.NotNull(difficultyWitness); Assert.Contains(catalog[index].Title, difficultyWitness.PlainText, StringComparison.Ordinal);
            Assert.NotNull(chapterWitness); Assert.Equal(catalog[index].Title, chapterWitness.Title);
            Assert.NotNull(resultWitness); // Exact selected-offer result appears only after the real RunCheck returns.
            if (type == "BranchChoice") Assert.Null(liveWitness); // Original branch grade is synchronous, with no live minigame frame.
            else
            {
                Assert.NotNull(liveWitness); Assert.NotNull(liveWitness.QteFrame);
                var expectedFrame = type is "MashInput" or "RhythmPulse" or "PrecisionChoice" ? "QteLive" : type;
                var expectedTitle = type switch { "MashInput" => "Рывок на усилие", "RhythmPulse" => "Ритм пульса", _ => catalog[index].Title };
                Assert.Equal(expectedFrame, liveWitness.QteFrame.Type);
                Assert.StartsWith(expectedTitle, liveWitness.QteFrame.Title, StringComparison.Ordinal);
            }
            if (type == "ChargeRelease") Assert.Equal(2, chargeKeys);
            Assert.Contains("qte-practice-next", seenScreens);
            Assert.Equal(DarenRetirementEvidenceFixtures.Before, File.ReadAllBytes(Path.Combine(root, DarenRetirementEvidenceFixtures.Profile)));
            Assert.Equal(soulBefore, File.ReadAllBytes(files.ResolvePath("game_state/meta/soul_state.json")));
            output.WriteLine(JsonSerializer.Serialize(new { Type = type, ActualRunPracticeMode = true, ExpectedChapter = expectedChapter,
                ObservedChapter = chapterWitness.ScreenId, ObservedResult = resultWitness.ScreenId,
                Frame = liveWitness == null ? null : new { liveWitness.ScreenId, liveWitness.QteFrame!.Type, liveWitness.QteFrame.Title, liveWitness.QteFrame.Phase },
                ChargeReleaseAcceptedKeys = chargeKeys, Screens = screens, Inputs = inputs,
                ProofLimit = "real console selected-offer ready gate/RunCheck/result; two genuine ChargeRelease keys, other live games use their actual bounded no-input outcome; not eleven successful-score playthroughs" }));
        }
        finally
        {
            if (running is { IsCompleted: false })
            {
                // Keep live input open while Escape unwinds all ten real live loops;
                // continue/Exit then closes the actual practice menus. Cancellation
                // wakes a blocked ReadKey only; it is not a polling-loop cancellation.
                var settling = DateTime.UtcNow.AddSeconds(10);
                var cancelledRead = false;
                while (!running.IsCompleted && DateTime.UtcNow < settling)
                {
                    Drive(teardown: true);
                    if (!cancelledRead && DateTime.UtcNow >= settling.AddSeconds(-1))
                    { input!.CancelPendingReads("owned practice teardown: unblock pending ReadKey"); cancelledRead = true; }
                    await Task.Delay(15);
                }
            }
            var settled = running == null || running.IsCompleted;
            output.WriteLine(JsonSerializer.Serialize(new { PracticeOwnedTaskSettledBeforeCleanup = settled,
                TaskStatus = running?.Status.ToString(), Root = root, RootRetained = !settled, Inputs = inputs }));
            if (!settled)
                throw new InvalidOperationException("Owned console practice task remains unsettled; input/root retained for qualification failure, never reported as cleaned.");
            input?.Shutdown("owned practice task fully settled before cleanup");
            input?.Dispose();
            cleanup.Dispose();
        }
    }
}
