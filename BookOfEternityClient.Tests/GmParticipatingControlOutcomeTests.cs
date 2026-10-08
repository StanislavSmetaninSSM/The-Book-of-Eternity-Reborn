using System.Text.Json;
using Xunit;
namespace BookOfEternityClient.Tests;
public sealed class GmParticipatingControlOutcomeTests
{
    [Theory]
    [InlineData("rollback")]
    [InlineData("unknown")]
    [InlineData("committed-debt")]
    [InlineData("control")]
    [InlineData("ps-uncertain")]
    [InlineData("ps-rollback")]
    [InlineData("ps-closing")]
    public async Task OriginalControlRetainsPublicationAndClosingDecision(string boundary)
    {
        string? folder=null;
        await GmOwnedTerminalLinuxTests.RunAsync("terminal-main-operation-outcome-"+boundary, value=>folder=value);
        using var evidence=JsonDocument.Parse(File.ReadAllBytes(Path.Combine(folder!,"scenario.json")));
        foreach(var failure in new[]{"CleanupFailure","DisposeFailure","ShutdownFailure"})
            Assert.False(evidence.RootElement.TryGetProperty(failure,out _),evidence.RootElement.ToString());
        Assert.True(evidence.RootElement.GetProperty("OriginalOutcomeOwnerRetired").GetBoolean());
        Assert.True(evidence.RootElement.GetProperty("DurableStopped").GetBoolean());
        Assert.True(evidence.RootElement.GetProperty("FinalOriginalOutcomeOwnerStopped").GetBoolean());
        Assert.False(evidence.RootElement.GetProperty("FinalOriginalOutcomeOwnerRetainsAuthority").GetBoolean());
    }
}
