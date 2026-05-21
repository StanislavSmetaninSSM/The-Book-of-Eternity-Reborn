namespace BookOfEternityClient.CommandProtocol;

public sealed class UiPromptSession
{
    public string SessionId { get; init; } = string.Empty;
    public string SubmitEndpoint { get; init; } = string.Empty;
    public string CancelEndpoint { get; init; } = string.Empty;
    public bool RequiresLocalUiLock { get; init; }
    public string OwnerId { get; init; } = string.Empty;
    public DateTime ExpiresAtUtc { get; init; }
}
