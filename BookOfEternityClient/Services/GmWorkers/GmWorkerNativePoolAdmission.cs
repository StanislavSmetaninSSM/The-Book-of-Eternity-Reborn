using BookOfEternityClient.Core;

namespace BookOfEternityClient.Services.GmWorkers;

// Explicitly injected, isolated fixture capability. Never constructed from a
// public profile, configuration, environment variable or command-line option.
internal sealed class GmWorkerNativePoolAdmission(string packageDirectory, string fixtureRoot)
{
    internal string PackageDirectory { get; } = Path.GetFullPath(packageDirectory);
    internal string FixtureRoot { get; } = Path.GetFullPath(fixtureRoot);
    internal string RuntimeBase => Path.Combine(FixtureRoot, "native-runtime");

    internal GmWorkerProposalStore CreateProposalStore(FileSystemManager fs)
    {
        SelectFor(fs);
        return new(fs, (lease, path, bytes) => fs.WriteFileAtomicBytesAsync(lease, path, bytes),
            new GmWorkerSyntheticBundlePublication(FixtureRoot));
    }

    internal GmWorkerBackendSelection SelectFor(FileSystemManager fs)
    {
        if (!OperatingSystem.IsLinux() || !string.Equals(Path.GetFullPath(fs.BasePath), FixtureRoot, StringComparison.Ordinal))
            throw new InvalidOperationException("Synthetic native admission belongs to another isolated source root.");
        return Selection;
    }

    internal GmWorkerBackendSelection ValidateHost(GmWorkerProcessHostLaunch host)
    {
        if (!OperatingSystem.IsLinux()) throw new PlatformNotSupportedException("Synthetic native admission requires Linux.");
        var relative = Path.GetRelativePath(RuntimeBase, host.WorkerWorkingDirectory);
        if (relative == "." || relative == ".." || relative.StartsWith(".." + Path.DirectorySeparatorChar, StringComparison.Ordinal) || Path.IsPathRooted(relative))
            throw new InvalidOperationException("Synthetic native host is outside its admitted detached runtime root.");
        return Selection;
    }

    private static GmWorkerBackendSelection Selection => new(GmWorkerBackendRequest.NativeLineage,
        GmWorkerRequiredCapability.SyntheticWorkerRelease, GmWorkerBackend.NativeLineage,
        GmWorkerBackendAvailability.PreflightRequired, GmWorkerBackendSelector.NativeGuarantee,
        "Explicit isolated synthetic pool fixture only; general Linux Release remains unavailable.",
        GmWorkerBackendAvailability.NotImplemented);
}
