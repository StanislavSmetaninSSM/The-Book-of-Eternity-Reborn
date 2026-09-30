namespace BookOfEternityClient.Services;

internal static partial class AcceptedMechanicsPlanner
{
    /// <summary>
    /// Captures completed ordinary resource work and its closed effect history.
    /// Signed source binding belongs to the common capture; construction alone
    /// does not confer ownership by a resource session.
    /// </summary>
    internal sealed class OriginalResourcePrefix
    {
        internal OriginalResourcePrefix(ResourceStateLedger state,
            IReadOnlyList<ResourceTransition> applied, IReadOnlyList<ResourceTransition> replay,
            IReadOnlyList<ResourceAppliedEvent> events,
            AcceptedEffectBoundaryTranscript.ClosedPrefix effects)
        {
            State = state;
            AppliedTransitions = Array.AsReadOnly(applied.ToArray());
            ReplayTransitions = Array.AsReadOnly(replay.ToArray());
            Events = Array.AsReadOnly(events.ToArray());
            EffectPrefix = effects;
        }
        internal ResourceStateLedger State { get; }
        internal IReadOnlyList<ResourceTransition> AppliedTransitions { get; }
        internal IReadOnlyList<ResourceTransition> ReplayTransitions { get; }
        internal IReadOnlyList<ResourceAppliedEvent> Events { get; }
        internal AcceptedEffectBoundaryTranscript.ClosedPrefix EffectPrefix { get; }
    }

    internal sealed partial class ResourceExecutionSession
    {
        private bool _originalPrefixRequested;
        internal bool OriginalPrefixRequested => _originalPrefixRequested;
        /// <summary>
        /// Checks whether this usable executor produced the exact retained prefix.
        /// A successful final result preserves ownership; failure or disposal revokes it.
        /// </summary>
        /// <param name="prefix">
        /// The prefix object to check; a foreign or absent object is not owned.
        /// </param>
        /// <returns>
        /// <see langword="true"/> for the retained prefix of an executor that has not failed or been disposed.
        /// </returns>
        internal bool Owns(OriginalResourcePrefix prefix) =>
            !_disposed && !_faulted && Result?.IsValid != false && _state.Owns(prefix);

        /// <summary>
        /// Executes ordinary work before any exchange is staged, retaining this executor
        /// for later exchanges. Call once before execution; resolve returned resource waits
        /// through the existing receipt continuation before consuming the closed prefix.
        /// </summary>
        /// <returns>
        /// A closed original prefix, an unresolved resource wait, or an invalid final result.
        /// </returns>
        internal ResourceExecutionStep AdvanceOriginalPrefix()
        {
            Enter();
            try
            {
                EnsureUsable();
                if (!_live || _started || _staged != null || _originalPrefixRequested)
                    throw new InvalidOperationException("Request the original prefix once before staging or execution.");
                _originalPrefixRequested = true;
                return MoveNextOwned();
            }
            finally { Exit(); }
        }
    }

    private sealed partial class ResourceExecutionState
    {
        private OriginalResourcePrefix? _originalPrefix;
        internal bool Owns(OriginalResourcePrefix prefix) =>
            prefix != null && ReferenceEquals(_originalPrefix, prefix);
    }
}
