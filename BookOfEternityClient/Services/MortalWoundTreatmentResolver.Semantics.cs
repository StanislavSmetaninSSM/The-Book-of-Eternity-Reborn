using System.Globalization;
using System.Collections.Immutable;

namespace BookOfEternityClient.Services;

internal sealed record MortalWoundTreatmentComplicationRootBinding(
    string LocalDefinitionRef, string DefinitionRef, string ApplicationRef, string OperationKey);

internal sealed class MortalWoundTreatmentComplicationBindingPreparation
{
    private MortalWoundTreatmentComplicationBindingPreparation(string complicationRef, string complicationId,
        IEnumerable<MortalWoundTreatmentReferenceBinding> definitions,
        IEnumerable<MortalWoundTreatmentReferenceBinding> applications, string fingerprint,
        IEnumerable<MortalWoundTreatmentComplicationRootBinding> roots)
    {
        ComplicationRef = complicationRef;
        ComplicationId = complicationId;
        DefinitionReferenceBindings = definitions.Select(row => MortalWoundTreatmentReferenceBinding.Create(row.LocalRef, row.NamespacedRef)).ToImmutableArray();
        ApplicationReferenceBindings = applications.Select(row => MortalWoundTreatmentReferenceBinding.Create(row.LocalRef, row.NamespacedRef)).ToImmutableArray();
        PreparationFingerprint = fingerprint;
        Roots = roots.Select(row => row with { }).ToImmutableArray();
    }
    internal string ComplicationRef { get; }
    internal string ComplicationId { get; }
    internal ImmutableArray<MortalWoundTreatmentReferenceBinding> DefinitionReferenceBindings { get; }
    internal ImmutableArray<MortalWoundTreatmentReferenceBinding> ApplicationReferenceBindings { get; }
    internal string PreparationFingerprint { get; }
    internal ImmutableArray<MortalWoundTreatmentComplicationRootBinding> Roots { get; }
    internal static MortalWoundTreatmentComplicationBindingPreparation Create(string complicationRef, string complicationId,
        IEnumerable<MortalWoundTreatmentReferenceBinding> definitions, IEnumerable<MortalWoundTreatmentReferenceBinding> applications,
        string fingerprint, IEnumerable<MortalWoundTreatmentComplicationRootBinding> roots) =>
        new(complicationRef, complicationId, definitions, applications, fingerprint, roots);
}

internal sealed partial class MortalWoundCriticalReactionResolutionResult
{
    internal static MortalWoundCriticalReactionResolutionResult Valid(
        MortalWoundCriticalReactionIntent? intent) => new(
        true,
        Array.Empty<ValidationIssue>(),
        intent);
}

internal sealed partial class MortalWoundCriticalReactionIntent
{
    internal static MortalWoundCriticalReactionIntent Create(
        MortalWoundTreatmentAttemptRequest request,
        MortalWoundPreparedCriticalReaction prepared)
    {
        ArgumentNullException.ThrowIfNull(request);
        ArgumentNullException.ThrowIfNull(prepared);
        var coordinates = request.Coordinates;
        var eventRef = string.Join(
            ':',
            "turn_" + coordinates.Turn.ToString(CultureInfo.InvariantCulture),
            "wound_treatment",
            coordinates.AttemptId,
            "critical_reaction");
        var causalEventRef = string.Join(
            ':',
            "turn_" + coordinates.Turn.ToString(CultureInfo.InvariantCulture),
            "wound_treatment",
            coordinates.AttemptId,
            "procedure_roll");
        var intentFingerprint = WoundAcceptedTurnFingerprintWriter.Compute(
            new string?[]
            {
                "book_of_eternity.mortal_wound_treatment.critical_reaction_intent",
                "1",
                eventRef,
                causalEventRef,
                coordinates.Turn.ToString(CultureInfo.InvariantCulture),
                coordinates.Realm,
                "player",
                "player_current",
                prepared.EffectId,
                prepared.TriggerId,
                prepared.AcceptedEffectFingerprint,
                prepared.PreparedReactionFingerprint,
                request.RequestFingerprint
            });
        return new MortalWoundCriticalReactionIntent(
            "owner_critical_failure",
            eventRef,
            causalEventRef,
            coordinates.Turn,
            coordinates.Realm,
            "player",
            "player_current",
            prepared.EffectId,
            prepared.TriggerId,
            prepared.AcceptedEffectFingerprint,
            prepared.PreparedReactionFingerprint,
            request.RequestFingerprint,
            intentFingerprint);
    }
}

internal sealed partial class MortalWoundTreatmentAttemptRequest
{
    internal bool HasMatchingFingerprint()
    {
        try
        {
            return string.Equals(
                RequestFingerprint,
                ComputeFingerprint(
                    Mode,
                    Coordinates,
                    MilestoneOrdinal,
                    RouteSourceWoundFingerprint,
                    ModeAuthority,
                    RequirementAuthority,
                    ResourceAuthority),
                StringComparison.Ordinal);
        }
        catch (Exception exception) when (exception is ArgumentException or
                                           InvalidOperationException or
                                           OverflowException)
        {
            return false;
        }
    }
}

internal sealed partial class MortalWoundTreatmentResolutionResult
{
    internal static MortalWoundTreatmentResolutionResult Resolved(
        MortalWoundTreatmentResolution resolution)
    {
        ArgumentNullException.ThrowIfNull(resolution);
        return new MortalWoundTreatmentResolutionResult(
            "Resolved",
            Array.Empty<ValidationIssue>(),
            resolution,
            null);
    }

    internal static MortalWoundTreatmentResolutionResult Rejected(
        IEnumerable<ValidationIssue> issues) => new(
        "Rejected",
        issues,
        null,
        null);

    internal static MortalWoundTreatmentResolutionResult ExactReplay(
        MortalWoundTreatmentReceipt receipt)
    {
        ArgumentNullException.ThrowIfNull(receipt);
        return new MortalWoundTreatmentResolutionResult(
            "ExactReplay",
            Array.Empty<ValidationIssue>(),
            null,
            receipt);
    }

    internal static MortalWoundTreatmentResolutionResult Conflict(
        IEnumerable<ValidationIssue> issues) => new(
        "Conflict",
        issues,
        null,
        null);
}

internal sealed partial class MortalWoundTreatmentResolution
{
    internal static MortalWoundTreatmentResolution Create(
        string mode,
        MortalWoundTreatmentAttemptCoordinates coordinates,
        string attemptDisposition,
        string resultCategory,
        int? selectedOutcomeIndex,
        bool interruption,
        IEnumerable<MortalWoundTreatmentOperation> declaredResult,
        IEnumerable<MortalWoundTreatmentOutcomeIntent> outcomeIntents,
        MortalWoundCriticalReactionIntent? criticalReactionIntent,
        string consumptionTrigger,
        string? courseId,
        int? courseMilestoneOrdinal,
        string? courseDisposition,
        MortalWoundTreatmentAttemptRequest requestAuthority,
        MortalWoundTreatmentModeEvidence modeEvidence,
        string routeFingerprint,
        string routeCompletion)
    {
        ArgumentNullException.ThrowIfNull(coordinates);
        ArgumentNullException.ThrowIfNull(requestAuthority);
        ArgumentNullException.ThrowIfNull(modeEvidence);
        var declared = declaredResult.ToArray();
        var intents = outcomeIntents.ToArray();
        if (declared.Length != intents.Length ||
            !string.Equals(requestAuthority.Mode, mode, StringComparison.Ordinal) ||
            !string.Equals(
                requestAuthority.Coordinates.CoordinatesFingerprint,
                coordinates.CoordinatesFingerprint,
                StringComparison.Ordinal))
        {
            throw new InvalidOperationException(
                "The treatment result does not agree with its sealed request and intent cardinality.");
        }
        for (var ordinal = 0; ordinal < declared.Length; ordinal++)
        {
            var expectedDeclaredFingerprint =
                MortalWoundTreatmentOutcomeIntentComposer.DeclaredFingerprint(
                    ordinal,
                    declared[ordinal]);
            if (intents[ordinal].OperationOrdinal != ordinal ||
                !string.Equals(
                    intents[ordinal].Kind,
                    declared[ordinal].Kind,
                    StringComparison.Ordinal) ||
                !string.Equals(
                    intents[ordinal].DeclaredOperationFingerprint,
                    expectedDeclaredFingerprint,
                    StringComparison.Ordinal))
            {
                throw new InvalidOperationException(
                    "The treatment result intent does not agree with its declared operation.");
            }
        }
        var resolutionAuthorityFingerprint = ComputeResolutionAuthorityFingerprint(
            requestAuthority.RequestFingerprint,
            mode,
            attemptDisposition,
            resultCategory,
            selectedOutcomeIndex,
            interruption,
            consumptionTrigger,
            courseId,
            courseMilestoneOrdinal,
            courseDisposition,
            routeFingerprint,
            routeCompletion,
            criticalReactionIntent?.IntentFingerprint,
            ComputeModeEvidenceFingerprint(modeEvidence));
        var resultFingerprint = ComputeResultFingerprint(
            resolutionAuthorityFingerprint,
            declared);
        return new MortalWoundTreatmentResolution(
            mode,
            coordinates,
            attemptDisposition,
            resultCategory,
            selectedOutcomeIndex,
            interruption,
            declared,
            intents,
            criticalReactionIntent,
            consumptionTrigger,
            courseId,
            courseMilestoneOrdinal,
            courseDisposition,
            requestAuthority,
            requestAuthority.RequirementAuthority,
            requestAuthority.ResourceAuthority,
            modeEvidence,
            routeFingerprint,
            resolutionAuthorityFingerprint,
            requestAuthority.RequestFingerprint,
            resultFingerprint,
            routeCompletion);
    }

    internal static string ComputeModeEvidenceFingerprint(
        MortalWoundTreatmentModeEvidence evidence) =>
        evidence switch
        {
            MortalWoundProcedureModeEvidence procedure => procedure.AcceptedRollFingerprint,
            MortalWoundCourseModeEvidence course => course.ClockEvidenceFingerprint,
            MortalWoundGuaranteedModeEvidence guaranteed => guaranteed.CapabilityProofFingerprint,
            _ => throw new ArgumentException(
                "Unsupported Mortal wound-treatment mode evidence.",
                nameof(evidence))
        };

    internal static bool TryRecomputeModeEvidenceFingerprint(
        MortalWoundTreatmentResolution resolution,
        out string? fingerprint)
    {
        ArgumentNullException.ThrowIfNull(resolution);
        fingerprint = null;
        var request = resolution.RequestAuthority;
        if (!MortalWoundTreatmentDetachedSealValidator.TryGetRoute(
                request,
                out var selectedRoute) ||
            selectedRoute is null)
        {
            return false;
        }

        MortalWoundTreatmentModeEvidence expected;
        try
        {
            expected = (request.ModeAuthority, selectedRoute) switch
            {
                (MortalWoundProcedureCheckAuthority authority,
                    MortalWoundProcedureRouteDefinition route) =>
                    RecomputeProcedureEvidence(resolution, request, authority, route),
                (MortalWoundCourseModeAuthority authority,
                    MortalWoundCourseRouteDefinition route) =>
                    RecomputeCourseEvidence(resolution, request, authority, route),
                (MortalWoundTreatmentCapabilityProof proof,
                    MortalWoundGuaranteedRouteDefinition route) =>
                    RecomputeGuaranteedEvidence(resolution, proof, route),
                _ => throw new InvalidOperationException(
                    "The mode authority and route do not form one closed evidence case.")
            };
        }
        catch (Exception exception) when (exception is ArgumentException or
                                           InvalidOperationException or
                                           OverflowException)
        {
            return false;
        }

        if (!ModeEvidenceEquals(resolution.ModeEvidence, expected))
            return false;
        fingerprint = ComputeModeEvidenceFingerprint(expected);
        return true;
    }

    private static MortalWoundProcedureModeEvidence RecomputeProcedureEvidence(
        MortalWoundTreatmentResolution resolution,
        MortalWoundTreatmentAttemptRequest request,
        MortalWoundProcedureCheckAuthority authority,
        MortalWoundProcedureRouteDefinition route)
    {
        if (!string.Equals(resolution.Mode, "procedure", StringComparison.Ordinal) ||
            resolution.Interruption)
            throw new InvalidOperationException("Procedure evidence requires procedure mode.");
        var total = checked((long)authority.NaturalRoll + authority.Modifier);
        var margin = checked(total - authority.EffectiveDifficulty);
        var expectedReaction = authority.PreparedCriticalReaction is null
            ? null
            : MortalWoundCriticalReactionIntent.Create(
                request,
                authority.PreparedCriticalReaction);
        if (!string.Equals(
                expectedReaction?.IntentFingerprint,
                resolution.CriticalReactionIntent?.IntentFingerprint,
                StringComparison.Ordinal))
        {
            throw new InvalidOperationException(
                "The critical reaction does not match the sealed procedure authority.");
        }
        var selectedIndex = MortalWoundTreatmentPlanner.SelectProcedureBand(
            route,
            authority.NaturalRoll,
            margin,
            expectedReaction is not null);
        if (selectedIndex < 0 ||
            selectedIndex >= route.Bands.Length ||
            resolution.SelectedOutcomeIndex != selectedIndex ||
            !string.Equals(
                resolution.ResultCategory,
                route.Bands[selectedIndex].Category,
                StringComparison.Ordinal) ||
            !DeclaredResultsEqual(
                resolution.DeclaredResult,
                route.Bands[selectedIndex].DeclaredResult))
        {
            throw new InvalidOperationException(
                "The selected procedure outcome does not match the sealed roll.");
        }
        var originalOutcome = authority.NaturalRoll switch
        {
            20 => "critical_success",
            1 => "critical_failure",
            _ => "ordinary"
        };
        var resolvedOutcome = authority.NaturalRoll == 1 && expectedReaction is not null
            ? "failure"
            : originalOutcome;
        return MortalWoundProcedureModeEvidence.Create(
            authority,
            route,
            total,
            margin,
            originalOutcome,
            resolvedOutcome,
            route.Bands[selectedIndex],
            selectedIndex,
            expectedReaction);
    }

    private static MortalWoundCourseModeEvidence RecomputeCourseEvidence(
        MortalWoundTreatmentResolution resolution,
        MortalWoundTreatmentAttemptRequest request,
        MortalWoundCourseModeAuthority authority,
        MortalWoundCourseRouteDefinition route)
    {
        if (!string.Equals(resolution.Mode, "course", StringComparison.Ordinal) ||
            request.MilestoneOrdinal != authority.MilestoneOrdinal ||
            resolution.CourseMilestoneOrdinal != authority.MilestoneOrdinal ||
            !string.Equals(resolution.CourseId, authority.CourseId, StringComparison.Ordinal) ||
            !string.Equals(resolution.RouteFingerprint, authority.CourseStartAuthority.RouteFingerprint,
                StringComparison.Ordinal) ||
            !string.Equals(resolution.RouteFingerprint, request.RequirementAuthority.RouteFingerprint,
                StringComparison.Ordinal))
        {
            throw new InvalidOperationException("Course evidence requires one current milestone.");
        }
        var milestones = route.Milestones.Where(candidate =>
            candidate.Ordinal == authority.MilestoneOrdinal).ToArray();
        if (milestones.Length != 1)
            throw new InvalidOperationException("The current course milestone is ambiguous.");
        var interrupted = request.RequirementAuthority.InterruptionReason is not null;
        var expectedDisposition = interrupted
            ? "interrupted"
            : milestones[0].Completion;
        var expectedOutcomeIndex = interrupted
            ? (int?)null
            : authority.MilestoneOrdinal - 1;
        var expectedCategory = interrupted
            ? route.Interruption.Category
            : milestones[0].Category;
        var expectedDeclaredResult = interrupted
            ? route.Interruption.DeclaredResult
            : milestones[0].DeclaredResult;
        if (resolution.Interruption != interrupted ||
            resolution.SelectedOutcomeIndex != expectedOutcomeIndex ||
            !string.Equals(
                resolution.CourseDisposition,
                expectedDisposition,
                StringComparison.Ordinal) ||
            !string.Equals(
                resolution.ResultCategory,
                expectedCategory,
                StringComparison.Ordinal) ||
            !DeclaredResultsEqual(
                resolution.DeclaredResult,
                expectedDeclaredResult))
        {
            throw new InvalidOperationException(
                "The course result does not match its current milestone evidence.");
        }
        return MortalWoundCourseModeEvidence.Create(authority, expectedDisposition);
    }

    private static MortalWoundGuaranteedModeEvidence RecomputeGuaranteedEvidence(
        MortalWoundTreatmentResolution resolution,
        MortalWoundTreatmentCapabilityProof proof,
        MortalWoundGuaranteedRouteDefinition route)
    {
        if (!string.Equals(resolution.Mode, "guaranteed", StringComparison.Ordinal) ||
            resolution.SelectedOutcomeIndex != 0 ||
            resolution.Interruption ||
            !string.Equals(
                resolution.ResultCategory,
                route.Outcome.Category,
                StringComparison.Ordinal) ||
            !DeclaredResultsEqual(
                resolution.DeclaredResult,
                route.Outcome.DeclaredResult))
        {
            throw new InvalidOperationException(
                "Guaranteed evidence requires the sole non-interrupted outcome.");
        }
        return MortalWoundGuaranteedModeEvidence.Create(proof, route);
    }

    private static bool DeclaredResultsEqual(
        IReadOnlyList<MortalWoundTreatmentOperation> actual,
        IReadOnlyList<MortalWoundTreatmentOperation> expected)
    {
        if (actual.Count != expected.Count)
            return false;
        for (var ordinal = 0; ordinal < actual.Count; ordinal++)
        {
            if (!string.Equals(
                    MortalWoundTreatmentOutcomeIntentComposer.DeclaredFingerprint(
                        ordinal,
                        actual[ordinal]),
                    MortalWoundTreatmentOutcomeIntentComposer.DeclaredFingerprint(
                        ordinal,
                        expected[ordinal]),
                    StringComparison.Ordinal))
            {
                return false;
            }
        }
        return true;
    }

    private static bool ModeEvidenceEquals(
        MortalWoundTreatmentModeEvidence actual,
        MortalWoundTreatmentModeEvidence expected) => (actual, expected) switch
    {
        (MortalWoundProcedureModeEvidence left,
            MortalWoundProcedureModeEvidence right) =>
            string.Equals(left.RollMode, right.RollMode, StringComparison.Ordinal) &&
            string.Equals(left.RollActorKind, right.RollActorKind,
                StringComparison.Ordinal) &&
            string.Equals(left.RollActorId, right.RollActorId,
                StringComparison.Ordinal) &&
            left.SourceIndices.SequenceEqual(right.SourceIndices) &&
            left.SourceRolls.SequenceEqual(right.SourceRolls) &&
            left.SelectedSourceIndex == right.SelectedSourceIndex &&
            left.NaturalRoll == right.NaturalRoll &&
            left.Modifier == right.Modifier &&
            left.Total == right.Total &&
            left.BaseDifficulty == right.BaseDifficulty &&
            left.ComplicationDifficultyModifier ==
                right.ComplicationDifficultyModifier &&
            left.EffectiveDifficulty == right.EffectiveDifficulty &&
            left.Margin == right.Margin &&
            string.Equals(left.OriginalOutcome, right.OriginalOutcome,
                StringComparison.Ordinal) &&
            string.Equals(left.ResolvedOutcome, right.ResolvedOutcome,
                StringComparison.Ordinal) &&
            string.Equals(left.SelectedBandId, right.SelectedBandId,
                StringComparison.Ordinal) &&
            left.SelectedOutcomeIndex == right.SelectedOutcomeIndex &&
            string.Equals(left.ReactionEffectId, right.ReactionEffectId,
                StringComparison.Ordinal) &&
            string.Equals(left.ReactionTriggerId, right.ReactionTriggerId,
                StringComparison.Ordinal) &&
            string.Equals(left.ReactionFingerprint, right.ReactionFingerprint,
                StringComparison.Ordinal) &&
            string.Equals(left.AcceptedRollFingerprint, right.AcceptedRollFingerprint,
                StringComparison.Ordinal),
        (MortalWoundCourseModeEvidence left, MortalWoundCourseModeEvidence right) =>
            string.Equals(left.CourseId, right.CourseId, StringComparison.Ordinal) &&
            left.MilestoneOrdinal == right.MilestoneOrdinal &&
            left.CourseStartedAtGameTimeMinutes == right.CourseStartedAtGameTimeMinutes &&
            left.ResolvedAtGameTimeMinutes == right.ResolvedAtGameTimeMinutes &&
            string.Equals(left.ClockEvidenceFingerprint, right.ClockEvidenceFingerprint,
                StringComparison.Ordinal) &&
            string.Equals(left.CourseDisposition, right.CourseDisposition,
                StringComparison.Ordinal),
        (MortalWoundGuaranteedModeEvidence left,
            MortalWoundGuaranteedModeEvidence right) =>
            string.Equals(left.CapabilityRef, right.CapabilityRef,
                StringComparison.Ordinal) &&
            string.Equals(left.ActorRole, right.ActorRole, StringComparison.Ordinal) &&
            string.Equals(left.SkillId, right.SkillId, StringComparison.Ordinal) &&
            string.Equals(left.SourceSemanticFingerprint,
                right.SourceSemanticFingerprint, StringComparison.Ordinal) &&
            string.Equals(left.CapabilityProofFingerprint,
                right.CapabilityProofFingerprint, StringComparison.Ordinal),
        _ => false
    };

    internal static string ComputeResolutionAuthorityFingerprint(
        string requestFingerprint,
        string mode,
        string attemptDisposition,
        string resultCategory,
        int? selectedOutcomeIndex,
        bool interruption,
        string consumptionTrigger,
        string? courseId,
        int? courseMilestoneOrdinal,
        string? courseDisposition,
        string routeFingerprint,
        string routeCompletion,
        string? criticalReactionFingerprint,
        string modeEvidenceFingerprint) =>
        WoundAcceptedTurnFingerprintWriter.Compute(new string?[]
        {
            "book_of_eternity.mortal_wound_treatment.resolution_authority",
            "1",
            requestFingerprint,
            mode,
            attemptDisposition,
            resultCategory,
            selectedOutcomeIndex?.ToString(CultureInfo.InvariantCulture),
            interruption ? "true" : "false",
            consumptionTrigger,
            courseId,
            courseMilestoneOrdinal?.ToString(CultureInfo.InvariantCulture),
            courseDisposition,
            routeFingerprint,
            routeCompletion,
            criticalReactionFingerprint,
            modeEvidenceFingerprint
        });

    internal static string ComputeResultFingerprint(
        string resolutionAuthorityFingerprint,
        IReadOnlyList<MortalWoundTreatmentOperation> declaredResult)
    {
        var fields = new List<string?>
        {
            "book_of_eternity.mortal_wound_treatment.result",
            "1",
            resolutionAuthorityFingerprint,
            declaredResult.Count.ToString(CultureInfo.InvariantCulture)
        };
        for (var ordinal = 0; ordinal < declaredResult.Count; ordinal++)
        {
            fields.Add(MortalWoundTreatmentOutcomeIntentComposer.DeclaredFingerprint(
                ordinal,
                declaredResult[ordinal]));
        }
        return WoundAcceptedTurnFingerprintWriter.Compute(fields);
    }
}

internal sealed partial class MortalWoundProcedureModeEvidence
{
    internal static MortalWoundProcedureModeEvidence Create(
        MortalWoundProcedureCheckAuthority authority,
        MortalWoundProcedureRouteDefinition route,
        long total,
        long margin,
        string originalOutcome,
        string resolvedOutcome,
        MortalWoundProcedureBand selectedBand,
        int selectedOutcomeIndex,
        MortalWoundCriticalReactionIntent? reactionIntent)
    {
        ArgumentNullException.ThrowIfNull(authority);
        ArgumentNullException.ThrowIfNull(route);
        ArgumentNullException.ThrowIfNull(selectedBand);
        var fingerprint = WoundAcceptedTurnFingerprintWriter.Compute(
            new string?[]
            {
                "book_of_eternity.mortal_wound_treatment.procedure_evidence",
                "1",
                authority.AuthorityFingerprint,
                total.ToString(CultureInfo.InvariantCulture),
                route.Resolution.Difficulty.ToString(CultureInfo.InvariantCulture),
                margin.ToString(CultureInfo.InvariantCulture),
                originalOutcome,
                resolvedOutcome,
                selectedBand.BandId,
                selectedOutcomeIndex.ToString(CultureInfo.InvariantCulture),
                reactionIntent?.IntentFingerprint
            });
        return new MortalWoundProcedureModeEvidence(
            authority.RollMode,
            authority.RollActorKind,
            authority.RollActorId,
            authority.SourceIndices,
            authority.SourceRolls,
            authority.SelectedSourceIndex,
            authority.NaturalRoll,
            authority.Modifier,
            total,
            route.Resolution.Difficulty,
            authority.ComplicationDifficultyModifier,
            authority.EffectiveDifficulty,
            margin,
            originalOutcome,
            resolvedOutcome,
            selectedBand.BandId,
            selectedOutcomeIndex,
            reactionIntent?.EffectId,
            reactionIntent?.TriggerId,
            reactionIntent?.IntentFingerprint,
            fingerprint);
    }
}

internal sealed partial class MortalWoundGuaranteedModeEvidence
{
    internal static MortalWoundGuaranteedModeEvidence Create(
        MortalWoundTreatmentCapabilityProof proof,
        MortalWoundGuaranteedRouteDefinition route)
    {
        ArgumentNullException.ThrowIfNull(proof);
        ArgumentNullException.ThrowIfNull(route);
        return new MortalWoundGuaranteedModeEvidence(
            proof.CapabilityRef,
            route.Resolution.ActorRole,
            proof.SkillId,
            proof.SourceSemanticFingerprint,
            proof.ProofFingerprint);
    }
}

internal sealed partial class MortalWoundCourseModeEvidence
{
    internal static MortalWoundCourseModeEvidence Create(
        MortalWoundCourseModeAuthority authority,
        string courseDisposition)
    {
        ArgumentNullException.ThrowIfNull(authority);
        var fingerprint = WoundAcceptedTurnFingerprintWriter.Compute(
            new string?[]
            {
                "book_of_eternity.mortal_wound_treatment.course_evidence",
                "1",
                authority.AuthorityFingerprint,
                authority.CourseId,
                authority.MilestoneOrdinal.ToString(CultureInfo.InvariantCulture),
                authority.CourseStartAuthority.StartedAtGameTimeMinutes.ToString(
                    CultureInfo.InvariantCulture),
                authority.GameTimeAuthority.CurrentTimeInMinutes.ToString(
                    CultureInfo.InvariantCulture),
                authority.WindowDisposition,
                courseDisposition
            });
        return new MortalWoundCourseModeEvidence(
            authority.CourseId,
            authority.MilestoneOrdinal,
            authority.CourseStartAuthority.StartedAtGameTimeMinutes,
            authority.GameTimeAuthority.CurrentTimeInMinutes,
            fingerprint,
            courseDisposition);
    }
}

internal static class MortalWoundTreatmentOutcomeIntentComposer
{
    internal static bool TryCompose(
        MortalWoundTreatmentAttemptRequest request,
        IReadOnlyList<MortalWoundTreatmentOperation> operations,
        MortalWoundTreatmentAcceptedStateAuthority acceptedState,
        out IReadOnlyList<MortalWoundTreatmentOutcomeIntent> intents,
        out IReadOnlyList<ValidationIssue> issues)
    {
        ArgumentNullException.ThrowIfNull(acceptedState);
        var composed = new List<MortalWoundTreatmentOutcomeIntent>(operations.Count);
        var failures = new List<ValidationIssue>();
        for (var ordinal = 0; ordinal < operations.Count; ordinal++)
        {
            var operation = operations[ordinal];
            var declaredFingerprint = DeclaredFingerprint(ordinal, operation);
            var intentFingerprint = IntentFingerprint(
                request.RequestFingerprint,
                ordinal,
                operation,
                declaredFingerprint);
            MortalWoundTreatmentOutcomeIntent? intent = operation switch
            {
                MortalWoundNoImprovementOperation =>
                    MortalWoundNoImprovementOutcomeIntent.Create(
                        ordinal,
                        declaredFingerprint,
                        intentFingerprint),
                MortalWoundStabilizeOperation =>
                    MortalWoundStabilizeOutcomeIntent.Create(
                        ordinal,
                        declaredFingerprint,
                        intentFingerprint),
                MortalWoundAddRecoveryOperation addRecovery =>
                    MortalWoundAddRecoveryOutcomeIntent.Create(
                        ordinal,
                        declaredFingerprint,
                        intentFingerprint,
                        addRecovery.Points),
                MortalWoundReduceSeverityOperation reduceSeverity =>
                    MortalWoundReduceSeverityOutcomeIntent.Create(
                        ordinal,
                        declaredFingerprint,
                        intentFingerprint,
                        reduceSeverity.Steps),
                MortalWoundRemoveComplicationOperation removeComplication =>
                    MortalWoundRemoveComplicationOutcomeIntent.Create(
                        ordinal,
                        declaredFingerprint,
                        intentFingerprint,
                        removeComplication.ComplicationId),
                MortalWoundAddComplicationOperation addComplication =>
                    ComposeAddComplicationIntent(
                        request,
                        ordinal,
                        declaredFingerprint,
                        intentFingerprint,
                        addComplication),
                MortalWoundHealOperation heal => ComposeHealIntent(
                    request,
                    ordinal,
                    declaredFingerprint,
                    intentFingerprint,
                    heal),
                _ => null
            };
            if (operation is MortalWoundApplyDeteriorationOperation deterioration)
            {
                if (!MortalWoundTreatmentDeteriorationPreparation.TryCreate(
                        request,
                        ordinal,
                        deterioration,
                        acceptedState,
                        out var preparedPolicy,
                        out var policyIssues))
                {
                    failures.AddRange(policyIssues);
                    continue;
                }
                intent = MortalWoundApplyDeteriorationOutcomeIntent.CreatePrepared(
                    preparedPolicy!);
            }
            if (intent is null)
            {
                failures.Add(new ValidationIssue(
                    "treatmentAttempt.result",
                    IssueSeverity.Error,
                    "The selected Mortal wound-treatment operation cannot be reduced to its typed intent.",
                    code: "mortal_wound_treatment_outcome_intent_unsupported",
                    actor: "Client",
                    section: "wound_materialization",
                    expected: "one closed production-derived treatment outcome intent",
                    actual: operation.Kind));
                continue;
            }
            composed.Add(intent);
        }

        intents = composed;
        issues = failures;
        return failures.Count == 0;
    }

    private static MortalWoundAddComplicationOutcomeIntent ComposeAddComplicationIntent(
        MortalWoundTreatmentAttemptRequest request,
        int ordinal,
        string declaredFingerprint,
        string baseIntentFingerprint,
        MortalWoundAddComplicationOperation operation)
    {
        var binding = PrepareComplicationBindings(request.RequestFingerprint, ordinal,
            operation.ComplicationDraft, declaredFingerprint);
        var intentFingerprint = WoundAcceptedTurnFingerprintWriter.Compute(
            new string?[]
            {
                "book_of_eternity.mortal_wound_treatment.complication_intent",
                "1", baseIntentFingerprint, binding.ComplicationId, binding.PreparationFingerprint
            });
        return MortalWoundAddComplicationOutcomeIntent.Create(ordinal, declaredFingerprint,
            intentFingerprint, binding.ComplicationRef, binding.ComplicationId,
            binding.DefinitionReferenceBindings, binding.ApplicationReferenceBindings, binding.PreparationFingerprint);
    }

    internal static MortalWoundTreatmentComplicationBindingPreparation PrepareComplicationBindings(
        string requestFingerprint, int operationOrdinal,
        MortalWoundComplicationProposalDraft draft, string declaredOperationFingerprint)
    {
        var ordinal = operationOrdinal;
        var declaredFingerprint = declaredOperationFingerprint;
        var complicationRef = draft.Complication.ComplicationRef;
        var complicationId = "mortal_wound_complication_" +
                            MortalWoundTreatmentIdentityWriter.Digest(
                                "complication",
                                requestFingerprint,
                                ordinal.ToString(CultureInfo.InvariantCulture),
                                complicationRef);
        var definitionBindings = draft.ConsequenceDefinitions
            .Select(definition => MortalWoundTreatmentReferenceBinding.Create(
                definition.DefinitionRef,
                ordinal.ToString(CultureInfo.InvariantCulture) + "/" +
                complicationRef + "/" + definition.DefinitionRef))
            .ToArray();
        var applicationBindings = draft.ConsequenceDefinitions
            .Where(static definition => definition.Root is not null)
            .Select(definition =>
            {
                var localRef = definition.DefinitionRef + "_application";
                return MortalWoundTreatmentReferenceBinding.Create(
                    localRef,
                    ordinal.ToString(CultureInfo.InvariantCulture) + "/" +
                    complicationRef + "/" + localRef);
            })
            .ToArray();
        var preparationFields = new List<string?>
        {
            "book_of_eternity.mortal_wound_treatment.complication_preparation",
            "1",
            requestFingerprint,
            ordinal.ToString(CultureInfo.InvariantCulture),
            complicationRef,
            complicationId,
            declaredFingerprint
        };
        preparationFields.AddRange(definitionBindings.SelectMany(static binding =>
            new[] { binding.LocalRef, binding.NamespacedRef }));
        preparationFields.AddRange(applicationBindings.SelectMany(static binding =>
            new[] { binding.LocalRef, binding.NamespacedRef }));
        var preparationFingerprint = WoundAcceptedTurnFingerprintWriter.Compute(
            preparationFields);
        var roots = draft.ConsequenceDefinitions.Where(static row => row.Root is not null)
            .Select(row =>
            {
                var definition = definitionBindings.Single(binding => binding.LocalRef == row.DefinitionRef);
                var application = applicationBindings.Single(binding => binding.LocalRef == row.DefinitionRef + "_application");
                return new MortalWoundTreatmentComplicationRootBinding(row.DefinitionRef, definition.NamespacedRef,
                    application.NamespacedRef, WoundResponseInputComposer.CreateLocalIdentifier(
                        "wound_root_operation", requestFingerprint, definition.NamespacedRef));
            }).ToImmutableArray();
        return MortalWoundTreatmentComplicationBindingPreparation.Create(complicationRef, complicationId,
            definitionBindings, applicationBindings, preparationFingerprint, roots);
    }

    private static MortalWoundHealOutcomeIntent ComposeHealIntent(
        MortalWoundTreatmentAttemptRequest request,
        int ordinal,
        string declaredFingerprint,
        string baseIntentFingerprint,
        MortalWoundHealOperation heal)
    {
        var child = MortalWoundHealChildCoordinates.Create(
            request.RequestFingerprint,
            ordinal);
        var seeds = heal.Legacies.Select((legacy, legacyOrdinal) =>
            MortalWoundTreatmentLegacySeedBinding.Create(
                request.RequestFingerprint,
                ordinal,
                legacyOrdinal,
                legacy)).ToArray();
        var preparationFields = new List<string?>
        {
            "book_of_eternity.mortal_wound_treatment.heal_preparation",
            "1",
            request.RequestFingerprint,
            ordinal.ToString(CultureInfo.InvariantCulture),
            child.OperationKey,
            child.TransitionId,
            child.EventRef,
            child.CausalEventRef,
            seeds.Length.ToString(CultureInfo.InvariantCulture)
        };
        preparationFields.AddRange(seeds.Select(static seed => seed.SeedFingerprint));
        var preparationFingerprint = WoundAcceptedTurnFingerprintWriter.Compute(
            preparationFields);
        var intentFingerprint = WoundAcceptedTurnFingerprintWriter.Compute(
            new string?[]
            {
                "book_of_eternity.mortal_wound_treatment.heal_intent",
                "1",
                baseIntentFingerprint,
                preparationFingerprint
            });
        return MortalWoundHealOutcomeIntent.Create(
            ordinal,
            declaredFingerprint,
            intentFingerprint,
            child,
            seeds,
            preparationFingerprint);
    }

    internal static string DeclaredFingerprint(
        int ordinal,
        MortalWoundTreatmentOperation operation) => WoundAcceptedTurnFingerprintWriter.Compute(
        OperationFields(
            "book_of_eternity.mortal_wound_treatment.declared_operation",
            ordinal,
            operation));

    internal static string DeteriorationIntentFingerprint(
        string requestFingerprint,
        int ordinal,
        MortalWoundApplyDeteriorationOperation operation,
        string declaredFingerprint,
        string authorityFingerprint) => WoundAcceptedTurnFingerprintWriter.Compute(
        new string?[]
        {
            "book_of_eternity.mortal_wound_treatment.deterioration_intent",
            "1",
            IntentFingerprint(
                requestFingerprint,
                ordinal,
                operation,
                declaredFingerprint),
            operation.PolicyRef,
            authorityFingerprint
        });

    private static string IntentFingerprint(
        string requestFingerprint,
        int ordinal,
        MortalWoundTreatmentOperation operation,
        string declaredFingerprint) => WoundAcceptedTurnFingerprintWriter.Compute(
        new string?[]
        {
            "book_of_eternity.mortal_wound_treatment.outcome_intent",
            "1",
            requestFingerprint,
            ordinal.ToString(CultureInfo.InvariantCulture),
            operation.Kind,
            declaredFingerprint
        }.Concat(OperationValueFields(operation)));

    private static IEnumerable<string?> OperationFields(
        string domain,
        int ordinal,
        MortalWoundTreatmentOperation operation) => new string?[]
        {
            domain,
            "1",
            ordinal.ToString(CultureInfo.InvariantCulture),
            operation.Kind
        }.Concat(OperationValueFields(operation));

    private static IEnumerable<string?> OperationValueFields(
        MortalWoundTreatmentOperation operation)
    {
        switch (operation)
        {
            case MortalWoundAddRecoveryOperation value:
                return new[] { value.Points.ToString(CultureInfo.InvariantCulture) };
            case MortalWoundReduceSeverityOperation value:
                return new[] { value.Steps.ToString(CultureInfo.InvariantCulture) };
            case MortalWoundRemoveComplicationOperation value:
                return new[] { value.ComplicationId };
            case MortalWoundApplyDeteriorationOperation value:
                return new[] { value.PolicyRef };
            case MortalWoundAddComplicationOperation value:
                return ComplicationValueFields(value);
            case MortalWoundHealOperation value:
                return HealValueFields(value);
            default:
                return Array.Empty<string?>();
        }
    }

    private static IEnumerable<string?> ComplicationValueFields(
        MortalWoundAddComplicationOperation operation)
    {
        var draft = operation.ComplicationDraft;
        var complication = draft.Complication;
        var fields = new List<string?>
        {
            complication.ComplicationRef,
            complication.Kind,
            complication.State,
            complication.DisplayName,
            complication.TreatmentDifficultyModifier.ToString(CultureInfo.InvariantCulture),
            complication.Visibility,
            draft.ConsequenceDefinitions.Length.ToString(CultureInfo.InvariantCulture)
        };
        foreach (var definition in draft.ConsequenceDefinitions)
        {
            fields.Add(definition.DefinitionRef);
            fields.Add(Canonical(definition.Definition));
            fields.Add(definition.Root?.OwnershipKind);
            fields.Add(definition.Root?.ComplicationRef);
            fields.Add(definition.Root?.Slots.Length.ToString(CultureInfo.InvariantCulture));
            if (definition.Root is not null)
            {
                foreach (var slot in definition.Root.Slots)
                {
                    fields.Add(slot.ProfileKey);
                    fields.Add(slot.ReadableSummary);
                }
            }
        }
        return fields;
    }

    private static IEnumerable<string?> HealValueFields(MortalWoundHealOperation operation)
    {
        var fields = new List<string?>
        {
            operation.Legacies.Length.ToString(CultureInfo.InvariantCulture)
        };
        foreach (var legacy in operation.Legacies)
        {
            fields.Add(legacy.LocalLegacyRef);
            fields.Add(legacy.Kind);
            fields.Add(legacy.ReadableSummary);
            if (legacy is not MortalWoundMechanicalEffectLegacyDraft mechanical)
                continue;
            fields.Add(mechanical.EffectDraft.SchemaVersion.ToString(
                CultureInfo.InvariantCulture));
            fields.Add(mechanical.EffectDraft.Definitions.Length.ToString(
                CultureInfo.InvariantCulture));
            foreach (var definition in mechanical.EffectDraft.Definitions)
            {
                fields.Add(definition.DefinitionRef);
                fields.Add(Canonical(definition.Definition));
            }
            fields.Add(mechanical.EffectDraft.Applications.Length.ToString(
                CultureInfo.InvariantCulture));
            foreach (var application in mechanical.EffectDraft.Applications)
            {
                fields.Add(application.ApplicationRef);
                fields.Add(application.DefinitionRef);
                fields.Add(Canonical(application.Parameters));
            }
        }
        return fields;
    }

    private static string? Canonical(System.Text.Json.JsonElement value) =>
        WoundAcceptedTurnFingerprintWriter.CanonicalJson(
            System.Text.Json.Nodes.JsonNode.Parse(value.GetRawText()));
}

internal sealed partial class MortalWoundNoImprovementOutcomeIntent
{
    internal static MortalWoundNoImprovementOutcomeIntent Create(
        int ordinal,
        string declaredFingerprint,
        string intentFingerprint) => new(
        ordinal,
        "no_improvement",
        declaredFingerprint,
        intentFingerprint);
}

internal sealed partial class MortalWoundStabilizeOutcomeIntent
{
    internal static MortalWoundStabilizeOutcomeIntent Create(
        int ordinal,
        string declaredFingerprint,
        string intentFingerprint) => new(
        ordinal,
        "stabilize",
        declaredFingerprint,
        intentFingerprint);
}

internal sealed partial class MortalWoundAddRecoveryOutcomeIntent
{
    internal static MortalWoundAddRecoveryOutcomeIntent Create(
        int ordinal,
        string declaredFingerprint,
        string intentFingerprint,
        int points) => new(
        ordinal,
        "add_recovery",
        declaredFingerprint,
        intentFingerprint,
        points);
}

internal sealed partial class MortalWoundReduceSeverityOutcomeIntent
{
    internal static MortalWoundReduceSeverityOutcomeIntent Create(
        int ordinal,
        string declaredFingerprint,
        string intentFingerprint,
        int steps) => new(
        ordinal,
        "reduce_severity",
        declaredFingerprint,
        intentFingerprint,
        steps);
}

internal sealed partial class MortalWoundRemoveComplicationOutcomeIntent
{
    internal static MortalWoundRemoveComplicationOutcomeIntent Create(
        int ordinal,
        string declaredFingerprint,
        string intentFingerprint,
        string complicationId) => new(
        ordinal,
        "remove_complication",
        declaredFingerprint,
        intentFingerprint,
        complicationId);
}

internal sealed partial class MortalWoundApplyDeteriorationOutcomeIntent
{
    internal static MortalWoundApplyDeteriorationOutcomeIntent Create(
        int ordinal,
        string declaredFingerprint,
        string intentFingerprint,
        string policyRef,
        string authorityFingerprint) => new(
        ordinal,
        "apply_deterioration",
        declaredFingerprint,
        intentFingerprint,
        policyRef,
        authorityFingerprint);
}

internal sealed partial class MortalWoundAddComplicationOutcomeIntent
{
    internal static MortalWoundAddComplicationOutcomeIntent Create(
        int ordinal,
        string declaredFingerprint,
        string intentFingerprint,
        string complicationRef,
        string complicationId,
        IEnumerable<MortalWoundTreatmentReferenceBinding> definitionBindings,
        IEnumerable<MortalWoundTreatmentReferenceBinding> applicationBindings,
        string preparationFingerprint) => new(
        ordinal,
        "add_complication",
        declaredFingerprint,
        intentFingerprint,
        complicationRef,
        complicationId,
        definitionBindings,
        applicationBindings,
        preparationFingerprint);
}

internal sealed partial class MortalWoundHealOutcomeIntent
{
    internal static MortalWoundHealOutcomeIntent Create(
        int ordinal,
        string declaredFingerprint,
        string intentFingerprint,
        MortalWoundHealChildCoordinates childCoordinates,
        IEnumerable<MortalWoundTreatmentLegacySeedBinding> legacySeeds,
        string preparationFingerprint) => new(
        ordinal,
        "heal",
        declaredFingerprint,
        intentFingerprint,
        childCoordinates,
        legacySeeds,
        preparationFingerprint);
}

internal sealed partial class MortalWoundHealChildCoordinates
{
    internal static MortalWoundHealChildCoordinates Create(
        string requestFingerprint,
        int operationOrdinal)
    {
        var digest = MortalWoundTreatmentIdentityWriter.Digest(
            "heal",
            requestFingerprint,
            operationOrdinal.ToString(CultureInfo.InvariantCulture));
        return new MortalWoundHealChildCoordinates(
            "mortal_wound_treatment_heal_operation_" + digest,
            "wound_transition_" + digest,
            "mortal_wound_treatment_heal_event_" + digest,
            "mortal_wound_treatment_heal_causal_" + digest);
    }
}

internal sealed partial class MortalWoundTreatmentLegacySeedBinding
{
    internal static MortalWoundTreatmentLegacySeedBinding Create(
        string requestFingerprint,
        int operationOrdinal,
        int legacyOrdinal,
        MortalWoundHealLegacyDraft legacy)
    {
        ArgumentNullException.ThrowIfNull(legacy);
        var legacyId = "mortal_wound_legacy_" + MortalWoundTreatmentIdentityWriter.Digest(
            "legacy",
            requestFingerprint,
            operationOrdinal.ToString(CultureInfo.InvariantCulture),
            legacyOrdinal.ToString(CultureInfo.InvariantCulture),
            legacy.LocalLegacyRef);
        var definitionBindings = legacy is MortalWoundMechanicalEffectLegacyDraft mechanical
            ? mechanical.EffectDraft.Definitions.Select(definition =>
                MortalWoundTreatmentReferenceBinding.Create(
                    definition.DefinitionRef,
                    legacy.LocalLegacyRef + "/" + definition.DefinitionRef)).ToArray()
            : Array.Empty<MortalWoundTreatmentReferenceBinding>();
        var applicationBindings = legacy is MortalWoundMechanicalEffectLegacyDraft mechanicalDraft
            ? mechanicalDraft.EffectDraft.Applications.Select(application =>
                MortalWoundTreatmentReferenceBinding.Create(
                    application.ApplicationRef,
                    legacy.LocalLegacyRef + "/" + application.ApplicationRef)).ToArray()
            : Array.Empty<MortalWoundTreatmentReferenceBinding>();
        var declaredFields = new List<string?>
        {
            "book_of_eternity.mortal_wound_treatment.declared_legacy",
            "1",
            legacyOrdinal.ToString(CultureInfo.InvariantCulture),
            legacy.LocalLegacyRef,
            legacy.Kind,
            legacy.ReadableSummary
        };
        if (legacy is MortalWoundMechanicalEffectLegacyDraft effectLegacy)
        {
            declaredFields.Add(effectLegacy.EffectDraft.SchemaVersion.ToString(
                CultureInfo.InvariantCulture));
            foreach (var definition in effectLegacy.EffectDraft.Definitions)
            {
                declaredFields.Add(definition.DefinitionRef);
                declaredFields.Add(Canonical(definition.Definition));
            }
            foreach (var application in effectLegacy.EffectDraft.Applications)
            {
                declaredFields.Add(application.ApplicationRef);
                declaredFields.Add(application.DefinitionRef);
                declaredFields.Add(Canonical(application.Parameters));
            }
        }
        var declaredFingerprint = WoundAcceptedTurnFingerprintWriter.Compute(declaredFields);
        var seedFields = new List<string?>
        {
            "book_of_eternity.mortal_wound_treatment.legacy_seed",
            "1",
            requestFingerprint,
            operationOrdinal.ToString(CultureInfo.InvariantCulture),
            legacyOrdinal.ToString(CultureInfo.InvariantCulture),
            legacyId,
            declaredFingerprint
        };
        seedFields.AddRange(definitionBindings.SelectMany(static binding =>
            new[] { binding.LocalRef, binding.NamespacedRef }));
        seedFields.AddRange(applicationBindings.SelectMany(static binding =>
            new[] { binding.LocalRef, binding.NamespacedRef }));
        var seedFingerprint = WoundAcceptedTurnFingerprintWriter.Compute(seedFields);
        return new MortalWoundTreatmentLegacySeedBinding(
            legacyOrdinal,
            legacy.LocalLegacyRef,
            legacyId,
            legacy.Kind,
            definitionBindings,
            applicationBindings,
            declaredFingerprint,
            seedFingerprint);
    }

    private static string? Canonical(System.Text.Json.JsonElement value) =>
        WoundAcceptedTurnFingerprintWriter.CanonicalJson(
            System.Text.Json.Nodes.JsonNode.Parse(value.GetRawText()));
}

internal sealed partial class MortalWoundTreatmentReferenceBinding
{
    internal static MortalWoundTreatmentReferenceBinding Create(
        string localRef,
        string namespacedRef) => new(localRef, namespacedRef);
}

internal static class MortalWoundTreatmentIdentityWriter
{
    internal static string Digest(string role, params string?[] fields) =>
        WoundAcceptedTurnFingerprintWriter.Compute(
            new string?[]
            {
                "book_of_eternity.mortal_wound_treatment.identity",
                "1",
                role
            }.Concat(fields))["sha256:".Length..];
}

internal static partial class MortalWoundTreatmentPlanner
{
    internal static MortalWoundTreatmentResolutionResult CreateProcedureAttempt(
        MortalWoundTreatmentAttemptRequest? request,
        WoundHistoryParseResult? history,
        WoundMaterializationEnvelope? before,
        MortalWoundTreatmentAcceptedStateAuthority? acceptedState)
    {
        if (TryResolveHistoricalAttempt(request, history, out var replay))
            return replay!;

        if (!TryResolveFreshRequest(
                "procedure",
                request,
                history,
                before,
                acceptedState,
                out var route,
                out var failure) ||
            route is not MortalWoundProcedureRouteDefinition procedureRoute ||
            request!.ModeAuthority is not MortalWoundProcedureCheckAuthority procedure)
        {
            return failure ?? RejectedResolution(
                "mortal_wound_treatment_procedure_request_invalid",
                "one current sealed procedure request",
                request?.Mode ?? "missing request");
        }

        long total;
        long margin;
        try
        {
            total = checked((long)procedure.NaturalRoll + procedure.Modifier);
            margin = checked(total - procedure.EffectiveDifficulty);
        }
        catch (OverflowException)
        {
            return RejectedResolution(
                "mortal_wound_treatment_procedure_arithmetic_overflow",
                "checked signed-64-bit procedure total and margin",
                "overflow");
        }

        var reactionResult = EffectAcceptedEventReportCatalog
            .ResolvePreparedMortalWoundCriticalReaction(request, acceptedState!);
        if (!reactionResult.IsValid)
            return MortalWoundTreatmentResolutionResult.Rejected(reactionResult.Issues);
        var originalOutcome = procedure.NaturalRoll switch
        {
            20 => "critical_success",
            1 => "critical_failure",
            _ => "ordinary"
        };
        var resolvedOutcome = procedure.NaturalRoll == 1 && reactionResult.Intent is not null
            ? "failure"
            : originalOutcome;
        var selectedIndex = SelectProcedureBand(
            procedureRoute,
            procedure.NaturalRoll,
            margin,
            reactionResult.Intent is not null);
        if (selectedIndex < 0)
        {
            return RejectedResolution(
                "mortal_wound_treatment_procedure_band_unresolved",
                "one exact selected procedure result band",
                margin.ToString(CultureInfo.InvariantCulture));
        }

        var selectedBand = procedureRoute.Bands[selectedIndex];
        if (!MortalWoundTreatmentOutcomeIntentComposer.TryCompose(
                request,
                selectedBand.DeclaredResult,
                acceptedState!,
                out var intents,
                out var intentIssues))
        {
            return MortalWoundTreatmentResolutionResult.Rejected(intentIssues);
        }
        var evidence = MortalWoundProcedureModeEvidence.Create(
            procedure,
            procedureRoute,
            total,
            margin,
            originalOutcome,
            resolvedOutcome,
            selectedBand,
            selectedIndex,
            reactionResult.Intent);
        var consumptionTrigger = DeriveConsumptionTrigger(procedureRoute, selectedBand.Category, false);
        var routeCompletion = DeriveRouteCompletion(before!, procedureRoute, selectedBand.Category,
            false, null);
        return MortalWoundTreatmentResolutionResult.Resolved(
            MortalWoundTreatmentResolution.Create(
                "procedure",
                request.Coordinates,
                "AcceptedTerminal",
                selectedBand.Category,
                selectedIndex,
                interruption: false,
                selectedBand.DeclaredResult,
                intents,
                reactionResult.Intent,
                consumptionTrigger,
                courseId: null,
                courseMilestoneOrdinal: null,
                courseDisposition: null,
                request,
                evidence,
                request.RequirementAuthority.RouteFingerprint,
                routeCompletion));
    }

    internal static MortalWoundTreatmentResolutionResult CreateGuaranteedAttempt(
        MortalWoundTreatmentAttemptRequest? request,
        WoundHistoryParseResult? history,
        WoundMaterializationEnvelope? before,
        MortalWoundTreatmentAcceptedStateAuthority? acceptedState)
    {
        if (TryResolveHistoricalAttempt(request, history, out var replay))
            return replay!;

        if (!TryResolveFreshRequest(
                "guaranteed",
                request,
                history,
                before,
                acceptedState,
                out var route,
                out var failure) ||
            route is not MortalWoundGuaranteedRouteDefinition guaranteedRoute ||
            request!.ModeAuthority is not MortalWoundTreatmentCapabilityProof proof)
        {
            return failure ?? RejectedResolution(
                "mortal_wound_treatment_guaranteed_request_invalid",
                "one current sealed guaranteed request",
                request?.Mode ?? "missing request");
        }

        if (!MortalWoundTreatmentOutcomeIntentComposer.TryCompose(
                request,
                guaranteedRoute.Outcome.DeclaredResult,
                acceptedState!,
                out var intents,
                out var intentIssues))
        {
            return MortalWoundTreatmentResolutionResult.Rejected(intentIssues);
        }

        var evidence = MortalWoundGuaranteedModeEvidence.Create(proof, guaranteedRoute);
        var consumptionTrigger = DeriveConsumptionTrigger(guaranteedRoute, guaranteedRoute.Outcome.Category, false);
        var routeCompletion = DeriveRouteCompletion(before!, guaranteedRoute, guaranteedRoute.Outcome.Category,
            false, null);
        return MortalWoundTreatmentResolutionResult.Resolved(
            MortalWoundTreatmentResolution.Create(
                "guaranteed",
                request.Coordinates,
                "AcceptedTerminal",
                guaranteedRoute.Outcome.Category,
                selectedOutcomeIndex: 0,
                interruption: false,
                guaranteedRoute.Outcome.DeclaredResult,
                intents,
                criticalReactionIntent: null,
                consumptionTrigger,
                courseId: null,
                courseMilestoneOrdinal: null,
                courseDisposition: null,
                request,
                evidence,
                request.RequirementAuthority.RouteFingerprint,
                routeCompletion));
    }

    internal static MortalWoundTreatmentResolutionResult CreateCourseMilestoneAttempt(
        MortalWoundTreatmentAttemptRequest? request,
        WoundHistoryParseResult? history,
        WoundMaterializationEnvelope? before,
        MortalWoundTreatmentAcceptedStateAuthority? acceptedState)
    {
        if (TryResolveHistoricalAttempt(request, history, out var replay))
            return replay!;

        if (!TryResolveFreshRequest(
                "course",
                request,
                history,
                before,
                acceptedState,
                out var route,
                out var failure) ||
            route is not MortalWoundCourseRouteDefinition courseRoute ||
            request!.ModeAuthority is not MortalWoundCourseModeAuthority course ||
            request.MilestoneOrdinal is not { } milestoneOrdinal ||
            milestoneOrdinal != course.MilestoneOrdinal ||
            milestoneOrdinal < 1 ||
            milestoneOrdinal > courseRoute.Milestones.Length)
        {
            return failure ?? RejectedResolution(
                "mortal_wound_treatment_course_request_invalid",
                "one current sealed course milestone request",
                request?.Mode ?? "missing request");
        }

        var interruptionReason = request.RequirementAuthority.InterruptionReason;
        var interrupted = interruptionReason is not null;
        var selectedOutcomeIndex = interrupted ? (int?)null : milestoneOrdinal - 1;
        var declaredResult = interrupted
            ? courseRoute.Interruption.DeclaredResult
            : courseRoute.Milestones[milestoneOrdinal - 1].DeclaredResult;
        var resultCategory = interrupted
            ? courseRoute.Interruption.Category
            : courseRoute.Milestones[milestoneOrdinal - 1].Category;
        var courseDisposition = interrupted
            ? "interrupted"
            : courseRoute.Milestones[milestoneOrdinal - 1].Completion;

        if (!MortalWoundTreatmentOutcomeIntentComposer.TryCompose(
                request,
                declaredResult,
                acceptedState!,
                out var intents,
                out var intentIssues))
        {
            return MortalWoundTreatmentResolutionResult.Rejected(intentIssues);
        }

        var evidence = MortalWoundCourseModeEvidence.Create(course, courseDisposition);
        var consumptionTrigger = DeriveConsumptionTrigger(courseRoute, resultCategory, interrupted);
        var routeCompletion = DeriveRouteCompletion(before!, courseRoute, resultCategory,
            interrupted, courseDisposition);
        return MortalWoundTreatmentResolutionResult.Resolved(
            MortalWoundTreatmentResolution.Create(
                "course",
                request.Coordinates,
                "AcceptedTerminal",
                resultCategory,
                selectedOutcomeIndex,
                interrupted,
                declaredResult,
                intents,
                criticalReactionIntent: null,
                consumptionTrigger,
                course.CourseId,
                milestoneOrdinal,
                courseDisposition,
                request,
                evidence,
                request.RequirementAuthority.RouteFingerprint,
                routeCompletion));
    }

    internal static string DeriveConsumptionTrigger(
        MortalWoundTreatmentRouteDefinition route, string category, bool interruption) =>
        !interruption && route.ResourcePolicy.ConsumeOn.Contains(category, StringComparer.Ordinal)
            ? category : "none";

    internal static string DeriveRouteCompletion(
        WoundMaterializationEnvelope before, MortalWoundTreatmentRouteDefinition route,
        string category, bool interruption, string? courseDisposition) =>
        (route.Mode == "course"
            ? !interruption && courseDisposition == "completed"
            : category == "success") &&
        !before.Treatment.CompletedRouteIds.Contains(route.RouteId, StringComparer.Ordinal)
            ? "AppendOnce" : "None";

    internal static int SelectProcedureBand(
        MortalWoundProcedureRouteDefinition route,
        int naturalRoll,
        long margin,
        bool reacted)
    {
        if (naturalRoll == 20)
            return 0;
        if (naturalRoll == 1)
        {
            if (!reacted)
                return route.Bands.Length - 1;
            for (var index = 0; index < route.Bands.Length; index++)
            {
                if (string.Equals(
                        route.Bands[index].Category,
                        "failed_attempt",
                        StringComparison.Ordinal))
                {
                    return index;
                }
            }
            return -1;
        }

        for (var index = 0; index < route.Bands.Length; index++)
        {
            var band = route.Bands[index];
            if ((!band.MinimumMargin.HasValue || margin >= band.MinimumMargin.Value) &&
                (!band.MaximumMargin.HasValue || margin <= band.MaximumMargin.Value))
            {
                return index;
            }
        }
        return -1;
    }

    private static bool TryResolveHistoricalAttempt(
        MortalWoundTreatmentAttemptRequest? request,
        WoundHistoryParseResult? history,
        out MortalWoundTreatmentResolutionResult? result)
    {
        result = null;
        if (history is null)
            return false;
        if (!history.IsValid)
        {
            result = MortalWoundTreatmentResolutionResult.Rejected(history.Issues);
            return true;
        }
        if (request is null)
            return false;

        var probe = history.ProbeTreatmentAttempt(
            request.Coordinates.OperationKey,
            request.Coordinates.AttemptId,
            request.RequestFingerprint);
        switch (probe.Status)
        {
            case "NotFound":
                return false;
            case "ExactReplay" when probe.Receipt is not null:
                result = MortalWoundTreatmentResolutionResult.ExactReplay(probe.Receipt);
                return true;
            case "InvalidHistory":
                result = MortalWoundTreatmentResolutionResult.Rejected(probe.Issues);
                return true;
            case "Conflict":
                result = MortalWoundTreatmentResolutionResult.Conflict(probe.Issues);
                return true;
            default:
                result = RejectedResolution(
                    "mortal_wound_treatment_replay_probe_invalid",
                    "one closed treatment replay classification",
                    probe.Status);
                return true;
        }
    }

    private static bool TryResolveFreshRequest(
        string mode,
        MortalWoundTreatmentAttemptRequest? request,
        WoundHistoryParseResult? history,
        WoundMaterializationEnvelope? before,
        MortalWoundTreatmentAcceptedStateAuthority? acceptedState,
        out MortalWoundTreatmentRouteDefinition? route,
        out MortalWoundTreatmentResolutionResult? failure)
    {
        route = null;
        failure = null;
        var invalidAuthorities = new List<string>();
        if (request is null) invalidAuthorities.Add("request");
        if (history is null) invalidAuthorities.Add("history");
        if (before is null) invalidAuthorities.Add("wound");
        if (acceptedState is null) invalidAuthorities.Add("accepted_state");
        if (request is not null && !request.HasMatchingFingerprint())
            invalidAuthorities.Add("request_seal");
        if (request is not null &&
            !string.Equals(request.Mode, mode, StringComparison.Ordinal))
            invalidAuthorities.Add("request_mode");
        if (request is not null &&
            !request.Coordinates.MatchesAcceptedState(acceptedState))
            invalidAuthorities.Add("coordinates");
        if (acceptedState is not null &&
            !acceptedState.MatchesCurrentWound(before))
            invalidAuthorities.Add("current_wound");
        if (acceptedState is not null &&
            !acceptedState.MatchesCompleteHistory(history))
            invalidAuthorities.Add("complete_history");
        if (invalidAuthorities.Count != 0)
        {
            failure = RejectedResolution(
                "mortal_wound_treatment_resolution_authority_invalid",
                "one current accepted state and its exact sealed request/wound/history",
                string.Join(',', invalidAuthorities));
            return false;
        }

        ArgumentNullException.ThrowIfNull(request);
        ArgumentNullException.ThrowIfNull(history);
        ArgumentNullException.ThrowIfNull(before);
        ArgumentNullException.ThrowIfNull(acceptedState);

        var routeMatches = acceptedState.TreatmentDefinition.Routes.Where(candidate =>
                string.Equals(
                    candidate.RouteId,
                    request.Coordinates.RouteId,
                    StringComparison.Ordinal) &&
                string.Equals(candidate.Mode, mode, StringComparison.Ordinal))
            .ToArray();
        if (routeMatches.Length != 1)
        {
            failure = RejectedResolution(
                "mortal_wound_treatment_resolution_route_invalid",
                $"one exact current {mode} route",
                request.Coordinates.RouteId);
            return false;
        }
        string routeFingerprint;
        try
        {
            routeFingerprint = MortalWoundTreatmentRouteFingerprint.Compute(
                before,
                request.Coordinates.RouteId);
        }
        catch (Exception exception) when (exception is ArgumentException or
                                           InvalidOperationException or
                                           System.Text.Json.JsonException)
        {
            failure = RejectedResolution(
                "mortal_wound_treatment_resolution_route_invalid",
                "one recomputable current route fingerprint",
                exception.GetType().Name);
            return false;
        }
        if (!string.Equals(
                routeFingerprint,
                request.RequirementAuthority.RouteFingerprint,
                StringComparison.Ordinal) ||
            !string.Equals(
                routeFingerprint,
                request.ResourceAuthority.RouteFingerprint,
                StringComparison.Ordinal))
        {
            failure = RejectedResolution(
                "mortal_wound_treatment_resolution_route_seal_mismatch",
                "the exact route sealed by the complete request",
                routeFingerprint);
            return false;
        }
        route = routeMatches[0];
        var freshMismatch = MortalWoundTreatmentFreshAuthorityValidator.FindMismatch(
            request,
            history,
            before,
            acceptedState,
            route);
        if (freshMismatch is not null)
        {
            failure = RejectedResolution(
                "mortal_wound_treatment_resolution_fresh_authority_mismatch",
                "the exact request authorities independently recreated from current accepted state",
                freshMismatch);
            route = null;
            return false;
        }
        return true;
    }

    private static MortalWoundTreatmentResolutionResult RejectedResolution(
        string code,
        string expected,
        string actual) => MortalWoundTreatmentResolutionResult.Rejected(
        new[]
        {
            new ValidationIssue(
                "treatmentAttempt.resolution",
                IssueSeverity.Error,
                "The Mortal wound-treatment resolution cannot be trusted.",
                code: code,
                actor: "Client",
                section: "wound_materialization",
                expected: expected,
                actual: actual,
                repairHint:
                "Restore the exact sealed request or recreate a fresh attempt from current canonical authority.")
        });
}
