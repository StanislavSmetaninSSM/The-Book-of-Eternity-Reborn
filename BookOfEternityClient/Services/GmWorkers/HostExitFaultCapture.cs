namespace BookOfEternityClient.Services.GmWorkers;

// Diagnostic DATA only: never a liveness or cleanup decision.
internal static class HostExitFaultCapture
{
    internal const string PollDataKey = "BOE.HostExit.PollObservation";
    internal sealed record Fault(string ExceptionType, string? Message, string? StackText,
        GmWorkerHostIdentity.PidfdPollObservation? Poll, bool CaptureComplete, string? CaptureFailure);

    internal static Fault? Capture(Task? originalExit)
    {
        // Already-completed original task only. No wait, native call or authority decision.
        if (originalExit?.IsFaulted != true) return null;
        var failure = originalExit.Exception!.GetBaseException();
        string? message = null, stack = null, captureFailure = null;
        GmWorkerHostIdentity.PidfdPollObservation? poll = null;
        try { poll = failure.Data[PollDataKey] is GmWorkerHostIdentity.PidfdPollObservation value ? value : null; }
        catch (Exception error) { captureFailure = error.GetType().FullName; }
        try { message = failure.Message; }
        catch (Exception error) { captureFailure ??= error.GetType().FullName; }
        try { stack = failure.StackTrace; }
        catch (Exception error) { captureFailure ??= error.GetType().FullName; }
        return new(failure.GetType().FullName!, message,
            stack, poll, captureFailure == null, captureFailure);
    }
}
