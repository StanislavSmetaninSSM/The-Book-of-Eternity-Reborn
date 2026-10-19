using System.Diagnostics;
using System.Globalization;
using System.Reflection;
using System.Text.Json;
using BookOfEternityClient.Services.GmWorkers;

namespace BookOfEternityClient.Tests;

/// <summary>
/// Supplies finite children and retained-owner scenarios beneath the existing Linux guardian.
/// </summary>
internal static class NativeFinitePipeScenarioDriver
{
    /// <summary>
    /// Executes a finite child or its owning driver with a prebuilt native package.
    /// </summary>
    /// <param name="mode">
    /// A driver scenario or child role prefixed with child-.
    /// </param>
    /// <param name="package">
    /// The explicitly prepared package directory; child roles receive their literal argument here.
    /// </param>
    /// <param name="own">
    /// The unique scenario evidence directory.
    /// </param>
    /// <returns>
    /// The child result, or zero after the driver records its actual observations for outer assertions.
    /// </returns>
    internal static async Task<int> RunAsync(string mode, string package, string own)
    {
        if (mode.StartsWith("child-", StringComparison.Ordinal))
        {
            var childMode = mode[6..];
            await File.WriteAllTextAsync(Path.Combine(own, "child.json"), JsonSerializer.Serialize(new
            {
                pid = Environment.ProcessId, birth = ReadBirth(Environment.ProcessId),
                tmp = Environment.GetEnvironmentVariable("TMPDIR"), argument = package
            }));
            if (childMode == "output")
            {
                await Task.WhenAll(Console.Out.WriteAsync(new string('O', 131072)), Console.Error.WriteAsync(new string('E', 131072)));
            }
            if (childMode is "timeout" or "control-loss" or "status-loss") await Task.Delay(TimeSpan.FromSeconds(8));
            return childMode == "seventy-three" ? 73 : 0;
        }

        Directory.CreateDirectory(Path.Combine(own, "tmp with spaces"));
        var dotnet = Path.Combine(Environment.GetEnvironmentVariable("DOTNET_ROOT")!, "dotnet");
        var start = new ProcessStartInfo(dotnet) { UseShellExecute = false, WorkingDirectory = own };
        foreach (var arg in new[] { typeof(NativeHostScenarioDriver).Assembly.Location, "finite-pipe-child-" + mode, "literal argument with spaces", own })
            start.ArgumentList.Add(arg);
        start.Environment["TMPDIR"] = Path.Combine(own, "tmp with spaces");
        if (mode == "exec-failure") start.FileName = Path.Combine(own, "missing executable");
        if (mode == "fast") { start.FileName = "/bin/true"; start.ArgumentList.Clear(); }
        if (mode == "start-failure") start.WorkingDirectory = Path.Combine(own, "absent directory");
        NativeLineageOwner? owner = null, heldOwner = null, exceptionOwner = null;
        GmWorkerStopEvidence? stop = null;
        string? failure = null, stdout = null, stderr = null, bootstrap = null;
        int? rootPid = null, supervisorPid = null;
        ulong? heldBirth = null;
        var heldObserved = false; var heldBeforeExec = false; var disposed = false; var eof = false;
        try
        {
            using var deadline = new CancellationTokenSource(TimeSpan.FromSeconds(5));
            owner = await NativeLineageOwner.StartFinitePipeAsync(start, package, (original, pid) =>
            {
                heldOwner = original; bootstrap = original.BootstrapDirectory;
                rootPid = pid; supervisorPid = original.SupervisorProcessId;
                heldBirth = ReadBirth(pid); heldObserved = true;
                heldBeforeExec = !File.Exists(Path.Combine(own, "child.json"));
                if (mode == "malformed-terminal")
                    original.SetSyntheticObservationFault(new(GmWorkerNativeObservationFaultKind.MalformedTerminal));
                if (mode == "held-error") throw new InvalidOperationException("Held observer failed.");
            }, deadline.Token);
            if (mode is "control-loss" or "status-loss")
            {
                var supervisor = (Process)typeof(NativeLineageOwner).GetField("_supervisor", BindingFlags.Instance | BindingFlags.NonPublic)!.GetValue(owner)!;
                if (mode == "control-loss") supervisor.StandardInput.Close();
                else supervisor.StandardOutput.Close();
            }
            await owner.HostExited.WaitAsync(TimeSpan.FromMilliseconds(mode == "timeout" ? 100 : 10000));
            if (mode != "natural-stop-race") await owner.SupervisorExited.WaitAsync(TimeSpan.FromSeconds(5));
        }
        catch (GmWorkerOwnedLaunchException error)
        {
            owner = (NativeLineageOwner)error.Owner;
            exceptionOwner = owner;
            failure = error.InnerException?.GetType().Name ?? error.GetType().Name;
        }
        catch (Exception error) { failure = error.GetType().Name; }
        finally
        {
            if (owner != null)
            {
                bootstrap ??= owner.BootstrapDirectory;
                stop = await owner.StopAndObserveAsync();
                try
                {
                    if (stop.State == GmWorkerStopState.StoppedWithinScope)
                    {
                        var outputs = await owner.SettleOutputsAsync();
                        stdout = outputs.StandardOutput; stderr = outputs.StandardError;
                    }
                    else
                    {
                        await Task.WhenAll(owner.HostStandardOutput, owner.HostStandardError).WaitAsync(TimeSpan.FromSeconds(5));
                        stdout = await owner.HostStandardOutput; stderr = await owner.HostStandardError;
                    }
                    eof = true;
                    await owner.DisposeAsync(); disposed = true;
                }
                catch (InvalidOperationException) when (stop.State == GmWorkerStopState.Uncertain) { }
                catch (Exception error) { failure ??= error.GetType().Name; }
            }
            var birthMatches = false; var environmentMatches = false;
            var childPath = Path.Combine(own, "child.json");
            if (File.Exists(childPath))
            {
                using var child = JsonDocument.Parse(await File.ReadAllTextAsync(childPath));
                birthMatches = child.RootElement.GetProperty("pid").GetInt32() == rootPid && child.RootElement.GetProperty("birth").GetUInt64() == heldBirth;
                environmentMatches = child.RootElement.GetProperty("tmp").GetString() == Path.Combine(own, "tmp with spaces") &&
                    child.RootElement.GetProperty("argument").GetString() == "literal argument with spaces";
            }
            await File.WriteAllTextAsync(Path.Combine(own, "scenario.json"), JsonSerializer.Serialize(new
            {
                failure, heldObserved, heldBeforeExec,
                sameOriginalOwner = owner != null && (ReferenceEquals(owner, heldOwner) || ReferenceEquals(owner, exceptionOwner)),
                rootPid, supervisorPid, heldBirth, birthMatches, environmentMatches, stop, disposed, eof, stdout, stderr,
                bootstrapRemoved = bootstrap != null && !Directory.Exists(bootstrap), uncertainty = owner?.Uncertainty
            }));
            GC.KeepAlive(owner); // Uncertain authority is retained until this isolated driver exits.
        }
        return 0;
    }

    /// <summary>
    /// Reads the live process's Linux start tick independently on each side of exec.
    /// </summary>
    /// <param name="pid">
    /// The bound live root or the child process itself.
    /// </param>
    /// <returns>
    /// Linux stat field22, without interpreting it as a Windows timestamp.
    /// </returns>
    private static ulong ReadBirth(int pid)
    {
        var stat = File.ReadAllText($"/proc/{pid}/stat");
        var fields = stat[(stat.LastIndexOf(')') + 2)..].Split(' ', StringSplitOptions.RemoveEmptyEntries);
        return ulong.Parse(fields[19], CultureInfo.InvariantCulture);
    }
}
