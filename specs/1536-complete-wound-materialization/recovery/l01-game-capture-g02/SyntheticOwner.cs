using System;
using System.IO;
using System.Linq;
using System.Diagnostics;
using System.Collections.Generic;
using System.Security.Cryptography;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;
using System.Threading.Channels;

// Adapter only: the genuine, unchanged S01 target is the oracle. Only the common G01 collector invokes the diagnostic API.
internal static class SyntheticOwner
{
    static JsonElement Read(string path) => JsonDocument.Parse(File.ReadAllText(path)).RootElement.Clone();
    static string S(JsonElement e, string n) => e.GetProperty(n).GetString()!;
    static long N(JsonElement e, string n) => e.GetProperty(n).GetInt64();
    static string Hash(byte[] b) => Convert.ToHexString(SHA256.HashData(b)).ToLowerInvariant();
    static void Require(bool value, string message) { if (!value) throw new InvalidOperationException(message); }
    static void Write(string path, object value)
    {
        string temp = path + ".new";
        File.WriteAllText(temp, JsonSerializer.Serialize(value)); File.Move(temp, path, true);
    }
    static (long Tick, int Parent, int Uid) Identity(int pid)
    {
        string text = File.ReadAllText($"/proc/{pid}/stat");
        string[] f = text[(text.LastIndexOf(')') + 2)..].Split(' ', StringSplitOptions.RemoveEmptyEntries);
        string uid = File.ReadAllLines($"/proc/{pid}/status").Single(x => x.StartsWith("Uid:"));
        return (long.Parse(f[19]), int.Parse(f[1]), int.Parse(uid.Split((char[]?)null, StringSplitOptions.RemoveEmptyEntries)[1]));
    }
    static Process Start(string executable, IEnumerable<string> args, string cwd, string tmp)
    {
        var info = new ProcessStartInfo(executable) { UseShellExecute=false, WorkingDirectory=cwd,
            RedirectStandardInput=true, RedirectStandardOutput=true, RedirectStandardError=true };
        foreach (string arg in args) info.ArgumentList.Add(arg);
        info.Environment["TMPDIR"] = tmp;
        return Process.Start(info) ?? throw new InvalidOperationException("Process.Start returned null");
    }
    static JsonElement Oracle(string line)
    {
        Require(line.StartsWith("L01_SYNTHETIC ", StringComparison.Ordinal), "Oracle prefix");
        return JsonDocument.Parse(line[14..]).RootElement.Clone();
    }
    static async Task MainCore(string[] args)
    {
        Require(args.Length==2, "Settings path and SHA required");
        byte[] raw=File.ReadAllBytes(args[0]); Require(Hash(raw)==args[1], "Settings hash");
        JsonElement cfg=JsonDocument.Parse(raw).RootElement;
        Require(cfg.GetProperty("SyntheticOracle").ValueKind==JsonValueKind.True &&
            S(cfg,"OracleContract")=="S01_G01_ONE_CAPTURE", "Explicit synthetic-only role");
        string own=S(cfg,"Own"), capture=Path.Combine(own,"capture"), tmp=Path.Combine(own,"tmp");
        Require(!Directory.Exists(own) && !File.Exists(own), "Fresh own required");
        Directory.CreateDirectory(own,UnixFileMode.UserRead|UnixFileMode.UserWrite|UnixFileMode.UserExecute);
        Directory.CreateDirectory(capture,UnixFileMode.UserRead|UnixFileMode.UserWrite|UnixFileMode.UserExecute);
        Directory.CreateDirectory(tmp,UnixFileMode.UserRead|UnixFileMode.UserWrite|UnixFileMode.UserExecute);
        string root=Path.Combine(tmp,"oracle-root");Directory.CreateDirectory(root);
        int owner=Environment.ProcessId; var ownerId=Identity(owner);
        string nonce=S(cfg,"Nonce");Require(Guid.TryParseExact(nonce,"N",out _),"Nonce");
        Write(Path.Combine(own,"parent.json"),new {Nonce=nonce,ParentPid=owner,Cut=1,Fault=(string?)null,Diagnostic=true});
        Write(Path.Combine(own,"cut.json"),new {Root=root,Nonce=nonce,Pid=owner,Cut=1,SyntheticOracle=true});
        string parentHash=Hash(File.ReadAllBytes(Path.Combine(own,"parent.json")));
        string[] command={S(cfg,"Dotnet"),"exec","--runtimeconfig",S(cfg,"OracleRuntimeconfig"),S(cfg,"OracleAssembly"),nonce};
        Process? target=null, watchdog=null; long targetTick=0,watchdogTick=0;
        Task? stdoutDrain=null,stderrDrain=null;
        var errors=new List<string>(); var record=new Dictionary<string,object?> {
            ["kind"]="G02_S01_THROUGH_G01_CAPTURE",["syntheticOnly"]=true,["gameProcesses"]=0,
            ["runId"]=S(cfg,"RunId"),["apiCalls"]=null,["admissionIsSimulated"]=true,["ownerPid"]=owner,["ownerBirthTick"]=ownerId.Tick };
        using var healthStop=new CancellationTokenSource(); Task? health=null;
        try
        {
            long launched=Stopwatch.GetTimestamp(), deadline=launched+60*Stopwatch.Frequency;
            target=Start(command[0],command.Skip(1),own,tmp);
            record["targetLaunches"]=1;record["targetPid"]=target.Id;record["targetNativeExit"]=null;record["targetJoined"]=false;
            var targetId=Identity(target.Id); targetTick=targetId.Tick;
            var eventsChannel=Channel.CreateBounded<JsonElement>(4);
            stdoutDrain=Task.Run(async () => {
                using var output=new StreamWriter(Path.Combine(own,"target.stdout"));
                try {
                    while(await target.StandardOutput.ReadLineAsync() is string line) {
                        await output.WriteLineAsync(line);
                        if(line.StartsWith("L01_SYNTHETIC ",StringComparison.Ordinal))
                            Require(eventsChannel.Writer.TryWrite(Oracle(line)),"Oracle event overflow");
                    }
                    eventsChannel.Writer.Complete();
                }catch(Exception e){eventsChannel.Writer.TryComplete(e);throw;}
            });
            stderrDrain=Task.Run(async () => File.WriteAllText(Path.Combine(own,"target.stderr"),await target.StandardError.ReadToEndAsync()));
            JsonElement ready=await eventsChannel.Reader.ReadAsync().AsTask().WaitAsync(TimeSpan.FromSeconds(5));
            Require(S(ready,"Stage")=="ready" && S(ready,"Nonce")==nonce && N(ready,"Pid")==target.Id &&
                N(ready,"BirthTick")==targetTick && N(ready,"Uid")==ownerId.Uid && targetId.Parent==owner &&
                S(ready,"Mvid")==S(cfg,"OracleMvid") && S(ready,"Runtime")==".NET 8.0.32" &&
                N(ready,"WaitTid")!=N(ready,"CpuTid") && N(ready,"Iterations")>=1000,"Genuine S01 ready identity/ABI/TIDs");
            Require((deadline-Stopwatch.GetTimestamp())/(double)Stopwatch.Frequency>=45,"Insufficient synthetic60 window");
            record["ready"]=ready; record["deadlineFromPreLaunchStamp"]=deadline;record["launchStamp"]=launched;
            long sequence=0;
            void PublishHealth()
            {
                Write(Path.Combine(capture,"health.json"),new { Pid=target.Id,BirthTick=targetTick,Root=root,Nonce=nonce,Cut=1,
                    Sequence=Interlocked.Increment(ref sequence),Stamp=Stopwatch.GetTimestamp(),Frequency=Stopwatch.Frequency,
                    DeadlineStamp=deadline,Cancelled=false,ReceiptAdmitted=true,PolicyState="ADMITTED",
                    Health=new {Uncertain=false,Eof=false,RawReadFault=false,AcceptedSeen=false,LateReturnSeen=false,
                        ObserverFailure=(string?)null,CustodySeen=true,Dropped=0,Malformed=0,Oversize=0,IncompleteTail=0,
                        IdentityRejected=0,Duplicates=0,ElapsedRegressions=0,TrackingExhausted=0} });
            }
            PublishHealth();
            health=Task.Run(async () => {while(!healthStop.IsCancellationRequested) {PublishHealth();await Task.Delay(20);}});
            Write(Path.Combine(capture,"admission.json"),new {RunId=S(cfg,"RunId"),OwnerPid=owner,OwnerBirthTick=ownerId.Tick,
                TargetPid=target.Id,TargetBirthTick=targetTick,Root=root,Nonce=nonce,Cut=1,Own=own,
                TargetExecutable=S(cfg,"Dotnet"),TargetCommand=command,SettingsPath=Path.GetFullPath(args[0]),SettingsSha256=args[1],
                HealthPath=Path.Combine(capture,"health.json"),DumpPath=Path.Combine(capture,"game-normal.core"),
                ParentFileSha256=parentHash,DeadlineStamp=deadline,HealthFloor=1 });
            watchdog=Start(S(cfg,"Python"),new[]{S(cfg,"Watchdog"),Path.Combine(capture,"admission.json")},capture,tmp);
            record["watchdogLaunches"]=1;record["watchdogPid"]=watchdog.Id;record["watchdogNativeExit"]=null;record["watchdogJoined"]=false;
            watchdogTick=Identity(watchdog.Id).Tick;
            Task<string> wo=watchdog.StandardOutput.ReadToEndAsync(),we=watchdog.StandardError.ReadToEndAsync();
            await watchdog.WaitForExitAsync().WaitAsync(TimeSpan.FromSeconds(27));
            record["watchdogNativeExit"]=watchdog.ExitCode; record["watchdogJoined"]=true;
            string watchOut=await wo.WaitAsync(TimeSpan.FromSeconds(2)),watchErr=await we.WaitAsync(TimeSpan.FromSeconds(2));
            File.WriteAllText(Path.Combine(capture,"watchdog.stdout"),watchOut);File.WriteAllText(Path.Combine(capture,"watchdog.stderr"),watchErr);
            JsonElement wr=Read(Path.Combine(capture,"request-record.json"));record["watchdog"]=wr;record["apiCalls"]=wr.GetProperty("diagnosticRequests").Clone();record["apiOutcome"]=S(wr,"apiOutcome");
            Require(watchdog.ExitCode==0 && N(wr,"nativeExit")==0 && N(wr,"collectorLaunches")==1 &&
                N(wr,"diagnosticRequests")==1 && S(wr,"apiOutcome")=="RETURNED" && wr.GetProperty("observerErrors").GetArrayLength()==0,
                "Watchdog/collector native or API/observer failure");
            var events=File.ReadAllLines(Path.Combine(capture,"collector.stdout")).Where(x=>x.StartsWith("L01_COLLECTOR "))
                .Select(x=>JsonDocument.Parse(x[14..]).RootElement.Clone()).ToArray();
            Require(events.Count(x=>S(x,"Stage")=="pre_api_ready")==1 && events.Count(x=>S(x,"Stage")=="request_started")==1 &&
                events.Length==4 && S(events[3],"Stage")=="collector_exit" && N(events[3],"PreflightStatus")==0 &&
                events[3].GetProperty("ObserverErrors").GetArrayLength()==0,"Exactly one pre-API marker, actual request, return and clean exit required");
            string dump=Path.Combine(capture,"game-normal.core"),report= dump+".crashreport.json";
            Require(new FileInfo(dump).Length>0 && new FileInfo(report).Length>0,"Capture artifacts absent/empty");
            _=Read(report).GetProperty("payload").GetProperty("threads");
            record["collectorEvents"]=events;record["preApiReady"]=1;record["apiCalls"]=1;
            record["privateDumpPath"]=dump;record["privateReportPath"]=report;record["reportReadable"]=true;
            await target.StandardInput.WriteLineAsync("STOP:"+nonce);await target.StandardInput.FlushAsync();target.StandardInput.Close();
            JsonElement done=await eventsChannel.Reader.ReadAsync().AsTask().WaitAsync(TimeSpan.FromSeconds(5));record["done"]=done;
            Require(S(done,"Stage")=="done" && S(done,"Nonce")==nonce && done.GetProperty("ValidStop").GetBoolean() &&
                N(done,"Iterations")>=N(ready,"Iterations"),"STOP oracle");
            await target.WaitForExitAsync().WaitAsync(TimeSpan.FromSeconds(5));
            record["targetNativeExit"]=target.ExitCode;record["targetJoined"]=true;
            await stdoutDrain.WaitAsync(TimeSpan.FromSeconds(2));await stderrDrain.WaitAsync(TimeSpan.FromSeconds(2));
            Require(target.ExitCode==0 && !eventsChannel.Reader.TryRead(out _),"Target native/oracle event tail");
            record["stdoutEof"]=true;record["stderrEof"]=true;
        }
        catch(Exception error) {errors.Add(error.ToString());}
        finally
        {
            healthStop.Cancel();if(health!=null)try {await health.WaitAsync(TimeSpan.FromSeconds(1));}catch(Exception e){errors.Add("Health join: "+e);}
            // Successful Start is accounted before Identity. Missing identity stays an observer failure.
            // Fallback uses only the retained direct child Process, never an arbitrary PID/tree/global kill.
            long cleanup=Stopwatch.GetTimestamp()+5*Stopwatch.Frequency;
            foreach(var item in new[]{(Name:"watchdog",P:watchdog,Tick:watchdogTick),(Name:"target",P:target,Tick:targetTick)})
                if(item.P!=null)try {
                    bool forced=false;
                    if(item.P.HasExited) {
                        if(record[item.Name+"NativeExit"]==null)record[item.Name+"NativeExit"]=item.P.ExitCode;
                    } else {
                        if(item.Tick!=0) {
                            var id=Identity(item.P.Id);Require(id.Tick==item.Tick && id.Parent==owner && id.Uid==ownerId.Uid,"Cleanup ownership unknown");
                        } else record[item.Name+"Identity"]= "UNKNOWN_DIRECT_CHILD_FALLBACK_ONLY";
                        Require(Stopwatch.GetTimestamp()<cleanup,"Cleanup budget exhausted");item.P.Kill();forced=true;
                        errors.Add(item.Name+": owned direct-child fallback kill required");
                    }
                    double remaining=(cleanup-Stopwatch.GetTimestamp())/(double)Stopwatch.Frequency;
                    Require(remaining>0,"Cleanup join budget exhausted");await item.P.WaitForExitAsync().WaitAsync(TimeSpan.FromSeconds(remaining));
                    record[item.Name+"Joined"]=true;
                    if(forced)record[item.Name+"CleanupExit"]=item.P.ExitCode;
                    else if(record[item.Name+"NativeExit"]==null)record[item.Name+"NativeExit"]=item.P.ExitCode;
                }catch(Exception e){errors.Add("Owned cleanup: "+e);}
            foreach(var drain in new[]{stdoutDrain,stderrDrain})
                if(drain!=null)try {await drain.WaitAsync(TimeSpan.FromSeconds(1));}catch(Exception e){errors.Add("Target stream EOF/join: "+e);}
            record["observerErrors"]=errors;record["status"]=errors.Count==0?"SYNTHETIC_CAPTURE_PASSED_API1":"FROZEN_FIRST_SYNTHETIC_CAPTURE_FAILURE";
            Write(Path.Combine(own,"record.json"),record);
        }
        Require(errors.Count==0,"Synthetic capture failed; retain root and HOLD game");
    }
    public static async Task<int> Main(string[] args)
    {
        try {await MainCore(args);return 0;}catch(Exception e){Console.Error.WriteLine(e);return 1;}
    }
}
