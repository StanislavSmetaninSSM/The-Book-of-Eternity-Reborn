using System.Diagnostics;
using System.Text.Json;
using BookOfEternityClient.Core;
using BookOfEternityClient.Services.GmRuntime;
using Microsoft.Extensions.Logging.Abstractions;
using Xunit;
namespace BookOfEternityClient.Tests;
public sealed class GmMainParticipatingConsumerTests
{
    [Fact]
    public async Task PrepareCli_AbsentRootHasNoEffects()
    {
        var root=Path.Combine(Path.GetTempPath(),"f2-absent-"+Guid.NewGuid().ToString("N"));
        var configuration=new DirectoryInfo(AppContext.BaseDirectory).Parent!.Name;
        var start=new ProcessStartInfo("dotnet"){UseShellExecute=false,RedirectStandardOutput=true,RedirectStandardError=true};
        foreach(var a in new[]{Path.Combine(TestRepoPaths.RepoRoot,"BookOfEternityClient/bin",configuration,"net8.0/BookOfEternityClient.dll"),"--base-path",root,"--prepare-live-turn","--action","neutral"})start.ArgumentList.Add(a);
        using var process=Process.Start(start)!;var output=process.StandardOutput.ReadToEndAsync();var errors=process.StandardError.ReadToEndAsync();
        try {await process.WaitForExitAsync().WaitAsync(TimeSpan.FromSeconds(10));}
        finally {if(!process.HasExited){process.Kill();await process.WaitForExitAsync();}}
        try {Assert.Equal(2,process.ExitCode);Assert.False(Directory.Exists(root));}
        finally {await output;await errors;if(Directory.Exists(root))Directory.Delete(root,true);}
    }
    [Theory]
    [InlineData("start-daemon")]
    [InlineData("generate-session")]
    [InlineData("register-window")]
    public async Task RealAncillary_StoppingHasNoCanonicalEffect(string scenario)
    {
        var root=Path.Combine(Path.GetTempPath(),"f2-ancillary-"+Guid.NewGuid().ToString("N"));Directory.CreateDirectory(root);
        try {
            var files=new FileSystemManager(root,NullLogger<FileSystemManager>.Instance);
            var id=new GmSessionRunIdentity(files.BasePath,Guid.NewGuid().ToString("N"),Guid.NewGuid().ToString("N"),1,OperatingSystem.IsWindows()?GmSessionRunBackend.WindowsJob:GmSessionRunBackend.LinuxSupervisor,Guid.NewGuid().ToString("N"),"fixture-boot");
            var recordPath=Path.Combine(root,".boe_runtime/gm-runs/main.json");Directory.CreateDirectory(Path.GetDirectoryName(recordPath)!);File.WriteAllBytes(recordPath,GmSessionRunRecordCodec.Encode(new(1,id,GmSessionRunDisposition.Stopping,null)));
            var start=new ProcessStartInfo("pwsh"){UseShellExecute=false,RedirectStandardOutput=true,RedirectStandardError=true};
            foreach(var a in new[]{"-NoProfile","-NonInteractive","-File",Path.Combine(TestRepoPaths.RepoRoot,"tests/fixtures/GmMainOperation/ancillary.ps1"),"-RepoRoot",TestRepoPaths.RepoRoot,"-SessionPath",files.GameSessionPath,"-Scenario",scenario})start.ArgumentList.Add(a);
            using var process=Process.Start(start)!;var output=process.StandardOutput.ReadToEndAsync();var errors=process.StandardError.ReadToEndAsync();
            try {await process.WaitForExitAsync().WaitAsync(TimeSpan.FromSeconds(12));}finally {if(!process.HasExited){process.Kill(true);await process.WaitForExitAsync();}}
            Assert.True(process.ExitCode==0,await errors);Assert.False(Directory.Exists(files.GameSessionPath));await output;
        } finally {Directory.Delete(root,true);}
    }
    [Theory]
    [InlineData("daemon-status",false)]
    [InlineData("daemon-stale",true)]
    [InlineData("bootstrap",false)]
    [InlineData("context-copy",false)]
    [InlineData("launcher-stale",true)]
    [InlineData("launcher-delete",true)]
    public async Task RealPowerShell_StoppingHasNoCanonicalEffect(string scenario,bool preexisting)
    {
        var root=Path.Combine(Path.GetTempPath(),"f2-consumer-"+Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(root);
        try {
            var files=new FileSystemManager(root,NullLogger<FileSystemManager>.Instance);files.EnsureDirectoryStructure();
            var id=new GmSessionRunIdentity(files.BasePath,Guid.NewGuid().ToString("N"),Guid.NewGuid().ToString("N"),1,
                OperatingSystem.IsWindows()?GmSessionRunBackend.WindowsJob:GmSessionRunBackend.LinuxSupervisor,Guid.NewGuid().ToString("N"),"fixture-boot");
            var recordPath=Path.Combine(root,".boe_runtime/gm-runs/main.json");Directory.CreateDirectory(Path.GetDirectoryName(recordPath)!);
            var record=GmSessionRunRecordCodec.Encode(new(1,id,GmSessionRunDisposition.Stopping,null));File.WriteAllBytes(recordPath,record);
            var status=files.ResolvePath("game_state/control/gm_bridge_status.json");
            if(preexisting)File.WriteAllText(status,"{\"helperPid\":2147483647,\"pipeName\":\"inert\"}");
            var before=preexisting?File.ReadAllBytes(status):null;
            var start=new ProcessStartInfo("pwsh"){UseShellExecute=false,RedirectStandardOutput=true,RedirectStandardError=true};
            foreach(var a in new[]{"-NoProfile","-NonInteractive","-File",Path.Combine(TestRepoPaths.RepoRoot,"tests/fixtures/GmMainOperation/consumers.ps1"),"-RepoRoot",TestRepoPaths.RepoRoot,"-ClientRoot",root,"-Scenario",scenario})start.ArgumentList.Add(a);
            using var process=Process.Start(start)!;var output=process.StandardOutput.ReadToEndAsync();var error=process.StandardError.ReadToEndAsync();
            try{await process.WaitForExitAsync().WaitAsync(TimeSpan.FromSeconds(12));}finally{if(!process.HasExited){process.Kill(true);await process.WaitForExitAsync();}}
            Assert.True(process.ExitCode==0,await error);using var result=JsonDocument.Parse((await output).Trim());
            Assert.Equal(preexisting,result.RootElement.GetProperty("exists").GetBoolean());
            Assert.False(result.RootElement.GetProperty("contextDirectory").GetBoolean());
            Assert.Equal(record,File.ReadAllBytes(recordPath));if(before!=null)Assert.Equal(before,File.ReadAllBytes(status));
        } finally{Directory.Delete(root,true);Assert.False(Directory.Exists(root));}
    }
}
