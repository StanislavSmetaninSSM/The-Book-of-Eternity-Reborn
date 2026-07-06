using BookOfEternityClient.AgentConsole;
using BookOfEternityClient.Configuration;
using BookOfEternityClient.Models.GameState;
using System.Text;
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
        string? logPath = null,
        IReadOnlyList<string>? actionInputValues = null)
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
        var snapshot = AgentConsoleE2EObservationMapper.ToAgentConsoleSnapshot(
            observation,
            screenId: slug,
            actionInputValues: actionInputValues);
        liveInput.PublishSnapshot(snapshot, $"Rendered {slug}.");
    }

    private void RecordGameLoopPromptObservation()
    {
        var state = _stateManager.CurrentState;
        var dialogueOptions = _lastResponse?.DialogueOptions ?? [];
        var visibleOptions = new List<string>();
        var actionInputValues = new List<string>();
        foreach (var option in dialogueOptions)
        {
            var visibleText = DialogueOptionControlTagNormalizer.NormalizeVisibleText(option.Text);
            if (string.IsNullOrWhiteSpace(visibleText))
                continue;

            visibleOptions.Add(visibleText);
            actionInputValues.Add(DialogueOptionControlTagNormalizer.ResolveInputValue(option.Text, option.InputValue) ?? visibleText);
        }

        RecordConsoleObservation(
            ConsoleE2EInputMode.TextPrompt,
            "Ваш ход",
            BuildGameLoopPromptObservationText(state),
            visibleOptions,
            selectedOption: null,
            slug: "game-loop",
            actionInputValues: actionInputValues);
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

    private void PublishAgentConsoleGmWaitingSnapshot(
        string title,
        string plainText)
    {
        if (_inputSource is not AgentConsoleLiveInputSource liveInput)
            return;

        var now = DateTimeOffset.UtcNow;
        liveInput.PublishSnapshot(new AgentConsoleSnapshot
        {
            ScreenId = "gm-waiting",
            Mode = AgentConsoleMode.Loading,
            Title = title,
            PlainText = plainText,
            AwaitingInput = false,
            InputKind = AgentConsoleInputKind.None,
            RenderedAtUtc = now,
            UpdatedAtUtc = now
        }, "Waiting for GM response.");
    }

    private void PublishAgentConsoleValidationRepairSnapshot(ValidationRepairRequest request)
    {
        if (_inputSource is not AgentConsoleLiveInputSource liveInput)
            return;

        var lines = new List<string>
        {
            $"Ремонт данных: ход {request.TurnNumber}, попытка {request.RevalidationAttempt}",
            $"Источник проверки: {request.Source}",
            $"Запрос: {request.RequestId}",
            string.Empty
        };

        if (request.SummaryGroups.Count > 0)
        {
            lines.Add("Сводка ошибок:");
            foreach (var summary in request.SummaryGroups.Take(6))
                lines.Add("- " + summary);
            lines.Add(string.Empty);
        }

        if (request.HarnessRepairPackets.Count > 0)
        {
            lines.Add("Harness-пакеты:");
            foreach (var packet in request.HarnessRepairPackets.Take(4))
                lines.Add("- " + JoinNonEmpty(packet.Kind, packet.Title));
            lines.Add(string.Empty);
        }

        if (request.Errors.Count > 0)
        {
            lines.Add("Первые ошибки:");
            foreach (var error in request.Errors.Take(5))
            {
                var code = string.IsNullOrWhiteSpace(error.Code) ? "validation_error" : error.Code;
                var path = string.IsNullOrWhiteSpace(error.FilePath) ? "<unknown path>" : error.FilePath;
                lines.Add("- " + code + " :: " + path);
                if (!string.IsNullOrWhiteSpace(error.Message))
                    lines.Add("  " + error.Message);
            }
            lines.Add(string.Empty);
        }

        lines.Add("GM сейчас исправляет данные и должен завершить ремонт через Complete-BoeValidationRepair или validation_repair_ready.json.");

        var diagnostics = request.Errors
            .Take(AgentConsoleLimits.MaxDiagnostics)
            .Select(error => new AgentConsoleDiagnostic
            {
                Severity = AgentConsoleDiagnosticSeverity.Warning,
                Code = "validation-repair-progress",
                Message = string.IsNullOrWhiteSpace(error.Code)
                    ? "Validation repair is in progress."
                    : $"Validation repair is in progress: {error.Code}",
                Detail = BuildValidationRepairDiagnosticDetail(error)
            })
            .ToArray();

        var now = DateTimeOffset.UtcNow;
        liveInput.PublishSnapshot(new AgentConsoleSnapshot
        {
            ScreenId = "gm-validation-repair",
            Mode = AgentConsoleMode.Loading,
            Title = "Ремонт данных",
            PlainText = string.Join(Environment.NewLine, lines),
            AwaitingInput = false,
            InputKind = AgentConsoleInputKind.None,
            RenderedAtUtc = now,
            UpdatedAtUtc = now,
            Diagnostics = diagnostics
        }, "Validation repair request published.");
    }

    private IDisposable? BeginAgentConsoleInputBlockFromCurrentSnapshot(string reason)
    {
        if (_inputSource is not AgentConsoleLiveInputSource liveInput)
            return null;

        return liveInput.BeginInputBlockFromCurrentSnapshot(reason);
    }

    private static string BuildValidationRepairDiagnosticDetail(ValidationRepairIssue error)
    {
        var detail = new StringBuilder();
        if (!string.IsNullOrWhiteSpace(error.FilePath))
            detail.Append(error.FilePath);
        if (!string.IsNullOrWhiteSpace(error.Message))
        {
            if (detail.Length > 0)
                detail.Append(": ");
            detail.Append(error.Message);
        }
        return detail.ToString();
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

    private string PromptAgentConsoleTextInput(
        string promptMarkup,
        string title,
        string playerFacingText,
        string slug,
        string? defaultValue = null,
        bool allowEmpty = true,
        string? emptyError = null,
        bool preserveNewlines = false,
        IReadOnlyList<string>? options = null,
        IReadOnlyList<string>? optionInputValues = null)
    {
        if (_inputSource is AgentConsoleLiveInputSource or ConsoleE2EScriptedInputSource)
        {
            RecordConsoleObservation(
                ConsoleE2EInputMode.TextPrompt,
                title,
                playerFacingText,
                options ?? [],
                selectedOption: null,
                slug,
                actionInputValues: optionInputValues);
        }

        return PromptTextInput(
            promptMarkup,
            defaultValue,
            allowEmpty,
            emptyError,
            preserveNewlines);
    }

    private string PromptAgentConsoleMenuSelection(
        string promptMarkup,
        string title,
        string playerFacingText,
        string slug,
        IReadOnlyList<string> options,
        IReadOnlyList<string>? optionInputValues = null,
        int selectedIndex = 0)
    {
        if (options.Count == 0)
            throw new ArgumentException("Menu prompt must contain at least one option.", nameof(options));

        selectedIndex = Math.Clamp(selectedIndex, 0, options.Count - 1);
        var numberBuffer = string.Empty;

        while (true)
        {
            if (_inputSource is AgentConsoleLiveInputSource or ConsoleE2EScriptedInputSource)
            {
                RecordConsoleObservation(
                    ConsoleE2EInputMode.Menu,
                    title,
                    playerFacingText,
                    options,
                    selectedOption: options[selectedIndex],
                    slug,
                    actionInputValues: optionInputValues);
            }
            else
            {
                AnsiConsole.Markup($"{promptMarkup} ");
            }

            var key = _inputSource.ReadKey(intercept: true);
            if (key.Key == ConsoleKey.Enter)
            {
                if (TryMapBufferedMenuNumberSelection(numberBuffer, options.Count, out var bufferedIndex))
                    return ResolveMenuSelectionValue(bufferedIndex, options, optionInputValues);
                return ResolveMenuSelectionValue(selectedIndex, options, optionInputValues);
            }

            if (key.Key is ConsoleKey.UpArrow or ConsoleKey.LeftArrow)
            {
                numberBuffer = string.Empty;
                selectedIndex = selectedIndex <= 0 ? options.Count - 1 : selectedIndex - 1;
                continue;
            }

            if (key.Key is ConsoleKey.DownArrow or ConsoleKey.RightArrow)
            {
                numberBuffer = string.Empty;
                selectedIndex = selectedIndex >= options.Count - 1 ? 0 : selectedIndex + 1;
                continue;
            }

            if (TryAppendMenuNumberSelection(key, options.Count, ref numberBuffer, out var numberIndex))
                selectedIndex = numberIndex;
        }
    }

    private static string ResolveMenuSelectionValue(
        int selectedIndex,
        IReadOnlyList<string> options,
        IReadOnlyList<string>? optionInputValues)
    {
        if (optionInputValues is not null &&
            selectedIndex >= 0 &&
            selectedIndex < optionInputValues.Count &&
            !string.IsNullOrWhiteSpace(optionInputValues[selectedIndex]))
        {
            return optionInputValues[selectedIndex];
        }

        return options[selectedIndex];
    }

    private ConsoleKeyInfo ReadAgentConsoleKeyContinuation(
        string title,
        string playerFacingText,
        string slug,
        string actionLabel = "Продолжить")
    {
        if (_inputSource is AgentConsoleLiveInputSource liveInput)
        {
            liveInput.PublishSnapshot(new AgentConsoleSnapshot
            {
                ScreenId = slug,
                Mode = AgentConsoleMode.TextPrompt,
                Title = title,
                PlainText = playerFacingText,
                AwaitingInput = true,
                InputKind = AgentConsoleInputKind.Key,
                Actions =
                [
                    new AgentConsoleAction
                    {
                        Id = "continue",
                        Label = actionLabel,
                        Shortcut = "Enter",
                        IsDefault = true
                    }
                ],
                Prompt = new AgentConsolePrompt
                {
                    PromptId = $"{slug}:key",
                    Text = playerFacingText,
                    InputKind = AgentConsoleInputKind.Key,
                    DefaultValue = "Enter"
                },
                RenderedAtUtc = DateTimeOffset.UtcNow,
                UpdatedAtUtc = DateTimeOffset.UtcNow
            }, $"Rendered {slug}.");
        }

        return _inputSource.ReadKey(intercept: true);
    }

    private void PublishAgentConsoleStatAllocationSnapshot(
        string title,
        int remaining,
        IReadOnlyDictionary<string, int> baseStats,
        IReadOnlyDictionary<string, int> allocations,
        IReadOnlyList<string> statList,
        int selectedIndex)
    {
        if (_inputSource is not AgentConsoleLiveInputSource liveInput)
            return;

        var lines = new List<string>
        {
            title,
            $"Доступно очков: {remaining}",
            string.Empty,
            "Характеристики:"
        };

        for (var index = 0; index < statList.Count; index++)
        {
            var key = statList[index];
            var name = Characteristics.RussianNames.GetValueOrDefault(key, key);
            var baseValue = baseStats.TryGetValue(key, out var resolvedBase) ? resolvedBase : 1;
            var allocated = allocations.TryGetValue(key, out var resolvedAllocated) ? resolvedAllocated : 0;
            var cursor = index == selectedIndex ? ">" : " ";
            lines.Add($"{cursor} {name}: {baseValue} + {allocated} = {baseValue + allocated}");
        }

        lines.Add(string.Empty);
        lines.Add("Используйте действия: выбрать характеристику, добавить/убрать очко, подтвердить распределение.");

        var now = DateTimeOffset.UtcNow;
        liveInput.PublishSnapshot(new AgentConsoleSnapshot
        {
            ScreenId = "stat-allocation",
            Mode = AgentConsoleMode.TextPrompt,
            Title = title,
            PlainText = string.Join(Environment.NewLine, lines),
            AwaitingInput = true,
            InputKind = AgentConsoleInputKind.Key,
            SelectedIndex = selectedIndex,
            Actions =
            [
                new AgentConsoleAction { Id = "stat-up", Label = "Вверх", Shortcut = "Up" },
                new AgentConsoleAction { Id = "stat-down", Label = "Вниз", Shortcut = "Down" },
                new AgentConsoleAction { Id = "stat-add", Label = "Добавить очко", Shortcut = "Right" },
                new AgentConsoleAction { Id = "stat-remove", Label = "Убрать очко", Shortcut = "Left" },
                new AgentConsoleAction { Id = "stat-confirm", Label = "Подтвердить", Shortcut = "Enter", IsDefault = true }
            ],
            Prompt = new AgentConsolePrompt
            {
                PromptId = "stat-allocation:key",
                Text = "Распределите начальные очки характеристик.",
                InputKind = AgentConsoleInputKind.Key,
                DefaultValue = "Enter"
            },
            RenderedAtUtc = now,
            UpdatedAtUtc = now
        }, "Rendered stat-allocation.");
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
                var visibleText = DialogueOptionControlTagNormalizer.NormalizeVisibleText(option.Text);
                if (!string.IsNullOrWhiteSpace(visibleText))
                    lines.Add($"{index + 1}. {visibleText}");
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
            return "/статус /status | /сияющая_обитель /shining_abode | /сияющая_политика /shining_politics | /перья /архив_души /хроники_посмертия /уведомления_загробья | /реликвии /хранители /душа | /вернуться_в_море_хаоса /новая_игра+ | /help";

        if (state.IsInChaosSea)
        {
            return state.CanReenterShiningAbode
                ? "/статус /реликвии /хранители /обители /гача /перья /архив_души /хроники_посмертия | /воплотиться /вернуться_в_обитель | /help"
                : "/статус /реликвии /хранители /обители /гача /перья /архив_души /хроники_посмертия | /воплотиться | /help";
        }

        return "/инв /квесты /карта /статус | /конец_жизни | /help";
    }

    private static string JoinNonEmpty(params string?[] values)
        => string.Join(" / ", values.Where(value => !string.IsNullOrWhiteSpace(value)));
}
