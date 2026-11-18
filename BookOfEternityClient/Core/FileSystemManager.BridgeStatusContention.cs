namespace BookOfEternityClient.Core;

public partial class FileSystemManager
{
    private sealed class CanonicalLockContention : IOException
    {
        internal readonly FileSystemManager Manager;
        internal readonly object Occurrence;
        internal CanonicalLockContention(FileSystemManager manager, object occurrence, IOException original)
            : base(original.Message, original)
        {
            Manager = manager; Occurrence = occurrence; HResult = original.HResult;
        }
    }

    // Used only at the final exact FileStream constructor catch. Linux flock EWOULDBLOCK
    // maps to raw errno 11 in the qualified .NET runtime, not a Win32 low-word code.
    private static bool IsSupportedConstructorSharingFailure(IOException failure) =>
        OperatingSystem.IsLinux() ? failure.HResult == 11 :
        OperatingSystem.IsWindows() && (failure.HResult & 0xffff) is 32 or 33;

    internal bool IsOriginalCanonicalLockContention(Exception failure) =>
        failure is CanonicalLockContention contention && ReferenceEquals(contention.Manager, this);

    internal Task<bool> TryPublishGmBridgeStatusAsync(byte[] content) =>
        WriteOrdinaryFileAtomicBytesAsync("game_state/control/gm_bridge_status.json", content, null,
            deferOriginalContention: true);
}
