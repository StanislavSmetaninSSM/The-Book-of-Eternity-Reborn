using BookOfEternityClient.Core;
using BookOfEternityClient.Services;
using BookOfEternityClient.Services.GmRuntime;
using BookOfEternityClient.WebUi;
using Xunit;

namespace BookOfEternityClient.Tests;

public sealed partial class BrowserDirectGachaLinuxBoundaryTests
{
    [Theory]
    [InlineData("runtime")]
    [InlineData("notice")]
    public async Task RealUncertainRollbackPreservesLastConfirmedRuntimeAndRecoveryNotice(string observation)
    {
        using var fixture = new BrowserDirectGachaLinuxFixture(output);
        await fixture.InitializeAsync();
        byte[] unknown = [0, 255, 41];
        var reached = 0;
        object? lastConfirmedRuntime = null;
        fixture.Mutation = path =>
        {
            if (path != BrowserPendingTurnInspector.TurnRequestPath) return Task.CompletedTask;
            reached++;
            Assert.Equal(11, fixture.State.CurrentState.InkFeathers);
            lastConfirmedRuntime = fixture.State.CurrentState;
            fixture.Mutation = null;
            File.WriteAllBytes(fixture.Files.ResolvePath(BrowserDirectGachaLinuxFixture.Soul), unknown);
            throw new InvalidOperationException("actual queue cut: game_state/meta/soul_state.json");
        };
        fixture.Closing = () => Task.FromException(new IOException("original operation close fixture"));
        var failure = await Assert.ThrowsAsync<MainOperationContinuationException<BrowserPromptWriteResult>>(() => fixture.PullAsync());
        Assert.Equal(1, reached);
        Assert.Equal(MainOperationOutcome.Uncertain, failure.EstablishedOutcome);
        Assert.False(failure.EstablishedResult.Success);
        Assert.Equal(unknown, File.ReadAllBytes(fixture.Files.ResolvePath(BrowserDirectGachaLinuxFixture.Soul)));
        Assert.NotEmpty(Directory.GetFiles(fixture.Files.ResolvePath(ExplorerLocalTurnRollbackArtifacts.Root),
            "browser_write_manifest.json", SearchOption.AllDirectories));
        if (observation == "runtime")
        {
            Assert.Same(lastConfirmedRuntime, fixture.State.CurrentState);
            Assert.Equal(11, fixture.State.CurrentState.InkFeathers);
        }
        else
        {
            Assert.DoesNotContain("состояние восстановлено", failure.EstablishedResult.Message, StringComparison.OrdinalIgnoreCase);
            Assert.DoesNotContain("повторите действие", failure.EstablishedResult.Message, StringComparison.OrdinalIgnoreCase);
            Assert.DoesNotContain(".json", failure.EstablishedResult.Message, StringComparison.OrdinalIgnoreCase);
            Assert.Contains("провер", failure.EstablishedResult.Message, StringComparison.OrdinalIgnoreCase);
        }
    }

    [Fact]
    public async Task ConfirmedDiskRollbackWithRuntimeCallbackFailureRequiresFollowUp()
    {
        using var fixture = new BrowserDirectGachaLinuxFixture(output);
        await fixture.InitializeAsync();
        var reached = 0;
        var coordinator = new BrowserLocalWriteCoordinator(fixture.Files, new LocalUiSessionLockService(fixture.Files));
        var result = await coordinator.ExecuteAtomicAsync(
            new BrowserLocalWriteRequest("outcome-fixture", "Browser", "runtime callback outcome"),
            [BrowserDirectGachaLinuxFixture.Soul],
            async lease =>
            {
                await fixture.Files.WriteFileAtomicBytesAsync(lease, BrowserDirectGachaLinuxFixture.Soul, [0x41]);
                throw new InvalidOperationException("confirmed rollback control");
            },
            prepareAfterRollback: () => () =>
            {
                reached++;
                throw new InvalidOperationException("runtime callback failure");
            });
        Assert.Equal(1, reached);
        Assert.Equal(fixture.BeforeSoul, File.ReadAllBytes(fixture.Files.ResolvePath(BrowserDirectGachaLinuxFixture.Soul)));
        Assert.Equal(BrowserPreparedWriteDisposition.RolledBack, result.Disposition);
        Assert.True(result.NeedsFollowUp);
        Assert.False(result.Success);
        Assert.False(Directory.Exists(fixture.Files.ResolvePath(ExplorerLocalTurnRollbackArtifacts.Root)));
    }
}
