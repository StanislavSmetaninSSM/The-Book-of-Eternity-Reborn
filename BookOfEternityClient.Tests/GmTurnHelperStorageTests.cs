using System.Text.Json;
using Xunit;

namespace BookOfEternityClient.Tests;

public sealed class GmTurnHelperStorageTests
{
    [Theory]
    [InlineData("init-held")]
    [InlineData("read-held")]
    [InlineData("realm-held")]
    [InlineData("terminal-held")]
    [InlineData("stale-load")]
    [InlineData("path-dot")]
    [InlineData("path-sibling")]
    [InlineData("realm-link")]
    [InlineData("generation-missing")]
    [InlineData("generation-malformed")]
    public async Task OriginalHelperAdmitsCurrentGenerationAndCommittedPolicySnapshot(string scenario)
    {
        string? folder = null;
        try {
            await GmOwnedTerminalLinuxTests.RunAsync("terminal-main-helper-storage-" + scenario, path => folder = path);
        } finally {
            // Logical release matters even for a causal RED. Guardian ECHILD is
            // separately checked by the original outer process harness.
            if (folder != null && File.Exists(Path.Combine(folder, "scenario.json"))) {
                using var result = JsonDocument.Parse(File.ReadAllBytes(Path.Combine(folder, "scenario.json")));
                Assert.False(result.RootElement.TryGetProperty("CleanupFailure", out var failure), failure.ToString());
                Assert.True(result.RootElement.GetProperty("FixtureCanonicalOwnershipReleased").GetBoolean());
                Assert.True(result.RootElement.GetProperty("OriginalPowerShellExited").GetBoolean());
                Assert.False(result.RootElement.TryGetProperty("ForcedPowerShellTermination", out _), "A forced timeout is a fixture failure, not causal RED or PASS.");
            }
        }
    }
}
