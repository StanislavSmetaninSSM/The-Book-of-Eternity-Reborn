namespace BookOfEternityClient.Core;

internal static class PlayerFacingTextNormalizer
{
    public static string? NormalizeEscapedLineBreakArtifacts(string? value)
    {
        if (string.IsNullOrEmpty(value))
            return value;

        var normalized = NormalizePowerShellLineBreakArtifacts(value);

        return normalized
            .Replace("\\r\\n", "\n", StringComparison.Ordinal)
            .Replace("\\n", "\n", StringComparison.Ordinal)
            .Replace("\\r", "\n", StringComparison.Ordinal)
            .Replace("\r\n", "\n", StringComparison.Ordinal)
            .Replace('\r', '\n');
    }

    private static string NormalizePowerShellLineBreakArtifacts(string value)
    {
        var explicitPairsNormalized = value
            .Replace("`r`n", "\n", StringComparison.Ordinal)
            .Replace("`n`n", "\n\n", StringComparison.Ordinal);
        var protectedBackticks = FindPairedBackticks(explicitPairsNormalized);
        var builder = new System.Text.StringBuilder(explicitPairsNormalized.Length);
        for (var index = 0; index < explicitPairsNormalized.Length; index++)
        {
            if (explicitPairsNormalized[index] != '`')
            {
                builder.Append(explicitPairsNormalized[index]);
                continue;
            }

            if (!protectedBackticks[index] &&
                index + 1 < explicitPairsNormalized.Length &&
                explicitPairsNormalized[index + 1] is 'n' or 'r')
            {
                builder.Append('\n');
                index++;
                continue;
            }

            builder.Append(explicitPairsNormalized[index]);
        }

        return builder.ToString();
    }

    private static bool[] FindPairedBackticks(string value)
    {
        var paired = new bool[value.Length];
        var lineStart = 0;
        while (lineStart < value.Length)
        {
            var lineEnd = lineStart;
            while (lineEnd < value.Length && value[lineEnd] is not '\r' and not '\n')
                lineEnd++;

            var positions = new List<int>();
            for (var index = lineStart; index < lineEnd; index++)
            {
                if (value[index] == '`')
                    positions.Add(index);
            }

            for (var positionIndex = positions.Count - 1; positionIndex >= 0; positionIndex--)
            {
                var closingPosition = positions[positionIndex];
                if (paired[closingPosition] || IsPowerShellLineBreakCandidate(value, closingPosition))
                    continue;

                for (var openingIndex = positionIndex - 1; openingIndex >= 0; openingIndex--)
                {
                    var openingPosition = positions[openingIndex];
                    if (paired[openingPosition])
                        continue;

                    paired[openingPosition] = true;
                    paired[closingPosition] = true;
                    break;
                }
            }

            lineStart = lineEnd + 1;
        }

        return paired;
    }

    private static bool IsPowerShellLineBreakCandidate(string value, int backtickIndex) =>
        backtickIndex + 1 < value.Length && value[backtickIndex + 1] is 'n' or 'r';
}
