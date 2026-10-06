using System.Diagnostics;
using System.IO.Pipes;
using System.Net.Sockets;
using System.Text;
using System.Text.Json;
using Microsoft.Win32.SafeHandles;

namespace BookOfEternityClient.Services.GmWorkers;

// One retained owner contains every partial launch resource. Status has a dedicated
// bounded reader from Process.Start onward; host output never enters this parser.
internal class NativeLineageOwner : GmWorkerOwnedLaunch
{
    private readonly bool _terminalMode;
    private SafeFileHandle? _terminalMaster;
    internal SafeFileHandle TakeTerminalMaster() => Interlocked.Exchange(ref _terminalMaster, null) ?? throw new InvalidOperationException("Terminal master missing.");
    internal void ReportTerminalFault(string reason) => Lose(reason);
    private readonly string _run;
    protected readonly GmWorkerDurableExecution? _durable;
    private readonly string _bootstrapDirectory;
    private readonly Socket _listener;
    private Socket? _bootstrap;
    protected readonly Process _supervisor = new();
    private readonly AnonymousPipeServerStream? _output;
    private readonly AnonymousPipeServerStream? _error;
    private readonly TaskCompletionSource _ready = new(TaskCreationOptions.RunContinuationsAsynchronously);
    private readonly TaskCompletionSource _startedHost = new(TaskCreationOptions.RunContinuationsAsynchronously);
    private readonly object _stateGate = new();
    private readonly SemaphoreSlim _stopGate = new(1);
    private readonly SemaphoreSlim _controlGate = new(1);
    private readonly Task<string> _outputDrain, _errorDrain;
    private Task<string>? _helperErrorDrain;
    protected Task? _statusReader, _supervisorExit, _hostExit, _stopControl;
    private GmWorkerHostIdentity? _identity;
    protected SafeFileHandle? _hostPidfd;
    protected bool _processStarted, _sealed, _stopped, _disposed, _outputsSettled;
    protected string? _uncertainty;
    protected GmWorkerStopEvidence? _terminal;
    private int _phase;
    private GmWorkerNativeObservationFault? _observationFault;
    protected Task? _outputObservation;

    internal void SetSyntheticObservationFault(GmWorkerNativeObservationFault fault)
    {
        if (Interlocked.CompareExchange(ref _observationFault, fault, null) != null)
            throw new InvalidOperationException("A native observation fault is already fixed for this owner.");
    }

    protected NativeLineageOwner(GmWorkerDurableExecution? durable, bool terminal = false)
    {
        _terminalMode = terminal; _durable = durable; _run = durable?.Identity.RunId ?? Guid.NewGuid().ToString("N");
        _bootstrapDirectory = Path.Combine(Path.GetTempPath(), "boe-native-" + _run);
        var socketPath = Path.Combine(_bootstrapDirectory, "owner");
        if (Encoding.UTF8.GetByteCount(socketPath) > 107)
            throw new InvalidDataException("Private native bootstrap path exceeds the Linux socket bound.");
        var createdDirectory = false;
        try
        {
            Directory.CreateDirectory(_bootstrapDirectory, UnixFileMode.UserRead | UnixFileMode.UserWrite | UnixFileMode.UserExecute);
            createdDirectory = true;
            _listener = new Socket(AddressFamily.Unix, SocketType.Seqpacket, ProtocolType.Unspecified);
            _listener.Bind(new UnixDomainSocketEndPoint(socketPath));
            _listener.Listen(1);
            if (!terminal) {
            _output = new AnonymousPipeServerStream(PipeDirection.In, HandleInheritability.None);
            _error = new AnonymousPipeServerStream(PipeDirection.In, HandleInheritability.None);
            _outputDrain = CaptureHostOutputAsync(_output);
            _errorDrain = CaptureHostOutputAsync(_error);
            } else { _outputDrain = Task.FromResult(""); _errorDrain = Task.FromResult(""); }
        }
        catch
        {
            // No process start has been attempted. Roll back each acquired local
            // handle deterministically instead of relying on eventual finalization.
            _listener?.Dispose(); _output?.Dispose(); _error?.Dispose(); _supervisor.Dispose();
            _stopGate.Dispose(); _controlGate.Dispose();
            if (createdDirectory) Directory.Delete(_bootstrapDirectory, recursive: true);
            throw;
        }
    }

    internal override int HostProcessId => _identity?.ProcessId ?? throw new InvalidOperationException("Host identity has not been admitted.");
    internal override GmWorkerExecutionIdentity Identity => new(_run, GmWorkerBackend.NativeLineage, GmWorkerBackendSelector.NativeGuarantee);
    internal override int? AdmittedHostProcessId => _identity?.ProcessId;
    internal override Task HostExited => _hostExit ?? throw new InvalidOperationException("Host identity has not been admitted.");
    internal override int SupervisorProcessId => _supervisor.Id;
    internal Task<string> HostStandardOutput => _outputDrain;
    internal Task<string> HostStandardError => _errorDrain;
    internal Task SupervisorExited => _supervisorExit ?? Task.CompletedTask;

    internal static async Task<GmWorkerOwnedLaunch> StartOwnedAsync(GmWorkerProcessHostLaunch host, string executable, CancellationToken cancellationToken, GmWorkerDurableExecution? durable = null)
    {
        if (!Path.IsPathFullyQualified(host.StartInfo.FileName)) throw new InvalidDataException("Native host executable must be absolute.");
        cancellationToken.ThrowIfCancellationRequested();
        durable?.ConsumeNativeStart(host);
        var owner = new GmWorkerNativeLineageLaunch(durable);
        try { await owner.StartAsync(host.StartInfo, executable, cancellationToken); return owner; }
        catch (Exception ex)
        {
            owner.Lose("partial-launch");
            owner.CloseBootstrap();
            throw new GmWorkerOwnedLaunchException("Native host start retains its original owner.", owner, ex);
        }
    }

    internal static async Task<NativeLineageOwner> StartTerminalAsync(ProcessStartInfo fixture, string executable, int columns, int rows, CancellationToken token)
    {
        var owner = new NativeLineageOwner(null, terminal: true);
        try { await owner.StartAsync(fixture, executable, token, columns, rows); return owner; }
        catch (Exception ex) { owner.Lose("partial-terminal-launch"); owner.CloseBootstrap(); throw new GmWorkerOwnedLaunchException("Terminal partial launch retains owner.", owner, ex); }
    }

    private async Task StartAsync(ProcessStartInfo host, string executable, CancellationToken cancellationToken, int columns = 80, int rows = 25)
    {
        var start = new ProcessStartInfo(executable)
        {
            UseShellExecute = false, RedirectStandardInput = true, RedirectStandardOutput = true,
            RedirectStandardError = true, WorkingDirectory = host.WorkingDirectory,
            CreateNoWindow = true
        };
        foreach (var arg in new[] { _run, "100", "2500", _terminalMode ? "--terminal-v1" : "--host-v2", Path.Combine(_bootstrapDirectory, "owner") }) start.ArgumentList.Add(arg);
        if (_terminalMode) { start.ArgumentList.Add(columns.ToString(System.Globalization.CultureInfo.InvariantCulture)); start.ArgumentList.Add(rows.ToString(System.Globalization.CultureInfo.InvariantCulture)); }
        start.ArgumentList.Add(host.FileName);
        foreach (var arg in host.ArgumentList) start.ArgumentList.Add(arg);
        _supervisor.StartInfo = start;
        if (!_supervisor.Start()) throw new InvalidOperationException("Native supervisor did not start.");
        _processStarted = true;
        _supervisorExit = _supervisor.WaitForExitAsync();
        _statusReader = ReadStatusAsync(_supervisor.StandardOutput.BaseStream);
        _helperErrorDrain = DrainAsync(_supervisor.StandardError.BaseStream);
        using var deadline = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
        deadline.CancelAfter(TimeSpan.FromSeconds(5));
        _bootstrap = await _listener.AcceptAsync(deadline.Token);
        _listener.Dispose();
        if (_supervisor.HasExited) throw new InvalidDataException("Native bootstrap supervisor exited.");
        GmWorkerProcessHostPeerIdentity.Validate(_bootstrap.SafeHandle, _supervisor.Id,
            GmWorkerProcessHostPeerIdentity.CaptureEffectiveUserId(), "native-bootstrap");
        await GmWorkerNativeDescriptors.ReceiveAsync(_bootstrap, (_terminalMode ? "T1:" : "H2:") + _run, 0, deadline.Token);
        if (_supervisor.HasExited) throw new InvalidDataException("Native bootstrap supervisor exited.");
        if (_terminalMode) GmWorkerNativeDescriptors.Send(_bootstrap, "P1:" + _run);
        else GmWorkerNativeDescriptors.Send(_bootstrap, "P2:" + _run, _output!.ClientSafePipeHandle, _error!.ClientSafePipeHandle);
        await _ready.Task.WaitAsync(deadline.Token);
        // Ready proves the helper owns its copies. Closing ours makes output EOF
        // depend only on actual host/descendant holders after the helper forks.
        _output?.DisposeLocalCopyOfClientHandle(); _error?.DisposeLocalCopyOfClientHandle();
        await SendControlAsync('L');
        var rights = await GmWorkerNativeDescriptors.ReceiveAsync(_bootstrap, (_terminalMode ? "B1:" : "B2:") + _run, _terminalMode ? 2 : 1, deadline.Token);
        _hostPidfd = rights[0];
        if (_terminalMode) _terminalMaster = rights[1];
        _identity = GmWorkerHostIdentity.FromTransferredPidfd(_supervisor, _hostPidfd, AuthorityValid);
        _hostExit = ObserveHostExitAsync();
        GmWorkerNativeDescriptors.Send(_bootstrap, (_terminalMode ? "A1:" : "A2:") + _run);
        _bootstrap.Dispose(); _bootstrap = null;
        await _startedHost.Task.WaitAsync(deadline.Token);
        _identity.EnsureLive();
    }

    internal override Task WaitUntilReadyAsync(GmWorkerProcessHostLaunch host, CancellationToken cancellationToken) =>
        host.WaitUntilReadyAsync(_identity ?? throw new InvalidOperationException("Native host binding is missing."), cancellationToken);

    internal override Task<int> WaitForWorkerCompletionAsync(GmWorkerProcessHostLaunch host, CancellationToken cancellationToken) =>
        host.WaitForWorkerCompletionAsync(_identity ?? throw new InvalidOperationException("Native host binding is missing."), cancellationToken);

    private async Task ObserveHostExitAsync()
    {
        try { await _identity!.WaitForExitAsync(); }
        catch { Lose("host-exit-observation-lost"); throw; }
    }

    private bool AuthorityValid() { lock (_stateGate) return _uncertainty == null && _terminal == null && !_sealed; }
    private void Lose(string reason)
    {
        _durable?.CloseForUncertainty();
        lock (_stateGate) _uncertainty ??= reason;
        _ready.TrySetException(new InvalidDataException("Native supervisor authority became uncertain."));
        _startedHost.TrySetException(new InvalidDataException("Native supervisor authority became uncertain."));
    }

    private async Task ReadStatusAsync(Stream input)
    {
        var buffer = new byte[1024]; var used = 0; var one = new byte[1];
        try
        {
            while (await input.ReadAsync(one) != 0)
            {
                if (one[0] != (byte)'\n')
                {
                    if (used == buffer.Length) throw new InvalidDataException("Native status exceeds its bound.");
                    buffer[used++] = one[0]; continue;
                }
                ReadOnlyMemory<byte> bytes = buffer.AsMemory(0, used); used = 0;
                if (_observationFault != null) bytes = await _observationFault.ObserveFrameAsync(bytes);
                using var frame = JsonDocument.Parse(bytes);
                AcceptStatus(frame.RootElement);
            }
            lock (_stateGate) if (used != 0 || _terminal == null) Lose("status-lost");
        }
        catch { Lose("status-invalid"); }
    }

    private void AcceptStatus(JsonElement frame)
    {
        var names = new HashSet<string>(StringComparer.Ordinal);
        foreach (var property in frame.EnumerateObject()) if (!names.Add(property.Name)) throw new InvalidDataException();
        if (names.Count != 11 || frame.GetProperty("version").GetInt32() != 2 || frame.GetProperty("runId").GetString() != _run ||
            frame.GetProperty("backend").GetString() != "native-lineage" || frame.GetProperty("guarantee").GetString() != GmWorkerBackendSelector.NativeGuarantee) throw new InvalidDataException();
        var state = frame.GetProperty("state").GetString();
        var reason = frame.GetProperty("reason").GetString() ?? throw new InvalidDataException();
        var complete = frame.GetProperty("cleanupComplete").GetBoolean();
        var retained = frame.GetProperty("authorityRetained").GetBoolean();
        var exitCode = frame.GetProperty("rootExitCode").GetInt32();
        _ = frame.GetProperty("rootSignal").GetInt32(); _ = frame.GetProperty("errno").GetInt32();
        if (complete == retained) throw new InvalidDataException();
        lock (_stateGate)
        {
            if (_terminal != null) throw new InvalidDataException();
            switch (state)
            {
                case "Ready" when _phase == 0 && !complete: _phase = 1; _ready.TrySetResult(); break;
                case "Started" when _phase == 1 && !complete: _phase = 2; _startedHost.TrySetResult(); break;
                case "Stopping" when _phase < 3 && !complete: _phase = 3; _sealed = true; break;
                case "Uncertain":
                    Lose(reason); _sealed = true;
                    if (complete) _terminal = new(_run, GmWorkerBackend.NativeLineage, GmWorkerBackendSelector.NativeGuarantee,
                        GmWorkerStopState.Uncertain, reason, true, false, exitCode < 0 ? null : exitCode);
                    break;
                case "StoppedWithinScope" when _phase == 3 && _sealed && complete:
                    _terminal = new(_run, GmWorkerBackend.NativeLineage, GmWorkerBackendSelector.NativeGuarantee,
                        GmWorkerStopState.StoppedWithinScope, reason, true, false, exitCode < 0 ? null : exitCode);
                    break;
                default: throw new InvalidDataException();
            }
        }
    }

    private async Task SendControlAsync(char command)
    {
        await _controlGate.WaitAsync();
        try { await _supervisor.StandardInput.WriteAsync(command); await _supervisor.StandardInput.FlushAsync(); }
        finally { _controlGate.Release(); }
    }

    internal override async Task<GmWorkerStopEvidence> StopAndObserveAsync()
    {
        await _stopGate.WaitAsync();
        try
        {
            using var observation = new CancellationTokenSource(TimeSpan.FromSeconds(5));
            if (!_processStarted)
                return new(_run, GmWorkerBackend.NativeLineage, GmWorkerBackendSelector.NativeGuarantee, GmWorkerStopState.Uncertain, "start-unconfirmed", false, true, null);
            if (!_stopped)
            {
                _stopped = true;
                try
                {
                    if (!_supervisor.HasExited)
                    {
                        _stopControl = SendControlAsync('S');
                        await _stopControl.WaitAsync(observation.Token);
                    }
                }
                catch { Lose("control-lost"); }
            }
            try
            {
                await Task.WhenAll(_supervisorExit!, _statusReader!, _helperErrorDrain!, _hostExit ?? Task.CompletedTask).WaitAsync(observation.Token);
            }
            catch { Lose("observation-timeout-or-io"); }
            lock (_stateGate)
            {
                if (_uncertainty == null && _terminal?.State == GmWorkerStopState.StoppedWithinScope &&
                    _supervisorExit!.IsCompletedSuccessfully && _supervisor.ExitCode == 0)
                    return _terminal;
                return new(_run, GmWorkerBackend.NativeLineage, GmWorkerBackendSelector.NativeGuarantee, GmWorkerStopState.Uncertain,
                    _uncertainty ?? "stop-unconfirmed", _terminal?.CleanupComplete == true && _supervisorExit!.IsCompletedSuccessfully,
                    !_supervisorExit!.IsCompletedSuccessfully, _terminal?.RootExitCode);
            }
        }
        finally { _stopGate.Release(); }
    }

    internal override async Task<GmWorkerOwnedOutputs> SettleOutputsAsync()
    {
        try
        {
            _outputObservation ??= ObserveOwnedOutputCompletionAsync();
            await _outputObservation.WaitAsync(TimeSpan.FromSeconds(5));
            lock (_stateGate)
            {
                if (_uncertainty != null || _terminal?.State != GmWorkerStopState.StoppedWithinScope)
                    throw new InvalidOperationException("Native outputs cannot settle without matching scoped retirement.");
                _outputsSettled = true;
            }
            return new(await _outputDrain, await _errorDrain);
        }
        catch { Lose("owned-output-observation-failed"); throw; }
    }

    private async Task ObserveOwnedOutputCompletionAsync()
    {
        await Task.WhenAll(_outputDrain, _errorDrain);
        if (_observationFault != null) await _observationFault.ObserveOutputsAsync();
    }

    private async Task<string> CaptureHostOutputAsync(Stream stream)
    {
        using var reader = new StreamReader(stream, Encoding.UTF8, detectEncodingFromByteOrderMarks: true, leaveOpen: true);
        try { return await GmWorkerBridgePool.CaptureProcessOutputAsync(reader); }
        catch { Lose("output-drain-lost"); throw; }
    }

    private async Task<string> DrainAsync(Stream stream)
    {
        using var reader = new StreamReader(stream, Encoding.UTF8, detectEncodingFromByteOrderMarks: true, leaveOpen: true);
        var buffer = new char[1024]; var tail = new StringBuilder();
        try
        {
            int n;
            while ((n = await reader.ReadAsync(buffer)) != 0)
            {
                tail.Append(buffer, 0, n);
                if (tail.Length > 8192) tail.Remove(0, tail.Length - 8192);
            }
        }
        catch { Lose("output-drain-lost"); }
        return tail.ToString();
    }

    private void CloseBootstrap()
    {
        _bootstrap?.Dispose(); _bootstrap = null; _listener.Dispose();
        _output?.DisposeLocalCopyOfClientHandle(); _error?.DisposeLocalCopyOfClientHandle();
    }

    public override async ValueTask DisposeAsync()
    {
        if (_disposed) return;
        // Disposal cannot manufacture stop evidence or abandon a live original owner.
        lock (_stateGate)
        {
            if (_uncertainty != null || _terminal?.State != GmWorkerStopState.StoppedWithinScope ||
                !_processStarted || !_supervisorExit!.IsCompletedSuccessfully || _supervisor.ExitCode != 0 ||
                !_statusReader!.IsCompletedSuccessfully || !_outputsSettled || !_outputDrain.IsCompletedSuccessfully || !_errorDrain.IsCompletedSuccessfully)
                throw new InvalidOperationException("Native launch retains an unconfirmed owner; output EOF and helper exit cannot retire it.");
        }
        _observationFault?.BeforeDispose();
        // A filesystem failure retains the still-disposable original resources.
        // Repeat deletion is harmless after a partially successful attempt.
        if (Directory.Exists(_bootstrapDirectory)) Directory.Delete(_bootstrapDirectory, recursive: true);
        CloseBootstrap();
        await Task.WhenAll(_outputDrain, _errorDrain);
        if (_hostExit != null) await _hostExit;
        _hostPidfd?.Dispose(); _terminalMaster?.Dispose(); _supervisor.Dispose(); _output?.Dispose(); _error?.Dispose();
        _stopGate.Dispose(); _controlGate.Dispose();
        _disposed = true;
    }
}
