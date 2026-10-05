using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using System.Diagnostics;
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
        if (mode == "pool-worker") return await Worker(package, output, null);
        if (mode.StartsWith("pool-worker-", StringComparison.Ordinal)) return await Worker(package, output, mode[12..]);
        var descendantMode = mode.StartsWith("pool-descendant-", StringComparison.Ordinal) ? mode[16..] : null;
        var terminalMode = mode.StartsWith("pool-terminal-", StringComparison.Ordinal) ? mode[14..] : null;
        if (mode != "pool-happy" && descendantMode is not ("tree" or "root-first" or "doublefork" or "ignore" or "spawn") &&
            terminalMode is not ("cancel-release" or "timeout-release" or "cancel-publication" or "timeout-publication" or
                "generation" or "task-bytes" or "nonzero" or "missing-proposal")) return 64;
        var workerMode = terminalMode is "nonzero" or "missing-proposal" ? terminalMode : descendantMode;
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
                typeof(NativePoolScenarioDriver).Assembly.Location,
                workerMode == null ? "pool-worker" : "pool-worker-" + workerMode, package, output
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
        using var cancellation = new CancellationTokenSource();
        using var timeout = new CancellationTokenSource();
        var hooks = new GmWorkerBridgePoolHooks
        {
            BeforeWorkerReleaseAsync = () =>
            {
                releases++;
                if (terminalMode == "cancel-release") cancellation.Cancel();
                if (terminalMode == "timeout-release") timeout.Cancel();
                return Task.CompletedTask;
            },
            BeforeProposalPublicationAsync = async () =>
            {
                publicationCalls++;
                if (terminalMode == "cancel-publication") cancellation.Cancel();
                if (terminalMode == "timeout-publication") timeout.Cancel();
                if (terminalMode == "generation")
                {
                    await using var lifecycle = await fs.AcquireSessionLifecycleLeaseAsync();
                    await using var lease = await fs.AcquireSessionReplacementWriteLeaseAsync(lifecycle);
                    fs.RotateSessionGeneration(lease);
                }
                if (terminalMode == "task-bytes")
                    await fs.WriteFileAtomicBytesAsync(GmWorkerBridgePool.GetTaskPacketPath(task.TaskId), [99]);
            },
            BeforeWorkspaceCleanupAsync = path => { cleanupPath = path; return Task.CompletedTask; },
            TimeoutSignal = timeout.Token
        };
        var reaper = new GmWorkerQuarantineReaper(capacity: 1, retrySchedule: [], runInBackground: false);
        var pool = new GmWorkerBridgePool(fs, null, new GmWorkerAuditLog(fs),
            hooks, GmWorkerProcessTreeFactory.Instance, reaper, new GmWorkerNativePoolAdmission(package, fixtureRoot));
        GmWorkerTaskRunResult? result = null; string? failure = null; var canceled = false;
        var executionClock = Stopwatch.StartNew();
        try { result = await pool.RunTaskAsync(profile, task, cancellation.Token); }
        catch (Exception ex) { failure = ex.ToString(); canceled = ex is OperationCanceledException; }
        executionClock.Stop();
        var stored = result?.Proposal == null ? null : await new GmWorkerProposalStore(fs).ReadProposalAsync(result.Proposal.ProposalId);
        await File.WriteAllTextAsync(Path.Combine(output, "scenario.json"), JsonSerializer.Serialize(new
        {
            success = result?.Status.State == WorkerBridgeState.Stopped && result.ExitCode == 0 && stored != null,
            failure = failure ?? result?.Status.LastError,
            canceled, resultReturned = result != null,
            releases, publicationCalls,
            workerStarts = File.Exists(Path.Combine(output, "worker-starts")) ? File.ReadAllLines(Path.Combine(output, "worker-starts")).Length : 0,
            workspaceCleaned = cleanupPath != null && !Directory.Exists(cleanupPath),
            reaperEntries = reaper.EntryCount, reaperCapacity = reaper.OwnedCapacity,
            canonicalContextUnchanged = await File.ReadAllTextAsync(fs.ResolvePath(contextPath)) == weather,
            validatedExecution = result?.HasValidatedExecutionFor(task) == true,
            proposalBytesMatch = result?.Proposal != null && (await File.ReadAllBytesAsync(
                fs.ResolvePath(GmWorkerProposalStore.GetProposalPath(result.Proposal.ProposalId))))
                .AsSpan().SequenceEqual(Encoding.UTF8.GetBytes(GmWorkerJson.Serialize(result.Proposal))),
            stagingCleaned = !Directory.Exists(Path.Combine(fs.RuntimeRootPath, "proposal-staging")) ||
                !Directory.EnumerateFileSystemEntries(Path.Combine(fs.RuntimeRootPath, "proposal-staging")).Any(),
            elapsedMilliseconds = executionClock.ElapsedMilliseconds,
            fixtureEvents = ReadFixtureEvents(output),
            permitChecks = CheckConsumerPermits(result, task),
            failureCopyRejected = result != null && FailureCopyRejected(result, task),
            noCanonicalProposalOrInbox = !File.Exists(fs.ResolvePath(GmWorkerProposalStore.GetProposalPath("worker_proposal_native_pool_happy"))) &&
                !File.Exists(fs.ResolvePath(GmWorkerBridgePool.GetProposalInboxPath(task.TaskId))),
            tamperedTaskRetained = terminalMode != "task-bytes" ||
                (await File.ReadAllBytesAsync(fs.ResolvePath(GmWorkerBridgePool.GetTaskPacketPath(task.TaskId)))).AsSpan().SequenceEqual(new byte[] { 99 }),
            result
        }));
        return 0;
    }

    private static bool FailureCopyRejected(GmWorkerTaskRunResult result, WorkerTaskPacket task)
    {
        var forged = result with
        {
            Status = result.Status with { State = WorkerBridgeState.Stopped }, ExitCode = 0,
            TimedOut = false, SessionReplaced = false, BoundTask = task,
            Proposal = new WorkerProposal { ProposalId = "forged-public-record", WorkerId = task.WorkerId,
                TaskId = task.TaskId, Status = WorkerProposalStatus.Completed }
        };
        return !GmWorkerProposalOnlyDispatchService.CanAcceptExecution(forged, task) &&
            !GmWorkerValidationRepairDelegator.CanAcceptExecution(forged, task);
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

    private static async Task<int> Worker(string package, string output, string? descendantMode)
    {
        await File.AppendAllTextAsync(Path.Combine(output, "worker-starts"), "started\n");
        if (descendantMode == "missing-proposal") return 0;
        if (descendantMode != null && descendantMode != "nonzero")
        {
            if (descendantMode is not ("tree" or "root-first" or "doublefork" or "ignore" or "spawn")) return 64;
            var start = new ProcessStartInfo(Path.Combine(package, "lineage-fixture")) { UseShellExecute = false };
            foreach (var value in new[] { "--worker", output, descendantMode }) start.ArgumentList.Add(value);
            // The synthetic actor has its own7s expiry and the independent outer
            // guardian. Its inherited output handles deliberately outlive this worker.
            using var actor = Process.Start(start)!;
            var preparation = Stopwatch.StartNew();
            while (!ReadFixtureEvents(output).Any(entry => entry.GetProperty("kind").GetString() ==
                       (descendantMode == "root-first" ? "leaf" : "prepared")))
            {
                if (preparation.Elapsed > TimeSpan.FromSeconds(3))
                    throw new TimeoutException("Native descendant fixture preparation did not reach its ready event.");
                await Task.Delay(10);
            }
            if (descendantMode == "root-first") await actor.WaitForExitAsync().WaitAsync(TimeSpan.FromSeconds(3));
            // No stop/kill is issued by the worker. The actual pool owner must
            // retire this lineage after observing the managed worker's Completed.
        }
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
        return descendantMode == "nonzero" ? 23 : 0;
    }

    private static JsonElement[] ReadFixtureEvents(string output)
    {
        var path = Path.Combine(output, "events.jsonl");
        if (!File.Exists(path)) return [];
        var result = new List<JsonElement>();
        foreach (var line in File.ReadAllLines(path))
        {
            try { using var value = JsonDocument.Parse(line); result.Add(value.RootElement.Clone()); }
            catch (JsonException) { /* Only a concurrently written final event may be incomplete. */ }
        }
        return result.ToArray();
    }
}
