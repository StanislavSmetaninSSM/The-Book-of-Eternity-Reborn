using Xunit;
namespace BookOfEternityClient.Tests;

public sealed class ClipboardLinuxAdapterTests
{
    [Fact]
    public async Task PublicService_ReadsSyntheticUnicodeAndParagraphs()
    {
        using var fixture = new ClipboardLinuxFixture();
        var r = await fixture.Run(new() { Text = "Первый 🌌\r\n\r\nВторой\r\n" });
        Assert.True(r.GetProperty("Result").GetProperty("Success").GetBoolean(), r.ToString());
        Assert.Equal("Первый 🌌\n\nВторой", r.GetProperty("Result").GetProperty("Text").GetString());
        Assert.Single(fixture.ReadCalls());
    }
}
