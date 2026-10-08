using System.Text.Json;
using BookOfEternityClient.Core;
using BookOfEternityClient.Services;
using BookOfEternityClient.Services.GmRuntime;
using Microsoft.Extensions.Logging.Abstractions;

namespace BookOfEternityClient.Tests;

internal static partial class MainRunCrashScenarioDriver
{
    private static async Task<int> WitnessStopAsync(string cut,string package,string folder,Dictionary<string,object?> result)
    {
        await using var owner=Child(Prefix+"owner-stop-"+cut,package,folder);
        await WaitFileAsync(Path.Combine(folder,"cut.json"),owner.Process);
        var info=ReadInfo(folder);var before=Snapshot(info.Root,cut=="staged");
        if(cut=="readback") {
            await using var warm=Child(Prefix+"cold-warm-blocked",package,folder);await warm.SuccessAsync();
            result["WarmPendingAckGuardRefused"]=true;
        }
        await owner.KillAsync();result["OriginalApplicationKilled"]=true;
        var afterDeath=Snapshot(info.Root);
        Require(Equal(before,afterDeath.Where(p=>cut!="staged" || !IsMainStage(p.Key)).ToDictionary(p=>p.Key,p=>p.Value)),"Stop cut death changed non-stage canonical/main evidence.");
        var terminal=cut is "readback" or "ack";
        await using var cold=Child(Prefix+"cold-"+(terminal?"fresh":"refuse"),package,folder);await cold.SuccessAsync();
        if(!terminal)Require(Equal(afterDeath,Snapshot(info.Root)),"Incomplete stop cold actor changed retained evidence including actual crashed stage.");
        result["Cut"]=JsonSerializer.Deserialize<JsonElement>(File.ReadAllBytes(Path.Combine(folder,"cut.json")));
        result["ColdActor"]=JsonSerializer.Deserialize<JsonElement>(File.ReadAllBytes(Path.Combine(folder,"cold.json")));
        result["SealedStopped"]=terminal;result["BeforeDeath"]=before;result["AfterDeathIncludingStage"]=afterDeath;
        result["OpenStageSnapshotOnlyAfterDeath"]=cut=="staged";result["AfterCold"]=Snapshot(info.Root);result["Success"]=true;return 0;
    }

    private static async Task<int> OwnerStopAsync(string cut,string package,string folder)
    {
        try {
            var launch=NeutralTerminalLaunch.Create(package,folder);var root=Directory.GetParent(launch.Scratch)!.FullName;
            var files=Files(root);var state=PortableSaveFixture.Seed(files);
            Require(await new SaveLoadService(files,state,NullLogger<SaveLoadService>.Instance).SaveGameAsync("stop-source","isolated synthetic stop crash"),"Preparation: archive failed.");
            var archive=Directory.GetFiles(files.ResolvePath("saves/manual_saves"),"*.zip").Single();
            using(var generation=JsonDocument.Parse(File.ReadAllBytes(files.SessionGenerationPath)))WriteJson(Path.Combine(folder,"root.json"),new FixtureInfo(root,archive,generation.RootElement.GetProperty("generationId").GetString()!));
            using var output=new StopOutput(Path.Combine(folder,"owner-output.bin"),cut is "io" or "fault",cut=="fault");
            var host=new FixtureHost(launch,output);var stopping=false;
            host.Set("ObserveMainMetadata",(Action<MainRunIoStage>)(stage=>{
                if(!stopping)return;var record=ReadRecord(root);
                if(cut=="stopping" && stage==MainRunIoStage.Readback && record.Disposition==GmSessionRunDisposition.Stopping)Cut(folder,host,"durable-Stopping-before-scoped-stop");
                if((cut=="staged" && stage==MainRunIoStage.Staged && record.Disposition==GmSessionRunDisposition.Stopping) ||
                    (cut=="readback" && stage==MainRunIoStage.Readback && record.Disposition==GmSessionRunDisposition.Stopped)) {
                    var disposedTerminal=host.Terminal;
                    Require((bool)disposedTerminal.GetType().GetField("_disposed",System.Reflection.BindingFlags.Instance|System.Reflection.BindingFlags.NonPublic)!.GetValue(disposedTerminal)! &&
                        ((Task)host.Field("_terminalDisposeTask")!).IsCompletedSuccessfully && ((Task)host.Field("_outputPumpTask")!).IsCompletedSuccessfully,
                        "Stopped publication preceded actual original I/O/disposal.");
                    WriteJson(Path.Combine(folder,"actual-disposal.json"),new{TerminalDisposed=true,OutputPumpJoined=true,RootExited=disposedTerminal.RootExited.IsCompletedSuccessfully});
                    Cut(folder,host,cut=="staged"?"actual-disposal-Stopped-stage-before-publication":"sealed-Stopped-readback-before-warm-ACK");
                }
            }));
            await host.CallAsync("StartShellAsync");var original=host.Owner;var terminal=host.Terminal;
            if(cut is "io" or "fault")await output.Entered.Task.WaitAsync(TimeSpan.FromSeconds(2));
            if(cut=="fault") {
                output.Release.TrySetResult();await WaitAsync(()=>original.IsUncertain);
                Exception? failure=null;try{await host.CallAsync("StopShellAsync");}catch(Exception error){failure=error;}
                Require(failure!=null && original.IsUncertain && original.RetainsAuthority && ReadRecord(root).Disposition==GmSessionRunDisposition.Uncertain,
                    "Actual pre-seal output failure became a confirmed Stopped owner.");
                var proof=await terminal.StopAndObserveAsync(CancellationToken.None);
                Require(proof.CleanupComplete && terminal.RootExited.IsCompletedSuccessfully,"Independent scoped cleanup did not complete after actual output failure.");
                WriteJson(Path.Combine(folder,"pre-seal-fault.json"),new{Failure=failure!.ToString(),OriginalRetained=true,LogicalUncertain=true,
                    PhysicalScopedEmpty=proof.CleanupComplete,ActualOutputPumpFaulted=((Task)host.Field("_outputPumpTask")!).IsFaulted});
                Cut(folder,host,"real-output-fault-Uncertain-after-physical-cleanup",original);return 65;
            }
            stopping=true;var stop=host.CallAsync("StopShellAsync");
            if(cut=="io") {
                await terminal.RootExited.WaitAsync(TimeSpan.FromSeconds(2));var proof=await terminal.StopAndObserveAsync(CancellationToken.None);
                Require(proof.CleanupComplete && !((Task)host.Field("_outputPumpTask")!).IsCompleted && !stop.IsCompleted &&
                    ReadRecord(root).Disposition==GmSessionRunDisposition.Stopping,"Pending actual output pump was treated as Stopped.");
                WriteJson(Path.Combine(folder,"actual-pending-io.json"),new{PhysicalScopedEmpty=true,OutputPumpPending=true,ActualStopAckPending=true});
                Cut(folder,host,"scoped-empty-with-pending-actual-output",original);return 65;
            }
            await stop;
            Require(cut=="ack" && !original.RetainsAuthority && ReadRecord(root).Disposition==GmSessionRunDisposition.Stopped,"Selected stop cut was missed or ACK retired early.");
            WriteJson(Path.Combine(folder,"actual-stop-ack.json"),new{ActualStopCallCompleted=true,OriginalRetired=true});
            Cut(folder,host,"original-stop-returned-after-durable-ACK",original);return 65;
        } catch(Exception failure){WriteJson(Path.Combine(folder,"owner-failure.json"),new{Failure=failure.ToString()});return 1;}
    }

    private static async Task<int> WarmBlockedAsync(string folder)
    {
        var result=new Dictionary<string,object?>{["Mode"]="warm-blocked",["Pid"]=Environment.ProcessId};
        try {
            var info=ReadInfo(folder);var before=Snapshot(info.Root);Require(ReadRecord(info.Root).Disposition==GmSessionRunDisposition.Stopped,"Warm ACK probe lacks visible Stopped.");
            using var deadline=new CancellationTokenSource(TimeSpan.FromMilliseconds(250));Exception? failure=null;
            try{await using var lease=await Files(info.Root).AcquireCanonicalWriteLeaseAsync(cancellationToken:deadline.Token);}catch(Exception error){failure=error;}
            Require(failure!=null && Equal(before,Snapshot(info.Root)),"Visible Stopped bypassed original pending-ACK guard.");
            result["OriginalGuardRefused"]=true;result["FailureType"]=failure!.GetType().Name;result["Success"]=true;return 0;
        } catch(Exception failure){result["Failure"]=failure.ToString();return 1;}
        finally{WriteJson(Path.Combine(folder,"warm.json"),result);}
    }

    private sealed class StopOutput : Stream
    {
        private readonly FileStream _output;private readonly bool _gated,_fault;
        internal readonly TaskCompletionSource Entered=new(TaskCreationOptions.RunContinuationsAsynchronously),Release=new(TaskCreationOptions.RunContinuationsAsynchronously);
        internal StopOutput(string path,bool gated,bool fault){_gated=gated;_fault=fault;_output=new(path,FileMode.Create,FileAccess.Write,FileShare.ReadWrite,4096,FileOptions.Asynchronous);}
        public override async ValueTask WriteAsync(ReadOnlyMemory<byte> bytes,CancellationToken token=default) {
            await _output.WriteAsync(bytes,token);await _output.FlushAsync(token);Entered.TrySetResult();
            if(_gated)await Release.Task;
            if(_fault)throw new IOException("Controlled real original output destination failure before stop seal.");
        }
        protected override void Dispose(bool disposing){Release.TrySetResult();_output.Dispose();base.Dispose(disposing);}
        public override bool CanRead=>false;public override bool CanSeek=>false;public override bool CanWrite=>true;
        public override long Length=>throw new NotSupportedException();public override long Position{get=>throw new NotSupportedException();set=>throw new NotSupportedException();}
        public override void Flush(){}public override Task FlushAsync(CancellationToken token)=>Task.CompletedTask;
        public override int Read(byte[] bytes,int offset,int count)=>throw new NotSupportedException();public override void Write(byte[] bytes,int offset,int count)=>throw new NotSupportedException();
        public override long Seek(long offset,SeekOrigin origin)=>throw new NotSupportedException();public override void SetLength(long length)=>throw new NotSupportedException();
    }
}
