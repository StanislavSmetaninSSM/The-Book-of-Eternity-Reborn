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
    /// <summary>
    /// Registers main admission synchronously in the caller's execution flow.
    /// </summary>
    /// <param name="participating">
    /// True permits joining an existing remote operation through its ordinary protocol.
    /// </param>
    /// <param name="worldIssuance">
    /// Exact read reservation issuing recovery, or null for ordinary admission.
    /// </param>
    /// <param name="physicalScope">
    /// True marks a physical lease frame that conflicts with a new read reservation.
    /// </param>
    /// <returns>
    /// A frame that must be acquired and disposed by its owner.
    /// </returns>
    private MainAdmission BeginMainAdmission(bool participating, ValidationReadReservation? worldIssuance = null, bool physicalScope = false)
    {
        var parent=MainAdmissions.Value;
        for (var p = parent; p != null; p = p.Parent)
            if (!p.Closed && p.RootIdentity == CanonicalRootAuthorityIdentity) p.RequireNoWorldReadConflict(worldIssuance);
        for(var ancestor=parent;ancestor!=null;ancestor=ancestor.Parent)
            if(ancestor.Closed && ancestor.OwnsRemote && ancestor.RootIdentity==CanonicalRootAuthorityIdentity)throw GmSessionRunPersistence.Invalid();
        while(parent is { Closed:true })parent=parent.Parent;
        var frame=new MainAdmission(this,parent,GmSessionRunCoordinator.Current,participating,worldIssuance,physicalScope);
        MainAdmissions.Value=frame;return frame;
    }
    internal sealed class MainAdmission : IDisposable,IAsyncDisposable
    {
        private readonly FileSystemManager _files;
        private readonly MainAdmission? _parent;
        private readonly GmSessionRunCoordinator.Access? _requested;
        private MainAccess? _access, _worldReservedAccess;
        private bool _closed,_ownsRemote;
        private GmMainOperationClient? _retainedRemote;
        private GmSessionRunCoordinator.Access? _retainedOriginal;
        private readonly bool _participating;
        private readonly ValidationReadReservation? _worldIssuance;
        internal readonly bool PhysicalScope;
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
        /// <summary>
        /// Binds an admission frame to its actual manager, parent and requested access.
        /// </summary>
        /// <param name="files">
        /// Manager issuing this frame.
        /// </param>
        /// <param name="parent">
        /// Calling-flow parent, or null for a root admission.
        /// </param>
        /// <param name="requested">
        /// Existing coordinator access, or null to use ordinary owner admission.
        /// </param>
        /// <param name="participating">
        /// Whether the ordinary remote participation protocol is permitted.
        /// </param>
        /// <param name="worldIssuance">
        /// Exact read reservation opening recovery, or null outside reserved acquisition.
        /// </param>
        /// <param name="physicalScope">
        /// Whether this frame owns a physical filesystem lease.
        /// </param>
        internal MainAdmission(FileSystemManager files,MainAdmission? parent,GmSessionRunCoordinator.Access? requested,bool participating, ValidationReadReservation? worldIssuance, bool physicalScope)
        {_files=files;_parent=parent;_requested=requested;_participating=participating;_worldIssuance=worldIssuance;PhysicalScope=physicalScope;}
        /// <summary>
        /// Acquires or retains the original main access through its existing ownership route.
        /// </summary>
        /// <param name="token">
        /// Cancellation for physical guard or remote acquisition.
        /// </param>
        /// <param name="closing">
        /// Whether this frame must borrow an actual bound closing operation.
        /// </param>
        /// <param name="quiescentOnly">
        /// Whether active original pins and remote operations must be refused.
        /// </param>
        /// <param name="workerPurpose">
        /// Existing worker purpose to retain, or <see langword="null"/> for ordinary admission.
        /// </param>
        /// <returns>
        /// Completion after this frame has acquired or retained and validated its original access.
        /// </returns>
        internal async Task AcquireAsync(CancellationToken token=default,bool closing=false,bool quiescentOnly=false, GmWorkerCanonicalPurpose? workerPurpose=null)
        {
            _workerPurpose=workerPurpose;
            // Finalization borrows an actual original closing frame. The purpose
            // value alone cannot bypass admission or acquire recovery authority.
            if(closing && !BoundClosing)throw GmSessionRunPersistence.Invalid();
            var browserOriginal=closing?null:_files.PreflightBrowserOriginalAdmission(this,diagnosticOrigin:"main-admission-acquire");
            // A quiescent witness cannot become a new active grant while opening.
            browserOriginal?.RequireCurrent(_files);
            if(browserOriginal!=null && _files._hooks?.AfterBrowserOriginalPreflightAsync is { } originalHook)
                await originalHook();
            if(quiescentOnly && _requested?.Pin!=null)throw GmSessionRunPersistence.Invalid();
            if(_requested?.Owner.RootIdentity==_files.CanonicalRootAuthorityIdentity)_requested.Owner.ValidateAccessAcquisition(_requested,closing);
            for(var p=_parent;p!=null;p=p._parent)
                if(!p._closed && p._files.CanonicalRootAuthorityIdentity==_files.CanonicalRootAuthorityIdentity && p._requested==_requested && p._access is { } borrowed)
                {
                    if(quiescentOnly && (borrowed.Remote!=null || borrowed.Original?.Pin!=null))throw GmSessionRunPersistence.Invalid();
                    borrowed.Remote?.Validate(_files.BasePath,closing && borrowed.Remote.Closing);
                    if(_files._hooks?.BeforeMainBorrowRetainAsync is { } hook)await hook();
                    _access=borrowed.Retain(this,_worldIssuance);WasRemote=_access.Remote!=null;_retainedRemote=_access.Remote;_retainedOriginal=_access.Original;return;}
            if(_requested!=null && _requested.Owner.RootIdentity==_files.CanonicalRootAuthorityIdentity)
            {_requested.Pin?.Retain();_access=new(this,null,_requested);_retainedOriginal=_requested;return;}
            // This read can only refuse. Absent/Stopped still requires the original
            // physical guard and validation below, including a pending Stopped ACK.
            var observed=GmSessionRunPersistence.Read(_files.BasePath);
            if(observed!=null && GmSessionRunRecordCodec.Decode(observed).Disposition!=GmSessionRunDisposition.Stopped) {
                if(browserOriginal!=null && browserOriginal.ActiveIdentity==null)throw BrowserOriginalMainCondition.Invalid();
                if(!_participating || closing)throw GmSessionRunPersistence.Invalid();
                if(OperatingSystem.IsWindows())_files.RequireAbsentMainWorkerInventory();
                var knownWorkers=!OperatingSystem.IsWindows() && _files.HasKnownMainWorkerInventory();
                var remote=await GmMainOperationClient.OpenAsync(_files,token,browserOriginal?.ActiveIdentity);
                _access=new(this,null,null,remote,workerObservationRequired:knownWorkers);_retainedRemote=remote;_ownsRemote=true;WasRemote=true;return;
            }
            var mainOwnerContended = _files._hooks?.MainOwnerLockContendedAsync;
            _access=new(this,await GmMainOwnerGuard.AcquireAsync(_files.BasePath,token,
                mainOwnerContended == null ? null : () => _files.InvokeRecoveryValidationCallbackAsync(mainOwnerContended),
                CanonicalWriteLockRetryCount,TransientFileAccessRetryDelay),null);
            try{
                // A known inventory requires its original durable coordinator,
                // even with disabled helpers, before canonical recovery/leases.
                if(OperatingSystem.IsWindows())_files.RequireAbsentMainWorkerInventory();
                else if(_files.HasKnownMainWorkerInventory()) {
                    var workers=GmWorkerRootContext.Attach(_files,true,null);
                    _access.Workers=workers;workers.ValidateMainAdmission(_files,_workerPurpose);
                }
                Validate(null);
                browserOriginal?.RequireCurrent(_files);
            }catch{Dispose();throw;}
        }
        internal bool Acquired=>_access!=null && !_closed;
        internal bool SharesAccess(MainAdmission other)=>_access!=null && ReferenceEquals(_access,other._access);
        /// <summary>
        /// Reserves this frame's actual shared access for one validation read lifetime.
        /// </summary>
        /// <param name="scope">
        /// New reservation checked against existing borrowers and owner ancestry.
        /// </param>
        internal void ReserveWorldRead(ValidationReadReservation scope)
        {
            var access = _access;
            if (_closed || access == null) throw BrowserOriginalMainCondition.Invalid();
            var ancestors = new HashSet<MainAdmission>();
            for (var p = this; p != null; p = p.Parent) ancestors.Add(p);
            access.ReserveWorldRead(scope, ancestors);
            _worldReservedAccess = access;
        }
        /// <summary>
        /// Atomically reserves available shared access without treating unsupported entry as a read failure.
        /// </summary>
        /// <param name="scope">
        /// New read identity retaining this frame's physical admission.
        /// </param>
        /// <returns>
        /// <see langword="true"/> if reserved; otherwise, <see langword="false"/> without changing shared state.
        /// </returns>
        internal bool TryReserveWorldRead(ValidationReadReservation scope)
        {
            var access = _access;
            if (_closed || access == null) return false;
            var ancestors = new HashSet<MainAdmission>();
            for (var p = this; p != null; p = p.Parent) ancestors.Add(p);
            if (!access.TryReserveWorldRead(scope, ancestors)) return false;
            _worldReservedAccess = access;
            return true;
        }
        /// <summary>
        /// Releases this frame's original read reservation.
        /// </summary>
        /// <param name="scope">
        /// Original reservation to close; another identity cannot release shared access.
        /// </param>
        internal void ReleaseWorldRead(ValidationReadReservation scope)
        {
            var access = _worldReservedAccess;
            access?.ReleaseWorldRead(scope, () => _worldReservedAccess = null);
        }
        /// <summary>
        /// Refuses a conflicting borrower of the reserved access.
        /// </summary>
        /// <param name="issuance">
        /// Exact opening reservation, or null to require unreserved access.
        /// </param>
        internal void RequireNoWorldReadConflict(ValidationReadReservation? issuance = null) => _access?.RequireNoWorldReadConflict(issuance);
        /// <summary>
        /// Original reservation during recovery acquisition, otherwise null.
        /// </summary>
        internal ValidationReadReservation? WorldRecoveryIssuance => _worldIssuance is { Opening: true } ? _worldIssuance : null;
        internal void RequireNoWorldMutation() => _access?.RequireNoWorldReadConflict(WorldRecoveryIssuance);
        /// <summary>
        /// Registers one mutation on this actual access before its first awaited or physical effect.
        /// </summary>
        /// <returns>
        /// An independent token retained until the operation and its cleanup end.
        /// </returns>
        internal MainMutationReservation BeginMutation() =>
            (_access ?? throw new InvalidOperationException("Mutation admission is no longer active."))
                .BeginMutation(this, WorldRecoveryIssuance);

        /// <summary>
        /// Captures a root validation version through this frame's original physical access without filesystem checks.
        /// The caller retains the root worker-context gate for this capture.
        /// </summary>
        /// <param name="lease">
        /// Original ordinary lease whose manager and physical admission must remain unchanged and active.
        /// </param>
        /// <param name="compatibleRead">
        /// Exact healthy borrowed read reservation established by the caller, or <see langword="null"/> outside a read set.
        /// </param>
        /// <param name="version">
        /// Current root identity on success, or <see langword="null"/> when this access cannot support reuse.
        /// </param>
        /// <returns>
        /// <see langword="true"/> if this original access is eligible and no root mutation is active; otherwise, <see langword="false"/>.
        /// </returns>
        internal bool TryCaptureRecoveryValidationVersion(CanonicalWriteLease lease,
            ValidationReadReservation? compatibleRead, out object? version)
        {
            var access = _access;
            version = null;
            var found = false;
            for (var current = MainAdmissions.Value; current != null; current = current.Parent)
            {
                if (current.RootIdentity == RootIdentity && (current.Closed || current.Closing || current.BoundClosing))
                    return false;
                if (ReferenceEquals(current, this)) found = true;
            }
            return found && access != null &&
                access.TryCaptureRecoveryValidationVersion(this, _files, lease, compatibleRead, out version);
        }

        /// <summary>
        /// Starts physical teardown atomically with read reservation, before detaching any lease resource.
        /// </summary>
        internal void BeginPhysicalLeaseClose() => _access?.BeginPhysicalLeaseClose(this, _worldIssuance);
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
        /// <summary>
        /// Starts original operation close only after its pending-snapshot read reservation has ended.
        /// </summary>
        internal void BeginClosing()
        {
            if (_access is { } access) access.BeginOwnerClosing(this, () => _boundClosing = true);
            else _boundClosing = true;
            _access?.Remote?.BeginClosing();
        }
        internal async Task CompleteAsync(MainOperationOutcome outcome,bool closingFailed)
        {
            if(!_ownsRemote)return;
            if(_access==null)throw GmSessionRunPersistence.Invalid();
            _access.FreezeForTerminalClose();
            await _access.Remote!.CompleteAsync(outcome,closingFailed);
        }
        /// <summary>
        /// Checks this main admission's existing remote, original-owner or guarded persisted-state route.
        /// </summary>
        /// <param name="lease">
        /// The current lease, or <see langword="null"/> for the existing lease-free checks; lease-specific generation checks remain conditional.
        /// </param>
        internal void Validate(CanonicalWriteLease? lease)
        {
            if(_closed || _access==null)throw GmSessionRunPersistence.Invalid();
            if(lease!=null && !ReferenceEquals(lease.WorkerPurpose,_workerPurpose))throw GmSessionRunPersistence.Invalid();
            if(lease?.Purpose==CanonicalWritePurpose.SessionFinalization && !BoundClosing)
                throw GmSessionRunPersistence.Invalid();
            if(_access.Remote is { } remote) {
                using var remoteTiming = _files.MeasureOperation("MainAdmission.Validate.Remote");
                if(lease?.Purpose==CanonicalWritePurpose.SessionReplacement)throw GmSessionRunPersistence.Invalid();
                using (_files.MeasureOperation("MainAdmission.Validate.RemoteCheck"))
                    remote.Validate(_files.BasePath,lease?.Purpose==CanonicalWritePurpose.SessionFinalization);
                if(OperatingSystem.IsWindows() && !(Closing && lease?.Purpose==CanonicalWritePurpose.SessionFinalization))
                    using (_files.MeasureOperation("MainAdmission.Validate.WorkerInventory"))
                        _files.RequireAbsentMainWorkerInventory();
                if(_access.WorkerObservationRequired && !(Closing && lease?.Purpose==CanonicalWritePurpose.SessionFinalization)) {
                    // This observation can only withdraw an already granted
                    // original connection. It cannot grant/replace inventory authority.
                    using var observationTiming = _files.MeasureOperation("MainAdmission.Validate.WorkerObservation");
                    var observed=GmWorkerRunLedger.ObserveAsync(new(_files.BasePath)).GetAwaiter().GetResult();
                    if(observed.Kind!=WorkerRunObservationKind.Quiescent)throw GmSessionRunPersistence.Invalid();
                }
                if(lease!=null)
                    using (_files.MeasureOperation("MainAdmission.Validate.GenerationRead"))
                        if(_files.ReadLocalGenerationSnapshotBelowWorkerFence(lease).Binding.Id!=remote.Identity.GenerationId)throw GmSessionRunPersistence.Invalid();
                return;
            }
            if(_access.Original is { } a)
            {
                using var originalTiming = _files.MeasureOperation("MainAdmission.Validate.OriginalOwner");
                if(a.Pin!=null && lease?.Purpose==CanonicalWritePurpose.SessionReplacement)throw GmSessionRunPersistence.Invalid();
                using (_files.MeasureOperation("MainAdmission.Validate.OriginalOwnerCheck"))
                    a.Owner.ValidateAccess(a,lease?.Purpose==CanonicalWritePurpose.SessionFinalization);
                if(a.Pin!=null && lease!=null)
                    using (_files.MeasureOperation("MainAdmission.Validate.GenerationRead"))
                        if(_files.ReadLocalGenerationSnapshotBelowWorkerFence(lease).Binding.Id!=a.Owner.Identity.GenerationId)
                            throw GmSessionRunPersistence.Invalid();
                return;
            }
            using var guardTiming = _files.MeasureOperation("MainAdmission.Validate.Guard");
            using (_files.MeasureOperation("MainAdmission.Validate.GuardCheck"))
                _access.Guard!.Validate();
            if(OperatingSystem.IsWindows())
                using (_files.MeasureOperation("MainAdmission.Validate.WorkerInventory"))
                    _files.RequireAbsentMainWorkerInventory();
            if(_access.Workers is { } workers)
                using (_files.MeasureOperation("MainAdmission.Validate.WorkerObservation"))
                    workers.ValidateMainAdmission(_files,_workerPurpose);
            byte[]? bytes;
            using (_files.MeasureOperation("MainAdmission.Validate.PersistenceRead"))
                bytes=GmSessionRunPersistence.Read(_files.BasePath);
            if(bytes==null)return;
            GmSessionRunRecord r;
            using (_files.MeasureOperation("MainAdmission.Validate.DecodePersistence"))
                r=GmSessionRunRecordCodec.Decode(bytes);
            var backend=OperatingSystem.IsWindows()?GmSessionRunBackend.WindowsJob:GmSessionRunBackend.LinuxSupervisor;
            if(r.Disposition!=GmSessionRunDisposition.Stopped || r.Identity.Backend!=backend ||
                !GmSessionRunValidation.AdmissionRootMatches(r.Identity.RootKey,_files.BasePath,backend))throw GmSessionRunPersistence.Invalid();
        }
        public async ValueTask DisposeAsync()
        {
            var remote=_ownsRemote?_access?.Remote:null;
            Dispose();if(remote!=null)await remote.DisposeAsync();
        }
        /// <summary>
        /// Releases this frame, refusing disposal of an original owner with an active pending-snapshot reservation.
        /// </summary>
        public void Dispose()
        {
            if(_closed)return;
            try
            {
                if (_access == null) _closed = true;
                else _access.Release(this, PhysicalScope ? _worldIssuance : null, () => _closed = true);
            }
            finally
            {
                if (_closed)
                {
                    _access = null;
                    if (MainAdmissions.Value == this) MainAdmissions.Value = _parent;
                }
            }
        }
    }
    private sealed class MainAccess(MainAdmission owner, GmMainOwnerGuard? guard,GmSessionRunCoordinator.Access? original,GmMainOperationClient? remote=null,bool workerObservationRequired=false)
    {
        private readonly object _state = new();
        private int _references=1;
        private bool _frozen;
        private readonly HashSet<MainAdmission> _admissions = [owner];
        private readonly HashSet<MainAdmission> _physicalClosing = [];
        private readonly HashSet<MainMutationReservation> _mutations = [];
        private ValidationReadReservation? _worldRead;
        /// <summary>
        /// Checks the exact ordinary physical admission and captures its root version under shared access state.
        /// The caller retains the root worker-context gate before entering this state lock.
        /// </summary>
        /// <param name="admission">
        /// Original physical frame belonging to this shared access.
        /// </param>
        /// <param name="files">
        /// Manager that issued <paramref name="admission"/> and must own <paramref name="lease"/>.
        /// </param>
        /// <param name="lease">
        /// Original active ordinary lease; special authority and pending decision routes are ineligible.
        /// </param>
        /// <param name="compatibleRead">
        /// Exact healthy borrowed read reservation, or <see langword="null"/> to require unreserved access.
        /// </param>
        /// <param name="version">
        /// Current root version on success, or <see langword="null"/> when reuse is unsupported.
        /// </param>
        /// <returns>
        /// <see langword="true"/> if the original access remains eligible and its root has no active mutation; otherwise, <see langword="false"/>.
        /// </returns>
        internal bool TryCaptureRecoveryValidationVersion(MainAdmission admission, FileSystemManager files,
            CanonicalWriteLease lease, ValidationReadReservation? compatibleRead, out object? version)
        {
            lock (_state)
            {
                version = null;
                if (_frozen || _references == 0 || owner.Closed || !_admissions.Contains(admission) || !admission.PhysicalScope ||
                    !ReferenceEquals(lease.Owner, files) || !ReferenceEquals(lease.MainAdmission, admission) ||
                    !lease.IsActive || lease.Purpose != CanonicalWritePurpose.SessionMutation ||
                    lease.WorkerPurpose != null || lease.WorkerRootPin != null || lease.PendingLocalDecision != null ||
                    lease.MutationIntentRecorder != null || lease.IsLegacyStorageRecovery || lease.BrowserLocalAccess != null ||
                    files.HasBrowserOriginalOperation || remote != null || Workers != null || workerObservationRequired ||
                    original?.MetadataOnly == true || original?.Owner.AdmissionClosed == true ||
                    owner.RootIdentity.WorkerContext != null || _mutations.Count != 0 || _physicalClosing.Count != 0 ||
                    (_worldRead != null && (!ReferenceEquals(_worldRead, compatibleRead) || compatibleRead!.Closed ||
                        !ReferenceEquals(compatibleRead.RetainedPhysicalAdmission, admission))) ||
                    _admissions.Any(p => p.Closed || p.Closing || p.BoundClosing || p.MetadataOnly ||
                        (p.PhysicalScope && !ReferenceEquals(p, admission))))
                    return false;
                return owner.RootIdentity.ValidationEpoch.TryCapture(out version);
            }
        }

        /// <summary>
        /// Reserves shared access without mutations, closing or unrelated physical or sibling borrowers.
        /// </summary>
        /// <param name="scope">
        /// Read reservation to install.
        /// </param>
        /// <param name="ancestors">
        /// Existing owner ancestry permitted to remain while the reservation is active.
        /// </param>
        internal void ReserveWorldRead(ValidationReadReservation scope, HashSet<MainAdmission> ancestors)
        {
            if (!TryReserveWorldRead(scope, ancestors))
                throw new InvalidOperationException("World read phase conflicts with an existing original admission.");
        }
        /// <summary>
        /// Checks availability and installs one read reservation under the same shared state lock.
        /// </summary>
        /// <param name="scope">
        /// New read reservation, never a request to nest an existing one.
        /// </param>
        /// <param name="ancestors">
        /// The exact retained owner ancestry allowed to remain active.
        /// </param>
        /// <returns>
        /// <see langword="true"/> if reserved; otherwise, <see langword="false"/> with no reservation installed.
        /// </returns>
        internal bool TryReserveWorldRead(ValidationReadReservation scope, HashSet<MainAdmission> ancestors)
        {
            lock (_state)
            {
                var retained = scope.RetainedPhysicalAdmission;
                if (_frozen || _worldRead != null || _references == 0 ||
                    _mutations.Count != 0 || _physicalClosing.Count != 0 ||
                    (retained != null && (!retained.PhysicalScope || !_admissions.Contains(retained))) ||
                    _admissions.Any(p => p.Closed || p.Closing || p.BoundClosing || !ancestors.Contains(p) ||
                        (p.PhysicalScope && !ReferenceEquals(p, retained))))
                    return false;
                _worldRead = scope;
                return true;
            }
        }
        /// <summary>
        /// Withdraws only the matching shared-access read reservation.
        /// </summary>
        /// <param name="scope">
        /// Original reservation to withdraw.
        /// </param>
        /// <param name="markReleased">
        /// Clears the owning frame's release pointer before another reservation can enter.
        /// </param>
        internal void ReleaseWorldRead(ValidationReadReservation scope, Action markReleased)
        {
            lock (_state)
            {
                if (!ReferenceEquals(_worldRead, scope)) return;
                markReleased();
                _worldRead = null;
            }
        }
        /// <summary>
        /// Refuses access unless unreserved or borrowed by the exact opening reservation.
        /// </summary>
        /// <param name="issuance">
        /// Opening read reservation, or null for ordinary borrowing or mutation.
        /// </param>
        internal void RequireNoWorldReadConflict(ValidationReadReservation? issuance)
        { lock (_state) { if (_worldRead != null && !ReferenceEquals(_worldRead, issuance))
            throw new InvalidOperationException("Original access is reserved by a World read phase."); } }
        /// <summary>
        /// Starts console lifecycle closing atomically with pending-snapshot read reservation.
        /// Browser World/Meta close behavior remains governed by its existing proof contract.
        /// </summary>
        /// <param name="admission">
        /// Original live frame beginning close.
        /// </param>
        /// <param name="markClosing">
        /// Marks that frame closing under the same lock after the read check succeeds.
        /// </param>
        internal void BeginOwnerClosing(MainAdmission admission, Action markClosing)
        {
            lock (_state)
            {
                if (_worldRead is PendingSnapshotValidationReadScope)
                    throw new InvalidOperationException("Pending snapshot validation must close before its original operation.");
                if (!_admissions.Contains(admission) || admission.Closed)
                    throw new InvalidOperationException("The original admission is no longer active.");
                markClosing();
            }
        }

        /// <summary>
        /// Starts physical close before the lease can detach resources, excluding new read reservations.
        /// </summary>
        /// <param name="admission">
        /// Exact physical frame being closed.
        /// </param>
        /// <param name="issuance">
        /// Original owned read acquisition, or <see langword="null"/> for an ordinary lease.
        /// </param>
        internal void BeginPhysicalLeaseClose(MainAdmission admission, ValidationReadReservation? issuance)
        {
            lock (_state)
            {
                RequirePendingSnapshotRelease(issuance);
                if (_admissions.Contains(admission)) _physicalClosing.Add(admission);
            }
        }

        /// <summary>
        /// Atomically registers a local and root mutation outside a reserved read, except its exact owned opening recovery.
        /// </summary>
        /// <param name="admission">
        /// Original live admission performing the mutation.
        /// </param>
        /// <param name="recoveryIssuance">
        /// Exact opening recovery identity, or <see langword="null"/> for ordinary mutation.
        /// </param>
        /// <returns>
        /// A separately tracked token; nested tokens cannot release each other.
        /// </returns>
        internal MainMutationReservation BeginMutation(MainAdmission admission, ValidationReadReservation? recoveryIssuance)
        {
            lock (_state)
            {
                if (_frozen || admission.Closed || !_admissions.Contains(admission) || _physicalClosing.Contains(admission) ||
                    (_worldRead != null && !(ReferenceEquals(_worldRead, recoveryIssuance) && recoveryIssuance!.Opening)))
                    throw new InvalidOperationException("Mutation conflicts with reserved or closing original access.");
                var token = new MainMutationReservation(ReleaseMutation);
                _mutations.Add(token);
                try { owner.RootIdentity.ValidationEpoch.Begin(token); }
                catch
                {
                    _mutations.Remove(token);
                    throw;
                }
                return token;
            }
        }

        /// <summary>
        /// Removes the supplied local mutation identity, then retires its root identity last under shared state.
        /// </summary>
        /// <param name="token">
        /// Token issued by this access; a foreign or retired token has no effect.
        /// </param>
        private void ReleaseMutation(MainMutationReservation token)
        {
            lock (_state)
            {
                if (_mutations.Remove(token)) owner.RootIdentity.ValidationEpoch.End(token);
            }
        }

        /// <summary>
        /// Checks pending-snapshot teardown while the caller holds the shared state lock.
        /// </summary>
        /// <param name="physicalIssuance">
        /// Exact owned physical acquisition identity, or <see langword="null"/> for another frame.
        /// </param>
        private void RequirePendingSnapshotRelease(ValidationReadReservation? physicalIssuance)
        {
            if (_worldRead is PendingSnapshotValidationReadScope pending &&
                !(ReferenceEquals(pending, physicalIssuance) && (pending.Opening || pending.Closed)))
                throw new InvalidOperationException("Pending snapshot validation must close before its original operation.");
        }
        internal GmWorkerRootContext? Workers;
        internal bool WorkerObservationRequired=>workerObservationRequired;
        internal GmMainOperationClient? Remote=>remote;
        internal GmMainOwnerGuard? Guard=>guard;
        internal GmSessionRunCoordinator.Access? Original=>original;
        /// <summary>
        /// Retains this actual access for one compatible live borrower.
        /// </summary>
        /// <param name="admission">
        /// New admission frame to retain exactly once.
        /// </param>
        /// <param name="issuance">
        /// Exact read reservation opening recovery, or null outside that acquisition.
        /// </param>
        /// <returns>
        /// This same shared access after successful retention.
        /// </returns>
        internal MainAccess Retain(MainAdmission admission, ValidationReadReservation? issuance)
        {
            lock(_state)
            {
                if(_frozen || _references==0)throw GmSessionRunPersistence.Invalid();
                if (_worldRead != null && !ReferenceEquals(_worldRead, issuance)) throw new InvalidOperationException("Original access is reserved by a World read phase.");
                if (!_admissions.Add(admission)) throw GmSessionRunPersistence.Invalid();
                _references++;return this;
            }
        }
        /// <summary>
        /// Freezes terminal ownership after rejecting an active pending-snapshot read without changing state.
        /// </summary>
        internal void FreezeForTerminalClose()
        {
            lock(_state)
            {
                RequirePendingSnapshotRelease(null);
                _frozen=true;
                if(_references!=1)throw new IOException("Original operation still owns filesystem leases; close is unconfirmed.");
            }
        }
        /// <summary>
        /// Atomically closes and removes an admission only when its retained read reservation permits it.
        /// </summary>
        /// <param name="admission">
        /// The original admission being disposed.
        /// </param>
        /// <param name="physicalIssuance">
        /// Exact physical acquisition identity, or null for a nonphysical original owner.
        /// </param>
        /// <param name="markClosed">
        /// Marks that admission closed after the reservation check and before removal under the same lock.
        /// </param>
        public void Release(MainAdmission admission, ValidationReadReservation? physicalIssuance, Action markClosed)
        {
            bool release;
            lock (_state)
            {
                RequirePendingSnapshotRelease(physicalIssuance);
                markClosed();
                if (!_admissions.Remove(admission)) return;
                _physicalClosing.Remove(admission);
                release = --_references == 0;
            }
            if(release){Workers?.ReleaseClient();guard?.Dispose();original?.Pin?.Dispose();}
        }
    }
    // Negative admission is observed without creating or adopting a worker
    // ledger. The Windows main route cannot reinterpret a Linux-only inventory.
    /// <summary>
    /// Requires worker inventory absence under the original root gate without creating or adopting an inventory.
    /// </summary>
    internal void RequireAbsentMainWorkerInventory()
    {
        using var operationTiming = MeasureOperation("MainAdmission.WorkerInventory.GateAndBody");
        var root=CanonicalRootAuthorityIdentity;
        lock(root.WorkerContextGate) {
            using var bodyTiming = MeasureOperation("MainAdmission.WorkerInventory.Body");
            if(root.WorkerContext!=null)throw GmSessionRunPersistence.Invalid();
            TrustedLocalFileScope.CreateAndObserveNamespace(BasePath,
                new WorkerLedgerTarget(BasePath).DirectoryPath,out var observed,timingOwner:this,
                constructionOperationName:"MainAdmission.WorkerInventory.ScopeConstruction",
                observationOperationName:"MainAdmission.WorkerInventory.ObserveNamespace");
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
        lease.MainAdmission.RequireNoWorldMutation();
        if(lease.MainAdmission.MetadataOnly || lease.Purpose==CanonicalWritePurpose.SessionFinalization)
            throw GmSessionRunPersistence.Invalid();
        lease.MainAdmission.Validate(lease);
        if(CurrentBrowserOriginalScope() is { Cleanup:true } cleanup)
        {
            cleanup.ValidateOwner(lease.MainAdmission);
            cleanup.ValidateCleanupInventory();
        }
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
    private void EnsureMainBeforeRecovery(CanonicalWriteLease lease,string diagnosticOrigin="other")
    {
        lease.MainAdmission!.Validate(lease);
        if(lease.MainAdmission.MetadataOnly)throw GmSessionRunPersistence.Invalid();
        _ = PreflightBrowserOriginalAdmission(lease.MainAdmission,lease,diagnosticOrigin);
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
