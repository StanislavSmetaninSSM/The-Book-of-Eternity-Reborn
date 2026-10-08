using System.Collections.Concurrent;
using System.Text;
using System.Text.Json;
using BookOfEternityClient.Core;
using BookOfEternityClient.Services.GmWorkers;
using Microsoft.Extensions.Logging.Abstractions;

namespace BookOfEternityClient.Tests;

internal static partial class NativePoolScenarioDriver
{
    private static async Task<int> SeedRestartFenceInventory(string mode, string package, string output)
    {
        var root = Path.Combine(output, "state-copy");
        await BootstrapRestartRoot(root);
        var fs = new FileSystemManager(root, NullLogger<FileSystemManager>.Instance);
        var statePath = Path.Combine(root, ".boe_runtime", "worker-runs-v1", "state.json");
        var count = mode == "runtime-missing" || mode == "journal-conflict" ? 1 : 2;
        var owners = new ConcurrentDictionary<string, GmWorkerDurableExecution>();
        var first = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        var hold = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        var ready = 0; var cuts = 0;
        var profile = OwnershipProfile(package, output) with { MaxConcurrentTasks = count };
        var task = OwnershipTask("restart_r3_inventory_a");
        var reaper = new GmWorkerQuarantineReaper(count, retrySchedule: [], runInBackground: false);
        void Crash(string phase, bool originalAccepted = false)
        {
            if (++cuts != 1) throw new InvalidOperationException("Inventory crash observation retried.");
            var state = GmWorkerRunLedgerCodec.Decode(new(root), File.ReadAllBytes(statePath));
            File.WriteAllText(Path.Combine(output, "fence-inventory-cut.json"), JsonSerializer.Serialize(new
            {
                mode, cuts, phase, active = state.Entries.Length, retired = state.Retired.Length,
                phases = state.Entries.Select(e => e.Phase.ToString()).ToArray(), records = state.Entries,
                ownedEntries = owners.Count, ready, sourceLimit = profile.MaxConcurrentTasks,
                coldLimit = OwnershipProfile(package, output).MaxConcurrentTasks,
                exactOriginalBindings = state.Entries.All(e => owners.TryGetValue(e.Identity.RunId, out var original) && original.Identity == e.Identity &&
                    File.Exists(fs.ResolvePath(GmWorkerBridgePool.GetTaskPacketPath(e.Identity.TaskId))) && e.Identity.TaskSha256 == GmWorkerRunLedgerCodec.Hash(File.ReadAllBytes(fs.ResolvePath(GmWorkerBridgePool.GetTaskPacketPath(e.Identity.TaskId))))),
                capacity = reaper.OwnedCapacity, entries = reaper.EntryCount, workerStarts = FenceWorkerStarts(output), originalAccepted,
                journalExists = File.Exists(Path.Combine(fs.RuntimeRootPath, "trusted-local-publication-v1", "active.json")),
                cutFilesExcludingLockDescriptors = FenceCutSnapshot(root)
            }));
            ExitRestartImmediately(77);
        }
        var hooks = new GmWorkerBridgePoolHooks
        {
            AfterOwnerBound = owner =>
            {
                var original = FenceField<GmWorkerDurableExecution>(owner, "_durable")!;
                if (!owners.TryAdd(original.Identity.RunId, original)) throw new InvalidOperationException("Duplicate original inventory identity.");
            },
            BeforeWorkerReleaseAsync = async () =>
            {
                if (mode == "journal-conflict") return;
                var reached = Interlocked.Increment(ref ready);
                if (reached == count) Crash("BoundBeforeRelease");
                first.TrySetResult();
                await hold.Task.WaitAsync(TimeSpan.FromSeconds(8));
            }
        };
        using var admission = new GmWorkerNativePoolAdmission(package, root, durable: true);
        var pool = new GmWorkerBridgePool(fs, null, new GmWorkerAuditLog(fs), hooks, GmWorkerProcessTreeFactory.Instance, reaper, admission);
        var running = pool.RunTaskAsync(profile, task);
        if (count == 2)
        {
            await first.Task.WaitAsync(TimeSpan.FromSeconds(8));
            await Task.WhenAll(running, pool.RunTaskAsync(profile, OwnershipTask("restart_r3_inventory_b")));
        }
        else
        {
            var result = await running;
            if (mode == "journal-conflict")
            {
                var state = GmWorkerRunLedgerCodec.Decode(new(root), File.ReadAllBytes(statePath));
                if (!result.HasValidatedExecutionFor(task) || state.Entries.Length != 0 || state.Retired.Length != 1 || reaper.OwnedCapacity != 0)
                    throw new InvalidOperationException("Unmasked journal probe requires an actually retired quiescent root.");
                await using var lease = await fs.AcquireCanonicalWriteLeaseAsync();
                new TrustedLocalFilePublication(fs, new TrustedLocalFileScope([root])).Publish(lease,
                    TrustedLocalGeneration.Existing(task.SessionGeneration),
                    [new(fs.ResolvePath(RestartContextPath), RestartContextBytes, Encoding.UTF8.GetBytes("{\"fixture\":\"r3-committed\"}"))],
                    (phase, _) => { if (phase == TrustedLocalPublicationPhase.Committed) Crash(phase.ToString(), true); });
            }
        }
        throw new InvalidOperationException("Required actual inventory crash boundary not reached: " + mode);
    }
}
