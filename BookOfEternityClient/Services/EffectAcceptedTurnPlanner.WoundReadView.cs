namespace BookOfEternityClient.Services;

internal static partial class EffectAcceptedTurnPlanner
{
    internal sealed partial class EffectAcceptedDraft
    {
        /// <summary>
        /// Gets the current wound version for a resource-owned lazy read binding.
        /// This number alone grants no authority.
        /// </summary>
        internal long WoundReadVersion => _woundVersion;

        /// <summary>
        /// Gets the local effect-history version used to invalidate cached reads without inventing a routing generation.
        /// </summary>
        internal long DependencyCutVersion => _dependencyCutVersion;

        /// <summary>
        /// Checks whether a captured wound version remains readable without a pending installation.
        /// </summary>
        /// <param name="version">
        /// Version captured by the actual resource mechanics owner.
        /// </param>
        /// <returns>
        /// True for the current usable version; otherwise false.
        /// </returns>
        internal bool IsCurrentWoundRead(long version) =>
            !_disposed && !_faulted && !_completed && !HasPendingWoundIntegration && version == _woundVersion;

        /// <summary>
        /// Reads detached current snapshots without initializing the draft, allocating identities or executing a phase.
        /// This read is not the post-dependency-cut authority required to apply a worsening.
        /// </summary>
        /// <param name="capture">
        /// Actual original capture owning this draft and its accepted base.
        /// </param>
        /// <param name="resources">
        /// Exact resource executor belonging to the capture.
        /// </param>
        /// <param name="source">
        /// Exact source owner belonging to the capture.
        /// </param>
        /// <param name="version">
        /// Previously captured wound version; stale versions are rejected.
        /// </param>
        /// <param name="failures">
        /// Receives ownership or initial-state validation failures.
        /// </param>
        /// <returns>
        /// Detached current data, or null when ownership or state validation fails.
        /// </returns>
        internal WoundOperationBeforeData? ReadCurrentWoundView(
            ValidationService.SpiritualOriginalTurnCapture capture,
            AcceptedMechanicsPlanner.ResourceExecutionSession resources,
            ValidationService.SpiritualWoundSourceSession source, long version, List<ValidationIssue> failures)
        {
            if (System.Threading.Interlocked.CompareExchange(ref _busy, 1, 0) != 0)
                throw new InvalidOperationException("Wound snapshot reads cannot re-enter the effect draft.");
            try
            {
                if (!IsCurrentWoundRead(version) ||
                    !ReferenceEquals(capture.ReadOwnedWoundDraft(resources, source), this) ||
                    !capture.OwnsWoundReadBase(this, plan))
                {
                    Add(failures, "acceptedTurn.wounds", "spiritual_wound_read_stale",
                        "the exact current original draft and wound version", "stale or foreign");
                    return null;
                }
                var wounds = _currentWoundState ?? capture.ReadInitialWoundReadSeed(this, plan, failures);
                if (wounds == null || failures.Count != 0)
                    return null;
                return new(wounds.WoundCarriers, wounds.WoundIdentity, wounds.WoundHistory,
                    _initializationAttempted ? workspace.ToInput() : plan.ResourceTriggerCarriers,
                    _initializationAttempted ? identityRoot.ReadSnapshot() : plan.IdentityIndexAfterImage);
            }
            finally { System.Threading.Volatile.Write(ref _busy, 0); }
        }

        /// <summary>
        /// Reads the physical capture's actual current generation at an exact ordinary checkpoint without running any phase.
        /// </summary>
        /// <param name="capture">
        /// Locked original validator capture owning both executors.
        /// </param>
        /// <param name="resources">
        /// Exact resource executor of that capture.
        /// </param>
        /// <param name="checkpoint">
        /// Current closed checkpoint issued by the executor.
        /// </param>
        /// <param name="version">
        /// Expected current wound generation version.
        /// </param>
        /// <param name="cutVersion">
        /// Expected current local effect journal version.
        /// </param>
        /// <param name="failures">
        /// Receives ownership or wound-state agreement diagnostics.
        /// </param>
        /// <returns>
        /// Detached current state, or null when ownership, versions or canonical wound agreement fail.
        /// </returns>
        internal WoundOperationBeforeData? ReadCurrentWoundView(
            ValidationService.MortalOriginalTurnCapture capture,
            AcceptedMechanicsPlanner.ResourceExecutionSession resources,
            AcceptedMechanicsPlanner.ResourceClosedBoundaryCheckpoint checkpoint,
            long version, long cutVersion, List<ValidationIssue> failures)
        {
            if (System.Threading.Interlocked.CompareExchange(ref _busy, 1, 0) != 0)
                throw new InvalidOperationException("Wound snapshot reads cannot re-enter the effect draft.");
            try
            {
                if (!IsCurrentWoundRead(version) || cutVersion != _dependencyCutVersion ||
                    !capture.OwnsWoundReadBase(this, plan, resources, checkpoint))
                {
                    Add(failures, "acceptedTurn.wounds", "mortal_wound_read_stale",
                        "the exact current original draft, checkpoint and wound version", "stale or foreign");
                    return null;
                }
                var wounds = _currentWoundState ?? capture.ReadInitialWoundReadSeed();
                var catalog = WoundCarrierCatalog.Build(wounds.WoundCarriers!);
                var identities = WoundIdentityState.Parse(wounds.WoundIdentity!.ToJsonString(), WoundIdentityState.StatePath);
                var history = WoundHistoryState.Parse(wounds.WoundHistory!.ToJsonString(), WoundHistoryState.HistoryPath);
                failures.AddRange(catalog.Issues);
                failures.AddRange(identities.Issues);
                failures.AddRange(history.Issues);
                if (identities.State != null && history.State != null && failures.Count == 0)
                    failures.AddRange(history.State.ValidateAgreement(identities.State, catalog));
                return failures.Count == 0 ? new(wounds.WoundCarriers, wounds.WoundIdentity, wounds.WoundHistory,
                    _initializationAttempted ? workspace.ToInput() : plan.ResourceTriggerCarriers,
                    _initializationAttempted ? identityRoot.ReadSnapshot() : plan.IdentityIndexAfterImage) : null;
            }
            finally { System.Threading.Volatile.Write(ref _busy, 0); }
        }
    }
}
