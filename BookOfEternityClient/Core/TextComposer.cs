using System.Text;
using BookOfEternityClient.Services;
using Spectre.Console;

namespace BookOfEternityClient.Core;

internal enum TextComposerMode
{
    Immediate,
    MultilineEditor
}

internal sealed class TextComposerOptions
{
    public required string PromptMarkup { get; init; }
    public string? DefaultValue { get; init; }
    public bool AllowEmpty { get; init; } = true;
    public string? EmptyError { get; init; }
    public bool PreserveNewlines { get; init; }
    public TextComposerMode Mode { get; init; } = TextComposerMode.Immediate;
    public string? HelpMarkup { get; init; }
    public bool AllowClearCommand { get; init; }
    public string ClearCommand { get; init; } = "/clear";
}

internal static class TextComposer
{
    public static string Read(
        ITextComposerConsole console,
        IClipboardService? clipboardService,
        TextComposerOptions options)
    {
        while (true)
        {
            if (!string.IsNullOrWhiteSpace(options.HelpMarkup))
                console.MarkupLine(options.HelpMarkup);

            console.Markup($"{options.PromptMarkup} ");
            var value = options.Mode == TextComposerMode.MultilineEditor
                ? ReadMultiline(console, clipboardService, options)
                : ReadImmediate(console, clipboardService, options);

            if (options.AllowEmpty || !string.IsNullOrWhiteSpace(value))
                return value;

            console.MarkupLine($"[red]{Markup.Escape(options.EmptyError ?? "Значение не может быть пустым")}[/]");
        }
    }

    internal static string CollapseToSingleLine(string text)
    {
        return string.Join(" ", text
            .Replace("\r\n", "\n")
            .Replace('\r', '\n')
            .Split('\n', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries));
    }

    private static string ReadImmediate(
        ITextComposerConsole console,
        IClipboardService? clipboardService,
        TextComposerOptions options)
    {
        var firstLine = console.ReadLine() ?? string.Empty;
        if (TryResolveClipboardShortcut(firstLine, clipboardService, out var clipboardText))
            return FinalizeValue(clipboardText, options);

        var pastedRemainder = BufferedConsolePasteCapture.Drain(
            () => console.KeyAvailable,
            console.ReadKey);

        var combined = Combine(firstLine, pastedRemainder);
        return FinalizeValue(combined, options);
    }

    private static string ReadMultiline(
        ITextComposerConsole console,
        IClipboardService? clipboardService,
        TextComposerOptions options)
    {
        var firstLine = console.ReadLine() ?? string.Empty;
        if (TryResolveSpecialCommand(firstLine, clipboardService, options, out var specialValue))
            return specialValue;

        var pastedRemainder = BufferedConsolePasteCapture.Drain(
            () => console.KeyAvailable,
            console.ReadKey);
        if (!string.IsNullOrEmpty(pastedRemainder))
            return NormalizeMultiline(Combine(firstLine, pastedRemainder), options.DefaultValue);

        if (string.IsNullOrEmpty(firstLine))
            return NormalizeMultiline(options.DefaultValue ?? string.Empty, options.DefaultValue);

        var lines = new List<string> { firstLine };
        var blankStreak = 0;

        while (true)
        {
            var line = console.ReadLine() ?? string.Empty;

            if (TryResolveClipboardShortcut(line, clipboardService, out var clipboardText))
            {
                AppendClipboard(lines, clipboardText);
                blankStreak = 0;
                continue;
            }

            if (options.AllowClearCommand &&
                line.Trim().Equals(options.ClearCommand, StringComparison.OrdinalIgnoreCase))
            {
                return string.Empty;
            }

            if (string.IsNullOrEmpty(line))
            {
                blankStreak++;
                if (blankStreak >= 2)
                {
                    if (lines.Count > 0 && lines[^1].Length == 0)
                        lines.RemoveAt(lines.Count - 1);

                    return NormalizeMultiline(string.Join("\n", lines), options.DefaultValue);
                }

                lines.Add(string.Empty);
                continue;
            }

            blankStreak = 0;
            lines.Add(line);
        }
    }

    private static bool TryResolveSpecialCommand(
        string input,
        IClipboardService? clipboardService,
        TextComposerOptions options,
        out string value)
    {
        if (TryResolveClipboardShortcut(input, clipboardService, out value))
            return true;

        if (options.AllowClearCommand &&
            input.Trim().Equals(options.ClearCommand, StringComparison.OrdinalIgnoreCase))
        {
            value = string.Empty;
            return true;
        }

        value = string.Empty;
        return false;
    }

    private static bool TryResolveClipboardShortcut(
        string input,
        IClipboardService? clipboardService,
        out string value)
    {
        var trimmed = input.Trim();
        if (!trimmed.Equals("\\p", StringComparison.OrdinalIgnoreCase) &&
            !trimmed.Equals("/paste", StringComparison.OrdinalIgnoreCase) &&
            !trimmed.Equals("/вставить", StringComparison.OrdinalIgnoreCase))
        {
            value = string.Empty;
            return false;
        }

        var result = clipboardService?.TryReadText();
        value = result is { Success: true } clipboardResult ? clipboardResult.Text ?? string.Empty : string.Empty;
        return true;
    }

    private static string FinalizeValue(string raw, TextComposerOptions options)
    {
        var normalized = NormalizeLineEndings(raw);
        if (string.IsNullOrEmpty(normalized))
            normalized = options.DefaultValue ?? string.Empty;

        return options.PreserveNewlines
            ? normalized.TrimEnd('\n')
            : CollapseToSingleLine(normalized);
    }

    private static string NormalizeMultiline(string raw, string? defaultValue)
    {
        var normalized = NormalizeLineEndings(raw);
        if (string.IsNullOrEmpty(normalized))
            normalized = defaultValue ?? string.Empty;

        return normalized.TrimEnd('\n');
    }

    private static string NormalizeLineEndings(string value)
    {
        return value.Replace("\r\n", "\n").Replace('\r', '\n');
    }

    private static string Combine(string firstLine, string remainder)
    {
        if (string.IsNullOrEmpty(remainder))
            return firstLine;

        if (string.IsNullOrEmpty(firstLine))
            return remainder;

        return firstLine + "\n" + remainder;
    }

    private static void AppendClipboard(List<string> lines, string clipboardText)
    {
        var normalized = NormalizeLineEndings(clipboardText);
        if (string.IsNullOrEmpty(normalized))
            return;

        var parts = normalized.Split('\n');
        if (parts.Length == 0)
            return;

        if (lines.Count == 0)
        {
            lines.AddRange(parts);
            return;
        }

        lines[^1] += parts[0];
        for (var i = 1; i < parts.Length; i++)
            lines.Add(parts[i]);
    }
}
