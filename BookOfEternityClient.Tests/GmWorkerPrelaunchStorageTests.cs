using System.Text.Json;
using Xunit;
using Xunit.Abstractions;

namespace BookOfEternityClient.Tests;

public sealed class GmWorkerPrelaunchStorageTests(ITestOutputHelper output)
{
    [Theory]
    [InlineData("reservation_unknown")]
    [InlineData("dispatch_unknown")]
    [InlineData("known_diagnostic_unknown")]
    [InlineData("known_reservation")]
    [InlineData("public_linux_refusal")]
    public async Task OriginalBridgePrelaunchStorageRetainsUncertaintyAndReleasesOwnedCapacity(string mode)
    {
        string? folder = null;
        try { await GmOwnedTerminalLinuxTests.RunAsync("terminal-main-worker-storage-" + mode, path => folder = path); }
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
                    if (value.TryGetProperty("WorkerFixtureRoot", out var rootValue))
                    {
                        var root = rootValue.GetString()!;
                        Assert.Equal(Path.GetFullPath(folder), Path.GetDirectoryName(Path.GetFullPath(root)));
                        Assert.StartsWith("neutral-session-", Path.GetFileName(root), StringComparison.Ordinal);
                        Assert.True(stopped, "original guardian did not establish complete physical settlement");
                        if (Directory.Exists(root)) Directory.Delete(root, recursive: true);
                        output.WriteLine(JsonSerializer.Serialize(new { CleanupOwnedRoot = root, OwnedFixtureRemoved = !Directory.Exists(root), CleanupFailure = (string?)null }));
                        Assert.False(Directory.Exists(root));
                    }
                    Assert.True(value.GetProperty("OriginalWorkerMainRetired").GetBoolean());
                    Assert.False(value.TryGetProperty("CleanupFailure", out var cleanup), cleanup.ToString());
                    Assert.False(value.TryGetProperty("DisposeFailure", out var dispose), dispose.ToString());
                }
            }
        }
    }
}
