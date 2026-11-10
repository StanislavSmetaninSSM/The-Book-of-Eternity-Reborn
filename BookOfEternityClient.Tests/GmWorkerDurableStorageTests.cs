using System.Text.Json;
using Xunit;
using Xunit.Abstractions;

namespace BookOfEternityClient.Tests;

public sealed class GmWorkerDurableStorageTests(ITestOutputHelper output)
{
    [Theory]
    [InlineData("inbox_unknown")]
    [InlineData("derived_audit_unknown")]
    [InlineData("inbox_unknown_dispose")]
    [InlineData("terminal_audit_unknown")]
    [InlineData("inbox_known")]
    [InlineData("audit_known")]
    [InlineData("success")]
    [InlineData("required_audit_unavailable")]
    [InlineData("timeout_audit_unknown")]
    [InlineData("cancelled")]
    [InlineData("known_failure")]
    public Task OriginalDurableWorkerPreservesPublicationFactsAndStopsCanonicalContinuation(string mode) => Run(mode);

    [Theory]
    [InlineData("required_audit_unknown")]
    [InlineData("cleanup_audit_refused")]
    [InlineData("reaper_audit_refused")]
    public Task OriginalDurableCleanupAuditUsesRetainedPurpose(string mode) => Run(mode);

    [Fact]
    public Task OriginalReaperWaitsForActualTerminalDecision() => Run("terminal_pending_reaper");

    private async Task Run(string mode)
    {
        string? folder = null;
        try
        {
            await GmWorkerNativePoolTests.RunScenario("pool-storage-" + mode,
                observeOutputDirectory: path => folder = path);
        }
        finally
        {
            if (folder != null)
            {
                var scenarioPath = Path.Combine(folder, "scenario.json");
                var guardianPath = Path.Combine(folder, "guardian.json");
                if (File.Exists(scenarioPath) && File.Exists(guardianPath))
                {
                    using var scenario = JsonDocument.Parse(File.ReadAllBytes(scenarioPath));
                    using var guardian = JsonDocument.Parse(File.ReadAllBytes(guardianPath));
                    var value = scenario.RootElement;
                    var stopped = guardian.RootElement.GetProperty("echild").GetBoolean()
                        && !guardian.RootElement.GetProperty("deadline").GetBoolean()
                        && guardian.RootElement.GetProperty("failures").GetInt32() == 0
                        && guardian.RootElement.GetProperty("emergencySignals").GetInt32() == 0;
                    output.WriteLine(JsonSerializer.Serialize(new { mode, FixtureFolder = folder, Scenario = value, Guardian = guardian.RootElement }));
                    var root = value.GetProperty("WorkerFixtureRoot").GetString()!;
                    Assert.Equal(Path.GetFullPath(folder), Path.GetDirectoryName(Path.GetFullPath(root)));
                    Assert.Equal("state-copy", Path.GetFileName(root));
                    Assert.True(stopped, "original guardian did not establish complete physical settlement");
                    if (Directory.Exists(root)) Directory.Delete(root, recursive: true);
                    output.WriteLine(JsonSerializer.Serialize(new { CleanupOwnedRoot = root, OwnedFixtureRemoved = !Directory.Exists(root), CleanupFailure = (string?)null }));
                    Assert.False(Directory.Exists(root));
                    Assert.False(value.TryGetProperty("FixtureCleanupFailure", out var failure), failure.ToString());
                }
            }
        }
    }
}
