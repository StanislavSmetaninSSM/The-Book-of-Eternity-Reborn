using BookOfEternityClient.Core;
using BookOfEternityClient.Services.GmWorkers;
using Microsoft.Extensions.Logging.Abstractions;
using Xunit;

namespace BookOfEternityClient.Tests;

public sealed class GmWorkerProposalStoreTests
{
    [Fact]
    public void ProposalStore_DoesNotExposeGenerationUnboundSaveApi()
    {
        Assert.DoesNotContain(
            typeof(GmWorkerProposalStore).GetMethods(),
            method => method.IsPublic && method.Name == "SaveProposalAsync");
    }

    [Fact]
    public async Task ReadProposalAsync_ReadsDurableProposalFixture()
    {
        var root = CreateTempRoot();
        try
        {
            var fs = CreateFileSystem(root);
            var store = new GmWorkerProposalStore(fs);
            var proposal = GmWorkerBridgeTestFixtures.NarrativeDraftProposal();
            var savedPath = GmWorkerProposalStore.GetProposalPath(proposal.ProposalId);

            await GmWorkerBridgeTestFixtures.WriteProposalFixtureAsync(fs, proposal);
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
            string sessionGeneration;
            await using (var writeLease = await fs.AcquireCanonicalWriteLeaseAsync())
            {
                sessionGeneration = fs.GetOrCreateSessionGeneration(writeLease);
                await fs.WriteFileAtomicBytesAsync(writeLease, taskPath, taskBytes);
            }
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
                sessionGeneration,
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

    [Fact]
    public async Task PublishBundleAsync_ReservedInboxProposalIdIsRejectedWithoutNamespaceMutation()
    {
        var root = CreateTempRoot();
        try
        {
            var fs = CreateFileSystem(root);
            var proposal = GmWorkerBridgeTestFixtures.NarrativeDraftProposal() with
            {
                ProposalId = "inbox"
            };
            var taskPath = GmWorkerBridgePool.GetTaskPacketPath(proposal.TaskId);
            var taskBytes = System.Text.Encoding.UTF8.GetBytes("task-generation");
            string sessionGeneration;
            await using (var writeLease = await fs.AcquireCanonicalWriteLeaseAsync())
            {
                sessionGeneration = fs.GetOrCreateSessionGeneration(writeLease);
                await fs.WriteFileAtomicBytesAsync(writeLease, taskPath, taskBytes);
            }

            var result = await new GmWorkerProposalStore(fs).PublishBundleAsync(
                proposal,
                System.Text.Encoding.UTF8.GetBytes(GmWorkerJson.Serialize(proposal)),
                new Dictionary<string, byte[]>(),
                taskPath,
                taskBytes,
                sessionGeneration,
                GmWorkerBridgePool.GetProposalInboxPath(proposal.TaskId));

            Assert.False(result.Published);
            Assert.Contains("reserved", result.Error!, StringComparison.OrdinalIgnoreCase);
            Assert.False(fs.FileExists("worker_proposals/inbox/proposal.json"));
        }
        finally
        {
            CleanupTempRoot(root);
        }
    }

    [Fact]
    public async Task PublishBundleAsync_CancellationWhileWaitingForCanonicalLeasePublishesNothing()
    {
        var root = CreateTempRoot();
        try
        {
            var fs = CreateFileSystem(root);
            var proposal = GmWorkerBridgeTestFixtures.NarrativeDraftProposal() with
            {
                ProposalId = "worker_proposal_canceled_while_waiting"
            };
            var taskPath = GmWorkerBridgePool.GetTaskPacketPath(proposal.TaskId);
            var taskBytes = System.Text.Encoding.UTF8.GetBytes("task-generation");
            string sessionGeneration;
            await using (var setupLease = await fs.AcquireCanonicalWriteLeaseAsync())
            {
                sessionGeneration = fs.GetOrCreateSessionGeneration(setupLease);
                await fs.WriteFileAtomicBytesAsync(setupLease, taskPath, taskBytes);
            }

            var contentionReached = new TaskCompletionSource(
                TaskCreationOptions.RunContinuationsAsynchronously);
            var publishingFs = new FileSystemManager(
                root,
                NullLogger<FileSystemManager>.Instance,
                PhysicalLoadTransactionOperations.Instance,
                new FileSystemManagerHooks
                {
                    CanonicalWriteLockContendedAsync = () =>
                    {
                        contentionReached.TrySetResult();
                        return Task.CompletedTask;
                    }
                });
            var inboxPath = GmWorkerBridgePool.GetProposalInboxPath(proposal.TaskId);
            using var cancellation = new CancellationTokenSource();
            await using var blockingLease = await fs.AcquireCanonicalWriteLeaseAsync();

            var publicationTask = new GmWorkerProposalStore(publishingFs).PublishBundleAsync(
                proposal,
                System.Text.Encoding.UTF8.GetBytes(GmWorkerJson.Serialize(proposal)),
                new Dictionary<string, byte[]>(),
                taskPath,
                taskBytes,
                sessionGeneration,
                inboxPath,
                cancellationToken: cancellation.Token);
            await contentionReached.Task.WaitAsync(TimeSpan.FromSeconds(10));
            cancellation.Cancel();

            await Assert.ThrowsAnyAsync<OperationCanceledException>(
                () => publicationTask.WaitAsync(TimeSpan.FromSeconds(10)));
            Assert.False(Directory.Exists(fs.ResolvePath(
                $"{GmWorkerProposalStore.ProposalRoot}/{proposal.ProposalId}")));
            Assert.False(fs.FileExists(inboxPath));
        }
        finally
        {
            CleanupTempRoot(root);
        }
    }

    [Fact]
    public async Task PublishBundleAsync_CancellationAfterDurableTransitionDoesNotRevokeBundle()
    {
        var root = CreateTempRoot();
        var releaseInbox = new TaskCompletionSource(
            TaskCreationOptions.RunContinuationsAsynchronously);
        try
        {
            var fs = CreateFileSystem(root);
            var proposal = GmWorkerBridgeTestFixtures.NarrativeDraftProposal() with
            {
                ProposalId = "worker_proposal_publication_wins"
            };
            var taskPath = GmWorkerBridgePool.GetTaskPacketPath(proposal.TaskId);
            var taskBytes = System.Text.Encoding.UTF8.GetBytes("task-generation");
            string sessionGeneration;
            await using (var setupLease = await fs.AcquireCanonicalWriteLeaseAsync())
            {
                sessionGeneration = fs.GetOrCreateSessionGeneration(setupLease);
                await fs.WriteFileAtomicBytesAsync(setupLease, taskPath, taskBytes);
            }

            var durableTransitionReached = new TaskCompletionSource(
                TaskCreationOptions.RunContinuationsAsynchronously);
            var store = new GmWorkerProposalStore(
                fs,
                async (lease, path, content) =>
                {
                    durableTransitionReached.TrySetResult();
                    await releaseInbox.Task;
                    await fs.WriteFileAtomicBytesAsync(lease, path, content);
                });
            using var cancellation = new CancellationTokenSource();

            var publicationTask = store.PublishBundleAsync(
                proposal,
                System.Text.Encoding.UTF8.GetBytes(GmWorkerJson.Serialize(proposal)),
                new Dictionary<string, byte[]>(),
                taskPath,
                taskBytes,
                sessionGeneration,
                GmWorkerBridgePool.GetProposalInboxPath(proposal.TaskId),
                cancellationToken: cancellation.Token);
            await durableTransitionReached.Task.WaitAsync(TimeSpan.FromSeconds(10));
            cancellation.Cancel();
            releaseInbox.TrySetResult();

            var result = await publicationTask.WaitAsync(TimeSpan.FromSeconds(10));
            Assert.True(result.Published);
            Assert.NotNull(await store.ReadProposalAsync(proposal.ProposalId));
        }
        finally
        {
            releaseInbox.TrySetResult();
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
