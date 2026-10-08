namespace BookOfEternityClient.Services.GmWorkers;

internal enum WorkerRunPhase
{
    Prepared, LaunchIntent, ReleaseIntent, Released, StopValidated,
    PublicationIntent, Published, CleanupPending, Uncertain, AbortedBeforeLaunch, Retired
}

internal enum WorkerRunBackend { WindowsJob, LinuxSystemd, LinuxNativeLineage }
internal enum WorkerRunScope { WindowsJob, SystemdUnit, OrdinarySamePidNamespace }
internal enum WorkerRunObservationKind { Missing, Quiescent, Uncertain, Blocked }

// Durable descriptions never implement a live coordinator, launch token or stop witness.
internal sealed record WorkerRunIdentity(string RootKey, long Epoch, string RunId,
    string GenerationId, string WorkerId, string TaskId, string TaskSha256,
    WorkerRunBackend Backend, WorkerRunScope Scope, string HostInstanceId, string WorkspacePath);

// Persisted progress is a description, never a live acknowledgement or success permit.
internal sealed record WorkerRunPublication(string ProposalId, string ProposalSha256, string ContentSha256, bool Committed);
internal sealed record WorkerRunCleanup(bool RequiredAudit, string? AuditEventId, string? AuditSha256);
internal sealed record WorkerRunProgress(WorkerRunPublication? Publication, WorkerRunCleanup? Cleanup);
internal sealed record WorkerRunRecord(int SchemaVersion, WorkerRunIdentity Identity, WorkerRunPhase Phase,
    WorkerRunProgress? Progress = null);
internal sealed record WorkerRunRecordObservation(WorkerRunObservationKind Kind, WorkerRunRecord? Record);
