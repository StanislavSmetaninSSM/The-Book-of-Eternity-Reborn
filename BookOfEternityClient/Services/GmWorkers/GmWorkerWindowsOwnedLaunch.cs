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

internal sealed class GmWorkerWindowsOwnedLaunch : GmWorkerOwnedLaunch
{
    private readonly Process _process;
    private IGmWorkerProcessTree? _tree;
    private bool _started, _disposed;
    private readonly string _run = Guid.NewGuid().ToString("N");
    private Task? _hostExit;
    private Task<string>? _output, _error;
    private GmWorkerStopEvidence? _stop;
    private GmWorkerOwnedOutputs? _outputs;
    private readonly SemaphoreSlim _stopGate = new(1);

    internal GmWorkerWindowsOwnedLaunch(ProcessStartInfo start) =>
        _process = new Process { StartInfo = start, EnableRaisingEvents = true };
    internal void Start()
    {
        if (!_process.Start()) throw new InvalidOperationException("Worker process did not start.");
        _started = true;
        _hostExit = _process.WaitForExitAsync();
        _output = GmWorkerBridgePool.CaptureProcessOutputAsync(_process.StandardOutput);
        _error = GmWorkerBridgePool.CaptureProcessOutputAsync(_process.StandardError);
    }
    internal void Attach(IGmWorkerProcessTreeFactory factory) => _tree = factory.Attach(_process);
    internal override GmWorkerExecutionIdentity Identity => new(_run, GmWorkerBackend.WindowsJob, "windows-job");
    internal override int HostProcessId => _process.Id;
    internal override int SupervisorProcessId => _process.Id;
    internal override int? AdmittedHostProcessId => _started ? _process.Id : null;
    internal override Task HostExited => _hostExit ?? Task.CompletedTask;
    internal override bool RetainsAssignedWindowsJob => !_disposed && _tree is WindowsJobProcessTree { HasRetainedAuthority: true };
    internal override Task WaitUntilReadyAsync(GmWorkerProcessHostLaunch host, CancellationToken token) => host.WaitUntilReadyAsync(_process, token);
    internal override Task<int> WaitForWorkerCompletionAsync(GmWorkerProcessHostLaunch host, CancellationToken token) => host.WaitForWorkerCompletionAsync(_process, token);
    internal override Task WaitForDiagnosticDrainAsync(GmWorkerProcessHostLaunch host, CancellationToken token) => host.WaitForOutputDrainAsync(_process, token);

    internal override async Task<GmWorkerStopEvidence> StopAndObserveAsync()
    {
        await _stopGate.WaitAsync();
        try
        {
            if (_stop != null) return _stop;
            if (_started)
            {
                if (_tree != null) await _tree.StopAndWaitAsync();
                else await GmWorkerBridgePool.StopUnattachedProcessTreeAsync(_process);
            }
            return _stop = new(_run, GmWorkerBackend.WindowsJob, "windows-job", GmWorkerStopState.StoppedWithinScope,
                "windows-stop", true, false, _started ? _process.ExitCode : null);
        }
        finally { _stopGate.Release(); }
    }

    internal override async Task<GmWorkerOwnedOutputs> SettleOutputsAsync()
    {
        if (_stop == null) throw new InvalidOperationException("Windows outputs require confirmed original Job stop.");
        if (_outputs != null) return _outputs;
        if (!_started) return _outputs = new("", "");
        await Task.WhenAll(_output!, _error!).WaitAsync(TimeSpan.FromSeconds(5));
        return _outputs = new(await _output!, await _error!);
    }

    public override async ValueTask DisposeAsync()
    {
        if (_disposed) return;
        if (_stop == null || _outputs == null) throw new InvalidOperationException("Original Windows owner retains unsettled execution.");
        if (_tree != null) { await _tree.DisposeAsync(); _tree = null; }
        _process.Dispose();
        _disposed = true;
    }
}
