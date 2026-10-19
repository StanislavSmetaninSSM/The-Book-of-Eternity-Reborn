namespace BookOfEternityClient.Core;

public partial class FileSystemManager
{
    /// <summary>
    /// Keeps one actual mutation registered through its publication, compensation and cleanup.
    /// </summary>
    /// <param name="release">
    /// Callback that removes only this token from its original shared access.
    /// </param>
    internal sealed class MainMutationReservation(Action<MainMutationReservation> release) : IDisposable
    {
        private Action<MainMutationReservation>? _release = release;

        /// <summary>
        /// Releases this registration at most once without closing or retaining an admission.
        /// </summary>
        public void Dispose() => Interlocked.Exchange(ref _release, null)?.Invoke(this);
    }

    /// <summary>
    /// Registers a lease-taking mutation before preparation or recovery can begin.
    /// </summary>
    /// <param name="lease">
    /// Active original physical lease; its existing admission supplies the shared state.
    /// </param>
    /// <returns>
    /// A token to dispose after all asynchronous work and cleanup have finished.
    /// </returns>
    private MainMutationReservation BeginValidationMutation(CanonicalWriteLease lease)
    {
        EnsurePhysicalCanonicalWriteLease(lease);
        return (lease.MainAdmission ?? throw new InvalidOperationException("Mutation requires its original admission."))
            .BeginMutation();
    }

    /// <summary>
    /// Registers a private legacy writer under its current physical frame without acquiring new authority.
    /// </summary>
    /// <returns>
    /// A mutation token covering the private writer and its cleanup.
    /// </returns>
    private MainMutationReservation BeginValidationMutation()
    {
        for (var frame = MainAdmissions.Value; frame != null; frame = frame.Parent)
            if (!frame.Closed && frame.Acquired && frame.PhysicalScope && frame.RootIdentity == CanonicalRootAuthorityIdentity)
                return frame.BeginMutation();
        throw new InvalidOperationException("A private canonical writer requires its original physical admission.");
    }
}
