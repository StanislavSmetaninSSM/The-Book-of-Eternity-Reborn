using System.Security.Cryptography;
using System.Text.Json;
using BookOfEternityClient.Core;
using BookOfEternityClient.Services.GmWorkers;
using Microsoft.Extensions.Logging.Abstractions;

namespace BookOfEternityClient.Tests;

// Actual production pool in a private state copy. Launched only beneath the
// independent C subreaper; it retains cleanup authority if this process is lost.
internal static class NativePoolScenarioDriver
{
    internal static async Task<int> Run(string mode, string package, string output)
    {
        if (mode == "pool-worker") return await Worker(output);
        if (mode != "pool-happy") return 64;
        var fixtureRoot = Path.Combine(output, "state-copy");
        Directory.CreateDirectory(fixtureRoot);
        var fs = new FileSystemManager(fixtureRoot, NullLogger<FileSystemManager>.Instance);
        fs.EnsureDirectoryStructure();
        Directory.CreateDirectory(Path.GetDirectoryName(fs.SessionGenerationPath)!);
        await File.WriteAllTextAsync(fs.SessionGenerationPath,
            $$"""{"SchemaVersion":1,"GenerationId":"{{GmWorkerBridgeTestFixtures.SessionGeneration}}"}""");
        const string weather = "{\"fixture\":\"isolated-pool-context\"}";
        const string contextPath = "game_state/world/weather.json";
        await fs.WriteFileAtomicAsync(contextPath, weather);
        var profile = GmWorkerBridgeTestFixtures.AnalysisCodexProfile() with
        {
            LaunchCommand = string.Join(" ", new[]
            {
                Path.Combine(Environment.GetEnvironmentVariable("DOTNET_ROOT")!, "dotnet"),
                typeof(NativePoolScenarioDriver).Assembly.Location, "pool-worker", package, output
            }.Select(value => "\"" + value + "\"")),
            TimeoutSeconds = 15
        };
        var task = GmWorkerBridgeTestFixtures.AnalysisTask() with
        {
            TimeoutSeconds = profile.TimeoutSeconds,
            ContextFiles = [new WorkerFileReference
            {
                Path = contextPath,
                Sha256 = Convert.ToHexString(SHA256.HashData(await File.ReadAllBytesAsync(fs.ResolvePath(contextPath)))).ToLowerInvariant()
            }]
        };
        var releases = 0; var publicationCalls = 0; string? cleanupPath = null;
        var hooks = new GmWorkerBridgePoolHooks
        {
            BeforeWorkerReleaseAsync = () => { releases++; return Task.CompletedTask; },
            BeforeProposalPublicationAsync = () => { publicationCalls++; return Task.CompletedTask; },
            BeforeWorkspaceCleanupAsync = path => { cleanupPath = path; return Task.CompletedTask; }
        };
        var reaper = new GmWorkerQuarantineReaper(capacity: 1, retrySchedule: [], runInBackground: false);
        var pool = new GmWorkerBridgePool(fs, new GmWorkerProposalStore(fs), new GmWorkerAuditLog(fs),
            hooks, GmWorkerProcessTreeFactory.Instance, reaper, new GmWorkerNativePoolAdmission(package, fixtureRoot));
        GmWorkerTaskRunResult? result = null; string? failure = null;
        try { result = await pool.RunTaskAsync(profile, task); }
        catch (Exception ex) { failure = ex.ToString(); }
        var stored = result?.Proposal == null ? null : await new GmWorkerProposalStore(fs).ReadProposalAsync(result.Proposal.ProposalId);
        await File.WriteAllTextAsync(Path.Combine(output, "scenario.json"), JsonSerializer.Serialize(new
        {
            success = result?.Status.State == WorkerBridgeState.Stopped && result.ExitCode == 0 && stored != null,
            failure = failure ?? result?.Status.LastError,
            releases, publicationCalls,
            workerStarts = File.Exists(Path.Combine(output, "worker-starts")) ? File.ReadAllLines(Path.Combine(output, "worker-starts")).Length : 0,
            workspaceCleaned = cleanupPath != null && !Directory.Exists(cleanupPath),
            reaperEntries = reaper.EntryCount, reaperCapacity = reaper.OwnedCapacity,
            canonicalContextUnchanged = await File.ReadAllTextAsync(fs.ResolvePath(contextPath)) == weather,
            result
        }));
        return 0;
    }

    private static async Task<int> Worker(string output)
    {
        await File.AppendAllTextAsync(Path.Combine(output, "worker-starts"), "started\n");
        var task = GmWorkerJson.Deserialize<WorkerTaskPacket>(await File.ReadAllTextAsync(
            Environment.GetEnvironmentVariable(GmWorkerBridgePool.TaskPathEnvironmentVariable)!))!;
        var proposal = new WorkerProposal
        {
            ProposalId = "worker_proposal_native_pool_happy", TaskId = task.TaskId, WorkerId = task.WorkerId,
            Status = WorkerProposalStatus.Completed, Summary = "Synthetic isolated pool analysis.",
            ChangedFiles = [], Findings = [new WorkerFinding { Kind = "analysis", Message = "Private fixture context only." }],
            SelfCheck = new WorkerSelfCheck { ScopeReviewed = true, ValidationExpectedToPass = true },
            CreatedAtUtc = "2026-10-05T00:00:00Z"
        };
        await File.WriteAllTextAsync(Environment.GetEnvironmentVariable(GmWorkerBridgePool.ProposalPathEnvironmentVariable)!,
            GmWorkerJson.Serialize(proposal));
        await Console.Out.WriteLineAsync("pool-worker-stdout");
        await Console.Error.WriteLineAsync("pool-worker-stderr");
        return 0;
    }
}
