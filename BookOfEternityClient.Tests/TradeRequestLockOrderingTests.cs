using BookOfEternityClient.Core;
using BookOfEternityClient.Services;
using Microsoft.Extensions.Logging.Abstractions;
using Xunit;

namespace BookOfEternityClient.Tests;

[CollectionDefinition(CollectionName, DisableParallelization = true)]
public sealed class TradeRequestLockOrderingCollection
{
    public const string CollectionName = "Canonical trade request lock ordering";
}

[Collection(TradeRequestLockOrderingCollection.CollectionName)]
public sealed class TradeRequestLockOrderingTests
{
    [Fact]
    public async Task GuardianTrade_LeaseBoundWriteDoesNotWaitBehindUnboundCanonicalContention()
    {
        var root = CreateTempRoot();
        try
        {
            var canonicalContended = new TaskCompletionSource(
                TaskCreationOptions.RunContinuationsAsynchronously);
            var fs = CreateFileSystem(root, canonicalContended);
            fs.EnsureDirectoryStructure();
            var request = CreateGuardianRequest();
            await GuardianTradeRequestState.WriteAsync(fs, request);

            var lease = await fs.AcquireCanonicalWriteLeaseAsync();
            Task? unboundWrite = null;
            Task<bool>? leaseBoundWrite = null;
            var completedBeforeLeaseRelease = false;
            try
            {
                unboundWrite = GuardianTradeRequestState.WriteAsync(fs, request);
                await canonicalContended.Task.WaitAsync(TimeSpan.FromSeconds(5));

                leaseBoundWrite = GuardianTradeRequestState.ClearIfMatchesAsync(fs, lease, request);
                completedBeforeLeaseRelease = await CompletesWithinAsync(
                    leaseBoundWrite,
                    TimeSpan.FromMilliseconds(500));
            }
            finally
            {
                await lease.DisposeAsync();
                await ObserveCompletionAsync(unboundWrite);
                await ObserveCompletionAsync(leaseBoundWrite);
            }

            Assert.True(
                completedBeforeLeaseRelease,
                "Lease-bound guardian trade writes must not wait behind an unbound writer that is waiting for the same canonical lease.");
        }
        finally
        {
            CleanupTempRoot(root);
        }
    }

    [Fact]
    public async Task ShiningTrade_LeaseBoundWriteDoesNotWaitBehindUnboundCanonicalContention()
    {
        var root = CreateTempRoot();
        try
        {
            var canonicalContended = new TaskCompletionSource(
                TaskCreationOptions.RunContinuationsAsynchronously);
            var fs = CreateFileSystem(root, canonicalContended);
            fs.EnsureDirectoryStructure();
            await ShiningTradeRequestState.WriteRequestAsync(
                fs,
                CreateShiningRequest("request_existing", "faction_existing"));

            var lease = await fs.AcquireCanonicalWriteLeaseAsync();
            Task? unboundWrite = null;
            Task<bool>? leaseBoundWrite = null;
            var completedBeforeLeaseRelease = false;
            try
            {
                unboundWrite = ShiningTradeRequestState.WriteRequestAsync(
                    fs,
                    CreateShiningRequest("request_unbound", "faction_unbound"));
                await canonicalContended.Task.WaitAsync(TimeSpan.FromSeconds(5));

                leaseBoundWrite = ShiningTradeRequestState.TryWriteScopedRequestAsync(
                    fs,
                    lease,
                    CreateShiningRequest("request_bound", "faction_bound"),
                    CreateShiningScope());
                completedBeforeLeaseRelease = await CompletesWithinAsync(
                    leaseBoundWrite,
                    TimeSpan.FromMilliseconds(500));
            }
            finally
            {
                await lease.DisposeAsync();
                await ObserveCompletionAsync(unboundWrite);
                await ObserveCompletionAsync(leaseBoundWrite);
            }

            Assert.True(
                completedBeforeLeaseRelease,
                "Lease-bound Shining trade writes must not wait behind an unbound writer that is waiting for the same canonical lease.");
        }
        finally
        {
            CleanupTempRoot(root);
        }
    }

    private static FileSystemManager CreateFileSystem(
        string root,
        TaskCompletionSource canonicalContended) =>
        new(
            root,
            NullLogger<FileSystemManager>.Instance,
            PhysicalLoadTransactionOperations.Instance,
            new FileSystemManagerHooks
            {
                CanonicalWriteLockContendedAsync = () =>
                {
                    canonicalContended.TrySetResult();
                    return Task.CompletedTask;
                }
            });

    private static GuardianTradeRequestState.PendingGuardianTradeRequest CreateGuardianRequest() =>
        new()
        {
            RequestId = "guardian_trade_lock_order",
            GuardianId = "guardian_lock_order",
            GuardianName = "Хранитель порядка",
            AbodeId = "abode_lock_order",
            ReturnCycleId = "return_lock_order",
            CurrentReputation = 10,
            DerivedTradeSlotCount = 3,
            CreatedAtTurn = 5
        };

    private static ShiningTradeRequestState.PendingShiningTradeInventoryRequest CreateShiningRequest(
        string requestId,
        string factionId) =>
        new()
        {
            RequestId = requestId,
            FactionId = factionId,
            FactionName = factionId,
            TradeCycleId = "shining_return_lock_order",
            DerivedTradeTier = 1,
            DerivedTradeSlotCount = 3,
            DerivedRarityCeiling = "common",
            DerivedServiceMultiplier = 1,
            CreatedAtTurn = 5
        };

    private static LocalInteractionScope CreateShiningScope() =>
        new(
            LocalInteractionRealmKind.ShiningAbode,
            IsResolved: true,
            LocationId: "hall_lock_order",
            LocationName: "Зал порядка",
            CurrentGuardianId: string.Empty,
            LocalActorIds: new HashSet<string>(StringComparer.OrdinalIgnoreCase),
            LocalFactionIds: new HashSet<string>(StringComparer.OrdinalIgnoreCase)
            {
                "faction_bound"
            },
            UnavailableReason: null,
            AuthoritySnapshots: Array.Empty<LocalInteractionAuthoritySnapshot>());

    private static async Task<bool> CompletesWithinAsync(Task task, TimeSpan timeout) =>
        ReferenceEquals(await Task.WhenAny(task, Task.Delay(timeout)), task);

    private static async Task ObserveCompletionAsync(Task? task)
    {
        if (task == null)
            return;

        try
        {
            await task.WaitAsync(TimeSpan.FromSeconds(5));
        }
        catch
        {
            // The RED implementation can resume with a disposed lease after the
            // test releases the canonical lock. The ordering assertion is the
            // behavior under test; observing the task prevents a leaked gate.
        }
    }

    private static string CreateTempRoot()
    {
        var root = Path.Combine(
            AppContext.BaseDirectory,
            "boe-test-artifacts",
            "trade-lock-order-" + Guid.NewGuid().ToString("N"));
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
            // Best-effort cleanup for Windows file-handle timing.
        }
    }
}
