using System.Collections.ObjectModel;
using System.Globalization;
using System.Text.Json;

namespace BookOfEternityClient.Services;

internal sealed partial class MortalWoundProcedureCheckAuthorityResult
{
    private readonly ReadOnlyCollection<ValidationIssue> _issues;

    private MortalWoundProcedureCheckAuthorityResult(
        bool isValid,
        IEnumerable<ValidationIssue> issues,
        MortalWoundProcedureCheckAuthority? authority)
    {
        IsValid = isValid;
        _issues = MortalWoundTreatmentShellDetachment.Freeze(issues);
        Authority = authority;
    }

    public bool IsValid { get; }
    public IReadOnlyList<ValidationIssue> Issues => _issues;
    public MortalWoundProcedureCheckAuthority? Authority { get; }

    internal static MortalWoundProcedureCheckAuthorityResult Invalid(
        ValidationIssue issue)
    {
        ArgumentNullException.ThrowIfNull(issue);
        return new MortalWoundProcedureCheckAuthorityResult(
            false,
            new[] { issue },
            null);
    }

    internal static MortalWoundProcedureCheckAuthorityResult Invalid(
        IEnumerable<ValidationIssue> issues) => new(false, issues, null);

    internal static MortalWoundProcedureCheckAuthorityResult Valid(
        MortalWoundProcedureCheckAuthority authority)
    {
        ArgumentNullException.ThrowIfNull(authority);
        return new MortalWoundProcedureCheckAuthorityResult(
            true,
            Array.Empty<ValidationIssue>(),
            authority);
    }
}

internal sealed partial class MortalWoundProcedureCheckAuthority :
    MortalWoundTreatmentModeAuthority
{
    private static readonly object ProcedureReservationCapability = new();
    private readonly ReadOnlyCollection<MortalWoundProcedureRollContribution>
        _rollContributions;
    private readonly ReadOnlyCollection<int> _sourceIndices;
    private readonly ReadOnlyCollection<int> _sourceRolls;
    private readonly MortalWoundProcedureDiceReservation _diceReservation;
    private readonly MortalWoundCriticalReactionReservation?
        _criticalReactionReservation;
    private readonly MortalWoundCriticalReactionReservationAgreement?
        _criticalReactionAgreement;

    internal static bool IsProcedureReservationCapability(object capability) =>
        ReferenceEquals(capability, ProcedureReservationCapability);

    private MortalWoundProcedureCheckAuthority(
        string sourcePath,
        string rollMode,
        string rollActorKind,
        string rollActorId,
        IEnumerable<MortalWoundProcedureRollContribution> rollContributions,
        IEnumerable<int> sourceIndices,
        IEnumerable<int> sourceRolls,
        int selectedSourceIndex,
        int naturalRoll,
        int modifier,
        int complicationDifficultyModifier,
        int effectiveDifficulty,
        string requirementAuthorityFingerprint,
        string coordinatesFingerprint,
        string acceptedStateFingerprint,
        MortalWoundPreparedCriticalReaction? preparedCriticalReaction,
        string authorityFingerprint,
        MortalWoundProcedureDiceReservation diceReservation,
        MortalWoundCriticalReactionReservation? criticalReactionReservation,
        MortalWoundCriticalReactionReservationAgreement? criticalReactionAgreement)
    {
        SourcePath = sourcePath;
        RollMode = rollMode;
        RollActorKind = rollActorKind;
        RollActorId = rollActorId;
        _rollContributions = MortalWoundTreatmentShellDetachment.Freeze(rollContributions);
        _sourceIndices = MortalWoundTreatmentShellDetachment.Freeze(sourceIndices);
        _sourceRolls = MortalWoundTreatmentShellDetachment.Freeze(sourceRolls);
        SelectedSourceIndex = selectedSourceIndex;
        NaturalRoll = naturalRoll;
        Modifier = modifier;
        ComplicationDifficultyModifier = complicationDifficultyModifier;
        EffectiveDifficulty = effectiveDifficulty;
        RequirementAuthorityFingerprint = requirementAuthorityFingerprint;
        CoordinatesFingerprint = coordinatesFingerprint;
        AcceptedStateFingerprint = acceptedStateFingerprint;
        PreparedCriticalReaction = preparedCriticalReaction;
        AuthorityFingerprint = authorityFingerprint;
        _diceReservation = diceReservation;
        _criticalReactionReservation = criticalReactionReservation;
        _criticalReactionAgreement = criticalReactionAgreement;
    }

    public string SourcePath { get; }
    public string RollMode { get; }
    public string RollActorKind { get; }
    public string RollActorId { get; }
    public IReadOnlyList<MortalWoundProcedureRollContribution> RollContributions =>
        _rollContributions;
    public IReadOnlyList<int> SourceIndices => _sourceIndices;
    public IReadOnlyList<int> SourceRolls => _sourceRolls;
    public int SelectedSourceIndex { get; }
    public int NaturalRoll { get; }
    public int Modifier { get; }
    public int ComplicationDifficultyModifier { get; }
    public int EffectiveDifficulty { get; }
    public string RequirementAuthorityFingerprint { get; }
    public string CoordinatesFingerprint { get; }
    public string AcceptedStateFingerprint { get; }
    public MortalWoundPreparedCriticalReaction? PreparedCriticalReaction { get; }
    public string AuthorityFingerprint { get; }

    internal static MortalWoundProcedureCheckAuthorityResult Create(
        MortalWoundTreatmentAttemptCoordinates? coordinates,
        MortalWoundProcedureRouteDefinition? route,
        WoundMaterializationEnvelope? before,
        MortalWoundTreatmentRequirementAuthorityBundle? requirementAuthority,
        MortalWoundTreatmentAcceptedStateAuthority? acceptedState)
    {
        if (coordinates is null ||
            route is null ||
            before is null ||
            requirementAuthority is null ||
            acceptedState is null ||
            !acceptedState.HasCurrentAdmissionAuthority() ||
            !coordinates.MatchesAcceptedState(acceptedState) ||
            !acceptedState.MatchesCurrentWound(before))
        {
            return Invalid(
                "treatmentAttempt.procedureCheck",
                "mortal_wound_treatment_procedure_authority_invalid",
                "one current accepted state with exact coordinates, route, and wound",
                "missing, stale, foreign, or mismatched authority");
        }

        var currentRoutes = acceptedState.TreatmentDefinition.Routes
            .Where(candidate => string.Equals(
                candidate.RouteId,
                coordinates.RouteId,
                StringComparison.Ordinal))
            .ToArray();
        if (currentRoutes.Length != 1 ||
            currentRoutes[0] is not MortalWoundProcedureRouteDefinition currentRoute ||
            !currentRoute.Equals(route) ||
            !string.Equals(route.RouteId, coordinates.RouteId, StringComparison.Ordinal))
        {
            return Invalid(
                "treatmentAttempt.procedureCheck.routeId",
                "mortal_wound_treatment_procedure_route_invalid",
                "the exact current typed procedure route",
                coordinates.RouteId);
        }

        string routeFingerprint;
        try
        {
            routeFingerprint = MortalWoundTreatmentRouteFingerprint.Compute(
                before,
                route.RouteId);
        }
        catch (Exception exception) when (exception is ArgumentException or
                                           InvalidOperationException or
                                           JsonException)
        {
            return Invalid(
                "treatmentAttempt.procedureCheck.routeId",
                "mortal_wound_treatment_procedure_route_invalid",
                "one canonical route with a recomputable fingerprint",
                exception.GetType().Name);
        }

        if (!TryValidateRequirementAuthority(
                coordinates,
                route,
                requirementAuthority,
                routeFingerprint,
                out var commonScope))
        {
            return Invalid(
                "treatmentAttempt.procedureCheck.requirements",
                "mortal_wound_treatment_procedure_requirement_authority_invalid",
                "one satisfied current procedure requirement authority",
                requirementAuthority.AuthorityFingerprint);
        }

        if (!TryResolveModifier(
                coordinates,
                route,
                commonScope!,
                out var modifier,
                out var rollActorKind,
                out var rollActorId))
        {
            return Invalid(
                "treatmentAttempt.procedureCheck.modifierSource",
                "mortal_wound_treatment_procedure_modifier_source_invalid",
                "fixed zero/provider or one exact satisfied skill-tier binding",
                route.Resolution.ModifierSource.Kind);
        }

        int complicationDifficultyModifier;
        int effectiveDifficulty;
        try
        {
            complicationDifficultyModifier = before.Complications
                .Where(static complication => string.Equals(
                    complication.State,
                    "active",
                    StringComparison.Ordinal))
                .Aggregate(0, static (sum, complication) => checked(
                    sum + complication.TreatmentDifficultyModifier));
            effectiveDifficulty = checked(
                route.Resolution.Difficulty + complicationDifficultyModifier);
        }
        catch (OverflowException)
        {
            return Invalid(
                "treatmentAttempt.procedureCheck.effectiveDifficulty",
                "mortal_wound_treatment_procedure_difficulty_overflow",
                "one signed-32-bit checked effective difficulty",
                "overflow");
        }

        if (!TryComposeRollContributions(
                acceptedState,
                coordinates.Realm,
                rollActorKind!,
                rollActorId!,
                out var rollContributions,
                out var rollMode))
        {
            return Invalid(
                "treatmentAttempt.procedureCheck.rollContributions",
                "mortal_wound_treatment_procedure_roll_effect_invalid",
                "accepted roll-actor roll_modifier(skill_check) evidence",
                "malformed accepted component projection");
        }

        MortalWoundProcedureDiceReservation? reservation = null;
        MortalWoundCriticalReactionReservation? criticalReactionReservation = null;
        MortalWoundCriticalReactionReservationAgreement? criticalReactionAgreement = null;
        MortalWoundProcedureReservationOwnership? reservationOwnership = null;
        var transferred = false;
        try
        {
            var reservationResult = acceptedState.ReserveProcedureReservations(
                ProcedureReservationCapability,
                coordinates,
                rollMode!,
                rollActorKind!,
                rollActorId!);
            if (!reservationResult.IsValid ||
                reservationResult.DiceReservation is null)
                return MortalWoundProcedureCheckAuthorityResult.Invalid(
                    reservationResult.Issues);
            reservation = reservationResult.DiceReservation;
            criticalReactionReservation =
                reservationResult.CriticalReactionReservation;
            criticalReactionAgreement =
                reservationResult.CriticalReactionAgreement;
            reservationOwnership = reservationResult.Ownership;
            if (reservationOwnership is null)
                throw new InvalidOperationException(
                    "A valid procedure reservation requires ownership provenance.");

            var selectedOrdinal = SelectSourceOrdinal(
                reservation.SourceRolls,
                rollMode!);
            var selectedSourceIndex = reservation.SourceIndices[selectedOrdinal];
            var naturalRoll = reservation.SourceRolls[selectedOrdinal];
            var preparedCriticalReaction = criticalReactionReservation is null
                ? null
                : MortalWoundPreparedCriticalReaction.Create(
                    criticalReactionReservation);
            var authorityFingerprint = ComputeAuthorityFingerprint(
                LiveTurnPreparationService.TurnRequestPath,
                rollMode!,
                rollActorKind!,
                rollActorId!,
                rollContributions!,
                reservation.SourceIndices,
                reservation.SourceRolls,
                selectedSourceIndex,
                naturalRoll,
                modifier,
                complicationDifficultyModifier,
                effectiveDifficulty,
                requirementAuthority.AuthorityFingerprint,
                coordinates.CoordinatesFingerprint,
                coordinates.AcceptedStateFingerprint,
                preparedCriticalReaction?.PreparedReactionFingerprint);
            var authority = new MortalWoundProcedureCheckAuthority(
                LiveTurnPreparationService.TurnRequestPath,
                rollMode!,
                rollActorKind!,
                rollActorId!,
                rollContributions!,
                reservation.SourceIndices,
                reservation.SourceRolls,
                selectedSourceIndex,
                naturalRoll,
                modifier,
                complicationDifficultyModifier,
                effectiveDifficulty,
                requirementAuthority.AuthorityFingerprint,
                coordinates.CoordinatesFingerprint,
                coordinates.AcceptedStateFingerprint,
                preparedCriticalReaction,
                authorityFingerprint,
                reservation,
                criticalReactionReservation,
                criticalReactionAgreement);
            transferred = true;
            return MortalWoundProcedureCheckAuthorityResult.Valid(authority);
        }
        catch (Exception exception) when (exception is ArgumentException or
                                           InvalidOperationException or
                                           OverflowException)
        {
            return Invalid(
                "treatmentAttempt.procedureCheck",
                "mortal_wound_treatment_procedure_authority_invalid",
                "one complete sealed procedure-check authority",
                exception.GetType().Name);
        }
        finally
        {
            if (!transferred &&
                reservation is not null &&
                reservationOwnership is not null)
            {
                acceptedState.RollbackNewProcedureReservations(
                    reservation,
                    criticalReactionReservation,
                    criticalReactionAgreement,
                    reservationOwnership);
            }
        }
    }

    internal bool ReleaseProvisionalReservations(
        MortalWoundTreatmentAcceptedStateAuthority acceptedState) =>
        acceptedState is not null && acceptedState.ReleaseProcedureReservations(
            _diceReservation,
            _criticalReactionReservation,
            _criticalReactionAgreement);

    internal bool HasLiveReservationAgreement(
        object liveCheckCapability,
        MortalWoundProcedureDiceReservationRegistry diceRegistry,
        MortalWoundCriticalReactionReservationRegistry criticalReactionRegistry)
    {
        ArgumentNullException.ThrowIfNull(diceRegistry);
        ArgumentNullException.ThrowIfNull(criticalReactionRegistry);
        return AcceptedTurnAuthorityRegistry.IsProcedureReservationLiveCheckCapability(
                   liveCheckCapability) &&
               diceRegistry.CanRelease(_diceReservation) &&
               criticalReactionRegistry.MatchesReleaseAgreement(
                   _diceReservation,
                   _criticalReactionReservation,
                   _criticalReactionAgreement);
    }

    private static bool TryValidateRequirementAuthority(
        MortalWoundTreatmentAttemptCoordinates coordinates,
        MortalWoundProcedureRouteDefinition route,
        MortalWoundTreatmentRequirementAuthorityBundle requirementAuthority,
        string routeFingerprint,
        out MortalWoundTreatmentRequirementScopeAuthority? commonScope)
    {
        commonScope = null;
        if (!string.Equals(requirementAuthority.Mode, "procedure", StringComparison.Ordinal) ||
            !string.Equals(
                requirementAuthority.ContextFingerprint,
                coordinates.ContextFingerprint,
                StringComparison.Ordinal) ||
            !string.Equals(
                requirementAuthority.AcceptedStateFingerprint,
                coordinates.AcceptedStateFingerprint,
                StringComparison.Ordinal) ||
            !string.Equals(
                requirementAuthority.RouteFingerprint,
                routeFingerprint,
                StringComparison.Ordinal) ||
            requirementAuthority.CourseId is not null ||
            requirementAuthority.CourseMilestoneOrdinal is not null ||
            requirementAuthority.CourseCoordinateFingerprint is not null ||
            requirementAuthority.CourseRequirementStatus is not null ||
            requirementAuthority.InterruptionReason is not null)
        {
            return false;
        }

        var scopes = requirementAuthority.Scopes.Where(scope => string.Equals(
            scope.Scope,
            "common",
            StringComparison.Ordinal)).ToArray();
        if (requirementAuthority.Scopes.Count != 1 ||
            scopes.Length != 1 ||
            !string.Equals(scopes[0].Status, "Satisfied", StringComparison.Ordinal) ||
            scopes[0].CourseMilestoneOrdinal is not null ||
            scopes[0].FailureWitnesses.Count != 0 ||
            scopes[0].Bindings.Count != route.Requirements.Length)
        {
            return false;
        }

        var indices = scopes[0].Bindings
            .Select(static binding => binding.RequirementIndex)
            .OrderBy(static index => index)
            .ToArray();
        if (!indices.SequenceEqual(Enumerable.Range(0, route.Requirements.Length)))
            return false;

        commonScope = scopes[0];
        return true;
    }

    private static bool TryResolveModifier(
        MortalWoundTreatmentAttemptCoordinates coordinates,
        MortalWoundProcedureRouteDefinition route,
        MortalWoundTreatmentRequirementScopeAuthority commonScope,
        out int modifier,
        out string? rollActorKind,
        out string? rollActorId)
    {
        modifier = 0;
        rollActorKind = null;
        rollActorId = null;
        switch (route.Resolution.ModifierSource)
        {
            case MortalWoundFixedZeroModifierSource:
                rollActorKind = coordinates.ProviderKind;
                rollActorId = coordinates.ProviderId;
                return ResourceMaterializationContract.IsExactIdentifier(rollActorKind) &&
                       ResourceMaterializationContract.IsExactIdentifier(rollActorId);
            case MortalWoundResolvedSkillTierModifierSource resolved:
            {
                if (resolved.RequirementIndex < 0 ||
                    resolved.RequirementIndex >= route.Requirements.Length ||
                    route.Requirements[resolved.RequirementIndex] is not
                        MortalWoundSkillTierRequirement routeRequirement)
                {
                    return false;
                }

                var bindings = commonScope.Bindings.Where(binding =>
                    binding.RequirementIndex == resolved.RequirementIndex).ToArray();
                if (bindings.Length != 1)
                    return false;
                var binding = bindings[0];
                var row = binding.ResolvedRequirement;
                var witness = binding.SuccessWitness;
                if (!string.Equals(row.Kind, "skill_tier", StringComparison.Ordinal) ||
                    row.CurrentTier is null ||
                    row.OwnerKind is null ||
                    row.OwnerId is null ||
                    witness.Evidence is not MortalWoundSkillTierRequirementEvidence evidence ||
                    witness.RequirementIndex != resolved.RequirementIndex ||
                    !string.Equals(witness.Kind, row.Kind, StringComparison.Ordinal) ||
                    !string.Equals(witness.AuthorityRef, row.AuthorityRef, StringComparison.Ordinal) ||
                    !string.Equals(row.AuthorityRef, routeRequirement.CapabilityRef, StringComparison.Ordinal) ||
                    row.MinimumTier != routeRequirement.MinimumTier ||
                    evidence.MinimumTier != routeRequirement.MinimumTier ||
                    evidence.CurrentTier != row.CurrentTier.Value ||
                    !string.Equals(evidence.ActorRole, routeRequirement.ActorRole, StringComparison.Ordinal) ||
                    !string.Equals(witness.OwnerKind, row.OwnerKind, StringComparison.Ordinal) ||
                    !string.Equals(witness.OwnerId, row.OwnerId, StringComparison.Ordinal))
                {
                    return false;
                }

                modifier = row.CurrentTier.Value;
                rollActorKind = row.OwnerKind;
                rollActorId = row.OwnerId;
                return ResourceMaterializationContract.IsExactIdentifier(rollActorKind) &&
                       ResourceMaterializationContract.IsExactIdentifier(rollActorId);
            }
            default:
                return false;
        }
    }

    private static bool TryComposeRollContributions(
        MortalWoundTreatmentAcceptedStateAuthority acceptedState,
        string realm,
        string rollActorKind,
        string rollActorId,
        out IReadOnlyList<MortalWoundProcedureRollContribution>? contributions,
        out string? rollMode)
    {
        var accepted = new List<MortalWoundProcedureRollContribution>();
        var hasAdvantage = false;
        var hasDisadvantage = false;
        foreach (var component in acceptedState.EffectMechanics.Components)
        {
            if (!string.Equals(component.Realm, realm, StringComparison.Ordinal) ||
                !string.Equals(component.TargetKind, rollActorKind, StringComparison.Ordinal) ||
                !string.Equals(component.TargetId, rollActorId, StringComparison.Ordinal) ||
                !string.Equals(component.Profile, "roll_modifier", StringComparison.Ordinal))
            {
                continue;
            }

            var payload = component.Payload;
            if (payload.ValueKind != JsonValueKind.Object ||
                !payload.TryGetProperty("operations", out var operations) ||
                operations.ValueKind != JsonValueKind.Array ||
                operations.EnumerateArray().Any(static operation =>
                    operation.ValueKind != JsonValueKind.String))
            {
                contributions = null;
                rollMode = null;
                return false;
            }
            if (!operations.EnumerateArray().Any(operation => string.Equals(
                    operation.GetString(),
                    "skill_check",
                    StringComparison.Ordinal)))
            {
                continue;
            }
            if (!payload.TryGetProperty("contribution", out var contributionNode) ||
                contributionNode.ValueKind != JsonValueKind.String)
            {
                contributions = null;
                rollMode = null;
                return false;
            }
            var contribution = contributionNode.GetString();
            switch (contribution)
            {
                case "advantage":
                    hasAdvantage = true;
                    break;
                case "disadvantage":
                    hasDisadvantage = true;
                    break;
                default:
                    contributions = null;
                    rollMode = null;
                    return false;
            }
            accepted.Add(MortalWoundProcedureRollContribution.Create(
                component.EffectId,
                component.ComponentId,
                contribution));
        }

        contributions = Array.AsReadOnly(accepted.ToArray());
        rollMode = hasAdvantage == hasDisadvantage
            ? "normal"
            : hasAdvantage ? "advantage" : "disadvantage";
        return true;
    }

    private static int SelectSourceOrdinal(
        IReadOnlyList<int> rolls,
        string rollMode)
    {
        if (rolls.Count == 1)
            return 0;
        var left = rolls[0];
        var right = rolls[1];
        return rollMode switch
        {
            "advantage" => right > left ? 1 : 0,
            "disadvantage" => right < left ? 1 : 0,
            _ => throw new InvalidOperationException("Two dice require advantage or disadvantage.")
        };
    }

    private static string ComputeAuthorityFingerprint(
        string sourcePath,
        string rollMode,
        string rollActorKind,
        string rollActorId,
        IReadOnlyList<MortalWoundProcedureRollContribution> contributions,
        IReadOnlyList<int> sourceIndices,
        IReadOnlyList<int> sourceRolls,
        int selectedSourceIndex,
        int naturalRoll,
        int modifier,
        int complicationDifficultyModifier,
        int effectiveDifficulty,
        string requirementAuthorityFingerprint,
        string coordinatesFingerprint,
        string acceptedStateFingerprint,
        string? preparedReactionFingerprint)
    {
        var fields = new List<string?>
        {
            "book_of_eternity.mortal_wound_treatment.procedure_check_authority",
            "1",
            sourcePath,
            rollMode,
            rollActorKind,
            rollActorId,
            contributions.Count.ToString(CultureInfo.InvariantCulture)
        };
        for (var index = 0; index < contributions.Count; index++)
        {
            fields.Add(index.ToString(CultureInfo.InvariantCulture));
            fields.Add(contributions[index].EffectId);
            fields.Add(contributions[index].ComponentId);
            fields.Add(contributions[index].Contribution);
        }
        fields.Add(sourceIndices.Count.ToString(CultureInfo.InvariantCulture));
        for (var index = 0; index < sourceIndices.Count; index++)
        {
            fields.Add(index.ToString(CultureInfo.InvariantCulture));
            fields.Add(sourceIndices[index].ToString(CultureInfo.InvariantCulture));
            fields.Add(sourceRolls[index].ToString(CultureInfo.InvariantCulture));
        }
        fields.Add(selectedSourceIndex.ToString(CultureInfo.InvariantCulture));
        fields.Add(naturalRoll.ToString(CultureInfo.InvariantCulture));
        fields.Add(modifier.ToString(CultureInfo.InvariantCulture));
        fields.Add(complicationDifficultyModifier.ToString(CultureInfo.InvariantCulture));
        fields.Add(effectiveDifficulty.ToString(CultureInfo.InvariantCulture));
        fields.Add(requirementAuthorityFingerprint);
        fields.Add(coordinatesFingerprint);
        fields.Add(acceptedStateFingerprint);
        fields.Add(preparedReactionFingerprint);
        return WoundAcceptedTurnFingerprintWriter.Compute(fields);
    }

    private static MortalWoundProcedureCheckAuthorityResult Invalid(
        string path,
        string code,
        string expected,
        string actual) =>
        MortalWoundProcedureCheckAuthorityResult.Invalid(new ValidationIssue(
            path,
            IssueSeverity.Error,
            "The Mortal wound procedure-check authority cannot be trusted.",
            code: code,
            actor: "Client",
            section: "wound_materialization",
            expected: expected,
            actual: actual));
}

internal sealed partial class MortalWoundProcedureRollContribution
{
    private MortalWoundProcedureRollContribution(
        string effectId,
        string componentId,
        string contribution)
    {
        EffectId = effectId;
        ComponentId = componentId;
        Contribution = contribution;
    }

    public string EffectId { get; }
    public string ComponentId { get; }
    public string Contribution { get; }

    internal static MortalWoundProcedureRollContribution Create(
        string effectId,
        string componentId,
        string contribution) => new(effectId, componentId, contribution);
}

internal sealed partial class MortalWoundPreparedCriticalReaction
{
    private MortalWoundPreparedCriticalReaction(
        string effectId,
        string triggerId,
        string acceptedEffectFingerprint,
        string preparedReactionFingerprint)
    {
        EffectId = effectId;
        TriggerId = triggerId;
        AcceptedEffectFingerprint = acceptedEffectFingerprint;
        PreparedReactionFingerprint = preparedReactionFingerprint;
    }

    public string EffectId { get; }
    public string TriggerId { get; }
    public string AcceptedEffectFingerprint { get; }
    public string PreparedReactionFingerprint { get; }

    internal static MortalWoundPreparedCriticalReaction Create(
        MortalWoundCriticalReactionReservation reservation)
    {
        ArgumentNullException.ThrowIfNull(reservation);
        return new MortalWoundPreparedCriticalReaction(
            reservation.EffectId,
            reservation.TriggerId,
            reservation.AcceptedEffectFingerprint,
            reservation.PreparedReactionFingerprint);
    }
}
