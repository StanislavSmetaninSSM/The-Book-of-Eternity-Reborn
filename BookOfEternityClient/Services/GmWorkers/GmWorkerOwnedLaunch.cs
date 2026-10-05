using System.Diagnostics;

namespace BookOfEternityClient.Services.GmWorkers;

internal enum GmWorkerStopState { StoppedWithinScope, Uncertain }

internal sealed record GmWorkerStopEvidence(
    string RunId, GmWorkerBackend Backend, string Guarantee, GmWorkerStopState State,
    string Reason, bool CleanupComplete, bool AuthorityRetained, int? RootExitCode);

internal interface IGmWorkerOwnedLauncher
{
    Task<GmWorkerOwnedLaunch> StartAsync(GmWorkerProcessHostLaunch host,
        GmWorkerBackendSelection selection, CancellationToken cancellationToken);
}

internal abstract class GmWorkerOwnedLaunch : IAsyncDisposable
{
    internal abstract int HostProcessId { get; }
    internal abstract int SupervisorProcessId { get; }
    internal abstract Task WaitUntilReadyAsync(GmWorkerProcessHostLaunch host, CancellationToken cancellationToken);
    internal abstract Task<GmWorkerStopEvidence> StopAndObserveAsync();
    public abstract ValueTask DisposeAsync();
}

// Every failure after Start may have succeeded must carry its original owner.
internal sealed class GmWorkerOwnedLaunchException(string message, GmWorkerOwnedLaunch owner, Exception inner)
    : Exception(message, inner)
{
    internal GmWorkerOwnedLaunch Owner { get; } = owner;
}

internal sealed class GmWorkerNativeLineageLauncher(string? packageDirectory = null) : IGmWorkerOwnedLauncher
{
    public Task<GmWorkerOwnedLaunch> StartAsync(GmWorkerProcessHostLaunch host,
        GmWorkerBackendSelection selection, CancellationToken cancellationToken)
    {
        if (selection.Backend != GmWorkerBackend.NativeLineage || selection.Capability != GmWorkerRequiredCapability.NeutralHost)
            throw new PlatformNotSupportedException("Native launcher admits only NeutralHost.");
        var executable = GmWorkerNativePackage.Validate(packageDirectory);
        return GmWorkerNativeLineageLaunch.StartOwnedAsync(host, executable, cancellationToken);
    }
}
