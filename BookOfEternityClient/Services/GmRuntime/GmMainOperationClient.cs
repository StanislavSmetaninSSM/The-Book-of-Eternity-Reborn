using System.Text.Json;
using BookOfEternityClient.Core;

namespace BookOfEternityClient.Services.GmRuntime;

// Created only by a successful original connection handshake. A decoded identity
// is an expectation/refusal condition, never the constructor for a live pin.
internal sealed class GmMainOperationClient : IAsyncDisposable
{
    private readonly Stream _pipe;
    private readonly MainOperationReader _reader;
    private readonly CancellationTokenSource _reading=new();
    private readonly MainOperationReply _grant;
    private readonly string? _diagnosticNonce;
    private readonly TaskCompletionSource<MainOperationReply> _closedReply=new(TaskCreationOptions.RunContinuationsAsynchronously);
    private Task _readTask=Task.CompletedTask;
    private int _lost,_closing,_locallyClosed;
    internal bool Closing=>Volatile.Read(ref _closing)!=0;
    internal GmSessionRunIdentity Identity=>_grant.Identity!;
    // Immutable original handshake DATA only; no connection or metadata read.
    internal MainOperationReply ObservedOriginalGrant=>_grant;
    internal MainOperationClose? TerminalClose {get;private set;}
    internal bool CloseObserved=>_closedReply.Task.IsCompletedSuccessfully;
    private GmMainOperationClient(Stream pipe,MainOperationReader reader,MainOperationReply grant,string? nonce){_pipe=pipe;_reader=reader;_grant=grant;_diagnosticNonce=nonce;}
    internal static async Task<GmMainOperationClient> OpenAsync(FileSystemManager files,CancellationToken token,GmSessionRunIdentity? originalExpectation=null)
    {
        var diagnosticNonce=MainOperationFailureDiagnostic.ReadNonce();
        var handshake="read-record";MainOperationReply? observedGrant=null;
        GmSessionRunRecord expected;
        try {
            var bytes=GmSessionRunPersistence.Read(files.BasePath)??throw GmSessionRunPersistence.Invalid();
            expected=GmSessionRunRecordCodec.Decode(bytes);
            if(originalExpectation!=null && !GmSessionRunValidation.IdentityMatches(originalExpectation,expected.Identity))throw GmSessionRunPersistence.Invalid();
            if(expected.Disposition!=GmSessionRunDisposition.Running)throw GmSessionRunPersistence.Invalid();
        } catch(Exception failure){failure.Data["ParticipatingAdmissionPhase"]="read-record";MainOperationFailureDiagnostic.Report(diagnosticNonce,"read-record",null,failure,owner:originalExpectation);throw;}
        string status;
        try {status=await files.ReadDiagnosticStatusAsync("game_state/control/gm_bridge_status.json")??throw GmSessionRunPersistence.Invalid();}
        catch(Exception failure){failure.Data["ParticipatingAdmissionPhase"]="read-endpoint";MainOperationFailureDiagnostic.Report(diagnosticNonce,"read-endpoint",null,failure,owner:expected.Identity);throw;}
        using var doc=JsonDocument.Parse(status);
        var endpoint=BridgeTransportConnector.DecodeStatus(doc.RootElement);
        Stream? pipe=null;
        try {
            using var bounded=CancellationTokenSource.CreateLinkedTokenSource(token);bounded.CancelAfter(TimeSpan.FromSeconds(3));
            handshake="connect";pipe=await BridgeTransportConnector.ConnectAsync(endpoint,bounded.Token);var reader=new MainOperationReader(pipe);var operation=Guid.NewGuid().ToString("N");
            handshake="begin-write";await MainOperationReader.WriteAsync(pipe,new{command="beginMainOperation",rootKey=files.BasePath,operationId=operation,expectedMainIdentity=originalExpectation??expected.Identity},bounded.Token);
            handshake="grant-read";var grant=await reader.ReadAsync<MainOperationReply>(bounded.Token)??throw GmSessionRunPersistence.Invalid();
            observedGrant=grant;
            if(!grant.Ok || grant.State!=MainOperationState.PreparedGrant || grant.OperationId!=operation ||
                !Guid.TryParseExact(grant.PinId,"N",out _) || !Guid.TryParseExact(grant.CloseId,"N",out _) || grant.Identity==null ||
                !GmSessionRunValidation.IdentityMatches(expected.Identity,grant.Identity))throw GmSessionRunPersistence.Invalid();
            handshake="activate-write";await MainOperationReader.WriteAsync(pipe,new MainOperationFrame{Command="activateMainOperation",PinId=grant.PinId,CloseId=grant.CloseId,OperationId=operation,Identity=grant.Identity},bounded.Token);
            handshake="active-reply-read";var active=await reader.ReadAsync<MainOperationReply>(bounded.Token)??throw GmSessionRunPersistence.Invalid();
            if(active!=(grant with{State=MainOperationState.Active}))throw GmSessionRunPersistence.Invalid();
            MainOperationFailureDiagnostic.Mark(diagnosticNonce,files.BasePath,"active-received",active);
            var client=new GmMainOperationClient(pipe,reader,active,diagnosticNonce);client._readTask=client.ReadRepliesAsync();return client;
        } catch(Exception failure) {
            failure.Data["ParticipatingAdmissionPhase"]="original-connection";
            try {pipe?.Dispose();}
            finally {MainOperationFailureDiagnostic.Report(diagnosticNonce,handshake,observedGrant,failure,owner:expected.Identity);}
            throw;
        }
    }
    private async Task ReadRepliesAsync()
    {
        try {
            var reply=await _reader.ReadAsync<MainOperationReply>(_reading.Token);
            if(TerminalClose==null || reply!=(_grant with{State=MainOperationState.ClosedObserved}))throw new IOException("Original operation close reply lost.");
            _closedReply.TrySetResult(reply);
        } catch(Exception e){Interlocked.Exchange(ref _lost,1);_closedReply.TrySetException(e);
            var closing=Closing;var close=TerminalClose;
            MainOperationFailureDiagnostic.Report(_diagnosticNonce,"active-close-reply-read",_grant,e,close,closing,owner:Identity);
        }
    }
    internal void Abort(){Interlocked.Exchange(ref _lost,1);_pipe.Dispose();}
    internal void BeginClosing(){Interlocked.Exchange(ref _closing,1);}
    internal void Validate(string root,bool finalization)
    {
        var backend=OperatingSystem.IsWindows()?GmSessionRunBackend.WindowsJob:GmSessionRunBackend.LinuxSupervisor;
        if(Volatile.Read(ref _lost)!=0 || Volatile.Read(ref _locallyClosed)!=0 || Identity.Backend!=backend ||
            !GmSessionRunValidation.AdmissionRootMatches(root,Identity.RootKey,backend) || (Closing && !finalization))throw GmSessionRunPersistence.Invalid();
        var r=GmSessionRunRecordCodec.Decode(GmSessionRunPersistence.Read(root)??throw GmSessionRunPersistence.Invalid());
        if(!GmSessionRunValidation.IdentityMatches(r.Identity,Identity) ||
            !(finalization && Closing ? r.Disposition is GmSessionRunDisposition.Running or GmSessionRunDisposition.Stopping or GmSessionRunDisposition.Uncertain : r.Disposition==GmSessionRunDisposition.Running))
            throw GmSessionRunPersistence.Invalid();
    }
    internal MainOperationClose DescribeClose(MainOperationOutcome outcome,bool closingFailed)=>new(_grant.PinId!,_grant.CloseId!,_grant.OperationId!,Identity,outcome,closingFailed);
    internal async Task CompleteAsync(MainOperationOutcome outcome,bool closingFailed)
    {
        TerminalClose=DescribeClose(outcome,closingFailed);
        if(Volatile.Read(ref _lost)!=0)throw new IOException("Original operation is unresolved.");
        if(Interlocked.Exchange(ref _locallyClosed,1)!=0)throw GmSessionRunPersistence.Invalid();
        BeginClosing();
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
