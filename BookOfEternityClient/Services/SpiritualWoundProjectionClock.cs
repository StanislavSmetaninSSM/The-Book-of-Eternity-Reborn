using System.Globalization;
using System.Text.Json.Nodes;

namespace BookOfEternityClient.Services;

/// <summary>
/// Retains one timestamp per pure projection key within a serialized capture attempt.
/// Repeated projections reuse values without repeating ordered journal allocations.
/// </summary>
internal sealed partial class SpiritualWoundProjectionClock : AcceptedTurnProjectionClock
{
    private readonly SpiritualWoundReplayJournal _journal;
    private readonly AcceptedTurnProjectionClock _underlying;
    private readonly Dictionary<string, DateTimeOffset> _values = new(StringComparer.Ordinal);

    /// <summary>
    /// Gets whether the shared allocation attempt remains healthy for retained source ownership.
    /// </summary>
    internal bool IsHealthy => _journal.IsHealthy;

    /// <summary>
    /// Creates a fresh attempt-owned clock without borrowing another attempt's memoized values.
    /// </summary>
    /// <param name="journal">
    /// Non-null ordered allocation stream shared with the attempt's other owners.
    /// </param>
    /// <param name="underlying">
    /// Non-null ordinary clock invoked only for new append-mode allocations.
    /// </param>
    internal SpiritualWoundProjectionClock(SpiritualWoundReplayJournal journal, AcceptedTurnProjectionClock underlying)
    {
        _journal = journal ?? throw new ArgumentNullException(nameof(journal));
        _underlying = underlying ?? throw new ArgumentNullException(nameof(underlying));
    }

    /// <inheritdoc/>
    internal override DateTimeOffset GetUtcNow(AcceptedTurnProjectionTimeKind role, JsonObject evidence)
    {
        _journal.EnsureUsable();
        try
        {
            ArgumentNullException.ThrowIfNull(evidence);
            if (!Enum.IsDefined(role)) throw new ArgumentOutOfRangeException(nameof(role));
            if (_activeConflictProbe is not null && role != AcceptedTurnProjectionTimeKind.ConflictResolution)
                throw new InvalidOperationException("A conflict validation probe cannot serve another projection owner.");
            var frozen = evidence.DeepClone().AsObject();
            var key = new JsonObject { ["role"] = role.ToString(), ["evidence"] = frozen.DeepClone() };
            var coordinate = SpiritualWoundStateJson.Hash(key, "projection_clock");
            if (_values.TryGetValue(coordinate, out var cached)) return cached;
            if (_activeConflictProbe is { } probe)
            {
                _values.Add(coordinate, probe.Time);
                return probe.Time;
            }
            var retained = _journal.Request("utc_time", "projections", coordinate,
                () => _underlying.GetUtcNow(role, frozen.DeepClone().AsObject())
                    .ToUniversalTime().ToString("O", CultureInfo.InvariantCulture));
            _journal.EnsureUsable();
            var value = DateTimeOffset.ParseExact(retained, "O", CultureInfo.InvariantCulture);
            _values.Add(coordinate, value);
            return value;
        }
        catch
        {
            _journal.Invalidate();
            throw;
        }
    }
}
