using System.Diagnostics;
using System.Text.Json;
using Xunit;
namespace BookOfEternityClient.Tests;
public sealed class MortalRejectedCleanupTests
{
    [Fact]
    public async Task RejectedRollbackCleanupSourceContracts()
    {
        Assert.True(OperatingSystem.IsLinux());
        var root=Path.Combine(TestDiagnosticPaths.Root,"mortal-rejected-cleanup",Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(root);
        var stdoutPath=Path.Combine(root,"stdout.raw");
        var stderrPath=Path.Combine(root,"stderr.raw");
        await using var stdout=File.Create(stdoutPath);
        await using var stderr=File.Create(stderrPath);
        var start=new ProcessStartInfo("/usr/bin/python3") {
            UseShellExecute=false,RedirectStandardOutput=true,RedirectStandardError=true,
            WorkingDirectory=TestRepoPaths.RepoRoot
        };
        start.ArgumentList.Add(Path.Combine(TestRepoPaths.RepoRoot,
            "tests/fixtures/ProductionMain/test-mortal-rejected-cleanup.py"));
        using var process=Process.Start(start) ?? throw new IOException("Python contract process did not start.");
        var output=process.StandardOutput.BaseStream.CopyToAsync(stdout);
        var error=process.StandardError.BaseStream.CopyToAsync(stderr);
        var exit=process.WaitForExitAsync();
        Exception? failure=null;
        var cleanupFailures=new List<string>();
        try { await Task.WhenAll(exit,output,error).WaitAsync(TimeSpan.FromSeconds(20)); }
        catch(Exception exception) { failure=exception; }
        finally
        {
            try { if(!process.HasExited) process.Kill(entireProcessTree:true); }
            catch(Exception exception) { cleanupFailures.Add(exception.ToString()); }
            try { await Task.WhenAll(exit,output,error).WaitAsync(TimeSpan.FromSeconds(5)); }
            catch(Exception exception) { cleanupFailures.Add(exception.ToString()); }
            await stdout.FlushAsync();
            await stderr.FlushAsync();
            await File.WriteAllTextAsync(Path.Combine(root,"outcome.json"),JsonSerializer.Serialize(new {
                Failure=failure?.ToString(), CleanupFailures=cleanupFailures,
                Exited=process.HasExited, ExitCode=process.HasExited?(int?)process.ExitCode:null,
                StdoutComplete=output.IsCompletedSuccessfully, StderrComplete=error.IsCompletedSuccessfully
            }));
        }
        Assert.True(failure is null && cleanupFailures.Count==0 && process.HasExited && process.ExitCode==0,
            $"Relay source contracts failed. Evidence: {root}\n{failure}\n{string.Join("\n",cleanupFailures)}");
    }
}
