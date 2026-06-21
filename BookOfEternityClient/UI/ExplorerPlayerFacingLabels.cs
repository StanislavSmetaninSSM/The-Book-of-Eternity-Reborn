using BookOfEternityClient.CommandProtocol;
using BookOfEternityClient.Services;

namespace BookOfEternityClient.UI;

internal static class ExplorerPlayerFacingLabels
{
    public static string Realm(string realm) => realm.Trim() switch
    {
        "" => "неизвестно",
        "Mortal World" or "Mortal Realm" => "Смертный мир",
        "Chaos Sea" => "Море Хаоса",
        "Shining Abode" => "Сияющая Обитель",
        _ => realm.Trim()
    };

    public static string WorldTime(string worldTime)
    {
        var result = worldTime.Trim();
        if (string.IsNullOrWhiteSpace(result))
            return string.Empty;

        foreach (var (source, label) in WorldTimeTerms)
            result = result.Replace(source, label, StringComparison.OrdinalIgnoreCase);

        return result;
    }

    private static readonly IReadOnlyList<(string Source, string Label)> WorldTimeTerms =
    [
        ("Month of Beginnings", "Месяц Начал")
    ];

    public static string CurrentMapNode(MapViewDto map)
    {
        if (string.IsNullOrWhiteSpace(map.CurrentNodeId))
            return string.Empty;

        var node = map.Nodes.FirstOrDefault(node =>
            string.Equals(node.Id, map.CurrentNodeId, StringComparison.OrdinalIgnoreCase));
        if (!string.IsNullOrWhiteSpace(node?.Label))
            return node.Label;

        node = map.Nodes.FirstOrDefault(node => node.IsCurrent);
        if (!string.IsNullOrWhiteSpace(node?.Label))
            return node.Label;

        return "отмечена на карте";
    }

    public static string LocationLinkState(string linkState) => linkState.Trim().ToLowerInvariant() switch
    {
        "" or "safe" or "open" or "available" or "unknown" => string.Empty,
        "dangerous" => "опасный путь",
        "hidden" => "скрытый путь",
        "blocked" => "путь закрыт",
        "requires key" or "requires_key" or "locked" => "нужен ключ",
        "restricted" => "доступ ограничен",
        "unstable" => "нестабильный путь",
        _ => "особое состояние пути"
    };

    public static string LocationLinkStateColor(string linkState) => linkState.Trim().ToLowerInvariant() switch
    {
        "dangerous" => "red",
        "hidden" => "grey",
        "blocked" or "requires key" or "requires_key" or "locked" => "maroon",
        "restricted" or "unstable" => "yellow",
        _ => "aqua"
    };

    public static string SystemModDescription(string description)
    {
        var trimmed = description.Trim();
        return IsPlaceholderDescription(trimmed)
            ? string.Empty
            : trimmed;
    }

    public static List<(string Label, SystemModService.SystemModDescriptor Mod)> SystemModChoiceLabels(
        IReadOnlyList<SystemModService.SystemModDescriptor> mods)
    {
        var totals = mods
            .GroupBy(static mod => mod.Name, StringComparer.OrdinalIgnoreCase)
            .ToDictionary(static group => group.Key, static group => group.Count(), StringComparer.OrdinalIgnoreCase);
        var seen = new Dictionary<string, int>(StringComparer.OrdinalIgnoreCase);
        var result = new List<(string Label, SystemModService.SystemModDescriptor Mod)>(mods.Count);

        foreach (var mod in mods)
        {
            seen.TryGetValue(mod.Name, out var index);
            index++;
            seen[mod.Name] = index;

            var label = $"📄 {mod.Name}";
            if (totals.TryGetValue(mod.Name, out var total) && total > 1)
                label += $" #{index}";

            result.Add((label, mod));
        }

        return result;
    }

    private static bool IsPlaceholderDescription(string value) =>
        string.Equals(value, "Description", StringComparison.OrdinalIgnoreCase) ||
        string.Equals(value, "Summary", StringComparison.OrdinalIgnoreCase) ||
        string.Equals(value, "Overview", StringComparison.OrdinalIgnoreCase);
}
