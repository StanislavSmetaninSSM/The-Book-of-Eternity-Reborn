using System.Security.Cryptography;
using System.Text;
using System.Text.Json.Nodes;
using BookOfEternityClient.Services;

namespace BookOfEternityClient.Core;

public partial class FileSystemManager
{
    private const string BrowserManifestPath = "game_state/control/pending_turn_snapshot.json";
    private static readonly AsyncLocal<BrowserOriginalOperationScope?> BrowserOriginalOperations = new();

    private IDisposable? MeasureOriginalBrowserAdmission(string stage)
    {
        IDisposable? existing = _hooks?.BrowserOriginalAdmissionTimingObserver is { } observer
            ? new OriginalBrowserAdmissionTiming(observer, stage) : null;
#if DEBUG
        var diagnostic = _browserAdmissionDiagnostic?.Measure(stage);
        if (existing == null) return diagnostic;
        if (diagnostic != null) return new CombinedBrowserTiming(existing, diagnostic);
#endif
        return existing;
    }
#if DEBUG
    private sealed class CombinedBrowserTiming(IDisposable existing, IDisposable diagnostic) : IDisposable
    {
        public void Dispose() { try { existing.Dispose(); } finally { diagnostic.Dispose(); } }
    }
#endif

    private sealed class OriginalBrowserAdmissionTiming(Action<string, TimeSpan> observer, string stage) : IDisposable
    {
        private readonly long _started = System.Diagnostics.Stopwatch.GetTimestamp();
        public void Dispose() => observer(stage, System.Diagnostics.Stopwatch.GetElapsedTime(_started));
    }

    // An original pre-publication refusal, not a grant to bypass the browser
    // fence. Only this issuing filesystem can identify it for diagnostics.
    private sealed class BrowserOriginalPhaseRefusal(FileSystemManager files)
        : InvalidOperationException("Original browser operation is incomplete; recovery retained.")
    {
        internal readonly FileSystemManager Files = files;
    }
    internal bool IsOriginalBrowserPhaseRefusal(Exception failure) =>
        failure is BrowserOriginalPhaseRefusal refusal && ReferenceEquals(refusal.Files, this);

    // Privately issued under the actual acquired owner, never deserialized. It
    // narrows that owner while original preparation/cleanup changes its layout.
    internal sealed class BrowserOriginalOperationScope : IDisposable
    {
        internal readonly FileSystemManager _files;
        private readonly MainAdmission _owner;
        internal readonly BrowserOriginalOperationScope? _parent;
        internal readonly PendingPlayerActionService.Binding Binding;
        internal readonly BrowserOriginalMainCondition Condition;
        internal PendingPlayerActionService.Staged? Staged { get; private set; }
        internal bool Processing { get; private set; }
        internal bool Cleanup { get; private set; }
        private bool _disposed;
        internal string? ExpectedPendingJson { get; private set; }
        internal PendingPlayerActionService.State? ExpectedState { get; private set; }
        internal PendingPlayerActionService.Staged? ExpectedStaged { get; private set; }
        private readonly string _submittedAtUtc;
        private Dictionary<string,string>? _cleanupInventory;
        internal BrowserOriginalOperationScope(FileSystemManager files, MainAdmission owner,
            PendingPlayerActionService.Binding binding, PendingPlayerActionService.Staged? staged,
            BrowserOriginalOperationScope? parent)
        {
            _files=files;_owner=owner;Binding=binding;_parent=parent;
            var expectedJson=staged?.Json??binding.Json;
            ExpectedPendingJson=expectedJson;
            _submittedAtUtc=JsonNode.Parse(binding.Json)!["submittedAtUtc"]!.GetValue<string>();
            var expectedState=PendingPlayerActionService.Parse(expectedJson,binding.Generation);
            RequireInvariantBinding(expectedState);
            ExpectedState=expectedState;
            ExpectedStaged=ReadExpectedStaged(expectedState);
            Condition=owner.CaptureBrowserCondition(binding.Generation);
            if(staged != null)
            {
#if DEBUG
                using var diagnostic = files.BrowserDiagnosticContext("operation-construction", staged);
#endif
                RequireOriginalTuple(ExpectedStaged??throw BrowserOriginalMainCondition.Invalid(),staged);
                var manifest=files.ValidateOriginalBrowserAdmission(staged, verifyPhysical: true);
                if(manifest.BrowserOriginalMainCondition != Condition) throw BrowserOriginalMainCondition.Invalid();
                Staged=staged;
            }
        }
        internal void ValidateOwner(MainAdmission? current)
        {
            if(_disposed || _owner.Closed || !_owner.Acquired || _owner.MetadataOnly) throw BrowserOriginalMainCondition.Invalid();
            for(var p=current;p!=null;p=p.Parent)
                if(!p.Closed && p.SharesAccess(_owner)) { _owner.Validate(null);Condition.RequireCurrent(_files);return; }
            throw BrowserOriginalMainCondition.Invalid();
        }
        internal void Seal(PendingPlayerActionService.Staged staged)
        {
#if DEBUG
            using var diagnostic = _files.BrowserDiagnosticContext("operation-seal", staged);
#endif
            ValidateOwner(MainAdmissions.Value);
            if(Staged != null || staged.Binding.ActionId != Binding.ActionId) throw BrowserOriginalMainCondition.Invalid();
            RequireOriginalTuple(ExpectedStaged??throw BrowserOriginalMainCondition.Invalid(),staged);
            var manifest=_files.ValidateOriginalBrowserAdmission(staged, verifyPhysical:true);
            if(manifest.BrowserOriginalMainCondition != Condition) throw BrowserOriginalMainCondition.Invalid();
            Staged=staged;
        }
        internal void BeginProcessing(PendingPlayerActionService.Staged staged)
        {
            ValidateOwner(MainAdmissions.Value);
            if(Staged == null || Staged != staged) throw BrowserOriginalMainCondition.Invalid();
            Processing=true;
        }
        internal void BeginCleanup()
        {
#if DEBUG
            using var diagnostic = _files.BrowserDiagnosticContext("cleanup-begin", Staged);
#endif
            ValidateOwner(MainAdmissions.Value);
            if(!Processing || Staged == null) throw BrowserOriginalMainCondition.Invalid();
            if(!Cleanup)
            {
                var manifest=_files.ValidateOriginalBrowserAdmission(Staged,verifyPhysical:true);
                var payload=GameEngine.ValidateOriginalBrowserAuthority(manifest,Staged.AuthorityJson,_files.ReadOriginalBrowserBytes);
                var inventory=new Dictionary<string,string>(StringComparer.Ordinal);
                foreach(var (logical,path) in manifest.Files)inventory.Add(path,manifest.SnapshotFileHashes[logical]);
                foreach(var (logical,path) in payload.RollbackBackups)inventory.Add(path,payload.RollbackBackupHashes[logical]);
                foreach(var path in new[]{"input/turn_request.json",BrowserManifestPath,PendingTurnSnapshotAuthority.AuthorityPath})
                    inventory.Add(path,Convert.ToHexString(SHA256.HashData(_files.ReadOriginalBrowserBytes(path)??throw BrowserOriginalMainCondition.Invalid())));
                _cleanupInventory=inventory;
                ValidateCleanupInventory();
                Cleanup=true;
            }
            else ValidateCleanupInventory();
        }
        internal void ValidateCleanupInventory()
        {
            var inventory=_cleanupInventory??throw BrowserOriginalMainCondition.Invalid();
            foreach(var path in _files.EnumerateOriginalBrowserPhysicalFiles())
            {
                if(path.StartsWith("game_state/control/pending_turn_snapshot/",StringComparison.OrdinalIgnoreCase) ||
                    path.Contains(".rollback.",StringComparison.OrdinalIgnoreCase) && !GameEngine.IsExplorerLocalTurnRollbackArtifactPath(path))
                    if(!inventory.ContainsKey(path))throw BrowserOriginalMainCondition.Invalid();
            }
            foreach(var (path,hash) in inventory)
                if(_files.ReadOriginalBrowserBytes(path) is { } remaining &&
                    !string.Equals(Convert.ToHexString(SHA256.HashData(remaining)),hash,StringComparison.OrdinalIgnoreCase))
                    throw BrowserOriginalMainCondition.Invalid();
        }
        internal void ObservePublishedPhase(string previous,string? next)
        {
            ValidateOwner(MainAdmissions.Value);
            if(ExpectedPendingJson!=previous || _files.ReadOriginalBrowserText(PendingPlayerActionService.PendingPath)!=next)
                throw BrowserOriginalMainCondition.Invalid();
            var before=ExpectedState??throw BrowserOriginalMainCondition.Invalid();
            var after=next==null?null:PendingPlayerActionService.Parse(next,Binding.Generation);
            var allowed=before.Phase switch
            {
                "queued"=>after?.Phase=="preparing",
                "preparing"=>after?.Phase=="staged",
                "staged"=>Processing && after?.Phase=="terminalProcessing",
                "terminalProcessing"=>Cleanup && after?.Phase is "accepted" or "settled",
                "accepted" or "settled"=>Cleanup && after==null,
                _=>false
            };
            if(!allowed)throw BrowserOriginalMainCondition.Invalid();
            if(after!=null)RequireInvariantBinding(after);
            var afterStaged=after==null?null:ReadExpectedStaged(after);
            if(Staged!=null && afterStaged!=null)RequireOriginalTuple(afterStaged,Staged);
            ExpectedState=after;
            ExpectedStaged=afterStaged;
            ExpectedPendingJson=next;
        }
        private void RequireInvariantBinding(PendingPlayerActionService.State state)
        {
            if(state.Binding.ActionId!=Binding.ActionId || state.Binding.Generation!=Binding.Generation ||
                state.Binding.Action!=Binding.Action || state.Binding.Source!=Binding.Source ||
                JsonNode.Parse(state.Json)!["submittedAtUtc"]!.GetValue<string>()!=_submittedAtUtc)
                throw BrowserOriginalMainCondition.Invalid();
        }
        private static PendingPlayerActionService.Staged? ReadExpectedStaged(PendingPlayerActionService.State state) =>
            state.Phase is "staged" or "terminalProcessing" or "accepted" or "settled"
                ? PendingPlayerActionService.ReadStaged(state) : null;
        private static void RequireOriginalTuple(PendingPlayerActionService.Staged current,PendingPlayerActionService.Staged original)
        {
            if(current.RequestJson!=original.RequestJson || current.ManifestJson!=original.ManifestJson ||
                current.AuthorityJson!=original.AuthorityJson || current.HistoryJson!=original.HistoryJson)
                throw BrowserOriginalMainCondition.Invalid();
        }
        public void Dispose()
        {
            if(_disposed)return;_disposed=true;
            if(ReferenceEquals(BrowserOriginalOperations.Value,this))BrowserOriginalOperations.Value=_parent;
        }
    }

    internal bool HasBrowserOriginalOperation=>CurrentBrowserOriginalScope()!=null;
    internal BrowserOriginalOperationScope BeginBrowserOriginalOperation(PendingPlayerActionService.Binding binding,
        PendingPlayerActionService.Staged? staged=null)
    {
        var owner=MainAdmissions.Value;
        while(owner!=null && (owner.Closed || owner.RootIdentity!=CanonicalRootAuthorityIdentity || !owner.Acquired))owner=owner.Parent;
        if(owner==null)throw BrowserOriginalMainCondition.Invalid();
        var scope=new BrowserOriginalOperationScope(this,owner,binding,staged,BrowserOriginalOperations.Value);
        BrowserOriginalOperations.Value=scope;return scope;
    }
    internal BrowserOriginalMainCondition CaptureBrowserOriginalCondition(CanonicalWriteLease lease,string generation)
    {
        EnsurePhysicalCanonicalWriteLease(lease);
        var condition=lease.MainAdmission!.CaptureBrowserCondition(generation);
        var scope=CurrentBrowserOriginalScope();
        if(scope==null || scope.Condition!=condition)throw BrowserOriginalMainCondition.Invalid();
        scope.ValidateOwner(lease.MainAdmission);return condition;
    }
    internal void SealBrowserOriginalPreparation(PendingPlayerActionService.Staged staged) =>
        (CurrentBrowserOriginalScope()??throw BrowserOriginalMainCondition.Invalid()).Seal(staged);
    internal void BeginBrowserOriginalProcessing(PendingPlayerActionService.Staged staged) =>
        (CurrentBrowserOriginalScope()??throw BrowserOriginalMainCondition.Invalid()).BeginProcessing(staged);
    internal void AllowBrowserOriginalCleanup() => CurrentBrowserOriginalScope()?.BeginCleanup();
    internal void ObserveBrowserOriginalPhasePublication(string previous,string? next) =>
        CurrentBrowserOriginalScope()?.ObservePublishedPhase(previous,next);
    private BrowserOriginalOperationScope? CurrentBrowserOriginalScope()
    {
        for(var p=BrowserOriginalOperations.Value;p!=null;p=p._parent)
            if(p._files.CanonicalRootAuthorityIdentity==CanonicalRootAuthorityIdentity)return p;
        return null;
    }

    // No recovery-capable FileSystemManager reader is allowed below this fence.
    private TrustedLocalFileScope CreateOriginalBrowserFileScope(bool? held = null)
    {
        using var timing=MeasureOriginalBrowserAdmission((held??HasAmbientCanonicalLease())
            ? "trusted-scope-construction-held" : "trusted-scope-construction-unheld");
        return new TrustedLocalFileScope([BasePath]);
    }
    private byte[]? ReadOriginalBrowserBytes(string relative) => ReadOriginalBrowserBytes(relative,null,null);
    private byte[]? ReadOriginalBrowserBytes(string relative,TrustedLocalFileScope? scope,bool? held)
    {
        if(!PendingTurnSnapshotAuthority.IsSafeRelativePath(relative))throw BrowserOriginalMainCondition.Invalid();
        scope??=CreateOriginalBrowserFileScope(held);var path=scope.ValidateFile(ResolvePath(relative));
        if(!File.Exists(path))return null;
        using var stream=new FileStream(scope.ValidateFile(path,false),FileMode.Open,FileAccess.Read,FileShare.ReadWrite|FileShare.Delete);
        var length=stream.Length;
        // Same representability limit as the existing complete byte reader, not
        // a new cold-only limit on produced history or canonical snapshots.
        if(length>Array.MaxLength)throw new IOException("Original browser file cannot be represented by the existing byte reader.");
        var bytes=new byte[(int)length];stream.ReadExactly(bytes);
#if DEBUG
        _browserAdmissionDiagnostic?.Read(relative is "input/turn_request.json" or BrowserManifestPath or PendingTurnSnapshotAuthority.AuthorityPath or PendingPlayerActionService.PendingPath
            ? "tuple" : relative.StartsWith("game_state/control/pending_turn_snapshot/", StringComparison.Ordinal)
                ? "snapshot" : relative.Contains(".rollback.", StringComparison.Ordinal) ? "rollback" : "other", length);
#endif
        if(stream.ReadByte()!=-1)throw BrowserOriginalMainCondition.Invalid();
        scope.ValidateFile(path,false);return bytes;
    }
    private string? ReadOriginalBrowserText(string relative) => ReadOriginalBrowserText(relative,null,null);
    private string? ReadOriginalBrowserText(string relative,TrustedLocalFileScope? scope,bool? held)
    {
        var bytes=ReadOriginalBrowserBytes(relative,scope,held);if(bytes==null)return null;
        using var stream=new MemoryStream(bytes,false);
        using var reader=new StreamReader(stream,Encoding.UTF8,detectEncodingFromByteOrderMarks:true);
        return reader.ReadToEnd();
    }
    private void RequireOriginalSnapshotHash(string relative,string expected,TrustedLocalFileScope? scope,bool held)
    {
        if(!PendingTurnSnapshotAuthority.IsSafeRelativePath(relative) ||
            !relative.StartsWith("game_state/control/pending_turn_snapshot/",StringComparison.Ordinal))throw BrowserOriginalMainCondition.Invalid();
        scope??=CreateOriginalBrowserFileScope(held);var path=scope.ValidateFile(ResolvePath(relative),false);
        using var stream=new FileStream(path,FileMode.Open,FileAccess.Read,FileShare.ReadWrite|FileShare.Delete);
        var actualHash=Convert.ToHexString(SHA256.HashData(stream));
#if DEBUG
        _browserAdmissionDiagnostic?.Read("snapshot-hash", stream.Position);
#endif
        if(actualHash!=expected)throw BrowserOriginalMainCondition.Invalid();
        scope.ValidateFile(path,false);
    }
    private GameEngine.PendingTurnSnapshotManifest ValidateOriginalBrowserAdmission(PendingPlayerActionService.Staged staged,bool verifyPhysical,
        CanonicalWriteLease? physicalLease=null)
    {
#if DEBUG
        using var diagnostic = BrowserDiagnosticContext(_browserAdmissionDiagnostic?.CurrentOrigin ?? "other", staged, physicalLease);
#endif
        GameEngine.PendingTurnSnapshotManifest manifest;
        using(MeasureOriginalBrowserAdmission("detached-proof-decoding"))
        {
            manifest=GameEngine.ValidateDetachedBrowserBinding(staged);
            manifest.BrowserOriginalMainCondition!.Validate(BasePath,staged.Binding.Generation);
        }
        if(!verifyPhysical)return manifest;
        if(physicalLease!=null)EnsurePhysicalCanonicalWriteLease(physicalLease);
        // The post-lock preflight runs before ambient activation. Its actual
        // same-filesystem physical lease is the diagnostic witness at that call.
        var held=physicalLease!=null || HasAmbientCanonicalLease();
        using var physicalTiming=MeasureOriginalBrowserAdmission("physical-verification");
#if DEBUG
        _browserAdmissionDiagnostic?.Inventory(manifest.Files.Count, manifest.RollbackBackups.Count);
#endif
        // Grants live only in this one held proof. Each reader still freshly
        // validates the complete ancestor/leaf path before and after its read.
        var fileScope=held?CreateOriginalBrowserFileScope(held):null;
        if(ReadOriginalBrowserText("input/turn_request.json",fileScope,held)!=staged.RequestJson ||
            ReadOriginalBrowserText(BrowserManifestPath,fileScope,held)!=staged.ManifestJson ||
            ReadOriginalBrowserText(PendingTurnSnapshotAuthority.AuthorityPath,fileScope,held)!=staged.AuthorityJson)
            throw BrowserOriginalMainCondition.Invalid();
        PendingTurnSnapshotAuthority.PendingTurnSnapshotAuthorityPayload payload;
        using(MeasureOriginalBrowserAdmission("authority-and-rollback-verification"))
            payload=GameEngine.ValidateOriginalBrowserAuthority(manifest,staged.AuthorityJson,relative=>ReadOriginalBrowserBytes(relative,fileScope,held));
        if(payload.SnapshotHashMode!=PendingTurnSnapshotAuthority.ExactSnapshotHashMode ||
            payload.RollbackHashMode!=PendingTurnSnapshotAuthority.ExactRollbackHashMode ||
            !manifest.Files.Keys.ToHashSet(StringComparer.Ordinal).SetEquals(manifest.SnapshotFileHashes.Keys))
            throw BrowserOriginalMainCondition.Invalid();
        PendingTurnSnapshotAuthority.RequireExactSignedPaths(manifest.Files.Keys.Concat(manifest.Files.Values));
        using(MeasureOriginalBrowserAdmission("snapshot-hash-verification"))
            foreach(var (logical,path) in manifest.Files)RequireOriginalSnapshotHash(path,manifest.SnapshotFileHashes[logical],fileScope,held);
        return manifest;
    }

#if DEBUG
    internal (BrowserOriginalMainCondition Condition, Dictionary<string,string> Hashes)
        InspectOriginalBrowserClosedIdle(PendingPlayerActionService.Staged staged)
    {
#if DEBUG
        using var diagnostic = BrowserDiagnosticContext("closed-idle-diagnostic", staged);
#endif
        RequireLoadIpcOutsideFileScopes();
        var manifest=ValidateOriginalBrowserAdmission(staged,verifyPhysical:true);
        if(ReadOriginalBrowserText(PendingPlayerActionService.PendingPath)!=staged.Json)
            throw BrowserOriginalMainCondition.Invalid();
        var condition=manifest.BrowserOriginalMainCondition!;
        condition.RequireCurrent(this);
        var paths=new[]{PendingPlayerActionService.PendingPath,"input/turn_request.json",BrowserManifestPath,
            PendingTurnSnapshotAuthority.AuthorityPath}.Concat(manifest.Files.Values)
            .Concat(manifest.RollbackBackups.Values).Distinct(StringComparer.Ordinal);
        var hashes=paths.ToDictionary(path=>path,path=>Convert.ToHexString(SHA256.HashData(
            ReadOriginalBrowserBytes(path)??throw BrowserOriginalMainCondition.Invalid())),StringComparer.Ordinal);
        return (condition,hashes);
    }
#endif

    private bool OriginalBrowserFileExists(string relative)
    {
        if(!PendingTurnSnapshotAuthority.IsSafeRelativePath(relative))throw BrowserOriginalMainCondition.Invalid();
        var scope=new TrustedLocalFileScope([BasePath]);
        return File.Exists(scope.ValidateFile(ResolvePath(relative)));
    }
    private string[] EnumerateOriginalBrowserPhysicalFiles()
    {
        var scope=new TrustedLocalFileScope([BasePath]);var result=new List<string>();
        void Walk(string directory)
        {
            foreach(var path in Directory.EnumerateFileSystemEntries(scope.ValidateDirectory(directory,false)))
            {
                var observed=scope.ObserveNamespace(path);
                if(observed.BlockingFileAncestor!=null)throw BrowserOriginalMainCondition.Invalid();
                if(observed.Kind==TrustedLocalNamespaceKind.Directory)Walk(path);
                else if(observed.Kind==TrustedLocalNamespaceKind.File)
                    result.Add(Path.GetRelativePath(GameSessionPath,scope.ValidateFile(path,false)).Replace('\\','/'));
                else throw BrowserOriginalMainCondition.Invalid();
            }
        }
        Walk(GameSessionPath);return result.ToArray();
    }

    private BrowserOriginalMainCondition? PreflightBrowserOriginalAdmission(MainAdmission admission,CanonicalWriteLease? physicalLease=null,
        string diagnosticOrigin="other")
    {
#if DEBUG
        using var diagnostic = BrowserDiagnosticContext(diagnosticOrigin, lease: physicalLease);
#endif
        using var preflightTiming=MeasureOriginalBrowserAdmission("inclusive-preflight");
        if(admission.MetadataOnly)return null; // existing typed stop/diagnostic contract
        var scope=CurrentBrowserOriginalScope();
        if(scope!=null)scope.ValidateOwner(admission);
        var pendingJson=ReadOriginalBrowserText(PendingPlayerActionService.PendingPath);
        var manifestJson=ReadOriginalBrowserText(BrowserManifestPath);
        var raw=manifestJson==null || scope!=null && pendingJson!=null?null:StrictJsonAuthority.Deserialize<JsonObject>(manifestJson,
            new System.Text.Json.JsonSerializerOptions(),"original admission manifest classification");
        var marked=raw!=null && (raw.ContainsKey("browserActionId") || raw.ContainsKey("browserSessionGeneration") ||
            raw.ContainsKey("browserOriginalMainCondition"));
        if(scope!=null && pendingJson!=scope.ExpectedPendingJson)throw BrowserOriginalMainCondition.Invalid();
        if(pendingJson==null)
        {
            if(marked || scope!=null && !scope.Cleanup)throw BrowserOriginalMainCondition.Invalid();
            scope?.ValidateCleanupInventory();
            return scope?.Condition;
        }
        // The private scope advances only after actual publication and checks
        // its complete immutable tuple. Exact current bytes and actual owner
        // checks above still precede reuse; physical verification below remains.
        var state=scope==null?PendingPlayerActionService.Parse(pendingJson,ObserveExistingHelperGeneration())
            :scope.ExpectedState??throw BrowserOriginalMainCondition.Invalid();
        if(scope!=null)
        {
            if(scope.Staged==null)
            {
                if(state.Phase is not ("queued" or "preparing"))throw BrowserOriginalMainCondition.Invalid();
                return scope.Condition;
            }
            if(state.Phase is not ("staged" or "terminalProcessing" or "accepted" or "settled") ||
                state.Phase=="terminalProcessing" && !scope.Processing)throw BrowserOriginalMainCondition.Invalid();
            var retained=scope.ExpectedStaged??throw BrowserOriginalMainCondition.Invalid();
            if(retained.RequestJson!=scope.Staged.RequestJson || retained.ManifestJson!=scope.Staged.ManifestJson ||
                retained.AuthorityJson!=scope.Staged.AuthorityJson || retained.HistoryJson!=scope.Staged.HistoryJson)
                throw BrowserOriginalMainCondition.Invalid();
            if(!scope.Cleanup)ValidateOriginalBrowserAdmission(retained,verifyPhysical:true,physicalLease);
            else scope.ValidateCleanupInventory();
            return scope.Condition;
        }
        if(state.Phase=="queued")
        {
            if(marked)throw BrowserOriginalMainCondition.Invalid();
            return null;
        }
        if(state.Phase is "preparing" or "terminalProcessing")throw new BrowserOriginalPhaseRefusal(this);
        var staged=PendingPlayerActionService.ReadStaged(state);
        var original=ValidateOriginalBrowserAdmission(staged,verifyPhysical:state.Phase=="staged",physicalLease);
        // Completed receipts retain their existing cleanup-only contract. Their
        // old run need not remain live; the original receipt validator still runs.
        if(state.Phase is "accepted" or "settled")
        {
            GameEngine.ValidateCompletedBrowserReceipt(state,original,ReadOriginalBrowserBytes,
                OriginalBrowserFileExists,EnumerateOriginalBrowserPhysicalFiles());
            RequireOriginalBrowserRecoverySafe(staged,original);
            return null;
        }
        original.BrowserOriginalMainCondition!.RequireCurrent(this);
        RequireOriginalBrowserRecoverySafe(staged,original);
        return original.BrowserOriginalMainCondition;
    }

    private void RequireOriginalBrowserRecoverySafe(PendingPlayerActionService.Staged staged,
        GameEngine.PendingTurnSnapshotManifest manifest)
    {
        var history=StrictJsonAuthority.Deserialize<JsonObject>(staged.HistoryJson,
            new System.Text.Json.JsonSerializerOptions(),"original browser history inventory")
            ??throw BrowserOriginalMainCondition.Invalid();
        var paths=manifest.Files.Keys.Concat(manifest.Files.Values).Concat(manifest.RollbackBackups.Values)
            .Concat(history.Select(pair=>pair.Key))
            .Concat(GameEngine.SelectOriginalStoryContinuityFiles(EnumerateOriginalBrowserPhysicalFiles()))
            .Concat(new[]{PendingPlayerActionService.PendingPath,"input/turn_request.json",BrowserManifestPath,
                PendingTurnSnapshotAuthority.AuthorityPath,"ready/turn_complete.json","ready/turn_error.json"});
        var settled=PendingPlayerActionService.Parse(staged.Json,staged.Binding.Generation).Phase=="settled";
        new TrustedLocalFilePublication(this,new TrustedLocalFileScope([BasePath]))
            .RequireBrowserRecoveryPreservesOriginal(staged.Binding.Generation,paths,relative =>
                settled && GameEngine.IsOriginalBrowserPhysicalInventoryPath(relative) ||
                GameEngine.SelectOriginalStoryContinuityFiles([relative]).Any(),
                _hooks?.BrowserRecoveryJournalSelectedObserver);
    }
}
