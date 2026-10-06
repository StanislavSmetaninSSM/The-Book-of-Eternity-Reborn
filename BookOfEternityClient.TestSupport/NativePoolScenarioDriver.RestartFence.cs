using System.Reflection;
using System.Text.Json;
using BookOfEternityClient.Core;
using BookOfEternityClient.Services.GmWorkers;
using Microsoft.Extensions.Logging.Abstractions;

namespace BookOfEternityClient.Tests;

internal static partial class NativePoolScenarioDriver
{
    private static async Task<int> RunRestartFence(string mode, string package, string output)
    {
        if (mode == "probe-denied") return await ProbeRestartFence(package, output);
        if (mode.StartsWith("seed-launch-", StringComparison.Ordinal))
            return await SeedRestartFenceLaunch(mode[12..], package, output);
        if (mode.StartsWith("seed-publication-", StringComparison.Ordinal))
            return await SeedRestartFencePublication(mode[17..], package, output);
        if (mode.StartsWith("seed-retirement-", StringComparison.Ordinal))
            return await SeedRestartFenceRetirement(mode[16..], package, output);
        if (mode == "probe-retired") return await ProbeRestartFenceRetired(package, output);
        if (mode.StartsWith("race-", StringComparison.Ordinal)) return await RunRestartFenceRace(mode[5..], package, output);
        if (mode.StartsWith("cleanup-loss-", StringComparison.Ordinal)) return await RunRestartFenceCleanupLoss(mode[13..], package, output);
        return 64;
    }
    private static T? FenceField<T>(object instance, string field) =>
        (T?)instance.GetType().GetField(field, BindingFlags.Instance | BindingFlags.NonPublic)!.GetValue(instance);
    // Original canonical/ledger lock descriptors may intentionally exclude readers
    // at the cut. Their bytes carry no outcome; post-guardian cold snapshots remain full.
    private static string[] FenceCutSnapshot(string root) => Directory.EnumerateFiles(root, "*", SearchOption.AllDirectories)
        .Where(path => !path.EndsWith(".lock", StringComparison.Ordinal))
        .Order(StringComparer.Ordinal).Select(path => Path.GetRelativePath(root, path) + ":" + GmWorkerRunLedgerCodec.Hash(File.ReadAllBytes(path))).ToArray();
    private static int FenceWorkerStarts(string output) => File.Exists(Path.Combine(output, "worker-starts"))
        ? File.ReadAllLines(Path.Combine(output, "worker-starts")).Length : 0;

    private static async Task<int> SeedRestartFenceLaunch(string cut, string package, string output)
    {
        var root = Path.Combine(output, "state-copy");
        await BootstrapRestartRoot(root);
        var fs = new FileSystemManager(root, NullLogger<FileSystemManager>.Instance);
        var task = OwnershipTask("restart_r3_original");
        var profile = OwnershipProfile(package, output);
        if (cut == "released-ack") profile = profile with
        { LaunchCommand = profile.LaunchCommand.Replace("pool-worker-content-valid", "pool-worker-content-held", StringComparison.Ordinal) };
        var statePath = Path.Combine(root, ".boe_runtime", "worker-runs-v1", "state.json");
        GmWorkerNativeLineageLaunch? owner = null;
        GmWorkerDurableExecution? execution = null;
        ReleaseCountingStream? frames = null;
        Task<int>? completion = null; int? originalCompletionExit = null;
        var cutAttempts = 0;
        void Crash()
        {
            if (++cutAttempts != 1) throw new InvalidOperationException("The first crash observation failed; a later retry is not the requested cut.");
            var state = GmWorkerRunLedgerCodec.Decode(new(root), File.ReadAllBytes(statePath));
            var record = state.Entries.Single();
            var taskBytes = File.ReadAllBytes(fs.ResolvePath(GmWorkerBridgePool.GetTaskPacketPath(task.TaskId)));
            var authority = execution?.Authority;
            File.WriteAllText(Path.Combine(output, "fence-cut.json"), JsonSerializer.Serialize(new
            {
                cut, cutAttempts, diskPhase = record.Phase.ToString(), livePhase = execution == null ? null : FenceField<WorkerRunRecord>(execution, "_record")?.Phase.ToString(),
                record.Identity, ownerBound = owner != null,
                originalTaskBound = record.Identity.TaskId == task.TaskId && record.Identity.TaskSha256 == GmWorkerRunLedgerCodec.Hash(taskBytes),
                releaseFrames = frames?.ReleaseFrames ?? 0, workerStarts = FenceWorkerStarts(output),
                completionTaskCompleted = completion?.IsCompleted, originalCompletionExit,
                recordedCompletion = authority == null ? null : FenceField<int?>(authority, "_completion"),
                stop = authority?.StopEvidence?.State.ToString(), outputsSettled = authority?.OutputsSettled == true,
                workspaceExists = Directory.Exists(record.Identity.WorkspacePath), cutFilesExcludingLockDescriptors = FenceCutSnapshot(root)
            }));
            ExitRestartImmediately(77);
        }
        void Observe(WorkerLedgerIoStage stage)
        {
            if (cut == "prepared" && stage == WorkerLedgerIoStage.BeforeLiveRegistration) { Crash(); return; }
            if (stage != WorkerLedgerIoStage.StateDirectorySynced || !File.Exists(statePath)) return;
            var state = GmWorkerRunLedgerCodec.Decode(new(root), File.ReadAllBytes(statePath));
            if (state.Entries.Length != 1) return;
            if (cut == "release-intent" && state.Entries[0].Phase == WorkerRunPhase.ReleaseIntent ||
                cut == "outputs-stop-record" && state.Entries[0].Phase == WorkerRunPhase.StopValidated) Crash();
        }
        var hooks = new GmWorkerBridgePoolHooks
        {
            AfterOwnerBound = actual =>
            { owner = (GmWorkerNativeLineageLaunch)actual; execution = FenceField<GmWorkerDurableExecution>(owner, "_durable")!; },
            AfterHostPrepared = host =>
            {
                var stream = FenceField<Stream>(host, "_controlPipe")!;
                frames = new(stream, FenceField<string>(host, "_launchNonce")!);
                typeof(GmWorkerProcessHostLaunch).GetField("_controlChannel", BindingFlags.Instance | BindingFlags.NonPublic)!
                    .SetValue(host, new GmWorkerProcessHostFrameChannel(frames));
                if (cut == "helper") Crash();
            },
            AfterWorkerReleaseAsync = async () =>
            {
                await WaitOwnershipFile(Path.Combine(output, "worker-starts"));
                if (cut == "release-sent") Crash();
            },
            BeforeCompletionArbitrationAsync = async original =>
            {
                completion = original;
                if (cut == "released-ack")
                {
                    if (original.IsCompleted) throw new InvalidOperationException("Released ACK cut missed its held worker boundary.");
                    Crash();
                }
                if (cut == "completed")
                { originalCompletionExit = await original.WaitAsync(TimeSpan.FromSeconds(8)); Crash(); }
            },
            AfterScopedStop = () => { if (cut == "scoped-stop") Crash(); }
        };
        var reaper = new GmWorkerQuarantineReaper(1, retrySchedule: [], runInBackground: false);
        using var admission = new GmWorkerNativePoolAdmission(package, root, durable: true, Observe);
        var pool = new GmWorkerBridgePool(fs, null, new GmWorkerAuditLog(fs), hooks, GmWorkerProcessTreeFactory.Instance, reaper, admission);
        var result = await pool.RunTaskAsync(profile, task);
        throw new InvalidOperationException("Required actual launch crash cut was not reached: " + cut + "; " + result.Status.LastError);
    }

    private static async Task<int> ProbeRestartFence(string package, string output)
    {
        var root = Path.Combine(output, "state-copy");
        var before = RestartSnapshot(root);
        var starts = FenceWorkerStarts(output);
        var recoveries = 0; var slots = 0; var reservations = 0; var owners = 0; var releases = 0;
        var fs = new FileSystemManager(root, NullLogger<FileSystemManager>.Instance,
            PhysicalLoadTransactionOperations.Instance, new FileSystemManagerHooks { LocalPublicationRecoveryObserver = (_, _) => recoveries++ });
        var task = OwnershipTask("restart_r3_distinct_cold_probe");
        var hooks = new GmWorkerBridgePoolHooks
        {
            BeforeWorkerSlotWaitAsync = () =>
            { slots++; throw new InvalidOperationException("Unexpected admission stops before new capacity allocation."); },
            BeforeTaskReservationAsync = () => { reservations++; return Task.CompletedTask; },
            AfterOwnerBound = _ => owners++,
            BeforeWorkerReleaseAsync = () => { releases++; return Task.CompletedTask; }
        };
        var reaper = new GmWorkerQuarantineReaper(1, retrySchedule: [], runInBackground: false);
        using var admission = new GmWorkerNativePoolAdmission(package, root, durable: true);
        var pool = new GmWorkerBridgePool(fs, null, new GmWorkerAuditLog(fs), hooks, GmWorkerProcessTreeFactory.Instance, reaper, admission);
        var result = await pool.RunTaskAsync(OwnershipProfile(package, output), task);
        var observed = await GmWorkerRunLedger.ObserveAsync(new(root));
        await File.WriteAllTextAsync(Path.Combine(output, "fence-probe.json"), JsonSerializer.Serialize(new
        {
            recoveries, slots, reservations, owners, releases, capacity = reaper.OwnedCapacity, entries = reaper.EntryCount,
            kind = observed.Kind.ToString(), activeReservations = observed.Entries.Count,
            accepted = result.HasValidatedExecutionFor(task),
            proposalConsumer = GmWorkerProposalOnlyDispatchService.CanAcceptExecution(result, task),
            repairConsumer = GmWorkerValidationRepairDelegator.CanAcceptExecution(result, task),
            before, after = RestartSnapshot(root), preserved = before.SequenceEqual(RestartSnapshot(root)) && starts == FenceWorkerStarts(output),
            workerStarts = FenceWorkerStarts(output), error = result.Status.LastError
        }));
        return 0;
    }
}
