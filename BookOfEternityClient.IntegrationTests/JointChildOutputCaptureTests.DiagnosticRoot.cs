using Xunit;

namespace BookOfEternityClient.Tests;

public sealed partial class JointChildOutputCaptureTests
{
    /// <summary>
    /// Honors an explicit artifact parent independently of the runner's configured default destination.
    /// </summary>
    /// <returns>
    /// Completion after both exact streams are finalized outside the disposable game fixture.
    /// </returns>
    [Fact]
    public async Task DiagnosticRoot_ExplicitArtifactParentKeepsPriority()
    {
        using var fixture = new OutputTestFixture();
        using var artifacts = new OutputTestFixture();
        var capture = new JointChildOutputCapture(fixture.Path, new { Role = "explicit-parent-control" }, artifacts.Path);
        var output = await BookOfEternityClient.Services.GmWorkers.FinitePipeOutput.DrainAsync(new StringReader("all-output"), capture.StandardOutput);
        var error = await BookOfEternityClient.Services.GmWorkers.FinitePipeOutput.DrainAsync(new StringReader("all-error"), capture.StandardError);
        await capture.FinishAsync(output, error);
        Assert.Equal(Path.GetFullPath(artifacts.Path), Path.GetDirectoryName(capture.DirectoryPath));
        Assert.Null(capture.PreserveFailure(null));
        Assert.Equal("all-output", File.ReadAllText(capture.OutputReference.Path));
        Assert.Equal("all-error", File.ReadAllText(capture.ErrorReference.Path));
        Assert.True(capture.OutputReference.PersistenceComplete);
        Assert.True(capture.ErrorReference.PersistenceComplete);
    }

    /// <summary>
    /// Resolves configured and default roots without creating either destination or changing the environment.
    /// </summary>
    [Fact]
    public void DiagnosticRoot_ResolvesConfiguredAndDefaultWithoutSideEffects()
    {
        var configured = Path.Combine(Path.GetTempPath(), "boe-output-root-" + Guid.NewGuid().ToString("N"));
        Assert.Equal(Path.GetFullPath(Path.Combine(configured, "joint-child-output")),
            JointChildOutputCapture.ResolveDefaultArtifactParent(configured));
        Assert.False(Directory.Exists(configured));
        Assert.Equal(Path.GetFullPath(Path.Combine(TestRepoPaths.RepoRoot, "TestResults", "joint-child-output")),
            JointChildOutputCapture.ResolveDefaultArtifactParent(null));
    }

    /// <summary>
    /// Rejects invalid diagnostic roots before creating output artifacts.
    /// </summary>
    /// <param name="root">
    /// The empty, whitespace or relative root that cannot designate an external diagnostic destination.
    /// </param>
    [Theory]
    [InlineData("")]
    [InlineData(" ")]
    [InlineData("relative-output")]
    public void DiagnosticRoot_RejectsInvalidValues(string root) =>
        Assert.Throws<InvalidDataException>(() => JointChildOutputCapture.ResolveDefaultArtifactParent(root));
}
