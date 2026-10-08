using BookOfEternityClient.AgentConsole;
using BookOfEternityClient.Services;
using Xunit;

namespace BookOfEternityClient.Tests;

public sealed partial class GameEngineTurnLifecycleTests
{
    [Fact]
    public void CoordinatedPublication_UncertainOutcomeUsesRussianRecoveryNoticeWithoutRetryPromise()
    {
        const string privateDetail = "/private/game_state/secret-coordinated.json";
        var error = new CoordinatedStatePublicationUncertainException(new IOException(privateDetail));
        var store = new AgentConsoleStateStore();
        using var input = new AgentConsoleLiveInputSource(store, readTimeout: TimeSpan.FromSeconds(5));
        var engine = CreateGameEngine(input);

        InvokePrivate(engine, "RecordGameLoopErrorObservation", error);

        var snapshot = Assert.IsType<AgentConsoleSnapshot>(store.GetSnapshot());
        Assert.Equal("world-turn-paused", snapshot.ScreenId);
        Assert.Equal(AgentConsoleMode.Error, snapshot.Mode);
        AssertPlayerAgentConsoleSnapshotIsPrivate(snapshot, store.GetEvents(), privateDetail,
            nameof(CoordinatedStatePublicationUncertainException));
        Assert.Contains("сохран", snapshot.PlainText, StringComparison.OrdinalIgnoreCase);
        Assert.Contains("восстанов", snapshot.PlainText, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("не было применено", snapshot.PlainText, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("повторите", snapshot.PlainText, StringComparison.OrdinalIgnoreCase);
        Assert.Contains("восстанов", error.Message, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain(privateDetail, error.Message, StringComparison.Ordinal);
    }
}
