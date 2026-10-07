using System.Diagnostics;
using System.Text.Json;
using Xunit;
namespace BookOfEternityClient.Tests;
public sealed class GmRelayTransportTests
{
    [Fact]
    public async Task ActualDriver_RetainsTurnReceiptAndRefusesUnclosedRollback()
    {
        var start=new ProcessStartInfo("/usr/bin/python3"){UseShellExecute=false,RedirectStandardOutput=true,RedirectStandardError=true};
        start.ArgumentList.Add(Path.Combine(TestRepoPaths.RepoRoot,"tests/fixtures/ProductionMain/relay-driver-checks.py"));
        using var p=Process.Start(start)!;var stdout=p.StandardOutput.ReadToEndAsync();var stderr=p.StandardError.ReadToEndAsync();
        await p.WaitForExitAsync().WaitAsync(TimeSpan.FromSeconds(8));Assert.True(p.ExitCode==0,(await stdout)+(await stderr));
    }
    [Theory]
    [InlineData("paste")]
    [InlineData("once")]
    [InlineData("wrong-reply")]
    [InlineData("stale")]
    [InlineData("close-held-child")]
    [InlineData("guardian-budgets")]
    public async Task RealRelay_IsolatedTransportAndClosure(string mode)
    {
        var repo=TestRepoPaths.RepoRoot;
        var own=Path.Combine("/tmp","rt-"+Guid.NewGuid().ToString("N"));Directory.CreateDirectory(own);
        async Task<int> Run(string executable,string[] arguments,string log,int seconds=40)
        {
            var start=new ProcessStartInfo(executable){UseShellExecute=false,RedirectStandardOutput=true,RedirectStandardError=true};
            foreach(var a in arguments)start.ArgumentList.Add(a);
            using var p=Process.Start(start)!;
            await LinuxFallbackSupervisorTests.NativeRun.ObserveBuild(p,TimeSpan.FromSeconds(seconds),Path.Combine(own,log));return p.ExitCode;
        }
        Assert.Equal(0,await Run("pwsh",["-NoProfile","-File",Path.Combine(repo,"scripts/build-linux-supervisor.ps1"),"-OutputDirectory",own,"-IncludeHostGuardian"],"prepare.log"));
        var guardian=Path.Combine(own,"host-guardian");
        var code=await Run(guardian,[Path.Combine(own,"guardian.json"),"15000","/usr/bin/python3",Path.Combine(repo,"tests/fixtures/ProductionMain/relay-transport-checks.py"),mode,own,guardian],"scenario.log",20);
        using var proof=JsonDocument.Parse(File.ReadAllBytes(Path.Combine(own,"guardian.json")));
        Assert.True(proof.RootElement.GetProperty("echild").GetBoolean());
        Assert.Equal(0,proof.RootElement.GetProperty("emergencySignals").GetInt32());
        Assert.Equal(0,proof.RootElement.GetProperty("failures").GetInt32());
        Assert.False(proof.RootElement.GetProperty("deadline").GetBoolean());
        Assert.True(proof.RootElement.GetProperty("driverExitCode").GetInt32()==0,File.ReadAllText(Path.Combine(own,"scenario.log")));
        Assert.True(code==0,File.ReadAllText(Path.Combine(own,"scenario.log")));
    }
}
