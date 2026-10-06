using BookOfEternityClient.Core;
using BookOfEternityClient.Services.GmRuntime;
using System.Text.Json;

namespace BookOfEternityGMBridge;

internal sealed partial class BridgeHost
{
    private sealed record StatusPublication(GmSessionRunCoordinator Owner, byte[] Bytes);
    private StatusPublication? _pendingStatus;
    private TaskCompletionSource _firstStatus = SettledStatus();
    private Task _statusPublisher = Task.CompletedTask;
    private bool _statusSealed = true;
    private SemaphoreSlim? _statusSignal;
    private TaskCompletionSource _statusSettlement = SettledStatus();
    private static TaskCompletionSource SettledStatus()
    {
        var settled = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        settled.SetResult(); return settled;
    }

    private void OpenOriginalStatusPublication()
    {
        lock (_sync)
        {
            if (!_statusPublisher.IsCompleted || _pendingStatus != null) throw new InvalidOperationException("Original status publication remains retained.");
            _statusSealed = false;
            _firstStatus = new(TaskCreationOptions.RunContinuationsAsynchronously);
            _statusSettlement = new(TaskCreationOptions.RunContinuationsAsynchronously);
            var signal = new SemaphoreSlim(0, 1);
            _statusSignal = signal;
            _statusPublisher = Task.Run(() => PublishOriginalStatusAsync(signal));
        }
    }

    private void QueueOriginalStatusPublication()
    {
        lock (_sync)
        {
            _status.UpdatedAtUtc = DateTimeOffset.UtcNow.ToString("O");
            // Serialization freezes the complete nested collections under _sync.
            // The captured owner can never be redirected to a later epoch.
            if (_statusSealed || _mainRun is not { AdmissionClosed: false, IsUncertain: false } owner) return;
            _pendingStatus = new(owner, JsonSerializer.SerializeToUtf8Bytes(_status, JsonOpts));
            if (_statusSignal!.CurrentCount == 0) _statusSignal.Release();
        }
    }

    private async Task PublishOriginalStatusAsync(SemaphoreSlim signal)
    {
        try
        {
            while (true)
            {
                await signal.WaitAsync();
                StatusPublication? publication;
                lock (_sync)
                {
                    publication = _pendingStatus; _pendingStatus = null;
                    if (_statusSealed) return;
                    if (publication == null) continue;
                }
                await publication.Owner.RunOperationAsync(async () =>
                {
                    await _neutralFiles!.WriteFileAtomicBytesAsync("game_state/control/gm_bridge_status.json", publication.Bytes);
                    return true;
                });
                _firstStatus.TrySetResult();

            }
        }
        catch (Exception failure)
        {
            lock (_sync)
            {
                _pendingStatus = null; _statusSealed = true;
                _mainRun?.NotifyUncertain();
                _status.LastError = "Original status publication is unconfirmed.";
                _statusSettlement.TrySetException(failure);
                _firstStatus.TrySetException(failure);
            }
            throw;
        }
        finally { signal.Dispose(); }
    }

    private async Task SealOriginalStatusPublicationAsync()
    {
        Task actual;
        lock (_sync)
        {
            _statusSealed = true; _pendingStatus = null; actual = _statusPublisher;
            if (!actual.IsCompleted && _statusSignal is { CurrentCount: 0 }) _statusSignal.Release();
        }
        try { await actual; lock (_sync) _statusSignal = null; _statusSettlement.TrySetResult(); }
        catch (Exception failure) { _mainRun?.NotifyUncertain(); _statusSettlement.TrySetException(failure); throw; }
        await _statusSettlement.Task;
    }
}
