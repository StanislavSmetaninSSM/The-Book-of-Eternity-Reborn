using BookOfEternityClient.AgentConsole;
using BookOfEternityClient.Configuration;
using BookOfEternityClient.Models.GameState;
using Spectre.Console;

namespace BookOfEternityClient.Core;

public partial class GameEngine
{
    private void RecordConsoleObservation(
        ConsoleE2EInputMode inputMode,
        string screenTitle,
        string playerFacingText,
        IReadOnlyList<string> options,
        string? selectedOption,
        string slug,
        string? logPath = null)
    {
        if (_inputSource is ConsoleE2EScriptedInputSource scriptedInput)
        {
            scriptedInput.WriteObservation(
                inputMode,
                screenTitle,
                playerFacingText,
                options,
                selectedOption,
                slug,
                logPath);
        }

        if (_inputSource is not AgentConsoleLiveInputSource liveInput)
            return;

        var observation = new ConsoleE2EObservationSnapshot(
            RunId: "agent-console",
            StepIndex: 0,
            CapturedAtUtc: DateTimeOffset.UtcNow,
            InputMode: inputMode,
            ScreenTitle: screenTitle,
            PlayerFacingText: playerFacingText,
            Options: options,
            SelectedOption: selectedOption,
            ArtifactRoot: string.Empty,
            LogPath: null);
        var snapshot = AgentConsoleE2EObservationMapper.ToAgentConsoleSnapshot(observation, screenId: slug);
        liveInput.PublishSnapshot(snapshot, $"Rendered {slug}.");
    }

    private void RecordGameLoopPromptObservation()
    {
        var state = _stateManager.CurrentState;
        var options = _lastResponse?.DialogueOptions?
            .Select(option => option.Text)
            .Where(text => !string.IsNullOrWhiteSpace(text))
            .Select(text => text!)
            .ToArray() ?? [];

        RecordConsoleObservation(
            ConsoleE2EInputMode.TextPrompt,
            "Ваш ход",
            BuildGameLoopPromptObservationText(state),
            options,
            selectedOption: null,
            slug: "game-loop");
    }

    private void RecordGameLoopErrorObservation(Exception ex)
    {
        RecordConsoleObservation(
            ConsoleE2EInputMode.Error,
            "Ошибка игрового цикла",
            $"Ошибка в игровом цикле: {ex.Message}\nОшибка сохранена в game_session/error_log.txt. Данные не потеряны.\n{_loc.T("press_any_key")}",
            [],
            selectedOption: null,
            slug: "game-loop-error");
    }

    private bool ConfirmWithConsoleObservation(
        string promptMarkup,
        string plainPrompt,
        bool defaultValue,
        string slug,
        string title)
    {
        if (_inputSource is not AgentConsoleLiveInputSource &&
            _inputSource is not ConsoleE2EScriptedInputSource)
        {
            return AnsiConsole.Confirm(promptMarkup, defaultValue);
        }

        RecordConsoleObservation(
            ConsoleE2EInputMode.Confirmation,
            title,
            plainPrompt,
            ["Да", "Нет"],
            selectedOption: defaultValue ? "Да" : "Нет",
            slug);

        AnsiConsole.Markup($"{promptMarkup} ");
        AnsiConsole.WriteLine(defaultValue ? "[Y/n]" : "[y/N]");

        while (true)
        {
            var key = _inputSource.ReadKey(intercept: true);
            var resolved = ResolveConfirmationKey(key, defaultValue);
            if (resolved.HasValue)
                return resolved.Value;
        }
    }

    private static bool? ResolveConfirmationKey(ConsoleKeyInfo key, bool defaultValue)
    {
        if (key.Key == ConsoleKey.Enter)
            return defaultValue;

        var keyChar = char.ToLowerInvariant(key.KeyChar);
        return key.Key switch
        {
            ConsoleKey.Y => true,
            ConsoleKey.N => false,
            _ when keyChar is 'y' or 'д' => true,
            _ when keyChar is 'n' or 'т' => false,
            _ => null
        };
    }

    private string BuildGameLoopPromptObservationText(AggregatedGameState state)
    {
        var lines = new List<string>();
        var realm = state.IsInShiningAbodePendingBootstrap
            ? "Сияющая Обитель: handoff"
            : state.IsInShiningAbode
                ? _loc.T("realm_shining_abode")
                : state.IsInAfterlifeRealm
                    ? _loc.T("realm_chaos_sea")
                    : _loc.T("realm_mortal");

        lines.Add($"Область: {realm}");
        lines.Add($"Ход: {_gameLoop.TurnNumber}");

        if (state.IsInAfterlifeRealm)
        {
            if (!string.IsNullOrWhiteSpace(state.SoulName))
                lines.Add($"Душа: {state.SoulName}");
            if (!string.IsNullOrWhiteSpace(state.ActiveGuardianName))
                lines.Add($"Хранитель: {state.ActiveGuardianName}");
            lines.Add($"Чернильные перья: {state.InkFeathers}");
        }
        else
        {
            if (!string.IsNullOrWhiteSpace(state.CharacterName))
                lines.Add($"Персонаж: {state.CharacterName}");
            if (!string.IsNullOrWhiteSpace(state.CharacterRace) || !string.IsNullOrWhiteSpace(state.CharacterClass))
                lines.Add($"Роль: {JoinNonEmpty(state.CharacterRace, state.CharacterClass)}");
            if (!string.IsNullOrWhiteSpace(state.CurrentLocation))
                lines.Add($"Локация: {state.CurrentLocation}");
            AppendMortalStatus(lines, state.PlayerStatus);
        }

        var narrative = _lastResponse?.Response;
        if (string.IsNullOrWhiteSpace(narrative))
            narrative = state.Narrative;
        if (!string.IsNullOrWhiteSpace(narrative))
        {
            lines.Add(string.Empty);
            lines.Add("Сцена:");
            lines.Add(narrative);
        }

        if (_lastResponse?.DialogueOptions is { Length: > 0 } dialogueOptions)
        {
            lines.Add(string.Empty);
            lines.Add("Варианты:");
            foreach (var (option, index) in dialogueOptions.Select((option, index) => (option, index)))
            {
                if (!string.IsNullOrWhiteSpace(option.Text))
                    lines.Add($"{index + 1}. {option.Text}");
            }
        }

        lines.Add(string.Empty);
        lines.Add(BuildCommandHint(state));
        lines.Add("Enter = отправить; \\m = текстовый редактор; \\p = fallback-вставка.");

        return string.Join(Environment.NewLine, lines);
    }

    private static void AppendMortalStatus(List<string> lines, PlayerStatusState status)
    {
        lines.Add($"Здоровье: {status.HealthPercentage}");
        lines.Add($"Энергия: {status.EnergyPercentage}");
        lines.Add($"Равновесие: {status.PoisePercentage}");

        if (!string.IsNullOrWhiteSpace(status.CurrentCondition))
            lines.Add($"Состояние: {status.CurrentCondition}");

        if (status.ActiveConditions.Length > 0)
            lines.Add("Активные состояния: " + string.Join("; ", status.ActiveConditions));
    }

    private string BuildCommandHint(AggregatedGameState state)
    {
        if (state.IsInShiningAbodePendingBootstrap)
            return "Подготовка следующей жизни уже передана в bootstrap; обычные действия Обители и Моря Хаоса здесь недоступны.";

        if (state.IsInShiningAbode)
            return "/статус /status | /сияющая_обитель /shining_abode | /сияющая_политика /shining_politics | /перья /архив_души /уведомления_загробья | /реликвии /хранители /душа | /вернуться_в_море_хаоса /новая_игра+ | /help";

        if (state.IsInChaosSea)
        {
            return state.CanReenterShiningAbode
                ? "/статус /реликвии /хранители /обители /гача /перья /архив_души | /воплотиться /вернуться_в_обитель | /help"
                : "/статус /реликвии /хранители /обители /гача /перья /архив_души | /воплотиться | /help";
        }

        return "/инв /квесты /карта /статус | /конец_жизни | /help";
    }

    private static string JoinNonEmpty(params string?[] values)
        => string.Join(" / ", values.Where(value => !string.IsNullOrWhiteSpace(value)));
}
