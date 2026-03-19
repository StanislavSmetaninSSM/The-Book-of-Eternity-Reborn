using BookOfEternityClient.Core;
using Xunit;

namespace BookOfEternityClient.Tests;

public sealed class BufferedConsolePasteCaptureTests
{
    [Fact]
    public void Drain_ReturnsBufferedMultilineTailAsSingleBlock()
    {
        var keys = new Queue<ConsoleKeyInfo>(new[]
        {
            new ConsoleKeyInfo('В', ConsoleKey.V, false, false, false),
            new ConsoleKeyInfo('т', ConsoleKey.T, false, false, false),
            new ConsoleKeyInfo('о', ConsoleKey.O, false, false, false),
            new ConsoleKeyInfo('р', ConsoleKey.R, false, false, false),
            new ConsoleKeyInfo('а', ConsoleKey.A, false, false, false),
            new ConsoleKeyInfo('я', ConsoleKey.Y, false, false, false),
            new ConsoleKeyInfo('\r', ConsoleKey.Enter, false, false, false),
            new ConsoleKeyInfo('Т', ConsoleKey.T, false, false, false),
            new ConsoleKeyInfo('р', ConsoleKey.R, false, false, false),
            new ConsoleKeyInfo('е', ConsoleKey.E, false, false, false),
            new ConsoleKeyInfo('т', ConsoleKey.T, false, false, false),
            new ConsoleKeyInfo('ь', ConsoleKey.Oem7, false, false, false),
            new ConsoleKeyInfo('я', ConsoleKey.Y, false, false, false)
        });

        var captured = BufferedConsolePasteCapture.Drain(
            () => keys.Count > 0,
            () => keys.Dequeue(),
            quietPeriodMs: 0,
            maxTotalMs: 50);

        Assert.Equal("Вторая\nТретья", captured);
    }
}
