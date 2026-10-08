using System.Diagnostics;
using System.Text;
using Microsoft.Win32.SafeHandles;

namespace BookOfEternityClient.Services;

// Owns only the original foreground read command. External desktop services are not owned here.
internal sealed class ClipboardCommandReader(Func<Process, SafeFileHandle?> capture)
{
    private Process? _process;
    private readonly CancellationTokenSource _io = new();
    private Task? _exit;
    private Task<byte[]>? _stdout, _stderr;
    private bool _disposed;
    private SafeFileHandle? _identity;

    internal async Task<ClipboardReadResult> ReadAsync(ProcessStartInfo start, bool windows)
    {
        var deadline = Task.Delay(TimeSpan.FromSeconds(2));
        var outcome = ClipboardReadOutcome.Error;
        try
        {
            _process = Process.Start(start);
            if (_process == null) return ClipboardReadResult.Refused(outcome);
            _exit = _process.WaitForExitAsync();
            _stdout = ReadBounded(_process.StandardOutput.BaseStream, 1024 * 1024, true, _io.Token);
            _stderr = ReadBounded(_process.StandardError.BaseStream, 64 * 1024, false, _io.Token);
            if (OperatingSystem.IsLinux()) _identity = capture(_process);
            _process.StandardInput.Close(); // readers never consume the client's input
            var pending = new List<Task> { _exit, _stdout, _stderr };
            while (pending.Count > 0)
            {
                var completed = await Task.WhenAny(pending.Append(deadline));
                if (completed == deadline)
                {
                    outcome = ClipboardReadOutcome.Timeout;
                    break;
                }
                await completed; // observe faults immediately, even if the other pipe is still open
                pending.Remove(completed);
            }
            if (pending.Count == 0)
            {
                if (windows && _process.ExitCode == 3) outcome = ClipboardReadOutcome.Empty;
                else if (_process.ExitCode == 0)
                {
                    var text = SystemClipboardService.NormalizeClipboardText(new UTF8Encoding(false, true).GetString(await _stdout));
                    return string.IsNullOrEmpty(text)
                        ? ClipboardReadResult.Refused(ClipboardReadOutcome.Empty)
                        : ClipboardReadResult.Ok(text);
                }
            }
        }
        catch (OutputLimitException) { outcome = ClipboardReadOutcome.TooLarge; }
        catch (Exception ex) when (ex is IOException or System.ComponentModel.Win32Exception or InvalidOperationException or DecoderFallbackException or OperationCanceledException)
        { outcome = ClipboardReadOutcome.Error; }

        // Cancel only our I/O and signal only our original Process, never after confirmed exit/reap.
        _io.Cancel();
        if (_process != null && _exit is not { IsCompletedSuccessfully: true })
        {
            try
            {
                if (!_process.HasExited)
                {
                    if (OperatingSystem.IsLinux())
                    {
                        if (_identity != null) LinuxClipboardReaderIdentity.Stop(_identity);
                    }
                    else if (OperatingSystem.IsWindows()) _process.Kill();
                }
            }
            catch (Exception ex) when (ex is InvalidOperationException or System.ComponentModel.Win32Exception) { }
        }
        var tasks = OwnedTasks();
        var settlement = Task.WhenAll(tasks);
        await Task.WhenAny(settlement, Task.Delay(TimeSpan.FromSeconds(1)));
        if (settlement.IsCompleted) _ = settlement.Exception;
        return ClipboardReadResult.Refused(outcome);
    }

    internal bool TrySettle()
    {
        if (_disposed) return true;
        if (_process != null && (_exit is not { IsCompletedSuccessfully: true } || OwnedTasks().Any(t => !t.IsCompleted)))
            return false;
        foreach (var task in OwnedTasks()) _ = task.Exception; // observe completed canceled/faulted reads
        _process?.Dispose();
        _identity?.Dispose();
        _io.Dispose();
        _disposed = true;
        return true;
    }

    private Task[] OwnedTasks() => new Task?[] { _exit, _stdout, _stderr }.OfType<Task>().ToArray();

    private static async Task<byte[]> ReadBounded(Stream stream, int limit, bool retain, CancellationToken cancellation)
    {
        using var output = new MemoryStream();
        var buffer = new byte[8192];
        var length = 0;
        while (true)
        {
            var read = await stream.ReadAsync(buffer, cancellation);
            if (read == 0) return retain ? output.ToArray() : [];
            length += read;
            if (length > limit) throw new OutputLimitException();
            if (retain) output.Write(buffer, 0, read);
        }
    }

    private sealed class OutputLimitException : IOException;
}
