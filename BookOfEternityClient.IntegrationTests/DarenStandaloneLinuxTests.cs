using System.Text.Json.Nodes;
using BookOfEternityClient.Services;
using Xunit;
using Xunit.Abstractions;

namespace BookOfEternityClient.Tests;

public sealed partial class DarenStandaloneLinuxTests(ITestOutputHelper output)
{
    [Fact]
    public async Task RealProfileService_CreateUpgradeFreshReadAndOriginalGrant()
    {
        using var fixture = new DarenStandaloneLinuxFixture(output);
        await fixture.InitializeAsync();
        Assert.False(Directory.Exists(Path.GetDirectoryName(fixture.ProfilePath)));
        Assert.Null((await fixture.Profile.ReadProfileAsync()).DarenShowcase);
        Assert.False(Directory.Exists(Path.GetDirectoryName(fixture.ProfilePath)));

        var first = await fixture.Profile.RecordCompletionAsync(DarenQteRewardProfileService.ResolveEnding(true, 55),
            new DateTime(2026, 6, 11, 1, 0, 0, DateTimeKind.Utc)); // Actual standalone physical consumer is the missing Linux path.
        Assert.True(first.Updated);
        Assert.Equal("broken_trail", first.Profile.DarenShowcase!.BestTierId);
        var worse = await fixture.Profile.RecordCompletionAsync(DarenQteRewardProfileService.ResolveEnding(true, 40),
            new DateTime(2026, 6, 11, 2, 0, 0, DateTimeKind.Utc));
        Assert.False(worse.Updated);
        var upgrade = await fixture.Profile.RecordCompletionAsync(DarenQteRewardProfileService.ResolveEnding(true, 90),
            new DateTime(2026, 6, 11, 3, 0, 0, DateTimeKind.Utc));
        Assert.True(upgrade.Updated);
        var fresh = new DarenQteRewardProfileService(fixture.Fresh());
        var read = await fresh.ReadProfileAsync();
        Assert.Equal("perfect_shadow", read.DarenShowcase!.BestTierId);
        Assert.Equal(90, read.DarenShowcase.BestScore);
        Assert.Equal(6, read.DarenShowcase.InkFeatherBonus);
        Assert.Equal(new DateTime(2026, 6, 11, 3, 0, 0, DateTimeKind.Utc), read.DarenShowcase.CompletedAtUtc);

        var soul = new JsonObject { ["inkFeathers"] = new JsonObject { ["current"] = 3, ["total"] = 8 } };
        Assert.True((await fresh.ApplyBestRewardToNewSoulStateAsync(soul)).Granted);
        var granted = soul.ToJsonString();
        Assert.Equal(9, soul["inkFeathers"]!["current"]!.GetValue<int>());
        Assert.Equal(14, soul["inkFeathers"]!["total"]!.GetValue<int>());
        Assert.Equal("perfect_shadow", soul["clientRewardGrants"]!["darenQteShowcase"]!["tierId"]!.GetValue<string>());
        Assert.False((await fresh.ApplyBestRewardToNewSoulStateAsync(soul)).Granted);
        Assert.Equal(granted, soul.ToJsonString());
        Assert.True(fixture.ProfileMutationBoundaries > 0);
        fixture.AssertPublisherClean();
    }
}
