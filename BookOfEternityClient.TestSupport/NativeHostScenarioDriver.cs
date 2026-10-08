using System.Diagnostics;
using System.Text.Json;
using System.Reflection;
using System.IO.Pipes;
using BookOfEternityClient.Services.GmWorkers;
using Microsoft.Win32.SafeHandles;

namespace BookOfEternityClient.Tests;

// Executed only beneath the independent C guardian. The production launcher remains
// the actual Process.Start caller, so SO_PEERCRED is checked against the right Process.
internal static class NativeHostScenarioDriver
{
    internal static async Task<int> Main(string[] args)
    {
        if(args.Length==3 && args[0].StartsWith("terminal-systemd-",StringComparison.Ordinal))return await SystemdControlledScenarioDriver.RunAsync(args[0],args[1],args[2]);
        if(args.Length==3 && args[0].StartsWith("production-main-",StringComparison.Ordinal))return await OwnedTerminalScenarioDriver.RunAsync(args[0],args[1],args[2]);
        if(args.Length==3 && args[0].StartsWith("terminal-main-crash-",StringComparison.Ordinal))return await MainRunCrashScenarioDriver.RunAsync(args[0],args[1],args[2]);
        if(args.Length==3 && args[0].StartsWith("terminal-main-operation-",StringComparison.Ordinal))return await MainOperationScenarioDriver.RunAsync(args[0],args[1],args[2]);
        if(args.Length==3 && args[0].StartsWith("terminal-main-",StringComparison.Ordinal))return await MainRunFenceScenarioDriver.RunAsync(args[0],args[1],args[2]);
        if (args.Length == 3 && args[0].StartsWith("terminal-", StringComparison.Ordinal))
            return await OwnedTerminalScenarioDriver.RunAsync(args[0], args[1], args[2]);
        if (args.Length == 3 && args[0].StartsWith("restart-", StringComparison.Ordinal))
            return await NativePoolScenarioDriver.RunRestart(args[0], args[1], args[2]);
        if (args.Length == 3 && args[0].StartsWith("ledger-", StringComparison.Ordinal))
            return await WorkerRunLedgerScenarioDriver.Run(args[0], args[1], args[2]);
        if (args.Length == 3 && args[0].StartsWith("pool-", StringComparison.Ordinal))
            return await NativePoolScenarioDriver.Run(args[0], args[1], args[2]);
        if (args.Length == 3 && args[0] is "bootstrap-close" or "bootstrap-wrong-ack")
            return await NativeBootstrapScenario.Run(args[0], args[1], args[2]);
        if (args.Length != 3 || args[0] is not ("neutral-ready" or "constructor-path" or "helper-loss-closed-output" or
            "foreign-control" or "foreign-status" or "cancel-before-ready" or "exec-failure" or "owner-eof" or "status-loss" or "release-denied" or "published-ready" or "pre-canceled" or "output-audit")) return 64;
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
        host.StartInfo.ArgumentList.Add(mode switch
        {
            "helper-loss-closed-output" => "--expire-closed-output-exec",
            "cancel-before-ready" => "--expire-delayed-exec",
            "output-audit" => "--expire-audit-exec",
            _ => "--expire-exec"
        });
        if (mode == "cancel-before-ready") host.StartInfo.ArgumentList.Add(Path.Combine(output, "host-held"));
        if (mode == "output-audit") host.StartInfo.ArgumentList.Add(Path.Combine(output, "fd-audit.json"));
        host.StartInfo.ArgumentList.Add(executable);
        foreach (var arg in hostArguments) host.StartInfo.ArgumentList.Add(arg);
        if (mode == "exec-failure") host.StartInfo.FileName = Path.Combine(output, "absent-neutral-host");
        GmWorkerOwnedLaunch? owner = null;
        GmWorkerStopEvidence? stop = null;
        var ready = false; string? failure = null; int? hostPid = null, supervisorPid = null;
        var originalTemp = Environment.GetEnvironmentVariable("TMPDIR");
        var longTemp = Path.Combine(output, new string('t', 100));
        var disposeAllowed = false; var hostAliveBeforeDispose = false; var pidfdClosedAfterDispose = false;
        var releaseDenied = false; var canceled = false; GmWorkerStopEvidence? laterStop = null;
        string? hostStdout = null, hostStderr = null; JsonElement? fdAudit = null;
        NamedPipeClientStream? foreign = null;
        if (mode == "constructor-path")
        {
            Directory.CreateDirectory(longTemp);
            Environment.SetEnvironmentVariable("TMPDIR", longTemp); // This isolated scenario process only.
        }
        try
        {
            using var deadline = new CancellationTokenSource(TimeSpan.FromSeconds(5));
            if (mode == "pre-canceled") deadline.Cancel();
            if (mode is "foreign-control" or "foreign-status")
            {
                foreign = new NamedPipeClientStream(".", hostArguments[mode == "foreign-control" ? ^3 : ^2],
                    mode == "foreign-control" ? PipeDirection.In : PipeDirection.Out, PipeOptions.Asynchronous | PipeOptions.CurrentUserOnly);
                await foreign.ConnectAsync(deadline.Token);
            }
            var launcher = new ObserveReturnedOwner(new GmWorkerNativeLineageLauncher(mode == "published-ready" ? null : args[1]));
            var preparation = host.PrepareOwnedAsync(launcher,
                GmWorkerBackendRequest.NativeLineage, GmWorkerRequiredCapability.NeutralHost, deadline.Token);
            if (mode == "cancel-before-ready")
            {
                // Exact lifecycle anchor: actual native StartAsync has completed
                // binding/ACK/Started; shared named-channel Ready is still blocked.
                _ = await launcher.Started.Task.WaitAsync(deadline.Token);
                while (!File.Exists(Path.Combine(output, "host-held"))) await Task.Delay(10, deadline.Token);
                deadline.Cancel();
            }
            owner = await preparation;
            ready = true; hostPid = owner.HostProcessId; supervisorPid = owner.SupervisorProcessId;
            if (mode == "release-denied")
            {
                try { await host.ReleaseAsync(deadline.Token); }
                catch (InvalidOperationException) { releaseDenied = true; }
            }
            if (mode is "owner-eof" or "status-loss")
            {
                var original = (Process)typeof(GmWorkerNativeLineageLaunch).GetField("_supervisor", BindingFlags.Instance | BindingFlags.NonPublic)!.GetValue(owner)!;
                if (mode == "owner-eof") original.StandardInput.Close();
                else original.StandardOutput.Close();
            }
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
        catch (GmWorkerOwnedLaunchException ex)
        {
            owner = ex.Owner; failure = ex.InnerException?.Message ?? ex.Message;
            canceled = ex.InnerException is OperationCanceledException;
        }
        catch (Exception ex) { failure = ex.Message; canceled = ex is OperationCanceledException; }
        finally
        {
            Environment.SetEnvironmentVariable("TMPDIR", originalTemp);
            await host.DisposeAsync(); // Named-channel owner-close, not helper control EOF.
            foreign?.Dispose();
            if (owner != null && mode != "helper-loss-closed-output")
            {
                stop = await owner.StopAndObserveAsync();
                if (stop.State == GmWorkerStopState.StoppedWithinScope) await owner.SettleOutputsAsync();
                if (mode == "output-audit")
                {
                    hostStdout = await ((GmWorkerNativeLineageLaunch)owner).HostStandardOutput;
                    hostStderr = await ((GmWorkerNativeLineageLaunch)owner).HostStandardError;
                    using var audit = JsonDocument.Parse(await File.ReadAllTextAsync(Path.Combine(output, "fd-audit.json")));
                    fdAudit = audit.RootElement.Clone();
                }
                if (mode is "owner-eof" or "status-loss" or "exec-failure") laterStop = await owner.StopAndObserveAsync();
                try { await owner.DisposeAsync(); disposeAllowed = true; }
                catch (InvalidOperationException) when (stop.State == GmWorkerStopState.Uncertain) { }
                var pidfd = (SafeFileHandle?)typeof(GmWorkerNativeLineageLaunch).GetField("_hostPidfd", BindingFlags.Instance | BindingFlags.NonPublic)!.GetValue(owner);
                pidfdClosedAfterDispose = pidfd?.IsClosed ?? false;
            }
            await File.WriteAllTextAsync(Path.Combine(output, "scenario.json"), JsonSerializer.Serialize(new
            {
                ready, failure, hostPid, supervisorPid, stop, workerReleased = File.Exists(marker),
                bootstrapDirectoriesAfter = mode == "constructor-path" ? Directory.GetDirectories(longTemp).Length : 0,
                disposeAllowed, hostAliveBeforeDispose, pidfdClosedAfterDispose, releaseDenied, canceled, laterStop,
                hostStdout, hostStderr, fdAudit
            }));
        }
        return 0; // Assertions live outside the independently reaped scenario driver.
    }

    private sealed class ObserveReturnedOwner(IGmWorkerOwnedLauncher actual) : IGmWorkerOwnedLauncher
    {
        internal TaskCompletionSource<GmWorkerOwnedLaunch> Started { get; } = new(TaskCreationOptions.RunContinuationsAsynchronously);
        public async Task<GmWorkerOwnedLaunch> StartAsync(GmWorkerProcessHostLaunch host, GmWorkerBackendSelection selection, CancellationToken token)
        {
            try { var owner = await actual.StartAsync(host, selection, token); Started.TrySetResult(owner); return owner; }
            catch (Exception ex) { Started.TrySetException(ex); throw; }
        }
    }
}
