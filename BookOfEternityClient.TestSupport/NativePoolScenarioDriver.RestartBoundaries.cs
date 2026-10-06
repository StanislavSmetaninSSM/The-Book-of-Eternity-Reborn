using System.Text;
using System.Text.Json;
using BookOfEternityClient.Core;
using BookOfEternityClient.Services.GmWorkers;
using Microsoft.Extensions.Logging.Abstractions;

namespace BookOfEternityClient.Tests;

internal static partial class NativePoolScenarioDriver
{
    private static async Task<int> RunRestartBoundary(string mode, string package, string output)
    {
        var root = Path.Combine(output, "state-copy");
        await BootstrapRestartRoot(root);
        var fs = new FileSystemManager(root, NullLogger<FileSystemManager>.Instance);
        var statePath = Path.Combine(root, ".boe_runtime", "worker-runs-v1", "state.json");
        var modePath = Path.Combine(root, ".boe_runtime", "worker-runs-v1", "mode.json");
        var profile = GmWorkerBridgeTestFixtures.ValidationRepairCodexProfile() with
        {
            LaunchCommand = string.Join(" ", new[] { Path.Combine(Environment.GetEnvironmentVariable("DOTNET_ROOT")!, "dotnet"),
                typeof(NativePoolScenarioDriver).Assembly.Location, "pool-worker-content-valid", package, output }.Select(x => "\"" + x + "\"")),
            TimeoutSeconds = 15
        };
        var task = GmWorkerBridgeTestFixtures.ValidationRepairTask() with
        {
            TimeoutSeconds = profile.TimeoutSeconds,
            ContextFiles = [new WorkerFileReference { Path = RestartContextPath, Sha256 = GmWorkerRunLedgerCodec.Hash(RestartContextBytes) }]
        };
        var faultEnabled = true; var faults = 0; var stageFailures = 0; var cleanupFailures = 0;
        byte[]? originalMode = null; string? workspace = null;
        void CorruptOriginalMode()
        {
            originalMode ??= File.ReadAllBytes(modePath);
            File.WriteAllText(modePath, "{\"synthetic\":\"mode-authority-lost\"}"); faults++;
        }
        void Observe(WorkerLedgerIoStage stage)
        {
            if (!faultEnabled) return;
            if (mode == "prepared-registration-loss" && stage == WorkerLedgerIoStage.BeforeLiveRegistration)
            { CorruptOriginalMode(); return; }
            if (stage != WorkerLedgerIoStage.StateDirectorySynced || !File.Exists(statePath)) return;
            var state = GmWorkerRunLedgerCodec.Decode(new(root), File.ReadAllBytes(statePath));
            if (mode == "publication-ack" && state.Entries.Any(x => x.Phase == WorkerRunPhase.Published) ||
                mode == "retirement-ack" && state.Retired.Length == 1 && state.Entries.Length == 0)
            { faults++; throw new IOException("Synthetic negative-only lost original metadata ACK."); }
        }
        var hooks = new GmWorkerBridgePoolHooks
        {
            BeforeWorkspaceFileCreateAsync = _ =>
            {
                if (mode == "partial-workspace" && ++stageFailures == 1) throw new IOException("Synthetic actual staging failure after original workspace acquisition.");
                return Task.CompletedTask;
            },
            BeforeWorkspaceCleanupAsync = path =>
            {
                workspace = path;
                if (mode == "retirement-authority-loss" && faults == 0) CorruptOriginalMode();
                if (mode == "partial-workspace" && faultEnabled) { cleanupFailures++; throw new IOException("Synthetic retained cleanup failure."); }
                return Task.CompletedTask;
            }
        };
        var reaper = new GmWorkerQuarantineReaper(capacity: 1, retrySchedule: [], runInBackground: false);
        using var admission = new GmWorkerNativePoolAdmission(package, root, durable: true, Observe);
        var pool = new GmWorkerBridgePool(fs, null, new GmWorkerAuditLog(fs), hooks,
            GmWorkerProcessTreeFactory.Instance, reaper, admission);
        var result = await pool.RunTaskAsync(profile, task);
        var before = BoundarySnapshot(result, task, reaper, statePath, workspace);
        var stateBefore = File.ReadAllBytes(statePath);
        var archived = Directory.GetFiles(Path.Combine(root, ".boe_runtime", "worker-runs-v1", "retired"));
        byte[]? archiveBefore = archived.Length == 1 ? File.ReadAllBytes(archived[0]) : null;
        var archiveTime = new DateTime(2020, 1, 2, 3, 4, 5, DateTimeKind.Utc);
        if (archiveBefore != null) File.SetLastWriteTimeUtc(archived[0], archiveTime);
        admission.Dispose();
        await using var competingOwner = await GmWorkerRunLedger.OpenCoordinatorAsync(new(root));
        var lockRetainedAfterClientDispose = competingOwner == null;
        var probeReservations = 0; var canonicalRefused = false;
        var secondFs = new FileSystemManager(root, NullLogger<FileSystemManager>.Instance);
        try { await using var forbidden = await secondFs.AcquireCanonicalWriteLeaseAsync(); }
        catch (Exception error) when (GmWorkerRunLedger.Unavailable(error)) { canonicalRefused = true; }
        using (var secondAdmission = new GmWorkerNativePoolAdmission(package, root, durable: true))
        {
            var secondHooks = new GmWorkerBridgePoolHooks { BeforeTaskReservationAsync = () =>
            { probeReservations++; throw new InvalidOperationException("Probe stops before any new launch."); } };
            var secondReaper = new GmWorkerQuarantineReaper(capacity: 1, retrySchedule: [], runInBackground: false);
            var secondPool = new GmWorkerBridgePool(secondFs, null, null, secondHooks, GmWorkerProcessTreeFactory.Instance, secondReaper, secondAdmission);
            using var deadline = new CancellationTokenSource(TimeSpan.FromSeconds(3));
            _ = await secondPool.RunTaskAsync(profile with { WorkerId = "other_fixture_worker" },
                task with { WorkerId = "other_fixture_worker", TaskId = "other_fixture_task" }, deadline.Token);
        }
        faultEnabled = false;
        if (originalMode != null) File.WriteAllBytes(modePath, originalMode);
        await Task.WhenAll(reaper.RunPassAsync(), reaper.RunPassAsync());
        var after = BoundarySnapshot(result, task, reaper, statePath, workspace);
        var finalState = GmWorkerRunLedgerCodec.Decode(new(root), File.ReadAllBytes(statePath));
        WorkerRunRecord? terminal = null;
        if (finalState.Retired.Length == 1)
            terminal = GmWorkerRunRecordCodec.Decode(File.ReadAllBytes(Path.Combine(root, ".boe_runtime", "worker-runs-v1", "retired", finalState.Retired[0].RunId + ".json")));
        var proposalPath = fs.ResolvePath(GmWorkerProposalStore.GetProposalPath("worker_proposal_native_pool_happy"));
        await File.WriteAllTextAsync(Path.Combine(output, "restart-boundary.json"), JsonSerializer.Serialize(new
        {
            mode, faults, stageFailures, cleanupFailures, before, after, probeReservations, canonicalRefused,
            lockRetainedAfterClientDispose, terminal, terminalPhase = terminal?.Phase.ToString(), proposalPreserved = File.Exists(proposalPath),
            exactTerminalRetry = archiveBefore != null && archived.Length == 1 && File.ReadAllBytes(archived[0]).AsSpan().SequenceEqual(archiveBefore) &&
                File.GetLastWriteTimeUtc(archived[0]) == archiveTime && File.ReadAllBytes(statePath).AsSpan().SequenceEqual(stateBefore),
            workerStarts = File.Exists(Path.Combine(output, "worker-starts")) ? File.ReadAllLines(Path.Combine(output, "worker-starts")).Length : 0
        }));
        return 0;
    }
    private static object BoundarySnapshot(GmWorkerTaskRunResult result, WorkerTaskPacket task, GmWorkerQuarantineReaper reaper,
        string statePath, string? workspace)
    {
        using var state = JsonDocument.Parse(File.ReadAllBytes(statePath));
        return new
        {
            accepted = result.HasValidatedExecutionFor(task),
            proposalConsumer = GmWorkerProposalOnlyDispatchService.CanAcceptExecution(result, task),
            repairConsumer = GmWorkerValidationRepairDelegator.CanAcceptExecution(result, task),
            stopState = result.StopEvidence?.State.ToString(), outputsSettled = result.OutputsSettled,
            entries = reaper.EntryCount, capacity = reaper.OwnedCapacity,
            active = state.RootElement.GetProperty("Entries").GetArrayLength(), retired = state.RootElement.GetProperty("Retired").GetArrayLength(),
            phases = state.RootElement.GetProperty("Entries").EnumerateArray().Select(x => x.GetProperty("Phase").GetString()).ToArray(),
            workspaceExists = workspace != null && Directory.Exists(workspace),
            lastError = result.Status.LastError, cleanupDeferred = result.Status.CleanupDeferred
        };
    }
}
