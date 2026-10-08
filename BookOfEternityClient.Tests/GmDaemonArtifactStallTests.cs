using System.Diagnostics;
using Xunit;
namespace BookOfEternityClient.Tests;
public sealed class GmDaemonArtifactStallTests
{
    [Theory]
    [InlineData("echo-only")]
    [InlineData("visible-stall")]
    [InlineData("output-progress")]
    [InlineData("ready")]
    [InlineData("intent-cleared")]
    [InlineData("missing-diagnostics")]
    public async Task ActualDaemon_CurrentVisibleIntent(string scenario)
    {
        var start=new ProcessStartInfo("pwsh"){UseShellExecute=false,RedirectStandardOutput=true,RedirectStandardError=true};
        foreach(var arg in new[]{"-NoProfile","-File",Path.Combine(TestRepoPaths.RepoRoot,"tests/fixtures/ProductionMain/daemon-artifact-stall.ps1"),"-RepoRoot",TestRepoPaths.RepoRoot,"-Scenario",scenario})start.ArgumentList.Add(arg);
        using var process=Process.Start(start)!;
        var output=process.StandardOutput.ReadToEndAsync();var error=process.StandardError.ReadToEndAsync();
        await process.WaitForExitAsync().WaitAsync(TimeSpan.FromSeconds(8));
        Assert.True(process.ExitCode==0,(await output)+(await error));
    }
}
