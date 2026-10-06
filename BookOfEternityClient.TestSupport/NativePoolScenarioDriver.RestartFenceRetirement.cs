using System.Text;
using System.Text.Json;
using BookOfEternityClient.Core;
using BookOfEternityClient.Services.GmWorkers;
using Microsoft.Extensions.Logging.Abstractions;

namespace BookOfEternityClient.Tests;

internal static partial class NativePoolScenarioDriver
{
    private static async Task<int> SeedRestartFenceRetirement(string cut, string package, string output)
    {
        var root = Path.Combine(output, "state-copy");
        await BootstrapRestartRoot(root);
        var fs = new FileSystemManager(root, NullLogger<FileSystemManager>.Instance);
        var task = OwnershipTask("restart_r3_original");
        var ledger = Path.Combine(root, ".boe_runtime", "worker-runs-v1");
        var statePath = Path.Combine(ledger, "state.json");
        GmWorkerDurableExecution? execution = null;
        GmWorkerTaskRunResult? result = null;
        var reaper = new GmWorkerQuarantineReaper(1, retrySchedule: [], runInBackground: false);
        var failures = 0; var attempts = 0; var retry = false;
        void Crash()
        {
            if (++attempts != 1) throw new InvalidOperationException("A later retirement retry is not the original cut.");
            var state = GmWorkerRunLedgerCodec.Decode(new(root), File.ReadAllBytes(statePath));
            var live = FenceField<WorkerRunRecord>(execution!, "_record")!;
            var archivePath = Path.Combine(ledger, "retired", live.Identity.RunId + ".json");
            var archive = File.Exists(archivePath) ? GmWorkerRunRecordCodec.Decode(File.ReadAllBytes(archivePath)) : null;
            var originalAudit = FenceField<WorkerRunCleanup>(execution!, "_boundAudit")!;
            var events = File.ReadAllLines(fs.ResolvePath(GmWorkerAuditLog.AuditLogPath))
                .Select(line => GmWorkerJson.Deserialize<WorkerAuditEvent>(line)!).Where(e => e.EventId == originalAudit.AuditEventId).ToArray();
            File.WriteAllText(Path.Combine(output, "fence-cut.json"), JsonSerializer.Serialize(new
            {
                cut, cutAttempts = attempts, failures, active = state.Entries.Length, retired = state.Retired.Length,
                diskPhase = state.Entries.SingleOrDefault()?.Phase.ToString(), livePhase = live.Phase.ToString(),
                retirementAcknowledged = FenceField<bool>(execution!, "_retirementAcknowledged"),
                publicationAcknowledged = FenceField<bool>(execution!, "_publicationAcknowledged"),
                originalAccepted = result!.HasValidatedExecutionFor(task), result.Status.CleanupDeferred,
                cleanup = (archive ?? state.Entries.Single()).Progress!.Cleanup,
                originalAudit, auditCount = events.Length,
                auditMatches = events.Length == 1 && GmWorkerDurableExecution.AuditFacts(events[0]) == originalAudit,
                archivePhase = archive?.Phase.ToString(), archiveHash = File.Exists(archivePath) ? GmWorkerRunLedgerCodec.Hash(File.ReadAllBytes(archivePath)) : null,
                live.Identity, capacity = reaper.OwnedCapacity, entries = reaper.EntryCount,
                workerStarts = FenceWorkerStarts(output), workspaceExists = Directory.Exists(live.Identity.WorkspacePath),
                runtimeRetained = FenceField<GmWorkerExecutionWorkspace>(FenceField<object>(execution!, "_cleanupOwner")!, "_workspace") != null,
                slotRetained = FenceField<IDisposable>(FenceField<object>(execution!, "_cleanupOwner")!, "_workerSlot") != null,
                cutFilesExcludingLockDescriptors = FenceCutSnapshot(root)
            }));
            ExitRestartImmediately(77);
        }
        void Observe(WorkerLedgerIoStage stage)
        {
            if (!retry || !File.Exists(statePath)) return;
            var state = GmWorkerRunLedgerCodec.Decode(new(root), File.ReadAllBytes(statePath));
            if (cut == "cleanup-pending" && stage == WorkerLedgerIoStage.StateDirectorySynced && state.Entries.Any(e => e.Phase == WorkerRunPhase.CleanupPending) ||
                cut == "archive" && stage == WorkerLedgerIoStage.ArchiveDirectorySynced ||
                cut == "terminal-disk" && stage == WorkerLedgerIoStage.StateDirectorySynced && state.Entries.Length == 0 && state.Retired.Length == 1) Crash();
        }
        var hooks = new GmWorkerBridgePoolHooks
        {
            AfterOwnerBound = owner => execution = FenceField<GmWorkerDurableExecution>(owner, "_durable")!,
            BeforeWorkspaceCleanupAsync = _ =>
            {
                if (failures == 0) { failures++; throw new IOException("Synthetic one-shot original cleanup failure for conditional audit."); }
                return Task.CompletedTask;
            },
            AfterRetirementAcknowledged = () => { if (retry && cut == "terminal-ack") Crash(); }
        };
        using var admission = new GmWorkerNativePoolAdmission(package, root, durable: true, Observe);
        var pool = new GmWorkerBridgePool(fs, null, new GmWorkerAuditLog(fs), hooks, GmWorkerProcessTreeFactory.Instance, reaper, admission);
        result = await pool.RunTaskAsync(OwnershipProfile(package, output), task);
        if (!result.HasValidatedExecutionFor(task) || !result.Status.CleanupDeferred || reaper.EntryCount != 1 || reaper.OwnedCapacity != 1)
            throw new InvalidOperationException("Original publication and quarantined cleanup prerequisite not reached.");
        retry = true;
        await reaper.RunPassAsync();
        if (cut == "capacity-released" && reaper.EntryCount == 0 && reaper.OwnedCapacity == 0) Crash();
        throw new InvalidOperationException("Required actual retirement cut was not reached: " + cut);
    }

    private static async Task<int> ProbeRestartFenceRetired(string package, string output)
    {
        var root = Path.Combine(output, "state-copy");
        var fs = new FileSystemManager(root, NullLogger<FileSystemManager>.Instance);
        var oldTask = OwnershipTask("restart_r3_original");
        var ledger = Path.Combine(root, ".boe_runtime", "worker-runs-v1");
        var initial = GmWorkerRunLedgerCodec.Decode(new(root), File.ReadAllBytes(Path.Combine(ledger, "state.json")));
        var oldReference = initial.Retired.Single();
        var pins = new[] { fs.ResolvePath(GmWorkerBridgePool.GetTaskPacketPath(oldTask.TaskId)),
            fs.ResolvePath(GmWorkerBridgePool.GetProposalInboxPath(oldTask.TaskId)), Path.Combine(ledger, "retired", oldReference.RunId + ".json") }
            .Concat(Directory.GetFiles(Path.GetDirectoryName(fs.ResolvePath(GmWorkerProposalStore.GetProposalPath("worker_proposal_" + oldTask.TaskId)))!, "*", SearchOption.AllDirectories))
            .ToDictionary(path => path, File.ReadAllBytes);
        var auditPath = fs.ResolvePath(GmWorkerAuditLog.AuditLogPath);
        var oldAudit = File.ReadAllBytes(auditPath);
        var before = RestartSnapshot(root);
        var slots = 0; var owners = 0; var releases = 0;
        var hooks = new GmWorkerBridgePoolHooks
        {
            BeforeWorkerSlotWaitAsync = () => { slots++; return Task.CompletedTask; },
            AfterOwnerBound = _ => owners++, BeforeWorkerReleaseAsync = () => { releases++; return Task.CompletedTask; }
        };
        var reaper = new GmWorkerQuarantineReaper(1, retrySchedule: [], runInBackground: false);
        using var admission = new GmWorkerNativePoolAdmission(package, root, durable: true);
        var pool = new GmWorkerBridgePool(fs, null, new GmWorkerAuditLog(fs), hooks, GmWorkerProcessTreeFactory.Instance, reaper, admission);
        var oldResult = await pool.RunTaskAsync(OwnershipProfile(package, output), oldTask);
        var oldPreserved = before.SequenceEqual(RestartSnapshot(root));
        var oldSlots = slots; var oldOwners = owners; var oldReleases = releases;
        var newTask = OwnershipTask("restart_r3_distinct_after_retirement");
        var newResult = await pool.RunTaskAsync(OwnershipProfile(package, output), newTask);
        var final = GmWorkerRunLedgerCodec.Decode(new(root), File.ReadAllBytes(Path.Combine(ledger, "state.json")));
        File.WriteAllText(Path.Combine(output, "fence-retired-probe.json"), JsonSerializer.Serialize(new
        {
            oldAccepted = oldResult.HasValidatedExecutionFor(oldTask), oldProposalConsumer = GmWorkerProposalOnlyDispatchService.CanAcceptExecution(oldResult, oldTask),
            oldRepairConsumer = GmWorkerValidationRepairDelegator.CanAcceptExecution(oldResult, oldTask), oldPreserved, oldSlots, oldOwners, oldReleases,
            newAccepted = newResult.HasValidatedExecutionFor(newTask), newProposalConsumer = GmWorkerProposalOnlyDispatchService.CanAcceptExecution(newResult, newTask),
            newRepairConsumer = GmWorkerValidationRepairDelegator.CanAcceptExecution(newResult, newTask),
            slots, owners, releases, workerStarts = FenceWorkerStarts(output), active = final.Entries.Length, retired = final.Retired.Length,
            capacity = reaper.OwnedCapacity, entries = reaper.EntryCount,
            oldBytesPreserved = pins.All(p => File.Exists(p.Key) && p.Value.SequenceEqual(File.ReadAllBytes(p.Key))),
            oldTombstonePreserved = final.Retired.Contains(oldReference), auditPrefixPreserved = File.ReadAllBytes(auditPath).AsSpan().StartsWith(oldAudit)
        }));
        return 0;
    }
}
