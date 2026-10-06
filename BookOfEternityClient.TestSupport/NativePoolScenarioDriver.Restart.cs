using System.Diagnostics;
using System.Runtime.InteropServices;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using BookOfEternityClient.Core;
using BookOfEternityClient.Services.GmWorkers;
using Microsoft.Extensions.Logging.Abstractions;

namespace BookOfEternityClient.Tests;

internal static partial class NativePoolScenarioDriver
{
    private const string RestartContextPath = "game_state/world/weather.json";
    private static readonly byte[] RestartContextBytes = Encoding.UTF8.GetBytes("{\"fixture\":\"restart-before\"}");

    internal static async Task<int> RunRestart(string mode, string package, string output)
    {
        var root = Path.Combine(output, "state-copy");
        if (mode == "restart-seed-cold") return await SeedColdRestart(root, output);
        if (mode is not ("restart-cold-run" or "restart-happy-run")) return 64;
        if (mode == "restart-happy-run") await BootstrapRestartRoot(root);

        // This is deliberately a fresh FS and actual pool. No bootstrap, generation
        // write, recovery, ledger rewrite or hand-made result precedes admission.
        var recoveryObservations = 0;
        var fs = new FileSystemManager(root, NullLogger<FileSystemManager>.Instance,
            PhysicalLoadTransactionOperations.Instance, new FileSystemManagerHooks
            { LocalPublicationRecoveryObserver = (_, _) => recoveryObservations++ });
        var profile = GmWorkerBridgeTestFixtures.ValidationRepairCodexProfile() with
        {
            LaunchCommand = string.Join(" ", new[]
            {
                Path.Combine(Environment.GetEnvironmentVariable("DOTNET_ROOT")!, "dotnet"),
                typeof(NativePoolScenarioDriver).Assembly.Location,
                "pool-worker-content-valid", package, output
            }.Select(value => "\"" + value + "\"")),
            TimeoutSeconds = 15
        };
        var task = GmWorkerBridgeTestFixtures.ValidationRepairTask() with
        {
            TimeoutSeconds = profile.TimeoutSeconds,
            ContextFiles = [new WorkerFileReference
            { Path = RestartContextPath, Sha256 = GmWorkerRunLedgerCodec.Hash(RestartContextBytes) }]
        };
        var releases = 0; var boundOwners = 0; var publicationCalls = 0;
        var reservationCalls = 0; string? workspace = null;
        var hooks = new GmWorkerBridgePoolHooks
        {
            AfterOwnerBound = _ => boundOwners++,
            BeforeTaskReservationAsync = () => { reservationCalls++; return Task.CompletedTask; },
            BeforeWorkerReleaseAsync = () => { releases++; return Task.CompletedTask; },
            BeforeProposalPublicationAsync = () => { publicationCalls++; return Task.CompletedTask; },
            BeforeWorkspaceCleanupAsync = path => { workspace = path; return Task.CompletedTask; }
        };
        var reaper = new GmWorkerQuarantineReaper(capacity: 1, retrySchedule: [], runInBackground: false);
        using var admission = new GmWorkerNativePoolAdmission(package, root, durable: true);
        var pool = new GmWorkerBridgePool(fs, null, new GmWorkerAuditLog(fs), hooks,
            GmWorkerProcessTreeFactory.Instance, reaper, admission);
        var before = RestartSnapshot(root);
        GmWorkerTaskRunResult? result = null; string? failure = null;
        var elapsed = Stopwatch.StartNew();
        try { result = await pool.RunTaskAsync(profile, task); }
        catch (Exception error) { failure = error.ToString(); }
        elapsed.Stop();
        var after = RestartSnapshot(root);
        var observed = await GmWorkerRunLedger.ObserveAsync(new(root));
        var proposalPath = fs.ResolvePath(GmWorkerProposalStore.GetProposalPath("worker_proposal_native_pool_happy"));
        var statePath = Path.Combine(root, ".boe_runtime", "worker-runs-v1", "state.json");
        var state = File.Exists(statePath) ? GmWorkerRunLedgerCodec.Decode(new(root), File.ReadAllBytes(statePath)) : null;
        WorkerRunRecord? retired = null;
        if (state?.Retired.Length == 1)
            retired = GmWorkerRunRecordCodec.Decode(File.ReadAllBytes(Path.Combine(root, ".boe_runtime", "worker-runs-v1", "retired", state.Retired[0].RunId + ".json")));
        var taskPath = fs.ResolvePath(GmWorkerBridgePool.GetTaskPacketPath(task.TaskId));
        var publication = retired?.Progress?.Publication;
        var contentImportedExactly = result?.Proposal?.ChangedFiles.Count == 1 &&
            File.Exists(fs.ResolvePath(result.Proposal.ChangedFiles[0].ContentRef!)) &&
            File.ReadAllBytes(fs.ResolvePath(result.Proposal.ChangedFiles[0].ContentRef!)).AsSpan().SequenceEqual(ProposedContent);
        await File.WriteAllTextAsync(Path.Combine(output, "restart-result.json"), JsonSerializer.Serialize(new
        {
            mode, failure, boundOwners, releases, publicationCalls, reservationCalls,
            recoveryObservations, before, after,
            preservedRoot = before.SequenceEqual(after),
            workerStarts = File.Exists(Path.Combine(output, "worker-starts"))
                ? File.ReadAllLines(Path.Combine(output, "worker-starts")).Length : 0,
            actualSuccess = result?.HasValidatedExecutionFor(task) == true,
            proposalPublished = File.Exists(proposalPath),
            workspaceCleaned = workspace == null || !Directory.Exists(workspace),
            reaperEntries = reaper.EntryCount, reaperCapacity = reaper.OwnedCapacity,
            ledgerKind = observed.Kind.ToString(),
            phases = observed.Entries.Select(entry => entry.Phase.ToString()),
            activeEntries = state?.Entries.Length, retiredEntries = state?.Retired.Length,
            retiredPhase = retired?.Phase.ToString(), contentImportedExactly,
            originalRunBound = retired?.Identity.RunId == result?.ExecutionIdentity?.RunId && retired is not null,
            originalTaskBound = retired is not null && File.Exists(taskPath) && retired.Identity.TaskSha256 == GmWorkerRunLedgerCodec.Hash(File.ReadAllBytes(taskPath)) &&
                retired.Identity.WorkerId == task.WorkerId && retired.Identity.TaskId == task.TaskId && retired.Identity.GenerationId == task.SessionGeneration,
            publicationCommitted = publication?.Committed == true && result?.Proposal is not null && publication.ProposalId == result.Proposal.ProposalId &&
                File.Exists(proposalPath) && publication.ProposalSha256 == GmWorkerRunLedgerCodec.Hash(File.ReadAllBytes(proposalPath)),
            normalCleanupWithoutAudit = retired?.Progress?.Cleanup is { RequiredAudit: false, AuditEventId: null, AuditSha256: null },
            elapsedMilliseconds = elapsed.ElapsedMilliseconds, result
        }));
        return 0;
    }

    private static async Task<int> SeedColdRestart(string root, string output)
    {
        await BootstrapRestartRoot(root);
        var fs = new FileSystemManager(root, NullLogger<FileSystemManager>.Instance);
        await using var canonical = await fs.AcquireCanonicalWriteLeaseAsync();
        await using var coordinator = await GmWorkerRunLedger.OpenCoordinatorAsync(new(root));
        if (coordinator is null || await coordinator.InitializeAsync() != WorkerLedgerMutationKind.Applied) return 71;
        var prior = GmWorkerBridgeTestFixtures.AnalysisTask() with { TaskId = "earlier_unresolved_task" };
        var prepared = await coordinator.PrepareAsync(new(prior.SessionGeneration, prior.WorkerId, prior.TaskId,
            GmWorkerRunLedgerCodec.Hash(Encoding.UTF8.GetBytes(GmWorkerJson.Serialize(prior))),
            WorkerRunBackend.LinuxNativeLineage, WorkerRunScope.OrdinarySamePidNamespace,
            Path.Combine(output, "earlier-workspace")), coordinator.Sequence);
        if (prepared.Kind != WorkerLedgerMutationKind.Applied) return 72;

        // A real interrupted ordinary publication provides a causal recovery writer.
        // The first app exits abruptly after member install, leaving both Prepared
        // and pending canonical decision evidence. No worker is started in this app.
        new TrustedLocalFilePublication(fs, new TrustedLocalFileScope([root])).Publish(canonical,
            TrustedLocalGeneration.Existing(prior.SessionGeneration),
            [new(fs.ResolvePath(RestartContextPath), RestartContextBytes, Encoding.UTF8.GetBytes("{\"fixture\":\"interrupted\"}"))],
            (phase, _) =>
            {
                if (phase != TrustedLocalPublicationPhase.MemberPublished) return;
                File.WriteAllText(Path.Combine(output, "restart-cut.json"), JsonSerializer.Serialize(new
                { phase = phase.ToString(), ledgerPhase = "Prepared", exitCode = 77 }));
                ExitRestartImmediately(77);
            });
        return 73;
    }

    private static async Task BootstrapRestartRoot(string root)
    {
        Directory.CreateDirectory(root);
        var fs = new FileSystemManager(root, NullLogger<FileSystemManager>.Instance);
        fs.EnsureDirectoryStructure();
        Directory.CreateDirectory(Path.GetDirectoryName(fs.SessionGenerationPath)!);
        await File.WriteAllTextAsync(fs.SessionGenerationPath, JsonSerializer.Serialize(new
        { SchemaVersion = 1, GenerationId = GmWorkerBridgeTestFixtures.SessionGeneration }));
        await fs.WriteFileAtomicBytesAsync(RestartContextPath, RestartContextBytes);
    }

    private static string[] RestartSnapshot(string root) => Directory.EnumerateFiles(root, "*", SearchOption.AllDirectories)
        .Where(path => path != Path.Combine(root, ".boe_runtime", "worker-runs-v1", "owner.lock") &&
            path != Path.Combine(root, ".boe_runtime", "worker-runs-v1", "journal.lock"))
        .Order(StringComparer.Ordinal).Select(path => Path.GetRelativePath(root, path) + ":" +
            Convert.ToHexString(SHA256.HashData(File.ReadAllBytes(path))).ToLowerInvariant()).ToArray();

    [DllImport("libc", EntryPoint = "_exit")] private static extern void ExitRestartImmediately(int status);
}
