using Microsoft.Win32.SafeHandles;
using BookOfEternityClient.Services.GmWorkers;
using Xunit;

namespace BookOfEternityClient.Tests;

public sealed class HostExitObservationDiagnosticTests
{
    [Theory]
    [InlineData(-1, 0, 4)] // EINTR still fails closed: diagnostics are not a retry fix.
    [InlineData(-1, 0, 9)]
    [InlineData(1, 32, 0)] // POLLNVAL
    [InlineData(1, 8, 0)] // POLLERR
    public async Task PollFailure_RetainsExactOriginalObservation(int result, short events, int error)
    {
        using var handle = File.OpenHandle(Path.GetTempFileName(), FileMode.Open,
            FileAccess.ReadWrite, FileShare.ReadWrite, FileOptions.DeleteOnClose);
        var calls = 0;
        var observation = new GmWorkerHostIdentity.PidfdPollObservation(result, events, result < 0 ? error : null);
        var observer = new GmWorkerHostIdentity.PidfdExitObserver(_ => { calls++; return observation; });
        var task = observer.WaitAsync(handle);
        var failure = await Assert.ThrowsAsync<InvalidDataException>(() => task);
        Assert.Equal(1, calls);
        Assert.False(handle.IsClosed);
        var captured = Assert.IsType<HostExitFaultCapture.Fault>(HostExitFaultCapture.Capture(task));
        Assert.Equal(failure.GetType().FullName, captured.ExceptionType);
        Assert.Equal(failure.Message, captured.Message);
        Assert.Equal(failure.StackTrace, captured.StackText);
        Assert.Equal(observation, captured.Poll);
        Assert.True(captured.CaptureComplete);
    }

    [Theory]
    [InlineData(1)]
    [InlineData(16)]
    [InlineData(17)]
    public async Task LiveThenExit_UsesSameHeldHandleWithoutFault(short events)
    {
        var path = Path.GetTempFileName();
        using var handle = File.OpenHandle(path, FileMode.Open, FileAccess.ReadWrite,
            FileShare.ReadWrite, FileOptions.DeleteOnClose);
        var calls = 0; var descriptor = handle.DangerousGetHandle().ToInt32();
        var observer = new GmWorkerHostIdentity.PidfdExitObserver(fd =>
        {
            Assert.Equal(descriptor, fd);
            Assert.False(handle.IsClosed);
            return ++calls == 1 ? new(0, 0, null) : new(1, events, null);
        });
        var task = observer.WaitAsync(handle);
        await task.WaitAsync(TimeSpan.FromSeconds(2));
        Assert.Equal(2, calls);
        Assert.Null(HostExitFaultCapture.Capture(task));
    }

    [Fact]
    public async Task DisposedHandle_AddRefFaultRetainsOriginalExceptionWithoutPoll()
    {
        var path = Path.GetTempFileName();
        var handle = File.OpenHandle(path, FileMode.Open, FileAccess.ReadWrite,
            FileShare.ReadWrite, FileOptions.DeleteOnClose);
        handle.Dispose();
        var calls = 0;
        var task = new GmWorkerHostIdentity.PidfdExitObserver(_ => { calls++; return new(1, 1, null); }).WaitAsync(handle);
        var failure = await Assert.ThrowsAsync<ObjectDisposedException>(() => task);
        Assert.Equal(0, calls);
        var captured = Assert.IsType<HostExitFaultCapture.Fault>(HostExitFaultCapture.Capture(task));
        Assert.Equal(failure.GetType().FullName, captured.ExceptionType);
        Assert.Equal(failure.Message, captured.Message);
        Assert.Equal(failure.StackTrace, captured.StackText);
        Assert.Null(captured.Poll);
    }

    [Fact]
    public void UncompletedCancelledAndSuccessfulTasks_ProvideNoFaultEvidence()
    {
        var pending = new TaskCompletionSource();
        Assert.Null(HostExitFaultCapture.Capture(null));
        Assert.Null(HostExitFaultCapture.Capture(pending.Task));
        Assert.False(pending.Task.IsCompleted);
        Assert.Null(HostExitFaultCapture.Capture(Task.CompletedTask));
        Assert.Null(HostExitFaultCapture.Capture(Task.FromCanceled(new CancellationToken(true))));
    }
}
