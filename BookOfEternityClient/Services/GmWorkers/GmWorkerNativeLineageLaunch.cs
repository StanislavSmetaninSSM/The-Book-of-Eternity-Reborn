namespace BookOfEternityClient.Services.GmWorkers;

// Preserve the existing worker owner type; both transports share this single engine.
internal sealed class GmWorkerNativeLineageLaunch : NativeLineageOwner
{
    internal GmWorkerNativeLineageLaunch(GmWorkerDurableExecution? durable) : base(durable) { }
}
