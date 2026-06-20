using System.Text;
using System.Collections;
using System.Text.RegularExpressions;
using System.Reflection;
using BookOfEternityClient.Core;
using Spectre.Console;
using Spectre.Console.Rendering;

namespace BookOfEternityClient.UI;

public sealed class AgentConsoleRecordingExplorerConsole : IExplorerConsole
{
    private static readonly Regex AnsiEscapeRegex = new(@"\x1B\[[0-?]*[ -/]*[@-~]", RegexOptions.Compiled);

    private readonly IExplorerConsole _inner;
    private readonly StringBuilder _buffer = new();

    public AgentConsoleRecordingExplorerConsole(IExplorerConsole inner)
    {
        _inner = inner ?? throw new ArgumentNullException(nameof(inner));
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

    private static string NormalizeText(string text)
    {
        if (string.IsNullOrWhiteSpace(text))
            return string.Empty;

        var withoutAnsi = AnsiEscapeRegex.Replace(text, string.Empty);
        return withoutAnsi.Replace("\r\n", "\n").Replace('\r', '\n').Trim();
    }
}
