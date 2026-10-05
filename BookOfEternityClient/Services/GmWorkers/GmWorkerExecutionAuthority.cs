namespace BookOfEternityClient.Services.GmWorkers;

internal sealed record GmWorkerExecutionIdentity(string RunId, GmWorkerBackend Backend, string Guarantee);

// B2 scaffold: deliberately captures only the former completed-stop shape.
// Not consumed by the pool; synthetic admission remains closed until connected.
internal sealed class GmWorkerExecutionAuthority(GmWorkerExecutionIdentity identity, WorkerTaskPacket task)
{
    internal bool ObserveStop(GmWorkerStopEvidence evidence) =>
        evidence.State == GmWorkerStopState.StoppedWithinScope && evidence.CleanupComplete && !evidence.AuthorityRetained;

    internal void ObserveUncertainty(string reason) { }
}
