using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using BookOfEternityClient.Core;
using BookOfEternityClient.Services.GmWorkers;

namespace BookOfEternityClient.Tests;

internal static partial class NativePoolScenarioDriver
{
    private sealed class CleanupScenario(string mode)
    {
        private bool _probe, _blockAudit;
        private int _workspaceCalls, _tempCalls, _publishedCalls, _auditFailures, _probeCalls;
        private string? _workspace, _receipt;
        internal bool WithoutAudit => mode is "audit-unavailable" or "receipt-temp" or "receipt-ack" or "receipt-conflict";
        internal GmWorkerNativeObservationFault? ObservationFault { get; } = mode == "dispose"
            ? new(GmWorkerNativeObservationFaultKind.DisposeOnce) : null;

        internal FileSystemManagerHooks FileHooks => new()
        {
            BeforeCanonicalMutationAsync = path =>
            {
                if (_blockAudit && path == GmWorkerAuditLog.AuditLogPath)
                {
                    _auditFailures++;
                    throw new IOException("Synthetic canonical audit unavailable at its actual mutation boundary.");
                }
                return Task.CompletedTask;
            }
        };

        internal Task BeforeReservation()
        {
            if (_probe)
            {
                _probeCalls++;
                throw new InvalidOperationException("Slot probe intentionally stops before reservation or launch.");
            }
            return Task.CompletedTask;
        }

        internal Task BeforeWorkspace(string path)
        {
            _workspace = path;
            if (++_workspaceCalls == 1 && mode != "dispose")
                throw new IOException("Synthetic one-shot workspace cleanup failure.");
            return Task.CompletedTask;
        }

        internal Task AfterReceiptTemp(string _)
        {
            if (++_tempCalls == 1 && mode == "receipt-temp")
                throw new IOException("Synthetic receipt temp failure.");
            return Task.CompletedTask;
        }

        internal Task AfterReceiptPublished(string path)
        {
            _receipt = path;
            _publishedCalls++;
            if (_publishedCalls == 1 && mode is "receipt-ack" or "receipt-conflict")
            {
                if (mode == "receipt-conflict") File.WriteAllText(path, "{\"conflict\":true}", new UTF8Encoding(false));
                throw new IOException("Synthetic lost publication acknowledgement.");
            }
            return Task.CompletedTask;
        }

        internal async Task<object> CompleteAsync(FileSystemManager fs, GmWorkerQuarantineReaper reaper,
            GmWorkerBridgePool pool, WorkerBridgeProfile profile, WorkerTaskPacket task, GmWorkerTaskRunResult? run,
            GmWorkerNativeLineageLaunch? owner)
        {
            if (_workspace == null && owner != null) _workspace = OriginalSupervisor(owner).StartInfo.WorkingDirectory;
            var initial = Snapshot(reaper, owner);
            var acceptedBefore = run?.HasValidatedExecutionFor(task) == true && run.Proposal != null &&
                File.Exists(fs.ResolvePath(GmWorkerProposalStore.GetProposalPath(run.Proposal.ProposalId)));
            var proposalBytes = run?.Proposal == null ? null : File.ReadAllBytes(fs.ResolvePath(GmWorkerProposalStore.GetProposalPath(run.Proposal.ProposalId)));
            if (mode == "receipt-replaced")
            {
                await using var lifecycle = await fs.AcquireSessionLifecycleLeaseAsync();
                await using var replacement = await fs.AcquireSessionReplacementWriteLeaseAsync(lifecycle);
                fs.RotateSessionGeneration(replacement);
            }
            _blockAudit = mode == "audit-failure";
            await reaper.RunPassAsync();
            var first = Snapshot(reaper, owner);
            byte[]? firstReceipt = _receipt == null ? null : File.ReadAllBytes(_receipt);
            if (_receipt != null) File.SetLastWriteTimeUtc(_receipt, new DateTime(2020, 1, 2, 3, 4, 5, DateTimeKind.Utc));
            _blockAudit = false;
            await Task.WhenAll(Enumerable.Range(0, 8).Select(_ => reaper.RunPassAsync()));
            await reaper.RunPassAsync();
            var final = Snapshot(reaper, owner);
            var receiptStable = firstReceipt == null || (_receipt != null &&
                firstReceipt.AsSpan().SequenceEqual(File.ReadAllBytes(_receipt)) &&
                File.GetLastWriteTimeUtc(_receipt) == new DateTime(2020, 1, 2, 3, 4, 5, DateTimeKind.Utc));
            var capacityReusable = false;
            var probeStoppedBeforeReservation = false;
            if (reaper.OwnedCapacity == 0)
            {
                using (var firstReservation = reaper.TryReserve())
                using (var blockedReservation = reaper.TryReserve())
                    capacityReusable = firstReservation != null && blockedReservation == null;
                _probe = true;
                using var deadline = new CancellationTokenSource(TimeSpan.FromSeconds(2));
                var probeTask = task with { TaskId = task.TaskId + "_slot_probe" };
                var probe = await pool.RunTaskAsync(profile, probeTask, deadline.Token);
                probeStoppedBeforeReservation = _probeCalls == 1 && probe.Status.LastError?.Contains("Slot probe intentionally", StringComparison.Ordinal) == true &&
                    !File.Exists(fs.ResolvePath(GmWorkerBridgePool.GetTaskPacketPath(probeTask.TaskId)));
            }
            var audit = ReadAudit(fs);
            var receiptBound = false;
            if (_receipt != null && mode != "receipt-conflict")
            {
                using var receipt = JsonDocument.Parse(File.ReadAllBytes(_receipt));
                var evidence = receipt.RootElement.GetProperty("auditEvent");
                receiptBound = receipt.RootElement.GetProperty("sessionGeneration").GetString() == task.SessionGeneration &&
                    evidence.GetProperty("eventType").GetString() == "process-tree-cleanup-confirmed" &&
                    evidence.GetProperty("workerId").GetString() == task.WorkerId && evidence.GetProperty("taskId").GetString() == task.TaskId &&
                    !string.IsNullOrWhiteSpace(evidence.GetProperty("eventId").GetString());
            }
            return new
            {
                mode, acceptedBefore, initial, first, final, receiptStable, receiptBound,
                capacityReusable, probeStoppedBeforeReservation,
                auditFailures = _auditFailures, receiptTempCalls = _tempCalls, receiptPublishedCalls = _publishedCalls,
                cleanupConfirmedEvents = audit.Count(entry => entry.EventType == "process-tree-cleanup-confirmed"),
                proposalReceivedEvents = audit.Count(entry => entry.EventType == "proposal-received"),
                proposalUnchanged = mode == "receipt-replaced" || (proposalBytes != null && proposalBytes.AsSpan().SequenceEqual(
                    File.ReadAllBytes(fs.ResolvePath(GmWorkerProposalStore.GetProposalPath(run!.Proposal!.ProposalId))))),
                receiptSha256 = _receipt == null ? null : Convert.ToHexString(SHA256.HashData(File.ReadAllBytes(_receipt))),
                conflictingReceiptRetained = mode != "receipt-conflict" || (_receipt != null && File.ReadAllText(_receipt) == "{\"conflict\":true}"),
                capacityAfterProbe = reaper.OwnedCapacity
            };
        }

        private static bool RuntimeAuthorityRetained(GmWorkerQuarantineReaper reaper)
        {
            const System.Reflection.BindingFlags flags = System.Reflection.BindingFlags.Instance | System.Reflection.BindingFlags.NonPublic;
            var entries = (System.Collections.IEnumerable)typeof(GmWorkerQuarantineReaper).GetField("_entries", flags)!.GetValue(reaper)!;
            foreach (var pair in entries)
            {
                var entry = pair.GetType().GetProperty("Value")!.GetValue(pair)!;
                var owner = entry.GetType().GetField("_owner", flags)!.GetValue(entry)!;
                var workspace = typeof(GmWorkerQuarantinedExecution).GetField("_workspace", flags)!.GetValue(owner);
                if (workspace != null && typeof(GmWorkerExecutionWorkspace).GetField("_runtimeRootAuthority", flags)!.GetValue(workspace) != null) return true;
            }
            return false;
        }

        private object Snapshot(GmWorkerQuarantineReaper reaper, GmWorkerNativeLineageLaunch? owner) => new
        {
            entries = reaper.EntryCount, capacity = reaper.OwnedCapacity, workspaceHookCalls = _workspaceCalls,
            runtimeAuthorityRetained = RuntimeAuthorityRetained(reaper),
            workspaceExists = _workspace != null && Directory.Exists(_workspace),
            workspaceRootExists = _workspace != null && Directory.Exists(Path.GetDirectoryName(_workspace)),
            disposeAttempts = ObservationFault?.DisposeAttempts ?? 0,
            ownerDisposed = owner != null && (bool)typeof(GmWorkerNativeLineageLaunch).GetField("_disposed", System.Reflection.BindingFlags.Instance | System.Reflection.BindingFlags.NonPublic)!.GetValue(owner)!
        };
    }
}
