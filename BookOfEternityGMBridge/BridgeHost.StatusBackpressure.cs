namespace BookOfEternityGMBridge;

internal sealed partial class BridgeHost
{
    internal enum StatusBackpressureDecision { Refuse, DiscardSealed, KeepNewer, RestoreRefused }

    // DATA decision only; caller retains actual owner checks, lock, signal and admission.
    // RED stub: no refusal has become a successful or deferred publication.
    internal static StatusBackpressureDecision DecideStatusBackpressure(
        bool actualPublicationSucceeded, bool sealedPublication, bool newerPending) => StatusBackpressureDecision.Refuse;
}
