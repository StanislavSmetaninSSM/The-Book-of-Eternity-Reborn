using BookOfEternityClient.AgentConsole;
using BookOfEternityClient.Configuration;
using BookOfEternityClient.Core;
using Spectre.Console;

namespace BookOfEternityClient.Services;

public sealed partial class QteSceneService
{
    private string PromptQteSelection(
        string screenId,
        string screenTitle,
        string playerFacingText,
        IReadOnlyList<string> choices,
        string promptMarkup,
        string promptPlainText,
        Color highlightColor)
    {
        if (_inputSource is AgentConsoleLiveInputSource liveInput)
            return PromptAgentConsoleSelection(liveInput, screenId, screenTitle, playerFacingText, choices, promptPlainText);

        return AnsiConsole.Prompt(new SelectionPrompt<string>()
            .Title(promptMarkup)
            .HighlightStyle(new Style(highlightColor))
            .AddChoices(choices));
    }

    private void WaitForQteContinueKey(
        string screenId,
        string screenTitle,
        string playerFacingText)
    {
        if (_inputSource is AgentConsoleLiveInputSource liveInput)
        {
            var snapshot = new AgentConsoleSnapshot
            {
                ScreenId = screenId,
                Mode = AgentConsoleMode.TextPrompt,
                Title = screenTitle,
                PlainText = playerFacingText,
                AwaitingInput = true,
                InputKind = AgentConsoleInputKind.Key,
                RenderedAtUtc = DateTimeOffset.UtcNow,
                UpdatedAtUtc = DateTimeOffset.UtcNow,
                Diagnostics = []
            };
            liveInput.PublishSnapshot(snapshot, $"Rendered {screenId}.");
        }

        _inputSource.ReadKey(intercept: true);
    }

    private string PromptAgentConsoleSelection(
        AgentConsoleLiveInputSource liveInput,
        string screenId,
        string screenTitle,
        string playerFacingText,
        IReadOnlyList<string> choices,
        string promptPlainText)
    {
        if (choices.Count == 0)
            throw new InvalidOperationException($"QTE screen '{screenId}' has no choices.");

        var selectedIndex = 0;
        while (true)
        {
            PublishAgentConsoleMenuSnapshot(
                liveInput,
                screenId,
                screenTitle,
                BuildAgentConsoleMenuText(screenTitle, playerFacingText, promptPlainText, choices, selectedIndex),
                choices,
                selectedIndex);

            var key = _inputSource.ReadKey(intercept: true);
            var inputResult = ConsoleMainMenuInputHandler.Apply(key, selectedIndex, choices.Count);
            selectedIndex = inputResult.SelectedIndex;

            if (inputResult.ActivateSelection)
                return choices[selectedIndex];
        }
    }

    private static void PublishAgentConsoleMenuSnapshot(
        AgentConsoleLiveInputSource liveInput,
        string screenId,
        string screenTitle,
        string playerFacingText,
        IReadOnlyList<string> choices,
        int selectedIndex)
    {
        var observation = new ConsoleE2EObservationSnapshot(
            RunId: "agent-console",
            StepIndex: 0,
            CapturedAtUtc: DateTimeOffset.UtcNow,
            InputMode: ConsoleE2EInputMode.Menu,
            ScreenTitle: screenTitle,
            PlayerFacingText: playerFacingText,
            Options: choices,
            SelectedOption: choices[Math.Clamp(selectedIndex, 0, choices.Count - 1)],
            ArtifactRoot: string.Empty,
            LogPath: null);
        var snapshot = AgentConsoleE2EObservationMapper.ToAgentConsoleSnapshot(observation, screenId);
        liveInput.PublishSnapshot(snapshot, $"Rendered {screenId}.");
    }

    private static string BuildAgentConsoleMenuText(
        string screenTitle,
        string playerFacingText,
        string promptPlainText,
        IReadOnlyList<string> choices,
        int selectedIndex)
    {
        var lines = new List<string>();
        if (!string.IsNullOrWhiteSpace(screenTitle))
            lines.Add(screenTitle.Trim());

        if (!string.IsNullOrWhiteSpace(playerFacingText))
        {
            if (lines.Count > 0)
                lines.Add(string.Empty);
            lines.Add(playerFacingText.Trim());
        }

        lines.Add(string.Empty);
        lines.Add(promptPlainText.Trim());
        for (var index = 0; index < choices.Count; index++)
        {
            var marker = index == selectedIndex ? ">" : " ";
            lines.Add($"{marker} {index + 1}. {choices[index]}");
        }

        return string.Join(Environment.NewLine, lines);
    }

    private static string BuildQteOfferPlainText(QteOffer offer)
    {
        var lines = new List<string> { offer.Title ?? "QTE событие" };
        if (!string.IsNullOrWhiteSpace(offer.OfferText))
            lines.Add(offer.OfferText!);
        if (!string.IsNullOrWhiteSpace(offer.IntroNarrative))
            lines.Add(offer.IntroNarrative!);
        if (!string.IsNullOrWhiteSpace(offer.CinematicJustification))
            lines.Add("Почему QTE: " + offer.CinematicJustification);
        if (!string.IsNullOrWhiteSpace(offer.DeclineHint))
            lines.Add(offer.DeclineHint!);

        return string.Join(Environment.NewLine + Environment.NewLine, lines);
    }

    private static string BuildQteChapterPlainText(
        QteOffer offer,
        QteChapter chapter,
        QteScoreState? scoreState)
    {
        var lines = new List<string>
        {
            chapter.Title ?? offer.Title ?? "QTE сцена"
        };

        if (!string.IsNullOrWhiteSpace(chapter.Narrative))
            lines.Add(chapter.Narrative!);

        var visibleMetrics = GetVisibleActiveScoreMetrics(scoreState).ToList();
        if (visibleMetrics.Count > 0)
        {
            lines.Add("Счёт сцены:");
            foreach (var metric in visibleMetrics)
                lines.Add($"- {metric.Label}: {FormatScoreValue(metric.Value)}");
        }

        return string.Join(Environment.NewLine + Environment.NewLine, lines);
    }

    private static string ToAgentConsoleScreenPart(string? value)
    {
        if (string.IsNullOrWhiteSpace(value))
            return "unknown";

        var builder = new System.Text.StringBuilder(value.Length);
        foreach (var ch in value.Trim().ToLowerInvariant())
            builder.Append(char.IsLetterOrDigit(ch) ? ch : '-');

        var safe = builder.ToString().Trim('-');
        return string.IsNullOrWhiteSpace(safe) ? "unknown" : safe;
    }

    private static string StripSpectreMarkup(string value)
    {
        if (string.IsNullOrEmpty(value))
            return string.Empty;

        var builder = new System.Text.StringBuilder(value.Length);
        var insideTag = false;
        foreach (var ch in value)
        {
            if (ch == '[')
            {
                insideTag = true;
                continue;
            }

            if (insideTag)
            {
                if (ch == ']')
                    insideTag = false;
                continue;
            }

            builder.Append(ch);
        }

        return builder.ToString();
    }
}
