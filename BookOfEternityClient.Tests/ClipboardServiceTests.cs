using BookOfEternityClient.Services;
using Xunit;

namespace BookOfEternityClient.Tests;

public sealed class ClipboardServiceTests
{
    [Fact]
    public void NormalizeClipboardText_PreservesParagraphsButTrimsTrailingNewlines()
    {
        var raw = "Первый абзац\r\n\r\nВторой абзац\r\n";

        var normalized = SystemClipboardService.NormalizeClipboardText(raw);

        Assert.Equal("Первый абзац\n\nВторой абзац", normalized);
    }
}
