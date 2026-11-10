using System.Diagnostics;
using System.Text;
using System.Text.Json;
using System.Text.RegularExpressions;
using BookOfEternityClient.Configuration;
using BookOfEternityClient.Core;
using Microsoft.Extensions.Logging.Abstractions;

namespace BookOfEternityClient.Tests;

// Original business assertions stay in the integration tests. This fixture owns
// only their neutral PowerShell/helper processes and genuine canonical bootstrap.
internal static class GmHelperContractScenario
{
    internal static async Task<(int ExitCode,string StdOut,string StdErr)> RunOwnedAsync(string command)
    {
        if(!OperatingSystem.IsLinux()&&!OperatingSystem.IsWindows())throw new PlatformNotSupportedException("The owned contract fixture supports Linux and Windows only.");
        var root=TestRepoPaths.RepoRoot;var folder=Path.Combine(root,"TestResults/native-terminal",Guid.NewGuid().ToString("N"));Directory.CreateDirectory(folder);
        await File.WriteAllTextAsync(Path.Combine(folder,"original.ps1"),command);
        if(OperatingSystem.IsWindows())
        {
            // Same setup and assertions, but no Linux guardian is claimed here.
            // Actual PS5.1 and abnormal native cleanup remain unexecuted recipes.
            if(await RunDriverAsync(folder)!=0)throw new InvalidOperationException("Native original helper fixture failed: "+await File.ReadAllTextAsync(Path.Combine(folder,"scenario.json")));
        }
        else
        {
        var build=new ProcessStartInfo("pwsh"){UseShellExecute=false,RedirectStandardOutput=true,RedirectStandardError=true};
        foreach(var arg in new[]{"-NoProfile","-File",Path.Combine(root,"scripts/build-linux-supervisor.ps1"),"-OutputDirectory",folder,"-IncludeHostGuardian"})build.ArgumentList.Add(arg);
        var compiled=await JoinAsync(build,TimeSpan.FromSeconds(40));await File.WriteAllTextAsync(Path.Combine(folder,"build.log"),compiled.StdOut+compiled.StdErr);
        if(compiled.ExitCode!=0)throw new InvalidOperationException("Guardian preparation failed: "+compiled.StdErr);
        var launch=new ProcessStartInfo(Path.Combine(folder,"host-guardian")){UseShellExecute=false,RedirectStandardOutput=true,RedirectStandardError=true};
        foreach(var arg in new[]{Path.Combine(folder,"guardian.json"),"30000",Path.Combine(Environment.GetEnvironmentVariable("DOTNET_ROOT")!,"dotnet"),typeof(GmHelperContractScenario).Assembly.Location,"terminal-main-helper-contract",folder,folder})launch.ArgumentList.Add(arg);
        var guarded=await JoinAsync(launch,TimeSpan.FromSeconds(35));await File.WriteAllTextAsync(Path.Combine(folder,"guardian.log"),guarded.StdOut+guarded.StdErr);
        using var guardian=JsonDocument.Parse(await File.ReadAllBytesAsync(Path.Combine(folder,"guardian.json")));var g=guardian.RootElement;
        if(guarded.ExitCode!=0||!g.GetProperty("echild").GetBoolean()||g.GetProperty("emergencySignals").GetInt32()!=0||g.GetProperty("failures").GetInt32()!=0||g.GetProperty("deadline").GetBoolean()||g.GetProperty("driverExitCode").GetInt32()!=0)
            throw new InvalidOperationException("Original helper contract ownership failed: "+guarded.StdErr+guarded.StdOut);
        }
        using var scenario=JsonDocument.Parse(await File.ReadAllBytesAsync(Path.Combine(folder,"scenario.json")));var s=scenario.RootElement;
        if(!s.GetProperty("Success").GetBoolean())throw new InvalidOperationException("Original contract fixture did not settle.");
        return(s.GetProperty("ExitCode").GetInt32(),s.GetProperty("StdOut").GetString()!,s.GetProperty("StdErr").GetString()!);
    }

    internal static async Task<int> RunDriverAsync(string folder)
    {
        var evidence=new Dictionary<string,object?>();var roots=new List<FileSystemManager>();
        try
        {
            var command=await File.ReadAllTextAsync(Path.Combine(folder,"original.ps1"));
            // These are literal test-owned fixture operands, not production discovery.
            foreach(Match match in Regex.Matches(command,@"Initialize-BoeGmTurnHelper -GameSessionPath '((?:[^']|'')*)'"))
            {
                var session=match.Groups[1].Value.Replace("''","'");
                if(Path.GetFileName(session)!="game_session"||!Directory.Exists(session))throw new InvalidDataException("Original fixture session is absent.");
                if(roots.Any(f=>f.GameSessionPath==session))continue;
                roots.Add(await PrepareSessionAsync(session));
            }
            var glue=Path.Combine(TestRepoPaths.RepoRoot,"tests/fixtures/GmTurnHelper/contract-bootstrap.ps1");
            string Q(string value)=>"'"+value.Replace("'","''")+"'";
            // Exact literal emitted by these test bodies; works at either a newline
            // or a semicolon command boundary without rewriting their business code.
            var sourceCommand=". "+Q(Path.Combine(TestRepoPaths.RepoRoot,"BookOfEternityClient","Launcher","GM_Turn_Helper.ps1"));
            var insertionCount=Regex.Matches(command,Regex.Escape(sourceCommand)).Count;
            if(roots.Count!=0&&insertionCount==0)throw new InvalidDataException("Original fixture helper dot-source was not identified before launch.");
            command=command.Replace(sourceCommand,sourceCommand+"; . "+Q(glue)+" -Folder "+Q(folder)+" -TestSupport "+Q(typeof(GmHelperContractScenario).Assembly.Location),StringComparison.Ordinal);
            evidence["BootstrapInsertions"]=insertionCount;
            await File.WriteAllTextAsync(Path.Combine(folder,"owned.ps1"),command,new UTF8Encoding(OperatingSystem.IsWindows()));
            var start=new ProcessStartInfo(OperatingSystem.IsWindows()?"powershell.exe":"pwsh"){UseShellExecute=false,RedirectStandardOutput=true,RedirectStandardError=true};
            foreach(var arg in new[]{"-NoLogo","-NoProfile","-NonInteractive","-ExecutionPolicy","Bypass","-File",Path.Combine(folder,"owned.ps1")})start.ArgumentList.Add(arg);
            var execution=await JoinAsync(start,TimeSpan.FromSeconds(25));evidence["ExitCode"]=execution.ExitCode;evidence["StdOut"]=execution.StdOut;evidence["StdErr"]=execution.StdErr;
            var joined=File.Exists(Path.Combine(folder,"joined.jsonl"))?File.ReadAllLines(Path.Combine(folder,"joined.jsonl")).Select(x=>JsonDocument.Parse(x)).ToArray():[];
            try
            {
                var reports=Directory.GetFiles(folder,"helper-child-*.json");
                if(roots.Count!=0&&reports.Length==0)throw new InvalidOperationException("Original helper role was never launched.");
                if(joined.Length!=reports.Length||joined.Select(x=>x.RootElement.GetProperty("ProcessId").GetInt32()).Distinct().Count()!=joined.Length)throw new InvalidOperationException("Original helper join coverage mismatch.");
                foreach(var path in reports){using var child=JsonDocument.Parse(File.ReadAllBytes(path));var c=child.RootElement;var j=joined.Single(x=>x.RootElement.GetProperty("ProcessId").GetInt32()==c.GetProperty("ProcessId").GetInt32()).RootElement;
                    if(!c.GetProperty("Completed").GetBoolean()||!j.GetProperty("ExitedBeforeDispose").GetBoolean()||j.GetProperty("ExitCode").GetInt32()!=c.GetProperty("ExitCode").GetInt32())throw new InvalidOperationException("Original helper was forced or did not reach its joined exit.");}
                evidence["JoinedHelperChildren"]=joined.Select(x=>x.RootElement.Clone()).ToArray();
            }finally{foreach(var item in joined)item.Dispose();}
            evidence["Success"]=true;return 0;
        }
        catch(Exception failure){evidence["Failure"]=failure.ToString();return 1;}
        finally
        {
            var failures=new List<string>();foreach(var files in roots){try{await using var lease=await files.AcquireCanonicalWriteLeaseAsync();if(files.ReadExistingSessionGeneration(lease)==null)throw new InvalidDataException("Fixture generation lost.");}catch(Exception failure){failures.Add(failure.ToString());}}
            evidence["CanonicalOwnershipReleased"]=failures.Count==0;if(failures.Count!=0){evidence["CleanupFailures"]=failures;evidence["Success"]=false;}
            await File.WriteAllTextAsync(Path.Combine(folder,"scenario.json"),JsonSerializer.Serialize(evidence));
        }
    }

    internal static async Task<FileSystemManager> PrepareSessionAsync(string session)
    {
        if(Path.GetFileName(session)!="game_session"||!Directory.Exists(session))throw new InvalidDataException("Original fixture session is absent.");
        var files=new FileSystemManager(Path.GetDirectoryName(session)!,NullLogger<FileSystemManager>.Instance);
        await using var lease=await files.AcquireCanonicalWriteLeaseAsync();
        var config=await files.ReadFileBytesAsync(lease,"config.json");
        files.BootstrapLocalStorage(lease,config,JsonSerializer.SerializeToUtf8Bytes(new GameSettings()));
        if(files.ReadExistingSessionGeneration(lease)==null)throw new InvalidDataException("Original fixture generation is absent after bootstrap.");
        return files;
    }

    private static async Task<(int ExitCode,string StdOut,string StdErr)> JoinAsync(ProcessStartInfo start,TimeSpan budget)
    {
        using var process=Process.Start(start)??throw new InvalidOperationException("Original fixture process did not start.");
        var stdout=process.StandardOutput.ReadToEndAsync();var stderr=process.StandardError.ReadToEndAsync();
        try{await process.WaitForExitAsync().WaitAsync(budget);await Task.WhenAll(stdout,stderr).WaitAsync(budget);return(process.ExitCode,await stdout,await stderr);}
        finally{if(!process.HasExited){process.Kill();await process.WaitForExitAsync();}await Task.WhenAll(stdout,stderr);}
    }
}
