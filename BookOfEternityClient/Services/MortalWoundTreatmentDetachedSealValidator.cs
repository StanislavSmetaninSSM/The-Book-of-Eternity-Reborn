using System.Globalization;
using System.Text.Json;

namespace BookOfEternityClient.Services;

internal static class MortalWoundTreatmentDetachedSealValidator
{
    internal static bool IsValid(MortalWoundTreatmentAttemptRequest request)
        => FindMismatch(request) is null;

    internal static string? FindMismatch(MortalWoundTreatmentAttemptRequest request)
    {
        ArgumentNullException.ThrowIfNull(request);
        if (!request.HasMatchingFingerprint()) return "request";
        if (!HasValidCoordinates(request.Coordinates)) return "coordinates";
        var routeSourceMismatch = FindRouteSourceMismatch(request);
        if (routeSourceMismatch is not null)
            return "route_source." + routeSourceMismatch;
        if (!TryGetRoute(request, out var selectedRoute) || selectedRoute is null)
            return "route_source.route";
        var requirementValidated = false;
        if (request.Mode is "procedure" or "guaranteed")
        {
            if (!HasValidRequirementAuthority(request, selectedRoute))
                return "requirement_authority";
            requirementValidated = true;
        }
        if (request.ModeAuthority is MortalWoundCourseModeAuthority course)
        {
            var courseMismatch = FindCourseAuthorityMismatch(
                course,
                request.Coordinates);
            if (courseMismatch is not null)
                return "mode_authority." + courseMismatch;
            if (!string.Equals(
                    course.CourseStartAuthority.RouteId,
                    request.Coordinates.RouteId,
                    StringComparison.Ordinal) ||
                !string.Equals(
                    course.CourseStartAuthority.StartingWound.WoundId,
                    request.Coordinates.WoundId,
                    StringComparison.Ordinal) ||
                !string.Equals(
                    course.CourseStartAuthority.RouteFingerprint,
                    request.RequirementAuthority.RouteFingerprint,
                    StringComparison.Ordinal))
            {
                return "mode_authority.binding";
            }
        }
        else if (request.ModeAuthority is MortalWoundProcedureCheckAuthority procedure)
        {
            if (selectedRoute is not MortalWoundProcedureRouteDefinition procedureRoute ||
                !HasValidProcedureAuthority(procedure, request, procedureRoute))
            {
                return "mode_authority";
            }
        }
        else if (request.ModeAuthority is MortalWoundTreatmentCapabilityProof proof)
        {
            if (selectedRoute is not MortalWoundGuaranteedRouteDefinition guaranteedRoute ||
                !HasValidCapabilityProof(proof, request, guaranteedRoute))
            {
                return "mode_authority";
            }
        }
        else
        {
            return "mode_authority";
        }
        if (!requirementValidated &&
            !HasValidRequirementAuthority(request, selectedRoute))
            return "requirement_authority";
        if (!HasValidResourceAuthority(request.ResourceAuthority))
            return "resource_authority";
        if (!HasValidResourceAgreement(request))
            return "resource_requirement_agreement";
        return MortalWoundTreatmentPlanner.MatchesRequestAuthorities(
            request.Mode,
            request.Coordinates,
            request.MilestoneOrdinal,
            request.ModeAuthority,
            request.RequirementAuthority,
            request.ResourceAuthority)
            ? null
            : "authority_agreement";
    }

    private static bool HasValidCoordinates(MortalWoundTreatmentAttemptCoordinates value)
    {
        if (value.SchemaVersion != 1 ||
            value.Turn < 0 ||
            !string.Equals(value.Realm, "mortal_world", StringComparison.Ordinal) ||
            value.ProviderKind is not ("player" or "npc" or "combatant" or
                "combatant_member") ||
            value.TargetKind is not ("player" or "npc" or "combatant" or
                "combatant_member") ||
            !ResourceMaterializationContract.IsExactIdentifier(value.SessionId) ||
            !ResourceMaterializationContract.IsExactIdentifier(value.SessionGeneration) ||
            !ResourceMaterializationContract.IsExactIdentifier(value.RequestId) ||
            !ResourceMaterializationContract.IsExactIdentifier(value.SnapshotToken) ||
            !ResourceMaterializationContract.IsExactIdentifier(value.OperationKey) ||
            !ResourceMaterializationContract.IsExactIdentifier(value.WoundId) ||
            !ResourceMaterializationContract.IsExactIdentifier(value.RouteId) ||
            !ResourceMaterializationContract.IsExactIdentifier(value.EventRef) ||
            !ResourceMaterializationContract.IsExactIdentifier(value.EventKind) ||
            !ResourceMaterializationContract.IsExactIdentifier(value.EventAuthorityId) ||
            !ResourceMaterializationContract.IsExactIdentifier(value.ProviderId) ||
            !ResourceMaterializationContract.IsExactIdentifier(value.TargetId) ||
            !ResourceMaterializationContract.IsExactIdentifier(value.LocationId) ||
            !ResourceMaterializationContract.IsAuthorityFingerprint(
                value.ExpectedBeforeFingerprint) ||
            !ResourceMaterializationContract.IsAuthorityFingerprint(
                value.EventSemanticFingerprint) ||
            !ResourceMaterializationContract.IsAuthorityFingerprint(
                value.ContextFingerprint) ||
            !ResourceMaterializationContract.IsAuthorityFingerprint(
                value.AcceptedStateFingerprint))
        {
            return false;
        }
        var semanticFields = new string?[]
        {
            Number(value.SchemaVersion), value.SessionId, value.SessionGeneration,
            value.RequestId, value.SnapshotToken, value.OperationKey, value.WoundId,
            value.RouteId, value.ExpectedBeforeFingerprint, value.EventRef,
            value.EventKind, value.EventAuthorityId, value.EventSemanticFingerprint,
            Number(value.Turn), value.Realm, value.ProviderKind, value.ProviderId,
            value.TargetKind, value.TargetId, value.LocationId,
            value.ContextFingerprint, value.AcceptedStateFingerprint
        };
        var attemptId = "wound_treatment_attempt_" + Fingerprint(
            "book_of_eternity.mortal_wound_treatment.attempt_identity",
            semanticFields)["sha256:".Length..];
        var coordinatesFingerprint = Fingerprint(
            "book_of_eternity.mortal_wound_treatment.attempt_coordinates",
            semanticFields.Concat(new[] { attemptId }));
        return string.Equals(value.AttemptId, attemptId, StringComparison.Ordinal) &&
               string.Equals(
                   value.CoordinatesFingerprint,
                   coordinatesFingerprint,
                   StringComparison.Ordinal);
    }

    private static string? FindRouteSourceMismatch(
        MortalWoundTreatmentAttemptRequest request)
    {
        try
        {
            var source = request.RouteSourceWound;
            if (source is null ||
                !ResourceMaterializationContract.IsAuthorityFingerprint(
                    request.RouteSourceWoundFingerprint))
            {
                return "shape";
            }
            var sourceFingerprint =
                WoundIdentityState.ComputeSemanticFingerprint(source);
            if (!string.Equals(
                    request.RouteSourceWoundFingerprint,
                    sourceFingerprint,
                    StringComparison.Ordinal) ||
                !string.Equals(
                    sourceFingerprint,
                    request.Coordinates.ExpectedBeforeFingerprint,
                    StringComparison.Ordinal))
            {
                return "wound_fingerprint";
            }
            if (!string.Equals(
                    source.WoundId,
                    request.Coordinates.WoundId,
                    StringComparison.Ordinal) ||
                !string.Equals(
                    source.Owner.Realm,
                    request.Coordinates.Realm,
                    StringComparison.Ordinal) ||
                !string.Equals(
                    source.Owner.OwnerKind,
                    request.Coordinates.TargetKind,
                    StringComparison.Ordinal) ||
                !string.Equals(
                    source.Owner.OwnerId,
                    request.Coordinates.TargetId,
                    StringComparison.Ordinal))
            {
                return "coordinates";
            }
            return TryGetRoute(request, out _) ? null : "route";
        }
        catch (Exception exception) when (exception is ArgumentException or
                                           InvalidOperationException or
                                           System.Text.Json.JsonException or
                                           OverflowException or
                                           NullReferenceException)
        {
            return "invalid";
        }
    }

    internal static bool TryGetRoute(
        MortalWoundTreatmentAttemptRequest request,
        out MortalWoundTreatmentRouteDefinition? route)
    {
        route = null;
        var source = request.RouteSourceWound;
        try
        {
            var parsed = MortalWoundTreatmentContract.ParseProjection(
                source.Treatment,
                "treatmentAttempt.request.routeSourceWound.treatment",
                source.Owner.Realm,
                source.Owner.OwnerKind,
                source.Severity.Rank,
                source.Complications,
                source.Recovery.DeteriorationPolicy);
            if (!parsed.IsValid || parsed.Treatment is null)
                return false;
            var matches = parsed.Treatment.Routes.Where(candidate =>
                    string.Equals(
                        candidate.RouteId,
                        request.Coordinates.RouteId,
                        StringComparison.Ordinal) &&
                    string.Equals(candidate.Mode, request.Mode, StringComparison.Ordinal))
                .ToArray();
            if (matches.Length != 1)
                return false;
            var routeFingerprint = MortalWoundTreatmentRouteFingerprint.Compute(
                source,
                request.Coordinates.RouteId);
            if (!string.Equals(
                    routeFingerprint,
                    request.RequirementAuthority.RouteFingerprint,
                    StringComparison.Ordinal) ||
                !string.Equals(
                    routeFingerprint,
                    request.ResourceAuthority.RouteFingerprint,
                    StringComparison.Ordinal))
            {
                return false;
            }
            route = matches[0];
            return true;
        }
        catch (Exception exception) when (exception is ArgumentException or
                                           InvalidOperationException or
                                           System.Text.Json.JsonException or
                                           OverflowException or
                                           NullReferenceException)
        {
            return false;
        }
    }

    private static bool HasValidProcedureAuthority(
        MortalWoundProcedureCheckAuthority value,
        MortalWoundTreatmentAttemptRequest request,
        MortalWoundProcedureRouteDefinition route)
    {
        if (!string.Equals(request.Mode, "procedure", StringComparison.Ordinal) ||
            request.MilestoneOrdinal is not null ||
            !string.Equals(
                value.SourcePath,
                LiveTurnPreparationService.TurnRequestPath,
                StringComparison.Ordinal) ||
            !string.Equals(
                value.RequirementAuthorityFingerprint,
                request.RequirementAuthority.AuthorityFingerprint,
                StringComparison.Ordinal) ||
            !string.Equals(
                value.CoordinatesFingerprint,
                request.Coordinates.CoordinatesFingerprint,
                StringComparison.Ordinal) ||
            !string.Equals(
                value.AcceptedStateFingerprint,
                request.Coordinates.AcceptedStateFingerprint,
                StringComparison.Ordinal) ||
            !TryResolveDetachedProcedureModifier(
                request,
                route,
                out var expectedModifier,
                out var expectedActorKind,
                out var expectedActorId,
                out var expectedRollSkillId,
                out var skillProof) ||
            value.Modifier != expectedModifier ||
            !string.Equals(
                value.RollActorKind,
                expectedActorKind,
                StringComparison.Ordinal) ||
            !string.Equals(
                value.RollActorId,
                expectedActorId,
                StringComparison.Ordinal) ||
            !string.Equals(
                value.RollSkillId,
                expectedRollSkillId,
                StringComparison.Ordinal) ||
            value.RollContributions.Any(static contribution => contribution is null))
        {
            return false;
        }
        var resolution = EffectRollContributionResolver.Resolve(
            value.RollSourceAuthority,
            new EffectRollContext(
                request.Coordinates.Realm,
                expectedActorKind!,
                expectedActorId!,
                "skill_check",
                expectedRollSkillId),
            skillProof);
        if (!resolution.IsValid ||
            !string.Equals(value.RollMode, resolution.RollMode, StringComparison.Ordinal) ||
            !DetachedContributionsAgree(
                value.RollContributions,
                resolution.Contributions) ||
            !HasValidDetachedProcedureDice(value, resolution.RollMode))
        {
            return false;
        }

        try
        {
            var complicationModifier = request.RouteSourceWound.Complications
                .Where(static complication => string.Equals(
                    complication.State,
                    "active",
                    StringComparison.Ordinal))
                .Aggregate(0, static (sum, complication) => checked(
                    sum + complication.TreatmentDifficultyModifier));
            if (value.ComplicationDifficultyModifier != complicationModifier ||
                value.EffectiveDifficulty != checked(
                    route.Resolution.Difficulty + complicationModifier))
            {
                return false;
            }
        }
        catch (OverflowException)
        {
            return false;
        }

        string? preparedFingerprint = null;
        if (value.PreparedCriticalReaction is { } reaction)
        {
            if (value.NaturalRoll != 1 ||
                !string.Equals(value.RollActorKind, "player", StringComparison.Ordinal) ||
                !string.Equals(value.RollActorId, "player_current", StringComparison.Ordinal) ||
                !IsExactIdentifier(reaction.EffectId) ||
                !IsExactIdentifier(reaction.TriggerId) ||
                !ResourceMaterializationContract.IsAuthorityFingerprint(
                    reaction.AcceptedEffectFingerprint))
            {
                return false;
            }
            preparedFingerprint = WoundAcceptedTurnFingerprintWriter.Compute(new string?[]
            {
                "book_of_eternity.mortal_wound_treatment.prepared_critical_reaction",
                "1", reaction.EffectId, reaction.TriggerId,
                reaction.AcceptedEffectFingerprint, value.CoordinatesFingerprint,
                value.AcceptedStateFingerprint
            });
            if (!string.Equals(
                    reaction.PreparedReactionFingerprint,
                    preparedFingerprint,
                    StringComparison.Ordinal))
            {
                return false;
            }
        }

        var fields = new List<string?>
        {
            "book_of_eternity.mortal_wound_treatment.procedure_check_authority",
            "2",
            value.SourcePath,
            value.RollMode,
            value.RollActorKind,
            value.RollActorId,
            value.RollSkillId,
            value.RollSourceAuthority.AuthorityFingerprint,
            Number(value.RollContributions.Count)
        };
        for (var index = 0; index < value.RollContributions.Count; index++)
        {
            var contribution = value.RollContributions[index];
            fields.Add(Number(index));
            fields.Add(contribution.EffectId);
            fields.Add(contribution.ComponentId);
            fields.Add(contribution.Contribution);
        }
        fields.Add(Number(value.SourceIndices.Count));
        for (var index = 0; index < value.SourceIndices.Count; index++)
        {
            fields.Add(Number(index));
            fields.Add(Number(value.SourceIndices[index]));
            fields.Add(Number(value.SourceRolls[index]));
        }
        fields.Add(Number(value.SelectedSourceIndex));
        fields.Add(Number(value.NaturalRoll));
        fields.Add(Number(value.Modifier));
        fields.Add(Number(value.ComplicationDifficultyModifier));
        fields.Add(Number(value.EffectiveDifficulty));
        fields.Add(value.RequirementAuthorityFingerprint);
        fields.Add(value.CoordinatesFingerprint);
        fields.Add(value.AcceptedStateFingerprint);
        fields.Add(preparedFingerprint);
        return string.Equals(
            value.AuthorityFingerprint,
            WoundAcceptedTurnFingerprintWriter.Compute(fields),
            StringComparison.Ordinal);
    }

    private static bool TryResolveDetachedProcedureModifier(
        MortalWoundTreatmentAttemptRequest request,
        MortalWoundProcedureRouteDefinition route,
        out int modifier,
        out string? actorKind,
        out string? actorId,
        out string? rollSkillId,
        out EffectRollSkillUsabilityProof? skillProof)
    {
        modifier = 0;
        actorKind = null;
        actorId = null;
        rollSkillId = null;
        skillProof = null;
        var coordinates = request.Coordinates;
        var bundle = request.RequirementAuthority;
        if (!string.Equals(bundle.Mode, "procedure", StringComparison.Ordinal) ||
            !string.Equals(
                bundle.ContextFingerprint,
                coordinates.ContextFingerprint,
                StringComparison.Ordinal) ||
            !string.Equals(
                bundle.AcceptedStateFingerprint,
                coordinates.AcceptedStateFingerprint,
                StringComparison.Ordinal) ||
            bundle.CourseId is not null ||
            bundle.CourseMilestoneOrdinal is not null ||
            bundle.CourseCoordinateFingerprint is not null ||
            bundle.CourseRequirementStatus is not null ||
            bundle.InterruptionReason is not null)
        {
            return false;
        }

        var commonScopes = bundle.Scopes.Where(static scope => string.Equals(
            scope.Scope,
            "common",
            StringComparison.Ordinal)).ToArray();
        if (bundle.Scopes.Count != 1 ||
            commonScopes.Length != 1 ||
            !string.Equals(commonScopes[0].Status, "Satisfied", StringComparison.Ordinal) ||
            commonScopes[0].CourseMilestoneOrdinal is not null ||
            commonScopes[0].FailureWitnesses.Count != 0)
        {
            return false;
        }

        switch (route.Resolution.ModifierSource)
        {
            case MortalWoundFixedZeroModifierSource:
                actorKind = coordinates.ProviderKind;
                actorId = coordinates.ProviderId;
                return ValidActorCoordinate(actorKind, actorId);

            case MortalWoundResolvedSkillTierModifierSource resolved:
                if (resolved.RequirementIndex < 0 ||
                    resolved.RequirementIndex >= route.Requirements.Length ||
                    route.Requirements[resolved.RequirementIndex] is not
                        MortalWoundSkillTierRequirement authored)
                {
                    return false;
                }
                var bindings = commonScopes[0].Bindings.Where(binding =>
                    binding.RequirementIndex == resolved.RequirementIndex).ToArray();
                if (bindings.Length != 1)
                    return false;
                var binding = bindings[0];
                var row = binding.ResolvedRequirement;
                var witness = binding.SuccessWitness;
                if (witness.Evidence is not MortalWoundSkillTierRequirementEvidence evidence ||
                    row.RequirementIndex != resolved.RequirementIndex ||
                    !string.Equals(row.Kind, "skill_tier", StringComparison.Ordinal) ||
                    !string.Equals(row.AuthorityRef, authored.CapabilityRef,
                        StringComparison.Ordinal) ||
                    row.MinimumTier != authored.MinimumTier ||
                    row.CurrentTier is null ||
                    row.SkillId is null ||
                    !string.Equals(witness.Scope, "common", StringComparison.Ordinal) ||
                    witness.RequirementIndex != resolved.RequirementIndex ||
                    !string.Equals(witness.Kind, row.Kind, StringComparison.Ordinal) ||
                    !string.Equals(witness.AuthorityRef, row.AuthorityRef,
                        StringComparison.Ordinal) ||
                    !string.Equals(witness.OwnerKind, row.OwnerKind,
                        StringComparison.Ordinal) ||
                    !string.Equals(witness.OwnerId, row.OwnerId,
                        StringComparison.Ordinal) ||
                    !string.Equals(evidence.ActorRole, authored.ActorRole,
                        StringComparison.Ordinal) ||
                    evidence.MinimumTier != authored.MinimumTier ||
                    evidence.CurrentTier != row.CurrentTier.Value ||
                    !string.Equals(evidence.SkillId, row.SkillId, StringComparison.Ordinal) ||
                    !ValidActorCoordinate(row.OwnerKind, row.OwnerId))
                {
                    return false;
                }
                modifier = row.CurrentTier.Value;
                actorKind = row.OwnerKind;
                actorId = row.OwnerId;
                rollSkillId = row.SkillId;
                skillProof = new EffectRollSkillUsabilityProof(
                    request.Coordinates.Realm,
                    actorKind!,
                    actorId!,
                    rollSkillId);
                return ResourceMaterializationContract.IsExactIdentifier(rollSkillId) &&
                    evidence.ActorReachable &&
                    evidence.ActorPresent &&
                    IsCurrentActive(evidence.ActorLifecycle, evidence.ActorActive) &&
                    IsCurrentActive(evidence.SkillLifecycle, evidence.SkillActive);

            default:
                return false;
        }
    }

    private static bool DetachedContributionsAgree(
        IReadOnlyList<MortalWoundProcedureRollContribution> actual,
        IReadOnlyList<EffectRollContributionEvidence> expected)
    {
        return actual.Count == expected.Count &&
            actual.Select(
                (value, index) =>
                    string.Equals(
                        value.EffectId,
                        expected[index].EffectId,
                        StringComparison.Ordinal) &&
                    string.Equals(
                        value.ComponentId,
                        expected[index].ComponentId,
                        StringComparison.Ordinal) &&
                    string.Equals(
                        value.Contribution,
                        expected[index].Contribution,
                        StringComparison.Ordinal)).All(static value => value);
    }

    private static bool HasValidDetachedProcedureDice(
        MortalWoundProcedureCheckAuthority value,
        string rollMode)
    {
        var requiredDice = string.Equals(rollMode, "normal", StringComparison.Ordinal)
            ? 1
            : 2;
        if (value.SourceIndices.Count != requiredDice ||
            value.SourceRolls.Count != requiredDice ||
            value.SourceIndices[0] < 0 ||
            value.SourceRolls.Any(static roll => roll is < 1 or > 20))
        {
            return false;
        }
        for (var index = 1; index < requiredDice; index++)
        {
            if (value.SourceIndices[index - 1] == int.MaxValue ||
                value.SourceIndices[index] != value.SourceIndices[index - 1] + 1)
            {
                return false;
            }
        }

        var selectedOrdinal = requiredDice == 1
            ? 0
            : rollMode switch
            {
                "advantage" => value.SourceRolls[1] > value.SourceRolls[0] ? 1 : 0,
                "disadvantage" => value.SourceRolls[1] < value.SourceRolls[0] ? 1 : 0,
                _ => -1
            };
        return selectedOrdinal >= 0 &&
               value.SelectedSourceIndex == value.SourceIndices[selectedOrdinal] &&
               value.NaturalRoll == value.SourceRolls[selectedOrdinal];
    }

    private static string? FindCourseAuthorityMismatch(
        MortalWoundCourseModeAuthority value,
        MortalWoundTreatmentAttemptCoordinates? requestCoordinates = null)
    {
        if (value.GameTimeAuthority is null || value.CourseStartAuthority is null)
            return "nested_authority";
        var time = value.GameTimeAuthority;
        var timeFingerprint = WoundAcceptedTurnFingerprintWriter.Compute(new string?[]
        {
            "book_of_eternity.mortal_wound_treatment.game_time_authority", "1",
            MortalWoundGameTimeAuthority.CanonicalClockKind,
            MortalWoundGameTimeAuthority.CanonicalSourcePath,
            Number(time.CurrentTimeInMinutes), time.CoordinatesFingerprint,
            time.AcceptedStateFingerprint
        });
        if (!string.Equals(time.AuthorityFingerprint, timeFingerprint,
                StringComparison.Ordinal))
        {
            return "game_time";
        }
        if (time.CurrentTimeInMinutes < 0 ||
            !string.Equals(
                time.CoordinatesFingerprint,
                value.CoordinatesFingerprint,
                StringComparison.Ordinal) ||
            !string.Equals(
                time.AcceptedStateFingerprint,
                value.AcceptedStateFingerprint,
                StringComparison.Ordinal))
        {
            return "game_time_binding";
        }

        var start = value.CourseStartAuthority;
        if (start.StartingWound is null || start.StartingWound.Owner is null)
            return "starting_wound";
        if (!ResourceMaterializationContract.IsExactIdentifier(value.CourseId) ||
            !ResourceMaterializationContract.IsExactIdentifier(start.CourseId) ||
            !string.Equals(start.CourseId, value.CourseId,
                StringComparison.Ordinal) ||
            value.MilestoneOrdinal == 1 &&
            (!string.Equals(
                 start.AcceptedStateFingerprint,
                 value.AcceptedStateFingerprint,
                 StringComparison.Ordinal) ||
             !string.Equals(
                 start.CoordinatesFingerprint,
                 value.CoordinatesFingerprint,
                 StringComparison.Ordinal)) ||
            requestCoordinates is not null &&
            (!string.Equals(
                 value.AcceptedStateFingerprint,
                 requestCoordinates.AcceptedStateFingerprint,
                 StringComparison.Ordinal) ||
             !string.Equals(
                 value.CoordinatesFingerprint,
                 requestCoordinates.CoordinatesFingerprint,
                 StringComparison.Ordinal) ||
             !string.Equals(
                 start.StartingWound.Owner.Realm,
                 requestCoordinates.Realm,
                 StringComparison.Ordinal) ||
             !string.Equals(
                 start.StartingWound.Owner.OwnerKind,
                 requestCoordinates.TargetKind,
                 StringComparison.Ordinal) ||
             !string.Equals(
                 start.StartingWound.Owner.OwnerId,
                 requestCoordinates.TargetId,
                 StringComparison.Ordinal)))
        {
            return "course_start_binding";
        }
        string canonicalWound;
        string startingWoundFingerprint;
        try
        {
            canonicalWound = WoundMaterializationContract.SerializeCanonical(
                start.StartingWound);
            startingWoundFingerprint = WoundIdentityState.ComputeSemanticFingerprint(
                start.StartingWound);
        }
        catch (Exception exception) when (exception is ArgumentException or
                                           InvalidOperationException or
                                           System.Text.Json.JsonException)
        {
            return "starting_wound";
        }
        var startFingerprint = WoundAcceptedTurnFingerprintWriter.Compute(new string?[]
        {
            "book_of_eternity.mortal_wound_treatment.course_start_authority", "1",
            start.CourseId, start.RouteId, start.RouteFingerprint, canonicalWound,
            startingWoundFingerprint, Number(start.StartedAtGameTimeMinutes),
            start.AcceptedStateFingerprint, start.CoordinatesFingerprint
        });
        if (!string.Equals(start.StartingWoundFingerprint, startingWoundFingerprint,
                StringComparison.Ordinal))
            return "starting_wound_fingerprint";
        if (!string.Equals(start.AuthorityFingerprint, startFingerprint,
                StringComparison.Ordinal))
            return "course_start";

        if (!TryGetCourseRoute(value, out var route) ||
            route is null ||
            start.StartedAtGameTimeMinutes < 0)
        {
            return "course_route";
        }
        var milestones = route.Milestones.Where(candidate =>
            candidate.Ordinal == value.MilestoneOrdinal).ToArray();
        if (milestones.Length != 1)
            return "course_milestone";
        long due;
        long deadline;
        try
        {
            due = checked(
                start.StartedAtGameTimeMinutes + milestones[0].AfterMinutes);
            deadline = checked(due + route.Resolution.MaximumGapMinutes);
        }
        catch (OverflowException)
        {
            return "course_window";
        }
        var expectedWindow = time.CurrentTimeInMinutes < due
            ? null
            : time.CurrentTimeInMinutes <= deadline
                ? "ready"
                : "deadline_exceeded";
        if (expectedWindow is null ||
            value.DueAtGameTimeMinutes != due ||
            value.DeadlineAtGameTimeMinutes != deadline ||
            !string.Equals(
                value.WindowDisposition,
                expectedWindow,
                StringComparison.Ordinal))
        {
            return "course_window";
        }

        var courseCoordinate = WoundAcceptedTurnFingerprintWriter.Compute(new string?[]
        {
            "book_of_eternity.mortal_wound_treatment.course_coordinates", "1",
            value.CourseId, Number(value.MilestoneOrdinal), start.StartingWound.WoundId,
            start.StartingWoundFingerprint, start.RouteId, start.RouteFingerprint,
            start.AuthorityFingerprint
        });
        var authorityFingerprint = WoundAcceptedTurnFingerprintWriter.Compute(new string?[]
        {
            "book_of_eternity.mortal_wound_treatment.course_mode_authority", "1",
            time.AuthorityFingerprint, value.CourseId, Number(value.MilestoneOrdinal),
            Number(value.DueAtGameTimeMinutes), Number(value.DeadlineAtGameTimeMinutes),
            value.WindowDisposition, start.AuthorityFingerprint, courseCoordinate,
            value.CoordinatesFingerprint, value.AcceptedStateFingerprint
        });
        if (value.MilestoneOrdinal <= 0 ||
            !string.Equals(value.CourseId, start.CourseId, StringComparison.Ordinal))
            return "identity";
        if (!string.Equals(value.CourseCoordinateFingerprint, courseCoordinate,
                StringComparison.Ordinal))
            return "coordinate";
        return string.Equals(value.AuthorityFingerprint, authorityFingerprint,
            StringComparison.Ordinal)
            ? null
            : "authority";
    }

    private static bool HasValidCapabilityProof(
        MortalWoundTreatmentCapabilityProof value,
        MortalWoundTreatmentAttemptRequest request,
        MortalWoundGuaranteedRouteDefinition route)
    {
        if (value.OperationLimits is null ||
            !string.Equals(request.Mode, "guaranteed", StringComparison.Ordinal) ||
            request.MilestoneOrdinal is not null ||
            !string.Equals(
                value.SnapshotToken,
                request.Coordinates.SnapshotToken,
                StringComparison.Ordinal) ||
            !string.Equals(
                value.ContextFingerprint,
                request.Coordinates.ContextFingerprint,
                StringComparison.Ordinal) ||
            !string.Equals(
                value.AcceptedStateFingerprint,
                request.Coordinates.AcceptedStateFingerprint,
                StringComparison.Ordinal) ||
            !string.Equals(
                value.CoordinatesFingerprint,
                request.Coordinates.CoordinatesFingerprint,
                StringComparison.Ordinal) ||
            !string.Equals(
                route.Resolution.CapabilityRef,
                value.CapabilityRef,
                StringComparison.Ordinal) ||
            route.Resolution.ActorRole is not ("provider" or "target") ||
            !TrySelectRole(
                request.Coordinates,
                route.Resolution.ActorRole,
                out var expectedOwner) ||
            expectedOwner.ActorKind is not ("player" or "npc") ||
            !string.Equals(
                value.OwnerKind,
                expectedOwner.ActorKind,
                StringComparison.Ordinal) ||
            !string.Equals(
                value.OwnerId,
                expectedOwner.ActorId,
                StringComparison.Ordinal) ||
            !HasValidDetachedCapabilityIdentity(value) ||
            !HasValidDetachedCapabilityBinding(request, route, value, expectedOwner) ||
            !HasValidDetachedCapabilityLimits(value.OperationLimits) ||
            !string.Equals(value.WoundDomain, "physical", StringComparison.Ordinal) ||
            !string.Equals(
                request.RouteSourceWound.Classification.Domain,
                "physical",
                StringComparison.Ordinal) ||
            value.MinimumSeverityRank is < 1 or > 4 ||
            value.MaximumSeverityRank is < 1 or > 4 ||
            value.MinimumSeverityRank > value.MaximumSeverityRank ||
            request.RouteSourceWound.Severity.Rank < value.MinimumSeverityRank ||
            request.RouteSourceWound.Severity.Rank > value.MaximumSeverityRank)
        {
            return false;
        }

        var limits = value.OperationLimits;
        var sourceFingerprint = WoundAcceptedTurnFingerprintWriter.Compute(new string?[]
        {
            "book_of_eternity.mortal_wound_treatment.capability_source", "1",
            value.SourcePath, value.OwnerKind, value.OwnerId, value.SkillKind,
            value.SkillId, "1", value.CapabilityRef, value.WoundDomain,
            Number(value.MinimumSeverityRank), Number(value.MaximumSeverityRank),
            "active", "true", Boolean(limits.MayStabilize),
            Number(limits.MaximumRecoveryPoints),
            Number(limits.MaximumSeverityReductionSteps),
            Number(limits.RemovableComplicationKinds.Count),
            string.Join(",", limits.RemovableComplicationKinds),
            Boolean(limits.MayHealAtSeverityI),
            Number(limits.MaximumCosmeticHealLegacies),
            Number(limits.MaximumMechanicalEffectHealLegacies)
        });
        if (!string.Equals(
                value.SourceSemanticFingerprint,
                sourceFingerprint,
                StringComparison.Ordinal))
        {
            return false;
        }

        var fingerprint = WoundAcceptedTurnFingerprintWriter.Compute(new string?[]
        {
            "book_of_eternity.mortal_wound_treatment.capability_proof", "1",
            value.SnapshotToken, value.SourcePath, value.OwnerKind, value.OwnerId,
            value.SkillKind, value.SkillId, value.CapabilityRef, value.WoundDomain,
            Number(value.MinimumSeverityRank), Number(value.MaximumSeverityRank),
            Boolean(limits.MayStabilize), Number(limits.MaximumRecoveryPoints),
            Number(limits.MaximumSeverityReductionSteps),
            Number(limits.RemovableComplicationKinds.Count),
            string.Join(",", limits.RemovableComplicationKinds),
            Boolean(limits.MayHealAtSeverityI),
            Number(limits.MaximumCosmeticHealLegacies),
            Number(limits.MaximumMechanicalEffectHealLegacies),
            value.ContextFingerprint, value.AcceptedStateFingerprint,
            value.CoordinatesFingerprint, sourceFingerprint
        });
        return string.Equals(value.ProofFingerprint, fingerprint, StringComparison.Ordinal) &&
               DetachedCapabilityCoversGuaranteedOutcome(
                   request.RouteSourceWound,
                   route.Outcome.DeclaredResult,
                   value);
    }

    private static bool HasValidDetachedCapabilityIdentity(
        MortalWoundTreatmentCapabilityProof value)
    {
        if (value.OwnerKind is not ("player" or "npc") ||
            value.SkillKind is not ("active" or "passive") ||
            !IsExactIdentifier(value.OwnerId) ||
            !IsExactIdentifier(value.SkillId) ||
            !IsExactIdentifier(value.CapabilityRef))
        {
            return false;
        }
        var sourcePath = value.OwnerKind switch
        {
            "npc" => "game_state/npcs/npc_core.json",
            "player" when value.SkillKind == "active" =>
                "game_state/player/skills_active.json",
            "player" when value.SkillKind == "passive" =>
                "game_state/player/skills_passive.json",
            _ => null
        };
        return string.Equals(value.SourcePath, sourcePath, StringComparison.Ordinal);
    }

    private static bool HasValidDetachedCapabilityBinding(
        MortalWoundTreatmentAttemptRequest request,
        MortalWoundGuaranteedRouteDefinition route,
        MortalWoundTreatmentCapabilityProof proof,
        MortalWoundTreatmentAuthority.ActorCoordinate expectedOwner)
    {
        var bundle = request.RequirementAuthority;
        if (!string.Equals(bundle.Mode, "guaranteed", StringComparison.Ordinal) ||
            !string.Equals(
                bundle.ContextFingerprint,
                request.Coordinates.ContextFingerprint,
                StringComparison.Ordinal) ||
            !string.Equals(
                bundle.AcceptedStateFingerprint,
                request.Coordinates.AcceptedStateFingerprint,
                StringComparison.Ordinal) ||
            bundle.CourseId is not null ||
            bundle.CourseMilestoneOrdinal is not null ||
            bundle.CourseCoordinateFingerprint is not null ||
            bundle.CourseRequirementStatus is not null ||
            bundle.InterruptionReason is not null)
        {
            return false;
        }

        var sourceRequirements = route.Requirements
            .Select((requirement, index) => (requirement, index))
            .Where(candidate => candidate.requirement is
                MortalWoundSourceCapabilityRequirement authored &&
                string.Equals(
                    authored.CapabilityRef,
                    route.Resolution.CapabilityRef,
                    StringComparison.Ordinal) &&
                string.Equals(
                    authored.ActorRole,
                    route.Resolution.ActorRole,
                    StringComparison.Ordinal))
            .ToArray();
        var commonScopes = bundle.Scopes.Where(static scope => string.Equals(
            scope.Scope,
            "common",
            StringComparison.Ordinal)).ToArray();
        if (sourceRequirements.Length != 1 ||
            bundle.Scopes.Count != 1 ||
            commonScopes.Length != 1 ||
            !string.Equals(commonScopes[0].Status, "Satisfied", StringComparison.Ordinal) ||
            commonScopes[0].CourseMilestoneOrdinal is not null ||
            commonScopes[0].FailureWitnesses.Count != 0)
        {
            return false;
        }

        var sourceIndex = sourceRequirements[0].index;
        var bindings = commonScopes[0].Bindings.Where(binding =>
            binding.RequirementIndex == sourceIndex).ToArray();
        if (bindings.Length != 1)
            return false;
        var binding = bindings[0];
        var row = binding.ResolvedRequirement;
        var witness = binding.SuccessWitness;
        return witness.Evidence is MortalWoundSourceCapabilityRequirementEvidence evidence &&
               row.RequirementIndex == sourceIndex &&
               string.Equals(row.Kind, "source_capability", StringComparison.Ordinal) &&
               string.Equals(row.AuthorityRef, proof.CapabilityRef,
                   StringComparison.Ordinal) &&
               string.Equals(row.OwnerKind, expectedOwner.ActorKind,
                   StringComparison.Ordinal) &&
               string.Equals(row.OwnerId, expectedOwner.ActorId,
                   StringComparison.Ordinal) &&
               string.Equals(witness.Scope, "common", StringComparison.Ordinal) &&
               witness.RequirementIndex == sourceIndex &&
               string.Equals(witness.Kind, row.Kind, StringComparison.Ordinal) &&
               string.Equals(witness.AuthorityRef, row.AuthorityRef,
                   StringComparison.Ordinal) &&
               string.Equals(witness.OwnerKind, row.OwnerKind,
                   StringComparison.Ordinal) &&
               string.Equals(witness.OwnerId, row.OwnerId,
                   StringComparison.Ordinal) &&
               string.Equals(evidence.ActorRole, route.Resolution.ActorRole,
                   StringComparison.Ordinal) &&
               string.Equals(evidence.ActorLifecycle, "active",
                   StringComparison.Ordinal) &&
               evidence.ActorActive &&
               string.Equals(evidence.CapabilityLifecycle, "active",
                   StringComparison.Ordinal) &&
               evidence.CapabilityActive;
    }

    private static bool HasValidDetachedCapabilityLimits(
        MortalWoundTreatmentCapabilityOperationLimits limits)
    {
        var allowedKinds = new HashSet<string>(StringComparer.Ordinal)
        {
            "bleeding", "infection", "pain", "impairment",
            "systemic_instability", "spiritual_instability", "other"
        };
        if (limits.MaximumRecoveryPoints < 0 ||
            limits.MaximumSeverityReductionSteps is < 0 or > 3 ||
            limits.MaximumCosmeticHealLegacies is < 0 or > 8 ||
            limits.MaximumMechanicalEffectHealLegacies is < 0 or > 8 ||
            (long)limits.MaximumCosmeticHealLegacies +
            limits.MaximumMechanicalEffectHealLegacies > 8 ||
            limits.RemovableComplicationKinds.Count > allowedKinds.Count ||
            !limits.RemovableComplicationKinds.SequenceEqual(
                limits.RemovableComplicationKinds.OrderBy(
                    static kind => kind,
                    StringComparer.Ordinal),
                StringComparer.Ordinal))
        {
            return false;
        }

        var exact = new HashSet<string>(StringComparer.Ordinal);
        var confusable = new HashSet<string>(StringComparer.Ordinal);
        foreach (var kind in limits.RemovableComplicationKinds)
        {
            if (!allowedKinds.Contains(kind) ||
                !exact.Add(kind) ||
                !confusable.Add(ExactIdentifierConfusableKey.Build(kind)))
            {
                return false;
            }
        }
        return limits.MayStabilize ||
               limits.MaximumRecoveryPoints > 0 ||
               limits.MaximumSeverityReductionSteps > 0 ||
               limits.RemovableComplicationKinds.Count > 0 ||
               limits.MayHealAtSeverityI ||
               limits.MaximumCosmeticHealLegacies > 0 ||
               limits.MaximumMechanicalEffectHealLegacies > 0;
    }

    private static bool DetachedCapabilityCoversGuaranteedOutcome(
        WoundMaterializationEnvelope wound,
        IReadOnlyList<MortalWoundTreatmentOperation> operations,
        MortalWoundTreatmentCapabilityProof proof)
    {
        if (operations.Count == 0)
            return false;

        long recoveryPoints = 0;
        long severityReduction = 0;
        long cosmeticLegacies = 0;
        long mechanicalLegacies = 0;
        var workingSeverity = wound.Severity.Rank;
        var workingRecovery = wound.Recovery.CurrentStepProgress;
        var workingCareState = wound.Care.State;
        var removedComplications = new HashSet<string>(StringComparer.Ordinal);
        var removableKinds = proof.OperationLimits.RemovableComplicationKinds
            .ToHashSet(StringComparer.Ordinal);
        var applicablePositive = false;
        var terminal = false;
        var healCount = 0;
        try
        {
            foreach (var operation in operations)
            {
                if (terminal)
                    return false;
                switch (operation)
                {
                    case MortalWoundStabilizeOperation:
                        if (!proof.OperationLimits.MayStabilize ||
                            workingCareState is "stabilized" or "healed")
                        {
                            return false;
                        }
                        workingCareState = "stabilized";
                        applicablePositive = true;
                        break;

                    case MortalWoundAddRecoveryOperation recovery when recovery.Points > 0:
                        recoveryPoints = checked(recoveryPoints + recovery.Points);
                        workingRecovery = checked(workingRecovery + recovery.Points);
                        applicablePositive = true;
                        break;

                    case MortalWoundReduceSeverityOperation severity when severity.Steps > 0:
                        if (severity.Steps >= workingSeverity)
                            return false;
                        severityReduction = checked(severityReduction + severity.Steps);
                        workingSeverity -= severity.Steps;
                        applicablePositive = true;
                        break;

                    case MortalWoundRemoveComplicationOperation removal:
                        var matches = wound.Complications.Where(complication =>
                            !removedComplications.Contains(complication.ComplicationId) &&
                            string.Equals(
                                complication.ComplicationId,
                                removal.ComplicationId,
                                StringComparison.Ordinal)).ToArray();
                        if (matches.Length != 1 ||
                            !removableKinds.Contains(matches[0].Kind))
                        {
                            return false;
                        }
                        removedComplications.Add(removal.ComplicationId);
                        applicablePositive = true;
                        break;

                    case MortalWoundHealOperation heal:
                        if (!proof.OperationLimits.MayHealAtSeverityI ||
                            ++healCount > 1 ||
                            workingSeverity != 1)
                        {
                            return false;
                        }
                        cosmeticLegacies = checked(cosmeticLegacies + heal.Legacies.Count(
                            static legacy => legacy is MortalWoundCosmeticLegacyDraft));
                        mechanicalLegacies = checked(mechanicalLegacies + heal.Legacies.Count(
                            static legacy => legacy is
                                MortalWoundMechanicalEffectLegacyDraft));
                        if (cosmeticLegacies + mechanicalLegacies != heal.Legacies.Length)
                            return false;
                        terminal = true;
                        applicablePositive = true;
                        break;

                    case MortalWoundNoImprovementOperation:
                    case MortalWoundAddComplicationOperation:
                    case MortalWoundApplyDeteriorationOperation:
                    default:
                        return false;
                }
            }
        }
        catch (OverflowException)
        {
            return false;
        }

        return applicablePositive &&
               recoveryPoints <= proof.OperationLimits.MaximumRecoveryPoints &&
               severityReduction <=
               proof.OperationLimits.MaximumSeverityReductionSteps &&
               cosmeticLegacies <= proof.OperationLimits.MaximumCosmeticHealLegacies &&
               mechanicalLegacies <=
               proof.OperationLimits.MaximumMechanicalEffectHealLegacies;
    }

    private sealed record QuantitySnapshot(
        long Current,
        long Available,
        string ReservationState,
        string Lifecycle,
        bool Active,
        long AggregateRequested);

    private static bool HasValidRequirementAuthority(
        MortalWoundTreatmentAttemptRequest request,
        MortalWoundTreatmentRouteDefinition route)
    {
        var value = request.RequirementAuthority;
        var coordinates = request.Coordinates;
        if (value is null || value.Scopes is null ||
            !string.Equals(value.Mode, request.Mode, StringComparison.Ordinal) ||
            !string.Equals(value.ContextFingerprint, coordinates.ContextFingerprint,
                StringComparison.Ordinal) ||
            !string.Equals(
                value.AcceptedStateFingerprint,
                coordinates.AcceptedStateFingerprint,
                StringComparison.Ordinal) ||
            !string.Equals(
                value.RouteFingerprint,
                request.ResourceAuthority.RouteFingerprint,
                StringComparison.Ordinal) ||
            route.Requirements.Length >
            WoundMaterializationContract.MaxRequirementsPerTreatmentMember)
        {
            return false;
        }

        var context = new MortalWoundTreatmentAuthority.Context(
            1,
            coordinates.Realm,
            coordinates.TargetKind,
            coordinates.TargetId,
            coordinates.ProviderKind,
            coordinates.ProviderId,
            coordinates.LocationId);
        var aggregate = new Dictionary<string, QuantitySnapshot>(StringComparer.Ordinal);
        if (request.Mode is "procedure" or "guaranteed")
        {
            if (value.Scopes.Count != 1 ||
                !HasValidRequirementScope(
                    value.Scopes[0],
                    "common",
                    null,
                    route.Requirements,
                    coordinates,
                    context,
                    aggregate))
            {
                return false;
            }
        }
        else
        {
            if (route is not MortalWoundCourseRouteDefinition courseRoute ||
                request.MilestoneOrdinal is not { } ordinal)
            {
                return false;
            }
            var milestones = courseRoute.Milestones.Where(candidate =>
                candidate.Ordinal == ordinal).ToArray();
            if (milestones.Length != 1 ||
                milestones[0].Requirements.Length >
                WoundMaterializationContract.MaxRequirementsPerTreatmentMember ||
                value.Scopes.Count != 2 ||
                !HasValidRequirementScope(
                    value.Scopes[0],
                    "common",
                    null,
                    route.Requirements,
                    coordinates,
                    context,
                    aggregate) ||
                !HasValidRequirementScope(
                    value.Scopes[1],
                    "course_milestone",
                    ordinal,
                    milestones[0].Requirements,
                    coordinates,
                    context,
                    aggregate))
            {
                return false;
            }
        }

        if (!TryValidateScopeTopology(request, out _))
            return false;
        var fields = new List<string?>
        {
            "book_of_eternity.mortal_wound_treatment.requirement_bundle", "1",
            value.Mode, value.ContextFingerprint, value.AcceptedStateFingerprint,
            value.RouteFingerprint, value.CourseId,
            NullableNumber(value.CourseMilestoneOrdinal),
            value.CourseCoordinateFingerprint, value.CourseRequirementStatus,
            value.InterruptionReason, Number(value.Scopes.Count)
        };
        fields.AddRange(value.Scopes.Select(static scope => scope.AuthorityFingerprint));
        return string.Equals(
            value.AuthorityFingerprint,
            WoundAcceptedTurnFingerprintWriter.Compute(fields),
            StringComparison.Ordinal);
    }

    private static bool HasValidRequirementScope(
        MortalWoundTreatmentRequirementScopeAuthority value,
        string expectedScope,
        int? expectedOrdinal,
        IReadOnlyList<MortalWoundTreatmentRequirement> requirements,
        MortalWoundTreatmentAttemptCoordinates coordinates,
        MortalWoundTreatmentAuthority.Context context,
        IDictionary<string, QuantitySnapshot> aggregate)
    {
        if (value is null || value.Bindings is null ||
            value.FailureWitnesses is null ||
            !string.Equals(value.Scope, expectedScope, StringComparison.Ordinal) ||
            value.CourseMilestoneOrdinal != expectedOrdinal ||
            requirements.Count >
            WoundMaterializationContract.MaxRequirementsPerTreatmentMember ||
            value.Bindings.Count + value.FailureWitnesses.Count != requirements.Count ||
            value.Bindings.Count + value.FailureWitnesses.Count >
            WoundMaterializationContract.MaxRequirementsPerTreatmentMember ||
            !IsStrictlyIncreasing(value.Bindings.Select(static binding =>
                binding.RequirementIndex)) ||
            !IsStrictlyIncreasing(value.FailureWitnesses.Select(static failure =>
                failure.RequirementIndex)))
        {
            return false;
        }

        var bindings = value.Bindings.ToDictionary(
            static binding => binding.RequirementIndex);
        var failures = value.FailureWitnesses.ToDictionary(
            static failure => failure.RequirementIndex);
        if (bindings.Count != value.Bindings.Count ||
            failures.Count != value.FailureWitnesses.Count ||
            bindings.Keys.Intersect(failures.Keys).Any())
        {
            return false;
        }

        var local = new Dictionary<string, long>(StringComparer.Ordinal);
        for (var index = 0; index < requirements.Count; index++)
        {
            if (bindings.TryGetValue(index, out var binding))
            {
                if (!HasValidRequirementBinding(
                        binding,
                        expectedScope,
                        index,
                        requirements[index],
                        coordinates,
                        context,
                        local,
                        aggregate))
                {
                    return false;
                }
            }
            else if (!failures.TryGetValue(index, out var failure) ||
                     !HasValidFailureWitness(
                         failure,
                         expectedScope,
                         index,
                         requirements[index],
                         coordinates,
                         context,
                         local,
                         aggregate))
            {
                return false;
            }
        }

        var expectedStatus = value.FailureWitnesses.Count == 0
            ? "Satisfied"
            : "Unsatisfied";
        var fields = new List<string?>
        {
            "book_of_eternity.mortal_wound_treatment.requirement_scope", "1",
            value.Scope, NullableNumber(value.CourseMilestoneOrdinal), value.Status,
            Number(value.Bindings.Count)
        };
        fields.AddRange(value.Bindings.Select(static binding =>
            binding.BindingFingerprint));
        fields.Add(Number(value.FailureWitnesses.Count));
        fields.AddRange(value.FailureWitnesses.Select(static failure =>
            failure.WitnessFingerprint));
        return string.Equals(value.Status, expectedStatus, StringComparison.Ordinal) &&
               string.Equals(
                   value.AuthorityFingerprint,
                   WoundAcceptedTurnFingerprintWriter.Compute(fields),
                   StringComparison.Ordinal);
    }

    // Kept as a narrow structural seam for the existing detached-scope guard tests.
    // Request admission always uses the route-aware overload above.
    private static bool HasValidRequirementScope(
        MortalWoundTreatmentRequirementScopeAuthority value)
    {
        if (value is null || value.Bindings is null ||
            value.FailureWitnesses is null ||
            value.Bindings.Count + value.FailureWitnesses.Count >
            WoundMaterializationContract.MaxRequirementsPerTreatmentMember ||
            value.Bindings.Any(binding =>
                binding is null || binding.SuccessWitness is null ||
                !string.Equals(binding.SuccessWitness.Scope, value.Scope,
                    StringComparison.Ordinal) ||
                !HasValidRequirementBindingSeal(binding)) ||
            value.FailureWitnesses.Any(failure =>
                failure is null ||
                !string.Equals(failure.Scope, value.Scope,
                    StringComparison.Ordinal) ||
                !HasValidFailureWitnessSeal(failure)))
        {
            return false;
        }
        var indices = value.Bindings.Select(static binding => binding.RequirementIndex)
            .Concat(value.FailureWitnesses.Select(static failure =>
                failure.RequirementIndex))
            .OrderBy(static index => index)
            .ToArray();
        if (!indices.SequenceEqual(Enumerable.Range(0, indices.Length)))
            return false;
        var expectedStatus = value.FailureWitnesses.Count == 0
            ? "Satisfied"
            : "Unsatisfied";
        var fields = new List<string?>
        {
            "book_of_eternity.mortal_wound_treatment.requirement_scope", "1",
            value.Scope, NullableNumber(value.CourseMilestoneOrdinal), value.Status,
            Number(value.Bindings.Count)
        };
        fields.AddRange(value.Bindings.Select(static binding =>
            binding.BindingFingerprint));
        fields.Add(Number(value.FailureWitnesses.Count));
        fields.AddRange(value.FailureWitnesses.Select(static failure =>
            failure.WitnessFingerprint));
        return value.Status == expectedStatus &&
               value.AuthorityFingerprint ==
               WoundAcceptedTurnFingerprintWriter.Compute(fields);
    }

    private static bool HasValidRequirementBindingSeal(
        MortalWoundTreatmentRequirementBinding value)
    {
        var row = value.ResolvedRequirement;
        var witness = value.SuccessWitness;
        if (row is null || witness is null || witness.Evidence is null ||
            value.RequirementIndex != row.RequirementIndex ||
            value.RequirementIndex != witness.RequirementIndex ||
            witness.Kind != row.Kind || witness.AuthorityRef != row.AuthorityRef ||
            !TryMechanicalFields(row, witness.Evidence, out var mechanicalFields) ||
            !HasValidSuccessWitnessSeal(row, witness, mechanicalFields))
        {
            return false;
        }
        return value.BindingFingerprint ==
               WoundAcceptedTurnFingerprintWriter.Compute(new string?[]
               {
                   "book_of_eternity.mortal_wound_treatment.requirement_binding", "1",
                   witness.Scope, Number(row.RequirementIndex), row.AuthorityFingerprint,
                   witness.WitnessFingerprint
               });
    }

    private static bool HasValidFailureWitnessSeal(
        MortalWoundTreatmentRequirementFailureWitness value)
    {
        if (value.RequirementIndex < 0 ||
            value.Observation is { } observation &&
            !HasValidFailureObservationSeal(value, observation))
        {
            return false;
        }
        return value.WitnessFingerprint ==
               WoundAcceptedTurnFingerprintWriter.Compute(new string?[]
               {
                   "book_of_eternity.mortal_wound_treatment.requirement_failure_witness",
                   "1", value.Scope, Number(value.RequirementIndex), value.Kind,
                   value.AuthorityRef, value.LossReason,
                   value.Observation?.ObservationFingerprint
               });
    }

    private static bool HasValidFailureObservationSeal(
        MortalWoundTreatmentRequirementFailureWitness failure,
        MortalWoundTreatmentRequirementFailureObservation observation)
    {
        if (observation.Evidence is null)
            return false;
        var row = FailureRow(failure, observation);
        if (!TryMechanicalFields(row, observation.Evidence, out var mechanicalFields))
            return false;
        var fields = new List<string?>
        {
            "book_of_eternity.mortal_wound_treatment.requirement_failure_observation", "1",
            observation.SnapshotToken, observation.Realm, observation.OwnerKind,
            observation.OwnerId, observation.ProviderKind, observation.ProviderId,
            observation.TargetKind, observation.TargetId, observation.LocationId,
            observation.Evidence.Kind, Number(mechanicalFields.Count)
        };
        fields.AddRange(mechanicalFields);
        var evidenceFields = EvidenceFields(observation.Evidence);
        fields.Add(Number(evidenceFields.Count));
        fields.AddRange(evidenceFields);
        return observation.ObservationFingerprint ==
               WoundAcceptedTurnFingerprintWriter.Compute(fields);
    }

    private static bool HasValidRequirementBinding(
        MortalWoundTreatmentRequirementBinding value,
        string scope,
        int index,
        MortalWoundTreatmentRequirement requirement,
        MortalWoundTreatmentAttemptCoordinates coordinates,
        MortalWoundTreatmentAuthority.Context context,
        IDictionary<string, long> local,
        IDictionary<string, QuantitySnapshot> aggregate)
    {
        if (value is null || value.ResolvedRequirement is null ||
            value.SuccessWitness is null)
        {
            return false;
        }
        var row = value.ResolvedRequirement;
        var witness = value.SuccessWitness;
        if (value.RequirementIndex != index || row.RequirementIndex != index ||
            witness.RequirementIndex != index ||
            !string.Equals(witness.Scope, scope, StringComparison.Ordinal) ||
            !string.Equals(witness.Kind, row.Kind, StringComparison.Ordinal) ||
            !string.Equals(witness.AuthorityRef, row.AuthorityRef,
                StringComparison.Ordinal) ||
            !string.Equals(witness.SnapshotToken, coordinates.SnapshotToken,
                StringComparison.Ordinal) ||
            !TryValidateSuccessfulRequirement(
                requirement,
                row,
                witness.Evidence,
                coordinates,
                local,
                aggregate) ||
            !TryMechanicalFields(row, witness.Evidence, out var mechanicalFields) ||
            !string.Equals(
                row.AuthorityFingerprint,
                MortalWoundTreatmentAuthority.RecomputeResolvedRequirementFingerprint(
                    row,
                    context,
                    SerializeRequirement(requirement),
                    mechanicalFields),
                StringComparison.Ordinal) ||
            !HasValidSuccessWitnessSeal(row, witness, mechanicalFields))
        {
            return false;
        }
        var fingerprint = WoundAcceptedTurnFingerprintWriter.Compute(new string?[]
        {
            "book_of_eternity.mortal_wound_treatment.requirement_binding", "1",
            witness.Scope, Number(row.RequirementIndex), row.AuthorityFingerprint,
            witness.WitnessFingerprint
        });
        return string.Equals(value.BindingFingerprint, fingerprint,
            StringComparison.Ordinal);
    }

    private static bool HasValidSuccessWitnessSeal(
        MortalWoundResolvedRequirement row,
        MortalWoundTreatmentRequirementSuccessWitness witness,
        IReadOnlyList<string?> mechanicalFields)
    {
        var fields = new List<string?>
        {
            "book_of_eternity.mortal_wound_treatment.requirement_success_witness", "1",
            witness.Scope, Number(row.RequirementIndex), row.Kind, row.AuthorityRef,
            witness.SnapshotToken, row.Realm, row.OwnerKind, row.OwnerId,
            row.ProviderKind, row.ProviderId, row.TargetKind, row.TargetId,
            row.LocationId, witness.Evidence.Kind, Number(mechanicalFields.Count)
        };
        fields.AddRange(mechanicalFields);
        var evidenceFields = EvidenceFields(witness.Evidence);
        fields.Add(Number(evidenceFields.Count));
        fields.AddRange(evidenceFields);
        return string.Equals(witness.Realm, row.Realm, StringComparison.Ordinal) &&
               string.Equals(witness.OwnerKind, row.OwnerKind, StringComparison.Ordinal) &&
               string.Equals(witness.OwnerId, row.OwnerId, StringComparison.Ordinal) &&
               string.Equals(witness.ProviderKind, row.ProviderKind,
                   StringComparison.Ordinal) &&
               string.Equals(witness.ProviderId, row.ProviderId,
                   StringComparison.Ordinal) &&
               string.Equals(witness.TargetKind, row.TargetKind,
                   StringComparison.Ordinal) &&
               string.Equals(witness.TargetId, row.TargetId,
                   StringComparison.Ordinal) &&
               string.Equals(witness.LocationId, row.LocationId,
                   StringComparison.Ordinal) &&
               string.Equals(
                   witness.WitnessFingerprint,
                   WoundAcceptedTurnFingerprintWriter.Compute(fields),
                   StringComparison.Ordinal);
    }

    private static bool HasValidFailureWitness(
        MortalWoundTreatmentRequirementFailureWitness value,
        string scope,
        int index,
        MortalWoundTreatmentRequirement requirement,
        MortalWoundTreatmentAttemptCoordinates coordinates,
        MortalWoundTreatmentAuthority.Context context,
        IDictionary<string, long> local,
        IDictionary<string, QuantitySnapshot> aggregate)
    {
        if (value is null || value.RequirementIndex != index ||
            !string.Equals(value.Scope, scope, StringComparison.Ordinal) ||
            !RequirementMatches(value.Kind, value.AuthorityRef, requirement))
        {
            return false;
        }
        if (string.Equals(value.LossReason, "authority_absent",
                StringComparison.Ordinal))
        {
            if (value.Observation is not null)
                return false;
        }
        else if (!IsClosedObservedLossReason(value.LossReason) ||
                 value.Observation is not { } observation ||
                 !HasValidFailureObservation(
                     value,
                     observation,
                     requirement,
                     coordinates,
                     context,
                     local,
                     aggregate))
        {
            return false;
        }

        var fingerprint = WoundAcceptedTurnFingerprintWriter.Compute(new string?[]
        {
            "book_of_eternity.mortal_wound_treatment.requirement_failure_witness", "1",
            value.Scope, Number(value.RequirementIndex), value.Kind,
            value.AuthorityRef, value.LossReason,
            value.Observation?.ObservationFingerprint
        });
        return string.Equals(value.WitnessFingerprint, fingerprint,
            StringComparison.Ordinal);
    }

    private static bool HasValidFailureObservation(
        MortalWoundTreatmentRequirementFailureWitness failure,
        MortalWoundTreatmentRequirementFailureObservation observation,
        MortalWoundTreatmentRequirement requirement,
        MortalWoundTreatmentAttemptCoordinates coordinates,
        MortalWoundTreatmentAuthority.Context context,
        IDictionary<string, long> local,
        IDictionary<string, QuantitySnapshot> aggregate)
    {
        if (observation.Evidence is null ||
            !string.Equals(observation.SnapshotToken, coordinates.SnapshotToken,
                StringComparison.Ordinal) ||
            !string.Equals(observation.Realm, coordinates.Realm,
                StringComparison.Ordinal))
        {
            return false;
        }
        var row = FailureRow(failure, observation);
        if (!TryValidateFailedRequirement(
                requirement,
                row,
                observation.Evidence,
                failure.LossReason,
                coordinates,
                local,
                aggregate) ||
            !TryMechanicalFields(row, observation.Evidence, out var mechanicalFields))
        {
            return false;
        }
        var fields = new List<string?>
        {
            "book_of_eternity.mortal_wound_treatment.requirement_failure_observation", "1",
            observation.SnapshotToken, observation.Realm, observation.OwnerKind,
            observation.OwnerId, observation.ProviderKind, observation.ProviderId,
            observation.TargetKind, observation.TargetId, observation.LocationId,
            observation.Evidence.Kind, Number(mechanicalFields.Count)
        };
        fields.AddRange(mechanicalFields);
        var evidenceFields = EvidenceFields(observation.Evidence);
        fields.Add(Number(evidenceFields.Count));
        fields.AddRange(evidenceFields);
        return string.Equals(
            observation.ObservationFingerprint,
            WoundAcceptedTurnFingerprintWriter.Compute(fields),
            StringComparison.Ordinal);
    }

    private static bool TryValidateSuccessfulRequirement(
        MortalWoundTreatmentRequirement requirement,
        MortalWoundResolvedRequirement row,
        MortalWoundTreatmentRequirementEvidence evidence,
        MortalWoundTreatmentAttemptCoordinates coordinates,
        IDictionary<string, long> local,
        IDictionary<string, QuantitySnapshot> aggregate)
    {
        if (row is null || evidence is null ||
            !string.Equals(row.Realm, coordinates.Realm, StringComparison.Ordinal))
        {
            return false;
        }
        switch (requirement, evidence)
        {
            case (MortalWoundItemQuantityRequirement authored,
                MortalWoundItemQuantityRequirementEvidence item):
                return TrySelectRole(coordinates, authored.OwnerRole, out var itemOwner) &&
                       ExactRow(
                           row, requirement.Kind, authored.ItemRef, coordinates.Realm,
                           itemOwner.ActorKind, itemOwner.ActorId, null, null, null, null, null,
                           authored.Quantity, null, null, null) &&
                       string.Equals(item.OwnerRole, authored.OwnerRole,
                           StringComparison.Ordinal) &&
                       item.RequestedQuantity == authored.Quantity &&
                       TryValidateQuantitySuccess(
                           row, item.Count, item.AvailableCount,
                           item.ReservationState, item.Lifecycle, item.Active,
                           item.CumulativeRequestedQuantity, local, aggregate,
                           requirePositiveCurrent: true);
            case (MortalWoundResourceQuantityRequirement authored,
                MortalWoundResourceQuantityRequirementEvidence resource):
                return TrySelectRole(
                           coordinates,
                           authored.OwnerRole,
                           out var resourceOwner) &&
                       ExactRow(
                           row, requirement.Kind, authored.ResourceRef,
                           coordinates.Realm, resourceOwner.ActorKind, resourceOwner.ActorId,
                           null, null, null, null, null, authored.Quantity,
                           null, null, null) &&
                       string.Equals(resource.OwnerRole, authored.OwnerRole,
                           StringComparison.Ordinal) &&
                       resource.RequestedQuantity == authored.Quantity &&
                       TryValidateQuantitySuccess(
                           row, resource.CurrentValue, resource.AvailableValue,
                           resource.ReservationState, resource.Lifecycle,
                           resource.Active, resource.CumulativeRequestedQuantity,
                           local, aggregate, requirePositiveCurrent: false);
            case (MortalWoundSkillTierRequirement authored,
                MortalWoundSkillTierRequirementEvidence skill):
                return TrySelectRole(coordinates, authored.ActorRole, out var skillOwner) &&
                       ExactAcceptedActorRow(
                           row, requirement.Kind, authored.CapabilityRef,
                           coordinates, skillOwner, null, authored.MinimumTier,
                           skill.CurrentTier) &&
                       string.Equals(skill.ActorRole, authored.ActorRole,
                           StringComparison.Ordinal) &&
                       skill.MinimumTier == authored.MinimumTier &&
                       skill.CurrentTier >= authored.MinimumTier &&
                       IsExactIdentifier(skill.ActorCurrentLocationId) &&
                       string.Equals(skill.RequiredLocationId, coordinates.LocationId,
                           StringComparison.Ordinal) &&
                       IsCurrentActive(skill.ActorLifecycle, skill.ActorActive) &&
                       IsCurrentActive(skill.SkillLifecycle, skill.SkillActive);
            case (MortalWoundSourceCapabilityRequirement authored,
                MortalWoundSourceCapabilityRequirementEvidence capability):
                return TrySelectRole(
                           coordinates,
                           authored.ActorRole,
                           out var capabilityOwner) &&
                       ExactAcceptedActorRow(
                           row, requirement.Kind, authored.CapabilityRef,
                           coordinates, capabilityOwner, null, null, null) &&
                       string.Equals(capability.ActorRole, authored.ActorRole,
                           StringComparison.Ordinal) &&
                       IsExactIdentifier(capability.ActorCurrentLocationId) &&
                       string.Equals(
                           capability.RequiredLocationId,
                           coordinates.LocationId,
                           StringComparison.Ordinal) &&
                       IsCurrentActive(
                           capability.ActorLifecycle,
                           capability.ActorActive) &&
                       IsCurrentActive(
                           capability.CapabilityLifecycle,
                           capability.CapabilityActive);
            case (MortalWoundProviderRequirement authored,
                MortalWoundProviderRequirementEvidence provider):
                return string.Equals(authored.ProviderRef, coordinates.ProviderId,
                           StringComparison.Ordinal) &&
                       ExactRow(
                           row, requirement.Kind, authored.ProviderRef,
                           coordinates.Realm, null, null, coordinates.ProviderKind,
                           coordinates.ProviderId, null, null, coordinates.LocationId,
                           null, null, null, null) &&
                       string.Equals(provider.CurrentLocationId,
                           coordinates.LocationId, StringComparison.Ordinal) &&
                       string.Equals(provider.RequiredLocationId,
                           coordinates.LocationId, StringComparison.Ordinal) &&
                       IsCurrentActive(provider.Lifecycle, provider.Active) &&
                       provider.Reachable && provider.Present;
            case (MortalWoundConsentRequirement authored,
                MortalWoundConsentRequirementEvidence consent):
                return string.Equals(authored.ProviderRef, coordinates.ProviderId,
                           StringComparison.Ordinal) &&
                       string.Equals(authored.TargetRef, coordinates.TargetId,
                           StringComparison.Ordinal) &&
                       ExactRow(
                           row, requirement.Kind, authored.ConsentRef,
                           coordinates.Realm, null, null, coordinates.ProviderKind,
                           coordinates.ProviderId, coordinates.TargetKind,
                           coordinates.TargetId, null, null, null, null,
                           consent.Status) &&
                       ConsentEvidenceMatches(
                           consent, authored, coordinates) &&
                       string.Equals(consent.Status, "granted",
                           StringComparison.Ordinal) &&
                       IsCurrentActive(
                           consent.ProviderLifecycle,
                           consent.ProviderActive) &&
                       IsCurrentActive(consent.Lifecycle, consent.Active);
            case (MortalWoundFacilityRequirement authored,
                MortalWoundFacilityRequirementEvidence facility):
                return ExactRow(
                           row, requirement.Kind, authored.FacilityRef,
                           coordinates.Realm, null, null, null, null, null, null,
                           coordinates.LocationId, null, null, null, null) &&
                       string.Equals(facility.FacilityLocationId,
                           coordinates.LocationId, StringComparison.Ordinal) &&
                       string.Equals(facility.RequiredLocationId,
                           coordinates.LocationId, StringComparison.Ordinal) &&
                       string.Equals(facility.RequiredActorKind,
                           coordinates.TargetKind, StringComparison.Ordinal) &&
                       string.Equals(facility.RequiredActorId,
                           coordinates.TargetId, StringComparison.Ordinal) &&
                       IsCurrentActive(facility.Lifecycle, facility.Active) &&
                       facility.Available;
            case (MortalWoundLocationRequirement authored,
                MortalWoundLocationRequirementEvidence location):
                return string.Equals(authored.LocationRef, coordinates.LocationId,
                           StringComparison.Ordinal) &&
                       ExactRow(
                           row, requirement.Kind, authored.LocationRef,
                           coordinates.Realm, null, null, null, null,
                           coordinates.TargetKind, coordinates.TargetId,
                           authored.LocationRef, null, null, null, null) &&
                       LocationEvidenceMatches(
                           location, authored, coordinates, requirePresent: true) &&
                       IsCurrentActive(location.Lifecycle, location.Active);
            case (MortalWoundQuestStateRequirement authored,
                MortalWoundQuestStateRequirementEvidence quest):
                return ExactRow(
                           row, requirement.Kind, authored.QuestRef,
                           coordinates.Realm, null, null, null, null, null, null,
                           null, null, null, null, authored.RequiredState) &&
                       string.Equals(quest.RequiredState, authored.RequiredState,
                           StringComparison.Ordinal) &&
                       string.Equals(quest.CurrentState, authored.RequiredState,
                           StringComparison.Ordinal) &&
                       IsCurrentActive(quest.Lifecycle, quest.Active);
            case (MortalWoundEffectStateRequirement authored,
                MortalWoundEffectStateRequirementEvidence effect):
                return ExactRow(
                           row, requirement.Kind, authored.EffectRef,
                           coordinates.Realm, null, null, null, null,
                           coordinates.TargetKind, coordinates.TargetId, null,
                           null, null, null, authored.RequiredState) &&
                       string.Equals(authored.TargetRole, "target",
                           StringComparison.Ordinal) &&
                       string.Equals(effect.RequiredState, authored.RequiredState,
                           StringComparison.Ordinal) &&
                       string.Equals(effect.CurrentState, authored.RequiredState,
                           StringComparison.Ordinal) &&
                       IsCurrentActive(effect.Lifecycle, effect.Active);
            case (MortalWoundEnvironmentRequirement authored,
                MortalWoundEnvironmentRequirementEvidence environment):
                return ExactRow(
                           row, requirement.Kind, authored.EnvironmentRef,
                           coordinates.Realm, null, null, null, null, null, null,
                           coordinates.LocationId, null, null, null,
                           authored.RequiredState) &&
                       string.Equals(environment.RequiredState,
                           authored.RequiredState, StringComparison.Ordinal) &&
                       string.Equals(environment.CurrentState,
                           authored.RequiredState, StringComparison.Ordinal) &&
                       string.Equals(environment.CurrentLocationId,
                           coordinates.LocationId, StringComparison.Ordinal) &&
                       string.Equals(environment.RequiredLocationId,
                           coordinates.LocationId, StringComparison.Ordinal) &&
                       IsCurrentActive(environment.Lifecycle, environment.Active);
            default:
                return false;
        }
    }

    private static bool TryValidateFailedRequirement(
        MortalWoundTreatmentRequirement requirement,
        MortalWoundResolvedRequirement row,
        MortalWoundTreatmentRequirementEvidence evidence,
        string lossReason,
        MortalWoundTreatmentAttemptCoordinates coordinates,
        IDictionary<string, long> local,
        IDictionary<string, QuantitySnapshot> aggregate)
    {
        string? expectedReason;
        switch (requirement, evidence)
        {
            case (MortalWoundItemQuantityRequirement authored,
                MortalWoundItemQuantityRequirementEvidence item):
                if (!TrySelectRole(coordinates, authored.OwnerRole, out var itemOwner) ||
                    !ExactFailureQuantityRow(
                        row, requirement.Kind, authored.ItemRef, coordinates,
                        authored.Quantity) ||
                    !string.Equals(item.OwnerRole, authored.OwnerRole,
                        StringComparison.Ordinal) ||
                    item.RequestedQuantity != authored.Quantity ||
                    !TryClassifyQuantityFailure(
                        row, itemOwner, item.Count, item.AvailableCount,
                        item.ReservationState, item.Lifecycle, item.Active,
                        item.CumulativeRequestedQuantity, local, aggregate,
                        requirePositiveCurrent: true, out expectedReason))
                {
                    return false;
                }
                break;
            case (MortalWoundResourceQuantityRequirement authored,
                MortalWoundResourceQuantityRequirementEvidence resource):
                if (!TrySelectRole(coordinates, authored.OwnerRole, out var resourceOwner) ||
                    !ExactFailureQuantityRow(
                        row, requirement.Kind, authored.ResourceRef, coordinates,
                        authored.Quantity) ||
                    !string.Equals(resource.OwnerRole, authored.OwnerRole,
                        StringComparison.Ordinal) ||
                    resource.RequestedQuantity != authored.Quantity ||
                    !TryClassifyQuantityFailure(
                        row, resourceOwner, resource.CurrentValue,
                        resource.AvailableValue, resource.ReservationState,
                        resource.Lifecycle, resource.Active,
                        resource.CumulativeRequestedQuantity, local, aggregate,
                        requirePositiveCurrent: false, out expectedReason))
                {
                    return false;
                }
                break;
            case (MortalWoundSkillTierRequirement authored,
                MortalWoundSkillTierRequirementEvidence skill):
                if (!TrySelectRole(coordinates, authored.ActorRole, out var skillOwner) ||
                    !ExactRow(
                        row, requirement.Kind, authored.CapabilityRef,
                        coordinates.Realm, row.OwnerKind, row.OwnerId, null, null,
                        null, null, null, null, authored.MinimumTier,
                        skill.CurrentTier, null) ||
                    !ValidActorCoordinate(row.OwnerKind, row.OwnerId) ||
                    !string.Equals(skill.ActorRole, authored.ActorRole,
                        StringComparison.Ordinal) ||
                    skill.MinimumTier != authored.MinimumTier ||
                    !IsExactIdentifier(skill.ActorCurrentLocationId) ||
                    !string.Equals(skill.RequiredLocationId, coordinates.LocationId,
                        StringComparison.Ordinal) ||
                    !IsClosedLifecycle(skill.ActorLifecycle) ||
                    !IsClosedLifecycle(skill.SkillLifecycle))
                {
                    return false;
                }
                expectedReason = !SameActor(row, skillOwner)
                    ? "owner_unavailable"
                    : IsRetired(skill.ActorLifecycle, skill.SkillLifecycle)
                        ? "retired"
                        : !skill.ActorActive || !skill.SkillActive
                            ? "inactive"
                            : skill.CurrentTier < authored.MinimumTier
                                ? "tier_insufficient"
                                : null;
                break;
            case (MortalWoundSourceCapabilityRequirement authored,
                MortalWoundSourceCapabilityRequirementEvidence capability):
                if (!TrySelectRole(coordinates, authored.ActorRole, out var capabilityOwner) ||
                    !ExactRow(
                        row, requirement.Kind, authored.CapabilityRef,
                        coordinates.Realm, row.OwnerKind, row.OwnerId, null, null,
                        null, null, null, null, null, null, null) ||
                    !ValidActorCoordinate(row.OwnerKind, row.OwnerId) ||
                    !string.Equals(capability.ActorRole, authored.ActorRole,
                        StringComparison.Ordinal) ||
                    !IsExactIdentifier(capability.ActorCurrentLocationId) ||
                    !string.Equals(capability.RequiredLocationId,
                        coordinates.LocationId, StringComparison.Ordinal) ||
                    !IsClosedLifecycle(capability.ActorLifecycle) ||
                    !IsClosedLifecycle(capability.CapabilityLifecycle))
                {
                    return false;
                }
                expectedReason = !SameActor(row, capabilityOwner)
                    ? "owner_unavailable"
                    : IsRetired(
                        capability.ActorLifecycle,
                        capability.CapabilityLifecycle)
                        ? "retired"
                        : !capability.ActorActive || !capability.CapabilityActive
                            ? "inactive"
                            : null;
                break;
            case (MortalWoundProviderRequirement authored,
                MortalWoundProviderRequirementEvidence provider):
                if (!string.Equals(authored.ProviderRef, coordinates.ProviderId,
                        StringComparison.Ordinal) ||
                    !ExactRow(
                        row, requirement.Kind, authored.ProviderRef,
                        coordinates.Realm, null, null, coordinates.ProviderKind,
                        coordinates.ProviderId, null, null, coordinates.LocationId,
                        null, null, null, null) ||
                    !IsExactIdentifier(provider.CurrentLocationId) ||
                    !string.Equals(provider.RequiredLocationId,
                        coordinates.LocationId, StringComparison.Ordinal) ||
                    !IsClosedLifecycle(provider.Lifecycle))
                {
                    return false;
                }
                expectedReason = provider.Lifecycle == "retired"
                    ? "retired"
                    : !provider.Active
                        ? "inactive"
                        : !provider.Reachable
                            ? "provider_unreachable"
                            : !string.Equals(provider.CurrentLocationId,
                                provider.RequiredLocationId,
                                StringComparison.Ordinal)
                                ? "wrong_location"
                                : !provider.Present ? "actor_not_present" : null;
                break;
            case (MortalWoundConsentRequirement authored,
                MortalWoundConsentRequirementEvidence consent):
                if (!string.Equals(authored.ProviderRef, coordinates.ProviderId,
                        StringComparison.Ordinal) ||
                    !string.Equals(authored.TargetRef, coordinates.TargetId,
                        StringComparison.Ordinal) ||
                    !ExactRow(
                        row, requirement.Kind, authored.ConsentRef,
                        coordinates.Realm, null, null, coordinates.ProviderKind,
                        coordinates.ProviderId, coordinates.TargetKind,
                        coordinates.TargetId, null, null, null, null,
                        consent.Status) ||
                    !ConsentEvidenceMatches(consent, authored, coordinates) ||
                    !IsClosedLifecycle(consent.ProviderLifecycle) ||
                    !IsClosedLifecycle(consent.Lifecycle) ||
                    consent.Status is not ("granted" or "withdrawn"))
                {
                    return false;
                }
                expectedReason = IsRetired(
                        consent.ProviderLifecycle,
                        consent.Lifecycle)
                    ? "retired"
                    : consent.Status != "granted"
                        ? "consent_absent"
                        : !consent.ProviderActive || !consent.Active
                            ? "inactive"
                            : null;
                break;
            case (MortalWoundFacilityRequirement authored,
                MortalWoundFacilityRequirementEvidence facility):
                if (!ExactRow(
                        row, requirement.Kind, authored.FacilityRef,
                        coordinates.Realm, null, null, null, null, null, null,
                        facility.FacilityLocationId, null, null, null, null) ||
                    !IsExactIdentifier(facility.FacilityLocationId) ||
                    !string.Equals(facility.RequiredLocationId,
                        coordinates.LocationId, StringComparison.Ordinal) ||
                    !string.Equals(facility.RequiredActorKind,
                        coordinates.TargetKind, StringComparison.Ordinal) ||
                    !string.Equals(facility.RequiredActorId,
                        coordinates.TargetId, StringComparison.Ordinal) ||
                    !IsClosedLifecycle(facility.Lifecycle))
                {
                    return false;
                }
                expectedReason = facility.Lifecycle == "retired"
                    ? "retired"
                    : !string.Equals(facility.FacilityLocationId,
                        facility.RequiredLocationId, StringComparison.Ordinal)
                        ? "wrong_location"
                        : !facility.Active
                            ? "inactive"
                            : !facility.Available ? "facility_unavailable" : null;
                break;
            case (MortalWoundLocationRequirement authored,
                MortalWoundLocationRequirementEvidence location):
                if (!ExactRow(
                        row, requirement.Kind, authored.LocationRef,
                        coordinates.Realm, null, null, null, null,
                        coordinates.TargetKind, coordinates.TargetId,
                        authored.LocationRef, null, null, null, null) ||
                    !LocationEvidenceMatches(
                        location, authored, coordinates, requirePresent: false) ||
                    !IsClosedLifecycle(location.Lifecycle))
                {
                    return false;
                }
                expectedReason = location.Lifecycle == "retired"
                    ? "retired"
                    : !location.Active
                        ? "inactive"
                        : !string.Equals(authored.LocationRef,
                            coordinates.LocationId, StringComparison.Ordinal)
                            ? "wrong_location"
                            : !location.TargetPresent ? "actor_not_present" : null;
                break;
            case (MortalWoundQuestStateRequirement authored,
                MortalWoundQuestStateRequirementEvidence quest):
                if (!ExactRow(
                        row, requirement.Kind, authored.QuestRef,
                        coordinates.Realm, null, null, null, null, null, null,
                        null, null, null, null, quest.CurrentState) ||
                    !string.Equals(quest.RequiredState, authored.RequiredState,
                        StringComparison.Ordinal) ||
                    !IsExactIdentifier(quest.CurrentState) ||
                    !IsClosedLifecycle(quest.Lifecycle))
                {
                    return false;
                }
                expectedReason = ClassifyStateFailure(
                    quest.Lifecycle, quest.Active, quest.CurrentState,
                    authored.RequiredState);
                break;
            case (MortalWoundEffectStateRequirement authored,
                MortalWoundEffectStateRequirementEvidence effect):
                if (!ExactRow(
                        row, requirement.Kind, authored.EffectRef,
                        coordinates.Realm, null, null, null, null,
                        coordinates.TargetKind, coordinates.TargetId, null,
                        null, null, null, effect.CurrentState) ||
                    !string.Equals(authored.TargetRole, "target",
                        StringComparison.Ordinal) ||
                    !string.Equals(effect.RequiredState, authored.RequiredState,
                        StringComparison.Ordinal) ||
                    !IsExactIdentifier(effect.CurrentState) ||
                    !IsClosedLifecycle(effect.Lifecycle))
                {
                    return false;
                }
                expectedReason = ClassifyStateFailure(
                    effect.Lifecycle, effect.Active, effect.CurrentState,
                    authored.RequiredState);
                break;
            case (MortalWoundEnvironmentRequirement authored,
                MortalWoundEnvironmentRequirementEvidence environment):
                if (!ExactRow(
                        row, requirement.Kind, authored.EnvironmentRef,
                        coordinates.Realm, null, null, null, null, null, null,
                        environment.CurrentLocationId, null, null, null,
                        environment.CurrentState) ||
                    !string.Equals(environment.RequiredState,
                        authored.RequiredState, StringComparison.Ordinal) ||
                    !IsExactIdentifier(environment.CurrentState) ||
                    !IsExactIdentifier(environment.CurrentLocationId) ||
                    !string.Equals(environment.RequiredLocationId,
                        coordinates.LocationId, StringComparison.Ordinal) ||
                    !IsClosedLifecycle(environment.Lifecycle))
                {
                    return false;
                }
                expectedReason = environment.Lifecycle == "retired"
                    ? "retired"
                    : !environment.Active
                        ? "inactive"
                        : !string.Equals(environment.CurrentLocationId,
                            environment.RequiredLocationId,
                            StringComparison.Ordinal)
                            ? "wrong_location"
                            : !string.Equals(environment.CurrentState,
                                authored.RequiredState, StringComparison.Ordinal)
                                ? "state_mismatch"
                                : null;
                break;
            default:
                return false;
        }
        return expectedReason is not null &&
               string.Equals(lossReason, expectedReason, StringComparison.Ordinal);
    }

    private static bool TryValidateQuantitySuccess(
        MortalWoundResolvedRequirement row,
        long current,
        long available,
        string reservationState,
        string lifecycle,
        bool active,
        long observedCumulative,
        IDictionary<string, long> local,
        IDictionary<string, QuantitySnapshot> aggregate,
        bool requirePositiveCurrent)
    {
        if (row.RequestedQuantity is not { } quantity || quantity <= 0 ||
            (requirePositiveCurrent ? current <= 0 : current < 0) ||
            available < 0 || available > current ||
            !string.Equals(reservationState, "available", StringComparison.Ordinal) ||
            !IsCurrentActive(lifecycle, active))
        {
            return false;
        }
        var key = QuantityKey(row);
        var localCumulative = (local.TryGetValue(key, out var prior) ? prior : 0L) +
                              quantity;
        if (observedCumulative != localCumulative ||
            localCumulative > current || localCumulative > available ||
            !TryReadQuantitySnapshot(
                aggregate, key, current, available, reservationState,
                lifecycle, active, out var priorAggregate))
        {
            return false;
        }
        var aggregateCumulative = priorAggregate + quantity;
        if (aggregateCumulative > available)
            return false;
        local[key] = localCumulative;
        aggregate[key] = new QuantitySnapshot(
            current, available, reservationState, lifecycle, active,
            aggregateCumulative);
        return true;
    }

    private static bool TryClassifyQuantityFailure(
        MortalWoundResolvedRequirement row,
        MortalWoundTreatmentAuthority.ActorCoordinate expectedOwner,
        long current,
        long available,
        string reservationState,
        string lifecycle,
        bool active,
        long observedCumulative,
        IDictionary<string, long> local,
        IDictionary<string, QuantitySnapshot> aggregate,
        bool requirePositiveCurrent,
        out string? reason)
    {
        reason = null;
        if (row.RequestedQuantity is not { } quantity || quantity <= 0 ||
            !ValidActorCoordinate(row.OwnerKind, row.OwnerId) ||
            (requirePositiveCurrent ? current <= 0 : current < 0) ||
            available < 0 || available > current ||
            reservationState is not ("available" or "reserved") ||
            !IsClosedLifecycle(lifecycle))
        {
            return false;
        }
        var key = QuantityKey(row);
        var localCumulative = (local.TryGetValue(key, out var prior) ? prior : 0L) +
                              quantity;
        var localReason = !SameActor(row, expectedOwner)
            ? "owner_unavailable"
            : lifecycle == "retired"
                ? "retired"
                : !active
                    ? "inactive"
                    : reservationState != "available"
                        ? "reserved"
                        : localCumulative > current || localCumulative > available
                            ? "quantity_insufficient"
                            : null;
        if (!TryReadQuantitySnapshot(
                aggregate, key, current, available, reservationState,
                lifecycle, active, out var priorAggregate))
        {
            return false;
        }
        local[key] = localCumulative;
        if (localReason is not null)
        {
            if (observedCumulative != localCumulative)
                return false;
            if (!aggregate.ContainsKey(key))
            {
                aggregate[key] = new QuantitySnapshot(
                    current, available, reservationState, lifecycle, active, 0L);
            }
            reason = localReason;
            return true;
        }

        var aggregateCumulative = priorAggregate + quantity;
        if (observedCumulative != aggregateCumulative ||
            aggregateCumulative <= available)
        {
            return false;
        }
        aggregate[key] = new QuantitySnapshot(
            current, available, reservationState, lifecycle, active,
            aggregateCumulative);
        reason = "quantity_insufficient";
        return true;
    }

    private static bool TryReadQuantitySnapshot(
        IDictionary<string, QuantitySnapshot> aggregate,
        string key,
        long current,
        long available,
        string reservationState,
        string lifecycle,
        bool active,
        out long requested)
    {
        requested = 0;
        if (!aggregate.TryGetValue(key, out var snapshot))
            return true;
        requested = snapshot.AggregateRequested;
        return snapshot.Current == current && snapshot.Available == available &&
               string.Equals(snapshot.ReservationState, reservationState,
                   StringComparison.Ordinal) &&
               string.Equals(snapshot.Lifecycle, lifecycle,
                   StringComparison.Ordinal) &&
               snapshot.Active == active;
    }

    private static bool ConsentEvidenceMatches(
        MortalWoundConsentRequirementEvidence evidence,
        MortalWoundConsentRequirement requirement,
        MortalWoundTreatmentAttemptCoordinates coordinates) =>
        string.Equals(evidence.RequiredProviderRef, requirement.ProviderRef,
            StringComparison.Ordinal) &&
        string.Equals(evidence.RequiredTargetRef, requirement.TargetRef,
            StringComparison.Ordinal) &&
        IsExactIdentifier(evidence.ProviderCurrentLocationId) &&
        string.Equals(evidence.RequiredLocationId, coordinates.LocationId,
            StringComparison.Ordinal);

    private static bool LocationEvidenceMatches(
        MortalWoundLocationRequirementEvidence evidence,
        MortalWoundLocationRequirement requirement,
        MortalWoundTreatmentAttemptCoordinates coordinates,
        bool requirePresent)
    {
        if (evidence.PresentActors is null ||
            !string.Equals(evidence.TargetRole, requirement.TargetRole,
                StringComparison.Ordinal) ||
            !string.Equals(evidence.AuthorityLocationId, requirement.LocationRef,
                StringComparison.Ordinal) ||
            !string.Equals(evidence.RequiredLocationId, coordinates.LocationId,
                StringComparison.Ordinal))
        {
            return false;
        }
        var actorKeys = new HashSet<string>(StringComparer.Ordinal);
        var targetCount = 0;
        foreach (var actor in evidence.PresentActors)
        {
            if (actor is null || !ValidActorCoordinate(actor.ActorKind, actor.ActorId) ||
                !actorKeys.Add(actor.ActorKind + "\u001f" + actor.ActorId))
            {
                return false;
            }
            if (string.Equals(actor.ActorKind, coordinates.TargetKind,
                    StringComparison.Ordinal) &&
                string.Equals(actor.ActorId, coordinates.TargetId,
                    StringComparison.Ordinal))
            {
                targetCount++;
            }
        }
        if (evidence.TargetPresent != (targetCount == 1))
            return false;
        return !requirePresent || evidence.TargetPresent;
    }

    private static bool ExactAcceptedActorRow(
        MortalWoundResolvedRequirement row,
        string kind,
        string authorityRef,
        MortalWoundTreatmentAttemptCoordinates coordinates,
        MortalWoundTreatmentAuthority.ActorCoordinate owner,
        int? requestedQuantity,
        int? minimumTier,
        int? currentTier) => ExactRow(
        row, kind, authorityRef, coordinates.Realm, owner.ActorKind, owner.ActorId,
        coordinates.ProviderKind, coordinates.ProviderId, coordinates.TargetKind,
        coordinates.TargetId, coordinates.LocationId, requestedQuantity,
        minimumTier, currentTier, null);

    private static bool ExactFailureQuantityRow(
        MortalWoundResolvedRequirement row,
        string kind,
        string authorityRef,
        MortalWoundTreatmentAttemptCoordinates coordinates,
        int quantity) =>
        ValidActorCoordinate(row.OwnerKind, row.OwnerId) &&
        ExactRow(
            row, kind, authorityRef, coordinates.Realm, row.OwnerKind, row.OwnerId,
            null, null, null, null, null, quantity, null, null, null);

    private static bool ExactRow(
        MortalWoundResolvedRequirement row,
        string kind,
        string authorityRef,
        string realm,
        string? ownerKind,
        string? ownerId,
        string? providerKind,
        string? providerId,
        string? targetKind,
        string? targetId,
        string? locationId,
        int? requestedQuantity,
        int? minimumTier,
        int? currentTier,
        string? currentState) =>
        string.Equals(row.Kind, kind, StringComparison.Ordinal) &&
        string.Equals(row.AuthorityRef, authorityRef, StringComparison.Ordinal) &&
        string.Equals(row.Realm, realm, StringComparison.Ordinal) &&
        string.Equals(row.OwnerKind, ownerKind, StringComparison.Ordinal) &&
        string.Equals(row.OwnerId, ownerId, StringComparison.Ordinal) &&
        string.Equals(row.ProviderKind, providerKind, StringComparison.Ordinal) &&
        string.Equals(row.ProviderId, providerId, StringComparison.Ordinal) &&
        string.Equals(row.TargetKind, targetKind, StringComparison.Ordinal) &&
        string.Equals(row.TargetId, targetId, StringComparison.Ordinal) &&
        string.Equals(row.LocationId, locationId, StringComparison.Ordinal) &&
        row.RequestedQuantity == requestedQuantity &&
        row.MinimumTier == minimumTier && row.CurrentTier == currentTier &&
        string.Equals(row.CurrentState, currentState, StringComparison.Ordinal);

    private static bool RequirementMatches(
        string kind,
        string authorityRef,
        MortalWoundTreatmentRequirement requirement) => requirement switch
    {
        MortalWoundItemQuantityRequirement value =>
            kind == value.Kind && authorityRef == value.ItemRef,
        MortalWoundResourceQuantityRequirement value =>
            kind == value.Kind && authorityRef == value.ResourceRef,
        MortalWoundSkillTierRequirement value =>
            kind == value.Kind && authorityRef == value.CapabilityRef,
        MortalWoundSourceCapabilityRequirement value =>
            kind == value.Kind && authorityRef == value.CapabilityRef,
        MortalWoundProviderRequirement value =>
            kind == value.Kind && authorityRef == value.ProviderRef,
        MortalWoundConsentRequirement value =>
            kind == value.Kind && authorityRef == value.ConsentRef,
        MortalWoundFacilityRequirement value =>
            kind == value.Kind && authorityRef == value.FacilityRef,
        MortalWoundLocationRequirement value =>
            kind == value.Kind && authorityRef == value.LocationRef,
        MortalWoundQuestStateRequirement value =>
            kind == value.Kind && authorityRef == value.QuestRef,
        MortalWoundEffectStateRequirement value =>
            kind == value.Kind && authorityRef == value.EffectRef,
        MortalWoundEnvironmentRequirement value =>
            kind == value.Kind && authorityRef == value.EnvironmentRef,
        _ => false
    };

    private static JsonElement SerializeRequirement(
        MortalWoundTreatmentRequirement requirement)
    {
        Dictionary<string, object?> fields = requirement switch
        {
            MortalWoundItemQuantityRequirement value => new()
            {
                ["kind"] = value.Kind,
                ["itemRef"] = value.ItemRef,
                ["quantity"] = value.Quantity,
                ["ownerRole"] = value.OwnerRole
            },
            MortalWoundResourceQuantityRequirement value => new()
            {
                ["kind"] = value.Kind,
                ["resourceRef"] = value.ResourceRef,
                ["quantity"] = value.Quantity,
                ["ownerRole"] = value.OwnerRole
            },
            MortalWoundSkillTierRequirement value => new()
            {
                ["kind"] = value.Kind,
                ["capabilityRef"] = value.CapabilityRef,
                ["minimumTier"] = value.MinimumTier,
                ["actorRole"] = value.ActorRole
            },
            MortalWoundSourceCapabilityRequirement value => new()
            {
                ["kind"] = value.Kind,
                ["capabilityRef"] = value.CapabilityRef,
                ["actorRole"] = value.ActorRole
            },
            MortalWoundProviderRequirement value => new()
            {
                ["kind"] = value.Kind,
                ["providerRef"] = value.ProviderRef
            },
            MortalWoundConsentRequirement value => new()
            {
                ["kind"] = value.Kind,
                ["consentRef"] = value.ConsentRef,
                ["providerRef"] = value.ProviderRef,
                ["targetRef"] = value.TargetRef
            },
            MortalWoundFacilityRequirement value => new()
            {
                ["kind"] = value.Kind,
                ["facilityRef"] = value.FacilityRef
            },
            MortalWoundLocationRequirement value => new()
            {
                ["kind"] = value.Kind,
                ["locationRef"] = value.LocationRef,
                ["targetRole"] = value.TargetRole
            },
            MortalWoundQuestStateRequirement value => new()
            {
                ["kind"] = value.Kind,
                ["questRef"] = value.QuestRef,
                ["requiredState"] = value.RequiredState
            },
            MortalWoundEffectStateRequirement value => new()
            {
                ["kind"] = value.Kind,
                ["effectRef"] = value.EffectRef,
                ["requiredState"] = value.RequiredState,
                ["targetRole"] = value.TargetRole
            },
            MortalWoundEnvironmentRequirement value => new()
            {
                ["kind"] = value.Kind,
                ["environmentRef"] = value.EnvironmentRef,
                ["requiredState"] = value.RequiredState
            },
            _ => throw new InvalidOperationException(
                $"Unsupported requirement kind '{requirement.Kind}'.")
        };
        return JsonSerializer.SerializeToElement(fields);
    }

    private static bool TrySelectRole(
        MortalWoundTreatmentAttemptCoordinates coordinates,
        string role,
        out MortalWoundTreatmentAuthority.ActorCoordinate actor)
    {
        actor = role switch
        {
            "provider" => new MortalWoundTreatmentAuthority.ActorCoordinate(
                coordinates.ProviderKind,
                coordinates.ProviderId),
            "target" => new MortalWoundTreatmentAuthority.ActorCoordinate(
                coordinates.TargetKind,
                coordinates.TargetId),
            _ => null!
        };
        return actor is not null;
    }

    private static bool SameActor(
        MortalWoundResolvedRequirement row,
        MortalWoundTreatmentAuthority.ActorCoordinate actor) =>
        string.Equals(row.OwnerKind, actor.ActorKind, StringComparison.Ordinal) &&
        string.Equals(row.OwnerId, actor.ActorId, StringComparison.Ordinal);

    private static bool ValidActorCoordinate(string? kind, string? id) =>
        kind is "player" or "npc" or "combatant" or "combatant_member" &&
        IsExactIdentifier(id);

    private static bool IsExactIdentifier(string? value) =>
        value is not null &&
        ResourceMaterializationContract.IsExactIdentifier(value);

    private static bool IsClosedLifecycle(string value) =>
        value is "active" or "retired";

    private static bool IsCurrentActive(string lifecycle, bool active) =>
        string.Equals(lifecycle, "active", StringComparison.Ordinal) && active;

    private static bool IsRetired(params string[] lifecycles) =>
        lifecycles.Any(static lifecycle =>
            string.Equals(lifecycle, "retired", StringComparison.Ordinal));

    private static string? ClassifyStateFailure(
        string lifecycle,
        bool active,
        string currentState,
        string requiredState) => lifecycle == "retired"
        ? "retired"
        : !active
            ? "inactive"
            : !string.Equals(currentState, requiredState, StringComparison.Ordinal)
                ? "state_mismatch"
                : null;

    private static bool IsClosedObservedLossReason(string value) => value is
        "quantity_insufficient" or "reserved" or "tier_insufficient" or
        "retired" or "inactive" or "state_mismatch" or "owner_unavailable" or
        "provider_unreachable" or "consent_absent" or "facility_unavailable" or
        "wrong_location" or "actor_not_present";

    private static string QuantityKey(MortalWoundResolvedRequirement row) =>
        string.Join(
            "\u001f",
            row.Kind,
            row.AuthorityRef,
            row.OwnerKind,
            row.OwnerId);

    private static MortalWoundResolvedRequirement FailureRow(
        MortalWoundTreatmentRequirementFailureWitness failure,
        MortalWoundTreatmentRequirementFailureObservation observation) => new(
        failure.RequirementIndex,
        failure.Kind,
        failure.AuthorityRef,
        observation.Evidence is MortalWoundSkillTierRequirementEvidence skillId
            ? skillId.SkillId
            : null,
        observation.Realm,
        observation.OwnerKind,
        observation.OwnerId,
        observation.ProviderKind,
        observation.ProviderId,
        observation.TargetKind,
        observation.TargetId,
        observation.LocationId,
        observation.Evidence switch
        {
            MortalWoundItemQuantityRequirementEvidence item => item.RequestedQuantity,
            MortalWoundResourceQuantityRequirementEvidence resource =>
                resource.RequestedQuantity,
            _ => null
        },
        observation.Evidence is MortalWoundSkillTierRequirementEvidence skill
            ? skill.MinimumTier
            : null,
        observation.Evidence is MortalWoundSkillTierRequirementEvidence currentSkill
            ? currentSkill.CurrentTier
            : null,
        observation.Evidence switch
        {
            MortalWoundQuestStateRequirementEvidence quest => quest.CurrentState,
            MortalWoundEffectStateRequirementEvidence effect => effect.CurrentState,
            MortalWoundEnvironmentRequirementEvidence environment =>
                environment.CurrentState,
            _ => null
        },
        string.Empty);

    private static bool HasValidResourceAuthority(
        MortalWoundTreatmentResourceReservationAuthority value)
    {
        if (value.Policy is null ||
            value.Claims.Any(static claim =>
                claim is null || !HasValidResourceClaim(claim)) ||
            value.Claims.Select(static claim => claim.ClaimFingerprint)
                .Distinct(StringComparer.Ordinal).Count() != value.Claims.Count)
        {
            return false;
        }
        var semanticFingerprint = ComputeResourceAuthorityFingerprint(value, null);
        var expectedReservationId = value.ReservationDisposition switch
        {
            "not_required" when value.Claims.Count == 0 => null,
            "held" when value.Claims.Count > 0 =>
                "wound_treatment_resource_reservation_" +
                semanticFingerprint["sha256:".Length..],
            _ => "invalid"
        };
        return string.Equals(value.ReservationId, expectedReservationId,
                   StringComparison.Ordinal) &&
               string.Equals(
                   value.AuthorityFingerprint,
                   ComputeResourceAuthorityFingerprint(value, expectedReservationId),
                   StringComparison.Ordinal);
    }

    private static bool HasValidResourceClaim(MortalWoundTreatmentResourceClaim value)
    {
        if (value.RequirementIndex < 0 || value.Quantity <= 0)
            return false;
        var fingerprint = WoundAcceptedTurnFingerprintWriter.Compute(new string?[]
        {
            "book_of_eternity.mortal_wound_treatment.resource_claim", "1",
            value.Scope, Number(value.RequirementIndex), value.Kind,
            value.AuthorityRef, value.Realm, value.OwnerKind, value.OwnerId,
            Number(value.Quantity), value.SuccessWitnessFingerprint
        });
        return string.Equals(value.ClaimFingerprint, fingerprint,
            StringComparison.Ordinal);
    }

    private sealed record ExpectedResourceClaim(
        string Scope,
        int RequirementIndex,
        string Kind,
        string AuthorityRef,
        string Realm,
        string OwnerKind,
        string OwnerId,
        int Quantity,
        string SuccessWitnessFingerprint,
        string ClaimFingerprint,
        long AvailableQuantity);

    private static bool HasValidResourceAgreement(
        MortalWoundTreatmentAttemptRequest request)
    {
        var requirement = request.RequirementAuthority;
        var resource = request.ResourceAuthority;
        if (!TryValidateScopeTopology(
                request,
                out var interruption) ||
            !TryProjectExpectedClaims(
                requirement,
                interruption,
                out var expected) ||
            !HasValidResourcePolicy(request, expected))
        {
            return false;
        }

        if (resource.Claims.Count != expected.Count)
            return false;
        var coordinates = new HashSet<string>(StringComparer.Ordinal);
        var claimFingerprints = new HashSet<string>(StringComparer.Ordinal);
        for (var index = 0; index < expected.Count; index++)
        {
            var actual = resource.Claims[index];
            var projected = expected[index];
            var coordinate = string.Join(
                "\u001f",
                actual.Scope,
                request.Mode == "course" &&
                actual.Scope == "course_milestone"
                    ? request.MilestoneOrdinal?.ToString(CultureInfo.InvariantCulture)
                    : "null",
                Number(actual.RequirementIndex));
            if (!coordinates.Add(coordinate) ||
                !claimFingerprints.Add(actual.ClaimFingerprint) ||
                !string.Equals(actual.Scope, projected.Scope, StringComparison.Ordinal) ||
                actual.RequirementIndex != projected.RequirementIndex ||
                !string.Equals(actual.Kind, projected.Kind, StringComparison.Ordinal) ||
                !string.Equals(actual.AuthorityRef, projected.AuthorityRef,
                    StringComparison.Ordinal) ||
                !string.Equals(actual.Realm, projected.Realm, StringComparison.Ordinal) ||
                !string.Equals(actual.OwnerKind, projected.OwnerKind,
                    StringComparison.Ordinal) ||
                !string.Equals(actual.OwnerId, projected.OwnerId,
                    StringComparison.Ordinal) ||
                actual.Quantity != projected.Quantity ||
                !string.Equals(
                    actual.SuccessWitnessFingerprint,
                    projected.SuccessWitnessFingerprint,
                    StringComparison.Ordinal) ||
                !string.Equals(
                    actual.ClaimFingerprint,
                    projected.ClaimFingerprint,
                    StringComparison.Ordinal))
            {
                return false;
            }
        }

        return expected.Count == 0
            ? resource.ReservationDisposition == "not_required" &&
              resource.ReservationId is null
            : resource.ReservationDisposition == "held" &&
              ResourceMaterializationContract.IsExactIdentifier(resource.ReservationId);
    }

    private static bool TryValidateScopeTopology(
        MortalWoundTreatmentAttemptRequest request,
        out bool interruption)
    {
        interruption = false;
        var bundle = request.RequirementAuthority;
        if (request.Mode is "procedure" or "guaranteed")
        {
            return bundle.Scopes.Count == 1 &&
                   HasExactScopeTopology(bundle.Scopes[0], "common", null) &&
                   bundle.Scopes[0].Status == "Satisfied" &&
                   bundle.CourseId is null &&
                   bundle.CourseMilestoneOrdinal is null &&
                   bundle.CourseCoordinateFingerprint is null &&
                   bundle.CourseRequirementStatus is null &&
                   bundle.InterruptionReason is null;
        }

        if (request.Mode != "course" ||
            request.ModeAuthority is not MortalWoundCourseModeAuthority course ||
            request.MilestoneOrdinal is not { } ordinal ||
            ordinal <= 0 ||
            bundle.Scopes.Count != 2 ||
            !HasExactScopeTopology(bundle.Scopes[0], "common", null) ||
            !HasExactScopeTopology(
                bundle.Scopes[1],
                "course_milestone",
                ordinal) ||
            bundle.CourseMilestoneOrdinal != ordinal ||
            !string.Equals(bundle.CourseId, course.CourseId, StringComparison.Ordinal) ||
            !string.Equals(
                bundle.CourseCoordinateFingerprint,
                course.CourseCoordinateFingerprint,
                StringComparison.Ordinal))
        {
            return false;
        }

        var status = bundle.Scopes.All(static scope => scope.Status == "Satisfied")
            ? "Satisfied"
            : "Unsatisfied";
        if (!string.Equals(
                bundle.CourseRequirementStatus,
                status,
                StringComparison.Ordinal))
        {
            return false;
        }
        if (ordinal == 1 && status == "Unsatisfied")
            return false;
        var expectedReason = course.WindowDisposition switch
        {
            "deadline_exceeded" => "deadline_exceeded",
            "ready" when status == "Unsatisfied" && ordinal > 1 =>
                "requirements_unsatisfied",
            _ => null
        };
        if (!string.Equals(
                bundle.InterruptionReason,
                expectedReason,
                StringComparison.Ordinal))
        {
            return false;
        }
        interruption = expectedReason is not null;
        return true;
    }

    private static bool HasExactScopeTopology(
        MortalWoundTreatmentRequirementScopeAuthority scope,
        string expectedScope,
        int? expectedOrdinal)
    {
        if (scope is null ||
            !string.Equals(scope.Scope, expectedScope, StringComparison.Ordinal) ||
            scope.CourseMilestoneOrdinal != expectedOrdinal ||
            scope.Bindings.Count + scope.FailureWitnesses.Count >
            WoundMaterializationContract.MaxRequirementsPerTreatmentMember ||
            !IsStrictlyIncreasing(scope.Bindings.Select(static value =>
                value.RequirementIndex)) ||
            !IsStrictlyIncreasing(scope.FailureWitnesses.Select(static value =>
                value.RequirementIndex)))
        {
            return false;
        }
        var indices = scope.Bindings.Select(static value => value.RequirementIndex)
            .Concat(scope.FailureWitnesses.Select(static value =>
                value.RequirementIndex))
            .OrderBy(static value => value)
            .ToArray();
        return indices.SequenceEqual(Enumerable.Range(0, indices.Length));
    }

    private static bool IsStrictlyIncreasing(IEnumerable<int> values)
    {
        var prior = -1;
        foreach (var value in values)
        {
            if (value <= prior)
                return false;
            prior = value;
        }
        return true;
    }

    private static bool TryProjectExpectedClaims(
        MortalWoundTreatmentRequirementAuthorityBundle requirement,
        bool interruption,
        out IReadOnlyList<ExpectedResourceClaim> expected)
    {
        var result = new List<ExpectedResourceClaim>();
        var aggregate = new Dictionary<string, (long Available, long Requested)>(
            StringComparer.Ordinal);
        var confusable = new Dictionary<string, string>(StringComparer.Ordinal);
        foreach (var scope in requirement.Scopes)
        {
            var local = new Dictionary<string, long>(StringComparer.Ordinal);
            foreach (var binding in scope.Bindings)
            {
                var row = binding.ResolvedRequirement;
                if (row.Kind is not ("item_quantity" or "resource_quantity"))
                    continue;
                if (row.RequestedQuantity is not { } quantity ||
                    quantity <= 0 ||
                    row.OwnerKind is null ||
                    row.OwnerId is null ||
                    !ResourceMaterializationContract.IsExactIdentifier(row.AuthorityRef) ||
                    !TryValidateQuantityEvidence(
                        row,
                        binding.SuccessWitness.Evidence,
                        local,
                        out var available))
                {
                    expected = Array.Empty<ExpectedResourceClaim>();
                    return false;
                }

                var identity = string.Join(
                    "\u001f",
                    row.Kind,
                    row.AuthorityRef,
                    row.OwnerKind,
                    row.OwnerId);
                if (aggregate.TryGetValue(identity, out var aggregateRow))
                {
                    if (aggregateRow.Available != available ||
                        checked(aggregateRow.Requested + quantity) > available)
                    {
                        expected = Array.Empty<ExpectedResourceClaim>();
                        return false;
                    }
                    aggregate[identity] = (
                        available,
                        aggregateRow.Requested + quantity);
                }
                else
                {
                    aggregate.Add(identity, (available, quantity));
                }

                var confusableKey = string.Join(
                    "\u001f",
                    row.Kind,
                    row.Realm,
                    row.OwnerKind,
                    row.OwnerId,
                    ExactIdentifierConfusableKey.Build(row.AuthorityRef));
                if (confusable.TryGetValue(confusableKey, out var existingRef) &&
                    !string.Equals(existingRef, row.AuthorityRef,
                        StringComparison.Ordinal))
                {
                    expected = Array.Empty<ExpectedResourceClaim>();
                    return false;
                }
                confusable[confusableKey] = row.AuthorityRef;

                var claimFingerprint = WoundAcceptedTurnFingerprintWriter.Compute(
                    new string?[]
                    {
                        "book_of_eternity.mortal_wound_treatment.resource_claim", "1",
                        binding.SuccessWitness.Scope, Number(row.RequirementIndex),
                        row.Kind, row.AuthorityRef, row.Realm, row.OwnerKind,
                        row.OwnerId, Number(quantity),
                        binding.SuccessWitness.WitnessFingerprint
                    });
                result.Add(new ExpectedResourceClaim(
                    binding.SuccessWitness.Scope,
                    row.RequirementIndex,
                    row.Kind,
                    row.AuthorityRef,
                    row.Realm,
                    row.OwnerKind,
                    row.OwnerId,
                    quantity,
                    binding.SuccessWitness.WitnessFingerprint,
                    claimFingerprint,
                    available));
            }
        }
        expected = interruption
            ? Array.Empty<ExpectedResourceClaim>()
            : result;
        return true;
    }

    private static bool TryValidateQuantityEvidence(
        MortalWoundResolvedRequirement row,
        MortalWoundTreatmentRequirementEvidence evidence,
        IDictionary<string, long> local,
        out long available)
    {
        available = 0;
        var key = string.Join(
            "\u001f",
            row.Kind,
            row.AuthorityRef,
            row.OwnerKind,
            row.OwnerId);
        var cumulative = checked(
            (local.TryGetValue(key, out var prior) ? prior : 0L) +
            row.RequestedQuantity!.Value);
        local[key] = cumulative;
        switch (evidence)
        {
            case MortalWoundItemQuantityRequirementEvidence item
                when row.Kind == "item_quantity" &&
                     item.RequestedQuantity == row.RequestedQuantity &&
                     item.Count >= 0 &&
                     item.AvailableCount >= row.RequestedQuantity &&
                     item.AvailableCount <= item.Count &&
                     item.CumulativeRequestedQuantity == cumulative &&
                     item.CumulativeRequestedQuantity <= item.AvailableCount &&
                     item.ReservationState == "available" &&
                     item.Lifecycle == "active" && item.Active:
                available = item.AvailableCount;
                return true;
            case MortalWoundResourceQuantityRequirementEvidence resource
                when row.Kind == "resource_quantity" &&
                     resource.RequestedQuantity == row.RequestedQuantity &&
                     resource.CurrentValue >= 0 &&
                     resource.AvailableValue >= row.RequestedQuantity &&
                     resource.AvailableValue <= resource.CurrentValue &&
                     resource.CumulativeRequestedQuantity == cumulative &&
                     resource.CumulativeRequestedQuantity <= resource.AvailableValue &&
                     resource.ReservationState == "available" &&
                     resource.Lifecycle == "active" && resource.Active:
                available = resource.AvailableValue;
                return true;
            default:
                return false;
        }
    }

    private static bool HasValidResourcePolicy(
        MortalWoundTreatmentAttemptRequest request,
        IReadOnlyList<ExpectedResourceClaim> expected)
    {
        var policy = request.ResourceAuthority.Policy;
        if (!TryGetRoute(request, out var selectedRoute) || selectedRoute is null)
        {
            return false;
        }
        var courseRoute = selectedRoute as MortalWoundCourseRouteDefinition;
        if ((request.Mode == "course") != (courseRoute is not null) ||
            !string.Equals(
                MortalWoundTreatmentResourceComposer.ComputePolicyFingerprint(policy),
                MortalWoundTreatmentResourceComposer.ComputePolicyFingerprint(
                    selectedRoute.ResourcePolicy),
                StringComparison.Ordinal))
        {
            return false;
        }
        var allowedConsumeOn = request.Mode == "procedure"
            ? new[] { "success", "partial_success", "failed_attempt" }
            : new[] { "success" };
        if (!policy.ReserveBeforeResolution ||
            policy.ConsumeOn.Length > allowedConsumeOn.Length ||
            policy.ConsumeOn.Distinct(StringComparer.Ordinal).Count() !=
            policy.ConsumeOn.Length ||
            policy.ConsumeOn.Any(value => !allowedConsumeOn.Contains(
                value,
                StringComparer.Ordinal)) ||
            !policy.RefundOn.SequenceEqual(new[]
            {
                "cancelled", "validation_failed", "rolled_back"
            }) ||
            policy.Mutations.Length > 64 ||
            policy.Mutations.Any(static mutation => mutation is null))
        {
            return false;
        }

        var selectorKeys = new HashSet<string>(StringComparer.Ordinal);
        foreach (var mutation in policy.Mutations)
        {
            if (mutation.Kind != "consume_requirement" ||
                mutation.RequirementIndex < 0)
            {
                return false;
            }
            var key = string.Join(
                "\u001f",
                mutation.Scope,
                NullableNumber(mutation.MilestoneOrdinal),
                Number(mutation.RequirementIndex));
            if (!selectorKeys.Add(key))
                return false;

            if (mutation.Scope == "common" && mutation.MilestoneOrdinal is null)
            {
                if (!CurrentRequirementIsQuantity(
                        request.RequirementAuthority.Scopes[0],
                        mutation.RequirementIndex))
                {
                    return false;
                }
                continue;
            }
            if (mutation.Scope != "course_milestone" ||
                request.Mode != "course" ||
                mutation.MilestoneOrdinal is not > 0 ||
                courseRoute is null)
            {
                return false;
            }
            var milestones = courseRoute.Milestones.Where(candidate =>
                candidate.Ordinal == mutation.MilestoneOrdinal.Value).ToArray();
            if (milestones.Length != 1 ||
                mutation.RequirementIndex >= milestones[0].Requirements.Length ||
                milestones[0].Requirements[mutation.RequirementIndex].Kind is not
                    ("item_quantity" or "resource_quantity"))
            {
                return false;
            }
        }

        return policy.Mutations.Where(mutation =>
                mutation.Scope == "common" ||
                mutation.MilestoneOrdinal == request.MilestoneOrdinal)
            .All(mutation =>
                expected.Any(claim =>
                    claim.Scope == mutation.Scope &&
                    claim.RequirementIndex == mutation.RequirementIndex) ||
                request.RequirementAuthority.InterruptionReason is not null ||
                CurrentRequirementIsFailure(
                    mutation.Scope == "common"
                        ? request.RequirementAuthority.Scopes[0]
                        : request.RequirementAuthority.Scopes[1],
                    mutation.RequirementIndex));
    }

    internal static bool TryGetCourseRoute(
        MortalWoundCourseModeAuthority authority,
        out MortalWoundCourseRouteDefinition? route)
    {
        route = null;
        var wound = authority.CourseStartAuthority.StartingWound;
        try
        {
            var parsed = MortalWoundTreatmentContract.ParseProjection(
                wound.Treatment,
                "treatmentAttempt.courseStart.startingWound.treatment",
                wound.Owner.Realm,
                wound.Owner.OwnerKind,
                wound.Severity.Rank,
                wound.Complications,
                wound.Recovery.DeteriorationPolicy);
            if (!parsed.IsValid || parsed.Treatment is null)
                return false;
            var matches = parsed.Treatment.Routes
                .OfType<MortalWoundCourseRouteDefinition>()
                .Where(candidate => string.Equals(
                    candidate.RouteId,
                    authority.CourseStartAuthority.RouteId,
                    StringComparison.Ordinal))
                .ToArray();
            if (matches.Length != 1 ||
                !string.Equals(
                    authority.CourseStartAuthority.RouteFingerprint,
                    MortalWoundTreatmentRouteFingerprint.Compute(
                        wound,
                        authority.CourseStartAuthority.RouteId),
                    StringComparison.Ordinal))
            {
                return false;
            }
            route = matches[0];
            return true;
        }
        catch (Exception exception) when (exception is ArgumentException or
                                           InvalidOperationException or
                                           System.Text.Json.JsonException or
                                           OverflowException)
        {
            return false;
        }
    }

    private static bool CurrentRequirementIsQuantity(
        MortalWoundTreatmentRequirementScopeAuthority scope,
        int index) =>
        scope.Bindings.Count(binding => binding.RequirementIndex == index &&
            binding.ResolvedRequirement.Kind is "item_quantity" or "resource_quantity") +
        scope.FailureWitnesses.Count(failure => failure.RequirementIndex == index &&
            failure.Kind is "item_quantity" or "resource_quantity") == 1;

    private static bool CurrentRequirementIsFailure(
        MortalWoundTreatmentRequirementScopeAuthority scope,
        int index) => scope.FailureWitnesses.Count(failure =>
        failure.RequirementIndex == index) == 1;

    private static string ComputeResourceAuthorityFingerprint(
        MortalWoundTreatmentResourceReservationAuthority value,
        string? reservationId)
    {
        var fields = new List<string?>
        {
            "book_of_eternity.mortal_wound_treatment.resource_reservation_authority", "1",
            value.ReservationDisposition, reservationId, value.CoordinatesFingerprint,
            value.AcceptedStateFingerprint, value.RouteFingerprint, value.CourseId,
            NullableNumber(value.CourseMilestoneOrdinal),
            value.CourseCoordinateFingerprint, value.RequirementAuthorityFingerprint,
            MortalWoundTreatmentResourceComposer.ComputePolicyFingerprint(value.Policy),
            Number(value.Claims.Count)
        };
        fields.AddRange(value.Claims.Select(static claim => claim.ClaimFingerprint));
        return WoundAcceptedTurnFingerprintWriter.Compute(fields);
    }

    private static bool TryMechanicalFields(
        MortalWoundResolvedRequirement row,
        MortalWoundTreatmentRequirementEvidence evidence,
        out IReadOnlyList<string?> fields)
    {
        fields = Array.Empty<string?>();
        if (row is null || evidence is null ||
            !string.Equals(row.Kind, evidence.Kind, StringComparison.Ordinal))
            return false;
        switch (evidence)
        {
            case MortalWoundItemQuantityRequirementEvidence item
                when row.RequestedQuantity == item.RequestedQuantity &&
                     row.OwnerKind is not null && row.OwnerId is not null:
                fields = new string?[]
                {
                    row.AuthorityRef, row.Realm, row.OwnerKind, row.OwnerId,
                    Number(item.Count), Number(item.AvailableCount),
                    item.ReservationState, item.Lifecycle, Boolean(item.Active),
                    Number(item.CumulativeRequestedQuantity)
                };
                return true;
            case MortalWoundResourceQuantityRequirementEvidence resource
                when row.RequestedQuantity == resource.RequestedQuantity &&
                     row.OwnerKind is not null && row.OwnerId is not null:
                fields = new string?[]
                {
                    row.AuthorityRef, row.Realm, row.OwnerKind, row.OwnerId,
                    Number(resource.CurrentValue), Number(resource.AvailableValue),
                    resource.ReservationState, resource.Lifecycle,
                    Boolean(resource.Active), Number(resource.CumulativeRequestedQuantity)
                };
                return true;
            case MortalWoundSkillTierRequirementEvidence skill
                when row.MinimumTier == skill.MinimumTier &&
                     row.CurrentTier == skill.CurrentTier &&
                     row.SkillId == skill.SkillId &&
                     row.OwnerKind is not null && row.OwnerId is not null:
                fields = ActorFields(
                        row.OwnerKind,
                        row.OwnerId,
                        row.Realm,
                        skill.ActorCurrentLocationId,
                        skill.ActorLifecycle,
                        skill.ActorActive,
                        skill.ActorReachable)
                    .Concat(new string?[]
                    {
                        row.SkillId, row.AuthorityRef, Number(skill.CurrentTier),
                        skill.SkillLifecycle, Boolean(skill.SkillActive)
                    }).ToArray();
                return true;
            case MortalWoundSourceCapabilityRequirementEvidence capability
                when row.OwnerKind is not null && row.OwnerId is not null:
                fields = ActorFields(
                        row.OwnerKind,
                        row.OwnerId,
                        row.Realm,
                        capability.ActorCurrentLocationId,
                        capability.ActorLifecycle,
                        capability.ActorActive,
                        capability.ActorReachable)
                    .Concat(new string?[]
                    {
                        row.AuthorityRef, capability.CapabilityLifecycle,
                        Boolean(capability.CapabilityActive)
                    }).ToArray();
                return true;
            case MortalWoundProviderRequirementEvidence provider
                when row.ProviderKind is not null && row.ProviderId is not null:
                fields = ActorFields(
                        row.ProviderKind,
                        row.ProviderId,
                        row.Realm,
                        provider.CurrentLocationId,
                        provider.Lifecycle,
                        provider.Active,
                        provider.Reachable)
                    .Concat(new[] { Boolean(provider.Present) }).ToArray();
                return true;
            case MortalWoundConsentRequirementEvidence consent
                when row.ProviderKind is not null && row.ProviderId is not null &&
                     row.TargetKind is not null && row.TargetId is not null:
                fields = ActorFields(
                        row.ProviderKind,
                        row.ProviderId,
                        row.Realm,
                        consent.ProviderCurrentLocationId,
                        consent.ProviderLifecycle,
                        consent.ProviderActive,
                        consent.ProviderReachable)
                    .Concat(new string?[]
                    {
                        row.AuthorityRef, row.ProviderKind, row.ProviderId,
                        row.TargetKind, row.TargetId, consent.Status,
                        consent.Lifecycle, Boolean(consent.Active)
                    }).ToArray();
                return true;
            case MortalWoundFacilityRequirementEvidence facility:
                fields = new string?[]
                {
                    row.AuthorityRef, row.Realm, facility.FacilityLocationId,
                    facility.Lifecycle, Boolean(facility.Active),
                    Boolean(facility.Available)
                };
                return true;
            case MortalWoundLocationRequirementEvidence location
                when string.Equals(row.AuthorityRef, location.AuthorityLocationId,
                    StringComparison.Ordinal):
            {
                var result = new List<string?>
                {
                    row.AuthorityRef, row.Realm, location.Lifecycle,
                    Boolean(location.Active), Number(location.PresentActors.Count)
                };
                foreach (var actor in location.PresentActors)
                {
                    result.Add(actor.ActorKind);
                    result.Add(actor.ActorId);
                }
                fields = result;
                return true;
            }
            case MortalWoundQuestStateRequirementEvidence quest:
                fields = new string?[]
                {
                    row.AuthorityRef, row.Realm, quest.CurrentState,
                    quest.Lifecycle, Boolean(quest.Active)
                };
                return true;
            case MortalWoundEffectStateRequirementEvidence effect
                when row.TargetKind is not null && row.TargetId is not null:
                fields = new string?[]
                {
                    row.AuthorityRef, row.Realm, row.TargetKind, row.TargetId,
                    effect.CurrentState, effect.Lifecycle, Boolean(effect.Active)
                };
                return true;
            case MortalWoundEnvironmentRequirementEvidence environment:
                fields = new string?[]
                {
                    row.AuthorityRef, row.Realm, environment.CurrentLocationId,
                    environment.CurrentState, environment.Lifecycle,
                    Boolean(environment.Active)
                };
                return true;
            default:
                return false;
        }
    }

    private static IReadOnlyList<string?> EvidenceFields(
        MortalWoundTreatmentRequirementEvidence evidence)
    {
        var fields = new List<string?> { evidence.Kind };
        switch (evidence)
        {
            case MortalWoundItemQuantityRequirementEvidence item:
                fields.AddRange(new string?[]
                {
                    item.OwnerRole, Number(item.RequestedQuantity), Number(item.Count),
                    Number(item.AvailableCount), item.ReservationState, item.Lifecycle,
                    Boolean(item.Active), Number(item.CumulativeRequestedQuantity)
                });
                break;
            case MortalWoundResourceQuantityRequirementEvidence resource:
                fields.AddRange(new string?[]
                {
                    resource.OwnerRole, Number(resource.RequestedQuantity),
                    Number(resource.CurrentValue), Number(resource.AvailableValue),
                    resource.ReservationState, resource.Lifecycle,
                    Boolean(resource.Active), Number(resource.CumulativeRequestedQuantity)
                });
                break;
            case MortalWoundSkillTierRequirementEvidence skill:
                fields.AddRange(new string?[]
                {
                    skill.ActorRole, skill.SkillId, Number(skill.MinimumTier), Number(skill.CurrentTier),
                    skill.ActorCurrentLocationId, skill.RequiredLocationId,
                    skill.ActorLifecycle, Boolean(skill.ActorActive),
                    Boolean(skill.ActorReachable), Boolean(skill.ActorPresent),
                    skill.SkillLifecycle, Boolean(skill.SkillActive)
                });
                break;
            case MortalWoundSourceCapabilityRequirementEvidence capability:
                fields.AddRange(new string?[]
                {
                    capability.ActorRole, capability.ActorCurrentLocationId,
                    capability.RequiredLocationId, capability.ActorLifecycle,
                    Boolean(capability.ActorActive), Boolean(capability.ActorReachable),
                    Boolean(capability.ActorPresent), capability.CapabilityLifecycle,
                    Boolean(capability.CapabilityActive)
                });
                break;
            case MortalWoundProviderRequirementEvidence provider:
                fields.AddRange(new string?[]
                {
                    provider.CurrentLocationId, provider.RequiredLocationId,
                    provider.Lifecycle, Boolean(provider.Active),
                    Boolean(provider.Reachable), Boolean(provider.Present)
                });
                break;
            case MortalWoundConsentRequirementEvidence consent:
                fields.AddRange(new string?[]
                {
                    consent.RequiredProviderRef, consent.RequiredTargetRef,
                    consent.ProviderCurrentLocationId, consent.RequiredLocationId,
                    consent.ProviderLifecycle, Boolean(consent.ProviderActive),
                    Boolean(consent.ProviderReachable), Boolean(consent.ProviderPresent),
                    consent.Status, consent.Lifecycle, Boolean(consent.Active)
                });
                break;
            case MortalWoundFacilityRequirementEvidence facility:
                fields.AddRange(new string?[]
                {
                    facility.FacilityLocationId, facility.RequiredLocationId,
                    facility.Lifecycle, Boolean(facility.Active),
                    Boolean(facility.Available), facility.RequiredActorKind,
                    facility.RequiredActorId, Boolean(facility.RequiredActorPresent)
                });
                break;
            case MortalWoundLocationRequirementEvidence location:
                fields.AddRange(new string?[]
                {
                    location.TargetRole, location.AuthorityLocationId,
                    location.RequiredLocationId, location.Lifecycle,
                    Boolean(location.Active), Boolean(location.TargetPresent),
                    Number(location.PresentActors.Count)
                });
                foreach (var actor in location.PresentActors)
                {
                    fields.Add(actor.ActorKind);
                    fields.Add(actor.ActorId);
                }
                break;
            case MortalWoundQuestStateRequirementEvidence quest:
                fields.AddRange(new string?[]
                {
                    quest.RequiredState, quest.CurrentState, quest.Lifecycle,
                    Boolean(quest.Active)
                });
                break;
            case MortalWoundEffectStateRequirementEvidence effect:
                fields.AddRange(new string?[]
                {
                    effect.RequiredState, effect.CurrentState, effect.Lifecycle,
                    Boolean(effect.Active)
                });
                break;
            case MortalWoundEnvironmentRequirementEvidence environment:
                fields.AddRange(new string?[]
                {
                    environment.RequiredState, environment.CurrentState,
                    environment.CurrentLocationId, environment.RequiredLocationId,
                    environment.Lifecycle, Boolean(environment.Active)
                });
                break;
            default:
                return Array.Empty<string?>();
        }
        return fields;
    }

    private static IEnumerable<string?> ActorFields(
        string kind,
        string id,
        string realm,
        string location,
        string lifecycle,
        bool active,
        bool reachable) => new string?[]
    {
        kind, id, realm, location, lifecycle, Boolean(active), Boolean(reachable)
    };

    private static string Fingerprint(string domain, IEnumerable<string?> fields) =>
        WoundAcceptedTurnFingerprintWriter.Compute(
            new string?[] { domain, "1" }.Concat(fields));

    private static string Number(int value) =>
        value.ToString(CultureInfo.InvariantCulture);

    private static string Number(long value) =>
        value.ToString(CultureInfo.InvariantCulture);

    private static string? NullableNumber(int? value) =>
        value?.ToString(CultureInfo.InvariantCulture);

    private static string Boolean(bool value) => value ? "true" : "false";
}
