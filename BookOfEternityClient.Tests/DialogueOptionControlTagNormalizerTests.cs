using System.Reflection;
using BookOfEternityClient.Core;
using Xunit;

namespace BookOfEternityClient.Tests;

public sealed class DialogueOptionControlTagNormalizerTests
{
    [Fact]
    public void NormalizeVisibleText_HidesLeadingAfterlifeSpiritualActionTag()
    {
        const string raw = "[AFTERLIFE_SPIRITUAL_ACTION: afterlife_conflict_myriel_ash_ward_trial_turn_7] Держу первый круг Оберега.";

        var visible = InvokeNormalizeVisibleText(raw);

        Assert.Equal("Держу первый круг Оберега.", visible);
        Assert.DoesNotContain("AFTERLIFE_SPIRITUAL_ACTION", visible, StringComparison.Ordinal);
    }

    [Fact]
    public void ResolveInputValue_PreservesRawTaggedActionWhenVisibleTextIsSanitized()
    {
        const string raw = "[AFTERLIFE_SPIRITUAL_ACTION: afterlife_conflict_myriel_ash_ward_trial_turn_7] Держу первый круг Оберега.";

        var inputValue = InvokeResolveInputValue(raw, existingInputValue: null);

        Assert.Equal(raw, inputValue);
    }

    [Fact]
    public void ResolveInputValue_PreservesExplicitInputValueWhenPresent()
    {
        const string raw = "[AFTERLIFE_SPIRITUAL_ACTION: conflict] Игрок защищается.";
        const string explicitInputValue = "[AFTERLIFE_SPIRITUAL_ACTION: conflict] Полное действие для ГМ.";

        var inputValue = InvokeResolveInputValue(raw, explicitInputValue);

        Assert.Equal(explicitInputValue, inputValue);
    }

    [Fact]
    public void ResolveInputValue_ReturnsNullForVisibleTextWithoutExplicitInputValue()
    {
        var inputValue = InvokeResolveInputValue("Осмотреть печать письма.", existingInputValue: "");

        Assert.Null(inputValue);
    }

    [Fact]
    public void NormalizeVisibleText_HidesSimilarActionControlTag()
    {
        const string raw = "[INK_FEATHER_ACTION: LEARN_SKILL] Изучить духовный приём.";

        var visible = InvokeNormalizeVisibleText(raw);

        Assert.Equal("Изучить духовный приём.", visible);
        Assert.DoesNotContain("INK_FEATHER_ACTION", visible, StringComparison.Ordinal);
    }

    private static string? InvokeNormalizeVisibleText(string? text)
    {
        var normalizer = GetNormalizerType();
        var method = normalizer.GetMethod("NormalizeVisibleText", BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Static);
        Assert.NotNull(method);
        return (string?)method.Invoke(null, [text]);
    }

    private static string? InvokeResolveInputValue(string? text, string? existingInputValue)
    {
        var normalizer = GetNormalizerType();
        var method = normalizer.GetMethod("ResolveInputValue", BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Static);
        Assert.NotNull(method);
        return (string?)method.Invoke(null, [text, existingInputValue]);
    }

    private static Type GetNormalizerType()
    {
        var type = typeof(GameEngine).Assembly.GetType("BookOfEternityClient.Core.DialogueOptionControlTagNormalizer");
        Assert.NotNull(type);
        return type;
    }
}
