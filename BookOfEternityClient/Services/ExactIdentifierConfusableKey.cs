namespace BookOfEternityClient.Services;

/// <summary>
/// One spoof-resistant comparison key for exact identifiers crossing wound proposal
/// dialects. Exact spelling remains authoritative; this key is used only to reject
/// visually confusable siblings before any client-owned identity is allocated.
/// </summary>
internal static class ExactIdentifierConfusableKey
{
    internal static string Build(string value)
    {
        ArgumentNullException.ThrowIfNull(value);
        var dashFolded = value
            .Replace('\u2010', '-')
            .Replace('\u2011', '-')
            .Replace('\u2012', '-')
            .Replace('\u2013', '-')
            .Replace('\u2014', '-')
            .Replace('\u2212', '-')
            .Replace('\uFE58', '-')
            .Replace('\uFE63', '-')
            .Replace('\uFF0D', '-');
        return MortalLocationIdentityState.BuildConfusableKey(dashFolded);
    }
}
