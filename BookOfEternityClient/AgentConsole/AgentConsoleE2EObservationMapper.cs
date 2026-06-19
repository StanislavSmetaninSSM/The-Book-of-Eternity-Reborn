using BookOfEternityClient.Configuration;

namespace BookOfEternityClient.AgentConsole;

public static class AgentConsoleE2EObservationMapper
{
    public static AgentConsoleSnapshot ToAgentConsoleSnapshot(
        ConsoleE2EObservationSnapshot observation,
        string? screenId = null,
        string? ansiText = null)
    {
        ArgumentNullException.ThrowIfNull(observation);

        var selectedIndex = ResolveSelectedIndex(observation.Options, observation.SelectedOption);
        var mode = ToAgentConsoleMode(observation.InputMode);
        var inputKind = ToAgentConsoleInputKind(observation.InputMode);

        return new AgentConsoleSnapshot
        {
            ScreenId = string.IsNullOrWhiteSpace(screenId)
                ? $"e2e:{observation.RunId}:{observation.StepIndex}"
                : screenId,
            Mode = mode,
            Title = observation.ScreenTitle,
            PlainText = observation.PlayerFacingText,
            AnsiText = ansiText,
            AwaitingInput = inputKind is AgentConsoleInputKind.MenuSelection
                or AgentConsoleInputKind.Text
                or AgentConsoleInputKind.Confirmation,
            InputKind = inputKind,
            SelectedIndex = selectedIndex,
            Actions = BuildActions(observation.Options, selectedIndex, inputKind),
            Prompt = BuildPrompt(observation, inputKind),
            RenderedAtUtc = observation.CapturedAtUtc,
            UpdatedAtUtc = observation.CapturedAtUtc,
            Diagnostics = BuildDiagnostics(observation)
        };
    }

    private static AgentConsoleMode ToAgentConsoleMode(ConsoleE2EInputMode mode) => mode switch
    {
        ConsoleE2EInputMode.Menu => AgentConsoleMode.Menu,
        ConsoleE2EInputMode.TextPrompt => AgentConsoleMode.TextPrompt,
        ConsoleE2EInputMode.Confirmation => AgentConsoleMode.Confirmation,
        ConsoleE2EInputMode.Loading => AgentConsoleMode.Loading,
        ConsoleE2EInputMode.Error => AgentConsoleMode.Error,
        ConsoleE2EInputMode.Exit => AgentConsoleMode.Exit,
        _ => throw new ArgumentOutOfRangeException(nameof(mode), mode, null)
    };

    private static AgentConsoleInputKind ToAgentConsoleInputKind(ConsoleE2EInputMode mode) => mode switch
    {
        ConsoleE2EInputMode.Menu => AgentConsoleInputKind.MenuSelection,
        ConsoleE2EInputMode.TextPrompt => AgentConsoleInputKind.Text,
        ConsoleE2EInputMode.Confirmation => AgentConsoleInputKind.Confirmation,
        ConsoleE2EInputMode.Loading => AgentConsoleInputKind.None,
        ConsoleE2EInputMode.Error => AgentConsoleInputKind.None,
        ConsoleE2EInputMode.Exit => AgentConsoleInputKind.None,
        _ => throw new ArgumentOutOfRangeException(nameof(mode), mode, null)
    };

    private static int? ResolveSelectedIndex(IReadOnlyList<string> options, string? selectedOption)
    {
        if (string.IsNullOrWhiteSpace(selectedOption))
            return null;

        for (var index = 0; index < options.Count; index++)
        {
            if (string.Equals(options[index], selectedOption, StringComparison.Ordinal))
                return index;
        }

        return null;
    }

    private static AgentConsolePrompt? BuildPrompt(
        ConsoleE2EObservationSnapshot observation,
        AgentConsoleInputKind inputKind)
    {
        if (inputKind is not (AgentConsoleInputKind.Text or AgentConsoleInputKind.Confirmation))
            return null;

        return new AgentConsolePrompt
        {
            PromptId = $"e2e:{observation.RunId}:{observation.StepIndex}:prompt",
            Text = observation.PlayerFacingText,
            InputKind = inputKind,
            DefaultValue = inputKind == AgentConsoleInputKind.Confirmation ? observation.SelectedOption : null,
            Choices = observation.Options.ToArray()
        };
    }

    private static IReadOnlyList<AgentConsoleAction> BuildActions(
        IReadOnlyList<string> options,
        int? selectedIndex,
        AgentConsoleInputKind inputKind)
    {
        return options
            .Select((label, index) => new AgentConsoleAction
            {
                Id = $"option-{index}",
                Label = label,
                Shortcut = inputKind == AgentConsoleInputKind.Confirmation ? ResolveConfirmationShortcut(index) : null,
                IsDefault = selectedIndex == index
            })
            .ToArray();
    }

    private static string? ResolveConfirmationShortcut(int optionIndex) => optionIndex switch
    {
        0 => "y",
        1 => "n",
        _ => null
    };

    private static IReadOnlyList<AgentConsoleDiagnostic> BuildDiagnostics(ConsoleE2EObservationSnapshot observation)
    {
        if (string.IsNullOrWhiteSpace(observation.ErrorType) &&
            string.IsNullOrWhiteSpace(observation.ErrorMessage) &&
            observation.InputMode != ConsoleE2EInputMode.Error)
        {
            return [];
        }

        var message = !string.IsNullOrWhiteSpace(observation.ErrorMessage)
            ? observation.ErrorMessage
            : !string.IsNullOrWhiteSpace(observation.ErrorType)
                ? observation.ErrorType
                : "Console observation is in error mode.";

        return
        [
            new AgentConsoleDiagnostic
            {
                Severity = AgentConsoleDiagnosticSeverity.Error,
                Code = "console-e2e",
                Message = message,
                ExceptionType = string.IsNullOrWhiteSpace(observation.ErrorType) ? null : observation.ErrorType,
                Detail = null
            }
        ];
    }
}
