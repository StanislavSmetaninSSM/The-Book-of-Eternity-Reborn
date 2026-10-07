using BookOfEternityClient.Services.GmWorkers;
namespace BookOfEternityClient.Services.GmRuntime;
internal sealed class SystemdUserScopeOwner
{
    private readonly NativeLineageOwner _original;
    private readonly IOwnedTerminalSession _native;
    private readonly SystemdUserBus _bus;
    private readonly SystemdCgroupObservation _cgroup;
    private NativeHeldTerminalPidfd? _held;
    private readonly TaskCompletionSource<string> _lost=new(TaskCreationOptions.RunContinuationsAsynchronously);
    private volatile bool _disposed;
    private int _release;
    internal SystemdUserScopeOwner(NativeLineageOwner original,IOwnedTerminalSession native,SystemdControlledFixture fixture) {
        _original=original;_native=native;_bus=new(fixture.Transport);_cgroup=new(fixture.Cgroup);
        _=ObserveAuthorityAsync(native.AuthorityLost);_=ObserveAuthorityAsync(_bus.AuthorityLost);
    }
    internal Task<string> AuthorityLost {get {
        if(!_disposed && (_native.AuthorityLost.IsCompleted || _bus.AuthorityLost.IsCompleted))MarkUncertain("systemd-original-authority-lost");
        return _lost.Task;
    }}
    internal void MarkUncertain(string reason)=>_lost.TrySetResult(reason);
    private async Task ObserveAuthorityAsync(Task<string> task) {
        try{var reason=await task;if(!_disposed)MarkUncertain(reason);}catch{if(!_disposed)MarkUncertain("systemd-authority-observation-lost");}
    }
    internal async Task AttachAsync(CancellationToken token) {
        _held=NativeHeldTerminalPidfd.FromOriginal(_original);
        using var bound=CancellationTokenSource.CreateLinkedTokenSource(token);bound.CancelAfter(_held.Remaining);
        await _bus.AttachAsync(_held,bound.Token);_cgroup.Bind(_bus.Unit,_held.Pid);
        bound.Token.ThrowIfCancellationRequested();_held.ValidateHeld();_bus.RequireManager();
    }
    internal async Task ReleaseAsync(CancellationToken token) {
        if(Interlocked.Exchange(ref _release,1)!=0)throw new InvalidOperationException("Original scope release is single-use.");
        try {
            if(AuthorityLost.IsCompleted || _held==null)throw new IOException("Original scope release authority lost.");
            using var bound=CancellationTokenSource.CreateLinkedTokenSource(token);bound.CancelAfter(_held.Remaining);
            _held.ValidateHeld();await _bus.ValidateOriginalAsync(_held.Descriptor,bound.Token);_cgroup.Validate();
            await _original.ReleaseTerminalAsync(bound.Token);
        }catch{MarkUncertain("systemd-release-unconfirmed");throw;}
    }
    internal async Task<TerminalStopEvidence> StopAsync(TerminalIdentity identity) {
        var empty=false;
        try {
            using var bound=new CancellationTokenSource(TimeSpan.FromSeconds(2));
            if(!AuthorityLost.IsCompleted && _bus.Bound) {
                await _bus.StopAsync(bound.Token);empty=_cgroup.ReadFreshEmpty();_bus.RequireManager();
                if(!empty)MarkUncertain("systemd-scope-not-empty");
            }else MarkUncertain("systemd-scope-unbound-or-lost");
        }catch{MarkUncertain("systemd-stop-unconfirmed");}
        // Always attempt narrow native cleanup, never promote it to cgroup proof.
        try {
            var native=await _native.StopAndObserveAsync(CancellationToken.None);
            if(native.State!=GmWorkerStopState.StoppedWithinScope || !native.CleanupComplete || native.AuthorityRetained)
                MarkUncertain("systemd-native-retirement-unconfirmed");
            if(empty && !AuthorityLost.IsCompleted) {
                using var bound=new CancellationTokenSource(TimeSpan.FromSeconds(2));
                await _bus.ValidateOriginalAsync(null,bound.Token);empty=_cgroup.ReadFreshEmpty();_bus.RequireManager();
                if(!empty)MarkUncertain("systemd-post-reap-scope-not-empty");
            }
        }catch{MarkUncertain("systemd-post-reap-or-native-retirement-unconfirmed");}
        return AuthorityLost.IsCompleted || !empty
            ? new(identity,GmWorkerStopState.Uncertain,"systemd-original-authority-uncertain",false,true)
            : new(identity,GmWorkerStopState.StoppedWithinScope,"original-unit-cgroup-empty-and-native-reaped",true,false);
    }
    internal async ValueTask DisposeAsync() {
        if(_disposed)return;if(AuthorityLost.IsCompleted)throw new InvalidOperationException("Uncertain scope retains original authorities.");
        try {
            await _native.DisposeAsync();
            if(AuthorityLost.IsCompleted)throw new InvalidOperationException("Original scope lost before disposal.");
            _disposed=true; // expected connection closure only after actual native I/O/disposal
            await _bus.DisposeAsync();_cgroup.Dispose();_held?.Dispose();
        }catch{_disposed=false;MarkUncertain("systemd-disposal-unconfirmed");throw;}
    }
}
