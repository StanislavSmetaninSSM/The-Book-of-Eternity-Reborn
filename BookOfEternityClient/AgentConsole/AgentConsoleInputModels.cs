using System.Text.Json.Serialization;

namespace BookOfEternityClient.AgentConsole;

public enum AgentConsoleInputRejectionCode
{
    None,
    InvalidRequest,
    InputClosed,
    QueueFull,
    NoSnapshot,
    NotAwaitingInput,
    ScreenMismatch,
    InputKindMismatch,
    ActionMissing,
    ActionDisabled,
    UnsupportedActionShortcut,
    UnsupportedActionResolution
}

public enum AgentConsoleInputReadFailureReason
{
    Timeout,
    Cancelled,
    Shutdown,
    InputKindMismatch
}

public sealed record AgentConsoleActionRequest
{
    [JsonPropertyName("actionId")]
    public required string ActionId { get; init; }

    [JsonPropertyName("screenId")]
    public string? ScreenId { get; init; }

    [JsonPropertyName("inputKind")]
    public AgentConsoleInputKind? InputKind { get; init; }
}

public sealed record AgentConsoleInputResult
{
    [JsonPropertyName("accepted")]
    public bool Accepted { get; init; }

    [JsonPropertyName("rejectionCode")]
    public AgentConsoleInputRejectionCode RejectionCode { get; init; }

    [JsonPropertyName("message")]
    public required string Message { get; init; }

    [JsonPropertyName("event")]
    public required AgentConsoleEvent Event { get; init; }
}
