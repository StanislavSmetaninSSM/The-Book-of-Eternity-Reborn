using System.Diagnostics;
using System.Net.Sockets;
using System.Text.Json;
using BookOfEternityClient.Core;
using BookOfEternityClient.Services.GmRuntime;
using BookOfEternityGMBridge;
using Microsoft.Extensions.Logging.Abstractions;

namespace BookOfEternityClient.Tests;

// Real typed host and shipped independent launcher processes beneath host-guardian.
// Neutral terminal exercises ownership/transport; it is not a live GM game claim.
internal static partial class GmBridgeConsumerScenarioDriver
{
    internal static async Task<int> RunAsync(string mode, string package, string output)
    {
        if (mode is not ("terminal-main-consumer-fifo" or "terminal-main-consumer-load-fifo" or "terminal-main-consumer-pidfd-soak" or "terminal-main-prepared-stop-race" or "terminal-main-loss-prepared" or "terminal-main-loss-active" or "terminal-main-nested-heartbeat" or "terminal-main-held-ready" or "terminal-main-explorer-dispatch" or "terminal-main-explorer-stopping"))
            throw new ArgumentException("Unknown bridge consumer mode.", nameof(mode));
        var soak = mode == "terminal-main-consumer-pidfd-soak";
        var race = mode == "terminal-main-prepared-stop-race";
        var loss = mode is "terminal-main-loss-prepared" or "terminal-main-loss-active";
        var nested = mode == "terminal-main-nested-heartbeat";
        var heldReady = mode == "terminal-main-held-ready";
        var explorer = mode is "terminal-main-explorer-dispatch" or "terminal-main-explorer-stopping";
        var diagnostic = soak || race || loss || nested || heldReady;
        var expectedLossProven = false;
        var priorNonce = Environment.GetEnvironmentVariable(GmSessionRunCoordinator.StopDiagnosticNonceVariable);
        if (diagnostic) Environment.SetEnvironmentVariable(GmSessionRunCoordinator.StopDiagnosticNonceVariable, Guid.NewGuid().ToString("N"));
        string? status = null;
        GmSessionRunRecord? original = null;
        var stopAttempted = false;
        var result = new Dictionary<string, object?> { ["Mode"] = mode };
        if (diagnostic) result["StopDiagnosticNonce"] = Environment.GetEnvironmentVariable(GmSessionRunCoordinator.StopDiagnosticNonceVariable);
        BridgeHost? host = null;
        Task<int>? running = null;
        GmMainOperationClient? pin = null;
        var exit = 1;
        using var deadline = new CancellationTokenSource(TimeSpan.FromSeconds(soak ? 140 : nested || heldReady || explorer ? 30 : race || loss ? 20 : 25));
        try
        {
            var expectedKind = "named-pipe-v1";
            try { using var probe = new Socket(AddressFamily.Unix, SocketType.Stream, ProtocolType.Unspecified); }
            catch (SocketException error) when (error.NativeErrorCode is 1 or 13) { expectedKind = "linux-fifo-v1"; }
            result["ObservedSocketCapability"] = expectedKind;
            if (soak && expectedKind != "linux-fifo-v1") throw new IOException("Explicit FIFO soak requires observed unavailable Unix sockets; no forced transport fallback.");
            var launch = NeutralTerminalLaunch.Create(package, output);
            var root = Directory.GetParent(launch.Scratch)!.FullName;
            var files = new FileSystemManager(root, NullLogger<FileSystemManager>.Instance);
            files.EnsureDirectoryStructure();
            var explorerFixture = explorer ? await ExplorerParticipationFixture.CreateAsync(files) : null;
            host = new BridgeHost(launch.Scratch, "consumer-" + Guid.NewGuid().ToString("N"));
            host.ConfigureNeutral(launch);
            var stoppingReadback = new TaskCompletionSource<GmSessionRunRecord>(TaskCreationOptions.RunContinuationsAsynchronously);
            if (race || mode == "terminal-main-explorer-stopping") host.ObserveMainMetadata = stage =>
            {
                if (stage != MainRunIoStage.Readback) return;
                try
                {
                    var observed = GmSessionRunRecordCodec.Decode(GmSessionRunPersistence.Read(root)
                        ?? throw new IOException("Original metadata absent at writer readback."));
                    if (observed.Disposition == GmSessionRunDisposition.Stopping)
                        stoppingReadback.TrySetResult(observed);
                }
                catch (Exception error) { stoppingReadback.TrySetException(error); }
            };
            running = host.RunAsync();

            using var startupDeadline = CancellationTokenSource.CreateLinkedTokenSource(deadline.Token);
            startupDeadline.CancelAfter(TimeSpan.FromSeconds(5));
            while (status == null)
            {
                startupDeadline.Token.ThrowIfCancellationRequested();
                if (running.IsCompleted)
                    throw new IOException($"Actual bridge ended before publication (exit {await running}).");
                var candidate = await files.ReadDiagnosticStatusAsync("game_state/control/gm_bridge_status.json");
                if (candidate != null)
                {
                    using var document = JsonDocument.Parse(candidate);
                    // First canonical terminal status is published only after the
                    // original launch reaches Running. Before it, metadata may be
                    // legitimately staged by admission and is not an observer input.
                    if (document.RootElement.TryGetProperty("terminalRunId", out var run) &&
                        run.ValueKind == JsonValueKind.String && !string.IsNullOrEmpty(run.GetString()))
                    {
                        var recordBytes = GmSessionRunPersistence.Read(root)
                            ?? throw new IOException("Published terminal has no original run record.");
                        var record = GmSessionRunRecordCodec.Decode(recordBytes);
                        if (record.Disposition == GmSessionRunDisposition.Running && run.GetString() == record.Identity.RunId)
                        {
                            original = record;
                            status = candidate;
                            break;
                        }
                    }
                }
                await Task.Delay(10, startupDeadline.Token);
            }
            var endpoint = BridgeTransportConnector.DecodeStatus(status);
            if (endpoint.Kind != expectedKind) throw new IOException("Actual host transport disagrees with fresh socket capability.");
            if (!TransportEntryExists(endpoint.Address)) throw new IOException("Published transport entry is absent before use.");
            result["ActualEndpoint"] = endpoint;
            if (explorer)
            {
                if (mode == "terminal-main-explorer-stopping")
                    await VerifyExplorerStoppingAsync(explorerFixture!, stoppingReadback.Task, original!, output, result, deadline.Token);
                else
                {
                    await VerifyExplorerDispatchAsync(explorerFixture!, original!, result, deadline.Token);
                    using var shutdown = await LauncherAsync("shutdown-bridge", launch.Scratch, output, deadline.Token);
                    if (!shutdown.RootElement.GetProperty("ok").GetBoolean() || shutdown.RootElement.GetProperty("fallbackUsed").GetBoolean() ||
                        shutdown.RootElement.GetProperty("status").GetString() != "original-scoped-stopped") throw new IOException("Original Explorer shutdown receipt missing.");
                }
            }
            else if (heldReady)
            {
                if(endpoint.Kind!="linux-fifo-v1")throw new IOException("Held-ready control requires actual FIFO.");
                var directory=new DirectoryInfo(AppContext.BaseDirectory);
                while(directory!=null&&!File.Exists(Path.Combine(directory.FullName,"AGENTS.md")))directory=directory.Parent;
                var repo=directory?.FullName??throw new IOException("Source root missing.");
                loss=await VerifyHeldReadyAsync(files,status,launch.Scratch,host,repo,output,original!,result,deadline.Token);
                if(loss)
                {
                    await VerifyOriginalUnresolvedStopAsync(status,original!,root,output,result,deadline.Token);
                    expectedLossProven=true;
                }
                else await StopOriginalSoakAsync(status,original!,output);
            }
            else if (loss)
            {
                await VerifyUnresolvedLossAsync(mode, status, original!, root, output, result, deadline.Token);
                expectedLossProven = true;
            }
            else if (mode == "terminal-main-prepared-stop-race")
            {
                await VerifyPreparedStopRaceAsync(status, original!, root, output, result, stoppingReadback.Task, deadline.Token);
            }
            else
            {
            pin = await GmMainOperationClient.OpenAsync(files, deadline.Token, original!.Identity);
            if (pin.Identity != original.Identity) throw new IOException("Actual main grant changed original identity.");
            result["ActualMainGrantAndActivation"] = true;

            using (var diagnostics = await LauncherAsync("diagnostics", launch.Scratch, output, deadline.Token))
            {
                var reply = diagnostics.RootElement;
                if (!reply.GetProperty("ok").GetBoolean() ||
                    reply.GetProperty("status").GetProperty("terminalRunId").GetString() != original.Identity.RunId)
                    throw new IOException("Shipped launcher did not observe the original running terminal.");
                result["IndependentLauncherDiagnostics"] = true;
            }
            await pin.CompleteAsync(MainOperationOutcome.Completed, false);
            if (!pin.CloseObserved || pin.TerminalClose?.Identity != original.Identity)
                throw new IOException("Original main close ACK did not settle on its original stream.");
            result["OriginalCloseObserved"] = true;
            await pin.DisposeAsync();
            pin = null;

            if (nested) await VerifyNestedParticipationAsync(status, launch.Scratch, output, original, result, deadline.Token);

            if (soak) await ObserveOriginalSoakAsync(status, original, root, running, output, result, deadline.Token);

            if (mode == "terminal-main-consumer-load-fifo")
            {
                // Real Load stop/finish IPC without archive replacement or restart.
                // The original host sees both receipts on the same accepted stream.
                var generationBefore = await File.ReadAllBytesAsync(files.SessionGenerationPath, deadline.Token);
                var receipts = new List<(Stream Stream, GmLoadMainState State)>();
                host.BeforeLoadReceipt = (stream, state) => receipts.Add((stream, state));
                try
                {
                    await using var load = GmLoadSessionOperation.Reserve(files, Guid.NewGuid().ToString("N"),
                        "transport-no-replacement", original.Identity.GenerationId);
                    await load.StopAsync(deadline.Token);
                    if (!load.HadSession || load.State != GmLoadMainState.Stopped)
                        throw new IOException("Actual Load did not receive original terminal stop acknowledgement.");
                    var afterStop = GmSessionRunRecordCodec.Decode(GmSessionRunPersistence.Read(root) ?? throw new IOException("Load stopped record missing."));
                    if (afterStop.Identity != original.Identity || afterStop.Disposition != GmSessionRunDisposition.Stopped)
                        throw new IOException("Load did not stop the original terminal durably.");
                    if (await load.FinishAsync(false, false, null) != GmLoadMainState.Stopped)
                        throw new IOException("No-replacement Load finish did not retain Stopped.");
                    if (receipts.Count != 2 || !ReferenceEquals(receipts[0].Stream, receipts[1].Stream) ||
                        receipts.Any(receipt => receipt.State != GmLoadMainState.Stopped))
                        throw new IOException("Load receipts did not use the one original accepted channel.");
                    var generationAfter = await File.ReadAllBytesAsync(files.SessionGenerationPath, deadline.Token);
                    if (!generationBefore.AsSpan().SequenceEqual(generationAfter))
                        throw new IOException("Transport-only Load changed the installed generation.");
                    result["ActualLoadStopFinishOriginalStream"] = true;
                    result["LoadNoReplacementNoRestart"] = true;
                }
                finally { host.BeforeLoadReceipt = null; }
            }

            if (soak)
            {
                stopAttempted = true;
                await StopOriginalSoakAsync(status, original, output);
            }
            else using (var shutdown = await LauncherAsync("shutdown-bridge", launch.Scratch, output, deadline.Token))
            {
                var reply = shutdown.RootElement;
                if (!reply.GetProperty("ok").GetBoolean() || reply.GetProperty("fallbackUsed").GetBoolean() ||
                    reply.GetProperty("status").GetString() != "original-scoped-stopped")
                    throw new IOException("Shipped launcher did not receive original scoped shutdown receipt.");
                result["IndependentLauncherShutdown"] = true;
            }
            }
            if (!loss)
            {
            if (await running.WaitAsync(deadline.Token) != 0) throw new IOException("Actual bridge returned failure.");
            var stopped = GmSessionRunRecordCodec.Decode(GmSessionRunPersistence.Read(root) ?? throw new IOException("Stopped record missing."));
            if (stopped.Identity != original.Identity || stopped.Disposition != GmSessionRunDisposition.Stopped)
                throw new IOException("Original terminal did not become durably Stopped.");
            if (diagnostic && (stopped.StopEvidence?.Identity != original.Identity ||
                stopped.StopEvidence.Kind != GmSessionRunStopKind.OwnedScopeEmpty))
                throw new IOException("Soak original durable stop evidence missing.");
            if (TransportEntryExists(endpoint.Address)) throw new IOException("Original transport entry survived actual host shutdown.");
            result["OriginalStoppedAndEndpointRemoved"] = true;
            }
            result["Success"] = true;
            exit = 0;
        }
        catch (Exception error) { result["Failure"] = error.ToString(); }
        finally
        {
            // Soak reserves 5s pin + 8s scoped stop + 5s Dispose + 5s join below the 170s guardian.
            if (soak && pin != null)
            {
                try
                {
                    var ownedPin = pin;
                    async Task CloseOwnedPin()
                    {
                        try
                        {
                            if (ownedPin.TerminalClose == null)
                                await ownedPin.CompleteAsync(MainOperationOutcome.Failed, true);
                        }
                        finally { await ownedPin.DisposeAsync(); }
                    }
                    await CloseOwnedPin().WaitAsync(TimeSpan.FromSeconds(5));
                }
                catch (Exception error) { result["PinDisposeFailure"] = error.ToString(); exit = 1; }
                pin = null; // One attempt on this exact owned pin, never reopen/retry.
            }
            if (soak && !stopAttempted && status != null && original != null)
            {
                stopAttempted = true;
                try { await StopOriginalSoakAsync(status, original, output); }
                catch (Exception error) { result["OriginalCleanupStopFailure"] = error.ToString(); exit = 1; }
            }
            if (pin != null)
                try { await pin.DisposeAsync(); }
                catch (Exception error) { result["PinDisposeFailure"] = error.ToString(); exit = 1; }
            if ((loss || explorer) && host != null && running != null)
            {
                // Two serialized original stop attempts each retain the production 5s
                // pin drain. One 18s observation budget leaves reserve under guardian50.
                using var teardown = new CancellationTokenSource(TimeSpan.FromSeconds(18));
                var dispose = Task.Run(host.Dispose);
                async Task ObserveOriginalTask(Task task, string label)
                {
                    try { await task.WaitAsync(teardown.Token); }
                    catch (Exception error)
                    {
                        result[label + "Failure"] = error.ToString();
                        if (!expectedLossProven || !task.IsFaulted ||
                            !IsExpectedUnresolvedStop(error, original!)) exit = 1;
                    }
                    result[label + "Completed"] = task.IsCompleted;
                    if (!task.IsCompleted) exit = 1;
                }
                await ObserveOriginalTask(dispose, "HostDispose");
                await ObserveOriginalTask(running, "HostJoin");
            }
            else
            {
                if (host != null)
                    try
                    {
                        if (diagnostic) await Task.Run(host.Dispose).WaitAsync(TimeSpan.FromSeconds(5));
                        else host.Dispose();
                    }
                    catch (Exception error) { result["HostDisposeFailure"] = error.ToString(); exit = 1; }
                if (running != null)
                    try { await running.WaitAsync(TimeSpan.FromSeconds(5)); }
                    catch (Exception error) { result["HostJoinFailure"] = error.ToString(); exit = 1; }
            }
            if (diagnostic) Environment.SetEnvironmentVariable(GmSessionRunCoordinator.StopDiagnosticNonceVariable, priorNonce);
            if (exit != 0) result["Success"] = false;
            await File.WriteAllTextAsync(Path.Combine(output, "scenario.json"), JsonSerializer.Serialize(result));
        }
        return exit;
    }

    private static bool TransportEntryExists(string address)
        => Directory.EnumerateFileSystemEntries(Path.GetDirectoryName(address)!)
            .Any(entry => string.Equals(entry, address, StringComparison.Ordinal));

    private static async Task<JsonDocument> LauncherAsync(string command, string session, string output, CancellationToken token)
    {
        var directory = new DirectoryInfo(AppContext.BaseDirectory);
        while (directory != null && !File.Exists(Path.Combine(directory.FullName, "AGENTS.md"))) directory = directory.Parent;
        var repo = directory?.FullName ?? throw new IOException("Source-bound launcher missing.");
        var start = new ProcessStartInfo("pwsh")
        {
            UseShellExecute = false, RedirectStandardOutput = true,
            RedirectStandardError = true, RedirectStandardInput = true,
            WorkingDirectory = repo
        };
        foreach (var argument in new[] { "-NoProfile", "-File", Path.Combine(repo, "BookOfEternityClient/Launcher/bookofeternity.ps1"), command, "-SessionPath", session })
            start.ArgumentList.Add(argument);
        using var process = Process.Start(start) ?? throw new IOException("Launcher process not started.");
        process.StandardInput.Close();
        var stdout = process.StandardOutput.ReadToEndAsync();
        var stderr = process.StandardError.ReadToEndAsync();
        try { await process.WaitForExitAsync(token); }
        finally
        {
            if (!process.HasExited) { process.Kill(entireProcessTree: true); await process.WaitForExitAsync(); }
            await File.WriteAllTextAsync(Path.Combine(output, command + ".stdout.log"), await stdout);
            await File.WriteAllTextAsync(Path.Combine(output, command + ".stderr.log"), await stderr);
            await File.WriteAllTextAsync(Path.Combine(output, command + ".exit.txt"), process.ExitCode.ToString());
        }
        if (process.ExitCode != 0) throw new IOException($"Shipped launcher {command} exited {process.ExitCode}: {await stderr}");
        return JsonDocument.Parse(await stdout);
    }
}
