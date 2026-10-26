using NarrativeReadObservation = BookOfEternityClient.Services.ValidationService.NarrativeInterfaceSourceSession.NarrativeReadObservation;
using DebugPrefixReadObservation = BookOfEternityClient.Services.ValidationService.DebugPrefixSourceSession.DebugPrefixReadObservation;

namespace BookOfEternityClient.Core;

public partial class FileSystemManager
{
    internal Task<string?> ReadFileAsync(string relativePath, NarrativeReadObservation? observation) =>
        ReadObservedFileAsyncCore(relativePath, (FileReadObservation?)observation);

    internal Task<string?> ReadFileAsync(
        CanonicalWriteLease writeLease,
        string relativePath, NarrativeReadObservation? observation = null) =>
        ReadObservedFileAsyncCore(writeLease, relativePath, (FileReadObservation?)observation);

    internal Task<byte[]?> ReadFileBytesObservedAsync(string relativePath, NarrativeReadObservation observation) =>
        ReadObservedFileBytesObservedAsyncCore(relativePath, (FileReadObservation?)observation);

    internal Task<byte[]?> ReadFileBytesAsync(
        CanonicalWriteLease writeLease,
        string relativePath, NarrativeReadObservation? observation = null) =>
        ReadObservedFileBytesAsyncCore(writeLease, relativePath, (FileReadObservation?)observation);

    internal string? ReadFileSync(string relativePath, NarrativeReadObservation? observation) =>
        ReadObservedFileSyncCore(relativePath, (FileReadObservation?)observation);

    internal string? ReadFileSync(
        CanonicalWriteLease writeLease,
        string relativePath, NarrativeReadObservation? observation = null) =>
        ReadObservedFileSyncCore(writeLease, relativePath, (FileReadObservation?)observation);

    internal byte[]? ReadFileBytesSync(CanonicalWriteLease writeLease, string relativePath, NarrativeReadObservation? observation = null) =>
        ReadObservedFileBytesSyncCore(writeLease, relativePath, (FileReadObservation?)observation);

    internal byte[]? ReadFileBytesSync(string relativePath, NarrativeReadObservation? observation) =>
        ReadObservedFileBytesSyncCore(relativePath, (FileReadObservation?)observation);

    internal Task<string?> ReadDebugPrefixTextAsync(CanonicalWriteLease lease, string path,
        DebugPrefixReadObservation observation) => ReadObservedFileAsyncCore(lease, path, observation);
    internal Task<byte[]?> ReadDebugPrefixBytesAsync(CanonicalWriteLease lease, string path,
        DebugPrefixReadObservation observation) => ReadObservedFileBytesAsyncCore(lease, path, observation);
}
