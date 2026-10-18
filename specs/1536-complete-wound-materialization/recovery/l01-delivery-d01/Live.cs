#nullable enable
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;
using D = BookOfEternityClient.Tests.JointInterfaceDiagnosticDelivery;

// Genuine synthetic children only. No game reference, diagnostics API or arbitrary PID mode.
internal static class D01Live
{
    private static ulong Tick(int pid)
    {
        var stat=File.ReadAllText($"/proc/{pid}/stat");
        var fields=stat[(stat.LastIndexOf(')')+2)..].Split(' ',StringSplitOptions.RemoveEmptyEntries);
        return ulong.Parse(fields[19],System.Globalization.CultureInfo.InvariantCulture);
    }
    private static string Hash(string text)=>Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(text))).ToLowerInvariant();
    private static string Nonce(string runNonce,int exit)=>Hash(runNonce+":"+exit)[..32];
    private static void Require(bool condition,string message){if(!condition)throw new InvalidOperationException(message);}
    private static int Child(string root,string nonce,int exit)
    {
        using var watchdog=new Timer(_=>Environment.Exit(124),null,15000,Timeout.Infinite);
        var marker=JsonSerializer.Serialize(new {Boundary=D.Boundary,Stage="finalization-return",Detail=D.Custody,
            Pid=Environment.ProcessId,BirthTick=Tick(Environment.ProcessId),Root=root,Nonce=nonce,Cut=1,
            Utc=DateTimeOffset.UtcNow,ElapsedMilliseconds=1});
        var text="ordinary 🦉\r\n"+(exit==7?"{"+D.Boundary+"\n":"")+marker+"\n";
        var finalText=text+"done\n";
        Console.Out.Write(text);Console.Out.Flush();
        Console.Error.WriteLine(JsonSerializer.Serialize(new {Kind="D01_OWN_CHILD",Pid=Environment.ProcessId,
            BirthTick=Tick(Environment.ProcessId),Root=root,Nonce=nonce,ExpectedExit=exit,RawStdoutSha256=Hash(finalText)}));
        Console.Error.Flush();
        if(Console.ReadLine()!="STOP:"+nonce)return 65;
        Console.Out.Write("done\n");Console.Out.Flush();
        return exit;
    }
    private static async Task<Dictionary<string,object?>> One(string root,string runNonce,int expectedExit)
    {
        var nonce=Nonce(runNonce,expectedExit);var own=Path.Combine(root,"exit"+expectedExit);Directory.CreateDirectory(own);
        var assembly=typeof(D01Live).Assembly.Location;var executable=Path.Combine(Environment.GetEnvironmentVariable("DOTNET_ROOT")!,"dotnet");
        var arguments=new[]{"exec","--runtimeconfig",Path.ChangeExtension(assembly,".runtimeconfig.json"),assembly,"--child",own,nonce,expectedExit.ToString()};
        var record=new Dictionary<string,object?>{{"ExpectedNativeExit",expectedExit},{"Root",own},{"Nonce",nonce},{"Command",new[]{executable}.Concat(arguments).ToArray()},
            {"NativeExit",null},{"ObserverErrors",new List<string>()},{"Passed",false}};
        var observerErrors=(List<string>)record["ObserverErrors"]!;
        Process? child=null;ulong tick=0;Task<string>? raw=null,err=null;D? delivery=null;string? primary=null;
        try
        {
            var start=new ProcessStartInfo(executable){UseShellExecute=false,RedirectStandardInput=true,RedirectStandardOutput=true,RedirectStandardError=true,WorkingDirectory=own};
            foreach(var argument in arguments)start.ArgumentList.Add(argument);
            child=Process.Start(start)??throw new InvalidOperationException("Own synthetic child missing.");
            tick=Tick(child.Id);record["Pid"]=child.Id;record["BirthTick"]=tick;
            raw=D.Start(child.StandardOutput,new(child.Id,tick,own,nonce,1),out delivery,out var constructionError);
            err=child.StandardError.ReadToEndAsync();Require(constructionError is null && delivery is not null,"live delivery unavailable");
            var item=await delivery!.Reader.ReadAsync().AsTask().WaitAsync(TimeSpan.FromSeconds(3));
            record["EventBeforeStop"]=item;record["HealthBeforeStop"]=delivery.ReadHealth();
            Require(item.Control=="CUSTODY_RETURN" && item.Pid==child.Id && item.BirthTick==tick && item.Root==own && item.Nonce==nonce,"wrong live event identity/control");
            Require(!child.HasExited && !raw.IsCompleted && !delivery.ReadHealth().Eof,"event was available only after exit/EOF");
            record["ChildAliveBeforeStop"]=true;
            if(expectedExit==0)Require(!delivery.ReadHealth().Uncertain,"positive delivery has health gap");
            else Require(delivery.ReadHealth().Malformed==1 && delivery.ReadHealth().Uncertain,"observer parse fault not retained");
        }
        catch(Exception error){primary=error.ToString();}
        finally
        {
            if(child is not null)
            {
                try
                {
                    if(!child.HasExited)
                    {
                        Require(tick!=0 && Tick(child.Id)==tick,"owned child identity uncertain during cleanup");
                        await child.StandardInput.WriteLineAsync("STOP:"+nonce);await child.StandardInput.FlushAsync();child.StandardInput.Close();
                    }
                    await child.WaitForExitAsync().WaitAsync(TimeSpan.FromSeconds(5));record["NativeExit"]=child.ExitCode;
                    raw??=child.StandardOutput.ReadToEndAsync();err??=child.StandardError.ReadToEndAsync();
                    await Task.WhenAll(raw,err).WaitAsync(TimeSpan.FromSeconds(5));
                    record["JoinedEof"]=true;record["RawStdout"]=raw.Result;record["RawStderr"]=err.Result;
                    record["HealthFinal"]=delivery?.ReadHealth();
                    using var report=JsonDocument.Parse(err.Result);var e=report.RootElement;
                    Require(e.GetProperty("Pid").GetInt32()==child.Id && e.GetProperty("BirthTick").GetUInt64()==tick &&
                        e.GetProperty("Root").GetString()==own && e.GetProperty("Nonce").GetString()==nonce,"child self-report mismatch");
                    Require(e.GetProperty("RawStdoutSha256").GetString()==Hash(raw.Result),"raw stdout differs from child's own text hash");
                    record["RawStdoutChildHashMatches"]=true;
                    Require(child.ExitCode==expectedExit,"known native outcome changed");
                }
                catch(Exception error){observerErrors.Add(error.ToString());if(child.HasExited)record["NativeExit"]=child.ExitCode;}
                finally{child.Dispose();}
            }
        }
        record["PrimaryFailure"]=primary;
        record["Passed"]=primary is null && observerErrors.Count==0 && record.ContainsKey("JoinedEof") && record.ContainsKey("ChildAliveBeforeStop");
        try { File.WriteAllText(Path.Combine(own,"record.json"),JsonSerializer.Serialize(record,new JsonSerializerOptions{WriteIndented=true})); }
        catch(Exception error) { observerErrors.Add("Original record publication: "+error.GetType().Name); record["Passed"]=false; }
        return record;
    }
    private static async Task<int> Main(string[] args)
    {
        if(args.Length==4 && args[0]=="--child")return Child(args[1],args[2],int.Parse(args[3]));
        if(args.Length!=3 || args[0]!="live" || !Path.IsPathFullyQualified(args[1]) || !Guid.TryParseExact(args[2],"N",out _))return 64;
        var cases=new List<Dictionary<string,object?>>();
        foreach(var exit in new[]{0,7})
        {
            var result=await One(args[1],args[2],exit);cases.Add(result);
            if(!(bool)result["Passed"]!)break; // Freeze first unexpected result; no automatic retry/next child.
        }
        Console.WriteLine(JsonSerializer.Serialize(new {Kind="D01_FIRST_GENUINE_SYNTHETIC_SELECTION",Cases=cases,
            PlannedFixtureChildren=2,CompletedFixtureChildren=cases.Count,GameProcesses=0,DiagnosticRequests=0}));
        return cases.Count==2 && cases.All(x=>(bool)x["Passed"]!)?0:2;
    }
}
