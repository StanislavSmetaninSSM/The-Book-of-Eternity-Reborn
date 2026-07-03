namespace BookOfEternityClient.Core;

internal static class PlayerFacingTextNormalizer
{
    public static string? NormalizeEscapedLineBreakArtifacts(string? value)
    {
        if (string.IsNullOrEmpty(value))
            return value;

        return value
            .Replace("\\r\\n", "\n", StringComparison.Ordinal)
            .Replace("\\n", "\n", StringComparison.Ordinal)
            .Replace("\\r", "\n", StringComparison.Ordinal)
            .Replace("\r\n", "\n", StringComparison.Ordinal)
            .Replace('\r', '\n');
    }
}
