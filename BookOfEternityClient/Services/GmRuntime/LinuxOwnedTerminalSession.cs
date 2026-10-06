using System.Runtime.InteropServices;
using BookOfEternityClient.Services.GmWorkers;
using Microsoft.Win32.SafeHandles;

namespace BookOfEternityClient.Services.GmRuntime;

internal sealed class LinuxOwnedTerminalSession : IOwnedTerminalSession
{
    private readonly NativeLineageOwner _owner;
    private Task<TerminalStopEvidence>? _stop;
    private readonly object _gate = new();
    private bool _disposed;
    public TerminalIdentity Identity { get; }
    public Stream InputWriter { get; }
    public Stream OutputReader { get; }
    public Task<TerminalRootExit> RootExited { get; }
    internal LinuxOwnedTerminalSession(NativeLineageOwner owner)
    {
        _owner = owner;
        Identity = new(owner.Identity.RunId, "native-lineage", owner.Identity.Guarantee, owner.HostProcessId);
        using var master = owner.TakeTerminalMaster();
        var input = Duplicate(master); SafeFileHandle? output = null;
        try
        {
            output = Duplicate(master);
            InputWriter = new LinuxPtyStream(input, true, owner.ReportTerminalFault);
            OutputReader = new LinuxPtyStream(output, false, owner.ReportTerminalFault);
            RootExited = ObserveRootAsync(owner);
        }
        catch { input.Dispose(); output?.Dispose(); owner.ReportTerminalFault("terminal-adapter-fault"); throw; }
    }
    private static SafeFileHandle Duplicate(SafeFileHandle master)
    {
        var fd = Fcntl(master, 1030, 3); // F_DUPFD_CLOEXEC
        if (fd < 0) throw new IOException("PTY duplicate failed.");
        var safe = new SafeFileHandle((IntPtr)fd, true);
        if (Fcntl(safe, 4, 0x802) < 0) { safe.Dispose(); throw new IOException("PTY nonblocking mode failed."); }
        return safe;
    }
    private static async Task<TerminalRootExit> ObserveRootAsync(NativeLineageOwner owner) { await owner.HostExited; return new(null); }
    public ValueTask ResizeAsync(TerminalSize size, CancellationToken token)
    { token.ThrowIfCancellationRequested(); ((LinuxPtyStream)InputWriter).Resize(size); return ValueTask.CompletedTask; }
    public Task<TerminalStopEvidence> StopAndObserveAsync(CancellationToken token)
    { lock (_gate) return (_stop ??= ObserveStopAsync()).WaitAsync(token); }
    private async Task<TerminalStopEvidence> ObserveStopAsync()
    {
        var proof = await _owner.StopAndObserveAsync();
        return new(Identity, proof.State, proof.Reason, proof.CleanupComplete, proof.AuthorityRetained);
    }
    public async ValueTask DisposeAsync()
    {
        if (_disposed) return;
        var proof = await StopAndObserveAsync(CancellationToken.None);
        if (proof.State != GmWorkerStopState.StoppedWithinScope || !proof.CleanupComplete) throw new InvalidOperationException("Uncertain terminal retains original owner.");
        await _owner.SettleOutputsAsync();
        await _owner.DisposeAsync();
        await InputWriter.DisposeAsync(); await OutputReader.DisposeAsync();
        _disposed = true;
    }
    [DllImport("libc",EntryPoint="fcntl",SetLastError=true)] private static extern int Fcntl(SafeFileHandle fd,int command,int argument);
}
