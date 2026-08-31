using System.Collections.ObjectModel;
using BookOfEternityClient.Core;

namespace BookOfEternityClient.Services;

internal sealed record MortalWoundDeteriorationPolicyAuthorityResult(
    bool IsValid,
    IReadOnlyList<ValidationIssue> Issues,
    MortalWoundDeteriorationPolicyAuthority? Authority);

/// <summary>
/// Registry-owned proof that one exact canonical Mortal deterioration policy is
/// structurally valid, currently applicable, and strictly worsening. The authority
/// carries no caller-authored policy or mutation surface.
/// </summary>
internal sealed class MortalWoundDeteriorationPolicyAuthority
{
    private const string AuthorityDomain =
        "book_of_eternity.mortal_wound.deterioration_policy_authority";
    private readonly MortalWoundDeteriorationPolicyDefinition _policy;

    private MortalWoundDeteriorationPolicyAuthority(
        MortalWoundDeteriorationPolicyDefinition policy,
        string authorityFingerprint)
    {
        _policy = policy;
        PolicyRef = policy.PolicyRef;
        Classification = policy.Classification;
        AuthorityFingerprint = authorityFingerprint;
    }

    public string PolicyRef { get; }
    public MortalWoundDeteriorationPolicyClassification Classification { get; }
    public string AuthorityFingerprint { get; }

    internal MortalWoundDeteriorationPolicyDefinition Policy => _policy;

    internal static MortalWoundDeteriorationPolicyAuthorityResult Create(
        FileSystemManager fileSystem,
        FileSystemManager.CanonicalWriteLease writeLease,
        WoundAcceptedTurnBinding binding,
        string woundId,
        string policyRef)
    {
        if (fileSystem is null || writeLease is null)
        {
            return InvalidAuthority(
                "recovery.deteriorationPolicy",
                "missing canonical file-system authority or write lease");
        }

        return AcceptedTurnAuthorityRegistry
            .CreateMortalWoundDeteriorationPolicyAuthority(
                fileSystem,
                writeLease,
                binding,
                woundId,
                policyRef);
    }

    internal static MortalWoundDeteriorationPolicyAuthorityResult Create(
        MortalWoundTreatmentAcceptedStateAuthority acceptedState,
        MortalWoundTreatmentAttemptCoordinates coordinates,
        string policyRef)
    {
        if (acceptedState is null)
        {
            return InvalidAuthority(
                "recovery.deteriorationPolicy",
                "missing accepted-state authority");
        }

        return acceptedState.CreateDeteriorationPolicyAuthority(
            coordinates,
            policyRef);
    }

    internal static MortalWoundDeteriorationPolicyAuthorityResult CreateRegistered(
        object capability,
        MortalWoundTreatmentAcceptedStateAuthority acceptedState,
        MortalWoundTreatmentAttemptCoordinates? coordinates,
        string? policyRef,
        string scope)
    {
        ArgumentNullException.ThrowIfNull(acceptedState);
        var path = acceptedState.WoundSourcePath + ".recovery.deteriorationPolicy";
        if (!AcceptedTurnAuthorityRegistry
                .IsDeteriorationPolicyAuthorityCapability(capability) ||
            scope is not ("recovery" or "treatment_interruption"))
        {
            return InvalidAuthority(
                path,
                "detached registry capability or unsupported authority scope");
        }

        var wound = acceptedState.CurrentWound;
        if (!string.Equals(wound.Lifecycle, "active", StringComparison.Ordinal) ||
            !string.Equals(wound.Owner.Realm, "mortal_world", StringComparison.Ordinal) ||
            !string.Equals(wound.Classification.Domain, "physical", StringComparison.Ordinal) ||
            wound.Recovery.DeteriorationPolicy is not { } policyElement)
        {
            return InvalidAuthority(
                path,
                "current active Mortal physical wound with one policy");
        }

        if (!ResourceMaterializationContract.IsExactIdentifier(policyRef))
        {
            return ReferenceMismatch(path + ".policyRef", policyRef ?? "missing");
        }

        var parsed = MortalWoundDeteriorationPolicyContract.Parse(
            policyElement,
            path,
            wound.Owner.Realm,
            WoundMaterializationContract.ResolveEffectTargetKind(wound.Owner.OwnerKind),
            wound.Severity.Rank);
        if (!parsed.IsValid || parsed.Policy is not { } policy)
        {
            return new MortalWoundDeteriorationPolicyAuthorityResult(
                false,
                new ReadOnlyCollection<ValidationIssue>(parsed.Issues.ToArray()),
                null);
        }

        if (!string.Equals(policy.PolicyRef, policyRef, StringComparison.Ordinal))
            return ReferenceMismatch(path + ".policyRef", policyRef!);

        if (!IsApplicableStrictWorseningPolicy(wound, policy))
            return NotStrictlyWorsening(path, policy.ResultKind.ToString());

        var authorityFingerprint = WoundAcceptedTurnFingerprintWriter.Compute(
            new string?[]
            {
                AuthorityDomain,
                "1",
                scope,
                acceptedState.SessionGeneration,
                acceptedState.AcceptedStateFingerprint,
                acceptedState.BindingFingerprint,
                acceptedState.WoundFingerprint,
                acceptedState.WoundSourcePath,
                policy.CanonicalProjection,
                policy.PolicyRef,
                policy.Classification.ToString(),
                coordinates?.CoordinatesFingerprint
            });
        return new MortalWoundDeteriorationPolicyAuthorityResult(
            true,
            Array.Empty<ValidationIssue>(),
            new MortalWoundDeteriorationPolicyAuthority(
                policy,
                authorityFingerprint));
    }

    internal static MortalWoundDeteriorationPolicyAuthorityResult InvalidAuthority(
        string path,
        string actual) => Failure(Issue(
        path,
        "mortal_wound_deterioration_policy_authority_invalid",
        "one registry-current accepted-state authority for the exact wound and policy",
        actual));

    private static bool IsApplicableStrictWorseningPolicy(
        WoundMaterializationEnvelope wound,
        MortalWoundDeteriorationPolicyDefinition policy)
    {
        if (policy.Classification !=
            MortalWoundDeteriorationPolicyClassification.StrictlyWorsening)
        {
            return false;
        }

        return policy.ResultKind switch
        {
            MortalWoundDeteriorationResultKind.IncreaseSeverity =>
                wound.Severity.Rank is >= 1 and < 4,
            MortalWoundDeteriorationResultKind.DeathContour => true,
            MortalWoundDeteriorationResultKind.AddComplication =>
                CanAcceptComplication(wound, policy),
            _ => false
        };
    }

    private static bool CanAcceptComplication(
        WoundMaterializationEnvelope wound,
        MortalWoundDeteriorationPolicyDefinition policy)
    {
        try
        {
            var resultingComplications = checked(wound.Complications.Count + 1);
            var resultingSlots = checked(
                wound.Consequences.SlotsUsed + policy.AdditionalConsequenceSlots);
            var resultingDefinitions = checked(
                wound.Consequences.OwnedEffectSources.Definitions.Count +
                policy.AdditionalDefinitions);
            var resultingRoots = checked(
                wound.Consequences.OwnedEffectSources.RootBindings.Count +
                policy.AdditionalRoots);
            return resultingComplications <= WoundMaterializationContract.MaxComplications &&
                   resultingSlots <= wound.Consequences.SlotBudget &&
                   resultingSlots <= WoundMaterializationContract.MaxConsequences &&
                   resultingDefinitions <=
                        WoundMaterializationContract.MaxOwnedEffectDefinitions &&
                   resultingRoots <=
                       WoundMaterializationContract.MaxOwnedEffectRootBindings;
        }
        catch (OverflowException)
        {
            return false;
        }
    }

    private static MortalWoundDeteriorationPolicyAuthorityResult ReferenceMismatch(
        string path,
        string actual) => Failure(Issue(
        path,
        "mortal_wound_deterioration_policy_reference_mismatch",
        "the exact policyRef parsed from the current canonical wound",
        actual));

    private static MortalWoundDeteriorationPolicyAuthorityResult
        NotStrictlyWorsening(string path, string actual) => Failure(Issue(
        path,
        "mortal_wound_deterioration_policy_not_strictly_worsening",
        "one currently applicable strictly-worsening policy result",
        actual));

    private static MortalWoundDeteriorationPolicyAuthorityResult Failure(
        ValidationIssue issue) => new(
        false,
        new ReadOnlyCollection<ValidationIssue>(new[] { issue }),
        null);

    private static ValidationIssue Issue(
        string path,
        string code,
        string expected,
        string actual) => new(
        path,
        IssueSeverity.Error,
        "The Mortal wound deterioration policy cannot grant current authority.",
        code: code,
        actor: "Client",
        section: "wound_materialization",
        expected: expected,
        actual: actual,
        repairHint:
        "Re-export the current wound state and use its exact closed, applicable, strictly-worsening policy.");
}
