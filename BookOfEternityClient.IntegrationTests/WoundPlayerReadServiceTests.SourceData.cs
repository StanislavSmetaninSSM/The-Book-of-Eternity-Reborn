using BookOfEternityClient.Core;
using BookOfEternityClient.Services;
using Xunit;

namespace BookOfEternityClient.Tests;

public sealed partial class WoundPlayerReadServiceTests
{
    /// <summary>Measures actual physical source reads, then proves another opening observes changed and invalid DATA.</summary>
    [Fact]
    public async Task Load_OneOpeningReadsSharedCarriersOnceAndNeverRetainsThemAcrossOpenings()
    {
        var reads = new CanonicalReadActivityAccumulator();
        await using var fixture = await WoundPlayerReadFixture.CreateAsync(output, reads: reads);
        var paths = new[] { WoundCarrierCatalog.PlayerPath, WoundCarrierCatalog.NpcPath,
            WoundCarrierCatalog.EnemiesPath, WoundCarrierCatalog.AlliesPath, WoundCarrierCatalog.AfterlifeProfilesPath,
            WoundIdentityState.StatePath, WoundHistoryState.HistoryPath, ResourceMaterializationContract.DefinitionsPath };
        var counts = paths.Select(reads.ReadCount).ToArray();
        var before = fixture.Snapshot();
        var first = await WoundPlayerReadService.LoadAsync(fixture.Files);
        Assert.True(first.IsAvailable);
        Assert.Single(first.Active);
        Assert.Equal(before, fixture.Snapshot());
        Assert.All(paths.Select((path, index) => reads.ReadCount(path) - counts[index]), count => Assert.Equal(1L, count));

        var wound = WoundContractTestData.CreateActiveWound();
        wound["display"]!["name"] = "Новое имя после отдельного открытия";
        var changed = WoundPlayerProjectionTestData.Input(wound);
        await using (var lease = await fixture.Files.AcquireCanonicalWriteLeaseAsync())
        {
            await fixture.Files.WriteFileAtomicAsync(lease, WoundCarrierCatalog.PlayerPath, changed.Carriers.PlayerWounds!.ToJsonString());
            await fixture.Files.WriteFileAtomicAsync(lease, WoundIdentityState.StatePath, WoundIdentityState.SerializeCanonical(changed.Identities));
            await fixture.Files.WriteFileAtomicAsync(lease, WoundHistoryState.HistoryPath, WoundHistoryState.SerializeCanonical(changed.History));
        }
        counts = paths.Select(reads.ReadCount).ToArray();
        before = fixture.Snapshot();
        var second = await WoundPlayerReadService.LoadAsync(fixture.Files);
        Assert.True(second.IsAvailable);
        Assert.Equal("Новое имя после отдельного открытия", Assert.Single(second.Active).Name);
        Assert.Equal(before, fixture.Snapshot());
        Assert.All(paths.Select((path, index) => reads.ReadCount(path) - counts[index]), count => Assert.Equal(1L, count));

        await using (var lease = await fixture.Files.AcquireCanonicalWriteLeaseAsync())
            await fixture.Files.WriteFileAtomicAsync(lease, WoundCarrierCatalog.PlayerPath, "{");
        before = fixture.Snapshot();
        var malformed = await WoundPlayerReadService.LoadAsync(fixture.Files);
        Assert.False(malformed.IsAvailable);
        Assert.Empty(malformed.Active);
        Assert.Empty(malformed.History);
        Assert.Equal(WoundPlayerProjection.UnavailableMessage, malformed.StatusMessage);
        Assert.Equal(before, fixture.Snapshot());
    }
}
