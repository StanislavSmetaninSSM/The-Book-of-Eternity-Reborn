using System.Text.Json.Nodes;
using BookOfEternityClient.Services;

namespace BookOfEternityClient.UI;

internal static class ReadableInventoryDocumentShelfProjection
{
    private const int PreviewLimit = 96;

    public static ReadableDocumentShelf Build(
        JsonNode? inventoryRoot,
        JsonNode? itemTextRoot,
        JsonNode? itemJournalRoot)
    {
        var documents = ReadableInventoryDocumentAuthority.ResolveDocuments(inventoryRoot, itemTextRoot, itemJournalRoot);

        var items = new List<ReadableDocumentShelfItem>();
        var usedSelectors = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        foreach (var document in documents)
        {
            var aliases = document.Identities
                .Concat([document.Name])
                .Where(static value => !string.IsNullOrWhiteSpace(value))
                .Distinct(StringComparer.OrdinalIgnoreCase)
                .ToList();
            var selector = BuildUniqueSelector(FirstNonEmpty(document.Identities) ?? document.Name, items.Count, usedSelectors);
            items.Add(CreateItem(
                selector,
                document.Name,
                $"Предмет: {document.Name}",
                document.TextEntries,
                document.UnreadableReason,
                aliases));
        }

        return new ReadableDocumentShelf(items);
    }

    public static ReadableDocumentShelfItem? FindBySelector(ReadableDocumentShelf shelf, string? selector)
    {
        var normalized = NormalizeSelectorArgument(selector);
        if (string.IsNullOrWhiteSpace(normalized))
            return null;

        var exactMatch = shelf.Items.FirstOrDefault(item =>
            string.Equals(item.Selector, normalized, StringComparison.OrdinalIgnoreCase) ||
            item.SelectionAliases.Any(alias => string.Equals(alias, normalized, StringComparison.OrdinalIgnoreCase)));
        if (exactMatch is not null)
            return exactMatch;

        if (int.TryParse(normalized, out var oneBasedIndex) &&
            oneBasedIndex >= 1 &&
            oneBasedIndex <= shelf.Items.Count)
        {
            return shelf.Items[oneBasedIndex - 1];
        }

        return null;
    }

    public static string FormatCommandArgument(string selector) =>
        IsSimpleCommandArgument(selector)
            ? selector
            : "\"" + selector.Replace("\"", "\\\"", StringComparison.Ordinal) + "\"";

    private static ReadableDocumentShelfItem CreateItem(
        string selector,
        string title,
        string source,
        IReadOnlyList<string> textEntries,
        string? unreadableReason,
        IReadOnlyList<string> aliases)
    {
        var entries = SplitEntries(textEntries);
        var hasReadableContent = entries.Count > 0;
        var accessStatus = hasReadableContent ? "Можно читать" : "Не прочесть";
        var countLabel = hasReadableContent
            ? FormatEntryCount(entries.Count)
            : FirstNonEmpty(unreadableReason) ?? "Текст пока недоступен.";
        var summary = hasReadableContent
            ? $"{countLabel}: {BuildPreview(entries)}"
            : countLabel;

        return new ReadableDocumentShelfItem(
            Selector: selector,
            Title: string.IsNullOrWhiteSpace(title) ? "Безымянный документ" : title.Trim(),
            Source: source,
            AccessStatus: accessStatus,
            Summary: summary,
            CountLabel: countLabel,
            Entries: entries,
            UnreadableReason: unreadableReason,
            SelectionAliases: aliases);
    }

    private static IReadOnlyList<string> SplitEntries(IReadOnlyList<string> textEntries)
    {
        var result = new List<string>();
        foreach (var entry in textEntries)
        {
            var normalized = NormalizeWhitespace(entry);
            if (!string.IsNullOrWhiteSpace(normalized))
                result.Add(normalized);
        }

        return result;
    }

    private static string BuildPreview(IReadOnlyList<string> entries)
    {
        var first = entries.FirstOrDefault() ?? string.Empty;
        if (first.Length <= PreviewLimit)
            return first;

        return first[..PreviewLimit].TrimEnd() + "...";
    }

    private static string BuildUniqueSelector(string value, int index, HashSet<string> usedSelectors)
    {
        var baseSelector = NormalizeSelector(value);
        if (string.IsNullOrWhiteSpace(baseSelector))
            baseSelector = $"doc-{index + 1}";

        var selector = baseSelector;
        var suffix = 2;
        while (!usedSelectors.Add(selector))
        {
            selector = $"{baseSelector}-{suffix}";
            suffix++;
        }

        return selector;
    }

    private static string NormalizeSelector(string value)
    {
        var chars = new List<char>();
        foreach (var ch in value.Trim())
        {
            if (char.IsLetterOrDigit(ch) || ch is '_' or '-' or '.')
            {
                chars.Add(ch);
                continue;
            }

            if (char.IsWhiteSpace(ch) && chars.Count > 0 && chars[^1] != '-')
                chars.Add('-');
        }

        return new string(chars.ToArray()).Trim('-');
    }

    private static string NormalizeSelectorArgument(string? value)
    {
        var trimmed = value?.Trim() ?? string.Empty;
        if (trimmed.Length >= 2 &&
            trimmed[0] == '"' &&
            trimmed[^1] == '"')
        {
            trimmed = trimmed[1..^1].Replace("\\\"", "\"", StringComparison.Ordinal);
        }

        return trimmed;
    }

    private static bool IsSimpleCommandArgument(string value) =>
        value.Length > 0 && value.All(static ch => char.IsLetterOrDigit(ch) || ch is '_' or '-' or '.');

    private static string NormalizeWhitespace(string value) =>
        string.Join(' ', value.Split((char[]?)null, StringSplitOptions.RemoveEmptyEntries)).Trim();

    private static string FormatEntryCount(int count)
    {
        var lastTwo = count % 100;
        var last = count % 10;
        var noun = lastTwo is >= 11 and <= 14
            ? "записей"
            : last switch
            {
                1 => "запись",
                >= 2 and <= 4 => "записи",
                _ => "записей"
            };

        return $"{count} {noun}";
    }

    private static string? FirstNonEmpty(IEnumerable<string?> values) =>
        values.FirstOrDefault(static value => !string.IsNullOrWhiteSpace(value));

    private static string? FirstNonEmpty(params string?[] values) =>
        FirstNonEmpty((IEnumerable<string?>)values);
}

internal sealed record ReadableDocumentShelf(IReadOnlyList<ReadableDocumentShelfItem> Items);

internal sealed record ReadableDocumentShelfItem(
    string Selector,
    string Title,
    string Source,
    string AccessStatus,
    string Summary,
    string CountLabel,
    IReadOnlyList<string> Entries,
    string? UnreadableReason,
    IReadOnlyList<string> SelectionAliases)
{
    public bool HasReadableContent => Entries.Count > 0;

    public string ChoiceLabel => $"📜 {Title} — {AccessStatus} — {CountLabel}";
}
