using System.Text.Json.Nodes;
using BookOfEternityClient.CommandProtocol;

namespace BookOfEternityClient.UI;

internal static class ExplorerMortalEffectDetailActions
{
    internal static IReadOnlyList<UiAction> Build(
        string commandToken,
        EffectPlayerProjectionResult projection)
    {
        if (!projection.IsAvailable)
            return Array.Empty<UiAction>();

        return projection.Entries.Select(entry => new UiAction
        {
            Id = "effects-detail-" + ToActionIdPart(entry.Selector),
            Label = $"Подробнее: «{entry.Name}»",
            Command = BuildEffectDetailCommand(commandToken, entry.Selector),
            Style = UiActionStyle.Secondary,
            RequiresConfirmation = false,
            Payload = new JsonObject
            {
                ["selector"] = entry.Selector,
                ["name"] = entry.Name
            }
        }).ToArray();
    }

    internal static IReadOnlyList<UiAction> BuildForEntry(
        string commandToken,
        EffectPlayerEntry entry) =>
        entry.Actions.Select(action => new UiAction
        {
            Id = "effects-action-" + ToActionIdPart(action.Selector),
            Label = action.Label,
            Command = BuildEffectActionCommand(commandToken, action.Selector),
            Style = UiActionStyle.Primary,
            RequiresConfirmation = false,
            Payload = new JsonObject
            {
                ["selector"] = action.Selector,
                ["label"] = action.Label,
                ["description"] = action.Description,
                ["actionScope"] = "effect_only",
                ["woundTreatment"] = false
            }
        }).ToArray();

    internal static EffectPlayerEntry? FindExact(
        EffectPlayerProjectionResult projection,
        string? selector)
    {
        if (!projection.IsAvailable || string.IsNullOrWhiteSpace(selector))
            return null;

        EffectPlayerEntry? match = null;
        foreach (var entry in projection.Entries)
        {
            if (!string.Equals(entry.Selector, selector, StringComparison.Ordinal))
                continue;
            if (match != null)
                return null;
            match = entry;
        }

        return match;
    }

    internal static bool TryFindAction(
        EffectPlayerProjectionResult projection,
        string? selector,
        out EffectPlayerEntry entry,
        out EffectPlayerAction action)
    {
        entry = null!;
        action = null!;
        if (!projection.IsAvailable || string.IsNullOrWhiteSpace(selector))
            return false;

        EffectPlayerEntry? matchedEntry = null;
        EffectPlayerAction? matchedAction = null;
        foreach (var candidateEntry in projection.Entries)
        {
            foreach (var candidateAction in candidateEntry.Actions)
            {
                if (!string.Equals(candidateAction.Selector, selector, StringComparison.Ordinal))
                    continue;
                if (matchedAction != null)
                    return false;
                matchedEntry = candidateEntry;
                matchedAction = candidateAction;
            }
        }

        if (matchedEntry == null || matchedAction == null)
            return false;

        entry = matchedEntry;
        action = matchedAction;
        return true;
    }

    private static string BuildEffectDetailCommand(string commandToken, string selector)
    {
        var detailToken = string.Equals(commandToken, "/effects", StringComparison.OrdinalIgnoreCase)
            ? "effect"
            : "эффект";
        return commandToken + " " + detailToken + " " + selector;
    }

    private static string BuildEffectActionCommand(string commandToken, string selector)
    {
        var actionToken = string.Equals(commandToken, "/effects", StringComparison.OrdinalIgnoreCase)
            ? "action"
            : "действие";
        return commandToken + " " + actionToken + " " + selector;
    }

    private static string ToActionIdPart(string value)
    {
        var chars = value
            .Select(static ch => char.IsLetterOrDigit(ch) || ch is '-' or '_' ? ch : '-')
            .ToArray();
        var result = new string(chars).Trim('-');
        return string.IsNullOrWhiteSpace(result) ? "effect" : result;
    }
}
