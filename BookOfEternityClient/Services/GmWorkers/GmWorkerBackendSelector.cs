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

    internal static GmWorkerBackendSelection Select(GmWorkerBackendRequest request,
        GmWorkerRequiredCapability capability, bool windows, bool linux)
    {
        GmWorkerBackendSelection Unavailable(GmWorkerBackendAvailability state, string reason) =>
            new(request, capability, GmWorkerBackend.None, state, "none", reason,
                GmWorkerBackendAvailability.NotImplemented);
        if (!Enum.IsDefined(request) || !Enum.IsDefined(capability))
            return Unavailable(GmWorkerBackendAvailability.Unsupported, "Unknown owned-launch request.");
        if (windows && request == GmWorkerBackendRequest.Auto)
            return new(request, capability, GmWorkerBackend.WindowsJob, GmWorkerBackendAvailability.Available,
                "windows-job", "Existing Windows Job path.", GmWorkerBackendAvailability.NotImplemented);
        if (!linux)
            return Unavailable(GmWorkerBackendAvailability.Unsupported, "Requested backend is unsupported on this platform.");
        if (request == GmWorkerBackendRequest.SystemdUser)
            return Unavailable(GmWorkerBackendAvailability.NotImplemented, "systemd-user adapter is not implemented or qualified in this build.");
        if (capability == GmWorkerRequiredCapability.WorkerRelease)
            return Unavailable(GmWorkerBackendAvailability.NotQualified, "Linux worker Release requires typed pool/quarantine and durable admission; no process was launched.");
        return new(request, capability, GmWorkerBackend.NativeLineage, GmWorkerBackendAvailability.PreflightRequired,
            NativeGuarantee, "systemd-user is not implemented; native-lineage package and runtime preflight are required before neutral-host launch.",
            GmWorkerBackendAvailability.NotImplemented);
    }
}
