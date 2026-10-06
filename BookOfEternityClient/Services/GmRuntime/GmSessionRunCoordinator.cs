using BookOfEternityClient.Core;
using BookOfEternityClient.Services.GmWorkers;

namespace BookOfEternityClient.Services.GmRuntime;

// Live authority is the original coordinator/terminal/guard and admitted pin objects.
// Decoded identities, status, PID, EOF and caller-supplied stop records never mint it.
internal sealed class GmSessionRunCoordinator
{
    private readonly FileSystemManager _files;
    private readonly GmMainOwnerGuard _guard;
    private readonly GmSessionRunPersistence _persistence;
    private readonly object _sync=new();
    private readonly SemaphoreSlim _transitions=new(1,1);
    private GmSessionRunRecord? _record, _planned;
    private byte[]? _acknowledged;
    private IOwnedTerminalSession? _terminal;
    private bool _closed,_uncertain,_released,_retired;
    private int _pins;
    private TaskCompletionSource _drained=Completed();
    private TerminalStopEvidence? _settledStop;
    private FileSystemManager.SessionLifecycleLease? _stopLifecycle;
    private sealed record Retirement(IOwnedTerminalSession Session,Task[] Loops,Func<Task?> InputDrain);
    private Retirement? _retirement;
    internal void BindActualBridgeRetirement(IOwnedTerminalSession session,Task[] loops,Func<Task?> inputDrain)
    {
        // A partial original start also owns cleanup tasks. Registration does
        // not admit input, replay release, or mint successful launch evidence.
        if(!ReferenceEquals(session,_terminal) || _retirement!=null)throw GmSessionRunPersistence.Invalid();
        _retirement=new(session,loops.ToArray(),inputDrain);
    }
    private static TaskCompletionSource Completed(){var t=new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);t.SetResult();return t;}
    private static readonly AsyncLocal<Access?> Ambient=new();
    internal static Access? Current=>Ambient.Value;
    internal CanonicalRootIdentity RootIdentity=>_files.CanonicalRootAuthorityIdentity;
    internal GmSessionRunIdentity Identity=>_record?.Identity??throw GmSessionRunPersistence.Invalid();
    internal bool AdmissionClosed {get{lock(_sync)return _closed;}}
    internal bool HasMetadataDebt=>_persistence.HasDebt;
    internal bool RetainsAuthority=>!_retired;
    internal GmSessionRunRecord? Record=>_record;
    private GmSessionRunCoordinator(FileSystemManager files,GmMainOwnerGuard guard,Action<MainRunIoStage>? observe)
    {_files=files;_guard=guard;_persistence=new(guard,observe);}
    internal static async Task<GmSessionRunCoordinator> OpenNeutralAsync(FileSystemManager files,Action<MainRunIoStage>? observe=null)
    {
        var guard=await GmMainOwnerGuard.AcquireAsync(files.BasePath);
        try
        {
            var owner=new GmSessionRunCoordinator(files,guard,observe);
            lock(files.CanonicalRootAuthorityIdentity.WorkerContextGate)
            {
                if(files.CanonicalRootAuthorityIdentity.MainCoordinator!=null)throw GmSessionRunPersistence.Invalid();
                var bytes=GmSessionRunPersistence.Read(files.BasePath);
                if(bytes!=null) {
                    var r=GmSessionRunRecordCodec.Decode(bytes);
                    if(r.Disposition!=GmSessionRunDisposition.Stopped || r.Identity.Backend!=GmSessionRunBackend.LinuxSupervisor ||
                        r.Identity.RootKey!=files.BasePath || r.Identity.Epoch==long.MaxValue)throw GmSessionRunPersistence.Invalid();
                    owner._acknowledged=bytes;owner._record=r;
                }
                files.CanonicalRootAuthorityIdentity.MainCoordinator=owner;
            }
            return owner;
        }
        catch{guard.Dispose();throw;}
    }
    internal sealed record Access(GmSessionRunCoordinator Owner,OperationPin? Pin,bool MetadataOnly);
    private sealed class Scope(Access value) : IDisposable
    {
        private readonly Access? _before=Ambient.Value;
        private bool _disposed;
        internal void Enter()=>Ambient.Value=value;
        public void Dispose(){if(!_disposed){_disposed=true;Ambient.Value=_before;}}
    }
    private IDisposable Enter(bool metadata,OperationPin? pin=null){var s=new Scope(new(this,pin,metadata));s.Enter();return s;}
    internal sealed class OperationPin : IDisposable
    {
        private readonly GmSessionRunCoordinator _owner;
        private int _references=1;
        internal OperationPin(GmSessionRunCoordinator owner){_owner=owner;}
        internal void Retain(){lock(_owner._sync){if(_references==0)throw GmSessionRunPersistence.Invalid();_references++;}}
        internal void Validate(GmSessionRunCoordinator owner){lock(_owner._sync){if(_references==0 || !ReferenceEquals(_owner,owner))throw GmSessionRunPersistence.Invalid();}}
        public void Dispose(){lock(_owner._sync){if(_references==0)return;if(--_references==0 && --_owner._pins==0)_owner._drained.TrySetResult();}}
    }
    internal async Task<T> RunOperationAsync<T>(Func<Task<T>> operation)
    {
        OperationPin pin;
        lock(_sync)
        {
            if(_closed || _uncertain || !_released || _retired || _persistence.HasDebt || _record?.Disposition!=GmSessionRunDisposition.Running)
                throw GmSessionRunPersistence.Invalid();
            if(_pins++==0)_drained=new(TaskCreationOptions.RunContinuationsAsynchronously);pin=new(this);
        }
        using(pin)using(Enter(false,pin))return await operation();
    }
    internal void ValidateAccessAcquisition(Access access,bool closing=false)
    {
        _guard.Validate();if(!ReferenceEquals(access.Owner,this) || _retired)throw GmSessionRunPersistence.Invalid();
        if(access.Pin!=null && !access.MetadataOnly && AdmissionClosed && !closing)throw GmSessionRunPersistence.Invalid();
        access.Pin?.Validate(this);
    }
    internal void ValidateAccess(Access access,bool finalization)
    {
        _guard.Validate();if(!ReferenceEquals(access.Owner,this) || _retired)throw GmSessionRunPersistence.Invalid();
        if(access.MetadataOnly)return; // actual internal scope; never ordinary mutation authority
        if(access.Pin==null)
        {
            // Original start's recovery admission is quiescent only, before Prepared.
            if(_record!=null && _record.Disposition!=GmSessionRunDisposition.Stopped || _persistence.HasDebt)throw GmSessionRunPersistence.Invalid();return;
        }
        access.Pin.Validate(this);
        var bytes=GmSessionRunPersistence.Read(_files.BasePath);
        if(bytes==null || _acknowledged==null || !bytes.AsSpan().SequenceEqual(_acknowledged))throw GmSessionRunPersistence.Invalid();
        if(finalization && _closed && _record?.Disposition is GmSessionRunDisposition.Stopping or GmSessionRunDisposition.Uncertain)return;
        if(!_released || _uncertain || _persistence.HasDebt || _record?.Disposition!=GmSessionRunDisposition.Running ||
            _terminal?.AuthorityLost.IsCompleted==true || _terminal?.RootExited.IsCompleted==true)throw GmSessionRunPersistence.Invalid();
    }
    internal async Task<IOwnedTerminalSession> LaunchNeutralAsync(NeutralTerminalLaunch launch,CancellationToken token,Action<int>? held=null)
    {
        using var original=Enter(false);
        try
        {
        await using var lifecycle=await _files.AcquireSessionLifecycleLeaseAsync();
        await using(var lease=await _files.AcquireCanonicalWriteLeaseAsync(cancellationToken:token))
        {
            var generation=_files.GetOrCreateSessionGeneration(lease);
            var boot=ReadBootIdentity();
            var identity=new GmSessionRunIdentity(_files.BasePath,Guid.NewGuid().ToString("N"),generation,
                _record==null?1:checked(_record.Identity.Epoch+1),GmSessionRunBackend.LinuxSupervisor,Guid.NewGuid().ToString("N"),boot);
            Publish(new(1,identity,GmSessionRunDisposition.Prepared,null));
        }
            var prepared=await OwnedTerminalSessionFactory.PrepareNeutralAsync(launch,Identity.RunId,token,held);
            _terminal=prepared.Session;
            using(Enter(true))await using(var lease=await _files.AcquireMainMetadataLeaseAsync())
            {
                if(_terminal.Identity.RunId!=Identity.RunId || _terminal.AuthorityLost.IsCompleted || _terminal.RootExited.IsCompleted)throw GmSessionRunPersistence.Invalid();
                RequireOriginalLaunchGeneration(lease);
                Publish(GmSessionRunTransitions.MarkRunning(_record!,Identity));
                RequireOriginalLaunchGeneration(lease);
            }
            // No canonical/transition lock spans release. The original held adapter
            // checks pidfd/liveness and consumes its one release even if ACK is lost.
            await prepared.ReleaseAsync(token);lock(_sync)_released=true;return _terminal;
        }
        catch(OwnedTerminalStartException ex){_terminal=ex.Owner;NotifyUncertain();throw;}
        catch(Exception ex)
        {
            if(_terminal==null && _planned==null && !_persistence.HasDebt && _record?.Disposition is null or GmSessionRunDisposition.Stopped) {
                // Failure before any Prepared publication/creation: release this
                // exact quiescent guard after actual leases have unwound.
                lock(RootIdentity.WorkerContextGate){if(RootIdentity.MainCoordinator!=this)throw GmSessionRunPersistence.Invalid();RootIdentity.MainCoordinator=null;}
                _retired=true;_guard.Dispose();throw;
            }
            NotifyUncertain();if(_terminal!=null)throw new OwnedTerminalStartException(_terminal,ex);throw;
        }
    }
    private static string ReadBootIdentity()
    {
        using var file=File.OpenRead("/proc/sys/kernel/random/boot_id");
        // proc files may report zero length; bound the actual stream instead.
        var bytes=new byte[129];var n=0;while(n<bytes.Length){var count=file.Read(bytes,n,bytes.Length-n);if(count==0)break;n+=count;}
        if(n is <1 or >128)throw GmSessionRunPersistence.Invalid();
        var boot=System.Text.Encoding.ASCII.GetString(bytes,0,n).Trim();
        if(!Guid.TryParseExact(boot,"D",out _))throw GmSessionRunPersistence.Invalid();return boot;
    }
    private void RequireOriginalLaunchGeneration(FileSystemManager.CanonicalWriteLease lease)
    {
        if(_files.ReadLocalGenerationSnapshotBelowWorkerFence(lease).Binding!=TrustedLocalGeneration.Existing(Identity.GenerationId))
            throw GmSessionRunPersistence.Invalid();
    }
    private void Publish(GmSessionRunRecord next)
    {
        if(_planned!=null)throw GmSessionRunPersistence.Invalid();_planned=next;
        _persistence.Publish(_acknowledged,next);Acknowledge();
    }
    private void Acknowledge(){_record=_planned??throw GmSessionRunPersistence.Invalid();_acknowledged=GmSessionRunRecordCodec.Encode(_record);_planned=null;}
    internal void NotifyUncertain(){lock(_sync){_uncertain=true;_closed=true;}}
    internal async Task BeginStopAsync()
    {
        lock(_sync)_closed=true;
        await _transitions.WaitAsync();
        try
        {
            using(Enter(true))await using(var lease=await _files.AcquireMainMetadataLeaseAsync())
            {
                if(_persistence.HasDebt){_persistence.Retry();Acknowledge();} // exact metadata only; never release replay
                if(_record==null || _record.Disposition==GmSessionRunDisposition.Stopped)return;
                var next=_uncertain?GmSessionRunTransitions.MarkUncertain(_record):
                    _record.Disposition==GmSessionRunDisposition.Stopping?_record:GmSessionRunTransitions.MarkStopping(_record,Identity);
                if(next!=_record)Publish(next);
            }
        }
        finally{_transitions.Release();}
        if(_terminal==null)throw GmSessionRunPersistence.Invalid(); // retained no-child uncertainty cannot claim retirement
        Task drain;lock(_sync)drain=_drained.Task;
        try {await drain.WaitAsync(TimeSpan.FromSeconds(5));}
        catch(TimeoutException){NotifyUncertain();throw;} // no filesystem locks across pin drain
        using(Enter(true))if(_stopLifecycle==null)_stopLifecycle=await _files.AcquireSessionLifecycleLeaseAsync();
    }
    internal async Task ConfirmSettledStopAsync(IOwnedTerminalSession original,TerminalStopEvidence proof)
    {
        var binding=_retirement;
        var drain=binding?.InputDrain();
        // Caller evidence alone cannot mint retirement. Require actual registered
        // original managed tasks, original adapter's proof object and disposal.
        if(!ReferenceEquals(original,_terminal) || binding==null || !ReferenceEquals(binding.Session,original) ||
            drain?.IsCompleted!=true || binding.Loops.Any(t=>!t.IsCompleted) || _stopLifecycle?.IsActive!=true)
            throw GmSessionRunPersistence.Invalid();
        var actual=await original.StopAndObserveAsync(CancellationToken.None);
        if(!ReferenceEquals(proof,actual))throw GmSessionRunPersistence.Invalid();
        try {await drain;await Task.WhenAll(binding.Loops);await original.DisposeAsync();}
        catch{NotifyUncertain();throw;}
        if(actual.Identity!=original.Identity || actual.Identity.RunId!=Identity.RunId ||
            actual.State!=GmWorkerStopState.StoppedWithinScope || !actual.CleanupComplete || actual.AuthorityRetained ||
            original.AuthorityLost.IsCompleted || !original.RootExited.IsCompletedSuccessfully)
        {NotifyUncertain();throw GmSessionRunPersistence.Invalid();}
        // The original proof and actual settlement survive metadata ACK debt.
        _settledStop??=proof;
        lock(_sync)if(_uncertain || _pins!=0)throw GmSessionRunPersistence.Invalid();
        using(Enter(true))await using(var lease=await _files.AcquireMainMetadataLeaseAsync())
        {
            if(_persistence.HasDebt){_persistence.Retry();Acknowledge();}
            if(_record?.Disposition is not (GmSessionRunDisposition.Stopping or GmSessionRunDisposition.Stopped))throw GmSessionRunPersistence.Invalid();
            if(_record?.Disposition!=GmSessionRunDisposition.Stopped)
                Publish(GmSessionRunTransitions.ConfirmStopped(_record!,new(Identity,GmSessionRunStopKind.OwnedScopeEmpty,Identity.BootId)));
        }
        if(original.AuthorityLost.IsCompleted || !original.RootExited.IsCompletedSuccessfully) {
            NotifyUncertain();throw GmSessionRunPersistence.Invalid();
        }
        await _stopLifecycle.DisposeAsync();_stopLifecycle=null;
        lock(_sync){if(_uncertain)throw GmSessionRunPersistence.Invalid();_retired=true;}
        lock(RootIdentity.WorkerContextGate){if(RootIdentity.MainCoordinator!=this)throw GmSessionRunPersistence.Invalid();RootIdentity.MainCoordinator=null;}
        _guard.Dispose();
    }
}
