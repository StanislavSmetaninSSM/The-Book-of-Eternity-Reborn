namespace BookOfEternityClient.Services.GmWorkers;

internal enum WorkerRunPhase
{
    Prepared, LaunchIntent, ReleaseIntent, Released, StopValidated,
    PublicationIntent, Published, CleanupPending, Uncertain, AbortedBeforeLaunch
}

internal enum WorkerRunBackend { WindowsJob, LinuxSystemd, LinuxNativeLineage }
internal enum WorkerRunScope { WindowsJob, SystemdUnit, OrdinarySamePidNamespace }
internal enum WorkerRunObservationKind { Missing, Quiescent, Uncertain, Blocked }

// Durable descriptions never implement a live coordinator, launch token or stop witness.
internal sealed record WorkerRunIdentity(string RootKey, long Epoch, string RunId,
    string GenerationId, string WorkerId, string TaskId, string TaskSha256,
    WorkerRunBackend Backend, WorkerRunScope Scope, string HostInstanceId, string WorkspacePath);

internal sealed record WorkerRunRecord(int SchemaVersion, WorkerRunIdentity Identity, WorkerRunPhase Phase);
internal sealed record WorkerRunRecordObservation(WorkerRunObservationKind Kind, WorkerRunRecord? Record);
