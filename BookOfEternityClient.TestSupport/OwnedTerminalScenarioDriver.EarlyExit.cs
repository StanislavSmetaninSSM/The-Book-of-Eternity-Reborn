using System.Reflection;
using System.Text.Json;
using BookOfEternityClient.Core;
using BookOfEternityClient.Services.GmRuntime;
using BookOfEternityClient.Services.GmWorkers;

namespace BookOfEternityClient.Tests;
internal static partial class OwnedTerminalScenarioDriver
{
    private static async Task<int> RunEarlyExitAsync(string mode,Type type,object host,string folder,
        Dictionary<string,object?> result,Func<object,Task<JsonElement>> rpc)
    {
        var terminal=(IOwnedTerminalSession)type.GetField("_pty",BindingFlags.Instance|BindingFlags.NonPublic)!.GetValue(host)!;
        var main=(GmSessionRunCoordinator)type.GetField("_mainRun",BindingFlags.Instance|BindingFlags.NonPublic)!.GetValue(host)!;
        var identity=main.Identity;
        var fault=mode.EndsWith("metadata-fault",StringComparison.Ordinal);
        var entered=new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        var release=new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        object? Call(string name)=>type.GetMethod(name,BindingFlags.Instance|BindingFlags.NonPublic)!.Invoke(host,null);
        void Require(bool value,string error){if(!value)throw new InvalidOperationException(error);}
        // Settle startup publications before selecting this exact in-flight one.
        await (Task)Call("SealOriginalStatusPublicationAsync")!;
        Call("OpenOriginalStatusPublication");
        type.GetField("BeforeStatusPublication",BindingFlags.Instance|BindingFlags.NonPublic)!.SetValue(host,(Func<Task>)(async()=>{
            entered.TrySetResult();await release.Task;
            if(fault)throw GmSessionRunPersistence.Invalid();
        }));
        try
        {
            Call("WriteStatusFile");await entered.Task.WaitAsync(TimeSpan.FromSeconds(2));
            Require(!main.AdmissionClosed && !main.IsUncertain && !terminal.RootExited.IsCompleted,"Selected original publication was not held while Running.");
            result["OriginalIdentity"]=identity;
            var input=await rpc(new{command="addText",text="exit-error\r"});
            Require(input.GetProperty("ok").GetBoolean(),"Actual pipe refused inert CLI exit input.");
            result["OriginalLeaderExit"]=await terminal.RootExited.WaitAsync(TimeSpan.FromSeconds(3));
            result["FixtureRequestedExitCode"]=17;
            Require(!terminal.AuthorityLost.IsCompleted,"Original root exit lost native authority; cannot classify as lifecycle withdrawal.");
            Require(!main.AdmissionClosed,"Stop masked the selected early-exit publication.");
            release.TrySetResult();
            Exception? publicationFailure=null;
            try {await ((Task)Call("SealOriginalStatusPublicationAsync")!).WaitAsync(TimeSpan.FromSeconds(3));}
            catch(Exception ex){publicationFailure=ex;}
            result["SelectedPublicationFailure"]=publicationFailure?.ToString();
            result["PublicationSettledBeforeBeginStop"]=true;
            result["UncertainBeforeStop"]=main.IsUncertain;
            // One original stop is attempted even if observation failed. No retry.
            result["ShutdownAttempts"]=1;
            var stop=await rpc(new{command="shutdown",rootKey=identity.RootKey,expectedMainIdentity=identity});
            result["ShutdownReceipt"]=stop;
            Require(main.Identity==identity,"Early exit changed original durable identity.");
            result["DurableDisposition"]=main.Record?.Disposition.ToString();
            if(fault)
            {
                Require(publicationFailure is IOException && main.IsUncertain && main.RetainsAuthority && main.AdmissionClosed,
                    "Generic marked metadata failure was swallowed as lifecycle withdrawal.");
                Require(main.Record?.Disposition==GmSessionRunDisposition.Uncertain && !stop.GetProperty("ok").GetBoolean(),"Metadata failure was falsely Stopped/ACK.");
                Require(ReferenceEquals(terminal,type.GetField("_pty",BindingFlags.Instance|BindingFlags.NonPublic)!.GetValue(host)),"Uncertain lost original terminal.");
                Require(!(await rpc(new{command="addText",text="must-not-replay"})).GetProperty("ok").GetBoolean(),"Uncertain admitted later input.");
                try {await main.RunOperationAsync(()=>Task.FromResult(true));throw new InvalidOperationException("Uncertain admitted a new operation pin.");}
                catch(IOException){}
                await using var excluded=await GmWorkerRunLedger.OpenCoordinatorAsync(new(Path.Combine(folder,"root")));
                Require(excluded==null,"Uncertain released original worker inventory.");
                result["LogicalUncertainOriginalInventoryRetained"]=true;
            }
            else
            {
                Require(publicationFailure==null && !main.IsUncertain,"Causal RED: ordinary original root exit contaminated status settlement with Uncertain.");
                Require(stop.GetProperty("ok").GetBoolean() && main.Record?.Disposition==GmSessionRunDisposition.Stopped,"Original early-exit stop lacked durable Stopped/ACK.");
                var proof=stop.GetProperty("status").GetProperty("terminalStop");
                Require(proof.GetProperty("state").GetString()=="stopped-within-scope" && proof.GetProperty("cleanupComplete").GetBoolean(),"Original actual scope/I/O/disposal not confirmed.");
                Require(type.GetField("_pty",BindingFlags.Instance|BindingFlags.NonPublic)!.GetValue(host)==null,"Confirmed original owner not retired.");
                result["ScopedStopViaPipe"]=true;
            }
            result["Success"]=true;return 0;
        }
        finally
        {
            release.TrySetResult();
            // Cleanup authority is the retained original terminal, never PID/status.
            // Physical scope empty is not logical success or a metadata clear.
            var cleanup=await terminal.StopAndObserveAsync(CancellationToken.None);
            result["OriginalPhysicalCleanup"]=cleanup;
            Require(cleanup.State==GmWorkerStopState.StoppedWithinScope && cleanup.CleanupComplete,
                "Original physical cleanup unconfirmed; guardian closure is insufficient.");
        }
    }
}
