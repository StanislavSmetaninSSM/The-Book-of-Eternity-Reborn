using System.Security.Cryptography;

namespace BookOfEternityClient.Core;

// One dedicated helper's original lease. Policy remains in the existing script;
// every input it consumes is witnessed again at the original publication cut.
internal sealed class GmHelperCanonicalScope
{
    private readonly FileSystemManager _files;
    private readonly FileSystemManager.CanonicalWriteLease _lease;
    private readonly TrustedLocalFileScope _scope;
    private readonly bool _canWrite;
    private readonly StringComparer _comparer = OperatingSystem.IsWindows() ? StringComparer.OrdinalIgnoreCase : StringComparer.Ordinal;
    private readonly Dictionary<string, string?> _reads;
    private readonly Dictionary<string, TrustedLocalNamespaceKind> _kinds;
    private readonly Dictionary<string, string[]> _trees;

    internal GmHelperCanonicalScope(FileSystemManager files, FileSystemManager.CanonicalWriteLease lease, bool canWrite)
    {
        _files=files; _lease=lease; _canWrite=canWrite;
        _scope=new TrustedLocalFileScope([files.GameSessionPath]);
        _reads=new(_comparer); _kinds=new(_comparer); _trees=new(_comparer);
    }

    internal string Normalize(string path)
    {
        _files.VerifyCurrentSessionOperation(_lease);
        if (string.IsNullOrWhiteSpace(path)) throw new InvalidDataException("A helper target is required.");
        var absolute=Path.GetFullPath(Path.IsPathRooted(path) ? path : Path.Combine(_files.GameSessionPath,path.Replace('/',Path.DirectorySeparatorChar)));
        absolute=_scope.ValidateNamespacePath(absolute);
        var observation=_scope.ObserveNamespace(absolute);
        if(observation.BlockingFileAncestor!=null) throw new InvalidDataException("A helper target has a file ancestor.");
        return FileSystemManager.GetLocalRelativePath(_files.GameSessionPath,absolute,OperatingSystem.IsWindows());
    }

    internal async Task<byte[]?> ReadAsync(string path)
    {
        path=Normalize(path);
        var bytes=await _files.ReadFileBytesAsync(_lease,path);
        _reads.TryAdd(path,Hash(bytes));
        return bytes;
    }

    internal TrustedLocalNamespaceKind Kind(string path)
    {
        path=Normalize(path);
        var kind=_scope.ObserveNamespace(_files.ResolvePath(path)).Kind;
        _kinds.TryAdd(path,kind);
        return kind;
    }

    internal string[] List(string path)
    {
        path=Normalize(path);
        var entries=_files.EnumerateCanonicalLocalTreeFiles(_lease,path).OrderBy(x=>x,_comparer).ToArray();
        _trees.TryAdd(path,entries);
        return entries;
    }

    internal async Task<TrustedLocalPublicationOutcome> PublishAsync(string path,byte[] bytes,IReadOnlyDictionary<string,string?> expected)
    {
        if(!_canWrite) throw new InvalidDataException("Read-only helper admission cannot publish.");
        _files.EnsureWorkerGeneralMutationAllowed(_lease);
        path=Normalize(path);
        var before=await ReadAsync(path);
        var supplied=new Dictionary<string,string?>(_comparer);
        foreach(var item in expected)
            if(!supplied.TryAdd(Normalize(item.Key),item.Value)) throw new InvalidDataException("Duplicate helper witness target.");
        async Task ValidateAsync()
        {
            _files.VerifyCurrentSessionOperation(_lease);
            foreach(var item in _reads)
                if(Hash(await _files.ReadFileBytesAsync(_lease,item.Key))!=item.Value) throw new InvalidDataException("A helper read witness changed.");
            foreach(var item in supplied)
                if(Hash(await _files.ReadFileBytesAsync(_lease,item.Key))!=item.Value) throw new InvalidDataException("A helper baseline changed.");
            foreach(var item in _kinds)
                if(_scope.ObserveNamespace(_files.ResolvePath(item.Key)).Kind!=item.Value) throw new InvalidDataException("A helper namespace witness changed.");
            foreach(var item in _trees)
                if(!_files.EnumerateCanonicalLocalTreeFiles(_lease,item.Key).OrderBy(x=>x,_comparer).SequenceEqual(item.Value,_comparer))
                    throw new InvalidDataException("A helper enumeration witness changed.");
            _files.VerifyCurrentSessionOperation(_lease);
        }
        await ValidateAsync();
        var result=await _files.PublishLocalFilesAsync(_lease,[new(path,before,bytes)],validatePreparedNamespace:ValidateAsync);
        if(result.Disposition==TrustedLocalPublicationDisposition.Committed)
        {
            _reads[path]=Hash(bytes);
            if(_kinds.ContainsKey(path)) _kinds[path]=TrustedLocalNamespaceKind.File;
            foreach(var tree in _trees.Keys.ToArray())
                if(IsDescendant(path,tree) && !_trees[tree].Contains(path,_comparer))
                    _trees[tree]=_trees[tree].Append(path).OrderBy(x=>x,_comparer).ToArray();
            foreach(var parent in _kinds.Keys.ToArray())
                if(IsDescendant(path,parent)) _kinds[parent]=TrustedLocalNamespaceKind.Directory;
        }
        return result;
    }

    private bool IsDescendant(string path,string root) => path.Length>root.Length && path[root.Length]=='/' &&
        path.StartsWith(root,OperatingSystem.IsWindows()?StringComparison.OrdinalIgnoreCase:StringComparison.Ordinal);
    internal static string? Hash(byte[]? bytes) => bytes==null?null:Convert.ToHexString(SHA256.HashData(bytes)).ToLowerInvariant();
}
