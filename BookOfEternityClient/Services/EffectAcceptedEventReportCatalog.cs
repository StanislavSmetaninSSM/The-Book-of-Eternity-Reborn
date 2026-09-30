using System.Text.Json.Nodes;

namespace BookOfEternityClient.Services;

internal sealed record EffectAcceptedEventReportResult(
    IReadOnlyList<JsonObject> LifecycleEvents,
    IReadOnlyList<ValidationIssue> Issues)
{
    internal bool IsValid => Issues.Count == 0;
}

/// <summary>
/// Converts closed GM event evidence into client-derived lifecycle authority.
/// The GM may report an event and its audited outcome, but never selects an
/// effect identity, trigger identity, or lifecycle transition.
/// </summary>
internal static class EffectAcceptedEventReportCatalog
{
    internal const string ResponseField = "effectEventReports";

    private const int MaxReportsPerTurn = 16;
    private const int MaxReasonLength = 2_048;

    private static readonly HashSet<string> ReportFields = Set(
        "eventType", "target", "evidence", "reason");
    private static readonly HashSet<string> TargetFields = Set(
        "kind", "targetId");
    private static readonly HashSet<string> MortalRollEvidenceFields = Set(
        "kind", "rollMode", "diceIndexes", "selectedIndex", "selectedValue",
        "originalOutcome", "resolvedOutcome");
    private static readonly HashSet<string> RollModes = Set(
        "normal", "advantage", "great_advantage", "disadvantage",
        "dire_disadvantage");

    internal static EffectAcceptedEventReportResult Compose(
        JsonNode? reportsNode,
        int turn,
        string realm,
        IReadOnlyList<int> authoritativeDice,
        EffectCarrierCatalogInput carriers)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(realm);
        ArgumentNullException.ThrowIfNull(authoritativeDice);
        ArgumentNullException.ThrowIfNull(carriers);

        var issues = new List<ValidationIssue>();
        var events = new List<JsonObject>();
        if (reportsNode == null)
            return new EffectAcceptedEventReportResult(events, issues);
        if (reportsNode is not JsonArray reports)
        {
            Add(
                issues,
                ResponseField,
                "effect_event_reports_invalid_root",
                "effectEventReports array",
                Describe(reportsNode));
            return new EffectAcceptedEventReportResult(events, issues);
        }
        if (turn <= 0)
        {
            Add(
                issues,
                ResponseField,
                "effect_event_report_turn_invalid",
                "positive accepted turn",
                turn.ToString(System.Globalization.CultureInfo.InvariantCulture));
        }
        if (reports.Count > MaxReportsPerTurn)
        {
            Add(
                issues,
                ResponseField,
                "effect_event_report_limit_exceeded",
                $"at most {MaxReportsPerTurn} reports",
                reports.Count.ToString(System.Globalization.CultureInfo.InvariantCulture));
        }
        if (issues.Count != 0)
            return new EffectAcceptedEventReportResult(events, issues);

        var catalog = EffectCarrierCatalog.Build(carriers);
        issues.AddRange(catalog.Issues);
        var fateCandidates = FateShieldReactionArbiter.ProjectEligibleCandidates(
            catalog.Occurrences);
        var seenEvents = new HashSet<string>(StringComparer.Ordinal);
        var reservedEffectIds = new HashSet<string>(StringComparer.Ordinal);
        for (var index = 0; index < reports.Count; index++)
        {
            var path = $"{ResponseField}[{index}]";
            if (reports[index] is not JsonObject report)
            {
                Add(
                    issues,
                    path,
                    "effect_event_report_invalid",
                    "closed event report object",
                    Describe(reports[index]));
                continue;
            }
            ValidateClosed(report, ReportFields, path, issues);
            if (!TryExact(report["eventType"], out var eventType) ||
                !string.Equals(
                    eventType,
                    "owner_critical_failure",
                    StringComparison.Ordinal))
            {
                Add(
                    issues,
                    path + ".eventType",
                    "effect_event_report_type_unsupported",
                    "owner_critical_failure",
                    Describe(report["eventType"]));
                continue;
            }
            if (!seenEvents.Add(eventType))
            {
                Add(
                    issues,
                    path + ".eventType",
                    "effect_event_report_duplicate",
                    "one owner_critical_failure report per accepted Mortal action",
                    eventType);
                continue;
            }
            if (!string.Equals(realm, "mortal_world", StringComparison.Ordinal))
            {
                Add(
                    issues,
                    path,
                    "effect_event_report_realm_invalid",
                    "mortal_world accepted realm",
                    realm);
                continue;
            }
            if (!ValidatePlayerTarget(report["target"], path + ".target", issues) ||
                !ValidateCriticalFailureEvidence(
                    report["evidence"],
                    path + ".evidence",
                    authoritativeDice,
                    issues) ||
                !TryReadable(report["reason"], out var reason) ||
                reason.Length > MaxReasonLength)
            {
                if (!TryReadable(report["reason"], out reason) ||
                    reason.Length > MaxReasonLength)
                {
                    Add(
                        issues,
                        path + ".reason",
                        "effect_event_report_reason_invalid",
                        $"trimmed non-empty reason up to {MaxReasonLength} characters",
                        Describe(report["reason"]));
                }
                continue;
            }

            var selected = FateShieldReactionArbiter.SelectOldest(
                fateCandidates,
                reservedEffectIds);
            if (selected is null)
            {
                Add(
                    issues,
                    path,
                    "effect_event_report_reaction_unresolved",
                    "one active source-authorized Fate Shield reaction",
                    "no eligible effect");
                continue;
            }

            reservedEffectIds.Add(selected.EffectId);
            events.Add(new JsonObject
            {
                ["eventRef"] = $"turn_{turn}:reported:{eventType}:{index + 1}",
                ["causalEventRef"] = $"turn_{turn}:mortal_action_roll:1",
                ["turn"] = turn,
                ["phase"] = eventType,
                ["realm"] = realm,
                ["target"] = new JsonObject
                {
                    ["kind"] = "player",
                    ["targetId"] = "player_current"
                },
                ["effectId"] = selected.EffectId,
                ["triggerId"] = selected.TriggerId,
                ["currentTime"] = null,
                ["currentSceneId"] = null,
                ["sceneClosed"] = false,
                ["sourceSatisfied"] = null,
                ["conditionSatisfied"] = null,
                ["targetSatisfied"] = true,
                ["currentRealm"] = realm
            });
        }

        return issues.Count == 0
            ? new EffectAcceptedEventReportResult(events, Array.Empty<ValidationIssue>())
            : new EffectAcceptedEventReportResult(
                Array.Empty<JsonObject>(),
                issues.ToArray());
    }

    internal static MortalWoundCriticalReactionResolutionResult
        ResolvePreparedMortalWoundCriticalReaction(
            MortalWoundTreatmentAttemptRequest request,
            MortalWoundTreatmentAcceptedStateAuthority acceptedState)
    {
        if (request is null ||
            acceptedState is null ||
            !request.HasMatchingFingerprint() ||
            !string.Equals(request.Mode, "procedure", StringComparison.Ordinal) ||
            request.MilestoneOrdinal is not null ||
            request.ModeAuthority is not MortalWoundProcedureCheckAuthority procedure ||
            !request.Coordinates.MatchesAcceptedState(acceptedState) ||
            !string.Equals(
                procedure.CoordinatesFingerprint,
                request.Coordinates.CoordinatesFingerprint,
                StringComparison.Ordinal) ||
            !string.Equals(
                procedure.AcceptedStateFingerprint,
                request.Coordinates.AcceptedStateFingerprint,
                StringComparison.Ordinal) ||
            !string.Equals(
                procedure.RequirementAuthorityFingerprint,
                request.RequirementAuthority.AuthorityFingerprint,
                StringComparison.Ordinal))
        {
            return MortalWoundCriticalReactionResolutionResult.Invalid(new ValidationIssue(
                LiveTurnPreparationService.TurnRequestPath,
                IssueSeverity.Error,
                "The prepared Mortal wound critical reaction cannot be trusted.",
                code: "mortal_wound_treatment_critical_reaction_authority_invalid",
                actor: "Client",
                section: "wound_materialization",
                expected: "one sealed current Mortal wound treatment request and accepted state",
                actual: request is null || acceptedState is null
                    ? "missing input"
                    : "foreign, stale, or mismatched request"));
        }

        var prepared = procedure.PreparedCriticalReaction;
        if (prepared is null)
            return MortalWoundCriticalReactionResolutionResult.Valid(null);
        if (procedure.NaturalRoll != 1 ||
            !string.Equals(procedure.RollActorKind, "player", StringComparison.Ordinal) ||
            !string.Equals(procedure.RollActorId, "player_current", StringComparison.Ordinal))
        {
            return MortalWoundCriticalReactionResolutionResult.Invalid(new ValidationIssue(
                LiveTurnPreparationService.TurnRequestPath,
                IssueSeverity.Error,
                "The prepared Mortal wound critical reaction cannot be trusted.",
                code: "mortal_wound_treatment_critical_reaction_candidate_invalid",
                actor: "Client",
                section: "wound_materialization",
                expected: "one player-owned prepared reaction for a sealed natural 1",
                actual: $"{procedure.RollActorKind}/{procedure.RollActorId}/{procedure.NaturalRoll}"));
        }

        return MortalWoundCriticalReactionResolutionResult.Valid(
            MortalWoundCriticalReactionIntent.Create(request, prepared));
    }

    private static bool ValidatePlayerTarget(
        JsonNode? node,
        string path,
        List<ValidationIssue> issues)
    {
        if (node is not JsonObject target)
        {
            Add(
                issues,
                path,
                "effect_event_report_target_invalid",
                "exact player/player_current target",
                Describe(node));
            return false;
        }
        ValidateClosed(target, TargetFields, path, issues);
        var valid = HasExact(target, "kind", "player") &&
            HasExact(target, "targetId", "player_current");
        if (!valid)
        {
            Add(
                issues,
                path,
                "effect_event_report_target_invalid",
                "exact player/player_current target",
                target.ToJsonString());
        }
        return valid;
    }

    private static bool ValidateCriticalFailureEvidence(
        JsonNode? node,
        string path,
        IReadOnlyList<int> authoritativeDice,
        List<ValidationIssue> issues)
    {
        if (node is not JsonObject evidence)
        {
            Add(
                issues,
                path,
                "effect_event_report_evidence_invalid",
                "closed mortal_action_roll evidence",
                Describe(node));
            return false;
        }
        ValidateClosed(evidence, MortalRollEvidenceFields, path, issues);
        if (!HasExact(evidence, "kind", "mortal_action_roll") ||
            !TryExact(evidence["rollMode"], out var rollMode) ||
            !RollModes.Contains(rollMode) ||
            !HasExact(evidence, "originalOutcome", "critical_failure") ||
            !HasExact(evidence, "resolvedOutcome", "failure"))
        {
            Add(
                issues,
                path,
                "effect_event_report_outcome_invalid",
                "mortal_action_roll proving critical_failure -> failure",
                evidence.ToJsonString());
            return false;
        }

        var expectedCount = rollMode switch
        {
            "normal" => 1,
            "advantage" or "disadvantage" => 2,
            "great_advantage" or "dire_disadvantage" => 3,
            _ => 0
        };
        if (evidence["diceIndexes"] is not JsonArray indexes ||
            indexes.Count != expectedCount ||
            !TryReadIndexes(indexes, expectedCount, authoritativeDice.Count, out var parsed) ||
            !TryReadInt(evidence["selectedIndex"], out var selectedIndex) ||
            !TryReadInt(evidence["selectedValue"], out var selectedValue))
        {
            Add(
                issues,
                path,
                "effect_event_report_dice_invalid",
                "exact leading sealed dice indexes and selected value for rollMode",
                evidence.ToJsonString());
            return false;
        }

        var expectedIndex = rollMode switch
        {
            "advantage" or "great_advantage" => parsed
                .OrderByDescending(index => authoritativeDice[index])
                .ThenBy(static index => index)
                .First(),
            "disadvantage" or "dire_disadvantage" => parsed
                .OrderBy(index => authoritativeDice[index])
                .ThenBy(static index => index)
                .First(),
            _ => parsed[0]
        };
        var valid = selectedIndex == expectedIndex &&
            selectedValue == authoritativeDice[expectedIndex] &&
            selectedValue == 1;
        if (!valid)
        {
            Add(
                issues,
                path,
                "effect_event_report_dice_not_authorized",
                $"selectedIndex={expectedIndex};selectedValue=1 from sealed dice",
                $"selectedIndex={selectedIndex};selectedValue={selectedValue}");
        }
        return valid;
    }

    private static bool TryReadIndexes(
        JsonArray values,
        int count,
        int diceCount,
        out int[] indexes)
    {
        indexes = Array.Empty<int>();
        var parsed = new int[count];
        for (var index = 0; index < count; index++)
        {
            if (!TryReadInt(values[index], out var value) ||
                value != index ||
                value < 0 ||
                value >= diceCount)
            {
                return false;
            }
            parsed[index] = value;
        }
        indexes = parsed;
        return true;
    }

    private static bool HasExact(
        JsonObject value,
        string field,
        string expected) =>
        TryExact(value[field], out var actual) &&
        string.Equals(actual, expected, StringComparison.Ordinal);

    private static bool TryExact(JsonNode? node, out string value)
    {
        value = string.Empty;
        if (node is not JsonValue scalar ||
            !scalar.TryGetValue<string>(out var parsed) ||
            parsed == null)
        {
            return false;
        }
        value = parsed;
        return value.Length != 0 &&
            string.Equals(value, value.Trim(), StringComparison.Ordinal);
    }

    private static bool TryReadable(JsonNode? node, out string value)
    {
        value = string.Empty;
        if (node is not JsonValue scalar ||
            !scalar.TryGetValue<string>(out var parsed) ||
            parsed == null)
        {
            return false;
        }
        value = parsed;
        return !string.IsNullOrWhiteSpace(value) &&
            string.Equals(value, value.Trim(), StringComparison.Ordinal);
    }

    private static bool TryReadInt(JsonNode? node, out int value)
    {
        value = 0;
        return node is JsonValue scalar && scalar.TryGetValue<int>(out value);
    }

    private static void ValidateClosed(
        JsonObject value,
        IReadOnlySet<string> allowed,
        string path,
        List<ValidationIssue> issues)
    {
        foreach (var property in value)
        {
            if (!allowed.Contains(property.Key))
            {
                Add(
                    issues,
                    path + "." + property.Key,
                    "effect_event_report_unknown_field",
                    "registered event report field",
                    property.Key);
            }
        }
        foreach (var field in allowed)
        {
            if (!value.ContainsKey(field))
            {
                Add(
                    issues,
                    path + "." + field,
                    "effect_event_report_missing_field",
                    "required event report field",
                    "missing");
            }
        }
    }

    private static string Describe(JsonNode? node) =>
        node?.ToJsonString() ?? "missing";

    private static HashSet<string> Set(params string[] values) =>
        new(values, StringComparer.Ordinal);

    private static void Add(
        List<ValidationIssue> issues,
        string path,
        string code,
        string expected,
        string actual) =>
        issues.Add(new ValidationIssue(
            path,
            IssueSeverity.Error,
            "Effect event report cannot become accepted lifecycle authority.",
            code: code,
            section: "effect_materialization",
            expected: expected,
            actual: actual,
            repairHint: "Report one closed event with exact sealed dice evidence and the source-owned resolved outcome; never submit effectId, triggerId, or lifecycle post-state."));
}
