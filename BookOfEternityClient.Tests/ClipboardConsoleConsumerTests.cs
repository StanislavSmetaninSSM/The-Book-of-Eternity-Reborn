using Xunit;
namespace BookOfEternityClient.Tests;

public sealed class ClipboardConsoleConsumerTests
{
    [Fact]
    public async Task RealGetPlayerInput_LiteralShortcutIsOneReadAndRequiresManualAcceptance()
    {
        using var fixture = new ClipboardLinuxFixture();
        var r = await fixture.Run(new() { Mode = "turn", Provider = "literal", Text = "/paste", Lines = ["\\p", ""] });
        Assert.Equal(1, r.GetProperty("ClipboardReads").GetInt32());
        Assert.Equal(2, r.GetProperty("InputReads").GetInt32());
        Assert.Equal("/paste", r.GetProperty("Value").GetString());
    }

    [Fact]
    public async Task RealAsk_FailureShowsEscapedMessageAndRetainsDefaultUntilManualAcceptance()
    {
        using var fixture = new ClipboardLinuxFixture();
        var r = await fixture.Run(new() { Mode = "ask", Provider = "error", DefaultValue = "Ручной черновик", Lines = ["\\p", ""] });
        Assert.Contains("Не удалось [прочитать] буфер.", r.GetProperty("Screen").GetString());
        Assert.Equal(2, r.GetProperty("InputReads").GetInt32());
        Assert.Equal("Ручной черновик", r.GetProperty("Value").GetString());
        Assert.Equal(1, r.GetProperty("ClipboardReads").GetInt32());
    }
}
