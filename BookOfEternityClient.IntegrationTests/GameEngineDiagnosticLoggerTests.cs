using BookOfEternityClient.Core;
using Microsoft.Extensions.Logging;
using Xunit;
using Xunit.Abstractions;

namespace BookOfEternityClient.Tests;

/// <summary>
/// Checks complete engine diagnostic retention without constructing an engine or a game world.
/// </summary>
public sealed class GameEngineDiagnosticLoggerTests
{
    private readonly ITestOutputHelper _output;

    /// <summary>
    /// Retains the test output sink for the complete observed logger image.
    /// </summary>
    /// <param name="output">
    /// The test-owned sink that records every observed message without an application truncation rule.
    /// </param>
    public GameEngineDiagnosticLoggerTests(ITestOutputHelper output) => _output = output;

    /// <summary>
    /// Preserves every enabled message and full exception text beyond the old count and length limits.
    /// </summary>
    [Fact]
    public void RetainsEveryLongMessageAndFullExceptionInArrivalOrder()
    {
        var logger = new GameEngineTurnLifecycleTests.SpiritualLifecycleTestLogger();
        var expected = Enumerable.Range(0, 32)
            .Select(index => $"message-{index:D2}:" + new string((char)('a' + index % 26), 2301) + $":end-{index:D2}")
            .ToList();
        foreach (var message in expected)
            logger.Log(LogLevel.Information, default, message, null, static (state, _) => state);

        var failure = new InvalidOperationException("complete-exception:" + new string('e', 2301) + ":exception-end");
        logger.Log(LogLevel.Error, default, "last-message", failure, static (state, _) => state);
        expected.Add("last-message\n" + failure);

        var actual = logger.Describe();
        _output.WriteLine(actual);
        Assert.Equal("Engine diagnostics:\n" + string.Join("\n", expected), actual);
    }
}
