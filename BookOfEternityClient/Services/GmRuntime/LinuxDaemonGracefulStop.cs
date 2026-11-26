using System.Runtime.InteropServices;

namespace BookOfEternityClient.Services.GmRuntime;

/// <summary>Requests an orderly loop return; never cancels a live PowerShell pipeline.</summary>
public sealed class LinuxDaemonGracefulStop : IDisposable
{
    private readonly PosixSignalRegistration _registration;
    private volatile bool _requested;

    public bool Requested => _requested;

    public LinuxDaemonGracefulStop()
    {
        if (!OperatingSystem.IsLinux())
            throw new PlatformNotSupportedException("The signal-backed daemon stop is Linux-only.");
        _registration = PosixSignalRegistration.Create(PosixSignal.SIGTERM, context =>
        {
            context.Cancel = true;
            _requested = true;
        });
    }

    public void Dispose() => _registration.Dispose();
}
