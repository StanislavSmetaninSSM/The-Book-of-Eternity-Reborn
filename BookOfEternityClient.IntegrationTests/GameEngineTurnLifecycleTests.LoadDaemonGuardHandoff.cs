using System.Text.Json;
using BookOfEternityClient.Core;
using BookOfEternityClient.Services;
using BookOfEternityClient.Services.GmRuntime;
using Microsoft.Extensions.Logging.Abstractions;
using Xunit;

namespace BookOfEternityClient.Tests;

public sealed partial class GameEngineTurnLifecycleTests
{
    private const string LoadHandoffDaemonStatus = "game_state/control/gm_daemon_status.json";

    /// <summary>One unbound daemon existence read crossing a real Load must complete against authenticated B.</summary>
    [Fact]
    public async Task ActiveLoadDaemonGuardHandoff_ContendedExistenceReadUsesReplacementOnce()
    {
        var evidence = await RunHostedSaveLoadProbeAsync("engine-active-load-daemon-handoff", "ld-", 0, guardianSeconds: 40);
        var detail = evidence.ToJsonString();
        Assert.Null(evidence["SetupFailure"]);
        Assert.Null(evidence["CleanupFailure"]);
        Assert.Null(evidence["ServerJoinFailure"]);
        Assert.Null(evidence["HelperJoinFailure"]);
        Assert.Null(evidence["LoadJoinFailure"]);
        Assert.Null(evidence["PositiveEvidenceFailure"]);
        Assert.False(evidence["HelperTimedOut"]!.GetValue<bool>(), detail);
        Assert.Equal(1, evidence["HelperStarts"]!.GetValue<int>());
        Assert.Equal(1, evidence["PublicationCuts"]!.GetValue<int>());
        Assert.True(evidence["CommittedLoadGuardContended"]!.GetValue<bool>(), detail);
        Assert.True(evidence["WaiterPausedBeforeAnyFrame"]!.GetValue<bool>(), detail);
        Assert.True(evidence["OriginalStopped"]!.GetValue<bool>(), detail);
        Assert.True(evidence["AuthenticatedReplacementBeforeWaiterRelease"]!.GetValue<bool>(), detail);
        Assert.True(evidence["ReplacementStopped"]!.GetValue<bool>(), detail);
        Assert.True(evidence["ExactArchiveAndReplacementData"]!.GetValue<bool>(), detail);
        // This positive requirement deliberately fails on the old production admission.
        // Every actual Load/guard/cleanup prerequisite above is checked before calling it RED.
        Assert.True(evidence["SingleReplacementGrantReadClose"]!.GetValue<bool>(), detail);
        Assert.Null(evidence["HelperFailure"]);
        Assert.Equal(1, evidence["HelperClosingCallbacks"]!.GetValue<int>());
    }

    /// <summary>Runs the real replacement and helper only under the existing neutral native guardian.</summary>
    public static async Task WriteActiveLoadDaemonGuardHandoffProbeAsync(string package, string output, int mode, string own)
    {
        Assert.Equal(0, mode);
        var launch = NeutralTerminalLaunch.Create(package, own);
        var root = Directory.GetParent(launch.Scratch)!.FullName;
        var seed = new FileSystemManager(root, NullLogger<FileSystemManager>.Instance);
        var state = PortableSaveFixture.Seed(seed);
        var prepare = new SaveLoadService(seed, state, NullLogger<SaveLoadService>.Instance);
        const string markerName = "game_state/world/test_fixture_state.json";
        var marker = seed.ResolvePath(markerName);
        var savedMarker = File.ReadAllBytes(marker);
        var created = await prepare.CreateSaveAsync("active-load-daemon-handoff", "owned native fixture", turnNumber: 41);
        Assert.True(created.Committed, created.Failure?.ToString());
        var archive = Assert.Single(Directory.GetFiles(seed.ResolvePath("saves/manual_saves"), "*.zip"));
        var archiveBytes = File.ReadAllBytes(archive);
        await seed.WriteFileAtomicAsync(markerName, "{\"state\":\"after-own-save-before-real-load\"}");
        Assert.False(savedMarker.AsSpan().SequenceEqual(File.ReadAllBytes(marker)));
        Assert.False(File.Exists(seed.ResolvePath(LoadHandoffDaemonStatus)));
        var originalGeneration = FileSystemManager.ParseSessionGenerationText(File.ReadAllText(seed.SessionGenerationPath));

        using var terminal = new MemoryStream();
        var host = new MainRunCrashScenarioDriver.FixtureHost(launch, terminal);
        using var supervisorControl = new CancellationTokenSource();
        var server = host.StartServer(supervisorControl.Token);
        using var input = new MemoryStream();
        using var outputFrames = new MemoryStream();
        var transfer = await WriteLoadHandoffSnapshotRequestAsync(input);
        var atLoadGuard = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        var releaseLoad = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        var atMainOwnerContention = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        var releaseWaiter = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        Task<LoadReplacementResult>? loading = null;
        Task<int>? waiter = null;
        GmSessionRunCoordinator? ownerA = null, authenticatedB = null;
        var loadPublicationCuts = 0;
        var contentions = 0;
        var helperStarts = 0;
        var helperClosings = 0;
        var evidence = new Dictionary<string, object?> { ["SingleReplacementGrantReadClose"] = false };
        GmSessionRunRecord ReadRecord() => GmSessionRunRecordCodec.Decode(GmSessionRunPersistence.Read(root)!);
        try
        {
            await host.CallAsync("StartShellAsync");
            ownerA = host.Owner;
            var ownerAIdentity = ownerA.Identity;
            var terminalA = host.Terminal;
            Assert.Equal(GmSessionRunDisposition.Running, ReadRecord().Disposition);
            Assert.Equal(ownerAIdentity, ReadRecord().Identity);
            Assert.Equal(originalGeneration, ownerAIdentity.GenerationId);
            Assert.False(terminalA.RootExited.IsCompleted);
            evidence["OriginalIdentity"] = ownerAIdentity;
            // Like the console, SaveLoadService and StateManager must share
            // their exact filesystem manager for the committed refresh lease.
            var loadFiles = seed;
            var loadService = new SaveLoadService(loadFiles, state, NullLogger<SaveLoadService>.Instance,
                new SaveLoadServiceHooks
                {
                    AfterLoadPublicationValidatedAsync = async () =>
                    {
                        Assert.Equal(1, Interlocked.Increment(ref loadPublicationCuts));
                        atLoadGuard.TrySetResult();
                        await releaseLoad.Task.WaitAsync(TimeSpan.FromSeconds(12));
                    }
                });
            using (ExecutionContext.SuppressFlow()) loading = Task.Run(async () =>
            {
                await using var load = GmLoadSessionOperation.Reserve(loadFiles, Guid.NewGuid().ToString("N"), archive, originalGeneration);
                await load.StopAsync();
                Assert.True(load.HadSession);
                Assert.Equal(GmLoadMainState.Stopped, load.State);
                LoadReplacementResult result;
                // The same quiescent outer main admission as the console holds the
                // genuine local guard through committed storage publication/refresh.
                using (var local = loadFiles.BeginMainAdmission())
                {
                    await local.AcquireAsync(quiescentOnly: true);
                    await load.ValidateLoadGenerationAsync();
                    result = await loadService.LoadGameWithAdmissionAsync(archive, null);
                }
                var refreshed = result.Disposition == LoadReplacementDisposition.Committed && !result.NeedsFollowUp && !result.ContinuationBlocked;
                var finish = await load.FinishAsync(result.Disposition == LoadReplacementDisposition.Committed, refreshed, result.EstablishedGeneration);
                Assert.True(refreshed, result.Failure?.ToString());
                Assert.Equal(GmLoadMainState.Running, finish); // authenticated against the exact retained A connection and installed B by production
                return result;
            });
            await WaitForLoadHandoffGateAsync(atLoadGuard.Task, loading, "real Load committed publication cut");
            var stoppedA = ReadRecord();
            Assert.Equal(GmSessionRunDisposition.Stopped, stoppedA.Disposition);
            Assert.Equal(ownerAIdentity, stoppedA.Identity);
            Assert.Equal(ownerAIdentity, stoppedA.StopEvidence?.Identity);
            Assert.Equal(GmSessionRunStopKind.OwnedScopeEmpty, stoppedA.StopEvidence?.Kind);
            Assert.False(ownerA.RetainsAuthority);
            Assert.True(terminalA.RootExited.IsCompletedSuccessfully);
            var committedGeneration = FileSystemManager.ParseSessionGenerationText(File.ReadAllText(seed.SessionGenerationPath));
            Assert.NotEqual(originalGeneration, committedGeneration);
            Assert.Equal(savedMarker, File.ReadAllBytes(marker));
            evidence["OriginalStopped"] = true;
            evidence["CommittedGenerationAtGuard"] = committedGeneration;

            var helperFiles = new FileSystemManager(root, NullLogger<FileSystemManager>.Instance,
                PhysicalLoadTransactionOperations.Instance, new FileSystemManagerHooks
                {
                    MainOwnerLockContendedAsync = async () =>
                    {
                        if (Interlocked.Increment(ref contentions) != 1) return;
                        atMainOwnerContention.TrySetResult();
                        await releaseWaiter.Task.WaitAsync(TimeSpan.FromSeconds(12));
                    },
                    SessionOperationClosingAsync = () => { Interlocked.Increment(ref helperClosings); return Task.CompletedTask; }
                });
            using (ExecutionContext.SuppressFlow()) waiter = Task.Run(async () =>
            {
                Assert.Null(GmSessionRunCoordinator.Current);
                Assert.False(SessionOperationContext.TryGetExpectedGeneration(root, out _));
                Interlocked.Increment(ref helperStarts);
                return await GmMainParticipatingControl.RunAsync(helperFiles, input, outputFrames);
            });
            await WaitForLoadHandoffGateAsync(atMainOwnerContention.Task, waiter, "actual local main-owner guard contention");
            Assert.False(loading.IsCompleted);
            Assert.Equal(stoppedA, ReadRecord());
            Assert.Equal(1, Volatile.Read(ref contentions));
            Assert.Equal(0L, input.Position);
            Assert.Equal(0L, outputFrames.Length);
            evidence["CommittedLoadGuardContended"] = true;
            evidence["WaiterPausedBeforeAnyFrame"] = true;

            releaseLoad.TrySetResult();
            var loaded = await loading.WaitAsync(TimeSpan.FromSeconds(8));
            var beforeWaiterRelease = ReadRecord();
            Assert.Equal(LoadReplacementDisposition.Committed, loaded.Disposition);
            Assert.Equal(archive, loaded.SelectedSourcePath);
            Assert.Equal(committedGeneration, loaded.EstablishedGeneration);
            Assert.Equal(GmSessionRunDisposition.Running, beforeWaiterRelease.Disposition);
            Assert.Equal(committedGeneration, beforeWaiterRelease.Identity.GenerationId);
            Assert.Equal(ownerAIdentity.RootKey, beforeWaiterRelease.Identity.RootKey);
            Assert.NotEqual(ownerAIdentity.RunId, beforeWaiterRelease.Identity.RunId);
            Assert.True(beforeWaiterRelease.Identity.Epoch > ownerAIdentity.Epoch);
            var ownerB = host.Owner;
            Assert.Equal(beforeWaiterRelease.Identity, ownerB.Identity);
            Assert.Equal(beforeWaiterRelease.Identity.RunId, host.Terminal.Identity.RunId);
            Assert.False(host.Terminal.RootExited.IsCompleted);
            Assert.False(waiter.IsCompleted);
            Assert.Equal(0L, input.Position);
            Assert.Equal(0L, outputFrames.Length);
            authenticatedB = ownerB;
            evidence["ReplacementIdentity"] = beforeWaiterRelease.Identity;
            evidence["AuthenticatedReplacementBeforeWaiterRelease"] = true;

            releaseWaiter.TrySetResult();
            int? helperExit = null;
            try { helperExit = await waiter.WaitAsync(TimeSpan.FromSeconds(15)); }
            catch (Exception failure)
            {
                evidence["HelperFailure"] = failure.ToString();
                evidence["HelperFailureType"] = failure.GetType().FullName;
                evidence["HelperAdmissionPhase"] = failure.Data["ParticipatingAdmissionPhase"] as string;
                evidence["HelperTimedOut"] = failure is TimeoutException;
            }
            evidence.TryAdd("HelperTimedOut", false);
            evidence["HelperExit"] = helperExit;
            evidence["HelperFramesBytes"] = waiter.IsCompleted ? outputFrames.Length : null;
            if (helperExit == 0)
            {
                try
                {
                    outputFrames.Position = 0;
                    await VerifyLoadHandoffSnapshotAsync(outputFrames, transfer, ownerB);
                    evidence["SingleReplacementGrantReadClose"] = true;
                }
                catch (Exception failure) { evidence["PositiveEvidenceFailure"] = failure.ToString(); }
            }
            Assert.Equal(beforeWaiterRelease, ReadRecord());
            Assert.Equal(committedGeneration, FileSystemManager.ParseSessionGenerationText(File.ReadAllText(seed.SessionGenerationPath)));
            Assert.Equal(archive, Assert.Single(Directory.GetFiles(seed.ResolvePath("saves/manual_saves"), "*.zip")));
            Assert.Equal(archiveBytes, File.ReadAllBytes(archive));
            Assert.Equal(savedMarker, File.ReadAllBytes(marker));
            Assert.False(File.Exists(seed.ResolvePath(LoadHandoffDaemonStatus)));
            Assert.False(File.Exists(seed.ResolvePath("input/turn_request.json")));
            evidence["ExactArchiveAndReplacementData"] = true;
        }
        catch (Exception failure) { evidence["SetupFailure"] = failure.ToString(); }
        finally
        {
            releaseLoad.TrySetResult();
            releaseWaiter.TrySetResult();
            // Observe an owned worker before any Shutdown. A setup failure
            // without B evidence must not adopt a Running owner from disk.
            if (waiter is { IsCompleted: false })
                try { await waiter.WaitAsync(TimeSpan.FromSeconds(2)); } catch (Exception failure) { evidence["HelperJoinFailure"] = failure.ToString(); }
            if (loading is { IsCompleted: false })
                try { await loading.WaitAsync(TimeSpan.FromSeconds(2)); } catch (Exception failure) { evidence["LoadJoinFailure"] = failure.ToString(); }
            try
            {
                if (authenticatedB is not null && (waiter?.IsCompleted ?? true))
                {
                    Assert.Same(authenticatedB, host.Owner);
                    await host.CallAsync("StopShellAsync").WaitAsync(TimeSpan.FromSeconds(5));
                    var stoppedB = ReadRecord();
                    evidence["ReplacementStopped"] = stoppedB.Disposition == GmSessionRunDisposition.Stopped
                        && stoppedB.Identity == authenticatedB.Identity && stoppedB.StopEvidence?.Identity == authenticatedB.Identity
                        && stoppedB.StopEvidence.Kind == GmSessionRunStopKind.OwnedScopeEmpty && !authenticatedB.RetainsAuthority;
                }
                else if (loading is null && ownerA is { RetainsAuthority: true } && ReferenceEquals(ownerA, host.Owner))
                    await host.CallAsync("StopShellAsync").WaitAsync(TimeSpan.FromSeconds(5));
            }
            catch (Exception failure) { evidence["CleanupFailure"] = failure.ToString(); }
            try { await supervisorControl.CancelAsync(); await server.WaitAsync(TimeSpan.FromSeconds(5)); }
            catch (Exception failure) { evidence["ServerJoinFailure"] = failure.ToString(); }
            evidence["PublicationCuts"] = loadPublicationCuts;
            evidence["MainOwnerContentions"] = contentions;
            evidence["HelperStarts"] = helperStarts;
            evidence["HelperClosingCallbacks"] = helperClosings;
            await File.WriteAllTextAsync(output, JsonSerializer.Serialize(evidence));
        }
    }

    private static async Task WaitForLoadHandoffGateAsync(Task gate, Task competing, string name)
    {
        if (await Task.WhenAny(gate, competing).WaitAsync(TimeSpan.FromSeconds(7)) != gate)
        {
            await competing;
            throw new InvalidOperationException($"Fixture ended before the actual {name}.");
        }
        await gate;
    }

    private static async Task<string> WriteLoadHandoffSnapshotRequestAsync(MemoryStream input)
    {
        var transfer = Guid.NewGuid().ToString("N");
        var request = JsonSerializer.SerializeToUtf8Bytes(new
        {
            action = "snapshot", paths = new[] { LoadHandoffDaemonStatus }, trees = Array.Empty<string>(),
            expected = new Dictionary<string, string?>(), generation = (string?)null
        }, MainOperationReader.Json);
        await MainOperationReader.WriteAsync(input, new { sequence = 1, action = "daemon-request-begin", transfer, length = request.Length, hash = GmHelperCanonicalScope.Hash(request) }, CancellationToken.None);
        await MainOperationReader.WriteAsync(input, new { sequence = 2, action = "daemon-request-chunk", transfer, offset = 0, bytes = Convert.ToBase64String(request) }, CancellationToken.None);
        await MainOperationReader.WriteAsync(input, new { sequence = 3, action = "daemon-request-end", transfer }, CancellationToken.None);
        await MainOperationReader.WriteAsync(input, new { sequence = 4, action = "daemon-response-chunk", transfer, offset = 0 }, CancellationToken.None);
        await MainOperationReader.WriteAsync(input, new { sequence = 5, action = "close", outcome = MainOperationOutcome.Completed }, CancellationToken.None);
        input.Position = 0;
        return transfer;
    }

    private static async Task VerifyLoadHandoffSnapshotAsync(MemoryStream output, string transfer, GmSessionRunCoordinator owner)
    {
        var reader = new MainOperationReader(output);
        async Task<JsonElement> NextAsync(int sequence)
        {
            var row = await reader.ReadAsync<JsonElement>(CancellationToken.None);
            Assert.True(row.GetProperty("ok").GetBoolean());
            Assert.Equal(sequence, row.GetProperty("sequence").GetInt32());
            return row;
        }
        var ready = await NextAsync(0);
        Assert.Equal("active", ready.GetProperty("state").GetString());
        var projection = ready.GetProperty("originalClose").Deserialize<MainOperationClose>(MainOperationReader.Json)!;
        Assert.Equal(owner.Identity, projection.Identity);
        Assert.Equal(MainOperationOutcome.Completed, projection.Outcome);
        Assert.False(projection.ClosingFailed);
        await NextAsync(1);
        await NextAsync(2);
        var header = await NextAsync(3);
        Assert.Equal(transfer, header.GetProperty("transfer").GetString());
        var block = await NextAsync(4);
        Assert.Equal(transfer, block.GetProperty("transfer").GetString());
        Assert.Equal(0, block.GetProperty("offset").GetInt32());
        Assert.True(block.GetProperty("complete").GetBoolean());
        var bytes = block.GetProperty("bytes").GetBytesFromBase64();
        Assert.Equal(bytes.Length, header.GetProperty("length").GetInt32());
        Assert.Equal(GmHelperCanonicalScope.Hash(bytes), header.GetProperty("hash").GetString());
        using var observation = JsonDocument.Parse(bytes);
        var data = observation.RootElement;
        Assert.True(data.GetProperty("matches").GetBoolean());
        Assert.Equal(owner.Identity.GenerationId, data.GetProperty("generation").GetString());
        var status = Assert.Single(data.GetProperty("files").EnumerateArray());
        Assert.Equal(LoadHandoffDaemonStatus, status.GetProperty("path").GetString());
        Assert.Equal("Missing", status.GetProperty("kind").GetString());
        Assert.Empty(data.GetProperty("trees").EnumerateArray());
        var close = await NextAsync(5);
        Assert.Equal("closed-observed", close.GetProperty("state").GetString());
        Assert.Equal("original-main", close.GetProperty("completionKind").GetString());
        Assert.True(close.GetProperty("closeObserved").GetBoolean());
        Assert.False(close.GetProperty("localScopeCompleted").GetBoolean());
        Assert.False(close.GetProperty("continuationFailed").GetBoolean());
        var actual = close.GetProperty("terminalClose").Deserialize<MainOperationClose>(MainOperationReader.Json)!;
        Assert.Equal(projection, actual);
        var original = owner.QueryRemoteOperation(actual);
        Assert.True(original.Ok);
        Assert.Equal(MainOperationState.ClosedObserved, original.State);
        Assert.Equal(owner.Identity, original.Identity);
        Assert.Equal(JsonValueKind.Undefined, (await reader.ReadAsync<JsonElement>(CancellationToken.None)).ValueKind);
    }
}
