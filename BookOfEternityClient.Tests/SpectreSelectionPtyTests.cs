using System.Diagnostics;
using System.Text.Json;
using Xunit;
namespace BookOfEternityClient.Tests;

public sealed class SpectreSelectionPtyTests
{
    [Theory]
    [InlineData("pty")]
    [InlineData("pipe-dumb")]
    [InlineData("pipe-xterm")]
    public async Task ActualDefaultPromptUsesOwnedClientConsole(string mode)
        => await RunModeAsync(mode);

    [Fact]
    public Task ActualFiveRowInventoryUsesFiniteSelectorAndReturnsLockedTarget() => RunModeAsync("pipe-inventory");

    [Fact]
    public Task ActualInventoryFailureClosesBackAndPromptWithoutCtrlC() => RunModeAsync("pipe-inventory-cleanup");

    [Fact]
    public Task ActualLongLocationLoadPipeFrameIsUnwrappedAndReturnsBack() => RunModeAsync("pipe-load");

    [Fact]
    public async Task ActualExact78ColumnLoadCapturesGeometryBeforeAdmission()
    {
        var root=Path.Combine(TestDiagnosticPaths.Root,"spectre-geometry-data",Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(root);
        await RunAsync("/usr/bin/python3",
            [Path.Combine(TestRepoPaths.RepoRoot,"tests/fixtures/SelectionPrompt/test_geometry.py")],
            root,"data",10);
        await RunModeAsync("pipe-load-geometry");
    }

    [Theory]
    [InlineData("pty-load-100")]
    [InlineData("pty-load-80")]
    public Task ActualSamePtyLoadGeometryHasExplicitOutcome(string mode) => RunModeAsync(mode);

    [Fact]
    public async Task ActualLocalStorageSaveAndBackOnlyLoadUseRealUi()
    {
        var root = Path.Combine(TestDiagnosticPaths.Root, "fullroute-input-data", Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(root);
        await RunAsync("/usr/bin/python3",
            [Path.Combine(TestRepoPaths.RepoRoot, "tests/fixtures/SelectionPrompt/test_fullroute_inputs.py")],
            root, "data-nine", 10);
        await RunModeAsync("pty-fullroute");
    }

    /// <summary>Reuses the owned inert renderer process and its exact EOF/guardian cleanup for one explicit console mode.</summary>
    private static async Task RunModeAsync(string mode)
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
        // This existing fixture-only selector changes the budget range, not authority.
        // The inert full route uses 45s work / 55s guardian / 60s observer, with no GM or live turn.
        string[] guardianBudgetSelector = mode == "pty-fullroute" ? ["--live-turn"] : [];
        await RunAsync(Path.Combine(package,"host-guardian"),
            [.. guardianBudgetSelector, Path.Combine(root,"guardian.json"),mode == "pty-fullroute" ? "55000" : "20000","/usr/bin/python3",
             Path.Combine(TestRepoPaths.RepoRoot,"tests/fixtures/SelectionPrompt/smoke.py"),
             Path.Combine(root,"peer"),helper,mode], root, "guardian", mode == "pty-fullroute" ? 60 : 25);
        using var guardian = JsonDocument.Parse(File.ReadAllBytes(Path.Combine(root,"guardian.json")));
        var g = guardian.RootElement;
        Assert.True(g.GetProperty("echild").GetBoolean());
        Assert.Equal(0,g.GetProperty("driverExitCode").GetInt32());
        Assert.Equal(0,g.GetProperty("emergencySignals").GetInt32());
        Assert.Equal(0,g.GetProperty("failures").GetInt32());
        Assert.False(g.GetProperty("deadline").GetBoolean());
        using var result=JsonDocument.Parse(File.ReadAllBytes(Path.Combine(root,"peer/result.json")));
        Assert.True(result.RootElement.GetProperty("Success").GetBoolean(), result.RootElement.ToString());
        if (mode == "pty-load-80")
        {
            Assert.True(result.RootElement.GetProperty("ExpectedWrappedRefusal").GetBoolean());
            Assert.False(result.RootElement.GetProperty("NormalExitQualified").GetBoolean());
            Assert.True(result.RootElement.GetProperty("Joined").GetBoolean());
            Assert.True(result.RootElement.GetProperty("CleanupDrained").GetBoolean());
            return; // Negative cleanup is not a normal-exit/termios assertion.
        }
        Assert.True(result.RootElement.GetProperty("EOF").GetBoolean());
        Assert.True(result.RootElement.GetProperty("TermiosRestored").GetBoolean());
    }

    [Fact]
    public async Task ScreenProjectionRefusesUnsupportedOrIncompleteTerminalState()
    {
        var root=Path.Combine(TestDiagnosticPaths.Root,"spectre-screen",Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(root);
        await RunAsync("/usr/bin/python3",
            [Path.Combine(TestRepoPaths.RepoRoot,"tests/fixtures/SelectionPrompt/test_screen.py")],
            root,"parser",10);
    }

    private static async Task RunAsync(string executable, string[] args, string root, string label, int seconds)
    {
        FileStream? stdout=null, stderr=null;
        Process? process=null;
        var start=new ProcessStartInfo(executable) {
            UseShellExecute=false,RedirectStandardOutput=true,RedirectStandardError=true,
            WorkingDirectory=TestRepoPaths.RepoRoot
        };
        foreach(var arg in args) start.ArgumentList.Add(arg);
        try
        {
            stdout=File.Create(Path.Combine(root,label+".stdout.raw"));
            stderr=File.Create(Path.Combine(root,label+".stderr.raw"));
            process=Process.Start(start) ?? throw new IOException("Selection smoke process did not start.");
        }
        catch (Exception startupFailure)
        {
            // No pump exists yet. Each successfully created file is independently released.
            var startupCleanup=new List<string>();
            try { stdout?.Dispose(); } catch(Exception e) { startupCleanup.Add(e.ToString()); }
            try { stderr?.Dispose(); } catch(Exception e) { startupCleanup.Add(e.ToString()); }
            File.WriteAllText(Path.Combine(root,label+".startup-failure.json"),JsonSerializer.Serialize(new {
                Failure=startupFailure.ToString(),CleanupFailures=startupCleanup
            }));
            throw;
        }
        Task StartTask(Func<Task> operation)
        {
            try { return operation(); }
            catch(Exception error) { return Task.FromException(error); }
        }
        var output=StartTask(() => process.StandardOutput.BaseStream.CopyToAsync(stdout));
        var error=StartTask(() => process.StandardError.BaseStream.CopyToAsync(stderr));
        var exit=StartTask(() => process.WaitForExitAsync());
        var retainedTasks=new List<Task> { exit, output, error };
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
                retainedTasks.Add(task);
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
            if (retainedTasks.All(task => task.IsCompleted))
            {
                try { process.Dispose(); }
                catch(Exception exception) { cleanupFailures.Add("process-dispose: " + exception); }
            }
            else
            {
                // This continuation retains exact original resources until all their I/O settles.
                // The test remains FAILED/incomplete; late cleanup is never reported as PASS.
                cleanupFailures.Add("Late cleanup retained by original-task continuation");
                _ = Task.WhenAll(retainedTasks).ContinueWith(async completed => {
                    _ = completed.Exception;
                    try { await stdout.DisposeAsync(); } catch { }
                    try { await stderr.DisposeAsync(); } catch { }
                    try { process.Dispose(); } catch { }
                }, CancellationToken.None, TaskContinuationOptions.ExecuteSynchronously,
                    TaskScheduler.Default).Unwrap();
            }

        }
        Assert.True(failure is null && cleanupFailures.Count==0 && observedExitCode==0,
            $"Selection smoke contracts failed. Evidence: {root}\n{failure}\n{string.Join("\n",cleanupFailures)}");
    }
}
