using System.Text.Json;
using System.Text.Json.Nodes;
using BookOfEternityClient.Core;

namespace BookOfEternityClient.Services;

/// <summary>
/// Holds a detached accepted-turn output image and whether normalization changed it.
/// </summary>
/// <param name="Json">
/// Decoded output JSON, preserving the input text when unchanged.
/// </param>
/// <param name="Changed">
/// <see langword="true"/> when the projected image differs from the input.
/// </param>
internal sealed record AcceptedOutputProjection(string Json, bool Changed);

/// <summary>
/// Projects accepted-turn output text without filesystem or clock access.
/// </summary>
internal static class AcceptedTurnOutputProjector
{
    /// <summary>
    /// Projects escaped line breaks in a narrative response string.
    /// </summary>
    /// <param name="json">
    /// Decoded narrative output JSON.
    /// </param>
    /// <returns>
    /// The projected narrative and whether it changed.
    /// </returns>
    internal static AcceptedOutputProjection ProjectNarrative(string json)
    {
        ArgumentNullException.ThrowIfNull(json);
        try
        {
            if (JsonNode.Parse(json) is not JsonObject root ||
                !root.TryGetPropertyValue("response", out var node) ||
                node is not JsonValue value || !value.TryGetValue<string>(out var response))
                return new(json, false);

            var normalized = PlayerFacingTextNormalizer.NormalizeEscapedLineBreakArtifacts(response);
            if (string.Equals(response, normalized, StringComparison.Ordinal))
                return new(json, false);

            root["response"] = normalized;
            return new(Serialize(root), true);
        }
        catch (JsonException)
        {
            return new(json, false);
        }
    }

    /// <summary>
    /// Determines whether useful interface property presence needs a fallback timestamp.
    /// </summary>
    /// <param name="json">
    /// Decoded interface output JSON.
    /// </param>
    /// <returns>
    /// <see langword="true"/> when the interface requires a fallback timestamp.
    /// </returns>
    internal static bool RequiresInterfaceTimestampFallback(string json)
    {
        ArgumentNullException.ThrowIfNull(json);
        try
        {
            return JsonNode.Parse(json) is JsonObject root && NeedsInterfaceTimestamp(root);
        }
        catch (JsonException)
        {
            return false;
        }
    }

    /// <summary>
    /// Projects dialogue choices and an optional fallback timestamp.
    /// </summary>
    /// <param name="json">
    /// Decoded interface output JSON.
    /// </param>
    /// <param name="fallbackTimestamp">
    /// Supplied fallback time when the input requires one; otherwise <see langword="null"/>.
    /// </param>
    /// <returns>
    /// The projected interface and whether it changed.
    /// </returns>
    internal static AcceptedOutputProjection ProjectInterface(string json, DateTimeOffset? fallbackTimestamp)
    {
        ArgumentNullException.ThrowIfNull(json);
        try
        {
            if (JsonNode.Parse(json) is not JsonObject root)
                return new(json, false);

            var changed = false;
            if (root.TryGetPropertyValue("dialogueOptions", out var choices) && choices is JsonArray options)
            {
                var normalizedOptions = new JsonArray();
                foreach (var option in options)
                {
                    if (option is JsonValue value && value.TryGetValue<string>(out var text))
                    {
                        text = PlayerFacingTextNormalizer.NormalizeEscapedLineBreakArtifacts(text) ?? text;
                        var normalizedOption = new JsonObject { ["text"] = text };
                        if (DialogueOptionControlTagNormalizer.TrySplitLeadingHiddenControlTag(
                                text, out var visibleText, out var inputValue))
                        {
                            normalizedOption["text"] = visibleText;
                            normalizedOption["inputValue"] = inputValue;
                        }
                        normalizedOptions.Add(normalizedOption);
                        changed = true;
                        continue;
                    }

                    if (option is JsonObject optionObject)
                    {
                        var normalizedOption = optionObject.DeepClone().AsObject();
                        if (NormalizeOptionLineBreaks(normalizedOption)) changed = true;
                        if (NormalizeOptionControlTag(normalizedOption)) changed = true;
                        normalizedOptions.Add(normalizedOption);
                        continue;
                    }

                    normalizedOptions.Add(option?.DeepClone());
                }

                if (changed) root["dialogueOptions"] = normalizedOptions;
            }

            if (NeedsInterfaceTimestamp(root))
            {
                if (fallbackTimestamp is null)
                    throw new ArgumentException("A fallback timestamp is required for this interface output.",
                        nameof(fallbackTimestamp));
                root["timestamp"] = fallbackTimestamp.Value.ToString("O");
                changed = true;
            }

            return changed ? new(Serialize(root), true) : new(json, false);
        }
        catch (JsonException)
        {
            return new(json, false);
        }
    }

    /// <summary>
    /// Checks useful property presence and the original blank timestamp condition.
    /// </summary>
    /// <param name="root">
    /// Parsed interface object, which remains owned by the caller.
    /// </param>
    /// <returns>
    /// <see langword="true"/> only when the ordinary wrapper would generate time.
    /// </returns>
    private static bool NeedsInterfaceTimestamp(JsonObject root)
    {
        var hasPayload = root.TryGetPropertyValue("dialogueOptions", out _) ||
                         root.TryGetPropertyValue("image_prompt", out _);
        if (!hasPayload) return false;
        if (!root.TryGetPropertyValue("timestamp", out var timestamp) || timestamp is null) return true;
        return timestamp is JsonValue value && value.TryGetValue<string>(out var text) &&
               string.IsNullOrWhiteSpace(text);
    }

    /// <summary>
    /// Uses the ordinary accepted-output serializer without changing property order.
    /// </summary>
    /// <param name="root">
    /// Parsed output object to serialize.
    /// </param>
    /// <returns>
    /// Indented JSON with relaxed escaping.
    /// </returns>
    private static string Serialize(JsonObject root) => root.ToJsonString(new JsonSerializerOptions
    {
        WriteIndented = true,
        Encoder = System.Text.Encodings.Web.JavaScriptEncoder.UnsafeRelaxedJsonEscaping
    });

    /// <summary>
    /// Replaces escaped line-break artifacts in the choice's text and input value.
    /// </summary>
    /// <param name="option">
    /// Detached dialogue option object to update in place.
    /// </param>
    /// <returns>
    /// <see langword="true"/> when either supported string changed.
    /// </returns>
    private static bool NormalizeOptionLineBreaks(JsonObject option)
    {
        var changed = false;
        foreach (var name in new[] { "text", "inputValue" })
        {
            if (!option.TryGetPropertyValue(name, out var node) || node is not JsonValue value ||
                !value.TryGetValue<string>(out var text)) continue;
            var normalized = PlayerFacingTextNormalizer.NormalizeEscapedLineBreakArtifacts(text);
            if (string.Equals(text, normalized, StringComparison.Ordinal)) continue;
            option[name] = normalized;
            changed = true;
        }
        return changed;
    }

    /// <summary>
    /// Moves a leading hidden control tag into an option's input value when needed.
    /// </summary>
    /// <param name="option">
    /// Detached dialogue option object to update in place.
    /// </param>
    /// <returns>
    /// <see langword="true"/> when a recognized tag changed the visible text.
    /// </returns>
    private static bool NormalizeOptionControlTag(JsonObject option)
    {
        if (!TryGetNonblankString(option, "text", out var text) ||
            !DialogueOptionControlTagNormalizer.TrySplitLeadingHiddenControlTag(
                text, out var visibleText, out var inputValue)) return false;
        option["text"] = visibleText;
        if (!TryGetNonblankString(option, "inputValue", out _)) option["inputValue"] = inputValue;
        return true;
    }

    /// <summary>
    /// Reads a nonblank string property without coercing another JSON type.
    /// </summary>
    /// <param name="option">
    /// Option object to inspect.
    /// </param>
    /// <param name="name">
    /// Exact property name.
    /// </param>
    /// <param name="value">
    /// Receives the string, or an empty string when absent or invalid.
    /// </param>
    /// <returns>
    /// <see langword="true"/> when the property is a nonblank string.
    /// </returns>
    private static bool TryGetNonblankString(JsonObject option, string name, out string value)
    {
        value = string.Empty;
        if (!option.TryGetPropertyValue(name, out var node) || node is not JsonValue jsonValue ||
            !jsonValue.TryGetValue<string>(out var text) || string.IsNullOrWhiteSpace(text)) return false;
        value = text;
        return true;
    }
}
