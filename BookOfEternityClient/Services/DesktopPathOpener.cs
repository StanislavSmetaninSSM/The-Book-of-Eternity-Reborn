using System.Diagnostics;

namespace BookOfEternityClient.Services;

/// <summary>Managed association request; injectable launch boundary, no desktop detection or shell commands.</summary>
public sealed class DesktopPathOpener
{
    private readonly Action<ProcessStartInfo> _launch;
    public DesktopPathOpener(Action<ProcessStartInfo>? launch = null)
        => _launch = launch ?? (start => { using var process = Process.Start(start); });

    public void Open(string path) => _launch(new ProcessStartInfo { FileName = path, UseShellExecute = true });
}
