using BookOfEternityClient.Services.GmRuntime;

namespace BookOfEternityGMBridge;

internal sealed partial class BridgeHost
{
    private readonly WindowsStartupObservationProbe _startupObservation = new();
    private bool _startupObservationFailed;
    private sealed class StartupObservationUnavailableException(Exception inner)
        : IOException("Original startup observation is unavailable or retains unfinished cleanup.", inner);

    // Controlled process injection does not enable the Windows production path.
    internal BridgeHost(string sessionPath, string pipeName, WindowsStartupObservationProbe probe)
        : this(sessionPath, pipeName) => _startupObservation = probe;

    private async Task<WindowsStartupObservation> ReadStartupObservationAsync()
    {
        lock (_sync) ObjectDisposedException.ThrowIf(_inputClosed, this);
        try
        {
            var observation = await _startupObservation.ReadAsync(_cts.Token);
            lock (_sync) _startupObservationFailed = false;
            return observation;
        }
        catch (Exception failure)
        {
            lock (_sync)
            {
                _startupObservationFailed = true;
                _status.Ready = false;
                _status.State = "StartupObservationUnavailable";
                _status.LastError = "Original startup observation is unavailable or retains unfinished cleanup.";
            }
            throw new StartupObservationUnavailableException(failure);
        }
    }

    private void RequireStartupObservationSettled()
    {
        if (!_startupObservation.TrySettle())
            throw new StartupObservationUnavailableException(new IOException("Original command cleanup remains unsettled."));
    }

    private async Task CloseStartupObservationAdmissionAndDrainAsync()
    {
        lock (_sync) _inputClosed = true;
        // Join any already admitted start before checking the retained probe. No
        // new start can pass _inputClosed after this barrier. Never hold either
        // host lock during the potentially unbounded original-owner settlement.
        await _shellLifecycleLock.WaitAsync();
        _shellLifecycleLock.Release();
        while (!_startupObservation.TrySettle()) await Task.Delay(20);
    }
}
