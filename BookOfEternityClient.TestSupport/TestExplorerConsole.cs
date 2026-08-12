using System.Collections;
using System.Reflection;
using BookOfEternityClient.UI;
using Spectre.Console;
using Spectre.Console.Rendering;

namespace BookOfEternityClient.Tests;

internal sealed class TestExplorerConsole : IExplorerConsole
{
    private readonly Dictionary<string, int> _selectionPromptVisits = new(StringComparer.Ordinal);
    private readonly List<(string promptFragment, Queue<string> values)> _selectionOverrides = new();
    private readonly List<(string promptFragment, Queue<string> values)> _askOverrides = new();
    private readonly List<(string promptFragment, Queue<bool> values)> _confirmOverrides = new();
    private readonly Queue<string> _anySelectionResponses = new();
    private readonly Queue<string> _anyAskResponses = new();
    private readonly Queue<bool> _anyConfirmResponses = new();
    private readonly Queue<string?> _readLineResponses = new();
    private readonly Queue<ConsoleKeyInfo> _readKeys = new();

    public List<IRenderable> Rendered { get; } = new();
    public List<string> MarkupLines { get; } = new();
    public List<string> AskPrompts { get; } = new();
    public List<string> ConfirmPrompts { get; } = new();
    public List<string> SelectionTitles { get; } = new();
    public List<(string Title, IReadOnlyList<string> Choices)> SelectionChoicesHistory { get; } = new();
    public int ClearCalls { get; private set; }
    public int ReadKeyCalls { get; private set; }
    public Action? ReadKeyCallback { get; set; }

    public void Clear() => ClearCalls++;

    public void Write(IRenderable content) => Rendered.Add(content);

    public void WriteLine() => MarkupLines.Add(string.Empty);

    public void Markup(string markup) => MarkupLines.Add(markup);

    public void MarkupLine(string markup) => MarkupLines.Add(markup);

    public string Ask(string prompt, string defaultValue = "")
    {
        AskPrompts.Add(prompt);
        if (_anyAskResponses.Count > 0)
            return _anyAskResponses.Dequeue();

        if (TryDequeue(_askOverrides, prompt, out var value))
            return value;

        return defaultValue;
    }

    public bool Confirm(string prompt, bool defaultValue = false)
    {
        ConfirmPrompts.Add(prompt);
        if (_anyConfirmResponses.Count > 0)
            return _anyConfirmResponses.Dequeue();

        if (TryDequeue(_confirmOverrides, prompt, out var value))
            return value;

        return defaultValue;
    }

    public T Prompt<T>(IPrompt<T> prompt)
    {
        if (prompt is SelectionPrompt<string> selection)
            return (T)(object)ResolveSelection(selection);

        if (prompt is ConfirmationPrompt)
        {
            if (_anyConfirmResponses.Count > 0)
                return (T)(object)_anyConfirmResponses.Dequeue();
            return (T)(object)false;
        }

        if (prompt is TextPrompt<string>)
            return (T)(object)string.Empty;

        if (prompt is TextPrompt<int>)
            return (T)(object)1;

        throw new NotSupportedException($"Unsupported prompt type in test console: {prompt.GetType().FullName}");
    }

    public string? ReadLine()
    {
        if (_readLineResponses.Count > 0)
            return _readLineResponses.Dequeue();

        return string.Empty;
    }

    public bool KeyAvailable => _readKeys.Count > 0;

    public ConsoleKeyInfo ReadKey()
    {
        ReadKeyCalls++;
        ReadKeyCallback?.Invoke();
        if (_readKeys.Count > 0)
            return _readKeys.Dequeue();

        return new ConsoleKeyInfo('\r', ConsoleKey.Enter, false, false, false);
    }

    public void QueueSelection(string titleFragment, params string[] responses)
    {
        _selectionOverrides.Add((titleFragment, new Queue<string>(responses)));
    }

    public void QueueAnySelection(params string[] responses)
    {
        foreach (var response in responses)
            _anySelectionResponses.Enqueue(response);
    }

    public void QueueAskResponse(string promptFragment, params string[] responses)
    {
        _askOverrides.Add((promptFragment, new Queue<string>(responses)));
    }

    public void QueueAnyAskResponse(params string[] responses)
    {
        foreach (var response in responses)
            _anyAskResponses.Enqueue(response);
    }

    public void QueueConfirmResponse(string promptFragment, params bool[] responses)
    {
        _confirmOverrides.Add((promptFragment, new Queue<bool>(responses)));
    }

    public void QueueAnyConfirmResponse(params bool[] responses)
    {
        foreach (var response in responses)
            _anyConfirmResponses.Enqueue(response);
    }

    public void QueueReadLineResponses(params string?[] responses)
    {
        foreach (var response in responses)
            _readLineResponses.Enqueue(response);
    }

    public void QueueReadKeys(params ConsoleKeyInfo[] responses)
    {
        foreach (var response in responses)
            _readKeys.Enqueue(response);
    }

    private string ResolveSelection(SelectionPrompt<string> selection)
    {
        var title = ReadPromptTitle(selection);
        SelectionTitles.Add(title);
        var choices = ReadChoices(selection);
        SelectionChoicesHistory.Add((title, choices.ToArray()));
        if (choices.Count == 0)
            throw new InvalidOperationException($"Selection prompt '{title}' has no choices.");

        if (TryDequeue(_selectionOverrides, title, out var scriptedChoice))
            return scriptedChoice;

        if (_anySelectionResponses.Count > 0)
            return _anySelectionResponses.Dequeue();

        if (title.Contains("Действие", StringComparison.OrdinalIgnoreCase))
            return choices.FirstOrDefault(IsBackChoice) ?? choices[0];

        var visitKey = $"{title}::{choices.Count}";
        _selectionPromptVisits.TryGetValue(visitKey, out var visits);
        _selectionPromptVisits[visitKey] = visits + 1;

        if (visits > 0)
            return choices.FirstOrDefault(IsBackChoice) ?? choices[0];

        return choices.FirstOrDefault(choice => !IsBackChoice(choice)) ?? choices[0];
    }

    private static string ReadPromptTitle(SelectionPrompt<string> selection)
    {
        var titleProperty = typeof(SelectionPrompt<string>).GetProperty("Title", BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic);
        return titleProperty?.GetValue(selection)?.ToString() ?? string.Empty;
    }

    private static List<string> ReadChoices(SelectionPrompt<string> selection)
    {
        var treeField = typeof(SelectionPrompt<string>).GetField("_tree", BindingFlags.Instance | BindingFlags.NonPublic);
        var tree = treeField?.GetValue(selection);
        if (tree == null)
            return new List<string>();

        var result = new List<string>();
        var rootsField = tree.GetType().GetField("_roots", BindingFlags.Instance | BindingFlags.NonPublic);
        var roots = rootsField?.GetValue(tree) as IEnumerable;
        if (roots == null)
            return result;

        foreach (var root in roots)
            AppendChoiceTree(root, result);

        return result;
    }

    private static bool IsBackChoice(string choice)
    {
        return choice.Contains("←", StringComparison.Ordinal) ||
               choice.Contains("Назад", StringComparison.OrdinalIgnoreCase);
    }

    private static void AppendChoiceTree(object? node, List<string> result)
    {
        if (node == null)
            return;

        var nodeType = node.GetType();
        var dataProperty = nodeType.GetProperty("Data", BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic);
        var childrenProperty = nodeType.GetProperty("Children", BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic);

        var data = dataProperty?.GetValue(node)?.ToString();
        if (!string.IsNullOrEmpty(data))
            result.Add(data);

        var children = childrenProperty?.GetValue(node) as IEnumerable;
        if (children == null)
            return;

        foreach (var child in children)
            AppendChoiceTree(child, result);
    }

    private static bool TryDequeue<T>(List<(string promptFragment, Queue<T> values)> overrides, string prompt, out T value)
    {
        foreach (var entry in overrides)
        {
            if (!prompt.Contains(entry.promptFragment, StringComparison.OrdinalIgnoreCase) || entry.values.Count == 0)
                continue;

            value = entry.values.Dequeue();
            return true;
        }

        value = default!;
        return false;
    }
}
