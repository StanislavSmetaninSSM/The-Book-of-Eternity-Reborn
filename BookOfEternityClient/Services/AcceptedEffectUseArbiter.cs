namespace BookOfEternityClient.Services;

internal sealed record CanonicalEffectUseSeed(
    string EffectId,
    int RemainingUses);

internal sealed record EffectActivationCandidateIdentity(
    string EffectId,
    string TriggerId,
    string EventKind,
    string EventRef,
    string TriggerEventRef);

// Output cardinality is intentionally absent. Mutations, reactions, and
// pending records share this one activation identity and cannot spend uses
// independently.
internal sealed record EffectActivationCandidate(
    EffectActivationCandidateIdentity Identity,
    int Priority,
    bool ConsumesUse,
    ResourcePendingAuthorityBinding EffectAuthority);

internal sealed record AcceptedEffectActivationTranscriptStamp(
    EffectActivationCandidateIdentity Identity,
    ResourcePendingAuthorityBinding EffectAuthority,
    int Priority,
    bool ConsumesUse,
    int? UsesBefore,
    long ActivationOrdinal);

internal sealed record AcceptedEffectActivation(
    AcceptedEffectActivationTranscriptStamp Stamp,
    int? UsesAfter,
    bool EffectTerminal);

internal enum EffectActivationRejectionReason
{
    EffectTerminal
}

internal sealed record RejectedEffectActivation(
    EffectActivationCandidateIdentity Identity,
    EffectActivationRejectionReason Reason);

internal sealed record AcceptedEffectUseArbiterIssue(
    string Code,
    string Message);

internal static class AcceptedEffectUseArbiterIssueCodes
{
    internal const string DuplicateActivation =
        "effect_activation_duplicate";
    internal const string DuplicateUseSeed =
        "effect_use_seed_duplicate";
    internal const string InvalidUseSeed =
        "effect_use_seed_invalid";
    internal const string InvalidActivationIdentity =
        "effect_activation_identity_invalid";
    internal const string InvalidTranscriptStamp =
        "effect_activation_transcript_stamp_invalid";
    internal const string MissingUseSeed =
        "effect_activation_use_seed_missing";
    internal const string ReplayBudgetMismatch =
        "effect_activation_replay_budget_mismatch";
    internal const string ReplayAfterTerminal =
        "effect_activation_replay_after_terminal";
    internal const string ReplayOrdinalMismatch =
        "effect_activation_replay_ordinal_mismatch";
}

internal sealed record AcceptedEffectUseArbiterInitializationResult(
    AcceptedEffectUseArbiter? Arbiter,
    IReadOnlyList<AcceptedEffectUseArbiterIssue> Issues)
{
    internal bool IsValid => Arbiter is not null && Issues.Count == 0;
}

internal sealed record AcceptedEffectUseArbitrationResult(
    IReadOnlyList<AcceptedEffectActivation> AcceptedActivations,
    IReadOnlyList<RejectedEffectActivation> RejectedEvidence,
    IReadOnlyList<AcceptedEffectUseArbiterIssue> Issues)
{
    internal bool IsValid => Issues.Count == 0;
}

internal sealed class AcceptedEffectUseArbiter
{
    private sealed record ActivationDuplicateKey(
        ResourcePendingAuthorityBinding EffectAuthority,
        string TriggerId,
        string EventKind,
        string EventRef,
        string TriggerEventRef);

    private readonly Dictionary<string, int> _remainingUses;
    private readonly HashSet<ActivationDuplicateKey> _seenActivations;
    private readonly List<AcceptedEffectActivationTranscriptStamp>
        _acceptedTranscript;
    private long _nextActivationOrdinal;

    private AcceptedEffectUseArbiter(
        Dictionary<string, int> remainingUses,
        HashSet<ActivationDuplicateKey> seenActivations,
        List<AcceptedEffectActivationTranscriptStamp> acceptedTranscript,
        long nextActivationOrdinal)
    {
        _remainingUses = remainingUses;
        _seenActivations = seenActivations;
        _acceptedTranscript = acceptedTranscript;
        _nextActivationOrdinal = nextActivationOrdinal;
    }

    internal IReadOnlyList<AcceptedEffectActivationTranscriptStamp>
        AcceptedTranscript => ReadOnly(_acceptedTranscript.ToArray());

    internal static AcceptedEffectUseArbiterInitializationResult Initialize(
        IReadOnlyList<CanonicalEffectUseSeed> seeds,
        IReadOnlyList<AcceptedEffectActivationTranscriptStamp>? acceptedReplay = null)
    {
        if (seeds is null)
        {
            return InitializationFailure(
                AcceptedEffectUseArbiterIssueCodes.InvalidUseSeed,
                "Canonical effect-use seeds are required, even when the collection is empty.");
        }

        var issues = new List<AcceptedEffectUseArbiterIssue>();
        var remainingUses = new Dictionary<string, int>(StringComparer.Ordinal);
        foreach (var seed in seeds)
        {
            if (seed is null || !IsExactToken(seed.EffectId) || seed.RemainingUses < 0)
            {
                issues.Add(Issue(
                    AcceptedEffectUseArbiterIssueCodes.InvalidUseSeed,
                    "Every canonical effect-use seed requires an exact effect id and a non-negative remaining-use count."));
                continue;
            }

            if (!remainingUses.TryAdd(seed.EffectId, seed.RemainingUses))
            {
                issues.Add(Issue(
                    AcceptedEffectUseArbiterIssueCodes.DuplicateUseSeed,
                    $"Effect '{seed.EffectId}' has more than one canonical use seed."));
            }
        }

        var replay = acceptedReplay?.ToArray() ??
                     Array.Empty<AcceptedEffectActivationTranscriptStamp>();
        var replayIdentities = new HashSet<ActivationDuplicateKey>();
        for (var index = 0; index < replay.Length; index++)
        {
            var stamp = replay[index];
            if (stamp is null ||
                !IsValidIdentity(stamp.Identity) ||
                !IsValidEffectAuthority(
                    stamp.Identity.EffectId,
                    stamp.EffectAuthority))
            {
                issues.Add(Issue(
                    AcceptedEffectUseArbiterIssueCodes.InvalidActivationIdentity,
                    "Every replayed activation requires an exact activation identity and typed effect authority."));
                continue;
            }

            if (stamp.ActivationOrdinal != index)
            {
                issues.Add(Issue(
                    AcceptedEffectUseArbiterIssueCodes.ReplayOrdinalMismatch,
                    $"Replayed activation '{Describe(stamp.Identity)}' has ordinal " +
                    $"{stamp.ActivationOrdinal}, expected {index}."));
            }

            if (!replayIdentities.Add(CreateDuplicateKey(
                    stamp.Identity,
                    stamp.EffectAuthority)))
            {
                issues.Add(Issue(
                    AcceptedEffectUseArbiterIssueCodes.DuplicateActivation,
                    $"Activation '{Describe(stamp.Identity)}' occurs more than once in the accepted replay transcript."));
            }

            if (stamp.ConsumesUse)
            {
                if (stamp.UsesBefore is not > 0)
                {
                    issues.Add(Issue(
                        AcceptedEffectUseArbiterIssueCodes.InvalidTranscriptStamp,
                        $"Consuming activation '{Describe(stamp.Identity)}' must record a positive UsesBefore value."));
                }
            }
            else if (stamp.UsesBefore.HasValue)
            {
                issues.Add(Issue(
                    AcceptedEffectUseArbiterIssueCodes.InvalidTranscriptStamp,
                    $"Non-consuming activation '{Describe(stamp.Identity)}' cannot record UsesBefore."));
            }
        }

        if (issues.Count != 0)
        {
            return new AcceptedEffectUseArbiterInitializationResult(
                null,
                ReadOnly(issues.ToArray()));
        }

        var arbiter = new AcceptedEffectUseArbiter(
            remainingUses,
            new HashSet<ActivationDuplicateKey>(),
            new List<AcceptedEffectActivationTranscriptStamp>(),
            nextActivationOrdinal: 0);
        foreach (var stamp in replay)
        {
            if (arbiter.IsTerminal(stamp.Identity.EffectId))
            {
                return InitializationFailure(
                    AcceptedEffectUseArbiterIssueCodes.ReplayAfterTerminal,
                    $"Accepted replay activation '{Describe(stamp.Identity)}' occurs after its effect became terminal.");
            }

            if (stamp.ConsumesUse)
            {
                if (!arbiter._remainingUses.TryGetValue(
                        stamp.Identity.EffectId,
                        out var remaining))
                {
                    return InitializationFailure(
                        AcceptedEffectUseArbiterIssueCodes.MissingUseSeed,
                        $"Consuming replay activation '{Describe(stamp.Identity)}' has no canonical effect-use seed.");
                }

                if (remaining != stamp.UsesBefore)
                {
                    return InitializationFailure(
                        AcceptedEffectUseArbiterIssueCodes.ReplayBudgetMismatch,
                        $"Consuming replay activation '{Describe(stamp.Identity)}' records UsesBefore={stamp.UsesBefore}, expected exactly {remaining}.");
                }

                arbiter._remainingUses[stamp.Identity.EffectId] = remaining - 1;
            }

            arbiter._seenActivations.Add(CreateDuplicateKey(
                stamp.Identity,
                stamp.EffectAuthority));
            arbiter._acceptedTranscript.Add(stamp);
            arbiter._nextActivationOrdinal++;
        }

        return new AcceptedEffectUseArbiterInitializationResult(
            arbiter,
            ReadOnly(Array.Empty<AcceptedEffectUseArbiterIssue>()));
    }

    /// <summary>
    /// Adds budgets for newly registered instances without replaying or resetting existing activations.
    /// The owning routing registry must authenticate the instances, including those without use budgets.
    /// This operation only validates and stores the supplied arithmetic seeds.
    /// </summary>
    /// <param name="seeds">
    /// New exact effect identifiers with non-negative budgets. An empty collection makes no change;
    /// a <see langword="null"/> collection, invalid seed or previously known identifier rejects the entire batch.
    /// </param>
    /// <returns>
    /// An empty list after registration, or validation issues with all arbiter state preserved.
    /// </returns>
    internal IReadOnlyList<AcceptedEffectUseArbiterIssue> RegisterNewSeeds(
        IReadOnlyList<CanonicalEffectUseSeed> seeds)
    {
        if (seeds is null)
            return ReadOnly(new[] { Issue(AcceptedEffectUseArbiterIssueCodes.InvalidUseSeed,
                "A new-instance seed collection is required.") });
        var additions = new Dictionary<string, int>(StringComparer.Ordinal);
        var issues = new List<AcceptedEffectUseArbiterIssue>();
        var activatedIds = _acceptedTranscript.Select(stamp => stamp.Identity.EffectId)
            .ToHashSet(StringComparer.Ordinal);
        foreach (var seed in seeds)
        {
            if (seed is null || !IsExactToken(seed.EffectId) || seed.RemainingUses < 0)
            {
                issues.Add(Issue(AcceptedEffectUseArbiterIssueCodes.InvalidUseSeed,
                    "Every new seed requires an exact effect id and a non-negative budget."));
                continue;
            }
            if (_remainingUses.ContainsKey(seed.EffectId) || activatedIds.Contains(seed.EffectId) ||
                !additions.TryAdd(seed.EffectId, seed.RemainingUses))
                issues.Add(Issue(AcceptedEffectUseArbiterIssueCodes.DuplicateUseSeed,
                    $"Effect '{seed.EffectId}' is already known or occurs twice in the new seed batch."));
        }
        if (issues.Count != 0)
            return ReadOnly(issues.ToArray());
        foreach (var addition in additions)
            _remainingUses.Add(addition.Key, addition.Value);
        return ReadOnly(Array.Empty<AcceptedEffectUseArbiterIssue>());
    }

    internal AcceptedEffectUseArbitrationResult Arbitrate(
        IReadOnlyList<EffectActivationCandidate> candidates)
    {
        if (candidates is null)
        {
            return ArbitrationFailure(
                AcceptedEffectUseArbiterIssueCodes.InvalidActivationIdentity,
                "An activation-candidate collection is required.");
        }

        var candidateArray = candidates.ToArray();
        var issues = new List<AcceptedEffectUseArbiterIssue>();
        var batchIdentities = new HashSet<ActivationDuplicateKey>();
        foreach (var candidate in candidateArray)
        {
            if (candidate is null || !IsValidIdentity(candidate.Identity))
            {
                issues.Add(Issue(
                    AcceptedEffectUseArbiterIssueCodes.InvalidActivationIdentity,
                    "Every activation candidate requires an exact activation identity."));
                continue;
            }
            if (!IsValidEffectAuthority(
                    candidate.Identity.EffectId,
                    candidate.EffectAuthority))
            {
                issues.Add(Issue(
                    AcceptedEffectUseArbiterIssueCodes.InvalidActivationIdentity,
                    "An activation effect authority must be accepted_application, or permanent with an authority id equal to the effect id."));
                continue;
            }

            var duplicateKey = CreateDuplicateKey(
                candidate.Identity,
                candidate.EffectAuthority);
            if (!batchIdentities.Add(duplicateKey) ||
                _seenActivations.Contains(duplicateKey))
            {
                issues.Add(Issue(
                    AcceptedEffectUseArbiterIssueCodes.DuplicateActivation,
                    $"Activation '{Describe(candidate.Identity)}' was submitted more than once."));
            }

            if (candidate.ConsumesUse &&
                !_remainingUses.ContainsKey(candidate.Identity.EffectId))
            {
                issues.Add(Issue(
                    AcceptedEffectUseArbiterIssueCodes.MissingUseSeed,
                    $"Consuming activation '{Describe(candidate.Identity)}' has no canonical effect-use seed."));
            }
        }

        if (issues.Count != 0)
        {
            return new AcceptedEffectUseArbitrationResult(
                ReadOnly(Array.Empty<AcceptedEffectActivation>()),
                ReadOnly(Array.Empty<RejectedEffectActivation>()),
                ReadOnly(issues.ToArray()));
        }

        var ordered = candidateArray
            .OrderBy(static candidate => candidate.Priority)
            .ThenBy(
                static candidate => candidate.Identity.TriggerId,
                StringComparer.Ordinal)
            .ThenBy(
                static candidate => candidate.EffectAuthority.BindingKind,
                StringComparer.Ordinal)
            .ThenBy(
                static candidate => candidate.EffectAuthority.AuthorityId,
                StringComparer.Ordinal)
            .ThenBy(
                static candidate => candidate.Identity.EventKind,
                StringComparer.Ordinal)
            .ThenBy(
                static candidate => candidate.Identity.EventRef,
                StringComparer.Ordinal)
            .ThenBy(
                static candidate => candidate.Identity.TriggerEventRef,
                StringComparer.Ordinal)
            .ToArray();
        var accepted = new List<AcceptedEffectActivation>(ordered.Length);
        var rejected = new List<RejectedEffectActivation>();
        foreach (var candidate in ordered)
        {
            _seenActivations.Add(CreateDuplicateKey(
                candidate.Identity,
                candidate.EffectAuthority));
            if (IsTerminal(candidate.Identity.EffectId))
            {
                rejected.Add(new RejectedEffectActivation(
                    candidate.Identity,
                    EffectActivationRejectionReason.EffectTerminal));
                continue;
            }

            int? usesBefore = null;
            int? usesAfter = null;
            var terminal = false;
            if (candidate.ConsumesUse)
            {
                usesBefore = _remainingUses[candidate.Identity.EffectId];
                usesAfter = usesBefore.Value - 1;
                _remainingUses[candidate.Identity.EffectId] = usesAfter.Value;
                terminal = usesAfter == 0;
            }

            var stamp = new AcceptedEffectActivationTranscriptStamp(
                candidate.Identity,
                candidate.EffectAuthority,
                candidate.Priority,
                candidate.ConsumesUse,
                usesBefore,
                _nextActivationOrdinal++);
            _acceptedTranscript.Add(stamp);
            accepted.Add(new AcceptedEffectActivation(
                stamp,
                usesAfter,
                terminal));
        }

        return new AcceptedEffectUseArbitrationResult(
            ReadOnly(accepted.ToArray()),
            ReadOnly(rejected.ToArray()),
            ReadOnly(Array.Empty<AcceptedEffectUseArbiterIssue>()));
    }

    private static ActivationDuplicateKey CreateDuplicateKey(
        EffectActivationCandidateIdentity identity,
        ResourcePendingAuthorityBinding effectAuthority) =>
        new(
            effectAuthority,
            identity.TriggerId,
            identity.EventKind,
            identity.EventRef,
            identity.TriggerEventRef);

    private static bool IsValidEffectAuthority(
        string effectId,
        ResourcePendingAuthorityBinding? authority) =>
        authority is not null &&
        ResourceMaterializationContract.IsExactIdentifier(effectId) &&
        ResourceMaterializationContract.IsExactIdentifier(
            authority.BindingKind) &&
        ResourceMaterializationContract.IsExactIdentifier(
            authority.AuthorityId) &&
        (string.Equals(
             authority.BindingKind,
             "accepted_application",
             StringComparison.Ordinal) ||
         string.Equals(
             authority.BindingKind,
             "permanent",
             StringComparison.Ordinal) &&
         string.Equals(
             authority.AuthorityId,
             effectId,
             StringComparison.Ordinal));

    internal bool TryGetRemainingUses(string effectId, out int remainingUses)
    {
        if (effectId is null)
        {
            remainingUses = default;
            return false;
        }

        return _remainingUses.TryGetValue(effectId, out remainingUses);
    }

    private bool IsTerminal(string effectId) =>
        _remainingUses.TryGetValue(effectId, out var remaining) && remaining == 0;

    private static bool IsValidIdentity(EffectActivationCandidateIdentity? identity) =>
        identity is not null &&
        IsExactToken(identity.EffectId) &&
        IsExactToken(identity.TriggerId) &&
        IsExactToken(identity.EventKind) &&
        IsExactToken(identity.EventRef) &&
        IsExactToken(identity.TriggerEventRef);

    private static bool IsExactToken(string? value) =>
        ResourceMaterializationContract.IsExactIdentifier(value);

    private static string Describe(EffectActivationCandidateIdentity identity) =>
        string.Join(
            "/",
            identity.EffectId,
            identity.TriggerId,
            identity.EventKind,
            identity.EventRef,
            identity.TriggerEventRef);

    private static AcceptedEffectUseArbiterInitializationResult
        InitializationFailure(string code, string message) =>
        new(
            null,
            ReadOnly(new[] { Issue(code, message) }));

    private static AcceptedEffectUseArbitrationResult ArbitrationFailure(
        string code,
        string message) =>
        new(
            ReadOnly(Array.Empty<AcceptedEffectActivation>()),
            ReadOnly(Array.Empty<RejectedEffectActivation>()),
            ReadOnly(new[] { Issue(code, message) }));

    private static IReadOnlyList<T> ReadOnly<T>(T[] values) =>
        Array.AsReadOnly(values);

    private static AcceptedEffectUseArbiterIssue Issue(
        string code,
        string message) =>
        new(code, message);
}
