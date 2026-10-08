using System.Diagnostics;
using System.Reflection;
using System.Runtime.ExceptionServices;
using System.Text;
using System.Text.Json;
using BookOfEternityClient.Core;
using BookOfEternityClient.Services;
using BookOfEternityClient.Services.GmRuntime;
using Microsoft.Extensions.Logging.Abstractions;

namespace BookOfEternityClient.Tests;

// Isolated original-owner scenarios. Only the test-support executable selects
// faults; the production CLI, transport and PowerShell commands have no switch.
internal static class ParticipatingControlOutcomeScenario
{
    internal const string Primary="game_state/control/f2-outcome.bin";
    internal const string Secondary="game_state/control/f2-outcome-next.bin";
    private static readonly byte[] Before=[0x41,0xff,0x00];
    private static readonly byte[] After=[0x42,0x80,0x01];
    private static readonly byte[] Next=[0x43,0x02,0xfe];
    private static readonly byte[] Unknown=[0x44,0x03,0xfd];
    private static void Require(bool condition,string message){if(!condition)throw new InvalidOperationException(message);}

    private sealed class FaultState : IDisposable
    {
        private readonly string _mode,_journal;
        private readonly Dictionary<string,object?> _evidence;
        private FileStream? _holder;
        private Exception? _forward;
        private CoordinatedStatePublicationUncertainException? _uncertain;
        private bool _closing;
        private long _lastReply;
        internal readonly FileSystemManager Files;
        internal readonly List<JsonElement> Replies=[];
        internal int Cuts,SharingRefusals,SecondLeases,Recoveries,SecondPublications,Publications,ClosingCalls;
        internal byte[]? FirstJournal;
        internal FaultState(string mode,string root,Dictionary<string,object?> evidence)
        {
            _mode=mode;_evidence=evidence;_journal=Path.Combine(root,".boe_runtime/trusted-local-publication-v1/active.json");
            Files=new(root,NullLogger<FileSystemManager>.Instance,PhysicalLoadTransactionOperations.Instance,new FileSystemManagerHooks {
                AfterCanonicalWriteLockOpenedAsync=()=>{if(_lastReply==1 && !_closing)SecondLeases++;return Task.CompletedTask;},
                SessionOperationClosingAsync=()=>{_closing=true;ClosingCalls++;return mode=="ps-closing"
                    ?Task.FromException(new IOException("actual bound finalization failure after committed control command")):Task.CompletedTask;},
                LocalPublicationRecoveryObserver=(phase,_)=>{if(phase==TrustedLocalPublicationPhase.MemberRestored)Recoveries++;},
                LocalPublicationObserver=Observe
            });
            AppDomain.CurrentDomain.FirstChanceException+=FirstChance;
        }
        private void FirstChance(object? _,FirstChanceExceptionEventArgs args)
        {
            // Evidence only: exact injected primary reference through the real
            // publisher's aggregate; never change or throw from this observer.
            if(args.Exception is CoordinatedStatePublicationUncertainException failure && _forward!=null && Contains(failure,_forward))
                Interlocked.CompareExchange(ref _uncertain,failure,null);
        }
        private static bool Contains(Exception candidate,Exception original)=>ReferenceEquals(candidate,original) ||
            candidate is AggregateException aggregate && aggregate.InnerExceptions.Any(e=>Contains(e,original)) ||
            candidate.InnerException is { } inner && Contains(inner,original);
        private void Observe(TrustedLocalPublicationPhase phase,int index)
        {
            if(phase is not (TrustedLocalPublicationPhase.MemberPublished or TrustedLocalPublicationPhase.Committed))return;
            using var parsed=JsonDocument.Parse(File.ReadAllBytes(_journal));var members=parsed.RootElement.GetProperty("Members");
            Require(members.GetArrayLength()==1,"Control command unexpectedly published a batch.");
            var path=members[0].GetProperty("Path").GetString();
            if(phase==TrustedLocalPublicationPhase.MemberPublished) {
                Require(index==0,"Control fault did not select an actual member index.");Publications++;
                if(path==Files.ResolvePath(Secondary))SecondPublications++;
            }
            var selected=_mode=="ps-rollback"?Secondary:Primary;
            if(path!=Files.ResolvePath(selected))return;
            if(_mode=="committed-debt") {
                if(phase!=TrustedLocalPublicationPhase.Committed)return;
                Require(parsed.RootElement.GetProperty("Committed").GetBoolean() && File.ReadAllBytes(path!).SequenceEqual(After),"Committed cut preceded its actual decision/bytes.");
                Cuts++;FirstJournal=File.ReadAllBytes(_journal);
                throw new InvalidOperationException("actual committed control cleanup cut");
            }
            if(phase!=TrustedLocalPublicationPhase.MemberPublished || _mode is "control" or "ps-closing")return;
            var desired=_mode=="ps-rollback"?Next:After;
            Require(File.ReadAllBytes(path!).SequenceEqual(desired),"Control cut did not reach distinct desired bytes.");
            Cuts++;FirstJournal=File.ReadAllBytes(_journal);
            if(_mode=="unknown")File.WriteAllBytes(path!,Unknown);
            if(_mode=="ps-uncertain") {
                _holder=new FileStream(path!,FileMode.Open,FileAccess.ReadWrite,FileShare.None);
                try {using var probe=new FileStream(path!,FileMode.Open,FileAccess.Read,FileShare.Read|FileShare.Delete);}
                catch(IOException failure){SharingRefusals++;_evidence["ActualSharingRefusal"]=failure.ToString();}
                Require(SharingRefusals==1,"Fixture filesystem did not enforce the actual sharing conflict; no causal uncertainty claim.");
            }
            _forward=new InvalidOperationException("actual original control publication cut");throw _forward;
        }
        internal void Reply(JsonElement reply)
        {
            Replies.Add(reply.Clone());
            if(!reply.TryGetProperty("sequence",out var seq) || seq.GetInt64()==0)return;
            _lastReply=seq.GetInt64();
            if(_lastReply!=1)return; // full first mutation reply, never active frame
            _evidence["FirstReply"]=reply.Clone();
            if(_mode=="ps-uncertain") {
                Require(Cuts==1 && SharingRefusals==1 && _holder!=null && _uncertain!=null,"Reply1 preceded actual publication/sharing/typed uncertainty.");
                _holder.Position=0;var bytes=new byte[checked((int)_holder.Length)];_holder.ReadExactly(bytes);
                Require(bytes.SequenceEqual(After) && File.ReadAllBytes(_journal).SequenceEqual(FirstJournal!),"Sharing cut lost known B or original pending journal.");
                _evidence["KnownBAndJournalAtReply1"]=true;
                _holder.Dispose();_holder=null;
                Require(File.ReadAllBytes(Files.ResolvePath(Primary)).SequenceEqual(After),"Releasing original holder changed published B.");
                _evidence["OriginalSharingHolderReleased"]=true;
            }
            else if(_mode=="unknown") {
                Require(_uncertain!=null && File.ReadAllBytes(Files.ResolvePath(Primary)).SequenceEqual(Unknown) && File.ReadAllBytes(_journal).SequenceEqual(FirstJournal!),"Original unknown carrier/bytes/journal absent at reply1.");
                _evidence["UnknownAndJournalAtReply1"]=true;
            }
            else if(_mode=="rollback")Require(File.ReadAllBytes(Files.ResolvePath(Primary)).SequenceEqual(Before) && !File.Exists(_journal),"Known command rollback did not complete before reply1.");
        }
        internal void ReleaseHolder(){_holder?.Dispose();_holder=null;}
        internal void Capture()
        {
            _evidence["Replies"]=Replies;_evidence["PublicationCuts"]=Cuts;_evidence["Publications"]=Publications;
            _evidence["SecondCommandLeases"]=SecondLeases;_evidence["ActualRecoveryMembers"]=Recoveries;
            _evidence["SecondMemberPublications"]=SecondPublications;_evidence["ClosingCalls"]=ClosingCalls;
            _evidence["OriginalUncertainFailure"]=_uncertain?.ToString();
            _evidence["PrimaryBytes"]=File.ReadAllBytes(Files.ResolvePath(Primary));
            _evidence["SecondaryBytes"]=File.ReadAllBytes(Files.ResolvePath(Secondary));
            _evidence["JournalRetained"]=File.Exists(_journal);
            if(File.Exists(_journal))_evidence["JournalBytes"]=File.ReadAllBytes(_journal);
        }
        public void Dispose(){AppDomain.CurrentDomain.FirstChanceException-=FirstChance;ReleaseHolder();}
    }

    private sealed class ReplyObservationStream(Stream inner,Action<JsonElement> observe):Stream
    {
        private readonly List<byte> _pending=[];
        private void Capture(ReadOnlySpan<byte> bytes){foreach(var b in bytes){if(b==10){using var json=JsonDocument.Parse(_pending.ToArray());observe(json.RootElement);_pending.Clear();}else _pending.Add(b);}}
        public override async ValueTask WriteAsync(ReadOnlyMemory<byte> buffer,CancellationToken token=default){await inner.WriteAsync(buffer,token);Capture(buffer.Span);}
        public override Task WriteAsync(byte[] buffer,int offset,int count,CancellationToken token)=>WriteAsync(buffer.AsMemory(offset,count),token).AsTask();
        public override void Write(byte[] buffer,int offset,int count){inner.Write(buffer,offset,count);Capture(buffer.AsSpan(offset,count));}
        public override void Flush()=>inner.Flush();public override Task FlushAsync(CancellationToken token)=>inner.FlushAsync(token);
        public override bool CanRead=>false;public override bool CanSeek=>false;public override bool CanWrite=>true;
        public override long Length=>throw new NotSupportedException();public override long Position{get=>throw new NotSupportedException();set=>throw new NotSupportedException();}
        public override int Read(byte[] b,int o,int c)=>throw new NotSupportedException();public override long Seek(long o,SeekOrigin s)=>throw new NotSupportedException();public override void SetLength(long l)=>throw new NotSupportedException();
    }

    internal static async Task<int> RunChildAsync(string mode,string root,string folder)
    {
        var evidence=new Dictionary<string,object?> { ["Mode"]=mode };
        using var state=new FaultState(mode,root,evidence);
        using var output=new ReplyObservationStream(Console.OpenStandardOutput(),state.Reply);
        try {var exit=await GmMainParticipatingControl.RunAsync(state.Files,Console.OpenStandardInput(),output);evidence["HelperExit"]=exit;return exit;}
        catch(Exception failure){evidence["HelperFailure"]=failure.ToString();return 2;}
        finally {state.ReleaseHolder();state.Capture();await File.WriteAllTextAsync(Path.Combine(folder,"control-helper.json"),JsonSerializer.Serialize(evidence));}
    }

    internal static async Task RunAsync(string mode,string root,string folder,object host,Type hostType,GmSessionRunCoordinator owner,Dictionary<string,object?> evidence)
    {
        var seed=new FileSystemManager(root,NullLogger<FileSystemManager>.Instance);
        await owner.RunOperationAsync(async()=>{await seed.WriteFileAtomicBytesAsync(Primary,Before);await seed.WriteFileAtomicBytesAsync(Secondary,Before);return 0;});
        var receipts=new List<MainOperationClose>();
        hostType.GetField("BeforeMainCloseReply",BindingFlags.Instance|BindingFlags.NonPublic)!.SetValue(host,(Func<MainOperationClose,Task>)(close=>{receipts.Add(close);return Task.CompletedTask;}));
        try {
            if(mode.StartsWith("ps-",StringComparison.Ordinal))await RunPowerShellAsync(mode,root,folder,receipts,evidence);
            else await RunDirectAsync(mode,root,receipts,evidence);
        }
        finally {evidence["ActualOriginalReceipts"]=receipts;hostType.GetField("BeforeMainCloseReply",BindingFlags.Instance|BindingFlags.NonPublic)!.SetValue(host,null);}
    }

    private static object Command(long sequence,string action,string path,byte[]? bytes=null)=>new {sequence,action,path,bytes=bytes==null?null:Convert.ToBase64String(bytes)};
    private static string? Disposition(JsonElement reply)=>reply.ValueKind==JsonValueKind.Object && reply.TryGetProperty("publicationDisposition",out var p)&&p.ValueKind==JsonValueKind.String?p.GetString():null;
    private static bool Flag(JsonElement reply,string name)=>reply.ValueKind==JsonValueKind.Object && reply.TryGetProperty(name,out var p)&&p.ValueKind==JsonValueKind.True;
    private static async Task RunDirectAsync(string mode,string root,List<MainOperationClose> receipts,Dictionary<string,object?> evidence)
    {
        using var state=new FaultState(mode,root,evidence);var commands=new List<object>();
        if(mode=="control") {
            commands.Add(Command(1,"write","game_state/core/outside-control.bin",After));
            commands.Add(new {sequence=2,action="write",path=Primary,bytes="not-base64"});
            commands.Add(Command(3,"directory","game_state/control/f2-owned-directory"));
            commands.Add(Command(4,"append",Primary,Encoding.UTF8.GetBytes("tail")));
            commands.Add(Command(5,"append","game_state/control/f2-absent-append.bin",Encoding.UTF8.GetBytes("new")));
        } else {
            commands.Add(Command(1,"write",Primary,After));
            if(mode!="committed-debt")commands.Add(Command(2,"write",Secondary,Next));
        }
        commands.Add(new {sequence=commands.Count+1,action="close",outcome=MainOperationOutcome.Completed});
        using var input=new MemoryStream(Encoding.UTF8.GetBytes(string.Join("\n",commands.Select(c=>JsonSerializer.Serialize(c)))+"\n"));
        using var bytes=new MemoryStream();using var output=new ReplyObservationStream(bytes,state.Reply);
        try {
            var exit=await GmMainParticipatingControl.RunAsync(state.Files,input,output);evidence["ControlExit"]=exit;
            state.Capture(); // collect actual second effects/close before new reply assertions
            Require(receipts.Count==1,"Control did not close exactly one original operation.");
            var mutationReplies=state.Replies.Where(r=>r.GetProperty("sequence").GetInt64()>0 && !r.TryGetProperty("state",out _)).ToArray();
            var journal=Path.Combine(root,".boe_runtime/trusted-local-publication-v1/active.json");
            if(mode=="unknown") {
                Require(state.Cuts==1 && evidence["OriginalUncertainFailure"]!=null && File.ReadAllBytes(state.Files.ResolvePath(Primary)).SequenceEqual(Unknown) && File.ReadAllBytes(journal).SequenceEqual(state.FirstJournal!),"Actual uncertain publication evidence changed.");
                Require(state.SecondLeases==0 && state.SecondPublications==0 && state.Recoveries==0,"Causal RED: caught uncertainty admitted a later command lease/recovery.");
                Require(receipts[0].Outcome==MainOperationOutcome.Uncertain,"Original close erased established command uncertainty.");
                Require(Disposition(mutationReplies[0])=="Uncertain" && Flag(mutationReplies[1],"blockedByPriorUncertainty"),"Uncertain command reply lost actual decision/continuation refusal.");
            } else if(mode=="rollback") {
                Require(state.Cuts==1 && File.ReadAllBytes(state.Files.ResolvePath(Primary)).SequenceEqual(Before) && File.ReadAllBytes(state.Files.ResolvePath(Secondary)).SequenceEqual(Next) && !File.Exists(journal),"Known rollback/later confirmed command bytes differ.");
                Require(receipts[0].Outcome==MainOperationOutcome.Completed,"Per-command rollback incorrectly relabelled the entire body.");
                Require(Disposition(mutationReplies[0])=="RolledBack" && Disposition(mutationReplies[1])=="Committed","Causal RED: actual rollback/commit decisions collapsed to bool.");
            } else if(mode=="committed-debt") {
                Require(state.Cuts==1 && File.ReadAllBytes(state.Files.ResolvePath(Primary)).SequenceEqual(After) && File.Exists(journal),"Committed control debt erased desired bytes or journal.");
                using var parsed=JsonDocument.Parse(File.ReadAllBytes(journal));Require(parsed.RootElement.GetProperty("Committed").GetBoolean(),"Cleanup fault lost durable commit.");
                Require(receipts[0].Outcome==MainOperationOutcome.Completed && Flag(mutationReplies[0],"ok") && Disposition(mutationReplies[0])=="Committed" && Flag(mutationReplies[0],"cleanupPending"),"Causal RED: committed command cleanup debt lost its bounded diagnostic.");
            } else {
                Require(!Flag(mutationReplies[0],"ok") && !Flag(mutationReplies[1],"ok") && Flag(mutationReplies[2],"ok"),"Non-publication refusal/directory semantics changed.");
                Require(mutationReplies.Take(3).All(r=>Disposition(r)==null),"Non-publication command invented a publication decision.");
                Require(Directory.Exists(state.Files.ResolvePath("game_state/control/f2-owned-directory")) && !File.Exists(state.Files.ResolvePath("game_state/core/outside-control.bin")),"Control path preparation escaped its original scope.");
                Require(File.ReadAllBytes(state.Files.ResolvePath(Primary)).SequenceEqual(Before.Concat(Encoding.UTF8.GetBytes("tail"))) &&
                    File.ReadAllBytes(state.Files.ResolvePath("game_state/control/f2-absent-append.bin")).SequenceEqual(Encoding.UTF8.GetPreamble().Concat(Encoding.UTF8.GetBytes("new"))),"Append changed original arbitrary bytes or absent BOM semantics.");
                Require(state.Publications==2 && receipts[0].Outcome==MainOperationOutcome.Completed,"Control invented extra publication or whole-body rollback.");
            }
        }
        finally {state.Capture();}
    }

    private static async Task RunPowerShellAsync(string mode,string root,string folder,List<MainOperationClose> receipts,Dictionary<string,object?> evidence)
    {
        var repo=TestRepoPaths.RepoRoot;
        var start=new ProcessStartInfo("pwsh"){UseShellExecute=false,RedirectStandardOutput=true,RedirectStandardError=true};
        foreach(var arg in new[]{"-NoProfile","-NonInteractive","-File",Path.Combine(repo,"tests/fixtures/GmMainOperation/outcomes.ps1"),"-RepoRoot",repo,"-SessionPath",Path.Combine(root,"game_session"),"-Scenario",mode,"-Folder",folder,"-TestSupport",typeof(ParticipatingControlOutcomeScenario).Assembly.Location})start.ArgumentList.Add(arg);
        using var child=Process.Start(start)!;var output=child.StandardOutput.ReadToEndAsync();var errors=child.StandardError.ReadToEndAsync();
        try {await child.WaitForExitAsync().WaitAsync(TimeSpan.FromSeconds(8));}
        finally {if(!child.HasExited){child.Kill();await child.WaitForExitAsync();}await Task.WhenAll(output,errors);}
        evidence["PowerShellExit"]=child.ExitCode;evidence["PowerShellOutput"]=await output;evidence["PowerShellError"]=await errors;
        using var helper=JsonDocument.Parse(File.ReadAllBytes(Path.Combine(folder,"control-helper.json")));
        using var report=JsonDocument.Parse(File.ReadAllBytes(Path.Combine(folder,"control-powershell.json")));
        evidence["ActualHelper"]=helper.RootElement.Clone();evidence["ActualPowerShell"]=report.RootElement.Clone();
        var actual=helper.RootElement;var ps=report.RootElement;
        Require(child.ExitCode==0 && receipts.Count==1,"Original PowerShell transport/close fixture failed: "+await errors);
        if(mode=="ps-uncertain") {
            Require(actual.GetProperty("PublicationCuts").GetInt32()==1 && actual.GetProperty("KnownBAndJournalAtReply1").GetBoolean() && actual.GetProperty("OriginalSharingHolderReleased").GetBoolean() && actual.GetProperty("OriginalUncertainFailure").ValueKind==JsonValueKind.String,"Actual recoverable uncertainty sharing cut not reached.");
            Require(actual.GetProperty("SecondCommandLeases").GetInt32()==0 && actual.GetProperty("ActualRecoveryMembers").GetInt32()==0 && actual.GetProperty("SecondMemberPublications").GetInt32()==0 && ps.GetProperty("secondRefused").GetBoolean(),"Causal RED: caught actual recoverable uncertainty admitted a second command/recovery.");
            Require(receipts[0].Outcome==MainOperationOutcome.Uncertain && ps.GetProperty("closeOutcome").GetInt32()==(int)MainOperationOutcome.Uncertain && !ps.GetProperty("lost").GetBoolean(),"Caught command uncertainty disappeared from original close or became transport loss.");
        } else if(mode=="ps-rollback") {
            Require(File.ReadAllBytes(Path.Combine(root,"game_session",Primary)).SequenceEqual(After) && File.ReadAllBytes(Path.Combine(root,"game_session",Secondary)).SequenceEqual(Before) && actual.GetProperty("PublicationCuts").GetInt32()==1,"Prior committed command or selected exact rollback lost its bytes.");
            Require(receipts[0].Outcome==MainOperationOutcome.Completed && ps.GetProperty("closeOutcome").GetInt32()==0 && ps.GetProperty("value").GetInt32()==42,"Per-command rollback incorrectly became whole-body rollback/failure.");
            Require(Disposition(ps.GetProperty("firstReply"))=="Committed" && Disposition(ps.GetProperty("failedReply"))=="RolledBack","Causal RED: PowerShell lost actual command decisions before caught failure.");
        } else {
            Require(File.ReadAllBytes(Path.Combine(root,"game_session",Primary)).SequenceEqual(After) && actual.GetProperty("Publications").GetInt32()==1 && actual.GetProperty("ClosingCalls").GetInt32()==1,"Actual committed command/finalization boundary not reached.");
            Require(receipts[0].Outcome==MainOperationOutcome.Completed && receipts[0].ClosingFailed,"Original finalization did not freeze established result/failed close separately.");
            Require(ps.GetProperty("establishedResult").GetInt32()==42,"Actual PowerShell continuation lost established body value.");
            var projected=ps.GetProperty("terminalClose").Deserialize<MainOperationClose>(MainOperationReader.Json);
            Require(projected==receipts[0] && ps.GetProperty("closeObserved").GetBoolean(),"Causal RED: PowerShell invented a close before actual failed finalization or lost its observed immutable receipt.");
        }
        if(mode!="ps-closing") {
            var projected=ps.GetProperty("terminalClose").Deserialize<MainOperationClose>(MainOperationReader.Json);
            Require(projected==receipts[0] && ps.GetProperty("closeObserved").GetBoolean(),"PowerShell effective terminal close does not match original owner receipt.");
        }
    }
}
