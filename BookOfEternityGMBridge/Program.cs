using System.Diagnostics;
using System.IO.Pipes;
using System.Runtime.InteropServices;
using System.Text;
using System.Text.Json;
using System.Text.Json.Serialization;
using BookOfEternityClient.Core;
using BookOfEternityClient.Configuration;
using BookOfEternityClient.Services.GmWorkers;
using BookOfEternityClient.Services.GmRuntime;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Win32.SafeHandles;

namespace BookOfEternityGMBridge;

internal static class Program
{
    private static async Task<int> Main(string[] args)
    {
        if (!TryParseHostArgs(args, out var sessionPath, out var pipeName))
        {
            PrintUsage();
            return 1;
        }

        try
        {
            var neutral = Array.IndexOf(args, "--neutralPackage");
            var launch = neutral >= 0 && neutral+1<args.Length ? NeutralTerminalLaunch.Create(args[neutral+1],Path.GetTempPath()) : null;
            using var host = new BridgeHost(launch?.Scratch ?? sessionPath, pipeName);
            if(launch!=null)host.ConfigureNeutral(launch);
            return await host.RunAsync();
        }
        catch (Exception ex)
        {
            Console.Error.WriteLine("Bridge fatal error:");
            Console.Error.WriteLine(ex.ToString());
            return 1;
        }
    }

    private static bool TryParseHostArgs(string[] args, out string sessionPath, out string pipeName)
    {
        sessionPath = string.Empty;
        pipeName = string.Empty;

        for (var i = 0; i < args.Length; i++)
        {
            switch (args[i])
            {
                case "--host":
                    continue;
                case "--sessionPath" when i + 1 < args.Length:
                    sessionPath = args[++i];
                    continue;
                case "--pipeName" when i + 1 < args.Length:
                    pipeName = args[++i];
                    continue;
            }
        }

        if (string.IsNullOrWhiteSpace(sessionPath))
            return false;

        sessionPath = Path.GetFullPath(sessionPath);
        if (string.IsNullOrWhiteSpace(pipeName))
            pipeName = "boe-gmbridge-" + Guid.NewGuid().ToString("N");

        return true;
    }

    private static void PrintUsage()
    {
        Console.WriteLine("BookOfEternityGMBridge");
        Console.WriteLine("Usage:");
        Console.WriteLine("  BookOfEternityGMBridge --host --sessionPath <path> [--pipeName <pipe>]");
    }
}

internal sealed partial class BridgeHost : IDisposable
{
    private static readonly JsonSerializerOptions JsonOpts = new()
    {
        WriteIndented = true,
        DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull,
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
        PropertyNameCaseInsensitive = true,
        Converters = { new JsonStringEnumConverter(JsonNamingPolicy.KebabCaseLower) }
    };
    private static readonly JsonSerializerOptions PipeJsonOpts = new()
    {
        WriteIndented = false,
        DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull,
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
        PropertyNameCaseInsensitive = true,
        Converters = { new JsonStringEnumConverter(JsonNamingPolicy.KebabCaseLower) }
    };

    private readonly string _sessionPath;
    private readonly string _clientRoot;
    private readonly string _repoRoot;
    private readonly string _pipeName;
    private readonly string _controlDir;
    private readonly string _statusPath;
    private readonly string _configPath;
    private readonly CancellationTokenSource _cts = new();
    private readonly object _sync = new();
    private readonly SemaphoreSlim _ptyWriteLock = new(1, 1);
    private readonly SemaphoreSlim _shellLifecycleLock = new(1, 1);
    private NeutralTerminalLaunch? _neutralLaunch;
    internal void ConfigureNeutral(NeutralTerminalLaunch launch) {
        if(_sessionPath!=launch.Scratch)throw new InvalidOperationException("Neutral host requires its fresh admitted scratch."); _neutralLaunch=launch;
        File.WriteAllText(_configPath, JsonSerializer.Serialize(new { GmCliInputProfile = new { IdleMarker="NEUTRAL READY", PromptPrefix="> ", WorkingMarker="NEUTRAL WORKING", ObservationTimeoutMilliseconds=1500 } }));
    }
    private InputLifetime? _inputLifetime;
    private bool _inputClosed;
    private bool _writeGateDisposed;
    private static readonly TimeSpan InputDrainTimeout = TimeSpan.FromSeconds(5);

    // Local stream/task identity only; never a persistent run, generation or ownership grant.
    private sealed class InputLifetime(Stream input, CancellationTokenSource cancellation)
    {
        public string Id { get; } = Guid.NewGuid().ToString("N");
        public bool ManualTakeover;
        public long ManualObservationAfter = -1;
        public CancellationTokenSource? BootstrapCancellation;
        public readonly List<Task> PromptTasks = new();
        public Stream Input { get; } = input;
        public CancellationTokenSource Cancellation { get; } = cancellation;
        public CancellationToken Token { get; } = cancellation.Token;
        public bool Revoked;
        public int PendingWrites;
        public TaskCompletionSource<bool>? WritesDrained;
        public Task CancellationTask = Task.CompletedTask;
        public Task? DrainTask;
    }

    private sealed class InputLifetimeUnavailableException() : InvalidOperationException("PTY input lifetime is no longer active.");
    private sealed class PtyInputWriteException(Exception inner) : IOException(
        "PTY input write failed after it started; some bytes may have been delivered.", inner);
    private readonly StringBuilder _recentOutput = new();

    private IOwnedTerminalSession? _pty;
    private Task<TerminalStopEvidence>? _terminalStopTask;
    private Task? _terminalDisposeTask;
    private bool _terminalUncertain;
    private TerminalScreen? _terminalScreen;
    private TerminalViewObservation CaptureTerminalView()
    {
        lock (_sync) { var view = _terminalScreen?.Capture() ?? new("", 0, "", false);
            return view with { Reliable = view.Reliable && !_terminalUncertain && _pty?.AuthorityLost.IsCompleted != true && _pty?.RootExited.IsCompleted != true && _inputLifetime is { Revoked: false } input && input.Id == view.BindingId }; }
    }
    private Stream? _ptyInput;
    private Task? _outputPumpTask;
    private Task? _terminalAuthorityTask;
    private Task? _terminalRootTask;
    private Task? _keyboardPumpTask;
    private Task? _resizePumpTask;
    private CancellationTokenSource? _shellLoopCts;
    private long _outputVersion;
    private TaskCompletionSource<bool> _outputChanged = CreateOutputSignal();
    private BridgeStatus _status;

    public BridgeHost(string sessionPath, string pipeName)
    {
        _sessionPath = Path.GetFullPath(sessionPath);
        _clientRoot = Directory.GetParent(_sessionPath)?.FullName ?? _sessionPath;
        _repoRoot = ResolveRepoRoot(_clientRoot, Environment.CurrentDirectory, AppContext.BaseDirectory);
        _pipeName = pipeName;
        _controlDir = Path.Combine(_sessionPath, "game_state", "control");
        _statusPath = Path.Combine(_controlDir, "gm_bridge_status.json");
        _configPath = Path.Combine(_sessionPath, "config.json");
        Directory.CreateDirectory(_controlDir);

        _status = new BridgeStatus
        {
            Backend = "ConPTYBridge",
            State = "Starting",
            Ready = false,
            HelperPid = Environment.ProcessId,
            SessionPath = _sessionPath,
            PipeName = _pipeName,
            StartedAtUtc = DateTimeOffset.UtcNow.ToString("O"),
            UpdatedAtUtc = DateTimeOffset.UtcNow.ToString("O")
        };
    }

    public async Task<int> RunAsync()
    {
        if (OperatingSystem.IsWindows()) { NativeMethods.SetConsoleCP(65001); NativeMethods.SetConsoleOutputCP(65001); }
        Console.InputEncoding = Encoding.UTF8;
        Console.OutputEncoding = Encoding.UTF8;
        if (OperatingSystem.IsWindows()) EnableVirtualTerminalOutput();
        UpdateConsoleTitle();
        PrintBanner();

        try { await StartShellAsync(); } catch(OwnedTerminalStartException) { /* Original owner retained; keep diagnostics/control alive. */ }
        var serverTask = RunServerLoopAsync(_cts.Token);
        var controlKeys=!Console.IsInputRedirected;
        var previousControlKeys=controlKeys && Console.TreatControlCAsInput;
        if(controlKeys)Console.TreatControlCAsInput=true;

        try
        {
            while (!_cts.IsCancellationRequested)
            {
                IOwnedTerminalSession? exited;
                InputLifetime? observedInput;
                lock (_sync)
                {
                    exited = _pty?.RootExited.IsCompletedSuccessfully == true ? _pty : null;
                    observedInput = _inputLifetime;
                }
                if (exited != null)
                    await HandlePtyExitedAsync(exited, observedInput);

                await RefreshBridgeAutomationStateAsync();
                await Task.Delay(250, _cts.Token);
            }
        }
        catch (OperationCanceledException) when (_cts.IsCancellationRequested)
        {
            // normal shutdown
        }
        finally
        {
            try { await serverTask; } catch { /* ignored */ }
            try { await StopShellAsync(); } finally { if(controlKeys)Console.TreatControlCAsInput=previousControlKeys; }
            SafeDeleteStatusFile();
        }

        return 0;
    }

    private void PrintBanner()
    {
        Console.WriteLine();
        Console.WriteLine("==============================================");
        Console.WriteLine(" Book of Eternity GM Bridge");
        Console.WriteLine("==============================================");
        Console.WriteLine($"Session : {_sessionPath}");
        Console.WriteLine($"Client  : {_clientRoot}");
        Console.WriteLine($"Repo    : {_repoRoot}");
        Console.WriteLine($"Pipe    : {_pipeName}");
        Console.WriteLine("This window is the GM CLI host.");
        Console.WriteLine("You can type here manually at any time.");
        Console.WriteLine("Use `bookofeternity.ps1 ready` after the CLI is fully ready to receive prompts.");
        Console.WriteLine();
    }

    private async Task RunServerLoopAsync(CancellationToken cancellationToken)
    {
        var peers = new List<Task>();
        using var capacity = new SemaphoreSlim(16, 16);
        NamedPipeServerStream NewListener() => new(_pipeName,PipeDirection.InOut,NamedPipeServerStream.MaxAllowedServerInstances,PipeTransmissionMode.Byte,PipeOptions.Asynchronous);
        var listener=NewListener();
        try
        {
            while (!cancellationToken.IsCancellationRequested)
            {
                await capacity.WaitAsync(cancellationToken);
                var server=listener;
                try { await server.WaitForConnectionAsync(cancellationToken); }
                catch { server.Dispose(); capacity.Release(); throw; }
                // Keep the Unix pipe listener alive before a completed handler can dispose its peer.
                listener=NewListener();
                peers.RemoveAll(t => t.IsCompleted);
                peers.Add(ServePeerAsync(server));
            }
        }
        catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested) { }
        finally { listener.Dispose(); await Task.WhenAll(peers); }

        async Task ServePeerAsync(NamedPipeServerStream server)
        {
            using (server)
            using (var deadline = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken))
            {
                // Bound incomplete reads independently of dispatch; every accepted peer is joined.
                deadline.CancelAfter(TimeSpan.FromSeconds(3));
                try
                {
                    var request = await ReadMessageAsync<BridgeRequest>(server, deadline.Token) ?? new BridgeRequest();
                    deadline.CancelAfter(Timeout.InfiniteTimeSpan);
                    await ProcessConnectedRequestAsync(server, async () =>
                    {
                        var response = await HandleRequestAsync(request);
                        deadline.CancelAfter(TimeSpan.FromSeconds(3));
                        await WriteMessageAsync(server, response, deadline.Token);
                        if (response.ShutdownAfterResponse) _cts.Cancel();
                    }, deadline.Token);
                }
                catch (Exception) { /* The accepted peer owns a bounded read/response; no second unbounded write. */ }
                finally { capacity.Release(); }
            }
        }
    }

    private async Task<bool> ProcessConnectedRequestAsync(Stream server, Func<Task> processRequest, CancellationToken cancellationToken)
    {
        try
        {
            await processRequest();
        }
        catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
        {
            return false;
        }
        catch (Exception ex)
        {
            // The handler may throw before the normal response deadline is armed.
            using var errorDeadline = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
            errorDeadline.CancelAfter(TimeSpan.FromSeconds(3));
            await WriteMessageAsync(server, new BridgeResponse
            {
                Ok = false,
                Error = ex.Message,
                Status = SnapshotStatus()
            }, errorDeadline.Token);
        }
        return true;
    }

    private async Task<BridgeResponse> HandleRequestAsync(BridgeRequest request)
    {
        var command = (request.Command ?? string.Empty).Trim().ToLowerInvariant();
        if(_neutralLaunch!=null && command=="dispatchworkertask")return BridgeResponse.Failure("Neutral fixture has no worker or game-writing admission.",SnapshotStatus());
        await RefreshBridgeAutomationStateAsync();

        switch (command)
        {
            case "status":
                return BridgeResponse.Success(SnapshotStatus());

            case "diagnostics":
                return BridgeResponse.Success(SnapshotStatus(), SnapshotDiagnostics());

            case "setready":
                return SetReady(request.Ready ?? true);

            case "dispatchworkertask":
                return await DispatchWorkerTaskAsync(request);

            case "resize":
                IOwnedTerminalSession? resizedSession;
                lock (_sync) resizedSession = _pty;
                if (resizedSession == null) return BridgeResponse.Failure("No original terminal.", SnapshotStatus());
                await resizedSession.ResizeAsync(new(request.Columns ?? 80, request.Rows ?? 25), _cts.Token);
                return BridgeResponse.Success(SnapshotStatus());
            case "addtext":
                await WriteManualInputAsync(CaptureInputLifetime(), request.Text ?? string.Empty, _cts.Token);
                return BridgeResponse.Success(SnapshotStatus());
            case "sendenter":
                await WriteManualInputAsync(CaptureInputLifetime(), LoadBridgeConfig().GmCliInputProfile.SubmitSequence, _cts.Token);
                return BridgeResponse.Success(SnapshotStatus());
            case "dispatchprompt":
                return await DispatchPromptAsync(request);
            case "promptstatus":
                return QueryPrompt(request, false);
            case "cancelprompt":
                return QueryPrompt(request, true);

            case "restartshell":
            case "restartcli":
                await StartShellAsync();
                return BridgeResponse.Success(SnapshotStatus());

            case "stopterminal":
                if(_neutralLaunch==null)return BridgeResponse.Failure("Scoped terminal harness control requires fixed neutral admission.",SnapshotStatus());
                await StopShellAsync();return BridgeResponse.Success(SnapshotStatus());
            case "shutdown":
                lock (_sync)
                {
                    _status.Ready = false;
                    _status.State = "ShuttingDown";
                    _status.LastError = null;
                    WriteStatusFile();
                }

                return BridgeResponse.Shutdown(SnapshotStatus());

            default:
                return BridgeResponse.Failure($"Unknown bridge command '{request.Command}'.", SnapshotStatus());
        }
    }

    private async Task StartShellAsync()
    {
        await _shellLifecycleLock.WaitAsync(_cts.Token);
        try
        {
            lock (_sync)
                ObjectDisposedException.ThrowIf(_inputClosed, this);
            await StopShellCoreAsync();

            if (_neutralLaunch != null) {
                IOwnedTerminalSession neutralSession;
                try { neutralSession=await OwnedTerminalSessionFactory.StartNeutralAsync(_neutralLaunch, _cts.Token); }
                catch(OwnedTerminalStartException ex) { AttachOwnedTerminal(ex.Owner,Console.OpenStandardOutput()); MarkTerminalUncertain(); throw; }
                AttachOwnedTerminal(neutralSession, Console.OpenStandardOutput());
                return;
            }
            if (!OperatingSystem.IsWindows()) throw new PlatformNotSupportedException("Production main launch remains fenced; only fixed neutral terminal admission is available here.");
            var config = LoadBridgeConfig();
            var shellExe = ResolveShellExecutable();
            var shellArgs = BuildShellArguments(shellExe);
            var workingDirectory = ResolveGmBridgeShellWorkingDirectory(config.GmBridgeShellWorkingDirectory);
            var (width, height) = GetConsoleSize();
            ConPtySession pty;
            try { pty=ConPtySession.Start(shellExe,shellArgs,workingDirectory,width,height); }
            catch(OwnedTerminalStartException ex) { AttachOwnedTerminal(ex.Owner,Console.OpenStandardOutput()); MarkTerminalUncertain(); throw; }
            var outputWriter = Console.OpenStandardOutput();

            var input = AttachOwnedTerminal(pty, outputWriter);

            lock (_sync)
            {
                _status.ShellPid = pty.ProcessId;
                _status.CliProcessId = null;
                _status.CliLaunchCommand = config.GmCliLaunchCommand;
                _status.ShellWorkingDirectory = workingDirectory;
                _status.WorkerStatuses = GmWorkerBridgePool.BuildInitialStatuses(config.GmWorkerBridgeProfiles).ToList();
                _status.Ready = false;
                _status.State = "OperatorNotReady";
                _status.LastError = null;
                WriteStatusFile();
            }

            Console.WriteLine();
            Console.WriteLine($"[Bridge] Hosted PTY shell started (pid={pty.ProcessId}).");
            Console.WriteLine($"[Bridge] Working directory: {workingDirectory}");
            Console.WriteLine($"[Bridge] Shell command: {shellExe} {shellArgs}");
            if (string.IsNullOrWhiteSpace(config.GmCliLaunchCommand))
                Console.WriteLine("[Bridge] GmCliLaunchCommand is empty. Type your CLI launch command manually, then mark bridge ready.");
            else
            {
                Console.WriteLine($"[Bridge] Launch command: {config.GmCliLaunchCommand}");
                var bootstrap = BuildShellBootstrap(config.GmCliLaunchCommand);
                await Task.Delay(250, input.Token);
                await WriteShellBootstrapAsync(input, bootstrap, input.Token);
            }

        }
        finally { _shellLifecycleLock.Release(); }
    }

    // All admitted sessions consume the original writer/lifetime/pumps, never a second dispatcher.
    private InputLifetime AttachOwnedTerminal(IOwnedTerminalSession session, Stream output)
    {
        lock (_sync) { if (_pty != null || _terminalUncertain) throw new InvalidOperationException("Original terminal owner is retained."); _pty = session; }
        var shellLoopCts = new CancellationTokenSource();
        var input = BeginInputLifetime(session.InputWriter, shellLoopCts);
        lock (_sync) { _terminalScreen = new(input.Id); _promptScreenReader = () => { var view=CaptureTerminalView(); return view.Reliable ? view.Text : ""; }; }
        // Output has its own lifetime: revoking input must not discard final terminal bytes.
        _outputPumpTask = Task.Run(async () => { try { await PumpOutputAsync(session.OutputReader, output, CancellationToken.None); } catch { MarkTerminalUncertain(); throw; } });
        _terminalAuthorityTask = ObserveTerminalAuthorityAsync(session,input);
        _terminalRootTask = ObserveTerminalRootAsync(session,input);
        _keyboardPumpTask = Task.Run(() => PumpKeyboardAsync(input, ReadConsoleKeyAsync, shellLoopCts.Token));
        _resizePumpTask = Task.Run(() => PumpResizeAsync(shellLoopCts.Token));
        lock (_sync) { _status.ShellPid = session.Identity.RootPid; _status.Backend = session.Identity.Backend; _status.TerminalRunId=session.Identity.RunId; _status.TerminalGuarantee=session.Identity.Guarantee; _status.State = "OperatorNotReady"; WriteStatusFile(); }
        return input;
    }

    private async Task ObserveTerminalAuthorityAsync(IOwnedTerminalSession session, InputLifetime input)
    {
        try { await session.AuthorityLost.WaitAsync(input.Token); }
        catch(OperationCanceledException) when(input.Token.IsCancellationRequested) { return; }
        lock(_sync) { if(!ReferenceEquals(_pty,session)||!ReferenceEquals(_inputLifetime,input))return; MarkTerminalUncertain(); }
        RevokeInputLifetime(input);
    }

    private async Task ObserveTerminalRootAsync(IOwnedTerminalSession session, InputLifetime input)
    {
        try { await session.RootExited.WaitAsync(input.Token); }
        catch(OperationCanceledException) when(input.Token.IsCancellationRequested) { return; }
        catch { lock(_sync) { if(ReferenceEquals(_pty,session) && ReferenceEquals(_inputLifetime,input))MarkTerminalUncertain(); } }
        lock(_sync) {
            if(!ReferenceEquals(_pty,session)||!ReferenceEquals(_inputLifetime,input))return;
            _status.Ready=false;
        }
        // Exit withdraws only this original input binding; it is never scoped stop proof.
        RevokeInputLifetime(input);
    }

    private async Task StopShellAsync()
    {
        await _shellLifecycleLock.WaitAsync();
        try { await StopShellCoreAsync(); }
        finally { _shellLifecycleLock.Release(); }
    }

    private async Task StopShellCoreAsync()
    {
        InputLifetime? input;
        IOwnedTerminalSession? pty;
        lock (_sync)
        {
            input = _inputLifetime;
            pty = _pty;
        }
        if (input != null)
            RevokeInputLifetime(input);
        if (pty != null)
        {
            try
            {
                _terminalStopTask ??= ObserveScopedTerminalStopAsync(pty);
                var proof = await _terminalStopTask.WaitAsync(InputDrainTimeout);
                lock(_sync)_status.TerminalStop=proof;
                if (_terminalUncertain || proof.Identity != pty.Identity ||
                    proof.State != GmWorkerStopState.StoppedWithinScope || !proof.CleanupComplete || proof.AuthorityRetained)
                    throw new InvalidOperationException("Original terminal scoped stop is unconfirmed.");
            }
            catch (Exception ex)
            {
                MarkTerminalUncertain();
                throw new TimeoutException("Original terminal owner is retained as Uncertain.", ex);
            }
        }
        if (input == null)
        {
            if(pty!=null) { await Task.WhenAll(_outputPumpTask??Task.CompletedTask,_resizePumpTask??Task.CompletedTask).WaitAsync(InputDrainTimeout); await RetireTerminalHandlesAsync(pty); }
            return;
        }

        Task drain;
        lock (_sync)
        {
            drain = input.DrainTask ??= ObserveManagedDrainAsync(
                input.WritesDrained?.Task ?? Task.CompletedTask,
                _keyboardPumpTask ?? Task.CompletedTask,
                _terminalAuthorityTask ?? Task.CompletedTask,
                _terminalRootTask ?? Task.CompletedTask,
                _outputPumpTask ?? Task.CompletedTask,
                _resizePumpTask ?? Task.CompletedTask,
                input.CancellationTask,
                Task.WhenAll(input.PromptTasks));
        }
        try { await drain.WaitAsync(InputDrainTimeout); }
        catch (TimeoutException)
        {
            if (pty != null) MarkTerminalUncertain();
            lock (_sync)
            {
                if (ReferenceEquals(_inputLifetime, input))
                {
                    _status.Ready = false;
                    _status.LastInputWriteError ??= "PTY input lifetime cleanup is incomplete; replacement remains blocked.";
                    TryWriteInputStatus();
                }
            }
            throw; // Keep actual tasks, binding and CTS for the next stop attempt.
        }
        if (pty != null) await RetireTerminalHandlesAsync(pty);
        lock (_sync)
        {
            if (ReferenceEquals(_inputLifetime, input))
            {
                _inputLifetime = null;
                _shellLoopCts = null;
                _keyboardPumpTask = null;
                _terminalAuthorityTask = null;
                _terminalRootTask = null;
                _outputPumpTask = null;
                _resizePumpTask = null;
            }
        }
        input.Cancellation.Dispose();
    }

    private static async Task<TerminalStopEvidence> ObserveScopedTerminalStopAsync(IOwnedTerminalSession session) =>
        await session.StopAndObserveAsync(CancellationToken.None);

    private void MarkTerminalUncertain()
    {
        lock (_sync)
        {
            _terminalUncertain = true;
            _status.TerminalUncertain=true;
            _status.Ready = false;
            _status.State = "TerminalUncertain";
            _status.LastError = "Original terminal ownership or I/O settlement is uncertain; replacement remains blocked.";
            TryWriteInputStatus();
        }
    }

    private async Task RetireTerminalHandlesAsync(IOwnedTerminalSession session)
    {
        try
        {
            _terminalDisposeTask ??= session.DisposeAsync().AsTask();
            await _terminalDisposeTask.WaitAsync(InputDrainTimeout);
        }
        catch (Exception ex)
        {
            MarkTerminalUncertain();
            throw new TimeoutException("Original terminal resources remain retained.", ex);
        }
        lock (_sync)
        {
            if (!ReferenceEquals(_pty, session)) throw new InvalidOperationException("Terminal retirement identity changed.");
            _pty = null;
            _status.Ready=false;_status.State="TerminalStopped";_status.ShellPid=null;
            _ptyInput = null;
            _terminalStopTask = null;
            _terminalDisposeTask = null;
            _terminalScreen = null;
        }
    }

    private static async Task ObserveManagedDrainAsync(params Task[] tasks)
    {
        try { await Task.WhenAll(tasks); }
        catch { /* All actual tasks have now settled; write uncertainty is retained at its source. */ }
    }

    private InputLifetime BeginInputLifetime(Stream stream, CancellationTokenSource shellLoopCts)
    {
        lock (_sync)
        {
            ObjectDisposedException.ThrowIf(_inputClosed, this);
            if (_inputLifetime != null)
                throw new InvalidOperationException("Previous PTY input lifetime has not been drained.");
            var input = new InputLifetime(stream, shellLoopCts);
            _inputLifetime = input;
            _ptyInput = stream;
            _shellLoopCts = shellLoopCts;
            _status.LastInputWriteError = null;
            _status.InputBindingId = input.Id;
            return input;
        }
    }

    private InputLifetime CaptureInputLifetime()
    {
        lock (_sync)
        {
            var input = _inputLifetime;
            if (_inputClosed || input == null || input.Revoked || input.Token.IsCancellationRequested)
                throw new InputLifetimeUnavailableException();
            return input;
        }
    }

    private void RevokeInputLifetime(InputLifetime input)
    {
        lock (_sync)
        {
            if (!ReferenceEquals(_inputLifetime, input) || input.Revoked)
                return;
            input.Revoked = true;
            // CancelAsync marks the token now and runs callbacks asynchronously, never inline under _sync.
            input.CancellationTask = input.Cancellation.CancelAsync();
        }
    }

    private void CompletePromptDispatch(InputLifetime input, bool succeeded, long elapsedMilliseconds)
    {
        lock (_sync)
        {
            if (!ReferenceEquals(_inputLifetime, input))
                return;
            _status.LastPromptDispatchCompletedAtUtc = DateTimeOffset.UtcNow.ToString("O");
            _status.LastPromptDispatchElapsedMs = elapsedMilliseconds;
            _status.LastPromptDispatchState = succeeded ? "Completed" : "Failed";
            if (!string.Equals(_status.State, "DispatchFailed", StringComparison.Ordinal))
                _status.State = _status.Ready ? "Ready" : "OperatorNotReady";
            WriteStatusFile();
        }
    }

    private BridgeResponse SetReady(bool ready)
    {
        lock (_sync)
        {
            if (ready && (_terminalUncertain || _automaticInputPaused || _admittedPrompts != 0 || _inputLifetime == null ||
                _inputLifetime.Revoked || (_inputLifetime.ManualTakeover && PromptObservationVersion <= _inputLifetime.ManualObservationAfter) || !IsEmptyIdleView(LoadBridgeConfig().GmCliInputProfile.Snapshot(), _promptScreenReader())))
                return BridgeResponse.Failure("Fresh empty supported idle view is required; uncertain operations remain paused.", SnapshotStatus());
            if (ready) _inputLifetime!.ManualTakeover = false;
            _status.Ready = ready;
            _status.State = ready ? "Ready" : "OperatorNotReady";
            WriteStatusFile();
            return BridgeResponse.Success(SnapshotStatus());
        }
    }

    private void EnsureShellAlive()
    {
        lock (_sync)
        {
            if (_pty == null || _pty.RootExited.IsCompleted || _ptyInput == null)
                throw new InvalidOperationException("Hosted PTY shell is not running.");
        }
    }

    private Task RefreshBridgeAutomationStateAsync()
    {
        // Observation never acknowledges trust/update prompts or clears an uncertain operation.
        lock (_sync)
        {
            if (_terminalUncertain) { _status.Ready=false; _status.State="TerminalUncertain"; TryWriteInputStatus(); return Task.CompletedTask; }
            var profile = LoadBridgeConfig().GmCliInputProfile.Snapshot();
            if (_inputLifetime != null && !_inputLifetime.Revoked && !_inputLifetime.ManualTakeover &&
                !_automaticInputPaused && _admittedPrompts == 0 && IsEmptyIdleView(profile, _promptScreenReader()))
            {
                _status.Ready = true;
                _status.State = "Ready";
                TryWriteInputStatus();
            }
            else if (_admittedPrompts == 0)
            {
                _status.Ready = false;
                if (!_automaticInputPaused) _status.State = "OperatorNotReady";
                TryWriteInputStatus();
            }
        }
        return Task.CompletedTask;
    }

    private CliPromptReadiness ProbeCliPromptReadinessForDispatch()
    {
        var visibleText = ReadVisibleConsoleText();
        if (string.IsNullOrWhiteSpace(visibleText))
        {
            return IsCodexCliConfigured()
                ? CliPromptReadiness.NotReady("Codex CLI is not at an idle input prompt.")
                : CliPromptReadiness.Ready();
        }

        var normalized = NormalizeVisibleConsoleText(visibleText);
        if (IsCodexCliWorkingScreen(normalized))
        {
            return CliPromptReadiness.NotReady("Codex CLI is still working on a previous request.");
        }

        if (IsCodexCliUpdatePrompt(normalized))
        {
            return CliPromptReadiness.NotReady("Codex CLI is waiting at an update prompt.");
        }

        if (normalized.Contains("trust this", StringComparison.OrdinalIgnoreCase) ||
            normalized.Contains("Do you trust", StringComparison.OrdinalIgnoreCase))
        {
            return CliPromptReadiness.NotReady("Codex CLI is waiting for a workspace trust confirmation.");
        }

        if (IsCodexCliConfigured() && !IsCodexCliIdlePrompt(normalized))
            return CliPromptReadiness.NotReady("Codex CLI is not at an idle input prompt.");

        return CliPromptReadiness.Ready();
    }

    private static string NormalizeVisibleConsoleText(string visibleText) =>
        visibleText.Replace('\u00A0', ' ');

    private bool IsCodexCliConfigured()
    {
        string? launchCommand;
        lock (_sync)
            launchCommand = _status.CliLaunchCommand;

        return IsCodexCliCommand(launchCommand);
    }

    private static bool IsCodexCliCommand(string? launchCommand) =>
        !string.IsNullOrWhiteSpace(launchCommand) &&
        launchCommand.Contains("codex", StringComparison.OrdinalIgnoreCase);

    private static bool IsWorkspaceTrustPrompt(string visibleText)
    {
        if (string.IsNullOrWhiteSpace(visibleText))
            return false;

        var normalized = NormalizeVisibleConsoleText(visibleText);
        return normalized.Contains("Do you trust the contents of this directory", StringComparison.OrdinalIgnoreCase) ||
               normalized.Contains("Do you trust", StringComparison.OrdinalIgnoreCase) &&
               normalized.Contains("Press enter to continue", StringComparison.OrdinalIgnoreCase);
    }

    private static bool IsCodexCliUpdatePrompt(string visibleText)
    {
        if (string.IsNullOrWhiteSpace(visibleText))
            return false;

        var normalized = NormalizeVisibleConsoleText(visibleText);
        return normalized.Contains("Update available!", StringComparison.OrdinalIgnoreCase) &&
            normalized.Contains("Skip until next version", StringComparison.OrdinalIgnoreCase) &&
            normalized.Contains("Press enter to continue", StringComparison.OrdinalIgnoreCase);
    }

    private bool IsTrustedCodexWorkingDirectory(string? workingDirectory)
    {
        if (string.IsNullOrWhiteSpace(workingDirectory))
            return false;

        var fullPath = Path.GetFullPath(workingDirectory);
        var sessionRootPath = Path.GetFullPath(_sessionPath);
        var contextPackPath = Path.GetFullPath(Path.Combine(_sessionPath, "game_state", "control", "gm_context_pack"));
        return string.Equals(fullPath, sessionRootPath, StringComparison.OrdinalIgnoreCase) ||
               string.Equals(fullPath, contextPackPath, StringComparison.OrdinalIgnoreCase) ||
               fullPath.StartsWith(contextPackPath + Path.DirectorySeparatorChar, StringComparison.OrdinalIgnoreCase) ||
               fullPath.StartsWith(contextPackPath + Path.AltDirectorySeparatorChar, StringComparison.OrdinalIgnoreCase);
    }

    private static bool IsCodexCliWorkingScreen(string visibleText)
    {
        var normalized = NormalizeVisibleConsoleText(visibleText);
        if (IsCodexCliBootOrModelLoadingScreen(normalized))
            return true;

        if (HasCodexIdlePromptAfterLastWorkingMarker(normalized))
            return false;

        return normalized.Contains("esc to interrupt", StringComparison.OrdinalIgnoreCase) &&
            normalized.Contains("Working", StringComparison.OrdinalIgnoreCase);
    }

    private static bool IsCodexCliIdlePrompt(string visibleText)
    {
        if (string.IsNullOrWhiteSpace(visibleText))
            return false;

        var normalized = NormalizeVisibleConsoleText(visibleText);
        if (IsWorkspaceTrustPrompt(normalized) || IsCodexCliUpdatePrompt(normalized))
            return false;

        if (IsCodexCliBootOrModelLoadingScreen(normalized))
            return false;

        return HasCodexIdlePromptAfterLastWorkingMarker(normalized) ||
            normalized.Contains("OpenAI Codex", StringComparison.OrdinalIgnoreCase) &&
            normalized.Contains("›", StringComparison.Ordinal) ||
            IsCodexCliCompletedTurnIdlePrompt(normalized);
    }

    private static bool IsCodexCliBootOrModelLoadingScreen(string visibleText)
    {
        var normalized = NormalizeVisibleConsoleText(visibleText);
        return normalized.Contains("Starting MCP server", StringComparison.OrdinalIgnoreCase) ||
            normalized.Contains("Booting MCP server", StringComparison.OrdinalIgnoreCase) ||
            normalized.Contains("model:", StringComparison.OrdinalIgnoreCase) &&
            normalized.Contains("loading", StringComparison.OrdinalIgnoreCase);
    }

    private static bool HasCodexIdlePromptAfterLastWorkingMarker(string visibleText)
    {
        if (string.IsNullOrWhiteSpace(visibleText))
            return false;

        var normalized = NormalizeVisibleConsoleText(visibleText);
        var promptIndex = normalized.LastIndexOf("›", StringComparison.Ordinal);
        if (promptIndex < 0)
            return false;

        var workingIndex = normalized.LastIndexOf("Working", StringComparison.OrdinalIgnoreCase);
        var interruptIndex = normalized.LastIndexOf("esc to interrupt", StringComparison.OrdinalIgnoreCase);
        var latestBusyMarker = Math.Max(workingIndex, interruptIndex);
        if (promptIndex <= latestBusyMarker)
            return false;

        var promptTail = normalized[promptIndex..];
        return IsCodexCliModelFooter(promptTail);
    }

    private static bool IsCodexCliCompletedTurnIdlePrompt(string visibleText)
    {
        var normalized = NormalizeVisibleConsoleText(visibleText);
        return normalized.Contains("Worked for", StringComparison.OrdinalIgnoreCase) &&
            normalized.Contains("›", StringComparison.Ordinal) &&
            IsCodexCliModelFooter(normalized) &&
            (normalized.Contains("Run /review on my current changes", StringComparison.OrdinalIgnoreCase) ||
             normalized.Contains("Find and fix a bug in @filename", StringComparison.OrdinalIgnoreCase) ||
             normalized.Contains("gpt-", StringComparison.OrdinalIgnoreCase));
    }

    private static bool IsCodexCliModelFooter(string visibleText)
    {
        var normalized = NormalizeVisibleConsoleText(visibleText);
        return normalized.Contains("gpt-", StringComparison.OrdinalIgnoreCase) &&
            normalized.Contains("·", StringComparison.Ordinal);
    }

    private void RefreshDispatchFailureRecoveryIfCliPromptReady()
    {
        bool shouldProbe;
        lock (_sync)
        {
            shouldProbe = string.Equals(_status.State, "DispatchFailed", StringComparison.Ordinal) && !_status.Ready;
        }

        if (!shouldProbe)
            return;

        var readiness = ProbeCliPromptReadinessForDispatch();
        if (!readiness.IsReady)
            return;

        lock (_sync)
        {
            if (!string.Equals(_status.State, "DispatchFailed", StringComparison.Ordinal) || _status.Ready)
                return;

            _status.Ready = true;
            _status.State = "Ready";
            _status.LastError = null;
            WriteStatusFile();
        }
    }

    private async Task WriteToPtyAsync(InputLifetime input, string text, bool appendEnter, CancellationToken cancellationToken)
    {
        var bytes = Encoding.UTF8.GetBytes(appendEnter ? text + "\r" : text);
        lock (_sync)
        {
            if (_inputClosed || !ReferenceEquals(_inputLifetime, input) || input.Revoked || _pty?.AuthorityLost.IsCompleted == true || _pty?.RootExited.IsCompleted == true)
                throw new InputLifetimeUnavailableException();
            cancellationToken.ThrowIfCancellationRequested();
            input.Token.ThrowIfCancellationRequested();
            _cts.Token.ThrowIfCancellationRequested();
            if (input.PendingWrites++ == 0)
                input.WritesDrained = CreateOutputSignal();
        }
        var acquired = false;
        var started = false;
        try
        {
            using var linked = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken, _cts.Token, input.Token);
            await _ptyWriteLock.WaitAsync(linked.Token);
            acquired = true;
            lock (_sync)
            {
                linked.Token.ThrowIfCancellationRequested();
                if (_inputClosed || !ReferenceEquals(_inputLifetime, input) || input.Revoked || _pty?.AuthorityLost.IsCompleted == true || _pty?.RootExited.IsCompleted == true)
                    throw new InputLifetimeUnavailableException();
                started = true; // Reservation linearizes before revocation; failures after it are uncertain.
            }
            await input.Input.WriteAsync(bytes, 0, bytes.Length, linked.Token);
            linked.Token.ThrowIfCancellationRequested();
            await input.Input.FlushAsync(linked.Token);
            lock (_sync)
            {
                linked.Token.ThrowIfCancellationRequested();
                if (input.Revoked || !ReferenceEquals(_inputLifetime, input))
                    throw new InputLifetimeUnavailableException();
            }
        }
        catch (Exception ex) when (started)
        {
            var error = new PtyInputWriteException(ex);
            lock (_sync)
            {
                if (ReferenceEquals(_inputLifetime, input))
                {
                    _status.Ready = false;
                    _status.State = "DispatchFailed";
                    _status.LastInputWriteError = error.Message;
                    _status.LastError = error.Message;
                    TryWriteInputStatus();
                }
            }
            RevokeInputLifetime(input);
            throw error;
        }
        finally
        {
            if (acquired) _ptyWriteLock.Release();
            lock (_sync)
            {
                if (--input.PendingWrites == 0)
                    input.WritesDrained!.TrySetResult(true);
            }
        }
    }

    private void TryWriteInputStatus()
    {
        try { WriteStatusFile(); }
        catch { /* In-memory status and the fixed exception still retain input uncertainty. */ }
    }

    private async Task PumpOutputAsync(Stream outputReader, Stream consoleWriter, CancellationToken cancellationToken)
    {
        TerminalScreen? screen;
        lock (_sync) screen = _terminalScreen;
        var buffer = new byte[4096];
        var decoder = Encoding.UTF8.GetDecoder();
        var characters = new char[Encoding.UTF8.GetMaxCharCount(buffer.Length)];
        while (true)
        {
            cancellationToken.ThrowIfCancellationRequested();
            var read = await outputReader.ReadAsync(buffer.AsMemory(0, buffer.Length), cancellationToken);
            cancellationToken.ThrowIfCancellationRequested();
            if (read == 0)
            {
                // Only a real EOF finalizes pending bytes. Faults/cancellation propagate above.
                var finalCount = decoder.GetChars(buffer, 0, 0, characters, 0, flush: true);
                lock(_sync) { if(finalCount>0)RecordOutputChunk(characters,finalCount,hasByteActivity:false); screen?.Fault(); }
                return;
            }

            await consoleWriter.WriteAsync(buffer.AsMemory(0, read), cancellationToken);
            await consoleWriter.FlushAsync(cancellationToken);

            var count = decoder.GetChars(buffer, 0, read, characters, 0, flush: false);
            // A partial scalar is still byte activity, even when it produces no text yet.
            lock(_sync) { RecordOutputChunk(characters,count,hasByteActivity:true); if(ReferenceEquals(_terminalScreen,screen))screen?.Feed(buffer.AsSpan(0,read),characters.AsSpan(0,count)); }
        }
    }

    private void RecordOutputChunk(char[] characters, int count, bool hasByteActivity)
    {
        TaskCompletionSource<bool> signalToRelease;
        lock (_sync)
        {
            _recentOutput.Append(characters, 0, count);
            if (_recentOutput.Length > 65536)
            {
                var removeCount = _recentOutput.Length - 65536;
                if (char.IsHighSurrogate(_recentOutput[removeCount - 1]) &&
                    char.IsLowSurrogate(_recentOutput[removeCount]))
                    removeCount++;
                _recentOutput.Remove(0, removeCount);
            }
            if (hasByteActivity)
                _outputVersion++;
            signalToRelease = _outputChanged;
            _outputChanged = CreateOutputSignal();
        }

        signalToRelease.TrySetResult(true);
    }

    private async Task PumpKeyboardAsync(InputLifetime input,
        Func<CancellationToken, ValueTask<ConsoleKeyInfo?>> keySource, CancellationToken cancellationToken)
    {
        using var linked = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken, input.Token, _cts.Token);
        char? highSurrogate=null;
        try
        {
            while (true)
            {
                linked.Token.ThrowIfCancellationRequested();
                var key = await keySource(linked.Token);
                linked.Token.ThrowIfCancellationRequested();
                lock (_sync)
                    if (_inputClosed || !ReferenceEquals(_inputLifetime, input) || input.Revoked || _pty?.AuthorityLost.IsCompleted == true || _pty?.RootExited.IsCompleted == true)
                        return;
                if (key == null) continue;
                TakeManualInput(input);
                string? sequence;
                var character=key.Value.KeyChar;
                if(char.IsHighSurrogate(character)) { highSurrogate=character; continue; }
                if(char.IsLowSurrogate(character)) { sequence=highSurrogate is { } high ? new string([high,character]) : null; highSurrogate=null; }
                else { highSurrogate=null;
                    if((key.Value.Modifiers & ConsoleModifiers.Control)!=0 && key.Value.Key==ConsoleKey.D)sequence=LoadBridgeConfig().GmCliInputProfile.ExitSequence;
                    else if((key.Value.Modifiers & ConsoleModifiers.Control)!=0 && key.Value.Key==ConsoleKey.C)sequence=LoadBridgeConfig().GmCliInputProfile.InterruptSequence;
                    else sequence = KeyToSequence(key.Value);
                }
                if (sequence == null) continue;
                await WriteManualInputAsync(input, sequence, linked.Token);
            }
        }
        catch (OperationCanceledException) when (linked.IsCancellationRequested) { }
        catch (InputLifetimeUnavailableException) { }
        catch (PtyInputWriteException) { /* Recorded and revoked by the real writer; no silent retry. */ }
    }

    private static async ValueTask<ConsoleKeyInfo?> ReadConsoleKeyAsync(CancellationToken cancellationToken)
    {
        if (!Console.IsInputRedirected && Console.KeyAvailable)
            return Console.ReadKey(intercept: true);
        await Task.Delay(15, cancellationToken);
        return null;
    }

    private async Task PumpResizeAsync(CancellationToken cancellationToken)
    {
        var last = GetConsoleSize();
        while (!cancellationToken.IsCancellationRequested)
        {
            await Task.Delay(200, cancellationToken);
            var current = GetConsoleSize();
            if (current == last)
                continue;

            IOwnedTerminalSession? session;
            lock (_sync) session = _pty;
            if (session?.RootExited.IsCompleted == true) return;
            if (session != null) await session.ResizeAsync(new(current.width, current.height), cancellationToken);

            last = current;
        }
    }

    private static string? KeyToSequence(ConsoleKeyInfo key)
    {
        if ((key.Modifiers & ConsoleModifiers.Control) != 0 && key.Key == ConsoleKey.C)
            return "\u0003";

        if (key.KeyChar != '\0' && !char.IsControl(key.KeyChar))
            return key.KeyChar.ToString();

        return key.Key switch
        {
            ConsoleKey.Enter => "\r",
            ConsoleKey.Backspace => "\u007F",
            ConsoleKey.Tab => "\t",
            ConsoleKey.Escape => "\u001B",
            ConsoleKey.UpArrow => "\u001B[A",
            ConsoleKey.DownArrow => "\u001B[B",
            ConsoleKey.RightArrow => "\u001B[C",
            ConsoleKey.LeftArrow => "\u001B[D",
            ConsoleKey.Home => "\u001B[H",
            ConsoleKey.End => "\u001B[F",
            ConsoleKey.Delete => "\u001B[3~",
            ConsoleKey.Insert => "\u001B[2~",
            ConsoleKey.PageUp => "\u001B[5~",
            ConsoleKey.PageDown => "\u001B[6~",
            _ => null
        };
    }

    private static string BuildBracketedPastePayload(string text)
    {
        const string bracketedPasteStart = "\u001b[200~";
        const string bracketedPasteEnd = "\u001b[201~";
        return bracketedPasteStart + text + bracketedPasteEnd;
    }

    private async Task<bool> WaitForPromptVisibleAsync(
        string prompt,
        long outputVersionBefore,
        int outputLengthBefore,
        GameSettings visibilitySettings,
        TimeSpan timeout,
        CancellationToken cancellationToken)
    {
        if (string.IsNullOrWhiteSpace(prompt))
            return true;

        var deadline = DateTime.UtcNow + timeout;
        while (DateTime.UtcNow < deadline)
        {
            Task waitTask;
            lock (_sync)
            {
                var screen = ReadVisibleConsoleText();
                if (_outputVersion > outputVersionBefore &&
                    GmBridgePasteVisibilityPolicy.IsPromptVisible(prompt, screen, visibilitySettings))
                {
                    return true;
                }

                waitTask = _outputChanged.Task;
            }

            var remaining = deadline - DateTime.UtcNow;
            if (remaining <= TimeSpan.Zero)
                break;

            try
            {
                await waitTask.WaitAsync(remaining, cancellationToken);
            }
            catch (TimeoutException)
            {
                return false;
            }
        }

        return false;
    }

    private async Task<bool> WaitForPromptSubmittedAfterEnterAsync(
        string prompt,
        long outputVersionBeforeEnter,
        GameSettings visibilitySettings,
        TimeSpan timeout,
        CancellationToken cancellationToken)
    {
        if (string.IsNullOrWhiteSpace(prompt))
            return true;

        var deadline = DateTime.UtcNow + timeout;
        while (DateTime.UtcNow < deadline)
        {
            Task waitTask;
            lock (_sync)
            {
                var screen = ReadVisibleConsoleText();
                if (_outputVersion > outputVersionBeforeEnter)
                {
                    if (IsCodexCliWorkingScreen(screen) ||
                        !GmBridgePasteVisibilityPolicy.IsPromptVisible(prompt, screen, visibilitySettings))
                    {
                        return true;
                    }
                }

                waitTask = _outputChanged.Task;
            }

            var remaining = deadline - DateTime.UtcNow;
            if (remaining <= TimeSpan.Zero)
                break;

            try
            {
                await waitTask.WaitAsync(remaining, cancellationToken);
            }
            catch (TimeoutException)
            {
                return false;
            }
        }

        return false;
    }

    private async Task WaitForOutputQuietPeriodAsync(TimeSpan quietPeriod, TimeSpan overallTimeout, CancellationToken cancellationToken)
    {
        var deadline = DateTime.UtcNow + overallTimeout;
        while (DateTime.UtcNow < deadline)
        {
            long versionBefore;
            Task signalTask;
            lock (_sync)
            {
                versionBefore = _outputVersion;
                signalTask = _outputChanged.Task;
            }

            try
            {
                await signalTask.WaitAsync(quietPeriod, cancellationToken);
            }
            catch (TimeoutException)
            {
                lock (_sync)
                {
                    if (_outputVersion == versionBefore)
                        return;
                }
            }
        }
    }

    private static TaskCompletionSource<bool> CreateOutputSignal() =>
        new(TaskCreationOptions.RunContinuationsAsynchronously);

    private static string ReadVisibleConsoleText()
    {
        if (!OperatingSystem.IsWindows()) return string.Empty;
        var stdOut = NativeMethods.GetStdHandle(NativeMethods.STD_OUTPUT_HANDLE);
        if (stdOut == IntPtr.Zero || stdOut == NativeMethods.INVALID_HANDLE_VALUE)
            return string.Empty;

        if (!NativeMethods.GetConsoleScreenBufferInfo(stdOut, out var info))
            return string.Empty;

        var width = info.srWindow.Right - info.srWindow.Left + 1;
        var height = info.srWindow.Bottom - info.srWindow.Top + 1;
        if (width <= 0 || height <= 0)
            return string.Empty;

        var total = width * height;
        var builder = new StringBuilder(total);

        for (short row = info.srWindow.Top; row <= info.srWindow.Bottom; row++)
        {
            var line = new StringBuilder(width);
            line.Append(' ', width);
            if (NativeMethods.ReadConsoleOutputCharacterW(stdOut, line, (uint)width, new ConPtyNativeMethods.COORD { X = info.srWindow.Left, Y = row }, out var charsRead) &&
                charsRead > 0)
            {
                builder.Append(line.ToString(0, (int)charsRead));
            }
            builder.Append('\n');
        }

        return builder.ToString();
    }

    private static string BuildShellArguments(string shellExe)
    {
        if (shellExe.EndsWith("powershell.exe", StringComparison.OrdinalIgnoreCase))
            return "-NoLogo -NoExit -ExecutionPolicy Bypass";

        return "-NoLogo -NoExit";
    }

    private static string BuildShellBootstrap(string cliLaunchCommand)
    {
        return
            "$OutputEncoding = [System.Text.UTF8Encoding]::new($false); " +
            "[Console]::InputEncoding = [System.Text.UTF8Encoding]::new($false); " +
            "[Console]::OutputEncoding = [System.Text.UTF8Encoding]::new($false); " +
            "chcp 65001 > $null; " +
            cliLaunchCommand;
    }

    private string ResolveShellExecutable()
    {
        foreach (var candidate in new[] { "pwsh.exe", "powershell.exe" })
        {
            try
            {
                using var probe = Process.Start(new ProcessStartInfo
                {
                    FileName = candidate,
                    ArgumentList = { "-NoLogo", "-NoProfile", "-Command", "$PSVersionTable.PSVersion.ToString()" },
                    RedirectStandardOutput = true,
                    RedirectStandardError = true,
                    UseShellExecute = false,
                    CreateNoWindow = true
                });
                if (probe == null)
                    continue;

                probe.WaitForExit(2000);
                if (probe.ExitCode == 0)
                    return candidate;
            }
            catch
            {
                // ignored
            }
        }

        throw new InvalidOperationException("Neither pwsh.exe nor powershell.exe is available for bridge hosting.");
    }

    private static string ResolveRepoRoot(string fallback, params string[] candidates)
    {
        foreach (var candidate in candidates.Where(candidate => !string.IsNullOrWhiteSpace(candidate)))
        {
            var directory = new DirectoryInfo(candidate);
            while (directory != null)
            {
                if (IsRepoRoot(directory.FullName))
                    return directory.FullName;

                directory = directory.Parent;
            }
        }

        return fallback;
    }

    private static bool IsRepoRoot(string path) =>
        File.Exists(Path.Combine(path, "TheBookOfEternityReborn.sln")) ||
        (Directory.Exists(Path.Combine(path, "BookOfEternityClient")) &&
         Directory.Exists(Path.Combine(path, "BookOfEternityGMBridge")));

    private string ResolveGmBridgeShellWorkingDirectory(string? configuredWorkingDirectory)
    {
        if (!string.IsNullOrWhiteSpace(configuredWorkingDirectory))
        {
            var configured = configuredWorkingDirectory.Trim();
            var fullPath = Path.IsPathRooted(configured)
                ? Path.GetFullPath(configured)
                : Path.GetFullPath(Path.Combine(_sessionPath, configured));
            Directory.CreateDirectory(fullPath);
            return fullPath;
        }

        if (Directory.Exists(_sessionPath))
            return _sessionPath;
        if (Directory.Exists(_clientRoot))
            return _clientRoot;
        if (Directory.Exists(_repoRoot))
            return _repoRoot;
        return Environment.CurrentDirectory;
    }

    private GameSettings LoadBridgeConfig()
    {
        try
        {
            if (!File.Exists(_configPath))
                return new GameSettings();

            var json = File.ReadAllText(_configPath, Encoding.UTF8);
            var loaded = JsonSerializer.Deserialize<GameSettings>(json, JsonOpts);
            var settings = new GameSettings();
            if (loaded != null)
                settings.ApplyLoadedValues(loaded);
            return settings;
        }
        catch
        {
            return new GameSettings();
        }
    }

    private BridgeStatus SnapshotStatus()
    {
        lock (_sync)
        {
            return _status with { TerminalOwnerRetained=_pty!=null };
        }
    }

    private BridgeDiagnostics SnapshotDiagnostics()
    {
        long outputVersion;
        string recentOutput;
        string visibleScreenText;
        lock (_sync)
        {
            recentOutput = GetRecentOutputTail();
            outputVersion = _outputVersion;
            visibleScreenText = _promptScreenReader();
        }

        return new BridgeDiagnostics
        {
            OutputVersion = outputVersion,
            RecentOutputTail = recentOutput,
            VisibleScreenText = visibleScreenText,
            WorkerProposalInbox = ReadWorkerProposalInbox()
        };
    }

    private string GetRecentOutputTail()
    {
        const int tailLimit = 12000;
        var recentOutput = _recentOutput.ToString();
        if (recentOutput.Length > tailLimit)
        {
            var start = recentOutput.Length - tailLimit;
            if (char.IsHighSurrogate(recentOutput[start - 1]) && char.IsLowSurrogate(recentOutput[start]))
                start++;
            recentOutput = recentOutput[start..];
        }
        return recentOutput;
    }

    private List<GmWorkerProposalInboxEntry> ReadWorkerProposalInbox()
    {
        try
        {
            var fs = new FileSystemManager(_clientRoot, NullLogger<FileSystemManager>.Instance);
            return new GmWorkerProposalInboxService(fs).ListAsync().GetAwaiter().GetResult().ToList();
        }
        catch
        {
            return [];
        }
    }

    private async Task<BridgeResponse> DispatchWorkerTaskAsync(BridgeRequest request)
    {
        var settings = LoadBridgeConfig();
        var fs = new FileSystemManager(_clientRoot, NullLogger<FileSystemManager>.Instance);
        var audit = new GmWorkerAuditLog(fs);
        var service = new GmWorkerProposalOnlyDispatchService(
            fs,
            new GmWorkerBridgePool(fs, new GmWorkerProposalStore(fs), audit),
            audit);
        var dispatchRequest = BuildWorkerDispatchRequest(request);
        var result = await service.DispatchAsync(settings.GmWorkerBridgeProfiles, dispatchRequest);

        return BridgeResponse.Success(SnapshotStatus(), SnapshotDiagnostics(), result);
    }

    private static GmWorkerProposalOnlyDispatchRequest BuildWorkerDispatchRequest(BridgeRequest request)
    {
        var taskType = ParseWorkerTaskType(request.WorkerTaskType);
        var sourceTurn = new WorkerTurnReference
        {
            SessionId = string.IsNullOrWhiteSpace(request.SessionId) ? "bridge-manual-dispatch" : request.SessionId!,
            RequestId = string.IsNullOrWhiteSpace(request.RequestId)
                ? "worker-dispatch-" + DateTimeOffset.UtcNow.ToString("yyyyMMddHHmmssfff")
                : request.RequestId!,
            TurnNumber = request.TurnNumber ?? 0
        };

        return taskType switch
        {
            WorkerTaskType.NarrativeDraft => GmWorkerProposalOnlyDispatchRequest.NarrativeDraft(
                sourceTurn,
                request.SceneGoal ?? "",
                request.Tone ?? "",
                request.ContinuityNotes,
                request.TargetLength ?? "",
                request.ContextPaths),
            WorkerTaskType.Analysis => GmWorkerProposalOnlyDispatchRequest.Analysis(
                sourceTurn,
                request.AnalysisGoal ?? "",
                request.Questions,
                request.ContextPaths),
            _ when WorkerTaskTypes.IsContentAuthoring(taskType) =>
                GmWorkerProposalOnlyDispatchRequest.ContentAuthoring(
                    taskType,
                    sourceTurn,
                    new WorkerContentAuthoringRequest
                    {
                        Domain = ParseWorkerAuthoringDomain(request.AuthoringDomain, taskType),
                        Goal = request.AuthoringGoal ?? "",
                        EntityHints = request.EntityHints,
                        RequiredLinks = request.RequiredLinks,
                        OutputNotes = request.OutputNotes
                    },
                    request.ContextPaths),
            _ => new GmWorkerProposalOnlyDispatchRequest
            {
                TaskType = taskType,
                SourceTurn = sourceTurn,
                ContextPaths = request.ContextPaths
            }
        };
    }

    private static WorkerTaskType ParseWorkerTaskType(string? taskType)
    {
        var normalized = (taskType ?? "").Trim().Replace("_", "-", StringComparison.Ordinal).ToLowerInvariant();
        return normalized switch
        {
            "narrative-draft" or "narrativedraft" => WorkerTaskType.NarrativeDraft,
            "analysis" => WorkerTaskType.Analysis,
            "validation-repair" or "validationrepair" => WorkerTaskType.ValidationRepair,
            "lore-consistency" or "loreconsistency" => WorkerTaskType.LoreConsistency,
            "npc-analysis" or "npcanalysis" => WorkerTaskType.NpcAnalysis,
            "qte-content" or "qtecontent" => WorkerTaskType.QteContent,
            "inventory-content" or "inventorycontent" => WorkerTaskType.InventoryContent,
            "skill-content" or "skillcontent" => WorkerTaskType.SkillContent,
            "npc-content" or "npccontent" => WorkerTaskType.NpcContent,
            "social-dialogue-content" or "socialdialoguecontent" => WorkerTaskType.SocialDialogueContent,
            "faction-content" or "factioncontent" => WorkerTaskType.FactionContent,
            "location-content" or "locationcontent" => WorkerTaskType.LocationContent,
            "quest-content" or "questcontent" => WorkerTaskType.QuestContent,
            "book-document-content" or "bookdocumentcontent" => WorkerTaskType.BookDocumentContent,
            "economy-crafting-content" or "economycraftingcontent" => WorkerTaskType.EconomyCraftingContent,
            "world-state-content" or "worldstatecontent" => WorkerTaskType.WorldStateContent,
            "encounter-content" or "encountercontent" => WorkerTaskType.EncounterContent,
            _ => WorkerTaskType.Analysis
        };
    }

    private static WorkerAuthoringDomain ParseWorkerAuthoringDomain(string? domain, WorkerTaskType taskType)
    {
        var normalized = (domain ?? "").Trim().Replace("_", "-", StringComparison.Ordinal).ToLowerInvariant();
        return normalized switch
        {
            "inventory" => WorkerAuthoringDomain.Inventory,
            "skill" => WorkerAuthoringDomain.Skill,
            "npc" => WorkerAuthoringDomain.Npc,
            "social-dialogue" or "socialdialogue" => WorkerAuthoringDomain.SocialDialogue,
            "faction" => WorkerAuthoringDomain.Faction,
            "location" => WorkerAuthoringDomain.Location,
            "quest" => WorkerAuthoringDomain.Quest,
            "book-document" or "bookdocument" => WorkerAuthoringDomain.BookDocument,
            "economy-crafting" or "economycrafting" => WorkerAuthoringDomain.EconomyCrafting,
            "world-state" or "worldstate" => WorkerAuthoringDomain.WorldState,
            "encounter" => WorkerAuthoringDomain.Encounter,
            "qte" => WorkerAuthoringDomain.Qte,
            _ => taskType switch
            {
                WorkerTaskType.InventoryContent => WorkerAuthoringDomain.Inventory,
                WorkerTaskType.SkillContent => WorkerAuthoringDomain.Skill,
                WorkerTaskType.NpcContent => WorkerAuthoringDomain.Npc,
                WorkerTaskType.SocialDialogueContent => WorkerAuthoringDomain.SocialDialogue,
                WorkerTaskType.FactionContent => WorkerAuthoringDomain.Faction,
                WorkerTaskType.LocationContent => WorkerAuthoringDomain.Location,
                WorkerTaskType.QuestContent => WorkerAuthoringDomain.Quest,
                WorkerTaskType.BookDocumentContent => WorkerAuthoringDomain.BookDocument,
                WorkerTaskType.EconomyCraftingContent => WorkerAuthoringDomain.EconomyCrafting,
                WorkerTaskType.WorldStateContent => WorkerAuthoringDomain.WorldState,
                WorkerTaskType.EncounterContent => WorkerAuthoringDomain.Encounter,
                WorkerTaskType.QteContent => WorkerAuthoringDomain.Qte,
                _ => WorkerAuthoringDomain.WorldState
            }
        };
    }

    private BridgeResponse FailWithLastError(string error)
    {
        lock (_sync)
        {
            _status.Ready = false;
            _status.State = "DispatchFailed";
            _status.LastError = error;
            WriteStatusFile();
        }

        return BridgeResponse.Failure(error, SnapshotStatus(), SnapshotDiagnostics());
    }

    private async Task HandlePtyExitedAsync(IOwnedTerminalSession observedPty, InputLifetime? observedInput)
    {
        await _shellLifecycleLock.WaitAsync();
        try
        {
            int? exitCode;
            lock (_sync)
            {
                if (!ReferenceEquals(_pty, observedPty) || !ReferenceEquals(_inputLifetime, observedInput))
                    return;
                exitCode = observedPty.RootExited.IsCompletedSuccessfully ? observedPty.RootExited.Result.ExitCode : null;
            }
            try { await StopShellCoreAsync(); }
            catch (TimeoutException) { return; } // Retained drain blocks replacement; continue serving diagnostics.
            lock (_sync)
            {
                _status.ShellPid = null;
                _status.CliProcessId = null;
                _status.Ready = false;
                _status.State = "Disconnected";
                _status.LastError = $"PTY shell exited with code {exitCode}.";
                WriteStatusFile();
            }
            Console.WriteLine();
            Console.WriteLine($"[Bridge] Hosted PTY shell exited with code {exitCode}. Use `bookofeternity.ps1 restart-shell` to restart it.");
        }
        finally { _shellLifecycleLock.Release(); }
    }

    private void WriteStatusFile()
    {
        _status.UpdatedAtUtc = DateTimeOffset.UtcNow.ToString("O");
        var json = JsonSerializer.Serialize(_status, JsonOpts);
        File.WriteAllText(_statusPath, json, Encoding.UTF8);
        UpdateConsoleTitle();
    }

    private void UpdateConsoleTitle()
    {
        lock (_sync)
        {
            var state = _status.Ready ? "READY" : _status.State.ToUpperInvariant();
            var command = string.IsNullOrWhiteSpace(_status.CliLaunchCommand) ? "manual CLI" : _status.CliLaunchCommand;
            Console.Title = $"Book of Eternity GM Bridge [{state}] - {command}";
        }
    }

    private void SafeDeleteStatusFile()
    {
        try
        {
            if (File.Exists(_statusPath))
                File.Delete(_statusPath);
        }
        catch
        {
            // ignored
        }
    }

    private static void EnableVirtualTerminalOutput()
    {
        var stdOut = NativeMethods.GetStdHandle(NativeMethods.STD_OUTPUT_HANDLE);
        if (stdOut == IntPtr.Zero || stdOut == NativeMethods.INVALID_HANDLE_VALUE)
            return;

        if (!NativeMethods.GetConsoleMode(stdOut, out var mode))
            return;

        mode |= NativeMethods.ENABLE_VIRTUAL_TERMINAL_PROCESSING;
        NativeMethods.SetConsoleMode(stdOut, mode);
    }

    private static (short width, short height) GetConsoleSize()
    {
        try
        {
            return ((short)Math.Clamp(Console.WindowWidth, 40, short.MaxValue),
                    (short)Math.Clamp(Console.WindowHeight, 10, short.MaxValue));
        }
        catch
        {
            return (120, 40);
        }
    }

    public void Dispose()
    {
        lock (_sync) _inputClosed = true;
        _cts.Cancel();
        try { StopShellAsync().GetAwaiter().GetResult(); }
        catch (TimeoutException) { return; } // Retain the gate/CTS and actual tasks for a later drain attempt.
        lock (_sync)
        {
            if (!_writeGateDisposed)
            {
                _ptyWriteLock.Dispose();
                _writeGateDisposed = true;
            }
        }
        SafeDeleteStatusFile();
    }

    private static async Task<T?> ReadMessageAsync<T>(NamedPipeServerStream stream, CancellationToken cancellationToken)
    {
        using var reader = new StreamReader(stream, Encoding.UTF8, false, 1024, leaveOpen: true);
        var line = await reader.ReadLineAsync().WaitAsync(cancellationToken);
        if (string.IsNullOrWhiteSpace(line))
            return default;

        return JsonSerializer.Deserialize<T>(line, PipeJsonOpts);
    }

    private static async Task WriteMessageAsync<T>(Stream stream, T payload, CancellationToken cancellationToken)
    {
        // No StreamWriter.Dispose synchronous flush after a timed-out response.
        var bytes = Encoding.UTF8.GetBytes(JsonSerializer.Serialize(payload, PipeJsonOpts) + "\n");
        await stream.WriteAsync(bytes, cancellationToken);
        await stream.FlushAsync(cancellationToken);
    }

}

internal sealed class BridgeRequest
{
    public int? Columns { get; set; }
    public int? Rows { get; set; }
    public string? Command { get; set; }
    public string? Text { get; set; }
    public bool AppendEnter { get; set; } = true;
    public bool? Ready { get; set; }
    public string? OperationId { get; set; }
    public string? OperationKind { get; set; }
    public string? OperationRevision { get; set; }
    public string? InputBindingId { get; set; }
    public string? WorkerTaskType { get; set; }
    public string? SessionId { get; set; }
    public string? RequestId { get; set; }
    public int? TurnNumber { get; set; }
    public string? SceneGoal { get; set; }
    public string? Tone { get; set; }
    public List<string> ContinuityNotes { get; set; } = new();
    public string? TargetLength { get; set; }
    public string? AnalysisGoal { get; set; }
    public List<string> Questions { get; set; } = new();
    public string? AuthoringDomain { get; set; }
    public string? AuthoringGoal { get; set; }
    public List<string> EntityHints { get; set; } = new();
    public List<string> RequiredLinks { get; set; } = new();
    public List<string> OutputNotes { get; set; } = new();
    public List<string> ContextPaths { get; set; } = new();
}

internal sealed class BridgeResponse
{
    public bool Ok { get; set; }
    public string? Error { get; set; }
    public BridgeStatus? Status { get; set; }
    public BridgeDiagnostics? Diagnostics { get; set; }
    public PromptDeliveryResult? PromptDelivery { get; set; }
    public GmWorkerProposalOnlyDispatchResult? WorkerDispatch { get; set; }
    [JsonIgnore]
    public bool ShutdownAfterResponse { get; set; }

    public static BridgeResponse Success(BridgeStatus status) => new()
    {
        Ok = true,
        Status = status
    };

    public static BridgeResponse Success(BridgeStatus status, BridgeDiagnostics diagnostics) => new()
    {
        Ok = true,
        Status = status,
        Diagnostics = diagnostics
    };

    public static BridgeResponse Success(
        BridgeStatus status,
        BridgeDiagnostics diagnostics,
        GmWorkerProposalOnlyDispatchResult workerDispatch) => new()
    {
        Ok = true,
        Status = status,
        Diagnostics = diagnostics,
        WorkerDispatch = workerDispatch
    };

    public static BridgeResponse Shutdown(BridgeStatus status) => new()
    {
        Ok = true,
        Status = status,
        ShutdownAfterResponse = true
    };

    public static BridgeResponse Failure(string error, BridgeStatus status) => new()
    {
        Ok = false,
        Error = error,
        Status = status
    };

    public static BridgeResponse Failure(string error, BridgeStatus status, BridgeDiagnostics diagnostics) => new()
    {
        Ok = false,
        Error = error,
        Status = status,
        Diagnostics = diagnostics
    };
}

internal sealed record BridgeStatus
{
    public string? TerminalRunId { get; set; }
    public string? TerminalGuarantee { get; set; }
    public bool TerminalUncertain { get; set; }
    public TerminalStopEvidence? TerminalStop { get; set; }
    public bool TerminalOwnerRetained { get; set; }
    public string? InputBindingId { get; set; }
    public PromptDeliveryResult? PromptDelivery { get; set; }
    public string Backend { get; set; } = "ConPTYBridge";
    public string State { get; set; } = "Starting";
    public bool Ready { get; set; }
    public int HelperPid { get; set; }
    public string SessionPath { get; set; } = string.Empty;
    public int? ShellPid { get; set; }
    public int? CliProcessId { get; set; }
    public string PipeName { get; set; } = string.Empty;
    public string CliLaunchCommand { get; set; } = string.Empty;
    public string ShellWorkingDirectory { get; set; } = string.Empty;
    public string LastPromptDispatchState { get; set; } = "None";
    public string? LastPromptDispatchStartedAtUtc { get; set; }
    public string? LastPromptDispatchCompletedAtUtc { get; set; }
    public long? LastPromptDispatchElapsedMs { get; set; }
    public string StartedAtUtc { get; set; } = DateTimeOffset.UtcNow.ToString("O");
    public string UpdatedAtUtc { get; set; } = DateTimeOffset.UtcNow.ToString("O");
    public string? LastError { get; set; }
    public string? LastInputWriteError { get; set; }
    public List<WorkerBridgeStatus> WorkerStatuses { get; set; } = new();
}

internal sealed class BridgeDiagnostics
{
    public long OutputVersion { get; set; }
    public string RecentOutputTail { get; set; } = string.Empty;
    public string VisibleScreenText { get; set; } = string.Empty;
    public List<GmWorkerProposalInboxEntry> WorkerProposalInbox { get; set; } = new();
}

internal sealed record CliPromptReadiness(bool IsReady, string Reason)
{
    public static CliPromptReadiness Ready() => new(true, string.Empty);
    public static CliPromptReadiness NotReady(string reason) => new(false, reason);
}

internal static class NativeMethods
{
    public const int STD_OUTPUT_HANDLE = -11;
    public const uint ENABLE_VIRTUAL_TERMINAL_PROCESSING = 0x0004;
    public static readonly IntPtr INVALID_HANDLE_VALUE = new(-1);

    [DllImport("kernel32.dll", SetLastError = true)]
    public static extern bool SetConsoleCP(uint wCodePageID);

    [DllImport("kernel32.dll", SetLastError = true)]
    public static extern bool SetConsoleOutputCP(uint wCodePageID);

    [DllImport("kernel32.dll", SetLastError = true)]
    public static extern IntPtr GetStdHandle(int nStdHandle);

    [DllImport("kernel32.dll", SetLastError = true)]
    public static extern bool GetConsoleMode(IntPtr hConsoleHandle, out uint lpMode);

    [DllImport("kernel32.dll", SetLastError = true)]
    public static extern bool SetConsoleMode(IntPtr hConsoleHandle, uint dwMode);

    [DllImport("kernel32.dll", SetLastError = true)]
    public static extern bool GetConsoleScreenBufferInfo(IntPtr hConsoleOutput, out CONSOLE_SCREEN_BUFFER_INFO lpConsoleScreenBufferInfo);

    [DllImport("kernel32.dll", CharSet = CharSet.Unicode, SetLastError = true)]
    public static extern bool ReadConsoleOutputCharacterW(
        IntPtr hConsoleOutput,
        StringBuilder lpCharacter,
        uint nLength,
        ConPtyNativeMethods.COORD dwReadCoord,
        out uint lpNumberOfCharsRead);

    [StructLayout(LayoutKind.Sequential)]
    public struct SMALL_RECT
    {
        public short Left;
        public short Top;
        public short Right;
        public short Bottom;
    }

    [StructLayout(LayoutKind.Sequential)]
    public struct CONSOLE_SCREEN_BUFFER_INFO
    {
        public ConPtyNativeMethods.COORD dwSize;
        public ConPtyNativeMethods.COORD dwCursorPosition;
        public short wAttributes;
        public SMALL_RECT srWindow;
        public ConPtyNativeMethods.COORD dwMaximumWindowSize;
    }
}
