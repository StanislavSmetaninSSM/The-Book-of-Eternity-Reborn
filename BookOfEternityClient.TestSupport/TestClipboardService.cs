using BookOfEternityClient.Services;

namespace BookOfEternityClient.Tests;

internal sealed class TestClipboardService : IClipboardService
{
    public string? Text { get; set; }
    public string? Error { get; set; }

    public ClipboardReadResult TryReadText()
    {
        if (!string.IsNullOrWhiteSpace(Error))
            return ClipboardReadResult.Fail(Error);

        return ClipboardReadResult.Ok(Text ?? string.Empty);
    }
}
