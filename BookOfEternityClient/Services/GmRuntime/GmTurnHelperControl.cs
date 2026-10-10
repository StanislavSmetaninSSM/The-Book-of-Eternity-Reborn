using System.Runtime.InteropServices;
using System.Text.Json;
using System.Text.Json.Serialization;
using BookOfEternityClient.Core;
using Microsoft.Extensions.Logging.Abstractions;

namespace BookOfEternityClient.Services.GmRuntime;

// Separate executable role. The ordinary participating-control allowlist cannot
// enter this interpreter or acquire its held helper lease.
internal static class GmTurnHelperControl
{
    internal const int ChunkBytes = 32768;
    private sealed class Frame
    {
        [JsonRequired] public long Sequence { get; set; }
        public string? Action { get; set; }
        public string? Mode { get; set; }
        public string? Transfer { get; set; }
        public long Length { get; set; }
        public long Offset { get; set; }
        public string? Hash { get; set; }
        public string? Bytes { get; set; }
        public MainOperationOutcome? Outcome { get; set; }
    }
    private sealed class Request
    {
        public string? Action { get; set; }
        public string? Path { get; set; }
        public string? Bytes { get; set; }
        public Dictionary<string,string?>? Expected { get; set; }
    }

    internal static async Task<int?> TryRunAsync(string[] args)
    {
        if(!args.Contains("--gm-turn-helper",StringComparer.Ordinal)) return null;
        try
        {
            if(args.Length!=5 || args[0]!="--gm-turn-helper" || args[1]!="--root" || args[3]!="--generation" || !Directory.Exists(args[2]))
                throw new InvalidDataException("An explicit helper root and initialized generation are required.");
            return await RunInterruptibleAsync(new FileSystemManager(args[2],NullLogger<FileSystemManager>.Instance),
                Console.OpenStandardInput(),Console.OpenStandardOutput(),args[4]=="initialize"?null:args[4]);
        }
#if DEBUG
        catch (Exception failure) {
            EmitFixtureFailure(failure, "outer-catch", null);
#else
        catch {
#endif
            Console.Error.WriteLine("Original GM helper admission or continuation refused; no replay."); return 2;
        }
    }

    internal static Task<int> RunAsync(FileSystemManager files,Stream input,Stream output,string? expectedGeneration) =>
        RunCoreAsync(files,input,output,expectedGeneration,CancellationToken.None,null);

    // The original foreground helper owns this signal registration. Cancellation
    // withdraws only admission; once active, the original explicit close wins.
    internal static async Task<int> RunInterruptibleAsync(FileSystemManager files,Stream input,Stream output,string? expectedGeneration)
    {
        using var cancellation=new CancellationTokenSource();
        var gate=new object();var active=false;
        using var interrupt=OperatingSystem.IsLinux()?PosixSignalRegistration.Create(PosixSignal.SIGINT,c=>{
            c.Cancel=true;lock(gate){if(!active)cancellation.Cancel();}
        }):null;
        void BeginActive(){lock(gate){cancellation.Token.ThrowIfCancellationRequested();active=true;}}
        return await RunCoreAsync(files,input,output,expectedGeneration,cancellation.Token,BeginActive);
    }

    private static async Task<int> RunCoreAsync(FileSystemManager files,Stream input,Stream output,string? expectedGeneration,CancellationToken admissionCancellation,Action? beginActive)
    {
        var reader=new MainOperationReader(input);
        var opening=await reader.ReadAsync<Frame>(CancellationToken.None)??throw new IOException("Helper opening is absent.");
        if(opening.Sequence!=0 || opening.Action!="open" || opening.Mode is not ("read" or "write" or "initialize") ||
            (expectedGeneration==null)!=(opening.Mode=="initialize")) throw new InvalidDataException("Invalid helper admission role.");
        var generation=expectedGeneration??files.ObserveExistingHelperGeneration();
        var write=opening.Mode=="write";
        var explicitClose=false; var uncertain=false; var closeReplyAttempted=false; var becameActive=false;
        var remote=false; var observed=false; var localCompleted=false;
        long sequence=0;
        var outcome=MainOperationOutcome.Completed;
        MainOperationClose? terminalClose=null;
        async Task CloseReply(bool failed)
        {
            closeReplyAttempted=true;
            await MainOperationReader.WriteAsync(output,new {ok=observed||localCompleted,state=observed||localCompleted?"closed-observed":"close-unconfirmed",
                sequence,completionKind=remote?"original-main":"local",localScopeCompleted=localCompleted,effectiveOutcome=outcome,
                terminalClose,closeObserved=observed,continuationFailed=failed},CancellationToken.None);
        }
        try
        {
            await SessionOperationContext.RunParticipatingExpectedSessionAsync(files,generation,async()=>
            {
                // Lease disposal happens before binding finalization reacquires its
                // read-only closing lease; there is never a nested lock acquisition.
                await using var lease=await files.AcquireCanonicalWriteLeaseAsync(write?CanonicalWritePurpose.SessionMutation:CanonicalWritePurpose.PublicationReadQuiescence,admissionCancellation);
                var config=await files.ReadFileBytesAsync(lease,"config.json")??throw new InvalidDataException("Initialized configuration is absent.");
                _=StateManager.PrepareLocalLoadSettings(config);
                var scope=new GmHelperCanonicalScope(files,lease,write);
                using var incoming=new MemoryStream();
                byte[]? outgoing=null; string? transfer=null; string? expectedHash=null;
                long expectedLength=0,readOffset=0;
                var receiving=false;
                try
                {
                admissionCancellation.ThrowIfCancellationRequested();beginActive?.Invoke();becameActive=true;
                await MainOperationReader.WriteAsync(output,new {ok=true,state="active",sequence,generation,
                    originalClose=files.DescribeMainOperationClose(MainOperationOutcome.Completed,false)},CancellationToken.None);
                    while(true)
                    {
                        var frame=await reader.ReadAsync<Frame>(CancellationToken.None)??throw new IOException("Original helper input was lost.");
                        if(frame.Sequence!=checked(sequence+1)) throw new InvalidDataException("Invalid helper sequence.");
                        sequence=frame.Sequence;
                        if(frame.Action=="close")
                        {
                            if(frame.Outcome==null || !Enum.IsDefined(frame.Outcome.Value)) throw new InvalidDataException("Invalid helper close.");
                            // An incomplete transfer cannot publish. Explicit close
                            // drops only this original process's uncommitted buffer.
                            outcome=uncertain?MainOperationOutcome.Uncertain:frame.Outcome.Value;
                            explicitClose=true;return 0;
                        }
                        if(uncertain)
                        {
                            await MainOperationReader.WriteAsync(output,new {ok=false,sequence,blockedByPriorUncertainty=true,errorKind="storage"},CancellationToken.None);
                            continue;
                        }
                        TrustedLocalPublicationOutcome? publication=null;
                        var replyAttempted=false;
                        async Task Reply<T>(T value)
                        {
                            replyAttempted=true;
                            await MainOperationReader.WriteAsync(output,value,CancellationToken.None);
                        }
                        try
                        {
                            if(frame.Action=="request-begin")
                            {
                                if(receiving||outgoing!=null || !Guid.TryParseExact(frame.Transfer,"N",out var id) || id.ToString("N")!=frame.Transfer ||
                                    frame.Length<0 || frame.Hash==null || frame.Hash.Length!=64 || frame.Hash.Any(c=>!char.IsAsciiHexDigit(c)) )
                                    throw new InvalidDataException("Invalid helper transfer header.");
                                transfer=frame.Transfer;expectedLength=frame.Length;expectedHash=frame.Hash;incoming.SetLength(0);receiving=true;
                                await Reply(new {ok=true,sequence});
                            }
                            else if(frame.Action=="request-chunk")
                            {
                                if(!receiving || frame.Transfer!=transfer || frame.Offset!=incoming.Length) throw new InvalidDataException("Invalid helper chunk identity/offset.");
                                var bytes=Convert.FromBase64String(frame.Bytes??throw new InvalidDataException());
                                if(bytes.Length is <1 or >ChunkBytes || checked(incoming.Length+bytes.Length)>expectedLength) throw new InvalidDataException("Invalid helper chunk length.");
                                incoming.Write(bytes);
                                await Reply(new {ok=true,sequence});
                            }
                            else if(frame.Action=="request-end")
                            {
                                if(!receiving || frame.Transfer!=transfer || incoming.Length!=expectedLength) throw new InvalidDataException("Incomplete helper transfer.");
                                var bytes=incoming.ToArray();
                                if(GmHelperCanonicalScope.Hash(bytes)!=expectedHash) throw new InvalidDataException("Helper transfer hash mismatch.");
                                receiving=false;incoming.SetLength(0);
                                var request=JsonSerializer.Deserialize<Request>(bytes,MainOperationReader.Json)??throw new InvalidDataException("Missing helper request.");
                                var path=request.Path??throw new InvalidDataException("Missing helper target.");
                                object payload;
                                switch(request.Action)
                                {
                                    case "normalize": payload=scope.Normalize(path);break;
                                    case "kind": payload=scope.Kind(path).ToString();break;
                                    case "read": payload=new {bytes=await scope.ReadAsync(path)};break;
                                    case "list": payload=scope.List(path);break;
                                    case "publish":
                                        publication=await scope.PublishAsync(path,Convert.FromBase64String(request.Bytes??throw new InvalidDataException()),request.Expected??new());
                                        // Capture the same publisher decision before facade throw,
                                        // command response or lease finalization can fail.
                                        uncertain=publication.Disposition==TrustedLocalPublicationDisposition.Uncertain;
                                        files.RequireCommittedLocalPublication(publication);
                                        payload=new {committed=true};break;
                                    default:throw new InvalidDataException("Unknown dedicated helper operation.");
                                }
                                outgoing=JsonSerializer.SerializeToUtf8Bytes(payload,MainOperationReader.Json);readOffset=0;
                                await Reply(new {ok=true,sequence,transfer,length=outgoing.LongLength,hash=GmHelperCanonicalScope.Hash(outgoing),
                                    publicationDisposition=publication?.Disposition.ToString(),cleanupPending=publication?.Failure!=null});
                            }
                            else if(frame.Action=="response-chunk")
                            {
                                if(outgoing==null || frame.Transfer!=transfer || frame.Offset!=readOffset) throw new InvalidDataException("Invalid helper response identity/offset.");
                                var count=(int)Math.Min(ChunkBytes,outgoing.LongLength-readOffset);
                                var bytes=Convert.ToBase64String(outgoing,checked((int)readOffset),count);readOffset=checked(readOffset+count);
                                var complete=readOffset==outgoing.LongLength;
                                await Reply(new {ok=true,sequence,transfer,offset=frame.Offset,bytes,complete});
                                if(complete){outgoing=null;transfer=null;}
                            }
                            else throw new InvalidDataException("Unknown helper transfer command.");
                        }
                        catch(Exception failure) when(!replyAttempted)
                        {
                            // Drop malformed/incomplete data; never retain a buffer that
                            // another command could finalize after a rejected chunk.
                            receiving=false;incoming.SetLength(0);outgoing=null;transfer=null;
                            if(failure is CoordinatedStatePublicationUncertainException) uncertain=true;
                            await Reply(new {ok=false,sequence,errorKind="storage",
                                errorCode=failure is GmHelperCanonicalScope.BaselineChangedException?"read-baseline-changed":null,
                                publicationDisposition=publication?.Disposition.ToString(),
                                publicationUncertain=uncertain,cleanupPending=publication?.Disposition==TrustedLocalPublicationDisposition.Committed&&publication.Failure!=null});
                        }
                    }
                }
                finally {if(becameActive&&!explicitClose)files.MarkMainOperationUnresolved();}
            },()=>outcome,(close,ack,wasRemote)=>{terminalClose=close;observed=ack;remote=wasRemote;},admissionCancellation);
            localCompleted=!remote;await CloseReply(false);return 0;
        }
        catch(OperationCanceledException) when(!becameActive && admissionCancellation.IsCancellationRequested)
        {
            // Original bound finalization may wait for its read-only lease. The
            // receipt below is observed only after that real fence/close settles.
            outcome=MainOperationOutcome.Cancelled;
            await MainOperationReader.WriteAsync(output,new {ok=false,state="admission-cancelled",sequence,
                terminalClose,closeObserved=observed,effectiveOutcome=outcome},CancellationToken.None);
            throw;
        }
#if DEBUG
        catch (Exception failure)
        {
            // The participating/binding finalization has already run. Capture
            // its retained original exception before this catch's close reply.
            EmitFixtureFailure(failure, "run-core-catch", new {
                mode=opening.Mode, expectedGeneration, generation, becameActive, explicitClose,
                closeReplyAttempted, sequence, uncertain, outcome, terminalClose,
                observed, remote, localCompleted
            });
#else
        catch
        {
#endif
            if(becameActive&&!explicitClose)files.MarkMainOperationUnresolved();
            else if(terminalClose!=null&&!closeReplyAttempted){try{await CloseReply(true);}catch{/* preserve original continuation failure */}}
            throw;
        }
    }

#if DEBUG
    private static void EmitFixtureFailure(Exception failure, string phase, object? state)
    {
        string? nonce=null;
        try
        {
            nonce=Environment.GetEnvironmentVariable("BOE_TEST_HELPER_FAILURE_NONCE");
            if(!Guid.TryParseExact(nonce,"N",out var parsed)||parsed.ToString("N")!=nonce)return;
            const int maxNodes=6, maxDepth=3, maxCharacters=12288, maxUtf8Bytes=49152;
            var remaining=maxCharacters;var incomplete=false;
            var omitted=new List<string>();
            var pending=new List<(Exception Failure,int Depth)> { (failure,0) };
            var nodes=new List<object>();
            string Clip(string text)
            {
                var count=Math.Min(remaining,text.Length);remaining-=count;
                if(count!=text.Length)incomplete=true;
                return text[..count];
            }
            for(var index=0;index<pending.Count;index++)
            {
                var (current,depth)=pending[index];
                string ReadText(Func<string?> read,string field)
                {
                    try{return Clip(read()??"");}
                    catch{incomplete=true;omitted.Add(index+":"+field+":unreadable");return "";}
                }
                var message=ReadText(()=>current.Message,"Message");
                var stackTrace=ReadText(()=>current.StackTrace,"StackTrace");
                var links=new Dictionary<string,int>();
                void Link(string name,Exception? linked)
                {
                    if(linked==null)return;
                    var existing=pending.FindIndex(item=>ReferenceEquals(item.Failure,linked));
                    if(existing>=0){links[name]=existing;return;}
                    if(depth>=maxDepth||pending.Count>=maxNodes){incomplete=true;omitted.Add(index+":"+name);return;}
                    links[name]=pending.Count;pending.Add((linked,depth+1));
                }
                Link("InnerException",current.InnerException);
                // Only these original lifecycle exception links are inspected;
                // no ToString, arbitrary Data enumeration or established result.
                foreach(var key in new[]{"MainOperationCloseFailure","SessionFinalizationFailure","SessionOperationFailure"})
                {
                    try{if(current.Data.Contains(key)){if(current.Data[key] is Exception linked)Link(key,linked);else{incomplete=true;omitted.Add(index+":"+key+":not-exception");}}}
                    catch{incomplete=true;omitted.Add(index+":"+key+":unreadable");}
                }
                nodes.Add(new { id=index, depth, type=current.GetType().FullName, message, stackTrace, links });
            }
            var ticks=System.Diagnostics.Stopwatch.GetTimestamp();
            object record=new {
                kind="boe-helper-fixture-failure",nonce,pid=Environment.ProcessId,phase,state,
                stopwatchTicks=ticks,stopwatchFrequency=System.Diagnostics.Stopwatch.Frequency,
                utc=DateTimeOffset.UtcNow.ToString("O"),exceptions=nodes,captureIncomplete=incomplete,
                omittedLinks=omitted,limits=new { maxNodes,maxDepth,maxCharacters,maxUtf8Bytes },
                boundary=phase=="run-core-catch"
                    ?"Original caught exception after participating/binding finalization; before this catch's optional close serialization. BecameActive is the original flag, not transport acknowledgment."
                    :"Outer catch can precede RunCore/participating admission or follow a propagated failure; no finalization occurrence is inferred."
            };
            var json=JsonSerializer.Serialize(record);
            if(System.Text.Encoding.UTF8.GetByteCount(json)>maxUtf8Bytes)
            {
                // Keep a valid bounded record; an oversized diagnostic is missing
                // causal evidence, never a reason to alter the original failure.
                var type=failure.GetType().FullName??"";type=type[..Math.Min(256,type.Length)];
                json=JsonSerializer.Serialize(new {kind="boe-helper-fixture-failure",nonce,pid=Environment.ProcessId,
                    phase,stopwatchTicks=ticks,stopwatchFrequency=System.Diagnostics.Stopwatch.Frequency,
                    captureIncomplete=true,serializationLimitExceeded=true,exceptionType=type,
                    limits=new { maxNodes,maxDepth,maxCharacters,maxUtf8Bytes }});
                if(System.Text.Encoding.UTF8.GetByteCount(json)>maxUtf8Bytes)
                    json=JsonSerializer.Serialize(new {kind="boe-helper-fixture-failure",nonce,pid=Environment.ProcessId,
                        phase,captureIncomplete=true,serializationLimitExceeded=true});
            }
            Console.Error.WriteLine(json);
        }
        catch
        {
            // Best-effort explicit observation failure; even this fallback must
            // leave the original exception/close/exit untouched.
            try {
                if(Guid.TryParseExact(nonce,"N",out var parsed)&&parsed.ToString("N")==nonce)
                    Console.Error.WriteLine(JsonSerializer.Serialize(new {kind="boe-helper-fixture-failure",nonce,
                        pid=Environment.ProcessId,phase,captureIncomplete=true,captureError=true}));
            } catch { }
        }
    }
#endif
}
