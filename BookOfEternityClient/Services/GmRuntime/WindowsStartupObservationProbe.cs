using System.Diagnostics;
using System.Text;
using Microsoft.Win32.SafeHandles;

namespace BookOfEternityClient.Services.GmRuntime;

// Bridge retains this slot beyond a refused coordinator. A timeout/kill request
// cannot discard an unsettled original command or admit another launch.
internal sealed class WindowsStartupObservationProbe
{
    private readonly Func<ProcessStartInfo> _start;
    private readonly Func<Process, SafeFileHandle?> _captureLinuxIdentity;
    private readonly TimeSpan _observationBudget, _settlementBudget;
    private readonly SemaphoreSlim _gate = new(1, 1);
    private ForegroundOwner? _owner;

    internal WindowsStartupObservationProbe() : this(CreateWindowsStart,
        TimeSpan.FromSeconds(5), TimeSpan.FromSeconds(1)) { }

    // Controlled foreground processes exercise ownership on Linux; they do not
    // qualify the Windows WMI provider or ConPTY/Job adapter.
    internal WindowsStartupObservationProbe(Func<ProcessStartInfo> start,
        TimeSpan observationBudget, TimeSpan settlementBudget,
        Func<Process, SafeFileHandle?>? captureLinuxIdentity = null)
    {
        _start = start; _observationBudget = observationBudget; _settlementBudget = settlementBudget;
        _captureLinuxIdentity = captureLinuxIdentity ?? LinuxClipboardReaderIdentity.Capture;
    }

    internal async Task<WindowsStartupObservation> ReadAsync(CancellationToken token)
    {
        await _gate.WaitAsync(token);
        try
        {
            if (!SettleOwner()) throw new IOException("Original startup observation command has unsettled cleanup.");
            token.ThrowIfCancellationRequested();
            var start = _start();
            _owner = new(_captureLinuxIdentity, _observationBudget, _settlementBudget);
            var bytes = await _owner.ReadAsync(start, token);
            if (!SettleOwner()) throw new IOException("Original startup observation command has unsettled cleanup.");
            return WindowsStartupObservation.Parse(bytes);
        }
        finally { _gate.Release(); }
    }

    internal bool TrySettle()
    {
        if (!_gate.Wait(0)) return false;
        try { return SettleOwner(); }
        finally { _gate.Release(); }
    }

    private bool SettleOwner()
    {
        if (_owner != null && !_owner.TrySettle()) return false;
        _owner = null;
        return true;
    }

    private static ProcessStartInfo CreateWindowsStart()
    {
        if (!OperatingSystem.IsWindows()) throw new PlatformNotSupportedException("Windows WMI startup observation requires Windows.");
        var executable = Path.GetFullPath(Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.System),
            "WindowsPowerShell", "v1.0", "powershell.exe"));
        if (!File.Exists(executable)) throw new IOException("Windows startup observation provider is unavailable.");
        const string script = """
            $ErrorActionPreference='Stop'; $WarningPreference='Stop'; $ProgressPreference='SilentlyContinue'
            [Console]::OutputEncoding=[System.Text.UTF8Encoding]::new($false)
            $os=@(CimCmdlets\Get-CimInstance -Namespace root/cimv2 -ClassName Win32_OperatingSystem -Property LastBootUpTime -OperationTimeoutSec 5)
            if ($os.Count -ne 1 -or $os[0].LastBootUpTime -isnot [datetime]) { throw 'Startup observation unavailable' }
            $value=$os[0].LastBootUpTime.ToUniversalTime().ToString("yyyy-MM-dd'T'HH:mm:ss.fffffff'Z'",[Globalization.CultureInfo]::InvariantCulture)
            [Console]::Out.Write('wmi-lastboot-v1:'+$value)
            """;
        var start = new ProcessStartInfo(executable)
        {
            UseShellExecute = false, CreateNoWindow = true,
            RedirectStandardInput = true, RedirectStandardOutput = true, RedirectStandardError = true
        };
        foreach (var argument in new[] { "-NoLogo", "-NoProfile", "-NonInteractive", "-EncodedCommand",
                     Convert.ToBase64String(Encoding.Unicode.GetBytes(script)) }) start.ArgumentList.Add(argument);
        return start;
    }

    private sealed class ForegroundOwner(Func<Process, SafeFileHandle?> captureLinuxIdentity,
        TimeSpan observationBudget, TimeSpan settlementBudget)
    {
        private readonly CancellationTokenSource _io = new();
        private Process? _process;
        private SafeFileHandle? _linuxIdentity;
        private Task? _exit;
        private Task<byte[]>? _stdout, _stderr;
        private bool _disposed;

        internal async Task<byte[]> ReadAsync(ProcessStartInfo start, CancellationToken token)
        {
            try
            {
                token.ThrowIfCancellationRequested();
                _process = Process.Start(start) ?? throw new IOException("Startup observation command did not start.");
                _exit = _process.WaitForExitAsync();
                _stdout = ReadBoundedAsync(_process.StandardOutput.BaseStream, 4096, _io.Token);
                _stderr = ReadBoundedAsync(_process.StandardError.BaseStream, 8192, _io.Token);
                _process.StandardInput.Close();
                if (OperatingSystem.IsLinux())
                {
                    _linuxIdentity = captureLinuxIdentity(_process);
                    if (_linuxIdentity == null && !_process.HasExited)
                        throw new IOException("Controlled foreground process identity is unavailable.");
                }
                var deadline = Task.Delay(observationBudget, token);
                var pending = new List<Task> { _exit, _stdout, _stderr };
                while (pending.Count > 0)
                {
                    var completed = await Task.WhenAny(pending.Append(deadline));
                    if (completed == deadline)
                    {
                        token.ThrowIfCancellationRequested();
                        throw new TimeoutException("Startup observation exceeded its application budget.");
                    }
                    await completed;
                    pending.Remove(completed);
                }
                token.ThrowIfCancellationRequested();
                if (_process.ExitCode != 0) throw new IOException("Startup observation provider failed.");
                return await _stdout;
            }
            catch
            {
                _io.Cancel();
                if (_process != null && _exit is not { IsCompletedSuccessfully: true })
                {
                    try
                    {
                        if (!_process.HasExited)
                        {
                            if (OperatingSystem.IsWindows()) _process.Kill();
                            else if (_linuxIdentity != null) LinuxClipboardReaderIdentity.Stop(_linuxIdentity);
                        }
                    }
                    catch (Exception failure) when (failure is InvalidOperationException or System.ComponentModel.Win32Exception) { }
                }
                var settlement = Task.WhenAll(OwnedTasks());
                await Task.WhenAny(settlement, Task.Delay(settlementBudget));
                if (settlement.IsCompleted) _ = settlement.Exception;
                throw;
            }
        }

        internal bool TrySettle()
        {
            if (_disposed) return true;
            if (_process != null && (_exit is not { IsCompletedSuccessfully: true } || OwnedTasks().Any(task => !task.IsCompleted)))
                return false;
            foreach (var task in OwnedTasks()) _ = task.Exception;
            _process?.Dispose(); _linuxIdentity?.Dispose(); _io.Dispose(); _disposed = true;
            return true;
        }

        private Task[] OwnedTasks() => new Task?[] { _exit, _stdout, _stderr }.OfType<Task>().ToArray();

        private static async Task<byte[]> ReadBoundedAsync(Stream stream, int limit, CancellationToken token)
        {
            using var output = new MemoryStream();
            var buffer = new byte[1024];
            while (true)
            {
                var read = await stream.ReadAsync(buffer, token);
                if (read == 0) return output.ToArray();
                if (output.Length + read > limit) throw new IOException("Startup observation output exceeded its bound.");
                output.Write(buffer, 0, read);
            }
        }
    }
}
