namespace BookOfEternityClient.Services.GmWorkers;

internal sealed record WorkerLedgerTarget(string RootPath)
{
    internal string DirectoryPath => Path.Combine(RootPath, ".boe_runtime", "worker-runs-v1");
}

internal enum WorkerLedgerMutationKind { Applied, AlreadyExact, Blocked, CommitPending }
internal sealed record WorkerLedgerObservation(WorkerRunObservationKind Kind, long Sequence, long EpochHighWater,
    IReadOnlyList<WorkerRunRecord> Entries);

// R1 storage compile scaffold. It does not yet persist or own filesystem authority.
internal static class GmWorkerRunLedger
{
    internal static Task<WorkerLedgerObservation> ObserveAsync(WorkerLedgerTarget target, CancellationToken cancellationToken = default) =>
        Task.FromResult(new WorkerLedgerObservation(WorkerRunObservationKind.Missing, 0, 0, []));

    internal static Task<WorkerRunLedgerCoordinator?> OpenCoordinatorAsync(WorkerLedgerTarget target, CancellationToken cancellationToken = default) =>
        Task.FromResult<WorkerRunLedgerCoordinator?>(new(target));
}

internal sealed class WorkerRunLedgerCoordinator(WorkerLedgerTarget target) : IAsyncDisposable
{
    internal Task<WorkerLedgerMutationKind> InitializeAsync(CancellationToken cancellationToken = default) =>
        Task.FromResult(WorkerLedgerMutationKind.Applied);
    public ValueTask DisposeAsync() => ValueTask.CompletedTask;
}
