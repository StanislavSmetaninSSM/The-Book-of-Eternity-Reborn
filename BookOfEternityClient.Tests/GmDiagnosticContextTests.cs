using System.Diagnostics;
using System.Text.Json;
using Xunit;
namespace BookOfEternityClient.Tests;
public sealed class GmDiagnosticContextTests
{
    [Theory]
    [InlineData("main-close")]
    [InlineData("dedicated-initialize")]
    [InlineData("missing-write")]
    [InlineData("throwing-write")]
    [InlineData("missing-timeout")]
    [InlineData("throwing-timeout")]
    public async Task OptionalDiagnosticContextCannotPreventOriginalProtocol(string testCase)
    {
        Assert.True(OperatingSystem.IsLinux());
        var root=Path.Combine(TestDiagnosticPaths.Root,"diagnostic-context",Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(root);
        var stdoutPath=Path.Combine(root,"stdout.raw");
        var stderrPath=Path.Combine(root,"stderr.raw");
        var stdout=File.Create(stdoutPath);
        var stderr=File.Create(stderrPath);
        var start=new ProcessStartInfo("pwsh") {
            UseShellExecute=false,RedirectStandardOutput=true,RedirectStandardError=true,
            WorkingDirectory=TestRepoPaths.RepoRoot
        };
        start.ArgumentList.Add("-NoProfile");
        start.ArgumentList.Add("-File");
        start.ArgumentList.Add(Path.Combine(TestRepoPaths.RepoRoot,"tests/fixtures/GmDiagnosticContext/check.ps1"));
        start.ArgumentList.Add("-Repo");start.ArgumentList.Add(TestRepoPaths.RepoRoot);
        start.ArgumentList.Add("-Case");start.ArgumentList.Add(testCase);
        var process=Process.Start(start) ?? throw new IOException("PowerShell diagnostic-context process did not start.");
        var output=process.StandardOutput.BaseStream.CopyToAsync(stdout);
        var error=process.StandardError.BaseStream.CopyToAsync(stderr);
        var exit=process.WaitForExitAsync();
        int? observedExitCode = null;
        Exception? failure=null;
        var cleanupFailures=new List<string>();
        try { await Task.WhenAll(exit,output,error).WaitAsync(TimeSpan.FromSeconds(15)); }
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
                await File.WriteAllTextAsync(Path.Combine(root,"outcome.json"),JsonSerializer.Serialize(new {
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
            $"PowerShell diagnostic-context contracts failed. Evidence: {root}\n{failure}\n{string.Join("\n",cleanupFailures)}");
    }
}
