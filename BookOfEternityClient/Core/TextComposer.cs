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

internal sealed class TextComposerInputClosedException(string draft) : OperationCanceledException("Ввод закрыт; черновик не отправлен.")
{
    public string Draft { get; } = draft;
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
        var draft = options.DefaultValue ?? string.Empty;
        while (true)
        {
            var line = ReadLine(console, draft);
            if (IsClipboardShortcut(line))
            {
                if (ReadClipboard(console, clipboardService, out var text)) draft = text;
                console.Markup("[dim]Enter = подтвердить черновик; новый текст = заменить: [/]");
                continue;
            }
            if (options.AllowClearCommand && line.Trim().Equals(options.ClearCommand, StringComparison.OrdinalIgnoreCase))
                return string.Empty;
            var pastedRemainder = BufferedConsolePasteCapture.Drain(() => console.KeyAvailable, console.ReadKey);
            var combined = Combine(line, pastedRemainder);
            return FinalizeValue(string.IsNullOrEmpty(combined) ? draft : combined, options);
        }
    }

    private static string ReadMultiline(
        ITextComposerConsole console,
        IClipboardService? clipboardService,
        TextComposerOptions options)
    {
        var lines = new List<string>();
        var started = false;
        var blankStreak = 0;
        while (true)
        {
            var draft = lines.Count == 0 ? options.DefaultValue ?? string.Empty : string.Join("\n", lines);
            var line = ReadLine(console, draft);
            if (IsClipboardShortcut(line))
            {
                if (ReadClipboard(console, clipboardService, out var text))
                {
                    AppendClipboard(lines, text);
                    started = true;
                }
                blankStreak = 0;
                continue;
            }

            if (options.AllowClearCommand &&
                line.Trim().Equals(options.ClearCommand, StringComparison.OrdinalIgnoreCase))
            {
                return string.Empty;
            }

            if (!started)
            {
                var pastedRemainder = BufferedConsolePasteCapture.Drain(() => console.KeyAvailable, console.ReadKey);
                if (!string.IsNullOrEmpty(pastedRemainder))
                    return NormalizeMultiline(Combine(line, pastedRemainder), options.DefaultValue);
                if (string.IsNullOrEmpty(line))
                    return NormalizeMultiline(options.DefaultValue ?? string.Empty, options.DefaultValue);
                started = true;
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

    private static string ReadLine(ITextComposerConsole console, string draft) =>
        console.ReadLine() ?? throw new TextComposerInputClosedException(draft);

    private static bool IsClipboardShortcut(string input)
    {
        var trimmed = input.Trim();
        return trimmed.Equals("\\p", StringComparison.OrdinalIgnoreCase) ||
            trimmed.Equals("/paste", StringComparison.OrdinalIgnoreCase) ||
            trimmed.Equals("/вставить", StringComparison.OrdinalIgnoreCase);
    }

    private static bool ReadClipboard(ITextComposerConsole console, IClipboardService? clipboardService, out string value)
    {
        var result = clipboardService?.TryReadText() ?? ClipboardReadResult.Refused(ClipboardReadOutcome.Unavailable);
        if (!result.Success)
        {
            console.MarkupLine($"[yellow]{Markup.Escape(result.Error ?? "Не удалось прочитать буфер обмена.")}[/]");
            value = string.Empty;
            return false;
        }
        value = result.Text ?? string.Empty;
        // Preview is bounded and cannot interpret markup or terminal control characters.
        var preview = new string(value.Take(512).Select(c => char.IsControl(c) && c != '\n' && c != '\t' ? '�' : c).ToArray());
        console.MarkupLine($"[dim]Черновик из буфера (ещё не отправлен):[/] {Markup.Escape(preview)}");
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
