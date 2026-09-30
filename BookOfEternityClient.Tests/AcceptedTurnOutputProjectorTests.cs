using System.Text.Json.Nodes;
using BookOfEternityClient.Services;
using Xunit;

namespace BookOfEternityClient.Tests;

/// <summary>
/// Verifies detached accepted-turn output transforms and timestamp fallback decisions.
/// </summary>
public sealed class AcceptedTurnOutputProjectorTests
{
    /// <summary>
    /// Preserves malformed, nonobject and already normalized narrative text exactly.
    /// </summary>
    /// <param name="json">
    /// Original decoded narrative text to retain.
    /// </param>
    [Theory]
    [InlineData("{broken")]
    [InlineData("[]")]
    [InlineData("{\"Response\":\"First`nSecond\"}")]
    [InlineData("{\"response\":42}")]
    [InlineData("{\"response\":\"Already clean\",\"timestamp\":\"2026-01-01T00:00:00Z\"}")]
    public void Narrative_PreservesMalformedNonobjectAndUnchangedInput(string json)
    {
        var projected = AcceptedTurnOutputProjector.ProjectNarrative(json);

        Assert.False(projected.Changed);
        Assert.Equal(json, projected.Json);
    }

    /// <summary>
    /// Normalizes only an exact string response and keeps the ordinary serialized field order and escaping.
    /// </summary>
    [Fact]
    public void Narrative_NormalizesOnlyExactStringResponseAndPreservesSerializedShape()
    {
        const string json = "{\"first\":\"Русский <текст>\",\"response\":\"Первая`r`nВторая\",\"last\":7}";

        var projected = AcceptedTurnOutputProjector.ProjectNarrative(json);

        Assert.True(projected.Changed);
        Assert.Equal("{\n  \"first\": \"Русский <текст>\",\n  \"response\": \"Первая\\nВторая\",\n  \"last\": 7\n}"
            .Replace("\n", Environment.NewLine, StringComparison.Ordinal),
            projected.Json);
    }

    /// <summary>
    /// Requires fallback time only for exact useful property presence with a missing or blank timestamp.
    /// </summary>
    /// <param name="json">
    /// Original decoded interface text to inspect.
    /// </param>
    /// <param name="expected">
    /// Expected fallback decision for the original shape.
    /// </param>
    [Theory]
    [InlineData("{bad", false)]
    [InlineData("[]", false)]
    [InlineData("{}", false)]
    [InlineData("{\"dialogueOptions\":null}", true)]
    [InlineData("{\"image_prompt\":42}", true)]
    [InlineData("{\"dialogueOptions\":[],\"timestamp\":null}", true)]
    [InlineData("{\"image_prompt\":null,\"timestamp\":\"  \"}", true)]
    [InlineData("{\"dialogueOptions\":[],\"timestamp\":\"2026-01-01T00:00:00Z\"}", false)]
    [InlineData("{\"dialogueOptions\":[],\"timestamp\":42}", false)]
    [InlineData("{\"dialogueOptions\":[],\"timestamp\":\"invalid\"}", false)]
    [InlineData("{\"dialogueOptions\":[],\"timestamp\":{}}", false)]
    public void InterfaceFallback_UsesExactPropertyPresenceAndBlankTimestamp(string json, bool expected)
    {
        Assert.Equal(expected, AcceptedTurnOutputProjector.RequiresInterfaceTimestampFallback(json));
    }

    /// <summary>
    /// Preserves malformed, nonobject and unchanged interface text exactly.
    /// </summary>
    /// <param name="json">
    /// Original decoded interface text to retain.
    /// </param>
    [Theory]
    [InlineData("{bad")]
    [InlineData("[]")]
    [InlineData("{\"timestamp\":null}")]
    [InlineData("{\"dialogueOptions\":null,\"timestamp\":42}")]
    public void Interface_PreservesMalformedNonobjectAndUnchangedInput(string json)
    {
        var projected = AcceptedTurnOutputProjector.ProjectInterface(json, null);

        Assert.False(projected.Changed);
        Assert.Equal(json, projected.Json);
    }

    /// <summary>
    /// Converts string choices, separates hidden tags and retains a nonblank explicit input value.
    /// </summary>
    [Fact]
    public void Interface_ConvertsMixedOptionsAndPreservesExistingInputValue()
    {
        const string json = "{\"image_prompt\":null,\"dialogueOptions\":[\"[INK_FEATHER_ACTION: LEARN_SKILL] Выбор`nдва\",{\"text\":\"[AFTERLIFE_SPIRITUAL_ACTION: action_1] Вход`r`nдва\",\"inputValue\":\"Сохранить\",\"category\":\"action\"},7,null],\"timestamp\":\"2026-01-01T00:00:00Z\"}";

        var projected = AcceptedTurnOutputProjector.ProjectInterface(json, null);

        Assert.True(projected.Changed);
        var root = JsonNode.Parse(projected.Json)!.AsObject();
        var options = root["dialogueOptions"]!.AsArray();
        Assert.Equal("Выбор\nдва", options[0]!["text"]!.GetValue<string>());
        Assert.Equal("[INK_FEATHER_ACTION: LEARN_SKILL] Выбор\nдва", options[0]!["inputValue"]!.GetValue<string>());
        Assert.Equal("Вход\nдва", options[1]!["text"]!.GetValue<string>());
        Assert.Equal("Сохранить", options[1]!["inputValue"]!.GetValue<string>());
        Assert.Equal("action", options[1]!["category"]!.GetValue<string>());
        Assert.Equal(7, options[2]!.GetValue<int>());
        Assert.Null(options[3]);
    }

    /// <summary>
    /// Adds supplied fallback time while retaining property order and relaxed Unicode escaping.
    /// </summary>
    [Fact]
    public void Interface_AddsSuppliedTimestampAfterExistingProperties()
    {
        const string json = "{\"image_prompt\":\"Пламя <над водой>\",\"timestamp\":null}";
        var time = new DateTimeOffset(2026, 9, 23, 1, 2, 3, TimeSpan.Zero);

        var projected = AcceptedTurnOutputProjector.ProjectInterface(json, time);

        Assert.True(projected.Changed);
        Assert.Equal("2026-09-23T01:02:03.0000000+00:00",
            JsonNode.Parse(projected.Json)!["timestamp"]!.GetValue<string>());
        Assert.Contains("Пламя <над водой>", projected.Json, StringComparison.Ordinal);
        Assert.True(projected.Json.IndexOf("\"image_prompt\"", StringComparison.Ordinal) <
                    projected.Json.IndexOf("\"timestamp\"", StringComparison.Ordinal));
    }

    /// <summary>
    /// Requires an explicit time only for a true fallback and leaves wrong-type timestamps for validation.
    /// </summary>
    [Fact]
    public void Interface_RequiresTimestampOnlyForActualFallback()
    {
        Assert.Throws<ArgumentException>(() => AcceptedTurnOutputProjector.ProjectInterface(
            "{\"dialogueOptions\":null}", null));
        var unchanged = AcceptedTurnOutputProjector.ProjectInterface(
            "{\"dialogueOptions\":null,\"timestamp\":42}", null);
        Assert.False(unchanged.Changed);
    }

    /// <summary>
    /// Replaces blank input values from recognized tags and normalizes escaped and backtick line breaks.
    /// </summary>
    [Fact]
    public void Interface_ReplacesBlankInputValueWithHiddenTagAndNormalizesBothBreakForms()
    {
        const string json = """{"dialogueOptions":[{"text":"[INK_FEATHER_ACTION: LEARN_SKILL] Строка\\nдва","inputValue":" "},{"text":"Готово","inputValue":"Первая\\r\\nвторая`nтретья"}],"timestamp":"2026-01-01T00:00:00Z"}""";

        var projected = AcceptedTurnOutputProjector.ProjectInterface(json, null);

        Assert.True(projected.Changed);
        var choices = JsonNode.Parse(projected.Json)!["dialogueOptions"]!.AsArray();
        Assert.Equal("Строка\nдва", choices[0]!["text"]!.GetValue<string>());
        Assert.Equal("[INK_FEATHER_ACTION: LEARN_SKILL] Строка\nдва",
            choices[0]!["inputValue"]!.GetValue<string>());
        Assert.Equal("Первая\nвторая\nтретья", choices[1]!["inputValue"]!.GetValue<string>());
    }

    /// <summary>
    /// Converts literal escaped backslashes in narrative text to visible line breaks.
    /// </summary>
    [Fact]
    public void Narrative_NormalizesLiteralEscapedBackslashBreaks()
    {
        const string json = """{"response":"Первая\\nвторая\\r\\nтретья"}""";

        var projected = AcceptedTurnOutputProjector.ProjectNarrative(json);

        Assert.True(projected.Changed);
        Assert.Equal("Первая\nвторая\nтретья", JsonNode.Parse(projected.Json)!["response"]!.GetValue<string>());
    }
}
