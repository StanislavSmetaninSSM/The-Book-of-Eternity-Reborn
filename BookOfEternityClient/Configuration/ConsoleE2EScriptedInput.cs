using System.Text;
using System.Text.Json;
using System.Text.Json.Serialization;
using BookOfEternityClient.Core;

namespace BookOfEternityClient.Configuration;

public sealed class ConsoleE2EScriptInputException : InvalidOperationException
{
    public ConsoleE2EScriptInputException(string message, string scriptPath, int nextStepIndex)
        : base(message)
    {
        ScriptPath = scriptPath;
        NextStepIndex = nextStepIndex;
    }

    public string ScriptPath { get; }

    public int NextStepIndex { get; }
}

public sealed class ConsoleE2EScriptedInputSource : IConsoleInputSource
{
    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        PropertyNameCaseInsensitive = true,
        Converters = { new JsonStringEnumConverter(JsonNamingPolicy.CamelCase) }
    };

    private readonly IReadOnlyList<ConsoleE2EScriptStep> _steps;
    private readonly string _scriptPath;
    private readonly string _artifactRoot;
    private readonly ConsoleE2EObservationArtifactWriter _observationWriter;
    private int _nextStepIndex;
    private bool _suppressNextKeyAvailable;

    private ConsoleE2EScriptedInputSource(
        IReadOnlyList<ConsoleE2EScriptStep> steps,
        string scriptPath,
        string artifactRoot,
        string runId)
    {
        _steps = steps;
        _scriptPath = scriptPath;
        _artifactRoot = artifactRoot;
        RunId = runId;
        _observationWriter = new ConsoleE2EObservationArtifactWriter(artifactRoot, runId);
    }

    public bool IsScripted => true;

    public string ArtifactRoot => _artifactRoot;

    public string RunId { get; }

    public int NextStepIndex => _nextStepIndex;

    public bool KeyAvailable
    {
        get
        {
            if (_suppressNextKeyAvailable)
            {
                _suppressNextKeyAvailable = false;
                return false;
            }

            return _nextStepIndex < _steps.Count &&
                   _steps[_nextStepIndex].Kind == ConsoleE2EScriptStepKind.Key;
        }
    }

    public static ConsoleE2EScriptedInputSource FromFile(string scriptPath, string? artifactRoot)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(scriptPath);

        var fullScriptPath = Path.GetFullPath(scriptPath);
        var root = string.IsNullOrWhiteSpace(artifactRoot)
            ? Path.Combine(Path.GetDirectoryName(fullScriptPath) ?? Environment.CurrentDirectory, "artifacts")
            : artifactRoot;
        var fullRoot = Path.GetFullPath(root);
        Directory.CreateDirectory(fullRoot);

        if (!File.Exists(fullScriptPath))
        {
            var message = $"Console E2E script was not found: {fullScriptPath}";
            WriteFailureArtifact(fullRoot, fullScriptPath, nextStepIndex: 0, "script-load", message);
            throw new ConsoleE2EScriptInputException(
                message,
                fullScriptPath,
                nextStepIndex: 0);
        }

        ConsoleE2EScriptDocument? document;
        try
        {
            document = JsonSerializer.Deserialize<ConsoleE2EScriptDocument>(File.ReadAllText(fullScriptPath), JsonOptions);
        }
        catch (JsonException ex)
        {
            var message = $"Invalid console E2E script JSON: {ex.Message}";
            WriteFailureArtifact(fullRoot, fullScriptPath, nextStepIndex: 0, "script-load", message);
            throw new ConsoleE2EScriptInputException(
                message,
                fullScriptPath,
                nextStepIndex: 0);
        }

        var steps = document?.Steps ?? [];
        return new ConsoleE2EScriptedInputSource(
            steps.Select((step, index) => NormalizeStep(step, index, fullScriptPath, fullRoot)).ToArray(),
            fullScriptPath,
            fullRoot,
            BuildRunId(fullRoot));
    }

    public ConsoleKeyInfo ReadKey(bool intercept = true)
    {
        if (_nextStepIndex >= _steps.Count)
            throw Fail("ReadKey", "The console E2E script is exhausted before the client requested a key.");

        var step = _steps[_nextStepIndex];
        if (step.Kind != ConsoleE2EScriptStepKind.Key)
        {
            throw Fail(
                "ReadKey",
                $"Expected a key step, but next script step is '{step.Kind.ToString().ToLowerInvariant()}'.");
        }

        _nextStepIndex++;
        return ToConsoleKeyInfo(step.Key!, step.Text);
    }

    public string? ReadLine()
    {
        if (_nextStepIndex >= _steps.Count)
            throw Fail("ReadLine", "The console E2E script is exhausted before the client requested text input.");

        var step = _steps[_nextStepIndex];
        if (step.Kind == ConsoleE2EScriptStepKind.Text)
        {
            _nextStepIndex++;
            _suppressNextKeyAvailable = true;
            return step.Text ?? string.Empty;
        }

        if (step.Kind != ConsoleE2EScriptStepKind.Key)
        {
            throw Fail(
                "ReadLine",
                $"Expected a text or printable key step, but next script step is '{step.Kind.ToString().ToLowerInvariant()}'.");
        }

        var builder = new StringBuilder();
        while (_nextStepIndex < _steps.Count && _steps[_nextStepIndex].Kind == ConsoleE2EScriptStepKind.Key)
        {
            var keyInfo = ToConsoleKeyInfo(_steps[_nextStepIndex].Key!, _steps[_nextStepIndex].Text);
            _nextStepIndex++;
            if (keyInfo.Key == ConsoleKey.Enter)
            {
                _suppressNextKeyAvailable = true;
                return builder.ToString();
            }

            if (keyInfo.KeyChar != '\0')
                builder.Append(keyInfo.KeyChar);
        }

        throw Fail("ReadLine", "Printable key input for ReadLine ended before an Enter step.");
    }

    public void AssertCompleted()
    {
        if (_nextStepIndex == _steps.Count)
            return;

        throw Fail(
            "AssertCompleted",
            $"The console E2E script finished with {_steps.Count - _nextStepIndex} unconsumed step(s).");
    }

    public ConsoleE2EObservationArtifact WriteObservation(
        ConsoleE2EInputMode inputMode,
        string screenTitle,
        string playerFacingText,
        IReadOnlyList<string> options,
        string? selectedOption,
        string slug,
        string? logPath = null)
    {
        var snapshot = new ConsoleE2EObservationSnapshot(
            RunId: RunId,
            StepIndex: _nextStepIndex,
            CapturedAtUtc: DateTimeOffset.UtcNow,
            InputMode: inputMode,
            ScreenTitle: screenTitle,
            PlayerFacingText: playerFacingText,
            Options: options,
            SelectedOption: selectedOption,
            ArtifactRoot: _artifactRoot,
            LogPath: logPath);

        return _observationWriter.WriteSnapshot(snapshot, slug);
    }

    public ConsoleE2EObservationArtifact WriteExceptionObservation(
        string screenTitle,
        string playerFacingText,
        Exception exception,
        string slug)
        => _observationWriter.WriteExceptionSnapshot(_nextStepIndex, screenTitle, playerFacingText, exception, slug);

    private static ConsoleE2EScriptStep NormalizeStep(
        ConsoleE2EScriptStep step,
        int index,
        string scriptPath,
        string artifactRoot)
    {
        if (step.Kind == ConsoleE2EScriptStepKind.Key)
        {
            if (string.IsNullOrWhiteSpace(step.Key))
                throw BuildStaticFailure(scriptPath, artifactRoot, index, "Script key step is missing the 'key' value.");

            try
            {
                _ = ToConsoleKey(step.Key, out _);
            }
            catch (ArgumentException ex)
            {
                throw BuildStaticFailure(scriptPath, artifactRoot, index, ex.Message);
            }

            return step;
        }

        if (step.Kind == ConsoleE2EScriptStepKind.Text)
        {
            return step with { Text = step.Text ?? string.Empty };
        }

        throw BuildStaticFailure(scriptPath, artifactRoot, index, $"Unsupported script step kind '{step.Kind}'.");
    }

    private static ConsoleE2EScriptInputException BuildStaticFailure(
        string scriptPath,
        string artifactRoot,
        int nextStepIndex,
        string message)
    {
        var fullScriptPath = Path.GetFullPath(scriptPath);
        var fullArtifactRoot = Path.GetFullPath(artifactRoot);
        Directory.CreateDirectory(fullArtifactRoot);
        WriteFailureArtifact(fullArtifactRoot, fullScriptPath, nextStepIndex, "script-load", message);
        return new ConsoleE2EScriptInputException(message, fullScriptPath, nextStepIndex);
    }

    private ConsoleE2EScriptInputException Fail(string operation, string message)
    {
        WriteFailureArtifact(_artifactRoot, _scriptPath, _nextStepIndex, operation, message);
        return new ConsoleE2EScriptInputException(message, _scriptPath, _nextStepIndex);
    }

    private static void WriteFailureArtifact(
        string artifactRoot,
        string scriptPath,
        int nextStepIndex,
        string operation,
        string message)
    {
        Directory.CreateDirectory(artifactRoot);
        var failurePath = Path.Combine(artifactRoot, "failure.txt");
        File.WriteAllText(
            failurePath,
            $"Console E2E scripted input failure{Environment.NewLine}" +
            $"scriptPath: {scriptPath}{Environment.NewLine}" +
            $"operation: {operation}{Environment.NewLine}" +
            $"nextStepIndex: {nextStepIndex}{Environment.NewLine}" +
            $"message: {message}{Environment.NewLine}",
            Encoding.UTF8);
    }

    private static string BuildRunId(string artifactRoot)
    {
        var trimmed = artifactRoot.TrimEnd(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar);
        var candidate = Path.GetFileName(trimmed);
        if (string.Equals(candidate, "artifacts", StringComparison.OrdinalIgnoreCase))
            candidate = Directory.GetParent(trimmed)?.Name;

        return string.IsNullOrWhiteSpace(candidate)
            ? "run-console-e2e"
            : candidate;
    }

    private static ConsoleKeyInfo ToConsoleKeyInfo(string keyName, string? text)
    {
        var key = ToConsoleKey(keyName, out var keyChar);
        if (!string.IsNullOrEmpty(text) && text.Length == 1)
            keyChar = text[0];

        return new ConsoleKeyInfo(keyChar, key, shift: false, alt: false, control: false);
    }

    private static ConsoleKey ToConsoleKey(string keyName, out char keyChar)
    {
        keyChar = '\0';
        var normalized = keyName.Trim();
        if (normalized.Length == 1)
        {
            var ch = normalized[0];
            if (char.IsLetter(ch))
            {
                keyChar = char.ToLowerInvariant(ch);
                return Enum.Parse<ConsoleKey>(char.ToUpperInvariant(ch).ToString());
            }

            if (char.IsDigit(ch) && ch != '0')
            {
                keyChar = ch;
                return Enum.Parse<ConsoleKey>("D" + ch);
            }
        }

        if (normalized.Equals("space", StringComparison.OrdinalIgnoreCase))
        {
            keyChar = ' ';
            return ConsoleKey.Spacebar;
        }

        return normalized.ToLowerInvariant() switch
        {
            "up" or "uparrow" => ConsoleKey.UpArrow,
            "down" or "downarrow" => ConsoleKey.DownArrow,
            "left" or "leftarrow" => ConsoleKey.LeftArrow,
            "right" or "rightarrow" => ConsoleKey.RightArrow,
            "enter" or "return" => ConsoleKey.Enter,
            "escape" or "esc" => ConsoleKey.Escape,
            "w" => ConsoleKey.W,
            "s" => ConsoleKey.S,
            _ when Enum.TryParse<ConsoleKey>(normalized, ignoreCase: true, out var parsed) => parsed,
            _ => throw new ArgumentException($"Unsupported console E2E key '{keyName}'.", nameof(keyName))
        };
    }

    private sealed record ConsoleE2EScriptDocument(IReadOnlyList<ConsoleE2EScriptStep>? Steps);

    private sealed record ConsoleE2EScriptStep(
        ConsoleE2EScriptStepKind Kind,
        string? Key = null,
        string? Text = null);

    private enum ConsoleE2EScriptStepKind
    {
        Key,
        Text
    }
}
