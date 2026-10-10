using System.Security.Cryptography;
using System.Text;
using System.Text.Json.Nodes;
using BookOfEternityClient.Services;

namespace BookOfEternityClient.Core;

public partial class FileSystemManager
{
    private const string BrowserManifestPath = "game_state/control/pending_turn_snapshot.json";
    private static readonly AsyncLocal<BrowserOriginalOperationScope?> BrowserOriginalOperations = new();

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
        internal BrowserOriginalOperationScope(FileSystemManager files, MainAdmission owner,
            PendingPlayerActionService.Binding binding, PendingPlayerActionService.Staged? staged,
            BrowserOriginalOperationScope? parent)
        {
            _files=files;_owner=owner;Binding=binding;_parent=parent;
            Condition=owner.CaptureBrowserCondition(binding.Generation);
            if(staged != null)
            {
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
            ValidateOwner(MainAdmissions.Value);
            if(Staged != null || staged.Binding.ActionId != Binding.ActionId) throw BrowserOriginalMainCondition.Invalid();
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
            ValidateOwner(MainAdmissions.Value);
            if(!Processing || Staged == null) throw BrowserOriginalMainCondition.Invalid();
            Cleanup=true;
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
    private BrowserOriginalOperationScope? CurrentBrowserOriginalScope()
    {
        for(var p=BrowserOriginalOperations.Value;p!=null;p=p._parent)
            if(p._files.CanonicalRootAuthorityIdentity==CanonicalRootAuthorityIdentity)return p;
        return null;
    }

    // No recovery-capable FileSystemManager reader is allowed below this fence.
    private byte[]? ReadOriginalBrowserBytes(string relative)
    {
        if(!PendingTurnSnapshotAuthority.IsSafeRelativePath(relative))throw BrowserOriginalMainCondition.Invalid();
        var scope=new TrustedLocalFileScope([BasePath]);var path=scope.ValidateFile(ResolvePath(relative));
        if(!File.Exists(path))return null;
        using var stream=new FileStream(scope.ValidateFile(path,false),FileMode.Open,FileAccess.Read,FileShare.ReadWrite|FileShare.Delete);
        var length=stream.Length;
        // Same representability limit as the existing complete byte reader, not
        // a new cold-only limit on produced history or canonical snapshots.
        if(length>Array.MaxLength)throw new IOException("Original browser file cannot be represented by the existing byte reader.");
        var bytes=new byte[(int)length];stream.ReadExactly(bytes);
        if(stream.ReadByte()!=-1)throw BrowserOriginalMainCondition.Invalid();
        scope.ValidateFile(path,false);return bytes;
    }
    private string? ReadOriginalBrowserText(string relative)
    {
        var bytes=ReadOriginalBrowserBytes(relative);if(bytes==null)return null;
        using var stream=new MemoryStream(bytes,false);
        using var reader=new StreamReader(stream,Encoding.UTF8,detectEncodingFromByteOrderMarks:true);
        return reader.ReadToEnd();
    }
    private void RequireOriginalSnapshotHash(string relative,string expected)
    {
        if(!PendingTurnSnapshotAuthority.IsSafeRelativePath(relative) ||
            !relative.StartsWith("game_state/control/pending_turn_snapshot/",StringComparison.Ordinal))throw BrowserOriginalMainCondition.Invalid();
        var scope=new TrustedLocalFileScope([BasePath]);var path=scope.ValidateFile(ResolvePath(relative),false);
        using var stream=new FileStream(path,FileMode.Open,FileAccess.Read,FileShare.ReadWrite|FileShare.Delete);
        if(Convert.ToHexString(SHA256.HashData(stream))!=expected)throw BrowserOriginalMainCondition.Invalid();
        scope.ValidateFile(path,false);
    }
    private GameEngine.PendingTurnSnapshotManifest ValidateOriginalBrowserAdmission(PendingPlayerActionService.Staged staged,bool verifyPhysical)
    {
        var manifest=GameEngine.ValidateDetachedBrowserBinding(staged);
        manifest.BrowserOriginalMainCondition!.Validate(BasePath,staged.Binding.Generation);
        if(!verifyPhysical)return manifest;
        if(ReadOriginalBrowserText("input/turn_request.json")!=staged.RequestJson ||
            ReadOriginalBrowserText(BrowserManifestPath)!=staged.ManifestJson ||
            ReadOriginalBrowserText(PendingTurnSnapshotAuthority.AuthorityPath)!=staged.AuthorityJson)
            throw BrowserOriginalMainCondition.Invalid();
        var payload=GameEngine.ValidateOriginalBrowserAuthority(manifest,staged.AuthorityJson,ReadOriginalBrowserBytes);
        if(payload.SnapshotHashMode!=PendingTurnSnapshotAuthority.ExactSnapshotHashMode ||
            payload.RollbackHashMode!=PendingTurnSnapshotAuthority.ExactRollbackHashMode ||
            !manifest.Files.Keys.ToHashSet(StringComparer.Ordinal).SetEquals(manifest.SnapshotFileHashes.Keys))
            throw BrowserOriginalMainCondition.Invalid();
        PendingTurnSnapshotAuthority.RequireExactSignedPaths(manifest.Files.Keys.Concat(manifest.Files.Values));
        foreach(var (logical,path) in manifest.Files)RequireOriginalSnapshotHash(path,manifest.SnapshotFileHashes[logical]);
        return manifest;
    }

    private BrowserOriginalMainCondition? PreflightBrowserOriginalAdmission(MainAdmission admission)
    {
        if(admission.MetadataOnly)return null; // existing typed stop/diagnostic contract
        var scope=CurrentBrowserOriginalScope();
        if(scope!=null)scope.ValidateOwner(admission);
        var pendingJson=ReadOriginalBrowserText(PendingPlayerActionService.PendingPath);
        var manifestJson=ReadOriginalBrowserText(BrowserManifestPath);
        var marked=manifestJson!=null && (manifestJson.Contains("\"browserActionId\"",StringComparison.Ordinal) ||
            manifestJson.Contains("\"browserSessionGeneration\"",StringComparison.Ordinal) ||
            manifestJson.Contains("\"browserOriginalMainCondition\"",StringComparison.Ordinal));
        if(pendingJson==null)
        {
            if(marked || scope!=null)throw BrowserOriginalMainCondition.Invalid();
            return null;
        }
        var state=PendingPlayerActionService.Parse(pendingJson,ObserveExistingHelperGeneration());
        if(scope!=null)
        {
            if(state.Binding.ActionId!=scope.Binding.ActionId || state.Binding.Generation!=scope.Binding.Generation ||
                state.Binding.Action!=scope.Binding.Action || state.Binding.Source!=scope.Binding.Source ||
                JsonNode.Parse(state.Json)!["submittedAtUtc"]!.GetValue<string>()!=JsonNode.Parse(scope.Binding.Json)!["submittedAtUtc"]!.GetValue<string>())
                throw BrowserOriginalMainCondition.Invalid();
            if(scope.Staged==null)
            {
                if(state.Phase is not ("queued" or "preparing"))throw BrowserOriginalMainCondition.Invalid();
                return scope.Condition;
            }
            if(state.Phase is not ("staged" or "terminalProcessing" or "accepted" or "settled") ||
                state.Phase=="terminalProcessing" && !scope.Processing)throw BrowserOriginalMainCondition.Invalid();
            var retained=PendingPlayerActionService.ReadStaged(state);
            if(retained.RequestJson!=scope.Staged.RequestJson || retained.ManifestJson!=scope.Staged.ManifestJson ||
                retained.AuthorityJson!=scope.Staged.AuthorityJson || retained.HistoryJson!=scope.Staged.HistoryJson)
                throw BrowserOriginalMainCondition.Invalid();
            if(!scope.Cleanup)ValidateOriginalBrowserAdmission(retained,verifyPhysical:true);
            else foreach(var (path,expected) in new[]{("input/turn_request.json",retained.RequestJson),
                (BrowserManifestPath,retained.ManifestJson),(PendingTurnSnapshotAuthority.AuthorityPath,retained.AuthorityJson)})
                if(ReadOriginalBrowserText(path) is { } present && present!=expected)throw BrowserOriginalMainCondition.Invalid();
            return scope.Condition;
        }
        if(state.Phase=="queued")
        {
            if(marked)throw BrowserOriginalMainCondition.Invalid();
            return null;
        }
        if(state.Phase is "preparing" or "terminalProcessing")throw new InvalidOperationException("Original browser operation is incomplete; recovery retained.");
        var staged=PendingPlayerActionService.ReadStaged(state);
        var original=ValidateOriginalBrowserAdmission(staged,verifyPhysical:state.Phase=="staged");
        // Completed receipts retain their existing cleanup-only contract. Their
        // old run need not remain live; the original receipt validator still runs.
        if(state.Phase is "accepted" or "settled")return null;
        original.BrowserOriginalMainCondition!.RequireCurrent(this);
        return original.BrowserOriginalMainCondition;
    }
}
