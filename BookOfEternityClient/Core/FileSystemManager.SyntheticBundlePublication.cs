namespace BookOfEternityClient.Core;

public partial class FileSystemManager
{
    // Explicit fixture-only trusted-local operation. The default descriptor-bound
    // method and its Windows capability check remain separate and unchanged.
    internal async Task MoveSyntheticRuntimeBundleIntoCanonicalSessionAsync(CanonicalWriteLease lease,
        string admittedFixtureRoot, string sourceDirectory, string destinationRelativePath)
    {
        if (!OperatingSystem.IsLinux())
            throw new PlatformNotSupportedException("Synthetic bundle publication requires Linux.");
        if (!string.Equals(Path.GetFullPath(admittedFixtureRoot), BasePath, StringComparison.Ordinal))
            throw new InvalidDataException("Synthetic bundle publication belongs to another fixture root.");
        ValidateLease();
        EnsureSafeCanonicalRelativePath(destinationRelativePath);
        var destinationParts = destinationRelativePath.Split('/');
        if (destinationParts.Length != 2 || destinationParts[0] != "worker_proposals" ||
            !IsBundleId(destinationParts[1]) || destinationParts[1] == "inbox")
            throw new InvalidDataException("Synthetic bundle destination must be one nonreserved proposal ID.");

        var stagingArea = Path.Combine(RuntimeRootPath, "proposal-staging");
        var runtimeScope = new TrustedLocalFileScope([stagingArea]);
        var source = runtimeScope.ValidateDirectory(sourceDirectory, allowMissing: false);
        var sourceParts = Path.GetRelativePath(stagingArea, source).Split(Path.DirectorySeparatorChar);
        if (sourceParts.Length != 2 || !Guid.TryParseExact(sourceParts[0], "N", out _) ||
            sourceParts[1] != destinationParts[1])
            throw new InvalidDataException("Synthetic source must be the matching bundle in a private proposal staging directory.");
        var destination = ResolvePath(destinationRelativePath);
        var canonicalScope = new TrustedLocalFileScope([GameSessionPath]);
        using var mutation = new InProcessMutationRegistration(destination);
        await InvokeBeforeCanonicalMutationBoundaryAsync(destinationRelativePath);
        ValidateLease();
        EnsureCanonicalMutationBoundary(destinationRelativePath, destination);
        canonicalScope.EnsureDirectory(Path.GetDirectoryName(destination)!);
        ValidateNamespace();
        await InvokeAfterCanonicalMutationBoundaryValidatedAsync(destinationRelativePath);
        // Awaited hooks cannot carry an old lease or unchecked namespace into the
        // native commit. By-name validation retains the accepted trusted-local limit.
        ValidateLease();
        EnsureCanonicalMutationBoundary(destinationRelativePath, destination);
        ValidateNamespace();
        LinuxCreateOnlyDirectoryRename.Move(source, destination);

        void ValidateLease()
        {
            VerifyCurrentSessionOperation(lease);
            lease.EnsureNoPendingLocalDecision();
            if (lease.IsLegacyStorageRecovery || !UsesTrustedLocalWriter(lease, destinationRelativePath))
                throw new InvalidOperationException("Synthetic bundle publication requires an ordinary canonical lease.");
        }
        void ValidateNamespace()
        {
            EnsureRuntimePathIsSafe(source);
            runtimeScope.ValidateDirectory(source, allowMissing: false);
            _ = EnumerateLocalTreeFiles(runtimeScope, source);
            if (canonicalScope.ObserveNamespace(destination).Kind != TrustedLocalNamespaceKind.Missing)
                throw new IOException("Synthetic bundle destination already exists.");
            canonicalScope.ValidateDirectory(Path.GetDirectoryName(destination)!, allowMissing: false);
        }
    }

    private static bool IsBundleId(string value) => value.Length != 0 &&
        value.All(ch => ch is >= 'a' and <= 'z' or >= '0' and <= '9' or '_' or '-');
}
