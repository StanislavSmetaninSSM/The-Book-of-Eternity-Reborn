using BookOfEternityClient.AgentConsole;
using BookOfEternityClient.Core;
using BookOfEternityClient.Services;
using Xunit;

namespace BookOfEternityClient.Tests;

public sealed partial class GameEngineTurnLifecycleTests
{
    [Fact]
    public async Task OrdinaryPublication_ActualUncertainWriteProjectsRecoveryWithoutRetryPromise()
    {
        const string member = "game_state/core/ordinary_uncertain.json";
        await _fs.WriteFileAtomicBytesAsync(member, [0x41]);
        var store = new AgentConsoleStateStore();
        using var input = new AgentConsoleLiveInputSource(store, readTimeout: TimeSpan.FromSeconds(5));
        var engine = CreateGameEngine(input);
        var reached = 0;
        _consolePublicationObserver = (phase, _) =>
        {
            if (phase != TrustedLocalPublicationPhase.MemberPublished) return;
            reached++;
            File.WriteAllBytes(_fs.ResolvePath(member), [0xFF]);
            throw new InvalidOperationException("/private/ordinary-publication-cut.json");
        };
        var failure = await Record.ExceptionAsync(() => _fs.WriteFileAtomicBytesAsync(member, [0x42]));
        _consolePublicationObserver = null;
        Assert.Equal(1, reached);
        Assert.NotNull(failure);
        Assert.Equal(new byte[] { 0xFF }, File.ReadAllBytes(_fs.ResolvePath(member)));
        Assert.True(File.Exists(Path.Combine(_fs.RuntimeRootPath, "trusted-local-publication-v1", "active.json")));
        InvokePrivate(engine, "RecordGameLoopErrorObservation", failure);
        var snapshot = Assert.IsType<AgentConsoleSnapshot>(store.GetSnapshot());
        Assert.Contains(CoordinatedStatePublicationUncertainException.PlayerMessage, snapshot.PlainText, StringComparison.Ordinal);
        Assert.DoesNotContain("повторите", snapshot.PlainText, StringComparison.OrdinalIgnoreCase);
        AssertPlayerAgentConsoleSnapshotIsPrivate(snapshot, store.GetEvents(), "/private/ordinary-publication-cut.json");
    }
}
