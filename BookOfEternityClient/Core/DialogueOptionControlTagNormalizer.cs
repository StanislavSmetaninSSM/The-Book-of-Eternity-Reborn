namespace BookOfEternityClient.Core;

internal static class DialogueOptionControlTagNormalizer
{
    public static string? NormalizeVisibleText(string? text)
    {
        return TrySplitLeadingHiddenControlTag(text, out var visibleText, out _)
            ? visibleText
            : text;
    }

    public static string? ResolveInputValue(string? text, string? existingInputValue)
    {
        if (!string.IsNullOrWhiteSpace(existingInputValue))
            return existingInputValue;

        return TrySplitLeadingHiddenControlTag(text, out _, out var inputValue)
            ? inputValue
            : null;
    }

    public static bool TrySplitLeadingHiddenControlTag(string? text, out string visibleText, out string inputValue)
    {
        visibleText = text ?? string.Empty;
        inputValue = text ?? string.Empty;

        if (string.IsNullOrWhiteSpace(text))
            return false;

        var trimmed = text.TrimStart();
        if (!trimmed.StartsWith('['))
            return false;

        var closeIndex = trimmed.IndexOf(']');
        if (closeIndex <= 1)
            return false;

        var tag = trimmed[1..closeIndex].Trim();
        if (!IsPlayerHiddenControlTag(tag))
            return false;

        var remaining = trimmed[(closeIndex + 1)..].TrimStart();
        if (string.IsNullOrWhiteSpace(remaining))
            return false;

        visibleText = remaining;
        inputValue = trimmed;
        return true;
    }

    private static bool IsPlayerHiddenControlTag(string tag)
    {
        var separatorIndex = tag.IndexOf(':');
        if (separatorIndex < 0)
            return false;

        var tagName = tag[..separatorIndex].Trim();
        if (tagName.Length == 0)
            return false;

        foreach (var c in tagName)
        {
            if (c is not ('_' or >= 'A' and <= 'Z' or >= '0' and <= '9'))
                return false;
        }

        return tagName.EndsWith("_ACTION", StringComparison.Ordinal) ||
               tagName.EndsWith("_CONTROL", StringComparison.Ordinal);
    }
}
