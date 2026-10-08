using BookOfEternityClient.Core;
using BookOfEternityClient.Services;
using Microsoft.Extensions.Logging.Abstractions;
using Xunit;

namespace BookOfEternityClient.Tests;

public sealed partial class DarenStandaloneLinuxBoundaryTests
{
    [Fact]
    public async Task PublicConsoleTerminalWriteFailure_DoesNotReportCompletionOrAward()
    {
        using var fixture = new DarenStandaloneLinuxFixture(output);
        await fixture.InitializeAsync();
        var qte = new QteSceneService(fixture.Files, fixture.State.Settings,
            new CharacteristicsService(fixture.Files, fixture.State, NullLogger<CharacteristicsService>.Instance),
            null!, null!, null!, null!, null!, fixture.State, NullLogger<QteSceneService>.Instance);
        var attempt = qte.StartDarenShowcaseAttempt();
        var cut = new InvalidOperationException("actual public console terminal profile failure");
        fixture.Mutation = path => path == "@daren_reward_profile" ? Task.FromException(cut) : Task.CompletedTask;
        var actions = 0;
        var failure = await Record.ExceptionAsync(async () =>
        {
            while (attempt.State == "Active" && actions++ < 64)
            {
                var scene = attempt.ActiveScene!;
                var chapter = Assert.Single(scene.Offer!.Chapters.Where(item => item.ChapterId == scene.CurrentChapterId));
                await qte.ResolveDarenShowcaseActionAsync(attempt, Assert.Single(chapter.Actions).ActionId, "success");
            }
        });
        Assert.Same(cut, failure); Assert.InRange(actions, 1, 64);
        Assert.Equal("Active", attempt.State); Assert.Null(attempt.LastCompletion); Assert.Null(attempt.Ending);
        Assert.False(File.Exists(fixture.ProfilePath)); Assert.Equal(1, fixture.ProfileMutationBoundaries);
        fixture.AssertPublisherClean();
        output.WriteLine("Actual public console terminal cut; actions=" + actions + "; no command retry.");
    }

    [Fact]
    public async Task OriginalCancellationAtProfileBoundary_DoesNotPublish()
    {
        using var fixture = new DarenStandaloneLinuxFixture(output);
        await fixture.InitializeAsync();
        await using var lease = await fixture.Files.AcquireCanonicalWriteLeaseAsync();
        var generation = fixture.Files.ReadExistingSessionGeneration(lease);
        using var cancel = new CancellationTokenSource();
        fixture.Mutation = path => { if (path == "@daren_reward_profile") cancel.Cancel(); return Task.CompletedTask; };
        await Assert.ThrowsAnyAsync<OperationCanceledException>(() => new DarenRewardProfileFileStore(fixture.Files).WriteExactBytesAtomicAsync(lease, After, cancel.Token));
        Assert.True(cancel.IsCancellationRequested); Assert.Equal(1, fixture.ProfileMutationBoundaries);
        Assert.Equal(generation, fixture.Files.ReadExistingSessionGeneration(lease));
        Assert.False(File.Exists(fixture.ProfilePath)); fixture.AssertPublisherClean();
    }
}
