using Microsoft.Win32.SafeHandles;
using BookOfEternityClient.Services.GmWorkers;
namespace BookOfEternityClient.Services.GmRuntime;
// Minted only by checking the original unreaped native owner, never from PID/JSON.
internal sealed class NativeHeldTerminalPidfd : IDisposable
{
    private readonly NativeLineageOwner _original;
    private NativeHeldTerminalPidfd(NativeLineageOwner original,SafeFileHandle descriptor) {
        _original=original;Descriptor=descriptor;Pid=original.HostProcessId;RunId=original.Identity.RunId;
    }
    internal static NativeHeldTerminalPidfd FromOriginal(NativeLineageOwner original)=>new(original,original.DuplicateHeldTerminalPidfd());
    internal SafeFileHandle Descriptor {get;}
    internal int Pid {get;}
    internal string RunId {get;}
    internal void ValidateHeld()=>_original.RequireOriginalHeldTerminal();
    internal TimeSpan Remaining=>_original.HeldTerminalRemaining;
    public void Dispose()=>Descriptor.Dispose();
}
