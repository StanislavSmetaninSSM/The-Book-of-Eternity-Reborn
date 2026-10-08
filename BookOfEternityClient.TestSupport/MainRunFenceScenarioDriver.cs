using System.Diagnostics;
using System.Reflection;
using System.Text.Json;
using System.Text;
using System.IO.Pipes;
using BookOfEternityClient.Core;
using BookOfEternityClient.Services.GmRuntime;
using BookOfEternityClient.Services.GmWorkers;
using Microsoft.Extensions.Logging.Abstractions;
using BookOfEternityClient.Services;

namespace BookOfEternityClient.Tests;

internal static class MainRunFenceScenarioDriver
{
    internal static async Task<int> RunAsync(string mode,string package,string folder,SystemdControlledFixture? systemd=null)
    {
        if(mode=="terminal-main-cold-reader") {
            var files=new FileSystemManager(package,NullLogger<FileSystemManager>.Instance);
            var path=Path.Combine(package,".boe_runtime/gm-runs/main.json");var record=File.ReadAllBytes(path);var generation=File.ReadAllBytes(files.SessionGenerationPath);
            var refused=0;try{await using var lease=await files.AcquireCanonicalWriteLeaseAsync();}catch(IOException){refused++;}
            try{await files.ClearGameStateAsync();}catch(IOException){refused++;}
            var unchanged=record.AsSpan().SequenceEqual(File.ReadAllBytes(path)) && generation.AsSpan().SequenceEqual(File.ReadAllBytes(files.SessionGenerationPath));
            await File.WriteAllTextAsync(Path.Combine(folder,"unbound.json"),JsonSerializer.Serialize(new{Refused=refused,Unchanged=unchanged}));return refused==2 && unchanged?0:1;
        }
        var result=new Dictionary<string,object?>(){["Mode"]=mode};object? host=null;Type? type=null;GmSessionRunCoordinator? helperOwner=null;
        object? Field(string name)=>type!.GetField(name,BindingFlags.Instance|BindingFlags.NonPublic)!.GetValue(host);
        void Set(string name,object value)=>type!.GetField(name,BindingFlags.Instance|BindingFlags.NonPublic|BindingFlags.Public)!.SetValue(host,value);
        async Task Call(string name){try{await (Task)type!.GetMethod(name,BindingFlags.Instance|BindingFlags.NonPublic)!.Invoke(host,null)!;}catch(TargetInvocationException e){throw e.InnerException!;}}
        void Require(bool condition,string message){if(!condition)throw new InvalidOperationException(message);}
        try
        {
            var launch=NeutralTerminalLaunch.Create(package,folder);var root=Directory.GetParent(launch.Scratch)!.FullName;
            new FileSystemManager(root,NullLogger<FileSystemManager>.Instance).EnsureDirectoryStructure();
            var recordPath=Path.Combine(root,".boe_runtime/gm-runs/main.json");
            GmSessionRunRecord Read()=>GmSessionRunRecordCodec.Decode(File.ReadAllBytes(recordPath));
            var repo=FindRepoRoot();var configuration=new DirectoryInfo(AppContext.BaseDirectory).Parent!.Name;
            type=Assembly.LoadFrom(Path.Combine(repo,"BookOfEternityGMBridge/bin",configuration,"net8.0/BookOfEternityGMBridge.dll")).GetType("BookOfEternityGMBridge.BridgeHost",true)!;
            host=Activator.CreateInstance(type,[launch.Scratch,"f1-"+Guid.NewGuid().ToString("N")]);
            if(systemd==null)type.GetMethod("ConfigureNeutral",BindingFlags.Instance|BindingFlags.NonPublic)!.Invoke(host,[launch]);
            else type.GetMethod("ConfigureSystemdControlled",BindingFlags.Instance|BindingFlags.NonPublic)!.Invoke(host,[launch,systemd]);
            byte[]? recoveryIntent=null,recoveryBefore=null;var recoveryAfter=Encoding.UTF8.GetBytes("retained-after");
            var seedFiles=new FileSystemManager(root,NullLogger<FileSystemManager>.Instance);var recoveryTarget=seedFiles.ResolvePath("game_state/main-recovery.bin");
            if(mode is "terminal-main-recovery-generation" or "terminal-main-recovery-same") {
                await using var lease=await seedFiles.AcquireCanonicalWriteLeaseAsync();var generation=seedFiles.GetOrCreateSessionGeneration(lease);
                await seedFiles.WriteFileAtomicAsync(lease,"game_state/main-recovery.bin","before");recoveryBefore=File.ReadAllBytes(recoveryTarget);
                var changes=new List<TrustedLocalFileChange>{new(recoveryTarget,recoveryBefore,recoveryAfter)};
                if(mode=="terminal-main-recovery-generation")changes.Add(new(seedFiles.SessionGenerationPath,File.ReadAllBytes(seedFiles.SessionGenerationPath),JsonSerializer.SerializeToUtf8Bytes(new{schemaVersion=1,generationId=Guid.NewGuid().ToString("N")})));
                var publisher=new TrustedLocalFilePublication(seedFiles,new TrustedLocalFileScope([root]));var fired=false;
                try{publisher.Publish(lease,TrustedLocalGeneration.Existing(generation),changes,(s,_)=>{if(s==TrustedLocalPublicationPhase.IntentPublished){fired=true;throw new IOException("retain valid controlled intent");}});}catch(IOException){}
                Require(fired,"Recovery fixture did not reach its valid retained intent cut.");
                recoveryIntent=File.ReadAllBytes(Path.Combine(root,".boe_runtime/trusted-local-publication-v1/active.json"));publisher.Recover(lease);
            }
            SaveLoadService? save=null;string? archive=null;
            if(mode=="terminal-main-save-load") {
                var state=PortableSaveFixture.Seed(seedFiles);save=new(seedFiles,state,NullLogger<SaveLoadService>.Instance);
                Require(await save.SaveGameAsync("before-main","synthetic F1 fixture"),"Save/load fixture preparation failed.");archive=Directory.GetFiles(seedFiles.ResolvePath("saves/manual_saves"),"*.zip").Single();
            }
            using var output=new ControlledOutput(mode=="terminal-main-output-fault");
            if(mode is "terminal-main-output-drain" or "terminal-main-output-fault")Set("NeutralOutput",output);
            var stopDebt=true;
            if(mode is "terminal-main-stopped-debt-epoch" or "terminal-main-stop-late-authority")
                Set("ObserveMainMetadata",(Action<MainRunIoStage>)(s=>{
                    if(s!=MainRunIoStage.Readback || Read().Disposition!=GmSessionRunDisposition.Stopped)return;
                    if(mode=="terminal-main-stopped-debt-epoch" && stopDebt)throw new IOException("Stopped ACK debt");
                    if(mode=="terminal-main-stop-late-authority") {
                        var session=(IOwnedTerminalSession)Field("_pty")!;
                        var native=session.GetType().GetField("_owner",BindingFlags.Instance|BindingFlags.NonPublic)!.GetValue(session)!;
                        native.GetType().GetMethod("ReportTerminalFault",BindingFlags.Instance|BindingFlags.NonPublic)!.Invoke(native,["late-stop-ack-fault"]);
                    }
                }));
            async Task<JsonElement> Rpc(object message) {
                using var timeout=new CancellationTokenSource(TimeSpan.FromSeconds(2));
                using var pipe=new NamedPipeClientStream(".",(string)Field("_pipeName")!,PipeDirection.InOut,PipeOptions.Asynchronous);
                await pipe.ConnectAsync(timeout.Token);await pipe.WriteAsync(Encoding.UTF8.GetBytes(JsonSerializer.Serialize(message)+"\n"),timeout.Token);await pipe.FlushAsync(timeout.Token);
                using var reader=new StreamReader(pipe,Encoding.UTF8);using var doc=JsonDocument.Parse((await reader.ReadLineAsync(timeout.Token))!);return doc.RootElement.Clone();
            }
            if(mode=="terminal-main-prepared-debt") {
                var created=false;Set("ObserveMainHeldRoot",(Action<int>)(_=>created=true));
                Set("ObserveMainMetadata",(Action<MainRunIoStage>)(s=>{if(s==MainRunIoStage.Readback)throw new IOException("Prepared ACK debt");}));
                var running=(Task<int>)type.GetMethod("RunAsync",BindingFlags.Instance|BindingFlags.Public)!.Invoke(host,null)!;
                try {
                    var status=(await Rpc(new{command="status"})).GetProperty("status");
                    Require(!created && status.GetProperty("terminalOwnerRetained").GetBoolean() && status.GetProperty("terminalUncertain").GetBoolean(),"Prepared debt lost original guard diagnostics or created child.");
                    Require(((GmSessionRunCoordinator)Field("_mainRun")!).HasMetadataDebt,"Prepared debt was accepted.");
                    Require(!(await Rpc(new{command="restart"})).GetProperty("ok").GetBoolean(),"Prepared debt restart succeeded.");
                } finally {await ((CancellationTokenSource)Field("_cts")!).CancelAsync();try{await running;}catch{}}
                result["Success"]=true;return 0;
            }
            if(mode=="terminal-main-held-expiry") {
                Set("ObserveMainMetadata",(Action<MainRunIoStage>)(s=>{if(s==MainRunIoStage.Readback && Read().Disposition==GmSessionRunDisposition.Running)Thread.Sleep(5200);}));
                Exception? failure=null;try{await Call("StartShellAsync");}catch(Exception e){failure=e;}
                Require(failure is OwnedTerminalStartException,"Expired held root was released after late Running ACK.");
                Require(ReferenceEquals(((OwnedTerminalStartException)failure!).Owner,Field("_pty")),"Expired original was replaced.");
                Require((bool)Field("_terminalUncertain")!,"Expired original not uncertain.");result["Success"]=true;return 0;
            }
            if(mode=="terminal-main-launch-generation") {
                var generationPath=Path.Combine(root,".boe_runtime/session-generation/current.json");
                byte[]? generation=null;var preparedObserved=false;
                Set("ObserveMainHeldRoot",(Action<int>)(_=>{preparedObserved=Read().Disposition==GmSessionRunDisposition.Prepared;generation=File.ReadAllBytes(generationPath);File.Delete(generationPath);}));
                Exception? failure=null;try{await Call("StartShellAsync");}catch(Exception e){failure=e;}
                Require(preparedObserved,"Prepared was absent before original process creation.");
                Require(failure is OwnedTerminalStartException,"Missing launch generation released the original terminal.");
                Require(Read().Disposition==GmSessionRunDisposition.Prepared,"Missing launch generation published Running.");
                File.WriteAllBytes(generationPath,generation!);
                result["Success"]=true;return 0;
            }
            if(mode=="terminal-main-release-ack") {
                var fault=new GmWorkerNativeObservationFault(GmWorkerNativeObservationFaultKind.MalformedStarted);
                NativeLineageOwner? native=null;
                Set("ObserveMainMetadata",(Action<MainRunIoStage>)(stage=>{
                    if(stage!=MainRunIoStage.Readback || Read().Disposition!=GmSessionRunDisposition.Running)return;
                    var coordinator=(GmSessionRunCoordinator)Field("_mainRun")!;
                    var session=(IOwnedTerminalSession)typeof(GmSessionRunCoordinator).GetField("_terminal",BindingFlags.Instance|BindingFlags.NonPublic)!.GetValue(coordinator)!;
                    native=(NativeLineageOwner)session.GetType().GetField("_owner",BindingFlags.Instance|BindingFlags.NonPublic)!.GetValue(session)!;
                    native.SetSyntheticObservationFault(fault);
                }));
                OwnedTerminalStartException? partial=null;
                try{await Call("StartShellAsync");}catch(OwnedTerminalStartException e){partial=e;}
                await fault.Reached.WaitAsync(TimeSpan.FromSeconds(2));
                Require(partial!=null && ReferenceEquals(partial.Owner,Field("_pty")),"Started ACK loss replaced/dropped original partial owner.");
                Require(native!=null && (int)typeof(NativeLineageOwner).GetField("_terminalRelease",BindingFlags.Instance|BindingFlags.NonPublic)!.GetValue(native)! == 1,"A1 was not consumed exactly once before ACK loss.");
                var retained=(GmSessionRunCoordinator)Field("_mainRun")!;
                Require(retained.RetainsAuthority && retained.IsUncertain && native!.AuthorityLost.IsCompleted && Read().Disposition!=GmSessionRunDisposition.Stopped,"Started ACK loss granted retirement/ordinary authority.");
                Exception? replay=null;try{await native!.ReleaseTerminalAsync(CancellationToken.None);}catch(Exception e){replay=e;}
                Require(replay is InvalidOperationException,"Unconfirmed A1 release was automatically replayable.");
                using var control=new CancellationTokenSource();
                var server=(Task)type.GetMethod("RunServerLoopAsync",BindingFlags.Instance|BindingFlags.NonPublic)!.Invoke(host,[control.Token])!;
                try {
                    var status=(await Rpc(new{command="status"})).GetProperty("status");
                    Require(status.GetProperty("terminalOwnerRetained").GetBoolean() && status.GetProperty("terminalUncertain").GetBoolean(),"Lost release ACK hid retained original.");
                    Require(!(await Rpc(new{command="addText",text="must-not-write"})).GetProperty("ok").GetBoolean(),"Lost release ACK admitted input.");
                    Exception? stop=null;try{await Call("StopShellAsync");}catch(Exception e){stop=e;}
                    Require(stop!=null && retained.RetainsAuthority && Read().Disposition!=GmSessionRunDisposition.Stopped && ReferenceEquals(partial!.Owner,Field("_pty")),"Lost ACK was accepted as confirmed stop.");
                } finally {await control.CancelAsync();await server;}
                result["A1ConsumedOnce"]=true;result["StartedFrameObservedAndInvalidated"]=true;
                result["OriginalOwnerRetained"]=true;result["InputClosedViaActualPipe"]=true;
                result["NoStoppedOrReplay"]=true;result["Success"]=true;return 0;
            }
            if(mode=="terminal-main-running-debt") {
                Set("ObserveMainMetadata",(Action<MainRunIoStage>)(stage=>{if(stage==MainRunIoStage.Readback && Read().Disposition==GmSessionRunDisposition.Running)throw new IOException("Running ACK debt");}));
                OwnedTerminalStartException? partial=null;
                try{await Call("StartShellAsync");}catch(OwnedTerminalStartException e){partial=e;}
                Require(partial!=null && ReferenceEquals(partial.Owner,Field("_pty")),"Partial Running ACK debt lost its exact original exception/owner.");
                var retained=(GmSessionRunCoordinator)Field("_mainRun")!;Require(retained.HasMetadataDebt,"Running ACK debt was accepted.");
                using var control=new CancellationTokenSource();
                var server=(Task)type.GetMethod("RunServerLoopAsync",BindingFlags.Instance|BindingFlags.NonPublic)!.Invoke(host,[control.Token])!;
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
            if(mode.StartsWith("terminal-main-daemon-storage-",StringComparison.Ordinal)) {
                using var control=new CancellationTokenSource();
                var server=(Task)type.GetMethod("RunServerLoopAsync",BindingFlags.Instance|BindingFlags.NonPublic)!.Invoke(host,[control.Token])!;
                try {await GmDaemonStorageScenario.ExerciseAsync(mode["terminal-main-daemon-storage-".Length..],files,owner,folder,()=>Call("StopShellAsync"),result);}
                finally {await control.CancelAsync();await server;}
            }
            else if(mode.StartsWith("terminal-main-helper-current-",StringComparison.Ordinal)) {
                helperOwner=owner;
                await owner.RunOperationAsync(async()=>{await GmHelperCurrentScenario.SeedAsync(files);return 0;});
                using var control=new CancellationTokenSource();
                var server=(Task)type.GetMethod("RunServerLoopAsync",BindingFlags.Instance|BindingFlags.NonPublic)!.Invoke(host,[control.Token])!;
                try {
                    var currentMode=mode["terminal-main-helper-current-".Length..];
                    if(currentMode=="admission-cancel")await GmHelperCurrentScenario.CancelAdmissionAsync(files,owner,folder,result);
                    else await GmHelperCurrentScenario.ExerciseAsync(currentMode,files,folder,result);
                }finally {await control.CancelAsync();await server;}
                await Call("StopShellAsync");
                Require(owner.Record.Disposition==GmSessionRunDisposition.Stopped&&!owner.RetainsAuthority,"Helper fixture lost original main retirement.");
            }
            else if(mode.StartsWith("terminal-main-worker-dispatch-",StringComparison.Ordinal)) {
                await MainWorkerDispatchScenario.RunAsync(mode["terminal-main-worker-dispatch-".Length..],root,package,host!,type!,owner,()=>Call("StopShellAsync"),result);
            }
            else if(mode.StartsWith("terminal-main-worker-cleanup-",StringComparison.Ordinal)) {
                await MainWorkerCleanupScenario.RunAsync(mode["terminal-main-worker-cleanup-".Length..],root,folder,owner,
                    ()=>Call("StopShellAsync"),result);
            }
            else if(mode is "terminal-main-finalization-purpose" or "terminal-main-finalization-readonly" or "terminal-main-finalization-bound-close") {
                const string member="game_state/main-finalization.bin";
                var target=files.ResolvePath(member);
                byte[] before=[17,29,41],after=[53,67,79];
                var desired=before;var published=0;var closingReads=0;
                FileSystemManager? hooked=null;
                var hooks=new FileSystemManagerHooks {
                    LocalPublicationObserver=(phase,index)=>{
                        if(phase!=TrustedLocalPublicationPhase.MemberPublished)return;
                        using var journal=JsonDocument.Parse(File.ReadAllBytes(Path.Combine(root,".boe_runtime/trusted-local-publication-v1/active.json")));
                        var members=journal.RootElement.GetProperty("Members").EnumerateArray().ToArray();
                        Require(index==0 && members.Length==1 && members[0].GetProperty("Path").GetString()==target,"Finalization fixture selected the wrong real member.");
                        Require(File.ReadAllBytes(target).AsSpan().SequenceEqual(desired),"Actual selected member after-image was not published.");published++;
                    },
                    SessionOperationClosingAsync=async()=>{
                        Require(mode=="terminal-main-finalization-bound-close","Direct-purpose row unexpectedly gained bound closing.");
                        await using var closing=await hooked!.AcquireCanonicalWriteLeaseAsync(CanonicalWritePurpose.SessionFinalization);
                        Require(hooked.IsCurrentSessionGeneration(closing,owner.Identity.GenerationId),"Authorized finalization lost original generation read.");closingReads++;
                    }
                };
                hooked=new FileSystemManager(root,NullLogger<FileSystemManager>.Instance,PhysicalLoadTransactionOperations.Instance,hooks);
                await owner.RunOperationAsync(async()=>{await hooked.WriteFileAtomicBytesAsync(member,before);return 0;});
                Require(published==1,"Ordinary positive control did not reach the selected current publisher.");
                published=0;desired=after;
                if(mode is "terminal-main-finalization-purpose" or "terminal-main-finalization-readonly") {
                    var acquired=0;Exception? failure=null;
                    await owner.RunOperationAsync(async()=>{
                        Require(!SessionOperationContext.TryGetExpectedGeneration(root,out _),"Direct-purpose case unexpectedly has a bound closing context.");
                        FileSystemManager.MainAdmission? closingScope=null;
                        try {
                            if(mode=="terminal-main-finalization-readonly") {
                                closingScope=hooked.BeginMainAdmission();await closingScope.AcquireAsync();hooked.BeginMainOperationClosing();
                            }
                            await using var closing=await hooked.AcquireCanonicalWriteLeaseAsync(CanonicalWritePurpose.SessionFinalization);
                            acquired++;
                            Require(hooked.IsCurrentSessionGeneration(closing,owner.Identity.GenerationId),"Finalization generation read failed.");closingReads++;
                            await hooked.WriteFileAtomicBytesAsync(closing,member,after);
                        }
                        catch(Exception error){failure=error;}
                        finally {if(closingScope!=null)await closingScope.DisposeAsync();}
                        return 0;
                    });
                    result["OrdinaryPositiveControlReached"]=true;result["FinalizationAcquired"]=acquired;
                    result["SelectedMemberPublished"]=published;result["TargetIsAttemptedAfter"]=File.ReadAllBytes(target).AsSpan().SequenceEqual(after);
                    result["ObservedFailure"]=failure?.ToString();result["FinalizationGenerationReads"]=closingReads;
                    var expectedAcquired=mode=="terminal-main-finalization-readonly"?1:0;
                    Require(failure is IOException && acquired==expectedAcquired && closingReads==expectedAcquired && published==0 && File.ReadAllBytes(target).AsSpan().SequenceEqual(before),
                        "Purpose alone granted finalization/recovery/mutation capability; inspect reached member/after-image diagnostics.");
                } else {
                    await owner.RunOperationAsync(async()=>{
                        await SessionOperationContext.RunBoundAsync(hooked,owner.Identity.GenerationId,()=>hooked.WriteFileAtomicBytesAsync(member,after));return 0;
                    });
                    Require(closingReads==1 && published==1 && File.ReadAllBytes(target).AsSpan().SequenceEqual(after),"Actual bound closure or ordinary operation publication failed.");
                    result["BoundClosingGenerationReads"]=closingReads;
                }
                Require(Read().Identity==old.Identity && Read().Disposition==GmSessionRunDisposition.Running && owner.RetainsAuthority,
                    "Finalization check changed the original main run instead of closing its operation.");
            }
            else if(mode=="terminal-main-save-load") {
                var before=File.ReadAllBytes(seedFiles.SessionGenerationPath);var count=Directory.GetFiles(seedFiles.ResolvePath("saves/manual_saves"),"*.zip").Length;
                var unbound=await save!.CreateSaveAsync("unbound","synthetic F1 fixture");
                Require(unbound.Disposition==SaveCreationDisposition.Uncertain,"Unbound save acquired Running original main authority or hid retained refusal.");
                try{unbound.ToBoolean();throw new InvalidOperationException("Boolean save hid typed uncertainty.");}
                catch(CoordinatedStatePublicationUncertainException){}
                Require(Directory.GetFiles(seedFiles.ResolvePath("saves/manual_saves"),"*.zip").Length==count,"Unbound save published before admission.");
                var outside=await save.LoadGameWithOutcomeAsync(archive!);Require(outside.Disposition==LoadReplacementDisposition.NotLoaded,"Unbound Load replaced Running original.");
                await owner.RunOperationAsync(async()=>{
                    var inside=await save.LoadGameWithOutcomeAsync(archive!);Require(inside.Disposition==LoadReplacementDisposition.NotLoaded,"Live original pin admitted replacement Load.");
                    Require(await save.SaveGameAsync("live-main","synthetic F1 fixture"),"Original in-generation snapshot/publication was refused.");return 0;
                });
                Require(before.AsSpan().SequenceEqual(File.ReadAllBytes(seedFiles.SessionGenerationPath)) && Read().Identity==old.Identity,"Live Load/save changed original generation/epoch.");
            }
            else if(mode is "terminal-main-recovery-generation" or "terminal-main-recovery-same") {
                var active=Path.Combine(root,".boe_runtime/trusted-local-publication-v1/active.json");var gen=File.ReadAllBytes(files.SessionGenerationPath);
                void Seed(){File.WriteAllBytes(recoveryTarget,recoveryAfter);File.WriteAllBytes(active,recoveryIntent!);}
                await owner.RunOperationAsync(async()=>{
                    await using var lease=await files.AcquireCanonicalWriteLeaseAsync();Seed();Exception? error=null;
                    try{new TrustedLocalFilePublication(files,new TrustedLocalFileScope([root])).Recover(lease);}catch(Exception e){error=e;}
                    if(mode=="terminal-main-recovery-generation") {
                        Require(error!=null && File.ReadAllBytes(recoveryTarget).AsSpan().SequenceEqual(recoveryAfter) && File.ReadAllBytes(active).AsSpan().SequenceEqual(recoveryIntent),"Held generation-changing recovery restored/cleaned before refusal.");
                    } else Require(error==null && File.ReadAllBytes(recoveryTarget).AsSpan().SequenceEqual(recoveryBefore) && !File.Exists(active),"Held in-generation recovery did not settle exact original rollback.");return 0;
                });
                await owner.RunOperationAsync(async()=>{
                    if(mode=="terminal-main-recovery-same")Seed();Exception? error=null;try{await using var lease=await files.AcquireCanonicalWriteLeaseAsync();}catch(Exception e){error=e;}
                    if(mode=="terminal-main-recovery-generation")Require(error!=null && File.ReadAllBytes(recoveryTarget).AsSpan().SequenceEqual(recoveryAfter) && File.ReadAllBytes(active).AsSpan().SequenceEqual(recoveryIntent),"Pre-recovery admission changed generation-changing evidence.");
                    else Require(error==null && File.ReadAllBytes(recoveryTarget).AsSpan().SequenceEqual(recoveryBefore) && !File.Exists(active),"Pre-recovery original admission did not settle same-generation evidence.");return 0;
                });
                Require(File.ReadAllBytes(files.SessionGenerationPath).AsSpan().SequenceEqual(gen),"Recovery changed original generation.");
            }
            else if(mode=="terminal-main-unbound") {
                var start=new ProcessStartInfo(Path.Combine(Environment.GetEnvironmentVariable("DOTNET_ROOT")!,"dotnet")){UseShellExecute=false};
                foreach(var arg in new[]{typeof(NativeHostScenarioDriver).Assembly.Location,"terminal-main-cold-reader",root,folder})start.ArgumentList.Add(arg);
                using var child=Process.Start(start)!;await child.WaitForExitAsync().WaitAsync(TimeSpan.FromSeconds(4));Require(child.ExitCode==0,"Separate unbound child inherited/minted original authority.");
            }
            else if(mode=="terminal-main-single-release") {
                var native=terminal.GetType().GetField("_owner",BindingFlags.Instance|BindingFlags.NonPublic)!.GetValue(terminal)!;
                Require((int)native.GetType().GetField("_terminalRelease",BindingFlags.Instance|BindingFlags.NonPublic)!.GetValue(native)! == 1,"Original release was not consumed once.");
                Exception? error=null;try{await (Task)native.GetType().GetMethod("ReleaseTerminalAsync",BindingFlags.Instance|BindingFlags.NonPublic)!.Invoke(native,[CancellationToken.None])!;}catch(Exception e){error=e;}
                Require(error is InvalidOperationException && !terminal.AuthorityLost.IsCompleted && Read().Disposition==GmSessionRunDisposition.Running,"Repeated release replayed or harmed the original.");
            }
            else if(mode is "terminal-main-output-drain" or "terminal-main-output-fault") {
                await output.Entered.Task.WaitAsync(TimeSpan.FromSeconds(2));
                if(mode=="terminal-main-output-fault") {
                    output.Release.SetResult();try{await (Task)Field("_outputPumpTask")!;}catch(IOException){}
                    Exception? error=null;try{await Call("StopShellAsync");}catch(Exception e){error=e;}
                    Require(error!=null && (bool)Field("_terminalUncertain")! && Field("_mainRun")!=null && Read().Disposition!=GmSessionRunDisposition.Stopped,"Actual output fault upgraded uncertain main to Stopped.");
                } else {
                    var stop=Call("StopShellAsync");
                    try {
                        await terminal.RootExited.WaitAsync(TimeSpan.FromSeconds(2));
                        Require(!stop.IsCompleted && Field("_mainRun")!=null && ReferenceEquals(terminal,Field("_pty")) && Read().Disposition!=GmSessionRunDisposition.Stopped,"Actual blocked output/disposal did not retain original main.");
                    } finally {output.Release.TrySetResult();await stop;}
                    Require(Read().Disposition==GmSessionRunDisposition.Stopped && Field("_mainRun")==null,"Actual output settlement did not durably retire original.");
                }
            }
            else if(mode is "terminal-main-held-rollback" or "terminal-main-held-commit") {
                Task? stop=null;
                await owner.RunOperationAsync(async()=>{
                    await using var lease=await files.AcquireCanonicalWriteLeaseAsync();var target=files.ResolvePath("game_state/main-held.bin");
                    await files.WriteFileAtomicAsync(lease,"game_state/main-held.bin","before");var before=File.ReadAllBytes(target);var after=Encoding.UTF8.GetBytes("after");
                    var publisher=new TrustedLocalFilePublication(files,new TrustedLocalFileScope([root]));
                    var cut=mode=="terminal-main-held-commit"?TrustedLocalPublicationPhase.Committed:TrustedLocalPublicationPhase.MemberPublished;
                    var fired=false;var outcome=publisher.PublishWithOutcome(lease,TrustedLocalGeneration.Existing(owner.Identity.GenerationId),[new(target,before,after)],(s,_)=>{
                        if(s!=cut || fired)return;fired=true;
                        using(ExecutionContext.SuppressFlow())stop=Task.Run(()=>Call("StopShellAsync"));
                        for(var i=0;i<100 && !owner.AdmissionClosed;i++)Thread.Sleep(5);
                        Require(owner.AdmissionClosed && Read().Disposition==GmSessionRunDisposition.Running,"Held canonical decision lost its earlier linearization.");throw new IOException("owned held decision interruption");
                    });
                    Require(fired && outcome.Disposition==(mode=="terminal-main-held-commit"?TrustedLocalPublicationDisposition.Committed:TrustedLocalPublicationDisposition.RolledBack),"Closed new pins aborted original held storage decision.");
                    Require(File.ReadAllBytes(target).AsSpan().SequenceEqual(mode=="terminal-main-held-commit"?after:before),"Held storage decision changed exact established result.");return 0;
                });
                await stop!;Require(Read().Disposition==GmSessionRunDisposition.Stopped,"Held decision prevented confirmed main stop.");
            }
            else if(mode=="terminal-main-pin-timeout") {
                var entered=new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);var finish=new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
                var operation=owner.RunOperationAsync(async()=>{entered.SetResult();await finish.Task;return 0;});await entered.Task;
                try {
                    Exception? error=null;try{await Call("StopShellAsync");}catch(Exception e){error=e;}
                    Require(error!=null && (bool)Field("_terminalUncertain")! && owner.RetainsAuthority && Read().Disposition!=GmSessionRunDisposition.Stopped,"Pin timeout retired main or hid uncertainty.");
                } finally {finish.SetResult();await operation;}
            }
            else if(mode is "terminal-main-native-dispose-fault" or "terminal-main-bus-dispose-fault") {
                GmWorkerNativeObservationFault? fault=null;
                if(mode=="terminal-main-native-dispose-fault") {
                    var scope=terminal.GetType().GetField("_scope",BindingFlags.Instance|BindingFlags.NonPublic)!.GetValue(terminal)!;
                    var native=(NativeLineageOwner)scope.GetType().GetField("_original",BindingFlags.Instance|BindingFlags.NonPublic)!.GetValue(scope)!;
                    fault=new(GmWorkerNativeObservationFaultKind.DisposeOnce);native.SetSyntheticObservationFault(fault);
                }
                Exception? error=null;try{await Call("StopShellAsync");}catch(Exception e){error=e;}
                Require(error!=null && owner.RetainsAuthority && ReferenceEquals(terminal,Field("_pty")) && Read().Disposition!=GmSessionRunDisposition.Stopped,"Disposal failure retired original durable owner.");
                Require(terminal.AuthorityLost.IsCompleted,"Adapter failed to latch original disposal uncertainty.");
                Require((await terminal.StopAndObserveAsync(CancellationToken.None)).State==GmWorkerStopState.Uncertain,"Adapter promoted failed disposal to successful cached stop.");
                try{await terminal.DisposeAsync();}catch{}if(fault!=null)Require(fault.DisposeAttempts==1,"Cached failed original disposal replayed.");
                result["LogicalUncertainRetained"]=true;
            }
            else if(mode=="terminal-main-stopped-debt-epoch") {
                Exception? error=null;try{await Call("StopShellAsync");}catch(Exception e){error=e;}
                Require(error!=null && owner.HasMetadataDebt && Read().Disposition==GmSessionRunDisposition.Stopped && ReferenceEquals(terminal,Field("_pty")),"Visible Stopped retired original before ACK.");
                Require(terminal.RootExited.IsCompletedSuccessfully,"Stopped ACK debt preceded real root exit.");
                error=null;try{await Call("StartShellAsync");}catch(Exception e){error=e;}
                Require(error!=null && Read().Identity.Epoch==old.Identity.Epoch && ReferenceEquals(terminal,Field("_pty")),"New epoch bypassed pending Stopped ACK.");
                Task<Exception?> blocked;using(ExecutionContext.SuppressFlow())blocked=Task.Run(async()=>{try{await files.ClearGameStateAsync();return null;}catch(Exception e){return e;}});
                Require(await blocked!=null,"Visible Stopped bypassed original guard.");
                stopDebt=false;await Call("StopShellAsync");Require(Field("_mainRun")==null && Field("_pty")==null,"Exact Stopped ACK retry retained successful original.");
                if(systemd!=null) {
                    error=null;try{await Call("StartShellAsync");}catch(Exception e){error=e;}
                    Require(error!=null && Field("_mainRun")==null && Field("_pty")==null && Read().Identity==old.Identity,"Consumed scope capability created a new Prepared/owner.");
                    result["ConsumedFixtureRefusedBeforePrepared"]=true;result["Success"]=true;return 0;
                }
                await Call("StartShellAsync");var next=Read();
                Require(next.Identity.Epoch==old.Identity.Epoch+1 && next.Identity.RunId!=old.Identity.RunId && next.Identity.RootKey==old.Identity.RootKey,"Next confirmed epoch changed root/reused identity.");
                Require(((IOwnedTerminalSession)Field("_pty")!).Identity.RunId==next.Identity.RunId,"Next epoch original identity mismatch.");
            }
            else if(mode=="terminal-main-stop-late-authority") {
                Exception? error=null;try{await Call("StopShellAsync");}catch(Exception e){error=e;}
                Require(error!=null && ReferenceEquals(terminal,Field("_pty")) && Field("_mainRun")!=null,"Late authority loss released main owner at Stopped ACK.");
                result["RetainedUncertain"]=true;
            }
            else if(mode=="terminal-main-staged-rollback") {
                await owner.RunOperationAsync(async()=>{
                    await using var lease=await files.AcquireCanonicalWriteLeaseAsync();
                    var target=files.ResolvePath("game_state/main-stage.txt");await files.WriteFileAtomicAsync(lease,"game_state/main-stage.txt","before");
                    var publisher=new TrustedLocalFilePublication(files,new TrustedLocalFileScope([root]));
                    var change=new TrustedLocalFileChange(target,File.ReadAllBytes(target),Encoding.UTF8.GetBytes("after"));var fired=false;
                    var outcome=publisher.PublishWithOutcome(lease,TrustedLocalGeneration.Existing(owner.Identity.GenerationId),[change],(s,_)=>{if(s==TrustedLocalPublicationPhase.IntentStaged){fired=true;throw new IOException("owned staged interruption");}});
                    Require(fired,"Staged fixture failed preparation before its causal cut.");
                    result["Disposition"]=outcome.Disposition.ToString();result["PublicationFailure"]=outcome.Failure?.ToString();Require(outcome.Disposition==TrustedLocalPublicationDisposition.RolledBack,"Known original staged before-decision did not settle rollback.");
                    Require(File.ReadAllText(target)=="before" && !Directory.EnumerateFileSystemEntries(Path.Combine(root,".boe_runtime/trusted-local-publication-v1")).Any(),"Original staged rollback left intent or changed before bytes.");return 0;
                });
            }
            else if(mode.StartsWith("terminal-main-namespace-",StringComparison.Ordinal)) {
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
                    result["NamespaceAttemptFailure"]=error?.ToString();
                    Require(error!=null,"Active original pin admitted a generation-changing namespace plan.");
                    Require(effects==0 && !Directory.EnumerateFileSystemEntries(Path.Combine(root,".boe_runtime/trusted-local-publication-v1")).Any(),"Namespace generation refusal followed publication effects.");
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
            if(host!=null){try{await Call("StopShellAsync");result["CleanupAttempted"]=true;if(helperOwner!=null){result["OriginalHelperOwnerRetired"]=helperOwner.Record.Disposition==GmSessionRunDisposition.Stopped&&!helperOwner.RetainsAuthority;Require((bool)result["OriginalHelperOwnerRetired"]!,"Original helper main did not retire.");}}catch(Exception e){result["CleanupFailure"]=e.ToString();}try{((IDisposable)host).Dispose();}catch(Exception e){result["DisposeFailure"]=e.ToString();}}
            await File.WriteAllTextAsync(Path.Combine(folder,"scenario.json"),JsonSerializer.Serialize(result));
        }
    }
    private static string FindRepoRoot(){var d=new DirectoryInfo(AppContext.BaseDirectory);while(d!=null){if(File.Exists(Path.Combine(d.FullName,"AGENTS.md")))return d.FullName;d=d.Parent;}throw new InvalidOperationException();}
    private sealed class ControlledOutput(bool fail) : Stream
    {
        internal TaskCompletionSource Entered=new(TaskCreationOptions.RunContinuationsAsynchronously),Release=new(TaskCreationOptions.RunContinuationsAsynchronously);
        public override async ValueTask WriteAsync(ReadOnlyMemory<byte> bytes,CancellationToken token=default){Entered.TrySetResult();await Release.Task.WaitAsync(token);if(fail)throw new IOException("controlled actual output write fault");}
        protected override void Dispose(bool disposing){Release.TrySetResult();base.Dispose(disposing);}
        public override bool CanRead=>false;public override bool CanSeek=>false;public override bool CanWrite=>true;public override long Length=>throw new NotSupportedException();public override long Position{get=>throw new NotSupportedException();set=>throw new NotSupportedException();}
        public override void Flush(){}public override Task FlushAsync(CancellationToken token)=>Task.CompletedTask;public override int Read(byte[] b,int o,int c)=>throw new NotSupportedException();public override void Write(byte[] b,int o,int c)=>throw new NotSupportedException();public override long Seek(long o,SeekOrigin s)=>throw new NotSupportedException();public override void SetLength(long l)=>throw new NotSupportedException();
    }
}
