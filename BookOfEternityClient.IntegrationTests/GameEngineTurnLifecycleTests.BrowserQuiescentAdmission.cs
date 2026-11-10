using System.Reflection;
using System.Text.Json;
using System.Text.Json.Nodes;
using BookOfEternityClient.Core;
using BookOfEternityClient.Services;
using BookOfEternityClient.Services.GmRuntime;
using Microsoft.Extensions.Logging.Abstractions;
using Xunit;

namespace BookOfEternityClient.Tests;

public sealed partial class GameEngineTurnLifecycleTests
{
    [Theory]
    [InlineData(0)] // originally absent -> genuine newly Running at open
    [InlineData(1)] // genuine stopped -> genuine newly Running at open
    [InlineData(2)] // genuine stopped -> absent at physical guard
    [InlineData(3)] // genuine stopped -> different genuine stopped at guard
    [InlineData(4)] // genuine stopped unchanged positive
    public async Task BrowserOriginalAdmission_ActualQuiescentEvidenceMustRemainExactBeforeOpening(int mode)
    {
        var own = Path.Combine("/tmp","gc-"+Guid.NewGuid().ToString("N")[..12]);
        Directory.CreateDirectory(own);
        _directGachaOutput?.WriteLine("Owned actual staging cut evidence: "+own);
        var package = Path.Combine(own,"package");
        await RunBrowserColdChildAsync("pwsh",["-NoLogo","-NoProfile","-File",Path.Combine(TestRepoPaths.RepoRoot,"scripts/build-linux-supervisor.ps1"),
            "-OutputDirectory",package,"-IncludeHostGuardian","-IncludeTerminalFixture"],Path.Combine(own,"native.log"),25);
        var support = Path.Combine(Path.GetDirectoryName(typeof(GameEngineTurnLifecycleTests).Assembly.Location)!,"BookOfEternityClient.TestSupport.dll");
        await RunBrowserColdChildAsync(Path.Combine(package,"host-guardian"),["--live-turn",Path.Combine(own,"guardian.json"),"25000",
            Path.Combine(Environment.GetEnvironmentVariable("DOTNET_ROOT")!,"dotnet"),support,"engine-browser-quiescent-admission",
            typeof(GameEngineTurnLifecycleTests).Assembly.Location,package,Path.Combine(own,"result.json"),mode.ToString(),own],Path.Combine(own,"probe.log"),30);
        var guardian = JsonNode.Parse(File.ReadAllText(Path.Combine(own,"guardian.json")))!;
        Assert.True(guardian["echild"]!.GetValue<bool>());
        Assert.Equal(0,guardian["driverExitCode"]!.GetValue<int>());
        Assert.Equal(0,guardian["failures"]!.GetValue<int>());
        Assert.Equal(0,guardian["emergencySignals"]!.GetValue<int>());
        Assert.False(guardian["deadline"]!.GetValue<bool>());
        var result = JsonNode.Parse(File.ReadAllText(Path.Combine(own,"result.json")))!;
        Assert.True(result["BehaviorMatched"]!.GetValue<bool>(),result.ToJsonString());
    }

    public static async Task WriteBrowserQuiescentAdmissionProbeAsync(string package,string output,int mode,string own)
    {
        using var factory = new GameEngineTurnLifecycleTests();
        var launch = NeutralTerminalLaunch.Create(package,own);
        var root = Directory.GetParent(launch.Scratch)!.FullName;
        var files = new FileSystemManager(root,NullLogger<FileSystemManager>.Instance);
        files.EnsureDirectoryStructure();
        var configuration = new DirectoryInfo(AppContext.BaseDirectory).Parent!.Name;
        var type = Assembly.LoadFrom(Path.Combine(TestRepoPaths.RepoRoot,"BookOfEternityGMBridge/bin",configuration,"net8.0/BookOfEternityGMBridge.dll"))
            .GetType("BookOfEternityGMBridge.BridgeHost",true)!;
        var host = Activator.CreateInstance(type,[launch.Scratch,"c5-quiescent-"+Guid.NewGuid().ToString("N")])!;
        type.GetMethod("ConfigureNeutral",BindingFlags.Instance|BindingFlags.NonPublic)!.Invoke(host,[launch]);
        async Task Call(string name) => await (Task)type.GetMethod(name,BindingFlags.Instance|BindingFlags.NonPublic)!.Invoke(host,null)!;
        using var control = new CancellationTokenSource();
        var server = (Task)type.GetMethod("RunServerLoopAsync",BindingFlags.Instance|BindingFlags.NonPublic)!.Invoke(host,[control.Token])!;
        GmSessionRunCoordinator? Owner() => (GmSessionRunCoordinator?)type.GetField("_mainRun",BindingFlags.Instance|BindingFlags.NonPublic)!.GetValue(host);
        var matched = false;
        var blocked = false;
        var minted = false;
        var hookReached = false;
        Exception? cleanupFailure = null;
        GmSessionRunRecord? originalStopped = null;
        try
        {
            if(mode!=0) { await Call("StartShellAsync");await Call("StopShellAsync"); originalStopped=GmSessionRunRecordCodec.Decode(GmSessionRunPersistence.Read(root)!); }
            var (_,staged) = await factory.PrepareBrowserInputStagingAsync(files:files);
            var condition = GameEngine.ValidateDetachedBrowserBinding(staged).BrowserOriginalMainCondition!;
            Assert.Equal(mode==0?"quiescentAbsent":"quiescentStopped",condition.Kind);
            Assert.Equal(originalStopped,condition.StoppedRecord);
            var cold = new FileSystemManager(root,NullLogger<FileSystemManager>.Instance,PhysicalLoadTransactionOperations.Instance,
                new FileSystemManagerHooks{AfterBrowserOriginalPreflightAsync=async()=>
                {
                    if(hookReached)return;
                    hookReached=true;
                    if(mode==2)File.Delete(Path.Combine(root,".boe_runtime/gm-runs/main.json"));
                    else if(mode is 0 or 1 or 3)
                    {
                        // Explicit fixture combination: produce a genuine later
                        // run/stop while preserving exact old signed artifacts.
                        // This setup is not a production recovery permission.
                        var paths=new[]{PendingPlayerActionService.PendingPath,"game_state/control/pending_turn_snapshot.json"};
                        var retained=paths.ToDictionary(path=>path,path=>File.ReadAllBytes(files.ResolvePath(path)));
                        foreach(var path in paths)File.Delete(files.ResolvePath(path));
                        try
                        {
                            await Call("StartShellAsync");
                            await ((TaskCompletionSource)type.GetField("_firstStatus",BindingFlags.Instance|BindingFlags.NonPublic)!.GetValue(host)!).Task.WaitAsync(TimeSpan.FromSeconds(3));
                            if(mode==3)await Call("StopShellAsync");
                        }
                        finally {foreach(var pair in retained)File.WriteAllBytes(files.ResolvePath(pair.Key),pair.Value);}
                    }
                }});
            await using(var admission=cold.BeginParticipatingMainAdmission())
            {
                try
                {
                    await admission.AcquireAsync();
                    minted=admission.WasRemote;
                    // A causal RED must close the genuine mistakenly granted
                    // original connection before asserting or fixture stopping.
                    await admission.CompleteAsync(MainOperationOutcome.Completed,false);
                }
                catch(Exception failure) when(failure is IOException or InvalidDataException or InvalidOperationException) { blocked=true; }
            }
            matched=hookReached && (mode==4 ? !blocked && !minted : blocked && !minted);
        }
        finally
        {
            if(Owner()?.Record?.Disposition==GmSessionRunDisposition.Running)
            {
                var paths=new[]{PendingPlayerActionService.PendingPath,"game_state/control/pending_turn_snapshot.json"};
                var retained=paths.ToDictionary(path=>path,path=>File.ReadAllBytes(files.ResolvePath(path)));
                foreach(var path in paths)File.Delete(files.ResolvePath(path));
                try{await Call("StopShellAsync");}
                catch(Exception failure){cleanupFailure=failure;}
                finally{foreach(var pair in retained)File.WriteAllBytes(files.ResolvePath(pair.Key),pair.Value);}
            }
            await control.CancelAsync();
            await server.WaitAsync(TimeSpan.FromSeconds(5));
            ((IDisposable)host).Dispose();
        }
        await File.WriteAllTextAsync(output,JsonSerializer.Serialize(new{Mode=mode,BehaviorMatched=matched && cleanupFailure==null,Blocked=blocked,MintedRemotePin=minted,
            HookReached=hookReached,OriginalStopped=originalStopped,OriginalUncertain=Owner()?.IsUncertain,CleanupFailure=cleanupFailure?.ToString(),Scope="genuine NativeLineage original owner and guarded original browser staging; no Program/relay/model"}));
    }
}
