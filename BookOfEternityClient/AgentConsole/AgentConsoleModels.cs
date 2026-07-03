using System.Text.Json;
using System.Text.Json.Serialization;

namespace BookOfEternityClient.AgentConsole;

public static class AgentConsoleJson
{
    public static JsonSerializerOptions Options { get; } = CreateOptions();

    private static JsonSerializerOptions CreateOptions()
    {
        var options = new JsonSerializerOptions
        {
            WriteIndented = true,
            DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull,
            PropertyNamingPolicy = JsonNamingPolicy.CamelCase
        };
        options.Converters.Add(new JsonStringEnumConverter(JsonNamingPolicy.CamelCase));
        return options;
    }
}

public static class AgentConsoleLimits
{
    public const int MaxDiagnostics = 8;
}

public enum AgentConsoleMode
{
    Menu,
    TextPrompt,
    Confirmation,
    QteLive,
    Loading,
    Error,
    Exit
}

public enum AgentConsoleInputKind
{
    None,
    Key,
    Text,
    MenuSelection,
    Confirmation
}

public enum AgentConsoleDiagnosticSeverity
{
    Info,
    Warning,
    Error
}

public enum AgentConsoleEventKind
{
    ScreenRendered,
    PromptStarted,
    InputAccepted,
    InputRejected,
    StateChanged,
    Failure
}

public sealed record AgentConsoleAction
{
    [JsonPropertyName("id")]
    public required string Id { get; init; }

    [JsonPropertyName("label")]
    public required string Label { get; init; }

    [JsonPropertyName("description")]
    public string? Description { get; init; }

    [JsonPropertyName("shortcut")]
    public string? Shortcut { get; init; }

    [JsonPropertyName("inputValue")]
    public string? InputValue { get; init; }

    [JsonPropertyName("isEnabled")]
    public bool IsEnabled { get; init; } = true;

    [JsonPropertyName("isDefault")]
    public bool IsDefault { get; init; }
}

public sealed record AgentConsolePrompt
{
    [JsonPropertyName("promptId")]
    public required string PromptId { get; init; }

    [JsonPropertyName("text")]
    public required string Text { get; init; }

    [JsonPropertyName("inputKind")]
    public AgentConsoleInputKind InputKind { get; init; }

    [JsonPropertyName("defaultValue")]
    public string? DefaultValue { get; init; }

    [JsonPropertyName("choices")]
    public IReadOnlyList<string> Choices { get; init; } = [];
}

public sealed record AgentConsoleDiagnostic
{
    [JsonPropertyName("severity")]
    public AgentConsoleDiagnosticSeverity Severity { get; init; }

    [JsonPropertyName("message")]
    public required string Message { get; init; }

    [JsonPropertyName("code")]
    public string? Code { get; init; }

    [JsonPropertyName("detail")]
    public string? Detail { get; init; }

    [JsonPropertyName("exceptionType")]
    public string? ExceptionType { get; init; }
}

public sealed record AgentConsoleQteFrame
{
    [JsonPropertyName("qteId")]
    public string? QteId { get; init; }

    [JsonPropertyName("type")]
    public required string Type { get; init; }

    [JsonPropertyName("title")]
    public required string Title { get; init; }

    [JsonPropertyName("phase")]
    public required string Phase { get; init; }

    [JsonPropertyName("instructions")]
    public required string Instructions { get; init; }

    [JsonPropertyName("bodyText")]
    public required string BodyText { get; init; }

    [JsonPropertyName("awaitingInputKind")]
    public AgentConsoleInputKind AwaitingInputKind { get; init; } = AgentConsoleInputKind.Key;

    [JsonPropertyName("requiredInputs")]
    public IReadOnlyList<string> RequiredInputs { get; init; } = [];

    [JsonPropertyName("choices")]
    public IReadOnlyList<string> Choices { get; init; } = [];

    [JsonPropertyName("inputBuffer")]
    public IReadOnlyList<string> InputBuffer { get; init; } = [];

    [JsonPropertyName("remainingMs")]
    public int? RemainingMs { get; init; }

    [JsonPropertyName("timeoutMs")]
    public int? TimeoutMs { get; init; }

    [JsonPropertyName("progressValue")]
    public int? ProgressValue { get; init; }

    [JsonPropertyName("progressMax")]
    public int? ProgressMax { get; init; }

    [JsonPropertyName("markerValue")]
    public int? MarkerValue { get; init; }

    [JsonPropertyName("markerMin")]
    public int? MarkerMin { get; init; }

    [JsonPropertyName("markerMax")]
    public int? MarkerMax { get; init; }

    [JsonPropertyName("targetStart")]
    public int? TargetStart { get; init; }

    [JsonPropertyName("targetEnd")]
    public int? TargetEnd { get; init; }

    [JsonPropertyName("partialStart")]
    public int? PartialStart { get; init; }

    [JsonPropertyName("partialEnd")]
    public int? PartialEnd { get; init; }

    [JsonPropertyName("safeStart")]
    public int? SafeStart { get; init; }

    [JsonPropertyName("safeEnd")]
    public int? SafeEnd { get; init; }

    [JsonPropertyName("lastInput")]
    public string? LastInput { get; init; }

    [JsonPropertyName("lastInputAccepted")]
    public bool? LastInputAccepted { get; init; }

    [JsonPropertyName("feedback")]
    public IReadOnlyList<string> Feedback { get; init; } = [];
}

public sealed record AgentConsoleSnapshot
{
    private IReadOnlyList<AgentConsoleDiagnostic> _diagnostics = [];

    [JsonPropertyName("schemaVersion")]
    public int SchemaVersion { get; init; } = 1;

    [JsonPropertyName("screenId")]
    public required string ScreenId { get; init; }

    [JsonPropertyName("mode")]
    public AgentConsoleMode Mode { get; init; }

    [JsonPropertyName("title")]
    public required string Title { get; init; }

    [JsonPropertyName("plainText")]
    public required string PlainText { get; init; }

    [JsonPropertyName("ansiText")]
    public string? AnsiText { get; init; }

    [JsonPropertyName("awaitingInput")]
    public bool AwaitingInput { get; init; }

    [JsonPropertyName("inputKind")]
    public AgentConsoleInputKind InputKind { get; init; } = AgentConsoleInputKind.None;

    [JsonPropertyName("selectedIndex")]
    public int? SelectedIndex { get; init; }

    [JsonPropertyName("actions")]
    public IReadOnlyList<AgentConsoleAction> Actions { get; init; } = [];

    [JsonPropertyName("prompt")]
    public AgentConsolePrompt? Prompt { get; init; }

    [JsonPropertyName("qteFrame")]
    public AgentConsoleQteFrame? QteFrame { get; init; }

    [JsonPropertyName("renderedAtUtc")]
    public DateTimeOffset RenderedAtUtc { get; init; }

    [JsonPropertyName("updatedAtUtc")]
    public DateTimeOffset UpdatedAtUtc { get; init; }

    [JsonPropertyName("diagnostics")]
    public IReadOnlyList<AgentConsoleDiagnostic> Diagnostics
    {
        get => _diagnostics;
        init => _diagnostics = value?.Take(AgentConsoleLimits.MaxDiagnostics).ToArray() ?? [];
    }
}

public sealed record AgentConsoleEvent
{
    [JsonPropertyName("sequenceId")]
    public long SequenceId { get; init; }

    [JsonPropertyName("kind")]
    public AgentConsoleEventKind Kind { get; init; }

    [JsonPropertyName("occurredAtUtc")]
    public DateTimeOffset OccurredAtUtc { get; init; }

    [JsonPropertyName("screenId")]
    public string? ScreenId { get; init; }

    [JsonPropertyName("inputKind")]
    public AgentConsoleInputKind? InputKind { get; init; }

    [JsonPropertyName("message")]
    public string? Message { get; init; }

    [JsonPropertyName("diagnostic")]
    public AgentConsoleDiagnostic? Diagnostic { get; init; }
}

public sealed record AgentConsoleObservationState
{
    [JsonPropertyName("currentSnapshot")]
    public AgentConsoleSnapshot? CurrentSnapshot { get; init; }

    [JsonPropertyName("events")]
    public IReadOnlyList<AgentConsoleEvent> Events { get; init; } = [];

    [JsonPropertyName("observedAtUtc")]
    public DateTimeOffset ObservedAtUtc { get; init; }
}
