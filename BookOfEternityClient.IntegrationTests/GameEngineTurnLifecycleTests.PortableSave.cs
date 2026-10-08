using System.IO.Compression;
using System.Buffers.Binary;
using System.Text.Json;
using System.Diagnostics;
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
                    throw new InvalidDataException("synthetic manual save publication cut");
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
                    throw new InvalidDataException("/private/synthetic-save-diagnostic");
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
    /// Drives the dispatched player-turn autosave caller and retains its accepted state on blocked save continuation.
    /// </summary>
    /// <param name="committed">
    /// Introduces unknown archive bytes after durable commit when true, or before commit otherwise.
    /// </param>
    /// <returns>
    /// A task completing after the actual save publication cut and retained accepted state are verified.
    /// </returns>
    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public Task PortableAutosave_PlayerTurnRetainsAcceptedStateAndTerminalEvidence(bool committed) =>
        RunPortableAutosaveEngineContinuationAsync("player", committed);

    /// <summary>
    /// Drives the waiting-turn autosave caller and retains its accepted state on blocked save continuation.
    /// </summary>
    /// <param name="committed">
    /// Introduces unknown archive bytes after durable commit when true, or before commit otherwise.
    /// </param>
    /// <returns>
    /// A task completing after the actual save publication cut and retained accepted state are verified.
    /// </returns>
    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public Task PortableAutosave_WaitingTurnRetainsAcceptedStateAndTerminalEvidence(bool committed) =>
        RunPortableAutosaveEngineContinuationAsync("waiting", committed);

    /// <summary>
    /// Drives the late-turn autosave caller and retains its accepted state on blocked save continuation.
    /// </summary>
    /// <param name="committed">
    /// Introduces unknown archive bytes after durable commit when true, or before commit otherwise.
    /// </param>
    /// <returns>
    /// A task completing after the actual save publication cut and retained accepted state are verified.
    /// </returns>
    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public Task PortableAutosave_LateTurnRetainsAcceptedStateAndTerminalEvidence(bool committed) =>
        RunPortableAutosaveEngineContinuationAsync("late", committed);

    /// <summary>
    /// Publishes a real correlated reply and stops the selected engine continuation at its actual archive decision.
    /// </summary>
    /// <param name="entry">
    /// Selects the dispatched player, waiting, or late turn caller.
    /// </param>
    /// <param name="committed">
    /// Selects the committed conflict or uncertain pre-commit conflict boundary.
    /// </param>
    /// <returns>
    /// A task completing after accepted state and terminal evidence preservation are verified.
    /// </returns>
    private async Task RunPortableAutosaveEngineContinuationAsync(string entry, bool committed)
    {
        var timer = Stopwatch.StartNew();
        var timings = new List<string>();
        CopyDirectory(TestRepoPaths.BaseSessionRoot, _fs.GameSessionPath);
        timings.Add($"template-copy={timer.Elapsed}");
        using var soul = JsonDocument.Parse(File.ReadAllBytes(_fs.ResolvePath("game_state/meta/soul_state.json")));
        var storyPath = StoryService.GetStoryPath(soul.RootElement.GetProperty("currentRealm").GetString()!,
            soul.RootElement.GetProperty("currentIncarnation").GetInt32());
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
                    var archive = ReadPortableEngineSaveJournalTarget(files!);
                    if (archive == null) return; // Earlier accepted-state publications are outside this save decision.
                    reached++;
                    timings.Add($"save-cut={timer.Elapsed}");
                    acceptedSoul = File.ReadAllBytes(files.ResolvePath("game_state/meta/soul_state.json"));
                    acceptedStory = File.ReadAllBytes(files.ResolvePath(storyPath));
                    File.WriteAllBytes(archive, [83, 0, 255]);
                    throw new InvalidDataException("synthetic autosave decision conflict");
                }
            });
        var engine = CreateGameEngine(new QueuedConsoleInputSource([]), settings => settings.AutosaveIntervalTurns = 1,
            fileSystem: files);
        await InvokePrivateTaskAsync(engine, "RefreshRuntimeStateAsync");
        timings.Add($"runtime-refresh={timer.Elapsed}");
        await InvokePrivateTaskAsync(engine, "EnsureClientOwnedSystemFilesHealthyAsync");
        timings.Add($"client-health={timer.Elapsed}");
        var gameLoop = GetPrivateField<GameLoop>(engine, "_gameLoop");
        gameLoop.SetSession("portable-save-engine-session", 0);
        Task operation;
        if (entry == "player")
        {
            operation = InvokePrivateTaskAsync(engine, "ProcessPlayerTurn", "Синтетический принятый ход перед сохранением.", null);
            var request = await WaitForTurnRequestAsync();
            timings.Add($"player-request-staged={timer.Elapsed}");
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
            timings.Add($"backup-created={timer.Elapsed}");
            await InvokePrivateTaskResultAsync(engine, "CreateCanonicalBaselineSnapshotAsync", request, backup, "portable-save-engine");
            timings.Add($"baseline-snapshot-created={timer.Elapsed}");
            await WriteJsonAsync("input/turn_request.json", request);
            await WritePortableSaveEngineReplyAsync(request);
            operation = InvokePrivateTaskAsync(engine, entry == "waiting" ? "WaitForGmResponse" :
                "ProcessLateTerminalAndIdleTransitionsForCurrentSessionAsync");
        }

        var error = await Record.ExceptionAsync(() => operation.WaitAsync(TimeSpan.FromMinutes(2)));
        timings.Add($"operation-stopped={timer.Elapsed}");
        var diagnostic = $"Entry={entry}; committed={committed}; {string.Join("; ", timings)}\n{error}";
        Assert.True(reached == 1, $"Expected one actual save cut, reached {reached}. {diagnostic}");
        var expected = committed ? typeof(CommittedSaveContinuationException) : typeof(CoordinatedStatePublicationUncertainException);
        Assert.True(error?.GetType() == expected, $"Expected {expected}. {diagnostic}");
        Assert.Equal(1, gameLoop.TurnNumber);
        Assert.NotNull(acceptedSoul);
        Assert.NotNull(acceptedStory);
        Assert.Equal(acceptedSoul, File.ReadAllBytes(files.ResolvePath("game_state/meta/soul_state.json")));
        Assert.Equal(acceptedStory, File.ReadAllBytes(files.ResolvePath(storyPath)));
        Assert.Contains("Синтетический ответ принятого хода", System.Text.Encoding.UTF8.GetString(acceptedStory!));
        foreach (var path in new[] { "input/turn_request.json", "ready/turn_complete.json",
            "game_state/control/pending_turn_snapshot.json", "game_state/control/pending_turn_snapshot.authority.json" })
            Assert.True(File.Exists(files.ResolvePath(path)), $"Save continuation must preserve {path}.");
        Assert.True(File.Exists(Path.Combine(files.RuntimeRootPath, "trusted-local-publication-v1/active.json")));
        Assert.Equal(new byte[] { 83, 0, 255 }, File.ReadAllBytes(Assert.Single(Directory.GetFiles(files.ResolvePath("saves/autosaves"), "*.zip"))));
    }

    /// <summary>
    /// Selects only a stream publication declaring one new autosave archive, ignoring earlier engine decisions.
    /// </summary>
    /// <param name="files">
    /// The synthetic manager whose actual active journal identifies the published member set.
    /// </param>
    /// <returns>
    /// The exact declared autosave destination, or null for a different publication.
    /// </returns>
    private static string? ReadPortableEngineSaveJournalTarget(FileSystemManager files)
    {
        var journal = Path.Combine(files.RuntimeRootPath, "trusted-local-publication-v1/active.json");
        using var input = File.OpenRead(journal);
        Span<byte> prefix = stackalloc byte[16];
        input.ReadExactly(prefix);
        if (!prefix[..8].SequenceEqual("BOELP2\r\n"u8)) return null;
        var length = BinaryPrimitives.ReadInt64LittleEndian(prefix[8..]);
        Assert.InRange(length, 1, 1024 * 1024);
        var metadata = new byte[(int)length];
        input.ReadExactly(metadata);
        using var header = JsonDocument.Parse(metadata);
        var members = header.RootElement.GetProperty("Members");
        if (members.GetArrayLength() != 1) return null;
        var member = members[0];
        var path = member.GetProperty("Path").GetString()!;
        if (!string.Equals(Path.GetDirectoryName(path), files.ResolvePath("saves/autosaves"),
                OperatingSystem.IsWindows() ? StringComparison.OrdinalIgnoreCase : StringComparison.Ordinal) ||
            !Path.GetFileName(path).StartsWith("autosave_turn1_", StringComparison.Ordinal)) return null;
        Assert.False(member.GetProperty("Before").GetProperty("Exists").GetBoolean());
        Assert.True(member.GetProperty("After").GetProperty("Exists").GetBoolean());
        return path;
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
