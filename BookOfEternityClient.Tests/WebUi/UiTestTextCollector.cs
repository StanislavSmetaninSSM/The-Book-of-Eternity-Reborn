using BookOfEternityClient.CommandProtocol;
using BookOfEternityClient.UI;

namespace BookOfEternityClient.Tests.WebUi;

internal static class UiTestTextCollector
{
    public static string CollectResultAndPromptText(ExplorerCommandResult result, bool includeRawJson = false) =>
        CollectBlockText(result.Blocks, includeRawJson) + "\n" +
        string.Join("\n", result.Prompts.Select(CollectPromptText)) + "\n" +
        string.Join("\n", result.Notifications.Select(static notification => $"{notification.Title}\n{notification.Message}"));

    public static string CollectBlockText(IEnumerable<UiBlock> blocks, bool includeRawJson = false)
    {
        var parts = new List<string>();
        foreach (var block in blocks)
            CollectBlockText(block, parts, includeRawJson);
        return string.Join("\n", parts);
    }

    public static string CollectPromptText(UiPrompt prompt)
    {
        var parts = new List<string> { prompt.Prompt };
        switch (prompt)
        {
            case UiSelectionPrompt selection:
                foreach (var option in selection.Options)
                {
                    parts.Add(option.Label);
                    parts.Add(option.Description);
                }
                break;
            case UiTextInputPrompt textInput:
                parts.Add(textInput.Placeholder);
                break;
            case UiLongTextInputPrompt longTextInput:
                parts.Add(longTextInput.Placeholder);
                break;
        }

        return string.Join("\n", parts);
    }

    private static void CollectBlockText(UiBlock block, List<string> parts, bool includeRawJson)
    {
        switch (block)
        {
            case UiTextBlock text:
                parts.Add(text.Text);
                break;
            case UiMessageBlock message:
                parts.Add(message.Title);
                parts.Add(message.Message);
                break;
            case UiPanelBlock panel:
                parts.Add(panel.Title);
                foreach (var child in panel.Blocks)
                    CollectBlockText(child, parts, includeRawJson);
                break;
            case UiEntityDossierBlock dossier:
                CollectDossierText(dossier, parts, includeRawJson);
                break;
            case UiTableBlock table:
                parts.Add(table.Title);
                parts.AddRange(table.Columns);
                foreach (var row in table.Rows)
                    parts.AddRange(row.Cells);
                break;
            case UiListBlock list:
                parts.AddRange(list.Items);
                break;
            case UiKeyValueGridBlock grid:
                foreach (var item in grid.Items)
                {
                    parts.Add(item.Key);
                    parts.Add(item.Value);
                }
                break;
            case UiRawJsonBlock raw when includeRawJson:
                parts.Add(raw.Title);
                parts.Add(raw.Json?.ToJsonString() ?? string.Empty);
                break;
        }
    }

    private static void CollectDossierText(UiEntityDossierBlock dossier, List<string> parts, bool includeRawJson)
    {
        parts.Add(dossier.Title);
        parts.Add(dossier.Subtitle);
        parts.Add(dossier.Summary);
        parts.AddRange(dossier.Badges.Select(static badge => badge.Label));
        parts.AddRange(dossier.Facts.SelectMany(static fact => new[] { fact.Label, fact.Value }));
        parts.AddRange(dossier.Metrics.SelectMany(static metric => new[] { metric.Label, metric.Note }));
        parts.AddRange(dossier.Hints.SelectMany(static hint => new[] { hint.Title, hint.Text }));
        parts.AddRange(dossier.List);
        foreach (var card in dossier.Cards)
            CollectCardText(card, parts);
        foreach (var section in dossier.Sections)
            CollectSectionText(section, parts, includeRawJson);
    }

    private static void CollectSectionText(UiEntityDossierSection section, List<string> parts, bool includeRawJson)
    {
        parts.Add(section.Title);
        parts.Add(section.Summary);
        parts.Add(section.CollectionLabel);
        parts.AddRange(section.Facts.SelectMany(static fact => new[] { fact.Label, fact.Value }));
        parts.AddRange(section.Metrics.SelectMany(static metric => new[] { metric.Label, metric.Note }));
        parts.AddRange(section.Hints.SelectMany(static hint => new[] { hint.Title, hint.Text }));
        parts.AddRange(section.List);
        foreach (var card in section.Cards)
            CollectCardText(card, parts);
        foreach (var block in section.Blocks)
            CollectBlockText(block, parts, includeRawJson);
    }

    private static void CollectCardText(UiEntityCard card, List<string> parts)
    {
        parts.Add(card.Title);
        parts.Add(card.Subtitle);
        parts.Add(card.Summary);
        parts.AddRange(card.Badges.Select(static badge => badge.Label));
        parts.AddRange(card.Facts.SelectMany(static fact => new[] { fact.Label, fact.Value }));
        parts.AddRange(card.Metrics.SelectMany(static metric => new[] { metric.Label, metric.Note }));
        parts.AddRange(card.Hints.SelectMany(static hint => new[] { hint.Title, hint.Text }));
        parts.AddRange(card.List);
        foreach (var nested in card.Nested)
            CollectCardText(nested, parts);
        foreach (var child in card.Cards)
            CollectCardText(child, parts);
    }
}
