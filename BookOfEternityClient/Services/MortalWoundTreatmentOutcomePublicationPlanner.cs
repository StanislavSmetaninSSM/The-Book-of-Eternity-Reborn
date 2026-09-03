using System.Collections.Immutable;

namespace BookOfEternityClient.Services;

internal sealed record MortalWoundTreatmentOutcomePublicationResult(
    WoundMaterializationEnvelope? After,
    WoundDeclaredTransitionOutcome? DeclaredOutcome,
    string? TransitionId,
    string? Fingerprint,
    IReadOnlyList<ValidationIssue> Issues)
{
    internal bool IsValid =>
        After is not null &&
        DeclaredOutcome is not null &&
        TransitionId is not null &&
        Fingerprint is not null &&
        Issues.Count == 0;
}

/// <summary>
/// Rebuilds the canonical scalar wound successor from the sealed treatment result.
/// Later effect-bearing and terminal outcome kinds extend this dispatcher instead of
/// creating another publication path.
/// </summary>
internal static class MortalWoundTreatmentOutcomePublicationPlanner
{
    private const string IssuePath = "treatmentPublication.outcome";

    internal static MortalWoundTreatmentOutcomePublicationResult Compose(
        MortalWoundTreatmentAcceptedStateAuthority acceptedState,
        MortalWoundTreatmentAttemptRequest request,
        MortalWoundTreatmentResolution resolution,
        long currentGameMinute)
    {
        ArgumentNullException.ThrowIfNull(acceptedState);
        ArgumentNullException.ThrowIfNull(request);
        ArgumentNullException.ThrowIfNull(resolution);

        var issues = ValidateCommon(
            acceptedState,
            request,
            resolution,
            currentGameMinute,
            out var recomposedIntent);
        if (issues.Count != 0 || recomposedIntent is null)
            return Invalid(issues);

        var transitionId = CreateTransitionId(resolution);
        var completedRoutes = resolution.RouteCompletion switch
        {
            "AppendOnce" => request.RouteSourceWound.Treatment.CompletedRouteIds
                .Append(request.Coordinates.RouteId)
                .Distinct(StringComparer.Ordinal)
                .ToImmutableArray(),
            "None" => request.RouteSourceWound.Treatment.CompletedRouteIds
                .ToImmutableArray(),
            _ => throw new InvalidOperationException(
                "Validated treatment route completion is not closed.")
        };

        var before = request.RouteSourceWound;
        var after = before with
        {
            Care = before.Care with
            {
                LastAttemptId = request.Coordinates.AttemptId
            },
            Treatment = before.Treatment with
            {
                CompletedRouteIds = completedRoutes
            },
            LastTransition = new WoundLastTransition(
                transitionId,
                checked(before.LastTransition.Ordinal + 1),
                request.Coordinates.Turn,
                "treat")
        };

        after = recomposedIntent switch
        {
            MortalWoundNoImprovementOutcomeIntent => after,
            MortalWoundStabilizeOutcomeIntent => ApplyStabilization(
                after,
                transitionId,
                currentGameMinute),
            _ => throw new InvalidOperationException(
                "Validated scalar treatment intent is not registered.")
        };

        var parsed = WoundMaterializationContract.Parse(
            WoundMaterializationContract.SerializeCanonical(after),
            IssuePath + ".afterWound");
        if (!parsed.IsValid || parsed.Wound is null)
            return Invalid(parsed.Issues);
        after = parsed.Wound;

        var declaredOutcome = CreateDeclaredOutcome(after);
        var fingerprint = ComputeFingerprint(
            before,
            after,
            resolution,
            transitionId,
            declaredOutcome);
        return new MortalWoundTreatmentOutcomePublicationResult(
            after,
            declaredOutcome,
            transitionId,
            fingerprint,
            Array.Empty<ValidationIssue>());
    }

    internal static string ComputeFingerprint(
        WoundMaterializationEnvelope before,
        WoundMaterializationEnvelope after,
        MortalWoundTreatmentResolution resolution,
        string transitionId,
        WoundDeclaredTransitionOutcome declaredOutcome)
    {
        ArgumentNullException.ThrowIfNull(before);
        ArgumentNullException.ThrowIfNull(after);
        ArgumentNullException.ThrowIfNull(resolution);
        ArgumentException.ThrowIfNullOrWhiteSpace(transitionId);
        ArgumentNullException.ThrowIfNull(declaredOutcome);
        var fields = new List<string?>
        {
            "book_of_eternity.mortal_wound_treatment.outcome_publication",
            "1",
            WoundMaterializationContract.SerializeCanonical(before),
            WoundMaterializationContract.SerializeCanonical(after),
            resolution.RequestFingerprint,
            resolution.ResolutionAuthorityFingerprint,
            resolution.ResultFingerprint,
            transitionId,
            declaredOutcome.ResultingSeverityRank.ToString(
                System.Globalization.CultureInfo.InvariantCulture),
            declaredOutcome.ResultingCareState,
            declaredOutcome.ResultingRecoveryProgress.ToString(
                System.Globalization.CultureInfo.InvariantCulture),
            declaredOutcome.Heals.ToString(),
            declaredOutcome.TerminalAttempt.ToString(),
            declaredOutcome.AllowsWorsening.ToString()
        };
        Append(fields, declaredOutcome.ResultingComplicationIds);
        Append(fields, declaredOutcome.ResultingEffectIds);
        Append(fields, declaredOutcome.ResultingRecoveryBlockers);
        Append(fields, declaredOutcome.ResultingCompletedRouteIds);
        return WoundAcceptedTurnFingerprintWriter.Compute(fields);
    }

    private static IReadOnlyList<ValidationIssue> ValidateCommon(
        MortalWoundTreatmentAcceptedStateAuthority acceptedState,
        MortalWoundTreatmentAttemptRequest request,
        MortalWoundTreatmentResolution resolution,
        long currentGameMinute,
        out MortalWoundTreatmentOutcomeIntent? recomposedIntent)
    {
        recomposedIntent = null;
        var failedAxes = new List<string>();
        if (!acceptedState.HasCurrentAdmissionAuthority())
            failedAxes.Add("accepted_state");
        if (!request.HasMatchingFingerprint() ||
            !string.Equals(
                resolution.RequestFingerprint,
                request.RequestFingerprint,
                StringComparison.Ordinal) ||
            !string.Equals(
                resolution.RequestAuthority?.RequestFingerprint,
                request.RequestFingerprint,
                StringComparison.Ordinal))
        {
            failedAxes.Add("request");
        }
        if (!request.Coordinates.MatchesAcceptedState(acceptedState) ||
            !string.Equals(
                resolution.Coordinates.CoordinatesFingerprint,
                request.Coordinates.CoordinatesFingerprint,
                StringComparison.Ordinal) ||
            currentGameMinute != acceptedState.CurrentGameMinute)
        {
            failedAxes.Add("coordinates");
        }
        if (!acceptedState.MatchesCurrentWound(request.RouteSourceWound) ||
            !string.Equals(
                request.RouteSourceWoundFingerprint,
                request.Coordinates.ExpectedBeforeFingerprint,
                StringComparison.Ordinal))
        {
            failedAxes.Add("before_wound");
        }
        if (!string.Equals(request.Mode, resolution.Mode, StringComparison.Ordinal) ||
            request.Mode is not ("guaranteed" or "procedure"))
        {
            failedAxes.Add("mode");
        }
        if (!string.Equals(
                resolution.AttemptDisposition,
                "AcceptedTerminal",
                StringComparison.Ordinal) ||
            resolution.Interruption)
        {
            failedAxes.Add("attempt_disposition");
        }
        if (resolution.CourseId is not null ||
            resolution.CourseMilestoneOrdinal is not null ||
            resolution.CourseDisposition is not null)
        {
            failedAxes.Add("course");
        }
        if (!MortalWoundTreatmentResolution.TryRecomputeModeEvidenceFingerprint(
                resolution,
                out _))
        {
            failedAxes.Add("mode_evidence");
        }

        var recomposed = MortalWoundTreatmentOutcomeIntentComposer.TryCompose(
            request,
            resolution.DeclaredResult,
            acceptedState,
            out var recomposedIntents,
            out _);
        if (!recomposed ||
            resolution.DeclaredResult.Count != 1 ||
            resolution.OutcomeIntents.Count != 1 ||
            recomposedIntents.Count != 1 ||
            !ScalarIntentAgrees(
                resolution.OutcomeIntents[0],
                recomposedIntents[0]))
        {
            failedAxes.Add("outcome");
        }
        else
        {
            recomposedIntent = recomposedIntents[0];
            var resultShape = recomposedIntent switch
            {
                MortalWoundStabilizeOutcomeIntent =>
                    string.Equals(
                        resolution.ResultCategory,
                        "success",
                        StringComparison.Ordinal) &&
                    resolution.SelectedOutcomeIndex is not null &&
                    string.Equals(
                        resolution.RouteCompletion,
                        "AppendOnce",
                        StringComparison.Ordinal),
                MortalWoundNoImprovementOutcomeIntent =>
                    string.Equals(
                        resolution.ResultCategory,
                        "failed_attempt",
                        StringComparison.Ordinal) &&
                    resolution.SelectedOutcomeIndex is not null &&
                    string.Equals(
                        resolution.RouteCompletion,
                        "None",
                        StringComparison.Ordinal),
                _ => false
            };
            if (!resultShape)
                failedAxes.Add("result_selection");
        }

        if (request.Mode == "guaranteed" &&
            resolution.CriticalReactionIntent is not null)
        {
            failedAxes.Add("critical_reaction");
        }
        if (failedAxes.Count == 0)
            return Array.Empty<ValidationIssue>();

        recomposedIntent = null;
        return new[]
        {
            Issue(
                "mortal_wound_treatment_publication_slice_unsupported",
                "one accepted-terminal guaranteed/procedure singleton stabilize or procedure singleton no_improvement result",
                string.Join(',', failedAxes))
        };
    }

    private static bool ScalarIntentAgrees(
        MortalWoundTreatmentOutcomeIntent actual,
        MortalWoundTreatmentOutcomeIntent expected) =>
        actual.GetType() == expected.GetType() &&
        (actual is MortalWoundNoImprovementOutcomeIntent or
            MortalWoundStabilizeOutcomeIntent) &&
        actual.OperationOrdinal == 0 &&
        expected.OperationOrdinal == 0 &&
        string.Equals(actual.Kind, expected.Kind, StringComparison.Ordinal) &&
        string.Equals(
            actual.DeclaredOperationFingerprint,
            expected.DeclaredOperationFingerprint,
            StringComparison.Ordinal) &&
        string.Equals(
            actual.IntentFingerprint,
            expected.IntentFingerprint,
            StringComparison.Ordinal);

    private static WoundMaterializationEnvelope ApplyStabilization(
        WoundMaterializationEnvelope after,
        string transitionId,
        long currentGameMinute)
    {
        var blockers = after.Recovery.Blockers
            .Where(static blocker => !string.Equals(
                blocker,
                "not_stabilized",
                StringComparison.Ordinal))
            .ToImmutableArray();
        var deteriorationAnchor = after.Recovery.DeteriorationAnchor is
            { ConditionKey: "not_stabilized" }
                ? null
                : after.Recovery.DeteriorationAnchor;
        return after with
        {
            Care = after.Care with
            {
                State = "stabilized",
                StabilizedAtTurn = after.LastTransition.Turn
            },
            Recovery = after.Recovery with
            {
                Blockers = blockers,
                RecoveryAnchor = new WoundRecoveryAnchor(
                    "stabilization",
                    currentGameMinute,
                    transitionId),
                DeteriorationAnchor = deteriorationAnchor
            }
        };
    }

    private static WoundDeclaredTransitionOutcome CreateDeclaredOutcome(
        WoundMaterializationEnvelope after) => new(
        after.Severity.Rank,
        after.Care.State,
        after.Recovery.CurrentStepProgress,
        Heals: false,
        TerminalAttempt: true,
        AllowsWorsening: false,
        after.Complications.Select(static value => value.ComplicationId).ToArray(),
        after.Consequences.OwnedEffectSources.RootBindings
            .Select(static value => value.EffectId)
            .OrderBy(static value => value, StringComparer.Ordinal)
            .ToArray(),
        after.Recovery.Blockers.ToArray(),
        after.Treatment.CompletedRouteIds.ToArray());

    private static string CreateTransitionId(
        MortalWoundTreatmentResolution resolution) =>
        "wound_transition_" + WoundAcceptedTurnFingerprintWriter.Compute(
            new string?[]
            {
                "book_of_eternity.wound.treatment_transition_identity",
                "1",
                resolution.Coordinates.SessionGeneration,
                resolution.Coordinates.SessionId,
                resolution.Coordinates.RequestId,
                resolution.Coordinates.SnapshotToken,
                resolution.Coordinates.OperationKey,
                resolution.Coordinates.AttemptId,
                resolution.RequestFingerprint,
                resolution.ResultFingerprint
            })["sha256:".Length..];

    private static void Append(
        ICollection<string?> fields,
        IReadOnlyList<string> values)
    {
        fields.Add(values.Count.ToString(
            System.Globalization.CultureInfo.InvariantCulture));
        foreach (var value in values)
            fields.Add(value);
    }

    private static MortalWoundTreatmentOutcomePublicationResult Invalid(
        IReadOnlyList<ValidationIssue> issues) => new(
        null,
        null,
        null,
        null,
        issues.ToArray());

    private static ValidationIssue Issue(
        string code,
        string expected,
        string actual) => new(
        IssuePath,
        IssueSeverity.Error,
        "The accepted Mortal wound treatment outcome could not be published.",
        code: code,
        actor: "Client",
        section: "wound_materialization",
        expected: expected,
        actual: actual,
        repairHint:
            "Reuse the exact sealed accepted terminal treatment while its authority remains current.");
}
