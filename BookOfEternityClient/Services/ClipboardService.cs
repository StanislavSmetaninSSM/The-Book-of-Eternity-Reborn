using System.Diagnostics;
using System.Text;
using Microsoft.Extensions.Logging;

namespace BookOfEternityClient.Services;

public readonly record struct ClipboardReadResult(bool Success, string? Text, string? Error)
{
    public static ClipboardReadResult Ok(string? text) => new(true, text, null);
    public static ClipboardReadResult Fail(string error) => new(false, null, error);
}

public interface IClipboardService
{
    ClipboardReadResult TryReadText();
}

public sealed class SystemClipboardService : IClipboardService
{
    private readonly ILogger<SystemClipboardService> _logger;

    public SystemClipboardService(ILogger<SystemClipboardService> logger)
    {
        _logger = logger;
    }

    public ClipboardReadResult TryReadText()
    {
        if (!OperatingSystem.IsWindows())
            return ClipboardReadResult.Fail("Буфер обмена поддерживается только в Windows-версии клиента.");

        foreach (var executable in new[] { "powershell.exe", "pwsh.exe" })
        {
            try
            {
                var command = "$ErrorActionPreference='Stop'; [Console]::OutputEncoding=[System.Text.Encoding]::UTF8; $text = Get-Clipboard -Raw; if ($null -eq $text) { exit 3 }; [Console]::Out.Write($text)";
                var encodedCommand = Convert.ToBase64String(Encoding.Unicode.GetBytes(command));
                var startInfo = new ProcessStartInfo
                {
                    FileName = executable,
                    Arguments = $"-NoProfile -STA -EncodedCommand {encodedCommand}",
                    RedirectStandardOutput = true,
                    RedirectStandardError = true,
                    UseShellExecute = false,
                    CreateNoWindow = true,
                    StandardOutputEncoding = Encoding.UTF8,
                    StandardErrorEncoding = Encoding.UTF8
                };

                using var process = Process.Start(startInfo);
                if (process == null)
                    continue;

                var stdout = process.StandardOutput.ReadToEnd();
                var stderr = process.StandardError.ReadToEnd();
                process.WaitForExit();

                if (process.ExitCode == 0)
                {
                    var normalized = NormalizeClipboardText(stdout);
                    if (string.IsNullOrEmpty(normalized))
                        return ClipboardReadResult.Fail("Буфер обмена пуст.");

                    return ClipboardReadResult.Ok(normalized);
                }

                if (process.ExitCode == 3)
                    return ClipboardReadResult.Fail("Буфер обмена пуст.");

                _logger.LogDebug(
                    "Clipboard read via {Executable} failed with exit code {ExitCode}: {Error}",
                    executable,
                    process.ExitCode,
                    stderr);
            }
            catch (Exception ex)
            {
                _logger.LogDebug(ex, "Clipboard read via {Executable} failed", executable);
            }
        }

        return ClipboardReadResult.Fail("Не удалось прочитать текст из буфера обмена.");
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
