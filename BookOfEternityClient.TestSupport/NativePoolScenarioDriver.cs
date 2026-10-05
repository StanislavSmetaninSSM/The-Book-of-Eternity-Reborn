using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using System.Diagnostics;
using System.Reflection;
using BookOfEternityClient.Core;
using BookOfEternityClient.Services.GmWorkers;
using Microsoft.Extensions.Logging.Abstractions;

namespace BookOfEternityClient.Tests;

// Actual production pool in a private state copy. Launched only beneath the
// independent C subreaper; it retains cleanup authority if this process is lost.
internal static partial class NativePoolScenarioDriver
{
    private static readonly byte[] ProposedContent = Encoding.UTF8.GetBytes("{\"fixture\":\"proposed-content-only\"}\n");

    internal static async Task<int> Run(string mode, string package, string output)
    {
        if (mode == "pool-worker") return await Worker(package, output, null);
        if (mode.StartsWith("pool-worker-", StringComparison.Ordinal)) return await Worker(package, output, mode[12..]);
        var descendantMode = mode.StartsWith("pool-descendant-", StringComparison.Ordinal) ? mode[16..] : null;
        var terminalMode = mode.StartsWith("pool-terminal-", StringComparison.Ordinal) ? mode[14..] : null;
        var contentMode = mode.StartsWith("pool-content-", StringComparison.Ordinal) ? mode[13..] : null;
        var helperLoss = mode == "pool-helper-loss-before-release";
        var faultMode = mode.StartsWith("pool-fault-", StringComparison.Ordinal) ? mode[11..] : null;
        var boundaryMode = mode.StartsWith("pool-boundary-", StringComparison.Ordinal) ? mode[14..] : null;
        var cleanupMode = mode.StartsWith("pool-cleanup-", StringComparison.Ordinal) ? mode[13..] : null;
        var cleanupCase = cleanupMode is "dispose" or "workspace" or "audit-failure" or "audit-unavailable" or "receipt-temp" or "receipt-ack" or "receipt-replaced" or "receipt-conflict";
        var cleanupScenario = cleanupCase ? new CleanupScenario(cleanupMode!) : null;
        var observationFault = cleanupScenario?.ObservationFault ?? CreateObservationFault(faultMode, boundaryMode);
        var faultCase = faultMode != null && (observationFault != null || faultMode is "owner-eof" or "status-loss" or "helper-loss");
        var boundaryCase = boundaryMode is "cancel-completed" or "timeout-completed" or "cancel-stop" or "timeout-stop" or "cancel-output" or "timeout-output";
        if (!cleanupCase && !faultCase && !boundaryCase && !helperLoss && contentMode is not ("valid" or "bad-hash" or "missing") && mode != "pool-happy" && descendantMode is not ("tree" or "root-first" or "doublefork" or "ignore" or "spawn") &&
            terminalMode is not ("cancel-release" or "timeout-release" or "cancel-publication" or "timeout-publication" or
                "generation" or "task-bytes" or "nonzero" or "missing-proposal")) return 64;
        var workerMode = contentMode != null ? "content-" + contentMode : terminalMode is "nonzero" or "missing-proposal" ? terminalMode : descendantMode;
        var fixtureRoot = Path.Combine(output, "state-copy");
        Directory.CreateDirectory(fixtureRoot);
        var fs = new FileSystemManager(fixtureRoot, NullLogger<FileSystemManager>.Instance, PhysicalLoadTransactionOperations.Instance, cleanupScenario?.FileHooks);
        fs.EnsureDirectoryStructure();
        Directory.CreateDirectory(Path.GetDirectoryName(fs.SessionGenerationPath)!);
        await File.WriteAllTextAsync(fs.SessionGenerationPath,
            $$"""{"SchemaVersion":1,"GenerationId":"{{GmWorkerBridgeTestFixtures.SessionGeneration}}"}""");
        const string weather = "{\"fixture\":\"isolated-pool-context\"}";
        const string contextPath = "game_state/world/weather.json";
        await fs.WriteFileAtomicAsync(contextPath, weather);
        var profile = (contentMode != null ? GmWorkerBridgeTestFixtures.ValidationRepairCodexProfile() : GmWorkerBridgeTestFixtures.AnalysisCodexProfile()) with
        {
            LaunchCommand = string.Join(" ", new[]
            {
                Path.Combine(Environment.GetEnvironmentVariable("DOTNET_ROOT")!, "dotnet"),
                typeof(NativePoolScenarioDriver).Assembly.Location,
                workerMode == null ? "pool-worker" : "pool-worker-" + workerMode, package, output
            }.Select(value => "\"" + value + "\"")),
            TimeoutSeconds = 15
        };
        var task = (contentMode != null ? GmWorkerBridgeTestFixtures.ValidationRepairTask() : GmWorkerBridgeTestFixtures.AnalysisTask()) with
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
        GmWorkerNativeLineageLaunch? capturedOwner = null;
        var helperWasLost = false;
        int? observedCompletion = null; GmWorkerProcessCompletionOutcomeKind? arbiterOutcome = null;
        var boundaryReached = false;
        var hooks = new GmWorkerBridgePoolHooks
        {
            AfterOwnerBound = owner =>
            {
                capturedOwner = (GmWorkerNativeLineageLaunch)owner;
                if (observationFault != null) capturedOwner.SetSyntheticObservationFault(observationFault);
            },
            BeforeCompletionArbitrationAsync = async original =>
            {
                if (boundaryMode?.EndsWith("completed", StringComparison.Ordinal) == true || faultMode is "owner-eof" or "status-loss" or "helper-loss")
                {
                    observedCompletion = await original.WaitAsync(TimeSpan.FromSeconds(5));
                    if (boundaryMode != null)
                    {
                        boundaryReached = true;
                        (boundaryMode.StartsWith("cancel-", StringComparison.Ordinal) ? cancellation : timeout).Cancel();
                    }
                    else
                    {
                        var originalHelper = OriginalSupervisor(capturedOwner!);
                        if (faultMode == "owner-eof") originalHelper.StandardInput.Close();
                        else if (faultMode == "status-loss") originalHelper.StandardOutput.Close();
                        else { originalHelper.Kill(); await originalHelper.WaitForExitAsync(); helperWasLost = true; }
                    }
                }
            },
            AfterCompletionArbitration = outcome => arbiterOutcome = outcome.Kind,
            BeforeWorkerReleaseAsync = async () =>
            {
                releases++;
                if (terminalMode == "cancel-release") cancellation.Cancel();
                if (terminalMode == "timeout-release") timeout.Cancel();
                if (helperLoss)
                {
                    var original = OriginalSupervisor(capturedOwner!);
                    original.Kill(); // Original retained Process only; outer guardian owns surviving host.
                    await original.WaitForExitAsync();
                    helperWasLost = true;
                }
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
            BeforeTaskReservationAsync = () => cleanupScenario?.BeforeReservation() ?? Task.CompletedTask,
            BeforeWorkspaceCleanupAsync = path => { cleanupPath = path; return cleanupScenario?.BeforeWorkspace(path) ?? Task.CompletedTask; },
            AfterQuarantineAuditTempCreatedAsync = path => cleanupScenario?.AfterReceiptTemp(path) ?? Task.CompletedTask,
            AfterQuarantineAuditPublishedAsync = path => cleanupScenario?.AfterReceiptPublished(path) ?? Task.CompletedTask,
            TimeoutSignal = timeout.Token
        };
        var reaper = new GmWorkerQuarantineReaper(capacity: 1, retrySchedule: [], runInBackground: false);
        var pool = new GmWorkerBridgePool(fs, null, cleanupScenario?.WithoutAudit == true ? null : new GmWorkerAuditLog(fs),
            hooks, GmWorkerProcessTreeFactory.Instance, reaper, new GmWorkerNativePoolAdmission(package, fixtureRoot));
        GmWorkerTaskRunResult? result = null; string? failure = null; var canceled = false;
        var executionClock = Stopwatch.StartNew();
        try
        {
            var pending = pool.RunTaskAsync(profile, task, cancellation.Token);
            Exception? boundaryFailure = null;
            if (boundaryCase && observationFault != null)
            {
                try
                {
                    await observationFault.Reached.WaitAsync(TimeSpan.FromSeconds(5));
                    boundaryReached = true;
                    (boundaryMode!.StartsWith("cancel-", StringComparison.Ordinal) ? cancellation : timeout).Cancel();
                }
                catch (Exception ex) { boundaryFailure = ex; }
                finally { observationFault.ReleaseObservation(); }
            }
            result = await pending;
            if (boundaryFailure != null) throw new InvalidOperationException("Boundary fixture did not reach its observation gate.", boundaryFailure);
        }
        catch (Exception ex) { failure = ex.ToString(); canceled = ex is OperationCanceledException; }
        executionClock.Stop();
        var beforeLateEntries = reaper.EntryCount;
        var beforeLateCapacity = reaper.OwnedCapacity;
        GmWorkerStopEvidence? lateStop = null;
        var lateOutputObservationSettled = false;
        var retainedSlotProbePending = false; var retainedSlotProbeCanceled = false;
        if (faultMode == "late-output")
        {
            using var probeCancellation = new CancellationTokenSource(TimeSpan.FromSeconds(2));
            var probe = pool.RunTaskAsync(profile, task with { TaskId = task.TaskId + "_held_probe" }, probeCancellation.Token);
            _ = await Task.WhenAny(probe, Task.Delay(100));
            retainedSlotProbePending = !probe.IsCompleted;
            probeCancellation.Cancel();
            try { _ = await probe; }
            catch (OperationCanceledException) { retainedSlotProbeCanceled = true; }
        }
        if (faultCase)
        {
            observationFault?.ReleaseObservation();
            if (faultMode == "late-output")
            {
                var originalObservation = (Task)typeof(GmWorkerNativeLineageLaunch).GetField("_outputObservation", BindingFlags.Instance | BindingFlags.NonPublic)!.GetValue(capturedOwner)!;
                await originalObservation.WaitAsync(TimeSpan.FromSeconds(5));
                lateOutputObservationSettled = originalObservation.IsCompletedSuccessfully;
            }
            lateStop = await capturedOwner!.StopAndObserveAsync();
            await Task.WhenAll(reaper.RunPassAsync(), reaper.RunPassAsync());
        }
        var cleanupReport = cleanupScenario == null ? null : await cleanupScenario.CompleteAsync(fs, reaper, pool, profile, task, result, capturedOwner);
        var stored = result?.Proposal == null ? null : await new GmWorkerProposalStore(fs).ReadProposalAsync(result.Proposal.ProposalId);
        await File.WriteAllTextAsync(Path.Combine(output, "scenario.json"), JsonSerializer.Serialize(new
        {
            success = result?.Status.State == WorkerBridgeState.Stopped && result.ExitCode == 0 && stored != null,
            failure = failure ?? result?.Status.LastError,
            canceled, resultReturned = result != null,
            mode, helperWasLost, releases, publicationCalls,
            boundaryReached, observedCompletion, arbiterOutcome,
            ownerTerminal = capturedOwner == null ? null : typeof(GmWorkerNativeLineageLaunch).GetField("_terminal", BindingFlags.Instance | BindingFlags.NonPublic)!.GetValue(capturedOwner),
            ownerUncertainty = capturedOwner == null ? null : typeof(GmWorkerNativeLineageLaunch).GetField("_uncertainty", BindingFlags.Instance | BindingFlags.NonPublic)!.GetValue(capturedOwner),
            observationFaultReached = observationFault?.Reached.IsCompletedSuccessfully == true,
            beforeLateEntries, beforeLateCapacity, lateStop, lateOutputObservationSettled, retainedSlotProbePending, retainedSlotProbeCanceled, cleanupReport,
            actualOutputTasksSettled = capturedOwner?.HostStandardOutput.IsCompletedSuccessfully == true && capturedOwner.HostStandardError.IsCompletedSuccessfully,
            cleanupConfirmedAuditCount = ReadAudit(fs).Count(entry => entry.EventType == "process-tree-cleanup-confirmed"),
            quarantineReceipts = Directory.Exists(Path.Combine(fixtureRoot, "native-runtime"))
                ? Directory.GetFiles(Path.Combine(fixtureRoot, "native-runtime"), "*.json", SearchOption.AllDirectories).Count(path => path.Contains("/quarantine-audit/", StringComparison.Ordinal)) : 0,
            retainedWorkspaces = Directory.Exists(Path.Combine(fixtureRoot, "native-runtime"))
                ? Directory.GetDirectories(Path.Combine(fixtureRoot, "native-runtime"), "game_session", SearchOption.AllDirectories).Length : 0,
            contentImportedExactly = stored?.ChangedFiles.Count == 1 && File.Exists(fs.ResolvePath(stored.ChangedFiles[0].ContentRef!)) &&
                (await File.ReadAllBytesAsync(fs.ResolvePath(stored.ChangedFiles[0].ContentRef!))).AsSpan().SequenceEqual(ProposedContent),
            inboxMatches = stored != null && (await File.ReadAllBytesAsync(fs.ResolvePath(GmWorkerBridgePool.GetProposalInboxPath(task.TaskId))))
                .AsSpan().SequenceEqual(Encoding.UTF8.GetBytes(GmWorkerJson.Serialize(stored))),
            workerStarts = File.Exists(Path.Combine(output, "worker-starts")) ? File.ReadAllLines(Path.Combine(output, "worker-starts")).Length : 0,
            workspaceCleaned = cleanupPath != null && !Directory.Exists(cleanupPath),
            reaperEntries = reaper.EntryCount, reaperCapacity = reaper.OwnedCapacity,
            canonicalContextUnchanged = await File.ReadAllTextAsync(fs.ResolvePath(contextPath)) == weather,
            validatedExecution = result?.HasValidatedExecutionFor(task) == true,
            proposalBytesMatch = result?.Proposal != null && File.Exists(fs.ResolvePath(GmWorkerProposalStore.GetProposalPath(result.Proposal.ProposalId))) && (await File.ReadAllBytesAsync(
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

    private static Process OriginalSupervisor(GmWorkerNativeLineageLaunch owner) =>
        (Process)typeof(GmWorkerNativeLineageLaunch).GetField("_supervisor", BindingFlags.Instance | BindingFlags.NonPublic)!.GetValue(owner)!;

    private static WorkerAuditEvent[] ReadAudit(FileSystemManager fs) => File.Exists(fs.ResolvePath(GmWorkerAuditLog.AuditLogPath))
        ? File.ReadAllLines(fs.ResolvePath(GmWorkerAuditLog.AuditLogPath)).Select(line => GmWorkerJson.Deserialize<WorkerAuditEvent>(line)!).ToArray() : [];

    private static GmWorkerNativeObservationFault? CreateObservationFault(string? fault, string? boundary) => (fault ?? boundary) switch
    {
        "wrong-run" => new(GmWorkerNativeObservationFaultKind.WrongRun),
        "wrong-scope" => new(GmWorkerNativeObservationFaultKind.WrongScope),
        "malformed" => new(GmWorkerNativeObservationFaultKind.MalformedTerminal),
        "uncertain" => new(GmWorkerNativeObservationFaultKind.ReportUncertain),
        "late-terminal" or "cancel-stop" or "timeout-stop" => new(GmWorkerNativeObservationFaultKind.HoldTerminal),
        "late-output" or "cancel-output" or "timeout-output" => new(GmWorkerNativeObservationFaultKind.HoldOutputs),
        _ => null
    };

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
        if (descendantMode != null && descendantMode != "nonzero" && !descendantMode.StartsWith("content-", StringComparison.Ordinal))
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
        if (descendantMode?.StartsWith("content-", StringComparison.Ordinal) == true)
        {
            var contentRef = "worker_proposals/" + proposal.ProposalId + "/game_state/world/weather.json";
            proposal = proposal with { ChangedFiles = [new WorkerChangedFile
            {
                Path = "game_state/world/weather.json", ChangeKind = WorkerFileChangeKind.Replace,
                BeforeSha256 = task.ContextFiles[0].Sha256,
                AfterSha256 = descendantMode == "content-bad-hash" ? new string('f', 64) : Convert.ToHexString(SHA256.HashData(ProposedContent)).ToLowerInvariant(),
                ContentRef = contentRef
            }] };
            if (descendantMode != "content-missing")
            {
                var path = Path.Combine(Environment.GetEnvironmentVariable(GmWorkerBridgePool.SessionPathEnvironmentVariable)!, contentRef);
                Directory.CreateDirectory(Path.GetDirectoryName(path)!);
                await File.WriteAllBytesAsync(path, ProposedContent);
            }
        }
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
