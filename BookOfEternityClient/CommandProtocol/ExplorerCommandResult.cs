using System.Text.Json.Serialization;

namespace BookOfEternityClient.CommandProtocol;

public sealed class ExplorerCommandResult
{
    public string Command { get; init; } = string.Empty;
    public CommandExecutionState State { get; init; } = CommandExecutionState.Completed;
    public List<UiBlock> Blocks { get; init; } = [];
    public List<UiAction> Actions { get; init; } = [];
    public List<UiPrompt> Prompts { get; init; } = [];
    public List<UiNotification> Notifications { get; init; } = [];
    public UiPromptSession? InteractiveSession { get; init; }
}

[JsonConverter(typeof(JsonStringEnumConverter))]
public enum CommandExecutionState
{
    Completed,
    RequiresInput,
    Pending,
    Blocked,
    Failed
}
