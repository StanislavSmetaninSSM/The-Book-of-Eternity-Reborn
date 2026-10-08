using System.Diagnostics;
using System.Runtime.InteropServices;
using Microsoft.Win32.SafeHandles;
using System.Text;
using System.Text.Json;
using BookOfEternityClient.Configuration;
using BookOfEternityClient.Core;
using BookOfEternityClient.Services;
using BookOfEternityClient.Services.GmRuntime;
using Microsoft.Extensions.Logging.Abstractions;

namespace BookOfEternityClient.Tests;

internal static class GmHelperCurrentScenario
{
    internal const string Target="output/helper-current.json";
    internal static readonly byte[] Before=Encoding.UTF8.GetBytes("{\"value\":\"before\"}");
    private static void Require(bool condition,string message){if(!condition)throw new InvalidOperationException(message);}
    internal static string Journal(FileSystemManager files)=>Path.Combine(files.RuntimeRootPath,"trusted-local-publication-v1/active.json");

    internal static async Task<int> RunChildAsync(string root,string folder,string expected,string mode)
    {
        var report=new Dictionary<string,object?>{["ProcessId"]=Environment.ProcessId};
        var cuts=0;var leases=0;var recovery=0;FileSystemManager? files=null;
        files=new FileSystemManager(root,NullLogger<FileSystemManager>.Instance,PhysicalLoadTransactionOperations.Instance,new FileSystemManagerHooks{
            AfterCanonicalWriteLockOpenedAsync=()=>{leases++;return Task.CompletedTask;},
            CanonicalWriteLockContendedAsync=()=>{if(mode=="admission-cancel")File.WriteAllText(Path.Combine(folder,"helper-contended"),"actual canonical lock");return Task.CompletedTask;},
            SessionOperationClosingAsync=()=>{if(mode=="admission-cancel")File.WriteAllText(Path.Combine(folder,"helper-closing"),"actual bound closing");return Task.CompletedTask;},
            LocalPublicationRecoveryObserver=(_,_)=>recovery++,
            LocalPublicationObserver=(phase,index)=>{
                if(mode is not ("known-rollback" or "publication-unknown" or "committed-debt"))return;
                if(phase!=(mode=="committed-debt"?TrustedLocalPublicationPhase.Committed:TrustedLocalPublicationPhase.MemberPublished))return;
                using var journal=JsonDocument.Parse(File.ReadAllBytes(Journal(files!)));
                var members=journal.RootElement.GetProperty("Members");
                if(members.GetArrayLength()!=1||members[0].GetProperty("Path").GetString()!=files!.ResolvePath(Target))return;
                Require(index==0||phase==TrustedLocalPublicationPhase.Committed,"Wrong selected publication index.");
                var after=File.ReadAllBytes(files.ResolvePath(Target));Require(!after.SequenceEqual(Before),"Selected publication has no actual changed after-image.");
                cuts++;report["ActualAfter"]=after;report["ActualJournal"]=File.ReadAllBytes(Journal(files));
                if(mode=="publication-unknown")File.WriteAllBytes(files.ResolvePath(Target),[41,43,47]);
                throw new InvalidOperationException("actual dedicated helper publication cut");
            }
        });
        try {var exit=mode=="admission-cancel"
                ?await GmTurnHelperControl.RunInterruptibleAsync(files,Console.OpenStandardInput(),Console.OpenStandardOutput(),expected)
                :await GmTurnHelperControl.RunAsync(files,Console.OpenStandardInput(),Console.OpenStandardOutput(),expected=="initialize"?null:expected);report["ExitCode"]=exit;return exit;}
        catch(Exception failure){report["Failure"]=failure.ToString();report["Cancelled"]=failure is OperationCanceledException;report["ExitCode"]=2;return 2;}
        finally {report["Completed"]=true;report["PublicationCuts"]=cuts;report["LeaseOpens"]=leases;report["RecoveryPhases"]=recovery;await File.WriteAllTextAsync(Path.Combine(folder,$"helper-child-{Environment.ProcessId}.json"),JsonSerializer.Serialize(report));}
    }

    internal static async Task SeedAsync(FileSystemManager files)
    {
        await using var lease=await files.AcquireCanonicalWriteLeaseAsync();
        if(files.ReadExistingSessionGeneration(lease)==null)files.BootstrapLocalStorage(lease,null,JsonSerializer.SerializeToUtf8Bytes(new GameSettings()));
        if(await files.ReadFileBytesAsync(lease,"config.json")==null)await files.WriteFileAtomicBytesAsync(lease,"config.json",JsonSerializer.SerializeToUtf8Bytes(new GameSettings()));
        await files.WriteFileAtomicBytesAsync(lease,Target,Before);
        await files.WriteFileAtomicAsync(lease,"game_state/meta/soul_state.json","{\"currentRealm\":\"Mortal World\"}");
        await files.WriteFileAtomicAsync(lease,"output/helper-large.json",JsonSerializer.Serialize(new {value=new string('Ж',60000)}));
        await files.WriteFileAtomicAsync(lease,"output/A.json","{\"value\":\"A\"}");
        await files.WriteFileAtomicAsync(lease,"output/a.json","{\"value\":\"a\"}");
    }

    internal static async Task<int> RunAsync(string mode,string folder)
    {
        var evidence=new Dictionary<string,object?>{["Mode"]=mode};
        var root=Path.Combine(folder,"session-owner");Directory.CreateDirectory(root);
        var files=new FileSystemManager(root,NullLogger<FileSystemManager>.Instance);
        try {await SeedAsync(files);await ExerciseAsync(mode,files,folder,evidence);evidence["Success"]=true;return 0;}
        catch(Exception failure){evidence["Failure"]=failure.ToString();return 1;}
        finally
        {
            try {
                if(mode=="publication-unknown"&&File.Exists(Journal(files)))
                {
                    try{await using var refused=await files.AcquireCanonicalWriteLeaseAsync();throw new InvalidOperationException("Unknown journal admitted.");}
                    catch(InvalidDataException){evidence["ActualColdRefusal"]=true;}
                    var cut=Directory.GetFiles(folder,"helper-child-*.json").Select(p=>JsonDocument.Parse(File.ReadAllBytes(p))).Single(d=>d.RootElement.TryGetProperty("ActualAfter",out _));
                    using(cut)File.WriteAllBytes(files.ResolvePath(Target),cut.RootElement.GetProperty("ActualAfter").GetBytesFromBase64());
                }
                var soul=Path.Combine(files.GameSessionPath,"game_state/misc/characteristics.json");
                if(new FileInfo(soul).LinkTarget!=null){File.Delete(soul);File.WriteAllText(soul,"{}");}
                await using var lease=await files.AcquireCanonicalWriteLeaseAsync();Require(files.ReadExistingSessionGeneration(lease)!=null,"Missing original fixture generation.");
                evidence["FixtureCanonicalOwnershipReleased"]=true;
            }catch(Exception cleanup){evidence["CleanupFailure"]=cleanup.ToString();evidence["Success"]=false;}
            await File.WriteAllTextAsync(Path.Combine(folder,"scenario.json"),JsonSerializer.Serialize(evidence));
        }
    }

    internal static async Task CancelAdmissionAsync(FileSystemManager files,GmSessionRunCoordinator owner,string folder,Dictionary<string,object?> evidence)
    {
        var held=new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        var release=new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        var holder=owner.RunOperationAsync(async()=>{await using var lease=await files.AcquireCanonicalWriteLeaseAsync();held.SetResult();await release.Task;return 0;});
        Process? process=null;Task<string>? stdout=null,stderr=null;
        try
        {
            await held.Task.WaitAsync(TimeSpan.FromSeconds(5));
            var before=File.ReadAllBytes(files.ResolvePath(Target));
            var start=new ProcessStartInfo("dotnet"){UseShellExecute=false,RedirectStandardInput=true,RedirectStandardOutput=true,RedirectStandardError=true};
            foreach(var argument in new[]{typeof(GmHelperCurrentScenario).Assembly.Location,"helper-current-bootstrap",files.BasePath,folder,owner.Identity.GenerationId,"admission-cancel"})start.ArgumentList.Add(argument);
            process=Process.Start(start)!;stdout=process.StandardOutput.ReadToEndAsync();stderr=process.StandardError.ReadToEndAsync();
            using var identity=LinuxClipboardReaderIdentity.Capture(process)??throw new InvalidOperationException("Original helper pidfd unavailable.");
            await process.StandardInput.WriteLineAsync("{\"sequence\":0,\"action\":\"open\",\"mode\":\"write\"}");await process.StandardInput.FlushAsync();
            await WaitFile("helper-contended");
            Require(Signal(identity,2,IntPtr.Zero,0)==0,"Original helper SIGINT failed.");
            await WaitFile("helper-closing");
            evidence["ActualCanonicalContention"]=true;evidence["ClosingBeforeHolderRelease"]=true;
            Require(!process.HasExited&&!holder.IsCompleted,"Cancellation discarded the original finalization fence.");
            release.TrySetResult();await holder;
            await process.WaitForExitAsync().WaitAsync(TimeSpan.FromSeconds(5));await Task.WhenAll(stdout,stderr);
            evidence["HelperOutput"]=await stdout;evidence["HelperError"]=await stderr;evidence["HelperExit"]=process.ExitCode;
            var lines=(await stdout).Split('\n',StringSplitOptions.RemoveEmptyEntries);Require(lines.Length==1,"Cancelled admission emitted an active or extra frame.");
            using var reply=JsonDocument.Parse(lines[0]);var value=reply.RootElement;
            Require(value.GetProperty("state").GetString()=="admission-cancelled"&&value.GetProperty("effectiveOutcome").GetInt32()==2&&value.GetProperty("closeObserved").GetBoolean(),"Cancelled original admission lost its actual close/ACK.");
            var close=value.GetProperty("terminalClose");
            Require(close.GetProperty("outcome").GetInt32()==2&&close.GetProperty("identity").GetProperty("runId").GetString()==owner.Identity.RunId,"Cancelled receipt belongs to another original run/outcome.");
            using var report=JsonDocument.Parse(File.ReadAllBytes(Path.Combine(folder,$"helper-child-{process.Id}.json")));var child=report.RootElement;evidence["ActualHelperChild"]=child.Clone();
            Require(process.ExitCode==2&&child.GetProperty("Cancelled").GetBoolean()&&child.GetProperty("Completed").GetBoolean()&&child.GetProperty("PublicationCuts").GetInt32()==0&&child.GetProperty("RecoveryPhases").GetInt32()==0,"Cancelled wait reached publication/recovery or lacked cooperative cancellation.");
            Require(before.SequenceEqual(File.ReadAllBytes(files.ResolvePath(Target)))&&!File.Exists(Journal(files)),"Cancelled admission changed canonical bytes/evidence.");
        }
        finally
        {
            release.TrySetResult();
            var failures=new List<Exception>();
            try{await holder;}catch(Exception failure){failures.Add(failure);}
            try{if(process!=null){if(!process.HasExited){evidence["ForcedHelperTermination"]=true;process.Kill();}await process.WaitForExitAsync();if(stdout!=null&&stderr!=null)await Task.WhenAll(stdout,stderr);evidence["OriginalHelperExited"]=process.HasExited;}}catch(Exception failure){failures.Add(failure);}
            process?.Dispose();if(failures.Count!=0)throw new AggregateException("Original cancellation fixture cleanup failed.",failures);
        }
        async Task WaitFile(string name){var timeout=Stopwatch.StartNew();while(!File.Exists(Path.Combine(folder,name))){if(timeout.Elapsed>TimeSpan.FromSeconds(5))throw new TimeoutException("Actual helper boundary not reached: "+name);await Task.Delay(10);}}
    }
    [DllImport("libc",EntryPoint="pidfd_send_signal",SetLastError=true)]
    private static extern int Signal(SafeFileHandle identity,int signal,IntPtr info,uint flags);

    internal static async Task ExerciseAsync(string mode,FileSystemManager files,string folder,Dictionary<string,object?> evidence)
    {
        var start=new ProcessStartInfo("pwsh"){UseShellExecute=false,RedirectStandardOutput=true,RedirectStandardError=true};
        foreach(var argument in new[]{"-NoProfile","-NonInteractive","-File",Path.Combine(TestRepoPaths.RepoRoot,"tests/fixtures/GmTurnHelper/current.ps1"),
            "-RepoRoot",TestRepoPaths.RepoRoot,"-SessionPath",files.GameSessionPath,"-Scenario",mode,"-Folder",folder,"-TestSupport",typeof(GmHelperCurrentScenario).Assembly.Location})start.ArgumentList.Add(argument);
        using var process=Process.Start(start)!;var stdout=process.StandardOutput.ReadToEndAsync();var stderr=process.StandardError.ReadToEndAsync();
        try {await process.WaitForExitAsync().WaitAsync(TimeSpan.FromSeconds(12));}
        finally
        {
            if(!process.HasExited){evidence["ForcedPowerShellTermination"]=true;process.Kill();await process.WaitForExitAsync();}
            await Task.WhenAll(stdout,stderr);evidence["PowerShellExited"]=process.HasExited;
        }
        evidence["PowerShellExit"]=process.ExitCode;evidence["PowerShellOutput"]=await stdout;evidence["PowerShellError"]=await stderr;
        Require(!evidence.ContainsKey("ForcedPowerShellTermination")&&process.ExitCode==0,"Actual PowerShell control failed: "+await stderr);
        using var result=JsonDocument.Parse(File.ReadAllBytes(Path.Combine(folder,"powershell.json")));evidence["ActualPowerShell"]=result.RootElement.Clone();
        Require(result.RootElement.GetProperty("Success").GetBoolean(),"Actual PowerShell control did not complete its assertions.");
        var joined=result.RootElement.GetProperty("Joined").EnumerateArray().ToArray();
        Require(joined.Select(x=>x.GetProperty("ProcessId").GetInt32()).Distinct().Count()==joined.Length,"Duplicate joined original helper.");
        var reports=new List<JsonElement>();
        foreach(var path in Directory.GetFiles(folder,"helper-child-*.json"))
        {
            using var child=JsonDocument.Parse(File.ReadAllBytes(path));var item=child.RootElement;reports.Add(item.Clone());
            var original=joined.Single(x=>x.GetProperty("ProcessId").GetInt32()==item.GetProperty("ProcessId").GetInt32());
            Require(item.GetProperty("Completed").GetBoolean()&&original.GetProperty("ExitedBeforeDispose").GetBoolean()&&original.GetProperty("ExitCode").GetInt32()==item.GetProperty("ExitCode").GetInt32(),"Original helper was not joined at its real exit.");
        }
        evidence["ActualHelperChildren"]=reports;
        if(mode is "known-rollback" or "publication-unknown" or "committed-debt")Require(reports.Sum(x=>x.GetProperty("PublicationCuts").GetInt32())==1,"Selected actual publication cut did not occur exactly once.");
        if(mode=="known-rollback")Require(File.ReadAllBytes(files.ResolvePath(Target)).SequenceEqual(Before)&&!File.Exists(Journal(files)),"Known rollback did not preserve exact before bytes.");
        if(mode=="publication-unknown")Require(File.ReadAllBytes(files.ResolvePath(Target)).SequenceEqual(new byte[]{41,43,47})&&File.Exists(Journal(files)),"Actual unknown evidence was lost.");
        if(mode=="committed-debt")Require(!File.ReadAllBytes(files.ResolvePath(Target)).SequenceEqual(Before)&&File.Exists(Journal(files)),"Committed decision debt was lost before cold cleanup.");
    }
}
