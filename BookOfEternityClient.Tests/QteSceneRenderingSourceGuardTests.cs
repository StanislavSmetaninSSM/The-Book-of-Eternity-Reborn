using Xunit;

namespace BookOfEternityClient.Tests;

public sealed class QteSceneRenderingSourceGuardTests
{
    private static string ReadQteSceneServiceSource()
    {
        return File.ReadAllText(Path.Combine(
            TestRepoPaths.RepoRoot,
            "BookOfEternityClient",
            "Services",
            "QteSceneService.cs"));
    }

    [Fact]
    public void RenderMiniGamePanel_MustNotClearTerminal()
    {
        var source = ReadQteSceneServiceSource();
        var methodSource = ExtractMethodSource(source, "private static void RenderMiniGamePanel(");

        Assert.DoesNotContain("AnsiConsole.Clear(", methodSource, StringComparison.Ordinal);
    }

    [Theory]
    [InlineData("RunTimingBarAsync")]
    [InlineData("RunPromptChainAsync")]
    [InlineData("RunBalanceMeterAsync")]
    [InlineData("RunMashInputAsync")]
    [InlineData("RunPatternMemoryAsync")]
    [InlineData("RunRhythmPulseAsync")]
    [InlineData("RunStealthNoiseAsync")]
    [InlineData("RunLockPinSetAsync")]
    public void TimedMiniGameLoops_MustUseLiveRendererInsteadOfClearingPanelHelper(string methodName)
    {
        var source = ReadQteSceneServiceSource();
        var methodSource = ExtractMethodSource(source, $"private async Task<QteGrade> {methodName}(");

        Assert.Contains("RunMiniGameLiveAsync", methodSource, StringComparison.Ordinal);
        Assert.Contains(".Update(", methodSource, StringComparison.Ordinal);
        Assert.DoesNotContain("RenderMiniGamePanel(", methodSource, StringComparison.Ordinal);
    }

    [Fact]
    public void TimingBarSpeed_MustGetFasterWhenDifficultyIncreases()
    {
        var source = ReadQteSceneServiceSource();
        var methodSource = ExtractMethodSource(source, "private async Task<QteGrade> RunTimingBarAsync(");

        Assert.Contains("- (difficulty *", methodSource, StringComparison.Ordinal);
        Assert.Contains("+ (statTier *", methodSource, StringComparison.Ordinal);
        Assert.DoesNotContain("+ (difficulty * 10)", methodSource, StringComparison.Ordinal);
    }

    [Fact]
    public void PatternMemoryInputProgress_MustNotRenderOriginalSequence()
    {
        var source = ReadQteSceneServiceSource();
        var methodSource = ExtractMethodSource(source, "private static string BuildPatternMemoryInputProgress(");

        Assert.Contains("Введено", methodSource, StringComparison.Ordinal);
        Assert.DoesNotContain("FormatPatternMemorySequence", methodSource, StringComparison.Ordinal);
        Assert.DoesNotContain("Показ:", methodSource, StringComparison.Ordinal);
    }

    [Fact]
    public void BalanceMeterProgress_MustExplainPositionAndControlStep()
    {
        var source = ReadQteSceneServiceSource();
        var methodSource = ExtractMethodSource(source, "private static string BuildBalanceMeter(");

        Assert.Contains("Позиция:", methodSource, StringComparison.Ordinal);
        Assert.Contains("безопасная зона", methodSource, StringComparison.OrdinalIgnoreCase);
        Assert.Contains("A/←: -10", methodSource, StringComparison.Ordinal);
        Assert.Contains("D/→: +10", methodSource, StringComparison.Ordinal);
    }

    private static string ExtractMethodSource(string source, string signature)
    {
        var start = source.IndexOf(signature, StringComparison.Ordinal);
        Assert.True(start >= 0, $"Could not find method signature: {signature}");

        var bodyStart = source.IndexOf('{', start);
        Assert.True(bodyStart >= 0, $"Could not find method body for: {signature}");

        var depth = 0;
        for (var index = bodyStart; index < source.Length; index++)
        {
            if (source[index] == '{')
            {
                depth++;
            }
            else if (source[index] == '}')
            {
                depth--;
                if (depth == 0)
                    return source[start..(index + 1)];
            }
        }

        throw new InvalidOperationException($"Could not extract method body for: {signature}");
    }
}
