using System.Diagnostics;
using System.IO.Pipes;
using System.Runtime.InteropServices;
using System.Text;
using System.Text.Json;
using System.Text.Json.Serialization;
using BookOfEternityClient.Core;
using BookOfEternityClient.Configuration;
using BookOfEternityClient.Services.GmWorkers;
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
            using var host = new BridgeHost(sessionPath, pipeName);
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

internal sealed class BridgeHost : IDisposable
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
    private readonly StringBuilder _recentOutput = new();

    private ConPtySession? _pty;
    private Stream? _ptyInput;
    private Task? _outputPumpTask;
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
            PipeName = _pipeName,
            StartedAtUtc = DateTimeOffset.UtcNow.ToString("O"),
            UpdatedAtUtc = DateTimeOffset.UtcNow.ToString("O")
        };
    }

    public async Task<int> RunAsync()
    {
        NativeMethods.SetConsoleCP(65001);
        NativeMethods.SetConsoleOutputCP(65001);
        Console.InputEncoding = Encoding.UTF8;
        Console.OutputEncoding = Encoding.UTF8;
        EnableVirtualTerminalOutput();
        UpdateConsoleTitle();
        PrintBanner();

        await StartShellAsync();
        var serverTask = RunServerLoopAsync(_cts.Token);

        try
        {
            while (!_cts.IsCancellationRequested)
            {
                lock (_sync)
                {
                    if (_pty is { HasExited: true })
                        HandlePtyExited_NoThrow();
                }

                await Task.Delay(250, _cts.Token);
            }
        }
        catch (OperationCanceledException)
        {
            // normal shutdown
        }
        finally
        {
            try { await serverTask; } catch { /* ignored */ }
            await StopShellAsync();
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
        while (!cancellationToken.IsCancellationRequested)
        {
            using var server = new NamedPipeServerStream(
                _pipeName,
                PipeDirection.InOut,
                1,
                PipeTransmissionMode.Byte,
                PipeOptions.Asynchronous);

            await server.WaitForConnectionAsync(cancellationToken);

            try
            {
                var request = await ReadMessageAsync<BridgeRequest>(server, cancellationToken) ?? new BridgeRequest();
                var response = await HandleRequestAsync(request);
                await WriteMessageAsync(server, response, cancellationToken);
            }
            catch (OperationCanceledException)
            {
                break;
            }
            catch (Exception ex)
            {
                await WriteMessageAsync(server, new BridgeResponse
                {
                    Ok = false,
                    Error = ex.Message,
                    Status = SnapshotStatus()
                }, cancellationToken);
            }
        }
    }

    private async Task<BridgeResponse> HandleRequestAsync(BridgeRequest request)
    {
        var command = (request.Command ?? string.Empty).Trim().ToLowerInvariant();
        switch (command)
        {
            case "status":
                return BridgeResponse.Success(SnapshotStatus());

            case "diagnostics":
                return BridgeResponse.Success(SnapshotStatus(), SnapshotDiagnostics());

            case "setready":
                SetReady(request.Ready ?? true);
                return BridgeResponse.Success(SnapshotStatus());

            case "dispatchworkertask":
                return await DispatchWorkerTaskAsync(request);

            case "addtext":
                EnsureShellAlive();
                await WriteToPtyAsync(request.Text ?? string.Empty, appendEnter: false);
                return BridgeResponse.Success(SnapshotStatus());

            case "sendenter":
                EnsureShellAlive();
                await WriteToPtyAsync(string.Empty, appendEnter: true);
                return BridgeResponse.Success(SnapshotStatus());

            case "dispatchprompt":
                EnsureShellAlive();
                var dispatchStartedAt = DateTimeOffset.UtcNow;
                var dispatchStopwatch = Stopwatch.StartNew();
                lock (_sync)
                {
                    if (!_status.Ready)
                        return BridgeResponse.Failure("Bridge is not marked ready.", SnapshotStatus());

                    _status.State = "Busy";
                    _status.LastPromptDispatchState = "Dispatching";
                    _status.LastPromptDispatchStartedAtUtc = dispatchStartedAt.ToString("O");
                    _status.LastPromptDispatchCompletedAtUtc = null;
                    _status.LastPromptDispatchElapsedMs = null;
                    WriteStatusFile();
                }

                try
                {
                    long outputVersionBefore;
                    int outputLengthBefore;
                    var visibilitySettings = LoadBridgeConfig();
                    lock (_sync)
                    {
                        outputVersionBefore = _outputVersion;
                        outputLengthBefore = _recentOutput.Length;
                    }

                    var payload = BuildBracketedPastePayload(request.Text ?? string.Empty);
                    await WriteToPtyAsync(payload, appendEnter: false);
                    if (request.AppendEnter)
                    {
                        var visible = await WaitForPromptVisibleAsync(
                            request.Text ?? string.Empty,
                            outputVersionBefore,
                            outputLengthBefore,
                            visibilitySettings,
                            TimeSpan.FromSeconds(visibilitySettings.GmBridgePromptVisibilityTimeoutSeconds),
                            _cts.Token);
                        if (!visible)
                        {
                            return FailWithLastError(
                                "Prompt text was pasted into the PTY, but it never became visible in the CLI output. Enter was not sent to avoid switching the CLI into a wrong mode.");
                        }

                        await WaitForOutputQuietPeriodAsync(TimeSpan.FromMilliseconds(250), TimeSpan.FromSeconds(2), _cts.Token);
                        await WriteToPtyAsync(string.Empty, appendEnter: true);
                    }
                }
                finally
                {
                    dispatchStopwatch.Stop();
                    lock (_sync)
                    {
                        _status.LastPromptDispatchCompletedAtUtc = DateTimeOffset.UtcNow.ToString("O");
                        _status.LastPromptDispatchElapsedMs = dispatchStopwatch.ElapsedMilliseconds;
                        _status.LastPromptDispatchState = string.Equals(_status.State, "DispatchFailed", StringComparison.Ordinal)
                            ? "Failed"
                            : "Completed";
                        if (!string.Equals(_status.State, "DispatchFailed", StringComparison.Ordinal))
                            _status.State = _status.Ready ? "Ready" : "OperatorNotReady";
                        WriteStatusFile();
                    }
                }

                return BridgeResponse.Success(SnapshotStatus());

            case "restartshell":
            case "restartcli":
                await StartShellAsync();
                return BridgeResponse.Success(SnapshotStatus());

            case "shutdown":
                _cts.Cancel();
                return BridgeResponse.Success(SnapshotStatus());

            default:
                return BridgeResponse.Failure($"Unknown bridge command '{request.Command}'.", SnapshotStatus());
        }
    }

    private async Task StartShellAsync()
    {
        await StopShellAsync();

        var config = LoadBridgeConfig();
        var shellExe = ResolveShellExecutable();
        var shellArgs = BuildShellArguments(shellExe);
        var workingDirectory = ResolveGmBridgeShellWorkingDirectory(config.GmBridgeShellWorkingDirectory);
        var (width, height) = GetConsoleSize();
        var pty = ConPtySession.Start(shellExe, shellArgs, workingDirectory, width, height);
        var outputWriter = Console.OpenStandardOutput();

        _pty = pty;
        _ptyInput = pty.InputWriter;
        _shellLoopCts = new CancellationTokenSource();
        _outputPumpTask = Task.Run(() => PumpOutputAsync(pty.OutputReader, outputWriter, _shellLoopCts.Token), _shellLoopCts.Token);
        _keyboardPumpTask = Task.Run(() => PumpKeyboardAsync(_shellLoopCts.Token), _shellLoopCts.Token);
        _resizePumpTask = Task.Run(() => PumpResizeAsync(_shellLoopCts.Token), _shellLoopCts.Token);

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
            await Task.Delay(250);
            await WriteToPtyAsync(bootstrap, appendEnter: true);
        }

        await Task.CompletedTask;
    }

    private async Task StopShellAsync()
    {
        ConPtySession? pty;
        Task? outputPump;
        Task? keyboardPump;
        Task? resizePump;
        CancellationTokenSource? shellLoopCts;

        lock (_sync)
        {
            pty = _pty;
            outputPump = _outputPumpTask;
            keyboardPump = _keyboardPumpTask;
            resizePump = _resizePumpTask;
            shellLoopCts = _shellLoopCts;
            _pty = null;
            _ptyInput = null;
            _outputPumpTask = null;
            _keyboardPumpTask = null;
            _resizePumpTask = null;
            _shellLoopCts = null;
        }

        if (pty == null)
            return;

        try
        {
            shellLoopCts?.Cancel();
        }
        catch
        {
            // ignored
        }

        try
        {
            pty.Dispose();
        }
        catch
        {
            // ignored
        }

        try
        {
            if (outputPump != null)
                await outputPump;
        }
        catch
        {
            // ignored
        }

        try
        {
            if (keyboardPump != null)
                await keyboardPump;
        }
        catch
        {
            // ignored
        }

        try
        {
            if (resizePump != null)
                await resizePump;
        }
        catch
        {
            // ignored
        }
    }

    private void SetReady(bool ready)
    {
        lock (_sync)
        {
            _status.Ready = ready;
            _status.State = ready ? "Ready" : "OperatorNotReady";
            _status.LastError = null;
            WriteStatusFile();
        }

        Console.WriteLine();
        Console.WriteLine(ready
            ? "[Bridge] Marked READY. Daemon may dispatch prompts now."
            : "[Bridge] Marked NOT READY. Daemon dispatch should pause or fallback.");
    }

    private void EnsureShellAlive()
    {
        lock (_sync)
        {
            if (_pty == null || _pty.HasExited || _ptyInput == null)
                throw new InvalidOperationException("Hosted PTY shell is not running.");
        }
    }

    private async Task WriteToPtyAsync(string text, bool appendEnter)
    {
        Stream? input;
        lock (_sync)
            input = _ptyInput;

        if (input == null)
            throw new InvalidOperationException("PTY input stream is unavailable.");

        await _ptyWriteLock.WaitAsync(_cts.Token);
        try
        {
            var payload = appendEnter ? text + "\r" : text;
            var bytes = Encoding.UTF8.GetBytes(payload);
            await input.WriteAsync(bytes, 0, bytes.Length, _cts.Token);
            await input.FlushAsync(_cts.Token);
        }
        finally
        {
            _ptyWriteLock.Release();
        }
    }

    private async Task PumpOutputAsync(Stream outputReader, Stream consoleWriter, CancellationToken cancellationToken)
    {
        var buffer = new byte[4096];
        while (!cancellationToken.IsCancellationRequested)
        {
            var read = await outputReader.ReadAsync(buffer.AsMemory(0, buffer.Length), cancellationToken);
            if (read <= 0)
                break;

            await consoleWriter.WriteAsync(buffer.AsMemory(0, read), cancellationToken);
            await consoleWriter.FlushAsync(cancellationToken);

            var chunk = Encoding.UTF8.GetString(buffer, 0, read);
            TaskCompletionSource<bool> signalToRelease;
            lock (_sync)
            {
                _recentOutput.Append(chunk);
                if (_recentOutput.Length > 65536)
                    _recentOutput.Remove(0, _recentOutput.Length - 65536);
                _outputVersion++;
                signalToRelease = _outputChanged;
                _outputChanged = CreateOutputSignal();
            }

            signalToRelease.TrySetResult(true);

        }
    }

    private async Task PumpKeyboardAsync(CancellationToken cancellationToken)
    {
        while (!cancellationToken.IsCancellationRequested)
        {
            if (!Console.KeyAvailable)
            {
                await Task.Delay(15, cancellationToken);
                continue;
            }

            var key = Console.ReadKey(intercept: true);
            var sequence = KeyToSequence(key);
            if (sequence == null)
                continue;

            try
            {
                await WriteToPtyAsync(sequence, appendEnter: false);
            }
            catch
            {
                // ignored
            }
        }
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

            lock (_sync)
            {
                _pty?.Resize(current.width, current.height);
            }

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
            return _status with { };
        }
    }

    private BridgeDiagnostics SnapshotDiagnostics()
    {
        const int tailLimit = 12000;
        long outputVersion;
        string recentOutput;
        string visibleScreenText;
        lock (_sync)
        {
            recentOutput = _recentOutput.ToString();
            if (recentOutput.Length > tailLimit)
                recentOutput = recentOutput[^tailLimit..];
            outputVersion = _outputVersion;
            visibleScreenText = ReadVisibleConsoleText();
        }

        return new BridgeDiagnostics
        {
            OutputVersion = outputVersion,
            RecentOutputTail = recentOutput,
            VisibleScreenText = visibleScreenText,
            WorkerProposalInbox = ReadWorkerProposalInbox()
        };
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
            _ => WorkerTaskType.Analysis
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

    private void HandlePtyExited_NoThrow()
    {
        if (_pty == null)
            return;

        var exitCode = _pty.ExitCode;
        _pty.Dispose();
        _pty = null;
        _ptyInput = null;

        _status.ShellPid = null;
        _status.CliProcessId = null;
        _status.Ready = false;
        _status.State = "Disconnected";
        _status.LastError = $"PTY shell exited with code {exitCode}.";
        WriteStatusFile();

        Console.WriteLine();
        Console.WriteLine($"[Bridge] Hosted PTY shell exited with code {exitCode}. Use `bookofeternity.ps1 restart-shell` to restart it.");
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
        _cts.Cancel();
        try { StopShellAsync().GetAwaiter().GetResult(); } catch { /* ignored */ }
        _shellLoopCts?.Dispose();
        _ptyWriteLock.Dispose();
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

    private static async Task WriteMessageAsync(NamedPipeServerStream stream, object payload, CancellationToken cancellationToken)
    {
        using var writer = new StreamWriter(stream, Encoding.UTF8, 1024, leaveOpen: true)
        {
            AutoFlush = true
        };
        var json = JsonSerializer.Serialize(payload, PipeJsonOpts);
        await writer.WriteLineAsync(json.AsMemory(), cancellationToken);
    }
}

internal sealed class BridgeRequest
{
    public string? Command { get; set; }
    public string? Text { get; set; }
    public bool AppendEnter { get; set; } = true;
    public bool? Ready { get; set; }
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
    public List<string> ContextPaths { get; set; } = new();
}

internal sealed class BridgeResponse
{
    public bool Ok { get; set; }
    public string? Error { get; set; }
    public BridgeStatus? Status { get; set; }
    public BridgeDiagnostics? Diagnostics { get; set; }
    public GmWorkerProposalOnlyDispatchResult? WorkerDispatch { get; set; }

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
    public string Backend { get; set; } = "ConPTYBridge";
    public string State { get; set; } = "Starting";
    public bool Ready { get; set; }
    public int HelperPid { get; set; }
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
    public List<WorkerBridgeStatus> WorkerStatuses { get; set; } = new();
}

internal sealed class BridgeDiagnostics
{
    public long OutputVersion { get; set; }
    public string RecentOutputTail { get; set; } = string.Empty;
    public string VisibleScreenText { get; set; } = string.Empty;
    public List<GmWorkerProposalInboxEntry> WorkerProposalInbox { get; set; } = new();
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
