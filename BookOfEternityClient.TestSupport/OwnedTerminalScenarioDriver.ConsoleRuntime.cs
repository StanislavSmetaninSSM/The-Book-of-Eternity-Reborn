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
    private static async Task RunConsoleRuntimeAsync(string folder,object host,Type hostType,
        Func<object,Task<JsonElement>> rpc,IOwnedTerminalSession original,Dictionary<string,object?> evidence)
    {
        var files=new FileSystemManager(Path.Combine(folder,"root"),NullLogger<FileSystemManager>.Instance);
        var settings=JsonSerializer.Deserialize<GameSettings>(File.ReadAllBytes(files.ResolvePath("config.json")))!;
        var engine=ProductionLoadGameEngine.Create(files,settings);
        var main=(GmSessionRunCoordinator)hostType.GetField("_mainRun",BindingFlags.Instance|BindingFlags.NonPublic)!.GetValue(host)!;
        var methods=new[]{"NormalizeRuntimeUiArtifactsAsync","RefreshRuntimeStateAsync",
            "NormalizePendingRepairArtifactsAsync","NormalizePendingTerminalProtocolFailureArtifactsAsync","HasCurrentSessionAsync"};
        async Task Invoke(string name)=>await ((Task)typeof(GameEngine).GetMethod(name,BindingFlags.Instance|BindingFlags.NonPublic)!.Invoke(engine,null)!).WaitAsync(TimeSpan.FromSeconds(4));
        foreach(var method in methods)await Invoke(method);
        var pins=(System.Collections.IDictionary)typeof(GmSessionRunCoordinator).GetField("_remotePins",BindingFlags.Instance|BindingFlags.NonPublic)!.GetValue(main)!;
        if(pins.Count<methods.Length)throw new InvalidOperationException("Real console runtime consumers did not use original connection pins.");
        foreach(var pin in pins.Values)
            if((MainOperationState)pin!.GetType().GetField("State",BindingFlags.Instance|BindingFlags.NonPublic)!.GetValue(pin)! != MainOperationState.ClosedObserved)
                throw new InvalidOperationException("Original console pin close unconfirmed.");
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
