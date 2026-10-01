namespace BookOfEternityClient.Core;

internal sealed record LocalSessionGenerationSnapshot(TrustedLocalGeneration Binding, byte[]? Bytes);

public partial class FileSystemManager
{
    // This reader is below the generation fence: it must not verify that fence
    // or enter the ordinary canonical byte reader, which calls it in turn.
    internal LocalSessionGenerationSnapshot ReadLocalGenerationSnapshot(CanonicalWriteLease lease) =>
        throw new NotImplementedException();
}
