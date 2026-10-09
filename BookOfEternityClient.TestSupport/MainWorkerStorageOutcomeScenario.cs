using System.Collections;
using System.Reflection;
using System.Text.Json;
using BookOfEternityClient.Configuration;
using BookOfEternityClient.Core;
using BookOfEternityClient.Services.GmRuntime;
using BookOfEternityClient.Services.GmWorkers;
using Microsoft.Extensions.Logging.Abstractions;
using static BookOfEternityClient.Tests.WorkerStorageOutcomeProbe;

namespace BookOfEternityClient.Tests;

// Original private Bridge -> original pin -> proposal dispatcher -> real pool.
// Only prelaunch worker paths are admitted; the already-owned neutral main is
// the existing bounded terminal fixture, never a provider or game-running GM.
internal static class MainWorkerStorageOutcomeScenario
{
    internal static async Task RunAsync(string mode, string root, string package, object host, Type hostType,
        GmSessionRunCoordinator owner, Func<Task> stop, Dictionary<string, object?> evidence)
    {
        const BindingFlags flags = BindingFlags.Instance | BindingFlags.NonPublic;
        const string contextPath = "game_state/world/weather.json";
        evidence["WorkerFixtureRoot"] = root;
        var fs = new FileSystemManager(root, NullLogger<FileSystemManager>.Instance);
        var profile = GmWorkerBridgeTestFixtures.AnalysisCodexProfile() with
        { LaunchCommand = "exit 74", TimeoutSeconds = 5, MaxConcurrentTasks = 1 };
        await owner.RunOperationAsync(async () =>
        {
            await fs.WriteFileAtomicAsync("config.json", JsonSerializer.Serialize(new GameSettings
            { GmCliLaunchCommand = "", GmWorkerBridgeProfiles = [profile] }));
            await fs.WriteFileAtomicAsync(contextPath, "{\"fixture\":\"original prelaunch storage\"}");
            return 0;
        });
        var loaded = (GameSettings)hostType.GetMethod("LoadBridgeConfig", flags)!.Invoke(host, null)!;
        Require(loaded.GmWorkerBridgeProfiles.Count == 1 && GmWorkerContractValidator.ValidateProfile(loaded.GmWorkerBridgeProfiles[0]).IsValid,
            "actual Bridge config did not preserve the sole valid profile");
        var prior = new[] { fs.ResolvePath("config.json"), fs.ResolvePath(contextPath), fs.SessionGenerationPath }
            .ToDictionary(path => path, Bytes, StringComparer.Ordinal);
        Require(prior[fs.SessionGenerationPath] != null, "original main did not create a real generation");
        using var probe = new WorkerStorageOutcomeProbe();
        var known = new IOException("known original task reservation refusal");
        var publicDefault = mode == "public_linux_refusal";
        using var admission = publicDefault ? null : new GmWorkerNativePoolAdmission(package, root);
        var reaper = new GmWorkerQuarantineReaper(1, retrySchedule: [], runInBackground: false);
        var factories = 0; var slotEntries = 0; var reservationEntries = 0; var knownHits = 0;
        var forbidden = new List<string>();
        var pinHeldAtReservation = false; var capacityHeldAtReservation = false; var slotHeldAtReservation = false;
        string SlotKey() => fs.GameSessionPath.TrimEnd(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar) + "|" + profile.WorkerId;
        IDictionary Slots() => (IDictionary)typeof(GmWorkerBridgePool).GetField("WorkerConcurrencyGates", BindingFlags.Static | BindingFlags.NonPublic)!.GetValue(null)!;
        Task RefuseLaunch(string stage) { forbidden.Add(stage); throw new InvalidOperationException("fixture forbids worker launch: " + stage); }
        var hooks = new GmWorkerBridgePoolHooks
        {
            BeforeWorkerSlotWaitAsync = () => { slotEntries++; return Task.CompletedTask; },
            BeforeTaskReservationAsync = () =>
            {
                reservationEntries++;
                pinHeldAtReservation = (int)typeof(GmSessionRunCoordinator).GetField("_pins", flags)!.GetValue(owner)! > 0;
                capacityHeldAtReservation = reaper.OwnedCapacity == 1;
                slotHeldAtReservation = Slots().Contains(SlotKey());
                if (mode is "known_diagnostic_unknown" or "known_reservation") { knownHits++; throw known; }
                return Task.CompletedTask;
            },
            BeforeWorkspaceFileCreateAsync = _ => RefuseLaunch("workspace"),
            BeforeProcessTreeAttachAsync = () => RefuseLaunch("attach"),
            BeforeWorkerReleaseAsync = () => RefuseLaunch("release")
        };
        Func<FileSystemManager, GmWorkerAuditLog, GmWorkerBridgePool> factory = (actualFiles, audit) =>
        {
            factories++; probe.Attach(actualFiles);
            foreach (var pair in prior) probe.Committed[pair.Key] = pair.Value;
            probe.Select = path => mode == "reservation_unknown"
                ? path.StartsWith(actualFiles.ResolvePath(GmWorkerBridgePool.TaskRoot) + Path.DirectorySeparatorChar, StringComparison.Ordinal) && path.EndsWith("/task.json", StringComparison.Ordinal)
                : (mode is "dispatch_unknown" or "known_diagnostic_unknown") && path == actualFiles.ResolvePath(GmWorkerAuditLog.AuditLogPath);
            return new(actualFiles, null, audit, hooks, GmWorkerProcessTreeFactory.Instance, reaper, admission);
        };
        hostType.GetField("WorkerDispatchPoolFactory", flags)!.SetValue(host, factory);
        hostType.GetField("WorkerDispatchFileHooks", flags)!.SetValue(host, probe.Hooks);
        var requestType = hostType.Assembly.GetType("BookOfEternityGMBridge.BridgeRequest", true)!;
        var request = JsonSerializer.Deserialize(JsonSerializer.Serialize(new
        {
            WorkerTaskType = "analysis", AnalysisGoal = "Observe actual prelaunch storage",
            Questions = new[] { "Inspect the supplied context" }, ContextPaths = new[] { contextPath },
            SessionId = "prelaunch-storage", RequestId = "original-worker-request", TurnNumber = 7
        }), requestType)!;
        Exception? failure = null; object? response = null; Task? dispatch = null;
        Exception? admissionCloseFailure = null; Exception? mainStopFailure = null;
        GmWorkerProposalOnlyDispatchResult? result = null;
        try
        {
            try
            {
                dispatch = (Task)hostType.GetMethod("DispatchWorkerTaskAsync", flags)!.Invoke(host, [request])!;
                await dispatch;
                response = dispatch.GetType().GetProperty("Result")!.GetValue(dispatch)!;
                result = (GmWorkerProposalOnlyDispatchResult?)response.GetType().GetProperty("WorkerDispatch")!.GetValue(response);
            }
            catch (Exception error) { failure = error; }
        }
        finally
        {
            // Original main stop is required even on causal RED. No new worker
            // task, canonical recovery or synthetic owner is used for cleanup.
            try { admission?.Dispose(); }
            catch (Exception error) { admissionCloseFailure = error; }
            try { await stop(); }
            catch (Exception error) { mainStopFailure = error; }
            evidence["OriginalWorkerMainRetired"] = owner.Record?.Disposition == GmSessionRunDisposition.Stopped && !owner.RetainsAuthority;
            evidence["WorkerStorage"] = new
            {
                mode, Failure = failure?.ToString(), ResponseReturned = response != null, Outcome = result?.Outcome.ToString(), result,
                DispatchSettled = dispatch?.IsCompleted == true, SameOriginalUncertainty = ReferenceEquals(probe.OriginalUncertainty, failure),
                OriginalCauseRetained = ReferenceEquals(failure?.Data["GmWorkerOriginalFailure"], known),
                AdmissionCloseFailure = admissionCloseFailure?.ToString(), MainStopFailure = mainStopFailure?.ToString(),
                factories, slotEntries, reservationEntries, knownHits, forbidden,
                pinHeldAtReservation, capacityHeldAtReservation, slotHeldAtReservation,
                PinCountAfter = (int)typeof(GmSessionRunCoordinator).GetField("_pins", flags)!.GetValue(owner)!,
                SlotRetained = Slots().Contains(SlotKey()), reaper.EntryCount, reaper.OwnedCapacity,
                prior, afterImages = prior.ToDictionary(pair => pair.Key, pair => Bytes(pair.Key), StringComparer.Ordinal),
                AuditAfter = Bytes(fs.ResolvePath(GmWorkerAuditLog.AuditLogPath)), Cut = probe.Evidence()
            };
        }
        Require((bool)evidence["OriginalWorkerMainRetired"]!, "original main stop did not retire its owner");
        Require(admissionCloseFailure == null && mainStopFailure == null, "original admission/main cleanup failed");
        Require((int)typeof(GmSessionRunCoordinator).GetField("_pins", flags)!.GetValue(owner)! == 0, "original main operation pin leaked");
        Require(dispatch?.IsCompleted == true && factories == 1 && forbidden.Count == 0, "original dispatch did not settle before launch");
        Require(!Slots().Contains(SlotKey()) && reaper.EntryCount == 0 && reaper.OwnedCapacity == 0, "prelaunch ownership/capacity leaked");
        foreach (var pair in prior) Require(Equal(pair.Value, Bytes(pair.Key)), "prior canonical state changed");
        Require(reservationEntries == (publicDefault ? 0 : 1) && slotEntries == (publicDefault ? 0 : 1), "original reservation/slot path not reached exactly once");
        if (!publicDefault) Require(pinHeldAtReservation && capacityHeldAtReservation && slotHeldAtReservation, "actual prelaunch ownership was not held");
        if (mode.EndsWith("_unknown", StringComparison.Ordinal))
        {
            probe.RequireStopped();
            Require(ReferenceEquals(probe.OriginalUncertainty, failure) && response == null, "original Bridge did not propagate exact uncertainty");
            if (mode == "known_diagnostic_unknown") Require(ReferenceEquals(failure!.Data["GmWorkerOriginalFailure"], known), "original reservation cause was lost");
        }
        else
        {
            Require(failure == null && result?.Outcome == GmWorkerProposalOnlyDispatchOutcome.WorkerFailed, "known refusal policy changed");
            Require(probe.Cuts == 0 && !File.Exists(probe.JournalPath), "known refusal retained uncertain storage");
            Require(knownHits == (publicDefault ? 0 : 1), "known reservation refusal not reached exactly once");
            if (!publicDefault) Require(result!.FallbackReason.Contains(known.Message, StringComparison.Ordinal), "known refusal cause missing from result");
        }
    }
}
