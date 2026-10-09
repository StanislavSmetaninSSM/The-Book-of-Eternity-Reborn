using System.Collections;
using System.Reflection;
using System.Text.Json;
using BookOfEternityClient.Core;
using BookOfEternityClient.Services;
using BookOfEternityClient.Services.GmWorkers;
using Microsoft.Extensions.Logging.Abstractions;
using static BookOfEternityClient.Tests.WorkerStorageOutcomeProbe;

namespace BookOfEternityClient.Tests;

internal static partial class NativePoolScenarioDriver
{
    private static async Task<int> RunStorageOutcome(string mode, string package, string output)
    {
        var root = Path.Combine(output, "state-copy");
        var evidence = new Dictionary<string, object?> { ["Mode"] = mode, ["WorkerFixtureRoot"] = root };
        using var probe = new WorkerStorageOutcomeProbe();
        GmWorkerNativePoolAdmission? admission = null;
        GmWorkerNativeLineageLaunch? owner = null;
        GmWorkerProcessHostLaunch? host = null;
        GmWorkerDurableExecution? execution = null;
        Task<int>? originalCompletion = null;
        Task<GmWorkerTaskRunResult>? originalRun = null;
        const BindingFlags flags = BindingFlags.Instance | BindingFlags.NonPublic;
        object? Field(object? value, string name) => value?.GetType().GetField(name, flags)?.GetValue(value);
        object? CanonicalFailure() => execution?.Authority.GetType().GetProperty("CanonicalPublicationFailure", flags)?.GetValue(execution.Authority);
        bool Ack() => execution != null && FenceField<bool>(execution, "_publicationAcknowledged");
        bool Recorded() => execution != null && Field(execution.Authority, "_publication") != null;
        var disposal = mode == "inbox_unknown_dispose" ? new GmWorkerNativeObservationFault(GmWorkerNativeObservationFaultKind.DisposeOnce) : null;
        try
        {
            Directory.CreateDirectory(root);
            var known = new IOException("known original durable worker failure");
            var knownHits = 0; var workspaceCalls = 0; var reaperPasses = 0; var releases = 0;
            var early = mode is "terminal_audit_unknown" or "timeout_audit_unknown" or "cancelled" or "known_failure";
            var deferred = mode is "required_audit_unknown" or "cleanup_audit_unknown" or "reaper_audit_unknown" or "required_audit_unavailable";
            var delayed = mode is "required_audit_unknown" or "reaper_audit_unknown";
            var uncertain = mode.Contains("unknown", StringComparison.Ordinal);
            var fileHooks = probe.Hooks;
            FileSystemManager fs = null!;
            string inboxRelative = "";
            bool IsPath(string path, string relative) => path == relative || path == fs.ResolvePath(relative);
            fs = new FileSystemManager(root, NullLogger<FileSystemManager>.Instance, PhysicalLoadTransactionOperations.Instance,
                new FileSystemManagerHooks
                {
                    LocalPublicationObserver = fileHooks.LocalPublicationObserver,
                    LocalPublicationRecoveryObserver = fileHooks.LocalPublicationRecoveryObserver,
                    AfterCanonicalReadInitialValidationAsync = fileHooks.AfterCanonicalReadInitialValidationAsync,
                    SessionOperationClosingAsync = fileHooks.SessionOperationClosingAsync,
                    BeforeCanonicalWriteLockOpenAsync = fileHooks.BeforeCanonicalWriteLockOpenAsync,
                    BeforeCanonicalMutationBoundaryAsync = async path =>
                    {
                        await fileHooks.BeforeCanonicalMutationBoundaryAsync!(path);
                        if (Ack() && (mode == "inbox_known" && IsPath(path, inboxRelative) || mode == "audit_known" && IsPath(path, GmWorkerAuditLog.AuditLogPath)))
                        { knownHits++; throw known; }
                    }
                });
            probe.Attach(fs);
            fs.EnsureDirectoryStructure();
            string generation;
            await using (var lease = await fs.AcquireCanonicalWriteLeaseAsync())
            {
                generation = fs.GetOrCreateSessionGeneration(lease);
                await fs.WriteFileAtomicBytesAsync(lease, RestartContextPath, RestartContextBytes);
            }
            var task = OwnershipTask("restart_storage_" + mode) with { SessionGeneration = generation };
            var profile = OwnershipProfile(package, output);
            var taskPath = fs.ResolvePath(GmWorkerBridgePool.GetTaskPacketPath(task.TaskId));
            inboxRelative = GmWorkerBridgePool.GetProposalInboxPath(task.TaskId);
            var inboxPath = fs.ResolvePath(inboxRelative);
            var auditPath = fs.ResolvePath(GmWorkerAuditLog.AuditLogPath);
            var bundleRoot = fs.ResolvePath(GmWorkerProposalStore.ProposalRoot + "/worker_proposal_" + task.TaskId);
            var ledgerPath = Path.Combine(root, ".boe_runtime", "worker-runs-v1", "state.json");
            WorkerRunRecord Record() => GmWorkerRunLedgerCodec.Decode(new(root), File.ReadAllBytes(ledgerPath)).Entries.Single();
            WorkerAuditEvent LastAudit() => GmWorkerJson.Deserialize<WorkerAuditEvent>(File.ReadLines(auditPath).Last().TrimStart('\uFEFF'))!;
            Dictionary<string, byte[]> Snapshot(string? path) => path != null && Directory.Exists(path)
                ? Directory.EnumerateFiles(path, "*", SearchOption.AllDirectories).Where(p => !p.EndsWith(".lock", StringComparison.Ordinal))
                    .Order(StringComparer.Ordinal).ToDictionary(p => Path.GetRelativePath(path, p), File.ReadAllBytes, StringComparer.Ordinal)
                : new(StringComparer.Ordinal);
            bool Same(Dictionary<string, byte[]> before, Dictionary<string, byte[]> after) => before.Count == after.Count && before.All(p => after.TryGetValue(p.Key, out var value) && Equal(p.Value, value));
            var expectedEvent = mode switch
            {
                "derived_audit_unknown" => "proposal-received",
                "required_audit_unknown" => "process-tree-cleanup-confirmed",
                "terminal_audit_unknown" => "task-failed",
                "cleanup_audit_unknown" => "process-tree-cleanup-unconfirmed",
                "reaper_audit_unknown" => "process-tree-cleanup-retry-failed",
                "timeout_audit_unknown" => "task-timed-out",
                _ => null
            };
            object? factsAtCut = null;
            Dictionary<string, byte[]>? bundleAtCut = null, workspaceAtCut = null;
            string? workspacePath = null, receiptPath = null;
            byte[]? receiptBytes = null;
            var slotKey = fs.GameSessionPath.TrimEnd(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar) + "|" + profile.WorkerId;
            bool SlotHeld() => ((IDictionary)typeof(GmWorkerBridgePool).GetField("WorkerConcurrencyGates", BindingFlags.Static | BindingFlags.NonPublic)!.GetValue(null)!).Contains(slotKey);
            var reaper = new GmWorkerQuarantineReaper(1, retrySchedule: [], runInBackground: false);
            probe.Select = path => uncertain && execution != null && (mode.StartsWith("inbox_unknown", StringComparison.Ordinal)
                ? path == inboxPath && Ack()
                : path == auditPath && LastAudit().EventType == expectedEvent);
            probe.BeforeCut = () =>
            {
                var record = Record();
                var reservedBytes = File.ReadAllBytes(taskPath);
                var reserved = GmWorkerJson.Deserialize<WorkerTaskPacket>(System.Text.Encoding.UTF8.GetString(reservedBytes).TrimStart('\uFEFF'))!;
                Require(reserved.TaskId == task.TaskId && reserved.WorkerId == task.WorkerId && reserved.SessionGeneration == generation
                    && record.Identity.TaskId == task.TaskId && record.Identity.TaskSha256 == GmWorkerRunLedgerCodec.Hash(reservedBytes), "cut lost real reserved task/generation/ledger binding");
                Require(reserved.ContextFiles.Count == 1 && reserved.ContextFiles[0].Path == RestartContextPath
                    && reserved.ContextFiles[0].Sha256 == GmWorkerRunLedgerCodec.Hash(File.ReadAllBytes(fs.ResolvePath(RestartContextPath))),
                    "cut lost exact original pinned context bytes");
                if (expectedEvent != null)
                {
                    var audit = LastAudit();
                    Require(audit.EventType == expectedEvent && audit.TaskId == task.TaskId && audit.WorkerId == task.WorkerId, "cut missed exact original audit event");
                    if (mode is "terminal_audit_unknown" or "cleanup_audit_unknown" or "reaper_audit_unknown")
                        Require(audit.Summary.Contains(known.Message, StringComparison.Ordinal), "audit cut lost known original cause");
                }
                if (!early) Require(Ack() && record.Progress?.Publication?.Committed == true, "derived cut lacks actual durable publication ACK");
                Require(execution!.Authority.StopEvidence?.State == GmWorkerStopState.StoppedWithinScope && execution.Authority.OutputsSettled,
                    "publication/diagnostic cut lacks original stop/output facts");
                workspacePath = record.Identity.WorkspacePath;
                bundleAtCut = Snapshot(bundleRoot); workspaceAtCut = Snapshot(workspacePath);
                factsAtCut = new
                {
                    record.Identity, record.Progress, diskPhase = record.Phase.ToString(),
                    livePhase = FenceField<WorkerRunRecord>(execution, "_record")!.Phase.ToString(),
                    publicationAcknowledged = Ack(), originalPublicationRecorded = Recorded(),
                    task = reserved, taskBytes = reservedBytes, actualGeneration = generation,
                    audit = expectedEvent == null ? null : LastAudit(), reaperPasses,
                    workerStarts = FenceWorkerStarts(output), originalCompletionSettled = originalCompletion?.IsCompleted,
                    stop = execution.Authority.StopEvidence, execution.Authority.OutputsSettled,
                    slotHeld = SlotHeld(), reaper.EntryCount, reaper.OwnedCapacity,
                    workspaceExists = Directory.Exists(workspacePath), bundleAtCut, workspaceAtCut
                };
            };
            using var cancelled = new CancellationTokenSource();
            using var timeout = new CancellationTokenSource();
            var hooks = new GmWorkerBridgePoolHooks
            {
                AfterOwnerBound = actual =>
                {
                    owner = (GmWorkerNativeLineageLaunch)actual;
                    execution = FenceField<GmWorkerDurableExecution>(owner, "_durable")!;
                    if (disposal != null) owner.SetSyntheticObservationFault(disposal);
                },
                AfterHostPrepared = actual => host = actual,
                BeforeWorkerReleaseAsync = () =>
                {
                    releases++;
                    if (mode is "terminal_audit_unknown" or "known_failure") { knownHits++; throw known; }
                    if (mode == "timeout_audit_unknown") timeout.Cancel();
                    if (mode == "cancelled") cancelled.Cancel();
                    return Task.CompletedTask;
                },
                BeforeCompletionArbitrationAsync = actual => { originalCompletion = actual; return Task.CompletedTask; },
                BeforeWorkspaceCleanupAsync = path =>
                {
                    workspacePath = path; workspaceCalls++;
                    if (deferred && workspaceCalls <= (mode == "reaper_audit_unknown" ? 2 : 1)) { knownHits++; throw known; }
                    return Task.CompletedTask;
                },
                AfterQuarantineAuditPublishedAsync = path => { receiptPath = path; receiptBytes = File.ReadAllBytes(path); return Task.CompletedTask; },
                TimeoutSignal = timeout.Token
            };
            admission = new(package, root, durable: true);
            var pool = new GmWorkerBridgePool(fs, null, mode == "required_audit_unavailable" ? null : new GmWorkerAuditLog(fs), hooks,
                GmWorkerProcessTreeFactory.Instance, reaper, admission);
            GmWorkerTaskRunResult? result = null;
            Exception? failure = null;
            originalRun = pool.RunTaskAsync(profile, task, cancelled.Token);
            try { result = await originalRun; }
            catch (Exception caught) { failure = caught; }
            var acceptedBeforeReaper = result?.HasValidatedExecutionFor(task) == true;
            var atReturn = new { slotHeld = SlotHeld(), reaper.EntryCount, reaper.OwnedCapacity, workspaceCalls,
                workspaceExists = workspacePath != null && Directory.Exists(workspacePath), disposeAttempts = disposal?.DisposeAttempts ?? 0 };
            if (deferred || disposal != null)
            {
                reaperPasses++; await reaper.RunPassAsync();
            }
            var firstReaperAccepted = result?.HasValidatedExecutionFor(task) == true;
            if (uncertain)
            {
                reaperPasses++; await reaper.RunPassAsync();
            }
            var recordAfter = Record();
            var cleanupOwner = Field(execution, "_cleanupOwner");
            var physicalSettled = cleanupOwner != null && Field(cleanupOwner, "_owner") == null
                && Field(cleanupOwner, "_processHostLaunch") == null && Field(cleanupOwner, "_workerCompletionTask") == null;
            var actualStop = execution!.Authority.StopEvidence;
            var bundleAfter = Snapshot(bundleRoot);
            var workspaceAfter = Snapshot(workspacePath);
            evidence["WorkerStorage"] = new
            {
                mode, Failure = failure?.ToString(), ResultReturned = result != null, Status = result?.Status.State.ToString(),
                result?.TimedOut, result?.ExitCode, DispatchSettled = originalRun.IsCompleted,
                SameOriginalUncertainty = probe.OriginalUncertainty != null && ReferenceEquals(probe.OriginalUncertainty, failure),
                SameRetainedUncertainty = probe.OriginalUncertainty != null && ReferenceEquals(probe.OriginalUncertainty, CanonicalFailure()),
                OriginalCauseRetained = ReferenceEquals(failure?.Data["GmWorkerOriginalFailure"], known),
                CleanupFailureRetained = probe.OriginalUncertainty?.Data["GmWorkerCleanupFailure"] is Exception,
                TimeoutFactRetained = failure?.Data["GmWorkerTerminalOutcome"]?.ToString(),
                generation, task, taskBytes = Bytes(taskPath), factsAtCut, atReturn, releases, knownHits, workspaceCalls, reaperPasses,
                acceptedBeforeReaper, firstReaperAccepted, acceptedAfter = result?.HasValidatedExecutionFor(task) == true,
                recordAfter.Identity, recordAfter.Progress, diskPhase = recordAfter.Phase.ToString(),
                livePhase = FenceField<WorkerRunRecord>(execution, "_record")!.Phase.ToString(),
                publicationAcknowledged = Ack(), originalPublicationRecorded = Recorded(), execution.RetirementAcknowledged,
                actualStop, execution.Authority.OutputsSettled, execution.IsUncertain,
                originalCompletionSettled = originalCompletion?.IsCompleted, workerStarts = FenceWorkerStarts(output),
                ownerOutputTasksSettled = owner!.HostStandardOutput.IsCompletedSuccessfully && owner.HostStandardError.IsCompletedSuccessfully,
                physicalSettled, disposeAttempts = disposal?.DisposeAttempts ?? 0, slotHeld = SlotHeld(), reaper.EntryCount, reaper.OwnedCapacity,
                workspaceExists = workspacePath != null && Directory.Exists(workspacePath), bundleAfter, workspaceAfter,
                receiptPath, receiptBytes, AuditAfter = Bytes(auditPath), Cut = probe.Evidence()
            };
            Require(originalRun.IsCompleted && releases == 1 && owner != null && execution != null, "original pool/owner was not reached and settled");
            Require(actualStop?.State == GmWorkerStopState.StoppedWithinScope && execution.Authority.OutputsSettled && !execution.IsUncertain,
                "canonical outcome replaced original native stop/output facts");
            Require(owner.HostStandardOutput.IsCompletedSuccessfully && owner.HostStandardError.IsCompletedSuccessfully && physicalSettled,
                "original owner/output/waiter/host physical phases did not settle");
            Require(early ? originalCompletion == null && FenceWorkerStarts(output) == 0 : originalCompletion?.IsCompleted == true && FenceWorkerStarts(output) == 1,
                "original worker completion/start facts differ from the selected path");
            if (Ack())
            {
                var published = bundleAfter["proposal.json"];
                var proposal = GmWorkerJson.Deserialize<WorkerProposal>(System.Text.Encoding.UTF8.GetString(published).TrimStart('\uFEFF'))!;
                Require(proposal.TaskId == task.TaskId && proposal.WorkerId == task.WorkerId
                    && recordAfter.Progress?.Publication?.ProposalSha256 == GmWorkerRunLedgerCodec.Hash(published)
                    && proposal.ChangedFiles.Count == 1
                    && Equal(bundleAfter[RestartContextPath], ProposedContent), "actual acknowledged bundle/context bytes are not the original worker proposal");
            }
            if (uncertain)
            {
                probe.RequireStopped();
                Require(ReferenceEquals(probe.OriginalUncertainty, CanonicalFailure()), "original execution did not retain first canonical uncertainty");
                Require(SlotHeld() && reaper.EntryCount == 1 && reaper.OwnedCapacity == 1 && !execution.RetirementAcknowledged,
                    "canonical uncertainty released original slot/root/quarantine capacity");
                Require(Same(bundleAtCut!, bundleAfter), "actual durable bundle changed after uncertainty");
                Require(Same(workspaceAtCut!, workspaceAfter), "original detached workspace changed after uncertainty");
                Require(receiptBytes == null, "uncertain canonical audit was replaced by local success receipt");
                if (delayed)
                {
                    Require(acceptedBeforeReaper && !firstReaperAccepted && result != null && failure == null,
                        "actual later audit uncertainty did not revoke only subsequent acceptance");
                    Require(Recorded() && Ack(), "later uncertainty erased actual original publication/ACK");
                }
                else Require(ReferenceEquals(probe.OriginalUncertainty, failure) && result == null,
                    "original pool did not propagate exact canonical uncertainty");
                if (mode.StartsWith("inbox_unknown", StringComparison.Ordinal) || mode == "derived_audit_unknown")
                    Require(Ack() && !Recorded() && workspaceCalls == 0 && Directory.Exists(workspacePath), "Store uncertainty forged acceptance or removed its original workspace");
                if (early) Require(workspaceCalls == 0 && Directory.Exists(workspacePath), "terminal diagnostic uncertainty removed the original workspace before settlement");
                if (mode == "terminal_audit_unknown") Require(ReferenceEquals(failure!.Data["GmWorkerOriginalFailure"], known), "original worker cause was lost");
                if (mode == "timeout_audit_unknown") Require(failure!.Data["GmWorkerTerminalOutcome"]?.ToString() == "TimedOut", "actual decided timeout was lost");
                if (disposal != null) Require(disposal.DisposeAttempts >= 2 && probe.OriginalUncertainty!.Data["GmWorkerCleanupFailure"] is Exception,
                    "secondary original disposal failure was not retained and physically settled");
            }
            else
            {
                Require(probe.Cuts == 0 && probe.OriginalUncertainty == null && !File.Exists(probe.JournalPath), "known control retained uncertain canonical storage");
                Require(!SlotHeld() && reaper.EntryCount == 0 && reaper.OwnedCapacity == 0 && execution.RetirementAcknowledged,
                    "known control did not retire original capacity");
                if (mode == "cancelled") Require(failure is OperationCanceledException && result == null, "actual cancellation changed outcome");
                else if (mode == "known_failure") Require(failure == null && result?.Status.State == WorkerBridgeState.Failed && knownHits == 1, "known worker failure policy changed");
                else Require(failure == null && result?.HasValidatedExecutionFor(task) == true && Ack() && Recorded(), "known warning/success lost actual published authority");
                if (mode is "inbox_known" or "audit_known") Require(knownHits > 0, "known derived failure was not reached");
                if (mode == "required_audit_unavailable") Require(receiptBytes != null && receiptPath != null && knownHits == 1,
                    "ordinary absent-audit fallback was not actually published");
            }
            evidence["Success"] = true;
            return 0;
        }
        catch (Exception failure) { evidence["Failure"] = failure.ToString(); return 1; }
        finally
        {
            // Separate fixture teardown never manufactures publication, retirement,
            // slot release or a successful assertion; retained logical debt is recorded above.
            try
            {
                if (owner != null) { _ = await owner.StopAndObserveAsync(); await owner.DisposeAsync(); }
                if (originalCompletion != null)
                {
                    try { await originalCompletion.WaitAsync(TimeSpan.FromSeconds(5)); }
                    catch when (originalCompletion.IsCompleted) { }
                }
                if (host != null) await host.DisposeAsync();
                admission?.Dispose();
            }
            catch (Exception failure) { evidence["FixtureCleanupFailure"] = failure.ToString(); }
            await File.WriteAllTextAsync(Path.Combine(output, "scenario.json"), JsonSerializer.Serialize(evidence));
        }
    }
}
