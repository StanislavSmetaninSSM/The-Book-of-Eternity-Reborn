namespace BookOfEternityClient.Core;

public partial class FileSystemManager
{
    // RED preparation: current ordinary writer behavior, not a contention exemption.
    internal bool IsOriginalCanonicalLockContention(Exception failure) => false;

    internal async Task<bool> TryPublishGmBridgeStatusAsync(byte[] content)
    {
        await WriteFileAtomicBytesAsync("game_state/control/gm_bridge_status.json", content);
        return true;
    }
}
