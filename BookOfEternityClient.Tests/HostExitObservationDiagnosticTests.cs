using Microsoft.Win32.SafeHandles;
using System.Text.Json;
using BookOfEternityClient.Services.GmRuntime;
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
    [Fact]
    public async Task OriginalObserverFault_ComposesIntoActualStopStateJsonWithoutSecondTaskRead()
    {
        using var handle = File.OpenHandle(Path.GetTempFileName(), FileMode.Open,
            FileAccess.ReadWrite, FileShare.ReadWrite, FileOptions.DeleteOnClose);
        var original = new GmWorkerHostIdentity.PidfdExitObserver(_ => new(-1, 0, 4)).WaitAsync(handle);
        var failure = await Assert.ThrowsAsync<InvalidDataException>(() => original);
        async Task<TerminalRootExit> RelayOriginal() { await original; return new(0); }
        var root = RelayOriginal();
        Assert.Same(failure, await Assert.ThrowsAsync<InvalidDataException>(() => root));
        var session = new DiagnosticSession(root);
        var captured = GmSessionRunCoordinator.CaptureOriginalRootExit(session);
        Assert.Equal(1, session.Reads);
        var state = State(captured.Status, captured.Fault);
        using var json = JsonDocument.Parse(JsonSerializer.Serialize(state));
        Assert.Equal("Faulted", json.RootElement.GetProperty("RootExitStatus").GetString());
        var fault = json.RootElement.GetProperty("RootExitFault");
        Assert.Equal(failure.StackTrace, fault.GetProperty("StackText").GetString());
        Assert.Equal(4, fault.GetProperty("Poll").GetProperty("ErrorNumber").GetInt32());
        Assert.True(GmSessionRunCoordinator.StopCaptureComplete(true, state));
        Assert.False(GmSessionRunCoordinator.StopCaptureComplete(false, state));
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public void IncompleteOriginalFault_MakesAggregateCaptureIncomplete(bool hostile)
    {
        Exception failure = hostile ? new HostileException() : new Exception(new string('x', 2049));
        var task = Task.FromException<TerminalRootExit>(failure);
        var session = new DiagnosticSession(task);
        var captured = GmSessionRunCoordinator.CaptureOriginalRootExit(session);
        Assert.Equal(1, session.Reads);
        Assert.NotNull(captured.Fault);
        Assert.False(captured.Fault.CaptureComplete);
        Assert.NotNull(captured.Fault.CaptureFailure);
        if (!hostile) Assert.Equal(2048, captured.Fault.Message!.Length);
        var state = State(captured.Status, captured.Fault);
        Assert.False(GmSessionRunCoordinator.StopCaptureComplete(true, state));
        using var json = JsonDocument.Parse(JsonSerializer.Serialize(state));
        Assert.False(json.RootElement.GetProperty("RootExitFault").GetProperty("CaptureComplete").GetBoolean());
    }

    [Fact]
    public void OriginalPendingSession_CapturedOnceWithoutWaiting()
    {
        var root = new TaskCompletionSource<TerminalRootExit>();
        var session = new DiagnosticSession(root.Task);
        var captured = GmSessionRunCoordinator.CaptureOriginalRootExit(session);
        Assert.Equal(1, session.Reads);
        Assert.Null(captured.Fault);
        Assert.False(root.Task.IsCompleted);
        Assert.True(GmSessionRunCoordinator.StopCaptureComplete(true, State(captured.Status, null)));
    }

    private static GmSessionRunCoordinator.StopFailureState State(string? status, HostExitFaultCapture.Fault? fault) =>
        new(0, true, true, true, true, false, false, false, null, true, [], status, null, null, fault);

    private sealed class HostileException : Exception
    {
        public override string Message => throw new InvalidOperationException("hostile diagnostic getter");
    }

    // Diagnostic composition only: this fixture never acquires or impersonates coordinator authority.
    private sealed class DiagnosticSession(Task<TerminalRootExit> root) : IOwnedTerminalSession
    {
        public int Reads;
        public Task<TerminalRootExit> RootExited { get { Reads++; return root; } }
        public TerminalIdentity Identity => throw new NotSupportedException();
        public Stream InputWriter => throw new NotSupportedException();
        public Stream OutputReader => throw new NotSupportedException();
        public Task<string> AuthorityLost => throw new NotSupportedException();
        public ValueTask ResizeAsync(TerminalSize size, CancellationToken token) => throw new NotSupportedException();
        public Task<TerminalStopEvidence> StopAndObserveAsync(CancellationToken token) => throw new NotSupportedException();
        public ValueTask DisposeAsync() => throw new NotSupportedException();
    }

}
