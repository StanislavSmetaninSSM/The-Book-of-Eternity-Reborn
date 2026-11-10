using System.Text.Json;
using BookOfEternityClient.Core;

namespace BookOfEternityClient.Services.GmRuntime;

internal static partial class GmMainParticipatingControl
{
    // One exchange on the existing connection. Only buffers survive between
    // frames; canonical leases never span caller parsing, dispatch or waits.
    private sealed class DaemonExchange : IDisposable
    {
        private const int ChunkBytes=32768;
        private readonly MemoryStream incoming=new();
        private string? transfer,expectedHash;
        private long length,offset;
        private bool receiving;
        private byte[]? outgoing;
        private sealed class Request
        {
            public string? Action {get;set;}
            public string[] Paths {get;set;}=[];
            public string[] Trees {get;set;}=[];
            public Dictionary<string,string?> Expected {get;set;}=new();
            public string? Path {get;set;}
            public string? Bytes {get;set;}
            public string? Generation {get;set;}
        }
        private sealed class WitnessChangedException : InvalidOperationException { }
        private sealed record FileObservation(string Path,string Kind,byte[]? Bytes,string? Hash,DateTime? LastWriteTimeUtc);
        private sealed record TreeObservation(string Path,string[] Files);
        internal bool HasPending=>receiving||outgoing!=null;
        internal void Reset(){incoming.SetLength(0);outgoing=null;transfer=null;receiving=false;}
        public void Dispose()=>incoming.Dispose();

        internal async Task<Dictionary<string,object?>> HandleAsync(FileSystemManager files,Command command,CommandResult result)
        {
            try
            {
                switch(command.Action)
                {
                    case "daemon-request-begin":
                        if(receiving||outgoing!=null||!Guid.TryParseExact(command.Transfer,"N",out var id)||id.ToString("N")!=command.Transfer||
                            command.Length<0||command.Hash?.Length!=64||command.Hash.Any(c=>!char.IsAsciiHexDigit(c)))
                            throw new InvalidDataException("Invalid daemon exchange header.");
                        transfer=command.Transfer;length=command.Length;expectedHash=command.Hash;incoming.SetLength(0);receiving=true;
                        return new();
                    case "daemon-request-chunk":
                        if(!receiving||command.Transfer!=transfer||command.Offset!=incoming.Length)throw new InvalidDataException("Invalid daemon chunk.");
                        var chunk=Convert.FromBase64String(command.Bytes??throw new InvalidDataException());
                        if(chunk.Length is <1 or >ChunkBytes||checked(incoming.Length+chunk.Length)>length)throw new InvalidDataException("Invalid daemon chunk length.");
                        incoming.Write(chunk);return new();
                    case "daemon-request-end":
                        if(!receiving||command.Transfer!=transfer||incoming.Length!=length)throw new InvalidDataException("Incomplete daemon request.");
                        var bytes=incoming.ToArray();
                        if(GmHelperCanonicalScope.Hash(bytes)!=expectedHash)throw new InvalidDataException("Daemon request hash mismatch.");
                        receiving=false;incoming.SetLength(0);
                        var request=JsonSerializer.Deserialize<Request>(bytes,MainOperationReader.Json)??throw new InvalidDataException();
                        object payload;
                        if(request.Action=="snapshot")
                        {
                            try {payload=await SnapshotAsync(files,request);}
                            catch {result.ReadRefused=true;throw;}
                        }
                        else if(request.Action is "write-if-current" or "delete-if-current")payload=await ConditionalAsync(files,request,result);
                        else throw new InvalidDataException("Unknown daemon request.");
                        outgoing=JsonSerializer.SerializeToUtf8Bytes(payload,MainOperationReader.Json);offset=0;
                        return new(){["transfer"]=transfer,["length"]=outgoing.LongLength,["hash"]=GmHelperCanonicalScope.Hash(outgoing)};
                    case "daemon-response-chunk":
                        if(outgoing==null||command.Transfer!=transfer||command.Offset!=offset)throw new InvalidDataException("Invalid daemon response offset.");
                        var count=(int)Math.Min(ChunkBytes,outgoing.LongLength-offset);
                        var encoded=Convert.ToBase64String(outgoing,checked((int)offset),count);offset=checked(offset+count);
                        var complete=offset==outgoing.LongLength;
                        var reply=new Dictionary<string,object?>{["transfer"]=transfer,["offset"]=command.Offset,["bytes"]=encoded,["complete"]=complete};
                        if(complete){outgoing=null;transfer=null;}return reply;
                    default:throw new InvalidDataException("Unknown daemon exchange command.");
                }
            }
            catch {Reset();throw;}
        }

        private static async Task<object> SnapshotAsync(FileSystemManager files,Request request)
        {
            if(request.Generation!=null&&(!SessionOperationContext.TryGetExpectedGeneration(files.BasePath,out var bound)||bound!=request.Generation))return new {matches=false};
            await using var lease=await files.AcquireCanonicalWriteLeaseAsync(CanonicalWritePurpose.PublicationReadQuiescence);
            var scope=new GmHelperCanonicalScope(files,lease,false);
            if(request.Generation!=null&&files.ReadLocalGenerationSnapshot(lease).Binding.Id!=request.Generation)return new {matches=false};
            if(!await MatchesAsync(files,lease,scope,request.Expected))return new {matches=false};
            var comparer=OperatingSystem.IsWindows()?StringComparer.OrdinalIgnoreCase:StringComparer.Ordinal;
            var paths=new HashSet<string>(request.Paths.Select(scope.Normalize),comparer);
            var trees=new List<TreeObservation>();
            foreach(var tree in request.Trees)
            {
                var path=scope.Normalize(tree);var members=scope.List(path);
                trees.Add(new(path,members));foreach(var member in members)paths.Add(member);
            }
            var observations=new List<FileObservation>();
            foreach(var path in paths.OrderBy(p=>p,comparer))
            {
                var kind=scope.Kind(path);
                var snapshot=kind==TrustedLocalNamespaceKind.File?await files.ReadFileSnapshotAsync(lease,path):null;
                observations.Add(new(path,kind.ToString(),snapshot?.Content,GmHelperCanonicalScope.Hash(snapshot?.Content),snapshot?.LastWriteTimeUtc));
            }
            // The dynamic manifest/request witnesses stay exact through the whole
            // batch, including admitted read hooks. No JSON grants authority.
            if(!await MatchesAsync(files,lease,scope,request.Expected))return new {matches=false};
            files.VerifyCurrentSessionOperation(lease);
            return new {matches=true,generation=files.ReadLocalGenerationSnapshot(lease).Binding.Id,files=observations,trees};
        }

        private static async Task<bool> MatchesAsync(FileSystemManager files,FileSystemManager.CanonicalWriteLease lease,
            GmHelperCanonicalScope scope,IReadOnlyDictionary<string,string?> expected)
        {
            files.VerifyCurrentSessionOperation(lease);
            foreach(var entry in expected)
                if(GmHelperCanonicalScope.Hash(await files.ReadFileBytesAsync(lease,scope.Normalize(entry.Key)))!=entry.Value)return false;
            files.VerifyCurrentSessionOperation(lease);return true;
        }

        private static async Task<object> ConditionalAsync(FileSystemManager files,Request request,CommandResult result)
        {
            // Existing mutation role is unchanged; broader reads cannot create a
            // broader writer. The target itself must have an explicit witness.
            var path=NormalizeControlPath(request.Path);
            if(request.Generation==null||!SessionOperationContext.TryGetExpectedGeneration(files.BasePath,out var bound)||bound!=request.Generation)return new {matches=false};
            await using var lease=await files.AcquireCanonicalWriteLeaseAsync();
            files.EnsureWorkerGeneralMutationAllowed(lease);
            var scope=new GmHelperCanonicalScope(files,lease,false);
            var comparer=OperatingSystem.IsWindows()?StringComparer.OrdinalIgnoreCase:StringComparer.Ordinal;
            var expected=new Dictionary<string,string?>(comparer);
            foreach(var entry in request.Expected)
                if(!expected.TryAdd(scope.Normalize(entry.Key),entry.Value))throw new InvalidDataException("Duplicate daemon witness.");
            if(!expected.ContainsKey(path))throw new InvalidDataException("Missing daemon target witness.");
            async Task ValidateAsync(){if(request.Generation==null||files.ReadLocalGenerationSnapshot(lease).Binding.Id!=request.Generation||!await MatchesAsync(files,lease,scope,expected))throw new WitnessChangedException();}
            try
            {
                await ValidateAsync();
                var before=await files.ReadFileBytesAsync(lease,path);
                var after=request.Action=="delete-if-current"?null:Convert.FromBase64String(request.Bytes??throw new InvalidDataException());
                result.Publication=await files.PublishLocalFilesAsync(lease,[new(path,before,after)],validatePreparedNamespace:ValidateAsync);
                files.RequireCommittedLocalPublication(result.Publication);
                return new {matches=true};
            }
            catch(WitnessChangedException){return new {matches=false};}
        }
    }
}
