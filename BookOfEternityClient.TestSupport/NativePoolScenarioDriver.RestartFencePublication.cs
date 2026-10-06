using System.Text.Json;
using BookOfEternityClient.Core;
using BookOfEternityClient.Services.GmWorkers;
using Microsoft.Extensions.Logging.Abstractions;

namespace BookOfEternityClient.Tests;

internal static partial class NativePoolScenarioDriver
{
    private static async Task<int> SeedRestartFencePublication(string cut, string package, string output)
    {
        var root = Path.Combine(output, "state-copy");
        await BootstrapRestartRoot(root);
        var task = OwnershipTask("restart_r3_original");
        var statePath = Path.Combine(root, ".boe_runtime", "worker-runs-v1", "state.json");
        var bundlePath = Path.Combine(root, GmWorkerProposalStore.GetProposalPath("worker_proposal_" + task.TaskId));
        var inboxPath = Path.Combine(root, GmWorkerBridgePool.GetProposalInboxPath(task.TaskId));
        var auditPath = Path.Combine(root, GmWorkerAuditLog.AuditLogPath);
        GmWorkerDurableExecution? execution = null;
        var attempts = 0;
        WorkerRunRecord Record() => GmWorkerRunLedgerCodec.Decode(new(root), File.ReadAllBytes(statePath)).Entries.Single();
        string? Hash(string path) => File.Exists(path) ? GmWorkerRunLedgerCodec.Hash(File.ReadAllBytes(path)) : null;
        void Crash()
        {
            if (++attempts != 1) throw new InvalidOperationException("A retry is not the original publication cut.");
            var record = Record();
            var authority = execution!.Authority;
            File.WriteAllText(Path.Combine(output, "fence-cut.json"), JsonSerializer.Serialize(new
            {
                cut, cutAttempts = attempts, diskPhase = record.Phase.ToString(),
                livePhase = FenceField<WorkerRunRecord>(execution, "_record")!.Phase.ToString(),
                publicationAcknowledged = FenceField<bool>(execution, "_publicationAcknowledged"),
                originalPublicationRecorded = FenceField<object>(authority, "_publication") != null,
                record.Identity, record.Progress,
                originalTaskBound = record.Identity.TaskId == task.TaskId && record.Identity.TaskSha256 == Hash(Path.Combine(root, GmWorkerBridgePool.GetTaskPacketPath(task.TaskId))),
                workerStarts = FenceWorkerStarts(output), completion = FenceField<int?>(authority, "_completion"),
                stop = authority.StopEvidence?.State.ToString(), outputsSettled = authority.OutputsSettled,
                workspaceExists = Directory.Exists(record.Identity.WorkspacePath),
                bundleHash = Hash(bundlePath), inboxHash = Hash(inboxPath), auditHash = Hash(auditPath),
                receivedEvents = File.Exists(auditPath) ? File.ReadAllLines(auditPath).Count(line => line.Contains("proposal-received", StringComparison.Ordinal)) : 0,
                cutFilesExcludingLockDescriptors = FenceCutSnapshot(root)
            }));
            ExitRestartImmediately(77);
        }
        void Observe(WorkerLedgerIoStage stage)
        {
            if (!File.Exists(statePath)) return;
            var records = GmWorkerRunLedgerCodec.Decode(new(root), File.ReadAllBytes(statePath)).Entries;
            if (records.Length != 1) return;
            var phase = records[0].Phase;
            if (cut == "intent" && stage == WorkerLedgerIoStage.StateDirectorySynced && phase == WorkerRunPhase.PublicationIntent ||
                cut == "bundle" && stage == WorkerLedgerIoStage.BeforeStateWrite && phase == WorkerRunPhase.PublicationIntent && File.Exists(bundlePath) ||
                cut == "published-disk" && stage == WorkerLedgerIoStage.StateDirectorySynced && phase == WorkerRunPhase.Published)
                Crash();
        }
        var fs = new FileSystemManager(root, NullLogger<FileSystemManager>.Instance,
            PhysicalLoadTransactionOperations.Instance, new FileSystemManagerHooks
            {
                BeforeCanonicalMutationBoundaryAsync = path =>
                {
                    if (cut == "inbox" && path == GmWorkerAuditLog.AuditLogPath && File.Exists(inboxPath) && Record().Phase == WorkerRunPhase.Published) Crash();
                    return Task.CompletedTask;
                }
            });
        var hooks = new GmWorkerBridgePoolHooks
        {
            AfterOwnerBound = owner => execution = FenceField<GmWorkerDurableExecution>(owner, "_durable")!,
            BeforeWorkspaceCleanupAsync = _ => { if (cut == "live-publication") Crash(); return Task.CompletedTask; }
        };
        var reaper = new GmWorkerQuarantineReaper(1, retrySchedule: [], runInBackground: false);
        using var admission = new GmWorkerNativePoolAdmission(package, root, durable: true, Observe);
        var pool = new GmWorkerBridgePool(fs, null, new GmWorkerAuditLog(fs), hooks, GmWorkerProcessTreeFactory.Instance, reaper, admission);
        var result = await pool.RunTaskAsync(OwnershipProfile(package, output), task);
        throw new InvalidOperationException("Required actual publication crash cut was not reached: " + cut + "; " + result.Status.LastError);
    }
}
