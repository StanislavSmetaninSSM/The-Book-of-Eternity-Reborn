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
        if (mode != "restart-cold-run") return 64;

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
        // Initial causal RED intentionally exercises the accepted B injection before
        // R2 exists. The connected R2 context will replace this admission at GREEN;
        // the behavioral assertions and actual RunTaskAsync path stay the same.
        var admission = new GmWorkerNativePoolAdmission(package, root);
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
            elapsedMilliseconds = elapsed.ElapsedMilliseconds, result
        }));
        return 0;
    }

    private static async Task<int> SeedColdRestart(string root, string output)
    {
        Directory.CreateDirectory(root);
        var fs = new FileSystemManager(root, NullLogger<FileSystemManager>.Instance);
        fs.EnsureDirectoryStructure();
        Directory.CreateDirectory(Path.GetDirectoryName(fs.SessionGenerationPath)!);
        await File.WriteAllTextAsync(fs.SessionGenerationPath, JsonSerializer.Serialize(new
        { SchemaVersion = 1, GenerationId = GmWorkerBridgeTestFixtures.SessionGeneration }));
        await fs.WriteFileAtomicBytesAsync(RestartContextPath, RestartContextBytes);
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

    private static string[] RestartSnapshot(string root) => Directory.EnumerateFiles(root, "*", SearchOption.AllDirectories)
        .Order(StringComparer.Ordinal).Select(path => Path.GetRelativePath(root, path) + ":" +
            Convert.ToHexString(SHA256.HashData(File.ReadAllBytes(path))).ToLowerInvariant()).ToArray();

    [DllImport("libc", EntryPoint = "_exit")] private static extern void ExitRestartImmediately(int status);
}
