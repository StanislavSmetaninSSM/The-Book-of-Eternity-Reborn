using System.Text.Json;
using System.Text.Json.Nodes;
using BookOfEternityClient.UI;

namespace BookOfEternityClient.Services;

internal sealed class WoundResponseInputCompositionResult
{
    private readonly JsonObject? _commandRoot;
    private readonly WoundOpportunityAuthority[] _materializedOpportunities;
    private readonly WoundAcceptedTransitionDraft[] _transitions;
    private readonly WoundPlayerNotification[] _notifications;
    private readonly WoundOpportunityDecisionReceipt[] _decisionReceipts;
    private readonly ValidationIssue[] _issues;

    internal WoundResponseInputCompositionResult(
        JsonObject? commandRoot,
        IReadOnlyList<WoundOpportunityAuthority> materializedOpportunities,
        IReadOnlyList<WoundAcceptedTransitionDraft> transitions,
        IReadOnlyList<WoundPlayerNotification> notifications,
        IReadOnlyList<WoundOpportunityDecisionReceipt> decisionReceipts,
        IReadOnlyList<ValidationIssue> issues)
    {
        _commandRoot = commandRoot?.DeepClone().AsObject();
        _materializedOpportunities = materializedOpportunities
            .Select(WoundAcceptedTurnData.CloneOpportunity)
            .ToArray();
        _transitions = transitions
            .Select(WoundAcceptedTurnData.CloneTransitionDraft)
            .ToArray();
        _notifications = notifications.Select(static value => value with
        {
            Text = value.Text with { }
        }).ToArray();
        _decisionReceipts = decisionReceipts.Select(static value => value with { })
            .ToArray();
        _issues = issues.Select(CloneIssue).ToArray();
    }

    internal JsonObject? CommandRoot => _commandRoot?.DeepClone().AsObject();

    internal IReadOnlyList<WoundOpportunityAuthority> MaterializedOpportunities =>
        Array.AsReadOnly(_materializedOpportunities
            .Select(WoundAcceptedTurnData.CloneOpportunity)
            .ToArray());

    internal IReadOnlyList<WoundAcceptedTransitionDraft> Transitions =>
        Array.AsReadOnly(_transitions
            .Select(WoundAcceptedTurnData.CloneTransitionDraft)
            .ToArray());

    internal IReadOnlyList<WoundPlayerNotification> Notifications =>
        Array.AsReadOnly(_notifications.Select(static value => value with
        {
            Text = value.Text with { }
        }).ToArray());

    internal IReadOnlyList<WoundOpportunityDecisionReceipt> DecisionReceipts =>
        Array.AsReadOnly(_decisionReceipts.Select(static value => value with { }).ToArray());

    internal IReadOnlyList<ValidationIssue> Issues =>
        Array.AsReadOnly(_issues.Select(CloneIssue).ToArray());

    internal bool Success => _commandRoot is not null && _issues.Length == 0;

    private static ValidationIssue CloneIssue(ValidationIssue issue) =>
        WoundAcceptedTurnData.CloneIssue(issue);
}

internal static partial class WoundResponseInputComposer
{
    private const int MaximumDecisions = 32;

    private sealed record ParsedDecision(
        int Index,
        JsonObject Raw,
        string OpportunityRef,
        string Decision,
        string? LocalWoundRef,
        JsonElement? Proposal,
        int? SeverityRank);

    private sealed record AcceptedDecision(
        WoundOpportunityAuthority Opportunity,
        ParsedDecision Response,
        WoundOpportunityDecisionAuthority Authority,
        bool AlreadyConsumed,
        WoundAcceptedTransitionDraft? Transition,
        WoundPlayerNotification? Notification);

    internal static IReadOnlyList<ValidationIssue> ValidateSkillScopes(
        WoundAcceptedTurnBinding binding,
        IReadOnlyList<WoundResponseCommandDraft> commands,
        IReadOnlyList<WoundAcceptedTransitionDraft> transitions,
        EffectRollSkillScopeAuthority? authority,
        out EffectApplicationDiagnosticLocations? locations)
    {
        locations = null;
        var issues = new List<ValidationIssue>();
        var rows = new Dictionary<EffectSourceKey, EffectApplicationDiagnosticLocation>();
        var decisions = commands.Select((command, index) => (
            Index: index,
            Decision: command.Decision.ValueKind == JsonValueKind.Object
                ? JsonNode.Parse(command.Decision.GetRawText()) as JsonObject : null)).ToArray();
        foreach (var transition in transitions)
        {
            var matches = decisions.Where(row =>
                row.Decision?["decision"]?.GetValue<string>() == "materialize" &&
                row.Decision?["woundRef"]?.GetValue<string>() == transition.LocalWoundRef).ToArray();
            if (matches.Length != 1 ||
                !WoundEffectCarrierAdapter.TryCreateTargetKey(transition.ProposedAfter.Owner, out var target))
            {
                InvalidLocation("woundDecisions");
                continue;
            }
            var (decisionIndex, decision) = matches[0];
            var prefix = $"woundDecisions[{decisionIndex}].proposal.consequenceDefinitions";
            if (decision?["proposal"]?["consequenceDefinitions"] is not JsonArray rawDefinitions)
            {
                InvalidLocation(prefix);
                continue;
            }
            foreach (var draft in transition.EffectDefinitions)
            {
                var definition = draft.Definition;
                var definitionKey = definition["definitionKey"]?.GetValue<string>();
                var definitions = rawDefinitions.Select((node, index) => (Node: node, Index: index))
                    .Where(row => row.Node?["definitionRef"]?.GetValue<string>() == draft.LocalEffectRef &&
                        row.Node?["definition"]?["definitionKey"]?.GetValue<string>() == definitionKey).ToArray();
                if (definitions.Length != 1 || definitionKey is null || definition["components"] is not JsonArray components)
                {
                    InvalidLocation(prefix);
                    continue;
                }
                var path = $"{prefix}[{definitions[0].Index}].definition.components";
                var key = new EffectSourceKey(binding.Realm, "wound", transition.LocalWoundRef, definitionKey);
                if (!rows.TryAdd(key, new(path, "wound_materialization")))
                {
                    InvalidLocation(path);
                    continue;
                }
                var scopeIssues = authority?.ValidateNewComponents(target, components, path, "wound_materialization")
                    ?? Array.Empty<ValidationIssue>();
                foreach (var issue in scopeIssues)
                    issues.Add(new ValidationIssue(issue.FilePath, issue.Severity, issue.Message,
                        code: "wound_materialization_effect_binding_invalid", section: "wound_materialization",
                        expected: issue.Expected, actual: issue.Actual, repairHint: issue.RepairHint));
            }
            foreach (var root in transition.RootApplications)
                if (transition.EffectDefinitions.Count(definition => definition.LocalEffectRef == root.LocalEffectRef) != 1)
                    InvalidLocation(prefix);
        }
        if (issues.Count == 0)
            locations = new EffectApplicationDiagnosticLocations(rows);
        return AttachRepairContexts(binding, commands.Select(command => command.Opportunity).ToArray(),
            commands.Select(command => command.Decision).ToArray(), commands.FirstOrDefault()?.FinalSceneText, issues);

        void InvalidLocation(string path) => issues.Add(new ValidationIssue(path, IssueSeverity.Error,
            "Wound effect diagnostic coordinates must match one exact recomposed proposal.",
            code: "wound_plan_effect_diagnostic_location_invalid", section: "wound_materialization"));
    }

    internal static WoundResponseInputCompositionResult Compose(
        WoundAcceptedTurnBinding binding,
        IReadOnlyList<WoundOpportunityAuthority> opportunities,
        IReadOnlyList<JsonElement>? responseDecisions,
        string? finalSceneText,
        IReadOnlyList<WoundOpportunityDecisionReceipt> priorReceipts)
    {
        ArgumentNullException.ThrowIfNull(binding);
        ArgumentNullException.ThrowIfNull(opportunities);
        ArgumentNullException.ThrowIfNull(priorReceipts);

        var issues = new List<ValidationIssue>();
        ValidateBindingAndOpportunities(binding, opportunities, issues);
        ValidatePriorReceipts(priorReceipts, issues);
        var rawDecisions = responseDecisions ?? Array.Empty<JsonElement>();
        if (rawDecisions.Count > MaximumDecisions)
        {
            Add(
                issues,
                "woundDecisions",
                "wound_response_decision_limit_exceeded",
                $"at most {MaximumDecisions} decisions",
                rawDecisions.Count.ToString(System.Globalization.CultureInfo.InvariantCulture));
        }

        var parsed = new List<ParsedDecision>(Math.Min(rawDecisions.Count, MaximumDecisions));
        for (var index = 0; index < rawDecisions.Count && index < MaximumDecisions; index++)
        {
            if (TryParseDecision(rawDecisions[index], index, issues, out var decision))
                parsed.Add(decision!);
        }

        ValidateDecisionBijection(opportunities, parsed, issues);
        if (issues.Count != 0)
        {
            return Failure(AttachRepairContexts(
                binding,
                opportunities,
                rawDecisions,
                finalSceneText,
                issues));
        }

        var opportunityByRef = opportunities.ToDictionary(
            static value => value.PublicRef,
            StringComparer.Ordinal);
        var accepted = new List<AcceptedDecision>(parsed.Count);
        foreach (var response in parsed)
        {
            var opportunity = opportunityByRef[response.OpportunityRef];
            var evaluated = WoundOpportunityDecisionAuthority.Evaluate(
                opportunity,
                new WoundOpportunityDecisionRequest(
                    response.OpportunityRef,
                    response.Decision,
                    response.SeverityRank,
                    response.LocalWoundRef),
                priorReceipts);
            foreach (var issue in evaluated.Issues)
            {
                issues.Add(CloneIssueForDecision(issue, response.Index));
            }
            if (!evaluated.Success || evaluated.Decision is null)
                continue;

            WoundAcceptedTransitionDraft? transition = null;
            WoundPlayerNotification? notification = null;
            if (!evaluated.AlreadyConsumed &&
                string.Equals(response.Decision, "materialize", StringComparison.Ordinal))
            {
                try
                {
                    var composed = opportunity.WorseningTarget is null
                        ? TryComposeCreateTransition(
                            binding,
                            opportunity,
                            response,
                            evaluated.Decision,
                            finalSceneText,
                            issues)
                        : TryComposeWorsenTransition(
                            binding,
                            opportunity,
                            response,
                            evaluated.Decision,
                            finalSceneText,
                            issues);
                    transition = composed.Transition;
                    notification = composed.Notification;
                }
                catch (Exception exception) when (
                    exception is JsonException or InvalidOperationException or
                        ArgumentException or FormatException or OverflowException)
                {
                    Add(
                        issues,
                        $"woundDecisions[{response.Index}].proposal",
                        "wound_response_invalid_field",
                        "one well-formed strict wound proposal",
                        exception.GetType().Name);
                }
            }

            accepted.Add(new AcceptedDecision(
                opportunity,
                response,
                evaluated.Decision,
                evaluated.AlreadyConsumed,
                transition,
                notification));
        }

        if (issues.Count != 0 || accepted.Count != parsed.Count)
        {
            return Failure(AttachRepairContexts(
                binding,
                opportunities,
                rawDecisions,
                finalSceneText,
                issues));
        }

        var commands = new JsonArray();
        foreach (var value in accepted.Where(static value => !value.AlreadyConsumed))
        {
            commands.Add(new JsonObject
            {
                ["kind"] = "opportunity_decision",
                ["opportunity"] = SerializeOpportunity(value.Opportunity),
                ["decision"] = value.Response.Raw.DeepClone(),
                ["finalSceneText"] = finalSceneText
            });
        }

        var commandRoot = new JsonObject
        {
            ["schemaVersion"] = 1,
            ["sessionId"] = binding.SessionId,
            ["requestId"] = binding.RequestId,
            ["snapshotToken"] = binding.SnapshotToken,
            ["commands"] = commands
        };
        return new WoundResponseInputCompositionResult(
            commandRoot,
            accepted.Where(static value => value.Transition is not null)
                .Select(static value => value.Opportunity)
                .ToArray(),
            accepted.Where(static value => value.Transition is not null)
                .Select(static value => value.Transition!)
                .ToArray(),
            accepted.Where(static value => value.Notification is not null)
                .Select(static value => value.Notification!)
                .ToArray(),
            accepted.Select(static value => new WoundOpportunityDecisionReceipt(
                value.Authority.OpportunityId,
                value.Authority.DecisionFingerprint,
                value.Authority.OperationKey)).ToArray(),
            Array.Empty<ValidationIssue>());
    }

    private static void ValidateBindingAndOpportunities(
        WoundAcceptedTurnBinding binding,
        IReadOnlyList<WoundOpportunityAuthority> opportunities,
        ICollection<ValidationIssue> issues)
    {
        var acceptedEvents = binding.AcceptedEvents ??
            Array.Empty<WoundAcceptedEventAuthority>();
        var bindingShapeValid =
            ResourceMaterializationContract.IsExactIdentifier(binding.SessionId) &&
            ResourceMaterializationContract.IsExactIdentifier(binding.RequestId) &&
            ResourceMaterializationContract.IsExactIdentifier(binding.SnapshotToken) &&
            binding.Realm is "mortal_world" or "chaos_sea" or "shining_abode" &&
            binding.Turn > 0 &&
            (acceptedEvents.Count > 0 || opportunities.Count == 0) &&
            acceptedEvents.All(static value => value is not null &&
                ResourceMaterializationContract.IsExactIdentifier(value.EventRef) &&
                ResourceMaterializationContract.IsExactIdentifier(value.Kind) &&
                ResourceMaterializationContract.IsExactIdentifier(value.AuthorityId) &&
                ResourceMaterializationContract.IsAuthorityFingerprint(
                    value.SemanticFingerprint)) &&
            string.Equals(
                binding.AcceptedEventsFingerprint,
                WoundAcceptedEventSetFingerprint.Compute(acceptedEvents),
                StringComparison.Ordinal);
        if (!bindingShapeValid)
        {
            Add(
                issues,
                "woundDecisions",
                "wound_response_binding_invalid",
                "one complete sealed accepted-turn binding",
                "malformed binding");
            return;
        }

        if (opportunities.Count > MaximumDecisions ||
            opportunities.Any(static value => value is null) ||
            opportunities.Any(value =>
                !WoundOpportunityAuthority.HasCompleteShape(value) ||
                !string.Equals(value.SessionId, binding.SessionId, StringComparison.Ordinal) ||
                !string.Equals(value.RequestId, binding.RequestId, StringComparison.Ordinal) ||
                !string.Equals(value.SnapshotToken, binding.SnapshotToken, StringComparison.Ordinal) ||
                !string.Equals(
                    value.AcceptedEventsFingerprint,
                    binding.AcceptedEventsFingerprint,
                    StringComparison.Ordinal) ||
                !string.Equals(value.Owner.Realm, binding.Realm, StringComparison.Ordinal) ||
                acceptedEvents.Count(eventValue => string.Equals(
                    eventValue.EventRef,
                    value.EventRef,
                    StringComparison.Ordinal)) != 1))
        {
            Add(
                issues,
                "woundDecisions",
                "wound_response_opportunity_invalid",
                "at most 32 complete opportunities bound to this accepted turn",
                "malformed or foreign opportunity");
        }

        if (!ExactAndConfusableUnique(opportunities.Select(static value => value.OpportunityId)) ||
            !PublicRefsUnique(opportunities.Select(static value => value.PublicRef)))
        {
            Add(
                issues,
                "woundDecisions",
                "wound_response_opportunity_duplicate",
                "exact and confusable-unique opportunity identities",
                "duplicate opportunity identity");
        }
    }

    private static void ValidatePriorReceipts(
        IReadOnlyList<WoundOpportunityDecisionReceipt> receipts,
        ICollection<ValidationIssue> issues)
    {
        if (receipts.Any(static value => value is null ||
                !ResourceMaterializationContract.IsExactIdentifier(value.OpportunityId) ||
                !ResourceMaterializationContract.IsAuthorityFingerprint(
                    value.DecisionFingerprint) ||
                !ResourceMaterializationContract.IsExactIdentifier(value.OperationKey)) ||
            !ExactAndConfusableUnique(receipts.Select(static value => value.OpportunityId)))
        {
            Add(
                issues,
                "woundDecisionReceipts",
                "wound_response_receipt_invalid",
                "unique complete prior decision receipts",
                "malformed or duplicate receipt");
        }
    }

    private static void ValidateDecisionBijection(
        IReadOnlyList<WoundOpportunityAuthority> opportunities,
        IReadOnlyList<ParsedDecision> decisions,
        ICollection<ValidationIssue> issues)
    {
        foreach (var opportunity in opportunities)
        {
            var count = decisions.Count(value => string.Equals(
                value.OpportunityRef,
                opportunity.PublicRef,
                StringComparison.Ordinal));
            if (count != 1)
            {
                Add(
                    issues,
                    "woundDecisions",
                    "wound_response_decision_missing_or_duplicate",
                    $"one decision for {opportunity.PublicRef}",
                    count.ToString(System.Globalization.CultureInfo.InvariantCulture));
            }
        }

        foreach (var decision in decisions)
        {
            if (opportunities.Count(value => string.Equals(
                    value.PublicRef,
                    decision.OpportunityRef,
                    StringComparison.Ordinal)) != 1)
            {
                Add(
                    issues,
                    "woundDecisions",
                    "wound_response_opportunity_reference_unknown",
                    "one exact current opportunityRef",
                    decision.OpportunityRef);
            }
        }
    }

    private static bool ExactAndConfusableUnique(IEnumerable<string> values)
    {
        var exact = new HashSet<string>(StringComparer.Ordinal);
        var confusable = new HashSet<string>(StringComparer.Ordinal);
        foreach (var value in values)
        {
            if (!ResourceMaterializationContract.IsExactIdentifier(value) ||
                !exact.Add(value) ||
                !confusable.Add(ExactIdentifierConfusableKey.Build(value)))
            {
                return false;
            }
        }
        return true;
    }

    private static bool PublicRefsUnique(IEnumerable<string> values)
    {
        var exact = new HashSet<string>(StringComparer.Ordinal);
        var confusable = new HashSet<string>(StringComparer.Ordinal);
        foreach (var value in values)
        {
            if (string.IsNullOrWhiteSpace(value) ||
                !exact.Add(value) ||
                !confusable.Add(MortalLocationIdentityState.BuildConfusableKey(value)))
            {
                return false;
            }
        }
        return true;
    }

    private static WoundResponseInputCompositionResult Failure(
        IReadOnlyList<ValidationIssue> issues) => new(
        null,
        Array.Empty<WoundOpportunityAuthority>(),
        Array.Empty<WoundAcceptedTransitionDraft>(),
        Array.Empty<WoundPlayerNotification>(),
        Array.Empty<WoundOpportunityDecisionReceipt>(),
        issues.Count == 0
            ? new[]
            {
                NewIssue(
                    "woundDecisions",
                    "wound_response_composition_failed",
                    "one complete atomic wound response",
                    "incomplete composition")
            }
            : issues);

    internal static IReadOnlyList<ValidationIssue> AttachRepairContexts(
        WoundAcceptedTurnBinding binding,
        IReadOnlyList<WoundOpportunityAuthority> opportunities,
        IReadOnlyList<JsonElement> rawDecisions,
        string? finalSceneText,
        IReadOnlyList<ValidationIssue> issues)
    {
        var result = issues.Select(WoundAcceptedTurnData.CloneIssue).ToList();
        var additions = new List<ValidationIssue>();
        for (var issueIndex = 0; issueIndex < result.Count; issueIndex++)
        {
            var issue = result[issueIndex];
            var replacedSourceIssue = false;
            foreach (var decisionIndex in ResolveRepairDecisionIndexes(
                         issue,
                         rawDecisions))
            {
                if (!TryBuildRepairIssue(
                        binding,
                        opportunities,
                        rawDecisions,
                        finalSceneText,
                        issues,
                        issue,
                        decisionIndex,
                        out var projected))
                {
                    continue;
                }

                if (WoundRepairPacketBuilder.IsRepairableIssue(issue) &&
                    !replacedSourceIssue)
                {
                    result[issueIndex] = projected;
                    replacedSourceIssue = true;
                }
                else
                {
                    additions.Add(projected);
                }
            }
        }

        foreach (var addition in additions)
        {
            if (!result.Any(issue =>
                    string.Equals(issue.Code, addition.Code, StringComparison.Ordinal) &&
                    string.Equals(
                        issue.FilePath,
                        addition.FilePath,
                        StringComparison.Ordinal) &&
                    string.Equals(
                        issue.WoundRepairContext?.CandidateRef,
                        addition.WoundRepairContext?.CandidateRef,
                        StringComparison.Ordinal)))
            {
                result.Add(addition);
            }
        }
        return result;
    }

    private static IReadOnlyList<int> ResolveRepairDecisionIndexes(
        ValidationIssue issue,
        IReadOnlyList<JsonElement> rawDecisions)
    {
        if (TryReadDecisionIndex(issue.FilePath, out var decisionIndex))
            return new[] { decisionIndex };
        if (issue.Code is not
                "wound_acquisition_narration_missing" and
                not "wound_acquisition_narration_contradiction")
        {
            return Array.Empty<int>();
        }

        return rawDecisions
            .Select((decision, index) => (decision, index))
            .Where(static value => IsMaterializeDecision(value.decision))
            .Select(static value => value.index)
            .ToArray();
    }

    private static bool TryBuildRepairIssue(
        WoundAcceptedTurnBinding binding,
        IReadOnlyList<WoundOpportunityAuthority> opportunities,
        IReadOnlyList<JsonElement> rawDecisions,
        string? finalSceneText,
        IReadOnlyList<ValidationIssue> allIssues,
        ValidationIssue source,
        int decisionIndex,
        out ValidationIssue projected)
    {
        projected = null!;
        if (decisionIndex < 0 ||
            decisionIndex >= rawDecisions.Count ||
            rawDecisions[decisionIndex].ValueKind != JsonValueKind.Object)
        {
            return false;
        }

        var duplicateIssues = new List<ValidationIssue>();
        ValidateNoDuplicateProperties(
            rawDecisions[decisionIndex],
            $"woundDecisions[{decisionIndex}]",
            duplicateIssues);
        if (duplicateIssues.Count != 0)
            return false;

        JsonObject rejectedDecision;
        try
        {
            rejectedDecision = JsonNode.Parse(
                rawDecisions[decisionIndex].GetRawText())?.AsObject() ??
                new JsonObject();
        }
        catch (Exception exception) when (
            exception is JsonException or InvalidOperationException)
        {
            return false;
        }

        if (!TryReadRepairString(
                rejectedDecision,
                "opportunityRef",
                out var opportunityRef) ||
            !TryReadRepairString(rejectedDecision, "decision", out var decision) ||
            !string.Equals(decision, "materialize", StringComparison.Ordinal) ||
            !TryReadRepairString(rejectedDecision, "woundRef", out _) ||
            rejectedDecision["proposal"] is not JsonObject proposal)
        {
            return false;
        }

        var matchingOpportunities = opportunities.Where(value =>
            string.Equals(value.PublicRef, opportunityRef, StringComparison.Ordinal))
            .ToArray();
        if (matchingOpportunities.Length != 1)
            return false;
        var opportunity = matchingOpportunities[0];
        if (!TryProjectRepairCoordinate(
                source,
                decisionIndex,
                proposal,
                out var projectedPath,
                out var projectedCode))
        {
            return false;
        }
        if (string.Equals(
                source.Code,
                "wound_response_unknown_field",
                StringComparison.Ordinal) &&
            !HasValidSanitizedUnknownFieldBase(
                binding,
                opportunity,
                rejectedDecision,
                decisionIndex,
                allIssues,
                finalSceneText))
        {
            return false;
        }

        projected = new ValidationIssue(
            projectedPath,
            source.Severity,
            source.Message,
            projectedCode,
            source.Actor,
            "wound_materialization",
            source.Expected,
            source.Actual,
            source.RepairHint,
            source.Category,
            source.RepairTargetFiles.ToArray())
        {
            WoundRepairContext = new WoundRepairContext(
                binding.SessionId,
                binding.RequestId,
                binding.SnapshotToken,
                opportunity.WorseningTarget is null
                    ? "construct_wound"
                    : "repair_wound",
                CreateLocalIdentifier(
                    "candidate_wound_repair",
                    binding.SessionId,
                    binding.RequestId,
                    binding.SnapshotToken,
                    opportunity.PublicRef,
                    decisionIndex.ToString(
                        System.Globalization.CultureInfo.InvariantCulture)),
                WoundAcceptedTurnFingerprintWriter.Compute(new string?[]
                {
                    "book_of_eternity.wound.repair_candidate",
                    "1",
                    binding.SessionId,
                    binding.RequestId,
                    binding.SnapshotToken,
                    opportunity.AuthorityFingerprint,
                    rawDecisions[decisionIndex].GetRawText(),
                    finalSceneText
                }),
                opportunity.PublicRef,
                new JsonObject
                {
                    ["event"] = opportunity.SafeContext.Cause,
                    ["target"] = opportunity.SafeContext.Target,
                    ["realm"] = ReadableRealm(binding.Realm)
                },
                opportunity.GuaranteedTrigger is null
                    ? new[] { "none", "materialize" }
                    : new[] { "materialize" },
                Roman(opportunity.MinimumSeverityRank ?? 1),
                Roman(opportunity.MaximumSeverityRank),
                rejectedDecision,
                opportunity.AuthorityFingerprint)
        };
        return WoundRepairPacketBuilder.IsRepairableIssue(projected);
    }

    private static bool HasValidSanitizedUnknownFieldBase(
        WoundAcceptedTurnBinding binding,
        WoundOpportunityAuthority opportunity,
        JsonObject rejectedDecision,
        int decisionIndex,
        IReadOnlyList<ValidationIssue> allIssues,
        string? finalSceneText)
    {
        var prefix = $"woundDecisions[{decisionIndex}].proposal.";
        var unknownPaths = allIssues
            .Where(issue =>
                string.Equals(
                    issue.Code,
                    "wound_response_unknown_field",
                    StringComparison.Ordinal) &&
                issue.FilePath.StartsWith(prefix, StringComparison.Ordinal))
            .Select(issue => issue.FilePath[prefix.Length..])
            .Where(static path => path.Length != 0)
            .Distinct(StringComparer.Ordinal)
            .ToArray();
        if (unknownPaths.Length == 0)
            return false;

        var sanitizedDecision = rejectedDecision.DeepClone().AsObject();
        if (sanitizedDecision["proposal"] is not JsonObject sanitizedProposal)
            return false;
        foreach (var unknownPath in unknownPaths)
            WoundRepairPacketBuilder.RemovePath(sanitizedProposal, unknownPath);

        var validationIssues = new List<ValidationIssue>();
        try
        {
            using var document = JsonDocument.Parse(sanitizedDecision.ToJsonString());
            if (!TryParseDecision(
                    document.RootElement,
                    decisionIndex,
                    validationIssues,
                    out var parsed) ||
                parsed is null)
            {
                return false;
            }

            var evaluated = WoundOpportunityDecisionAuthority.Evaluate(
                opportunity,
                new WoundOpportunityDecisionRequest(
                    parsed.OpportunityRef,
                    parsed.Decision,
                    parsed.SeverityRank,
                    parsed.LocalWoundRef),
                Array.Empty<WoundOpportunityDecisionReceipt>());
            foreach (var issue in evaluated.Issues)
                validationIssues.Add(CloneIssueForDecision(issue, decisionIndex));
            if (!evaluated.Success || evaluated.Decision is null)
                return false;

            var composed = opportunity.WorseningTarget is null
                ? TryComposeCreateTransition(
                    binding,
                    opportunity,
                    parsed,
                    evaluated.Decision,
                    finalSceneText,
                    validationIssues)
                : TryComposeWorsenTransition(
                    binding,
                    opportunity,
                    parsed,
                    evaluated.Decision,
                    finalSceneText,
                    validationIssues);
            return validationIssues.Count == 0 && composed.Transition is not null;
        }
        catch (Exception exception) when (
            exception is JsonException or InvalidOperationException or
                ArgumentException or FormatException or OverflowException)
        {
            return false;
        }
    }

    private static bool TryProjectRepairCoordinate(
        ValidationIssue issue,
        int decisionIndex,
        JsonObject proposal,
        out string path,
        out string code)
    {
        var prefix = $"woundDecisions[{decisionIndex}].proposal.";
        path = issue.FilePath;
        code = issue.Code ?? string.Empty;

        if (code == "wound_severity_above_opportunity")
        {
            path = prefix + "severity";
            return true;
        }
        if (code is
                "wound_acquisition_narration_missing" or
                "wound_acquisition_narration_contradiction")
        {
            path = "output/narrative_response.json.response";
            return true;
        }
        if (code == "wound_response_unknown_field" &&
            string.Equals(issue.FilePath, prefix + "owner", StringComparison.Ordinal))
        {
            return true;
        }
        if (code == "wound_response_unknown_field" &&
            issue.FilePath.StartsWith(prefix, StringComparison.Ordinal))
        {
            return true;
        }
        if (code is
                "wound_consequence_slot_budget_exceeded" or
                "wound_materialization_consequence_slot_invalid")
        {
            return TryFindOverBudgetSlotPath(proposal, prefix, out path) &&
                   SetCode("wound_consequence_slot_budget_exceeded", ref code);
        }
        if (code == "wound_materialization_effect_binding_invalid" &&
            issue.FilePath.StartsWith(prefix + "consequenceDefinitions[", StringComparison.Ordinal) &&
            issue.FilePath.Contains(".definition.components[", StringComparison.Ordinal) &&
            issue.FilePath.EndsWith(".payload.scope.skillId", StringComparison.Ordinal))
        {
            path = issue.FilePath;
            return true;
        }
        if ((code == "wound_materialization_effect_binding_invalid" ||
             code == "wound_response_client_authority_forbidden") &&
            TryFindEffectLinkPath(proposal, prefix, issue.FilePath, out path))
        {
            code = "wound_materialization_effect_binding_invalid";
            return true;
        }
        if (code == "wound_materialization_missing_field" &&
            issue.FilePath.StartsWith(prefix + "treatment.routes[", StringComparison.Ordinal))
        {
            return true;
        }
        if (code == "wound_consequence_resource_bound_missing" &&
            issue.FilePath.StartsWith(prefix, StringComparison.Ordinal) &&
            issue.FilePath.EndsWith(".payload.resource", StringComparison.Ordinal))
        {
            return true;
        }
        if (code == "wound_materialization_invalid_field" &&
            issue.FilePath.StartsWith(prefix, StringComparison.Ordinal))
        {
            return true;
        }
        return false;
    }

    private static bool TryFindOverBudgetSlotPath(
        JsonObject proposal,
        string prefix,
        out string path)
    {
        path = string.Empty;
        if (proposal["consequenceDefinitions"] is not JsonArray definitions)
            return false;
        for (var index = 0; index < definitions.Count; index++)
        {
            if (definitions[index]?["root"]?["slots"] is JsonArray slots &&
                slots.Count > 2)
            {
                path = $"{prefix}consequenceDefinitions[{index}].root.slots";
                return true;
            }
        }
        return false;
    }

    private static bool TryFindEffectLinkPath(
        JsonObject proposal,
        string prefix,
        string issuePath,
        out string path)
    {
        if (issuePath.StartsWith(prefix, StringComparison.Ordinal) &&
            issuePath.EndsWith(".definition.links", StringComparison.Ordinal))
        {
            path = issuePath;
            return true;
        }

        if (proposal["consequenceDefinitions"] is JsonArray definitions)
        {
            for (var index = 0; index < definitions.Count; index++)
            {
                if (definitions[index]?["definition"]?["links"] is JsonArray links &&
                    links.Count != 0)
                {
                    path = $"{prefix}consequenceDefinitions[{index}].definition.links";
                    return true;
                }
            }
        }
        path = string.Empty;
        return false;
    }

    private static bool TryReadDecisionIndex(string path, out int index)
    {
        index = -1;
        const string prefix = "woundDecisions[";
        if (!path.StartsWith(prefix, StringComparison.Ordinal))
            return false;
        var close = path.IndexOf(']', prefix.Length);
        if (close < 0)
            return false;
        var token = path[prefix.Length..close];
        return int.TryParse(
                   token,
                   System.Globalization.NumberStyles.None,
                   System.Globalization.CultureInfo.InvariantCulture,
                   out index) &&
               index >= 0 &&
               string.Equals(
                   token,
                   index.ToString(System.Globalization.CultureInfo.InvariantCulture),
                   StringComparison.Ordinal);
    }

    private static bool IsMaterializeDecision(JsonElement decision)
    {
        if (decision.ValueKind != JsonValueKind.Object)
            return false;
        var values = decision.EnumerateObject()
            .Where(static property => string.Equals(
                property.Name,
                "decision",
                StringComparison.Ordinal))
            .ToArray();
        return values.Length == 1 &&
               values[0].Value.ValueKind == JsonValueKind.String &&
               string.Equals(
                   values[0].Value.GetString(),
                   "materialize",
                   StringComparison.Ordinal);
    }

    private static bool TryReadRepairString(
        JsonObject source,
        string property,
        out string value)
    {
        value = source[property] is JsonValue scalar &&
                scalar.TryGetValue<string>(out var text)
            ? text
            : string.Empty;
        return ResourceMaterializationContract.IsExactIdentifier(value);
    }

    private static ValidationIssue CloneIssueForDecision(
        ValidationIssue issue,
        int decisionIndex)
    {
        var path = issue.Code == "wound_severity_above_opportunity"
            ? $"woundDecisions[{decisionIndex}].proposal.severity"
            : issue.FilePath;
        return new ValidationIssue(
            path,
            issue.Severity,
            issue.Message,
            issue.Code,
            issue.Actor,
            issue.Section,
            issue.Expected,
            issue.Actual,
            issue.RepairHint,
            issue.Category,
            issue.RepairTargetFiles.ToArray())
        {
            FactionRepairClassification = issue.FactionRepairClassification,
            MortalItemRepairContext = issue.MortalItemRepairContext,
            MortalLocationRepairContext = issue.MortalLocationRepairContext,
            EffectRepairContext = issue.EffectRepairContext,
            WoundRepairContext = issue.WoundRepairContext?.Clone()
        };
    }

    private static string ReadableRealm(string realm) => realm switch
    {
        "mortal_world" => "Смертный мир",
        "chaos_sea" => "Море Хаоса",
        "shining_abode" => "Сияющая обитель",
        _ => realm
    };

    private static bool SetCode(string value, ref string code)
    {
        code = value;
        return true;
    }

    private static ValidationIssue NewIssue(
        string path,
        string code,
        string expected,
        string actual) => new(
        path,
        IssueSeverity.Error,
        "The GM wound decision violates the strict accepted opportunity contract.",
        code: code,
        section: "wound_response",
        expected: expected,
        actual: actual,
        repairHint:
            "Keep the sealed opportunity unchanged and correct only the bounded wound decision proposal.");

    private static void Add(
        ICollection<ValidationIssue> issues,
        string path,
        string code,
        string expected,
        string actual) => issues.Add(NewIssue(path, code, expected, actual));
}
