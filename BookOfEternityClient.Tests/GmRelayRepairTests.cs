using System.Diagnostics;
using System.Text.Json;
using Xunit;
namespace BookOfEternityClient.Tests;
public sealed class GmRelayRepairTests
{
    [Theory]
    [InlineData("real-repair")]
    [InlineData("stale-repair")]
    [InlineData("helper-signature")]
    public async Task RealRepair_ActualDaemonShapeAndHelper(string mode)
    {
        var repo=TestRepoPaths.RepoRoot;var own=Path.Combine("/tmp","rr-"+Guid.NewGuid().ToString("N"));Directory.CreateDirectory(own);
        async Task<int> Run(string exe,string[] args,string log,int seconds)
        {
            var start=new ProcessStartInfo(exe){UseShellExecute=false,RedirectStandardOutput=true,RedirectStandardError=true};foreach(var a in args)start.ArgumentList.Add(a);
            using var p=Process.Start(start)!;await LinuxFallbackSupervisorTests.NativeRun.ObserveBuild(p,TimeSpan.FromSeconds(seconds),Path.Combine(own,log));return p.ExitCode;
        }
        Assert.Equal(0,await Run("pwsh",["-NoProfile","-File",Path.Combine(repo,"scripts/build-linux-supervisor.ps1"),"-OutputDirectory",own,"-IncludeHostGuardian"],"prepare.log",40));
        var code=await Run(Path.Combine(own,"host-guardian"),[Path.Combine(own,"guardian.json"),"15000","/usr/bin/python3",Path.Combine(repo,"tests/fixtures/ProductionMain/relay-repair-checks.py"),mode,own],"scenario.log",20);
        using var g=JsonDocument.Parse(File.ReadAllBytes(Path.Combine(own,"guardian.json")));
        Assert.True(g.RootElement.GetProperty("echild").GetBoolean());Assert.Equal(0,g.RootElement.GetProperty("emergencySignals").GetInt32());Assert.Equal(0,g.RootElement.GetProperty("failures").GetInt32());Assert.False(g.RootElement.GetProperty("deadline").GetBoolean());
        Assert.True(g.RootElement.GetProperty("driverExitCode").GetInt32()==0,File.ReadAllText(Path.Combine(own,"scenario.log")));
        Assert.True(code==0,File.ReadAllText(Path.Combine(own,"scenario.log")));
    }
}
