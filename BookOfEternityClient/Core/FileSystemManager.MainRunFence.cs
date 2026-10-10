using BookOfEternityClient.Services.GmRuntime;
using BookOfEternityClient.Services.GmWorkers;

namespace BookOfEternityClient.Core;

public partial class FileSystemManager
{
    // Registration is installed synchronously in the caller, just like canonical
    // ambient leases. Lifecycle/nested consumers borrow the original access; they
    // never re-lock the main guard from beneath a canonical/lifecycle lock.
    private static readonly AsyncLocal<MainAdmission?> MainAdmissions=new();
    internal void RequireLoadIpcOutsideFileScopes()
    {
        if(HasAmbientCanonicalLease() || SessionOperationContext.TryGetExpectedGeneration(BasePath,out _))
            throw new InvalidOperationException("Load IPC requires the original filesystem operation to close first.");
        for(var p=MainAdmissions.Value;p!=null;p=p.Parent)
            if(!p.Closed && p.RootIdentity==CanonicalRootAuthorityIdentity)throw new InvalidOperationException("Load IPC cannot borrow a filesystem admission.");
    }
    internal MainAdmission BeginMainAdmission()=>BeginMainAdmission(false);
    internal MainAdmission BeginParticipatingMainAdmission()=>BeginMainAdmission(true);
    private MainAdmission BeginMainAdmission(bool participating)
    {
        var parent=MainAdmissions.Value;
        for(var ancestor=parent;ancestor!=null;ancestor=ancestor.Parent)
            if(ancestor.Closed && ancestor.OwnsRemote && ancestor.RootIdentity==CanonicalRootAuthorityIdentity)throw GmSessionRunPersistence.Invalid();
        while(parent is { Closed:true })parent=parent.Parent;
        var frame=new MainAdmission(this,parent,GmSessionRunCoordinator.Current,participating);
        MainAdmissions.Value=frame;return frame;
    }
    internal sealed class MainAdmission : IDisposable,IAsyncDisposable
    {
        private readonly FileSystemManager _files;
        private readonly MainAdmission? _parent;
        private readonly GmSessionRunCoordinator.Access? _requested;
        private MainAccess? _access;
        private bool _closed,_ownsRemote;
        private GmMainOperationClient? _retainedRemote;
        private GmSessionRunCoordinator.Access? _retainedOriginal;
        private readonly bool _participating;
        private GmWorkerCanonicalPurpose? _workerPurpose;
        internal bool WasRemote {get;private set;}
        internal bool OwnsRemote=>_ownsRemote;
        internal MainOperationClose? TerminalClose=>_retainedRemote?.TerminalClose;
        internal bool CloseObserved=>_retainedRemote?.CloseObserved==true;
        internal MainOperationClose? DescribeClose(MainOperationOutcome outcome,bool closingFailed)=>_retainedRemote?.DescribeClose(outcome,closingFailed);
        internal string Root=>_files.BasePath;
        internal CanonicalRootIdentity RootIdentity=>_files.CanonicalRootAuthorityIdentity;
        internal bool Closed=>_closed;
        internal MainAdmission? Parent=>_parent;
        internal MainAdmission(FileSystemManager files,MainAdmission? parent,GmSessionRunCoordinator.Access? requested,bool participating)
        {_files=files;_parent=parent;_requested=requested;_participating=participating;}
        internal async Task AcquireAsync(CancellationToken token=default,bool closing=false,bool quiescentOnly=false, GmWorkerCanonicalPurpose? workerPurpose=null)
        {
            _workerPurpose=workerPurpose;
            // Finalization borrows an actual original closing frame. The purpose
            // value alone cannot bypass admission or acquire recovery authority.
            if(closing && !BoundClosing)throw GmSessionRunPersistence.Invalid();
            var browserOriginal=closing?null:_files.PreflightBrowserOriginalAdmission(this);
            if(quiescentOnly && _requested?.Pin!=null)throw GmSessionRunPersistence.Invalid();
            if(_requested?.Owner.RootIdentity==_files.CanonicalRootAuthorityIdentity)_requested.Owner.ValidateAccessAcquisition(_requested,closing);
            for(var p=_parent;p!=null;p=p._parent)
                if(!p._closed && p._files.CanonicalRootAuthorityIdentity==_files.CanonicalRootAuthorityIdentity && p._requested==_requested && p._access is { } borrowed)
                {
                    if(quiescentOnly && (borrowed.Remote!=null || borrowed.Original?.Pin!=null))throw GmSessionRunPersistence.Invalid();
                    borrowed.Remote?.Validate(_files.BasePath,closing && borrowed.Remote.Closing);
                    if(_files._hooks?.BeforeMainBorrowRetainAsync is { } hook)await hook();
                    _access=borrowed.Retain();WasRemote=_access.Remote!=null;_retainedRemote=_access.Remote;_retainedOriginal=_access.Original;return;}
            if(_requested!=null && _requested.Owner.RootIdentity==_files.CanonicalRootAuthorityIdentity)
            {_requested.Pin?.Retain();_access=new(null,_requested);_retainedOriginal=_requested;return;}
            // This read can only refuse. Absent/Stopped still requires the original
            // physical guard and validation below, including a pending Stopped ACK.
            var observed=GmSessionRunPersistence.Read(_files.BasePath);
            if(observed!=null && GmSessionRunRecordCodec.Decode(observed).Disposition!=GmSessionRunDisposition.Stopped) {
                if(!_participating || closing)throw GmSessionRunPersistence.Invalid();
                if(OperatingSystem.IsWindows())_files.RequireAbsentMainWorkerInventory();
                var knownWorkers=!OperatingSystem.IsWindows() && _files.HasKnownMainWorkerInventory();
                var remote=await GmMainOperationClient.OpenAsync(_files,token,browserOriginal?.ActiveIdentity);
                _access=new(null,null,remote,workerObservationRequired:knownWorkers);_retainedRemote=remote;_ownsRemote=true;WasRemote=true;return;
            }
            _access=new(await GmMainOwnerGuard.AcquireAsync(_files.BasePath,token,
                _files._hooks?.MainOwnerLockContendedAsync,CanonicalWriteLockRetryCount,TransientFileAccessRetryDelay),null);
            try{
                // A known inventory requires its original durable coordinator,
                // even with disabled helpers, before canonical recovery/leases.
                if(OperatingSystem.IsWindows())_files.RequireAbsentMainWorkerInventory();
                else if(_files.HasKnownMainWorkerInventory()) {
                    var workers=GmWorkerRootContext.Attach(_files,true,null);
                    _access.Workers=workers;workers.ValidateMainAdmission(_files,_workerPurpose);
                }
                Validate(null);
            }catch{Dispose();throw;}
        }
        internal bool Acquired=>_access!=null && !_closed;
        internal bool SharesAccess(MainAdmission other)=>_access!=null && ReferenceEquals(_access,other._access);
        internal BrowserOriginalMainCondition CaptureBrowserCondition(string generation)
        {
            Validate(null);
            if(MetadataOnly)throw BrowserOriginalMainCondition.Invalid();
            var active=_access?.Remote?.Identity ?? (_access?.Original?.Pin!=null?_access.Original.Owner.Identity:null);
            BrowserOriginalMainCondition condition;
            if(active!=null)condition=new("active",_files.BasePath,generation,active);
            else {
                if(_access?.Guard==null)throw BrowserOriginalMainCondition.Invalid();
                _access.Guard.Validate();
                var bytes=GmSessionRunPersistence.Read(_files.BasePath);
                var stopped=bytes==null?null:GmSessionRunRecordCodec.Decode(bytes);
                condition=stopped==null?new("quiescentAbsent",_files.BasePath,generation):new("quiescentStopped",_files.BasePath,generation,StoppedRecord:stopped);
            }
            condition.RequireCurrent(_files);return condition;
        }
        internal GmSessionRunCoordinator.Access? Original=>_access?.Original;
        internal bool MetadataOnly=>_requested?.MetadataOnly==true;
        internal bool Closing=>(_requested?.Pin!=null && _requested.Owner.AdmissionClosed) || _access?.Remote?.Closing==true;
        internal string? ActiveGeneration=>_access?.Remote?.Identity.GenerationId ?? (_access?.Original?.Pin!=null?_access.Original.Owner.Identity.GenerationId:null);
        internal void MarkUnresolved(){_retainedRemote?.Abort();_retainedOriginal?.Owner.NotifyUncertain();}
        private bool _boundClosing;
        internal bool BoundClosing => _boundClosing ||
            _parent is { Closed: false } && _parent.RootIdentity == RootIdentity && _parent.BoundClosing;
        internal void BeginClosing(){_boundClosing=true;_access?.Remote?.BeginClosing();}
        internal async Task CompleteAsync(MainOperationOutcome outcome,bool closingFailed)
        {
            if(!_ownsRemote)return;
            if(_access==null)throw GmSessionRunPersistence.Invalid();
            _access.FreezeForTerminalClose();
            await _access.Remote!.CompleteAsync(outcome,closingFailed);
        }
        internal void Validate(CanonicalWriteLease? lease)
        {
            if(_closed || _access==null)throw GmSessionRunPersistence.Invalid();
            if(lease!=null && !ReferenceEquals(lease.WorkerPurpose,_workerPurpose))throw GmSessionRunPersistence.Invalid();
            if(lease?.Purpose==CanonicalWritePurpose.SessionFinalization && !BoundClosing)
                throw GmSessionRunPersistence.Invalid();
            if(_access.Remote is { } remote) {
                if(lease?.Purpose==CanonicalWritePurpose.SessionReplacement)throw GmSessionRunPersistence.Invalid();
                remote.Validate(_files.BasePath,lease?.Purpose==CanonicalWritePurpose.SessionFinalization);
                if(OperatingSystem.IsWindows() && !(Closing && lease?.Purpose==CanonicalWritePurpose.SessionFinalization))
                    _files.RequireAbsentMainWorkerInventory();
                if(_access.WorkerObservationRequired && !(Closing && lease?.Purpose==CanonicalWritePurpose.SessionFinalization)) {
                    // This observation can only withdraw an already granted
                    // original connection. It cannot grant/replace inventory authority.
                    var observed=GmWorkerRunLedger.ObserveAsync(new(_files.BasePath)).GetAwaiter().GetResult();
                    if(observed.Kind!=WorkerRunObservationKind.Quiescent)throw GmSessionRunPersistence.Invalid();
                }
                if(lease!=null && _files.ReadLocalGenerationSnapshotBelowWorkerFence(lease).Binding.Id!=remote.Identity.GenerationId)throw GmSessionRunPersistence.Invalid();
                return;
            }
            if(_access.Original is { } a)
            {
                if(a.Pin!=null && lease?.Purpose==CanonicalWritePurpose.SessionReplacement)throw GmSessionRunPersistence.Invalid();
                a.Owner.ValidateAccess(a,lease?.Purpose==CanonicalWritePurpose.SessionFinalization);
                if(a.Pin!=null && lease!=null &&
                    _files.ReadLocalGenerationSnapshotBelowWorkerFence(lease).Binding.Id!=a.Owner.Identity.GenerationId)
                    throw GmSessionRunPersistence.Invalid();
                return;
            }
            _access.Guard!.Validate();
            if(OperatingSystem.IsWindows())_files.RequireAbsentMainWorkerInventory();
            _access.Workers?.ValidateMainAdmission(_files,_workerPurpose);
            var bytes=GmSessionRunPersistence.Read(_files.BasePath);
            if(bytes==null)return;
            var r=GmSessionRunRecordCodec.Decode(bytes);
            var backend=OperatingSystem.IsWindows()?GmSessionRunBackend.WindowsJob:GmSessionRunBackend.LinuxSupervisor;
            if(r.Disposition!=GmSessionRunDisposition.Stopped || r.Identity.Backend!=backend ||
                !GmSessionRunValidation.AdmissionRootMatches(r.Identity.RootKey,_files.BasePath,backend))throw GmSessionRunPersistence.Invalid();
        }
        public async ValueTask DisposeAsync()
        {
            var remote=_ownsRemote?_access?.Remote:null;
            Dispose();if(remote!=null)await remote.DisposeAsync();
        }
        public void Dispose()
        {
            if(_closed)return;_closed=true;_access?.Dispose();_access=null;
            if(MainAdmissions.Value==this)MainAdmissions.Value=_parent;
        }
    }
    private sealed class MainAccess(GmMainOwnerGuard? guard,GmSessionRunCoordinator.Access? original,GmMainOperationClient? remote=null,bool workerObservationRequired=false) : IDisposable
    {
        private readonly object _state = new();
        private int _references=1;
        private bool _frozen;
        internal GmWorkerRootContext? Workers;
        internal bool WorkerObservationRequired=>workerObservationRequired;
        internal GmMainOperationClient? Remote=>remote;
        internal GmMainOwnerGuard? Guard=>guard;
        internal GmSessionRunCoordinator.Access? Original=>original;
        internal MainAccess Retain()
        {
            lock(_state)
            {
                if(_frozen || _references==0)throw GmSessionRunPersistence.Invalid();
                _references++;return this;
            }
        }
        internal void FreezeForTerminalClose()
        {
            lock(_state)
            {
                _frozen=true;
                if(_references!=1)throw new IOException("Original operation still owns filesystem leases; close is unconfirmed.");
            }
        }
        public void Dispose()
        {
            bool release;
            lock(_state) { if(_references==0)return;release=--_references==0; }
            if(release){Workers?.ReleaseClient();guard?.Dispose();original?.Pin?.Dispose();}
        }
    }
    // Negative admission is observed without creating or adopting a worker
    // ledger. The Windows main route cannot reinterpret a Linux-only inventory.
    internal void RequireAbsentMainWorkerInventory()
    {
        var root=CanonicalRootAuthorityIdentity;
        lock(root.WorkerContextGate) {
            if(root.WorkerContext!=null)throw GmSessionRunPersistence.Invalid();
            var observed=new TrustedLocalFileScope([BasePath]).ObserveNamespace(new WorkerLedgerTarget(BasePath).DirectoryPath);
            if(observed.BlockingFileAncestor!=null || observed.Kind!=TrustedLocalNamespaceKind.Missing)
                throw GmSessionRunPersistence.Invalid();
        }
    }
    private bool HasKnownMainWorkerInventory()
    {
        var observed=new TrustedLocalFileScope([BasePath]).ObserveNamespace(new WorkerLedgerTarget(BasePath).DirectoryPath);
        if(observed.BlockingFileAncestor!=null || observed.Kind==TrustedLocalNamespaceKind.File)
            throw GmSessionRunPersistence.Invalid();
        return observed.Kind!=TrustedLocalNamespaceKind.Missing;
    }
    private void EnsureMainMutationAllowed(CanonicalWriteLease lease)
    {
        if(lease.MainAdmission==null)throw GmSessionRunPersistence.Invalid();
        if(lease.MainAdmission.MetadataOnly || lease.Purpose==CanonicalWritePurpose.SessionFinalization)
            throw GmSessionRunPersistence.Invalid();
        lease.MainAdmission.Validate(lease);
    }
    internal MainOperationClose? DescribeMainOperationClose(MainOperationOutcome outcome,bool closingFailed)
    {
        for(var p=MainAdmissions.Value;p!=null;p=p.Parent)if(p.RootIdentity==CanonicalRootAuthorityIdentity && p.DescribeClose(outcome,closingFailed) is { } close)return close;
        return null;
    }
    internal void MarkMainOperationUnresolved()
    {
        for(var p=MainAdmissions.Value;p!=null;p=p.Parent)if(p.RootIdentity==CanonicalRootAuthorityIdentity){p.MarkUnresolved();return;}
    }
    internal void BeginMainOperationClosing()
    {
        for(var p=MainAdmissions.Value;p!=null;p=p.Parent)if(!p.Closed && p.RootIdentity==CanonicalRootAuthorityIdentity){p.BeginClosing();return;}
    }
    internal Task<CanonicalWriteLease> AcquireMainMetadataLeaseAsync()=>
        AcquireCanonicalWriteLeaseWithAmbientAsync(CanonicalWritePurpose.MainMetadata,CancellationToken.None);
    private void EnsureMainBeforeRecovery(CanonicalWriteLease lease)
    {
        lease.MainAdmission!.Validate(lease);
        if(lease.MainAdmission.MetadataOnly)throw GmSessionRunPersistence.Invalid();
        _ = PreflightBrowserOriginalAdmission(lease.MainAdmission);
        // Original active recovery must inspect generation-changing intent before
        // effects. The typed trusted-local reader handles in-generation journals.
        if(lease.MainAdmission.ActiveGeneration!=null)
            new TrustedLocalFilePublication(this,new TrustedLocalFileScope([BasePath])).ValidateMainRecoveryGeneration(lease);
    }
    internal void EnsureMainRecoveryGeneration(CanonicalWriteLease lease,TrustedLocalGeneration before,TrustedLocalGeneration after)
    {
        if(lease.MainAdmission?.ActiveGeneration is { } expected &&
            (before!=TrustedLocalGeneration.Existing(expected) || after!=before))
            throw GmSessionRunPersistence.Invalid();
    }
}
