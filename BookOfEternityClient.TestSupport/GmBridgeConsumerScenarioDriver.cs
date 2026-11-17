using System.Diagnostics;
using System.Net.Sockets;
using System.Text.Json;
using BookOfEternityClient.Core;
using BookOfEternityClient.Services.GmRuntime;
using BookOfEternityGMBridge;
using Microsoft.Extensions.Logging.Abstractions;

namespace BookOfEternityClient.Tests;

// Real typed host and shipped independent launcher processes beneath host-guardian.
// Neutral terminal exercises ownership/transport; it is not a live GM game claim.
internal static class GmBridgeConsumerScenarioDriver
{
    internal static async Task<int> RunAsync(string package, string output)
    {
        var result = new Dictionary<string, object?>();
        BridgeHost? host = null;
        Task<int>? running = null;
        GmMainOperationClient? pin = null;
        var exit = 1;
        using var deadline = new CancellationTokenSource(TimeSpan.FromSeconds(25));
        try
        {
            var expectedKind = "named-pipe-v1";
            try { using var probe = new Socket(AddressFamily.Unix, SocketType.Stream, ProtocolType.Unspecified); }
            catch (SocketException error) when (error.NativeErrorCode is 1 or 13) { expectedKind = "linux-fifo-v1"; }
            result["ObservedSocketCapability"] = expectedKind;
            var launch = NeutralTerminalLaunch.Create(package, output);
            var root = Directory.GetParent(launch.Scratch)!.FullName;
            var files = new FileSystemManager(root, NullLogger<FileSystemManager>.Instance);
            files.EnsureDirectoryStructure();
            host = new BridgeHost(launch.Scratch, "consumer-" + Guid.NewGuid().ToString("N"));
            host.ConfigureNeutral(launch);
            running = host.RunAsync();

            string? status = null;
            GmSessionRunRecord? original = null;
            using var startupDeadline = CancellationTokenSource.CreateLinkedTokenSource(deadline.Token);
            startupDeadline.CancelAfter(TimeSpan.FromSeconds(5));
            while (status == null)
            {
                startupDeadline.Token.ThrowIfCancellationRequested();
                if (running.IsCompleted)
                    throw new IOException($"Actual bridge ended before publication (exit {await running}).");
                var candidate = await files.ReadDiagnosticStatusAsync("game_state/control/gm_bridge_status.json");
                var recordBytes = GmSessionRunPersistence.Read(root);
                if (candidate != null && recordBytes != null)
                {
                    using var document = JsonDocument.Parse(candidate);
                    var record = GmSessionRunRecordCodec.Decode(recordBytes);
                    if (record.Disposition == GmSessionRunDisposition.Running &&
                        document.RootElement.TryGetProperty("terminalRunId", out var run) &&
                        run.GetString() == record.Identity.RunId)
                    {
                        original = record;
                        status = candidate;
                        break;
                    }
                }
                await Task.Delay(10, startupDeadline.Token);
            }
            var endpoint = BridgeTransportConnector.DecodeStatus(status);
            if (endpoint.Kind != expectedKind) throw new IOException("Actual host transport disagrees with fresh socket capability.");
            if (!TransportEntryExists(endpoint.Address)) throw new IOException("Published transport entry is absent before use.");
            result["ActualEndpoint"] = endpoint;
            pin = await GmMainOperationClient.OpenAsync(files, deadline.Token, original!.Identity);
            if (pin.Identity != original.Identity) throw new IOException("Actual main grant changed original identity.");
            result["ActualMainGrantAndActivation"] = true;

            using (var diagnostics = await LauncherAsync("diagnostics", launch.Scratch, output, deadline.Token))
            {
                var reply = diagnostics.RootElement;
                if (!reply.GetProperty("ok").GetBoolean() ||
                    reply.GetProperty("status").GetProperty("terminalRunId").GetString() != original.Identity.RunId)
                    throw new IOException("Shipped launcher did not observe the original running terminal.");
                result["IndependentLauncherDiagnostics"] = true;
            }
            await pin.CompleteAsync(MainOperationOutcome.Completed, false);
            if (!pin.CloseObserved || pin.TerminalClose?.Identity != original.Identity)
                throw new IOException("Original main close ACK did not settle on its original stream.");
            result["OriginalCloseObserved"] = true;
            await pin.DisposeAsync();
            pin = null;

            using (var shutdown = await LauncherAsync("shutdown-bridge", launch.Scratch, output, deadline.Token))
            {
                var reply = shutdown.RootElement;
                if (!reply.GetProperty("ok").GetBoolean() || reply.GetProperty("fallbackUsed").GetBoolean() ||
                    reply.GetProperty("status").GetString() != "original-scoped-stopped")
                    throw new IOException("Shipped launcher did not receive original scoped shutdown receipt.");
                result["IndependentLauncherShutdown"] = true;
            }
            if (await running.WaitAsync(deadline.Token) != 0) throw new IOException("Actual bridge returned failure.");
            var stopped = GmSessionRunRecordCodec.Decode(GmSessionRunPersistence.Read(root) ?? throw new IOException("Stopped record missing."));
            if (stopped.Identity != original.Identity || stopped.Disposition != GmSessionRunDisposition.Stopped)
                throw new IOException("Original terminal did not become durably Stopped.");
            if (TransportEntryExists(endpoint.Address)) throw new IOException("Original transport entry survived actual host shutdown.");
            result["OriginalStoppedAndEndpointRemoved"] = true;
            result["Success"] = true;
            exit = 0;
        }
        catch (Exception error) { result["Failure"] = error.ToString(); }
        finally
        {
            if (pin != null)
                try { await pin.DisposeAsync(); }
                catch (Exception error) { result["PinDisposeFailure"] = error.ToString(); exit = 1; }
            if (host != null)
                try { host.Dispose(); }
                catch (Exception error) { result["HostDisposeFailure"] = error.ToString(); exit = 1; }
            if (running != null)
                try { await running.WaitAsync(TimeSpan.FromSeconds(5)); }
                catch (Exception error) { result["HostJoinFailure"] = error.ToString(); exit = 1; }
            if (exit != 0) result["Success"] = false;
            await File.WriteAllTextAsync(Path.Combine(output, "scenario.json"), JsonSerializer.Serialize(result));
        }
        return exit;
    }

    private static bool TransportEntryExists(string address)
        => Directory.EnumerateFileSystemEntries(Path.GetDirectoryName(address)!)
            .Any(entry => string.Equals(entry, address, StringComparison.Ordinal));

    private static async Task<JsonDocument> LauncherAsync(string command, string session, string output, CancellationToken token)
    {
        var directory = new DirectoryInfo(AppContext.BaseDirectory);
        while (directory != null && !File.Exists(Path.Combine(directory.FullName, "AGENTS.md"))) directory = directory.Parent;
        var repo = directory?.FullName ?? throw new IOException("Source-bound launcher missing.");
        var start = new ProcessStartInfo("pwsh")
        {
            UseShellExecute = false, RedirectStandardOutput = true,
            RedirectStandardError = true, RedirectStandardInput = true,
            WorkingDirectory = repo
        };
        foreach (var argument in new[] { "-NoProfile", "-File", Path.Combine(repo, "BookOfEternityClient/Launcher/bookofeternity.ps1"), command, "-SessionPath", session })
            start.ArgumentList.Add(argument);
        using var process = Process.Start(start) ?? throw new IOException("Launcher process not started.");
        process.StandardInput.Close();
        var stdout = process.StandardOutput.ReadToEndAsync();
        var stderr = process.StandardError.ReadToEndAsync();
        try { await process.WaitForExitAsync(token); }
        finally
        {
            if (!process.HasExited) { process.Kill(entireProcessTree: true); await process.WaitForExitAsync(); }
            await File.WriteAllTextAsync(Path.Combine(output, command + ".stdout.log"), await stdout);
            await File.WriteAllTextAsync(Path.Combine(output, command + ".stderr.log"), await stderr);
            await File.WriteAllTextAsync(Path.Combine(output, command + ".exit.txt"), process.ExitCode.ToString());
        }
        if (process.ExitCode != 0) throw new IOException($"Shipped launcher {command} exited {process.ExitCode}: {await stderr}");
        return JsonDocument.Parse(await stdout);
    }
}
