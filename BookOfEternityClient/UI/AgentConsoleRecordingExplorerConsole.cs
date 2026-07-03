using System.Text;
using System.Collections;
using System.Globalization;
using System.Text.RegularExpressions;
using System.Reflection;
using BookOfEternityClient.AgentConsole;
using BookOfEternityClient.Core;
using Spectre.Console;
using Spectre.Console.Rendering;

namespace BookOfEternityClient.UI;

public sealed class AgentConsoleRecordingExplorerConsole : IExplorerConsole
{
    private static readonly Regex AnsiEscapeRegex = new(@"\x1B\[[0-?]*[ -/]*[@-~]", RegexOptions.Compiled);

    private readonly IExplorerConsole _inner;
    private readonly AgentConsoleLiveInputSource? _liveInput;
    private readonly StringBuilder _buffer = new();
    private int _selectionPromptScreenIndex;

    public AgentConsoleRecordingExplorerConsole(
        IExplorerConsole inner,
        AgentConsoleLiveInputSource? liveInput = null)
    {
        _inner = inner ?? throw new ArgumentNullException(nameof(inner));
        _liveInput = liveInput;
    }

    public bool KeyAvailable => _inner.KeyAvailable;

    public void Clear()
    {
        _inner.Clear();
        ClearCapture();
    }

    public void Write(IRenderable content)
    {
        _inner.Write(content);
        AppendCaptured(RenderPlainText(content));
    }

    public void WriteLine()
    {
        _inner.WriteLine();
        _buffer.AppendLine();
    }

    public void Markup(string markup)
    {
        _inner.Markup(markup);
        _buffer.Append(StripMarkup(markup));
    }

    public void MarkupLine(string markup)
    {
        _inner.MarkupLine(markup);
        _buffer.AppendLine(StripMarkup(markup));
    }

    public string Ask(string prompt, string defaultValue = "")
    {
        AppendCaptured(StripMarkup(prompt));
        if (_liveInput is not null)
            return RunLiveTextPrompt(prompt, defaultValue);

        return _inner.Ask(prompt, defaultValue);
    }

    public bool Confirm(string prompt, bool defaultValue = false)
    {
        AppendCaptured(StripMarkup(prompt));
        if (_liveInput is not null)
            return RunLiveConfirmationPrompt(prompt, defaultValue);

        return _inner.Confirm(prompt, defaultValue);
    }

    public T Prompt<T>(IPrompt<T> prompt)
    {
        if (_liveInput is not null &&
            prompt is ConfirmationPrompt confirmationPrompt &&
            typeof(T) == typeof(bool))
        {
            var promptMarkup = ReadConfirmationPromptText(confirmationPrompt);
            var confirmed = RunLiveConfirmationPrompt(promptMarkup, confirmationPrompt.DefaultValue, confirmationPrompt.Yes, confirmationPrompt.No);
            return (T)(object)confirmed;
        }

        if (_liveInput is not null && TryRunLiveSelectionPrompt(prompt, out var selected))
            return selected;

        if (_liveInput is not null && TryRunLiveScalarTextPrompt(prompt, out var scalarValue))
            return scalarValue;

        try
        {
            return _inner.Prompt(prompt);
        }
        catch (NotSupportedException) when (TryBuildSelectionPromptFallback(prompt, out var fallback, out var captureText))
        {
            AppendCaptured(captureText);
            return fallback;
        }
    }

    public string? ReadLine() => _inner.ReadLine();

    public ConsoleKeyInfo ReadKey() => _inner.ReadKey();

    public string ReadCapturedText()
        => NormalizeText(_buffer.ToString());

    public void ClearCapture()
        => _buffer.Clear();

    private void AppendCaptured(string text)
    {
        if (string.IsNullOrWhiteSpace(text))
            return;

        _buffer.AppendLine(text.TrimEnd());
    }

    private static string StripMarkup(string markup)
    {
        if (string.IsNullOrEmpty(markup))
            return string.Empty;

        try
        {
            return Spectre.Console.Markup.Remove(markup);
        }
        catch
        {
            return markup;
        }
    }

    private static string RenderPlainText(IRenderable content)
    {
        using var writer = new StringWriter();
        var console = AnsiConsole.Create(new AnsiConsoleSettings
        {
            Ansi = AnsiSupport.No,
            ColorSystem = ColorSystemSupport.NoColors,
            Interactive = InteractionSupport.No,
            Out = new AnsiConsoleOutput(writer)
        });

        console.Write(content);
        return NormalizeText(writer.ToString());
    }

    private static bool TryBuildSelectionPromptFallback<T>(
        IPrompt<T> prompt,
        out T fallback,
        out string captureText)
    {
        fallback = default!;
        captureText = string.Empty;

        if (!prompt.GetType().IsGenericType ||
            prompt.GetType().GetGenericTypeDefinition() != typeof(SelectionPrompt<>))
        {
            return false;
        }

        var choices = ReadSelectionPromptChoices(prompt);
        if (choices.Count == 0)
            return false;

        var converter = prompt.GetType().GetProperty("Converter")?.GetValue(prompt) as Func<T, string>;
        var labels = choices
            .Select(choice => StripMarkup(converter?.Invoke(choice) ?? choice?.ToString() ?? string.Empty))
            .ToList();
        var fallbackIndex = FindSafeSelectionIndex(labels);
        fallback = choices[fallbackIndex];

        var title = StripMarkup(prompt.GetType().GetProperty("Title")?.GetValue(prompt) as string ?? "Выбор");
        var builder = new StringBuilder();
        builder.AppendLine(title);
        builder.AppendLine("Варианты:");
        for (var index = 0; index < labels.Count; index++)
            builder.AppendLine($"  {(index == fallbackIndex ? "→" : "•")} {labels[index]}");
        captureText = builder.ToString();
        return true;
    }

    private bool TryRunLiveSelectionPrompt<T>(
        IPrompt<T> prompt,
        out T selected)
    {
        selected = default!;

        if (!prompt.GetType().IsGenericType ||
            prompt.GetType().GetGenericTypeDefinition() != typeof(SelectionPrompt<>))
        {
            return false;
        }

        var choices = ReadSelectionPromptChoices(prompt);
        if (choices.Count == 0)
            return false;

        var converter = prompt.GetType().GetProperty("Converter")?.GetValue(prompt) as Func<T, string>;
        var labels = choices
            .Select(choice => StripMarkup(converter?.Invoke(choice) ?? choice?.ToString() ?? string.Empty))
            .ToArray();
        var safeIndex = FindSafeSelectionIndex(labels);
        var selectedIndex = safeIndex;
        var title = StripMarkup(prompt.GetType().GetProperty("Title")?.GetValue(prompt) as string ?? "Выбор");
        var screenId = $"explorer-selection-{++_selectionPromptScreenIndex}";

        PublishSelectionPromptSnapshot(screenId, title, labels, selectedIndex);

        while (true)
        {
            var key = _inner.ReadKey();
            var selectionChanged = false;

            switch (key.Key)
            {
                case ConsoleKey.UpArrow:
                case ConsoleKey.W:
                    selectedIndex = (selectedIndex - 1 + choices.Count) % choices.Count;
                    selectionChanged = true;
                    break;
                case ConsoleKey.DownArrow:
                case ConsoleKey.S:
                    selectedIndex = (selectedIndex + 1) % choices.Count;
                    selectionChanged = true;
                    break;
                case ConsoleKey.Escape:
                    selected = choices[safeIndex];
                    return true;
                case ConsoleKey.Enter:
                    selected = choices[selectedIndex];
                    return true;
                default:
                    if (TryMapSelectionNumber(key, choices.Count, out var numberIndex))
                    {
                        selectedIndex = numberIndex;
                        selectionChanged = true;
                    }

                    break;
            }

            if (selectionChanged)
                PublishSelectionPromptSnapshot(screenId, title, labels, selectedIndex);
        }
    }

    private bool TryRunLiveScalarTextPrompt<T>(
        IPrompt<T> prompt,
        out T selected)
    {
        selected = default!;

        if (!prompt.GetType().IsGenericType ||
            prompt.GetType().GetGenericTypeDefinition() != typeof(TextPrompt<>))
        {
            return false;
        }

        var targetType = Nullable.GetUnderlyingType(typeof(T)) ?? typeof(T);
        if (targetType != typeof(string) &&
            targetType != typeof(int) &&
            targetType != typeof(long) &&
            targetType != typeof(decimal) &&
            targetType != typeof(double) &&
            targetType != typeof(float))
        {
            return false;
        }

        var promptMarkup = ReadTextPromptText(prompt);
        var defaultValueText = ReadTextPromptDefaultValueText(prompt);

        while (true)
        {
            var line = RunLiveTextPrompt(promptMarkup, defaultValueText);
            if (!TryConvertScalarPromptValue(line, targetType, out var converted))
            {
                promptMarkup = $"{ReadTextPromptText(prompt)}\n[red]Введите корректное значение.[/]";
                continue;
            }

            if (!ValidateTextPromptValue(prompt, converted, out var validationMessage))
            {
                var safeMessage = string.IsNullOrWhiteSpace(validationMessage)
                    ? "Введите корректное значение."
                    : validationMessage;
                promptMarkup = $"{ReadTextPromptText(prompt)}\n[red]{Spectre.Console.Markup.Escape(StripMarkup(safeMessage))}[/]";
                continue;
            }

            selected = (T)converted!;
            return true;
        }
    }

    private void PublishSelectionPromptSnapshot(
        string screenId,
        string title,
        IReadOnlyList<string> labels,
        int selectedIndex)
    {
        var now = DateTimeOffset.UtcNow;
        var snapshot = new AgentConsoleSnapshot
        {
            ScreenId = screenId,
            Mode = AgentConsoleMode.Menu,
            Title = title,
            PlainText = BuildSelectionPromptPlainText(title, labels, selectedIndex),
            AwaitingInput = true,
            InputKind = AgentConsoleInputKind.MenuSelection,
            SelectedIndex = selectedIndex,
            Actions = labels.Select((label, index) => new AgentConsoleAction
            {
                Id = $"option-{index}",
                Label = string.IsNullOrWhiteSpace(label) ? $"Пункт {index + 1}" : label,
                IsDefault = selectedIndex == index
            }).ToArray(),
            RenderedAtUtc = now,
            UpdatedAtUtc = now
        };

        _liveInput?.PublishSnapshot(snapshot, $"Rendered explorer selection prompt {screenId}.");
    }

    private bool RunLiveConfirmationPrompt(
        string promptMarkup,
        bool defaultValue,
        char yesKey = 'y',
        char noKey = 'n')
    {
        var promptText = StripMarkup(promptMarkup);
        var screenId = $"explorer-confirmation-{++_selectionPromptScreenIndex}";
        PublishConfirmationPromptSnapshot(screenId, promptText, defaultValue, yesKey, noKey);

        while (true)
        {
            var key = _inner.ReadKey();
            var resolved = ResolveConfirmationKey(key, defaultValue, yesKey, noKey);
            if (resolved.HasValue)
                return resolved.Value;
        }
    }

    private void PublishConfirmationPromptSnapshot(
        string screenId,
        string promptText,
        bool defaultValue,
        char yesKey,
        char noKey)
    {
        var now = DateTimeOffset.UtcNow;
        var plainText = BuildConfirmationPromptPlainText(promptText, defaultValue);
        var snapshot = new AgentConsoleSnapshot
        {
            ScreenId = screenId,
            Mode = AgentConsoleMode.Confirmation,
            Title = "Подтверждение",
            PlainText = plainText,
            AwaitingInput = true,
            InputKind = AgentConsoleInputKind.Confirmation,
            SelectedIndex = defaultValue ? 0 : 1,
            Actions =
            [
                new AgentConsoleAction
                {
                    Id = "yes",
                    Label = "Да",
                    Shortcut = yesKey.ToString(),
                    IsDefault = defaultValue
                },
                new AgentConsoleAction
                {
                    Id = "no",
                    Label = "Нет",
                    Shortcut = noKey.ToString(),
                    IsDefault = !defaultValue
                }
            ],
            Prompt = new AgentConsolePrompt
            {
                PromptId = screenId,
                Text = promptText,
                InputKind = AgentConsoleInputKind.Confirmation,
                DefaultValue = defaultValue ? "Да" : "Нет",
                Choices = ["Да", "Нет"]
            },
            RenderedAtUtc = now,
            UpdatedAtUtc = now
        };

        _liveInput?.PublishSnapshot(snapshot, $"Rendered explorer confirmation prompt {screenId}.");
    }

    private string RunLiveTextPrompt(string promptMarkup, string defaultValue)
    {
        var promptText = StripMarkup(promptMarkup);
        var screenId = $"explorer-text-{++_selectionPromptScreenIndex}";
        PublishTextPromptSnapshot(screenId, promptText, defaultValue);

        _inner.Markup(promptMarkup);
        var line = _inner.ReadLine();
        if (string.IsNullOrEmpty(line))
            return defaultValue;

        return line;
    }

    private void PublishTextPromptSnapshot(
        string screenId,
        string promptText,
        string defaultValue)
    {
        var now = DateTimeOffset.UtcNow;
        var snapshot = new AgentConsoleSnapshot
        {
            ScreenId = screenId,
            Mode = AgentConsoleMode.TextPrompt,
            Title = "Ввод текста",
            PlainText = BuildTextPromptPlainText(promptText, defaultValue),
            AwaitingInput = true,
            InputKind = AgentConsoleInputKind.Text,
            Prompt = new AgentConsolePrompt
            {
                PromptId = screenId,
                Text = promptText,
                InputKind = AgentConsoleInputKind.Text,
                DefaultValue = defaultValue
            },
            RenderedAtUtc = now,
            UpdatedAtUtc = now
        };

        _liveInput?.PublishSnapshot(snapshot, $"Rendered explorer text prompt {screenId}.");
    }

    private string BuildTextPromptPlainText(string promptText, string defaultValue)
    {
        var builder = new StringBuilder();
        var context = ReadCapturedText();
        if (!string.IsNullOrWhiteSpace(context))
        {
            builder.AppendLine(context);
            builder.AppendLine();
        }

        builder.AppendLine(promptText);
        if (!string.IsNullOrWhiteSpace(defaultValue))
            builder.AppendLine($"По умолчанию: {defaultValue}");
        return NormalizeText(builder.ToString());
    }

    private string BuildConfirmationPromptPlainText(string promptText, bool defaultValue)
    {
        var builder = new StringBuilder();
        var context = ReadCapturedText();
        if (!string.IsNullOrWhiteSpace(context))
        {
            builder.AppendLine(context);
            builder.AppendLine();
        }

        builder.AppendLine(promptText);
        builder.AppendLine(defaultValue ? "[Y/n]" : "[y/N]");
        return NormalizeText(builder.ToString());
    }

    private static bool? ResolveConfirmationKey(
        ConsoleKeyInfo key,
        bool defaultValue,
        char yesKey,
        char noKey)
    {
        if (key.Key == ConsoleKey.Enter)
            return defaultValue;

        if (char.ToLowerInvariant(key.KeyChar) == char.ToLowerInvariant(yesKey))
            return true;

        if (char.ToLowerInvariant(key.KeyChar) == char.ToLowerInvariant(noKey))
            return false;

        return null;
    }

    private static string ReadConfirmationPromptText(ConfirmationPrompt prompt)
    {
        var raw = prompt.GetType()
            .GetField("_prompt", BindingFlags.Instance | BindingFlags.NonPublic)
            ?.GetValue(prompt) as string;
        return string.IsNullOrWhiteSpace(raw) ? "Подтвердить?" : raw;
    }

    private static string ReadTextPromptText<T>(IPrompt<T> prompt)
    {
        var raw = prompt.GetType()
            .GetField("_prompt", BindingFlags.Instance | BindingFlags.NonPublic)
            ?.GetValue(prompt) as string;
        return string.IsNullOrWhiteSpace(raw) ? "Введите значение:" : raw;
    }

    private static string ReadTextPromptDefaultValueText<T>(IPrompt<T> prompt)
    {
        var defaultValue = prompt.GetType()
            .GetProperty("DefaultValue", BindingFlags.Instance | BindingFlags.Public)
            ?.GetValue(prompt);
        var value = defaultValue?.GetType()
            .GetProperty("Value", BindingFlags.Instance | BindingFlags.Public)
            ?.GetValue(defaultValue);
        return value == null
            ? string.Empty
            : Convert.ToString(value, CultureInfo.CurrentCulture) ?? string.Empty;
    }

    private static bool TryConvertScalarPromptValue(string? line, Type targetType, out object? value)
    {
        value = null;
        var text = line?.Trim() ?? string.Empty;

        if (targetType == typeof(string))
        {
            value = text;
            return true;
        }

        if (targetType == typeof(int) &&
            int.TryParse(text, NumberStyles.Integer, CultureInfo.CurrentCulture, out var intValue))
        {
            value = intValue;
            return true;
        }

        if (targetType == typeof(long) &&
            long.TryParse(text, NumberStyles.Integer, CultureInfo.CurrentCulture, out var longValue))
        {
            value = longValue;
            return true;
        }

        if (targetType == typeof(decimal) &&
            decimal.TryParse(text, NumberStyles.Number, CultureInfo.CurrentCulture, out var decimalValue))
        {
            value = decimalValue;
            return true;
        }

        if (targetType == typeof(double) &&
            double.TryParse(text, NumberStyles.Float | NumberStyles.AllowThousands, CultureInfo.CurrentCulture, out var doubleValue))
        {
            value = doubleValue;
            return true;
        }

        if (targetType == typeof(float) &&
            float.TryParse(text, NumberStyles.Float | NumberStyles.AllowThousands, CultureInfo.CurrentCulture, out var floatValue))
        {
            value = floatValue;
            return true;
        }

        return false;
    }

    private static bool ValidateTextPromptValue<T>(
        IPrompt<T> prompt,
        object? converted,
        out string? validationMessage)
    {
        validationMessage = null;
        var validateResult = prompt.GetType()
            .GetMethod("ValidateResult", BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic);
        if (validateResult == null)
            return true;

        var parameters = new[] { converted, validationMessage };
        var valid = validateResult.Invoke(prompt, parameters) as bool?;
        validationMessage = parameters[1] as string;
        return valid != false;
    }

    private string BuildSelectionPromptPlainText(
        string title,
        IReadOnlyList<string> labels,
        int selectedIndex)
    {
        var builder = new StringBuilder();
        var context = ReadCapturedText();
        if (!string.IsNullOrWhiteSpace(context))
        {
            builder.AppendLine(context);
            builder.AppendLine();
        }

        builder.AppendLine(title);
        builder.AppendLine("Варианты:");
        for (var index = 0; index < labels.Count; index++)
        {
            var marker = index == selectedIndex ? ">" : " ";
            builder.AppendLine($"{marker} {index + 1}. {labels[index]}");
        }

        return NormalizeText(builder.ToString());
    }

    private static List<T> ReadSelectionPromptChoices<T>(IPrompt<T> prompt)
    {
        var result = new List<T>();
        var tree = prompt.GetType().GetField("_tree", BindingFlags.Instance | BindingFlags.NonPublic)?.GetValue(prompt);
        var roots = tree?.GetType().GetField("_roots", BindingFlags.Instance | BindingFlags.NonPublic)?.GetValue(tree) as IEnumerable;
        if (roots == null)
            return result;

        foreach (var item in roots)
            CollectSelectionPromptItem(item, result);

        return result;
    }

    private static void CollectSelectionPromptItem<T>(object? item, List<T> result)
    {
        if (item == null)
            return;

        var data = item.GetType().GetProperty("Data")?.GetValue(item);
        if (data is T value)
            result.Add(value);

        var children = item.GetType().GetProperty("Children")?.GetValue(item) as IEnumerable;
        if (children == null)
            return;

        foreach (var child in children)
            CollectSelectionPromptItem(child, result);
    }

    private static int FindSafeSelectionIndex(IReadOnlyList<string> labels)
    {
        for (var index = 0; index < labels.Count; index++)
        {
            var label = labels[index];
            if (label.Contains("Назад", StringComparison.OrdinalIgnoreCase) ||
                label.Contains("Закрыть", StringComparison.OrdinalIgnoreCase) ||
                label.Contains("Отмена", StringComparison.OrdinalIgnoreCase) ||
                label.Contains("Back", StringComparison.OrdinalIgnoreCase) ||
                label.Contains("Close", StringComparison.OrdinalIgnoreCase) ||
                label.Contains("Cancel", StringComparison.OrdinalIgnoreCase))
            {
                return index;
            }
        }

        return 0;
    }

    private static bool TryMapSelectionNumber(ConsoleKeyInfo key, int optionsCount, out int index)
    {
        index = -1;

        int? numeric = key.Key switch
        {
            ConsoleKey.D1 or ConsoleKey.NumPad1 => 1,
            ConsoleKey.D2 or ConsoleKey.NumPad2 => 2,
            ConsoleKey.D3 or ConsoleKey.NumPad3 => 3,
            ConsoleKey.D4 or ConsoleKey.NumPad4 => 4,
            ConsoleKey.D5 or ConsoleKey.NumPad5 => 5,
            ConsoleKey.D6 or ConsoleKey.NumPad6 => 6,
            ConsoleKey.D7 or ConsoleKey.NumPad7 => 7,
            ConsoleKey.D8 or ConsoleKey.NumPad8 => 8,
            ConsoleKey.D9 or ConsoleKey.NumPad9 => 9,
            _ => null
        };

        if (!numeric.HasValue || numeric.Value > optionsCount)
            return false;

        index = numeric.Value - 1;
        return true;
    }

    private static string NormalizeText(string text)
    {
        if (string.IsNullOrWhiteSpace(text))
            return string.Empty;

        var withoutAnsi = AnsiEscapeRegex.Replace(text, string.Empty);
        return withoutAnsi.Replace("\r\n", "\n").Replace('\r', '\n').Trim();
    }
}
