using System.IO.Pipes;
using System.Text;
using System.Text.Json;
using BookOfEternityClient.Configuration;
using BookOfEternityClient.Services.GmRuntime;

namespace BookOfEternityGMBridge;

internal sealed partial class BridgeHost
{
    private string? _draftLaunchBinding;
    private InputLifetime? _draftLaunchInput;

    private ProductionMainConfiguration ConfigureDraftObservation(ProductionMainConfiguration c,GmCliInputProfile p)
    {
        _draftLaunchBinding=null;_draftLaunchInput=null;
        if(p.TerminalPresentation!="synchronized-mini-v1")return c;
        if(!p.IsSupported || p.DraftDirectory!=System.IO.Path.GetFullPath(p.DraftDirectory) || !Directory.Exists(p.DraftDirectory))
            throw new InvalidDataException("Bounded draft observation requires an existing canonical temporary directory and one-use profile.");
        var apphost=System.IO.Path.Combine(AppContext.BaseDirectory,"BookOfEternityGMBridge");
        if(!File.Exists(apphost) || apphost.Contains(' ') || apphost.Any(char.IsWhiteSpace))
            throw new InvalidDataException("Packaged observer apphost must exist and be representable by the configured external editor.");
        _draftLaunchBinding=Guid.NewGuid().ToString("N");
        var endpoint=System.IO.Path.IsPathFullyQualified(_pipeName)?_pipeName:System.IO.Path.Combine(System.IO.Path.GetTempPath(),"CoreFxPipe_"+_pipeName);
        return c with {ChildEnvironment=new Dictionary<string,string?> {
            ["VISUAL"]=apphost+" --observe-draft",["EDITOR"]=apphost+" --observe-draft",
            ["BOE_DRAFT_DIRECTORY"]=p.DraftDirectory,["BOE_DRAFT_BINDING"]=_draftLaunchBinding,["BOE_DRAFT_PIPE"]=endpoint,["TMPDIR"]=p.DraftDirectory
        }};
    }

    private bool PrepareMiniOperation(PromptOperation op)
    {
        var text=op.Snapshot.Text;
        // No normalization or glyph/edge guess is allowed for this pinned first-turn profile.
        if(!ReferenceEquals(_draftLaunchInput,op.Input) || string.IsNullOrEmpty(_draftLaunchBinding) ||
            text.Contains('\r') || text.Contains('\t') || text.EndsWith('\n') || Encoding.UTF8.GetByteCount(text)>65536 ||
            text.Split('\n')[0].Length==0 || text.Any(c=>c!='\n' && !SupportedMiniGlyph(c)))return false;
        var entries=Directory.EnumerateFiles(op.Snapshot.Profile.DraftDirectory).Take(1025).ToArray();
        if(entries.Length>1024)return false;
        var lines=text.Split('\n');
        var first=lines[0].Split(' ')[0];var last=lines[^1][(lines[^1].LastIndexOf(' ')+1)..];
        if(first.Length==0 || last.Length==0)return false;
        op.FirstEdge=first[..Math.Min(24,first.Length)];op.LastEdge=last[Math.Max(0,last.Length-24)..];
        op.ExistingDrafts=entries;return true;
    }
    private static bool SupportedMiniGlyph(char c)=>SynchronizedTerminalScreen.SupportedCell(c);

    private async Task ServeDraftObservationAsync(NamedPipeServerStream stream,MainOperationReader reader,BridgeRequest request,CancellationToken token)
    {
        PromptOperation? op=null;
        using var bounded=CancellationTokenSource.CreateLinkedTokenSource(token);
        bounded.CancelAfter(TimeSpan.FromSeconds(3));
        try
        {
            string nonce;
            lock(_sync)
            {
                if(request.Binding!=_draftLaunchBinding || _draftLaunchInput==null)throw new IOException("Original draft launch not retained.");
                op=_promptOperations.Values.SingleOrDefault(o=>ReferenceEquals(o.Input,_draftLaunchInput) && o.DraftArmed && !o.DraftClaimed && o.Result==null);
                if(op==null || !PromptStillOwned(op) || op.Phase!=PromptDeliveryPhase.AwaitingPaste || request.Path==null ||
                    op.ExistingDrafts.Contains(request.Path,StringComparer.Ordinal))throw new IOException("Original awaiting operation not available.");
                // The original typed operation/connection claims once, before proof bytes.
                op.DraftClaimed=true;nonce=Guid.NewGuid().ToString("N");
            }
            using var linked=CancellationTokenSource.CreateLinkedTokenSource(bounded.Token,op.Cancellation.Token,op.Input.Token);
            await MainOperationReader.WriteAsync(stream,new {ok=true,nonce},linked.Token);
            var frame=await reader.ReadAsync<JsonElement>(linked.Token,100000);
            if(frame.GetProperty("nonce").GetString()!=nonce)throw new IOException("Original challenge mismatch.");
            var proof=frame.Deserialize<DraftFileProof>(MainOperationReader.Json)??throw new IOException("No actual file proof.");
            if(proof.Path!=request.Path)throw new IOException("Actual proof path mismatch.");
            if(proof!=DraftObservation.Read(op.Snapshot.Profile.DraftDirectory,proof.Path))throw new IOException("Actual proof changed before ACK.");
            if(!Convert.FromBase64String(proof.Bytes).AsSpan().SequenceEqual(Encoding.UTF8.GetBytes(op.Snapshot.Text)))throw new IOException("Actual immutable bytes mismatch.");
            lock(_sync)if(!PromptStillOwned(op) || op.Result!=null || !op.DraftArmed)throw new IOException("Original operation revoked before ACK.");
            await MainOperationReader.WriteAsync(stream,new {ok=true},linked.Token);
            var completed=await reader.ReadAsync<JsonElement>(linked.Token);
            if(completed.ValueKind!=JsonValueKind.Object || completed.GetProperty("command").GetString()!="draftComplete" ||
                completed.GetProperty("nonce").GetString()!=nonce || completed.GetProperty("path").GetString()!=proof.Path ||
                DraftObservation.Read(op.Snapshot.Profile.DraftDirectory,proof.Path)!=proof)
                throw new IOException("Actual helper final read did not complete on this original connection.");
            lock(_sync)if(!PromptStillOwned(op) || op.Result!=null)throw new IOException("Original operation lost before completion ACK.");
            await MainOperationReader.WriteAsync(stream,new {ok=true,nonce},linked.Token);
            // EOF settles a positively completed original connection, never supplies success itself.
            if(await reader.ReadAsync<JsonElement?>(linked.Token)!=null)throw new IOException("Unexpected observer continuation.");
            lock(_sync)if(!PromptStillOwned(op) || op.Result!=null)throw new IOException("Original operation lost before completion.");
            op.DraftProof.TrySetResult(proof);
        }
        catch(Exception ex)
        {
            op?.DraftProof.TrySetException(new IOException("Original draft witness refused.",ex));
            try{await MainOperationReader.WriteAsync(stream,new {ok=false},bounded.Token);}catch{}
        }
    }

    private async Task<bool> ObserveMiniDraftAsync(PromptOperation op,CancellationToken token)
    {
        long version;
        lock(_sync){if(!PromptStillOwned(op) || !IsMiniEdge(op,false))return false;version=PromptObservationVersion;op.DraftArmed=true;}
        await WriteMiniGestureAsync(op,"\u0018e",()=>IsMiniEdge(op,false),token);
        var proof=await op.DraftProof.Task.WaitAsync(TimeSpan.FromMilliseconds(op.Snapshot.Profile.ObservationTimeoutMilliseconds),token);
        if(!await ObservePromptAsync(op,version,screen=>DraftObservation.IsAbsent(op.Snapshot.Profile.DraftDirectory,proof.Path) && MiniDraftRegion(op,out _,out _,out _),token))return false;
        lock(_sync){if(!PromptStillOwned(op))return false;version=PromptObservationVersion;}
        await WriteMiniGestureAsync(op,"\u001b[H",()=>DraftObservation.IsAbsent(op.Snapshot.Profile.DraftDirectory,proof.Path) && MiniDraftRegion(op,out _,out _,out _),token);
        if(!await ObservePromptAsync(op,version,_=>IsMiniEdge(op,true),token))return false;
        lock(_sync){if(!PromptStillOwned(op))return false;version=PromptObservationVersion;}
        await WriteMiniGestureAsync(op,"\u001b[F",()=>DraftObservation.IsAbsent(op.Snapshot.Profile.DraftDirectory,proof.Path) && IsMiniEdge(op,true),token);
        if(!await ObservePromptAsync(op,version,_=>IsMiniEdge(op,false),token))return false;
        lock(_sync)op.DraftArmed=false;
        return true;
    }

    private Task WriteMiniGestureAsync(PromptOperation op,string bytes,Func<bool> currentView,CancellationToken token)
        => WriteToPtyCoreAsync(op.Input,bytes,false,token,()=>PromptStillOwned(op) && currentView());
}
