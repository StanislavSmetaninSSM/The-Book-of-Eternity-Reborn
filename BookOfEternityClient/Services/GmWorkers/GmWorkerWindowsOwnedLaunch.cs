using System.Diagnostics;

namespace BookOfEternityClient.Services.GmWorkers;

internal sealed class GmWorkerWindowsOwnedLauncher(IGmWorkerProcessTreeFactory factory, Func<Task>? beforeAttach) : IGmWorkerOwnedLauncher
{
    public async Task<GmWorkerOwnedLaunch> StartAsync(GmWorkerProcessHostLaunch host,
        GmWorkerBackendSelection selection, CancellationToken cancellationToken)
    {
        if (!OperatingSystem.IsWindows() || selection.Backend != GmWorkerBackend.WindowsJob)
            throw new PlatformNotSupportedException("Windows owned launch requires the Windows Job backend.");
        var owner = new GmWorkerWindowsOwnedLaunch(host.StartInfo);
        try
        {
            owner.Start();
            if (beforeAttach != null) await beforeAttach().WaitAsync(cancellationToken);
            owner.Attach(factory);
            return owner;
        }
        catch (Exception ex) { throw new GmWorkerOwnedLaunchException("Windows host start retains its original owner.", owner, ex); }
    }
}

// Slice A keeps the established Windows cleanup/quarantine implementation. Only
// this sealed adapter can transfer original Process/Job ownership into that path.
internal sealed class GmWorkerWindowsOwnedLaunch : GmWorkerOwnedLaunch
{
    private readonly Process _process;
    private IGmWorkerProcessTree? _tree;
    private bool _started, _transferred;
    private readonly string _run = Guid.NewGuid().ToString("N");

    internal GmWorkerWindowsOwnedLaunch(ProcessStartInfo start) =>
        _process = new Process { StartInfo = start, EnableRaisingEvents = true };
    internal void Start()
    {
        if (!_process.Start()) throw new InvalidOperationException("Worker process did not start.");
        _started = true;
    }
    internal void Attach(IGmWorkerProcessTreeFactory factory) => _tree = factory.Attach(_process);
    internal override int HostProcessId => _process.Id;
    internal override int SupervisorProcessId => _process.Id;
    internal override Task WaitUntilReadyAsync(GmWorkerProcessHostLaunch host, CancellationToken token) => host.WaitUntilReadyAsync(_process, token);

    internal (Process Process, IGmWorkerProcessTree? Tree, bool Started) TransferToWindowsPool()
    {
        if (_transferred) throw new InvalidOperationException("Windows launch ownership already transferred.");
        _transferred = true;
        return (_process, _tree, _started);
    }

    internal override async Task<GmWorkerStopEvidence> StopAndObserveAsync()
    {
        if (_transferred) throw new InvalidOperationException("Windows pool owns this launch.");
        if (_started)
        {
            if (_tree != null) await _tree.StopAndWaitAsync();
            else await GmWorkerBridgePool.StopUnattachedProcessTreeAsync(_process);
        }
        return new(_run, GmWorkerBackend.WindowsJob, "windows-job", GmWorkerStopState.StoppedWithinScope,
            "windows-stop", true, false, _started ? _process.ExitCode : null);
    }
    public override async ValueTask DisposeAsync()
    {
        if (_transferred) return;
        await StopAndObserveAsync();
        if (_tree != null) await _tree.DisposeAsync();
        _process.Dispose();
    }
}
