using BookOfEternityClient.Services.GmRuntime;

namespace BookOfEternityClient.Core;

public partial class FileSystemManager
{
    // Registration is installed synchronously in the caller, just like canonical
    // ambient leases. Lifecycle/nested consumers borrow the original access; they
    // never re-lock the main guard from beneath a canonical/lifecycle lock.
    private static readonly AsyncLocal<MainAdmission?> MainAdmissions=new();
    internal MainAdmission BeginMainAdmission()
    {
        var parent=MainAdmissions.Value;while(parent is { Closed:true })parent=parent.Parent;
        var frame=new MainAdmission(this,parent,GmSessionRunCoordinator.Current);
        MainAdmissions.Value=frame;return frame;
    }
    internal sealed class MainAdmission : IDisposable
    {
        private readonly FileSystemManager _files;
        private readonly MainAdmission? _parent;
        private readonly GmSessionRunCoordinator.Access? _requested;
        private MainAccess? _access;
        private bool _closed;
        internal bool Closed=>_closed;
        internal MainAdmission? Parent=>_parent;
        internal MainAdmission(FileSystemManager files,MainAdmission? parent,GmSessionRunCoordinator.Access? requested)
        {_files=files;_parent=parent;_requested=requested;}
        internal async Task AcquireAsync(CancellationToken token=default,bool closing=false)
        {
            if(_requested?.Owner.RootIdentity==_files.CanonicalRootAuthorityIdentity)_requested.Owner.ValidateAccessAcquisition(_requested,closing);
            for(var p=_parent;p!=null;p=p._parent)
                if(!p._closed && p._files.CanonicalRootAuthorityIdentity==_files.CanonicalRootAuthorityIdentity && p._requested==_requested && p._access!=null)
                {_access=p._access.Retain();return;}
            if(_requested!=null && _requested.Owner.RootIdentity==_files.CanonicalRootAuthorityIdentity)
            {_requested.Pin?.Retain();_access=new(null,_requested);return;}
            // This read can only refuse. Absent/Stopped still requires the original
            // physical guard and validation below, including a pending Stopped ACK.
            var observed=GmSessionRunPersistence.Read(_files.BasePath);
            if(observed!=null && GmSessionRunRecordCodec.Decode(observed).Disposition!=GmSessionRunDisposition.Stopped)
                throw GmSessionRunPersistence.Invalid();
            _access=new(await GmMainOwnerGuard.AcquireAsync(_files.BasePath,token,
                _files._hooks?.MainOwnerLockContendedAsync,CanonicalWriteLockRetryCount,TransientFileAccessRetryDelay),null);
            try{Validate(null);}catch{Dispose();throw;}
        }
        internal GmSessionRunCoordinator.Access? Original=>_access?.Original;
        internal bool MetadataOnly=>_requested?.MetadataOnly==true;
        internal bool Closing=>_requested?.Pin!=null && _requested.Owner.AdmissionClosed;
        internal void Validate(CanonicalWriteLease? lease)
        {
            if(_closed || _access==null)throw GmSessionRunPersistence.Invalid();
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
            var bytes=GmSessionRunPersistence.Read(_files.BasePath);
            if(bytes==null)return;
            var r=GmSessionRunRecordCodec.Decode(bytes);
            var backend=OperatingSystem.IsWindows()?GmSessionRunBackend.WindowsJob:GmSessionRunBackend.LinuxSupervisor;
            if(r.Disposition!=GmSessionRunDisposition.Stopped || r.Identity.Backend!=backend ||
                !GmSessionRunValidation.RootMatches(r.Identity.RootKey,_files.BasePath,backend))throw GmSessionRunPersistence.Invalid();
        }
        public void Dispose()
        {
            if(_closed)return;_closed=true;_access?.Dispose();_access=null;
            if(MainAdmissions.Value==this)MainAdmissions.Value=_parent;
        }
    }
    private sealed class MainAccess(GmMainOwnerGuard? guard,GmSessionRunCoordinator.Access? original) : IDisposable
    {
        private int _references=1;
        internal GmMainOwnerGuard? Guard=>guard;
        internal GmSessionRunCoordinator.Access? Original=>original;
        internal MainAccess Retain(){Interlocked.Increment(ref _references);return this;}
        public void Dispose(){if(Interlocked.Decrement(ref _references)==0){guard?.Dispose();original?.Pin?.Dispose();}}
    }
    private void EnsureMainMutationAllowed(CanonicalWriteLease lease)
    {
        if(lease.MainAdmission==null)throw GmSessionRunPersistence.Invalid();
        if(lease.MainAdmission.MetadataOnly || (lease.MainAdmission.Closing && lease.Purpose==CanonicalWritePurpose.SessionFinalization))
            throw GmSessionRunPersistence.Invalid();
        lease.MainAdmission.Validate(lease);
    }
    internal Task<CanonicalWriteLease> AcquireMainMetadataLeaseAsync()=>
        AcquireCanonicalWriteLeaseWithAmbientAsync(CanonicalWritePurpose.MainMetadata,CancellationToken.None);
    private void EnsureMainBeforeRecovery(CanonicalWriteLease lease)
    {
        lease.MainAdmission!.Validate(lease);
        if(lease.MainAdmission.MetadataOnly)throw GmSessionRunPersistence.Invalid();
        // Original active recovery must inspect generation-changing intent before
        // effects. The typed trusted-local reader handles in-generation journals.
        if(lease.MainAdmission.Original?.Pin!=null)
            new TrustedLocalFilePublication(this,new TrustedLocalFileScope([BasePath])).ValidateMainRecoveryGeneration(lease);
    }
    internal void EnsureMainRecoveryGeneration(CanonicalWriteLease lease,TrustedLocalGeneration before,TrustedLocalGeneration after)
    {
        if(lease.MainAdmission?.Original is {Pin:not null} a &&
            (before!=TrustedLocalGeneration.Existing(a.Owner.Identity.GenerationId) || after!=before))
            throw GmSessionRunPersistence.Invalid();
    }
}
