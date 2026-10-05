namespace BookOfEternityClient.Services.GmWorkers;

internal enum GmWorkerBackendRequest { Auto, SystemdUser, NativeLineage }
internal enum GmWorkerRequiredCapability { NeutralHost, WorkerRelease }
internal enum GmWorkerBackend { None, WindowsJob, NativeLineage }
internal enum GmWorkerBackendAvailability { NotImplemented, NotQualified, Unsupported, PreflightRequired, Available }

internal sealed record GmWorkerBackendSelection(
    GmWorkerBackendRequest Requested,
    GmWorkerRequiredCapability Capability,
    GmWorkerBackend Backend,
    GmWorkerBackendAvailability Availability,
    string Guarantee,
    string Reason,
    GmWorkerBackendAvailability SystemdAvailability)
{
    internal bool CanStart => Availability is GmWorkerBackendAvailability.Available or GmWorkerBackendAvailability.PreflightRequired;
}

internal static class GmWorkerBackendSelector
{
    internal const string NativeGuarantee = "ordinary-same-namespace-lineage";

    // Initial feature scaffold; no caller uses this until its causal tests pass.
    internal static GmWorkerBackendSelection Select(GmWorkerBackendRequest request,
        GmWorkerRequiredCapability capability, bool windows, bool linux) =>
        new(request, capability, GmWorkerBackend.None, GmWorkerBackendAvailability.Unsupported,
            "none", "Owned launch is not implemented.", GmWorkerBackendAvailability.NotImplemented);
}
