using System.Runtime.InteropServices;
using System.Security.Cryptography;
using System.Text.Json;
using BookOfEternityClient.Services.GmWorkers;

namespace BookOfEternityClient.Tests;

// Finite metadata-only process fixture; no pool, provider, native worker or production Main wiring.
internal static class WorkerRunLedgerScenarioDriver
{
    internal static async Task<int> Run(string mode, string root, string output)
    {
        var target = new WorkerLedgerTarget(root);
        Environment.SetEnvironmentVariable(GmWorkerBridgePool.WorkerRuntimeBaseEnvironmentVariable, Path.Combine(output, "changed-runtime-base"));
        if (mode == "ledger-observe")
        {
            var observed = await GmWorkerRunLedger.ObserveAsync(target);
            var coldDenied = true;
            if (observed.Kind is WorkerRunObservationKind.Blocked or WorkerRunObservationKind.Uncertain)
            { await using var cold = await GmWorkerRunLedger.OpenCoordinatorAsync(target); coldDenied = cold is null; }
            File.WriteAllText(Path.Combine(output, "observation.json"), JsonSerializer.Serialize(new
            { kind = observed.Kind.ToString(), observed.Sequence, observed.EpochHighWater, phases = observed.Entries.Select(item => item.Phase.ToString()), coldDenied,
                runtimeBaseCreated = Directory.Exists(Path.Combine(output, "changed-runtime-base")) }));
            return 0;
        }
        if (mode == "ledger-probe")
        {
            var inherited = Directory.EnumerateFileSystemEntries("/proc/self/fd").Select(path => new FileInfo(path).LinkTarget)
                .Where(path => path is not null && path.StartsWith(target.DirectoryPath + Path.DirectorySeparatorChar, StringComparison.Ordinal)).ToArray();
            await using var contender = await GmWorkerRunLedger.OpenCoordinatorAsync(target);
            File.WriteAllText(Path.Combine(output, "probe.json"), JsonSerializer.Serialize(new { opened = contender is not null, inherited }));
            return 0;
        }
        if (mode != "ledger-write") return 64;
        using var request = JsonDocument.Parse(File.ReadAllBytes(Path.Combine(output, "request.json")));
        var operation = request.RootElement.GetProperty("operation").GetString();
        var cut = request.RootElement.GetProperty("cut").GetString();
        var armed = operation == "bootstrap";
        Action<WorkerLedgerIoStage> observer = stage =>
        {
            if (!armed || stage.ToString() != cut) return;
            File.WriteAllText(Path.Combine(output, "cut.json"), JsonSerializer.Serialize(new { stage = stage.ToString(), exitCode = 77 }));
            ExitImmediately(77); // Genuine abrupt exit: no Dispose/finally or managed flushing.
        };
        await using var owner = await GmWorkerRunLedger.OpenCoordinatorAsync(target, observeIo: observer);
        if (owner is null) return 71;
        WorkerRunEntryHandle? entry = null;
        if (operation != "initialize" && await owner.InitializeAsync() != WorkerLedgerMutationKind.Applied) return 72;
        var preparation = new WorkerRunPreparation("22222222222222222222222222222222", "worker", "task", new string('a', 64),
            WorkerRunBackend.LinuxNativeLineage, WorkerRunScope.OrdinarySamePidNamespace, Path.Combine(output, "workspace"));
        if (operation is "launch" or "uncertain" or "abort")
        {
            var prepared = await owner.PrepareAsync(preparation, owner.Sequence);
            if (prepared.Kind != WorkerLedgerMutationKind.Applied) return 73;
            entry = prepared.Entry;
        }
        var state = Path.Combine(target.DirectoryPath, "state.json");
        if (File.Exists(state)) File.Copy(state, Path.Combine(output, "before.bin"));
        armed = true;
        var result = operation switch
        {
            "initialize" => await owner.InitializeAsync(),
            "prepare" => (await owner.PrepareAsync(preparation, owner.Sequence)).Kind,
            "launch" => await owner.PlanLaunchAsync(entry!, owner.Sequence),
            "uncertain" => await owner.MarkUncertainAsync(entry!, owner.Sequence),
            "abort" => await owner.AbortBeforeLaunchAsync(entry!, owner.Sequence),
            _ => WorkerLedgerMutationKind.Blocked
        };
        File.WriteAllText(Path.Combine(output, "write.json"), JsonSerializer.Serialize(new { result = result.ToString(), owner.Sequence,
            stateSha256 = File.Exists(state) ? Convert.ToHexString(SHA256.HashData(File.ReadAllBytes(state))).ToLowerInvariant() : null }));
        return result == WorkerLedgerMutationKind.Applied && cut is null ? 0 : 74;
    }

    [DllImport("libc", EntryPoint = "_exit")] private static extern void ExitImmediately(int status);
}
