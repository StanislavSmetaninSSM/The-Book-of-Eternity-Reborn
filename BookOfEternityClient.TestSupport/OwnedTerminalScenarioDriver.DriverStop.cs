using System.Reflection;
using System.Text.Json;
using BookOfEternityClient.Core;
using BookOfEternityClient.Services.GmRuntime;
using Microsoft.Extensions.Logging.Abstractions;
namespace BookOfEternityClient.Tests;
internal static partial class OwnedTerminalScenarioDriver
{
    private static async Task RunHeldDriverPinAsync(string folder,object host,Type type,
        Func<object,Task<JsonElement>> rpc,Dictionary<string,object?> evidence)
    {
        var main=(GmSessionRunCoordinator)type.GetField("_mainRun",BindingFlags.Instance|BindingFlags.NonPublic)!.GetValue(host)!;
        var files=new FileSystemManager(Path.Combine(folder,"root"),NullLogger<FileSystemManager>.Instance);
        var terminal=(IOwnedTerminalSession)type.GetField("_pty",BindingFlags.Instance|BindingFlags.NonPublic)!.GetValue(host)!;
        evidence["OriginalTerminalIdentity"]=terminal.Identity;
        await using var original=await GmMainOperationClient.OpenAsync(files,CancellationToken.None);
        object Snapshot() {
            var sync=typeof(GmSessionRunCoordinator).GetField("_sync",BindingFlags.Instance|BindingFlags.NonPublic)!.GetValue(main)!;
            lock(sync) {
                var pins=(System.Collections.IDictionary)typeof(GmSessionRunCoordinator).GetField("_remotePins",BindingFlags.Instance|BindingFlags.NonPublic)!.GetValue(main)!;
                var input=type.GetField("_inputLifetime",BindingFlags.Instance|BindingFlags.NonPublic)!.GetValue(host);
                var tasks=new[]{"_outputPumpTask","_keyboardPumpTask","_resizePumpTask","_terminalRootTask","_terminalAuthorityTask","_terminalDisposeTask"}
                    .ToDictionary(n=>n,n=>((Task?)type.GetField(n,BindingFlags.Instance|BindingFlags.NonPublic)!.GetValue(host))?.Status.ToString());
                return new {main.IsUncertain,Disposition=main.Record?.Disposition.ToString(),ManagedTasks=tasks,
                    InputDrain=(input?.GetType().GetField("DrainTask")?.GetValue(input) as Task)?.Status.ToString(),
                    LifecycleAcquired=typeof(GmSessionRunCoordinator).GetField("_stopLifecycle",BindingFlags.Instance|BindingFlags.NonPublic)!.GetValue(main)!=null,
                    Pins=pins.Values.Cast<object>().Select(p=>(MainOperationReply)p.GetType().GetProperty("Reply",BindingFlags.Instance|BindingFlags.NonPublic)!.GetValue(p)!).ToArray()};
            }
        }
        evidence["BeforeOriginalStop"]=Snapshot();
        var now=System.Diagnostics.Stopwatch.StartNew();
        var receipt=await rpc(new {command="shutdown",rootKey=main.Identity.RootKey,expectedMainIdentity=main.Identity});
        evidence["OriginalStopReceipt"]=receipt;evidence["DrainElapsedSeconds"]=now.Elapsed.TotalSeconds;evidence["AfterOriginalStop"]=Snapshot();
        if(receipt.GetProperty("ok").GetBoolean() || !main.IsUncertain || now.Elapsed.TotalSeconds<4.5 ||
            typeof(GmSessionRunCoordinator).GetField("_stopLifecycle",BindingFlags.Instance|BindingFlags.NonPublic)!.GetValue(main)!=null)
            throw new InvalidOperationException("Held original pin did not establish the separate drain-timeout boundary.");
        var physical=receipt.GetProperty("status").GetProperty("terminalStop");
        if(physical.GetProperty("state").GetString()!="stopped-within-scope" ||
            physical.GetProperty("identity").GetProperty("runId").GetString()!=terminal.Identity.RunId ||
            physical.GetProperty("identity").GetProperty("rootPid").GetInt32()!=terminal.Identity.RootPid ||
            !physical.GetProperty("cleanupComplete").GetBoolean() || physical.GetProperty("authorityRetained").GetBoolean())
            throw new InvalidOperationException("Controlled physical scope did not settle independently.");
        // First terminal close on the same retained connection. This cannot undo
        // already-established Uncertain or mint a second original stop/epoch.
        await original.CompleteAsync(MainOperationOutcome.Failed,false);
        evidence["AfterOriginalClose"]=Snapshot();
        if(!main.IsUncertain || main.Record?.Disposition==GmSessionRunDisposition.Stopped)
            throw new InvalidOperationException("A late close repainted the original drain timeout as logical success.");
        evidence["LogicalStopConfirmed"]=false;evidence["OriginalRpcShutdownAttempts"]=1;
        evidence["RetainedCleanupCallsRemainInFinallyAndDispose"]=true;
    }
}
