using System.Diagnostics;
using System.Text;
using System.Text.Json;
using BookOfEternityClient.Configuration;
using BookOfEternityClient.Core;
using BookOfEternityClient.Services.GmRuntime;
using Microsoft.Extensions.Logging.Abstractions;

namespace BookOfEternityClient.Tests;

// Test-only original-process hooks. No production CLI selector or fake receipt.
internal static class GmDaemonStorageScenario
{
    private const string Request="input/qte_effect_resolution_request.json";
    private const string Ready="ready/qte_effect_resolution_complete.json";
    private static void Require(bool value,string message){if(!value)throw new InvalidOperationException(message);}
    private static byte[] Packet(string tag,bool ready=false)=>JsonSerializer.SerializeToUtf8Bytes(new {
        requestKind="qte_deferred_effect_resolution",status=ready?"success":null,
        sessionId="daemon-fixture",sessionGeneration="packet-generation",continuationId="continuation-"+tag,
        requestId="request-"+tag,waveId="wave-"+tag,waveOrdinal=1,acceptedSourceTurn=3,qteId="qte-fixture",
        pendingStateFingerprint="fingerprint-"+tag,safePacket=new {requests=Array.Empty<object>()}
    });

    internal static async Task<int> RunChildAsync(string root,string folder)
    {
        var evidence=new Dictionary<string,object?> { ["ProcessId"]=Environment.ProcessId };
        var lastAction="admission";var contention=0;var reads=0;var recoveries=0;
        var files=new FileSystemManager(root,NullLogger<FileSystemManager>.Instance,PhysicalLoadTransactionOperations.Instance,new FileSystemManagerHooks {
            CanonicalWriteLockContendedAsync=()=>{
                Interlocked.Increment(ref contention);
                PublishTechnicalMarker(Path.Combine(folder,"canonical-contended.json"),JsonSerializer.Serialize(new {Action=lastAction,Count=contention}));
                return Task.CompletedTask;
            },
            BeforeCanonicalReadOpenAsync=_=>{Interlocked.Increment(ref reads);return Task.CompletedTask;},
            LocalPublicationRecoveryObserver=(_,_)=>Interlocked.Increment(ref recoveries)
        });
        using var input=new ObservedInput(Console.OpenStandardInput(),action=>lastAction=action);
        var exit=2;
        try {exit=await GmMainParticipatingControl.RunAsync(files,input,Console.OpenStandardOutput());evidence["Completed"]=true;return exit;}
        catch(Exception failure){evidence["Failure"]=failure.ToString();return exit;}
        finally {
            evidence["ExitCode"]=exit;evidence["Contentions"]=contention;evidence["CanonicalReads"]=reads;evidence["RecoveryPhases"]=recoveries;
            await File.WriteAllTextAsync(Path.Combine(folder,"daemon-helper.json"),JsonSerializer.Serialize(evidence));
        }
    }

    internal static async Task ExerciseAsync(string mode,FileSystemManager files,GmSessionRunCoordinator owner,string folder,
        Func<Task> stop,Dictionary<string,object?> evidence)
    {
        var isReady=mode.StartsWith("ready-",StringComparison.Ordinal);
        var target=isReady?Ready:Request;var before=isReady?null:Packet("A");var after=isReady?Packet("A",true):Packet("B");var third=Packet("C");
        var journal=Path.Combine(files.BasePath,".boe_runtime/trusted-local-publication-v1/active.json");
        var targetPath=files.ResolvePath(target);var release=new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        var reached=new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        var cuts=0;byte[]? retainedJournal=null;TrustedLocalPublicationOutcome? decision=null;
        Task? writer=null;Process? child=null;Task<string>? stdout=null,stderr=null;JsonElement ps=default;
        var cleanup=new List<string>();
        try
        {
            Require(owner.Record.Disposition==GmSessionRunDisposition.Running,"QTE fixture has no actual Running main.");
            await owner.RunOperationAsync(async()=>{
                await using var lease=await files.AcquireCanonicalWriteLeaseAsync();
                await files.WriteFileAtomicBytesAsync(lease,"config.json",JsonSerializer.SerializeToUtf8Bytes(new GameSettings {
                    GmBridgeEnabled=true,GmBridgeBackend="OwnedTerminal",GmBridgeAutoStart=false,GmCliLaunchCommand="exit 74"
                }));
                await files.WriteFileAtomicBytesAsync(lease,Request,Packet("A"));
                return 0;
            });
            var start=new ProcessStartInfo("pwsh"){UseShellExecute=false,RedirectStandardInput=true,RedirectStandardOutput=true,RedirectStandardError=true};
            foreach(var arg in new[]{"-NoProfile","-NonInteractive","-File",Path.Combine(TestRepoPaths.RepoRoot,"tests/fixtures/GmDaemonStorage/qte.ps1"),"-RepoRoot",TestRepoPaths.RepoRoot,"-SessionPath",files.GameSessionPath,"-Folder",folder,"-TestSupport",typeof(GmDaemonStorageScenario).Assembly.Location})start.ArgumentList.Add(arg);
            child=Process.Start(start)??throw new InvalidOperationException("Original PowerShell did not start.");
            stdout=child.StandardOutput.ReadToEndAsync();stderr=child.StandardError.ReadToEndAsync();
            await WaitForAsync(()=>File.Exists(Path.Combine(folder,"admitted.json")),child);
            using(var admitted=JsonDocument.Parse(File.ReadAllBytes(Path.Combine(folder,"admitted.json"))))
                Require(admitted.RootElement.GetProperty("identity").GetProperty("runId").GetString()==owner.Identity.RunId,"PowerShell did not retain this original Running owner.");
            evidence["OriginalOperationAdmittedBeforeWriter"]=true;
            var hooked=new FileSystemManager(files.BasePath,NullLogger<FileSystemManager>.Instance,PhysicalLoadTransactionOperations.Instance,new FileSystemManagerHooks {
                LocalPublicationObserver=(phase,index)=>{
                    if(phase!=TrustedLocalPublicationPhase.MemberPublished)return;
                    using var doc=JsonDocument.Parse(File.ReadAllBytes(journal));var members=doc.RootElement.GetProperty("Members");
                    Require(index==0&&members.GetArrayLength()==1&&members[0].GetProperty("Path").GetString()==targetPath,"Wrong actual publication member.");
                    Require(File.ReadAllBytes(targetPath).SequenceEqual(after),"Actual published after-image absent.");
                    Require(Interlocked.Increment(ref cuts)==1,"Unexpected repeated publication cut.");
                    if(mode=="request-unknown")File.WriteAllBytes(targetPath,third);
                    retainedJournal=File.ReadAllBytes(journal);reached.TrySetResult();release.Task.GetAwaiter().GetResult();
                    if(!mode.EndsWith("commit",StringComparison.Ordinal))throw new InvalidOperationException("Actual nontransient daemon publication cut.");
                }
            });
            writer=Task.Run(async()=>await owner.RunOperationAsync(async()=>{
                await using var lease=await hooked.AcquireCanonicalWriteLeaseAsync();
                decision=await hooked.PublishOrdinaryFileWithOutcomeAsync(lease,target,after);return 0;
            }));
            await reached.Task.WaitAsync(TimeSpan.FromSeconds(3));
            await child.StandardInput.WriteLineAsync("go");await child.StandardInput.FlushAsync();
            await WaitForAsync(()=>File.Exists(Path.Combine(folder,"dispatch-entered.json"))||File.Exists(Path.Combine(folder,"canonical-contended.json")),child);
            if(File.Exists(Path.Combine(folder,"dispatch-entered.json")))evidence["SynchronizationGate"]="lower-dispatch";
            else {
                using var contended=JsonDocument.Parse(File.ReadAllBytes(Path.Combine(folder,"canonical-contended.json")));
                evidence["SynchronizationGate"]="canonical-contention:"+contended.RootElement.GetProperty("Action").GetString();
            }
            // A status-write contention is synchronization only, never proof of an admitted read.
            release.TrySetResult();await writer.WaitAsync(TimeSpan.FromSeconds(3));
            Require(cuts==1&&decision!=null,"Actual producer decision missing.");
            evidence["PublicationCuts"]=cuts;evidence["ProducerDisposition"]=decision.Disposition.ToString();
            evidence["ProducerFailure"]=decision.Failure?.ToString();
            var expected=mode.EndsWith("commit",StringComparison.Ordinal)?after:mode=="request-unknown"?third:before;
            Require(expected==null?!File.Exists(targetPath):File.ReadAllBytes(targetPath).SequenceEqual(expected),"Producer decision bytes changed.");
            evidence["ExactProducerBytes"]=true;
            if(mode=="request-unknown") {
                Require(decision.Disposition==TrustedLocalPublicationDisposition.Uncertain&&File.ReadAllBytes(journal).SequenceEqual(retainedJournal!),"Unknown decision/evidence missing.");
                evidence["UnknownJournalBeforeCleanup"]=Convert.ToBase64String(retainedJournal!);
            } else Require(!File.Exists(journal)&&decision.Disposition==(mode.EndsWith("commit",StringComparison.Ordinal)?TrustedLocalPublicationDisposition.Committed:TrustedLocalPublicationDisposition.RolledBack),"Known decision not settled.");
            await child.WaitForExitAsync().WaitAsync(TimeSpan.FromSeconds(5));await Task.WhenAll(stdout,stderr).WaitAsync(TimeSpan.FromSeconds(2));
            evidence["PowerShellExitCode"]=child.ExitCode;evidence["PowerShellStdOut"]=await stdout;evidence["PowerShellStdErr"]=await stderr;
            Require(child.ExitCode==0,"Fixture PowerShell failed before its result projection.");
            using var parsed=JsonDocument.Parse(File.ReadAllBytes(Path.Combine(folder,"powershell.json")));ps=parsed.RootElement.Clone();evidence["PowerShell"]=ps;
            using var helper=JsonDocument.Parse(File.ReadAllBytes(Path.Combine(folder,"daemon-helper.json")));
            Require(ps.GetProperty("disposed").GetBoolean()&&ps.GetProperty("helperExitedBeforeDispose").GetBoolean()&&
                helper.RootElement.GetProperty("ProcessId").GetInt32()==ps.GetProperty("helperPid").GetInt32()&&helper.RootElement.GetProperty("Completed").GetBoolean()&&
                helper.RootElement.GetProperty("ExitCode").GetInt32()==ps.GetProperty("helperExitCode").GetInt32(),"Original helper join/completion missing.");
            Require(ps.GetProperty("closeObserved").GetBoolean()&&!ps.GetProperty("lost").GetBoolean(),"Original operation did not explicitly close.");
        }
        finally
        {
            release.TrySetResult();
            try {if(writer!=null)await writer.WaitAsync(TimeSpan.FromSeconds(3));}catch(Exception failure){cleanup.Add("writer: "+failure);}
            try {
                if(child!=null){try{child.StandardInput.Close();}catch{}if(!child.HasExited){evidence["ForcedPowerShellTermination"]=true;child.Kill();await child.WaitForExitAsync();}
                    if(stdout!=null&&stderr!=null)await Task.WhenAll(stdout,stderr);evidence["PowerShellExited"]=child.HasExited;child.Dispose();}
            }catch(Exception failure){cleanup.Add("original child: "+failure);}
            try {
                if(mode=="request-unknown"&&File.Exists(journal)) {
                    evidence["FixtureExactBeforeRepair"]=true;File.WriteAllBytes(targetPath,before!);
                    await owner.RunOperationAsync(async()=>{await using var lease=await files.AcquireCanonicalWriteLeaseAsync();return 0;});
                }
            }catch(Exception failure){cleanup.Add("owned evidence cleanup: "+failure);}
            try {await stop();evidence["OriginalDaemonOwnerRetired"]=owner.Record.Disposition==GmSessionRunDisposition.Stopped&&!owner.RetainsAuthority;
                Require((bool)evidence["OriginalDaemonOwnerRetired"]!,"Original daemon main did not retire.");}
            catch(Exception failure){cleanup.Add("original main: "+failure);}
            if(cleanup.Count!=0)evidence["DaemonCleanupFailures"]=cleanup;
        }
        Require(cleanup.Count==0,"Daemon fixture cleanup failed.");
        var dispatches=ps.GetProperty("dispatches").EnumerateArray().ToArray();
        var shouldSend=mode is "request-rollback" or "request-commit" or "ready-rollback";
        Require(dispatches.Length==(shouldSend?1:0),"Actual daemon eligibility used a transient request or Ready image.");
        if(shouldSend) {
            var tag=mode=="request-commit"?"B":"A";var item=dispatches[0];
            Require(item.GetProperty("sourceHash").GetString()==Convert.ToHexString(System.Security.Cryptography.SHA256.HashData(Packet(tag)))&&
                item.GetProperty("payload").GetProperty("text").GetString()!.Contains("requestId=request-"+tag,StringComparison.Ordinal),"Frozen daemon dispatch does not match recovered authority.");
        }
        if(mode=="request-unknown")Require(ps.GetProperty("failure").ValueKind==JsonValueKind.String,"Storage read refusal was hidden from original outer consumer.");
    }

    private static void PublishTechnicalMarker(string path,string json)
    {
        var temporary=path+"."+Guid.NewGuid().ToString("N")+".tmp";
        try {File.WriteAllText(temporary,json);File.Move(temporary,path,true);}
        finally {if(File.Exists(temporary))File.Delete(temporary);}
    }

    private static async Task WaitForAsync(Func<bool> predicate,Process child)
    {
        var until=DateTime.UtcNow.AddSeconds(4);
        while(!predicate()){if(child.HasExited||DateTime.UtcNow>=until)throw new InvalidOperationException("Original fixture boundary not reached.");await Task.Delay(10);}
    }

    private sealed class ObservedInput(Stream inner,Action<string> observe) : Stream
    {
        private readonly List<byte> line=[];
        private void Capture(ReadOnlySpan<byte> bytes){foreach(var b in bytes){if(b==10){try{using var j=JsonDocument.Parse(line.ToArray());observe(j.RootElement.GetProperty("action").GetString()??"missing");}catch{observe("unparsed");}line.Clear();}else line.Add(b);}}
        public override async ValueTask<int> ReadAsync(Memory<byte> buffer,CancellationToken token=default){var n=await inner.ReadAsync(buffer,token);Capture(buffer.Span[..n]);return n;}
        public override int Read(byte[] buffer,int offset,int count){var n=inner.Read(buffer,offset,count);Capture(buffer.AsSpan(offset,n));return n;}
        public override bool CanRead=>true;public override bool CanSeek=>false;public override bool CanWrite=>false;public override long Length=>throw new NotSupportedException();
        public override long Position{get=>throw new NotSupportedException();set=>throw new NotSupportedException();}public override void Flush(){}
        public override long Seek(long offset,SeekOrigin origin)=>throw new NotSupportedException();public override void SetLength(long value)=>throw new NotSupportedException();public override void Write(byte[] buffer,int offset,int count)=>throw new NotSupportedException();
    }
}
