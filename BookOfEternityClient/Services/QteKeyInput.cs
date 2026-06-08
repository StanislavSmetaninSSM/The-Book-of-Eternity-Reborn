namespace BookOfEternityClient.Services;

internal static class QteKeyInput
{
    internal const string LayoutSupportNote =
        "Клавиши быстрых сцен читаются как физические: Q / Й, W / Ц, E / У, A / Ф, S / Ы, D / В и Space работают без смены раскладки.";

    private static readonly Dictionary<ConsoleKey, string> KeyTokens = new()
    {
        [ConsoleKey.Q] = "q",
        [ConsoleKey.W] = "w",
        [ConsoleKey.E] = "e",
        [ConsoleKey.A] = "a",
        [ConsoleKey.S] = "s",
        [ConsoleKey.D] = "d",
        [ConsoleKey.Spacebar] = "space"
    };

    private static readonly Dictionary<char, string> CharacterTokens = new()
    {
        ['q'] = "q",
        ['й'] = "q",
        ['w'] = "w",
        ['ц'] = "w",
        ['e'] = "e",
        ['у'] = "e",
        ['a'] = "a",
        ['ф'] = "a",
        ['s'] = "s",
        ['ы'] = "s",
        ['d'] = "d",
        ['в'] = "d",
        [' '] = "space"
    };

    private static readonly Dictionary<string, string> PromptLabels = new(StringComparer.OrdinalIgnoreCase)
    {
        ["q"] = "Q / Й",
        ["w"] = "W / Ц",
        ["e"] = "E / У",
        ["a"] = "A / Ф",
        ["s"] = "S / Ы",
        ["d"] = "D / В",
        ["space"] = "Space"
    };

    internal static string? NormalizeConsoleInput(ConsoleKeyInfo input) =>
        NormalizeConsoleKey(input.Key) ?? NormalizeCharacter(input.KeyChar);

    internal static string? NormalizeConsoleKey(ConsoleKey key) =>
        KeyTokens.TryGetValue(key, out var token) ? token : null;

    internal static string? NormalizeCharacter(char input)
    {
        var normalized = char.ToLowerInvariant(input);
        return CharacterTokens.TryGetValue(normalized, out var token) ? token : null;
    }

    internal static bool MatchesConsoleKey(ConsoleKeyInfo input, ConsoleKey expectedKey)
    {
        var actual = NormalizeConsoleInput(input);
        var expected = NormalizeConsoleKey(expectedKey);
        return actual != null && string.Equals(actual, expected, StringComparison.Ordinal);
    }

    internal static string FormatPromptLabel(ConsoleKey key)
    {
        var token = NormalizeConsoleKey(key);
        return token == null ? key.ToString().ToUpperInvariant() : FormatPromptLabel(token);
    }

    internal static string FormatPromptLabel(string token) =>
        PromptLabels.TryGetValue(token, out var label) ? label : token.ToUpperInvariant();
}
