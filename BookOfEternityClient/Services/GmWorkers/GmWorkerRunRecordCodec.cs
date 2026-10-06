namespace BookOfEternityClient.Services.GmWorkers;

// R1 compile scaffold. Cold evidence interpretation is deliberately absent for causal RED.
internal static class GmWorkerRunRecordCodec
{
    internal const int MaximumBytes = 64 * 1024;
    internal static WorkerRunRecordObservation Observe(ReadOnlyMemory<byte> bytes) =>
        new(WorkerRunObservationKind.Missing, null);
}
