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
    [Theory]
    [InlineData("success")][InlineData("write-fails")][InlineData("readback-fails")]
    public async Task ActualGuardedStatusWriterRequiresConfirmedPublication(string scenario)
    {
        var start=new ProcessStartInfo("pwsh"){UseShellExecute=false,RedirectStandardOutput=true,RedirectStandardError=true};
        foreach(var arg in new[]{"-NoProfile","-File",Path.Combine(TestRepoPaths.RepoRoot,"tests/fixtures/GmDaemonSingleton/status.ps1"),"-RepoRoot",TestRepoPaths.RepoRoot,"-Scenario",scenario})start.ArgumentList.Add(arg);
        using var process=Process.Start(start)!;
        var output=process.StandardOutput.ReadToEndAsync();var error=process.StandardError.ReadToEndAsync();
        try {await process.WaitForExitAsync().WaitAsync(TimeSpan.FromSeconds(10));}
        finally {if(!process.HasExited){process.Kill(true);await process.WaitForExitAsync();}await Task.WhenAll(output,error);}
        Assert.True(process.ExitCode==0,(await output)+(await error));
    }
    [Fact]
    public void AllEntryGuardPrecedesBootstrapAndNonLinuxReturnsBeforeTypeResolution()
    {
        var source=File.ReadAllText(Path.Combine(TestRepoPaths.RepoRoot,"BookOfEternityClient/game_master_daemon.ps1"));
        var helper=File.ReadAllText(Path.Combine(TestRepoPaths.RepoRoot,"BookOfEternityClient/Launcher/gm_daemon_instance.ps1"));
        Assert.True(source.IndexOf("try {\nStart-LinuxGmDaemonInstance",StringComparison.Ordinal)<source.IndexOf("Invoke-GmParticipatingConsumer $GameSessionPath {",StringComparison.Ordinal));
        Assert.True(helper.IndexOf("$PSVersionTable.PSEdition -eq 'Core' -and $IsLinux",StringComparison.Ordinal)<helper.IndexOf("Add-Type -Path",StringComparison.Ordinal));
        Assert.Contains("finally { $script:LinuxDaemonGuard = $null }",source,StringComparison.Ordinal);
        Assert.Contains("$script:DaemonTerminalSettled = $true",source,StringComparison.Ordinal);
    }

}
