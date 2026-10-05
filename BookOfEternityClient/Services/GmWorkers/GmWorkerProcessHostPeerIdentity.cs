using Microsoft.Win32.SafeHandles;

namespace BookOfEternityClient.Services.GmWorkers;

internal readonly record struct GmWorkerLinuxPeerQueryResult(
    int ReturnCode, int Error, uint Length, int ProcessId, uint EffectiveUserId);

// Test-first scaffold; not connected to admission until the policy RED is observed.
internal static class GmWorkerProcessHostPeerIdentity
{
    internal static void Validate(SafePipeHandle pipe, int expectedProcessId, uint expectedUserId, string channelName) =>
        throw new NotImplementedException("Worker host peer identity adapter is not implemented.");

    internal static void ValidateLinuxResult(GmWorkerLinuxPeerQueryResult result,
        int expectedProcessId, uint expectedUserId, string channelName) =>
        throw new NotImplementedException("Worker host peer identity policy is not implemented.");
}
