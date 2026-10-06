using System.Text;
using System.Text.Json;
using BookOfEternityClient.Core;
using BookOfEternityClient.Services.GmWorkers;
using Microsoft.Extensions.Logging.Abstractions;

namespace BookOfEternityClient.Tests;

internal static partial class NativePoolScenarioDriver
{
    private static async Task<int> RunRestartAudit(string mode, string output)
    {
        var root = Path.Combine(output, "state-copy");
        await BootstrapRestartRoot(root);
        GmWorkerRootContext? context = null;
        var hooks = new FileSystemManagerHooks
        {
            AfterCanonicalMutationBoundaryValidatedAsync = async _ =>
            {
                await Task.Yield();
                if (mode == "held-closure") context!.CloseForUncertainty();
            }
        };
        var fs = new FileSystemManager(root, NullLogger<FileSystemManager>.Instance,
            PhysicalLoadTransactionOperations.Instance, hooks);
        var empty = "game_state/control/audit-empty";
        Directory.CreateDirectory(fs.ResolvePath(empty));
        context = GmWorkerRootContext.Attach(fs, durable: true, observer: null);
        using var retained = context.Enter();
        var task = GmWorkerBridgeTestFixtures.ValidationRepairTask();
        var execution = await context.PrepareAsync(retained, task,
            Encoding.UTF8.GetBytes(GmWorkerJson.Serialize(task)), Path.Combine(output, "never-started-workspace"));
        await execution.EnsurePreparedAsync();
        var audit = new WorkerAuditEvent
        {
            EventId = "original-cleanup-event", EventType = "process-tree-cleanup-confirmed",
            WorkerId = task.WorkerId, TaskId = task.TaskId, TimestampUtc = DateTimeOffset.UtcNow.ToString("O"),
            Summary = "Original never-Start cleanup fixture"
        };
        execution.BindCleanupAudit(audit);
        execution.MarkCleanupDeferred();
        var line = JsonSerializer.Serialize(audit, new JsonSerializerOptions(GmWorkerJson.Options) { WriteIndented = false }) + Environment.NewLine;
        var suffix = Encoding.UTF8.GetBytes(line);
        var before = RecoverySnapshot(root);
        var directories = Directory.EnumerateDirectories(fs.GameSessionPath, "*", SearchOption.AllDirectories).Order().ToArray();
        Exception? refusal = null;
        bool appendedTwice = false;
        try
        {
            if (mode == "exact-once")
            {
                var log = new GmWorkerAuditLog(fs);
                appendedTwice = await log.AppendRequiredEventOnceIfCurrentSessionAsync(task.SessionGeneration, audit,
                    durableExecution: execution) == GmWorkerAuditAppendDisposition.Appended &&
                    await log.AppendRequiredEventOnceIfCurrentSessionAsync(task.SessionGeneration, audit,
                    durableExecution: execution) == GmWorkerAuditAppendDisposition.Appended;
            }
            else
            {
                await using var lease = await fs.AcquireCanonicalWriteLeaseAsync(workerPurpose: execution.CleanupPurpose(audit));
                var next = Encoding.UTF8.GetPreamble().Concat(suffix).ToArray();
                switch (mode)
                {
                    case "wrong-path": await fs.AppendFileAtomicAsync(lease, "unrelated/new-parent/payload.txt", line); break;
                    case "wrong-bytes": await fs.AppendFileAtomicAsync(lease, GmWorkerAuditLog.AuditLogPath, line + "unexpected"); break;
                    case "replace-audit": await fs.WriteFileAtomicBytesAsync(lease, GmWorkerAuditLog.AuditLogPath, next); break;
                    case "extra-member": await fs.PublishLocalFilesAsync(lease,
                        [new(GmWorkerAuditLog.AuditLogPath, null, next), new("unrelated/new-parent/payload.txt", null, suffix)]); break;
                    case "empty-prune": fs.DeleteDirectoryTree(lease, empty); break;
                    case "direct-publish": new TrustedLocalFilePublication(fs, new TrustedLocalFileScope([root])).Publish(lease,
                        TrustedLocalGeneration.Existing(task.SessionGeneration),
                        [new(fs.ResolvePath(RestartContextPath), RestartContextBytes, suffix)]); break;
                    case "legacy-append": lease.IsLegacyStorageRecovery = true; await fs.AppendFileAtomicAsync(lease, GmWorkerAuditLog.AuditLogPath, line); break;
                    case "held-closure": await fs.AppendFileAtomicAsync(lease, GmWorkerAuditLog.AuditLogPath, line); break;
                    case "cas": await fs.CompareExchangeFileBytesAsync(lease, RestartContextPath, RestartContextBytes, suffix); break;
                    case "delete": fs.DeleteFile(lease, RestartContextPath); break;
                    case "backup": fs.CreateBackup(lease, RestartContextPath); break;
                    case "worker-apply": await fs.BeginWorkerApplyTransactionAsync(lease,
                        [new(RestartContextPath, RestartContextBytes, suffix)]); break;
                    case "generation": fs.RotateSessionGeneration(lease); break;
                    case "directory-structure": fs.EnsureDirectoryStructure(lease); break;
                    default: throw new ArgumentException("Unknown audit fixture mode.");
                }
            }
        }
        catch (Exception error) { refusal = error; }
        var path = fs.ResolvePath(GmWorkerAuditLog.AuditLogPath);
        var exactBytes = Encoding.UTF8.GetPreamble().Concat(suffix).ToArray();
        await File.WriteAllTextAsync(Path.Combine(output, "restart-audit.json"), JsonSerializer.Serialize(new
        {
            mode, refused = refusal != null, error = refusal?.GetType().Name, message = refusal?.Message,
            appendedTwice, exactOnce = File.Exists(path) && File.ReadAllBytes(path).SequenceEqual(exactBytes),
            preserved = before.SequenceEqual(RecoverySnapshot(root)) && directories.SequenceEqual(
                Directory.EnumerateDirectories(fs.GameSessionPath, "*", SearchOption.AllDirectories).Order()),
            retained = (await GmWorkerRunLedger.ObserveAsync(new(root))).Entries.Count == 1
        }));
        // No helper/worker was started. Keep the unresolved original root until
        // this isolated driver exits; the independent guardian observes ECHILD.
        return 0;
    }
}
