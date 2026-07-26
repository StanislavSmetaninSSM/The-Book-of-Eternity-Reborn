using System.ComponentModel;
using System.Diagnostics;
using System.IO.Pipes;
using System.Runtime.InteropServices;
using System.Security.Cryptography;
using System.Security.Principal;
using System.Text;
using System.Text.Json;
using System.Text.Json.Serialization;
using Microsoft.Win32.SafeHandles;

namespace BookOfEternityClient.Services.GmWorkers;

internal sealed record GmWorkerProcessHostPayload(
    [property: JsonRequired] string FileName,
    [property: JsonRequired] IReadOnlyList<string> Arguments,
    [property: JsonRequired] string WorkingDirectory,
    [property: JsonRequired] Dictionary<string, string?> Environment);

internal enum GmWorkerProcessHostControlKind
{
    Unspecified = 0,
    Release = 1,
    Launch = 2
}

internal enum GmWorkerProcessHostStatusKind
{
    Unspecified = 0,
    Ready = 1,
    Completed = 2,
    Failed = 3,
    OutputDrained = 4
}

internal sealed record GmWorkerProcessHostControlFrame(
    [property: JsonRequired] int SchemaVersion,
    [property: JsonRequired] string LaunchNonce,
    [property: JsonRequired] GmWorkerProcessHostControlKind Kind,
    [property: JsonRequired] GmWorkerProcessHostPayload? Payload = null);

internal sealed record GmWorkerProcessHostStatusFrame(
    [property: JsonRequired] int SchemaVersion,
    [property: JsonRequired] string LaunchNonce,
    [property: JsonRequired] GmWorkerProcessHostStatusKind Kind,
    [property: JsonRequired] int? ExitCode,
    [property: JsonRequired] string? Error);

internal static class GmWorkerProcessHostProtocol
{
    internal const int SchemaVersion = 1;
    internal const int LaunchNonceLength = 32;

    private static readonly JsonSerializerOptions JsonOptions = CreateJsonOptions();

    internal static string SerializeControl(GmWorkerProcessHostControlFrame frame) =>
        JsonSerializer.Serialize(frame, JsonOptions);

    internal static string SerializeStatus(GmWorkerProcessHostStatusFrame frame) =>
        JsonSerializer.Serialize(frame, JsonOptions);

    internal static GmWorkerProcessHostControlFrame ParseControl(
        string json,
        string expectedNonce,
        GmWorkerProcessHostControlKind expectedKind)
    {
        var frame = Deserialize<GmWorkerProcessHostControlFrame>(json, "control");
        ValidateEnvelope(frame.SchemaVersion, frame.LaunchNonce, expectedNonce, "control");
        if (frame.Kind != expectedKind)
        {
            throw new InvalidDataException(
                $"Worker process host control frame kind must be {expectedKind}.");
        }

        switch (frame.Kind)
        {
            case GmWorkerProcessHostControlKind.Launch when frame.Payload == null:
                throw new InvalidDataException(
                    "Worker process host launch control frame requires payload.");
            case GmWorkerProcessHostControlKind.Launch:
                ValidatePayload(frame.Payload!);
                break;
            case GmWorkerProcessHostControlKind.Release when frame.Payload != null:
                throw new InvalidDataException(
                    "Worker process host release control frame must not contain payload.");
        }

        return frame;
    }

    internal static GmWorkerProcessHostStatusFrame ParseStatus(
        string json,
        string expectedNonce,
        GmWorkerProcessHostStatusKind expectedKind)
    {
        var frame = Deserialize<GmWorkerProcessHostStatusFrame>(json, "status");
        ValidateEnvelope(frame.SchemaVersion, frame.LaunchNonce, expectedNonce, "status");
        ValidateStatusPayload(frame);
        if (frame.Kind == GmWorkerProcessHostStatusKind.Failed)
        {
            throw new InvalidOperationException($"Worker process host failed: {frame.Error}");
        }
        if (frame.Kind != expectedKind)
        {
            throw new InvalidDataException(
                $"Worker process host status frame kind must be {expectedKind}.");
        }

        return frame;
    }

    internal static void ValidateLaunchNonce(string? nonce)
    {
        if (nonce == null || nonce.Length != LaunchNonceLength ||
            nonce.Any(ch => !char.IsAsciiHexDigit(ch) || char.IsUpper(ch)))
        {
            throw new InvalidDataException(
                $"Worker process host launch nonce must contain exactly {LaunchNonceLength} lowercase hexadecimal characters.");
        }
    }

    private static T Deserialize<T>(string json, string frameName)
    {
        try
        {
            using var document = JsonDocument.Parse(json);
            ValidateNoDuplicateProperties(document.RootElement, frameName);
            return document.RootElement.Deserialize<T>(JsonOptions) ??
                   throw new InvalidDataException($"Worker process host {frameName} frame is empty.");
        }
        catch (JsonException ex)
        {
            throw new InvalidDataException(
                $"Worker process host {frameName} frame is malformed: {ex.Message}",
                ex);
        }
    }

    private static void ValidateNoDuplicateProperties(JsonElement element, string frameName)
    {
        if (element.ValueKind == JsonValueKind.Object)
        {
            var names = new HashSet<string>(StringComparer.Ordinal);
            foreach (var property in element.EnumerateObject())
            {
                if (!names.Add(property.Name))
                {
                    throw new InvalidDataException(
                        $"Worker process host {frameName} frame contains duplicate property '{property.Name}'.");
                }

                ValidateNoDuplicateProperties(property.Value, frameName);
            }

            return;
        }

        if (element.ValueKind != JsonValueKind.Array)
            return;
        foreach (var item in element.EnumerateArray())
            ValidateNoDuplicateProperties(item, frameName);
    }

    private static void ValidatePayload(GmWorkerProcessHostPayload payload)
    {
        if (string.IsNullOrWhiteSpace(payload.FileName))
            throw new InvalidDataException("Worker process host launch payload executable is empty.");
        if (payload.Arguments == null || payload.Arguments.Any(argument => argument == null))
            throw new InvalidDataException("Worker process host launch payload arguments are malformed.");
        if (payload.WorkingDirectory == null)
            throw new InvalidDataException("Worker process host launch payload working directory is missing.");
        if (payload.Environment == null)
            throw new InvalidDataException("Worker process host launch payload environment is missing.");
    }

    private static void ValidateStatusPayload(GmWorkerProcessHostStatusFrame frame)
    {
        switch (frame.Kind)
        {
            case GmWorkerProcessHostStatusKind.Ready:
                if (frame.ExitCode != null)
                    throw new InvalidDataException(
                        "Worker process host ready status must not contain exitCode.");
                if (frame.Error != null)
                    throw new InvalidDataException(
                        "Worker process host ready status must not contain error.");
                break;
            case GmWorkerProcessHostStatusKind.Completed:
                if (frame.ExitCode == null)
                    throw new InvalidDataException(
                        "Worker process host completed status requires exitCode.");
                if (frame.Error != null)
                    throw new InvalidDataException(
                        "Worker process host completed status must not contain error.");
                break;
            case GmWorkerProcessHostStatusKind.Failed:
                if (frame.ExitCode != null)
                    throw new InvalidDataException(
                        "Worker process host failed status must not contain exitCode.");
                if (string.IsNullOrWhiteSpace(frame.Error))
                    throw new InvalidDataException(
                        "Worker process host failed status requires error.");
                break;
            case GmWorkerProcessHostStatusKind.OutputDrained:
                if (frame.ExitCode != null)
                    throw new InvalidDataException(
                        "Worker process host output-drained status must not contain exitCode.");
                if (frame.Error != null)
                    throw new InvalidDataException(
                        "Worker process host output-drained status must not contain error.");
                break;
        }
    }

    private static void ValidateEnvelope(
        int schemaVersion,
        string? nonce,
        string expectedNonce,
        string frameName)
    {
        if (schemaVersion != SchemaVersion)
        {
            throw new InvalidDataException(
                $"Unsupported worker process host {frameName} schema: {schemaVersion}.");
        }

        ValidateLaunchNonce(nonce);
        ValidateLaunchNonce(expectedNonce);
        var actualBytes = Encoding.ASCII.GetBytes(nonce!);
        var expectedBytes = Encoding.ASCII.GetBytes(expectedNonce);
        if (!CryptographicOperations.FixedTimeEquals(actualBytes, expectedBytes))
            throw new InvalidDataException($"Worker process host {frameName} nonce does not match this launch.");
    }

    private static JsonSerializerOptions CreateJsonOptions()
    {
        var options = new JsonSerializerOptions
        {
            PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
            PropertyNameCaseInsensitive = false,
            UnmappedMemberHandling = JsonUnmappedMemberHandling.Disallow
        };
        options.Converters.Add(
            new JsonStringEnumConverter(JsonNamingPolicy.CamelCase, allowIntegerValues: false));
        return options;
    }
}

internal sealed class GmWorkerProcessHostLaunch : IAsyncDisposable
{
    private const string ModeSwitch = "--gm-worker-process-host";
    private static readonly TimeSpan HostReadyTimeout = TimeSpan.FromSeconds(15);
    private readonly NamedPipeServerStream _controlPipe;
    private readonly NamedPipeServerStream _statusPipe;
    private StreamWriter? _controlWriter;
    private StreamReader? _statusReader;
    private readonly SemaphoreSlim _connectionGate = new(1, 1);
    private readonly SemaphoreSlim _controlGate = new(1, 1);
    private readonly SemaphoreSlim _statusGate = new(1, 1);
    private readonly string _launchNonce;
    private readonly GmWorkerProcessHostPayload _payload;
    private int _connected;
    private int _launchSent;
    private int _released;
    private int _disposed;

    private GmWorkerProcessHostLaunch(
        ProcessStartInfo startInfo,
        NamedPipeServerStream controlPipe,
        NamedPipeServerStream statusPipe,
        string launchNonce,
        GmWorkerProcessHostPayload payload)
    {
        StartInfo = startInfo;
        _controlPipe = controlPipe;
        _statusPipe = statusPipe;
        _launchNonce = launchNonce;
        _payload = payload;
    }

    internal ProcessStartInfo StartInfo { get; }

    internal static GmWorkerProcessHostLaunch Create(
        ProcessStartInfo workerStartInfo,
        string launchDirectory)
    {
        ArgumentNullException.ThrowIfNull(workerStartInfo);
        ArgumentException.ThrowIfNullOrWhiteSpace(launchDirectory);
        Directory.CreateDirectory(launchDirectory);

        NamedPipeServerStream? controlPipe = null;
        NamedPipeServerStream? statusPipe = null;
        try
        {
            var launchNonce = Guid.NewGuid().ToString("N");
            var endpointNonce = Guid.NewGuid().ToString("N");
            var controlPipeName = $"boe-gm-worker-control-{endpointNonce}";
            var statusPipeName = $"boe-gm-worker-status-{endpointNonce}";
            const PipeOptions pipeOptions = PipeOptions.Asynchronous | PipeOptions.CurrentUserOnly;
            controlPipe = new NamedPipeServerStream(
                controlPipeName,
                PipeDirection.Out,
                maxNumberOfServerInstances: 1,
                PipeTransmissionMode.Byte,
                pipeOptions);
            statusPipe = new NamedPipeServerStream(
                statusPipeName,
                PipeDirection.In,
                maxNumberOfServerInstances: 1,
                PipeTransmissionMode.Byte,
                pipeOptions);
            var payload = new GmWorkerProcessHostPayload(
                workerStartInfo.FileName,
                workerStartInfo.ArgumentList.ToArray(),
                workerStartInfo.WorkingDirectory,
                workerStartInfo.Environment.ToDictionary(
                    entry => entry.Key,
                    entry => entry.Value,
                    StringComparer.OrdinalIgnoreCase));
            var assemblyPath = typeof(GmWorkerProcessHost).Assembly.Location;
            var appHostPath = Path.Combine(
                Path.GetDirectoryName(assemblyPath)!,
                Path.GetFileNameWithoutExtension(assemblyPath) +
                (OperatingSystem.IsWindows() ? ".exe" : ""));
            var hostStartInfo = new ProcessStartInfo
            {
                FileName = File.Exists(appHostPath) ? appHostPath : "dotnet",
                WorkingDirectory = workerStartInfo.WorkingDirectory,
                UseShellExecute = false,
                CreateNoWindow = true,
                WindowStyle = ProcessWindowStyle.Hidden,
                RedirectStandardOutput = true,
                RedirectStandardError = true,
                StandardOutputEncoding = Encoding.UTF8,
                StandardErrorEncoding = Encoding.UTF8
            };
            if (!File.Exists(appHostPath))
                hostStartInfo.ArgumentList.Add(assemblyPath);
            hostStartInfo.ArgumentList.Add(ModeSwitch);
            hostStartInfo.ArgumentList.Add(controlPipeName);
            hostStartInfo.ArgumentList.Add(statusPipeName);
            hostStartInfo.ArgumentList.Add(launchNonce);

            var launch = new GmWorkerProcessHostLaunch(
                hostStartInfo,
                controlPipe,
                statusPipe,
                launchNonce,
                payload);
            controlPipe = null;
            statusPipe = null;
            return launch;
        }
        finally
        {
            controlPipe?.Dispose();
            statusPipe?.Dispose();
        }
    }

    internal async Task WaitUntilReadyAsync(
        Process hostProcess,
        CancellationToken cancellationToken)
    {
        await ConnectAndAuthenticateAsync(hostProcess, cancellationToken);
        await SendLaunchAsync(cancellationToken);
        var frame = await ReadStatusAsync(
            hostProcess,
            GmWorkerProcessHostStatusKind.Ready,
            cancellationToken,
            HostReadyTimeout);
        _ = frame;
    }

    private async Task SendLaunchAsync(CancellationToken cancellationToken)
    {
        await _controlGate.WaitAsync(cancellationToken);
        try
        {
            if (Volatile.Read(ref _launchSent) != 0)
                return;
            var frame = new GmWorkerProcessHostControlFrame(
                GmWorkerProcessHostProtocol.SchemaVersion,
                _launchNonce,
                GmWorkerProcessHostControlKind.Launch,
                _payload);
            var controlWriter = _controlWriter ??
                                throw new InvalidOperationException(
                                    "Worker process host control channel is not connected.");
            await controlWriter.WriteLineAsync(
                GmWorkerProcessHostProtocol.SerializeControl(frame).AsMemory(),
                cancellationToken);
            Interlocked.Exchange(ref _launchSent, 1);
        }
        finally
        {
            _controlGate.Release();
        }
    }

    internal async Task ReleaseAsync(CancellationToken cancellationToken)
    {
        await _controlGate.WaitAsync(cancellationToken);
        try
        {
            if (Volatile.Read(ref _released) != 0)
                return;
            var frame = new GmWorkerProcessHostControlFrame(
                GmWorkerProcessHostProtocol.SchemaVersion,
                _launchNonce,
                GmWorkerProcessHostControlKind.Release);
            var controlWriter = _controlWriter ??
                                throw new InvalidOperationException(
                                    "Worker process host control channel is not connected.");
            await controlWriter.WriteLineAsync(
                GmWorkerProcessHostProtocol.SerializeControl(frame).AsMemory(),
                cancellationToken);
            Interlocked.Exchange(ref _released, 1);
        }
        finally
        {
            _controlGate.Release();
        }
    }

    internal async Task<int> WaitForWorkerCompletionAsync(
        Process hostProcess,
        CancellationToken cancellationToken)
    {
        var frame = await ReadStatusAsync(
            hostProcess,
            GmWorkerProcessHostStatusKind.Completed,
            cancellationToken,
            timeout: null);
        return frame.ExitCode!.Value;
    }

    internal async Task WaitForOutputDrainAsync(
        Process hostProcess,
        CancellationToken cancellationToken)
    {
        _ = await ReadStatusAsync(
            hostProcess,
            GmWorkerProcessHostStatusKind.OutputDrained,
            cancellationToken,
            timeout: null);
    }

    public ValueTask DisposeAsync()
    {
        if (Interlocked.Exchange(ref _disposed, 1) != 0)
            return ValueTask.CompletedTask;

        TryDispose(_controlWriter);
        TryDispose(_statusReader);
        TryDispose(_controlPipe);
        TryDispose(_statusPipe);
        _connectionGate.Dispose();
        _controlGate.Dispose();
        _statusGate.Dispose();
        return ValueTask.CompletedTask;
    }

    private async Task<GmWorkerProcessHostStatusFrame> ReadStatusAsync(
        Process hostProcess,
        GmWorkerProcessHostStatusKind expectedKind,
        CancellationToken cancellationToken,
        TimeSpan? timeout)
    {
        await _statusGate.WaitAsync(cancellationToken);
        try
        {
            using var readCancellation = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
            if (timeout.HasValue)
                readCancellation.CancelAfter(timeout.Value);

            string? line;
            try
            {
                var statusReader = _statusReader ??
                                   throw new InvalidOperationException(
                                       "Worker process host status channel is not connected.");
                line = await statusReader.ReadLineAsync(readCancellation.Token);
            }
            catch (OperationCanceledException) when (
                timeout.HasValue &&
                !cancellationToken.IsCancellationRequested &&
                readCancellation.IsCancellationRequested)
            {
                throw new TimeoutException(
                    "Worker process host did not become ready for ownership handshake.");
            }

            if (line == null)
            {
                var suffix = hostProcess.HasExited
                    ? $" with code {hostProcess.ExitCode}"
                    : string.Empty;
                throw new InvalidOperationException(
                    $"Worker process host status channel closed before {expectedKind}{suffix}.");
            }

            return GmWorkerProcessHostProtocol.ParseStatus(line, _launchNonce, expectedKind);
        }
        finally
        {
            _statusGate.Release();
        }
    }

    private async Task ConnectAndAuthenticateAsync(
        Process hostProcess,
        CancellationToken cancellationToken)
    {
        await _connectionGate.WaitAsync(cancellationToken);
        try
        {
            if (Volatile.Read(ref _connected) != 0)
                return;

            using var connectionCancellation = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
            connectionCancellation.CancelAfter(HostReadyTimeout);
            try
            {
                await Task.WhenAll(
                    _controlPipe.WaitForConnectionAsync(connectionCancellation.Token),
                    _statusPipe.WaitForConnectionAsync(connectionCancellation.Token));
            }
            catch (OperationCanceledException) when (
                !cancellationToken.IsCancellationRequested &&
                connectionCancellation.IsCancellationRequested)
            {
                throw new TimeoutException(
                    "Worker process host did not connect its private channels before the ownership deadline.");
            }

            ValidateConnectedHost(_controlPipe, hostProcess.Id, "control");
            ValidateConnectedHost(_statusPipe, hostProcess.Id, "status");
            _controlWriter = new StreamWriter(
                _controlPipe,
                new UTF8Encoding(encoderShouldEmitUTF8Identifier: false),
                bufferSize: 1024,
                leaveOpen: true)
            {
                AutoFlush = true
            };
            _statusReader = new StreamReader(
                _statusPipe,
                Encoding.UTF8,
                detectEncodingFromByteOrderMarks: false,
                bufferSize: 1024,
                leaveOpen: true);
            Volatile.Write(ref _connected, 1);
        }
        finally
        {
            _connectionGate.Release();
        }
    }

    private static void ValidateConnectedHost(
        NamedPipeServerStream pipe,
        int expectedProcessId,
        string channelName)
    {
        if (!OperatingSystem.IsWindows())
            throw new PlatformNotSupportedException(
                "Worker process host channel authentication requires Windows named-pipe client identity.");
        if (!NativeMethods.GetNamedPipeClientProcessId(pipe.SafePipeHandle, out var processId))
            throw new Win32Exception(
                Marshal.GetLastWin32Error(),
                $"Worker process host {channelName} channel client identity could not be read.");
        if (processId != (uint)expectedProcessId)
        {
            throw new InvalidDataException(
                $"Worker process host {channelName} channel was connected by an unexpected process.");
        }
    }

    private static void TryDispose(IDisposable? disposable)
    {
        if (disposable == null)
            return;
        try
        {
            disposable.Dispose();
        }
        catch (IOException)
        {
            // Channel cleanup must not replace the authoritative worker result.
        }
        catch (ObjectDisposedException)
        {
            // Idempotent cleanup.
        }
    }

    internal static bool IsModeSwitch(string value) =>
        string.Equals(value, ModeSwitch, StringComparison.Ordinal);

    private static class NativeMethods
    {
        [DllImport("kernel32.dll", SetLastError = true)]
        [return: MarshalAs(UnmanagedType.Bool)]
        internal static extern bool GetNamedPipeClientProcessId(
            SafePipeHandle pipe,
            out uint clientProcessId);
    }
}

internal static class GmWorkerProcessHost
{
    private const int HostFailureExitCode = 125;
    private static readonly TimeSpan OwnershipReleaseTimeout = TimeSpan.FromMinutes(1);
    private static readonly TimeSpan OutputDrainGracePeriod = TimeSpan.FromMilliseconds(250);

    internal static async Task<int?> TryRunAsync(IReadOnlyList<string> args)
    {
        if (args.Count == 0 || !GmWorkerProcessHostLaunch.IsModeSwitch(args[0]))
            return null;

        StreamWriter? statusWriter = null;
        string? launchNonce = args.Count == 4 ? args[3] : null;
        try
        {
            if (args.Count != 4)
                throw new ArgumentException("Worker process host invocation is incomplete.");
            GmWorkerProcessHostProtocol.ValidateLaunchNonce(launchNonce);
            using var controlPipe = new NamedPipeClientStream(
                ".",
                args[1],
                PipeDirection.In,
                PipeOptions.Asynchronous,
                TokenImpersonationLevel.Identification,
                HandleInheritability.None);
            using var statusPipe = new NamedPipeClientStream(
                ".",
                args[2],
                PipeDirection.Out,
                PipeOptions.Asynchronous,
                TokenImpersonationLevel.Identification,
                HandleInheritability.None);
            await Task.WhenAll(
                controlPipe.ConnectAsync((int)OwnershipReleaseTimeout.TotalMilliseconds),
                statusPipe.ConnectAsync((int)OwnershipReleaseTimeout.TotalMilliseconds));
            using var controlReader = new StreamReader(
                controlPipe,
                Encoding.UTF8,
                detectEncodingFromByteOrderMarks: false,
                bufferSize: 1024,
                leaveOpen: true);
            statusWriter = new StreamWriter(
                statusPipe,
                new UTF8Encoding(encoderShouldEmitUTF8Identifier: false),
                bufferSize: 1024,
                leaveOpen: true)
            {
                AutoFlush = true
            };

            var launchJson = await controlReader.ReadLineAsync()
                                 .WaitAsync(OwnershipReleaseTimeout) ??
                             throw new InvalidDataException(
                                 "Worker process host control channel closed before launch payload.");
            var launchFrame = GmWorkerProcessHostProtocol.ParseControl(
                launchJson,
                launchNonce!,
                GmWorkerProcessHostControlKind.Launch);
            var payload = launchFrame.Payload ??
                          throw new InvalidDataException(
                              "Worker process host launch payload is empty.");

            await WriteStatusAsync(
                statusWriter,
                new GmWorkerProcessHostStatusFrame(
                    GmWorkerProcessHostProtocol.SchemaVersion,
                    launchNonce!,
                    GmWorkerProcessHostStatusKind.Ready,
                    ExitCode: null,
                    Error: null));
            var controlJson = await controlReader.ReadLineAsync()
                                  .WaitAsync(OwnershipReleaseTimeout) ??
                              throw new InvalidDataException(
                                  "Worker process host control channel closed before release.");
            GmWorkerProcessHostProtocol.ParseControl(
                controlJson,
                launchNonce!,
                GmWorkerProcessHostControlKind.Release);
            await RunWorkerAsync(payload, statusWriter, launchNonce!);
            throw new InvalidOperationException("Worker process host lifecycle ended unexpectedly.");
        }
        catch (Exception ex)
        {
            if (statusWriter != null && launchNonce != null)
            {
                try
                {
                    await WriteStatusAsync(
                        statusWriter,
                        new GmWorkerProcessHostStatusFrame(
                            GmWorkerProcessHostProtocol.SchemaVersion,
                            launchNonce,
                            GmWorkerProcessHostStatusKind.Failed,
                            ExitCode: null,
                            Error: ex.Message));
                }
                catch (Exception statusException) when (
                    statusException is IOException or ObjectDisposedException)
                {
                    // The owner may already have closed its private status channel.
                }
            }

            await Console.Error.WriteLineAsync($"worker-process-host failed: {ex.Message}");
            return HostFailureExitCode;
        }
        finally
        {
            statusWriter?.Dispose();
        }
    }

    private static async Task RunWorkerAsync(
        GmWorkerProcessHostPayload payload,
        StreamWriter statusWriter,
        string launchNonce)
    {
        if (string.IsNullOrWhiteSpace(payload.FileName))
            throw new InvalidDataException("Worker process host executable is empty.");
        var startInfo = new ProcessStartInfo
        {
            FileName = payload.FileName,
            WorkingDirectory = payload.WorkingDirectory,
            UseShellExecute = false,
            CreateNoWindow = true,
            WindowStyle = ProcessWindowStyle.Hidden,
            RedirectStandardInput = true,
            RedirectStandardOutput = true,
            RedirectStandardError = true
        };
        foreach (var argument in payload.Arguments)
            startInfo.ArgumentList.Add(argument);
        startInfo.Environment.Clear();
        foreach (var (key, value) in payload.Environment)
            startInfo.Environment[key] = value;

        using var process = new Process { StartInfo = startInfo };
        if (!process.Start())
            throw new InvalidOperationException("Worker command did not start inside its process host.");

        var stdout = process.StandardOutput.BaseStream.CopyToAsync(
            Console.OpenStandardOutput(),
            CancellationToken.None);
        var stderr = process.StandardError.BaseStream.CopyToAsync(
            Console.OpenStandardError(),
            CancellationToken.None);
        await process.WaitForExitAsync(CancellationToken.None);
        var exitCode = process.ExitCode;
        await WriteStatusAsync(
            statusWriter,
            new GmWorkerProcessHostStatusFrame(
                GmWorkerProcessHostProtocol.SchemaVersion,
                launchNonce,
                GmWorkerProcessHostStatusKind.Completed,
                exitCode,
                Error: null));

        var drain = Task.WhenAll(stdout, stderr);
        try
        {
            if (await Task.WhenAny(drain, Task.Delay(OutputDrainGracePeriod)) == drain)
                await drain;
        }
        catch (IOException)
        {
            // Output capture is diagnostic and cannot revoke the authoritative worker exit code.
        }
        await WriteStatusAsync(
            statusWriter,
            new GmWorkerProcessHostStatusFrame(
                GmWorkerProcessHostProtocol.SchemaVersion,
                launchNonce,
                GmWorkerProcessHostStatusKind.OutputDrained,
                ExitCode: null,
                Error: null));
        await Task.Delay(Timeout.InfiniteTimeSpan, CancellationToken.None);
    }

    private static async Task WriteStatusAsync(
        StreamWriter writer,
        GmWorkerProcessHostStatusFrame frame)
    {
        await writer.WriteLineAsync(GmWorkerProcessHostProtocol.SerializeStatus(frame));
        await writer.FlushAsync();
    }

}
