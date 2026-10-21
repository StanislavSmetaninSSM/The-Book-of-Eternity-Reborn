namespace BookOfEternityClient.Tests;

/// <summary>
/// Accounts for every attempted profile timing write separately from validator outcomes.
/// Deferred diagnostic failures are merged only after the validator's primary outcome is known.
/// </summary>
internal sealed class JointCleanupProfileDiagnosticCapture
{
    private readonly GameEngineTurnLifecycleTests.JointOperationTimings _timings;
    private readonly object _gate = new();
    private readonly List<(Exception Error, string Stage)> _failures = new();
    private long _attempted;
    private long _stored;

    /// <summary>
    /// Selects the uncapped JSONL destination without taking ownership of its lifetime.
    /// </summary>
    /// <param name="writer">
    /// The profile's external timing writer, kept open through owner close and cleanup.
    /// </param>
    /// <param name="timeProvider">
    /// Monotonic diagnostic clock, or <see langword="null"/> for <see cref="TimeProvider.System"/>.
    /// </param>
    internal JointCleanupProfileDiagnosticCapture(TextWriter writer, TimeProvider? timeProvider = null) => _timings = new(writer);

    /// <summary>
    /// Returns a detached ledger of every named timing-write attempt.
    /// </summary>
    /// <returns>
    /// Per-operation and boundary write costs, including failed and unmeasured attempts.
    /// </returns>
    internal WriteCost[] GetWriteCosts() => [];

    /// <summary>
    /// Describes the measured serializer and synchronous writer span for a literal timer boundary.
    /// </summary>
    /// <param name="Operation">
    /// Literal operation name supplied by the production hook.
    /// </param>
    /// <param name="State">
    /// Literal begin or end boundary supplied by that hook.
    /// </param>
    /// <param name="Attempted">
    /// Every attempted timing write for this name and boundary.
    /// </param>
    /// <param name="Stored">
    /// Writes whose serializer and writer returned successfully.
    /// </param>
    /// <param name="Measured">
    /// Attempts with both monotonic timestamps and a computed duration, including failed writes.
    /// </param>
    /// <param name="SerializationAndWriteElapsed">
    /// Summed elapsed serializer, inner lock and synchronous writer time; excludes outer gate wait and accounting.
    /// </param>
    internal readonly record struct WriteCost(string Operation, string State, long Attempted, long Stored,
        long Measured, TimeSpan SerializationAndWriteElapsed);

    /// <summary>
    /// Gets the number of timing boundaries offered to the observer, without a quota.
    /// </summary>
    internal long Attempted { get { lock (_gate) return _attempted; } }

    /// <summary>
    /// Gets the number of timing writes that returned successfully.
    /// </summary>
    internal long Stored { get { lock (_gate) return _stored; } }

    /// <summary>
    /// Gets whether every offered timing boundary was written and no deferred diagnostic error exists.
    /// Successful writer closing and exact artifact hashing are checked separately by the profile.
    /// </summary>
    internal bool Complete { get { lock (_gate) return _attempted == _stored && _failures.Count == 0; } }

    /// <summary>
    /// Observes a real timer boundary using the existing serializer and sequence numbering.
    /// </summary>
    /// <param name="name">
    /// The literal operation name received from the production hook.
    /// </param>
    /// <param name="boundary">
    /// The actual begin or end boundary received from that hook.
    /// </param>
    /// <param name="elapsed">
    /// The production timer's elapsed interval, or zero at begin.
    /// </param>
    internal void Observe(string name, string boundary, TimeSpan elapsed)
    {
        lock (_gate)
        {
            _attempted++;
            try
            {
                _timings.Observe(name, boundary, elapsed);
                _stored++;
            }
            catch (Exception error)
            {
                // The production hook isolates observation errors. Retain them here before
                // that boundary, while allowing the actual validator and later writes to run.
                _failures.Add((error, "timing-observer:" + name + ":" + boundary));
            }
        }
    }

    /// <summary>
    /// Records a diagnostic error for later durable reporting without replacing a validator failure.
    /// </summary>
    /// <param name="error">
    /// The original storage or component-report exception.
    /// </param>
    /// <param name="stage">
    /// The exact diagnostic stage retained with this error.
    /// </param>
    internal void RecordDiagnosticFailure(Exception error, string stage)
    {
        lock (_gate) _failures.Add((error, stage));
    }

    /// <summary>
    /// Returns every deferred diagnostic failure without removing or limiting observations.
    /// </summary>
    /// <returns>
    /// A snapshot of all original exception objects and their diagnostic stage labels.
    /// </returns>
    internal (Exception Error, string Stage)[] GetFailures()
    {
        lock (_gate) return _failures.ToArray();
    }
}
