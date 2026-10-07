using System.Reflection;
using System.Runtime.ExceptionServices;
using System.Text.Json;
using BookOfEternityClient.Configuration;
using BookOfEternityClient.Core;
using BookOfEternityClient.Services.GmRuntime;
using Microsoft.Extensions.Logging.Abstractions;

namespace BookOfEternityClient.Tests;
internal static partial class OwnedTerminalScenarioDriver
{
    // Component causal fixture: real BridgeHost/pipe/owner inside TestSupport.
    // This does not substitute for the separate production-host live evidence.
    private static async Task RunPendingAvailabilityAsync(string folder, object host, Type type,
        Func<object, Task<JsonElement>> rpc, IOwnedTerminalSession original, Dictionary<string, object?> evidence)
    {
        var root = Path.Combine(folder, "root");
        var files = new FileSystemManager(root, NullLogger<FileSystemManager>.Instance);
        var settings = JsonSerializer.Deserialize<GameSettings>(File.ReadAllBytes(files.ResolvePath("config.json")))!;
        var engine = ProductionLoadGameEngine.Create(files, settings);
        var main = (GmSessionRunCoordinator)type.GetField("_mainRun", BindingFlags.Instance | BindingFlags.NonPublic)!.GetValue(host)!;
        var pins = (System.Collections.IDictionary)typeof(GmSessionRunCoordinator).GetField("_remotePins", BindingFlags.Instance | BindingFlags.NonPublic)!.GetValue(main)!;
        var sync = typeof(GmSessionRunCoordinator).GetField("_sync", BindingFlags.Instance | BindingFlags.NonPublic)!.GetValue(main)!;
        MainOperationReply[] Receipts()
        {
            lock (sync) return pins.Values.Cast<object>().Select(pin =>
                (MainOperationReply)pin.GetType().GetProperty("Reply", BindingFlags.Instance | BindingFlags.NonPublic)!.GetValue(pin)!).ToArray();
        }
        var entered = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        using var release = new ManualResetEventSlim();
        var writer = new FileSystemManager(root, NullLogger<FileSystemManager>.Instance,
            PhysicalLoadTransactionOperations.Instance, new FileSystemManagerHooks
            {
                LocalPublicationObserver = (phase, _) =>
                {
                    if (phase != TrustedLocalPublicationPhase.IntentPublished) return;
                    entered.TrySetResult();
                    if (!release.Wait(TimeSpan.FromSeconds(10))) throw new TimeoutException("Own deferred publication bound.");
                }
            });
        Task publishing;
        // Neither worker may inherit another worker's ambient admission/lease.
        using (ExecutionContext.SuppressFlow()) publishing = Task.Run(() =>
            SessionOperationContext.RunParticipatingCurrentSessionAsync(writer, () =>
                writer.WriteFileAtomicAsync("game_state/control/pending_availability_fixture.json", "{\"committed\":true}")));
        Task<bool>? reading = null;
        Exception? failure = null;
        try
        {
            await entered.Task.WaitAsync(TimeSpan.FromSeconds(4));
            if (!File.Exists(Path.Combine(files.RuntimeRootPath, "trusted-local-publication-v1/active.json")))
                throw new InvalidOperationException("Preparation failure: genuine pending journal not retained.");
            evidence["HostContext"] = "TestSupport component; production bootstrap not reproduced";
            evidence["GenuinePendingPublication"] = true;
            var prior = Receipts().Select(p => p.PinId).ToHashSet();
            using (ExecutionContext.SuppressFlow()) reading = Task.Run(async () =>
            {
                Task<bool> actual;
                try { actual = (Task<bool>)typeof(GameEngine).GetMethod("HasCurrentSessionAsync", BindingFlags.Instance | BindingFlags.NonPublic)!.Invoke(engine, null)!; }
                catch (TargetInvocationException ex) when (ex.InnerException != null)
                { ExceptionDispatchInfo.Capture(ex.InnerException).Throw(); throw; }
                return await actual;
            });
            var deadline = DateTime.UtcNow + TimeSpan.FromSeconds(4);
            while (!reading.IsCompleted && !Receipts().Any(p => !prior.Contains(p.PinId) && p.State == MainOperationState.Active))
            {
                if (DateTime.UtcNow >= deadline) throw new TimeoutException("Reader original admission not observed.");
                await Task.Delay(10);
            }
            evidence["ReaderOriginalPinBeforePublisherRelease"] = Receipts().Any(p => !prior.Contains(p.PinId) && p.State == MainOperationState.Active);
        }
        finally
        {
            release.Set();
            await publishing.WaitAsync(TimeSpan.FromSeconds(5));
            if (reading != null)
            {
                try { evidence["HasCurrentSession"] = await reading.WaitAsync(TimeSpan.FromSeconds(5)); }
                catch (Exception ex) { failure = ex; evidence["OriginalConsumerException"] = ex.ToString(); }
            }
            evidence["OriginalReceipts"] = Receipts();
            evidence["PendingJournalAfterSettlement"] = File.Exists(Path.Combine(files.RuntimeRootPath, "trusted-local-publication-v1/active.json"));
            evidence["OriginalOwnerUnchanged"] = ReferenceEquals(original, type.GetField("_pty", BindingFlags.Instance | BindingFlags.NonPublic)!.GetValue(host));
            evidence["OriginalShutdownReceipt"] = await rpc(new { command = "shutdown", rootKey = main.Identity.RootKey, expectedMainIdentity = main.Identity });
        }
        if (failure != null) ExceptionDispatchInfo.Capture(failure).Throw();
        var receipts = (MainOperationReply[])evidence["OriginalReceipts"]!;
        if (reading == null || !reading.Result || !(bool)evidence["ReaderOriginalPinBeforePublisherRelease"]! || receipts.Length != 2 ||
            receipts.Any(p => p.State != MainOperationState.ClosedObserved || p.Identity == null || !GmSessionRunValidation.IdentityMatches(p.Identity, main.Identity)) ||
            (bool)evidence["PendingJournalAfterSettlement"]! || !(bool)evidence["OriginalOwnerUnchanged"]! ||
            !((JsonElement)evidence["OriginalShutdownReceipt"]!).GetProperty("ok").GetBoolean())
            throw new InvalidOperationException("Actual session availability did not settle its own publication/original pins/stop.");
    }
}
