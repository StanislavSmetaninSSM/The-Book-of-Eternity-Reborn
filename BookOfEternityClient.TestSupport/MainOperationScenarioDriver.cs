using System.IO.Pipes;
using System.Reflection;
using System.Text;
using System.Text.Json;
using BookOfEternityClient.Core;
using BookOfEternityClient.Services.GmRuntime;
using Microsoft.Extensions.Logging.Abstractions;

namespace BookOfEternityClient.Tests;

internal static class MainOperationScenarioDriver
{
    internal static async Task<int> RunAsync(string mode,string package,string folder)
    {
        if(mode=="terminal-main-operation-child") {
            var files=new FileSystemManager(package,NullLogger<FileSystemManager>.Instance);
            var record=GmSessionRunRecordCodec.Decode(File.ReadAllBytes(Path.Combine(package,".boe_runtime/gm-runs/main.json")));
            try {await SessionOperationContext.RunBoundAsync(files,record.Identity.GenerationId,()=>files.WriteFileAtomicAsync("game_state/control/f2-child.txt","two 🌌 inputs"));return 0;}
            catch(Exception e){await File.WriteAllTextAsync(Path.Combine(folder,"client-failure.json"),JsonSerializer.Serialize(new{Failure=e.ToString()}));return 1;}
        }
        var result=new Dictionary<string,object?> { ["Mode"]=mode }; object? host=null; Type? type=null; Task? server=null;
        using var control=new CancellationTokenSource();
        async Task Call(string name){try{await (Task)type!.GetMethod(name,BindingFlags.Instance|BindingFlags.NonPublic)!.Invoke(host,null)!;}catch(TargetInvocationException e){throw e.InnerException!;}}
        try {
            var launch=NeutralTerminalLaunch.Create(package,folder);var root=Directory.GetParent(launch.Scratch)!.FullName;
            new FileSystemManager(root,NullLogger<FileSystemManager>.Instance).EnsureDirectoryStructure();
            var repo=TestRepo();var configuration=new DirectoryInfo(AppContext.BaseDirectory).Parent!.Name;
            type=Assembly.LoadFrom(Path.Combine(repo,"BookOfEternityGMBridge/bin",configuration,"net8.0/BookOfEternityGMBridge.dll")).GetType("BookOfEternityGMBridge.BridgeHost",true)!;
            var pipeName="f2-"+Guid.NewGuid().ToString("N");host=Activator.CreateInstance(type,[launch.Scratch,pipeName]);
            type.GetMethod("ConfigureNeutral",BindingFlags.Instance|BindingFlags.NonPublic)!.Invoke(host,[launch]);
            await Call("StartShellAsync");server=(Task)type.GetMethod("RunServerLoopAsync",BindingFlags.Instance|BindingFlags.NonPublic)!.Invoke(host,[control.Token])!;
            using var timeout=new CancellationTokenSource(TimeSpan.FromSeconds(3));
            using var pipe=new NamedPipeClientStream(".",pipeName,PipeDirection.InOut,PipeOptions.Asynchronous);
            await pipe.ConnectAsync(timeout.Token);
            var operation=Guid.NewGuid().ToString("N");
            await pipe.WriteAsync(Encoding.UTF8.GetBytes(JsonSerializer.Serialize(new{command="beginMainOperation",rootKey=root,operationId=operation})+"\n"),timeout.Token);
            using var reader=new StreamReader(pipe,Encoding.UTF8,false,1024,true);
            using var reply=JsonDocument.Parse((await reader.ReadLineAsync(timeout.Token))!);
            result["BeginResponse"]=reply.RootElement.Clone();
            if(!reply.RootElement.GetProperty("ok").GetBoolean())throw new InvalidOperationException("Actual original owner pipe did not grant a retained main operation.");
            var grant=reply.RootElement;
            object Frame(string command)=>new{command,pinId=grant.GetProperty("pinId").GetString(),closeId=grant.GetProperty("closeId").GetString(),operationId=operation,identity=grant.GetProperty("identity")};
            var owner=(GmSessionRunCoordinator)type.GetField("_mainRun",BindingFlags.Instance|BindingFlags.NonPublic)!.GetValue(host)!;
            if(mode=="terminal-main-operation-activation-exit") {
                var terminal=(IOwnedTerminalSession)type.GetField("_pty",BindingFlags.Instance|BindingFlags.NonPublic)!.GetValue(host)!;
                await terminal.StopAndObserveAsync(CancellationToken.None);
            }
            await pipe.WriteAsync(Encoding.UTF8.GetBytes(JsonSerializer.Serialize(Frame("activateMainOperation"))+"\n"),timeout.Token);
            var activationLine=await reader.ReadLineAsync(timeout.Token);
            if(mode=="terminal-main-operation-activation-exit") {
                if(activationLine!=null)throw new InvalidOperationException("Dead original terminal granted Active after peer wait.");
                if(!owner.IsUncertain)throw new InvalidOperationException("Lost prepared grant did not retain Unresolved owner.");
                result["Success"]=true;return 0;
            }
            using var activated=JsonDocument.Parse(activationLine!);
            if(!activated.RootElement.GetProperty("ok").GetBoolean())throw new InvalidOperationException("Original grant did not activate.");
            var close=new {pinId=grant.GetProperty("pinId").GetString(),closeId=grant.GetProperty("closeId").GetString(),operationId=operation,identity=grant.GetProperty("identity"),outcome=0,closingFailed=false};
            if(mode=="terminal-main-operation-omitted-close") {
                var incomplete=new{pinId=grant.GetProperty("pinId").GetString(),closeId=grant.GetProperty("closeId").GetString(),operationId=operation,identity=grant.GetProperty("identity")};
                await pipe.WriteAsync(Encoding.UTF8.GetBytes(JsonSerializer.Serialize(new{command="closeMainOperation",close=incomplete})+"\n"),timeout.Token);
                if(await reader.ReadLineAsync(timeout.Token)!=null)throw new InvalidOperationException("Incomplete terminal close frame retired original pin.");
                if(!owner.IsUncertain)throw new InvalidOperationException("Incomplete close lost original unresolved pin.");
                result["Success"]=true;return 0;
            }
            await pipe.WriteAsync(Encoding.UTF8.GetBytes(JsonSerializer.Serialize(new{command="closeMainOperation",close})+"\n"),timeout.Token);
            using var closed=JsonDocument.Parse((await reader.ReadLineAsync(timeout.Token))!);
            if(closed.RootElement.GetProperty("state").GetInt32()!=3)throw new InvalidOperationException("Actual immutable close receipt not observed.");
            if(mode=="terminal-main-operation-client-positive") {
                var start=new System.Diagnostics.ProcessStartInfo(Path.Combine(Environment.GetEnvironmentVariable("DOTNET_ROOT")!,"dotnet")){UseShellExecute=false};
                foreach(var arg in new[]{typeof(MainOperationScenarioDriver).Assembly.Location,"terminal-main-operation-child",root,folder})start.ArgumentList.Add(arg);
                using var child=System.Diagnostics.Process.Start(start)!;await child.WaitForExitAsync().WaitAsync(TimeSpan.FromSeconds(4));
                if(child.ExitCode!=0 || File.ReadAllText(new FileSystemManager(root,NullLogger<FileSystemManager>.Instance).ResolvePath("game_state/control/f2-child.txt"))!="two 🌌 inputs")throw new InvalidOperationException("Separate real client did not retain participating admission through actual finalization.");
            }
            await Call("StopShellAsync");
            if(GmSessionRunRecordCodec.Decode(File.ReadAllBytes(Path.Combine(root,".boe_runtime/gm-runs/main.json"))).Disposition!=GmSessionRunDisposition.Stopped)throw new InvalidOperationException("Closed original pin prevented durable scoped stop.");
            result["ClosedObserved"]=true;result["DurableStopped"]=true;
            result["Success"]=true;return 0;
        } catch(Exception e){result["Failure"]=e.ToString();return 1;}
        finally {
            if(host!=null){try{await Call("StopShellAsync");result["CleanupAttempted"]=true;}catch(Exception e){result["CleanupFailure"]=e.ToString();}}
            await control.CancelAsync();if(server!=null)try{await server;}catch{}
            if(host is IDisposable disposable)try{disposable.Dispose();}catch(Exception e){result["DisposeFailure"]=e.ToString();}
            await File.WriteAllTextAsync(Path.Combine(folder,"scenario.json"),JsonSerializer.Serialize(result));
        }
    }
    private static string TestRepo(){var d=new DirectoryInfo(AppContext.BaseDirectory);while(d!=null){if(File.Exists(Path.Combine(d.FullName,"AGENTS.md")))return d.FullName;d=d.Parent;}throw new InvalidOperationException();}
}
