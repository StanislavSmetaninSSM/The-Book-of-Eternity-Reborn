using BookOfEternityClient.CommandProtocol;

namespace BookOfEternityClient.Tests;

internal static class ConsoleCommandOutputQualityClassifier
{
    private static readonly string[] ForbiddenPlayerFacingMarkers =
    [
        "game_state/",
        ".json",
        "DTO",
        "API",
        "endpoint",
        "protocol",
        "pending_",
        "requestId",
        "actionType",
        "debug",
        "debug_logs",
        "null",
        "UiRawJsonBlock",
        "JsonObject",
        "JsonArray",
        "JsonValue"
    ];

    public static ConsoleCommandOutputQualityReport Classify(ExplorerCommandResult result)
    {
        var violations = new List<string>();

        if (result.State is CommandExecutionState.Failed or CommandExecutionState.Blocked)
            violations.Add($"state is {result.State}");

        if (result.Blocks.Count == 0 && result.Actions.Count == 0 && result.Prompts.Count == 0)
            violations.Add("result has no visible blocks, actions, or prompts");

        if (result.Blocks.OfType<UiRawJsonBlock>().Any())
            violations.Add("default output contains raw JSON block");

        var visibleText = CollectVisibleText(result);
        if (string.IsNullOrWhiteSpace(visibleText))
            violations.Add("default output has no readable text");

        foreach (var forbidden in ForbiddenPlayerFacingMarkers)
        {
            if (visibleText.Contains(forbidden, StringComparison.OrdinalIgnoreCase))
                violations.Add($"visible text leaks technical marker: {forbidden}");
        }

        return new ConsoleCommandOutputQualityReport(visibleText, violations);
    }

    private static string CollectVisibleText(ExplorerCommandResult result)
    {
        var parts = new List<string>();
        foreach (var block in result.Blocks)
            CollectBlockText(block, parts);

        parts.AddRange(result.Actions.Select(static action => action.Label));

        foreach (var prompt in result.Prompts)
        {
            parts.Add(prompt.Prompt);
            switch (prompt)
            {
                case UiTextInputPrompt textInput:
                    parts.Add(textInput.Placeholder);
                    break;
                case UiLongTextInputPrompt longTextInput:
                    parts.Add(longTextInput.Placeholder);
                    break;
                case UiSelectionPrompt selection:
                    parts.AddRange(selection.Options.Select(static option => option.Label));
                    parts.AddRange(selection.Options.Select(static option => option.Description));
                    break;
            }
        }

        foreach (var notification in result.Notifications)
        {
            parts.Add(notification.Title);
            parts.Add(notification.Message);
        }

        return string.Join("\n", parts.Where(static part => !string.IsNullOrWhiteSpace(part)));
    }

    private static void CollectBlockText(UiBlock block, List<string> parts)
    {
        switch (block)
        {
            case UiTextBlock text:
                parts.Add(text.Text);
                break;
            case UiPanelBlock panel:
                parts.Add(panel.Title);
                foreach (var child in panel.Blocks)
                    CollectBlockText(child, parts);
                break;
            case UiEntityDossierBlock dossier:
                parts.Add(dossier.Title);
                parts.Add(dossier.Subtitle);
                parts.Add(dossier.Summary);
                parts.AddRange(dossier.Badges.Select(static badge => badge.Label));
                parts.AddRange(dossier.Facts.SelectMany(static fact => new[] { fact.Label, fact.Value }));
                parts.AddRange(dossier.Metrics.Select(static metric => metric.Label));
                parts.AddRange(dossier.Hints.SelectMany(static hint => new[] { hint.Title, hint.Text }));
                parts.AddRange(dossier.List);
                foreach (var card in dossier.Cards)
                    CollectCardText(card, parts);
                foreach (var section in dossier.Sections)
                    CollectSectionText(section, parts);
                break;
            case UiTableBlock table:
                parts.Add(table.Title);
                parts.AddRange(table.Columns);
                parts.AddRange(table.Rows.SelectMany(static row => row.Cells));
                break;
            case UiListBlock list:
                parts.AddRange(list.Items);
                break;
            case UiKeyValueGridBlock grid:
                parts.AddRange(grid.Items.SelectMany(static item => new[] { item.Key, item.Value }));
                break;
            case UiMessageBlock message:
                parts.Add(message.Title);
                parts.Add(message.Message);
                break;
            case UiRawJsonBlock raw:
                parts.Add(raw.Title);
                break;
        }
    }

    private static void CollectSectionText(UiEntityDossierSection section, List<string> parts)
    {
        parts.Add(section.Title);
        parts.Add(section.Summary);
        parts.AddRange(section.Facts.SelectMany(static fact => new[] { fact.Label, fact.Value }));
        parts.AddRange(section.Metrics.Select(static metric => metric.Label));
        parts.AddRange(section.Hints.SelectMany(static hint => new[] { hint.Title, hint.Text }));
        parts.AddRange(section.List);
        foreach (var card in section.Cards)
            CollectCardText(card, parts);
        foreach (var block in section.Blocks)
            CollectBlockText(block, parts);
    }

    private static void CollectCardText(UiEntityCard card, List<string> parts)
    {
        parts.Add(card.Title);
        parts.Add(card.Subtitle);
        parts.Add(card.Summary);
        parts.AddRange(card.Badges.Select(static badge => badge.Label));
        parts.AddRange(card.Facts.SelectMany(static fact => new[] { fact.Label, fact.Value }));
        parts.AddRange(card.Metrics.Select(static metric => metric.Label));
        parts.AddRange(card.Hints.SelectMany(static hint => new[] { hint.Title, hint.Text }));
        parts.AddRange(card.List);
        foreach (var child in card.Nested)
            CollectCardText(child, parts);
        foreach (var child in card.Cards)
            CollectCardText(child, parts);
    }
}

internal sealed record ConsoleCommandOutputQualityReport(string VisibleText, IReadOnlyList<string> Violations)
{
    public bool IsUsablePlayerOutput => Violations.Count == 0;
}
