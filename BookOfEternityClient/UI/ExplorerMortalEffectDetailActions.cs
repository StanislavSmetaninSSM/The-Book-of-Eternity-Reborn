using System.Text.Json.Nodes;
using BookOfEternityClient.CommandProtocol;

namespace BookOfEternityClient.UI;

internal static class ExplorerMortalEffectDetailActions
{
    public static IReadOnlyList<UiAction> Build(string commandToken, JsonNode? effectsRoot)
    {
        var entries = BuildEffectSnapshots(effectsRoot);
        var actions = new List<UiAction>();
        foreach (var entry in entries)
        {
            actions.Add(new UiAction
            {
                Id = "effects-detail-" + ToActionIdPart(entry.Selector),
                Label = $"Подробнее: «{entry.Name}»",
                Command = BuildEffectDetailCommand(commandToken, entry.Selector),
                Style = UiActionStyle.Secondary,
                RequiresConfirmation = false,
                Payload = new JsonObject
                {
                    ["selector"] = entry.Selector,
                    ["name"] = entry.Name,
                    ["section"] = entry.Section
                }
            });
        }

        return actions;
    }

    public static IReadOnlyList<EffectSnapshot> BuildEffectSnapshots(JsonNode? node)
    {
        var entries = new List<EffectSnapshot>();
        var usedSelectors = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        if (node is JsonArray array)
        {
            AddEffectSnapshots(entries, usedSelectors, "Активный эффект", array);
            return entries;
        }

        if (node is not JsonObject root)
            return entries;

        AddEffectSnapshots(entries, usedSelectors, "Активный эффект", root["activeEffects"] as JsonArray);
        AddEffectSnapshots(entries, usedSelectors, "Рана", root["wounds"] as JsonArray);
        AddEffectSnapshots(entries, usedSelectors, "Временное состояние", root["temporaryConditions"] as JsonArray);
        return entries;
    }

    private static void AddEffectSnapshots(
        List<EffectSnapshot> entries,
        HashSet<string> usedSelectors,
        string section,
        JsonArray? effects)
    {
        if (effects == null)
            return;

        foreach (var effect in effects.OfType<JsonObject>())
        {
            var name = FirstNonEmpty(
                GetNodeString(effect, "name"),
                GetNodeString(effect, "effectName"),
                GetNodeString(effect, "title"),
                "Безымянный эффект");
            var identity = FirstNonEmpty(
                GetNodeString(effect, "effectId"),
                GetNodeString(effect, "conditionId"),
                GetNodeString(effect, "woundId"),
                GetNodeString(effect, "id"),
                name);
            var selector = BuildUniqueEffectSelector(identity, entries.Count, usedSelectors);
            entries.Add(new EffectSnapshot(entries.Count + 1, selector, section, name, effect));
        }
    }

    private static string BuildEffectDetailCommand(string commandToken, string selector)
    {
        var detailToken = string.Equals(commandToken, "/effects", StringComparison.OrdinalIgnoreCase)
            ? "effect"
            : "эффект";
        return commandToken + " " + detailToken + " " + FormatCommandArgument(selector);
    }

    private static string BuildUniqueEffectSelector(string value, int index, HashSet<string> usedSelectors)
    {
        var baseSelector = NormalizeReferenceSelector(value);
        if (string.IsNullOrWhiteSpace(baseSelector))
            baseSelector = $"effect-{index + 1}";

        var selector = baseSelector;
        var suffix = 2;
        while (!usedSelectors.Add(selector))
        {
            selector = $"{baseSelector}-{suffix}";
            suffix++;
        }

        return selector;
    }

    private static string NormalizeReferenceSelector(string value)
    {
        var chars = value.Trim().ToLowerInvariant()
            .Select(static ch => char.IsLetterOrDigit(ch) ? ch : '_')
            .ToArray();
        var result = new string(chars).Trim('_');
        return string.IsNullOrWhiteSpace(result) ? "item" : result;
    }

    private static string FormatCommandArgument(string selector)
    {
        if (selector.All(static ch => char.IsLetterOrDigit(ch) || ch is '_' or '-' or '.'))
            return selector;

        return "\"" + selector.Replace("\\", "\\\\", StringComparison.Ordinal).Replace("\"", "\\\"", StringComparison.Ordinal) + "\"";
    }

    private static string ToActionIdPart(string value)
    {
        var chars = value
            .Select(static ch => char.IsLetterOrDigit(ch) || ch is '-' or '_' ? ch : '-')
            .ToArray();
        var result = new string(chars).Trim('-');
        return string.IsNullOrWhiteSpace(result) ? "item" : result;
    }

    private static string GetNodeString(JsonNode? node, string propertyName) =>
        node?[propertyName] switch
        {
            JsonValue value when value.TryGetValue<string>(out var text) => text,
            JsonValue value when value.TryGetValue<int>(out var number) => number.ToString(),
            JsonValue value when value.TryGetValue<long>(out var number) => number.ToString(),
            _ => string.Empty
        };

    private static string FirstNonEmpty(params string[] values) =>
        values.FirstOrDefault(static value => !string.IsNullOrWhiteSpace(value))?.Trim() ?? string.Empty;

    internal sealed record EffectSnapshot(
        int Index,
        string Selector,
        string Section,
        string Name,
        JsonObject Node);
}
