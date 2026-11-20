using System.Diagnostics;
using System.Text.Json;
using System.Text.RegularExpressions;
using Xunit;
namespace BookOfEternityClient.Tests;
public sealed class MortalConsoleRouteTests
{
    [Fact]
    public Task FiniteConsoleRouteDataContracts() => RunAsync("test-mortal-console-routes.py", 28);
    [Fact]
    public Task MortalOutcomeUnionDataContracts() => RunAsync("test-mortal-outcome-evidence.py", 7);
    [Fact]
    public Task RetainedDaemonLoadDataContracts() => RunAsync("test-relay-load-evidence.py", 23);
    [Fact]
    public Task SelfSaveLoadTurnDataContracts() => RunAsync("test-self-save-load-turn.py", 20);
    [Fact]
    public Task MortalStorageRetrieveSaveDataContracts() => RunAsync("test-mortal-storage-retrieve-save.py", 39);
    /// <summary>Runs one bounded Python DATA consumer with retained output and complete owned cleanup.</summary>
    internal static async Task RunAsync(string script, int expectedMethods, params string[] arguments)
    {
        Assert.True(OperatingSystem.IsLinux());
        var root=Path.Combine(TestDiagnosticPaths.Root,"mortal-console-routes",Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(root);
        var stdoutPath=Path.Combine(root,"stdout.raw");
        var stderrPath=Path.Combine(root,"stderr.raw");
        FileStream? stdout=null, stderr=null;
        Process? process=null;
        Task output=Task.CompletedTask, error=Task.CompletedTask, exit=Task.CompletedTask;
        var start=new ProcessStartInfo("/usr/bin/python3") {
            UseShellExecute=false,RedirectStandardOutput=true,RedirectStandardError=true,
            WorkingDirectory=TestRepoPaths.RepoRoot
        };
        start.ArgumentList.Add(Path.Combine(TestRepoPaths.RepoRoot,
            "tests/fixtures/ProductionMain", script));
        foreach (var argument in arguments) start.ArgumentList.Add(argument);
        int? observedExitCode = null;
        Exception? failure=null;
        var cleanupFailures=new List<string>();
        try {
            stdout=File.Create(stdoutPath);stderr=File.Create(stderrPath);
            process=Process.Start(start) ?? throw new IOException("Python DATA process did not start.");
            exit=process.WaitForExitAsync();
            output=process.StandardOutput.BaseStream.CopyToAsync(stdout);
            error=process.StandardError.BaseStream.CopyToAsync(stderr);
            await Task.WhenAll(exit,output,error).WaitAsync(TimeSpan.FromSeconds(20));
        }
        catch(Exception exception) { failure=exception; }
        finally
        {
            using var cleanup = new CancellationTokenSource(TimeSpan.FromSeconds(5));
            async Task Observe(Task task, string step)
            {
                try { await task.WaitAsync(cleanup.Token); }
                catch(Exception exception) { cleanupFailures.Add(step + ": " + exception); }
            }
            try { if(process is not null && !process.HasExited) process.Kill(entireProcessTree:true); }
            catch(Exception exception) { cleanupFailures.Add("kill: " + exception); }
            await Observe(exit, "exit");
            await Observe(output, "stdout-copy");
            await Observe(error, "stderr-copy");
            async Task SettleWriter(FileStream? writer, Task pump, string name)
            {
                if(writer is null)return;
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
                    ExitTaskStatus=exit.Status.ToString(), Exited=process?.HasExited,
                    ExitCode=process is { HasExited:true }?(int?)process.ExitCode:null,
                    StdoutTaskStatus=output.Status.ToString(), StderrTaskStatus=error.Status.ToString()
                }), cleanup.Token).WaitAsync(cleanup.Token);
            }
            catch(Exception exception) { cleanupFailures.Add("outcome: " + exception); }
            if (process is not null && exit.IsCompletedSuccessfully && process.HasExited) observedExitCode = process.ExitCode;
            // Process streams must not be disposed while either original pump remains active.
            if (process is not null && exit.IsCompleted && output.IsCompleted && error.IsCompleted)
            {
                try { process.Dispose(); }
                catch(Exception exception) { cleanupFailures.Add("process-dispose: " + exception); }
            }

        }
        Assert.True(failure is null && cleanupFailures.Count==0 && observedExitCode==0,
            $"Relay source contracts failed. Evidence: {root}\n{failure}\n{string.Join("\n",cleanupFailures)}");
        // Exit zero is insufficient: a missing/empty suite must not qualify.
        var raw = File.ReadAllText(stderrPath);
        var summaries = Regex.Matches(raw, @"(?m)^Ran (\d+) tests? in [^\r\n]+$");
        Assert.Single(summaries.Cast<Match>());
        Assert.Equal(expectedMethods, int.Parse(summaries[0].Groups[1].Value));
        Assert.Matches(@"(?m)^OK\r?$", raw);
        Assert.DoesNotContain("skipped=", raw, StringComparison.Ordinal);
    }
}
