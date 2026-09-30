namespace BookOfEternityClient.Services;

internal sealed partial class SpiritualWoundProjectionClock
{
    /// <summary>
    /// Starts a serialized scope that retains or restores both allocation rows and projection memo.
    /// The caller must revoke execution after a failure that has already advanced gameplay owners.
    /// </summary>
    /// <returns>
    /// A private combined scope whose disposal rolls back uncommitted comparison state.
    /// </returns>
    internal Speculation BeginSpeculation() => new(this);

    /// <summary>
    /// Keeps memoized projection values in the same speculative lifetime as their journal rows.
    /// </summary>
    internal sealed class Speculation : IDisposable
    {
        private readonly SpiritualWoundProjectionClock _owner;
        private readonly SpiritualWoundReplayJournal.Speculation _journalScope;
        private readonly Dictionary<string, DateTimeOffset> _values;
        private bool _closed;

        /// <summary>
        /// Acquires a fresh journal scope and detached memo exclusively from one clock owner.
        /// </summary>
        /// <param name="owner">
        /// Projection clock whose memo is restored on rejection.
        /// </param>
        internal Speculation(SpiritualWoundProjectionClock owner)
        {
            ArgumentNullException.ThrowIfNull(owner);
            owner._journal.EnsureUsable();
            _owner = owner;
            _values = new Dictionary<string, DateTimeOffset>(owner._values, StringComparer.Ordinal);
            _journalScope = owner._journal.BeginSpeculation();
        }

        /// <summary>
        /// Confirms this still-open allocation scope belongs to the expected clock and healthy attempt.
        /// </summary>
        /// <param name="expectedClock">
        /// Exact clock whose allocation transaction the caller is using.
        /// </param>
        internal void EnsureActiveFor(SpiritualWoundProjectionClock expectedClock)
        {
            ArgumentNullException.ThrowIfNull(expectedClock);
            if (!ReferenceEquals(_owner, expectedClock) || _closed)
                throw new InvalidOperationException("The original allocation scope is foreign or closed.");
            _owner._journal.EnsureUsable();
        }

        /// <summary>
        /// Retains journal and memo changes after the actual owner transition accepts them.
        /// Repeated commit is rejected by the journal's single-use ownership check.
        /// </summary>
        internal void Commit()
        {
            _journalScope.Commit();
            _closed = true;
        }

        /// <summary>
        /// Restores both uncommitted components without changing the journal's permanent fault state.
        /// Repeated cleanup and cleanup after commit cannot affect a newer scope.
        /// </summary>
        public void Dispose()
        {
            if (_closed) return;
            _journalScope.Dispose();
            _closed = true;
            _owner._values.Clear();
            foreach (var pair in _values) _owner._values.Add(pair.Key, pair.Value);
        }
    }
}
