using System.Text.Json;
using BookOfEternityClient.Core;
using BookOfEternityClient.Services;
using Xunit;
using Xunit.Abstractions;

namespace BookOfEternityClient.Tests;

public sealed partial class DarenStandaloneLinuxBoundaryTests(ITestOutputHelper output)
{
    private static readonly byte[] Before = [0xEF, 0xBB, 0xBF, 0, 0xFF, 41];
    private static readonly byte[] After = [0xD0, 0x94, 0, 0xFF, 42];

    [Theory]
    [InlineData(false, -1)] [InlineData(true, -1)]
    [InlineData(false, (int)TrustedLocalPublicationPhase.IntentStaged)] [InlineData(true, (int)TrustedLocalPublicationPhase.IntentStaged)]
    [InlineData(false, (int)TrustedLocalPublicationPhase.IntentPublished)] [InlineData(true, (int)TrustedLocalPublicationPhase.IntentPublished)]
    [InlineData(false, (int)TrustedLocalPublicationPhase.MemberStaged)] [InlineData(true, (int)TrustedLocalPublicationPhase.MemberStaged)]
    [InlineData(false, (int)TrustedLocalPublicationPhase.MemberPublished)] [InlineData(true, (int)TrustedLocalPublicationPhase.MemberPublished)]
    [InlineData(false, (int)TrustedLocalPublicationPhase.CommitStaged)] [InlineData(true, (int)TrustedLocalPublicationPhase.CommitStaged)]
    public async Task ActualProfilePublicationCut_ConfirmsExactRollbackOrOriginalAbsence(bool existed, int phase)
    {
        using var fixture = new DarenStandaloneLinuxFixture(output);
        await fixture.InitializeAsync();
        if (existed) { Directory.CreateDirectory(Path.GetDirectoryName(fixture.ProfilePath)!); File.WriteAllBytes(fixture.ProfilePath, Before); }
        await using var lease = await fixture.Files.AcquireCanonicalWriteLeaseAsync();
        var cut = new InvalidOperationException("actual Daren publication cut " + phase);
        var reached = false;
        if (phase == -1) fixture.Mutation = path =>
        {
            if (path == "@daren_reward_profile") { reached = true; throw cut; }
            return Task.CompletedTask;
        };
        else fixture.Publication = (at, _) =>
        {
            if ((int)at != phase) return;
            reached = true; fixture.Publication = null; throw cut;
        };
        var failure = await Assert.ThrowsAsync<InvalidOperationException>(() => new DarenRewardProfileFileStore(fixture.Files).WriteExactBytesAtomicAsync(lease, After));
        Assert.Same(cut, failure); Assert.True(reached);
        Assert.Equal(existed ? Before : null, await QteSceneService.ReadDarenProfileRollbackBytesAsync(fixture.Files, lease));
        Assert.Equal(existed, File.Exists(fixture.ProfilePath));
        fixture.AssertPublisherClean();
        output.WriteLine("Original rollback exception retained; phase=" + phase + " existed=" + existed);
    }

    [Theory]
    [InlineData((int)TrustedLocalPublicationPhase.Committed)]
    [InlineData((int)TrustedLocalPublicationPhase.CleanupMember)]
    public async Task RealCompletionCommittedDebt_FreshOriginalRecoveryKeepsReward(int phase)
    {
        using var fixture = new DarenStandaloneLinuxFixture(output);
        await fixture.InitializeAsync();
        var reached = false;
        fixture.Publication = (at, _) =>
        {
            if ((int)at != phase) return;
            reached = true; fixture.Publication = null;
            Assert.True(File.Exists(fixture.ActiveJournal));
            throw new InvalidOperationException("actual Daren committed cleanup cut");
        };
        var completed = new DateTime(2026, 6, 11, 5, 0, 0, DateTimeKind.Utc);
        var result = await fixture.Profile.RecordCompletionAsync(DarenQteRewardProfileService.ResolveEnding(true, 90), completed);
        Assert.True(reached); Assert.True(result.Updated); Assert.Equal("perfect_shadow", result.Profile.DarenShowcase!.BestTierId);
        Assert.True(File.Exists(fixture.ActiveJournal));
        var committedBytes = File.ReadAllBytes(fixture.ProfilePath);
        var fresh = new DarenQteRewardProfileService(fixture.Fresh());
        var read = await fresh.ReadProfileAsync();
        Assert.Equal("perfect_shadow", read.DarenShowcase!.BestTierId); Assert.Equal(6, read.DarenShowcase.InkFeatherBonus);
        Assert.Equal(completed, read.DarenShowcase.CompletedAtUtc);
        Assert.Equal(committedBytes, File.ReadAllBytes(fixture.ProfilePath));
        fixture.AssertPublisherClean();
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task ActualPostIntentAuthorityLoss_RetainsUncertainAndRefusesFreshRecovery(bool generationLoss)
    {
        using var fixture = new DarenStandaloneLinuxFixture(output);
        await fixture.InitializeAsync();
        var reached = false;
        fixture.Publication = (phase, _) =>
        {
            if (phase != TrustedLocalPublicationPhase.MemberPublished) return;
            reached = true; fixture.Publication = null;
            if (generationLoss) File.WriteAllBytes(fixture.Files.SessionGenerationPath,
                JsonSerializer.SerializeToUtf8Bytes(new { schemaVersion = 1, generationId = Guid.NewGuid().ToString("N") }));
            else File.WriteAllBytes(fixture.ProfilePath, [7, 7, 7]);
            throw new InvalidOperationException("controlled original profile authority-loss cut");
        };
        await using (var lease = await fixture.Files.AcquireCanonicalWriteLeaseAsync())
        {
            var failure = await Assert.ThrowsAsync<InvalidDataException>(() => new DarenRewardProfileFileStore(fixture.Files).WriteExactBytesAtomicAsync(lease, After));
            Assert.Contains("uncertain", failure.Message, StringComparison.OrdinalIgnoreCase);
        }
        Assert.True(reached); Assert.True(File.Exists(fixture.ActiveJournal));
        Assert.Equal(generationLoss ? After : new byte[] { 7, 7, 7 }, File.ReadAllBytes(fixture.ProfilePath));
        var retained = Directory.GetFiles(fixture.JournalRoot, "*", SearchOption.AllDirectories)
            .Concat(new[] { fixture.ProfilePath, fixture.Files.SessionGenerationPath })
            .ToDictionary(path => path, File.ReadAllBytes);
        Assert.NotNull(await Record.ExceptionAsync(() => fixture.Fresh().AcquireCanonicalWriteLeaseAsync()));
        foreach (var evidence in retained) Assert.Equal(evidence.Value, File.ReadAllBytes(evidence.Key));
        output.WriteLine("Logical Uncertain retained before owned fixture cleanup; no completion/replay/recovery grant.");
    }
}
