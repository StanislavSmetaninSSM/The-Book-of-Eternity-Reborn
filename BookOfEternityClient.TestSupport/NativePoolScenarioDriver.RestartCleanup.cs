using System.Reflection;
using System.Text;
using System.Text.Json;
using BookOfEternityClient.Core;
using BookOfEternityClient.Services.GmWorkers;
using Microsoft.Extensions.Logging.Abstractions;

namespace BookOfEternityClient.Tests;

internal static partial class NativePoolScenarioDriver
{
    private static async Task<int> RunRestartCleanup(string mode, string output)
    {
        var root = Path.Combine(output, "state-copy");
        await BootstrapRestartRoot(root);
        var fs = new FileSystemManager(root, NullLogger<FileSystemManager>.Instance);
        var fault = false;
        void Observe(WorkerLedgerIoStage stage)
        {
            if (fault && stage == WorkerLedgerIoStage.StateDirectorySynced)
                throw new IOException("Synthetic live-plan ACK withheld.");
        }
        var context = GmWorkerRootContext.Attach(fs, durable: true, Observe);
        using var retained = context.Enter();
        var task = GmWorkerBridgeTestFixtures.ValidationRepairTask() with
        { ContextFiles = [new WorkerFileReference { Path = RestartContextPath, Sha256 = GmWorkerRunLedgerCodec.Hash(RestartContextBytes) }] };
        var workspace = GmWorkerExecutionWorkspace.PlanCreation(fs, task, hooks: null, Path.Combine(output, "detached"));
        var execution = await context.PrepareAsync(retained, task,
            Encoding.UTF8.GetBytes(GmWorkerJson.Serialize(task)), workspace.GameSessionPath);
        await execution.EnsurePreparedAsync();
        await workspace.CreateRetainingAuthorityAsync(fs, task, CancellationToken.None);
        var coordinator = (WorkerRunLedgerCoordinator)typeof(GmWorkerDurableExecution)
            .GetField("_coordinator", BindingFlags.NonPublic | BindingFlags.Instance)!.GetValue(execution)!;
        if (mode == "r1-live-retry")
        {
            fault = true;
            try { await execution.PlanLaunchAsync(); }
            catch (IOException) { }
            fault = false;
        }
        var before = RestartSnapshot(root);
        WorkerLedgerMutationKind? outcome = null; Exception? refusal = null;
        try
        {
            switch (mode)
            {
                case "r1-abort": outcome = await coordinator.AbortBeforeLaunchAsync(execution.Entry, coordinator.Sequence); break;
                case "r1-start": outcome = await coordinator.PlanLaunchAsync(execution.Entry, coordinator.Sequence); break;
                case "r1-live-retry": outcome = (await coordinator.RetryPendingAsync()).Kind; break;
                case "premature-retire":
                    // The baseline accepts bool/DTO. Its replacement must require
                    // original completed cleanup evidence; absence never permits it.
                    var retire = typeof(GmWorkerDurableExecution).GetMethod("RetireAsync", BindingFlags.NonPublic | BindingFlags.Instance)!;
                    var arguments = retire.GetParameters().Length == 2
                        ? new object?[] { false, new WorkerAuditEvent() } : new object?[] { null };
                    await (Task)retire.Invoke(execution, arguments)!;
                    break;
                default: throw new ArgumentException("Unknown cleanup fixture mode.");
            }
        }
        catch (Exception error) { refusal = error; }
        await File.WriteAllTextAsync(Path.Combine(output, "restart-cleanup.json"), JsonSerializer.Serialize(new
        {
            mode, refused = refusal != null || outcome == WorkerLedgerMutationKind.Blocked,
            outcome = outcome?.ToString(), error = refusal?.GetType().Name,
            preserved = before.SequenceEqual(RestartSnapshot(root)), workspaceRetained = Directory.Exists(workspace.GameSessionPath)
        }));
        return 0;
    }
}
