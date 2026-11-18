using System.Diagnostics;
using Xunit;
namespace BookOfEternityClient.Tests;
public sealed class LinuxBridgeRestartDispatchTests
{
    [Theory]
    [InlineData("stale-pid")][InlineData("hidden")][InlineData("redirected")]
    public async Task ActualLauncherRetainedPidCannotReplaceLinuxOwnerAdmission(string scenario)
    {
        Assert.True(OperatingSystem.IsLinux());
        var root=Path.Combine(TestDiagnosticPaths.Root,"bridge-restart-dispatch",Guid.NewGuid().ToString("N"));
        var start=new ProcessStartInfo("/usr/bin/python3"){UseShellExecute=false,RedirectStandardOutput=true,RedirectStandardError=true};
        foreach(var arg in new[]{Path.Combine(TestRepoPaths.RepoRoot,"tests/fixtures/BridgeRestart/dispatch.py"),TestRepoPaths.RepoRoot,root,scenario})start.ArgumentList.Add(arg);
        using var process=Process.Start(start)!;
        var output=process.StandardOutput.ReadToEndAsync();var error=process.StandardError.ReadToEndAsync();
        try{await process.WaitForExitAsync().WaitAsync(TimeSpan.FromSeconds(20));}
        finally{if(!process.HasExited){process.Kill(true);await process.WaitForExitAsync();}await Task.WhenAll(output,error);}
        Assert.True(process.ExitCode==0,(await output)+(await error));
    }
}
