using System.Reflection;
using BookOfEternityClient.Services;
using Xunit;

namespace BookOfEternityClient.Tests;

public sealed class QteLivePlayabilityTests
{
    [Fact]
    public void QteLive_TimingBarHardDifficultyUsesBoundedFastPacing()
    {
        var easy = InvokeStatic("ComputeTimingBarLiveRequirement", 1, 0);
        var hard = InvokeStatic("ComputeTimingBarLiveRequirement", 5, 0);

        Assert.True(IntProperty(hard, "TickMs") < IntProperty(easy, "TickMs"));
        Assert.True(IntProperty(hard, "SuccessWindowMs") < IntProperty(easy, "SuccessWindowMs"));
        Assert.True(IntProperty(hard, "TimeoutMs") < IntProperty(easy, "TimeoutMs"));
        Assert.InRange(IntProperty(hard, "SuccessWindowMs"), 80, 220);
        Assert.InRange(IntProperty(hard, "TimeoutMs"), 1200, 3000);
    }

    [Fact]
    public void QteLive_PromptChainHardDifficultyKeepsReadableFirstPrompt()
    {
        var easy = InvokeStatic("ComputePromptChainLiveRequirement", 1, 0);
        var hard = InvokeStatic("ComputePromptChainLiveRequirement", 5, 0);

        Assert.True(IntProperty(hard, "PerPromptTimeoutMs") < IntProperty(easy, "PerPromptTimeoutMs"));
        Assert.InRange(IntProperty(hard, "PerPromptTimeoutMs"), 700, 1200);
        Assert.InRange(IntProperty(hard, "FirstPromptTimeoutMs"), 1000, 1800);
        Assert.True(IntProperty(hard, "FirstPromptTimeoutMs") > IntProperty(hard, "PerPromptTimeoutMs"));
    }

    [Fact]
    public void QteLive_BalanceMeterFrameExplainsControlsStepAndSafeRange()
    {
        var frame = (string)InvokeStatic("BuildBalanceMeterLiveFrame", 43, 12, 4, 20, 10);

        Assert.Contains("Позиция: 43/100", frame, StringComparison.Ordinal);
        Assert.Contains("безопасная зона: 38-62", frame, StringComparison.OrdinalIgnoreCase);
        Assert.Contains("A/Ф или ←", frame, StringComparison.Ordinal);
        Assert.Contains("влево на 10", frame, StringComparison.OrdinalIgnoreCase);
        Assert.Contains("D/В или →", frame, StringComparison.Ordinal);
        Assert.Contains("вправо на 10", frame, StringComparison.OrdinalIgnoreCase);
        Assert.Contains("Шаг управления: 10", frame, StringComparison.Ordinal);
    }

    [Fact]
    public void QteLive_PatternMemoryInputFrameDoesNotContainRevealSequence()
    {
        var reveal = (string)InvokeStatic("BuildPatternMemoryRevealFrame", new[] { "q", "w", "space" }, 800);
        var input = (string)InvokeStatic(
            "BuildPatternMemoryInputLiveFrame",
            3,
            new[] { Key(ConsoleKey.Q) },
            1,
            2200);

        Assert.Contains("Q / Й", reveal, StringComparison.Ordinal);
        Assert.Contains("W / Ц", reveal, StringComparison.Ordinal);
        Assert.Contains("Space", reveal, StringComparison.Ordinal);

        Assert.Contains("Введено:", input, StringComparison.Ordinal);
        Assert.Contains("Шаг 1/3", input, StringComparison.Ordinal);
        Assert.DoesNotContain("Показ:", input, StringComparison.Ordinal);
        Assert.DoesNotContain("W / Ц", input, StringComparison.Ordinal);
        Assert.DoesNotContain("Space", input, StringComparison.Ordinal);
    }

    private static object InvokeStatic(string methodName, params object[] args)
    {
        var method = typeof(QteSceneService)
            .GetMethods(BindingFlags.Static | BindingFlags.Public | BindingFlags.NonPublic)
            .SingleOrDefault(candidate =>
                string.Equals(candidate.Name, methodName, StringComparison.Ordinal) &&
                candidate.GetParameters().Length == args.Length);

        Assert.NotNull(method);
        return method!.Invoke(null, args)!;
    }

    private static int IntProperty(object instance, string propertyName)
    {
        var property = instance.GetType().GetProperty(propertyName, BindingFlags.Instance | BindingFlags.Public);
        Assert.NotNull(property);
        return Assert.IsType<int>(property!.GetValue(instance));
    }

    private static ConsoleKeyInfo Key(ConsoleKey key)
    {
        var keyChar = key == ConsoleKey.Spacebar ? ' ' : char.ToLowerInvariant(key.ToString()[0]);
        return new ConsoleKeyInfo(keyChar, key, false, false, false);
    }
}
