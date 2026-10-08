using System.Reflection;
using System.Text;
using System.Text.Json;
using BookOfEternityClient.Core;
using BookOfEternityClient.Services.GmRuntime;
using BookOfEternityClient.Services.GmWorkers;
using Microsoft.Extensions.Logging.Abstractions;

namespace BookOfEternityClient.Tests;

// Actual original main + production pool audit callback/reaper/receipt. The
// worker cleanup authority is explicitly NoLaunch; no worker process is started.
internal static class MainWorkerCleanupScenario
{
    internal static async Task RunAsync(string mode, string root, string folder,
        GmSessionRunCoordinator main, Func<Task> stopMain, Dictionary<string, object?> result)
    {
        void Require(bool condition, string message)
        { if (!condition) throw new InvalidOperationException(message); }
        var publicationFault = mode.StartsWith("publication", StringComparison.Ordinal);
        var debtAfterWithdrawal = mode is "publication-expired" or "publication-retired";
        var liveFault = mode == "metadata" || publicationFault;
        var recordPath = Path.Combine(root, ".boe_runtime/gm-runs/main.json");
        var journalPath = Path.Combine(root, ".boe_runtime/trusted-local-publication-v1/active.json");
        var originalRecord = File.ReadAllBytes(recordPath);
        var unknown = Encoding.UTF8.GetBytes("unknown canonical audit image");
        byte[]? published = null, journal = null;
        var publicationCuts = 0;
        var cutEnabled = publicationFault;
        FileSystemManager? files = null;
        files = new(root, NullLogger<FileSystemManager>.Instance, PhysicalLoadTransactionOperations.Instance,
            new FileSystemManagerHooks
            {
                LocalPublicationObserver = (phase, index) =>
                {
                    if (!cutEnabled || phase != TrustedLocalPublicationPhase.MemberPublished) return;
                    using var parsed = JsonDocument.Parse(File.ReadAllBytes(journalPath));
                    var members = parsed.RootElement.GetProperty("Members");
                    Require(index == 0 && members.GetArrayLength() == 1 &&
                        members[0].GetProperty("Path").GetString() == files!.ResolvePath(GmWorkerAuditLog.AuditLogPath),
                        "Cleanup fixture did not reach the actual audit member.");
                    published = File.ReadAllBytes(files!.ResolvePath(GmWorkerAuditLog.AuditLogPath));
                    Require(Encoding.UTF8.GetString(published).Contains("cleanup-main-stable", StringComparison.Ordinal),
                        "Actual publication did not contain the original cleanup event.");
                    publicationCuts++;
                    File.WriteAllBytes(files.ResolvePath(GmWorkerAuditLog.AuditLogPath), unknown);
                    journal = File.ReadAllBytes(journalPath);
                    throw new InvalidOperationException("actual required cleanup audit publication cut");
                }
            });
        var generation = main.Identity.GenerationId;
        var auditEvent = new WorkerAuditEvent
        {
            EventId = "cleanup-main-stable", EventType = "process-tree-cleanup-confirmed",
            WorkerId = "fixture-worker", TaskId = "fixture-task", TimestampUtc = "2026-10-08T00:00:00Z",
            Summary = "Original NoLaunch cleanup receipt", Details = new Dictionary<string, IReadOnlyList<string>>()
        };
        var task = GmWorkerBridgeTestFixtures.AnalysisTask() with
        { TaskId = auditEvent.TaskId, WorkerId = auditEvent.WorkerId, SessionGeneration = generation, ContextFiles = [] };
        var pool = new GmWorkerBridgePool(files, auditLog: new GmWorkerAuditLog(files));
        var append = typeof(GmWorkerBridgePool).GetMethod("RecordRequiredTerminalEventOnceAsync", BindingFlags.Instance | BindingFlags.NonPublic)!;
        var runtimeBase = Path.Combine(folder, "cleanup-runtime");
        var runtime = GmWorkerExecutionWorkspace.ResolveRuntimeRoot(root, runtimeBase);
        var receipt = Path.Combine(runtime, GmWorkerExecutionWorkspace.QuarantineAuditDirectoryName, auditEvent.EventId + ".json");
        var receiptCuts = 0;
        var failReceipt = mode == "receipt-retry";
        var hooks = new GmWorkerExecutionWorkspaceHooks
        {
            AfterQuarantineAuditPublishedAsync = path =>
            {
                Require(path == receipt, "Receipt cut selected another event.");
                receiptCuts++;
                return failReceipt ? Task.FromException(new IOException("actual local cleanup receipt ACK cut")) : Task.CompletedTask;
            }
        };
        GmWorkerExecutionWorkspace? workspace = null;
        var slot = new Slot();
        var failures = new List<Exception>();
        var appendCalls = 0;
        var firstCleanup = true;
        var reaper = new GmWorkerQuarantineReaper(1, [], runInBackground: false);
        var resume = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        Task? deferred = null;
        try
        {
            await main.RunOperationAsync(async () =>
            {
                workspace = await GmWorkerExecutionWorkspace.CreateAsync(files, task, CancellationToken.None, hooks, runtimeBase);
                var cleanup = new GmWorkerQuarantinedExecution("original-main/no-launch", GmWorkerExecutionAuthority.NoLaunch(task),
                    null, null, workspace, slot, null,
                    _ =>
                    {
                        if (firstCleanup) { firstCleanup = false; return Task.FromException(new IOException("defer original no-launch cleanup once")); }
                        return Task.CompletedTask;
                    }, generation, auditEvent,
                    async () =>
                    {
                        appendCalls++;
                        return await (Task<GmWorkerAuditAppendDisposition>)append.Invoke(pool, [generation, auditEvent, null])!;
                    }, failure => { failures.Add(failure); return Task.CompletedTask; });
                using var reservation = reaper.TryReserve() ?? throw new InvalidOperationException("Fixture quarantine capacity unavailable.");
                reservation.Transfer(cleanup);
                await reaper.RunPassAsync();
                Require(failures.Count == 1 && appendCalls == 0 && slot.Disposals == 0 && reaper.EntryCount == 1 &&
                    reaper.OwnedCapacity == 1 && Directory.Exists(workspace.GameSessionPath), "Initial cleanup did not retain its original owner before audit.");
                result["InitialCleanupRetained"] = true;
                if (liveFault)
                {
                    if (mode == "metadata") File.WriteAllBytes(recordPath, Encoding.UTF8.GetBytes("{malformed-main"));
                    await reaper.RunPassAsync();
                    result["LiveFaultAppendCalls"] = appendCalls;
                    result["PublicationCuts"] = publicationCuts;
                    result["LiveFaultRetainedSlot"] = slot.Disposals == 0 && reaper.EntryCount == 1 && reaper.OwnedCapacity == 1;
                    result["LiveFaultReceiptAbsent"] = !File.Exists(receipt);
                    result["LiveFaultFailure"] = failures.LastOrDefault()?.ToString();
                    Require(appendCalls == 1 && failures.Count == 2 && slot.Disposals == 0 && reaper.EntryCount == 1 &&
                        reaper.OwnedCapacity == 1 && !File.Exists(receipt), "Live original storage failure was converted to successful local cleanup.");
                    if (publicationFault)
                    {
                        Require(publicationCuts == 1 && failures[^1] is CoordinatedStatePublicationUncertainException &&
                            File.ReadAllBytes(files.ResolvePath(GmWorkerAuditLog.AuditLogPath)).AsSpan().SequenceEqual(unknown) &&
                            File.ReadAllBytes(journalPath).AsSpan().SequenceEqual(journal), "Actual audit uncertainty/evidence was lost.");
                        if (debtAfterWithdrawal)
                        {
                            var established = failures[^1];
                            deferred = Task.Run(async () =>
                            {
                                await resume.Task;
                                await reaper.RunPassAsync();
                                result["PriorAuditUncertainty"] = established.ToString();
                                result["PostWithdrawalFailure"] = failures.LastOrDefault()?.ToString();
                                result["PostWithdrawalOwnerRetained"] = slot.Disposals == 0 && reaper.EntryCount == 1 && reaper.OwnedCapacity == 1;
                                result["PostWithdrawalReceiptAbsent"] = !File.Exists(receipt);
                                Require(publicationCuts == 1 && appendCalls == 2 && slot.Disposals == 0 && reaper.EntryCount == 1 &&
                                    reaper.OwnedCapacity == 1 && !File.Exists(receipt) &&
                                    File.ReadAllBytes(files.ResolvePath(GmWorkerAuditLog.AuditLogPath)).AsSpan().SequenceEqual(unknown) &&
                                    File.ReadAllBytes(journalPath).AsSpan().SequenceEqual(journal),
                                    "Expired/retired main admission erased retained canonical audit debt.");
                                Require(ReferenceEquals(established, failures[^1]),
                                    "Original required-audit uncertainty was replaced after main pin withdrawal.");
                            });
                            return 0;
                        }
                        // Fixture restores the recorded known after-image only after
                        // refusal assertions, allowing original recovery and teardown.
                        cutEnabled = false;
                        File.WriteAllBytes(files.ResolvePath(GmWorkerAuditLog.AuditLogPath), published!);
                    }
                    else File.WriteAllBytes(recordPath, originalRecord);
                    await reaper.RunPassAsync();
                    Require(slot.Disposals == 1 && reaper.EntryCount == 0 && reaper.OwnedCapacity == 0 && !File.Exists(receipt),
                        "Live original retry did not use canonical audit and release once.");
                    Require(File.ReadAllText(files.ResolvePath(GmWorkerAuditLog.AuditLogPath))
                        .Split(auditEvent.EventId, StringSplitOptions.None).Length == 2, "Canonical retry duplicated its event.");
                }
                else
                {
                    // Task captures the actual original Access, then waits until
                    // RunOperation has released that exact original pin.
                    deferred = Task.Run(async () =>
                    {
                        await resume.Task;
                        await reaper.RunPassAsync();
                        result["DeferredAppendCalls"] = appendCalls;
                        result["DeferredFailure"] = failures.LastOrDefault()?.ToString();
                        result["ReceiptReached"] = receiptCuts;
                        result["DeferredSlotDisposals"] = slot.Disposals;
                        result["ReceiptExists"] = File.Exists(receipt);
                        Require(appendCalls == 1 && receiptCuts == 1 && File.Exists(receipt),
                            "Expired original main admission prevented the required retained cleanup receipt.");
                        var exact = File.ReadAllBytes(receipt);
                        using (var parsed = JsonDocument.Parse(exact))
                        {
                            Require(parsed.RootElement.GetProperty("sessionGeneration").GetString() == generation &&
                                parsed.RootElement.GetProperty("auditEvent").GetProperty("eventId").GetString() == auditEvent.EventId,
                                "Cleanup receipt lost its original generation/event.");
                        }
                        if (mode == "receipt-retry")
                        {
                            Require(slot.Disposals == 0 && reaper.EntryCount == 1 && reaper.OwnedCapacity == 1,
                                "Receipt ACK failure released retained cleanup authority.");
                            failReceipt = false;
                            await reaper.RunPassAsync();
                            Require(appendCalls == 2 && receiptCuts == 1 && File.ReadAllBytes(receipt).AsSpan().SequenceEqual(exact),
                                "Receipt retry changed bytes/event or republished the acknowledged member.");
                        }
                        await reaper.RunPassAsync();
                        Require(slot.Disposals == 1 && reaper.EntryCount == 0 && reaper.OwnedCapacity == 0,
                            "Exact cleanup receipt did not release the same slot once.");
                        Require(!File.Exists(files.ResolvePath(GmWorkerAuditLog.AuditLogPath)), "Expired original pin appended canonical audit.");
                        result["FinalSlotDisposals"] = slot.Disposals;
                    });
                }
                return 0;
            });
            if (mode is "retired" or "publication-retired")
            {
                await stopMain();
                Require(!main.RetainsAuthority && main.Record?.Disposition == GmSessionRunDisposition.Stopped,
                    "Fixture main was not actually retired before deferred audit.");
                result["MainRetiredBeforeAudit"] = true;
            }
            resume.TrySetResult();
            if (deferred != null) await deferred;
            result["NoWorkerLaunched"] = true;
        }
        finally
        {
            resume.TrySetResult();
            if (deferred != null) try { await deferred; } catch { }
            // Test-owned restoration only; never a production recovery decision.
            if (mode == "metadata") File.WriteAllBytes(recordPath, originalRecord);
            if (workspace != null) await workspace.DisposeAsync();
        }
    }
    private sealed class Slot : IDisposable
    {
        internal int Disposals { get; private set; }
        public void Dispose() => Disposals++;
    }
}
