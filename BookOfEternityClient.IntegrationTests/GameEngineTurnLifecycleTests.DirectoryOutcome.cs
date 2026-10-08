using System.Text.Json;
using BookOfEternityClient.AgentConsole;
using BookOfEternityClient.Core;
using BookOfEternityClient.Services;
using Xunit;

namespace BookOfEternityClient.Tests;

public sealed partial class GameEngineTurnLifecycleTests
{
    [Fact]
    public async Task ActualSnapshotTreeUncertaintyStopsCleanupAndProjectsRecoveryNotice()
    {
        const string tree = LiveTurnPreparationService.PendingTurnSnapshotDirectory;
        const string rollback = "game_state/core/tree_notice.rollback.fixture";
        await _fs.WriteFileAtomicBytesAsync(tree + "/a.bin", [1]);
        await _fs.WriteFileAtomicBytesAsync(tree + "/z.bin", [2]);
        await _fs.WriteFileAtomicBytesAsync(rollback, [3]);
        var store = new AgentConsoleStateStore();
        using var input = new AgentConsoleLiveInputSource(store, readTimeout: TimeSpan.FromSeconds(5));
        var engine = CreateGameEngine(input);
        var reached = 0;
        byte[]? retainedJournal = null;
        _consolePublicationObserver = (phase, index) =>
        {
            if (phase != TrustedLocalPublicationPhase.MemberPublished || index != 0) return;
            var journalPath = Path.Combine(_fs.RuntimeRootPath, "trusted-local-publication-v1", "active.json");
            using var journal = JsonDocument.Parse(File.ReadAllBytes(journalPath));
            var member = journal.RootElement.GetProperty("Members")[index];
            Assert.Equal(_fs.ResolvePath(tree + "/a.bin"), member.GetProperty("Path").GetString());
            Assert.False(member.GetProperty("After").GetProperty("Exists").GetBoolean());
            Assert.False(File.Exists(_fs.ResolvePath(tree + "/a.bin")));
            reached++;
            File.WriteAllBytes(_fs.ResolvePath(tree + "/z.bin"), [42]);
            retainedJournal = File.ReadAllBytes(journalPath);
            throw new InvalidOperationException("/private/tree-publication-cut.json");
        };
        var failure = await Record.ExceptionAsync(() => InvokePrivateTaskAsync(
            engine, "CleanupPendingTurnSnapshotAsync", new object?[] { null }));
        _consolePublicationObserver = null;
        Assert.Equal(1, reached);
        Assert.NotNull(failure);
        Assert.False(File.Exists(_fs.ResolvePath(tree + "/a.bin")));
        Assert.Equal(new byte[] { 42 }, File.ReadAllBytes(_fs.ResolvePath(tree + "/z.bin")));
        Assert.Equal(new byte[] { 3 }, File.ReadAllBytes(_fs.ResolvePath(rollback)));
        Assert.Equal(retainedJournal, File.ReadAllBytes(Path.Combine(_fs.RuntimeRootPath, "trusted-local-publication-v1", "active.json")));
        InvokePrivate(engine, "RecordGameLoopErrorObservation", failure);
        var snapshot = Assert.IsType<AgentConsoleSnapshot>(store.GetSnapshot());
        Assert.Contains(CoordinatedStatePublicationUncertainException.PlayerMessage, snapshot.PlainText, StringComparison.Ordinal);
        Assert.DoesNotContain("не было применено", snapshot.PlainText, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("повторите", snapshot.PlainText, StringComparison.OrdinalIgnoreCase);
        AssertPlayerAgentConsoleSnapshotIsPrivate(snapshot, store.GetEvents(), "/private/tree-publication-cut.json");
    }
}
