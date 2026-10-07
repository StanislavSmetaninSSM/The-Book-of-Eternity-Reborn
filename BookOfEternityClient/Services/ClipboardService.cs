using System.Diagnostics;
using System.Text;
using Microsoft.Extensions.Logging;

namespace BookOfEternityClient.Services;

public readonly record struct ClipboardReadResult(bool Success, string? Text, string? Error)
{
    public ClipboardReadOutcome Outcome { get; init; } = Success ? ClipboardReadOutcome.Text : ClipboardReadOutcome.Error;
    public static ClipboardReadResult Ok(string? text) => new(true, text, null);
    public static ClipboardReadResult Fail(string error) => new(false, null, error);
    internal static ClipboardReadResult Refused(ClipboardReadOutcome outcome) => new(false, null, outcome switch
    {
        ClipboardReadOutcome.Empty => "Буфер обмена пуст.",
        ClipboardReadOutcome.Unavailable => "Чтение буфера обмена недоступно в этой сессии.",
        ClipboardReadOutcome.Timeout => "Время чтения буфера обмена истекло. Черновик сохранён.",
        ClipboardReadOutcome.TooLarge => "Ответ буфера обмена превышает допустимый размер.",
        ClipboardReadOutcome.CleanupUncertain => "Завершение чтения буфера не подтверждено. Новое чтение пока недоступно.",
        _ => "Не удалось прочитать текст из буфера обмена."
    }) { Outcome = outcome };
}

public enum ClipboardReadOutcome { Text, Empty, Unavailable, Error, Timeout, TooLarge, CleanupUncertain }

public interface IClipboardService
{
    ClipboardReadResult TryReadText();
}

public sealed class SystemClipboardService : IClipboardService
{
    private readonly object _gate = new();
    private ClipboardCommandReader? _cleanupDebt;

    public SystemClipboardService(ILogger<SystemClipboardService> logger)
    {
        ArgumentNullException.ThrowIfNull(logger);
    }

    public ClipboardReadResult TryReadText()
    {
        lock (_gate)
        {
            if (_cleanupDebt != null)
            {
                if (!_cleanupDebt.TrySettle())
                    return ClipboardReadResult.Refused(ClipboardReadOutcome.CleanupUncertain);
                _cleanupDebt = null;
            }
            var start = SelectReader();
            if (start == null) return ClipboardReadResult.Refused(ClipboardReadOutcome.Unavailable);
            var reader = new ClipboardCommandReader();
            var result = reader.ReadAsync(start, OperatingSystem.IsWindows()).GetAwaiter().GetResult();
            if (!reader.TrySettle())
            {
                _cleanupDebt = reader;
                return ClipboardReadResult.Refused(ClipboardReadOutcome.CleanupUncertain);
            }
            return result;
        }
    }

    private static ProcessStartInfo? SelectReader()
    {
        string? executable = null;
        string[] args = [];
        if (OperatingSystem.IsWindows())
        {
            executable = FindExecutable("powershell.exe") ?? FindExecutable("pwsh.exe");
            const string command = "$ErrorActionPreference='Stop'; [Console]::OutputEncoding=[System.Text.Encoding]::UTF8; $text = Get-Clipboard -Raw; if ($null -eq $text) { exit 3 }; [Console]::Out.Write($text)";
            args = ["-NoProfile", "-STA", "-EncodedCommand", Convert.ToBase64String(Encoding.Unicode.GetBytes(command))];
        }
        else if (OperatingSystem.IsLinux())
        {
            if (!string.IsNullOrWhiteSpace(Environment.GetEnvironmentVariable("WAYLAND_DISPLAY")))
            {
                executable = FindExecutable("wl-paste");
                args = ["--no-newline", "--type", "text"];
            }
            if (executable == null && !string.IsNullOrWhiteSpace(Environment.GetEnvironmentVariable("DISPLAY")))
            {
                executable = FindExecutable("xclip");
                args = ["-selection", "clipboard", "-out", "-target", "UTF8_STRING"];
                if (executable == null)
                {
                    executable = FindExecutable("xsel");
                    args = ["--clipboard", "--output"];
                }
            }
        }
        if (executable == null) return null;
        var start = new ProcessStartInfo(executable)
        {
            UseShellExecute = false, CreateNoWindow = true,
            RedirectStandardInput = true, RedirectStandardOutput = true, RedirectStandardError = true
        };
        foreach (var arg in args) start.ArgumentList.Add(arg);
        return start;
    }

    private static string? FindExecutable(string name)
    {
        foreach (var directory in (Environment.GetEnvironmentVariable("PATH") ?? "").Split(Path.PathSeparator))
        {
            if (string.IsNullOrWhiteSpace(directory)) continue;
            try
            {
                var path = Path.GetFullPath(Path.Combine(directory.Trim('"'), name));
                if (File.Exists(path)) return path;
            }
            catch (Exception ex) when (ex is ArgumentException or IOException or UnauthorizedAccessException) { }
        }
        return null;
    }

    internal static string NormalizeClipboardText(string? text)
    {
        if (string.IsNullOrEmpty(text))
            return string.Empty;

        return text
            .Replace("\r\n", "\n")
            .Replace('\r', '\n')
            .TrimEnd('\n');
    }
}
