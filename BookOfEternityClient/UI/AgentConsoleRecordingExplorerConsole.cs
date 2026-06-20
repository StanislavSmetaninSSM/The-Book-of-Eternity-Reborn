using System.Text;
using System.Collections;
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
        return _inner.Ask(prompt, defaultValue);
    }

    public bool Confirm(string prompt, bool defaultValue = false)
    {
        AppendCaptured(StripMarkup(prompt));
        return _inner.Confirm(prompt, defaultValue);
    }

    public T Prompt<T>(IPrompt<T> prompt)
    {
        if (_liveInput is not null && TryRunLiveSelectionPrompt(prompt, out var selected))
            return selected;

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
