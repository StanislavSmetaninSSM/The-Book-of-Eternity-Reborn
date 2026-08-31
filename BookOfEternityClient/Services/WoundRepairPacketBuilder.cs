using System.Text.Json.Nodes;

namespace BookOfEternityClient.Services;

internal sealed record WoundRepairPacketIssue(
    string Path,
    string Code,
    string Expected,
    string Actual);

internal sealed record WoundRepairPacketReceipt(
    string SessionId,
    string RequestId,
    string SnapshotToken,
    string CandidateRef,
    string SemanticFingerprint);

internal sealed record WoundRepairPacketAuthority(
    string SessionId,
    string RequestId,
    string SnapshotToken,
    string Generation,
    string EventFingerprint,
    string TargetFingerprint,
    string RollFingerprint);

/// <summary>
/// Detached client-owned authority needed to reconstruct one bounded wound repair
/// candidate from a validation issue. None of these fields is inferred from an
/// error message or from mutable canonical state during repair dispatch.
/// </summary>
internal sealed class WoundRepairContext
{
    private readonly JsonObject _safeContext;
    private readonly string[] _allowedDecisions;
    private readonly JsonObject _rejectedDecision;

    internal WoundRepairContext(
        string sessionId,
        string requestId,
        string snapshotToken,
        string kind,
        string candidateRef,
        string semanticFingerprint,
        string opportunityRef,
        JsonObject safeContext,
        IReadOnlyList<string> allowedDecisions,
        string minimumSeverity,
        string maximumSeverity,
        JsonObject rejectedDecision,
        string? opportunityAuthorityFingerprint = null)
    {
        SessionId = sessionId;
        RequestId = requestId;
        SnapshotToken = snapshotToken;
        Kind = kind;
        CandidateRef = candidateRef;
        SemanticFingerprint = semanticFingerprint;
        OpportunityRef = opportunityRef;
        _safeContext = safeContext?.DeepClone().AsObject() ?? new JsonObject();
        _allowedDecisions = allowedDecisions?.ToArray() ?? Array.Empty<string>();
        MinimumSeverity = minimumSeverity;
        MaximumSeverity = maximumSeverity;
        _rejectedDecision = rejectedDecision?.DeepClone().AsObject() ?? new JsonObject();
        OpportunityAuthorityFingerprint = opportunityAuthorityFingerprint;
    }

    internal string SessionId { get; }
    internal string RequestId { get; }
    internal string SnapshotToken { get; }
    internal string Kind { get; }
    internal string CandidateRef { get; }
    internal string SemanticFingerprint { get; }
    internal string OpportunityRef { get; }
    internal JsonObject SafeContext => _safeContext.DeepClone().AsObject();
    internal IReadOnlyList<string> AllowedDecisions => _allowedDecisions.ToArray();
    internal string MinimumSeverity { get; }
    internal string MaximumSeverity { get; }
    internal JsonObject RejectedDecision => _rejectedDecision.DeepClone().AsObject();
    internal string? OpportunityAuthorityFingerprint { get; }

    internal WoundRepairContext Clone() => new(
        SessionId,
        RequestId,
        SnapshotToken,
        Kind,
        CandidateRef,
        SemanticFingerprint,
        OpportunityRef,
        _safeContext,
        _allowedDecisions,
        MinimumSeverity,
        MaximumSeverity,
        _rejectedDecision,
        OpportunityAuthorityFingerprint);

    internal bool HasSameAuthority(WoundRepairContext other) =>
        string.Equals(SessionId, other.SessionId, StringComparison.Ordinal) &&
        string.Equals(RequestId, other.RequestId, StringComparison.Ordinal) &&
        string.Equals(SnapshotToken, other.SnapshotToken, StringComparison.Ordinal) &&
        string.Equals(Kind, other.Kind, StringComparison.Ordinal) &&
        string.Equals(CandidateRef, other.CandidateRef, StringComparison.Ordinal) &&
        string.Equals(SemanticFingerprint, other.SemanticFingerprint, StringComparison.Ordinal) &&
        string.Equals(OpportunityRef, other.OpportunityRef, StringComparison.Ordinal) &&
        _allowedDecisions.SequenceEqual(other._allowedDecisions, StringComparer.Ordinal) &&
        string.Equals(MinimumSeverity, other.MinimumSeverity, StringComparison.Ordinal) &&
        string.Equals(MaximumSeverity, other.MaximumSeverity, StringComparison.Ordinal) &&
        string.Equals(
            OpportunityAuthorityFingerprint,
            other.OpportunityAuthorityFingerprint,
            StringComparison.Ordinal) &&
        JsonNode.DeepEquals(_safeContext, other._safeContext) &&
        JsonNode.DeepEquals(_rejectedDecision, other._rejectedDecision);
}

internal sealed class WoundRepairBuildRequest
{
    private readonly WoundRepairCandidateInput[] _candidates;

    internal WoundRepairBuildRequest(
        string sessionId,
        string requestId,
        string snapshotToken,
        IReadOnlyList<WoundRepairCandidateInput> candidates)
    {
        SessionId = sessionId;
        RequestId = requestId;
        SnapshotToken = snapshotToken;
        _candidates = candidates?.ToArray() ?? Array.Empty<WoundRepairCandidateInput>();
    }

    internal string SessionId { get; }
    internal string RequestId { get; }
    internal string SnapshotToken { get; }
    internal IReadOnlyList<WoundRepairCandidateInput> Candidates => _candidates;
}

/// <summary>
/// One rejected GM-authored wound candidate. The constructor detaches the mutable
/// JSON inputs from their caller; the exposed graphs intentionally remain editable
/// while the caller is assembling the request. Building a packet takes a second,
/// private snapshot and never mutates these graphs.
/// </summary>
internal sealed class WoundRepairCandidateInput
{
    private readonly string[] _allowedDecisions;
    private readonly ValidationIssue[] _issues;

    internal WoundRepairCandidateInput(
        string kind,
        string candidateRef,
        string semanticFingerprint,
        string opportunityRef,
        JsonObject safeContext,
        IReadOnlyList<string> allowedDecisions,
        string minimumSeverity,
        string maximumSeverity,
        JsonObject rejectedDecision,
        IReadOnlyList<ValidationIssue> issues,
        string? opportunityAuthorityFingerprint = null)
    {
        Kind = kind;
        CandidateRef = candidateRef;
        SemanticFingerprint = semanticFingerprint;
        OpportunityRef = opportunityRef;
        SafeContext = safeContext?.DeepClone().AsObject() ?? new JsonObject();
        _allowedDecisions = allowedDecisions?.ToArray() ?? Array.Empty<string>();
        MinimumSeverity = minimumSeverity;
        MaximumSeverity = maximumSeverity;
        RejectedDecision = rejectedDecision?.DeepClone().AsObject() ?? new JsonObject();
        _issues = issues?.ToArray() ?? Array.Empty<ValidationIssue>();
        OpportunityAuthorityFingerprint = opportunityAuthorityFingerprint;
    }

    internal string Kind { get; }
    internal string CandidateRef { get; }
    internal string SemanticFingerprint { get; }
    internal string OpportunityRef { get; }
    internal JsonObject SafeContext { get; }
    internal IReadOnlyList<string> AllowedDecisions => _allowedDecisions;
    internal string MinimumSeverity { get; }
    internal string MaximumSeverity { get; }
    internal JsonObject RejectedDecision { get; }
    internal IReadOnlyList<ValidationIssue> Issues => _issues;
    internal string? OpportunityAuthorityFingerprint { get; }
}

internal sealed class WoundRepairPacket
{
    private readonly WoundRepairPacketIssue[] _issues;
    private readonly JsonObject _safeContext;
    private readonly JsonObject _preservedProposal;
    private readonly JsonObject _requiredResponseShape;
    private readonly JsonObject _rejectedDecision;
    private readonly string? _opportunityAuthorityFingerprint;

    internal WoundRepairPacket(
        string sessionId,
        string requestId,
        string snapshotToken,
        string candidateRef,
        string semanticFingerprint,
        IReadOnlyList<WoundRepairPacketIssue> issues,
        JsonObject safeContext,
        JsonObject preservedProposal,
        JsonObject requiredResponseShape,
        JsonObject rejectedDecision,
        string? opportunityAuthorityFingerprint)
    {
        SessionId = sessionId;
        RequestId = requestId;
        SnapshotToken = snapshotToken;
        CandidateRef = candidateRef;
        SemanticFingerprint = semanticFingerprint;
        _issues = issues.ToArray();
        _safeContext = safeContext.DeepClone().AsObject();
        _preservedProposal = preservedProposal.DeepClone().AsObject();
        _requiredResponseShape = requiredResponseShape.DeepClone().AsObject();
        _rejectedDecision = rejectedDecision.DeepClone().AsObject();
        _opportunityAuthorityFingerprint = opportunityAuthorityFingerprint;
    }

    internal string Kind => "wound_materialization_repair";
    internal string SessionId { get; }
    internal string RequestId { get; }
    internal string SnapshotToken { get; }
    internal string CandidateRef { get; }
    internal string SemanticFingerprint { get; }
    internal IReadOnlyList<WoundRepairPacketIssue> Issues => _issues.ToArray();
    internal JsonObject SafeContext => _safeContext.DeepClone().AsObject();
    internal JsonObject PreservedProposal => _preservedProposal.DeepClone().AsObject();
    internal JsonObject RequiredResponseShape => _requiredResponseShape.DeepClone().AsObject();
    internal bool RequiresResponseCorrection => _issues.Any(static issue =>
        string.Equals(issue.Path, "response", StringComparison.Ordinal));

    internal string? OpportunityRef =>
        _requiredResponseShape["woundDecisions"] is JsonArray { Count: 1 } decisions &&
        decisions[0] is JsonObject decision &&
        decision["opportunityRef"] is JsonValue value &&
        value.TryGetValue<string>(out var opportunityRef)
            ? opportunityRef
            : null;

    internal bool MatchesRejectedDecision(JsonObject? decision) =>
        decision is not null && JsonNode.DeepEquals(decision, _rejectedDecision);

    internal bool MatchesOpportunity(WoundOpportunityAuthority? opportunity)
    {
        if (opportunity is null ||
            !ResourceMaterializationContract.IsAuthorityFingerprint(
                _opportunityAuthorityFingerprint) ||
            !string.Equals(
                opportunity.AuthorityFingerprint,
                _opportunityAuthorityFingerprint,
                StringComparison.Ordinal) ||
            !string.Equals(opportunity.SessionId, SessionId, StringComparison.Ordinal) ||
            !string.Equals(opportunity.RequestId, RequestId, StringComparison.Ordinal) ||
            !string.Equals(
                opportunity.SnapshotToken,
                SnapshotToken,
                StringComparison.Ordinal) ||
            _requiredResponseShape["woundDecisions"] is not JsonArray decisions ||
            decisions.Count != 1 ||
            decisions[0] is not JsonObject decision ||
            decision["opportunityRef"] is not JsonValue expectedRefNode ||
            !expectedRefNode.TryGetValue<string>(out var expectedRef))
        {
            return false;
        }
        return string.Equals(
                   opportunity.PublicRef,
                   expectedRef,
                   StringComparison.Ordinal) &&
               WoundOpportunityAuthority.HasCompleteShape(opportunity);
    }

    internal WoundRepairPacketReceipt CreateReceipt() => new(
        SessionId,
        RequestId,
        SnapshotToken,
        CandidateRef,
        SemanticFingerprint);

    internal JsonObject ToJsonObject() => new()
    {
        ["kind"] = Kind,
        ["sessionId"] = SessionId,
        ["requestId"] = RequestId,
        ["snapshotToken"] = SnapshotToken,
        ["candidateRef"] = CandidateRef,
        ["semanticFingerprint"] = SemanticFingerprint,
        ["issues"] = new JsonArray(_issues.Select(static issue =>
            (JsonNode)new JsonObject
            {
                ["path"] = issue.Path,
                ["code"] = issue.Code,
                ["expected"] = issue.Expected,
                ["actual"] = issue.Actual
            }).ToArray()),
        ["safeContext"] = _safeContext.DeepClone(),
        ["preservedProposal"] = _preservedProposal.DeepClone(),
        ["requiredResponseShape"] = _requiredResponseShape.DeepClone()
    };

    internal bool MatchesCorrectedDecision(
        JsonObject? correctedDecision,
        string? finalSceneText)
    {
        if (correctedDecision is null ||
            correctedDecision.Count != 4 ||
            correctedDecision.Any(static pair => pair.Key is not
                ("opportunityRef" or "decision" or "woundRef" or "proposal")) ||
            _requiredResponseShape["woundDecisions"] is not JsonArray requiredDecisions ||
            requiredDecisions.Count != 1 ||
            requiredDecisions[0] is not JsonObject requiredDecision ||
            !SameRequiredString(
                correctedDecision,
                requiredDecision,
                "opportunityRef") ||
            !SameRequiredString(correctedDecision, requiredDecision, "decision") ||
            !SameRequiredString(correctedDecision, requiredDecision, "woundRef") ||
            correctedDecision["proposal"] is not JsonObject correctedProposal)
        {
            return false;
        }

        var proposalWithoutCorrections = correctedProposal.DeepClone().AsObject();
        foreach (var issue in _issues)
        {
            if (string.Equals(issue.Path, "response", StringComparison.Ordinal))
                continue;
            if (!issue.Path.StartsWith("proposal.", StringComparison.Ordinal))
                return false;
            var semanticPath = issue.Path["proposal.".Length..];
            var omissionRequired =
                string.Equals(
                    issue.Code,
                    "wound_response_unknown_field",
                    StringComparison.Ordinal) ||
                string.Equals(
                    issue.Code,
                    "wound_materialization_invalid_field",
                    StringComparison.Ordinal);
            var correctedContainsPath = TryResolvePath(
                correctedProposal,
                semanticPath,
                out _);
            if (omissionRequired == correctedContainsPath)
                return false;
            WoundRepairPacketBuilder.RemovePath(
                proposalWithoutCorrections,
                semanticPath);
        }

        if (!JsonNode.DeepEquals(proposalWithoutCorrections, _preservedProposal) ||
            !TryResolvePath(
                correctedProposal,
                "display.acquisitionNarration",
                out var narrationNode) ||
            narrationNode is not JsonValue narrationValue ||
            !narrationValue.TryGetValue<string>(out var narration) ||
            string.IsNullOrWhiteSpace(narration) ||
            string.IsNullOrWhiteSpace(finalSceneText) ||
            !finalSceneText.Contains(narration, StringComparison.Ordinal))
        {
            return false;
        }
        return true;
    }

    private static bool SameRequiredString(
        JsonObject actual,
        JsonObject expected,
        string property) =>
        actual[property] is JsonValue actualValue &&
        expected[property] is JsonValue expectedValue &&
        actualValue.TryGetValue<string>(out var actualText) &&
        expectedValue.TryGetValue<string>(out var expectedText) &&
        string.Equals(actualText, expectedText, StringComparison.Ordinal);

    private static bool TryResolvePath(
        JsonObject root,
        string path,
        out JsonNode? value)
    {
        value = root;
        if (!WoundRepairPacketBuilder.TryParsePath(path, out var segments))
            return false;
        foreach (var segment in segments)
        {
            value = segment switch
            {
                string property when value is JsonObject currentObject &&
                                     currentObject.TryGetPropertyValue(
                                         property,
                                         out var propertyValue) =>
                    propertyValue,
                int ordinal when value is JsonArray currentArray &&
                                 ordinal >= 0 && ordinal < currentArray.Count =>
                    currentArray[ordinal],
                _ => null
            };
            if (value is null)
                return false;
        }
        return true;
    }
}

/// <summary>
/// Converts a bounded rejected wound candidate into safe, semantic-only repair
/// instructions. Client identities, canonical paths, seals, fingerprints, private
/// NPC data, and unrelated response content are removed before any packet is made.
/// One invalid candidate invalidates the complete pending wave.
/// </summary>
internal static partial class WoundRepairPacketBuilder
{
    private const int CandidateLimit = 64;
    private const int EvidenceLimit = 512;

    private static readonly string[] SafeContextFields =
        { "event", "target", "realm" };

    private static readonly string[] ProposalFields =
    {
        "classification",
        "display",
        "severity",
        "complications",
        "consequenceDefinitions",
        "treatment",
        "recovery"
    };

    private static readonly HashSet<string> CandidateKinds = new(
        new[]
        {
            "construct_wound",
            "repair_wound",
            "author_alternative_treatment",
            "narrate_acquisition"
        },
        StringComparer.Ordinal);

    private static readonly HashSet<string> RepairableIssueCodes = new(
        new[]
        {
            "wound_response_unknown_field",
            "wound_severity_above_opportunity",
            "wound_consequence_slot_budget_exceeded",
            "wound_materialization_effect_binding_invalid",
            "wound_materialization_missing_field",
            "wound_consequence_resource_bound_missing",
            "wound_acquisition_narration_missing",
            "wound_acquisition_narration_contradiction",
            "wound_materialization_invalid_field",
            "wound_repair_retry_authority_unavailable"
        },
        StringComparer.Ordinal);

    private static readonly HashSet<string> SensitiveKeys = new(
        new[]
        {
            "ownerId",
            "providerId",
            "routeSeal",
            "resourceSeal",
            "privateNpcData",
            "woundId",
            "effectId",
            "resourceId",
            "providerSeal",
            "sourceSeal",
            "carrierPath",
            "authorityFingerprint",
            "gmPrivateNotes"
        },
        StringComparer.OrdinalIgnoreCase);

    internal static IReadOnlyList<WoundRepairPacket> Build(
        WoundRepairBuildRequest request)
    {
        if (!TryBuild(request, out var packets))
            return Array.Empty<WoundRepairPacket>();
        return packets;
    }

    internal static bool RequiresFailClosedRollback(
        WoundRepairBuildRequest request) =>
        !TryBuild(request, out _);

    internal static IReadOnlyList<WoundRepairPacket> Build(
        IEnumerable<ValidationIssue> issues)
    {
        var snapshot = issues?.ToArray() ?? Array.Empty<ValidationIssue>();
        return TryBuild(snapshot, out var packets)
            ? packets
            : Array.Empty<WoundRepairPacket>();
    }

    internal static bool RequiresFailClosedRollback(
        IEnumerable<ValidationIssue> issues)
    {
        var snapshot = issues?.ToArray() ?? Array.Empty<ValidationIssue>();
        return snapshot.Any(IsRepairableIssue) && !TryBuild(snapshot, out _);
    }

    private static bool TryBuild(
        IReadOnlyList<ValidationIssue> issues,
        out IReadOnlyList<WoundRepairPacket> packets)
    {
        packets = Array.Empty<WoundRepairPacket>();
        var repairable = issues.Where(IsRepairableIssue).ToArray();
        if (repairable.Length == 0)
            return true;
        if (repairable.Any(static issue => issue.WoundRepairContext is null) ||
            issues.Any(static issue =>
                issue.WoundRepairContext is not null && !IsRepairableIssue(issue)))
        {
            return false;
        }

        var contexts = repairable
            .Select(static issue => issue.WoundRepairContext!)
            .ToArray();
        var requestContext = contexts[0];
        if (contexts.Any(context =>
                !string.Equals(
                    context.SessionId,
                    requestContext.SessionId,
                    StringComparison.Ordinal) ||
                !string.Equals(
                    context.RequestId,
                    requestContext.RequestId,
                    StringComparison.Ordinal) ||
                !string.Equals(
                    context.SnapshotToken,
                    requestContext.SnapshotToken,
                    StringComparison.Ordinal)))
        {
            return false;
        }

        var candidates = new List<WoundRepairCandidateInput>();
        foreach (var group in repairable.GroupBy(
                     static issue => issue.WoundRepairContext!.CandidateRef,
                     StringComparer.Ordinal))
        {
            var context = group.First().WoundRepairContext!;
            if (group.Any(issue =>
                    !context.HasSameAuthority(issue.WoundRepairContext!)))
            {
                return false;
            }

            var normalizedIssues = group.Select(static issue => new ValidationIssue(
                    issue.FilePath,
                    issue.Severity,
                    issue.Message,
                    issue.Code,
                    issue.Actor,
                    "wound_materialization",
                    issue.Expected,
                    issue.Actual,
                    issue.RepairHint,
                    issue.Category,
                    issue.RepairTargetFiles.ToArray()))
                .ToArray();
            candidates.Add(new WoundRepairCandidateInput(
                context.Kind,
                context.CandidateRef,
                context.SemanticFingerprint,
                context.OpportunityRef,
                context.SafeContext,
                context.AllowedDecisions,
                context.MinimumSeverity,
                context.MaximumSeverity,
                context.RejectedDecision,
                normalizedIssues,
                context.OpportunityAuthorityFingerprint));
        }

        return TryBuild(new WoundRepairBuildRequest(
            requestContext.SessionId,
            requestContext.RequestId,
            requestContext.SnapshotToken,
            candidates), out packets);
    }

    internal static bool IsRepairableIssue(ValidationIssue? issue) =>
        issue is not null &&
        issue.Severity == IssueSeverity.Error &&
        issue.Code is not null &&
        RepairableIssueCodes.Contains(issue.Code) &&
        (string.Equals(
             issue.Section,
             "wound_materialization",
             StringComparison.OrdinalIgnoreCase) ||
         string.Equals(
             issue.Section,
             "wound_response",
             StringComparison.OrdinalIgnoreCase));

    private static bool TryBuild(
        WoundRepairBuildRequest? request,
        out IReadOnlyList<WoundRepairPacket> packets)
    {
        packets = Array.Empty<WoundRepairPacket>();
        if (request == null ||
            !Exact(request.SessionId) ||
            !Exact(request.RequestId) ||
            !Exact(request.SnapshotToken) ||
            request.Candidates.Count is < 1 or > CandidateLimit ||
            !ExactAndConfusableUnique(request.Candidates.Select(static value =>
                value?.CandidateRef ?? string.Empty)))
        {
            return false;
        }

        var result = new List<WoundRepairPacket>(request.Candidates.Count);
        foreach (var candidate in request.Candidates)
        {
            if (candidate == null ||
                !TryBuildPacket(request, candidate, out var packet))
            {
                return false;
            }
            result.Add(packet);
        }

        packets = result.ToArray();
        return true;
    }

    private static bool TryBuildPacket(
        WoundRepairBuildRequest request,
        WoundRepairCandidateInput candidate,
        out WoundRepairPacket packet)
    {
        packet = null!;
        if (!CandidateKinds.Contains(candidate.Kind) ||
            !Exact(candidate.CandidateRef) ||
            !ResourceMaterializationContract.IsAuthorityFingerprint(
                candidate.SemanticFingerprint) ||
            (candidate.OpportunityAuthorityFingerprint is not null &&
             !ResourceMaterializationContract.IsAuthorityFingerprint(
                 candidate.OpportunityAuthorityFingerprint)) ||
            !Exact(candidate.OpportunityRef) ||
            !ValidAllowedDecisions(candidate.AllowedDecisions) ||
            !TrySeverityRank(candidate.MinimumSeverity, out var minimumSeverity) ||
            !TrySeverityRank(candidate.MaximumSeverity, out var maximumSeverity) ||
            minimumSeverity > maximumSeverity ||
            candidate.Issues.Count == 0)
        {
            return false;
        }

        var safeContextSource = candidate.SafeContext.DeepClone().AsObject();
        var rejectedDecision = candidate.RejectedDecision.DeepClone().AsObject();
        var secrets = new HashSet<string>(StringComparer.Ordinal);
        CollectSensitiveValues(safeContextSource, secrets);
        CollectSensitiveValues(rejectedDecision, secrets);

        if (!TryProjectSafeContext(safeContextSource, out var safeContext) ||
            !TryReadExactString(
                rejectedDecision,
                "opportunityRef",
                out var decisionOpportunityRef) ||
            !string.Equals(
                decisionOpportunityRef,
                candidate.OpportunityRef,
                StringComparison.Ordinal) ||
            !TryReadExactString(rejectedDecision, "decision", out var decision) ||
            !candidate.AllowedDecisions.Contains(decision, StringComparer.Ordinal) ||
            !TryReadExactString(rejectedDecision, "woundRef", out var woundRef) ||
            rejectedDecision["proposal"] is not JsonObject proposalSource)
        {
            return false;
        }

        var issues = new List<WoundRepairPacketIssue>(candidate.Issues.Count);
        var offendingProposalPaths = new List<string>(candidate.Issues.Count);
        foreach (var issue in candidate.Issues)
        {
            if (!TryProjectIssue(
                    issue,
                    candidate.MinimumSeverity,
                    candidate.MaximumSeverity,
                    secrets,
                    out var projected))
                return false;
            issues.Add(projected);
            if (projected.Path.StartsWith("proposal.", StringComparison.Ordinal))
                offendingProposalPaths.Add(projected.Path["proposal.".Length..]);
        }

        var preservedProposal = ProjectProposal(proposalSource);
        foreach (var path in offendingProposalPaths.Distinct(StringComparer.Ordinal))
            RemovePath(preservedProposal, path);

        var correctOnly = issues
            .Select(static issue => issue.Path)
            .Distinct(StringComparer.Ordinal)
            .Select(static path => (JsonNode)path)
            .ToArray();
        var requiredResponseShape = new JsonObject
        {
            ["woundDecisions"] = new JsonArray(new JsonObject
            {
                ["opportunityRef"] = candidate.OpportunityRef,
                ["decision"] = decision,
                ["woundRef"] = woundRef,
                ["proposal"] = new JsonObject
                {
                    ["base"] = "preservedProposal",
                    ["correctOnly"] = new JsonArray(correctOnly)
                }
            }),
            ["response"] =
                "complete final scene containing display.acquisitionNarration verbatim"
        };

        packet = new WoundRepairPacket(
            request.SessionId,
            request.RequestId,
            request.SnapshotToken,
            candidate.CandidateRef,
            candidate.SemanticFingerprint,
            issues,
            safeContext,
            preservedProposal,
            requiredResponseShape,
            rejectedDecision,
            candidate.OpportunityAuthorityFingerprint);
        return true;
    }

    private static bool TryProjectIssue(
        ValidationIssue? issue,
        string minimumSeverity,
        string maximumSeverity,
        IReadOnlySet<string> sensitiveValues,
        out WoundRepairPacketIssue projected)
    {
        projected = null!;
        if (issue == null ||
            issue.Severity != IssueSeverity.Error ||
            !string.Equals(
                issue.Section,
                "wound_materialization",
                StringComparison.OrdinalIgnoreCase) ||
            string.IsNullOrEmpty(issue.Code) ||
            !TryNormalizeIssuePath(issue.FilePath, out var path) ||
            !TryExpectedRange(
                path,
                issue.Code,
                minimumSeverity,
                maximumSeverity,
                out var expected))
        {
            return false;
        }

        var actual = Bound(issue.Actual ?? "missing");
        if (UnsafeEvidence(actual, sensitiveValues))
            actual = "redacted unsafe authority evidence";

        projected = new WoundRepairPacketIssue(
            path,
            issue.Code,
            expected,
            actual);
        return true;
    }

    private static bool TryNormalizeIssuePath(string? rawPath, out string path)
    {
        path = string.Empty;
        if (string.IsNullOrEmpty(rawPath) ||
            !string.Equals(rawPath, rawPath.Trim(), StringComparison.Ordinal) ||
            rawPath.Contains('\\') ||
            rawPath.StartsWith("game_state/", StringComparison.OrdinalIgnoreCase))
        {
            return false;
        }

        if (string.Equals(
                rawPath,
                "output/narrative_response.json.response",
                StringComparison.Ordinal))
        {
            path = "response";
            return true;
        }

        const string prefix = "woundDecisions[";
        const string proposalMarker = "].proposal.";
        if (!rawPath.StartsWith(prefix, StringComparison.Ordinal))
            return false;
        var markerIndex = rawPath.IndexOf(proposalMarker, prefix.Length, StringComparison.Ordinal);
        if (markerIndex < 0)
            return false;
        var ordinalToken = rawPath[prefix.Length..markerIndex];
        if (!int.TryParse(ordinalToken, out var ordinal) ||
            ordinal < 0 ||
            !string.Equals(ordinalToken, ordinal.ToString(), StringComparison.Ordinal))
        {
            return false;
        }

        var semanticTail = rawPath[(markerIndex + proposalMarker.Length)..];
        if (!TryParsePath(semanticTail, out var segments) ||
            segments.OfType<string>().Any(SensitiveKeys.Contains))
        {
            return false;
        }

        path = "proposal." + semanticTail;
        return true;
    }

    private static bool TryExpectedRange(
        string path,
        string code,
        string minimumSeverity,
        string maximumSeverity,
        out string expected)
    {
        expected = string.Empty;
        if (path == "proposal.owner" && code == "wound_response_unknown_field")
        {
            expected = "owner omitted; the client keeps the sealed target";
            return true;
        }
        if (path.StartsWith("proposal.", StringComparison.Ordinal) &&
            code == "wound_response_unknown_field")
        {
            expected =
                "remove only this unknown GM-authored field and preserve every valid sibling";
            return true;
        }
        if (path == "proposal.severity" && code == "wound_severity_above_opportunity")
        {
            expected = string.Equals(
                minimumSeverity,
                maximumSeverity,
                StringComparison.Ordinal)
                ? minimumSeverity
                : minimumSeverity + "-" + maximumSeverity;
            return true;
        }
        if (path.EndsWith(".root.slots", StringComparison.Ordinal) &&
            code == "wound_consequence_slot_budget_exceeded")
        {
            expected = "at most 2 independently understandable consequence slots";
            return true;
        }
        if (path.EndsWith(".definition.links", StringComparison.Ordinal) &&
            code == "wound_materialization_effect_binding_invalid")
        {
            expected = "one complete wound-owned effect definition with response-local links";
            return true;
        }
        if (path.StartsWith("proposal.treatment.routes[", StringComparison.Ordinal) &&
            code == "wound_materialization_missing_field")
        {
            expected = "one complete treatment route in the closed wound schema";
            return true;
        }
        if (path.EndsWith(".payload.resource", StringComparison.Ordinal) &&
            code == "wound_consequence_resource_bound_missing")
        {
            expected = "one accepted resource and a bounded quantum-aligned amount";
            return true;
        }
        if (path == "response" && code is
                "wound_acquisition_narration_missing" or
                "wound_acquisition_narration_contradiction")
        {
            expected = "the exact acquisition narration inside the final scene";
            return true;
        }
        if (path.StartsWith("proposal.", StringComparison.Ordinal) &&
            code == "wound_materialization_invalid_field")
        {
            expected = "remove only this invalid field and preserve every valid sibling";
            return true;
        }
        return false;
    }

    private static bool TryProjectSafeContext(
        JsonObject source,
        out JsonObject safeContext)
    {
        safeContext = new JsonObject();
        foreach (var field in SafeContextFields)
        {
            if (source[field] is not JsonValue value ||
                !value.TryGetValue<string>(out var text) ||
                !Readable(text))
            {
                return false;
            }
            safeContext[field] = text;
        }
        return true;
    }

    private static JsonObject ProjectProposal(JsonObject source)
    {
        var result = new JsonObject();
        foreach (var field in ProposalFields)
        {
            if (!source.TryGetPropertyValue(field, out var value))
                continue;
            result[field] = Sanitize(value);
        }
        return result;
    }

    private static JsonNode? Sanitize(JsonNode? node)
    {
        if (node is JsonObject sourceObject)
        {
            var result = new JsonObject();
            foreach (var pair in sourceObject)
            {
                if (!SensitiveKeys.Contains(pair.Key))
                    result[pair.Key] = Sanitize(pair.Value);
            }
            return result;
        }
        if (node is JsonArray sourceArray)
        {
            var result = new JsonArray();
            foreach (var value in sourceArray)
                result.Add(Sanitize(value));
            return result;
        }
        return node?.DeepClone();
    }

    private static void CollectSensitiveValues(
        JsonNode? node,
        ISet<string> values,
        bool underSensitiveKey = false)
    {
        if (node is JsonObject sourceObject)
        {
            foreach (var pair in sourceObject)
            {
                CollectSensitiveValues(
                    pair.Value,
                    values,
                    underSensitiveKey || SensitiveKeys.Contains(pair.Key));
            }
            return;
        }
        if (node is JsonArray sourceArray)
        {
            foreach (var element in sourceArray)
                CollectSensitiveValues(element, values, underSensitiveKey);
            return;
        }
        if (underSensitiveKey &&
            node is JsonValue value &&
            value.TryGetValue<string>(out var text) &&
            !string.IsNullOrEmpty(text))
        {
            values.Add(text);
        }
    }

    private static bool UnsafeEvidence(
        string value,
        IReadOnlySet<string> sensitiveValues)
    {
        if (value.Contains("game_state/", StringComparison.OrdinalIgnoreCase) ||
            value.Contains("sha256:", StringComparison.OrdinalIgnoreCase) ||
            SensitiveKeys.Any(key => value.Contains(key, StringComparison.OrdinalIgnoreCase)))
        {
            return true;
        }
        return sensitiveValues.Any(secret =>
            value.Contains(secret, StringComparison.Ordinal));
    }

    internal static void RemovePath(JsonObject root, string path)
    {
        if (!TryParsePath(path, out var segments) || segments.Count == 0)
            return;

        JsonNode? current = root;
        for (var index = 0; index < segments.Count - 1; index++)
        {
            current = segments[index] switch
            {
                string property when current is JsonObject currentObject =>
                    currentObject[property],
                int ordinal when current is JsonArray currentArray &&
                                 ordinal >= 0 && ordinal < currentArray.Count =>
                    currentArray[ordinal],
                _ => null
            };
            if (current == null)
                return;
        }

        switch (segments[^1])
        {
            case string property when current is JsonObject currentObject:
                currentObject.Remove(property);
                break;
            case int ordinal when current is JsonArray currentArray &&
                                  ordinal >= 0 && ordinal < currentArray.Count:
                currentArray.RemoveAt(ordinal);
                break;
        }
    }

    internal static bool TryParsePath(
        string value,
        out IReadOnlyList<object> segments)
    {
        var parsed = new List<object>();
        var position = 0;
        while (position < value.Length)
        {
            var propertyStart = position;
            while (position < value.Length && value[position] is not '.' and not '[')
                position++;
            if (position == propertyStart)
            {
                segments = Array.Empty<object>();
                return false;
            }
            var property = value[propertyStart..position];
            if (!ResourceMaterializationContract.IsExactIdentifier(property) ||
                property.IndexOfAny(new[] { ']', '/', '\\' }) >= 0)
            {
                segments = Array.Empty<object>();
                return false;
            }
            parsed.Add(property);

            while (position < value.Length && value[position] == '[')
            {
                var close = value.IndexOf(']', position + 1);
                if (close < 0)
                {
                    segments = Array.Empty<object>();
                    return false;
                }
                var token = value[(position + 1)..close];
                if (!int.TryParse(token, out var ordinal) ||
                    ordinal < 0 ||
                    !string.Equals(token, ordinal.ToString(), StringComparison.Ordinal))
                {
                    segments = Array.Empty<object>();
                    return false;
                }
                parsed.Add(ordinal);
                position = close + 1;
            }

            if (position == value.Length)
                break;
            if (value[position] != '.' || ++position == value.Length)
            {
                segments = Array.Empty<object>();
                return false;
            }
        }

        segments = parsed;
        return parsed.Count > 0;
    }

    private static bool ValidAllowedDecisions(IReadOnlyList<string> values) =>
        (values.Count == 2 &&
         string.Equals(values[0], "none", StringComparison.Ordinal) &&
         string.Equals(values[1], "materialize", StringComparison.Ordinal)) ||
        (values.Count == 1 &&
         string.Equals(values[0], "materialize", StringComparison.Ordinal));

    private static bool TrySeverityRank(string? value, out int rank)
    {
        rank = value switch
        {
            "I" => 1,
            "II" => 2,
            "III" => 3,
            "IV" => 4,
            _ => 0
        };
        return rank != 0;
    }

    private static bool TryReadExactString(
        JsonObject source,
        string property,
        out string value)
    {
        value = source[property] is JsonValue jsonValue &&
                jsonValue.TryGetValue<string>(out var text)
            ? text
            : string.Empty;
        return Exact(value);
    }

    private static bool Exact(string? value) =>
        ResourceMaterializationContract.IsExactIdentifier(value);

    private static bool Readable(string? value) =>
        Exact(value) && value!.Length <= 2_048;

    private static bool ExactAndConfusableUnique(IEnumerable<string> values)
    {
        var exact = new HashSet<string>(StringComparer.Ordinal);
        var confusable = new HashSet<string>(StringComparer.Ordinal);
        foreach (var value in values)
        {
            if (!Exact(value) ||
                !exact.Add(value) ||
                !confusable.Add(MortalLocationIdentityState.BuildConfusableKey(value)))
            {
                return false;
            }
        }
        return true;
    }

    private static string Bound(string value) =>
        value.Length <= EvidenceLimit ? value : value[..EvidenceLimit];
}
