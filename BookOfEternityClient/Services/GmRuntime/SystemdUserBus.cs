namespace BookOfEternityClient.Services.GmRuntime;

internal static class SystemdUserBus
{
    internal static Task AttachOriginalAsync(SystemdControlledFixture fixture, NativeHeldTerminalPidfd held, CancellationToken token)
        => Task.FromException(new NotImplementedException("S1 original transient-scope attachment missing after held native creation."));
}
