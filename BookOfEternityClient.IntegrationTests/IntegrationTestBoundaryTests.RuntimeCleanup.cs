using System.Diagnostics;
using System.Text.Json;
using Xunit;

namespace BookOfEternityClient.Tests;

public sealed partial class IntegrationTestBoundaryTests
{
    /// <summary>
    /// Runs the actual owned runtime cleanup child past its remaining budget and requires a timeout with confirmed process cleanup.
    /// </summary>
    /// <returns>
    /// A task completing after the delayed child is stopped, its runtime remains diagnostic evidence and the runner reports no success.
    /// </returns>
    [Fact]
    [Trait("Category", "ProcessIntegration")]
    public async Task CSharpRunner_RuntimeCleanupDeadlineStopsOwnedChildAndCannotPass()
    {
        var probe = await RunCSharpRunnerSelfTestAsync("RuntimeCleanupDeadline");
        Assert.True(probe.ExitCode == 124,
            $"Expected timeout exit 124; actual {probe.ExitCode}.\n{probe.StandardOutput}\n{probe.StandardError}");
        var results = ResultDirectoryFrom(probe.StandardOutput);
        using var document = JsonDocument.Parse(await File.ReadAllTextAsync(
            Path.Combine(results, "self-test-summary.json")));
        var summary = document.RootElement;
        Assert.Equal(124, summary.GetProperty("ExitCode").GetInt32());
        Assert.True(summary.GetProperty("TimedOut").GetBoolean());
        Assert.True(summary.GetProperty("OwnedTreeCleanupSucceeded").GetBoolean());
        Assert.False(summary.GetProperty("RuntimeCleanupSucceeded").GetBoolean());
        var cleanup = summary.GetProperty("RuntimeCleanup");
        Assert.True(cleanup.GetProperty("Attempted").GetBoolean());
        Assert.True(cleanup.GetProperty("OwnedProcessExited").GetBoolean());
        Assert.True(cleanup.GetProperty("ContainmentEmpty").GetBoolean());
        Assert.True(cleanup.GetProperty("Disposed").GetBoolean());
        Assert.False(cleanup.GetProperty("RegisteredAfterCleanup").GetBoolean());
        Assert.True(File.Exists(cleanup.GetProperty("StartedPath").GetString()));
        Assert.False(File.Exists(cleanup.GetProperty("FinishedPath").GetString()));
        Assert.True(File.Exists(Path.Combine(cleanup.GetProperty("RuntimeBasePath").GetString()!, "deadline-sentinel.txt")));
        var childId = cleanup.GetProperty("ChildProcessId").GetInt32();
        Assert.True(childId > 0);
        try
        {
            using var child = Process.GetProcessById(childId);
            Assert.True(child.HasExited, "The actual delayed runtime cleanup child remains alive.");
        }
        catch (ArgumentException)
        {
            // The process has already left the OS process table after owned containment stopped it.
        }
    }
}
