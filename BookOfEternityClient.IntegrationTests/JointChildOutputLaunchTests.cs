using System.Diagnostics;
using System.Text.Json;
using BookOfEternityClient.Services.GmWorkers;
using Xunit;

namespace BookOfEternityClient.Tests;

/// <summary>
/// Checks no-child refusals and independent join framing without launching game processes.
/// </summary>
public sealed class JointChildOutputLaunchTests
{
    /// <summary>
    /// Exited roots require actual EOF and an explicit ownership join before reports can claim Joined.
    /// </summary>
    /// <param name="eof">
    /// Whether both exclusive drains actually observed EOF.
    /// </param>
    /// <param name="joined">
    /// Whether the original owner positively recorded a join.
    /// </param>
    /// <param name="expected">
    /// Expected reported join state.
    /// </param>
    [Theory]
    [InlineData(false, false, false)]
    [InlineData(true, false, false)]
    [InlineData(false, true, false)]
    [InlineData(true, true, true)]
    public void ExitedRootCannotManufactureJoin(bool eof, bool joined, bool expected)
    {
        var drain = new FinitePipeDrainResult(eof, eof ? null : new IOException("pipe read"), null);
        var result = JointInterfaceCrashProcessSupport.ObserveDirectCompletion(true, joined, drain, drain);
        Assert.True(result.Exited); Assert.Equal(eof, result.Eof); Assert.Equal(expected, result.Joined);
    }

    /// <summary>
    /// A final budget refusal closes both prepared writers and retains the exact original exception.
    /// </summary>
    [Fact]
    public async Task FinalBudgetRefusalFinalizesUnstartedCapture()
    {
        var fixture = Path.Combine(TestRepoPaths.RepoRoot, "TestResults", "joint-child-launch-fixtures", Guid.NewGuid().ToString("N"));
        var capture = new JointChildOutputCapture(fixture, new { Role = "final-budget-refusal-control" });
        var fault = new TimeoutException("original shared Windows budget exhausted");
        var error = await Assert.ThrowsAsync<TimeoutException>(() => JointInterfaceCrashProcessSupport.StartPreparedDirectChildAsync(capture, () => throw fault));
        Assert.Same(fault, error); AssertUnstarted(capture);
    }

    /// <summary>
    /// Missing native configuration settles prepared capture without allocating or reporting an original owner.
    /// </summary>
    [Fact]
    public async Task MissingNativePackageFinalizesUnstartedCapture()
    {
        var fixture = Path.Combine(TestRepoPaths.RepoRoot, "TestResults", "joint-child-launch-fixtures", Guid.NewGuid().ToString("N"));
        var capture = new JointChildOutputCapture(fixture, new { Role = "missing-native-package-control" });
        var owners = 0; var reports = 0;
        await Assert.ThrowsAsync<InvalidDataException>(() => JointInterfaceCrashProcessSupport.RunNativeAsync(new ProcessStartInfo("unused"), capture,
            _ => owners++, _ => throw new InvalidOperationException("No owner may be disposed."), _ => reports++, () => null));
        Assert.Equal(0, owners); Assert.Equal(0, reports); AssertUnstarted(capture);
    }

    /// <summary>
    /// Requires closed empty artifacts with no invented EOF or pipe read failure after startup refusal.
    /// </summary>
    /// <param name="capture">
    /// The original capture prepared before a launch was refused.
    /// </param>
    private static void AssertUnstarted(JointChildOutputCapture capture)
    {
        foreach (var reference in new[] { capture.OutputReference, capture.ErrorReference })
        {
            Assert.False(reference.Eof); Assert.False(reference.PersistenceComplete);
            Assert.Equal(0L, reference.StoredBytes); Assert.NotNull(reference.Sha256); Assert.Null(reference.ReadFailure);
            using var exclusive = new FileStream(reference.Path, FileMode.Open, FileAccess.ReadWrite, FileShare.None);
            Assert.Equal(0, exclusive.Length);
        }
        using var manifest = JsonDocument.Parse(File.ReadAllText(Path.Combine(capture.DirectoryPath, "capture.json")));
        Assert.False(manifest.RootElement.GetProperty("Outcome").GetProperty("ChildStarted").GetBoolean());
        Assert.False(string.IsNullOrWhiteSpace(manifest.RootElement.GetProperty("Outcome").GetProperty("PrimaryFailure").GetString()));
        Assert.True(capture.EvidenceRetentionRequired);
    }
}
