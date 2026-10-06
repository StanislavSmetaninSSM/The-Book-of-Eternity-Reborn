using System.Net.Http.Json;
using System.IO.Compression;
using System.Reflection;
using System.Text.Json;
using BookOfEternityClient.Configuration;
using BookOfEternityClient.Core;
using BookOfEternityClient.Services.GmRuntime;
using BookOfEternityClient.WebUi;
using Microsoft.AspNetCore.Builder;
using Microsoft.Extensions.DependencyInjection;

namespace BookOfEternityClient.Tests;
internal static partial class OwnedTerminalScenarioDriver
{
    private static async Task RunBrowserHttpLoadAsync(string mode,string folder,object host,Type type,
        Func<object,Task<JsonElement>> rpc,IOwnedTerminalSession original,Dictionary<string,object?> evidence,FileSystemManager files,GameSettings profile,string path)
    {
        var assets=Path.Combine(folder,"frontend");Directory.CreateDirectory(assets);
        await File.WriteAllTextAsync(Path.Combine(assets,"index.html"),"<!doctype html><title>isolated Load fixture</title>");
        var faultName=mode.Contains("-fault-",StringComparison.Ordinal)?mode[(mode.LastIndexOf("-fault-",StringComparison.Ordinal)+7)..]:null;
        var storageFault=faultName is "rollback" or "uncertain";
        var publicationArmed=false;var replacing=false;var cuts=0;
        var lockOpens=0;var lockContentions=0;var closes=0;string? contendedAt=null;
        var marker=files.ResolvePath("game_state/world/test_fixture_state.json");
        if(storageFault)await files.WriteFileAtomicAsync("game_state/world/test_fixture_state.json","{\"state\":\"before-load\"}");
        var hooks=storageFault || faultName=="load-reply-loss"?new FileSystemManagerHooks{
            BeforeCanonicalWriteLockOpenAsync=()=>{if(publicationArmed)Interlocked.Increment(ref lockOpens);return Task.CompletedTask;},
            CanonicalWriteLockContendedAsync=()=>{if(publicationArmed){Interlocked.Increment(ref lockContentions);contendedAt=new System.Diagnostics.StackTrace().ToString();}return Task.CompletedTask;},
            SessionOperationClosingAsync=()=>{if(publicationArmed)Interlocked.Increment(ref closes);return Task.CompletedTask;},
            LocalPublicationObserver=(phase,_)=>{
            if(!publicationArmed || !storageFault)return;
            if(phase==TrustedLocalPublicationPhase.IntentPublished) {
                using var journal=File.OpenRead(Path.Combine(files.RuntimeRootPath,"trusted-local-publication-v1/active.json"));
                Span<byte> magic=stackalloc byte[8];journal.ReadExactly(magic);replacing=magic.SequenceEqual("BOELP3\r\n"u8);
            }
            if(replacing && phase==TrustedLocalPublicationPhase.CommitStaged) {
                cuts++;if(faultName=="uncertain")File.WriteAllText(marker,"{\"state\":\"unknown-cut\"}");
                throw new IOException("controlled actual Load commit cut");
            }
        }}:null;
        await using var app=LocalWebUiHost.Build([],new(files.BasePath,"http://127.0.0.1:0",assets),hooks);
        var transportAborted=new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        if(faultName=="load-reply-loss")app.Use(async(context,next)=>{
            if(context.Request.Path!="/api/saves/load"){await next(context);return;}
            using var observed=context.RequestAborted.Register(()=>transportAborted.TrySetResult());
            await next(context);
        });
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
        var profileCase=mode.EndsWith("profile-from-archive",StringComparison.Ordinal);
        if(profileCase) {
            var output=(await rpc(new{command="diagnostics"})).GetProperty("diagnostics").GetProperty("recentOutputTail").GetString()!;
            Require(output.Contains("CONFIGURED_ARG2:active-model-sentinel") && output.Contains("CONFIGURED_CWD:"+profile.GmBridgeShellWorkingDirectory),
                "Preparation failure: original child did not report active configured argv/cwd.");
            evidence["OriginalChildProfileOutput"]=output;
        }
        if(mode.Contains("-fault-",StringComparison.Ordinal)) {
            publicationArmed=true;
            try {await RunLoadHttpFaultAsync(faultName!,app,http,host,type,rpc,original,prior,request,evidence,files,transportAborted.Task);}
            finally {if(faultName=="load-reply-loss")evidence["BundlePhase"]=new{lockOpens,lockContentions,closes,contendedAt};}
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
        using var archive=ZipFile.OpenRead(path);using var installedConfig=archive.GetEntry("config.json")!.Open();
        var expectedProfile=JsonSerializer.Deserialize<GameSettings>(installedConfig)!;
        Require(after.GmCliLaunchCommand==expectedProfile.GmCliLaunchCommand && after.GmBridgeShellWorkingDirectory==expectedProfile.GmBridgeShellWorkingDirectory,
            "Installed configured command/model/args/cwd changed during fresh launch.");
        Require((await rpc(new{command="status"})).GetProperty("status").GetProperty("cliLaunchCommand").GetString()==expectedProfile.GmCliLaunchCommand,"Fresh production terminal used a profile from another generation.");
        if(mode.EndsWith("profile-from-archive",StringComparison.Ordinal))Require(profile.GmCliLaunchCommand!=expectedProfile.GmCliLaunchCommand && profile.GmBridgeShellWorkingDirectory!=expectedProfile.GmBridgeShellWorkingDirectory,
            "Preparation failure: original and archived neutral profile sentinels do not differ.");
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
        if(profileCase) {
            var output=(await rpc(new{command="diagnostics"})).GetProperty("diagnostics").GetProperty("recentOutputTail").GetString()!;
            Require(output.Contains("CONFIGURED_ARG2:gm-model-sentinel") && output.Contains("CONFIGURED_ARG4:a b") &&
                output.Contains("CONFIGURED_CWD:"+expectedProfile.GmBridgeShellWorkingDirectory),"Fresh child did not consume installed configured argv/cwd.");
            evidence["FreshChildProfileOutput"]=output;
        }
        var staleCancel=await Post("/api/saves/load-cancel",new BrowserLoadCompletionRequest(request.OperationId!,loaded.EstablishedGeneration,false));
        Require(staleCancel.ContinuationBlocked && ReferenceEquals(terminal,type.GetField("_pty",BindingFlags.Instance|BindingFlags.NonPublic)!.GetValue(host)),"Stale cancel stopped a later epoch.");
        evidence["FreshEpoch"]=next.Identity;evidence["TwoFreshInputsOneProcess"]=true;evidence["ProfilePreserved"]=true;
        await app.StopAsync();
    }
}
