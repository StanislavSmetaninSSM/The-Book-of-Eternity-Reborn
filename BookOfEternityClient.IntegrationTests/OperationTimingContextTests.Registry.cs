using BookOfEternityClient.Core;
using BookOfEternityClient.Services;
using Xunit;

namespace BookOfEternityClient.Tests;

public sealed partial class OperationTimingContextTests
{
    /// <summary>
    /// Preserves the registry's original null guards and restores passive context after a refused entry.
    /// </summary>
    /// <param name="nullFileSystem">
    /// Whether the filesystem is null; otherwise, only the canonical write lease is null.
    /// </param>
    [Theory]
    [InlineData(true)]
    [InlineData(false)]
    public void RegistryCandidate_PreservesOriginalNullGuards(bool nullFileSystem)
    {
        using var fixture = new Fixture(_output);
        var trace = new Trace(_output);
        var files = fixture.Create(trace.Observe);
        var failure = Assert.Throws<ArgumentNullException>(() =>
            AcceptedTurnAuthorityRegistry.CreateJointRepairCommonCandidate(
                nullFileSystem ? null! : files, null!, null!, null!));
        Assert.Equal(nullFileSystem ? "fileSystem" : "writeLease", failure.ParamName);
        Assert.Null(FileSystemManager.MeasureCurrentOperation("after-registry-refusal"));
        if (nullFileSystem) Assert.Empty(trace.Events);
        else AssertEvents(trace,
            ("JointCandidate.Registry.Total", "begin"), ("JointCandidate.Registry.State", "begin"),
            ("JointCandidate.Registry.State", "end"), ("JointCandidate.Registry.Total", "end"));
        AssertBalanced(trace);
    }
}
