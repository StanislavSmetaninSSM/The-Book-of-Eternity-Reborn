using BookOfEternityClient.Core;
using BookOfEternityClient.Services.GmWorkers;
using Microsoft.Extensions.Logging.Abstractions;
using System.Reflection;
using Xunit;

namespace BookOfEternityClient.Tests;

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

    [Fact]
    public async Task AppendEventIfCurrentSessionAsync_StaleGeneration_DropsAuditEvent()
    {
        var root = CreateTempRoot();
        try
        {
            var fs = CreateFileSystem(root);
            string staleGeneration;
            await using (var writeLease = await fs.AcquireCanonicalWriteLeaseAsync())
            {
                staleGeneration = fs.GetOrCreateSessionGeneration(writeLease);
                fs.RotateSessionGeneration(writeLease);
            }

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
    public void SharedAuditEventIdGenerator_DeterministicInputsProduceReadableStableId()
    {
        var generatorType = typeof(GmWorkerAuditLog).Assembly.GetType(
            "BookOfEternityClient.Services.GmWorkers.GmWorkerAuditEventIdGenerator");
        Assert.NotNull(generatorType);
        var factory = generatorType.GetMethod(
            "Create",
            BindingFlags.Static | BindingFlags.NonPublic,
            binder: null,
            types: [typeof(DateTimeOffset), typeof(Guid)],
            modifiers: null);
        Assert.NotNull(factory);

        var timestamp = new DateTimeOffset(2026, 7, 20, 3, 4, 5, 678, TimeSpan.Zero);
        var suffix = Guid.Parse("00112233-4455-6677-8899-aabbccddeeff");

        var eventId = Assert.IsType<string>(factory.Invoke(null, [timestamp, suffix]));

        Assert.Equal("worker_audit_20260720030405678_00112233445566778899aabbccddeeff", eventId);
    }

    [Fact]
    public void SharedAuditEventIdGenerator_ConcurrentCallsProduceUniqueReadableIds()
    {
        var generatorType = typeof(GmWorkerAuditLog).Assembly.GetType(
            "BookOfEternityClient.Services.GmWorkers.GmWorkerAuditEventIdGenerator");
        Assert.NotNull(generatorType);
        var factory = generatorType.GetMethod(
            "Create",
            BindingFlags.Static | BindingFlags.NonPublic,
            binder: null,
            types: Type.EmptyTypes,
            modifiers: null);
        Assert.NotNull(factory);
        var eventIds = new string[10_000];

        Parallel.For(0, eventIds.Length, index =>
        {
            eventIds[index] = Assert.IsType<string>(factory.Invoke(null, null));
        });

        Assert.Equal(
            eventIds.Length,
            eventIds.Distinct(StringComparer.Ordinal).Count());
        Assert.All(eventIds, eventId =>
            Assert.Matches("^worker_audit_[0-9]{17}_[0-9a-f]{32}$", eventId));
    }

    private static FileSystemManager CreateFileSystem(string root)
    {
        var fs = new FileSystemManager(root, NullLogger<FileSystemManager>.Instance);
        fs.EnsureDirectoryStructure();
        return fs;
    }

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
