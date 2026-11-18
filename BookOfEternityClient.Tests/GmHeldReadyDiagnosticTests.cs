using Xunit;
namespace BookOfEternityClient.Tests;
public sealed class GmHeldReadyDiagnosticTests
{
    [Fact]
    public async Task OriginalCanonicalHoldReleasedByActualHelperStdinClose()
        => await GmOwnedTerminalLinuxTests.RunAsync("terminal-main-held-ready");
}
