using BookOfEternityClient.Services.GmWorkers;
namespace BookOfEternityClient.Services.GmRuntime;
internal sealed class SystemdOwnedTerminalSession : IOwnedTerminalSession
{
    private readonly IOwnedTerminalSession _native;
    private readonly SystemdUserScopeOwner _scope;
    private Task<TerminalStopEvidence>? _stop;
    private Task? _dispose;
    private volatile bool _stopping;
    private readonly object _gate=new();
    internal SystemdOwnedTerminalSession(IOwnedTerminalSession native,SystemdUserScopeOwner scope) {
        _native=native;_scope=scope;Identity=native.Identity with {Backend="systemd-user",Guarantee="owned-unit-cgroup"};
        InputWriter=new GuardedWriter(native.InputWriter,()=>!_stopping && !AuthorityLost.IsCompleted);
    }
    public TerminalIdentity Identity {get;}
    public Stream InputWriter {get;}
    public Stream OutputReader=>_native.OutputReader;
    public Task<TerminalRootExit> RootExited=>_native.RootExited;
    public Task<string> AuthorityLost=>_scope.AuthorityLost;
    public ValueTask ResizeAsync(TerminalSize size,CancellationToken token) {
        if(_stopping || AuthorityLost.IsCompleted)throw new IOException("Original scope terminal input closed.");return _native.ResizeAsync(size,token);
    }
    public async Task<TerminalStopEvidence> StopAndObserveAsync(CancellationToken token) {
        Task<TerminalStopEvidence> task;lock(_gate){_stopping=true;task=_stop??=_scope.StopAsync(Identity);}
        var proof=await task.WaitAsync(token);
        return AuthorityLost.IsCompleted && proof.State!=GmWorkerStopState.Uncertain
            ? proof with {State=GmWorkerStopState.Uncertain,Reason="systemd-late-authority-loss",CleanupComplete=false,AuthorityRetained=true}:proof;
    }
    public ValueTask DisposeAsync(){lock(_gate)return new(_dispose??=DisposeCoreAsync());}
    private async Task DisposeCoreAsync() {
        var proof=await StopAndObserveAsync(CancellationToken.None);
        if(proof.State!=GmWorkerStopState.StoppedWithinScope || !proof.CleanupComplete || proof.AuthorityRetained)
            throw new InvalidOperationException("Uncertain original systemd terminal retained.");
        await _scope.DisposeAsync();
    }
    private sealed class GuardedWriter(Stream inner,Func<bool> allowed) : Stream
    {
        private void Validate(){if(!allowed())throw new IOException("Original systemd input authority closed.");}
        public override ValueTask WriteAsync(ReadOnlyMemory<byte> b,CancellationToken t=default){Validate();return inner.WriteAsync(b,t);}
        public override Task WriteAsync(byte[] b,int o,int c,CancellationToken t){Validate();return inner.WriteAsync(b,o,c,t);}
        public override void Write(byte[] b,int o,int c){Validate();inner.Write(b,o,c);}
        public override void Flush(){Validate();inner.Flush();}
        public override Task FlushAsync(CancellationToken t){Validate();return inner.FlushAsync(t);}
        public override bool CanRead=>false;public override bool CanSeek=>false;public override bool CanWrite=>true;
        public override long Length=>throw new NotSupportedException();public override long Position{get=>throw new NotSupportedException();set=>throw new NotSupportedException();}
        public override int Read(byte[] b,int o,int c)=>throw new NotSupportedException();public override long Seek(long o,SeekOrigin s)=>throw new NotSupportedException();public override void SetLength(long l)=>throw new NotSupportedException();
    }
}
