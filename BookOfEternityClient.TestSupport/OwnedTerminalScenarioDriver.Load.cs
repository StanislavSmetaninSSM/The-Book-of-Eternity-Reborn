using System.Reflection;
using System.Text.Json;
using BookOfEternityClient.Core;
using BookOfEternityClient.Configuration;
using BookOfEternityClient.Services;
using BookOfEternityClient.Services.GmRuntime;
using BookOfEternityClient.WebUi;
using Microsoft.Extensions.Logging.Abstractions;

namespace BookOfEternityClient.Tests;
internal static partial class OwnedTerminalScenarioDriver
{
    private static async Task RunProductionLoadAsync(string mode,string folder,object host,Type hostType,
        Func<object,Task<JsonElement>> rpc,IOwnedTerminalSession original,Dictionary<string,object?> evidence)
    {
        var files=new FileSystemManager(Path.Combine(folder,"root"),NullLogger<FileSystemManager>.Instance);
        var settings=JsonSerializer.Deserialize<GameSettings>(File.ReadAllBytes(files.ResolvePath("config.json")))!;
        var state=new StateManager(files,settings,NullLogger<StateManager>.Instance);
        var save=new SaveLoadService(files,state,NullLogger<SaveLoadService>.Instance);
        var writes=new BrowserLocalWriteCoordinator(files,new LocalUiSessionLockService(files));
        var status=new LocalWebUiSessionStatusService(files,writes);
        var dashboard=new BrowserLifecycleDashboardService(files,status,new ValidationService(files,NullLogger<ValidationService>.Instance));
        var menu=new LocalWebUiMainMenuService(files,dashboard,save,state,writes);
        var path=Directory.GetFiles(files.ResolvePath("saves/manual_saves"),"*.zip").Single();
        if(mode.Contains("http",StringComparison.Ordinal)) {
            await RunBrowserHttpLoadAsync(mode,folder,host,hostType,rpc,original,evidence,files,settings,path);return;
        }
        if(mode=="production-main-load-console") {
            var engine=ProductionLoadGameEngine.Create(files,settings,save);
            var load=(Task<LoadReplacementResult>)typeof(GameEngine).GetMethod("LoadSelectedSaveWithMainLifecycleAsync",BindingFlags.Instance|BindingFlags.NonPublic)!.Invoke(engine,[path])!;
            var console=await load; evidence["ConsoleLoadResult"]=new{console.Disposition,console.EstablishedGeneration,console.SelectedSourcePath,console.NeedsFollowUp,console.ContinuationBlocked,Failure=console.Failure?.ToString()};
            if(console.Disposition!=LoadReplacementDisposition.Committed)throw new InvalidOperationException("Causal RED: actual console selected Load refused the original Running GM: "+console.Disposition);
            if(!original.RootExited.IsCompleted)throw new InvalidOperationException("Console replacement preceded original stop.");
            var current=(GmSessionRunCoordinator?)hostType.GetField("_mainRun",BindingFlags.Instance|BindingFlags.NonPublic)!.GetValue(host);
            if(console.ContinuationBlocked || current?.Record?.Disposition!=GmSessionRunDisposition.Running || current.Identity.GenerationId!=console.EstablishedGeneration)
                throw new InvalidOperationException("Console mandatory refresh/fresh launch is unconfirmed: "+console.Failure?.Message);
            if(ReferenceEquals(original,hostType.GetField("_pty",BindingFlags.Instance|BindingFlags.NonPublic)!.GetValue(host)))
                throw new InvalidOperationException("Console reused the old terminal after Load.");
            evidence["ConsoleFreshIdentity"]=current.Identity;
            return;
        }
        var loaded=await menu.LoadSaveAsync(new("manual:"+Path.GetFileName(path)));
        evidence["LoadResult"]=loaded;
        if(loaded.Disposition!=LoadReplacementDisposition.Committed)
            throw new InvalidOperationException("Causal RED: actual browser Load refused the original Running GM instead of confirming scoped stop before replacement: "+loaded.Disposition);
        if(!original.RootExited.IsCompleted)throw new InvalidOperationException("Replacement occurred before original root retirement.");
    }
}
