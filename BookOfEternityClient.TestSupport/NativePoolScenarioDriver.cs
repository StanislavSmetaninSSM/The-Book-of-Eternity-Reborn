using System.Security.Cryptography;
using System.Text;
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
        var pool = new GmWorkerBridgePool(fs, null, new GmWorkerAuditLog(fs),
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
            validatedExecution = result?.HasValidatedExecutionFor(task) == true,
            proposalBytesMatch = result?.Proposal != null && (await File.ReadAllBytesAsync(
                fs.ResolvePath(GmWorkerProposalStore.GetProposalPath(result.Proposal.ProposalId))))
                .AsSpan().SequenceEqual(Encoding.UTF8.GetBytes(GmWorkerJson.Serialize(result.Proposal))),
            stagingCleaned = !Directory.EnumerateFileSystemEntries(Path.Combine(fs.RuntimeRootPath, "proposal-staging")).Any(),
            permitChecks = CheckConsumerPermits(result, task),
            result
        }));
        return 0;
    }

    private static Dictionary<string, bool> CheckConsumerPermits(GmWorkerTaskRunResult? result, WorkerTaskPacket task)
    {
        if (result?.Proposal == null || result.BoundTask == null) return new() { ["actual-publication-required"] = false };
        static bool Accept(GmWorkerTaskRunResult run, WorkerTaskPacket requested) =>
            GmWorkerProposalOnlyDispatchService.CanAcceptExecution(run, requested) &&
            GmWorkerValidationRepairDelegator.CanAcceptExecution(run, requested);
        static bool Reject(GmWorkerTaskRunResult run, WorkerTaskPacket requested) =>
            !GmWorkerProposalOnlyDispatchService.CanAcceptExecution(run, requested) &&
            !GmWorkerValidationRepairDelegator.CanAcceptExecution(run, requested);
        var checks = new Dictionary<string, bool>
        {
            ["actual-publication-accepted"] = Accept(result, task),
            ["changed-requested-body-rejected"] = Reject(result, task with { Instructions = "substituted body" }),
            ["changed-bound-body-rejected"] = Reject(result with { BoundTask = result.BoundTask with { Instructions = "substituted body" } }, task),
            ["changed-proposal-body-rejected"] = Reject(result with { Proposal = result.Proposal with { Summary = "substituted proposal" } }, task),
            ["nonzero-result-rejected"] = Reject(result with { ExitCode = 1 }, task),
            ["timeout-result-rejected"] = Reject(result with { TimedOut = true }, task),
            ["replaced-result-rejected"] = Reject(result with { SessionReplaced = true }, task),
            ["failed-result-rejected"] = Reject(result with { Status = result.Status with { State = WorkerBridgeState.Failed } }, task),
            ["wrong-worker-rejected"] = Reject(result with { Status = result.Status with { WorkerId = "another-worker" } }, task),
            ["wrong-task-rejected"] = Reject(result with { Status = result.Status with { CurrentTaskId = "another-task" } }, task)
        };
        var contexts = result.BoundTask.ContextFiles.ToList();
        var mutableBound = result with { BoundTask = result.BoundTask with { ContextFiles = contexts } };
        checks["same-bound-model-accepted"] = Accept(mutableBound, task);
        contexts[0] = contexts[0] with { Sha256 = new string('f', 64) };
        checks["mutated-bound-model-rejected"] = Reject(mutableBound, task);
        var findings = result.Proposal.Findings.ToList();
        var mutableProposal = result with { Proposal = result.Proposal with { Findings = findings } };
        checks["same-proposal-model-accepted"] = Accept(mutableProposal, task);
        findings[0] = findings[0] with { Message = "mutated after result copy" };
        checks["mutated-proposal-model-rejected"] = Reject(mutableProposal, task);
        return checks;
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
