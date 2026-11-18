namespace BookOfEternityClient.Services.GmRuntime;

/// <summary>Linux local daemon exclusion only; never canonical publication authority.</summary>
public sealed class LinuxDaemonInstanceGuard : IDisposable
{
    public string OwnerId => throw new InvalidOperationException("Daemon guard not implemented.");
    public string SessionPath => throw new InvalidOperationException("Daemon guard not implemented.");
    public static LinuxDaemonInstanceGuard Acquire(string basePath, string? priorStatusJson)
        => throw new InvalidOperationException("Daemon guard not implemented.");
    public void Validate() => throw new InvalidOperationException("Daemon guard not implemented.");
    public void Dispose() { }
}
