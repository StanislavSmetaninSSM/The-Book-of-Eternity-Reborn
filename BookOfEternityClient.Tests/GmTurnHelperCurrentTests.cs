using System.Text.Json;
using Xunit;

namespace BookOfEternityClient.Tests;

public sealed class GmTurnHelperCurrentTests
{
    [Fact]
    public Task DedicatedHelperNestedDefaultRoleRemainsSeparate() => DedicatedHelperPreservesOwnedTransportPolicyAndOutcomes("nested-default");

    [Theory]
    [InlineData("large-read")]
    [InlineData("large-write")]
    [InlineData("chunk-duplicate")]
    [InlineData("chunk-offset")]
    [InlineData("chunk-trailing")]
    [InlineData("chunk-hash")]
    [InlineData("chunk-incomplete")]
    [InlineData("partial-close")]
    [InlineData("admission-cancel")]
    [InlineData("known-rollback")]
    [InlineData("publication-unknown")]
    [InlineData("committed-debt")]
    [InlineData("case-baselines")]
    [InlineData("running-owner")]
    [InlineData("ancestor-file")]
    [InlineData("directory-leaf")]
    [InlineData("parse-fallback")]
    public async Task DedicatedHelperPreservesOwnedTransportPolicyAndOutcomes(string mode)
    {
        string? folder=null;
        try{await GmOwnedTerminalLinuxTests.RunAsync("terminal-main-helper-current-"+mode,path=>folder=path);}
        finally
        {
            if(folder!=null&&File.Exists(Path.Combine(folder,"scenario.json")))
            {
                using var result=JsonDocument.Parse(File.ReadAllBytes(Path.Combine(folder,"scenario.json")));var value=result.RootElement;
                Assert.False(value.TryGetProperty("CleanupFailure",out var cleanup),cleanup.ToString());
                Assert.False(value.TryGetProperty("DisposeFailure",out var disposal),disposal.ToString());
                Assert.False(value.TryGetProperty("ForcedPowerShellTermination",out _));Assert.False(value.TryGetProperty("ForcedHelperTermination",out _));
                if(mode is "running-owner" or "admission-cancel")Assert.True(value.GetProperty("OriginalHelperOwnerRetired").GetBoolean());
                else Assert.True(value.GetProperty("FixtureCanonicalOwnershipReleased").GetBoolean());
                if(mode=="admission-cancel")Assert.True(value.GetProperty("OriginalHelperExited").GetBoolean());
                else Assert.True(value.GetProperty("PowerShellExited").GetBoolean());
            }
        }
    }
}
