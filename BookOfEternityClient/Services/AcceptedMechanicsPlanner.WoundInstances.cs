namespace BookOfEternityClient.Services;

internal static partial class AcceptedMechanicsPlanner
{
    internal sealed partial class ResourceExecutionSession
    {
        private readonly Dictionary<EffectAcceptedTurnPlanner.EffectAcceptedDraft.EffectDraftWoundInsertion,
            WoundInstanceRegistration> _woundRegistrations = new();
        private HashSet<string>? _knownWoundInstanceIds;

        /// <summary>
        /// Verifies the actual current boundary selected by an insertion without fabricating an exchange interval.
        /// </summary>
        /// <param name="insertion">
        /// Concrete draft insertion retaining its spiritual interval or ordinary checkpoint.
        /// </param>
        /// <returns>
        /// True only for a usable owner paused at the insertion's exact current boundary; otherwise false.
        /// </returns>
        internal bool OwnsInsertionBoundary(EffectAcceptedTurnPlanner.EffectAcceptedDraft.EffectDraftWoundInsertion insertion) =>
            !_disposed && !_faulted && Result == null && (insertion.Interval is { } interval
                ? _live && _state.IsLatestClosedInterval(interval)
                : !_live && OwnsCurrentCheckpoint(insertion.Checkpoint));

        /// <summary>
        /// Registers actual newly inserted instances with this retained resource owner's existing use arbiter.
        /// Installs the registered insertion's routing view on this owner without publishing the turn.
        /// </summary>
        /// <param name="insertion">
        /// Exact current draft insertion admitted by this owner's latest closed exchange.
        /// </param>
        /// <param name="issues">
        /// Receives ownership, identity or use-budget validation failures.
        /// </param>
        /// <returns>
        /// The retained registration on success, or <see langword="null"/> on rejection.
        /// </returns>
        internal WoundInstanceRegistration? RegisterWoundInstances(
            EffectAcceptedTurnPlanner.EffectAcceptedDraft.EffectDraftWoundInsertion insertion,
            List<ValidationIssue> issues)
        {
            Enter();
            try
            {
                EnsureUsable();
                if (_routing is null || insertion is null || _active != null ||
                    _staged != null || _pendingExchange || !OwnsInsertionBoundary(insertion))
                    return RejectWoundRegistration(issues);
                var preparationIssues = new List<ValidationIssue>();
                var preparation = _routing.PrepareWoundInsertion(insertion, preparationIssues);
                if (preparation is null || preparationIssues.Count != 0)
                {
                    issues.AddRange(preparationIssues);
                    return RejectWoundRegistration(issues);
                }
                if (_woundRegistrations.TryGetValue(insertion, out var retained))
                    return retained;
                var known = _knownWoundInstanceIds ??
                    new HashSet<string>(_routing.ReadOriginalInstanceIds(), StringComparer.Ordinal);
                var ids = insertion.Applications.Select(application => application.EffectId).ToArray();
                if (ids.Length == 0 || ids.Distinct(StringComparer.Ordinal).Count() != ids.Length ||
                    ids.Any(known.Contains))
                    return RejectWoundRegistration(issues);
                var newIds = ids.ToHashSet(StringComparer.Ordinal);
                var seeds = preparation.Index.CanonicalUseSeeds.Where(seed => newIds.Contains(seed.EffectId)).ToArray();
                var seedIssues = _state.RegisterWoundSeeds(seeds);
                if (seedIssues.Count != 0)
                {
                    issues.AddRange(seedIssues);
                    return null;
                }
                try
                {
                    known.UnionWith(ids);
                    _knownWoundInstanceIds = known;
                    var registration = new WoundInstanceRegistration(ids);
                    _woundRegistrations.Add(insertion, registration);
                    if (!_routing.InstallWoundInsertion(this, insertion, issues))
                    {
                        _faulted = true;
                        return null;
                    }
                    if (!_live)
                    {
                        var refreshIssues = _state.RefreshUnfinishedWoundCandidates(_originalResourceInput,
                            _identityFactory, this, insertion);
                        if (refreshIssues.Count != 0)
                        {
                            issues.AddRange(refreshIssues);
                            _faulted = true;
                            return null;
                        }
                    }
                    return registration;
                }
                catch
                {
                    _faulted = true;
                    throw;
                }
            }
            finally { Exit(); }
        }

        /// <summary>
        /// Verifies the exact registered insertion while this owner holds its installation gate.
        /// </summary>
        /// <param name="routing">
        /// Routing owner requesting permission to install its prepared view.
        /// </param>
        /// <param name="insertion">
        /// Actual registered insertion at the latest closed resource interval.
        /// </param>
        /// <returns>
        /// True only inside this usable owner's registered insertion transaction.
        /// </returns>
        internal bool OwnsRegisteredWoundInstallation(EffectAcceptedTurnPlanner.BaseResourceRouting routing,
            EffectAcceptedTurnPlanner.EffectAcceptedDraft.EffectDraftWoundInsertion insertion) =>
            _busy == 1 && !_disposed && !_faulted && Result == null && ReferenceEquals(_routing, routing) &&
            _woundRegistrations.ContainsKey(insertion) && OwnsInsertionBoundary(insertion);

        /// <summary>
        /// Checks an installed insertion without granting common publication authority.
        /// </summary>
        /// <param name="insertion">
        /// Exact insertion registered and installed by this resource owner.
        /// </param>
        /// <returns>
        /// True for this usable owner's current installed insertion; otherwise false.
        /// </returns>
        internal bool OwnsInstalledWound(EffectAcceptedTurnPlanner.EffectAcceptedDraft.EffectDraftWoundInsertion insertion) =>
            !_disposed && !_faulted && Result == null && _woundRegistrations.ContainsKey(insertion) &&
            _routing?.HasInstalled(insertion) == true;

        /// <summary>
        /// Checks that the completed live resource owner registered the whole ordered insertion chain.
        /// </summary>
        /// <param name="insertions">
        /// Every insertion retained by the matching completed effect draft, in version order.
        /// </param>
        /// <returns>
        /// <see langword="true"/> only when every insertion was registered once by this owner
        /// and the last insertion remains its installed routing epoch.
        /// </returns>
        internal bool OwnsCompletedWoundChain(
            IReadOnlyList<EffectAcceptedTurnPlanner.EffectAcceptedDraft.EffectDraftWoundInsertion> insertions) =>
            !_disposed && !_faulted && _live && Result is { IsValid: true } &&
            _woundRegistrations.Count == insertions.Count && insertions.Count != 0 &&
            insertions.Distinct(ReferenceEqualityComparer.Instance).Count() == insertions.Count &&
            insertions.All(_woundRegistrations.ContainsKey) &&
            _routing?.HasInstalled(insertions[^1]) == true;

        /// <summary>
        /// Records a rejected registration without modifying retained instance or budget state.
        /// </summary>
        /// <param name="issues">
        /// Receives the exact insertion ownership diagnostic.
        /// </param>
        /// <returns>
        /// Always <see langword="null"/>.
        /// </returns>
        private static WoundInstanceRegistration? RejectWoundRegistration(List<ValidationIssue> issues)
        {
            issues.AddRange(Issue("spiritual_wound_instance_registration_mismatch",
                "an exact current insertion at this resource owner's latest closed interval", "foreign or stale"));
            return null;
        }

        /// <summary>
        /// Records instance registration only; it grants no routing or publication authority.
        /// </summary>
        internal sealed class WoundInstanceRegistration
        {
            /// <summary>
            /// Retains a detached list of identities registered by the resource owner.
            /// </summary>
            /// <param name="ids">
            /// Newly registered instance identities in actual application order.
            /// </param>
            internal WoundInstanceRegistration(string[] ids) => NewInstanceIds = Array.AsReadOnly(ids.ToArray());

            /// <summary>
            /// Gets the new identities, including instances that have no consuming trigger.
            /// </summary>
            internal IReadOnlyList<string> NewInstanceIds { get; }
        }
    }

    private sealed partial class ResourceExecutionState
    {
        /// <summary>
        /// Checks that the supplied interval is the latest wholly closed resource exchange.
        /// </summary>
        /// <param name="interval">
        /// Exact resource interval admitting the insertion.
        /// </param>
        /// <returns>
        /// <see langword="true"/> only at that closed exchange frontier; otherwise, <see langword="false"/>.
        /// </returns>
        internal bool IsLatestClosedInterval(SpiritualExchangeInterval interval) =>
            intervals.Count != 0 && ReferenceEquals(intervals[^1], interval) && activeBatch is null &&
            unresolvedPendingBoundaries.Count == 0;

        /// <summary>
        /// Adds new instance budgets to the existing arbiter without replaying accepted activations.
        /// </summary>
        /// <param name="seeds">
        /// Validated new-instance seeds derived from the owned insertion's trigger index.
        /// </param>
        /// <returns>
        /// Empty on success, or validation issues when no budgets were registered.
        /// </returns>
        internal IReadOnlyList<ValidationIssue> RegisterWoundSeeds(IReadOnlyList<CanonicalEffectUseSeed> seeds) =>
            ArbiterIssues(arbiter.RegisterNewSeeds(seeds));
    }
}
