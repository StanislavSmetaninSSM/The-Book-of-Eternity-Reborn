using System.Diagnostics;
using System.Text.Json;
using BookOfEternityClient.Core;
using BookOfEternityClient.Services.GmWorkers;
using Microsoft.Extensions.Logging.Abstractions;

namespace BookOfEternityClient.Tests;

internal static partial class NativePoolScenarioDriver
{
    private static WorkerBridgeProfile OwnershipProfile(string package, string output) =>
        GmWorkerBridgeTestFixtures.ValidationRepairCodexProfile() with
        {
            LaunchCommand = string.Join(" ", new[] { Path.Combine(Environment.GetEnvironmentVariable("DOTNET_ROOT")!, "dotnet"),
                typeof(NativePoolScenarioDriver).Assembly.Location, "pool-worker-content-valid", package, output }.Select(x => "\"" + x + "\"")),
            TimeoutSeconds = 15
        };
    private static WorkerTaskPacket OwnershipTask(string taskId) => GmWorkerBridgeTestFixtures.ValidationRepairTask() with
    {
        TaskId = taskId, TimeoutSeconds = 15,
        ContextFiles = [new WorkerFileReference { Path = RestartContextPath, Sha256 = GmWorkerRunLedgerCodec.Hash(RestartContextBytes) }]
    };
    private static async Task WaitOwnershipFile(string path)
    {
        var clock = Stopwatch.StartNew();
        while (!File.Exists(path))
        {
            if (clock.Elapsed > TimeSpan.FromSeconds(8)) throw new TimeoutException("Ownership fixture gate was not reached.");
            await Task.Delay(10);
        }
    }
    private static Process StartOwnershipChild(string kind, string stage, string package, string output)
    {
        var start = new ProcessStartInfo(Path.Combine(Environment.GetEnvironmentVariable("DOTNET_ROOT")!, "dotnet"))
        { UseShellExecute = false, RedirectStandardOutput = true, RedirectStandardError = true };
        foreach (var value in new[] { typeof(NativePoolScenarioDriver).Assembly.Location,
            "restart-ownership-child-" + kind + "-" + stage, package, output }) start.ArgumentList.Add(value);
        return Process.Start(start)!;
    }
    private static async Task FinishOwnershipChild(Process child, string log)
    {
        var stdout = child.StandardOutput.ReadToEndAsync(); var stderr = child.StandardError.ReadToEndAsync();
        await child.WaitForExitAsync().WaitAsync(TimeSpan.FromSeconds(15));
        await File.WriteAllTextAsync(log, await stdout + await stderr);
        if (child.ExitCode != 0) throw new InvalidOperationException("Ownership child fixture failed: " + child.ExitCode);
    }
    private static string[] RootDecisionSnapshot(string root)
    {
        var ledger = Path.Combine(root, ".boe_runtime", "worker-runs-v1");
        IEnumerable<string> entries = Directory.Exists(ledger) ? Directory.EnumerateFiles(ledger, "*", SearchOption.AllDirectories)
            .Where(p => Path.GetFileName(p) is not ("owner.lock" or "journal.lock"))
            .Select(p => Path.GetRelativePath(root, p) + ":" + GmWorkerRunLedgerCodec.Hash(File.ReadAllBytes(p))) : [];
        return RecoverySnapshot(root).Concat(entries).Order(StringComparer.Ordinal).ToArray();
    }
    private static async Task<int> RunRestartOwnership(string mode, string package, string output)
    {
        var root = Path.Combine(output, "state-copy");
        if (mode.StartsWith("child-", StringComparison.Ordinal))
        {
            var pieces = mode[6..].Split('-'); var kind = pieces[0]; var stage = pieces[1];
            if (stage == "race")
            {
                await File.WriteAllTextAsync(Path.Combine(output, "ready-" + kind), "ready");
                await WaitOwnershipFile(Path.Combine(output, "race-release"));
            }
            var fs = new FileSystemManager(root, NullLogger<FileSystemManager>.Instance);
            using var admission = new GmWorkerNativePoolAdmission(package, root, durable: kind == "durable");
            var reaper = new GmWorkerQuarantineReaper(1, retrySchedule: [], runInBackground: false);
            var slots = 0; var reservations = 0; var owners = 0;
            var hooks = new GmWorkerBridgePoolHooks
            {
                BeforeWorkerSlotWaitAsync = () =>
                {
                    slots++;
                    if (stage != "race") throw new InvalidOperationException("Refusal probe stops before new allocation.");
                    return Task.CompletedTask;
                },
                BeforeTaskReservationAsync = () => { reservations++; return Task.CompletedTask; },
                AfterOwnerBound = _ => owners++
            };
            var task = OwnershipTask("ownership_" + kind + "_" + stage);
            var pool = new GmWorkerBridgePool(fs, null, new GmWorkerAuditLog(fs), hooks, GmWorkerProcessTreeFactory.Instance, reaper, admission);
            GmWorkerTaskRunResult? result = null; Exception? error = null;
            try { result = await pool.RunTaskAsync(OwnershipProfile(package, output), task); }
            catch (Exception failure) { error = failure; }
            await File.WriteAllTextAsync(Path.Combine(output, "contender-" + kind + "-" + stage + ".json"), JsonSerializer.Serialize(new
            {
                kind, stage, slots, reservations, owners, success = result?.HasValidatedExecutionFor(task) == true,
                error = error?.GetType().Name, capacity = reaper.OwnedCapacity, entries = reaper.EntryCount
            }));
            return 0;
        }
        await BootstrapRestartRoot(root);
        if (mode == "race")
        {
            using var durable = StartOwnershipChild("durable", "race", package, output);
            using var legacy = StartOwnershipChild("legacy", "race", package, output);
            await Task.WhenAll(WaitOwnershipFile(Path.Combine(output, "ready-durable")), WaitOwnershipFile(Path.Combine(output, "ready-legacy")));
            await File.WriteAllTextAsync(Path.Combine(output, "race-release"), "go");
            await Task.WhenAll(FinishOwnershipChild(durable, Path.Combine(output, "durable-child.log")),
                FinishOwnershipChild(legacy, Path.Combine(output, "legacy-child.log")));
            return 0;
        }
        var order = mode.Split('-'); var firstDurable = order[0] == "durable"; var secondKind = order[1];
        var source = new FileSystemManager(root, NullLogger<FileSystemManager>.Instance);
        using var firstAdmission = new GmWorkerNativePoolAdmission(package, root, durable: firstDurable);
        var firstReaper = new GmWorkerQuarantineReaper(1, retrySchedule: [], runInBackground: false);
        var ready = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        var resume = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        var cleanupFault = true; var cleanupCalls = 0;
        var firstHooks = new GmWorkerBridgePoolHooks
        {
            BeforeWorkerReleaseAsync = async () => { ready.TrySetResult(); await resume.Task; },
            BeforeWorkspaceCleanupAsync = _ =>
            {
                cleanupCalls++;
                if (cleanupFault) throw new IOException("Synthetic original workspace cleanup remains pending.");
                return Task.CompletedTask;
            }
        };
        var firstTask = OwnershipTask("ownership_original");
        var firstPool = new GmWorkerBridgePool(source, null, new GmWorkerAuditLog(source), firstHooks,
            GmWorkerProcessTreeFactory.Instance, firstReaper, firstAdmission);
        var running = firstPool.RunTaskAsync(OwnershipProfile(package, output), firstTask);
        await ready.Task.WaitAsync(TimeSpan.FromSeconds(8));
        var activeBefore = RootDecisionSnapshot(root);
        bool activePreserved;
        try
        {
            using var contender = StartOwnershipChild(secondKind, "active", package, output);
            await FinishOwnershipChild(contender, Path.Combine(output, "active-child.log"));
            activePreserved = activeBefore.SequenceEqual(RootDecisionSnapshot(root));
        }
        finally { resume.TrySetResult(); }
        _ = await running;
        firstAdmission.Dispose();
        var retainedCapacity = firstReaper.OwnedCapacity;
        var retainedBefore = RootDecisionSnapshot(root);
        using (var contender = StartOwnershipChild(secondKind, "retained", package, output))
            await FinishOwnershipChild(contender, Path.Combine(output, "retained-child.log"));
        var retainedPreserved = retainedBefore.SequenceEqual(RootDecisionSnapshot(root));
        // A separate healthy legacy root remains usable while the first is quarantined.
        var otherOutput = Path.Combine(output, "other-positive"); Directory.CreateDirectory(otherOutput);
        var otherRoot = Path.Combine(otherOutput, "state-copy"); await BootstrapRestartRoot(otherRoot);
        var other = new FileSystemManager(otherRoot, NullLogger<FileSystemManager>.Instance);
        using var otherAdmission = new GmWorkerNativePoolAdmission(package, otherRoot);
        var otherReaper = new GmWorkerQuarantineReaper(1, retrySchedule: [], runInBackground: false);
        var otherTask = OwnershipTask("ownership_other");
        var otherPool = new GmWorkerBridgePool(other, null, new GmWorkerAuditLog(other), null, GmWorkerProcessTreeFactory.Instance, otherReaper, otherAdmission);
        var otherResult = await otherPool.RunTaskAsync(OwnershipProfile(package, otherOutput), otherTask);
        cleanupFault = false; await firstReaper.RunPassAsync();
        await File.WriteAllTextAsync(Path.Combine(output, "restart-ownership.json"), JsonSerializer.Serialize(new
        {
            mode, activePreserved, retainedPreserved, retainedCapacity, cleanupCalls,
            finalCapacity = firstReaper.OwnedCapacity, finalEntries = firstReaper.EntryCount,
            otherSuccess = otherResult.HasValidatedExecutionFor(otherTask), otherCapacity = otherReaper.OwnedCapacity,
            detachedRemaining = Directory.Exists(firstAdmission.RuntimeBase) && Directory.EnumerateDirectories(firstAdmission.RuntimeBase, "game_session", SearchOption.AllDirectories).Any()
        }));
        return 0;
    }
}
