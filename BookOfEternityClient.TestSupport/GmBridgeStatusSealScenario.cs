using System.Security.Cryptography;
using System.Text.Json;
using BookOfEternityClient.Core;
using BookOfEternityClient.Services.GmRuntime;
using BookOfEternityGMBridge;
using Microsoft.Extensions.Logging.Abstractions;

namespace BookOfEternityClient.Tests;

internal static class GmBridgeStatusSealScenario
{
    private static bool OnlyOriginalFailure(Exception actual, Exception original) =>
        ReferenceEquals(actual, original) || actual is AggregateException aggregate &&
        aggregate.InnerExceptions.Count > 0 && aggregate.InnerExceptions.All(item => OnlyOriginalFailure(item, original));

    internal static async Task<int> RunAsync(string mode, string package, string output)
    {
        var initial = mode == "terminal-main-status-initial-contention";
        var evidence = new Dictionary<string, object?> { ["Mode"] = mode };
        var priorNonce = Environment.GetEnvironmentVariable(GmSessionRunCoordinator.StopDiagnosticNonceVariable);
        var nonce = Guid.NewGuid().ToString("N");
        Environment.SetEnvironmentVariable(GmSessionRunCoordinator.StopDiagnosticNonceVariable, nonce);
        evidence["Nonce"] = nonce;
        using var deadline = new CancellationTokenSource(TimeSpan.FromSeconds(35));
        var entered = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        var proceed = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        BridgeHost? host = null; Task<int>? running = null; FileStream? held = null;
        Exception? expectedInitialFailure = null;
        var exit = 1;
        try
        {
            var launch = NeutralTerminalLaunch.Create(package, output);
            var root = Directory.GetParent(launch.Scratch)!.FullName;
            var files = new FileSystemManager(root, NullLogger<FileSystemManager>.Instance);
            files.EnsureDirectoryStructure();
            var statusPath = files.ResolvePath("game_state/control/gm_bridge_status.json");
            var armed = initial ? 1 : 0;
            host = new BridgeHost(launch.Scratch, "status-seal-" + Guid.NewGuid().ToString("N"));
            host.ConfigureNeutral(launch);
            host.BeforeStatusPublication = async () =>
            {
                if (Interlocked.Exchange(ref armed, 0) == 0) return;
                held = new FileStream(files.CanonicalWriteLockPath, FileMode.Open, FileAccess.ReadWrite, FileShare.None);
                entered.TrySetResult();
                await proceed.Task.WaitAsync(deadline.Token);
            };
            running = host.RunAsync();
            string? status = null; byte[]? before = null;
            GmSessionRunRecord? original = null;
            if (!initial)
            {
                while (status == null)
                {
                    deadline.Token.ThrowIfCancellationRequested();
                    if (running.IsCompleted) throw new IOException("Host ended before original publication.");
                    var raw = await files.ReadDiagnosticStatusAsync("game_state/control/gm_bridge_status.json");
                    if (raw != null)
                    {
                        using var json = JsonDocument.Parse(raw);
                        if (json.RootElement.TryGetProperty("terminalRunId", out var id) && !string.IsNullOrEmpty(id.GetString())) status = raw;
                    }
                    if (status == null) await Task.Delay(10, deadline.Token);
                }
                if (BridgeTransportConnector.DecodeStatus(status).Kind != "linux-fifo-v1") throw new IOException("Requires genuine FIFO transport.");
                original = GmSessionRunRecordCodec.Decode(GmSessionRunPersistence.Read(root)!);
                before = File.ReadAllBytes(statusPath);
                evidence["Original"] = original.Identity;
                evidence["BeforeSHA256"] = Convert.ToHexString(SHA256.HashData(before)).ToLowerInvariant();
                Volatile.Write(ref armed, 1);
                var reply = await BridgeTransportConnector.RequestAsync(status, "{\"command\":\"setready\",\"ready\":false}", 12000, deadline.Token);
                using var ack = JsonDocument.Parse(reply);
                if (!ack.RootElement.GetProperty("ok").GetBoolean()) throw new IOException("Original status update refused.");
            }
            await entered.Task.WaitAsync(deadline.Token);
            // Exact real publisher already owns its operation, but has not entered the constructor.
            // Seal synchronously clears pending before allowing this original acquisition to proceed.
            var sealing = host.SealOriginalStatusPublicationAsync();
            proceed.TrySetResult();
            try { await sealing.WaitAsync(deadline.Token); }
            catch (Exception failure) when (initial && failure is not OperationCanceledException)
            {
                expectedInitialFailure = failure;
                evidence["OriginalFirstPublicationFailure"] = failure.ToString();
                if (failure is not IOException { InnerException: IOException originalIo } || originalIo.HResult != 11 ||
                    failure.GetType().Name != "CanonicalLockContention")
                    throw new IOException("Initial failure was not genuine constructor contention.", failure);
            }
            if (initial)
            {
                if (expectedInitialFailure == null || File.Exists(statusPath)) throw new IOException("Initial contention was deferred or published.");
                var uncertain = GmSessionRunRecordCodec.Decode(GmSessionRunPersistence.Read(root)!);
                evidence["Original"] = uncertain.Identity;
                evidence["InitialDisposition"] = uncertain.Disposition.ToString();
                // NotifyUncertain is live authority state; durable disposition is recorded, not inferred.
            }
            else
            {
                if (!before!.AsSpan().SequenceEqual(File.ReadAllBytes(statusPath))) throw new IOException("Sealed snapshot changed canonical status.");
                evidence["SealedSnapshotUnchanged"] = true;
            }
            held!.Dispose(); held = null;
            if (!initial)
            {
                var reply = await BridgeTransportConnector.RequestAsync(status!, JsonSerializer.Serialize(new {
                    command = "shutdown", rootKey = original!.Identity.RootKey, expectedMainIdentity = original.Identity
                }), 12000, deadline.Token);
                using var ack = JsonDocument.Parse(reply);
                evidence["OriginalShutdown"] = ack.RootElement.Clone();
                if (!ack.RootElement.GetProperty("ok").GetBoolean()) throw new IOException("Post-seal original shutdown refused.");
                if (await running.WaitAsync(deadline.Token) != 0) throw new IOException("Original host failed after status seal.");
                var stopped = GmSessionRunRecordCodec.Decode(GmSessionRunPersistence.Read(root)!);
                if (stopped.Identity != original.Identity || stopped.Disposition != GmSessionRunDisposition.Stopped ||
                    stopped.StopEvidence?.Kind != GmSessionRunStopKind.OwnedScopeEmpty || stopped.StopEvidence.Identity != original.Identity)
                    throw new IOException("Post-seal original stop unconfirmed.");
                evidence["Stopped"] = stopped;
            }
            exit = 0;
        }
        catch (Exception failure) { evidence["Failure"] = failure.ToString(); }
        finally
        {
            proceed.TrySetResult();
            using var cleanup = new CancellationTokenSource(TimeSpan.FromSeconds(10));
            try { held?.Dispose(); }
            catch (Exception failure) { evidence["HolderDisposeFailure"] = failure.ToString(); exit = 1; }
            Task? disposing = host == null ? null : Task.Run(host.Dispose);
            if (disposing != null) try { await disposing.WaitAsync(cleanup.Token); }
            catch (Exception failure)
            {
                evidence["DisposeFailure"] = failure.ToString();
                if (!initial || expectedInitialFailure == null || !disposing.IsCompleted ||
                    !OnlyOriginalFailure(failure, expectedInitialFailure)) exit = 1;
            }
            // Independent observation uses the same remaining cleanup budget, even if Dispose failed.
            if (running != null) try { evidence["HostExit"] = await running.WaitAsync(cleanup.Token); }
            catch (Exception failure)
            {
                evidence["HostFailure"] = failure.ToString();
                if (!initial || expectedInitialFailure == null || !running.IsCompleted || !OnlyOriginalFailure(failure, expectedInitialFailure)) exit = 1;
            }
            evidence["DisposeTaskCompleted"] = disposing?.IsCompleted == true;
            evidence["DisposeTaskStatus"] = disposing?.Status.ToString();
            evidence["HostTaskStatus"] = running?.Status.ToString();
            evidence["HostCompleted"] = running?.IsCompleted == true;
            evidence["ExpectedInitialLogicalUncertainty"] = initial && expectedInitialFailure != null;
            evidence["Success"] = exit == 0;
            Environment.SetEnvironmentVariable(GmSessionRunCoordinator.StopDiagnosticNonceVariable, priorNonce);
            await File.WriteAllTextAsync(Path.Combine(output, "scenario.json"), JsonSerializer.Serialize(evidence));
        }
        return exit;
    }
}
