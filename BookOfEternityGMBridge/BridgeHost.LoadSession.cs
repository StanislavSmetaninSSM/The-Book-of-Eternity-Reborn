using BookOfEternityClient.Core;
using BookOfEternityClient.Services.GmRuntime;
using Microsoft.Extensions.Logging.Abstractions;

namespace BookOfEternityGMBridge;
internal sealed partial class BridgeHost
{
    // Reference + retained connection are authority. IDs only reject stale requests.
    private sealed class LoadSession(string id,IOwnedTerminalSession? terminal,GmSessionRunCoordinator? owner)
    {
        internal readonly string Id=id;
        internal readonly IOwnedTerminalSession? Terminal=terminal;
        internal readonly GmSessionRunCoordinator? Owner=owner;
        internal bool Cancelled,Restarting,Closed;
    }
    private LoadSession? _loadSession;
    private readonly HashSet<string> _loadSessionIds=new(StringComparer.Ordinal);
    internal Func<Task>? BeforeLoadStopReply;
    internal Func<Task>? BeforeLoadRestart;
    internal Func<Task>? BeforeLoadRestartReply;
    internal Action<Stream,GmLoadMainState>? BeforeLoadReceipt;

    private BridgeResponse CancelLoadSession(BridgeRequest request)
    {
        lock(_sync) {
            if(_loadSession==null || _loadSession.Id!=request.OperationId || _loadSession.Restarting || _loadSession.Closed)
                return BridgeResponse.Failure("This original Load is absent or its restart decision is already settled.",SnapshotStatus());
            _loadSession.Cancelled=true;
            return BridgeResponse.Success(SnapshotStatus());
        }
    }

    private async Task ServeLoadSessionAsync(Stream stream,MainOperationReader reader,BridgeRequest request,CancellationToken token)
    {
        LoadSession? operation=null;
        GmLoadSessionReply Reply(GmLoadMainState state,bool ok=true,string? error=null)=>new(ok,request.OperationId??"",state,
            (_mainRun??operation?.Owner??_lastMainRun)?.Record?.Identity,_pty?.Identity??operation?.Terminal?.Identity,error);
        try {
            await _shellLifecycleLock.WaitAsync(token);
            try {
                if(request.RootKey!=_clientRoot || !Guid.TryParseExact(request.OperationId,"N",out _) ||
                    string.IsNullOrWhiteSpace(request.LoadSourceKey) || string.IsNullOrWhiteSpace(request.ExpectedGeneration))
                    throw new InvalidDataException("Incomplete immutable Load identity.");
                var terminal=_pty;var owner=_mainRun;
                if(owner!=null) {
                    if(request.ExpectedMainIdentity==null || request.ExpectedGeneration!=owner.Identity.GenerationId)
                        throw new InvalidDataException("Original main/generation mismatch.");
                    owner.ValidateStopExpectation(request.ExpectedMainIdentity);
                }
                else if(request.ExpectedMainIdentity!=null &&
                    (_lastMainRun?.Record?.Disposition!=GmSessionRunDisposition.Stopped ||
                    !GmSessionRunValidation.IdentityMatches(_lastMainRun.Identity,request.ExpectedMainIdentity)))
                    throw new InvalidDataException("Original main is no longer active.");
                if(terminal!=null && request.ExpectedTerminalRunId!=terminal.Identity.RunId)
                    throw new InvalidDataException("Original terminal mismatch.");
                // Windows has an actual original ConPTY/Job even without schema1.
                _neutralFiles??=new FileSystemManager(_clientRoot,NullLogger<FileSystemManager>.Instance);
                if(owner==null) {
                    using var admission=_neutralFiles.BeginMainAdmission();await admission.AcquireAsync(quiescentOnly:true);
                    await using var lease=await _neutralFiles.AcquireCanonicalWriteLeaseAsync();
                    if(_neutralFiles.ReadExistingSessionGeneration(lease)!=request.ExpectedGeneration)
                        throw new InvalidDataException("Original installed generation changed.");
                }
                lock(_sync) {
                    if(_loadSession!=null || _loadSessionIds.Count>=128 || !_loadSessionIds.Add(request.OperationId!))
                        throw new InvalidOperationException("Original Load reservation is retained or already used.");
                    operation=new(request.OperationId!,terminal,owner);_loadSession=operation;
                }
                await StopShellCoreAsync(); // all short filesystem leases above have unwound
                if(terminal!=null && (_pty!=null || (owner!=null && (owner.RetainsAuthority || owner.Record?.Disposition!=GmSessionRunDisposition.Stopped))))
                    throw new InvalidOperationException("Original stop/disposal/Stopped ACK remains unconfirmed.");
            } finally {_shellLifecycleLock.Release();}
            if(BeforeLoadStopReply!=null)await BeforeLoadStopReply();
            BeforeLoadReceipt?.Invoke(stream,operation!.Terminal==null?GmLoadMainState.NoActiveSession:GmLoadMainState.Stopped);
            using(var response=CancellationTokenSource.CreateLinkedTokenSource(token)) {
                response.CancelAfter(TimeSpan.FromSeconds(3));
                await MainOperationReader.WriteAsync(stream,Reply(operation!.Terminal==null?GmLoadMainState.NoActiveSession:GmLoadMainState.Stopped),response.Token);
            }
            using var wait=CancellationTokenSource.CreateLinkedTokenSource(token);wait.CancelAfter(TimeSpan.FromSeconds(60));
            var finish=await reader.ReadAsync<GmLoadSessionFrame>(wait.Token)??throw new IOException("Original Load connection lost.");
            if(finish.Command!="finishLoadSession" || finish.OperationId!=operation!.Id)
                throw new InvalidDataException("Original immutable Load finish mismatch.");
            await _shellLifecycleLock.WaitAsync(token);
            GmLoadMainState result;
            try {
                if(BeforeLoadRestart!=null)await BeforeLoadRestart();
                lock(_sync) {
                    if(!ReferenceEquals(_loadSession,operation) || operation.Closed)throw new InvalidOperationException("Original Load authority lost.");
                    if(operation.Cancelled || !finish.Committed || !finish.RefreshConfirmed || operation.Terminal==null) {
                        result=operation.Cancelled?GmLoadMainState.Cancelled:operation.Terminal==null?GmLoadMainState.NoActiveSession:GmLoadMainState.Stopped;
                        operation.Closed=true;
                    } else {
                        if(string.IsNullOrWhiteSpace(finish.EstablishedGeneration))throw new InvalidDataException("Installed Load generation is missing.");
                        operation.Restarting=true;result=GmLoadMainState.Uncertain;
                    }
                }
                if(operation.Restarting) {
                    await StartShellCoreAsync(finish.EstablishedGeneration);
                    result=_mainRun?.Record?.Disposition==GmSessionRunDisposition.Running?GmLoadMainState.Running:
                        OperatingSystem.IsWindows() && _pty!=null?GmLoadMainState.StartedNotReady:throw new InvalidOperationException("Fresh original launch is unconfirmed.");
                    lock(_sync)operation.Closed=true; // original decision survives a lost reply; never replay
                }
            } finally {_shellLifecycleLock.Release();}
            if(BeforeLoadRestartReply!=null)await BeforeLoadRestartReply();
            BeforeLoadReceipt?.Invoke(stream,result);
            using var reply=CancellationTokenSource.CreateLinkedTokenSource(token);reply.CancelAfter(TimeSpan.FromSeconds(3));
            await MainOperationReader.WriteAsync(stream,Reply(result),reply.Token);
        } catch {
            // Loss before completion keeps the live reservation. Physical cleanup is not logical resolution.
            try {using var refused=new CancellationTokenSource(TimeSpan.FromSeconds(1));
                await MainOperationReader.WriteAsync(stream,Reply(operation==null?GmLoadMainState.Refused:GmLoadMainState.Uncertain,false,"Load session completion is unconfirmed; do not repeat."),refused.Token);
            } catch { }
        } finally {
            lock(_sync)if(operation?.Closed==true && ReferenceEquals(_loadSession,operation))_loadSession=null;
        }
    }
}
