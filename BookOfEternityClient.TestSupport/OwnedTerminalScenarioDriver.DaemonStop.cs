using System.Reflection;
using System.Text.Json;
using BookOfEternityClient.Services.GmRuntime;

namespace BookOfEternityClient.Tests;
internal static partial class OwnedTerminalScenarioDriver
{
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
        using var bound = new CancellationTokenSource(TimeSpan.FromSeconds(17));
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
        evidence["ActualDaemonStatus"] = JsonSerializer.Deserialize<JsonElement>(File.ReadAllText(Path.Combine(folder,"root/game_session/game_state/control/gm_daemon_status.json")));
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
