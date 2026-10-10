using System.Diagnostics;
using System.Reflection;
using System.Text.Json;
using System.Text.Json.Nodes;
using BookOfEternityClient.Configuration;
using BookOfEternityClient.Core;
using BookOfEternityClient.Models;
using BookOfEternityClient.Services;
using BookOfEternityClient.Services.GmRuntime;
using Microsoft.Extensions.Logging.Abstractions;
using Xunit;

namespace BookOfEternityClient.Tests;

public sealed partial class GameEngineTurnLifecycleTests
{
    private const int BrowserActivePollCut = 3;

    [Fact]
    public Task BrowserOriginalAdmission_ActualActivePollCrashRefusesColdBeforeRecovery() =>
        RunActualMusingsCrashAsync(BrowserActivePollCut);

    public static async Task RunOriginalBrowserActivePollAsync(string root, string output, int cut, string own)
    {
        const BindingFlags flags = BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic;
        Assert.Equal(BrowserActivePollCut, cut);
        using var factory = new GameEngineTurnLifecycleTests(); // Independent factory scratch; never bootstraps retained state.
        var control = JsonNode.Parse(File.ReadAllText(Path.Combine(own, "cut-control.json")))!;
        var files = new FileSystemManager(root, NullLogger<FileSystemManager>.Instance);
        var binding = await QueueBrowserProfileInputAsync(files);
        var gate = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        Task? operation = null, observer = null;
        bool claimed = false;
        var engine = factory.CreateGameEngine(new QueuedConsoleInputSource([]), fileSystem: files,
            configureSettings: settings => { settings.MusicEnabled = false; settings.SoundEnabled = false; },
            finalizationHooks: new GameEngineSessionFinalizationHooks
            {
                AtCheckpointAsync = checkpoint =>
                {
                    if (checkpoint != SessionFinalizationCheckpoint.TerminalSignalInspectionLeaseAcquired || claimed)
                        return Task.CompletedTask;
                    claimed = true;
                    // Retain the actual hook execution context before scheduling inspection.
                    // No synthetic admission, close, staged proof or replacement lease.
                    var admissions = (AsyncLocal<FileSystemManager.MainAdmission?>)typeof(FileSystemManager)
                        .GetField("MainAdmissions", BindingFlags.Static | BindingFlags.NonPublic)!.GetValue(null)!;
                    var remote = admissions.Value;
                    while (remote is not null && !remote.OwnsRemote) remote = remote.Parent;
                    var scopes = (AsyncLocal<FileSystemManager.BrowserOriginalOperationScope?>)typeof(FileSystemManager)
                        .GetField("BrowserOriginalOperations", BindingFlags.Static | BindingFlags.NonPublic)!.GetValue(null)!;
                    var scope = scopes.Value;
                    var ambient = (AsyncLocal<FileSystemManager.AmbientCanonicalLeaseRegistration?>)typeof(FileSystemManager)
                        .GetField("_ambientCanonicalLease", flags)!.GetValue(files)!;
                    var registration = ambient.Value;
                    var boundGeneration = SessionOperationContext.TryGetExpectedGeneration(files.BasePath, out var generation);
                    observer = Task.Run(async () =>
                    {
                        try
                        {
                            Assert.NotNull(remote); Assert.NotNull(scope); Assert.NotNull(registration);
                            Assert.True(boundGeneration); Assert.Equal(binding.Generation, generation);
                            Assert.True(remote.Acquired); Assert.True(remote.OwnsRemote);
                            Assert.False(remote.CloseObserved); Assert.Null(remote.TerminalClose);
                            Assert.True(registration.Active);
                            Assert.Same(files, scope._files); Assert.Equal(binding, scope.Binding);
                            Assert.False(scope.Processing); Assert.False(scope.Cleanup);
                            var staged = Assert.IsType<PendingPlayerActionService.Staged>(scope.Staged);
                            Assert.Equal(staged.Json, File.ReadAllText(files.ResolvePath(PendingPlayerActionService.PendingPath)));
                            var pending = PendingPlayerActionService.Parse(staged.Json, binding.Generation);
                            Assert.Equal("staged", pending.Phase);
                            var manifest = GameEngine.ValidateDetachedBrowserBinding(staged);
                            Assert.Equal(scope.Condition, manifest.BrowserOriginalMainCondition);
                            Assert.Equal("active", scope.Condition.Kind);
                            var identity = remote.DescribeClose(MainOperationOutcome.Completed, false)!; // Identity ONLY; no observed close.
                            Assert.Equal(identity.Identity, scope.Condition.ActiveIdentity);
                            Assert.Equal(binding.Generation, identity.Identity.GenerationId);
                            Assert.Equal(control["SessionId"]!.GetValue<string>(), manifest.SessionId);
                            Assert.Equal(control["TurnNumber"]!.GetValue<int>() + 1, manifest.TurnNumber);
                            Assert.Equal(binding.ActionId, manifest.RequestId);
                            Assert.Equal(staged.RequestJson, File.ReadAllText(files.ResolvePath("input/turn_request.json")));
                            Assert.Equal(staged.ManifestJson, File.ReadAllText(files.ResolvePath("game_state/control/pending_turn_snapshot.json")));
                            Assert.Equal(staged.AuthorityJson, File.ReadAllText(files.ResolvePath(PendingTurnSnapshotAuthority.AuthorityPath)));

                            // Assignment and await registration are a bounded handshake. The original
                            // UI polls waitTask; it does not yet await it. Prove the actual owned Task.Run
                            // waiter separately, then the exact gate's registered continuation to it.
                            var watch = Stopwatch.StartNew();
                            object? rootGraph = null, pollGraph = null;
                            FileSystemManager.CanonicalWriteLease? lease = null;
                            Task? waitTask = null;
                            string? waiterState = null, waiterField = null;
                            while (watch.Elapsed < TimeSpan.FromSeconds(10))
                            {
                                if (operation is not null)
                                {
                                    var rootWalk = ReadActualMusingsStateMachines(operation, gate.Task);
                                    rootGraph = rootWalk.Diagnostic;
                                    var waiters = rootWalk.States.Where(state => state.GetType().FullName!
                                        .Contains("<WaitForTerminalSignalWithParticipationAsync>", StringComparison.Ordinal)).ToArray();
                                    if (waiters.Length > 1) throw new InvalidOperationException("PREPARATION: ambiguous original waiter.");
                                    if (waiters.Length == 1)
                                    {
                                        var tasks = FindBrowserActivePollWaitTasks(waiters[0]).ToArray();
                                        if (tasks.Length > 1) throw new InvalidOperationException("PREPARATION: ambiguous owned waitTask.");
                                        if (tasks.Length == 1)
                                        {
                                            waitTask = tasks[0].Task; waiterField = tasks[0].Path;
                                            waiterState = waiters[0].GetType().FullName;
                                            var pollWalk = ReadActualMusingsStateMachines(waitTask, gate.Task);
                                            pollGraph = pollWalk.Diagnostic;
                                            if (pollWalk.GateReachable)
                                            {
                                                var owners = pollWalk.States.Select(FindActualMusingsPublicationLease)
                                                    .Where(value => value is not null).Distinct(ReferenceEqualityComparer.Instance).ToArray();
                                                if (owners.Length > 1) throw new InvalidOperationException("PREPARATION: ambiguous inspection lease.");
                                                if (owners.Length == 1) { lease = (FileSystemManager.CanonicalWriteLease)owners[0]!; break; }
                                            }
                                        }
                                    }
                                }
                                await Task.Delay(10);
                            }
                            File.WriteAllText(Path.Combine(own, "active-poll-task-graph.json"), JsonSerializer.Serialize(new
                            { OriginalOperationTaskId = operation?.Id, WaiterState = waiterState, WaitTaskField = waiterField,
                                ActualWaitTaskId = waitTask?.Id, RootGraph = rootGraph, PollGraph = pollGraph }));
                            Assert.NotNull(lease); Assert.NotNull(waitTask);
                            Assert.True(lease.IsActive); Assert.Same(files, lease.Owner);
                            Assert.Same(registration, lease.AmbientRegistration); Assert.True(registration.Active);
                            Assert.NotNull(lease.MainAdmission); Assert.True(lease.MainAdmission.SharesAccess(remote));
                            scope.ValidateOwner(lease.MainAdmission);
                            var stream = Assert.IsType<FileStream>(typeof(FileSystemManager.CanonicalWriteLease).GetField("_stream", flags)!.GetValue(lease));
                            Assert.False(stream.SafeFileHandle.IsClosed); Assert.False(stream.SafeFileHandle.IsInvalid);
                            Assert.False(remote.CloseObserved); Assert.Null(remote.TerminalClose);
                            Assert.False(gate.Task.IsCompleted); Assert.False(waitTask.IsCompleted);
                            foreach (var path in new[] { "ready/turn_complete.json", "ready/turn_error.json" })
                                Assert.False(File.Exists(files.ResolvePath(path)));
                            var stories = Directory.EnumerateFiles(files.ResolvePath("stories"), "*.jsonl", SearchOption.AllDirectories)
                                .SelectMany(File.ReadAllLines).Where(line => !string.IsNullOrWhiteSpace(line)).ToArray();
                            Assert.Empty(stories);
                            var fixture = CaptureBrowserProfileFixture(files, own);
                            RetainBrowserActivePollFiles(files, own, "cut-all");
                            var ack = JsonSerializer.Serialize(new
                            {
                                Pid = Environment.ProcessId, Nonce = control["Nonce"]!.GetValue<string>(), Cut = cut,
                                QualifiedBoundary = true, ActiveInspectionLease = true, ActualWaitTaskGateReachable = true,
                                PhysicalHandleOpen = true, ActiveRegistrationSameLease = true, OriginalAdmissionShared = true,
                                SessionId = manifest.SessionId, TurnNumber = manifest.TurnNumber, Generation = binding.Generation,
                                ActionId = binding.ActionId, RequestId = manifest.RequestId, PendingPhase = pending.Phase,
                                Processing = scope.Processing, Cleanup = scope.Cleanup, OriginalRun = scope.Condition.ActiveIdentity,
                                PinIdentity = identity, PinIdentityOnlyNotObservedClose = true, CloseObserved = remote.CloseObserved,
                                StoryEntries = stories.Length, ModelCalls = 0, Fixture = fixture,
                                Inventory = MusingsCutInventory(files), OriginalOperationTaskId = operation!.Id,
                                ActualWaitTaskId = waitTask.Id, GateTaskId = gate.Task.Id,
                                Scope = "First actual browser signal inspection after real staging; same original active physical lease/pin before signal read."
                            });
                            File.WriteAllText(output + ".tmp", ack); File.Move(output + ".tmp", output);
                        }
                        catch (Exception failure)
                        {
                            File.WriteAllText(Path.Combine(own, "fixture-preparation-failure.txt"), failure.ToString());
                            // Do not release the actual inspection or allow rollback before parent kill.
                        }
                    });
                    return gate.Task;
                }
            });
        GetPrivateField<GameLoop>(engine, "_gameLoop").SetSession(control["SessionId"]!.GetValue<string>(), control["TurnNumber"]!.GetValue<int>());
        operation = InvokePrivateTaskAsync(engine, "ProcessPlayerTurn", binding.Action, null, null, null, true, binding);
        await operation.WaitAsync(TimeSpan.FromSeconds(180));
        if (observer is not null) await observer;
        throw new InvalidOperationException("PREPARATION: original active inspection returned without parent crash.");
    }

    private static IEnumerable<(Task Task, string Path)> FindBrowserActivePollWaitTasks(object holder, string prefix = "")
    {
        foreach (var field in holder.GetType().GetFields(BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic))
        {
            var value = field.GetValue(holder);
            var path = prefix + field.Name;
            if ((field.Name == "waitTask" || field.Name.StartsWith("<waitTask>", StringComparison.Ordinal)) && value is Task task)
                yield return (task, path);
            else if (value?.GetType().FullName?.StartsWith(typeof(GameEngine).FullName + "+<>c__DisplayClass", StringComparison.Ordinal) == true)
                foreach (var owned in FindBrowserActivePollWaitTasks(value, path + "/")) yield return owned;
        }
    }

    private static void RetainBrowserActivePollFiles(FileSystemManager files, string own, string label)
    {
        var folder = Path.Combine(own, label); Directory.CreateDirectory(folder);
        // Every canonical inventory entry is retained as raw .bin, including ZIPs;
        // the original relative path remains explicit and the inventory supplies its hash.
        foreach (var path in MusingsCutInventory(files).Keys)
        {
            var target = Path.Combine(folder, path + ".bin"); Directory.CreateDirectory(Path.GetDirectoryName(target)!);
            File.WriteAllBytes(target, File.ReadAllBytes(path == "original-runtime-generation" ? files.SessionGenerationPath : files.ResolvePath(path)));
        }
        File.WriteAllText(Path.Combine(folder, "inventory.json"), JsonSerializer.Serialize(MusingsCutInventory(files)));
    }
}
