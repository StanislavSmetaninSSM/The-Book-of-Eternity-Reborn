using BookOfEternityClient.Core;
using BookOfEternityClient.Services;
using BookOfEternityClient.WebUi;
using Microsoft.Extensions.Logging.Abstractions;
using Xunit;

namespace BookOfEternityClient.Tests;

public sealed partial class BrowserRollbackLinuxBoundaryTests
{
    [Theory]
    [InlineData(false, false)]
    [InlineData(true, false)]
    [InlineData(false, true)]
    [InlineData(true, true)]
    public async Task DeclaredActualProfileStore_CommitsOrRestoresExactBaseline(bool existed, bool fail)
    {
        var (files, coordinator) = await CreateAsync();
        var path = Path.Combine(_root, DarenQteRewardProfileService.ProfileRelativePath);
        if (existed) { Directory.CreateDirectory(Path.GetDirectoryName(path)!); File.WriteAllBytes(path, Before); }
        var store = new DarenRewardProfileFileStore(files);
        var ran = false;
        var result = await coordinator.ExecuteAtomicAsync(Request, [Member], async lease =>
        {
            ran = true;
            store.EnsureWriteSupported(lease);
            Assert.Equal(existed ? Before : null, await store.ReadExactBytesAsync(lease));
            await store.WriteExactBytesAtomicAsync(lease, After);
            Assert.Equal(After, await store.ReadExactBytesAsync(lease));
            await files.WriteFileAtomicBytesAsync(lease, Member, After);
            if (fail) throw new InvalidOperationException("declared profile callback cut");
        }, rollbackExternalFileIds: [ExplorerLocalTurnRollbackArtifacts.DarenRewardProfileExternalFileId]);
        output.WriteLine(result.Message);
        Assert.True(ran, result.Message);
        Assert.Equal(!fail, result.Success);
        Assert.Equal(fail ? Before : After, File.ReadAllBytes(files.ResolvePath(Member)));
        if (!fail || existed) Assert.Equal(fail ? Before : After, File.ReadAllBytes(path));
        else Assert.False(File.Exists(path));
        AssertNoBrowserEvidence(files);
    }

    [Fact]
    public async Task ActualQteWebRewardResolution_UsesDeclaredPortableParticipant()
    {
        var (files, coordinator) = await CreateAsync();
        var state = PortableSaveFixture.Seed(files); // Synthetic ordinary game bytes only; no real save.
        var characteristics = new CharacteristicsService(files, state, NullLogger<CharacteristicsService>.Instance);
        var qte = new QteSceneService(files, state.Settings, characteristics, null!, null!, null!, null!, null!,
            state, NullLogger<QteSceneService>.Instance);
        var web = new QteWebInteractionService(files, qte, coordinator);
        var intro = await web.BuildDarenShowcaseStateAsync();
        var current = await web.StartDarenShowcaseAsync(Assert.IsType<string>(intro.InteractionToken));
        var actions = 0;
        while (current.State == "Active" && actions++ < 64)
        {
            var action = Assert.Single(current.ActiveScene!.CurrentChapter!.Actions);
            current = await web.ResolveDarenShowcaseActionAsync(new(action.ActionId, "perfect", Assert.IsType<string>(current.InteractionToken)));
            Assert.True(string.IsNullOrWhiteSpace(current.Error), current.Error);
        }
        Assert.Equal("Completed", current.State);
        Assert.InRange(actions, 1, 64);
        Assert.NotNull(current.Ending); Assert.NotNull(current.BestReward);
        var refreshed = await web.BuildDarenShowcaseStateAsync();
        Assert.NotNull(refreshed.BestReward); Assert.True(string.IsNullOrWhiteSpace(refreshed.Error), refreshed.Error);
        Assert.True(File.Exists(Path.Combine(_root, DarenQteRewardProfileService.ProfileRelativePath)));
        Assert.Equal(Before, File.ReadAllBytes(files.ResolvePath(Member)));
        AssertNoBrowserEvidence(files);
        output.WriteLine("Actual QTE action count=" + actions);
    }
}
