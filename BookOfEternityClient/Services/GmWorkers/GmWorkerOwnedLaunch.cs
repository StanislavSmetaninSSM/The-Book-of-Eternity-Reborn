using System.Diagnostics;

namespace BookOfEternityClient.Services.GmWorkers;

public enum GmWorkerStopState { StoppedWithinScope, Uncertain }

public sealed record GmWorkerStopEvidence(
    string RunId, GmWorkerBackend Backend, string Guarantee, GmWorkerStopState State,
    string Reason, bool CleanupComplete, bool AuthorityRetained, int? RootExitCode);

internal interface IGmWorkerOwnedLauncher
{
    Task<GmWorkerOwnedLaunch> StartAsync(GmWorkerProcessHostLaunch host,
        GmWorkerBackendSelection selection, CancellationToken cancellationToken);
}

internal abstract class GmWorkerOwnedLaunch : IAsyncDisposable
{
    internal abstract GmWorkerExecutionIdentity Identity { get; }
    internal abstract int HostProcessId { get; }
    internal abstract int SupervisorProcessId { get; }
    internal abstract int? AdmittedHostProcessId { get; }
    internal abstract Task HostExited { get; }
    internal virtual bool RetainsAssignedWindowsJob => false;
    internal abstract Task WaitUntilReadyAsync(GmWorkerProcessHostLaunch host, CancellationToken cancellationToken);
    internal abstract Task<int> WaitForWorkerCompletionAsync(GmWorkerProcessHostLaunch host, CancellationToken cancellationToken);
    internal virtual Task WaitForDiagnosticDrainAsync(GmWorkerProcessHostLaunch host, CancellationToken cancellationToken) =>
        throw new InvalidOperationException("Native output progress is not an ownership boundary.");
    internal abstract Task<GmWorkerStopEvidence> StopAndObserveAsync();
    internal abstract Task<GmWorkerOwnedOutputs> SettleOutputsAsync();
    public abstract ValueTask DisposeAsync();
}

internal sealed record GmWorkerOwnedOutputs(string StandardOutput, string StandardError);

// Every failure after Start may have succeeded must carry its original owner.
internal sealed class GmWorkerOwnedLaunchException(string message, GmWorkerOwnedLaunch owner, Exception inner)
    : Exception(message, inner)
{
    internal GmWorkerOwnedLaunch Owner { get; } = owner;
}

internal sealed class GmWorkerNativeLineageLauncher(string? packageDirectory = null,
    GmWorkerNativePoolAdmission? admission = null) : IGmWorkerOwnedLauncher
{
    public Task<GmWorkerOwnedLaunch> StartAsync(GmWorkerProcessHostLaunch host,
        GmWorkerBackendSelection selection, CancellationToken cancellationToken)
    {
        if (selection.Backend != GmWorkerBackend.NativeLineage ||
            (selection.Capability != GmWorkerRequiredCapability.NeutralHost &&
             !(selection.Capability == GmWorkerRequiredCapability.SyntheticWorkerRelease && admission != null && host.HasAdmission(admission))))
            throw new PlatformNotSupportedException("Native launcher requires NeutralHost or matching explicit synthetic admission.");
        var executable = GmWorkerNativePackage.Validate(packageDirectory);
        return GmWorkerNativeLineageLaunch.StartOwnedAsync(host, executable, cancellationToken);
    }
}
