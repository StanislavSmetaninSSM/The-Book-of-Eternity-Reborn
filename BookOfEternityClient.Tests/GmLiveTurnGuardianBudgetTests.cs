using System.Diagnostics;
using System.Text.Json;
using Xunit;
namespace BookOfEternityClient.Tests;
public sealed class GmLiveTurnGuardianBudgetTests
{
    [Fact]
    public async Task ActualGuardian_DedicatedLiveBudgetKeepsDefaultBound()
    {
        Assert.True(OperatingSystem.IsLinux());
        var folder=Path.Combine(TestRepoPaths.RepoRoot,"TestResults/live-guardian",Guid.NewGuid().ToString("N"));Directory.CreateDirectory(folder);
        var build=new ProcessStartInfo("pwsh"){UseShellExecute=false,RedirectStandardOutput=true,RedirectStandardError=true};
        foreach(var arg in new[]{"-NoProfile","-File",Path.Combine(TestRepoPaths.RepoRoot,"scripts/build-linux-supervisor.ps1"),"-OutputDirectory",folder,"-IncludeHostGuardian"})build.ArgumentList.Add(arg);
        using(var compiler=Process.Start(build)!){var log=await LinuxFallbackSupervisorTests.NativeRun.ObserveBuild(compiler,TimeSpan.FromSeconds(40),Path.Combine(folder,"build.log"));Assert.True(compiler.ExitCode==0,"Preparation failure: "+log);}
        var binary=Path.Combine(folder,"host-guardian");
        async Task<int> Run(string label,params string[] prefix)
        {
            var start=new ProcessStartInfo(binary){UseShellExecute=false,RedirectStandardOutput=true,RedirectStandardError=true};
            foreach(var arg in prefix)start.ArgumentList.Add(arg);
            start.ArgumentList.Add("/bin/true");start.ArgumentList.Add("own-no-op");using var child=Process.Start(start)!;
            await LinuxFallbackSupervisorTests.NativeRun.ObserveBuild(child,TimeSpan.FromSeconds(10),Path.Combine(folder,label+".log"));return child.ExitCode;
        }
        Assert.Equal(64,await Run("default-refusal",Path.Combine(folder,"default.json"),"300000"));
        Assert.Equal(64,await Run("live-overflow-refusal","--live-turn",Path.Combine(folder,"overflow.json"),"300001"));
        Assert.Equal(0,await Run("live-positive","--live-turn",Path.Combine(folder,"guardian.json"),"300000"));
        var report=JsonDocument.Parse(File.ReadAllBytes(Path.Combine(folder,"guardian.json"))).RootElement;
        Assert.True(report.GetProperty("echild").GetBoolean());Assert.Equal(0,report.GetProperty("driverExitCode").GetInt32());
        Assert.Equal(0,report.GetProperty("emergencySignals").GetInt32());Assert.Equal(0,report.GetProperty("failures").GetInt32());Assert.False(report.GetProperty("deadline").GetBoolean());
    }
}
