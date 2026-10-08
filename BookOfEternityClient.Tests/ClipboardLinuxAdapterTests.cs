using Xunit;
namespace BookOfEternityClient.Tests;

public sealed class ClipboardLinuxAdapterTests
{
    [Theory]
    [InlineData("fixture", "fixture", "wl-paste,xclip,xsel", "wl-paste", "--no-newline --type text")]
    [InlineData(null, "fixture", "wl-paste,xclip,xsel", "xclip", "-selection clipboard -out -target UTF8_STRING")]
    [InlineData("fixture", "fixture", "xclip,xsel", "xclip", "-selection clipboard -out -target UTF8_STRING")]
    [InlineData(null, "fixture", "xsel", "xsel", "--clipboard --output")]
    public async Task PublicService_SelectsOneReadOnlyToolBeforeLaunch(string? wayland, string? display, string tools, string expected, string args)
    {
        using var fixture = new ClipboardLinuxFixture();
        var r = await fixture.Run(new() { Text = "Нейтральный 🌌\nабзац" }, wayland, display, tools.Split(','));
        Assert.Equal("Text", r.GetProperty("Outcome").GetString());
        var call = Assert.Single(fixture.ReadCalls());
        Assert.Equal(expected, call.GetProperty("Tool").GetString());
        Assert.Equal(args, string.Join(" ", call.GetProperty("Args").EnumerateArray().Select(x => x.GetString())));
    }

    [Theory]
    [InlineData(null, null, "wl-paste,xclip,xsel")]
    [InlineData("fixture", "fixture", "")]
    public async Task PublicService_UnavailableDoesNotStartReader(string? wayland, string? display, string tools)
    {
        using var fixture = new ClipboardLinuxFixture();
        var r = await fixture.Run(new(), wayland, display, tools.Split(',', StringSplitOptions.RemoveEmptyEntries));
        Assert.Equal("Unavailable", r.GetProperty("Outcome").GetString());
        Assert.Empty(fixture.ReadCalls());
    }

    [Theory]
    [InlineData("text", "Empty")]
    [InlineData("error", "Error")]
    [InlineData("invalid", "Error")]
    [InlineData("timeout", "Timeout")]
    [InlineData("stdout-flood", "TooLarge")]
    [InlineData("stderr-flood", "TooLarge")]
    public async Task PublicService_FailureIsTypedBoundedAndDoesNotFallback(string mode, string outcome)
    {
        using var fixture = new ClipboardLinuxFixture();
        var elapsed = System.Diagnostics.Stopwatch.StartNew();
        var r = await fixture.Run(new() { ReaderMode = mode }, "fixture", "fixture", ["wl-paste", "xclip", "xsel"]);
        Assert.Equal(outcome, r.GetProperty("Outcome").GetString());
        Assert.False(r.GetProperty("Result").GetProperty("Success").GetBoolean());
        Assert.Equal(System.Text.Json.JsonValueKind.Null, r.GetProperty("Result").GetProperty("Text").ValueKind);
        Assert.Single(fixture.ReadCalls());
        Assert.DoesNotContain("synthetic private", r.GetProperty("Result").GetProperty("Error").GetString());
        Assert.True(elapsed.Elapsed < TimeSpan.FromSeconds(6), "Owned reader must settle inside its bounded host lifetime.");
    }

    [Fact]
    public async Task PublicService_StartupFailureIsErrorWithoutFallback()
    {
        using var fixture = new ClipboardLinuxFixture();
        var r = await fixture.Run(new() { ReaderMode = "bad-start" }, "fixture", "fixture", ["wl-paste", "xclip"]);
        Assert.Equal("Error", r.GetProperty("Outcome").GetString());
        Assert.Empty(fixture.ReadCalls());
    }

    [Fact]
    public async Task PublicService_AcceptsExactStdoutLimit()
    {
        using var fixture = new ClipboardLinuxFixture();
        var r = await fixture.Run(new() { Text = new string('a', 1024 * 1024) });
        Assert.Equal("Text", r.GetProperty("Outcome").GetString());
        Assert.Equal(1024 * 1024, r.GetProperty("Result").GetProperty("Text").GetString()!.Length);
        Assert.Single(fixture.ReadCalls());
    }

    [Fact]
    public async Task PublicService_AcceptsExactStderrLimitWithoutExposingIt()
    {
        using var fixture = new ClipboardLinuxFixture();
        var r = await fixture.Run(new() { ReaderMode = "stderr-limit", Text = "Текст" });
        Assert.Equal("Text", r.GetProperty("Outcome").GetString());
        Assert.Equal("Текст", r.GetProperty("Result").GetProperty("Text").GetString());
        Assert.Single(fixture.ReadCalls());
    }

    [Fact]
    public async Task PublicService_LimitCountsUtf8BytesRatherThanCharacters()
    {
        using var fixture = new ClipboardLinuxFixture();
        var text = new string('Ж', 600_000);
        Assert.True(text.Length < 1024 * 1024);
        var r = await fixture.Run(new() { Text = text });
        Assert.Equal("TooLarge", r.GetProperty("Outcome").GetString());
        Assert.Single(fixture.ReadCalls());
    }

    [Fact]
    public async Task RealService_CaptureDebtRetainsOriginalAndRefusesUntilActualExit()
    {
        using var fixture = new ClipboardLinuxFixture();
        var r = await fixture.Run(new() { Mode = "debt", ReaderMode = "capture-debt", Text = "Новый явный жест" });
        Assert.Equal(new[] { "CleanupUncertain", "CleanupUncertain", "Text" },
            r.GetProperty("DebtOutcomes").EnumerateArray().Select(x => x.GetString()).ToArray());
        Assert.True(r.GetProperty("OriginalAliveWithDebt").GetBoolean());
        Assert.True(r.GetProperty("OriginalExitObserved").GetBoolean());
        Assert.Equal(3, r.GetProperty("ClipboardReads").GetInt32());
        Assert.Equal(2, fixture.ReadCalls().Length);
        Assert.Equal("Новый явный жест", r.GetProperty("Result").GetProperty("Text").GetString());
    }

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
