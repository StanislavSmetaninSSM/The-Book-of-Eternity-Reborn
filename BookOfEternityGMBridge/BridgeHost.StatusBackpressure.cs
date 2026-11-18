namespace BookOfEternityGMBridge;

internal sealed partial class BridgeHost
{
    internal enum StatusBackpressureDecision { Refuse, DiscardSealed, KeepNewer, RestoreRefused }

    // DATA decision only; caller retains actual owner checks, lock, signal and admission.
    internal static StatusBackpressureDecision DecideStatusBackpressure(
        bool actualPublicationSucceeded, bool sealedPublication, bool newerPending) =>
        !actualPublicationSucceeded ? StatusBackpressureDecision.Refuse :
        sealedPublication ? StatusBackpressureDecision.DiscardSealed :
        newerPending ? StatusBackpressureDecision.KeepNewer : StatusBackpressureDecision.RestoreRefused;
}
