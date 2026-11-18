using System.Diagnostics;
using System.Text.Json;
using Xunit;
namespace BookOfEternityClient.Tests;

public sealed class SpectreSelectionPtyTests
{
    [Theory]
    [InlineData("pty")]
    [InlineData("captured-pipe")]
    public async Task ActualDefaultPromptUsesOwnedClientConsole(string mode)
    {
        Assert.True(OperatingSystem.IsLinux());
        var root = Path.Combine(TestDiagnosticPaths.Root, "spectre-selection", Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(root);
        var package = Path.Combine(root,"package");
        await RunAsync("pwsh", ["-NoLogo","-NoProfile","-File",
            Path.Combine(TestRepoPaths.RepoRoot,"scripts/build-linux-supervisor.ps1"),
            "-OutputDirectory",package,"-IncludeHostGuardian"], root, "build", 50);
        var helper = Path.Combine(AppContext.BaseDirectory,"BookOfEternityClient.TestSupport.dll");
        Assert.True(File.Exists(helper));
        await RunAsync(Path.Combine(package,"host-guardian"),
            [Path.Combine(root,"guardian.json"),"20000","/usr/bin/python3",
             Path.Combine(TestRepoPaths.RepoRoot,"tests/fixtures/SelectionPrompt/smoke.py"),
             Path.Combine(root,"peer"),helper,mode], root, "guardian", 25);
        using var guardian = JsonDocument.Parse(File.ReadAllBytes(Path.Combine(root,"guardian.json")));
        var g = guardian.RootElement;
        Assert.True(g.GetProperty("echild").GetBoolean());
        Assert.Equal(0,g.GetProperty("driverExitCode").GetInt32());
        Assert.Equal(0,g.GetProperty("emergencySignals").GetInt32());
        Assert.Equal(0,g.GetProperty("failures").GetInt32());
        Assert.False(g.GetProperty("deadline").GetBoolean());
        using var result=JsonDocument.Parse(File.ReadAllBytes(Path.Combine(root,"peer/result.json")));
        Assert.True(result.RootElement.GetProperty("Success").GetBoolean(), result.RootElement.ToString());
        Assert.True(result.RootElement.GetProperty("EOF").GetBoolean());
        Assert.True(result.RootElement.GetProperty("TermiosRestored").GetBoolean());
    }

    private static async Task RunAsync(string executable, string[] args, string root, string label, int seconds)
    {
        var stdout=File.Create(Path.Combine(root,label+".stdout.raw"));
        var stderr=File.Create(Path.Combine(root,label+".stderr.raw"));
        var start=new ProcessStartInfo(executable) {
            UseShellExecute=false,RedirectStandardOutput=true,RedirectStandardError=true,
            WorkingDirectory=TestRepoPaths.RepoRoot
        };
        foreach(var arg in args) start.ArgumentList.Add(arg);
        var process=Process.Start(start) ?? throw new IOException("Selection smoke process did not start.");
        var output=process.StandardOutput.BaseStream.CopyToAsync(stdout);
        var error=process.StandardError.BaseStream.CopyToAsync(stderr);
        var exit=process.WaitForExitAsync();
        int? observedExitCode = null;
        Exception? failure=null;
        var cleanupFailures=new List<string>();
        try { await Task.WhenAll(exit,output,error).WaitAsync(TimeSpan.FromSeconds(seconds)); }
        catch(Exception exception) { failure=exception; }
        finally
        {
            using var cleanup = new CancellationTokenSource(TimeSpan.FromSeconds(5));
            async Task Observe(Task task, string step)
            {
                try { await task.WaitAsync(cleanup.Token); }
                catch(Exception exception) { cleanupFailures.Add(step + ": " + exception); }
            }
            try { if(!process.HasExited) process.Kill(entireProcessTree:true); }
            catch(Exception exception) { cleanupFailures.Add("kill: " + exception); }
            await Observe(exit, "exit");
            await Observe(output, "stdout-copy");
            await Observe(error, "stderr-copy");
            async Task SettleWriter(FileStream writer, Task pump, string name)
            {
                if (!pump.IsCompleted) { cleanupFailures.Add(name + ": active copy retained; writer not disposed"); return; }
                Task? flush = null;
                try { flush = writer.FlushAsync(cleanup.Token); await Observe(flush, name + "-flush"); }
                catch(Exception exception) { cleanupFailures.Add(name + "-flush-start: " + exception); }
                if (flush is { IsCompleted: false })
                { cleanupFailures.Add(name + ": active flush retained; writer not disposed"); return; }
                try { await Observe(writer.DisposeAsync().AsTask(), name + "-dispose"); }
                catch(Exception exception) { cleanupFailures.Add(name + "-dispose-start: " + exception); }
            }
            await SettleWriter(stdout, output, "stdout");
            await SettleWriter(stderr, error, "stderr");
            try
            {
                await File.WriteAllTextAsync(Path.Combine(root,label+".outcome.json"),JsonSerializer.Serialize(new {
                    Failure=failure?.ToString(), CleanupFailures=cleanupFailures,
                    ExitTaskStatus=exit.Status.ToString(), Exited=process.HasExited,
                    ExitCode=process.HasExited?(int?)process.ExitCode:null,
                    StdoutTaskStatus=output.Status.ToString(), StderrTaskStatus=error.Status.ToString()
                }), cleanup.Token).WaitAsync(cleanup.Token);
            }
            catch(Exception exception) { cleanupFailures.Add("outcome: " + exception); }
            if (exit.IsCompletedSuccessfully && process.HasExited) observedExitCode = process.ExitCode;
            // Process streams must not be disposed while either original pump remains active.
            if (exit.IsCompleted && output.IsCompleted && error.IsCompleted)
            {
                try { process.Dispose(); }
                catch(Exception exception) { cleanupFailures.Add("process-dispose: " + exception); }
            }

        }
        Assert.True(failure is null && cleanupFailures.Count==0 && observedExitCode==0,
            $"Selection smoke contracts failed. Evidence: {root}\n{failure}\n{string.Join("\n",cleanupFailures)}");
    }
}
