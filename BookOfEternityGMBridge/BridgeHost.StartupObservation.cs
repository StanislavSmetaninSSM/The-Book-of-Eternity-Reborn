using BookOfEternityClient.Services.GmRuntime;

namespace BookOfEternityGMBridge;

internal sealed partial class BridgeHost
{
    private readonly WindowsStartupObservationProbe _startupObservation = new();
    private bool _startupObservationFailed;

    // Controlled process injection does not enable the Windows production path.
    internal BridgeHost(string sessionPath, string pipeName, WindowsStartupObservationProbe probe)
        : this(sessionPath, pipeName) => _startupObservation = probe;

    private async Task<WindowsStartupObservation> ReadStartupObservationAsync()
    {
        try
        {
            var observation = await _startupObservation.ReadAsync(_cts.Token);
            lock (_sync) _startupObservationFailed = false;
            return observation;
        }
        catch
        {
            lock (_sync)
            {
                _startupObservationFailed = true;
                _status.Ready = false;
                _status.State = "StartupObservationUnavailable";
                _status.LastError = "Original startup observation is unavailable or retains unfinished cleanup.";
            }
            throw;
        }
    }
}
