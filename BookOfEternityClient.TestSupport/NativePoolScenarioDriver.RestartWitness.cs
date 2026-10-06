using System.Reflection;
using System.Text;
using System.Text.Json;
using BookOfEternityClient.Core;
using BookOfEternityClient.Services.GmWorkers;
using Microsoft.Extensions.Logging.Abstractions;

namespace BookOfEternityClient.Tests;

internal static partial class NativePoolScenarioDriver
{
    private sealed class WitnessSlot : IDisposable
    {
        internal int Disposals;
        public void Dispose() => Disposals++;
    }
    private static async Task<int> RunRestartWitness(string mode, string output)
    {
        var withholdOriginalTerminalAck = mode == "forged";
        var terminalFaults = 0;
        async Task<(GmWorkerDurableExecution Execution, GmWorkerQuarantinedExecution Owner,
            GmWorkerExecutionWorkspace Workspace, WitnessSlot Slot)> Original(string name)
        {
            var root = Path.Combine(output, name);
            await BootstrapRestartRoot(root);
            var fs = new FileSystemManager(root, NullLogger<FileSystemManager>.Instance);
            void Observe(WorkerLedgerIoStage stage)
            {
                var statePath = Path.Combine(root, ".boe_runtime", "worker-runs-v1", "state.json");
                if (name == "original" && withholdOriginalTerminalAck && stage == WorkerLedgerIoStage.StateDirectorySynced &&
                    File.Exists(statePath) && GmWorkerRunLedgerCodec.Decode(new(root), File.ReadAllBytes(statePath)).Retired.Length == 1)
                { terminalFaults++; throw new IOException("Synthetic original cleanup terminal ACK withheld."); }
            }
            var context = GmWorkerRootContext.Attach(fs, durable: true, Observe);
            var retained = context.Enter(); retained.RetainForCleanup();
            var task = GmWorkerBridgeTestFixtures.ValidationRepairTask();
            var workspace = GmWorkerExecutionWorkspace.PlanCreation(fs, task, null, Path.Combine(output, name + "-detached"));
            var execution = await context.PrepareAsync(retained, task, Encoding.UTF8.GetBytes(GmWorkerJson.Serialize(task)), workspace);
            await execution.EnsurePreparedAsync();
            await workspace.CreateRetainingAuthorityAsync(fs, task, CancellationToken.None);
            var slot = new WitnessSlot();
            var owner = new GmWorkerQuarantinedExecution(name, execution.Authority, null, null, workspace, slot, null,
                null, task.SessionGeneration, new WorkerAuditEvent(),
                () => throw new InvalidOperationException("Never-quarantined witness fixture must not append an audit."),
                _ => Task.CompletedTask, quarantined: false, durable: execution, rootLease: retained);
            return (execution, owner, workspace, slot);
        }
        var original = await Original("original");
        GmWorkerQuarantinedExecution.CleanupCompletion completion;
        var foreignCompleted = false;
        if (mode == "foreign")
        {
            var foreign = await Original("foreign");
            await foreign.Owner.ConfirmDeathAsync();
            await foreign.Owner.CleanupConfirmedAsync();
            completion = (GmWorkerQuarantinedExecution.CleanupCompletion)typeof(GmWorkerQuarantinedExecution)
                .GetField("_completion", BindingFlags.Instance | BindingFlags.NonPublic)!.GetValue(foreign.Owner)!;
            foreignCompleted = foreign.Execution.RetirementAcknowledged && foreign.Slot.Disposals == 1 &&
                !Directory.Exists(foreign.Workspace.GameSessionPath);
            if (!foreignCompleted) throw new InvalidOperationException("Foreign positive cleanup did not complete.");
        }
        else
        {
            await original.Owner.ConfirmDeathAsync();
            try { await original.Owner.CleanupConfirmedAsync(); }
            catch (IOException) when (terminalFaults > 0) { }
            var retained = (GmWorkerQuarantinedExecution.CleanupCompletion?)typeof(GmWorkerQuarantinedExecution)
                .GetField("_completion", BindingFlags.Instance | BindingFlags.NonPublic)!.GetValue(original.Owner);
            completion = new(original.Owner);
            if (terminalFaults == 0 || retained == null || retained.Facts != completion.Facts ||
                original.Execution.RetirementAcknowledged || original.Slot.Disposals != 0)
                throw new InvalidOperationException("Original retained completion was not frozen before ACK loss.");
            // All cleanup facts now hold; only private retained-instance identity
            // distinguishes the fabricated token. The original retry may ACK now.
            withholdOriginalTerminalAck = false;
        }
        var rootBefore = RestartSnapshot(Path.Combine(output, "original"));
        Exception? refusal = null;
        try { await original.Execution.RetireAsync(completion); }
        catch (InvalidOperationException error) { refusal = error; }
        await File.WriteAllTextAsync(Path.Combine(output, "restart-witness.json"), JsonSerializer.Serialize(new
        {
            mode, refused = refusal != null, foreignCompleted, terminalFaults,
            preserved = rootBefore.SequenceEqual(RestartSnapshot(Path.Combine(output, "original"))),
            workspaceRetained = Directory.Exists(original.Workspace.GameSessionPath),
            original.Slot.Disposals, original.Execution.RetirementAcknowledged
        }));
        // The negative probe does not destroy original cleanup authority.
        await original.Owner.ConfirmDeathAsync();
        await original.Owner.CleanupConfirmedAsync();
        if (original.Slot.Disposals != 1 || Directory.Exists(original.Workspace.GameSessionPath))
            throw new InvalidOperationException("Original witness fixture cleanup failed.");
        return 0;
    }
}
