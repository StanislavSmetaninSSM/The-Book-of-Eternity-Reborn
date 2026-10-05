using System.Diagnostics;
using System.IO.Pipes;
using System.Security.Cryptography;
using System.Security.Principal;
using System.Text;
using System.Text.Json;
using System.Text.Json.Serialization;

namespace BookOfEternityClient.Services.GmWorkers;

internal sealed record GmWorkerProcessHostPayload(
    [property: JsonRequired] string FileName,
    [property: JsonRequired] IReadOnlyList<string> Arguments,
    [property: JsonRequired] string WorkingDirectory,
    [property: JsonRequired] Dictionary<string, string?> Environment)
{
    internal static GmWorkerProcessHostPayload Capture(ProcessStartInfo workerStartInfo) => new(
        workerStartInfo.FileName,
        workerStartInfo.ArgumentList.ToArray(),
        workerStartInfo.WorkingDirectory,
        workerStartInfo.Environment.ToDictionary(
            entry => entry.Key,
            entry => entry.Value,
            // Match ProcessStartInfo.Environment: case aliases are distinct on Unix.
            OperatingSystem.IsWindows() ? StringComparer.OrdinalIgnoreCase : StringComparer.Ordinal));
}

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
            throw new InvalidOperationException("Worker process host reported failure.");
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
        catch (JsonException)
        {
            // JsonException messages and paths can contain environment keys or payload excerpts.
            throw new InvalidDataException($"Worker process host {frameName} frame is malformed.");
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
                        $"Worker process host {frameName} frame contains a duplicate property.");
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
    private static readonly TimeSpan OwnershipReleaseTimeout = TimeSpan.FromSeconds(60);
    private readonly NamedPipeServerStream _controlPipe;
    private readonly NamedPipeServerStream _statusPipe;
    private GmWorkerProcessHostFrameChannel? _controlChannel;
    private GmWorkerProcessHostFrameChannel? _statusChannel;
    private readonly SemaphoreSlim _connectionGate = new(1, 1);
    private readonly SemaphoreSlim _controlGate = new(1, 1);
    private readonly SemaphoreSlim _statusGate = new(1, 1);
    private readonly string _launchNonce;
    private readonly GmWorkerProcessHostPayload _payload;
    private int _connected;
    private int _launchSent;
    private int _released;
    private GmWorkerHostIdentity? _readyIdentity;
    private int _disposed;
    private GmWorkerRequiredCapability _capability = GmWorkerRequiredCapability.WorkerRelease;

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
    internal string WorkerWorkingDirectory => _payload.WorkingDirectory;
    private GmWorkerNativePoolAdmission? _nativeAdmission;
    internal bool HasAdmission(GmWorkerNativePoolAdmission admission) => ReferenceEquals(_nativeAdmission, admission);

    internal async Task<GmWorkerOwnedLaunch> PrepareOwnedAsync(IGmWorkerOwnedLauncher launcher,
        GmWorkerBackendRequest request, GmWorkerRequiredCapability capability, CancellationToken cancellationToken,
        GmWorkerNativePoolAdmission? nativeAdmission = null)
    {
        var selection = capability == GmWorkerRequiredCapability.SyntheticWorkerRelease &&
            request == GmWorkerBackendRequest.NativeLineage && nativeAdmission != null
            ? nativeAdmission.ValidateHost(this)
            : GmWorkerBackendSelector.Select(request, capability, OperatingSystem.IsWindows(), OperatingSystem.IsLinux());
        if (!selection.CanStart) throw new PlatformNotSupportedException(selection.Reason);
        _capability = capability;
        _nativeAdmission = nativeAdmission;
        var owner = await launcher.StartAsync(this, selection, cancellationToken);
        try
        {
            await owner.WaitUntilReadyAsync(this, cancellationToken);
            return owner;
        }
        catch (Exception ex)
        {
            throw new GmWorkerOwnedLaunchException("Owned worker host did not become ready.", owner, ex);
        }
    }

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
            var payload = GmWorkerProcessHostPayload.Capture(workerStartInfo);
            var assemblyPath = typeof(GmWorkerProcessHost).Assembly.Location;
            var appHostPath = Path.Combine(
                Path.GetDirectoryName(assemblyPath)!,
                Path.GetFileNameWithoutExtension(assemblyPath) +
                (OperatingSystem.IsWindows() ? ".exe" : ""));
            var hostStartInfo = new ProcessStartInfo
            {
                FileName = File.Exists(appHostPath) ? appHostPath : ResolveDotnetExecutable(),
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

    private static string ResolveDotnetExecutable()
    {
        if (!OperatingSystem.IsLinux()) return "dotnet";
        var current = Environment.ProcessPath;
        if (current != null && Path.GetFileName(current) == "dotnet" && Path.IsPathFullyQualified(current)) return current;
        var root = Environment.GetEnvironmentVariable("DOTNET_ROOT");
        if (root != null && Path.IsPathFullyQualified(root) && File.Exists(Path.Combine(root, "dotnet"))) return Path.Combine(root, "dotnet");
        foreach (var directory in (Environment.GetEnvironmentVariable("PATH") ?? "").Split(Path.PathSeparator))
        {
            if (!Path.IsPathFullyQualified(directory)) continue;
            var candidate = Path.Combine(directory, "dotnet");
            if (File.Exists(candidate)) return candidate;
        }
        throw new FileNotFoundException("An absolute managed host executable could not be resolved.");
    }

    internal Task WaitUntilReadyAsync(Process hostProcess, CancellationToken cancellationToken) =>
        WaitUntilReadyAsync(GmWorkerHostIdentity.FromOwnedProcess(hostProcess), cancellationToken);

    internal async Task WaitUntilReadyAsync(
        GmWorkerHostIdentity hostProcess,
        CancellationToken cancellationToken)
    {
        using var readiness = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
        readiness.CancelAfter(HostReadyTimeout);
        try
        {
            await ConnectAndAuthenticateAsync(hostProcess, readiness.Token);
            await SendLaunchAsync(hostProcess, readiness.Token);
            _ = await ReadStatusAsync(hostProcess, GmWorkerProcessHostStatusKind.Ready, readiness.Token);
            _readyIdentity = hostProcess;
        }
        catch (OperationCanceledException) when (!cancellationToken.IsCancellationRequested && readiness.IsCancellationRequested)
        {
            CloseChannels();
            throw new TimeoutException("Worker process host did not become ready before the ownership deadline.");
        }
        catch
        {
            CloseChannels();
            throw;
        }
    }

    private async Task SendLaunchAsync(GmWorkerHostIdentity hostProcess, CancellationToken cancellationToken)
    {
        await _controlGate.WaitAsync(cancellationToken);
        try
        {
            cancellationToken.ThrowIfCancellationRequested();
            EnsureHostIsRunning(hostProcess);
            if (Volatile.Read(ref _launchSent) != 0)
                return;
            var frame = new GmWorkerProcessHostControlFrame(
                GmWorkerProcessHostProtocol.SchemaVersion,
                _launchNonce,
                GmWorkerProcessHostControlKind.Launch,
                _payload);
            var controlChannel = _controlChannel ??
                                throw new InvalidOperationException(
                                    "Worker process host control channel is not connected.");
            await controlChannel.WriteAsync(
                GmWorkerProcessHostProtocol.SerializeControl(frame),
                GmWorkerProcessHostFrameChannel.LaunchMaximumBytes,
                HostReadyTimeout, cancellationToken);
            Interlocked.Exchange(ref _launchSent, 1);
        }
        finally
        {
            _controlGate.Release();
        }
    }

    internal async Task ReleaseAsync(CancellationToken cancellationToken)
    {
        if (_capability != GmWorkerRequiredCapability.WorkerRelease &&
            !(_capability == GmWorkerRequiredCapability.SyntheticWorkerRelease && _nativeAdmission != null))
            throw new InvalidOperationException("This owned host was admitted for NeutralHost only.");
        using var deadline = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
        deadline.CancelAfter(OwnershipReleaseTimeout);
        var entered = false;
        try
        {
            await _controlGate.WaitAsync(deadline.Token);
            entered = true;
            if (Volatile.Read(ref _released) != 0) return;
            // Revalidate the original admitted pidfd/helper authority after the
            // control gate and every caller hook, before sending native Release.
            if (_capability == GmWorkerRequiredCapability.SyntheticWorkerRelease)
                (_readyIdentity ?? throw new InvalidOperationException("Native Release has no admitted ready identity.")).EnsureLive();
            var frame = new GmWorkerProcessHostControlFrame(
                GmWorkerProcessHostProtocol.SchemaVersion, _launchNonce, GmWorkerProcessHostControlKind.Release);
            var controlChannel = _controlChannel ?? throw new InvalidOperationException(
                "Worker process host control channel is not connected.");
            await controlChannel.WriteAsync(GmWorkerProcessHostProtocol.SerializeControl(frame),
                GmWorkerProcessHostFrameChannel.SmallMaximumBytes, OwnershipReleaseTimeout, deadline.Token);
            Interlocked.Exchange(ref _released, 1);
        }
        catch (OperationCanceledException) when (!cancellationToken.IsCancellationRequested && deadline.IsCancellationRequested)
        {
            CloseChannels();
            throw new TimeoutException("Worker process host release did not finish before its deadline.");
        }
        catch
        {
            CloseChannels();
            throw;
        }
        finally
        {
            if (entered) _controlGate.Release();
        }
    }

    internal Task<int> WaitForWorkerCompletionAsync(
        Process hostProcess,
        CancellationToken cancellationToken) => WaitForWorkerCompletionAsync(GmWorkerHostIdentity.FromOwnedProcess(hostProcess), cancellationToken);

    internal async Task<int> WaitForWorkerCompletionAsync(
        GmWorkerHostIdentity hostIdentity, CancellationToken cancellationToken)
    {
        var frame = await ReadStatusAsync(
            hostIdentity,
            GmWorkerProcessHostStatusKind.Completed,
            cancellationToken);
        return frame.ExitCode!.Value;
    }

    internal async Task WaitForOutputDrainAsync(
        Process hostProcess,
        CancellationToken cancellationToken)
    {
        _ = await ReadStatusAsync(
            GmWorkerHostIdentity.FromOwnedProcess(hostProcess),
            GmWorkerProcessHostStatusKind.OutputDrained,
            cancellationToken);
    }

    public ValueTask DisposeAsync()
    {
        if (Interlocked.Exchange(ref _disposed, 1) != 0)
            return ValueTask.CompletedTask;

        CloseChannels();
        _connectionGate.Dispose();
        _controlGate.Dispose();
        _statusGate.Dispose();
        return ValueTask.CompletedTask;
    }

    private async Task<GmWorkerProcessHostStatusFrame> ReadStatusAsync(
        GmWorkerHostIdentity hostProcess,
        GmWorkerProcessHostStatusKind expectedKind,
        CancellationToken cancellationToken)
    {
        await _statusGate.WaitAsync(cancellationToken);
        try
        {
            var channel = _statusChannel ?? throw new InvalidOperationException(
                "Worker process host status channel is not connected.");
            var line = await channel.ReadAsync(GmWorkerProcessHostFrameChannel.SmallMaximumBytes,
                GmWorkerProcessHostFrameChannel.FrameTimeout, cancellationToken,
                waitForFirstByte: expectedKind != GmWorkerProcessHostStatusKind.Ready);

            if (line == null)
            {
                var suffix = hostProcess.ExitDescription;
                throw new InvalidOperationException(
                    $"Worker process host status channel closed before {expectedKind}{suffix}.");
            }

            return GmWorkerProcessHostProtocol.ParseStatus(line, _launchNonce, expectedKind);
        }
        catch
        {
            CloseChannels();
            throw;
        }
        finally
        {
            _statusGate.Release();
        }
    }

    private async Task ConnectAndAuthenticateAsync(
        GmWorkerHostIdentity hostProcess,
        CancellationToken cancellationToken)
    {
        await _connectionGate.WaitAsync(cancellationToken);
        try
        {
            if (Volatile.Read(ref _connected) != 0)
                return;

            // The token retains the original owned Process or checked transferred pidfd.
            // Never obtain launch identity from a peer frame or a fresh PID lookup.
            EnsureHostIsRunning(hostProcess);
            var expectedProcessId = hostProcess.ProcessId;
            var expectedUserId = hostProcess.UserId;
            await Task.WhenAll(
                _controlPipe.WaitForConnectionAsync(cancellationToken),
                _statusPipe.WaitForConnectionAsync(cancellationToken));

            cancellationToken.ThrowIfCancellationRequested();
            EnsureHostIsRunning(hostProcess);
            GmWorkerProcessHostPeerIdentity.Validate(_controlPipe.SafePipeHandle, expectedProcessId, expectedUserId, "control");
            GmWorkerProcessHostPeerIdentity.Validate(_statusPipe.SafePipeHandle, expectedProcessId, expectedUserId, "status");
            // SO_PEERCRED describes connection-time credentials, not ongoing liveness.
            EnsureHostIsRunning(hostProcess);
            var controlChannel = new GmWorkerProcessHostFrameChannel(_controlPipe);
            var statusChannel = new GmWorkerProcessHostFrameChannel(_statusPipe);
            cancellationToken.ThrowIfCancellationRequested();
            _controlChannel = controlChannel;
            _statusChannel = statusChannel;
            Volatile.Write(ref _connected, 1);
        }
        finally
        {
            _connectionGate.Release();
        }
    }

    private static void EnsureHostIsRunning(GmWorkerHostIdentity hostProcess) => hostProcess.EnsureLive();

    private void CloseChannels()
    {
        TryDispose(_controlPipe);
        TryDispose(_statusPipe);
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

        NamedPipeClientStream? controlPipe = null;
        NamedPipeClientStream? statusPipe = null;
        GmWorkerProcessHostFrameChannel? statusChannel = null;
        string? launchNonce = args.Count == 4 ? args[3] : null;
        try
        {
            if (args.Count != 4)
                throw new ArgumentException("Worker process host invocation is incomplete.");
            GmWorkerProcessHostProtocol.ValidateLaunchNonce(launchNonce);
            controlPipe = new NamedPipeClientStream(
                ".",
                args[1],
                PipeDirection.In,
                PipeOptions.Asynchronous,
                TokenImpersonationLevel.Identification,
                HandleInheritability.None);
            statusPipe = new NamedPipeClientStream(
                ".",
                args[2],
                PipeDirection.Out,
                PipeOptions.Asynchronous,
                TokenImpersonationLevel.Identification,
                HandleInheritability.None);
            using var startup = new CancellationTokenSource(GmWorkerProcessHostFrameChannel.FrameTimeout);
            await Task.WhenAll(controlPipe.ConnectAsync(startup.Token), statusPipe.ConnectAsync(startup.Token));
            var controlChannel = new GmWorkerProcessHostFrameChannel(controlPipe);
            statusChannel = new GmWorkerProcessHostFrameChannel(statusPipe);
            var launchJson = await controlChannel.ReadAsync(GmWorkerProcessHostFrameChannel.LaunchMaximumBytes,
                GmWorkerProcessHostFrameChannel.FrameTimeout, startup.Token) ??
                throw new InvalidDataException("Worker process host control channel closed before launch payload.");
            var launchFrame = GmWorkerProcessHostProtocol.ParseControl(
                launchJson,
                launchNonce!,
                GmWorkerProcessHostControlKind.Launch);
            var payload = launchFrame.Payload ??
                          throw new InvalidDataException(
                              "Worker process host launch payload is empty.");

            await WriteStatusAsync(
                statusChannel,
                new GmWorkerProcessHostStatusFrame(
                    GmWorkerProcessHostProtocol.SchemaVersion,
                    launchNonce!,
                    GmWorkerProcessHostStatusKind.Ready,
                    ExitCode: null,
                    Error: null), startup.Token);
            var controlJson = await controlChannel.ReadAsync(GmWorkerProcessHostFrameChannel.SmallMaximumBytes,
                OwnershipReleaseTimeout, CancellationToken.None) ??
                throw new InvalidDataException("Worker process host control channel closed before release.");
            GmWorkerProcessHostProtocol.ParseControl(
                controlJson,
                launchNonce!,
                GmWorkerProcessHostControlKind.Release);
            await RunWorkerAsync(payload, statusChannel, launchNonce!);
            throw new InvalidOperationException("Worker process host lifecycle ended unexpectedly.");
        }
        catch (Exception ex)
        {
            if (statusChannel != null && launchNonce != null)
            {
                try
                {
                    await WriteStatusAsync(
                        statusChannel,
                        new GmWorkerProcessHostStatusFrame(
                            GmWorkerProcessHostProtocol.SchemaVersion,
                            launchNonce,
                            GmWorkerProcessHostStatusKind.Failed,
                            ExitCode: null,
                            Error: SafeFailureReason(ex)));
                }
                catch (Exception statusException) when (
                    statusException is IOException or ObjectDisposedException or TimeoutException or OperationCanceledException)
                {
                    // The owner may already have closed its private status channel.
                }
            }

            await Console.Error.WriteLineAsync($"worker-process-host failed: {SafeFailureReason(ex)}");
            return HostFailureExitCode;
        }
        finally
        {
            controlPipe?.Dispose();
            statusPipe?.Dispose();
        }
    }

    internal static ProcessStartInfo CreateWorkerStartInfo(GmWorkerProcessHostPayload payload)
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

        return startInfo;
    }

    private static async Task RunWorkerAsync(
        GmWorkerProcessHostPayload payload,
        GmWorkerProcessHostFrameChannel statusChannel,
        string launchNonce)
    {
        using var process = new Process { StartInfo = CreateWorkerStartInfo(payload) };
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
            statusChannel,
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
            statusChannel,
            new GmWorkerProcessHostStatusFrame(
                GmWorkerProcessHostProtocol.SchemaVersion,
                launchNonce,
                GmWorkerProcessHostStatusKind.OutputDrained,
                ExitCode: null,
                Error: null));
        await Task.Delay(Timeout.InfiniteTimeSpan, CancellationToken.None);
    }

    private static Task WriteStatusAsync(
        GmWorkerProcessHostFrameChannel channel,
        GmWorkerProcessHostStatusFrame frame,
        CancellationToken cancellationToken = default) =>
        channel.WriteAsync(GmWorkerProcessHostProtocol.SerializeStatus(frame),
            GmWorkerProcessHostFrameChannel.SmallMaximumBytes,
            GmWorkerProcessHostFrameChannel.FrameTimeout, cancellationToken);

    private static string SafeFailureReason(Exception exception) => exception switch
    {
        InvalidDataException => "invalid protocol data",
        OperationCanceledException or TimeoutException => "protocol deadline or cancellation",
        IOException => "channel I/O failure",
        _ => "host operation failed"
    };
}
