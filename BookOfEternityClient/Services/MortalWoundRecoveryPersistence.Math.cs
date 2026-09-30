namespace BookOfEternityClient.Services;

/// <summary>
/// Records the total elapsed intervals, newly available intervals and next due minute for one epoch.
/// </summary>
/// <param name="ElapsedTotal">
/// The ordinal of the latest interval due at the evaluated minute, or zero before the first interval.
/// </param>
/// <param name="NewIntervals">
/// The elapsed ordinal less the already consumed ordinal.
/// </param>
/// <param name="NextDueMinute">
/// The next interval on the original epoch's schedule.
/// </param>
internal sealed record MortalWoundRecoveryCadenceResult(
    long ElapsedTotal, long NewIntervals, long NextDueMinute);

/// <summary>
/// Calculates interval consumption without changing the canonical anchor epoch.
/// </summary>
internal static class MortalWoundRecoveryCadence
{
    /// <summary>
    /// Evaluates one cadence using checked arithmetic and an existing consumed ordinal.
    /// </summary>
    /// <param name="epochMinute">
    /// The recovery anchor minute, or the deterioration grace deadline when <paramref name="inclusive"/> is true.
    /// </param>
    /// <param name="cadence">
    /// The strictly positive number of minutes between intervals.
    /// </param>
    /// <param name="currentMinute">
    /// The signed minute of the evaluation.
    /// </param>
    /// <param name="consumed">
    /// The nonnegative last consumed ordinal belonging to this exact epoch.
    /// </param>
    /// <param name="inclusive">
    /// Whether the first interval is due at <paramref name="epochMinute"/> rather than one cadence later.
    /// </param>
    /// <param name="advancePastElapsed">
    /// <see langword="true"/>, the default, returns the due minute after all elapsed intervals.
    /// <see langword="false"/> retains the first unconsumed due minute without advancing blocked recovery.
    /// </param>
    /// <returns>
    /// The elapsed ordinal, unconsumed interval count and next due minute; invalid or future consumption is rejected.
    /// </returns>
    internal static MortalWoundRecoveryCadenceResult Evaluate(
        long epochMinute, long cadence, long currentMinute, long consumed, bool inclusive,
        bool advancePastElapsed = true)
    {
        if (epochMinute < 0 || currentMinute < 0 || cadence <= 0 || consumed < 0)
            throw new InvalidDataException("Invalid canonical cadence coordinates.");
        var firstDue = inclusive ? epochMinute : checked(epochMinute + cadence);
        var total = currentMinute < firstDue ? 0 : checked(
            checked(currentMinute - epochMinute) / cadence + (inclusive ? 1 : 0));
        if (consumed > total)
            throw new InvalidDataException("Consumed cadence ordinal exceeds elapsed intervals.");
        var nextOrdinal = checked((advancePastElapsed ? total : consumed) + (inclusive ? 0 : 1));
        var next = checked(epochMinute + checked(nextOrdinal * cadence));
        return new(total, checked(total - consumed), next);
    }
}

/// <summary>
/// Retains the accepted scalar source coordinates used to reconstruct a recovery calculation.
/// </summary>
/// <param name="SessionGeneration">
/// The original canonical session generation.
/// </param>
/// <param name="BindingFingerprint">
/// The recomputed seal of the complete original accepted binding.
/// </param>
/// <param name="Context">
/// The detached original treatment-context coordinates used by accepted-state admission.
/// </param>
/// <param name="ContextFingerprint">
/// The recomputed context seal for the original session and binding.
/// </param>
/// <param name="AcceptedD20EventValues">
/// The immutable original accepted d20 values used to reconstruct the request-event source seal.
/// </param>
/// <param name="WoundSourcePath">
/// The original exact canonical wound source path.
/// </param>
/// <param name="WoundFingerprint">
/// The recomputed semantic seal of the complete source wound.
/// </param>
/// <param name="HistoryFingerprint">
/// The original complete history-prefix seal, checked against the surrounding durable history.
/// </param>
/// <param name="HistoryNextOrdinal">
/// The original global history next ordinal before the primary stage.
/// </param>
/// <param name="IdentityFingerprint">
/// The retained original accepted identity-index component seal.
/// </param>
/// <param name="ClockFingerprint">
/// The recomputed original canonical clock seal.
/// </param>
/// <param name="CurrentGameMinute">
/// The original signed accepted evaluation minute.
/// </param>
/// <param name="EffectFingerprint">
/// The retained original accepted effect-mechanics component seal.
/// </param>
/// <param name="ItemResourceFingerprint">
/// The retained original accepted item and resource component seal.
/// </param>
/// <param name="ActorLocationFingerprint">
/// The retained original accepted actor and location component seal.
/// </param>
/// <param name="PlayerCapabilityCatalogFingerprint">
/// The retained original accepted player capability component seal.
/// </param>
/// <param name="NpcCapabilityCatalogFingerprint">
/// The retained original accepted NPC capability component seal.
/// </param>
/// <param name="SkillSourceFingerprint">
/// The recomputed joint player and NPC capability seal.
/// </param>
/// <param name="RequirementSnapshotFingerprint">
/// The retained original accepted mechanical requirement snapshot seal.
/// </param>
/// <param name="AcceptedStateFingerprint">
/// The recomputed original accepted-state seal incorporating every retained component.
/// </param>
internal sealed record MortalWoundRecoverySourceEvidence(
    string SessionGeneration,
    string BindingFingerprint,
    MortalWoundTreatmentAuthority.Context Context,
    string ContextFingerprint,
    IReadOnlyList<int> AcceptedD20EventValues,
    string WoundSourcePath,
    string WoundFingerprint,
    string HistoryFingerprint,
    int HistoryNextOrdinal,
    string IdentityFingerprint,
    string ClockFingerprint,
    long CurrentGameMinute,
    string EffectFingerprint,
    string ItemResourceFingerprint,
    string ActorLocationFingerprint,
    string PlayerCapabilityCatalogFingerprint,
    string NpcCapabilityCatalogFingerprint,
    string SkillSourceFingerprint,
    string RequirementSnapshotFingerprint,
    string AcceptedStateFingerprint)
{
    /// <summary>
    /// Copies comparison evidence from a genuine accepted-state authority.
    /// </summary>
    /// <param name="acceptedState">
    /// The registry-owned source whose immutable coordinates are copied.
    /// </param>
    /// <returns>
    /// Detached scalar evidence that grants no publication authority.
    /// </returns>
    internal static MortalWoundRecoverySourceEvidence FromAcceptedState(
        MortalWoundTreatmentAcceptedStateAuthority acceptedState) => new(
        acceptedState.SessionGeneration, acceptedState.BindingFingerprint,
        acceptedState.RequirementContext, acceptedState.ContextFingerprint,
        Array.AsReadOnly(acceptedState.RecoveryAcceptedD20EventValues.ToArray()),
        acceptedState.WoundSourcePath, acceptedState.WoundFingerprint,
        acceptedState.HistoryFingerprint, acceptedState.History.NextOrdinal,
        acceptedState.IdentityFingerprint, acceptedState.ClockFingerprint,
        acceptedState.CurrentGameMinute, acceptedState.EffectFingerprint,
        acceptedState.ItemResourceFingerprint, acceptedState.ActorLocationFingerprint,
        acceptedState.PlayerCapabilityCatalogFingerprint, acceptedState.NpcCapabilityCatalogFingerprint,
        acceptedState.SkillSourceFingerprint, acceptedState.RequirementSnapshotFingerprint,
        acceptedState.AcceptedStateFingerprint);

}

/// <summary>
/// Retains consumption for one exact recovery or condition epoch.
/// </summary>
/// <param name="EpochKind">
/// The recovery anchor kind, or condition for a deterioration epoch.
/// </param>
/// <param name="ConditionKey">
/// The deterioration condition key, or null for a recovery epoch.
/// </param>
/// <param name="AnchorMinute">
/// The original canonical epoch minute.
/// </param>
/// <param name="AnchorTransitionId">
/// The exact accepted transition that allocated the epoch.
/// </param>
/// <param name="ConsumedBefore">
/// The ordinal already consumed before this evaluation.
/// </param>
/// <param name="ConsumedAfter">
/// The ordinal consumed after this evaluation.
/// </param>
internal sealed record MortalWoundRecoveryEpochConsumption(
    string EpochKind, string? ConditionKey, long AnchorMinute,
    string AnchorTransitionId, long ConsumedBefore, long ConsumedAfter);
