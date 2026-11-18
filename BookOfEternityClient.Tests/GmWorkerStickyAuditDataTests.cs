using BookOfEternityClient.Services;
using BookOfEternityClient.Services.GmWorkers;
using Xunit;

namespace BookOfEternityClient.Tests;

public sealed class GmWorkerStickyAuditDataTests
{
    [Fact]
    public async Task OriginalCanonicalUncertaintySuppressesCallbacksAndSecondAppend()
    {
        var task = GmWorkerBridgeTestFixtures.AnalysisTask();
        var authority = GmWorkerExecutionAuthority.NoLaunch(task);
        var fault = new CoordinatedStatePublicationUncertainException(new IOException("exact required-audit sentinel"));
        var slot = new Slot();
        var appends = 0;
        var callbacks = 0;
        var owner = new GmWorkerQuarantinedExecution("sticky-proof", authority, null, null, null, slot, null,
            null, task.SessionGeneration, new WorkerAuditEvent
            {
                EventId = "sticky-proof", EventType = "process-tree-cleanup-confirmed",
                WorkerId = task.WorkerId, TaskId = task.TaskId,
                TimestampUtc = "2026-11-18T00:00:00Z", Summary = "NoLaunch typed sentinel",
                Details = new Dictionary<string, IReadOnlyList<string>>()
            }, () => { appends++; return Task.FromException<GmWorkerAuditAppendDisposition>(fault); },
            _ => { callbacks++; return Task.CompletedTask; });
        var reaper = new GmWorkerQuarantineReaper(1, [], runInBackground: false);
        using var reservation = reaper.TryReserve();
        Assert.NotNull(reservation);
        reservation.Transfer(owner);
        for (var pass = 0; pass < 2; pass++)
        {
            await reaper.RunPassAsync();
            Assert.Same(fault, authority.CanonicalPublicationFailure);
            Assert.Same(fault, Record.Exception(authority.ThrowIfCanonicalPublicationFailed));
            Assert.Equal(1, appends);
            Assert.Equal(0, callbacks);
            Assert.Equal(0, slot.Disposals);
            Assert.Equal(1, reaper.EntryCount);
            Assert.Equal(1, reaper.OwnedCapacity);
        }
        // No process/filesystem owner exists here. Logical debt is deliberately
        // retained; the test never clears it to manufacture cleanup success.
    }
    private sealed class Slot : IDisposable
    {
        internal int Disposals;
        public void Dispose() => Disposals++;
    }
}
