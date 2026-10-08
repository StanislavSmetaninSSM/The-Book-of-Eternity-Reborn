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
        string? prepared=null;var debtInjected=0;
        var save=new SaveLoadService(files,state,NullLogger<SaveLoadService>.Instance,mode.EndsWith("debt",StringComparison.Ordinal)?new SaveLoadServiceHooks{
            AfterLoadArchiveExtractedAsync=staging=>{prepared=staging;return Task.CompletedTask;},
            AfterLoadPublicationValidatedAsync=()=>{
                // Own private staging only, after commitment. Real candidate disposal reports cleanup debt.
                File.CreateSymbolicLink(Path.Combine(prepared!,"cleanup-debt"),Path.Combine(folder,"absent-own-target"));debtInjected++;
                return Task.CompletedTask;
            }}:null);
        var writes=new BrowserLocalWriteCoordinator(files,new LocalUiSessionLockService(files));
        var status=new LocalWebUiSessionStatusService(files,writes);
        var dashboard=new BrowserLifecycleDashboardService(files,status,new ValidationService(files,NullLogger<ValidationService>.Instance));
        var menu=new LocalWebUiMainMenuService(files,dashboard,save,state,writes);
        var path=Directory.GetFiles(files.ResolvePath("saves/manual_saves"),"*.zip").Single();
        if(mode.Contains("http",StringComparison.Ordinal)) {
            await RunBrowserHttpLoadAsync(mode,folder,host,hostType,rpc,original,evidence,files,settings,path);return;
        }
        if(mode.StartsWith("production-main-load-console",StringComparison.Ordinal)) {
            var receiptBoundary=mode.StartsWith("production-main-load-console-receipt-",StringComparison.Ordinal)
                ? mode["production-main-load-console-receipt-".Length..] : null;
            var receiptCuts=0;
            if(receiptBoundary!=null) {
                hostType.GetField("BeforeLoadReceipt",BindingFlags.Instance|BindingFlags.NonPublic)!.SetValue(host,
                    (Action<Stream,GmLoadMainState>)((stream,phase)=>{
                        if(phase!=GmLoadMainState.Running)return;
                        var fresh=(GmSessionRunCoordinator)hostType.GetField("_mainRun",BindingFlags.Instance|BindingFlags.NonPublic)!.GetValue(host)!;
                        var terminal=(IOwnedTerminalSession)hostType.GetField("_pty",BindingFlags.Instance|BindingFlags.NonPublic)!.GetValue(host)!;
                        var operation=hostType.GetField("_loadSession",BindingFlags.Instance|BindingFlags.NonPublic)!.GetValue(host)!;
                        var operationId=(string)operation.GetType().GetField("Id",BindingFlags.Instance|BindingFlags.NonPublic)!.GetValue(operation)!;
                        var actual=GmSessionRunRecordCodec.Decode(File.ReadAllBytes(Path.Combine(files.BasePath,".boe_runtime/gm-runs/main.json")));
                        var installed=FileSystemManager.ParseSessionGenerationText(File.ReadAllText(files.SessionGenerationPath));
                        if(actual.Disposition!=GmSessionRunDisposition.Running || !GmSessionRunValidation.IdentityMatches(actual.Identity,fresh.Identity) ||
                            terminal.Identity.RunId!=actual.Identity.RunId || actual.Identity.GenerationId!=installed ||
                            terminal.Identity.RunId==original.Identity.RunId || !original.RootExited.IsCompleted)
                            throw new InvalidOperationException("Receipt fixture lacks an actual new Running owner after original stop/replacement.");
                        GmSessionRunIdentity? supplied=actual.Identity;
                        var suppliedTerminal=terminal.Identity;
                        switch(receiptBoundary) {
                            case "exact": break;
                            case "generation": supplied=supplied with {GenerationId=Guid.NewGuid().ToString("N")};break;
                            case "run": supplied=supplied with {RunId=original.Identity.RunId};break;
                            case "terminal": suppliedTerminal=suppliedTerminal with {RunId=Guid.NewGuid().ToString("N")};break;
                            case "null": supplied=null;break;
                            case "root": supplied=supplied with {RootKey=Path.Combine(folder,"foreign-root")};break;
                            case "backend": supplied=supplied with {Backend=GmSessionRunBackend.WindowsJob};break;
                            case "invalid": supplied=supplied with {HostInstanceId="invalid-host"};break;
                            case "foreign-host": supplied=supplied with {HostInstanceId=Guid.NewGuid().ToString("N")};break;
                            default: throw new InvalidOperationException("Unknown finite receipt fixture.");
                        }
                        receiptCuts++;
                        evidence["ActualFreshReceiptIdentity"]=actual.Identity;evidence["ReceiptBoundary"]=receiptBoundary;
                        evidence["ActualFreshTerminal"]=terminal.Identity;evidence["InjectedReceiptIdentity"]=supplied;
                        // First reply on the retained original connection is altered;
                        // canonical metadata/actual owner are never fabricated.
                        MainOperationReader.WriteAsync(stream,new GmLoadSessionReply(true,operationId,GmLoadMainState.StartedNotReady,supplied,suppliedTerminal),CancellationToken.None)
                            .WaitAsync(TimeSpan.FromSeconds(3)).GetAwaiter().GetResult();
                    }));
            }
            var engine=ProductionLoadGameEngine.Create(files,settings,save);
            var load=(Task<LoadReplacementResult>)typeof(GameEngine).GetMethod("LoadSelectedSaveWithMainLifecycleAsync",BindingFlags.Instance|BindingFlags.NonPublic)!.Invoke(engine,[path])!;
            var console=await load; evidence["ConsoleLoadResult"]=new{console.Disposition,console.EstablishedGeneration,console.SelectedSourcePath,console.NeedsFollowUp,console.ContinuationBlocked,Failure=console.Failure?.ToString()};
            if(console.Disposition!=LoadReplacementDisposition.Committed)throw new InvalidOperationException("Causal RED: actual console selected Load refused the original Running GM: "+console.Disposition);
            if(!original.RootExited.IsCompleted)throw new InvalidOperationException("Console replacement preceded original stop.");
            if(receiptBoundary!=null) {
                var received=(GmLoadMainState)typeof(GameEngine).GetField("_consoleLoadMainState",BindingFlags.Instance|BindingFlags.NonPublic)!.GetValue(engine)!;
                evidence["ReceiptCuts"]=receiptCuts;evidence["ReceivedMainState"]=received;
                if(receiptCuts!=1)throw new InvalidOperationException("Actual original restart receipt cut not reached exactly once.");
                if(receiptBoundary=="exact") {
                    if(received!=GmLoadMainState.StartedNotReady || console.NeedsFollowUp || console.ContinuationBlocked)
                        throw new InvalidOperationException("Exact new owner receipt falsely reports unconfirmed restart instead of manual readiness.");
                } else if(received!=GmLoadMainState.Uncertain || !console.NeedsFollowUp || !console.ContinuationBlocked)
                    throw new InvalidOperationException("Malformed/foreign StartedNotReady receipt was not refused by the original client.");
                var retained=(GmSessionRunCoordinator)hostType.GetField("_mainRun",BindingFlags.Instance|BindingFlags.NonPublic)!.GetValue(host)!;
                if(retained.Record?.Disposition!=GmSessionRunDisposition.Running || retained.Identity.GenerationId!=console.EstablishedGeneration)
                    throw new InvalidOperationException("Receipt handling changed or replayed the actual fresh original owner.");
                return;
            }
            if(mode.EndsWith("debt",StringComparison.Ordinal)) {
                evidence["CleanupDebtInjected"]=debtInjected;
                if(debtInjected!=1 || !console.NeedsFollowUp)throw new InvalidOperationException("Preparation failure: intended committed cleanup debt was not reached.");
                if(!console.NeedsFollowUp || !console.ContinuationBlocked || hostType.GetField("_pty",BindingFlags.Instance|BindingFlags.NonPublic)!.GetValue(host)!=null)
                    throw new InvalidOperationException("Causal RED: committed cleanup debt freshly launched a GM instead of retaining the decision and stopping continuation.");
                return;
            }
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
