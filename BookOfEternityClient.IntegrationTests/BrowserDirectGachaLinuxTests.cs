using BookOfEternityClient.CommandProtocol;
using BookOfEternityClient.Services;
using BookOfEternityClient.WebUi;
using Xunit;
using Xunit.Abstractions;

namespace BookOfEternityClient.Tests;

public sealed class BrowserDirectGachaLinuxTests(ITestOutputHelper output)
{
    [Fact]
    public async Task RealBrowserPull_PublishesExactPreSpendBackupAndQueuedTurn()
    {
        using var fixture = new BrowserDirectGachaLinuxFixture(output);
        await fixture.InitializeAsync();
        var result = await fixture.PullAsync();
        output.WriteLine(result.Message);
        Assert.True(result.Success, result.Message);
        Assert.Equal(CommandExecutionState.Completed, result.State);
        Assert.Equal(fixture.BeforeSoul, File.ReadAllBytes(fixture.BackupPath()));
        Assert.Equal(11, fixture.Feathers());
        Assert.Equal("Rare", result.Payload!["gachaBaseResult"]!["baseRarity"]!.GetValue<string>());
        Assert.Equal(7, result.Payload["spentInkFeathers"]!.GetValue<int>());
        Assert.Contains("[CHAOS_SEA_DIRECT_GACHA]", result.Payload["gmAction"]!.GetValue<string>());
        var manifest = fixture.Manifest();
        var relativeBackup = manifest["rollbackBackups"]![BrowserDirectGachaLinuxFixture.Soul]!.GetValue<string>();
        Assert.Equal(fixture.Files.ResolvePath(relativeBackup), fixture.BackupPath());
        Assert.True(File.Exists(fixture.Files.ResolvePath(BrowserPendingTurnInspector.TurnRequestPath)));
        Assert.True(File.Exists(fixture.Files.ResolvePath(PendingTurnSnapshotAuthority.AuthorityPath)));
        Assert.Equal(fixture.BeforeHistory, File.ReadAllBytes(fixture.Files.ResolvePath("game_state/history/chat_log.json")));
        Assert.False(File.Exists(fixture.Files.ResolvePath(LocalUiSessionLockService.LockPath)));
        Assert.Empty(Directory.GetFiles(fixture.Files.ResolvePath(ExplorerLocalTurnRollbackArtifacts.Root), "browser_write_manifest.json", SearchOption.AllDirectories));
    }
}
