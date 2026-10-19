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
    /// <summary>
    /// Evaluates already observed finite launch evidence without creating native evidence.
    /// </summary>
    /// <param name="observation">
    /// The retained owner's current observations.
    /// </param>
    /// <returns>
    /// The current admission decision.
    /// </returns>
    internal static NativeFiniteStartDecision EvaluateFiniteStart(NativeFiniteObservation observation)
    {
        if (observation.Uncertainty != null || observation.Terminal?.State == GmWorkerStopState.Uncertain ||
            (observation.SupervisorExited && observation.SupervisorExitCode is int exitCode && exitCode != 0))
            return NativeFiniteStartDecision.Rejected;
        if (!observation.Released) return NativeFiniteStartDecision.Pending;
        if (observation.Started) return NativeFiniteStartDecision.Started;
        return HasCompleteFiniteRetirement(observation) ? NativeFiniteStartDecision.Completed : NativeFiniteStartDecision.Pending;
    }

    /// <summary>
    /// Determines whether a closed stop pipe agrees with complete natural retirement.
    /// </summary>
    /// <param name="observation">
    /// The retained owner's current observations.
    /// </param>
    /// <param name="controlSettled">
    /// Whether the original control write has finished; a timed-out pending write must retain uncertainty.
    /// </param>
    /// <returns>
    /// <see langword="true"/> only for complete natural retirement without uncertainty; otherwise <see langword="false"/>.
    /// </returns>
    internal static bool CanAcceptFiniteClosedControl(NativeFiniteObservation observation, bool controlSettled = true) =>
        controlSettled && HasCompleteFiniteRetirement(observation) && observation.Terminal!.Reason == "root-exited";

    /// <summary>
    /// Checks the complete positive retirement conjunction independently of Started.
    /// </summary>
    /// <param name="observation">
    /// The original owner's observations, including sticky uncertainty.
    /// </param>
    /// <returns>
    /// <see langword="true"/> when all required retirement observations agree; otherwise <see langword="false"/>.
    /// </returns>
    private static bool HasCompleteFiniteRetirement(NativeFiniteObservation observation) =>
        observation.Released && observation.Uncertainty == null && observation.RootExited &&
        observation.SupervisorExited && observation.SupervisorExitCode == 0 &&
        observation.StatusCompleted && observation.HelperErrorCompleted &&
        observation.Terminal is { State: GmWorkerStopState.StoppedWithinScope, CleanupComplete: true,
            AuthorityRetained: false, RootExitCode: not null };

    /// <summary>
    /// Waits for finite admission or rejection while preserving the caller's cancellation.
    /// </summary>
    /// <param name="snapshot">
    /// Reads the current observations from the original owner.
    /// </param>
    /// <param name="started">
    /// The existing Started notification.
    /// </param>
    /// <param name="retirementObserved">
    /// Completes after original root, helper and status observations settle.
    /// </param>
    /// <param name="authorityLost">
    /// Completes when the original owner loses authority.
    /// </param>
    /// <param name="cancellationToken">
    /// Cancels this wait without changing the owner's observations.
    /// </param>
    /// <returns>
    /// The observed admission decision; cancellation propagates to the caller.
    /// </returns>
    internal static async Task<NativeFiniteStartDecision> AwaitFiniteStartAsync(
        Func<NativeFiniteObservation> snapshot, Task started, Task retirementObserved,
        Task<string> authorityLost, CancellationToken cancellationToken)
    {
        var pending = new List<Task> { started, retirementObserved, authorityLost };
        while (true)
        {
            cancellationToken.ThrowIfCancellationRequested();
            if (authorityLost.IsCompleted) return NativeFiniteStartDecision.Rejected;
            var decision = EvaluateFiniteStart(snapshot());
            if (decision != NativeFiniteStartDecision.Pending) return decision;
            var notification = await Task.WhenAny(pending).WaitAsync(cancellationToken);
            try { await notification; }
            catch { return NativeFiniteStartDecision.Rejected; }
            pending.Remove(notification);
        }
    }

    /// <summary>
    /// Builds the pipe supervisor command while retaining literal child arguments.
    /// </summary>
    /// <param name="host">
    /// The configured child executable, working directory, arguments and environment.
    /// </param>
    /// <param name="executable">
    /// The validated supervisor executable path.
    /// </param>
    /// <param name="runId">
    /// The native run identity.
    /// </param>
    /// <param name="socketPath">
    /// The owner's private bootstrap socket path.
    /// </param>
    /// <param name="finitePipe">
    /// Whether this launch consumes the child's complete configured environment.
    /// </param>
    /// <returns>
    /// A separate supervisor start configuration.
    /// </returns>
    internal static ProcessStartInfo CreatePipeSupervisorStartInfo(
        ProcessStartInfo host, string executable, string runId, string socketPath, bool finitePipe)
    {
        var start = new ProcessStartInfo(executable)
        {
            UseShellExecute = false, RedirectStandardInput = true, RedirectStandardOutput = true,
            RedirectStandardError = true, WorkingDirectory = host.WorkingDirectory, CreateNoWindow = true
        };
        if (finitePipe)
        {
            start.Environment.Clear();
            foreach (var item in host.Environment) start.Environment[item.Key] = item.Value;
        }
        foreach (var arg in new[] { runId, "100", "2500", "--host-v2", socketPath, host.FileName })
            start.ArgumentList.Add(arg);
        foreach (var arg in host.ArgumentList) start.ArgumentList.Add(arg);
        return start;
    }

    /// <summary>
    /// Reads a child pipe without taking ownership of the supplied stream.
    /// </summary>
    /// <param name="input">
    /// The readable child stream, left open after reading.
    /// </param>
    /// <param name="finitePipe">
    /// Whether to retain the complete stream instead of the existing bridge capture limit.
    /// </param>
    /// <returns>
    /// The decoded output; stream errors propagate to the owner.
    /// </returns>
    internal static async Task<string> ReadPipeOutputAsync(Stream input, bool finitePipe)
    {
        using var reader = new StreamReader(input, Encoding.UTF8, detectEncodingFromByteOrderMarks: true, leaveOpen: true);
        return finitePipe ? await reader.ReadToEndAsync() : await GmWorkerBridgePool.CaptureProcessOutputAsync(reader);
    }


    private readonly bool _terminalMode;
    private readonly bool _finitePipe;
    private bool _finiteReleased;
    private Task? _terminalIoSettlement;
    private readonly TaskCompletionSource<string> _authorityLost = new(TaskCreationOptions.RunContinuationsAsynchronously);
    internal Task<string> AuthorityLost => _authorityLost.Task;
    internal string? Uncertainty { get { lock(_stateGate)return _uncertainty; } }
    internal void RegisterTerminalSettlement(Task task) {
        lock(_stateGate) { if(!_terminalMode || _terminalIoSettlement!=null)throw new InvalidOperationException("Terminal settlement already bound."); _terminalIoSettlement=task; }
    }
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

    /// <summary>
    /// Acquires the private bootstrap and output resources of one original owner.
    /// </summary>
    /// <param name="durable">
    /// Optional durable worker execution; finite test roots have none.
    /// </param>
    /// <param name="terminal">
    /// Whether the owner uses terminal transport instead of separate host pipes.
    /// </param>
    /// <param name="terminalRunId">
    /// Optional existing terminal run identity; otherwise a fresh identity is created.
    /// </param>
    /// <param name="finitePipe">
    /// Enables finite pipe environment, output and admission policy; defaults to the existing worker behavior.
    /// </param>
    protected NativeLineageOwner(GmWorkerDurableExecution? durable, bool terminal = false,string? terminalRunId=null, bool finitePipe = false)
    {
        _terminalMode = terminal; _finitePipe = finitePipe; _durable = durable; _run = durable?.Identity.RunId ?? terminalRunId ?? Guid.NewGuid().ToString("N");
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

    /// <summary>
    /// Gets the private bootstrap directory retained by this owner until successful disposal.
    /// </summary>
    internal string BootstrapDirectory => _bootstrapDirectory;

    /// <summary>
    /// Starts a finite pipe child through the existing native bootstrap and retained identity.
    /// </summary>
    /// <param name="startInfo">
    /// The absolute child executable, literal arguments, working directory and child environment; shell execution is forbidden.
    /// </param>
    /// <param name="packageDirectory">
    /// The absolute directory of an explicitly prepared native package.
    /// </param>
    /// <param name="observeHeldRoot">
    /// Records the original owner and bound root synchronously before the single exec release.
    /// </param>
    /// <param name="cancellationToken">
    /// Cancels startup; a failure after allocation carries the original owner in the launch exception.
    /// </param>
    /// <returns>
    /// The retained owner after Started or a fully observed finite retirement.
    /// </returns>
    internal static async Task<NativeLineageOwner> StartFinitePipeAsync(ProcessStartInfo startInfo,
        string packageDirectory, Action<NativeLineageOwner, int> observeHeldRoot, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(startInfo);
        ArgumentNullException.ThrowIfNull(observeHeldRoot);
        cancellationToken.ThrowIfCancellationRequested();
        if (!Path.IsPathFullyQualified(startInfo.FileName) || startInfo.UseShellExecute || startInfo.Arguments.Length != 0)
            throw new InvalidDataException("Finite native child requires an absolute executable and ArgumentList without shell execution.");
        if (!Path.IsPathFullyQualified(packageDirectory))
            throw new InvalidDataException("Finite native package directory must be absolute.");
        var executable = GmWorkerNativePackage.Validate(packageDirectory);
        var owner = new NativeLineageOwner(null, finitePipe: true);
        try
        {
            await owner.StartAsync(startInfo, executable, cancellationToken,
                observeHeldRoot: pid => observeHeldRoot(owner, pid));
            return owner;
        }
        catch (Exception error)
        {
            owner.Lose("partial-finite-launch");
            owner.CloseBootstrap();
            throw new GmWorkerOwnedLaunchException("Finite launch retains its original owner.", owner, error);
        }
    }

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

    internal static async Task<NativeLineageOwner> StartTerminalAsync(ProcessStartInfo fixture, string executable, int columns, int rows, CancellationToken token, Action<int>? observeHeldRoot = null)
    {
        var owner = new NativeLineageOwner(null, terminal: true);
        try { await owner.StartAsync(fixture, executable, token, columns, rows, observeHeldRoot); return owner; }
        catch (Exception ex) { owner.Lose("partial-terminal-launch"); owner.CloseBootstrap(); throw new GmWorkerOwnedLaunchException("Terminal partial launch retains owner.", owner, ex); }
    }

    private int _terminalRelease;
    private long _heldDeadline;
    internal TimeSpan HeldTerminalRemaining=>TimeSpan.FromSeconds(Math.Max(0,(_heldDeadline-Stopwatch.GetTimestamp())/(double)Stopwatch.Frequency));
    internal void RequireOriginalHeldTerminal()
    {
        if(!_terminalMode || _terminalRelease!=0 || _bootstrap==null || !AuthorityValid() || HeldTerminalRemaining<=TimeSpan.Zero)
            throw new IOException("Original held terminal authority unavailable.");
        _identity!.EnsureLive();
    }
    internal SafeFileHandle DuplicateHeldTerminalPidfd()
    {
        RequireOriginalHeldTerminal();
        var fd=DuplicatePidfd(_hostPidfd!,1030,3);
        if(fd<0)throw new IOException("Original held pidfd duplicate failed.");
        var copy=new SafeFileHandle((IntPtr)fd,true);
        try { RequireOriginalHeldTerminal();return copy; } catch {copy.Dispose();throw;}
    }
    [System.Runtime.InteropServices.DllImport("libc",EntryPoint="fcntl",SetLastError=true)]
    private static extern int DuplicatePidfd(SafeFileHandle descriptor,int command,int argument);
    internal static async Task<NativeLineageOwner> PrepareTerminalAsync(ProcessStartInfo fixture,string executable,string runId,int columns,int rows,CancellationToken token,Action<int>? held=null)
    {
        if(!Guid.TryParseExact(runId,"N",out var id) || id==Guid.Empty)throw new InvalidDataException("Terminal run identity is invalid.");
        var owner=new NativeLineageOwner(null,true,runId);
        try {await owner.StartAsync(fixture,executable,token,columns,rows,held,true);return owner;}
        catch(Exception ex){owner.Lose("partial-terminal-prepare");owner.CloseBootstrap();throw new GmWorkerOwnedLaunchException("Terminal prepare retains original owner.",owner,ex);}
    }
    internal async Task ReleaseTerminalAsync(CancellationToken token)
    {
        if(!_terminalMode || Interlocked.Exchange(ref _terminalRelease,1)!=0)throw new InvalidOperationException("Original terminal release is single-use.");
        try {
            token.ThrowIfCancellationRequested();
            if(Stopwatch.GetTimestamp()>=_heldDeadline || _bootstrap==null || !AuthorityValid())throw new IOException("Original held terminal expired or lost authority.");
            _identity!.EnsureLive();GmWorkerNativeDescriptors.Send(_bootstrap,"A1:"+_run);
            _bootstrap.Dispose();_bootstrap=null;
            await _startedHost.Task.WaitAsync(TimeSpan.FromSeconds(5),token);_identity.EnsureLive();
        }catch{Lose("terminal-release-unconfirmed");CloseBootstrap();throw;}
    }

    /// <summary>
    /// Starts the original supervisor and binds the root identity before releasing exec.
    /// </summary>
    /// <param name="host">
    /// The child command and working directory.
    /// </param>
    /// <param name="executable">
    /// The validated supervisor executable.
    /// </param>
    /// <param name="cancellationToken">
    /// Cancels bootstrap without replacing the retained owner.
    /// </param>
    /// <param name="columns">
    /// Initial terminal columns; ignored for pipe launches.
    /// </param>
    /// <param name="rows">
    /// Initial terminal rows; ignored for pipe launches.
    /// </param>
    /// <param name="observeHeldRoot">
    /// Optional synchronous observation of the bound root before release.
    /// </param>
    /// <param name="holdTerminal">
    /// Whether to return a held terminal for a later explicit release.
    /// </param>
    /// <returns>
    /// Completion after admission or after the requested held-terminal preparation.
    /// </returns>
    private async Task StartAsync(ProcessStartInfo host, string executable, CancellationToken cancellationToken, int columns = 80, int rows = 25, Action<int>? observeHeldRoot = null,bool holdTerminal=false)
    {
        ProcessStartInfo start;
        if (_terminalMode)
        {
            start = new ProcessStartInfo(executable)
            {
                UseShellExecute = false, RedirectStandardInput = true, RedirectStandardOutput = true,
                RedirectStandardError = true, WorkingDirectory = host.WorkingDirectory, CreateNoWindow = true
            };
            start.Environment.Clear();
            foreach (var item in host.Environment) start.Environment[item.Key] = item.Value;
            foreach (var arg in new[] { _run, "100", "2500", "--terminal-v1", Path.Combine(_bootstrapDirectory, "owner") })
                start.ArgumentList.Add(arg);
            start.ArgumentList.Add(columns.ToString(System.Globalization.CultureInfo.InvariantCulture));
            start.ArgumentList.Add(rows.ToString(System.Globalization.CultureInfo.InvariantCulture));
            start.ArgumentList.Add(host.FileName);
            foreach (var arg in host.ArgumentList) start.ArgumentList.Add(arg);
        }
        else start = CreatePipeSupervisorStartInfo(host, executable, _run, Path.Combine(_bootstrapDirectory, "owner"), _finitePipe);
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
        _heldDeadline=Stopwatch.GetTimestamp()+5*Stopwatch.Frequency;
        await SendControlAsync('L');
        var rights = await GmWorkerNativeDescriptors.ReceiveAsync(_bootstrap, (_terminalMode ? "B1:" : "B2:") + _run, _terminalMode ? 2 : 1, deadline.Token);
        _hostPidfd = rights[0];
        if (_terminalMode) _terminalMaster = rights[1];
        _identity = GmWorkerHostIdentity.FromTransferredPidfd(_supervisor, _hostPidfd, AuthorityValid);
        _hostExit = ObserveHostExitAsync();
        observeHeldRoot?.Invoke(_identity.ProcessId);
        if(holdTerminal)return;
        GmWorkerNativeDescriptors.Send(_bootstrap, (_terminalMode ? "A1:" : "A2:") + _run);
        if (_finitePipe) { lock (_stateGate) _finiteReleased = true; }
        _bootstrap.Dispose(); _bootstrap = null;
        if (_finitePipe)
        {
            var retirement = Task.WhenAll(_supervisorExit, _statusReader, _helperErrorDrain, _hostExit);
            var decision = await AwaitFiniteStartAsync(ReadFiniteObservation, _startedHost.Task,
                retirement, _authorityLost.Task, deadline.Token);
            if (decision == NativeFiniteStartDecision.Rejected)
                throw new InvalidDataException("Finite native admission lost its original observations.");
            return;
        }
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
        _authorityLost.TrySetResult(reason);
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

    /// <summary>
    /// Reads the original owner's validated observations for finite policy.
    /// </summary>
    /// <returns>
    /// A snapshot whose terminal evidence comes only from the existing status parser.
    /// </returns>
    private NativeFiniteObservation ReadFiniteObservation()
    {
        lock (_stateGate)
        {
            var supervisorExited = _supervisorExit?.IsCompletedSuccessfully == true;
            return new(_finiteReleased, _startedHost.Task.IsCompletedSuccessfully,
                _hostExit?.IsCompletedSuccessfully == true, supervisorExited,
                supervisorExited ? _supervisor.ExitCode : null,
                _statusReader?.IsCompletedSuccessfully == true, _helperErrorDrain?.IsCompletedSuccessfully == true,
                _terminal, _uncertainty);
        }
    }

    /// <summary>
    /// Requests scoped stop and observes the retained root, supervisor and status streams.
    /// Finite roots already exiting may complete their natural retirement without another stop command.
    /// </summary>
    /// <returns>
    /// Positive scoped retirement, or uncertainty without replacing or discarding the original owner.
    /// </returns>
    internal override async Task<GmWorkerStopEvidence> StopAndObserveAsync()
    {
        await _stopGate.WaitAsync();
        try
        {
            using var observation = new CancellationTokenSource(TimeSpan.FromSeconds(5));
            if (!_processStarted)
                return new(_run, GmWorkerBackend.NativeLineage, GmWorkerBackendSelector.NativeGuarantee, GmWorkerStopState.Uncertain, "start-unconfirmed", false, true, null);
            var finiteControlFailure = false;
            if (!_stopped)
            {
                _stopped = true;
                try
                {
                    if (!_supervisor.HasExited && !(_finitePipe && _hostExit?.IsCompletedSuccessfully == true))
                    {
                        _stopControl = SendControlAsync('S');
                        await _stopControl.WaitAsync(observation.Token);
                    }
                }
                catch
                {
                    if (_finitePipe) finiteControlFailure = true;
                    else Lose("control-lost");
                }
            }
            try
            {
                await Task.WhenAll(_supervisorExit!, _statusReader!, _helperErrorDrain!, _hostExit ?? Task.CompletedTask).WaitAsync(observation.Token);
            }
            catch { Lose("observation-timeout-or-io"); }
            if (finiteControlFailure && !CanAcceptFiniteClosedControl(ReadFiniteObservation(), _stopControl?.IsCompleted == true)) Lose("control-lost");
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
        if (_terminalMode) await (_terminalIoSettlement ?? throw new InvalidOperationException("Original PTY settlement not bound."));
        await Task.WhenAll(_outputDrain, _errorDrain);
        if (_observationFault != null) await _observationFault.ObserveOutputsAsync();
    }

    /// <summary>
    /// Drains one owned host pipe and records any loss on the original owner.
    /// </summary>
    /// <param name="stream">
    /// The owned readable pipe; its handle remains owned by this launch.
    /// </param>
    /// <returns>
    /// Captured output after EOF, or a propagated stream failure.
    /// </returns>
    private async Task<string> CaptureHostOutputAsync(Stream stream)
    {
        try { return await ReadPipeOutputAsync(stream, _finitePipe); }
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
                !_statusReader!.IsCompletedSuccessfully || (_terminalMode && _terminalIoSettlement?.IsCompletedSuccessfully != true) || !_outputsSettled || !_outputDrain.IsCompletedSuccessfully || !_errorDrain.IsCompletedSuccessfully)
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

/// <summary>
/// Describes the admission outcome of an already released finite child.
/// </summary>
internal enum NativeFiniteStartDecision
{
    /// <summary>
    /// Required observations have not yet arrived.
    /// </summary>
    Pending,
    /// <summary>
    /// The original owner observed a successful Started notification.
    /// </summary>
    Started,
    /// <summary>
    /// The child retired completely without requiring a Started notification.
    /// </summary>
    Completed,
    /// <summary>
    /// An observed failure prevents admission.
    /// </summary>
    Rejected
}

/// <summary>
/// Carries immutable observations used by finite admission and stop policy.
/// The owner supplies validated native evidence; this value does not validate its provenance.
/// </summary>
/// <param name="Released">
/// Whether the original pre-exec gate has been released.
/// </param>
/// <param name="Started">
/// Whether the owner accepted the Started notification.
/// </param>
/// <param name="RootExited">
/// Whether the original root exit observation completed successfully.
/// </param>
/// <param name="SupervisorExited">
/// Whether the original supervisor exit observation completed successfully.
/// </param>
/// <param name="SupervisorExitCode">
/// The observed helper exit code, or null before it is available.
/// </param>
/// <param name="StatusCompleted">
/// Whether the dedicated status reader completed successfully.
/// </param>
/// <param name="HelperErrorCompleted">
/// Whether the helper error stream finished.
/// </param>
/// <param name="Terminal">
/// The terminal evidence accepted by the owner, or null before terminal evidence.
/// </param>
/// <param name="Uncertainty">
/// The sticky loss reason, or null when none has been recorded.
/// </param>
internal sealed record NativeFiniteObservation(bool Released, bool Started,
    bool RootExited, bool SupervisorExited, int? SupervisorExitCode,
    bool StatusCompleted, bool HelperErrorCompleted,
    GmWorkerStopEvidence? Terminal, string? Uncertainty);
