using Microsoft.Win32.SafeHandles;

namespace BookOfEternityClient.Services.GmRuntime;

// This wrapper is minted only from the original unreaped native owner.
internal sealed class NativeHeldTerminalPidfd(SafeFileHandle descriptor, int pid, string runId) : IDisposable
{
    internal SafeFileHandle Descriptor { get; } = descriptor;
    internal int Pid { get; } = pid;
    internal string RunId { get; } = runId;
    public void Dispose() => Descriptor.Dispose();
}
