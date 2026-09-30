namespace BookOfEternityClient.Core;

// API scaffold only: permits the new behavioral tests to compile before implementation.
internal sealed class TrustedLocalFileScope
{
    internal TrustedLocalFileScope(IEnumerable<string> roots, IEnumerable<string>? exactFiles = null) =>
        throw new NotImplementedException("Trusted local path scope has not been implemented.");

    internal string ValidateFile(string path, bool allowMissing = true) => throw new NotImplementedException();
    internal string EnsureDirectory(string path) => throw new NotImplementedException();
    internal void DeleteOwnedFile(string path) => throw new NotImplementedException();
    internal void DeleteOwnedTree(string path) => throw new NotImplementedException();
}
