namespace BookOfEternityClient.Services.GmWorkers;

// Diagnostic DATA only: never a liveness or cleanup decision.
internal static class HostExitFaultCapture
{
    internal const string PollDataKey = "BOE.HostExit.PollObservation";
    internal sealed record Fault(string ExceptionType, string? Message, string? StackText,
        GmWorkerHostIdentity.PidfdPollObservation? Poll, bool CaptureComplete, string? CaptureFailure);

    internal static Fault? Capture(Task? originalExit) => null;
}
