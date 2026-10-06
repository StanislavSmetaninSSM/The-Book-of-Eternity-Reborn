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
    private static async Task<int> RunRestartRoot(string mode, string package, string output)
    {
        var root = Path.Combine(output, "state-copy");
        await BootstrapRestartRoot(root);
        var recovery = 0;
        var fs = new FileSystemManager(root, NullLogger<FileSystemManager>.Instance,
            PhysicalLoadTransactionOperations.Instance, new FileSystemManagerHooks { LocalPublicationRecoveryObserver = (_, _) => recovery++ });
        var profile = GmWorkerBridgeTestFixtures.ValidationRepairCodexProfile() with
        {
            LaunchCommand = string.Join(" ", new[] { Path.Combine(Environment.GetEnvironmentVariable("DOTNET_ROOT")!, "dotnet"),
                typeof(NativePoolScenarioDriver).Assembly.Location, "pool-worker-content-valid", package, output }.Select(x => "\"" + x + "\"")),
            TimeoutSeconds = 15, MaxConcurrentTasks = 2
        };
        var taskA = GmWorkerBridgeTestFixtures.ValidationRepairTask() with
        {
            TaskId = "restart_a", TimeoutSeconds = profile.TimeoutSeconds,
            ContextFiles = [new WorkerFileReference { Path = RestartContextPath, Sha256 = GmWorkerRunLedgerCodec.Hash(RestartContextBytes) }]
        };
        var taskB = taskA with { TaskId = "restart_b" };
        var statePath = Path.Combine(root, ".boe_runtime", "worker-runs-v1", "state.json");
        var faultEnabled = true; var faults = 0; var seeded = false; var cleanupCalls = 0;
        var held = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        var resume = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        GmWorkerNativeLineageLaunch? ownerA = null;
        void Observe(WorkerLedgerIoStage stage)
        {
            if (mode != "foreign-pending" || !faultEnabled || stage != WorkerLedgerIoStage.StateDirectorySynced || !File.Exists(statePath)) return;
            var state = GmWorkerRunLedgerCodec.Decode(new(root), File.ReadAllBytes(statePath));
            if (state.Entries.Any(x => x.Identity.TaskId == taskA.TaskId && x.Phase == WorkerRunPhase.Published))
            { faults++; throw new IOException("Synthetic original A publication ACK is withheld."); }
        }
        using var admissionA = new GmWorkerNativePoolAdmission(package, root, durable: true, Observe);
        using var admissionB = new GmWorkerNativePoolAdmission(package, root, durable: true);
        var reaperA = new GmWorkerQuarantineReaper(capacity: 1, retrySchedule: [], runInBackground: false);
        var reaperB = new GmWorkerQuarantineReaper(capacity: 1, retrySchedule: [], runInBackground: false);
        // Attach the common coordinator with its negative observer before either pool.
        using (admissionA.Enter(fs)) { }
        var hooksA = new GmWorkerBridgePoolHooks
        {
            AfterOwnerBound = owner => ownerA = (GmWorkerNativeLineageLaunch)owner,
            BeforeWorkerReleaseAsync = async () =>
            {
                if (mode != "uncertain-recovery") return;
                held.TrySetResult(); await resume.Task.WaitAsync(TimeSpan.FromSeconds(8));
                OriginalSupervisor(ownerA!).StandardOutput.Close();
                var execution = (GmWorkerDurableExecution)typeof(GmWorkerNativeLineageLaunch).GetField("_durable", BindingFlags.Instance | BindingFlags.NonPublic)!.GetValue(ownerA)!;
                var clock = Stopwatch.StartNew();
                while (!execution.IsUncertain)
                {
                    if (clock.Elapsed > TimeSpan.FromSeconds(3)) throw new TimeoutException("Actual native status loss did not close original authority.");
                    await Task.Delay(10);
                }
            }
        };
        var hooksB = new GmWorkerBridgePoolHooks
        {
            BeforeWorkspaceCleanupAsync = async _ =>
            {
                if (++cleanupCalls != 1) return;
                if (mode == "foreign-pending")
                { held.TrySetResult(); await resume.Task.WaitAsync(TimeSpan.FromSeconds(8)); return; }
                // Leave a real interrupted canonical decision while both original
                // executions are still healthy; subsequent diagnostic append is
                // refused by the retained cleanup gate, so it cannot recover it.
                await using var lease = await fs.AcquireCanonicalWriteLeaseAsync();
                try
                {
                    new TrustedLocalFilePublication(fs, new TrustedLocalFileScope([root])).Publish(lease,
                        TrustedLocalGeneration.Existing(taskB.SessionGeneration),
                        [new(fs.ResolvePath(RestartContextPath), RestartContextBytes, Encoding.UTF8.GetBytes("{\"fixture\":\"unresolved-member\"}"))],
                        (phase, _) =>
                        {
                            if (phase == TrustedLocalPublicationPhase.MemberPublished)
                            { seeded = true; throw new IOException("Synthetic direct interrupted member publication."); }
                        });
                }
                catch (IOException error) when (seeded && error.Message == "Synthetic direct interrupted member publication.") { }
                if (!seeded) throw new InvalidOperationException("Real recovery evidence was not prepared.");
                throw new IOException("Synthetic one-shot cleanup failure retains B for exact audit retry.");
            }
        };
        var poolA = new GmWorkerBridgePool(fs, null, new GmWorkerAuditLog(fs), hooksA, GmWorkerProcessTreeFactory.Instance, reaperA, admissionA);
        var poolB = new GmWorkerBridgePool(fs, null, new GmWorkerAuditLog(fs), hooksB, GmWorkerProcessTreeFactory.Instance, reaperB, admissionB);
        GmWorkerTaskRunResult resultA, resultB;
        if (mode == "foreign-pending")
        {
            var pendingB = poolB.RunTaskAsync(profile, taskB);
            await held.Task.WaitAsync(TimeSpan.FromSeconds(8));
            resultA = await poolA.RunTaskAsync(profile, taskA);
            resume.TrySetResult(); resultB = await pendingB;
        }
        else
        {
            var pendingA = poolA.RunTaskAsync(profile, taskA);
            await held.Task.WaitAsync(TimeSpan.FromSeconds(8));
            resultB = await poolB.RunTaskAsync(profile, taskB);
            resume.TrySetResult(); resultA = await pendingA;
        }
        GmWorkerStopEvidence? lateOriginalStop = null;
        if (mode == "uncertain-recovery")
        {
            // Original owner may still issue bounded stop despite its lost status
            // observation. This does not clear Uncertain or retire any capacity.
            lateOriginalStop = await ownerA!.StopAndObserveAsync();
            await ownerA.SupervisorExited.WaitAsync(TimeSpan.FromSeconds(5));
        }
        var beforeA = BoundarySnapshot(resultA, taskA, reaperA, statePath, null);
        var beforeB = BoundarySnapshot(resultB, taskB, reaperB, statePath, null);
        var before = RecoverySnapshot(root);
        var beforeRecovery = recovery;
        faultEnabled = false;
        if (mode == "foreign-pending") await reaperA.RunPassAsync();
        await reaperB.RunPassAsync();
        var state = GmWorkerRunLedgerCodec.Decode(new(root), File.ReadAllBytes(statePath));
        var retired = state.Retired.Select(x => GmWorkerRunRecordCodec.Decode(File.ReadAllBytes(Path.Combine(root, ".boe_runtime", "worker-runs-v1", "retired", x.RunId + ".json")))).ToArray();
        await File.WriteAllTextAsync(Path.Combine(output, "restart-root.json"), JsonSerializer.Serialize(new
        {
            mode, faults, seeded, lateOriginalStop, beforeA, beforeB,
            afterA = BoundarySnapshot(resultA, taskA, reaperA, statePath, null),
            afterB = BoundarySnapshot(resultB, taskB, reaperB, statePath, null),
            recoveryWrites = recovery - beforeRecovery, recoveryEvidencePreserved = before.SequenceEqual(RecoverySnapshot(root)),
            active = state.Entries.Length, retired = state.Retired.Length,
            distinctOriginalEpochs = retired.Select(x => x.Identity.Epoch).Distinct().Count() == 2 && retired.All(x =>
                x.Identity.RunId == (x.Identity.TaskId == taskA.TaskId ? resultA : resultB).ExecutionIdentity?.RunId),
            workerStarts = File.Exists(Path.Combine(output, "worker-starts")) ? File.ReadAllLines(Path.Combine(output, "worker-starts")).Length : 0
        }));
        return 0;
    }
    private static string[] RecoverySnapshot(string root) => Directory.EnumerateFiles(root, "*", SearchOption.AllDirectories)
        .Where(path => path.StartsWith(Path.Combine(root, "game_session") + Path.DirectorySeparatorChar, StringComparison.Ordinal) ||
            path.StartsWith(Path.Combine(root, ".boe_runtime", "trusted-local-publication-v1") + Path.DirectorySeparatorChar, StringComparison.Ordinal))
        .Order(StringComparer.Ordinal).Select(path => Path.GetRelativePath(root, path) + ":" + GmWorkerRunLedgerCodec.Hash(File.ReadAllBytes(path))).ToArray();
}
