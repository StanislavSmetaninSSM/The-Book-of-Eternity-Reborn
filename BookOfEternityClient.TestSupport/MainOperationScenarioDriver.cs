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
        var result=new Dictionary<string,object?> { ["Mode"]=mode }; object? host=null; Type? type=null; Task? server=null; Task<int>? running=null;
        using var control=new CancellationTokenSource();
        async Task Call(string name){try{await (Task)type!.GetMethod(name,BindingFlags.Instance|BindingFlags.NonPublic)!.Invoke(host,null)!;}catch(TargetInvocationException e){throw e.InnerException!;}}
        try {
            var launch=NeutralTerminalLaunch.Create(package,folder);var root=Directory.GetParent(launch.Scratch)!.FullName;
            new FileSystemManager(root,NullLogger<FileSystemManager>.Instance).EnsureDirectoryStructure();
            var repo=TestRepo();var configuration=new DirectoryInfo(AppContext.BaseDirectory).Parent!.Name;
            type=Assembly.LoadFrom(Path.Combine(repo,"BookOfEternityGMBridge/bin",configuration,"net8.0/BookOfEternityGMBridge.dll")).GetType("BookOfEternityGMBridge.BridgeHost",true)!;
            var pipeName="f2-"+Guid.NewGuid().ToString("N");host=Activator.CreateInstance(type,[launch.Scratch,pipeName]);
            type.GetMethod("ConfigureNeutral",BindingFlags.Instance|BindingFlags.NonPublic)!.Invoke(host,[launch]);
            if(mode=="terminal-main-operation-status-admission") {
                type.GetMethod("WriteStatusFile",BindingFlags.Instance|BindingFlags.NonPublic)!.Invoke(host,null);
                if(File.Exists(Path.Combine(launch.Scratch,"game_state/control/gm_bridge_status.json")))throw new InvalidOperationException("Canonical status published before original admission.");
                result["Success"]=true;return 0;
            }
            if(mode=="terminal-main-operation-shutdown") {
                running=(Task<int>)type.GetMethod("RunAsync")!.Invoke(host,null)!;
                for(var i=0;i<200 && type.GetField("_mainRun",BindingFlags.Instance|BindingFlags.NonPublic)!.GetValue(host) is not GmSessionRunCoordinator {Record.Disposition:GmSessionRunDisposition.Running};i++)await Task.Delay(5);
            } else await Call("StartShellAsync");
            if (mode is "terminal-main-operation-status-fault" or "terminal-main-operation-status-stall") {
                var terminal=(IOwnedTerminalSession)type.GetField("_pty",BindingFlags.Instance|BindingFlags.NonPublic)!.GetValue(host)!;
                var statusOwner=(GmSessionRunCoordinator)type.GetField("_mainRun",BindingFlags.Instance|BindingFlags.NonPublic)!.GetValue(host)!;
                var entered=new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
                var release=new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
                type.GetField("BeforeStatusPublication",BindingFlags.Instance|BindingFlags.NonPublic)!.SetValue(host,(Func<Task>)(async()=>{
                    entered.TrySetResult();await release.Task;
                    if(mode.EndsWith("fault",StringComparison.Ordinal))throw new IOException("Controlled status failure.");
                }));
                type.GetMethod("WriteStatusFile",BindingFlags.Instance|BindingFlags.NonPublic)!.Invoke(host,null);
                await entered.Task.WaitAsync(TimeSpan.FromSeconds(1));
                Task? stopping=null;
                try {
                    if(mode.EndsWith("fault",StringComparison.Ordinal)) {
                        release.TrySetResult();
                        try {await (Task)type.GetField("_statusPublisher",BindingFlags.Instance|BindingFlags.NonPublic)!.GetValue(host)!;}catch(IOException){}
                        try {await Call("StopShellAsync");}catch(Exception){}
                        if(!terminal.RootExited.IsCompleted || !statusOwner.IsUncertain || statusOwner.Record?.Disposition!=GmSessionRunDisposition.Uncertain)
                            throw new InvalidOperationException("Status fault bypassed durable refusal and independent original physical stop.");
                    } else {
                        stopping=Call("StopShellAsync");await Task.Delay(150);
                        var input=type.GetField("_inputLifetime",BindingFlags.Instance|BindingFlags.NonPublic)!.GetValue(host)!;
                        if(!statusOwner.AdmissionClosed || !(bool)input.GetType().GetField("Revoked")!.GetValue(input)! || statusOwner.Record?.Disposition!=GmSessionRunDisposition.Stopping)
                            throw new InvalidOperationException("Stalled status publisher left new admissions/input open before drain.");
                        release.TrySetResult();await stopping.WaitAsync(TimeSpan.FromSeconds(3));
                        if(statusOwner.Record?.Disposition!=GmSessionRunDisposition.Stopped)throw new InvalidOperationException("Revoked queued status did not settle without new writes.");
                    }
                    result["Success"]=true;return 0;
                } finally {
                    release.TrySetResult();if(stopping!=null)try{await stopping.WaitAsync(TimeSpan.FromSeconds(3));}catch{}
                    // Independent physical cleanup is not a logical Stopped claim.
                    await terminal.StopAndObserveAsync(CancellationToken.None);result["IndependentPhysicalCleanup"]=true;
                }
            }
            if(running==null)server=(Task)type.GetMethod("RunServerLoopAsync",BindingFlags.Instance|BindingFlags.NonPublic)!.Invoke(host,[control.Token])!;
            async Task<JsonElement> Rpc(object message) {
                using var bounded=new CancellationTokenSource(TimeSpan.FromSeconds(7));
                using var request=new NamedPipeClientStream(".",pipeName,PipeDirection.InOut,PipeOptions.Asynchronous);await request.ConnectAsync(bounded.Token);
                await request.WriteAsync(Encoding.UTF8.GetBytes(JsonSerializer.Serialize(message)+"\n"),bounded.Token);
                using var input=new StreamReader(request,Encoding.UTF8);using var response=JsonDocument.Parse((await input.ReadLineAsync(bounded.Token))!);return response.RootElement.Clone();
            }
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
            Task<JsonElement>? shutdown=null;
            if(mode=="terminal-main-operation-shutdown") {
                shutdown=Rpc(new{command="shutdown"});
                for(var i=0;i<200;i++) {
                    var observed=GmSessionRunRecordCodec.Decode(File.ReadAllBytes(Path.Combine(root,".boe_runtime/gm-runs/main.json"))).Disposition;
                    if(observed==GmSessionRunDisposition.Stopping)break;
                    if(observed==GmSessionRunDisposition.Uncertain)throw new InvalidOperationException("Shutdown lost retained connection before durable Stopping and original closing.");
                    await Task.Delay(5);
                }
            }
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
            if(mode=="terminal-main-operation-query") {
                using var query=new NamedPipeClientStream(".",pipeName,PipeDirection.InOut,PipeOptions.Asynchronous);await query.ConnectAsync(timeout.Token);
                await query.WriteAsync(Encoding.UTF8.GetBytes(JsonSerializer.Serialize(new{command="mainOperationStatus",mainOperationClose=close})+"\n"),timeout.Token);
                var known=await new MainOperationReader(query).ReadAsync<MainOperationReply>(timeout.Token);
                if(known?.State!=MainOperationState.ClosedObserved)throw new InvalidOperationException("Original typed close lookup has incompatible encoding.");
            }
            if(shutdown!=null) {
                if(!(await shutdown).GetProperty("ok").GetBoolean())throw new InvalidOperationException("Actual shutdown did not settle original closing.");
                await running!.WaitAsync(TimeSpan.FromSeconds(3));
            }
            if(mode=="terminal-main-operation-retain-race") {
                var entered=new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);var release=new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);var opened=false;Task? child=null;
                var files=new FileSystemManager(root,NullLogger<FileSystemManager>.Instance);
                var borrowed=new FileSystemManager(root,NullLogger<FileSystemManager>.Instance,PhysicalLoadTransactionOperations.Instance,new FileSystemManagerHooks{
                    BeforeMainBorrowRetainAsync=async()=>{entered.TrySetResult();await release.Task;},AfterCanonicalWriteLockOpenedAsync=()=>{opened=true;return Task.CompletedTask;}});
                try {
                    await SessionOperationContext.RunBoundAsync(files,owner.Identity.GenerationId,async()=>{
                        child=Task.Run(async()=>{try{await using var lease=await borrowed.AcquireCanonicalWriteLeaseAsync();}catch(IOException){} });
                        await entered.Task.WaitAsync(TimeSpan.FromSeconds(1));return 42;
                    });
                } finally {release.TrySetResult();if(child!=null)await child.WaitAsync(TimeSpan.FromSeconds(2));}
                if(opened)throw new InvalidOperationException("Delayed borrower opened an actual canonical lease after original close receipt.");
            }
            if(mode is "terminal-main-operation-cancel-closing" or "terminal-main-operation-failed-clean-closing") {
                MainOperationClose? observed=null;type.GetField("BeforeMainCloseReply",BindingFlags.Instance|BindingFlags.NonPublic)!.SetValue(host,(Func<MainOperationClose,Task>)(close=>{observed=close;return Task.CompletedTask;}));
                var cancel=mode.EndsWith("cancel-closing",StringComparison.Ordinal);
                var files=new FileSystemManager(root,NullLogger<FileSystemManager>.Instance,PhysicalLoadTransactionOperations.Instance,new FileSystemManagerHooks{SessionOperationClosingAsync=cancel?()=>throw new IOException("Controlled closing failure."):null});
                Exception? escaped=null;
                try {await SessionOperationContext.RunBoundAsync<int>(files,owner.Identity.GenerationId,()=>cancel?Task.FromException<int>(new OperationCanceledException("Established cancellation.")):Task.FromException<int>(new InvalidOperationException("Established operation failure.")));}
                catch(Exception failure){escaped=failure;}
                if(observed?.Outcome!=(cancel?MainOperationOutcome.Cancelled:MainOperationOutcome.Failed) || observed.ClosingFailed!=cancel ||
                    (cancel && escaped is not OperationCanceledException) || (!cancel && escaped is not InvalidOperationException))
                    throw new InvalidOperationException("Established outcome/actual closing diagnostic was not retained in immutable terminal frame.");
            }
            if(mode=="terminal-main-operation-helper-oversized") {
                var start=new System.Diagnostics.ProcessStartInfo("pwsh"){UseShellExecute=false,RedirectStandardOutput=true,RedirectStandardError=true};
                foreach(var arg in new[]{"-NoProfile","-NonInteractive","-File",Path.Combine(repo,"tests/fixtures/GmMainOperation/oversized.ps1"),"-RepoRoot",repo,"-SessionPath",launch.Scratch})start.ArgumentList.Add(arg);
                using var child=System.Diagnostics.Process.Start(start)!;var output=child.StandardOutput.ReadToEndAsync();var errors=child.StandardError.ReadToEndAsync();
                try{await child.WaitForExitAsync().WaitAsync(TimeSpan.FromSeconds(5));}finally{if(!child.HasExited){child.Kill();await child.WaitForExitAsync();}}
                if(child.ExitCode!=0)throw new InvalidOperationException("Original oversized helper could not close after zero effects: "+await errors);
                if(File.Exists(Path.Combine(launch.Scratch,"game_state/control/oversized.txt")))throw new InvalidOperationException("Oversized unsent helper created canonical bytes.");
                result["HelperOutput"]=await output;
            }
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
            if(running!=null)try{await running.WaitAsync(TimeSpan.FromSeconds(8));}catch(Exception e){result["ShutdownFailure"]=e.ToString();}
            if(host!=null && running==null){try{await Call("StopShellAsync");result["CleanupAttempted"]=true;}catch(Exception e){result["CleanupFailure"]=e.ToString();}}
            await control.CancelAsync();if(server!=null)try{await server;}catch{}
            if(host is IDisposable disposable)try{disposable.Dispose();}catch(Exception e){result["DisposeFailure"]=e.ToString();}
            await File.WriteAllTextAsync(Path.Combine(folder,"scenario.json"),JsonSerializer.Serialize(result));
        }
    }
    private static string TestRepo(){var d=new DirectoryInfo(AppContext.BaseDirectory);while(d!=null){if(File.Exists(Path.Combine(d.FullName,"AGENTS.md")))return d.FullName;d=d.Parent;}throw new InvalidOperationException();}
}
