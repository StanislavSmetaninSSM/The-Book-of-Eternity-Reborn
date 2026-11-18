using System.Diagnostics;
using System.Security.Cryptography;
using System.Text;
using BookOfEternityGMBridge;
using System.Runtime.ExceptionServices;
using System.Text.Json;
using BookOfEternityClient.Core;
using BookOfEternityClient.Services.GmRuntime;
namespace BookOfEternityClient.Tests;
internal static partial class GmBridgeConsumerScenarioDriver
{
    // Real original owner, one cooperative cancellation, no authority/retry hook.
    private static async Task<bool> VerifyHeldReadyAsync(FileSystemManager files, string status, string session, BridgeHost host,
        string repo, string output, GmSessionRunRecord original,
        Dictionary<string,object?> result, CancellationToken token)
    {
        result["HeldReadyInitialStatusSHA256"]=Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(status))).ToLowerInvariant();
        var statusEntered=new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        var oldStatusObserver=host.BeforeStatusPublication;
        var gate=new object();var statusEntries=0;DateTimeOffset? firstStatusEntryAt=null,secondStatusEntryAt=null;
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
        DateTimeOffset? timeoutAt=null,cancelAt=null,releaseAt=null;
        try
        {
            await Task.WhenAny(held.Task,holder).WaitAsync(token);
            if(!held.Task.IsCompletedSuccessfully){await holder;throw new IOException("Holder ended before acquisition.");}
            host.BeforeStatusPublication=()=>{
                lock(gate){
                    statusEntries++;
                    if(statusEntries==1){firstStatusEntryAt=DateTimeOffset.UtcNow;statusEntered.TrySetResult();}
                    if(statusEntries==2){secondStatusEntryAt=DateTimeOffset.UtcNow;ReleaseIfBothObserved();}
                }
                return Task.CompletedTask;
            };
            var heldStatus=await BridgeTransportConnector.RequestAsync(status,JsonSerializer.Serialize(new{command="setready",ready=false}),3000,token);
            await File.WriteAllTextAsync(Path.Combine(output,"held-ready-status-queued.json"),heldStatus,token);
            using(var queued=JsonDocument.Parse(heldStatus)){
                if(!queued.RootElement.GetProperty("ok").GetBoolean()||queued.RootElement.GetProperty("status").GetProperty("terminalRunId").GetString()!=original.Identity.RunId)
                    throw new IOException("Real original status queue refused.");
                result["HeldReadyQueuedStatusUpdatedAt"]=queued.RootElement.GetProperty("status").GetProperty("updatedAtUtc").GetString();
            }
            await statusEntered.Task.WaitAsync(token);
            // First callback follows pending dequeue. This second REAL queue retains
            // newer DATA and a real signal, not a diagnostic retry/self-signal.
            var newerRaw=await BridgeTransportConnector.RequestAsync(status,JsonSerializer.Serialize(new{command="setready",ready=false}),3000,token);
            await File.WriteAllTextAsync(Path.Combine(output,"held-ready-newer-status-queued.json"),newerRaw,token);
            using(var newer=JsonDocument.Parse(newerRaw)){
                if(!newer.RootElement.GetProperty("ok").GetBoolean())throw new IOException("Newer status queue refused.");
                result["HeldReadyNewerStatusUpdatedAt"]=newer.RootElement.GetProperty("status").GetProperty("updatedAtUtc").GetString();
                if(Equals(result["HeldReadyQueuedStatusUpdatedAt"],result["HeldReadyNewerStatusUpdatedAt"]))throw new IOException("Two distinct queue snapshots not established.");
            }
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
            if(timeoutAt==null||cancelAt==null||releaseAt==null)
                throw new IOException("Required timeout/cancellation-send causal boundaries were not reached; no retry.");
        }
        catch(Exception failure){primary=failure;}
        finally
        {
            // Even malformed marker, early process exit or timeout releases OUR lease.
            release.TrySetResult();
            host.BeforeStatusPublication=oldStatusObserver;
            using var cleanup=new CancellationTokenSource(TimeSpan.FromSeconds(8));
            async Task Attempt(string name,Func<Task> action){try{await action().WaitAsync(cleanup.Token);}catch(Exception e){cleanupFailures.Add(name+": "+e);}}
            await Attempt("holder",()=>holder);
            try{if(child!=null&&!child.HasExited)child.Kill(entireProcessTree:true);}catch(Exception e){cleanupFailures.Add("kill: "+e);}
            if(child!=null)await Attempt("exit",()=>exited??=child.WaitForExitAsync());
            if(outPump!=null)await Attempt("stdout",()=>outPump);
            if(errPump!=null)await Attempt("stderr",()=>errPump);
            await Attempt("stdout-flush",()=>stdout.FlushAsync(cleanup.Token));
            await Attempt("stderr-flush",()=>stderr.FlushAsync(cleanup.Token));
            // The owned exclusive artifact handles must close after drained pumps,
            // before later forensic readback. Flush alone does not release sharing.
            if(outPump?.IsCompletedSuccessfully==true)await Attempt("stdout-dispose",()=>stdout.DisposeAsync().AsTask());
            if(errPump?.IsCompletedSuccessfully==true)await Attempt("stderr-dispose",()=>stderr.DisposeAsync().AsTask());
            result["HeldReadyEvents"]=events;
            result["HeldReadyReleaseUtc"]=releaseAt;
            result["HeldReadyFirstStatusEntryUtc"]=firstStatusEntryAt;
            result["HeldReadySecondStatusEntryUtc"]=secondStatusEntryAt;
            result["HeldReadyCancellationSentUtc"]=cancelAt;
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
        if(secondStatusEntryAt==null||releaseAt<secondStatusEntryAt||releaseAt<cancelAt)
            throw new IOException("Release did not follow both original cancellation and second publication entry.");
        if(cleanupFailures.Count!=0||exited?.IsCompletedSuccessfully!=true||outPump?.IsCompletedSuccessfully!=true||errPump?.IsCompletedSuccessfully!=true||!holderObserved||holderClose?.Identity!=original.Identity)
            throw new IOException("Held-ready owned cleanup incomplete.");
        if(!result.TryGetValue("HeldReadyExit",out var childExit)||!Equals(childExit,0))throw new IOException("Diagnostic PS did not finish its expected refusal capture.");
        using(var observed=JsonDocument.Parse(await File.ReadAllBytesAsync(Path.Combine(output,"held-ready.stdout.log"),token))){
            var row=observed.RootElement;
            if(row.GetProperty("bodyEntered").GetBoolean()||row.GetProperty("returned").GetBoolean()||
                !row.GetProperty("refusal").GetString()!.Contains("Original participating admission unavailable",StringComparison.Ordinal))
                throw new IOException("Readiness timeout returned or executed a body.");
        }
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
        var terminal=JsonSerializer.Deserialize<MainOperationClose>(completion.GetProperty("terminalClose"),MainOperationReader.Json)
            ??throw new IOException("Actual cancellation close absent.");
        var expected=new MainOperationClose(grant.PinId!,grant.CloseId!,grant.OperationId!,original.Identity,MainOperationOutcome.Cancelled,false);
        if(terminal!=expected||!completion.GetProperty("closeObserved").GetBoolean()||!completion.GetProperty("lost").GetBoolean()||
            completion.GetProperty("exitCode").GetInt32()!=0||completion.GetProperty("cancellationCleanupFailures").GetArrayLength()!=0)
            throw new IOException("Actual cancellation receipt or helper cleanup not qualified.");
        var readCleanup=completion.GetProperty("cancellationReadCleanup");
        foreach(var key in new[]{"stdoutReadSettled","stderrReadSettled","stdoutDisposed","stderrDisposed","exitObserved"})
            if(!readCleanup.GetProperty(key).GetBoolean())throw new IOException("Cancellation reader cleanup incomplete: "+key);
        var replyRaw=await BridgeTransportConnector.RequestAsync(status,JsonSerializer.Serialize(new{command="mainOperationStatus",mainOperationClose=terminal}),3000,token);
        await File.WriteAllTextAsync(Path.Combine(output,"held-ready-original-pin.json"),replyRaw,token);
        var reply=JsonSerializer.Deserialize<MainOperationReply>(replyRaw,MainOperationReader.Json)??throw new IOException("Original pin status absent.");
        if(reply != (grant with {State=MainOperationState.ClosedObserved}))
            throw new IOException("Original cancellation was not actually ClosedObserved.");
        result["HeldReadyClassification"]="CancelledClosedObservedAfterTimeout";
        result["HeldReadyOriginalPinOutcome"]=reply;result["HeldReadyActualTerminalClose"]=terminal;
        // A real later queue supplies fresh DATA, not a read of the unchanged old JSON.
        var latestRaw=await BridgeTransportConnector.RequestAsync(status,JsonSerializer.Serialize(new{command="setready",ready=false}),3000,token);
        await File.WriteAllTextAsync(Path.Combine(output,"held-ready-latest-status-queued.json"),latestRaw,token);
        using var latest=JsonDocument.Parse(latestRaw);
        if(!latest.RootElement.GetProperty("ok").GetBoolean())throw new IOException("Latest real status queue refused.");
        var updated=latest.RootElement.GetProperty("status").GetProperty("updatedAtUtc").GetString();
        if(updated==null||updated==(result["HeldReadyQueuedStatusUpdatedAt"] as string))throw new IOException("Latest queue did not supply fresh snapshot.");
        using var publicationDeadline=CancellationTokenSource.CreateLinkedTokenSource(token);
        publicationDeadline.CancelAfter(TimeSpan.FromSeconds(3));
        while(true){
            publicationDeadline.Token.ThrowIfCancellationRequested();
            var published=await files.ReadDiagnosticStatusAsync("game_state/control/gm_bridge_status.json");
            if(published!=null){using var document=JsonDocument.Parse(published);var snapshot=document.RootElement;
                if(snapshot.GetProperty("updatedAtUtc").GetString()==updated){
                    if(snapshot.GetProperty("terminalRunId").GetString()!=original.Identity.RunId||snapshot.GetProperty("terminalUncertain").GetBoolean()||
                        (snapshot.TryGetProperty("lastError",out var statusError)&&statusError.ValueKind is not JsonValueKind.Null))
                        throw new IOException("Latest original status is uncertain or failed.");
                    var bytes=Encoding.UTF8.GetBytes(published);
                    await File.WriteAllBytesAsync(Path.Combine(output,"held-ready-latest-status.json"),bytes,token);
                    result["HeldReadyLatestStatusSHA256"]=Convert.ToHexString(SHA256.HashData(bytes)).ToLowerInvariant();
                    result["HeldReadyLatestStatusUpdatedAt"]=updated;break;
                }
            }
            await Task.Delay(10,publicationDeadline.Token);
        }
        return false; // Only clean cancellation qualifies; unresolved is no longer expected success.

        void ReleaseIfBothObserved()
        {
            // Called only with gate held. Continuations are never inline under it.
            if(cancelAt!=null&&secondStatusEntryAt!=null&&releaseAt==null){releaseAt=DateTimeOffset.UtcNow;release.TrySetResult();}
        }

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
                if(stage is not ("reply-timeout" or "readiness-cancel-sent"))continue;
                var expectedAction=stage=="reply-timeout"?"helper-ready":"readiness-cancel";
                var expectedSequence=stage=="reply-timeout"?0:1;
                if(row.GetProperty("lastAction").GetString()!=expectedAction||row.GetProperty("sequence").GetInt64()!=expectedSequence)
                    throw new IOException("Wrong original participating cancellation phase.");
                var observed=row.GetProperty("observedAtUtc").GetDateTimeOffset();
                if(stage=="reply-timeout") {if(timeoutAt!=null)throw new IOException("Repeated timeout marker.");timeoutAt=observed;}
                else
                {
                    if(timeoutAt==null||cancelAt!=null||observed<timeoutAt)throw new IOException("Missing/reordered timeout witness.");
                    lock(gate){cancelAt=observed;ReleaseIfBothObserved();}
                }
            }
        }
    }
}
