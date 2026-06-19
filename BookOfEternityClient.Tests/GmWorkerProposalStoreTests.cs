using BookOfEternityClient.Core;
using BookOfEternityClient.Services.GmWorkers;
using Microsoft.Extensions.Logging.Abstractions;
using Xunit;

namespace BookOfEternityClient.Tests;

public sealed class GmWorkerProposalStoreTests
{
    [Fact]
    public async Task SaveAndReadProposalAsync_PersistsProposalInWorkerInbox()
    {
        var root = CreateTempRoot();
        try
        {
            var fs = CreateFileSystem(root);
            var store = new GmWorkerProposalStore(fs);
            var proposal = GmWorkerBridgeTestFixtures.NarrativeDraftProposal();

            var savedPath = await store.SaveProposalAsync(proposal);
            var roundTrip = await store.ReadProposalAsync(proposal.ProposalId);

            Assert.Equal("worker_proposals/worker_proposal_20260620_0002/proposal.json", savedPath);
            Assert.NotNull(roundTrip);
            Assert.Equal(proposal.ProposalId, roundTrip!.ProposalId);
            Assert.Equal(proposal.DraftText, roundTrip.DraftText);
            Assert.True(fs.FileExists(savedPath));
        }
        finally
        {
            CleanupTempRoot(root);
        }
    }

    private static FileSystemManager CreateFileSystem(string root)
    {
        var fs = new FileSystemManager(root, NullLogger<FileSystemManager>.Instance);
        fs.EnsureDirectoryStructure();
        return fs;
    }

    private static string CreateTempRoot()
    {
        var root = Path.Combine(Path.GetTempPath(), "boe-gm-worker-store-" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(root);
        return root;
    }

    private static void CleanupTempRoot(string root)
    {
        try
        {
            if (Directory.Exists(root))
                Directory.Delete(root, recursive: true);
        }
        catch
        {
            // ignored
        }
    }
}
