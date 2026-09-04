namespace BookOfEternityClient.Services;

internal sealed partial class MortalWoundProcedureCheckAuthority
{
    internal bool MatchesFreshAcceptedState(
        MortalWoundTreatmentAcceptedStateAuthority acceptedState,
        MortalWoundTreatmentAttemptCoordinates coordinates,
        MortalWoundProcedureRouteDefinition route,
        WoundMaterializationEnvelope before,
        MortalWoundTreatmentRequirementAuthorityBundle requirementAuthority)
    {
        ArgumentNullException.ThrowIfNull(acceptedState);
        ArgumentNullException.ThrowIfNull(coordinates);
        ArgumentNullException.ThrowIfNull(route);
        ArgumentNullException.ThrowIfNull(before);
        ArgumentNullException.ThrowIfNull(requirementAuthority);

        try
        {
            var routeFingerprint = MortalWoundTreatmentRouteFingerprint.Compute(
                before,
                coordinates.RouteId);
            if (!string.Equals(
                    SourcePath,
                    LiveTurnPreparationService.TurnRequestPath,
                    StringComparison.Ordinal) ||
                !TryValidateRequirementAuthority(
                    coordinates,
                    route,
                    requirementAuthority,
                    routeFingerprint,
                    out var commonScope) ||
                commonScope is null ||
                !TryResolveModifier(
                    coordinates,
                    route,
                    commonScope,
                    out var expectedModifier,
                    out var expectedActorKind,
                    out var expectedActorId,
                    out var expectedRollSkillId))
            {
                return false;
            }
            var resolution = EffectRollContributionResolver.Resolve(
                acceptedState.EffectMechanics,
                new EffectRollContext(
                    coordinates.Realm,
                    expectedActorKind!,
                    expectedActorId!,
                    "skill_check",
                    expectedRollSkillId));
            if (!resolution.IsValid ||
                !string.Equals(RollMode, resolution.RollMode, StringComparison.Ordinal) ||
                !string.Equals(RollActorKind, expectedActorKind, StringComparison.Ordinal) ||
                !string.Equals(RollActorId, expectedActorId, StringComparison.Ordinal) ||
                !string.Equals(RollSkillId, expectedRollSkillId, StringComparison.Ordinal) ||
                Modifier != expectedModifier ||
                !ContributionsAgree(
                    RollContributions,
                    resolution.Contributions.Select(static contribution =>
                        MortalWoundProcedureRollContribution.Create(
                            contribution.EffectId,
                            contribution.ComponentId,
                            contribution.Contribution)).ToArray()))
            {
                return false;
            }

            var requiredDice = resolution.RollMode == "normal" ? 1 : 2;
            if (SourceIndices.Count != requiredDice ||
                SourceRolls.Count != requiredDice ||
                !SourceIndices.Select((value, offset) => value - offset)
                    .All(value => value == SourceIndices[0]) ||
                !acceptedState.MatchesProcedureDiceEvidence(
                    SourceIndices,
                    SourceRolls) ||
                !MatchesLiveReservationEvidence(acceptedState))
            {
                return false;
            }
            var selectedOrdinal = SelectSourceOrdinal(SourceRolls, resolution.RollMode);
            if (SelectedSourceIndex != SourceIndices[selectedOrdinal] ||
                NaturalRoll != SourceRolls[selectedOrdinal])
            {
                return false;
            }

            var complicationModifier = before.Complications
                .Where(static complication => string.Equals(
                    complication.State,
                    "active",
                    StringComparison.Ordinal))
                .Aggregate(0, static (sum, complication) => checked(
                    sum + complication.TreatmentDifficultyModifier));
            if (ComplicationDifficultyModifier != complicationModifier ||
                EffectiveDifficulty != checked(
                    route.Resolution.Difficulty + complicationModifier) ||
                !string.Equals(
                    RequirementAuthorityFingerprint,
                    requirementAuthority.AuthorityFingerprint,
                    StringComparison.Ordinal) ||
                !string.Equals(
                    CoordinatesFingerprint,
                    coordinates.CoordinatesFingerprint,
                    StringComparison.Ordinal) ||
                !string.Equals(
                    AcceptedStateFingerprint,
                    acceptedState.AcceptedStateFingerprint,
                    StringComparison.Ordinal))
            {
                return false;
            }

            var requiresReaction = NaturalRoll == 1 &&
                                   string.Equals(
                                       RollActorKind,
                                       "player",
                                       StringComparison.Ordinal) &&
                                   string.Equals(
                                       RollActorId,
                                       "player_current",
                                       StringComparison.Ordinal);
            if (!requiresReaction)
                return PreparedCriticalReaction is null;
            if (_diceReservation is not null)
                return MatchesLiveReactionEvidence();
            var oldest = FateShieldReactionArbiter.SelectOldest(
                acceptedState.EffectMechanics.FateShieldReactionCandidates,
                new HashSet<string>(StringComparer.Ordinal));
            return oldest is null
                ? PreparedCriticalReaction is null
                : PreparedCriticalReaction is not null &&
                  string.Equals(
                      oldest.EffectId,
                      PreparedCriticalReaction.EffectId,
                      StringComparison.Ordinal) &&
                  string.Equals(
                      oldest.TriggerId,
                      PreparedCriticalReaction.TriggerId,
                      StringComparison.Ordinal) &&
                  string.Equals(
                      oldest.AcceptedEffectFingerprint,
                      PreparedCriticalReaction.AcceptedEffectFingerprint,
                      StringComparison.Ordinal);
        }
        catch (Exception exception) when (exception is ArgumentException or
                                           InvalidOperationException or
                                           OverflowException or
                                           System.Text.Json.JsonException)
        {
            return false;
        }
    }

    private bool MatchesLiveReservationEvidence(
        MortalWoundTreatmentAcceptedStateAuthority acceptedState) =>
        _diceReservation is not null &&
        acceptedState.HasLiveProcedureReservationAgreement(this) &&
        string.Equals(
            _diceReservation.RollMode,
            RollMode,
            StringComparison.Ordinal) &&
        string.Equals(
            _diceReservation.CoordinatesFingerprint,
            CoordinatesFingerprint,
            StringComparison.Ordinal) &&
        string.Equals(
            _diceReservation.AcceptedStateFingerprint,
            AcceptedStateFingerprint,
            StringComparison.Ordinal) &&
        _diceReservation.SourceIndices.SequenceEqual(SourceIndices) &&
        _diceReservation.SourceRolls.SequenceEqual(SourceRolls);

    private bool MatchesLiveReactionEvidence() =>
        _criticalReactionReservation is null
            ? PreparedCriticalReaction is null
            : PreparedCriticalReaction is not null &&
              string.Equals(
                  _criticalReactionReservation.EffectId,
                  PreparedCriticalReaction.EffectId,
                  StringComparison.Ordinal) &&
              string.Equals(
                  _criticalReactionReservation.TriggerId,
                  PreparedCriticalReaction.TriggerId,
                  StringComparison.Ordinal) &&
              string.Equals(
                  _criticalReactionReservation.AcceptedEffectFingerprint,
                  PreparedCriticalReaction.AcceptedEffectFingerprint,
                  StringComparison.Ordinal) &&
              string.Equals(
                  _criticalReactionReservation.PreparedReactionFingerprint,
                  PreparedCriticalReaction.PreparedReactionFingerprint,
                  StringComparison.Ordinal);

    private static bool ContributionsAgree(
        IReadOnlyList<MortalWoundProcedureRollContribution> actual,
        IReadOnlyList<MortalWoundProcedureRollContribution> expected) =>
        actual.Count == expected.Count &&
        actual.Select((value, index) =>
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
                    StringComparison.Ordinal))
            .All(static value => value);
}
