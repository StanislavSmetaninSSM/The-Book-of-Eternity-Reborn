using System.Text.Json;
using Xunit;

namespace BookOfEternityClient.Tests;

public sealed class PreparedRemoteOutcomeTests
{
    [Theory]
    [InlineData("unknown")]
    [InlineData("rollback")]
    [InlineData("committed")]
    public async Task OriginalRemoteCloseRetainsPreparedPublicationDecision(string mode)
    {
        string? folder=null;
        try {await GmOwnedTerminalLinuxTests.RunAsync("terminal-main-prepared-outcome-"+mode,path=>folder=path);}
        finally {
            if(folder!=null&&File.Exists(Path.Combine(folder,"scenario.json"))){
                using var doc=JsonDocument.Parse(File.ReadAllBytes(Path.Combine(folder,"scenario.json")));var value=doc.RootElement;
                Assert.False(value.TryGetProperty("PreparedCleanupFailures",out var failures),failures.ToString());
                Assert.True(value.GetProperty("OriginalPreparedOwnerRetired").GetBoolean());
                Assert.False(value.TryGetProperty("CleanupFailure",out var cleanup),cleanup.ToString());
                Assert.False(value.TryGetProperty("DisposeFailure",out var dispose),dispose.ToString());
            }
        }
    }
}
