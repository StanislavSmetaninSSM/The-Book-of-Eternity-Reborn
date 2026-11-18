using System.Diagnostics;
using Xunit;
namespace BookOfEternityClient.Tests;
public sealed class LinuxDaemonSingletonScriptTests
{
    [Fact]
    public async Task ActualSingletonFunctionUsesHeldGuardInsteadOfStalePid()
    {
        var start=new ProcessStartInfo("pwsh"){UseShellExecute=false,RedirectStandardOutput=true,RedirectStandardError=true};
        foreach(var arg in new[]{"-NoProfile","-File",Path.Combine(TestRepoPaths.RepoRoot,"tests/fixtures/GmDaemonSingleton/assert.ps1"),"-RepoRoot",TestRepoPaths.RepoRoot})start.ArgumentList.Add(arg);
        using var process=Process.Start(start)!;
        var output=process.StandardOutput.ReadToEndAsync();var error=process.StandardError.ReadToEndAsync();
        try { await process.WaitForExitAsync().WaitAsync(TimeSpan.FromSeconds(10)); }
        finally {
            if(!process.HasExited){process.Kill(true);await process.WaitForExitAsync();}
            await Task.WhenAll(output,error);
        }
        Assert.True(process.ExitCode==0,(await output)+(await error));
    }
}
