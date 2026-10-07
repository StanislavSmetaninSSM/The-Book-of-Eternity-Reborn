using System.Text.Json;
using BookOfEternityClient.Core;
using BookOfEternityClient.Services;
using Microsoft.Extensions.Logging.Abstractions;
using Xunit;

namespace BookOfEternityClient.Tests;

public sealed partial class DarenStandaloneLinuxTests
{
    [Fact]
    public async Task PublicConsoleQte_CompletesOriginalStandaloneRouteWithoutBootstrap()
    {
        using var fixture = new DarenStandaloneLinuxFixture(output);
        await fixture.InitializeAsync(bootstrap: false);
        var characteristics = new CharacteristicsService(fixture.Files, fixture.State, NullLogger<CharacteristicsService>.Instance);
        var qte = new QteSceneService(fixture.Files, fixture.State.Settings, characteristics,
            null!, null!, null!, null!, null!, fixture.State, NullLogger<QteSceneService>.Instance);
        var attempt = qte.StartDarenShowcaseAttempt();
        var actions = 0;
        while (attempt.State == "Active" && actions++ < 64)
        {
            var scene = attempt.ActiveScene!;
            var chapter = Assert.Single(scene.Offer!.Chapters.Where(item => item.ChapterId == scene.CurrentChapterId));
            var action = Assert.Single(chapter.Actions);
            var result = await qte.ResolveDarenShowcaseActionAsync(attempt, action.ActionId, "success",
                new DateTime(2026, 6, 11, 4, 0, 0, DateTimeKind.Utc));
            Assert.Equal(attempt.State, result.State);
        }
        Assert.Equal("Completed", attempt.State);
        Assert.InRange(actions, 1, 64);
        Assert.NotNull(attempt.LastCompletion);
        var fresh = await new DarenQteRewardProfileService(fixture.Fresh()).ReadProfileAsync();
        Assert.Equal(attempt.Ending!.TierId, fresh.DarenShowcase!.BestTierId);
        Assert.Equal(attempt.Ending.InkFeatherBonus, fresh.DarenShowcase.InkFeatherBonus);
        Assert.Equal(DarenQteRewardProfileService.Source, fresh.DarenShowcase.Source);
        fixture.AssertPublisherClean();
        output.WriteLine("Actual public console QTE actions=" + actions);
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task ExactStoreAndOriginalQteRollback_RestoreBytesOrOriginalAbsence(bool existed)
    {
        using var fixture = new DarenStandaloneLinuxFixture(output);
        await fixture.InitializeAsync();
        byte[] before = [0xEF, 0xBB, 0xBF, 0, 0xFF, 41];
        byte[] after = [0xD0, 0x94, 0, 0xFF, 42];
        if (existed) { Directory.CreateDirectory(Path.GetDirectoryName(fixture.ProfilePath)!); File.WriteAllBytes(fixture.ProfilePath, before); }
        await using var lease = await fixture.Files.AcquireCanonicalWriteLeaseAsync();
        var store = new DarenRewardProfileFileStore(fixture.Files);
        store.EnsureWriteSupported(lease);
        var original = await QteSceneService.ReadDarenProfileRollbackBytesAsync(fixture.Files, lease);
        Assert.Equal(existed ? before : null, original);
        await store.WriteExactBytesAtomicAsync(lease, after);
        Assert.Equal(after, await store.ReadExactBytesAsync(lease));
        await store.WriteExactBytesAtomicAsync(lease, [9, 8, 0, 0xFF]);
        await QteSceneService.RestoreDarenProfileRollbackBytesAsync(fixture.Files, lease, original);
        Assert.Equal(original, await QteSceneService.ReadDarenProfileRollbackBytesAsync(fixture.Files, lease));
        Assert.Equal(existed, File.Exists(fixture.ProfilePath));
        fixture.AssertPublisherClean();
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task ExistingProfileRead_NormalizesThroughRealStore(bool corrupt)
    {
        using var fixture = new DarenStandaloneLinuxFixture(output);
        await fixture.InitializeAsync();
        Directory.CreateDirectory(Path.GetDirectoryName(fixture.ProfilePath)!);
        var raw = corrupt ? "{bad JSON" : JsonSerializer.Serialize(new DarenRewardProfileState
        {
            SchemaVersion = 99,
            DarenShowcase = new DarenRewardRecord { BestTierId = "perfect_shadow", BestTierName = "fixture",
                BestScore = 999, InkFeatherBonus = 999, Source = "fixture" }
        }, new JsonSerializerOptions { PropertyNamingPolicy = JsonNamingPolicy.CamelCase });
        File.WriteAllText(fixture.ProfilePath, raw);
        var result = await fixture.Profile.ReadProfileAsync();
        Assert.Equal(DarenQteRewardProfileService.SchemaVersion, result.SchemaVersion);
        if (corrupt) Assert.Null(result.DarenShowcase);
        else
        {
            Assert.Equal("perfect_shadow", result.DarenShowcase!.BestTierId);
            Assert.Equal(6, result.DarenShowcase.InkFeatherBonus);
            Assert.Equal(DarenQteRewardProfileService.Source, result.DarenShowcase.Source);
        }
        Assert.NotEqual(raw, File.ReadAllText(fixture.ProfilePath));
        Assert.Equal(result.DarenShowcase?.BestTierId, (await new DarenQteRewardProfileService(fixture.Fresh()).ReadProfileAsync()).DarenShowcase?.BestTierId);
        fixture.AssertPublisherClean();
    }

    [Fact]
    public async Task SupportedCheckAbsentRestoreAndPreCancel_HaveNoPreparationEffects()
    {
        using var fixture = new DarenStandaloneLinuxFixture(output);
        await fixture.InitializeAsync(bootstrap: false);
        await using var lease = await fixture.Files.AcquireCanonicalWriteLeaseAsync();
        var store = new DarenRewardProfileFileStore(fixture.Files);
        var entries = Directory.GetFileSystemEntries(fixture.Root, "*", SearchOption.AllDirectories).Order().ToArray();
        Assert.Null(fixture.Files.ReadExistingSessionGeneration(lease));
        store.EnsureWriteSupported(lease);
        await QteSceneService.RestoreDarenProfileRollbackBytesAsync(fixture.Files, lease, null);
        using var cancelled = new CancellationTokenSource(); cancelled.Cancel();
        await Assert.ThrowsAnyAsync<OperationCanceledException>(() => store.WriteExactBytesAtomicAsync(lease, [1], cancelled.Token));
        Assert.Null(fixture.Files.ReadExistingSessionGeneration(lease));
        Assert.Equal(entries, Directory.GetFileSystemEntries(fixture.Root, "*", SearchOption.AllDirectories).Order().ToArray());
        Assert.False(Directory.Exists(Path.GetDirectoryName(fixture.ProfilePath)));
        Assert.False(Directory.Exists(fixture.JournalRoot));
        Assert.Equal(0, fixture.ProfileMutationBoundaries);
    }
}
