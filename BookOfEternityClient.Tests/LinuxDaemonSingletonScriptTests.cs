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
        var source=File.ReadAllText(Path.Combine(TestRepoPaths.RepoRoot,"BookOfEternityClient/game_master_daemon.ps1")).ReplaceLineEndings("\n");
        var helper=File.ReadAllText(Path.Combine(TestRepoPaths.RepoRoot,"BookOfEternityClient/Launcher/gm_daemon_instance.ps1"));
        var admission=source.IndexOf("try {\nStart-LinuxGmDaemonInstance",StringComparison.Ordinal);
        Assert.True(admission>=0);
        var bootstrap=source.IndexOf("Invoke-GmParticipatingConsumer $GameSessionPath {",StringComparison.Ordinal);
        Assert.True(bootstrap>=0);Assert.True(admission<bootstrap);
        var platform=helper.IndexOf("$PSVersionTable.PSEdition -eq 'Core' -and $IsLinux",StringComparison.Ordinal);
        var load=helper.IndexOf("Add-Type -Path",StringComparison.Ordinal);
        Assert.True(platform>=0 && load>=0 && platform<load);
        Assert.Contains("finally { $script:LinuxDaemonGuard = $null }",source,StringComparison.Ordinal);
        Assert.Contains("$script:DaemonTerminalSettled = $true",source,StringComparison.Ordinal);
    }

    [Theory]
    [InlineData("missing")][InlineData("empty")][InlineData("initial-write-fails")][InlineData("terminal-readback-fails")][InlineData("wrong-session")][InlineData("malformed-json")][InlineData("malformed-utf8")]
    public async Task ActualStartPreservesMissingStatusAndTerminalConfirmation(string scenario)
    {
        var root=Path.Combine(TestDiagnosticPaths.Root,"daemon-actual-start",Guid.NewGuid().ToString("N"));
        var start=new ProcessStartInfo("pwsh"){UseShellExecute=false,RedirectStandardOutput=true,RedirectStandardError=true};
        foreach(var arg in new[]{"-NoProfile","-File",Path.Combine(TestRepoPaths.RepoRoot,"tests/fixtures/GmDaemonSingleton/start.ps1"),"-RepoRoot",TestRepoPaths.RepoRoot,"-Root",root,"-Scenario",scenario})start.ArgumentList.Add(arg);
        using var process=Process.Start(start)!;
        var output=process.StandardOutput.ReadToEndAsync();var error=process.StandardError.ReadToEndAsync();
        try {
            await process.WaitForExitAsync().WaitAsync(TimeSpan.FromSeconds(10));
            Assert.True(process.ExitCode==0,(await output)+(await error));
        } finally {
            if(!process.HasExited){process.Kill(true);await process.WaitForExitAsync();}
            await Task.WhenAll(output,error);
            if(Directory.Exists(root))Directory.Delete(root,true);
        }
    }

    [Fact]
    public async Task ActualFatalPublisherDoesNotWriteAfterGuardLoss()
    {
        var start=new ProcessStartInfo("pwsh"){UseShellExecute=false,RedirectStandardOutput=true,RedirectStandardError=true};
        foreach(var arg in new[]{"-NoProfile","-File",Path.Combine(TestRepoPaths.RepoRoot,"tests/fixtures/GmDaemonSingleton/fatal.ps1"),"-RepoRoot",TestRepoPaths.RepoRoot})start.ArgumentList.Add(arg);
        using var process=Process.Start(start)!;var output=process.StandardOutput.ReadToEndAsync();var error=process.StandardError.ReadToEndAsync();
        try {await process.WaitForExitAsync().WaitAsync(TimeSpan.FromSeconds(10));Assert.True(process.ExitCode==0,(await output)+(await error));}
        finally {if(!process.HasExited){process.Kill(true);await process.WaitForExitAsync();}await Task.WhenAll(output,error);}
    }

}
