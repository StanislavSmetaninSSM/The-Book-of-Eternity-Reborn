using System.Net.Http.Json;
using System.Reflection;
using System.Text.Json;
using BookOfEternityClient.Core;
using BookOfEternityClient.Services;
using BookOfEternityClient.Services.GmRuntime;
using BookOfEternityClient.Services.GmWorkers;
using BookOfEternityClient.WebUi;
using Microsoft.AspNetCore.Builder;
using Microsoft.Extensions.DependencyInjection;

namespace BookOfEternityClient.Tests;
internal static partial class OwnedTerminalScenarioDriver
{
    private static async Task RunLoadHttpFaultAsync(string fault,WebApplication app,HttpClient http,object host,Type type,
        Func<object,Task<JsonElement>> rpc,IOwnedTerminalSession original,GmSessionRunIdentity prior,BrowserLoadSaveRequest request,
        Dictionary<string,object?> evidence,FileSystemManager files)
    {
        object? Field(string name)=>type.GetField(name,BindingFlags.Instance|BindingFlags.NonPublic)!.GetValue(host);
        void Set(string name,object hook)=>type.GetField(name,BindingFlags.Instance|BindingFlags.NonPublic)!.SetValue(host,hook);
        void Require(bool yes,string error){if(!yes)throw new InvalidOperationException(error);}
        async Task<BrowserLoadSaveResultDto> Post(string route,object body) {
            using var response=await http.PostAsJsonAsync(route,body);return(await response.Content.ReadFromJsonAsync<BrowserLoadSaveResultDto>())!;
        }
        var generationBefore=File.ReadAllBytes(files.SessionGenerationPath);
        var archive=Directory.GetFiles(files.ResolvePath("saves/manual_saves"),"*.zip").Single();var sourceBefore=File.ReadAllBytes(archive);
        var entered=new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        var release=new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        var workerPrepared=false;
        if(fault=="stop-reply-loss")Set("BeforeLoadReceipt",(Action<Stream,GmLoadMainState>)((stream,state)=>{if(state==GmLoadMainState.Stopped)stream.Dispose();}));
        if(fault=="restart-reply-loss")Set("BeforeLoadReceipt",(Action<Stream,GmLoadMainState>)((stream,state)=>{if(state==GmLoadMainState.Running)stream.Dispose();}));
        if(fault=="stop-uncertain") {
            var owner=typeof(LinuxOwnedTerminalSession).GetField("_owner",BindingFlags.Instance|BindingFlags.NonPublic)!.GetValue(original)!;
            await(Task)owner.GetType().GetMethod("SendControlAsync",BindingFlags.Instance|BindingFlags.NonPublic)!.Invoke(owner,['U'])!;
        }
        if(fault=="no-active")await(Task)type.GetMethod("StopShellAsync",BindingFlags.Instance|BindingFlags.NonPublic)!.Invoke(host,null)!;
        if(fault=="worker-debt")Set("BeforeLoadStopReply",(Func<Task>)(async()=>{
            await using var ledger=await GmWorkerRunLedger.OpenCoordinatorAsync(new(files.BasePath));Require(ledger!=null,"Original inventory not released after confirmed stop.");
            var initialized=await ledger!.InitializeAsync();
            Require(initialized is WorkerLedgerMutationKind.Applied or WorkerLedgerMutationKind.AlreadyExact,"Worker fixture preparation failed.");
            var prepared=await ledger.PrepareAsync(new(prior.GenerationId,"inert_worker","load-admission",new string('a',64),WorkerRunBackend.LinuxNativeLineage,WorkerRunScope.OrdinarySamePidNamespace,files.GameSessionPath),ledger.Sequence);
            Require(prepared.Kind==WorkerLedgerMutationKind.Applied,"Actual retained worker preparation was not reached.");
            workerPrepared=true;evidence["WorkerPreparedMutation"]=prepared.Kind;
        }));
        if(fault=="generation-race")Set("BeforeLoadRestart",(Func<Task>)(async()=>{
            // Real short mutation lease can be acquired here only after client Load guards unwind.
            using var admission=files.BeginMainAdmission();await admission.AcquireAsync(quiescentOnly:true);
            await using var lease=await files.AcquireCanonicalWriteLeaseAsync();
            File.WriteAllBytes(files.SessionGenerationPath,JsonSerializer.SerializeToUtf8Bytes(new{schemaVersion=1,generationId=Guid.NewGuid().ToString("N")}));
            evidence["GenerationChangedBeforePrepared"]=true;
        }));
        if(fault=="cancel-after-decision")Set("BeforeLoadRestartReply",(Func<Task>)(async()=>{entered.TrySetResult();await release.Task;}));
        BrowserLoadSaveResultDto loaded;
        if(fault=="load-reply-loss") {
            var service=app.Services.GetRequiredService<LocalWebUiMainMenuService>();
            service.BeforeCommittedMenuRefresh=async()=>{entered.TrySetResult();await release.Task;};
            using var abort=new CancellationTokenSource();var sending=http.PostAsJsonAsync("/api/saves/load",request,abort.Token);
            await entered.Task.WaitAsync(TimeSpan.FromSeconds(5));
            var operation=typeof(LocalWebUiMainMenuService).GetField("_browserLoad",BindingFlags.Instance|BindingFlags.NonPublic)!.GetValue(service)!;
            var originalExecution=(Task<BrowserLoadSaveResultDto>)operation.GetType().GetField("Execution",BindingFlags.Instance|BindingFlags.NonPublic)!.GetValue(operation)!;
            abort.Cancel();try{using var ignored=await sending;throw new InvalidOperationException("Lost Load reply fixture did not cancel transport.");}catch(OperationCanceledException){}
            await Task.Delay(100);release.TrySetResult();loaded=await originalExecution.WaitAsync(TimeSpan.FromSeconds(5));
        } else loaded=await Post("/api/saves/load",request);
        evidence["InitialFaultLoad"]=loaded;
        if(fault=="worker-debt") {
            Require(workerPrepared,"Preparation failure: actual worker Prepared debt was not reached.");
            var inventory=await GmWorkerRunLedger.ObserveAsync(new(files.BasePath));
            Require(inventory.Kind==WorkerRunObservationKind.Uncertain && inventory.Entries.Count==1 && inventory.Entries[0].Phase==WorkerRunPhase.Prepared,
                "Retained worker inventory lost its exact unresolved Prepared entry.");
            evidence["ActualPreparedWorkerInventoryRetained"]=true;
        }
        if(fault is "stop-reply-loss" or "stop-uncertain" or "worker-debt") {
            Require(loaded.Disposition==LoadReplacementDisposition.NotLoaded && loaded.ContinuationBlocked && File.ReadAllBytes(files.SessionGenerationPath).SequenceEqual(generationBefore),"Failed stop/independent admission mutated or accepted Load.");
            if(fault=="stop-uncertain") {
                var owner=(GmSessionRunCoordinator)Field("_mainRun")!;
                Require(ReferenceEquals(original,Field("_pty")) && owner.IsUncertain && owner.RetainsAuthority,"Logical Uncertain/original owner was lost.");
                evidence["RetainedLogicalUncertain"]=owner.Record;
            } else Require(Field("_pty")==null,"Fresh terminal appeared after rejected Load.");
            Require(!(await rpc(new{command="restartcli"})).GetProperty("ok").GetBoolean(),"Unresolved Load/worker inventory allowed a competing restart.");
        } else if(fault is "rollback" or "uncertain") {
            Require(loaded.Disposition==(fault=="rollback"?LoadReplacementDisposition.RolledBack:LoadReplacementDisposition.Uncertain) && loaded.ContinuationBlocked && !loaded.FreshLaunchRequired && Field("_pty")==null,"Actual storage cut erased typed decision or started fresh.");
            if(fault=="rollback")Require(File.ReadAllBytes(files.SessionGenerationPath).SequenceEqual(generationBefore) && File.ReadAllText(files.ResolvePath("game_state/world/test_fixture_state.json")).Contains("before-load"),"Real rollback did not restore exact original generation/world.");
        } else if(fault is "no-active" or "load-reply-loss") {
            Require(loaded.Disposition==LoadReplacementDisposition.Committed && !loaded.FreshLaunchRequired && Field("_pty")==null,"No-active/lost-response Load created an unacknowledged new terminal.");
            Require(fault=="no-active"?!loaded.ContinuationBlocked && loaded.State!=null && loaded.MainSessionState==GmLoadMainState.NoActiveSession:loaded.ContinuationBlocked,"Required no-active refresh or lost Load block is missing.");
        } else {
            Require(loaded.Disposition==LoadReplacementDisposition.Committed && loaded.FreshLaunchRequired && original.RootExited.IsCompleted,"Fault scenario did not reach original stop + Committed + full bundle.");
            if(fault=="manual-cancel")Require((await rpc(new{command="cancelLoadSession",operationId=request.OperationId})).GetProperty("ok").GetBoolean(),"Actual manual cancellation missed the original operation.");
            var finishing=Post("/api/saves/load-complete",new BrowserLoadCompletionRequest(request.OperationId!,loaded.EstablishedGeneration,fault!="refresh-refused"));
            Task<BrowserLoadSaveResultDto>? cancel=null;
            if(fault=="cancel-after-decision") {
                await entered.Task.WaitAsync(TimeSpan.FromSeconds(5));var terminal=Field("_pty");Require(terminal!=null,"Actual fresh decision was not reached.");
                cancel=Post("/api/saves/load-cancel",new BrowserLoadCompletionRequest(request.OperationId!,loaded.EstablishedGeneration,false));
                await Task.Delay(50);Require(ReferenceEquals(terminal,Field("_pty")),"Late cancel revoked a later original epoch.");release.TrySetResult();
            }
            var completed=await finishing;evidence["CompletedFaultLoad"]=completed;
            Require(completed.Disposition==LoadReplacementDisposition.Committed && completed.EstablishedGeneration==loaded.EstablishedGeneration && completed.SelectedSourcePath==loaded.SelectedSourcePath && !completed.FreshLaunchRequired,"Follow-up changed confirmed storage identity.");
            if(fault=="cancel-after-decision") {
                Require(completed.MainSessionState==GmLoadMainState.Running && !completed.ContinuationBlocked && (await cancel!).MainSessionState==GmLoadMainState.Running,"Post-decision cancel rewrote settled fresh result.");
            } else {
                Require(completed.ContinuationBlocked,"Unconfirmed fresh path reopened continuation.");
                if(fault=="restart-reply-loss") {
                    var next=(GmSessionRunCoordinator)Field("_mainRun")!;var terminal=Field("_pty");
                    Require(completed.MainSessionState==GmLoadMainState.Uncertain && terminal!=null && next.Identity.RunId!=prior.RunId && next.Identity.Epoch==prior.Epoch+1,"Lost receipt did not retain actual single decision and client uncertainty.");
                    var repeated=await Post("/api/saves/load",request);Require(repeated.ContinuationBlocked && ReferenceEquals(terminal,Field("_pty")),"Lost response was replayed or minted another epoch.");
                } else Require(Field("_pty")==null,"Fresh terminal appeared after cancel/refusal/generation race.");
                if(fault=="generation-race")Require(GmSessionRunRecordCodec.Decode(GmSessionRunPersistence.Read(files.BasePath)!).Disposition==GmSessionRunDisposition.Stopped,"Generation race published Prepared before refusing.");
            }
        }
        Require(File.ReadAllBytes(archive).SequenceEqual(sourceBefore),"Source archive changed during Load lifecycle.");
        evidence["FaultScenario"]=fault;evidence["NoReplaySourceArchiveUnchanged"]=true;
    }
}
