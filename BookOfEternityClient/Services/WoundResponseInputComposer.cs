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
            return Failure(issues);

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
            issues.AddRange(evaluated.Issues);
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
            return Failure(issues);

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
            acceptedEvents.Count > 0 &&
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
                !confusable.Add(MortalLocationIdentityState.BuildConfusableKey(value)))
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
