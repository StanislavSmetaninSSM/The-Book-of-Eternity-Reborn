using System.Text.Json;
using BookOfEternityClient.Core;
using Xunit;

namespace BookOfEternityClient.Tests;

public sealed partial class PortableDirectoryDeletionTests
{
    [Theory]
    [InlineData(false, false)]
    [InlineData(true, false)]
    [InlineData(false, true)]
    [InlineData(true, true)]
    public async Task SameLeaseRepeatedTreeOrAncestorResolvesDebtBeforePruning(bool ancestor, bool conflict)
    {
        const string snapshot = "game_state/control/pending_turn_snapshot";
        var decisions = 0; var cuts = 0;
        var files = Manager(new FileSystemManagerHooks
        {
            LocalPublicationObserver = (phase, _) =>
            {
                if (phase == TrustedLocalPublicationPhase.IntentPublished) decisions++;
                if (phase != TrustedLocalPublicationPhase.Committed) return;
                cuts++; throw new CutFailure();
            }
        });
        SeedGeneration(files); Seed(files, snapshot + "/nested/member", [0xFF, 0]);
        var requested = ancestor ? "game_state/control" : snapshot;
        byte[] evidence;
        Exception? secondFailure;
        await using (var lease = await files.AcquireCanonicalWriteLeaseAsync())
        {
            files.DeleteDirectoryTree(lease, snapshot);
            Assert.Equal(1, cuts); Assert.Equal(1, decisions);
            Assert.False(File.Exists(files.ResolvePath(snapshot + "/nested/member")));
            Assert.True(Directory.Exists(files.ResolvePath(snapshot + "/nested")));
            evidence = File.ReadAllBytes(Journal(files));
            if (conflict)
                File.WriteAllBytes(files.SessionGenerationPath, JsonSerializer.SerializeToUtf8Bytes(
                    new { schemaVersion = 1, generationId = Guid.NewGuid().ToString("N") }));
            secondFailure = Record.Exception(() => files.DeleteDirectoryTree(lease, requested));
        }
        // Real later admission must still be able to construct B1 scratch grants.
        var coldFailure = await Record.ExceptionAsync(async () =>
        {
            await using var recovery = await Manager().AcquireCanonicalWriteLeaseAsync();
        });
        Assert.Equal(1, decisions);
        if (conflict)
        {
            Assert.IsType<InvalidDataException>(secondFailure);
            Assert.IsType<InvalidDataException>(coldFailure);
            Assert.True(Directory.Exists(files.ResolvePath(snapshot + "/nested")));
            Assert.Equal(evidence, File.ReadAllBytes(Journal(files)));
        }
        else
        {
            Assert.Null(secondFailure);
            Assert.Null(coldFailure);
            Assert.False(Directory.Exists(files.ResolvePath(requested)));
            Assert.False(File.Exists(Journal(files)));
        }
    }
}
