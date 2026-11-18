using System.Diagnostics;
using System.Runtime.ExceptionServices;
using System.Text.Json;
using BookOfEternityClient.Core;
using BookOfEternityClient.Services.GmRuntime;
namespace BookOfEternityClient.Tests;
internal static partial class GmBridgeConsumerScenarioDriver
{
    // DRAFT: called only after actual initial Running status publication, with no
    // intervening launcher status mutations. No arbitrary sleep or new authority hook.
    private static async Task<bool> VerifyHeldReadyAsync(FileSystemManager files, string status, string session,
        string repo, string output, GmSessionRunRecord original,
        Dictionary<string,object?> result, CancellationToken token)
    {
        var held=new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        var release=new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        MainOperationClose? holderClose=null;bool holderObserved=false;
        using var stdout=File.Create(Path.Combine(output,"held-ready.stdout.log"));
        using var stderr=File.Create(Path.Combine(output,"held-ready.stderr.log"));
        var holder=SessionOperationContext.RunParticipatingCurrentSessionAsync(files,async()=>
        {
            await using var lease=await files.AcquireCanonicalWriteLeaseAsync();
            held.TrySetResult();await release.Task;return true;
        },observeOriginalClose:(close,observed,remote)=>{holderClose=close;holderObserved=observed&&remote;});
        Process? child=null;Task? exited=null,outPump=null,errPump=null;
        Exception? primary=null;var cleanupFailures=new List<string>();
        var events=new List<JsonElement>();
        var nonce=Environment.GetEnvironmentVariable(GmSessionRunCoordinator.StopDiagnosticNonceVariable);
        DateTimeOffset? timeoutAt=null,stdinAt=null,releaseAt=null;
        try
        {
            await held.Task.WaitAsync(token);
            var start=new ProcessStartInfo("pwsh"){UseShellExecute=false,RedirectStandardInput=true,
                RedirectStandardOutput=true,RedirectStandardError=true,WorkingDirectory=repo};
            foreach(var arg in new[]{"-NoProfile","-File",Path.Combine(repo,"tests/fixtures/ProductionMain/held-ready.ps1"),
                "-GameSessionPath",session,"-Helper",Path.Combine(repo,"BookOfEternityClient/Launcher/gm_main_operation.ps1")})start.ArgumentList.Add(arg);
            child=Process.Start(start)??throw new IOException("Held-ready process absent.");
            child.StandardInput.Close();
            outPump=child.StandardOutput.BaseStream.CopyToAsync(stdout);
            errPump=ObserveErrorAsync(child);
            exited=child.WaitForExitAsync();
            await exited.WaitAsync(token);await Task.WhenAll(outPump,errPump).WaitAsync(token);
            if(timeoutAt==null||stdinAt==null||releaseAt==null)
                throw new IOException("Required timeout/stdin-close causal boundaries were not reached; no retry.");
        }
        catch(Exception failure){primary=failure;}
        finally
        {
            // Even malformed marker, early process exit or timeout releases OUR lease.
            release.TrySetResult();
            using var cleanup=new CancellationTokenSource(TimeSpan.FromSeconds(8));
            async Task Attempt(string name,Func<Task> action){try{await action().WaitAsync(cleanup.Token);}catch(Exception e){cleanupFailures.Add(name+": "+e);}}
            await Attempt("holder",()=>holder);
            try{if(child!=null&&!child.HasExited)child.Kill(entireProcessTree:true);}catch(Exception e){cleanupFailures.Add("kill: "+e);}
            if(child!=null)await Attempt("exit",()=>exited??=child.WaitForExitAsync());
            if(outPump!=null)await Attempt("stdout",()=>outPump);
            if(errPump!=null)await Attempt("stderr",()=>errPump);
            await Attempt("stdout-flush",()=>stdout.FlushAsync(cleanup.Token));
            await Attempt("stderr-flush",()=>stderr.FlushAsync(cleanup.Token));
            result["HeldReadyEvents"]=events;
            result["HeldReadyReleaseUtc"]=releaseAt;
            result["HeldReadyHolderClose"]=holderClose;
            result["HeldReadyHolderCloseObserved"]=holderObserved;
            result["HeldReadyExitObserved"]=exited?.IsCompletedSuccessfully==true;
            result["HeldReadyStdoutDrained"]=outPump?.IsCompletedSuccessfully==true;
            result["HeldReadyStderrDrained"]=errPump?.IsCompletedSuccessfully==true;
            result["HeldReadyCleanupFailures"]=cleanupFailures;
            if(primary!=null)result["HeldReadyPrimaryFailure"]=primary.ToString();
            if(child!=null&&exited?.IsCompletedSuccessfully==true)result["HeldReadyExit"]=child.ExitCode;
            child?.Dispose();
        }
        if(primary!=null)ExceptionDispatchInfo.Capture(primary).Throw();
        if(cleanupFailures.Count!=0||exited?.IsCompletedSuccessfully!=true||outPump?.IsCompletedSuccessfully!=true||errPump?.IsCompletedSuccessfully!=true||!holderObserved||holderClose?.Identity!=original.Identity)
            throw new IOException("Held-ready owned cleanup incomplete.");
        if(!result.TryGetValue("HeldReadyExit",out var childExit)||!Equals(childExit,0))throw new IOException("Diagnostic PS did not finish its expected refusal capture.");
        var completion=events.Single(e=>e.GetProperty("stage").GetString()=="helper-open-completion-after-dispose");
        var raw=completion.GetProperty("diagnostic").GetString()??throw new IOException("Original helper diagnostics absent.");
        var markers=new List<JsonElement>();
        foreach(var line in raw.Split('\n'))
        {
            const string prefix="BOE_MAIN_OPERATION_MARKER ";
            if(!line.StartsWith(prefix,StringComparison.Ordinal))continue;
            using var doc=JsonDocument.Parse(line[prefix.Length..]);markers.Add(doc.RootElement.Clone());
        }
        var active=markers.Single(m=>m.GetProperty("Stage").GetString()=="active-received");
        if(active.GetProperty("Nonce").GetString()!=nonce || active.GetProperty("ObservedAtUtc").GetDateTimeOffset()>timeoutAt)
            throw new IOException("Actual helper Active was not established before the timeout.");
        var grant=JsonSerializer.Deserialize<MainOperationReply>(active.GetProperty("OriginalPin"),MainOperationReader.Json)
            ??throw new IOException("Original helper grant absent.");
        if(!grant.Ok||grant.State!=MainOperationState.Active||grant.Identity!=original.Identity||
            !Guid.TryParseExact(grant.PinId,"N",out _)||!Guid.TryParseExact(grant.CloseId,"N",out _)||
            !Guid.TryParseExact(grant.OperationId,"N",out _)||grant.OperationId==holderClose.OperationId)
            throw new IOException("Held helper is not a distinct actual original Active operation.");
        var acquired=markers.Where(m=>m.GetProperty("Stage").GetString()=="canonical-current-generation-lease-acquired").ToArray();
        result["HeldReadyOriginalGrant"]=grant;result["HeldReadyManagedMarkers"]=markers;
        if(acquired.Length!=1||acquired[0].GetProperty("ProcessId").GetInt32()!=active.GetProperty("ProcessId").GetInt32()||
            acquired[0].GetProperty("Nonce").GetString()!=nonce||acquired[0].GetProperty("ObservedAtUtc").GetDateTimeOffset()<releaseAt)
        {
            result["HeldReadyClassification"]="Acquisition expired/refused before proved post-release acquisition; inconclusive";
            throw new IOException("Canonical acquisition ordering not established; no replay.");
        }
        var projection=new MainOperationClose(grant.PinId!,grant.CloseId!,grant.OperationId!,original.Identity,MainOperationOutcome.Completed,false);
        var replyRaw=await BridgeTransportConnector.RequestAsync(status,JsonSerializer.Serialize(new{command="mainOperationStatus",mainOperationClose=projection}),3000,token);
        await File.WriteAllTextAsync(Path.Combine(output,"held-ready-original-pin.json"),replyRaw,token);
        var reply=JsonSerializer.Deserialize<MainOperationReply>(replyRaw,MainOperationReader.Json)??throw new IOException("Original pin status absent.");
        if(reply != (grant with {State=reply.State}) || reply.State is not (MainOperationState.ClosedObserved or MainOperationState.Unresolved))
            throw new IOException("Original pin has no proved terminal close/loss outcome.");
        result["HeldReadyClassification"]=reply.State==MainOperationState.ClosedObserved?"CleanCloseAfterTimeout":"UnresolvedActiveAfterTimeout";
        result["HeldReadyOriginalPinOutcome"]=reply;
        return reply.State==MainOperationState.Unresolved;

        async Task ObserveErrorAsync(Process process)
        {
            using var writer=new StreamWriter(stderr,System.Text.Encoding.UTF8,leaveOpen:true){AutoFlush=true};
            while(await process.StandardError.ReadLineAsync() is { } line)
            {
                await writer.WriteLineAsync(line); // No raw diagnostic quota.
                const string prefix="BOE_MAIN_OPERATION_PS ";
                if(!line.StartsWith(prefix,StringComparison.Ordinal))continue;
                using var doc=JsonDocument.Parse(line[prefix.Length..]);var row=doc.RootElement;
                if(row.GetProperty("nonce").GetString()!=nonce||row.GetProperty("processId").GetInt32()!=process.Id)
                    throw new IOException("Diagnostic marker identity changed.");
                events.Add(row.Clone());
                var stage=row.GetProperty("stage").GetString();
                if(stage is not ("reply-timeout" or "helper-stdin-closed"))continue;
                if(row.GetProperty("lastAction").GetString()!="helper-ready")throw new IOException("Wrong participating phase.");
                var observed=row.GetProperty("observedAtUtc").GetDateTimeOffset();
                if(stage=="reply-timeout") {if(timeoutAt!=null)throw new IOException("Repeated timeout marker.");timeoutAt=observed;}
                else
                {
                    if(timeoutAt==null||stdinAt!=null||observed<timeoutAt)throw new IOException("Missing/reordered timeout witness.");
                    stdinAt=observed;releaseAt=DateTimeOffset.UtcNow;release.TrySetResult();
                }
            }
        }
    }
}
