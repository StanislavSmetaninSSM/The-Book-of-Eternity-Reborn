using BookOfEternityClient.Core;

namespace BookOfEternityClient.Services;

internal sealed record PreparedSystemModManifest(byte[] Bytes, IReadOnlyList<string> EnabledFiles,
    IReadOnlyList<SystemModService.SystemModDescriptor> ActiveMods);

public sealed partial class SystemModService
{
    internal Task<PreparedSystemModManifest> PrepareManifestForGmAsync(
        FileSystemManager.CanonicalWriteLease lease, IReadOnlyCollection<string> enabledFiles, byte[]? currentBytes) =>
        throw new NotImplementedException("Read-only mod manifest preparation is not implemented.");
}
