using BookOfEternityClient.Core;
using BookOfEternityClient.Services.GmWorkers;
using Microsoft.Extensions.Logging.Abstractions;
using System.Text.Json.Nodes;
using Xunit;
using Xunit.Abstractions;

namespace BookOfEternityClient.Tests;

public sealed class GmWorkerProposalInboxTests(ITestOutputHelper output)
{
    [Fact]
    public async Task ListAsync_ReturnsReadableProposalSummariesInStableNewestOrder()
    {
        var root = CreateTempRoot();
        try
        {
            var fs = CreateFileSystem(root);
            await WriteTaskAsync(fs, GmWorkerBridgeTestFixtures.ValidationRepairTask());
            await WriteTaskAsync(fs, GmWorkerBridgeTestFixtures.NarrativeDraftTask());
            await GmWorkerBridgeTestFixtures.WriteProposalFixtureAsync(
                fs,
                GmWorkerBridgeTestFixtures.ValidationRepairProposal());
            await GmWorkerBridgeTestFixtures.WriteProposalFixtureAsync(
                fs,
                GmWorkerBridgeTestFixtures.NarrativeDraftProposal());
            var inbox = new GmWorkerProposalInboxService(fs);

            var entries = await inbox.ListAsync();

            Assert.Collection(
                entries,
                first =>
                {
                    Assert.True(first.IsReadable);
                    Assert.Equal("worker_proposal_20260620_0002", first.ProposalId);
                    Assert.Equal(WorkerTaskType.NarrativeDraft, first.TaskType);
                    Assert.Equal("review-only", first.ReviewMode);
                    Assert.True(first.HasDraftText);
                    Assert.Equal(0, first.ChangedFileCount);
                    Assert.Equal(1, first.FindingCount);
                },
                second =>
                {
                    Assert.True(second.IsReadable);
                    Assert.Equal("worker_proposal_20260620_0001", second.ProposalId);
                    Assert.Equal(WorkerTaskType.ValidationRepair, second.TaskType);
                    Assert.Equal("apply-gate", second.ReviewMode);
                    Assert.False(second.HasDraftText);
                    Assert.Equal(1, second.ChangedFileCount);
                    Assert.Equal("Added the missing normalized weather description.", second.Summary);
                });
        }
        finally
        {
            CleanupTempRoot(root);
        }
    }

    [Fact]
    public async Task ListAsync_MalformedProposalFile_ReturnsUnreadableEntry()
    {
        var root = CreateTempRoot();
        try
        {
            var fs = CreateFileSystem(root);
            await fs.WriteFileAtomicAsync("worker_proposals/broken_proposal/proposal.json", "{ not valid json");
            var inbox = new GmWorkerProposalInboxService(fs);

            var entry = Assert.Single(await inbox.ListAsync());

            Assert.Equal("broken_proposal", entry.ProposalId);
            Assert.False(entry.IsReadable);
            Assert.Contains("malformed", entry.UnreadableReason, StringComparison.OrdinalIgnoreCase);
            Assert.Equal("unreadable", entry.ReviewMode);
        }
        finally
        {
            CleanupTempRoot(root);
        }
    }

    [Fact]
    public async Task ListAsync_ProposalWithoutStatus_ReturnsUnreadableEntry()
    {
        var root = CreateTempRoot();
        try
        {
            var fs = CreateFileSystem(root);
            var proposal = JsonNode.Parse(GmWorkerJson.Serialize(
                GmWorkerBridgeTestFixtures.ValidationRepairProposal()))!.AsObject();
            proposal.Remove("status");
            await fs.WriteFileAtomicAsync(
                "worker_proposals/proposal_without_status/proposal.json",
                proposal.ToJsonString());
            var inbox = new GmWorkerProposalInboxService(fs);

            var entry = Assert.Single(await inbox.ListAsync());

            Assert.False(entry.IsReadable);
            Assert.Contains("status", entry.UnreadableReason, StringComparison.OrdinalIgnoreCase);
            Assert.Equal("unreadable", entry.ReviewMode);
        }
        finally
        {
            CleanupTempRoot(root);
        }
    }

    [Fact]
    public async Task ReadAsync_JoinsProposalWithRelatedAuditEventsAndApplyState()
    {
        var root = CreateTempRoot();
        try
        {
            var fs = CreateFileSystem(root);
            var task = GmWorkerBridgeTestFixtures.ValidationRepairTask();
            var proposal = GmWorkerBridgeTestFixtures.ValidationRepairProposal();
            await WriteTaskAsync(fs, task);
            await GmWorkerBridgeTestFixtures.WriteProposalFixtureAsync(fs, proposal);
            var audit = new GmWorkerAuditLog(fs);
            await audit.RecordProposalReceivedAsync(proposal);
            await audit.RecordApplyDecisionAsync(proposal, new ApplyGateDecision
            {
                DecisionId = "apply_decision_20260620_0001",
                ProposalId = proposal.ProposalId,
                Result = ApplyGateResult.Accepted,
                AppliedFiles = ["game_state/world/weather.json"],
                DecidedAtUtc = "2026-06-20T00:00:30Z"
            });
            var inbox = new GmWorkerProposalInboxService(fs);

            var entry = await inbox.ReadAsync(proposal.ProposalId);

            Assert.NotNull(entry);
            Assert.Equal("applied", entry!.ApplyState);
            Assert.Contains("proposal-received", entry.RelatedAuditEventTypes);
            Assert.Contains("proposal-applied", entry.RelatedAuditEventTypes);
            Assert.Contains("game_state/world/weather.json", entry.ChangedFiles);
            Assert.Contains(entry.RelatedAuditSummaries, summary => summary.Contains("proposal-applied", StringComparison.Ordinal));
        }
        finally
        {
            CleanupTempRoot(root);
        }
    }

    [Fact]
    public async Task ReadAsync_ReturnsNullForMissingProposal()
    {
        var root = CreateTempRoot();
        try
        {
            var fs = CreateFileSystem(root);
            var inbox = new GmWorkerProposalInboxService(fs);

            Assert.Null(await inbox.ReadAsync("missing_proposal"));
        }
        finally
        {
            CleanupTempRoot(root);
        }
    }

    [Fact]
    public async Task ReadAsync_WaitsForAtomicProposalDirectoryPublicationBeforeClassifyingChild()
    {
        var root = CreateTempRoot();
        try
        {
            var paused = 0;
            var mainContentions = 0;
            var canonicalContentions = 0;
            var writerAtBoundary = new TaskCompletionSource(
                TaskCreationOptions.RunContinuationsAsynchronously);
            var allowPublication = new TaskCompletionSource(
                TaskCreationOptions.RunContinuationsAsynchronously);
            var readerContended = new TaskCompletionSource(
                TaskCreationOptions.RunContinuationsAsynchronously);
            var proposal = GmWorkerBridgeTestFixtures.ValidationRepairProposal();
            var destination =
                $"{GmWorkerProposalStore.ProposalRoot}/{proposal.ProposalId}";
            var hooks = new FileSystemManagerHooks
            {
                MainOwnerLockContendedAsync = () =>
                {
                    Interlocked.Increment(ref mainContentions);
                    readerContended.TrySetResult();
                    return Task.CompletedTask;
                },
                CanonicalWriteLockContendedAsync = () =>
                {
                    Interlocked.Increment(ref canonicalContentions);
                    return Task.CompletedTask;
                },
                AfterCanonicalMutationBoundaryValidatedAsync = async path =>
                {
                    if (!path.Equals(destination, StringComparison.OrdinalIgnoreCase))
                        return;

                    Interlocked.Increment(ref paused);
                    writerAtBoundary.TrySetResult();
                    await allowPublication.Task.WaitAsync(TimeSpan.FromSeconds(10));
                }
            };
            var fs = new FileSystemManager(
                root,
                NullLogger<FileSystemManager>.Instance,
                PhysicalLoadTransactionOperations.Instance,
                hooks);
            fs.EnsureDirectoryStructure();
            var stagingDirectory = fs.CreateRuntimeProposalStagingRoot();
            await File.WriteAllTextAsync(
                Path.Combine(stagingDirectory, "proposal.json"),
                GmWorkerJson.Serialize(proposal));
            var inbox = new GmWorkerProposalInboxService(fs);
            Task<GmWorkerProposalInboxEntry?>? readerTask = null;
            Task? publicationTask = null;

            try
            {
                await using (var writeLease = await fs.AcquireCanonicalWriteLeaseAsync())
                {
                    try
                    {
                        publicationTask =
                            fs.MoveRuntimeDirectoryIntoCanonicalSessionAsync(
                                writeLease,
                                stagingDirectory,
                                destination);
                        var reached = await Task.WhenAny(writerAtBoundary.Task, publicationTask)
                            .WaitAsync(TimeSpan.FromSeconds(10));
                        if (ReferenceEquals(reached, publicationTask)) await publicationTask;
                        Assert.Same(writerAtBoundary.Task, reached);

                        using (ExecutionContext.SuppressFlow())
                        {
                            readerTask = Task.Run(
                                () => inbox.ReadAsync(proposal.ProposalId));
                        }

                        var first = await Task.WhenAny(
                                readerTask,
                                readerContended.Task)
                            .WaitAsync(TimeSpan.FromSeconds(10));
                        Assert.Same(readerContended.Task, first);
                        Assert.Equal(1, paused);
                        Assert.True(mainContentions > 0);
                        Assert.Equal(0, canonicalContentions);
                        Assert.False(readerTask.IsCompleted);
                        allowPublication.TrySetResult();
                        await publicationTask;
                    }
                    finally
                    {
                        allowPublication.TrySetResult();
                        if (publicationTask is not null)
                            await Record.ExceptionAsync(() => publicationTask);
                    }
                }

                var entry = await readerTask!;
                Assert.NotNull(entry);
                Assert.Equal(proposal.ProposalId, entry!.ProposalId);
            }
            finally
            {
                allowPublication.TrySetResult();
                if (readerTask is not null)
                    await Record.ExceptionAsync(() => readerTask);
                output.WriteLine(System.Text.Json.JsonSerializer.Serialize(new
                {
                    kind = "f18-native-runtime-contention", method = nameof(ReadAsync_WaitsForAtomicProposalDirectoryPublicationBeforeClassifyingChild), root = root,
                    paused, mainContentions, canonicalContentions,
                    publicationStarted = publicationTask is not null, publicationSettled = publicationTask?.IsCompleted,
                    readerStarted = readerTask is not null, readerSettled = readerTask?.IsCompleted
                }));
            }
        }
        finally
        {
            CleanupTempRoot(root);
            output.WriteLine(System.Text.Json.JsonSerializer.Serialize(new
            { CleanupOwnedRoot = root, OwnedFixtureRemoved = !Directory.Exists(root) }));
            Assert.False(Directory.Exists(root));
        }
    }

    private static Task WriteTaskAsync(FileSystemManager fs, WorkerTaskPacket task) =>
        fs.WriteFileAtomicAsync(GmWorkerBridgePool.GetTaskPacketPath(task.TaskId), GmWorkerJson.Serialize(task));

    private static FileSystemManager CreateFileSystem(string root)
    {
        var fs = new FileSystemManager(root, NullLogger<FileSystemManager>.Instance);
        fs.EnsureDirectoryStructure();
        return fs;
    }

    private static string CreateTempRoot()
    {
        var root = Path.Combine(Path.GetTempPath(), "boe-gm-worker-inbox-" + Guid.NewGuid().ToString("N"));
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
