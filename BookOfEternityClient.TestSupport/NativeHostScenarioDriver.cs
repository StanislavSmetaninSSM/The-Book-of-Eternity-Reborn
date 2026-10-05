using System.Diagnostics;
using System.Text.Json;
using System.Reflection;
using BookOfEternityClient.Services.GmWorkers;
using Microsoft.Win32.SafeHandles;

namespace BookOfEternityClient.Tests;

// Executed only beneath the independent C guardian. The production launcher remains
// the actual Process.Start caller, so SO_PEERCRED is checked against the right Process.
internal static class NativeHostScenarioDriver
{
    internal static async Task<int> Main(string[] args)
    {
        if (args.Length != 3 || args[0] is not ("neutral-ready" or "constructor-path" or "helper-loss-closed-output")) return 64;
        var mode = args[0];
        var output = args[2];
        var marker = Path.Combine(output, "worker-released");
        var fixture = Path.Combine(args[1], "host-guardian");
        var worker = new ProcessStartInfo(fixture) { WorkingDirectory = output, UseShellExecute = false };
        worker.ArgumentList.Add("--worker-marker"); worker.ArgumentList.Add(marker);
        await using var host = GmWorkerProcessHostLaunch.Create(worker, output);
        // Secondary finite same-PID exec wrapper; the outer guardian owns cleanup.
        var executable = host.StartInfo.FileName;
        var hostArguments = host.StartInfo.ArgumentList.ToArray();
        host.StartInfo.FileName = fixture;
        host.StartInfo.ArgumentList.Clear();
        host.StartInfo.ArgumentList.Add(mode == "helper-loss-closed-output" ? "--expire-closed-output-exec" : "--expire-exec");
        host.StartInfo.ArgumentList.Add(executable);
        foreach (var arg in hostArguments) host.StartInfo.ArgumentList.Add(arg);
        GmWorkerOwnedLaunch? owner = null;
        GmWorkerStopEvidence? stop = null;
        var ready = false; string? failure = null; int? hostPid = null, supervisorPid = null;
        var originalTemp = Environment.GetEnvironmentVariable("TMPDIR");
        var longTemp = Path.Combine(output, new string('t', 100));
        var disposeAllowed = false; var hostAliveBeforeDispose = false; var pidfdClosedAfterDispose = false;
        if (mode == "constructor-path")
        {
            Directory.CreateDirectory(longTemp);
            Environment.SetEnvironmentVariable("TMPDIR", longTemp); // This isolated scenario process only.
        }
        try
        {
            using var deadline = new CancellationTokenSource(TimeSpan.FromSeconds(5));
            owner = await host.PrepareOwnedAsync(new GmWorkerNativeLineageLauncher(args[1]),
                GmWorkerBackendRequest.NativeLineage, GmWorkerRequiredCapability.NeutralHost, deadline.Token);
            ready = true; hostPid = owner.HostProcessId; supervisorPid = owner.SupervisorProcessId;
            if (mode == "helper-loss-closed-output")
            {
                var original = (Process)typeof(GmWorkerNativeLineageLaunch).GetField("_supervisor", BindingFlags.Instance | BindingFlags.NonPublic)!.GetValue(owner)!;
                original.Kill(); // Original Process only; independent guardian retains the host.
                await original.WaitForExitAsync();
                stop = await owner.StopAndObserveAsync();
                var pidfd = (SafeFileHandle)typeof(GmWorkerNativeLineageLaunch).GetField("_hostPidfd", BindingFlags.Instance | BindingFlags.NonPublic)!.GetValue(owner)!;
                hostAliveBeforeDispose = !GmWorkerHostIdentity.IsReadable(pidfd);
                try { await owner.DisposeAsync(); disposeAllowed = true; }
                catch (InvalidOperationException) { }
                pidfdClosedAfterDispose = pidfd.IsClosed;
            }
        }
        catch (GmWorkerOwnedLaunchException ex) { owner = ex.Owner; failure = ex.InnerException?.Message ?? ex.Message; }
        catch (Exception ex) { failure = ex.Message; }
        finally
        {
            Environment.SetEnvironmentVariable("TMPDIR", originalTemp);
            await host.DisposeAsync(); // Named-channel owner-close, not helper control EOF.
            if (owner != null && mode != "helper-loss-closed-output")
            {
                stop = await owner.StopAndObserveAsync();
                await owner.DisposeAsync();
            }
            await File.WriteAllTextAsync(Path.Combine(output, "scenario.json"), JsonSerializer.Serialize(new
            {
                ready, failure, hostPid, supervisorPid, stop, workerReleased = File.Exists(marker),
                bootstrapDirectoriesAfter = mode == "constructor-path" ? Directory.GetDirectories(longTemp).Length : 0,
                disposeAllowed, hostAliveBeforeDispose, pidfdClosedAfterDispose
            }));
        }
        return 0; // Assertions live outside the independently reaped scenario driver.
    }
}
