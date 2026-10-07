using System.Reflection;
using System.Security.Cryptography;
using System.Text.Json;
using BookOfEternityClient.Configuration;
using BookOfEternityClient.Core;
using BookOfEternityClient.Services.GmRuntime;
using Microsoft.Extensions.Logging.Abstractions;

namespace BookOfEternityClient.Tests;
internal static partial class OwnedTerminalScenarioDriver
{
    private static async Task RunActualConsoleContinueAsync(string folder,object host,Type hostType,
        Func<object,Task<JsonElement>> rpc,IOwnedTerminalSession original,Dictionary<string,object?> evidence)
    {
        var root=Path.Combine(folder,"root");var session=Path.Combine(root,"game_session");
        if(File.Exists(Path.Combine(session,"output/narrative_response.json")))throw new InvalidOperationException("Preparation must exercise missing-output read.");
        var script=Path.Combine(folder,"console-continue-input.json");
        await File.WriteAllTextAsync(script,"{\"steps\":[{\"kind\":\"key\",\"key\":\"Enter\"},{\"kind\":\"text\",\"text\":\"/options\"},{\"kind\":\"key\",\"key\":\"D4\"},{\"kind\":\"key\",\"key\":\"Enter\"},{\"kind\":\"key\",\"key\":\"Up\"},{\"kind\":\"key\",\"key\":\"Enter\"}]}");
        var start=new System.Diagnostics.ProcessStartInfo("dotnet"){UseShellExecute=false,RedirectStandardInput=true,RedirectStandardOutput=true,RedirectStandardError=true,WorkingDirectory=Path.Combine(folder,"ship")};
        foreach(var arg in new[]{Path.Combine(folder,"ship/BookOfEternityClient/BookOfEternityClient.dll"),root,"--plain-output","--e2e-script",script,"--e2e-artifacts",Path.Combine(folder,"console-observations")})start.ArgumentList.Add(arg);
        using var child=System.Diagnostics.Process.Start(start)!;child.StandardInput.Close();
        var stdout=child.StandardOutput.ReadToEndAsync();var stderr=child.StandardError.ReadToEndAsync();
        try {await child.WaitForExitAsync().WaitAsync(TimeSpan.FromSeconds(8));}
        finally {if(!child.HasExited){child.Kill();await child.WaitForExitAsync();}}
        var output=await stdout;var error=await stderr;
        await File.WriteAllTextAsync(Path.Combine(folder,"actual-console-continue.log"),output+"\n"+error);
        if(child.ExitCode!=0)throw new InvalidOperationException("Actual console Continue failed: "+output+error);
        if(!output.Contains("Ваш ход",StringComparison.Ordinal) || !output.Contains("Игровое меню",StringComparison.Ordinal))
            throw new InvalidOperationException("Actual console did not reach the player prompt/game menu.");
        if(File.Exists(Path.Combine(session,"input/turn_request.json")) || original.RootExited.IsCompleted)
            throw new InvalidOperationException("Console-only navigation submitted a turn or ended the original CLI.");
        var main=(GmSessionRunCoordinator)hostType.GetField("_mainRun",BindingFlags.Instance|BindingFlags.NonPublic)!.GetValue(host)!;
        var pins=(System.Collections.IDictionary)typeof(GmSessionRunCoordinator).GetField("_remotePins",BindingFlags.Instance|BindingFlags.NonPublic)!.GetValue(main)!;
        var receipts=new List<MainOperationReply>();
        foreach(var pin in pins.Values) {
            var reply=(MainOperationReply)pin!.GetType().GetProperty("Reply",BindingFlags.Instance|BindingFlags.NonPublic)!.GetValue(pin)!;
            if(reply.State!=MainOperationState.ClosedObserved || reply.Identity==null || !GmSessionRunValidation.IdentityMatches(reply.Identity,main.Identity))
                throw new InvalidOperationException("Actual console left an unclosed or different original pin.");
            receipts.Add(reply);
        }
        if(receipts.Count==0)throw new InvalidOperationException("Actual console used no original connection.");
        evidence["ActualConsoleContinueExitCode"]=child.ExitCode;evidence["ActualConsoleOriginalReceipts"]=receipts;
        var stop=await rpc(new{command="shutdown",rootKey=main.Identity.RootKey,expectedMainIdentity=main.Identity});
        if(!stop.GetProperty("ok").GetBoolean())throw new InvalidOperationException("Actual console original stop unconfirmed.");
        evidence["OriginalShutdownReceipt"]=stop;
    }
    private static async Task RunConsoleRuntimeAsync(string folder,object host,Type hostType,
        Func<object,Task<JsonElement>> rpc,IOwnedTerminalSession original,Dictionary<string,object?> evidence)
    {
        var files=new FileSystemManager(Path.Combine(folder,"root"),NullLogger<FileSystemManager>.Instance);
        var settings=JsonSerializer.Deserialize<GameSettings>(File.ReadAllBytes(files.ResolvePath("config.json")))!;
        var engine=ProductionLoadGameEngine.Create(files,settings);
        var main=(GmSessionRunCoordinator)hostType.GetField("_mainRun",BindingFlags.Instance|BindingFlags.NonPublic)!.GetValue(host)!;
        var methods=new[]{"NormalizeRuntimeUiArtifactsAsync","RefreshRuntimeStateAsync",
            "NormalizePendingRepairArtifactsAsync","NormalizePendingTerminalProtocolFailureArtifactsAsync","HasCurrentSessionAsync"};
        var pins=(System.Collections.IDictionary)typeof(GmSessionRunCoordinator).GetField("_remotePins",BindingFlags.Instance|BindingFlags.NonPublic)!.GetValue(main)!;
        async Task Invoke(string name) {
            var task=(Task)typeof(GameEngine).GetMethod(name,BindingFlags.Instance|BindingFlags.NonPublic)!.Invoke(engine,null)!;
            await task.WaitAsync(TimeSpan.FromSeconds(4));
            if(task is Task<bool> available && !available.Result)throw new InvalidOperationException("Real HasCurrentSession hid Continue on the healthy Running fixture.");
        }
        var receipts=new List<object>();
        foreach(var method in methods) {
            var previous=pins.Keys.Cast<string>().ToHashSet();await Invoke(method);
            var added=pins.Keys.Cast<string>().Where(k=>!previous.Contains(k)).ToArray();
            if(added.Length!=1)throw new InvalidOperationException("Consumer did not acquire exactly one original outer pin: "+method);
            var pin=pins[added[0]]!;var reply=(MainOperationReply)pin.GetType().GetProperty("Reply",BindingFlags.Instance|BindingFlags.NonPublic)!.GetValue(pin)!;
            if(reply.State!=MainOperationState.ClosedObserved || reply.Identity==null || !GmSessionRunValidation.IdentityMatches(reply.Identity,main.Identity))
                throw new InvalidOperationException("Consumer original identity or close unconfirmed: "+method);
            receipts.Add(new{Method=method,OriginalReceipt=reply});
        }
        evidence["ConsumerReceipts"]=receipts;evidence["HasCurrentSessionConfirmed"]=true;
        evidence["ActualRuntimeMethods"]=methods;evidence["OriginalClosedObservedPins"]=pins.Count;
        await main.BeginStopAsync();
        if(main.Record.Disposition!=GmSessionRunDisposition.Stopping)throw new InvalidOperationException("Original durable Stopping not reached.");
        Dictionary<string,string> Snapshot()=>Directory.GetFiles(files.GameSessionPath,"*",SearchOption.AllDirectories)
            .ToDictionary(p=>Path.GetRelativePath(files.GameSessionPath,p),p=>Convert.ToHexString(SHA256.HashData(File.ReadAllBytes(p))));
        var before=Snapshot();var refused=new List<string>();
        foreach(var method in methods) {
            try {await Invoke(method);throw new InvalidOperationException("Stopping admitted real consumer "+method);}
            catch(IOException ex) when(GmSessionRunPersistence.IsAdmissionRefusal(ex)){refused.Add(method);}
        }
        var after=Snapshot();
        if(before.Count!=after.Count || before.Any(p=>!after.TryGetValue(p.Key,out var bytes) || bytes!=p.Value))
            throw new InvalidOperationException("Refused console consumer changed canonical files.");
        evidence["StoppingRefusedBeforeEffects"]=refused;
        if(!ReferenceEquals(original,hostType.GetField("_pty",BindingFlags.Instance|BindingFlags.NonPublic)!.GetValue(host)))
            throw new InvalidOperationException("Console consumers changed the original terminal.");
        var stopped=await rpc(new {command="shutdown",rootKey=main.Identity.RootKey,expectedMainIdentity=main.Identity});
        if(!stopped.GetProperty("ok").GetBoolean())throw new InvalidOperationException("Original console scoped shutdown unconfirmed.");
        evidence["OriginalShutdownReceipt"]=stopped;
    }
}
