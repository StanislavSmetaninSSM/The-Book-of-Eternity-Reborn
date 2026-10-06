using System.Text.Json;
using BookOfEternityClient.Core;
using BookOfEternityClient.Services.GmWorkers;
using Microsoft.Extensions.Logging.Abstractions;

namespace BookOfEternityClient.Tests;

internal static partial class NativePoolScenarioDriver
{
    private static async Task<int> RunRestartFenceRace(string mode, string package, string output)
    {
        var root = Path.Combine(output, "state-copy");
        await BootstrapRestartRoot(root);
        var task = OwnershipTask("restart_r3_original");
        var held = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        var resume = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        var contended = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        var statePath = Path.Combine(root, ".boe_runtime", "worker-runs-v1", "state.json");
        GmWorkerDurableExecution? execution = null;
        var mutationWaits = 0; var publicationCalls = 0; var contentionCalls = 0;
        var fs = new FileSystemManager(root, NullLogger<FileSystemManager>.Instance,
            PhysicalLoadTransactionOperations.Instance, new FileSystemManagerHooks
            {
                CanonicalWriteLockContendedAsync = () =>
                { contentionCalls++; contended.TrySetResult(); return Task.CompletedTask; },
                AfterCanonicalMutationBoundaryValidatedAsync = async path =>
                {
                    if (mode == "queued-generation" || path != "worker_proposals/worker_proposal_" + task.TaskId) return;
                    mutationWaits++; held.TrySetResult(); await resume.Task.WaitAsync(TimeSpan.FromSeconds(8));
                }
            });
        var hooks = new GmWorkerBridgePoolHooks
        {
            AfterOwnerBound = owner => execution = FenceField<GmWorkerDurableExecution>(owner, "_durable")!,
            BeforeProposalPublicationAsync = async () =>
            {
                publicationCalls++;
                if (mode == "queued-generation") { held.TrySetResult(); await resume.Task.WaitAsync(TimeSpan.FromSeconds(8)); }
            }
        };
        var reaper = new GmWorkerQuarantineReaper(1, retrySchedule: [], runInBackground: false);
        using var admission = new GmWorkerNativePoolAdmission(package, root, durable: true);
        var pool = new GmWorkerBridgePool(fs, null, new GmWorkerAuditLog(fs), hooks, GmWorkerProcessTreeFactory.Instance, reaper, admission);
        var run = pool.RunTaskAsync(OwnershipProfile(package, output), task);
        await held.Task.WaitAsync(TimeSpan.FromSeconds(10));
        var atHold = GmWorkerRunLedgerCodec.Decode(new(root), File.ReadAllBytes(statePath)).Entries.Single();
        var taskPath = fs.ResolvePath(GmWorkerBridgePool.GetTaskPacketPath(task.TaskId));
        var originalTask = File.ReadAllBytes(taskPath);
        var workspace = atHold.Identity.WorkspacePath;
        var originalWorkspace = Directory.GetFiles(workspace, "*", SearchOption.AllDirectories).ToDictionary(path => path, File.ReadAllBytes);
        var originalGeneration = File.ReadAllBytes(fs.SessionGenerationPath);
        if (mode == "queued-generation")
        {
            var replacementFs = new FileSystemManager(root, NullLogger<FileSystemManager>.Instance);
            await using var lifecycle = await replacementFs.AcquireSessionLifecycleLeaseAsync();
            await using var replacement = await replacementFs.AcquireSessionReplacementWriteLeaseAsync(lifecycle);
            resume.TrySetResult();
            await contended.Task.WaitAsync(TimeSpan.FromSeconds(8));
            replacementFs.RotateSessionGeneration(replacement);
        }
        else
        {
            if (mode == "held-generation")
                await File.WriteAllTextAsync(fs.SessionGenerationPath, JsonSerializer.Serialize(new { SchemaVersion = 1, GenerationId = "22222222222222222222222222222222" }));
            else if (mode == "held-owner-loss")
            {
                var lockPath = Path.Combine(root, ".boe_runtime", "worker-runs-v1", "owner.lock");
                File.Move(lockPath, Path.Combine(output, "detached-original-owner.lock"));
                await File.WriteAllBytesAsync(lockPath, []);
            }
            else throw new ArgumentException("Unknown publication race.");
            resume.TrySetResult();
        }
        var result = await run.WaitAsync(TimeSpan.FromSeconds(12));
        object Snapshot()
        {
            var state = GmWorkerRunLedgerCodec.Decode(new(root), File.ReadAllBytes(statePath));
            return new
            {
                accepted = result.HasValidatedExecutionFor(task), proposalConsumer = GmWorkerProposalOnlyDispatchService.CanAcceptExecution(result, task),
                repairConsumer = GmWorkerValidationRepairDelegator.CanAcceptExecution(result, task),
                capacity = reaper.OwnedCapacity, entries = reaper.EntryCount, active = state.Entries.Length, retired = state.Retired.Length,
                phase = state.Entries.SingleOrDefault()?.Phase.ToString(), uncertain = execution!.IsUncertain,
                stop = execution.Authority.StopEvidence?.State.ToString(), outputsSettled = execution.Authority.OutputsSettled,
                workspaceExists = Directory.Exists(workspace), workspaceBytesPreserved = originalWorkspace.Count > 0 && originalWorkspace.All(p => File.Exists(p.Key) && p.Value.SequenceEqual(File.ReadAllBytes(p.Key))),
                taskPreserved = originalTask.SequenceEqual(File.ReadAllBytes(taskPath)),
                bundleExists = Directory.Exists(fs.ResolvePath("worker_proposals/worker_proposal_" + task.TaskId)),
                inboxExists = File.Exists(fs.ResolvePath(GmWorkerBridgePool.GetProposalInboxPath(task.TaskId))),
                publicationAcknowledged = FenceField<bool>(execution, "_publicationAcknowledged"), error = result.Status.LastError
            };
        }
        var beforeRetry = Snapshot();
        // Restoration of synthetic generation bytes cannot resolve the prior held
        // publication boundary or grant a new result/cleanup permit.
        if (mode == "held-generation") await File.WriteAllBytesAsync(fs.SessionGenerationPath, originalGeneration);
        await reaper.RunPassAsync();
        await reaper.RunPassAsync();
        File.WriteAllText(Path.Combine(output, "fence-race.json"), JsonSerializer.Serialize(new
        { mode, publicationCalls, mutationWaits, contentionCalls, heldPhase = atHold.Phase.ToString(), taskBound = atHold.Identity.TaskSha256 == GmWorkerRunLedgerCodec.Hash(originalTask),
            originalWorkspaceFiles = originalWorkspace.Count, beforeRetry, afterRetry = Snapshot(), workerStarts = FenceWorkerStarts(output) }));
        return 0;
    }
}
