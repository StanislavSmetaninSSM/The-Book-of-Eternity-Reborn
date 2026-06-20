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
}

internal sealed record ConsoleCommandOutputQualityReport(string VisibleText, IReadOnlyList<string> Violations)
{
    public bool IsUsablePlayerOutput => Violations.Count == 0;
}
