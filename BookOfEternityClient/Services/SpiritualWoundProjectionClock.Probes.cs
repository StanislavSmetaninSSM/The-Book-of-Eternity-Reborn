using System.Globalization;
using System.Text.Json.Nodes;

namespace BookOfEternityClient.Services;

internal sealed partial class SpiritualWoundProjectionClock
{
    private ConflictValidationProbe? _activeConflictProbe;

    /// <summary>
    /// Opens a discarded conflict-only comparison scope without consuming replay rows or reading time.
    /// Its caller must retain only diagnostics, never a provisional source ticket or projection.
    /// </summary>
    /// <param name="retainedEvidence">
    /// Actual retained source roots and pending evidence; all string values are excluded from probe time selection.
    /// A <see langword="null"/> element denotes absent evidence; the collection itself is required.
    /// </param>
    /// <returns>
    /// An exclusive disposable probe with no commit operation.
    /// </returns>
    internal ConflictValidationProbe BeginConflictValidationProbe(IEnumerable<JsonNode?> retainedEvidence) =>
        new(this, retainedEvidence);

    /// <summary>
    /// Keeps temporary conflict times private and restores memo state unconditionally on cleanup.
    /// It cannot commit journal rows or provide an accepted timestamp.
    /// </summary>
    internal sealed class ConflictValidationProbe : IDisposable
    {
        private readonly SpiritualWoundProjectionClock _owner;
        private readonly SpiritualWoundReplayJournal.Speculation _journalScope;
        private readonly Dictionary<string, DateTimeOffset> _values;
        private bool _closed;

        /// <summary>
        /// Acquires journal and memo lifetime from exactly one clock and chooses a collision-free comparison time.
        /// </summary>
        /// <param name="owner">
        /// Clock providing both the journal scope and detached memo snapshot.
        /// </param>
        /// <param name="retainedEvidence">
        /// Retained JSON whose string values must not equal the temporary UTC representation.
        /// </param>
        internal ConflictValidationProbe(SpiritualWoundProjectionClock owner, IEnumerable<JsonNode?> retainedEvidence)
        {
            ArgumentNullException.ThrowIfNull(owner);
            ArgumentNullException.ThrowIfNull(retainedEvidence);
            owner._journal.EnsureUsable();
            var excluded = new HashSet<string>(StringComparer.Ordinal);
            foreach (var root in retainedEvidence) CollectStrings(root?.DeepClone(), excluded);
            owner._journal.EnsureUsable();
            _owner = owner;
            _values = new Dictionary<string, DateTimeOffset>(owner._values, StringComparer.Ordinal);
            foreach (var value in _values.Values)
                excluded.Add(value.UtcDateTime.ToString("O", CultureInfo.InvariantCulture));
            var time = DateTimeOffset.MinValue;
            while (excluded.Contains(time.UtcDateTime.ToString("O", CultureInfo.InvariantCulture)))
                time = time.AddTicks(1);
            Time = time;
            _journalScope = owner._journal.BeginSpeculation();
            owner._activeConflictProbe = this;
        }

        /// <summary>
        /// Gets the temporary comparison value, which must never enter accepted source consumption.
        /// </summary>
        internal DateTimeOffset Time { get; }

        /// <summary>
        /// Restores the owning memo and journal scope; stale cleanup cannot touch a newer scope.
        /// Permanent journal faults remain permanent.
        /// </summary>
        public void Dispose()
        {
            if (_closed) return;
            if (!ReferenceEquals(_owner._activeConflictProbe, this))
            {
                _owner._journal.Invalidate();
                throw new InvalidOperationException("The conflict validation probe is not current.");
            }
            _journalScope.Dispose();
            _closed = true;
            _owner._values.Clear();
            foreach (var pair in _values) _owner._values.Add(pair.Key, pair.Value);
            _owner._activeConflictProbe = null;
        }

        /// <summary>
        /// Collects exact retained string values without interpreting them as accepted timestamps.
        /// </summary>
        /// <param name="node">
        /// Detached JSON subtree, or <see langword="null"/> for absent evidence.
        /// </param>
        /// <param name="values">
        /// Ordinal exclusion set receiving string leaves.
        /// </param>
        private static void CollectStrings(JsonNode? node, HashSet<string> values)
        {
            if (node is JsonValue value && value.TryGetValue<string>(out var text)) values.Add(text);
            else if (node is JsonObject obj)
                foreach (var pair in obj) CollectStrings(pair.Value, values);
            else if (node is JsonArray array)
                foreach (var child in array) CollectStrings(child, values);
        }
    }
}
