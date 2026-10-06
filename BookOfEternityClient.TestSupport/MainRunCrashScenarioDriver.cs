using System.Diagnostics;
using System.Reflection;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using BookOfEternityClient.Configuration;
using BookOfEternityClient.Core;
using BookOfEternityClient.Services;
using BookOfEternityClient.Services.GmRuntime;
using Microsoft.Extensions.Logging.Abstractions;

namespace BookOfEternityClient.Tests;

// Real owner and cold actors are separate processes beneath the existing guardian.
// Only the surviving witness can supply its fixed fixture root to a fresh actor.
internal static partial class MainRunCrashScenarioDriver
{
    private const string Prefix="terminal-main-crash-";
    private sealed record FixtureInfo(string Root,string Archive,string Generation);
    internal static async Task<int> RunAsync(string mode,string package,string folder)
    {
        if(mode.StartsWith(Prefix+"owner-",StringComparison.Ordinal))return await OwnerAsync(mode[(Prefix.Length+6)..],package,folder);
        if(mode.StartsWith(Prefix+"cold-",StringComparison.Ordinal))return await ColdAsync(mode[(Prefix.Length+5)..],folder);
        var result=new Dictionary<string,object?>{["Mode"]=mode};
        try {
            Require(mode.StartsWith(Prefix+"launch-",StringComparison.Ordinal),"Unknown crash witness mode.");
            var cut=mode[(Prefix.Length+7)..];
            await using var owner=Child(Prefix+"owner-launch-"+cut,package,folder);
            await WaitFileAsync(Path.Combine(folder,"cut.json"),owner.Process);
            var info=ReadInfo(folder);result["Root"]=info.Root;
            var before=Snapshot(info.Root);result["BeforeDeath"]=before;
            await owner.KillAsync();result["OriginalApplicationKilled"]=true;
            await using var cold=Child(Prefix+"cold-refuse",package,folder);
            await cold.SuccessAsync();
            result["ColdActor"]=JsonSerializer.Deserialize<JsonElement>(File.ReadAllBytes(Path.Combine(folder,"cold.json")));
            Require(Equal(before,Snapshot(info.Root)),"Process death/cold refusal changed canonical/runtime evidence.");
            var output=ReadOutput(folder,"owner-output.bin");
            Require(cut=="released" ? output.Contains("TTY_READY owned=1",StringComparison.Ordinal) : output.Length==0,
                "Held launch cut released the CLI, or released cut lacks actual CLI evidence.");
            result["CliReleased"]=cut=="released";result["AfterCold"]=Snapshot(info.Root);
            result["Success"]=true;return 0;
        } catch(Exception failure){result["Failure"]=failure.ToString();return 1;}
        finally {WriteJson(Path.Combine(folder,"scenario.json"),result);}
    }

    private static async Task<int> OwnerAsync(string mode,string package,string folder)
    {
        try {
            var launch=NeutralTerminalLaunch.Create(package,folder);var root=Directory.GetParent(launch.Scratch)!.FullName;
            var files=Files(root);var state=PortableSaveFixture.Seed(files);
            await files.WriteFileAtomicAsync("game_state/world/f3-marker.bin","isolated before 🌌");
            var service=new SaveLoadService(files,state,NullLogger<SaveLoadService>.Instance);
            Require(await service.SaveGameAsync("f3-source","fixed synthetic crash fixture"),"Preparation: fixture archive creation refused.");
            var archive=Directory.GetFiles(files.ResolvePath("saves/manual_saves"),"*.zip").Single();
            using(var generation=JsonDocument.Parse(File.ReadAllBytes(files.SessionGenerationPath)))
                WriteJson(Path.Combine(folder,"root.json"),new FixtureInfo(root,archive,generation.RootElement.GetProperty("generationId").GetString()!));
            using var output=new FileStream(Path.Combine(folder,"owner-output.bin"),FileMode.Create,FileAccess.Write,FileShare.ReadWrite,4096,FileOptions.Asynchronous);
            var host=new FixtureHost(launch,output);
            var cut=mode["launch-".Length..];
            host.Set("ObserveMainMetadata",(Action<MainRunIoStage>)(stage=>{
                if(cut=="namespace" && stage==MainRunIoStage.NamespaceCreated)Cut(folder,host,"initialized-before-Prepared");
                if(stage!=MainRunIoStage.Readback)return;
                var record=ReadRecord(root);
                if(cut=="prepared" && record.Disposition==GmSessionRunDisposition.Prepared)Cut(folder,host,"Prepared-before-creation");
                if(cut=="running" && record.Disposition==GmSessionRunDisposition.Running)Cut(folder,host,"Running-before-release");
            }));
            host.Set("ObserveMainHeldRoot",(Action<int>)(pid=>{
                Require(ReadRecord(root).Disposition==GmSessionRunDisposition.Prepared,"Actual held creation preceded Prepared.");
                WriteJson(Path.Combine(folder,"held.json"),new{Pid=pid,Disposition="Prepared"});
                if(cut=="held")Cut(folder,host,"actual-held-root-before-Running");
            }));
            await host.CallAsync("StartShellAsync");
            Require(cut=="released","Selected launch crash cut was not observed.");
            await WaitAsync(()=>ReadOutput(folder,"owner-output.bin").Contains("TTY_READY owned=1",StringComparison.Ordinal));
            Require(host.Owner.Record?.Disposition==GmSessionRunDisposition.Running && !host.Owner.IsUncertain,"Released original lacks acknowledged Running.");
            Cut(folder,host,"actual-release-confirmed");return 65;
        } catch(Exception failure){WriteJson(Path.Combine(folder,"owner-failure.json"),new{Failure=failure.ToString()});return 1;}
    }

    private static async Task<int> ColdAsync(string mode,string folder)
    {
        var result=new Dictionary<string,object?>{["Mode"]=mode,["Pid"]=Environment.ProcessId};
        try {
            Require(mode=="refuse","Unknown cold actor mode.");var info=ReadInfo(folder);var files=Files(info.Root);
            var before=Snapshot(info.Root);
            GmSessionRunRecord? record=null;try{record=GmSessionRunRecordCodec.Decode(GmSessionRunPersistence.Read(info.Root)!);}catch(Exception failure){result["RecordRefusal"]=failure.Message;}
            if(record!=null){Require(record.Disposition!=GmSessionRunDisposition.Stopped,"Nonterminal cold actor received terminal record.");Require(GmSessionRunTransitions.InterpretCold(record).Disposition==GmSessionRunDisposition.Uncertain,"Cold nonterminal was revived.");result["ObservedRecord"]=record;}
            async Task Refuses(Func<Task> action,string name) {
                Exception? failure=null;try{await action();}catch(Exception error){failure=error;}
                Require(failure!=null,"Cold actor admitted "+name);result[name]=failure!.GetType().Name;
            }
            await Refuses(async()=>{await using var lease=await files.AcquireCanonicalWriteLeaseAsync();},"CanonicalLease");
            await Refuses(()=>files.WriteFileAtomicAsync("game_state/world/f3-marker.bin","must never write"),"CanonicalWrite");
            await Refuses(()=>files.ClearGameStateAsync(),"Clear");
            var state=new StateManager(files,new GameSettings(),NullLogger<StateManager>.Instance);
            var load=await new SaveLoadService(files,state,NullLogger<SaveLoadService>.Instance).LoadGameWithOutcomeAsync(info.Archive);
            Require(load.Disposition==LoadReplacementDisposition.NotLoaded,"Cold main refusal changed typed Load outcome.");result["Load"]=DescribeLoad(load);
            await Refuses(async()=>{_ =await GmSessionRunCoordinator.OpenNeutralAsync(files);},"FreshOwner");
            Require(Equal(before,Snapshot(info.Root)),"Cold actor mutated before refused admission/recovery.");
            result["Before"]=before;result["After"]=Snapshot(info.Root);result["Success"]=true;return 0;
        } catch(Exception failure){result["Failure"]=failure.ToString();return 1;}
        finally {WriteJson(Path.Combine(folder,"cold.json"),result);}
    }

    private static void Cut(string folder,FixtureHost host,string name)
    {
        var root=ReadInfo(folder).Root;
        WriteJson(Path.Combine(folder,"cut.json"),new{Name=name,Pid=Environment.ProcessId,Record=host.Owner.Record,
            DurableRecord=File.Exists(Path.Combine(root,".boe_runtime/gm-runs/main.json"))?ReadRecord(root):null,
            MetadataDebt=host.Owner.HasMetadataDebt,RetainsAuthority=host.Owner.RetainsAuthority,Uncertain=host.Owner.IsUncertain});
        using var bounded=new ManualResetEventSlim();
        if(!bounded.Wait(TimeSpan.FromSeconds(6)))throw new TimeoutException("Witness did not kill the exact original application at its controlled cut.");
    }
    private static FileSystemManager Files(string root)=>new(root,NullLogger<FileSystemManager>.Instance);
    private static object DescribeLoad(LoadReplacementResult result)=>new{Disposition=result.Disposition.ToString(),result.EstablishedGeneration,
        result.SelectedSourcePath,result.NeedsFollowUp,result.ContinuationBlocked,Failure=result.Failure?.ToString()};
    private static FixtureInfo ReadInfo(string folder)=>JsonSerializer.Deserialize<FixtureInfo>(File.ReadAllBytes(Path.Combine(folder,"root.json")))!;
    private static GmSessionRunRecord ReadRecord(string root)=>GmSessionRunRecordCodec.Decode(File.ReadAllBytes(Path.Combine(root,".boe_runtime/gm-runs/main.json")));
    private static void Require(bool value,string message){if(!value)throw new InvalidOperationException(message);}
    private static void WriteJson(string path,object value)
    {var stage=path+".stage";File.WriteAllBytes(stage,JsonSerializer.SerializeToUtf8Bytes(value));File.Move(stage,path,true);}
    private static string ReadOutput(string folder,string name)=>File.Exists(Path.Combine(folder,name))?Encoding.UTF8.GetString(File.ReadAllBytes(Path.Combine(folder,name))):"";
    private static Dictionary<string,string> Snapshot(string root)=>Directory.EnumerateFiles(root,"*",SearchOption.AllDirectories)
        .ToDictionary(path=>Path.GetRelativePath(root,path),path=>Convert.ToHexString(SHA256.HashData(File.ReadAllBytes(path))),StringComparer.Ordinal);
    private static bool Equal(Dictionary<string,string> first,Dictionary<string,string> next)=>first.Count==next.Count && first.All(p=>next.TryGetValue(p.Key,out var hash)&&hash==p.Value);
    private static async Task WaitAsync(Func<bool> ready,Process? child=null)
    {
        var watch=Stopwatch.StartNew();
        while(!ready()) {
            if(child?.HasExited==true)throw new InvalidOperationException("Controlled child exited before its required boundary.");
            if(watch.Elapsed>TimeSpan.FromSeconds(4))throw new TimeoutException("Controlled boundary not reached.");
            await Task.Delay(10);
        }
    }
    private static Task WaitFileAsync(string path,Process? child=null)=>WaitAsync(()=>File.Exists(path),child);
    private static ChildProcess Child(string mode,string package,string folder)=>new(mode,package,folder);
    private sealed class ChildProcess : IAsyncDisposable
    {
        internal Process Process{get;}
        private readonly Task<string> _output,_error;
        private readonly string _folder,_mode;
        internal ChildProcess(string mode,string package,string folder)
        {
            _folder=folder;_mode=mode;
            var start=new ProcessStartInfo(Path.Combine(Environment.GetEnvironmentVariable("DOTNET_ROOT")!,"dotnet")) {
                UseShellExecute=false,RedirectStandardOutput=true,RedirectStandardError=true};
            foreach(var argument in new[]{typeof(NativeHostScenarioDriver).Assembly.Location,mode,package,folder})start.ArgumentList.Add(argument);
            Process=Process.Start(start)!;_output=Process.StandardOutput.ReadToEndAsync();_error=Process.StandardError.ReadToEndAsync();
        }
        internal async Task KillAsync(){if(!Process.HasExited)Process.Kill();await Process.WaitForExitAsync().WaitAsync(TimeSpan.FromSeconds(3));}
        internal async Task SuccessAsync()
        {
            await Process.WaitForExitAsync().WaitAsync(TimeSpan.FromSeconds(6));
            Require(Process.ExitCode==0,"Controlled child failed: "+_mode+" "+await _error);
        }
        public async ValueTask DisposeAsync()
        {
            await KillAsync();File.WriteAllText(Path.Combine(_folder,_mode+".log"),await _output+await _error);Process.Dispose();
        }
    }
    // Test utility invokes the actual host, never wraps/replaces its session/owner.
    private sealed class FixtureHost
    {
        private readonly Type _type;
        private readonly object _host;
        internal FixtureHost(NeutralTerminalLaunch launch,Stream output)
        {
            var root=new DirectoryInfo(AppContext.BaseDirectory);while(root!=null&&!File.Exists(Path.Combine(root.FullName,"AGENTS.md")))root=root.Parent;
            var configuration=new DirectoryInfo(AppContext.BaseDirectory).Parent!.Name;
            _type=Assembly.LoadFrom(Path.Combine(root!.FullName,"BookOfEternityGMBridge/bin",configuration,"net8.0/BookOfEternityGMBridge.dll")).GetType("BookOfEternityGMBridge.BridgeHost",true)!;
            _host=Activator.CreateInstance(_type,[launch.Scratch,"f3-"+Guid.NewGuid().ToString("N")])!;
            _type.GetMethod("ConfigureNeutral",BindingFlags.Instance|BindingFlags.NonPublic)!.Invoke(_host,[launch]);Set("NeutralOutput",output);
        }
        internal object? Field(string name)=>_type.GetField(name,BindingFlags.Instance|BindingFlags.NonPublic)!.GetValue(_host);
        internal void Set(string name,object value)=>_type.GetField(name,BindingFlags.Instance|BindingFlags.NonPublic)!.SetValue(_host,value);
        internal GmSessionRunCoordinator Owner=>(GmSessionRunCoordinator)Field("_mainRun")!;
        internal IOwnedTerminalSession Terminal=>(IOwnedTerminalSession)Field("_pty")!;
        internal async Task CallAsync(string name)
        {try{await (Task)_type.GetMethod(name,BindingFlags.Instance|BindingFlags.NonPublic)!.Invoke(_host,null)!;}catch(TargetInvocationException failure){throw failure.InnerException!;}}
    }
}
