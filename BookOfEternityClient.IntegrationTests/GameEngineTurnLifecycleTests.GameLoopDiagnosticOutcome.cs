using System.Text;
using System.Text.Json;
using System.Text.Json.Nodes;
using BookOfEternityClient.AgentConsole;
using BookOfEternityClient.Core;
using BookOfEternityClient.Services;
using Spectre.Console;
using Xunit;

namespace BookOfEternityClient.Tests;

public sealed partial class GameEngineTurnLifecycleTests
{
    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task GameLoopUnknown_OriginalLoopPresentsDecisionWithoutCanonicalDiagnosticContinuation(bool diagnostic)
    {
        using var owned = new CleanupOwnedFixture(_rootPath, line => _directGachaOutput?.WriteLine(line));
        Assert.True(OperatingSystem.IsLinux());
        using var probe = new LoreRealmProbe(_rootPath);
        var files = probe.Files;
        var soul = CreateLifecycleSoulState("Game loop outcome", "Chaos Sea");
        var shining = ShiningAbodeState.CreateDefaultState();
        shining["availability"] = ShiningAbodeState.AvailabilityActive;
        shining["lightSparks"] = 88;
        await CanonicalResourceQuartetTestFixture.CommitFreshBootstrapAsync(files, new AfterlifeOwnerResourceAcceptedState(
            Profiles: CreatePlayerSoulProfiles("Game loop outcome", "Chaos Sea"),
            SpiritualConflict: AfterlifeSpiritualConflictState.CreateDefaultRoot(), SoulState: soul,
            ShiningAbode: shining, Guardians: CreateEmptyGuardiansState()));
        foreach (var path in new[] { "ready/turn_complete.json", "ready/turn_error.json", "input/turn_request.json",
            "game_state/control/life_transitions.json", "game_state/control/incarnation_trigger.json", "game_state/control/ascension.json" }) files.DeleteFile(path);
        var store = new AgentConsoleStateStore();
        var known = new InvalidOperationException("controlled input failure /private/game-loop-source.json");
        GameEngine? engine = null;
        AgentConsoleLiveInputSource? live = null;
        var textReads = 0;
        var errorKeys = 0;
        var unexpectedReads = 0;
        AgentConsoleSnapshot? errorSnapshot = null;
        var inputHooks = new AgentConsoleLiveInputSourceHooks
        {
            ActiveReadRegistered = kind =>
            {
                var snapshot = store.GetSnapshot();
                if (kind == AgentConsoleInputKind.Key && snapshot?.Mode == AgentConsoleMode.Error)
                {
                    errorSnapshot = snapshot;
                    errorKeys++;
                    SetPrivateField(engine!, "_inGame", false);
                    Assert.True(live!.EnqueueKey(Key(ConsoleKey.Enter)).Accepted);
                    return;
                }
                if (kind == AgentConsoleInputKind.Text && textReads++ == 0)
                {
                    probe.Cut.Armed = true; // Idle preparation has completed at the original input boundary.
                    if (diagnostic) throw known;
                    Assert.True(live!.EnqueueLine("/reenter_shining_abode").Accepted);
                    return;
                }
                unexpectedReads++;
                SetPrivateField(engine!, "_inGame", false);
                throw new InvalidOperationException("fixture refuses a second command or unexpected input");
            }
        };
        using var input = live = new AgentConsoleLiveInputSource(store, TimeSpan.FromSeconds(5),
            AgentConsoleLiveInputSource.DefaultMaxQueueLength, inputHooks);
        engine = CreateGameEngine(input, InertRealmSettings, fileSystem: files);
        await GetPrivateField<StateManager>(engine, "_stateManager").RefreshGameStateAsync();
        Assert.True(GetPrivateField<StateManager>(engine, "_stateManager").CurrentState.IsInChaosSea);
        var generation = await GetOrCreateSessionGenerationAsync();
        var errorLogBefore = CleanupPublicationCut.ReadOptional(files.ResolvePath("error_log.txt"));
        probe.Cut.Select = (path, member) => diagnostic
            ? path == files.ResolvePath("error_log.txt") && member.GetProperty("After").GetProperty("Exists").GetBoolean()
            : probe.IsFirstRealmMember(path);
        probe.Cut.BeforeCut = () =>
        {
            probe.AssertGeneration(generation);
            if (diagnostic)
            {
                var published = File.ReadAllText(files.ResolvePath("error_log.txt"));
                Assert.Contains(known.GetType().Name, published, StringComparison.Ordinal);
                Assert.Contains(known.Message, published, StringComparison.Ordinal);
            }
        };
        using var console = new LoadMenuAnsiConsole();
        var originalConsole = AnsiConsole.Console;
        Exception? failure;
        var joined = false;
        try
        {
            AnsiConsole.Console = console;
            failure = await Record.ExceptionAsync(() => InvokePrivateTaskAsync(engine, "EnterGameLoop"));
            joined = true;
        }
        finally { AnsiConsole.Console = originalConsole; }
        _directGachaOutput?.WriteLine(JsonSerializer.Serialize(new { diagnostic, generation, textReads, errorKeys, unexpectedReads,
            ConsoleReads = console.Reads, OriginalLoopJoined = joined, Failure = failure?.ToString(), errorSnapshot,
            KnownCauseRetained = probe.Cut.RetainsDiagnostic(known), probe.RequestAttempts, errorLogBefore,
            ErrorLogAfter = CleanupPublicationCut.ReadOptional(files.ResolvePath("error_log.txt")), Cut = probe.Cut.Evidence() }));
        probe.Cut.AssertReachedAndStopped();
        Assert.Null(failure); Assert.True(joined); Assert.Equal(1, textReads); Assert.Equal(1, errorKeys); Assert.Equal(0, unexpectedReads);
        Assert.Equal(diagnostic ? 0 : 1, console.Reads); Assert.Equal(0, probe.RequestAttempts);
        Assert.NotNull(errorSnapshot); Assert.Equal("world-turn-paused", errorSnapshot!.ScreenId);
        Assert.Contains(CoordinatedStatePublicationUncertainException.PlayerMessage, errorSnapshot.PlainText, StringComparison.Ordinal);
        Assert.DoesNotContain("повторите", errorSnapshot.PlainText, StringComparison.OrdinalIgnoreCase);
        AssertPlayerAgentConsoleSnapshotIsPrivate(errorSnapshot, store.GetEvents(), known.Message,
            nameof(CoordinatedStatePublicationUncertainException), _rootPath);
        if (diagnostic) Assert.True(probe.Cut.RetainsDiagnostic(known));
        else Assert.Equal(errorLogBefore, CleanupPublicationCut.ReadOptional(files.ResolvePath("error_log.txt")));
    }

    [Fact]
    public async Task GameLoopDiagnosticUnknown_OriginalInvalidLifeEndRetainsSignalAndCause()
    {
        using var owned = new CleanupOwnedFixture(_rootPath, line => _directGachaOutput?.WriteLine(line));
        Assert.True(OperatingSystem.IsLinux());
        using var probe = new LoreRealmProbe(_rootPath);
        var files = probe.Files;
        const string signal = "game_state/control/life_transitions.json";
        await files.WriteFileAtomicAsync(signal, "{\"reason\":\"Death\",\"summary\":\"retained invalid transition\"}");
        var before = File.ReadAllBytes(files.ResolvePath(signal));
        var known = new GameEngine.TriggerLifeEndRuntimeContextException("controlled invalid life authority /private/life-end-source.json");
        probe.Cut.Select = (path, member) => path == files.ResolvePath("error_log.txt") && member.GetProperty("After").GetProperty("Exists").GetBoolean();
        probe.Cut.BeforeCut = () => Assert.Contains(known.Message, File.ReadAllText(files.ResolvePath("error_log.txt")), StringComparison.Ordinal);
        probe.Cut.Armed = true;
        var failure = Record.Exception(() => GameEngine.HandleInvalidTriggerLifeEndRuntimeFailure(files, known));
        var after = File.ReadAllBytes(files.ResolvePath(signal));
        _directGachaOutput?.WriteLine(JsonSerializer.Serialize(new { Failure = failure?.ToString(), before, after,
            KnownCauseRetained = probe.Cut.RetainsDiagnostic(known), probe.RequestAttempts, Cut = probe.Cut.Evidence() }));
        probe.Cut.AssertReachedAndStopped(); Assert.Same(probe.Cut.OriginalUncertainty, failure);
        Assert.True(probe.Cut.RetainsDiagnostic(known)); Assert.Equal(before, after); Assert.Equal(0, probe.RequestAttempts);
    }
}
