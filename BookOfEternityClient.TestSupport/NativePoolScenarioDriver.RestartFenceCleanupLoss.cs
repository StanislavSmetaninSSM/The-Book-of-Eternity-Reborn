using System.Text.Json;
using BookOfEternityClient.Core;
using BookOfEternityClient.Services.GmWorkers;
using Microsoft.Extensions.Logging.Abstractions;

namespace BookOfEternityClient.Tests;

internal static partial class NativePoolScenarioDriver
{
    private static async Task<int> RunRestartFenceCleanupLoss(string mode, string package, string output)
    {
        var root = Path.Combine(output, "state-copy");
        await BootstrapRestartRoot(root);
        var fs = new FileSystemManager(root, NullLogger<FileSystemManager>.Instance);
        var task = OwnershipTask("restart_r3_original");
        var statePath = Path.Combine(root, ".boe_runtime", "worker-runs-v1", "state.json");
        GmWorkerDurableExecution? execution = null;
        var held = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        var resume = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        Dictionary<string, byte[]>? pins = null;
        var faults = 0; var cleanupHooks = 0;
        string? heldDisk = null, heldLive = null, heldStop = null; bool heldOutputs = false;
        void ObserveCut()
        {
            var record = GmWorkerRunLedgerCodec.Decode(new(root), File.ReadAllBytes(statePath)).Entries.Single();
            pins = Directory.GetFiles(record.Identity.WorkspacePath, "*", SearchOption.AllDirectories).ToDictionary(p => p, File.ReadAllBytes);
            heldDisk = record.Phase.ToString(); heldLive = FenceField<WorkerRunRecord>(execution!, "_record")!.Phase.ToString();
            heldStop = execution!.Authority.StopEvidence?.State.ToString(); heldOutputs = execution.Authority.OutputsSettled;
        }
        void LoseOwner()
        {
            faults++;
            var ownerPath = Path.Combine(root, ".boe_runtime", "worker-runs-v1", "owner.lock");
            File.Move(ownerPath, Path.Combine(output, "detached-original-owner.lock"));
            File.WriteAllBytes(ownerPath, []);
        }
        void Observe(WorkerLedgerIoStage stage)
        {
            if (mode != "pending-ack" || faults != 0 || stage != WorkerLedgerIoStage.StateDirectorySynced || !File.Exists(statePath)) return;
            if (!GmWorkerRunLedgerCodec.Decode(new(root), File.ReadAllBytes(statePath)).Entries.Any(e => e.Phase == WorkerRunPhase.Published)) return;
            ObserveCut(); LoseOwner();
            throw new IOException("Synthetic original Published ACK withheld after actual owner-lock loss.");
        }
        var hooks = new GmWorkerBridgePoolHooks
        {
            AfterOwnerBound = owner => execution = FenceField<GmWorkerDurableExecution>(owner, "_durable")!,
            BeforeWorkspaceCleanupAsync = async _ =>
            {
                cleanupHooks++;
                if (mode == "cleanup-await" && faults == 0)
                { ObserveCut(); held.TrySetResult(); await resume.Task.WaitAsync(TimeSpan.FromSeconds(8)); }
            }
        };
        var reaper = new GmWorkerQuarantineReaper(1, retrySchedule: [], runInBackground: false);
        using var admission = new GmWorkerNativePoolAdmission(package, root, durable: true, Observe);
        var pool = new GmWorkerBridgePool(fs, null, new GmWorkerAuditLog(fs), hooks, GmWorkerProcessTreeFactory.Instance, reaper, admission);
        var run = pool.RunTaskAsync(OwnershipProfile(package, output), task);
        if (mode == "cleanup-await")
        { await held.Task.WaitAsync(TimeSpan.FromSeconds(10)); LoseOwner(); resume.TrySetResult(); }
        var result = await run.WaitAsync(TimeSpan.FromSeconds(12));
        object Snapshot()
        {
            var state = GmWorkerRunLedgerCodec.Decode(new(root), File.ReadAllBytes(statePath));
            return new
            {
                accepted = result.HasValidatedExecutionFor(task), proposalConsumer = GmWorkerProposalOnlyDispatchService.CanAcceptExecution(result, task),
                repairConsumer = GmWorkerValidationRepairDelegator.CanAcceptExecution(result, task), uncertain = execution!.IsUncertain,
                workspaceExists = Directory.Exists(execution.Identity.WorkspacePath),
                workspaceBytesPreserved = pins!.Count > 0 && pins.All(p => File.Exists(p.Key) && p.Value.SequenceEqual(File.ReadAllBytes(p.Key))),
                taskPreserved = File.Exists(fs.ResolvePath(GmWorkerBridgePool.GetTaskPacketPath(task.TaskId))) && execution.Identity.TaskSha256 == GmWorkerRunLedgerCodec.Hash(File.ReadAllBytes(fs.ResolvePath(GmWorkerBridgePool.GetTaskPacketPath(task.TaskId)))),
                capacity = reaper.OwnedCapacity, entries = reaper.EntryCount, active = state.Entries.Length, retired = state.Retired.Length,
                stop = execution.Authority.StopEvidence?.State.ToString(), outputsSettled = execution.Authority.OutputsSettled,
                bundleExists = File.Exists(fs.ResolvePath(GmWorkerProposalStore.GetProposalPath("worker_proposal_" + task.TaskId))),
                inboxExists = File.Exists(fs.ResolvePath(GmWorkerBridgePool.GetProposalInboxPath(task.TaskId))), error = result.Status.LastError
            };
        }
        var beforeRetry = Snapshot();
        await reaper.RunPassAsync(); await reaper.RunPassAsync();
        File.WriteAllText(Path.Combine(output, "fence-cleanup-loss.json"), JsonSerializer.Serialize(new
        { mode, faults, cleanupHooks, heldDisk, heldLive, heldStop, heldOutputs, originalWorkspaceFiles = pins!.Count,
            workerStarts = FenceWorkerStarts(output), beforeRetry, afterRetry = Snapshot() }));
        return 0;
    }
}
