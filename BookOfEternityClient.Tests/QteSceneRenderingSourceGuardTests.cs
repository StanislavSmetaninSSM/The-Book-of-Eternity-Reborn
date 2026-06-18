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
        var updateSource = methodSource;
        var liveLoopName = methodName.Replace("Async", "LiveLoopAsync", StringComparison.Ordinal);

        Assert.Contains("RunMiniGameLiveAsync", methodSource, StringComparison.Ordinal);
        if (!updateSource.Contains(".Update(", StringComparison.Ordinal) &&
            methodSource.Contains(liveLoopName, StringComparison.Ordinal))
        {
            updateSource = ExtractMethodSource(source, $"internal static async Task<string> {liveLoopName}(");
        }

        Assert.Contains(".Update(", updateSource, StringComparison.Ordinal);
        Assert.DoesNotContain("RenderMiniGamePanel(", methodSource, StringComparison.Ordinal);
    }

    [Fact]
    public void TimingBarSpeed_MustGetFasterWhenDifficultyIncreases()
    {
        var source = ReadQteSceneServiceSource();
        var methodSource = ExtractMethodSource(source, "private async Task<QteGrade> RunTimingBarAsync(");
        var requirementSource = ExtractMethodSource(source, "internal static TimingBarLiveRequirement ComputeTimingBarLiveRequirement(");

        Assert.Contains("ComputeTimingBarLiveRequirement", methodSource, StringComparison.Ordinal);
        Assert.Contains("TimeoutMs", methodSource, StringComparison.Ordinal);
        Assert.Contains("- (difficulty *", requirementSource, StringComparison.Ordinal);
        Assert.Contains("+ (statTier *", requirementSource, StringComparison.Ordinal);
        Assert.DoesNotContain("+ (difficulty * 10)", requirementSource, StringComparison.Ordinal);
    }

    [Fact]
    public void PromptChainStartup_MustUseReadableFirstPromptWindow()
    {
        var source = ReadQteSceneServiceSource();
        var methodSource = ExtractMethodSource(source, "private async Task<QteGrade> RunPromptChainAsync(");
        var loopSource = ExtractMethodSource(source, "internal static async Task<string> RunPromptChainLiveLoopAsync(");

        Assert.Contains("ComputePromptChainLiveRequirement", methodSource, StringComparison.Ordinal);
        Assert.Contains("RunPromptChainLiveLoopAsync", methodSource, StringComparison.Ordinal);
        Assert.Contains("FirstPromptGraceMs", loopSource, StringComparison.Ordinal);
    }

    [Fact]
    public void PatternMemory_MustReplaceRevealWithInputInSameLiveDisplay()
    {
        var source = ReadQteSceneServiceSource();
        var methodSource = ExtractMethodSource(source, "private async Task<QteGrade> RunPatternMemoryAsync(");
        var loopSource = ExtractMethodSource(source, "internal static async Task<string> RunPatternMemoryLiveLoopAsync(");

        Assert.Equal(1, CountOccurrences(methodSource, "RunMiniGameLiveAsync"));
        Assert.Contains("RunPatternMemoryLiveLoopAsync", methodSource, StringComparison.Ordinal);
        Assert.Contains("renderer.Update(", loopSource, StringComparison.Ordinal);
        Assert.Contains("Память рун: фаза ввода", loopSource, StringComparison.Ordinal);
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
        Assert.Contains("влево на", methodSource, StringComparison.OrdinalIgnoreCase);
        Assert.Contains("вправо на", methodSource, StringComparison.OrdinalIgnoreCase);
        Assert.Contains("Шаг управления", methodSource, StringComparison.Ordinal);
    }

    [Fact]
    public void ChargeRelease_MustUseDistinctChargePresentation()
    {
        var source = ReadQteSceneServiceSource();
        var methodSource = ExtractMethodSource(source, "private async Task<QteGrade> RunChargeReleaseAsync(");
        var frameSource = ExtractMethodSource(source, "internal static string BuildChargeReleaseLiveFrame(");

        Assert.Contains("\"Накопление силы\"", methodSource, StringComparison.Ordinal);
        Assert.DoesNotContain("\"Charge Release\"", methodSource, StringComparison.Ordinal);
        Assert.Contains("BuildChargeReleaseLiveFrame", methodSource, StringComparison.Ordinal);
        Assert.Contains("Заряд:", frameSource, StringComparison.Ordinal);
        Assert.Contains("Целевая сила:", frameSource, StringComparison.Ordinal);
        Assert.Contains("Отпустите", frameSource, StringComparison.Ordinal);
        Assert.Contains("▲", frameSource, StringComparison.Ordinal);
    }

    private static int CountOccurrences(string source, string value)
    {
        var count = 0;
        var index = 0;
        while ((index = source.IndexOf(value, index, StringComparison.Ordinal)) >= 0)
        {
            count++;
            index += value.Length;
        }

        return count;
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
