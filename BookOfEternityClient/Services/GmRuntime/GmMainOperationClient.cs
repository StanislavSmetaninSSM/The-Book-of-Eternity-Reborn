using System.IO.Pipes;
using System.Text.Json;
using BookOfEternityClient.Core;

namespace BookOfEternityClient.Services.GmRuntime;

// Created only by a successful original connection handshake. A decoded identity
// is an expectation/refusal condition, never the constructor for a live pin.
internal sealed class GmMainOperationClient : IAsyncDisposable
{
    private readonly NamedPipeClientStream _pipe;
    private readonly MainOperationReader _reader;
    private readonly CancellationTokenSource _reading=new();
    private readonly MainOperationReply _grant;
    private readonly TaskCompletionSource<MainOperationReply> _closedReply=new(TaskCreationOptions.RunContinuationsAsynchronously);
    private Task _readTask=Task.CompletedTask;
    private int _lost,_closing,_locallyClosed;
    internal bool Closing=>Volatile.Read(ref _closing)!=0;
    internal GmSessionRunIdentity Identity=>_grant.Identity!;
    internal MainOperationClose? TerminalClose {get;private set;}
    private GmMainOperationClient(NamedPipeClientStream pipe,MainOperationReader reader,MainOperationReply grant){_pipe=pipe;_reader=reader;_grant=grant;}
    internal static async Task<GmMainOperationClient> OpenAsync(FileSystemManager files,CancellationToken token)
    {
        var bytes=GmSessionRunPersistence.Read(files.BasePath)??throw GmSessionRunPersistence.Invalid();
        var expected=GmSessionRunRecordCodec.Decode(bytes);
        if(expected.Disposition!=GmSessionRunDisposition.Running)throw GmSessionRunPersistence.Invalid();
        using var doc=JsonDocument.Parse(await files.ReadDiagnosticStatusAsync("game_state/control/gm_bridge_status.json")??throw GmSessionRunPersistence.Invalid());
        var name=doc.RootElement.GetProperty("pipeName").GetString();
        if(string.IsNullOrWhiteSpace(name) || name.Length>128)throw GmSessionRunPersistence.Invalid();
        var pipe=new NamedPipeClientStream(".",name,PipeDirection.InOut,PipeOptions.Asynchronous);
        try {
            using var bounded=CancellationTokenSource.CreateLinkedTokenSource(token);bounded.CancelAfter(TimeSpan.FromSeconds(3));
            await pipe.ConnectAsync(bounded.Token);var reader=new MainOperationReader(pipe);var operation=Guid.NewGuid().ToString("N");
            await MainOperationReader.WriteAsync(pipe,new{command="beginMainOperation",rootKey=files.BasePath,operationId=operation},bounded.Token);
            var grant=await reader.ReadAsync<MainOperationReply>(bounded.Token)??throw GmSessionRunPersistence.Invalid();
            if(!grant.Ok || grant.State!=MainOperationState.PreparedGrant || grant.OperationId!=operation ||
                !Guid.TryParseExact(grant.PinId,"N",out _) || !Guid.TryParseExact(grant.CloseId,"N",out _) || grant.Identity==null ||
                !GmSessionRunValidation.IdentityMatches(expected.Identity,grant.Identity))throw GmSessionRunPersistence.Invalid();
            await MainOperationReader.WriteAsync(pipe,new MainOperationFrame{Command="activateMainOperation",PinId=grant.PinId,CloseId=grant.CloseId,OperationId=operation,Identity=grant.Identity},bounded.Token);
            var active=await reader.ReadAsync<MainOperationReply>(bounded.Token)??throw GmSessionRunPersistence.Invalid();
            if(active!=(grant with{State=MainOperationState.Active}))throw GmSessionRunPersistence.Invalid();
            var client=new GmMainOperationClient(pipe,reader,active);client._readTask=client.ReadRepliesAsync();return client;
        } catch(Exception failure) {failure.Data["ParticipatingAdmissionPhase"]="original-connection";pipe.Dispose();throw;}
    }
    private async Task ReadRepliesAsync()
    {
        try {
            var reply=await _reader.ReadAsync<MainOperationReply>(_reading.Token);
            if(TerminalClose==null || reply!=(_grant with{State=MainOperationState.ClosedObserved}))throw new IOException("Original operation close reply lost.");
            _closedReply.TrySetResult(reply);
        } catch(Exception e){Interlocked.Exchange(ref _lost,1);_closedReply.TrySetException(e);}
    }
    internal void Abort(){Interlocked.Exchange(ref _lost,1);_pipe.Dispose();}
    internal void BeginClosing(){Interlocked.Exchange(ref _closing,1);}
    internal void Validate(string root,bool finalization)
    {
        if(Volatile.Read(ref _lost)!=0 || Volatile.Read(ref _locallyClosed)!=0 || root!=Identity.RootKey || (Closing && !finalization))throw GmSessionRunPersistence.Invalid();
        var r=GmSessionRunRecordCodec.Decode(GmSessionRunPersistence.Read(root)??throw GmSessionRunPersistence.Invalid());
        if(!GmSessionRunValidation.IdentityMatches(r.Identity,Identity) ||
            !(finalization && Closing ? r.Disposition is GmSessionRunDisposition.Running or GmSessionRunDisposition.Stopping or GmSessionRunDisposition.Uncertain : r.Disposition==GmSessionRunDisposition.Running))
            throw GmSessionRunPersistence.Invalid();
    }
    internal async Task CompleteAsync(MainOperationOutcome outcome,bool closingFailed)
    {
        if(Volatile.Read(ref _lost)!=0)throw new IOException("Original operation is unresolved.");
        if(Interlocked.Exchange(ref _locallyClosed,1)!=0)throw GmSessionRunPersistence.Invalid();
        BeginClosing();TerminalClose=new(_grant.PinId!,_grant.CloseId!,_grant.OperationId!,Identity,outcome,closingFailed);
        using var bounded=new CancellationTokenSource(TimeSpan.FromSeconds(3));
        await MainOperationReader.WriteAsync(_pipe,new MainOperationFrame{Command="closeMainOperation",Close=TerminalClose},bounded.Token);
        await _closedReply.Task.WaitAsync(bounded.Token); // one send; failure never repeats/mints
    }
    public async ValueTask DisposeAsync()
    {
        _pipe.Dispose();await _reading.CancelAsync();try{await _readTask;}catch{} _reading.Dispose();
        // Observe a failed reply even when admission unwinding never attempted close.
        if(_closedReply.Task.IsFaulted)_ = _closedReply.Task.Exception;
    }
}
