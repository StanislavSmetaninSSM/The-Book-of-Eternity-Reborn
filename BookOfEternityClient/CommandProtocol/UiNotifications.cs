using System.Text.Json.Serialization;

namespace BookOfEternityClient.CommandProtocol;

public sealed class UiNotification
{
    public UiNotificationSeverity Severity { get; init; } = UiNotificationSeverity.Info;
    public string Title { get; init; } = string.Empty;
    public string Message { get; init; } = string.Empty;
}

[JsonConverter(typeof(JsonStringEnumConverter))]
public enum UiNotificationSeverity
{
    Info,
    Success,
    Warning,
    Error
}
