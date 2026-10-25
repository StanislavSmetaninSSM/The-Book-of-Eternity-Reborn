namespace BookOfEternityClient.Tests;

/// <summary>
/// Resolves diagnostic storage without allocating fixtures or changing repository identity.
/// </summary>
internal static class TestDiagnosticPaths
{
    /// <summary>
    /// Gets the configured absolute diagnostic root, or the legacy repository TestResults root.
    /// </summary>
    internal static string Root => ResolveRoot(Environment.GetEnvironmentVariable("BOE_TEST_DIAGNOSTIC_ROOT"));

    /// <summary>
    /// Validates explicit configuration and resolves a root without filesystem side effects.
    /// </summary>
    /// <param name="requestedRoot">
    /// An absolute nonempty diagnostic root, or <see langword="null"/> for the repository fallback.
    /// </param>
    /// <param name="repositoryRootForControl">
    /// An absolute per-call repository root used only by fallback path controls; null uses the actual repository.
    /// </param>
    /// <returns>
    /// The normalized root; invalid configuration throws before callers create output.
    /// </returns>
    internal static string ResolveRoot(string? requestedRoot, string? repositoryRootForControl = null)
    {
        if (requestedRoot is not null)
        {
            if (string.IsNullOrWhiteSpace(requestedRoot) || !Path.IsPathFullyQualified(requestedRoot))
                throw new InvalidDataException("BOE_TEST_DIAGNOSTIC_ROOT must be a nonempty absolute path when provided.");
            return Path.GetFullPath(requestedRoot);
        }

        var repositoryRoot = repositoryRootForControl ?? TestRepoPaths.RepoRoot;
        if (!Path.IsPathFullyQualified(repositoryRoot))
            throw new InvalidDataException("A diagnostic control repository root must be absolute.");
        return Path.GetFullPath(Path.Combine(repositoryRoot, "TestResults"));
    }
}
