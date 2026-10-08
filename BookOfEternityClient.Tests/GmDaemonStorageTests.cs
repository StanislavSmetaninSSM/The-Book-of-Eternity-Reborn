using System.Text.Json;
using Xunit;

namespace BookOfEternityClient.Tests;

public sealed class GmDaemonStorageTests
{
    [Theory]
    [InlineData("request-rollback")]
    [InlineData("request-commit")]
    [InlineData("request-unknown")]
    [InlineData("ready-rollback")]
    [InlineData("ready-commit")]
    public async Task OriginalAdmittedQteConsumerUsesRecoveredRequestAndReady(string mode)
    {
        string? folder=null;
        try {await GmOwnedTerminalLinuxTests.RunAsync("terminal-main-daemon-storage-"+mode,path=>folder=path);}
        finally
        {
            if(folder!=null&&File.Exists(Path.Combine(folder,"scenario.json")))
            {
                using var report=JsonDocument.Parse(File.ReadAllBytes(Path.Combine(folder,"scenario.json")));
                var value=report.RootElement;
                Assert.False(value.TryGetProperty("DaemonCleanupFailures",out var failures),failures.ToString());
                Assert.True(value.GetProperty("OriginalDaemonOwnerRetired").GetBoolean());
                Assert.True(value.GetProperty("PowerShellExited").GetBoolean());
                Assert.False(value.TryGetProperty("ForcedPowerShellTermination",out _));
                Assert.False(value.TryGetProperty("CleanupFailure",out var cleanup),cleanup.ToString());
                Assert.False(value.TryGetProperty("DisposeFailure",out var dispose),dispose.ToString());
            }
        }
    }
}
