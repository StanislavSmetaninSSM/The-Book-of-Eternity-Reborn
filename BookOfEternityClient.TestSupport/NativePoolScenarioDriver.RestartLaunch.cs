using System.Reflection;
using System.Text.Json;
using BookOfEternityClient.Core;
using BookOfEternityClient.Services.GmWorkers;
using Microsoft.Extensions.Logging.Abstractions;

namespace BookOfEternityClient.Tests;

internal static partial class NativePoolScenarioDriver
{
    private static async Task<int> RunRestartLaunch(string mode, string package, string output)
    {
        var root = Path.Combine(output, "state-copy");
        await BootstrapRestartRoot(root);
        var fs = new FileSystemManager(root, NullLogger<FileSystemManager>.Instance);
        var profile = OwnershipProfile(package, output) with { MaxConcurrentTasks = 2 };
        var taskA = OwnershipTask("launch_a"); var taskB = OwnershipTask("launch_b");
        var statePath = Path.Combine(root, ".boe_runtime", "worker-runs-v1", "state.json");
        var held = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        var resume = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        var fault = true; var faults = 0; var bOwners = 0; var refused = false;
        GmWorkerNativeLineageLaunch? ownerA = null;
        GmWorkerProcessHostLaunch? hostB = null; ReleaseCountingStream? observed = null;
        void Observe(WorkerLedgerIoStage stage)
        {
            if (mode != "busy" || !fault || stage != WorkerLedgerIoStage.StateDirectorySynced || !File.Exists(statePath)) return;
            var state = GmWorkerRunLedgerCodec.Decode(new(root), File.ReadAllBytes(statePath));
            if (state.Entries.Any(x => x.Identity.TaskId == taskA.TaskId && x.Phase == WorkerRunPhase.Released))
            { faults++; throw new IOException("Synthetic A Released ACK withheld before B LaunchIntent."); }
        }
        using var admissionA = new GmWorkerNativePoolAdmission(package, root, durable: true, Observe);
        using var admissionB = new GmWorkerNativePoolAdmission(package, root, durable: true);
        var reaperA = new GmWorkerQuarantineReaper(1, retrySchedule: [], runInBackground: false);
        var reaperB = new GmWorkerQuarantineReaper(1, retrySchedule: [], runInBackground: false);
        using (admissionA.Enter(fs)) { }
        var hooksA = new GmWorkerBridgePoolHooks
        {
            AfterOwnerBound = owner => ownerA = (GmWorkerNativeLineageLaunch)owner,
            BeforeWorkerReleaseAsync = async () =>
            {
                if (mode != "foreign") return;
                held.TrySetResult(); await resume.Task.WaitAsync(TimeSpan.FromSeconds(8));
                throw new IOException("Synthetic A stops without sending its own Release.");
            },
            AfterWorkerReleaseAsync = () => WaitOwnershipFile(Path.Combine(output, "worker-starts"))
        };
        var hooksB = new GmWorkerBridgePoolHooks
        {
            BeforeWorkspaceFileCreateAsync = async path =>
            {
                if (mode != "busy" || !path.EndsWith(GmWorkerBridgePool.GetTaskPacketPath(taskB.TaskId), StringComparison.Ordinal)) return;
                held.TrySetResult(); await resume.Task.WaitAsync(TimeSpan.FromSeconds(8));
            },
            AfterOwnerBound = _ => bOwners++,
            AfterHostPrepared = host =>
            {
                hostB = host;
                var flags = BindingFlags.Instance | BindingFlags.NonPublic;
                var stream = (Stream)typeof(GmWorkerProcessHostLaunch).GetField("_controlPipe", flags)!.GetValue(host)!;
                var nonce = (string)typeof(GmWorkerProcessHostLaunch).GetField("_launchNonce", flags)!.GetValue(host)!;
                observed = new(stream, nonce);
                typeof(GmWorkerProcessHostLaunch).GetField("_controlChannel", flags)!.SetValue(host, new GmWorkerProcessHostFrameChannel(observed));
            },
            BeforeWorkerReleaseAsync = async () =>
            {
                if (mode == "busy") throw new InvalidOperationException("B must stop at LaunchIntent Busy before binding a host.");
                if (mode == "omitted")
                {
                    try { await hostB!.ReleaseAsync(CancellationToken.None); }
                    catch (InvalidOperationException) { refused = true; }
                }
                else
                {
                    var originalA = (GmWorkerDurableExecution)typeof(GmWorkerNativeLineageLaunch)
                        .GetField("_durable", BindingFlags.Instance | BindingFlags.NonPublic)!.GetValue(ownerA)!;
                    await using var leaseA = await fs.AcquireCanonicalWriteLeaseAsync(workerPurpose: originalA.ReleasePurpose());
                    await originalA.PlanReleaseAsync(fs, leaseA);
                    try { await hostB!.ReleaseAsync(CancellationToken.None, originalA, leaseA); }
                    catch (InvalidOperationException) { refused = true; }
                }
                if (!refused) await WaitOwnershipFile(Path.Combine(output, "worker-starts"));
                throw new IOException("Synthetic probe stops before the pool's authorized Release.");
            }
        };
        var poolA = new GmWorkerBridgePool(fs, null, new GmWorkerAuditLog(fs), hooksA, GmWorkerProcessTreeFactory.Instance, reaperA, admissionA);
        var poolB = new GmWorkerBridgePool(fs, null, new GmWorkerAuditLog(fs), hooksB, GmWorkerProcessTreeFactory.Instance, reaperB, admissionB);
        GmWorkerTaskRunResult? resultA = null; GmWorkerTaskRunResult resultB;
        if (mode == "busy")
        {
            var pendingB = poolB.RunTaskAsync(profile, taskB);
            await held.Task.WaitAsync(TimeSpan.FromSeconds(8));
            resultA = await poolA.RunTaskAsync(profile, taskA);
            resume.TrySetResult(); resultB = await pendingB;
        }
        else if (mode == "foreign")
        {
            var pendingA = poolA.RunTaskAsync(profile, taskA);
            await held.Task.WaitAsync(TimeSpan.FromSeconds(8));
            resultB = await poolB.RunTaskAsync(profile, taskB);
            resume.TrySetResult(); resultA = await pendingA;
        }
        else resultB = await poolB.RunTaskAsync(profile, taskB);
        var beforeB = BoundarySnapshot(resultB, taskB, reaperB, statePath, null);
        fault = false;
        await reaperA.RunPassAsync(); await reaperB.RunPassAsync();
        var final = GmWorkerRunLedgerCodec.Decode(new(root), File.ReadAllBytes(statePath));
        var retired = final.Retired.Select(x => GmWorkerRunRecordCodec.Decode(File.ReadAllBytes(Path.Combine(root,
            ".boe_runtime", "worker-runs-v1", "retired", x.RunId + ".json")))).ToArray();
        await File.WriteAllTextAsync(Path.Combine(output, "restart-launch.json"), JsonSerializer.Serialize(new
        {
            mode, faults, bOwners, refused, beforeB,
            afterA = BoundarySnapshot(resultA, taskA, reaperA, statePath, null),
            afterB = BoundarySnapshot(resultB, taskB, reaperB, statePath, null),
            bNeverStartedRetired = retired.Any(x => x.Identity.TaskId == taskB.TaskId && x.Phase == WorkerRunPhase.AbortedBeforeLaunch),
            distinctEpochs = retired.Select(x => x.Identity.Epoch).Distinct().Count(), retired = retired.Length,
            releaseFrames = observed?.ReleaseFrames ?? 0,
            workerStarts = File.Exists(Path.Combine(output, "worker-starts")) ? File.ReadAllLines(Path.Combine(output, "worker-starts")).Length : 0,
            detachedRemaining = Directory.Exists(admissionB.RuntimeBase) && Directory.EnumerateDirectories(admissionB.RuntimeBase, "game_session", SearchOption.AllDirectories).Any()
        }));
        return 0;
    }
}
