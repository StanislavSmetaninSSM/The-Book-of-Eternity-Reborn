using System.Text.Json;
using BookOfEternityClient.Core;
using BookOfEternityClient.Services.GmRuntime;
using BookOfEternityGMBridge;

namespace BookOfEternityClient.Tests;

// Actual same-root guard contention between typed hosts in one driver process.
// Existing independent guardian owns physical cleanup; no cross-process contender or game/GM claim.
internal static class GmBridgeContenderRestartScenario
{
    internal static async Task<int> RunAsync(string folder)
    {
        var evidence = new Dictionary<string, object?>();
        var hosts = new List<(BridgeHost Host, Task<int> Run)>();
        using var deadline = new CancellationTokenSource(TimeSpan.FromSeconds(25));
        var root = Path.Combine(folder, "root");
        var session = Path.Combine(root, "game_session");
        var statusPath = Path.Combine(session, "game_state/control/gm_bridge_status.json");
        var configPath = Path.Combine(session, "config.json");
        var pipe = "contender-" + Guid.NewGuid().ToString("N");
        var held = new int[3];
        var contenderGuardHits = 0;
        var exit = 1;
        try
        {
            var config = await File.ReadAllBytesAsync(configPath, deadline.Token);
            (BridgeHost Host, Task<int> Run) Start(int index)
            {
                var host = new BridgeHost(session, pipe);
                host.ObserveMainHeldRoot = _ => Interlocked.Increment(ref held[index]);
                if (index == 1) host.ObserveMainGuardContention = () =>
                {
                    Interlocked.Increment(ref contenderGuardHits);
                    return Task.CompletedTask;
                };
                var pair = (host, host.RunAsync());
                hosts.Add(pair);
                return pair;
            }
            async Task<(string Status, GmSessionRunRecord Record)> Ready(Task<int> running, string? previous = null)
            {
                while (true)
                {
                    deadline.Token.ThrowIfCancellationRequested();
                    if (running.IsCompleted) throw new IOException("Configured host ended before Running.");
                    if (File.Exists(statusPath))
                    {
                        var status = await File.ReadAllTextAsync(statusPath, deadline.Token);
                        using var json = JsonDocument.Parse(status);
                        var record = GmSessionRunRecordCodec.Decode(GmSessionRunPersistence.Read(root) ?? throw new IOException("Missing main record."));
                        if (record.Disposition == GmSessionRunDisposition.Running && record.Identity.RunId != previous &&
                            json.RootElement.GetProperty("terminalRunId").GetString() == record.Identity.RunId &&
                            json.RootElement.TryGetProperty("transportEndpoint", out var endpoint) && endpoint.ValueKind == JsonValueKind.Object)
                            return (status, record);
                    }
                    await Task.Delay(10, deadline.Token);
                }
            }
            async Task<JsonElement> Rpc(string status, object request)
            {
                var response = await BridgeTransportConnector.RequestAsync(status, JsonSerializer.Serialize(request), 12000, deadline.Token);
                using var doc = JsonDocument.Parse(response);
                return doc.RootElement.Clone();
            }
            async Task Stop((BridgeHost Host, Task<int> Run) host, string status, GmSessionRunRecord original)
            {
                var ack = await Rpc(status, new { command = "shutdown", rootKey = original.Identity.RootKey, expectedMainIdentity = original.Identity });
                evidence["StopAck-" + original.Identity.RunId] = ack;
                if (!ack.GetProperty("ok").GetBoolean()) throw new IOException("Original scoped shutdown refused.");
                var observed = ack.GetProperty("status");
                var proof = observed.GetProperty("terminalStop");
                if (observed.GetProperty("terminalRunId").GetString() != original.Identity.RunId ||
                    proof.GetProperty("identity").GetProperty("runId").GetString() != original.Identity.RunId ||
                    proof.GetProperty("state").GetString() != "stopped-within-scope" ||
                    !proof.GetProperty("cleanupComplete").GetBoolean() || proof.GetProperty("authorityRetained").GetBoolean())
                    throw new IOException("Shutdown did not acknowledge the original owned scope.");
                if (await host.Run.WaitAsync(deadline.Token) != 0) throw new IOException("Original host failed during stop.");
                var stopped = GmSessionRunRecordCodec.Decode(GmSessionRunPersistence.Read(root) ?? throw new IOException("Missing stopped record."));
                if (stopped.Identity != original.Identity || stopped.Disposition != GmSessionRunDisposition.Stopped ||
                    stopped.StopEvidence?.Identity != original.Identity || stopped.StopEvidence.Kind != GmSessionRunStopKind.OwnedScopeEmpty)
                    throw new IOException("Original durable stop proof missing.");
                evidence["Stopped-" + original.Identity.RunId] = stopped;
            }
            var first = Start(0);
            var original = await Ready(first.Run);
            var durable = GmSessionRunPersistence.Read(root)!;
            var originalEndpoint = BridgeTransportConnector.DecodeStatus(original.Status);
            evidence["OriginalRunning"] = original.Record;
            evidence["OriginalEndpoint"] = originalEndpoint;
            var contender = Start(1);
            Exception? refusal = null;
            int? contenderExit = null;
            try { contenderExit = await contender.Run.WaitAsync(deadline.Token); }
            catch (Exception failure) when (failure is not OperationCanceledException && !deadline.IsCancellationRequested) { refusal = failure; }
            if (refusal == null && contenderExit == 0) throw new IOException("Contender unexpectedly succeeded.");
            evidence["ContenderRefusal"] = refusal?.ToString() ?? $"exit {contenderExit}";
            evidence["ContenderMainGuardContentionHits"] = contenderGuardHits;
            if (contenderGuardHits < 1) throw new IOException("Contender failed without reaching the original main guard.");
            contender.Host.Dispose();
            if (held[1] != 0 || held[0] != 1) throw new IOException("Contender created a second held native root.");
            if (!durable.AsSpan().SequenceEqual(GmSessionRunPersistence.Read(root))) throw new IOException("Contender changed original durable Running identity/state.");
            var after = await File.ReadAllTextAsync(statusPath, deadline.Token);
            if (BridgeTransportConnector.DecodeStatus(after) != originalEndpoint) throw new IOException("Contender replaced original transport descriptor.");
            using (var oldStatus = JsonDocument.Parse(original.Status))
            using (var currentStatus = JsonDocument.Parse(after))
                foreach (var field in new[] { "terminalRunId", "inputBindingId", "sessionPath", "pipeName" })
                    if (oldStatus.RootElement.GetProperty(field).GetRawText() != currentStatus.RootElement.GetProperty(field).GetRawText())
                        throw new IOException("Contender changed original status identity: " + field);
            var alive = await Rpc(after, new { command = "status" });
            if (!alive.GetProperty("ok").GetBoolean() || alive.GetProperty("status").GetProperty("terminalRunId").GetString() != original.Record.Identity.RunId)
                throw new IOException("Original status RPC unavailable after contender refusal.");
            evidence["OriginalRpcAfterRefusal"] = alive;
            await Stop(first, after, original.Record);
            first.Host.Dispose();
            var configAfterStop = await File.ReadAllBytesAsync(configPath, deadline.Token);
            if (!config.AsSpan().SequenceEqual(configAfterStop)) throw new IOException("Original config changed.");
            var restarted = Start(2);
            var next = await Ready(restarted.Run, original.Record.Identity.RunId);
            if (next.Record.Identity.Epoch <= original.Record.Identity.Epoch || next.Record.Identity.GenerationId != original.Record.Identity.GenerationId || held[2] != 1)
                throw new IOException("Same-config restart did not establish a new epoch/run in the same generation.");
            evidence["RestartedRunning"] = next.Record;
            await Stop(restarted, next.Status, next.Record);
            var configAfterRestart = await File.ReadAllBytesAsync(configPath, deadline.Token);
            if (!config.AsSpan().SequenceEqual(configAfterRestart)) throw new IOException("Restart changed original config.");
            evidence["HeldRootsByHost"] = held;
            evidence["Success"] = true;
            exit = 0;
        }
        catch (Exception failure) { evidence["Failure"] = failure.ToString(); }
        finally
        {
            foreach (var pair in hosts)
            {
                try { pair.Host.Dispose(); }
                catch (Exception failure) { evidence["DisposeFailure-" + hosts.IndexOf(pair)] = failure.ToString(); exit = 1; }
                try { await pair.Run.WaitAsync(TimeSpan.FromSeconds(3)); }
                catch (Exception failure) when (pair.Run.IsFaulted) { evidence["ObservedHostFailure-" + hosts.IndexOf(pair)] = failure.ToString(); }
                catch (Exception failure) { evidence["JoinFailure-" + hosts.IndexOf(pair)] = failure.ToString(); exit = 1; }
            }
            if (exit != 0) evidence["Success"] = false;
            await File.WriteAllTextAsync(Path.Combine(folder, "scenario.json"), JsonSerializer.Serialize(evidence));
        }
        return exit;
    }
}
