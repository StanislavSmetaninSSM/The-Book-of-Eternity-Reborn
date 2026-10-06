using System.Net.Http.Json;
using System.Reflection;
using System.Text.Json;
using BookOfEternityClient.Configuration;
using BookOfEternityClient.Core;
using BookOfEternityClient.Services.GmRuntime;
using BookOfEternityClient.WebUi;
using Microsoft.Extensions.DependencyInjection;

namespace BookOfEternityClient.Tests;
internal static partial class OwnedTerminalScenarioDriver
{
    private static async Task RunBrowserHttpLoadAsync(string mode,string folder,object host,Type type,
        Func<object,Task<JsonElement>> rpc,IOwnedTerminalSession original,Dictionary<string,object?> evidence,FileSystemManager files,GameSettings profile,string path)
    {
        var assets=Path.Combine(folder,"frontend");Directory.CreateDirectory(assets);
        await File.WriteAllTextAsync(Path.Combine(assets,"index.html"),"<!doctype html><title>isolated Load fixture</title>");
        var storageFault=mode.EndsWith("rollback",StringComparison.Ordinal)||mode.EndsWith("uncertain",StringComparison.Ordinal);
        var loading=false;var replacing=false;var cuts=0;
        var marker=files.ResolvePath("game_state/world/test_fixture_state.json");
        if(storageFault)await files.WriteFileAtomicAsync("game_state/world/test_fixture_state.json","{\"state\":\"before-load\"}");
        var hooks=storageFault?new FileSystemManagerHooks{LocalPublicationObserver=(phase,_)=>{
            if(!loading)return;
            if(phase==TrustedLocalPublicationPhase.IntentPublished) {
                using var journal=File.OpenRead(Path.Combine(files.RuntimeRootPath,"trusted-local-publication-v1/active.json"));
                Span<byte> magic=stackalloc byte[8];journal.ReadExactly(magic);replacing=magic.SequenceEqual("BOELP3\r\n"u8);
            }
            if(replacing && phase==TrustedLocalPublicationPhase.CommitStaged) {
                cuts++;if(mode.EndsWith("uncertain",StringComparison.Ordinal))File.WriteAllText(marker,"{\"state\":\"unknown-cut\"}");
                throw new IOException("controlled actual Load commit cut");
            }
        }}:null;
        await using var app=LocalWebUiHost.Build([],new(files.BasePath,"http://127.0.0.1:0",assets),hooks);
        await app.StartAsync();
        using var http=new HttpClient{BaseAddress=new Uri(app.Urls.Single()),Timeout=TimeSpan.FromSeconds(20)};
        void Require(bool yes,string failure){if(!yes)throw new InvalidOperationException(failure);}
        async Task<BrowserLoadSaveResultDto> Post(string route,object body) {
            using var response=await http.PostAsJsonAsync(route,body);
            return (await response.Content.ReadFromJsonAsync<BrowserLoadSaveResultDto>())!;
        }
        var menu=await http.GetFromJsonAsync<BrowserMainMenuDto>("/api/main-menu");
        var run=(GmSessionRunCoordinator)type.GetField("_mainRun",BindingFlags.Instance|BindingFlags.NonPublic)!.GetValue(host)!;
        var prior=run.Identity;
        var oldBinding=(await rpc(new{command="status"})).GetProperty("status").GetProperty("inputBindingId").GetString();
        var request=new BrowserLoadSaveRequest("manual:"+Path.GetFileName(path),Guid.NewGuid().ToString("N"),menu!.LoadGeneration);
        if(mode.Contains("-fault-",StringComparison.Ordinal)) {
            loading=true;
            await RunLoadHttpFaultAsync(mode[(mode.LastIndexOf("-fault-",StringComparison.Ordinal)+7)..],app,http,host,type,rpc,original,prior,request,evidence,files);
            if(storageFault)Require(cuts==1,"Preparation failure: real replacement publication cut not reached.");
            await app.StopAsync();return;
        }
        if(mode.EndsWith("early-ack",StringComparison.Ordinal)) {
            var entered=new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
            var release=new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
            app.Services.GetRequiredService<LocalWebUiMainMenuService>().BeforeCommittedMenuRefresh=async()=>{entered.TrySetResult();await release.Task;};
            var loading=Post("/api/saves/load",request);
            await entered.Task.WaitAsync(TimeSpan.FromSeconds(5));
            var early=Post("/api/saves/load-complete",new BrowserLoadCompletionRequest(request.OperationId!,null,true));
            await Task.Delay(100);release.TrySetResult();
            var ready=await loading;evidence["PreparedHttpLoad"]=ready;
            var rejected=await early.WaitAsync(TimeSpan.FromSeconds(5));evidence["PrematureAck"]=rejected;
            await Post("/api/saves/load-cancel",new BrowserLoadCompletionRequest(request.OperationId!,ready.EstablishedGeneration,false));
            Require(rejected.ContinuationBlocked && rejected.MainSessionState!=GmLoadMainState.Running && type.GetField("_pty",BindingFlags.Instance|BindingFlags.NonPublic)!.GetValue(host)==null,
                "Causal RED: early null-generation ACK authorized fresh launch before original bundle application.");
            await app.StopAsync();return;
        }
        var loaded=await Post("/api/saves/load",request);evidence["InitialHttpLoad"]=loaded;
        Require(loaded.Disposition==BookOfEternityClient.Services.LoadReplacementDisposition.Committed && !loaded.ContinuationBlocked && loaded.FreshLaunchRequired,"Actual HTTP Load did not retain refreshed Committed awaiting application: "+JsonSerializer.Serialize(loaded));
        Require(loaded.State!=null && loaded.State.EstablishedGeneration==loaded.EstablishedGeneration && loaded.State.Menu!=null && loaded.State.Settings!=null && loaded.State.Audio!=null,"Required full bundle is missing.");
        Require(original.RootExited.IsCompleted && type.GetField("_pty",BindingFlags.Instance|BindingFlags.NonPublic)!.GetValue(host)==null,"New terminal exists before UI refresh acknowledgement.");
        foreach(var command in new[]{"restartcli","restartshell"})
            Require(!(await rpc(new{command})).GetProperty("ok").GetBoolean(),"Manual restart bypassed retained original Load reservation.");
        var repeated=await Post("/api/saves/load",request);
        Require(repeated.ContinuationBlocked && repeated.Disposition!=BookOfEternityClient.Services.LoadReplacementDisposition.Committed,"Duplicate request re-executed Load.");
        var completed=await Post("/api/saves/load-complete",new BrowserLoadCompletionRequest(request.OperationId!,loaded.EstablishedGeneration,true));
        evidence["CompletedHttpLoad"]=completed;
        Require(completed.Disposition==loaded.Disposition && completed.EstablishedGeneration==loaded.EstablishedGeneration &&
            completed.MainSessionState==GmLoadMainState.Running && !completed.ContinuationBlocked && !completed.FreshLaunchRequired,"Fresh launch after actual refresh is unconfirmed: "+JsonSerializer.Serialize(completed));
        var next=(GmSessionRunCoordinator)type.GetField("_mainRun",BindingFlags.Instance|BindingFlags.NonPublic)!.GetValue(host)!;
        var terminal=(IOwnedTerminalSession)type.GetField("_pty",BindingFlags.Instance|BindingFlags.NonPublic)!.GetValue(host)!;
        Require(!ReferenceEquals(original,terminal) && next.Identity.RunId!=prior.RunId && next.Identity.Epoch==prior.Epoch+1 &&
            next.Identity.GenerationId==loaded.EstablishedGeneration,"Fresh original epoch/root does not match installed generation.");
        var after=JsonSerializer.Deserialize<GameSettings>(File.ReadAllBytes(files.ResolvePath("config.json")))!;
        Require(after.GmCliLaunchCommand==profile.GmCliLaunchCommand && after.GmBridgeShellWorkingDirectory==profile.GmBridgeShellWorkingDirectory,"Configured arbitrary command/model/args/cwd changed.");
        for(var i=0;i<150;i++) {
            if((await rpc(new{command="setReady",ready=true})).GetProperty("ok").GetBoolean())break;
            await Task.Delay(10);
        }
        var current=(await rpc(new{command="status"})).GetProperty("status");
        var binding=current.GetProperty("inputBindingId").GetString();
        Require(binding!=oldBinding,"Fresh launch reused the old input binding.");
        var stale=await rpc(new{command="dispatchPrompt",operationId="old-replay",operationKind="turn",operationRevision="neutral-1",inputBindingId=oldBinding,text="old-command",appendEnter=true});
        Require(stale.GetProperty("promptDelivery").GetProperty("disposition").GetString()!="submission-observed","Old input replayed into fresh CLI.");
        foreach(var text in new[]{"fresh one Ж😀","fresh two"}) {
            var reply=await rpc(new{command="dispatchPrompt",operationId=Guid.NewGuid().ToString("N"),operationKind="turn",operationRevision="neutral-1",inputBindingId=binding,text,appendEnter=true});
            Require(reply.GetProperty("promptDelivery").GetProperty("disposition").GetString()=="submission-observed","Fresh persistent neutral CLI input not observed.");
            for(var i=0;i<150;i++){if((await rpc(new{command="status"})).GetProperty("status").GetProperty("ready").GetBoolean())break;await Task.Delay(10);}
        }
        Require(ReferenceEquals(terminal,type.GetField("_pty",BindingFlags.Instance|BindingFlags.NonPublic)!.GetValue(host)),"Fresh inputs used different processes.");
        var staleCancel=await Post("/api/saves/load-cancel",new BrowserLoadCompletionRequest(request.OperationId!,loaded.EstablishedGeneration,false));
        Require(staleCancel.ContinuationBlocked && ReferenceEquals(terminal,type.GetField("_pty",BindingFlags.Instance|BindingFlags.NonPublic)!.GetValue(host)),"Stale cancel stopped a later epoch.");
        evidence["FreshEpoch"]=next.Identity;evidence["TwoFreshInputsOneProcess"]=true;evidence["ProfilePreserved"]=true;
        await app.StopAsync();
    }
}
