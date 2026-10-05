using System.Diagnostics;
using System.IO.Pipes;
using System.Net.Sockets;
using System.Text.Json;
using BookOfEternityClient.Services.GmWorkers;

namespace BookOfEternityClient.Tests;

// Real v2 protocol negative fixture, always beneath the independent guardian.
// It never acknowledges the bound root and never releases a worker.
internal static class NativeBootstrapScenario
{
    internal static async Task<int> Run(string mode, string package, string output)
    {
        var run = Guid.NewGuid().ToString("N");
        var root = Path.Combine(Path.GetTempPath(), "boe-bootstrap-" + run);
        Directory.CreateDirectory(root, UnixFileMode.UserRead | UnixFileMode.UserWrite | UnixFileMode.UserExecute);
        using var listener = new Socket(AddressFamily.Unix, SocketType.Seqpacket, ProtocolType.Unspecified);
        var endpoint = Path.Combine(root, "s");
        listener.Bind(new UnixDomainSocketEndPoint(endpoint)); listener.Listen(1);
        using var stdout = new AnonymousPipeServerStream(PipeDirection.In, HandleInheritability.None);
        using var stderr = new AnonymousPipeServerStream(PipeDirection.In, HandleInheritability.None);
        using var outReader = new StreamReader(stdout); using var errReader = new StreamReader(stderr);
        var outTask = outReader.ReadToEndAsync(); var errTask = errReader.ReadToEndAsync();
        var marker = Path.Combine(output, "gated-root-executed");
        var start = new ProcessStartInfo(GmWorkerNativePackage.Validate(package))
        { UseShellExecute = false, RedirectStandardInput = true, RedirectStandardOutput = true, RedirectStandardError = true, WorkingDirectory = output };
        foreach (var arg in new[] { run, "100", "1500", "--host-v2", endpoint, Path.Combine(package, "host-guardian"), "--worker-marker", marker }) start.ArgumentList.Add(arg);
        using var helper = Process.Start(start)!;
        var exit = helper.WaitForExitAsync(); var errors = helper.StandardError.ReadToEndAsync();
        var ready = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        var statuses = ReadStatuses(helper.StandardOutput, ready, Path.Combine(output, "bootstrap-status.jsonl"));
        Socket? connection = null; string? failure = null; var bound = false;
        try
        {
            using var deadline = new CancellationTokenSource(TimeSpan.FromSeconds(5));
            connection = await listener.AcceptAsync(deadline.Token);
            GmWorkerProcessHostPeerIdentity.Validate(connection.SafeHandle, helper.Id,
                GmWorkerProcessHostPeerIdentity.CaptureEffectiveUserId(), "fixture-bootstrap");
            await GmWorkerNativeDescriptors.ReceiveAsync(connection, "H2:" + run, 0, deadline.Token);
            GmWorkerNativeDescriptors.Send(connection, "P2:" + run, stdout.ClientSafePipeHandle, stderr.ClientSafePipeHandle);
            await ready.Task.WaitAsync(deadline.Token);
            stdout.DisposeLocalCopyOfClientHandle(); stderr.DisposeLocalCopyOfClientHandle();
            await helper.StandardInput.WriteAsync("L"); await helper.StandardInput.FlushAsync();
            var descriptors = await GmWorkerNativeDescriptors.ReceiveAsync(connection, "B2:" + run, 1, deadline.Token);
            using var pidfd = descriptors.Single();
            GmWorkerHostIdentity.FromTransferredPidfd(helper, pidfd, () => true).EnsureLive();
            bound = true;
            if (mode == "bootstrap-wrong-ack") GmWorkerNativeDescriptors.Send(connection, "A2:wrong-run");
            connection.Dispose(); connection = null;
            await exit.WaitAsync(deadline.Token);
        }
        catch (Exception ex) { failure = ex.Message; }
        finally
        {
            connection?.Dispose(); helper.StandardInput.Close();
            stdout.DisposeLocalCopyOfClientHandle(); stderr.DisposeLocalCopyOfClientHandle();
            // Observation failure does not abandon the actual helper wait. The
            // outer guardian owns the entire finite fixture if this driver fails.
            await exit;
            var frames = await statuses;
            await File.WriteAllTextAsync(Path.Combine(output, "helper-stderr.log"), await errors);
            await File.WriteAllTextAsync(Path.Combine(output, "scenario.json"), JsonSerializer.Serialize(new
            { bound, failure, rootExecuted = File.Exists(marker), helperExitCode = helper.ExitCode,
              frames, hostStdout = await outTask, hostStderr = await errTask, workerReleased = false }));
            Directory.Delete(root, recursive: true);
        }
        return 0;
    }

    private static async Task<List<JsonElement>> ReadStatuses(StreamReader input, TaskCompletionSource ready, string path)
    {
        var frames = new List<JsonElement>();
        while (await input.ReadLineAsync() is { } line)
        {
            if (line.Length > 1024 || frames.Count >= 16) throw new InvalidDataException("Fixture status bounds exceeded.");
            using var frame = JsonDocument.Parse(line); frames.Add(frame.RootElement.Clone());
            await File.AppendAllTextAsync(path, line + "\n");
            if (frame.RootElement.GetProperty("state").GetString() == "Ready") ready.TrySetResult();
        }
        return frames;
    }
}
