using Xunit;
namespace BookOfEternityClient.Tests;
public sealed class GmHeldReadyDiagnosticTests
{
    [Fact]
    public async Task OriginalCanonicalHoldReleasedByActualHelperStdinClose()
    {
        // Historical category identity retained; acceptance now requires actual
        // cancellation plus the original serialized publication-loop provenance.
        string? folder=null;
        await GmOwnedTerminalLinuxTests.RunAsync("terminal-main-held-ready",value=>folder=value);
        Assert.NotNull(folder);
        var proof=GmHeldReadyStatusOracle.Verify(
            await File.ReadAllTextAsync(Path.Combine(folder!,"scenario.json")),
            await File.ReadAllTextAsync(Path.Combine(folder!,"guardian.log")));
        await File.WriteAllTextAsync(Path.Combine(folder!,"held-ready-status-provenance.json"),proof);
    }
}
