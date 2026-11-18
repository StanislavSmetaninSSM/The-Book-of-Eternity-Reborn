using System.Diagnostics;
using System.Text.Json;
using Xunit;
namespace BookOfEternityClient.Tests;

// #1589: inert shell scheduling evidence, not a daemon/world acceptance test.
public sealed class LinuxPowerShellFinallyDiagnosticsTests : IClassFixture<PowerShellFinallyGuardianFixture>
{
    private readonly PowerShellFinallyGuardianFixture fixture;
    public LinuxPowerShellFinallyDiagnosticsTests(PowerShellFinallyGuardianFixture fixture) => this.fixture = fixture;

    [Theory]
    [InlineData("direct", "cooperative")]
    [InlineData("nested", "cooperative")]
    [InlineData("direct", "ctrl-c")]
    [InlineData("nested", "ctrl-c")]
    public async Task OwnedForegroundFinallyMarkers(string shape, string stop)
    {
        var folder = Path.Combine(fixture.Root, shape + "-" + stop);
        Directory.CreateDirectory(folder);
        var guardian = Path.Combine(folder, "guardian.json");
        await PowerShellFinallyGuardianFixture.RunAsync(Path.Combine(fixture.Root,"package/host-guardian"),
            [guardian,"20000","/usr/bin/python3",Path.Combine(TestRepoPaths.RepoRoot,"tests/fixtures/PowerShellFinally/owned-pty.py"),
                Path.Combine(folder,"peer"),shape,stop], folder, 25);
        using var report = JsonDocument.Parse(File.ReadAllBytes(guardian));
        Assert.True(report.RootElement.GetProperty("echild").GetBoolean());
        Assert.Equal(0, report.RootElement.GetProperty("driverExitCode").GetInt32());
        Assert.Equal(0, report.RootElement.GetProperty("emergencySignals").GetInt32());
        Assert.Equal(0, report.RootElement.GetProperty("failures").GetInt32());
        Assert.False(report.RootElement.GetProperty("deadline").GetBoolean());
        using var result = JsonDocument.Parse(File.ReadAllBytes(Path.Combine(folder,"peer/result.json")));
        Assert.True(result.RootElement.GetProperty("Success").GetBoolean());
        Assert.True(result.RootElement.GetProperty("EOF").GetBoolean());
        Assert.True(result.RootElement.GetProperty("TermiosRestored").GetBoolean());
        // Ctrl-C marker presence/absence and actual exit code remain diagnostic observations.
        // Cooperative markers are independently required by the Python driver as controls.
    }
}

public sealed class PowerShellFinallyGuardianFixture : IAsyncLifetime
{
    public string Root { get; } = Path.Combine(TestDiagnosticPaths.Root,"powershell-finally",Guid.NewGuid().ToString("N"));
    public async Task InitializeAsync()
    {
        Assert.True(OperatingSystem.IsLinux());
        Directory.CreateDirectory(Root);
        // One immutable native package per four-case class; never reuse ownership or a PTY.
        await RunAsync("pwsh",["-NoLogo","-NoProfile","-File",Path.Combine(TestRepoPaths.RepoRoot,"scripts/build-linux-supervisor.ps1"),
            "-OutputDirectory",Path.Combine(Root,"package"),"-IncludeHostGuardian"],Root,50);
    }
    public Task DisposeAsync() => Task.CompletedTask; // Preserve complete raw diagnostic artifacts.
    internal static async Task RunAsync(string executable, string[] arguments, string folder, int seconds)
    {
        var start = new ProcessStartInfo(executable) { UseShellExecute=false, RedirectStandardOutput=true, RedirectStandardError=true };
        foreach(var argument in arguments) start.ArgumentList.Add(argument);
        using var process = Process.Start(start)!;
        await using var stdout = File.Create(Path.Combine(folder,"stdout.raw"));
        await using var stderr = File.Create(Path.Combine(folder,"stderr.raw"));
        var output = process.StandardOutput.BaseStream.CopyToAsync(stdout);
        var error = process.StandardError.BaseStream.CopyToAsync(stderr);
        Exception? originalFailure = null;
        var cleanupFailures = new List<string>();
        try { await process.WaitForExitAsync().WaitAsync(TimeSpan.FromSeconds(seconds)); }
        catch(Exception failure) { originalFailure = failure; }
        // One shared cleanup budget bounds both joining and inherited-pipe draining.
        var cleanupDeadline = Stopwatch.StartNew();
        TimeSpan Remaining() => TimeSpan.FromMilliseconds(Math.Max(1, 5000-cleanupDeadline.Elapsed.TotalMilliseconds));
        try
        {
            if(!process.HasExited) process.Kill(entireProcessTree:true);
            await process.WaitForExitAsync().WaitAsync(Remaining());
        }
        catch(Exception failure) { cleanupFailures.Add("join: " + failure); }
        try { await Task.WhenAll(output,error).WaitAsync(Remaining()); }
        catch(Exception failure) { cleanupFailures.Add("drain: " + failure); }
        if(cleanupFailures.Count > 0)
        {
            // Closing only our reader handles releases pending local copies; no foreign PID lookup.
            process.StandardOutput.Dispose(); process.StandardError.Dispose();
            _ = Task.WhenAll(output,error).ContinueWith(t => { _ = t.Exception; },
                TaskContinuationOptions.OnlyOnFaulted | TaskContinuationOptions.ExecuteSynchronously);
        }
        await File.WriteAllTextAsync(Path.Combine(folder,"process-outcome.json"),JsonSerializer.Serialize(new {
            OriginalFailure=originalFailure?.ToString(), CleanupFailures=cleanupFailures,
            Joined=process.HasExited, StdoutDrained=output.IsCompletedSuccessfully,
            StderrDrained=error.IsCompletedSuccessfully
        }));
        if(originalFailure is not null)
            System.Runtime.ExceptionServices.ExceptionDispatchInfo.Capture(originalFailure).Throw();
        Assert.True(cleanupFailures.Count==0,string.Join("\n",cleanupFailures));
        Assert.True(process.ExitCode==0,$"Owned diagnostic process failed ({process.ExitCode}); raw artifacts: {folder}");
    }
}
