using System.Text;
using System.Text.Json;
using BookOfEternityClient.Core;
using BookOfEternityClient.Services.GmWorkers;
using Microsoft.Extensions.Logging.Abstractions;

namespace BookOfEternityClient.Tests;

internal static partial class NativePoolScenarioDriver
{
    private static async Task<int> RunRestartPurpose(string mode, string output)
    {
        var root = Path.Combine(output, "state-copy");
        var otherRoot = Path.Combine(output, "foreign-copy");
        await BootstrapRestartRoot(root); await BootstrapRestartRoot(otherRoot);
        var fs = new FileSystemManager(root, NullLogger<FileSystemManager>.Instance);
        var other = new FileSystemManager(otherRoot, NullLogger<FileSystemManager>.Instance);
        var context = GmWorkerRootContext.Attach(fs, durable: true, observer: null);
        using var original = context.Enter();
        var task = GmWorkerBridgeTestFixtures.ValidationRepairTask();
        var bytes = Encoding.UTF8.GetBytes(GmWorkerJson.Serialize(task));
        GmWorkerCanonicalPurpose purpose;
        GmWorkerDispatchAdmission? dispatch = null;
        WorkerAuditEvent? audit = null;
        if (mode == "cleanup")
        {
            var workspace = GmWorkerExecutionWorkspace.PlanCreation(fs, task, null, Path.Combine(output, "detached"));
            var execution = await context.PrepareAsync(original, task, bytes, workspace);
            await execution.EnsurePreparedAsync();
            audit = new WorkerAuditEvent { EventId = "original-audit", EventType = "process-tree-cleanup-confirmed",
                WorkerId = task.WorkerId, TaskId = task.TaskId, TimestampUtc = DateTimeOffset.UtcNow.ToString("O") };
            execution.BindCleanupAudit(audit); execution.MarkCleanupDeferred();
            purpose = execution.CleanupPurpose(audit);
        }
        else
        {
            dispatch = context.CreateDispatch(original, task, bytes);
            purpose = dispatch.ColdPurpose;
            if (mode == "reservation")
            {
                await using var cold = await fs.AcquireCanonicalWriteLeaseAsync(workerPurpose: purpose);
                dispatch.CompleteColdAdmission(fs, cold);
                purpose = dispatch.ReservationPurpose;
            }
        }
        await using (var seed = await other.AcquireCanonicalWriteLeaseAsync())
        {
            var installed = false;
            try
            {
                new TrustedLocalFilePublication(other, new TrustedLocalFileScope([otherRoot])).Publish(seed,
                    TrustedLocalGeneration.Existing(task.SessionGeneration),
                    [new(other.ResolvePath(RestartContextPath), RestartContextBytes, Encoding.UTF8.GetBytes("foreign interrupted member"))],
                    (phase, _) =>
                    {
                        if (phase == TrustedLocalPublicationPhase.MemberPublished)
                        { installed = true; throw new IOException("Foreign original recovery evidence."); }
                    });
            }
            catch (IOException) when (installed) { }
            if (!installed) throw new InvalidOperationException("Foreign fixture failed to install recovery evidence.");
        }
        var before = RecoverySnapshot(otherRoot);
        var sourceBefore = RestartSnapshot(root);
        Exception? refusal = null;
        try
        {
            await using var foreign = await other.AcquireCanonicalWriteLeaseAsync(workerPurpose: purpose);
            if (mode == "cold") dispatch!.CompleteColdAdmission(other, foreign);
            else if (mode == "reservation")
            {
                await other.CompareExchangeFileBytesAsync(foreign, GmWorkerBridgePool.GetTaskPacketPath(task.TaskId), null, bytes);
                await dispatch!.CompleteReservationAsync(other, foreign);
            }
            else await other.AppendFileAtomicAsync(foreign, GmWorkerAuditLog.AuditLogPath, GmWorkerAuditLog.SerializeAppend(audit!));
        }
        catch (Exception error) { refusal = error; }
        await File.WriteAllTextAsync(Path.Combine(output, "restart-purpose.json"), JsonSerializer.Serialize(new
        {
            mode, refused = refusal != null, error = refusal?.GetType().Name,
            foreignPreserved = before.SequenceEqual(RecoverySnapshot(otherRoot)), sourcePreserved = sourceBefore.SequenceEqual(RestartSnapshot(root))
        }));
        dispatch?.Dispose();
        return 0;
    }
}
