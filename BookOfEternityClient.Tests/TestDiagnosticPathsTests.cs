using Xunit;

namespace BookOfEternityClient.Tests;

/// <summary>
/// Checks diagnostic configuration using independent paths without creating output or game state.
/// </summary>
public sealed class TestDiagnosticPathsTests
{
    /// <summary>
    /// Requires explicit configuration to override only diagnostic storage and preserves the legacy fallback.
    /// </summary>
    [Fact]
    public void ExplicitAndFallbackRootsAreResolvedWithoutCreatingDirectories()
    {
        var unusedRoot = Path.Combine(Path.GetTempPath(), "boe-diagnostic-path-" + Guid.NewGuid().ToString("N"));
        var repositoryRoot = Path.Combine(unusedRoot, "repository");
        var diagnosticRoot = Path.Combine(unusedRoot, "diagnostics");
        Assert.Equal(Path.GetFullPath(diagnosticRoot), TestDiagnosticPaths.ResolveRoot(diagnosticRoot, repositoryRoot));
        Assert.Equal(Path.GetFullPath(Path.Combine(repositoryRoot, "TestResults")),
            TestDiagnosticPaths.ResolveRoot(null, repositoryRoot));
        Assert.False(Directory.Exists(unusedRoot));
    }

    /// <summary>
    /// Requires invalid explicit configuration to fail before filesystem output can be allocated.
    /// </summary>
    /// <param name="root">
    /// An empty, whitespace or relative diagnostic setting.
    /// </param>
    [Theory]
    [InlineData("")]
    [InlineData(" ")]
    [InlineData("relative/diagnostics")]
    public void InvalidExplicitRootsAreRejected(string root) =>
        Assert.Throws<InvalidDataException>(() => TestDiagnosticPaths.ResolveRoot(root));

    /// <summary>
    /// Requires the optional per-call fallback control to retain absolute-path semantics.
    /// </summary>
    [Fact]
    public void RelativeRepositoryControlIsRejected() =>
        Assert.Throws<InvalidDataException>(() => TestDiagnosticPaths.ResolveRoot(null, "relative-repository"));
}
