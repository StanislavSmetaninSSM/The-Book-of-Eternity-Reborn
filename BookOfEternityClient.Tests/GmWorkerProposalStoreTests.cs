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

    [Fact]
    public async Task SaveProposalAsync_ConcurrentDuplicateIdAllowsExactlyOneCreate()
    {
        var root = CreateTempRoot();
        try
        {
            var fs = CreateFileSystem(root);
            var firstStore = new GmWorkerProposalStore(fs);
            var secondStore = new GmWorkerProposalStore(fs);
            var proposal = GmWorkerBridgeTestFixtures.NarrativeDraftProposal() with
            {
                ProposalId = "worker_proposal_store_race"
            };

            var attempts = await Task.WhenAll(
                TrySaveAsync(firstStore, proposal),
                TrySaveAsync(secondStore, proposal));

            Assert.Single(attempts, result => result);
            Assert.NotNull(await firstStore.ReadProposalAsync(proposal.ProposalId));
        }
        finally
        {
            CleanupTempRoot(root);
        }
    }

    [Fact]
    public async Task PublishBundleAsync_InboxFailureKeepsCompleteBundleAuthoritative()
    {
        var root = CreateTempRoot();
        try
        {
            var fs = CreateFileSystem(root);
            var proposal = GmWorkerBridgeTestFixtures.NarrativeDraftProposal() with
            {
                ProposalId = "worker_proposal_durable_without_inbox"
            };
            var taskPath = GmWorkerBridgePool.GetTaskPacketPath(proposal.TaskId);
            var taskBytes = System.Text.Encoding.UTF8.GetBytes("task-generation");
            await fs.WriteFileAtomicBytesAsync(taskPath, taskBytes);
            var store = new GmWorkerProposalStore(
                fs,
                (_, _, _) => throw new IOException("Injected derived inbox failure."));
            var proposalBytes = System.Text.Encoding.UTF8.GetBytes(GmWorkerJson.Serialize(proposal));
            var inboxPath = GmWorkerBridgePool.GetProposalInboxPath(proposal.TaskId);

            var result = await store.PublishBundleAsync(
                proposal,
                proposalBytes,
                new Dictionary<string, byte[]>(),
                taskPath,
                taskBytes,
                inboxPath);

            Assert.True(result.Published);
            Assert.Contains("inbox", result.Warning!, StringComparison.OrdinalIgnoreCase);
            Assert.NotNull(await store.ReadProposalAsync(proposal.ProposalId));
            Assert.False(fs.FileExists(inboxPath));
        }
        finally
        {
            CleanupTempRoot(root);
        }
    }

    private static async Task<bool> TrySaveAsync(GmWorkerProposalStore store, WorkerProposal proposal)
    {
        try
        {
            await store.SaveProposalAsync(proposal);
            return true;
        }
        catch (IOException)
        {
            return false;
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
