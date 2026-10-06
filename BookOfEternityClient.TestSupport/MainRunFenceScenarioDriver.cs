using System.Diagnostics;
using System.Reflection;
using System.Text.Json;
using System.Text;
using System.IO.Pipes;
using BookOfEternityClient.Core;
using BookOfEternityClient.Services.GmRuntime;
using BookOfEternityClient.Services.GmWorkers;
using Microsoft.Extensions.Logging.Abstractions;

namespace BookOfEternityClient.Tests;

internal static class MainRunFenceScenarioDriver
{
    internal static async Task<int> RunAsync(string mode,string package,string folder)
    {
        var result=new Dictionary<string,object?>(){["Mode"]=mode};object? host=null;Type? type=null;
        object? Field(string name)=>type!.GetField(name,BindingFlags.Instance|BindingFlags.NonPublic)!.GetValue(host);
        void Set(string name,object value)=>type!.GetField(name,BindingFlags.Instance|BindingFlags.NonPublic|BindingFlags.Public)!.SetValue(host,value);
        async Task Call(string name){try{await (Task)type!.GetMethod(name,BindingFlags.Instance|BindingFlags.NonPublic)!.Invoke(host,null)!;}catch(TargetInvocationException e){throw e.InnerException!;}}
        void Require(bool condition,string message){if(!condition)throw new InvalidOperationException(message);}
        try
        {
            var launch=NeutralTerminalLaunch.Create(package,folder);var root=Directory.GetParent(launch.Scratch)!.FullName;
            var recordPath=Path.Combine(root,".boe_runtime/gm-runs/main.json");
            GmSessionRunRecord Read()=>GmSessionRunRecordCodec.Decode(File.ReadAllBytes(recordPath));
            var repo=FindRepoRoot();var configuration=new DirectoryInfo(AppContext.BaseDirectory).Parent!.Name;
            type=Assembly.LoadFrom(Path.Combine(repo,"BookOfEternityGMBridge/bin",configuration,"net8.0/BookOfEternityGMBridge.dll")).GetType("BookOfEternityGMBridge.BridgeHost",true)!;
            host=Activator.CreateInstance(type,[launch.Scratch,"f1-"+Guid.NewGuid().ToString("N")]);
            type.GetMethod("ConfigureNeutral",BindingFlags.Instance|BindingFlags.NonPublic)!.Invoke(host,[launch]);
            if(mode=="terminal-main-launch-generation") {
                var generationPath=Path.Combine(root,".boe_runtime/session-generation.json");
                byte[]? generation=null;var preparedObserved=false;
                Set("ObserveMainHeldRoot",(Action<int>)(_=>{preparedObserved=Read().Disposition==GmSessionRunDisposition.Prepared;generation=File.ReadAllBytes(generationPath);File.Delete(generationPath);}));
                Exception? failure=null;try{await Call("StartShellAsync");}catch(Exception e){failure=e;}
                Require(preparedObserved,"Prepared was absent before original process creation.");
                Require(failure is OwnedTerminalStartException,"Missing launch generation released the original terminal.");
                Require(Read().Disposition==GmSessionRunDisposition.Prepared,"Missing launch generation published Running.");
                File.WriteAllBytes(generationPath,generation!);
                result["Success"]=true;return 0;
            }
            if(mode=="terminal-main-running-debt") {
                Set("ObserveMainMetadata",(Action<MainRunIoStage>)(stage=>{if(stage==MainRunIoStage.Readback && Read().Disposition==GmSessionRunDisposition.Running)throw new IOException("Running ACK debt");}));
                OwnedTerminalStartException? partial=null;
                try{await Call("StartShellAsync");}catch(OwnedTerminalStartException e){partial=e;}
                Require(partial!=null && ReferenceEquals(partial.Owner,Field("_pty")),"Partial Running ACK debt lost its exact original exception/owner.");
                var retained=(GmSessionRunCoordinator)Field("_mainRun")!;Require(retained.HasMetadataDebt,"Running ACK debt was accepted.");
                using var control=new CancellationTokenSource();
                var server=(Task)type.GetMethod("RunServerLoopAsync",BindingFlags.Instance|BindingFlags.NonPublic)!.Invoke(host,[control.Token])!;
                async Task<JsonElement> Rpc(object message) {
                    using var timeout=new CancellationTokenSource(TimeSpan.FromSeconds(2));
                    // Use the actual fixed host pipe value, never another dispatcher.
                    using var actual=new NamedPipeClientStream(".",(string)Field("_pipeName")!,PipeDirection.InOut,PipeOptions.Asynchronous);
                    await actual.ConnectAsync(timeout.Token);await actual.WriteAsync(Encoding.UTF8.GetBytes(JsonSerializer.Serialize(message)+"\n"),timeout.Token);await actual.FlushAsync(timeout.Token);
                    using var reader=new StreamReader(actual,Encoding.UTF8);using var doc=JsonDocument.Parse((await reader.ReadLineAsync(timeout.Token))!);return doc.RootElement.Clone();
                }
                try {
                    var status=(await Rpc(new{command="status"})).GetProperty("status");
                    Require(status.GetProperty("terminalOwnerRetained").GetBoolean() && status.GetProperty("terminalUncertain").GetBoolean(),"Partial original owner not visible as uncertain.");
                    Require(!(await Rpc(new{command="addText",text="must-not-write"})).GetProperty("ok").GetBoolean(),"Partial owner admitted manual input.");
                } finally {await control.CancelAsync();await server;}
                result["Success"]=true;return 0;
            }
            await Call("StartShellAsync");
            var owner=(GmSessionRunCoordinator)Field("_mainRun")!;var terminal=(IOwnedTerminalSession)Field("_pty")!;
            var files=new FileSystemManager(root,NullLogger<FileSystemManager>.Instance);var old=Read();
            if(mode.StartsWith("terminal-main-namespace-",StringComparison.Ordinal)) {
                await owner.RunOperationAsync(async()=>{
                    await using var lease=await files.AcquireCanonicalWriteLeaseAsync();
                    var snapshot=files.ReadLocalGenerationSnapshot(lease);
                    var dirs=Directory.EnumerateDirectories(files.GameSessionPath,"*",SearchOption.AllDirectories).Prepend(files.GameSessionPath)
                        .Where(p=>p!=files.ResolvePath("saves") && !p.StartsWith(files.ResolvePath("saves")+Path.DirectorySeparatorChar,StringComparison.Ordinal));
                    var directory=new TrustedLocalNamespaceImage(TrustedLocalNamespaceKind.Directory,null);
                    var members=dirs.Select(p=>new TrustedLocalNamespaceChange(p,directory,directory)).ToList();
                    var scope=new TrustedLocalFileScope([root]);
                    foreach(var p in Directory.EnumerateFiles(files.GameSessionPath,"*",SearchOption.AllDirectories).Where(p=>!p.StartsWith(files.ResolvePath("saves")+Path.DirectorySeparatorChar,StringComparison.Ordinal))) {
                        var image=new TrustedLocalNamespaceImage(TrustedLocalNamespaceKind.File,TrustedLocalFileImage.CaptureFile(scope,p));members.Add(new(p,image,image));
                    }
                    var beforeGeneration=new TrustedLocalNamespaceImage(TrustedLocalNamespaceKind.File,TrustedLocalFileImage.FromBytes(snapshot.Bytes));
                    var afterGeneration=new TrustedLocalNamespaceImage(TrustedLocalNamespaceKind.File,TrustedLocalFileImage.FromBytes(JsonSerializer.SerializeToUtf8Bytes(new{schemaVersion=1,generationId=Guid.NewGuid().ToString("N")})));
                    members.Add(new(files.SessionGenerationPath,beforeGeneration,afterGeneration));
                    var plan=new TrustedLocalNamespacePlan(files.GameSessionPath,members,[new(files.ResolvePath("saves"),TrustedLocalNamespaceKind.Directory,0,null)]);
                    var publisher=new TrustedLocalFilePublication(files,scope);Exception? error=null;var effects=0;
                    try {
                        if(mode.EndsWith("validate",StringComparison.Ordinal))publisher.ValidateNamespaceBeforePublication(lease,snapshot.Binding,plan);
                        else {var outcome=publisher.PublishNamespaceWithOutcome(lease,snapshot.Binding,plan,(_,_)=>effects++);error=outcome.Failure;}
                    } catch(Exception e){error=e;}
                    Require(error!=null,"Active original pin admitted a generation-changing namespace plan.");
                    Require(effects==0 && !Directory.Exists(Path.Combine(root,".boe_runtime/trusted-local-publication-v1")),"Namespace generation refusal followed publication effects.");
                    Require(File.ReadAllBytes(files.SessionGenerationPath).AsSpan().SequenceEqual(snapshot.Bytes),"Namespace primitive changed original generation.");return 0;
                });
            }
            else if(mode=="terminal-main-replacement")
            {
                await owner.RunOperationAsync(async()=>{await using var l=await files.AcquireCanonicalWriteLeaseAsync();await files.WriteFileAtomicAsync(l,"game_state/marker.txt","original");return 0;});
                var before=File.ReadAllBytes(files.ResolvePath("game_state/marker.txt"));
                await owner.RunOperationAsync(async()=>{
                    Exception? error=null;try{await files.ClearGameStateAsync();}catch(Exception e){error=e;}
                    Require(error!=null,"Active clear was admitted.");
                    Require(File.Exists(files.ResolvePath("game_state/marker.txt")) && File.ReadAllBytes(files.ResolvePath("game_state/marker.txt")).AsSpan().SequenceEqual(before),"Active clear changed members before refusal.");
                    return 0;
                });
            }
            else if(mode=="terminal-main-forged-stop")
            {
                await owner.BeginStopAsync();
                Exception? error=null;try{await owner.ConfirmSettledStopAsync(terminal,new(terminal.Identity,GmWorkerStopState.StoppedWithinScope,"forged",true,false));}catch(Exception e){error=e;}
                Require(error!=null,"Caller proof retired a live original terminal.");
                Require(Read().Disposition!=GmSessionRunDisposition.Stopped,"Caller proof published Stopped.");
                Require(!terminal.RootExited.IsCompleted,"Forged proof stopped or replaced root.");
            }
            else if(mode=="terminal-main-closing" || mode=="terminal-main-pin-refusal")
            {
                var entered=new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
                var resume=new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
                var refused=new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
                var finish=new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
                var operation=owner.RunOperationAsync(async()=>{
                    if(mode=="terminal-main-closing")return await SessionOperationContext.RunBoundAsync(files,owner.Identity.GenerationId,async()=>{entered.SetResult();await resume.Task;return 0;});
                    entered.SetResult();await resume.Task;
                    try{await using var l=await files.AcquireCanonicalWriteLeaseAsync();throw new InvalidOperationException("Closed owner admitted a new lease.");}
                    catch(IOException){refused.SetResult();}
                    await finish.Task;return 0;
                });
                await entered.Task;var stop=owner.BeginStopAsync();
                for(var i=0;i<100 && Read().Disposition!=GmSessionRunDisposition.Stopping;i++)await Task.Delay(5);
                Require(Read().Disposition==GmSessionRunDisposition.Stopping,"Stopping not durable before pin drain.");resume.SetResult();
                try {
                    if(mode=="terminal-main-pin-refusal") {await refused.Task.WaitAsync(TimeSpan.FromSeconds(2));await Task.Delay(30);Require(!stop.IsCompleted,"Refused acquisition stole original operation reference.");finish.SetResult();}
                    await operation;await stop;
                } finally {resume.TrySetResult();finish.TrySetResult();try{await operation;}catch{}try{await stop;}catch{}}
            }
            else if(mode=="terminal-main-worker")
            {
                var workers=GmWorkerRootContext.Attach(files,true,null);workers.CloseForUncertainty();
                var path=Path.Combine(root,".boe_runtime/gm-workers/state.json");
                await Call("StopShellAsync");Require(Read().Disposition==GmSessionRunDisposition.Stopped,"Separate worker blocked main metadata settlement.");
                Exception? error=null;try{await using var l=await files.AcquireCanonicalWriteLeaseAsync();}catch(Exception e){error=e;}
                Require(error!=null,"Main Stopped bypassed independent worker refusal.");workers.ReleaseClient();
            }
            result["Success"]=true;return 0;
        }
        catch(Exception e){result["Failure"]=e.ToString();return 1;}
        finally
        {
            if(host!=null){try{await Call("StopShellAsync");result["CleanupAttempted"]=true;}catch(Exception e){result["CleanupFailure"]=e.ToString();}try{((IDisposable)host).Dispose();}catch(Exception e){result["DisposeFailure"]=e.ToString();}}
            await File.WriteAllTextAsync(Path.Combine(folder,"scenario.json"),JsonSerializer.Serialize(result));
        }
    }
    private static string FindRepoRoot(){var d=new DirectoryInfo(AppContext.BaseDirectory);while(d!=null){if(File.Exists(Path.Combine(d.FullName,"AGENTS.md")))return d.FullName;d=d.Parent;}throw new InvalidOperationException();}
}
