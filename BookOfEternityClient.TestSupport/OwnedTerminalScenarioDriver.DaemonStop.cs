using System.Reflection;
using BookOfEternityGMBridge;
using System.Text.Json;
using BookOfEternityClient.Services.GmRuntime;

namespace BookOfEternityClient.Tests;
internal static partial class OwnedTerminalScenarioDriver
{
    // These two modes use the actual host initialization and shipped connector.
    // Keep the existing original-pin observer below; no new reflection seam.
    private static async Task<int> RunTypedIdleDaemonStopAsync(string mode, string folder)
    {
        var evidence = new Dictionary<string,object?> { ["Mode"] = mode };
        BridgeHost? host = null;
        Task<int>? running = null;
        var exit = 1;
        using var deadline = new CancellationTokenSource(TimeSpan.FromSeconds(60));
        var session = Path.Combine(folder,"root/game_session");
        var statusPath = Path.Combine(session,"game_state/control/gm_bridge_status.json");
        try
        {
            host = new BridgeHost(session,"idle-daemon-"+Guid.NewGuid().ToString("N"));
            running = host.RunAsync();
            using (var startup = CancellationTokenSource.CreateLinkedTokenSource(deadline.Token))
            {
                startup.CancelAfter(TimeSpan.FromSeconds(5));
                while (true)
                {
                    startup.Token.ThrowIfCancellationRequested();
                    if (running.IsCompleted) throw new IOException("Bridge ended before original daemon control startup.");
                    if (File.Exists(statusPath))
                    {
                        var raw = await File.ReadAllTextAsync(statusPath,startup.Token);
                        using var status = JsonDocument.Parse(raw);
                        if (status.RootElement.TryGetProperty("terminalRunId",out var id) &&
                            !string.IsNullOrEmpty(id.GetString()) &&
                            status.RootElement.TryGetProperty("transportEndpoint",out var endpoint) && endpoint.ValueKind==JsonValueKind.Object)
                        {
                            evidence["PublishedTransport"] = endpoint.Clone();
                            break;
                        }
                    }
                    await Task.Delay(10,startup.Token);
                }
            }
            async Task<JsonElement> Rpc(object request)
            {
                var rawStatus = await File.ReadAllTextAsync(statusPath,deadline.Token);
                // Includes the original five-second pin drain without the old
                // named-pipe fixture's three-second premature RPC cutoff.
                var reply = await BridgeTransportConnector.RequestAsync(rawStatus,JsonSerializer.Serialize(request),12000,deadline.Token);
                using var parsed = JsonDocument.Parse(reply);
                return parsed.RootElement.Clone();
            }
            await RunIdleDaemonStopAsync(folder,host,host.GetType(),Rpc,evidence,
                mode=="production-main-daemon-coordinated-stop");
            var hostExit = await running.WaitAsync(deadline.Token);
            evidence["ActualHostExit"] = hostExit;
            if(hostExit!=0)throw new IOException("Actual bridge returned a failed lifecycle outcome.");
            exit=0;evidence["Success"]=true;
        }
        catch(Exception failure) { evidence["Failure"]=failure.ToString(); }
        finally
        {
            if(host!=null)try{host.Dispose();}catch(Exception failure){evidence["HostDisposeFailure"]=failure.ToString();exit=1;}
            if(running!=null)try{await running.WaitAsync(TimeSpan.FromSeconds(5));}catch(Exception failure){evidence["HostJoinFailure"]=failure.ToString();exit=1;}
            if(exit!=0)evidence["Success"]=false;
            await File.WriteAllTextAsync(Path.Combine(folder,"scenario.json"),JsonSerializer.Serialize(evidence));
        }
        return exit;
    }

    private static async Task RunIdleDaemonStopAsync(string folder, object host, Type type,
        Func<object,Task<JsonElement>> rpc, Dictionary<string,object?> evidence, bool coordinated)
    {
        var main = (GmSessionRunCoordinator)type.GetField("_mainRun",BindingFlags.Instance|BindingFlags.NonPublic)!.GetValue(host)!;
        var sync = typeof(GmSessionRunCoordinator).GetField("_sync",BindingFlags.Instance|BindingFlags.NonPublic)!.GetValue(main)!;
        MainOperationReply[] Snapshot()
        {
            lock(sync)
            {
                var pins = (System.Collections.IDictionary)typeof(GmSessionRunCoordinator).GetField("_remotePins",BindingFlags.Instance|BindingFlags.NonPublic)!.GetValue(main)!;
                return pins.Values.Cast<object>().Select(pin => (MainOperationReply)pin.GetType()
                    .GetProperty("Reply",BindingFlags.Instance|BindingFlags.NonPublic)!.GetValue(pin)!).ToArray();
            }
        }
        using var bound = new CancellationTokenSource(TimeSpan.FromSeconds(55));
        var start = new System.Diagnostics.ProcessStartInfo("/usr/bin/python3") { UseShellExecute=false,
            RedirectStandardOutput=true, RedirectStandardError=true };
        start.ArgumentList.Add(Path.Combine(folder,"ship/idle-daemon-stop.py")); start.ArgumentList.Add(folder);
        using var child = System.Diagnostics.Process.Start(start)!;
        var output = child.StandardOutput.ReadToEndAsync(); var error = child.StandardError.ReadToEndAsync();
        while(!File.Exists(Path.Combine(folder,"daemon-waiting.json")))
        { if(child.HasExited) throw new InvalidOperationException("Real idle daemon preparation failed."); await Task.Delay(1,bound.Token); }
        evidence["PinsBeforeSignal"] = Snapshot();
        MainOperationReply[] active;
        do { active=Snapshot(); if(active.Any(p=>p.State==MainOperationState.Active)) break; await Task.Delay(1,bound.Token); }
        while(!child.HasExited);
        if(!active.Any(p=>p.State==MainOperationState.Active)) throw new InvalidOperationException("No original active daemon pin observed; stop cause unqualified.");
        evidence["PinsAtSignalRequest"] = active;
        var originalDaemon = CaptureDaemonSettlement(folder,"before-stop",evidence);
        RequireDaemonSettlement(originalDaemon,settled:false);
        if (coordinated)
        {
            // Do not interrupt the participating caller's admission transport.
            // The original owner first closes admission and observes actual closes.
            var receipt = await rpc(new { command="shutdown", rootKey=main.Identity.RootKey, expectedMainIdentity=main.Identity });
            evidence["OriginalShutdownReceipt"] = receipt;
            evidence["PinsAfterOriginalShutdown"] = Snapshot();
            if (!receipt.GetProperty("ok").GetBoolean())
                throw new InvalidOperationException("Original coordinated stop unconfirmed; daemon transport remains intact.");
            var record = GmSessionRunRecordCodec.Decode(File.ReadAllBytes(Path.Combine(folder,"root/.boe_runtime/gm-runs/main.json")));
            if (record.Disposition!=GmSessionRunDisposition.Stopped || !GmSessionRunValidation.IdentityMatches(record.Identity,main.Identity))
                throw new InvalidOperationException("Original coordinated durable stop is unconfirmed.");
            evidence["OriginalStoppedBeforeDaemonSignal"] = record;
        }
        await File.WriteAllTextAsync(Path.Combine(folder,"daemon-stop-request.json"),JsonSerializer.Serialize(active),bound.Token);
        await child.WaitForExitAsync(bound.Token);
        evidence["PinsAfterDaemonExit"] = Snapshot();
        await File.WriteAllTextAsync(Path.Combine(folder,"idle-daemon-driver.log"),(await output)+(await error));
        var terminal=JsonSerializer.Deserialize<JsonElement>(File.ReadAllText(Path.Combine(folder,"idle-daemon-terminal.json")));
        evidence["ActualDaemonTerminal"] = terminal;
        // Historical controls proved pins/physical exit only. Terminal settlement is a separate gate.
        var finalDaemon = CaptureDaemonSettlement(folder,"after-eof",evidence);
        evidence["ActualDaemonStatus"] = finalDaemon.Status;
        RequireDaemonSettlement(finalDaemon,settled:true,expectedOwner:originalDaemon.Owner);
        if(!terminal.GetProperty("EOF").GetBoolean() || terminal.GetProperty("ExitCode").GetInt32()!=0 ||
            !terminal.GetProperty("TermiosRestored").GetBoolean())
            throw new InvalidOperationException("Original daemon EOF/exit/terminal restoration is unconfirmed.");
        evidence["OriginalDaemonSettlementVerified"] = true;
        var final=Snapshot(); evidence["PinsBeforeOriginalShutdown"] = final;
        if(child.ExitCode!=0 || final.Any(p=>p.State!=MainOperationState.ClosedObserved || p.Identity==null ||
            !GmSessionRunValidation.IdentityMatches(p.Identity,main.Identity)))
            throw new InvalidOperationException("Original daemon foreground stop left an unclosed original pin: "+JsonSerializer.Serialize(final));
        if (!coordinated)
        {
            var stopped=await rpc(new {command="shutdown",rootKey=main.Identity.RootKey,expectedMainIdentity=main.Identity});
            evidence["OriginalShutdownReceipt"] = stopped;
            if(!stopped.GetProperty("ok").GetBoolean()) throw new InvalidOperationException("Original daemon stop cleanup unconfirmed.");
        }
    }
}
