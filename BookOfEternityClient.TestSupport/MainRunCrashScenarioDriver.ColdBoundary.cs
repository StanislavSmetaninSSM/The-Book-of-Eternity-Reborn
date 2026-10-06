using System.IO.Pipes;
using System.Text;
using System.Text.Json;
using BookOfEternityClient.Core;
using BookOfEternityClient.Services;
using BookOfEternityClient.Services.GmRuntime;
using Microsoft.Extensions.Logging.Abstractions;

namespace BookOfEternityClient.Tests;

internal static partial class MainRunCrashScenarioDriver
{
    private const string OldQueuedText="OLD-F3-QUEUED-NEVER-REPLAY-Ж🌌";
    private static async Task<int> WitnessColdBoundaryAsync(string boundary,string package,string folder,Dictionary<string,object?> result)
    {
        await using var original=Child(Prefix+"owner-"+(boundary=="input"?"input":"stop-ack"),package,folder);
        await WaitFileAsync(Path.Combine(folder,"cut.json"),original.Process);var info=ReadInfo(folder);
        Require(ReadRecord(info.Root).Disposition==GmSessionRunDisposition.Stopped,"Cold boundary lacks actual original stop ACK.");
        await original.KillAsync();result["OriginalStoppedApplicationKilled"]=true;
        if(boundary=="clear") {
            await using var clear=Child(Prefix+"cold-clear",package,folder);await clear.SuccessAsync();
            result["Clear"]=JsonSerializer.Deserialize<JsonElement>(File.ReadAllBytes(Path.Combine(folder,"clear.json")));
        } else {
            Require(boundary=="input" && !ReadOutput(folder,"owner-output.bin").Contains(OldQueuedText,StringComparison.Ordinal),"Queued old input reached original CLI.");
            result["OriginalInput"]=JsonSerializer.Deserialize<JsonElement>(File.ReadAllBytes(Path.Combine(folder,"original-input.json")));
        }
        await using var fresh=Child(Prefix+"cold-fresh",package,folder);await fresh.SuccessAsync();
        Require(!ReadOutput(folder,"fresh-output.bin").Contains(OldQueuedText,StringComparison.Ordinal),"Fresh CLI replayed original revoked queued text.");
        result["FreshEpoch"]=JsonSerializer.Deserialize<JsonElement>(File.ReadAllBytes(Path.Combine(folder,"cold.json")));
        result["NoOriginalInputReplay"]=true;result["Success"]=true;return 0;
    }

    private static async Task<int> ClearStoppedAsync(string folder)
    {
        var result=new Dictionary<string,object?>{["Pid"]=Environment.ProcessId};
        try {
            var info=ReadInfo(folder);var files=Files(info.Root);var before=Snapshot(info.Root);
            Require(ReadRecord(info.Root).Disposition==GmSessionRunDisposition.Stopped,"Clear lacks sealed stop.");
            await files.ClearGameStateAsync();var generation=CurrentGeneration(files);var after=Snapshot(info.Root);
            var expected=new Dictionary<string,string>(before,StringComparer.Ordinal);
            foreach(var key in before.Keys.Where(k=>k.StartsWith("game_session/game_state/",StringComparison.Ordinal) &&
                k!="game_session/game_state/control/gm_bridge_status.json" && k!="game_session/game_state/control/gm_cli_window_binding.json" &&
                !k.StartsWith("game_session/game_state/control/gm_context_pack",StringComparison.Ordinal)))expected.Remove(key);
            expected[Path.GetRelativePath(info.Root,files.SessionGenerationPath)]=after[Path.GetRelativePath(info.Root,files.SessionGenerationPath)];
            Require(generation!=info.Generation && Equal(expected,after) && !File.Exists(files.ResolvePath(ReplacementMarker)),"Actual Clear did not establish exact deletion/new generation with preserved independent evidence/library.");
            result["Before"]=before;result["After"]=after;result["OldGeneration"]=info.Generation;result["NewGeneration"]=generation;
            result["MainStopAndArchiveUnchanged"]=true;result["Success"]=true;return 0;
        } catch(Exception failure){result["Failure"]=failure.ToString();return 1;}
        finally{WriteJson(Path.Combine(folder,"clear.json"),result);}
    }

    private static async Task<int> OwnerQueuedInputAsync(string package,string folder)
    {
        try {
            var launch=NeutralTerminalLaunch.Create(package,folder);var root=Directory.GetParent(launch.Scratch)!.FullName;
            var files=Files(root);var state=PortableSaveFixture.Seed(files);
            Require(await new SaveLoadService(files,state,NullLogger<SaveLoadService>.Instance).SaveGameAsync("input-source","isolated original input cold boundary"),"Preparation: input archive failed.");
            var archive=Directory.GetFiles(files.ResolvePath("saves/manual_saves"),"*.zip").Single();
            WriteJson(Path.Combine(folder,"root.json"),new FixtureInfo(root,archive,CurrentGeneration(files)));
            using var output=new FileStream(Path.Combine(folder,"owner-output.bin"),FileMode.Create,FileAccess.Write,FileShare.ReadWrite,4096,FileOptions.Asynchronous);
            var host=new FixtureHost(launch,output);await host.CallAsync("StartShellAsync");var original=host.Owner;var terminal=host.Terminal;
            using var serverCancellation=new CancellationTokenSource();var server=host.StartServer(serverCancellation.Token);
            await WaitAsync(()=>ReadOutput(folder,"owner-output.bin").Contains("NEUTRAL READY",StringComparison.Ordinal));
            var ready=await FixtureRpcAsync(host,new{command="setReady",ready=true});
            Require(ready.GetProperty("ok").GetBoolean(),"Actual original view refused ready.");
            var binding=ready.GetProperty("status").GetProperty("inputBindingId").GetString();
            var operation=new{command="dispatchPrompt",operationId="f3-original-queued",operationKind="turn",operationRevision="f3-neutral-1",inputBindingId=binding,text=OldQueuedText,appendEnter=true};
            var gate=(SemaphoreSlim)host.Field("_promptGate")!;await gate.WaitAsync();JsonElement delivery,stop;
            try {
                var queued=FixtureRpcAsync(host,operation);await WaitAsync(()=>(int)host.Field("_admittedPrompts")!>0);
                var stopping=FixtureRpcAsync(host,new{command="stopTerminal"});
                await WaitAsync(()=>{
                    var lifetime=host.Field("_inputLifetime");return lifetime!=null && (bool)lifetime.GetType().GetField("Revoked")!.GetValue(lifetime)!;
                });
                gate.Release();delivery=await queued;stop=await stopping;
            } finally{if(gate.CurrentCount==0)gate.Release();}
            Require(delivery.GetProperty("promptDelivery").GetProperty("disposition").GetString()=="queued-cancelled" && stop.GetProperty("ok").GetBoolean() &&
                !original.RetainsAuthority && ReadRecord(root).Disposition==GmSessionRunDisposition.Stopped && terminal.RootExited.IsCompletedSuccessfully &&
                !ReadOutput(folder,"owner-output.bin").Contains(OldQueuedText,StringComparison.Ordinal),"Actual original queued input was written/replayed or stop did not settle.");
            await serverCancellation.CancelAsync();await server;
            WriteJson(Path.Combine(folder,"original-input.json"),new{OriginalOperation=operation,Delivery=delivery,Stop=stop,ActualPipeLoop=true,OriginalBindingRevoked=true,NoCliEffect=true,ActualStopAck=true});
            Cut(folder,host,"original-queued-T042-revoked-and-actual-stop-ACK",original);return 65;
        } catch(Exception failure){WriteJson(Path.Combine(folder,"owner-failure.json"),new{Failure=failure.ToString()});return 1;}
    }

    private static async Task<JsonElement> FixtureRpcAsync(FixtureHost host,object request)
    {
        using var deadline=new CancellationTokenSource(TimeSpan.FromSeconds(3));
        using var pipe=new NamedPipeClientStream(".",(string)host.Field("_pipeName")!,PipeDirection.InOut,PipeOptions.Asynchronous);
        await pipe.ConnectAsync(deadline.Token);await pipe.WriteAsync(Encoding.UTF8.GetBytes(JsonSerializer.Serialize(request)+"\n"),deadline.Token);await pipe.FlushAsync(deadline.Token);
        using var reader=new StreamReader(pipe,Encoding.UTF8,leaveOpen:true);
        using var response=JsonDocument.Parse(await reader.ReadLineAsync(deadline.Token)??throw new EndOfStreamException());return response.RootElement.Clone();
    }
}
