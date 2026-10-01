using System.Collections.Concurrent;

namespace BookOfEternityClient.Core;

internal sealed class CanonicalRootIdentity
{
    private readonly SemaphoreSlim _gmWorkerAuditAppendAdmission = new(1, 1);
    private WeakReference<CanonicalRootIdentity>? _registration;
    private long _sessionGenerationRevision;

    internal CanonicalRootIdentity(string rootPath)
    {
        RootPath = rootPath;
    }

    internal string RootPath { get; }

    internal long SessionGenerationRevision =>
        Volatile.Read(ref _sessionGenerationRevision);

    internal void AdvanceSessionGenerationRevision() =>
        Interlocked.Increment(ref _sessionGenerationRevision);

    internal async ValueTask<GmWorkerAuditAppendAdmissionLease>
        EnterGmWorkerAuditAppendAdmissionAsync(
            CancellationToken cancellationToken = default)
    {
        await _gmWorkerAuditAppendAdmission.WaitAsync(cancellationToken);
        return new GmWorkerAuditAppendAdmissionLease(
            _gmWorkerAuditAppendAdmission);
    }

    internal sealed class GmWorkerAuditAppendAdmissionLease : IAsyncDisposable
    {
        private SemaphoreSlim? _admission;

        internal GmWorkerAuditAppendAdmissionLease(SemaphoreSlim admission)
        {
            _admission = admission;
        }

        public ValueTask DisposeAsync()
        {
            Interlocked.Exchange(ref _admission, null)?.Release();
            return ValueTask.CompletedTask;
        }
    }

    internal void AttachRegistration(
        WeakReference<CanonicalRootIdentity> registration)
    {
        ArgumentNullException.ThrowIfNull(registration);
        if (Interlocked.CompareExchange(
                ref _registration,
                registration,
                comparand: null) != null)
        {
            throw new InvalidOperationException(
                "Canonical root identity is already registered.");
        }
    }

    ~CanonicalRootIdentity()
    {
        var registration = _registration;
        if (registration != null)
        {
            CanonicalRootIdentityInterner.Release(
                RootPath,
                registration);
        }
    }
}

internal static class CanonicalRootIdentityInterner
{
    private static readonly StringComparer RootPathComparer =
        OperatingSystem.IsWindows()
            ? StringComparer.OrdinalIgnoreCase
            : StringComparer.Ordinal;

    private static readonly ConcurrentDictionary<
        string,
        WeakReference<CanonicalRootIdentity>> Identities =
        new(RootPathComparer);

    // Pure key-policy scaffold; filesystem and original journal paths are not rewritten.
    internal static string NormalizeRootKey(string fullPath, bool windows) =>
        throw new NotImplementedException();

    internal static CanonicalRootIdentity Get(string canonicalRoot)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(canonicalRoot);
        var normalizedRoot = Path.TrimEndingDirectorySeparator(
            Path.GetFullPath(canonicalRoot));

        while (true)
        {
            if (Identities.TryGetValue(normalizedRoot, out var existing) &&
                existing.TryGetTarget(out var identity))
            {
                return identity;
            }

            var created = new CanonicalRootIdentity(normalizedRoot);
            var registration = new WeakReference<CanonicalRootIdentity>(created);
            created.AttachRegistration(registration);

            if (existing == null)
            {
                if (Identities.TryAdd(normalizedRoot, registration))
                    return created;
            }
            else if (Identities.TryUpdate(
                         normalizedRoot,
                         registration,
                         existing))
            {
                return created;
            }
        }
    }

    internal static void Release(
        string canonicalRoot,
        WeakReference<CanonicalRootIdentity> registration)
    {
        var pair = new KeyValuePair<
            string,
            WeakReference<CanonicalRootIdentity>>(
            canonicalRoot,
            registration);
        _ = ((ICollection<KeyValuePair<
            string,
            WeakReference<CanonicalRootIdentity>>>)Identities).Remove(pair);
    }
}
