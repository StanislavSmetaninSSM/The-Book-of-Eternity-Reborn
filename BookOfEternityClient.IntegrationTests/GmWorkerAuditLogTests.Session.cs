using BookOfEternityClient.Core;
using BookOfEternityClient.Services.GmWorkers;
using Microsoft.Extensions.Logging.Abstractions;
using System.Reflection;
using Xunit;

namespace BookOfEternityClient.Tests;

[Trait("Category", "RegressionIntegration")]
public sealed class GmWorkerAuditLogTests
{
    [Fact]
    public async Task AppendEventAsync_AppendsDurableJsonLineAuditEvents()
    {
        var root = CreateTempRoot();
        try
        {
            var fs = CreateFileSystem(root);
            var audit = new GmWorkerAuditLog(fs);
            var first = new WorkerAuditEvent
            {
                EventId = "worker_audit_20260620_0001",
                EventType = "task-dispatched",
                WorkerId = "validation_repair_codex",
                TaskId = "worker_task_20260620_0001",
                TimestampUtc = "2026-06-20T00:00:00Z",
                Summary = "Dispatched validation repair task."
            };
            var second = first with
            {
                EventId = "worker_audit_20260620_0002",
                EventType = "proposal-applied",
                ProposalId = "worker_proposal_20260620_0001",
                Summary = "Worker repair proposal accepted after validation."
            };

            await audit.AppendEventAsync(first);
            await audit.AppendEventAsync(second);
            var events = await audit.ReadEventsAsync();

            Assert.True(fs.FileExists(GmWorkerAuditLog.AuditLogPath));
            Assert.Equal(2, events.Count);
            Assert.Equal("task-dispatched", events[0].EventType);
            Assert.Equal("proposal-applied", events[1].EventType);
            Assert.Equal("worker_proposal_20260620_0001", events[1].ProposalId);
        }
        finally
        {
            CleanupTempRoot(root);
        }
    }

    [Fact]
    public async Task TypedAuditHelpers_RecordDispatchProposalAndApplyEvents()
    {
        var root = CreateTempRoot();
        try
        {
            var fs = CreateFileSystem(root);
            var audit = new GmWorkerAuditLog(fs);
            var task = GmWorkerBridgeTestFixtures.ValidationRepairTask();
            var proposal = GmWorkerBridgeTestFixtures.ValidationRepairProposal();
            var decision = new ApplyGateDecision
            {
                DecisionId = "apply_decision_20260620_0001",
                ProposalId = proposal.ProposalId,
                Result = ApplyGateResult.Accepted,
                AppliedFiles = ["game_state/world/weather.json"],
                DecidedAtUtc = "2026-06-20T00:00:30Z"
            };

            await audit.RecordTaskDispatchedAsync(task);
            await audit.RecordProposalReceivedAsync(proposal);
            await audit.RecordApplyDecisionAsync(proposal, decision);
            var events = await audit.ReadEventsAsync();

            Assert.Collection(
                events,
                first => Assert.Equal("task-dispatched", first.EventType),
                second => Assert.Equal("proposal-received", second.EventType),
                third =>
                {
                    Assert.Equal("proposal-applied", third.EventType);
                    Assert.Equal(proposal.ProposalId, third.ProposalId);
                    Assert.Contains("game_state/world/weather.json", third.Details["appliedFiles"]);
                });
        }
        finally
        {
            CleanupTempRoot(root);
        }
    }

    [Fact]
    public async Task AppendEventAsync_ConcurrentWritersPreserveEveryEvent()
    {
        var root = CreateTempRoot();
        try
        {
            var fs = CreateFileSystem(root);
            var audit = new GmWorkerAuditLog(fs);
            var writes = Enumerable.Range(0, 32)
                .Select(index => audit.AppendEventAsync(new WorkerAuditEvent
                {
                    EventId = $"worker_audit_concurrent_{index:D2}",
                    EventType = "task-dispatched",
                    WorkerId = "validation_repair_codex",
                    TaskId = $"worker_task_concurrent_{index:D2}",
                    TimestampUtc = "2026-06-20T00:00:00Z",
                    Summary = $"Concurrent event {index}."
                }))
                .ToArray();

            await Task.WhenAll(writes);
            var events = await audit.ReadEventsAsync();

            Assert.Equal(32, events.Count);
            Assert.Equal(32, events.Select(item => item.EventId).Distinct(StringComparer.Ordinal).Count());
        }
        finally
        {
            CleanupTempRoot(root);
        }
    }

    [Theory]
    [InlineData(AuditAppendEntryPoint.BestEffort)]
    [InlineData(AuditAppendEntryPoint.CurrentSession)]
    [InlineData(AuditAppendEntryPoint.RequiredOnce)]
    public async Task SelfAcquiringAppendEntryPoints_SerializeBeforeCanonicalFileLock(
        AuditAppendEntryPoint entryPoint)
    {
        var root = CreateTempRoot();
        var firstWriterAtBoundary = new TaskCompletionSource(
            TaskCreationOptions.RunContinuationsAsynchronously);
        var releaseFirstWriter = new TaskCompletionSource(
            TaskCreationOptions.RunContinuationsAsynchronously);
        var holdFirstAuditWrite = 1;
        var canonicalContentionCount = 0;
        Task? startedFirstAppend = null;
        Task? startedSecondAppend = null;
        try
        {
            var firstFs = CreateFileSystem(
                root,
                new FileSystemManagerHooks
                {
                    BeforeCanonicalMutationBoundaryAsync = path =>
                    {
                        if (path.Equals(
                                GmWorkerAuditLog.AuditLogPath,
                                StringComparison.OrdinalIgnoreCase) &&
                            Interlocked.CompareExchange(
                                ref holdFirstAuditWrite,
                                0,
                                1) == 1)
                        {
                            firstWriterAtBoundary.TrySetResult();
                            return releaseFirstWriter.Task;
                        }

                        return Task.CompletedTask;
                    }
                });
            var secondFs = CreateFileSystem(
                root,
                new FileSystemManagerHooks
                {
                    CanonicalWriteLockContendedAsync = () =>
                    {
                        Interlocked.Increment(ref canonicalContentionCount);
                        return Task.CompletedTask;
                    }
                });
            string generation;
            await using (var writeLease =
                         await firstFs.AcquireCanonicalWriteLeaseAsync())
            {
                generation = firstFs.GetOrCreateSessionGeneration(writeLease);
            }

            var firstAppend = AppendThroughEntryPointAsync(
                new GmWorkerAuditLog(firstFs),
                entryPoint,
                generation,
                CreateConcurrentAuditEvent(0));
            startedFirstAppend = firstAppend;
            await firstWriterAtBoundary.Task.WaitAsync(TimeSpan.FromSeconds(5));

            var secondAppend = AppendThroughEntryPointAsync(
                new GmWorkerAuditLog(secondFs),
                entryPoint,
                generation,
                CreateConcurrentAuditEvent(1));
            startedSecondAppend = secondAppend;
            var contentionBeforeRelease =
                Volatile.Read(ref canonicalContentionCount);

            releaseFirstWriter.TrySetResult();
            await Task.WhenAll(firstAppend, secondAppend);

            Assert.Equal(0, contentionBeforeRelease);
            var events = await new GmWorkerAuditLog(firstFs).ReadEventsAsync();
            Assert.Equal(2, events.Count);
            Assert.Equal(
                2,
                events.Select(item => item.EventId)
                    .Distinct(StringComparer.Ordinal)
                    .Count());
        }
        finally
        {
            releaseFirstWriter.TrySetResult();
            try
            {
                await AwaitStartedTasksForCleanupAsync(
                    startedFirstAppend,
                    startedSecondAppend);
            }
            finally
            {
                CleanupTempRoot(root);
            }
        }
    }

    [Theory]
    [InlineData(AuditAppendEntryPoint.CurrentSession)]
    [InlineData(AuditAppendEntryPoint.RequiredOnce)]
    public async Task CancellationAwareAppendEntryPoints_CancelWhileWaitingForAdmission(
        AuditAppendEntryPoint entryPoint)
    {
        var root = CreateTempRoot();
        var firstWriterAtBoundary = new TaskCompletionSource(
            TaskCreationOptions.RunContinuationsAsynchronously);
        var releaseFirstWriter = new TaskCompletionSource(
            TaskCreationOptions.RunContinuationsAsynchronously);
        var canonicalContentionCount = 0;
        Task? startedFirstAppend = null;
        Task? startedCanceledAppend = null;
        try
        {
            var firstFs = CreateFileSystem(
                root,
                new FileSystemManagerHooks
                {
                    BeforeCanonicalMutationBoundaryAsync = path =>
                    {
                        if (!path.Equals(
                                GmWorkerAuditLog.AuditLogPath,
                                StringComparison.OrdinalIgnoreCase))
                        {
                            return Task.CompletedTask;
                        }

                        firstWriterAtBoundary.TrySetResult();
                        return releaseFirstWriter.Task;
                    }
                });
            var secondFs = CreateFileSystem(
                root,
                new FileSystemManagerHooks
                {
                    CanonicalWriteLockContendedAsync = () =>
                    {
                        Interlocked.Increment(ref canonicalContentionCount);
                        return Task.CompletedTask;
                    }
                });
            string generation;
            await using (var writeLease =
                         await firstFs.AcquireCanonicalWriteLeaseAsync())
            {
                generation = firstFs.GetOrCreateSessionGeneration(writeLease);
            }

            var firstAppend = new GmWorkerAuditLog(firstFs).AppendEventAsync(
                CreateConcurrentAuditEvent(0));
            startedFirstAppend = firstAppend;
            await firstWriterAtBoundary.Task.WaitAsync(TimeSpan.FromSeconds(5));

            using var cancellation = new CancellationTokenSource();
            var canceledAppend = AppendThroughEntryPointAsync(
                new GmWorkerAuditLog(secondFs),
                entryPoint,
                generation,
                CreateConcurrentAuditEvent(1),
                cancellation.Token);
            startedCanceledAppend = canceledAppend;
            cancellation.Cancel();

            await Assert.ThrowsAnyAsync<OperationCanceledException>(
                () => canceledAppend);
            Assert.Equal(0, Volatile.Read(ref canonicalContentionCount));

            releaseFirstWriter.TrySetResult();
            await firstAppend;
            Assert.Single(await new GmWorkerAuditLog(firstFs).ReadEventsAsync());
        }
        finally
        {
            releaseFirstWriter.TrySetResult();
            try
            {
                await AwaitStartedTasksForCleanupAsync(
                    startedFirstAppend,
                    startedCanceledAppend);
            }
            finally
            {
                CleanupTempRoot(root);
            }
        }
    }

    [Fact]
    public async Task AppendEventIfCurrentSessionAsync_StaleGeneration_DropsAuditEvent()
    {
        var root = CreateTempRoot();
        try
        {
            var fs = CreateFileSystem(root);
            string staleGeneration;
            await using (var writeLease = await fs.AcquireCanonicalWriteLeaseAsync())
                staleGeneration = fs.GetOrCreateSessionGeneration(writeLease);
            await SessionReplacementTestHarness.RotateGenerationAsync(fs);

            var appended = await new GmWorkerAuditLog(fs).AppendEventIfCurrentSessionAsync(
                staleGeneration,
                new WorkerAuditEvent
                {
                    EventId = "worker_audit_stale_session",
                    EventType = "task-dispatched",
                    WorkerId = "validation_repair_codex",
                    TaskId = "worker_task_stale_session",
                    TimestampUtc = "2026-07-22T00:00:00Z",
                    Summary = "Must not cross the session boundary."
                });

            Assert.False(appended);
            Assert.False(fs.FileExists(GmWorkerAuditLog.AuditLogPath));
        }
        finally
        {
            CleanupTempRoot(root);
        }
    }

    [Fact]
    public async Task AppendRequiredEventOnceIfCurrentSessionAsync_RepeatedEventIsIdempotent()
    {
        var root = CreateTempRoot();
        try
        {
            var fs = CreateFileSystem(root);
            string generation;
            await using (var writeLease = await fs.AcquireCanonicalWriteLeaseAsync())
                generation = fs.GetOrCreateSessionGeneration(writeLease);
            var audit = new GmWorkerAuditLog(fs);
            var auditEvent = new WorkerAuditEvent
            {
                EventId = "worker_audit_required_once",
                EventType = "process-tree-cleanup-confirmed",
                WorkerId = "validation_repair_codex",
                TaskId = "worker_task_required_once",
                TimestampUtc = "2026-08-02T00:00:00Z",
                Summary = "Quarantined process-tree cleanup completed."
            };

            var first = await audit.AppendRequiredEventOnceIfCurrentSessionAsync(
                generation,
                auditEvent);
            var second = await audit.AppendRequiredEventOnceIfCurrentSessionAsync(
                generation,
                auditEvent);

            Assert.Equal(GmWorkerAuditAppendDisposition.Appended, first);
            Assert.Equal(GmWorkerAuditAppendDisposition.Appended, second);
            var events = await audit.ReadEventsAsync();
            Assert.Single(events);
            Assert.Equal(auditEvent.EventId, events[0].EventId);
        }
        finally
        {
            CleanupTempRoot(root);
        }
    }

    [Fact]
    public async Task AppendRequiredEventOnceIfCurrentSessionAsync_WriteFailurePropagatesForRetry()
    {
        var root = CreateTempRoot();
        var rejectAuditWrite = 1;
        try
        {
            var fs = CreateFileSystem(
                root,
                new FileSystemManagerHooks
                {
                    BeforeCanonicalMutationBoundaryAsync = path =>
                    {
                        if (Volatile.Read(ref rejectAuditWrite) != 0 &&
                            path.Equals(
                                GmWorkerAuditLog.AuditLogPath,
                                StringComparison.OrdinalIgnoreCase))
                        {
                            return Task.FromException(
                                new IOException("Injected required audit write failure."));
                        }

                        return Task.CompletedTask;
                    }
                });
            string generation;
            await using (var writeLease = await fs.AcquireCanonicalWriteLeaseAsync())
                generation = fs.GetOrCreateSessionGeneration(writeLease);
            var audit = new GmWorkerAuditLog(fs);
            var auditEvent = new WorkerAuditEvent
            {
                EventId = "worker_audit_required_retry",
                EventType = "process-tree-cleanup-confirmed",
                WorkerId = "validation_repair_codex",
                TaskId = "worker_task_required_retry",
                TimestampUtc = "2026-08-02T00:00:00Z",
                Summary = "Quarantined process-tree cleanup completed."
            };

            await Assert.ThrowsAsync<IOException>(
                () => audit.AppendRequiredEventOnceIfCurrentSessionAsync(
                    generation,
                    auditEvent));
            Assert.False(fs.FileExists(GmWorkerAuditLog.AuditLogPath));

            Volatile.Write(ref rejectAuditWrite, 0);
            var disposition =
                await audit.AppendRequiredEventOnceIfCurrentSessionAsync(
                    generation,
                    auditEvent);

            Assert.Equal(GmWorkerAuditAppendDisposition.Appended, disposition);
            Assert.Single(await audit.ReadEventsAsync());
        }
        finally
        {
            CleanupTempRoot(root);
        }
    }

    private static FileSystemManager CreateFileSystem(
        string root,
        FileSystemManagerHooks? hooks = null)
    {
        var fs = new FileSystemManager(
            root,
            NullLogger<FileSystemManager>.Instance,
            PhysicalLoadTransactionOperations.Instance,
            hooks);
        fs.EnsureDirectoryStructure();
        return fs;
    }

    public enum AuditAppendEntryPoint
    {
        BestEffort,
        CurrentSession,
        RequiredOnce
    }

    private static async Task AppendThroughEntryPointAsync(
        GmWorkerAuditLog audit,
        AuditAppendEntryPoint entryPoint,
        string generation,
        WorkerAuditEvent auditEvent,
        CancellationToken cancellationToken = default)
    {
        switch (entryPoint)
        {
            case AuditAppendEntryPoint.BestEffort:
                await audit.AppendEventAsync(auditEvent);
                break;
            case AuditAppendEntryPoint.CurrentSession:
                Assert.True(await audit.AppendEventIfCurrentSessionAsync(
                    generation,
                    auditEvent,
                    cancellationToken));
                break;
            case AuditAppendEntryPoint.RequiredOnce:
                Assert.Equal(
                    GmWorkerAuditAppendDisposition.Appended,
                    await audit.AppendRequiredEventOnceIfCurrentSessionAsync(
                        generation,
                        auditEvent,
                        cancellationToken));
                break;
            default:
                throw new ArgumentOutOfRangeException(nameof(entryPoint));
        }
    }

    private static async Task AwaitStartedTasksForCleanupAsync(
        params Task?[] tasks)
    {
        try
        {
            await Task.WhenAll(tasks.OfType<Task>());
        }
        catch
        {
            // Preserve the test's primary assertion or operation failure.
        }
    }

    private static WorkerAuditEvent CreateConcurrentAuditEvent(int index) =>
        new()
        {
            EventId = $"worker_audit_admission_{index:D2}",
            EventType = "task-dispatched",
            WorkerId = "validation_repair_codex",
            TaskId = $"worker_task_admission_{index:D2}",
            TimestampUtc = "2026-08-25T00:00:00Z",
            Summary = $"Admission event {index}."
        };

    private static string CreateTempRoot()
    {
        var root = Path.Combine(Path.GetTempPath(), "boe-gm-worker-audit-" + Guid.NewGuid().ToString("N"));
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
