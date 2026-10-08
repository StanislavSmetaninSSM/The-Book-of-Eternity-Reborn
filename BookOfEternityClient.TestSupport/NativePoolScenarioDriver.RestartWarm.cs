using System.Diagnostics;
using System.Reflection;
using System.Text;
using System.Text.Json;
using BookOfEternityClient.Core;
using BookOfEternityClient.Services.GmWorkers;
using Microsoft.Extensions.Logging.Abstractions;

namespace BookOfEternityClient.Tests;

internal static partial class NativePoolScenarioDriver
{
    private static async Task<int> RunRestartWarm(string package, string output)
    {
        var root = Path.Combine(output, "state-copy"); await BootstrapRestartRoot(root);
        var fs = new FileSystemManager(root, NullLogger<FileSystemManager>.Instance);
        var statePath = Path.Combine(root, ".boe_runtime", "worker-runs-v1", "state.json");
        var fault = true; var faults = 0;
        void Observe(WorkerLedgerIoStage stage)
        {
            if (fault && stage == WorkerLedgerIoStage.StateDirectorySynced && File.Exists(statePath) &&
                GmWorkerRunLedgerCodec.Decode(new(root), File.ReadAllBytes(statePath)).Entries.Any(x => x.Phase == WorkerRunPhase.Uncertain))
            { faults++; throw new IOException("Synthetic Uncertain journal ACK withheld."); }
        }
        GmWorkerNativeLineageLaunch? owner = null;
        var ready = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        var resume = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        var hooks = new GmWorkerBridgePoolHooks
        {
            AfterOwnerBound = original => owner = (GmWorkerNativeLineageLaunch)original,
            BeforeWorkerReleaseAsync = async () => { ready.TrySetResult(); await resume.Task; }
        };
        using var admission = new GmWorkerNativePoolAdmission(package, root, durable: true, Observe);
        var reaper = new GmWorkerQuarantineReaper(1, retrySchedule: [], runInBackground: false);
        var task = OwnershipTask("warm_original");
        var pool = new GmWorkerBridgePool(fs, null, new GmWorkerAuditLog(fs), hooks, GmWorkerProcessTreeFactory.Instance, reaper, admission);
        var running = pool.RunTaskAsync(OwnershipProfile(package, output), task);
        await ready.Task.WaitAsync(TimeSpan.FromSeconds(8));
        await using (var lease = await fs.AcquireCanonicalWriteLeaseAsync())
        {
            var installed = false;
            try
            {
                new TrustedLocalFilePublication(fs, new TrustedLocalFileScope([root])).Publish(lease,
                    TrustedLocalGeneration.Existing(task.SessionGeneration),
                    [new(fs.ResolvePath(RestartContextPath), RestartContextBytes, Encoding.UTF8.GetBytes("warm interrupted member"))],
                    (phase, _) => { if (phase == TrustedLocalPublicationPhase.MemberPublished) { installed = true; throw new IOException("Original warm recovery evidence."); } });
            }
            catch (IOException) when (installed) { }
            if (!installed) throw new InvalidOperationException("Warm recovery fixture did not reach its cut.");
        }
        var flags = BindingFlags.Instance | BindingFlags.NonPublic;
        var execution = (GmWorkerDurableExecution)typeof(GmWorkerNativeLineageLaunch).GetField("_durable", flags)!.GetValue(owner)!;
        OriginalSupervisor(owner!).StandardOutput.Close();
        var clock = Stopwatch.StartNew();
        while (!execution.IsUncertain)
        {
            if (clock.Elapsed > TimeSpan.FromSeconds(3)) throw new TimeoutException("Original native loss was not observed.");
            await Task.Delay(10);
        }
        var beforeLateStop = typeof(GmWorkerNativeLineageLaunch).GetField("_stopControl", flags)!.GetValue(owner) == null;
        admission.Dispose();
        var before = RecoverySnapshot(root);
        var secondFs = new FileSystemManager(root, NullLogger<FileSystemManager>.Instance);
        var canonicalRefused = false; var slots = 0; var reservations = 0;
        try { await using var denied = await secondFs.AcquireCanonicalWriteLeaseAsync(); }
        catch (Exception error) when (GmWorkerRunLedger.Unavailable(error)) { canonicalRefused = true; }
        using (var secondAdmission = new GmWorkerNativePoolAdmission(package, root, durable: true))
        {
            var secondHooks = new GmWorkerBridgePoolHooks
            {
                BeforeWorkerSlotWaitAsync = () => { slots++; throw new InvalidOperationException("Warm refusal probe stops before capacity."); },
                BeforeTaskReservationAsync = () => { reservations++; return Task.CompletedTask; }
            };
            var secondReaper = new GmWorkerQuarantineReaper(1, retrySchedule: [], runInBackground: false);
            var secondPool = new GmWorkerBridgePool(secondFs, null, null, secondHooks, GmWorkerProcessTreeFactory.Instance, secondReaper, secondAdmission);
            try { _ = await secondPool.RunTaskAsync(OwnershipProfile(package, output), OwnershipTask("warm_second")); }
            finally { resume.TrySetResult(); }
        }
        var preservedBeforeStop = before.SequenceEqual(RecoverySnapshot(root));
        var result = await running;
        await owner!.SupervisorExited.WaitAsync(TimeSpan.FromSeconds(5));
        var beforeRetry = BoundarySnapshot(result, task, reaper, statePath, execution.Identity.WorkspacePath);
        await using var competing = await GmWorkerRunLedger.OpenCoordinatorAsync(new(root));
        var lockRetained = competing == null;
        fault = false; await reaper.RunPassAsync();
        await File.WriteAllTextAsync(Path.Combine(output, "restart-warm.json"), JsonSerializer.Serialize(new
        {
            beforeLateStop, canonicalRefused, slots, reservations, preservedBeforeStop, faults, lockRetained,
            beforeRetry, afterRetry = BoundarySnapshot(result, task, reaper, statePath, execution.Identity.WorkspacePath),
            absorbingUncertain = execution.IsUncertain, recoveryPreserved = before.SequenceEqual(RecoverySnapshot(root)),
            poolAttemptedStop = typeof(GmWorkerNativeLineageLaunch).GetField("_stopControl", flags)!.GetValue(owner) != null,
            workerStarts = File.Exists(Path.Combine(output, "worker-starts")) ? File.ReadAllLines(Path.Combine(output, "worker-starts")).Length : 0
        }));
        return 0;
    }
}
