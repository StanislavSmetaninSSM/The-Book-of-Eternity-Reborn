using System.Text.Json.Nodes;
using System.Text.Json.Serialization;

namespace BookOfEternityClient.CommandProtocol;

public sealed class UiAction
{
    public string Id { get; init; } = string.Empty;
    public string Label { get; init; } = string.Empty;
    public string Command { get; init; } = string.Empty;
    public UiActionStyle Style { get; init; } = UiActionStyle.Default;
    public bool RequiresConfirmation { get; init; }
    public JsonNode? Payload { get; init; }
}

[JsonConverter(typeof(JsonStringEnumConverter))]
public enum UiActionStyle
{
    Default,
    Primary,
    Secondary,
    Danger
}
