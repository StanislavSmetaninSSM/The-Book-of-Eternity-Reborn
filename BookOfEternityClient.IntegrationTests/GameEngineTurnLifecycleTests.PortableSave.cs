using System.IO.Compression;
using BookOfEternityClient.AgentConsole;
using BookOfEternityClient.Core;
using BookOfEternityClient.Models;
using BookOfEternityClient.Services;
using BookOfEternityClient.UI;
using Microsoft.Extensions.Logging.Abstractions;
using Spectre.Console;
using Xunit;

namespace BookOfEternityClient.Tests;

public sealed partial class GameEngineTurnLifecycleTests
{
    /// <summary>
    /// Exercises the actual console save menu without discarding a committed cleanup warning.
    /// </summary>
    /// <param name="committed">
    /// Cuts after durable commit when true, or after archive publication to force known rollback otherwise.
    /// </param>
    /// <returns>
    /// A task completing after the archive decision and player-visible message agree.
    /// </returns>
    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task PortableManualSave_ConsoleMenuReportsActualDecision(bool committed)
    {
        FileSystemManager? files = null;
        var reached = 0;
        files = new FileSystemManager(_rootPath, NullLogger<FileSystemManager>.Instance,
            PhysicalLoadTransactionOperations.Instance, new FileSystemManagerHooks
            {
                LocalPublicationObserver = (phase, index) =>
                {
                    if (committed ? phase != TrustedLocalPublicationPhase.Committed :
                        phase != TrustedLocalPublicationPhase.MemberPublished || index != 0) return;
                    reached++;
                    throw new IOException("synthetic manual save publication cut");
                }
            });
        PortableSaveFixture.Seed(files);
        var engine = CreateGameEngine(new QueuedConsoleInputSource(
            Enumerable.Repeat(Key(ConsoleKey.Enter), 4)), fileSystem: files);
        var original = AnsiConsole.Console;
        using var output = new StringWriter();
        AnsiConsole.Console = AnsiConsole.Create(new AnsiConsoleSettings
        {
            Ansi = AnsiSupport.No,
            ColorSystem = ColorSystemSupport.NoColors,
            Out = new AnsiConsoleOutput(output)
        });
        try
        {
            Assert.True(await InvokePrivateAsync<bool>(engine, "InGameOptionsMenu"));
        }
        finally { AnsiConsole.Console = original; }

        Assert.Equal(1, reached);
        var localization = GetPrivateField<LocalizationManager>(engine, "_loc");
        Assert.Contains(localization.T(committed ? "save_success" : "save_failed"), output.ToString());
        if (committed)
        {
            Assert.Contains("провер", output.ToString(), StringComparison.OrdinalIgnoreCase);
            Assert.DoesNotContain(localization.T("save_failed"), output.ToString());
            using var archive = ZipFile.OpenRead(Assert.Single(Directory.GetFiles(files.ResolvePath("saves/manual_saves"), "*.zip")));
            Assert.NotNull(archive.GetEntry("save_manifest.json"));
        }
        else Assert.Empty(Directory.GetFiles(files.ResolvePath("saves/manual_saves"), "*.zip"));
    }

    /// <summary>
    /// Keeps an actual committed save visible in the agent console when unresolved storage blocks continuation.
    /// </summary>
    /// <returns>
    /// A task completing after the public save's typed outcome receives a truthful private diagnostic screen.
    /// </returns>
    [Fact]
    public async Task PortableManualSave_CommittedDebtObservationDoesNotPromiseReplay()
    {
        FileSystemManager? files = null;
        var reached = 0;
        files = new FileSystemManager(_rootPath, NullLogger<FileSystemManager>.Instance,
            PhysicalLoadTransactionOperations.Instance, new FileSystemManagerHooks
            {
                LocalPublicationObserver = (phase, _) =>
                {
                    if (phase != TrustedLocalPublicationPhase.Committed) return;
                    reached++;
                    File.WriteAllBytes(Assert.Single(Directory.GetFiles(files!.ResolvePath("saves/manual_saves"), "*.zip")), [81, 0, 254]);
                    throw new IOException("/private/synthetic-save-diagnostic");
                }
            });
        var state = PortableSaveFixture.Seed(files);
        var save = new SaveLoadService(files, state, NullLogger<SaveLoadService>.Instance);
        var error = await Assert.ThrowsAsync<CommittedSaveContinuationException>(() => save.SaveGameAsync("committed", "exact decision"));
        var store = new AgentConsoleStateStore();
        using var input = new AgentConsoleLiveInputSource(store, readTimeout: TimeSpan.FromSeconds(5));
        var engine = CreateGameEngine(input, fileSystem: files);

        InvokePrivate(engine, "RecordGameLoopErrorObservation", error);

        var snapshot = Assert.IsType<AgentConsoleSnapshot>(store.GetSnapshot());
        Assert.Equal(1, reached);
        Assert.True(error.Result.Committed);
        Assert.True(error.Result.ContinuationBlocked);
        Assert.Contains("Сохранение создано", snapshot.PlainText);
        Assert.DoesNotContain("не было применено", snapshot.PlainText, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("повторите", snapshot.PlainText, StringComparison.OrdinalIgnoreCase);
        AssertPlayerAgentConsoleSnapshotIsPrivate(snapshot, store.GetEvents(),
            "/private/synthetic-save-diagnostic", nameof(CommittedSaveContinuationException));
    }

    /// <summary>
    /// Drives each real accepted-turn autosave caller and stops before terminal cleanup or accepted-state compensation.
    /// </summary>
    /// <param name="entry">
    /// Selects a newly dispatched player turn, a waiting turn, or a late terminal continuation.
    /// </param>
    /// <param name="committed">
    /// Introduces unknown archive bytes after durable commit when true, or before commit otherwise.
    /// </param>
    /// <returns>
    /// A task completing after the actual save publication cut and retained accepted state are verified.
    /// </returns>
    [Theory]
    [InlineData("player", false)]
    [InlineData("waiting", false)]
    [InlineData("late", false)]
    [InlineData("player", true)]
    [InlineData("waiting", true)]
    [InlineData("late", true)]
    public async Task PortableAutosave_RealTurnCallerRetainsAcceptedStateAndTerminalEvidence(string entry, bool committed)
    {
        CopyDirectory(TestRepoPaths.BaseSessionRoot, _fs.GameSessionPath);
        var reached = 0;
        byte[]? acceptedSoul = null;
        byte[]? acceptedStory = null;
        FileSystemManager? files = null;
        files = new FileSystemManager(_rootPath, NullLogger<FileSystemManager>.Instance,
            PhysicalLoadTransactionOperations.Instance, new FileSystemManagerHooks
            {
                LocalPublicationObserver = (phase, index) =>
                {
                    if (committed ? phase != TrustedLocalPublicationPhase.Committed :
                        phase != TrustedLocalPublicationPhase.MemberPublished || index != 0) return;
                    var archives = Directory.GetFiles(files!.ResolvePath("saves/autosaves"), "*.zip");
                    if (archives.Length != 1) return; // Only the actual save decision, after all earlier accepted work.
                    reached++;
                    acceptedSoul = File.ReadAllBytes(files.ResolvePath("game_state/meta/soul_state.json"));
                    acceptedStory = File.ReadAllBytes(files.ResolvePath("stories/chaos_sea.jsonl"));
                    File.WriteAllBytes(archives[0], [83, 0, 255]);
                    throw new IOException("synthetic autosave decision conflict");
                }
            });
        var engine = CreateGameEngine(new QueuedConsoleInputSource([]), settings => settings.AutosaveIntervalTurns = 1,
            fileSystem: files);
        await InvokePrivateTaskAsync(engine, "RefreshRuntimeStateAsync");
        await InvokePrivateTaskAsync(engine, "EnsureClientOwnedSystemFilesHealthyAsync");
        var gameLoop = GetPrivateField<GameLoop>(engine, "_gameLoop");
        gameLoop.SetSession("portable-save-engine-session", 0);
        Task operation;
        if (entry == "player")
        {
            operation = InvokePrivateTaskAsync(engine, "ProcessPlayerTurn", "Синтетический принятый ход перед сохранением.", null);
            var request = await WaitForTurnRequestAsync();
            await WritePortableSaveEngineReplyAsync(request);
        }
        else
        {
            var request = new TurnRequest
            {
                SessionId = gameLoop.SessionId,
                RequestId = "portable-save-engine-request",
                TurnNumber = 1,
                PlayerAction = "Синтетический принятый ход перед сохранением.",
                Timestamp = DateTime.UtcNow.ToString("O")
            };
            request.ProgressionControl = await new ProgressionScheduleService(files,
                NullLogger<ProgressionScheduleService>.Instance).BuildControlForNextTurnAsync();
            var backup = await InvokePrivateTaskResultAsync(engine, "CreatePreTurnBackup", "portable-save-engine");
            await InvokePrivateTaskResultAsync(engine, "CreateCanonicalBaselineSnapshotAsync", request, backup, "portable-save-engine");
            await WriteJsonAsync("input/turn_request.json", request);
            await WritePortableSaveEngineReplyAsync(request);
            operation = InvokePrivateTaskAsync(engine, entry == "waiting" ? "WaitForGmResponse" :
                "ProcessLateTerminalAndIdleTransitionsForCurrentSessionAsync");
        }

        var error = await Record.ExceptionAsync(() => operation.WaitAsync(TimeSpan.FromMinutes(2)));

        Assert.Equal(1, reached); // Failure before save preparation does not satisfy this test.
        if (committed) Assert.IsType<CommittedSaveContinuationException>(error);
        else Assert.IsType<CoordinatedStatePublicationUncertainException>(error);
        Assert.Equal(1, gameLoop.TurnNumber);
        Assert.NotNull(acceptedSoul);
        Assert.NotNull(acceptedStory);
        Assert.Equal(acceptedSoul, File.ReadAllBytes(files.ResolvePath("game_state/meta/soul_state.json")));
        Assert.Equal(acceptedStory, File.ReadAllBytes(files.ResolvePath("stories/chaos_sea.jsonl")));
        Assert.Contains("Синтетический ответ принятого хода", System.Text.Encoding.UTF8.GetString(acceptedStory!));
        foreach (var path in new[] { "input/turn_request.json", "ready/turn_complete.json",
            "game_state/control/pending_turn_snapshot.json", "game_state/control/pending_turn_snapshot.authority.json" })
            Assert.True(File.Exists(files.ResolvePath(path)), $"Save continuation must preserve {path}.");
        Assert.True(File.Exists(Path.Combine(files.RuntimeRootPath, "trusted-local-publication-v1/active.json")));
        Assert.Equal(new byte[] { 83, 0, 255 }, File.ReadAllBytes(Assert.Single(Directory.GetFiles(files.ResolvePath("saves/autosaves"), "*.zip"))));
    }

    /// <summary>
    /// Authors a minimal correlated, actor-free GM reply in the independently owned test session.
    /// </summary>
    /// <param name="request">
    /// The actual engine request whose correlation is used by the terminal signal.
    /// </param>
    /// <returns>
    /// A task completing once all output files and the final signal are present.
    /// </returns>
    private async Task WritePortableSaveEngineReplyAsync(TurnRequest request)
    {
        var timestamp = DateTime.UtcNow.ToString("O");
        await WriteJsonAsync("output/narrative_response.json", new { response = "Синтетический ответ принятого хода.", timestamp });
        await WriteJsonAsync("output/interface_updates.json", new
        {
            dialogueOptions = new[] { new { text = "Продолжить.", category = "neutral" } }, timestamp
        });
        await WriteJsonAsync("output/debug_logs.json", new
        {
            timestamp,
            gm_thoughts_markdown = "## NPC Scope\n- Mode: Scene-local\n- Relevant actors: нет\n- Why relevant: Синтетический ход без акторов.\n- Actors outside scope: нет\n- Why outside scope: Структурные акторы не меняются.\n\n## Reasoning\n- Verify accepted state survives save uncertainty."
        });
        await WriteJsonAsync("ready/turn_complete.json", new
        {
            sessionId = request.SessionId, requestId = request.RequestId, turnNumber = request.TurnNumber,
            timestamp, status = "success", filesModified = new[]
            {
                "output/narrative_response.json", "output/interface_updates.json", "output/debug_logs.json"
            }
        });
    }
}
