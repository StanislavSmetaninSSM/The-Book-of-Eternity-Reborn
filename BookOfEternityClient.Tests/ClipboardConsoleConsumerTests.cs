using Xunit;
namespace BookOfEternityClient.Tests;

public sealed class ClipboardConsoleConsumerTests
{
    [Theory]
    [InlineData("turn", "\\p", "Первый 🌌\n\nВторой", "Первый 🌌\n\nВторой")]
    [InlineData("ask", "/вставить", "Первый 🌌\n\nВторой", "Первый 🌌 Второй")]
    [InlineData("turn", "/paste", "\\p", "\\p")]
    [InlineData("ask", "\\p", "/вставить", "/вставить")]
    public async Task ActualConsumers_ReadOneRealSyntheticReaderThenAcceptManually(string mode, string gesture, string text, string expected)
    {
        using var fixture = new ClipboardLinuxFixture();
        var r = await fixture.Run(new() { Mode = mode, Text = text, Lines = [gesture, ""] });
        Assert.Equal(expected, r.GetProperty("Value").GetString());
        Assert.Equal(1, r.GetProperty("ClipboardReads").GetInt32());
        Assert.Equal(2, r.GetProperty("InputReads").GetInt32());
        Assert.Single(fixture.ReadCalls());
    }

    [Theory]
    [InlineData("turn", "Новое ручное действие")]
    [InlineData("ask", "Новая ручная строка")]
    public async Task ActualConsumers_ManualReplacementDoesNotReplayClipboard(string mode, string replacement)
    {
        using var fixture = new ClipboardLinuxFixture();
        var r = await fixture.Run(new() { Mode = mode, Text = "[red]старый черновик[/]", Lines = ["\\p", replacement] });
        Assert.Equal(replacement, r.GetProperty("Value").GetString());
        Assert.Single(fixture.ReadCalls());
        Assert.Contains("[red]старый черновик[/]", r.GetProperty("Screen").GetString());
    }

    [Theory]
    [InlineData("ask")]
    [InlineData("turn")]
    [InlineData("multiline")]
    public async Task ActualConsumers_EofCannotAcceptClipboardDraft(string mode)
    {
        using var fixture = new ClipboardLinuxFixture();
        var r = await fixture.Run(new() { Mode = mode, Text = "Не отправлять 🌌", Lines = ["\\p", null] });
        Assert.Equal(System.Text.Json.JsonValueKind.Null, r.GetProperty("Value").ValueKind);
        Assert.Equal("TextComposerInputClosedException", r.GetProperty("Exception").GetString());
        Assert.Equal("Не отправлять 🌌", r.GetProperty("Draft").GetString());
        Assert.Single(fixture.ReadCalls());
    }

    [Fact]
    public async Task RealMultiline_FailedReadPreservesManualParagraphs()
    {
        using var fixture = new ClipboardLinuxFixture();
        var r = await fixture.Run(new() { Mode = "multiline", ReaderMode = "error", Lines = ["Ручной 🌌", "\\p", "Абзац", "", ""] });
        Assert.Equal("Ручной 🌌\nАбзац", r.GetProperty("Value").GetString());
        Assert.Contains("Не удалось прочитать", r.GetProperty("Screen").GetString());
        Assert.Single(fixture.ReadCalls());
    }

    [Fact]
    public async Task RealMultiline_ClipboardDraftRequiresTwoExplicitBlankLines()
    {
        using var fixture = new ClipboardLinuxFixture();
        var r = await fixture.Run(new() { Mode = "multiline", Text = "Один\n\nДва 🌌", Lines = ["/paste", "", ""] });
        Assert.Equal("Один\n\nДва 🌌", r.GetProperty("Value").GetString());
        Assert.Equal(3, r.GetProperty("InputReads").GetInt32());
        Assert.Single(fixture.ReadCalls());
    }

    [Fact]
    public async Task RealAsk_TimeoutPreservesDefaultAndDoesNotShowReaderStderr()
    {
        using var fixture = new ClipboardLinuxFixture();
        var r = await fixture.Run(new() { Mode = "ask", ReaderMode = "timeout", DefaultValue = "Ручной", Lines = ["/paste", ""] });
        Assert.Equal("Ручной", r.GetProperty("Value").GetString());
        Assert.Contains("Время чтения", r.GetProperty("Screen").GetString());
        Assert.Single(fixture.ReadCalls());
    }

    [Fact]
    public async Task RealAsk_UnavailableKeepsDefaultWithoutReader()
    {
        using var fixture = new ClipboardLinuxFixture();
        var r = await fixture.Run(new() { Mode = "ask", DefaultValue = "Ручной", Lines = ["\\p", ""] }, null, null);
        Assert.Equal("Ручной", r.GetProperty("Value").GetString());
        Assert.Contains("недоступно", r.GetProperty("Screen").GetString());
        Assert.Empty(fixture.ReadCalls());
    }

    [Fact]
    public async Task RealAsk_OnlyNewGestureCanRetryAfterError()
    {
        using var fixture = new ClipboardLinuxFixture();
        var r = await fixture.Run(new() { Mode = "ask", ReaderMode = "error", DefaultValue = "Ручной", Lines = ["\\p", "/paste", ""] });
        Assert.Equal("Ручной", r.GetProperty("Value").GetString());
        Assert.Equal(2, fixture.ReadCalls().Length);
        Assert.DoesNotContain("synthetic private", r.GetProperty("Screen").GetString());
    }

    [Fact]
    public async Task RealAsk_PreviewIsBoundedEscapedAndDoesNotAlterDraft()
    {
        using var fixture = new ClipboardLinuxFixture();
        var text = "[red]🌌[/]\u001b" + new string('a', 4000);
        var r = await fixture.Run(new() { Mode = "ask", Text = text, Lines = ["\\p", ""] });
        Assert.Equal(text, r.GetProperty("Value").GetString());
        Assert.True(r.GetProperty("Screen").GetString()!.Length < 1200);
        Assert.DoesNotContain("\u001b", r.GetProperty("Screen").GetString(), StringComparison.Ordinal);
    }

    [Fact]
    public async Task RealGetPlayerInput_MultilineRouteUsesSameClipboardAndManualTerminator()
    {
        using var fixture = new ClipboardLinuxFixture();
        var r = await fixture.Run(new() { Mode = "turn", Text = "Один\nДва 🌌", Lines = ["\\m", "\\p", "", ""] });
        Assert.Equal("Один\nДва 🌌", r.GetProperty("Value").GetString());
        Assert.Equal(4, r.GetProperty("InputReads").GetInt32());
        Assert.Single(fixture.ReadCalls());
    }

    [Theory]
    [InlineData("ask")]
    [InlineData("multiline")]
    public async Task ActualConsumers_ErrorThenEofPreservesDefaultWithoutAccepting(string mode)
    {
        using var fixture = new ClipboardLinuxFixture();
        var r = await fixture.Run(new() { Mode = mode, ReaderMode = "error", DefaultValue = "Сохранить ручной", Lines = ["\\p", null] });
        Assert.Equal(System.Text.Json.JsonValueKind.Null, r.GetProperty("Value").ValueKind);
        Assert.Equal("TextComposerInputClosedException", r.GetProperty("Exception").GetString());
        Assert.Equal("Сохранить ручной", r.GetProperty("Draft").GetString());
        Assert.Single(fixture.ReadCalls());
    }

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
