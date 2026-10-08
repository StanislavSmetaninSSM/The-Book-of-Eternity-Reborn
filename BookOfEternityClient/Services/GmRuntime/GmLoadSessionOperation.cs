using System.IO.Pipes;
using System.Runtime.CompilerServices;
using System.Text.Json;
using BookOfEternityClient.Core;

namespace BookOfEternityClient.Services.GmRuntime;

// One original live operation per root. This is not a durable journal or cold replay cache.
internal sealed class GmLoadSessionOperation : IAsyncDisposable
{
    private sealed class RootState
    {
        internal GmLoadSessionOperation? Active;
        internal bool Uncertain;
        internal readonly HashSet<string> Used=new(StringComparer.Ordinal);
    }
    private static readonly ConditionalWeakTable<CanonicalRootIdentity,RootState> Roots=new();
    private readonly FileSystemManager _files;
    private readonly RootState _root;
    private NamedPipeClientStream? _pipe;
    private MainOperationReader? _reader;
    private GmLoadSessionReply? _stop;
    private bool _finished;
    internal string Id {get;}
    internal string SourceKey {get;}
    internal string? ExpectedGeneration {get;private set;}
    internal GmLoadMainState State {get;private set;}=GmLoadMainState.Refused;
    internal bool HadSession=>_stop?.State==GmLoadMainState.Stopped;

    private GmLoadSessionOperation(FileSystemManager files,RootState root,string id,string source,string? generation)
    {_files=files;_root=root;Id=id;SourceKey=source;ExpectedGeneration=generation;}
    internal static GmLoadSessionOperation Reserve(FileSystemManager files,string id,string source,string? generation)
    {
        if(!Guid.TryParseExact(id,"N",out _) || string.IsNullOrWhiteSpace(source))throw new InvalidDataException("Immutable Load identity is incomplete.");
        var root=Roots.GetValue(files.CanonicalRootAuthorityIdentity,_=>new());
        lock(root) {
            if(root.Active!=null || root.Uncertain || root.Used.Count>=128 || !root.Used.Add(id))
                throw new InvalidOperationException("Load is already active, unresolved or this operation was used.");
            return root.Active=new(files,root,id,source,generation);
        }
    }
    internal async Task StopAsync(CancellationToken token=default)
    {
        _files.RequireLoadIpcOutsideFileScopes();
        try {
            var bytes=GmSessionRunPersistence.Read(_files.BasePath);
            var expected=bytes==null?null:GmSessionRunRecordCodec.Decode(bytes);
            if(expected!=null && expected.Disposition is not (GmSessionRunDisposition.Running or GmSessionRunDisposition.Stopped))
                throw new InvalidOperationException("Original main is not stoppable from this Load.");
            ExpectedGeneration??=expected?.Disposition==GmSessionRunDisposition.Running?expected.Identity.GenerationId:ReadGenerationExpectation();
            var status=await _files.ReadDiagnosticStatusAsync("game_state/control/gm_bridge_status.json");
            if(string.IsNullOrWhiteSpace(status)) {
                if(expected?.Disposition==GmSessionRunDisposition.Running)throw new IOException("Original running bridge endpoint is absent.");
                State=GmLoadMainState.NoActiveSession;_stop=new(true,Id,State);return;
            }
            using var doc=JsonDocument.Parse(status);
            var name=doc.RootElement.GetProperty("pipeName").GetString();
            if(string.IsNullOrWhiteSpace(name) || name.Length>128)throw new InvalidDataException("Original bridge endpoint is invalid.");
            var terminal=doc.RootElement.TryGetProperty("terminalRunId",out var run)?run.GetString():null;
            _pipe=new(".",name,PipeDirection.InOut,PipeOptions.Asynchronous);
            using var bounded=CancellationTokenSource.CreateLinkedTokenSource(token);bounded.CancelAfter(TimeSpan.FromSeconds(15));
            await _pipe.ConnectAsync(bounded.Token);_reader=new(_pipe);
            await MainOperationReader.WriteAsync(_pipe,new {command="beginLoadSession",operationId=Id,rootKey=_files.BasePath,
                loadSourceKey=SourceKey,expectedGeneration=ExpectedGeneration,expectedTerminalRunId=terminal,
                expectedMainIdentity=expected?.Disposition==GmSessionRunDisposition.Running?expected.Identity:null},bounded.Token);
            _stop=await _reader.ReadAsync<GmLoadSessionReply>(bounded.Token)??throw new IOException("Original stop receipt lost.");
            if(!_stop.Ok || _stop.OperationId!=Id || _stop.State is not (GmLoadMainState.Stopped or GmLoadMainState.NoActiveSession))
                throw new IOException("Original stop/disposal receipt is unconfirmed.");
            if(expected?.Disposition==GmSessionRunDisposition.Running &&
                (_stop.State!=GmLoadMainState.Stopped || !GmSessionRunValidation.IdentityMatches(expected.Identity,_stop.MainIdentity!)))
                throw new InvalidDataException("Stop receipt belongs to another original main.");
            if(_stop.State==GmLoadMainState.Stopped && (_stop.TerminalIdentity==null || _stop.TerminalIdentity.RunId!=terminal))
                throw new InvalidDataException("Stop receipt belongs to another original terminal.");
            State=_stop.State;
        } catch {State=GmLoadMainState.Uncertain;throw;}
    }
    private string? ReadGenerationExpectation()
    {
        if(!File.Exists(_files.SessionGenerationPath))return null;
        // Share the actual writer/reader schema; this expectation cannot grant authority.
        return FileSystemManager.ParseSessionGenerationText(File.ReadAllText(_files.SessionGenerationPath));
    }
    internal async Task ValidateLoadGenerationAsync()
    {
        await using var lease=await _files.AcquireCanonicalWriteLeaseAsync();
        if(ExpectedGeneration!=null && _files.ReadExistingSessionGeneration(lease)!=ExpectedGeneration)
            throw new InvalidDataException("The selected view generation is stale.");
    }
    internal async Task<GmLoadMainState> FinishAsync(bool committed,bool refreshConfirmed,string? generation)
    {
        _files.RequireLoadIpcOutsideFileScopes();
        if(_finished || _stop==null)throw new InvalidOperationException("Original Load finish is absent or already consumed.");
        _finished=true;
        if(_pipe==null)return State=GmLoadMainState.NoActiveSession;
        try {
            using var deadline=new CancellationTokenSource(TimeSpan.FromSeconds(15));
            await MainOperationReader.WriteAsync(_pipe,new GmLoadSessionFrame("finishLoadSession",Id,committed,refreshConfirmed,generation),deadline.Token);
            var reply=await _reader!.ReadAsync<GmLoadSessionReply>(deadline.Token)??throw new IOException("Original restart receipt lost.");
            if(!reply.Ok || reply.OperationId!=Id || !Enum.IsDefined(reply.State))throw new IOException("Original Load completion is unconfirmed.");
            if(reply.State is GmLoadMainState.Running or GmLoadMainState.StartedNotReady) {
                GmSessionRunValidation.ValidateIdentity(reply.MainIdentity);
                var trustedBackend=OperatingSystem.IsWindows()?GmSessionRunBackend.WindowsJob:GmSessionRunBackend.LinuxSupervisor;
                var currentBytes=GmSessionRunPersistence.Read(_files.BasePath);
                var current=currentBytes==null?null:GmSessionRunRecordCodec.Decode(currentBytes);
                // Current metadata is a refusal/consistency check paired with this
                // retained original connection, never authority to adopt a run.
                if(!committed || !refreshConfirmed || !HadSession || current?.Disposition!=GmSessionRunDisposition.Running ||
                    current.Identity.Backend!=trustedBackend ||
                    !GmSessionRunValidation.AdmissionRootMatches(current.Identity.RootKey,_files.BasePath,trustedBackend) ||
                    !GmSessionRunValidation.IdentityMatches(current.Identity,reply.MainIdentity!) ||
                    reply.MainIdentity!.GenerationId!=generation || reply.MainIdentity.RunId==_stop.MainIdentity?.RunId ||
                    reply.TerminalIdentity?.RunId!=reply.MainIdentity.RunId)
                    throw new InvalidDataException("Fresh launch receipt is not bound to the installed replacement.");
            }
            return State=reply.State;
        } catch {State=GmLoadMainState.Uncertain;throw;}
    }
    public ValueTask DisposeAsync()
    {
        _pipe?.Dispose();
        lock(_root) {
            if(State==GmLoadMainState.Uncertain || (_stop!=null && !_finished))_root.Uncertain=true;
            if(ReferenceEquals(_root.Active,this))_root.Active=null;
        }
        return ValueTask.CompletedTask;
    }
}
